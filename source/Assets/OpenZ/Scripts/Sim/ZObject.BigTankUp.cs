using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// Fighting shooting so easier to snipe.
    /// <para>Before: the MAMMOTH (DV180) and the BIZON (DV181) are lid tanks like the heavy tank, so their driver is up in the open hatch (and
    /// can be sniped, ZObject.CanBeSniped) only while Zod's lid is open: it opens 4 times in 5 when the tank takes a NEW target that can snipe
    /// (ZObject.SignalLidShouldOpen, zvehicle.cpp:198-207: never for a tank, a Tough, a gun or a building), it closes 0-1.4 s after the tank lets
    /// go of its target (SignalLidShouldClose, zvehicle.cpp:209-240), a close still pending when the next target is taken is not called off (so
    /// the lid shuts during that fight), and while the tank drives to a target out of its range it has no target at all (lid shut). In the
    /// scripted fights of the kit's counted check the driver was up only part of the fighting time (the kit's README has the numbers).</para>
    /// <para>DV211, for the Mammoth and the Bizon only, an owned and crewed one: while it fights the lid is held open, so its driver is up
    /// the whole fight. It fights while it has a target (ZObject.AttackObject, any target: a robot, a vehicle, a gun, a building), or its
    /// first order is an ATTACK / AGRO on a target inside its agro reach (driving in to fire), and for <see cref="ZWorld.BigTankUpLingerSeconds"/>
    /// (8 s) after the last tick it did (it fired in the last 8 s: moving between targets, its target out of sight for a moment): Zod's own
    /// close of the lid is set to the end of those 8 s (so letting go of a target draws no close of its own while he is held up). After 8 s without a
    /// fight the lid shuts and the driver is down as before (Zod's lid, DV75's rest after a long calm).</para>
    /// <para>
    /// tank often makes the 4 kills of a veteran (counted: without this its driver was up only 63-83 % of the fight). The view shows a veteran
    /// driver of a big tank while this rule holds the lid (View/BoardingView.cs ShowHatchDriver, one guard); DV12's snipe factor on him stays
    /// (x0.25), and like every veteran he does not fire his own gun from the hatch (DV87 part A).</para>
    /// <para>Unchanged: how he can be sniped while he is up (CanBeSniped: the same rule as an open lid; who can snipe, the hit and snipe
    /// rolls, the driver's pool, DC2's floors, the sniper factor, DV12's veteran factor), his own weapon while up (DV87 part A: the psycho's gun,
    /// its range, damage and cycle), no driver up on a bridge or in a door lane (DV87 part B, kit b1: he goes down there and comes up again past
    /// it), Zod's lid roll (drawn as before), every other vehicle, the hull, the guns, speeds and health.</para>
    /// <para>Switch <see cref="ZWorld.BigTankUpDeviation"/>: View/ZGame.cs on at every level start, off in 'Zod exact'; headless default off;
    /// off in every counted check unless the check sets it (Editor/OzV11Kit.cs); the parity report's Zod rows and probes hold it off; its row
    /// DV211 (Editor/OpenZParity.V15BigTankUp.cs). Deterministic: sim time and sim state only (the tick's time and index, the target, the first
    /// order, whole-pixel distances), no random draw of its own, no frame time, no view state. The view shows the driver whenever the lid is
    /// open (View/BoardingView.cs ShowHatchDriver; the only view change is the veteran guard above).</para>
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV211 switch (static like the others). View/ZGame.cs sets it at every level start (off in 'Zod exact').</summary>
        public static bool BigTankUpDeviation = false;

        /// <summary>DV211: seconds the driver stays up after the big tank last fought (a target, or an attack / agro order on a target in its
        /// agro reach). The lead's "fighting": it has a target, is firing, or fired in the last 8 s.</summary>
        public const double BigTankUpLingerSeconds = 8.0;

        /// <summary>DV211 statistics (checks, all worlds): lids the rule opened (a fight that Zod's roll left shut, a target Zod never opens
        /// for, the drive in to a target, past a bridge / door lane); holds that ended after 8 s without a fight.</summary>
        public static int BigTankUpOpens, BigTankUpDowns;

        public static IDisposable BigTankUpOff() => new BigTankUpHold(false);
        public static IDisposable BigTankUpOn() => new BigTankUpHold(true);
        sealed class BigTankUpHold : IDisposable
        {
            readonly bool was = BigTankUpDeviation;
            public BigTankUpHold(bool on) { BigTankUpDeviation = on; }
            public void Dispose() { BigTankUpDeviation = was; }
        }
    }

    public partial class ZObject
    {
        double btFightAt = -1, btUntil = -1; long btTick = -1; bool btHeld;

        /// <summary>DV211: sim time this big tank last fought (-1 = not in this hold). Checks only.</summary>
        public double BigTankFightAt => btFightAt;
        /// <summary>DV211: the rule holds this big tank's lid open now (its driver up for the fight). Checks only; the view reads LidOpen.</summary>
        public bool BigTankUpHeld => btHeld;

        /// <summary>DV211: this vehicle is a Mammoth or a Bizon the rule is for (switch on, owned, crewed; a veteran crew too).</summary>
        bool BigTankUpFor => ZWorld.BigTankUpDeviation && Kind == ZKind.Vehicle && HasLid && ZBigTanks.Is(Type) && !Destroyed && Owner != Team.Neutral && Drivers.Count > 0;

        /// <summary>DV211: the big tank fights in this tick: it has a target, or its first order is an attack / agro on a live target of
        /// another side inside its agro reach (it drives in to fire).</summary>
        public bool BigTankFighting()
        {
            if (AttackObject != null) return true;
            if (Waypoints.Count == 0 || World == null) return false;
            var wp = Waypoints[0];
            if (wp.mode != WpMode.Attack && wp.mode != WpMode.Agro) return false;
            var t = World.Get(wp.refId);
            return t != null && !t.Destroyed && t.KillAt < 0 && t.Owner != Owner && Within(CenterX, CenterY, t.CenterX, t.CenterY, AgroReachTo(t));
        }

        /// <summary>DV211, in DriverFireStep right after DV87 part B (every tick of a lid tank, also in the stun hold; after the tick's move
        /// and shot, before the hatch driver's own shot): while the big tank fights, and until BigTankUpLingerSeconds after the last tick it
        /// did, the lid is open, and Zod's own close of the lid (ProcessServerLid) is set to that moment: a close drawn when the tank lets go
        /// of a target is moved there, and when 8 s pass without a fight Zod's close step shuts the lid (this step does it in a stun hold,
        /// where Zod's close step does not run).</summary>
        void BigTankUpStep(double time)
        {
            if (!ZWorld.BigTankUpDeviation || !ZBigTanks.Is(Type)) return;                       // every other vehicle, and the switch off: nothing here
            long tick = World != null ? World.TickIndex : 0;
            if (btHeld && tick != btTick + 1) BigTankUpRelease();                                 // this step did not run in the last tick (the tank stood empty: DV79's hold ends its tick before it): a crew in it now starts afresh
            btTick = tick;
            if (!BigTankUpFor) { if (btHeld) BigTankUpRelease(); return; }                        // emptied (with DV79 off; with it on the hold ends the tick before this step): Zod's lid
            if (BigTankFighting()) { btFightAt = time; btUntil = time + ZWorld.BigTankUpLingerSeconds; }
            if (btFightAt < 0 || time >= btUntil)
            {                                                                                     // no fight for 8 s: down (Zod's close step already shut the lid at btUntil)
                if (btHeld) { BigTankUpRelease(); if (ZWorld.BigTankUpDowns < int.MaxValue) ZWorld.BigTankUpDowns++; }
                return;
            }
            btHeld = true;
            if (!LidOpen)
            {
                if (HatchShutSpot(time, true)) { doCloseLid = false; return; }                     // DV87 part B: never up on a bridge or in a door lane (up again past it)
                LidOpen = true;
                if (ZWorld.BigTankUpOpens < int.MaxValue) ZWorld.BigTankUpOpens++;
            }
            doCloseLid = true; nextCloseLidTime = btUntil;                                         // Zod's own close, 8 s after the last fighting tick
        }

        /// <summary>DV211: the hold ends. No target: the lid shuts now (the driver down). A target still set (the switch turned off in a
        /// fight, a driver shot out with DV79 off): the lid stays as Zod has it and Zod's own close comes when the tank lets go of it.</summary>
        void BigTankUpRelease()
        {
            btHeld = false; btFightAt = btUntil = -1;
            if (AttackObject == null) LidOpen = false;
            doCloseLid = false;
        }
    }
}
