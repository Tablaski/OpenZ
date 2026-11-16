using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class ReplicationPerformance
{
    public static void Run()
    {
        string root = Environment.GetEnvironmentVariable("OZ_DUEL_RESOURCES") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../source/Assets/OpenZ/Resources/OpenZ"));
        foreach (bool moving in new[] { false, true })
        foreach (string map in new[] { "L02", "L07", "L16", "L17" })
        {
            var host = ZWorld.Create(File.ReadAllText(Path.Combine(root, "default_settings.txt")), File.ReadAllText(Path.Combine(root, "Maps", map + "_grid.json")));
            host.ConfigureDuel(new DuelRules { Map = map }, false);
            var guest = ZWorld.Create(File.ReadAllText(Path.Combine(root, "default_settings.txt")), File.ReadAllText(Path.Combine(root, "Maps", map + "_grid.json")));
            guest.ConfigureDuel(new DuelRules { Map = map }, true);
            var sender = new DuelDeltaSender(); var receiver = new DuelDeltaReceiver();
#if !BASELINE
            var capture = new DuelReplica.CaptureBuffer();
#endif
            var hostSamples = new List<double>(); var guestSamples = new List<double>();
            long hostTime = 0, guestTime = 0, allocated = 0, bytes = 0;
            for (int frame = 0; frame < 100; frame++)
            {
                for (int tick = 0; tick < 7; tick++) host.Tick(.01);
                if (moving) foreach (var obj in host.Objects) if (obj.IsUnit) { obj.LocX += 1; obj.CenterX += 1; obj.Dx = frame % 2; }
                long allocationStart = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
#if BASELINE
                var delta = sender.Capture(DuelReplica.Capture(host));
#else
                var delta = sender.CaptureImmutable(capture.Capture(host));
#endif
                var payload = DuelDeltaCodec.Encode(delta);
                long middle = Stopwatch.GetTimestamp();
                var decoded = DuelDeltaCodec.Decode(payload);
#if BASELINE
                if (!receiver.Apply(decoded)) throw new Exception("Benchmark baseline rejected");
#else
                if (!receiver.ApplyOwned(decoded)) throw new Exception("Benchmark baseline rejected");
#endif
                DuelReplica.ApplyDelta(guest, decoded);
                long end = Stopwatch.GetTimestamp();
                if (frame < 10) continue;
                hostSamples.Add((middle - start) * 1000.0 / Stopwatch.Frequency);
                guestSamples.Add((end - middle) * 1000.0 / Stopwatch.Frequency);
                hostTime += middle - start; guestTime += end - middle;
                allocated += GC.GetAllocatedBytesForCurrentThread() - allocationStart; bytes += payload.Length;
            }
            hostSamples.Sort(); guestSamples.Sort();
            Console.WriteLine($"BENCH {map} {(moving ? "motion" : "idle")}: host {hostTime * 1000.0 / Stopwatch.Frequency / 90:F3} ms, guest {guestTime * 1000.0 / Stopwatch.Frequency / 90:F3} ms, hostMedian {hostSamples[45]:F3} ms, guestMedian {guestSamples[45]:F3} ms, managed {allocated / 90 / 1024} KiB/update, payload {bytes / 90} bytes (.NET harness, not Unity FPS)");
        }
    }
}
