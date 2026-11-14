using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    // ============================================================================================================================
    // k75a4 (1.2 owner items 102, 104, 108, 112, 113, 115): the faction screen's polish (UI/OzFactionPolish.cs: the numbers and rules).
    //  * Each poster's 3D render draws with its own copy of the render material: Zod's with the glint across his sunglasses, EvilZod's
    //    with his eyes' glow, his render a tiny bit bigger (its uv rect) and his cape completed down to the frame band.
    //  * The posters' hard drop shadow and focus glow lie under the DOSSIER cards (each in a holder that keeps its poster's place and
    //    scale); while a card is out its poster casts only a narrow, light shade on it.
    //  * Each row of a card coming out ticks once (ui_dossier_row), quietly, never two ticks in the same 45 ms.
    // Hooks in UI/OpenZMainMenu.Faction.cs: BuildFaction (BuildFactionPolish), TickFaction (FcFigureMat for the render's material,
    // TickFactionPolish after TickFactionDeck), CloseFactionRig (CloseFactionPolish).
    // ============================================================================================================================
    public partial class OpenZMainMenu
    {
        readonly Material[] fcFx = new Material[2];                                                            // each poster's render material
        Material fcFxFrom;                                                                                     // the rig's material they were copied from
        readonly RectTransform[] fcUnder = new RectTransform[2];                                              // the posters' shadow and glow, under the cards
        readonly RawImage[] fcShade = new RawImage[2];                                                        // the shade a poster casts on its card
        readonly OzFactionDeck.CardState[] fcTickState = new OzFactionDeck.CardState[2];
        readonly int[] fcTickNext = { OzFactionDeck.Rows, OzFactionDeck.Rows };
        float fcTickAt = -10f;
        static Texture2D fcShadeTex;

        /// <summary>Checks: the row ticks played since the game started.</summary>
        public static int FactionRowTicks { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFactionPolishChecks() { FactionRowTicks = 0; }

        // ---------------------------------------------------------------- build (BuildFaction, after the posters and the VS sign)
        void BuildFactionPolish()
        {
            if (!fcGroup) return;
            for (int i = 0; i < 2; i++)
            {
                var p = fc[i]; if (p == null || !p.rt) continue;
                string name = i == OzFaction.Red ? "Zod" : "EvilZod";
                // the poster's drop shadow and focus glow under the cards: a holder with the poster's place, its scale copied every frame
                var under = Hud.Node("PosterUnder_" + name, fcGroup).Place(new OzRect(p.x, p.y, p.w, p.h));
                under.pivot = new Vector2(0.5f, 0.5f); under.anchoredPosition += new Vector2(p.w * 0.5f, -p.h * 0.5f);
                under.SetSiblingIndex(0);
                if (p.glow) p.glow.rectTransform.SetParent(under, false);
                if (p.shadow) p.shadow.rectTransform.SetParent(under, false);
                fcUnder[i] = under;
                // the narrow, light shade on the card (a child of the poster: over the card, under the poster's paper)
                var card = fcLayout.Card(i);
                var sh = Raw(p.rt, "CardShade");
                sh.rectTransform.Place(new OzRect(i == OzFaction.Red ? -OzFactionPolish.ShadeW : p.w, card.y - p.y, OzFactionPolish.ShadeW, card.height));
                sh.rectTransform.SetSiblingIndex(0);
                sh.texture = FcShadeTexture();
                sh.uvRect = i == OzFaction.Red ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);            // dark at the poster's edge
                sh.color = new Color(0f, 0f, 0f, 0f); sh.enabled = false;
                fcShade[i] = sh;
            }
        }

        // ---------------------------------------------------------------- the render's material (TickFaction)
        /// <summary>The poster's own copy of the rig's render material (its glint or glow on top); the rig's material while it has none.</summary>
        Material FcFigureMat(int i)
        {
            var src = fcRig ? fcRig.Figure : null;
            if (!src) return null;
            if (fcFxFrom != src) { FcFxDrop(); fcFxFrom = src; }
            if (!fcFx[i])
                fcFx[i] = new Material(src) { name = "OzPosterFigure (faction render " + i + ")", hideFlags = HideFlags.DontSave };
            return fcFx[i];
        }

        void FcFxDrop()
        {
            for (int i = 0; i < 2; i++)
            {
                if (fc[i] != null && fc[i].figure && fcFx[i] && fc[i].figure.material == fcFx[i]) fc[i].figure.material = null;
                if (fcFx[i]) { Destroy(fcFx[i]); fcFx[i] = null; }
            }
            fcFxFrom = null;
        }

        /// <summary>CloseFactionRig: the materials go with the rig; no row ticks left over.</summary>
        void CloseFactionPolish()
        {
            FcFxDrop();
            for (int i = 0; i < 2; i++) { fcTickState[i] = OzFactionDeck.CardState.Hidden; fcTickNext[i] = OzFactionDeck.Rows; }
        }

        // ---------------------------------------------------------------- every frame (TickFaction, after TickFactionDeck)
        void TickFactionPolish(float dt, int active)
        {
            if (!fcRoot || fc[0] == null || fc[1] == null) return;
            float now = OzMotion.Now; bool calm = OzMotion.Reduced;
            for (int i = 0; i < 2; i++)
            {
                var p = fc[i]; var d = fcDos[i];
                if (fcUnder[i] && p.rt && fcUnder[i].localScale != p.rt.localScale) fcUnder[i].localScale = p.rt.localScale;
                float k = d != null && d.state != OzFactionDeck.CardState.Hidden ? Mathf.Clamp01(d.slide) * (d.cg ? d.cg.alpha : 1f) : 0f;   // CALM MODE: the card fades, its shade with it
                var sh = fcShade[i];
                if (sh)
                {
                    bool on = k > 0.001f;
                    if (sh.enabled != on) sh.enabled = on;
                    if (on) sh.color = new Color(0f, 0f, 0f, OzFactionPolish.ShadeAlpha * k);
                }
                var uv = OzFactionPolish.FigureUv(i);
                if (p.figure && p.figure.uvRect != uv) p.figure.uvRect = uv;
                var m = fcFx[i];
                if (m && fcRig && fcRig.Figure)
                {
                    m.SetFloat(FcRimPx, fcRig.Figure.GetFloat(FcRimPx)); m.SetColor(FcPaperBehind, fcRig.Figure.GetColor(FcPaperBehind));
                    FcFx(i, m, fcRig.P[i], uv, now, calm);
                }
                FcRowTick(i, d, now, calm);
            }
        }

        /// <summary>The render's glint (Zod) or glow and fill (EvilZod): the lenses / eyes projected with the poster's camera as it is now.</summary>
        void FcFx(int i, Material m, OzFactionPosters.Poster rp, Rect uv, float now, bool calm)
        {
            bool zod = i == OzFaction.Red;
            var spots = zod ? OzFactionPolish.Lenses : OzFactionPolish.Eyes;
            float grow = zod ? OzFactionPolish.LensGrow : 1f;
            OzFactionPolish.SpotUv s0 = default(OzFactionPolish.SpotUv), s1 = default(OzFactionPolish.SpotUv);
            Vector4 m0 = Vector4.zero, m1 = Vector4.zero;
            bool ok = rp != null && rp.cam && rp.root && rp.rt
                      && OzFactionPolish.Project(rp.cam, rp.root, spots[0], grow, out s0) && OzFactionPolish.Project(rp.cam, rp.root, spots[1], grow, out s1)
                      && OzFactionPolish.Inverse(s0.a1, s0.a2, out m0) && OzFactionPolish.Inverse(s1.a1, s1.a2, out m1);
            m.SetFloat(OzFactionPolish.IdFxMode, ok ? (zod ? OzFactionPolish.FxGlint : OzFactionPolish.FxGlow) : OzFactionPolish.FxNone);
            m.SetVector(OzFactionPolish.IdFill, zod ? Vector4.zero : OzFactionPolish.Fill(uv));                 // EvilZod's cape down to the band
            if (!ok) return;
            var px = new Vector2(rp.rt.width, rp.rt.height);
            m.SetVector(OzFactionPolish.IdSpotC, new Vector4(s0.c.x, s0.c.y, s1.c.x, s1.c.y));
            m.SetVector(OzFactionPolish.IdSpotM1, m0); m.SetVector(OzFactionPolish.IdSpotM2, m1);
            if (zod)
            {
                m.SetVector(OzFactionPolish.IdGlint, OzFactionPolish.Glint(now, calm));
                m.SetVector(OzFactionPolish.IdGlintSpan, OzFactionPolish.GlintSpan(s0, s1, px));
                m.SetColor(OzFactionPolish.IdTone, OzFactionPolish.GlintTone);
            }
            else
            {
                m.SetVector(OzFactionPolish.IdEyeGlow, OzFactionPolish.EyeGlow(now, calm, OzFactionPolish.HaloPx(s0, s1, px)));
                m.SetColor(OzFactionPolish.IdEyeTone, OzFactionPolish.EyeTone);
            }
        }

        /// <summary>One quiet tick per row as a card's rows appear (not in CALM MODE: the rows come all at once with the card's own sound).</summary>
        void FcRowTick(int i, FcDossier d, float now, bool calm)
        {
            if (d == null) return;
            var s = d.state;
            bool coming = s == OzFactionDeck.CardState.In || s == OzFactionDeck.CardState.Shown;
            float t = fcProofCardT ?? (now - d.since);
            if (s == OzFactionDeck.CardState.In && fcTickState[i] != OzFactionDeck.CardState.In) fcTickNext[i] = OzFactionPolish.RowsIn(t);   // (re)started: the rows already in do not tick
            fcTickState[i] = s;
            if (!coming) { fcTickNext[i] = OzFactionDeck.Rows; return; }
            if (calm || !d.content) return;
            if (OzFactionPolish.RowDue(fcTickNext[i], t) && now - fcTickAt >= OzFactionPolish.RowTickGap)
            {
                Sfx(OzFactionPolish.SoundRow); FactionRowTicks++; fcTickAt = now; fcTickNext[i]++;
            }
        }

        /// <summary>The shade's gradient (white; the shade's colour tints it): opaque at the poster's edge, clear at its far side.</summary>
        static Texture2D FcShadeTexture()
        {
            if (fcShadeTex) return fcShadeTex;
            const int n = 32;
            var t = new Texture2D(n, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "OzFaction_CardShade" };
            var px = new Color32[n * 2];
            for (int x = 0; x < n; x++)
            {
                byte a = (byte)Mathf.RoundToInt(OzFactionPolish.ShadeAt((x + 0.5f) / n) * 255f);
                px[x] = px[n + x] = new Color32(255, 255, 255, a);
            }
            t.SetPixels32(px); t.Apply(false, true);
            fcShadeTex = t;
            return t;
        }

        // ---------------------------------------------------------------- proof hooks (View/OzFactionPolishProof.cs, Play only)
        public Material ProofFigureFx(int i) => fcFx[i];
        public RawImage ProofCardShade(int i) => fcShade[i];
        public RectTransform ProofPosterUnder(int i) => fcUnder[i];
        public Image ProofPosterShadow(int i) => fc[i] != null ? fc[i].shadow : null;
        public Image ProofPosterGlow(int i) => fc[i] != null ? fc[i].glow : null;
        public RectTransform ProofFactionRoot => fcRoot;
    }
}
