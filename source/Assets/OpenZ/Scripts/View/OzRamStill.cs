using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// VIEW side of DV195 (Sim/ZObject.RamChain.cs). From the contact of a Bizon's crush or a Mammoth's ram until its death is
    /// drawn, the doomed vehicle or gun is drawn standing still:
    /// <list type="bullet">
    /// <item>its drawn hull stays exactly where it is drawn (UnitView.NoCrab: VehicleSlip.Hold, as DV79's unmanned stop): no catch-up drive onto
    /// its rules spot, no pivot, no K-turn of a wheeled crane / APC / jeep, no turn; its wheels and tracks stop (the drive speed the wheels roll
    /// with is 0);</item>
    /// <item>the view spacing never pushes it (UnitView.NoCrab PairWeight 0: the others make room);</item>
    /// <item>its turret and gun stay locked on the hull (UnitAim.RamLock, now for a crushed ground gun too);</item>
    /// <item>a crane's arm stands as it is (CraneArmRig: no slew, luff, hook or weld while it is doomed; the rules dropped its job at the contact).</item>
    /// </list>
    /// The Mammoth's flip (OzMammothRamView / OzRamLever / OzRamFlipPhysics) and the Bizon's press (OzCrushRide / OzCrushSquash) still place the
    /// hull as before: they own it while they run. View only: the rules are never written. Switch <see cref="On"/>; it also follows the rules'
    /// switch ZWorld.RamChainDeviation (off in 'Zod exact': the old picture).
    /// </summary>
    public static class OzRamStill
    {
        public static bool On = true;
        /// <summary>Proof (Play check): frames a crane's arm stood held.</summary>
        public static int CraneArmFrames;

        /// <summary>o is rammed / crushed (doomed by the rules, not destroyed yet): drawn standing still.</summary>
        public static bool Holds(ZObject o) => On && ZWorld.RamChainDeviation && o != null && o.RamDoomBy >= 0 && !o.Destroyed;

        public static string Status() => $"OzRamStill: on {On} (rules DV195 {ZWorld.RamChainDeviation}), crane arm frames held {CraneArmFrames}";
    }
}
