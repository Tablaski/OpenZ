# Player validation record

## OpenZ-VK 1.5.57 AI 2026 and TeamPlay

- Source checkpoint `189e5aa8` passed 9,056 core and real-map simulation checks, including 24 AI 2026 checks. All Unity runtime C# scripts compiled with the Unity compiler and references. These checks used the committed source in isolation from concurrent workspace edits.
- Against `2ee3c206`, six fixed-seed legacy runs matched over the first 100 simulated seconds. The two maps were SM07 and SM18, with Easy, Normal and Hard. Sampled unit positions, health, ownership, waypoints, production and cumulative A* counts matched.
- On `96439efb`, Desert P08M01 with seven Normal or seven Zod bots, Friendly Fire and All for One, seed 52, three interleaved runs per generation after warmup. Over simulated seconds 15–60, median mean tick time was 0.870 ms for Normal and 1.086 ms for Zod. The median p99 was 2.678 ms and 3.382 ms respectively. There were 133 and 138 live units at 60 seconds, so the comparisons include the consequences of different decisions. The early-match p99 is above the proposed 5% comparison margin. These are .NET simulation timings.
- Pending in a compiled game. Verify the two difficulty groups and tooltips on the solo deploy screen, the local Skirmish roster and the online host's seat menu. Launch each generation, mix them in teams, change the host combat profile before launch, and try a mixed room with All for One and Friendly Fire.
- Compare evenly matched tanks in OpenZ and Z Classic, then test the higher levels on a bridge map and on Desert P08M01. Check fort defense, scouting with radar loss, range management, team assistance, fort rushing, and speed under a seven-Zod roster. Calibrate difficulty and win rates after longer matches.

## OpenZ-VK 1.5.56 five-biome V4 and preview capture

- Passed 9,032 headless simulation checks. The capture inventory lists 88 enabled AI Maps. The runtime C# scripts and the V4 HLSL fragment include compiled separately. Native map files, decoded grids and build scene assets were unchanged.
- Pending in Unity. The editor returned 198 and reported no valid licence. Use `tools/Refresh-AiMapPreviews.ps1` for the 88 map videos and five matched V3/V4 comparisons once Unity is available. The currently published package contains the earlier previews.
- Inspect snow and gravel, frost boundaries, open water, both bridge axes and the preserved ramps in the Arctic maps, including an intact, destroyed and repaired crossing. Compare both sides of each bridge in the captures. Verify that robots can still wade in the original Arctic water and vehicles use the same crossings.
- Review the 88 unique clips and check the selected map's video in solo, the online lobby and local Skirmish. Confirm stationary units throughout, distinguishable factories, radars, forts and rock formations, and the high straight road on SM16. Check that no shot frames the whole map at once and check poster fallback.

## OpenZ-VK 1.5.54 AI Maps V4

- Source checks passed on `5a9edcb4`. The Unity runtime C# scripts compiled with existing warnings, the V4 HLSL include compiled separately as a Direct3D fragment shader, and 8,816 headless checks passed, including 70 V4 checks. The V4 changes leave the native maps, grids and scenes byte-for-byte unchanged.
- Pending in Unity. The local Editor reports no valid licence. Compile the terrain shaders, compare V3 and V4 at identical camera positions in 4K, and review ground and roads on SM02, SM08, SM16 and SM17.
- Verify both edges and approaches of every bridge in the comparison scenes, including Desert P03M01, the two lava bridges on SM08 and destroyed spans on SM17. Move robots and vehicles through intact and repaired bridges. Confirm destroyed spans remain blocked and ground picking works near the rails.
- Check the raised central road shelf and flanking depressions in SM16 against the native map and official OpenZ scene. Check continuity along the shelf, the open entrances and native side gaps. Try both starting sides, ground picking around the lip and object placement on the plateau. Verify that `-oz-ai-maps-v3` restores the previous visuals.

## OpenZ-VK 1.5.53 local Skirmish defeat

- Pending in a compiled game. Lose your fort in a local Skirmish with two or more opponents. Check that the camera follows the falling fort, then the Defeat screen shows the statistics and Play Again works. Repeat with a swapped seat, surrender, and a team match with All for one. Check that the loss of a bot alone leaves the match running and that an early online elimination still leads to Ghost view.

## OpenZ-VK 1.5.52 restored AI Maps

- The restored entries passed the 8,746 headless simulation checks, including mode selection and native map data.
- Pending in a compiled game. Check the four restored AI maps under Z Original in solo and in the two-player lobby. Check previews and launch each map in both modes. Verify that the matching official levels still launch from OpenZ Maps and Ballerman's Map is absent.

## OpenZ-VK 1.5.51 team chat

- Pending in a compiled game. In an online team match with a Ghost, check teammate delivery from both host and guest, switch to TO ALL, verify the highlighted prefix and delivery to every seat and Ghost, then switch back to TO TEAM and verify opponents receive nothing.

## OpenZ-VK 1.5.50 equal teams and All for one

- Pending in a compiled game. Check the disabled Teams button on a three-start map and the launch warning for uneven teams. Check 2 × 3 and 3 × 2 with six positions and 2 × 4 and 4 × 2 with eight. Include bots, closed positions and Ghosts.
- Check the online and local defaults, the biceps icon and explanation, and that changing the rule online clears readiness. With All for one on, trigger a fort loss by destruction, infiltration and loss of the last unit. Check allied forts explode together and the camera focuses on the first fort. Include a three-team match to verify an early team loss and a separate final elimination. Repeat with the rule off.

## OpenZ-VK 1.5.49 team matches

- Passed source validation. All Unity runtime scripts compile. The headless suite passed 8,524 core and real-map checks, including 30 targeted teamplay checks, plus 464 separate behavior and movement checks.
- Pending in a compiled game. Play local Skirmish and online matches with human and bot teammates, including two bots on the same team. Check that bots defend their own fort first, relieve a threatened allied fort or territory, and can reinforce an allied attack or take an undefended enemy flag. Compare with solo and free-for-all bots.
- Try normal, Ctrl and queued attack orders against allied units and buildings. Check that the Friendly Fire option still controls incidental damage and that tanks and Jeeps do not ram or run over allies.
- Hover over an allied flag with both an eligible and an ineligible force. Check the capture cursor or red cross, including hardware cursor mode and a Ghost following the player. Confirm a blocked click leaves existing orders intact. Move onto the flag and check that it transfers with its buildings only if the claimant owns fewer factories and the flag is closer to the claimant’s fort. Confirm treating a ruined factory as still owned and that an in-team transfer leaves the shared radar and territory discount active.
- Send a damaged vehicle to an ally’s repair centre by click and by hotkey. Check the queue, the repaired unit’s owner and what happens if the centre changes hands. Include a shuttle where available.
- Capture two radars with different allies, then lose them one at a time. Compare host and guest minimaps, sweep colours, enemy tracking and health bars, fog, zoom and radar alerts. Check that construction times change with the alliance’s combined flag share, including with a custom territory cap and an explicit seat handicap.

## OpenZ-VK 1.5.48 in-match map changes

- Pending in a compiled game. In a duel and a room with three or more players, open Ask to Change Map from a guest and a host. Check the named HUD countdown for the requester, other players and Ghosts. Verify time and orders stay frozen through selection and through the fresh 30-second response window.
- Check gallery filters, paging and search, the current map marker, factory tech adjustments and reset. Verify accepted changes carry the proposed values and leave the other lobby rules intact. Check cancellation, selection timeout, veto or decline, and each mode's existing response-timeout behavior.
- After a cancelled or refused request, check that the same player sees the remaining three-minute wait and that a different player can request immediately. Check that an applied map change clears the wait.

## OpenZ-VK 1.5.47 Skirmish and multiplayer loading

- The five catalog biomes have matching 1920 × 1080 backgrounds in the local AI Maps package. The multiplayer suite still stops at the baseline assertion `Team edits publish as one revision` in `MultiplayerRoomChecks.cs:71`.

- Pending in a compiled game. Start a Skirmish on a wide or tall AI Map with bots. Check the planet, unclipped minimap, readable map name, progress and preparation text. Repeat on an official map and at 16:9 and 4:3.
- Start an online 1 vs 1 match and a room with three or more active players and a Ghost. Delay one active player's load and verify that ready clients show MAP LOADED while they wait, then enter the match together. In a match with opening orders, check the transition into the planning phase.
- Repeat after a restart and a map change. Check that the new map title, minimap, planet and progress replace the previous values. Disconnect during loading and verify that the error screen remains usable.

## OpenZ-VK 1.5.46 multiplayer results and lobby return

- Source compilation and a focused two- and eight-team final-statistics round trip passed. The multiplayer suite stops at `Team edits publish as one revision` in `MultiplayerRoomChecks.cs:71`. The same failure was reproduced at baseline `6f6a7293`.

