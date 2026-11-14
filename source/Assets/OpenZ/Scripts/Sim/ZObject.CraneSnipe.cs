using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ deviation DV141 "the crane's driver can be shot out while he is up in the cab hatch, and stays inside while the crane works"
    /// (version 1.2, items 32-34; part of the driver-sniping deviation DC2). The crane's driver came up and fired, and no sniper, jeep or
    /// squad could ever hit him.
    /// <para>Zod: the crane has no weapon and its driver can never be hit (vcrane.cpp sets no can_be_sniped, zobject.cpp:77 leaves it
    /// false; CanBeSniped zobject.cpp:4693-4696). OpenZ DV31 (ZObject.VehiclesNb.cs) brings him up through the cab hatch to fire a
    /// squad's rifle at the units attacking the crane (CraneGunUp: from the moment he pops out until ZWorld.CraneGunLinger after his
    /// last shot), but left him out of the sniping rule: every shot at him went to the 10000 HP hull.</para>
    /// <para>DV141, with ZWorld.CraneSnipeDeviation and DC2 (ZWorld.SnipeDeviation) on:</para>
    /// <list type="bullet">
    /// <item>While the driver is up (CraneGunUp) and the crane does not work, the crane is a vehicle whose driver can be hit
    /// (ZObject.CanBeSniped), like a tank with its lid open: Zod's list of who can snipe (grunt, psycho, sniper, pyro, laser, jeep,
    /// gatling), Zod's hit roll and snipe roll with each attacker's own chance (zobject.cpp:2187-2199) and every OpenZ factor on that
    /// roll (DV27 from behind, DV12 a veteran crew, DV121), the driver's own health as the pool (1081 HP for a grunt). With the driver
    /// inside, or resting in the hatch (DV75: his rest ends with the first hit), the crane cannot be sniped, as before.</item>
    /// <item>One driver hit on the crane removes at least <see cref="ZWorld.CraneDriverHitFloor"/> HP (the jeep's open-seat floor of
    /// DC2, ZWorld.DriverHitFloor; a sniper's hit x ZWorld.SniperDriverFactor as on every vehicle).</item>
    /// <item>The emptied crane is a sniped vehicle like every other (zserver.cpp:1808-1874): neutral, stopped, its orders gone, any
    /// team can drive it.</item>
    /// <item>While the crane works (<see cref="CraneWorking"/>: a crane job has begun and has not ended: the rebuild of a destroyed
    /// bridge, radar, repair station or factory, the DV61 mend of a damaged standing building) its driver does not come up to fire,
    /// drops back in at once when he is up as the job begins, and the crane cannot be sniped: no crane is ever emptied in the middle
    /// of a job, and the job goes on under fire.</item>
    /// <item>On a spot where DV82 (ZWorld.SnipeSafe.cs, with its own switch) turns every driver hit into a hull hit (a standing bridge
    /// and 88 px before each end, the exit lane of a fort or factory) the crane's driver stays inside and does not fire, and drops
    /// back in at once when the crane rolls onto one: the tanks' lids do the same there (DV87 part B), so no driver who cannot be
    /// hit is ever shown firing.</item>
    /// <item>The same while the crane is in a ruin (CraneInRuin: a destroyed bridge, its deck and 88 px before each end as DV82 has it
    /// for a standing one; the box of a destroyed radar, repair station or factory) without working there: on its way in or out of
    /// a job, or with its job dropped there; one emptied there would stand where no robot can reach it.</item>
    /// </list>
    /// Unchanged: when the driver comes up outside a job, whom he fires at and his rifle (DV31), the crane's orders and jobs, its hull,
    /// everything for every other vehicle. Deterministic: sim state and sim time only, no draw of its own (Zod's snipe roll is drawn
    /// for a hit on the crane while he is up, as for any vehicle whose driver can be hit), no frame time, no view state. Switch:
    /// ZWorld.CraneSnipeDeviation (the game turns it on at every start, off in 'Zod exact'; headless default off; held off in the
    /// parity report's Deviations block and in every counted check unless the check sets it itself).
    /// </summary>
    public partial class ZObject
    {
        double csSpotAt = -1; bool csSpot;

        /// <summary>DV141: this crane's driver is up in the cab hatch (DV31), the crane does not work, and it can be sniped.</summary>
        public bool CraneDriverExposed => ZWorld.CraneSnipeDeviation && ZWorld.SnipeDeviation && Kind == ZKind.Vehicle && Type == "crane" &&
                                          Drivers.Count > 0 && CraneGunUp && !CraneWorking;

        /// <summary>DV141: this crane works: the job of the crane order it is on has begun (ProcessCraneRepairWP sent its CraneAnim on and
        /// is in stage EnterBuilding or ExitBuilding: driving into a ruin and back out, or mending a damaged building at its entrance)
        /// and has not ended (its CraneAnim off and the order's end, or the order dropped; a new order waits for the job's end,
        /// ZObject.CanOverwriteWP). Sim state only.</summary>
        public bool CraneWorking => Kind == ZKind.Vehicle && Type == "crane" && Waypoints.Count > 0 && Waypoints[0].mode == WpMode.CraneRepair &&
                                    Waypoints[0].Equals(lastWp) && (cur.stage == Stage.EnterBuilding || cur.stage == Stage.ExitBuilding);

        /// <summary>DV141, CraneGunStep after the rifle is known: true = the driver stays inside (no target, no shot) and is down at once:
        /// while the crane works (asked every tick), and on a spot where DV82 turns driver hits into hull hits or in a ruin a crane
        /// rebuilds (CraneInRuin). The position is asked every ZWorld.HatchSpotSeconds while the driver has a target, is up or is about
        /// to look for one; otherwise not at all.</summary>
        bool CraneHatchHeld(double time)
        {
            if (!ZWorld.CraneSnipeDeviation || !ZWorld.SnipeDeviation || World == null) return false;
            if (cgTarget == null && cgUpSince < 0 && time < cgNextScan) return false;                 // nothing to look at this tick
            if (time >= csSpotAt) { csSpotAt = time + ZWorld.HatchSpotSeconds; csSpot = World.SnipeSafeSpot(this) || CraneInRuin(); }
            bool work = CraneWorking;
            if (!work && !csSpot) return false;
            if (cgTarget != null || cgUpSince >= 0)
            {
                if (work) { if (ZWorld.CraneWorkHolds < int.MaxValue) ZWorld.CraneWorkHolds++; }
                else if (ZWorld.CraneHatchesHeld < int.MaxValue) ZWorld.CraneHatchesHeld++;
            }
            cgTarget = null; cgUpSince = -1; cgLastShot = -100;                                       // down at once, as a tank's lid shuts there
            return true;
        }

        /// <summary>DV141: the crane's box reaches into a ruin a crane can rebuild (ZObject.GetCraneCenter: bridges, radar, repair station,
        /// factories; ProcessCraneRepairWP drives the crane into it): a destroyed bridge's own width, on the deck or up to
        /// ZWorld.SnipeSafeBridgeReach px before either end along its axis (DV82's area, which ZWorld.SnipeSafeSpot asks of standing
        /// bridges only), or the box of any other destroyed one. Positions only.</summary>
        bool CraneInRuin()
        {
            int x0 = LocX, y0 = LocY, x1 = LocX + WidthPix, y1 = LocY + HeightPix, reach = ZWorld.SnipeSafeBridgeReach;
            foreach (var b in World.Objects)
            {
                if (b.Kind != ZKind.Building || !b.Destroyed || !b.GetCraneCenter(out _, out _)) continue;
                int ax = b.Type == "bridge_horz" ? reach : 0, ay = b.Type == "bridge_vert" ? reach : 0;   // along a bridge's axis only
                if (x0 < b.LocX + b.WidthPix + ax && b.LocX - ax < x1 && y0 < b.LocY + b.HeightPix + ay && b.LocY - ay < y1) return true;
            }
            return false;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV141: the crane's driver can be sniped while he is up in the cab hatch, and stays inside while the crane works
        /// (Sim/ZObject.CraneSnipe.cs). The game turns it on at every start (off in 'Zod exact'); headless default off.</summary>
        public static bool CraneSnipeDeviation = false;
        /// <summary>DV141: the HP one driver hit on the crane removes at least (DC2's jeep floor: a 3-grunt squad needs about 19 driver
        /// hits; a sniper's hit is x SniperDriverFactor = 210, 6 hits). Set on counted fights (Editor/OzV12CraneSnipeCount.cs).</summary>
        public const int CraneDriverHitFloor = 57;
        /// <summary>DV141 statistics (checks, all worlds): times the driver, with a target or up, was sent back in or kept inside because
        /// the crane works; the same on a DV82 spot or in a ruin.</summary>
        public static int CraneWorkHolds, CraneHatchesHeld;

        /// <summary>DV141, ZWorld.DriverHitDamage after the floor of the target type: a driver hit on the crane removes at least
        /// CraneDriverHitFloor HP (before the sniper factor). Every other target: d as it is.</summary>
        internal static int CraneDriverHit(ZObject target, int d) =>
            CraneSnipeDeviation && target != null && target.Kind == ZKind.Vehicle && target.Type == "crane" && d < CraneDriverHitFloor ? CraneDriverHitFloor : d;

        /// <summary>DV141: holds the rule off (the parity report's Deviations block, counted checks), puts it back on Dispose.</summary>
        public sealed class CraneSnipeHold : IDisposable
        {
            readonly bool was;
            internal CraneSnipeHold(bool on) { was = CraneSnipeDeviation; CraneSnipeDeviation = on; }
            public void Dispose() { CraneSnipeDeviation = was; }
        }
        public static CraneSnipeHold CraneSnipeOff() => new CraneSnipeHold(false);
        public static CraneSnipeHold CraneSnipeOn() => new CraneSnipeHold(true);
    }
}
