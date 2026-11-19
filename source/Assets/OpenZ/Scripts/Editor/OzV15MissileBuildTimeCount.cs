using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5, the missile cannon builds 40 % longer than the howitzer, rule DV191 (Sim/ZMissileBuildTime.cs): the counted check, headless on the
    /// Sim alone, on the real level 2 map, nothing saved: menu OpenZ/V1.5/Missile Build Time: Count (headless, DV191), and off-project on the
    /// editor's Mono (the kit's kit_tools/harness). One line per fact, [OpenZ V1.5 missile build time] PASS n/n; a fact with no sample is a
    /// FAIL. The check sets the rule's switch itself (on as View/ZGame.cs does at a level start, off as in 'Zod exact') and puts it back.
    ///  N  the numbers: off = the text's rows (howitzer 179 s, missile cannon 182 s); on = (179 x 14 + 5) / 10 = 251 s (the nearest whole
    ///     second to 250.6), applied once a parse; the formula = x 1.4 rounded half up for every whole second 0..2000; the Zod hold and the
    ///     ZodEngine parse read 182 s with the switch on; a text without the howitzer's build time leaves the missile cannon's own row
    ///  O  every other unit type's row (the howitzer, the gun, the gatling, the missile launcher by name), every other field of the missile
    ///     cannon, the global settings and the raw keys: the same off and on
    ///  F  real ticks on level 2 (every unit off the map, tech level 5): with the switch on the player's fort builds a missile cannon in the
    ///     game's rounding of 251 s x its flags and damage, the player's vehicle factory a howitzer in 179 s x its own, the computer's fort on
    ///     MAYHEM (DV175) a missile cannon in 251 s x its own x 0.9; with the switch off the fort's missile cannon in 182 s x its own; the
    ///     cannon is stored; the production window's numbers (its BASE line and its total read the row through BuildTimeModified) = the
    ///     countdown the factory runs
    ///  G  every modifier on top, written out (Zod's base x (1 - 0.5 z) x (1 + 1.25 (1 - hp/max)); DV175 x 0.9): flags 0 / 1/7 / 2/7 / 1/2 / 1,
    ///     health 100 / 60 / 25 %, the player's and the computer's side on MAYHEM, the missile cannon and the howitzer, switch on and off:
    ///     bit for bit
    ///  D  off = before: the switch off leaves every row as the text has it (the rule not applied, its counter still); the factory's missile
    ///     cannon takes Zod's 182 s (F); off Unity the harness compares a P build with a P + kit build field by field
    ///  R  (full) the same real-tick run twice: the same ticks and events
    ///  U  (full, the editor only) the Unit editor's OpenZ set shows 251 s on / 182 s off; its ZodEngine set 182 s with the switch on
    ///  L  (full, the editor only) the battlefield screen's rules (OzLevelStats.RulesAsLevelStart) set the switch as the level start will
    /// </summary>
    public static class OzV15MissileBuildTimeCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);
        static string F3(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        /// <summary>Seconds as the production window shows them (UnitInfo.Mmss: m:ss, rounded up).</summary>
        public static string Mmss(double s) { int t = (int)Math.Ceiling(Math.Max(0, s)); return t / 60 + ":" + (t % 60).ToString("00", CultureInfo.InvariantCulture); }

        public const int ZodHowitzer = 179, ZodMissile = 182, NewMissile = 251;
        const string M = ZMissileBuildTime.Type, H = ZMissileBuildTime.Base;
        const double CpuScale = ZWorld.CpuBuildFasterScale;
        static readonly string[] Named = { "howitzer", "gun", "gatling", "missile_launcher" };

        static IDisposable Rule(bool on) => new Hold(on);
        sealed class Hold : IDisposable
        {
            readonly bool was = ZWorld.MissileTurretBuildTimeDeviation;
            public Hold(bool on) { ZWorld.MissileTurretBuildTimeDeviation = on; }
            public void Dispose() { ZWorld.MissileTurretBuildTimeDeviation = was; }
        }

        static ZSettings Parse(string settings, bool on) { using (Rule(on)) return ZSettings.Parse(settings); }

        static readonly FieldInfo[] UnitFields = typeof(ZUnitSettings).GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
        static readonly FieldInfo[] GlobalFields = typeof(ZSettings).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(double)).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
        static bool Same(object a, object b) => a is double x && b is double y ? BitConverter.DoubleToInt64Bits(x) == BitConverter.DoubleToInt64Bits(y) : Equals(a, b);

        /// <summary>Zod's build time (zbuilding.cpp:633-667), written out here on its own: base x (1 - 0.5 z) x (1 + 1.25 (1 - hp/max)).</summary>
        public static double Zod(int baseSeconds, double zone, int health, int maxHealth)
        {
            double z = Math.Max(0, Math.Min(1, zone));
            double t = baseSeconds - baseSeconds * 0.5 * z;
            double hp = maxHealth > 0 ? (double)health / maxHealth : 1;
            t += t * 1.25 * (1.0 - hp);
            return t;
        }

        /// <summary>The game's rounding of a build to ticks: the world's clock steps ZConst.TICK a tick (ZWorld.Tick) and ProcessBuilding
        /// finishes the item on the first tick whose time is at or after BFinal.</summary>
        public static long TicksTo(double start, double bfinal)
        {
            double tm = start; long n = 0;
            while (!(tm >= bfinal) && n < 100000000) { tm += ZConst.TICK; n++; }
            return n;
        }

        // ------------------------------------------------------------------------------------------------ N
        static void N(string settings)
        {
            int a0 = ZMissileBuildTime.Applied;
            var off = Parse(settings, false); int a1 = ZMissileBuildTime.Applied;
            var on = Parse(settings, true); int a2 = ZMissileBuildTime.Applied;
            var h0 = off.Unit(H); var m0 = off.Unit(M); var h1 = on.Unit(H); var m1 = on.Unit(M);
            off.Raw.TryGetValue("unit." + M + ".build_time", out double rawM); off.Raw.TryGetValue("unit." + H + ".build_time", out double rawH);
            Check(m0 != null && h0 != null && m0.build_time == ZodMissile && h0.build_time == ZodHowitzer && rawM == ZodMissile && rawH == ZodHowitzer && a1 == a0,
                $"N1 switch off ('Zod exact', headless): the missile cannon {m0?.build_time} s ({Mmss(m0?.build_time ?? 0)}), the howitzer {h0?.build_time} s ({Mmss(h0?.build_time ?? 0)}): the text's own rows "
                + $"(unit.{M}.build_time={rawM}, unit.{H}.build_time={rawH}); not applied");
            int want = h1 != null ? (h1.build_time * 14 + 5) / 10 : -1;
            Check(m1 != null && h1 != null && m1.build_time == want && m1.build_time == NewMissile && h1.build_time == ZodHowitzer && a2 == a1 + 1,
                $"N2 switch on (the game): the missile cannon {m1?.build_time} s ({Mmss(m1?.build_time ?? 0)}) = (the howitzer's {h1?.build_time} x 14 + 5) / 10 (exactly {F3((h1?.build_time ?? 0) * 1.4)} s; "
                + $"whole seconds like every build_time row); the howitzer {h1?.build_time} s; applied once a parse");
            double ratio = m1.build_time / (double)h1.build_time;
            Check(Math.Abs(m1.build_time - h1.build_time * 1.4) <= 0.5 && Math.Abs(ratio - 1.4) < 0.5 / h1.build_time,
                $"N3 {m1.build_time} / {h1.build_time} = x {ratio.ToString("0.0000", CultureInfo.InvariantCulture)} (+{((ratio - 1) * 100).ToString("0.0", CultureInfo.InvariantCulture)} %), the nearest whole second to x 1.4; "
                + $"{m1.build_time - m0.build_time:+0;-0} s on Zod's {m0.build_time} s ({Mmss(m0.build_time)} -> {Mmss(m1.build_time)})");
            int bad = 0; string firstBad = null;
            for (int h = 0; h <= 2000; h++)
            {
                int r = (int)Math.Floor(h * 1.4 + 0.5 + 1e-9);
                if (ZMissileBuildTime.BuildTime(h) != r) { bad++; firstBad ??= $"{h} -> {ZMissileBuildTime.BuildTime(h)} (want {r})"; }
            }
            Check(bad == 0, $"N4 the formula: x 1.4 to the nearest whole second, halves up, for every howitzer time 0..2000 s ({2001 - bad} of 2001){(bad > 0 ? "; first wrong " + firstBad : "")}");
            int z1, z2; bool back;
            using (Rule(true))
            {
                using (ZWorld.MissileTurretBuildTimeOff()) z1 = ZSettings.Parse(settings).Unit(M).build_time;
                bool mid = ZWorld.MissileTurretBuildTimeDeviation;
                z2 = ZMissileBuildTime.ParseZod(settings).Unit(M).build_time;
                back = mid && ZWorld.MissileTurretBuildTimeDeviation;
            }
            Check(z1 == ZodMissile && z2 == ZodMissile && back,
                $"N5 with the switch on, the Zod hold (ZWorld.MissileTurretBuildTimeOff: the parity's Zod rows) and the ZodEngine parse (ZMissileBuildTime.ParseZod: the Unit editor's ZodEngine set) read Zod's {z1} / {z2} s; the switch is on again after each");
            string noH = string.Join("\n", settings.Split('\n').Where(l => !l.Trim().StartsWith("unit." + H + ".build_time", StringComparison.Ordinal)));
            int a3 = ZMissileBuildTime.Applied; var nh = Parse(noH, true); int a4 = ZMissileBuildTime.Applied;
            Check(nh.Unit(M).build_time == ZodMissile && nh.Unit(H).build_time == 0 && a4 == a3,
                $"N6 a settings text without the howitzer's build time, switch on: the missile cannon keeps its own row ({nh.Unit(M).build_time} s), never 0 s; not applied");
        }

        // ------------------------------------------------------------------------------------------------ O
        static void O(string settings)
        {
            var off = Parse(settings, false); var on = Parse(settings, true);
            var diff = new List<string>(); int types = 0, fields = 0;
            foreach (var name in off.Units.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (name == M) continue;
                types++;
                var a = off.Unit(name); var b = on.Unit(name);
                if (b == null) { diff.Add(name + " missing"); continue; }
                foreach (var f in UnitFields) { fields++; if (!Same(f.GetValue(a), f.GetValue(b))) diff.Add(name + "." + f.Name); }
            }
            Check(diff.Count == 0 && types >= 18,
                $"O1 every other unit type's row the same off and on: {types} types ({string.Join(" ", off.Units.Keys.Where(k => k != M).OrderBy(k => k, StringComparer.Ordinal))}), {fields} fields{(diff.Count > 0 ? "; differ: " + string.Join(" ", diff.Take(12)) : "")}");
            var named = Named.Select(n => (n, a: off.Unit(n)?.build_time ?? -1, b: on.Unit(n)?.build_time ?? -2)).ToList();
            Check(named.All(x => x.a == x.b && x.a > 0) && named[0].a == ZodHowitzer,
                "O2 the howitzer and three other cannons / vehicles, build time off / on: " + string.Join(", ", named.Select(x => $"{x.n} {x.a} / {x.b} s")));
            var m0 = off.Unit(M); var m1 = on.Unit(M);
            var other = UnitFields.Where(f => f.Name != "build_time").ToList();
            var od = other.Where(f => !Same(f.GetValue(m0), f.GetValue(m1))).Select(f => f.Name).ToList();
            Check(od.Count == 0, $"O3 every other field of the missile cannon the same off and on ({other.Count}: health {m1.MaxHealth} HP, range {m1.attack_radius} px, damage {m1.Damage}, reload {F3(m1.attack_speed)} s){(od.Count > 0 ? "; differ: " + string.Join(" ", od) : "")}");
            var gd = GlobalFields.Where(f => !Same(f.GetValue(off), f.GetValue(on))).Select(f => f.Name).ToList();
            var rd = off.Raw.Keys.Union(on.Raw.Keys).Where(k => !(off.Raw.TryGetValue(k, out var x) && on.Raw.TryGetValue(k, out var y) && Same(x, y))).ToList();
            Check(gd.Count == 0 && rd.Count == 0, $"O4 the global settings ({GlobalFields.Length} fields) and every raw key of the text ({off.Raw.Count}: the text stays Zod's) the same off and on{(gd.Count + rd.Count > 0 ? "; differ: " + string.Join(" ", gd.Concat(rd)) : "")}");
        }

        // ------------------------------------------------------------------------------------------------ F, R: real ticks
        /// <summary>Level 2 with nobody on it: every unit and gun off the map at the first tick, no game end, every producer stopped and
        /// empty, both sides at tech level 5 (every producer lists the missile cannon). The switch as asked while the world parses.</summary>
        static ZWorld New(string settings, string map, bool on)
        {
            ZWorld w; using (Rule(on)) w = ZWorld.Create(settings, map, 1);
            w.NoGameEnd = true;
            foreach (var o in w.Objects) if (o.IsUnit) o.KillAt = 0;
            foreach (var o in w.Objects) if (o.IsProducer) { o.StopBuildingProduction(); o.StoredCannons.Clear(); }
            w.TechBonus[(int)Team.Red] = 5; w.TechBonus[(int)Team.Blue] = 5;
            for (int i = 0; i < 37; i++) w.Tick(ZConst.TICK);
            w.Events.Clear();
            return w;
        }

        sealed class Job
        {
            public string label, unit; public ZObject b; public Team team; public bool cpu;
            public int baseS; public double start, binit, bfinal, zone, want, window; public int hp, max; public string windowBase;
            public long ticks = -1, wantTicks; public double elapsed = -1; public bool stored, started, steady = true;
        }

        sealed class RealRun { public List<Job> jobs = new List<Job>(); public ulong hash = 1469598103934665603UL; public string why = ""; public int baseRow; }

        static RealRun Real(string settings, string map, bool on)
        {
            var w = New(settings, map, on); var r = new RealRun();
            w.SetCpuBuildFaster(true, Team.Red, true, false);                                              // MAYHEM, the human red: the computer's side (blue) builds x 0.9 (DV175)
            var flag = w.Flags.FirstOrDefault(f => f.LinkedObjects.Any(o => o.Type == "vehicle_factory"));
            if (flag != null) w.AwardZone(flag, Team.Red);                                                  // the player owns a vehicle factory (and that flag's share)
            for (int i = 0; i < 3; i++) w.Tick(ZConst.TICK);
            w.Events.Clear();
            var rf = w.Objects.FirstOrDefault(o => o.IsFort && o.Owner == Team.Red && !o.Destroyed);
            var bf = w.Objects.FirstOrDefault(o => o.IsFort && o.Owner == Team.Blue && !o.Destroyed);
            var vf = w.Objects.FirstOrDefault(o => o.Type == "vehicle_factory" && o.Owner == Team.Red && !o.Destroyed);
            r.baseRow = w.S.Unit(M).build_time;
            void Add(string label, ZObject b, Team t, string unit, bool cpu) { if (b != null) r.jobs.Add(new Job { label = label, b = b, team = t, unit = unit, cpu = cpu }); else r.why += label + " not on level 2; "; }
            Add("the player's fort, a missile cannon", rf, Team.Red, M, false);
            if (on) { Add("the player's vehicle factory, a howitzer", vf, Team.Red, H, false); Add("the computer's fort on MAYHEM, a missile cannon", bf, Team.Blue, M, true); }
            foreach (var j in r.jobs)
            {
                j.started = w.StartBuilding(j.team, j.b, j.unit);
                j.start = w.Time; j.binit = j.b.BInit; j.bfinal = j.b.BFinal; j.zone = j.b.ZoneOwnage; j.hp = j.b.Health; j.max = j.b.MaxHealth;
                j.baseS = w.S.Unit(j.unit).build_time;
                j.want = Zod(j.baseS, j.zone, j.hp, j.max) * (j.cpu ? CpuScale : 1.0);
                j.wantTicks = TicksTo(j.start, j.bfinal);
                j.window = j.b.BuildTimeModified(w.S.Unit(j.unit).build_time);                          // what UI/OpenZProductionWindow.cs shows: its total and its BASE line's right side
                j.windowBase = Mmss(w.S.Unit(j.unit).build_time);
            }
            for (long i = 1; i <= 40000 && r.jobs.Any(j => j.ticks < 0 && j.started); i++)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                {
                    r.hash = (r.hash ^ (ulong)(long)e.type) * 1099511628211UL; r.hash = (r.hash ^ (ulong)(long)e.objId) * 1099511628211UL;
                    foreach (var j in r.jobs)
                        if (e.type == ZEventType.CannonStored && e.objId == j.b.Id && j.ticks < 0) { j.ticks = i; j.elapsed = e.value; j.stored = true; }
                }
                w.Events.Clear();
                foreach (var j in r.jobs)
                {
                    if (j.ticks >= 0) continue;
                    if (j.b.BFinal != j.bfinal || j.b.ZoneOwnage != j.zone || j.b.Health != j.hp) j.steady = false;
                    if (!j.b.Building || j.b.BInit != j.binit) { j.ticks = i; j.elapsed = w.Time - j.binit; }   // done without a stored cannon (never expected here)
                }
            }
            return r;
        }

        static void FR(string settings, string map, bool full)
        {
            var on = Real(settings, map, true);
            var off = Real(settings, map, false);
            if (on.why.Length + off.why.Length > 0) Check(false, "F0 the level 2 producers: " + on.why + off.why);
            foreach (var (run, sw) in new[] { (on, "on"), (off, "off") })
                foreach (var j in run.jobs)
                {
                    bool exact = j.bfinal == j.binit + j.want;
                    bool ticks = j.ticks == j.wantTicks && j.ticks > 0;
                    bool window = j.window == j.bfinal - j.binit;                                     // the window's total = the factory's own countdown (both through BuildTimeModified)
                    bool baseOk = j.unit == M ? j.baseS == (sw == "on" ? NewMissile : ZodMissile) : j.baseS == ZodHowitzer;
                    Check(j.started && exact && ticks && j.stored && j.steady && window && baseOk,
                        $"F {sw}: {j.label}: base {j.baseS} s, flags {F3(j.zone)}, health {j.hp}/{j.max}{(j.cpu ? ", x 0.9" : "")}: the countdown {F3(j.bfinal - j.binit)} s = written out {F3(j.want)} s bit for bit {exact}; "
                        + $"done after {j.ticks} ticks (the game's rounding: {j.wantTicks}), {F3(j.elapsed)} s, the cannon stored {j.stored}; the production window: {j.windowBase} BASE, {Mmss(j.window)} in all");
                }
            var pm = on.jobs.FirstOrDefault(j => j.unit == M && !j.cpu); var ph = on.jobs.FirstOrDefault(j => j.unit == H);
            var cm = on.jobs.FirstOrDefault(j => j.cpu); var om = off.jobs.FirstOrDefault(j => j.unit == M);
            if (pm != null && ph != null && cm != null && om != null)
            {
                double basePm = pm.want / Zod(1, pm.zone, pm.hp, pm.max), baseOm = om.want / Zod(1, om.zone, om.hp, om.max);
                Check(pm.baseS == NewMissile && ph.baseS == ZodHowitzer && om.baseS == ZodMissile && Math.Abs(basePm - NewMissile) < 1e-9 && Math.Abs(baseOm - ZodMissile) < 1e-9
                      && cm.ticks < pm.ticks && Math.Abs(cm.want - Zod(NewMissile, cm.zone, cm.hp, cm.max) * CpuScale) < 1e-9,
                    $"F1 the same fort, the same flags and health: switch on {Mmss(pm.want)} ({pm.ticks} ticks), off {Mmss(om.want)} ({om.ticks} ticks) = {NewMissile} / {ZodMissile} of it; "
                    + $"the howitzer {Mmss(ph.want)} at its factory; the computer's missile cannon on MAYHEM {Mmss(cm.want)} ({cm.ticks} ticks: x 0.9 on top of the new time)");
            }
            else Check(false, "F1 a sample is missing (the player's missile cannon / howitzer, the computer's missile cannon, the switch-off one)");
            if (full)
            {
                var again = Real(settings, map, true);
                bool same = again.hash == on.hash && again.jobs.Count == on.jobs.Count && again.jobs.Zip(on.jobs, (a, b) => a.ticks == b.ticks && a.bfinal == b.bfinal).All(x => x);
                Check(same, $"R1 the same switch-on run twice: the same ticks and events ({on.hash:X16} / {again.hash:X16})");
            }
        }

        // ------------------------------------------------------------------------------------------------ G: the modifiers on top
        static readonly double[] Zones = { 0, 1.0 / 7, 2.0 / 7, 0.5, 1 };
        static readonly double[] Hps = { 1, 0.6, 0.25 };

        static void G(string settings, string map)
        {
            foreach (bool sw in new[] { true, false })
            {
                var w = New(settings, map, sw);
                w.SetCpuBuildFaster(true, Team.Red, true, false);
                var b = w.Objects.First(o => o.IsFort && o.Owner == Team.Red);
                int n = 0, ok = 0; string ex = null, bad = null;
                foreach (var unit in new[] { M, H })
                {
                    int bt = w.S.Unit(unit).build_time;
                    foreach (double z in Zones)
                        foreach (double r in Hps)
                        {
                            int hp = Math.Max(1, (int)Math.Round(b.MaxHealth * r));
                            double zod = Zod(bt, z, hp, b.MaxHealth);
                            Team owner = b.Owner; int hp0 = b.Health; double z0 = b.ZoneOwnage; string cu = b.CurUnit; bool bu = b.Building; double bi = b.BInit, bf = b.BFinal;
                            double p, c, s0;
                            try
                            {
                                b.ZoneOwnage = z; b.Health = hp; b.CurUnit = unit; b.Building = true; b.BInit = w.Time; s0 = b.BInit;
                                b.Owner = Team.Red; b.RecalcBuildTime(); p = b.BFinal;                            // the player's side
                                b.Owner = Team.Blue; b.RecalcBuildTime(); c = b.BFinal;                           // the computer's side on MAYHEM
                            }
                            finally { b.Owner = owner; b.Health = hp0; b.ZoneOwnage = z0; b.CurUnit = cu; b.Building = bu; b.BInit = bi; b.BFinal = bf; }
                            n++;
                            int wantBase = unit == M ? (sw ? NewMissile : ZodMissile) : ZodHowitzer;
                            if (p == s0 + zod && c == s0 + zod * CpuScale && bt == wantBase) ok++;
                            else bad ??= $"{unit} flags {F3(z)} health {r}: {F3(p - s0)} / {F3(c - s0)} s, want {F3(zod)} / {F3(zod * CpuScale)}";
                            if (unit == M && z == 2.0 / 7 && r == 0.6) ex = $"missile cannon at 2/7 flags, 60 % health: the player {Mmss(p - s0)} ({F3(p - s0)} s), the computer {Mmss(c - s0)} ({F3(c - s0)} s)";
                        }
                }
                Check(n == 30 && ok == n, $"G {(sw ? "on" : "off")}: every modifier on top of the base time (flags x 5, health x 3, the player's side and the computer's x 0.9 on MAYHEM; the missile cannon {(sw ? NewMissile : ZodMissile)} s, the howitzer {ZodHowitzer} s): "
                                         + $"{ok}/{n} bit for bit; {ex}{(bad != null ? "; WRONG: " + bad : "")}");
            }
        }

        // ------------------------------------------------------------------------------------------------ D: off = before
        static void D(string settings)
        {
            int a0 = ZMissileBuildTime.Applied;
            var off = Parse(settings, false);
            int a1 = ZMissileBuildTime.Applied;
            var rows = new List<string>(); int n = 0;
            foreach (var kv in off.Units.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (!off.Raw.TryGetValue("unit." + kv.Key + ".build_time", out double raw)) continue;
                n++;
                if (kv.Value.build_time != (int)raw) rows.Add($"{kv.Key} {kv.Value.build_time} / {raw}");
            }
            Check(rows.Count == 0 && n >= 19 && a1 == a0 && off.Unit(M).build_time == ZodMissile,
                $"D1 switch off: the build time of every unit with a row in the text = the text's ({n} units, the missile cannon {off.Unit(M).build_time} s); the rule not applied (its counter {a0} -> {a1}){(rows.Count > 0 ? "; differ: " + string.Join(", ", rows) : "")}");
            ZSettings held; using (Rule(true)) using (ZWorld.MissileTurretBuildTimeOff()) held = ZSettings.Parse(settings);
            var diff = new List<string>();
            foreach (var name in off.Units.Keys) foreach (var f in UnitFields) if (!Same(f.GetValue(off.Unit(name)), f.GetValue(held.Unit(name)))) diff.Add(name + "." + f.Name);
            Check(diff.Count == 0, $"D2 the parity's hold with the switch on gives the switch-off rows, every field of every unit ({off.Units.Count} types){(diff.Count > 0 ? "; differ: " + string.Join(" ", diff.Take(8)) : "")}");
        }

