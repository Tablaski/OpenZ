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
Those earlier checks covered the TCP prototype. Online transport validation is
recorded below.

## Online transport and integrated lobby

- 481 core and real-map checks pass, including the four-slot armies and additive
  handicaps from the parallel lobby update.
- The Windows player builds successfully with the pinned libdatachannel 0.24.6
  native dependency, static C++ runtime, and bundled upstream license notices.
- The Cloudflare invitation service is deployed at
  `https://openz-duel.openz-tablaski.workers.dev`. Live checks pass for HTTPS
  invitations, authenticated WebSockets, offers/answers and both ICE requests.
- Two distinct private access codes are accepted and receive real expiring TURN
  credentials. Public users are refused relay credentials even after TURN is
  enabled. Permanent TURN credentials are encrypted server-side, never in the
  distributed game. Private access is protected locally with Windows DPAPI.
- Native WebRTC checks pass for selected TURN candidate pairs on both peers,
  a fragmented state larger than 1 MiB, reverse commands, voice, build mismatch
  rejection and oversized-frame rejection.
- The integrated player passed a full two-player mission with **TURN forced**:
  both processes report `transport=TURN`, combat damage, guest orders, received
  voice, destroyed blue fort, red victory and zero exceptions. Evidence:
  `proof-online-L02-20261115-003339` under `tests/Multiplayer/.cli/`.
- The actual player registers its own per-user `openz://` handler without
  administrator rights. The private relay game also validates reading the
  owner's installed DPAPI-protected access, without a test credential override.
- Advanced TCP/IP passes all four missions on the integrated network build:

| Mission | Evidence under tests/Multiplayer/.cli/ |
| --- | --- |
| Desert | proof-L02-20261115-003548 |
| Volcano | proof-L07-20261115-003708 |
| Jungle | proof-L16-20261115-003825 |
| Car Park | proof-L17-20261115-003942 |

The native menu proof `menus-20261115-004050` passes for the main menu entries,
automatic/advanced connection screens, tactical map, four shared outside slots,
cannon picker, additive handicap, both beneficiaries, empty slots and unchanged
construction times. The subsequent portrait-only correction was rebuilt and
validated by `menus-20261115-005351`; the captured general portraits are populated.
It does not change the network transport or simulation.

The final player also passed the direct online path in
`proof-online-L02-20261115-005545`, reporting `transport=WebRTC direct` on both
peers, without a service URL or private-code override in the test environment.
The earlier direct check was `proof-online-L02-20261115-001513`.
These are same-PC two-process tests using
the actual public service and, when forced, the actual Internet TURN relay.
They do not prove every pair of remote networks or physical microphone/headset
quality. Voice proofs use synthetic samples without enabling a microphone.

The current native ICE backend supports TURN over UDP only. Networks blocking
all UDP can still fail. Private credentials expire after six hours; there is no
mid-match ICE restart, credential renewal or host migration. Cloudflare's free
allowance is not a spending cap.
