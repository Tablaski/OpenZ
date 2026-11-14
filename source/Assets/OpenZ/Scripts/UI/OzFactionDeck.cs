using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// k75a3 (1.2 owner items 101 / 102): the faction screen's VS sign, its live poster prints and the DOSSIER cards. This class holds
    /// the words, the layout and the pure motion rules (counted headless by Editor/OzV12FactionDeckCheck.cs); UI/OpenZMainMenu.FactionDeck.cs
    /// draws them, View/OzFactionDeckAssets.cs loads their pictures, fonts and motion maps only while the faction screen shows.
    ///  * Layout : the two posters stand <see cref="Spread"/> from the middle (the owner's
    ///    picture measured: 394 u), the VS sign in the middle between them, each DOSSIER card on its poster's outer side. The whole group
    ///    scales down together on a canvas narrower than it (16:10, 4:3, 5:4), so nothing overlaps anywhere.
    ///  * The VS sign (item 101: "this exact vs sign ... cut it out nicely ... slightly animate it smoothen pulsing and some light
    ///    animation in loop going through it"): his sign cut out of his picture (Resources/OpenZ/Faction/Vs, premultiplied: no halo, no
    ///    backdrop), a slow eased pulse (scale 1.00 .. 1.04 and its glow, 2.5 s) and a soft light band passing through the letters
    ///    (every 3.6 s, masked to the sign). <see cref="VsSign"/> picks the sign: the grunge one of his item 101 picture (the default) or
    ///    the glowing one of his item 102 picture.
    ///  * The live prints : while the pointer (or the keys' / pad's focus) is
    ///    on a poster, its print comes alive, easing in and out: Zod's sunburst rays turn slowly and his star breathes; EvilZod's rays fan,
    ///    his banners wave, his jets drift and his robot army's eyes glow; a soft light passes over the paper. Each motion is weighted by
    ///    the poster's motion map (Faction/Layers/&lt;poster&gt;_motion: 0 = never moves), so the frame and the rest of the print stay put.
    ///  * The DOSSIER cards (item 102, his card design and his words): on hover / focus the card of that poster slides out from behind it
    ///    with a small tilt that settles, its rows appear one after another, a paper sound; on leave it slides back; never both at once.
    /// </summary>
    public static class OzFactionDeck
    {
        public const string DossierWord = "DOSSIER //";
        /// <summary>Each row: the label (typewriter) and its value (bold), as the owner wrote them: "&lt;label&gt; &lt;value&gt;".</summary>
        public static readonly string[][] Label =
        {
            new[] { "Grumpiness:", "Rocket Fuel Tolerance:", "Patience:", "Mood Forecast:", "Tolerance for nonsense -" },
            new[] { "Tendencies:", "Diplomacy:", "Ego levels:", "Boardroom Villainy:", "Treachery Potential:" },
        };
        public static readonly string[][] Value =
        {
            new[] { "100%", "Legendary", "2%", "Stormy", "none" },
            new[] { "World Hostile Takeover", "Not an option", "Critical", "100%", "Peak" },
        };
        /// <summary>The owner's line of a row, plain ("Grumpiness: 100%").</summary>
        public static string Line(int side, int row) => Label[S(side)][row] + " " + Value[S(side)][row];
        /// <summary>The row as the card draws it: the value in bold; on two lines (label, then value) when one line is wider than the
        /// row's text width (the mock-up's long rows wrap this way).</summary>
        public static string RowText(int side, int row, bool twoLines) => Label[S(side)][row] + (twoLines ? "\n" : " ") + "<b>" + Value[S(side)][row] + "</b>";
        /// <summary>The row icons (cells of the icon atlas, Faction/Dossier/dossier_icons: 5 x 2 cells of 128 px).</summary>
        public static readonly int[][] Icon = { new[] { 0, 1, 2, 3, 4 }, new[] { 5, 6, 7, 8, 9 } };
        public static readonly string[] IconName = { "angry face", "rocket", "hourglass", "storm cloud", "no-sign", "globe", "handshake", "crown", "briefcase", "dagger" };
        public const int Rows = 5, IconCols = 5, IconRowsInAtlas = 2;
        /// <summary>The icon's cell in the atlas as a RawImage uv rect (cell 0 = top left).</summary>
        public static Rect IconUv(int cell) => new Rect((cell % IconCols) / (float)IconCols, 1f - (cell / IconCols + 1) / (float)IconRowsInAtlas, 1f / IconCols, 1f / IconRowsInAtlas);
        static int S(int side) => side == OzFaction.Blue ? OzFaction.Blue : OzFaction.Red;

        // ---------------------------------------------------------------- the files (Resources, loaded only on the faction screen)
        public const string Folder = "OpenZ/Faction/";
        public static readonly string[] CardTex = { Folder + "Dossier/dossier_zod", Folder + "Dossier/dossier_evil" };
        public const string IconTex = Folder + "Dossier/dossier_icons";
        public static readonly string[] MotionTex = { Folder + "Layers/zod_motion", Folder + "Layers/evil_motion" };
        /// <summary>k75a4: the rows and "DOSSIER //" in Courier Prime (a slab-serif typewriter as the owner's mock-up; regular, the values
        /// </summary>
        public const string FontHeading = Folder + "Fonts/Staatliches-Regular", FontBody = Folder + "Fonts/CourierPrime-Regular", FontBold = Folder + "Fonts/CourierPrime-Bold";
        public const string SoundIn = "ui_dossier_in", SoundOut = "ui_dossier_out";

        // ---------------------------------------------------------------- the VS sign
        public const int VsGrunge = 0, VsGlow = 1;
        /// <summary>Which sign shows: VsGrunge (the red / blue painted VS of the owner's item 101 picture, his "this exact vs sign") or
        /// VsGlow (the glowing lightning VS of his item 102 picture).</summary>
        public static int VsSign = VsGrunge;
        public static readonly string[] VsTex = { Folder + "Vs/vs_grunge", Folder + "Vs/vs_glow" };
        public static readonly string[] VsHaloTex = { Folder + "Vs/vs_grunge_halo", Folder + "Vs/vs_glow_halo" };
        /// <summary>The sign's size on the canvas (u; its picture's own aspect), as it stands between the posters in his pictures (the
        /// posters' height as the ruler), and its middle relative to the posters' middle (u, + = down).</summary>
        public static readonly Vector2[] VsSize = { new Vector2(316f, 429f), new Vector2(291f, 499f) };
        public static readonly float[] VsDy = { -7f, -30f };
        /// <summary>The pictures' pixels (the sign; its halo is the sign's blurred glow with HaloMarginPx of the sign's pixels round it).</summary>
        public static readonly Vector2Int[] VsPx = { new Vector2Int(817, 1109), new Vector2Int(879, 1508) };
        public const float HaloMarginPx = 120f;
        /// <summary>The pulse: scale 1 .. 1 + PulseScale and the halo's alpha HaloLo .. HaloHi, one eased cosine loop of PulsePeriod s.
        /// The light: a soft band (SweepWidth of the sign's diagonal) passes through the letters in SweepTime s, once every SweepPeriod s,
        /// adding up to SweepStrength of white where the sign is (masked by the sign's own coverage). CALM MODE keeps CalmShare of the
        /// pulse and no light band.</summary>
        public const float PulsePeriod = 2.5f, PulseScale = 0.04f, HaloLo = 0.30f, HaloHi = 0.62f;
        public const float SweepPeriod = 3.6f, SweepTime = 1.5f, SweepWidth = 0.085f, SweepStrength = 0.55f, CalmShare = 0.25f;

        /// <summary>0 .. 1 .. 0 over one pulse (a cosine: eased, never a jump).</summary>
        public static float Pulse01(float t) => 0.5f - 0.5f * Mathf.Cos(t / PulsePeriod * Mathf.PI * 2f);
        public static float VsScale(float t, bool calm) => 1f + PulseScale * Pulse01(t) * (calm ? CalmShare : 1f);
        public static float HaloAlpha(float t, bool calm) => Mathf.Lerp(HaloLo, HaloLo + (HaloHi - HaloLo) * (calm ? CalmShare : 1f), Pulse01(t));
        /// <summary>The light band's place along the sign's diagonal (0 = its top left corner, 1 = its bottom right) at time t; it runs from
        /// -0.3 to 1.3 (fully outside at both ends, so it never pops) during the first SweepTime s of each SweepPeriod; outside that it waits
        /// at -1 (no light).</summary>
        public static float SweepPos(float t)
        {
            float u = Mathf.Repeat(t, SweepPeriod) / SweepTime;
            if (u >= 1f) return -1f;
            float e = u * u * (3f - 2f * u);                                                                   // eased in and out
            return Mathf.Lerp(-0.3f, 1.3f, e);
        }

        // ---------------------------------------------------------------- the layout (canvas units, 1080 u tall; group-local = canvas when the scale is 1)
        /// <summary>The posters' middles stand Spread u left and right of the middle (k75a: 277; the owner's item 101 picture: 394, his item
        /// 102 picture: 355, with the cards beside them). The DOSSIER card's paper is CardW u wide,
        /// tucked CardTuck u behind its poster's outer edge (it comes out from there), its top CardTop u under the posters' top. The group
        /// keeps Margin u from the screen's sides; narrower canvases scale the whole group about the middle of the posters' top edge.</summary>
        public const float Spread = 390f, CardW = 330f, CardTuck = 8f, CardTop = 46f, Margin = 16f;
        /// <summary>The card pictures (px) and the card's paper inside them (px: left, top, right, bottom): the clip, the ring and the torn
        /// corners stand out round the paper. The paper is drawn CardW u wide.</summary>
        public static readonly Vector2Int[] CardPx = { new Vector2Int(879, 1487), new Vector2Int(933, 1506) };
        public static readonly Vector4[] CardBody = { new Vector4(26f, 52f, 873f, 1461f), new Vector4(5f, 40f, 910f, 1482f) };
        public static float CardUnit(int side) { var b = CardBody[S(side)]; return CardW / (b.z - b.x); }                     // u per picture px
        public static float CardH(int side) { var b = CardBody[S(side)]; return (b.w - b.y) * CardUnit(side); }

        public struct Layout
        {
            /// <summary>The group's scale and the point it scales about (canvas units, top-left origin).</summary>
            public float scale; public Vector2 pivot;
            /// <summary>Group-local rects (top-left origin, y down): the posters, the VS sign (its picture), the cards' paper, the cards' pictures.</summary>
            public Rect poster0, poster1, vs, card0, card1, art0, art1;
            /// <summary>The group's left / right extent before the scale (u).</summary>
            public float left, right;
            public Rect Poster(int i) => i == 0 ? poster0 : poster1;
            public Rect Card(int i) => i == 0 ? card0 : card1;
            public Rect Art(int i) => i == 0 ? art0 : art1;
            /// <summary>A group-local point on the canvas (after the scale).</summary>
            public Vector2 ToCanvas(Vector2 p) => pivot + (p - pivot) * scale;
            public Rect ToCanvas(Rect r) { var a = ToCanvas(r.min); var b = ToCanvas(r.max); return Rect.MinMaxRect(a.x, a.y, b.x, b.y); }
        }

        /// <summary>The faction screen's group on a canvas cw u wide (the posters k75a's size, OzFaction.PosterH tall at OzFaction.PosterTop).</summary>
        public static Layout Group(float cw, int vsSign)
        {
            float h = OzFaction.PosterH, w = Mathf.Round(h * OzFaction.PosterAspect), top = OzFaction.PosterTop, cx = cw * 0.5f;
            var L = new Layout();
            L.poster0 = new Rect(Mathf.Round(cx - Spread - w * 0.5f), top, w, h);
            L.poster1 = new Rect(Mathf.Round(cx + Spread - w * 0.5f), top, w, h);
            int v = vsSign == VsGlow ? VsGlow : VsGrunge;
            var vs = VsSize[v];
            L.vs = new Rect(cx - vs.x * 0.5f, top + h * 0.5f + VsDy[v] - vs.y * 0.5f, vs.x, vs.y);
            for (int i = 0; i < 2; i++)
            {
                float k = CardUnit(i), ch = CardH(i);
                var b = CardBody[i];
                float x = i == 0 ? L.poster0.xMin + CardTuck - CardW : L.poster1.xMax - CardTuck;
                var card = new Rect(x, top + CardTop, CardW, ch);
                var art = new Rect(card.x - b.x * k, card.y - b.y * k, CardPx[i].x * k, CardPx[i].y * k);
                if (i == 0) { L.card0 = card; L.art0 = art; } else { L.card1 = card; L.art1 = art; }
            }
            L.left = Mathf.Min(L.art0.xMin, L.poster0.xMin);
            L.right = Mathf.Max(L.art1.xMax, L.poster1.xMax);
            float half = Mathf.Max(cx - L.left, L.right - cx);
            L.scale = Mathf.Min(1f, (cw * 0.5f - Margin) / Mathf.Max(1f, half));
            L.pivot = new Vector2(cx, top);
            return L;
        }

        // ---------------------------------------------------------------- the card (paper-relative: x / w of the paper, y / h of the paper)
        /// <summary>The card's words and lines on its paper, measured on the owner's mock-up card: "DOSSIER //" (the typewriter's bold) and
        /// the general's name (the condensed capitals) at the top left (the badge is the picture's, top right), the rule under them, then the
        /// five rows (icon, typewriter words, the value bold) with thin dotted lines between them (a dot every RowDot u). Sizes are canvas units.</summary>
        public const float DossierX = 0.10f, DossierY = 0.108f, DossierSize = 22f, HeadingSize = 43f, HeadingRight = 0.92f;                  // k75a4: DossierSize 22 (Courier Prime Bold: his mock-up's 138 u wide word)
        /// <summary>The general's name's cap top, per card: EvilZod's longer name runs under his badge, so it starts a little lower (clear of it).</summary>
        public static readonly float[] HeadingY = { 0.158f, 0.186f };
        public const float DossierTracking = 0.04f, HeadingTracking = 0.01f;
        /// <summary>The fonts' cap heights (share of the size: the fonts' own numbers), so a label stands by its cap top.</summary>
        public const float CapHeading = 0.696f, CapBody = 0.58f;                                              // k75a4: CapBody of Courier Prime (0.5796)
        public const float RuleY = 0.262f, RuleX0 = 0.095f, RuleX1 = 0.915f, RuleH = 2.5f;
        /// <summary>The rows' words from RowTextX to RowTextRight of the paper (245.85 u: "Treachery Potential: Peak", 25 letters of
        /// Courier Prime's 0.5996 em at 16.25 u = 243.6 u, stays on one line; the longer rows still take two lines, as before).</summary>
        public const float RowsY = 0.292f, RowIconX = 0.135f, RowIconSize = 31f, RowTextX = 0.215f, RowTextRight = 0.96f, RowTextSize = 16.25f;
        public const float RowPad = 10f, RowMinH = 52f, RowLineX0 = 0.10f, RowLineX1 = 0.905f, RowLineH = 1.1f, RowDot = 4f;
        public static readonly Color32 DossierInk = new Color32(52, 48, 44, 255), RowInk = new Color32(38, 35, 31, 255), RowLineInk = new Color32(92, 82, 70, 75);
        /// <summary>The cards' faction colour: the posters' title inks (red, navy blue).</summary>
        public static Color32 Ink(int side) => OzFaction.GeneralInk[S(side)];

        // ---------------------------------------------------------------- the card's animation
        /// <summary>In: the card slides out from behind its poster over CardIn s (an ease-out with a small overshoot) with a tilt of
        /// TiltIn deg that settles to its rest tilt (a damped swing: TiltHz, TiltDecay s); its heading appears at HeadAt s, the rows one
        /// after another from RowAt s, RowStagger s apart, each over RowFade s (and RowSlide u from the left). Out: back behind the poster
        /// over CardOut s (an ease-in), the rows fading first. The rest tilt as in the owner's picture (Zod's card turned a little
        /// anticlockwise, EvilZod's clockwise; deg, + = anticlockwise). CALM MODE: a plain fade of CalmFade s at rest.</summary>
        public const float CardIn = 0.42f, CardOut = 0.22f, Overshoot = 1.25f, TiltIn = 7f, TiltHz = 2.1f, TiltDecay = 0.13f;
        public const float HeadAt = 0.12f, RowAt = 0.20f, RowStagger = 0.055f, RowFade = 0.16f, RowSlide = 10f, CalmFade = 0.12f;
        public static readonly float[] RestTilt = { 1.6f, -1.15f };
        /// <summary>How far the hidden card sits behind its poster: CardW - CardTuck + HideExtra u toward the poster's middle.</summary>
        public const float HideExtra = 8f;
        public const float InDone = 0.75f;                                                                         // the In state ends (the swing has settled)

        /// <summary>The slide in (0 = hidden behind the poster, 1 = at rest; a little over 1 on the way: the overshoot) at t s.</summary>
        public static float SlideIn(float t) => t <= 0f ? 0f : t >= CardIn ? 1f : OzEase.OutBack(t / CardIn, Overshoot);
        /// <summary>The time on the way out at which the slide is k (0 .. 1): a card turned again on its way back comes on from there.</summary>
        public static float SlideInTime(float k)
        {
            if (k <= 0f) return 0f;
            float lo = 0f, hi = CardIn;
            for (int i = 0; i < 24; i++) { float m = (lo + hi) * 0.5f; if (SlideIn(m) < k) lo = m; else hi = m; }
            return hi;
        }

        /// <summary>The slide out from k0 (the place it had when it turned back) at t s.</summary>
        public static float SlideOut(float k0, float t) => t >= CardOut ? 0f : k0 * (1f - OzEase.InCubic(Mathf.Clamp01(t / CardOut)));
        /// <summary>The tilt (deg) at t s after the card started out: from rest + TiltIn (turned outward) swinging to rest.</summary>
        public static float TiltAt(int side, float t)
        {
            float sign = side == OzFaction.Red ? 1f : -1f;
            return RestTilt[S(side)] + sign * TiltIn * Mathf.Exp(-Mathf.Max(0f, t) / TiltDecay) * Mathf.Cos(Mathf.Max(0f, t) * TiltHz * Mathf.PI * 2f);
        }
        /// <summary>A row's (or the heading's: row -1) alpha at t s after the card started out.</summary>
        public static float RowIn(int row, float t)
        {
            float at = row < 0 ? HeadAt : RowAt + row * RowStagger;
            return OzEase.OutCubic(Mathf.Clamp01((t - at) / RowFade));
        }
        /// <summary>The card's x (group-local) at a slide k: hidden behind the poster at 0, at rest at 1.</summary>
        public static float CardX(int side, Rect card, float k)
        {
            float hidden = card.x + (side == OzFaction.Red ? 1f : -1f) * (CardW - CardTuck + HideExtra);
            return Mathf.LerpUnclamped(hidden, card.x, k);
        }

        /// <summary>The cards' states (pure, for the menu and the check): Hidden, In (coming out), Shown, Out (going back).</summary>
        public enum CardState { Hidden, In, Shown, Out }

        /// <summary>One frame of a card's state machine: want = its poster is the one in focus; otherShown = the other card is not
        /// Hidden. A card starts out only when the other one is fully back (never both at once); it turns back at once when the focus leaves.
        /// Returns the new state; started / turned say which sound to play.</summary>
        public static CardState Step(CardState s, bool want, bool otherShown, float since, out bool started, out bool turned)
        {
            started = turned = false;
            switch (s)
            {
                case CardState.Hidden: if (want && !otherShown) { started = true; return CardState.In; } return s;
                case CardState.In: if (!want) { turned = true; return CardState.Out; } return since >= InDone ? CardState.Shown : s;
                case CardState.Shown: if (!want) { turned = true; return CardState.Out; } return s;
                default: if (want && !otherShown) { started = true; return CardState.In; } return since >= CardOut ? CardState.Hidden : s;
            }
        }

        /// <summary>The live weight's ease (s, a critically damped step as the mood's) and the light over the paper: a soft band
        /// (PaperSweepWidth of the poster's diagonal) crossing the print in PaperSweepTime s every PaperSweepPeriod s, brightening the
        /// paper by up to PaperSweepStrength (the ink a third of that).</summary>
        public const float LiveTime = 0.45f, PaperSweepPeriod = 5.2f, PaperSweepTime = 2.2f, PaperSweepWidth = 0.11f, PaperSweepStrength = 0.11f;
        public static readonly Color PaperSweepTone = new Color(1f, 0.96f, 0.86f, 1f);

        /// <summary>The paper light band's place along the poster's diagonal (as SweepPos, with the poster's period).</summary>
        public static float PaperSweepPos(float t, float phase)
        {
            float u = Mathf.Repeat(t + phase, PaperSweepPeriod) / PaperSweepTime;
            if (u >= 1f) return -1f;
            float e = u * u * (3f - 2f * u);
            return Mathf.Lerp(-0.3f, 1.3f, e);
        }

        /// <summary>One print layer's motion (shader OzPosterFigure, print mode; each weighted by a channel of the poster's motion map and
        /// by the live weight). Spin: xy = the middle (share of the layer from its top left), z = amplitude (deg), w = period (s); SpinMirror 1 = the left half turns the
        /// other way (a fan). Breathe: xy = the middle (as the spin's), z = scale amplitude, w = period (s). Wave (hanging cloth, the weight grows
        /// toward the free end): x = amplitude (layer px), y = wavelength (layer px), z = speed (waves per s), w = phase spread across x.
        /// Drift: x / y = amplitude (layer px), z = period (s), w = phase spread across x. Glow: x = amount, y = period (s), z = phase
        /// spread across x. Each *W picks the motion map's channels (r, g, b, a).</summary>
        public struct Motion
        {
            public Vector4 spin, spinW, breathe, breatheW, wave, waveW, drift, driftW, glow, glowW; public float spinMirror;
        }

        /// <summary>[side * 2 + layer] (layer 0 = the back plate, 1 = the mid layer). Amplitudes stay inside the motion maps' margins (the
        /// counted check): a ray moves at most 26 layer px, a banner 9, a jet 12; the maps reach 30 / 14 / 22 px round them.</summary>
        public static readonly Motion[] Motions =
        {
            // Zod, back plate: the sunburst's rays turn slowly about its middle
            new Motion { spin = new Vector4(0.5f, 0.45f, 1.5f, 11f), spinW = new Vector4(1, 0, 0, 0) },
            // Zod, mid layer: the red star breathes and turns a little about its own middle
            new Motion { spin = new Vector4(0.5f, 0.203f, 1.2f, 8.5f), spinW = new Vector4(0, 1, 0, 0), breathe = new Vector4(0.5f, 0.203f, 0.022f, 3.4f), breatheW = new Vector4(0, 1, 0, 0) },
            // EvilZod, back plate: the red rays fan about their meeting point (left and right mirrored)
            new Motion { spin = new Vector4(0.5f, 0.41f, 1.4f, 6.5f), spinW = new Vector4(1, 0, 0, 0), spinMirror = 1f },
            // EvilZod, mid layer: the banners wave, the jets drift, the army's eyes glow
            new Motion { wave = new Vector4(9f, 260f, 0.55f, 2.3f), waveW = new Vector4(0, 1, 0, 0), drift = new Vector4(12f, 3.5f, 4.8f, 3.1f), driftW = new Vector4(0, 0, 1, 0),
                         glow = new Vector4(0.85f, 1.6f, 9f, 0f), glowW = new Vector4(0, 0, 0, 1) },
        };
        public static Motion MotionOf(int side, int layer) => Motions[S(side) * 2 + (layer == 0 ? 0 : 1)];
        /// <summary>The layers' grid (px; the motions' px are of this grid) and the largest move a motion makes there at full weight.</summary>
        public const float LayerW = 1152f, LayerH = 2048f;
        public static float MaxMovePx(Motion m)
        {
            float spin = 0f;
            if (m.spin.z > 0f)
            {
                float rx = Mathf.Max(m.spin.x, 1f - m.spin.x) * LayerW, ry = Mathf.Max(m.spin.y, 1f - m.spin.y) * LayerH;
                spin = Mathf.Sqrt(rx * rx + ry * ry) * m.spin.z * Mathf.Deg2Rad;
            }
            float br = m.breathe.z * Mathf.Sqrt(LayerW * LayerW + LayerH * LayerH) * 0.5f;
            float wave = m.wave.x, drift = Mathf.Sqrt(m.drift.x * m.drift.x + m.drift.y * m.drift.y);
            return Mathf.Max(spin + br, Mathf.Max(wave, drift));
        }
        public const float CalmLive = 0.25f;

        /// <summary>The shader's property names of the live prints.</summary>
        public static readonly int IdLive = Shader.PropertyToID("_Live"), IdLiveT = Shader.PropertyToID("_LiveT"), IdMotionMap = Shader.PropertyToID("_MotionMap"),
            IdSpin = Shader.PropertyToID("_Spin"), IdSpinW = Shader.PropertyToID("_SpinW"), IdSpinMirror = Shader.PropertyToID("_SpinMirror"),
            IdBreathe = Shader.PropertyToID("_Breathe"), IdBreatheW = Shader.PropertyToID("_BreatheW"), IdWave = Shader.PropertyToID("_Wave"), IdWaveW = Shader.PropertyToID("_WaveW"),
            IdDrift = Shader.PropertyToID("_Drift"), IdDriftW = Shader.PropertyToID("_DriftW"), IdGlow = Shader.PropertyToID("_Glow"), IdGlowW = Shader.PropertyToID("_GlowW"),
            IdSweep = Shader.PropertyToID("_Sweep"), IdSweepTone = Shader.PropertyToID("_SweepTone"), IdLayerPx = Shader.PropertyToID("_LayerPx");

        /// <summary>A print layer's material gets its motion (once; the live weight and the clock each frame).</summary>
        public static void SetMotion(Material m, Motion mo, Texture map)
        {
            if (!m) return;
            m.SetTexture(IdMotionMap, map);
            m.SetVector(IdSpin, new Vector4(mo.spin.x, 1f - mo.spin.y, mo.spin.z, mo.spin.w)); m.SetVector(IdSpinW, mo.spinW); m.SetFloat(IdSpinMirror, mo.spinMirror);   // the middles: from the top here, uv (from the bottom) in the shader
            m.SetVector(IdBreathe, new Vector4(mo.breathe.x, 1f - mo.breathe.y, mo.breathe.z, mo.breathe.w)); m.SetVector(IdBreatheW, mo.breatheW);
            m.SetVector(IdWave, mo.wave); m.SetVector(IdWaveW, mo.waveW);
            m.SetVector(IdDrift, mo.drift); m.SetVector(IdDriftW, mo.driftW);
            m.SetVector(IdGlow, mo.glow); m.SetVector(IdGlowW, mo.glowW);
            m.SetVector(IdLayerPx, new Vector4(LayerW, LayerH, 0f, 0f));
            m.SetColor(IdSweepTone, PaperSweepTone);
        }

        /// <summary>The sweep vector of the shader: x = the band's place on the diagonal (-1: none), y = its width, z = its strength (x the
        /// live weight), w = the share the ink gets.</summary>
        public static Vector4 PaperSweep(float t, float phase, float live) => new Vector4(PaperSweepPos(t, phase), PaperSweepWidth, PaperSweepStrength * live, 0.33f);
        public static Vector4 VsSweep(float t, bool calm) => calm ? new Vector4(-1f, SweepWidth, 0f, 1f) : new Vector4(SweepPos(t), SweepWidth, SweepStrength, 1f);
    }
}
