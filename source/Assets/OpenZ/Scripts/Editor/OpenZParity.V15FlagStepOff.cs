#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Sim/ZObject.FlagStepOff.cs). Measured headless on a plain map (Editor/OzV15VehicleOffFlagCount.cs Quick); the whole check: menu
    /// OpenZ/V1.5/Vehicle Off Flag: Count (headless, DV184). The parity's Zod rows run with the switch held off (OpenZParity.cs), the block's
    /// other probes under OzV11Kit.GameSwitches (off there too).</summary>
    public static partial class OpenZParity
    {
        static void AddFlagStepOffDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15VehicleOffFlagCount.Quick(SettingsText);   // the counted checks' game switches, put back after
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.5/Vehicle Off Flag: Count (headless, DV184))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV184", change = "A VEHICLE that captured a flag and then stands on it with no further order rolls once a short way off it, to the nearest free spot beside the flag",
                zod = "zserver.cpp CheckFlagCaptures / oflag.cpp: the first enemy unit whose box touches the flag takes its zone; the vehicle's MOVE simply ends where it was sent "
                      + "and it parks on top of the flag until it gets another order",
                openz = $"a vehicle (any team, the computer's too; never a robot, a gun or the shuttle) that captured a flag, came to rest on it and has no order rolls off "
                        + $"{ZObject.FlagOffSettleTicks} ticks (0.5 s) later, once, by the pathing layer's own step-aside MOVE: the box moved in one of 8 directions just far enough to clear the flag "
                        + $"by {ZObject.FlagOffGapPx} px (at most {ZObject.FlagOffMaxPx} px per axis), the shortest move weighted to roll on ahead of its last heading; the spot and the half way there "
                        + "clear of the vehicle grid and of every other object. It stays (overlap is fine) when there is no such spot, or the capture came with any other order (queued waypoints, "
                        + "attack or any non-move order, a move past the flag, patrol, hold, a group move, an explicit STOP where it stands) or any order follows it, or it waits more than "
                        + $"{ZObject.FlagOffWaitTicks / 100} s (a fight on the flag). The capture rule, zones and flags are unchanged; flags never block movement. Deterministic: integers and the object order",
                measured = measured,
                toggle = "ZWorld.FlagStepOffDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off: Zod parks on the flag)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
