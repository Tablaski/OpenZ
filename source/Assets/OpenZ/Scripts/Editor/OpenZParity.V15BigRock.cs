#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit r14_r16: the DV164 row ("the heavy tank clears the rock it clips", now also the Mammoth and the Bizon) run alone in batch. Its sub-checks:
    /// the heavy tank's (Editor/OzV13HeavyRockCount.cs, R1-R14) and the big tanks' (Editor/OzV15BigRockCount.cs, B1-B9).</summary>
    public static partial class OpenZParity
    {
        /// <summary>Batch: only the DV164 row (-executeMethod OpenZ.EditorTools.OpenZParity.HeavyRockRowBatch): the row and the big tanks' whole counted
        /// check (with the real levels) into Logs/heavy_rock_DV164.txt; exit 0 = the row ACTIVE (every sub-check PASS) and the whole check PASS, 1 = not.</summary>
        public static void HeavyRockRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                AddHeavyRockDeviation(list);
                var settings = Resources.Load<TextAsset>("OpenZ/default_settings").text;
                string Level(string lv) { var a = Resources.Load<TextAsset>("OpenZ/Maps/" + lv + "_grid"); return a != null ? a.text : null; }
                string full; using (OzV11Kit.GameSwitches()) full = OzV15BigRockCount.Check(settings, Level, true);
                var d = list[0];
                bool active = d.status == DevActive;
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                string text = $"DV164 row: status {(active ? "ACTIVE" : "CHECK")}\nchange: {d.change}\nopenz: {d.openz}\nmeasured: {d.measured}\ntoggle: {d.toggle}\n\n"
                              + $"the big tanks' whole check (with levels {string.Join(", ", OzV15BigRockCount.Levels)}): {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/heavy_rock_DV164.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = active && f == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV164 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }
}
#endif
