param(
    [Parameter(Mandatory=$true)][string]$PlayerDirectory,
    [ValidateSet('stable')][string]$Channel='stable',
    [string]$Tag='',
    [Parameter(Mandatory=$true)][string]$NotesFile,
    [string[]]$AdditionalAssets=@(),
    [switch]$Publish
)
$ErrorActionPreference='Stop'
$updateRoot=Split-Path $PSScriptRoot -Parent
$PlayerDirectory=(Resolve-Path -LiteralPath $PlayerDirectory).Path
$NotesFile=(Resolve-Path -LiteralPath $NotesFile).Path
$stamp=Get-Content -LiteralPath (Join-Path $PlayerDirectory 'FORK_BUILD.json') -Raw | ConvertFrom-Json
if ($stamp.dirty) { throw 'A public channel update must identify committed player sources.' }
if ($stamp.version -notmatch '^\d+\.\d+\.\d+$' -or $stamp.commit -notmatch '^[0-9a-f]{40}$') { throw 'Invalid build identity.' }
if (-not $Tag) { $Tag='v'+$stamp.version }
if ($Tag -cne ('v'+$stamp.version)) { throw 'A stable release tag must match the compiled game version.' }
if ($Publish) {
    foreach ($ref in 'refs/heads/main', "refs/tags/$Tag^{}") {
        $remoteRef = & git -C $updateRoot ls-remote --exit-code origin $ref
        if ($LASTEXITCODE -ne 0 -or ($remoteRef -split '\s+')[0] -ne $stamp.commit) {
            throw "Push $ref at the exact compiled game commit first."
        }
    }
}
$releaseDirectory=Join-Path $updateRoot ('builds/release-'+$stamp.version)
$updateDirectory=Join-Path $releaseDirectory ('update-'+$Channel)
$installerDirectory=Join-Path $updateRoot ('builds/installer-'+$stamp.version)
$installer = Join-Path $installerDirectory 'OpenZ-VK-Installer.exe'
$updateTools = Join-Path $installerDirectory 'OpenZ-VK-UpdateTools.exe'
$notes = [IO.File]::ReadAllText($NotesFile)
if (!(Test-Path -LiteralPath (Join-Path $updateDirectory 'OpenZ-VK-update.json'))) {
    & (Join-Path $PSScriptRoot 'Build-Installer.ps1') -OutputDirectory $installerDirectory
}
if (-not (Test-Path -LiteralPath (Join-Path $updateDirectory 'OpenZ-VK-update.json'))) {
    $pending = "$updateDirectory.pending-$([Guid]::NewGuid().ToString('N'))"
    & $updateTools --pack $PlayerDirectory $pending $Channel $Tag $NotesFile
    if ($LASTEXITCODE -ne 0) { throw 'Update payload generation failed.' }
    Move-Item -LiteralPath $pending -Destination $updateDirectory
}
$manifest=Get-Content -LiteralPath (Join-Path $updateDirectory 'OpenZ-VK-update.json') -Raw | ConvertFrom-Json
if ($manifest.commit -ne $stamp.commit -or $manifest.releaseTag -ne $Tag -or $manifest.channel -ne $Channel -or $manifest.notes -cne $notes) {
    throw 'The existing update package belongs to another build or uses different release notes. Preserve it elsewhere before preparing new assets.'
}
foreach($pack in $manifest.packs) {
    $path=Join-Path $updateDirectory $pack.name
    if ((Get-Item -LiteralPath $path).Length -ne $pack.length -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $pack.sha256) { throw 'Payload verification failed.' }
}
$assetPaths=@($installer,(Join-Path $updateDirectory 'OpenZ-VK-update.json'))+@($manifest.packs | ForEach-Object {Join-Path $updateDirectory $_.name})+@($AdditionalAssets | ForEach-Object {(Resolve-Path -LiteralPath $_).Path})
if (@($assetPaths | ForEach-Object {[IO.Path]::GetFileName($_)} | Select-Object -Unique).Count -ne $assetPaths.Count) { throw 'Release asset names must be unique.' }
$checksums=Join-Path $updateDirectory 'SHA256SUMS.txt'
$files = @($assetPaths | ForEach-Object {
    $item = Get-Item -LiteralPath $_
    if ($item.Length -le 0 -or $item.Length -ge 2GB) { throw "Invalid release asset size: $($item.Name)" }
    [pscustomobject]@{ File = $item; Hash = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
@($files | ForEach-Object { $_.Hash + '  ' + $_.File.Name }) | Set-Content -LiteralPath $checksums -Encoding ascii
if (-not $Publish) { Write-Output ('Prepared '+($files.Count + 1)+' verified assets for '+$Tag+'.'); return }
$files += [pscustomobject]@{ File = (Get-Item -LiteralPath $checksums); Hash = (Get-FileHash -LiteralPath $checksums -Algorithm SHA256).Hash.ToLowerInvariant() }
$credential=@{}
("protocol=https`nhost=github.com`npath=Tablaski/OpenZ.git`n`n" | & git -C $updateRoot credential fill) | ForEach-Object {if($_ -match '^([^=]+)=(.*)$'){$credential[$matches[1]]=$matches[2]}}
if ($LASTEXITCODE -ne 0) { throw 'GitHub credential lookup failed.' }
if (-not $credential.password) { throw 'GitHub authentication is unavailable.' }
$headers=@{Authorization='Bearer '+$credential.password;Accept='application/vnd.github+json';'User-Agent'='OpenZ-VK-release'}
$api='https://api.github.com/repos/Tablaski/OpenZ'
$releaseIdPath = Join-Path $releaseDirectory 'RELEASE_ID.txt'
$release = if (Test-Path -LiteralPath $releaseIdPath) {
    $id = (Get-Content -LiteralPath $releaseIdPath -Raw).Trim()
    if ($id -notmatch '^\d+$') { throw 'Invalid saved release ID.' }
    Invoke-RestMethod -Uri "$api/releases/$id" -Headers $headers
} else {
    $page = 1
    do {
        $releases = @(Invoke-RestMethod -Uri "$api/releases?per_page=100&page=$page" -Headers $headers | ForEach-Object { $_ })
        $existing = $releases | Where-Object tag_name -CEQ $Tag | Select-Object -First 1
        if ($existing) { break }
        $page++
    } while ($releases.Count -eq 100)
    if ($existing) { $existing } else {
        Invoke-RestMethod -Method Post -Uri "$api/releases" -Headers $headers -ContentType 'application/json; charset=utf-8' -Body (@{
            tag_name=$Tag; target_commitish=$stamp.commit; name="OpenZ-VK $($stamp.version)"; body=$notes; draft=$true; prerelease=$false
        } | ConvertTo-Json)
    }
}
if (!$release.draft -or $release.prerelease -or $release.tag_name -cne $Tag -or $release.body -cne $notes) {
    throw 'The existing GitHub release is published or differs from the prepared draft.'
}
$release.id | Set-Content -LiteralPath $releaseIdPath -Encoding ascii
$assetsUri = "$api/releases/$($release.id)/assets?per_page=100"
$uploaded = @(Invoke-RestMethod -Uri $assetsUri -Headers $headers | ForEach-Object { $_ })
if ($files.Count -gt 100 -or @($uploaded | Where-Object name -CNotIn $files.File.Name).Count) {
    throw 'The draft asset list differs from the prepared release.'
}

function Assert-RemoteAsset($Record, $Remote) {
    $file = $Record.File
    if ($Remote.size -ne $file.Length -or $Remote.state -ne 'uploaded' -or $Remote.digest -ne ('sha256:'+$Record.Hash)) {
        throw "GitHub asset verification failed for $($file.Name). The release remains a draft."
    }
}
Add-Type -AssemblyName System.Net.Http
$client=[Net.Http.HttpClient]::new(); $client.Timeout=[TimeSpan]::FromHours(2)
$client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$credential.password)
$client.DefaultRequestHeaders.UserAgent.ParseAdd('OpenZ-VK-release')
try {
    foreach($record in $files) {
        $file = $record.File
        $existingAsset = $uploaded | Where-Object name -CEQ $file.Name | Select-Object -First 1
        if ($existingAsset) { Assert-RemoteAsset $record $existingAsset; continue }
        Write-Output ('Uploading '+$file.Name+' · '+$file.Length+' bytes')
        $stream=[IO.File]::OpenRead($file.FullName); $content=[Net.Http.StreamContent]::new($stream)
        $content.Headers.ContentType=[Net.Http.Headers.MediaTypeHeaderValue]::new('application/octet-stream')
        try {
            $response=$client.PostAsync('https://uploads.github.com/repos/Tablaski/OpenZ/releases/'+$release.id+'/assets?name='+[Uri]::EscapeDataString($file.Name),$content).GetAwaiter().GetResult()
            try {$response.EnsureSuccessStatusCode() | Out-Null} finally {$response.Dispose()}
        } finally {$content.Dispose();$stream.Dispose()}
    }
} finally {$client.Dispose()}
$assets=@(Invoke-RestMethod -Uri $assetsUri -Headers $headers | ForEach-Object { $_ })
if ($assets.Count -ne $files.Count) { throw 'GitHub asset count differs from the prepared release.' }
foreach($record in $files) {
    $remote = @($assets | Where-Object name -CEQ $record.File.Name)
    if ($remote.Count -ne 1) { throw "GitHub is missing an unambiguous asset for $($record.File.Name)." }
    Assert-RemoteAsset $record $remote[0]
}
$published=Invoke-RestMethod -Method Patch -Uri ($api+'/releases/'+$release.id) -Headers $headers -ContentType 'application/json' -Body (@{draft=$false;make_latest=$(if($Channel -eq 'stable'){'true'}else{'false'})} | ConvertTo-Json)
if($published.draft){throw 'Publication failed.'}
$published.html_url | Set-Content -LiteralPath (Join-Path $releaseDirectory 'RELEASE_URL.txt') -Encoding ascii
Write-Output ('PUBLISHED '+$published.html_url)
