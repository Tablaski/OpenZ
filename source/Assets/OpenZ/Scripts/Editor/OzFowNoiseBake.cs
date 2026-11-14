// OpenZ (GPL-3.0) - version 1.4, FOG OF WAR: the cloud noise textures, baked once in the editor and shipped as assets.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4 FOG OF WAR (premium clouds, 5 Nov). Round 4 (layered cloud sheets): the cloud shader reads three tileable 2D cloud
    /// sheets (Resources/OpenZ/FX/FowSheets/: OzFowSheet 1024^2, OzFowStreak 512^2, OzFowFlow 256^2, RGBA8 PNG, linear, mips, Repeat),
    /// baked by the kit's bake_sheets.py (numpy, fixed seeds, the same bytes on every bake), and the blue noise below (the output dither),
    /// the only texture this file still bakes. The three 3D / curl textures of the earlier raymarch are no longer made or shipped (their
    /// code stays below for reference). Noise Count checks the four textures the shader reads.
    /// Earlier (rounds 1 - 3): the four noise textures the raymarch read (Resources/OpenZ/FX/FowNoise/), made here from one seed (no copy of
    /// another package's texture, nothing downloaded), deterministic, tileable in every axis:
    /// <list type="bullet">
    /// <item>OzFowShape: 3D 128^3 RG16 with mips. R "sea": Perlin-Worley FBM (Perlin 4 octaves from 4 a tile, inverted Worley at 4, 8 and 16
    /// cells a tile). G "billows": inverted Worley at 3, 6 and 12 cells (0.6 / 0.3 / 0.1) 75 % + Perlin (2 octaves) 25 %. Both stretched to
    /// their 0.5 - 99.5 percentiles.</item>
    /// <item>OzFowDetail: 3D 64^3 R8 with mips: inverted Worley FBM at 4 / 8 / 16 cells (0.625 / 0.25 / 0.125).</item>
    /// <item>OzFowCurl: 2D 128^2 RG16 with mips: the curl of a tileable Perlin (3 octaves from 4), stored as 0.5 + 0.5 v.</item>
    /// <item>OzFowBlue: 2D 64^2 R8, point: void-and-cluster blue noise (sigma 1.5), the output dither only.</item>
    /// </list>
    /// Menus: OpenZ/V1.4/Fog of War: Bake Cloud Noise, and Noise Count (headless) with rows N1 - N5.
    /// </summary>
    public static class OzFowNoiseBake
    {
        public const int Seed = 20261105;
        public const string Dir = "Assets/OpenZ/Resources/OpenZ/FX/FowNoise";
        public const int ShapeN = 128, DetailN = 64, CurlN = 128, BlueN = 64;
        const string Tag = "[OpenZ V1.4 FogOfWar noise]";

        // ================================================================ menus
        [MenuItem("OpenZ/V1.4/Fog of War: Bake Cloud Noise")]
        public static void BakeMenu()
        {
            // round 4: only the blue noise (the cloud sheets come from bake_sheets.py; the raymarch's 3D textures are no longer shipped)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var blue = BakeBlue();
            Directory.CreateDirectory(Path.GetFullPath(Dir));
            AssetDatabase.Refresh();
            Save(MakeBlue(blue), "OzFowBlue.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{Tag} baked the blue noise into {Dir} in {sw.Elapsed.TotalSeconds:0.00} s (seed {Seed})");
        }

        /// <summary>Round 4: the cloud sheets (Resources/OpenZ/FX/FowSheets/, bake_sheets.py): size, mips, and the MD5 of the baked PNG
        /// (the same bytes on every bake).</summary>
        public const string SheetsDir = "Assets/OpenZ/Resources/OpenZ/FX/FowSheets";
        static readonly (string name, int size, int mips, string md5)[] Sheets =
        {
            ("OzFowSheet", 1024, 11, "ebf6cc58aee5f88081de43cf2380151b"),
            ("OzFowStreak", 512, 10, "3e6c7d7528db17dd8e6a41d637d91b9c"),
            ("OzFowFlow", 256, 9, "b9729bdfad57c2060782fb353be9862c"),
        };

        static void Save(UnityEngine.Object tex, string file)
        {
            string path = Dir + "/" + file;
            var old = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (old != null && old.GetType() == tex.GetType())
            {
                EditorUtility.CopySerialized(tex, old);                                   // keeps the asset's GUID
                old.name = Path.GetFileNameWithoutExtension(file);
                EditorUtility.SetDirty(old);
                UnityEngine.Object.DestroyImmediate(tex);
            }
            else
            {
                tex.name = Path.GetFileNameWithoutExtension(file);
                AssetDatabase.CreateAsset(tex, path);
            }
            // fix round 1: the shipped asset keeps no CPU copy (about 5 MB of RAM in the game); the checks read it back from the GPU (Level0)
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset != null)
            {
                var so = new SerializedObject(asset); var rd = so.FindProperty("m_IsReadable");
                if (rd != null && rd.boolValue) { rd.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset); }
            }
        }

        /// <summary>A baked texture's level 0 bytes: its CPU copy when it has one, else read back from the graphics card.</summary>
        static byte[] Level0(Texture t)
        {
            if (t == null) return new byte[0];
            if (t.isReadable)
            {
                if (t is Texture3D a) return a.GetPixelData<byte>(0).ToArray();
                if (t is Texture2D b) return b.GetPixelData<byte>(0).ToArray();
            }
            var req = UnityEngine.Rendering.AsyncGPUReadback.Request(t, 0);
            req.WaitForCompletion();
            if (req.hasError) { Debug.LogWarning($"{Tag} could not read {t.name} back from the graphics card"); return new byte[0]; }
            var all = new List<byte>();
            for (int l = 0; l < Math.Max(1, req.layerCount); l++) all.AddRange(req.GetData<byte>(l).ToArray());
            return all.ToArray();
        }

        // ================================================================ the bake (plain arrays, deterministic)
        public sealed class Baked
        {
            public byte[][] shape, detail, curl; public byte[] blue;
            public string Md5()
            {
                using (var md5 = MD5.Create())
                {
                    var all = new List<byte>();
                    foreach (var lv in shape) all.AddRange(lv);
                    foreach (var lv in detail) all.AddRange(lv);
                    foreach (var lv in curl) all.AddRange(lv);
                    all.AddRange(blue);
                    return BitConverter.ToString(md5.ComputeHash(all.ToArray())).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        public static Baked BakeAll()
        {
            var b = new Baked();
            b.shape = Mips3(BakeShape(), ShapeN, 2);
            b.detail = Mips3(BakeDetail(), DetailN, 1);
            b.curl = Mips2(BakeCurl(), CurlN, 2);
            b.blue = BakeBlue();
            return b;
        }

        // ---------------------------------------------------------------- hashing
        static uint Hash(int x, int y, int z, uint s)
        {
            unchecked
            {
                uint h = s * 0x9E3779B1u + 0x7F4A7C15u;
                h ^= (uint)x * 0x85EBCA77u; h = (h << 13) | (h >> 19); h *= 0xC2B2AE3Du;
                h ^= (uint)y * 0x27D4EB2Fu; h = (h << 13) | (h >> 19); h *= 0x165667B1u;
                h ^= (uint)z * 0x9E3779B1u; h = (h << 13) | (h >> 19); h *= 0x85EBCA77u;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return h;
            }
        }
        static float U01(uint h) => (h >> 8) * (1f / 16777216f);
        static int Mod(int a, int n) { int r = a % n; return r < 0 ? r + n : r; }

        // ---------------------------------------------------------------- tileable Perlin (gradient) noise
        static readonly float[] G3 = { 1, 1, 0, -1, 1, 0, 1, -1, 0, -1, -1, 0, 1, 0, 1, -1, 0, 1, 1, 0, -1, -1, 0, -1, 0, 1, 1, 0, -1, 1, 0, 1, -1, 0, -1, -1 };
        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Grad(int ix, int iy, int iz, int per, uint s, float fx, float fy, float fz)
        {
            int k = (int)(Hash(Mod(ix, per), Mod(iy, per), Mod(iz, per), s) % 12u) * 3;
            return G3[k] * fx + G3[k + 1] * fy + G3[k + 2] * fz;
        }

        /// <summary>Perlin noise at a point in lattice units, period <paramref name="per"/> cells in every axis; about -1 .. 1.</summary>
        static float Perlin3(float x, float y, float z, int per, uint s)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y), iz = (int)Math.Floor(z);
            float fx = x - ix, fy = y - iy, fz = z - iz;
            float u = Fade(fx), v = Fade(fy), w = Fade(fz);
            float n000 = Grad(ix, iy, iz, per, s, fx, fy, fz), n100 = Grad(ix + 1, iy, iz, per, s, fx - 1, fy, fz);
            float n010 = Grad(ix, iy + 1, iz, per, s, fx, fy - 1, fz), n110 = Grad(ix + 1, iy + 1, iz, per, s, fx - 1, fy - 1, fz);
            float n001 = Grad(ix, iy, iz + 1, per, s, fx, fy, fz - 1), n101 = Grad(ix + 1, iy, iz + 1, per, s, fx - 1, fy, fz - 1);
            float n011 = Grad(ix, iy + 1, iz + 1, per, s, fx, fy - 1, fz - 1), n111 = Grad(ix + 1, iy + 1, iz + 1, per, s, fx - 1, fy - 1, fz - 1);
            return Lerp(Lerp(Lerp(n000, n100, u), Lerp(n010, n110, u), v), Lerp(Lerp(n001, n101, u), Lerp(n011, n111, u), v), w);
        }

        /// <summary>Tileable Perlin FBM over a tile (u in 0..1), <paramref name="baseCells"/> lattice cells a tile for the first octave; 0..1.</summary>
        static float PerlinFbm(float ux, float uy, float uz, int baseCells, int octaves, uint s)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                int f = baseCells << o;
                sum += amp * Perlin3(ux * f, uy * f, uz * f, f, s + (uint)(o * 101));
                norm += amp; amp *= 0.5f;
            }
            return Mathf.Clamp01(0.5f + 0.5f * sum / norm * 1.6f);
        }

        // ---------------------------------------------------------------- tileable Worley (cellular) noise
        /// <summary>One feature point per cell of an n^3 grid over the tile (cell units, 0..1 inside its cell).</summary>
        static float[] Points(int n, uint s)
        {
            var p = new float[n * n * n * 3];
            for (int z = 0; z < n; z++) for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                int i = ((z * n + y) * n + x) * 3;
                p[i] = U01(Hash(x, y, z, s)); p[i + 1] = U01(Hash(x, y, z, s + 1)); p[i + 2] = U01(Hash(x, y, z, s + 2));
            }
            return p;
        }

        /// <summary>Inverted Worley F1 (1 near a feature point, 0 a cell away), n cells a tile, u in 0..1.</summary>
        static float Worley(float ux, float uy, float uz, int n, float[] pts)
        {
            float qx = ux * n, qy = uy * n, qz = uz * n;
            int cx = (int)Math.Floor(qx), cy = (int)Math.Floor(qy), cz = (int)Math.Floor(qz);
            float best = 9f;
            for (int dz = -1; dz <= 1; dz++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int ax = cx + dx, ay = cy + dy, az = cz + dz;
                        int i = ((Mod(az, n) * n + Mod(ay, n)) * n + Mod(ax, n)) * 3;
                        float ex = ax + pts[i] - qx, ey = ay + pts[i + 1] - qy, ez = az + pts[i + 2] - qz;
                        float d2 = ex * ex + ey * ey + ez * ez;
                        if (d2 < best) best = d2;
                    }
            return 1f - Mathf.Clamp01((float)Math.Sqrt(best));
        }

        static float Remap(float v, float lo, float hi, float a, float b) => a + (v - lo) / Math.Max(hi - lo, 1e-6f) * (b - a);

        /// <summary>Stretches the values to their 0.5 - 99.5 percentiles (0..1), in place.</summary>
        static void Stretch(float[] v)
        {
            var sample = new float[(v.Length + 6) / 7];
            for (int i = 0, k = 0; i < v.Length && k < sample.Length; i += 7, k++) sample[k] = v[i];
            Array.Sort(sample);
            float lo = sample[(int)(sample.Length * 0.005)], hi = sample[Math.Min(sample.Length - 1, (int)(sample.Length * 0.995))];
            float inv = 1f / Math.Max(hi - lo, 1e-6f);
            for (int i = 0; i < v.Length; i++) v[i] = Mathf.Clamp01((v[i] - lo) * inv);
        }

        static byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        // ---------------------------------------------------------------- the four textures (level 0)
        static byte[] BakeShape()
        {
            int n = ShapeN, n3 = n * n * n; uint s = Seed;
            var w4 = Points(4, s + 10); var w8 = Points(8, s + 20); var w16 = Points(16, s + 30);
            var w3 = Points(3, s + 40); var w6 = Points(6, s + 50); var w12 = Points(12, s + 60);
            var r = new float[n3]; var g = new float[n3];
            Parallel.For(0, n, z =>
            {
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float ux = (x + 0.5f) / n, uy = (y + 0.5f) / n, uz = (z + 0.5f) / n;
                        float perlin = PerlinFbm(ux, uy, uz, 4, 4, s + 1);
                        float a1 = Worley(ux, uy, uz, 4, w4), a2 = Worley(ux, uy, uz, 8, w8), a3 = Worley(ux, uy, uz, 16, w16);
                        float pw = Remap(perlin, a1 - 1f, 1f, 0f, 1f);
                        float wf = 0.625f * a1 + 0.25f * a2 + 0.125f * a3;
                        int i = (z * n + y) * n + x;
                        r[i] = Mathf.Clamp01(Remap(pw, wf - 1f, 1f, 0f, 1f));
                        float b3 = Worley(ux, uy, uz, 3, w3), b6 = Worley(ux, uy, uz, 6, w6), b12 = Worley(ux, uy, uz, 12, w12);
                        g[i] = 0.75f * (0.6f * b3 + 0.3f * b6 + 0.1f * b12) + 0.25f * PerlinFbm(ux, uy, uz, 4, 2, s + 7);
                    }
            });
            Stretch(r); Stretch(g);
            var o = new byte[n3 * 2];
            for (int i = 0; i < n3; i++) { o[2 * i] = B(r[i]); o[2 * i + 1] = B(g[i]); }
            return o;
        }

        static byte[] BakeDetail()
        {
            int n = DetailN, n3 = n * n * n; uint s = Seed + 1000;
            var w4 = Points(4, s + 1); var w8 = Points(8, s + 2); var w16 = Points(16, s + 3);
            var v = new float[n3];
            Parallel.For(0, n, z =>
            {
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float ux = (x + 0.5f) / n, uy = (y + 0.5f) / n, uz = (z + 0.5f) / n;
                        v[(z * n + y) * n + x] = 0.625f * Worley(ux, uy, uz, 4, w4) + 0.25f * Worley(ux, uy, uz, 8, w8) + 0.125f * Worley(ux, uy, uz, 16, w16);
                    }
            });
            Stretch(v);
            var o = new byte[n3];
            for (int i = 0; i < n3; i++) o[i] = B(v[i]);
            return o;
        }

        static byte[] BakeCurl()
        {
            int n = CurlN; uint s = Seed + 2000;
            var psi = new float[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float ux = (float)x / n, uy = (float)y / n, sum = 0f, amp = 1f;
                    for (int o = 0; o < 3; o++) { int f = 4 << o; sum += amp * Perlin3(ux * f, uy * f, 0.5f, f, s + (uint)o); amp *= 0.5f; }
                    psi[y * n + x] = sum;
                }
            var vx = new float[n * n]; var vy = new float[n * n]; float mx = 1e-6f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dpdx = 0.5f * (psi[y * n + Mod(x + 1, n)] - psi[y * n + Mod(x - 1, n)]);
                    float dpdy = 0.5f * (psi[Mod(y + 1, n) * n + x] - psi[Mod(y - 1, n) * n + x]);
                    int i = y * n + x; vx[i] = dpdy; vy[i] = -dpdx;
                    mx = Math.Max(mx, (float)Math.Sqrt(dpdy * dpdy + dpdx * dpdx));
                }
            var o2 = new byte[n * n * 2];
            for (int i = 0; i < n * n; i++) { o2[2 * i] = B(0.5f + 0.5f * vx[i] / mx); o2[2 * i + 1] = B(0.5f + 0.5f * vy[i] / mx); }
            return o2;
        }

        /// <summary>Void-and-cluster blue noise (Ulichney), 64 x 64, Gaussian sigma 1.5 on a torus: the rank of every pixel as a byte.</summary>
        static byte[] BakeBlue()
        {
            int n = BlueN, nn = n * n; const float sigma = 1.5f;
            var lut = new float[nn];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int dx = Math.Min(x, n - x), dy = Math.Min(y, n - y);
                    lut[y * n + x] = (float)Math.Exp(-(dx * dx + dy * dy) / (2.0 * sigma * sigma));
                }
            var on = new bool[nn]; var energy = new float[nn];
            void Splat(int p, float sgn)
            {
                int px = p % n, py = p / n;
                for (int y = 0; y < n; y++) { int ry = Mod(y - py, n) * n; int row = y * n; for (int x = 0; x < n; x++) energy[row + x] += sgn * lut[ry + Mod(x - px, n)]; }
            }
            int Tightest() { int b = -1; float e = float.MinValue; for (int i = 0; i < nn; i++) if (on[i] && energy[i] > e) { e = energy[i]; b = i; } return b; }
            int Largest() { int b = -1; float e = float.MaxValue; for (int i = 0; i < nn; i++) if (!on[i] && energy[i] < e) { e = energy[i]; b = i; } return b; }

            // initial pattern: 10 % of the pixels, then relaxed (move the tightest cluster to the largest void until it stops)
            int ones = nn / 10; uint s = Seed + 3000; int placed = 0, k = 0;
            while (placed < ones) { int p = (int)(Hash(k++, 0, 0, s) % (uint)nn); if (!on[p]) { on[p] = true; Splat(p, 1f); placed++; } }
            for (int it = 0; it < 4 * nn; it++)
            {
                int c = Tightest(); on[c] = false; Splat(c, -1f);
                int v = Largest(); if (v == c) { on[c] = true; Splat(c, 1f); break; }
                on[v] = true; Splat(v, 1f);
            }
            var rank = new int[nn];
            var start = (bool[])on.Clone(); var startE = (float[])energy.Clone();
            // phase 1: ranks below the initial count, removing the tightest clusters
            for (int r = ones - 1; r >= 0; r--) { int c = Tightest(); on[c] = false; Splat(c, -1f); rank[c] = r; }
            // phase 2: from the initial pattern, filling the largest voids up to half
            Array.Copy(start, on, nn); Array.Copy(startE, energy, nn);
            for (int r = ones; r < nn / 2; r++) { int v = Largest(); on[v] = true; Splat(v, 1f); rank[v] = r; }
            // phase 3: the rest, as the tightest clusters of the empty pixels (energy of the zeros)
            Array.Clear(energy, 0, nn);
            for (int i = 0; i < nn; i++) if (!on[i]) Splat(i, 1f);
            for (int r = nn / 2; r < nn; r++)
            {
                int c = -1; float e = float.MinValue;
                for (int i = 0; i < nn; i++) if (!on[i] && energy[i] > e) { e = energy[i]; c = i; }
                on[c] = true; Splat(c, -1f); rank[c] = r;
            }
            var o = new byte[nn];
            for (int i = 0; i < nn; i++) o[i] = (byte)(rank[i] * 256 / nn);
            return o;
        }

        // ---------------------------------------------------------------- mips (box filter; a tileable level stays tileable)
        static byte[][] Mips3(byte[] l0, int n, int ch)
        {
            var levels = new List<byte[]> { l0 };
            var cur = l0; int size = n;
            while (size > 1)
            {
                int m = size / 2; var nx = new byte[m * m * m * ch];
                for (int z = 0; z < m; z++) for (int y = 0; y < m; y++) for (int x = 0; x < m; x++)
                    for (int c = 0; c < ch; c++)
                    {
                        int sum = 0;
                        for (int dz = 0; dz < 2; dz++) for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
                            sum += cur[(((2 * z + dz) * size + 2 * y + dy) * size + 2 * x + dx) * ch + c];
                        nx[((z * m + y) * m + x) * ch + c] = (byte)((sum + 4) / 8);
                    }
                levels.Add(nx); cur = nx; size = m;
            }
            return levels.ToArray();
        }

        static byte[][] Mips2(byte[] l0, int n, int ch)
        {
            var levels = new List<byte[]> { l0 };
            var cur = l0; int size = n;
            while (size > 1)
            {
                int m = size / 2; var nx = new byte[m * m * ch];
                for (int y = 0; y < m; y++) for (int x = 0; x < m; x++)
                    for (int c = 0; c < ch; c++)
                    {
                        int sum = cur[((2 * y) * size + 2 * x) * ch + c] + cur[((2 * y) * size + 2 * x + 1) * ch + c]
                                + cur[((2 * y + 1) * size + 2 * x) * ch + c] + cur[((2 * y + 1) * size + 2 * x + 1) * ch + c];
                        nx[(y * m + x) * ch + c] = (byte)((sum + 2) / 4);
                    }
                levels.Add(nx); cur = nx; size = m;
            }
            return levels.ToArray();
        }

        // ---------------------------------------------------------------- Unity textures
        static Texture3D MakeShape(byte[][] lv)
        {
            var t = new Texture3D(ShapeN, ShapeN, ShapeN, TextureFormat.RG16, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 0 };
            for (int m = 0; m < lv.Length && m < t.mipmapCount; m++) t.SetPixelData(lv[m], m);
            t.Apply(false, false);
            return t;
        }

        static Texture3D MakeDetail(byte[][] lv)
        {
            var t = new Texture3D(DetailN, DetailN, DetailN, TextureFormat.R8, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 0 };
            for (int m = 0; m < lv.Length && m < t.mipmapCount; m++) t.SetPixelData(lv[m], m);
            t.Apply(false, false);
            return t;
        }

        static Texture2D MakeCurl(byte[][] lv)
        {
            var t = new Texture2D(CurlN, CurlN, TextureFormat.RG16, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 0 };
            for (int m = 0; m < lv.Length && m < t.mipmapCount; m++) t.SetPixelData(lv[m], m);
            t.Apply(false, false);
            return t;
        }

        static Texture2D MakeBlue(byte[] data)
        {
            var t = new Texture2D(BlueN, BlueN, TextureFormat.R8, false, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, anisoLevel = 0 };
            t.SetPixelData(data, 0);
            t.Apply(false, false);
            return t;
        }

        // ================================================================ Noise Count (headless)
        static readonly List<string> lines = new List<string>();
        static int pass, total;
        static void Row(string id, bool ok, string text) { total++; if (ok) pass++; string s = $"  {(ok ? "PASS" : "FAIL")} {id} {text}"; lines.Add(s); Debug.Log(s); }

        [MenuItem("OpenZ/V1.4/Fog of War: Noise Count (headless)")]
        public static void NoiseCount()
        {
            lines.Clear(); pass = total = 0;
            string head = $"{Tag} Noise Count (headless): the cloud sheets and the blue noise (round 4)"; lines.Add(head); Debug.Log(head);
            var tex = new Texture2D[Sheets.Length]; var imp = new TextureImporter[Sheets.Length];
            for (int i = 0; i < Sheets.Length; i++)
            {
                string path = SheetsDir + "/" + Sheets[i].name + ".png";
                tex[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path); imp[i] = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            var blue = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/OzFowBlue.asset");
            bool have = blue && Array.TrueForAll(tex, x => x) && Array.TrueForAll(imp, x => x != null);
            var sb = new System.Text.StringBuilder(); bool fmt = have;
            for (int i = 0; i < Sheets.Length && have; i++)
            {
                var t = tex[i]; var m = imp[i];
                bool ok = t.width == Sheets[i].size && t.height == Sheets[i].size && t.format == TextureFormat.RGBA32 && t.mipmapCount == Sheets[i].mips
                          && t.wrapMode == TextureWrapMode.Repeat && !m.sRGBTexture && m.mipmapEnabled && m.textureCompression == TextureImporterCompression.Uncompressed;
                fmt &= ok;
                sb.Append($"{Sheets[i].name} {t.width}^2 {t.format} {t.mipmapCount} mips {t.wrapMode} {(m.sRGBTexture ? "sRGB" : "linear")} {t.filterMode} aniso {t.anisoLevel}{(ok ? "" : " WRONG")}; ");
            }
            fmt &= have && blue.width == BlueN && blue.format == TextureFormat.R8 && blue.mipmapCount == 1 && blue.filterMode == FilterMode.Point;
            Row("N1", fmt, have ? "sizes and formats: " + sb + $"blue {blue.width}^2 {blue.format} {blue.filterMode}"
                                : "a texture is missing: the sheets in " + SheetsDir + " (bake_sheets.py), the blue noise in " + Dir + " (OpenZ/V1.4/Fog of War: Bake Cloud Noise)");
            if (!have) { End(); return; }

            // N2: tile seams. The step across the wrap is no rougher than a step inside (mean over every row and column), each channel.
            double seam = 0, inner = 0; int cnt = 0; var lv = new byte[Sheets.Length][];
            for (int i = 0; i < Sheets.Length; i++)
            {
                var a = Level0(tex[i]); lv[i] = a; int n = Sheets[i].size;
                if (a.Length < n * n * 4) continue;
                for (int c = 0; c < 4; c++)
                    for (int y = 0; y < n; y++)
                    {
                        seam += Math.Abs(a[(y * n + n - 1) * 4 + c] - a[(y * n) * 4 + c]); inner += Math.Abs(a[(y * n + n / 2) * 4 + c] - a[(y * n + n / 2 - 1) * 4 + c]);
                        seam += Math.Abs(a[((n - 1) * n + y) * 4 + c] - a[y * 4 + c]); inner += Math.Abs(a[((n / 2) * n + y) * 4 + c] - a[((n / 2 - 1) * n + y) * 4 + c]);
                        cnt += 2;
                    }
            }
            double s2 = cnt > 0 ? (seam - inner) / cnt : 99;
            Row("N2", cnt > 0 && s2 <= 1.0, $"tile seams of the three sheets: the step across the wrap minus a step inside, mean {s2:0.000} levels (bar 1 level); mean step inside {(cnt > 0 ? inner / cnt : 0):0.00} levels");

            // N3: deterministic: the PNGs are the bytes bake_sheets.py makes; a re-bake of the blue noise gives the asset's bytes
            var sb3 = new System.Text.StringBuilder(); bool same = true;
            using (var md5 = MD5.Create())
                for (int i = 0; i < Sheets.Length; i++)
                {
                    string h = BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(Path.GetFullPath(SheetsDir + "/" + Sheets[i].name + ".png")))).Replace("-", "").ToLowerInvariant();
                    bool ok = h == Sheets[i].md5; same &= ok; sb3.Append($"{Sheets[i].name} {h}{(ok ? "" : " DIFFERS")}; ");
                }
            bool blueSame = Same(BakeBlue(), Level0(blue));
            Row("N3", same && blueSame, "deterministic: " + sb3 + $"the blue noise re-baked {(blueSame ? "the same bytes" : "DIFFERS (bake again)")}");

            // N4: spread of the sheet's height and of the streaks (5 - 95 %)
            float sh = Spread(Channel(lv[0], 0)), sk = Spread(Channel(lv[1], 1)), sf = Spread(Channel(lv[2], 2));
            Row("N4", sh >= 0.45f && sk >= 0.45f && sf >= 0.3f, $"value spread 5 - 95 %: sheet height {sh:0.00}, wind streaks {sk:0.00} (bar 0.45), breathing {sf:0.00} (bar 0.3)");

            // N5: blue noise, power in the lowest frequencies (radius <= 8 of 64) against white noise of the same variance
            var bl = Level0(blue);
            double low = LowBandRatio(bl, BlueN);
            Row("N5", low <= 0.10, $"blue noise: low-frequency power {low * 100:0.0} % of white noise (bar 10 %)");
            End();
        }

        static byte[] Channel(byte[] rgba, int c) { var o = new byte[rgba.Length / 4]; for (int i = 0; i < o.Length; i++) o[i] = rgba[4 * i + c]; return o; }

        static bool Same(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }

        static float Spread(byte[] v)
        {
            var h = new int[256]; foreach (var b in v) h[b]++;
            int lo = -1, hi = -1, acc = 0;
            for (int i = 0; i < 256; i++) { acc += h[i]; if (lo < 0 && acc >= v.Length * 0.05) lo = i; if (hi < 0 && acc >= v.Length * 0.95) hi = i; }
            return (hi - lo) / 255f;
        }

        static double LowBandRatio(byte[] v, int n)
        {
            double mean = 0; foreach (var b in v) mean += b; mean /= v.Length;
            double all = 0, low = 0; int nl = 0, na = 0;
            for (int ky = 0; ky < n; ky++)
                for (int kx = 0; kx < n; kx++)
                {
                    if (kx == 0 && ky == 0) continue;
                    double re = 0, im = 0;
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            double a = -2.0 * Math.PI * ((double)kx * x / n + (double)ky * y / n);
                            double val = v[y * n + x] - mean;
                            re += val * Math.Cos(a); im += val * Math.Sin(a);
                        }
                    double p = re * re + im * im;
                    int fx = Math.Min(kx, n - kx), fy = Math.Min(ky, n - ky);
                    all += p; na++;
                    if (fx * fx + fy * fy <= 64) { low += p; nl++; }
                }
            return (low / Math.Max(1, nl)) / (all / Math.Max(1, na));
        }

        static void End()
        {
            string s = $"{Tag} {(pass == total ? "PASS" : "FAIL")} {pass}/{total}"; lines.Add(s); Debug.Log(s);
            try
            {
                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "oz_v14_fog_of_war_noise.txt"), string.Join("\n", lines) + "\n");
            }
            catch (Exception e) { Debug.LogWarning($"{Tag} could not write the log file: {e.Message}"); }
        }
    }
}
