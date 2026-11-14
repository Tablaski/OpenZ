param([string]$Executable = '')
$ErrorActionPreference = 'Stop'
if (-not $Executable) {
    $Executable=Join-Path $PSScriptRoot 'OpenZ-Duel.exe'
    if (-not (Test-Path -LiteralPath $Executable)) { $Executable=Join-Path $PSScriptRoot '..\builds\OpenZ-Duel\OpenZ-Duel.exe' }
}
$duelExecutable = (Resolve-Path -LiteralPath $Executable).Path
if ([IO.Path]::GetFileName($duelExecutable) -ne 'OpenZ-Duel.exe') { throw 'Expected OpenZ-Duel.exe.' }
# Per-user protocol registration. No administrator account or service is needed.
$protocol = 'HKCU:\Software\Classes\openz'
New-Item -Path $protocol -Force | Out-Null
Set-Item -LiteralPath $protocol -Value 'URL:OpenZ Duel invitation'
New-ItemProperty -LiteralPath $protocol -Name 'URL Protocol' -Value '' -PropertyType String -Force | Out-Null
New-Item -Path ($protocol+'\shell\open\command') -Force | Out-Null
Set-Item -LiteralPath ($protocol+'\shell\open\command') -Value ('"'+$duelExecutable+'" "%1"')
Write-Output 'OpenZ invitations registered for this Windows account. An invitation opens the join menu; click Join to connect.'
