using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ deviation DV149 "the mech loses its hand gun" (version 1.2, on top of DV146 the hand gun, ZObject.MechHandGun.cs). A badly
    /// damaged mech can have the machine gun in its right hand knocked off by a heavy hit, for good:
    /// <list type="bullet">
    /// <item>when: a BLAST (every missile of the rules: tank, gun and howitzer shells, rockets, guided missiles, mortar rounds and thrown
    /// grenades; ZWorld.ProcessMissileDamage) takes at least <see cref="ZWorld.GunLossHeavyHp"/> off the mech (a HEAVY hit: a quarter of
    /// the light tank's shell, <see cref="ZWorld.GunLossHeavyShare"/>, 521 HP with the default rules) and leaves it alive with less than
    /// <see cref="ZWorld.GunLossHpShare"/> of its full health (under 20 % left). Small arms are never heavy: rifles, machine guns, the
    /// sniper, flames, the laser, zaps and burning ground hurt it without a draw.</item>
    /// <item>then: one draw on the world's Zod rand() stream; under <see cref="ZWorld.GunLossChance"/> (50 %) the gun is lost. One draw
    /// per such hit; none once the gun is gone, none for any other unit or hit, none with the switch off.</item>
    /// <item>after: the hand gun never fires again (<see cref="HasHandGun"/> is false from then on: no target, no aim, no shot; the
    /// computer's mechs alike, they fire it by themselves). Nothing gives it back: repair on a station's pad (the same unit), Zod's repair
    /// swap (the fresh vehicle is given the loss: <see cref="GunLossStore"/> / <see cref="GunLossRestore"/>), capture, a new crew. The
    /// rockets fire as before.</item>
    /// </list>
    /// Deterministic: sim health and the world's rand() stream only, no frame time, no view state. Switch: ZWorld.MechGunLossDeviation (the
    /// game sets it with DV146, so it is off in 'Zod exact'; headless default off); without DV146 there is no hand gun to lose.
    /// View: View/MechGunLossView.cs (the gun's forearm falls off with physics, the arm stays without it).
    /// </summary>
    public partial class ZObject
    {
        bool repairGunLost; double repairGunLostAt = -100; int repairGunFromX, repairGunFromY;   // station side of Zod's repair swap

        /// <summary>DV149: this mech's hand gun was knocked off (for good).</summary>
        public bool HandGunLost { get; private set; }
        /// <summary>DV149: sim time of the loss (-100 = never; the fresh vehicle of Zod's repair swap keeps the old time) and the map point
        /// of the blast that did it.</summary>
        public double HandGunLostAt { get; private set; } = -100;
        public int HandGunLostFromX { get; private set; }
        public int HandGunLostFromY { get; private set; }
        /// <summary>DV149 counter (checks): the draws made for this unit (heavy blasts that left it under the health line).</summary>
        public int GunLossRolls { get; private set; }

        /// <summary>ZWorld.ProcessMissileDamage, right after the blast m hit this object (hp0 = its health before the hit).</summary>
        internal void GunLossBlast(int hp0, ZMissile m)
        {
            if (!ZWorld.MechGunLossDeviation || !HasHandGun || World == null || m == null) return;   // HasHandGun: the mech, DV146 on, the gun still there
            if (Destroyed || KillAt >= 0 || MaxHealth <= 0) return;                                  // a killed mech falls apart as a whole
            if (hp0 - Health < World.GunLossHeavyHp) return;                                         // not a heavy hit
            if (Health >= MaxHealth * ZWorld.GunLossHpShare) return;                                 // still 20 % or more left
            GunLossRolls++; if (ZWorld.GunLossRollsAll < int.MaxValue) ZWorld.GunLossRollsAll++;
            if (World.Rng.Roll() >= ZWorld.GunLossChance) return;                                    // Zod's rand() stream, one draw (Roll: 0 .. 0.9999)
            HandGunLost = true; HandGunLostAt = World.Time; HandGunLostFromX = m.x; HandGunLostFromY = m.y;
            HandGunDropAim();
            if (ZWorld.GunLossLostAll < int.MaxValue) ZWorld.GunLossLostAll++;
        }

        /// <summary>The hand gun's target and arm let go (nothing aims or waits for a gun that is gone).</summary>
        void HandGunDropAim() { hgTarget = null; hgAimFor = null; hgShotAt = null; hgOnSince = -1; hgArmHdg = double.NaN; }

        /// <summary>SetRepairUnit (on the station, Zod's repair swap): the loss rides along with the vehicle's crew.</summary>
        internal void GunLossStore(ZObject unit)
        {
            repairGunLost = unit != null && unit.HandGunLost;
            repairGunLostAt = repairGunLost ? unit.HandGunLostAt : -100; repairGunFromX = repairGunLost ? unit.HandGunLostFromX : 0; repairGunFromY = repairGunLost ? unit.HandGunLostFromY : 0;
        }

        /// <summary>BuildingRepairUnit (on the station): the fresh vehicle has no hand gun when the one that drove in had lost it.</summary>
        internal void GunLossRestore(ZObject u)
        {
            if (!repairGunLost || u == null) return;
            u.HandGunLost = true; u.HandGunLostAt = repairGunLostAt; u.HandGunLostFromX = repairGunFromX; u.HandGunLostFromY = repairGunFromY;
            u.HandGunDropAim();
            repairGunLost = false;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV149 the mech loses its hand gun (ZObject.MechGunLoss.cs). The game sets it with DV146 (off in 'Zod exact'); headless default off.</summary>
        public static bool MechGunLossDeviation = false;
        /// <summary>DV149: the health line (share of full health left after the hit: under it a heavy hit may knock the gun off), the chance
        /// per such hit, and the share of the reference shell's damage that makes a blast's hit heavy.</summary>
        public static double GunLossHpShare = 0.20, GunLossChance = 0.5, GunLossHeavyShare = 0.25;
        /// <summary>DV149: the unit whose shot is the reference heavy hit (the light tank's shell) and the HP used when the rules have no such row.</summary>
        public const string GunLossHeavyRef = "light";
        public const int GunLossHeavyFallback = 521;
        /// <summary>DV149 statistics (checks, all worlds): draws made, guns lost.</summary>
        public static int GunLossRollsAll, GunLossLostAll;

        /// <summary>DV149: the least HP a blast must take off the mech to be a heavy hit (this world's rules: the light tank's shell damage x
        /// <see cref="GunLossHeavyShare"/>, rounded; 2083 x 0.25 = 521 with the default rules).</summary>
        public int GunLossHeavyHp
        {
            get
            {
                var u = S == null ? null : S.Unit(GunLossHeavyRef);
                int d = u == null ? 0 : u.Damage;
                return d > 0 ? Math.Max(1, (int)Math.Round(d * GunLossHeavyShare)) : GunLossHeavyFallback;
            }
        }

        /// <summary>DV149: the rule off for a block of code (the parity report's Zod rows and other probes; a Play session leaves the switch on).</summary>
        public static IDisposable GunLossOff() => new GunLossOffHold();
        sealed class GunLossOffHold : IDisposable
        {
            readonly bool was = MechGunLossDeviation;
            public GunLossOffHold() { MechGunLossDeviation = false; }
            public void Dispose() { MechGunLossDeviation = was; }
        }
    }
}
