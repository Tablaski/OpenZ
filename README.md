> **Experimental fork — early beta stage.** OpenZ-VK is a work in progress. Multiplayer, interface changes and custom combat behavior may contain bugs or change between versions. Builds are shared for experimentation and feedback; they are not a stable replacement for OpenZ.

# OpenZ-VK

An experimental fork of [OpenZ](https://github.com/playopenz/OpenZ), focused on playing against a friend and exploring different ways for units to fight.

## Main features

- **1 vs 1 multiplayer:** invite a friend, prepare a match together and play red against blue.
- **More comfortable play:** clearer army comparison, adjustable flags and territory borders, and easier access to chat and voice controls.
- **Custom combat dynamics:** experiment with tank and robot dodging, boosts, turning, acceleration and braking, with shared settings and exceptions for individual unit types.
- **A combat testing playground:** stage repeatable fights, compare outcomes and save your own behavior profiles.

## Play 1 vs 1

- Host a match and share an invitation link or code with a friend.
- Choose Desert, Volcano, Jungle or Car Park, with a tactical preview of the starting armies.
- Configure a common starting army and fort defenses for both sides. Optionally give one player extra starting units or a production handicap.
- Review the shared rules, confirm readiness and enter the match together.
- Use text chat and optional voice chat, including mute and speaking controls.
- Surrender, request a restart that the opponent can accept, review end-of-match statistics, or return together to the lobby for another game.

Both players need the same version. Rejoining an interrupted match is not currently supported, and some network connections may fail.

## Interface and readability

- Compare the two armies by unit count or offensive production value, displayed as a score.
- Adjust flag size and use smaller, dotted territory borders to reduce visual clutter.
- Configure the chat shortcut and access chat and microphone controls during a match.
- Keep your existing game preferences across updates.

## Experiment with combat dynamics

- Keep OpenZ behavior, try the Zod Classic reference, or build a custom profile between and beyond those starting points.
- Tune tanks and robots independently. Apply common values to a family, then override individual unit types.
- Control when units dodge, which threats they react to, their reaction timing, direction, distance and defensive boost.
- Use **brake, pivot, then accelerate** to make the hull turn before the tank drives into a new direction. Adjust pivot speed, remaining movement during the turn, alignment, acceleration and braking.
- Choose whether turning affects real movement or only the displayed model.
- Tanks choose the front or rear hull axis requiring the shortest turn. With reversing enabled, rearward moves back up instead of making an unnecessary half-turn, including brake-pivot-accelerate dodges.
- Open **CRIPPLING** to configure moving hull shake, retained speed, extra acceleration time, rear smoke and fading oil trails for each health colour. Green is above 50% HP, yellow is above 25% through 50%, and red is 25% or below, matching the Unit Editor hover card.
- Enable **Movement penalty on damaging hits** for two independently configurable damage thresholds, speed caps and durations. A heavy hit can briefly stop a tank; smaller hits cannot weaken an active heavy penalty. These thresholds use actual HP lost per hit.
- Save named profiles, export them to share, and select separate tank and robot profiles for later matches.

These settings are experiments, not a promise of identical behavior to the original Z or The Zod Engine.

## Test and compare units

- Place opposing units in the Unit Editor and tune their behavior live.
- Follow damage, kills, deaths and success percentages, with optional automatic counter resets after relevant tuning changes.
- Repeat fights using paired respawn points, including optional survival through otherwise lethal hits.
- Drag units into position and inspect their movement targets, combat positions and visual lag.
- Compare the normal movement model with custom physical constraints before saving a profile.

## Optional balance rules

- Limit the production advantage from territory control. Leaving the cap off preserves the original territorial advantage.
- Give the weaker army an optional fort-production bonus based on the two sides' offensive army values.
- Keep these optional rules separate from the original production rules and agree on them in the lobby.

## Download and play

Download the Windows build from [OpenZ-VK releases](https://github.com/Tablaski/OpenZ/releases). Follow that release's extraction instructions, keep the complete game folder together, and run **openz-vk.exe**. Do not use GitHub's automatic source-code ZIP as a playable download.

To try the tank movement mode: open **Unit Editor > Dynamics > Tanks**, choose **ALL TANKS** or an individual type, then click **BRAKE > PIVOT > ACCELERATE** under **FEEL** or **MOVEMENT**. Set **Retained speed during pivot** to 0% for a full stop or a small value for a near-stop, then tune turning, acceleration and braking. Existing per-type overrides take priority over family values. Use **SAVE AS** to keep the result.

For damage behaviour, open **CRIPPLING**, turn on **Custom damage movement and trails**, then select **GREEN**, **YELLOW** or **RED**. Zero shake/smoke/oil disables that effect; oil fade duration is in simulation seconds. Retained speed replaces the native damage speed factor in that band; extra acceleration time also works with physical movement limits off. Hit penalties have their own enable switch. Both new switches default to off, preserving existing profiles and preferences. **Allow boost when badly damaged** has moved from BOOST to CRIPPLING with its saved key unchanged.

New builds still need manual gameplay validation. Development and the latest fork documentation are on `dev`.

## Changelog

These are fork source versions. A playable build is identified separately by its release assets and `FORK_BUILD.json`; a source update alone is not a player release.

### 0.2.7

- Choose the shortest tank hull rotation, including backing into rearward dodge directions. Respect reverse speed and delay, and brake before switching travel direction.
- Add the OpenZ-VK **CRIPPLING** tab, three health bands, configurable movement shake, speed and acceleration loss, rear smoke and gradually fading oil trails.
- Add two temporary hit penalties using actual HP loss, with independent thresholds, retained speeds and durations; allow a temporary complete stop without losing orders.
- Move the existing damaged-boost option into CRIPPLING, preserve existing profile IDs and choices, and advance local preference history to schema 3.
- Use actual accepted movement for custom-mode track animation and render extrapolation. Add regression coverage for all four travel quadrants, colour boundaries, impact recovery and multiplayer replication.

### 0.2.6

- Add tunable brake-pivot-accelerate movement, including pivot speed, alignment tolerance, acceleration and braking.
- Preserve live physical hull heading and replicate movement phases. Publish a Windows x64 experimental player in two complementary ZIPs.

### 0.2.5

- Bound the testing movement arrows and clarify the markers for simulation position, movement targets and visual lag.

### 0.2.4

- Name new Windows players `openz-vk.exe`, while preserving the OpenZ preference storage identity and invitation registration.

### 0.2.3

- Present offensive production values as scores and reduce the size of dotted territory boundaries.

### 0.2.2

- Remove private relay access codes and add guarded universal TURN access with a shared usage cutoff. Preserve direct connections when relay access is unavailable.

### 0.2.1

- Add FEEL controls and Advanced sections to simplify combat tuning. Add simulation-position, target and visual-lag overlays.

### 0.2.0

- Introduce OpenZ-VK branding and fork version/build identity, grouped violet fork settings and local preference history.
- Add multiplayer surrender, restart and end-screen flow controls, alongside the existing combat profile and testing playground work.

## Original project and credits

OpenZ-VK builds on the work of the [OpenZ project](https://github.com/playopenz/OpenZ), its contributors, and the simulation rules ported from The Zod Engine. OpenZ is a fan remake of **Z**, created by **The Bitmap Brothers**. This fork preserves the upstream credits and licenses.

The [original OpenZ presentation](docs/UPSTREAM_README.md) is retained separately; its download links describe upstream OpenZ. For the original game, use [OpenZ's releases](https://github.com/playopenz/OpenZ/releases).

Free and open source. See [LICENSE](LICENSE) and the [contributor guide](CONTRIBUTING.md).
