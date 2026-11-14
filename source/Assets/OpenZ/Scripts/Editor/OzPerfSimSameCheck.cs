using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Version 1.3 item 25, the counted check of the rule speed-ups (Sim/ZWorld.PerfRules.cs): the same rules, bit for bit. Headless: no
    /// scene, no Play mode, nothing saved. Every part runs the same thing twice in this build, once with ZWorld.PerfFastRules off (the 1.2
    /// code paths) and once on:
    /// <list type="bullet">
    /// <item>S1 whole computer matches on L02, L16 and L17 (the switches of a game in Play): the world's fingerprint after EVERY tick
    /// (OzV11StrikeCount.Fold plus the tick's events, hold / target / destroyed of every object, the flags and the computer players'
    /// counters) must be identical from the first tick to the last, and the match must end the same.</item>
    /// <item>S2 the path work counters (Zod A* runs and work units, the DV44 budget's input) and the approach walk counters are the same.</item>
    /// <item>S3 Zod's A* alone: random searches on every map (robot / vehicle, with / without explosives, the search forced): the same points
    /// and the same work units for every search; the time of both shown.</item>
    /// <item>S4 the DV28 approach walk alone: at 2, 5 and 8 minutes of a match, every unit against up to 12 enemies: the same answer, spot
    /// and path; the time of both shown.</item>
    /// </list>
    /// The other half of the proof (this build against the 1.2 build, tick for tick) needs the two builds side by side: the package's
    /// </summary>
    public static class OzPerfSimSameCheck
    {
        public static readonly string[] Levels = { "L02", "L16", "L17" };

        /// <summary>The newer rules a Play session leaves on (OzV11Kit.GameSwitches turns them off for the counted checks); set when the
        /// build has them, so the matches run the code paths of a real game.</summary>
        public static readonly string[] PlayRules =
        {
            "VeteranHealDeviation", "FortSniperDeviation", "SniperHeadshotDeviation", "SniperSteadyDeviation", "ToughSteadyDeviation", "NoAutoFlagUnderFireDeviation",
            "HeavyFallDeviation", "GroundFireDeviation", "ProneTargetDeviation", "HeroBoostDeviation", "HeroPlusDeviation", "JeepChargeDeviation", "CraneSnipeDeviation",
            "ApcSnipeDeviation", "MechHpDeviation", "MechMixDeviation", "MechGunLossDeviation", "BotHeavyDeviation", "CpuRepairDeviation", "CpuDefendDeviation",
            "SniperBalanceDeviation", "CpuShuttleDeviation",
        };

        public sealed class Run
        {
            public string level, error; public int seed; public bool fast, play;
            public double end, ms; public bool over; public Team winner;
            public int astar, walks; public long work, tiles;
            public readonly List<ulong> hashes = new List<ulong>();
        }

        static ulong Mix(ulong h, long v) { h ^= (ulong)v; h *= 1099511628211UL; h ^= h >> 31; return h; }
        static string F(double v, string f = "0") => v.ToString(f, CultureInfo.InvariantCulture);

        /// <summary>One computer match, the fingerprint of every tick.</summary>
        public static Run Match(string settings, string map, string level, int seed, bool fast, bool play, double seconds)
        {
            var R = new Run { level = level, seed = seed, fast = fast, play = play };
            bool old = ZWorld.PerfFastRules;
            try
            {
                using (OzV11StrikeCount.Game(true))
                {
                    if (play) foreach (var n in PlayRules) OzV11Kit.TrySet(typeof(ZWorld), n, true);
                    ZWorld.PerfFastRules = fast;
                    var w = ZWorld.Create(settings, map, seed, level == "L17");
                    w.Bots.Add(new ZBot(w, Team.Blue, seed + 101)); w.Bots.Add(new ZBot(w, Team.Red, seed));
                    w.Events.Clear();
                    ulong h = 1469598103934665603UL;
                    var sw = Stopwatch.StartNew(); double tickMs = 0;
                    while (w.Time < seconds && !w.GameOver)
                    {
                        long t0 = Stopwatch.GetTimestamp();
                        w.Tick(ZConst.TICK);
                        tickMs += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                        foreach (var e in w.Events) { h = Mix(h, (long)e.type); h = Mix(h, e.objId); }
                        w.Events.Clear();
                        h = OzV11StrikeCount.Fold(h, w);
                        foreach (var o in w.Objects) { h = Mix(h, o.HoldPosition ? 1 : 0); h = Mix(h, o.AttackObject != null ? o.AttackObject.Id : -1); h = Mix(h, o.Destroyed ? 1 : 0); }
                        foreach (var f in w.Flags) h = Mix(h, (long)f.Owner);
                        foreach (var b in w.Bots) { h = Mix(h, b.Orders); h = Mix(h, b.Rounds); h = Mix(h, b.RejectedOrders); h = Mix(h, b.BuildChanges); }
                        R.hashes.Add(h);
                    }
                    R.ms = tickMs; R.end = w.Time; R.over = w.GameOver; R.winner = w.Winner;
                    R.astar = w.Path.AStarRuns; R.work = w.Path.Work; R.walks = (int)w.ApproachWalks; R.tiles = w.ApproachTiles;
                }
            }
            catch (Exception e) { R.error = e.GetType().Name + ": " + e.Message; }
            finally { ZWorld.PerfFastRules = old; }
            return R;
        }

        /// <summary>S3: random forced A* searches on one map, slow and fast; (same, total, slow ms, fast ms).</summary>
        public static (int same, int total, double slowMs, double fastMs, string firstDiff) AStarDirect(string settings, string map, string level, int queries)
        {
            bool old = ZWorld.PerfFastRules;
            int same = 0, total = 0; double slowMs = 0, fastMs = 0; string firstDiff = null;
            try
            {
                using (OzV11StrikeCount.Game(true))
                {
                    var w = ZWorld.Create(settings, map, 1, level == "L17");
                    var pf = w.Path; var rnd = new Random(25);
                    for (int q = 0; q < queries; q++)
                    {
                        bool robot = (q & 1) == 0, nr = (q & 2) != 0;
                        int sx, sy, ex, ey, guard = 0;
                        do { sx = rnd.Next(pf.W); sy = rnd.Next(pf.H); } while (!pf.TilePassable(sx, sy, robot) && ++guard < 1000);
                        guard = 0;
                        do { ex = rnd.Next(pf.W); ey = rnd.Next(pf.H); } while (!pf.TilePassable(ex, ey, robot) && ++guard < 1000);
                        int o = robot ? 8 : 16;
                        var a = One(pf, false, sx * 16 + o, sy * 16 + o, ex * 16 + o, ey * 16 + o, robot, nr, ref slowMs);
                        var b = One(pf, true, sx * 16 + o, sy * 16 + o, ex * 16 + o, ey * 16 + o, robot, nr, ref fastMs);
                        total++;
                        if (a == b) same++; else if (firstDiff == null) firstDiff = $"{level} ({sx},{sy})->({ex},{ey}) robot {robot} explosives {nr}: 1.2 {a} / fast {b}";
                    }
                }
            }
            finally { ZWorld.PerfFastRules = old; }
            return (same, total, slowMs, fastMs, firstDiff);
        }

        static string One(ZPathFinder pf, bool fast, int sx, int sy, int ex, int ey, bool robot, bool nr, ref double ms)
        {
            ZWorld.PerfFastRules = fast;
            long w0 = pf.Work; int r0 = pf.AStarRuns;
            pf.ForceSearch = true;
            long t0 = Stopwatch.GetTimestamp();
            int id = pf.FindPath(sx, sy, ex, ey, robot, nr, out var pts);
            ms += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            var sb = new StringBuilder();
            sb.Append(id != 0 ? "search" : "none").Append(" runs ").Append(pf.AStarRuns - r0).Append(" work ").Append(pf.Work - w0).Append(" pts");
            if (pts != null) foreach (var (x, y) in pts) sb.Append(' ').Append(x).Append(',').Append(y);
            return sb.ToString();
        }

        /// <summary>S4: the DV28 walk itself (internal ZWorld.NearestInRangeSpot, by reflection), both ways on the same world state.</summary>
        public static (int same, int total, double slowMs, double fastMs, string firstDiff) ApproachDirect(string settings, string map, string level, int seed)
        {
            bool old = ZWorld.PerfFastRules;
            int same = 0, total = 0; double slowMs = 0, fastMs = 0; string firstDiff = null;
            var mi = typeof(ZWorld).GetMethod("NearestInRangeSpot", BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi == null) return (0, 0, 0, 0, "NearestInRangeSpot not found");
            try
            {
                using (OzV11StrikeCount.Game(true))
                {
                    foreach (var n in PlayRules) OzV11Kit.TrySet(typeof(ZWorld), n, true);
                    ZWorld.PerfFastRules = true;
                    var w = ZWorld.Create(settings, map, seed, level == "L17");
                    w.Bots.Add(new ZBot(w, Team.Blue, seed + 101)); w.Bots.Add(new ZBot(w, Team.Red, seed));
                    foreach (double at in new[] { 120.0, 300.0, 480.0 })
                    {
                        while (w.Time < at && !w.GameOver) { w.Tick(ZConst.TICK); w.Events.Clear(); }
                        if (w.GameOver) break;
                        var units = w.Objects.Where(o => o.IsUnit && !o.Destroyed && o.KillAt < 0 && o.Owner != Team.Neutral && o.CanMove).ToList();
                        foreach (var u in units)
                        {
                            int k = 0;
                            foreach (var t in w.Objects)
                            {
                                if (!t.IsUnit || t.Destroyed || t.KillAt >= 0 || t.Owner == Team.Neutral || t.Owner == u.Owner) continue;
                                if (++k > 12) break;
                                int range = Math.Max(8, u.AttackRadius - 4);
                                string a = Walk(mi, w, u, t, range, false, ref slowMs), b = Walk(mi, w, u, t, range, true, ref fastMs);
                                total++;
                                if (a == b) same++; else if (firstDiff == null) firstDiff = $"{level} {F(w.Time, "0")} s #{u.Id} {u.Type} -> #{t.Id} {t.Type}: 1.2 {a} / fast {b}";
                            }
                        }
                        ZWorld.PerfFastRules = true;
                    }
                }
            }
            finally { ZWorld.PerfFastRules = old; }
            return (same, total, slowMs, fastMs, firstDiff);
        }

        static string Walk(MethodInfo mi, ZWorld w, ZObject u, ZObject t, int range, bool fast, ref double ms)
        {
            ZWorld.PerfFastRules = fast;
            var args = new object[] { u, t, range, 0, 0, null };
            long t0 = Stopwatch.GetTimestamp();
            bool ok = (bool)mi.Invoke(w, args);
            ms += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            var sb = new StringBuilder(ok ? "spot " : "none ");
            sb.Append(args[3]).Append(',').Append(args[4]);
            if (args[5] is List<(int x, int y)> pts) foreach (var (x, y) in pts) sb.Append(' ').Append(x).Append(',').Append(y);
            return sb.ToString();
        }

        /// <summary>The whole check. seeds 1..seeds, minutes of game time per match; both = the counted checks' switches too (not only a game's).</summary>
        public static string Check(string settings, Func<string, string> map, int seeds, double minutes, string[] levels, bool both, int astarQueries)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"1.3 item 25: the rule speed-ups give the same results (ZWorld.PerfFastRules off = the 1.2 code, on = the fast code); {levels.Length} level(s), seeds 1-{seeds}, {F(minutes)} min a match");
            var V = new List<string>();
            int pairs = 0, identical = 0, countersSame = 0; double msOld = 0, msFast = 0;
            foreach (var lv in levels)
                for (int s = 1; s <= seeds; s++)
                    foreach (bool play in both ? new[] { true, false } : new[] { true })
                    {
                        var a = Match(settings, map(lv), lv, s, false, play, minutes * 60.0);
                        var b = Match(settings, map(lv), lv, s, true, play, minutes * 60.0);
                        pairs++;
                        int d = -1;
                        for (int i = 0; i < Math.Min(a.hashes.Count, b.hashes.Count); i++) if (a.hashes[i] != b.hashes[i]) { d = i; break; }
                        bool same = a.error == null && b.error == null && d < 0 && a.hashes.Count == b.hashes.Count && a.over == b.over && a.winner == b.winner;
                        if (same) identical++;
                        bool cs = a.astar == b.astar && a.work == b.work && a.walks == b.walks && a.tiles == b.tiles;
                        if (cs) countersSame++;
                        msOld += a.ms; msFast += b.ms;
                        sb.AppendLine($"  {lv} seed {s} {(play ? "game rules" : "check rules")}: {(same ? "IDENTICAL" : "DIFFERENT")} over {a.hashes.Count} ticks{(d >= 0 ? " (first different tick " + d + ", game " + F(d * ZConst.TICK, "0.00") + " s)" : "")}"
                                      + $"{(a.error != null || b.error != null ? "; ERROR " + (a.error ?? b.error) : "")}; end {F(a.end, "0.0")} / {F(b.end, "0.0")} s, winner {a.winner} / {b.winner}; "
                                      + $"A* {a.astar} / {b.astar}, work {a.work} / {b.work}, approach walks {a.walks} / {b.walks} ({a.tiles} / {b.tiles} tiles); rule time {F(a.ms)} / {F(b.ms)} ms ({F(a.ms / Math.Max(1e-6, b.ms), "0.00")}x)");
                    }
            V.Add(OzV11Kit.Verdict(pairs > 0 && identical == pairs, $"S1 the same match, tick for tick: {identical} of {pairs} matches identical from the first tick to the last, the same end"));
            V.Add(OzV11Kit.Verdict(pairs > 0 && countersSame == pairs, $"S2 the same path work (the DV44 budget's input) and the same approach walks: {countersSame} of {pairs} matches"));
            int aSame = 0, aTotal = 0; double aSlow = 0, aFast = 0; string aDiff = null;
            foreach (var lv in levels) { var r = AStarDirect(settings, map(lv), lv, astarQueries); aSame += r.same; aTotal += r.total; aSlow += r.slowMs; aFast += r.fastMs; aDiff ??= r.firstDiff; }
            V.Add(OzV11Kit.Verdict(aTotal > 0 && aSame == aTotal, $"S3 Zod's A* alone: {aSame} of {aTotal} forced searches gave the same points and the same work units; time {F(aSlow)} ms (1.2) / {F(aFast)} ms (fast), {F(aSlow / Math.Max(1e-6, aFast), "0.0")}x{(aDiff != null ? "; first difference: " + aDiff : "")}"));
            int wSame = 0, wTotal = 0; double wSlow = 0, wFast = 0; string wDiff = null;
            foreach (var lv in levels) { var r = ApproachDirect(settings, map(lv), lv, 1); wSame += r.same; wTotal += r.total; wSlow += r.slowMs; wFast += r.fastMs; wDiff ??= r.firstDiff; }
            V.Add(OzV11Kit.Verdict(wTotal > 0 && wSame == wTotal, $"S4 the DV28 approach walk alone: {wSame} of {wTotal} walks gave the same answer, spot and path; time {F(wSlow)} ms (1.2) / {F(wFast)} ms (fast), {F(wSlow / Math.Max(1e-6, wFast), "0.0")}x{(wDiff != null ? "; first difference: " + wDiff : "")}"));
            sb.AppendLine($"  rule time of all matches: {F(msOld)} ms (1.2 code) / {F(msFast)} ms (fast), {F(msOld / Math.Max(1e-6, msFast), "0.00")}x (this machine, this runtime; the player's Mono is slower in both)");
            foreach (var v in V) sb.AppendLine(v);
            sb.AppendLine($"MACHINE pairs {pairs} identical {identical} counters {countersSame} astar {aSame}/{aTotal} walks {wSame}/{wTotal} msOld {F(msOld)} msFast {F(msFast)}");
            return sb.ToString();
        }

