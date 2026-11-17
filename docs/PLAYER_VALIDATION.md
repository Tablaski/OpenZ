# Player validation record

## 1v1 map gallery layering fix (0.2.13, source only)

- Raise the shared map-gallery canvas from sorting order 950 to 1100, above the
  lobby canvas and full-screen raycast blocker at 1000. Build identity remains
  above both at 1200. No map data, preferences or authority rules change.
- Completed: runtime C# compilation and source inspection of the actual lobby,
  gallery and build-identity canvas priorities.
- Pending manual checks on a newly requested release player: open the 1v1 map
  popup, click campaign tabs/world filters/sort/cards, scroll, apply as host,
  inspect as guest, close and reopen; check the solo gallery too.
- No game player was built or launched. The fix requires a fresh release player.


## Complete p02 Shitty Maps catalog and blue borders (0.2.12, source only)

- Added the tracked shittymaps/ Unity local package: 60 selected native-source
  variants, 31 solo choices, 44 converted duel choices, plus the four normal OpenZ
  maps. Campaign levels 02, 07, 16 and 17 are excluded from converted choices.
- Solo separates Z Original (01–20) and Z Expansion (21–35), using original files.
  The user approved FIXED-only exceptions for missing originals 21 and 27; both
  are visibly labeled. Original and fixed Mayhem have separate map identities.
- Duel prefers fixed/FDL variants; maps without one show a warning pictogram.
  Non-numbered p02 maps have a Multiplayer tab. Names 30/31/32 retain approved
  numeric placeholders; custom names are derived from source filenames.
- Added a shared minimap gallery with world filters and numeric/name/world sort;
  guests inspect, only hosts apply. Cards and the lobby selector display poop
  pictograms independently of font emoji coverage.
- Added an Arctic snow/ice/blue-water presentation using OpenZ geometry and small
  generated shared textures, with matching minimaps. No original Zod artwork.
- Regenerate terrain geometry from tracked grids before ZGame.Awake. Map scenes
  reference shared prefabs; rebuilt/destroyed bridges keep their source world's
  model family. Inherited atmospheric/detail helpers use the selected map ID.
- Raised destructible rocks to 4.1 m with unchanged horizontal footprints.
- Lightened displayed blue territory LEDs to RGB (0.30, 0.68, 1.00), including team
  swaps, while preserving red/neutral colors, size choices and saved preferences.
- Completed: 1,959 core and real-map simulation checks, including every variant's
  identity/counts, solo/duel selection policy, authoritative armies and orders,
  and initial replica creation; existing loopback transport checks pass.
- Completed: batch Unity compilation, all 60 scenes authored, all source object
  views spawned, obstacle dimensions checked, and 1,571 starting-unit records
  checked for actual simulation types, owners and source coordinates.
- The package and its metadata/scenes/minimaps/materials are tracked directly in
  Git and mounted through source/Packages/manifest.json. No separate resource
  ZIP is needed for these maps; the shared base OpenZ assets remain required.
- Pending manual player validation: both solo tabs, every world/sort/filter,
  scrolling/selection/closing and poop/warning pictograms; 21/27 exception labels;
  loading/restart/end-screen names; terrain generation, Arctic palette, bridges,
  rocks and opened passages; full AI campaigns, performance on the largest maps;
  two-PC host/guest selection, previews, readiness, starts, combat and restarts.
  Check blue LED contrast on City in daylight/night and after a team-color swap.
- This is a broad addition. A fresh release build is recommended for manual
  validation when requested. No game player was built or launched in this task.


## Mayhem terrain, force HUD and 1v1 selection (0.2.11, source only)

- Raised all 307 original Mayhem rock views to 4.1 m, comparable to its City
  cliffs, preserving horizontal footprints, source objects and blocked tiles.
- Assigned generated repeating concrete and asphalt textures to ground and roads.
- Fixed the widened blue percentage label's right edge so it stays out of the badge.
- Added L19 to host-owned duel selection, map validation, loading and peer data
  compatibility, with a font-independent poop pictogram on its lobby button.
- Completed: Unity runtime source compilation (existing warnings only), full
  Unity editor compilation, scene repair/save, original map/object/start checks,
  and 1,563 core/real-map checks including L19 simulation and loopback transport.
