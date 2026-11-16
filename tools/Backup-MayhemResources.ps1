param([string]$Version = '0.2.11')
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$destination = Join-Path $repository ".local/converted-map-backups/$Version"
$staging = Join-Path $destination 'resources'
New-Item -ItemType Directory -Path $staging -Force | Out-Null
$paths = @(
    'source/Assets/OpenZ/Scenes/L19_Mayhem_Converted.unity',
    'source/Assets/OpenZ/Scenes/L19_Mayhem_Converted.unity.meta',
    'source/Assets/OpenZ/Resources/OpenZ/Maps/L19_grid.json',
    'source/Assets/OpenZ/Resources/OpenZ/Maps/L19_grid.json.meta'
)
foreach ($folder in @('Materials/Converted', 'Models/Generated/Converted')) {
    $assetPath = Join-Path $repository "source/Assets/OpenZ/$folder"
    $paths += Get-ChildItem -LiteralPath $assetPath -Recurse -File | ForEach-Object { $_.FullName.Substring($repository.Length + 1) }
    if (Test-Path -LiteralPath "$assetPath.meta") { $paths += "source/Assets/OpenZ/$folder.meta" }
}
$manifest = [ordered]@{}
foreach ($relative in $paths) {
    $original = Join-Path $repository $relative
    $copy = Join-Path $staging $relative
    New-Item -ItemType Directory -Path (Split-Path $copy) -Force | Out-Null
    Copy-Item -LiteralPath $original -Destination $copy -Force
    $manifest[$relative.Replace('\', '/')] = (Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $staging 'SHA256-MANIFEST.json') -Encoding utf8
$archive = Join-Path $destination "OpenZ-VK-$Version-MayhemResources.zip"
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $archive -Force
Get-Item -LiteralPath $archive | Select-Object FullName, Length
Get-FileHash -LiteralPath $archive -Algorithm SHA256
