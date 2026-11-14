#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The menus of open_fixes_2nov/wreck_toss_landing (thrown fort cannons and turrets land where their arc ends, no flip, no jump,
    /// their pieces never vanish in one frame): the Play check View/OzTossLandingCheck.cs, staged on a level or as a recorder.</summary>
    public static class OzV13TossLandingMenu
    {
        [MenuItem("OpenZ/V1.3/Wrecks: Toss Landing Check, staged (a level in Play)")]
        static void Staged()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] toss landing check: start a level in Play first"); return; }
            OzTossLandingCheck.Run();
            Debug.Log("[OpenZ] toss landing check started (about 50 s a fort); its report: OzTossLandingCheck.Report and " + OzTossLandingCheck.Folder);
        }

        [MenuItem("OpenZ/V1.3/Wrecks: Toss Landing Recorder ON (follows every toss until OFF)")]
        static void On()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] toss landing recorder: start a level in Play first"); return; }
            OzTossLandingCheck.Arm();
            Debug.Log("[OpenZ] toss landing recorder on: every toss from now on is followed until the OFF menu");
        }

        [MenuItem("OpenZ/V1.3/Wrecks: Toss Landing Recorder OFF (writes the report)")]
        static void Off()
        {
            OzTossLandingCheck.Disarm();
            Debug.Log("[OpenZ] toss landing recorder: " + OzTossLandingCheck.Report);
        }
    }
}
#endif
