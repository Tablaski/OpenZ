#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using System.Linq;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations rows DV147 "the mech's health x 0.95" (1.2 owner item 179; the rule: Sim/ZMechHp.cs) and DV148 "the computer
    /// builds mechs and medium tanks" (1.2 owner item 180; Sim/ZBot.MechMix.cs). Measured headless by one run of the counted check
    /// Editor/OzV12MechHpCount.cs (the rules, placed and built mechs, the Unit editor's sets; the computer on levels 16 and 17, three
    /// 20-minute CPU-vs-CPU games each with DV148): the lines before "## the computer" are DV147's, the rest DV148's.</summary>
    public static partial class OpenZParity
    {
        static void AddMechHpDeviations(List<Deviation> list)
        {
            string report = null, error = null;
            bool hp = ZWorld.MechHpDeviation, mix = ZWorld.MechMixDeviation;
            try
            {
                var ta = UnityEngine.Resources.Load<UnityEngine.TextAsset>(OzUeSetData.ResourcePath);
                using (OzV11Kit.GameSwitches()) report = OzV12MechHpCount.Run(SettingsText, MapText, ta ? ta.text : null, 1200, new[] { 1, 2, 3 }, null, false);   // DV148 alone here (the menu also plays the old rule for comparison)
            }
            catch (Exception ex) { error = "the probe threw " + ex.GetType().Name + ": " + ex.Message; }
            finally { ZWorld.MechHpDeviation = hp; ZWorld.MechMixDeviation = mix; }
            string head = report ?? "", cpu = "";
            int k = head.IndexOf(OzV12MechHpCount.CpuHeading, StringComparison.Ordinal);
            if (k >= 0) { cpu = head.Substring(k); head = head.Substring(0, k); }
            bool p1 = false, p2 = false;
            string m1 = error ?? OzV11Kit.VerdictCell(head, out p1), m2 = error ?? OzV11Kit.VerdictCell(cpu, out p2);
            var s = ZSettings.Parse(SettingsText);
            list.Add(new Deviation
            {
                id = "DV147", change = "The mech has 5 % less health (it carries a second weapon, the hand gun of DV146): 6418 HP instead of 6756",
                zod = $"Z's medium tank (OpenZ's mech, type {ZMechHp.Type}) has health 0.675676 = 6756 HP (default_settings.txt, kept as Zod's)",
                openz = $"every parse of the game's settings gives the mech health x {ZMechHp.Factor:0.##} rounded to six places (0.641892 = 6418 HP): every mech made, built, captured or repaired, " +
                        "the player's and the computer's, the Unit editor's OpenZ set (its default stats; a Custom value reset to OpenZ's), a Custom level's rows; a Custom value of the player's stays his. " +
                        $"Only the mech: the APC and the missile launcher keep Zod's 6756 HP, the medium tank its own {s.Unit(ZMTank.Type)?.MaxHealth} HP. Deterministic: a fixed factor",
                measured = m1 + " (the whole report: menu OpenZ/V1.2/Mech HP + CPU mix: counted rules check)",
                toggle = "ZWorld.MechHpDeviation (code switch; ZGame: on, off in 'Zod exact'; headless default off: Zod's mech)",
                status = error == null && p1 ? DevActive : DevCheck,
            });
            list.Add(new Deviation
            {
                id = "DV148", change = "The computer builds mechs AND medium tanks in a mix: for each mech-tier build the one its team has built fewer of, a coin when even",
                zod = "Z's computer builds its preferred unit, the medium tank (OpenZ's mech), wherever it can; DV145 gave it the medium tank where its team had fewer of them on the field, which in play gave long runs of mechs",
                openz = "where the computer's pick is the mech and the producer can make the medium tank: the one of the two its team has built fewer of this game (its producers' finished units) " +
                        "counting what its other producers are making or about to make; when even, a draw on the computer's own random stream (seeded from its seed; no other stream moves); " +
                        $"the choice holds until that producer finishes a unit; a build already {ZBot.MTANK_KEEP_S:0.#} s along is finished, and the vehicle in the making at 8:00 too (DV115). " +
                        "Deterministic: the same seed gives the same games",
                measured = m2 + " (the whole report: menu OpenZ/V1.2/Mech HP + CPU mix: counted rules check)",
                toggle = "ZWorld.MechMixDeviation (code switch; ZGame: on, off in 'Zod exact'; headless default off: DV145's own rule)",
                status = error == null && p2 ? DevActive : DevCheck,
            });
        }
    }
}
#endif
