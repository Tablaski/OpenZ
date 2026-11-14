#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV136 "the computer defends its frontier" (1.2 items 14 + 14a; the rule: Sim/ZBot.CpuDefend.cs). Measured headless
    /// by the kit's counted check (Editor/OzV12CpuDefendCount.cs) in its quick form: level 17, the computer against the stand-in player, one seed, 14 minutes, the rule
    /// off and on, the switches of a game in Play; the switch is put back afterwards (the whole check, three levels, both sides: menu
    /// OpenZ/V1.2/CPU: 14 Defend Orders Check (headless)).</summary>
    public static partial class OpenZParity
    {
        static void AddCpuDefendDeviation(List<Deviation> list, Func<string, int, int> getInt)
        {
            string measured; bool pass;
            bool was = ZWorld.CpuDefendDeviation;
            try
            {
                string check = OzV12CpuDefendCount.Quick(SettingsText, MapText);
                measured = OzV11Kit.VerdictCell(check, out pass) + " (the whole check, three levels and both sides: menu OpenZ/V1.2/CPU: 14 Defend Orders Check (headless))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            finally { ZWorld.CpuDefendDeviation = was; }
            bool on = getInt("oz_zod_exact", 0) == 0;
            list.Add(new Deviation
            {
                id = "DV136", change = "The computer defends its frontier: from minute 8 one in four of its attack / capture orders sends the unit to guard a frontier flag for 2 minutes",
                zod = "every order of the engine's computer attacks: a foreign flag, an enemy unit or the enemy fort (zbot.cpp:127-414); it never stands guard",
                openz = $"from {ZWorld.CpuDefendStartSec / 60:0}:00 of the game clock (before that every order is the engine's) one in {ZWorld.CpuDefendEvery} of the orders the computer's own order path gives to attack, " +
                        "enter an enemy fort or take a foreign flag (its rounds, the idle groups DV56, the Electrobot's escort; not the fort finish DV60 nor a strike group's release DV107), the place in each block of four " +
                        "drawn from a hash of the game tick (no random stream of the game), becomes a DEFEND order: the unit (a squad leader or an armed vehicle; not the Electrobot, a shuttle, a crane, a strike group member) goes to " +
                        $"the nearest frontier flag it reaches (a flag of its own whose territory touches one another team holds; at most {ZWorld.CpuDefendPerFlag} guards a flag), to a spot of its own " +
                        $"{ZWorld.CpuDefendNearPx}-{ZWorld.CpuDefendFarPx} px from the flag on the side facing the enemy, in its own territory, on dry ground, never on a flag, {ZWorld.CpuDefendPitchPx} px from every other guard, " +
                        $"cover and a clear line to the border preferred; it walks there with the last {ZWorld.CpuDefendBackPx} px toward the enemy (so it stands facing it), holds position {ZWorld.CpuDefendHoldSec / 60:0} minutes " +
                        "(fires at what comes into its range, no chase), left alone by the order rounds, DV56, DV60, DV70 and the hatch rest; released at the end, when its flag is lost or when it cannot reach its spot, " +
                        "with an order of the computer's own kind at once. When the drawn order cannot defend (no frontier flag, no free spot), it stays the engine's and the turn passes to the next. Normal and Easy. Deterministic: game time and ticks",
                measured = measured,
                toggle = $"ZWorld.CpuDefendDeviation: the game sets it at every start, off in 'Zod exact' (oz_zod_exact = {(on ? "0" : "1")}); held off in the parity rows, the counted checks and the computer's own checks",
                status = !pass ? DevCheck : on ? DevActive : DevActive + " (off by pref)",
            });
        }
    }
}
#endif
