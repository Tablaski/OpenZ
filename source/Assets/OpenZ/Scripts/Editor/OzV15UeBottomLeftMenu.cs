#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Unit editor's new bottom-left (the bigger, team-tinted RED / BLUE / NEUTRAL switch) and of the zoom row moved into the BACKGROUND
    /// popover (View/OzUeBottomLeftPlayCheck.cs; the code UI/OzUeTeamSeg.cs, UI/OzUeBottomLayout.cs, UI/OzUeZoomBar.cs, UI/OzUeBackdropMenu.cs)
    /// and its last report. Run it in the Unit editor (Play, the playground up), after one click into the Game view.</summary>
    public static class OzV15UeBottomLeftMenu
    {
        [MenuItem("OpenZ/V1.5/UE Bottom Left: Play Check")]
        static void Play()
        {
            if (!Application.isPlaying || !OzUnitEditor.Active) { Debug.LogError("[OpenZ ue bottom left check] start the Unit editor in Play first (the playground up)"); return; }
            OzUeBottomLeftPlayCheck.Run();
            Debug.Log("[OpenZ ue bottom left check] started, about 25 s (it moves the game's own mouse; touch nothing): report in " + OzUeBottomLeftPlayCheck.Folder());
        }

        [MenuItem("OpenZ/V1.5/UE Bottom Left: Play Check, last report")]
        static void Last() => Debug.Log("[OpenZ ue bottom left check] " + OzUeBottomLeftPlayCheck.Report + " (" + OzUeBottomLeftPlayCheck.Folder() + ")");
    }
}
#endif
