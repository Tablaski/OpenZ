using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenZ.Multiplayer;

static class Program
{
    static int checks;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        checks++;
    }
    static void Reject(Action action, string name)
    {
        try { action(); } catch (ArgumentException) { checks++; return; }
        catch (InvalidOperationException) { checks++; return; }
        throw new Exception(name);
    }
    static async Task RejectFrame(byte[] data, string name)
    {
        try { await DuelWire.ReadAsync(new FragmentedStream(data), CancellationToken.None); }
        catch (InvalidDataException) { checks++; return; }
        catch (IOException) { checks++; return; }
        throw new Exception(name);
    }
    static async Task<int> Main()
    {
        try { await Run(); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    static async Task Run()
    {
        var lobby = new DuelLobby();
        lobby.Join(0, "Host"); lobby.Join(1, "Guest");
        int originalRevision = lobby.Revision;
        lobby.SetReady(1, originalRevision, true);
        var rules = lobby.Rules; rules.EnableHandicap(0); rules.Handicap.BuildDuration = .8;
        lobby.ChangeRules(0, rules);
        Check(!lobby.Ready(1), "Rule change cancels consent");
        Reject(() => lobby.SetReady(0, originalRevision, true), "Stale readiness accepted");
        Reject(() => lobby.ChangeRules(1, rules), "Guest changed host rules");
        rules.Handicap.BuildDuration = .1;
        Check(lobby.Rules.Handicap.BuildDuration == .8, "Caller mutated live rules");
        var external = lobby.Rules; external.Common.StartingUnits.Add("heavy");
        Check(lobby.Rules.Common.StartingUnits.Count == 0, "Rules leaked a mutable list");
        lobby.SetReady(0, lobby.Revision, true);
        Check(lobby.Phase == DuelPhase.Lobby, "One ready started loading");
        lobby.SetReady(1, lobby.Revision, true);
        Check(lobby.Phase == DuelPhase.Loading, "Both ready did not load");
        lobby.MarkLoaded(0, lobby.Revision, 10);
        Check(!lobby.Advance(100), "One loaded started simulation");
        lobby.MarkLoaded(1, lobby.Revision, 100);
        Check(!lobby.Advance(102.9), "Countdown skipped");
        lobby.MarkLoaded(1, lobby.Revision, 101);
        Check(lobby.StartAt == 103, "Duplicate load reset countdown");
        Check(lobby.Advance(103) && lobby.Phase == DuelPhase.Playing, "Loading barrier failed");
        Reject(() => lobby.ChangeRules(0, rules), "Rules changed during match");
        lobby.Disconnect("Guest left");
        Check(!lobby.Advance(200) && lobby.Phase == DuelPhase.Closed, "Closed session restarted");
        Reject(() => new DuelRules { TerritoryBonus = double.NaN }.Validate(), "NaN rule accepted");
        var invalid = new DuelRules(); invalid.Common.StartingUnits.Add("unknown");
        Reject(invalid.Validate, "Unknown unit accepted");
        foreach (string turret in new[] { "gun", "gatling", "howitzer", "missile_cannon" })
        {
            var placement = new DuelRules(); placement.Common.StartingUnits.Add(turret);
            Reject(placement.Validate, "Shared outside army accepted a turret: " + turret);
            placement.Common.StartingUnits.Clear(); placement.Handicap.StartingUnits.Add(turret);
            Reject(placement.Validate, "Handicap outside army accepted a turret: " + turret);
            placement.Handicap.StartingUnits.Clear(); placement.Common.FortTurrets[0] = turret;
            placement.Validate();
        }
        foreach (string mobile in new[] { "grunt", "psycho", "sniper", "tough", "pyro", "laser", "jeep", "light", "medium", "mtank", "heavy", "missile_launcher", "crane", "apc" })
        {
            var placement = new DuelRules(); placement.Common.StartingUnits.Add(mobile); placement.Handicap.StartingUnits.Add(mobile);
            placement.Validate(); placement.Common.FortTurrets[0] = mobile;
            Reject(placement.Validate, "Fort accepted a mobile unit: " + mobile);
        }
        Check(Math.Abs(new DuelRules().BuildSeconds(0, 100, 1, 1) - 50) < .001, "Territory calculation");
        var handicap = new DuelRules(); handicap.EnableHandicap(0); handicap.Handicap.BuildDuration = .8;
        Check(handicap.BuildSeconds(0, 100, 0, 1) == 80 && handicap.BuildSeconds(1, 100, 0, 1) == 100, "Asymmetric handicap");
        handicap.HandicapEnabled = false;
        Check(handicap.BuildSeconds(0, 100, 0, 1) == 100 && handicap.BuildSeconds(1, 100, 0, 1) == 100, "Disabled handicap still changes rules");
        handicap.Common.StartingUnits.Add("jeep"); handicap.Common.FortTurrets[0] = "gun";
        handicap.EnableHandicap(1); handicap.Handicap.StartingUnits.Add("heavy");
        Check(handicap.EffectiveFor(0).StartingUnits.Count == 1 && handicap.EffectiveFor(1).StartingUnits.Count == 2, "Handicap changed both armies");
        handicap.EffectiveFor(0).FortTurrets[0] = "";
        Check(handicap.Common.FortTurrets[0] == "gun", "Effective army leaked shared turret slots");
        handicap.HandicapEnabled = false;
        Check(handicap.EffectiveFor(1).StartingUnits.Count == 1, "Disabled handicap retained custom army");
        handicap.EnableHandicap(1);
        Check(handicap.Handicap.StartingUnits.Count == 0, "Handicap slots did not start empty");
        handicap.Common.StartingUnits.Add(""); handicap.Common.StartingUnits.Add("light");
        handicap.Handicap.StartingUnits.Add(""); handicap.Handicap.StartingUnits.Add("heavy");
        Check(handicap.EffectiveFor(0).StartingUnits.Count == 2 && handicap.EffectiveFor(1).StartingUnits.Count == 3,
            "Empty slots spawned units or shared units were not inherited");
        handicap.Common.FortTurrets[0] = "howitzer";
        Check(handicap.EffectiveFor(1).FortTurrets[0] == "howitzer", "Handicap replaced the shared fort");
        handicap.EnableHandicap(0);
        Check(handicap.Handicap.StartingUnits.Count == 2 && handicap.EffectiveFor(0).StartingUnits.Contains("heavy"),
            "Changing the beneficiary erased the additional slots");
        var tooMany = new DuelRules();
        tooMany.Common.StartingUnits.AddRange(new[] { "jeep", "jeep", "jeep", "jeep", "jeep" });
        Reject(tooMany.Validate, "More than four shared slots accepted");
        tooMany.Common.StartingUnits.Clear(); tooMany.Handicap.StartingUnits.AddRange(new[] { "jeep", "jeep", "jeep", "jeep", "jeep" });
        Reject(tooMany.Validate, "More than four additional slots accepted");

        var sender = new DuelDeltaSender(); var receiver = new DuelDeltaReceiver();
        var world = new Dictionary<int, byte[]> { [1] = new byte[] { 100 }, [2] = new byte[] { 50 } };
        Check(receiver.Apply(sender.Capture(world)), "Initial full frame");
        world[1][0] = 30; world.Remove(2); world[3] = new byte[] { 80 };
        var delta = sender.Capture(world);
        delta = DuelDeltaCodec.Decode(DuelDeltaCodec.Encode(delta));
        Check(delta.Changed.Count == 2 && delta.Removed.Count == 1, "Delta omitted creation/change/deletion");
        Check(receiver.Apply(delta) && receiver.Snapshot()[1][0] == 30 && !receiver.Snapshot().ContainsKey(2), "Replica state incorrect");
        Check(!receiver.Apply(delta), "Duplicate delta applied");
        Check(sender.Capture(world).Changed.Count == 0, "Stationary world generated changes");
        world[1][0] = 20; var skipped = sender.Capture(world);
        Check(!receiver.Apply(skipped), "Missing baseline accepted");
        Check(receiver.Apply(sender.Capture(world, true)), "Full frame did not recover lost baseline");
        var snapshot = receiver.Snapshot(); snapshot[1][0] = 0;
        Check(receiver.Snapshot()[1][0] == 20, "Replica state leaked mutable buffers");
        delta.Changed[1][0] = 0;
        Check(receiver.Snapshot()[1][0] == 20, "Replica kept packet buffer references");
        byte[] validDelta = DuelDeltaCodec.Encode(sender.Capture(world, true));
        for (int size = 0; size < validDelta.Length; size++)
        {
            var truncated = new byte[size]; Array.Copy(validDelta, truncated, size);
            bool rejected = false;
            try { DuelDeltaCodec.Decode(truncated); }
            catch (InvalidDataException) { rejected = true; }
            catch (EndOfStreamException) { rejected = true; }
            Check(rejected, "Truncated delta accepted at byte " + size);
        }
        var trailing = new byte[validDelta.Length + 1]; Array.Copy(validDelta, trailing, validDelta.Length);
        bool trailingRejected = false;
        try { DuelDeltaCodec.Decode(trailing); } catch (InvalidDataException) { trailingRejected = true; }
        Check(trailingRejected, "Trailing state bytes accepted");

        byte[] encoded;
        using (var stream = new MemoryStream())
        {
            await DuelWire.WriteAsync(stream, new DuelPacket(DuelMessage.Chat, new byte[] { 1, 2, 3 }), CancellationToken.None);
            encoded = stream.ToArray();
            await DuelWire.WriteAsync(stream, new DuelPacket(DuelMessage.Ping, new byte[0]), CancellationToken.None);
            using (var read = new FragmentedStream(stream.ToArray()))
            {
                var packet = await DuelWire.ReadAsync(read, CancellationToken.None);
                Check(packet.Kind == DuelMessage.Chat && packet.Payload.Length == 3, "Fragmented TCP frame failed");
                Check((await DuelWire.ReadAsync(read, CancellationToken.None)).Kind == DuelMessage.Ping, "Consecutive TCP frames failed");
            }
        }
        var corrupt = (byte[])encoded.Clone(); corrupt[0] = 0;
        await RejectFrame(corrupt, "Bad magic accepted");
        corrupt = (byte[])encoded.Clone(); corrupt[4] = 99;
        await RejectFrame(corrupt, "Bad version accepted");
        corrupt = (byte[])encoded.Clone(); corrupt[6] = 255;
        await RejectFrame(corrupt, "Unknown packet accepted");
        corrupt = (byte[])encoded.Clone(); for (int i = 8; i < 12; i++) corrupt[i] = 255;
        await RejectFrame(corrupt, "Negative allocation accepted");
        corrupt = (byte[])encoded.Clone(); corrupt[11] = 1;
        await RejectFrame(corrupt, "Unbounded allocation accepted");
        await RejectFrame(new byte[] { 1, 2 }, "Truncated header accepted");
        Array.Resize(ref encoded, 13); await RejectFrame(encoded, "Truncated payload accepted");
        SimulationChecks.Run(Check, Reject);
        await TransportChecks.Run(Check);
        VoiceChecks.Run(Check, Reject);
        Console.WriteLine("PASS: " + checks + " core and real-map simulation checks (not Unity rendering validation).");
    }

    sealed class FragmentedStream : MemoryStream
    {
        public FragmentedStream(byte[] bytes) : base(bytes, false) { }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancel) =>
            base.ReadAsync(buffer, offset, Math.Min(1, count), cancel);
    }
}
