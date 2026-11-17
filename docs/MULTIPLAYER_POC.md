# OpenZ Duel: 1vs1 multiplayer prototype

## How it works

Unity 6000.3.27f2 / C#. The host runs the existing simulation. The guest controls
the blue team through a replicated world without running a second simulation.
A versioned protocol sends the initial state, then property changes,
creations, removals and events at 15 Hz. Commands are validated by the host.
Map, settings and schema fingerprints must match.
Online play uses encrypted reliable ordered WebRTC data channels with ICE/STUN
and Cloudflare TURN fallback with preventive usage cutoff. Advanced TCP/IP preserves the original
transport as a separate alternative. Cloudflare Workers and SQLite Durable
Objects handle invitations and signaling, never simulation or voice packets.

The native Unity menus use the game's UI kit and portraits. PLAY SOLO and
PLAY 1 VS 1 are consecutive entries in the main menu. The lobby provides
Desert, Volcano, Jungle and Car Park, chat, ping, two ready confirmations,
a shared loading barrier and a three-second countdown.

The MAP section shows the solo mission thumbnail and a complete tactical preview
of the configured starting armies. Changing maps preserves the custom armies.
Both players share four slots outside the fort. A dedicated top-view popup
shows four real cannon mounts on each fort, editable independently for red and
blue. Existing shared defaults remain compatible until a team mount is edited.
Outside slots open a unit-selection popup; fort mounts accept cannons only.
The default fort contains a howitzer and a missile cannon. The outside slots
start with up to four entries from the mission's red army, without solo bonuses
or bots. A robot slot represents a whole squad.
An optional handicap is disabled by default. Selecting General Zod (red) or Evil
Zod (blue) selects its beneficiary. Four initially empty additional outside slots
add units to that player's shared army. Later edits to the common army still
apply to both players. Any rule change resets both ready confirmations.
The guest can use PREPARE SETTINGS to edit a local draft and SEND PROPOSAL to
submit it as one request. The host sees one persistent panel with REVIEW,
ACCEPT and DECLINE; no per-field notifications are sent. A new proposal replaces
the old one and can be withdrawn. The map gallery also supports direct map
proposals. Official changes expire proposals and require stale drafts to be
refreshed. Proposal submission, replacement, withdrawal and rejection preserve
readiness. Acceptance applies all changes and resets both confirmations.
Construction formulas and their existing defaults are unchanged in this UI
revision; new territory, comeback and construction-time rules remain deferred.

In-game features include confirmed surrender (destroying the player's fort and
awarding the opponent victory), a dedicated menu, chat with a configurable key,
ping and optional push-to-talk voice with microphone selection. Voice uses a
separate unreliable unordered WebRTC data channel online, or UDP in advanced
TCP/IP mode: mono 16 kHz mu-law, about 18 kB/s while speaking.
The microphone is never opened before the player explicitly enables it.

## Build and validate

The only Unity project is source/. Edit it directly; no installation or patch
step is required. See [CONTRIBUTING.md](../CONTRIBUTING.md).

~~~powershell
./tools/Build-Duel.ps1
dotnet run --project tests/Multiplayer/Multiplayer.Checks.csproj -c Release
./tools/Test-DuelPlayers.ps1 -Map L02
~~~

The player is builds/OpenZ-Duel/OpenZ-Duel.exe. Keep all neighboring files.
The build validates Unity serialization of commands, events and rules.
Every build includes scenes and resources so asset changes are incorporated.

The core suite checks lobby and handicap rules, readiness, team ownership,
real local TCP connections, invalid/truncated frames, differential replication,
references, projectiles, production, symmetric armies, surrender and voice
encoding against all four real missions. Core tests alone do not validate
Unity rendering or gameplay.

The two-player script starts two real players, captures the lobby, in-game menu
and battle, readies both players, loads the scenes, sends blue orders, triggers
combat, exchanges a synthetic UDP voice frame and makes the guest surrender.
Results and logs are written to tests/Multiplayer/.cli/proof-*.

~~~powershell
./tools/Build-And-Test-Duel.ps1
~~~

This runs the core checks, builds the player, tests all four missions and checks
the native menus. See [DUEL_VALIDATION.md](DUEL_VALIDATION.md) for actual results.

## Connect and invite

Main menu > PLAY 1 VS 1 > PLAY ONLINE. The host creates an invitation and sends
its HTTPS link to the guest. The guest opens it and chooses Join in OpenZ, or
pastes the link/code into JOIN FRIEND. Neither player enters an IP address or
configures a router or VPN. Both players need the same build and mark themselves
ready in the lobby. See [the service guide](../services/duel-signaling/README.md)
for deployment and the universal relay usage guard.

The Windows player registers openz:// links for the current account on a normal
launch. An online link joins automatically. No administrator rights or background
service are needed. Some messaging clients require copying and pasting the link.
The existing registration script remains available for manual setup.

ADVANCED TCP/IP requires the host's chosen TCP port (27888 by default) to
be reachable through the firewall/router. Voice uses the next UDP port (27889).
For LAN play, use the host's LAN address. No permanent server is required.

## Limitations

There is no reconnection or host migration. All players can receive temporary
TURN credentials while the account-wide usage guard permits it; otherwise
connections use STUN only. No private access code is required.
This Windows libjuice build supports TURN over UDP, not TCP/TLS, so networks
blocking all UDP can still fail. Temporary credentials expire after six hours.
Two remote PCs and actual headset quality still require separate testing.

Reliable game channels recover packet loss but can delay later updates. Voice
uses a separate bounded buffer and drops stale frames. TURN usage beyond the
Cloudflare free allowance can be billed; the allowance is not a hard spending cap.