#if !OPENZ_HARNESS
        // ------------------------------------------------------------------------------------------------ U, L (the editor only)
        static void U(string settings)
        {
            var rules = UeRules.FromGame();
            double b1, b0;
            using (Rule(true)) b1 = OzUeLiveStats.Table(ZSettings.Parse(settings), rules)[M][(int)UeStat.Build].value;
            using (Rule(false)) b0 = OzUeLiveStats.Table(ZSettings.Parse(settings), rules)[M][(int)UeStat.Build].value;
            Check(b1 == NewMissile && b0 == ZodMissile, $"U1 the Unit editor's OpenZ set (the missile cannon's card, BUILD): on {b1} s, off ('Zod exact') {b0} s");
            var js = UnityEngine.Resources.Load<UnityEngine.TextAsset>(OzUeSetData.ResourcePath);
            if (js == null) { Info("U2 the stats sets file (" + OzUeSetData.ResourcePath + ") is missing: its ZodEngine set not checked"); return; }
            using (Rule(true))
            {
                var sets = OzUeSetData.Parse(js.text);
                bool has = sets.zod.TryGetValue(M, out var z) && z[(int)UeStat.Build].HasNumber;
                if (!has) { Info("U2 the ZodEngine set has no build time for the missile cannon: nothing to compare"); return; }
                double zb = z[(int)UeStat.Build].value;
                Check(sets.error == null && zb == ZodMissile && ZWorld.MissileTurretBuildTimeDeviation, $"U2 its ZodEngine set with the switch on: the missile cannon {zb} s (Zod's){(sets.error != null ? "; " + sets.error : "")}");
            }
        }

        static void L()
        {
            bool was = ZWorld.MissileTurretBuildTimeDeviation, mh = ZWorld.MechHpDeviation, gb = ZWorld.GatlingBuffDeviation;
            try
            {
                bool zodExact = UnityEngine.PlayerPrefs.GetInt("oz_zod_exact", 0) != 0;                  // read only
                ZWorld.MissileTurretBuildTimeDeviation = false;                                            // a fresh session: no level has set the switch yet
                OzLevelStats.RulesAsLevelStart();
                bool on = ZWorld.MissileTurretBuildTimeDeviation;
                Check(on == !zodExact && on == ZWorld.GatlingBuffDeviation,
                    $"L1 the battlefield screen's rules (OzLevelStats.RulesAsLevelStart) in a fresh session: the missile cannon's switch {(on ? "on" : "off")} as the level start sets it ('Zod exact' {(zodExact ? "on" : "off")}), as DV170's gatling switch");
            }
            finally { ZWorld.MissileTurretBuildTimeDeviation = was; ZWorld.MechHpDeviation = mh; ZWorld.GatlingBuffDeviation = gb; }
        }
