#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.3 item 39 (the hover pre-cull, View/PlayerInput.HoverCull.cs): menu OpenZ/V1.3/Hover pre-cull check L02 L16 L17 (Play). Enter Play
    /// (the MainMenu scene or a level), then run it: View/OzHoverPreCullCheck.cs loads L02, L16 and L17 in turn and compares the hovered
    /// object with the pre-cull on and off at 2000 seeded pointer positions a level (heights, the RTS view and free-camera poses,
    /// selections), with the time of both passes and the objects skipped. About 3-4 minutes; hands off. Report:
    /// A-B look in Play (it is on by default and comes back on with every domain reload).
    /// </summary>
    public static class OzV13HoverPreCullMenu
    {
        [MenuItem("OpenZ/V1.3/Hover pre-cull check L02 L16 L17 (Play)")]
        static void Check()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[OpenZ HoverPreCull] enter Play mode first (the MainMenu scene or a level)"); return; }
            var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gv != null) EditorWindow.FocusWindowIfItsOpen(gv);
            OzHoverPreCullCheck.Run();
        }

        [MenuItem("OpenZ/V1.3/Hover pre-cull on or off (switch)")]
        static void Switch()
        {
            PlayerInput.HoverPreCull = !PlayerInput.HoverPreCull;
            Debug.Log("[OpenZ HoverPreCull] the hover pre-cull is " + (PlayerInput.HoverPreCull ? "ON (default)" : "OFF (the 1.2 full pass)"));
        }
    }
}
#endif
