using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.2 item 6 (the Unit editor's unit cap, View/OzUeCap.cs): menu OpenZ/V1.2/Unit Editor/5 Unit Cap Bench (Play). In Play (the MainMenu
    /// scene or the playground) it runs View/OzUeCapBench.cs: at 1920 x 1080, 100, 200, 300 and 400 units a side of fighting mixed armies,
    /// 30 s each at x1, and writes the average frames a second, the 1 % low, the longest frame, the main thread's and the graphics chip's
    /// time of a frame and the allocation per frame to k6e_unit_editor_owner_test/proof/unit_cap_bench.txt (one console line "[OpenZ
    /// UnitEditor cap bench] DONE ..."), so the cap can be chosen. The frame rate limit is never lifted. 5a shows the last report; 5b stops a run.
    /// </summary>
    public static class OzUeCapBenchMenu
    {
        [MenuItem("OpenZ/V1.2/Unit Editor/5 Unit Cap Bench (Play)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[OpenZ UnitEditor cap bench] enter Play mode in the MainMenu scene (or the playground) first"); return; }
            var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gv != null) EditorWindow.FocusWindowIfItsOpen(gv);
            OzUeCapBench.Begin();
        }

        [MenuItem("OpenZ/V1.2/Unit Editor/5a Unit Cap Bench: last report")]
        public static void Last() { Debug.Log(OzUeCapBench.Report); }

        [MenuItem("OpenZ/V1.2/Unit Editor/5b Unit Cap Bench: stop")]
        public static void Stop() { OzUeCapBench.Abort(); }
    }
}