- Ignored scene, materials, textures, map data and meshes are backed up separately
  by tools/Backup-MayhemResources.ps1 under .local/converted-map-backups/0.2.11/.
  Restore the archive at the repository root alongside the existing local assets.
- Pending manual validation on a newly requested release player: rock appearance,
  destruction and passage through opened gaps; ground/road textures and contrast;
  blue count/percentage with both force modes and large values; all five lobby
  buttons, poop icon and client read-only selection; L19 preview, loading, both
  armies, orders, combat, restart and return to lobby between two PCs.
- No game player was built or launched. A fresh release build would be useful
  for the terrain, HUD and new multiplayer map checks when requested.


## Multiplayer replication optimization (0.2.9, source only)

- Reuse capture streams, dictionaries and immutable unchanged records; compile
  typed scalar writers once on Mono, retaining the reflection fallback on AOT.
- Preserve the existing field-level delta schema and 15 Hz target. Skip capture
  while outgoing buffers are congested, without dropping already published
  baseline-dependent deltas or pending events.
- Reuse client record readers and lists; visit changed fields directly; avoid
  defensive byte-array copies inside the private validated replication pipeline.
  Public APIs retain their defensive-copy contract.
- Replace encode-time decoding with writer-side validation and exact capacity.
- Bound receive processing to three milliseconds between complete packets.
  An individual large packet can still exceed this budget; no delta is discarded.
- Fix zero/stale multiplayer simulation timers. Add passive ten-second
  [OpenZ Multiplayer Perf] lines with build identity, host/guest, direct/TURN/TCP,
  FPS, longest frame, send/receive costs, state rates, throughput, queue sizes,
  ping, collection count and live object count. Logs use the existing Player.log
  rotation, contain no credentials/SDP/chat/IP, and do not change settings or caps.
- Validation details and reproducible benchmark procedure are in
  MULTIPLAYER_PERFORMANCE.md. No game player was built or launched.
- Pending manual testing on a newly requested release player: both remote PCs,
  FPS and hitch behavior, orders/combat/production, surrender, restart, return to
  lobby, voice, slow links and direct/TURN paths. This is a broad change to the
  replication hot path: a fresh release build is recommended when requested.


## Converted maps (0.2.8)

- Added a separate converted-map browser under PLAY: Load Shitty maps, with a
  minimap card for Level 19 - Mayhem (City).
- Baked the original Zod Engine p02_bb_orig19_fixed.map with City resources and
  ordinary game object prefabs. The 128 x 116 map retains all 453 source objects.
- Completed: Unity compilation and actual game simulation checks of all 16
  red/blue starting unit objects, including owners, types and coordinates.
- Completed: standalone editor parser checks against every original Mayhem
  object, document round-trip, and measured bridge deck/ramp support tests.
- Completed on release player 91af9620: normal level cards remain visible, the
  converted-map button is separate from High Scores, the Mayhem/City minimap card
  opens, and DEPLOY reaches the ordinary loading screen and the actual L19 scene.
- Completed on that same release player: visible City terrain and starting army,
  normal orders, flag captures and combat. A red Mech and Light Tank crossed the
  City bridge at (20,76) to the opposite bank. The unattended test reached defeat.
- The final source additionally corrects the end-screen level name and portrait
  background to Mayhem/City. These cosmetic changes require the final player;
  the gameplay observations above were made before this two-line correction.
- Its reconstruction was interrupted before player output was updated because
  unrelated multiplayer changes and a 0.2.9 version edit appeared concurrently.
  The existing tested output remains stamped 0.2.8 / 91af9620. It does not yet
  include the end-screen identity correction in d4560dac. Do not present that
  correction as validated or included in the existing player.
- Pending: other bridges, dense opposing traffic, exhaustive unit/decor placement,
  and extended converted-map play. Native integration beyond this solo map was
  not tested. A successful build alone does not pass runtime checks.
- Ignored Mayhem scene, terrain meshes/materials and map data are distributed
  separately in the Mayhem resource archive; they are not stored in Git.

## Lobby rules replacement (0.2.7, awaiting player validation)

