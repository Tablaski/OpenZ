#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The menus of the 1.5 kit croc_alert (no siren at a crocodile attack; a robot of ours that sees it warns in words,
    /// View/OzCrocAlert.cs): the counters, a test line through the voice director (to hear the mix next to the other robot lines), and the
    /// two switches for a look by hand (back on by themselves at the next domain reload).</summary>
    public static class OzV15CrocAlertMenu
    {
        [MenuItem("OpenZ/V1.5/Croc Alert: report")]
        static void Report() => Debug.Log(OzCrocAlert.Summary());

        [MenuItem("OpenZ/V1.5/Croc Alert: reset the counters")]
        static void Reset() { OzCrocAlert.ResetCounters(); Debug.Log(OzCrocAlert.Summary()); }

        [MenuItem("OpenZ/V1.5/Croc Alert: say a warning line now (Play)")]
        static void Say()
        {
            if (!Application.isPlaying || !AudioDirector.I) { Debug.LogWarning("[OpenZ] croc alert: start a level in Play first"); return; }
            if (!AudioDirector.Has(OzCrocAlert.LineId)) { Debug.LogWarning("[OpenZ] croc alert: " + OzCrocAlert.LineId + " has no clips (sound table or clips missing)"); return; }
            AudioDirector.Say(OzCrocAlert.LineId);
            Debug.Log("[OpenZ] croc alert: asked the director for " + OzCrocAlert.LineId + " (said only into silence and outside its 20 s cool-down; the sound log shows the clip)");
        }

        [MenuItem("OpenZ/V1.5/Croc Alert: switches ON (no siren, the warning lines)")]
        static void On() { OzCrocAlert.SirenOff = true; OzCrocAlert.LinesOn = true; Debug.Log("[OpenZ] croc alert: SirenOff = true, LinesOn = true"); }

        [MenuItem("OpenZ/V1.5/Croc Alert: switches OFF (1.4: the siren, no warning line; a look by hand only)")]
        static void Off() { OzCrocAlert.SirenOff = false; OzCrocAlert.LinesOn = false; Debug.Log("[OpenZ] croc alert: SirenOff = false, LinesOn = false (back on at the next domain reload)"); }
    }
}
#endif
