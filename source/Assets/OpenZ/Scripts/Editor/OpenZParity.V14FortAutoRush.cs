#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV168 "a player's idle robot near an enemy fort's gate runs in" (version 1.4; the rule: Sim/ZWorld.FortAutoRush.cs). Measured
    /// headless on level 2 (Editor/OzV14FortRushCount.cs Quick); the whole check: menu OpenZ/V1.4/Fort: Auto Rush Count (headless, DV168). It acts only for a team
    /// with no CPU: a bot match and the parity's other probes (the switch held off while they run) are as before.</summary>
    public static partial class OpenZParity
    {
        static void AddFortAutoRushDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try { measured = OzV11Kit.VerdictCell(OzV14FortRushCount.Quick(SettingsText, OzV11Kit.MapText("L02")), out pass) + " (the whole check: menu OpenZ/V1.4/Fort: Auto Rush Count (headless, DV168))"; }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV168", change = "The run into the enemy fort from near its gate: a player's robot standing near an enemy fort's gate without an order of the player runs in by itself",
                zod = "a fort falls only to an ENTER_FORT order (zobject.cpp:2774-2913); a robot left standing in front of the enemy gate stands there",
                openz = $"Every {ZWorld.FortAutoRushEveryTicks} ticks, a squad leader of a team with no CPU (it can move, not stunned, not on patrol or HOLD POSITION, not a roof defender) with no "
                        + $"order of the player in its list, standing within {ZWorld.FortAutoRushPx} px (4 tiles) of a living enemy fort's gate exit point, with Zod's straight robot walk free "
                        + "from it to that point and on through the gate, and the gate clear (no living enemy robot, vehicle or ground gun within "
                        + $"{ZWorld.FortAutoRushClearPx} px of that point; the roof defender, the tower guns, units inside a building and a crane at work do not count), "
                        + "gets the player's own gate order (ENTER_FORT through SEND_WAYPOINTS); DV34 / DV102 run it in, the fort falls as "
                        + $"with any ordered entry. A robot it sent is left alone {ZWorld.FortAutoRushRetryTicks} ticks. Robots only; the computer's teams never (their storm is DV60's).",
                measured = measured,
                toggle = "ZWorld.FortAutoRushDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; a bot match has no player's team)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
