using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// "Gatling add 10% range and 15% more damage per shot"). Only the unit type "gatling" (the GATLING of the CANNONS row: built,
    /// stored, placed, the empty ones on the maps, crewed by either side, the player's and the computer's). Zod's row in
    /// default_settings.txt stays Zod's (unit.gatling.attack_radius=120, attack_damage=0.003976; zsettings.cpp:203-214), and while the
    /// switch is on every parse of the game's settings text gives the gatling:
    /// <list type="bullet">
    /// <item><b>Range</b> x 1.10 in whole px, rounded: <see cref="Radius"/>(120) = 132 px = 16.5 m (8 px = 1 m; Zod's 15 m).</item>
    /// <item><b>Damage per shot</b> x <see cref="DamageFactor"/>: the row's own double times 1.15 (0.003976 x 1.15 = 0.0045724), so the HP one
    /// hit removes (ZUnitSettings.Damage, zobject.cpp:183, truncated like Zod) is 45 instead of 39.</item>
    /// </list>
    /// Everything else of the gatling is Zod's: fire rate (attack_speed 0.1 s), hit chance 0.65, snipe chance 0.4, health, build time.
    /// No other type changes: the jeep's gun, the Electrobot's tesla gatling (type "electrobot"), an APC's crew weapon, the fort's tower
    /// guns (type "gun" on every map) and every other unit read their own rows. One place, ZSettings.Parse (after DV147's mech line), so
    /// every reader of the rules sees the same numbers: every gatling object (ZWorld.CreateObject copies the row into AttackRadius and
    /// Damage), the range rings, the computer's own uses of a gun's reach (ZBot.CpuShuttle's danger circles read the object's
    /// AttackRadius), DV162's high-ground bonus (10 % on top of the plain range), the fog of war's sight (View/OzFogOfWar.cs SightPx:
    /// attack_radius + agro + 24 px, so a crewed gatling sees 196 px instead of 184), the Unit editor's OpenZ set and its playground (the
    /// same sim, the same parse), the battlefield screen's check of the saved Custom profile (View/OzLevelStats.cs RulesAsLevelStart sets
    /// the switch as the level start will). A Custom level (DV142) plays the rows of its saved profile, and the Unit editor saves every
    /// value a Custom set can change: a profile saved before 1.4 keeps Zod's gatling (15 m, 39 HP a hit), one saved in 1.4 has 16.5 m / 45.
    /// Switch: <see cref="ZWorld.GatlingBuffDeviation"/>, false here (headless worlds, the parity's Zod rows, the Unit editor's ZodEngine
    /// set and every older counted check keep Zod's gatling); View/ZGame.cs turns it on at every start unless 'Zod exact'.
    /// Deterministic: an integer range and one IEEE multiplication of two constants at parse time; no random draw, no clock.
    /// </summary>
    public static class ZGatlingBuff
    {
        /// <summary>The one unit type this rule changes.</summary>
        public const string Type = "gatling";
        /// <summary>Range factor in tenths (11 = x 1.10), integer px so the range stays a whole number of px.</summary>
        public const int RangeTenths = 11;
        /// <summary>Damage per shot factor on the row's attack_damage.</summary>
        public const double DamageFactor = 1.15;

        /// <summary>The gatling's range under DV170 from its row's range (px): x 1.10 rounded to the nearest px (Zod's 120 -> 132).</summary>
        public static int Radius(int rowRadius) => (rowRadius * RangeTenths + 5) / 10;

        /// <summary>The gatling's attack_damage under DV170 from its row's own value: x 1.15 (Zod's 0.003976 -> 0.0045724, 39 -> 45 HP a hit).</summary>
        public static double DamagePerShot(double rowDamage) => rowDamage * DamageFactor;

        /// <summary>ZSettings.Parse, after the rows are read: the gatling's range and damage per shot while the switch is on. True when applied.</summary>
        public static bool Apply(ZSettings s)
        {
            if (!ZWorld.GatlingBuffDeviation || s == null) return false;
            var u = s.Unit(Type);
            if (u == null) return false;
            u.attack_radius = Radius(u.attack_radius);
            u.attack_damage = DamagePerShot(u.attack_damage);
            Applied++;
            return true;
        }

        /// <summary>Statistics (the counted check): parses that gave the gatling the DV170 numbers.</summary>
        public static int Applied;

        /// <summary>A settings text parsed as Zod has it whatever a Play session left: DV147's mech and this rule's gatling held off
        /// (the Unit editor's ZodEngine set, View/OzUeSetData.cs).</summary>
        public static ZSettings ParseZod(string text)
        {
            using (ZWorld.GatlingBuffOff()) return ZMechHp.ParseZod(text);
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV170 the gatling reaches 10 % farther and hits 15 % harder per shot (Sim/ZGatlingBuff.cs). View/ZGame.cs sets it at every
        /// start (off in 'Zod exact'); off by default so headless worlds, the parity's Zod rows and the tests keep Zod's gatling.</summary>
        public static bool GatlingBuffDeviation = false;

        /// <summary>The parity's Zod rows and probes: Zod's gatling while this lives (a Play session leaves the switch on).</summary>
        public static IDisposable GatlingBuffOff() => new GatlingBuffHold();
        sealed class GatlingBuffHold : IDisposable
        {
            readonly bool was = GatlingBuffDeviation;
            public GatlingBuffHold() { GatlingBuffDeviation = false; }
            public void Dispose() { GatlingBuffDeviation = was; }
        }
    }
}
