// OpenZ (GPL-3.0)
// View only.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>1.5 kit robot_pieces_ground_r11b (A35): a robot pop piece's rest (round 10's FitRest) is laid on the REAL surface under its own drawn
    /// points: round 10 put its box face GroundY + half its box over the ground at its middle, so a piece whose middle lay over a kerb, a rail's foot or
    /// a step of the Unit editor's platform deck rested on that height while its drawn points lay over the lower deck (the round 11 check: 3.7 and 7.3 cm
    /// up on the 4.5 m deck). Now the piece as it will lie (its rest pose) is measured by its drawn points (OzDrawnPoints) against the support right under
    /// each (OzRestSurface.Gap: rays down to the colliders) and its rest point moved so its lowest drawn point lies LiftM over it. A piece that lands off
    /// the map is popped away at once (out of sight). Switch OzRestSurface.On.</summary>
    public partial class RobotPopDeath
    {
        static readonly List<Vector3> rsBuf = new List<Vector3>(256);

        /// <summary>FitRest's last step: the rest point's height from the drawn points over the real surface.</summary>
        void FitRestSurface(Piece p)
        {
            if (!OzRestSurface.On || p == null || !p.t || !p.mt || !p.mf || !p.mf.sharedMesh) return;
            if (!OzRestSurface.InMap(p.restP, 0f)) { p.life = Mathf.Min(p.life, p.age + 0.3f); OzRestSurface.OffMap++; return; }
            var pts = OzDrawnPoints.Rigid(p.mf.sharedMesh); if (pts == null || pts.Length == 0) return;
            var m = Matrix4x4.TRS(p.restP, p.restQ, p.t.localScale) * Matrix4x4.TRS(p.mt.localPosition, p.mt.localRotation, p.mt.localScale);
            rsBuf.Clear(); for (int i = 0; i < pts.Length; i++) rsBuf.Add(m.MultiplyPoint3x4(pts[i]));
            float gap = OzRestSurface.Gap(rsBuf, p.t, OzBodyGround.WallM, out _, out _);
            if (float.IsNaN(gap)) return;
            float dy = Mathf.Clamp(OzRestSurface.LiftM - gap, -OzBodyGround.MaxDownM, OzRestSurface.MaxRaiseM);
            if (Mathf.Abs(dy) < 0.002f) return;
            p.restP.y += dy; OzRestSurface.PiecesSeated++;
        }
    }
}