- This replaces the unshipped first 0.2.7 build, keeping 0.2.7 once at the user's
  explicit request. Its earlier interrupted upload did not replace the 0.2.6
  downloads. Future releases increment the fork version.
- Tank/robot profiles move into DYNAMICS beside the lobby logo. The host chooses
  profiles, guests inspect the values by unit type, and the simulation freezes
  profiles and availability for the match. Main/pause-menu selectors are removed.
- Allowed units includes all 22 editor unit types with thumbnails. OpenZ allows
  all; Z 1996 follows the local Zod Engine's six robot, seven vehicle and four
  cannon IDs; toggling a unit selects Custom. The historical medium ID remains
  OpenZ's MEDIUM MECH; the additional mtank is excluded from Z 1996.
- Starting and handicap slots, neutral map units, production lists/defaults,
  stored cannons and authoritative build/queue commands respect the roster.
  Excluded crew types cannot be ejected; fort defenders and EvilZod spawns also
  respect it. Allowed units retain their original technology requirements.
- Logos are dark orange in the main menu, editor, HUD, multiplayer and build
  identity. Lobby controls use orange/neutral; other fork game options stay violet.
- Completed: 1,220 core/real-map checks, including the prior movement/crippling
  checks and new preset classification, readiness invalidation, host authority,
  immutable profiles, restricted production and guest-state checks on all maps.
- Completed: Unity runtime source compilation (existing warnings only). The
  build's Unity wire check additionally covers profile and roster serialization.
- Completed: native WebRTC library checks for encrypted handshake, fragmented
  1 MiB snapshot, reverse commands, independent voice channel, incompatible-build
  rejection and oversized-snapshot rejection. This did not launch a game player.
- Pending manual checks on the replacement player: host/client lobby dialogs,
  thumbnails, scrolling, custom preset transitions and slot clearing; loading,
  restarts and in-game profile lock; production and neutral units under all
  presets; logo colours and fork highlights. All movement/crippling checks below
  remain pending too.
- The replacement Windows x64 release build succeeded with zero errors, with
  development, script debugging and profiler connections OFF. The Unity lobby
  wire check passed, including the new roster and both dynamics profiles.
  Source: `093c35cdba0e6d7ad3f8d9c0fde1537ef630b988`; build stamp:
  `2026-11-16 07:58:24 UTC`. The pre-existing local ProjectSettings shader
  preload accounts for the modified-tree flag and is included as an exact patch.
- Player: `builds/OpenZ-VK-0.2.7-Windows-x64/openz-vk.exe`. The two ZIPs under
  `builds/release-0.2.7/` contain all 187 runtime/distribution files, without
  missing or duplicate entries. Every entry's SHA-256 matches its player file.
  Part 1: 1,741,064,194 bytes; part 2: 982,654,543 bytes. Extract both into the
  same destination. No personal preferences, credentials, history or symbols
  are included. No player gameplay or rendering was launched for validation.
