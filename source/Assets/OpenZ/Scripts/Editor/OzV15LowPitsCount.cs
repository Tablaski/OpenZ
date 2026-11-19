using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit pits_no_block_r3: the counted check of DV205 "low pits never block sight or shots" (Sim/ZWorld.LowPits.cs, the hook in
    /// Sim/ZSight.cs), no Unity API (the kit's Mono harness runs it too). Every number is counted on two worlds of the same map, DV205 off
    /// (P before this kit) and on; DV23 (line of sight) on in both.
    /// <para>1 Every level (2, 7, 16, 17, 18): the impassable tiles, lava (DV152), the low tiles of the data, the sight walls off / on: a tile
    /// opens only when it is low; every other tile is the same wall as before (raised rock, cliffs, mountains, city blocks, the map's ring);
    /// a low tile under a standing rock / hut / building stays shut while that stands; movement unchanged: every low tile is still impassable
    /// for robots and vehicles, and the path finder's grid is the same.</para>
    /// <para>2 Level 16, the owner's spot (zone 9 ravine, the gun on the left side, tile 16,84): every robot-passable tile in the gun's reach
    /// whose line from the gun crosses a low tile: sight queries off / on; then a red gun on that spot (the map's own gun) and one blue grunt in
    /// the ravine's floor: the gun's shots at it in 20 s, off / on; the grunt's shots back.</para>
    /// <para>3 Level 16, across the whole pits: every pair of robot-passable tiles within 144 px (the longest direct gun) whose line crosses a
    /// low tile, once per pair: clear off / on (the gorges, the ravine, the holes); and every pair whose line crosses a RAISED impassable
    /// tile: clear off / on (must stay the same).</para>
    /// <para>4 The fight of part 2 twice with DV205 on: the same counts and the same rules fingerprint (deterministic).</para>
    /// </summary>
    public static class OzV15LowPitsCount
    {
        public static readonly string[] Levels = { "L02", "L07", "L16", "L17", "L18" };
        /// <summary>The owner's spot on level 16: the map's gun at tile (16, 84) (cannon:gun, neutral at the start), the ravine of zone 9 north of it.</summary>
        public const int GunTx = 16, GunTy = 84;
        const double FightSec = 20;
        const int PairReach = 144;

        static ZWorld Make(string settings, string mapJson, bool on, int seed = 1)
        {
            ZWorld.LowPitsDeviation = on; ZWorld.LosDeviation = true;
            var w = OzV11Kit.World(settings, mapJson, seed);
            var _ = w.Sight;                                                                    // the sight grid reads DV205 here, once
            return w;
        }

        static bool Shot(ZEvent e) => e.type == ZEventType.MissileFired || e.type == ZEventType.Hitscan;

        // ------------------------------------------------------------------ 1 every level
        internal sealed class LevelRow { public string level; public int imp, lava, low, opened, alreadyOpen, heldByObject, changedNotLow, pathDiff, lowPassable; }

        internal static LevelRow CountLevel(string settings, string level, string mapJson)
        {
            var off = Make(settings, mapJson, false); var on = Make(settings, mapJson, true);
            var m = off.Map; var r = new LevelRow { level = level };
            for (int ty = 0; ty < m.height; ty++)
                for (int tx = 0; tx < m.width; tx++)
                {
                    bool imp = m.ClassAt(tx, ty) == ZClass.Impassable;
                    if (imp) r.imp++;
                    if (imp && m.LavaAt(tx, ty)) r.lava++;
                    bool low = on.LowPitAt(tx, ty);
                    if (low) r.low++;
                    bool a = off.Sight.BlockedTile(tx, ty), b = on.Sight.BlockedTile(tx, ty);
                    if (a != b && !low) r.changedNotLow++;
                    if (low && a && !b) r.opened++;
                    if (low && !a) r.alreadyOpen++;
                    if (low && a && b) r.heldByObject++;
                    if (off.Path.TilePassable(tx, ty, true) != on.Path.TilePassable(tx, ty, true) || off.Path.TilePassable(tx, ty, false) != on.Path.TilePassable(tx, ty, false)) r.pathDiff++;
                    if (low && (on.Path.TilePassable(tx, ty, true) || on.Path.TilePassable(tx, ty, false))) r.lowPassable++;
                }
            return r;
        }

        // ------------------------------------------------------------------ 2 the owner's spot
        internal sealed class Spot { public int targets, clearOff, clearOn, shutRaised, shutObject; public string sample = ""; }

        static int Cx(int t) => (t << 4) + 8;

        static bool LineCrossesLow(ZWorld w, int ax, int ay, int bx, int by)
        {
            foreach (var (x, y) in ZPathFinder.Bresenham(ax >> 4, ay >> 4, bx >> 4, by >> 4, w.Map.width, w.Map.height)) if (w.LowPitAt(x, y)) return true;
            foreach (var (x, y) in ZPathFinder.Bresenham(bx >> 4, by >> 4, ax >> 4, ay >> 4, w.Map.width, w.Map.height)) if (w.LowPitAt(x, y)) return true;
            return false;
        }

        static bool LineCrossesRaised(ZWorld w, int ax, int ay, int bx, int by)
        {
            int ex = bx >> 4, ey = by >> 4;
            foreach (var (x, y) in ZPathFinder.Bresenham(ax >> 4, ay >> 4, ex, ey, w.Map.width, w.Map.height))
            {
                if (x == ex && y == ey) break;
                if (w.Map.ClassAt(x, y) == ZClass.Impassable && !w.Map.LavaAt(x, y) && !w.LowPitAt(x, y)) return true;
            }
            return false;
        }

        static string NoUnitsL16(string l16) => OzV11Kit.NoUnits(l16);                      // the terrain, bridges, buildings, flags and map items stay

        internal static Spot SightFromGun(string settings, string l16)
        {
            var mj = NoUnitsL16(l16);
            var off = Make(settings, mj, false); var on = Make(settings, mj, true);
            var gOff = off.CreateObject("cannon", "gun", GunTx * 16, GunTy * 16, Team.Red); var gOn = on.CreateObject("cannon", "gun", GunTx * 16, GunTy * 16, Team.Red);
            var s = new Spot(); int reach = gOn.AttackRadius;
            for (int ty = 0; ty < on.Map.height; ty++)
                for (int tx = 0; tx < on.Map.width; tx++)
                {
                    int px = Cx(tx), py = Cx(ty);
                    if (!on.Path.TilePassable(tx, ty, true)) continue;
                    long dx = px - gOn.CenterX, dy = py - gOn.CenterY;
                    if (dx * dx + dy * dy > (long)reach * reach || !LineCrossesLow(on, gOn.CenterX, gOn.CenterY, px, py)) continue;
                    s.targets++;
                    bool a = off.Sight.Clear(gOff.CenterX, gOff.CenterY, off.Sight.ExemptOf(gOff), px, py, 0), b = on.Sight.Clear(gOn.CenterX, gOn.CenterY, on.Sight.ExemptOf(gOn), px, py, 0);
                    if (a) s.clearOff++;
                    if (b) s.clearOn++;
                    else if (LineCrossesRaised(on, gOn.CenterX, gOn.CenterY, px, py) || LineCrossesRaised(on, px, py, gOn.CenterX, gOn.CenterY)) s.shutRaised++;
                    else s.shutObject++;                                                    // a standing rock / hut / building on the line
                    if (s.sample.Length < 160 && !a && b) s.sample += (s.sample.Length > 0 ? " " : "") + "(" + tx + "," + ty + ")";
                }
            return s;
        }

        internal sealed class Fight { public int gunShots, gruntShots; public double firstShot = -1; public bool gruntDead; public string target = "", sig = ""; }

        /// <summary>The red gun on the owner's spot, one blue grunt on a robot-passable tile north of the gun, in its reach, whose line from the gun
        /// crosses a low tile (the same tile off and on): across = the farthest such tile (beyond the ravine), else the nearest (its floor).</summary>
        internal static Fight GunFight(string settings, string l16, bool on, int seed = 1, bool across = false)
        {
            var w = Make(settings, NoUnitsL16(l16), on, seed);
            OzV11Kit.Dummy(w, 90 * 16, 120 * 16);
            var gun = w.CreateObject("cannon", "gun", GunTx * 16, GunTy * 16, Team.Red);
            int best = int.MaxValue, bx = -1, by = -1;
            for (int ty = GunTy - 8; ty < GunTy; ty++)
                for (int tx = GunTx - 8; tx <= GunTx + 9; tx++)
                {
                    if (!w.Path.TilePassable(tx, ty, true)) continue;
                    int px = Cx(tx), py = Cx(ty); long dx = px - gun.CenterX, dy = py - gun.CenterY; long d2 = dx * dx + dy * dy;
                    if (d2 > (long)(gun.AttackRadius - 8) * (gun.AttackRadius - 8) || !LineCrossesLow(w, gun.CenterX, gun.CenterY, px, py)) continue;
                    int key = across ? ty * 4096 + (int)Math.Min(4095, d2 >> 4) : (int)d2;     // across: the northmost, then the nearest; else the nearest
                    if (key < best) { best = key; bx = tx; by = ty; }
                }
            var f = new Fight();
            if (bx < 0) { f.target = "no tile"; return f; }
            var g = OzV11Kit.Place(w, "grunt", Cx(bx), Cx(by), Team.Blue, 1);
            g.MoveSpeed = 0;                                                                    // it stays in the pit (a target, not a walker)
            f.target = "(" + bx + "," + by + ")";
            ulong h = 1469598103934665603UL;
            int ticks = (int)Math.Round(FightSec / ZConst.TICK);
            for (int i = 0; i < ticks; i++)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                {
                    if (!Shot(e)) continue;
                    if (e.objId == gun.Id && e.otherId == g.Id) { f.gunShots++; if (f.firstShot < 0) f.firstShot = w.Time; }
                    if (e.objId == g.Id && e.otherId == gun.Id) f.gruntShots++;
                }
                w.Events.Clear();
                if (i % 50 == 49) h = w.RulesFingerprint(h);
            }
            f.gruntDead = !OzV11Kit.Alive(g);
            f.sig = h.ToString("x16");
            return f;
        }

        // ------------------------------------------------------------------ 3 across the pits
        internal sealed class Pairs { public int low, lowClearOff, lowClearOn, raised, raisedClearOff, raisedClearOn; }

        internal static Pairs AcrossPits(string settings, string l16)
        {
            var mj = NoUnitsL16(l16);
            var off = Make(settings, mj, false); var on = Make(settings, mj, true);
            var p = new Pairs(); var m = on.Map; int r = PairReach >> 4;
            var tiles = new List<(int x, int y)>();
            for (int ty = 0; ty < m.height; ty++) for (int tx = 0; tx < m.width; tx++) if (on.Path.TilePassable(tx, ty, true) && m.ClassAt(tx, ty) != ZClass.Water) tiles.Add((tx, ty));
            var set = new HashSet<long>(tiles.Select(t => (long)t.y * m.width + t.x));
            foreach (var a in tiles)
                for (int dy = 0; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (dy == 0 && dx <= 0) continue;                                       // each pair once
                        if (dx * dx + dy * dy > r * r) continue;
                        int bx = a.x + dx, by = a.y + dy;
                        if (bx < 0 || by < 0 || bx >= m.width || by >= m.height || !set.Contains((long)by * m.width + bx)) continue;
                        if ((dx & 1) != 0 || (dy & 1) != 0) continue;                           // every second tile each way: a quarter of the pairs, the same everywhere
                        int ax = Cx(a.x), ay = Cx(a.y), cx = Cx(bx), cy = Cx(by);
                        bool low = LineCrossesLow(on, ax, ay, cx, cy), raised = LineCrossesRaised(on, ax, ay, cx, cy) || LineCrossesRaised(on, cx, cy, ax, ay);
                        bool c0 = off.Sight.Clear(ax, ay, 0, cx, cy, 0), c1 = on.Sight.Clear(ax, ay, 0, cx, cy, 0);
                        if (raised) { p.raised++; if (c0) p.raisedClearOff++; if (c1) p.raisedClearOn++; }
                        else if (low) { p.low++; if (c0) p.lowClearOff++; if (c1) p.lowClearOn++; }
                    }
            return p;
        }

        // ------------------------------------------------------------------ the report
        public static string Run(string settings, Func<string, string> map, TextWriter progress = null)
        {
            bool was = ZWorld.LowPitsDeviation, los = ZWorld.LosDeviation;
            var sb = new StringBuilder();
            try
            {
                sb.AppendLine("DV205 low pits never block sight or shots: counted on two worlds of each map, DV205 off (as before) and on; DV23 on in both");
                sb.AppendLine("level  impassable  lava(DV152)  low(DV205)  opened now  already open (bridges)  held by a standing rock/hut  other tiles changed  path grid changed  low tiles walkable");
                bool allSame = true, allPath = true, allData = true; int opened16 = 0;
                foreach (var lv in Levels)
                {
                    string mj; try { mj = map(lv); } catch { sb.AppendLine(lv + "    (map not installed)"); continue; }
                    if (string.IsNullOrEmpty(mj)) { sb.AppendLine(lv + "    (map not installed)"); continue; }
                    var r = CountLevel(settings, lv, mj);
                    progress?.WriteLine("# " + lv + " counted");
                    sb.AppendLine(string.Format("{0,-5}  {1,10}  {2,11}  {3,10}  {4,10}  {5,22}  {6,27}  {7,19}  {8,17}  {9,18}", lv, r.imp, r.lava, r.low, r.opened, r.alreadyOpen, r.heldByObject, r.changedNotLow, r.pathDiff, r.lowPassable));
                    allSame &= r.changedNotLow == 0; allPath &= r.pathDiff == 0 && r.lowPassable == 0;
                    var row = ZWorld.LowPitData.FirstOrDefault(d => d.File == ZMapData.FromJson(mj).file);
                    allData &= row.File == null ? r.low == 0 : r.low == row.Count;
                    if (lv == "L16") opened16 = r.opened;
                }
                sb.AppendLine(OzV11Kit.Verdict(allSame, "every tile that is not low is the same wall (or the same open tile) as before on every level: raised rock, cliffs, mountains, city blocks and the map's ring still block"));
                sb.AppendLine(OzV11Kit.Verdict(allPath, "movement unchanged: every low tile is still impassable for robots and vehicles, the path grid is the same on every level"));
                sb.AppendLine(OzV11Kit.Verdict(allData, "the low tiles the world reads are the data's tiles on every level (none on a level without a row)"));
                sb.AppendLine(OzV11Kit.Verdict(opened16 > 0, "level 16: " + opened16 + " impassable tiles of its pits are no wall for sight any more"));

                string l16 = null; try { l16 = map("L16"); } catch { }
                if (!string.IsNullOrEmpty(l16))
                {
                    var s = SightFromGun(settings, l16);
                    progress?.WriteLine("# owner spot sight counted");
                    sb.AppendLine($"L16 owner's spot, the gun at tile ({GunTx},{GunTy}): {s.targets} robot-passable tiles in its reach whose line crosses the pit: clear before {s.clearOff}, clear now {s.clearOn}, still hidden by raised rock {s.shutRaised}, by a standing rock / hut / building {s.shutObject}; opened e.g. {s.sample}");
                    sb.AppendLine(OzV11Kit.Verdict(s.targets > 0 && s.clearOn > s.clearOff && s.clearOn + s.shutRaised + s.shutObject == s.targets, $"L16 owner's spot: the gun sees across / into the pit everywhere in its reach that no raised rock or standing rock hides ({s.clearOn} of {s.targets}; before {s.clearOff})"));
                    var f0 = GunFight(settings, l16, false); var f1 = GunFight(settings, l16, true); var f2 = GunFight(settings, l16, true);
                    var a0 = GunFight(settings, l16, false, 1, true); var a1 = GunFight(settings, l16, true, 1, true); var a2 = GunFight(settings, l16, true, 1, true);
                    progress?.WriteLine("# owner spot fight counted");
                    sb.AppendLine($"L16 owner's spot fight, a red gun on ({GunTx},{GunTy}) and one blue grunt in the pit's floor at {f1.target}, {FightSec:0} s: before: gun shots {f0.gunShots}, grunt shots {f0.gruntShots}, grunt dead {f0.gruntDead}; now: gun shots {f1.gunShots} (first at {f1.firstShot:0.0} s), grunt shots {f1.gruntShots}, grunt dead {f1.gruntDead}");
                    sb.AppendLine($"L16 owner's spot fight, the same gun and one blue grunt ACROSS the pit (north of the ravine's west end) at {a1.target}, {FightSec:0} s: before: gun shots {a0.gunShots}, grunt shots {a0.gruntShots}, grunt dead {a0.gruntDead}; now: gun shots {a1.gunShots} (first at {a1.firstShot:0.0} s), grunt shots {a1.gruntShots}, grunt dead {a1.gruntDead}");
                    sb.AppendLine(OzV11Kit.Verdict(f0.gunShots == 0 && f1.gunShots > 0 && a0.gunShots == 0 && a1.gunShots > 0, $"L16 owner's spot: the gun shoots into and across the pit now (floor {f1.gunShots} shots, across {a1.gunShots}; before {f0.gunShots} / {a0.gunShots})"));
                    sb.AppendLine(OzV11Kit.Verdict(f1.gunShots == f2.gunShots && f1.gruntShots == f2.gruntShots && f1.sig == f2.sig && f1.firstShot == f2.firstShot && a1.sig == a2.sig && a1.gunShots == a2.gunShots,
                        $"deterministic: the same fights twice give the same shots and the same rules fingerprints ({f1.sig} / {f2.sig}, {a1.sig} / {a2.sig})"));
                    var p = AcrossPits(settings, l16);
                    progress?.WriteLine("# pairs counted");
                    sb.AppendLine($"L16 pairs of robot-passable tiles within {PairReach} px (every second tile each way): {p.low} lines cross only low impassable tiles: clear before {p.lowClearOff}, now {p.lowClearOn}; {p.raised} lines cross a raised impassable tile: clear before {p.raisedClearOff}, now {p.raisedClearOn}");
                    sb.AppendLine(OzV11Kit.Verdict(p.low > 0 && p.lowClearOn > p.lowClearOff, $"L16: lines over the pits open ({p.lowClearOn - p.lowClearOff} more clear of {p.low}; still shut: {p.low - p.lowClearOn}, by a standing rock / hut / building / the shuttle)"));
                    sb.AppendLine(OzV11Kit.Verdict(p.raisedClearOn == p.raisedClearOff, $"L16: lines over raised rock stay as they were ({p.raisedClearOn} clear, before {p.raisedClearOff}, of {p.raised})"));
                }
                else sb.AppendLine("FAIL  level 16 map not installed");
            }
            finally { ZWorld.LowPitsDeviation = was; ZWorld.LosDeviation = los; }
            return sb.ToString();
        }

        /// <summary>The parity row's short form: the per-level part and the owner's spot (no pair sweep).</summary>
        public static string Quick(string settings, Func<string, string> map)
        {
            bool was = ZWorld.LowPitsDeviation, los = ZWorld.LosDeviation;
            var sb = new StringBuilder();
            try
            {
                int opened = 0, other = 0, path = 0;
                foreach (var lv in Levels)
                {
                    string mj; try { mj = map(lv); } catch { continue; }
                    if (string.IsNullOrEmpty(mj)) continue;
                    var r = CountLevel(settings, lv, mj); opened += r.opened; other += r.changedNotLow; path += r.pathDiff + r.lowPassable;
                    sb.Append(lv + " " + r.low + " low / " + r.opened + " opened; ");
                }
                sb.AppendLine();
                sb.AppendLine(OzV11Kit.Verdict(other == 0 && path == 0 && opened > 0, "only low tiles open (" + opened + " on all levels), raised walls and movement unchanged"));
                string l16 = null; try { l16 = map("L16"); } catch { }
                if (!string.IsNullOrEmpty(l16))
                {
                    var f0 = GunFight(settings, l16, false, 1, true); var f1 = GunFight(settings, l16, true, 1, true);
                    sb.AppendLine(OzV11Kit.Verdict(f0.gunShots == 0 && f1.gunShots > 0, $"L16 owner's spot: the gun shoots across the pit at {f1.target} ({f1.gunShots} shots in {FightSec:0} s; before {f0.gunShots})"));
                }
            }
            finally { ZWorld.LowPitsDeviation = was; ZWorld.LosDeviation = los; }
            return sb.ToString();
        }
    }
}
