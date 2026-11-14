#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV156 "the medium tank's own reload" (1.3 owner item 14; the rule: Sim/ZMTank.cs ReloadFactor and the
    /// unit.mtank.attack_speed row of default_settings.txt). Measured here: the row against the rule (quick); the 1 vs 1 duels against the
    /// heavy and the light tank are the menu OpenZ/V1.3/Medium Tank reload: 1 vs 1 duels (Editor/OzV13MTankDuelCount.cs; minutes, so not
    /// run inside the parity report).</summary>
    public static partial class OpenZParity
    {
        static void AddMTankReloadDeviation(List<Deviation> list)
        {
            var s = ZSettings.Parse(SettingsText); var u = s.Unit(ZMTank.Type); var l = s.Unit(ZMTank.Gun);
            bool pass = u != null && l != null && Math.Abs(u.attack_speed - ZMTank.Reload(l.attack_speed)) < 1e-9;
            list.Add(new Deviation
            {
                id = "DV156", change = "The medium tank reloads 15 % slower than the light tank (its gun is otherwise the light tank's)",
                zod = "Z's medium tank slot is OpenZ's walking Mech (type medium; DV145 adds the tank 'mtank'); in Z each heavier tank reloads slower (Z-Wiki: light 1.1 s, medium 1.65 s, heavy 2.3 s)",
                openz = $"unit.{ZMTank.Type}.attack_speed = the light tank's {l?.attack_speed:0.###} s x {ZMTank.ReloadFactor:0.00} = {u?.attack_speed:0.####} s (1.2: the light tank's reload exactly); " +
                        "damage, range, blast, shell speed, hit and snipe chances stay the light tank's; the light tank, the mech and the heavy tank unchanged. Deterministic: a fixed number",
                measured = (pass ? "the row = the rule" : "the row differs from the rule") + " (the duels: menu OpenZ/V1.3/Medium Tank reload: 1 vs 1 duels, counted (headless, DV156))",
                toggle = "none: a number of the medium tank's own row (the medium tank itself is DV145's switch, off in 'Zod exact'); the other choice, 10 % slower: ZMTank.ReloadFactor 1.10 and the row 1.240800",
                status = pass ? DevActive : DevCheck,
            });
        }
    }
}
#endif
