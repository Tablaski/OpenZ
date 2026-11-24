[CmdletBinding()]
param(
    [string] $DataDirectory,
    [string] $ZodSource = 'D:\fenio-zod-engine',
    [string] $ZodSettings,
    [string] $CandidateProfile,
    [ValidateRange(1024, 65535)] [int] $Port = 8911,
    [switch] $NoBrowser
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (!$DataDirectory) { $DataDirectory = Join-Path $root '.local/combat-lab' }
$env:OPENZ_COMBAT_ROOT = $root
$env:OPENZ_COMBAT_DATA = [IO.Path]::GetFullPath($DataDirectory)
$env:OPENZ_COMBAT_ZOD_SOURCE = [IO.Path]::GetFullPath($ZodSource)
$env:OPENZ_COMBAT_ZOD_SETTINGS = if ($ZodSettings) { (Resolve-Path $ZodSettings).Path } else { $null }
$env:OPENZ_COMBAT_PROFILE = if ($CandidateProfile) { (Resolve-Path $CandidateProfile).Path } else { $null }
$env:OPENZ_COMBAT_PORT = "$Port"
$env:OPENZ_COMBAT_BROWSER = if ($NoBrowser) { '0' } else { '1' }
$mingwMarker = Join-Path $env:OPENZ_COMBAT_DATA 'zod-bin/runtime-path.txt'
if (Test-Path $mingwMarker) { $env:PATH = "$((Get-Content -Raw $mingwMarker).Trim());$env:PATH" }
dotnet run --project (Join-Path $PSScriptRoot 'CombatLab.csproj') --configuration Release --no-launch-profile
exit $LASTEXITCODE
