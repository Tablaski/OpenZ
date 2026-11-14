using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUnitEditor.cs): menu OpenZ/V1.2/Unit Editor/3 Play Proof (pictures). Enter Play in the MainMenu
    /// scene, click into the Game view once (so the proof's input events reach the game), then run it: View/OzUeProof.cs goes through
    /// ARCHITECTURE 10.2 items 1-10 with real clicks and keys, sets the Game view to 1920 x 1080, then 1366 x 768, then 2560 x 1600 for its pictures
    /// (k6_unit_editor/proof/pictures), writes proof/checks.txt and logs one line "[OpenZ UnitEditor proof] PASS n/n checks ..." (or FAIL).
    /// The Game view's own size comes back at the end. 3a shows the last report; 3b stops a run.
    /// </summary>
    public static class OzUeProofMenu
    {
        [MenuItem("OpenZ/V1.2/Unit Editor/3 Play Proof (pictures)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[OpenZ UnitEditor proof] enter Play mode in the MainMenu scene first"); return; }
            var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gv != null) EditorWindow.FocusWindowIfItsOpen(gv);
            OzUeProof.Begin();
        }

        [MenuItem("OpenZ/V1.2/Unit Editor/3a Play Proof: last report")]
        public static void Last() { Debug.Log(OzUeProof.Report); }

        [MenuItem("OpenZ/V1.2/Unit Editor/3b Play Proof: stop")]
        public static void Stop() { OzUeProof.Abort(); }
    }
}