- Pending in a compiled game: compare the host and guests' final counters and force histories in three-player and eight-player matches, including a team win and an earlier elimination. Check Victory and Defeat, long names, the chart toggle with keyboard and mouse, and the table at 16:9 and 4:3.
- In two-, three- and eight-human sessions, let different players return by button and by timeout. Verify Waiting and Not Ready on the correct player rows and that another match can start after everyone returns. Include the host returning first, a player leaving from results, a Ghost, a direct-to-relay route change and a mixed human/bot room. Confirm solo and bot matches have no countdown.

## OpenZ-VK 1.5.45 multiplayer routing and compression

- Pending in a compiled game using the updated invitation service. On two networks that can connect directly, start a 1 vs 1 game from a magic-link lobby. Verify Direct in Connection Details and compression Off by default in the host's network diagnostics (Ctrl+Alt+Up with the pointer in the top-left corner).
- Add a third human and verify SFU. Have that person leave and verify the two remaining humans return to Direct without a new invitation. Repeat the route change during a match and confirm the surviving guest receives a full battlefield update. Include a departure during the results sequence.
- Check a pair that needs TURN and a room using SFU. Verify compression starts On, view the wire-state rate on a busy map, toggle compression Off and confirm state updates continue. Reset Tuning restores the route default. Start another match and verify the default again.

## OpenZ-VK 1.5.44 final fort camera

- Pending in a compiled game. In a three-or-more-player match, eliminate a player and verify that the other players see the eliminated player's name in the HUD and retain camera control. Verify that the eliminated player can switch to Ghost view.
- Destroy the final opposing fort and verify that the winner, the last eliminated player, earlier eliminated players and Ghosts all see the same fort before the results. Include Ghosts in Free Cam and player follow mode. Repeat with the host eliminated, in a team match, and with two eliminations close together.

## Experimental dynamics and radar-free maps

- Pending in a compiled game. Restart after selecting Z Classic and verify that solo defaults to OpenZ, then explicitly select Z Classic. Check its flask icon and experimental description in solo and in a new multiplayer lobby. Verify saved custom profiles remain available.
- With each radar-loss setting, play maps with no radar and confirm no fog or zoom penalty applies. On a map with radars, lose and recover control of the last working radar and verify the selected penalty activates and clears as before.

## OpenZ-VK 1.5.43 Ghost point of view and connection HUD

- Pending in a compiled game. In an eight-player online match, check that the Ghost controls, scores and pings fit without overlap at 16:9, 16:10 and 4:3. Switch directly between players, return to Free Cam, and check visibility when a player disconnects or is eliminated.
- Follow a player while they pan, zoom, move the cursor, select units, queue an order with waypoints and change a factory's production. Compare both screens and verify that the spectator sees the same native controls and cues.
- In a match with fog of war, check that follow mode uses the followed player's visibility and that Free Cam can toggle the fog. In a match without fog, verify the fog control is hidden.
- Check that the network indicator follows each seat across ordinary latency, connection degradation and disconnects. Verify that bots and unused seats do not show fabricated network readings.

## Game options controls

- Pending in a compiled game. In both Game options menus, use the arrows in both directions and check wraparound for Flag Size (1x, 1.5x, 2x, 2.5x, 3x) and Minimap Radar Sweep (Off, 1.5 s, 3 s, 5 s). Check live flag scaling and radar sweep timing.
- Toggle damaged tank effects on and off. Check the oil trails on moving critically damaged tanks and the shake on stationary and moving critically damaged tanks. With Reduced Motion on, check that tank shake is suppressed and radar outlines remain visible at an active interval.
- Check saved preferences after reopening the game, including a legacy 10-second radar interval, a disabled radar setting and a profile with just one of the two tank effects enabled.

## OpenZ-VK 1.5.42 radar-loss camera framing

- Pending in a compiled game. On Car Park at 16:9, compare the locked view near the red fort with the reference capture (97 m height, 55° pitch). Check Zoom Lock and Fog + Zoom in solo and 1 vs 1. After losing the last radar, verify that wheel, touch and gamepad cannot change the zoom and that panning remains available. Recover a radar and verify that zoom controls return. Check normal camera behavior with Fog and Off.

## OpenZ-VK 1.5.41 solo factory technology

- Pending in a compiled game. Open Factory Tech from the deployment bar in the official campaign and AI Maps. Change a factory level on an official map, deploy, and verify the level and difficulty tech bonus in play. Verify an unrelated map keeps its own settings and Restore Campaign Defaults restores the official map's default behavior.

## OpenZ-VK 1.5.40 fort destruction in multiplayer

- Source checkpoint `5ebb4b02` compiled against the Unity 6000.3.27f2 release runtime references. Skirmish simulation checks reached the room test suite, which stopped at `Team edits publish as one revision`. The same assertion fails on the parent checkpoint `75fde69e`.

- Pending in a compiled game. In a two-participant match, destroy either fort and verify both clients see the existing ending and results.
- Start an online match with at least three occupied seats, including a bot, and repeat with the host and a guest eliminated first. Verify only the defeated client sees the cinematic camera, while the other clients retain camera and unit control and see one elimination notice. Check that the flyover and explosion remain visible when looking toward the fort, including when two forts fall nearly together.
- After the sequence, verify free camera, following surviving human players, the Ghost overlay, text chat and order rejection for the eliminated client. Verify the last surviving team still triggers the normal results and starting a new match clears the previous Ghost state.
- In local Skirmish with two or more bots, verify the same camera separation and that the match continues after a non-final defeat.

## OpenZ-VK 1.5.39 solo battlefield selection

- Pending in a compiled game. Check that Gaming Options and Allowed Units stay in the deployment bar to the left of Difficulty and Head Start in both the OpenZ campaign and AI Maps. Check 16:9, 16:10, 4:3 and 5:4, switch between maps with and without the Shuttle option, and open and close both panels. Confirm the objective, faction label and AI Maps category controls remain readable.
- In Gaming Options, switch between OpenZ, Z Classic and a saved custom profile, then change the radar-loss rule. Check that each active value is yellow, inactive options are grey and the choices persist when reopening. With no saved profiles, check that Custom is greyed out and cannot open.

## OpenZ-VK 1.5.38 radar and flag controls

- Pending in a compiled game. Verify the four interval buttons and five flag size buttons in both Game options menus. Confirm flag and pole scale changes live on territory, fort and Unit Editor flags.
- Time each radar interval, including 1.5 seconds. Check that the minimap icon turns white and the battlefield brackets appear together for each sweep with Radar Loss Consequence off and on. Check Minimap Radars off and the static minimap outlines with Reduced Motion enabled.
- Reload an older settings profile with the saved 4x flag size and another with 2x. Verify the choices read as 3x and 2x, respectively, and that the previously saved 3, 5 or 10 second intervals carry forward.

## OpenZ-VK 1.5.37 Desert rendering experiment

- Source checkpoint `b020f292` compiled against the Unity 6000.3.27f2 release runtime references. The corresponding Desert material has instancing enabled. The 4K FPS target remains unverified.
- Acceptance target is at least 30 average FPS on Desert P08M01 at 3840 × 2160 with seven Medium bots and the maintainer's graphics options. The comparable 1.5.34 session measured 15.0 fps with 66.3 ms on the main thread, 5.1 ms on the GPU and 24.9 ms in the rules per frame.
- Run the same compiled game with and without `-oz-batch-desert-rocks`, keeping graphics, camera, seed and bot roster fixed. Compare post-loading average FPS, main-thread time, the 99th-percentile frame and batch counts. The `[OpenZ Rock Batches]` line reports eligible pillars and instanced submissions.
- Pending on the compiled game. Check day and night, shadows, reflections, unit occlusion, zoom and panning, rock damage and collapse, returning to the menu and starting another match. Keep the opt-in off if there is a visual regression.

## OpenZ-VK 1.5.36 minimap radar and territory border contrast

- Pending in a compiled game. Check the full Minimap Radar Sweep Interval label in both Game options menus. Time several consecutive sweep starts at each interval (3, 5 and 10 seconds), including after a live interval change, with Radar Loss Consequence off and on.
- Check one and several friendly radars, losing and regaining the last friendly radar, enemy radar visibility, Minimap Radars off and Reduced Motion on.
- Compare dark grey neutral dots on Arctic and white neutral dots on City and Bridge Game. Check red and blue next to lava, rivers and coastal water in daylight and at night, at x1, x2 and x4 and at maximum zoom-out. Verify owner colours after capture and a team-colour swap, and check that border dots still follow the ground and shallow water.

## OpenZ-VK 1.5.34 large-map performance

