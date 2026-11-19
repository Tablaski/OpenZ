using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit bridge_aim_further_r6: the counted check of DV210 "a shot at a bridge lands on its deck" (Sim/ZObject.BridgeDeckAim.cs), no Unity
    /// API (the kit's Mono harness runs it too). Two parts:
    /// <list type="number">
    /// <item>Geometry: every bridge of levels 2, 16, 17 and 18 (12 bridges, short and long), shooters on the road before either end (1-8 tiles),
    /// beside the deck (both sides, every 8 px along it), on the deck and at the corners, every depth the spread can pick: the aim point is on the
    /// deck's centre line, inside the bridge; from off the deck 32-48 px (4-6 m) past the near end and never past the middle; from beside it at
    /// least the shooter's own depth; from on the deck at least 16 px ahead of the shooter.</item>
    /// <item>Play on the plain simulation, as DV207's count does it (Editor/OzV15BridgeHealthCount.cs: the map's start units taken away, the
    /// bridge at full health, one attacker of the red team on the road 1-2 tiles before the bridge's north / west end, the player's attack order
    /// on it, DV207 on): every attacker (Tough, grunts with grenades, Pyro with Ctrl, light / medium tank, mech, heavy tank, MLRS, gun, howitzer,
    /// missile cannon, Mammoth, Bizon) on short and long bridges, DV210 off and on. Counted: shots, where each rocket / shell / grenade lands
    /// (how far past the near end, inside the bridge or not, how far from its shooter), the seconds until the bridge falls, the first shot (time
    /// and the shooter's spot), the hit points the attacker's own team lost, other objects hit.</item>
    /// </list>
    /// </summary>
    public static class OzV15BridgeAimCount
    {
        public const double LimitSec = 300;

        /// <summary>The bridges of the play part: short (L02 middle 5 x 4, L18 north-east 4 x 6) and long (L16 4 x 11, L18 canal 4 x 12).</summary>
        public static readonly (string level, int tx, int ty, string name)[] Bridges =
        {
            ("L02", 26, 44, "short 5 x 4 (L02 middle, desert)"),
            ("L18", 113, 26, "short 4 x 6 (L18 north-east)"),
            ("L16", 70, 69, "long 4 x 11 (L16 jungle planks)"),
            ("L18", 61, 54, "long 4 x 12 (L18 canal, starts destroyed)"),
        };

        /// <summary>DV207's attackers plus level 18's two big tanks.</summary>
        public static readonly (string label, string type, int amount, bool ctrl, bool nades)[] Attackers =
            OzV15BridgeHealthCount.Attackers.Concat(new[] { ("Mammoth", ZBigTanks.Mammoth, 1, false, false), ("Bizon", ZBigTanks.Bizon, 1, false, false) }).ToArray();

        /// <summary>The ones the owner named (rockets, tank shells) and DV207's MLRS: the spread / falls lines are counted on these.</summary>
        static readonly string[] Named = { "tough", "light", ZMTank.Type, "heavy", "missile_launcher" };

        public sealed class AimRun
        {
            public string bridge = "", attacker = "", sig = "", note = "";
            public int shots, landed, inside, ownHpLost, othersHit, hp0, hpLeft, fx = -1, fy = -1;
            public int minDepth = int.MaxValue, maxDepth = int.MinValue, minFromShooter = int.MaxValue, maxFromShooter;
            public bool killed, canAttack;
            public double firstShot = -1, killAt = -1;
            public readonly List<double> times = new List<double>();
            public readonly HashSet<int> depths = new HashSet<int>();
            public int Outside => landed - inside;
        }

        static void Line(StringBuilder sb, ref int fails, bool ok, string text) { sb.Append(ok ? "PASS  " : "FAIL  ").Append(text).Append('\n'); if (!ok) fails++; }
        static string Sec(double s) => s < 0 ? "-" : s.ToString("0.0");
        static int Cx(int t) => t * 16 + 8;

        // ------------------------------------------------------------------ the bridge's own frame (px)
        /// <summary>Depth of point (x, y) past the end of bridge b nearer to (sx, sy) (px, along the deck), its offset from the deck's centre
        /// line, inside the bridge's rectangle.</summary>
        public static (int depth, int across, bool inside) Frame(ZObject b, int sx, int sy, int x, int y)
        {
            bool vert = b.Type == "bridge_vert";
            int a0 = vert ? b.LocY : b.LocX, len = vert ? b.HeightPix : b.WidthPix, c0 = vert ? b.LocX : b.LocY, wid = vert ? b.WidthPix : b.HeightPix;
            int s = (vert ? sy : sx) - a0, a = (vert ? y : x) - a0, c = (vert ? x : y) - c0;
            bool fromStart = 2 * s <= len;
            return (fromStart ? a : len - a, c - (wid >> 1), a >= 0 && a < len && c >= 0 && c < wid);
        }

        // ------------------------------------------------------------------ part 1: geometry
        /// <summary>Every bridge of the level, every test shooter spot, every depth of the spread. (lines checked, faults, text).</summary>
        public static (int n, int bad, string text) Geometry(string settings, string mapJson, string level)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.NoUnits(mapJson), 1);
            int n = 0, bad = 0, bridges = 0; var sb = new StringBuilder(); string firstBad = null;
            var spread = new SortedSet<int>();
            foreach (var b in w.Objects.Where(o => o.IsBridge))
            {
                bridges++;
                bool vert = b.Type == "bridge_vert";
                int len = vert ? b.HeightPix : b.WidthPix, wid = vert ? b.WidthPix : b.HeightPix, half = len / 2;
                var spots = new List<(int s, int c, string kind)>();
                for (int k = 1; k <= 8; k++) { spots.Add((-16 * k + 8, wid / 2, "before")); spots.Add((len + 16 * k - 8, wid / 2, "before")); }   // on the road before either end
                for (int s = 0; s < len; s += 8) { spots.Add((s, -24, "beside")); spots.Add((s, wid + 24, "beside")); }                       // in the water beside the deck
                for (int s = 8; s < len; s += 8) spots.Add((s, wid / 2, "on"));                                                                 // on the deck
                spots.Add((-32, -32, "corner")); spots.Add((len + 32, wid + 32, "corner")); spots.Add((-32, wid + 32, "corner")); spots.Add((len + 32, -32, "corner"));
                foreach (var (s, c, kind) in spots)
                {
                    int sx = vert ? b.LocX + c : b.LocX + s, sy = vert ? b.LocY + s : b.LocY + c;
                    var (pDepth, _, _) = Frame(b, sx, sy, sx, sy);                                                 // the shooter's own depth
                    for (uint roll = 0; roll < 17; roll++)
                    {
                        ZObject.BridgeDeckPoint(b, sx, sy, roll, out int tx, out int ty);
                        var (d, ac, inside) = Frame(b, sx, sy, tx, ty);
                        bool ok = inside && ac == 0;
                        if (kind == "before" || kind == "corner") { ok &= d >= 32 && d <= 48 && d <= half; if (kind == "before" && len >= 96) spread.Add(d); }
                        else if (kind == "beside") ok &= d <= half && d >= Math.Min(Math.Max(pDepth, 40) - 8, half) && d >= 32;
                        else ok &= d - pDepth >= 16 && d <= len - 16;
                        n++;
                        if (!ok) { bad++; if (firstBad == null) firstBad = $"bridge #{b.Id} {b.Type} len {len}: {kind} shooter at depth {pDepth}, roll {roll}: aim depth {d}, across {ac}, inside {inside}"; }
                    }
                }
            }
            sb.Append($"{level}: {bridges} bridges, {n} aim points (shooter spots x 17 rolls): {bad} off the rule" + (firstBad != null ? "; first: " + firstBad : "") +
                      (spread.Count > 0 ? $"; depths picked from a spot before a 12 m+ bridge: {spread.Count} values {spread.Min}..{spread.Max} px" : ""));
            return (n, bad, sb.ToString());
        }

        // ------------------------------------------------------------------ part 2: play on the plain simulation
        static bool InRect(ZObject b, int tx, int ty, int pad) =>
            tx >= (b.LocX >> 4) - pad && tx < ((b.LocX + b.WidthPix) >> 4) + pad && ty >= (b.LocY >> 4) - pad && ty < ((b.LocY + b.HeightPix) >> 4) + pad;

        static long RectD2(ZObject b, int x, int y)
        {
            int px = Math.Max(b.LocX, Math.Min(x, b.LocX + b.WidthPix - 1)), py = Math.Max(b.LocY, Math.Min(y, b.LocY + b.HeightPix - 1));
            return (long)(px - x) * (px - x) + (long)(py - y) * (py - y);
        }

        /// <summary>DV207's spot (Editor/OzV15BridgeHealthCount.cs Spot, the same spiral): the road 1-2 tiles before the bridge's north / west
        /// end; a gun the first such spot within its reach of the centre.</summary>
        static bool Spot(ZWorld w, ZObject b, string type, int reach, out int sx, out int sy)
        {
            bool vert = b.Type == "bridge_vert", robot = ZBuildLists.IsRobot(type), gun = ZBuildLists.IsCannon(type);
            int ex = vert ? (b.LocX >> 4) + 1 : (b.LocX >> 4) - 3, ey = vert ? (b.LocY >> 4) - 3 : (b.LocY >> 4) + 1;
            for (int r = 0; r <= 14; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int tx = ex + dx, ty = ey + dy;
                        if (InRect(b, tx, ty, 1)) continue;
                        bool ok = robot ? w.Path.TilePassable(tx, ty, true)
                                        : w.Path.TilePassable(tx, ty, false) && w.Path.TilePassable(tx + 1, ty, false) && w.Path.TilePassable(tx, ty + 1, false) && w.Path.TilePassable(tx + 1, ty + 1, false) && !InRect(b, tx + 1, ty + 1, 1);
                        if (!ok) continue;
                        if (gun)
                        {
                            long cx = tx * 16 + 16 - b.CenterX, cy = ty * 16 + 16 - b.CenterY;
                            if (cx * cx + cy * cy > (long)(reach - 8) * (reach - 8)) continue;
                        }
                        sx = robot ? Cx(tx) : tx * 16 + 16; sy = robot ? Cx(ty) : ty * 16 + 16; return true;
                    }
            sx = sy = -1; return false;
        }

        /// <summary>One attacker against one bridge, DV207 on, DV210 as asked. The loop is DV207's Kill tick for tick (the same fingerprint when
        /// DV210 is off); it only reads more.</summary>
        public static AimRun Run(string settings, string mapJson, int btx, int bty, (string label, string type, int amount, bool ctrl, bool nades) a, bool dv210, int seed = 1)
        {
            OzV11Kit.TrySet(typeof(ZWorld), "BridgeHealthDeviation", true);
            OzV11Kit.TrySet(typeof(ZWorld), "BridgeDeckAimDeviation", dv210);
            var r = new AimRun { attacker = a.label };
            ZWorld w;
            try { w = OzV11Kit.World(settings, mapJson, seed); }
            catch (Exception ex) { r.note = "no world: " + ex.Message; return r; }
            var b = w.Objects.FirstOrDefault(o => o.IsBridge && o.LocX == btx * 16 && o.LocY == bty * 16);
            if (b == null) { r.bridge = "no bridge at (" + btx + "," + bty + ")"; return r; }
            r.bridge = "(" + btx + "," + bty + ")"; r.hp0 = b.Health;
            ZObject dummy = null;
            for (int ty = w.Map.height - 3; ty > 2 && dummy == null; ty--)
                for (int tx = w.Map.width - 3; tx > 2 && dummy == null; tx--)
                    if (w.Path.TilePassable(tx, ty, true) && RectD2(b, Cx(tx), Cx(ty)) > 400L * 400L) dummy = OzV11Kit.Dummy(w, Cx(tx), Cx(ty));
            int reach = w.S.Unit(a.type)?.attack_radius ?? 0;
            if (!Spot(w, b, a.type, reach, out int sx, out int sy)) { r.note = "no spot"; return r; }
            ZObject u;
            try { u = OzV11Kit.Place(w, a.type, sx, sy, Team.Red, a.amount); }
            catch (Exception ex) { r.note = "cannot place: " + ex.Message; return r; }
            if (u == null) { r.note = "cannot place"; return r; }
            if (a.nades) u.Grenades = w.S.grenades_per_box;
            var mine = new HashSet<int>(OzV11Kit.Squad(u).Select(o => o.Id));
            w.SendWaypoints(Team.Red, u, new[] { new ZWaypoint { mode = WpMode.Attack, refId = b.Id, x = b.CenterX, y = b.CenterY, forceFire = a.ctrl } });
            r.canAttack = u.CanAttackObject(b);
            var hp = new Dictionary<int, int>(); var hitOthers = new HashSet<int>();
            foreach (var o in w.Objects) hp[o.Id] = o.Health;
            ulong h = 1469598103934665603UL;
            int ticks = (int)Math.Round(LimitSec / ZConst.TICK);
            for (int i = 0; i < ticks; i++)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                {
                    if (!mine.Contains(e.objId) || e.otherId != b.Id || (e.type != ZEventType.MissileFired && e.type != ZEventType.Hitscan)) continue;
                    r.shots++; r.times.Add(w.Time);
                    var s = w.Get(e.objId); int fx = s != null ? s.CenterX : sx, fy = s != null ? s.CenterY : sy;
                    if (r.firstShot < 0) { r.firstShot = w.Time; r.fx = fx; r.fy = fy; }
                    if (e.type != ZEventType.MissileFired) continue;                                       // a Pyro's flame: no landing point
                    var (d, _, inside) = Frame(b, fx, fy, e.x, e.y);
                    int far = (int)Math.Round(Math.Sqrt((double)(e.x - fx) * (e.x - fx) + (double)(e.y - fy) * (e.y - fy)));
                    r.landed++; if (inside) r.inside++;
                    r.minDepth = Math.Min(r.minDepth, d); r.maxDepth = Math.Max(r.maxDepth, d); r.depths.Add(d);
                    r.minFromShooter = Math.Min(r.minFromShooter, far); r.maxFromShooter = Math.Max(r.maxFromShooter, far);
                }
                w.Events.Clear();
                foreach (var o in w.Objects)
                {
                    if (!hp.TryGetValue(o.Id, out int was)) { hp[o.Id] = o.Health; continue; }
                    if (o.Health < was && o != b)
                    {
                        if (o.Owner == Team.Red) r.ownHpLost += was - o.Health;
                        else hitOthers.Add(o.Id);
                    }
                    hp[o.Id] = o.Health;
                }
                if (i % 50 == 49) h = w.RulesFingerprint(h);
                if (b.Destroyed) { r.killed = true; r.killAt = w.Time; break; }
            }
            r.othersHit = hitOthers.Count;
            r.hpLeft = Math.Max(0, b.Health);
            r.sig = h.ToString("x16");
            return r;
        }

        static string Text(string level, string name, AimRun r, bool on) =>
            $"{level} {name} | {r.attacker} | DV210 {(on ? "ON " : "off")} | can attack {r.canAttack} | shots {r.shots} | " +
            (r.killed ? $"DOWN at {Sec(r.killAt)} s" : $"standing after {LimitSec:0} s, hp left {r.hpLeft} ({100.0 * r.hpLeft / Math.Max(1, r.hp0):0} %)") +
            (r.landed > 0 ? $" | landed past the near end {r.minDepth}..{r.maxDepth} px ({r.minDepth / 8.0:0.0}..{r.maxDepth / 8.0:0.0} m), outside the bridge {r.Outside} of {r.landed}, {r.depths.Count} depths, from the shooter {r.minFromShooter / 8.0:0.0}..{r.maxFromShooter / 8.0:0.0} m" : "") +
            $" | first shot {Sec(r.firstShot)} s from ({r.fx},{r.fy}) | own team HP lost {r.ownHpLost} | others hit {r.othersHit}" + (r.note.Length > 0 ? " | " + r.note : "") + " | " + r.sig;

        /// <summary>The whole table: every bridge x every attacker, DV210 off and on (the kit's proof files).</summary>
        public static string Table(string settings, Func<string, string> map, Func<string, bool> only = null)
        {
            var sb = new StringBuilder();
            bool was = ZWorld.BridgeDeckAimDeviation, was207 = ZWorld.BridgeHealthDeviation;
            try
            {
                foreach (var br in Bridges)
                {
                    if (only != null && !only(br.level)) continue;
                    var mj = OzV15BridgeHealthCount.Prepared(map(br.level));
                    foreach (var a in Attackers)
                    {
                        sb.Append(Text(br.level, br.name, Run(settings, mj, br.tx, br.ty, a, false), false)).Append('\n');
                        sb.Append(Text(br.level, br.name, Run(settings, mj, br.tx, br.ty, a, true), true)).Append('\n');
                    }
                }
            }
            finally { ZWorld.BridgeDeckAimDeviation = was; ZWorld.BridgeHealthDeviation = was207; }
            return sb.ToString();
        }

        /// <summary>The whole check (menu OpenZ/Checks/V1.5/Bridge aim onto the deck (no Play), the harness's 'check'). Lines PASS / FAIL.</summary>
        public static string Check(string settings, Func<string, string> map) => Check(settings, map, false);

        /// <summary>The parity row's short probe: level 2's geometry, its middle bridge with the MLRS and the Tough squad.</summary>
        public static string Quick(string settings, Func<string, string> map) => Check(settings, map, true);

        static string Check(string settings, Func<string, string> map, bool quick)
        {
            bool was = ZWorld.BridgeDeckAimDeviation, was207 = ZWorld.BridgeHealthDeviation;
            var sb = new StringBuilder(); int fails = 0;
            try
            {
                // 1 geometry
                foreach (var lv in quick ? new[] { "L02" } : new[] { "L02", "L16", "L17", "L18" })
                {
                    var g = Geometry(settings, map(lv), lv);
                    Line(sb, ref fails, g.n > 0 && g.bad == 0, "geometry " + g.text);
                }
                // 2 play
                var bridges = quick ? new[] { Bridges[0] } : Bridges;
                var atk = quick ? Attackers.Where(x => x.type == "tough" || x.type == "missile_launcher").ToArray() : Attackers;
                var rows = new List<(string where, (string label, string type, int amount, bool ctrl, bool nades) a, AimRun off, AimRun on, OzV15BridgeHealthCount.KillRun dv207)>();
                foreach (var br in bridges)
                {
                    var mj = OzV15BridgeHealthCount.Prepared(map(br.level));
                    foreach (var a in atk)
                    {
                        var off = Run(settings, mj, br.tx, br.ty, a, false);
                        ZWorld.BridgeDeckAimDeviation = false;
                        var k = OzV15BridgeHealthCount.Kill(settings, mj, br.tx, br.ty, a, true);
                        var on = Run(settings, mj, br.tx, br.ty, a, true);
                        rows.Add((br.level + " " + br.name, a, off, on, k));
                        sb.Append("      ").Append(Text(br.level, br.name, off, false)).Append('\n');
                        sb.Append("      ").Append(Text(br.level, br.name, on, true)).Append('\n');
                    }
                }
                var played = rows.Where(x => x.off.note.Length == 0 && x.off.canAttack).ToList();
                var shells = played.Where(x => x.a.type != "pyro").ToList();
                {
                    var bad = rows.Where(x => x.off.note.Length == 0 && (x.off.shots != x.dv207.shots || x.off.killAt != x.dv207.killAt || x.off.sig != x.dv207.sig)).ToList();
                    Line(sb, ref fails, bad.Count == 0 && rows.Count > 0, $"DV210 off = DV207's own count (Editor/OzV15BridgeHealthCount.cs Kill): {rows.Count - bad.Count} of {rows.Count} runs the same shots, fall time and fingerprint" +
                         (bad.Count > 0 ? $"; first different: {bad[0].where} {bad[0].a.label}: off {bad[0].off.shots} shots {bad[0].off.sig}, DV207 {bad[0].dv207.shots} shots {bad[0].dv207.sig}" : ""));
                }
                {
                    int landed = shells.Sum(x => x.on.landed), inside = shells.Sum(x => x.on.inside);
                    int minD = shells.Where(x => x.on.landed > 0).Select(x => x.on.minDepth).DefaultIfEmpty(-1).Min();
                    int minF = shells.Where(x => x.on.landed > 0).Select(x => x.on.minFromShooter).DefaultIfEmpty(-1).Min();
                    int offLanded = shells.Sum(x => x.off.landed), offOut = shells.Sum(x => x.off.Outside);
                    int offMinF = shells.Where(x => x.off.landed > 0).Select(x => x.off.minFromShooter).DefaultIfEmpty(-1).Min();
                    Line(sb, ref fails, landed > 0 && inside == landed && minD >= 8 && minF >= 32,   // 8 px: the rule's 32 px floor less a grenade's 24 px scatter; 32 px = 4 m
                        $"DV210 on: every rocket / shell / grenade lands inside the bridge, {minD / 8.0:0.0} m or more past its near end (1 m or more asked), {minF / 8.0:0.0} m or more from its shooter (4 m or more asked) ({inside} of {landed}); " +
                        $"off (before): {offOut} of {offLanded} outside the bridge, the nearest {offMinF / 8.0:0.0} m from its shooter");
                }
                {
                    var named = shells.Where(x => Named.Contains(x.a.type)).ToList();
                    var bad = named.Where(x => x.off.killed && (!x.on.killed || x.on.shots > x.off.shots)).ToList();
                    var mlrs = named.Where(x => x.a.type == "missile_launcher").ToList();
                    bool mlrsOk = mlrs.Count > 0 && mlrs.All(x => x.on.killed && x.on.shots <= 6);
                    Line(sb, ref fails, named.Count > 0 && bad.Count == 0 && mlrsOk,
                        "the bridge still falls (DV207 unchanged): " + string.Join("; ", named.Select(x => $"{x.where.Split(' ')[0]} {x.where.Split(' ')[1]} {x.a.label} {x.off.shots} -> {x.on.shots} shots ({Sec(x.off.killAt)} -> {Sec(x.on.killAt)} s)")) +
                        (bad.Count > 0 ? $"; MORE shots or standing: {bad[0].where} {bad[0].a.label}" : "") + (mlrsOk ? "; MLRS at most Z's 6" : "; MLRS over 6"));
                }
                {
                    var bad = played.Where(x => Math.Abs(x.on.firstShot - x.off.firstShot) > 1e-9 || x.on.fx != x.off.fx || x.on.fy != x.off.fy).ToList();
                    Line(sb, ref fails, played.Count > 0 && bad.Count == 0, $"range and stand unchanged: the first shot at the same time from the same spot, off and on ({played.Count - bad.Count} of {played.Count})" +
                         (bad.Count > 0 ? $"; first different: {bad[0].where} {bad[0].a.label} off {Sec(bad[0].off.firstShot)} s ({bad[0].off.fx},{bad[0].off.fy}), on {Sec(bad[0].on.firstShot)} s ({bad[0].on.fx},{bad[0].on.fy})" : ""));
                }
                {
                    var bad = played.Where(x => { int n = Math.Min(x.off.times.Count, x.on.times.Count); for (int i = 0; i < n; i++) if (Math.Abs(x.off.times[i] - x.on.times[i]) > 1e-9) return true; return false; }).ToList();
                    Line(sb, ref fails, played.Count > 0 && bad.Count == 0, $"no random draw, no new timing: every shot at the same time off and on until the bridge falls ({played.Count - bad.Count} of {played.Count})" +
                         (bad.Count > 0 ? $"; first different: {bad[0].where} {bad[0].a.label}" : ""));
                }
                {
                    int lost = played.Sum(x => x.on.ownHpLost + x.off.ownHpLost);
                    Line(sb, ref fails, lost == 0, $"no harm to the shooter's own team: {lost} hit points lost, off and on (no friendly fire, ZWorld.ProcessMissileDamage)");
                }
                {
                    var named = shells.Where(x => Named.Contains(x.a.type) && x.on.landed >= 4).ToList();
                    var bad = named.Where(x => x.on.maxDepth - x.on.minDepth < 8 || x.on.depths.Count * 2 < x.on.landed).ToList();
                    Line(sb, ref fails, named.Count > 0 && bad.Count == 0, $"spread along the deck, never one spot: {named.Count - bad.Count} of {named.Count} runs span 1 m or more ({named.Select(x => x.on.maxDepth - x.on.minDepth).DefaultIfEmpty(0).Min() / 8.0:0.0}..{named.Select(x => x.on.maxDepth - x.on.minDepth).DefaultIfEmpty(0).Max() / 8.0:0.0} m), at least one depth for every two shots" +
                         (bad.Count > 0 ? $"; first narrow: {bad[0].where} {bad[0].a.label} {bad[0].on.minDepth}..{bad[0].on.maxDepth} px, {bad[0].on.depths.Count} depths of {bad[0].on.landed}" : ""));
                }
                {
                    var mj = OzV15BridgeHealthCount.Prepared(map(Bridges[0].level)); var tough = Attackers[0];
                    var x1 = Run(settings, mj, Bridges[0].tx, Bridges[0].ty, tough, true); var x2 = Run(settings, mj, Bridges[0].tx, Bridges[0].ty, tough, true);
                    Line(sb, ref fails, x1.sig == x2.sig && x1.shots == x2.shots && x1.killAt == x2.killAt && x1.minDepth == x2.minDepth && x1.maxDepth == x2.maxDepth,
                        $"deterministic: the same run twice (Tough squad, {Bridges[0].name}, DV210 on): {x1.shots} / {x2.shots} shots, {x1.sig} / {x2.sig}");
                }
            }
            finally { ZWorld.BridgeDeckAimDeviation = was; ZWorld.BridgeHealthDeviation = was207; }
            sb.Insert(0, fails == 0 ? "DV210 bridge aim on the deck: every line PASS\n" : $"DV210 bridge aim on the deck: {fails} FAIL\n");
            return sb.ToString();
        }
    }
}
