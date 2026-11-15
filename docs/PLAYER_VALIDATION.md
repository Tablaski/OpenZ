# Player validation record

## Pending — shared right-hand Stats / Balance / Dynamics / Testing tabs

These changes are newer than the last player. No new game build or game tests
were requested or run. They await a requested build and the user's validation.

- Remove the separate Combat dynamics / Unit testing buttons and left panel.
- Integrate Dynamics and Testing as content inside the existing right-hand
  Stats/Balance panel, with one shared header and hide/restore rail.
- Content-aware panel widths (500 for Stats/Balance, 520-620 for Testing,
  640-760 for Dynamics), field/tooltip/reference comfort and window resizing.
- Switching tabs commits active fields and preserves profiles, testing rules,
  HP and counters; hide, restore, I/B keys and normal stats/table behavior.
- Native proof source now expects the widened right-hand Dynamics content;
  no native proof was executed, as game testing belongs to the user.

Unity 6000.3.27f2 script-only compilation passed with exit code 0
(`editor-four-tabs-final-compile.log`). Runtime layout, keyboard interactions and
visual comfort remain for the user's validation after a requested build.

## Pending — success ratio, resets, JSON and left-hand combat dynamics

These changes are now included in `builds/OpenZ-Release`, built by the user's
batch-file launch on November 15, 2026, completed at 18:51 Paris time with zero
build errors. Game source commit: `25d216cb220653adbd0b4da34915fa813332602e`.
An overlapping launch failed while the first Unity build continued. The launcher
now rejects that case before touching the active log. The source record in the
player folder was corrected and a patch records existing local tracked changes.
No game was launched or tested; all checks below remain for the user.

- Success percentage above kill/death counters, including zero score, virtual
  deaths, destroyed units and readability alongside damage numbers.
- RESET COUNTERS next to Show counts, plus optional automatic all-unit reset
  only on effective changes affecting a fighter; preserve HP and orders.
- Readable JSON export/import with both families, references, master and overrides;
  legacy imports remain supported. Manual import errors and folder flows.
- COMBAT DYNAMICS beside the top-left BLUE/RED/background controls, with the
  panel below on the left, free combat space on the right, resize and scrolling.

Source simulation checks: 762 passed, including ratio overflow, both reset modes,
no-op/name/unused-type changes and preservation of partial damage and orders.
Unity 6000.3.27f2 script-only compilation completed with exit code 0
(`combat-dynamics-json-compile.log`). JSON file flows and the new placement
remain for the user's validation in a future player; no game was launched.

## Pending — right-hand behavior and unit testing laboratory

The following changes are included in `builds/OpenZ-Release/OpenZ-Duel.exe`,
built successfully through `BUILD-OPENZ.bat` on November 15, 2026 (zero build
errors, release settings). `SOURCE_VERSION.txt` identifies the source revision
and existing local changes. No post-build game tests were run: the user explicitly
reserved gameplay/UI/native validation for manual testing. All checks below
remain pending until the user reports results or explicitly requests agent tests.

- Native right-hand BEHAVIOR/UNIT TESTING tabs, same geometry/style as STATS;
  both entry buttons, restoration of STATS, resizing, scrolling, focus, free
  numeric keyboard entry and all detailed hover tooltips/reference labels.
- Red/blue reference HP sliders and fields on existing/new units; fixed-HP
  immortality; circular damage returning to the chosen percentage; preserved
  targets/orders, protected crews and independent settings per team.
- Floating actual/theoretical/crew damage and visible trophy/skull counters,
  ordinary and virtual deaths, simultaneous hits and crowded robot squads.
- Boosts above x5, repeated projectile replanning without indefinitely deferring
  attack resumption, explicit player orders and the optional full-step reference.
- Zod Classic stop-and-fire versus OpenZ offensive manoeuvres, engaged-robot
  restrictions and practical visual/mechanical comparison with the local Zod engine.

Completed source validation: 751 core/real-map simulation checks, including new
HP, virtual-score, crew, free-value and sustained-volley regression checks.
Unity 6000.3.27f2 script-only compilation also completed with exit code 0
(`laboratory-final-compile.log` and `laboratory-proof-compile.log`).
The native `DuelUnitEditorProof` now captures all eight canvas-based pages and
the testing sidebar, and checks clickable entries, layout, HP and scores. It
has been prepared for the next player; it has not passed on these new sources yet.
Run it and all outstanding native integration checks against that same release.

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

## Unit testing respawns and field gestures (awaiting a new release player)

- Added playground-only respawning at the end of Unit Testing: enable it and
  set RED and BLUE points by clicking the field. Labels mark both points.
- A lethal hull or crew hit, including virtual immortal defeats with cycling
  damage enabled or disabled, resets both opponents to their points, restores
  reference HP and crew, resets weapon readiness and stamina, removes their old
  projectiles and gives both a new attack order. Kill/death counters persist.
  Both points must be configured and passable for the opponents.
- Dragging a field unit lifts and moves it (including its squad), clears its
  orders and protects it from combat while held. Invalid terrain keeps the last
  valid position. Dropping resumes ordinary simulation. Escape releases it.
- RIGHT-DRAG SCROLL now follows Options > Game in the unit editor. A pan release
  must neither issue an order nor select a unit; the disabled option retains
  right-button orders. UI panels and point placement block pan initiation.
- Source checks: Unity runtime scripts compile using refreshed source paths;
  simulation checks cover repeated mortal/virtual respawns, crew defeat,
  projectile cleanup, score preservation, held-unit protection and disabling.
- Pending user validation on the next requested release: point buttons and
  markers, repeated fights at the chosen separation, drag/drop lift appearance,
  squad movement, cancellation, and right-drag enabled/disabled with open panels
  and a rotated camera. No player was built or launched for these changes.
