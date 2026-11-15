# Player validation record

## Combined release — November 15, 2026

Final player: `builds/OpenZ-Behavior-Release/OpenZ-Duel.exe`. Keep its complete
runtime folder together. Source code commit:
`763a80c0278e28ec39d6d0149d4f5e965255c4d7`. `SOURCE_VERSION.txt` also identifies
pre-existing uncommitted force-bar/configuration changes included in this local
player; their exact patch is provided separately in the player folder.

Build succeeded with zero errors, development OFF, script debugging OFF,
profiler connection OFF and native-debugger wait OFF. The 720 .NET checks pass,
including native-profile equivalence, inheritance, save/reference round trips,
live order interruption, independent tank/robot rules, profile replication and
real physical acceleration/displacement/arrival.

### Completed on this same final player

- Unit Editor opens, world/input/camera initialize, BEHAVIOR is registered as a
  clickable HUD entry and opens through its button callback. All eight pages
  execute without errors. Every parameter has detailed help and reference data.
  Live family overrides, named saves, import/export, the game dropdown and clean
  return to the main menu pass. Evidence: `unit-editor-20261115-165116`.
- Main menu and 1vs1: four maps, fort/field pickers, additive handicap and
  beneficiary, explanatory construction labels, microphone controls, gain and
  dropdown pass. Both new map-readability settings are present in GAME.
  Evidence: `menus-20261115-165200`. Lobby/layout captures were visually checked.
- Native two-player tests pass on all four maps, with guest commands, damage,
  lobby voice, chat, surrender/fort destruction, red victory and zero exceptions.
  The accumulated force-count/military-value options and bonus display are
  checked too. On each host, in-match GAME options cycle actual existing flags
  through x4/x2/x1, enable owner-coloured/neutral-grey dotted borders, capture
  evidence and disable the borders live. Preferences are restored afterwards.

| Map | Evidence under `tests/Multiplayer/.cli/` |
| --- | --- |
| Desert | `proof-L02-20261115-165252` |
| Volcano | `proof-L07-20261115-165353` |
| Jungle | `proof-L16-20261115-165501` |
| Car Park | `proof-L17-20261115-165605` |

Each folder contains host/guest results, logs, native screenshots and
`map-readability-options.png` / `flags-x4-dotted-borders.png`. Desert's native
readability and in-match option captures were visually inspected.

### Pending manual validation

- Detailed IMGUI behavior-panel layout, actual mouse edits and hover-tooltip
  readability still need manual inspection: hidden-window ScreenCapture images
  are black and are not accepted as visual proof. The user stopped Computer Use
  with physical Escape before the final panel could be inspected interactively.
- The initial behavior entry lacked a HUD blocker: the first API-based proof
  missed its unclickable entry, and the user found it. The blocker is fixed and
  the final proof now rejects a visible entry not captured by the HUD before
  exercising its real callback. A physical mouse check is still desirable.
- Preset balance, animation coherence in crowded fights, extreme zoom and large
  fort-flag occlusion require gameplay judgement. The current four-map native
  checks do not certify every combination of the 72 behavior parameters.
- WAN/router/TURN connectivity and actual microphone/headset listening were not
  rerun for this release. The native proofs above use same-PC TCP plus voice
  transport, independently of earlier online validation.

No new build is needed for the user's manual checks of the current features;
use the combined player above. Future source changes requiring a new player
must be added here and checked together on the next requested release.
