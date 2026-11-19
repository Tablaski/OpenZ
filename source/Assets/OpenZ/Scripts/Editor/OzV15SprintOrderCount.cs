using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5, SPRINT ORDER, rule DV178 (Sim/ZObject.SprintOrder.cs; kit fps_feel): the counted check, headless on the Sim alone, on a plain map,
    /// nothing saved: menu OpenZ/V1.5/Sprint Order: Count (headless, DV178). One line per fact, [OpenZ V1.5 sprint order] PASS n/n. The check
    /// turns the rule's switch as asked and the DV69 calm start off (the scenes act at once), on top of the counted checks' usual game switches
    /// (the parity row's probe runs inside OzV11Kit.GameSwitches), and puts both back.
    /// The scene ("one walk"): a plain 64 x 40-tile map, one red unit (no CPU), one MOVE order 640 px east, 300 ticks (3 s). Every tick the
    /// unit's place, sub-pixel carry, velocity, speed and waypoints are folded into a trace, at the end the world's rules fingerprint and the
    /// next random number.
    ///  S  speed: a grunt, a light tank, a jeep, a heavy tank: with the mark the speed at tick 100 is the plain speed x SprintPct / 100 (to double
    ///     rounding) and the unit went 1.35 .. 1.6 x as far; sprint ticks counted
    ///  N  no change without the mark: the plain walk with the switch ON is byte-identical to the switch OFF (trace, fingerprint, random number)
    ///  O  the switch off: a walk WITH the mark is byte-identical to the plain walk (the rule does nothing in 'Zod exact')
    ///  E  the mark ends with its order: a plain order sent at tick 150: from tick 170 the speed has the plain bits again
    ///  Q  a squad: a grunt squad (3) with the mark: every member went at least 1.3 x as far as without
    ///  R  the same sprint walk twice: the same trace (deterministic)
    ///  C  the source (editor only): the mark is set (".sprint = true") in exactly one file of Scripts outside Editor, View/PlayerInput.FpsFeel.cs
    /// </summary>
    public static class OzV15SprintOrderCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);

        const Team Me = Team.Red;
        public const int GroundW = 64, GroundH = 40, TravelPx = 640, Ticks = 300, SpeedTick = 100, ReorderTick = 150;
        static string groundMap;

        sealed class Hold : IDisposable
        {
            readonly bool so = ZWorld.SprintOrderDeviation, calm = ZWorld.CalmStartDeviation;
            public Hold(bool on) { ZWorld.SprintOrderDeviation = on; ZWorld.CalmStartDeviation = false; }   // the scenes act at once
            public void Dispose() { ZWorld.SprintOrderDeviation = so; ZWorld.CalmStartDeviation = calm; }
        }

        /// <summary>One walk: what it gives.</summary>
        public sealed class Walk
        {
            public ulong trace = 1469598103934665603UL; public int rand, sprintTicks; public double dist, speed, speedAfter; public long speedBits, speedAfterBits;
            public double[] mates = new double[0];
        }

        static void Mix(ref ulong h, long v) { h = (h ^ (ulong)v) * 1099511628211UL; }

        /// <summary>The "one walk" scene. cat "robot" (amount: squad size, 1 = one robot) or "vehicle". reorder: a plain MOVE to the same goal at
        /// ReorderTick (the mark gone).</summary>
        public static Walk OneWalk(string settings, string cat, string type, int amount, bool rule, bool sprint, bool reorder = false, int seed = 3)
        {
            using (new Hold(rule))
            {
                var w = ZWorld.Create(settings, groundMap ??= OzV11Kit.Plain(GroundW, GroundH), seed);
                w.NoGameEnd = true;
                int x0 = 64, y0 = GroundH * 8 - 8;
                var u = cat == "robot" ? w.CreateRobotGroup(type, x0, y0, Me, amount) : w.CreateObject(cat, type, x0, y0 - 8, Me);
                w.Events.Clear();
                var mates = u.Minions.Where(m => m != null).ToList();
                int sx = u.CenterX; var ms = mates.Select(m => m.CenterX).ToArray();
                int gx = u.CenterX + TravelPx, gy = u.CenterY;
                var wp = ZWaypoint.Move(gx, gy); wp.sprint = sprint;
                w.SendWaypoints(Me, u, new[] { wp });
                var r = new Walk();
                for (int i = 0; i < Ticks; i++)
                {
                    if (reorder && i == ReorderTick) w.SendWaypoints(Me, u, new[] { ZWaypoint.Move(gx, gy) });
                    w.Tick(ZConst.TICK); w.Events.Clear();
                    if (i == SpeedTick) { r.speed = u.RealMoveSpeed; r.speedBits = BitConverter.DoubleToInt64Bits(u.RealMoveSpeed); }
                    if (i == ReorderTick + 20) { r.speedAfter = u.RealMoveSpeed; r.speedAfterBits = BitConverter.DoubleToInt64Bits(u.RealMoveSpeed); }
                    Mix(ref r.trace, u.LocX); Mix(ref r.trace, u.LocY); Mix(ref r.trace, BitConverter.DoubleToInt64Bits(u.XOver)); Mix(ref r.trace, BitConverter.DoubleToInt64Bits(u.YOver));
                    Mix(ref r.trace, BitConverter.SingleToInt32Bits(u.Dx)); Mix(ref r.trace, BitConverter.SingleToInt32Bits(u.Dy)); Mix(ref r.trace, BitConverter.DoubleToInt64Bits(u.RealMoveSpeed));
                    Mix(ref r.trace, u.Waypoints.Count);
                    foreach (var m in mates) { Mix(ref r.trace, m.LocX); Mix(ref r.trace, m.LocY); }
                }
                r.dist = u.CenterX - sx;
                r.mates = mates.Select((m, k) => (double)(m.CenterX - ms[k])).ToArray();
                r.sprintTicks = w.SprintTicks;
                r.trace = w.RulesFingerprint(r.trace);
                r.rand = w.Rng.Next(1 << 30);
                return r;
            }
        }

        static readonly (string cat, string type, string name)[] Types = { ("robot", "grunt", "grunt"), ("vehicle", "light", "light tank"), ("vehicle", "jeep", "jeep"), ("vehicle", "heavy", "heavy tank") };

        static void S(string set)
        {
            foreach (var (cat, type, name) in Types)
            {
                var p = OneWalk(set, cat, type, 1, true, false); var s = OneWalk(set, cat, type, 1, true, true);
                double want = p.speed * ZWorld.SprintPct / 100, ratio = p.dist > 0 ? s.dist / p.dist : 0;
                Check(Math.Abs(s.speed - want) < 1e-9 && ratio >= 1.35 && ratio <= 1.6 && s.sprintTicks > 0 && p.sprintTicks == 0,
                      $"S {name}: speed {p.speed:0.000} -> {s.speed:0.000} px/s (x {(p.speed > 0 ? s.speed / p.speed : 0):0.0000}, the rule's x {ZWorld.SprintPct / 100.0:0.00}), {p.dist:0} -> {s.dist:0} px in {Ticks / 100.0:0.0} s (x {ratio:0.00}), {s.sprintTicks} sprint ticks");
            }
        }

        static void N(string set)
        {
            foreach (var (cat, type, name) in Types)
            {
                var off = OneWalk(set, cat, type, 1, false, false); var on = OneWalk(set, cat, type, 1, true, false);
                Check(off.trace == on.trace && off.rand == on.rand && on.sprintTicks == 0, $"N {name}: a plain order with the switch on is the plain walk ({off.trace == on.trace}, random {off.rand == on.rand}, sprint ticks {on.sprintTicks})");
            }
        }

        static void O(string set)
        {
            foreach (var (cat, type, name) in Types)
            {
                var plain = OneWalk(set, cat, type, 1, false, false); var marked = OneWalk(set, cat, type, 1, false, true);
                Check(plain.trace == marked.trace && marked.sprintTicks == 0, $"O {name}: the switch off: the marked order walks as the plain one ({plain.trace == marked.trace}, sprint ticks {marked.sprintTicks})");
            }
        }

        static void E(string set)
        {
            foreach (var (cat, type, name) in Types)
            {
                var plain = OneWalk(set, cat, type, 1, true, false, true); var back = OneWalk(set, cat, type, 1, true, true, true);
                Check(back.speedAfterBits == plain.speedAfterBits && back.speed > plain.speed, $"E {name}: after a plain order the plain speed is back exactly ({back.speedAfter:0.000} = {plain.speedAfter:0.000} px/s; sprinting before it {back.speed:0.000})");
            }
        }

        static void Q(string set)
        {
            var p = OneWalk(set, "robot", "grunt", 3, true, false); var s = OneWalk(set, "robot", "grunt", 3, true, true);
            bool ok = p.mates.Length == 2 && s.mates.Length == 2; var cells = new List<string>();
            for (int i = 0; ok && i < p.mates.Length; i++) { double k = p.mates[i] > 0 ? s.mates[i] / p.mates[i] : 0; ok &= k >= 1.3; cells.Add($"{p.mates[i]:0} -> {s.mates[i]:0} px (x {k:0.00})"); }
            Check(ok && s.dist >= 1.3 * p.dist, $"Q a grunt squad of 3: the leader {p.dist:0} -> {s.dist:0} px, its two members {string.Join(", ", cells)}: the squad sprints together");
        }

        static void R(string set)
        {
            var a = OneWalk(set, "vehicle", "light", 1, true, true); var b = OneWalk(set, "vehicle", "light", 1, true, true);
            Check(a.trace == b.trace && a.rand == b.rand, $"R the same sprint walk twice: the same trace ({a.trace == b.trace}) and random number ({a.rand == b.rand})");
        }

