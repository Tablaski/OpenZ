using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// <para>Who: a MISSILE CANNON (type "missile_cannon", the CANNONS row's missile battery) standing on one of a fort's four tower spots:
    /// exactly <see cref="IsTowerGun"/> (DV153: the cannons Zod makes not ejectable, ZWorld.MarkFortTurretCannons at the map start,
    /// ZWorld.PlaceCannon for one placed from the fort's store on a tower spot, DV73's door-tower gun on HARD and MAYHEM). Either side, the
    /// player's and the computer's, every level. The Mobile Missile vehicle (type "missile_launcher") is a vehicle and never stands on a fort
    /// (its tiles are impassable), so it is not touched; a missile cannon on the ground is not touched; the fort's other tower guns (gun,
    /// howitzer, gatling) are not touched.</para>
    /// <para>What: its pause between two shots is <see cref="FortTurretReloadFactor"/> (2) times its row's attack_speed: Zod's 1.124 s
    /// (unit.missile_cannon.attack_speed, default_settings.txt) becomes 2.248 s, so on the sim's 10 ms clock it fires every 225 ticks
    /// (2.25 s) instead of every 113 (1.13 s): half the rate of fire. Its first shot at a new target comes as before (the reload starts when
    /// it fires); damage, blast, range (and DV162's high-ground 10 % on top), rocket speed, hit rules, health and crew stay as they are.</para>
    /// <para>Where: one hook in ZObject.ProcessAttackDamage, after Zod's reload line (<c>nextDamageTime = time + AttackSpeed;</c>,
    /// zobject.cpp:2102-2213, the only place a cannon's reload is set) and its two no-shot exits (the target no longer attackable; DV23's
    /// LOS hold), so only a shot that really leaves waits twice: <see cref="FortTurretRofReload"/> adds the row's attack_speed once
    /// more. A reload spent without a shot stays Zod's single one, so the first shot at the next target is never held up. AttackSpeed itself (the row's value) is not changed, so the Unit editor's card, the build menu's card, Custom unit stats (DV142,
    /// a Custom reload is doubled on a tower the same way) and every other reader keep the row; <see cref="ReloadSeconds"/> gives the pause
    /// this object really has (checks, a future card line).</para>
    /// <para>Switch <see cref="ZWorld.FortTurretRofDeviation"/>: false here (headless worlds, the parity's Zod rows, every older counted check
    /// keep Zod's reload); View/ZGame.cs turns it on at every start unless 'Zod exact' (every difficulty, MAYHEM included, bot matches too).
    /// Deterministic: no random draw, no clock, no Unity, no new number type: one more addition of the unit's own reload to the sim's own
    /// clock, exactly as Zod's reload line adds it; object state only.</para>
    /// </summary>
    public partial class ZObject
    {
        /// <summary>DV177: the one cannon type this rule slows when it stands on a fort's tower.</summary>
        public const string FortTurretRofType = "missile_cannon";
        /// <summary>DV177: the pause between two shots of a fort-mounted missile cannon, in whole multiples of its row's attack_speed.</summary>
        public const int FortTurretReloadFactor = 2;

        /// <summary>DV177: this object is a missile cannon on a fort's tower spot and the rule is on.</summary>
        public bool FortTurretRofSlow => ZWorld.FortTurretRofDeviation && IsTowerGun && Type == FortTurretRofType;

        /// <summary>Seconds from one shot of this object to the next: AttackSpeed (its row's attack_speed), x <see cref="FortTurretReloadFactor"/>
        /// for a fort-mounted missile cannon while DV177 is on. Read only (checks, cards); the rule itself is <see cref="FortTurretRofReload"/>.</summary>
        public double ReloadSeconds => FortTurretRofSlow ? AttackSpeed * FortTurretReloadFactor : AttackSpeed;

        /// <summary>DV177 hook in ProcessAttackDamage, after Zod's <c>nextDamageTime = time + AttackSpeed;</c> and its two no-shot exits
        /// (right after <c>if (LosHoldShot(t)) return;</c>, so only on a real shot): a fort-mounted missile
        /// cannon waits its reload (factor - 1) more times. Anything else: returns at once, nothing changes.</summary>
        void FortTurretRofReload()
        {
            if (!FortTurretRofSlow) return;
            for (int k = 1; k < FortTurretReloadFactor; k++) nextDamageTime += AttackSpeed;
            ZWorld.FortTurretRofReloads++;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV177 a missile cannon on a fort's tower reloads twice as long (Sim/ZObject.FortTurretRof.cs). View/ZGame.cs sets it at every
        /// start (off in 'Zod exact'); off by default so headless worlds, the parity's Zod rows and the tests keep Zod's reload.</summary>
        public static bool FortTurretRofDeviation = false;

        /// <summary>Statistics (the counted check; no rule reads it): reloads DV177 made longer.</summary>
        public static int FortTurretRofReloads;

        /// <summary>The parity's Zod rows and probes: Zod's reload for every tower gun while this lives (a Play session leaves the switch on).</summary>
        public static IDisposable FortTurretRofOff() => new FortTurretRofHold();
        sealed class FortTurretRofHold : IDisposable
        {
            readonly bool was = FortTurretRofDeviation;
            public FortTurretRofHold() { FortTurretRofDeviation = false; }
            public void Dispose() { FortTurretRofDeviation = was; }
        }
    }
}