- Source checkpoint `7d6fc13a` passed 8,488 core and real-map checks, including byte-for-byte comparison of the room's combined capture and delta path with the prior sender. Coverage includes changed and unchanged scenery, in-place map data edits, removals, full refresh and restart.
- An independent .NET comparison against `afc0ef17` matched cumulative per-tick rule fingerprints, final unit counts, and A* search and work counters for all 54,000 measured ticks across Desert P08M01 and Hunters.
- Desert P08M01 has 2,316 destructible rock objects in the native grid and 2,316 individually matched rock pillar names in the converted scene. Hunters has no rock objects. Scene-only geometry and map tiles do not enter the room object delta. A rock pillar with no simulation health source is no longer registered in the health polling loop. No Desert rock was excluded as decorative, and the impact of rendering the static map border remains unmeasured.
- In the .NET replication benchmark, the combined host path averaged 8.8 ms per update on Desert with seven bots, down from 15.2 ms in the same session. Unity builds overlapped the timing runs, so the size of this gain is provisional. Payload sizes and the count of accepted updates matched.
- The Windows release build stamped `7d6fc13a` passed the local seven-bot proof on Desert P08M01 at 1080p, DX12 and maximum zoom-out. It measured 35.1, 33.2 and 33.5 fps across three post-warmup windows. The paired default-zoom run and the queued 4K run were stopped at the maintainer's request.
- The direct three-client Ghost proof reached the battlefield, then failed to receive the host camera and production state. The logs reported no battlefield snapshots during the sampled windows. A matched repeat and obstacle damage, refresh and reconnection in a large online match remain pending.
- Verify that unit selection, hover precedence, damaged-unit bars and squads moving off camera behave consistently in Skirmish and on a guest.
- Verify that decorative border pillars without a simulation object stay visible, and that bound pillars still crack and collapse after explosives damage in solo and on a guest.

## OpenZ-VK 1.5.33 online lobby and Ghost spectators

- Windows build `70a36c96` passed 7,651 core and real-map simulation checks and Unity wire validation. The native WebRTC direct and local SFU checks passed. All 44 installer checks passed.
- Three compiled instances on one PC joined using the same online invitation. All three reached the match with distinct forts and no bots in the active-only scenario. With two active humans, one Ghost and a bot, the match launched without the Ghost readying up. The Ghost received the host camera and open fort, then switched to free camera. That scenario passed with default transport and again with relay forced.
- Reports and captures are in `builds/captures/three-seated-1.5.33-final/`, `builds/captures/ghost-1.5.33-verified/` and `builds/captures/ghost-relay-1.5.33-verified/`.
- Pending in the compiled game: manually check the minimap gallery, shared settings, team drag and drop, unit pickers, AI Help, connection details, text and voice, and Ready lock and cancellation. The capture shows missing glyphs on two compact voice buttons.
- Switch an eight-human lobby to a four-start map. Confirm the four active players, four Ghosts, single rules revision, spectator free camera, isolated match communications and return to lobby. Run once over direct transport and once over relay.
- Start an official two-start map and an AI map with bots. Check initial armies, radar penalties and the optional idle-unit and factory-capture AI Help rules.
- Run `tools/Test-OnlineGhost.ps1 -CompiledGame <build-folder>` from PowerShell to exercise one host, one active guest, one Ghost and a bot on a single Windows PC. Confirm each report passes and check the two captures. Repeat with `-RelayOnly` if the relay offer is active.
- Verify that the Ghost can follow each surviving human, toggle the cursor, selected units and orders, production and player fog, leave the tracked camera and recenter. Check that the Ghost never issues gameplay commands or receives or transmits active-player match communications.

## OpenZ-VK 1.5.32 Graphics Custom

- Pending in a compiled game. In the main menu and in solo and multiplayer pause menus, select all four presets at common and narrow aspect ratios. Verify that all four buttons remain visible, that their tooltips and selection styles work, and that the five violet rows appear only in Custom.
- On Bridge Game, verify hardware Off, MSAA ×2 and ×4 independently of post-processing Off, FXAA and SMAA. Confirm combined MSAA and post-processing, switching from one mode to another live, and restoring each OpenZ preset's render settings. Check DX11, DX12 and the Mac render-scale path when available.
- Compare Light and Off volumetric fog near night lamps and smoke. Verify that fog of war and briefings retain their own settings. Toggle water object reflections by a bridge, verify that the mirror pass stops, and check that preset selection restores the original water behavior.
- Use thermal sensor readings or a controlled QA temperature source to verify the 90 °C and 85 °C thresholds, the 30 FPS cap and VSync, toggling the bypass while capped, and returning to an OpenZ preset while still in the thermal band.
- Restart with each preset and check saved Custom settings. For older profiles, confirm that explicit anti-aliasing settings, fog choices, reflection and thermal toggles carry over. Custom uses High as its rendering base, so existing Medium and Low users with overrides should verify both the migrated Custom profile and their original preset.

## OpenZ-VK 1.5.31 Skirmish lobby

- The game scripts compiled against Unity 6000.3.27f2 references, and all 7,647 core and real-map simulation checks passed. The Unity editor batch run did not finish script import because its licensing client failed to connect.
- Pending in a compiled game. Open Multiplayer → Skirmish at 16:9, 4:3 and ultrawide resolutions. Check the three aligned panels, complete minimaps, 3D preview, map tech, opening orders and the Start Game button.
- Switch between OpenZ Maps and AI Maps. Verify the 2, 3, 4 and 8 player filters and that 6 is disabled. Check that Desert P08M01 and Hunters each start with one human and seven bots.
- In Teams and Free For All, use each All Bots difficulty button and check that every bot changes difficulty in place. Change one bot individually and check that the group selection clears for the mixed roster.
- In a local Skirmish match, cancel Quit to Lobby once and verify the match can resume. Then confirm Quit to Lobby and verify the map, teams, bot difficulties and rules are available for another match.
- Move participants by dragging and by selecting a card followed by a team. Verify dropping outside a team leaves the assignment intact. Check difficulty, scrolling with eight participants, adding a team, Free for All, swapping starting sides and the two-opposing-teams requirement.

## OpenZ-VK 1.5.30 solo map selection

- Pending in a compiled game. Check the campaign button between High Scores and Back, the robot head in AI Maps labels, and the settings controls on both wide and compact canvases.
- Switch among the three AI Map categories and the OpenZ campaign. Check that each group remembers its selected map and only the current card displays its flyby and minimap overlay. Deploy a Zod Engine map against the computer and return to mission selection.

## OpenZ-VK 1.5.29 AI Maps

- Pending in a compiled game. Open solo AI Maps and change the selection via mouse and keyboard. Verify that only the selected card plays its preview, the minimap disappears from the previous card, and unselected cards display the entire battlefield.
- Verify that the Experimental category is absent from solo and the 1 vs 1 gallery.

## OpenZ-VK 1.5.28 anti-aliasing modes

- Pending manual checks: compare the five anti-aliasing modes on Bridge Game at 4K High. Verify off, FXAA, SMAA and both MSAA modes run independently. Check the tooltip in both menus, live switching, saved choices, and the fresh-install High preset.

## OpenZ-VK 1.5.27 independent MSAA

- Pending manual checks: compare Bridge Game at native 4K High with MSAA at 4x, 2x and Off. Check the main and pause menus, persistence across restart and preset changes, a new profile’s High/4x default, and migration from the previous 2x limiter. Verify the camera’s SMAA remains active.

## OpenZ-VK 1.5.26 graphics performance options

- Pending compiled-game checks: compare GPU and main-thread time at 4K High on Bridge Game by day and night with each option switched independently. Check that the original fog returns immediately, briefings retain their atmosphere, MSAA restores across preset changes, new profiles show every fork performance toggle off, and existing thermal, reflection and fog choices carry over. The fog switch skips the volumetric calculation and smoke distance pass during play. The shared renderer still performs its fullscreen color copy.

## OpenZ-VK 1.5.25 unit presentation performance

- Pending compiled-game checks: compare main-thread timing with 1.5.24 at 1080p High on official Bridge Game and on Desert P08M01 with seven bots. Verify unit placement around walls, wrecks and factory exits, including moving vehicles and turret-mounted cannons.

## OpenZ-VK 1.5.24 volumetric fog performance

- The 1.5.23 official Bridge Game run logged 53.6 fps at 1920 × 1080 High with 7.9 ms GPU and 17.5 ms main-thread time. A separate native 3840 × 2160 High window recorded 35.8 ms GPU with the target at 60 fps. The in-game scenes and warm-up state differed. The maintainer observed only a small change from toggling object reflections.
- Pending compiled-game checks: compare High at native 3840 × 2160 with lighter fog off and on, at the same camera, time of day and GPU clock. Check fog banks, light shafts and smoke intersections on Bridge Game and Car Park by day and night, and verify that object shadows, the fog of war and the 3D resolution remain intact. Verify live switching and persistence in the Graphics menus.

## OpenZ-VK 1.5.23 thermal frame limit

- Pending manual checks: turn off the thermal limit while it is active and verify the selected frame rate cap returns. Check preference persistence and the Detailed counter with and without a temperature reading. The Windows build also covers the pending water reflection checks below.