#endif

        // ------------------------------------------------------------------------------------------------ runs
        /// <summary>The whole check (full: + R, U, L).</summary>
        public static string Run(string settings, Func<string, string> mapText, bool full)
        {
            pass = fail = 0; sb = new StringBuilder();
            sb.AppendLine("[OpenZ V1.5 missile build time] DV191: the missile cannon's base build time = the howitzer's x 1.4 (Sim/ZMissileBuildTime.cs)");
            try
            {
                string map = mapText("L02");
                N(settings);
                O(settings);
                FR(settings, map, full);
                G(settings, map);
                D(settings);
#if !OPENZ_HARNESS
                if (full) { U(settings); L(); }
#else
                Info("U (the Unit editor's sets) and L (the battlefield screen's rules) run in the editor only (menu OpenZ/V1.5/Missile Build Time: Count (headless, DV191))");
#endif
                Info("D3 off = before, field by field (a P build against a P + kit build, the switch off): off Unity in the kit's harness");
            }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace); }
            if (pass + fail == 0) Check(false, "no fact was counted");
            sb.AppendLine($"[OpenZ V1.5 missile build time{(full ? "" : " quick")}] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

        /// <summary>The Deviations row's probe (Editor/OpenZParity.V15MissileBuildTime.cs): N, O, F, G, D.</summary>
        public static string Quick(string settings, Func<string, string> mapText) => Run(settings, mapText, false);
    }
}
