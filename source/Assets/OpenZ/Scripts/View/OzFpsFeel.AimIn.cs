using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only.
    /// <para>Before: the glide turned the camera from the RTS camera's rotation to the eye's (the unit's front, level) with one slerp over the
    /// whole way, so half way in it looked sideways at the horizon and the unit left the picture.</para>
    /// <para>Now the camera aims at the unit all the way: its line of sight goes from where it is to a point <see cref="AimLeadM"/> past the
    /// eye along the eye's own look (at the start that point lies just behind the unit, so the unit is in the middle of the picture; at the
    /// eye it is exactly the eye's look, so the end is seamless). The place still moves on a straight line, eased as before. Over the first
    /// <see cref="AimEaseShare"/> of the way the RTS camera's own rotation eases into that aim (the way out ends on the RTS camera's own pose
    /// the same way). And the view's first look (OzFpsView.Begin) is along the way in (from the camera to the unit, level) instead of the
    /// unit's front, so the camera never turns sideways at the end either: it dives in and levels out (a flying shuttle keeps its nose's look).
    /// <see cref="AimOn"/> off = the old slerp and the unit's front.</para>
    /// </summary>
    public sealed partial class OzFpsFeel
    {
        /// <summary>N15: the glide aims at the unit (on).</summary>
        public static bool AimOn = true;
        /// <summary>N15: the aim point lies this far (m) past the eye along the eye's look; the share of the way (0..1) over which the RTS
        /// camera's own rotation eases into the aim.</summary>
        public static float AimLeadM = 4f, AimEaseShare = 0.15f;
        /// <summary>N15 proof: glides in whose first look was set along the way in; the last approach yaw (deg).</summary>
        public static int AimYawsSet;
        public static float LastAimYaw;

        /// <summary>The glide's rotation at share s, the camera at gp: aimed through the unit to the point AimLeadM past the eye along its look.</summary>
        static Quaternion AimRot(Pose a, Pose e, Vector3 gp, float s)
        {
            var fwd = e.rotation * Vector3.forward;
            var d = e.position + fwd * AimLeadM - gp;
            if (d.sqrMagnitude < 1e-6f) return e.rotation;
            var aim = Quaternion.LookRotation(d, Vector3.up);
            float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s / Mathf.Max(1e-3f, AimEaseShare)));
            return Quaternion.Slerp(a.rotation, aim, w);
        }

        /// <summary>OzFpsView.Begin, right after it set the first look along the unit's front (yaw, deg): a glide in that starts now looks along the
        /// way in instead (from the camera, still at the RTS pose, to the unit; level). A turn round, a view with no glide, a flying shuttle: as it was.</summary>
        public static float ApproachYaw(Camera c, UnitView v, ZObject u, float yaw)
        {
            var f = inst;
            if (!AimOn || !f || f.legacy || !GlideOn || !f.gliding || f.dir <= 0 || f.pendingLook || !c || !v) return yaw;
            if (u != null && OzFpsShuttleView.Flies(u)) return yaw;
            var d = v.transform.position - c.transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.25f) return yaw;
            AimYawsSet++;
            return LastAimYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }
    }
}