## OpenZ-VK 1.5.22 water reflection performance

- The maintainer's 1.5.21 official Bridge Game session used DX12 at 3840 × 2160. High and Medium drew the scene at native resolution, while Low drew it at 1440 × 810. The session changed presets during sampling. The GPU reached 90 °C twice and the heat guard applied its 30 fps limit, then restored 60 fps at 84 °C. The mixed samples cannot quantify the cost of reflections.
- Pending compiled-game checks: compare High and Medium with the reflection setting on and off at 3840 × 2160, on the same official Bridge Game camera path after the briefing. Record frame time, GPU time, temperature and the active cap. Verify the option persists and applies during battle, Low continues to skip object reflections, and camera pans and zooms preserve the look of the river and sea-skirt reflections when enabled. Check Car Park and the DX11 fallback as well.

## OpenZ-VK 1.5.21 building outline performance

- Source checkpoint `51df3903` compiled successfully for Windows in release mode. On Desert P08M01 with seven Normal bots, 1080p Low and DX12, the first 40-second window reported 20.2 fps and a 122 ms 99th-percentile frame. Its final frames coincided with returning to the main menu, so the window is provisional. The preceding engine summary reported 3.30 ms of hover per frame, compared with 8.78 ms in the corresponding 1.5.20 summary.
- The compiled game was closed through the menus before the remaining two windows and the cursor comparison. Pending checks include the full benchmark and outline parity while moving, zooming, selecting buildings and entering first person.

## OpenZ-VK 1.5.20 rock-heavy map performance

- Source checkpoint `b6a11bef` passed 7,714 core and real-map simulation checks. The two fixed-seed seven-bot simulations matched the 1.5.18 fingerprints, bot-order counts and A* counts after 4,500 ticks on Desert P08M01 and Hunters. Concurrent build work makes the headless timing measurements unsuitable for comparison.
- The 1.5.20 Windows build passed the seven-bot proof, including 35 cursor positions against the full hover pass. Desert P08M01 at 1080p Low in DX12 measured 15.7, 16.8 and 16.6 fps across the same three windows. The 99th-percentile frame took 138, 93 and 90 ms. In the stable minute, the main thread averaged 62.2 ms, the GPU 2.2 ms, hover 8.04 ms and simulation 3.07 ms per tick.
- Desert P08M01 remains above the 16.7 ms frame budget for 60 fps. Local logs are under `.local/perf-1519/`. Pending checks include eight-human CPU headroom, graphics quality above Low, and the remaining interaction cases listed below.
- Both 1.5.20 Bridge Game runs hit the benchmark's 30 fps hold at 83 °C. Neither run is valid for a performance comparison.

## OpenZ-VK 1.5.19 large-map performance

- Source checkpoint `c19f4077` produced a non-development Windows build and passed 7,714 core and real-map simulation checks. The seven-bot proof matched full-pass hover results at 35 cursor positions in both graphics APIs.
- Desert P08M01, seven Normal bots, seed 1, 1080p Low and a 60 fps cap. Three 40-second windows after warm-up measured 14.5, 15.2 and 14.8 fps in DX12 and 14.7, 15.7 and 15.1 fps in DX11. The 99th-percentile frame took 103–140 ms in DX12 and 102–139 ms in DX11.
- In the stable minute, the DX12 build spent 68.6 ms on the main thread and 2.3 ms on the GPU. The simulation took 3.28 ms per tick and hover took 9.06 ms per frame. The prior manual 1.5.18 run reported 6.1–6.4 fps, 7.09–8.06 ms per rule tick and 23–26 ms of hover per frame. The camera and sampling procedure differed.
- A diagnostic run found the shared shader warm-up could do work during battle on converted maps. The revised build defers it during battle. Remaining checks include first-use shader effects, fog and boarding interactions, official Bridge Game and CPU headroom in eight-human SFU and direct matches.

## OpenZ-VK 1.5.18 Battle and Skirmish performance

- Source checkpoint `db82918b` passed all 7,714 core and real-map simulation checks and compiled with the Unity runtime references. Native WebRTC passed the direct duel and Skirmish packet checks and the SFU-style local loopback.
- A fixed-seed .NET comparison with `126958ba` matched the rule fingerprints, bot orders and path-search counts over 4,500 ticks on Desert P08M01 and Hunters with seven Normal bots. Six measurements per version gave median simulation times of 1.139 to 1.072 ms per tick on Desert and 0.182 to 0.188 ms on Hunters. These timings exclude Unity presentation and transport.
- The local Windows release build from `44ebfe77` carries the modified-tree flag for existing workspace changes. On an i7-13700HX and RTX 4090 Laptop at 1080p Low, official Bridge Game at the widest zoom averaged 42.8 fps in DX12 versus 46.3 fps on the archived 1.5.17 build, with 1% lows of 22.2 and 21.4 fps. These are single five-view passes. The two 1.5.18 DX11 attempts hit the benchmark's 30 fps thermal hold and were invalid for comparison. The 1.5.17 DX11 pass averaged 46.2 fps.
- Desert P08M01 with seven Normal bots on 1.5.18 (1080p Low, DX12) averaged 6.1 and 6.4 fps after loading. Main-thread time averaged 164 and 157 ms per frame, simulation took 81 and 71 ms, hover processing took 23 and 26 ms, and GPU time was 2.1 ms. The rule timing was 8.18 ms per tick, including 2.15 ms in pathing, 1.57 ms in movement and 0.13 ms in bot logic. No matched 1.5.17 comparison was made for this scenario.
- Remaining performance comparisons: repeat a valid DX11 pass and test both APIs at 4K and under night and combat conditions. Compare Desert P08M01 and Hunters with matched starting armies, camera settings and rule configurations on both versions.
- Verify distant and close unit spacing, boarding, destroyed units, rock damage and debris, repaired paths, and water reflections during camera and water-height changes.
- Measure a local battle with seven bots and eight-human matches on both SFU and direct WebRTC. On host and guests, compare CPU and GPU frame times, peak simulation and snapshot work, state rates and queue depth. Exercise a slow guest, reconnect and route fallback. The target is 60 fps at 1080p with 4–6 ms per frame available for simulation and network work on the reference laptop.

## OpenZ-VK 1.5.17 Skirmish lobby, converted terrain and Solo factory tech

- The 1.5.16 baseline passed the two-instance L02 LAN duel proof for proposals, chat and voice, combat and the synchronized result. Its native menu proof stopped at an assertion that expected the 22-unit catalogue to exclude the two Bridge Game tanks.
- On 1.5.17, 7,713 core and real-map checks passed. Unity compiled the runtime and produced a Windows build from `0f1ab721`. Unity loaded all 23 larger Skirmish scenes and rendered their V3 lobby previews.
- Local Skirmish passed all five tabs, a changed balancing rule in a two-start match against one bot, six active participants with the two unused starts closed, and Desert Fortress with three bots and V3 terrain active.
- Online Skirmish passed launch with the host and two bots through the invitation service. A separate room seated three humans using the same link on three instances of one Windows PC. All three readied up and entered the same match with distinct forts and no bots. The host's lobby showed the SFU route.
- Solo Factory Tech passed restoring original tech, saving a vehicle override for Death Valley, and applying that override in the loaded mission. Evidence from the final Windows build is under the local `.local/skirmish-1517-final/` directory.
- Remaining compiled-game coverage includes multi-network play, fallback and reconnect, voice and moderation in a larger room, a full match and return to the lobby, and traversable crossings across all terrain families. The Solo proof does not cover replay, playing as Blue or both values of the difficulty tech bonus on every mission.

## OpenZ-VK 1.5.16 Skirmish lobby and balancing

- Pending compiled-game check: browse the five tabs while eight places, invitations, chat and microphone remain visible. Close and reopen a seat, seat a waiting guest, configure shared and individual starting units, and verify that a setup edit clears readiness.
- Pending host and guest check: mute/restore a guest, check visible mute and text chat, kick with a private message and check a room ban on a separate network. Exercise the match player list as host and verify guest permissions.
- Pending gameplay check: use alliance and free-for-all setups to measure the progressive territory and fort bonuses across unequal army values. Verify the default OpenZ modifiers, construction progress, extra unit placement and closed-start cleanup.

## OpenZ-VK 1.5.15 Skirmish network

- Pending deployed SFU and compiled-game check: use the same magic link on three Windows PCs. Verify all names, positions, readiness, voice and chat. Launch P03M01, verify orders from every seat and check that surrender affects the correct participant. Repeat on an eight-start map with up to eight humans and with mixed bot teams.
- Pending direct fallback check: on staging with an intentionally closed relay allowance, verify the same invitation and a match with three humans. During a relayed match, close the allowance and verify the direct reconnect, full battlefield update, continued orders, vote and return to lobby. Record what happens if a player's network blocks direct connections.
- Pending performance comparison: record updates per second, RTT, jitter, queue depth, snapshot payload and frame time on the same map at 3, 4 and 8 humans. Compare the original 1 vs 1 with STUN/TURN and check the relay's usage across multiple matches.

