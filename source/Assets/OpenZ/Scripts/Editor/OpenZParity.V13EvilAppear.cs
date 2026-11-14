#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV165 "EvilZod in the campaign levels" (the rule: Sim/ZEvilZod.Appear.cs). Measured headless on a plain map with two forts
    /// (Editor/OzV13EvilAppearCount.cs Quick); the whole check: menu OpenZ/V1.3/EvilZod: Appearances Count (headless, DV165). Nothing runs in a world without a
    /// last-defense team and without the retry call, so the row changes no other row.</summary>
    public static partial class OpenZParity
    {
        static void AddEvilAppearDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try { measured = OzV11Kit.VerdictCell(OzV13EvilAppearCount.Quick(SettingsText), out pass) + " (the whole check: menu OpenZ/V1.3/EvilZod: Appearances Count (headless, DV165))"; }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV165", change = "EvilZod in the campaign levels: a blue player's retry help, blue's last defense against a red player",
                zod = "-",
                openz = "Levels 2, 7, 16, 17, a human against the computer, with DV161. A blue player's start right after a defeat (the lose screen's REPLAY or RETRY): EvilZod "
                        + "stands in front of the blue fort near its doors, the player's own unit. A red player: from " + ZEvilAppear.LastDefenseTicks / 6000 + ":00, when blue owns no sector "
                        + "but its fort's, the blue fort sends EvilZod out of its doors once a game (a dead one is never replaced); he guards a spot " + ZEvilAppear.SpotOutPx + " px out of "
                        + "the exit point and " + ZEvilAppear.SpotSidePx + " px to its side (outside every exit lane, " + ZEvilAppear.FlagPx + " px from flags, clear of every object), "
                        + "holds it (no chase, no flag, no vehicle, no order), fires at what comes into range, steps up to " + ZEvilAppear.StepPx + " px toward a target just out of range "
                        + "and walks back; a traffic step aside never takes him farther from his spot than that (1.3 row 82). On his walk out, past the door's lane, an enemy in his "
                        + "range stops the walk: he fights there, then walks to his spot (1.3 final). Walking home from in front of his spot (the outward side along the "
                        + "door axis) he always goes by the point behind it first, so his last step points out of the door (1.4, row G5).",
                measured = measured,
                toggle = "View/ZGame.cs at every start: ZWorld.EvilLastDefenseTeam (Blue for a red player) and ZWorld.AddEvilRetryHelp (the lose screen's one-shot flag, UI/OzUi.cs "
                         + "OzScenes.RestartAfterLoss); both need ZWorld.EvilZodDeviation (off in 'Zod exact'); never in a bot match, a headless check or the Unit editor",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
