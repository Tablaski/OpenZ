#if !OPENZ_HARNESS
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV153 "fort tower guns are answered" (1.3 owner item 3; the rule: Sim/ZObject.TowerAnswer.cs). Counted headless
    /// by the menu OpenZ/V1.3/Tower guns: every unit in range fires (Editor/OzV13TowerFireCount.cs; minutes, so not run inside the parity report).</summary>
    public static partial class OpenZParity
    {
        static void AddTowerAnswerDeviation(List<Deviation> list)
        {
            list.Add(new Deviation
            {
                id = "DV153", change = "Fort tower guns are answered: a unit that can hit one from where it stands always may, and an automatic chase of one goes to a spot with a clear shot",
                zod = "Zod's auto-target rules treat a tower gun like any target: as a goal it is never reachable (it stands inside the fort's blocked tiles), so the 'failed to reach' memory and the straight AGRO chase at its centre left units walking or driving at the fort without a shot",
                openz = "fort tower guns only (the cannons Zod makes not ejectable): never dropped as 'unreachable' by the attack-move pick or the squad help call while the unit can hit it (range, Zod's barrier rule, DV23's line); " +
                        "an automatic chase of one out of reach is an ATTACK (Zod's planner, DV28 / DV23: the nearest spot in range with a clear shot), not an AGRO; an attack on one with no shot spot is remembered like an unreachable goal for 6 s; the squad help call on one also reaches a unit that is only stepping aside (a shell's sidestep, a nudge): its chase goes in behind the step. " +
                        "Ranges, damage, rates, rolls and the player's own orders unchanged. Deterministic: no random draw",
                measured = "counted headless: menu OpenZ/V1.3/Tower guns: every unit in range fires, counted (headless, DV153) (levels 2 / 7 / 16 / 17, every weapon; idle in range, attack order, under fire, attack-move; shooters staged so no shell kills them)",
                toggle = "ZWorld.TowerAnswerDeviation (code switch; ZGame: on, off in 'Zod exact'; headless default off)",
                status = DevActive,
            });
        }
    }
}
#endif
