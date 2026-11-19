using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// line of the jump, the walk on (ExitWalkSmooth: the figure's facing blended into his robot's own facing as the short walk ends) turned the
    /// figure up to 116 deg in ONE frame at the first frame of the walk (blue heavy exit: 5.9 -> 249.5 deg), so his legs, still in the landing
    /// stance, were seen from a new side at once. Now a hatch-exit figure's drawn facing turns at most <see cref="ExitTurnDegPerSec"/> (a brisk
    /// human turn on the spot), and the hand-over to his robot waits until he faces its way (within <see cref="ExitTurnDoneDeg"/>; at most
    /// <see cref="ExitTurnMaxSec"/> past the walk's end), pivoting on his idle stance once he stands on the spot. Every other figure (getting in, the other exits) is
    /// unchanged. <see cref="ExitTurnEaseOn"/> off = as before.
    /// </summary>
    public partial class BoardingView
    {
        /// <summary>N2: a hatch-exit figure turns at a human rate (on).</summary>
        public static bool ExitTurnEaseOn = true;
        /// <summary>N2: its largest turn (deg/s), the facing gap the hand-over waits for (deg), the longest wait past the walk's end (s), and how near his
        /// robot's spot (m) he pivots on the idle stance instead of stepping.</summary>
        public static float ExitTurnDegPerSec = 420f, ExitTurnDoneDeg = 8f, ExitTurnMaxSec = 0.6f, ExitPivotM = 0.2f;
        /// <summary>N2 proof: frames a turn was eased, hand-overs held for the turn, the largest one-frame turn drawn (deg).</summary>
        public static int ExitTurnEased, ExitTurnHeldFrames;
        public static float ExitTurnMaxStepDeg;
        readonly Dictionary<Ghost, (Quaternion rot, int frame)> hatchTurnLast = new Dictionary<Ghost, (Quaternion, int)>();

        /// <summary>A figure of a hatch leap on its walk on (not getting in, not a demo).</summary>
        bool HatchWalker(Ghost g, out HatchExit st)
        {
            st = null;
            return ExitTurnEaseOn && g != null && !g.enter && !g.demo && hatchExits.Count > 0 && hatchExits.TryGetValue(g, out st) && st != null && !st.dead && st.phase == JumpPhase.Walk;
        }

        /// <summary>ExitWalkSmooth, right after it drew the figure: its turn since the last frame held to ExitTurnDegPerSec (from the facing it was
        /// drawn with last frame; on the walk's first frame from the landing's facing). placed = as Place drew it this frame (not used: kept for the proof).</summary>
        void HatchTurnEase(Ghost g, Transform tr, Quaternion placed, float dt)
        {
            if (!tr || dt <= 0f || !HatchWalker(g, out var st)) return;
            var prev = hatchTurnLast.TryGetValue(g, out var last) && Time.frameCount - last.frame <= 2 ? last.rot : HatchFace(st.dir);   // the walk's first frame: from the landing's facing (drawn last frame)
            var want = tr.rotation;
            var now = Quaternion.RotateTowards(prev, want, ExitTurnDegPerSec * dt);
            if (Quaternion.Angle(now, want) > 0.01f) { tr.rotation = now; ExitTurnEased++; }
            ExitTurnMaxStepDeg = Mathf.Max(ExitTurnMaxStepDeg, Quaternion.Angle(prev, tr.rotation));
            hatchTurnLast[g] = (tr.rotation, Time.frameCount);
            if (hatchTurnLast.Count > 64) hatchTurnLast.Clear();
        }

        /// <summary>ExitWalkSmooth: he stands on his robot's spot (within <see cref="ExitPivotM"/>) and still turns to its facing: the idle stance pivots
        /// (the walk clip's steps would cross his legs on the spot).</summary>
        bool HatchTurnInPlace(Ghost g)
        {
            if (!HatchWalker(g, out _) || !g.go || !g.live) return false;
            var a = g.go.transform.position; var b = g.live.transform.position; a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b) <= ExitPivotM && Quaternion.Angle(g.go.transform.rotation, g.live.transform.rotation) > ExitTurnDoneDeg;
        }

        /// <summary>ExitWalkReached: true = the hand-over waits (he still turns to his robot's facing).</summary>
        bool HatchTurnHolds(Ghost g)
        {
            if (!HatchWalker(g, out _) || !g.go || !g.live) return false;
            if (g.time - g.total > ExitTurnMaxSec) return false;
            if (Quaternion.Angle(g.go.transform.rotation, g.live.transform.rotation) <= ExitTurnDoneDeg) return false;
            ExitTurnHeldFrames++;
            return true;
        }
    }
}
