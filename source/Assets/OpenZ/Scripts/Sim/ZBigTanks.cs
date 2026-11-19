using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ 1.5 deviations DV180 "the MAMMOTH" and DV181 "the BIZON": two big tracked tanks, not in Z, placed only by the Unit editor (like
    /// EvilZod, DV161): no level, no factory, no build list and no computer build ever makes them. Each has its own type id
    /// (<see cref="Mammoth"/>, <see cref="Bizon"/>: no "heavy", "medium" or "light" token, so no picture or list keyed by those words takes them).
    /// Their rows are filled in code (<see cref="Fill"/>, ZSettings.Parse) from the rows of the parsed text, so default_settings.txt does not change:
    /// <list type="bullet">
    /// <item>MAMMOTH (DV180): the heavy tank's row with: health x <see cref="MammothHealthFactor"/> (+30 %), move speed x <see cref="MammothSpeedFactor"/>
    /// to the nearest whole number (-10 %: 9 -> 8), range x <see cref="MammothRangeFactor"/> (+5 %: 144 -> 151 px), each shell x <see cref="MammothShellFactor"/>
    /// faster (135 -> 149 px/s), reload x <see cref="MammothReloadFactor"/> (4.088 -> 5.11 s); the heavy's damage and blast; two barrels fired one-two
    /// like the heavy (DV30 widened: half the damage each, the second <see cref="ZWorld.HeavyPairGap"/> s later).</item>
    /// <item>BIZON (DV181): the heavy tank's row with: health x <see cref="BizonHealthFactor"/> (+40 %), move speed x <see cref="BizonSpeedFactor"/> to the
    /// nearest whole number (-20 %: 9 -> 7); the light tank's range (120 px); the medium tank's shot (damage, blast, shell speed: 2083 HP, 40 px,
    /// 225 px/s); one barrel; a slow reload, the medium tank's x <see cref="BizonReloadFactor"/> (1.2972 -> 2.5944 s). It never backs off in a fight:
    /// no step aside from shells (Zod's DodgeMissile) and no duel manoeuvre (DV9's arc, flank or reverse): it drives straight at its target
    /// until it is in range and fires from there (<see cref="ZObject.BizonHoldsCourse"/>).</item>
    /// <item>Both: a lid tank like the heavy (hatch, explosives, driver snipes at the heavy's floor, turret debris); an owned one starts with a
    /// PSYCHO driver (<see cref="DefaultDriver"/>), not Zod's grunt; spacing radius <see cref="MammothRadiusPx"/> / <see cref="BizonRadiusPx"/> px
    /// (x1.2 / x1.3 of the heavy's 20 px: the drawn tanks are 1.2 / 1.3 times the heavy tank v2).</item>
    /// </list>
    /// Switches: <see cref="ZWorld.MammothDeviation"/>, <see cref="ZWorld.BizonDeviation"/> (View/ZGame.cs: on at every start, off in 'Zod exact';
    /// headless default off; the parity report and the counted checks hold them off). With a switch off the type still has its row, but the
    /// Unit editor does not offer it and its own rules (the psycho driver, the twin barrels, the Bizon's course) are Zod's (a grunt, one shell, the
    /// step aside). Deterministic: doubles and whole numbers only, no random draw of its own, no clock.
    /// </summary>
    public static class ZBigTanks
    {
        public const string Mammoth = "mammoth", Bizon = "bizon";
        /// <summary>Both, in the Unit editor's order (after the heavy tank).</summary>
        public static readonly string[] Types = { Mammoth, Bizon };
        /// <summary>The rows they are made from.</summary>
        public const string Heavy = "heavy", Light = "light";
        /// <summary></summary>
        public const string DefaultDriver = "psycho";

        public const double MammothHealthFactor = 1.30, MammothSpeedFactor = 0.90, MammothRangeFactor = 1.05, MammothShellFactor = 1.10, MammothReloadFactor = 1.25;
        public const double BizonHealthFactor = 1.40, BizonSpeedFactor = 0.80, BizonReloadFactor = 2.0;
        /// <summary>Spacing radius (ZPathing.VehicleRadiusOf): the heavy's 20 px x 1.2 / x 1.3.</summary>
        public const int MammothRadiusPx = 24, BizonRadiusPx = 26;
        /// <summary>The hull's rear from its centre for the exit lanes (ZWorld.ExitLanes: the heavy's 21 px x 1.2 / x 1.3).</summary>
        public const int MammothRearPx = 25, BizonRearPx = 27;
        /// <summary>The turret's debris cover (ZWorld.HeavyFall TurretCoverM: the heavy's 1.4 m x 1.2 / x 1.3).</summary>
        public const double MammothTurretCoverM = 1.68, BizonTurretCoverM = 1.82;
        /// <summary>DV9 duel margin (px under the attack radius it keeps): the heavy's 26.</summary>
        public const int DuelMarginPx = 26;
        /// <summary>DV171 EXIT DRIVER: ticks the driver is held while he climbs down (the heavy's 136 + the taller hull: 1.2 / 1.3 of its climb).</summary>
        public const int MammothExitHoldTicks = 150, BizonExitHoldTicks = 156;

        public static bool Is(string type) => type == Mammoth || type == Bizon;
        /// <summary>The heavy tank and the two big tanks: the rules that are the heavy's for them (lists, class flags, snipes, debris).</summary>
        public static bool IsHeavyLike(string type) => type == Heavy || type == Mammoth || type == Bizon;
        /// <summary>This type's own switch is on.</summary>
        public static bool On(string type) => type == Mammoth ? ZWorld.MammothDeviation : type == Bizon ? ZWorld.BizonDeviation : false;

        /// <summary>Rounded like the settings rows (six decimal places, halves away from zero).</summary>
        public static double R6(double v) => Math.Round(v, 6, MidpointRounding.AwayFromZero);
        /// <summary>A whole number field (move speed, range, shell speed): the nearest whole number, halves away from zero.</summary>
        public static int Whole(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);

        /// <summary>The MAMMOTH's row from the heavy tank's.</summary>
        public static ZUnitSettings DeriveMammoth(ZUnitSettings heavy)
        {
            var u = new ZUnitSettings { name = Mammoth };
            if (heavy == null) return u;
            u.group_amount = 1; u.build_time = heavy.build_time; u.max_run_time = heavy.max_run_time;
            u.health = R6(heavy.health * MammothHealthFactor);
            u.move_speed = Whole(heavy.move_speed * MammothSpeedFactor);
            u.attack_radius = Whole(heavy.attack_radius * MammothRangeFactor);
            u.attack_damage = heavy.attack_damage; u.attack_damage_chance = heavy.attack_damage_chance; u.attack_damage_radius = heavy.attack_damage_radius;
            u.attack_missile_speed = Whole(heavy.attack_missile_speed * MammothShellFactor);
            u.attack_speed = R6(heavy.attack_speed * MammothReloadFactor);
            u.attack_snipe_chance = heavy.attack_snipe_chance;
            return u;
        }

        /// <summary>The BIZON's row from the heavy tank's (health, speed), the light tank's (range) and the medium tank's (its shot and reload).</summary>
        public static ZUnitSettings DeriveBizon(ZUnitSettings heavy, ZUnitSettings light, ZUnitSettings mtank)
        {
            var u = new ZUnitSettings { name = Bizon };
            if (heavy == null || light == null || mtank == null) return u;
            u.group_amount = 1; u.build_time = heavy.build_time; u.max_run_time = heavy.max_run_time;
            u.health = R6(heavy.health * BizonHealthFactor);
            u.move_speed = Whole(heavy.move_speed * BizonSpeedFactor);
            u.attack_radius = light.attack_radius;
            u.attack_damage = mtank.attack_damage; u.attack_damage_chance = mtank.attack_damage_chance; u.attack_damage_radius = mtank.attack_damage_radius;
            u.attack_missile_speed = mtank.attack_missile_speed;
            u.attack_speed = R6(mtank.attack_speed * BizonReloadFactor);
            u.attack_snipe_chance = mtank.attack_snipe_chance;
            return u;
        }

        /// <summary>ZSettings.Parse, after the medium tank's row is filled and before the censor: both rows from the rows of the text. A later change
        /// of those rows (Custom stats) does not move them; a text with its own unit.mammoth / unit.bizon keys keeps those fields (a key given in
        /// the text wins, field by field).</summary>
        public static void Fill(ZSettings s)
        {
            if (s == null) return;
            var heavy = s.Unit(Heavy); var light = s.Unit(Light); var mtank = s.Unit(ZMTank.Type);
            if (heavy == null || light == null || mtank == null) return;
            Put(s, DeriveMammoth(heavy));
            Put(s, DeriveBizon(heavy, light, mtank));
        }

        static void Put(ZSettings s, ZUnitSettings d)
        {
            s.Units[d.name] = d;
            string pre = "unit." + d.name + ".";
            foreach (var kv in s.Raw)
                if (kv.Key.StartsWith(pre, StringComparison.Ordinal)) SetField(d, kv.Key.Substring(pre.Length), kv.Value);
            Filled++;
        }

        /// <summary>The settings text's own field for one of the two rows (ZSettings' SetUnitField, the same truncations).</summary>
        static void SetField(ZUnitSettings u, string f, double d)
        {
            switch (f)
            {
                case "group_amount": u.group_amount = (int)d; break;
                case "move_speed": u.move_speed = (int)d; break;
                case "attack_radius": u.attack_radius = (int)d; break;
                case "attack_damage": u.attack_damage = d; break;
                case "attack_damage_chance": u.attack_damage_chance = d; break;
                case "attack_damage_radius": u.attack_damage_radius = (int)d; break;
                case "attack_missile_speed": u.attack_missile_speed = (int)d; break;
                case "attack_speed": u.attack_speed = d; break;
                case "attack_snipe_chance": u.attack_snipe_chance = d; break;
                case "health": u.health = d; break;
                case "build_time": u.build_time = (int)d; break;
                case "max_run_time": u.max_run_time = d; break;
            }
        }

        /// <summary>Statistics (the counted check): rows filled.</summary>
        public static int Filled;

        /// <summary>ZWorld.CreateObject: the robot an owned vehicle starts with. Zod: a grunt (zvehicle.cpp:155-168); a big tank with its switch on: a psycho.</summary>
        public static string FirstDriver(string type) => Is(type) && On(type) ? DefaultDriver : "grunt";

        /// <summary>ZPathing.VehicleRadiusOf.</summary>
        public static int RadiusPx(string type) => type == Mammoth ? MammothRadiusPx : BizonRadiusPx;

        /// <summary>The fields the counted check compares one by one.</summary>
        public static IEnumerable<(string field, double want, double got)> Compare(ZUnitSettings want, ZUnitSettings got)
        {
            yield return ("build_time", want.build_time, got.build_time);
            yield return ("health", want.health, got.health);
            yield return ("move_speed", want.move_speed, got.move_speed);
            yield return ("max_run_time", want.max_run_time, got.max_run_time);
            yield return ("group_amount", want.group_amount, got.group_amount);
            yield return ("attack_radius", want.attack_radius, got.attack_radius);
            yield return ("attack_damage", want.attack_damage, got.attack_damage);
            yield return ("attack_damage_chance", want.attack_damage_chance, got.attack_damage_chance);
            yield return ("attack_damage_radius", want.attack_damage_radius, got.attack_damage_radius);
            yield return ("attack_missile_speed", want.attack_missile_speed, got.attack_missile_speed);
            yield return ("attack_speed", want.attack_speed, got.attack_speed);
            yield return ("attack_snipe_chance", want.attack_snipe_chance, got.attack_snipe_chance);
        }
    }

    public partial class ZObject
    {
        /// <summary>DV181: the Bizon holds its course in a fight: no step aside from a shell (ZObject.DodgeMissile, before any random draw) and no DV9
        /// duel manoeuvre (it stands and fires in range, Zod's way); out of range it keeps driving straight at its target (Zod's attack approach).</summary>
        public bool BizonHoldsCourse => ZWorld.BizonDeviation && Kind == ZKind.Vehicle && Type == ZBigTanks.Bizon;
        /// <summary>DV180: the Mammoth fires its two barrels one-two like the heavy tank (DV30).</summary>
        public bool MammothPair => ZWorld.MammothDeviation && Kind == ZKind.Vehicle && Type == ZBigTanks.Mammoth;
        /// <summary>Statistics (the counted check): shells the Bizon did not step aside from.</summary>
        public static int BizonDodgesHeld;
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV180 the MAMMOTH (Sim/ZBigTanks.cs). ZGame turns it on at every start (off in 'Zod exact'); headless default off.</summary>
        public static bool MammothDeviation = false;
        /// <summary>DV181 the BIZON (Sim/ZBigTanks.cs). ZGame turns it on at every start (off in 'Zod exact'); headless default off.</summary>
        public static bool BizonDeviation = false;

        /// <summary>The parity report and the older counted checks: both off while this lives (a Play session leaves them on).</summary>
        public static IDisposable BigTanksOff() => new BigTanksOffHold();
        sealed class BigTanksOffHold : IDisposable
        {
            readonly bool m = MammothDeviation, b = BizonDeviation;
            public BigTanksOffHold() { MammothDeviation = false; BizonDeviation = false; }
            public void Dispose() { MammothDeviation = m; BizonDeviation = b; }
        }
    }
}
