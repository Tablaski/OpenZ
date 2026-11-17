# Multiplayer field capture cache (OpenZ-VK 0.2.20)

This source update avoids serializing unchanged object fields before a network
snapshot. It preserves the existing field schema, packet ordering, host-owned
simulation and 15 Hz snapshot target. No preferences, game rules or assets change.

## What is checked and what is cached

Every snapshot still inspects current field values. A match-local value history
detects changes before invoking the existing serializer. Unchanged fields reuse
their previously published immutable byte arrays. Changed fields use the same
serializer and delta pipeline as before; a full refresh includes every record.

This applies to units, buildings, map objects and retained dead objects. Objects
are not assumed immutable just because they do not move. Health, ownership,
production, destruction, repair, obstacle tiles and all other schema fields
remain checked. The object descriptor also checks live/graveyard membership.

Snapshots retain values rather than mutable collection references. Lists and
arrays are checked element by element, including in-place edits with unchanged
length. Map-source entries include their nested arrays. Waypoints include every
wire field, including flags omitted by ZWaypoint.Equals. Object references are
compared by their transmitted ID. Float and double comparisons preserve exact
bits, including signed zero and NaN payloads. Future supported schema fields
automatically receive a tracker.

World time, zone ownership and auxiliary state (including missiles and
simulation debris) still pass through the original serialization path every
snapshot. Current unit path/motion records do too. Local Unity fragment physics,
collisions, rendering, smoke and fragment disappearance remain independent of
this network cache. The simulation continues ticking normally.

Mono compiles typed value readers once, alongside the existing scalar writers.
The AOT branch uses equivalent recursive reflection checks without expression
compilation. The .NET harness exercises both branches; it does not establish
native IL2CPP performance or integration.

Histories disappear when objects leave both the live list and the retained
graveyard. Changing authority worlds resets all histories, even if IDs repeat.
Failed captures discard partially updated histories. Restart, session close
and return to the lobby discard the capture buffer. The optimization trades
retained value-history memory for less serialization work and fewer temporary
allocations. It does not reduce the already delta-compressed network payload.

## Validation

The regression suite compares cached and uncached captures and encoded packets,
including periodic full refreshes. Coverage includes every supported ZObject
field and writable property, nested source/waypoint fields, in-place lists,
arrays and reference IDs, null/empty transitions, signed zero/NaNs, descriptor
changes, destruction/repair, graveyard eviction, match replacement and capture
failure recovery. All 44 converted duel choices also run capture/packet/receiver
equivalence through movement, unit destruction, building repair, simulation
debris, projectiles, a full refresh and surrender.

Player validation remains pending: both PCs, orders and combat, map obstacle
destruction and passages, repaired buildings/bridges, ownership and production,
wreck/debris presentation, restart, return to lobby, voice and direct/TURN/TCP.
A fresh release player would be useful for this broad hot-path change, when
requested. No player has been built or launched for this work.

## Reproducing the measurements

Paired measurements on 17 November 2026 use the same authoritative world for both
modes and alternate their measurement order on every frame. Each workload runs
120 frames with seven simulation ticks between frames, discards 20 warmup frames
and measures 100 samples per mode. The motion workload changes every unit's X
position, center and direction. All paired payloads are compared byte for byte.

Median milliseconds per host update for that motion workload:

| Map | Cache off | Cache on | Reduction | Managed KiB/update off / on |
| --- | ---: | ---: | ---: | ---: |
| L02 | 2.060 | 1.355 | 34% | 86 / 41 |
| L07 | 3.466 | 2.147 | 38% | 201 / 38 |
| L16 | 3.160 | 2.091 | 34% | 126 / 43 |
| L17 | 3.175 | 2.080 | 34% | 117 / 44 |

Idle host medians fell 33-38% in this run; warmed-up object fields were almost
entirely reused. Motion reuses over 99% of object-field records, serializing
about 102-143 changed fields per update rather than 21,281-31,757. The world,
auxiliary and motion records remain outside those field counts. Motion payloads
remain identical at 1512 / 1379 / 1722 / 1856 bytes on average. Guest median
costs are essentially unchanged, as expected for a sender-side optimization.
Combined per-update managed allocations fell about 52-81% in motion and 88-96%
in idle workloads.

Separate retained-heap readings estimate an additional 1.4-2.5 MiB of value
history on these four maps, released with the capture buffer. Larger maps and
longer lists can require more. A previous paired run gave host motion reductions
of 33-41%; absolute timings vary with runtime and machine load. These are .NET
measurements, not Unity player performance guarantees.

Use the same harness and sources for both modes. Disable .NET tiered compilation
for this comparison to reduce warmup-related differences.
Simulation ticks are excluded from timing; Unity rendering, native transport,
JSON event envelopes and voice are also excluded. These results concern CPU time
per replication update, not game FPS. The original 0.2.9 benchmark methodology
and its limits are documented in MULTIPLAYER_PERFORMANCE.md.

```powershell
$env:OZ_DUEL_BENCH = '1'
$env:DOTNET_TieredCompilation = '0'
$env:OZ_DUEL_FIELD_CACHE = 'compare'
dotnet run --project tests/Multiplayer/Multiplayer.Checks.csproj -c Release
# Optional separate runs, including approximate retained-memory readings:
$env:OZ_DUEL_FIELD_CACHE = '0'
dotnet run --project tests/Multiplayer/Multiplayer.Checks.csproj -c Release
$env:OZ_DUEL_FIELD_CACHE = '1'
dotnet run --project tests/Multiplayer/Multiplayer.Checks.csproj -c Release
Remove-Item Env:OZ_DUEL_BENCH, Env:OZ_DUEL_FIELD_CACHE, Env:DOTNET_TieredCompilation
```

The harness prints average and median host/guest time, combined managed
allocations, payload sizes, fields serialized/reused and approximate retained
capture memory. Cold initialization is outside timed samples. Retained-memory
figures include reusable records and can include first-use runtime initialization;
they are approximate managed-heap readings, not player memory measurements.
