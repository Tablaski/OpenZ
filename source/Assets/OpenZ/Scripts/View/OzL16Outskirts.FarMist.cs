// OpenZ (GPL-3.0) - Level 16: the mist beyond the rim without a hard end, and the ground a drop meets out there (testing the free
// camera.
// Round 4's mist (BuildMist) lay over the land's grid only: at the grid's outer edge, Reach metres beyond the rim, it stopped at its
// full strength (about 0.45), a straight hard line across the haze of a low, turned camera (the FREE CAMERA option shows the land up to
// 380 m beyond the rim on a 16:9 screen, 580 m on a 32:9 one; the fixed view about 30 m). Now the same mist runs on over the far plain at that strength
// to the plain's own end (FarMistOut from the map's middle), beyond every view: one mesh, the same material, tone and night. On the
// land's grid its points and strengths are round 4's (every other point of the grid, now with the grid's last point too); beyond the
// grid the plain is flat and the mist's lines there are at most FarMistCell apart. Every line runs through the whole mesh, so the grid's
// points and the plain's meet without a seam.
// The rain (OzJungleWeather.FarGround.cs) asks FarGround where a drop lands beyond the plateau, where the level has no collider: on this
// land, on its pools' water, or in a low shot on the flyover's far water. It used to land at height 0 there: under the land, or over a
// dip or a pool. On the plateau nothing changes.
// Play check: return OpenZ.OzL16Outskirts.FarMistStatus();
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzL16Outskirts
    {
        /// <summary>On: the mist runs on over the far plain (no end in any view). Off: round 4's mist, which ends at the land's grid. Read
        /// when the land is built.</summary>
        public static bool FarMistOn = true;
        /// <summary>The far plain's mist lines at most this far apart (m), and how far the mist runs from the map's middle (m: the plain's own end).</summary>
        public static float FarMistCell = 64f, FarMistOut = 1150f;
        /// <summary>The mist mesh made: its points and triangles (0 = round 4's mist was made).</summary>
        public static int MistPoints, MistTriangles;
        readonly List<Vector4[]> mistEnd = new List<Vector4[]>(), mistEndR4 = new List<Vector4[]>();   // the borders (x, y, z, strength): this mesh's, round 4's

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void FarMistResetStatics() { FarMistOn = true; MistPoints = MistTriangles = 0; }

        /// <summary>Round 4's strength of the mist over a point of the land (0 on the plateau, rising from MistFrom to MistFull beyond the
        /// rim's edge, a slow 15 % tone noise).</summary>
        float MistAlpha(Vector3 p)
        {
            EdgeS(p.x, p.z, out float d);
            float a = MistMaxNow * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(MistFrom, MistFull, d)) * (0.85f + 0.15f * N(p.x, p.z, 60f, 5.5f, 3.3f));   // MistMaxNow: OzL16Outskirts.Landscape.cs
            return d <= 0f ? 0f : a;
        }

        /// <summary>The mist's lines along one axis: the far plain's from a up to the land grid's first point (at most FarMistCell apart),
        /// every other point of the grid as round 4 and its last point, the plain's again out to b. grid = the line's point of the land's grid
        /// (-1: the plain).</summary>
        static void MistLines(float a, float g0, int gn, float b, List<float> at, List<int> grid)
        {
            float g1 = g0 + (gn - 1) * Cell, step = Mathf.Max(1f, MistCellNow);                                  // MistCellNow: OzL16Outskirts.Landscape.cs
            if (a < g0 - 1f) { int n = Mathf.CeilToInt((g0 - a) / step); for (int i = 0; i < n; i++) { at.Add(Mathf.Lerp(a, g0, i / (float)n)); grid.Add(-1); } }
            for (int i = 0; i < gn; i += 2) { at.Add(g0 + i * Cell); grid.Add(i); }
            if ((gn - 1) % 2 != 0) { at.Add(g1); grid.Add(gn - 1); }
            if (b > g1 + 1f) { int n = Mathf.CeilToInt((b - g1) / step); for (int i = 1; i <= n; i++) { at.Add(Mathf.Lerp(g1, b, i / (float)n)); grid.Add(-1); } }
        }

        /// <summary>From BuildMist: the mist over the land's grid and the far plain in one mesh. False = not made (round 4's mist is made).</summary>
        bool FarMist(Vector3[] land)
        {
            mistEnd.Clear(); mistEndR4.Clear(); MistPoints = MistTriangles = 0;
            if (land == null || gh == null || gnx < 2 || gnz < 2 || land.Length < gnx * gnz) return false;
            int sx = (gnx + 1) / 2, sz = (gnz + 1) / 2;                                                // round 4's border, for the check (its grid: every other point of the land's)
            Ring(mistEndR4, sx, sz, (i, j) =>
            {
                var q = land[Mathf.Min(j * 2, gnz - 1) * gnx + Mathf.Min(i * 2, gnx - 1)];
                return new Vector4(q.x, q.y + MistOver, q.z, MistAlpha(q));
            });
            if (!FarMistOn) return false;
            var sh = Shader.Find("OpenZ/Overlay"); if (!sh) return false;
            Vector2 mid = new Vector2(map.x * 0.5f, -map.y * 0.5f);
            var xs = new List<float>(); var xi = new List<int>(); var zs = new List<float>(); var zi = new List<int>();
            MistLines(mid.x - FarMistOut, gx0, gnx, mid.x + FarMistOut, xs, xi);
            MistLines(mid.y - FarMistOut, gz0, gnz, mid.y + FarMistOut, zs, zi);
            int nx = xs.Count, nz = zs.Count;
            var mv = new Vector3[nx * nz]; var mc = new Color[mv.Length]; var mn = new Vector3[mv.Length];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int k = j * nx + i;
                    var p = xi[i] >= 0 && zi[j] >= 0 ? land[zi[j] * gnx + xi[i]] : new Vector3(xs[i], FarMistY(xs[i], zs[j]), zs[j]);   // the land's own point, or the far land
                    mv[k] = p + Vector3.up * MistOver; mn[k] = Vector3.up;
                    mc[k] = new Color(1f, 1f, 1f, MistAlpha(p));
                }
            var t = new List<int>(nx * nz * 6);
            for (int j = 0; j + 1 < nz; j++)
                for (int i = 0; i + 1 < nx; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx + 1, dd = a + nx;
                    if (mc[a].a + mc[b].a + mc[c].a + mc[dd].a <= 0f) continue;                        // the plateau: no mist at all
                    t.Add(a); t.Add(dd); t.Add(c); t.Add(a); t.Add(c); t.Add(b);
                }
            Ring(mistEnd, nx, nz, (i, j) => new Vector4(mv[j * nx + i].x, mv[j * nx + i].y, mv[j * nx + i].z, mc[j * nx + i].a));   // this mesh's border, for the check
            mistMat = new Material(sh) { name = "L16Outskirts mist (runtime)" };
            if (mistMat.HasProperty("_Scan")) mistMat.SetFloat("_Scan", 0f);
            mistNight = -1f;
            mistGo = MakeMesh("L16_Outskirts_Mist", mv, mn, t, mistMat, false, 1f, mc);
            MistPoints = mv.Length; MistTriangles = t.Count / 3;
            return true;
        }

        /// <summary>The four sides of a grid's outer ring as point rows.</summary>
        static void Ring(List<Vector4[]> into, int nx, int nz, System.Func<int, int, Vector4> at)
        {
            var s = new Vector4[nx]; for (int i = 0; i < nx; i++) s[i] = at(i, 0); into.Add(s);
            s = new Vector4[nx]; for (int i = 0; i < nx; i++) s[i] = at(i, nz - 1); into.Add(s);
            s = new Vector4[nz]; for (int j = 0; j < nz; j++) s[j] = at(0, j); into.Add(s);
            s = new Vector4[nz]; for (int j = 0; j < nz; j++) s[j] = at(nx - 1, j); into.Add(s);
        }

        /// <summary>Where a drop lands beyond the plateau (the map and its rim), where the level has no collider. False on the plateau or
        /// while none of this is drawn (the caller keeps its own ground there); else the land's height (the plain's beyond its grid) or, under
        /// water, the water of its pools; in a low shot (the land stands back) the flyover's far water.</summary>
        public static bool FarGround(float x, float z, out float y, out bool water)
        {
            y = 0f; water = false;
            var o = I; if (!o || o.map.x <= 0f) return false;
            float m = OzL16FlyoverFlora.GroundMargin, outside = Mathf.Max(Mathf.Max(-x, x - o.map.x), Mathf.Max(z, -o.map.y - z));
            if (outside <= m) return false;
            if (OzL16FlyoverFlora.LowLookOn)
            {
                if (!OzL16FlyoverFlora.FarLand) return false;
                y = OzL16FlyoverFlora.FarWaterY; water = true; return true;
            }
            if (!o.built || o.standBack || o.gh == null) return false;
            float h = o.GridAt(x, z);
            if (o.waterGo && h < o.waterY && outside > m + 8f) { y = o.waterY; water = true; return true; }   // a pool (its water cells lie this far out)
            y = h; return true;
        }

        /// <summary>Play check: the mist's ends a camera frames: points of its border, every 4 m along it, with any strength (over 0.02), in
        /// the frame and nearer than the far plane; now = the drawn mist's (round 4's when FarMistOn was off), round4 = round 4's border (the
        /// land grid's edge), for before / after.</summary>
        public static void MistEndsInView(Camera cam, out int now, out int round4)
        {
            now = round4 = 0;
            var o = I; if (!o || !cam || !o.mistGo) return;
            round4 = EndsIn(cam, o.mistEndR4);
            now = o.mistEnd.Count > 0 ? EndsIn(cam, o.mistEnd) : round4;
        }

        static int EndsIn(Camera cam, List<Vector4[]> sides)
        {
            if (sides == null) return 0;
            int n = 0;
            foreach (var s in sides)
                for (int i = 0; i + 1 < s.Length; i++)
                {
                    Vector4 a = s[i], b = s[i + 1];
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 4f));
                    for (int k = 0; k < steps; k++)
                    {
                        var p = Vector4.Lerp(a, b, k / (float)steps);
                        if (p.w <= 0.02f) continue;
                        var v = cam.WorldToViewportPoint(new Vector3(p.x, p.y, p.z));
                        if (v.z > cam.nearClipPlane && v.z < cam.farClipPlane && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f) n++;
                    }
                }
            return n;
        }

        /// <summary>One line for the Play check.</summary>
        public static string FarMistStatus()
        {
            var o = I; if (!o) return "far mist: not on this level";
            string mist = !o.mistGo ? "not built yet" : MistPoints > 0
                ? $"runs on over the far plain to {FarMistOut:0} m from the map's middle ({MistPoints} points, {MistTriangles} triangles; lines at most {FarMistCell:0} m apart beyond the land's grid)"
                : "round 4's (it ends at the land's grid)";
            return $"far mist: {mist}, drawn {(o.mistGo && o.mistGo.activeSelf ? "yes" : "no (a low shot)")}";
        }
    }
}
