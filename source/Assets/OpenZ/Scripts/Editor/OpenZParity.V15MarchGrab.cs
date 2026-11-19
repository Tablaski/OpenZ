#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit march_grab_r3: the parity row DV203 "march grabs" (Sim/ZObject.MarchGrab.cs) and the menu of its counted check
    /// (OzV15MarchGrabCount).</summary>
    public static partial class OpenZParity
    {
        static void AddMarchGrabDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15MarchGrabCount.Quick(SettingsText, MapText("L02"));
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/Checks/V1.5/March grabs (no Play))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV203", change = "March grabs: one robot of a walking squad takes a flag or an empty gun on the way, the rest walk on (human teams)",
                zod = "only an idle unit looks for a flag or an empty vehicle / gun (auto_grab_flag_distance / auto_grab_vehicle_distance 220 px, zobject.cpp:1603-1853, zsettings.cpp:276-277); only the squad leader looks and the whole squad follows it; a walking squad takes nothing",
                openz = $"on the player's plain move a robot squad looks every half second: a flag that is not its team's within {220} px of any member, or an empty gun within {ZWorld.MarchGrabGunPx} px; the ONE member nearest it leaves the squad (DV98's split), runs (DV54) and takes the flag, then walks the squad's remaining order to the same place, or mans the gun; the others walk on. " +
                        "One robot per target and order; DV84's one-vehicle-per-order mark unchanged; DV130 under fire; not on a bridge approach or a retreat; the computer unchanged (Sim/ZObject.MarchGrab.cs)",
                measured = measured,
                toggle = "ZWorld.MarchGrabDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows and probes hold it off)",
                status = !pass ? DevCheck : DevActive,
            });
        }

        /// <summary>Batch: only the DV203 row (-executeMethod OpenZ.EditorTools.OpenZParity.MarchGrabRowBatch) into Logs/march_grab_DV203.txt;
        /// exit 0 = ACTIVE and every line of the whole check PASS.</summary>
        public static void MarchGrabRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                using (ZWorld.MarchGrabOff()) AddMarchGrabDeviation(list);
                string full; using (OzV11Kit.GameSwitches()) full = OzV15MarchGrabCount.Run(Resources.Load<TextAsset>("OpenZ/default_settings").text, OzV11Kit.MapText("L02"));
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                var d = list[0];
                string text = $"DV203 row: status {d.status}\nmeasured: {d.measured}\n\nthe whole check: {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/march_grab_DV203.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = d.status == DevActive && f == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV203 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }

    public static class OzV15MarchGrabMenu
    {
        [MenuItem("OpenZ/Checks/V1.5/March grabs (no Play)")]
        static void Count()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] march grabs: leave Play mode first (the check sets the simulation's static switches)"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15MarchGrabCount.Run(OzV11Kit.SettingsText, OzV11Kit.MapText("L02"));
            int pass = 0, fail = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) pass++; else if (l.StartsWith("FAIL")) fail++; }
            Debug.Log($"[OpenZ V1.5] march grabs (DV203): {pass} PASS, {fail} FAIL\n{r}");
        }
    }
}
#endif
