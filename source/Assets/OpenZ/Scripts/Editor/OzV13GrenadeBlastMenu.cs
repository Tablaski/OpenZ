#if !OPENZ_HARNESS
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// view only) and its before / after pictures. The switch works in Play (the next grenade is drawn with the choice) and before Play:
    /// the choice is kept for this editor session (SessionState: it survives the domain reload on entering Play; a new editor session
    /// starts again from the code's default, ON, the look the built game draws). Nothing is saved for players.
    /// The pictures: OzV11GrenadeStage (Play mode, a running level) with its first row the grenade as 1.2 drew it and its second
    /// the grenade as it is drawn now, through the game's own path (look and sound), beside a Tough rocket and a light tank shell,
    /// (else v1_3), subfolder item32_blast_bigger.
    /// </summary>
    [InitializeOnLoad]
    public static class OzV13GrenadeBlastMenu
    {
        const string Root = "OpenZ/V1.3/Grenade blast drawn bigger (item 32)/", Key = "OpenZ.V13.GrenadeBlastBigger", Switch = Root + "Drawn bigger (default ON)";

        static OzV13GrenadeBlastMenu() { OzGrenadeBlast.Bigger = SessionState.GetBool(Key, OzGrenadeBlast.Bigger); }

        [MenuItem(Switch, false, 1)]
        static void Toggle()
        {
            OzGrenadeBlast.Bigger = !OzGrenadeBlast.Bigger; SessionState.SetBool(Key, OzGrenadeBlast.Bigger);
            Debug.Log($"[OpenZ] 1.3 item 32: grenade blast drawn {(OzGrenadeBlast.Bigger ? $"bigger (x {OzGrenadeBlast.BiggerK:0.00})" : "as in 1.2")}" +
                      (Application.isPlaying ? "; from the next grenade" : "; when Play starts"));
        }
        [MenuItem(Switch, true)] static bool ToggleCheck() { Menu.SetChecked(Switch, OzGrenadeBlast.Bigger); return true; }

        [MenuItem(Root + "Stage before / after pictures (Play mode)", false, 20)]
        static void Stage()
        {
            string keep = OzV11GrenadeStage.Folder;
            OzV11GrenadeStage.Folder = OutDir();
            try { OzV11GrenadeStage.Run(); }                                                // reads Folder at once; the run itself goes on in the editor loop
            finally { OzV11GrenadeStage.Folder = keep; }
        }

        [MenuItem(Root + "Report (Play)", false, 40)]
        static void Report() =>
            Debug.Log($"[OpenZ] 1.3 item 32: bigger {(OzGrenadeBlast.Bigger ? "ON" : "OFF")} (x {OzGrenadeBlast.BiggerK:0.00}); grenade blasts drawn by their radius {OzGrenadeBlast.Drawn}, " +
                      $"the last: {OzGrenadeBlast.LastRadiusPx} px, strength {OzGrenadeBlast.LastStrength:0.00}, drawn x {OzGrenadeBlast.LastDrawK:0.00}, " +
                      $"fire to {OzV11GrenadeStage.FireEdge(OzGrenadeBlast.LastStrength) * OzGrenadeBlast.LastDrawK:0.00} m of {OzGrenadeBlast.LastRadiusPx / 8f:0.00} m");

        static string OutDir()
        {
            string all = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones")), stage = "v1_3";
            if (Directory.Exists(all))
            {
                var ds = Directory.GetDirectories(all, "v1_3*");
                System.Array.Sort(ds, System.StringComparer.Ordinal);
                if (ds.Length > 0) stage = Path.GetFileName(ds[ds.Length - 1]);
            }
            return Path.Combine(all, stage, "item32_blast_bigger");
        }
    }
}
#endif
