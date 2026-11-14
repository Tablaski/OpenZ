using System;

namespace OpenZ
{
    /// <summary>
    /// 1.4 kit perf_ground_grid: the pure maths of one tile bake of the ground grid (View/ZGame.GroundGrid.cs). No engine type in this file, so the kit's
    /// tools/core_harness compiles this very file on plain .NET and runs it on each level's real terrain collider against the numpy mirror
    /// (tools/grid_exact_offline.py): the same cells must pass, with the same corner heights.
    ///
    /// Input: the walk surface's FRONT-FACING triangles over the tile (what a ray straight down can hit), in world metres, each with its group (which colliders
    /// GroundY treats alike), and the cells the bake must leave to the ray. Output: the 17 x 17 corner heights (the highest triangle at each corner, as the ray
    /// gives it; NaN = no walk surface there) and per cell its group (0 = the ray answers in this cell).
    /// A cell passes only when, from the triangles themselves (point samples miss posts and walls between them):
    ///  1. its TOP layer covers it exactly once: the clipped pieces, highest first by their lowest point, add up to the cell's area and no more
    ///     (a hole fails, and so does anything standing on the ground: a post, a wall top, a deck edge);
    ///  2. every other piece lies wholly under the top layer's lowest point (a floor or a skirt under the ground, the river bed under a deck): hidden from the ray;
    ///  3. on every top piece the plane stays within tol of the bilinear of the 4 corner heights, taken exactly: on a convex piece their difference is a plane
    ///     or a saddle, so its extreme lies at a vertex or at the one stationary point of an edge;
    ///  4. the top pieces all belong to one group.
    /// </summary>
    public sealed class GroundGridCore
    {
        public const int Shift = 4, N = 1 << Shift, Mask = N - 1, NC = N + 1;
        public const double AreaTol = 1e-5, EpsX = 1e-6, BaryEps = 1e-9, DetEps = 1e-14, StatEps = 1e-15;

        /// <summary>The tile's triangles: a, b, c as x, y, z (9 numbers each, world metres).</summary>
        public double[] T = new double[9 * 512];
        /// <summary>Each triangle's group (1..255).</summary>
        public byte[] G = new byte[512];
        public int Count;
        /// <summary>Cells the bake must leave to the ray (index j * N + i).</summary>
        public readonly bool[] Blocked = new bool[N * N];
        /// <summary>Last bake: triangle-cell pieces looked at, cells that passed.</summary>
        public int Pieces, Passed;

        readonly int[] binN = new int[NC * NC], binAt = new int[NC * NC + 1], binFill = new int[NC * NC];
        int[] binItem = new int[2048];
        int[] spanT = new int[1024], spanJ = new int[1024], spanI0 = new int[1024], spanI1 = new int[1024]; int spans;
        readonly double[] au = new double[16], av = new double[16], bu = new double[16], bv = new double[16];
        double[] pA = new double[64], pMin = new double[64], pMax = new double[64], pF = new double[64]; byte[] pG = new byte[64];

        public void Clear() { Count = 0; Array.Clear(Blocked, 0, Blocked.Length); }

        public void Add(double ax, double ay, double az, double bx, double by, double bz, double cx, double cy, double cz, byte g)
        {
            if (Count == G.Length) { Array.Resize(ref G, Count * 2); Array.Resize(ref T, Count * 18); }
            int o = Count * 9;
            T[o] = ax; T[o + 1] = ay; T[o + 2] = az; T[o + 3] = bx; T[o + 4] = by; T[o + 5] = bz; T[o + 6] = cx; T[o + 7] = cy; T[o + 8] = cz;
            G[Count++] = g;
        }

        /// <summary>The bake of a tile whose first cell starts at (x0, z0), cells of c metres: corner heights into y (NC x NC), groups into g (N x N).</summary>
        public void Bake(double x0, double z0, double c, double tol, float[] y, byte[] g)
        {
            Pieces = 0; Passed = 0;
            Bin(x0, z0, c);
            for (int j = 0; j <= N; j++)
                for (int i = 0; i <= N; i++)
                    y[j * NC + i] = (float)Top(x0 + i * c, z0 + j * c, j * NC + i);
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    int k = (j << Shift) | i;
                    byte r = Blocked[k] ? (byte)0 : Judge(j * NC + i, x0 + i * c, z0 + j * c, c, tol,
                        y[j * NC + i], y[j * NC + i + 1], y[(j + 1) * NC + i], y[(j + 1) * NC + i + 1]);
                    g[k] = r;
                    if (r != 0) Passed++;
                }
        }

