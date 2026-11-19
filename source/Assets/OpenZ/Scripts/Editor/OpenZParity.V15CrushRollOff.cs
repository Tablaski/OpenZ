#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV187 "the Bizon rolls off the far side" (version 1.5, kit bizon_crush_climb; the rule: Sim/ZObject.CrushRollOff.cs
    /// on DV183). Measured headless (Editor/OzV15CrushRollOffCount.cs Quick, on plain maps); the whole check: menu OpenZ/V1.5/Bizon Crush Climb:
    /// Roll Off Count (headless, DV187). Only a crushing Bizon's drive changes, so a level, a bot match and the parity's other probes are as before. The parity report's Zod rows and every other row's probe hold it off.</summary>
    public static partial class OpenZParity
    {
        static void AddCrushRollOffDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15CrushRollOffCount.Quick(SettingsText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.5/Bizon Crush Climb: Roll Off Count (headless, DV187))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV187", change = "the Bizon rolls off the far side: its drive over a vehicle or ground gun it crushed goes on until its rear is off the wreck, the target destroyed right after, where it stood",
                zod = "Z has no crush (DV183 is OpenZ's); DV183 ended the drive with the Bizon's centre on the crushed target's spot",
                openz = "the drive goes on until the Bizon's REAR is off the wreck: the clear point = the target's drawn far extent + the Bizon's drawn rear reach + " + ZObject.CrushClearPx + " px beyond the target's centre along the crush; the target is destroyed "
                        + ZObject.CrushBlastTicks + " ticks after the Bizon's centre passes it (or after a drive that ended early; never later than DV183's " + ZObject.CrushFallbackTicks + "-tick fallback), never moved; the drive's end is " + ZObject.CrushBrakePx
                        + " px beyond the clear point, braking at " + ZObject.CrushBrakeAccelPx + " px/s2 over its last part (never below its plain speed); time limit " + ZObject.CrushMaxTicks + " + " + ZObject.CrushRollOffTicks
                        + " ticks; the contact, the boost, the end when blocked or held (never through a unit) are DV183's; whole numbers, no random draw",
                measured = measured,
                toggle = "ZWorld.CrushRollOffDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; acts only with DV183 on; the parity report's Zod rows and probes hold it off)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
