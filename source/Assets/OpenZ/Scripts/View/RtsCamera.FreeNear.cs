using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 54 (the closest zoom still too close for an RTS game, above all with the free camera). Tilted lower than the fixed view,
    /// the free look kept the fixed view's distance to the point it looks at, so the camera sank (15.5 m over the ground at the 30 m
    /// zoom and the lowest tilt) and the bottom of the screen came far nearer than the fixed view ever shows (23 m of ground across it
    /// instead of 31). Now, near the closest zoom, a low tilt backs the camera off along its tilt just far enough that the nearest ground
    /// on screen (the bottom row) is never nearer than the fixed view's nearest ground at the closest zoom: zoomed all the way in, the
    /// free camera never shows less of the battle than the fixed view does (tilted lowest it stands about 24 m up, 57 m from the point
    /// it looks at). The tilt, the turn and the view centre stay what the player asked for; the back-off grows smoothly as the view
    /// tilts lower (none at the fixed view's own tilt and steeper) and fades out by the standard 70 m, so every wheel notch still moves
    /// the camera, and from 70 m on (the widest zoom included) the free look is what it was. The floors of PlaceFree (inside the map,
    /// at its border, over the terrain) still apply after it. The Unit editor's playground keeps its own camera: its closest zoom
    /// (UeClosest) and its free look are as they were.
    /// </summary>
    public partial class RtsCamera
    {
        /// <summary>Off = the free look keeps the fixed view's distance at every tilt, as before (for a before / after look).</summary>
        public static bool FreeNearOn = true;
        /// <summary>Half the game camera's vertical field of view (deg; the scenes' 32) and the height (m) by which the back-off has
        /// faded out (the standard view, Zod's own framing).</summary>
        public const float FreeNearHalfFov = 16f, FreeNearFadeTo = 70f;
        /// <summary>The Unit editor's closest zoom (m): the one its playground was built with (the match's closest is ZoomClosest).</summary>
        public const float UeClosest = 30f;

        /// <summary>This camera serves the Unit editor's playground (a playground runs, or the camera stands in its scene).</summary>
        bool UnitEditorScene => OzUeHooks.Active || gameObject.scene.name == OzUnitEditor.SceneName;

        /// <summary>The free look's distance (m) to the focus at zoom h and tilt t (deg below the horizon), the fixed view's being d.</summary>
        float FreeNearDistance(float h, float d, float t) => FreeNearOn && !OzUeHooks.Active ? FreeNearDistance(h, d, t, minHeight, pitch) : d;

        /// <summary>The back-off, pure (the checks reckon with it): tilted lower than the pitch, the distance grows by (k - 1) x the
        /// closest zoom's distance x the fade, k = sin(pitch) sin(t + 16) / (sin(pitch + 16) sin(t)) (the fixed view's nearest ground
        /// over this tilt's at one distance: 1 at the pitch), the fade 1 at the closest zoom and 0 from FreeNearFadeTo on. At the
        /// closest zoom the nearest ground on screen is then exactly the fixed view's, zoomed out it is farther, and the distance grows
        /// with the zoom at every tilt the free look allows (k - 1 stays under (70 - closest) / closest).</summary>
        public static float FreeNearDistance(float h, float d, float t, float closest, float pitch)
        {
            if (t >= pitch || t <= 1f || closest <= 0f || FreeNearFadeTo - closest < 1f) return d;
            float a = FreeNearHalfFov * Mathf.Deg2Rad, p = pitch * Mathf.Deg2Rad, r = t * Mathf.Deg2Rad;
            float k = Mathf.Sin(p) * Mathf.Sin(r + a) / (Mathf.Sin(p + a) * Mathf.Sin(r));
            float fade = Mathf.Clamp01((FreeNearFadeTo - h) / (FreeNearFadeTo - closest));
            return d + Mathf.Max(0f, k - 1f) * (closest / Mathf.Sin(p)) * fade;
        }

        /// <summary>The nearest ground on screen (m: the bottom row's middle ray to the y = 0 plane) of the fixed view at this height.</summary>
        public static float FixedNearest(float height, float pitch) => height / Mathf.Sin((pitch + FreeNearHalfFov) * Mathf.Deg2Rad);
    }
}