#if UNITY_EDITOR && !OPENZ_HARNESS
        static void C()
        {
            var root = System.IO.Path.Combine(UnityEngine.Application.dataPath, "OpenZ", "Scripts");
            var hits = new List<string>();
            foreach (var f in System.IO.Directory.GetFiles(root, "*.cs", System.IO.SearchOption.AllDirectories))
            {
                var rel = f.Substring(root.Length + 1).Replace('\\', '/');
                if (rel.StartsWith("Editor/")) continue;
                if (System.IO.File.ReadAllText(f).Contains(".sprint = true")) hits.Add(rel);
            }
            Check(hits.Count == 1 && hits[0] == "View/PlayerInput.FpsFeel.cs", $"C the sprint mark is set in {hits.Count} file(s) of Scripts outside Editor: {string.Join(", ", hits)} (the view's FpsSprint only)");
        }
#endif

        // ------------------------------------------------------------------------------------------------ entry points
        public static string Run(string settings)
        {
            pass = fail = 0; sb = new StringBuilder();
            var t0 = DateTime.UtcNow;
            try
            {
                Info($"the rule's factor {ZWorld.SprintPct} % (Sim/ZObject.SprintOrder.cs); scenes: a plain {GroundW} x {GroundH}-tile map, one MOVE order {TravelPx} px east, {Ticks} ticks");
                S(settings); N(settings); O(settings); E(settings); Q(settings); R(settings);
#if UNITY_EDITOR && !OPENZ_HARNESS
                C();
#endif
            }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace); }
            Info($"run time {(DateTime.UtcNow - t0).TotalSeconds:0.0} s (the check's own, not a rule input)");
            sb.AppendLine($"[OpenZ V1.5 sprint order] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

        /// <summary>The Deviations row's probe: a grunt and a light tank, the marked order against the plain one, the switch on and off.</summary>
        public static string Quick(string settings)
        {
            pass = fail = 0; sb = new StringBuilder();
            try
            {
                foreach (var (cat, type, name) in new[] { ("robot", "grunt", "grunt"), ("vehicle", "light", "light tank") })
                {
                    var p0 = OneWalk(settings, cat, type, 1, false, false); var p1 = OneWalk(settings, cat, type, 1, true, false);
                    var s0 = OneWalk(settings, cat, type, 1, false, true); var s1 = OneWalk(settings, cat, type, 1, true, true);
                    double k = p1.speed > 0 ? s1.speed / p1.speed : 0;
                    Check(p0.trace == p1.trace && s0.trace == p0.trace && s1.trace != p1.trace && Math.Abs(k - ZWorld.SprintPct / 100.0) < 1e-9,
                          $"{name}: a plain order unchanged ({p0.trace == p1.trace}), the mark with the switch off unchanged ({s0.trace == p0.trace}), with it on x {k:0.0000}");
                }
            }
            catch (Exception ex) { Check(false, "the probe threw " + ex.GetType().Name + ": " + ex.Message); }
            sb.AppendLine($"[OpenZ V1.5 sprint order quick] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

#if UNITY_EDITOR && !OPENZ_HARNESS
        [UnityEditor.MenuItem("OpenZ/V1.5/Sprint Order: Count (headless, DV178)")]
        static void Menu() => OzV11Kit.RunMenu("sprint order: count (DV178)", () => Run(OzV11Kit.SettingsText));
#endif
    }
}
