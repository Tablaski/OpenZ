using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The MAMMOTH's and the BIZON's counted rules check (1.5, DV180 / DV181; Sim/ZBigTanks.cs). Headless: the simulation only (ZWorld.Create +
    /// Tick), no scene, no Play mode, nothing saved; the game's switches (OzV11Kit.GameSwitches) put back after. Every line a counted PASS / FAIL:
    ///  1. the rows: each = the rules from the heavy / light / medium tank rows field for field (health over 1.0 is kept: 10891 / 11729 HP);
    ///     a settings text with its own unit.bizon.* key keeps that field;
    ///  2. a placed one: a crewed vehicle, its driver a PSYCHO with the switch on (Zod's grunt with it off), the heavy's class flags, its room
    ///     on the field (24 / 26 px), the heavy's driver hits and snipe chances, its turret falls;
    ///  3. the Mammoth fires two shells per cycle, one-two (the second ZWorld.HeavyPairGap s later, flag2), one with its switch off;
    ///  4. the Bizon holds its course: it never steps aside from a shell and never duels (a Mammoth and a heavy do), and ordered at a
    ///     howitzer that outranges it it closes in without ever backing off until it is in range;
    ///  5. speeds: 40 tiles of open ground, the Mammoth 9 / 8 and the Bizon 9 / 7 of the heavy's time;
    ///  6. Unit editor only: no build list of any producer at levels 0-5 lists them (switches on), and no level map places one;
    ///  7. deterministic: the same fight twice gives the same world hash; a fight without them is the same with the switches on or off.
    /// </summary>
    public static class OzV15BigTanksCount
    {
        static string N(double v) => v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The parity report's probe: the rows, the placed checks, the twin barrels, the Bizon's course and the lists (no map needed).</summary>
        public static string Quick(string settings) => Run(settings, null, false);

        public static string Run(string settings, Func<string, string> map, bool full = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# the MAMMOTH and the BIZON, counted (DV180 / DV181, Sim/ZBigTanks.cs)");
            bool m0 = ZWorld.MammothDeviation, b0 = ZWorld.BizonDeviation;
            try
            {
                ZWorld.MammothDeviation = ZWorld.BizonDeviation = true;
                Rows(sb, settings);
                Placed(sb, settings);
                Pair(sb, settings);
                Course(sb, settings);
                Lists(sb, settings, map);
                if (full) { Speed(sb, settings); Same(sb, settings); }
            }
            catch (Exception e) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + e.GetType().Name + ": " + e.Message)); }
            finally { ZWorld.MammothDeviation = m0; ZWorld.BizonDeviation = b0; }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ 1. the rows
        static void Rows(StringBuilder sb, string settings)
        {
            var s = ZSettings.Parse(settings);
            var h = s.Unit("heavy"); var l = s.Unit("light"); var mt = s.Unit(ZMTank.Type);
            var m = s.Unit(ZBigTanks.Mammoth); var b = s.Unit(ZBigTanks.Bizon);
            sb.AppendLine(OzV11Kit.Verdict(m != null && b != null && h != null && l != null && mt != null, "both rows are in the parsed settings"));
            if (m == null || b == null || h == null || l == null || mt == null) return;
            foreach (var (row, want) in new[] { (m, ZBigTanks.DeriveMammoth(h)), (b, ZBigTanks.DeriveBizon(h, l, mt)) })
            {
                var bad = ZBigTanks.Compare(want, row).Where(c => Math.Abs(c.want - c.got) > 1e-9).Select(c => $"{c.field} {N(c.got)} (want {N(c.want)})").ToList();
                sb.AppendLine(OzV11Kit.Verdict(bad.Count == 0, $"{row.name}: the row = the rules, field for field" + (bad.Count == 0 ? "" : ": " + string.Join(", ", bad))));
            }
            sb.AppendLine(OzV11Kit.Verdict(Math.Abs(m.MaxHealth - h.MaxHealth * 1.3) <= 1.0 && m.move_speed == 8 && m.attack_radius == 151 && m.attack_missile_speed == 149
                && Math.Abs(m.attack_speed - h.attack_speed * 1.25) < 1e-6 && m.attack_damage == h.attack_damage && m.attack_damage_radius == h.attack_damage_radius,
                $"MAMMOTH: {m.MaxHealth} HP (heavy {h.MaxHealth} x 1.30), speed {m.move_speed} (heavy {h.move_speed}, -{100.0 * (h.move_speed - m.move_speed) / h.move_speed:0.0} %), range {m.attack_radius} px (heavy {h.attack_radius} x 1.05), " +
                $"shell {m.attack_missile_speed} px/s (heavy {h.attack_missile_speed} x 1.10), reload {N(m.attack_speed)} s (heavy {N(h.attack_speed)} x 1.25), the heavy's {m.Damage} HP / {m.attack_damage_radius} px in two shells"));
            sb.AppendLine(OzV11Kit.Verdict(Math.Abs(b.MaxHealth - h.MaxHealth * 1.4) <= 1.0 && b.move_speed == 7 && b.attack_radius == l.attack_radius && b.attack_damage == mt.attack_damage
                && b.attack_damage_radius == mt.attack_damage_radius && b.attack_missile_speed == mt.attack_missile_speed && Math.Abs(b.attack_speed - mt.attack_speed * 2.0) < 1e-6,
                $"BIZON: {b.MaxHealth} HP (heavy x 1.40), speed {b.move_speed} (-{100.0 * (h.move_speed - b.move_speed) / h.move_speed:0.0} %), range {b.attack_radius} px (the light tank's), " +
                $"the medium tank's shot {b.Damage} HP / {b.attack_damage_radius} px / {b.attack_missile_speed} px/s, reload {N(b.attack_speed)} s (the medium tank's {N(mt.attack_speed)} x 2)"));
            sb.AppendLine(OzV11Kit.Verdict(m.MaxHealth > ZConst.MAX_UNIT_HEALTH && b.MaxHealth > ZConst.MAX_UNIT_HEALTH, $"health over 1.0 is kept ({m.MaxHealth} / {b.MaxHealth} HP > {ZConst.MAX_UNIT_HEALTH}): no clamp"));
            var s2 = ZSettings.Parse(settings + "\nunit.bizon.attack_radius=130\n");
            sb.AppendLine(OzV11Kit.Verdict(s2.Unit(ZBigTanks.Bizon).attack_radius == 130 && s2.Unit(ZBigTanks.Bizon).move_speed == b.move_speed, "a settings text's own unit.bizon.* key wins for that field, the rest stay derived"));
        }

        // ------------------------------------------------------------------ 2. a placed one against the heavy
        static void Placed(StringBuilder sb, string settings)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(48, 48), 1);
            OzV11Kit.Dummy(w, 700, 700);
            var hv = OzV11Kit.Place(w, "heavy", 160, 160, Team.Red);
            foreach (var t in ZBigTanks.Types)
            {
                var o = OzV11Kit.Place(w, t, t == ZBigTanks.Mammoth ? 260 : 360, 160, Team.Red);
                sb.AppendLine(OzV11Kit.Verdict(o.Kind == ZKind.Vehicle && o.U != null && o.Drivers.Count == 1 && o.DriverType == ZBigTanks.DefaultDriver
                    && o.Drivers[0] == w.S.Unit(ZBigTanks.DefaultDriver).MaxHealth, $"{t}: a crewed vehicle, its driver a {o.DriverType} ({o.Drivers[0]} HP), 2 x 2 tiles"));
                sb.AppendLine(OzV11Kit.Verdict(o.HasLid == hv.HasLid && o.CanBeSnipedFlag == hv.CanBeSnipedFlag && o.HasExplosivesFlag == hv.HasExplosivesFlag && o.DamageIsMissile == hv.DamageIsMissile && !o.CanSnipe
                    && o.IsHatchVehicle, $"{t}: the heavy's class flags (lid {o.HasLid}, snipable {o.CanBeSnipedFlag}, explosives {o.HasExplosivesFlag}, missile {o.DamageIsMissile}, hatch vehicle {o.IsHatchVehicle})"));
                int want = ZBigTanks.RadiusPx(t);
                sb.AppendLine(OzV11Kit.Verdict(ZPathing.VehicleRadiusOf(t) == want && ZPathing.Radius(o) == want, $"{t}: room on the field {ZPathing.Radius(o)} px (heavy {ZPathing.Radius(hv)})"));
                bool sd = ZWorld.SnipeDeviation; ZWorld.SnipeDeviation = true;
                try
                {
                    var rows = new List<string>(); bool ok = true;
                    foreach (var a in new[] { "grunt", "psycho", "jeep", "gatling", "sniper" })
                    {
                        var at = OzV11Kit.Place(w, a, 600, 400, Team.Blue);
                        int x = w.DriverHitDamage(at, o), y = w.DriverHitDamage(at, hv);
                        double cx = ZWorld.SnipeChanceOn(at, o), cy = ZWorld.SnipeChanceOn(at, hv);
                        ok &= x == y && Math.Abs(cx - cy) < 1e-12; rows.Add($"{a} {x}/{y}");
                    }
                    sb.AppendLine(OzV11Kit.Verdict(ok, $"{t}: driver hits and snipe chances = the heavy's ({string.Join(", ", rows)})"));
                }
                finally { ZWorld.SnipeDeviation = sd; }
                sb.AppendLine(OzV11Kit.Verdict(ZWorld.IsTurretFaller(o) && ZWorld.TurretCoverM(t) > ZWorld.TurretCoverM("heavy"), $"{t}: its turret falls when it dies (cover {ZWorld.TurretCoverM(t):0.00} m, heavy {ZWorld.TurretCoverM("heavy"):0.0})"));
            }
            ZWorld.MammothDeviation = ZWorld.BizonDeviation = false;
            var off = OzV11Kit.Place(w, ZBigTanks.Bizon, 460, 300, Team.Red);
            sb.AppendLine(OzV11Kit.Verdict(off.DriverType == "grunt", $"with the switches off a placed one gets Zod's {off.DriverType}"));
            ZWorld.MammothDeviation = ZWorld.BizonDeviation = true;
        }

        // ------------------------------------------------------------------ 3. the Mammoth's two barrels
        static void Pair(StringBuilder sb, string settings)
        {
            foreach (bool on in new[] { true, false })
            {
                ZWorld.MammothDeviation = on; bool hp = ZWorld.HeavyPairDeviation; ZWorld.HeavyPairDeviation = true;
                try
                {
                    var w = OzV11Kit.World(settings, OzV11Kit.Plain(48, 24), 2);
                    OzV11Kit.Dummy(w, 700, 300);
                    var m = OzV11Kit.Place(w, ZBigTanks.Mammoth, 160, 192, Team.Red);
                    var t = OzV11Kit.Place(w, "heavy", 160 + 120, 192, Team.Blue); t.MoveSpeed = 0; t.Damage = 0; t.MaxHealth = t.Health = 100000000;
                    OzV11Kit.PlayerAttack(w, m, t);
                    var shots = new List<(double at, bool second)>();
                    double t0 = w.Time;
                    while (w.Time < t0 + 12)
                    {
                        w.Tick(ZConst.TICK);
                        foreach (var e in w.Events) if (e.type == ZEventType.MissileFired && e.objId == m.Id) shots.Add((w.Time, e.flag2));
                        w.Events.Clear();
                    }
                    int firsts = shots.Count(x => !x.second), seconds = shots.Count(x => x.second);
                    var gaps = new List<double>();
                    for (int i = 1; i < shots.Count; i++) if (shots[i].second && !shots[i - 1].second) gaps.Add(shots[i].at - shots[i - 1].at);
                    if (on) sb.AppendLine(OzV11Kit.Verdict(firsts >= 2 && seconds == firsts && gaps.Count == seconds && gaps.All(g => Math.Abs(g - ZWorld.HeavyPairGap) < ZConst.TICK + 1e-6),
                        $"MAMMOTH: {firsts} cycles in 12 s, each a one-two of 2 shells ({seconds} second shells, gaps {string.Join(" / ", gaps.Select(g => g.ToString("0.00")))} s = {ZWorld.HeavyPairGap:0.00} s)"));
                    else sb.AppendLine(OzV11Kit.Verdict(firsts >= 2 && seconds == 0, $"MAMMOTH with DV180 off: one shell a cycle ({firsts} shells, {seconds} second shells)"));
                }
                finally { ZWorld.HeavyPairDeviation = hp; ZWorld.MammothDeviation = true; }
            }
        }

        // ------------------------------------------------------------------ 4. the Bizon holds its course
        static void Course(StringBuilder sb, string settings)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(64, 32), 3);
            OzV11Kit.Dummy(w, 980, 480);
            var b = OzV11Kit.Place(w, ZBigTanks.Bizon, 200, 256, Team.Red);
            var hv = OzV11Kit.Place(w, "heavy", 200, 100, Team.Red);
            var m = OzV11Kit.Place(w, ZBigTanks.Mammoth, 200, 400, Team.Red);
            int held0 = ZObject.BizonDodgesHeld;
            bool bd = b.DodgeMissile(b.CenterX + 10, b.CenterY, 1.0), hd = hv.DodgeMissile(hv.CenterX + 10, hv.CenterY, 1.0);
            sb.AppendLine(OzV11Kit.Verdict(!bd && ZObject.BizonDodgesHeld == held0 + 1 && !b.DuelCapable && hv.DuelCapable && m.DuelCapable,
                $"the Bizon does not step aside from a shell (it would: {bd}; a heavy beside it: {hd}) and never duels (Bizon {b.DuelCapable}, heavy {hv.DuelCapable}, Mammoth {m.DuelCapable})"));
            w.RemoveNow(hv); w.RemoveNow(m);
            // ordered at a howitzer that outranges it: it closes in and never backs off
            var g = OzV11Kit.Place(w, "howitzer", 200 + 420, 256, Team.Blue); g.MaxHealth = g.Health = 100000000;
            OzV11Kit.CpuAttack(w, b, g);
            double best = OzV11Kit.Dist(b, g), worstBack = 0, t0 = w.Time, inRangeAt = -1; int shellsAt = 0;
            while (w.Time < t0 + 120)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events) if (e.type == ZEventType.MissileFired && e.otherId == b.Id) shellsAt++;
                w.Events.Clear();
                double d = OzV11Kit.Dist(b, g);
                if (d > best) worstBack = Math.Max(worstBack, d - best); else best = d;
                if (inRangeAt < 0 && b.WithinAttackRadius(g)) { inRangeAt = w.Time - t0; break; }
                if (!OzV11Kit.Alive(b)) break;
            }
            sb.AppendLine(OzV11Kit.Verdict(inRangeAt > 0 && worstBack <= 2.0 && b.Waypoints.All(x => x.mode != WpMode.Dodge),
                $"ordered at a howitzer 420 px away (it outranges the Bizon's {b.AttackRadius} px): in range after {inRangeAt:0.0} s, the most it ever moved back {worstBack:0.0} px, shells fired at it {shellsAt}, steps aside held {ZObject.BizonDodgesHeld - held0}"));
        }

        // ------------------------------------------------------------------ 5. speed on open ground
        static double Drive(string settings, string type)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(64, 24), 5);
            OzV11Kit.Dummy(w, 900, 300);
            var v = OzV11Kit.Place(w, type, 5 * 16 + 16, 12 * 16, Team.Red);
            int x1 = 45 * 16 + 16;
            OzV11Kit.PlayerMove(w, v, x1, 12 * 16, false);
            double t0 = w.Time;
            while (w.Time < t0 + 400) { w.Tick(ZConst.TICK); w.Events.Clear(); if (v.CenterX >= x1 - 2) return w.Time - t0; }
            return -1;
        }
        static void Speed(StringBuilder sb, string settings)
        {
            double th = Drive(settings, "heavy"), tm = Drive(settings, ZBigTanks.Mammoth), tb = Drive(settings, ZBigTanks.Bizon);
            double km = th > 0 ? tm / th : -1, kb = th > 0 ? tb / th : -1;
            sb.AppendLine(OzV11Kit.Verdict(th > 0 && Math.Abs(km - 9.0 / 8.0) < 0.04 && Math.Abs(kb - 9.0 / 7.0) < 0.05,
                $"40 tiles of open ground: heavy {th:0.00} s, Mammoth {tm:0.00} s ({km:0.000} x, want 1.125), Bizon {tb:0.00} s ({kb:0.000} x, want 1.286)"));
        }

        // ------------------------------------------------------------------ 6. Unit editor only
        static void Lists(StringBuilder sb, string settings, Func<string, string> map)
        {
            bool mt = ZWorld.MTankDeviation; ZWorld.MTankDeviation = true;
            try
            {
                var bad = new List<string>(); int lists = 0;
                foreach (var b in new[] { "fort_front", "fort_back", "vehicle_factory", "robot_factory" })
                    for (int lv = 0; lv <= 5; lv++) { lists++; foreach (var t in ZBuildLists.For(b, lv)) if (ZBigTanks.Is(t)) bad.Add($"{b} L{lv} lists {t}"); }
                sb.AppendLine(OzV11Kit.Verdict(bad.Count == 0, $"no build list lists them ({lists} lists, every switch on){(bad.Count > 0 ? ": " + string.Join("; ", bad) : "")}: no factory, fort or computer build ever makes one"));
            }
            finally { ZWorld.MTankDeviation = mt; }
            if (map == null) return;
            var found = new List<string>();
            foreach (var lv in new[] { "L02", "L17", "L16" })
            {
                string text; try { text = map(lv); } catch { continue; }
                if (string.IsNullOrEmpty(text)) continue;
                var w = ZWorld.Create(settings, text, 1);
                int n = w.Objects.Count(o => ZBigTanks.Is(o.Type));
                if (n > 0) found.Add(lv + " " + n);
            }
            sb.AppendLine(OzV11Kit.Verdict(found.Count == 0, "no level map places one" + (found.Count > 0 ? ": " + string.Join(", ", found) : "")));
        }

        // ------------------------------------------------------------------ 7. deterministic
        static string Fight(string settings, bool big, int seed)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(56, 32), seed);
            OzV11Kit.Dummy(w, 860, 480);
            var a = OzV11Kit.Place(w, big ? ZBigTanks.Mammoth : "heavy", 160, 200, Team.Red);
            var b = OzV11Kit.Place(w, big ? ZBigTanks.Bizon : "light", 160, 330, Team.Red);
            var c = OzV11Kit.Place(w, "heavy", 560, 260, Team.Blue);
            var d = OzV11Kit.Place(w, "grunt", 600, 330, Team.Blue);
            OzV11Kit.CpuAttack(w, a, c); OzV11Kit.CpuAttack(w, b, c);
            while (w.Time < 60) { w.Tick(ZConst.TICK); w.Events.Clear(); }
            unchecked
            {
                long h = 17;
                foreach (var o in w.Objects.OrderBy(q => q.Id)) h = h * 31 + o.Id * 7919 + o.LocX * 131 + o.LocY * 137 + o.Health + (o.Destroyed ? 1 : 0);
                return h.ToString("X");
            }
        }
        static void Same(StringBuilder sb, string settings)
        {
            string a = Fight(settings, true, 7), b = Fight(settings, true, 7);
            sb.AppendLine(OzV11Kit.Verdict(a == b, $"the same fight of a Mammoth and a Bizon twice (seed 7, 60 s): the same world hash ({a} / {b})"));
            string on = Fight(settings, false, 9);
            ZWorld.MammothDeviation = ZWorld.BizonDeviation = false;
            string off = Fight(settings, false, 9);
            ZWorld.MammothDeviation = ZWorld.BizonDeviation = true;
            sb.AppendLine(OzV11Kit.Verdict(on == off, $"a fight without them is the same with their switches on or off ({on} / {off}): no other unit changes"));
        }
    }
}
