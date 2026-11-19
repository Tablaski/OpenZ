#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit ram_chain_n24_n25: the parity row DV195 (the rammed stay still, the big tanks ram on; Sim/ZObject.RamChain.cs) and the menu of
    /// its counted check (OzV15RamChainCount). Measured headless on plain maps (Quick); the whole check: menu OpenZ/V1.5/Ram Chain: Count (headless,
    /// DV195). Only a ram's / crush's target and the big tank after a hit change, so a level, a bot match and the parity's other probes are as
    /// before. The parity report's Zod rows and every other row's probe hold it off.</summary>
    public static partial class OpenZParity
    {
        static void AddRamChainDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15RamChainCount.Quick(SettingsText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.5/Ram Chain: Count (headless, DV195))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV195", change = "the rammed stay still, the big tanks ram on: a vehicle or gun the Mammoth rammed or the Bizon crushed never moves again; the Mammoth and the Bizon ram the next enemy close in front without a stop",
                zod = "Z has no ram or crush (DV182 / DV183 are OpenZ's); DV182 held the rammed vehicle but took its orders and let the spacing push move it, and stood RamCoolTicks (" + ZObject.RamCoolTicks + " ticks) after every hit",
                openz = "from the contact until it is destroyed the doomed vehicle or gun refuses every order, is a fixed obstacle for the pathing layer (never pushed, stepped aside or shoved) and may overlap the big tank that doomed it; "
                        + "a ram / crush that ends with a hit rams the next enemy that DV182's start rules accept right then (enemy crewed vehicle or, for the Bizon, ground gun; within +/- " + ZObject.RamStartArcDeg + " deg and " + ZObject.RamStartGapPx
                        + " px; the way clear, its own doomed victims no obstacle; never in water or lava (DV192), never a friendly) in the same tick: no stop, the boost building up again from its speed over " + ZObject.RamRampTicks
                        + " ticks; a drive held by that next target ends at once; nothing fits: the order goes on, the next look " + ZObject.RamLookTicks + " ticks on (not " + ZObject.RamCoolTicks + "); whole numbers, no random draw",
                measured = measured,
                toggle = "ZWorld.RamChainDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; acts with DV182 / DV183; the parity report's Zod rows and probes hold it off)",
                status = !pass ? DevCheck : DevActive,
            });
        }

        /// <summary>Batch: only the DV195 row (-executeMethod OpenZ.EditorTools.OpenZParity.RamChainRowBatch): the row and the whole counted check into
        /// Logs/ram_chain_DV195.txt; exit 0 = the row ACTIVE and every line of the whole check PASS, 1 = not.</summary>
        public static void RamChainRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                using (ZWorld.RamChainOff()) AddRamChainDeviation(list);
                var settings = Resources.Load<TextAsset>("OpenZ/default_settings").text;
                string full; using (OzV11Kit.GameSwitches()) full = OzV15RamChainCount.Run(settings, true);
                bool active = list.Count == 1 && list[0].status == DevActive;
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                var sb = new System.Text.StringBuilder();
                foreach (var d in list) sb.Append($"{d.id} row: status {(d.status == DevActive ? "ACTIVE" : "CHECK")}\nzod: {d.zod}\nopenz: {d.openz}\nmeasured: {d.measured}\n\n");
                string text = sb + $"the whole check: {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/ram_chain_DV195.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = active && f == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV195 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }

    public static class OzV15RamChainMenu
    {
        [MenuItem("OpenZ/V1.5/Ram Chain: Count (headless, DV195)")]
        static void Count()
        {
            var settings = Resources.Load<TextAsset>("OpenZ/default_settings");
            if (!settings) { Debug.LogError("[OpenZ V1.5] ram chain count: no Resources/OpenZ/default_settings"); return; }
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] ram chain count: leave Play mode first (the check sets the simulation's static switches)"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15RamChainCount.Run(settings.text, true);
            int pass = 0, fail = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) pass++; else if (l.StartsWith("FAIL")) fail++; }
            Debug.Log($"[OpenZ V1.5] ram chain count (DV195): {pass} PASS, {fail} FAIL\n{r}");
        }

        [MenuItem("OpenZ/V1.5/Ram Chain: Count (short, DV195)")]
        static void Short()
        {
            var settings = Resources.Load<TextAsset>("OpenZ/default_settings");
            if (!settings || Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] ram chain count (short): leave Play mode first"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15RamChainCount.Quick(settings.text);
            int pass = 0, fail = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) pass++; else if (l.StartsWith("FAIL")) fail++; }
            Debug.Log($"[OpenZ V1.5] ram chain count, short (DV195): {pass} PASS, {fail} FAIL\n{r}");
        }
    }
}
#endif
