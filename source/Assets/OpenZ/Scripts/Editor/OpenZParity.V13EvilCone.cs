#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV167 "EvilZod's shotgun hits its whole cone" (the rule: Sim/ZEvilZod.Cone.cs). Measured headless on a plain map (Editor/OzV13EvilConeCount.cs
    /// Quick); the whole check: menu OpenZ/V1.3/EvilZod: Cone Count (headless, DV167). It does something only where an EvilZod fires: no level, no bot match has one, so
    /// the row changes no other row.</summary>
    public static partial class OpenZParity
    {
        static void AddEvilConeDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try { measured = OzV11Kit.VerdictCell(OzV13EvilConeCount.Quick(SettingsText), out pass) + " (the whole check: menu OpenZ/V1.3/EvilZod: Cone Count (headless, DV167))"; }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV167", change = "EvilZod's shotgun hits every enemy robot in the cone of its pellets, not only its target",
                zod = "-",
                openz = "With DV161. His shot hits its target as before and every other enemy robot on the ground in the cone of its pellets: ahead of him, its body within "
                        + ZEvilCone.TanPermille / 10.0 + " % of its distance along the line of the aim line (9 deg, a dash shot " + ZEvilCone.DashTanPermille / 10.0 + " %, 6 deg), "
                        + "in his range and line of fire, at most " + ZEvilCone.PastPx + " px farther than the target; each takes the shot's damage at its own distance (any robot within "
                        + ZEvilZod.FullBandPx + " px dies), the kill is his. Vehicles, guns and buildings as before.",
                measured = measured,
                toggle = "ZWorld.EvilConeDeviation (on; the game never sets it): nothing happens without an EvilZod, so 'Zod exact' (no DV161) and every level and bot match are as before",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