        // ------------------------------------------------------------------------------------------------ bins: per row of cells, the triangle's own x-span in that row
        void Bin(double x0, double z0, double c)
        {
            spans = 0;
            for (int t = 0; t < Count; t++)
            {
                int o = t * 9;
                double ax = T[o], az = T[o + 2], bx = T[o + 3], bz = T[o + 5], cx = T[o + 6], cz = T[o + 8];
                double zmin = Math.Min(az, Math.Min(bz, cz)), zmax = Math.Max(az, Math.Max(bz, cz));
                int j0 = (int)Math.Floor((zmin - EpsX - z0) / c), j1 = (int)Math.Floor((zmax + EpsX - z0) / c);
                if (j1 < 0 || j0 > N) continue;
                if (j0 < 0) j0 = 0; if (j1 > N) j1 = N;
                for (int j = j0; j <= j1; j++)
                {
                    double za = z0 + j * c - EpsX, zb = z0 + (j + 1) * c + EpsX, lo = double.PositiveInfinity, hi = double.NegativeInfinity;
                    Span(ax, az, bx, bz, za, zb, ref lo, ref hi); Span(bx, bz, cx, cz, za, zb, ref lo, ref hi); Span(cx, cz, ax, az, za, zb, ref lo, ref hi);
                    if (double.IsInfinity(lo)) continue;
                    int i0 = (int)Math.Floor((lo - EpsX - x0) / c), i1 = (int)Math.Floor((hi + EpsX - x0) / c);
                    if (i1 < 0 || i0 > N) continue;
                    if (i0 < 0) i0 = 0; if (i1 > N) i1 = N;
                    if (spans == spanT.Length) { int n2 = spans * 2; Array.Resize(ref spanT, n2); Array.Resize(ref spanJ, n2); Array.Resize(ref spanI0, n2); Array.Resize(ref spanI1, n2); }
                    spanT[spans] = t; spanJ[spans] = j; spanI0[spans] = i0; spanI1[spans] = i1; spans++;
                }
            }
            Array.Clear(binN, 0, binN.Length);
            for (int s = 0; s < spans; s++) for (int i = spanI0[s]; i <= spanI1[s]; i++) binN[spanJ[s] * NC + i]++;
            binAt[0] = 0;
            for (int b = 0; b < NC * NC; b++) binAt[b + 1] = binAt[b] + binN[b];
            if (binItem.Length < binAt[NC * NC]) binItem = new int[Math.Max(binAt[NC * NC], binItem.Length * 2)];
            Array.Copy(binAt, binFill, NC * NC);
            for (int s = 0; s < spans; s++) for (int i = spanI0[s]; i <= spanI1[s]; i++) binItem[binFill[spanJ[s] * NC + i]++] = spanT[s];
        }

        /// <summary>One edge p-q: its points inside the strip za..zb widen the x-span (the vertex p, and where it crosses za and zb).</summary>
        static void Span(double px, double pz, double qx, double qz, double za, double zb, ref double lo, ref double hi)
        {
            if (pz >= za && pz <= zb) { if (px < lo) lo = px; if (px > hi) hi = px; }
            double dz = qz - pz;
            if (dz == 0) return;
            double t = (za - pz) / dz;
            if (t >= 0 && t <= 1) { double x = px + t * (qx - px); if (x < lo) lo = x; if (x > hi) hi = x; }
            t = (zb - pz) / dz;
            if (t >= 0 && t <= 1) { double x = px + t * (qx - px); if (x < lo) lo = x; if (x > hi) hi = x; }
        }

        // ------------------------------------------------------------------------------------------------ the corner: the highest front-facing triangle there (the ray's hit)
        double Top(double px, double pz, int bin)
        {
            double best = double.NegativeInfinity;
            for (int q = binAt[bin], e = binAt[bin + 1]; q < e; q++)
            {
                int o = binItem[q] * 9;
                double ax = T[o], ay = T[o + 1], az = T[o + 2], bx = T[o + 3], by = T[o + 4], bz = T[o + 5], cx = T[o + 6], cy = T[o + 7], cz = T[o + 8];
                double d = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
                if (Math.Abs(d) < DetEps) continue;
                double u = ((bz - cz) * (px - cx) + (cx - bx) * (pz - cz)) / d, v = ((cz - az) * (px - cx) + (ax - cx) * (pz - cz)) / d, w = 1 - u - v;
                if (u < -BaryEps || v < -BaryEps || w < -BaryEps) continue;
                double h = u * ay + v * by + w * cy;
                if (h > best) best = h;
            }
            return double.IsNegativeInfinity(best) ? double.NaN : best;
        }

