using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.4, PREMIUM DRIVER DODGE, rule DV173 (Sim/ZObject.PremiumDodge.cs): the counted check, headless on the Sim alone, on a plain map, nothing
    /// saved: menu OpenZ/V1.4/Premium Dodge: Count (headless, DV173), and off-project on the editor's Mono (the kit's tools/harness). One line per
    /// fact, [OpenZ V1.4 premium dodge] PASS n/n. The check turns the rule's switch on as View/ZGame.cs does at a level start and the DV69 calm
    /// start off (the scenes act at once), on top of the counted checks' usual game switches, and puts both back.
    /// The scene ("one shell"): a plain 64 x 40-tile map; red (no CPU) has one vehicle in the open, blue (a CPU that gives no orders of its own)
    /// a howitzer `range` px east of it, ordered to fire at it; the scene runs until that first shell has gone off (+ 0.2 s). Every tick the
    /// vehicle's position, sub-pixel carry, velocity, speed and health are folded into a trace; at the end the world's rules fingerprint and
    /// the next random number. The same seed gives the same shell and the same direction of Zod's step aside, so a grunt and a premium driver
    /// are compared shot for shot.
    ///  T  the four types (light, medium, heavy tank, mech) with each robot type as the driver (grunt, psycho, sniper, tough, pyro, laser; and
    ///     a hero squad's Brad, a grunt): rule ON against rule OFF: a grunt (and Brad): the trace byte-identical, no premium tick; every other
    ///     driver: the trace differs, premium ticks, the speed of the step aside x the factor (to float rounding) at the same tick; every row:
    ///     the vehicle really stepped aside in both runs (review 6 Nov: an 'identical' with no step aside proves nothing)
    ///  V  every other vehicle (jeep, APC, crane, missile launcher) with a tough driver: ON against OFF byte-identical, no premium tick, and it
    ///     really stepped aside in both runs
    ///  P  the speed after the step aside: the plain speed back exactly (the same bits as the plain speed the factor was last put on)
    ///  S  a driver change while the factor is still in (review 6 Nov): a step aside planned then uses the plain speed, and the next tick
    ///     takes the factor off
    ///  X  the DV171 path: a grunt-driven light tank, EXIT DRIVER, a tough ordered in: premium; a grunt ordered in instead: not
    ///  N  a neutral (empty) tank, and the switch off with a tough driver: nothing changes
    ///  R  the same premium scene twice: the same trace (deterministic)
    ///  M  the measure (INFO lines + one bound): for the four types, ranges 80 / 120 / 160 / 192 px, seeds 1..Seeds (150): the mean damage a
    ///     grunt-driven and a premium-driven tank takes from the one shell and its mean distance from the blast, for the factors 1.03 / 1.05 /
    ///     1.08 (the kit) / 1.10; PASS ('subtle') when with the kit's factor the four types take less howitzer damage than with a grunt, at
    ///     most MaxCutAll (15 %) less over the four ranges, and no shell saves more than MaxSavedHp (6 %) of the tank's own health
    /// </summary>
    public static class OzV14PremiumDodgeCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);

        const Team Me = Team.Red, Cpu = Team.Blue;
        public static readonly string[] Types = { "light", ZMTank.Type, "heavy", ZMTank.Mech };
        static readonly string[] Others = { "jeep", "apc", "crane", "missile_launcher" };
        static readonly string[] Drivers = { "grunt", "brad", "psycho", "sniper", "tough", "pyro", "laser" };
        static string Name(string t) => t == "light" ? "light tank" : t == ZMTank.Type ? "medium tank" : t == "heavy" ? "heavy tank" : t == ZMTank.Mech ? "mech" : t;
        public static int Seeds = 150;

        public const int GroundW = 64, GroundH = 40;
        static string groundMap;

        sealed class Hold : IDisposable
        {
            readonly bool pd = ZWorld.PremiumDodgeDeviation, calm = ZWorld.CalmStartDeviation, ve = ZWorld.VehicleExitDeviation; readonly double mul = ZWorld.PremiumDodgeSpeedMul;
            public Hold(bool on) { ZWorld.PremiumDodgeDeviation = on; ZWorld.CalmStartDeviation = false; ZWorld.VehicleExitDeviation = true; }   // as View/ZGame.cs at a start; the scenes act at once
            public void Dispose() { ZWorld.PremiumDodgeDeviation = pd; ZWorld.CalmStartDeviation = calm; ZWorld.VehicleExitDeviation = ve; ZWorld.PremiumDodgeSpeedMul = mul; }
        }

        /// <summary>One shell at one vehicle: what it gives.</summary>
        public sealed class Shot
        {
            public ulong trace = 1469598103934665603UL; public bool fired, exploded; public int hp0, hp1, rand, premiumTicks, plans, settles;
            public double blastDist = -1, lastBase = -1, speedAfter = -1; public long dodgeTick = -1; public string driverType;
            public bool boostExact = true, afterSeen; public int boostedTicks; public double flight;
            public List<double> speeds = new List<double>();
            public double Share(int fullDamage) => fullDamage > 0 ? (double)(hp0 - hp1) / fullDamage : 0;
        }

        static void Mix(ref ulong h, long v) { h = (h ^ (ulong)v) * 1099511628211UL; }

        /// <summary>The "one shell" scene. driver: a robot type (the vehicle's DriverType set before the first tick, as RobotEnterObject sets
        /// it), "brad" (Alan and Brad's squad, ordered in), or null (the vehicle keeps its built-in grunt).</summary>
        public static Shot OneShell(string settings, string type, string driver, int range, int seed, bool rule, double mul = -1, Func<ZWorld, ZObject, ZObject> prepare = null)
        {
            using (new Hold(rule))
            {
                if (mul > 0) ZWorld.PremiumDodgeSpeedMul = mul;
                var w = ZWorld.Create(settings, groundMap ??= OzV11Kit.Plain(GroundW, GroundH), seed);
                w.NoGameEnd = true;
                w.Bots.Add(new ZBot(w, Cpu, seed + 101) { Ignored = true });
                int ox = GroundW * 8 - 100, oy = GroundH * 8;
                ZObject v;
                if (prepare != null) v = prepare(w, null);
                else
                {
                    v = w.CreateObject("vehicle", type, ox - 16, oy - 16, driver == "brad" ? Team.Neutral : Me);
                    if (driver == "brad")
                    {   // a hero squad's Brad drives (a grunt by type): an empty vehicle, the squad ordered in
                        var sq = w.CreateHeroSquad(ZHeroSquads.AlanBrad, Me, new[] { (ox - 64) / 16, (oy - 8) / 16, (ox - 64) / 16, (oy + 24) / 16 });
                        w.SendWaypoints(Me, sq, new[] { new ZWaypoint { mode = WpMode.Enter, refId = v.Id, x = v.CenterX, y = v.CenterY } });
                        for (int i = 0; i < 2000 && !(v.Owner == Me && v.Drivers.Count > 0); i++) { w.Tick(ZConst.TICK); w.Events.Clear(); }
                    }
                    else if (driver != null) v.DriverType = driver;
                }
                var how = w.CreateObject("cannon", "howitzer", v.CenterX + range - 16, v.CenterY - 16, Cpu);
                w.Events.Clear();
                var s = new Shot { hp0 = v.Health, driverType = v.DriverType };
                OzV11Kit.CpuAttack(w, how, v);
                for (int i = 0; i < 3000; i++)
                {
                    w.Tick(ZConst.TICK);
                    foreach (var e in w.Events)
                    {
                        if (e.type == ZEventType.MissileFired && e.objId == how.Id && !s.fired) { s.fired = true; s.hp0 = v.Health; s.flight = e.value; }
                        if (e.type == ZEventType.MissileExploded && e.objId == how.Id && !s.exploded)
                        {
                            s.exploded = true; double dx = e.x - v.CenterX, dy = e.y - v.CenterY; s.blastDist = Math.Sqrt(dx * dx + dy * dy);
                        }
                    }
                    w.Events.Clear();
                    bool dodging = v.Waypoints.Count > 0 && v.Waypoints[0].mode == WpMode.Dodge;
                    double sp = Math.Sqrt((double)v.Dx * v.Dx + (double)v.Dy * v.Dy);
                    if (s.fired) s.speeds.Add(dodging ? sp : -1);
                    if (dodging && s.dodgeTick < 0 && sp > 0 && i > 0) s.dodgeTick = w.TickIndex;
                    Mix(ref s.trace, v.LocX); Mix(ref s.trace, v.LocY); Mix(ref s.trace, BitConverter.DoubleToInt64Bits(v.XOver)); Mix(ref s.trace, BitConverter.DoubleToInt64Bits(v.YOver));
                    Mix(ref s.trace, BitConverter.SingleToInt32Bits(v.Dx)); Mix(ref s.trace, BitConverter.SingleToInt32Bits(v.Dy)); Mix(ref s.trace, BitConverter.DoubleToInt64Bits(v.RealMoveSpeed));
                    Mix(ref s.trace, v.Health); Mix(ref s.trace, v.Waypoints.Count);
                    if (v.PremiumDodgeBoosted)
                    {   // the factor in: the speed is exactly the plain one x the factor (the same bits)
                        s.boostedTicks++; s.lastBase = v.PremiumDodgeBaseSpeed;
                        if (BitConverter.DoubleToInt64Bits(v.RealMoveSpeed) != BitConverter.DoubleToInt64Bits(v.PremiumDodgeBaseSpeed * ZWorld.PremiumDodgeSpeedMul)) s.boostExact = false;
                    }
                    else if (s.boostedTicks > 0 && !s.afterSeen) { s.afterSeen = true; s.speedAfter = v.RealMoveSpeed; }   // the first tick with the factor taken off
                    if (s.exploded && s.speeds.Count > 20 && s.speeds.Skip(s.speeds.Count - 20).All(x => x < 0)) break;
                }
                s.hp1 = v.Destroyed ? 0 : v.Health;
                s.trace = w.RulesFingerprint(s.trace);
                s.rand = w.Rng.Next(1 << 30);
                s.premiumTicks = w.PremiumDodgeTicks; s.plans = w.PremiumDodgePlans; s.settles = w.PremiumDodgeSettles;
                return s;
            }
        }

        // ------------------------------------------------------------------------------------------------ T, V, P, N, R
        static void T(string set)
        {
            foreach (var type in Types)
            {
                var rows = new List<string>(); bool ok = true;
                foreach (var d in Drivers)
                {
                    var off = OneShell(set, type, d, 160, 3, false); var on = OneShell(set, type, d, 160, 3, true);
                    bool premium = d != "grunt" && d != "brad";
                    bool shot = off.fired && on.fired && off.exploded && on.exploded && on.driverType == (d == "brad" ? "grunt" : d) && off.dodgeTick >= 0 && on.dodgeTick >= 0;
                    bool same = off.trace == on.trace && off.rand == on.rand && off.hp1 == on.hp1;
                    double ratio = Ratio(off, on);
                    bool good = shot && (premium ? !same && on.premiumTicks > 0 && on.plans > 0 && Math.Abs(ratio - ZWorld.PremiumDodgeSpeedMul) < 1e-5
                                               : same && on.premiumTicks == 0 && on.plans == 0 && off.premiumTicks == 0);
                    ok &= good;
                    rows.Add($"{d}{(premium ? "" : " (grunt)")}: {(off.dodgeTick >= 0 && on.dodgeTick >= 0 ? "stepped aside" : "NO STEP ASIDE")}, {(same ? "identical" : "differs")}, premium ticks {on.premiumTicks}, plans {on.plans}{(premium ? $", dodge speed x {ratio:0.000000}" : "")}, hp {off.hp0}->{off.hp1} / {on.hp1}{(good ? "" : " <-- WRONG")}");
                }
                Check(ok, $"{Name(type)}, one howitzer shell at 160 px, rule ON against OFF: " + string.Join("; ", rows));
            }
        }

        /// <summary>The step aside's speed in the ON run over the OFF run at the same dodge tick (after its first tick: the factor goes in when it moves).</summary>
        static double Ratio(Shot off, Shot on)
        {
            for (int i = 2; i < Math.Min(off.speeds.Count, on.speeds.Count); i++)
                if (off.speeds[i] > 0 && on.speeds[i] > 0 && off.speeds[i - 1] > 0 && on.speeds[i - 1] > 0) return on.speeds[i] / off.speeds[i];
            return 0;
        }

        static void V(string set)
        {
            var rows = new List<string>(); bool ok = true;
            foreach (var type in Others)
            {
                var off = OneShell(set, type, "tough", 160, 3, false); var on = OneShell(set, type, "tough", 160, 3, true);
                bool same = off.trace == on.trace && off.rand == on.rand && off.hp1 == on.hp1 && on.premiumTicks == 0 && on.plans == 0;
                bool stepped = off.dodgeTick >= 0 && on.dodgeTick >= 0;
                ok &= same && on.fired && stepped;
                rows.Add($"{type}: {(same ? "identical" : "DIFFERS")} (fired {on.fired}, {(stepped ? "stepped aside" : "NO STEP ASIDE")}, premium ticks {on.premiumTicks})");
            }
            Check(ok, "every other vehicle with a tough driver, one howitzer shell, rule ON against OFF byte-identical: " + string.Join("; ", rows));
        }

        static void P(string set)
        {
            var rows = new List<string>(); bool ok = true;
            foreach (var type in Types)
            {
                var on = OneShell(set, type, "tough", 192, 5, true);
                bool exact = on.afterSeen && BitConverter.DoubleToInt64Bits(on.speedAfter) == BitConverter.DoubleToInt64Bits(on.lastBase);
                ok &= exact && on.boostExact && on.settles > 0 && on.premiumTicks > 0;
                rows.Add($"{Name(type)}: {on.boostedTicks} ticks at exactly the plain speed x the factor ({on.boostExact}), factor taken off {on.settles} time(s), "
                         + $"the speed then {on.speedAfter:0.######} px/s = the plain {on.lastBase:0.######} ({exact}){(exact && on.boostExact ? "" : " <-- WRONG")}");
            }
            Check(ok, "after the step aside the plain speed is back exactly (no factor left in it): " + string.Join("; ", rows));
        }

        static void X(string set)
        {
            foreach (var who in new[] { "tough", "grunt" })
            {
                var on = OneShell(set, "light", null, 160, 7, true, -1, (w, _) =>
                {
                    int ox = GroundW * 8 - 100, oy = GroundH * 8;
                    var v = w.CreateObject("vehicle", "light", ox - 16, oy - 16, Me);
                    for (int i = 0; i < 50; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); }
                    bool exit = w.Eject(Me, v);
                    var r = w.CreateRobotGroup(who, ox - 72, oy - 8, Me, 1);
                    for (int i = 0; i < 400; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); }
                    w.SendWaypoints(Me, r, new[] { new ZWaypoint { mode = WpMode.Enter, refId = v.Id, x = v.CenterX, y = v.CenterY } });
                    for (int i = 0; i < 2000 && !(v.Owner == Me && v.Drivers.Count > 0); i++) { w.Tick(ZConst.TICK); w.Events.Clear(); }
                    if (!exit) v.DriverType = "?";
                    return v;
                });
                bool ok = who == "tough" ? on.driverType == "tough" && on.premiumTicks > 0 : on.driverType == "grunt" && on.premiumTicks == 0 && on.plans == 0;
                Check(ok && on.fired, $"the DV171 path: a grunt-driven light tank, EXIT DRIVER, a {who} ordered in: driver {on.driverType}, premium ticks {on.premiumTicks} ({(who == "tough" ? "premium" : "not premium")})");
            }
        }

        static void N(string set)
        {
            var off = OneShell(set, "light", "tough", 160, 3, false);
            var off2 = OneShell(set, "light", null, 160, 3, false);
            Check(off.premiumTicks == 0 && off.plans == 0 && off.trace == off2.trace, $"the switch off: a tough-driven light tank steps aside as a grunt-driven one (the same trace {off.trace == off2.trace}, premium ticks {off.premiumTicks})");
            using (new Hold(true))
            {
                var w = ZWorld.Create(set, groundMap ??= OzV11Kit.Plain(GroundW, GroundH), 1);
                var v = w.CreateObject("vehicle", "heavy", 300, 300, Team.Neutral); v.DriverType = "tough";
                Check(!v.PremiumDriven, $"a neutral (empty) heavy tank is never premium ({v.PremiumDriven})");
            }
        }

        /// <summary>S (review 6 Nov): a tough-driven light tank steps aside with the factor in; its driver becomes a grunt before its next tick (as when a
        /// tough is sniped or climbs out mid-step and a grunt gets in within the same tick): a step aside planned now uses the plain speed (not the
        /// factor still in RealMoveSpeed), and the next tick takes the factor off.</summary>
        static void S(string set)
        {
            using (new Hold(true))
            {
                var w = ZWorld.Create(set, groundMap ??= OzV11Kit.Plain(GroundW, GroundH), 3);
                w.NoGameEnd = true; w.Bots.Add(new ZBot(w, Cpu, 104) { Ignored = true });
                int ox = GroundW * 8 - 100, oy = GroundH * 8;
                var v = w.CreateObject("vehicle", "light", ox - 16, oy - 16, Me); v.DriverType = "tough";
                var how = w.CreateObject("cannon", "howitzer", v.CenterX + 160 - 16, v.CenterY - 16, Cpu);
                OzV11Kit.CpuAttack(w, how, v);
                int i = 0; for (; i < 3000 && !v.PremiumDodgeBoosted; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); }
                for (int j = 0; j < 5 && v.PremiumDodgeBoosted; j++) { w.Tick(ZConst.TICK); w.Events.Clear(); }
                bool boosted = v.PremiumDodgeBoosted; double speed = v.RealMoveSpeed, plain = v.PremiumDodgeBaseSpeed;
                v.DriverType = "grunt";
                var prop = typeof(ZObject).GetProperty("DodgePlanSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                int plans0 = w.PremiumDodgePlans;
                double plan = prop != null ? (double)prop.GetValue(v) : -1;
                w.Tick(ZConst.TICK); w.Events.Clear();
                bool planOk = boosted && prop != null && BitConverter.DoubleToInt64Bits(plan) == BitConverter.DoubleToInt64Bits(plain) && plan != speed && w.PremiumDodgePlans == plans0;
                bool settled = !v.PremiumDodgeBoosted && BitConverter.DoubleToInt64Bits(v.RealMoveSpeed) == BitConverter.DoubleToInt64Bits(plain);
                Check(planOk && settled, $"a driver change while the factor is in (light tank, tough -> grunt at tick {i}): the step aside is planned with {plan:0.######} px/s "
                      + $"= the plain {plain:0.######} (not {speed:0.######}) ({planOk}); the next tick the factor is off, the speed {v.RealMoveSpeed:0.######} ({settled})");
            }
        }

        static void R(string set)
        {
            var a = OneShell(set, ZMTank.Mech, "sniper", 192, 11, true); var b = OneShell(set, ZMTank.Mech, "sniper", 192, 11, true);
            Check(a.trace == b.trace && a.rand == b.rand && a.premiumTicks == b.premiumTicks && a.premiumTicks > 0, $"the same premium scene twice (mech, sniper, 192 px, seed 11): the same trace {a.trace:X16} / {b.trace:X16}, premium ticks {a.premiumTicks} / {b.premiumTicks}");
        }

        // ------------------------------------------------------------------------------------------------ M
        /// <summary>The factors the measure compares (the kit's own is added when it is not among them).</summary>
        public static double[] MeasureMuls = { 1.03, 1.05, 1.08, 1.10 };
        public static int[] MeasureRanges = { 80, 120, 160, 192 };

        static void M(string set)
        {
            var muls = MeasureMuls.Contains(ZWorld.PremiumDodgeSpeedMul) ? MeasureMuls : MeasureMuls.Concat(new[] { ZWorld.PremiumDodgeSpeedMul }).OrderBy(x => x).ToArray();
            int full; using (new Hold(true)) { var w = ZWorld.Create(set, groundMap ??= OzV11Kit.Plain(GroundW, GroundH), 1); full = w.S.Unit("howitzer").Damage; }
            bool ok = true; double worstHp = 0, sumG = 0, sumP = 0;
            foreach (var type in Types)
            {
                var rows = new List<string>(); int hp = 0;
                foreach (var range in MeasureRanges)
                {
                    var g = Enumerable.Range(1, Seeds).Select(sd => OneShell(set, type, "grunt", range, sd, true)).Where(x => x.exploded).ToList();
                    if (g.Count == 0) { rows.Add($"{range} px: no shell went off"); ok = false; continue; }
                    hp = g[0].hp0;
                    double gd = g.Average(x => x.Share(full)), gm = g.Average(x => x.blastDist) / 8, fl = g.Average(x => x.flight);
                    var cells = new List<string> { $"grunt {gd:0.000} of a shell, {gm:0.00} m from the blast" };
                    foreach (var mul in muls)
                    {
                        var p = Enumerable.Range(1, Seeds).Select(sd => OneShell(set, type, "tough", range, sd, true, mul)).Where(x => x.exploded).ToList();
                        double pd = p.Average(x => x.Share(full)), pm = p.Average(x => x.blastDist) / 8, cut = gd > 0 ? 100 * (1 - pd / gd) : 0;
                        double saved = 100.0 * (gd - pd) * full / Math.Max(1, hp);                              // the tank's own health saved per shell, in %
                        cells.Add($"x{mul:0.00} {pd:0.000}, {pm:0.00} m ({cut:+0;-0} % less: {saved:+0.0;-0.0} % of its health per shell)");
                        if (mul == ZWorld.PremiumDodgeSpeedMul) { worstHp = Math.Max(worstHp, saved); sumG += gd; sumP += pd; }
                    }
                    rows.Add($"{range} px ({g.Count} shells, {fl:0.00} s in the air): " + string.Join(", ", cells));
                }
                Info($"measure, {Name(type)} ({hp} hp; a full howitzer hit {full}), one howitzer shell, seeds 1..{Seeds}, the mean share of the shell's full damage it takes and its distance from the blast:" + string.Join("", rows.Select(r => Environment.NewLine + "      " + r)));
            }
            double cutAll = 100 * (1 - sumP / Math.Max(1e-9, sumG));
            ok &= sumP < sumG && cutAll <= MaxCutAll;                                   // review 6 Nov: strictly less (the text says 'more than 0')
            Check(ok && worstHp <= MaxSavedHp, $"the measure ('subtle'): with the kit's x{ZWorld.PremiumDodgeSpeedMul:0.00} the four types take {cutAll:0.0} % less howitzer damage over the four ranges "
                  + $"(more than 0, at most {MaxCutAll:0} %); at most {worstHp:0.0} % of the tank's own health saved per shell (at most {MaxSavedHp:0.0} %)");
        }

        /// <summary>The measure's bounds for "subtle": a premium driver saves at most this much of his tank's own health per howitzer shell (%), and
        /// the four types take at most this much less howitzer damage over the four ranges (%). The kit's x1.08 measured 5.2 % and 10.2 % (6 Nov).</summary>
        public static double MaxSavedHp = 6.0, MaxCutAll = 15.0;

        // ------------------------------------------------------------------------------------------------ entry points
        public static string Run(string settings, bool measure = true)
        {
            pass = fail = 0; sb = new StringBuilder();
            var t0 = DateTime.UtcNow;
            try
            {
                Info($"the rule's factor x{ZWorld.PremiumDodgeSpeedMul:0.00} (Sim; the view's quicker hull turn, View/HullSteer.cs PremiumDodgeTurn, is not in this check); scenes: a plain {GroundW} x {GroundH}-tile map, one howitzer shell (range 200 px; 192 px = its full range here)");
                T(settings); V(settings); P(settings); X(settings); N(settings); S(settings); R(settings);
                if (measure) M(settings);
            }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace); }
            Info($"run time {(DateTime.UtcNow - t0).TotalSeconds:0.0} s (the check's own, not a rule input)");
            sb.AppendLine($"[OpenZ V1.4 premium dodge] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

        /// <summary>The Deviations row's probe: a light tank and the mech, a grunt and a tough driver, one shell each, rule ON against OFF.</summary>
        public static string Quick(string settings)
        {
            pass = fail = 0; sb = new StringBuilder();
            try
            {
                foreach (var type in new[] { "light", ZMTank.Mech })
                {
                    var g0 = OneShell(settings, type, "grunt", 160, 3, false); var g1 = OneShell(settings, type, "grunt", 160, 3, true);
                    var p0 = OneShell(settings, type, "tough", 160, 3, false); var p1 = OneShell(settings, type, "tough", 160, 3, true);
                    double r = Ratio(p0, p1);
                    Check(g0.trace == g1.trace && g1.premiumTicks == 0 && p0.trace != p1.trace && p1.premiumTicks > 0 && Math.Abs(r - ZWorld.PremiumDodgeSpeedMul) < 1e-5,
                          $"{Name(type)}: grunt driver unchanged ({g0.trace == g1.trace}), tough driver steps aside x {r:0.0000} ({p1.premiumTicks} premium ticks)");
                }
                {
                    var j0 = OneShell(settings, "jeep", "tough", 160, 3, false); var j1 = OneShell(settings, "jeep", "tough", 160, 3, true);
                    Check(j0.trace == j1.trace && j1.premiumTicks == 0, $"a jeep with a tough driver unchanged ({j0.trace == j1.trace})");
                }
            }
            catch (Exception ex) { Check(false, "the probe threw " + ex.GetType().Name + ": " + ex.Message); }
            sb.AppendLine($"[OpenZ V1.4 premium dodge quick] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

#if UNITY_EDITOR && !OPENZ_HARNESS
        [UnityEditor.MenuItem("OpenZ/V1.4/Premium Dodge: Count (headless, DV173)")]
        static void Menu() => OzV11Kit.RunMenu("premium dodge: count (DV173)", () => Run(OzV11Kit.SettingsText));

        [UnityEditor.MenuItem("OpenZ/V1.4/Premium Dodge: Count without the measure (headless, DV173)")]
        static void MenuShort() => OzV11Kit.RunMenu("premium dodge: count, no measure (DV173)", () => Run(OzV11Kit.SettingsText, false));
#endif
    }
}
