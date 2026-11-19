using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit sim_notes11: the counted check of DV192 "the Bizon is amphibious" and DV193 "the big tanks crush wrecks" (Sim/ZBigTanks.Ground.cs),
    /// headless, on the real level maps (their start units taken off), the big-tank switches on as in a Play session.
    /// DV192 rows: the Bizon crosses the L02 river, the L16 swamp and the L07 lava (a straight crossing the check finds on each map: dry ground,
    /// 2-10 wet tiles, dry ground) on a plain move order: it arrives, drives wet ticks at x 0.6, its way at most 1.6 x the straight line; a heavy
    /// tank given the same order never enters a wet tile; DV192 off: the Bizon never enters one either; standing in the river it fires at an
    /// enemy on the bank; driving across with an enemy in range it fires while wading; in water it never starts a crush (a medium tank right in
    /// front: 0 crushes; the same scene on dry ground crushes); no other unit's path changes (a mixed L02 scene without a Bizon: DV192 + DV193 on
    /// = off field by field).
    /// with its centre in the lava, no heat); a full-health Bizon held in a lava pool loses MaxHealth / ZWorld.BizonLavaDieTicks a tick (whole HP, the
    /// rule's remainder: the exact sums after 1 s and 10 s) and is destroyed at exactly that tick (one Death event, no killer); ordered into the
    /// pool it stops there and burns to the end the same way; driven out the burn stops at once; the widest L07 crossing its path takes (the
    /// kit's measure of all 119 straight L07 lava runs) it survives with the predicted loss; standing in the L02 river or on L07 dry ground: no heat;
    /// DV192 off: a Bizon placed in the lava takes no heat.
    /// DV193 rows: a 2 x 2 tile vehicle wreck on the straight line: the heavy tank, the Mammoth and the Bizon drive through without a stop (no stop
    /// tick, the way within 1.1 x the straight line), the wreck crushed by them; the medium tank, the jeep, the light tank and the APC: DV193 on = off
    /// field by field (they treat it as before); the Unit editor's holo obstacle is never crushed; the same scene twice = the same fingerprint.
    /// </summary>
    public static class OzV15BigTankGroundCount
    {
        public static string Quick(string settings, Func<string, string> map) => Run(settings, map, false);

        static readonly string[] Big = { "MammothDeviation", "BizonDeviation", "MammothRamDeviation", "BizonCrushDeviation", "RamDuelDeviation", "CrushRollOffDeviation", "RamDebrisDeviation", "RamPolishDeviation" };
        const int Seed = 41;

        public static string Run(string settings, Func<string, string> map, bool full = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DV192 the Bizon is amphibious + DV193 the big tanks crush wrecks, counted (Sim/ZBigTanks.Ground.cs)");
            var keep = new Dictionary<string, bool>();
            foreach (var n in Big.Concat(new[] { "BizonAmphibDeviation", "BigTankWreckDeviation", "CalmStartDeviation", "BigTankRamFirstDeviation" })) keep[n] = Get(n);
            full0 = full;
            try
            {
                foreach (var n in Big) Set(n, true);
                Set("CalmStartDeviation", false); Set("BigTankRamFirstDeviation", false);
                Set("BizonAmphibDeviation", true); Set("BigTankWreckDeviation", true);
                foreach (var (lv, lava) in new[] { ("L02", false), ("L16", false), ("L07", true) }) Crossing(sb, settings, map, lv, lava);
                LavaBurn(sb, settings, map);
                FiresWading(sb, settings, map);
                NoCrushWet(sb, settings, map);
                OthersUnchanged(sb, settings, map);
                foreach (var t in new[] { "heavy", ZBigTanks.Mammoth, ZBigTanks.Bizon }) WreckThrough(sb, settings, t);
                foreach (var t in new[] { "medium", "jeep", "light", "apc" }) WreckOthers(sb, settings, t);
                ObstacleKept(sb, settings);
                string a = Print(settings), b = Print(settings);
                sb.AppendLine(OzV11Kit.Verdict(a == b, "the same scene twice (DV192 + DV193 on: a Bizon across a wreck): the same fingerprint " + Fnv(a).ToString("x16")));
            }
            catch (Exception e) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace?.Split('\n').FirstOrDefault())); }
            finally { foreach (var kv in keep) Set(kv.Key, kv.Value); }
            return sb.ToString();
        }

        static bool Get(string n) { var f = typeof(ZWorld).GetField(n, BindingFlags.Public | BindingFlags.Static); return f != null && (bool)f.GetValue(null); }
        static void Set(string n, bool v) => OzV11Kit.TrySet(typeof(ZWorld), n, v);
        static ulong Fnv(string s) { ulong h = 1469598103934665603UL; foreach (char c in s) { h ^= c; h *= 1099511628211UL; } return h; }

        static ZObject U(ZWorld w, string type, int cx, int cy, Team t, bool armed)
        {
            var o = OzV11Kit.Place(w, type, cx, cy, t);
            if (!armed) { o.Damage = 0; o.MaxHealth = o.Health = 1000000000; }
            return o;
        }

        // ------------------------------------------------------------------ the crossings
        sealed class Cross { public int sx, sy, ex, ey, wet, mx, my; public string where; }

        /// <summary>A straight crossing on the map: along a row (or a column) dry tiles, 2-10 wet tiles (water, or lava), dry tiles; the start and
        /// the end 3 tiles off the wet run, a 3 tile wide corridor open in the Bizon's grid, both ends open in Zod's vehicle grid.</summary>
        static Cross Find(ZWorld w, bool lava)
        {
            var m = w.Map; var pf = w.Path;
            bool Wet(int x, int y) => x >= 0 && y >= 0 && x < m.width && y < m.height && (lava ? m.LavaAt(x, y) : m.ClassAt(x, y) == ZClass.Water);
            bool Dry(int x, int y) => x >= 0 && y >= 0 && x < m.width && y < m.height && !m.LavaAt(x, y) && (m.ClassAt(x, y) == ZClass.Ground || m.ClassAt(x, y) == ZClass.Road);
            bool AmphOk(int x, int y) => pf.TilePassableKind(x, y, ZPathFinder.GridAmphib);
            bool ZodOk(int x, int y) => pf.TilePassableKind(x, y, ZPathFinder.GridZod);
            Cross best = null; int bestScore = int.MaxValue;
            for (int pass = 0; pass < 2; pass++)
            {
                int A = pass == 0 ? m.height : m.width, B = pass == 0 ? m.width : m.height;
                for (int a = 4; a < A - 4; a++)
                    for (int b = 6; b < B - 18; b++)
                    {
                        (int x, int y) T(int along, int side) => pass == 0 ? (along, a + side) : (a + side, along);
                        var t0 = T(b, 0); var tp = T(b - 1, 0);
                        if (!Wet(t0.x, t0.y) || Wet(tp.x, tp.y)) continue;
                        int k = 0; while (k < 12) { var tk = T(b + k, 0); if (!Wet(tk.x, tk.y)) break; k++; }
                        if (k < 2 || k > 10) continue;
                        bool ok = true;
                        for (int s = -1; s <= 1 && ok; s++)
                        {
                            for (int u = b - 5; u < b; u++) { var q = T(u, s); if (!Dry(q.x, q.y) || !ZodOk(q.x, q.y)) { ok = false; break; } }
                            for (int u = b + k; u < b + k + 5 && ok; u++) { var q = T(u, s); if (!Dry(q.x, q.y) || !ZodOk(q.x, q.y)) { ok = false; break; } }
                            for (int u = b - 5; u < b + k + 5 && ok; u++) { var q = T(u, s); if (!AmphOk(q.x, q.y)) { ok = false; break; } }
                        }
                        if (!ok) continue;
                        int score = Math.Abs(k - 4) * 10 + (pass == 0 ? 0 : 1);
                        if (score >= bestScore) continue;
                        var st = T(b - 3, 0); var en = T(b + k + 3, 0); var mid = T(b + k / 2, 0);
                        bestScore = score;
                        best = new Cross
                        {
                            sx = pass == 0 ? st.x * 16 : st.x * 16 + 8, sy = pass == 0 ? st.y * 16 + 8 : st.y * 16,
                            ex = pass == 0 ? en.x * 16 : en.x * 16 + 8, ey = pass == 0 ? en.y * 16 + 8 : en.y * 16,
                            mx = mid.x * 16 + 8, my = mid.y * 16 + 8, wet = k,
                            where = (pass == 0 ? "row " + a : "column " + a) + ", " + k + " wet tiles from " + (pass == 0 ? "x " : "y ") + b
                        };
                    }
            }
            return best;
        }

        /// <summary>The level as it loads (terrain, bridges, buildings, flags, map items, wrecks), its start robots, vehicles and guns taken out
        /// (ZWorld.RemoveNow: no death), blue a CPU team that gives no orders, a blue dummy that keeps the game running.</summary>
        public static ZWorld LevelWorld(string settings, Func<string, string> map, string lv)
        {
            var w = ZWorld.Create(settings, map(lv), Seed, lv == "L17");
            w.Bots.Add(new ZBot(w, Team.Blue, Seed + 101) { Ignored = true });
            foreach (var o in w.Objects.Where(o => o.IsUnit).ToList()) if (w.Get(o.Id) == o) w.RemoveNow(o);
            w.Events.Clear();
            OzV11Kit.Dummy(w, 24, 24);
            return w;
        }

        sealed class Run1 { public bool arrived; public int wetTicks, lavaTicks, ticks, stops; public double path, straight; public long wade; }

        static Run1 Drive(ZWorld w, ZObject u, int ex, int ey, int maxTicks)
        {
            var r = new Run1 { straight = Math.Sqrt((double)(ex - u.CenterX) * (ex - u.CenterX) + (double)(ey - u.CenterY) * (ey - u.CenterY)) };
            long wade0 = ZWorld.BizonWadeTicks;
            OzV11Kit.PlayerMove(w, u, ex, ey, false);
            var mo = new OzV11Kit.Motion(u);
            for (int i = 0; i < maxTicks; i++)
            {
                w.Tick(ZConst.TICK); w.Events.Clear(); mo.Step(u, w.Time);
                if (w.WetBox(u)) r.wetTicks++;
                if (w.InLavaNow(u)) r.lavaTicks++;
                r.ticks = i + 1;
                long dx = u.CenterX - ex, dy = u.CenterY - ey;
                if (dx * dx + dy * dy <= 20 * 20 && u.Waypoints.Count == 0) { r.arrived = true; break; }
            }
            r.path = mo.Path; r.stops = mo.Stops; r.wade = ZWorld.BizonWadeTicks - wade0;
            return r;
        }

        static void Crossing(StringBuilder sb, string settings, Func<string, string> map, string lv, bool lava)
        {
            var w0 = LevelWorld(settings, map, lv);
            var c = Find(w0, lava);
            string what = lava ? "lava" : lv == "L16" ? "swamp" : "river";
            if (c == null) { sb.AppendLine(OzV11Kit.Verdict(false, $"{lv} {what}: no straight crossing found on the map (dry - 2..10 wet tiles - dry, a 3 tile corridor)")); return; }
            var w = LevelWorld(settings, map, lv);
            var bz = U(w, ZBigTanks.Bizon, c.sx, c.sy, Team.Red, false);
            var r = Drive(w, bz, c.ex, c.ey, 8000);
            if (lava)
            {   // N12b: lava weighs x 8 a dry tile on the Bizon's path (it burns): this crossing has a short way round, so it goes round
                bool pl = r.arrived && r.lavaTicks == 0 && bz.BizonLavaTaken == 0 && r.path <= r.straight * 2.0;
                sb.AppendLine(OzV11Kit.Verdict(pl, $"{lv} {what} ({c.where}), a short way round: the Bizon from ({c.sx},{c.sy}) to ({c.ex},{c.ey}) on a move order goes round (lava burns it): arrived {r.arrived} in {r.ticks} ticks, {r.lavaTicks} ticks with its centre in the lava, heat taken {bz.BizonLavaTaken}, way {r.path:0} px for {r.straight:0} px straight, stops {r.stops}"));
            }
            else
            {
                bool pass = r.arrived && r.wetTicks > 0 && r.wade > 0 && r.path <= r.straight * 1.6;
                sb.AppendLine(OzV11Kit.Verdict(pass, $"{lv} {what} ({c.where}): the Bizon from ({c.sx},{c.sy}) to ({c.ex},{c.ey}) on a move order: arrived {r.arrived} in {r.ticks} ticks, {r.wetTicks} ticks on wet tiles ({r.wade} at the x {ZWorld.BizonWadePace} pace), way {r.path:0} px for {r.straight:0} px straight, stops {r.stops}"));
            }
            if (!full0) return;
            // the heavy tank, the same order: never on a wet tile (Zod's grid); DV192 off: the Bizon neither
            var wh = LevelWorld(settings, map, lv);
            var hv = U(wh, "heavy", c.sx, c.sy, Team.Red, false);
            var rh = Drive(wh, hv, c.ex, c.ey, 8000);
            sb.AppendLine(OzV11Kit.Verdict(rh.wetTicks == 0, $"{lv} {what}: a heavy tank, the same order: {rh.wetTicks} ticks on wet tiles (never in it), arrived {rh.arrived}, way {rh.path:0} px"));
            Set("BizonAmphibDeviation", false);
            try
            {
                var wo = LevelWorld(settings, map, lv);
                var bo = U(wo, ZBigTanks.Bizon, c.sx, c.sy, Team.Red, false);
                var ro = Drive(wo, bo, c.ex, c.ey, 8000);
                sb.AppendLine(OzV11Kit.Verdict(ro.wetTicks == 0, $"{lv} {what}: DV192 off: the Bizon, the same order: {ro.wetTicks} ticks on wet tiles (never in it), arrived {ro.arrived}, way {ro.path:0} px"));
            }
            finally { Set("BizonAmphibDeviation", true); }
        }

        static bool full0 = true;

        // ------------------------------------------------------------------ N12b: lava slowly burns the Bizon
        /// <summary>The widest L07 lava crossing a Bizon's own path takes (the kit's measure, kit_tools/n12b_run.py measure L07, 11 Nov: each of the
        /// 119 straight lava runs of L07 with dry ground past both ends, a full-health Bizon ordered across): column 31, lava y 44..45, from (504,664)
        /// to (504,776): its path crosses that 2 tile neck on a slant, 9.22 s in the lava; a heavy tank's way round there is 614 px. WideLossPct: the
        /// share of its health it costs at BizonLavaDieTicks 2700 (README), +- 2 points.</summary>
        const int WideSX = 504, WideSY = 664, WideEX = 504, WideEY = 776;
        const double WideLossPct = 34.1;

        /// <summary>The heat n ticks without a break in the lava take: whole HP, the rule's remainder (ZWorld.BizonHeatTick).</summary>
        static long Burnt(long n, int max) => n * max / ZWorld.BizonLavaDieTicks;

        /// <summary>The first L07 tile (row order) with lava r tiles all round it; (-1, -1) none.</summary>
        static (int x, int y) LavaPool(ZWorld w, int r)
        {
            var m = w.Map;
            for (int y = r; y < m.height - r; y++)
                for (int x = r; x < m.width - r; x++)
                {
                    bool all = true;
                    for (int j = -r; j <= r && all; j++) for (int i = -r; i <= r && all; i++) all = m.LavaAt(x + i, y + j);
                    if (all) return (x, y);
                }
            return (-1, -1);
        }

        /// <summary>From a lava tile: the nearest dry ground straight out along a row or a column (open for every vehicle with its 2 x 2 footprint,
        /// the next 3 tiles out dry too), first west, east, north, south; the tile 3 out from the shore. (-1, -1) none within 24 tiles.</summary>
        static (int x, int y) ShoreOut(ZWorld w, int px, int py)
        {
            var m = w.Map; var pf = w.Path;
            bool Open(int x, int y) => !m.LavaAt(x, y) && pf.TilePassableKind(x, y, ZPathFinder.GridZod);
            bool Dry(int x, int y) => x >= 0 && y >= 0 && x + 1 < m.width && y + 1 < m.height && Open(x, y) && Open(x + 1, y) && Open(x, y + 1) && Open(x + 1, y + 1);
            foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                for (int k = 1; k <= 24; k++)
                {
                    int x = px + dx * k, y = py + dy * k;
                    if (m.LavaAt(x, y)) continue;
                    bool ok = true;
                    for (int u = 0; u < 4 && ok; u++) ok = Dry(x + dx * u, y + dy * u);
                    if (ok) return (x + dx * 3, y + dy * 3);
                    break;
                }
            return (-1, -1);
        }

        static void LavaBurn(StringBuilder sb, string settings, Func<string, string> map)
        {
            int D = ZWorld.BizonLavaDieTicks;
            var w0 = LevelWorld(settings, map, "L07");
            int pr = 3; var pool = LavaPool(w0, pr);
            while (pool.x < 0 && pr > 1) pool = LavaPool(w0, --pr);                            // the deepest pool L07 has (lava pr tiles all round)
            var shore = pool.x < 0 ? (x: -1, y: -1) : ShoreOut(w0, pool.x, pool.y);
            if (pool.x < 0 || shore.x < 0) { sb.AppendLine(OzV11Kit.Verdict(false, $"L07 lava burn: no lava pool (3 x 3 lava tiles or more) with a dry shore found ({pool.x},{pool.y})")); return; }
            int poolX = pool.x * 16 + 8, poolY = pool.y * 16 + 8, dryX = shore.x * 16 + 8, dryY = shore.y * 16 + 8;

            {   // 1 held in the pool: placed there, no order (it stands): the rate, then the end at the set tick
                var w = LevelWorld(settings, map, "L07");
                int burns0 = ZWorld.BizonLavaBurns;
                var bz = U(w, ZBigTanks.Bizon, poolX, poolY, Team.Red, true);
                int max = bz.MaxHealth, hp0 = bz.Health, death = -1, deaths = 0, killer = -2; long at100 = -1, at1000 = -1; bool inAll = true;
                for (int i = 1; i <= D + 300; i++)
                {
                    w.Tick(ZConst.TICK);
                    foreach (var e in w.Events) if (e.type == ZEventType.Death && e.objId == bz.Id) { deaths++; killer = e.otherId; }
                    w.Events.Clear();
                    if (bz.Destroyed) { death = i; break; }
                    if (!w.InLavaNow(bz)) inAll = false;
                    if (i == 100) at100 = hp0 - bz.Health;
                    if (i == 1000) at1000 = hp0 - bz.Health;
                }
                bool pass = hp0 == max && at100 == Burnt(100, max) && at1000 == Burnt(1000, max) && death == D && deaths == 1 && killer == -1 && inAll && ZWorld.BizonLavaBurns - burns0 == 1;
                sb.AppendLine(OzV11Kit.Verdict(pass, $"L07 lava burns the Bizon slowly: a full-health Bizon ({max} HP) placed in a lava pool (tile {pool.x},{pool.y}, lava {pr} tiles all round), no order (it stands; in the lava all along {inAll}): heat {at100} HP in its first 1 s (want {Burnt(100, max)}), {at1000} in 10 s (want {Burnt(1000, max)}); destroyed at tick {death} = {death * ZConst.TICK:0.00} s (want {D} = {D * ZConst.TICK:0.00} s), {deaths} Death event, killer {killer} (none = -1), counted burns {ZWorld.BizonLavaBurns - burns0}"));
            }
            if (full0)
            {   // 2 the player's order into the middle of the pool: it drives in, stops there, and the burn goes on to the end
                var w = LevelWorld(settings, map, "L07");
                var bz = U(w, ZBigTanks.Bizon, dryX, dryY, Team.Red, true);
                int max = bz.MaxHealth, death = -1, stood = 0, dips = 0; bool was = false;
                OzV11Kit.PlayerMove(w, bz, poolX, poolY, false);
                for (int i = 1; i <= 15000; i++)
                {
                    w.Tick(ZConst.TICK); w.Events.Clear();
                    if (bz.Destroyed) { death = i; break; }
                    bool inL = w.InLavaNow(bz);
                    if (inL && !was) dips++;
                    if (inL && bz.Waypoints.Count == 0) stood++;
                    was = inL;
                }
                bool pass = death > 0 && dips == 1 && stood > 0 && bz.BizonLavaTicks == D && bz.BizonLavaTaken == max;
                sb.AppendLine(OzV11Kit.Verdict(pass, $"L07: the player's move order into the middle of the lava pool: the Bizon drives in from the shore ({dryX},{dryY}) and stops there ({stood} ticks standing in it, {dips} dip), the burn goes on: destroyed at tick {death} after {bz.BizonLavaTicks} ticks in the lava = {bz.BizonLavaTicks * ZConst.TICK:0.00} s (want {D}), heat taken {bz.BizonLavaTaken} of {max}"));
            }
            {   // 3 driven out of the pool: the burn stops at once
                var w = LevelWorld(settings, map, "L07");
                var bz = U(w, ZBigTanks.Bizon, poolX, poolY, Team.Red, true);
                int max = bz.MaxHealth, peak = 0, outAt = -1, hpOut = -1, outLt; bool arrived = false;
                OzV11Kit.PlayerMove(w, bz, dryX, dryY, false);
                for (int i = 1; i <= 9000; i++)
                {
                    w.Tick(ZConst.TICK); w.Events.Clear();
                    if (bz.Destroyed) break;
                    peak = Math.Max(peak, bz.BizonLavaTicks);
                    if (outAt < 0 && peak > 0 && bz.BizonLavaTicks == 0) { outAt = i; hpOut = bz.Health; }
                    long dx = bz.CenterX - dryX, dy = bz.CenterY - dryY;
                    if (!arrived && dx * dx + dy * dy <= 20 * 20 && bz.Waypoints.Count == 0) arrived = true;
                    if (outAt > 0 && i >= outAt + 1000 && arrived) break;
                }
                outLt = bz.BizonLavaTicks;
                bool pass = !bz.Destroyed && arrived && outAt > 0 && hpOut == bz.Health && outLt == 0 && bz.BizonLavaTaken == Burnt(peak, max) && max - bz.Health == bz.BizonLavaTaken;
                sb.AppendLine(OzV11Kit.Verdict(pass, $"L07: driven out of the lava pool to the shore: {peak} ticks in the lava = {peak * ZConst.TICK:0.00} s, heat {bz.BizonLavaTaken} HP (want {Burnt(peak, max)}); out at tick {outAt}: health {hpOut}, 10 s later {bz.Health} (the burn stops at once), its lava ticks back to {outLt}; arrived {arrived}"));
            }
            {   // 4 the widest crossing its path takes on L07: it survives with the predicted loss
                var w = LevelWorld(settings, map, "L07");
                var bz = U(w, ZBigTanks.Bizon, WideSX, WideSY, Team.Red, true);
                int max = bz.MaxHealth, peak = 0, dips = 0, ticks = 0, lavaT = 0; bool was = false, arrived = false;
                double straight = Math.Sqrt((double)(WideEX - WideSX) * (WideEX - WideSX) + (double)(WideEY - WideSY) * (WideEY - WideSY));
                OzV11Kit.PlayerMove(w, bz, WideEX, WideEY, false);
                var mo = new OzV11Kit.Motion(bz);
                for (int i = 0; i < 9000; i++)
                {
                    w.Tick(ZConst.TICK); w.Events.Clear(); mo.Step(bz, w.Time); ticks = i + 1;
                    if (bz.Destroyed) break;
                    bool inL = w.InLavaNow(bz); if (inL) { lavaT++; if (!was) dips++; } was = inL;
                    peak = Math.Max(peak, bz.BizonLavaTicks);
                    long dx = bz.CenterX - WideEX, dy = bz.CenterY - WideEY;
                    if (dx * dx + dy * dy <= 20 * 20 && bz.Waypoints.Count == 0) { arrived = true; break; }
                }
                double pct = 100.0 * (max - bz.Health) / max;
                bool pass = arrived && !bz.Destroyed && dips == 1 && Math.Abs(pct - WideLossPct) <= 2.0 && bz.BizonLavaTaken == Burnt(peak, max) && max - bz.Health == bz.BizonLavaTaken;
                sb.AppendLine(OzV11Kit.Verdict(pass, $"L07: the widest lava crossing a Bizon's path takes (column 31, a 2 tile neck crossed on a slant, the way round 614 px): from ({WideSX},{WideSY}) to ({WideEX},{WideEY}): arrived {arrived} in {ticks} ticks, alive {!bz.Destroyed}, {dips} dip, {peak} ticks in the lava = {peak * ZConst.TICK:0.00} s, lost {max - bz.Health} of {max} HP = {pct:0.0} % (predicted {WideLossPct:0.0} % +- 2), way {mo.Path:0} px for {straight:0} px straight"));
            }
            {   // 5 no heat in water or on land: standing 10 s in the L02 river; standing 10 s on L07 dry ground by the pool
                var wr0 = LevelWorld(settings, map, "L02"); var c = Find(wr0, false);
                int lostW = -1, takenW = -1, lostL, takenL; bool wet = false;
                if (c != null)
                {
                    var wr = LevelWorld(settings, map, "L02");
                    var bw = U(wr, ZBigTanks.Bizon, c.mx, c.my, Team.Red, true); int h0 = bw.Health;
                    for (int i = 0; i < 1000; i++) { wr.Tick(ZConst.TICK); wr.Events.Clear(); }
                    wet = wr.WetBox(bw); lostW = h0 - bw.Health; takenW = bw.BizonLavaTaken;
                }
                var wl = LevelWorld(settings, map, "L07");
                var bl = U(wl, ZBigTanks.Bizon, dryX, dryY, Team.Red, true); int h1 = bl.Health;
                for (int i = 0; i < 1000; i++) { wl.Tick(ZConst.TICK); wl.Events.Clear(); }
                lostL = h1 - bl.Health; takenL = bl.BizonLavaTaken;
                bool pass = c != null && wet && lostW == 0 && takenW == 0 && lostL == 0 && takenL == 0 && !wl.InLavaNow(bl);
                sb.AppendLine(OzV11Kit.Verdict(pass, $"no heat in water or on land: a full-health Bizon standing 10 s in the L02 river (wet {wet}): lost {lostW} HP, heat {takenW}; standing 10 s on L07 dry ground by the lava ({dryX},{dryY}): lost {lostL} HP, heat {takenL}"));
            }
            {   // 7 the same lava scene twice (a Bizon driven out of the pool past a second one standing in it): the same fingerprint
                string Scene()
                {
                    var w = LevelWorld(settings, map, "L07");
                    var a1 = U(w, ZBigTanks.Bizon, poolX, poolY, Team.Red, true); U(w, ZBigTanks.Bizon, poolX + 40, poolY, Team.Red, true);
                    OzV11Kit.PlayerMove(w, a1, dryX, dryY, false);
                    var s = new StringBuilder();
                    for (int i = 0; i < 1500; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); if (i % 10 == 9) s.Append(Fields(w)).Append(w.Rng.Next(1 << 30)).Append('|'); }
                    return s.ToString();
                }
                string f1 = Scene(), f2 = Scene();
                sb.AppendLine(OzV11Kit.Verdict(f1 == f2, $"the same L07 lava scene twice (one Bizon driven out of the pool, one standing in it, 15 s): the same fingerprint {Fnv(f1):x16} / {Fnv(f2):x16}"));
            }
            {   // 6 DV192 off ('Zod exact'): no heat for a Bizon placed in the lava
                Set("BizonAmphibDeviation", false);
                try
                {
                    var w = LevelWorld(settings, map, "L07");
                    var bz = U(w, ZBigTanks.Bizon, poolX, poolY, Team.Red, true); int h0 = bz.Health;
                    for (int i = 0; i < 500; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); }
                    sb.AppendLine(OzV11Kit.Verdict(h0 - bz.Health == 0 && bz.BizonLavaTaken == 0 && bz.BizonLavaTicks == 0, $"DV192 off: a Bizon placed in the L07 lava pool for 5 s: lost {h0 - bz.Health} HP, heat {bz.BizonLavaTaken}, lava ticks {bz.BizonLavaTicks} (the burn is DV192's)"));
                }
                finally { Set("BizonAmphibDeviation", true); }
            }
        }

        static int Shots(ZWorld w, ZObject u, List<ZEvent> evs, Func<bool> when, ref int wetShots)
        {
            int n = 0;
            foreach (var e in evs) if (e.type == ZEventType.MissileFired && e.objId == u.Id) { n++; if (when()) wetShots++; }
            return n;
        }

        static void FiresWading(StringBuilder sb, string settings, Func<string, string> map)
        {
            var w0 = LevelWorld(settings, map, "L02");
            var c = Find(w0, false);
            if (c == null) { sb.AppendLine(OzV11Kit.Verdict(false, "fires while wading: no L02 crossing")); return; }
            // standing in the river: an enemy (unarmed, immortal) medium tank on the far bank in range
            var w = LevelWorld(settings, map, "L02");
            var bz = U(w, ZBigTanks.Bizon, c.mx, c.my, Team.Red, true);
            var tg = U(w, "medium", c.ex, c.ey, Team.Blue, false);
            int shots = 0, wetShots = 0;
            for (int i = 0; i < 1200; i++) { w.Tick(ZConst.TICK); shots += Shots(w, bz, w.Events, () => w.WetBox(bz), ref wetShots); w.Events.Clear(); }
            double d = OzV11Kit.Dist(bz, tg);
            sb.AppendLine(OzV11Kit.Verdict(wetShots > 0, $"the Bizon standing in the L02 river (wet {w.WetBox(bz)}) at an enemy medium tank on the bank {d:0} px away (range {bz.AttackRadius}): {shots} shot(s) in 12 s, {wetShots} of them from the water"));
            // driving across, the enemy in range all the way
            var w2 = LevelWorld(settings, map, "L02");
            var b2 = U(w2, ZBigTanks.Bizon, c.sx, c.sy, Team.Red, true);
            var t2 = U(w2, "medium", c.mx + (c.my - c.sy == 0 ? 0 : 70), c.my + (c.my - c.sy == 0 ? 70 : 0), Team.Blue, false);
            OzV11Kit.PlayerMove(w2, b2, c.ex, c.ey, false);
            int s2 = 0, ws2 = 0, wet2 = 0;
            for (int i = 0; i < 8000; i++)
            {
                w2.Tick(ZConst.TICK); if (w2.WetBox(b2)) wet2++;
                s2 += Shots(w2, b2, w2.Events, () => w2.WetBox(b2), ref ws2); w2.Events.Clear();
                if (b2.Waypoints.Count == 0 && i > 100) break;
            }
            sb.AppendLine(OzV11Kit.Verdict(ws2 > 0 && wet2 > 0, $"the Bizon driving across the L02 river (a plain move order) with an enemy medium tank beside the crossing: {wet2} ticks wet, {s2} shot(s), {ws2} of them while wading"));
        }

        static void NoCrushWet(StringBuilder sb, string settings, Func<string, string> map)
        {
            var w0 = LevelWorld(settings, map, "L02");
            var c = Find(w0, false);
            if (c == null) { sb.AppendLine(OzV11Kit.Verdict(false, "no crush in water: no L02 crossing")); return; }
            int dx = Math.Sign(c.ex - c.sx), dy = Math.Sign(c.ey - c.sy);
            // in water: the Bizon in the middle of the river heading for the far bank, an enemy medium tank right in front of it
            var w = LevelWorld(settings, map, "L02");
            var bz = U(w, ZBigTanks.Bizon, c.mx, c.my, Team.Red, true);
            bz.FaceX = dx * 9f; bz.FaceY = dy * 9f;
            var tg = U(w, "medium", c.mx + dx * 56, c.my + dy * 56, Team.Blue, false);
            int hits0 = w.BizonCrushHits, ref0 = ZWorld.BizonWetRamRefused;
            for (int i = 0; i < 600; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); }
            int hitsWet = w.BizonCrushHits - hits0, refused = ZWorld.BizonWetRamRefused - ref0;
            // the same scene on the plain ground: it crushes
            var wp = OzV11Kit.World(settings, OzV11Kit.Plain(64, 48), Seed); OzV11Kit.Dummy(wp, 980, 730);
            var bp = U(wp, ZBigTanks.Bizon, 320, 384, Team.Red, true); bp.FaceX = 9f; bp.FaceY = 0f;
            U(wp, "medium", 320 + 56, 384, Team.Blue, false);
            int hp0 = wp.BizonCrushHits;
            for (int i = 0; i < 600; i++) { wp.Tick(ZConst.TICK); wp.Events.Clear(); }
            int hitsDry = wp.BizonCrushHits - hp0;
            sb.AppendLine(OzV11Kit.Verdict(hitsWet == 0 && hitsDry > 0, $"no crush in water: the Bizon in the L02 river with an enemy medium tank 56 px in front: {hitsWet} crush(es) in 6 s (wet {w.WetBox(bz)}, refused looks {refused}); the same scene on dry ground: {hitsDry} crush(es)"));
        }

        static string Fields(ZWorld w)
        {
            var sb = new StringBuilder();
            foreach (var o in w.Objects)
                sb.Append(o.Id).Append(':').Append(o.LocX).Append(',').Append(o.LocY).Append(',').Append(o.Health).Append(',').Append(o.Waypoints.Count).Append(',')
                  .Append(o.AttackObject != null ? o.AttackObject.Id : -1).Append(',').Append(BitConverter.DoubleToInt64Bits(o.XOver)).Append(',').Append(BitConverter.DoubleToInt64Bits(o.YOver)).Append(';');
            return sb.ToString();
        }

        static void OthersUnchanged(StringBuilder sb, string settings, Func<string, string> map)
        {
            string Scene()
            {
                var w0 = LevelWorld(settings, map, "L02");
                var c = Find(w0, false);
                var w = LevelWorld(settings, map, "L02");
                int x = c?.sx ?? 600, y = c?.sy ?? 600, ex = c?.ex ?? 700, ey = c?.ey ?? 700;
                var units = new List<ZObject> { U(w, "heavy", x, y, Team.Red, true), U(w, "medium", x - 40, y - 40, Team.Red, true), U(w, "jeep", x - 40, y + 40, Team.Red, true),
                                                U(w, "apc", x + 40, y - 40, Team.Red, true), U(w, ZBigTanks.Mammoth, x - 80, y, Team.Red, true), OzV11Kit.Place(w, "grunt", x, y + 60, Team.Red, 3) };
                foreach (var u in units) OzV11Kit.PlayerMove(w, u, ex, ey, true);
                var s = new StringBuilder();
                for (int i = 0; i < 2000; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); if (i % 50 == 49) s.Append(Fields(w)).Append('|'); }
                return s.ToString();
            }
            string on = Scene();
            Set("BizonAmphibDeviation", false); Set("BigTankWreckDeviation", false);
            string off;
            try { off = Scene(); }
            finally { Set("BizonAmphibDeviation", true); Set("BigTankWreckDeviation", true); }
            sb.AppendLine(OzV11Kit.Verdict(on == off, $"no other unit's path changed: an L02 scene without a Bizon (heavy, medium, jeep, APC, Mammoth, grunts sent across the river to the far bank, 20 s): DV192 + DV193 on = off field by field ({(on == off ? "same" : "DIFFERENT")}, {on.Length} chars, {Fnv(on):x16})"));
        }

        // ------------------------------------------------------------------ the wrecks
        sealed class WreckRun { public bool arrived, crushed, alive; public int ticks, stops, crushes; public double path, straight; public string fields; }

        static WreckRun WreckScene(string settings, string type, bool obstacle = false)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(64, 48), Seed); OzV11Kit.Dummy(w, 980, 730);
            ZObject wk;
            if (obstacle) wk = w.PlaceObstacle(19, 23, 2, ZWorld.PlaygroundObstacleHp, out _);
            else wk = w.AddTestWreck(19, 23, 2, 2);
            var u = U(w, type, 160, 384, Team.Red, true);
            u.FaceX = 9f; u.FaceY = 0f;
            int c0 = ZWorld.BigTankWreckCrushes;
            var r = new WreckRun { straight = 480 - 160 };
            OzV11Kit.PlayerMove(w, u, 480, 384, false);
            var mo = new OzV11Kit.Motion(u); var s = new StringBuilder();
            for (int i = 0; i < 6000; i++)
            {
                w.Tick(ZConst.TICK); w.Events.Clear(); mo.Step(u, w.Time);
                if (i % 25 == 24) s.Append(Fields(w)).Append('|');
                r.ticks = i + 1;
                if (u.Waypoints.Count == 0 && Math.Abs(u.CenterX - 480) <= 20 && Math.Abs(u.CenterY - 384) <= 20) { r.arrived = true; break; }
            }
            r.path = mo.Path; r.stops = mo.Stops; r.crushes = ZWorld.BigTankWreckCrushes - c0;
            r.alive = wk != null && OzV11Kit.Alive(wk) && w.Get(wk.Id) == wk; r.crushed = r.crushes > 0; r.fields = s.ToString();
            return r;
        }

        static void WreckThrough(StringBuilder sb, string settings, string type)
        {
            var r = WreckScene(settings, type);
            bool pass = r.arrived && r.stops == 0 && r.crushed && !r.alive && r.path <= r.straight * 1.1;
            sb.AppendLine(OzV11Kit.Verdict(pass, $"{type} through a 2 x 2 tile vehicle wreck on its straight way: arrived {r.arrived} in {r.ticks} ticks, stops {r.stops}, way {r.path:0} px for {r.straight:0} px straight, the wreck crushed {r.crushed} ({r.crushes}), still there {r.alive}"));
            Set("BigTankWreckDeviation", false);
            WreckRun o;
            try { o = WreckScene(settings, type); }
            finally { Set("BigTankWreckDeviation", true); }
            sb.AppendLine(OzV11Kit.Verdict(!o.crushed, $"{type}, DV193 off: as before: arrived {o.arrived} in {o.ticks} ticks, stops {o.stops}, way {o.path:0} px, the wreck crushed {o.crushed}, still there {o.alive} (it goes round or blasts it)"));
        }

        static void WreckOthers(StringBuilder sb, string settings, string type)
        {
            var on = WreckScene(settings, type);
            Set("BigTankWreckDeviation", false);
            WreckRun off;
            try { off = WreckScene(settings, type); }
            finally { Set("BigTankWreckDeviation", true); }
            bool same = on.fields == off.fields && !on.crushed;
            sb.AppendLine(OzV11Kit.Verdict(same, $"{type} and a vehicle wreck on its way: DV193 on = off field by field ({(on.fields == off.fields ? "same" : "DIFFERENT")}): arrived {on.arrived} in {on.ticks} ticks, stops {on.stops}, way {on.path:0} px, the wreck crushed {on.crushed}, still there {on.alive}"));
        }

        static void ObstacleKept(StringBuilder sb, string settings)
        {
            var r = WreckScene(settings, "heavy", true);
            sb.AppendLine(OzV11Kit.Verdict(!r.crushed, $"the Unit editor's holo obstacle on a heavy tank's way: never crushed (crushes {r.crushes}, still there {r.alive}; arrived {r.arrived}, way {r.path:0} px)"));
        }

        static string Print(string settings)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(64, 48), Seed); OzV11Kit.Dummy(w, 980, 730);
            w.AddTestWreck(19, 23, 2, 2);
            var u = U(w, ZBigTanks.Bizon, 160, 384, Team.Red, true);
            var h = U(w, "heavy", 160, 450, Team.Red, true);
            OzV11Kit.PlayerMove(w, u, 480, 384, false); OzV11Kit.PlayerMove(w, h, 480, 384, false);
            var s = new StringBuilder();
            for (int i = 0; i < 1500; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); if (i % 10 == 9) s.Append(Fields(w)).Append(w.Rng.Next(1 << 30)).Append('|'); }
            return s.ToString();
        }
    }
}
