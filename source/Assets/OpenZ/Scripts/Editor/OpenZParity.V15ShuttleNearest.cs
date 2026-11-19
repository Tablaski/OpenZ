#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit shuttle_nearest_landing: the parity row DV190 and the menu of its counted check (OzV15ShuttleNearestCount).</summary>
    public static partial class OpenZParity
    {
        static void AddShuttleNearestDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15ShuttleNearestCount.Quick(SettingsText, OzV11Kit.MapText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.5/Shuttle Nearest Landing: Count (headless, DV190))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV190", change = "SHUTTLE NEAREST LANDING (extends DV118): the player's shuttle ordered onto a place where it cannot land flies to the nearest place where it can, instead of the order being refused",
                zod = "Z has no playable shuttle (DV118 is OpenZ's); its click filter (K256g) refused a move to a place where the ship can never land",
                openz = "a human team's click (the filter of SHUTTLE ORDERS = LANDABLE SPOTS ONLY) whose landing point is a place where the ship can never land (ZWorld.ShuttleNeverLands, unchanged) goes to the nearest tile middle where it can (the same test) within "
                        + ZWorld.ShuttleNearestTiles + " tiles of the clicked tile (whole tile offsets, nearest by dx*dx + dy*dy, ties by dy then dx; not the edge band, not an own repair station); none: refused as before (the NO cursor, the blip + the toast NO PLACE TO LAND HERE); "
                        + "a landable click, STOP, the repair order, ANYWHERE and every computer order (DV137 / DV138 never pass the filter): unchanged",
                measured = measured,
                toggle = "ZWorld.ShuttleNearestLandingDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows and probes hold it off; needs DV118)",
                status = !pass ? DevCheck : DevActive,
            });
        }

        /// <summary>Batch: only the DV190 row (-executeMethod OpenZ.EditorTools.OpenZParity.ShuttleNearestRowBatch): the row and the whole counted
        /// check into Logs/shuttle_nearest_DV190.txt; exit 0 = the row ACTIVE and every sub-check of the whole check PASS, 1 = not.</summary>
        public static void ShuttleNearestRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                AddShuttleNearestDeviation(list);
                var settings = Resources.Load<TextAsset>("OpenZ/default_settings").text;
                string full; using (OzV11Kit.GameSwitches()) full = OzV15ShuttleNearestCount.Run(settings, OzV11Kit.MapText, true);
                var d = list[0];
                bool active = d.status == DevActive;
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                string text = $"DV190 row: status {(active ? "ACTIVE" : "CHECK")}\nmeasured: {d.measured}\n\nthe whole check: {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/shuttle_nearest_DV190.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = active && f == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV190 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }

    public static class OzV15ShuttleNearestMenu
    {
        [MenuItem("OpenZ/V1.5/Shuttle Nearest Landing: Count (headless, DV190)")]
        static void Count()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] shuttle nearest landing count: leave Play mode first (the check sets the simulation's static switches)."); return; }
            var settings = Resources.Load<TextAsset>("OpenZ/default_settings");
            if (!settings) { Debug.LogError("[OpenZ V1.5] shuttle nearest landing count: no Resources/OpenZ/default_settings"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15ShuttleNearestCount.Run(settings.text, OzV11Kit.MapText, true);
            int pass = 0, fail = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) pass++; else if (l.StartsWith("FAIL")) fail++; }
            Debug.Log($"[OpenZ V1.5] shuttle nearest landing count (DV190): {pass} PASS, {fail} FAIL\n{r}");
        }
    }
}
#endif
