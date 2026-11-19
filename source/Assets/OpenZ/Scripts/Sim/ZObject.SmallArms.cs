using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// Zod: a bullet that hits removes the shooter's attack_damage whatever it hits (zobject.cpp:2187-2199): grunt 11, psycho 26, jeep gun
    /// 27, gatling 39 (OpenZ DV170: 45). OpenZ DV212 part 1 (SmallArmsDeviation): a hit of a grunt, a psycho, the jeep's gun or the
    /// gatling gun on a ROBOT ON FOOT removes the shooter's own vs-robot value (VsRobotHit, or the pair in SmallArmsPair) when that is higher
    /// than what it removes today (DV78's pair or Zod's damage). Only the HP of a body hit on a robot changes: the hit roll, the snipe roll,
    /// a driver hit, a hull hit, a building (bullets never target one), the fire interval, the range, grenades and every other shooter stay
    /// as they are. Fort roof defenders (DV41) and Evil Zod (DV161) are left out on both sides. Pure sim, integers, no extra rand() draw.
    /// DV212 part 2 (SmallArmsZ95Deviation, OFF and in no menu yet: an offer to the owner, measured only): Z95's "bullets barely scratch a
    /// hull, the crew roll matters": a hull hit of the psycho, the jeep's gun and the gatling a quarter (the grunt's rifle on a hull keeps
    /// Zod's 11: one grunt already takes the original's time on a jeep, about 164 s); a crew roll of at least Z95's 60/128 for the four;
    /// a crew hit of at least the shooter's hit on a grunt (a driver is a grunt: the grunt's 155 = Z95's 7 crew hits).
    /// </summary>
    public partial class ZObject
    {
        /// <summary>A grunt or psycho on foot, the jeep (its gun) or the gatling gun: the four small arms of DV212.</summary>
        internal bool IsSmallArm => ZWorld.IsSmallArmType(Kind, Type);

        /// <summary>DV212: the HP a body hit of this shooter removes from t, given d = today's (DV78 pair or HullDamage).</summary>
        int SmallArmsHit(ZObject t, int d)
        {
            if (!IsSmallArm || IsFortDefender) return d;
            if (ZWorld.SmallArmsDeviation && t.IsRobot && !t.IsFortDefender && t.Type != ZEvilZod.Type)
            {
                int v = ZWorld.SmallArmsRobotHit(Type, t.Type);
                return v > d ? v : d;                                                     // never lower than today
            }
            if (ZWorld.SmallArmsZ95Deviation && (t.Kind == ZKind.Vehicle || t.Kind == ZKind.Cannon) && ZWorld.SmallArmsHullQuarter(Type))
                return Math.Max(1, (d + ZWorld.SmallArmsHullDivisor / 2) / ZWorld.SmallArmsHullDivisor);   // part 2: a quarter, rounded, at least 1
            return d;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV212 part 1 switch. Static like RiflePairDeviation: ZGame sets it at every start (on; off in 'Zod exact'); off by
        /// default so every headless world (parity rows, tests) keeps today's damage.</summary>
        public static bool SmallArmsDeviation = false;

        /// <summary>DV212 part 2 switch (the owner's Z idea, an offer): OFF by default; View/ZGame.cs sets it OFF at every start (the one
        /// </summary>
        public static bool SmallArmsZ95Deviation = false;

        /// <summary>Holds both DV212 switches for a block (the parity report's Zod rows and probes, checks), then puts them back.</summary>
        public static IDisposable SmallArmsOff() => new SmallArmsHold(false, false);
        public static IDisposable SmallArmsOn() => new SmallArmsHold(true, false);
        sealed class SmallArmsHold : IDisposable
        {
            readonly bool was = SmallArmsDeviation, wasZ95 = SmallArmsZ95Deviation;
            public SmallArmsHold(bool on, bool z95) { SmallArmsDeviation = on; SmallArmsZ95Deviation = z95; }
            public void Dispose() { SmallArmsDeviation = was; SmallArmsZ95Deviation = wasZ95; }
        }

        /// <summary>DV212 for the Unit editor's stats (View/OzUeLiveStats.cs VsGrunt): the HP one hit of this unit type removes from a
        /// grunt with DV212 on, given today's value d (the DV78 pair or the damage). Any other type: d.</summary>
        public static int SmallArmsVsGrunt(string type, int d)
        {
            if (type != "grunt" && type != "psycho" && type != "jeep" && type != "gatling") return d;
            int v = SmallArmsRobotHit(type, "grunt");
            return v > d ? v : d;
        }

        /// <summary>DV212: HP one body hit of the shooter type removes from any robot on foot (when higher than today's). Grunt 100 = the
        /// 1.2 sniper balance's grunt>sniper value for every robot (Zod 11: a grunt needed 115 s on a psycho, 223 s on a Tough); psycho 40
        /// (Zod 26, x1.54); the jeep's gun 45 (Zod 27, x1.67); the gatling 75 (DV170 45, x1.67). Set on the counted duels of kit
        /// small_arms_balance_r8 (proof/after_part1.txt; try 1 had the jeep 54 and the gatling 90: too strong against squads).</summary>
        public static readonly Dictionary<string, int> VsRobotHit = new Dictionary<string, int>
        {
            ["grunt"] = 100,
            ["psycho"] = 40,
            ["jeep"] = 45,
            ["gatling"] = 75,
        };

        /// <summary>DV212: shooter / target pairs that take the place of VsRobotHit. grunt>grunt 155 = 7 hits fell a grunt, the original
        /// Z's 7 (5 of 30 HP; DV78 125 = 9 hits, Zod 11 = 99).</summary>
        public static readonly Dictionary<(string attacker, string target), int> SmallArmsPair = new Dictionary<(string attacker, string target), int>
        {
            [("grunt", "grunt")] = 155,
        };

        /// <summary>DV212 part 2: hull hit = today's / SmallArmsHullDivisor (rounded, at least 1) for these shooters.</summary>
        public const int SmallArmsHullDivisor = 4;
        /// <summary>DV212 part 2: Z95's crew roll per confirmed hit of a grunt on an occupied jeep (the owner's research: 60/128).</summary>
        public const double SmallArmsZ95CrewRoll = 60.0 / 128.0;

        public static bool IsSmallArmType(ZKind kind, string type) =>
            kind == ZKind.Robot ? (type == "grunt" || type == "psycho") : kind == ZKind.Vehicle ? type == "jeep" : kind == ZKind.Cannon && type == "gatling";

        internal static bool SmallArmsHullQuarter(string type) => type == "psycho" || type == "jeep" || type == "gatling";

        /// <summary>DV212: the vs-robot hit of attacker type on target type (0 = none listed).</summary>
        public static int SmallArmsRobotHit(string attacker, string target)
        {
            if (SmallArmsPair.TryGetValue((attacker, target), out int p)) return p;
            return VsRobotHit.TryGetValue(attacker, out int v) ? v : 0;
        }

        /// <summary>DV212 part 2: the crew roll of a small arm at least Z95's 60/128 (off: sc as it is).</summary>
        internal static double SmallArmsCrewRoll(ZObject attacker, double sc) =>
            SmallArmsZ95Deviation && attacker != null && attacker.IsSmallArm && sc < SmallArmsZ95CrewRoll ? SmallArmsZ95CrewRoll : sc;

        /// <summary>DV212 part 2: a crew hit of a small arm removes at least the shooter's hit on a grunt (off: d as it is).</summary>
        internal static int SmallArmsDriverHit(ZObject attacker, int d)
        {
            if (!SmallArmsZ95Deviation || attacker == null || !attacker.IsSmallArm) return d;
            int v = SmallArmsRobotHit(attacker.Type, "grunt");
            return v > d ? v : d;
        }
    }
}
