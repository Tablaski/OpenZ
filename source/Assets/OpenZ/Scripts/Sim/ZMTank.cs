using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ deviation DV145 "the MEDIUM TANK" (1.2 owner item 139): a new tracked vehicle, not in Z. Z had a light, a medium and a heavy
    /// tank; in OpenZ Zod's "medium" type is drawn and played as the walking MECH, so the new tank has its own type id <see cref="Type"/>
    /// ("mtank", never the mech's "medium"). Its rules, from the live numbers of default_settings.txt (unit.mtank.* there; when a settings
    /// text has no unit.mtank row, <see cref="FillIfMissing"/> derives the same numbers from the mech's and the light tank's rows):
    ///  - build time = the mech's (unit.medium.build_time);
    ///  - health = <see cref="HealthRow"/>, a number of its own: 0.743244 = 7432 HP (the mech's Zod health x <see cref="HealthFactor"/> 1.10,
    ///    fixed when this unit was added; it does not follow the mech: DV147 lowers the mech's health, never this one);
    ///  - move speed = the mech's x <see cref="SpeedFactor"/> (0.85), to the nearest whole number (Z's move_speed is an integer: 12 x 0.85 =
    ///    10.2 -> 10, 16.7 % slower; "15 % more driving time" = 12 / 1.15 = 10.4 gives 10 too); its run time = the mech's;
    ///  - the light tank's gun: range, damage, blast, shell speed, hit and snipe chances (one shell per shot); its RELOAD is its own,
    ///    DV156 (1.3 owner item 14): the light tank's x <see cref="ReloadFactor"/> (1.15),
    ///    rounded to six decimal places like the settings rows (<see cref="Reload"/>): 1.128 s -> 1.2972 s, 13 % fewer shots a minute;
    ///  - the same tech level as the mech: every build list that has the mech lists the medium tank right after it (ZBuildLists.MTank.cs);
    ///  - a lid tank like the light tank (hatch, driver snipes, explosives, turret debris, crush and boarding rules by the class flags);
    ///  - the CPU builds it beside the mech, about one each (ZBot.MTankPick).
    /// Switch: <see cref="ZWorld.MTankDeviation"/> (ZGame: on, off in 'Zod exact'; headless default off: every Zod row and every older
    /// counted check reads Zod's build lists). With it off the type still exists in the rules (a placed one fights), but no producer lists it.
    /// </summary>
    public static class ZMTank
    {
        public const string Type = "mtank";
        /// <summary>The unit whose gun it carries and the unit whose build time and speed it is measured against.</summary>
        public const string Gun = "light", Mech = "medium";
        public const double HealthFactor = 1.10, SpeedFactor = 0.85;
        /// <summary>DV156 (1.3 owner item 14): the medium tank's reload = the light tank's x this (15 % slower: 1.128 s -> 1.2972 s). Its row in
        /// default_settings.txt (unit.mtank.attack_speed) carries the result; Editor/OzV12MTankCount.cs checks that the two agree. The owner's
        /// other choice, 10 % slower: 1.10 here and 1.240800 in the row.</summary>
        public const double ReloadFactor = 1.15;
        /// <summary>DV156: the medium tank's reload from the light tank's (rounded to six decimal places like every settings row).</summary>
        public static double Reload(double lightReload) => Math.Round(lightReload * ReloadFactor, 6);
        /// <summary>DV147: its own health field, absolute (7432 HP; the mech's Zod 0.675676 x <see cref="HealthFactor"/> rounded, as first set).</summary>
        public const double HealthRow = 0.743244;

        /// <summary>The rules row the medium tank must have under these settings (the mech's and the light tank's rows).</summary>
        public static ZUnitSettings Derive(ZUnitSettings mech, ZUnitSettings light)
        {
            var u = new ZUnitSettings { name = Type };
            if (mech == null || light == null) return u;
            u.group_amount = mech.group_amount;
            u.build_time = mech.build_time;
            u.health = HealthRow;                                                    // DV147: its own number, not the mech's x 1.10 (the mech's health changes with DV147)
            u.move_speed = (int)Math.Round(mech.move_speed * SpeedFactor, MidpointRounding.AwayFromZero);
            u.max_run_time = mech.max_run_time;
            u.attack_radius = light.attack_radius; u.attack_damage = light.attack_damage; u.attack_damage_chance = light.attack_damage_chance;
            u.attack_damage_radius = light.attack_damage_radius; u.attack_missile_speed = light.attack_missile_speed; u.attack_speed = Reload(light.attack_speed);   // DV156: its own reload
            u.attack_snipe_chance = light.attack_snipe_chance;
            return u;
        }

        /// <summary>ZSettings.Parse, before the censor: a settings text without any unit.mtank.* key gets the derived row (so the medium tank
        /// is never a zero-health unit); a text with the row keeps its own numbers.</summary>
        public static void FillIfMissing(ZSettings s)
        {
            if (s == null || !s.Units.TryGetValue(Type, out var u)) return;
            foreach (var k in s.Raw.Keys) if (k.StartsWith("unit." + Type + ".", StringComparison.Ordinal)) return;
            var d = Derive(s.Unit(Mech), s.Unit(Gun));
            u.group_amount = d.group_amount; u.build_time = d.build_time; u.health = d.health; u.move_speed = d.move_speed; u.max_run_time = d.max_run_time;
            u.attack_radius = d.attack_radius; u.attack_damage = d.attack_damage; u.attack_damage_chance = d.attack_damage_chance;
            u.attack_damage_radius = d.attack_damage_radius; u.attack_missile_speed = d.attack_missile_speed; u.attack_speed = d.attack_speed;
            u.attack_snipe_chance = d.attack_snipe_chance;
            Filled++;
        }
        /// <summary>Statistics (the counted check): settings texts that had no unit.mtank row.</summary>
        public static int Filled;

        /// <summary>The fields that must equal <see cref="Derive"/> (the counted check lists them one by one).</summary>
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

    public sealed partial class ZWorld
    {
        /// <summary>DV145 the medium tank in the build lists (right after the mech, ZBuildLists.MTank.cs) and in the CPU's production
        /// (ZBot.MTankPick). ZGame turns it on at every start (off in 'Zod exact'); headless checks keep Zod's lists unless they switch it on.</summary>
        public static bool MTankDeviation = false;

        /// <summary>The parity report and the older counted checks: Zod's build lists while this lives (a Play session leaves the switch on).</summary>
        public static IDisposable MTankOff() => new MTankOffHold();
        sealed class MTankOffHold : IDisposable
        {
            readonly bool was = MTankDeviation;
            public MTankOffHold() { MTankDeviation = false; }
            public void Dispose() { MTankDeviation = was; }
        }
    }

    public sealed partial class ZBot
    {
        /// <summary>DV145: a medium or medium tank build already this far along (game seconds) is finished, never swapped (a loss on the field
        /// mid-build does not throw the half-built vehicle away). Two AI rounds on Easy fit under it, so the swap after a finished build
        /// always lands in the first rounds of the next one.</summary>
        public const double MTANK_KEEP_S = 2.5;
        /// <summary>DV145 statistics (the counted check): rounds that turned Zod's mech into a medium tank.</summary>
        public int MTankPicks { get; private set; }

        /// <summary>DV145: the CPU treats the medium tank like the mech. Where Zod's pick (after DV115's heavy tanks / missile launchers) is the
        /// mech and the producer can make the medium tank, it builds whichever of the two its team has fewer of on the field (the mech when
        /// even), so its armies carry about as many medium tanks as mechs, losses included. A build already under way is finished first;
        /// DV115's rule for the vehicle in the making at 8:00 holds for a medium tank too. Deterministic: counted on the bot's own objects,
        /// no random draw, the same STOP_BUILDING + START_BUILDING commands. Public for the checks.</summary>
        public string MTankPick(ZObject b, string zodPick, string pick)
        {
            if (!ZWorld.MTankDeviation || b == null || !ZBuildLists.InList(b.Type, b.BuildLevel, ZMTank.Type)) return pick;
            if (zodPick == ZMTank.Mech && pick != zodPick && b.Building && b.CurUnit == ZMTank.Type && b.BInit + TIME_EPS < HEAVY_FROM) return ZMTank.Type;   // DV115: the one in the making at 8:00 is finished first
            if (pick != ZMTank.Mech) return pick;
            if (b.Building && (b.CurUnit == ZMTank.Mech || b.CurUnit == ZMTank.Type) && W.Time - b.BInit > MTANK_KEEP_S) return b.CurUnit;
            int mechs = 0, tanks = 0;
            foreach (var o in W.Objects)
            {
                if (o.Owner != Team || o.Kind != ZKind.Vehicle || o.Destroyed) continue;
                if (o.Type == ZMTank.Mech) mechs++; else if (o.Type == ZMTank.Type) tanks++;
            }
            if (tanks >= mechs) return ZMTank.Mech;
            MTankPicks++;
            return ZMTank.Type;
        }
    }

    public static partial class ZBuildLists
    {
        static readonly Dictionary<string[][], string[][]> withMTank = new Dictionary<string[][], string[][]>();

        /// <summary>DV145: the list of this level, with the medium tank right after the mech wherever the mech is listed (the same tech level as
        /// the mech in forts and vehicle factories) while ZWorld.MTankDeviation is on; Zod's own list otherwise.</summary>
        static IReadOnlyList<string> MTankList(string[][] table, int level)
        {
            if (!ZWorld.MTankDeviation) return table[level];
            if (!withMTank.TryGetValue(table, out var t)) withMTank[table] = t = WithMTank(table);
            return t[level];
        }

        static string[][] WithMTank(string[][] t)
        {
            var r = new string[t.Length][];
            for (int i = 0; i < t.Length; i++)
            {
                var l = new List<string>(t[i]);
                int k = l.IndexOf(ZMTank.Mech);
                if (k >= 0 && !l.Contains(ZMTank.Type)) l.Insert(k + 1, ZMTank.Type);
                r[i] = l.ToArray();
            }
            return r;
        }
    }
}
