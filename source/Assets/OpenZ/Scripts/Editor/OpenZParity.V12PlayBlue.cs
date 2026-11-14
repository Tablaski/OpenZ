#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV140 "EXPERIMENTAL PLAY AS BLUE" (1.2 item 11; the sides: View/OzSides.cs, the mode: View/OzPlayBlue.cs, the
    /// box: UI/OpenZMainMenu.PlayBlue.cs). Measured headless: the counted check of Editor/OzV12PlayBlueCount.cs without its whole matches (the
    /// side rules; per level the CPU list, the human team, the build-list bonus, HEAD START at the blue fort, own orders only, the start
    /// shuttles, the fort falls both ways, EASY). The whole check with the full matches and the off-state fingerprints: its menu.</summary>
    public static partial class OpenZParity
    {
        static void AddPlayBlueDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                measured = OzV11Kit.VerdictCell(OzV12PlayBlueCount.Check(SettingsText, MapText, true), out pass)
                           + " (the whole check with full matches and the off-state fingerprints: menu OpenZ/V1.2/Play As Blue: 11 Sides Check)";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV140",
                change = "PLAY AS BLUE (GENERAL EVILZOD): the player may command the blue team from the blue fort while the CPU commands red (picked on the faction screen before the level selection; GENERAL ZOD, red, is the default)",
                zod = "every client picks a team; the single player game of Z is always red against the blue CPU (OpenZ before 1.2: always red)",
                openz = "the box armed at DEPLOY: ZGame's player = blue, the CPU drives red with the same difficulty rule (the chosen one against the human), blue gets the build-list bonus and HEAD START "
                        + "at the blue fort; the map is not mirrored, the camera starts over the blue fort; everything that follows the player follows blue (selection, orders, HUD, minimap, "
                        + "announcer, end screen); the colour option and red's story scenes (Commander Zod's briefing, flyover and end scenes, the level 16 lounger ending) are off; the SHUTTLE "
                        + "box's ship stands by the blue fort on levels 16 / 17 (DV144); its victories enter the high scores as red ones do, each row tagged EVILZOD. The rules of the simulation are the same for both teams (nothing changes there)",
                measured = measured,
                toggle = "the faction screen before the level selection: GENERAL ZOD = red, GENERAL EVILZOD = blue (oz_play_blue, default red), read at DEPLOY; the blue side not offered in 'Zod exact'; never in a bot match or a level opened without DEPLOY",
                status = pass ? DevActive : DevCheck,
            });
        }
    }
}
#endif
