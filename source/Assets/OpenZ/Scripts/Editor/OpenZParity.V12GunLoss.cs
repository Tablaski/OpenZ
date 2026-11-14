#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV149 "the mech loses its hand gun" (1.2 owner item 189; the rule: Sim/ZObject.MechGunLoss.cs). Measured
    /// headless: the counted check of Editor/OzMechGunLossCount.cs (blast by blast: heavy hits under 20 % only, the 50 % chance, small arms
    /// never; the gate; repair on the pad and Zod's swap; capture; the switches, the rand() stream, determinism; its balance table is in the
    /// menu's report).</summary>
    public static partial class OpenZParity
    {
        static void AddGunLossDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            bool was = ZWorld.MechGunLossDeviation, wasHg = ZWorld.MechHandGunDeviation;
            try
            {
                string check;
                using (OzV11Kit.GameSwitches()) check = OzMechGunLossCount.Run(SettingsText, 4);
                measured = OzV11Kit.VerdictCell(check, out pass) + " (the whole report: menu OpenZ/Mech v2/22 Gun loss: counted rules check)";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            finally { ZWorld.MechGunLossDeviation = was; ZWorld.MechHandGunDeviation = wasHg; }
            var s = ZSettings.Parse(SettingsText); var l = s.Unit(ZWorld.GunLossHeavyRef);
            int heavy = l != null && l.Damage > 0 ? Math.Max(1, (int)Math.Round(l.Damage * ZWorld.GunLossHeavyShare)) : ZWorld.GunLossHeavyFallback;
            list.Add(new Deviation
            {
                id = "DV149", change = "A badly damaged mech can lose its hand gun to a heavy hit, for good",
                zod = "Z's medium tank (OpenZ's mech) has one weapon and never loses it; OpenZ's DV146 gave it the hand gun",
                openz = $"a blast (any shell, rocket, missile, mortar round or grenade) that takes at least {heavy} HP off the mech ({ZWorld.GunLossHeavyShare:0.##} of the {ZWorld.GunLossHeavyRef} tank's shell) " +
                        $"and leaves it alive under {ZWorld.GunLossHpShare * 100:0} % of its health: one draw on Zod's rand() stream, {ZWorld.GunLossChance * 100:0} % to lose the gun (small arms, flames, the laser, zaps never). " +
                        "Without it the mech never fires the hand gun again (the computer's too); repair (the pad, Zod's swap), capture and new crews do not give it back; the rockets fire as before. " +
                        "Player's and computer's mechs alike. Deterministic: one draw per such hit, none otherwise. View: the gun's forearm falls off as a wreck piece",
                measured = measured,
                toggle = "ZWorld.MechGunLossDeviation (code switch; ZGame: with DV146, off in 'Zod exact'; headless default off)",
                status = pass ? DevActive : DevCheck,
            });
        }
    }
}
#endif
