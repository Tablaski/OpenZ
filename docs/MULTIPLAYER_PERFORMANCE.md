# Multiplayer performance work (OpenZ-VK 0.2.9)

This is a source update, not a player release. Host authority, the field-level
wire schema and the 15 Hz snapshot target are preserved. No settings or stored
preferences are changed.

## Measured replication cost

A deterministic .NET 8 harness compared the original replication sources at
91af9620 with this implementation, sequentially on this machine. Each map runs
100 updates, discards ten warmup samples, and advances seven simulation ticks
before each sample. The motion workload then changes every unit's X position,
center and direction. Simulation advancement is excluded from timings. This is
an artificial replication workload, not a combat replay or Unity FPS test.

Median milliseconds per update for the motion workload:

| Map | Host before | Host after | Guest before | Guest after | Managed KiB/update before / after |
| --- | ---: | ---: | ---: | ---: | ---: |
| L02 | 29.743 | 2.554 | 0.287 | 0.078 | 17327 / 86 |
| L07 | 51.467 | 10.176 | 0.223 | 0.104 | 29914 / 202 |
| L16 | 44.782 | 5.196 | 0.236 | 0.094 | 25216 / 126 |
| L17 | 39.929 | 4.812 | 0.335 | 0.096 | 22713 / 117 |

Host median cost fell about 80-91%, guest median cost 53-73%, and combined
managed allocations over 99% for this workload. Payload sizes stayed exactly
1506 / 1398 / 1720 / 1860 bytes respectively. Idle-state payload sizes also
stayed identical. These are single-run measurements, sensitive to runtime JIT,
GC and machine load; they are not an FPS guarantee. The test excludes Unity
rendering, transport threads, native WebRTC, JSON event envelopes and voice.

To run the current harness from PowerShell:

```powershell
$env:OZ_DUEL_BENCH = '1'
dotnet run --project tests/Multiplayer/Multiplayer.Checks.csproj -c Release
Remove-Item Env:OZ_DUEL_BENCH
```

For a baseline comparison, compile the same harness with BASELINE defined and
the earlier DuelReplica, DuelReplication and DuelDeltaCodec sources. Keep the
same remaining simulation sources and resource directory. OZ_DUEL_RESOURCES
can override the default local resource path. Compare sequential runs, not
concurrent processes. The harness prints means, medians, managed allocations
and payload sizes. Allocation measurements cover both sides on the calling
thread; a release Unity player uses different timing and GC implementations.

## Implementation

- A match-local CaptureBuffer reuses its stream, writer and dictionary storage.
  Byte-identical records reuse immutable arrays; changed records allocate once.
- Scalar field writers use typed compiled accessors on Mono to avoid repeated
  reflection and boxing. AOT builds retain a reflection fallback. Map-item
  field indices are precomputed instead of repeatedly testing the full schema.
- The internal sender and receiver transfer immutable/decoded array ownership.
  Defensive copying remains the default for public APIs and snapshots.
- Writer validation checks the same bounds, sequences, removals and maximum
  payload without decoding its own output. Output capacity is known up front.
- Client application visits changed field records directly. A reusable record
  stream/reader replaces per-field reader allocation; lists retain identity
  and capacity rather than allocating an intermediate list and copying it.
- Receive work yields after three milliseconds between complete packets.
  Existing ordered deltas are never discarded. A single large packet can still
  exceed this budget. New snapshots wait while send queues exceed 256 KiB;
  pending events remain ordered and existing queue/event safety limits remain.

## Diagnostics and validation

Normal release players automatically write [OpenZ Multiplayer Perf] every ten
seconds during a match. Lines include build identity, role, selected transport,
FPS, longest frame, combined capture/diff/encode/send or decode/apply costs,
update frequencies, payload throughput, queued bytes, ping, collection count
and object count. Queues are approximate cross-thread snapshots; TCP in-flight
writes and native protocol overhead are not included in all counters.

The existing per-minute [OpenZ Perf] line continues to supply GPU/main-thread
and heap measurements. Multiplayer rule timers now measure actual simulation
time and reset on the guest. No -ozperf flag or uncapped mode is needed. Logs
use the existing OpenZ/OpenZ Player.log and Player-prev.log rotation, so copy
both after a match before subsequent launches replace them. No names, chat,
credentials, SDP or peer addresses are added by these counters.

The regression suite covers protocol equality, immutable-record lifetimes,
full-state refresh, changing lists, motion, destruction/graveyard state,
malformed writer input, ownership and existing real-map/transport behavior.
All Unity runtime scripts compile using the Unity compiler and references.
See PLAYER_VALIDATION.md for checks that still require a requested release
build and the user's manual testing on both remote PCs. Slow links, end-game,
restart/return to lobby, voice, direct/TURN and actual FPS remain pending.

## Two-sided match diagnostics (OpenZ-VK 0.2.23)

Both PCs automatically log an `[OpenZ Multiplayer Perf]` summary every ten
seconds, with a partial window on disconnect, restart or return to the lobby.
Compare the same map and match time using `matchS` and `windowS`. The optional
UTC comparison depends on the clocks of both PCs. Each line identifies the
role, transport and build.

- Compare host `sendGapMaxMs` with guest `arrivalGapMaxMs`. The host value is
  measured at submission to the transport. The guest value is measured after
  the complete delta reaches the transport worker. It includes network and
  transport-worker scheduling effects.
- Compare guest `receiveGapMaxMs` and `receiveQueueWaitMaxMs` to see whether
  deltas were available before the game thread started processing them.
  `drainLimitFrames` counts receive loops that reached the time or packet
  budget. Queue peaks are sampled, and queue bytes exclude some in-flight
  transport data. Payload counts and sizes cover game-state deltas.
- Use mean capture, encoding, enqueue, decode and apply times together with
  the maximum send and receive cost of a single delta. Encoding includes
  delta construction and serialization of events. Application includes
  updating the delta baseline, replica and event list.
- Compare frame spikes with simulation, guest view synchronization, frame
  timing and GC collection counts in the same window. The engine reports its
  latest available main-thread, render, GPU and present-wait figures. Zero
  frame-timing samples indicates unavailable timings. Window maxima may
  refer to different frames.
- Ping is an application round trip, sampled roughly once a second. It can
  include queueing and scheduling at either endpoint. Turn relay measurements
  alone do not isolate relay delay or packet loss.

On each Windows PC, run this PowerShell command before opening the game again
to collect the relevant summaries for sharing:

```powershell
Select-String -Path "$env:USERPROFILE\AppData\LocalLow\OpenZ\OpenZ\Player*.log" -SimpleMatch '[OpenZ Multiplayer Perf]' |
    Select-Object -ExpandProperty Line
```
