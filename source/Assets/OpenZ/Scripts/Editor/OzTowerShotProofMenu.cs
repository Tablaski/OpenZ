#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.3 item 3 Play proof's menu (View/OzTowerShotProof.cs): every weapon in turn at the enemy's fort tower gun, the drawn rounds
    /// counted (OzTowerAim) and pictured. Enter Play on level 2, 16 or 17 first. The output folder is OzTowerShotProof.Folder().</summary>
    public static class OzTowerShotProofMenu
    {
        [MenuItem("OpenZ/V1.3/Tower guns: shots Play proof (every weapon at a fort tower gun, Play)")]
        static void Proof()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] tower shots proof: enter Play on level 16 or 17, or on level 2 or 7 as blue (PLAY AS BLUE: their red fort has the tower guns), first"); return; }
            OzTowerShotProof.Run();
            Debug.Log("[OpenZ] tower shots proof started; its report: OzTowerShotProof.Report, the logs and frames in " + OzTowerShotProof.Folder());
        }
    }
}
#endif
