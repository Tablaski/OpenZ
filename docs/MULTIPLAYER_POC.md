# OpenZ Duel: 1vs1 multiplayer prototype

## How it works

Unity 6000.3.27f2 / C#. The host runs the existing simulation. The guest controls
the blue team through a replicated world without running a second simulation.
A versioned TCP protocol sends the initial state, then property changes,
creations, removals and events at 15 Hz. Commands are validated by the host.
Map, settings and schema fingerprints must match.

The native Unity menus use the game's UI kit and portraits. The lobby provides
all four missions (L02, L07, L16 and L17), chat, ping, two ready confirmations,
a shared loading barrier and a three-second countdown.

Both players share the same rules and starting army: four fort turret slots,
units outside the fort, build speed and territory bonus. By default, both teams
receive the mission's red starting army, with no solo bonuses or bots.
An optional handicap is disabled by default. When enabled, it copies the common
settings for one player, whose army and build speed can then be changed
independently. An alert marks the exception. Any rule change resets both ready
confirmations.

In-game features include confirmed surrender (destroying the player's fort and
awarding the opponent victory), a dedicated menu, chat with a configurable key,
ping and optional push-to-talk voice with microphone selection. Voice uses a
separate UDP channel: mono 16 kHz mu-law, about 18 kB/s while speaking.
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

Main menu > 1VS1. The host enters the public or LAN address to share, creates a
room and copies the invitation. The guest pastes it into Join. The invitation
contains an address, port and random token. Both players need the same build
and must mark themselves ready.

Run tools/Register-DuelInvitations.ps1 once to register openz:// links for the
current Windows account. Opening a link fills in the join menu; the player still
chooses to connect. Some messaging clients require copying and pasting the link.

Direct Internet play requires the host's chosen TCP port (27888 by default) to
be reachable through the firewall/router. Voice uses the next UDP port (27889).
For LAN play, use the host's LAN address. No permanent server is required.

## Limitations

There is no NAT relay, public HTTPS invitation page, reconnection or host
migration. Invitations cannot traverse every router. Two remote PCs and actual
headset audio quality still require separate testing.

TCP recovers packet loss but can delay later updates. State replication over
UDP would require acknowledged baselines and appropriate recovery. Voice
already uses a bounded buffer and drops stale frames.
