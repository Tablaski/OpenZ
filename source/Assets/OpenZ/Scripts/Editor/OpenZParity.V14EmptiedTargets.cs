#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV176 "nobody keeps shooting a vehicle just emptied" (version 1.4; the rule: Sim/ZWorld.DropEmptied.cs).
    /// Measured headless (Editor/OzV14EmptiedTargetsCount.cs Quick, on a plain map); the whole check: menu OpenZ/V1.4/Vehicle Exit Gun: Emptied
    /// Targets Count (headless, DV176). The parity report's Zod rows switch it off (Editor/OpenZParity.cs).</summary>
    public static partial class OpenZParity
    {
        static void AddEmptiedTargetsDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV14EmptiedTargetsCount.Quick(SettingsText) + OzV14U2FollowupsCount.Quick(SettingsText, MapText("L16"));   // the counted checks' game switches, put back after; u2_followups: + the fort / heavy / shuttle / gun rows (Editor/OzV14U2FollowupsCount.cs)
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.4/Vehicle Exit Gun: Emptied Targets Count (headless, DV176))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV176", change = "nobody keeps shooting a vehicle that was just emptied (EXIT DRIVER or its crew shot out): every unit of every team drops it as its target and every attack order on it in that tick",
                zod = "a crew SHOT OUT (zserver.cpp:1849-1870): units whose target it is disengage and the FIRST waypoint of every unit is dropped when it attacks it; an attack queued further back "
                      + "stays and is carried out later on the empty vehicle (DV81 drops those for human teams only); EXIT DRIVER is OpenZ's (DV171): before DV176 it left every other unit's "
                      + "target and attack orders on the emptied tank or mech, so an enemy jeep or squad went on shelling the empty hull",
                openz = "in the tick a vehicle is emptied, by EXIT DRIVER or by its crew shot out, every unit of every team (the computer's too) whose target it is disengages and every attack / "
                        + "agro waypoint on it is taken out of its list, the first and every queued one (a unit whose first waypoint went stops its move, as Zod's loop does); the shot-out path "
                        + "keeps Zod's loop for the target and the first waypoint and adds the computer's queued ones. Rounds already in the air may still land. Nothing picks the null team "
                        + "by itself afterwards, and an explicit Ctrl attack order given after the emptying is carried out (DV81). In the computer's game: its unit attacking a vehicle whose "
                        + "driver the player lets out stops at once; its attack queued behind another order on a vehicle whose crew was shot out is no longer carried out. Integers and the "
                        + "world's own object order only (deterministic). Since kit u2_followups also: a fort gun / mortar committed to it and a heavy tank's "
                        + "loaded second barrel at it drop it in that tick, and the other two emptying paths do the same (a hero pilot's shuttle EXIT, a gun's EJECT)",
                measured = measured,
                toggle = "ZWorld.EmptiedTargetsDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows switch it off)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
