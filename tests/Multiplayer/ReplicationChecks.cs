using System;
using System.IO;
using System.Linq;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class ReplicationChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../source/Assets/OpenZ/Resources/OpenZ"));
        foreach (string map in new[] { "L02", "L07", "L16", "L17" })
        {
            string settings = File.ReadAllText(Path.Combine(root, "default_settings.txt"));
            string json = File.ReadAllText(Path.Combine(root, "Maps", map + "_grid.json"));
            var host = ZWorld.Create(settings, json); host.ConfigureDuel(new DuelRules { Map = map }, false);
            var guest = ZWorld.Create(settings, json); guest.ConfigureDuel(new DuelRules { Map = map }, true);
            var capture = new DuelReplica.CaptureBuffer();
            var fast = new DuelDeltaSender(); var defensive = new DuelDeltaSender(); var receiver = new DuelDeltaReceiver();
            var unit = host.Objects.First(o => o.IsUnit);
            byte[] retained = null, retainedCopy = null;
            for (int frame = 0; frame < 12; frame++)
            {
                unit.LocX++; unit.CenterX++; unit.Dx = frame % 2;
                unit.Waypoints.Clear(); if (frame % 2 == 0) unit.Waypoints.Add(ZWaypoint.Move(320, 320));
                host.Tick(.01);
                var state = capture.Capture(host);
                var canonical = DuelReplica.Capture(host);
                check(state.Count == canonical.Count && state.All(p => canonical[p.Key].SequenceEqual(p.Value)), map + ": cached capture changed the wire state");
                var optimized = fast.CaptureImmutable(state, frame == 7);
                var reference = defensive.Capture(canonical, frame == 7);
                byte[] payload = DuelDeltaCodec.Encode(optimized);
                check(payload.SequenceEqual(DuelDeltaCodec.Encode(reference)), map + ": optimized delta changed the protocol");
                if (frame == 0) { retained = optimized.Changed[0]; retainedCopy = (byte[])retained.Clone(); }
                check(retained.SequenceEqual(retainedCopy), map + ": capture overwrote a published record");
                var decoded = DuelDeltaCodec.Decode(payload);
                check(receiver.ApplyOwned(decoded), map + ": owned delta lost its baseline");
                DuelReplica.ApplyDelta(guest, decoded);
                var replicated = guest.Get(unit.Id);
                check(replicated.LocX == unit.LocX && replicated.Dx == unit.Dx && replicated.Waypoints.Count == unit.Waypoints.Count,
                    map + ": direct application lost motion or reused list state");
                check(receiver.Snapshot().All(p => state[p.Key].SequenceEqual(p.Value)), map + ": receiver state differs from authority");
            }
            host.DuelSurrender(0);
            for (int tick = 0; tick < 200; tick++) host.Tick(.01);
            var afterDeath = capture.Capture(host);
            var deathDelta = DuelDeltaCodec.Decode(DuelDeltaCodec.Encode(fast.CaptureImmutable(afterDeath)));
            check(receiver.ApplyOwned(deathDelta), map + ": death delta lost its baseline");
            DuelReplica.ApplyDelta(guest, deathDelta);
            var received = receiver.Snapshot();
            check(received.Count == afterDeath.Count && received.All(p => afterDeath[p.Key].SequenceEqual(p.Value)), map + ": cached capture lost deletion or graveyard state");
        }
        var invalid = new DuelDelta { Sequence = 1, Baseline = 0, Full = true }; invalid.Removed.Add(1);
        Reject(invalid, check);
        invalid = new DuelDelta { Sequence = 0 }; Reject(invalid, check);
        invalid = new DuelDelta { Sequence = 1 }; invalid.Changed.Add(-1, new byte[0]); Reject(invalid, check);
        invalid = new DuelDelta { Sequence = 1 }; invalid.Changed.Add(1, new byte[65537]); Reject(invalid, check);
        invalid = new DuelDelta { Sequence = 1 }; invalid.Removed.Add(1); invalid.Removed.Add(1); Reject(invalid, check);
        invalid = new DuelDelta { Sequence = 1 }; invalid.Removed.Add(1); invalid.Changed.Add(1, new byte[0]); Reject(invalid, check);
    }
    static void Reject(DuelDelta delta, Action<bool, string> check)
    {
        bool rejected = false;
        try { DuelDeltaCodec.Encode(delta); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "Optimized writer accepted invalid state");
    }
}
