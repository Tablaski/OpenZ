#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.3 item 15 menu (the shuttle's pieces tumble as they fly; View/OzShuttleBreakup.Tumble.cs): the Play watch View/OzShuttleTumbleWatch.cs.
    /// Start it, then run OpenZ/V1.1/Shuttle: K256e Break-up Proof (its K run: four kills) or shoot shuttles down by hand.</summary>
    public static class OzV13ShuttleTumbleMenu
    {
        [MenuItem("OpenZ/V1.3/Wrecks: Item 15 Shuttle Tumble Watch Start or Stop (Play)")]
        static void Watch()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] item 15 tumble watch: enter Play first (level 16 for the K256e proof)"); return; }
            OzShuttleTumbleWatch.Toggle();
            Debug.Log("[OpenZ] item 15 tumble watch: " + OzShuttleTumbleWatch.Report + "; its log in " + OzShuttleTumbleWatch.Folder);
        }
    }
}
#endif
