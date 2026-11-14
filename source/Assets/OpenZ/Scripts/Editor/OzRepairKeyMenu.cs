#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 item 10 menu (the wrench key over the command bar, UI/OpenZHud.RepairKey.cs): the Play proof View/OzRepairKeyProof.cs
    /// on a level with a repair station (16 or 17): hidden without a station, shown for a damaged vehicle, hidden for undamaged ones, beside
    /// the factory list, gone with MINIMAL UI / the hidden HUD / the build menu / the touch layout, the click = the R order (to the station
    /// and back), the R key's same order, the station lost. Its output folder: OzRepairKeyProof.Folder() (built at run time from the
    /// project's place).</summary>
    public static class OzRepairKeyMenu
    {
        [MenuItem("OpenZ/V1.2/HUD: 10 Wrench Key Proof (Level 16 or 17, Play)")]
        static void Proof()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] 1.2 item 10 wrench key proof: start level 16 or 17 in Play first"); return; }
            OzRepairKeyProof.Run();
            Debug.Log("[OpenZ] 1.2 item 10 wrench key proof started; its report: OzRepairKeyProof.Report, the log and pictures in " + OzRepairKeyProof.Folder());
        }
    }
}
#endif
