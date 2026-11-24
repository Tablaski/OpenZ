param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$installerRoot = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $installerRoot 'builds/installer' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework compiler is missing.' }
$logo = Join-Path $installerRoot 'source/Assets/OpenZ/Resources/OpenZ/Kit/Wordmarks/wm_openz_vk_menu.png'
if (-not (Test-Path -LiteralPath $logo)) { throw 'Restore the supplied OpenZ-VK logo from the local resource backup before building.' }
$common = @('/nologo','/optimize+','/platform:x64','/r:System.Net.Http.dll','/r:System.Web.Extensions.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll',('/resource:'+(Join-Path $installerRoot 'source/Assets/OpenZ/Scripts/Multiplayer/Unity/Editor/OzForkBuildStamp.cs')+',OpenZVK.BuildStamp'),('/resource:'+(Join-Path $PSScriptRoot 'installer/LegacyBuild.txt')+',OpenZVK.LegacyBuild'))
$core = Join-Path $PSScriptRoot 'installer/UpdateCore.cs'
& $compiler @common '/target:winexe' ('/out:' + (Join-Path $OutputDirectory 'OpenZ-VK-Installer.exe')) '/r:System.Windows.Forms.dll' '/r:System.Drawing.dll' ('/resource:' + $logo + ',OpenZVK.Logo') $core (Join-Path $PSScriptRoot 'installer/SourceCore.cs') (Join-Path $PSScriptRoot 'installer/WizardUi.cs') (Join-Path $PSScriptRoot 'installer/SourceWindow.cs') (Join-Path $PSScriptRoot 'installer/Installer.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
& $compiler @common '/target:exe' ('/out:' + (Join-Path $OutputDirectory 'OpenZ-VK-UpdateTools.exe')) $core (Join-Path $PSScriptRoot 'installer/SourceCore.cs') (Join-Path $PSScriptRoot 'installer/Packager.cs')
if ($LASTEXITCODE -ne 0) { throw 'Update tools compilation failed.' }
Write-Output ('Built standalone installer: ' + (Join-Path $OutputDirectory 'OpenZ-VK-Installer.exe'))
