#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV186 "a rammed or crushed vehicle's turret lands near its wreck" (version 1.5, kit ram_throw_fix; the rule:
    /// Sim/ZWorld.RamDebris.cs on DV182 / DV183). Measured headless (Editor/OzV15RamDebrisCount.cs Quick, on plain maps); the whole check: menu
    /// OpenZ/V1.5/Ram Throw Fix: Ram Debris Count (headless, DV186). Only a ram's or a crush's victim is touched, so a level, a bot match and the
    /// parity's other probes are as before. The parity report's Zod rows and every other row's probe hold it off.</summary>
    public static partial class OpenZParity
    {
        static void AddRamDebrisDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15RamDebrisCount.Quick(SettingsText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.5/Ram Throw Fix: Ram Debris Count (headless, DV186))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV186", change = "a vehicle or gun destroyed by a Mammoth's ram or a Bizon's crush: its turret debris lands next to its wreck, not up to 37 m away",
                zod = "vlight.cpp:261-278 / cgatling.cpp:279-296: the turret debris lands at a random point up to max_turrent_horizontal_distance (300 px) away on each axis (Z has no ram or crush)",
                openz = "a ram's victim: " + ZWorld.RamDebrisAheadPx + " px beyond its centre along the ram (the side it is flipped to) +- " + ZWorld.RamDebrisSpreadPx + " px; a crush's: "
                        + ZWorld.CrushDebrisBackPx + " px behind its centre +- " + ZWorld.CrushDebrisSpreadPx + " px; Zod's own two random numbers scaled down (nothing drawn more or less); "
                        + "damage, radius and time Zod's; every other death as Zod's",
                measured = measured,
                toggle = "ZWorld.RamDebrisDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; acts only on DV182 / DV183 victims; the parity report's Zod rows and probes hold it off)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
