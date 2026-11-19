#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// the Unit editor's pause look (View/OzUePauseVignettePlayCheck.cs; the code View/OzUePauseLook.cs, View/OzUeFloor.cs Grade) and its last
    /// report. Run it once in the Unit editor and once in a level.</summary>
    public static class OzV15UePauseVignetteMenu
    {
        [MenuItem("OpenZ/V1.5/UE Pause Vignette: Play Check")]
        static void Play()
        {
            if (!Application.isPlaying) { Debug.LogError("[OpenZ ue pause vignette check] start the Unit editor (or a level) in Play first"); return; }
            OzUePauseVignettePlayCheck.Run();
            Debug.Log("[OpenZ ue pause vignette check] started, about 10 s (no keys or mouse used): report in " + OzUePauseVignettePlayCheck.Folder());
        }

        [MenuItem("OpenZ/V1.5/UE Pause Vignette: Play Check, last report")]
        static void Last() => Debug.Log("[OpenZ ue pause vignette check] " + OzUePauseVignettePlayCheck.Report + " (" + OzUePauseVignettePlayCheck.Folder() + ")");
    }
}
#endif
