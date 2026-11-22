[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$NotesFile,
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$ChangelogEntryFile,
    [string]$ChangelogIntroFile,
    [string]$DiscordHighlightsFile,
    [switch]$DeclareStable,
    [string]$Unity = $env:UNITY_EDITOR_PATH,
    [switch]$DryRun,
    [switch]$PrepareOnly,
    [string]$ConfirmTag
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$github = 'https://api.github.com/repos/Tablaski/OpenZ'
$versionPath = 'source/Assets/OpenZ/Scripts/UI/OzForkVersion.cs'
$utf8 = New-Object Text.UTF8Encoding($false)

function Invoke-Git([string[]]$Arguments) {
    & git -C $root @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Git failed ($LASTEXITCODE): $($Arguments[0])." }
}

function Assert-Clean {
    if (Invoke-Git @('status', '--porcelain', '--untracked-files=all')) {
        throw 'The release requires a clean checkout. Preserve and review local changes first.'
    }
}

function Assert-Ancestor([string]$Base, [string]$Tip) {
    & git -C $root merge-base --is-ancestor $Base $Tip
    if ($LASTEXITCODE -ne 0) { throw "The release requires a fast-forward from $Base to $Tip." }
}

function Get-RemoteHead([string]$Branch) {
    $ref = Invoke-Git @('ls-remote', '--exit-code', 'origin', "refs/heads/$Branch")
    return ($ref -split '\s+')[0]
}

function Invoke-Check([string]$Name, [scriptblock]$Command) {
    $log = Join-Path $releaseDirectory "$Name.log"
    & $Command *> $log
    if ($LASTEXITCODE -ne 0) { throw "$Name failed. See $log" }
    Write-Output "PASS: $Name"
}

function Assert-ArchiveCoverage([string[]]$Archives, [string]$ManifestPath) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    $remaining = @{}
    foreach ($file in $manifest.files) { $remaining.Add($file.path, [long]$file.length) }
    foreach ($archive in $Archives) {
        $zip = [IO.Compression.ZipFile]::OpenRead($archive)
        try {
            foreach ($entry in $zip.Entries) {
                if (!$remaining.ContainsKey($entry.FullName) -or $remaining[$entry.FullName] -ne $entry.Length) {
                    throw "Unexpected, repeated or invalid ZIP entry: $($entry.FullName)"
                }
                $remaining.Remove($entry.FullName)
            }
        } finally { $zip.Dispose() }
    }
    if ($remaining.Count) { throw 'The Windows ZIPs do not cover the complete compiled game in the update manifest.' }
}

function New-GameArchives([string]$Player, [string]$Destination) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $files = @(Get-ChildItem -LiteralPath $Player -File -Recurse -Force | Where-Object {
        $_.FullName.Substring($Player.Length + 1) -notmatch '(^|[\\/])[^\\/]*(?:_BurstDebugInformation_DoNotShip|_BackUpThisFolder_ButDontShipItWithYourGame)(?:[\\/]|$)'
    } | Sort-Object FullName)
    $limit = 2GB - 16MB
    $groups = [Collections.Generic.List[object]]::new()
    $group = [Collections.Generic.List[object]]::new()
    $size = 0L
    foreach ($file in $files) {
        # Store without compression so part limits are predictable, including ZIP headers.
        $entrySize = $file.Length + 1024 + 8 * $file.FullName.Length
        if ($entrySize -ge $limit) { throw "File cannot fit in a GitHub release ZIP: $($file.Name)" }
        if ($size + $entrySize -ge $limit) {
            $groups.Add($group)
            $group = [Collections.Generic.List[object]]::new()
            $size = 0L
        }
        $group.Add($file)
        $size += $entrySize
    }
    if ($group.Count) { $groups.Add($group) }
    if (!$groups.Count) { throw 'The compiled game is empty.' }
    $pending = "$Destination.pending-$([Guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $pending | Out-Null
    for ($i = 0; $i -lt $groups.Count; $i++) {
        $path = Join-Path $pending ("OpenZ-VK-$Version-Windows-x64-part{0}.zip" -f ($i + 1))
        $zip = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($file in $groups[$i]) {
                $entry = $file.FullName.Substring($Player.Length + 1).Replace('\', '/')
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entry, [IO.Compression.CompressionLevel]::NoCompression) | Out-Null
            }
        } finally { $zip.Dispose() }
        if ((Get-Item -LiteralPath $path).Length -ge 2GB) { throw "ZIP exceeds the GitHub asset limit: $path" }
    }
    Move-Item -LiteralPath $pending -Destination $Destination
}

