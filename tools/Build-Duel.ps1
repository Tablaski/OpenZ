param([string]$Unity = '', [string]$OutputDirectory = '', [switch]$ShowProgress)
$ErrorActionPreference = 'Stop'
$duelRoot = Split-Path $PSScriptRoot -Parent
$duelProject = Join-Path $duelRoot 'source'
if (-not (Test-Path -LiteralPath (Join-Path $duelProject 'Packages/org.openz.datachannel/Plugins/x86_64/datachannel.dll'))) {
    throw 'The WebRTC native dependency is missing. Run tools/Build-DuelNative.ps1 first.'
}
if (-not (Test-Path -LiteralPath (Join-Path $duelProject 'Assets\OpenZ\Scenes\MainMenu.unity'))) {
    throw 'The local Unity resources are missing. Complete the one-time setup in CONTRIBUTING.md before building.'
}
$duelVersion = ((Get-Content (Join-Path $duelProject 'ProjectSettings\ProjectVersion.txt') | Select-String '^m_EditorVersion: ').Line -split ': ')[1]
if (-not $Unity) { $Unity = $env:UNITY_EDITOR_PATH }
if (-not $Unity) {
    $duelCandidates = @(
        "D:\Unity\$duelVersion\Editor\Unity.exe",
        "${env:ProgramFiles}\Unity\Hub\Editor\$duelVersion\Editor\Unity.exe"
    )
    $Unity = $duelCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $Unity -or -not (Test-Path -LiteralPath $Unity)) {
    throw "Unity $duelVersion was not found. Pass -Unity 'C:\path\to\Unity.exe' or set UNITY_EDITOR_PATH."
}
$duelLogs = Join-Path $duelRoot 'tests\Multiplayer\.cli'
New-Item -ItemType Directory -Path $duelLogs -Force | Out-Null
$duelLogName = if ($OutputDirectory) { 'Unity-build-' + [IO.Path]::GetFileName([IO.Path]::GetFullPath($OutputDirectory)) + '.log' } else { 'Unity-build.log' }
$duelLog = Join-Path $duelLogs $duelLogName
$duelArguments = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $duelProject + '"'),
    '-executeMethod', 'OpenZ.Multiplayer.Editor.DuelBuild.Windows', '-logFile', ('"' + $duelLog + '"'))
if ($OutputDirectory) {
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    $duelArguments += @('-oz-duel-output', ('"' + $OutputDirectory + '"'))
}
$duelMutex = [Threading.Mutex]::new($false, 'Local\OpenZ-Duel-UnityBuild')
$duelOwnsMutex = $false
try {
    try { $duelOwnsMutex = $duelMutex.WaitOne(0) }
    catch [Threading.AbandonedMutexException] { $duelOwnsMutex = $true }
    if (-not $duelOwnsMutex) { throw 'Another Unity build is still running. Keep its window open and wait for SUCCESS; do not launch another build.' }
    # Unity also locks the project for editor sessions and builds started outside this launcher.
    # Check before starting a second instance, which would overwrite the running build's log.
    $duelLockPath = Join-Path $duelProject 'Temp\UnityLockfile'
    if (Test-Path -LiteralPath $duelLockPath) {
        try {
            $duelLockProbe = [IO.File]::Open($duelLockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            $duelLockProbe.Dispose()
        } catch [IO.IOException] {
            throw 'This project is already in use by Unity. A build may still be running: wait for its SUCCESS message. Otherwise close the Unity editor, then run BUILD-OPENZ.bat once. The current Unity process has not been stopped.'
        }
    }
    Write-Output ('Building the release. Progress log: ' + $duelLog)
    if ($ShowProgress) { [IO.File]::WriteAllText($duelLog, '') }
    $duelBuild = Start-Process -FilePath $Unity -ArgumentList $duelArguments -WorkingDirectory $duelRoot -WindowStyle Hidden -Wait:(-not $ShowProgress) -PassThru
    if ($ShowProgress) {
        $logReader = $null
        $elapsed = [Diagnostics.Stopwatch]::StartNew()
        $nextHeartbeat = 30
        try {
            do {
                $finished = $duelBuild.WaitForExit(1000)
                if (!$logReader) {
                    try {
                        $sharing = [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete
                        $logReader = [IO.StreamReader]::new([IO.File]::Open($duelLog, 'Open', 'Read', $sharing))
                    } catch [IO.IOException] {
                        # Unity may still be opening its log.
                    }
                }
                if ($logReader) {
                    while ($null -ne ($line = $logReader.ReadLine())) { Write-Host $line }
                }
                if (!$finished -and $elapsed.Elapsed.TotalSeconds -ge $nextHeartbeat) {
                    Write-Host ('Unity is still building ({0:hh\:mm\:ss} elapsed).' -f $elapsed.Elapsed)
                    $nextHeartbeat = $elapsed.Elapsed.TotalSeconds + 30
                }
            } until ($finished)
        } finally {
            if ($logReader) { $logReader.Dispose() }
        }
    }
    if ($duelBuild.ExitCode -ne 0) { throw "Unity build failed. Read $duelLog" }
    $duelResult = if ($OutputDirectory) { Join-Path $OutputDirectory 'BUILD_RESULT.txt' } else { Join-Path $duelRoot 'builds\OpenZ-Release\BUILD_RESULT.txt' }
    if (-not (Test-Path -LiteralPath $duelResult) -or -not (Get-Content -LiteralPath $duelResult -Raw).StartsWith('Succeeded')) {
        throw "Unity did not report a successful player build. Read $duelLog"
    }
    # Use the identity captured by Unity, rather than a tree that may have changed
    # while the player was compiling (for example installer-only work).
    $duelIdentity = Get-Content -LiteralPath (Join-Path (Split-Path $duelResult -Parent) 'FORK_BUILD.json') -Raw | ConvertFrom-Json
    $duelSourceRecord = @('OpenZ Windows Release', ('Source commit: ' + $duelIdentity.commit), ('Built UTC: ' + $duelIdentity.builtUtc), 'Development build: false', 'Script debugging: false', 'Profiler connection: false', ('Source modified when stamped: ' + [bool]$duelIdentity.dirty), ('Resource package: ' + $duelIdentity.resourcePackage))
    $duelSourceRecord | Set-Content -LiteralPath (Join-Path (Split-Path $duelResult -Parent) 'SOURCE_VERSION.txt') -Encoding UTF8
    Write-Output ('Built ' + (Join-Path (Split-Path $duelResult -Parent) 'openz-vk.exe') + '. Keep the complete output folder together.')
} finally {
    if ($duelOwnsMutex) { $duelMutex.ReleaseMutex() }
    $duelMutex.Dispose()
}
