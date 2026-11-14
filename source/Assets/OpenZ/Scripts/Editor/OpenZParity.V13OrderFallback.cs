#if !OPENZ_HARNESS
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV158 (number left open; 1.3 item 29, a player: units answered the order and did not move): a
    /// human player's unit that cannot attack the clicked object walks to the clicked point instead of standing still (the rule:
    /// Sim/ZWorld.OrderFallback.cs). Counted headless (Editor/OzV13GroupOrderCount.cs): 200 seeded group orders on L02, L16 and L17,
    /// before and after.</summary>
    public static partial class OpenZParity
    {
        static void AddOrderFallbackDeviation(List<Deviation> list)
        {
            list.Add(new Deviation
            {
                id = "DV158", change = "A unit that cannot attack the clicked object walks to the clicked point instead of standing still",
                zod = "every selected unit gets an ATTACK on the object under the pointer when it is not the player's (zplayer.cpp:2746-2881); the server " +
                      "clears the unit's list and drops the ATTACK it cannot carry out (zcore.cpp:234-243, :548): a robot without grenades at a building or a " +
                      "map object, a jeep at a rock, an APC or a crane at anything stops where it is",
                openz = "a human player's unit that can move gets a plain MOVE to the clicked point in that case (the click's attack-move flag, its own spot " +
                        "round the point); units that can attack the object keep Zod's ATTACK; the CPU's orders, guns and every other order unchanged" +
                        (ZWorld.OrderMoveFallbackDeviation ? "" : " (switch off in this run)"),
                measured = "counted headless (Editor/OzV13GroupOrderCount.cs, menu OpenZ/V1.3/Count Group Orders Not Moving (item 29)): silent stand-stills " +
                           "1 s after 200 seeded group orders per level, before and after; report work/records/milestones/v1_3_20261030/item29_group_orders/counts.txt",
                toggle = "ZWorld.OrderMoveFallbackDeviation (code switch; ZGame: on, off in 'Zod exact'; headless default off)",
                status = DevActive,
            });
        }
    }
}
#endif
