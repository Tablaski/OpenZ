param(
    [Parameter(Mandatory=$true)][string]$CompiledGame,
    [string]$OutputDirectory,
    [switch]$RelayOnly
)

$ErrorActionPreference = 'Stop'
$compiledPath = (Resolve-Path -LiteralPath $CompiledGame).Path
if ((Get-Item -LiteralPath $compiledPath).PSIsContainer) {
    $compiledPath = Join-Path $compiledPath 'openz-vk.exe'
}
if (-not (Test-Path -LiteralPath $compiledPath -PathType Leaf)) {
    throw 'Select the compiled game directory or its openz-vk.exe.'
}
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) ('builds/captures/ghost-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$capturePath = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $capturePath | Out-Null
if (Get-ChildItem -LiteralPath $capturePath | Select-Object -First 1) {
    throw 'Use an empty output directory for each run.'
}

$roles = 'host', 'guest1', 'guest2'
$clients = foreach ($role in $roles) {
    $options = @('-screen-fullscreen', '0', '-screen-width', '960', '-screen-height', '540',
        '-logFile', ('"' + (Join-Path $capturePath ($role + '.log')) + '"'),
        '-oz-duel-skirmish-proof', ('"' + $capturePath + '"'), '-oz-duel-proof-three-humans', $role,
        '-oz-duel-proof-ghost-follow')
    if ($RelayOnly) { $options += '-oz-duel-relay-only' }
    Start-Process -FilePath $compiledPath -ArgumentList $options -PassThru
}

$deadline = (Get-Date).AddMinutes(5)
while (@($clients | Where-Object { -not $_.HasExited }).Count -gt 0 -and (Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
}
foreach ($role in $roles) {
    $report = Join-Path $capturePath ($role + '-result.txt')
    if (-not (Test-Path -LiteralPath $report) -or -not (Get-Content -LiteralPath $report -Raw).StartsWith('PASS:')) {
        throw "The $role client did not pass. See $capturePath."
    }
}
Write-Output "Ghost verification passed. Reports and captures: $capturePath"