$branch = Invoke-Git @('branch', '--show-current')
if ($branch -cnotin @('dev', 'main')) { throw 'Switch to dev first, or use main to resume a prepared release.' }
foreach ($marker in 'MERGE_HEAD', 'CHERRY_PICK_HEAD', 'REVERT_HEAD', 'rebase-apply', 'rebase-merge', 'sequencer') {
    $markerPath = Invoke-Git @('rev-parse', '--path-format=absolute', '--git-path', $marker)
    if (Test-Path -LiteralPath $markerPath) { throw "Finish the current Git operation ($marker) first." }
}
Assert-Clean
foreach ($url in @((Invoke-Git @('remote', 'get-url', 'origin')), (Invoke-Git @('remote', 'get-url', '--push', 'origin')))) {
    if ($url -notmatch '^(https://github\.com/|git@github\.com:|ssh://git@github\.com/)Tablaski/OpenZ(?:\.git)?/?$') {
        throw 'origin must point to Tablaski/OpenZ for both fetch and push.'
    }
}
$notes = (Resolve-Path -LiteralPath $NotesFile).Path
$notesText = [IO.File]::ReadAllText($notes)
if ([string]::IsNullOrWhiteSpace($notesText)) { throw 'Supply reviewed English GitHub release notes.' }
$discordHighlights = if ($DiscordHighlightsFile) {
    [IO.File]::ReadAllText((Resolve-Path -LiteralPath $DiscordHighlightsFile).Path).Trim()
}
if (!$DryRun -and !$PrepareOnly -and [string]::IsNullOrWhiteSpace($discordHighlights)) {
    throw 'Supply reviewed English Discord highlights with -DiscordHighlightsFile.'
}

$dev = Invoke-Git @('rev-parse', 'refs/heads/dev')
$main = Invoke-Git @('rev-parse', 'refs/heads/main')
$remoteDev = if ($DryRun) { Invoke-Git @('rev-parse', 'refs/remotes/origin/dev') } else { Get-RemoteHead 'dev' }
$remoteMain = if ($DryRun) { Invoke-Git @('rev-parse', 'refs/remotes/origin/main') } else { Get-RemoteHead 'main' }
if ($dev -ne $remoteDev) { throw 'Local dev must match origin/dev.' }
Assert-Ancestor $remoteMain $main
Assert-Ancestor $main $dev
if (!$DryRun) {
    # Fetch updates ancestry objects and the release history without changing either local branch.
    Invoke-Git @('fetch', '--no-tags', 'origin', 'main', 'dev') | Out-Null
    if ((Get-RemoteHead 'dev') -ne $dev -or (Get-RemoteHead 'main') -ne $remoteMain) { throw 'The remote branches moved. Review and rerun.' }
}

