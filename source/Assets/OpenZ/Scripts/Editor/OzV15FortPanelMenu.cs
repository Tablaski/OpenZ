#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit fort_self_destruct: the Play check of the fort wall panel (View/OzFortSelfDestructCheck.cs; the panel
    /// View/OzFortSelfDestructPanels.cs) and its last report. Editor only.</summary>
    public static class OzV15FortPanelMenu
    {
        [MenuItem("OpenZ/V1.5/Fort: Wall Panel Check (Play, a level playing)")]
        static void Play()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5 fort wall panel] start a level in Play first (L02 or L07: both forts standing)"); return; }
            OzFortSelfDestructCheck.Folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/fort_self_destruct/proof"));
            OzFortSelfDestructCheck.Begin();
            Debug.Log("[OpenZ V1.5 fort wall panel] check started (about 20 s, hands off): pictures and fort_panel_report.txt in " + OzFortSelfDestructCheck.Folder);
        }

        [MenuItem("OpenZ/V1.5/Fort: Wall Panel Check, last report")]
        static void Last() => Debug.Log(OzFortSelfDestructCheck.Running ? "[OpenZ V1.5 fort wall panel] running" : OzFortSelfDestructCheck.Report);
    }
}
#endif
