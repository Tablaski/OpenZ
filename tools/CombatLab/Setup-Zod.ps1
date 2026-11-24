[CmdletBinding()]
param(
    [string] $ZodSource = 'D:\fenio-zod-engine',
    [string] $MsysRoot = 'C:\msys64',
    [string] $DataDirectory,
    [ValidateRange(1, 32)] [int] $Jobs = 4
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (!$DataDirectory) { $DataDirectory = Join-Path $root '.local/combat-lab' }
$revision = '5ccb43a'
$source = (Resolve-Path $ZodSource).Path
$bash = Join-Path $MsysRoot 'usr/bin/bash.exe'
if (!(Test-Path $bash)) { throw "MSYS2 is missing at $MsysRoot. See tools/CombatLab/README.md for the one-time setup." }
$buildRoot = Join-Path $DataDirectory ('zod-build-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Force -Path $buildRoot
$archive = Join-Path $buildRoot 'source.tar'
git -C $source archive --output=$archive $revision -- src CMakeLists.txt
if ($LASTEXITCODE) { throw "Could not extract fenio revision $revision." }
tar -xf $archive -C $buildRoot
if ($LASTEXITCODE) { throw 'Could not unpack the fenio source.' }

function Insert-Source([string] $File, [string] $Anchor, [string] $Replacement) {
    $path = Join-Path $buildRoot $File
    $content = [IO.File]::ReadAllText($path)
    if ($content.IndexOf($Anchor) -lt 0 -or $content.IndexOf($Anchor) -ne $content.LastIndexOf($Anchor)) {
        throw "Expected one source anchor in $File for fenio revision $revision."
    }
    [IO.File]::WriteAllText($path, $content.Replace($Anchor, $Replacement))
}
$header = "`t`tvoid Run();"
Insert-Source 'src/zserver.h' $header "$header`n`t`tint RunCombatLab(const char *settings);"
$mainAnchor = "`t// --map-info <mapfile>:"
$mainInsert = @'
    if (argc == 3 && string(argv[1]) == "--combat-lab")
    {
        ZServer server;
        return server.RunCombatLab(argv[2]);
    }

'@
Insert-Source 'src/main.cpp' $mainAnchor ($mainInsert + "`n" + $mainAnchor)
$pathAnchor = "`t//start thread"
$pathInsert = @'
    extern bool combat_lab_synchronous_paths;
    if (combat_lab_synchronous_paths)
    {
        Find_Path_Thread(response);
        return next_thread_id;
    }

'@
Insert-Source 'src/zpath_finding.cpp' $pathAnchor ($pathInsert + "`n" + $pathAnchor)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CombatLabZod.inc') -Destination (Join-Path $buildRoot 'src/CombatLabZod.inc')
[IO.File]::AppendAllText((Join-Path $buildRoot 'src/zserver.cpp'), "`n#include `"CombatLabZod.inc`"`n")
$env:MSYSTEM = 'MINGW64'
$env:CHERE_INVOKING = '1'
$env:OZ_COMBAT_BUILD = (Resolve-Path $buildRoot).Path
$env:OZ_COMBAT_JOBS = "$Jobs"
& $bash -lc 'set -e; cd "$(cygpath -u "$OZ_COMBAT_BUILD")"; cmake -S . -B build -G Ninja -DCMAKE_BUILD_TYPE=Release -DZOD_VERSION=5ccb43a-combat-lab; cmake --build build --target zod --parallel "$OZ_COMBAT_JOBS"'
if ($LASTEXITCODE) { throw "Zod compilation failed. Build files are in $buildRoot." }
$output = Join-Path $DataDirectory 'zod-bin'
$null = New-Item -ItemType Directory -Force -Path $output
Copy-Item -LiteralPath (Join-Path $buildRoot 'build/zod.exe') -Destination (Join-Path $output 'zod.exe')
[IO.File]::WriteAllText((Join-Path (Resolve-Path $output).Path 'runtime-path.txt'), (Join-Path $MsysRoot 'mingw64/bin'))
Write-Host "Zod worker built at $output. Start Combat Lab and enable Compare with Zod."
