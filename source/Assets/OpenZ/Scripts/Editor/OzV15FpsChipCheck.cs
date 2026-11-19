using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit fps_chip_place_r3 (FAILS_LIST A6; owner, exe 13c): the menus of the
    /// FPS chip's counted checks (UI/OzFpsChipProof.cs). PLAN (no Play; batch entry Batch): the Unit editor's top bar and a match's HUD at 13
    /// screens x 6 interface scales x ON / DETAILED, report Logs/fps_chip_plan_check.txt. SCREEN (Play): the screen as it is now, the chip
    /// against every visible picture and text, ON / DETAILED x 6 scales, appended to Logs/fps_chip_screens.txt (README: the screens to walk).
    /// </summary>
    public static class OzV15FpsChipCheck
    {
        const string PlanLog = "Logs/fps_chip_plan_check.txt";

        [MenuItem("OpenZ/Checks/V1.5/A6 FPS chip: plan check (no Play)")]
        public static void Plan()
        {
            int bad = RunPlan(out string text);
            Debug.Log(text);
            if (bad != 0) Debug.LogError("[OpenZ V1.5 A6 FPS chip] plan check: " + bad + " FAIL (see " + PlanLog + ")");
        }

        [MenuItem("OpenZ/Checks/V1.5/A6 FPS chip: count this screen (Play)")]
        public static void CountThisScreen()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[OpenZ V1.5 A6 FPS chip] enter Play and open the screen to count first"); return; }
            OzFpsChipProofRun.Begin(null);
        }

        /// <summary>Batch: -executeMethod OpenZ.EditorTools.OzV15FpsChipCheck.Batch (exit 0 = PASS).</summary>
        public static void Batch()
        {
            int bad;
            try { bad = RunPlan(out string text); Console.WriteLine(text); }
            catch (Exception e) { Debug.LogError("[OpenZ V1.5 A6 FPS chip] plan check threw: " + e); bad = 99; }
            EditorApplication.Exit(bad == 0 ? 0 : 1);
        }

        static int RunPlan(out string text)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[OpenZ V1.5 A6 FPS chip] plan check " + DateTime.Now.ToString("dd MMM yyyy HH:mm:ss") + " (kit fps_chip_place_r3; edit mode, no Play)");
            int bad = OzFpsChipProof.PlanCheck(sb);
            text = sb.ToString();
            try { Directory.CreateDirectory("Logs"); File.WriteAllText(PlanLog, text); } catch (Exception e) { Debug.LogWarning("[OpenZ V1.5 A6 FPS chip] log: " + e.Message); }
            return bad;
        }
    }
}
