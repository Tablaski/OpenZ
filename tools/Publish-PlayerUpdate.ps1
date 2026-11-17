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
$branch=if($Channel -eq 'stable'){'main'}else{'dev'}
if (-not $Tag) { $Tag=if($Channel -eq 'stable'){'v'+$stamp.version}else{'dev-v'+$stamp.version+'-'+$stamp.commit.Substring(0,10)} }
if ($Tag -notmatch '^[A-Za-z0-9._-]+$') { throw 'Use a simple version tag without path separators.' }
& git -C $updateRoot fetch origin $branch
if ($LASTEXITCODE -ne 0) { throw 'Cannot verify the published source branch.' }
& git -C $updateRoot merge-base --is-ancestor $stamp.commit ('origin/'+$branch)
if ($LASTEXITCODE -ne 0) { throw "Push the player source commit to origin/$branch first." }
$releaseDirectory=Join-Path $updateRoot ('builds/release-'+$stamp.version)
$updateDirectory=Join-Path $releaseDirectory ('update-'+$Channel)
$installerDirectory=Join-Path $updateRoot ('builds/installer-'+$stamp.version)
& (Join-Path $PSScriptRoot 'Build-Installer.ps1') -OutputDirectory $installerDirectory
if (-not (Test-Path -LiteralPath (Join-Path $updateDirectory 'OpenZ-VK-update.json'))) {
    & (Join-Path $installerDirectory 'OpenZ-VK-UpdateTools.exe') --pack $PlayerDirectory $updateDirectory $Channel $Tag $NotesFile
    if ($LASTEXITCODE -ne 0) { throw 'Update payload generation failed.' }
}
$manifest=Get-Content -LiteralPath (Join-Path $updateDirectory 'OpenZ-VK-update.json') -Raw | ConvertFrom-Json
if ($manifest.commit -ne $stamp.commit -or $manifest.releaseTag -ne $Tag -or $manifest.channel -ne $Channel) { throw 'An existing update package belongs to another build.' }
foreach($pack in $manifest.packs) {
    $path=Join-Path $updateDirectory $pack.name
    if ((Get-Item -LiteralPath $path).Length -ne $pack.length -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $pack.sha256) { throw 'Payload verification failed.' }
}
$assetPaths=@((Join-Path $installerDirectory 'OpenZ-VK-Installer.exe'),(Join-Path $updateDirectory 'OpenZ-VK-update.json'))+@($manifest.packs | ForEach-Object {Join-Path $updateDirectory $_.name})+@($AdditionalAssets | ForEach-Object {(Resolve-Path -LiteralPath $_).Path})
if (@($assetPaths | ForEach-Object {[IO.Path]::GetFileName($_)} | Select-Object -Unique).Count -ne $assetPaths.Count) { throw 'Release asset names must be unique.' }
$checksums=Join-Path $updateDirectory 'SHA256SUMS.txt'
@($assetPaths | ForEach-Object {(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($_)}) | Set-Content -LiteralPath $checksums -Encoding ascii
$assetPaths+= $checksums
if (-not $Publish) { Write-Output ('Prepared '+$assetPaths.Count+' verified assets for '+$Tag+'. Re-run with -Publish after review.'); return }
$env:GIT_TERMINAL_PROMPT='0'
$credential=@{}
("protocol=https`nhost=github.com`n`n" | & git -C $updateRoot credential fill) | ForEach-Object {if($_ -match '^([^=]+)=(.*)$'){$credential[$matches[1]]=$matches[2]}}
if (-not $credential.password) { throw 'GitHub authentication is unavailable.' }
$headers=@{Authorization='Bearer '+$credential.password;Accept='application/vnd.github+json';'User-Agent'='OpenZ-VK-release'}
$api='https://api.github.com/repos/Tablaski/OpenZ'
$existing=@(Invoke-RestMethod -Uri ($api+'/releases?per_page=100') -Headers $headers) | Where-Object tag_name -eq $Tag
if ($existing) { throw 'This release tag already exists. Do not silently replace an immutable channel version; use an explicitly authorized replacement procedure or increment the version.' }
$notes=Get-Content -LiteralPath $NotesFile -Raw
$release=Invoke-RestMethod -Method Post -Uri ($api+'/releases') -Headers $headers -ContentType 'application/json' -Body (@{tag_name=$Tag;target_commitish=$stamp.commit;name=('OpenZ-VK '+$stamp.version+$(if($Channel -eq 'dev'){' · Dev'}else{''}));body=$notes;draft=$true;prerelease=($Channel -eq 'dev')} | ConvertTo-Json)
$release.id | Set-Content -LiteralPath (Join-Path $releaseDirectory 'RELEASE_ID.txt') -Encoding ascii
$client=[Net.Http.HttpClient]::new(); $client.Timeout=[TimeSpan]::FromHours(2)
$client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$credential.password)
$client.DefaultRequestHeaders.UserAgent.ParseAdd('OpenZ-VK-release')
try {
    foreach($assetPath in $assetPaths) {
        $file=Get-Item -LiteralPath $assetPath
        if($file.Length -ge 2000000000){throw 'Release asset exceeds the upload limit.'}
        Write-Output ('Uploading '+$file.Name+' · '+$file.Length+' bytes')
        $stream=[IO.File]::OpenRead($assetPath); $content=[Net.Http.StreamContent]::new($stream)
        $content.Headers.ContentType=[Net.Http.Headers.MediaTypeHeaderValue]::new('application/octet-stream')
        try {
            $response=$client.PostAsync('https://uploads.github.com/repos/Tablaski/OpenZ/releases/'+$release.id+'/assets?name='+[Uri]::EscapeDataString($file.Name),$content).GetAwaiter().GetResult()
            try {$response.EnsureSuccessStatusCode() | Out-Null} finally {$response.Dispose()}
        } finally {$content.Dispose();$stream.Dispose()}
    }
} finally {$client.Dispose()}
$assets=@(Invoke-RestMethod -Uri ($api+'/releases/'+$release.id+'/assets?per_page=100') -Headers $headers)
foreach($path in $assetPaths) {
    $file=Get-Item -LiteralPath $path; $remote=@($assets | Where-Object name -eq $file.Name)
    $sha=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if($remote.Count -ne 1 -or $remote[0].size -ne $file.Length -or $remote[0].state -ne 'uploaded' -or ($remote[0].digest -and $remote[0].digest -ne ('sha256:'+$sha))){throw 'Remote release verification failed; the release remains a draft.'}
}
$published=Invoke-RestMethod -Method Patch -Uri ($api+'/releases/'+$release.id) -Headers $headers -ContentType 'application/json' -Body (@{draft=$false;make_latest=$(if($Channel -eq 'stable'){'true'}else{'false'})} | ConvertTo-Json)
if($published.draft){throw 'Publication failed.'}
$published.html_url | Set-Content -LiteralPath (Join-Path $releaseDirectory 'RELEASE_URL.txt') -Encoding ascii
Write-Output ('PUBLISHED '+$published.html_url)