- Published and independently re-read through the GitHub API:
  [OpenZ-VK v0.2.7](https://github.com/Tablaski/OpenZ/releases/tag/v0.2.7).
  The existing release was replaced in place, including its notes and version;
  both prior ZIPs and the prior checksum file were removed only after all new
  assets were received and their sizes/SHA-256 digests verified. The public
  README/changelog and completed source are on both main and dev. The v0.2.7
  tag identifies the stamped game source commit above; later commits only
  record build/publication results. Gameplay and visual checks remain pending.

## Shortest hull turns and CRIPPLING (0.2.7, awaiting player validation)

- The user's light-versus-light check of 0.2.6 confirms the quick pre-dodge
  pivot is satisfactory and reveals the unnecessary rearward half-turn fixed
  in this version. It does not validate the other outstanding 0.2.6 checks.
- Tanks select the nearest front/rear axis in physical movement (including
  brake-pivot-accelerate), direct view and the legacy follower. Physical reverse
  speed/delay and the explicit reversing switch remain effective.
- CRIPPLING provides green/yellow/red hover-bar bands, moving hull shake,
  retained speed, extra acceleration time, rear smoke and fading rear oil trails.
  Existing damaged-boost choice moved here with its saved ID unchanged.
- Separate light/heavy actual-HP hit thresholds, speed caps and durations can
  briefly stop real travel. Weaker follow-ups cannot weaken an active penalty.
  Custom per-band movement and hit penalties are independent, OFF by default.
- Completed: 1,164 core/real-map simulation checks, including all four quadrants,
  reverse speed, colour boundaries, zero-speed recovery, impact tiers and expiry,
  disabled/native compatibility, profile round trips and guest replication.
- Completed: Unity runtime source compilation (existing warnings only).
- Pending on this release player: light/light and other tank duels; diagonal
  backward pivots, replans, braking forward-to-reverse, track direction; tab
  layout/scrolling and tooltips; each health band, shake intensity, acceleration,
  rear smoke and oil placement/fade (including pause/game speed and terrain);
  light/heavy impact tuning, saved profile selection and two-PC replication.
- The superseded first Windows x64 release build succeeded with zero errors.
  Development mode, script debugging and profiler connections were disabled.
  Source commit: `1ffa9b42535831bc3ce478ff5d86ac0b37c606b1`; build stamp:
  `2026-11-16 07:25:50 UTC`. The modified-tree flag records the pre-existing
  ProjectSettings shader preload; its exact SOURCE_CHANGES.patch is included.
- Superseded local player: `builds/OpenZ-VK-0.2.7-before-lobby-20261116-075634/openz-vk.exe`.
  Its ZIPs were kept under `builds/release-0.2.7-before-lobby-20261116-075634/`;
  they jointly contain all 187 runtime/distribution files.
  Every ZIP entry was read back and SHA-256 checked against its player file;
  coverage and absence of duplicate entries were checked. Part 1 is
  1,741,074,810 bytes; part 2 is 982,649,533 bytes. Both must be extracted into
  the same destination. Symbols, personal settings and history are excluded.
- No game player was launched and no post-build gameplay, rendering, audio or
  live two-PC validation was performed. The manual checklist above stays pending.

## Brake-pivot-accelerate release (0.2.6, awaiting player validation)

- Movement-only setup on FEEL/MOVEMENT for the selected family or type: brake
  along the old hull axis, pivot at zero or limited speed, accelerate once aligned.
  Separate turn rate, angular acceleration, alignment, acceleration and braking.
- Existing profiles remain unchanged unless the new mode is enabled. Old setting
  IDs are preserved. Local preference history schema 2 includes both drafts,
  selected profiles and named profile library; prior history and choices remain.
- Actual accepted physical displacement drives render extrapolation and movement
  indicators. DEBUG ON reports the movement phase. Live edits retain hull heading.
- Completed: 1,052 core/real-map simulation checks, including full-stop and near-stop
  pivots, braking before turning, acceleration after alignment, family/type
  inheritance, legacy profile decoding and host/guest physical-state replication.
- Completed: Unity runtime source compilation and Git diff whitespace checks;
  only existing unrelated compiler warnings remain.
- Pending manual checks: hull rotation and track appearance, pivots from rest and
  while moving, dodge/replan behavior on all tank types, high turn rates, game-speed
  controls, camera/overlay rendering, profile save/load and existing preferences.
- The requested Windows x64 release build succeeded with zero errors. Development
  mode, script debugging and profiler connections were disabled. Game source:
  `e711edfd0a3d705de9884fd8cbdfc7b5ada8005d`; build stamp:
  `2026-11-15 23:08:25 UTC`. Its modified-tree flag records the pre-existing local
  shader preload in ProjectSettings; SOURCE_CHANGES.patch accompanies the player.
- Local player: `builds/OpenZ-VK-0.2.6-Windows-x64/openz-vk.exe`. Two ZIPs under
  `builds/release-0.2.6/` cover all 185 runtime/distribution files. Every ZIP entry
  was read back and its SHA-256 compared with the corresponding player file.
  Sizes: part 1 = 983,600,453 bytes; part 2 = 1,755,734,473 bytes. Both must be
  extracted into the same destination. Personal preferences/history are excluded;
  build-only symbol directories are excluded. READ-ME-FIRST.txt includes setup.
- Originally published as v0.2.6 (downloads subsequently replaced in place by
  [OpenZ-VK v0.2.7](https://github.com/Tablaski/OpenZ/releases/tag/v0.2.7)),
  marked experimental early beta. Both uploaded ZIP sizes and SHA-256 digests were
  verified against local archives. The release tag identifies the game source
  commit above; the accompanying checksum file is also available on the release.
- The game was not launched. Build and packaging success do not mark gameplay,
  rendering, audio, microphone or native integration checks as passed.

## Testing movement overlays (0.2.5, awaiting player validation)

- Cyan marks the authoritative combat position. Violet marks the displayed model
  and its horizontal gap only above 5 cm; elevation is excluded. A lone cyan cross
  means the model follows the simulation within that tolerance.
- Yellow uses the current movement target instead of queued order coordinates
  (which can be stale or unused for attack orders), and is hidden at rest. Green
  shows commanded simulation velocity before physical steering. Both use arrows
  capped at 6 m and 100 canvas UI pixels; neither predicts a complete trajectory.
- Pending manual checks: idle attack, ordinary movement, dodge, follower/direct
  model modes, physical steering, camera zoom and all overlay toggle combinations.
- No player build or game launch was requested or performed.
- Source validation: all Unity runtime scripts compile with the Unity compiler;
  git diff whitespace checks pass. Existing unrelated compiler warnings remain.

## Pending — OpenZ-VK 0.2.0 menus, identity and duel flow

No new player was requested, built or launched for this change set. These checks
remain pending for the user's manual testing on a future requested release build.

- Main menu, Unit Editor, solo and duel HUD, loading/faction/pause/end screens:
  OpenZ-VK identity and bottom-right fork version/commit/build timestamp; narrow
  windows, UI scale and no overlap with READY/other controls. Older existing
  players remain unchanged; compare the visible identity before testing.
- Violet fork option groups after upstream rows in main/pause options; live
  flag x2 continuity, border OFF/x1/x2/x4, bright round dots by day/night, water,
  cliffs, captures and team-colour swaps. No new raster/media assets are required.
- Command-bar chat and rebinding (including Escape cancellation); no chat entry
  in Escape, separate Surrender directly under Restart level.
- Microphone Always on / Tap to speak / Mute, physical input meter, device/gain,
  focus changes, typing, lobby/in-game audio, no muted transmission, reconnection.
- Restart requests from each peer: yes reloads the same map/rules on both sides,
  loading barrier, no rejects/desync; no/timeout/disconnect/simultaneous requests
  notify correctly and do not restart unexpectedly.
- Final statistics and chart after wins/surrender on both peers, no solo high-score
  submission; both finish viewing before returning to the same lobby with names,
  map/rules preserved and ready flags cleared; repeated matches and disconnects.
- Lobby names in handicap selection and L02/L07/L16/L17 with world names.
- Original territorial advantage with VK cap OFF; cap choices 40/30/25/20%; fort
  loser bonus independently OFF/30/50/75%, military-value ramp +30% to +90%,
  fort-only production, preserved work, activation/deactivation shown in force bar.
- Existing preferences survive replacement of the player folder; changed settings
  create local versioned history, including x2 flags, without resetting other keys.

Completed source validation: 1,025 core/real-map simulation checks, Unity runtime
C# compilation and isolated compilation of the fork build-stamp editor hook.
No rendering, menu interaction, native microphone, online-session or build-stamp
runtime result is claimed. The broad UI/network change warrants a requested
release build for the user's grouped manual checks.


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

## Combat feel laboratory (0.2.1, awaiting player validation)

- FEEL highlights five existing controls: direct 2D following, hull turn rate,
  dodge distance multiplier, defensive boost speed and dodge cooldown.
- Each detailed category shows its main controls first; Advanced reveals the
  remaining filters and fine tuning. Hidden values still apply. No saved keys,
  values, defaults or presets are removed or reset.
- Start from Zod Classic, keep direct following ON and physical limits OFF,
  then tune hull turn rate and boost before distance/cooldown. This is a
  suggested experiment, not a default migration. Zero turn rate means instant.
- Direct following now limits displayed hull rotation while retaining the 2D
  combat position. Sideways motion is intentional in this mode; this does not
  impose a turning radius on combat movement. Physical mode changes combat
  positions and takes priority. The legacy follower remains available for
  comparisons; its catch-up arcs can distort short defensive steps.
- Playground debug: cyan cross = combat position; violet line = model gap;
  yellow line = first queued waypoint (not full navigation path); green line =
  one simulation second of velocity. Reason labels include sprint, stamina,
  simulation speed and model gap. These overlays do not alter simulation.
- Completed: Unity runtime script compilation; 1025 core/real-map checks.
- Pending manual validation on the next requested release: FEEL layout and
  scrolling, each Advanced toggle, per-type overrides and preset round trips,
  debug visibility/readability at different camera angles, turn rate at 0/90,
  direct/follower/physical mode transitions and repeated infinite combat.
  No player build or gameplay tests were requested or performed.

## Universal relay with preventive cutoff (0.2.2, awaiting player validation)

- Private access codes, local credential loading and password fields are removed.
  Existing personal preference keys/choices and old ignored private files are not
  reset or deleted. No Google integration is introduced.
- Every authenticated room seat can request temporary TURN credentials when the
  shared server-side budget allows. The guard includes all TURN keys and SFU,
  closes at 900 decimal GB, fails closed on unavailable analytics/configuration,
  and caps budget grants at 120 per rolling hour across all rooms. Public direct
  play stays available when TURN is refused. This is not a hard spending cap.
- Server activation requires Account Analytics: Read credentials and the account's
  verified UTC billing day, supplied only through secure Wrangler prompts. Until
  configured, the updated service returns STUN only. No live credentials or live
  billing-period settings were guessed from the local clock.
- Completed: 12 server unit tests; real local Workers/SQLite/WebSocket integration
  test with mocked TURN/SFU analytics and real shared cutoff; all Unity runtime
  scripts compile (existing warnings only). No real TURN traffic was generated.
- Pending user validation on the next requested release: Online Settings without
  passwords, two remote PCs connecting directly and by relay, voice, and clear
  direct-only feedback when the allowance is reserved or analytics unavailable.
  Existing long sessions and credential expiry still need manual validation.
- No game player was built or launched. A requested release build is recommended
  because the connection flow and its failure handling changed.

## Live universal relay activation — 2026-11-15

- Deployed source 66ab2a45 to the Cloudflare invitation service with a shared
  900,000,000,000-byte cutoff, TURN/SFU analytics and billing day 14 (Realtime
  subscription renews December 14). Account and analytics credentials are stored
  only as Worker secrets. The user entered the new token through masked local
  input; no token was written to source or chat.
- Live checks passed: two room seats without private codes receive temporary
  UDP TURN credentials, HTTPS invitation page, authenticated WebSockets,
  offer/answer exchange and unauthorized ICE rejection. No actual relay payload,
  game player, voice or rendering test was run.
- The former STUN-only live smoke assertion was obsolete after universal access;
  the signaling smoke test now leaves issuance to the separate relay-live check.
- The server is active. Existing compiled players retain their previous menus;
  a newly requested release build is needed to include the password-free UI.
- The 900 GB threshold blocks new issuance. Analytics lag and already-issued
  six-hour credentials still prevent any guarantee of a hard billing cap.

## Windows executable name (0.2.4, awaiting player validation)

- Future fork builds emit builds/OpenZ-Release/openz-vk.exe. The release launcher,
  invitation registration helper and explicit player-test defaults use that name.
- Unity produces the matching openz-vk_Data folder during the next requested
  build; distribute the entire output folder. Existing players are not renamed.
- Company/product and PlayerPrefs remain OpenZ/OpenZ. Preferences and persistent
  data paths stay compatible. Automatic invitation registration uses the actual
  running executable path; the manual helper now expects openz-vk.exe.
- No player build or game launch was performed. Pending: next requested release
  executable/data folder, launch and invitation registration on the user's PC.

## Lobby release warning (0.2.10, source only)

- Exchange the displayed release/build identity in lobby metadata. Both updated
  players see a persistent warning for different identities, unstamped builds,
  or an unverified peer. Readiness remains available; release mismatch alone
  does not block launch. Schema/settings incompatibility checks remain intact.
- Release announcements cannot change readiness. Old players cannot display a
  warning added after their build; the updated peer reports them as unverified.
- Pending manual validation: matching and mismatching builds on both PCs,
  unstamped/legacy peers, ready races, restart and return to lobby.
- Source compilation checked; no player built or launched.
