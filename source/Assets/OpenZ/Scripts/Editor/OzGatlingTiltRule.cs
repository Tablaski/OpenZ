namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.3 gatling tilt: which triangle of the yaw mesh goes where. Plain numbers, no Unity type, so tools/harness runs this file on plain .NET on the real mesh and compares it with
    /// tools/classify.py to the triangle. Root space, the triangle's centroid; then every connected piece goes whole to the side holding most of its area, so nothing tears.
    /// STAY: collar, neck, yoke plates, drum, seat kit. HEAD: shield, receiver, tunnel. SKIRT: the shield's lower apron. The breech's corners too, compared with tools/pose.py the same way.
    /// </summary>
    public static class OzGatlingTiltRule
    {
        public const int Stay = 0, Head = 1, Skirt = 2;
        public const int Verts = 10438, Tris = 5623, StayTris = 5096, HeadTris = 487, SkirtTris = 40, KitTris = 1408, HeadVerts = 871, SkirtVerts = 77;
        /// <summary>gunner_seats_v1: the seat kit's vertices start here (View/CannonPolish.cs GunChairDebris reads the same number).</summary>
        public const int KitFrom = 7390;
        /// <summary>The hub bolt in the yoke plates (root space), the head's hinge; the drum's front flange.</summary>
        public const float HubX = -0.21f, HubY = 1.635f, JointX = -0.47f;
        static readonly float[] YokeX = { -.66f, -.46f, -.30f, -.02f, .06f, -.14f }, YokeY = { 1.0f, 1.86f, 1.95f, 1.93f, 1.72f, 1.0f };

        /// <summary>The breech: a sheet turned about the hub from BreechA0 to BreechA1 deg (0 = +x, 90 = up), and its two ends; BreechZ, BreechR = |z| and radius, 15 mm or so inside the head's roof at that |z|, so the
        /// head covers it at rest. Then the plate in the drum's mouth, 4 cm behind the head's rear end: MouthZ, MouthY = half its outline, just inside the mouth at MouthX. tools/pose.py carries the same numbers.</summary>
        public const float BreechA0 = 72f, BreechA1 = 135f, MouthX = -0.515f, MouthMidY = 1.96f;
        public const int BreechSeg = 8, BreechTris = 316;
        static readonly float[] BreechZ = { .06f, .09f, .27f, .30f, .33f, .36f, .39f, .415f, .465f }, BreechR = { .66f, .68f, .69f, .632f, .615f, .58f, .54f, .47f, .46f };
        static readonly float[] MouthZ = { 0f, .265f, .295f, .385f, .45f, .475f, .49f, .5f, .49f, .465f, .425f, .38f, .365f, .365f, 0f }, MouthY = { 2.35f, 2.34f, 2.325f, 2.275f, 2.175f, 2.115f, 2.035f, 1.96f, 1.84f, 1.76f, 1.68f, 1.62f, 1.58f, 1.3f, 1.3f };

        static bool InYoke(float x, float y)
        {
            bool r = false;
            for (int i = 0, j = YokeX.Length - 1; i < YokeX.Length; j = i++)
                if ((YokeY[i] > y) != (YokeY[j] > y) && x < (YokeX[j] - YokeX[i]) * (y - YokeY[i]) / (YokeY[j] - YokeY[i] + 1e-12f) + YokeX[i]) r = !r;
            return r;
        }

        static int TriRule(bool kit, float x, float y, float z)
        {
            if (kit) return Stay;
            bool yoke = (z < 0f ? -z : z) > 0.42f && InYoke(x, y);
            bool low = y < 1.2f || (x < 0.2f && y < 1.46f);
            if (yoke || low || !(x > JointX)) return Stay;
            return x >= 0.2f && y < 1.6f ? Skirt : Head;
        }

        /// <summary>x, y, z = the vertices in root space, tri = three indices per triangle. One label per triangle.</summary>
        public static byte[] Split(float[] x, float[] y, float[] z, int[] tri)
        {
            int nt = tri.Length / 3, nv = x.Length;
            var par = new int[nv]; for (int i = 0; i < nv; i++) par[i] = i;
            int Find(int a) { while (par[a] != a) { par[a] = par[par[a]]; a = par[a]; } return a; }
            var raw = new byte[nt]; var area = new double[nt];
            for (int t = 0; t < nt; t++)
            {
                int a = tri[t * 3], b = tri[t * 3 + 1], c = tri[t * 3 + 2];
                raw[t] = (byte)TriRule(a >= KitFrom && b >= KitFrom && c >= KitFrom, (x[a] + x[b] + x[c]) / 3f, (y[a] + y[b] + y[c]) / 3f, (z[a] + z[b] + z[c]) / 3f);
                double ux = x[b] - x[a], uy = y[b] - y[a], uz = z[b] - z[a], vx = x[c] - x[a], vy = y[c] - y[a], vz = z[c] - z[a];
                double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                area[t] = 0.5 * System.Math.Sqrt(nx * nx + ny * ny + nz * nz);
                int fa = Find(a); par[Find(b)] = fa; par[Find(c)] = fa;
            }
            var sum = new System.Collections.Generic.Dictionary<int, double[]>();
            for (int t = 0; t < nt; t++)
            {
                int r = Find(tri[t * 3]);
                if (!sum.TryGetValue(r, out var s)) sum[r] = s = new double[3];
                s[raw[t]] += area[t];
            }
            var lab = new byte[nt];
            for (int t = 0; t < nt; t++)
            {
                var s = sum[Find(tri[t * 3])];
                lab[t] = (byte)(s[Head] > s[Stay] ? (s[Skirt] > s[Head] ? Skirt : Head) : s[Skirt] > s[Stay] ? Skirt : Stay);
            }
            return lab;
        }

        /// <summary>The breech about the hub (the mesh's origin): position and normal of every corner, 6 floats each, 3 corners per triangle. Each front faces outward (Unity: the cross of the triangle's two sides).</summary>
        public static float[] Breech()
        {
            var o = new System.Collections.Generic.List<float>(BreechTris * 18);
            void Tri(float[] a, float[] b, float[] c, float ox, float oy, float oz)
            {
                float ux = b[0] - a[0], uy = b[1] - a[1], uz = b[2] - a[2], vx = c[0] - a[0], vy = c[1] - a[1], vz = c[2] - a[2];
                float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                if (nx * ox + ny * oy + nz * oz < 0f) { var s = b; b = c; c = s; nx = -nx; ny = -ny; nz = -nz; }
                float l = (float)System.Math.Sqrt(nx * nx + ny * ny + nz * nz); nx /= l; ny /= l; nz /= l;
                foreach (var p in new[] { a, b, c }) { o.Add(p[0]); o.Add(p[1]); o.Add(p[2]); o.Add(nx); o.Add(ny); o.Add(nz); }
            }
            float[] At(float deg, float r, float z) { double a = deg * System.Math.PI / 180.0; return new[] { (float)(r * System.Math.Cos(a)), (float)(r * System.Math.Sin(a)), z }; }
            int np = BreechZ.Length, nz2 = np * 2;
            float Z(int i) => i < np ? -BreechZ[np - 1 - i] : BreechZ[i - np];
            float R(int i) => i < np ? BreechR[np - 1 - i] : BreechR[i - np];
            for (int s = 0; s < BreechSeg; s++)
            {
                float a = BreechA0 + (BreechA1 - BreechA0) * s / BreechSeg, b = BreechA0 + (BreechA1 - BreechA0) * (s + 1) / BreechSeg; double m = (a + b) * 0.5 * System.Math.PI / 180.0;
                float ox = (float)System.Math.Cos(m), oy = (float)System.Math.Sin(m);
                for (int k = 0; k < nz2 - 1; k++)
                {
                    Tri(At(a, R(k), Z(k)), At(a, R(k + 1), Z(k + 1)), At(b, R(k + 1), Z(k + 1)), ox, oy, 0f);
                    Tri(At(a, R(k), Z(k)), At(b, R(k + 1), Z(k + 1)), At(b, R(k), Z(k)), ox, oy, 0f);
                }
                foreach (int e in new[] { 0, nz2 - 1 }) Tri(new[] { 0f, 0f, Z(e) }, At(a, R(e), Z(e)), At(b, R(e), Z(e)), 0f, 0f, Z(e) > 0f ? 1f : -1f);
            }
            float mx = MouthX - HubX;
            for (int i = 0; i < MouthZ.Length - 1; i++)
                foreach (float sg in new[] { 1f, -1f })
                    Tri(new[] { mx, MouthMidY - HubY, 0f }, new[] { mx, MouthY[i] - HubY, sg * MouthZ[i] }, new[] { mx, MouthY[i + 1] - HubY, sg * MouthZ[i + 1] }, 1f, 0f, 0f);
            return o.ToArray();
        }
    }
}
