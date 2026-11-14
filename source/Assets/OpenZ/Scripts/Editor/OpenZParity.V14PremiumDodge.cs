#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV173 PREMIUM DRIVER DODGE (version 1.4; the rule: Sim/ZObject.PremiumDodge.cs). Measured headless
    /// (Editor/OzV14PremiumDodgeCount.cs Quick, on a plain map); the whole check: menu OpenZ/V1.4/Premium Dodge: Count (headless, DV173). Only a
    /// tank or the mech with a non-grunt driver steps aside differently, so a bot match where no such driver ever sits in one is as before.</summary>
    public static partial class OpenZParity
    {
        static void AddPremiumDodgeDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV14PremiumDodgeCount.Quick(SettingsText);   // the counted checks' game switches, put back after
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.4/Premium Dodge: Count (headless, DV173))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV173", change = "PREMIUM DRIVER DODGE: a light, medium or heavy tank or the mech with a driver who is not a grunt steps aside from a shell a little faster",
                zod = "every unit steps aside from a shell or rocket fired at it at its own speed, whoever drives (DodgeMissile, zobject.cpp:3657-3714: the step's length = the time "
                      + "till the blast x the speed + a random 0..half of it, a random direction)",
                openz = $"a light / medium / heavy tank or the mech of a team whose driver is not a grunt (a psycho, sniper, tough, pyro, laser or Electrobot, e.g. put in with DV171's "
                        + $"EXIT DRIVER; a hero squad's Alan and Brad are grunts) moves x {ZWorld.PremiumDodgeSpeedMul:0.00} faster while it steps aside, and the step is planned with that "
                        + "speed (the same random draws in the same order); when the step ends the plain speed is back at once. The view turns its hull x 1.15 quicker while it steps "
                        + "aside (View/HullSteer.cs; the rules have no hull turn). Every other vehicle, every grunt-driven tank: as before. The computer's tanks get it when a non-grunt "
                        + "robot of the computer's climbs into one.",
                measured = measured,
                toggle = "ZWorld.PremiumDodgeDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off); ZWorld.PremiumDodgeSpeedMul (1.08); "
                         + "HullSteer.PremiumDodgeTurn (1.15, view)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
