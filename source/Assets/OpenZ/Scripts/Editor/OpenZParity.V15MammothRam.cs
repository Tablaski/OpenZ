#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV182 "the MAMMOTH RAM" (version 1.5, kit mammoth_ram; the rule: Sim/ZObject.MammothRam.cs). Measured headless
    /// (Editor/OzV15MammothRamCount.cs Quick, on plain maps); the whole check: menu OpenZ/V1.5/Mammoth Ram: Count (headless, DV182). The Mammoth is
    /// placed only by the Unit editor (DV180), so a level, a bot match and the parity's other probes are as before. The parity report's Zod rows and
    /// every other row's probe hold it off.</summary>
    public static partial class OpenZParity
    {
        static void AddMammothRamDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15MammothRamCount.Quick(SettingsText);   // the counted checks' game switches, put back after
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.5/Mammoth Ram: Count (headless, DV182))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV182", change = "the MAMMOTH RAM: a Mammoth rams an enemy vehicle close in front of its hull by itself (a short boost), the rammed vehicle flips over and is destroyed",
                zod = "Z has no ram and no Mammoth (DV180 is OpenZ's); vehicles never hurt vehicles by contact",
                openz = $"a crewed Mammoth sees an enemy crewed vehicle inside +/- {ZObject.RamStartArcDeg} deg of its hull's heading and at most {ZObject.RamStartGapPx} px from hull to hull, "
                        + $"the way drivable (dry free tiles, no bridge deck or one-lane chokepoint, no other unit but enemy robots): it drives straight at it, ACCELERATING from its speed (0 standing, x 1.00 moving) to x {ZObject.RamBoostPct / 100.0:0.00} over {ZObject.RamRampTicks} ticks in whole-percent steps, then holding; "
                        + $"front contact holds the target {ZObject.RamFlipTicks} ticks ({ZObject.RamFlipTicks * ZConst.TICK:0.00} s, the flip), then destroys it with its crew (the Mammoth's kill). "
                        + $"Two in front: the nearest, ties by object order; no second kill. It ends (no hit) when the target leaves the +/- {ZObject.RamKeepArcDeg} deg arc or {ZObject.RamKeepGapPx} px, "
                        + $"the way closes, {ZObject.RamMaxTicks} ticks pass, or a new order comes; then its order goes on from there. {ZObject.RamCoolTicks} ticks after a hit before the next. "
                        + "Robots are run over as before (DV65). Whole numbers, sim ticks and object order only, no random draw (deterministic)",
                measured = measured,
                toggle = "ZWorld.MammothRamDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; needs DV180's switch; the parity report's Zod rows and probes hold it off)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
