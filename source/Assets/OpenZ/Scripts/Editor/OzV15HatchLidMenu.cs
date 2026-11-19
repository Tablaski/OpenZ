#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The menus of the 1.5 kit hatch_lid (a tank's hatch lid never disappears): the Play check View/OzHatchLidCheck.cs in its three
    /// runs (NORMAL: the fix and the guard; BEFORE: the 1.4 knock-off and no guard, to show the check sees the fault; GUARD ONLY: the 1.4
    /// knock-off with the guard on, to show the net works), its last report, and the two switches of View/OzHatchLid.cs for a look by hand
    /// (they go back on by themselves at the next Play). Pictures and reports: the kit's proof/play folder.</summary>
    public static class OzV15HatchLidMenu
    {
        static string PlayFolder => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/hatch_lid/proof/play"));

        [MenuItem("OpenZ/V1.5/Hatch Lid: Play Check (a level playing)")]
        static void Normal() => Run(0);

        [MenuItem("OpenZ/V1.5/Hatch Lid: Play Check BEFORE (the 1.4 knock-off, no guard)")]
        static void Before() => Run(1);

        [MenuItem("OpenZ/V1.5/Hatch Lid: Play Check GUARD ONLY (the 1.4 knock-off, the guard on)")]
        static void GuardOnly() => Run(2);

        [MenuItem("OpenZ/V1.5/Hatch Lid: last report")]
        static void Last() => Debug.Log(OzHatchLidCheck.Report);

        [MenuItem("OpenZ/V1.5/Hatch Lid: switches ON (the fix and the guard)")]
        static void On() { OzHatchLid.KeepLidOn = true; OzHatchLid.GuardOn = true; Debug.Log("[OpenZ] hatch lid: OzHatchLid.KeepLidOn = true, GuardOn = true"); }

        [MenuItem("OpenZ/V1.5/Hatch Lid: switches OFF (the 1.4 knock-off, no guard; a look by hand only)")]
        static void Off() { OzHatchLid.KeepLidOn = false; OzHatchLid.GuardOn = false; Debug.Log("[OpenZ] hatch lid: OzHatchLid.KeepLidOn = false, GuardOn = false (back on at the next Play)"); }

        static void Run(int mode)
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] hatch lid check: start a level in Play first (the normal mode, not 'Zod exact')"); return; }
            if (OzHatchLidCheck.Running) { Debug.LogWarning("[OpenZ] hatch lid check: already running"); return; }
            OzHatchLidCheck.Folder = PlayFolder; OzHatchLidCheck.Mode = mode;
            OzHatchLidCheck.Begin();
            Debug.Log("[OpenZ] hatch lid check started (about 1.5 minutes: staging, hits on the shut lids, EXIT DRIVER and back, damage stages, the view in and out, hits on the open lids); report in " + PlayFolder);
        }
    }
}
#endif
