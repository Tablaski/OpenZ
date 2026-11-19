using System;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Sim alone (the playground's map UE_grid, no scene, nothing saved). Menu OpenZ/V1.5/Unit Editor/Boxes: Count (headless, DV194).
    ///  B0 a world with no box: no table, every call answers nothing, 60 s of an L02 match place none;
    ///  B1 one box: 2 x 2 tiles, level 0, every tile closed and destroyable, a shot across it blocked, the obstacle's hit points;
    ///  B2 stacking: three on one spot = levels 0, 1, 2; a brick half over the column = level 3 (its overhang tile closed); one half over that = 4;
    ///     a stack is at most 6 high (the 7th refused "high");
    ///  B3 refusals on free ground as the obstacle's: a unit, the flag, the holo obstacle, the map's edge;
    ///  B4 a blast hurts one level: aimed at the middle box only the middle; aimed at nothing the floor level only; the NUKE all three;
    ///  B5 the middle box destroyed: in the same tick the top box falls to level 1, the bottom box untouched, every tile of the column still closed
    ///     in that tick and after the dead box left the world;
    ///  B6 the bottom box destroyed under a column with bricks: every box above drops one level, the bricks keep resting on the column;
    ///  B7 removal (the Unit editor's Delete, also while paused): RemoveNow + BoxSettle: the box above falls at once; a lone box's tiles open;
    ///  B8 a lone box destroyed: its tiles open (pathing gets the floor back);
    ///  B9 a heavy tank on the attack order at the TOP box of three: it destroys it with the rules' own shots, the two below keep all their hit
    ///     points; at the MIDDLE box: it falls, the top box drops to level 1;
    ///  B10 the same scene twice: the same levels, hit points and tick (deterministic: integers only, no random draw in the box rules).
    /// </summary>
    public static partial class OzV15UeBoxesCount                                          // 1.5 N29 (kit ue_boxes_range_n29): lines B11-B16 in OzV15UeBoxesCount.Range.cs
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);

        static ZWorld World(string settings, string map, int seed)
        {
            var w = ZWorld.Create(settings, map, seed);
            w.NoGameEnd = true; w.FlagHoldSec = 1.0;
            w.Events.Clear();
            return w;
        }

        static void Run(ZWorld w, double sec) { int n = (int)Math.Round(sec / ZConst.TICK); for (int i = 0; i < n; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); } }
        static void Step(ZWorld w) { w.Tick(ZConst.TICK); w.Events.Clear(); }
        static ZObject Box(ZWorld w, int tx, int ty) { var o = w.PlaceBox(tx, ty, ZWorld.PlaygroundBoxHp, out _, out string why); if (o == null) throw new Exception($"no box at ({tx}, {ty}): {why}"); return o; }
        static bool Alive(ZObject o) => o != null && !o.Destroyed && o.KillAt < 0;
        static bool Closed(ZWorld w, ZObject o) { int tx = o.LocX >> 4, ty = o.LocY >> 4; for (int j = ty; j < ty + 2; j++) for (int i = tx; i < tx + 2; i++) if (w.Path.TilePassable(i, j, true) || w.Path.TilePassable(i, j, false)) return false; return true; }
        static bool ClosedAt(ZWorld w, int tx, int ty) { for (int j = ty; j < ty + 2; j++) for (int i = tx; i < tx + 2; i++) if (w.Path.TilePassable(i, j, true) || w.Path.TilePassable(i, j, false)) return false; return true; }
        static bool OpenAt(ZWorld w, int tx, int ty) { for (int j = ty; j < ty + 2; j++) for (int i = tx; i < tx + 2; i++) if (!w.Path.TilePassable(i, j, true) || !w.Path.TilePassable(i, j, false)) return false; return true; }
        static void Blast(ZWorld w, ZObject at, int targetId, int damage, bool nuke = false)
        {
            w.AddMissileForTest(new ZMissile { x = at.CenterX, y = at.CenterY, radius = 40, damage = damage, team = nuke ? Team.Neutral : Team.Red, targetId = targetId, nuke = nuke, explodeTime = w.Time });
            Step(w); Step(w);                                                                // a missile added between ticks joins the list in the first, bursts in the second
        }

        public static string Run(string settings, string map, string l02Map)
        {
            pass = fail = 0; sb = new StringBuilder();
            try { B0(settings, l02Map); B1(settings, map); B2(settings, map); B3(settings, map); B4(settings, map); B5(settings, map); B6(settings, map); B7(settings, map); B8(settings, map); B9(settings, map); B10(settings, map); RangeChecks(settings, map, l02Map); GroupChecks(settings, map, l02Map); }   // 1.5 N29: B11-B16 (OzV15UeBoxesCount.Range.cs); N29b: B17-B25 (OzV15UeBoxesCount.Group.cs)
            catch (Exception e) { Check(false, "the run stopped: " + e); }
            return "[OpenZ UeBoxes] " + (fail == 0 ? "PASS " : "FAIL ") + pass + " of " + (pass + fail) + "\n" + sb;
        }

        /// <summary>The parity row's short run (the same lines).</summary>
        public static string Quick(string settings, string map, string l02Map) => Run(settings, map, l02Map);

        static void B0(string s, string l02)
        {
            var w = World(s, l02, 1);
            Run(w, 60.0);
            Check(w.Boxes.Count == 0 && w.BoxesPlaced == 0 && w.BoxSettles == 0 && w.BoxSettle() == 0 && w.BoxLevelOf(1) == -1 && !w.IsBox(w.Objects.Count > 0 ? w.Objects[0] : null),
                  $"B0 L02, 60 s of the match: no box, no table, BoxSettle answers 0, BoxLevelOf -1 ({w.Objects.Count} objects)");
        }

        static void B1(string s, string m)
        {
            var w = World(s, m, 1);
            var b = Box(w, 40, 40);
            bool dest = w.Path.HasDestroyableBarrier(40, 40) && w.Path.HasDestroyableBarrier(41, 41);
            bool shot = w.EngageBarrierBetween(36 * 16 + 8, 41 * 16, 46 * 16 + 8, 41 * 16);
            Check(b.WidthPix == 32 && b.HeightPix == 32 && b.Type == ZWorld.WreckType && b.Owner == Team.Neutral && w.BoxLevelOf(b) == 0 && Closed(w, b) && dest && shot
                  && b.MaxHealth == ZWorld.PlaygroundBoxHp && b.Health == b.MaxHealth && b.IsDestroyableImpass,
                  $"B1 one box: 2 x 2 tiles, level {w.BoxLevelOf(b)}, tiles closed + destroyable {dest}, a shot across it blocked {shot}, {b.Health} HP");
        }

        static void B2(string s, string m)
        {
            var w = World(s, m, 1);
            var a0 = Box(w, 40, 40); var a1 = Box(w, 40, 40); var a2 = Box(w, 40, 40);
            var d = Box(w, 41, 40); var e = Box(w, 42, 40);
            Check(w.BoxLevelOf(a0) == 0 && w.BoxLevelOf(a1) == 1 && w.BoxLevelOf(a2) == 2 && w.BoxLevelOf(d) == 3 && w.BoxLevelOf(e) == 4 && ClosedAt(w, 41, 40) && ClosedAt(w, 42, 40),
                  $"B2 three on one spot: levels {w.BoxLevelOf(a0)} {w.BoxLevelOf(a1)} {w.BoxLevelOf(a2)}; a brick half over them {w.BoxLevelOf(d)}; one half over that {w.BoxLevelOf(e)}; the overhang tiles closed");
            var w2 = World(s, m, 1); int top = -1; for (int k = 0; k < ZWorld.BoxLevels; k++) top = w2.BoxLevelOf(Box(w2, 60, 60));
            var no = w2.PlaceBox(60, 60, ZWorld.PlaygroundBoxHp, out int lv7, out string why7);
            Check(top == ZWorld.BoxLevels - 1 && no == null && why7 == "high", $"B2 a stack of {ZWorld.BoxLevels}: top level {top}; the next refused ({why7})");
        }

        static void B3(string s, string m)
        {
            var w = World(s, m, 1);
            var g = OzV11Kit.Place(w, "grunt", 30 * 16 + 8, 30 * 16 + 8, Team.Red, 1);
            var f = w.CreateObject("map_item", "flag", 50 * 16, 30 * 16, Team.Neutral);
            var ob = w.PlaceObstacle(70, 30, 3, ZWorld.PlaygroundObstacleHp, out _);
            string w1, w2, w3, w4;
            bool r1 = w.BoxSpotFree(29, 29, out _, out w1), r2 = w.BoxSpotFree(49, 30, out _, out w2), r3 = w.BoxSpotFree(71, 31, out _, out w3), r4 = w.BoxSpotFree(0, 30, out _, out w4);
            bool ok = w.BoxSpotFree(90, 30, out int lv, out _);
            Check(g != null && f != null && ob != null && !r1 && w1 == "unit" && !r2 && w2 == "flag" && !r3 && w3 == "obstacle" && !r4 && w4 == "edge" && ok && lv == 0,
                  $"B3 refused on a unit ({w1}), the flag ({w2}), the holo obstacle ({w3}), the edge ({w4}); free ground takes it (level {lv})");
        }

        static void B4(string s, string m)
        {
            var w = World(s, m, 1);
            var a0 = Box(w, 40, 40); var a1 = Box(w, 40, 40); var a2 = Box(w, 40, 40);
            Blast(w, a1, a1.Id, 600);
            bool mid = a0.Health == a0.MaxHealth && a1.Health < a1.MaxHealth && a2.Health == a2.MaxHealth;
            int h1 = a1.Health;
            Blast(w, a1, -1, 600);
            bool floor = a0.Health < a0.MaxHealth && a1.Health == h1 && a2.Health == a2.MaxHealth;
            int h0 = a0.Health, h2 = a2.Health;
            Blast(w, a2, a2.Id, 600, true);
            bool all = !Alive(a0) && !Alive(a1) && !Alive(a2);
            Check(mid, $"B4 aimed at the middle box: only it is hurt (HP {a0.MaxHealth}/{h1}/{a2.MaxHealth})");
            Check(floor, $"B4 aimed at nothing: only the floor level is hurt (HP {h0}/{h1}/{h2})");
            Check(all, "B4 the NUKE: all three destroyed");
        }

        static void B5(string s, string m)
        {
            var w = World(s, m, 1);
            var a0 = Box(w, 40, 40); var a1 = Box(w, 40, 40); var a2 = Box(w, 40, 40);
            int falls0 = w.BoxFalls;
            Blast(w, a1, a1.Id, 30000);
            bool tick = !Alive(a1) && Alive(a0) && Alive(a2) && a0.Health == a0.MaxHealth && a2.Health == a2.MaxHealth && w.BoxLevelOf(a2) == 1 && w.BoxLevelOf(a0) == 0 && ClosedAt(w, 40, 40);
            Step(w);
            bool after = w.Get(a1.Id) == null && ClosedAt(w, 40, 40) && w.BoxLevelOf(a2) == 1;
            Run(w, 1.0);
            Check(tick, $"B5 the middle box destroyed: the same tick the top box is on level {w.BoxLevelOf(a2)}, the bottom untouched, the tiles closed");
            Check(after && ClosedAt(w, 40, 40) && w.BoxFalls == falls0 + 1, $"B5 the dead box left the world: the tiles stay closed, {w.BoxFalls - falls0} fall counted");
        }

        static void B6(string s, string m)
        {
            var w = World(s, m, 1);
            var a0 = Box(w, 40, 40); var a1 = Box(w, 40, 40); var a2 = Box(w, 40, 40);
            var d = Box(w, 41, 40); var e = Box(w, 42, 40);
            Blast(w, a0, a0.Id, 30000);
            Check(!Alive(a0) && w.BoxLevelOf(a1) == 0 && w.BoxLevelOf(a2) == 1 && w.BoxLevelOf(d) == 2 && w.BoxLevelOf(e) == 3 && ClosedAt(w, 40, 40) && ClosedAt(w, 42, 40),
                  $"B6 the bottom box destroyed: levels {w.BoxLevelOf(a1)} {w.BoxLevelOf(a2)}, bricks {w.BoxLevelOf(d)} {w.BoxLevelOf(e)}; every tile still closed");
        }

        static void B7(string s, string m)
        {
            var w = World(s, m, 1);
            var a0 = Box(w, 40, 40); var a1 = Box(w, 40, 40);
            var lone = Box(w, 60, 40);
            w.RemoveNow(a0); int f1 = w.BoxSettle();
            w.RemoveNow(lone); w.BoxSettle();
            Check(f1 == 1 && w.BoxLevelOf(a1) == 0 && ClosedAt(w, 40, 40) && OpenAt(w, 60, 40) && w.Boxes.Count == 1,
                  $"B7 removal: the box above falls at once (level {w.BoxLevelOf(a1)}), its tiles stay closed; a lone box's tiles open {OpenAt(w, 60, 40)}");
        }

        static void B8(string s, string m)
        {
            var w = World(s, m, 1);
            var b = Box(w, 40, 40);
            Blast(w, b, b.Id, 30000);
            bool now = !Alive(b) && OpenAt(w, 40, 40);
            Step(w);
            Check(now && OpenAt(w, 40, 40) && w.Get(b.Id) == null && w.Boxes.Count == 0, "B8 a lone box destroyed: its tiles open in that tick and stay open");
        }

        static void B9(string s, string m)
        {
            for (int pick = 2; pick >= 1; pick--)
            {
                var w = World(s, m, 1);
                var a = new[] { Box(w, 60, 50), Box(w, 60, 50), Box(w, 60, 50) };
                var tank = OzV11Kit.Place(w, "heavy", 60 * 16 + 16, 50 * 16 - 9 * 16, Team.Red);
                var t = a[pick];
                bool can = tank.CanAttackObject(t);
                OzV11Kit.PlayerAttack(w, tank, t);
                double sec = 0; while (sec < 400.0 && Alive(t)) { Step(w); sec += ZConst.TICK; }
                bool others = true; for (int k = 0; k < 3; k++) if (k != pick && (a[k].Health != a[k].MaxHealth || !Alive(a[k]))) others = false;
                string lv = string.Join(" ", Array.ConvertAll(a, x => Alive(x) ? w.BoxLevelOf(x).ToString() : "x"));
                if (pick == 2) Check(can && !Alive(t) && others && w.BoxLevelOf(a[0]) == 0 && w.BoxLevelOf(a[1]) == 1,
                                     $"B9 a heavy tank at the TOP box: destroyed in {sec:0.0} s, the two below keep all their HP (levels {lv})");
                else Check(can && !Alive(t) && others && w.BoxLevelOf(a[2]) == 1 && w.BoxLevelOf(a[0]) == 0,
                           $"B9 a heavy tank at the MIDDLE box: destroyed in {sec:0.0} s, the top box fell to level {w.BoxLevelOf(a[2])}, the others keep all their HP (levels {lv})");
            }
        }

        static string Scene(string s, string m)
        {
            var w = World(s, m, 7);
            var a = new List<ZObject> { Box(w, 50, 50), Box(w, 50, 50), Box(w, 50, 50), Box(w, 51, 50), Box(w, 52, 50), Box(w, 70, 50) };
            var tank = OzV11Kit.Place(w, "heavy", 50 * 16 + 16, 50 * 16 - 9 * 16, Team.Red);
            OzV11Kit.PlayerAttack(w, tank, a[1]);
            Run(w, 90.0);
            Blast(w, a[5], -1, 30000); Run(w, 1.0);
            var sb2 = new StringBuilder();
            foreach (var o in a) sb2.Append(Alive(o) ? w.BoxLevelOf(o) : -1).Append('/').Append(o.Health).Append(' ');
            sb2.Append("t=").Append(w.TickIndex).Append(" falls=").Append(w.BoxFalls).Append(" settles=").Append(w.BoxSettles);
            for (int j = 48; j < 56; j++) for (int i = 48; i < 74; i++) sb2.Append(w.Path.TilePassable(i, j, true) ? '1' : '0');
            return sb2.ToString();
        }

        static void B10(string s, string m)
        {
            string x = Scene(s, m), y = Scene(s, m);
            Check(x == y, "B10 the same scene twice: the same levels, hit points, tick and tiles (" + (x.Length > 90 ? x.Substring(0, 90) + " ..." : x) + ")");
        }
    }
}
