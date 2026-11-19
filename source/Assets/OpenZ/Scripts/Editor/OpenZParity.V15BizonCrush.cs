#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV183 "the BIZON CRUSH" (version 1.5, kit bizon_crush; the rule: Sim/ZObject.BizonCrush.cs on DV182's ram
    /// machinery). Measured headless (Editor/OzV15BizonCrushCount.cs Quick, on plain maps); the whole check: menu OpenZ/V1.5/Bizon Crush: Count
    /// (headless, DV183). The Bizon is placed only by the Unit editor (DV181), so a level, a bot match and the parity's other probes are as before.
    /// The parity report's Zod rows and every other row's probe hold it off.</summary>
    public static partial class OpenZParity
    {
        static void AddBizonCrushDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15BizonCrushCount.Quick(SettingsText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.5/Bizon Crush: Count (headless, DV183))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV183", change = "the BIZON CRUSH: a Bizon rolls over an enemy vehicle or ground gun close in front of its hull by itself (a short boost); the target is squashed under its tracks and destroyed",
                zod = "Z has no crush and no Bizon (DV181 is OpenZ's); vehicles never hurt vehicles or guns by contact",
                openz = $"the Mammoth's ram rules (DV182) for the start: a crewed Bizon, the target inside +/- {ZObject.RamStartArcDeg} deg of its heading and at most {ZObject.RamStartGapPx} px hull to hull, "
                        + $"the way drivable, the boost ACCELERATING from its speed to x {ZObject.CrushBoostPct / 100.0:0.00} over {ZObject.RamRampTicks} ticks (whole-percent steps), held through the crush; targets: enemy crewed vehicles of any type and enemy guns standing on the ground (never a fort tower gun, never one on a deck or building). "
                        + $"Front contact holds the target and the Bizon drives on over it; it is destroyed with its crew when the Bizon's front is two thirds across it, at the latest {ZObject.CrushFallbackTicks} ticks "
                        + $"({ZObject.CrushFallbackTicks * ZConst.TICK:0.00} s) after the contact (the Bizon's kill), whatever its health; one target at a time, then its order goes on; {ZObject.RamCoolTicks} ticks before the next. "
                        + "Whole numbers, sim ticks and object order only, no random draw (deterministic)",
                measured = measured,
                toggle = "ZWorld.BizonCrushDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; needs DV181's switch; the parity report's Zod rows and probes hold it off)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
