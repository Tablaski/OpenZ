param([Parameter(Mandatory=$true)][string]$OutputDirectory,[string]$Tag='v0.2.22')
$ErrorActionPreference='Stop'
$resourceRoot=Split-Path $PSScriptRoot -Parent
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$tracked=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
& git -C $resourceRoot ls-files | ForEach-Object {[void]$tracked.Add($_)}
if($LASTEXITCODE -ne 0){throw 'Cannot identify tracked source files.'}
$files=@(Get-ChildItem -LiteralPath (Join-Path $resourceRoot 'source/Assets') -Recurse -File | Where-Object { -not $tracked.Contains($_.FullName.Substring($resourceRoot.Length+1).Replace('\','/')) -and $_.Name -notlike 'fork_build_resources.json*' })
$plugin=Get-Item -LiteralPath (Join-Path $resourceRoot 'source/Packages/org.openz.datachannel/Plugins/x86_64/datachannel.dll')
$files+= $plugin
$archives=@(); $manifestFiles=@(); $group=@(); $length=0L; $part=0
function Write-ResourcePart([object[]]$ResourceFiles) {
    $script:part++
    $name='OpenZ-VK-UnityResources-part'+$script:part.ToString('00')+'.zip'
    $path=Join-Path $OutputDirectory $name
    if(Test-Path -LiteralPath $path){throw "Resource archive already exists: $path"}
    $zip=[IO.Compression.ZipFile]::Open($path,[IO.Compression.ZipArchiveMode]::Create)
    try{foreach($file in $ResourceFiles){$relative=$file.FullName.Substring($resourceRoot.Length+1).Replace('\','/');[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Fastest) | Out-Null;$script:manifestFiles+=@{path=$relative;length=$file.Length;sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant();archive=$name}}}finally{$zip.Dispose()}
    $item=Get-Item -LiteralPath $path
    if($item.Length -ge 2000000000){throw 'Resource archive exceeds GitHub asset size.'}
    $script:archives+= [pscustomobject]@{name=$name;length=$item.Length;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
    Write-Output ('Verified '+$name+' · '+$item.Length+' bytes')
}
foreach($file in ($files | Sort-Object FullName)) {
    if($group.Count -gt 0 -and $length+$file.Length -gt 1100000000){Write-ResourcePart $group;$group=@();$length=0L}
    $group+= $file;$length+=$file.Length
}
if($group.Count -gt 0){Write-ResourcePart $group}
$required=Get-Content -LiteralPath (Join-Path $resourceRoot 'SOURCE_RESOURCES.json') -Raw | ConvertFrom-Json
@{schema=2;version=$required.version;releaseTag=$Tag;archives=$archives;files=$manifestFiles} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'OpenZ-VK-source-resources.json') -Encoding utf8
Write-Output ('PASS: '+$files.Count+' local Unity resource files and native dependency packaged; tracked sources are retrieved separately.')
