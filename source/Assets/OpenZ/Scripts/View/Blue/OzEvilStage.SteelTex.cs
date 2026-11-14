// OpenZ (GPL-3.0) - version 1.2, the evil HQ: the steel pictures of its military dressing (OzEvilStage), drawn in code once per session.
// Each surface is one function of the place on its tile (metres): its colour, its height (for the bumps), how metallic and how smooth it
// is. From it come three pictures: the colour (sRGB), the bumps (tangent-space normals from the height, linear) and the metal (R metallic,
// A smoothness, linear), the same three maps the robots' own metal material reads. The tiles repeat seamlessly (the noise wraps with them).
//  - DECK: 2.4 m square, four 1.2 m steel plates (the second row offset by half a plate), 8 mm seams with bevels, bolts round every plate,
//    brushed and mottled steel, scuffs along the walking direction, worn bright edges, grime in the seams.
//  - GRATING: a 0.4 m strip across, an angle frame at both sides with bolts, bearing bars along the strip, cross rods, dark slots.
//  - WALL: 1.3 m square (one bay between the wall ribs), two 0.65 m riveted panels, batten straps over their seams, a pressed stiffener
//    across each panel, grime streaks running down.
//  - CONSOLE: its front, 1.8 x 0.9 m in three modules: toggle switches over blank plates; the scope's bezel, two gauges and push buttons;
//    an access door with hinges, a handle, a vent and a key switch; a louvre band along the bottom.
//  - HAZARD: 0.2 m square, worn diagonal black and yellow stripes (colour only).
// Colours are kept dark and cool (the background stays darker and softer than the robots); no text, no logos.
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzEvilStage
    {
        struct Surf { public float r, g, b, h, metal, smooth; }
        delegate Surf SurfFn(float x, float y);
        sealed class SteelMaps { public Texture2D albedo, normal, metal; }

        /// <summary>Value noise on a lattice that repeats with the tile: At(u, v) with u, v in [0, 1) of the tile and px x py cells across it.</summary>
        sealed class TileNoise
        {
            readonly float[] g = new float[256 * 256];
            public TileNoise(int seed) { var r = new System.Random(seed); for (int i = 0; i < g.Length; i++) g[i] = (float)r.NextDouble(); }
            public float At(float u, float v, int px, int py, int oct)
            {
                float fx = u * px, fy = v * py;
                int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                float tx = fx - x0, ty = fy - y0; tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
                int xa = Mod(x0, px), xb = Mod(x0 + 1, px), ya = Mod(y0, py), yb = Mod(y0 + 1, py), o = oct * 37;
                float a = G(xa + o, ya + o), b = G(xb + o, ya + o), c = G(xa + o, yb + o), d = G(xb + o, yb + o);
                return a + (b - a) * tx + (c - a) * ty + (a - b - c + d) * tx * ty;
            }
            public float Fbm(float u, float v, int p, int oct) => 0.5f * At(u, v, p, p, oct) + 0.3f * At(u, v, p * 2, p * 2, oct + 1) + 0.2f * At(u, v, p * 4, p * 4, oct + 2);
            float G(int x, int y) => g[((y & 255) << 8) | (x & 255)];
            static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }
        }

        static float Sat(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
        static float Smooth01(float a, float b, float x) { float t = Sat((x - a) / (b - a)); return t * t * (3f - 2f * t); }
        static byte B(float v) => (byte)(v < 0f ? 0 : v > 255f ? 255 : (int)(v + 0.5f));
        static Surf S(float r, float g, float b, float h, float metal, float smooth) => new Surf { r = r, g = g, b = b, h = h, metal = metal, smooth = smooth };
        static Surf Tint(Surf s, float k) { s.r *= k; s.g *= k; s.b *= k; return s; }
        static Surf Mix(Surf a, float r, float g, float b, float k) { a.r += (r - a.r) * k; a.g += (g - a.g) * k; a.b += (b - a.b) * k; return a; }

        static Texture2D SteelTex(string name, int w, int h, bool linear, Color32[] px, bool wrapU, bool wrapV, int aniso)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true, linear)
            {
                name = name, filterMode = FilterMode.Trilinear, anisoLevel = aniso, hideFlags = HideFlags.DontSave,
                wrapModeU = wrapU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, wrapModeV = wrapV ? TextureWrapMode.Repeat : TextureWrapMode.Clamp
            };
            t.SetPixels32(px); t.Apply(true, true);
            return t;
        }

        /// <summary>
        /// <see cref="UploadMaps"/> / <see cref="UploadColour"/> make the pictures from them on the main thread.</summary>
        sealed class SteelPx { public string name; public int w, h, mw, mh, aniso; public bool wrapU, wrapV; public Color32[] col, nrm, mpx; }

        /// <summary>The three maps of one surface: the colour and the bumps at w x h, the metal at half that when `halfMetal`.</summary>
        static SteelMaps PaintMaps(string name, SurfFn f, float tileW, float tileH, int w, int h, bool halfMetal, bool wrapU, bool wrapV, int aniso)
            => UploadMaps(PaintPx(name, f, tileW, tileH, w, h, halfMetal, wrapU, wrapV, aniso));

        /// <summary>The pictures of one surface's three maps.</summary>
        static SteelMaps UploadMaps(SteelPx p) => new SteelMaps
        {
            albedo = SteelTex(p.name + "_albedo", p.w, p.h, false, p.col, p.wrapU, p.wrapV, p.aniso),
            normal = SteelTex(p.name + "_normal", p.w, p.h, true, p.nrm, p.wrapU, p.wrapV, p.aniso),
            metal = SteelTex(p.name + "_metal", p.mw, p.mh, true, p.mpx, p.wrapU, p.wrapV, p.aniso)
        };

        /// <summary></summary>
        static SteelPx PaintPx(string name, SurfFn f, float tileW, float tileH, int w, int h, bool halfMetal, bool wrapU, bool wrapV, int aniso)
        {
            var col = new Color32[w * h]; var hgt = new float[w * h]; var met = new float[w * h * 2];
            for (int y = 0; y < h; y++)
            {
                float fy = (y + 0.5f) / h * tileH;
                for (int x = 0; x < w; x++)
                {
                    var s = f((x + 0.5f) / w * tileW, fy); int i = y * w + x;
                    col[i] = new Color32(B(s.r), B(s.g), B(s.b), 255); hgt[i] = s.h; met[i * 2] = Sat(s.metal); met[i * 2 + 1] = Sat(s.smooth);
                }
            }
            // the bumps: the height's slope (metres per metre) both ways, wrapped or clamped at the tile's edges
            var nrm = new Color32[w * h]; float dx = tileW / w, dy = tileH / h;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int xl = x - 1, xr = x + 1, yd = y - 1, yu = y + 1;
                    if (xl < 0) xl = wrapU ? w - 1 : 0; if (xr >= w) xr = wrapU ? 0 : w - 1;
                    if (yd < 0) yd = wrapV ? h - 1 : 0; if (yu >= h) yu = wrapV ? 0 : h - 1;
                    float sx = (hgt[y * w + xr] - hgt[y * w + xl]) / ((xr - xl + (xr < xl ? w : 0)) * dx);
                    float sy = (hgt[yu * w + x] - hgt[yd * w + x]) / ((yu - yd + (yu < yd ? h : 0)) * dy);
                    var n = new Vector3(-sx, -sy, 1f).normalized;
                    nrm[y * w + x] = new Color32(B((n.x * 0.5f + 0.5f) * 255f), B((n.y * 0.5f + 0.5f) * 255f), B((n.z * 0.5f + 0.5f) * 255f), 255);
                }
            int mw = halfMetal ? w / 2 : w, mh = halfMetal ? h / 2 : h; var mpx = new Color32[mw * mh];
            for (int y = 0; y < mh; y++)
                for (int x = 0; x < mw; x++)
                {
                    float m = 0f, sm = 0f; int k = halfMetal ? 2 : 1;
                    for (int j = 0; j < k; j++) for (int i = 0; i < k; i++) { int q = (y * k + j) * w + x * k + i; m += met[q * 2]; sm += met[q * 2 + 1]; }
                    m /= k * k; sm /= k * k;
                    mpx[y * mw + x] = new Color32(B(m * 255f), 0, 0, B(sm * 255f));
                }
            return new SteelPx { name = name, w = w, h = h, mw = mw, mh = mh, aniso = aniso, wrapU = wrapU, wrapV = wrapV, col = col, nrm = nrm, mpx = mpx };
        }

        static Texture2D PaintColour(string name, SurfFn f, float tileW, float tileH, int w, int h) => UploadColour(PaintColourPx(name, f, tileW, tileH, w, h));

        /// <summary></summary>
        static SteelPx PaintColourPx(string name, SurfFn f, float tileW, float tileH, int w, int h)
        {
            var col = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) { var s = f((x + 0.5f) / w * tileW, (y + 0.5f) / h * tileH); col[y * w + x] = new Color32(B(s.r), B(s.g), B(s.b), 255); }
            return new SteelPx { name = name, w = w, h = h, mw = w, mh = h, aniso = 4, wrapU = true, wrapV = true, col = col };
        }
        static Texture2D UploadColour(SteelPx p) => SteelTex(p.name, p.w, p.h, false, p.col, true, true, 4);

        /// <summary>The distance from (x, y) to the nearest of the rivets / bolts set `inset` inside each edge of a W x H panel, every `step`
        /// along the edge (the first `step` / 2 from the corner).</summary>
        static float EdgeBolt(float x, float y, float W, float H, float inset, float step)
        {
            float by = Mathf.Clamp(Mathf.Round((y - step * 0.5f) / step) * step + step * 0.5f, step * 0.5f, H - step * 0.5f);
            float bx = Mathf.Clamp(Mathf.Round((x - step * 0.5f) / step) * step + step * 0.5f, step * 0.5f, W - step * 0.5f);
            float d1 = Hyp(x - inset, y - by), d2 = Hyp(x - (W - inset), y - by), d3 = Hyp(x - bx, y - inset), d4 = Hyp(x - bx, y - (H - inset));
            return Mathf.Min(Mathf.Min(d1, d2), Mathf.Min(d3, d4));
        }
        static float Hyp(float a, float b) => Mathf.Sqrt(a * a + b * b);

        /// <summary>A round dome (a bolt, a rivet) of radius r and height hh at distance d, a dark seat ring round it; returns true when drawn.</summary>
        static bool Dome(ref Surf s, float d, float r, float hh, float lit)
        {
            if (d < r) { float k = 1f - d / r; s = Mix(s, lit, lit * 1.04f, lit * 1.1f, 0.55f + 0.3f * k); s.h += hh * Mathf.Sqrt(1f - (d / r) * (d / r)); s.metal = 0.75f; s.smooth = 0.48f; return true; }
            if (d < r + 0.0025f) { s = Tint(s, 0.55f); s.h -= 0.0006f; return true; }
            return false;
        }

        // ------------------------------------------------------------------------------------------------ the deck (2.4 m tile)
        static Surf DeckSurf(TileNoise nz, float x, float y)
        {
            const float P = 1.2f;
            int row = y < P ? 0 : 1;
            float xs = x + (row == 1 ? P * 0.5f : 0f);
            int col = ((int)(xs / P)) % 2;
            float px = xs % P, py = y - row * P;
            int id = row * 2 + col;
            float e = Mathf.Min(Mathf.Min(px, P - px), Mathf.Min(py, P - py));
            float u = x / 2.4f, v = y / 2.4f;
            float tone = id == 0 ? 1.0f : id == 1 ? 0.9f : id == 2 ? 1.1f : 0.95f;
            float mott = nz.Fbm(u, v, 6, 0), brush = nz.At(u, v, 4, 160, 3);
            float L = 58f * tone * (0.84f + 0.3f * mott) * (0.95f + 0.09f * brush);
            var s = S(L * 0.93f, L * 0.98f, L * 1.08f, 0.00015f * brush, 0.45f, 0.34f + 0.08f * brush);
            if ((id == 1 || id == 2) && e > 0.07f)
            {   // tread plate: raised lozenges on a 3 cm grid, turned +-45 deg in a checker (their worn tops a little brighter and smoother)
                const float C = 0.03f;
                float gx = px / C, gy = py / C; int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy);
                float cx = gx - ix - 0.5f, cy = gy - iy - 0.5f, q = 0.7071f;
                float a = ((ix + iy) & 1) == 0 ? (cx + cy) * q : (cx - cy) * q, b = ((ix + iy) & 1) == 0 ? (cy - cx) * q : (cx + cy) * q;
                float k = Mathf.Abs(a) / 0.4f + Mathf.Abs(b) / 0.11f;
                if (k < 1f) { float t = Mathf.Sqrt(1f - k); s.h += 0.0012f * t; s = Mix(s, 88f, 92f, 98f, 0.35f * t); s.smooth += 0.1f * t; s.metal += 0.1f * t; }
            }
            // scuffs along the walking direction (long thin bright streaks, in patches)
            float patch = Smooth01(0.45f, 0.65f, nz.At(u, v, 8, 8, 7));
            if (patch > 0f) { float sc = patch * Smooth01(0.80f, 0.92f, nz.At(u, v, 30, 400, 6)); s = Mix(s, 96f, 100f, 106f, 0.45f * sc); s.smooth += 0.08f * sc; }
            // grime toward the seams, in blotches
            if (e < 0.07f)
            {
                float grime = (1f - e / 0.07f) * (0.55f + 0.45f * nz.At(u, v, 24, 24, 4));
                s = Tint(s, 1f - 0.38f * grime); s.smooth -= 0.16f * grime; s.metal -= 0.10f * grime;
            }
            // the worn bright edge just inside the bevel (its width wanders: at most 4.6 cm)
            if (e > 0.012f && e < 0.058f)
            {
                float wear = Sat(1f - (e - 0.012f) / (0.006f + 0.04f * nz.At(u, v, 40, 40, 5)));
                s = Mix(s, 104f, 108f, 114f, 0.85f * wear); s.metal += 0.25f * wear; s.smooth += 0.14f * wear;
            }
            // bolts round the plate, 4.5 cm in from its edges, every 20 cm
            if (e < 0.06f && Dome(ref s, EdgeBolt(px, py, P, P, 0.045f, 0.2f), 0.011f, 0.0025f, 92f)) return s;
            // the seam and its bevels
            if (e < 0.004f) return S(12f, 13f, 15f, -0.004f, 0.3f, 0.1f);
            if (e < 0.014f) { float k = (e - 0.004f) / 0.01f; s.h -= 0.004f * (1f - k * k * (3f - 2f * k)); s = Tint(s, 0.82f + 0.18f * k); }
            return s;
        }

        // ------------------------------------------------------------------------------------------------ the grating strip (0.4 m along, 0.4 m across)
        static Surf GratingSurf(TileNoise nz, float x, float y)
        {
            float u = x / 0.4f, v = y / 0.4f, n = nz.Fbm(u, v * 0.25f, 4, 10);
            if (y < 0.03f || y > 0.37f)
            {   // the angle frame, bolted every 0.2 m
                float L = 56f * (0.88f + 0.24f * n);
                var s = S(L * 0.94f, L, L * 1.08f, 0.003f, 0.6f, 0.36f);
                float inner = y < 0.03f ? 0.03f - y : y - 0.37f;
                if (inner < 0.004f) { s.h = 0.003f * inner / 0.004f; s = Mix(s, 92f, 96f, 102f, 0.5f); }
                float cy = y < 0.03f ? 0.015f : 0.385f, bx = Mathf.Round((x - 0.1f) / 0.2f) * 0.2f + 0.1f;
                Dome(ref s, Hyp(x - bx, y - cy), 0.007f, 0.002f, 92f);
                return s;
            }
            float fy = (y - 0.03f) % 0.03f, fx = x % 0.1f;
            if (fy < 0.006f)
            {   // a bearing bar: its top worn bright
                float L = 70f * (0.9f + 0.2f * n), c = 1f - Mathf.Abs(fy - 0.003f) / 0.003f;
                return S(L * 0.94f, L, L * 1.07f, 0.0f - 0.0015f * (1f - c), 0.72f, 0.36f + 0.12f * c);
            }
            if (fx < 0.006f) { float L = 58f * (0.9f + 0.2f * n); return S(L * 0.94f, L, L * 1.07f, -0.003f, 0.65f, 0.3f); }   // a cross rod
            return S(18f, 19f, 21f, -0.02f, 0.1f, 0.08f);                                                                      // the slot: the dark pit under it
        }

        // ------------------------------------------------------------------------------------------------ the wall panels (1.3 m tile)
        static Surf WallSurf(TileNoise nz, float x, float y)
        {
            const float W = 0.65f, H = 1.3f;
            int pid = x < W ? 0 : 1; float px = x - pid * W;
            float e = Mathf.Min(Mathf.Min(px, W - px), Mathf.Min(y, H - y));
            float u = x / 1.3f, v = y / 1.3f;
            float L = 47f * (pid == 0 ? 0.97f : 1.03f) * (0.88f + 0.22f * nz.Fbm(u, v, 5, 12));
            L *= 0.92f + 0.1f * nz.At(u, v, 64, 3, 15);                                                  // streaks running down
            L *= 0.86f + 0.14f * Smooth01(0f, 0.45f, y);                                                  // grime gathering at the panel's foot
            var s = S(L * 0.95f, L, L * 1.08f, 0f, 0.55f, 0.32f);
            float dst = Mathf.Abs(y - 0.65f);                                                             // the pressed stiffener across the panel
            if (dst < 0.025f && e > 0.03f) { float k = 1f - dst / 0.025f; s.h += 0.002f * Mathf.Sqrt(k); s = Tint(s, 1f + 0.06f * k); }
            if (e < 0.03f)
            {   // the batten strap over the seam, riveted along both sides
                s = Tint(s, 0.9f); s.h = 0.004f; s.metal = 0.6f; s.smooth = 0.36f;
                if (e > 0.027f) { s.h = 0.004f * (0.03f - e) / 0.003f; s = Tint(s, 0.75f); }
                if (e < 0.0015f) { s = Tint(s, 0.6f); s.h = 0.0035f; }
                Dome(ref s, EdgeBolt(px, y, W, H, 0.015f, 0.075f), 0.007f, 0.002f, 86f);
            }
            return s;
        }

        // ------------------------------------------------------------------------------------------------ the console's front (1.8 x 0.9 m)
        static readonly float[] GaugeX = { 0.72f, 1.08f }, ButtonX = { 0.72f, 0.84f, 0.96f, 1.08f };
        static Surf ConsoleSurf(TileNoise nz, float x, float y)
        {
            const float W = 1.8f, H = 0.9f;
            float u = x / W, v = y / H;
            float L = 52f * (0.88f + 0.22f * nz.Fbm(u, v, 6, 20));
            var s = S(L * 0.96f, L, L * 1.04f, 0f, 0.5f, 0.34f);
            float eo = Mathf.Min(Mathf.Min(x, W - x), Mathf.Min(y, H - y));
            // wear near the module edges (handled metal)
            float wear = Sat(1f - Mathf.Min(eo, Mathf.Min(Mathf.Abs(x - 0.6f), Mathf.Abs(x - 1.2f))) / (0.01f + 0.04f * nz.At(u, v, 36, 18, 22)));
            s = Mix(s, 96f, 99f, 104f, 0.45f * wear);
            if (eo < 0.025f)
            {   // the riveted outer frame
                s.h = 0.003f; s = Tint(s, 0.86f);
                if (eo > 0.022f) { s.h = 0.003f * (0.025f - eo) / 0.003f; }
                Dome(ref s, EdgeBolt(x, y, W, H, 0.0125f, 0.08f), 0.006f, 0.0018f, 84f);
                return s;
            }
            if (Mathf.Abs(x - 0.6f) < 0.003f || Mathf.Abs(x - 1.2f) < 0.003f) return S(14f, 15f, 16f, -0.002f, 0.3f, 0.12f);   // the module seams
            if (y > 0.04f && y < 0.15f)
            {   // the louvre band
                float ly = (y - 0.04f) % 0.022f;
                if (ly < 0.009f) return S(12f, 13f, 14f, -0.003f * (1f - ly / 0.009f), 0.3f, 0.1f);
                return Tint(s, 0.94f);
            }
            if (x < 0.6f)
            {   // toggle switches (two rows of five) over blank plates; a blank plate along the top
                if (x > 0.08f && x < 0.52f && y > 0.79f && y < 0.84f) { s = S(82f, 82f, 77f, 0.0008f, 0.2f, 0.25f); return s; }
                for (int row = 0; row < 2; row++)
                    for (int k = 0; k < 5; k++)
                    {
                        float cx = 0.1f + 0.1f * k, cy = row == 0 ? 0.66f : 0.44f, dx = x - cx, dy = y - cy;
                        if (Mathf.Abs(dx) < 0.004f && dy > 0f && dy < 0.03f) return S(112f, 112f, 106f, 0.008f, 0.7f, 0.5f);   // the lever
                        float d = Hyp(dx, dy);
                        if (d < 0.011f) return S(60f, 62f, 64f, 0.004f * Mathf.Sqrt(1f - d / 0.011f), 0.75f, 0.45f);          // the boss
                        if (Mathf.Abs(dx) < 0.025f && Mathf.Abs(dy) < 0.03f) return S(30f, 32f, 34f, 0.0015f, 0.4f, 0.3f);    // the base plate
                        if (Mathf.Abs(dx) < 0.03f && dy < -0.045f && dy > -0.065f) return S(78f, 80f, 76f, 0.0008f, 0.2f, 0.25f);   // the blank label
                        float halo = Sat(1f - d / 0.04f); if (halo > 0f) s = Mix(s, 90f, 92f, 96f, 0.25f * halo);            // finger wear
                    }
                return s;
            }
            if (x < 1.2f)
            {   // the scope's bezel (the dim screen itself is geometry over it), two gauges, a row of push buttons
                float d = Hyp(x - 0.9f, y - 0.56f);
                if (d < 0.105f) return S(8f, 12f, 10f, -0.002f, 0.2f, 0.6f);
                if (d < 0.135f) { float k = (d - 0.105f) / 0.03f; var r = S(26f, 28f, 30f, 0.004f * Mathf.Sin(k * Mathf.PI), 0.7f, 0.42f); for (int q = 0; q < 4; q++) { float a = (45f + 90f * q) * Mathf.Deg2Rad; if (Hyp(x - 0.9f - Mathf.Cos(a) * 0.12f, y - 0.56f - Mathf.Sin(a) * 0.12f) < 0.006f) return S(70f, 72f, 76f, 0.005f, 0.8f, 0.5f); } return r; }
                foreach (var gx in GaugeX)
                {
                    float dg = Hyp(x - gx, y - 0.26f);
                    if (dg < 0.04f)
                    {   // a gauge face (dim cream), its needle and ticks
                        float ang = gx < 0.9f ? 40f : -20f, ca = Mathf.Cos(ang * Mathf.Deg2Rad), sa = Mathf.Sin(ang * Mathf.Deg2Rad);
                        float along = (x - gx) * sa + (y - 0.26f) * ca, across = (x - gx) * ca - (y - 0.26f) * sa;
                        if (along > -0.005f && along < 0.034f && Mathf.Abs(across) < 0.0018f) return S(22f, 20f, 18f, 0.0008f, 0.3f, 0.6f);
                        if (dg > 0.032f && Mathf.Repeat(Mathf.Atan2(y - 0.26f, x - gx) * Mathf.Rad2Deg, 18f) < 2.2f && y > 0.245f) return S(36f, 34f, 30f, 0f, 0.2f, 0.6f);
                        return S(86f, 84f, 74f, -0.001f, 0.1f, 0.62f);
                    }
                    if (dg < 0.052f) return S(28f, 30f, 32f, 0.003f, 0.7f, 0.45f);
                }
                foreach (var bx in ButtonX)
                {
                    float dbt = Hyp(x - bx, y - 0.82f);
                    if (dbt < 0.010f) return S(68f, 66f, 60f, 0.003f, 0.3f, 0.4f);
                    if (dbt < 0.016f) return S(24f, 25f, 27f, 0.0015f, 0.5f, 0.35f);
                }
                return s;
            }
            {   // the access door: its groove, two hinges, a handle, a vent, a key switch
                bool inDoor = x > 1.26f && x < 1.74f && y > 0.20f && y < 0.86f;
                float ed = Mathf.Min(Mathf.Min(Mathf.Abs(x - 1.26f), Mathf.Abs(x - 1.74f)), Mathf.Min(Mathf.Abs(y - 0.20f), Mathf.Abs(y - 0.86f)));
                if (ed < 0.003f && x > 1.257f && x < 1.743f && y > 0.197f && y < 0.863f) return S(14f, 15f, 16f, -0.002f, 0.3f, 0.12f);
                if (Mathf.Abs(x - 1.265f) < 0.008f && (Mathf.Abs(y - 0.30f) < 0.03f || Mathf.Abs(y - 0.76f) < 0.03f)) return S(40f, 42f, 45f, 0.004f, 0.7f, 0.4f);
                if (Mathf.Abs(x - 1.70f) < 0.007f && Mathf.Abs(y - 0.53f) < 0.05f) return S(76f, 78f, 82f, 0.006f, 0.8f, 0.5f);
                if (x > 1.34f && x < 1.66f && y > 0.62f && y < 0.80f) { float sx = (x - 1.34f) % 0.02f; if (sx < 0.008f) return S(12f, 13f, 14f, -0.003f, 0.3f, 0.1f); }
                float dk = Hyp(x - 1.50f, y - 0.38f);
                if (dk < 0.03f) { if (Mathf.Abs(x - 1.50f) < 0.005f && Mathf.Abs(y - 0.38f) < 0.022f) return S(70f, 70f, 66f, 0.005f, 0.7f, 0.5f); return S(30f, 31f, 33f, 0.003f, 0.6f, 0.4f); }
                if (inDoor) s.h += 0.0008f;
                return s;
            }
        }

        // ------------------------------------------------------------------------------------------------ the hazard stripes (0.2 m tile)
        static Surf HazardSurf(TileNoise nz, float x, float y)
        {
            float u = x / 0.2f, v = y / 0.2f, k = Mathf.Repeat((x + y) / 0.1f, 1f);
            var s = k < 0.5f ? S(148f, 116f, 32f, 0f, 0.1f, 0.3f) : S(24f, 24f, 24f, 0f, 0.1f, 0.3f);
            float edge = Mathf.Min(Mathf.Abs(k - 0.5f), Mathf.Min(k, 1f - k));                           // the stripes' painted edges a little darker
            if (edge < 0.03f) s = Tint(s, 0.85f);
            float worn = Smooth01(0.62f, 0.78f, nz.Fbm(u, v, 8, 30));                                     // paint worn down to steel, in small specks
            s = Mix(s, 66f, 68f, 70f, 0.6f * worn);
            return Tint(s, 0.85f + 0.2f * nz.At(u, v, 8, 8, 33));
        }
    }
}
