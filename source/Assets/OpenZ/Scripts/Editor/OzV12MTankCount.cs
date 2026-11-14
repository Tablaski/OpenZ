using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The MEDIUM TANK's counted rules check (1.2 owner item 139, DV145; Sim/ZMTank.cs). Headless: the simulation only (ZWorld.Create + Tick),
    /// no scene, no Play mode, nothing saved; only public members of the simulation (the editor menu OpenZ/Medium Tank/3 and the kit's
    /// off-editor runner call the same Run). Every line is a counted PASS / FAIL:
    ///  1. the rules row: unit.mtank.* of the settings text = the mech's build time, the mech's health x 1.10 (rounded like the other health
    ///     values), the mech's speed x 0.85 (to the whole number: Z's speeds are whole numbers), the mech's run time, the light tank's gun
    ///     field for field; the text has its own row (nothing derived in its place);
    ///  2. a placed medium tank: health, speed, damage, range, blast, shell speed, reload (DV156: the light tank's x ZMTank.ReloadFactor), hit and snipe chances, and the class flags (lid,
    ///     snipable driver, explosives, missile damage) against a placed mech and a placed light tank; its driver-hit damage and snipe chance
    ///     from grunts, psychos, jeeps and gatlings = the light tank's (DC2 open seat);
    ///  3. the build lists: with DV145 on, every fort / vehicle factory list of levels 0-5 that has the mech has the medium tank RIGHT AFTER
    ///     it and no list without the mech has it (the same tech level); robot factories never; with DV145 off Zod's lists exactly;
    ///  4. production: level 2's red producer builds one medium tank in the mech's build time (the same producer, the same flags and health);
    ///  5. speed: the medium tank, the mech and the light tank each drive the same 40 tiles on open ground: the medium tank's time = the
    ///     mech's x 12 / 10 (it is 1 / 0.85 = 1.18 at the speed's whole number: about 15 % slower);
    ///  6. the CPU: bot vs bot on level 17 (both forts at level 5), the game's switches + DV145, two seeds of 20 minutes: both CPUs build
    ///     medium tanks AND mechs, the medium tank share of the two between 30 % and 70 %; with DV145 off (Zod exact) no medium tank at all.
    /// </summary>
    public static class OzV12MTankCount
    {
        public static string Run(string settings, Func<string, string> map, int matchSeconds = 1200)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# the MEDIUM TANK, counted (DV145, Sim/ZMTank.cs)");
            bool was = ZWorld.MTankDeviation;
            try
            {
                Rules(sb, settings);
                Placed(sb, settings);
                Lists(sb);
                Production(sb, settings, map);
                Speed(sb, settings);
                Cpu(sb, settings, map, matchSeconds);
            }
            catch (Exception e) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + e.GetType().Name + ": " + e.Message)); }
            finally { ZWorld.MTankDeviation = was; }
            return sb.ToString();
        }

        static string N(double v) => v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ 1. the rules row
        static void Rules(StringBuilder sb, string settings)
        {
            int filled0 = ZMTank.Filled;
            var s = ZSettings.Parse(settings);
            var u = s.Unit(ZMTank.Type); var mech = s.Unit(ZMTank.Mech); var light = s.Unit(ZMTank.Gun);
            sb.AppendLine(OzV11Kit.Verdict(u != null && ZMTank.Filled == filled0, $"the settings text has its own unit.{ZMTank.Type} row (nothing derived in its place: {ZMTank.Filled - filled0})"));
            if (u == null || mech == null || light == null) return;
            var want = ZMTank.Derive(mech, light);
            var bad = ZMTank.Compare(want, u).Where(c => Math.Abs(c.want - c.got) > 1e-9).Select(c => $"{c.field} {N(c.got)} (want {N(c.want)})").ToList();
            sb.AppendLine(OzV11Kit.Verdict(bad.Count == 0, bad.Count == 0 ? "the row = the rules, field for field (12 fields)" : "the row differs: " + string.Join(", ", bad)));
            sb.AppendLine(OzV11Kit.Verdict(u.build_time == mech.build_time, $"build time {u.build_time} s = the mech's {mech.build_time} s"));
            sb.AppendLine(OzV11Kit.Verdict(u.MaxHealth == (int)Math.Round(mech.MaxHealth * ZMTank.HealthFactor) || Math.Abs(u.MaxHealth - mech.MaxHealth * ZMTank.HealthFactor) <= 1.0,
                $"health {u.MaxHealth} HP = the mech's {mech.MaxHealth} x 1.10 = {mech.MaxHealth * ZMTank.HealthFactor:0.0} (health {N(u.health)} = {N(mech.health)} x 1.10 rounded to six decimal places)"));
            sb.AppendLine(OzV11Kit.Verdict(u.move_speed == (int)Math.Round(mech.move_speed * ZMTank.SpeedFactor, MidpointRounding.AwayFromZero) && u.move_speed < mech.move_speed,
                $"move speed {u.move_speed} = the mech's {mech.move_speed} x 0.85 = {mech.move_speed * ZMTank.SpeedFactor:0.00} to the whole number ({100.0 * (mech.move_speed - u.move_speed) / mech.move_speed:0.0} % slower; 15 % more driving time = {mech.move_speed / 1.15:0.00} -> {Math.Round(mech.move_speed / 1.15)} too)"));
            sb.AppendLine(OzV11Kit.Verdict(u.attack_radius == light.attack_radius && u.attack_damage == light.attack_damage && u.attack_damage_radius == light.attack_damage_radius
                && u.attack_missile_speed == light.attack_missile_speed && Math.Abs(u.attack_speed - ZMTank.Reload(light.attack_speed)) < 1e-9 && u.attack_damage_chance == light.attack_damage_chance && u.attack_snipe_chance == light.attack_snipe_chance,
                $"the light tank's gun: range {u.attack_radius} px, damage {u.Damage}, blast {u.attack_damage_radius} px, shell {u.attack_missile_speed} px/s; its own reload {N(u.attack_speed)} s = the light tank's x {N(ZMTank.ReloadFactor)} (DV156) (light: {light.attack_radius}, {light.Damage}, {light.attack_damage_radius}, {light.attack_missile_speed}, {N(light.attack_speed)} s); the mech's rockets: {mech.Damage} dmg, {N(mech.attack_speed)} s"));
            // a text WITHOUT the row: the derived row fills it (never a zero-health unit)
            var bare = string.Join("\n", settings.Replace("\r\n", "\n").Split('\n').Where(l => !l.StartsWith("unit." + ZMTank.Type + ".")));
            var s2 = ZSettings.Parse(bare); var u2 = s2.Unit(ZMTank.Type);
            bool same = u2 != null && ZMTank.Compare(want, u2).All(c => Math.Abs(c.want - c.got) < 1e-9);
            sb.AppendLine(OzV11Kit.Verdict(same && ZMTank.Filled == filled0 + 1, $"a settings text without the row gets the derived one ({u2?.MaxHealth} HP, speed {u2?.move_speed}, build {u2?.build_time} s)"));
        }

        // ------------------------------------------------------------------ 2. a placed medium tank against the mech and the light tank
        static void Placed(StringBuilder sb, string settings)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(40, 40), 1);
            OzV11Kit.Dummy(w, 600, 600);
            var mt = OzV11Kit.Place(w, ZMTank.Type, 160, 160, Team.Red);
            var me = OzV11Kit.Place(w, ZMTank.Mech, 240, 160, Team.Red);
            var lt = OzV11Kit.Place(w, ZMTank.Gun, 320, 160, Team.Red);
            sb.AppendLine(OzV11Kit.Verdict(mt.Kind == ZKind.Vehicle && mt.U != null && mt.Drivers.Count == 1, $"a placed medium tank is a crewed vehicle (driver {mt.DriverType}, {mt.Drivers.Count} aboard), 2 x 2 tiles"));
            sb.AppendLine(OzV11Kit.Verdict(mt.MaxHealth > me.MaxHealth && Math.Abs(mt.MaxHealth - me.MaxHealth * 1.1) <= 1.0 && mt.MoveSpeed < me.MoveSpeed,
                $"health {mt.MaxHealth} (mech {me.MaxHealth}, light {lt.MaxHealth}); speed {mt.MoveSpeed} (mech {me.MoveSpeed}, light {lt.MoveSpeed})"));
            sb.AppendLine(OzV11Kit.Verdict(mt.Damage == lt.Damage && mt.AttackRadius == lt.AttackRadius && mt.DamageRadius == lt.DamageRadius && mt.MissileSpeed == lt.MissileSpeed
                && Math.Abs(mt.AttackSpeed - ZMTank.Reload(lt.AttackSpeed)) < 1e-9 && mt.DamageChance == lt.DamageChance && mt.SnipeChance == lt.SnipeChance,
                $"its gun as placed = the light tank's: damage {mt.Damage}, range {mt.AttackRadius}, blast {mt.DamageRadius}, shell {mt.MissileSpeed}; its own reload {N(mt.AttackSpeed)} s = the light tank's {N(lt.AttackSpeed)} s x {N(ZMTank.ReloadFactor)} (DV156)"));
            sb.AppendLine(OzV11Kit.Verdict(mt.HasLid == lt.HasLid && mt.CanBeSnipedFlag == lt.CanBeSnipedFlag && mt.HasExplosivesFlag == lt.HasExplosivesFlag && mt.DamageIsMissile == lt.DamageIsMissile && mt.CanSnipe == lt.CanSnipe && mt.HasLid,
                $"class flags = the light tank's: lid {mt.HasLid}, snipable driver {mt.CanBeSnipedFlag}, explosives {mt.HasExplosivesFlag}, missile damage {mt.DamageIsMissile}, snipes {mt.CanSnipe}"));
            mt.LidOpen = true; lt.LidOpen = true;
            sb.AppendLine(OzV11Kit.Verdict(mt.CanBeSniped() && lt.CanBeSniped(), $"with the lid open its driver can be sniped (medium {mt.CanBeSniped()}, light {lt.CanBeSniped()})"));
            mt.LidOpen = false; lt.LidOpen = false;
            bool snipeDev = ZWorld.SnipeDeviation; ZWorld.SnipeDeviation = true;
            try
            {
                var rows = new List<string>(); bool ok = true;
                foreach (var a in new[] { "grunt", "psycho", "jeep", "gatling", "sniper" })
                {
                    var at = OzV11Kit.Place(w, a, 400, 400, Team.Blue);
                    int hm = w.DriverHitDamage(at, mt), hl = w.DriverHitDamage(at, lt);
                    double cm = ZWorld.SnipeChanceOn(at, mt), cl = ZWorld.SnipeChanceOn(at, lt);
                    ok &= hm == hl && Math.Abs(cm - cl) < 1e-12;
                    rows.Add($"{a} {hm}/{hl} HP, {N(cm)}/{N(cl)}");
                }
                sb.AppendLine(OzV11Kit.Verdict(ok, "driver hits and snipe chances on it = on the light tank (medium / light): " + string.Join("; ", rows)));
            }
            finally { ZWorld.SnipeDeviation = snipeDev; }
            sb.AppendLine(OzV11Kit.Verdict(ZWorld.TurretCoverM(ZMTank.Type) > 0 && ZWorld.IsTurretFaller(mt), $"its turret falls like the tanks' when it dies (cover {ZWorld.TurretCoverM(ZMTank.Type):0.0} m; light {ZWorld.TurretCoverM(ZMTank.Gun):0.0}, mech {ZWorld.TurretCoverM(ZMTank.Mech):0.0})"));
            sb.AppendLine(OzV11Kit.Verdict(ZPathing.VehicleRadiusOf(ZMTank.Type) == 18 && ZPathing.Radius(mt) == 18, $"its room on the field {ZPathing.VehicleRadiusOf(ZMTank.Type)} px (light {ZPathing.VehicleRadiusOf(ZMTank.Gun)}, mech {ZPathing.VehicleRadiusOf(ZMTank.Mech)}, heavy {ZPathing.VehicleRadiusOf("heavy")})"));
            sb.AppendLine(OzV11Kit.Verdict(ZSettings.Vehicles.Contains(ZMTank.Type) && Array.IndexOf(ZSettings.Vehicles, ZMTank.Type) == Array.IndexOf(ZSettings.Vehicles, ZMTank.Mech) + 1,
                "ZSettings.Vehicles: " + string.Join(", ", ZSettings.Vehicles)));
        }

        // ------------------------------------------------------------------ 3. the build lists
        static void Lists(StringBuilder sb)
        {
            foreach (bool on in new[] { true, false })
            {
                ZWorld.MTankDeviation = on;
                var bad = new List<string>(); int lists = 0, with = 0;
                foreach (var b in new[] { "fort_front", "fort_back", "vehicle_factory", "robot_factory" })
                    for (int lv = 0; lv <= 5; lv++)
                    {
                        var l = ZBuildLists.For(b, lv).ToList(); lists++;
                        int m = l.IndexOf(ZMTank.Mech), t = l.IndexOf(ZMTank.Type);
                        if (!on) { if (t >= 0) bad.Add($"{b} L{lv} lists it with the switch off"); continue; }
                        if (m >= 0 && t != m + 1) bad.Add($"{b} L{lv}: mech at {m}, medium tank at {t}");
                        if (m < 0 && t >= 0) bad.Add($"{b} L{lv}: medium tank without the mech");
                        if (t >= 0) with++;
                    }
                sb.AppendLine(OzV11Kit.Verdict(bad.Count == 0 && (!on || with == 12), on
                    ? $"DV145 on: {with} of {lists} build lists have the medium tank, each right after the mech (forts and vehicle factories at levels 2-5, the mech's levels; robot factories never){(bad.Count > 0 ? ": " + string.Join("; ", bad) : "")}"
                    : $"DV145 off ('Zod exact'): no list has it ({lists} lists){(bad.Count > 0 ? ": " + string.Join("; ", bad) : "")}"));
            }
            ZWorld.MTankDeviation = true;
            sb.AppendLine("  vehicle factory level 2 with DV145: " + string.Join(", ", ZBuildLists.For("vehicle_factory", 2)));
            sb.AppendLine("  fort level 5 with DV145: " + string.Join(", ", ZBuildLists.For("fort_front", 5)));
        }

        // ------------------------------------------------------------------ 4. production
        static double BuildOne(string settings, string map, string type, out int made, out string where)
        {
            var w = ZWorld.Create(settings, map, 3);
            var f = w.Objects.Where(o => o.Owner == Team.Red && o.IsProducer && !o.Destroyed).OrderBy(o => o.Type == "vehicle_factory" ? 0 : 1).ThenBy(o => o.Id).First();
            if (f.Level < 2) f.Level = 2;                                                                       // the mech's tech level
            where = $"the red {f.Type} at tile ({f.LocX / 16},{f.LocY / 16}), level {f.BuildLevel}";
            foreach (var b in w.Objects.Where(o => o.IsProducer)) b.StopBuildingProduction();
            w.Events.Clear();
            made = 0;
            if (!w.StartBuilding(Team.Red, f, type)) { where += $" REFUSED the start (game over {w.GameOver}, current {f.CurUnit ?? "none"})"; return -1; }
            double t0 = w.Time;
            while (w.Time < t0 + 600)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                    if (e.type == ZEventType.UnitBuilt && e.otherId == f.Id && w.GetAny(e.objId)?.Type == type) { made++; return w.Time - t0; }
                w.Events.Clear();
            }
            return -1;
        }
        static void Production(StringBuilder sb, string settings, Func<string, string> map)
        {
            ZWorld.MTankDeviation = true;
            double tm = BuildOne(settings, map("L02"), ZMTank.Type, out int nm, out string at), te = BuildOne(settings, map("L02"), ZMTank.Mech, out int ne, out _);
            sb.AppendLine(OzV11Kit.Verdict(nm == 1 && ne == 1 && tm > 0 && Math.Abs(tm - te) <= 0.02, $"level 2, {at}: one medium tank built in {tm:0.00} s, one mech in {te:0.00} s (the same time; the rules' build time x this producer's flags and health)"));
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
            while (w.Time < t0 + 300)
            {
                w.Tick(ZConst.TICK); w.Events.Clear();
                if (v.CenterX >= x1 - 2) return w.Time - t0;
            }
            return -1;
        }
        static void Speed(StringBuilder sb, string settings)
        {
            double tm = Drive(settings, ZMTank.Type), te = Drive(settings, ZMTank.Mech), tl = Drive(settings, ZMTank.Gun);
            double k = te > 0 ? tm / te : -1;
            sb.AppendLine(OzV11Kit.Verdict(tm > 0 && te > 0 && k > 1.12 && k < 1.25, $"40 tiles of open ground: medium tank {tm:0.00} s, mech {te:0.00} s, light tank {tl:0.00} s: {k:0.000} x the mech's time ({100 * (1 - 1 / k):0.0} % slower)"));
        }

        // ------------------------------------------------------------------ 6. the CPU
        static void Cpu(StringBuilder sb, string settings, Func<string, string> map, int seconds)
        {
            foreach (bool on in new[] { true, false })
            {
                var built = new Dictionary<string, int>();
                int picks = 0;
                foreach (int seed in on ? new[] { 1, 2 } : new[] { 1 })
                {
                    ZWorld.MTankDeviation = on; ZWorld.BotHeavyDeviation = true;
                    var w = ZWorld.Create(settings, map("L17"), seed);
                    var red = new ZBot(w, Team.Red, ZBot.SeedFor(Team.Red, seed)); var blue = new ZBot(w, Team.Blue, ZBot.SeedFor(Team.Blue, seed));
                    w.Bots.Add(blue); w.Bots.Add(red);
                    while (w.Time < seconds && !w.GameOver)
                    {
                        w.Tick(ZConst.TICK);
                        foreach (var e in w.Events)
                        {
                            if (e.type != ZEventType.UnitBuilt) continue;
                            var u = w.GetAny(e.objId); if (u == null) continue;
                            string key = u.Owner + " " + u.Type;
                            built[key] = built.TryGetValue(key, out int n) ? n + 1 : 1;
                        }
                        w.Events.Clear();
                    }
                    picks += red.MTankPicks + blue.MTankPicks;
                }
                int Get(string k) => built.TryGetValue(k, out int n) ? n : 0;
                string all = string.Join(", ", built.OrderBy(kv => kv.Key).Select(kv => kv.Key + " " + kv.Value));
                if (on)
                {
                    int rm = Get("Red " + ZMTank.Type), bm = Get("Blue " + ZMTank.Type), re = Get("Red " + ZMTank.Mech), be = Get("Blue " + ZMTank.Mech);
                    double share = (rm + bm) / Math.Max(1.0, rm + bm + re + be);
                    sb.AppendLine(OzV11Kit.Verdict(rm > 0 && bm > 0 && re > 0 && be > 0 && share >= 0.30 && share <= 0.70,
                        $"the CPU, level 17, 2 seeds x {seconds / 60} min, the game's switches + DV145: medium tanks red {rm} / blue {bm}, mechs red {re} / blue {be} (medium tank share {100 * share:0} %; {picks} rounds turned a mech into a medium tank)"));
                    sb.AppendLine("  built: " + all);
                }
                else
                    sb.AppendLine(OzV11Kit.Verdict(Get("Red " + ZMTank.Type) + Get("Blue " + ZMTank.Type) == 0, $"DV145 off ('Zod exact'), level 17, 1 seed: no medium tank built ({all})"));
            }
        }
    }
}
