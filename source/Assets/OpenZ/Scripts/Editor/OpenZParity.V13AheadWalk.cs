#if !OPENZ_HARNESS
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV166 (1.3 item 29, a player: units answered the order and did not move): a squad member level with or
    /// ahead of its walking leader, out of his reach, that the squad follow would stand until he has walked past it, walks its own path to the
    /// order's goal instead (the rule: Sim/ZCommit.AheadWalk.cs). Counted headless (Editor/OzV13GroupOrderCount.cs): 200 seeded group orders
    /// on L02, L16 and L17, before and after.</summary>
    public static partial class OpenZParity
    {
        static void AddAheadWalkDeviation(List<Deviation> list)
        {
            list.Add(new Deviation
            {
                id = "DV166", change = "A squad member ahead of its leader walks on to the goal instead of standing until he has walked past it",
                zod = "every minion gets the leader's waypoint (CloneMinionWayPoints, zobject.cpp) and path-finds to the same point itself; OpenZ's squad " +
                      "follow (DV24 / DV44) has the minions follow the leader's trail instead, and one level with or in front of him on his way stood until " +
                      "he had walked up to it and past it (staged 6 tiles in front: its first step after about 3 s, 16 px towards the goal after about 5 s)",
                openz = "such a member farther than " + ZPathing.AheadReach + " px from its leader, or beside his line with a wall or rock right in " +
                        "front of it, walks its own path to the order's goal (Zod's minion " +
                        "path, the follow's own exit), and re-joins the follow within " + (ZPathing.AheadReach - ZPathing.AheadHysteresis) + " px of him or " +
                        "behind him; nearer than that with a free step the follow's side-step, walk on beside him and short wait stay as they were; both teams" +
                        (ZWorld.AheadWalkDeviation ? "" : " (switch off in this run)"),
                measured = "counted headless (Editor/OzV13GroupOrderCount.cs, menu OpenZ/V1.3/Count Group Orders Not Moving (item 29)): squad members " +
                           "standing 1 s after 200 seeded group orders per level while their leader walks, before and after; report " +
                           "work/records/milestones/v1_3_20261030/item29_group_orders/counts.txt",
                toggle = "ZWorld.AheadWalkDeviation (code switch; ZGame: on, off in 'Zod exact'; headless default off)",
                status = DevActive,
            });
        }
    }
}
#endif
