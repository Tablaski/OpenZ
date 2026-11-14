using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.3 item 4 (rule DV155, Sim/ZWorld.RuinProduction.cs): no build clock on a destroyed factory, nothing comes out of it when it is
    /// rebuilt. Counted headless (ZWorld.Create + Tick, no scene, nothing saved) on levels 2, 16 and 17 with the game's rules: blue = the
    /// computer, red = the strike check's stand-in player (OzV11StrikeCount.StandIn, so the match runs on). One blue factory (one the
    /// computer owns at the start, else the one of the flag nearest the blue fort, given to blue) is destroyed while it builds (at least
    /// 40 % of its unit done), then the match runs past its rebuild (Zod: 600-660 s) and one whole build time after it. Each run is
    /// played with the rule on and off (off = Zod: the bug):
    ///  R1 while it is a ruin: units out of it 0, ticks it showed a unit in production 0 (off: the computer restarts it within seconds);
    ///  R2 the rebuild comes at Zod's time: 600-660 s after the blast;
    ///  R3 the first 2 s after the rebuild: units out of it 0 (off: its last unit comes out at once in most runs);
    ///  R4 its first unit after the rebuild takes the full build time from the moment it was started (within one tick);
    ///  R5 owner switch FactoryAutoRebuild = false (level 2): no rebuild within 700 s, no unit out of it, a crane order still rebuilds it;
    ///  R6 deterministic: the same run twice gives the same times;
    ///  R7 the switches are back as they were;
    ///  R8 (review) a crane's DV61 mend of a damaged factory that still stands (it runs the same AutoRepair) keeps its build and queue.
    /// </summary>
    public static class OzRuinBuildCount
    {
        public static readonly string[] Levels = { "L02", "L16", "L17" };
        public static int Seeds = 2;

        public sealed class Run
        {
            public string level, factory = "-", unitAtKill = "-", unitAfter = "-", error; public int seed; public bool on, rebuild = true;
            public double killAt = -1, progressAtKill, revivedAt = -1, startAfter = -1, spanAfter = -1, firstOutAfter = -1;
            public int outWhileRuin, ruinTicksBuilding, outFirst2s, outAfter, botSkips;
            public bool craneRebuilt;
        }

        static string F(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);

        static ZObject PickFactory(ZWorld w)
        {
            var own = w.Objects.Where(o => o.IsProducer && !o.IsFort && o.Owner == Team.Blue && !o.Destroyed).OrderBy(o => o.Id).FirstOrDefault();
            if (own != null) return own;
            var fort = w.Objects.FirstOrDefault(o => o.IsFort && o.Owner == Team.Blue);
            if (fort == null) return null;
            foreach (var f in w.Flags.OrderBy(f => OzV11Kit.Dist(f, fort)).ThenBy(f => f.Id))
            {
                var b = f.LinkedObjects.FirstOrDefault(x => x.IsProducer && !x.IsFort && !x.Destroyed);
                if (b == null) continue;
                w.AwardZone(f, Team.Blue);
                return b;
            }
            return null;
        }

        /// <summary>One match. on = ZWorld.RuinNoBuildDeviation, rebuild = ZWorld.FactoryAutoRebuild.</summary>
        public static Run Match(string settings, string map, string level, int seed, bool on, bool rebuild = true)
        {
            var R = new Run { level = level, seed = seed, on = on, rebuild = rebuild };
            using (OzV11StrikeCount.Game(false))
            {
                ZWorld.RuinNoBuildDeviation = on; ZWorld.FactoryAutoRebuild = rebuild; ZWorld.RuinReviveDefault = false;
                try
                {
                    var w = ZWorld.Create(settings, map, seed, level == "L17");
                    var bot = new ZBot(w, Team.Blue, seed + 101);
                    w.Bots.Add(bot);
                    var stand = OzV11StrikeCount.StandIn(w);
                    var b = PickFactory(w);
                    if (b == null) { R.error = "no factory for blue on this level"; return R; }
                    R.factory = b.Type + " #" + b.Id + " at (" + (b.LocX >> 4) + "," + (b.LocY >> 4) + ")";
                    w.Events.Clear();
                    void Tick()
                    {
                        w.Tick(ZConst.TICK);
                        OzV11StrikeCount.StandInKeep(w, stand);
                        foreach (var e in w.Events)
                        {
                            bool out1 = (e.type == ZEventType.UnitBuilt && e.otherId == b.Id) || (e.type == ZEventType.CannonStored && e.objId == b.Id);
                            if (e.type == ZEventType.Revived && e.objId == b.Id) R.revivedAt = w.Time;
                            if (!out1 || R.killAt < 0) continue;
                            if (R.revivedAt < 0) R.outWhileRuin++;
                            else
                            {
                                R.outAfter++;
                                if (w.Time - R.revivedAt <= 2.0 + 1e-6) R.outFirst2s++;
                                if (R.firstOutAfter < 0) R.firstOutAfter = w.Time;
                            }
                        }
                        w.Events.Clear();
                        if (R.killAt >= 0 && b.Destroyed && b.Building) R.ruinTicksBuilding++;
                        if (R.revivedAt >= 0 && R.startAfter < 0 && b.Building && !b.Destroyed) { R.startAfter = b.BInit; R.spanAfter = b.BFinal - b.BInit; R.unitAfter = b.CurUnit; }
                    }
                    // 1. wait until the computer builds in it and the unit is at least 40 % done (at most 8 minutes)
                    while (w.Time < 480 && !w.GameOver && !(b.Building && b.ProductionProgress >= 0.4)) Tick();
                    if (!b.Building) { R.error = "the computer never built in it"; return R; }
                    // 2. destroy it while it builds
                    R.progressAtKill = b.ProductionProgress; R.unitAtKill = b.CurUnit; R.killAt = w.Time;
                    b.SetHealth(0); w.UpdateObjectHealth(b);
                    int skips0 = bot.RuinSkips;
                    // 3. past the rebuild and one whole build time after it (or 700 s with the rebuild off)
                    double end = R.killAt + 700;
                    while (w.Time < end && !w.GameOver)
                    {
                        Tick();
                        if (R.revivedAt >= 0) end = Math.Max(R.revivedAt + 2.5, R.startAfter >= 0 ? R.startAfter + R.spanAfter + 2 : R.revivedAt + 120);   // review: no cap at kill + 700 s, or a rebuild at 640 s ends the run before its next unit (R4 passed empty)
                    }
                    R.botSkips = bot.RuinSkips - skips0;
                    // 4. the rebuild off: a crane order still rebuilds it
                    if (!rebuild && R.revivedAt < 0 && b.Destroyed && b.GetCraneEntrance(out int ex, out int ey, out _, out _))
                    {
                        var crane = w.CreateObject("vehicle", "crane", ex - 16, ey + 32, Team.Blue);
                        bot.Ignored = true;                                                                     // the computer leaves the crane to its order
                        w.SendWaypoints(Team.Blue, crane, new[] { new ZWaypoint { mode = WpMode.CraneRepair, refId = b.Id, x = b.CenterX, y = b.CenterY } });
                        double t0 = w.Time; while (w.Time < t0 + 120 && R.revivedAt < 0) Tick();
                        R.craneRebuilt = R.revivedAt >= 0;
                    }
                }
                catch (Exception ex) { R.error = ex.GetType().Name + ": " + ex.Message; }
            }
            return R;
        }

        /// <summary>R8 (review): a crane's DV61 mend of a damaged factory that still stands also runs AutoRepair; that factory keeps its
        /// unit in progress, its start time and its queue. Returns "" when it held, else what changed.</summary>
        public static string MendKeepsBuild(string settings, string map, string level)
        {
            using (OzV11StrikeCount.Game(false))
            {
                ZWorld.RuinNoBuildDeviation = true;
                try
                {
                    var w = ZWorld.Create(settings, map, 1, level == "L17");
                    var b = PickFactory(w);
                    if (b == null) return "no factory for blue on this level";
                    var list = ZBuildLists.For(b.Type, b.BuildLevel);
                    if (list.Count == 0) return "empty build list";
                    w.StopBuilding(Team.Blue, b); w.StartBuilding(Team.Blue, b, list[0]); w.AddBuildingQueue(Team.Blue, b, list[0]);
                    for (int i = 0; i < 100; i++) w.Tick(ZConst.TICK);
                    b.SetHealth(b.MaxHealth / 2);
                    string unit = b.CurUnit; double init = b.BInit; int queue = b.Queue.Count;
                    b.DoAutoRepair = true; b.NextAutoRepairTime = 0;                                           // what the crane's Stage.ExitBuilding sets
                    w.Tick(ZConst.TICK);
                    if (b.Health != b.MaxHealth) return "not mended";
                    if (!b.Building || b.CurUnit != unit || b.BInit != init || b.Queue.Count != queue)
                        return $"build lost: {unit} -> {b.CurUnit ?? "none"}, queue {queue} -> {b.Queue.Count}";
                    return "";
                }
                catch (Exception ex) { return ex.GetType().Name + ": " + ex.Message; }
            }
        }

        static string Line(Run r) =>
            $"    {r.level} seed {r.seed} rule {(r.on ? "on " : "off")}{(r.rebuild ? "" : " (rebuild off)")}: {r.factory}, destroyed at {F(r.killAt, "0.0")} s building {r.unitAtKill} ({F(r.progressAtKill * 100, "0")} % done); " +
            $"out while a ruin {r.outWhileRuin}, ticks a ruin showed a unit in production {r.ruinTicksBuilding}, computer orders held back {r.botSkips}; " +
            $"rebuilt {(r.revivedAt < 0 ? "no" : F(r.revivedAt - r.killAt, "0.0") + " s after the blast")}; out in the first 2 s after it {r.outFirst2s}; " +
            $"next unit {r.unitAfter} started {(r.startAfter < 0 ? "-" : F(r.startAfter - Math.Max(0, r.revivedAt), "0.0") + " s after the rebuild")}, build time {F(r.spanAfter, "0.0")} s, first out {(r.firstOutAfter < 0 ? "-" : F(r.firstOutAfter - r.startAfter, "0.00") + " s after its start")}" +
            $"{(r.rebuild ? "" : ", crane rebuilt it " + r.craneRebuilt)}{(r.error != null ? "; ERROR " + r.error : "")}";

        public static string Check(string settings, Func<string, string> mapOf, int seeds = -1, string[] levels = null)
        {
            seeds = seeds > 0 ? seeds : Seeds; levels ??= Levels;
            bool sw0 = ZWorld.RuinNoBuildDeviation, rb0 = ZWorld.FactoryAutoRebuild, rd0 = ZWorld.RuinReviveDefault;
            var sb = new StringBuilder();
            sb.AppendLine($"DV155 check: no build clock on a destroyed factory ({levels.Length} levels x {seeds} seeds, rule on and off){OzV11StrikeCount.RulesNote()}");
            var on = new List<Run>(); var off = new List<Run>();
            foreach (var lv in levels)
                for (int s = 1; s <= seeds; s++)
                {
                    var a = Match(settings, mapOf(lv), lv, s, true); var z = Match(settings, mapOf(lv), lv, s, false);
                    on.Add(a); off.Add(z); sb.AppendLine(Line(a)); sb.AppendLine(Line(z));
                }
            var noRebuild = Match(settings, mapOf(levels[0]), levels[0], 1, true, false); sb.AppendLine(Line(noRebuild));
            var again = Match(settings, mapOf(levels[0]), levels[0], 1, true); sb.AppendLine(Line(again));
            sb.AppendLine();
            var v = new List<string>();
            var good = on.Where(r => r.error == null).ToList();
            v.Add(OzV11Kit.Verdict(on.Concat(off).All(r => r.error == null) && noRebuild.error == null, $"no error in {on.Count + off.Count + 2} matches"));
            v.Add(OzV11Kit.Verdict(good.Count > 0 && good.All(r => r.outWhileRuin == 0 && r.ruinTicksBuilding == 0),
                $"R1 rule on, while a ruin: units out {good.Sum(r => r.outWhileRuin)}, ticks in production {good.Sum(r => r.ruinTicksBuilding)} (want 0 / 0); Zod: ticks in production {off.Sum(r => r.ruinTicksBuilding)} in {off.Count(r => r.ruinTicksBuilding > 0)}/{off.Count} runs"));
            v.Add(OzV11Kit.Verdict(good.All(r => r.revivedAt >= 0 && r.revivedAt - r.killAt >= 600 - 1e-6 && r.revivedAt - r.killAt <= 660 + 0.05),
                $"R2 rebuilt at Zod's time: {string.Join(", ", good.Select(r => r.revivedAt < 0 ? "never" : F(r.revivedAt - r.killAt, "0.0")))} s (600-660)"));
            v.Add(OzV11Kit.Verdict(good.All(r => r.outFirst2s == 0),
                $"R3 the first 2 s after the rebuild: units out {good.Sum(r => r.outFirst2s)} (want 0); Zod: {off.Sum(r => r.outFirst2s)} units in {off.Count(r => r.outFirst2s > 0)}/{off.Count} runs (the bug)"));
            v.Add(OzV11Kit.Verdict(good.Count > 0 && good.All(r => r.firstOutAfter >= 0 && r.startAfter >= 0 && r.firstOutAfter - r.startAfter >= r.spanAfter - ZConst.TICK - 1e-6),
                $"R4 the first unit after the rebuild takes its whole build time: {string.Join(", ", good.Select(r => r.firstOutAfter < 0 ? "none yet" : F(r.firstOutAfter - r.startAfter, "0.0") + "/" + F(r.spanAfter, "0.0") + " s"))}"));
            v.Add(OzV11Kit.Verdict(noRebuild.error == null && noRebuild.outWhileRuin == 0 && noRebuild.craneRebuilt,
                $"R5 FactoryAutoRebuild off ({levels[0]}): rebuilt by itself {(noRebuild.craneRebuilt ? "no, by the crane yes" : noRebuild.revivedAt >= 0 ? "YES" : "no, the crane did not")}, units out while a ruin {noRebuild.outWhileRuin}"));
            var first = on.FirstOrDefault();
            v.Add(OzV11Kit.Verdict(first != null && again.killAt == first.killAt && again.revivedAt == first.revivedAt && again.firstOutAfter == first.firstOutAfter,
                $"R6 deterministic: {levels[0]} seed 1 twice: destroyed {F(first?.killAt ?? -1)} / {F(again.killAt)} s, rebuilt {F(first?.revivedAt ?? -1)} / {F(again.revivedAt)} s"));
            string mend = MendKeepsBuild(settings, mapOf(levels[0]), levels[0]);
            v.Add(OzV11Kit.Verdict(mend == "", $"R8 a crane's mend of a damaged factory that still stands keeps its unit in progress and its queue{(mend == "" ? "" : ": " + mend)}"));
            v.Add(OzV11Kit.Verdict(ZWorld.RuinNoBuildDeviation == sw0 && ZWorld.FactoryAutoRebuild == rb0 && ZWorld.RuinReviveDefault == rd0, "R7 the switches back as they were"));
            foreach (var l in v) sb.AppendLine(l);
            sb.AppendLine($"{v.Count(l => l.StartsWith("PASS"))} PASS, {v.Count(l => l.StartsWith("FAIL"))} FAIL");
            return sb.ToString();
        }
    }
}
