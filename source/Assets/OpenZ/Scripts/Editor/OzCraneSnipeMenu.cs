#if !OPENZ_HARNESS
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 item 32 menus (rule DV141, Sim/ZObject.CraneSnipe.cs: the crane's driver can be sniped while he is up in the cab hatch):
    /// the headless counted check Editor/OzV12CraneSnipeCount.cs (outside Play; the report goes to the console and to
    /// </summary>
    public static class OzCraneSnipeMenu
    {
        public const string CheckMenu = "OpenZ/V1.2/Combat: 32 Crane Driver Snipe Check (headless)";
        public const string ProofMenu = "OpenZ/V1.2/Combat: 32 Crane Driver Snipe Proof (Play)";

        [MenuItem(CheckMenu)]
        public static void Count()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.2] item 32 crane driver snipe check: leave Play mode first (the check sets the simulation's static switches)."); return; }
            string report;
            try { report = OzV12CraneSnipeCount.Check(OzV11Kit.SettingsText); }
            catch (Exception ex) { report = "FAIL  the check threw " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace; }
            string verdict = OzV12CraneSnipeCount.Summary(report);
            try
            {
                string p = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "..", "work", "records", "crane_driver_snipe_check.txt"));
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.WriteAllText(p, report);
                Debug.Log($"[OpenZ V1.2] item 32 crane driver snipe check: {verdict}\n-> {p}\n{report}");
            }
            catch (Exception ex) { Debug.Log($"[OpenZ V1.2] item 32 crane driver snipe check: {verdict} (file not written: {ex.Message})\n{report}"); }
        }

        [MenuItem(ProofMenu)]
        public static void Proof() => OzCraneSnipeProof.Run();
    }
}
#endif
