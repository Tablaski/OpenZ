using OpenZ.EditorTools;
using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// pictures in a folder of your own) and the rules' headless count (Editor/OzUePlatformSimCount.cs).</summary>
    static class OzUePlatformMenu
    {
        [MenuItem("OpenZ/V1.3/Unit Editor/3 Platforms Check (Play, the unit editor open)")]
        static void Check()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ PlatformCheck] enter Play and open the unit editor first"); return; }
            if (string.IsNullOrEmpty(OzUePlatformCheck.Folder))
                OzUePlatformCheck.Folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_3/open_fixes_31oct/ue_platforms/proof"));
            OzUePlatformCheck.Begin();
        }

        [MenuItem("OpenZ/V1.3/Unit Editor/3 Platforms Check: last report")]
        static void Last() => Debug.Log(OzUePlatformCheck.Running ? "[OpenZ PlatformCheck] running" : OzUePlatformCheck.Report);

        [MenuItem("OpenZ/V1.3/Unit Editor/3a Platforms Rules Count (no Play)")]
        static void Rules()
        {
            var st = Resources.Load<TextAsset>("OpenZ/default_settings"); var map = Resources.Load<TextAsset>("OpenZ/Maps/UE_grid");
            using (OzV11Kit.GameSwitches())
            {
                var r = OzUePlatformSimCount.Check(st.text, map.text);
                if (r.Contains("] PASS")) Debug.Log(r); else Debug.LogError(r);
            }
        }
    }
}
