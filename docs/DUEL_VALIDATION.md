# Duel validation — November 14, 2026

## Repository cleanup and English UI

- The complete local Unity project now lives in source/. Its tracked code and
  configuration are the same files used to compile the player.
- All 4,750 code, code metadata and configuration files were checked against
  Git's tracked paths. Multimedia and scene directories remain excluded, as
  requested for the upstream code-only layout. Git LFS is not used.
- 463 .NET core and real-map simulation checks passed after the move.
- A full Windows player build succeeded with zero errors.
- Two local players passed on all four missions: guest orders, combat damage,
  received UDP voice, guest surrender, destroyed blue fort, red winner and zero
  exceptions.
- Native English menu checks passed for entering multiplayer, creating a room,
  shared rules, portraits, the optional blue handicap, independent army edits
  and disabling the handicap. The final BACK label and generic capture player
  names were rebuilt and the native menu checks passed again.

## Evidence

Generated evidence is local in tests/Multiplayer/.cli/; selected English
screenshots are versioned in docs/duel-preview/.

| Mission | Evidence folder under tests/Multiplayer/.cli/ |
| --- | --- |
| Desert | proof-L02-20261114-230902 |
| Volcano | proof-L07-20261114-225555 |
| Jungle | proof-L16-20261114-225707 |
| Car Park | proof-L17-20261114-225816 |

Each mission folder contains host-result.txt, guest-result.txt, logs and native
Unity screenshots. The final native menu run is menus-20261114-230827/result.txt.
Desert was also rerun successfully on the final player after the last UI-only
label and capture-name correction.

The first full player build after moving the project reported missing scene
scripts and the Desert proof timed out while loading. The build helper now
validates key scene script bindings and reimports script metadata when needed.
After an editor reload and a fresh build, all four two-player proofs passed.
Upstream source and scene files were not edited to address this failure.

The launcher waits for the complete invitation before starting the guest.
The Jungle proof uses a clear firing lane. Captures use explicit rendering
instead of relying on hidden-window screenshots.

## Reproduce

Complete the one-time resource setup in [CONTRIBUTING.md](../CONTRIBUTING.md),
then run:

~~~powershell
./tools/Build-And-Test-Duel.ps1
~~~

This runs the core suite, builds the current player, tests all four missions and
checks the native menus. It stops on failure. Core checks alone do not prove
Unity rendering or gameplay.

Tests use two processes on the same PC. They do not prove connectivity through
all routers, variable remote latency or actual microphone/headset quality.
No managed relay or public HTTPS invitation page is configured.
