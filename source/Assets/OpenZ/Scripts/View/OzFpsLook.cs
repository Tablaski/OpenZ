// OpenZ (GPL-3.0) - version 1.5 kit fps_look_smooth: a very light easing of the private first-person view's mouse look (View/OzFpsView.cs).
// View only: no rule, no Sim file, no DV row.
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// <para>How: OzFpsView keeps its look (yaw, pitch) as before; the mouse moves it at once, and the drawn look trails it by a small lag (deg)
    /// that falls off exponentially with the time constant <see cref="Tau"/>: lag *= exp(-dt / Tau) once a frame on unscaled time. That is
    /// exactly the smoothing drawn = drawn + (look - drawn) * (1 - exp(-dt / Tau)), so it is the same at any frame rate, never overshoots (the
    /// drawn look is always between the old and the new look; the pitch stays inside the view's limits) and ends exactly on the look: a lag
    /// under <see cref="SnapDeg"/> is taken as 0. The lag is kept unwrapped (the mouse's own travel), so a yaw through +-180 eases the short
    /// way it was turned.</para>
    /// <para>What uses the drawn look: the camera's rotation (and with it the crosshair at the screen's centre, the fire pick
    /// PlayerInput.FpsTargetAtCentre, which casts from the camera, and the gun copy, a child of the camera) and the walk's direction. So the aim
    /// is always what the player sees.</para>
    /// <para>No lag (drawn = look at once): on entering the view (OzFpsView.Begin), for a look set by code (OzFpsView.LookBy: the Play checks,
    /// the glide's turn round), during the fps_feel glide (the glide is its own easing; a look made during the glide in is drawn at once, so
    /// at the end of the glide the lag is 0 and nothing drifts), while a 1.4 Play check of the view runs (OzFpsFeel.Legacy: the 1.4 view
    /// exactly) and with <see cref="On"/> off. The RTS camera is not touched. It runs the same in a level, in the Unit editor (unscaled time,
    /// also while the playground is paused) and on the shuttle.</para>
    /// <para>The numbers: Tau 40 ms. A flick is drawn 63 % of the way after 40 ms, 95 % after 120 ms and exactly there after about 0.4 s
    /// (a 30 deg flick); at 60 fps the first frame moves 34 % of the way. Turning the mouse steadily, the view trails by speed x Tau
    /// (90 deg/s: 3.6 deg).</para>
    /// </summary>
    public static class OzFpsLook
    {
        /// <summary>Switch for a before / after look (on in the game).</summary>
        public static bool On = true;

        /// <summary>The easing's time constant (s).</summary>
        public const float Tau = 0.04f;

        /// <summary>A lag under this (deg) is 0: the drawn look ends exactly on the look.</summary>
        public const float SnapDeg = 0.001f;

        /// <summary>The largest lag of the last frame (deg; 0 = the drawn look is the look). For the Play check and the log.</summary>
        public static float LagDeg { get; private set; }

        /// <summary>The easing is used now: on, not in a 1.4 Play check, not during the glide.</summary>
        public static bool Smooths => On && !OzFpsFeel.Legacy && !OzFpsFeel.Gliding;

        /// <summary>OzFpsView.MouseLook: the look moved by the mouse (dYaw unwrapped, dPitch after the clamp, deg); the drawn look stays where it
        /// was this instant and eases after it (<see cref="Ease"/>).</summary>
        public static void Trail(ref float lagYaw, ref float lagPitch, float dYaw, float dPitch)
        {
            if (!Smooths) { lagYaw = lagPitch = 0f; return; }
            lagYaw += dYaw; lagPitch += dPitch;
        }

        /// <summary>OzFpsView.PlaceCamera, once a frame (dt unscaled): the lag falls off. snap = the view's first pose (no lag).</summary>
        public static void Ease(ref float lagYaw, ref float lagPitch, float dt, bool snap)
        {
            if (snap || !Smooths) { lagYaw = lagPitch = 0f; LagDeg = 0f; return; }
            float keep = Mathf.Exp(-Mathf.Max(0f, dt) / Tau);
            lagYaw *= keep; lagPitch *= keep;
            if (Mathf.Abs(lagYaw) < SnapDeg) lagYaw = 0f;
            if (Mathf.Abs(lagPitch) < SnapDeg) lagPitch = 0f;
            LagDeg = Mathf.Max(Mathf.Abs(lagYaw), Mathf.Abs(lagPitch));
        }
    }
}