$content = (Invoke-Git @('show', "dev:$versionPath")) -join "`n"
$versionPattern = '(?m)(public const string Number = ")(\d+\.\d+\.\d+)(";)'
$match = [regex]::Match($content, $versionPattern)
if (!$match.Success) { throw 'Could not read OzForkVersion.Number.' }
$sourceVersion = $match.Groups[2].Value
if (!$Version) { $Version = $sourceVersion }
if ([version]$Version -lt [version]$sourceVersion) { throw 'The release cannot lower the fork version.' }
$tag = "v$Version"
if ($ConfirmTag -and ($ConfirmTag -cne $tag -or $DryRun -or $PrepareOnly)) {
    throw "Use -ConfirmTag $tag only to confirm a reviewed release."
}
$pin = ((Invoke-Git @('show', 'dev:SOURCE_RESOURCES.json')) -join "`n") | ConvertFrom-Json
if ($pin.upstream -ne ($Version -replace '\.\d+$', '')) { throw 'The fork version must match the pinned upstream major/minor version.' }
$changelogPath = 'docs/CHANGELOG.md'
$changelog = (Invoke-Git @('show', "dev:$changelogPath")) -join "`n"
$latestHeading = [regex]::Match($changelog, '(?m)^## OpenZ-VK (\d+\.\d+\.\d+) \(.+\)$')
if (!$latestHeading.Success) { throw 'Could not find the existing OpenZ-VK changelog.' }
$changelogNeeded = $latestHeading.Groups[1].Value -ne $Version
if ($changelogNeeded) {
    if ([version]$Version -le [version]$latestHeading.Groups[1].Value -or !$ChangelogEntryFile -or !$ChangelogIntroFile) {
        throw "Commit the reviewed OpenZ-VK $Version entry first, or provide -ChangelogEntryFile and -ChangelogIntroFile for a new version."
    }
    $intro = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $ChangelogIntroFile).Path).Trim()
    $entry = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $ChangelogEntryFile).Path).Trim()
    if ($intro -notmatch 'ValKognito' -or $intro -notmatch 'discord\.gg/' -or !$entry -or $entry -match '(?m)^## ') {
        throw 'Supply a reviewed changelog introduction with credit and Discord link, and entry text without a version heading.'
    }
    $localDate = [TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTimeOffset]::UtcNow, 'Romance Standard Time')
    $dateLabel = $localDate.ToString('dd MMM yyyy', [Globalization.CultureInfo]::InvariantCulture)
    $nextChangelog = "# OpenZ change log`n`n$intro`n`n## OpenZ-VK $Version ($dateLabel)`n`n$entry`n`n" + $changelog.Substring($latestHeading.Index) + "`n"
}
$readme = (Invoke-Git @('show', 'dev:README.md')) -join "`n"
$readmePattern = '(?m)^\*\*OpenZ-VK \d+\.\d+\.\d+\*\*(?= is )'
if ($readme -notmatch $readmePattern) { throw 'Could not find the public README version. Review README.md first.' }
$metadataNeeded = $changelogNeeded -or $sourceVersion -ne $Version -or
    $readme -notmatch "(?m)^\*\*OpenZ-VK $([regex]::Escape($Version))\*\* is " -or
    $readme.Contains('extract both ZIPs from the same release')
$localTag = Invoke-Git @('tag', '--list', $tag)
if ($localTag -and (Invoke-Git @('rev-parse', "$tag^{commit}")) -ne $dev) { throw "$tag already identifies another commit." }
if ($metadataNeeded -and ($branch -ne 'dev' -or $localTag)) { throw 'Prepare reviewed release metadata on dev before tagging.' }

if ($DryRun) {
    Write-Output "Dry run using local origin references. Release $tag from $dev, fast-forward main from $main."
    if ($metadataNeeded) { Write-Output 'Update version metadata on dev and verify its source-only push.' }
    Write-Output 'Run release checks and the existing Unity build. Package complete Windows ZIPs, installer, payloads and checksums. Require manual review and explicit confirmation before pushing main and the tag.'
    return
}

