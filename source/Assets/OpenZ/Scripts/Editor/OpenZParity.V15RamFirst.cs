#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit ram_first: the parity row DV189 and the menu of its counted check (OzV15RamFirstCount).</summary>
    public static partial class OpenZParity
    {
        static void AddRamFirstDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15RamFirstCount.Quick(SettingsText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.5/Ram First: Count (headless, DV189))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV189", change = "RAM FIRST: a Mammoth / Bizon whose attack target is a vehicle bigger than the light tank (the Bizon: also any ground gun) drives in from gun range and rams / crushes it instead of standing and shooting",
                zod = "Z has no ram or crush (DV182 / DV183 are OpenZ's); a tank stands at gun range and fires",
                openz = "target (player's ATTACK, AGRO, idle pick, attack-move pick) = an enemy crewed vehicle DV182 may ram except the jeep and the light tank (the Bizon: + every ground gun DV183 may crush): inside gun range it drives straight in, gun firing, until DV182 / DV183 start (unchanged); its own pick takes the nearest such target first (whole numbers, ties by object order); "
                        + "fallback (stand and fire) for " + ZObject.RamFirstCoolTicks + " ticks when blocked, held still for " + ZObject.RamFirstHeldTicks + " ticks, the chase over " + ZObject.RamFirstMaxTicks + " ticks or no way; HOLD POSITION and a plain MOVE: unchanged",
                measured = measured,
                toggle = "ZWorld.BigTankRamFirstDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows and probes hold it off; needs DV180 / DV182 or DV181 / DV183)",
                status = !pass ? DevCheck : DevActive,
            });
        }

        /// <summary>Batch: only the DV189 row (-executeMethod OpenZ.EditorTools.OpenZParity.RamFirstRowBatch): the row and the whole counted check
        /// into Logs/ram_first_DV189.txt; exit 0 = the row ACTIVE (every sub-check PASS), 1 = not.</summary>
        public static void RamFirstRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                AddRamFirstDeviation(list);
                var settings = Resources.Load<TextAsset>("OpenZ/default_settings").text;
                string full; using (OzV11Kit.GameSwitches()) full = OzV15RamFirstCount.Run(settings, true);
                var d = list[0];
                bool active = d.status == DevActive;
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                string text = $"DV189 row: status {(active ? "ACTIVE" : "CHECK")}\nmeasured: {d.measured}\n\nthe whole check: {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/ram_first_DV189.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = active && f == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV189 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }

    public static class OzV15RamFirstMenu
    {
        [MenuItem("OpenZ/V1.5/Ram First: Count (headless, DV189)")]
        static void Count()
        {
            var settings = Resources.Load<TextAsset>("OpenZ/default_settings");
            if (!settings) { Debug.LogError("[OpenZ V1.5] ram first count: no Resources/OpenZ/default_settings"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15RamFirstCount.Run(settings.text, true);
            int pass = 0, fail = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) pass++; else if (l.StartsWith("FAIL")) fail++; }
            Debug.Log($"[OpenZ V1.5] ram first count (DV189): {pass} PASS, {fail} FAIL\n{r}");
        }
    }
}
#endif
