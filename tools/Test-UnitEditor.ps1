param([string]$Executable=(Join-Path $PSScriptRoot '..\builds\OpenZ-Release\openz-vk.exe'))
$ErrorActionPreference = 'Stop'
$editorExecutable = (Resolve-Path -LiteralPath $Executable).Path
$editorProofFolder = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('..\tests\Multiplayer\.cli\unit-editor-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))))
New-Item -ItemType Directory -Path $editorProofFolder | Out-Null
Write-Output $editorProofFolder
$editorProof = Start-Process -FilePath $editorExecutable -ArgumentList @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-force-d3d11','-oz-duel-unit-editor-proof',('"' + $editorProofFolder + '"'),'-logFile',('"' + (Join-Path $editorProofFolder 'player.log') + '"')) -WindowStyle Hidden -PassThru
if (-not $editorProof.WaitForExit(90000)) {
    Stop-Process -Id $editorProof.Id -Force
    throw "Unit Editor proof did not exit within 90 seconds. Inspect $editorProofFolder"
}
$editorResult = Get-Content -LiteralPath (Join-Path $editorProofFolder 'result.txt') -Raw
Write-Output $editorResult
if ($editorProof.ExitCode -ne 0 -or -not $editorResult.StartsWith('PASS')) { throw "Unit Editor proof failed. Inspect $editorProofFolder" }
