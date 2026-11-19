#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit fps_feel: the Play check of the private first-person view's feel (View/OzFpsFeelPlayCheck.cs; the feel View/OzFpsFeel.cs)
    /// and its last report. The editor's only: the game has no word of the view.</summary>
    public static class OzV15FpsFeelMenu
    {
        [MenuItem("OpenZ/V1.5/FPS Feel: Play Check")]
        static void Play()
        {
            if (!Application.isPlaying) { Debug.LogError("[OpenZ FPS feel check] start L02 in Play first"); return; }
            OzFpsFeelPlayCheck.Run();
            Debug.Log("[OpenZ FPS feel check] started, about 2 minutes, hands off the keyboard and the mouse: report_feel.txt in " + OzFpsFeelPlayCheck.Folder());
        }

        [MenuItem("OpenZ/V1.5/FPS Feel: Play Check, last report")]
        static void Last() => Debug.Log("[OpenZ FPS feel check] " + OzFpsFeelPlayCheck.Report + " (" + OzFpsFeelPlayCheck.Folder() + ")");
    }
}
#endif