## OpenZ-VK 1.5.14 skirmish teams

- Pending two-PC check: assign both humans to Team 1 and the bot to another team on P03M01. Confirm the guest sees the team choices and that changing a team clears Ready. Confirm allied targeting and flag captures are blocked, allied forts remain intact on a shared victory, and both humans see and hear the victory result.
- Pending regression checks: use four or eight starts with mixed human and bot teams, change bot difficulty without changing its team, test the vote and return-to-lobby paths, reject a launch with everyone on one team, and repeat a free-for-all and a legacy duel.

## OpenZ-VK 1.5.12 multiplayer map filters

- Pending compiled-game check: open Multiplayer, create a duel and browse the AI Maps gallery through 2, 3, 4 and 8 Players. Check the terrain filters, tactical owner colors, preview-only status and that the official map selection remains unchanged when returning from a larger format. Apply a two-player map and confirm the guest receives it. Verify the Solo gallery and invitation flow.

## OpenZ-VK 1.5.11 force bar

- Pending compiled-game check: in Solo and 1 vs 1, switch between Units, Attacking Power and Both using Game options. Check that unit counts carry only the unit limit, that each percentage sits next to its attacking power score in both power views, and that the second row clears the territory tab. Check the two local preferences independently after restarting, including a migrated 1.5.10 preference, and verify the display at 16:9 and ultrawide resolutions.

## OpenZ-VK 1.5.10 duel lobby and balancing

- Pending two-PC lobby check: copy the invitation before the guest joins, check that the window closes when they arrive, rename from each seat and verify both Ready states stay visible while switching tabs. Scroll a conversation with several wrapped messages and try Mute, Always On and Tap to Speak with the input meter and gain shown.
- Pending setup check: verify that only Map shows the tactical inset, flybys start automatically, tech changes apply independently to the map’s factories, and resets restore forts and factories. Edit four common fort turrets, then load and save the separate near-fort unit preset. Submit and accept a guest proposal across tabs.
- Pending match check: verify Opening Orders Off, 1 s and 20 s from both seats, including the notification countdown. Check army-gap thresholds at 20% and 50%, preservation of production progress and the wider howitzer spread only when enabled. Compare the default force bar in a duel with the unchanged solo preference. Return to the lobby after choosing Z Classic and confirm OpenZ is selected. Check damaged-tank effects in local Game options.

## OpenZ-VK 1.5.9 Bridge Game AI Maps

- Pending in-game check: select Bridge Game from both catalogues in solo and 1 vs 1. Verify the AI previews and deployments, and check that solo uses the original conversion and 1 vs 1 uses the balanced conversion. Replay the AI mission in solo and reopen a hosted AI lobby to confirm the selection stays in AI Maps.
- Pending lobby check: switch between an official map and an AI map from both seats. Verify the correct clip plays muted in the lobby, the AI stamp follows the selection, and the tactical view still shows the starting armies.
- Pending pause check: tap Escape to open and close the match menu. From each seat, click Pause Match once or hold Escape for two seconds to pause. Check that a held key only triggers once, its release does not open the menu, and only the player who paused can resume with the button or another hold. Check cancellation during chat, confirmation dialogs and focus loss.

## OpenZ-VK 1.5.8 starting units and match network display

- Pending in-game check: on an Arctic AI Map with a neutral Medium Mech, use the OpenZ roster and board it with a robot. Replay with the Z 1996 roster and check the model, minimap marker and boarding target are absent. Compare both seats in 1 vs 1. Verify the Medium Tank remains available with the Z 1996 roster.
- Pending Bridge Game check: in solo and on both 1 vs 1 seats, exclude only the Bizon, then only the Mammoth. Check the excluded tank and its original scene placement disappear. Board the allowed tank. Check that a solo custom roster saves both permissions and that OpenZ still includes both tanks.
- Pending HUD check: check Ping and Sync Hz next to the force display at 16:9 and ultrawide resolutions. Check the update during a live 1 vs 1 match and the TURN label on a relayed connection.

## OpenZ-VK 1.5.7 radar sweep interval

- Pending manual check: cycle through 3, 5 and 10 seconds in the main menu and the pause menu. Time consecutive sweeps from the same radar in Solo and 1 vs 1, check the value survives a restart, and verify that two 1 vs 1 players can use different intervals.

## OpenZ-VK 1.5.6 radar sweep

- Pending solo and 1 vs 1 check: select Radar Loss Off with Minimap Radars enabled and compare the minimap with stock OpenZ, including Mayhem's original fog reveal in solo. Test Fog, Zoom Lock and Fog + Zoom, and check that a friendly radar's marker lights up white during each white sweep and returns to its team color between sweeps. Check Reduced Motion, the local Minimap Radars toggle, and that a guest's display follows the host's selected consequence.

## OpenZ-VK 1.5.5 radar visibility

- See the OpenZ-VK 1.5.42 radar-loss camera framing check for the current zoom level and controls.
- Pending minimap check: compare friendly, neutral, visible enemy, fogged and ruined radars. Check that capture or repair starts the friendly sweep and enemy capture or destruction stops it. Check clipping near the map edge, Reduced Motion, and the Options → Game toggle before and after restarting.

## OpenZ-VK 1.5.4 duel audio

- Pending two-PC check: verify both players can hear each other in the lobby with Always On, while typing and while entering and leaving audio setup. In Tap to Speak, start and stop with the chosen shortcut, continue typing while transmitting and confirm that typing the shortcut character does not toggle the microphone. Check Mute, input gain, clipping and the lobby transmission indicator.
- Pending Windows audio check: open Microphone / Speakers from the lobby and pause menu, adjust voice playback, change Windows volume and unmute the default output. Switch between a headset and speakers, then confirm game sound and voice resume on the selected output and voice playback volume survives a restart.

## OpenZ-VK 1.5.3 solo options and duel assistance

- Pending visual and interaction checks: open Gaming Options in both solo catalogues, select tank and robot profiles, open the Discord link and verify each radar rule before and after losing radar.
- Pending duel checks: verify host and guest handicap assignment, the five-second idle warning, the selected unit's green route, manual override and the 10/20 second cooldown. Capture factories with a weak build, a stronger build, and both production options enabled. Check the production badge and queue on both seats.
- Pending Windows invitation checks: open the copied online invitation from a browser with the game closed and use Join in OpenZ. Open a TCP invitation with its protocol association registered. Check short named request text for restart, map changes, returning to the lobby and proposed settings.

## OpenZ-VK 1.5.2 HUD and AI Maps

- Pending visual approval: check the larger transparent logo in the solo and duel HUD against bright and dark terrain.
- Pending manual checks: open AI Maps in solo and 1 vs 1 and watch the on-screen cards animate without clicking. Browse across the carousel and scroll the gallery. Select a different map and check that only its video appears behind the selection. In solo, switch to AI Maps while a commander is speaking and confirm that AI Maps plays without map commentary for either faction. Check that commentary still works in the official campaign.

## OpenZ-VK 1.5.1 and OpenZ 1.5

