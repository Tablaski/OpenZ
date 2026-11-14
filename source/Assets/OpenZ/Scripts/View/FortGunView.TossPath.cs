// OpenZ (GPL-3.0)
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The thrown fort cannon (StartToss, UpdateToss) spun about its seeded axis and, from 60 % of its flight on, was turned toward its landing
    /// pose by Slerp(spin, tossEndRot, w^2). The spin went on while that blend grew, so in almost every flight the spin passed the far side of
    /// the landing pose and the shortest way flipped: the cannon jumped up to 176 degrees in one frame 0.1-1 s before it hit (the blurred ball on
    /// <see cref="TossPathOn"/>: the same spin, carried by ONE fixed correction that grows smoothly over the whole flight: rotation =
    /// Slerp(identity, fix, SmoothStep(k)) x spin, fix = the turn that takes the spin's own end onto tossEndRot. Both ends of the correction
    /// are fixed, so it can never flip; it adds at most a few degrees a frame; on the landing frame the cannon is exactly in tossEndRot (the
    /// pose the rules' heavy fall, the trees under it and the break-up's plates are worked out for) and it still turns at the spin's full rate
    /// as it hits (item 172). Its arc ends where its real lowest vertex lies <see cref="LandClear"/> over the ground (OzHeavyLanding.AimGap):
    /// the break-up's pieces start exactly where the cannon is drawn and none is lifted out of the ground on the landing frame (the whole
    /// cannon was lifted up to 0.2 m in one frame there, more with its neck under it). The landing spot (x, z), the flight time, the apex and
    /// tossEndRot are as before; over open water only the turn changes. View only. Off = the flight as before this kit.
    /// </summary>
    public partial class FortGunView
    {
        public static bool TossPathOn = true;
        /// <summary>The cannon's real lowest vertex this far over the ground on its landing frame (m): the break-up's own 2 cm.</summary>
        public const float LandClear = 0.02f;
        /// <summary>Play check: flights planned, arcs moved, the last arc's move (m) and the last correction's size (deg).</summary>
        public static int TossPaths, TossAims; public static float LastAimMove, LastFixDeg;
        Quaternion tossFix = Quaternion.identity; bool tossPathPlanned;

        /// <summary>Play check: the cannon has landed (its landing frame came).</summary>
        public bool TossLandedNow => landed;
        /// <summary>Play check: the arc's end and the landing pose.</summary>
        public Vector3 TossToNow => tossTo;
        public Quaternion TossEndNow => tossEndRot;

        /// <summary>StartToss, once the landing place, the landing pose, the spin axis and the cannon's renderers are set.</summary>
        void TossPathPlan()
        {
            tossPathPlanned = false;
            if (!TossPathOn || !yaw) return;
            if (!OzWaterLanding.OpenWaterAt(tossTo, out _))
            {
                float gap = OzHeavyLanding.AimGap(yaw, tossTo, tossEndRot, out bool ok);
                if (ok) { LastAimMove = LandClear - gap; tossTo.y += LastAimMove; TossAims++; }
            }
            var spinEnd = Quaternion.AngleAxis(tossSpin * tossDuration, tossAxis) * tossStartRot;
            tossFix = tossEndRot * Quaternion.Inverse(spinEnd);
            LastFixDeg = Quaternion.Angle(Quaternion.identity, tossFix);
            tossPathPlanned = true; TossPaths++;
        }

        bool TossPathUse => TossPathOn && tossPathPlanned;

        /// <summary>UpdateToss: the turn drawn at flight share k for the free spin `spin` (exactly tossEndRot on the landing frame).</summary>
        Quaternion TossPathRot(Quaternion spin, float k)
        {
            if (k >= 1f) return tossEndRot;
            return Quaternion.Slerp(Quaternion.identity, tossFix, Mathf.SmoothStep(0f, 1f, k)) * spin;
        }
    }
}
