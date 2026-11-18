param([string]$Map='L02', [string]$Executable=(Join-Path $PSScriptRoot '..\builds\OpenZ-Release\openz-vk.exe'), [switch]$Online, [switch]$RelayOnly, [switch]$MatchControls)
$ErrorActionPreference='Stop'
$duelExecutable=(Resolve-Path -LiteralPath $Executable).Path
$proofPrefix = if ($Online) { 'proof-online-' } else { 'proof-' }
$proofFolder=Join-Path $PSScriptRoot ('..\tests\Multiplayer\.cli\'+$proofPrefix+$Map+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$proofFolder=[IO.Path]::GetFullPath($proofFolder)
New-Item -ItemType Directory -Path $proofFolder | Out-Null
$players=@()
foreach ($role in @('host','guest')) {
    if ($role -eq 'guest') {
        $invitationDeadline=(Get-Date).AddSeconds(60)
        $invitationReady=$false
        while (-not $invitationReady) {
            try {
                $invitationText = [IO.File]::ReadAllText((Join-Path $proofFolder 'invitation.txt'))
                $invitationReady = if ($Online) { $invitationText -match '/OPNZ-[A-F0-9]{24}$' } else { $invitationText -match '^openz://join\?.+&invite=.{32}$' }
            } catch [IO.IOException] { }
            if ((Get-Date) -gt $invitationDeadline) { throw "Host did not publish its invitation. Inspect $proofFolder" }
            if (-not $invitationReady) { Start-Sleep -Milliseconds 100 }
        }
    }
    $duelArguments=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-force-d3d11','-oz-duel-proof',$role,('"'+$proofFolder+'"'),'-oz-duel-proof-map',$Map,'-logFile',('"'+(Join-Path $proofFolder ($role+'.log'))+'"'))
    if ($Online) { $duelArguments += '-oz-duel-online-proof' }
    if ($RelayOnly) { $duelArguments += '-oz-duel-relay-only' }
    if ($MatchControls) { $duelArguments += '-oz-duel-match-controls' }
    $players+=Start-Process -FilePath $duelExecutable -ArgumentList $duelArguments -WindowStyle Hidden -PassThru
}
Write-Output $proofFolder
$proofDeadline=(Get-Date).AddSeconds(270)
while (@($players | Where-Object { -not $_.HasExited }).Count -gt 0) {
    if ((Get-Date) -gt $proofDeadline) {
        foreach ($testPlayer in $players) { if (-not $testPlayer.HasExited) { Stop-Process -Id $testPlayer.Id -Force } }
        throw "Players did not exit; inspect $proofFolder"
    }
    Start-Sleep -Milliseconds 500
}
foreach ($role in @('host','guest')) {
    $result=Get-Content -LiteralPath (Join-Path $proofFolder ($role+'-result.txt')) -Raw
    Write-Output ($role+': '+$result)
    if (-not $result.StartsWith('PASS')) { throw "Integration proof failed: $role. Inspect $proofFolder" }
}
