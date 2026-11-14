#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The menus of the 1.4 kit wreck_landing (a thrown turret settles where it lands: no rise, no slide up, no pop): the Play trace
    /// View/OzWreckLandingTrace.cs, staged on the roughest ground of a level or as a recorder, and the kit's switch for a before / after run.</summary>
    public static class OzV14WreckLandingMenu
    {
        [MenuItem("OpenZ/V1.4/Wrecks: Landing Trace, staged (a level in Play)")]
        static void Staged()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] wreck landing trace: start a level in Play first"); return; }
            OzWreckLandingTrace.Run();
            Debug.Log("[OpenZ] wreck landing trace started (16 spots, about 2 minutes); its report: OzWreckLandingTrace.Report and " + OzWreckLandingTrace.Folder);
        }

        [MenuItem("OpenZ/V1.4/Wrecks: Landing Trace Recorder ON (follows every thrown turret until OFF)")]
        static void On()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] wreck landing trace: start a level in Play first"); return; }
            OzWreckLandingTrace.Arm();
            Debug.Log("[OpenZ] wreck landing recorder on: every turret thrown from now on is followed until the OFF menu");
        }

        [MenuItem("OpenZ/V1.4/Wrecks: Landing Trace Recorder OFF (writes the report)")]
        static void Off()
        {
            OzWreckLandingTrace.Disarm();
            Debug.Log("[OpenZ] wreck landing trace: " + OzWreckLandingTrace.Report);
        }

        [MenuItem("OpenZ/V1.4/Wrecks: Turret Settle In Place ON (the kit)")]
        static void SettleOn() { OzHeavyLanding.TurretSettleInPlace = true; Debug.Log("[OpenZ] OzHeavyLanding.TurretSettleInPlace = true (the 1.4 settle)"); }

        [MenuItem("OpenZ/V1.4/Wrecks: Turret Settle In Place OFF (the 1.3 landing, for a before run)")]
        static void SettleOff() { OzHeavyLanding.TurretSettleInPlace = false; Debug.Log("[OpenZ] OzHeavyLanding.TurretSettleInPlace = false (the 1.3 landing: bounce, rock, skid, the fit every frame)"); }
    }
}
#endif