- Passed locally: 6,902 simulation and core checks, native WebRTC loopback, 44 installer checks, and preparation and reuse of the complete 1.5 resource package. The published 0.2.22 and 0.2.34 installers also passed resource discovery, migration and reuse against a simulated release feed with the 1.5.1 and 1.6.1 pins.
- Unity verified the enabled scenes, Bridge Game lobby serialization, both Changes & Updates tabs, and all 60 regenerated AI Maps videos and fallback stills. All clips decoded to 96 frames before import.
- Windows x64 compiled game from clean source commit `d0a74817efa38cf0ad9b5cccdd9632fca8b3438c`: all 20 scenarios passed. The build used the production flags and the pinned 1.5 resources.
- Passed in solo: Bridge Game deployment, both big tanks present, breached long bridges, restored bridge visuals and pathing, and tactical pause and resume. Checked independent oil and shake at 31%, 30%, critical and repaired health, and verified that reduced motion suppresses the shake. The bridge was restored through the simulation for this check.
- Passed in 1 vs 1: six maps over local TCP, Bridge Game over WebRTC direct and forced TURN, and the match-controls proof. Verified combat, chat, synthetic voice, proposals, surrender, map changes, frozen host rules, reconnection, lobby return and Windows invitation registration. Host and guest ran in separate processes on the same computer.
- Passed presentation and compatibility: five native video decode checks, the solo and duel AI Maps galleries, both changelog tabs, both Unit Editor entry points, the solo roster, six converted scenes, 200 crater stress rounds, Direct3D 11 and 12 on Bridge Game, and the incompatible-resource recovery screen and exit code.
- The local test script reported a Windows registry restoration error after all scenarios passed. The `openz://` association remained pointed at the tested build. This was a test-environment cleanup issue.
- Pending manual checks: use a crane to repair a long bridge, board the Mammoth and Bizon through gameplay, exercise the tactical-pause binding with physical input, and play a match from two separate computers.
- For a stable release, verify the published installer payloads and the 1.5 resource bundle on a clean Windows installation.
- The published 0.2.34 updater imported source commit `f172fe76eeedcb0521d3266af77f98479b9039ff` without Git in a fresh workspace. It installed all five archives from the local 1.5 bundle, detected the required Unity editor, and confirmed that no download was needed on a second run. The lobby tank changes use the same resource pin and installer.
- Independent Mammoth and Bizon permissions passed all four combinations in the 6,918 core and simulation checks. In the Windows x64 production build from clean commit `48f7842efdf4a1fa03a648c56cea7f170223b794`, the lobby toggles and both presets passed. Two Bridge Game duels each excluded a different tank. Both host and guest saw the permitted tank's model and no placement for the excluded tank. The solo Bridge Game proof still found both tanks.
- Final Windows x64 production candidate `bb49c94809bdc7353bdd6d5644f1d7a36ab07f63` passed 23 compiled-game scenarios. The confirmed match pause was tested from both seats over TCP and Internet WebRTC, including cancel and confirm, owner-only resume, a frozen clock, blocked camera and orders, access to the menus, chat and generated voice, and reconnection while paused. Bridge Game also passed direct WebRTC and forced TURN. Both clients ran on one PC. A match with two physical PCs and microphones remains unverified.
- The final candidate completed a real crane repair order on Bridge Game from the bank and boarded both tanks by exercising the game's waypoint generation and simulation. A Unity Input System keyboard event exercised the real Space binding. Tactical pause accepted unit orders and production changes in solo. This test caught and verified the fix for two reversed shortcut definitions.
- The final candidate also passed the menu, both changelog tabs, the Unit Editor and its direct launcher, AI Maps and bridge lifecycle checks, 200 crater rounds, both tank exclusions, invitation registration, incompatible-resource recovery and Direct3D 11 and 12. The isolated WebRTC test passed after limiting its linked sources to the transport and voice codecs it exercises.

## OpenZ-VK 0.2.40 duel chat

- In a 1 vs 1 match, type a draft, move the caret into the middle and keep typing while the opponent sends several messages and earlier chat notifications expire. Verify the draft and caret stay in place, selected text stays selected, and new messages appear in the history.
- Send with Enter and with the Send button. Verify each message appears once on both sides, the draft clears, and typing can resume immediately. Close the chat and verify incoming notifications still appear and open it when clicked.
- In the lobby and while choosing a new map mid-match, verify that incoming messages update the history while the chat field stays focused.

## OpenZ-VK 0.2.39 Unit Editor testing

- Enable projectile avoidance and compare tanks that stand still, partly escape blasts, and completely escape blasts. Check the per-unit label with kill and death counts on and off. Use Reset Avoidance on its own, then use Reset Counters and change an active combat parameter with automatic reset enabled.
- Enable VK Infinite Combat Testing Mode over open ground and near the edge of the field. Check both sides' immortality and circular damage, floating damage, kill and death counts, automatic reset and respawning. Confirm the markers are 19 metres apart and the duel restarts from them. Disable the mode and check that the prior settings and markers return.
- In solo and on both 1 vs 1 seats, open Options, then Build Keys from the pause menu. Bind a letter, cancel with Escape and clear with Backspace. Resume and try the changed binding at a factory. Confirm local bindings carry over to the main menu and the online match keeps running.
- In Changes & Updates, open both tabs using the mouse and Left/Right. Check the OpenZ comparison cards, the OpenZ-VK introduction and credit, the Discord link and the version notes from the most recent entry through the oldest fork entry. Verify scrolling and that the changelog is available in an offline compiled game.

## OpenZ-VK 0.2.38 Easy Tuning

- In the Unit Editor, open Easy Tuning under OpenZ, change one agility field, and load Z Classic from the profile menu while staying on the same page. Confirm Jeep 115%, Light 100%, APC 85%, Medium Tank 70%, Mech 60%, Heavy 50%, Missile Launcher 30% and Crane 45%. Load OpenZ again and confirm every field returns to 100%.
- Import a profile with a different agility value while on Easy Tuning, then reset its overrides. Confirm the displayed values follow the active profile after both actions.

## OpenZ-VK 0.2.37 radar interference

- In a 1 vs 1 with Radar Loss set to Fog of War, check the boundary between controlled and neutral territory. Check that signal interference is brief and stays within obscured ground.
- Watch hidden robots move beyond their last known positions. Advance an allied scout and verify that the old marks disappear when inspected and the robots appear again when they come into range.

## OpenZ-VK 0.2.35 combat and duel controls

- Verify both fort orientations and the closest road or bridge with four different turrets.
- In two connected compiled games, check both request roles, map-gallery navigation and chat, accept, decline, expiry, frozen lobby rules, and lobby return without mission results. Interrupt the route, verify the countdown on both sides, and verify recovery, abandonment, and timeout.
- Host a duel, change its map by agreement, then create a new lobby and verify that the last played map and all game rules are restored.
- In solo and 1 vs 1, open different factories and check eligibility, custom or cleared build keys, switching production and queueing the current unit.
- In the Unit Editor, check the Z Classic settings, variance fields, per-vehicle reset and apply, and saved custom profiles from the lobby.
- Inspect stationary smoke, trails deposited below red health, and the trail fade. On both sides, check radar static, last-seen unit and structure states, minimap markers, radar repair and camera recovery.
- Online TURN allowance and internet reconnection require the corresponding invitation service revision and a live relay account.

## OpenZ-VK 0.2.33 AI Maps

- The 6,601 simulation checks cover the full catalog, including both lava option states, water and ravine access, original flag and building coordinates, bridge damage and repair, rock clearance, and crocodile placement in native water.
- The Windows release-format build and executable checks are local while the source is on `dev`. The public release on `main` awaits the user's approval.

## OpenZ-VK 0.2.32 catalog terrain upgrade (awaiting compiled-game validation)

- All 65 existing scenes passed the catalog authoring pass. The converter verified the layout and recorded source hash for each of the 60 historical entries. The 5,555 simulation checks passed.
- In the compiled game, verify the V3 gallery entries, the walk decks on intact bridges, bridge repair and destruction, grenade crates, and the existing solo and 1v1 map selection rules.

## OpenZ-VK 0.2.31 V3 terrain palettes (awaiting compiled-game validation)

- The 5,555 simulation checks pass. The converter produced byte-identical V2 and V3 grids for Desert, Pyro Technics, Molten Kombat, Jungle and Car Park.
- Unity Play Mode rendered both modes for all five missions at matching camera positions. The terrain collider vertices, loaded object counts and lava tile counts matched, and every intact bridge had a walk deck. The solo gallery dispatched both Jungle edition callbacks and showed a single deploy button for Chilly Willy.
- In the compiled game, deploy a trial in V3, restart it, return to the gallery and deploy it in V2. Verify the selected terrain through both flows at normal RTS zoom. Check Chilly Willy, the V1 cards and the official OpenZ missions from their existing menus.

## OpenZ-VK 0.2.30 Chilly Willy V2 (awaiting compiled-game validation)

- The 5,555 simulation checks pass. They cover the vehicle route closing and reopening, robot access to meltwater, and removal of the blocking tile after a rock is destroyed.
- Unity Play Mode rendered the map from three camera angles and verified the west bridge's ruined model and the repaired east bridge's intact model and walk deck.
- Deploy Chilly Willy from AI Maps → OpenZ-VK → Arctic and check the snow, cliff edges and water at RTS zoom levels.
- Cross both intact bridges with vehicles. Destroy the western crossing, repair the eastern crossing with the neutral crane after capturing it, and confirm the route changes match the visible bridge stages.
- Wade with robots away from the bridges, clear a breakable slate ridge and collect a grenade crate. Check the base approaches and flags in a full solo match.

## OpenZ-VK 0.2.29 Jungle, Car Park and Desert V2 (awaiting compiled-game validation)

- Native tile, class, territory and object records match the decoded Zod files for all three maps. The 5,546 simulation checks pass, including the starting bridge states, route closure after destruction and robot access to water.
- All three maps ran in Unity Play Mode and were recorded from three camera angles each. All five intact bridge views registered a walk deck. The capture camera used reduced post-processing so the terrain detail stays visible.
- Open all three V2 maps in the solo AI Maps gallery and check their textures and silhouettes from the RTS camera. Verify that the original OpenZ missions and existing converted maps remain available.
- In Jungle and Desert, send robots into water and vehicles along the roads and bridge decks. Destroy and repair a bridge and a rock wall. Check that the opened or closed route matches the picture.
- In Car Park, verify that both canal bridges start ruined. Repair them and cross with units, then destroy one and confirm that the route closes. Compare the native starting units, flags and factory placement against the source.
- In Desert, collect and destroy a grenade crate. In Jungle, check the rock slices and scenery for gaps or clipping as the camera rotates.

