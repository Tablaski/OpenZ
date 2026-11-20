# Multiplayer with up to eight participants

## Implementation order

1. Keep the existing two-human invitation and WebRTC path. Generalize room membership, seat identities, map capacity, readiness and the loading barrier before removing two-seat assumptions from transport code. The first playable matches need one or two humans plus AI seats. Use the existing invitation flow for the second human. A seat is the fort and its owners' color. An alliance is the group that shares victory. An observer retains a room identity independently of an active seat.
2. Carry native ownership through map decoding, simulation, rendering and minimap markers. Use the named terrain palettes and reuse OpenZ scene assets. Verify each imported map at its native size and count the forts, zones, terrain classes, structures and starting units. Check crossings, terrain height, pathing and both AI and human arrival at the forts before exposing a map in AI Maps. Run `node tools/Check-MultiplayerMaps.cjs "D:/Zod Engine"` before importing. The tool reports inconsistencies without rewriting the source maps.
3. Extend the existing lobby layout with the five approved tabs, a persistent list of participants, lobby chat and microphone controls. Show human, AI, open and closed places, the AI level, membership in an alliance and readiness. Keep global balance options and per-participant advantages separate. Starting armies use the shared setup plus any per-participant bonus. Offer an on-demand total. Keep the magic link, and give moderation to the room creator. Omit settings proposals in the new lobby.
4. Run each AI seat in the authoritative simulation. Define owner and alliance queries centrally and use them for targeting, splash damage, territory, production, repair and victory. Preserve the two-side behavior on legacy maps. In team matches, apply the existing military-lead calculation to alliance totals for progressive balancing. Keep bot difficulty separate from AI Help. Verify mixed and asymmetric teams and free-for-all before raising the permitted human count beyond two.
5. Add individual match lifecycle states. A surrender ends that player's orders and leaves their session connected as an observer. They can watch to the end or leave for the main menu. The creator may kick a human after confirmation, optionally with a message. An in-match kick blocks re-entry until the room returns to the lobby. A lobby ban blocks re-entry to the current room. A host voice mute is enforced by the session and visible to the affected client, who can still use text chat.
6. Run map votes on the match authority with a 30-second deadline. Keep the chat available. Count one explicit Go or Veto from each eligible human and publish the result to everyone. Use the first tally to reach half of the eligible humans in even-sized votes or a majority in odd-sized votes. At the deadline apply the map if no tally has resolved. Validate map compatibility before opening a vote and retain seat identities through reload. Decide observer eligibility and how disconnects affect the electorate when wiring the host workflow.
7. Implement the HUD only after visual approval. Keep the existing two-side display for a two-participant game. Treat Units, Attacking Power and Both as a local presentation preference and preserve stored choices. For three or more participants, the mockup uses stable participant columns in free-for-all and alliance totals in team play. Check placement alongside network diagnostics at narrow and ultrawide resolutions.

## Existing systems to review

| Area | Required check |
| --- | --- |
| Match menu | Pause and resume ownership, restart, return to lobby, rematch and map vote have separate effects. A spectator's pause menu exposes observer actions and leaving. In-match moderation lives under the host's player list. |
| Input and camera | A surrendered or eliminated participant cannot issue orders, produce units or invoke per-player abilities. Test reconnect, free camera, player focus, fog visibility and spectator access to chat. |
| Battlefield UI | The minimap, flag and unit colors, radar warnings, factory notifications and selection ownership need more than two side identities. Preserve the difference between a participant color and an alliance indicator. |
| Match results | Victory, defeat, statistics, fort destruction and end-of-match readiness need per-participant states and an alliance result. Check matches that continue after the first fort falls. |
| Simulation | Audit unit caps, opposing-owner shortcuts, AI targets, territorial production, fort underdog reference, splash damage and radar sharing. Establish the reference opponent in free-for-all before enabling the underdog modifier there. |
| Transport and voice | Reconnect by participant identity, sequence authoritative commands, validate input permission, handle a slow client without blocking all others and keep host role separate from the simulation authority. Keep private kick messages distinct from public status. |

