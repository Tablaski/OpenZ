param([ValidateSet('L02','L07','L16','L17')][string]$Map='L02', [string]$Executable=(Join-Path $PSScriptRoot '..\builds\OpenZ-Duel\OpenZ-Duel.exe'))
$ErrorActionPreference='Stop'
$duelExecutable=(Resolve-Path -LiteralPath $Executable).Path
$proofFolder=Join-Path $PSScriptRoot ('..\tests\Multiplayer\.cli\proof-'+$Map+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$proofFolder=[IO.Path]::GetFullPath($proofFolder)
New-Item -ItemType Directory -Path $proofFolder | Out-Null
$players=@()
foreach ($role in @('host','guest')) {
    if ($role -eq 'guest') {
        $invitationDeadline=(Get-Date).AddSeconds(60)
        $invitationReady=$false
        while (-not $invitationReady) {
            try { $invitationReady=([IO.File]::ReadAllText((Join-Path $proofFolder 'invitation.txt')) -match '^openz://join\?.+&invite=.{32}$') } catch [IO.IOException] { }
            if ((Get-Date) -gt $invitationDeadline) { throw "Host did not publish its invitation. Inspect $proofFolder" }
            if (-not $invitationReady) { Start-Sleep -Milliseconds 100 }
        }
    }
    $duelArguments=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-force-d3d11','-oz-duel-proof',$role,('"'+$proofFolder+'"'),'-oz-duel-proof-map',$Map,'-logFile',('"'+(Join-Path $proofFolder ($role+'.log'))+'"'))
    $players+=Start-Process -FilePath $duelExecutable -ArgumentList $duelArguments -WindowStyle Hidden -PassThru
}
Write-Output $proofFolder
$proofDeadline=(Get-Date).AddSeconds(270)
while (@($players | Where-Object { -not $_.HasExited }).Count -gt 0) {
    if ((Get-Date) -gt $proofDeadline) { throw "Players did not exit; inspect $proofFolder" }
    Start-Sleep -Milliseconds 500
}
foreach ($role in @('host','guest')) {
    $result=Get-Content -LiteralPath (Join-Path $proofFolder ($role+'-result.txt')) -Raw
    Write-Output ($role+': '+$result)
    if (-not $result.StartsWith('PASS')) { throw "Integration proof failed: $role. Inspect $proofFolder" }
}
