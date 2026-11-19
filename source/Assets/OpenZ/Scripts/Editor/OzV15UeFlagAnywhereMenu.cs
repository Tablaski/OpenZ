using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// (View/OzUeFlagDeckCheck.cs; its report and pictures go to the kit's proof/play folder unless OzUeFlagDeckCheck.Folder is set first).</summary>
    static class OzV15UeFlagAnywhereMenu
    {
        [MenuItem("OpenZ/V1.5/Unit Editor: Flag on Platforms Check (Play, the unit editor open)")]
        static void Check()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ FlagDeckCheck] enter Play and open the unit editor first"); return; }
            if (string.IsNullOrEmpty(OzUeFlagDeckCheck.Folder))
                OzUeFlagDeckCheck.Folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/ue_flag_anywhere/proof/play"));
            OzUeFlagDeckCheck.Begin();
        }

        [MenuItem("OpenZ/V1.5/Unit Editor: Flag on Platforms Check, last report")]
        static void Last() => Debug.Log(OzUeFlagDeckCheck.Running ? "[OpenZ FlagDeckCheck] running" : OzUeFlagDeckCheck.Report);
    }
}
