using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUnitEditor.cs): menu OpenZ/V1.2/Unit Editor/4 Frame Cost Check (big fight). In Play (the
    /// MainMenu scene or the playground) it runs View/OzUeStallCheck.cs: 120 s of a fight of 100 units or more at 1366 x 768, then at
    /// 1920 x 1080, with every kind of world mark drawn, and writes the counted lines (frame times, frames per second, UI objects,
    /// allocation per frame, the dash walk) to k6b_unit_editor_fixes/proof/stall_check.txt and one console line
    /// "[OpenZ UnitEditor frame cost] PASS n/n checks ..." (or FAIL). The Game view's own size comes back at the end. 4a shows the last
    /// report; 4b stops a run.
    /// </summary>
    public static class OzUeStallCheckMenu
    {
        [MenuItem("OpenZ/V1.2/Unit Editor/4 Frame Cost Check (big fight)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[OpenZ UnitEditor frame cost] enter Play mode in the MainMenu scene (or the playground) first"); return; }
            var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gv != null) EditorWindow.FocusWindowIfItsOpen(gv);
            OzUeStallCheck.Begin();
        }

        [MenuItem("OpenZ/V1.2/Unit Editor/4a Frame Cost Check: last report")]
        public static void Last() { Debug.Log(OzUeStallCheck.Report); }

        [MenuItem("OpenZ/V1.2/Unit Editor/4b Frame Cost Check: stop")]
        public static void Stop() { OzUeStallCheck.Abort(); }
    }
}
