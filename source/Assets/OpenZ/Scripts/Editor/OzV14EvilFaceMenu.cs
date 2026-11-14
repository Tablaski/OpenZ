#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.4 DV174: the Play check of EvilZod's drawn gun at every drawn volley (View/OzEvilFacePlayCheck.cs) and its last report. The headless count is
    /// Editor/OzV14EvilFaceCount.cs.</summary>
    public static class OzV14EvilFaceMenu
    {
        [MenuItem("OpenZ/V1.4/EvilZod: Face Play Check (a level playing, DV174)")]
        static void Play()
        {
            if (!Application.isPlaying) { Debug.LogError("[OpenZ EvilZod face] start a level in Play first"); return; }
            OzEvilFacePlayCheck.Run();
            Debug.Log("[OpenZ EvilZod face] Play check started, about 2 minutes: report and frame sheets in " + OzEvilFacePlayCheck.Folder());
        }

        [MenuItem("OpenZ/V1.4/EvilZod: Face Play Check, last report")]
        static void Last() => Debug.Log("[OpenZ EvilZod face] " + OzEvilFacePlayCheck.Report + " (" + OzEvilFacePlayCheck.Folder() + ")");
    }
}
#endif
