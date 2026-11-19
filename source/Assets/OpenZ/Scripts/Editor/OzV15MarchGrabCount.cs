using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit march_grab_r3: the counted check of DV203 (Sim/ZObject.MarchGrab.cs), no Unity API (the kit's Mono harness runs it too).
    /// Level 2 (Desert) with its terrain and buildings (nobody's), no start units, flags or crates: one red squad (the player's) walks one
    /// plain move order along the west north-south road (tile x 18-19, from tile y 33 to y 75, 42 tiles, no bridge) past a free flag
    /// 56 / 120 / 200 px east of the road's centre line, or an empty gatling 60 px east of it, or both (a flag 120 px, a gun 60 px).
    /// Counted per run: the robots that left the squad's way for the target (a waypoint of their own on it), who (leader / member), the
    /// distance where the run started, the time from there to the flag taken / the gun manned, the members that stayed on the road
    /// (never more than 48 px from its centre line), the squad's arrival at the order's end (within 48 px) and the runner's.
    /// The rule off = the game before 1.5 (DV84: a flag within 80 px takes the whole squad, a gun never); on = DV203.
    /// The calm start (DV69) is switched off for the check.
    /// </summary>
    public static class OzV15MarchGrabCount
    {
        public const int Runs = 5;
        internal static string Note = "";
        const double LateSec = 8;
        const int RoadX = 19 * 16, StartY = 33 * 16 + 8, GoalY = 75 * 16 + 8, OnRoadPx = 48, ArrivePx = 48;

        internal sealed class Target { public bool gun; public int x, y; }
        internal sealed class Row { public string squad, name; public Target[] t; }

        static Target Flag(int tx, int ty) => new Target { gun = false, x = tx, y = ty };
        static Target Gun(int px, int py) => new Target { gun = true, x = px, y = py };

        internal static List<Row> Rows()
        {
            var l = new List<Row>();
            foreach (var s in new[] { "sniper", "laser", "grunt" })
            {
                l.Add(new Row { squad = s, name = "free flag 200 px from the road", t = new[] { Flag(31, 55) } });   // (first: with the rule off nobody leaves, the squad's own march time)
                l.Add(new Row { squad = s, name = "free flag 120 px from the road", t = new[] { Flag(26, 55) } });
                l.Add(new Row { squad = s, name = "free flag 56 px from the road", t = new[] { Flag(22, 55) } });
                l.Add(new Row { squad = s, name = "empty gun 60 px from the road", t = new[] { Gun(RoadX + 60, 55 * 16 + 8) } });
            }
            l.Add(new Row { squad = "grunt", name = "free flag 120 px AND an empty gun 60 px further on", t = new[] { Flag(26, 53), Gun(RoadX + 60, 58 * 16 + 8) } });
            return l;
        }

        /// <summary>Level 2's map without units, flags, crates or owners, plus the row's flags (nobody's).</summary>
        static string Map(string l02, Target[] t)
        {
            var m = ZMapData.FromJson(l02);
            var sb = new StringBuilder("[");
            bool first = true;
            void Obj(int x, int y, string kind, int ot, int oid, int bl, int el, int hp)
            {
                sb.Append(first ? "" : ",").Append("{\"x\":" + x + ",\"y\":" + y + ",\"owner\":\"null\",\"kind\":\"" + kind + "\",\"ot\":" + ot + ",\"oid\":" + oid + ",\"blevel\":" + bl + ",\"extra_links\":" + el + ",\"health_percent\":" + hp + "}");
                first = false;
            }
            foreach (var o in m.objects)
            {
                string c = o.kind.Split(':')[0];
                if (c == "robot" || c == "vehicle" || c == "cannon" || o.kind == "map_item:flag" || o.kind == "map_item:grenades") continue;
                Obj(o.x, o.y, o.kind, o.ot, o.oid, o.blevel, o.extra_links, o.health_percent);
            }
            foreach (var x in t) if (!x.gun) Obj(x.x, x.y, "map_item:flag", 7, 0, -52, 0, 100);
            sb.Append("]");
            var mo = System.Text.RegularExpressions.Regex.Match(l02, @"""objects""\s*:\s*\[");
            if (!mo.Success) throw new InvalidOperationException("level 2 map: no objects array");
            int i = mo.Index + mo.Length - 1, depth = 0, j = i;
            for (; j < l02.Length; j++) { if (l02[j] == '[') depth++; else if (l02[j] == ']' && --depth == 0) break; }
            return l02.Substring(0, i) + sb + l02.Substring(j + 1);
        }

        internal sealed class One
        {
            public int size, left, leaders, stayed; public bool taken, allTaken; public double startPx = -1, toTarget = -1, squadArrive = -1, runnerArrive = -1;
            public string who = "-", sig = "";
        }

        internal static One RunOne(string settings, string l02, Row row, int seed, Action<ZWorld, List<ZObject>> trace = null)
        {
            var w = OzV11Kit.World(settings, Map(l02, row.t), seed);
            OzV11Kit.Dummy(w, 66 * 16, 80 * 16);
            var targets = new List<ZObject>();
            foreach (var t in row.t) targets.Add(t.gun ? OzV11Kit.Place(w, "gatling", t.x, t.y, Team.Neutral) : w.Flags.OrderBy(f => Math.Abs(f.CenterX - (t.x * 16 + 8)) + Math.Abs(f.CenterY - (t.y * 16 + 8))).ThenBy(f => f.Id).First());
            Note = string.Join(" ", w.Flags.Select(f => $"flag#{f.Id}@({f.CenterX},{f.CenterY}){f.Owner}")) + " targets " + string.Join(" ", targets.Select(x => $"{x.Type}#{x.Id}@({x.CenterX},{x.CenterY})"));
            var lead = OzV11Kit.Place(w, row.squad, RoadX, StartY, Team.Red);
            var members = OzV11Kit.Squad(lead).ToList();
            OzV11Kit.PlayerMove(w, lead, RoadX, GoalY);
            var r = new One { size = members.Count };
            var claim = new Dictionary<int, double>(); var claimD = new Dictionary<int, double>(); var arrive = new Dictionary<int, double>();
            var maxOff = members.ToDictionary(m => m.Id, m => 0);
            var takenAt = new double[targets.Count]; for (int k = 0; k < takenAt.Length; k++) takenAt[k] = -1;
            var leaver = new HashSet<int>();
            while (w.Time < 200)
            {
                w.Tick(ZConst.TICK); w.Events.Clear(); trace?.Invoke(w, members);
                for (int k = 0; k < targets.Count; k++) if (takenAt[k] < 0 && targets[k].Owner == Team.Red) takenAt[k] = w.Time;
                foreach (var m in members)
                {
                    bool onFoot = w.Objects.Contains(m) && OzV11Kit.Alive(m);
                    if (!onFoot) continue;
                    foreach (var tg in targets)
                        if (!claim.ContainsKey(m.Id) && m.Waypoints.Count > 0 && m.Waypoints[0].refId == tg.Id && !m.Waypoints[0].playerGiven) { claim[m.Id] = w.Time; claimD[m.Id] = OzV11Kit.Dist(m, tg); leaver.Add(m.Id); }
                    if (!claim.ContainsKey(m.Id)) maxOff[m.Id] = Math.Max(maxOff[m.Id], Math.Abs(m.CenterX - RoadX));
                    double dg = Math.Sqrt((double)(m.CenterX - RoadX) * (m.CenterX - RoadX) + (double)(m.CenterY - GoalY) * (m.CenterY - GoalY));
                    if (!arrive.ContainsKey(m.Id) && dg <= ArrivePx && m.Waypoints.Count == 0) arrive[m.Id] = w.Time;
                }
                bool targetsDone = takenAt.All(x => x >= 0);
                bool footDone = members.All(m => !(w.Objects.Contains(m) && OzV11Kit.Alive(m)) || arrive.ContainsKey(m.Id));
                if (footDone && (targetsDone || w.Time > 120)) break;
            }
            r.left = leaver.Count; r.allTaken = takenAt.All(x => x >= 0); r.taken = takenAt.Any(x => x >= 0);
            r.leaders = members.Count(m => m.Leader == null);
            r.stayed = members.Count(m => !leaver.Contains(m.Id) && maxOff[m.Id] <= OnRoadPx);
            if (leaver.Count > 0)
            {
                var first = members.Where(m => leaver.Contains(m.Id)).OrderBy(m => claim[m.Id]).ThenBy(m => m.Id).ToList();
                r.who = string.Join("+", first.Select(m => (m == lead ? "leader" : "member") + "@" + claim[m.Id].ToString("0.0") + "s"));
                r.startPx = claimD[first[0].Id];
                double tt = -1;
                for (int k = 0; k < targets.Count; k++) if (takenAt[k] >= 0) tt = Math.Max(tt, takenAt[k] - claim[first[0].Id]);
                r.toTarget = tt;
                var ra = members.Where(m => leaver.Contains(m.Id) && arrive.ContainsKey(m.Id)).Select(m => arrive[m.Id]).ToList();
                if (ra.Count > 0) r.runnerArrive = ra.Max();
            }
            var sa = members.Where(m => !leaver.Contains(m.Id) && arrive.ContainsKey(m.Id)).Select(m => arrive[m.Id]).ToList();
            if (sa.Count > 0) r.squadArrive = sa.Max();
            r.sig = $"{r.left}/{r.who}/{r.startPx:0.0}/{r.toTarget:0.00}/{r.stayed}/{r.squadArrive:0.00}/{r.runnerArrive:0.00}/{string.Join(",", takenAt.Select(x => x.ToString("0.00")))}";
            return r;
        }

        /// <summary>The whole check: every row 'runs' times with the rule off and on. Returns the report (PASS / FAIL lines last).</summary>
        public static string Run(string settings, string l02Map, int runs = Runs)
        {
            var sb = new StringBuilder();
            bool had = OzV11Kit.TrySet(typeof(ZWorld), "MarchGrabDeviation", false);
            bool calm = ZWorld.CalmStartDeviation; ZWorld.CalmStartDeviation = false;
            int badOneOn = 0, badTakeOn = 0, badRoadOn = 0, badRunnerOn = 0, badLateOn = 0, gunOff = 0, farOff = 0, rowsOn = 0; string sameTwice = ""; var baseBySquad = new Dictionary<string, double>();
            try
            {
                sb.AppendLine($"Level 2, one red squad on one plain move order along the west road (tile x 18-19, y 33 -> 75, 42 tiles), {runs} runs per row (seeds 1-{runs}), the calm start off.");
                sb.AppendLine("squad | target | rule | robots in the squad | robots that left the road per run | who (first claim at) | start px (mean) | s from the start to the flag taken / gun manned (mean / worst) | runs taken | robots that stayed on the road (mean) | squad at the order's end s (mean) | runner at the order's end s (mean)");
                foreach (var row in Rows())
                {
                    var off = new List<One>();
                    foreach (bool rule in new[] { false, true })
                    {
                        if (rule && !had) continue;
                        ZWorld.MarchGrabDeviation = rule;
                        var res = new List<One>();
                        for (int s = 1; s <= runs; s++) res.Add(RunOne(settings, l02Map, row, s));
                        ZWorld.MarchGrabDeviation = false;
                        if (!rule) off = res;
                        var lf = res.Where(x => x.left > 0).ToList();
                        string M(IEnumerable<double> v) { var a = v.Where(x => x >= 0).ToList(); return a.Count == 0 ? "-" : a.Average().ToString("0.0"); }
                        sb.AppendLine($"{row.squad} | {row.name} | {(rule ? "on" : "off")} | {res[0].size} | {string.Join(" ", res.Select(x => x.left))} | {(lf.Count > 0 ? lf[0].who : "nobody")} | {M(res.Select(x => x.startPx))} | {M(res.Select(x => x.toTarget))} / {(lf.Count > 0 ? lf.Max(x => x.toTarget).ToString("0.0") : "-")} | {res.Count(x => x.allTaken)} of {runs} | {res.Average(x => x.stayed):0.0} | {M(res.Select(x => x.squadArrive))} | {M(res.Select(x => x.runnerArrive))}");
                        bool gunRow = row.t.Any(t => t.gun), flagOnly = !gunRow, near = row.t.Length == 1 && !row.t[0].gun && row.t[0].x == 22;
                        if (!rule)
                        {
                            foreach (var x in res) if (x.left == 0 && x.squadArrive >= 0 && !gunRow) baseBySquad[row.squad] = Math.Max(baseBySquad.TryGetValue(row.squad, out var b0) ? b0 : -1, x.squadArrive);
                            if (gunRow) gunOff += res.Count(x => x.left > 0 && row.t.Length == 1);
                            if (flagOnly && !near) farOff += res.Count(x => x.left > 0);
                            continue;
                        }
                        rowsOn++;
                        int want = row.t.Length;
                        if (!baseBySquad.ContainsKey(row.squad)) baseBySquad[row.squad] = -1;
                        double baseArr = baseBySquad[row.squad];
                        foreach (var x in res)
                        {
                            if (x.left < 1 || x.left > Math.Min(want, x.size) || x.leaders > 1 + x.left) badOneOn++;
                            if (!x.allTaken) badTakeOn++;
                            if (x.stayed < x.size - x.left) badRoadOn++;
                            if (!gunRow && x.runnerArrive < 0) badRunnerOn++;
                            if (baseArr >= 0 && x.squadArrive >= 0 && x.squadArrive > baseArr + LateSec) badLateOn++;
                        }
                    }
                }
                if (had)
                {
                    var r0 = Rows()[5];                                                   // the laser squad and the 120 px flag, seed 1, twice (row 5 = laser, 120 px)
                    ZWorld.MarchGrabDeviation = true;
                    string a = RunOne(settings, l02Map, r0, 1).sig, b = RunOne(settings, l02Map, r0, 1).sig;
                    ZWorld.MarchGrabDeviation = false;
                    sameTwice = a == b ? "SAME" : "DIFFER (" + a + " | " + b + ")";
                }
            }
            finally { ZWorld.MarchGrabDeviation = false; ZWorld.CalmStartDeviation = calm; }
            if (!had) { sb.AppendLine("The rule is not installed (ZWorld.MarchGrabDeviation not found): only the 'off' rows were run."); return sb.ToString(); }
            sb.AppendLine(OzV11Kit.Verdict(badOneOn == 0, $"rule on: one robot per target at most, and at least one, leaves the road in every run (the one that left may take a second target on its own way) (runs with another count: {badOneOn})"));
            sb.AppendLine(OzV11Kit.Verdict(badTakeOn == 0, $"rule on: every flag taken / gun manned in every run (runs without: {badTakeOn})"));
            sb.AppendLine(OzV11Kit.Verdict(badRoadOn == 0, $"rule on: the rest of the squad stays on the road (within {OnRoadPx} px of its centre line) in every run (runs with a member off the road: {badRoadOn})"));
            sb.AppendLine(OzV11Kit.Verdict(badRunnerOn == 0, $"rule on: after a flag the runner walks on to the order's end (runs where it did not arrive: {badRunnerOn})"));
            sb.AppendLine(OzV11Kit.Verdict(badLateOn == 0, $"rule on: the rest of the squad arrives at most {LateSec:0} s after the same squad with nothing to take (when its leader is the one that leaves, a new leader plans his own way from behind) (runs later: {badLateOn})"));
            sb.AppendLine(OzV11Kit.Verdict(gunOff == 0 && farOff == 0, $"rule off = the game before 1.5 (DV84): no gun taken out of a march (runs: {gunOff}), no flag at 120 / 200 px taken (runs: {farOff})"));
            sb.AppendLine(OzV11Kit.Verdict(sameTwice == "SAME", $"deterministic: the laser squad's 120 px flag run, seed 1, rule on, twice the same ({sameTwice})"));
            return sb.ToString();
        }

        /// <summary>The parity row's probe: the same check with 2 runs per row.</summary>
        public static string Quick(string settings, string l02Map) => Run(settings, l02Map, 2);
    }
}
