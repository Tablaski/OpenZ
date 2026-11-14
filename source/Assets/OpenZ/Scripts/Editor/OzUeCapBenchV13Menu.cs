using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.3 item 8 (the Unit editor's unit cap without losing fps, above all at x4): menu OpenZ/V1.3/Unit Editor/8 Unit Cap Bench x1 + x4
    /// (Play, the MainMenu scene or the playground): View/OzUeCapBench.cs Begin(true): 100 .. 400 a side at x1 and x4 with the rules' time
    /// per tick and its split, the worst 1 % frame, the speed reached, 6 SAMPLE FIGHTs at x4 and the same-hover check, into
    /// view side). 5a / 5b (the 1.2 menu) show the last report and stop a run. 8b (Editor/OzUeSameFight.cs): the same fight, edit mode.
    /// </summary>
    public static class OzUeCapBenchV13Menu
    {
        [MenuItem("OpenZ/V1.3/Unit Editor/8 Unit Cap Bench x1 + x4 (Play)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[OpenZ UnitEditor cap bench] enter Play mode in the MainMenu scene (or the playground) first"); return; }
            var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gv != null) EditorWindow.FocusWindowIfItsOpen(gv);
            OzUeField.HoverCull = true;
            OzUeCapBench.Begin(true);
        }

        [MenuItem("OpenZ/V1.3/Unit Editor/8a Unit Cap Bench x1 + x4, hover cull OFF (Play)")]
        public static void RunBefore()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[OpenZ UnitEditor cap bench] enter Play mode in the MainMenu scene (or the playground) first"); return; }
            OzUeCapBench.Begin(true);
            OzUeField.HoverCull = false;                                                                         // the bench puts it back on at its end
        }
    }
}
