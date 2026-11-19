#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit crane_deck_sound_r3: the parity row DV206 "a crane repairs a bridge from its land end" (Sim/ZObject.CraneLandEnd.cs, the
    /// two hooks in Sim/ZObject.Buildings.cs ProcessCraneRepairWP) and the menu of its counted check (OzV15CraneLandEndCount).</summary>
    public static partial class OpenZParity
    {
        static void AddCraneLandEndDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15CraneLandEndCount.Quick(SettingsText, MapText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/Checks/V1.5/Crane repairs a bridge from its land end (no Play))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV206", change = "A crane repairs a bridge from its land end: it stops at the bridge's nearest reachable dry end and works there; it never drives onto a destroyed span or over the water",
                zod = "ProcessCraneRepairWP (zobject.cpp:2915-3091): the crane paths to the nearest crane entrance (2 tiles past either end, bbridge.cpp:454-481), drives straight to the bridge's centre (over the river when the deck is down: the game treats it as wading and it sinks), drives back out to the entrance, and the bridge is repaired on the next tick",
                openz = "Sim/ZObject.CraneLandEnd.cs: bridges only (other buildings unchanged): the stop is Zod's entrance at the nearer end the crane can drive to whose 2 x 2 box is on dry ground off the bridge's own box (else up to 2 tiles further out along the axis); there it works for as long as Zod's drive in and out would have taken (the same straight line and tile speeds, 4 s at least, 40 s at most) and the bridge is repaired on the next tick after the work, as in Zod. A damaged bridge that still stands is mended from the same stop (DV61's 4 s). No dry reachable stop = Zod's pick",
                measured = measured,
                toggle = "ZWorld.CraneLandEndDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows and probes hold it off; off in every counted check unless the check sets it)",
                status = !pass ? DevCheck : DevActive,
            });
        }

        /// <summary>Batch: only the DV206 row (-executeMethod OpenZ.EditorTools.OpenZParity.CraneLandEndRowBatch) into Logs/crane_land_end_DV206.txt;
        /// exit 0 = ACTIVE and every line of the whole check PASS.</summary>
        public static void CraneLandEndRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                using (ZWorld.CraneLandEndOff()) AddCraneLandEndDeviation(list);
                string full; using (OzV11Kit.GameSwitches()) full = OzV15CraneLandEndCount.Run(SettingsText, MapText);
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                var d = list[0];
                string text = $"DV206 row: status {d.status}\nmeasured: {d.measured}\n\nthe whole check: {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/crane_land_end_DV206.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = d.status == DevActive && f == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV206 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }

    public static class OzV15CraneLandEndMenu
    {
        [MenuItem("OpenZ/Checks/V1.5/Crane repairs a bridge from its land end (no Play)")]
        static void Count()
        {
            string r; using (OzV11Kit.GameSwitches()) r = OzV15CraneLandEndCount.Run(OzV11Kit.SettingsText, OzV11Kit.MapText);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/crane_land_end_DV206_check.txt"), r);
            Debug.Log("[OpenZ V1.5] Crane repairs a bridge from its land end (DV206):\n" + r);
        }
    }
}
#endif
