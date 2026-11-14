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
    /// 1.4, the gatling buff, rule DV170 (Sim/ZGatlingBuff.cs): the counted check, headless on the Sim alone, nothing saved: menu
    /// OpenZ/V1.4/Gatling Buff: Count (headless, DV170), and off-project on the editor's Mono (the kit's tools/harness). One line per fact,
    /// [OpenZ V1.4 gatling buff] PASS n/n. The check sets the rule's switch itself (on as View/ZGame.cs does at a level start, off as
    /// in 'Zod exact') on top of the counted checks' usual game switches, and puts it back.
    ///  S  the settings: off = Zod's gatling (120 px, 0.003976, 39 HP a hit); on = 132 px (16.5 m) and 0.003976 x 1.15 = 0.0045724 (45 HP a
    ///     hit); every other field of the gatling unchanged; the Zod hold and the ZodEngine parse read Zod's gatling with the switch on;
    ///  O  every other unit type: every settings field, and the weapon of a unit made from it, the same off and on (the jeep's gun and the
    ///     Electrobot's tesla gatling included); a gatling made in a world: 120 / 39 off, 132 / 45 on;
    ///  R  the reach: a crewed gatling and one still enemy grunt at 100 to 140 px on flat ground: engaged up to 120 px off, up to 132 px on;
    ///     15.5 to 16.4 m (124 to 131 px) only with the switch on;
    ///  T  time to kill: the gatling against a still, harmless grunt squad, psycho squad, jeep and light tank at 48, 96 and 112 px, the same
    ///     seeds off and on: with every hit on the hull the hits to kill are exactly sum(ceil(HP / 39)) off and sum(ceil(HP / 45)) on, about
    ///     1 / 1.15 of them; shots and seconds shrink alike; with the target's driver snipable as in the game (the jeep, the light tank) the
    ///     snipe part keeps its own DC2 numbers and is shown only;
    ///  D  the same scripted fight twice: the same ticks, hits and events;
    ///  U  the Unit editor's stats code: its OpenZ set shows 16.5 m / 45 with the switch on, 15 m / 39 off; its ZodEngine set Zod's gatling with
    ///     the switch on; its notes for the gatling's damage and range (unit_stat_sets.json, the tooltip's 'OpenZ in play' and 'Difference')
    ///     say what OpenZ plays, as DV147's mech note does;
    ///  L  the battlefield screen's check of the saved Custom profile (View/OzLevelStats.cs RulesAsLevelStart, as UI/OpenZMainMenu.UnitStats.cs
    ///     calls it): in a fresh session it sets the gatling's switch as the level start will (with DV147's mech switch); an untouched profile
    ///     saved in 1.4 counts 0 changes (no selector, as before); a profile saved before 1.4 (the gatling 15 m / 39) counts the gatling's 2.
    ///     L reads the real 'Zod exact' preference and only reads it; the switches are put back.
    /// </summary>
    public static class OzV14GatlingBuffCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);
        static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        static string F(double v, string f = "0.0") => v.ToString(f, CultureInfo.InvariantCulture);
        static string M(int px) => (px / 8.0).ToString("0.###", CultureInfo.InvariantCulture) + " m";

        public const int ZodRadius = 120, NewRadius = 132, ZodHit = 39, NewHit = 45;
        public const double ZodDamage = 0.003976;
        const int Big = 1000000000;
        const int GX = 400, GY = 640;                                   // the gatling's centre on the plain map (px)

        static IDisposable Rule(bool on) => new Hold(on);
        sealed class Hold : IDisposable
        {
            readonly bool was = ZWorld.GatlingBuffDeviation;
            public Hold(bool on) { ZWorld.GatlingBuffDeviation = on; }
            public void Dispose() { ZWorld.GatlingBuffDeviation = was; }
        }

        static ZSettings Parse(string settings, bool on) { using (Rule(on)) return ZSettings.Parse(settings); }

        static readonly FieldInfo[] UnitFields = typeof(ZUnitSettings).GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
        static readonly FieldInfo[] GlobalFields = typeof(ZSettings).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(double)).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
        static bool Same(object a, object b) => a is double x && b is double y ? BitConverter.DoubleToInt64Bits(x) == BitConverter.DoubleToInt64Bits(y) : Equals(a, b);

        // ------------------------------------------------------------------------------------------------ S
        static void S(string settings)
        {
            int a0 = ZGatlingBuff.Applied;
            var off = Parse(settings, false); int a1 = ZGatlingBuff.Applied;
            var on = Parse(settings, true); int a2 = ZGatlingBuff.Applied;
            var g0 = off.Unit(ZGatlingBuff.Type); var g1 = on.Unit(ZGatlingBuff.Type);
            Check(g0 != null && g0.attack_radius == ZodRadius && Same(g0.attack_damage, ZodDamage) && g0.Damage == ZodHit && a1 == a0,
                $"switch off ('Zod exact', headless): the gatling's range {g0?.attack_radius} px ({M(g0?.attack_radius ?? 0)}), damage {N(g0?.attack_damage ?? 0)} = {g0?.Damage} HP a hit (Zod: 120 px, 0.003976, 39); not applied");
            double want = ZodDamage * ZGatlingBuff.DamageFactor;
            Check(g1 != null && g1.attack_radius == NewRadius && Same(g1.attack_damage, want) && g1.attack_damage.ToString("R", CultureInfo.InvariantCulture) == "0.0045724" && g1.Damage == NewHit && a2 == a1 + 1,
                $"switch on (the game): the gatling's range {g1?.attack_radius} px ({M(g1?.attack_radius ?? 0)}), damage {N(g1?.attack_damage ?? 0)} (bits {BitConverter.DoubleToInt64Bits(g1?.attack_damage ?? 0):X16} = 0.003976 x 1.15) = {g1?.Damage} HP a hit; applied once a parse");
            Check(g1 != null && Math.Abs(g1.attack_radius / (double)g0.attack_radius - 1.10) < 1e-12 && Math.Abs(g1.attack_damage / g0.attack_damage - 1.15) < 1e-12,
                $"x {F(g1.attack_radius / (double)g0.attack_radius, "0.000")} range, x {F(g1.attack_damage / g0.attack_damage, "0.000")} damage per shot (x {F(g1.Damage / (double)g0.Damage, "0.000")} on the truncated HP a hit)");
            var same = UnitFields.Where(f => f.Name != "attack_radius" && f.Name != "attack_damage").Select(f => (f.Name, a: f.GetValue(g0), b: f.GetValue(g1))).ToList();
            var bad = same.Where(x => !Same(x.a, x.b)).Select(x => x.Name).ToList();
            Check(bad.Count == 0, $"every other field of the gatling the same off and on ({same.Count}: reload {N(g1.attack_speed)} s, hit {N(g1.attack_damage_chance)}, snipe {N(g1.attack_snipe_chance)}, health {N(g1.health)} = {g1.MaxHealth} HP, build {g1.build_time} s){(bad.Count > 0 ? "; differ: " + string.Join(" ", bad) : "")}");
            bool back;
            int r1, r2; double d1, d2;
            using (Rule(true))
            {
                using (ZWorld.GatlingBuffOff()) { var z = ZSettings.Parse(settings).Unit(ZGatlingBuff.Type); r1 = z.attack_radius; d1 = z.attack_damage; }
                bool mid = ZWorld.GatlingBuffDeviation;
                var zz = ZGatlingBuff.ParseZod(settings).Unit(ZGatlingBuff.Type); r2 = zz.attack_radius; d2 = zz.attack_damage;
                back = mid && ZWorld.GatlingBuffDeviation;
            }
            Check(r1 == ZodRadius && r2 == ZodRadius && Same(d1, ZodDamage) && Same(d2, ZodDamage) && back,
                $"with the switch on, the Zod hold (ZWorld.GatlingBuffOff, the parity's Zod rows) and the ZodEngine parse (ZGatlingBuff.ParseZod, the Unit editor's ZodEngine set) read Zod's gatling: {r1} / {r2} px; the switch is on again after each");
        }

        // ------------------------------------------------------------------------------------------------ O
        static readonly string[] Weapon = { "AttackRadius", "Damage", "DamageChance", "AttackSpeed", "SnipeChance", "DamageRadius", "MissileSpeed", "MaxHealth", "MoveSpeed" };
        static string Arms(ZObject o) => string.Join(" ", Weapon.Select(n => { var f = typeof(ZObject).GetField(n); return f == null ? n + "?" : Convert.ToString(f.GetValue(o), CultureInfo.InvariantCulture); }));

        static Dictionary<string, string> Made(string settings, bool on, out ZObject gat)
        {
            using (Rule(on))
            {
                var w = ZWorld.Create(settings, OzV11Kit.Plain(80, 80), 1);
                var d = new Dictionary<string, string>(); gat = null; int i = 0;
                foreach (var type in ZSettings.Robots.Concat(ZSettings.Vehicles).Concat(ZSettings.Cannons))
                {
                    var o = OzV11Kit.Place(w, type, 64 + 96 * (i % 12), 64 + 96 * (i / 12), Team.Red, 1); i++;
                    d[type] = Arms(o);
                    if (type == ZGatlingBuff.Type) gat = o;
                }
                return d;
            }
        }

        static void O(string settings)
        {
            var off = Parse(settings, false); var on = Parse(settings, true);
            var diff = new List<string>(); int types = 0, fields = 0;
            foreach (var name in off.Units.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (name == ZGatlingBuff.Type) continue;
                types++;
                var a = off.Unit(name); var b = on.Unit(name);
                if (b == null) { diff.Add(name + " missing"); continue; }
                foreach (var f in UnitFields) { fields++; if (!Same(f.GetValue(a), f.GetValue(b))) diff.Add(name + "." + f.Name); }
            }
            Check(diff.Count == 0 && types >= 18 && !off.Units.Keys.Any(k => k != ZGatlingBuff.Type && k.Contains("gatling")),
                $"every other unit type's row the same off and on: {types} types ({string.Join(" ", off.Units.Keys.Where(k => k != ZGatlingBuff.Type).OrderBy(k => k, StringComparer.Ordinal))}), {fields} fields{(diff.Count > 0 ? "; differ: " + string.Join(" ", diff.Take(12)) : "")}");
            var gd = GlobalFields.Where(f => !Same(f.GetValue(off), f.GetValue(on))).Select(f => f.Name).ToList();
            var rd = off.Raw.Keys.Union(on.Raw.Keys).Where(k => !(off.Raw.TryGetValue(k, out var x) && on.Raw.TryGetValue(k, out var y) && Same(x, y))).ToList();
            Check(gd.Count == 0 && rd.Count == 0, $"the global settings ({GlobalFields.Length} fields) and every raw key of the text ({off.Raw.Count}, the gatling's two raw keys too: the text stays Zod's) the same off and on{(gd.Count + rd.Count > 0 ? "; differ: " + string.Join(" ", gd.Concat(rd)) : "")}");
            var m0 = Made(settings, false, out var x0); var m1 = Made(settings, true, out var x1);
            var md = m0.Keys.Where(k => k != ZGatlingBuff.Type && m0[k] != m1[k]).ToList();
            Check(md.Count == 0 && m0.Count >= 19, $"a unit made from each row ({m0.Count} types, {string.Join(" ", Weapon)}): every type but the gatling the same off and on (jeep {m1["jeep"]}; electrobot {m1["electrobot"]}){(md.Count > 0 ? "; differ: " + string.Join(" ", md) : "")}");
            Check(x0.AttackRadius == ZodRadius && x0.Damage == ZodHit && x1.AttackRadius == NewRadius && x1.Damage == NewHit && x0.DamageChance == x1.DamageChance && x0.AttackSpeed == x1.AttackSpeed && x0.SnipeChance == x1.SnipeChance && x0.MaxHealth == x1.MaxHealth,
                $"a gatling made in a world: off {x0.AttackRadius} px / {x0.Damage} HP a hit, on {x1.AttackRadius} px / {x1.Damage} HP a hit; reload, hit chance, snipe chance and health the same ({N(x1.AttackSpeed)} s, {N(x1.DamageChance)}, {N(x1.SnipeChance)}, {x1.MaxHealth} HP); its object's AttackRadius is what the range rings and the computer's gun circles (ZBot.CpuShuttle) read");
        }

        // ------------------------------------------------------------------------------------------------ scenes
        sealed class Fight
        {
            public ZWorld w; public ZObject g; public List<ZObject> targets = new List<ZObject>();
            public int shots, hits, snipes, ticks; public bool engaged, killed, emptied; public long killTick = -1; public int expectHits;
            public ulong hash = 1469598103934665603UL;
        }

        static void Still(ZObject q, bool hullOnly)
        {
            q.Damage = 0; q.DamageChance = 0; q.AttackRadius = 0; q.MoveSpeed = 0; q.RealMoveSpeed = 0;
            if (hullOnly) q.CanBeSnipedFlag = false;
        }

        /// <summary>A crewed red gatling at (GX, GY) that cannot die, and a blue `type` (a squad for robots, `amount` when given) `d` px to its east,
        /// still and harmless. Red counts as the player (blue is a CPU that gives no orders); no game end.</summary>
        static Fight New(string settings, string type, int d, int seed, bool hullOnly, int amount = -1)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(80, 80), seed);
            w.NoGameEnd = true;
            var g = OzV11Kit.Place(w, ZGatlingBuff.Type, GX, GY, Team.Red);
            g.MaxHealth = g.Health = Big; if (g.Drivers.Count > 0) g.Drivers[0] = Big;
            var t = OzV11Kit.Place(w, type, GX + d, GY, Team.Blue, amount);
            var f = new Fight { w = w, g = g };
            foreach (var q in OzV11Kit.Squad(t)) { Still(q, hullOnly); f.targets.Add(q); f.expectHits += (q.MaxHealth + g.Damage - 1) / g.Damage; }
            w.Events.Clear();
            return f;
        }

        static bool Down(ZObject q) => q.Destroyed || q.KillAt >= 0 || (q.Kind != ZKind.Robot && q.Owner == Team.Neutral);

        static void Run(Fight f, double seconds)
        {
            int n = (int)Math.Round(seconds / ZConst.TICK);
            for (int i = 0; i < n; i++)
            {
                f.w.Tick(ZConst.TICK); f.ticks++;
                if (f.g.AttackObject != null && f.targets.Contains(f.g.AttackObject)) f.engaged = true;
                foreach (var e in f.w.Events)
                {
                    f.hash = (f.hash ^ (ulong)(long)e.type) * 1099511628211UL; f.hash = (f.hash ^ (ulong)(long)e.objId) * 1099511628211UL;
                    f.hash = (f.hash ^ (ulong)(long)e.otherId) * 1099511628211UL; f.hash = (f.hash ^ (e.flag ? 1UL : 2UL)) * 1099511628211UL;
                    if (e.type == ZEventType.Hitscan && e.objId == f.g.Id) { f.shots++; if (e.flag) { if (e.flag2) f.snipes++; else f.hits++; } }
                }
                f.w.Events.Clear();
                if (f.targets.All(Down))
                {
                    f.killed = f.targets.All(q => q.Destroyed || q.KillAt >= 0); f.emptied = !f.killed; f.killTick = f.ticks; break;
                }
            }
            f.hash = (f.hash ^ (ulong)f.w.Rng.Next(1 << 30)) * 1099511628211UL;
        }

        // ------------------------------------------------------------------------------------------------ R
        public static readonly int[] ReachPx = { 100, 116, 120, 121, 124, 126, 128, 130, 131, 132, 133, 140 };

        static void R(string settings)
        {
            var rows = new List<string>(); bool okAll = true, band = true;
            foreach (int d in ReachPx)
            {
                bool[] eng = new bool[2]; int[] shots = new int[2];
                for (int k = 0; k < 2; k++)
                    using (Rule(k == 1))
                    {
                        var f = New(settings, "grunt", d, 7, false, 1);
                        Run(f, 3.0);
                        eng[k] = f.engaged; shots[k] = f.shots;
                    }
                bool want0 = d <= ZodRadius, want1 = d <= NewRadius;
                okAll &= eng[0] == want0 && eng[1] == want1 && (shots[0] > 0) == want0 && (shots[1] > 0) == want1;
                if (d >= 124 && d <= 131) band &= !eng[0] && eng[1];
                rows.Add($"{d} px ({M(d)}): off {(eng[0] ? "fires" : "-")} {shots[0]}, on {(eng[1] ? "fires" : "-")} {shots[1]}");
            }
            Check(okAll, "a still enemy grunt on flat ground, 3 s, shots in brackets: engaged up to 120 px off, up to 132 px on: " + string.Join("; ", rows));
            Check(band, "15.5 to 16.4 m (124 to 131 px): engaged only with the switch on");
            Fight j0, j1;
            using (Rule(false)) { j0 = New(settings, "jeep", 128, 7, false); Run(j0, 3.0); }
            using (Rule(true)) { j1 = New(settings, "jeep", 128, 7, false); Run(j1, 3.0); }
            bool je0 = j0.engaged;
            Check(!je0 && j1.engaged, $"a jeep at 128 px (16 m): off {(je0 ? "engaged" : "not engaged")}, on {(j1.engaged ? "engaged" : "not engaged")} ({j1.shots} shots in 3 s)");
        }

        // ------------------------------------------------------------------------------------------------ T
        public static readonly string[] TtkTargets = { "grunt", "psycho", "jeep", "light" };
        public static readonly int[] TtkPx = { 48, 96, 112 };

        public sealed class Cell { public double shots, hits, snipes, secs; public int n, killed, emptied, expect, minHits = int.MaxValue, maxHits; }

        public static Cell Ttk(string settings, string type, int d, bool on, bool hullOnly, int seeds)
        {
            var c = new Cell();
            using (Rule(on))
                for (int s = 1; s <= seeds; s++)
                {
                    var f = New(settings, type, d, 1000 + 37 * s, hullOnly);
                    Run(f, 240.0);
                    c.n++; c.shots += f.shots; c.hits += f.hits; c.snipes += f.snipes; c.secs += f.killTick < 0 ? 240.0 : f.killTick * ZConst.TICK;
                    if (f.killed) c.killed++; if (f.emptied) c.emptied++;
                    c.expect = f.expectHits; c.minHits = Math.Min(c.minHits, f.hits); c.maxHits = Math.Max(c.maxHits, f.hits);
                }
            c.shots /= c.n; c.hits /= c.n; c.snipes /= c.n; c.secs /= c.n;
            return c;
        }

        public const string TtkHeading = "## time to kill";

        static void T(string settings, int seeds)
        {
            sb.AppendLine(TtkHeading + $" (the gatling against a still, harmless target; {seeds} seeds per cell, the same seeds off and on; mean shots / hits / seconds)");
            var hits = new List<string>(); bool exact = true, ratio = true, shotsDown = true, secsDown = true, allDown = true;
            foreach (var type in TtkTargets)
                foreach (int d in TtkPx)
                {
                    var a = Ttk(settings, type, d, false, true, seeds); var b = Ttk(settings, type, d, true, true, seeds);
                    double rh = b.hits / a.hits, rs = b.shots / a.shots, rt = b.secs / a.secs;
                    exact &= a.minHits == a.expect && a.maxHits == a.expect && b.minHits == b.expect && b.maxHits == b.expect;
                    ratio &= rh > 0.80 && rh < 0.93; shotsDown &= rs < 0.97; secsDown &= rt < 0.97; allDown &= a.killed == a.n && b.killed == b.n;
                    sb.AppendLine($"TTK   {type,-6} {d,3} px ({M(d),6}) hull: off {F(a.shots, "0")} shots / {F(a.hits, "0")} hits / {F(a.secs, "0.00")} s, on {F(b.shots, "0")} / {F(b.hits, "0")} / {F(b.secs, "0.00")} s; " +
                                  $"on/off hits {F(rh, "0.000")} shots {F(rs, "0.000")} time {F(rt, "0.000")} (1/1.15 = 0.870); destroyed {a.killed}/{a.n} and {b.killed}/{b.n}");
                }
            Check(exact, "every hull-only fight needs exactly sum(ceil(HP / HP a hit)) hits: 39 HP a hit off, 45 on (no other factor on the gatling's hit)");
            Check(ratio && allDown, "hits to kill on / off between 0.80 and 0.93 for every target and distance (about 1 / 1.15; the truncated HP a hit 45 / 39 = 1.154 and the last hit's rounding give the spread); every target destroyed");
            Check(shotsDown && secsDown, "shots and seconds to kill fall with the hits for every cell (on / off under 0.97)");
            foreach (var type in new[] { "jeep", "light" })
            {
                var a = Ttk(settings, type, 96, false, false, seeds); var b = Ttk(settings, type, 96, true, false, seeds);
                Info($"TTK   {type,-6}  96 px, the driver snipable as in the game (DC2: a driver hit removes the pair's own HP, {(type == "jeep" ? "the jeep's floor 57" : "the gatling-light pair 155")}, unchanged by DV170): " +
                     $"off {F(a.shots, "0")} shots / {F(a.hits, "0")} hull hits / {F(a.snipes, "0")} driver hits / {F(a.secs, "0.00")} s (destroyed {a.killed}, emptied {a.emptied}), " +
                     $"on {F(b.shots, "0")} / {F(b.hits, "0")} / {F(b.snipes, "0")} / {F(b.secs, "0.00")} s (destroyed {b.killed}, emptied {b.emptied}); on/off time {F(b.secs / a.secs, "0.000")}");
            }
        }

        // ------------------------------------------------------------------------------------------------ D
        static void D(string settings)
        {
            Fight a, b;
            using (Rule(true)) { a = New(settings, "psycho", 96, 4242, false); Run(a, 240); b = New(settings, "psycho", 96, 4242, false); Run(b, 240); }
            Check(a.killTick == b.killTick && a.shots == b.shots && a.hits == b.hits && a.hash == b.hash && a.killTick > 0,
                $"the same fight twice (on, a psycho squad at 96 px, seed 4242): tick {a.killTick} / {b.killTick}, shots {a.shots} / {b.shots}, hits {a.hits} / {b.hits}, events and the random stream {a.hash:X16} / {b.hash:X16}");
            Fight c, e;
            using (Rule(false)) { c = New(settings, "psycho", 96, 4242, false); Run(c, 240); }
            using (Rule(false)) { e = New(settings, "psycho", 96, 4242, false); Run(e, 240); }
            Check(c.killTick == e.killTick && c.hash == e.hash && c.killTick > a.killTick,
                $"the same fight off twice: tick {c.killTick} / {e.killTick}, {c.hash:X16} / {e.hash:X16}; on it ends earlier ({a.killTick} < {c.killTick})");
        }

