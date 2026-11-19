#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// (View/OzOneDustDevilPlayCheck.cs; the code View/OzDustDevils.cs Count, View/Atmosphere.cs BuildDevils) and its last report.</summary>
    public static class OzV15OneDustDevilMenu
    {
        [MenuItem("OpenZ/V1.5/One Dust Devil: Play Check")]
        static void Play()
        {
            if (!Application.isPlaying) { Debug.LogError("[OpenZ one dust devil check] start L02 in Play first (the match running)"); return; }
            OzOneDustDevilPlayCheck.Run();
            Debug.Log("[OpenZ one dust devil check] started, about 70 s (no keys or mouse used): report_one_dust_devil.txt in " + OzOneDustDevilPlayCheck.Folder());
        }

        [MenuItem("OpenZ/V1.5/One Dust Devil: Play Check, last report")]
        static void Last() => Debug.Log("[OpenZ one dust devil check] " + OzOneDustDevilPlayCheck.Report + " (" + OzOneDustDevilPlayCheck.Folder() + ")");
    }
}
#endif
