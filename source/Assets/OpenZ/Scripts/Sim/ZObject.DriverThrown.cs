using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// 1.5 rule DV179 "thrown by the blast" (kit exit_leap_death), on top of DV171 EXIT DRIVER (Sim/ZObject.VehicleExit.cs) and DV132's
    /// getting-out hold (Sim/ZObject.ExitWalk.cs). The owner's known point carried over from 1.4.
    /// Before (1.4): the driver let out by EXIT DRIVER stands on his ground spot beside the hull from the EXIT's tick and is held as getting
    /// out until his figure's feet touch the ground (ZObject.DriverOutHoldTicks: 1.36-1.46 s); when the hull is destroyed meanwhile the hold
    /// ends at once (DriverOutHullGone) and the view lets his figure take the same calm jump with a somersault at once.
    /// Now, while <see cref="ZWorld.DriverThrownDeviation"/> is on (View/ZGame.cs: on at every start, off in 'Zod exact'; headless default
    /// off): a hull destroyed BEFORE his take-off (its death time, KillAt, less than <see cref="DriverThrownWindowTicks"/> after the EXIT's
    /// click: the lid opening and his crouch on the rim; the take-off is 0.42 s after the click) throws him: instead of ending, his hold
    /// becomes <see cref="DriverThrownTicks"/> from the tick his hold sees the hull gone (the flight off the hull, the fall, the time on the
    /// ground and the scramble up, drawn by View/BoardingView.HatchThrow.cs). While thrown he takes no step and fires nothing (DV132's hold:
    /// his orders wait and start from his spot when it ends) and he IS a target (<see cref="DriverThrownHeld"/> in DriverOutTarget: he can
    /// be shot and killed while he lies). He is not hurt by the throw itself: damage stays the rules' own (a round's splash that killed the
    /// hull may kill him too: then he dies, and the view draws that death on his thrown figure). A hull destroyed after the take-off (the
    /// window over): as before (free at once; his figure is off the hull already).
    /// Deterministic: the sim's own tick times (KillAt, DriverOutAt, the tick's time), fixed tick counts, no random draw, no float. With the
    /// switch off, or with nobody let out by EXIT DRIVER (the computer never uses it), every tick is the previous build's: DriverThrownOut
    /// returns the old value (the tick's time) and DriverThrownHeld is never true.
    /// </summary>
    public partial class ZObject
    {
        /// <summary>DV179: a hull destroyed this many sim ticks after the EXIT's click or later no longer throws him (his take-off: the lid
        /// kicked open and his crouch end 0.42 s after the click, ZObject.DriverOutHoldTicks' timeline).</summary>
        public const long DriverThrownWindowTicks = 42;
        /// <summary>DV179: how long a thrown driver is held (sim ticks, from the tick his hold sees the hull gone): the flight off the hull
        /// (about 1.0-1.2 s from the hatch rim), the fall (0.45 s), the time on the ground and the scramble up (the view stretches the time on
        /// the ground so he stands at this moment).</summary>
        public const long DriverThrownTicks = 260;
        /// <summary>DV179: the vehicle whose blast threw him (-1: none) and the sim time his thrown hold ends (-1: none). A record, kept after
        /// the hold ends (the view reads it: View/BoardingView.HatchThrow.cs).</summary>
        public int DriverThrownFromId = -1; public double DriverThrownAt = -1;
        /// <summary>DV179: held now by the thrown hold (his DV132 out moment is the one DriverThrownOut gave him). A pure read.</summary>
        public bool DriverThrownHeld => DriverThrownAt >= 0 && ExitOutAt >= 0 && Math.Abs(ExitOutAt - DriverThrownAt) <= 1e-9;
        /// <summary>DV179 statistics (checks, all worlds): drivers thrown by their hull's blast.</summary>
        public static int DriversThrown;

        /// <summary>DV179, ExitWalkHold (Sim/ZObject.ExitWalk.cs, one line) when DriverOutHullGone has just ended this rule's hold of a driver
        /// let out of vehicle `fromId`: the new end of his hold. The switch off, or the hull destroyed at or after the window: `time` (out now,
        /// as before). Destroyed before his take-off: thrown, held DriverThrownTicks from `time`. A read of the object list by id (GetAny: the
        /// hull may already be in the graveyard) and the sim's own times: deterministic.</summary>
        internal double DriverThrownOut(int fromId, double time)
        {
            if (!ZWorld.DriverThrownDeviation || !ZWorld.VehicleExitDeviation || World == null || fromId < 0 || Kind != ZKind.Robot) return time;
            var v = World.GetAny(fromId);
            if (v == null || v.Kind != ZKind.Vehicle || !v.IsHatchVehicle) return time;
            double died = v.KillAt >= 0 ? v.KillAt : time;                                   // RelayObjectDeath sets KillAt = the death's time (a vehicle CanBeDestroyed)
            double click = DriverOutAt - DriverOutHoldTicks(v.Type) * ZConst.TICK;           // ExitDriver: DriverOutAt = the click's time + his vehicle's hold
            if (died >= click + DriverThrownWindowTicks * ZConst.TICK - 1e-9) return time;   // after his take-off: free at once (1.4)
            DriverThrownFromId = fromId;
            DriverThrownAt = time + DriverThrownTicks * ZConst.TICK;
            DriversThrown++;
            return DriverThrownAt;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV179 switch (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off, as DV171 / DV176;
        /// OzV11Kit.GameSwitches and the parity's Zod rows hold it off). Off = as before: a hull destroyed while its driver gets out frees
        /// him at once.</summary>
        public static bool DriverThrownDeviation = false;
    }
}