## OpenZ-VK 0.2.28 volcanic V2 maps (awaiting compiled-game validation)

- Deploy both V2 maps from the solo AI Maps gallery and confirm that the ordinary Volcano mission and the V1 Molten Kombat card still open their own scenes.
- Check the terrain and road edges from the ordinary RTS camera and at low zoom. Verify the starting armies, flags and grenade crate pickups and destruction, including the crate by the bridge on Molten Kombat.
- Cross both Molten Kombat bridges, destroy and repair them, and confirm their crossing routes close and reopen. Destroy rocks in a wall and confirm the newly cleared tiles match the visible gap.
- Try lava orders with the optional OpenZ lava rules on and off. Check that the lava is distinct from solid walls and ravines, and that neither the terrain nor the water or lava views change native path classes.
- The two recordings cover the game renderer in Unity Play Mode. They cover several camera angles with the maps running. Compiled-game menus and full matches are pending.

## OpenZ-VK 0.2.27 lobby teams and state packing (awaiting compiled-game validation)

- On two PCs, have both players rename themselves and confirm both names update, then swap teams as host. Confirm readiness clears, the match opens on the assigned fort, each side controls its own army, surrender awards the opposite colour and returning to the lobby keeps the new assignments.
- On the same map and TURN path, compare binary events and state compression separately and together. Record sent and received game-state KiB/s, SYNC, frame times and queues. Toggle mid-match and verify movement, sounds and combat effects continue. Check both options start off after restart.
- State-data measurements include encoded game-state update payloads. Voice, commands and transport overhead are outside these counters.

## OpenZ-VK 0.2.26 live sync rate (awaiting compiled-game validation)

- Confirm that SYNC fits beside ping and the TURN indicator at supported HUD sizes. Compare the host's send rate and the guest's receive rate in the HUD with the diagnostic graph. Check that a receive stall lowers the guest rate and a restart clears the previous match's samples.

## OpenZ-VK 0.2.25 duel tuning (awaiting compiled-game validation)

- Source checks pass 5,486 core and real-map checks, including the motion
  buffer regression cases. The native WebRTC harness transfers a fragmented
  diagnostic report over a direct local connection.
- Check the hidden upper-left Ctrl+Alt+Up gesture in windowed and fullscreen
  modes, outside the corner, during chat, and after closing with Escape.
  Exercise all controls while the duel continues, then confirm defaults on
  restart. Inspect graph layout and timers at supported resolutions.
- Compare the same robot and vehicle paths on direct and TURN connections,
  with guest interpolation off and at 100 ms. Include turns, stops, unit
  spawns, deaths and long receive gaps. Check that the host view and orders
  behave consistently, then compare the new measurements from both PCs.
- Send a report from each side and save it on the other before leaving the
  match. Confirm that it opens as plain UTF-8 and contains that match's timing
  data. Verify that saving and transferring reports under load preserves play.

## OpenZ-VK 0.2.24 duel setup and production (awaiting compiled-game validation)

- On both duel seats, confirm that the battlefield opens without a briefing. Check the default start, then set opening orders to 10 seconds. Issue and replace move orders on both seats. Confirm that units stand still and the match clock stays at zero until the countdown ends. Repeat after a restart.
- In the lobby, browse all four OpenZ flybys and the separate AI Maps gallery. Apply a map as host and propose one as guest. Confirm the lobby only changes after host acceptance.
- Save a named formation with an empty slot, restart the game and load it. Check the default formation and verify that loading a saved formation still respects Allowed units.
- Bind the production shortcut to a key, then to Hold L-click. Try both on a factory with a partially produced unit and a queue. Confirm the chosen unit restarts from zero, the queue clears and a regular click keeps its usual behavior. Repeat as the duel guest.

## OpenZ-VK 0.2.23 duel diagnostics (awaiting compiled-game validation)

- Pending on two PCs: play the same map over a direct connection and over
  TURN. Collect both logs and align the host and guest windows. Confirm
  that send and arrival gaps, game-thread queue wait, processing costs and
  engine timings help explain any visible stalls.
- Confirm that a short duel produces a partial window on disconnect, restart
  and return to the lobby, and that the next match starts with fresh counters.
- Check that both HUDs show TURN alongside the in-match ping on a relayed
  connection, with enough width for a three-digit ping at supported UI sizes.
  Check the direct connection and TCP HUDs as well.
- Session summaries include build, timing and volume counters. They do not
  contain chat, player names, peer addresses, invitation links or credentials.

## Current local review adjustments (validated)

- Player changelogs now describe features, gameplay changes and visible fixes.
  Development details remain in commits and technical records. Upstream history
  is preserved unchanged.
- The supplied OpenZ-VK logo is the README's opening image.
- Solo and multiplayer galleries use AI Maps with the aligned poop pictogram.
- Solo mission selection has Allowed units with the shared OpenZ/Z 1996/Custom
  catalogue. Its saved selection is separate from multiplayer, preserves campaign
  difficulty/production bonuses and applies to starting units and production for
  both sides. Preferences schema 4 adds one optional key without resetting settings.
- Completed: 5,440 simulation checks, including the solo roster on four real maps;
  Unity runtime source compilation, updated native menu/solo captures, verified
  player ZIPs and installer installation/repair. Previous evidence is historical.

Current player: clean source f3b889745daaab362c066d444264eab7354a62a7,
built 2026-11-17 17:07:01 UTC, resource package openz-1.4-vk-1.
The final menu proof passes all 22 tile bounds, solo OpenZ/Z 1996/Custom selection,
saved choices and restoration, lobby OpenZ/Z 1996 controls, and both AI Maps
gallery titles/icons. Native captures are from menus-20261117-180956.
The final solo mission also passes on source f3b88974 (solo-units-final-20261117-182113). The Unit Editor and host/guest L02 pass on source 450350cd;
the final change only adjusts picker layout and its rendering check. The solo
mission verifies both teams' production and starting-unit restrictions with an
active CPU and no multiplayer rules. Simulation checks: 5,440 passed.

## Earlier OpenZ-VK 0.2.22 candidate (17 Nov 2026)

Release player: `builds/OpenZ-VK-0.2.22-Windows-x64/openz-vk.exe`.
Unity stamped source `a4d87dad96076e746b6e8b9a71b47041a52c4c30`, clean,
at `2026-11-17 13:21:25 UTC`, resource package `openz-1.4-vk-1`.
Development builds, script debugging and profiler connections are disabled.
Publication remains pending final review. Historical entries below retain the
validation state of their original source revisions; these checks supersede
their corresponding pending Windows player checks.

Completed on this release player:

- Actual startup mismatch: an incompatible required resource ID blocks play with
  repair instructions. Restore FORK_BUILD.json byte-for-byte afterwards. Normal
  launches confirm the matching package; menu checks also reject missing and
  mixed embedded identities.
- Native main menu and solo selection; ON/OFF option toggle geometry and actual
  screen captures; fort placement caption and unit picker; army/handicap controls;
  microphone devices; solo/duel map galleries, terrain filters and sorting.
  Inline AI Map icons are vertically aligned with their labels.
- Unit Editor through the menu and the exact `Unit Editor/Open Unit Editor.bat`
  launcher; all twelve Dynamics pages, help, tests, counters, import/export,
  independent tank/robot selections and returning to the main menu.
- Clean factory-profile screenshots contain OpenZ and Z Classic only. A separate
  saved example profile appears in the native selector. Tests restore existing
  local profiles, selections and drafts afterwards; no personal profiles are
  distributed.
- Native host/guest matches on L02, L07, L16, L17, SM09 (Arctic), SM19F (City)
  and SM05 (Volcanic). Both peers pass grouped proposal acceptance, independent
  fort cannons/profiles, commands, combat damage, surrender/end state, chat and
  synthetic voice. Both peers report zero exceptions. In-match ON/OFF toggle
  screenshots and flag/border readability checks pass on the same player.

Additional completed checks:

- 5,416 simulation, real-map and loopback transport checks, including Classic
  x5 boost and 6.25 m cumulative distance limits across replans.
- 44 installer checks: byte-range transport, verified content reuse, installation,
  repair/downgrade fixtures, cancellation/failure protection, unsafe paths,
  resource epoch migration, modified-resource backups, unknown-file preservation,
  historical unpinned resources and wrong-package rejection.
- Real Git clone/fetch and repeated exact-revision checkout without pushing.
- Real Git-free source import and Unity release compilation, followed by an
  updated source revision and independently pinned resource package. After the
  second build, resource preparation confirms no download is needed. The clean
  source-build stamp identifies `2913ff6e`; this separately tests the installer
  compilation mechanism, rather than replacing the reviewed player above.
