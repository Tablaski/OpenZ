#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV178 SPRINT ORDER (version 1.5, kit fps_feel; the rule: Sim/ZObject.SprintOrder.cs). Measured headless
    /// (Editor/OzV15SprintOrderCount.cs Quick, on a plain map); the whole check: menu OpenZ/V1.5/Sprint Order: Count (headless, DV178). Only an
    /// order carrying the sprint mark moves faster, and only the private test view sends one, so a bot match, every level and every player
    /// order are as before.</summary>
    public static partial class OpenZParity
    {
        static void AddSprintOrderDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15SprintOrderCount.Quick(SettingsText);   // the counted checks' game switches, put back after
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.5/Sprint Order: Count (headless, DV178))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV178", change = "SPRINT ORDER: a move order carrying the sprint mark moves the unit faster (sent only by the private test view, never by a player order or the computer)",
                zod = "a unit moves at its own speed (move_speed x its ground x its damage x Zod's robot run), whoever gives the order",
                openz = $"a MOVE waypoint with the sprint mark moves the unit x {ZWorld.SprintPct / 100.0:0.00} while it is the unit's first waypoint (after every other speed factor); "
                        + "the plain speed is back on the first step of any other waypoint; a squad's members carry the mark with the leader's waypoints. No order of the game's "
                        + "controls, no computer player and no menu sets the mark, so every match runs exactly as before.",
                measured = measured,
                toggle = "ZWorld.SprintOrderDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off); ZWorld.SprintPct (150)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
