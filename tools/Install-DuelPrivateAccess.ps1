param([string]$AccessFile = (Join-Path $PSScriptRoot '../.local/duel-private/owner-access.txt'))
$ErrorActionPreference = 'Stop'
$code = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $AccessFile).Path).Trim()
if ($code.Length -lt 32 -or $code.Length -gt 256) { throw 'Invalid private access file.' }
$encrypted = [Security.Cryptography.ProtectedData]::Protect([Text.Encoding]::UTF8.GetBytes($code), $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
$directory = Join-Path $env:USERPROFILE 'AppData/LocalLow/OpenZ/OpenZ'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
[IO.File]::WriteAllBytes((Join-Path $directory 'duel-private-access.bin'), $encrypted)
Write-Output 'Private relay access installed for this Windows user. No code was printed.'