#if !OPENZ_HARNESS || OZ_UE
        // ------------------------------------------------------------------------------------------------ U (the Unit editor's stats code; off Unity only in the harness built with its two View files, OZ_UE)
        static void U(string settings, string statSets)
        {
            var rules = UeRules.FromGame();
            double r1, d1, r0, d0;
            using (Rule(true)) { var oz = OzUeLiveStats.Table(ZSettings.Parse(settings), rules); r1 = oz[ZGatlingBuff.Type][(int)UeStat.Range].value; d1 = oz[ZGatlingBuff.Type][(int)UeStat.Damage].value; }
            using (Rule(false)) { var oz = OzUeLiveStats.Table(ZSettings.Parse(settings), rules); r0 = oz[ZGatlingBuff.Type][(int)UeStat.Range].value; d0 = oz[ZGatlingBuff.Type][(int)UeStat.Damage].value; }
            Check(r1 == NewRadius / 8.0 && d1 == NewHit && r0 == ZodRadius / 8.0 && d0 == ZodHit,
                $"the Unit editor's OpenZ set (its default stats, the gatling's card): on {F(r1, "0.0")} m / {d1} HP a hit, off ('Zod exact') {F(r0, "0.0")} m / {d0}");
            if (statSets == null) { Info("the stats sets file (unit_stat_sets.json) was not given: its ZodEngine set not checked"); return; }
            using (Rule(true))
            {
                var sets = OzUeSetData.Parse(statSets);
                bool has = sets.zod.TryGetValue(ZGatlingBuff.Type, out var z) && z[(int)UeStat.Range].HasNumber;
                double zr = has ? z[(int)UeStat.Range].value : -1, zd = has ? z[(int)UeStat.Damage].value : -1;
                Check(sets.error == null && zr == ZodRadius / 8.0 && zd == ZodHit && ZWorld.GatlingBuffDeviation,
                    $"its ZodEngine set with the switch on: the gatling {F(zr, "0.0")} m / {zd} HP a hit (Zod's){(sets.error != null ? "; " + sets.error : "")}");
                UeNote nd = null, nr = null;
                if (sets.notes.TryGetValue(ZGatlingBuff.Type, out var gn)) { gn.TryGetValue(UeStat.Damage, out nd); gn.TryGetValue(UeStat.Range, out nr); }
                const string OnPurpose = "OpenZ changed it on purpose";
                Check(nd != null && nr != null && nd.play != null && nd.play.StartsWith(NewHit + " HP a hit") && nr.play != null && nr.play.StartsWith(NewRadius + " px = 16.5 m")
                      && nd.diff == OnPurpose && nr.diff == OnPurpose,
                    $"its notes for the gatling (the tooltip's 'OpenZ in play' / 'Difference', as DV147's mech note): damage \"{nd?.play}\" / \"{nd?.diff}\"; range \"{nr?.play}\" / \"{nr?.diff}\"");
            }
        }
