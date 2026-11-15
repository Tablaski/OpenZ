param([ValidateRange(1, 31)][int]$BillingDay)
$ErrorActionPreference = 'Stop'
if (-not $BillingDay) {
    $BillingDay = [int](Read-Host 'UTC day the Cloudflare billing period starts (1-31; check Billing first)')
    if ($BillingDay -lt 1 -or $BillingDay -gt 31) { throw 'Expected a billing day from 1 to 31.' }
}
Push-Location -LiteralPath (Join-Path $PSScriptRoot '../services/duel-signaling')
try {
    # Prompts go directly to Wrangler. Never write tokens into source or logs.
    Write-Output 'Enter the Cloudflare account ID in the Wrangler secret prompt.'
    & npx wrangler secret put CF_ACCOUNT_ID
    if ($LASTEXITCODE -ne 0) { throw 'Account ID configuration failed.' }
    Write-Output 'Enter an account-scoped Account Analytics: Read token in the Wrangler secret prompt.'
    & npx wrangler secret put CF_ANALYTICS_TOKEN
    if ($LASTEXITCODE -ne 0) { throw 'Analytics token configuration failed.' }
    [string]$BillingDay | & npx wrangler secret put TURN_BILLING_DAY
    if ($LASTEXITCODE -ne 0) { throw 'Billing day configuration failed.' }
    Write-Output 'Budget secrets saved server-side. Deploy the checked service with tools/Deploy-DuelService.ps1.'
} finally { Pop-Location }
