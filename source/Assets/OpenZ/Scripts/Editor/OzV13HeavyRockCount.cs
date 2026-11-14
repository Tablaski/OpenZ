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
    /// DV164 "the heavy tank clears the rock it clips" (Sim/ZObject.HeavyRock.cs), counted headless: no scene, no Play, nothing saved. Menu OpenZ/V1.3/Heavy tank clears rocks: counted
    /// (headless, DV164); off-project on plain .NET by the kit's tools/harness.
    ///  R1 the numbers: the reach at every drawn size against the model; the hull inside the circle the barrels sweep at every size;
    ///  R2 clips and does not: 4 sizes, 4 facings and no facing, 8 bearings, each pixel round the edge, against the sum worked out here;
    ///  R3 a real run on each of the 4 sides: it turns, fires the game's shell, the rock goes, it never moved, it stands idle again, nothing more in 30 s;
    ///  R4 only that rock: a wall of rocks, an empty jeep, a friend, against the same shell fired on an order, whose blast still reaches them;
    ///  R5 never a rock a little further, never a chain;
    ///  R6 an order mid-shot: before the first shell, between the two barrels, the computer's order, a stop;
    ///  R7 an enemy: in range, between two shells, in the agro reach, behind the rock unseen; an enemy or an attack order while the gun turns gets its first shell as soon as
    ///     the same tank with no rock near, and an enemy just before the rock's first shell takes that shell;
    ///  R8 an empty tank, a crew shot out of it;
    ///  R9 every other vehicle, gun and robot in the same spot: nothing;
    ///  R10 idle only: not before the moment, not driving past, not on hold, a hold order while the gun turns, not in the calm start, the computer not held by the calm start;
    ///  R11 both teams and the computer;
    ///  R12 until it is gone; a rock that cannot be hurt;
    ///  R13 the drawn size decides, live;
    ///  R14 the switch: off, nothing, and the same world as a tank with no rock near; on, the same match twice;
    ///  R15 real levels (the menu only): rocks of levels 2, 7, 16, 17, the tile spots that touch them, and how many rocks one tank can take from any spot.
    /// </summary>
    public static class OzV13HeavyRockCount
    {
        static int pass, fail, secPass, secFail; static string firstBad; static StringBuilder sb, detail;
        public static readonly string[] Levels = { "L02", "L07", "L16", "L17" };
        static readonly string[] Others = { "jeep", "light", "medium", ZMTank.Type, "missile_launcher", "apc", "crane", "gun", "gatling", "howitzer", "missile_cannon", "tough", "grunt", "pyro" };
        const int Fx = 300, Fy = 300;                                                                    // the foot of the test rock: tile (18, 18)

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

        // ------------------------------------------------------------------ small worlds
        /// <summary>A plain map with a rock on each (tile x, bottom tile y).</summary>
        static string Map(params (int tx, int ty)[] rocks) => OzV11Kit.Plain(40, 40, rocks.Select(r => ("map_item:rock", r.tx, r.ty - 2, "null")).ToArray());
        static ZWorld Make(string settings, string map, int seed = 1)
        {
            var w = ZWorld.Create(settings, map, seed);
            w.NoGameEnd = true; w.Events.Clear();
            return w;
        }
        static ZObject Rock(ZWorld w, int tx, int ty) => w.Objects.First(o => o.Type == "rock" && o.LocX / 16 == tx && o.LocY / 16 + 2 == ty);
        static int FootY(ZObject r) => r.LocY + ZWorld.RockFootPx;
        /// <summary>A unit with its middle at (cx, cy); heavy facing (fx, fy) in px, 0 = never moved.</summary>
        static ZObject Tank(ZWorld w, int cx, int cy, Team t = Team.Red, float fx = 0f, float fy = 0f, string type = "heavy")
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
        /// <summary>Ticks until test says yes, at most `seconds`; the time it took, or -1.</summary>
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
        static ulong Hash(ZWorld w)
        {
            ulong h = 1469598103934665603UL;
            void M(long v) { unchecked { h = (h ^ (ulong)v) * 1099511628211UL; } }
            foreach (var o in w.Objects) { M(o.Id); M(o.LocX); M(o.LocY); M(o.Health); M(o.KillAt < 0 ? 0 : 1); M(o.Waypoints.Count); M(o.AttackObject != null ? o.AttackObject.Id : -1); }
            M(w.Objects.Count); M(w.Rng.Next(1000000));
            return h;
        }
        /// <summary>The ring by the sum of this check, not the rule's own helper.</summary>
        static void Ring(int cx, int cy, float fx, float fy, int pct, out int rx, out int ry)
        {
            rx = cx; ry = cy; double l = Math.Sqrt((double)fx * fx + (double)fy * fy); if (l < 1e-6) return;
            int back = (3 * pct + 50) / 100;
            rx -= (int)Math.Round(fx / l * back); ry -= (int)Math.Round(fy / l * back);
        }

        // ------------------------------------------------------------------ the count
        public static string Check(string settings, Func<string, string> level, bool full)
        {
            pass = fail = secPass = secFail = 0; firstBad = null; sb = new StringBuilder(); detail = new StringBuilder();
            bool keep = ZWorld.HeavyRockDeviation, calm = ZWorld.CalmStartDeviation, los = ZWorld.LosDeviation; int pct0 = ZWorld.HeavyRockDrawnPct; double idle0 = ZWorld.HeavyRockIdleSeconds, scan0 = ZWorld.HeavyRockScanSeconds;
            ZWorld.HeavyRockDeviation = true; ZWorld.CalmStartDeviation = false; ZWorld.LosDeviation = true; ZWorld.HeavyRockDrawnPct = 90;
            try
            {
                R1(); R2(settings); R3(settings); R4(settings); R5(settings); R6(settings); R7(settings); R8(settings); R9(settings); R10(settings); R11(settings); R12(settings); R13(settings); R14(settings);
                if (full) R15(settings, level);
            }
            finally { ZWorld.HeavyRockDeviation = keep; ZWorld.CalmStartDeviation = calm; ZWorld.LosDeviation = los; ZWorld.HeavyRockDrawnPct = pct0; ZWorld.HeavyRockIdleSeconds = idle0; ZWorld.HeavyRockScanSeconds = scan0; }
            sb.Append(fail == 0 ? "[OpenZ V1.3 heavy rock] PASS " : "[OpenZ V1.3 heavy rock] FAIL ").Append(pass).Append(" of ").Append(pass + fail).AppendLine();
            return sb.ToString();
        }

        static void R1()
        {
            for (int pct = 60; pct <= 100; pct += 5)
            {
                int reach = ZWorld.HeavyRockReachPx(pct), back = ZWorld.HeavyRockRingBackPx(pct);
                double tip = 4.13 * 8 * pct / 100, ring = 0.361 * 8 * pct / 100, hullL = 2.612 * 8 * pct / 100, hullW = 2.03 * 8 * pct / 100;
                Case(Math.Abs(reach - ZWorld.HeavyRockRadiusPx - tip) <= 0.55 && Math.Abs(back - ring) <= 0.65, $"{pct} %: tips {reach - ZWorld.HeavyRockRadiusPx} px against the model's {tip:0.0}, ring {back} px against {ring:0.0}, + the rock's {ZWorld.HeavyRockRadiusPx} = {reach} px");
                Case(Math.Sqrt((hullL + ring) * (hullL + ring) + hullW * hullW) < tip, $"{pct} %: the hull's far corner {Math.Sqrt((hullL + ring) * (hullL + ring) + hullW * hullW):0.0} px from the ring, inside the tips' {tip:0.0}");
            }
            Section("R1 the numbers");
        }

        static void R2(string s)
        {
            double idle = ZWorld.HeavyRockIdleSeconds, scan = ZWorld.HeavyRockScanSeconds;
            ZWorld.HeavyRockIdleSeconds = 0; ZWorld.HeavyRockScanSeconds = 0;
            var faces = new[] { ("east", 1f, 0f), ("west", -1f, 0f), ("north", 0f, -1f), ("south", 0f, 1f), ("not yet moved", 0f, 0f) };
            var bears = new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) };
            foreach (int pct in new[] { 60, 75, 90, 100 })
            {
                ZWorld.HeavyRockDrawnPct = pct;
                int reach = ZWorld.HeavyRockReachPx(pct), bad = 0, yes = 0, no = 0; string first = null;
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, 200, 200);
                foreach (var (name, fx, fy) in faces)
                    foreach (var (bx, by) in bears)
                        for (int d = reach - 3; d <= reach + 3; d++)
                        {
                            double l = Math.Sqrt(bx * bx + by * by);
                            int cx = rock.CenterX + (int)Math.Round(bx * d / l), cy = FootY(rock) + (int)Math.Round(by * d / l);
                            o.FaceX = fx * 9; o.FaceY = fy * 9; o.SetCords(cx - 16, cy - 16); o.Disengage();
                            Run(w, 0.03);
                            Ring(cx, cy, o.FaceX, o.FaceY, pct, out int rx, out int ry);
                            long dx = rock.CenterX - rx, dy = FootY(rock) - ry;
                            bool want = dx * dx + dy * dy <= (long)reach * reach, got = o.HeavyRockOn(rock) && o.AttackObject == rock;
                            if (want) yes++; else no++;
                            if (want != got) { bad++; first ??= $"{name}, bearing ({bx}, {by}), {d} px: wanted {want}, got {got}"; }
                        }
                Case(bad == 0 && yes > 0 && no > 0, $"{pct} %, reach {reach} px: {yes} spots clip, {no} do not, {bad} wrong{(first != null ? ": " + first : "")}");
            }
            ZWorld.HeavyRockIdleSeconds = idle; ZWorld.HeavyRockScanSeconds = scan; ZWorld.HeavyRockDrawnPct = 90;
            Section("R2 clips and does not: 4 sizes, 5 facings, 8 bearings, 7 distances");
        }

        static void R3(string s)
        {
            foreach (var (name, bx, by) in new[] { ("east", 1, 0), ("west", -1, 0), ("south", 0, 1), ("north", 0, -1) })
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18);
                var o = Tank(w, rock.CenterX + bx * 23, FootY(rock) + by * 23, Team.Red, -bx * 9, -by * 9);
                int x0 = o.LocX, y0 = o.LocY; var log = new List<ZEvent>();
                double first = TimeFirst(w, o, 8, log);
                Case(first >= ZWorld.HeavyRockIdleSeconds + ZWorld.HeavyRockTurnSeconds - 0.02 && first <= FirstLook + ZWorld.HeavyRockTurnSeconds + 0.02, $"{name}: the first shell {first:0.00} s after it stopped: {ZWorld.HeavyRockIdleSeconds:0.0} s idle + {ZWorld.HeavyRockTurnSeconds:0.0} s for the gun");
                double gone = Until(w, 3, () => Gone(w, rock), log);
                var aim = log.First(e => e.type == ZEventType.MissileFired);
                Case(gone >= 0 && aim.x == rock.CenterX && aim.y == FootY(rock), $"{name}: the rock gone {gone:0.00} s after the shell, the shell aimed at its foot");
                Case(log.Any(e => e.type == ZEventType.MissileExploded && e.otherId == rock.Id) && log.Any(e => e.type == ZEventType.Death && e.objId == rock.Id && e.otherId == o.Id), $"{name}: the game's own events: fired, burst, the rock's death by this tank");
                Run(w, 30, log);
                Case(o.LocX == x0 && o.LocY == y0 && o.Waypoints.Count == 0 && o.AttackObject == null && !o.IsMoving, $"{name}: it never moved, it stands idle again");
                Case(Shots(log, o) == 2 && w.HeavyRockTargets == 1, $"{name}: {Shots(log, o)} shells in all (the twin barrels' one pair), {w.HeavyRockTargets} rock taken on, nothing more in 30 s");
            }
            Section("R3 a real run, each side");
        }

        static void R4(string s)
        {
            var wall = Enumerable.Range(10, 17).Select(y => (18, y)).ToArray();                            // a wall one tile thick, 17 rocks
            var w = Make(s, Map(wall)); var mid = Rock(w, 18, 18);
            var o = Tank(w, mid.CenterX + 23, FootY(mid), Team.Red);
            var jeep = Tank(w, mid.CenterX - 30, FootY(mid), Team.Neutral, 0f, 0f, "jeep");
            var mate = OzV11Kit.Place(w, "grunt", mid.CenterX + 30, FootY(mid) + 30, Team.Red, 1);
            int reach = ZWorld.HeavyRockReachPx(90); var rocks = w.Objects.Where(r => r.Type == "rock").ToList();
            var clip = rocks.Where(r => { long dx = r.CenterX - o.CenterX, dy = FootY(r) - o.CenterY; return dx * dx + dy * dy <= (long)reach * reach; }).ToList();
            var log = Run(w, 60);
            int gone = rocks.Count(r => Gone(w, r)), wrong = rocks.Count(r => Gone(w, r) != clip.Contains(r));
            Case(gone == clip.Count && wrong == 0, $"a wall of {rocks.Count} rocks, tank touching it: the {clip.Count} it clips are gone, {wrong} others changed");
            Case(rocks.Where(r => !clip.Contains(r)).All(r => r.Health == r.MaxHealth), "every rock it does not clip still has all its health, the one next to a blast too");
            Case(jeep.Health == jeep.MaxHealth, $"an empty jeep beside the blast keeps all its health ({jeep.Health})");
            Case(mate.Health == mate.MaxHealth, "a friend beside it too");
            var w2 = Make(s, Map(wall)); var mid2 = Rock(w2, 18, 18); var next = Rock(w2, 18, 19);
            var far = Tank(w2, mid2.CenterX + 100, FootY(mid2), Team.Red, 0f, 0f);
            OzV11Kit.PlayerAttack(w2, far, mid2); Run(w2, 8);
            Case(next.Destroyed || next.Health < next.MaxHealth || Gone(w2, next), $"the same shell on an order: its blast still reaches the rock next to it (health {next.Health} of {next.MaxHealth}): nothing else about shells moved");
            Section("R4 only that rock");
        }

        static void R5(string s)
        {
            int reach = ZWorld.HeavyRockReachPx(90);
            var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18);
            var o = Tank(w, rock.CenterX + reach + 1, FootY(rock));
            var log = Run(w, 60);
            Case(!Gone(w, rock) && Shots(log, o) == 0, $"a rock 1 px past the reach ({reach + 1} px): left alone for 60 s");
            var w2 = Make(s, Map((18, 18), (15, 18))); var near = Rock(w2, 18, 18); var next = Rock(w2, 15, 18);
            var o2 = Tank(w2, near.CenterX + reach, FootY(near)); log = Run(w2, 60);
            Case(Gone(w2, near) && !Gone(w2, next) && next.Health == next.MaxHealth && Shots(log, o2, next) == 0, "one rock at the reach goes, the next one past it stands for 60 s: no chain");
            var w3 = Make(s, Map((18, 18), (18, 17))); var a = Rock(w3, 18, 18); var b = Rock(w3, 18, 17);
            var o3 = Tank(w3, a.CenterX + 23, FootY(a)); log = new List<ZEvent>();
            double ta = Until(w3, 8, () => Gone(w3, a) || Gone(w3, b), log); double tb = Until(w3, 8, () => Gone(w3, a) && Gone(w3, b), log);
            Case(ta > 0 && tb > ZWorld.HeavyRockIdleSeconds, $"two rocks that both clip: one, then {tb:0.0} s later the other: each one on its own idle moment");
            Section("R5 never a rock a little further, never a chain");
        }

        static void R6(string s)
        {
            int hp = 100000;
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 23, FootY(rock));
                var log = new List<ZEvent>(); double t = Until(w, 5, () => o.AttackObject == rock, log); Run(w, 0.5, log);
                OzV11Kit.PlayerMove(w, o, 500, 300, false); Run(w, 0.02, log);
                Case(t > 0 && o.AttackObject == null, "an order while the gun turns: the rock is dropped in the next tick");
                Run(w, 15, log);
                Case(Shots(log, o) == 0 && rock.Health == rock.MaxHealth && o.CenterX > rock.CenterX + 100, "no shell left, the rock is whole, the tank drove off");
            }
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); rock.MaxHealth = rock.Health = hp; var o = Tank(w, rock.CenterX + 23, FootY(rock));
                var log = new List<ZEvent>(); double t = TimeFirst(w, o, 8, log);
                OzV11Kit.PlayerMove(w, o, 500, 300, false); Run(w, 10, log);
                Case(t > 0 && Shots(log, o) == 1, $"an order between the two barrels: {Shots(log, o)} shell, the second does not leave");
            }
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 23, FootY(rock), Team.Blue);
                w.Bots.Add(new ZBot(w, Team.Blue, 101) { Ignored = true });
                var log = new List<ZEvent>(); double t = Until(w, 5, () => o.AttackObject == rock, log); Run(w, 0.5, log);
                w.SendWaypoints(Team.Blue, o, new[] { ZWaypoint.Move(500, 300, false) }); Run(w, 15, log);
                Case(t > 0 && Shots(log, o) == 0 && rock.Health == rock.MaxHealth, "the computer's order while the gun turns: the same");
            }
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 23, FootY(rock));
                var log = new List<ZEvent>(); Until(w, 5, () => o.AttackObject == rock, log); Run(w, 0.5, log);
                w.SendStop(Team.Red, o); Run(w, 0.02, log); bool dropped = o.AttackObject == null;
                double back = Until(w, 8, () => Gone(w, rock), log);
                Case(dropped && back >= ZWorld.HeavyRockIdleSeconds, $"a stop: dropped at once, and as it stands idle again it comes back to the rock ({back:0.0} s later)");
            }
            Section("R6 an order mid-shot");
        }

        static void R7(string s)
        {
            int reach = 144;
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 23, FootY(rock));
                var log = new List<ZEvent>(); Until(w, 5, () => o.AttackObject == rock, log); Run(w, 0.3, log);
                var e = OzV11Kit.Dummy(w, o.CenterX + reach - 10, o.CenterY);                         // a blue grunt that stands, cannot fire and cannot die
                double t = Until(w, 1, () => o.AttackObject == e, log); Run(w, 6, log);
                Case(t >= 0 && t <= 0.52, $"an enemy in range while the gun turns: the target {t:0.00} s later");
                Case(Shots(log, o, rock) == 0 && Shots(log, o, e) >= 1 && rock.Health == rock.MaxHealth, $"{Shots(log, o, e)} shells at the enemy, none at the rock");
            }
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); rock.MaxHealth = rock.Health = 100000; var o = Tank(w, rock.CenterX + 23, FootY(rock));
                var log = new List<ZEvent>(); TimeFirst(w, o, 8, log); Run(w, 1.0, log); int before = Shots(log, o, rock);
                var e = OzV11Kit.Dummy(w, o.CenterX + reach - 10, o.CenterY);
                double t = Until(w, 1, () => o.AttackObject == e, log); Run(w, 12, log);
                Case(t >= 0 && t <= 0.52 && Shots(log, o, rock) == before, $"an enemy after the first pair: on it in {t:0.00} s, no more shells at the rock ({before} before)");
            }
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); rock.MaxHealth = rock.Health = 100000; var o = Tank(w, rock.CenterX + 23, FootY(rock));
                var log = new List<ZEvent>(); Until(w, 5, () => o.AttackObject == rock, log);
                var e = OzV11Kit.Dummy(w, o.CenterX + reach + 30, o.CenterY);                         // past its range, in its agro reach: Zod's passive check sends it after him
                double t = Until(w, 1, () => o.Waypoints.Count > 0 || o.AttackObject != rock, log); Run(w, 0.02, log);
                Case(t >= 0 && t <= 0.52 && !o.HeavyRockOn(rock), $"an enemy in the agro reach: the rock dropped in {t:0.00} s, the tank goes for him ({(o.Waypoints.Count > 0 ? o.Waypoints[0].mode.ToString() : "no waypoint")})");
            }
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 23, FootY(rock));
                var e = OzV11Kit.Dummy(w, rock.CenterX - 60, FootY(rock));                            // straight behind the rock: no engage across it
                var log = Run(w, 8);
                Case(o.AttackObject == null && Shots(log, o) == 0 && !Gone(w, rock) && w.HeavyRockTargets == 0, "an enemy unseen behind the rock: the rock stays, the gun never turns to it, the wall between them is not shot away");
                e.MoveSpeed = 0; e.KillAt = w.Time; double t = Until(w, 8, () => Gone(w, rock), log);
                Case(t > 0, $"he is gone: the rock goes {t:0.0} s later");
            }
            double FirstAt(ZWorld w, ZObject o, ZObject at, double seconds)
            {
                double t0 = w.Time; int n = (int)Math.Round(seconds / ZConst.TICK);
                for (int i = 0; i < n; i++) { w.Tick(ZConst.TICK); bool f = w.Events.Any(v => v.type == ZEventType.MissileFired && v.objId == o.Id && v.otherId == at.Id); w.Events.Clear(); if (f) return w.Time - t0; }
                return -1;
            }
            foreach (bool order in new[] { false, true })
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 23, FootY(rock));
                Until(w, 5, () => o.AttackObject == rock); Run(w, 0.3); double elapsed = w.Time;
                var e = OzV11Kit.Dummy(w, o.CenterX + 130, o.CenterY); if (order) OzV11Kit.PlayerAttack(w, o, e);
                double with = FirstAt(w, o, e, 6);
                var c = Make(s, Map((3, 3))); var oc = Tank(c, o.CenterX, o.CenterY); Run(c, elapsed);       // the same tank, the same time, no rock near
                var ec = OzV11Kit.Dummy(c, oc.CenterX + 130, oc.CenterY); if (order) OzV11Kit.PlayerAttack(c, oc, ec);
                double ctrl = FirstAt(c, oc, ec, 6);
                Case(with > 0 && ctrl > 0 && with <= ctrl + 0.03, $"{(order ? "an attack order" : "an enemy in range")} while the gun turns: the first shell at him {with:0.00} s, with no rock near {ctrl:0.00} s");
            }
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 23, FootY(rock));
                Until(w, 5, () => o.AttackObject == rock); Run(w, ZWorld.HeavyRockTurnSeconds - 0.15);
                var e = OzV11Kit.Dummy(w, o.CenterX + 130, o.CenterY); var log = Run(w, 6);
                Case(Shots(log, o, rock) == 0 && Shots(log, o, e) >= 1 && rock.Health == rock.MaxHealth, $"an enemy in range 0.15 s before the rock's first shell: {Shots(log, o, rock)} shells at the rock, {Shots(log, o, e)} at him");
            }
            Section("R7 an enemy");
        }

        static void R8(string s)
        {
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 23, FootY(rock), Team.Neutral);
                var log = Run(w, 30);
                Case(o.Drivers.Count == 0 && o.AttackObject == null && Shots(log, o) == 0 && rock.Health == rock.MaxHealth, "an empty heavy tank: nothing in 30 s");
            }
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 23, FootY(rock));
                var log = new List<ZEvent>(); Until(w, 5, () => o.AttackObject == rock, log); Run(w, 0.3, log);
                while (o.Drivers.Count > 0) o.DamageDriverHealth(100000);
                Run(w, 10, log);
                Case(Shots(log, o) == 0 && rock.Health == rock.MaxHealth, "the crew shot out while the gun turns: no shell, the rock is whole");
            }
            Section("R8 an empty tank");
        }

        static void R9(string s)
        {
            foreach (var type in Others)
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18);
                var o = Tank(w, rock.CenterX + 23, FootY(rock), Team.Red, 0f, 0f, type);
                var log = Run(w, 30);
                Case(w.HeavyRockTargets == 0 && Shots(log, o) == 0 && o.AttackObject == null && rock.Health == rock.MaxHealth, $"{type}: nothing in 30 s");
            }
            Section("R9 every other vehicle, gun and robot");
        }

        static void R10(string s)
        {
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 23, FootY(rock));
                Run(w, ZWorld.HeavyRockIdleSeconds - 0.1);
                Case(o.AttackObject == null, $"{ZWorld.HeavyRockIdleSeconds - 0.1:0.0} s idle: not yet");
                Run(w, 1.0);
                Case(o.AttackObject == rock, "a moment more: on the rock");
            }
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 24, FootY(rock) + 140);
                OzV11Kit.PlayerMove(w, o, rock.CenterX + 24, FootY(rock) - 140, false); var log = new List<ZEvent>(); bool on = false;
                double t = Until(w, 60, () => { on |= o.AttackObject != null; return o.Waypoints.Count == 0 && !o.IsMoving; }, log);
                Case(t > 0 && !on && Shots(log, o) == 0, $"driving past a rock it clips: nothing ({t:0.0} s on the way)");
            }
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 23, FootY(rock));
                w.SendHold(Team.Red, o); var log = Run(w, 30);
                Case(o.HoldPosition && o.AttackObject == null && Shots(log, o) == 0, "a hold order: nothing in 30 s (the passive check's own idle)");
            }
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 23, FootY(rock));
                var log = new List<ZEvent>(); double t = Until(w, 5, () => o.AttackObject == rock, log); Run(w, 0.5, log);
                w.SendHold(Team.Red, o); Run(w, 10, log);
                Case(t > 0 && o.HoldPosition && !o.HeavyRockOn(rock) && Shots(log, o) == 0 && rock.Health == rock.MaxHealth, "a hold order while the gun turns: the rock dropped, no shell, the rock whole");
            }
            ZWorld.CalmStartDeviation = true;
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 23, FootY(rock));
                Run(w, 19.0); bool held = o.AttackObject == null; double t = Until(w, 8, () => o.AttackObject == rock);
                Case(held && t >= 1.0, $"the calm start: held for its 20 s, on the rock {19 + t:0.0} s in");
            }
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 23, FootY(rock), Team.Blue);
                w.Bots.Add(new ZBot(w, Team.Blue, 101) { Ignored = true });
                double t = Until(w, 8, () => o.AttackObject == rock);
                Case(t > 0 && t < 5, $"the computer is not held by the calm start ({t:0.0} s)");
            }
            ZWorld.CalmStartDeviation = false;
            Section("R10 idle only");
        }

        static void R11(string s)
        {
            foreach (var (name, team, cpu) in new[] { ("red, the player's", Team.Red, false), ("blue, the player's", Team.Blue, false), ("red, the computer's", Team.Red, true), ("blue, the computer's", Team.Blue, true) })
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 23, FootY(rock), team);
                if (cpu) w.Bots.Add(new ZBot(w, team, 101) { Ignored = true });
                var log = new List<ZEvent>(); double t = Until(w, 8, () => Gone(w, rock), log);
                Case(t > 0 && Shots(log, o, rock) >= 1, $"{name} tank: the rock gone in {t:0.0} s");
            }
            Section("R11 both teams and the computer");
        }

        static void R12(string s)
        {
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); rock.MaxHealth = rock.Health = 12000; var o = Tank(w, rock.CenterX + 23, FootY(rock));
                var log = new List<ZEvent>(); double t = Until(w, 40, () => Gone(w, rock), log); Run(w, 10, log);
                Case(t > 0 && Shots(log, o, rock) > 2 && o.AttackObject == null, $"a rock of 12000: {Shots(log, o, rock)} shells, reload by reload, until it is gone ({t:0.0} s)");
            }
            {
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); rock.MaxHealth = rock.Health = 0; var o = Tank(w, rock.CenterX + 23, FootY(rock));
                var log = Run(w, 30);
                Case(o.AttackObject == null && Shots(log, o) == 0, "a rock that cannot be hurt: nothing");
            }
            Section("R12 until it is gone");
        }

        static void R13(string s)
        {
            foreach (var (d, pct, want) in new[] { (38, 90, true), (38, 60, false), (28, 60, true), (42, 100, true), (42, 90, false) })
            {
                ZWorld.HeavyRockDrawnPct = pct;
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + d, FootY(rock));
                Run(w, FirstLook);
                Case((o.AttackObject == rock) == want, $"a rock {d} px away at {pct} %: {(want ? "clipped" : "not clipped")}");
            }
            {
                ZWorld.HeavyRockDrawnPct = 60;
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 38, FootY(rock));
                Run(w, 6); bool no = o.AttackObject == null;
                ZWorld.HeavyRockDrawnPct = 90; double t = Until(w, 2, () => o.AttackObject == rock);
                Case(no && t >= 0, $"the size changed from 60 to 90 % in the game: {t:0.0} s later");
            }
            ZWorld.HeavyRockDrawnPct = 90;
            Section("R13 the drawn size decides");
        }

        static string Match(string s, bool rock, int seed)
        {
            var w = Make(s, rock ? Map((18, 18), (19, 21), (9, 9)) : Map(), seed);
            var a = Tank(w, 300 + 23, 300, Team.Red); var b = Tank(w, 150, 150, Team.Blue); var c = Tank(w, 250, 120, Team.Red, 0f, 0f, "light");
            OzV11Kit.PlayerMove(w, c, 200, 200);
            var sb2 = new StringBuilder();
            for (int i = 1; i <= 1500; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); if (i % 500 == 0) sb2.Append(Hash(w).ToString("x16")).Append(' '); }
            return sb2.ToString();
        }

        static void R14(string s)
        {
            {
                ZWorld.HeavyRockDeviation = false;
                var w = Make(s, Map((18, 18))); var rock = Rock(w, 18, 18); var o = Tank(w, rock.CenterX + 23, FootY(rock));
                var log = Run(w, 30);
                Case(o.AttackObject == null && Shots(log, o) == 0 && rock.Health == rock.MaxHealth && w.HeavyRockTargets == 0, "rule off: nothing in 30 s");
                string off = Match(s, true, 7);
                ZWorld.HeavyRockDeviation = true;
                string on0 = Match(s, false, 7), off0;
                ZWorld.HeavyRockDeviation = false; off0 = Match(s, false, 7); ZWorld.HeavyRockDeviation = true;
                Case(on0 == off0, "no rock near: the same world with the rule on and off, tick for tick");
                string on1 = Match(s, true, 7), on2 = Match(s, true, 7);
                Case(on1 == on2, "rule on: the same match twice");
                Case(on1 != off, "rule on against off, rocks near: not the same (the rule does play)");
            }
            Section("R14 the switch");
        }

        /// <summary>Real levels with their units taken off: tile spots that touch a rock: it goes, what it does not clip stays.</summary>
        static void R15(string s, Func<string, string> level)
        {
            foreach (var lv in Levels)
            {
                var w = ZWorld.Create(s, level(lv), 1);
                w.NoGameEnd = true; w.Events.Clear();
                foreach (var b in w.Objects.Where(o => o.Kind == ZKind.Building)) b.Owner = Team.Neutral;     // nobody builds, no fort fires: only the tank acts on this map
                foreach (var f in w.Flags) f.Owner = Team.Red;                                                // and it stays where it stands: an idle tank takes a flag near it by itself, and that wins
                foreach (var u in w.Objects.Where(o => o.IsUnit).ToList()) u.KillAt = 0;
                Run(w, 0.05);
                int reach = ZWorld.HeavyRockReachPx(90); long r2 = (long)reach * reach;
                var rocks = w.Objects.Where(o => o.Type == "rock").ToList();
                // how many rocks one tank can take, from any tile spot where it fits
                int spots = 0, most = 0, none = 0, sum = 0; int mx = -1, my = -1;
                for (int ty = 1; ty < w.Map.height - 3; ty++)
                    for (int tx = 1; tx < w.Map.width - 3; tx++)
                    {
                        if (w.Path.WithinImpassable(tx * 16 + 1, ty * 16 + 1, 30, 30, out _, out _, false)) continue;
                        int cx = tx * 16 + 16, cy = ty * 16 + 16, n = 0;
                        foreach (var r in rocks) { long dx = r.CenterX - cx, dy = FootY(r) - cy; if (dx * dx + dy * dy <= r2) n++; }
                        spots++; sum += n; if (n == 0) none++; if (n > most) { most = n; mx = tx; my = ty; }
                    }
                Case(spots > 0, $"{lv}: {rocks.Count} rocks; {spots} tile spots a tank fits on, {none} with no rock in reach; the most on one spot {most} (tile {mx}, {my}), on average {(double)sum / Math.Max(1, spots):0.00}");
                // every rock with a tile spot touching it: can the tank fire at it from there (the game's own line, to the middle of the rock's sprite)?
                var probe = Tank(w, 8, 8); int touched = 0, noLine = 0;
                var spot = new Dictionary<ZObject, (int x, int y)>();
                foreach (var rock in rocks)
                {
                    int bx = rock.LocX / 16, by = rock.LocY / 16 + 2; bool any = false, seen = false;
                    foreach (var (ox, oy) in new[] { (1, 0), (-2, 0), (0, 1), (0, -2) })
                    {
                        if (w.Path.WithinImpassable((bx + ox) * 16 + 1, (by + oy) * 16 + 1, 30, 30, out _, out _, false)) continue;
                        int cx = (bx + ox) * 16 + 16, cy = (by + oy) * 16 + 16;
                        any = true; probe.SetCords(cx - 16, cy - 16);
                        if (probe.WithinAttackRadius(rock)) { seen = true; if (!spot.ContainsKey(rock)) spot[rock] = (cx, cy); }
                    }
                    if (any) touched++;
                    if (any && !seen) noLine++;
                }
                probe.KillAt = w.Time; Run(w, 0.05);
                Case(touched > 0 && noLine * 10 <= touched, $"{lv}: {touched} rocks have a tile spot touching them, {noLine} of them the game's own line never reaches from one (left alone, a rule of every shot)");
                // the first of them: it goes, nothing else is hurt
                int tried = 0, ok = 0, kept = 0; string first = null;
                foreach (var rock in rocks)
                {
                    if (tried >= 12) break;
                    if (!spot.TryGetValue(rock, out var at) || Gone(w, rock) || rock.Destroyed) continue;
                    tried++;
                    var before = rocks.Where(r => !Gone(w, r)).Select(r => (r, r.Health)).ToList();
                    var clip = before.Where(k => { long dx = k.r.CenterX - at.x, dy = FootY(k.r) - at.y; return dx * dx + dy * dy <= r2; }).Select(k => k.r).ToList();
                    var o2 = Tank(w, at.x, at.y, Team.Red);
                    Run(w, 3.5 + 5.0 * clip.Count);
                    bool mine = Gone(w, rock), others = before.Where(k => !clip.Contains(k.r)).All(k => !Gone(w, k.r) && k.r.Health == k.Health);
                    if (mine) ok++; else first ??= $"rock tile ({rock.LocX / 16}, {rock.LocY / 16 + 2}) from ({at.x}, {at.y}) stands; others alive {w.Objects.Count(o => o.IsUnit && !o.Destroyed && o != o2)}, target {(o2.AttackObject != null ? o2.AttackObject.Type : "none")}, order {o2.Waypoints.Count}, rocks taken on {w.HeavyRockTargets}, clip {clip.Count}";
                    if (others) kept++; else first ??= $"from ({at.x}, {at.y}) a rock it does not clip was hurt";
                    o2.KillAt = w.Time; Run(w, 0.05);
                }
                Case(tried > 0 && ok == tried && kept == tried, $"{lv}: {tried} rocks, a tank touching each: {ok} gone, {kept} times nothing else hurt{(first != null ? "; " + first : "")}");
            }
            Section("R15 real levels");
        }

#if UNITY_EDITOR
        [MenuItem("OpenZ/V1.3/Heavy tank clears rocks: counted (headless, DV164)")]
        static void Menu()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.3] heavy rock: leave Play mode first (the check sets the simulation's static switches)."); return; }
            using (OzV11Kit.GameSwitches())
            {
                var r = Check(OzV11Kit.SettingsText, OzV11Kit.MapText, true);
                if (r.Contains("] PASS")) Debug.Log(r); else Debug.LogError(r);
            }
        }

        [MenuItem("OpenZ/V1.3/Heavy tank clears rocks: Play check (level 7 or 16, Play)")]
        static void Play()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] heavy rock check: start level 7 or level 16 in Play first"); return; }
            OzHeavyRockCheck.Run();
            Debug.Log("[OpenZ] heavy rock check started (about a minute, hands off); its report: " + OzHeavyRockCheck.Folder);
        }
#endif
    }
}
