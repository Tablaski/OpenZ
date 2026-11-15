param([string]$ProjectPath=(Join-Path $PSScriptRoot '..\source'), [string]$UnityEditor='D:\Unity\6000.3.27f2\Editor')
$ErrorActionPreference='Stop'
$duelProject=(Resolve-Path -LiteralPath $ProjectPath).Path
$response=Join-Path $duelProject 'Library\Bee\artifacts\1900b0aPDevDbg.dag\Assembly-CSharp.rsp'
if (-not (Test-Path -LiteralPath $response)) { throw 'Build the Unity project once to generate its compiler response file.' }
$checkDirectory=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\tests\Multiplayer\.cli\unity-source-check'))
New-Item -ItemType Directory -Path $checkDirectory -Force | Out-Null
$lines=[IO.File]::ReadAllLines($response) | Where-Object { $_ -notmatch '^-(out|refout):' }
# Refresh source paths: scripts added since the last player build must be checked too.
$lines = @($lines | Where-Object { $_ -notmatch '^"Assets/OpenZ/Scripts/.*\.cs"$' })
$scriptRoot = Join-Path $duelProject 'Assets/OpenZ/Scripts'
$lines += @(Get-ChildItem -LiteralPath $scriptRoot -Filter '*.cs' -File -Recurse |
    Where-Object { $_.FullName -notmatch '[\\/]Editor[\\/]' } |
    ForEach-Object { '"' + $_.FullName.Substring($duelProject.Length + 1).Replace('\', '/') + '"' })
$lines+=('-out:"'+(Join-Path $checkDirectory 'Assembly-CSharp.dll')+'"')
$lines+=('-refout:"'+(Join-Path $checkDirectory 'Assembly-CSharp.ref.dll')+'"')
$checkResponse=Join-Path $checkDirectory 'runtime.rsp'
[IO.File]::WriteAllLines($checkResponse,$lines,(New-Object Text.UTF8Encoding($false)))
# Uses the actual Unity compiler and exact references from the last player build.
# Does not launch the Editor or any game, or replace the compiled player.
Push-Location -LiteralPath $duelProject
try {
    & (Join-Path $UnityEditor 'Data\netcorerun\netcorerun.exe') (Join-Path $UnityEditor 'Data\DotNetSdkRoslyn\csc.dll') ('@'+$checkResponse)
    if ($LASTEXITCODE -ne 0) { throw "Unity C# source check failed ($LASTEXITCODE)." }
} finally { Pop-Location }
Write-Output 'PASS: all Unity runtime scripts compile. This is a source check, not a player build or rendering test.'
