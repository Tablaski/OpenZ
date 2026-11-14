#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.4 kit fps_view: the Play check of the hidden first-person view (View/OzFpsViewPlayCheck.cs, the view View/OzFpsView.cs) and its last report.
    /// Hidden from the player: this menu is the editor's only, the game has no word of the view.</summary>
    public static class OzV14FpsViewMenu
    {
        [MenuItem("OpenZ/V1.4/FPS View: Play Check")]
        static void Play()
        {
            if (!Application.isPlaying) { Debug.LogError("[OpenZ FPS check] start L02 in Play first"); return; }
            OzFpsViewPlayCheck.Run();
            Debug.Log("[OpenZ FPS check] Play check started, about 5 minutes, hands off the keyboard and the mouse; its last row ends the match (fix round 3): report and pictures in " + OzFpsViewPlayCheck.Folder());
        }

        /// <summary>fps_view_sky: the short check (about 3 minutes, the match goes on): eye height and gun copy cover per type, the sky rows, the
        /// left click, the leave compared before / after (state list and a still RTS frame, L02 at night with a selected light tank), a flyby's sun.</summary>
        [MenuItem("OpenZ/V1.4/FPS View: Sky + Gun + Fire Check (short)")]
        static void Sky()
        {
            if (!Application.isPlaying) { Debug.LogError("[OpenZ FPS check] start L02 or L17 in Play first"); return; }
            OzFpsViewPlayCheck.RunSky();
            Debug.Log("[OpenZ FPS check] sky + gun + fire check started, about 3 minutes, hands off the keyboard and the mouse: report_sky.txt and pictures in " + OzFpsViewPlayCheck.Folder());
        }

        [MenuItem("OpenZ/V1.4/FPS View: Play Check, last report")]
        static void Last() => Debug.Log("[OpenZ FPS check] " + OzFpsViewPlayCheck.Report + " (" + OzFpsViewPlayCheck.Folder() + ")");
    }
}
#endif
