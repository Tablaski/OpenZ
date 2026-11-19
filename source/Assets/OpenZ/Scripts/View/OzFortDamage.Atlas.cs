using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 fort_damage: the marks' atlas, made in code once per session at the first fort (like ScorchDecals' own soot atlas): 2 x 2 tiles of
    /// 256 px, rgb = the darkest tint, a = how much of it (the OpenZ/ScorchDecal shader multiplies the wall by lerp(1, rgb, a x opacity x
    /// strength): it only darkens, keeps the wall's own light, shadow and fog, never glows at night).
    /// <list type="bullet">
    /// <item>tile 0: rising soot (a plume that widens upward, streaked: smoke soot climbs a wall);</item>
    /// <item>tile 1: blast soot (a splash with streaks out of a dark middle and a few speckles);</item>
    /// <item>tile 2: cracks (3-5 branching cracks out of one point, tapered, with a faint dust halo);</item>
    /// <item>tile 3: a breach (a ragged dark hole, a chipped ring, spalls, cracks running out, soot round it).</item>
    /// </list>
    /// Every tile's border (6 % of it) is clear, so a turned or sized mark and the mip maps never show a neighbour. Fixed seeds: the same
    /// atlas every run.
    /// </summary>
    public partial class OzFortDamage
    {
        static Texture2D atlas; static Material atlasMat;
        const int Tile = 256, AtlasSize = Tile * 2;
        public static bool AtlasReady => atlasMat;

        static void EnsureAtlas()
        {
            if (atlasMat) return;
            var sh = Shader.Find("OpenZ/ScorchDecal");
            if (!sh) { Debug.LogWarning("[OpenZ] fort damage: shader OpenZ/ScorchDecal missing (no wall marks)"); return; }
            float t0 = Time.realtimeSinceStartup;
            var px = new Color32[AtlasSize * AtlasSize];
            SootRising(px, 0, 0, 101); SootSplash(px, Tile, 0, 202); Cracks(px, 0, Tile, 303); Breach(px, Tile, Tile, 404);
            atlas = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, true, false) { name = "OZ_FortDamageAtlas", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            atlas.SetPixels32(px); atlas.Apply(true, true);
            atlasMat = new Material(sh) { name = "OZ_FortDamageMarks", mainTexture = atlas };
            if (atlasMat.HasProperty("_Strength")) atlasMat.SetFloat("_Strength", 0.92f);
            Debug.Log($"[OpenZ] fort damage: mark atlas made in {(Time.realtimeSinceStartup - t0) * 1000f:0} ms");
        }

        // ------------------------------------------------------------------ noise
        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static float VNoise(float x, float y, int s)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Hash(xi, yi, s), b = Hash(xi + 1, yi, s), c = Hash(xi, yi + 1, s), d = Hash(xi + 1, yi + 1, s);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float x, float y, int s)
        {
            float v = 0f, a = 0.5f, f = 1f;
            for (int o = 0; o < 4; o++) { v += a * VNoise(x * f, y * f, s + o * 31); f *= 2.03f; a *= 0.5f; }
            return v / 0.9375f;
        }

        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

        /// <summary>Clear border: 1 inside, 0 at the tile's edge (u, v in -1 .. 1).</summary>
        static float Border(float u, float v) => Smooth(1f, 0.88f, Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)));

        static void Put(Color32[] px, int ox, int oy, int x, int y, Color c, float a)
        {
            a = Mathf.Clamp01(a);
            px[(oy + y) * AtlasSize + ox + x] = new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(a * 255f));
        }

        static readonly Color SootTint = new Color(0.13f, 0.11f, 0.10f), CrackTint = new Color(0.08f, 0.07f, 0.065f), HoleTint = new Color(0.045f, 0.04f, 0.038f), ChipTint = new Color(0.52f, 0.49f, 0.46f);

        // ------------------------------------------------------------------ tiles
        static void SootRising(Color32[] px, int ox, int oy, int s)
        {
            for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                {
                    float u = x / (Tile - 1f) * 2f - 1f, v = y / (Tile - 1f) * 2f - 1f;
                    float n = Fbm(u * 3.1f + 7f, v * 2.2f, s), n2 = Fbm(u * 9f, v * 3f, s + 9);
                    float sway = 0.10f * Mathf.Sin(v * 2.6f + 1.3f) + 0.08f * (n - 0.5f);
                    float w = 0.28f + 0.30f * Mathf.Clamp01(v + 0.6f);                                      // widens as it climbs
                    float dd = Mathf.Abs(u - sway) / w + 0.45f * (n - 0.5f);
                    float env = Smooth(-0.80f, -0.45f, v) * (1f - Smooth(0.15f, 0.92f, v + 0.25f * (n2 - 0.5f)));
                    float streak = 0.75f + 0.25f * Mathf.Sin(u * 38f + n * 7f);                             // vertical streaks
                    float a = (1f - Smooth(0.45f, 1.0f, dd)) * env * streak * (0.65f + 0.35f * n2);
                    a += 0.35f * (1f - Smooth(0.0f, 0.45f, Mathf.Sqrt(u * u + (v + 0.5f) * (v + 0.5f)) + 0.3f * (n - 0.5f)));   // the dark root where it burnt
                    Put(px, ox, oy, x, y, SootTint * (0.85f + 0.3f * n2), a * Border(u, v));
                }
        }

        static void SootSplash(Color32[] px, int ox, int oy, int s)
        {
            for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                {
                    float u = x / (Tile - 1f) * 2f - 1f, v = y / (Tile - 1f) * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
                    float streak = Fbm(Mathf.Cos(th) * 3f + 10f, Mathf.Sin(th) * 3f + r * 0.6f, s);
                    float n = Fbm(u * 6f, v * 6f, s + 5);
                    float a = (1f - Smooth(0.22f, 0.92f, r + (streak - 0.5f) * 0.55f)) * (0.55f + 0.45f * n);
                    a = Mathf.Max(a, 0.9f * (1f - Smooth(0.05f, 0.30f, r + 0.15f * (n - 0.5f))));          // the dark middle
                    if (r < 0.9f && Hash(x, y, s) > 0.992f) a = Mathf.Max(a, 0.8f);                          // speckles
                    Put(px, ox, oy, x, y, SootTint * (0.8f + 0.35f * n), a * Border(u, v));
                }
        }

        /// <summary>Tapered cracks drawn into an alpha buffer (distance to each segment, anti-aliased), a faint halo round them.</summary>
        static void CrackLines(float[] a, float[] halo, System.Random rnd, float cx, float cy, int mains, float reach)
        {
            for (int m = 0; m < mains; m++)
            {
                float ang = (float)(m * (Mathf.PI * 2f / mains) + rnd.NextDouble() * 0.9);
                Walk(a, halo, rnd, cx, cy, ang, reach * (0.75f + 0.35f * (float)rnd.NextDouble()), 2.6f, true);
            }
        }

        static void Walk(float[] a, float[] halo, System.Random rnd, float x, float y, float ang, float len, float width, bool branches)
        {
            float done = 0f;
            while (done < len)
            {
                float step = 7f + 9f * (float)rnd.NextDouble();
                ang += (float)(rnd.NextDouble() - 0.5) * 0.9f;
                float nx = x + Mathf.Cos(ang) * step, ny = y + Mathf.Sin(ang) * step;
                float w = Mathf.Lerp(width, 0.6f, done / len);
                Segment(a, halo, x, y, nx, ny, w);
                if (branches && rnd.NextDouble() < 0.30) Walk(a, halo, rnd, nx, ny, ang + (rnd.NextDouble() < 0.5 ? -1f : 1f) * (0.5f + 0.6f * (float)rnd.NextDouble()), (len - done) * 0.45f, w * 0.6f, false);
                x = nx; y = ny; done += step;
                if (x < 10 || y < 10 || x > Tile - 10 || y > Tile - 10) break;
            }
        }

        static void Segment(float[] a, float[] halo, float x0, float y0, float x1, float y1, float w)
        {
            int minx = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - w - 5)), maxx = Mathf.Min(Tile - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + w + 5));
            int miny = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - w - 5)), maxy = Mathf.Min(Tile - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + w + 5));
            float dx = x1 - x0, dy = y1 - y0, l2 = Mathf.Max(1e-4f, dx * dx + dy * dy);
            for (int y = miny; y <= maxy; y++)
                for (int x = minx; x <= maxx; x++)
                {
                    float t = Mathf.Clamp01(((x - x0) * dx + (y - y0) * dy) / l2);
                    float ex = x0 + dx * t - x, ey = y0 + dy * t - y, d = Mathf.Sqrt(ex * ex + ey * ey);
                    int i = y * Tile + x;
                    a[i] = Mathf.Max(a[i], Mathf.Clamp01(w * 0.5f + 0.5f - d));
                    halo[i] = Mathf.Max(halo[i], 0.22f * Mathf.Clamp01(1f - d / (w + 4f)));
                }
        }

        static void Cracks(Color32[] px, int ox, int oy, int s)
        {
            var rnd = new System.Random(s); var a = new float[Tile * Tile]; var halo = new float[Tile * Tile];
            CrackLines(a, halo, rnd, Tile * 0.5f + 6f, Tile * 0.52f, 4, Tile * 0.42f);
            for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                {
                    float u = x / (Tile - 1f) * 2f - 1f, v = y / (Tile - 1f) * 2f - 1f; int i = y * Tile + x;
                    Put(px, ox, oy, x, y, CrackTint, Mathf.Max(a[i], halo[i]) * Border(u, v));
                }
        }

        static void Breach(Color32[] px, int ox, int oy, int s)
        {
            var rnd = new System.Random(s); var a = new float[Tile * Tile]; var halo = new float[Tile * Tile];
            float R = 0.34f;
            for (int k = 0; k < 8; k++)
            {   // cracks running out of the hole's rim
                float ang = (float)(k * Mathf.PI * 2f / 8f + rnd.NextDouble() * 0.6);
                float sx = Tile * 0.5f + Mathf.Cos(ang) * R * Tile * 0.5f, sy = Tile * 0.5f + Mathf.Sin(ang) * R * Tile * 0.5f;
                Walk(a, halo, rnd, sx, sy, ang, Tile * (0.16f + 0.14f * (float)rnd.NextDouble()), 2.2f, true);
            }
            for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                {
                    float u = x / (Tile - 1f) * 2f - 1f, v = y / (Tile - 1f) * 2f - 1f; int i = y * Tile + x;
                    float r = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
                    float rr = R * (0.80f + 0.45f * (Fbm(Mathf.Cos(th) * 2.2f + 3f, Mathf.Sin(th) * 2.2f, s) - 0.5f) * 2f);   // the ragged hole
                    float n = Fbm(u * 7f, v * 7f, s + 3);
                    Color c; float al;
                    if (r < rr) { c = HoleTint * (0.9f + 0.6f * Smooth(rr * 0.55f, rr, r)); al = 1f; }               // the hole, a little lighter at its lip
                    else if (r < rr * 1.45f)
                    {   // the chipped ring: rough broken concrete, spalls
                        bool spall = n > 0.56f;
                        c = spall ? ChipTint * 0.55f : ChipTint; al = spall ? 0.85f : 0.55f * (1f - Smooth(rr * 1.15f, rr * 1.45f, r)) + 0.15f;
                    }
                    else { c = SootTint; al = 0.32f * (1f - Smooth(rr * 1.45f, 0.95f, r)) * (0.6f + 0.4f * n); }      // soot round it
                    float ca = Mathf.Max(a[i], halo[i]);
                    if (ca > al) { c = CrackTint; al = ca; }
                    Put(px, ox, oy, x, y, c, al * Border(u, v));
                }
        }
    }
}
