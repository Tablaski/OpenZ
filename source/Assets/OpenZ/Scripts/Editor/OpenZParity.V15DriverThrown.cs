#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV179 "a driver getting out is thrown by his hull's blast" (version 1.5, kit exit_leap_death; the rule:
    /// Sim/ZObject.DriverThrown.cs). Measured headless (Editor/OzV15ExitLeapDeathCount.cs Quick, on a plain map); the whole check: menu
    /// OpenZ/V1.5/Exit Leap Death: Count (headless, DV179). Only a driver the player let out by EXIT DRIVER (DV171) is ever thrown, and the
    /// computer never uses EXIT DRIVER, so a bot match and the parity's other probes are as before. The parity report's Zod rows switch it off.</summary>
    public static partial class OpenZParity
    {
        static void AddDriverThrownDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15ExitLeapDeathCount.Quick(SettingsText);   // the counted checks' game switches, put back after
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.5/Exit Leap Death: Count (headless, DV179))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV179", change = "EXIT DRIVER: a tank or the mech destroyed before its driver has jumped off throws him: he is held down for 2.6 s (thrown, on the ground, getting up), a target meanwhile",
                zod = "Zod has no EXIT DRIVER for tanks (DV171 is OpenZ's); a tank destroyed with its driver inside kills him with it",
                openz = "the driver let out by EXIT DRIVER stands on his spot beside the hull from the EXIT's tick and is held as getting out until his jump lands (DV171 / DV132); "
                        + $"before 1.5 a hull destroyed meanwhile freed him at once. Now a hull destroyed less than {ZObject.DriverThrownWindowTicks} ticks (0.42 s, his take-off) after "
                        + $"the EXIT holds him {ZObject.DriverThrownTicks} ticks ({ZObject.DriverThrownTicks * ZConst.TICK:0.00} s) from the tick his hold sees it: no step, no shot, his "
                        + "orders wait and start from his spot; he is a target and can be killed meanwhile; the throw itself does not hurt him (the round's own splash may). A hull "
                        + "destroyed later frees him at once, as before. The sim's own tick times and fixed tick counts only, no random draw (deterministic)",
                measured = measured,
                toggle = "ZWorld.DriverThrownDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows switch it off)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
