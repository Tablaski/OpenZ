using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    // ============================================================================================================================
    // k75a3 (1.2 owner items 101 / 102): the faction screen's VS sign, its live poster prints and the DOSSIER cards (UI/OzFactionDeck.cs:
    // the words, layout and motion rules; View/OzFactionDeckAssets.cs: the pictures and fonts, loaded only while the screen shows).
    //   [card ZOD] [poster GENERAL ZOD]      VS      [poster GENERAL EVILZOD] [card EVILZOD]      (one card at a time, on hover / focus)
    //  * The group (the posters, their tip slots, the VS sign and the cards) stands as the owner's pictures show; on a canvas narrower than
    //    it the whole group scales down together about the middle of the posters' top edge.
    //  * The VS sign pulses slowly (scale and glow) and a soft light passes through its letters (OzPosterFigure in its VS mode).
    //  * While a poster is pointed at (or has the keys' / pad's focus) its print comes alive (the print layers' own motion, eased in and out).
    //  * Its DOSSIER card slides out from behind it with a tilt that settles, the rows appear one after another, a paper sound; it slides
    //    back when the pointer leaves (the card itself counts as part of its poster); never both at once.
    // Hooks in UI/OpenZMainMenu.Faction.cs: BuildFaction (the group, the posters' places, the VS), BuildFactionPoster (the group as the
    // parent), EnterFaction (OpenFactionDeck), CloseFactionRig (CloseFactionDeck), TickFaction (the card as part of the poster, the glow's
    // place in the scaled group, the live print materials, TickFactionDeck).
    // k83: the card is on before its words are made and measured (the long rows break after their label, as the mock-up), the VS halo
    // is drawn with the poster shader's halo mode (UI/OpenZMainMenu.FactionLook.cs, UI/OzFactionLook.cs).
    // ============================================================================================================================
    public partial class OpenZMainMenu
    {
        sealed class FcDossier
        {
            public int side; public RectTransform root; public RawImage art; public Image shadow; public CanvasGroup cg;
            public Rect rest; public Vector2 basePos;
            public OzFactionDeck.CardState state; public float since, k0, tilt0, slide, tilt, rowsAt0 = 1f; public bool content;
            public RectTransform contentRt; public CanvasGroup headCg; public readonly CanvasGroup[] rowCg = new CanvasGroup[OzFactionDeck.Rows];
            public readonly RectTransform[] rowRt = new RectTransform[OzFactionDeck.Rows]; public readonly float[] rowX = new float[OzFactionDeck.Rows];
            public readonly string[] rowText = new string[OzFactionDeck.Rows];
        }

        readonly FcDossier[] fcDos = new FcDossier[2];
        RectTransform fcGroup, fcVs; RawImage fcVsSign, fcVsHalo; Material fcVsMat; float fcVsK;
        OzFactionDeck.Layout fcLayout;
        OzFactionDeckAssets fcDeck;
        readonly Material[] fcLive = new Material[4];                                                          // [side * 2 + layer]: the live back plate / mid layer
        readonly float[] fcLiveK = new float[2], fcLiveVel = new float[2], fcLiveSet = new float[2];
        float? fcProofCardT;                                                                                   // the proof: the card's clock held at this time
        static Texture2D fcDotsTex;

        /// <summary>Checks: the paper sounds (out / back), frames with both cards out (must stay 0), cards built.</summary>
        public static int FactionPaperIn { get; private set; }
        public static int FactionPaperOut { get; private set; }
        public static int FactionBothCards { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFactionDeckChecks() { FactionPaperIn = FactionPaperOut = FactionBothCards = 0; }

        // ---------------------------------------------------------------- build (BuildFaction, before the posters: the cards lie behind them)
        void BuildFactionGroup()
        {
            fcLayout = OzFactionDeck.Group(CW, OzFactionDeck.VsSign);
            fcGroup = Hud.Node("Group", fcRoot); fcGroup.Fill();
            fcGroup.pivot = new Vector2(fcLayout.pivot.x / Mathf.Max(1f, CW), 1f - fcLayout.pivot.y / OzLayout.RefH);   // the group scales about the posters' top middle
            fcGroup.localScale = new Vector3(fcLayout.scale, fcLayout.scale, 1f);
            for (int i = 0; i < 2; i++)
            {
                var d = new FcDossier { side = i, rest = fcLayout.Card(i) };
                d.root = Hud.Node(i == OzFaction.Red ? "Dossier_Zod" : "Dossier_EvilZod", fcGroup).Place(new OzRect(d.rest.x, d.rest.y, d.rest.width, d.rest.height));
                d.root.pivot = new Vector2(0.5f, 0.5f); d.root.anchoredPosition += new Vector2(d.rest.width * 0.5f, -d.rest.height * 0.5f);   // turns about its middle
                d.basePos = d.root.anchoredPosition;
                d.cg = d.root.gameObject.AddComponent<CanvasGroup>(); d.cg.blocksRaycasts = false; d.cg.interactable = false;
                var art = fcLayout.Art(i);
                d.shadow = Hud.Kit(d.root, "Shadow", "Select/card_glow", new OzRect(-12f + 7f, -12f + 10f, d.rest.width + 24f, d.rest.height + 24f), new Color(0f, 0f, 0f, 0.55f), Image.Type.Sliced, true);
                if (OzKit.GetSprite("Select/card_glow") == null) d.shadow.enabled = false;
                d.art = Raw(d.root, "Card"); d.art.rectTransform.Place(new OzRect(art.x - d.rest.x, art.y - d.rest.y, art.width, art.height));
                d.root.gameObject.SetActive(false);
                fcDos[i] = d;
                int idx = i;
                AddHot(d.root, View.Faction, () => PickFaction(idx, "click"), null, null, "");                     // the card is part of its poster: a click on it picks
            }
        }

        /// <summary>BuildFaction, after the posters: the VS sign between them (its halo behind it).</summary>
        void BuildFactionVs()
        {
            var r = fcLayout.vs; int v = OzFactionDeck.VsSign == OzFactionDeck.VsGlow ? 1 : 0;
            fcVs = Hud.Node("VS", fcGroup).Place(new OzRect(r.x, r.y, r.width, r.height));
            fcVs.pivot = new Vector2(0.5f, 0.5f); fcVs.anchoredPosition += new Vector2(r.width * 0.5f, -r.height * 0.5f);
            float hm = OzFactionDeck.HaloMarginPx * r.width / OzFactionDeck.VsPx[v].x;
            fcVsHalo = Raw(fcVs, "Halo"); fcVsHalo.rectTransform.offsetMin = new Vector2(-hm, -hm); fcVsHalo.rectTransform.offsetMax = new Vector2(hm, hm);
            fcVsSign = Raw(fcVs, "Sign");
            fcVsHalo.enabled = fcVsSign.enabled = false; fcVsK = 0f;
        }

        // ---------------------------------------------------------------- open / close (with the posters' rig)
        void OpenFactionDeck()
        {
            fcDeck = OzFactionDeckAssets.Open(transform, OzFactionDeck.VsSign);
            for (int i = 0; i < 2; i++)
            {
                fcLiveK[i] = fcLiveVel[i] = 0f; fcLiveSet[i] = -1f;
                var d = fcDos[i]; if (d == null) continue;
                d.state = OzFactionDeck.CardState.Hidden; d.slide = 0f; d.since = OzMotion.Now;
                if (d.root) d.root.gameObject.SetActive(false);
            }
            fcVsK = 0f;
        }

        void CloseFactionDeck(bool sweep)
        {
            for (int i = 0; i < 2; i++)
            {
                var p = fc[i];
                if (p != null) { if (p.back && p.back.material == fcLive[i * 2]) p.back.material = null; if (p.mid && p.mid.material == fcLive[i * 2 + 1]) p.mid.material = null; }
                var d = fcDos[i]; if (d == null) continue;
                if (d.contentRt) Destroy(d.contentRt.gameObject);
                d.contentRt = null; d.content = false;
                if (d.art) { d.art.texture = null; d.art.enabled = false; }
                d.state = OzFactionDeck.CardState.Hidden; d.slide = 0f;
                if (d.root) d.root.gameObject.SetActive(false);
            }
            for (int k = 0; k < fcLive.Length; k++) if (fcLive[k]) { Destroy(fcLive[k]); fcLive[k] = null; }
            if (fcVsSign) { fcVsSign.texture = null; fcVsSign.material = null; fcVsSign.enabled = false; }
            if (fcVsHalo) { fcVsHalo.texture = null; fcVsHalo.enabled = false; }
            FcHaloLookClose();                                                                                 // k83: the halo's material goes with the screen
            if (fcVsMat) { Destroy(fcVsMat); fcVsMat = null; }
            if (fcDeck) { fcDeck.Close(); fcDeck = null; }
        }

        // ---------------------------------------------------------------- every frame (TickFaction)
        /// <summary>The DOSSIER card counts as part of its poster while it is out (the pointer may move onto it).</summary>
        bool FcCardHit(int i, Vector2 ptr)
        {
            var d = fcDos[i];
            return d != null && d.root && d.root.gameObject.activeInHierarchy && d.state != OzFactionDeck.CardState.Hidden && d.state != OzFactionDeck.CardState.Out && d.slide > 0.5f
                   && RectTransformUtility.RectangleContainsScreenPoint(d.root, ptr, null);
        }

        /// <summary>A poster's live print layer material (layer 0 the back plate, 1 the mid layer): while the poster is alive, the print material
        /// with the motion (once its motion map is in); at rest the print's own material, exactly as before.</summary>
        Material FcLiveMat(int side, int layer, Material print)
        {
            int k = side * 2 + layer;
            if (fcLiveK[side] <= 0f) return print;
            if (fcLive[k]) return fcLive[k];
            if (!print || !fcDeck || !fcDeck.Ready || !fcDeck.Motion[side]) return print;
            var m = new Material(print) { name = "OzPosterFigure (faction live " + side + "." + layer + ")", hideFlags = HideFlags.DontSave };
            OzFactionDeck.SetMotion(m, OzFactionDeck.MotionOf(side, layer), fcDeck.Motion[side]);
            fcLive[k] = m;
            fcLiveSet[side] = -1f;
            return m;
        }

        /// <summary>The screen's colour behind a poster goes to its live layers too (their aged paper, as the print's).</summary>
        void FcLivePaper(int side, Color behind)
        {
            for (int l = 0; l < 2; l++) { var m = fcLive[side * 2 + l]; if (m) m.SetColor(FcPaperBehind, behind); }
        }

        void TickFactionDeck(float dt, int active)
        {
            if (!fcGroup) return;
            float now = OzMotion.Now; bool calm = OzMotion.Reduced;
            bool ready = fcDeck && fcDeck.Ready && view == View.Faction;
            TickFactionVs(now, dt, calm, ready);
            // the live prints: the poster in focus (or picked) comes alive, eased; the others are as before
            for (int i = 0; i < 2; i++)
            {
                fcLiveK[i] = OzFaction.Damp(fcLiveK[i], active == i ? 1f : 0f, ref fcLiveVel[i], OzFactionDeck.LiveTime, dt);
                if (active != i && fcLiveK[i] < 0.002f) { fcLiveK[i] = 0f; fcLiveVel[i] = 0f; }
                float lk = fcLiveK[i] * (calm ? OzFactionDeck.CalmLive : 1f);
                if (lk <= 0f && fcLiveSet[i] == 0f) continue;                                                     // at rest: nothing to set
                var pm = fcRig ? fcRig.Print[i] : null;
                var sweep = OzFactionDeck.PaperSweep(now, i * 1.9f, lk);
                for (int l = 0; l < 3; l++)
                {
                    var m = l < 2 ? fcLive[i * 2 + l] : pm;
                    if (!m) continue;
                    m.SetFloat(OzFactionDeck.IdLive, lk); m.SetFloat(OzFactionDeck.IdLiveT, now); m.SetVector(OzFactionDeck.IdSweep, sweep);
                    if (l == 2) m.SetColor(OzFactionDeck.IdSweepTone, OzFactionDeck.PaperSweepTone);
                }
                fcLiveSet[i] = lk;
            }
            // the DOSSIER cards: one at a time
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < 2; i++)
                {
                    var d = fcDos[i]; if (d == null) continue;
                    bool want = ready && active == i;
                    if ((pass == 0) == want) continue;                                                            // the one going back first
                    var other = fcDos[1 - i];
                    bool otherOut = other != null && other.state != OzFactionDeck.CardState.Hidden;
                    var was = d.state;
                    var s = OzFactionDeck.Step(d.state, want, otherOut, now - d.since, out bool started, out bool turned);
                    if (started)
                    {
                        if (!d.content) FcBuildContent(d);
                        if (!d.content) s = OzFactionDeck.CardState.Hidden;
                        else
                        {
                            d.since = now - (was == OzFactionDeck.CardState.Out ? OzFactionDeck.SlideInTime(d.slide) : 0f);   // turned again on its way back: on from where it is
                            d.root.gameObject.SetActive(true); Sfx(OzFactionDeck.SoundIn); FactionPaperIn++;
                        }
                    }
                    else if (turned)
                    {
                        d.k0 = Mathf.Max(0f, d.slide); d.tilt0 = d.tilt; d.rowsAt0 = d.headCg ? d.headCg.alpha : 1f; d.since = now;
                        if (d.k0 > 0.3f) { Sfx(OzFactionDeck.SoundOut); FactionPaperOut++; }
                    }
                    else if (s != was) d.since = now;
                    d.state = s;
                    FcPose(d, now, calm);
                }
            if (fcDos[0] != null && fcDos[1] != null && fcDos[0].state != OzFactionDeck.CardState.Hidden && fcDos[1].state != OzFactionDeck.CardState.Hidden) FactionBothCards++;
        }

        void FcPose(FcDossier d, float now, bool calm)
        {
            if (!d.root) return;
            if (d.state == OzFactionDeck.CardState.Hidden) { if (d.root.gameObject.activeSelf) d.root.gameObject.SetActive(false); d.slide = 0f; return; }
            float t = fcProofCardT ?? (now - d.since);
            float rest = OzFactionDeck.RestTilt[d.side], sign = d.side == OzFaction.Red ? 1f : -1f;
            float slide, tilt, alpha = 1f, head, rowsOut = 1f;
            bool coming = d.state == OzFactionDeck.CardState.In || d.state == OzFactionDeck.CardState.Shown;
            if (calm)
            {
                slide = 1f; tilt = rest;
                alpha = d.state == OzFactionDeck.CardState.Shown && !fcProofCardT.HasValue ? 1f : coming ? Mathf.Clamp01(t / OzFactionDeck.CalmFade) : d.k0 * (1f - Mathf.Clamp01(t / OzFactionDeck.CalmFade));
                head = 1f;
            }
            else if (d.state == OzFactionDeck.CardState.Out)
            {
                float u = Mathf.Clamp01(t / OzFactionDeck.CardOut);
                slide = OzFactionDeck.SlideOut(d.k0, t);
                tilt = Mathf.Lerp(d.tilt0, rest + sign * OzFactionDeck.TiltIn * 0.6f, OzEase.InCubic(u));
                head = d.rowsAt0 * (1f - Mathf.Clamp01(u * 1.8f)); rowsOut = head;
            }
            else
            {
                bool shown = d.state == OzFactionDeck.CardState.Shown && !fcProofCardT.HasValue;
                slide = shown ? 1f : OzFactionDeck.SlideIn(t);
                tilt = shown ? rest : OzFactionDeck.TiltAt(d.side, t);
                head = shown ? 1f : OzFactionDeck.RowIn(-1, t);
            }
            d.slide = slide; d.tilt = tilt;
            if (!d.root.gameObject.activeSelf) d.root.gameObject.SetActive(true);
            float x = OzFactionDeck.CardX(d.side, d.rest, slide) - d.rest.x;
            d.root.anchoredPosition = d.basePos + new Vector2(x, 0f);
            d.root.localRotation = Quaternion.Euler(0f, 0f, tilt);
            if (!Mathf.Approximately(d.cg.alpha, alpha)) d.cg.alpha = alpha;
            if (d.art && fcDeck && fcDeck.Card[d.side]) { if (d.art.texture != fcDeck.Card[d.side]) d.art.texture = fcDeck.Card[d.side]; d.art.enabled = true; if (d.art.color != Color.white) d.art.color = Color.white; }
            if (!d.content) return;
            if (d.headCg) d.headCg.alpha = head;
            for (int r = 0; r < OzFactionDeck.Rows; r++)
            {
                float a = calm ? 1f : d.state == OzFactionDeck.CardState.Out ? rowsOut : (d.state == OzFactionDeck.CardState.Shown && !fcProofCardT.HasValue) ? 1f : OzFactionDeck.RowIn(r, t);
                if (d.rowCg[r]) d.rowCg[r].alpha = a;
                if (d.rowRt[r]) d.rowRt[r].anchoredPosition = new Vector2(d.rowX[r] - (1f - a) * OzFactionDeck.RowSlide * (d.state == OzFactionDeck.CardState.Out || calm ? 0f : 1f), d.rowRt[r].anchoredPosition.y);
            }
        }

        // ---------------------------------------------------------------- the VS sign
        void TickFactionVs(float now, float dt, bool calm, bool ready)
        {
            if (!fcVs || !fcVsSign) return;
            if (ready && !fcVsMat && fcRig && fcRig.Figure && fcDeck.Vs)
            {
                fcVsMat = new Material(fcRig.Figure) { name = "OzPosterFigure (faction VS)", hideFlags = HideFlags.DontSave };
                fcVsMat.SetFloat("_Print", 2f); fcVsMat.SetFloat("_RimPx", 0f);
                var px = OzFactionDeck.VsPx[fcDeck.VsSign];
                fcVsMat.SetVector(OzFactionDeck.IdLayerPx, new Vector4(px.x, px.y, 0f, 0f));
                fcVsMat.SetColor(OzFactionDeck.IdSweepTone, new Color(1f, 0.97f, 0.9f, 1f));
            }
            bool on = ready && fcVsMat && fcDeck.Vs;
            if (on)
            {
                if (fcVsSign.texture != fcDeck.Vs) fcVsSign.texture = fcDeck.Vs;
                if (fcVsSign.material != fcVsMat) fcVsSign.material = fcVsMat;
                if (fcDeck.VsHalo && fcVsHalo.texture != fcDeck.VsHalo) fcVsHalo.texture = fcDeck.VsHalo;
            }
            fcVsK = Mathf.MoveTowards(fcVsK, on ? 1f : 0f, dt / Mathf.Max(0.01f, OzMotion.Dur(0.35f, true)));
            float k = OzEase.OutCubic(fcVsK);
            bool vis = k > 0.001f && on;
            if (fcVsSign.enabled != vis) fcVsSign.enabled = vis;
            bool hv = vis && fcVsHalo.texture;
            if (fcVsHalo.enabled != hv) fcVsHalo.enabled = hv;
            if (!vis) return;
            float s = OzFactionDeck.VsScale(now, calm);
            fcVs.localScale = new Vector3(s, s, 1f);
            fcVsSign.color = new Color(1f, 1f, 1f, k);
            fcVsHalo.color = new Color(1f, 1f, 1f, k * OzFactionDeck.HaloAlpha(now, calm));
            FcHaloLook();                                                                                      // k83: the halo mixed as drawn (UI/OpenZMainMenu.FactionLook.cs)
            fcVsMat.SetVector(OzFactionDeck.IdSweep, OzFactionDeck.VsSweep(now, calm));
        }

        // ---------------------------------------------------------------- the card's words (made when it first comes out on each opening)
        void FcBuildContent(FcDossier d)
        {
            if (d.content || !fcDeck || !fcDeck.Ready || !d.root) return;
            FcWake(d.root);                                                                                    // k83: on before its words are made (a text made on a card that is off measured at a tenth)
            bool tmp = fcDeck.BodyTmp && fcDeck.HeadingTmp;
            if (!tmp && (!fcDeck.Body || !fcDeck.Heading)) return;
            int s = d.side; float W = d.rest.width, H = d.rest.height;
            Color ink = OzFactionDeck.Ink(s);
            d.contentRt = Hud.Node("Words", d.root).Place(new OzRect(0f, 0f, W, H));
            // the heading: "DOSSIER //" and the general's name, the rule under them
            var head = Hud.Node("Heading", d.contentRt).Place(new OzRect(0f, 0f, W, H));
            d.headCg = head.gameObject.AddComponent<CanvasGroup>();
            FcLabel(head, "Dossier", OzFactionDeck.DossierWord, tmp ? (fcDeck.BoldTmp ? fcDeck.BoldTmp : fcDeck.BodyTmp) : null, fcDeck.Bold ? fcDeck.Bold : fcDeck.Body, OzFactionDeck.DossierSize, OzFactionDeck.DossierInk,
                    new Rect(OzFactionDeck.DossierX * W, OzFactionDeck.DossierY * H, W * 0.6f, OzFactionDeck.CapBody), false, OzFactionDeck.DossierTracking);
            float hw = (OzFactionDeck.HeadingRight - OzFactionDeck.DossierX) * W;
            var name = FcLabel(head, "General", OzFaction.General[s], tmp ? fcDeck.HeadingTmp : null, fcDeck.Heading, OzFactionDeck.HeadingSize, ink,
                               new Rect(OzFactionDeck.DossierX * W, OzFactionDeck.HeadingY[s] * H, hw, OzFactionDeck.CapHeading), false, OzFactionDeck.HeadingTracking);
            FcFit(name, OzFaction.General[s], OzFactionDeck.HeadingSize, hw);
            var rule = Hud.Img(head, "Rule", ink); rule.rectTransform.Place(new OzRect(OzFactionDeck.RuleX0 * W, OzFactionDeck.RuleY * H, (OzFactionDeck.RuleX1 - OzFactionDeck.RuleX0) * W, OzFactionDeck.RuleH));
            // the rows: icon, typewriter words (the value in bold; on two lines when one is too wide), a thin dotted line between rows
            float y = OzFactionDeck.RowsY * H, tx = OzFactionDeck.RowTextX * W, tw = (OzFactionDeck.RowTextRight - OzFactionDeck.RowTextX) * W;
            for (int r = 0; r < OzFactionDeck.Rows; r++)
            {
                var row = Hud.Node("Row" + (r + 1), d.contentRt).Place(new OzRect(0f, y, W, 10f));
                d.rowCg[r] = row.gameObject.AddComponent<CanvasGroup>();
                string one = OzFactionDeck.RowText(s, r, false);
                var text = FcLabel(row, "Words", one, tmp ? fcDeck.BodyTmp : null, fcDeck.Body, OzFactionDeck.RowTextSize, OzFactionDeck.RowInk, new Rect(tx, 0f, tw, 10f), true, 0f);
                bool two = FcWidth(text, one) > tw;
                string words = two ? OzFactionDeck.RowText(s, r, true) : one;
                FcSet(text, words);
                float th = FcHeight(text, words, tw);
                float rh = Mathf.Max(OzFactionDeck.RowMinH, Mathf.Max(OzFactionDeck.RowIconSize, th) + 2f * OzFactionDeck.RowPad);
                row.sizeDelta = new Vector2(W, rh);
                ((RectTransform)text.transform).Place(new OzRect(tx, 0f, tw, rh));
                var icon = Raw(row, "Icon"); icon.texture = fcDeck.Icons; icon.uvRect = OzFactionDeck.IconUv(OzFactionDeck.Icon[s][r]); icon.color = ink;
                icon.rectTransform.Place(new OzRect(OzFactionDeck.RowIconX * W - OzFactionDeck.RowIconSize * 0.5f, (rh - OzFactionDeck.RowIconSize) * 0.5f, OzFactionDeck.RowIconSize, OzFactionDeck.RowIconSize));
                icon.enabled = fcDeck.Icons;
                if (r < OzFactionDeck.Rows - 1)
                {
                    var line = Raw(row, "Line"); line.texture = FcDots(); line.color = (Color)OzFactionDeck.RowLineInk;
                    float lw = (OzFactionDeck.RowLineX1 - OzFactionDeck.RowLineX0) * W;
                    line.rectTransform.Place(new OzRect(OzFactionDeck.RowLineX0 * W, rh - OzFactionDeck.RowLineH * 0.5f, lw, OzFactionDeck.RowLineH));
                    line.uvRect = new Rect(0f, 0f, lw / OzFactionDeck.RowDot, 1f);                                  // a dot every RowDot u
                }
                d.rowRt[r] = row; d.rowX[r] = row.anchoredPosition.x; d.rowText[r] = words;
                y += rh;
            }
            d.content = true;
        }

        /// <summary>A card label: TextMeshPro with the card's font asset, or plain text with the font itself. A one-line label: r.y is its
        /// cap top and r.height the font's cap height (share of the size); a wrapping one fills r (centred in its height).</summary>
        static Graphic FcLabel(RectTransform parent, string name, string text, TMP_FontAsset fa, Font font, float size, Color color, Rect r, bool wrap, float tracking)
        {
            var rt = Hud.Node(name, parent);
            float bh = size * 1.8f;
            if (wrap) rt.Place(new OzRect(r.x, r.y, r.width, r.height));
            else if (fa) rt.Place(new OzRect(r.x, r.y + r.height * size * 0.5f - bh * 0.5f, r.width, bh));                    // the caps centred on the box (Capline)
            else rt.Place(new OzRect(r.x, r.y - 0.3f * size, r.width, bh));                                                    // plain text: its top is the font's ascent
            if (fa)
            {
                var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
                FcUiText(t);                                                                                   // k83: a UI text from its first measure
                t.font = fa; t.fontSharedMaterial = fa.material; t.fontSize = size; t.color = color; t.richText = true; t.raycastTarget = false;
                t.characterSpacing = tracking * 100f; t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                t.overflowMode = TextOverflowModes.Overflow; t.alignment = wrap ? TextAlignmentOptions.Left : TextAlignmentOptions.CaplineLeft;
                t.text = text;
                return t;
            }
            var l = rt.gameObject.AddComponent<Text>();
            l.font = font; l.fontSize = Mathf.Max(1, Mathf.RoundToInt(size)); l.color = color; l.supportRichText = true; l.raycastTarget = false;
            l.alignment = wrap ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft; l.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            l.verticalOverflow = VerticalWrapMode.Overflow; l.text = text;
            return l;
        }

        static void FcSet(Graphic g, string s) { if (g is TMP_Text t) t.text = s; else if (g is Text l) l.text = s; }
        static float FcWidth(Graphic g, string s)
        {
            if (g is TMP_Text t) return t.GetPreferredValues(s, 99999f, 0f).x;
            if (g is Text l) { var st = l.GetGenerationSettings(Vector2.zero); return l.cachedTextGeneratorForLayout.GetPreferredWidth(s, st) / Mathf.Max(0.01f, l.pixelsPerUnit); }
            return 0f;
        }
        static float FcHeight(Graphic g, string s, float w)
        {
            if (g is TMP_Text t) return t.GetPreferredValues(s, w, 0f).y;
            if (g is Text l) { var st = l.GetGenerationSettings(new Vector2(w, 0f)); return l.cachedTextGeneratorForLayout.GetPreferredHeight(s, st) / Mathf.Max(0.01f, l.pixelsPerUnit); }
            return 0f;
        }
        /// <summary>The general's name keeps to its line (a smaller size when it would be wider).</summary>
        static void FcFit(Graphic g, string s, float size, float w)
        {
            float pw = FcWidth(g, s);
            if (pw <= w || pw <= 0f) return;
            float k = Mathf.Max(0.6f, w / pw * 0.98f);
            if (g is TMP_Text t) t.fontSize = size * k; else if (g is Text l) l.fontSize = Mathf.Max(1, Mathf.RoundToInt(size * k));
        }

        /// <summary>The rows' dotted line: a dot and a gap (white; the line's colour tints it), repeated across.</summary>
        static Texture2D FcDots()
        {
            if (fcDotsTex) return fcDotsTex;
            var t = new Texture2D(8, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "OzFaction_DossierDots" };
            var px = new Color32[16];
            for (int y = 0; y < 2; y++) for (int x = 0; x < 8; x++) px[y * 8 + x] = new Color32(255, 255, 255, (byte)(x < 4 ? 255 : x == 4 || x == 7 ? 70 : 0));
            t.SetPixels32(px); t.Apply(false, true);
            fcDotsTex = t;
            return t;
        }

        // ---------------------------------------------------------------- proof hooks (View/OzFactionDeckProof.cs, Play only)
        public OzFactionDeckAssets ProofDeck => fcDeck;
        public bool ProofDeckReady => fcDeck && fcDeck.Ready && fcVsMat;
        public RectTransform ProofDossier(int i) => fcDos[i] != null ? fcDos[i].root : null;
        public string ProofDossierState(int i) => fcDos[i] != null ? fcDos[i].state.ToString() : "";
        public float ProofDossierSlide(int i) => fcDos[i] != null ? fcDos[i].slide : 0f;
        public float ProofDossierTilt(int i) => fcDos[i] != null ? fcDos[i].tilt : 0f;
        public float ProofDossierRowAlpha(int i, int r) => fcDos[i] != null && fcDos[i].rowCg[r] ? fcDos[i].rowCg[r].alpha : 0f;
        public string ProofDossierRow(int i, int r) => fcDos[i] != null ? fcDos[i].rowText[r] ?? "" : "";
        public Graphic ProofDossierLabel(int i, string name)
        {
            var d = fcDos[i]; if (d == null || !d.contentRt) return null;
            foreach (var g in d.contentRt.GetComponentsInChildren<Graphic>(true)) if (g.name == name) return g;
            return null;
        }
        /// <summary>The card's animation clock held at t s (null: its own clock again); the pictures of the card coming out.</summary>
        public void ProofDossierClock(float? t) => fcProofCardT = t;
        public RectTransform ProofVs => fcVs;
        public RawImage ProofVsSign => fcVsSign;
        public RawImage ProofVsHalo => fcVsHalo;
        public Material ProofVsMat => fcVsMat;
        public float ProofLive(int i) => fcLiveK[i];
        public Material ProofLiveMat(int i, int layer) => fcLive[i * 2 + layer];
        public OzFactionDeck.Layout ProofLayout => fcLayout;
        public RectTransform ProofGroup => fcGroup;
    }
}
