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

New builds still need manual gameplay validation. Development and the latest fork documentation are on `dev`.

## Original project and credits

OpenZ-VK builds on the work of the [OpenZ project](https://github.com/playopenz/OpenZ), its contributors, and the simulation rules ported from The Zod Engine. OpenZ is a fan remake of **Z**, created by **The Bitmap Brothers**. This fork preserves the upstream credits and licenses.

The [original OpenZ presentation](docs/UPSTREAM_README.md) is retained separately; its download links describe upstream OpenZ. For the original game, use [OpenZ's releases](https://github.com/playopenz/OpenZ/releases).

Free and open source. See [LICENSE](LICENSE) and the [contributor guide](CONTRIBUTING.md).