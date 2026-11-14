#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV175 MAYHEM: THE COMPUTER BUILDS 10 % FASTER (version 1.4; the rule: Sim/ZWorld.CpuBuild.cs, named by
    /// View/ZGame.cs ApplyTechBonus). Measured headless (Editor/OzV14MayhemBuildCount.cs Quick, level 2); the whole check: menu
    /// OpenZ/V1.4/Mayhem Build: Count (headless, DV175). The parity rows and every bot match name no faster side: Zod's build times there.</summary>
    public static partial class OpenZParity
    {
        static void AddMayhemBuildDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV14MayhemBuildCount.Quick(SettingsText, MapText("L02"));   // the counted checks' game switches, put back after
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.4/Mayhem Build: Count (headless, DV175))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV175", change = "MAYHEM: the computer builds every unit and gun in 90 % of the time the player's side takes for it",
                zod = "both sides build at the same speed: T = the unit's build_time x (1 - 0.5 x the share of flags owned) x (1 + 1.25 x the building's damage share), "
                      + "counted from the start of the item (zbuilding.cpp:633-667); no difficulty changes a build time",
                openz = $"on MAYHEM, in a match of a human against the computer (red or blue: PLAY AS BLUE too), every item of the computer's fort and factories takes "
                        + $"{ZWorld.CpuBuildFasterScale:0.0} x Zod's time of the same item on the player's side (the same flags and damage), e.g. a grunt 72 s -> 64.8 s, a jeep "
                        + "81 s -> 72.9 s, a heavy tank 309 s -> 278.1 s with no flags and no damage; the item is done on the first 100 Hz tick at or after its time (the "
                        + "game's rounding, the same for both sides), so 0.9 x the player's ticks to within one tick; a hit or a flag re-times it as before, from its start; "
                        + "a factory that changes hands takes its new owner's time at once. The player's own times, EASY / MEDIUM / HARD, a computer-only match, the "
                        + "parity rows and the gate battles: Zod's times bit for bit.",
                measured = measured,
                toggle = "View/ZGame.cs ApplyTechBonus -> ZWorld.SetCpuBuildFaster(IsMayhem, player, blueBot, redBot) (at the start and on a live change of the "
                         + "difficulty; headless nobody: off); ZWorld.CpuBuildFasterScale (0.9)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
