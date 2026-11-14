#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// the rule: Sim/ZGatlingBuff.cs). Measured headless (Editor/OzV14GatlingBuffCount.cs Quick); the whole check: menu OpenZ/V1.4/Gatling
    /// Buff: Count (headless, DV170). The parity's Zod rows and the block's other probes run with the switch held off (ZWorld.GatlingBuffOff).</summary>
    public static partial class OpenZParity
    {
        static void AddGatlingBuffDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try { measured = OzV11Kit.VerdictCell(OzV14GatlingBuffCount.Quick(SettingsText), out pass) + " (the whole check: menu OpenZ/V1.4/Gatling Buff: Count (headless, DV170))"; }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV170", change = "The GATLING reaches 10 % farther (16.5 m instead of 15 m) and each of its shots hits 15 % harder",
                zod = "unit.gatling.attack_radius=120 (15 m), attack_damage=0.003976 = 39 HP a hit (default_settings.txt; zsettings.cpp:203-214, zobject.cpp:181-183); reload 0.1 s, hit 0.65, snipe 0.4",
                openz = $"every parse of the game's settings gives the gatling range x 1.10 in whole px ({OzV14GatlingBuffCount.ZodRadius} -> {ZGatlingBuff.Radius(OzV14GatlingBuffCount.ZodRadius)} px) and damage per shot "
                        + $"x {ZGatlingBuff.DamageFactor:0.00} (0.003976 -> {ZGatlingBuff.DamagePerShot(OzV14GatlingBuffCount.ZodDamage).ToString("R", System.Globalization.CultureInfo.InvariantCulture)} = 45 HP a hit): every gatling built, placed, "
                        + "found empty on a map and crewed, the player's and the computer's (its reach checks read the gun's own range), the Unit editor's OpenZ set and its playground. "
                        + "Only the gatling: fire rate, hit and snipe chance, health and build time stay Zod's; the jeep, the Electrobot's tesla gatling and every other unit keep their rows. "
                        + "Deterministic: an integer range and one fixed factor at parse time",
                measured = measured,
                toggle = "ZWorld.GatlingBuffDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off: Zod's gatling)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
