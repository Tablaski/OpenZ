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

Online Skirmish uses the same invitation format for up to eight humans. New participants take open starts first and replace a bot if necessary. Closed starts do not take part. If the map is full, the host can open a start, assign a waiting player to a bot start or choose a larger map. All active humans must be ready and finish loading. Only the creator runs the simulation. The creator distributes authoritative room snapshots and battlefield deltas and authenticates orders by the originating connection. Teams and individually configured Easy and Normal bots can share the roster. Map voting uses the surviving humans and the authority reloads the accepted map for the room.

The five-tab lobby keeps the roster, invitation, chat and microphone available. Shared starting units and individual additions go alongside the map army. Individual build speed and army additions are configured by starting position. Optional balancing uses total military value by alliance and compares it with the strongest remaining opponent. The territory reduction applies to the owner's factories, and the fort uses the opposing alliance's territory to calculate the production pace it is catching up to.

The host can mute voice, kick with a private note and ban a network from the current room. A ban is room-scoped because magic links have no persistent user accounts. Skirmish protocol 2 requires an updated invitation Worker. The Worker still accepts protocol 1 rooms and the original duel flow. Assigned ghosts remain separate work.

Skirmish uses Cloudflare Realtime SFU when enabled and admitted by the invitation service. The host publishes one reliable state and control stream, while each guest publishes an upstream channel subscribed to by the host. Dependent battlefield deltas stay ordered. Voice uses a separate lossy channel, silence suppression and narrower frames on the SFU route. Direct fallback uses one existing WebRTC connection per guest with STUN. A route change suspends the simulation while peers reconnect, then sends a full state before resuming deltas. The legacy 1 vs 1 path retains its existing STUN/TURN policy. See [service setup](../services/duel-signaling/README.md) for rollout and budget behavior.

The Skirmish sender adjusts the snapshot rate from 6 to 20 Hz using send backlog, RTT growth, jitter, simulation time and the measured state payload multiplied by the number of SFU receivers. It uses compression, skips captures under backpressure and paces SFU state bursts. Guests adjust interpolation to the snapshot arrival rate and jitter. The room creator remains the simulation authority on both routes.

Ghost is an optional spectator mode. A ghost can switch between surviving human players, follow their camera, zoom and cursor, or move independently. Provide a button and keyboard shortcut to toggle following, recenter on the selected player and switch between their fog view and full visibility. AI seats are excluded from the follow list. If no human remains, use free camera while the bots finish. Ghosts use their own text and voice channels during the match. Keep the world updates the observer already receives and relay only the extra view state for the selected human.

## Validation awaiting a compiled game

### Skirmish maps and online match flow

Run `node tools/Convert-AiSkirmish.cjs "D:/Zod Engine"` to regenerate the tracked native maps, grids and catalogue. In the project editor, run `OpenZ.EditorTools.OzAiMapBake.Skirmish` in batch mode. It writes the 23 package scenes and previews and enables them in Build Settings. Multiplayer offers **Local Skirmish with Bots**, and the online setup offers **Create Online Skirmish**.

The headless checks start every map with a bot at each non-human position. They also cover Easy and Normal settings, surrender with two survivors, a Green victory and an authenticated guest command and elimination event on P03M01. Unity imported and registered all 23 scenes. Manual validation still covers the live invitation and relay, online launch and return, synchronized map reload and veto, terrain rendering and navigation, the three Force Bar modes, free camera after surrender and the results screen.

Verify invitation entry, room capacity, AI skill setup, one-human free-for-all, two-human cooperation, mixed-alliance victory, surrender into observation, kicking and expiry of the re-entry block, vote notification and map reload. Verify every converted scene after its data, rendering, navigation and starting units are integrated. Run the existing duel and solo regression checks on the same compiled game. HUD approval precedes implementation and runtime validation.
