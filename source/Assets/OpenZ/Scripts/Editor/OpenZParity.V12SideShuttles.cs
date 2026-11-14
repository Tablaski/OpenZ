#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV144 "the SHUTTLE box for both sides" (1.2 item 111; the rule: Sim/ZWorld.SideShuttles.cs, on top of DV118,
    /// K256f's empty shuttle, DV134 and DV140). Measured headless: the counted check of Editor/OzV12SideShuttleCount.cs without its matches
    /// (every start of levels 2 / 16 / 17 for both sides, every difficulty, the box and HEAD START on and off; red's own game unchanged; the
    /// switch off; 'Zod exact').</summary>
    public static partial class OpenZParity
    {
        static void AddSideShuttlesDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            bool was = ZWorld.SideShuttlesDeviation;
            try
            {
                string check;
                using (OzV11Kit.GameSwitches()) check = OzV12SideShuttleCount.Check(SettingsText, MapText, OzL17ShuttleCount.EditorKeepOut(), null, true);
                measured = OzV11Kit.VerdictCell(check, out pass) + " (the whole report: menu OpenZ/V1.2/Shuttle: 111 Both Sides Check (headless))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            finally { ZWorld.SideShuttlesDeviation = was; }
            var b16 = ZWorld.L16BlueFortShuttleSpot; var b17 = ZWorld.L17BlueFortShuttleSpot; var cp = ZWorld.L17BlueCarParkShuttleSpot;
            list.Add(new Deviation
            {
                id = "DV144", change = "The SHUTTLE box works for the blue side too (the player's ship by the blue fort), level 17's car park has an empty shuttle on each side, and the HEAD START never parks on or beside a shuttle",
                zod = "no shuttle in Zod (DV118 is an OpenZ unit); before: the box was locked while playing blue (its ships belong to the red fort), level 17 had one empty car-park shuttle (the red side's), the head start looked only at the shuttle's 2 x 2 tile box",
                openz = $"at map load, right after DV134's ships (every other object, DV66 move and id unchanged): with the player blue and the box asking (levels 16 / 17, EASY / MEDIUM, the box on) his crewed ship stands by the BLUE fort, " +
                        $"level 16 on tile {b16[0]},{b16[1]} facing {b16[2]} deg (by the blue fort's south-east corner on flat ground, the ramp toward the gate), level 17 on tile {b17[0]},{b17[1]} facing {b17[2]} deg (the pocket west of the blue fort, the ramp toward the gate side); " +
                        $"the red fort's spots stay empty; level 17's second EMPTY ship on tile {cp[0]},{cp[1]} facing {cp[2]} deg (the west car park's first row, the mirror of DV134's) on every start, whichever side; " +
                        "the head start refuses a parking box nearer than 4 m to a shuttle's outline or 0.5 m to its ramp lane. Each spot is checked with the landing spot rule on its exact heading and the drawn props the rules know; " +
                        "a changed map would use the nearest free tile within 3 (counted). The computer gets no start shuttle (the box is the player's). Deterministic: fixed places, no random draw",
                measured = measured,
                toggle = "ZWorld.SideShuttlesDeviation (code switch; ZGame: on, off in 'Zod exact'; headless default off) with the battlefield screen's SHUTTLE box (oz_start_shuttle) and the side (oz_play_blue); needs DV118, the car-park ship DV134",
                status = pass ? DevActive : DevCheck,
            });
        }
    }
}
#endif
