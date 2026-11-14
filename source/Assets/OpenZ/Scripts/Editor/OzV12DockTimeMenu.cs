using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>1.2 items 70 and 71: the dock's drag start (View/OzUeDragCheck.cs) and the factory time at every zoom
    /// (UI/OpenZUnitOverlay.TimeAlways.cs).</summary>
    static class OzV12DockTimeMenu
    {
        [MenuItem("OpenZ/V1.2/Unit Editor: 70 Dock Drag Gestures Check (unit editor open, Play)")]
        static void DragCheck()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ DockDrag] enter Play and open the unit editor first"); return; }
            OzUeDragCheck.Begin();
        }

        [MenuItem("OpenZ/V1.2/Factory Time: 71 Time At Every Zoom Check (edit mode or Play)")]
        static void TimeCheck()
        {
            string r = OpenZUnitOverlay.TimeCheck(out int pass, out int fail, out int oldMiss);
            if (fail == 0 && pass > 0) Debug.Log(r); else Debug.LogError(r);
        }
    }
}
