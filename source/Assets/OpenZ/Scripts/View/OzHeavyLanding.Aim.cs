// OpenZ (GPL-3.0) - 1.3, open_fixes_2nov wreck_toss_landing: where a thrown piece's arc must end so that it touches the ground on its landing frame.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// A wreck top (the turret of a tank or of a gun, the fort's cannon) flies a scripted arc and lands in a pose that is known before it lands.
    /// The arc used to end at a fixed height (a turret's pivot 0.35 m over the ground, the cannon's renderer box 0.2 m in), so on the landing
    /// frame a turret lying on its side was drawn up to a metre and more in the ground and the landing fit lifted it out in 0.08 s (a jump), and
    /// the cannon's break-up lifted all its pieces as one out of the ground on that frame. <see cref="AimGap"/> measures, before the flight, how
    /// far the real lowest vertex of the piece would lie over the ground (+) or in it (-) if it were drawn at `at` turned `rot`: the same
    /// points the landing fit uses (each readable mesh's used vertices, else its box corners counted 15 cm higher), the ground from a 0.5 m
    /// lattice for all of them and the exact probe (ZGame.GroundY) for the 24 lowest. The caller moves the arc's end by it. Nothing is moved
    /// here and nothing is kept. View only.
    /// </summary>
    public partial class OzHeavyLanding
    {
        /// <summary>Calls (Play check).</summary>
        public static int Aims;
        static readonly List<Vector4> aimPts = new List<Vector4>();
        static readonly List<float> aimLow = new List<float>();
        static readonly List<int> aimIdx = new List<int>(), aimOrder = new List<int>();
        static readonly Dictionary<long, float> aimLattice = new Dictionary<long, float>();

        /// <summary>The real lowest vertex of `piece` drawn at `at` turned `rot`: its height over the ground under it (m, + = over, - = in);
        /// ok = false when there is no ground or nothing drawn to measure.</summary>
        public static float AimGap(Transform piece, Vector3 at, Quaternion rot, out bool ok)
        {
            ok = false; var g = ZGame.I;
            if (!piece || !g) return 0f;
            var move = Matrix4x4.TRS(at, rot, Vector3.one) * Matrix4x4.TRS(piece.position, piece.rotation, Vector3.one).inverse;
            aimPts.Clear(); aimLattice.Clear();
            float minY = float.MaxValue, x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var r in piece.GetComponentsInChildren<Renderer>())
            {
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r is BillboardRenderer || r is SpriteRenderer) continue;
                Vector3[] pts; float sink = Sink; Matrix4x4 m;
                if (r is MeshRenderer)
                {
                    var mf = r.GetComponent<MeshFilter>(); var mesh = mf ? mf.sharedMesh : null;
                    if (!mesh) continue;
                    pts = Points(mesh);
                    if (pts == null) { pts = Corners(mesh.bounds); sink = BoxSink; }
                    m = move * r.transform.localToWorldMatrix;
                }
                else { pts = Corners(r.bounds); sink = BoxSink; m = move; }
                for (int i = 0; i < pts.Length; i++)
                {
                    var q = m.MultiplyPoint3x4(pts[i]);
                    aimPts.Add(new Vector4(q.x, q.y, q.z, sink));
                    if (q.y < minY) minY = q.y;
                    if (q.x < x0) x0 = q.x;
                    if (q.x > x1) x1 = q.x;
                    if (q.z < z0) z0 = q.z;
                    if (q.z > z1) z1 = q.z;
                }
            }
            if (aimPts.Count == 0) return 0f;
            // only points within the ground's own rise under the piece of the lowest one can touch first (as Lift)
            float a = AimGround(g, x0, z0), b = AimGround(g, x1, z0), c = AimGround(g, x0, z1), d = AimGround(g, x1, z1), e = AimGround(g, (x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
            float band = Mathf.Max(Mathf.Max(a, b), Mathf.Max(Mathf.Max(c, d), e)) - Mathf.Min(Mathf.Min(a, b), Mathf.Min(Mathf.Min(c, d), e)) + 0.4f + (BoxSink - Sink);
            aimLow.Clear(); aimIdx.Clear();
            for (int i = 0; i < aimPts.Count; i++)
            {
                var q = aimPts[i]; if (q.y > minY + band) continue;
                aimLow.Add(q.y - AimGround(g, q.x, q.z) + (q.w - Sink));                                   // a box corner stands off the real shape: 15 cm higher
                aimIdx.Add(i);
            }
            if (aimIdx.Count == 0) return 0f;
            // the 24 lowest by the lattice, each again with the exact probe under it
            aimOrder.Clear(); for (int i = 0; i < aimIdx.Count; i++) aimOrder.Add(i);
            aimOrder.Sort((p, q) => aimLow[p].CompareTo(aimLow[q]));
            float best = float.MaxValue;
            for (int n = 0; n < aimOrder.Count && n < 24; n++)
            {
                var q = aimPts[aimIdx[aimOrder[n]]];
                float gap = q.y - g.GroundY(q.x, q.z) + (q.w - Sink);
                if (gap < best) best = gap;
            }
            ok = best < float.MaxValue; Aims++;
            return ok ? best : 0f;
        }

        static float AimGround(ZGame g, float x, float z)
        {
            float fx = x / Cell, fz = z / Cell; int i = Mathf.FloorToInt(fx), j = Mathf.FloorToInt(fz); float u = fx - i, w = fz - j;
            return Mathf.Lerp(Mathf.Lerp(AimNode(g, i, j), AimNode(g, i + 1, j), u), Mathf.Lerp(AimNode(g, i, j + 1), AimNode(g, i + 1, j + 1), u), w);
        }

        static float AimNode(ZGame g, int i, int j)
        {
            long key = ((long)i << 32) ^ (uint)j;
            if (!aimLattice.TryGetValue(key, out float h)) { h = g.GroundY(i * Cell, j * Cell); aimLattice[key] = h; }
            return h;
        }
    }
}
