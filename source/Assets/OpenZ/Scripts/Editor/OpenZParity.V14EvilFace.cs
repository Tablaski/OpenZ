#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV174 "EvilZod turns onto his target before he fires" (version 1.4; the rule: Sim/ZEvilZod.Face.cs; the view's gate:
    /// View/OzEvilZodFx.cs). Measured headless (Editor/OzV14EvilFaceCount.cs Quick, the plain-map scenes, the rule off and on); the whole check: menu
    /// OpenZ/V1.4/EvilZod: Face Count (headless, DV174). Only EvilZod (DV161) has the rule, and no level or bot match has him, so a bot match is as before.</summary>
    public static partial class OpenZParity
    {
        static void AddEvilFaceDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV14EvilFaceCount.Quick(SettingsText, OzV11Kit.MapText);   // the counted checks' game switches, put back after
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu " + OzV14EvilFaceCount.MenuPath + ")";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV174", change = "EvilZod turns onto his target before he fires (his shot waits for his turn)",
                zod = "no unit's shot reads a direction: the server fires on its cooldown (zobject.cpp:2102-2213) and the client snaps the robot's sprite to the target "
                      + "(zrobot.cpp:139-143); EvilZod is OpenZ's own hero (DV161), so Zod has no row for him",
                openz = $"the rules keep EvilZod's heading (whole hundredths of a degree) and turn it {ZEvilFace.TurnCentiPerTick / 100.0:0.0} deg a tick = "
                        + $"{ZEvilFace.TurnCentiPerTick} deg/s toward his dash line, his attack target, else the way he walks (the robots' body turn of the view, View/UnitAim.cs); "
                        + $"his shot, ready and in range, waits until that heading is within {ZEvilFace.TolCenti / 100} deg of the target (the robots' aim tolerance of the view), "
                        + "at most the turn itself (180 deg = 0.24 s). Before DV174 he fired on the tick a target was picked, whatever way he faced, and his view drew the volley "
                        + $"within 0.1 s. The view now draws a volley only once his drawn gun points within {OzEvilZodFx.FaceTolDeg:0} deg of the target (at most "
                        + $"{OzEvilZodFx.FaceWaitMax:0.00} s after the rules' shot), and his drawn body turns onto the target at the rules' {UnitView.EvilFaceTurnDegPerSec:0} deg/s "
                        + "right after a stop (the legs' walk tail) and while a volley waits (View/UnitView.EvilFace.cs; the robots' shared UnitAim turn is unchanged). "
                        + "Measured off Unity (the kit's proof, 6 Nov 2026): before, 99 of 869 shots more than 10 deg off his heading (the worst 180 deg); after, 0 of 855. "
                        + "Damage, range, the reload cycle, the cone (DV167) and the dash: as before. Not followed by the rules' heading (the view's gate covers the drawing): "
                        + "the ramp walk out of a vehicle (ExitWalkHold skips his step), a new EvilZod object after a shuttle ride (south again), the view's early turn to a "
                        + "target about to come in range (UnitAim.Predict).",
                measured = measured,
                toggle = "ZWorld.EvilFaceFireDeviation (code switch, on; the game never sets it: without an EvilZod nothing runs, and DV161 has him only outside 'Zod exact'); "
                         + "OzEvilZodFx.FaceTolDeg / FaceWaitMax (view)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
