param([string]$ServiceUrl = '', [switch]$LoginOnly)
$ErrorActionPreference = 'Stop'
$duelRoot = Split-Path $PSScriptRoot -Parent
$serviceDirectory = Join-Path $duelRoot 'services/duel-signaling'
Push-Location -LiteralPath $serviceDirectory
try {
    if (-not (Test-Path -LiteralPath 'node_modules/wrangler')) {
        & npm ci --ignore-scripts --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'Cloudflare tooling installation failed.' }
    }
    if ($LoginOnly) { & npx wrangler login --scopes account:read user:read workers_scripts:write; return }
    & npm test
    if ($LASTEXITCODE -ne 0) { throw 'Invitation service checks failed.' }
    & npx wrangler deploy
    if ($LASTEXITCODE -ne 0) { throw 'Deployment failed. Run tools/Deploy-DuelService.ps1 -LoginOnly first.' }
    if (-not $ServiceUrl) { $ServiceUrl = Read-Host 'Paste the public https://...workers.dev URL printed above' }
    $serviceUri = [Uri]$ServiceUrl
    if ($serviceUri.Scheme -ne 'https' -or $serviceUri.UserInfo -or $serviceUri.Query -or $serviceUri.Fragment -or $serviceUri.AbsolutePath -ne '/') { throw 'Expected the public HTTPS service origin only.' }
    $normalizedUrl = $serviceUri.GetLeftPart([UriPartial]::Authority)
    $health = Invoke-RestMethod ($normalizedUrl + '/health')
    if ($health.service -ne 'OpenZ Duel' -or $health.version -ne 1) { throw 'Unexpected invitation service response.' }
    $configuration = Join-Path $duelRoot 'source/Assets/OpenZ/Scripts/Multiplayer/Unity/DuelServiceConfiguration.cs'
    # A public endpoint is safe in source. No account token or private code is written here.
    $configurationText = "namespace OpenZ.Multiplayer`n{`n    public static class DuelServiceConfiguration`n    {`n        public const string ServiceUrl = `"$normalizedUrl`";`n    }`n}`n"
    [IO.File]::WriteAllText($configuration, $configurationText, (New-Object Text.UTF8Encoding($false)))
    Write-Output "Invitation service configured: $normalizedUrl. Build the player to include this public endpoint. Universal TURN requires server-side secrets, the correct billing day and successful budget checks."
} finally { Pop-Location }
