#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV161 "EvilZod" (the rule: Sim/ZEvilZod.cs). Measured headless by one dash on a plain map (Editor/OzV13EvilZodCount.cs Quick) and, 1.4,
    /// the auto dash's probe (Editor/OzV14EvilDashCount.cs Quick);
    /// the whole check: menu OpenZ/V1.3/EvilZod: Rules Count (headless, DV161). No level holds him, so the row changes no other row.</summary>
    public static partial class OpenZParity
    {
        static void AddEvilZodDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try { measured = OzV11Kit.VerdictCell(OzV13EvilZodCount.Quick(SettingsText), out pass) + " (the whole check: menu OpenZ/V1.3/EvilZod: Rules Count (headless, DV161))"; }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            try { measured += "; 1.4 cooldown and auto dash: " + OzV11Kit.VerdictCell(OzV14EvilDashCount.Quick(SettingsText), out bool autoPass) + " (the whole check: menu " + OzV14EvilDashCount.MenuPath + ")"; pass &= autoPass; }
            catch (Exception ex) { measured += "; the auto dash probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV161", change = "EvilZod: a hero robot for the Unit editor and the testing panel, in no level",
                zod = "-",
                openz = $"A squad of one, not built by anyone, placed on Blue only. {ZEvilZod.Type}: the heavy tank's hit points and walk, the grunt's range, no running. Shot every {ZEvilZod.CycleTicks / 100} s, no roll, "
                        + $"robots {ZEvilZod.RobotNearHp} HP up to {ZEvilZod.FullBandPx} px falling to {ZEvilZod.RobotFarHp}, vehicles and guns {ZEvilZod.HullNearHp} to {ZEvilZod.HullFarHp}. "
                        + $"Power dash on a given attack order, in a clear line, from {ZEvilZod.FullBandPx} to {ZEvilZod.DashRangePx} px, cooldown {ZEvilZod.CooldownTicks * ZConst.TICK:0.0#} s, a veteran {ZEvilZod.VeteranCooldownTicks * ZConst.TICK:0.0#} s. "
                        + $"1.4 auto dash: the dash ready, hit by an enemy in the last {ZEvilAutoDash.UnderFireSec:0} s and no enemy in his range, he dashes by himself at the nearest enemy he can attack "
                        + $"inside the dash range (the ordered dash's line and landing rules; never with a player order running, holding position, or guarding the fort as its last defense). "
                        + $"Heals {ZEvilZod.HealPctPerMin} % a minute, a veteran {ZEvilZod.VeteranHealPctPerMin} %. No vehicle, gun or APC; his own crewed shuttle as a passenger. No dodge, headshot, run-over or knockdown.",
                measured = measured,
                toggle = "ZWorld.EvilZodDeviation: View/ZGame.cs sets it at every start, off in 'Zod exact' (oz_zod_exact = 1); the 1.4 auto dash: ZWorld.EvilAutoDashDeviation (on, the game never sets it)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
