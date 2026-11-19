#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit fps_shuttle_badges: the Play check of the private first-person view on the shuttle and of the viewed robot's own badges
    /// (View/OzFpsShuttleBadgesPlayCheck.cs; the code View/OzFpsShuttleView.cs, View/OzFpsBadgeHold.cs) and its last report. The editor's only:
    /// the game has no word of the view.</summary>
    public static class OzV15FpsShuttleBadgesMenu
    {
        [MenuItem("OpenZ/V1.5/FPS Shuttle + Badges: Play Check")]
        static void Play()
        {
            if (!Application.isPlaying) { Debug.LogError("[OpenZ FPS shuttle check] start L02 in Play first"); return; }
            OzFpsShuttleBadgesPlayCheck.Run();
            Debug.Log("[OpenZ FPS shuttle check] started, about 3 minutes, hands off the keyboard and the mouse: report_shuttle_badges.txt in " + OzFpsShuttleBadgesPlayCheck.Folder());
        }

        [MenuItem("OpenZ/V1.5/FPS Shuttle + Badges: Play Check, last report")]
        static void Last() => Debug.Log("[OpenZ FPS shuttle check] " + OzFpsShuttleBadgesPlayCheck.Report + " (" + OzFpsShuttleBadgesPlayCheck.Folder() + ")");
    }
}
#endif