- Actual source wizard captures: tools, main/dev history, matching resources and
  build result. Installed Git/Unity setup buttons are disabled.
- Native WebRTC harness: handshake, a fragmented 1 MiB snapshot, reverse commands
  and voice, incompatible-build and oversize rejection.

Remaining manual coverage: two physical PCs over a real remote network,
NAT/TURN combinations and slow links, real microphone/headset quality, other
hardware/display configurations, and exhaustive playthroughs of every converted
map. The experimental standalone map editor is excluded from this release.

Local proof logs and original screenshots are retained under
`tests/Multiplayer/.cli/` and `.local/release-0.2.22/`.

## Multiplayer field capture cache (0.2.20, source only)

- Compare values before serializing object fields and reuse immutable field
  records until their contents change. Check nested lists/arrays, all waypoint
  flags, source entries and referenced IDs; preserve exact floating-point bits.
- Keep health, destruction, repair, production, ownership, obstacle fields and
  live/graveyard descriptors current. World/auxiliary state and unit motion
  records retain the existing serialization path, including simulation debris.
  Local Unity debris physics/rendering is unchanged.
- Expire histories for removed objects, new worlds and failed captures. Reset
  the capture buffer when returning to the lobby, restarting or closing.
- Completed: 5,410 core, real-map and loopback transport checks on the normal
  path and the AOT reflection-check branch in the .NET harness. This does not
  establish native IL2CPP integration. Packet/state equality includes all 44
  converted duel choices, destruction, repair, projectile/debris state, surrender,
  full refreshes, in-place mutations, match replacement and failure recovery.
- Benchmark procedure, measurements and retained-memory tradeoff are documented
  in MULTIPLAYER_CAPTURE_CACHE.md. No FPS claim follows from the .NET benchmark.
- Completed: all Unity runtime scripts compile with the Unity compiler and
  references, with existing warnings only. Paired benchmarks compare every
  payload byte for byte and show 34-38% less host replication CPU time in motion.
- Pending user checks on a newly requested release player: both PCs, orders,
  combat, destruction and opened passages, building/bridge repairs, capture and
  production, wrecks/fragments, restart, lobby return, voice, slow links and
  direct/TURN/TCP; compare the existing performance log lines on both machines.
- This changes the replication hot path. A new release build is recommended
  when requested; no player build, launch or gameplay automation was performed.

## Dodge/boost budgets and directional circle (0.2.18, source only)

- FEEL exposes both maximum durations and both maximum travel distances.
  Distances count actual accepted movement across replans. Exhausting boost
  restores normal speed; exhausting dodge resumes the interrupted order.
- FEEL and DODGING show 16 clockwise directions: forward 0 degrees, right 90,
  back 180, left 270. Select a direction and edit its weight; percentages
  normalize across permitted directions. All-zero weights refuse dodging.
- Default weights group the 1,000 native Zod angle draws into the nearest
  16 sectors. Native Zod uses world angles, not heading-relative sectors.
  Editing weights enables relative circle mode; RESET ZOD restores the exact
  original continuous angle strategy and default direction permissions.
  Reset affects the selected scope and preserves other dynamics settings.
- Save the profile in the editor, then select it under lobby DYNAMICS. Host
  rules remain frozen during multiplayer matches; clients cannot edit them.
- Pending user validation on a requested release player: circle readability,
  clipping/scrolling/input at supported scales; select/edit/reset each direction;
  0-degree continuation and 45-degree advance; saved profile selection in the
  lobby; duration/distance endpoints and normal-speed continuation after boost.
- Completed: all Unity runtime scripts compile with existing warnings only;
  2,070 core, simulation and loopback transport checks pass, including 435
  focused behavior/movement checks and all 16 relative angles.
- No player build, gameplay automation or rendering test was requested.

## Multiplayer and map-gallery logos (0.2.17, source only)

- Fit the existing transparent Wordmarks/wm_openz_vk_menu sprite into a 300 x 100
  multiplayer header and 160 x 48 gallery header. Preserve aspect ratio, disable
  raycast interception, and retain the text fallback if the local asset is absent.
- Reuses the PNG and metadata in .local/OpenZ-VK-menu-logo-0.2.14.zip.
- Completed: all runtime C# scripts compile using the Unity compiler and references,
  with a separate output assembly to avoid another running source-check lock.
- Pending manual player validation: logo visibility and clean transparency on all
  multiplayer pages and solo/duel gallery tabs, with no subtitle/button overlap.
- No player build or runtime test was requested for this source change.

## Small editor and HUD logos (0.2.15, source only)

- Reuse Wordmarks/wm_openz_vk_menu in the Unit Editor top bar and in-game
  top-left badge, with aspect preservation, no input raycasts and a text fallback.
- Uses the existing local PNG and metadata backed up in
  .local/OpenZ-VK-menu-logo-0.2.14.zip; no additional image resource is required.
- Completed: runtime C# compilation with the committed simulation files substituted
  in a temporary compiler response file; ongoing simulation edits were untouched.
  The full working-tree check encountered an unrelated ZBehaviorProfile.cs syntax error.
- Pending manual player validation: legibility at small sizes, transparency,
  compact/normal editor top bar and in-game badge spacing at supported scales.
- No player build or gameplay test was requested or performed.

## Transparent main-menu logo (0.2.14, source only)

- Replace only the large main-menu wordmark with the supplied gold/pink artwork.
  Background extraction uses the built-in ImageGen tool; preserve the lettering,
  tank and character and replace the checkerboard with real alpha transparency.
- Local asset: source/Assets/OpenZ/Resources/OpenZ/Kit/Wordmarks/wm_openz_vk_menu.png
  and its Unity metadata. Separate backup: .local/OpenZ-VK-menu-logo-0.2.14.zip.
  Restore both files into that Wordmarks directory on another source checkout.
  The original OpenZ artwork remains unchanged; multimedia is excluded from Git.
- Completed: verified RGBA alpha at outer background and the O counter.
- Pending manual player validation: aspect ratio, clean edges, entrance animation,
  anniversary badge and all menu entries at supported resolutions. No player built.



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


## Complete p02 AI Maps catalog and blue borders (0.2.12, source only)

- Added the tracked aimaps/ Unity local package: 60 selected native-source
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

- Added a separate converted-map browser under PLAY: Load AI maps, with a
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

## Lobby proposals and independent fort mounts (0.2.21, source only)

- Guest edits stay local until SEND PROPOSAL. The host receives one replaceable
  grouped request with a difference list and ACCEPT / DECLINE. No chat message,
  sound or modal prompt is emitted for each changed field. Map suggestions are
  also available directly in the gallery; WITHDRAW removes the pending request.
- Official host changes expire pending proposals. Stale drafts require REFRESH
  SETTINGS, which starts again from current official values. Accepting the whole
  proposal clears both readiness confirmations. Decline/withdrawal do not.
- Fort placement has a dedicated top-view popup with four fixed real mounts per
  team. Red and blue cannons are independent. Legacy shared defaults still work.
  There is no free positioning outside the four simulation mounts.
- Completed: 4,354 core and real-map checks, including proposal authority,
  snapshot isolation, replacement/old-accept races, stale revisions, rejection,
  withdrawal, match freeze, legacy mounts, roster filtering and all eight actual
  team mount coordinates on L02/L07/L16/L17. Unity runtime compilation passed.
- Pending on the next requested release: host/client lobby layout and popup
  readability, map-gallery suggestions, profile proposals, replacement/cooldown,
  host edits while a guest is drafting, readiness races, disconnect/reconnect,
  actual JSON transport, tactical previews, correct team cannon models and mount
  positions on converted maps. Both PCs need the new lobby protocol.
- No player was built or launched. A new release build is recommended for these
  network/menu changes and the accumulated source updates; only the user can
  authorize that build, and its gameplay checks remain pending manual testing.
## OpenZ-VK 1.5.55 (source only)

- Pending manual lobby checks in online multiplayer, classic duel and local Skirmish: open the illustrated gallery from a turret, shared starting-unit and player advantage slot. Select an allowed unit, clear the slot, close with Escape and check that excluded units do not appear.
- Pending manual match checks: compare ground and fort-mounted howitzers with Wider Howitzer Spread off and on. Compare other cannons and the fort roof weapon across both settings.

# OpenZ-VK 1.5.35

- Pending manual Windows checks: verify unified combat presets and legacy custom imports in Solo, Unit Editor and multiplayer. Confirm the default, switch to Z Classic and select a saved custom profile.
- Pending manual lobby checks: verify mode and map filters, player dropdowns and rename, chat grouping and scrollbar, voice icons and tooltips, balancing examples and bot difficulty.
- Pending manual match checks: compare howitzer and fort-gun spread with the rule off and on, verify Friendly Fire in Teams and its absence in Free for All, and confirm Skirmish loading and menu overlay placement.
