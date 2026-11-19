using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit r14_r16: DV164 for the MAMMOTH and the BIZON (Sim/ZObject.BigRock.cs), counted headless: no scene, no Play, nothing saved. Its sections
    /// join the DV164 parity row (Editor/OpenZParity.V13HeavyRock.cs) after the heavy tank's own (Editor/OzV13HeavyRockCount.cs). Menu OpenZ/V1.5/Big
    /// tanks clear rocks: counted (headless, DV164). The game's switches for the big tanks (DV180 / DV181) on, as in a game.
    ///  B1 the numbers: each outline against the drawn model at every size; the Mammoth's hull inside its barrels' circle, the Bizon's not;
    ///  B2 clips and does not: 2 tanks, 3 sizes, 5 facings (4 + not yet moved), 8 bearings, 47 distances each, against the sum worked out here;
    ///  B3 a real run, each tank, each side: it turns, fires the game's shell(s), the rock goes, it never moved, it stands idle again, nothing more in 30 s;
    ///  B4 its hull overlaps a rock (past the barrels' circle): ahead and behind it shoots it, the same distance beside it does not;
    ///  B5 an enemy near: it fights him instead (in range while the gun turns, after a shell, in the agro reach), an enemy unseen behind the rock;
    ///  B6 only that rock, never one a pixel further, never a chain;
    ///  B7 an order or a hold while the gun turns; an empty tank;
    ///  B8 both teams and the computer;
    ///  B9 the switch: off = nothing, and with no rock near the same world on and off, tick for tick; on, the same match twice; on against off differs;
    ///  B10 real levels (the menu only): rocks of levels 2 and 17 a Mammoth / Bizon overlaps from a tile spot: gone, nothing else hurt.
    /// </summary>
    public static class OzV15BigRockCount
    {
        static int pass, fail, secPass, secFail; static string firstBad; static StringBuilder sb, detail;
        static readonly string[] Big = { ZBigTanks.Mammoth, ZBigTanks.Bizon };
        public static readonly string[] Levels = { "L02", "L17" };

        static void Case(bool ok, string what)
        {
            if (ok) { pass++; secPass++; } else { fail++; secFail++; firstBad ??= what; }
            detail.Append(ok ? "  ok    " : "  BAD   ").AppendLine(what);
        }
        static void Section(string name)
        {
            sb.Append(secFail == 0 ? "PASS  " : "FAIL  ").Append(name).Append(": ").Append(secPass).Append(" of ").Append(secPass + secFail);
            if (firstBad != null) sb.Append("; first: ").Append(firstBad);
            sb.AppendLine().Append(detail);
            secPass = secFail = 0; firstBad = null; detail.Clear();
        }

        // ------------------------------------------------------------------ small worlds (the heavy tank's check's own)
        static string Map(params (int tx, int ty)[] rocks) => OzV11Kit.Plain(40, 40, rocks.Select(r => ("map_item:rock", r.tx, r.ty - 2, "null")).ToArray());
        static ZWorld Make(string settings, string map, int seed = 1)
        {
            var w = ZWorld.Create(settings, map, seed);
            w.NoGameEnd = true; w.Events.Clear();
            return w;
        }
        static ZObject Rock(ZWorld w, int tx, int ty) => w.Objects.First(o => o.Type == "rock" && o.LocX / 16 == tx && o.LocY / 16 + 2 == ty);
        static int FootY(ZObject r) => r.LocY + ZWorld.RockFootPx;
        static ZObject Tank(ZWorld w, string type, int cx, int cy, Team t = Team.Red, float fx = 0f, float fy = 0f)
        {
            var o = OzV11Kit.Place(w, type, cx, cy, t, 1); o.FaceX = fx; o.FaceY = fy;
            return o;
        }
        static List<ZEvent> Run(ZWorld w, double seconds, List<ZEvent> log = null)
        {
            log ??= new List<ZEvent>();
            int n = (int)Math.Round(seconds / ZConst.TICK);
            for (int i = 0; i < n; i++) { w.Tick(ZConst.TICK); log.AddRange(w.Events); w.Events.Clear(); }
            return log;
        }
        static double Until(ZWorld w, double seconds, Func<bool> test, List<ZEvent> log = null)
        {
            double t0 = w.Time; int n = (int)Math.Round(seconds / ZConst.TICK);
            for (int i = 0; i < n; i++) { if (test()) return w.Time - t0; w.Tick(ZConst.TICK); log?.AddRange(w.Events); w.Events.Clear(); }
            return -1;
        }
        static int Shots(List<ZEvent> log, ZObject o, ZObject at = null) => log.Count(e => e.type == ZEventType.MissileFired && e.objId == o.Id && (at == null || e.otherId == at.Id));
        static double TimeFirst(ZWorld w, ZObject o, double seconds, List<ZEvent> log)
        {
            double t0 = w.Time; int n = (int)Math.Round(seconds / ZConst.TICK);
            for (int i = 0; i < n; i++) { w.Tick(ZConst.TICK); bool f = w.Events.Any(e => e.type == ZEventType.MissileFired && e.objId == o.Id); log.AddRange(w.Events); w.Events.Clear(); if (f) return w.Time - t0; }
            return -1;
        }
        static bool Gone(ZWorld w, ZObject r) => w.Get(r.Id) == null;
        static double FirstLook => ZWorld.HeavyRockIdleSeconds + 2 * ZWorld.HeavyRockScanSeconds + 0.02;
        static string Name(string t) => t == ZBigTanks.Mammoth ? "Mammoth" : "Bizon";
        static ulong Hash(ZWorld w)
        {
            ulong h = 1469598103934665603UL;
            void M(long v) { unchecked { h = (h ^ (ulong)v) * 1099511628211UL; } }
            foreach (var o in w.Objects) { M(o.Id); M(o.LocX); M(o.LocY); M(o.Health); M(o.KillAt < 0 ? 0 : 1); M(o.Waypoints.Count); M(o.AttackObject != null ? o.AttackObject.Id : -1); }
            M(w.Objects.Count); M(w.Rng.Next(1000000));
            return h;
        }

        // ------------------------------------------------------------------ the outline, worked out here (not the rule's own helpers)
        /// <summary>Full size, px: ring back, barrels, hull ahead / behind / each side of the ring, its farthest point from the ring, from the middle.</summary>
        static int[] Outline(string type) => type == ZBigTanks.Mammoth ? new[] { 4, 35, 35, 22, 20, 35, 31 } : new[] { 2, 20, 34, 26, 20, 36, 34 };
        static int Sz(int v, int pct) => (v * pct + 50) / 100;
        /// <summary>Does a rock's foot at (px, py) clip a tank of this type with its middle at (cx, cy) and heading (fx, fy) (0, 0 = never moved)?</summary>
        static bool Want(string type, int pct, int cx, int cy, float fx, float fy, int px, int py)
        {
            var o = Outline(type).Select(v => Sz(v, pct)).ToArray();
            int back = o[0], barrel = o[1], front = o[2], rear = o[3], half = o[4], far = o[5], farMid = o[6], rr = ZWorld.HeavyRockRadiusPx;
            double l = Math.Sqrt((double)fx * fx + (double)fy * fy);
            int rx = cx, ry = cy;
            if (l < 1e-6)
            {
                long ex = px - cx, ey = py - cy, e2 = ex * ex + ey * ey, rad = Math.Max(barrel, farMid) + rr;
                return e2 <= rad * (long)rad;
            }
            rx -= (int)Math.Round(fx / l * back); ry -= (int)Math.Round(fy / l * back);
            long dx = px - rx, dy = py - ry, d2 = dx * dx + dy * dy;
            if (d2 <= (long)(barrel + rr) * (barrel + rr)) return true;
            if (d2 > (long)(far + rr) * (far + rr)) return false;
            int hx = (int)Math.Round(fx * 1024.0 / l), hy = (int)Math.Round(fy * 1024.0 / l);
            long a = dx * hx + dy * hy, s = dy * hx - dx * hy;
            long ea = a > front * 1024L ? a - front * 1024L : a < -rear * 1024L ? a + rear * 1024L : 0;
            long es = s > half * 1024L ? s - half * 1024L : s < -half * 1024L ? s + half * 1024L : 0;
            return ea * ea + es * es <= (long)rr * rr * 1024L * 1024L;
        }

        // ------------------------------------------------------------------ the count
        public static string Check(string settings, Func<string, string> level, bool full)
        {
            pass = fail = secPass = secFail = 0; firstBad = null; sb = new StringBuilder(); detail = new StringBuilder();
            bool keep = ZWorld.HeavyRockDeviation, calm = ZWorld.CalmStartDeviation, los = ZWorld.LosDeviation, mam = ZWorld.MammothDeviation, biz = ZWorld.BizonDeviation;
            int pct0 = ZWorld.HeavyRockDrawnPct; double idle0 = ZWorld.HeavyRockIdleSeconds, scan0 = ZWorld.HeavyRockScanSeconds;
            ZWorld.HeavyRockDeviation = true; ZWorld.CalmStartDeviation = false; ZWorld.LosDeviation = true; ZWorld.HeavyRockDrawnPct = 90;
            ZWorld.MammothDeviation = true; ZWorld.BizonDeviation = true;
            try
            {
                B1(); B2(settings); B3(settings); B4(settings); B5(settings); B6(settings); B7(settings); B8(settings); B9(settings);
                if (full && level != null) B10(settings, level);
            }
            finally
            {
                ZWorld.HeavyRockDeviation = keep; ZWorld.CalmStartDeviation = calm; ZWorld.LosDeviation = los; ZWorld.MammothDeviation = mam; ZWorld.BizonDeviation = biz;
                ZWorld.HeavyRockDrawnPct = pct0; ZWorld.HeavyRockIdleSeconds = idle0; ZWorld.HeavyRockScanSeconds = scan0;
            }
            sb.Append(fail == 0 ? "[OpenZ V1.5 big tank rock] PASS " : "[OpenZ V1.5 big tank rock] FAIL ").Append(pass).Append(" of ").Append(pass + fail).AppendLine();
            return sb.ToString();
        }

        static void B1()
        {
            // the models in game metres (kit r14_r16 tools/measure_glb.py): ring back, barrels (turret's farthest point), hull ahead / behind / side of the ring, far from the ring, from the middle
            var model = new Dictionary<string, double[]> { [ZBigTanks.Mammoth] = new[] { 0.468, 4.386, 4.331, 2.721, 2.501, 4.372, 3.908 }, [ZBigTanks.Bizon] = new[] { 0.300, 2.495, 4.191, 3.282, 2.491, 4.548, 4.293 } };
            string[] what = { "ring back", "barrels", "hull ahead", "hull behind", "hull each side", "hull farthest", "farthest from the middle" };
            foreach (var t in Big)
                for (int pct = 60; pct <= 100; pct += 10)
                {
                    var g = ZWorld.BigRockShapeOf(t, pct);
                    int[] got = { g.Back, g.Barrel, g.Front, g.Rear, g.Half, g.Far, g.FarMid };
                    int worst = -1; double dev = 0;
                    for (int i = 0; i < 7; i++) { double d = Math.Abs(got[i] - model[t][i] * 8 * pct / 100); if (d > dev) { dev = d; worst = i; } }
                    Case(dev < 1.0, $"{Name(t)} {pct} %: {g}; largest step from the model {dev:0.00} px ({what[worst]}; whole px at full size, then scaled and rounded: under 1 px = 0.125 m)");
                }
            var m = ZWorld.MammothRock; var b = ZWorld.BizonRock;
            Case(m.Far <= m.Barrel && m.FarMid <= m.Barrel, $"the Mammoth's hull lies inside its barrels' circle ({m.Far} <= {m.Barrel} px): the heavy tank's one test");
            Case(b.Far > b.Barrel && b.Front > b.Barrel, $"the Bizon's hull reaches past its short barrel ({b.Front} px ahead, {b.Far} px at most, the barrels {b.Barrel} px): its hull's box counts");
            Section("B1 the numbers");
        }

        static void B2(string s)
        {
            ZWorld.HeavyRockIdleSeconds = 0; ZWorld.HeavyRockScanSeconds = 0;
            var faces = new[] { ("east", 1f, 0f), ("west", -1f, 0f), ("north", 0f, -1f), ("south", 0f, 1f), ("not yet moved", 0f, 0f) };
            var bears = new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) };
            foreach (var t in Big)
                foreach (int pct in new[] { 60, 90, 100 })
                {
                    ZWorld.HeavyRockDrawnPct = pct;
                    int bad = 0, yes = 0, no = 0, helper = 0; string first = null;
                    var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, t, 120, 120);
                    foreach (var (name, fx, fy) in faces)
                        foreach (var (bx, by) in bears)
                            for (int d = 10; d <= 56; d++)
                            {
                                double l = Math.Sqrt(bx * bx + by * by);
                                int cx = rock.CenterX + (int)Math.Round(bx * d / l), cy = FootY(rock) + (int)Math.Round(by * d / l);
                                o.FaceX = fx * 9; o.FaceY = fy * 9; o.SetCords(cx - 16, cy - 16); o.Disengage();
                                Run(w, 0.03);
                                bool want = Want(t, pct, cx, cy, o.FaceX, o.FaceY, rock.CenterX, FootY(rock)), got = o.HeavyRockOn(rock) && o.AttackObject == rock;
                                if (o.BigRockClipsNow(rock) != want) helper++;
                                if (want) yes++; else no++;
                                if (want != got) { bad++; first ??= $"{name}, bearing ({bx}, {by}), {d} px: wanted {want}, got {got}"; }
                            }
                    Case(bad == 0 && helper == 0 && yes > 0 && no > 0, $"{Name(t)} {pct} %: {yes} spots clip, {no} do not, {bad} wrong{(helper > 0 ? $", the rule's own sum differs {helper} times" : "")}{(first != null ? ": " + first : "")}");
                }
            ZWorld.HeavyRockIdleSeconds = 2.0; ZWorld.HeavyRockScanSeconds = 0.5; ZWorld.HeavyRockDrawnPct = 90;
            Section("B2 clips and does not: 2 tanks, 3 sizes, 5 facings, 8 bearings, 47 distances");
        }

        static void B3(string s)
        {
            foreach (var t in Big)
                foreach (var (name, bx, by) in new[] { ("east", 1, 0), ("west", -1, 0), ("south", 0, 1), ("north", 0, -1) })
                {
                    var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18);
                    var o = Tank(w, t, rock.CenterX + bx * 23, FootY(rock) + by * 23, Team.Red, -bx * 9, -by * 9);
                    int x0 = o.LocX, y0 = o.LocY; var log = new List<ZEvent>();
                    double first = TimeFirst(w, o, 8, log);
                    Case(first >= ZWorld.HeavyRockIdleSeconds + ZWorld.HeavyRockTurnSeconds - 0.02 && first <= FirstLook + ZWorld.HeavyRockTurnSeconds + 0.02, $"{Name(t)} {name}: the first shell {first:0.00} s after it stopped ({ZWorld.HeavyRockIdleSeconds:0.0} s idle + {ZWorld.HeavyRockTurnSeconds:0.0} s for the gun)");
                    double gone = Until(w, 6, () => Gone(w, rock), log); int byGone = Shots(log, o);
                    bool fired = log.Any(e => e.type == ZEventType.MissileFired); var aim = fired ? log.First(e => e.type == ZEventType.MissileFired) : default;
                    Case(gone >= 0 && fired && aim.x == rock.CenterX && aim.y == FootY(rock), $"{Name(t)} {name}: the rock gone {gone:0.00} s after the shell, the shell aimed at its foot");
                    Case(log.Any(e => e.type == ZEventType.MissileExploded && e.otherId == rock.Id) && log.Any(e => e.type == ZEventType.Death && e.objId == rock.Id && e.otherId == o.Id), $"{Name(t)} {name}: the game's own events: fired, burst, the rock's death by this tank");
                    Run(w, 30, log);
                    bool pair = t == ZBigTanks.Mammoth;
                    Case(o.LocX == x0 && o.LocY == y0 && o.Waypoints.Count == 0 && o.AttackObject == null && !o.IsMoving, $"{Name(t)} {name}: it never moved, it stands idle again");
                    Case((pair ? Shots(log, o) == 2 : Shots(log, o) == byGone && byGone >= 1) && w.HeavyRockTargets == 1,
                         $"{Name(t)} {name}: {Shots(log, o)} shell(s) in all ({(pair ? "its two barrels one-two" : $"one barrel, reload by reload until the rock of {rock.MaxHealth} was gone")}), {w.HeavyRockTargets} rock taken on, nothing more in 30 s");
                }
            Section("B3 a real run, each tank, each side");
        }

        static void B4(string s)
        {
            foreach (var t in Big)
            {
                var g = ZWorld.BigRockShapeOf(t, 90);
                // heading east: the ring g.Back px behind the middle; a rock's foot 6 px past the hull's front edge (ahead), past its rear edge (behind), and the same
                // distance from the ring straight beside it
                int ahead = g.Front + 6, behind = g.Rear + 6;
                bool inAhead = false, inBeside = true;
                foreach (var (where, ax, ay) in new[] { ("ahead", ahead, 0), ("behind", -behind, 0), ("beside, as far as ahead", 0, ahead), ("beside, as far as behind", 0, behind) })
                {
                    var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18);
                    int rx = rock.CenterX - ax, ry = FootY(rock) - ay;                                    // the ring
                    var o = Tank(w, t, rx + g.Back, ry, Team.Red, 9f, 0f);
                    bool want = Want(t, 90, o.CenterX, o.CenterY, 9f, 0f, rock.CenterX, FootY(rock));
                    if (where == "ahead") inAhead = want; else if (where == "beside, as far as ahead") inBeside = want;
                    var log = new List<ZEvent>(); double gone = Until(w, 12, () => Gone(w, rock), log);
                    bool shot = gone >= 0;
                    double dist = Math.Sqrt((double)ax * ax + (double)ay * ay);
                    Case(shot == want, $"{Name(t)} heading east, a rock {where} ({dist:0} px from the ring; the barrels' circle {g.Barrel + ZWorld.HeavyRockRadiusPx} px): {(shot ? $"shot, gone in {gone:0.0} s" : "left alone for 12 s")}{(shot == want ? "" : $", wanted {(want ? "shot" : "left alone")}")}");
                }
                if (t == ZBigTanks.Bizon) Case(inAhead && !inBeside, $"the Bizon: a rock its long hull stands on ahead ({ahead} px from the ring) is shot, the same distance beside its hull is not (past its {g.Barrel} px barrel + the rock)");
                else Case(inAhead, $"the Mammoth: a rock {ahead} px ahead of its ring (past its hull's front) is within its barrels' circle: shot");
            }
            {   // the owner's case: a Mammoth parked with its hull on a rock (its middle 18 px from the rock's foot, heading north), no enemy near
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18);
                var o = Tank(w, ZBigTanks.Mammoth, rock.CenterX + 18, FootY(rock) - 4, Team.Red, 0f, -9f);
                var log = new List<ZEvent>(); double gone = Until(w, 8, () => Gone(w, rock), log);
                Case(gone > 0 && Shots(log, o, rock) >= 1, $"a Mammoth parked on a rock (18 px beside its middle), no enemy near: the rock shot away in {gone:0.0} s");
            }
            Section("B4 the hull overlaps a rock");
        }

        static void B5(string s)
        {
            foreach (var t in Big)
            {
                {
                    var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, t, rock.CenterX + 23, FootY(rock)); int range = o.AttackRadius;
                    var log = new List<ZEvent>(); Until(w, 5, () => o.AttackObject == rock, log); Run(w, 0.3, log);
                    var e = OzV11Kit.Dummy(w, o.CenterX + range - 12, o.CenterY);
                    double tt = Until(w, 1, () => o.AttackObject == e, log); Run(w, 6, log);
                    Case(tt >= 0 && tt <= 0.52 && Shots(log, o, rock) == 0 && Shots(log, o, e) >= 1 && rock.Health == rock.MaxHealth, $"{Name(t)}: an enemy in range while the gun turns: on him in {tt:0.00} s, {Shots(log, o, e)} shell(s) at him, none at the rock");
                }
                {
                    var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); rock.MaxHealth = rock.Health = 100000; var o = Tank(w, t, rock.CenterX + 23, FootY(rock)); int range = o.AttackRadius;
                    var log = new List<ZEvent>(); TimeFirst(w, o, 8, log); Run(w, 1.0, log); int before = Shots(log, o, rock);
                    var e = OzV11Kit.Dummy(w, o.CenterX + range - 12, o.CenterY);
                    double tt = Until(w, 1, () => o.AttackObject == e, log); Run(w, 12, log);
                    Case(before >= 1 && tt >= 0 && tt <= 0.52 && Shots(log, o, rock) == before, $"{Name(t)}: an enemy after its first shell(s): on him in {tt:0.00} s, no more shells at the rock ({before} before)");
                }
                {
                    var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, t, rock.CenterX + 23, FootY(rock)); int range = o.AttackRadius;
                    var log = new List<ZEvent>(); Until(w, 5, () => o.AttackObject == rock, log);
                    var e = OzV11Kit.Dummy(w, o.CenterX + range + 30, o.CenterY);                       // past its range, in its agro reach
                    double tt = Until(w, 1, () => o.Waypoints.Count > 0 || o.AttackObject != rock, log); Run(w, 0.02, log);
                    Case(tt >= 0 && tt <= 0.52 && !o.HeavyRockOn(rock) && rock.Health == rock.MaxHealth, $"{Name(t)}: an enemy in the agro reach: the rock dropped in {tt:0.00} s, it goes for him ({(o.Waypoints.Count > 0 ? o.Waypoints[0].mode.ToString() : "no waypoint")})");
                }
                {
                    var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, t, rock.CenterX + 23, FootY(rock));
                    var e = OzV11Kit.Dummy(w, rock.CenterX - 60, FootY(rock));                              // straight behind the rock
                    var log = Run(w, 8);
                    Case(o.AttackObject == null && Shots(log, o) == 0 && !Gone(w, rock) && w.HeavyRockTargets == 0, $"{Name(t)}: an enemy unseen behind the rock: the rock stays, the wall between them is not shot away");
                    e.MoveSpeed = 0; e.KillAt = w.Time; double tt = Until(w, 8, () => Gone(w, rock), log);
                    Case(tt > 0, $"{Name(t)}: he is gone: the rock goes {tt:0.0} s later");
                }
            }
            Section("B5 an enemy near: it fights");
        }

        static void B6(string s)
        {
            foreach (var t in Big)
            {
                {
                    var wall = Enumerable.Range(10, 17).Select(y => (18, y)).ToArray();
                    var w = Make(s, Map(wall)); var mid = Rock(w, 18, 18);
                    var o = Tank(w, t, mid.CenterX + 23, FootY(mid), Team.Red, -9f, 0f);
                    var jeep = Tank(w, "jeep", mid.CenterX - 30, FootY(mid), Team.Neutral);
                    var rocks = w.Objects.Where(r => r.Type == "rock").ToList();
                    var clip = rocks.Where(r => Want(t, 90, o.CenterX, o.CenterY, o.FaceX, o.FaceY, r.CenterX, FootY(r))).ToList();
                    Run(w, 60);
                    int gone = rocks.Count(r => Gone(w, r)), wrong = rocks.Count(r => Gone(w, r) != clip.Contains(r));
                    Case(gone == clip.Count && wrong == 0 && rocks.Where(r => !clip.Contains(r)).All(r => r.Health == r.MaxHealth) && jeep.Health == jeep.MaxHealth,
                         $"{Name(t)} touching a wall of {rocks.Count} rocks: the {clip.Count} it clips are gone, {wrong} others changed, the rest and an empty jeep beside unhurt");
                }
                {
                    var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18);
                    int far = 0; for (int d = 10; d <= 80; d++) if (Want(t, 90, rock.CenterX + d, FootY(rock), 0f, 0f, rock.CenterX, FootY(rock))) far = d;
                    var o = Tank(w, t, rock.CenterX + far + 1, FootY(rock));
                    var log = Run(w, 60);
                    Case(far > 0 && !Gone(w, rock) && Shots(log, o) == 0, $"{Name(t)} (not yet moved): a rock 1 px past its reach ({far + 1} px from its middle): left alone for 60 s");
                }
            }
            Section("B6 only that rock, never one further");
        }

        static void B7(string s)
        {
            foreach (var t in Big)
            {
                {
                    var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, t, rock.CenterX + 23, FootY(rock));
                    var log = new List<ZEvent>(); double tt = Until(w, 5, () => o.AttackObject == rock, log); Run(w, 0.5, log);
                    OzV11Kit.PlayerMove(w, o, 500, 300, false); Run(w, 0.02, log);
                    bool dropped = o.AttackObject == null;
                    Run(w, 15, log);
                    Case(tt > 0 && dropped && Shots(log, o) == 0 && rock.Health == rock.MaxHealth && o.CenterX > rock.CenterX + 63, $"{Name(t)}: an order while the gun turns: dropped in the next tick, no shell, the rock whole, it drove off");
                }
                {
                    var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, t, rock.CenterX + 23, FootY(rock));
                    w.SendHold(Team.Red, o); var log = Run(w, 30);
                    Case(o.HoldPosition && o.AttackObject == null && Shots(log, o) == 0, $"{Name(t)}: a hold order: nothing in 30 s");
                }
                {
                    var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, t, rock.CenterX + 23, FootY(rock), Team.Neutral);
                    var log = Run(w, 30);
                    Case(o.Drivers.Count == 0 && o.AttackObject == null && Shots(log, o) == 0 && rock.Health == rock.MaxHealth, $"{Name(t)}: empty: nothing in 30 s");
                }
            }
            Section("B7 orders win, an empty tank");
        }

        static void B8(string s)
        {
            foreach (var t in Big)
                foreach (var (name, team, cpu) in new[] { ("red, the player's", Team.Red, false), ("blue, the player's", Team.Blue, false), ("red, the computer's", Team.Red, true), ("blue, the computer's", Team.Blue, true) })
                {
                    var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, t, rock.CenterX + 23, FootY(rock), team);
                    if (cpu) w.Bots.Add(new ZBot(w, team, 101) { Ignored = true });
                    var log = new List<ZEvent>(); double tt = Until(w, 8, () => Gone(w, rock), log);
                    Case(tt > 0 && Shots(log, o, rock) >= 1, $"{Name(t)}, {name}: the rock gone in {tt:0.0} s");
                }
            Section("B8 both teams and the computer");
        }

        static string Match(string s, bool rocks, int seed)
        {
            var w = Make(s, rocks ? Map((18, 18), (24, 30), (9, 9)) : Map(), seed);                       // one side only: no fight, the rule alone plays
            Tank(w, ZBigTanks.Mammoth, 18 * 16 + 8 + 27, 18 * 16 + 8, Team.Red, -9f, 0f);                // its ring 23 px from the rock's foot
            Tank(w, ZBigTanks.Bizon, 24 * 16 + 8 + 36, 30 * 16 + 8, Team.Red, -9f, 0f);                  // the hull's front on the rock, past the barrel
            Tank(w, ZBigTanks.Bizon, 9 * 16 + 8, 9 * 16 + 8 + 30, Team.Red);                              // not moved yet
            var c = Tank(w, "light", 250, 120, Team.Red); OzV11Kit.PlayerMove(w, c, 200, 200);
            var sb2 = new StringBuilder();
            for (int i = 1; i <= 1500; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); if (i % 500 == 0) sb2.Append(Hash(w).ToString("x16")).Append(' '); }
            return sb2.ToString();
        }

        static void B9(string s)
        {
            foreach (var t in Big)
            {
                ZWorld.HeavyRockDeviation = false;
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, t, rock.CenterX + 23, FootY(rock));
                var log = Run(w, 30);
                ZWorld.HeavyRockDeviation = true;
                Case(o.AttackObject == null && Shots(log, o) == 0 && rock.Health == rock.MaxHealth && w.HeavyRockTargets == 0, $"{Name(t)}: the switch off: nothing in 30 s");
            }
            ZWorld.HeavyRockDeviation = false; string off = Match(s, true, 7), off0 = Match(s, false, 7);
            ZWorld.HeavyRockDeviation = true; string on0 = Match(s, false, 7), on1 = Match(s, true, 7), on2 = Match(s, true, 7);
            Case(on0 == off0, "no rock near: the same world with the switch on and off, tick for tick (a Mammoth, 2 Bizons, a light tank driving)");
            Case(on1 == on2, "the switch on: the same match twice");
            Case(on1 != off, "the switch on against off, rocks near: not the same (the rule plays)");
            Section("B9 the switch");
        }

        /// <summary>Real levels with their units taken off: from a tile spot where a Mammoth / Bizon (heading east) overlaps a rock it clips: that rock goes,
        /// nothing it does not clip is hurt.</summary>
        static void B10(string s, Func<string, string> level)
        {
            foreach (var lv in Levels)
                foreach (var t in Big)
                {
                    string map = level(lv); if (map == null) { Case(false, $"{lv}: no map"); continue; }
                    var w = ZWorld.Create(s, map, 1);
                    w.NoGameEnd = true; w.Events.Clear();
                    foreach (var b in w.Objects.Where(o => o.Kind == ZKind.Building)) b.Owner = Team.Neutral;
                    foreach (var f in w.Flags) f.Owner = Team.Red;
                    foreach (var u in w.Objects.Where(o => o.IsUnit).ToList()) u.KillAt = 0;
                    Run(w, 0.05);
                    var rocks = w.Objects.Where(o => o.Type == "rock").ToList();
                    int tried = 0, ok = 0, kept = 0; string first = null;
                    foreach (var rock in rocks)
                    {
                        if (tried >= 8) break;
                        if (Gone(w, rock) || rock.Destroyed) continue;
                        int bx = rock.LocX / 16, by = rock.LocY / 16 + 2, cx = -1, cy = -1;
                        foreach (var (ox, oy) in new[] { (1, 0), (-2, 0), (0, 1), (0, -2) })
                        {
                            if (w.Path.WithinImpassable((bx + ox) * 16 + 1, (by + oy) * 16 + 1, 30, 30, out _, out _, false)) continue;
                            cx = (bx + ox) * 16 + 16; cy = (by + oy) * 16 + 16; break;
                        }
                        if (cx < 0) continue;
                        var before = rocks.Where(r => !Gone(w, r)).Select(r => (r, r.Health)).ToList();
                        var clip = before.Where(k => Want(t, 90, cx, cy, 9f, 0f, k.r.CenterX, FootY(k.r))).Select(k => k.r).ToList();
                        if (!clip.Contains(rock)) continue;
                        var probe = Tank(w, t, cx, cy, Team.Red, 9f, 0f);
                        if (!probe.WithinAttackRadius(rock)) { probe.KillAt = w.Time; Run(w, 0.05); continue; }   // the game's own line never reaches it from here (a rule of every shot)
                        tried++;
                        Run(w, 4.0 + 6.5 * clip.Count);
                        bool mine = Gone(w, rock), others = before.Where(k => !clip.Contains(k.r)).All(k => !Gone(w, k.r) && k.r.Health == k.Health);
                        if (mine) ok++; else first ??= $"rock tile ({bx}, {by}) from ({cx}, {cy}) stands; target {(probe.AttackObject != null ? probe.AttackObject.Type : "none")}, order {probe.Waypoints.Count}, rocks taken on {w.HeavyRockTargets}";
                        if (others) kept++; else first ??= $"from ({cx}, {cy}) a rock it does not clip was hurt";
                        probe.KillAt = w.Time; Run(w, 0.05);
                    }
                    Case(tried > 0 && ok == tried && kept == tried, $"{lv}, {Name(t)}: {tried} rocks it overlaps from a tile spot: {ok} gone, {kept} times nothing else hurt{(first != null ? "; " + first : "")}");
                }
            Section("B10 real levels");
        }

#if UNITY_EDITOR
        [MenuItem("OpenZ/V1.5/Big tanks clear rocks: counted (headless, DV164)")]
        static void Menu()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] big tank rock: leave Play mode first (the check sets the simulation's static switches)."); return; }
            using (OzV11Kit.GameSwitches())
            {
                var r = Check(OzV11Kit.SettingsText, OzV11Kit.MapText, true);
                if (r.Contains("] PASS")) Debug.Log(r); else Debug.LogError(r);
            }
        }
#endif
    }
}
