param([string]$CMake = '')
$ErrorActionPreference = 'Stop'
$duelRoot = Split-Path $PSScriptRoot -Parent
$nativeWork = Join-Path $duelRoot '.local/build-tools'
New-Item -ItemType Directory -Path $nativeWork -Force | Out-Null
if (-not $CMake) {
    $cmakeCommand = Get-Command cmake -ErrorAction SilentlyContinue
    if ($cmakeCommand) { $CMake = $cmakeCommand.Source }
    else { $CMake = 'C:/Program Files/Microsoft Visual Studio/2022/Community/Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe' }
}
if (-not (Test-Path -LiteralPath $CMake)) { throw 'Install Visual Studio 2022 C++ tools and CMake, or pass -CMake.' }
function Invoke-NativeCommand([string]$Program, [string[]]$Arguments) {
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Native build command failed: $Program" }
}
$rtcSource = Join-Path $nativeWork 'libdatachannel-src'
$tlsSource = Join-Path $nativeWork 'mbedtls-src'
if (-not (Test-Path -LiteralPath $rtcSource)) {
    Invoke-NativeCommand git @('clone', '--branch', 'v0.24.6', '--depth', '1', '--recurse-submodules', '--shallow-submodules', 'https://github.com/paullouisageneau/libdatachannel.git', $rtcSource)
}
if (-not (Test-Path -LiteralPath $tlsSource)) {
    Invoke-NativeCommand git @('clone', '--branch', 'v3.6.5', '--depth', '1', '--recurse-submodules', '--shallow-submodules', 'https://github.com/Mbed-TLS/mbedtls.git', $tlsSource)
}
if ((& git -C $rtcSource rev-parse HEAD) -ne '6b1e2e620f1e37f0eafeee702eaea0043cb305fd') { throw 'Unexpected libdatachannel source revision.' }
if ((& git -C $tlsSource rev-parse HEAD) -ne 'e185d7fd85499c8ce5ca2a54f5cf8fe7dbe3f8df') { throw 'Unexpected Mbed TLS source revision.' }
Invoke-NativeCommand git @('-C', $tlsSource, 'submodule', 'update', '--init', '--depth', '1')
$tlsConfig = Join-Path $tlsSource 'include/mbedtls/mbedtls_config.h'
$tlsText = [IO.File]::ReadAllText($tlsConfig) -replace '(?m)^//#define MBEDTLS_SSL_DTLS_SRTP', '#define MBEDTLS_SSL_DTLS_SRTP'
[IO.File]::WriteAllText($tlsConfig, $tlsText, (New-Object Text.UTF8Encoding($false)))
$tlsBuild = Join-Path $nativeWork 'mbedtls-build'
$tlsInstall = Join-Path $nativeWork 'mbedtls-install'
Invoke-NativeCommand $CMake @('-S', $tlsSource, '-B', $tlsBuild, '-A', 'x64', '-DCMAKE_POLICY_DEFAULT_CMP0091=NEW', '-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded', '-DENABLE_TESTING=OFF', '-DENABLE_PROGRAMS=OFF', '-DGEN_FILES=OFF', "-DCMAKE_INSTALL_PREFIX=$tlsInstall")
Invoke-NativeCommand $CMake @('--build', $tlsBuild, '--config', 'Release', '--target', 'install', '--parallel', '8')
$rtcBuild = Join-Path $nativeWork 'datachannel-build'
Invoke-NativeCommand $CMake @('-S', $rtcSource, '-B', $rtcBuild, '-A', 'x64', '-DCMAKE_POLICY_DEFAULT_CMP0091=NEW', '-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded', '-DUSE_MBEDTLS=ON', '-DNO_MEDIA=ON', '-DNO_WEBSOCKET=ON', '-DNO_EXAMPLES=ON', '-DNO_TESTS=ON', "-DCMAKE_PREFIX_PATH=$tlsInstall")
Invoke-NativeCommand $CMake @('--build', $rtcBuild, '--config', 'Release', '--target', 'datachannel', '--parallel', '8')
$nativePackage = Join-Path $duelRoot 'source/Packages/org.openz.datachannel'
$nativePlugins = Join-Path $nativePackage 'Plugins/x86_64'
New-Item -ItemType Directory -Path $nativePlugins -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $rtcBuild 'Release/datachannel.dll') -Destination $nativePlugins
$notices = Join-Path $nativePackage 'Third Party Notices.txt'
$licenses = @(
    @('libdatachannel v0.24.6 (MPL-2.0)', (Join-Path $rtcSource 'LICENSE')),
    @('Mbed TLS v3.6.5 (Apache-2.0 OR GPL-2.0-or-later; distributed under Apache-2.0)', (Join-Path $tlsSource 'LICENSE')),
    @('libjuice (MPL-2.0)', (Join-Path $rtcSource 'deps/libjuice/LICENSE')),
    @('usrsctp (BSD-3-Clause)', (Join-Path $rtcSource 'deps/usrsctp/LICENSE.md')),
    @('plog (MIT)', (Join-Path $rtcSource 'deps/plog/LICENSE'))
)
$noticeText = "OpenZ WebRTC native dependencies. Upstream sources and credits are preserved.`nSources: https://github.com/paullouisageneau/libdatachannel/tree/v0.24.6 and https://github.com/Mbed-TLS/mbedtls/tree/v3.6.5`n"
foreach ($license in $licenses) {
    if (-not (Test-Path -LiteralPath $license[1])) { throw "Missing dependency license: $($license[1])" }
    $noticeText += "`n`n===== $($license[0]) =====`n" + [IO.File]::ReadAllText($license[1])
}
[IO.File]::WriteAllText($notices, $noticeText, (New-Object Text.UTF8Encoding($false)))
$nativeArchive = Join-Path $duelRoot 'builds/OpenZ-WebRTC-Native-0.24.6-Windows-x64.zip'
New-Item -ItemType Directory -Path (Split-Path $nativeArchive) -Force | Out-Null
Compress-Archive -Path (Join-Path $nativePackage '*') -DestinationPath $nativeArchive -Force
Get-FileHash -LiteralPath (Join-Path $nativePlugins 'datachannel.dll') -Algorithm SHA256
Write-Output "Native dependency backup: $nativeArchive"
