using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ deviation DV143 "the APC's lone driver can be shot out while he is up in the roof hatch" (version 1.2, item 69; part of
    /// the driver-sniping deviation DC2, the same rule as the crane's driver, DV141). An APC with its driver and nobody else aboard
    /// brought him up through the roof hatch to fire, and no sniper, jeep or squad could ever hit him.
    /// <para>Zod: the APC fires its crew's weapon from inside, never shows anybody and can never be sniped (vapc.cpp sets no
    /// can_be_sniped, zobject.cpp:77 leaves it false; CanBeSniped zobject.cpp:4693-4696, zvehicle.cpp:178-186). OpenZ DV87 part C
    /// (ZObject.DriverFire.cs) brings the lone driver up to fire (ApcDriverUp: from the moment the APC has a target until
    /// ZWorld.CraneGunLinger after it last had one), but left him out of the sniping rule: every shot at him went to the hull.</para>
    /// <para>DV143, with ZWorld.ApcSnipeDeviation and DC2 (ZWorld.SnipeDeviation) on:</para>
    /// <list type="bullet">
    /// <item>While the lone driver is up (ApcDriverUp, at most CraneGunLinger after the APC last had a target) the APC is a vehicle
    /// whose driver can be hit (ZObject.CanBeSniped), like a tank with its lid open and the crane's driver: Zod's list of who can
    /// snipe (grunt, psycho, sniper, pyro, laser, jeep, gatling), Zod's hit roll and snipe roll with each attacker's own chance
    /// (zobject.cpp:2187-2199) and every OpenZ factor on that roll (DV27 from behind, DV121), the driver's own health as the pool
    /// (1081 HP for a grunt).</item>
    /// <item>One driver hit on the APC removes at least <see cref="ZWorld.ApcDriverHitFloor"/> HP (the crane driver's floor of
    /// DV141, the jeep's open-seat floor of DC2: the same robot with the same rifle; a sniper's hit x ZWorld.SniperDriverFactor as on
    /// every vehicle).</item>
    /// <item>The emptied APC is a sniped vehicle like every other (zserver.cpp:1808-1874): neutral, stopped, its orders gone, any team
    /// can drive it (a squad that walks in: its leader drives, the others ride, DV15).</item>
    /// <item>Unchanged: with passengers aboard, for a veteran crew (DV12) and on a bridge or in a door lane (DV82) he stays inside
    /// (DV87 part C) and the APC cannot be sniped; when he comes up, whom the APC fires at and its weapon; the hull; every other
    /// vehicle.</item>
    /// </list>
    /// Deterministic: sim state and sim time only, no draw of its own (Zod's snipe roll is drawn for a hit on the APC while he is up,
    /// as for any vehicle whose driver can be hit), no frame time, no view state. Switch: ZWorld.ApcSnipeDeviation (the game turns it
    /// on at every start, off in 'Zod exact'; headless default off; held off in the parity report's Deviations block and in every
    /// counted check unless the check sets it itself).
    /// </summary>
    public partial class ZObject
    {
        /// <summary>DV143: the lone driver of this APC is up in the roof hatch (DV87 part C: a target now, or one within the last
        /// CraneGunLinger s), he is not a veteran, and the APC can be sniped. An APC emptied while he was up and entered again later
        /// shows nobody up until it has a target again.</summary>
        public bool ApcDriverExposed => ZWorld.ApcSnipeDeviation && ZWorld.SnipeDeviation && ApcDriverUp && !VeteranActive && World != null && World.Time < dfUpUntil;
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV143: the APC's lone driver can be sniped while he is up in the roof hatch (Sim/ZObject.ApcSnipe.cs). The game turns
        /// it on at every start (off in 'Zod exact'); headless default off.</summary>
        public static bool ApcSnipeDeviation = false;
        /// <summary>DV143: the HP one driver hit on the APC removes at least: the crane driver's floor (DV141, DC2's jeep floor: a 3-grunt
        /// squad needs about 19 driver hits; a sniper's hit is x SniperDriverFactor = 210, 6 hits).</summary>
        public const int ApcDriverHitFloor = CraneDriverHitFloor;

        /// <summary>DV143, ZWorld.DriverHitDamage after the crane's floor: a driver hit on the APC removes at least ApcDriverHitFloor HP
        /// (before the sniper factor). Every other target: d as it is.</summary>
        internal static int ApcDriverHit(ZObject target, int d) =>
            ApcSnipeDeviation && target != null && target.IsApc && d < ApcDriverHitFloor ? ApcDriverHitFloor : d;

        /// <summary>DV143: holds the rule off (the parity report's Deviations block, counted checks), puts it back on Dispose.</summary>
        public sealed class ApcSnipeHold : IDisposable
        {
            readonly bool was;
            internal ApcSnipeHold(bool on) { was = ApcSnipeDeviation; ApcSnipeDeviation = on; }
            public void Dispose() { ApcSnipeDeviation = was; }
        }
        public static ApcSnipeHold ApcSnipeOff() => new ApcSnipeHold(false);
        public static ApcSnipeHold ApcSnipeOn() => new ApcSnipeHold(true);
    }
}