## Map inventory on the available source

- All ten P03 files have three forts with native owners 1, 2 and 3. The multiplayer decoder assigns neutral ownership to the P03M06 repair station at (15, 16) and vehicle factory at (27, 19), and to the P03M07 flag at (63, 115). These three records use native owner 7 without a corresponding fort. Their positions and the native map files are preserved.
- P04 has eleven files, including Desert Fortress.
- P06 is deferred. The sole local file with that prefix, `p06_Merged_Map.map`, has eight distinct owned forts and is an eight-start layout.
- P08 has two files, including Hunters.

The 23 selected native files, grids, scenes and previews are packaged. Multiplayer AI Maps presents 2, 3, 4 and 8 starting-position filters. The duel accepts two-start maps. Local and online Skirmish expose the converted three-, four- and eight-start maps. Run `node tools/Convert-AiSkirmish.cjs --check` to verify the grids and catalogue against the packaged maps.

## Active seats and Ghost

The host may launch only when every map start has exactly one human or bot, with at least one active human. Extra humans must be assigned as observers or leave the room. Reduce the roster or choose a larger map for an over-capacity setup. Fill an empty start with a bot or choose a smaller map. Observers never consume a map start and cannot issue game orders, surrender, request a map change or vote on it.

The room state implements host-owned map and seat edits, independent bot difficulty, alliances, observer assignments, revision-bound human readiness and a locked setup after launch. A smaller map leaves humans outside its capacity unassigned and clears out-of-range bots. The loading barrier waits for every active human, with observers and bots excluded. Connection identities must come from the authenticated transport when the room is wired to invitations, lobby controls and match loading.

The online Skirmish path uses the existing invitation, WebRTC peer, microphone and chat. The two humans occupy Red and Blue, and the host can set each remaining position to open, Easy or Normal. Both humans must be ready and every start must be filled. The links exchange room snapshots, load acknowledgments, authenticated orders, battlefield deltas and events. Only the host runs the simulation. The map vote runs on the host, accepts ballots from surviving humans and reloads the accepted map for both participants. This path supports free-for-all with two humans. The expanded lobby, alliances, assigned ghosts, moderation and transport for more human clients remain pending.

Ghost is an optional spectator mode. A ghost can switch between surviving human players, follow their camera, zoom and cursor, or move independently. Provide a button and keyboard shortcut to toggle following, recenter on the selected player and switch between their fog view and full visibility. AI seats are excluded from the follow list. If no human remains, use free camera while the bots finish. Ghosts use their own text and voice channels during the match. Keep the world updates the observer already receives and relay only the extra view state for the selected human.

## Validation awaiting a compiled game

### Skirmish maps and online match flow

Run `node tools/Convert-AiSkirmish.cjs "D:/Zod Engine"` to regenerate the tracked native maps, grids and catalogue. In the project editor, run `OpenZ.EditorTools.OzAiMapBake.Skirmish` in batch mode. It writes the 23 package scenes and previews and enables them in Build Settings. Multiplayer offers **Local Skirmish with Bots**, and the online setup offers **Create Online Skirmish**.

The headless checks start every map with a bot at each non-human position. They also cover Easy and Normal settings, surrender with two survivors, a Green victory and an authenticated guest command and elimination event on P03M01. Unity imported and registered all 23 scenes. Manual validation still covers the live invitation and relay, online launch and return, synchronized map reload and veto, terrain rendering and navigation, the three Force Bar modes, free camera after surrender and the results screen.

Verify invitation entry, room capacity, AI skill setup, one-human free-for-all, two-human cooperation, mixed-alliance victory, surrender into observation, kicking and expiry of the re-entry block, vote notification and map reload. Verify every converted scene after its data, rendering, navigation and starting units are integrated. Run the existing duel and solo regression checks on the same compiled game. HUD approval precedes implementation and runtime validation.
