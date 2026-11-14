using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 owner item 146 for the mech ("tracks never wobble ... and remain on ground": the mech's tracks are its feet), v1 AND v2 (one walk
    /// code), after the mech v2 Play checks (the walk film: v2 327 and v1 47 planted foot-frames more than 3 cm under where the walk stands the
    /// sole; v2's resting soles 3.6 cm over and 4.5 cm under the ground for the whole game; a standing foot dragged up to 64-77 cm). Causes and fixes:
    ///  * THE ANKLE HEIGHT read at Start was the WORLD height of each ankle over the unit's root: a mech spawned on a slope (tilted root) kept
    ///    one sole 3.6 cm over and the other 4.5 cm in the ground for life. Now it is the ankle's height in the unit's own frame (tilt-free).
    ///  * A SOLE ON UNEVEN GROUND: the planted plate was turned to the ground normal sampled 0.35 m round the ankle and set at the ankle height
    ///    over the ground under the ankle; a long sole on a crease or a curved bank sank 3.6-10 cm for a WHOLE stance (the film: constant per
    ///    stance, walking straight). Now the sole rests on the ground: at a plant the ankle's height is set so that the sole's lowest point
    ///    (its own outline from the foot mesh: <see cref="SoleDirs"/> directions, half-way points and its centre, turned to the plate's normal and
    ///    the foot's yaw) stands exactly at its design height over the ground under it (v2 0, v1's 3.5 cm as v1 always stood); the swinging
    ///    foot eases from its old height to the one it will land at; the pelvis follows. On a ridge the sole rests on the ridge, never through it.
    ///  * A FOOT ABOUT TO BE DRAGGED (past 0.95 of its reach while walking and turning) waited for the other foot's 0.06 s landing rest (the
    ///    turn steps' fairness); now it lifts as soon as the other foot stands (<see cref="LiftBeforeDrag"/>).
    ///  * THE HIP ROLL LIMIT: turning fast while walking, a standing foot ends up to 34 deg out of its leg's plane; at 18 deg the clamped
    ///    roll left the drawn ankle off its spot (slid 7-29 cm, dipped 1-6 cm): MechWalk.MaxRoll is 30 deg (the legs' clearance unchanged).
    /// Measured off the engine with the walk's numpy port (walking, firing, turning in place and walking turns at 140 deg/s on flat, 12 deg,
    /// side, bumpy and creased ground): the shared walk script v2 0 standing soles more than 3 cm off (was 2), drift 0 cm (was 11); walking
    /// turns v1 51 -> 5, v2 31 -> 10 frames, no drag left on v2. View only. Switches: <see cref="SoleOnGround"/>, <see cref="DragLift"/>
    /// (off = the walk as before, statement for statement; MaxRoll is a tuning number).
    /// </summary>
    public partial class MechWalk
    {
        /// <summary>The switch for the tilt-free ankle height and the sole resting on the ground under it (off = as before).</summary>
        public static bool SoleOnGround = true;
        /// <summary>The switch for lifting a foot about to be dragged as soon as the other foot stands (off = as before).</summary>
        public static bool DragLift = true;
        /// <summary>The sole's outline: directions sampled round it (each: its farthest bottom point and the point half-way to it from the centre).</summary>
        public static int SoleDirs = 16;
        /// <summary>Statistics (the Play checks): plants set on their sole, the largest lift over / under the ankle's plain height (m).</summary>
        public static int SolePlants;
        public static float SoleLiftMaxM;

        Vector3[][] soleOff; float[] soleDepth, soleLift, soleLiftYaw; Vector3[] soleLiftAt, soleLiftN;

        int Ix(Leg l) => legs != null && legs.Length > 0 && legs[0] == l ? 0 : 1;

        /// <summary>Start: the ankle's height over the soles' plane in the unit's own frame (with the switch off: the world height, as before).</summary>
        float K92AnkleH(Leg g)
        {
            if (!SoleOnGround || !body) return g.ankle.position.y - body.position.y;
            return body.InverseTransformPoint(g.ankle.position).y * Mathf.Abs(body.lossyScale.y);
        }

        /// <summary>Start: the sole of this leg's foot (Mech_footL / _R, its mesh's bottom: the vertices within 2 cm of its lowest, as the proofs
        /// read it) as offsets from the ankle in the unit's frame (metres); none (no lift) when the mesh cannot be read.</summary>
        void K92Sole(Leg g)
        {
            if (soleOff == null) { soleOff = new Vector3[2][]; soleDepth = new float[2]; soleLift = new float[2]; soleLiftYaw = new float[2]; soleLiftAt = new Vector3[2]; soleLiftN = new Vector3[2]; }
            int i = Ix(g);
            soleOff[i] = null; soleLift[i] = 0f; soleLiftAt[i] = new Vector3(float.NaN, 0f, 0f);
            string n = "Mech_foot" + (g.left ? "L" : "R"); Transform foot = null;
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) { foot = t; break; }
            var mf = foot ? foot.GetComponent<MeshFilter>() : null;
            if (!body || !mf || !mf.sharedMesh || !mf.sharedMesh.isReadable) return;
            var vs = mf.sharedMesh.vertices; if (vs.Length == 0) return;
            var sc = new Vector3(Mathf.Abs(body.lossyScale.x), Mathf.Abs(body.lossyScale.y), Mathf.Abs(body.lossyScale.z)); var a = g.ankle.position;
            var off = new Vector3[vs.Length]; float lo = float.MaxValue;
            for (int k = 0; k < vs.Length; k++) { off[k] = Vector3.Scale(body.InverseTransformVector(foot.TransformPoint(vs[k]) - a), sc); lo = Mathf.Min(lo, off[k].y); }
            var bottom = new List<Vector3>(); var c = Vector3.zero;
            foreach (var o in off) if (o.y < lo + 0.02f) { bottom.Add(o); c += o; }
            c /= bottom.Count;
            var pts = new List<Vector3> { c };
            for (int d = 0; d < SoleDirs; d++)
            {
                float ang = d * 2f * Mathf.PI / SoleDirs, dx = Mathf.Cos(ang), dz = Mathf.Sin(ang), best = float.NegativeInfinity; var far = c;
                foreach (var o in bottom) { float s = o.x * dx + o.z * dz; if (s > best) { best = s; far = o; } }
                pts.Add(far); pts.Add(0.5f * (c + far));
            }
            float depth = float.NegativeInfinity; foreach (var p in pts) depth = Mathf.Max(depth, -p.y);
            soleOff[i] = pts.ToArray(); soleDepth[i] = depth;
        }

        /// <summary>The ankle's extra height (m, + up) that stands the sole's lowest point at its design height over the ground under it, the plate
        /// turned to `n` and the foot's world yaw `yaw` (deg), the ankle over the ground point P.</summary>
        float LiftAt(int i, ZGame g, Vector3 P, Vector3 n, float yaw)
        {
            var pts = soleOff != null ? soleOff[i] : null;
            if (!SoleOnGround || pts == null || g == null) return 0f;
            var R = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.AngleAxis(yaw, Vector3.up);
            float best = float.NegativeInfinity;
            foreach (var o in pts) { var h = R * o; best = Mathf.Max(best, g.GroundY(P.x + h.x, P.z + h.z) - P.y - h.y); }
            return best - soleDepth[i];
        }

        /// <summary>A planted foot (its plant, plate normal and yaw): the lift, worked out again only when the plant changed (a landing, a crater,
        /// a drag).</summary>
        float SoleLift(ZGame g, Leg l, Vector3 P, Vector3 n, float yaw)
        {
            if (!SoleOnGround || soleOff == null) return 0f;
            int i = Ix(l);
            if ((P - soleLiftAt[i]).sqrMagnitude < 1e-8f && Mathf.Abs(yaw - soleLiftYaw[i]) < 0.01f && n == soleLiftN[i]) return soleLift[i];
            soleLiftAt[i] = P; soleLiftYaw[i] = yaw; soleLiftN[i] = n;
            soleLift[i] = LiftAt(i, g, P, n, yaw);
            SolePlants++; SoleLiftMaxM = Mathf.Max(SoleLiftMaxM, Mathf.Abs(soleLift[i]));
            return soleLift[i];
        }

        /// <summary>A swinging foot: from the lift it stood at to the lift it will land at (its aim, the aim's normal, the yaw it lands with),
        /// eased like its path (e).</summary>
        float SwingLift(ZGame g, Leg l, float e, float yaw)
        {
            if (!SoleOnGround || soleOff == null) return 0f;
            int i = Ix(l);
            float sc = Mathf.Max(0.05f, body.lossyScale.y);
            return Mathf.Lerp(soleLift[i], LiftAt(i, g, l.aim, Normal(g, l.aim, 0.35f * sc), yaw), e);
        }

        /// <summary>The pick: a planted foot past 0.95 of its reach may lift as soon as the other foot stands (no 0.06 s landing rest for it).</summary>
        bool LiftBeforeDrag(Leg l, Leg o, float hipDist, float reachH) => DragLift && o.planted && hipDist > 0.95f * reachH && l.plantedFor > 0.06f;
    }
}
