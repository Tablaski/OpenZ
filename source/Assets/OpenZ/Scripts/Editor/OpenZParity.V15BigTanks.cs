#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations rows DV180 "the MAMMOTH" and DV181 "the BIZON" (version 1.5, kit new_tanks; the rules: Sim/ZBigTanks.cs). Measured
    /// headless (Editor/OzV15BigTanksCount.cs Quick); the whole check: menu OpenZ/1.5/Big Tanks/4 Rules check (headless). Both are placed only by the
    /// Unit editor (no level, factory, build list or computer build), so a bot match and the parity's other probes are as before; the report's Zod
    /// rows switch both off.</summary>
    public static partial class OpenZParity
    {
        static void AddBigTanksDeviations(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15BigTanksCount.Quick(SettingsText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/1.5/Big Tanks/4 Rules check (headless))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV180", change = "the MAMMOTH, a new big tank (Unit editor only): the heavy tank 1.2 times the size with two barrels",
                zod = "Z has no such tank",
                openz = $"its row from the heavy tank's: health x {ZBigTanks.MammothHealthFactor:0.00}, speed x {ZBigTanks.MammothSpeedFactor:0.00} to the whole number, range x {ZBigTanks.MammothRangeFactor:0.00}, "
                        + $"shell speed x {ZBigTanks.MammothShellFactor:0.00}, reload x {ZBigTanks.MammothReloadFactor:0.00}, the heavy's damage and blast in two shells one-two (DV30's {ZWorld.HeavyPairGap:0.00} s); "
                        + $"a psycho driver; the heavy's class flags and driver rules; {ZBigTanks.MammothRadiusPx} px room. Placed only by the Unit editor; doubles and whole numbers, no random draw of its own (deterministic)",
                measured = measured,
                toggle = "ZWorld.MammothDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows switch it off)",
                status = !pass ? DevCheck : DevActive,
            });
            list.Add(new Deviation
            {
                id = "DV181", change = "the BIZON, a new big tank (Unit editor only): a tractor-like tank 1.3 times the heavy tank's size that never backs off",
                zod = "Z has no such tank",
                openz = $"its row: the heavy tank's health x {ZBigTanks.BizonHealthFactor:0.00} and speed x {ZBigTanks.BizonSpeedFactor:0.00} (whole number), the light tank's range, the medium tank's shot, "
                        + $"the medium tank's reload x {ZBigTanks.BizonReloadFactor:0.0}, one barrel; it never steps aside from a shell and never duels (it drives straight at its target until it is in range and fires "
                        + $"from there); a psycho driver; the heavy's class flags and driver rules; {ZBigTanks.BizonRadiusPx} px room. Placed only by the Unit editor; deterministic",
                measured = measured,
                toggle = "ZWorld.BizonDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows switch it off)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
