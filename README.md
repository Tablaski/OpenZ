<p align="center">
  <img src="media/openz-vk-logo.png" alt="OpenZ-VK" width="640">
</p>

[Join the OpenZ Discord (ZZone)](https://discord.gg/WjFqg35PsE) — talk about the original game, OpenZ and this fork.

# OpenZ-VK

**OpenZ-VK 1.5.91** is an experimental fork of [OpenZ](https://github.com/playopenz/OpenZ) 1.5, the fan remake of **Z** by **The Bitmap Brothers**.

## Why this fork?

I want to bring back the combat feel and advanced tactics that Z's 1996 veterans remember, while tackling its rough edges—especially the balance that can turn an early lead into an unstoppable snowball. OpenZ-VK is a place for multiplayer, fairer optional game modes and a laboratory for trying ideas. You can still play with normal OpenZ rules and leave the fork's optional changes off.

This is **work in progress**, not a claim of perfect Z fidelity. If you remember the tricks, duels and hard-earned tactics of the original, come help: play matches, challenge the balance and tell us what feels right—or wrong.

## What can you play with?

- **Online multiplayer.** Share a magic link for 1 vs 1 or a larger match, or connect through TCP/IP using the same lobby. Pick a battlefield from full minimaps, drag participants into teams and choose their starting forts. Guests can follow the host's setup while chatting. In team matches, allies share vision, radar and territorial production bonuses. Ghost spectators can watch with a free camera or follow a human player.
- **1 vs 1 and team rules.** Play head to head, with teams or free for all. Choose a Team Defeat rule for team matches. After an online match, see the results and return to the same lobby together.
- **Your duel, your rules.** Choose allowed units, starting armies, handicaps and four fort cannon slots per team. On the official **Bridge Game**, the host can allow or exclude the Mammoth and Bizon individually. Your opponent can propose a setup for the host to accept. Optional production rules aim to keep matches competitive for longer.
- **Solo match setup.** Choose allowed units and tank and robot dynamics from the battlefield selector. Play against the classic 1996 AI or choose one of four AI 2026 difficulties. Skilled and Zod use local reinforcements and coordinated attacks.
- **AI Maps.** Rediscover battlefields across five planets with V5 terrain, from volcanic lava fields to Arctic steel crossings. Browse 3D flybys in solo or explore the multiplayer gallery with large minimaps and terrain filters. The collection includes low-cost AI reproductions of Z Original, Z Expansion and multiplayer maps, plus the original Chilly Willy V2 Arctic layout.
- **A combat laboratory for old Z hands.** Use the Unit Editor to stage repeatable fights, compare units and help us work toward the combat dynamics and advanced tactics of 1996. Feedback and contributions from experienced players are very welcome.
- **Local Skirmish.** Choose a map by its minimap, mix 1996 and 2026 bots, and drag participants between teams. Fork-added options outside the lobby are marked in violet.

**Early beta:** expect bugs and changing rules. Z 1996 is a unit-availability preset, not a complete historical rules mode. Existing preferences and personal combat profiles are retained across updates. The separate experimental map editor is not included.

<table>
  <tr><td colspan="2"><img src="media/review-0.2.22/shared-rules.png" alt="Shared lobby rules and centered fort placement"></td></tr>
  <tr><td><img src="media/review-0.2.22/solo-allowed-z1996.png" alt="Solo allowed-unit selection"></td><td><img src="media/review-0.2.22/behavior-tanks-0.png" alt="Unit Editor dynamics"></td></tr>
</table>

## Download and play

Download **OpenZ-VK-Installer.exe** from [the latest OpenZ-VK release](https://github.com/Tablaski/OpenZ/releases/latest).

- **Compiled-game ZIPs:** ready to play, with no Unity or Git required. For manual installation, extract all Windows ZIP parts from the same release into one folder and run **openz-vk.exe**.
- **Source code:** the editable code on stable **main** or development **dev**; it must be compiled before playing. Development versions are distributed as source code only.
- **Unity:** the editor required to compile the source code; the installer helps you install the correct version.
- **Resource ZIPs:** the OpenZ 1.5 base assets with required fork adaptations, needed for source compilation. The installer reuses matching files across updates.
- **Installer:** installs and updates the compiled game, shows its changelog, or retrieves a chosen **main/dev** commit and builds it with Unity. It reuses unchanged data and uses Git when available, with optional Git installation and a Git-free fallback.

GitHub's automatic source-code ZIP is not a ready-to-play compiled game. A startup check detects incompatible resource packages. See the short [installer guide](docs/INSTALLER.md).

**Unit Editor:** click **UNIT EDITOR** in the installer, or open the build's **Unit Editor** folder and double-click **Open Unit Editor.bat**. It is also available in the game's main menu.

Use the same game version as the other participants. The lobby shows version differences before launch. An interrupted 1 vs 1 match pauses while the connection is restored.

See the [changelog](docs/CHANGELOG.md), [combat profiles](docs/BEHAVIOR_PROFILES.md), [Unit Editor guide](docs/UNIT_EDITOR.md). Development is on `dev`.

## Credits and licenses

Built on [OpenZ](https://github.com/playopenz/OpenZ) and the simulation rules ported from The Zod Engine. Upstream names, credits and licenses are preserved. See the [original OpenZ presentation](docs/UPSTREAM_README.md), [LICENSE](LICENSE) and [asset credits](docs/LICENSE-ASSETS.md). For upstream OpenZ, use [its releases](https://github.com/playopenz/OpenZ/releases).
