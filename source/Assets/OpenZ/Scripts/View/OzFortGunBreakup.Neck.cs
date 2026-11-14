using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// landing frame the trunk breaks in two at a bend about the head's hinge line (neck_a, the upper part; neck_b, the foot with the deck collars and the body
    /// stubs), each closed with the broken-steel face; the four ram sections come loose whole (neck_stage_l / _r, neck_rod_l / _r); the head's collar rides on
    /// older cut: every older piece keeps its numbers. The pieces wear the gun's own paint and the rams' steel; they tumble, rest and sink like the others.
    /// </summary>
    public partial class OzFortGunBreakup
    {
        /// <summary>The neck's transforms and mesh, read on the main thread.</summary>
        sealed class NeckRead
        {
            public Transform baseT, collarT; public Mesh baseMesh; public string baseName; public float sc;
            public readonly List<(string name, Transform t, Mesh m)> wholes = new List<(string, Transform, Mesh)>();
        }

        /// <summary>The neck under `yaw` (null: no neck).</summary>
        static NeckRead ReadNeck(Transform yaw)
        {
            var b = yaw ? yaw.Find(FortGunNeck.Prefix + "Base") : null;
            if (!b || !b.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh || !b.GetComponent<MeshRenderer>()) return null;
            var n = new NeckRead { baseT = b, baseMesh = mf.sharedMesh, baseName = mf.sharedMesh.name, sc = MeshScale(b) };
            var head = Find(yaw, "FortGunHead"); var col = head ? head.Find(FortGunNeck.Prefix + "Collar") : null;
            if (col && col.GetComponent<MeshRenderer>() && col.GetComponent<MeshFilter>()) n.collarT = col;
            foreach (var k in new[] { "Stage_L", "Stage_R", "Rod_L", "Rod_R" })
            {
                var t = yaw.Find(FortGunNeck.Prefix + k);
                if (t && t.TryGetComponent<MeshFilter>(out var f) && f.sharedMesh && t.GetComponent<MeshRenderer>()) n.wholes.Add(("neck_" + k.ToLowerInvariant(), t, f.sharedMesh));
            }
            return n;
        }

        /// <summary>The trunk broken at a bend 8-18 deg up its arc (the plane through the hinge line; fractured when the look is on), its own numbers.
        /// upper = null: no break (the whole base is `lower`). n = the plane's normal (the upper part's outward side).</summary>
        static void NeckCuts(OzMeshSlice.Part bp, int seed, float sc, out OzMeshSlice.Part upper, out OzMeshSlice.Part lower, out Vector3 n)
        {
            var rn = new System.Random(seed * 3571 + 34);
            float th = (8f + (float)rn.NextDouble() * 10f) * Mathf.Deg2Rad;
            n = new Vector3(0f, Mathf.Cos(th), -Mathf.Sin(th));
            float w = Vector3.Dot(n, new Vector3(0f, FortGunNeck.HingeY, FortGunNeck.HingeZ));
            if (FSplit(bp, n, w, out var a, out var b, sc, FractureLook ? rn : null) && a != null && b != null && a.Tris > 0 && b.Tris > 0) { upper = a; lower = b; }
            else { upper = null; lower = bp; }
        }

        /// <summary>The head's lid plate stays on the ring mesh since the neck kit (FortGunView.RingLid) and is welded to the ring's pit, so it would sit in the ring's
        /// box and lift the ring's closing plane over the ring's own top (the rear deck opening would stay open). Its triangles (centre above splitY, mesh y, the
        /// same rule as FortGunView.SplitHead) go to `toRing`, merged after the closing exactly as when they came with the head; returns the ring without them.
        /// </summary>
        static OzMeshSlice.Part TakeLid(OzMeshSlice.Part p, float splitY, List<OzMeshSlice.Part> toRing)
        {
            if (p == null) return null;
            var ring = p.Like(); var lid = p.Like(); var mr = new Dictionary<int, int>(); var ml = new Dictionary<int, int>();
            for (int s = 0; s < p.sub.Length; s++)
            {
                var t = p.sub[s];
                for (int k = 0; k + 2 < t.Count; k += 3)
                {
                    bool up = (p.v[t[k]].p.y + p.v[t[k + 1]].p.y + p.v[t[k + 2]].p.y) / 3f > splitY;
                    var dst = up ? lid : ring; var map = up ? ml : mr;
                    for (int j = 0; j < 3; j++)
                    {
                        int i = t[k + j];
                        if (!map.TryGetValue(i, out int d)) { d = dst.v.Count; dst.v.Add(p.v[i]); map[i] = d; }
                        dst.sub[s].Add(d);
                    }
                }
            }
            if (lid.Tris == 0 || ring.Tris == 0) return p;
            toRing.Add(lid);
            return ring;
        }

        static string NeckText(NeckRead nk, bool broke) =>
            $"neck: trunk {(broke ? "broken in two" : "whole")}, ram sections {nk.wholes.Count}, head collar {(nk.collarT ? "rides on the head" : "none")}; ";
    }
}