        // ------------------------------------------------------------------------------------------------ the cell test
        byte Judge(int bin, double ox, double oz, double c, double tol, float f00, float f10, float f01, float f11)
        {
            if (float.IsNaN(f00) || float.IsNaN(f10) || float.IsNaN(f01) || float.IsNaN(f11)) return 0;
            double y00 = f00, y10 = f10, y01 = f01, y11 = f11, dl = y00 - y10 - y01 + y11;
            int np = 0;
            for (int q = binAt[bin], e = binAt[bin + 1]; q < e; q++)
            {
                int t = binItem[q], o = t * 9;
                double ua = (T[o] - ox) / c, va = (T[o + 2] - oz) / c, ya = T[o + 1];
                double ub = (T[o + 3] - ox) / c, vb = (T[o + 5] - oz) / c, yb = T[o + 4];
                double uc = (T[o + 6] - ox) / c, vc = (T[o + 8] - oz) / c, yc = T[o + 7];
                au[0] = ua; av[0] = va; au[1] = ub; av[1] = vb; au[2] = uc; av[2] = vc;
                int k = 3;
                k = Clip(au, av, k, bu, bv, true, 0.0, true); k = Clip(bu, bv, k, au, av, true, 1.0, false);
                k = Clip(au, av, k, bu, bv, false, 0.0, true); k = Clip(bu, bv, k, au, av, false, 1.0, false);
                if (k < 3) continue;
                double cr = 0;
                for (int i = 0; i < k; i++) { int n = i + 1 == k ? 0 : i + 1; cr += au[i] * av[n] - au[n] * av[i]; }
                double area = 0.5 * Math.Abs(cr);
                if (!(area > 0)) continue;
                Pieces++;
                double du1 = ub - ua, dv1 = vb - va, dy1 = yb - ya, du2 = uc - ua, dv2 = vc - va, dy2 = yc - ya, det = du1 * dv2 - du2 * dv1;
                if (Math.Abs(det) < DetEps) return 0;
                double gu = (dy1 * dv2 - dy2 * dv1) / det, gv = (du1 * dy2 - du2 * dy1) / det;
                if (double.IsNaN(gu) || double.IsInfinity(gu) || double.IsNaN(gv) || double.IsInfinity(gv)) return 0;
                double p0 = ya - gu * ua - gv * va, f0 = p0 - y00, fu = gu - (y10 - y00), fv = gv - (y01 - y00);
                double hmin = double.PositiveInfinity, hmax = double.NegativeInfinity, mf = 0;
                for (int i = 0; i < k; i++)
                {
                    double u = au[i], v = av[i], h = p0 + gu * u + gv * v;
                    if (h < hmin) hmin = h; if (h > hmax) hmax = h;
                    double fp = f0 + fu * u + fv * v - dl * u * v, a = Math.Abs(fp);
                    if (a > mf) mf = a;
                    int n = i + 1 == k ? 0 : i + 1;
                    double eu = au[n] - u, ev = av[n] - v, f2 = -dl * eu * ev, f1 = fu * eu + fv * ev - dl * (u * ev + v * eu);
                    if (Math.Abs(f2) > StatEps)
                    {
                        double s = -f1 / (2 * f2);
                        if (s > 0 && s < 1) { double fs = Math.Abs(fp + f1 * s + f2 * s * s); if (fs > mf) mf = fs; }
                    }
                }
                if (np == pA.Length) { int n2 = np * 2; Array.Resize(ref pA, n2); Array.Resize(ref pMin, n2); Array.Resize(ref pMax, n2); Array.Resize(ref pF, n2); Array.Resize(ref pG, n2); }
                // highest first by the lowest point; equal ones keep the triangles' order (stable insertion)
                int at = np;
                while (at > 0 && pMin[at - 1] < hmin) { pA[at] = pA[at - 1]; pMin[at] = pMin[at - 1]; pMax[at] = pMax[at - 1]; pF[at] = pF[at - 1]; pG[at] = pG[at - 1]; at--; }
                pA[at] = area; pMin[at] = hmin; pMax[at] = hmax; pF[at] = mf; pG[at] = G[t];
                np++;
            }
            if (np == 0) return 0;
            double cum = 0, smin = double.PositiveInfinity, fm = 0; byte grp = pG[0]; int s0 = 0;
            for (; s0 < np && cum < 1.0 - AreaTol; s0++)
            {
                cum += pA[s0];
                if (pMin[s0] < smin) smin = pMin[s0];
                if (pF[s0] > fm) fm = pF[s0];
                if (pG[s0] != grp) return 0;
            }
            if (Math.Abs(cum - 1.0) > AreaTol) return 0;
            for (int r = s0; r < np; r++) if (pMax[r] > smin) return 0;
            if (!(fm <= tol)) return 0;
            return grp;
        }

        /// <summary>Sutherland-Hodgman against one side of the unit cell: keep u (or v) >= val, or <= val.</summary>
        static int Clip(double[] iu, double[] iv, int n, double[] ou, double[] ov, bool onU, double val, bool keepGe)
        {
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1;
                double si = ((onU ? iu[i] : iv[i]) - val) * (keepGe ? 1.0 : -1.0), sj = ((onU ? iu[j] : iv[j]) - val) * (keepGe ? 1.0 : -1.0);
                bool ini = si >= 0, inj = sj >= 0;
                if (ini) { ou[m] = iu[i]; ov[m] = iv[i]; m++; }
                if (ini != inj)
                {
                    double t = si / (si - sj), u = iu[i] + t * (iu[j] - iu[i]), v = iv[i] + t * (iv[j] - iv[i]);
                    if (onU) u = val; else v = val;
                    ou[m] = u; ov[m] = v; m++;
                }
            }
            return m;
        }
    }
}
