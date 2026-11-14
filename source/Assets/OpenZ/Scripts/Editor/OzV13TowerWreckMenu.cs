#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.3 item 16 menu (a destroyed gun, gatling, missile cannon or howitzer on a fort tower is a burnt wreck and falls apart;
    /// UnitView.BeginWreck, TeamPaint.FortPaletteBurns, WreckPieces.MeetStatics): the Play check View/OzTowerWreckCheck.cs (any level, both
    /// forts, each type on each tower corner, then the four types on the ground).</summary>
    public static class OzV13TowerWreckMenu
    {
        [MenuItem("OpenZ/V1.3/Wrecks: Item 16 Tower Gun Wrecks Check (a level in Play)")]
        static void Check()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] item 16 check: start a level in Play first"); return; }
            OzTowerWreckCheck.Run();
            Debug.Log("[OpenZ] item 16 check started (about 20 s a round, 4-5 rounds a fort, then one round on the ground); its report: OzTowerWreckCheck.Report and " + OzTowerWreckCheck.Folder);
        }
    }
}
#endif
