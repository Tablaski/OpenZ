using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.4 kit perf_ground_grid (View/ZGame.GroundGrid.cs): what the ground grid needs from the deformable ground.
    ///  - A crater holds its area on the grid from its spawn until it is gone (Spawn and Kill call GridHold / GridRelease): there GroundY casts its ray, as
    ///    before, through the whole life of the crater (easing in, the chunk swap, the live bowl, the removal). The area: the bowl, and every terrain chunk
    ///    triangle that touches the crater's square (CutMesh replaces exactly those with a 0.5 m grid on the same surface), widened by that grid's step.
    ///  - The grid waits for the deformable ground to be ready (its chunks replace the terrain collider on the walk layer), and starts again when it becomes
    ///    ready later (switched on in a match).
    ///  - The bowl colliders are not terrain for the grid's groups.
    /// </summary>
    public sealed partial class CraterField
    {
        /// <summary>CutMesh's grid step (m) and a little.</summary>
        const float GridCutStep = 0.55f;

        void GridHold(Crater c)
        {
            float R = c.v.R;
            float x0 = c.c.x - R, x1 = c.c.x + R, z0 = c.c.z - R, z1 = c.c.z + R;
            float bx0 = x0, bx1 = x1, bz0 = z0, bz1 = z1;
            if (chunkVerts != null)
                foreach (var ch in c.chunks)
                {
                    if (ch == null || ch.tris == null) continue;
                    for (int t = 0; t + 2 < ch.tris.Length; t += 3)
                    {
                        Vector3 a = chunkVerts[ch.tris[t]], b = chunkVerts[ch.tris[t + 1]], d = chunkVerts[ch.tris[t + 2]];
                        float tx0 = Mathf.Min(a.x, Mathf.Min(b.x, d.x)), tx1 = Mathf.Max(a.x, Mathf.Max(b.x, d.x));
                        float tz0 = Mathf.Min(a.z, Mathf.Min(b.z, d.z)), tz1 = Mathf.Max(a.z, Mathf.Max(b.z, d.z));
                        if (tx1 < x0 || tx0 > x1 || tz1 < z0 || tz0 > z1) continue;                       // CutMesh's own test, with the edges counted in
                        if (tx0 < bx0) bx0 = tx0; if (tx1 > bx1) bx1 = tx1; if (tz0 < bz0) bz0 = tz0; if (tz1 > bz1) bz1 = tz1;
                    }
                }
            ZGame.GroundGridHold(c, bx0 - GridCutStep, bz0 - GridCutStep, bx1 + GridCutStep, bz1 + GridCutStep);
        }

        void GridRelease(Crater c) => ZGame.GroundGridRelease(c);

        /// <summary>A grid made while craters stand (made after them): each of them holds its ground on it.</summary>
        internal static void GridHoldAll() { if (I != null) foreach (var c in I.craters) if (!c.dead) I.GridHold(c); }

        /// <summary>A crater's bowl collider (its own group on the grid, never the terrain's).</summary>
        internal static bool IsBowl(Collider c) => c != null && craterCols.Contains(c);

        /// <summary>The deformable ground is on and not ready yet: the grid is not made until it is (its chunks are about to replace the terrain collider).</summary>
        internal static bool GridWaiting => I != null && !I.ready && !I.failed && Enabled;

        /// <summary>Checks only (the ground grid's counted check): a crater at this ground point, the site rules as for a blast, no chance roll. Null = made,
        /// else why not.</summary>
        internal static string GridCheckCrater(Vector3 at, float s = 1.7f)
        {
            if (!Enabled) return "deformable ground is OFF (Options)";
            var f = Ensure();
            if (!f.ready || ZGame.I == null) return "not ready";
            return f.TrySpawn(at, s, "ground grid check", true);
        }

        /// <summary>Checks only: craters standing now (made, not gone), and how many of them have their live bowl.</summary>
        internal static int GridCheckCraters(out int live)
        {
            live = 0; int n = 0;
            if (I == null) return 0;
            foreach (var c in I.craters) if (!c.dead) { n++; if (c.live) live++; }
            return n;
        }

        /// <summary>Checks only: every crater's centre and radius.</summary>
        internal static System.Collections.Generic.List<Vector4> GridCheckCraterSpots()
        {
            var l = new System.Collections.Generic.List<Vector4>();
            if (I != null) foreach (var c in I.craters) if (!c.dead) l.Add(new Vector4(c.c.x, c.c.y, c.c.z, c.v.R));
            return l;
        }

        /// <summary>Checks only: every crater goes at once (the option's own clear).</summary>
        internal static void GridCheckClear() { if (I != null) I.ClearAll("ground grid check"); }

        /// <summary>Checks only: the deformable ground has its chunk colliders.</summary>
        internal static bool GridCheckPhysics => I != null && I.physicsOk;
    }
}
