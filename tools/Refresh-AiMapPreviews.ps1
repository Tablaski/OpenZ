[CmdletBinding()]
param(
    [string]$Unity,
    [string]$Ffmpeg,
    [string]$OutputDirectory,
    [switch]$ComparisonsOnly,
    [switch]$VideosOnly
)

$ErrorActionPreference = 'Stop'
if ($ComparisonsOnly -and $VideosOnly) { throw 'Choose one capture mode.' }
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'source'
if (!$Unity) {
    $editorVersion = ([regex]::Match((Get-Content "$project/ProjectSettings/ProjectVersion.txt" -Raw), 'm_EditorVersion: (\S+)')).Groups[1].Value
    $Unity = @($env:UNITY_EDITOR_PATH, "D:/Unity/$editorVersion/Editor/Unity.exe", "$env:ProgramFiles/Unity/Hub/Editor/$editorVersion/Editor/Unity.exe") |
        Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -First 1
}
if (!$Ffmpeg) {
    $command = Get-Command ffmpeg -ErrorAction SilentlyContinue
    $Ffmpeg = if ($command) { $command.Source } else { "$env:ProgramFiles/Shutter Encoder/app/Library/ffmpeg.exe" }
}
if (!$Unity -or !(Test-Path -LiteralPath $Unity -PathType Leaf)) { throw 'The project Unity Editor was not found.' }
if (!(Test-Path -LiteralPath $Ffmpeg -PathType Leaf)) { throw 'FFmpeg was not found.' }
$probe = Join-Path (Split-Path $Ffmpeg -Parent) 'ffprobe.exe'
if (!$ComparisonsOnly -and !(Test-Path -LiteralPath $probe -PathType Leaf)) { throw 'FFprobe is required beside FFmpeg.' }

$revision = & git -C $root rev-parse --short HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not identify the source revision.' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root ".local/ai-map-capture-$revision" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function Capture([string]$Name, [string[]]$Options) {
    $destination = Join-Path $OutputDirectory $Name
    $log = Join-Path $OutputDirectory "$Name.log"
    $args = @('-batchmode', '-force-d3d11', '-projectPath', ('"' + $project + '"'),
        '-executeMethod', 'OpenZ.EditorTools.OzAiMapPreviewCapture.Run',
        '-oz-preview-output', ('"' + $destination + '"'),
        '-oz-preview-ffmpeg', ('"' + $Ffmpeg + '"'), '-logFile', ('"' + $log + '"')) + $Options
    $process = Start-Process -FilePath $Unity -ArgumentList $args -WindowStyle Hidden -PassThru
    Write-Host "Capturing $Name. Unity log: $log"
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 16 | Write-Host }
        throw "Unity capture failed for $Name ($($process.ExitCode))."
    }
    return $destination
}

if (!$ComparisonsOnly) {
    $stage = Capture 'Videos' @()
    $settings = Get-Content "$project/ProjectSettings/EditorBuildSettings.asset" -Raw
    $ids = @([regex]::Matches($settings, '(?m)^  - enabled: 1\r?\n    path: Packages/org\.openz\.aimaps/Scenes/(?<id>\w+)_Converted\.unity\s*$') |
        ForEach-Object { $_.Groups['id'].Value } | Sort-Object -Unique)
    if (!$ids.Count) { throw 'No AI maps are enabled in the game build.' }
    $digests = [Collections.Generic.HashSet[string]]::new()
    foreach ($id in $ids) {
        $video = Join-Path $stage "${id}_preview.mp4"
        $poster = Join-Path $stage "${id}_poster.jpg"
        if (!(Test-Path -LiteralPath $video -PathType Leaf) -or !(Test-Path -LiteralPath $poster -PathType Leaf)) {
            throw "Incomplete capture for $id."
        }
        $info = & $probe -v error -select_streams 'v:0' -show_entries 'stream=codec_name,width,height,nb_frames' -of json $video
        if ($LASTEXITCODE -ne 0) { throw "Could not probe $id." }
        $stream = ($info -join "`n" | ConvertFrom-Json).streams[0]
        if ($stream.codec_name -ne 'h264' -or $stream.width -ne 960 -or $stream.height -ne 540 -or $stream.nb_frames -lt 100) {
            throw "Unexpected clip format or duration for $id."
        }
        if (!$digests.Add((Get-FileHash -LiteralPath $video -Algorithm SHA256).Hash)) { throw "Duplicate video for $id." }
        & $Ffmpeg -hide_banner -loglevel error -xerror -i $video -i $poster -map '0:v:0' -map '1:v:0' -f null -
        if ($LASTEXITCODE -ne 0) { throw "Could not decode preview assets for $id." }
    }
    $resources = Join-Path $root 'aimaps/Resources/OpenZ/AiMaps'
    $videoTemplate = Get-Content "$resources/Videos/SM02_preview.mp4.meta" -Raw
    $posterTemplate = Get-Content "$resources/Posters/SM02_poster.jpg.meta" -Raw
    foreach ($id in $ids) {
        $video = "$resources/Videos/${id}_preview.mp4"
        $poster = "$resources/Posters/${id}_poster.jpg"
        Copy-Item -LiteralPath "$stage/${id}_preview.mp4" -Destination $video
        Copy-Item -LiteralPath "$stage/${id}_poster.jpg" -Destination $poster
        foreach ($asset in @(@{Path=$video; Template=$videoTemplate}, @{Path=$poster; Template=$posterTemplate})) {
            $path = $asset.Path + '.meta'
            if (!(Test-Path -LiteralPath $path)) {
                $guid = [guid]::NewGuid().ToString('N')
                $meta = $asset.Template -replace '(?m)^guid: [a-f0-9]+', "guid: $guid" -replace '(?m)[ \t]+(?=\r?$)', ''
                [IO.File]::WriteAllText($path, $meta)
            }
        }
    }
    Write-Host "Integrated $($ids.Count) verified videos and posters in the AI Maps package."
}

if ($VideosOnly) { return }
$v3 = Capture 'V3' @('-oz-preview-comparisons', '-oz-ai-maps-v3')
$v4 = Capture 'V4' @('-oz-preview-comparisons', '-oz-ai-maps-v4')
foreach ($id in 'SM02', 'SM08', 'SM11', 'SM16', 'SM17') {
    $pair = Join-Path $OutputDirectory "$id-V3-left-V4-right.png"
    & $Ffmpeg -hide_banner -loglevel error -y -i "$v3/$id.png" -i "$v4/$id.png" -filter_complex 'hstack' -frames:v 1 -update 1 $pair
    if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $pair -PathType Leaf)) { throw "Could not compose $id." }
}
Write-Host "Five 4K comparisons saved in $OutputDirectory (V3 left, V4 right)."
