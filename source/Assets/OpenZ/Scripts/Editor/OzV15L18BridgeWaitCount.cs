using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OpenZ.Sim;
#if UNITY_EDITOR && !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;
#endif

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit centre_bridge_wait_r5 (master checklist row ADD5b): the counted check of "the computer waits 50-120 s at the centre bridge's red
    /// end". No Unity API in the count itself (the kit's Mono harness runs the same code); nothing in the game reads this file.
    /// Level 18 at HARD as a human against the computer, the human side giving no order (as the 13 Nov HARD Play run): the computer RED
    /// (human blue, PLAY AS BLUE) and the computer BLUE (human red), <see cref="Seeds"/> seeds each, up to <see cref="Secs"/> game seconds
    /// (a match the computer wins ends sooner), with the switches View/ZGame*.cs turns on at a level start (read from its own source).
    /// Counted every game second for every unit of the computer:
    /// <para>1 the centre bridge's red end box (tiles x 54-72, y 61-76: the deck's last tiles, the bank, the road and the red fort's door at
    /// (63, 75)): every stay of 50 s or more, split into seconds standing WITH an order (stuck or queued) and standing with NO order (idle:
    /// waiting for the computer's next order round, Zod's planner, Sim/ZBot.cs Stage1AI_3).</para>
    /// <para>2 every crossing of the centre river (from the last second on one bank to the first on the other, x 50-75): its time, over the
    /// deck or wading, and the seconds it stood still with an order on the way.</para>
    /// <para>3 the centre bridge's queue (Sim/ZBridges.cs): its longest wait and its give-ups.</para>
    /// PASS = no computer unit stands 50 s with an order in the box; every crossing stands still with an order under 20 s and ends within
    /// 75 s (a robot) / 150 s (a vehicle: the Bizon wades at its own slow pace); the centre bridge's longest queue wait under 30 s, no
    /// give-up; and at least one crossing for each colour (else nothing was counted).
    /// </summary>
    public static class OzV15L18BridgeWaitCount
    {
        public const int Seeds = 5;
        public const double Secs = 600;
        public const double LiveStandFail = 50, RiverStillFail = 20, RobotCrossFail = 75, VehicleCrossFail = 150, QueueWaitFail = 30;

        /// <summary>Set by the game only on other difficulties / options than HARD with the default OPTIONS (not in the count).</summary>
        static readonly HashSet<string> OffByDefault = new HashSet<string> { "ArmourChipDeviation", "OvertakeDeviation", "CustomStatsDeviation", "L17CrewsDeviation" };

        /// <summary>Every ZWorld switch View/ZGame*.cs sets at a level start (a name ending in Deviation, and NoTankDuelVsJeep), read from the
        /// files' own text: all on with the default OPTIONS except <see cref="OffByDefault"/>; FortDoorGunDeviation is on at HARD against the
        /// computer, which is the count's game.</summary>
        public static List<string> PlaySwitches(IEnumerable<string> zgameSources)
        {
            var names = new List<string>();
            foreach (var src in zgameSources)
                foreach (Match m in Regex.Matches(src, @"ZWorld\.([A-Za-z0-9]+)\s*=(?!=)"))
                {
                    var n = m.Groups[1].Value;
                    if ((n.EndsWith("Deviation") || n == "NoTankDuelVsJeep") && !OffByDefault.Contains(n) && !names.Contains(n)) names.Add(n);
                }
            return names;
        }

        public static bool RedEndBox(double tx, double ty) => tx >= 54 && tx <= 72 && ty >= 61 && ty <= 76;
        static bool OnDeck(double tx) => (tx >= 41 && tx < 43) || (tx >= 62 && tx < 64) || (tx >= 83 && tx < 85);
        static double Sq(double v) => v * v;

        sealed class U
        {
            public double ax, ay, at;                                       // where it stood since when (4 px)
            public double boxIn = -1, boxLive, boxIdle, liveRun;          // liveRun: seconds standing WITH an order in a row
            public int bank; public double bankLeft, riverStill; public bool deck;
        }

        public sealed class One
        {
            public bool cpuRed; public int seed; public double end; public string winner = "";
            public int stays, staysLive, crossings, slow; public double longestStay, longestLiveStand, longestCross, longestRobotCross, longestVehicleCross, mostStill, queueWait; public int giveUps;
            public readonly List<string> lines = new List<string>();
        }

        /// <summary>One match: the computer on one colour, the other colour gives no order.</summary>
        public static One Match(string settings, string l18Map, IList<string> playSwitches, bool cpuRed, int seed, double secs)
        {
            var r = new One { cpuRed = cpuRed, seed = seed };
            using (OzV11Kit.GameSwitches())
            {
                foreach (var n in playSwitches) OzV11Kit.TrySet(typeof(ZWorld), n, true);
                OzV11Kit.TrySet(typeof(ZWorld), "FortDoorGunDeviation", true);              // HARD, a human against the computer (View/ZGame.cs)
                OzV11Kit.TrySet(typeof(ZWorld), "HeroSquadsOffAsBlue", cpuRed);            // PLAY AS BLUE when the computer is red
                Team cpu = cpuRed ? Team.Red : Team.Blue;
                var w = ZWorld.Create(settings, l18Map, seed, false);
                w.Bots.Add(new ZBot(w, cpu, cpuRed ? seed : seed + 101));                   // the game's own seeding (View/ZGame.cs)
                var us = new Dictionary<int, U>();
                int ticks = (int)Math.Round(secs / ZConst.TICK), perSec = (int)Math.Round(1.0 / ZConst.TICK);
                for (int i = 0; i < ticks && !w.GameOver; i++)
                {
                    w.Tick(ZConst.TICK); w.Events.Clear();
                    if ((i + 1) % perSec != 0) continue;
                    double t = w.Time; var seen = new HashSet<int>();
                    foreach (var o in w.Objects)
                    {
                        if (!o.IsMobile || o.Destroyed || !o.CanMove || o.Owner != cpu) continue;
                        seen.Add(o.Id);
                        if (!us.TryGetValue(o.Id, out var u)) { u = new U { ax = o.CenterX, ay = o.CenterY, at = t }; us[o.Id] = u; }
                        if (Math.Abs(o.CenterX - u.ax) > 4 || Math.Abs(o.CenterY - u.ay) > 4) { u.ax = o.CenterX; u.ay = o.CenterY; u.at = t; }
                        bool order = o.Waypoints.Count > 0, still = t - u.at >= 1;
                        double tx = o.CenterX / 16.0, ty = o.CenterY / 16.0;
                        // 1 the red end box
                        bool inBox = RedEndBox(tx, ty);
                        if (inBox && u.boxIn < 0) { u.boxIn = t; u.boxLive = u.boxIdle = 0; }
                        u.liveRun = still && order ? u.liveRun + 1 : 0;
                        if (inBox && still) { if (order) { if (u.liveRun > r.longestLiveStand) r.longestLiveStand = u.liveRun; u.boxLive++; } else u.boxIdle++; }
                        if (!inBox && u.boxIn >= 0) { Stay(r, o, u, t, $"left at ({tx:F0},{ty:F0})"); u.boxIn = -1; }
                        // 2 centre river crossings
                        int bank = ty >= 66 ? 1 : ty <= 53 ? -1 : 0;
                        if (bank != 0)
                        {
                            if (u.bank != 0 && bank != u.bank && tx >= 50 && tx <= 75)
                            {
                                double d = t - u.bankLeft; bool veh = o.Kind == ZKind.Vehicle; r.crossings++;
                                if (d > r.longestCross) r.longestCross = d;
                                if (veh) { if (d > r.longestVehicleCross) r.longestVehicleCross = d; } else if (d > r.longestRobotCross) r.longestRobotCross = d;
                                if (u.riverStill > r.mostStill) r.mostStill = u.riverStill;
                                bool bad = u.riverStill >= RiverStillFail || d > (veh ? VehicleCrossFail : RobotCrossFail);
                                if (bad) { r.slow++; r.lines.Add($"    SLOW CROSSING {d:0} s {(u.bank > 0 ? "S->N" : "N->S")} {o.Type}#{o.Id} {u.bankLeft:0}-{t:0} s {(u.deck ? "over the deck" : "wading")}, still with an order {u.riverStill:0} s"); }
                            }
                            u.bank = bank; u.bankLeft = t; u.riverStill = 0; u.deck = false;
                        }
                        else if (u.bank != 0) { if (OnDeck(tx)) u.deck = true; if (still && order) u.riverStill++; }
                    }
                    foreach (var kv in us) if (!seen.Contains(kv.Key) && kv.Value.boxIn >= 0) { Stay(r, null, kv.Value, t, "destroyed / boarded"); kv.Value.boxIn = -1; }
                    // 3 the centre bridge's queue, read every second (its lane exists only while the bridge stands)
                    var lane = w.Pathing != null ? w.Pathing.BridgeLanes.FirstOrDefault(b => b.BridgeObjects.Any(x => (x.LocX >> 4) == 61 && (x.LocY >> 4) == 54)) : null;
                    if (lane != null) { r.queueWait = Math.Max(r.queueWait, lane.MaxWait); r.giveUps = Math.Max(r.giveUps, lane.GiveUps); }
                }
                foreach (var kv in us) if (kv.Value.boxIn >= 0) Stay(r, w.Objects.FirstOrDefault(o => o.Id == kv.Key), kv.Value, w.Time, "in the box at the end");
                r.end = w.Time; r.winner = w.GameOver ? w.Winner.ToString() : "none";
            }
            return r;
        }

        static void Stay(One r, ZObject o, U u, double t, string how)
        {
            double d = t - u.boxIn;
            if (d < LiveStandFail) return;
            r.stays++; if (d > r.longestStay) r.longestStay = d;
            if (u.boxLive >= LiveStandFail) r.staysLive++;
            r.lines.Add($"    stay {d:0} s {(o != null ? o.Type + "#" + o.Id : "a unit")} {u.boxIn:0}-{t:0} s ({how}): standing with NO order {u.boxIdle:0} s, standing WITH an order {u.boxLive:0} s");
        }

        /// <summary>The whole count: <paramref name="seeds"/> matches the computer red, as many the computer blue; verdict lines PASS / FAIL.</summary>
        public static string Run(string settings, string l18Map, IList<string> playSwitches, int seeds = Seeds, double secs = Secs, string colours = "red,blue")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"L18 centre bridge wait (kit centre_bridge_wait_r5, row ADD5b): HARD, a human who gives no order against the computer; {seeds} seeds per colour, up to {secs:0} game s; {playSwitches.Count} game switches from View/ZGame*.cs");
            sb.AppendLine("computer | seed | match end | red-end stays >= 50 s (all) | of them standing WITH an order >= 50 s | longest stand with an order in the box | centre crossings | longest robot / vehicle crossing | most still with an order in one crossing | centre bridge queue: longest wait, give-ups");
            var all = new List<One>();
            foreach (var col in colours.Split(','))
                for (int s = 1; s <= seeds; s++)
                {
                    var r = Match(settings, l18Map, playSwitches, col.Trim() == "red", s, secs); all.Add(r);
                    sb.AppendLine($"{(r.cpuRed ? "RED " : "BLUE")} | {s} | {r.end:0} s (winner {r.winner}) | {r.stays} (longest {r.longestStay:0} s) | {r.staysLive} | {r.longestLiveStand:0} s | {r.crossings} | {r.longestRobotCross:0} / {r.longestVehicleCross:0} s | {r.mostStill:0} s | {r.queueWait:0} s, {r.giveUps}");
                    foreach (var l in r.lines) sb.AppendLine(l);
                }
            int live = all.Sum(r => r.staysLive), slow = all.Sum(r => r.slow), give = all.Sum(r => r.giveUps);
            double liveStand = all.Max(r => r.longestLiveStand), queue = all.Max(r => r.queueWait);
            Verdict(sb, liveStand < LiveStandFail && live == 0, $"no computer unit stands {LiveStandFail:0} s WITH an order at the centre bridge's red end (longest {liveStand:0} s; stays >= {LiveStandFail:0} s with an order: {live}); every stay >= {LiveStandFail:0} s there ({all.Sum(r => r.stays)}, longest {all.Max(r => r.longestStay):0} s) is standing with no order: waiting for the computer's next order round");
            Verdict(sb, slow == 0, $"every centre river crossing of the computer ({all.Sum(r => r.crossings)}) stands still with an order under {RiverStillFail:0} s (most {all.Max(r => r.mostStill):0} s) and ends within {RobotCrossFail:0} s for a robot (longest {all.Max(r => r.longestRobotCross):0} s) / {VehicleCrossFail:0} s for a vehicle (longest {all.Max(r => r.longestVehicleCross):0} s): slow {slow}");
            Verdict(sb, queue < QueueWaitFail && give == 0, $"the centre bridge's queue: longest wait {queue:0} s (under {QueueWaitFail:0} s), give-ups {give}");
            foreach (var col in colours.Split(','))
            {
                bool red = col.Trim() == "red"; int n = all.Where(r => r.cpuRed == red).Sum(r => r.crossings);
                Verdict(sb, n > 0, $"the computer {(red ? "red" : "blue")} crossed the centre river {n} times in {seeds} matches (something was counted)");
            }
            return sb.ToString();
        }

        static void Verdict(StringBuilder sb, bool pass, string what) => sb.AppendLine((pass ? "PASS  " : "FAIL  ") + what);
    }

