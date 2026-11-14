#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 item 60 menus (UNIT STATS OPENZ | CUSTOM in the levels, DV142): the headless counted check (no Play; whole bot
    /// matches on levels 2, 16 and 17, a few minutes, run once from the editor's update loop), the Play proof (View/OzLevelStatsProof.cs,
    /// from the MainMenu scene in Play), its battlefield-screen-only form for the pictures at another Game view size, and a status line.
    /// Output folder: OzLevelStatsProof.Folder().</summary>
    public static class OzCustomStatsMenu
    {
        const string Running = "oz_k60_check_running";

        /// <summary>The editor keeps static values after Play: Play stopped inside a Custom level would leave the DV142 switch, the rows and the
        /// rule values set in edit mode (the parity report and the counted checks would see them). Back in edit mode everything is ended.</summary>
        [InitializeOnLoadMethod]
        static void EndWithPlay() => EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredEditMode) OzLevelStats.Disarm("Play stopped in the editor"); };

        [MenuItem("OpenZ/V1.2/Unit Stats: 60 Custom Stats Check (headless, no Play)")]
        static void Check()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ] 1.2 item 60 check: leave Play mode first (it sets the simulation's static switches)"); return; }
            if (SessionState.GetBool(Running, false)) { Debug.LogWarning("[OpenZ] 1.2 item 60 check: already running (its report comes in the console and in " + OzLevelStatsProof.Folder() + ")"); return; }
            SessionState.SetBool(Running, true);
            Debug.Log("[OpenZ] 1.2 item 60 Custom Stats check: started (headless, a few minutes; the report follows in the console and in " + OzLevelStatsProof.Folder() + "/check.txt)");
            EditorApplication.CallbackFunction once = null;
            once = () =>
            {
                EditorApplication.update -= once;
                try
                {
                    string report = null;
                    OzV11Kit.RunMenu("1.2 item 60 Unit Stats: Custom Stats check", () => report = OzV12CustomStatsCount.Check(OzV11Kit.SettingsText, OzV11Kit.MapText, false));
                    if (report != null)
                    {
                        System.IO.Directory.CreateDirectory(OzLevelStatsProof.Folder());
                        System.IO.File.WriteAllText(System.IO.Path.Combine(OzLevelStatsProof.Folder(), "check.txt"), report);
                    }
                }
                finally { SessionState.SetBool(Running, false); OzLevelStats.Disarm("the check ended"); }
            };
            EditorApplication.update += once;
        }

        [MenuItem("OpenZ/V1.2/Unit Stats: 60 Custom Stats Play Proof (Play, from the main menu)")]
        static void Proof()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] 1.2 item 60 proof: start the MainMenu scene in Play first"); return; }
            OzLevelStatsProof.Run(false);
            Debug.Log("[OpenZ] 1.2 item 60 Custom Stats proof started; its report: OzLevelStatsProof.Report, the log and pictures in " + OzLevelStatsProof.Folder());
        }

        [MenuItem("OpenZ/V1.2/Unit Stats: 60b Battlefield Screen Pictures (Play, main menu, this Game view size)")]
        static void MenuPictures()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] 1.2 item 60b: start the MainMenu scene in Play first"); return; }
            OzLevelStatsProof.Run(true);
            Debug.Log("[OpenZ] 1.2 item 60b battlefield screen pictures started (" + Screen.width + "x" + Screen.height + "); pictures in " + OzLevelStatsProof.Folder());
        }

        [MenuItem("OpenZ/V1.2/Unit Stats: 60 Status (Play)")]
        static void Status() => Debug.Log("[OpenZ] " + OzLevelStatsLevel.Status() + (OzLevelStats.LastStart != null ? " | the level start: " + OzLevelStats.LastStart.line : ""));
    }
}
#endif
