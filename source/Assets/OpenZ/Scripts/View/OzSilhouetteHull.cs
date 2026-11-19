// OpenZ (GPL-3.0) - 1.5 kit issue8_slow_chip_picture, part B (GitHub issue 8: "hover 5.4-8.4 ms a frame" in the Linux player's Perf
// lines, an i7-8565U). The hover pick (View/PlayerInput.cs HoverAt) asks every building for its pick box on every frame, and a
// the lowest point of every cell of a 24 x 24 grid over the model), each put through Camera.WorldToScreenPoint; the box = the smallest
// and largest screen x and y of them. The 1.3 pre-cull (View/PlayerInput.HoverCull.cs) never skips a building, because the brackets,
// the production window and the badges read the same per-frame box later in the frame (the 1.3 notes name the silhouettes as the
// largest cost left in the hover).
//
// THE SAVING (the same box, fewer projections). Once per building (one building a frame, so no long frame) its points' convex hull is
// worked out in its own model space. A point can never be the box's edge, and is left out of the per-frame projection, when
//  (a) it lies deeper than KeepM (5 cm in the world) inside the hull from every hull face, or
//  (b) it lies on a flat face of the hull (a floor, a flat roof, a straight wall: within a fraction of a millimetre of its plane) and
//      at least PlaneM (25 cm in the world) inside that face's outline.
// Why the box stays the same, to the last bit:
//  * a perspective projection of points that all lie in front of the camera has its smallest and largest screen x and y at corners of
//    their convex hull (a ratio of two linear functions has no inner extreme on a convex set); every hull corner is kept;
//  * for (a): a 5 cm ball round the point lies in the hull, so some kept point projects at least r f / Zmax further out in every screen
//    direction (r the ball in the world, f the focal length in pixels, Zmax the deepest kept point; the exact ratio, no linearising);
//  * for (b): a 25 cm disc round the point lies in that face, so some kept corner of the face projects at least
//    r f h / (sqrt2 Zhi rho) further out (h the camera's distance from the face's plane, Zhi / rho the face corners' deepest
//    depth and farthest distance); that bound goes to 0 when the camera comes near the face's plane, so it is worked
//    out every frame for every face, and a face whose margin is too small simply has its points projected on that frame too;
//  * every margin must be over MarginPx (0.1 px, hundreds of times the float rounding of WorldToScreenPoint) plus the face points'
//    own distance from their plane, else the points it would leave out are projected on that frame too; the nearest kept point must
//    be NearM in front of the camera (then every point is), else the whole building takes today's full pass on that frame;
//  * the kept points go through the same call on the same numbers as before (l2w.MultiplyPoint3x4, then Camera.WorldToScreenPoint),
//    so the smallest and largest values are the very same floats.
// A hull that cannot be proven (fewer than 24 points, flat, not closed round its inner point) keeps every point: that building takes
// today's full pass. Off switch for the checks: On = false (the full pass for every building). View only: nothing is drawn, no rule,
// picture, cursor or click changes.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public static class OzSilhouetteHull
    {
        /// <summary>On (default): the per-frame silhouette projects only its outline candidates. Off: every point (the 1.4 pass).</summary>
        public static bool On = true;
        /// <summary>The hull's tolerance and a face's flatness (shares of the model's size), the depth in the world a point must lie
        /// inside the hull (KeepM) or inside a flat face's outline (PlaneM), the margin in pixels every left-out point must give each
        /// frame, the nearest distance in front of the camera, the fewest points worth a hull, the fewest points worth a face.</summary>
        public const double BuildEps = 1e-4, FlatEps = 2e-5;
        public const float KeepM = 0.05f, PlaneM = 0.25f, MarginPx = 0.1f, NearM = 2f;
        public const int MinPoints = 24, MinFacePoints = 8;
        /// <summary>Checks: boxes by the short pass / by the full pass, hulls built, hulls refused, points projected by each.</summary>
        public static int ShortPasses, FullPasses, Built, Refused; public static long Projected, ProjectedFull;

        /// <summary>One building's points as the short pass uses them.</summary>
        public sealed class Pack
        {
            public Vector3[] src, keep;                                                                      // all points; the ones always projected
            public float keepLocal;                                                                          // (a): the depth (model units) every deep point lies inside
            public int deep;                                                                                 // how many points (a) left out
            public Vector3[] deepPts;                                                                        // those points (projected too on a frame whose margin is too small)
            public Face2[] faces;                                                                            // (b): the flat faces with points left out
            public string why;
            public int Kept => keep != null ? keep.Length : src != null ? src.Length : 0;
            public int OnFaces { get { int n = 0; if (faces != null) foreach (var f in faces) n += f.pts.Length; return n; } }
        }
        /// <summary>A flat face of the hull: its plane (a point, a unit normal: model space), its corners (indices into keep), the points
        /// left out on it, the disc (model units) each lies inside its outline, their largest distance from the plane.</summary>
        public sealed class Face2 { public Vector3 p0, n; public int[] corners; public Vector3[] pts; public float discLocal, offLocal; }

        static readonly Dictionary<int, Pack> cache = new Dictionary<int, Pack>();
        static int builtFrame = -1;
        static Vector3[] wbuf = new Vector3[256]; static float[] zbuf = new float[256];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); builtFrame = -1; On = true; ShortPasses = FullPasses = Built = Refused = 0; Projected = ProjectedFull = 0; }

        /// <summary>A level's views are gone (PlayerInput.OnDestroy): drop the hulls with them.</summary>
        public static void Clear() { cache.Clear(); }

        /// <summary>Checks: the pack of a building (null: none yet).</summary>
        public static Pack Of(int id) => cache.TryGetValue(id, out var p) ? p : null;

        /// <summary>PlayerInput.MeshRect, a building's (or the shuttle's) silhouette this frame: true = smn / smx / ok set from the kept points
        /// (the very values the full pass gives); false = nothing set, the caller runs its full pass as before.</summary>
        public static bool Rect(int id, Vector3[] sil, Camera cm, Matrix4x4 l2w, ref Vector2 smn, ref Vector2 smx, ref bool ok)
        {
            if (!On || sil == null || !cm || cm.orthographic) { FullPasses++; ProjectedFull += sil != null ? sil.Length : 0; return false; }
            if (!cache.TryGetValue(id, out var e) || !ReferenceEquals(e.src, sil))
            {
                if (builtFrame == Time.frameCount) { FullPasses++; ProjectedFull += sil.Length; return false; }   // one hull a frame
                builtFrame = Time.frameCount;
                e = Reduce(sil, WorldPerLocal(l2w));
                if (e.keep != null) Built++; else Refused++;
                cache[id] = e;
            }
            if (e.keep == null) { FullPasses++; ProjectedFull += sil.Length; return false; }
            if (!Short(e, cm, l2w, out var mn, out var mx, out int projected)) { Projected += projected; FullPasses++; ProjectedFull += sil.Length; return false; }   // not proven this frame: today's pass
            Projected += projected;
            smn = mn; smx = mx; ok = true; ShortPasses++;
            return true;
        }

        /// <summary>A lower bound of the smallest stretch of the matrix's 3 x 3 part (|det| / |M|^2) and an upper bound of its largest (|M|).</summary>
        static void Stretch(Matrix4x4 m, out float lo, out float hi, out float det)
        {
            float a = m.m00, b = m.m01, c = m.m02, d = m.m10, f = m.m11, g = m.m12, h = m.m20, i = m.m21, j = m.m22;
            det = a * (f * j - g * i) - b * (d * j - g * h) + c * (d * i - f * h);
            float fro = a * a + b * b + c * c + d * d + f * f + g * g + h * h + i * i + j * j;
            lo = fro > 0f ? Mathf.Abs(det) / fro : 0f; hi = Mathf.Sqrt(fro);
        }

        public static float WorldPerLocal(Matrix4x4 m) { Stretch(m, out float lo, out _, out _); return lo; }

        /// <summary>The kept points projected as the full pass projects them; true = their smallest / largest screen x and y are the whole
        /// point set's (see the head of this file); false = not proven this frame. projected: the points it projected.</summary>
        public static bool Short(Pack pk, Camera cm, Matrix4x4 l2w, out Vector2 mn, out Vector2 mx, out int projected)
        {
            mn = new Vector2(float.MaxValue, float.MaxValue); mx = new Vector2(float.MinValue, float.MinValue); projected = 0;
            var k = pk != null ? pk.keep : null;
            if (k == null || k.Length == 0 || !cm || cm.orthographic) return false;
            Stretch(l2w, out float lo, out float hi, out float det);
            var pm = cm.projectionMatrix; var pr = cm.pixelRect;
            float focal = Mathf.Min(Mathf.Abs(pm.m00) * pr.width, Mathf.Abs(pm.m11) * pr.height) * 0.5f;   // pixels per unit of x / z at depth 1
            Vector3 eye = cm.cameraToWorldMatrix.GetColumn(3);
            if (wbuf.Length < k.Length) { wbuf = new Vector3[k.Length * 2]; zbuf = new float[k.Length * 2]; }
            float zLo = float.MaxValue, zHi = 0f;
            for (int n = 0; n < k.Length; n++)
            {
                var w = l2w.MultiplyPoint3x4(k[n]); var sp = cm.WorldToScreenPoint(w);
                wbuf[n] = w; zbuf[n] = sp.z;
                if (sp.z < zLo) zLo = sp.z; if (sp.z > zHi) zHi = sp.z;
                mn = Vector2.Min(mn, sp); mx = Vector2.Max(mx, sp);
            }
            projected = k.Length;
            if (!(zLo >= NearM) || !(focal > 0f)) return false;
            if (pk.deep > 0 && !(pk.keepLocal * lo * focal / zHi >= MarginPx))                             // (a): the 5 cm ball's margin at the deepest point is too small (far away, a small window):
            {                                                                                              // the deep points are projected too on this frame
                var dp = pk.deepPts;
                for (int n = 0; n < dp.Length; n++) { var sp = cm.WorldToScreenPoint(l2w.MultiplyPoint3x4(dp[n])); mn = Vector2.Min(mn, sp); mx = Vector2.Max(mx, sp); if (!(sp.z >= 0f)) return false; }
                projected += dp.Length;
            }
            if (pk.faces == null) return true;
            // the inverse transpose (up to its sign and size) for the faces' normals in the world
            float a = l2w.m00, b = l2w.m01, c = l2w.m02, d = l2w.m10, f = l2w.m11, g = l2w.m12, h = l2w.m20, i = l2w.m21, j = l2w.m22;
            foreach (var fc in pk.faces)
            {
                float fzLo = float.MaxValue, fzHi = 0f, rho = 0f;
                foreach (int ci in fc.corners) { float z = zbuf[ci]; if (z < fzLo) fzLo = z; if (z > fzHi) fzHi = z; float r = (wbuf[ci] - eye).magnitude; if (r > rho) rho = r; }
                var nl = fc.n;
                var nw = new Vector3((f * j - g * i) * nl.x + (g * h - d * j) * nl.y + (d * i - f * h) * nl.z,
                                     (c * i - b * j) * nl.x + (a * j - c * h) * nl.y + (b * h - a * i) * nl.z,
                                     (b * g - c * f) * nl.x + (c * d - a * g) * nl.y + (a * f - b * d) * nl.z);   // cofactors: det x the inverse transpose
                float nm = nw.magnitude;
                float hEye = nm > 0f ? Mathf.Abs(Vector3.Dot(nw, eye - l2w.MultiplyPoint3x4(fc.p0))) / nm : 0f;
                float gain = fc.discLocal * lo * focal * hEye / (1.41421357f * fzHi * Mathf.Max(rho, 1e-3f));   // r f h / (sqrt2 Zhi rho): the exact ratio, no linearising (head of this file)
                float off = 3f * fc.offLocal * hi * 1.41421357f * focal * rho / (fzLo * fzLo);             // the face points' (and the disc's) distance from the plane, in pixels at most
                if (fzLo >= NearM && gain >= MarginPx + off) continue;                                      // proven: none of this face's points can be an edge
                var fp = fc.pts;
                for (int n = 0; n < fp.Length; n++) { var sp = cm.WorldToScreenPoint(l2w.MultiplyPoint3x4(fp[n])); mn = Vector2.Min(mn, sp); mx = Vector2.Max(mx, sp); if (!(sp.z >= 0f)) return false; }
                projected += fp.Length;
            }
            return true;
        }

        // ------------------------------------------------------------------ the hull (double precision, once per building)
        struct Face { public int a, b, c; public double nx, ny, nz, d, area; public bool alive; }

        /// <summary>The short pass's pack of a point set. worldPerLocal: the world size of one model unit (at least) when it is built; a
        /// pack with keep == null keeps every point (why says what failed).</summary>
        public static Pack Reduce(Vector3[] p, float worldPerLocal)
        {
            var pk = new Pack { src = p };
            int n = p != null ? p.Length : 0;
            if (n < MinPoints) { pk.why = "fewer than " + MinPoints + " points"; return pk; }
            if (!(worldPerLocal > 1e-6f)) { pk.why = "no size in the world"; return pk; }
            var x = new double[n]; var y = new double[n]; var z = new double[n];
            double lox = double.MaxValue, hix = double.MinValue, loy = double.MaxValue, hiy = double.MinValue, loz = double.MaxValue, hiz = double.MinValue;
            for (int q = 0; q < n; q++)
            {
                x[q] = p[q].x; y[q] = p[q].y; z[q] = p[q].z;
                if (double.IsNaN(x[q] + y[q] + z[q]) || double.IsInfinity(x[q] + y[q] + z[q])) { pk.why = "a point is not a number"; return pk; }
                lox = Math.Min(lox, x[q]); hix = Math.Max(hix, x[q]); loy = Math.Min(loy, y[q]); hiy = Math.Max(hiy, y[q]); loz = Math.Min(loz, z[q]); hiz = Math.Max(hiz, z[q]);
            }
            double ext = Math.Max(hix - lox, Math.Max(hiy - loy, hiz - loz));
            if (!(ext > 0)) { pk.why = "all points in one place"; return pk; }
            double eps = BuildEps * ext, flat = FlatEps * ext;
            // the first tetrahedron: the far ends
            int i0 = 0; for (int q = 1; q < n; q++) if (x[q] < x[i0]) i0 = q;
            int i1 = -1; double best = 0; for (int q = 0; q < n; q++) { double dd = D2(x, y, z, q, i0); if (dd > best) { best = dd; i1 = q; } }
            if (i1 < 0 || Math.Sqrt(best) < ext * 0.01) { pk.why = "flat (a point)"; return pk; }
            int i2 = -1; best = 0;
            for (int q = 0; q < n; q++)
            {
                double ux = x[i1] - x[i0], uy = y[i1] - y[i0], uz = z[i1] - z[i0], vx = x[q] - x[i0], vy = y[q] - y[i0], vz = z[q] - z[i0];
                double cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx, dd = cx * cx + cy * cy + cz * cz;
                if (dd > best) { best = dd; i2 = q; }
            }
            if (i2 < 0 || Math.Sqrt(best) / Math.Sqrt(D2(x, y, z, i1, i0)) < ext * 0.01) { pk.why = "flat (a line)"; return pk; }
            var faces = new List<Face>(256);
            var f0 = Make(x, y, z, i0, i1, i2);
            int i3 = -1; best = 0;
            for (int q = 0; q < n; q++) { double dd = Math.Abs(Dist(f0, x[q], y[q], z[q])); if (dd > best) { best = dd; i3 = q; } }
            if (i3 < 0 || best < ext * 0.01) { pk.why = "flat (a plane)"; return pk; }
            double ccx = (x[i0] + x[i1] + x[i2] + x[i3]) * 0.25, ccy = (y[i0] + y[i1] + y[i2] + y[i3]) * 0.25, ccz = (z[i0] + z[i1] + z[i2] + z[i3]) * 0.25;
            foreach (var t in new[] { (i0, i1, i2), (i0, i1, i3), (i0, i2, i3), (i1, i2, i3) })
            {
                var fc = Make(x, y, z, t.Item1, t.Item2, t.Item3);
                if (Dist(fc, ccx, ccy, ccz) > 0) fc = Make(x, y, z, t.Item1, t.Item3, t.Item2);
                faces.Add(fc);
            }
            var edges = new HashSet<long>(); var horizon = new List<(int a, int b)>(); var vis = new List<int>(); var newFaces = new List<Face>();
            int alive = 4, folds = 0;
            for (int q = 0; q < n; q++)
            {
                if (q == i0 || q == i1 || q == i2 || q == i3) continue;
                vis.Clear();
                double far0 = double.MinValue; for (int fi = 0; fi < faces.Count; fi++) if (faces[fi].alive) far0 = Math.Max(far0, Dist(faces[fi], x[q], y[q], z[q]));
                if (far0 <= eps) continue;                                                                 // inside (or within the tolerance of) the hull so far
                for (int fi = 0; fi < faces.Count; fi++) if (faces[fi].alive && Dist(faces[fi], x[q], y[q], z[q]) > eps * 1e-3) vis.Add(fi);   // every face it is in front of (a thousandth of the tolerance: fewer folds)
                edges.Clear(); horizon.Clear();
                foreach (int fi in vis) { var fc = faces[fi]; edges.Add(Key(fc.a, fc.b, n)); edges.Add(Key(fc.b, fc.c, n)); edges.Add(Key(fc.c, fc.a, n)); }
                foreach (int fi in vis)
                {
                    var fc = faces[fi];
                    if (!edges.Contains(Key(fc.b, fc.a, n))) horizon.Add((fc.a, fc.b));
                    if (!edges.Contains(Key(fc.c, fc.b, n))) horizon.Add((fc.b, fc.c));
                    if (!edges.Contains(Key(fc.a, fc.c, n))) horizon.Add((fc.c, fc.a));
                }
                // the new faces are made first; a point whose new faces would not all face out from the inner point (a fold of nearly flat
                // faces) is not taken into the hull: it stays outside it and is simply kept (every point outside a face is kept below)
                newFaces.Clear(); bool fold = false;
                foreach (var hz in horizon)
                {
                    var nf = Make(x, y, z, hz.a, hz.b, q);
                    if (!(Dist(nf, ccx, ccy, ccz) < 0) || !(nf.area > 0)) { fold = true; break; }
                    newFaces.Add(nf);
                }
                if (fold) { folds++; continue; }
                foreach (int fi in vis) { var fc = faces[fi]; fc.alive = false; faces[fi] = fc; alive--; }
                foreach (var nf in newFaces) { faces.Add(nf); alive++; }
                if (faces.Count > 4 * alive + 64) faces.RemoveAll(fc => !fc.alive);
            }
            faces.RemoveAll(fc => !fc.alive);
            if (faces.Count < 4) { pk.why = "too few faces"; return pk; }
            // the proof the hull is a closed surface round the inner point: every edge once each way, every face facing out from it
            var count = new Dictionary<long, int>();
            foreach (var fc in faces)
            {
                if (!(Dist(fc, ccx, ccy, ccz) < -eps) || !(fc.area > 0)) { pk.why = "the inner point is not inside every face"; return pk; }
                foreach (var k in new[] { Key(fc.a, fc.b, n), Key(fc.b, fc.c, n), Key(fc.c, fc.a, n) }) { count.TryGetValue(k, out int c0); count[k] = c0 + 1; }
            }
            foreach (var kv in count)
            {
                long k = kv.Key; int a = (int)(k / n), b = (int)(k % n);
                if (kv.Value != 1 || !count.TryGetValue(Key(b, a, n), out int back) || back != 1) { pk.why = "the hull is not closed"; return pk; }
            }
            var corner = new bool[n]; foreach (var fc in faces) { corner[fc.a] = corner[fc.b] = corner[fc.c] = true; }
            double keep = KeepM / worldPerLocal, disc = PlaneM / worldPerLocal;
            // the flat faces: the hull's triangles grouped by plane (every corner within `flat` of the largest triangle's plane)
            var order = new List<int>(); for (int fi = 0; fi < faces.Count; fi++) order.Add(fi);
            order.Sort((u, v) => faces[v].area.CompareTo(faces[u].area));
            var group = new int[faces.Count]; for (int fi = 0; fi < group.Length; fi++) group[fi] = -1;
            var planes = new List<Face>();
            foreach (int fi in order)
            {
                if (group[fi] >= 0) continue;
                var pl = faces[fi]; int gi = planes.Count; planes.Add(pl); group[fi] = gi;
                for (int fj = 0; fj < faces.Count; fj++)
                {
                    if (group[fj] >= 0) continue; var o = faces[fj];
                    if (o.nx * pl.nx + o.ny * pl.ny + o.nz * pl.nz < 0.9) continue;
                    if (Math.Abs(Dist(pl, x[o.a], y[o.a], z[o.a])) <= flat && Math.Abs(Dist(pl, x[o.b], y[o.b], z[o.b])) <= flat && Math.Abs(Dist(pl, x[o.c], y[o.c], z[o.c])) <= flat) group[fj] = gi;
                }
            }
            // each flat face's outline: its triangles' edges whose other side belongs to another face (inward = n x (b - a))
            var owner = new Dictionary<long, int>(); for (int fi = 0; fi < faces.Count; fi++) { var fc = faces[fi]; owner[Key(fc.a, fc.b, n)] = fi; owner[Key(fc.b, fc.c, n)] = fi; owner[Key(fc.c, fc.a, n)] = fi; }
            var outline = new List<(double px, double py, double pz, double ix, double iy, double iz)>[planes.Count];
            var fcorners = new HashSet<int>[planes.Count];
            for (int gi = 0; gi < planes.Count; gi++) { outline[gi] = new List<(double, double, double, double, double, double)>(); fcorners[gi] = new HashSet<int>(); }
            for (int fi = 0; fi < faces.Count; fi++)
            {
                var fc = faces[fi]; int gi = group[fi]; var pl = planes[gi];
                fcorners[gi].Add(fc.a); fcorners[gi].Add(fc.b); fcorners[gi].Add(fc.c);
                foreach (var (ea, eb) in new[] { (fc.a, fc.b), (fc.b, fc.c), (fc.c, fc.a) })
                {
                    if (group[owner[Key(eb, ea, n)]] == gi) continue;                                          // an inner edge of the flat face
                    double ex = x[eb] - x[ea], ey = y[eb] - y[ea], ez = z[eb] - z[ea];
                    double ix = pl.ny * ez - pl.nz * ey, iy = pl.nz * ex - pl.nx * ez, iz = pl.nx * ey - pl.ny * ex, il = Math.Sqrt(ix * ix + iy * iy + iz * iz);
                    if (il > 0) outline[gi].Add((x[ea], y[ea], z[ea], ix / il, iy / il, iz / il));
                }
            }
            // sort every point: a corner (kept), deep inside (a), on a flat face well inside its outline (b), else kept
            var onFace = new List<int>[planes.Count]; for (int gi = 0; gi < planes.Count; gi++) onFace[gi] = new List<int>();
            var kept = new List<int>(n / 4); var deepL = new List<int>();
            var offMax = new double[planes.Count];
            for (int q = 0; q < n; q++)
            {
                if (corner[q]) { kept.Add(q); continue; }
                double far = double.MinValue;
                foreach (var fc in faces) { double dd = Dist(fc, x[q], y[q], z[q]); if (dd > far) { far = dd; if (far >= -keep) break; } }
                if (far < -keep) { deepL.Add(q); continue; }                                               // (a)
                int put = -1;
                for (int gi = 0; gi < planes.Count && put < 0; gi++)
                {
                    double dp = Dist(planes[gi], x[q], y[q], z[q]);
                    if (Math.Abs(dp) > flat || outline[gi].Count < 3) continue;
                    double qx = x[q] - dp * planes[gi].nx, qy = y[q] - dp * planes[gi].ny, qz = z[q] - dp * planes[gi].nz;   // the point on the plane
                    bool inside = true;
                    foreach (var e in outline[gi]) if ((qx - e.px) * e.ix + (qy - e.py) * e.iy + (qz - e.pz) * e.iz < disc) { inside = false; break; }
                    if (inside) { put = gi; offMax[gi] = Math.Max(offMax[gi], Math.Abs(dp)); }
                }
                if (put >= 0) onFace[put].Add(q); else kept.Add(q);
            }
            var flats = new List<(int gi, List<int> pts)>();
            for (int gi = 0; gi < planes.Count; gi++)
            {
                if (onFace[gi].Count >= MinFacePoints) flats.Add((gi, onFace[gi]));
                else kept.AddRange(onFace[gi]);                                                            // too few to be worth a face test every frame
            }
            kept.Sort();
            var at = new Dictionary<int, int>(); for (int s = 0; s < kept.Count; s++) at[kept[s]] = s;
            var keepPts = new Vector3[kept.Count]; for (int s = 0; s < kept.Count; s++) keepPts[s] = p[kept[s]];
            var f2 = new List<Face2>();
            foreach (var (gi, list) in flats)
            {
                var pl = planes[gi]; var cs = new List<int>();
                foreach (int ci in fcorners[gi]) cs.Add(at[ci]);                                           // every corner of a face is a hull corner, so kept
                var pts = new Vector3[list.Count]; for (int s = 0; s < list.Count; s++) pts[s] = p[list[s]];
                f2.Add(new Face2 { p0 = p[pl.a], n = new Vector3((float)pl.nx, (float)pl.ny, (float)pl.nz), corners = cs.ToArray(), pts = pts, discLocal = (float)disc * 0.999f, offLocal = (float)Math.Max(offMax[gi], flat) * 1.001f + 1e-6f });
            }
            var deepPts = new Vector3[deepL.Count]; for (int s = 0; s < deepL.Count; s++) deepPts[s] = p[deepL[s]];
            pk.keep = keepPts; pk.keepLocal = (float)keep * 0.999f; pk.deep = deepL.Count; pk.deepPts = deepPts; pk.faces = f2.Count > 0 ? f2.ToArray() : null;
            pk.why = $"{faces.Count} hull triangles, {planes.Count} faces ({f2.Count} flat ones used){(folds > 0 ? ", " + folds + " points left out of the hull (kept)" : "")}; of {n} points {keepPts.Length} kept, {deepL.Count} deep inside, {pk.OnFaces} on flat faces";
            return pk;
        }

        static long Key(int a, int b, int n) => (long)a * n + b;
        static double D2(double[] x, double[] y, double[] z, int a, int b) { double dx = x[a] - x[b], dy = y[a] - y[b], dz = z[a] - z[b]; return dx * dx + dy * dy + dz * dz; }
        static double Dist(Face f, double px, double py, double pz) => f.nx * px + f.ny * py + f.nz * pz - f.d;

        static Face Make(double[] x, double[] y, double[] z, int a, int b, int c)
        {
            double ux = x[b] - x[a], uy = y[b] - y[a], uz = z[b] - z[a], vx = x[c] - x[a], vy = y[c] - y[a], vz = z[c] - z[a];
            double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx, l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (l > 0) { nx /= l; ny /= l; nz /= l; }
            return new Face { a = a, b = b, c = c, nx = nx, ny = ny, nz = nz, d = nx * x[a] + ny * y[a] + nz * z[a], area = l * 0.5, alive = true };
        }
    }
}
