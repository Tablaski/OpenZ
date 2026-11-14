using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>1.4 ue_nuke_stem_gone: the Play check of the Unit editor's NUKE stem (View/OzUeStemGoneCheck.cs): take 1 measured at 60 fps
    /// </summary>
    static class OzUeStemGoneMenu
    {
        [MenuItem("OpenZ/V1.4/Unit Editor/Nuke Stem Gone: 60 fps Capture + Check + Owner Clip (Play, the unit editor open)")]
        static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ NukeStemGone] enter Play and open the unit editor first"); return; }
            OzUeStemGoneCheck.Folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                "../../../../work/staging/v1_4/kits/ue_nuke_stem_gone/proof/play_" + System.DateTime.Now.ToString("yyyyMMdd_HHmm")));
            OzUeStemGoneCheck.Begin();
        }

        [MenuItem("OpenZ/V1.4/Unit Editor/Nuke Stem Gone: last report")]
        static void Last() => Debug.Log(OzUeStemGoneCheck.Running ? "[OpenZ NukeStemGone] running" : OzUeStemGoneCheck.Report);
    }
}
