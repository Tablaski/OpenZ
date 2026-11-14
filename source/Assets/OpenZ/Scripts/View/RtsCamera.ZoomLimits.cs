using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The zoom limits: the closest view a little less close (22 m -> 25 m, the view about 14 % wider; version 1.2: 25 m -> 30 m, the
    /// view 20 % wider, every unit on screen a sixth smaller, one wheel notch less, so it stays an RTS view zoomed all the way in; then
    /// 30 m -> 35 m, one more notch: zoomed all the way in the units are at most twice their size in Z's own view, which the 70 m
    /// start matches, and the free camera never shows nearer ground than this view, RtsCamera.FreeNear.cs) and the widest a little
    /// less wide (230 m -> 200 m, the view about 13 % narrower; level 16 has its own, OzL16Outskirts.MaxHeight).
    /// The standard view (70 m, where every level starts) is unchanged. The level scenes still hold the old two numbers on their
    /// camera, so the new ones are set when the camera wakes in Play.
    /// Only the limits change. What reads minHeight / maxHeight as a limit follows them (the wheel, touch and gamepad zoom, ZoomTo,
    /// SetHeightImmediate, the drawn height, the free camera's distance). What used the old span as a scale keeps it, so it is the
    /// same as before at every height: the scroll speed (PanSpeedAt), the sounds' zoom gain (CombatAudio) and the free camera's
    /// floor at the map border (FreeEdgeHeight). Checks that move the limits for a close shot (they set minHeight after this) keep
    /// working.
    /// </summary>
    public partial class RtsCamera
    {
        /// <summary>The closest and the widest camera height (m) in a match (the closest 35 since version 1.2, its first step 30, 25 in
        /// 1.1; the Unit editor's playground keeps its own, UeClosest).</summary>
        public const float ZoomClosest = 35f, ZoomWidest = 200f;
        /// <summary>The zoom span before the new limits (m). The scroll speed and the sounds' zoom gain are scaled over it, so they
        /// are what they were at every height.</summary>
        public const float SpanClosest = 22f, SpanWidest = 230f;
        /// <summary>Free look: the height the camera's floor rises to at the map border (m): the old closest height, which clears the
        /// scenery outside the map (it has no colliders). Kept, so the free camera is what it was there.</summary>
        public const float FreeEdgeHeight = SpanClosest;

        /// <summary>The scroll speed (m/s, before the hold ramp and SHIFT) at camera height h: 0.6 to 2.6 x panSpeed over the old
        /// zoom span, as before the new limits.</summary>
        public float PanSpeedAt(float h) => panSpeed * Mathf.Lerp(0.6f, 2.6f, Mathf.InverseLerp(SpanClosest, SpanWidest, h));

        void Awake()
        {
            if (!Application.isPlaying) return;
            minHeight = ZoomClosest; maxHeight = ZoomWidest;
            if (UnitEditorScene) minHeight = UeClosest;                                                  // 1.2 item 54: the Unit editor's playground keeps its own closest zoom (RtsCamera.FreeNear.cs)
            height = Mathf.Clamp(height, minHeight, maxHeight);
        }
    }
}
