param([string]$Unity = '', [string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$duelRoot = Split-Path $PSScriptRoot -Parent
$duelProject = Join-Path $duelRoot 'source'
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
$duelLog = Join-Path $duelLogs 'Unity-build.log'
$duelArguments = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $duelProject + '"'),
    '-executeMethod', 'OpenZ.Multiplayer.Editor.DuelBuild.Windows', '-logFile', ('"' + $duelLog + '"'))
if ($OutputDirectory) {
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    $duelArguments += @('-oz-duel-output', ('"' + $OutputDirectory + '"'))
}
$duelBuild = Start-Process -FilePath $Unity -ArgumentList $duelArguments -WindowStyle Hidden -Wait -PassThru
if ($duelBuild.ExitCode -ne 0) { throw "Unity build failed. Read $duelLog" }
$duelResult = if ($OutputDirectory) { Join-Path $OutputDirectory 'BUILD_RESULT.txt' } else { Join-Path $duelRoot 'builds\OpenZ-Duel\BUILD_RESULT.txt' }
if (-not (Test-Path -LiteralPath $duelResult) -or -not (Get-Content -LiteralPath $duelResult -Raw).StartsWith('Succeeded')) {
    throw "Unity did not report a successful player build. Read $duelLog"
}
Write-Output ('Built ' + (Join-Path (Split-Path $duelResult -Parent) 'OpenZ-Duel.exe') + '. Keep the complete output folder together.')
