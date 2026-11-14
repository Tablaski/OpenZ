using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>1.3 bullets_straight: the counted Play check (View/ShotLineCheck.cs; set ShotLineCheck.Folder first for its report and pictures in a folder of your own).</summary>
    static class ShotLineMenu
    {
        [MenuItem("OpenZ/V1.3/Unit Editor/4 Bullets Straight Check (Play, the unit editor open)")]
        static void Check()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ ShotLineCheck] enter Play and open the unit editor first"); return; }
            if (string.IsNullOrEmpty(ShotLineCheck.Folder))
                ShotLineCheck.Folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_3/open_fixes_31oct/bullets_straight/proof"));
            ShotLineCheck.Begin();
        }

        [MenuItem("OpenZ/V1.3/Bullets Straight: fort line (Play, a level with a fort)")]
        static void FortLine()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ ShotLineCheck] enter Play on a level with a fort first"); return; }
            if (string.IsNullOrEmpty(ShotLineCheck.Folder))
                ShotLineCheck.Folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_3/open_fixes_31oct/bullets_straight/proof"));
            ShotLineCheck.FortLine();
        }

        [MenuItem("OpenZ/V1.3/Unit Editor/4 Bullets Straight Check: last report")]
        static void Last() => Debug.Log(ShotLineCheck.Running ? "[OpenZ ShotLineCheck] running" : ShotLineCheck.Report);
    }
}