#endif

#if !OPENZ_HARNESS || OZ_LS
        // ------------------------------------------------------------------------------------------------ L (the battlefield screen's check of the saved Custom profile; off Unity only in the harness built with View/OzLevelStats.cs's RulesAsLevelStart, OZ_LS)
        static void L(string settings)
        {
            bool mh = ZWorld.MechHpDeviation, gb = ZWorld.GatlingBuffDeviation;
            try
            {
                bool zodExact = UnityEngine.PlayerPrefs.GetInt("oz_zod_exact", 0) != 0;                  // read only
                ZWorld.MechHpDeviation = false; ZWorld.GatlingBuffDeviation = false;                       // a fresh session: no level has set the switches yet
                var r = OzLevelStats.RulesAsLevelStart();
                bool on = ZWorld.GatlingBuffDeviation;
                Check(on == !zodExact && on == ZWorld.MechHpDeviation,
                    $"the battlefield screen's rules (OzLevelStats.RulesAsLevelStart) in a fresh session: the gatling's switch {(on ? "on" : "off")} as the level start sets it ('Zod exact' {(zodExact ? "on" : "off")}), as DV147's mech switch");
                if (zodExact) { Info("'Zod exact' is on: the profile part of L is not run (CUSTOM is not offered in 'Zod exact')"); return; }
                var table = OzLevelStatsPlan.OpenZTable(settings, r);
                var g = table[ZGatlingBuff.Type];
                string saved = OzLevelStatsPlan.TextOf(table, r, null);                                      // an untouched profile as the 1.4 Unit editor's SAVE writes it
                var p = OzLevelStatsPlan.Read(saved, "check", 0, table, r);
                Dictionary<string, UeCell[]> zodTable;
                using (Rule(false)) zodTable = OzLevelStatsPlan.OpenZTable(settings, r);
                var q = OzLevelStatsPlan.Read(saved, "check", 0, zodTable, r);                               // the same profile checked against Zod's gatling: the screen without the kit's line
                Check(p.Error == null && p.Changed == 0 && !p.Exists && q.Error == null && q.Changed == 2,
                    $"an untouched profile saved in 1.4 (the gatling {F(g[(int)UeStat.Range].value)} m / {g[(int)UeStat.Damage].value}): {p.Changed} change(s), no selector, as before the kit; "
                    + $"checked against Zod's gatling (the screen without the kit's line) it would count {q.Changed}: {string.Join(", ", q.ChangedNotes)}");
                string old = OzLevelStatsPlan.TextOf(table, r, new Dictionary<string, double> { [ZGatlingBuff.Type + ".range"] = ZodRadius / 8.0, [ZGatlingBuff.Type + ".dmg"] = ZodHit });
                var o = OzLevelStatsPlan.Read(old, "check", 0, table, r);
                Check(o.Error == null && o.Changed == 2 && o.Exists && o.ChangedNotes.All(n => n.StartsWith(ZGatlingBuff.Type + " ")),
                    $"a profile saved before 1.4 (every value OpenZ's of its day, the gatling 15 m / 39): {o.Changed} change(s) ({string.Join(", ", o.ChangedNotes)}): "
                    + "the selector shows and CUSTOM plays that old gatling (not migrated; the README says so)");
            }
            finally { ZWorld.MechHpDeviation = mh; ZWorld.GatlingBuffDeviation = gb; }
        }

        /// <summary>L alone (the harness runs it a second time with 'Zod exact' on in its PlayerPrefs stand-in; in Unity L reads the real preference
        /// once, inside <see cref="Run"/>).</summary>
        public static string LevelScreen(string settings)
        {
            pass = fail = 0; sb = new StringBuilder();
            try { L(settings); }
            catch (Exception ex) { Check(false, "L threw " + ex.GetType().Name + ": " + ex.Message); }
            sb.AppendLine($"[OpenZ V1.4 gatling buff level screen] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }
#endif

        public static string Run(string settings, Func<string, string> map, string statSets = null, int seeds = 6)
        {
            pass = fail = 0; sb = new StringBuilder();
            var t0 = DateTime.UtcNow;
            try
            {
                S(settings); O(settings); R(settings); T(settings, seeds); D(settings);
#if !OPENZ_HARNESS || OZ_UE
                U(settings, statSets);
#endif
#if !OPENZ_HARNESS || OZ_LS
                L(settings);
#endif
            }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace); }
            Info($"run time {(DateTime.UtcNow - t0).TotalSeconds:0.0} s (the check's own, not a rule input)");
            sb.AppendLine($"[OpenZ V1.4 gatling buff] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

        /// <summary>The Deviations row's probe: the settings off and on, a grunt at 128 px (16 m) engaged only on, a grunt squad at 96 px
        /// needing about 1 / 1.15 of the hits.</summary>
        public static string Quick(string settings)
        {
            pass = fail = 0; sb = new StringBuilder();
            try
            {
                var g0 = Parse(settings, false).Unit(ZGatlingBuff.Type); var g1 = Parse(settings, true).Unit(ZGatlingBuff.Type);
                Check(g0.attack_radius == ZodRadius && g0.Damage == ZodHit && g1.attack_radius == NewRadius && g1.Damage == NewHit,
                    $"off {g0.attack_radius} px / {g0.Damage} HP a hit (Zod), on {g1.attack_radius} px / {g1.Damage} HP a hit");
                bool e0, e1;
                using (Rule(false)) { var f = New(settings, "grunt", 128, 7, false, 1); Run(f, 3.0); e0 = f.engaged; }
                using (Rule(true)) { var f = New(settings, "grunt", 128, 7, false, 1); Run(f, 3.0); e1 = f.engaged; }
                Check(!e0 && e1, $"a grunt at 128 px (16 m): off {(e0 ? "engaged" : "out of reach")}, on {(e1 ? "engaged" : "out of reach")}");
                var a = Ttk(settings, "grunt", 96, false, true, 2); var b = Ttk(settings, "grunt", 96, true, true, 2);
                Check(a.hits == a.expect && b.hits == b.expect && b.hits / a.hits > 0.80 && b.hits / a.hits < 0.93,
                    $"a grunt squad at 96 px: {F(a.hits, "0")} hits off, {F(b.hits, "0")} on ({F(b.hits / a.hits, "0.000")})");
            }
            catch (Exception ex) { Check(false, "the probe threw " + ex.GetType().Name + ": " + ex.Message); }
            sb.AppendLine($"[OpenZ V1.4 gatling buff quick] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

#if UNITY_EDITOR && !OPENZ_HARNESS
        [UnityEditor.MenuItem("OpenZ/V1.4/Gatling Buff: Count (headless, DV170)")]
        static void Menu()
        {
            var ta = UnityEngine.Resources.Load<UnityEngine.TextAsset>(OzUeSetData.ResourcePath);
            OzV11Kit.RunMenu("gatling buff: count (DV170)", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText, ta ? ta.text : null));
        }
#endif
    }
}
