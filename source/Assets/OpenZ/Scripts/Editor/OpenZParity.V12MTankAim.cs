#if !OPENZ_HARNESS
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV151 "the medium tank misses more often until it becomes a veteran" (1.2 run 10, owner; the rule:
    /// Sim/ZObject.MTankAim.cs). Counted headless (Editor/OzV12MTankAimCount.cs, W/RESULTS_V12.md run 10 item 1e): a medium tank that is
    /// not a veteran and a veteran one against a moving light tank at medium range, shells and hits, the switch off and on; light and heavy tanks unchanged.</summary>
    public static partial class OpenZParity
    {
        static void AddMTankAimDeviation(List<Deviation> list)
        {
            list.Add(new Deviation
            {
                id = "DV151", change = "The medium tank's aim is slightly less sure until it becomes a veteran",
                zod = "Z's shells scatter +-16 px for every unit (rand()%32 - 16 per axis, zobject.cpp:2155); OpenZ's DV12 veterans 3/4 of it",
                openz = $"the medium tank only: while it is not a veteran its shells scatter x{ZWorld.MTankGreenScatter:0.00}; " +
                        "a veteran medium tank exactly as before (DV12). The same random draws (only their size changes); light, heavy, mech and every other unit untouched",
                measured = "counted headless (Editor/OzV12MTankAimCount.cs Run11Report): see W/RESULTS_V12.md, run 11 item 1 (run 10 item 1e: x2.40, too much)",
                toggle = "ZWorld.MTankAimDeviation (code switch; ZGame: with DV145, off in 'Zod exact'; headless default off)",
                status = DevActive,
            });
        }
    }
}
#endif
