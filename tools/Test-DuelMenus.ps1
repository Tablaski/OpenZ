param([string]$Executable=(Join-Path $PSScriptRoot '..\builds\OpenZ-Release\openz-vk.exe'))
$ErrorActionPreference='Stop'
$duelExecutable=(Resolve-Path -LiteralPath $Executable).Path
$menuProofFolder=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('..\tests\Multiplayer\.cli\menus-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))))
New-Item -ItemType Directory -Path $menuProofFolder | Out-Null
Write-Output $menuProofFolder
$menuProof=Start-Process -FilePath $duelExecutable -ArgumentList @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-force-d3d11','-oz-duel-menu-proof',('"'+$menuProofFolder+'"'),'-logFile',('"'+(Join-Path $menuProofFolder 'menus.log')+'"')) -WindowStyle Hidden -PassThru
if (-not $menuProof.WaitForExit(90000)) {
    Stop-Process -Id $menuProof.Id -Force
    throw "Menu proof did not exit within 90 seconds. Inspect $menuProofFolder"
}
$result=Get-Content -LiteralPath (Join-Path $menuProofFolder 'result.txt') -Raw
Write-Output $result
if ($menuProof.ExitCode -ne 0 -or -not $result.StartsWith('PASS')) { throw "Native menu proof failed. Inspect $menuProofFolder" }
