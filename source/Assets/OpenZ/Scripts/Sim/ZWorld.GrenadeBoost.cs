using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// DV104, version 1.1: a THROWN grenade is judged by where it lands.
    ///   blast radius   grenade_damage_radius x 120 % (30 px -> 36 px = 4.5 m), for every target of the blast
    ///   direct hit     the grenade lands ON the target's own body (its box: a robot 16 x 16 px, a vehicle or gun 32 x 32 px, a
    ///                  building its footprint): grenade_damage x MAX_UNIT_HEALTH x 115 % (1666 -> 1916) at the centre of the blast,
    ///                  with Zod's straight-line fall-off to the 36 px radius (a grenade on the edge of a tank's hull is not a
    ///                  grenade on its middle)
    ///   area hit       the target is only caught in the blast: Zod's 1666 less one tenth (1499) at the centre, the same fall-off,
    ///                  nothing at the 36 px radius
    /// Thrown = the two places a robot lets go of a grenade: a squad member (ZObject.cs ProcessAttackDamage) and the fort's roof
    /// defender (ZObject.FortDefender.cs FdLob); both mark the missile (ZMissile.grenade). Nothing else is touched: the throw range,
    /// the +-24 px scatter, the 40 px/s flight, the 2.254 s between throws, 20 a box, "never the thrower's own side", and the step
    /// aside of the unit that is thrown at are Zod's. The blasts of a destroyed grenade crate (ZWorld.cs DeathDebris: 1666, 40 px),
    /// of a rock and of every shell and rocket keep their numbers and Zod's one rule for every target.
    /// Deterministic: whole numbers only, no random draw, no clock. Switch: ZWorld.GrenadeBoostDeviation (the game sets it at
    /// every start: on, off in 'Zod exact' and with oz_grenade_boost = 0; headless default off, so every parity row and every
    /// other check runs with Zod's grenade). With the switch off every number and the blast line are Zod's own, tick for tick.
    /// its two numbers are taken x GrenadeStrongerPct / 100 more (115: a direct hit 1916 -> 2203, an area hit 1499 -> 1724 at the
    /// centre of the blast, each rounded half up); the 36 px radius, the fall-off, the throw, the flight, the scatter, the time between
    /// throws and 20 a box stay as they were. It acts only while DV104 is on, so 'Zod exact' and oz_grenade_boost = 0 keep Zod's
    /// OpenZ/V1.3/Grenades stronger: counted (headless, DV160), Editor/OzV13GrenadeCount.cs).
    /// </summary>
    public sealed partial class ZWorld
    {
        public static bool GrenadeBoostDeviation = false;
        /// <summary>Per cent of Zod's grenade blast radius, of Zod's grenade damage on a direct hit and on an area hit while the rule
        /// is on (each rounded to a whole number).</summary>
        public const int GrenadeBoostRadiusPct = 120, GrenadeBoostDamagePct = 115, GrenadeAreaDamagePct = 90;

        /// <summary>DV160 (1.3 item 32): the shipped per cent on top of DV104's direct and area hit (100 = the 1.2 grenade).</summary>
        public const int ShippedGrenadeStrongerPct = 115;
        /// <summary>DV160: the per cent in use. Static so the counted check can measure the 1.2 grenade (100) beside the shipped step;
        /// the check snapshot (Editor/OzV11Kit.GameSwitches) puts it back. Nothing in the game changes it.</summary>
        public static int GrenadeStrongerPct = ShippedGrenadeStrongerPct;

        /// <summary>DV160: hp x GrenadeStrongerPct / 100, rounded half up (whole numbers only).</summary>
        public static int GrenadeStronger(int hp) => GrenadeStrongerPct == 100 ? hp : (hp * GrenadeStrongerPct + 50) / 100;

        /// <summary>Zod's grenade damage at the centre of the blast: grenade_damage x MAX_UNIT_HEALTH (1666).</summary>
        public int ZodGrenadeDamage => (int)(S.grenade_damage * ZConst.MAX_UNIT_HEALTH);

        /// <summary>The damage a thrown grenade carries (a direct hit at the centre of its blast; Zod: grenade_damage x MAX_UNIT_HEALTH).
        /// DV104 x DV160 while the rule is on (1666 -> 1916 -> 2203).</summary>
        public int ThrownGrenadeDamage => GrenadeBoostDeviation ? GrenadeStronger((ZodGrenadeDamage * GrenadeBoostDamagePct + 50) / 100) : ZodGrenadeDamage;

        /// <summary>An area hit's damage at the centre of the blast: Zod's less one tenth (1499), then DV160 (1724); Zod's own with the
        /// rule off.</summary>
        public int ThrownGrenadeAreaDamage => GrenadeBoostDeviation ? GrenadeStronger((ZodGrenadeDamage * GrenadeAreaDamagePct + 50) / 100) : ZodGrenadeDamage;

        /// <summary>The blast radius of a thrown grenade in px (Zod: grenade_damage_radius).</summary>
        public int ThrownGrenadeRadius => GrenadeBoostDeviation ? (S.grenade_damage_radius * GrenadeBoostRadiusPct + 50) / 100 : S.grenade_damage_radius;

        /// <summary>A direct hit: the blast point lies on the object's own body (its box, ZObject.Contains).</summary>
        public static bool GrenadeDirectHit(int bx, int by, ZObject o) => o.Contains(bx, by);

        /// <summary>The HP one blast takes from o, ratio = its distance / the radius (ProcessMissileDamage). Zod's line
        /// (damage x (1 - ratio)) for every blast that is not a thrown grenade and for every blast while the rule is off.</summary>
        internal int BlastHit(ZMissile m, ZObject o, float ratio)
        {
            if (!GrenadeBoostDeviation || !m.grenade) return (int)(m.damage * (1.0 - ratio));
            if (m.areaDamage > 0) return GrenadeHit(m.damage, m.areaDamage, m.x, m.y, o, ratio);   // DV112 (item 257): a hero's grenade carries its own area hit (Sim/ZHeroSquad.Kh4.cs)
            return GrenadeHit(m.damage, ThrownGrenadeAreaDamage, m.x, m.y, o, ratio);
        }

        /// <summary>The rule for one thrown grenade and one target (also used by the counted check): direct hit = the grenade's own
        /// damage, area hit = areaDamage, each with Zod's fall-off.</summary>
        public static int GrenadeHit(int damage, int areaDamage, int bx, int by, ZObject o, float ratio) =>
            (int)((GrenadeDirectHit(bx, by, o) ? damage : areaDamage) * (1.0 - ratio));

        /// <summary>Turns the rule off until it is disposed, then puts back what was there. The parity report holds one while it
        /// measures (a Play session leaves the switch on); its own row turns the rule on by itself.</summary>
        public sealed class GrenadeBoostHold : IDisposable
        {
            readonly bool was;
            internal GrenadeBoostHold() { was = GrenadeBoostDeviation; GrenadeBoostDeviation = false; }
            public void Dispose() { GrenadeBoostDeviation = was; }
        }
        public static GrenadeBoostHold GrenadeBoostOff() => new GrenadeBoostHold();
    }
}
