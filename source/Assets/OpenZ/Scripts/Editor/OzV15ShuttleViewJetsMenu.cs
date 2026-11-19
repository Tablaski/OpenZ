#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit shuttle_view_thrusters_r7 (FAILS A23): the Play check of the first-person view on a shuttle drawing none of the ship's own
    /// thruster flames, halos or heat (View/OzShuttleViewJetsPlayCheck.cs; the code View/OzShuttleJets.cs) and its last report. The editor's
    /// only: the game has no word of the view.</summary>
    public static class OzV15ShuttleViewJetsMenu
    {
        [MenuItem("OpenZ/V1.5/Shuttle View Jets: Play Check")]
        static void Play()
        {
            if (!Application.isPlaying) { Debug.LogError("[OpenZ shuttle view jets check] start a level (L02 or L18) in Play first"); return; }
            OzShuttleViewJetsPlayCheck.Run();
            Debug.Log("[OpenZ shuttle view jets check] started, about a minute (no keys or mouse used): report_shuttle_view_jets.txt in " + OzShuttleViewJetsPlayCheck.Folder());
        }

        [MenuItem("OpenZ/V1.5/Shuttle View Jets: Play Check, last report")]
        static void Last() => Debug.Log("[OpenZ shuttle view jets check] " + OzShuttleViewJetsPlayCheck.Report + " (" + OzShuttleViewJetsPlayCheck.Folder() + ")");
    }
}
#endif