#if UNITY_EDITOR && !OPENZ_HARNESS
    /// <summary>Menus of the count (no Play: the headless simulation, the editor freezes for one to three minutes per colour).</summary>
    public static class OzV15L18BridgeWaitMenu
    {
        static string ReportPath => System.IO.Path.Combine(Application.dataPath, "../Logs/l18_centre_bridge_wait_check.txt");

        static List<string> Switches() => OzV15L18BridgeWaitCount.PlaySwitches(
            System.IO.Directory.GetFiles(System.IO.Path.Combine(Application.dataPath, "OpenZ/Scripts/View"), "ZGame*.cs").OrderBy(f => f).Select(f => System.IO.File.ReadAllText(f)));

        static void Go(string colours)
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] L18 centre bridge wait: leave Play mode first (the count sets the simulation's switches)."); return; }
            string r = OzV15L18BridgeWaitCount.Run(OzV11Kit.SettingsText, OzV11Kit.MapText("L18"), Switches(), OzV15L18BridgeWaitCount.Seeds, OzV15L18BridgeWaitCount.Secs, colours);
            System.IO.File.AppendAllText(ReportPath, $"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} computer {colours}\n" + r);
            Debug.Log("[OpenZ V1.5] L18 centre bridge wait (" + colours + "), report " + ReportPath + ":\n" + r);
        }

        [MenuItem("OpenZ/V1.5/L18 Proofs/5 Centre bridge wait: the computer RED x 5 (no Play)", false, 7704)]
        static void Red() => Go("red");

        [MenuItem("OpenZ/V1.5/L18 Proofs/6 Centre bridge wait: the computer BLUE x 5 (no Play)", false, 7705)]
        static void Blue() => Go("blue");
    }
#endif
}
