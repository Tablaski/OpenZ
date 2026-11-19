using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit l18_cpu_bigtank_r2: the counted check of DV202 "the computer drives its big tank" (Sim/ZBot.BigTankDrive.cs), no Unity
    /// API (the kit's harness builds it too). Level 18, two computers, the big tanks' own rules on (DV180-DV183, DV189, DV192, DV198, DV199),
    /// <see cref="Seconds"/> s, the same seed with DV202 OFF and ON. For each side's big tank while a computer owns it: orders, shots, the longest
    /// stand with no order and no fight. PASS when: ON, every owned big tank gets DV202 orders, fires, and never stands idle longer than
    /// <see cref="IdleLimitSec"/> s; OFF, DV202 gives no order (Zod's planner as before); and level 2 (no big tank) plays the same fingerprint
    /// with DV202 on and off.</summary>
    public static class OzV15BigTankDriveCount
    {
        public const double Seconds = 300, IdleLimitSec = 12, Level2Seconds = 120;

        static readonly string[] BigTankRules = { "MammothDeviation", "BizonDeviation", "MammothRamDeviation", "BizonCrushDeviation", "BigTankRamFirstDeviation",
                                                  "BizonAmphibDeviation", "L18BigTanksDeviation", "BotBigTanksDeviation" };

        sealed class Tank { public ZObject o; public int shots, idleNow, idleMax, owned, lastX, lastY; public double idleAt; }

        /// <summary>One match: (text, every owned big tank's facts, DV202 orders of both bots).</summary>
        static (string text, List<Tank> tanks, int orders) Match(string settings, string map, int seed, bool drive)
        {
            bool was = ZWorld.BigTankDriveDeviation; ZWorld.BigTankDriveDeviation = drive;
            try
            {
                var w = ZWorld.Create(settings, map, seed, false);
                var bots = new[] { new ZBot(w, Team.Blue, ZBot.SeedFor(Team.Blue, seed)), new ZBot(w, Team.Red, ZBot.SeedFor(Team.Red, seed)) };
                foreach (var b in bots) w.Bots.Add(b);
                var tanks = w.Objects.Where(o => o.Kind == ZKind.Vehicle && ZBigTanks.Is(o.Type)).Select(o => new Tank { o = o, lastX = o.CenterX, lastY = o.CenterY }).ToList();
                var byId = tanks.ToDictionary(t => t.o.Id);
                int ticks = (int)Math.Round(Seconds / ZConst.TICK);
                for (int i = 0; i < ticks && !w.GameOver; i++)
                {
                    w.Tick(ZConst.TICK);
                    foreach (var e in w.Events) if (e.type == ZEventType.MissileFired && byId.TryGetValue(e.objId, out var t)) t.shots++;
                    w.Events.Clear();
                    if (i % 100 != 99) continue;
                    foreach (var t in tanks)
                    {
                        var o = t.o;
                        if (o.Destroyed || o.Owner == Team.Neutral) { t.idleNow = 0; t.lastX = o.CenterX; t.lastY = o.CenterY; continue; }
                        t.owned++;
                        int dx = o.CenterX - t.lastX, dy = o.CenterY - t.lastY; t.lastX = o.CenterX; t.lastY = o.CenterY;
                        bool idle = o.Waypoints.Count == 0 && o.AttackObject == null && dx * dx + dy * dy < 4;
                        if (idle) { t.idleNow++; if (t.idleNow > t.idleMax) { t.idleMax = t.idleNow; t.idleAt = w.Time; } } else t.idleNow = 0;
                    }
                }
                int orders = bots.Sum(b => b.BigTankDriveOrders);
                string text = $"seed {seed} DV202 {(drive ? "ON " : "OFF")}: DV202 orders {orders} (stalls given up {bots.Sum(b => b.BigTankDriveStalls)}); " +
                              string.Join("; ", tanks.Select(t => $"{t.o.Type}#{t.o.Id} now {t.o.Owner}{(t.o.Destroyed ? " destroyed" : "")}, owned {t.owned} s, shots {t.shots}, longest idle {t.idleMax} s{(t.idleMax > 0 ? " (ending " + t.idleAt.ToString("0") + " s)" : "")}"));
                return (text, tanks, orders);
            }
            finally { ZWorld.BigTankDriveDeviation = was; }
        }

        static string Level2(string settings, string map2, bool drive)
        {
            bool was = ZWorld.BigTankDriveDeviation; ZWorld.BigTankDriveDeviation = drive;
            try
            {
                var w = ZWorld.Create(settings, map2, 1, false);
                w.Bots.Add(new ZBot(w, Team.Blue, ZBot.SeedFor(Team.Blue, 1))); w.Bots.Add(new ZBot(w, Team.Red, ZBot.SeedFor(Team.Red, 1)));
                ulong h = 1469598103934665603UL; int ticks = (int)Math.Round(Level2Seconds / ZConst.TICK);
                for (int i = 0; i < ticks && !w.GameOver; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); if (i % 100 == 99) h = w.RulesFingerprint(h); }
                return $"{h:x16} at {w.Time:0} s, {w.Objects.Count} objects";
            }
            finally { ZWorld.BigTankDriveDeviation = was; }
        }

        /// <summary>The check. map = level 18's grid, map2 = level 2's (null: that part is left out). Call inside OzV11Kit.GameSwitches().</summary>
        public static string Run(string settings, string map, string map2, int seed = 1)
        {
            var sb = new StringBuilder(); int fails = 0;
            void Check(bool ok, string what) { sb.AppendLine((ok ? "PASS  " : "FAIL  ") + what); if (!ok) fails++; }
            var held = new List<(string n, bool v)>();
            foreach (var n in BigTankRules) { var f = typeof(ZWorld).GetField(n); if (f != null) { held.Add((n, (bool)f.GetValue(null))); f.SetValue(null, true); } }
            try
            {
                var off = Match(settings, map, seed, false); sb.AppendLine("INFO  " + off.text);
                var on = Match(settings, map, seed, true); sb.AppendLine("INFO  " + on.text);
                Check(off.orders == 0, $"DV202 OFF: no order of the round ({off.orders}): Zod's planner orders the big tanks as before");
                var owned = on.tanks.Where(t => t.owned > 0).ToList();
                Check(owned.Count > 0, $"DV202 ON: {owned.Count} big tank(s) driven by a computer in {Seconds:0} s");
                Check(on.orders > 0, $"DV202 ON: {on.orders} orders of the round");
                foreach (var t in owned)
                    Check(t.shots > 0 && t.idleMax <= IdleLimitSec, $"DV202 ON: {t.o.Type}#{t.o.Id} owned {t.owned} s fired {t.shots} shots (want 1 or more), longest idle {t.idleMax} s (want {IdleLimitSec:0} s or less)");
                if (map2 != null)
                {
                    string a = Level2(settings, map2, false), b = Level2(settings, map2, true);
                    Check(a == b, $"level 2 (no big tank), two computers {Level2Seconds:0} s: DV202 off {a}, on {b}: the same");
                }
            }
            finally { foreach (var (n, v) in held) typeof(ZWorld).GetField(n).SetValue(null, v); }
            sb.AppendLine(fails == 0 ? "DV202: PASS" : $"DV202: FAIL ({fails})");
            return sb.ToString();
        }
    }
}
