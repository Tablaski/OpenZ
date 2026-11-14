param([string]$Unity='D:\Unity\6000.3.27f2\Editor\Unity.exe')
$ErrorActionPreference='Stop'
$duelRoot=Split-Path $PSScriptRoot -Parent
$duelProject=Join-Path $duelRoot 'OpenZ_1.4_source'
$duelLog=Join-Path $duelRoot 'tests\Multiplayer\.cli\Unity-final-build.log'
& (Join-Path $PSScriptRoot 'Prepare-DuelProject.ps1') -ProjectPath $duelProject
$duelBuild=Start-Process -FilePath $Unity -ArgumentList @('-batchmode','-nographics','-quit','-projectPath',$duelProject,'-executeMethod','OpenZ.Multiplayer.Editor.DuelBuild.Windows','-logFile',$duelLog) -WindowStyle Hidden -Wait -PassThru
if ($duelBuild.ExitCode -ne 0) { throw "Unity build failed. Read $duelLog" }
foreach ($duelMap in @('L02','L07','L16','L17')) {
    & (Join-Path $PSScriptRoot 'Test-DuelPlayers.ps1') -Map $duelMap
}
& (Join-Path $PSScriptRoot 'Test-DuelMenus.ps1')
Write-Output 'PASS: final player built, all four local two-player integration proofs and native menu checks passed.'
