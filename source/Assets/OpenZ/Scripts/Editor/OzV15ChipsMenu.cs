#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The menus of the 1.5 kit chips_white (heavy-hit chips as pieces of the struck armour, from the hit point, varied sizes): the Play
    /// check View/OzChipsCheck.cs and its last report. Pictures and report: the kit's proof/play folder.</summary>
    public static class OzV15ChipsMenu
    {
        static string PlayFolder => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/chips_white/proof/play"));

        [MenuItem("OpenZ/V1.5/Chips: Play Check (a level playing)")]
        static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] chips check: start a level in Play first (the normal mode)"); return; }
            if (OzChipsCheck.Running) { Debug.LogWarning("[OpenZ] chips check: already running"); return; }
            OzChipsCheck.Folder = PlayFolder;
            OzChipsCheck.Begin();
            Debug.Log("[OpenZ] chips check started (about 10 s: four hulls hit on the side by one heavy burst each, the piece-material chips); report in " + PlayFolder);
        }

        [MenuItem("OpenZ/V1.5/Chips: last report")]
        static void Last() => Debug.Log(OzChipsCheck.Report);
    }
}
#endif