$releaseMutex = [Threading.Mutex]::new($false, 'Local\OpenZ-VK-StablePublication')
$ownsReleaseMutex = $false
try {
    try { $ownsReleaseMutex = $releaseMutex.WaitOne(0) }
    catch [Threading.AbandonedMutexException] { $ownsReleaseMutex = $true }
    if (!$ownsReleaseMutex) { throw 'Another stable release session is running.' }

    $latest = Invoke-RestMethod -Uri "$github/releases/latest" -Headers @{'User-Agent' = 'OpenZ-VK-release'}
    if ($latest.tag_name -match '^v(\d+\.\d+\.\d+)$' -and [version]$Version -le [version]$Matches[1]) {
        throw "The release version must be newer than $($latest.tag_name)."
    }
    $remoteTag = @(Invoke-Git @('ls-remote', 'origin', "refs/tags/$tag", "refs/tags/$tag^{}"))
    if ($remoteTag.Count -and (($remoteTag[-1] -split '\s+')[0] -ne $dev -or $remoteMain -ne $dev -or $metadataNeeded)) {
        throw "$tag is already in use on origin."
    }

    $previousPin = ((Invoke-Git @('show', "${remoteMain}:SOURCE_RESOURCES.json")) -join "`n") | ConvertFrom-Json
    $packageResources = $pin.releaseTag -eq $tag -and ($pin.version -ne $previousPin.version -or $remoteMain -eq $dev)
    if (!$packageResources) {
        $resourceRelease = Invoke-RestMethod -Uri "$github/releases/tags/$($pin.releaseTag)" -Headers @{'User-Agent' = 'OpenZ-VK-release'}
        if ($pin.index -notin @($resourceRelease.assets.name)) { throw 'The pinned base resource index is missing from GitHub.' }
    }

    $sourceProject = Join-Path $root 'source'
    $unityVersion = (Select-String -LiteralPath (Join-Path $sourceProject 'ProjectSettings/ProjectVersion.txt') -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value
    if (!$Unity) {
        $Unity = @("D:\Unity\$unityVersion\Editor\Unity.exe", "${env:ProgramFiles}\Unity\Hub\Editor\$unityVersion\Editor\Unity.exe") |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
    if (!$Unity -or !(Test-Path -LiteralPath $Unity -PathType Leaf)) { throw "Unity $unityVersion is required. Pass -Unity with its executable path." }
    $Unity = (Resolve-Path -LiteralPath $Unity).Path
    $marker = Get-Content -LiteralPath (Join-Path $sourceProject 'Assets/OpenZ/Resources/OpenZ/resource_package.json') -Raw | ConvertFrom-Json
    if ($marker.version -ne $pin.version -or $marker.upstream -ne $pin.upstream) { throw 'Restore the pinned local Unity resources before preparing a release.' }

    if ($metadataNeeded) {
        $sourceFile = Join-Path $root $versionPath
        $readmeFile = Join-Path $root 'README.md'
        [IO.File]::WriteAllText($sourceFile, [regex]::Replace([IO.File]::ReadAllText($sourceFile), $versionPattern, '${1}' + $Version + '${3}'), $utf8)
        $readme = [regex]::Replace([IO.File]::ReadAllText($readmeFile), $readmePattern, "**OpenZ-VK $Version**")
        [IO.File]::WriteAllText($readmeFile, $readme.Replace('extract both ZIPs from the same release', 'extract all Windows ZIP parts from the same release'), $utf8)
        if ($changelogNeeded) { [IO.File]::WriteAllText((Join-Path $root $changelogPath), $nextChangelog, $utf8) }
        $paths = @(Invoke-Git @('diff', '--name-only', '--', $versionPath, 'README.md', $changelogPath))
        & (Join-Path $PSScriptRoot 'Push-Dev.ps1') -Message "Prepare OpenZ-VK $Version release metadata" -Paths $paths
        $dev = Get-RemoteHead 'dev'
    }
    Assert-Clean
    if ((Invoke-Git @('rev-parse', 'HEAD')) -ne $dev) { throw 'Check out the exact release commit on dev.' }

    $releaseDirectory = Join-Path $root "builds/release-$Version"
    $player = Join-Path $releaseDirectory 'compiled-game'
    $zipFolder = Join-Path $releaseDirectory 'distribution'
    $installerFolder = Join-Path $root "builds/installer-$Version"
    $updateTools = Join-Path $installerFolder 'OpenZ-VK-UpdateTools.exe'
    New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
    Invoke-Check 'simulation' { & dotnet run --project (Join-Path $root 'tests/Multiplayer/Multiplayer.Checks.csproj') -c Release }
    Invoke-Check 'webrtc-direct' { & dotnet run --project (Join-Path $root 'tests/WebRTC/WebRTC.Checks.csproj') -c Release }
    Invoke-Check 'webrtc-sfu-loopback' { & dotnet run --project (Join-Path $root 'tests/WebRTC/WebRTC.Checks.csproj') -c Release -- --sfu-loopback }
    $buildStamp = Join-Path $player 'FORK_BUILD.json'
    if (!(Test-Path -LiteralPath $buildStamp)) {
        if (Test-Path -LiteralPath $player) { throw "Preserve the interrupted output at $player elsewhere before retrying." }
        & (Join-Path $PSScriptRoot 'Build-Duel.ps1') -Unity $Unity -OutputDirectory $player
    }
    $stamp = Get-Content -LiteralPath $buildStamp -Raw | ConvertFrom-Json
    if ($stamp.dirty -or $stamp.commit -ne $dev -or $stamp.version -ne $Version -or $stamp.resourcePackage -ne $pin.version -or
        !(Get-Content -LiteralPath (Join-Path $player 'BUILD_RESULT.txt') -Raw).StartsWith('Succeeded')) {
        throw "The compiled game at $player does not match this clean release candidate. Preserve it elsewhere before retrying."
    }
    foreach ($file in 'openz-vk.exe', 'UnityPlayer.dll', 'Unit Editor/Open Unit Editor.bat', 'SOURCE_VERSION.txt') {
        if (!(Test-Path -LiteralPath (Join-Path $player $file) -PathType Leaf)) { throw "Missing compiled-game file: $file" }
    }
    Assert-Clean
    if (!(Test-Path -LiteralPath $zipFolder)) { New-GameArchives $player $zipFolder }
    $assets = @(Get-ChildItem -LiteralPath $zipFolder -File -Filter '*.zip' | Select-Object -ExpandProperty FullName)
    if (!$assets.Count) { throw 'No compiled-game ZIPs were prepared.' }
    if ($packageResources) {
        $resourceFolder = Join-Path $releaseDirectory 'source-resources'
        if (!(Test-Path -LiteralPath (Join-Path $resourceFolder $pin.index))) {
            & (Join-Path $PSScriptRoot 'Package-UnityResources.ps1') -OutputDirectory $resourceFolder -Tag $tag
        }
        $assets += @(Get-ChildItem -LiteralPath $resourceFolder -File | Select-Object -ExpandProperty FullName)
    }
    $publisher = Join-Path $PSScriptRoot 'Publish-PlayerUpdate.ps1'
    & $publisher -PlayerDirectory $player -Tag $tag -NotesFile $notes -AdditionalAssets $assets
    Assert-ArchiveCoverage @(Get-ChildItem -LiteralPath $zipFolder -File -Filter '*.zip' | Select-Object -ExpandProperty FullName) (Join-Path $releaseDirectory 'update-stable/OpenZ-VK-update.json')
    Invoke-Check 'installer' { & $updateTools --test }
    if ($PrepareOnly) { Write-Output "Prepared $tag at $releaseDirectory for manual validation."; return }

    Write-Output "Review the README, changelog introduction, both Changes & Updates tabs and pending checks in docs/PLAYER_VALIDATION.md using $player."
    Write-Output "Review player-facing release notes: $notes"
    if (!$ConfirmTag -and (Read-Host "When review is complete, type PUBLISH $tag") -cne "PUBLISH $tag") {
        throw 'Publication confirmation did not match.'
    }
    Assert-Clean
    if ((Get-RemoteHead 'dev') -ne $dev -or (Get-RemoteHead 'main') -ne $remoteMain -or
        (Invoke-Git @('rev-parse', 'HEAD')) -ne $dev) { throw 'Source or remote branches changed during review.' }
    if ($branch -ne 'main') { Invoke-Git @('switch', 'main') }
    Invoke-Git @('merge', '--ff-only', $dev)
    if (!$localTag) { Invoke-Git @('tag', '-a', $tag, '-m', "OpenZ-VK $Version", $dev) }
    Invoke-Git @('push', '--atomic', '--no-follow-tags', 'origin', "${dev}:refs/heads/main", "refs/tags/${tag}:refs/tags/$tag")
    $verifiedTag = Invoke-Git @('ls-remote', '--exit-code', 'origin', "refs/tags/$tag^{}")
    if ((Get-RemoteHead 'main') -ne $dev -or ($verifiedTag -split '\s+')[0] -ne $dev) { throw 'Remote main or the tag could not be verified.' }
    & $publisher -PlayerDirectory $player -Tag $tag -NotesFile $notes -AdditionalAssets $assets -Publish
    $published = Invoke-RestMethod -Uri "$github/releases/latest" -Headers @{'User-Agent' = 'OpenZ-VK-release'}
    if ($published.tag_name -ne $tag -or $published.draft -or $published.prerelease) { throw "Could not verify $tag as the latest public release." }
    $installerAsset = @($published.assets | Where-Object name -CEQ 'OpenZ-VK-Installer.exe')
    $expectedDownload = "https://github.com/Tablaski/OpenZ/releases/download/$tag/OpenZ-VK-Installer.exe"
    if ($installerAsset.Count -ne 1 -or $installerAsset[0].browser_download_url -cne $expectedDownload) {
        throw 'The published installer download URL could not be verified.'
    }
    Write-Output "Verified $($published.html_url)"
    $disk = [char]::ConvertFromUtf32(0x1F4BE)
    $warning = [string][char]0x26A0 + [char]0xFE0F
    $announcement = "OpenZ-VK $Version is out ! $expectedDownload. It includes :`n`n$discordHighlights`n`n" +
        "$disk Don't forget: you can update faster with the installer without redownloading everything."
    if (!$DeclareStable) { $announcement += "`n$warning Not fully tested version, please report bugs." }
    $announcementPath = Join-Path $releaseDirectory 'DISCORD_ANNOUNCEMENT.md'
    [IO.File]::WriteAllText($announcementPath, $announcement + "`n", $utf8)
    Write-Output "Discord announcement: $announcementPath"
} finally {
    if ($ownsReleaseMutex) { $releaseMutex.ReleaseMutex() }
    $releaseMutex.Dispose()
}
