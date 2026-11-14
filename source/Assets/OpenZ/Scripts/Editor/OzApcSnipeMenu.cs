#if !OPENZ_HARNESS
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 item 69 menus (rule DV143, Sim/ZObject.ApcSnipe.cs: the APC's lone driver can be sniped while he is up in the roof
    /// hatch): the headless counted check Editor/OzV12ApcSnipeCount.cs (outside Play; the report goes to the console and to
    /// </summary>
    public static class OzApcSnipeMenu
    {
        public const string CheckMenu = "OpenZ/V1.2/Combat: 69 APC Driver Snipe Check (headless)";
        public const string ProofMenu = "OpenZ/V1.2/Combat: 69 APC Driver Snipe Proof (Play)";

        [MenuItem(CheckMenu)]
        public static void Count()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.2] item 69 APC driver snipe check: leave Play mode first (the check sets the simulation's static switches)."); return; }
            string report;
            try { report = OzV12ApcSnipeCount.Check(OzV11Kit.SettingsText, OzV11Kit.MapText("L02")); }
            catch (Exception ex) { report = "FAIL  the check threw " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace; }
            string verdict = OzV12ApcSnipeCount.Summary(report);
            try
            {
                string p = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "..", "work", "records", "apc_driver_snipe_check.txt"));
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.WriteAllText(p, report);
                Debug.Log($"[OpenZ V1.2] item 69 APC driver snipe check: {verdict}\n-> {p}\n{report}");
            }
            catch (Exception ex) { Debug.Log($"[OpenZ V1.2] item 69 APC driver snipe check: {verdict} (file not written: {ex.Message})\n{report}"); }
        }

        [MenuItem(ProofMenu)]
        public static void Proof() => OzApcSnipeProof.Run();
    }
}
#endif
