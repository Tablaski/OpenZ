param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../.local/duel-private'))
$ErrorActionPreference = 'Stop'
$privateDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$duelRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $privateDirectory.StartsWith(($duelRoot + [IO.Path]::DirectorySeparatorChar), [StringComparison]::OrdinalIgnoreCase)) { throw 'Keep generated access files inside the ignored .local directory of this workspace.' }
if (-not $privateDirectory.StartsWith((Join-Path $duelRoot '.local/'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Private codes must be stored under .local/.' }
New-Item -ItemType Directory -Path $privateDirectory -Force | Out-Null
$relayHashes = @()
foreach ($player in @('owner', 'brother')) {
    $codePath = Join-Path $privateDirectory ($player + '-access.txt')
    if (Test-Path -LiteralPath $codePath) { throw "Access file already exists: $codePath. Do not rotate existing codes accidentally." }
}
foreach ($player in @('owner', 'brother')) {
    $bytes = New-Object byte[] 32
    $random = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $random.GetBytes($bytes) } finally { $random.Dispose() }
    $code = [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    [IO.File]::WriteAllText((Join-Path $privateDirectory ($player + '-access.txt')), $code)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $relayHashes += ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($code)))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}
[IO.File]::WriteAllText((Join-Path $privateDirectory 'PRIVATE_RELAY_HASHES.txt'), ($relayHashes -join ','))
Write-Output "Two private access files and the server hash allowlist were saved under $privateDirectory. Contents were not printed. Share only brother-access.txt with your brother. Never distribute these files with the player."
