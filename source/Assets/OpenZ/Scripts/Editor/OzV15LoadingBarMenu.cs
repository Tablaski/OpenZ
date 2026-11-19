#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit loading_bar_real_r8 (FAILS A24): the loading bar's Play check (View/OzLoadBarPlayCheck.cs; the bar UI/OpenZMainMenu.cs
    /// TickLoadingHonest; the per-frame log View/OzLoadBarLog.cs) and its last report. Play with the MainMenu scene first; no keys or mouse are used.
    /// Pictures, rows and the report go to the kit's proof/play/check_&lt;date_time&gt;.</summary>
    public static class OzV15LoadingBarMenu
    {
        public static string ProofDir => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/loading_bar_real_r8/proof/play"));

        static void Go(string[] entries)
        {
            if (!Application.isPlaying || !Object.FindFirstObjectByType<OpenZMainMenu>()) { Debug.LogError(OzLoadBarPlayCheck.Tag + " enter Play with the MainMenu scene first (the check starts every level from the menu)"); return; }
            var dir = System.IO.Path.Combine(ProofDir, "check_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            OzLoadBarPlayCheck.Run(dir, entries);
            Debug.Log(OzLoadBarPlayCheck.Tag + " started: " + string.Join(" ", entries ?? OzLoadBarPlayCheck.AllEntries) + ", warmup (not counted) / before / after / after2 each (about 30 s a start; all 7 entries about 14 min, L02 + L18 + Unit editor about 6 min): " + dir);
        }

        [MenuItem("OpenZ/1.5/Checks/Loading Bar/1 Every level from the menu (L02 L07 L16 L17 L18, L18 blue, Unit editor)")]
        static void All() => Go(null);

        [MenuItem("OpenZ/1.5/Checks/Loading Bar/2 L02, L18 and the Unit editor")]
        static void Two() => Go(new[] { "L02", "L18", "UE" });

        [MenuItem("OpenZ/1.5/Checks/Loading Bar/3 Per-frame log on for my own starts")]
        static void Arm()
        {
            OzLoadBarLog.Arm();
            Debug.Log(OzLoadBarLog.Tag + " per-frame log armed: every loading screen from the click to 3 s after the level's first picture, one log entry each");
        }

        [MenuItem("OpenZ/1.5/Checks/Loading Bar/4 Last report")]
        static void Last() => Debug.Log(OzLoadBarPlayCheck.Tag + " " + OzLoadBarPlayCheck.Report + "; last bar log: " + OzLoadBarLog.LastSummary);

        [MenuItem("OpenZ/1.5/Checks/Loading Bar/1 Every level from the menu (L02 L07 L16 L17 L18, L18 blue, Unit editor)", true)]
        [MenuItem("OpenZ/1.5/Checks/Loading Bar/2 L02, L18 and the Unit editor", true)]
        [MenuItem("OpenZ/1.5/Checks/Loading Bar/3 Per-frame log on for my own starts", true)]
        static bool CanPlay() => Application.isPlaying && !OzLoadBarPlayCheck.Running;
    }
}
#endif
