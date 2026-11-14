param([string]$Unity='')
$ErrorActionPreference='Stop'
$duelRoot=Split-Path $PSScriptRoot -Parent
& dotnet run --project (Join-Path $duelRoot 'tests\Multiplayer\Multiplayer.Checks.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Core simulation checks failed.' }
& (Join-Path $PSScriptRoot 'Build-Duel.ps1') -Unity $Unity
foreach ($duelMap in @('L02','L07','L16','L17')) {
    & (Join-Path $PSScriptRoot 'Test-DuelPlayers.ps1') -Map $duelMap
}
& (Join-Path $PSScriptRoot 'Test-DuelMenus.ps1')
Write-Output 'PASS: final player built, all four local two-player integration proofs and native menu checks passed.'
