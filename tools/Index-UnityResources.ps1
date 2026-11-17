param([Parameter(Mandatory=$true)][string]$Directory,[string]$Tag='v0.2.22')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$Directory=[IO.Path]::GetFullPath($Directory)
$markerZip=Join-Path $Directory 'OpenZ-VK-UnityResources-part06.zip'
if(-not(Test-Path -LiteralPath $markerZip)){
    $zip=[IO.Compression.ZipFile]::Open($markerZip,[IO.Compression.ZipArchiveMode]::Create)
    try{foreach($relative in @('source/Assets/OpenZ/Resources/OpenZ/resource_package.json','source/Assets/OpenZ/Resources/OpenZ/resource_package.json.meta')){
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $repo $relative),$relative,[IO.Compression.CompressionLevel]::Fastest)|Out-Null
    }}finally{$zip.Dispose()}
}
$archives=[Collections.Generic.List[object]]::new();$files=[Collections.Generic.List[object]]::new()
foreach($archive in (Get-ChildItem -LiteralPath $Directory -Filter 'OpenZ-VK-UnityResources-part*.zip' -File|Sort-Object Name)){
    $archives.Add(@{name=$archive.Name;length=$archive.Length;sha256=(Get-FileHash -LiteralPath $archive.FullName -Algorithm SHA256).Hash.ToLowerInvariant()})
    $zip=[IO.Compression.ZipFile]::OpenRead($archive.FullName)
    try{foreach($entry in $zip.Entries){if($entry.Name -eq '' -or $entry.FullName -match '/(fork_build_resources|Menu/build_info)\.json(\.meta)?$'){continue};$input=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create();try{$hash=[BitConverter]::ToString($sha.ComputeHash($input)).Replace('-','').ToLowerInvariant()}finally{$input.Dispose();$sha.Dispose()};$files.Add(@{path=$entry.FullName;length=$entry.Length;sha256=$hash;archive=$archive.Name})}}finally{$zip.Dispose()}
    Write-Output ('Indexed '+$archive.Name)
}
$required=Get-Content -LiteralPath (Join-Path $repo 'SOURCE_RESOURCES.json') -Raw|ConvertFrom-Json
@{schema=2;version=$required.version;releaseTag=$Tag;archives=@($archives.ToArray());files=@($files.ToArray())}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $Directory 'OpenZ-VK-source-resources.json') -Encoding utf8
Write-Output ('PASS: '+$files.Count+' resource identities indexed.')
