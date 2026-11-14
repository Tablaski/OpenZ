// OpenZ (GPL-3.0) - version 1.3, level 7: the counted check of the lava orders (rule DV152, Sim/ZWorld.Lava.cs).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Version 1.3, level 7 "Pyro Technics", the owner's lava orders: a click on lava always sends the robots in, a robot in lava sent on goes the short
    /// way, a robot left in lava walks to the shore by itself after 5 s. Headless simulation on the real level 7 grid without its start units, the rule
    /// on and the movement rules of a Play session on (calm move and follow, rock round, overtake, order fallback, Shift append, the steady and grab rules).
    ///   G1 lava tiles with no dry spot within LavaSearchTiles tiles and a clean straight line: 0
    ///   I1 one squad sent onto every InStep-th lava tile from 4 sides, 7 types: every member in, every order (100 %)
    ///   I3 three squads sent onto one tile together, every 11th tile, grunt and tough: every member in, every order
    ///   O1 a squad in lava sent to dry ground past it, every OutStep-th tile, grunt, psycho, tough: the long way round (took more than 1.5 x the best time
    ///      + 2 s) in 2 % of the runs or fewer; O2 the time taken / the best time, 90 % of the runs at 1.1 or less
    ///   the named cases, on the two-tile river at columns 27 and 28, rows 50 to 55, for each of the 7 types:
    ///   C1 a click on the far half of the river from the near bank: all in within 8 s, nobody walks more than 8 tiles
    ///   C2 Shift: a dry point, then lava: all in.  C3 Shift: lava, then the far bank: all in lava, at the goal within 12 s.  C5 16 clicks 0.25 s apart: all in
    ///   C6 left in lava with no order: each member walks out 5 s after its own last order (within 2 ticks), all out
    ///   C7 Stop in lava: the walk out starts 5 s after the Stop (within 2 ticks).  C8 hold position in lava: nobody walks out by itself
    ///   C9 an enemy in range while it stands in lava: it fires and stays.  C10 in the near half, sent to ground past the far bank: it leaves forward, the
    ///      leader at the goal within 12 s.  C11 in lava, attack a target past the far bank: it leaves forward.  C12 the leader dies: the others go in
    ///   C13 three squads on one tile: all in.  C16 three squads in on one tile and left there, touching: each robot walks out 5 s after its own last order
    ///   C14 the rule off: nobody steps in.  C15 the computer's robot ordered onto lava: it never steps in
    /// Short (two lines of OzL07LavaCount, so the parity row DV152 measures it): L1 = C1, L2 = C6, grunt.
    /// Menus: OpenZ/L07 Lava/Checks/Count lava orders (every 7th tile, a few minutes) and its quick form (every 21st tile). Last line "PASS n/n".
    /// </summary>
    public static class OzV13LavaOrdersCount
    {
        public const string Level = "L07";
        public const double InSeconds = 45, OutSeconds = 60, C1Seconds = 8, C1Tiles = 8, GoalSeconds = 12, LongWayShare = 0.02, TimeRatioP90 = 1.1;
        public struct Line { public bool ok; public string text; public Line(bool ok, string text) { this.ok = ok; this.text = text; } }
        static readonly string[] Types = { "grunt", "psycho", "tough", "sniper", "pyro", "laser", "electrobot" };
        static readonly string[] PlayRules = { "CalmMoveDeviation", "CalmFollowDeviation", "RockRoundDeviation", "OvertakeDeviation", "OrderMoveFallbackDeviation", "ShiftAppendDeviation",
                                               "NoAutoFlagUnderFireDeviation", "SniperSteadyDeviation", "ToughSteadyDeviation", "GrabCertainDeviation" };
        const int RivL = 27, RivR = 28, RivTop = 50, RivBot = 55, Row = 52, WestX = 24, EastX = 31;

        static int Px(int t) => t * 16 + 8;
        static double D(int ax, int ay, int bx, int by) => Math.Sqrt((double)(ax - bx) * (ax - bx) + (double)(ay - by) * (ay - by));

        // ------------------------------------------------------------------ setup
        sealed class Ctx
        {
            public string settings, noUnits; public ZWorld g; public int W, H, main; public bool[] lava; public int[] comp;
            public ZWorld World(int seed)
            {
                var w = OzV11Kit.World(settings, noUnits, seed);
                OzV11Kit.Dummy(w, 16 * 4, 16 * 4);
                var d = w.CreateRobotGroup("grunt", 16 * 4 - 8, (w.Map.height - 5) * 16 - 8, Team.Red, 1); d.MoveSpeed = 0; d.Damage = 0; d.MaxHealth = d.Health = 1000000000;
                return w;
            }
        }

        static Ctx Setup(string settings, string mapJson)
        {
            ZWorld.LavaDeviation = true;
            foreach (var n in PlayRules) OzV11Kit.TrySet(typeof(ZWorld), n, true);
            var c = new Ctx { settings = settings, noUnits = OzV11Kit.NoUnits(mapJson) };
            c.g = c.World(1); c.W = c.g.Map.width; c.H = c.g.Map.height; c.lava = new bool[c.W * c.H]; c.comp = new int[c.W * c.H];
            var sizes = new Dictionary<int, int>();
            for (int y = 0; y < c.H; y++) for (int x = 0; x < c.W; x++)
                {
                    int i = y * c.W + x; c.lava[i] = c.g.Map.LavaAt(x, y); c.comp[i] = c.g.Pathing.Reach.Comp(x, y, true, false);
                    if (c.comp[i] >= 0) sizes[c.comp[i]] = (sizes.TryGetValue(c.comp[i], out int k) ? k : 0) + 1;
                }
            c.main = sizes.OrderByDescending(k => k.Value).ThenBy(k => k.Key).First().Key;
            return c;
        }

        static void Tick(ZWorld w) { w.Tick(ZConst.TICK); w.Events.Clear(); }

        /// <summary>The river of the named cases is there: lava at columns 27 and 28 on rows 50 to 55, dry main ground at columns 24 to 26 and 29 to 31 on row 52.</summary>
        static bool RiverThere(Ctx c)
        {
            for (int y = RivTop; y <= RivBot; y++) for (int x = RivL; x <= RivR; x++) if (!c.lava[y * c.W + x]) return false;
            for (int x = WestX; x <= EastX; x++) if ((x < RivL || x > RivR) && (c.lava[Row * c.W + x] || c.comp[Row * c.W + x] != c.main)) return false;
            return true;
        }

        /// <summary>A dry main-ground tile rMin..rMax tiles from the lava tile on the side (qx, qy), with room for a squad.</summary>
        static (int x, int y)? StartNear(Ctx c, int tx, int ty, int qx, int qy, int rMin, int rMax)
        {
            for (int r = rMin; r <= rMax; r++)
                for (int k = -r; k <= r; k++)
                {
                    int x = tx + (qx != 0 ? qx * r : k), y = ty + (qy != 0 ? qy * r : k);
                    if (x < 1 || y < 1 || x >= c.W - 1 || y >= c.H - 1 || c.comp[y * c.W + x] != c.main || c.lava[y * c.W + x]) continue;
                    if (!c.g.Pathing.Reach.BoxFree(Px(x) - 16, Px(y) - 16, 32, 32, true, false)) continue;
                    return (x, y);
                }
            return null;
        }

        /// <summary>Walking px on the main ground from (sx, sy) to every tile (16 a step, 23 a diagonal), Dijkstra on a binary heap.</summary>
        static int[] Walk(Ctx c, int sx, int sy)
        {
            var d = new int[c.W * c.H]; for (int i = 0; i < d.Length; i++) d[i] = int.MaxValue;
            var heap = new List<long>(); int s = sy * c.W + sx; d[s] = 0; Push(heap, s);
            while (heap.Count > 0)
            {
                long top = Pop(heap); int cur = (int)(top & 0xFFFFFF), dc = (int)(top >> 24);
                if (dc != d[cur]) continue;
                int cx = cur % c.W, cy = cur / c.W;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy; if (nx < 0 || ny < 0 || nx >= c.W || ny >= c.H) continue;
                        int n = ny * c.W + nx; if (c.comp[n] != c.main || !c.g.Path.StepOk(cx, cy, nx, ny, true, false)) continue;
                        int nd = dc + (dx != 0 && dy != 0 ? 23 : 16);
                        if (nd < d[n]) { d[n] = nd; Push(heap, ((long)nd << 24) | (uint)n); }
                    }
            }
            return d;
            void Push(List<long> h, long v) { h.Add(v); int i = h.Count - 1; while (i > 0) { int p = (i - 1) / 2; if (h[p] <= h[i]) break; (h[p], h[i]) = (h[i], h[p]); i = p; } }
            long Pop(List<long> h) { long r = h[0]; h[0] = h[h.Count - 1]; h.RemoveAt(h.Count - 1); int i = 0; while (true) { int a = 2 * i + 1, b = a + 1, m = i; if (a < h.Count && h[a] < h[m]) m = a; if (b < h.Count && h[b] < h[m]) m = b; if (m == i) break; (h[m], h[i]) = (h[i], h[m]); i = m; } return r; }
        }

        // ------------------------------------------------------------------ the whole check
        public static string Run(string settings, string mapJson, int inStep = 7, int outStep = 7)
        {
            var sb = new StringBuilder(); int pass = 0, total = 0;
            void V(bool ok, string what) { total++; if (ok) pass++; sb.AppendLine(OzV11Kit.Verdict(ok, what)); }
            using (new OzV11Kit.Switches())
            {
                var c = Setup(settings, mapJson);
                var lt = new List<(int x, int y)>(); for (int y = 0; y < c.H; y++) for (int x = 0; x < c.W; x++) if (c.lava[y * c.W + x]) lt.Add((x, y));
                sb.AppendLine($"info  level 7: {c.W} x {c.H}, {lt.Count} lava tiles, the movement rules of a Play session on");
                // G1
                {
                    int none = 0;
                    foreach (var (x, y) in lt)
                    {
                        bool any = false;
                        for (int ty = y - ZWorld.LavaSearchTiles; ty <= y + ZWorld.LavaSearchTiles && !any; ty++)
                            for (int tx = x - ZWorld.LavaSearchTiles; tx <= x + ZWorld.LavaSearchTiles && !any; tx++)
                                if (tx >= 0 && ty >= 0 && tx < c.W && ty < c.H && c.g.Path.TilePassable(tx, ty, true) && !c.lava[ty * c.W + tx] && c.g.LavaLineClean(Px(tx), Px(ty), Px(x), Px(y))) any = true;
                        if (!any) none++;
                    }
                    V(none == 0, $"G1 lava tiles with no dry spot within {ZWorld.LavaSearchTiles} tiles and a clean line: {none} of {lt.Count}");
                }
                // I1, I3
                var i1 = Into(c, lt, inStep, Types, 1); V(i1.all == i1.runs && i1.runs > 0, $"I1 one squad onto every {inStep}th lava tile from 4 sides, 7 types: every member in {i1.all} of {i1.runs} orders (some {i1.some}, none {i1.none}); mean time until all in {i1.mean:F1} s{i1.fails}");
                var i3 = Into(c, lt, 11, new[] { "grunt", "tough" }, 3); V(i3.all == i3.runs && i3.runs > 0, $"I3 three squads onto one tile, every 11th tile, grunt and tough: every member in {i3.all} of {i3.runs}{i3.fails}");
                // O1, O2
                var o = OutOf(c, lt, outStep, new[] { "grunt", "psycho", "tough" });
                V(o.runs > 0 && o.longWay <= LongWayShare * o.runs, $"O1 in lava, sent past it, every {outStep}th tile, grunt, psycho, tough: the long way round {o.longWay} of {o.runs} ({100.0 * o.longWay / Math.Max(1, o.runs):F1} %, want {LongWayShare * 100:F0} % or less); not there in {OutSeconds:F0} s alive {o.stuck}");
                V(o.runs > 0 && o.p90 <= TimeRatioP90, $"O2 time taken / best time: median {o.median:F2}, 90 % under {o.p90:F2} (want {TimeRatioP90:F2} or less), worst {o.max:F2}");
                // the named cases
                if (!RiverThere(c)) V(false, "the river of the named cases (columns 27 and 28, rows 50 to 55) is not on the grid any more: the cases cannot run");
                else foreach (var t in Types) foreach (var l in Cases(c, t, false)) V(l.ok, l.text);
            }
            sb.AppendLine((pass == total ? "PASS " : "FAIL ") + pass + "/" + total);
            return sb.ToString();
        }

        /// <summary>Two lines for OzL07LavaCount (the parity row DV152): C1 and C6, grunt. Puts every switch back.</summary>
        public static List<Line> Short(string settings, string mapJson)
        {
            using (new OzV11Kit.Switches())
            {
                var c = Setup(settings, mapJson);
                if (!RiverThere(c)) return new List<Line> { new Line(false, "L1 the river of the lava order cases (columns 27 and 28, rows 50 to 55) is not on the grid any more") };
                return Cases(c, "grunt", true).Select((l, i) => new Line(l.ok, "L" + (i + 1) + l.text.Substring(2))).ToList();
            }
        }

        // ------------------------------------------------------------------ sweeps
        sealed class IntoResult { public int runs, all, some, none; public double mean; public string fails = ""; }

        static IntoResult Into(Ctx c, List<(int x, int y)> lt, int step, string[] types, int squads)
        {
            var r = new IntoResult(); double sum = 0; var fails = new List<string>();
            var q = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            for (int i = 0; i < lt.Count; i += step)
            {
                var (tx, ty) = lt[i];
                foreach (var (qx, qy) in q)
                {
                    var st = StartNear(c, tx, ty, qx, qy, 4, 9); if (st == null) continue;
                    foreach (var type in types)
                    {
                        bool free = true;
                        for (int k = 0; k < squads; k++) { int cx = Px(st.Value.x) + (k % 2) * 48 - 24 * (squads > 1 ? 1 : 0), cy = Px(st.Value.y) + (k / 2) * 48; if (!c.g.Pathing.Reach.BoxFree(cx - 16, cy - 16, 32, 32, true, false)) free = false; }
                        if (!free) continue;
                        var w = c.World(5 + r.runs); var leads = new List<ZObject>();
                        for (int k = 0; k < squads; k++) leads.Add(OzV11Kit.Place(w, type, Px(st.Value.x) + (k % 2) * 48 - 24 * (squads > 1 ? 1 : 0), Px(st.Value.y) + (k / 2) * 48, Team.Red));
                        var sq = leads.SelectMany(l => OzV11Kit.Squad(l)).ToList();
                        foreach (var l in leads) OzV11Kit.PlayerMove(w, l, Px(tx), Px(ty), true);
                        var entered = new HashSet<int>(); double end = w.Time + InSeconds, t0 = w.Time, allAt = -1;
                        while (w.Time < end)
                        {
                            Tick(w);
                            foreach (var m in sq) if (OzV11Kit.Alive(m) && w.InLavaNow(m)) entered.Add(m.Id);
                            if (entered.Count == sq.Count && allAt < 0) { allAt = w.Time; break; }
                        }
                        r.runs++;
                        if (entered.Count == sq.Count) { r.all++; sum += allAt - t0; }
                        else { if (entered.Count > 0) r.some++; else r.none++; if (fails.Count < 6) fails.Add($"{type} lava ({tx},{ty}) from ({st.Value.x},{st.Value.y}) {entered.Count}/{sq.Count}"); }
                    }
                }
            }
            r.mean = r.all > 0 ? sum / r.all : 0; if (fails.Count > 0) r.fails = "; missed: " + string.Join(", ", fails);
            return r;
        }

        sealed class OutResult { public int runs, longWay, stuck; public double median, p90, max; }

        static OutResult OutOf(Ctx c, List<(int x, int y)> lt, int step, string[] types)
        {
            var r = new OutResult(); var ratios = new List<double>(); var cache = new Dictionary<(int, int), int[]>();
            var q = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            for (int i = 0; i < lt.Count; i += step)
            {
                var (tx, ty) = lt[i];
                foreach (var (qx, qy) in q)
                {
                    var st = StartNear(c, tx, ty, qx, qy, 3, 7); if (st == null) continue;
                    var gl = StartNear(c, tx, ty, -qx, -qy, 7, 14); if (gl == null) continue;
                    if (!cache.TryGetValue(gl.Value, out var dg)) cache[gl.Value] = dg = Walk(c, gl.Value.x, gl.Value.y);
                    foreach (var type in types)
                    {
                        var w = c.World(5 + r.runs);
                        var lead = OzV11Kit.Place(w, type, Px(st.Value.x), Px(st.Value.y), Team.Red); var sq = OzV11Kit.Squad(lead).ToList();
                        OzV11Kit.PlayerMove(w, lead, Px(tx), Px(ty), true);
                        double e0 = w.Time + 25; bool got = false;
                        while (w.Time < e0) { Tick(w); if (OzV11Kit.Alive(lead) && w.InLavaNow(lead)) { got = true; break; } }
                        if (!got) continue;
                        double e1 = w.Time + 0.3; while (w.Time < e1) Tick(w);
                        if (!OzV11Kit.Alive(lead)) continue;
                        int gx = Px(gl.Value.x), gy = Px(gl.Value.y), sx0 = lead.CenterX, sy0 = lead.CenterY;
                        OzV11Kit.PlayerMove(w, lead, gx, gy, false);
                        double t0 = w.Time, end = w.Time + OutSeconds; bool arrived = false;
                        while (w.Time < end)
                        {
                            Tick(w);
                            if (sq.Where(OzV11Kit.Alive).All(m => D(m.CenterX, m.CenterY, gx, gy) <= 56)) { arrived = true; break; }
                            if (!sq.Any(OzV11Kit.Alive)) break;
                        }
                        r.runs++;
                        double ideal = 1e9;
                        for (int yy = Math.Max(0, (sy0 >> 4) - 8); yy <= Math.Min(c.H - 1, (sy0 >> 4) + 8); yy++)
                            for (int xx = Math.Max(0, (sx0 >> 4) - 8); xx <= Math.Min(c.W - 1, (sx0 >> 4) + 8); xx++)
                                if (c.comp[yy * c.W + xx] == c.main && !c.lava[yy * c.W + xx] && dg[yy * c.W + xx] != int.MaxValue && w.LavaLineClean(sx0, sy0, Px(xx), Px(yy)))
                                    ideal = Math.Min(ideal, D(sx0, sy0, Px(xx), Px(yy)) / 0.7 + dg[yy * c.W + xx]);
                        double speed = lead.MoveSpeed > 0 ? lead.MoveSpeed : 30, took = w.Time - t0, idealT = ideal / speed;
                        if (arrived) { ratios.Add(took / Math.Max(idealT, 0.5)); if (took > idealT * 1.5 + 2) r.longWay++; }
                        else if (sq.Any(OzV11Kit.Alive)) r.stuck++;
                    }
                }
            }
            ratios.Sort();
            if (ratios.Count > 0) { r.median = ratios[ratios.Count / 2]; r.p90 = ratios[ratios.Count * 9 / 10]; r.max = ratios[ratios.Count - 1]; }
            return r;
        }

        // ------------------------------------------------------------------ the named cases
        sealed class Rec
        {
            public ZWorld w; public List<ZObject> sq; public Dictionary<int, double> tIn = new Dictionary<int, double>(), walked = new Dictionary<int, double>();
            public Dictionary<int, bool> was; public List<(int x, int y)> exits = new List<(int, int)>(); readonly Dictionary<int, (int x, int y)> last;
            public Rec(ZWorld w, List<ZObject> sq) { this.w = w; this.sq = sq; was = sq.ToDictionary(m => m.Id, m => w.InLavaNow(m)); foreach (var m in sq) walked[m.Id] = 0; last = sq.ToDictionary(m => m.Id, m => (m.CenterX, m.CenterY)); }
            public void Step(double secs, Func<bool> stop = null, Action each = null)
            {
                double end = w.Time + secs;
                while (w.Time < end)
                {
                    Tick(w);
                    foreach (var m in sq)
                    {
                        if (!OzV11Kit.Alive(m)) continue;
                        bool now = w.InLavaNow(m);
                        if (now && !was[m.Id] && !tIn.ContainsKey(m.Id)) tIn[m.Id] = w.Time;
                        if (!now && was[m.Id]) exits.Add((m.CenterX >> 4, m.CenterY >> 4));
                        was[m.Id] = now; var q = last[m.Id]; walked[m.Id] += D(q.x, q.y, m.CenterX, m.CenterY); last[m.Id] = (m.CenterX, m.CenterY);
                    }
                    each?.Invoke();
                    if (stop != null && stop()) break;
                }
            }
            public bool AllIn => sq.All(m => tIn.ContainsKey(m.Id) || was[m.Id]);
            public int NowIn => sq.Count(m => OzV11Kit.Alive(m) && w.InLavaNow(m));
            public int Alive => sq.Count(OzV11Kit.Alive);
            public double MostTiles => walked.Values.Max() / 16;
        }

        /// <summary>The 5 s clock of a robot in lava, as the player sees it: from its first tick in lava with no order (no waypoint of the player, no walk out
        /// under way, no target, no hold) to the tick it walks out by itself (a walk out that is not the player's): stood. A robot that leaves the lava
        /// before that with no order (pushed over the edge, its leader's walk) is out early. Late: it stood in lava with no order longer than the 5 s and
        /// did not walk out (its time started again with no order, or it never left).</summary>
        sealed class IdleClock
        {
            readonly ZWorld w; readonly List<ZObject> sq; readonly Dictionary<int, double> idleFrom = new Dictionary<int, double>();
            public readonly Dictionary<int, double> stood = new Dictionary<int, double>(), early = new Dictionary<int, double>(), longest = new Dictionary<int, double>();
            public IdleClock(ZWorld w, List<ZObject> sq) { this.w = w; this.sq = sq; }
            public void Tick()
            {
                foreach (var m in sq)
                {
                    if (!OzV11Kit.Alive(m) || stood.ContainsKey(m.Id)) continue;
                    bool ordered = m.AttackObject != null || m.HoldPosition; foreach (var q in m.Waypoints) if (q.playerGiven) ordered = true;
                    bool walksOut = m.Waypoints.Count > 0 && m.Waypoints[0].refId == ZWorld.RefLavaOut && !m.Waypoints[0].playerGiven;
                    bool idle = idleFrom.TryGetValue(m.Id, out double from);
                    if (idle) longest[m.Id] = Math.Max(longest.TryGetValue(m.Id, out var x) ? x : 0, w.Time - from);
                    if (walksOut) { if (idle) stood[m.Id] = w.Time - from; idleFrom.Remove(m.Id); continue; }
                    if (ordered) { idleFrom.Remove(m.Id); continue; }
                    if (!w.InLavaNow(m)) { if (idle && !early.ContainsKey(m.Id)) early[m.Id] = w.Time - from; idleFrom.Remove(m.Id); continue; }
                    if (!idle) idleFrom[m.Id] = w.Time;
                }
            }
            bool Late(ZObject m) => longest.TryGetValue(m.Id, out var l) && l > ZWorld.LavaIdleSeconds + 2 * ZConst.TICK + 1e-6;
            public bool Fair(ZObject m) => !Late(m) && (stood.TryGetValue(m.Id, out var s) ? Math.Abs(s - ZWorld.LavaIdleSeconds) <= 2 * ZConst.TICK + 1e-6 : early.ContainsKey(m.Id));
            public bool Done(ZObject m) => !OzV11Kit.Alive(m) || stood.ContainsKey(m.Id) || early.ContainsKey(m.Id) && !w.InLavaNow(m);
            public string Of(ZObject m) => stood.TryGetValue(m.Id, out var s) ? s.ToString("F2") : early.TryGetValue(m.Id, out var e) ? "out " + e.ToString("F2") : Late(m) ? "late " + longest[m.Id].ToString("F2") : "-";
        }

        static ZObject Foe(ZWorld w, int tx, int ty) { var d = w.CreateRobotGroup("grunt", Px(tx) - 8, Px(ty) - 8, Team.Blue, 1); d.MoveSpeed = 0; d.Damage = 0; d.MaxHealth = d.Health = 1000000000; return d; }

        static List<Line> Cases(Ctx c, string type, bool shortOnly)
        {
            var o = new List<Line>(); void L(bool ok, string s) => o.Add(new Line(ok, s + " (" + type + ")"));
            ZObject Put(ZWorld w, int x, int y, Team team = Team.Red) => OzV11Kit.Place(w, type, Px(x), Px(y), team);
            void Move(ZWorld w, ZObject l, int x, int y) => OzV11Kit.PlayerMove(w, l, Px(x), Px(y), true);
            bool Past(int x) => x > RivR;
            { // C1
                var w = c.World(9); var l = Put(w, WestX, Row); var r = new Rec(w, OzV11Kit.Squad(l).ToList()); Move(w, l, RivR, Row);
                double t0 = w.Time; r.Step(40, () => r.AllIn); double t = w.Time - t0;
                L(r.AllIn && t <= C1Seconds && r.MostTiles <= C1Tiles, $"C1 a click on the far half of the river from the near bank: {(r.AllIn ? "all in" : r.tIn.Count + " of " + r.sq.Count + " in")} after {t:F1} s (want {C1Seconds:F0} s or less), walked {r.MostTiles:F0} tiles at most (want {C1Tiles:F0} or less)");
            }
            { // C6
                var w = c.World(9); var l = Put(w, WestX, Row); var sq = OzV11Kit.Squad(l).ToList(); var r = new Rec(w, sq); var clk = new IdleClock(w, sq); Move(w, l, RivL, Row);
                r.Step(40, () => r.AllIn && r.NowIn == 0 && sq.All(clk.Done), clk.Tick);
                bool ok = r.AllIn && sq.All(m => !OzV11Kit.Alive(m) || clk.Fair(m)) && r.NowIn == 0 && r.Alive > 0;
                L(ok, $"C6 left in lava with no order: each robot walked out {string.Join(" ", sq.Select(clk.Of))} s after its last order (want {ZWorld.LavaIdleSeconds:F2}, or out sooner with its squad), in lava now {r.NowIn}, alive {r.Alive} of {sq.Count}, hp {string.Join(" ", sq.Select(m => m.Health))} of {l.MaxHealth}");
            }
            if (shortOnly) return o;
            { // C2
                var w = c.World(9); var l = Put(w, WestX, Row); var r = new Rec(w, OzV11Kit.Squad(l).ToList());
                w.SendWaypoints(l.Owner, l, new[] { ZWaypoint.Move(Px(WestX), Px(Row - 5), true), ZWaypoint.Move(Px(RivR), Px(Row - 2), true) });
                double t0 = w.Time; r.Step(40, () => r.AllIn);
                L(r.AllIn, $"C2 Shift, a dry point then lava: {(r.AllIn ? "all in" : r.tIn.Count + " of " + r.sq.Count + " in")} after {w.Time - t0:F1} s");
            }
            { // C3
                var w = c.World(9); var l = Put(w, WestX, Row); var r = new Rec(w, OzV11Kit.Squad(l).ToList());
                w.SendWaypoints(l.Owner, l, new[] { ZWaypoint.Move(Px(RivL), Px(Row), true), ZWaypoint.Move(Px(EastX), Px(Row), true) });
                double t0 = w.Time; r.Step(60, () => r.sq.Where(OzV11Kit.Alive).All(m => Math.Abs(m.CenterX - Px(EastX)) + Math.Abs(m.CenterY - Px(Row)) < 48)); double t = w.Time - t0;
                L(r.tIn.Count == r.sq.Count && t <= GoalSeconds, $"C3 Shift, lava then the far bank: in lava {r.tIn.Count} of {r.sq.Count}, at the goal after {t:F1} s (want {GoalSeconds:F0} s or less), exits {string.Join(" ", r.exits.Select(p => $"({p.x},{p.y})"))}");
            }
            { // C5
                var w = c.World(9); var l = Put(w, WestX, Row); var r = new Rec(w, OzV11Kit.Squad(l).ToList());
                double t0 = w.Time; for (int i = 0; i < 16; i++) { Move(w, l, RivR, Row); r.Step(0.25); }
                r.Step(30, () => r.AllIn);
                L(r.AllIn, $"C5 16 clicks on one lava tile 0.25 s apart: {(r.AllIn ? "all in" : r.tIn.Count + " of " + r.sq.Count + " in")} after {w.Time - t0:F1} s");
            }
            { // C7
                var w = c.World(9); var l = Put(w, WestX, Row); var r = new Rec(w, OzV11Kit.Squad(l).ToList()); Move(w, l, RivL, Row);
                r.Step(25, () => l.Waypoints.Count == 0 && w.InLavaNow(l)); r.Step(3); w.SendStop(l.Owner, l); double stopAt = w.Time, leftAt = -1;
                r.Step(13, () => { if (leftAt < 0 && l.Waypoints.Count > 0) leftAt = w.Time; return r.NowIn == 0; });
                double s = leftAt < 0 ? -1 : leftAt - stopAt;
                L(leftAt >= 0 && Math.Abs(s - ZWorld.LavaIdleSeconds) <= 2 * ZConst.TICK + 1e-6, $"C7 Stop after 3 s in lava: the leader walks out {(leftAt >= 0 ? s.ToString("F2") + " s" : "never")} after the Stop (want {ZWorld.LavaIdleSeconds:F2})");
            }
            { // C8
                var w = c.World(9); var l = Put(w, WestX, Row); var r = new Rec(w, OzV11Kit.Squad(l).ToList()); Move(w, l, RivL, Row);
                r.Step(25, () => r.sq.All(m => m.Waypoints.Count == 0 && w.InLavaNow(m))); int outs0 = w.LavaIdleWalks; w.SendHold(l.Owner, l); r.Step(14);
                L(w.LavaIdleWalks == outs0, $"C8 hold position in lava: walks out by itself {w.LavaIdleWalks - outs0}, alive {r.Alive} of {r.sq.Count} (it stays, as ordered)");
            }
            { // C9
                var w = c.World(9); var l = Put(w, WestX, Row); var r = new Rec(w, OzV11Kit.Squad(l).ToList()); Move(w, l, RivL, Row);
                Foe(w, 33, Row);
                r.Step(25, () => l.Waypoints.Count == 0 && w.InLavaNow(l)); bool fired = false, startedOut = false, leftWhileFiring = false;
                r.Step(9, () =>
                {
                    if (l.AttackObject != null) fired = true;
                    if (!startedOut && l.Waypoints.Count > 0 && l.Waypoints[0].refId == ZWorld.RefLavaOut && !l.Waypoints[0].playerGiven) { startedOut = true; if (l.AttackObject != null) leftWhileFiring = true; }
                    return false;
                });
                L(!leftWhileFiring, $"C9 an enemy in range while it stands in lava: fired {fired}, walked out by itself while it had a target {leftWhileFiring}, walked out by itself at all {startedOut} (its range decides whether the enemy is in range)");
            }
            { // C10
                var w = c.World(9); var l = Put(w, WestX, Row); var r = new Rec(w, OzV11Kit.Squad(l).ToList()); Move(w, l, RivL, Row);
                r.Step(25, () => w.InLavaNow(l)); OzV11Kit.PlayerMove(w, l, Px(EastX + 3), Px(Row), false); double t0 = w.Time, at = -1;
                r.Step(60, () => { if (at < 0 && Math.Abs(l.CenterX - Px(EastX + 3)) + Math.Abs(l.CenterY - Px(Row)) < 48) at = w.Time; return at >= 0; });
                bool fwd = r.exits.Count > 0 && r.exits.All(p => Past(p.x));
                L(fwd && at >= 0 && at - t0 <= GoalSeconds, $"C10 in the near half, sent past the far bank: exits {string.Join(" ", r.exits.Select(p => $"({p.x},{p.y})"))} (want past column {RivR}), the leader at the goal after {(at >= 0 ? (at - t0).ToString("F1") + " s" : "never")} (want {GoalSeconds:F0} s or less)");
            }
            { // C11
                var w = c.World(9); var l = Put(w, WestX, Row); var r = new Rec(w, OzV11Kit.Squad(l).ToList()); Move(w, l, RivR, Row); var foe = Foe(w, EastX + 9, Row);
                r.Step(40, () => w.InLavaNow(l)); OzV11Kit.PlayerAttack(w, l, foe); double t0 = w.Time; r.Step(30, () => l.AttackObject == foe);
                bool fwd = r.exits.Count > 0 && r.exits.All(p => Past(p.x));
                L(fwd && l.AttackObject == foe, $"C11 in lava, attack a target past the far bank: exits {string.Join(" ", r.exits.Select(p => $"({p.x},{p.y})"))} (want past column {RivR}), in range after {w.Time - t0:F1} s");
            }
            { // C12
                var w = c.World(9); var l = Put(w, WestX, Row); var r = new Rec(w, OzV11Kit.Squad(l).ToList()); Move(w, l, RivR, Row);
                r.Step(25, () => w.InLavaNow(l)); l.Health = 1; r.Step(12, () => r.AllIn);
                L(r.AllIn, $"C12 the leader dies at its first bite: {r.tIn.Count} of {r.sq.Count} in");
            }
            { // C13
                var w = c.World(9); var leads = new List<ZObject>(); for (int k = 0; k < 3; k++) leads.Add(Put(w, WestX - 3, Row - 4 + 4 * k));
                var r = new Rec(w, leads.SelectMany(OzV11Kit.Squad).ToList()); foreach (var s in leads) Move(w, s, RivL, Row);
                double t0 = w.Time; r.Step(40, () => r.AllIn);
                L(r.AllIn, $"C13 three squads onto one tile: {(r.AllIn ? "all in" : r.tIn.Count + " of " + r.sq.Count + " in")} after {w.Time - t0:F1} s");
            }
            { // C16
                var w = c.World(9); var leads = new List<ZObject>(); for (int k = 0; k < 3; k++) leads.Add(Put(w, WestX - 3, Row - 4 + 4 * k));
                var sq = leads.SelectMany(OzV11Kit.Squad).ToList(); var r = new Rec(w, sq); var clk = new IdleClock(w, sq); foreach (var s in leads) Move(w, s, RivL, Row);
                r.Step(40, () => r.AllIn && r.NowIn == 0 && sq.All(clk.Done), clk.Tick);
                bool ok = r.AllIn && r.NowIn == 0 && clk.stood.Count > 0 && sq.All(m => !OzV11Kit.Alive(m) || clk.Fair(m));
                L(ok, $"C16 three squads in on one tile, left there: each robot walked out {string.Join(" ", sq.Select(clk.Of))} s after its last order (want {ZWorld.LavaIdleSeconds:F2}, or out sooner with its squad), in lava now {r.NowIn}, alive {r.Alive} of {sq.Count}");
            }
            { // C14
                ZWorld.LavaDeviation = false;
                var w = c.World(9); var l = Put(w, WestX, Row); var r = new Rec(w, OzV11Kit.Squad(l).ToList()); Move(w, l, RivL, Row); r.Step(20);
                ZWorld.LavaDeviation = true;
                L(r.tIn.Count == 0 && w.LavaOrders == 0, $"C14 the rule off: stepped in {r.tIn.Count}, lava orders {w.LavaOrders}");
            }
            { // C15
                var w = c.World(9); var l = Put(w, WestX, Row, Team.Blue); var r = new Rec(w, OzV11Kit.Squad(l).ToList()); Move(w, l, RivL, Row); r.Step(20);
                L(r.tIn.Count == 0 && w.LavaOrders == 0, $"C15 the computer's robot ordered onto lava: stepped in {r.tIn.Count}, lava orders {w.LavaOrders}");
            }
            return o;
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("OpenZ/L07 Lava/Checks/Count lava orders")]
        static void Menu() => OzV11Kit.RunMenu("L07 lava orders", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText(Level)));
        [UnityEditor.MenuItem("OpenZ/L07 Lava/Checks/Count lava orders, quick")]
        static void MenuQuick() => OzV11Kit.RunMenu("L07 lava orders, quick", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText(Level), 21, 21));
#endif
    }
}
