using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit level18_containers: the counted check of DV201 (Sim/ZWorld.L18Containers.cs), headless, on level 18's own map file. The row's
    /// switch is set here (the rest as OzV11Kit.GameSwitches has them); nothing is left changed.
    ///  made     7 containers, none refused: neutral DV39-style wreck map items, Zod's map-object hit points, explosives only, not crushable, every
    ///           footprint tile shallow water that was open and is now closed; every older object keeps its id; switch off: none, every tile open.
    ///  places   no footprint tile on a lane or a bridge deck's keep-clear area; every bridge (standing or destroyed) 3 tiles or more off its span's side
    ///           and 5 past its ends (the crane's approach and repair area); 2 tiles
    ///           from every flag, building, cannon and start unit; never on land.
    ///  wades    no robot region split; the robots' fort-to-fort wade the same length; between every pair of the key points (flags, fort gates, the
    ///           water tiles round each container, a fixed sample of open tiles) the robots' way at most 1 tile longer; the Bizon's river crossing
    ///           the same length.
    ///  shot     a red heavy tank on the bank shoots a container down (player's attack order): its hit points fall shot by shot, the Death event,
    ///           the object gone, every footprint tile open again (the map's own class), no debris missile, a rifle squad cannot hurt it, a Tough
    ///           squad can; the line of fire over a standing container is barred like a rock's.
    ///  twice    the shot scene twice: the same seconds, shots, hit point trace and tiles; two computers 120 s with the row on: the same fingerprint.
    /// Menus: OpenZ/Checks/V1.5/L18 containers (no Play); the parity row DV201 runs <see cref="Run"/> (OpenZParity.V15L18Containers.cs).
    /// </summary>
    public static class OzV15L18ContainersCount
    {
        static void On(bool v) => OzV11Kit.TrySet(typeof(ZWorld), "L18ContainersDeviation", v);
        static bool Alive(ZObject o) => o != null && !o.Destroyed && o.KillAt < 0;

        /// <summary>Shortest ways in tiles from a by the game's own step rule (ZPathFinder.StepOk, the grids as they are now), every tile;
        /// double.MaxValue = none. Dijkstra on a binary heap (8 neighbours, 1 or the square root of 2 a step).</summary>
        internal static double[,] Ways(ZWorld w, (int x, int y) a, bool robot)
        {
            int W = w.Map.width, H = w.Map.height; var d = new double[W, H];
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) d[x, y] = double.MaxValue;
            var hk = new double[W * H * 8 + 8]; var hv = new int[hk.Length]; int n = 0;
            void Push(double k, int v)
            {
                int i = n++; hk[i] = k; hv[i] = v;
                while (i > 0) { int p = (i - 1) >> 1; if (hk[p] < hk[i] || (hk[p] == hk[i] && hv[p] <= hv[i])) break; (hk[p], hk[i]) = (hk[i], hk[p]); (hv[p], hv[i]) = (hv[i], hv[p]); i = p; }
            }
            d[a.x, a.y] = 0; Push(0, a.y * W + a.x);
            while (n > 0)
            {
                double c = hk[0]; int v = hv[0]; n--; hk[0] = hk[n]; hv[0] = hv[n];
                for (int i = 0; ;)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < n && (hk[l] < hk[m] || (hk[l] == hk[m] && hv[l] < hv[m]))) m = l;
                    if (r < n && (hk[r] < hk[m] || (hk[r] == hk[m] && hv[r] < hv[m]))) m = r;
                    if (m == i) break;
                    (hk[m], hk[i]) = (hk[i], hk[m]); (hv[m], hv[i]) = (hv[i], hv[m]); i = m;
                }
                int x = v % W, y = v / W;
                if (c > d[x, y]) continue;
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                    if (!w.Path.StepOk(x, y, nx, ny, robot, false)) continue;
                    double nc = c + (dx != 0 && dy != 0 ? 1.41421356 : 1.0);
                    if (nc < d[nx, ny]) { d[nx, ny] = nc; Push(nc, ny * W + nx); }
                }
            }
            return d;
        }

        internal static int Regions(ZWorld w, bool robot)
        {
            int W = w.Map.width, H = w.Map.height, n = 0; var seen = new bool[W, H]; var q = new Queue<(int, int)>();
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++)
            {
                if (seen[x, y] || !w.Path.TilePassable(x, y, robot)) continue;
                n++; seen[x, y] = true; q.Enqueue((x, y));
                while (q.Count > 0)
                {
                    var (cx, cy) = q.Dequeue();
                    for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = cx + dx, ny = cy + dy; if (nx < 0 || ny < 0 || nx >= W || ny >= H || seen[nx, ny]) continue;
                        if (!w.Path.StepOk(cx, cy, nx, ny, robot, false)) continue;
                        seen[nx, ny] = true; q.Enqueue((nx, ny));
                    }
                }
            }
            return n;
        }

        static IEnumerable<(int x, int y)> Foot(int k)
        {
            var t = ZWorld.L18ContainerTiles;
            for (int i = t[k * 4]; i < t[k * 4] + t[k * 4 + 2]; i++) for (int j = t[k * 4 + 1]; j < t[k * 4 + 1] + t[k * 4 + 3]; j++) yield return (i, j);
        }

        /// <summary>The key points of the wade check: every flag, the two fort gates, the open tiles 2 tiles off each container's sides and ends, and
        /// every 9th open tile of a fixed 9 x 9 lattice (deterministic).</summary>
        internal static List<(int x, int y, string what)> Keys(ZWorld w)
        {
            var k = new List<(int, int, string)>();
            foreach (var o in w.Objects.Where(o => o.Type == "flag")) k.Add((o.CenterX / 16, o.CenterY / 16, "flag#" + o.Id));
            foreach (var f in w.Objects.Where(o => o.IsFort)) { f.GetCreationMovePoint(out int gx, out int gy); k.Add((gx / 16, gy / 16, f.Owner + " gate")); }
            var t = ZWorld.L18ContainerTiles;
            for (int c = 0; c * 4 < t.Length; c++)
            {
                int tx = t[c * 4], ty = t[c * 4 + 1], cw = t[c * 4 + 2], ch = t[c * 4 + 3];
                foreach (var (x, y) in new[] { (tx + cw / 2, ty - 2), (tx + cw / 2, ty + ch + 1), (tx - 2, ty + ch / 2), (tx + cw + 1, ty + ch / 2), (tx - 2, ty - 2), (tx + cw + 1, ty + ch + 1) })
                    if (x >= 0 && y >= 0 && x < w.Map.width && y < w.Map.height && w.Path.TilePassable(x, y, true)) k.Add((x, y, ZWorld.L18ContainerIds[c] + " side"));
            }
            for (int y = 4; y < w.Map.height - 4; y += 9) for (int x = 4; x < w.Map.width - 4; x += 9) if (w.Path.TilePassable(x, y, true)) k.Add((x, y, "lattice"));
            return k;
        }

        /// <summary>DV201 places: what is wrong with the footprints of the tile table on the map as w0 has it (the row off): a tile in a lane's
        /// keep-clear area, a bridge (standing or destroyed) nearer than 3 tiles off its span's side or 5 past its ends, a flag, building, cannon or
        /// unit nearer than 2 tiles. Empty = right.</summary>
        internal static List<string> PlaceProblems(ZWorld w0)
        {
            int n = ZWorld.L18ContainerIds.Length;
            var bad = new List<string>();
            w0.Pathing.EnsureLanes();                                                     // the lanes of the map as it is without the containers
            for (int k = 0; k < n; k++)
            {
                var id = ZWorld.L18ContainerIds[k];
                foreach (var p in Foot(k))
                {
                    int px = p.x * 16 + 8, py = p.y * 16 + 8;
                    foreach (var l in w0.Pathing.Lanes)
                        if (px >= l.KX0 && px < l.KX1 && py >= l.KY0 && py < l.KY1) bad.Add($"{id} tile ({p.x},{p.y}) in a lane's keep-clear area");
                }
                var t = ZWorld.L18ContainerTiles;
                int fx0 = t[k * 4] * 16, fy0 = t[k * 4 + 1] * 16, fx1 = fx0 + t[k * 4 + 2] * 16, fy1 = fy0 + t[k * 4 + 3] * 16;
                foreach (var o in w0.Objects)
                {
                    if (o.Type == "rock" || (o.Destroyed && !o.IsBridge)) continue;          // a bridge counts destroyed too (level 18's long bridges start destroyed; a crane repairs them)
                    int gx = Math.Max(o.LocX - fx1, fx0 - (o.LocX + o.WidthPix)), gy = Math.Max(o.LocY - fy1, fy0 - (o.LocY + o.HeightPix));   // px, < 0 = overlap on that axis
                    int gap = Math.Max(gx, gy);
                    if (o.IsBridge)
                    {   // beside the span (overlapping its length): 3 tiles off the deck's side; beyond an end: 5 tiles (the crane's approach and repair area)
                        bool vert = o.HeightPix > o.WidthPix;
                        bool beside = vert ? gy < 0 : gx < 0;
                        int need = beside ? 3 * 16 : 5 * 16;
                        if (gap < need) bad.Add($"{id} {gap / 16.0:0.0} tiles from {(o.Destroyed ? "destroyed " : "")}bridge #{o.Id} at ({o.LocX / 16},{o.LocY / 16}) {o.WidthPix / 16} x {o.HeightPix / 16} ({(beside ? "beside its span: 3" : "past its end: 5")} wanted)");
                    }
                    else if (o.Kind != ZKind.MapItem && gap < 2 * 16) bad.Add($"{id} {gap / 16.0:0.0} tiles from {o.Type}#{o.Id} at ({o.LocX / 16},{o.LocY / 16})");
                    else if (o.Type == "flag" && gap < 2 * 16) bad.Add($"{id} {gap / 16.0:0.0} tiles from a flag");
                }
            }
            return bad;
        }

        /// <summary>DV201 wades: the robots' way between every pair of key points with the containers (w) and without (w0): the worst increase
        /// between flags, gates and the lattice, the worst from a point beside a container, and the pairs that lost their way. d0 = a cache of the
        /// ways without the containers per source (may be null).</summary>
        internal static (double worst, string worstAt, double local, string localAt, int lost, int pairs) WadeScore(ZWorld w, ZWorld w0, List<(int x, int y, string what)> keys, Dictionary<(int, int), double[,]> d0)
        {
            double worst = 0, worstLocal = 0; string worstAt = "-", worstLocalAt = "-"; int pairs = 0, lost = 0;
            foreach (var a in keys)
            {
                if (!w.Path.TilePassable(a.x, a.y, true)) continue;
                var d1 = Ways(w, (a.x, a.y), true);
                double[,] z;
                if (d0 == null || !d0.TryGetValue((a.x, a.y), out z)) { z = Ways(w0, (a.x, a.y), true); if (d0 != null) d0[(a.x, a.y)] = z; }
                foreach (var b in keys)
                {
                    if (b.x == a.x && b.y == a.y) continue;
                    if (z[b.x, b.y] > 1e9 || !w.Path.TilePassable(b.x, b.y, true)) continue;   // (a point beside one container may lie in its neighbour's footprint)
                    pairs++;
                    if (d1[b.x, b.y] > 1e9) { lost++; continue; }
                    double inc = d1[b.x, b.y] - z[b.x, b.y];
                    bool local = a.what.EndsWith(" side") || b.what.EndsWith(" side");   // a point right beside a container: the wade round it
                    if (local) { if (inc > worstLocal) { worstLocal = inc; worstLocalAt = $"{a.what} ({a.x},{a.y}) -> {b.what} ({b.x},{b.y}): {z[b.x, b.y]:0.0} -> {d1[b.x, b.y]:0.0}"; } }
                    else if (inc > worst) { worst = inc; worstAt = $"{a.what} ({a.x},{a.y}) -> {b.what} ({b.x},{b.y}): {z[b.x, b.y]:0.0} -> {d1[b.x, b.y]:0.0}"; }
                }
            }
            return (worst, worstAt, worstLocal, worstLocalAt, lost, pairs);
        }

        /// <summary>The shot scene: a red heavy tank on the east bank of the east canal shoots container N2 (the player's attack order) until it is
        /// destroyed or 240 s pass. Returns a line that must be the same twice.</summary>
        static string Shot(string settings, string map18, out bool down, out double sec, out int shots, out int hits, out bool open, out int debris, out bool deathEv, out bool gone)
        {
            On(true);
            var w = ZWorld.Create(settings, map18, 1, false);
            var c = w.L18Containers.FirstOrDefault(o => w.L18ContainerIndex(o) == 1);
            down = false; sec = 0; shots = 0; hits = 0; open = false; debris = 0; deathEv = false; gone = false;
            if (c == null) return "no N2";
            int tx = c.LocX / 16, ty = c.LocY / 16;
            var tank = OzV11Kit.Place(w, "heavy", (tx + 4) * 16 + 8, (ty + 1) * 16 + 8, Team.Red);
            OzV11Kit.PlayerAttack(w, tank, c);
            var trace = new StringBuilder(); int last = c.Health;
            for (int i = 0; i < 24000 && Alive(c); i++)
            {
                w.Tick(ZConst.TICK); sec += ZConst.TICK;
                foreach (var e in w.Events)
                {
                    if (e.type == ZEventType.MissileFired && e.objId == tank.Id) shots++;
                    if (e.type == ZEventType.MissileFired && e.objId == c.Id) debris++;
                    if (e.type == ZEventType.Death && e.objId == c.Id) deathEv = true;
                }
                w.Events.Clear();
                if (c.Health != last) { hits++; trace.Append(c.Health).Append(' '); last = c.Health; }
            }
            down = !Alive(c);
            for (int i = 0; i < 3; i++) { w.Tick(ZConst.TICK); foreach (var e in w.Events) { if (e.type == ZEventType.Death && e.objId == c.Id) deathEv = true; if (e.type == ZEventType.MissileFired && e.objId == c.Id) debris++; } w.Events.Clear(); }
            gone = w.Get(c.Id) == null;
            open = Foot(1).All(p => w.Path.TilePassable(p.x, p.y, true));
            var tiles = new StringBuilder(); foreach (var p in Foot(1)) tiles.Append(w.Path.TilePassable(p.x, p.y, true) ? '1' : '0');
            return $"{sec:0.00} s, {shots} shots, {hits} hits, hp {trace}, tiles {tiles}, tick {w.TickIndex}, tank hp {tank.Health}";
        }

        public static string Run(string settings, string map18, string part = "all")
        {
            var sb = new StringBuilder();
            void Check(bool ok, string what) => sb.AppendLine(OzV11Kit.Verdict(ok, what));
            void Info(string what) => sb.AppendLine("INFO  " + what);
            bool all = part == "all" || part == "DV201";
            bool was = ZWorld.L18ContainersDeviation;
            try
            {
                On(true);
                ZWorld L18(int seed = 1) => ZWorld.Create(settings, map18, seed, false);
                var w = L18();
                if (!w.L18Map) { Check(false, "the map is not level 18's (" + w.Map.file + ")"); return sb.ToString(); }
                On(false); var w0 = L18(); On(true);
                int n = ZWorld.L18ContainerIds.Length;
                // ---------------------------------------------------------------- made
                Check(w.L18Containers.Count == n && w.L18ContainersRefused == 0, $"DV201 made: {w.L18Containers.Count} of {n} containers, {w.L18ContainersRefused} refused");
                int hp = (int)(w.S.map_item_health * ZConst.MAX_UNIT_HEALTH);
                Check(w.L18Containers.All(o => o.Type == ZWorld.WreckType && o.Kind == ZKind.MapItem && o.Owner == Team.Neutral && o.MaxHealth == hp && o.Health == hp && o.IsDestroyableImpass && o.AttackedByExplosives && !o.VehicleWreck),
                      $"DV201 each a neutral wreck map item (the harbour wrecks' kind), {hp} HP (Zod's map object), destroyable impassable, explosives only, not a car wreck (never crushed)");
                int closed = 0, water = 0, open0 = 0, tiles = 0;
                for (int k = 0; k < n; k++) foreach (var p in Foot(k)) { tiles++; if (!w.Path.TilePassable(p.x, p.y, true)) closed++; if (w0.Map.ClassAt(p.x, p.y) == ZClass.Water) water++; if (w0.Path.TilePassable(p.x, p.y, true)) open0++; }
                Check(closed == tiles && water == tiles && open0 == tiles, $"DV201 footprints: {tiles} tiles, {water} shallow water, {open0} open without the row, {closed} closed with it");
                bool idsSame = w0.Objects.Count + n == w.Objects.Count;
                for (int i = 0; idsSame && i < w0.Objects.Count; i++) idsSame = w0.Objects[i].Id == w.Objects[i].Id && w0.Objects[i].Type == w.Objects[i].Type;
                Check(idsSame && w.Objects.Skip(w0.Objects.Count).All(o => o.Type == ZWorld.WreckType), $"DV201 objects at map load {w.Objects.Count} with the row, {w0.Objects.Count} without: every older object keeps its id and type, the containers come last");
                Check(w0.L18Containers.Count == 0 && w0.Objects.All(o => o.Type != ZWorld.WreckType), "DV201 switch off ('Zod exact'): no container, the water open");
                // ---------------------------------------------------------------- places
                var bad = PlaceProblems(w0);
                foreach (var b in bad.Take(12)) Info(b);
                Check(bad.Count == 0, $"DV201 places: no tile in a lane's keep-clear area, every bridge (standing or destroyed) 3+ tiles off its span's side and 5+ past its ends (the crane's repair area), 2+ tiles from every flag, building, cannon and unit ({bad.Count} found)");
                // ---------------------------------------------------------------- wades
                int r0 = Regions(w0, true), r1 = Regions(w, true);
                Check(r0 == r1, $"DV201 robot regions over the grid (8-way step rule): {r1} with the containers, {r0} without (none split)");
                var blue = w0.Objects.First(o => o.IsFort && o.Owner == Team.Blue); var red = w0.Objects.First(o => o.IsFort && o.Owner == Team.Red);
                blue.GetCreationMovePoint(out int bgx, out int bgy); red.GetCreationMovePoint(out int rgx, out int rgy);
                var gw1 = Ways(w, (bgx / 16, bgy / 16), true); var gw0 = Ways(w0, (bgx / 16, bgy / 16), true);
                double g1 = gw1[rgx / 16, rgy / 16], g0 = gw0[rgx / 16, rgy / 16];
                Check(g1 < 1e9 && Math.Abs(g1 - g0) < 0.01, $"DV201 the robots' wade from the blue gate to the red gate: {g1:0.0} tiles with the containers, {g0:0.0} without (the same)");
                var keys = Keys(w0);
                var (worst, worstAt, worstLocal, worstLocalAt, lost, pairs) = WadeScore(w, w0, keys, null);
                Check(lost == 0 && worst <= 1.0 + 1e-6, $"DV201 robots' ways between the {keys.Count(k => !k.what.EndsWith(" side"))} key points (flags, fort gates, a 9-tile lattice; the harbour wrecks' rule): none lost ({lost} of {pairs} pairs incl. the points beside the containers), the worst +{worst:0.00} tiles ({worstAt}); rule: at most +1");
                Check(worstLocal <= 4.5 + 1e-6, $"DV201 the wade round a container from a point right beside it (2 tiles off its sides and ends): the worst +{worstLocal:0.00} tiles ({worstLocalAt}); rule: at most +4.5 (as round the river stumps, DV197)");
                using (w.Path.Kind(ZPathFinder.GridAmphib))
                using (w0.Path.Kind(ZPathFinder.GridAmphib))
                {
                    var bz1 = Ways(w, (56, 78), false); var bz0 = Ways(w0, (56, 78), false);
                    double a1 = bz1[56, 50], a0 = bz0[56, 50];
                    Check(Math.Abs(a1 - a0) < 0.01, $"DV201 the Bizon's own grid across the river, red fort's north side (56,78) to (56,50): {(a1 > 1e9 ? "none" : a1.ToString("0.0"))} tiles with the containers, {(a0 > 1e9 ? "none" : a0.ToString("0.0"))} without");
                }
                // ---------------------------------------------------------------- shot
                string s1 = Shot(settings, map18, out bool down, out double sec, out int shots, out int hits, out bool open, out int debris, out bool deathEv, out bool gone);
                Info("shot scene 1: " + s1);
                Check(down && deathEv && gone && open && hits >= 1 && debris == 0,
                      $"DV201 shot down: a red heavy tank on the bank destroys container N2 in {sec:0.0} s with {shots} shots ({hits} hits); the Death event {deathEv}, the object gone {gone}, its 3 tiles open again {open}, debris missiles {debris} (0: a wreck throws none; the view breaks it up)");
                {
                    var ws = L18(); var c = ws.L18Containers.First(o => ws.L18ContainerIndex(o) == 1); int tx = c.LocX / 16, ty = c.LocY / 16;
                    var grunts = OzV11Kit.Place(ws, "grunt", (tx + 3) * 16 + 8, (ty + 1) * 16 + 8, Team.Red);
                    bool canG = grunts.CanAttackObject(c);
                    OzV11Kit.PlayerAttack(ws, grunts, c);
                    for (int i = 0; i < 3000; i++) { ws.Tick(ZConst.TICK); ws.Events.Clear(); }
                    Check(!canG && c.Health == c.MaxHealth, $"DV201 rifles cannot hurt it: a grunt squad's attack allowed {canG}, its hit points after 30 s {c.Health} of {c.MaxHealth}");
                    var wt = L18(); var c2 = wt.L18Containers.First(o => wt.L18ContainerIndex(o) == 1);
                    var tough = OzV11Kit.Place(wt, "tough", (tx + 3) * 16 + 8, (ty + 1) * 16 + 8, Team.Red);
                    OzV11Kit.PlayerAttack(wt, tough, c2); double ts = 0;
                    for (int i = 0; i < 30000 && Alive(c2); i++) { wt.Tick(ZConst.TICK); wt.Events.Clear(); ts += ZConst.TICK; }
                    Check(!Alive(c2), $"DV201 a Tough squad (bazookas) on the bank destroys it: {(Alive(c2) ? "NOT in 300 s, hp " + c2.Health : ts.ToString("0.0") + " s")}");
                    var cb = w.L18Containers[0]; int ccx = cb.CenterX, ccy = cb.CenterY;
                    bool barred = w.EngageBarrierBetween(ccx - 48, ccy, ccx + 48, ccy) || w.EngageBarrierBetween(ccx, ccy - 48, ccx, ccy + 48);
                    Check(barred, $"DV201 a line of fire across a standing container is barred like a rock's (EngageBarrierBetween over {ZWorld.L18ContainerIds[0]})");
                }
                // ---------------------------------------------------------------- twice
                string s2 = Shot(settings, map18, out _, out _, out _, out _, out _, out _, out _, out _);
                Check(s1 == s2, "DV201 the shot scene twice: the same seconds, shots, hit point trace, tiles and tick (" + (s2.Length > 120 ? s2.Substring(0, 120) + " ..." : s2) + ")");
                if (part == "all")
                {
                    var runs = new List<string>();
                    for (int k = 0; k < 2; k++)
                    {
                        var wb = L18(3); wb.Bots.Add(new ZBot(wb, Team.Blue, 104)); wb.Bots.Add(new ZBot(wb, Team.Red, 3));
                        ulong h = 1469598103934665603UL;
                        for (int i = 0; i < 12000 && !wb.GameOver; i++) { wb.Tick(ZConst.TICK); wb.Events.Clear(); if (i % 100 == 99) h = wb.RulesFingerprint(h); }
                        runs.Add($"{h:x16} {wb.Objects.Count} objects, containers standing {wb.L18Containers.Count(Alive)}");
                    }
                    Check(runs[0] == runs[1], "DV201 two computers 120 s on level 18 with the row on, twice: " + runs[0] + (runs[0] == runs[1] ? " (the same)" : " | " + runs[1]));
                }
            }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace?.Split('\n').FirstOrDefault()); }
            finally { ZWorld.L18ContainersDeviation = was; }
            return sb.ToString();
        }
    }
}
