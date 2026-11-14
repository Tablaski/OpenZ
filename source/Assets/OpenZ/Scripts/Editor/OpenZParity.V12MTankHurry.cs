#if !OPENZ_HARNESS
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV150 "the medium tank picks up speed when it fights under fire" (1.2 run 9, owner; the rule:
    /// Sim/ZObject.MTankHurry.cs). Counted in Play (W/RESULTS_V12.md, run 9b, item "medium tank speed"): the medium, light and heavy tank
    /// each under long-range fire, average speed while manoeuvring against its own open-ground speed, switch off and on.</summary>
    public static partial class OpenZParity
    {
        static void AddMTankHurryDeviation(List<Deviation> list)
        {
            list.Add(new Deviation
            {
                id = "DV150", change = "The medium tank picks up speed when it fights under fire",
                zod = "Z's units drive at their own speed in a fight; only the sidestep run (x1.8) is faster, for every type",
                openz = $"the medium tank only: while its order is a fight or a sidestep (Attack, Agro, Dodge; never a plain move) and an enemy shell was aimed at it " +
                        $"or an enemy hit it within {ZWorld.MTankHurrySec:0.#} s, it drives x{ZWorld.MTankHurryMul:0.00} (x{ZWorld.MTankHurryFarMul:0.00} when the shell came from beyond its own range); " +
                        "Zod's run still wins while it runs. Sim time only, no random number; light, heavy, mech and jeep untouched",
                measured = "counted in Play (menu-free probe, run 9b): see W/RESULTS_V12.md, 'medium tank speed'",
                toggle = "ZWorld.MTankHurryDeviation (code switch; ZGame: with DV145, off in 'Zod exact'; headless default off)",
                status = DevActive,
            });
        }
    }
}
#endif
