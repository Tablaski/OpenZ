#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV185 "the RAM DUEL" (version 1.5, kit big_tanks_fx; the rule: Sim/ZObject.RamDuel.cs on DV182 / DV183's ram
    /// machinery). Measured headless (Editor/OzV15RamDuelCount.cs Quick, on plain maps); the whole check: menu OpenZ/V1.5/Big Tanks FX: Ram Duel
    /// Count (headless, DV185). The Mammoth and the Bizon are placed only by the Unit editor, so a level, a bot match and the parity's other probes
    /// are as before. The parity report's Zod rows and every other row's probe hold it off.</summary>
    public static partial class OpenZParity
    {
        static void AddRamDuelDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15RamDuelCount.Quick(SettingsText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.5/Big Tanks FX: Ram Duel Count (headless, DV185))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV185", change = "the RAM DUEL: two enemy big tanks trying to ram each other (Mammoth / Bizon, any pair) get one clean result; a rammer that is rammed stops at once",
                zod = "Z has no ram, no crush and no big tanks (DV180-DV183 are OpenZ's)",
                openz = "a tank never starts a ram on a tank already ramming it unless it wins the duel: the ram that started first wins, the same tick the lower object id; "
                        + "the loser's ram is cancelled that tick (its order goes on); so never both destroyed by the pair, never both stuck, never a flip and a crush on one pair. "
                        + "A Mammoth rammed or crushed mid-ram stops at once (its target untouched unless already hit); a Bizon rammed or crushed while rolling over a vehicle it crushed: "
                        + "that vehicle is destroyed the next tick (the Bizon's kill), not up to " + ZObject.CrushFallbackTicks + " ticks later. Tick counts and object ids only, no random draw (deterministic). "
                        + "The turret turn rates of the two tanks (Mammoth = heavy x 0.8, Bizon = the medium tank's) are the drawn turret's: the rules have no turret, as Zod's",
                measured = measured,
                toggle = "ZWorld.RamDuelDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; acts only with DV182 or DV183 on; the parity report's Zod rows and probes hold it off)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
