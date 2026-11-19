using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit bridge_health_r3: the counted check of DV207 "bridges as weak as in Z" (Sim/ZWorld.BridgeHealth.cs), no Unity API (the kit's Mono
    /// harness runs it too). Every number is counted on the plain simulation with the game's switches: one bridge of each kind (short L02 / L17 /
    /// L18, long L16 / L18) at full health (the L17 / L18 ones that start destroyed are stood up for the count), the map's start units taken away,
    /// one attacker (a squad of the usual size, or one vehicle / gun) of the red team on the road beside one end of the bridge, given the
    /// player's attack order on it. Counted: the attacker's shots at the bridge, the seconds until the bridge falls (300 s at most), the hit
    /// points left. DV207 is set by name (OzV11Kit.TrySet), so <see cref="Table"/> also counts on a tree without the kit (DV207 then absent).
    /// </summary>
    public static class OzV15BridgeHealthCount
    {
        public const double LimitSec = 300;

        /// <summary>The bridges counted: level, tile of the map entry, the kind's plain name.</summary>
        public static readonly (string level, int tx, int ty, string name)[] Bridges =
        {
            ("L02", 26, 44, "short 5 x 4 (L02 middle)"),
            ("L02", 17, 26, "short 4 x 6 (L02 north)"),
            ("L17", 52, 94, "short 6 x 4 (L17, starts destroyed)"),
            ("L18", 113, 26, "short 4 x 6 (L18 north-east)"),
            ("L16", 70, 69, "long 4 x 11 (L16)"),
            ("L18", 61, 54, "long 4 x 12 (L18 canal, starts destroyed)"),
        };

        /// <summary>The attackers: label, unit type, squad size (-1 = the type's group_amount), Ctrl mark (DV113), grenades for the squad.</summary>
        public static readonly (string label, string type, int amount, bool ctrl, bool nades)[] Attackers =
        {
            ("Tough squad (2)", "tough", -1, false, false),
            ("Grunt squad with grenades", "grunt", -1, false, true),
            ("Pyro squad (4), Ctrl", "pyro", -1, true, false),
            ("Light tank", "light", 1, false, false),
            ("Medium tank", ZMTank.Type, 1, false, false),
            ("Mech (Zod 'medium')", "medium", 1, false, false),
            ("Heavy tank", "heavy", 1, false, false),
            ("MLRS (Mobile Missile)", "missile_launcher", 1, false, false),
            ("Gun", "gun", 1, false, false),
            ("Howitzer", "howitzer", 1, false, false),
            ("Missile cannon", "missile_cannon", 1, false, false),
        };

        /// <summary>The five the owner's row names (Tough, light tank, heavy tank, MLRS, howitzer): the PASS lines are counted on these.</summary>
        static readonly string[] Named = { "tough", "light", "heavy", "missile_launcher", "howitzer" };

        public sealed class KillRun
        {
            public string bridge, attacker; public int shots, hp0, hpLeft; public bool killed, canAttack; public double firstShot = -1, killAt = -1;
            public string sig = "";
            public double FromFirst => killed && firstShot >= 0 ? killAt - firstShot : -1;
        }

        static bool IsBridgeKind(string kind) => kind == "building:bridge_vert" || kind == "building:bridge_horz";

        /// <summary>The level's map without start units, every bridge at full health.</summary>
        public static string Prepared(string mapJson)
        {
            var m = ZMapData.FromJson(OzV11Kit.NoUnits(mapJson));
            foreach (var o in m.objects) if (IsBridgeKind(o.kind)) o.health_percent = 100;
            return UnityEngine.JsonUtility.ToJson(m);
        }

        static int Cx(int t) => t * 16 + 8;

        static long RectD2(ZObject b, int x, int y)
        {
            int px = Math.Max(b.LocX, Math.Min(x, b.LocX + b.WidthPix - 1)), py = Math.Max(b.LocY, Math.Min(y, b.LocY + b.HeightPix - 1));
            return (long)(px - x) * (px - x) + (long)(py - y) * (py - y);
        }

        static bool InRect(ZObject b, int tx, int ty, int pad) =>
            tx >= (b.LocX >> 4) - pad && tx < ((b.LocX + b.WidthPix) >> 4) + pad && ty >= (b.LocY >> 4) - pad && ty < ((b.LocY + b.HeightPix) >> 4) + pad;

        /// <summary>The attacker's tile: the road beside the bridge's first end (north / west), 1-2 tiles off the bridge, the first free spot in
        /// a spiral from there (2 x 2 tiles free for a vehicle or gun); a gun (it cannot move) the first such spot within its reach of the centre.</summary>
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

        static bool Shot(ZEvent e) => e.type == ZEventType.MissileFired || e.type == ZEventType.Hitscan;

        /// <summary>One attacker against one bridge. dv207: DV207's switch for this world (null = leave it as it is).</summary>
        public static KillRun Kill(string settings, string mapJson, int btx, int bty, (string label, string type, int amount, bool ctrl, bool nades) a, bool? dv207, int seed = 1)
        {
            if (dv207.HasValue) OzV11Kit.TrySet(typeof(ZWorld), "BridgeHealthDeviation", dv207.Value);
            var w = OzV11Kit.World(settings, mapJson, seed);
            var r = new KillRun { attacker = a.label };
            var b = w.Objects.FirstOrDefault(o => o.IsBridge && o.LocX == btx * 16 && o.LocY == bty * 16);
            if (b == null) { r.bridge = "no bridge at (" + btx + "," + bty + ")"; return r; }
            r.bridge = "(" + btx + "," + bty + ")"; r.hp0 = b.Health;
            ZObject dummy = null;
            for (int ty = w.Map.height - 3; ty > 2 && dummy == null; ty--)                         // a target-less blue grunt far away (the game is never over)
                for (int tx = w.Map.width - 3; tx > 2 && dummy == null; tx--)
                    if (w.Path.TilePassable(tx, ty, true) && RectD2(b, Cx(tx), Cx(ty)) > 400L * 400L) dummy = OzV11Kit.Dummy(w, Cx(tx), Cx(ty));
            int reach = w.S.Unit(a.type)?.attack_radius ?? 0;
            if (!Spot(w, b, a.type, reach, out int sx, out int sy)) { r.bridge += " no spot"; return r; }
            var u = OzV11Kit.Place(w, a.type, sx, sy, Team.Red, a.amount);
            if (a.nades) u.Grenades = w.S.grenades_per_box;
            var mine = new HashSet<int>(OzV11Kit.Squad(u).Select(o => o.Id));
            w.SendWaypoints(Team.Red, u, new[] { new ZWaypoint { mode = WpMode.Attack, refId = b.Id, x = b.CenterX, y = b.CenterY, forceFire = a.ctrl } });
            r.canAttack = u.CanAttackObject(b);
            ulong h = 1469598103934665603UL;
            int ticks = (int)Math.Round(LimitSec / ZConst.TICK);
            for (int i = 0; i < ticks; i++)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                    if (Shot(e) && mine.Contains(e.objId) && e.otherId == b.Id) { r.shots++; if (r.firstShot < 0) r.firstShot = w.Time; }
                w.Events.Clear();
                if (i % 50 == 49) h = w.RulesFingerprint(h);
                if (b.Destroyed) { r.killed = true; r.killAt = w.Time; break; }
            }
            r.hpLeft = Math.Max(0, b.Health);
            r.sig = h.ToString("x16");
            return r;
        }

        public static string Sec(double s) => s < 0 ? "-" : s.ToString("0.0");

        static string Line(string level, string name, KillRun r) =>
            $"{level} {name} | {r.attacker} | hp {r.hp0} | can attack {r.canAttack} | shots {r.shots} | first shot {Sec(r.firstShot)} s | " +
            (r.killed ? $"DOWN at {Sec(r.killAt)} s ({Sec(r.FromFirst)} s of fire)" : $"standing after {LimitSec:0} s, hp left {r.hpLeft} ({100.0 * r.hpLeft / Math.Max(1, r.hp0):0} %)") + " | " + r.sig;

        /// <summary>The whole table for one rule set (label), every bridge kind x every attacker.</summary>
        public static string Table(string settings, Func<string, string> map, string label, bool? dv207, Func<string, bool> only = null)
        {
            var sb = new StringBuilder();
            sb.Append("== ").Append(label).Append('\n');
            foreach (var br in Bridges)
            {
                if (only != null && !only(br.level)) continue;
                var mj = Prepared(map(br.level));
                foreach (var a in Attackers) sb.Append(Line(br.level, br.name, Kill(settings, mj, br.tx, br.ty, a, dv207))).Append('\n');
            }
            return sb.ToString();
        }

#if !OPENZ_HARNESS || OZ_DV207   // the kit's Mono harness on a tree without the kit (P before the apply) builds the table only
        /// <summary>Every bridge of the level's own map (start states as the map has them): (count, hit points of each, destroyed at start).</summary>
        static (int n, string hps, int down) MapBridges(string settings, string mapJson, bool dv207)
        {
            OzV11Kit.TrySet(typeof(ZWorld), "BridgeHealthDeviation", dv207);
            var w = OzV11Kit.World(settings, OzV11Kit.NoUnits(mapJson), 1);
            var bs = w.Objects.Where(o => o.IsBridge).ToList();
            return (bs.Count, string.Join(",", bs.Select(o => o.MaxHealth).Distinct()), bs.Count(o => o.Destroyed));
        }

        /// <summary>A bridge destroyed in play comes back by itself: (seconds from its fall to its return, health and max after the return).</summary>
        static (double back, int hp, int max) Repair(string settings, string mapJson, int btx, int bty, bool dv207)
        {
            OzV11Kit.TrySet(typeof(ZWorld), "BridgeHealthDeviation", dv207);
            var w = OzV11Kit.World(settings, mapJson, 1);
            var b = w.Objects.First(o => o.IsBridge && o.LocX == btx * 16 && o.LocY == bty * 16);
            for (int i = 0; i < 50; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); }
            b.DamageHealth(b.Health); double at = w.Time;
            int ticks = (int)Math.Round(400 / ZConst.TICK);
            for (int i = 0; i < ticks && b.Destroyed; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); }
            return (b.Destroyed ? -1 : w.Time - at, b.Health, b.MaxHealth);
        }

        static void Line(StringBuilder sb, ref int fails, bool ok, string text) { sb.Append(ok ? "PASS  " : "FAIL  ").Append(text).Append('\n'); if (!ok) fails++; }

        /// <summary>The whole check (menu OpenZ/Checks/V1.5/Bridge health as in Z (no Play), the harness's 'check'). Lines PASS / FAIL.</summary>
        public static string Run(string settings, Func<string, string> map) => Check(settings, map, false);

        /// <summary>The parity row's short probe: L02's middle bridge, the MLRS and the Tough squad, the hit points of every L02 bridge.</summary>
        public static string Quick(string settings, Func<string, string> map) => Check(settings, map, true);

        static string Check(string settings, Func<string, string> map, bool quick)
        {
            bool was = ZWorld.BridgeHealthDeviation;
            var sb = new StringBuilder(); int fails = 0;
            try
            {
                foreach (var lv in quick ? new[] { "L02" } : new[] { "L02", "L16", "L17", "L18" })
                {
                    var off = MapBridges(settings, map(lv), false); var on = MapBridges(settings, map(lv), true);
                    Line(sb, ref fails, off.n > 0 && off.hps == ZWorld.ZodBridgeHitPoints.ToString() && on.n == off.n && on.hps == ZWorld.BridgeHitPoints.ToString() && on.down == off.down,
                        $"{lv}: {off.n} bridges; hit points off {off.hps} (Zod's), on {on.hps}; destroyed at the start off {off.down}, on {on.down} (unchanged)");
                }
                var picks = quick ? new[] { Bridges[0] } : new[] { Bridges[0], Bridges[4], Bridges[5] };
                foreach (var br in picks)
                {
                    var mj = Prepared(map(br.level));
                    foreach (var a in Attackers.Where(x => Named.Contains(x.type) && (!quick || x.type == "tough" || x.type == "missile_launcher")))
                    {
                        var r0 = Kill(settings, mj, br.tx, br.ty, a, false); var r1 = Kill(settings, mj, br.tx, br.ty, a, true);
                        bool ok = r0.killed && r1.killed && r1.shots < r0.shots && r1.killAt < r0.killAt && (a.type != "missile_launcher" || r1.shots <= 6);
                        Line(sb, ref fails, ok, $"{br.level} {br.name}, {a.label}: off {r0.shots} shots {(r0.killed ? "down at " + Sec(r0.killAt) + " s" : "STANDING")}; on {r1.shots} shots {(r1.killed ? "down at " + Sec(r1.killAt) + " s" : "STANDING")}" + (a.type == "missile_launcher" ? " (Z: 2000 / 360 = 5.6 -> 6 missiles)" : ""));
                    }
                }
                {
                    var mj = Prepared(map("L02")); var tough = Attackers[0];
                    var x1 = Kill(settings, mj, 26, 44, tough, true); var x2 = Kill(settings, mj, 26, 44, tough, true);
                    Line(sb, ref fails, x1.sig == x2.sig && x1.shots == x2.shots && x1.killAt == x2.killAt, $"deterministic: the same kill twice (Tough squad, L02 middle, DV207 on): {x1.shots} / {x2.shots} shots, {x1.sig} / {x2.sig}");
                }
                if (!quick)
                {
                    var mj = Prepared(map("L02"));
                    var p0 = Repair(settings, mj, 26, 44, false); var p1 = Repair(settings, mj, 26, 44, true);
                    Line(sb, ref fails, p0.back > 0 && Math.Abs(p0.back - p1.back) < 1e-6 && p1.hp == p1.max && p1.max == ZWorld.BridgeHitPoints && p0.hp == ZWorld.ZodBridgeHitPoints,
                        $"repair rule unchanged: a bridge destroyed in play comes back by itself after {Sec(p0.back)} s off / {Sec(p1.back)} s on, at full health ({p0.hp} / {p1.hp} of {p1.max})");
                }
            }
            finally { ZWorld.BridgeHealthDeviation = was; }
            sb.Insert(0, fails == 0 ? "DV207 bridge health: every line PASS\n" : $"DV207 bridge health: {fails} FAIL\n");
            return sb.ToString();
        }
#endif
    }
}
