using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (View/OzUnitEditor.cs; spec E047): the unit pictures as the editor's dock draws them, for every module (P3's list rows,
    /// portraits, card and table, P4's drag ghost): the TEAM picture of a unit (robots and heroes their portrait, vehicles, guns and the
    /// shuttle their crewed portrait); NEUTRAL = the blue picture in grey (luminance x 1.06, the prototype's grayscale(1) brightness(1.06));
    /// the FLAG tile's picture (the prototype's flag on a pole over a dashed ring) and the OBSTACLE's wire cube (the prototype's picture:
    /// faint #BFE3FF faces, #D6EEF6 edges and grid lines), both drawn in code on their dark well, since 1.3 only the stand-ins when a tile picture
    /// controls are drawn with (gradients, dashes, discs, glows), made once in code.
    /// </summary>
    public static class OzUeArt
    {
        /// <summary>The obstacle picture's edges and grid lines (prototype OBST_SVG strokes); its faces are #BFE3FF.</summary>
        public static readonly Color CubeTint = new Color32(0xD6, 0xEE, 0xF6, 255);
        /// <summary>The well behind the flag and obstacle pictures (prototype #15181A).</summary>
        public static readonly Color ExtraWell = new Color32(0x15, 0x18, 0x1A, 255);

        static readonly Dictionary<Texture, Texture2D> greys = new Dictionary<Texture, Texture2D>();
        static readonly Dictionary<string, Texture2D> made = new Dictionary<string, Texture2D>();

        /// <summary>The picture of a palette id for a team (null when the game has none).</summary>
        public static Texture Picture(string id, Team team)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (id == OzUePlayground.Flag) { var t = Tile(FlagTilePath); return t ? t : FlagPicture(); }
            if (id == OzUePlayground.Obstacle) { var t = Tile(ObstacleTilePath); return t ? t : ObstaclePicture(); }
            if (id == OzUePlayground.Grenades) return GrenadesPicture();
            if (OzUePlayground.IsPlatformId(id)) { var t = Tile(id == OzUePlayground.PlatformHigh ? PlatformHighTilePath : PlatformMidTilePath); return t ? t : OzUePlatformArt.Picture(id); }   // 1.3: the code-drawn one in View/OzUePlatformView.cs if it is missing
            if (id == OzUePlayground.Nuke) return NukePicture();                                                 // 1.3 item 2
            if (team == Team.Neutral) return OzNeutralArt.ForUnit(id) ?? Grey(TeamPicture(id, Team.Blue));   // 1.2 item 167c: the empty vehicle's / gun's / shuttle's own neutral picture (View/OzNeutralArt.cs); the grey filter only where none
            return TeamPicture(id, team);
        }

        /// <summary></summary>
        public const string FlagTilePath = "OpenZ/UnitEditor/ue_flag_tile", ObstacleTilePath = "OpenZ/UnitEditor/ue_obstacle_tile",
                            PlatformMidTilePath = "OpenZ/UnitEditor/ue_platform_mid_tile", PlatformHighTilePath = "OpenZ/UnitEditor/ue_platform_high_tile";
        static readonly Dictionary<string, Texture2D> tiles = new Dictionary<string, Texture2D>();
        static Texture2D Tile(string path)
        {
            if (tiles.TryGetValue(path, out var t) && t) return t;
            bool first = !tiles.ContainsKey(path);
            t = Resources.Load<Texture2D>(path); tiles[path] = t;
            if (!t && first) Debug.LogWarning("[OpenZ UnitEditor] the tile picture " + path + " is missing, the code-drawn one stands in");
            return t;
        }

        /// <summary>The GRENADES tile's picture (256 x 256, the import of the production-card icons), since 1.3 a 3D portrait of the game's grenade crate.</summary>
        public const string GrenadesTilePath = "OpenZ/UnitEditor/ue_grenades_tile";
        static Texture2D grenadesTile; static bool grenadesWarned;
        static Texture GrenadesPicture()
        {
            if (!grenadesTile) grenadesTile = Resources.Load<Texture2D>(GrenadesTilePath);
            if (!grenadesTile && !grenadesWarned) { grenadesWarned = true; Debug.LogWarning("[OpenZ UnitEditor] the GRENADES tile picture " + GrenadesTilePath + " is missing"); }
            return grenadesTile;
        }

        /// <summary></summary>
        public const string NukeTilePath = "OpenZ/UnitEditor/ue_nuke_tile";
        static Texture2D nukeTile; static bool nukeWarned;
        static Texture NukePicture()
        {
            if (!nukeTile) nukeTile = Resources.Load<Texture2D>(NukeTilePath);
            if (!nukeTile && !nukeWarned) { nukeWarned = true; Debug.LogWarning("[OpenZ UnitEditor] the NUKE tile picture " + NukeTilePath + " is missing"); }
            return nukeTile;
        }

        static Texture TeamPicture(string id, Team team)
        {
            if (id == OzUePlayground.Heroes)
            {
                var hero = OzHeroSquadView.MemberPortrait(ZHeroSquads.Get(ZHeroSquads.AlanBrad), -1, team);
                return hero ? hero : UnitArt.Portrait("grunt", team);
            }
            if (ZBuildLists.IsRobot(id)) return UnitArt.Portrait(id, team);
            return (Texture)UnitArt.VehiclePortrait(id, team) ?? UnitArt.Icon(id, team);
        }

        /// <summary>Sets the picture on any RawImage (tiles, rows, portraits, the drag ghost), drawn as it is (the image's alpha kept).</summary>
        public static void Apply(RawImage img, string id, Team team)
        {
            if (!img) return;
            var t = Picture(id, team);
            img.texture = t;
            img.enabled = t != null;
            img.color = new Color(1f, 1f, 1f, img.color.a);
        }

        /// <summary>The edge of a tile's picture well (spec E047): blue rgba(77,115,217,.65), red rgba(217,69,47,.55), neutral KHAKI 70 %,
        /// by the colour the team is DRAWN in (YOUR TEAM COLOUR, D16).</summary>
        public static Color WellEdge(Team team)
        {
            var s = TeamColors.Shown(team);
            return s == Team.Blue ? new Color32(77, 115, 217, 166) : s == Team.Red ? new Color32(217, 69, 47, 140) : OzUi.A(OzUi.Khaki, 0.7f);
        }

        // ------------------------------------------------------------------------------------------------ the neutral grey look
        /// <summary>A grey copy of a picture (luminance of the sRGB values x 1.06, the prototype's filter), made once per picture: the
        /// picture is copied through a render texture (the game's pictures are not readable) and read back.</summary>
        public static Texture2D Grey(Texture src)
        {
            if (!src) return null;
            if (greys.TryGetValue(src, out var g) && g) return g;
            RenderTexture rt = null, was = RenderTexture.active;
            try
            {
                int w = Mathf.Clamp(src.width, 8, 512), h = Mathf.Clamp(src.height, 8, 512);
                rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                g = new Texture2D(w, h, TextureFormat.RGBA32, true, false) { name = src.name + "_ue_grey", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
                g.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                var px = g.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    float l = (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) * 1.06f;
                    byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(l), 0, 255);
                    px[i] = new Color32(v, v, v, c.a);
                }
                g.SetPixels32(px); g.Apply(true, true);
            }
            catch (Exception e) { Debug.LogWarning("[OpenZ UnitEditor] grey picture of " + src.name + ": " + e.Message); g = null; }
            finally { RenderTexture.active = was; if (rt) RenderTexture.ReleaseTemporary(rt); }
            greys[src] = g;
            return g;
        }

        // ------------------------------------------------------------------------------------------------ small textures made in code
        /// <summary>A vertical gradient (top colour at the top), sRGB stops like a CSS linear-gradient; mid at midAt (0..1) when given.</summary>
        public static Texture2D Gradient(Color top, Color bottom, Color? mid = null, float midAt = 0.5f)
        {
            string key = "grad" + (Color32)top + (Color32)bottom + (mid.HasValue ? (Color32)mid.Value + "@" + midAt : "");
            if (made.TryGetValue(key, out var t) && t) return t;
            const int H = 64;
            t = new Texture2D(2, H, TextureFormat.RGBA32, false, false) { name = "ue_" + key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[2 * H];
            for (int y = 0; y < H; y++)
            {
                float v = 1f - (y + 0.5f) / H;                                                  // texture row 0 is the bottom
                Color c = !mid.HasValue ? Color.Lerp(top, bottom, v)
                        : v < midAt ? Color.Lerp(top, mid.Value, v / Mathf.Max(midAt, 1e-3f)) : Color.Lerp(mid.Value, bottom, (v - midAt) / Mathf.Max(1f - midAt, 1e-3f));
                px[y * 2] = px[y * 2 + 1] = c;
            }
            t.SetPixels(px); t.Apply(false, true);
            made[key] = t;
            return t;
        }

        /// <summary>A dash pattern for 1 u dashed edges: on / off texels, repeat, point filtered (white).</summary>
        public static Texture2D Dash(int on = 3, int off = 3)
        {
            string key = "dash" + on + "_" + off;
            if (made.TryGetValue(key, out var t) && t) return t;
            t = new Texture2D(on + off, 1, TextureFormat.RGBA32, false, false) { name = "ue_" + key, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var px = new Color32[on + off];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)(i < on ? 255 : 0));
            t.SetPixels32(px); t.Apply(false, true);
            made[key] = t;
            return t;
        }

        /// <summary>The same dash pattern for vertical edges (along v).</summary>
        public static Texture2D DashV(int on = 3, int off = 3)
        {
            string key = "dashv" + on + "_" + off;
            if (made.TryGetValue(key, out var t) && t) return t;
            t = new Texture2D(1, on + off, TextureFormat.RGBA32, false, false) { name = "ue_" + key, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var px = new Color32[on + off];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)(i < on ? 255 : 0));
            t.SetPixels32(px); t.Apply(false, true);
            made[key] = t;
            return t;
        }

        /// <summary>White shapes for the round "i" badge, the OpenZ-only corner and the checkbox tick (64 px, anti-aliased).</summary>
        public static Texture2D Disc() => Shape("disc", 64, (x, y) => 31.5f - Mathf.Sqrt((x - 32f) * (x - 32f) + (y - 32f) * (y - 32f)));
        /// <summary>A ring of 1/16 of its size (1 u on a 16 u badge).</summary>
        public static Texture2D Ring() => Shape("ring", 64, (x, y) => { float r = Mathf.Sqrt((x - 32f) * (x - 32f) + (y - 32f) * (y - 32f)); return 2f - Mathf.Abs(r - 29.5f); });
        /// <summary>The top-right right-angled triangle of a box (CSS border-top + transparent border-left).</summary>
        public static Texture2D Corner() => Shape("corner", 64, (x, y) => (x - y) * 0.70710678f);
        /// <summary>A check mark (two strokes).</summary>
        public static Texture2D Tick() => Shape("tick", 64, (x, y) => 4.2f - Mathf.Min(SegDist(x, y, 14f, 33f, 26f, 45f), SegDist(x, y, 26f, 45f, 50f, 19f)));

        static float SegDist(float x, float y, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay, t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
            float ex = x - (ax + dx * t), ey = y - (ay + dy * t);
            return Mathf.Sqrt(ex * ex + ey * ey);
        }

        /// <summary>A white shape from a signed distance in texels (positive inside), 4 x 4 samples per texel; (0, 0) = top left.</summary>
        static Texture2D Shape(string key, int n, Func<float, float, float> inside)
        {
            if (made.TryGetValue(key, out var t) && t) return t;
            t = new Texture2D(n, n, TextureFormat.RGBA32, true, false) { name = "ue_" + key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float a = 0f;
                    for (int sy = 0; sy < 4; sy++) for (int sx = 0; sx < 4; sx++) a += inside(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f) > 0f ? 1f : 0f;
                    px[(n - 1 - y) * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a / 16f * 255f));
                }
            t.SetPixels32(px); t.Apply(true, true);
            made[key] = t;
            return t;
        }

        /// <summary>An outer CSS box-shadow glow ("0 0 blur colour") round a w x h box, as a white alpha picture of (w + 2 blur) x (h + 2 blur)
        /// units at 2 texels per unit; nothing under the box itself (CSS draws an outer shadow only outside the box).</summary>
        public static Texture2D Glow(float wU, float hU, float blurU)
        {
            string key = $"glow{wU:0.#}x{hU:0.#}b{blurU:0.#}";
            if (made.TryGetValue(key, out var t) && t) return t;
            int W = Mathf.CeilToInt((wU + 2f * blurU) * 2f), H = Mathf.CeilToInt((hU + 2f * blurU) * 2f);
            t = new Texture2D(Mathf.Max(W, 2), Mathf.Max(H, 2), TextureFormat.RGBA32, false, false) { name = "ue_" + key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float sig = Mathf.Max(0.5f, blurU), hw = wU, hh = hU;
            var px = new Color32[t.width * t.height];
            for (int y = 0; y < t.height; y++)
                for (int x = 0; x < t.width; x++)
                {
                    float ux = (x + 0.5f) / 2f - blurU, uy = (y + 0.5f) / 2f - blurU;                   // units from the box's corner
                    bool under = ux >= 0f && ux <= hw && uy >= 0f && uy <= hh;
                    float a = under ? 0f : Box1(ux, hw, sig) * Box1(uy, hh, sig);
                    px[y * t.width + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
                }
            t.SetPixels32(px); t.Apply(false, true);
            made[key] = t;
            return t;
        }

        // a box [0, len] blurred by a gaussian (sigma = the CSS blur radius / 2), at x
        static float Box1(float x, float len, float blur)
        {
            float s = blur * 0.5f * 1.41421356f;
            return 0.5f * (Erf((x) / s) - Erf((x - len) / s));
        }

        static float Erf(float x)
        {   // Abramowitz and Stegun 7.1.26
            float sgn = x < 0f ? -1f : 1f; x = Mathf.Abs(x);
            float t = 1f / (1f + 0.3275911f * x);
            float y = 1f - (((((1.061405429f * t - 1.453152027f) * t) + 1.421413741f) * t - 0.284496736f) * t + 0.254829592f) * t * Mathf.Exp(-x * x);
            return sgn * y;
        }

        // ------------------------------------------------------------------------------------------------ the FLAG tile's picture
        /// <summary>The prototype's FLAG_SVG on its 64 grid (dark well, ground shadow, dashed ring, pole, ball, a waving cloth with a dark
        /// edge), drawn once at 128 px.</summary>
        public static Texture2D FlagPicture()
        {
            if (made.TryGetValue("flag", out var t) && t) return t;
            const int N = 128, S = 4; const float k = 64f / N;
            var well = (Color)ExtraWell; var ringC = (Color)new Color32(0xCF, 0xCD, 0xBF, 255); var poleC = (Color)new Color32(0xC9, 0xC6, 0xB4, 255);
            var ballC = (Color)new Color32(0xEC, 0xE9, 0xD6, 255); var clothC = (Color)new Color32(0xE2, 0xDF, 0xCB, 255); var edgeC = (Color)new Color32(0x0B, 0x0D, 0x0C, 255);
            var cloth = ClothOutline();
            t = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { name = "ue_flag", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    Color acc = Color.clear;
                    for (int sy = 0; sy < S; sy++)
                        for (int sx = 0; sx < S; sx++)
                        {
                            float u = (x + (sx + 0.5f) / S) * k, v = (y + (sy + 0.5f) / S) * k;            // 64-grid point, y down
                            Color c = well;
                            float e = ((u - 22f) / 12f) * ((u - 22f) / 12f) + ((v - 56f) / 3.5f) * ((v - 56f) / 3.5f);
                            if (e <= 1f) c = Color.Lerp(c, Color.black, 0.45f);                              // the shadow under the pole
                            if (OnRingDash(u - 22f, v - 56f)) c = ringC;                                     // the dashed ring (stroke 1, dashes 3 / 2)
                            if (u >= 20.5f && u <= 23.5f && v >= 8f && v <= 56f) c = poleC;
                            if ((u - 22f) * (u - 22f) + (v - 8f) * (v - 8f) <= 2.6f * 2.6f) c = ballC;
                            float dc = Inside(cloth, u, v);
                            if (dc > -0.6f) c = dc > 0.6f ? clothC : edgeC;                                  // the cloth last, with its 1.2 dark edge
                            acc += c;
                        }
                    px[(N - 1 - y) * N + x] = acc / (S * S);
                }
            t.SetPixels(px); t.Apply(true, true);
            made["flag"] = t;
            return t;
        }

        // ------------------------------------------------------------------------------------------------ the OBSTACLE tile's picture
        // OBST_SVG on its 64 grid, in its paint order: the shadow, then each face's fill and its 1.4 edge, then the 0.8 grid lines
        static readonly List<Vector2> ObShadow = new List<Vector2> { new Vector2(12f, 46f), new Vector2(36f, 54f), new Vector2(54f, 44f), new Vector2(30f, 36f) };
        static readonly List<Vector2>[] ObFaces =
        {
            new List<Vector2> { new Vector2(14f, 24f), new Vector2(36f, 31f), new Vector2(36f, 52f), new Vector2(14f, 45f) },   // front, fill 10 %
            new List<Vector2> { new Vector2(36f, 31f), new Vector2(52f, 23f), new Vector2(52f, 44f), new Vector2(36f, 52f) },   // side, 7 %
            new List<Vector2> { new Vector2(14f, 24f), new Vector2(30f, 16f), new Vector2(52f, 23f), new Vector2(36f, 31f) },   // top, 16 %
        };
        static readonly float[] ObFaceFill = { 0.10f, 0.07f, 0.16f };
        static readonly Vector4[] ObGrid =
        {
            new Vector4(21f, 26.3f, 21f, 47.3f), new Vector4(28.5f, 28.7f, 28.5f, 49.7f), new Vector4(14f, 31.5f, 36f, 38.5f), new Vector4(14f, 38.5f, 36f, 45.5f),
            new Vector4(41.5f, 28.3f, 41.5f, 49.3f), new Vector4(46.5f, 25.8f, 46.5f, 46.8f), new Vector4(36f, 38f, 52f, 30f), new Vector4(36f, 45f, 52f, 37f),
            new Vector4(21f, 20.5f, 43f, 27.5f), new Vector4(25.5f, 18.3f, 47.5f, 25.3f), new Vector4(19f, 22.3f, 41f, 29.3f),
        };

        /// <summary>The prototype's OBST_SVG on its 64 grid (dark well, a ground shadow, a wire cube with faint #BFE3FF faces, #D6EEF6 edges
        /// and grid lines), drawn once at 128 px.</summary>
        public static Texture2D ObstaclePicture()
        {
            if (made.TryGetValue("obstacle", out var t) && t) return t;
            const int N = 128, S = 4; const float k = 64f / N;
            var well = (Color)ExtraWell; var faceC = (Color)new Color32(0xBF, 0xE3, 0xFF, 255); var lineC = CubeTint;
            t = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { name = "ue_obstacle", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    Color acc = Color.clear;
                    for (int sy = 0; sy < S; sy++)
                        for (int sx = 0; sx < S; sx++)
                        {
                            float u = (x + (sx + 0.5f) / S) * k, v = (y + (sy + 0.5f) / S) * k;            // 64-grid point, y down
                            Color c = well;
                            if (Inside(ObShadow, u, v) > 0f) c = Color.Lerp(c, Color.black, 0.35f);
                            for (int f = 0; f < ObFaces.Length; f++)
                            {
                                float d = Inside(ObFaces[f], u, v);
                                if (d > 0f) c = Color.Lerp(c, faceC, ObFaceFill[f]);
                                if (Mathf.Abs(d) <= 0.7f) c = lineC;                                       // the 1.4 edge, on the outline
                            }
                            bool onGrid = false;                                                               // one path: its stroke is painted once where lines cross
                            foreach (var g in ObGrid) if (ButtDist(u, v, g.x, g.y, g.z, g.w) <= 0.4f) { onGrid = true; break; }
                            if (onGrid) c = Color.Lerp(c, lineC, 0.32f);
                            acc += c;
                        }
                    px[(N - 1 - y) * N + x] = acc / (S * S);
                }
            t.SetPixels(px); t.Apply(true, true);
            made["obstacle"] = t;
            return t;
        }

        // distance to a segment drawn with butt caps (none past its ends)
        static float ButtDist(float x, float y, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy, s = ((x - ax) * dx + (y - ay) * dy) / l2;
            if (s < 0f || s > 1f) return 1e9f;
            float ex = x - (ax + dx * s), ey = y - (ay + dy * s);
            return Mathf.Sqrt(ex * ex + ey * ey);
        }

        // the ring: <ellipse rx 15 ry 4.5 stroke-width 1 stroke-dasharray "3 2">. A point (dx, dy from the centre) is on a dash when it lies
        // within 0.5 of the ellipse's line (its distance: the normalised radius' excess over its gradient) and the length along the line
        // from the ellipse's start (its right end, running down first, as SVG draws an ellipse) is in the first 3 of every 5.
        const float RingA = 15f, RingB = 4.5f; const int RingSteps = 512;
        static float[] ringArc;

        static bool OnRingDash(float dx, float dy)
        {
            float er = Mathf.Sqrt((dx / RingA) * (dx / RingA) + (dy / RingB) * (dy / RingB));
            if (er < 1e-4f) return false;
            float gx = dx / (RingA * RingA * er), gy = dy / (RingB * RingB * er);
            float dist = (er - 1f) / Mathf.Max(1e-4f, Mathf.Sqrt(gx * gx + gy * gy));
            if (Mathf.Abs(dist) > 0.5f) return false;
            float th = Mathf.Atan2(dy / RingB, dx / RingA); if (th < 0f) th += 2f * Mathf.PI;
            return Mathf.Repeat(RingArc(th), 5f) < 3f;
        }

        // the length along the ring from its start to the parametric angle th (0 .. 2 pi), from a table made once
        static float RingArc(float th)
        {
            if (ringArc == null)
            {
                ringArc = new float[RingSteps + 1];
                float step = 2f * Mathf.PI / RingSteps, prev = RingSpeed(0f);
                for (int i = 1; i <= RingSteps; i++) { float cur = RingSpeed(i * step); ringArc[i] = ringArc[i - 1] + 0.5f * (prev + cur) * step; prev = cur; }
            }
            float f = Mathf.Clamp(th / (2f * Mathf.PI) * RingSteps, 0f, RingSteps); int k = Mathf.Min(RingSteps - 1, Mathf.FloorToInt(f));
            return Mathf.Lerp(ringArc[k], ringArc[k + 1], f - k);
        }
        static float RingSpeed(float t) { float s = Mathf.Sin(t), c = Mathf.Cos(t); return Mathf.Sqrt(RingA * RingA * s * s + RingB * RingB * c * c); }

        // the cloth: M23.5 10 C32 7 38 14 50 11 L50 29 C38 32 32 25 23.5 28 Z, flattened
        static List<Vector2> ClothOutline()
        {
            var p = new List<Vector2>();
            Bez(p, new Vector2(23.5f, 10f), new Vector2(32f, 7f), new Vector2(38f, 14f), new Vector2(50f, 11f));
            Bez(p, new Vector2(50f, 29f), new Vector2(38f, 32f), new Vector2(32f, 25f), new Vector2(23.5f, 28f));
            return p;
        }
        static void Bez(List<Vector2> p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f, s = 1f - t;
                p.Add(s * s * s * a + 3f * s * s * t * b + 3f * s * t * t * c + t * t * t * d);
            }
        }
        // signed distance to a closed polygon (positive inside)
        static float Inside(List<Vector2> poly, float x, float y)
        {
            bool inside = false; float best = 1e9f;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vector2 a = poly[j], b = poly[i];
                if ((b.y > y) != (a.y > y) && x < (a.x - b.x) * (y - b.y) / (a.y - b.y) + b.x) inside = !inside;
                best = Mathf.Min(best, SegDist(x, y, a.x, a.y, b.x, b.y));
            }
            return inside ? best : -best;
        }
    }
}
