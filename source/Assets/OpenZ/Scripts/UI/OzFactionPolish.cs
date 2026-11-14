using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// k75a4 (1.2 owner items 102, 104, 108, 112, 113, 115): the faction screen's polish. Pure numbers and rules (counted headless by
    /// Editor/OzV12FactionPolishCheck.cs); UI/OpenZMainMenu.FactionPolish.cs puts them on the screen, the shader OzPosterFigure draws them.
    ///    back (OzFactionDeck.SoundIn / SoundOut) and a quiet ui_dossier_row as each of its rows appears: one tick per row, never two in
    ///    the same 45 ms (the menu's own guard plays one sound id at most every 40 ms).
    ///  * EvilZod a tiny bit bigger (item 108): his poster's render is drawn <see cref="EvilScale"/> times its size about a point on the
    ///    frame band's top edge three quarters across, so the band still covers what it covered, his head grows upward and the figure
    ///    grows toward the poster's free left side (his cape is already cut by the right edge, his fist never reaches the left one).
    ///  * The cape's torn ends (item 113): just above EvilZod's frame band his cape ends in rags that showed the print between them as two
    ///    spikes with the paper rim round them. In a strip above the band (<see cref="Fill"/>) the render is completed downward with its own
    ///    row at the strip's top (laid under what is there), so the cape reaches the band like the legs do: no gaps, no rim there.
    ///  * Smooth edges (item 115): the renders are supersampled (OzFactionPosters.Supersample, their mipmaps make the downsample).
    ///  * A light glint across Zod's sunglasses (item 115): a soft slanted band runs over both lenses, left to right, every
    ///    <see cref="GlintPeriod"/> s, only on the dark glass inside each lens.
    ///  * EvilZod's eyes glow (item 115): their own light brightens and a soft red halo grows round them on a slow pulse (never off).
    ///  * The poster's shadow on its card (item 112): the posters' hard drop shadow and focus glow lie under the cards; the poster casts
    ///    only a narrow, light shade (<see cref="ShadeW"/> u, <see cref="ShadeAlpha"/>) on its card while the card is out.
    /// The lenses and the eyes are measured on the light models' own renders at the measured poster cameras (their middles and half-axes
    /// in the model's root, metres); the game projects them with the poster's camera every frame, so they follow the sway.
    /// </summary>
    public static class OzFactionPolish
    {
        // ---------------------------------------------------------------- sounds (item 104)
        public const string SoundRow = "ui_dossier_row";
        /// <summary>The least time between two row ticks (s); the rows come RowStagger (55 ms) apart.</summary>
        public const float RowTickGap = 0.045f;
        /// <summary>The rows already in at t s after the card started (a card turned again on its way back: they tick no more).</summary>
        public static int RowsIn(float t)
        {
            int n = 0;
            for (int r = 0; r < OzFactionDeck.Rows; r++) if (t > OzFactionDeck.RowAt + r * OzFactionDeck.RowStagger) n++;
            return n;
        }
        /// <summary>Row r has started to appear at t s.</summary>
        public static bool RowDue(int r, float t) => r < OzFactionDeck.Rows && t >= OzFactionDeck.RowAt + r * OzFactionDeck.RowStagger;

        // ---------------------------------------------------------------- EvilZod's figure (item 108)
        /// <summary>His render's scale and the
        /// point it scales about (poster uv from the bottom left: three quarters across, the frame band's top edge; measured on the light
        /// model's renders in the sway range: the figure's rows touching the right edge grow by 11 .. 50 of 791 px instead of 14 .. 103 about
        /// the middle, the left edge stays clear, his head moves 6 px to the left).</summary>
        public const float EvilScale = 1.06f;
        public const float BandV = 0.1934f;
        public static readonly Vector2 EvilAnchor = new Vector2(0.75f, BandV);
        /// <summary>The figure's uv rect: Zod's whole render; EvilZod's render shown EvilScale times bigger about EvilAnchor.</summary>
        public static Rect FigureUv(int side) => side == OzFaction.Blue ? Magnify(EvilAnchor, EvilScale) : new Rect(0f, 0f, 1f, 1f);
        /// <summary>A uv rect that shows a picture s times bigger about the point a (uv): a stays where it was.</summary>
        public static Rect Magnify(Vector2 a, float s) => new Rect(a.x - a.x / s, a.y - a.y / s, 1f / s, 1f / s);

        // ---------------------------------------------------------------- the cape's torn ends (item 113)
        /// <summary>The strip completed downward (shares of the poster): from FillBelow under the band's top edge (hidden by the band) to
        /// FillHeight above it, from the poster's left to FillRight (the cape; the leg armour beside it is solid down to the band), its right
        /// side fading over FillSoft. Measured on the light model's renders in the screen's range (yaw -8 .. +8, pitch -3 .. +3): the rags'
        /// gaps start 84 .. 108 render px above the band (of 1672); the strip's top row is 142 px above it (the fist stays 280 px above).</summary>
        public const float FillBelow = 0.008f, FillHeight = 0.09f, FillRight = 0.42f, FillSoft = 0.03f;
        /// <summary>The source row's colour is taken from the render's mip 3 (an 8 x 8 texel average: no thin highlight drawn out into
        /// streaks) and its coverage is at least FillGrow x that average's (a sliver of paper between the cape and the armour closes, so no
        /// rim lines run down the strip). The shader holds the same two numbers (OzPosterFigure: Filled).</summary>
        public const float FillMip = 3f, FillGrow = 1.6f;
        /// <summary>The shader's fill vector for a figure shown with the uv rect uv (render uv): from v, the source row v, right u, soft u.</summary>
        public static Vector4 Fill(Rect uv)
        {
            float band = uv.y + BandV * uv.height;
            return new Vector4(band - FillBelow * uv.height, band + FillHeight * uv.height, uv.x + FillRight * uv.width, FillSoft * uv.width);
        }

        // ---------------------------------------------------------------- the lenses and the eyes (item 115)
        /// <summary>A spot on a light model: its middle and two half-axes (model root, metres).</summary>
        public struct Spot { public Vector3 centre, axis1, axis2; }
        /// <summary>Zod's left and right lens (the dark glass inside the gold frames).</summary>
        public static readonly Spot[] Lenses =
        {
            new Spot { centre = new Vector3(-0.11951f, 0.50836f, 0.15714f), axis1 = new Vector3(-0.04511f, 0.00738f, 0.00319f), axis2 = new Vector3(0.00531f, 0.0338f, -0.00309f) },
            new Spot { centre = new Vector3(0.00714f, 0.49748f, 0.16874f), axis1 = new Vector3(-0.05058f, 0.00118f, 0.00413f), axis2 = new Vector3(0.00049f, 0.02936f, -0.00234f) },
        };
        /// <summary>EvilZod's left and right eye (their red-orange glow).</summary>
        public static readonly Spot[] Eyes =
        {
            new Spot { centre = new Vector3(-0.01454f, 0.47574f, 0.08016f), axis1 = new Vector3(-0.01866f, 0.00614f, -0.00064f), axis2 = new Vector3(0.0024f, 0.00728f, -0.00004f) },
            new Spot { centre = new Vector3(0.08769f, 0.46311f, 0.08729f), axis1 = new Vector3(-0.02011f, -0.00379f, -0.00054f), axis2 = new Vector3(-0.00142f, 0.00753f, -0.00016f) },
        };
        /// <summary>The lens ellipses a little larger than the glass (the glint is also kept to the dark pixels, so the gold frame stays).</summary>
        public const float LensGrow = 1.04f;

        /// <summary>A spot projected to the render (uv): its middle and its two half-axes.</summary>
        public struct SpotUv { public Vector2 c, a1, a2; }

        /// <summary>A spot through the poster's camera (root = the poster model's root); false when it is behind the camera.</summary>
        public static bool Project(Camera cam, Transform root, Spot s, float grow, out SpotUv o)
        {
            o = default(SpotUv);
            if (!cam || !root) return false;
            Vector3 p0 = cam.WorldToViewportPoint(root.TransformPoint(s.centre));
            Vector3 p1 = cam.WorldToViewportPoint(root.TransformPoint(s.centre + s.axis1 * grow));
            Vector3 p2 = cam.WorldToViewportPoint(root.TransformPoint(s.centre + s.axis2 * grow));
            if (p0.z <= 0f || p1.z <= 0f || p2.z <= 0f) return false;
            o.c = new Vector2(p0.x, p0.y); o.a1 = new Vector2(p1.x - p0.x, p1.y - p0.y); o.a2 = new Vector2(p2.x - p0.x, p2.y - p0.y);
            return true;
        }

        /// <summary>The map from render uv to a spot's ellipse (the inverse of its axes' matrix; |e| = 1 on its edge) as the shader's
        /// vector: e = (dot(xy, d), dot(zw, d)), d = uv - middle. False when the axes are degenerate.</summary>
        public static bool Inverse(Vector2 a1, Vector2 a2, out Vector4 inv)
        {
            float det = a1.x * a2.y - a2.x * a1.y;
            inv = Vector4.zero;
            if (Mathf.Abs(det) < 1e-10f) return false;
            inv = new Vector4(a2.y / det, -a2.x / det, -a1.y / det, a1.x / det);
            return true;
        }

        // the glint
        /// <summary>The glint: once every GlintPeriod s (the first GlintDelay s after the screen's clock passes a period's start) a band of
        /// GlintWidth (share of the two lenses' span) runs from GlintFrom to GlintTo across both lenses in GlintTime s, eased in and out,
        /// slanted (GlintSlant px of x per px of y: a "/"), adding up to GlintStrength of a warm white on the dark glass. CALM MODE: none.</summary>
        public const float GlintPeriod = 4.6f, GlintDelay = 1.2f, GlintTime = 0.9f, GlintFrom = -0.25f, GlintTo = 1.25f, GlintWidth = 0.05f, GlintStrength = 0.42f, GlintSlant = 0.55f;
        /// <summary>The glass: linear lightness up to DarkLo is glass (the band shows fully), from DarkHi up not (the frame, the face). The
        /// glass measures 0.01 on the light model's render and 0.014 .. 0.045 in the game's picture; the face round it 0.06 .. 0.21.</summary>
        public const float DarkLo = 0.04f, DarkHi = 0.08f;
        public static readonly Color GlintTone = new Color(1f, 0.98f, 0.92f, 1f);
        /// <summary>The band's place at t s (shares of the lenses' span), -9 between passes (no light).</summary>
        public static float GlintPos(float t)
        {
            float u = Mathf.Repeat(t - GlintDelay, GlintPeriod) / GlintTime;
            if (u >= 1f) return -9f;
            float e = u * u * (3f - 2f * u);
            return Mathf.Lerp(GlintFrom, GlintTo, e);
        }
        /// <summary>The shader's glint vector: place, width, strength, slant.</summary>
        public static Vector4 Glint(float t, bool calm) => calm ? new Vector4(-9f, GlintWidth, 0f, GlintSlant) : new Vector4(GlintPos(t), GlintWidth, GlintStrength, GlintSlant);
        /// <summary>The band's coordinate span over the two lenses (render px, slanted: x - GlintSlant y) and the glass's darkness limits.</summary>
        public static Vector4 GlintSpan(SpotUv l0, SpotUv l1, Vector2 px)
        {
            float lo0, hi0, lo1, hi1;
            Span(l0, px, out lo0, out hi0); Span(l1, px, out lo1, out hi1);
            float lo = Mathf.Min(lo0, lo1), hi = Mathf.Max(hi0, hi1);
            return new Vector4(lo, Mathf.Max(lo + 1f, hi), DarkLo, DarkHi);
        }
        static void Span(SpotUv s, Vector2 px, out float lo, out float hi)
        {
            Vector2 c = Vector2.Scale(s.c, px), a = Vector2.Scale(s.a1, px), b = Vector2.Scale(s.a2, px);
            float xc = c.x - GlintSlant * c.y, ax = a.x - GlintSlant * a.y, bx = b.x - GlintSlant * b.y;
            float e = Mathf.Sqrt(ax * ax + bx * bx);
            lo = xc - e; hi = xc + e;
        }

        // the eyes' glow
        /// <summary>The eyes' glow: a slow pulse (GlowPeriod s, a cosine: eased, never a jump) from GlowBase to 1, the eyes' own light up to
        /// GlowCore brighter and a halo of GlowHalo (EyeTone) of GlowHaloSize times an eye's half-length. CALM MODE: CalmShare of the pulse.</summary>
        public const float GlowPeriod = 2.8f, GlowBase = 0.35f, GlowCore = 0.9f, GlowHalo = 0.5f, GlowHaloSize = 1.9f, CalmShare = 0.25f;
        public static readonly Color EyeTone = new Color(1f, 0.26f, 0.07f, 1f);
        public static float Pulse01(float t) => 0.5f - 0.5f * Mathf.Cos(t / GlowPeriod * Mathf.PI * 2f);
        public static float GlowK(float t, bool calm) => GlowBase + (1f - GlowBase) * Pulse01(t) * (calm ? CalmShare : 1f);
        /// <summary>The shader's glow vector: amount, own light, halo, halo size (render px).</summary>
        public static Vector4 EyeGlow(float t, bool calm, float haloPx) => new Vector4(GlowK(t, calm), GlowCore, GlowHalo, Mathf.Max(1f, haloPx));
        /// <summary>The halo's size (render px) from the two eyes' projected half-lengths.</summary>
        public static float HaloPx(SpotUv e0, SpotUv e1, Vector2 px) => GlowHaloSize * 0.5f * (Vector2.Scale(e0.a1, px).magnitude + Vector2.Scale(e1.a1, px).magnitude);

        // ---------------------------------------------------------------- the poster's shade on its card (item 112)
        /// <summary>The shade's width (u) and its strength at the poster's edge (black alpha; it falls smoothly to 0 across the width).</summary>
        public const float ShadeW = 6f, ShadeAlpha = 0.18f;
        /// <summary>The shade at u (0 = the poster's edge, 1 = its far side).</summary>
        public static float ShadeAt(float u) { float s = 1f - Mathf.Clamp01(u); return s * s * (3f - 2f * s); }

        // ---------------------------------------------------------------- the shader's names
        public const float FxNone = 0f, FxGlint = 1f, FxGlow = 2f;
        public static readonly int IdFxMode = Shader.PropertyToID("_FxMode"), IdSpotC = Shader.PropertyToID("_SpotC"), IdSpotM1 = Shader.PropertyToID("_SpotM1"),
            IdSpotM2 = Shader.PropertyToID("_SpotM2"), IdGlint = Shader.PropertyToID("_Glint"), IdGlintSpan = Shader.PropertyToID("_GlintSpan"),
            IdEyeGlow = Shader.PropertyToID("_EyeGlow"), IdEyeTone = Shader.PropertyToID("_EyeTone"), IdFill = Shader.PropertyToID("_Fill"), IdTone = Shader.PropertyToID("_SweepTone");
    }
}
