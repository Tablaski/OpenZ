#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.3 item 17 menu (a dead gun's / howitzer's spinning turret flight 30 % shorter; View/UnitView.GunSpin.cs): the Play check
    /// View/OzGunSpinCheck.cs (any level; it places its own guns near the camera's focus: look at open ground first).</summary>
    public static class OzV13GunSpinMenu
    {
        [MenuItem("OpenZ/V1.3/Wrecks: Item 17 Gun Spin Check (a level in Play)")]
        static void Check()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] item 17 check: start a level in Play first and look at open ground"); return; }
            OzGunSpinCheck.Run();
            Debug.Log("[OpenZ] item 17 check started (about 3 minutes); its report: OzGunSpinCheck.Report and " + OzGunSpinCheck.Folder);
        }
    }
}
#endif
