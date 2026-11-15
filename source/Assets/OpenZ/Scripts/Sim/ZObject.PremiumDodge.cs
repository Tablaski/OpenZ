namespace OpenZ.Sim
{
    /// <summary>
    /// Zod: every unit steps aside from a shell or rocket fired at it (DodgeMissile, zobject.cpp:3657-3714) at its own speed, whoever drives;
    /// the step's length is the time till the blast x that speed (+ up to half again, a random draw).
    /// OpenZ, while <see cref="ZWorld.PremiumDodgeDeviation"/> is on (View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off):
    /// <list type="bullet">
    /// <item>Who: the light tank, the medium tank, the heavy tank and the mech (<see cref="IsHatchVehicle"/>, the four of DV171) of a team, while
    /// their driver is NOT a grunt (<see cref="DriverType"/>: a psycho, sniper, tough, pyro, laser or Electrobot put in, e.g. with DV171's EXIT
    /// DRIVER and a new ENTER; a hero squad's Alan and Brad are grunts). Every other vehicle, every grunt-driven tank: Zod's dodge as before.</item>
    /// <item>What: only while its first waypoint is Zod's step aside (WpMode.Dodge): its speed is x <see cref="ZWorld.PremiumDodgeSpeedMul"/>
    /// (ProcessMoveCore, after every other speed factor; the velocity re-scaled the way a tile change re-scales it), and the step aside is planned
    /// with that speed (DodgeMissile: the same rule, time till the blast x the dodge speed, the same random draws in the same order). When the
    /// step aside ends (reached, replaced by an order, blocked) the speed is the plain one again before the next waypoint starts
    /// (<see cref="PremiumDodgeSettle"/>, the exact value it had, no division).</item>
    /// <item>The quicker turn while it dodges is drawn by the view only (View/HullSteer.cs: the hull's ease and turn cap x 1.15): the sim has no
    /// hull turn, a step aside starts in its new direction at once, as in Zod.</item>
    /// </list>
    /// Doubles only (no float: the il2cpp_determinism scan stays at 0), no random draw of its own, no Unity, no clock. The computer's tanks get it
    /// too when a non-grunt robot of the computer's climbs into one (a neutral tank taken by a tough, say).
    /// </summary>
    public partial class ZObject
    {
        /// <summary>DV173: RealMoveSpeed holds the premium dodge factor now (set with it in <see cref="PremiumDodgePace"/>); pdBase = the speed
        /// without it, put back by <see cref="PremiumDodgeSettle"/>.</summary>
        bool pdBoosted; double pdBase;

        /// <summary>DV173, for the checks (no rule reads them): the factor is in RealMoveSpeed now; the plain speed it was put on.</summary>
        public bool PremiumDodgeBoosted => pdBoosted;
        public double PremiumDodgeBaseSpeed => pdBase;

        /// <summary>DV173: a tank or the mech of a team with a non-grunt driver (the rule's switch on).</summary>
        public bool PremiumDriven => ZWorld.PremiumDodgeDeviation && IsHatchVehicle && Owner != Team.Neutral && Drivers.Count > 0 && DriverType != "grunt";

        /// <summary>DV173: a premium-driven tank / mech in Zod's step aside right now (the view's quicker hull turn reads it too).</summary>
        public bool PremiumDodgeNow => PremiumDriven && Waypoints.Count > 0 && Waypoints[0].mode == WpMode.Dodge;

        /// <summary>ProcessMoveCore, right after every other factor of RealMoveSpeed: the dodge factor while a premium step aside runs.</summary>
        void PremiumDodgePace()
        {
            if (Behavior != null) { pdBoosted = false; return; }
            if (!PremiumDodgeNow) { pdBoosted = false; return; }
            pdBase = RealMoveSpeed;
            RealMoveSpeed *= ZWorld.PremiumDodgeSpeedMul;
            pdBoosted = true;
            World.PremiumDodgeTicks++;
        }

        /// <summary>Every tick before the waypoint step (only while the factor is in RealMoveSpeed): the step aside is over (or the tank is no
        /// longer premium-driven): the plain speed back, exactly, and the velocity with it (as ProcessMoveCore re-scales it).</summary>
        void PremiumDodgeSettle()
        {
            if (PremiumDodgeNow) return;
            double prev = RealMoveSpeed;
            RealMoveSpeed = pdBase; pdBoosted = false;
            if ((!IsZ(Dx) || !IsZ(Dy)) && prev > 0)
            {
                Dx = (float)(Dx / prev); Dy = (float)(Dy / prev);
                Dx = (float)(Dx * RealMoveSpeed); Dy = (float)(Dy * RealMoveSpeed);
                UpdatedVelocity = true;
            }
            World.PremiumDodgeSettles++;
        }

        /// <summary>DodgeMissile's speed for the step's length: RealMoveSpeed; for a premium-driven tank / mech its dodge speed (the factor once,
        /// also while a step aside already runs).</summary>
        double DodgePlanSpeed
        {
            get
            {
                // review 6 Nov: no longer premium-driven (driver sniped / out / a grunt in) while the factor is still in RealMoveSpeed (its
                // PremiumDodgeSettle comes on its own next tick): the plain speed, as Zod. A unit that never had the factor: RealMoveSpeed as before.
                if (!PremiumDriven) return pdBoosted ? pdBase : RealMoveSpeed;
                World.PremiumDodgePlans++;
                return pdBoosted ? RealMoveSpeed : RealMoveSpeed * ZWorld.PremiumDodgeSpeedMul;
            }
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV173 PREMIUM DRIVER DODGE (Sim/ZObject.PremiumDodge.cs). View/ZGame.cs: on at every start, off in 'Zod exact'; headless
        /// default off (every Zod row, every older counted check and the bot tools as before).</summary>
        public static bool PremiumDodgeDeviation = false;
        /// <summary>DV173: a premium-driven tank's / mech's speed while it steps aside, and the speed its step is planned with (x 1.08, the low end
        /// of the owner's 8-10 %: chosen in the kit premium_driver_dodge with tools/dodge_model.py and the counted check's sweep; at most 0.29 m
        /// more step per shell; the damage of a full-range howitzer shell 7-18 % less, of every other shell 0-6 % less).</summary>
        public static double PremiumDodgeSpeedMul = 1.08;
        /// <summary>DV173 statistics (checks; no rule reads them): ticks moved with the factor, plans made with it, factors taken off.</summary>
        public int PremiumDodgeTicks, PremiumDodgePlans, PremiumDodgeSettles;
    }
}