#if UNITY_EDITOR
        static string OutDir()
        {
            string d = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../..", "work", "staging", "v1_3", "item25_out"));
            System.IO.Directory.CreateDirectory(d);
            return d;
        }

        static void Menu(string title, int seeds, double minutes, bool both, int queries)
        {
            OzV11Kit.RunMenu(title, () =>
            {
                string rep = Check(OzV11Kit.SettingsText, OzV11Kit.MapText, seeds, minutes, Levels, both, queries);
                string f = System.IO.Path.Combine(OutDir(), "OzPerfSimSame_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".txt");
                System.IO.File.WriteAllText(f, rep);
                return rep + "report: " + f.Replace('\\', '/');
            });
        }

        [UnityEditor.MenuItem("OpenZ/Checks/1.3 perf: same rules, fast vs 1.2 code (quick: 3 levels, seed 1, 3 min)")]
        static void Quick() => Menu("1.3 perf: same rules (quick)", 1, 3, false, 600);

        [UnityEditor.MenuItem("OpenZ/Checks/1.3 perf: same rules, fast vs 1.2 code (full: 3 levels, seeds 1-2, 10 min, both rule sets)")]
        static void Full() => Menu("1.3 perf: same rules (full)", 2, 10, true, 3000);
#endif
    }
}
