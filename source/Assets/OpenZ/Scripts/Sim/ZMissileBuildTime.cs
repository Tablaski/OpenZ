using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// "also Missile turrets production time should be 40% longer than howitzers build time"). Only the unit type "missile_cannon" (the
    /// MISSILE CANNON of the CANNONS row, built by a fort, a robot factory or a vehicle factory). Zod's rows in default_settings.txt stay
    /// Zod's (unit.howitzer.build_time=179, unit.missile_cannon.build_time=182; zsettings.cpp) and while the switch is on every parse of the
    /// game's settings text gives the missile cannon the base build time <see cref="BuildTime"/>(the howitzer's row) = (179 x 14 + 5) / 10 =
    /// 251 s (4:11; exactly 250.6 s, rounded to the nearest whole second like every build_time row: the field is an int, Zod's atoi).
    /// One place, ZSettings.Parse (after DV170's gatling line), so every reader of the row sees the same number: the factory's countdown
    /// (ZObject.RecalcBuildTime: BFinal = BInit + BuildTimeModified(row)) with every modifier on top exactly as before (Zod's flag share and
    /// building damage, zbuilding.cpp:633-667; DV175 MAYHEM: the computer's side x 0.9; every recalculation on a hit or a flag change), the
    /// production window (its time, its tiles and its "BASE" line read the row through the same formula: 4:11 BASE), the computer's own
    /// waiting estimates (ZBot.StrikeGroup), the Unit editor's OpenZ set and the battlefield screen's check of a Custom profile
    /// (View/OzLevelStats.cs RulesAsLevelStart sets the switch as the level start will). A Custom level (DV142) writes its saved values over
    /// the parsed rows: a build time the player set there wins.
    /// No other unit changes: the howitzer keeps 179 s, every other row and field is the text's.
    /// Switch: <see cref="ZWorld.MissileTurretBuildTimeDeviation"/>, false here (headless worlds, the parity's Zod rows, the Unit editor's
    /// ZodEngine set and every older counted check keep Zod's 182 s); View/ZGame.cs turns it on at every start unless 'Zod exact'.
    /// Off: Apply returns at its first line, not one field changes. Deterministic: integer maths once at parse time; no random draw, no clock.
    /// Checks: Editor/OzV15MissileBuildTimeCount.cs (menu OpenZ/V1.5/Missile Build Time: Count (headless, DV191)); the Deviations row DV191.
    /// </summary>
    public static class ZMissileBuildTime
    {
        /// <summary>The one unit type this rule changes, and the unit whose build time it is measured against.</summary>
        public const string Type = "missile_cannon", Base = "howitzer";
        /// <summary>The factor in tenths (14 = x 1.4), so the result stays a whole number of seconds.</summary>
        public const int Tenths = 14;

        /// <summary>The missile cannon's base build time under DV191 from the howitzer's row (s): x 1.4 to the nearest whole second, halves
        /// up (Zod's 179 -> 251).</summary>
        public static int BuildTime(int howitzerSeconds) => (howitzerSeconds * Tenths + 5) / 10;

        /// <summary>ZSettings.Parse, after the rows are read: the missile cannon's build time while the switch is on. A text without a howitzer
        /// build time (0) leaves the missile cannon's own row as it is. True when applied.</summary>
        public static bool Apply(ZSettings s)
        {
            if (!ZWorld.MissileTurretBuildTimeDeviation || s == null) return false;
            var m = s.Unit(Type); var h = s.Unit(Base);
            if (m == null || h == null || h.build_time <= 0) return false;
            m.build_time = BuildTime(h.build_time);
            Applied++;
            return true;
        }

        /// <summary>Statistics (the counted check): parses that gave the missile cannon the DV191 build time.</summary>
        public static int Applied;

        /// <summary>A settings text parsed as Zod has it whatever a Play session left: DV147's mech, DV170's gatling and this rule's missile
        /// cannon held off (the Unit editor's ZodEngine set, View/OzUeSetData.cs).</summary>
        public static ZSettings ParseZod(string text)
        {
            using (ZWorld.MissileTurretBuildTimeOff()) return ZGatlingBuff.ParseZod(text);
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV191 the missile cannon builds in the howitzer's time x 1.4 (Sim/ZMissileBuildTime.cs). View/ZGame.cs sets it at every
        /// start (off in 'Zod exact'), before ZWorld.Create parses the settings; off by default so headless worlds, the parity's Zod rows and
        /// the tests keep Zod's 182 s.</summary>
        public static bool MissileTurretBuildTimeDeviation = false;

        /// <summary>The parity's Zod rows and probes: Zod's missile cannon build time while this lives (a Play session leaves the switch on).</summary>
        public static IDisposable MissileTurretBuildTimeOff() => new MissileTurretBuildTimeHold();
        sealed class MissileTurretBuildTimeHold : IDisposable
        {
            readonly bool was = MissileTurretBuildTimeDeviation;
            public MissileTurretBuildTimeHold() { MissileTurretBuildTimeDeviation = false; }
            public void Dispose() { MissileTurretBuildTimeDeviation = was; }
        }
    }
}
