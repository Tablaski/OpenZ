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
#if !BASELINE
        if (Environment.GetEnvironmentVariable("OZ_DUEL_FIELD_CACHE") == "compare") { Paired(); return; }
#endif
        bool cacheFields = Environment.GetEnvironmentVariable("OZ_DUEL_FIELD_CACHE") != "0";
        Console.WriteLine("BENCH field cache: " + (cacheFields ? "on" : "off") + " (same serializer and protocol)");
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
            var capture = new DuelReplica.CaptureBuffer(cacheFields);
            long retainedBefore = GC.GetTotalMemory(true);
            capture.Capture(host);
            long retainedCapture = GC.GetTotalMemory(true) - retainedBefore;
            Console.WriteLine($"BENCH {map} capture retained: {retainedCapture / 1024} KiB (approximate, includes reusable records and value history)");
#endif
            var hostSamples = new List<double>(); var guestSamples = new List<double>();
            long hostTime = 0, guestTime = 0, allocated = 0, bytes = 0, serialized = 0, reused = 0;
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
#if !BASELINE
                serialized += capture.SerializedFieldsLastCapture; reused += capture.ReusedFieldsLastCapture;
#endif
            }
            hostSamples.Sort(); guestSamples.Sort();
            Console.WriteLine($"BENCH {map} {(moving ? "motion" : "idle")}: host {hostTime * 1000.0 / Stopwatch.Frequency / 90:F3} ms, guest {guestTime * 1000.0 / Stopwatch.Frequency / 90:F3} ms, hostMedian {hostSamples[45]:F3} ms, guestMedian {guestSamples[45]:F3} ms, managed {allocated / 90 / 1024} KiB/update, payload {bytes / 90} bytes (.NET harness, not Unity FPS)");
            Console.WriteLine($"BENCH fields/update: serialized {serialized / 90}, reused {reused / 90}");
        }
    }
#if !BASELINE
    // Alternate measurement order on each frame so machine load and CPU frequency
    // changes are shared by both modes. Neither run advances the simulation.
    static void Paired()
    {
        string root = Environment.GetEnvironmentVariable("OZ_DUEL_RESOURCES") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../source/Assets/OpenZ/Resources/OpenZ"));
        string settings = File.ReadAllText(Path.Combine(root, "default_settings.txt"));
        foreach (bool moving in new[] { false, true })
        foreach (string map in new[] { "L02", "L07", "L16", "L17" })
        {
            string json = File.ReadAllText(Path.Combine(root, "Maps", map + "_grid.json"));
            var host = ZWorld.Create(settings, json); host.ConfigureDuel(new DuelRules { Map = map }, false);
            var captures = new[] { new DuelReplica.CaptureBuffer(false), new DuelReplica.CaptureBuffer() };
            var senders = new[] { new DuelDeltaSender(), new DuelDeltaSender() };
            var receivers = new[] { new DuelDeltaReceiver(), new DuelDeltaReceiver() };
            var guests = new[] { ZWorld.Create(settings, json), ZWorld.Create(settings, json) };
            foreach (var guest in guests) guest.ConfigureDuel(new DuelRules { Map = map }, true);
            var hostSamples = new[] { new List<double>(), new List<double>() };
            var guestSamples = new[] { new List<double>(), new List<double>() };
            var allocations = new long[2]; var sizes = new long[2];
            var serialized = new long[2]; var reused = new long[2];
            for (int frame = 0; frame < 120; frame++)
            {
                for (int tick = 0; tick < 7; tick++) host.Tick(.01);
                if (moving) foreach (var obj in host.Objects) if (obj.IsUnit) { obj.LocX++; obj.CenterX++; obj.Dx = frame % 2; }
                var payloads = new byte[2][];
                for (int order = 0; order < 2; order++)
                {
                    int mode = (frame + order) % 2;
                    long allocated = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                    var payload = DuelDeltaCodec.Encode(senders[mode].CaptureImmutable(captures[mode].Capture(host)));
                    payloads[mode] = payload;
                    long middle = Stopwatch.GetTimestamp();
                    var decoded = DuelDeltaCodec.Decode(payload);
                    if (!receivers[mode].ApplyOwned(decoded)) throw new Exception("Paired benchmark baseline rejected");
                    DuelReplica.ApplyDelta(guests[mode], decoded);
                    long end = Stopwatch.GetTimestamp();
                    if (frame < 20) continue;
                    hostSamples[mode].Add((middle - start) * 1000.0 / Stopwatch.Frequency);
                    guestSamples[mode].Add((end - middle) * 1000.0 / Stopwatch.Frequency);
                    allocations[mode] += GC.GetAllocatedBytesForCurrentThread() - allocated; sizes[mode] += payload.Length;
                    serialized[mode] += captures[mode].SerializedFieldsLastCapture; reused[mode] += captures[mode].ReusedFieldsLastCapture;
                }
                if (!payloads[0].AsSpan().SequenceEqual(payloads[1])) throw new Exception("Field cache changed paired benchmark packets");
                host.Events.Clear();
            }
            for (int mode = 0; mode < 2; mode++)
            {
                hostSamples[mode].Sort(); guestSamples[mode].Sort();
                Console.WriteLine($"PAIRED {map} {(moving ? "motion" : "idle")} cache={(mode == 0 ? "off" : "on")}: hostMedian {hostSamples[mode][50]:F3} ms, guestMedian {guestSamples[mode][50]:F3} ms, managed {allocations[mode] / 100 / 1024} KiB/update, payload {sizes[mode] / 100} bytes, serialized {serialized[mode] / 100}, reused {reused[mode] / 100}");
            }
        }
    }
#endif
}
