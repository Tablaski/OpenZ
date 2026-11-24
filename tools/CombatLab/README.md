# OpenZ combat lab

Double-click `START-COMBAT-LAB.bat`. The dashboard opens in your browser. It runs on this computer and requires the .NET 8 SDK and the local OpenZ source resources. Unity and the compiled game are unnecessary for these tests.

1. Start **Shell damage and radius** to compare projectile settings and actual damage. Pick a weapon and target to see the curve. The distance axis uses simulation pixels, measured from the target center.
2. Start **Tank shots and duels** for Light, Medium and Heavy in both directions, including matching unit types. Choose a target order in Results to examine idle, moving or attacking targets. **Road, water and damaged units** adds road combats, two damaged health bands and straight-line movement on ground, road and water.
3. Use **Pause**, close the browser or stop the launcher with Ctrl+C when needed. The launcher keeps running after the browser closes. Relaunch and choose **Resume** to continue a saved campaign. Each committed batch survives a restart. To add more seeds to a completed fight campaign, enter the new total repeats in **Repeats per fight**, select the saved campaign and click **Add repeats**.

Results are in `.local/combat-lab/results.sqlite3`. The dashboard exports raw CSV as well. Keep the database and any accompanying SQLite files together when copying results from a running process. Use `-CandidateProfile` with a tank behavior JSON exported from the Unit Editor to compare it against OpenZ's standalone baseline in the same campaign.

The blast test fires the weapon through the engine, then moves the first projectile's impact point relative to the stationary target and calls that engine's actual damage resolver. It samples every integer offset from 0 through 128. Dueling records each side's HP lost and outcome over at most 45 simulated seconds. The one-shot test measures HP lost for the isolated first projectile. A dodge start records a dodge waypoint seen during the shot, including attempts that still take damage. The movement test takes two simulated seconds from the start of a move order. The OpenZ baseline is the standalone simulation with stock local settings, without Unity preferences.

## Add the fenio reference once

Install MSYS2 and its 64-bit MinGW packages in the MINGW64 terminal, using the same dependency family as the fenio Windows build.

```sh
pacman -Syu
pacman -S --needed mingw-w64-x86_64-toolchain mingw-w64-x86_64-cmake mingw-w64-x86_64-ninja mingw-w64-x86_64-pkgconf mingw-w64-x86_64-sdl3 mingw-w64-x86_64-sdl3-image mingw-w64-x86_64-sdl3-mixer mingw-w64-x86_64-sdl3-ttf
```

If MSYS2 asks you to close and reopen the terminal during its update, finish the update in the new terminal. Then run the following from the OpenZ repository in PowerShell.

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\CombatLab\Setup-Zod.ps1
```

The setup extracts `5ccb43a` from `D:\fenio-zod-engine` into an ignored local build directory and adds the lab entry point to that copy. Use `-ZodSource` or `-MsysRoot` if your paths differ. The dashboard enables **Compare with Zod** when the worker and data files exist. Use `-ZodSettings` on the launcher to select another settings file. Campaign resume checks that the engine and settings fingerprints still match. The fixed-step fenio worker is an experimental arena; its parity with the normal real-time server needs separate validation.

## Commands

With the launcher running in another window, use PowerShell for unattended runs. A single request starts the full campaign. The application needs no further input. The default port is `8911`.

```powershell
$lab = 'http://127.0.0.1:8911/api'
Invoke-RestMethod "$lab/campaign" -Method Post -ContentType 'application/json' -Body '{"suite":"tanks","repeats":100,"compareZod":false,"candidate":false}'
Invoke-RestMethod "$lab/status"
Invoke-RestMethod "$lab/pause" -Method Post
Invoke-RestMethod "$lab/campaign/1/resume" -Method Post
Invoke-RestMethod "$lab/campaign/1/resume?repeats=200" -Method Post
Invoke-WebRequest "$lab/campaign/1/export.csv" -OutFile combat-results.csv
```
