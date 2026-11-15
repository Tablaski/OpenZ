# OpenZ Duel repository guide

This fork develops a 1vs1 multiplayer prototype on top of OpenZ 1.4.
It keeps the upstream **code-only Git layout**. The complete Unity project is
local, in source/, and we edit its tracked code directly.

## What is saved where

| Path | Purpose | In Git? |
| --- | --- | --- |
| source/Assets/OpenZ/Scripts/ | Game code, our multiplayer module and code metadata | Yes |
| source/Assets/OpenZ/Shaders/ | Shader code and its metadata | Yes |
| source/Packages/ | Dependencies and package lock file | Yes |
| source/ProjectSettings/ | Shared project settings and Unity version | Yes |
| tools/, tests/Multiplayer/, docs/ | Our build tools, test sources and documentation | Yes |
| Other source/Assets/ folders | Models, textures, sounds, scenes and other resources | No |
| source/Library/, Logs/, UserSettings/ | Regenerated caches and local settings | No |
| builds/ | Compiled players | No |
| .local/ | Local migration backup and audit files | No |

A clone contains the code, not the complete playable game or its multimedia.
This is the same distribution split used by upstream OpenZ. Resource files must
be supplied separately from the official source release when preparing a new
machine. See [CONTRIBUTING.md](../CONTRIBUTING.md).

## One working project

The complete source release was already present locally. Its project now lives
in source/, with our existing multiplayer integration included. The old second
working folder and the module copy/patch preparation script are no longer used.
There is no Git LFS dependency.

Upstream names and content are preserved. Our English translation applies only
to our multiplayer additions and documentation. The code snapshot now matches
the full OpenZ 1.4 project we actually used to build the game, rather than the
older partial code snapshot originally published in Git.

## Save work

Use dev for development and main for stable versions. A commit records a local
version; a push sends it to GitHub. Neither happens automatically while editing.
Each completed code change must be committed, pushed and checked against the
remote branch. All new contributions must be in English.

Only the paths listed as tracked above are covered by this Git backup. Editing
an ignored model, sound or scene does not save it on GitHub. Any future resource
change needs a separate backup/distribution decision before delivery.

## Build and share

Use tools/Build-Duel.ps1 to build the existing complete local project directly.
The Windows output is builds/OpenZ-Release/openz-vk.exe. Players need that whole folder in a
release ZIP; they do not need Unity or Git. A playable release should identify
its source commit and preserve the licenses and credits.

See [contributor instructions](../CONTRIBUTING.md),
[multiplayer behavior](MULTIPLAYER_POC.md) and
[validation results](DUEL_VALIDATION.md).
