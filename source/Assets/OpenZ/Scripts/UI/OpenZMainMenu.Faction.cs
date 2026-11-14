using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    // ============================================================================================================================
    // k75a (1.2 owner items 77, 78, 78b, 81, 88, 89, 90): THE FACTION SCREEN, between the main menu's PLAY and the level selection. It
    // replaces the level selection's PLAY AS BLUE box: the side picked here is that box's own setting (OzFaction.Choose ->
    // OzPlayBlue.Box), so DEPLOY plays the levels red or blue exactly as the box did.
    //   [SELECT FACTION / CHOOSE YOUR GENERAL ......................................... BACK (ESC)]
    //   [poster GENERAL ZOD / ZODCORP]          [poster GENERAL EVILZOD / EVIL TAKEOVER INDUSTRIES]
    //   [RED FORT · THE CPU COMMANDS BLUE]      [BLUE FORT · THE CPU COMMANDS RED]   (a TIP plate in its place on hover / focus)
    //  * Each poster is the owner's print in layers (back plate, mid layer, frame border, the REAL 3D characters rendered live by the
    //    poster's own camera, frame band with the titles drawn by the game, paper grain): View/OzFactionPosters.cs loads and renders them
    //    only while this screen is up. The camera sways on a slow loop and follows the pointer over its poster; the back plate and the
    //    mid layer slide with it for depth (OzFaction).
    //  * Mood (item 90): a soft red glow round Zod's poster as the pointer goes toward it, a blue one round EvilZod's, eased; keys / D-pad /
    //    finger: the focused one. The print's light paper is aged and takes a share of the screen's colour behind it (k75a2).
    //  * Hover tip (item 81): "TIP: Wise choice!" / "TIP: You chose... poorly." under the poster pointed at (or focused).
    //  * The pick (click, ENTER, the gamepad's A; a finger: the first tap shows the tip, the second picks): the stamp sound, the poster pops,
    //    Zod says the joke (item 78b) on his voice bus with its words on the plate, then the level selection of that side opens.
    //    'Zod exact' (the box was not offered there either): the EvilZod poster is locked (ui_disabled).
    //  * Keys: LEFT / RIGHT (A / D) focus a poster, ENTER picks, ESC / BACK to the main menu; the level selection's BACK and ESC come back
    //    here. A gamepad and a finger drive the same pointer and keys (OzTouch).
    // Hooks in OpenZMainMenu.cs: View.Faction, PLAY, Build, ApplyView, Update, Keys, TickViews, GoSelect / GoMain from here, the level
    // selection's BACK (BackFromSelect). UI/OpenZMainMenu.PlayBlue.cs: the level selection's tag line names the side picked here.
    // k75a3 (items 101 / 102): the posters stand apart with the VS sign between them, a poster's print comes alive while it is in focus
    // and its DOSSIER card slides out beside it: UI/OpenZMainMenu.FactionDeck.cs (the group, scaled to fit, and its hooks below).
    // k75a4 (items 104, 108, 112, 113, 115): each render draws with its own material (Zod's glasses' glint, EvilZod's eyes' glow, his
    // render a tiny bit bigger, his cape down to the band), the posters' shadow under the cards, a row tick: UI/OpenZMainMenu.FactionPolish.cs.
    // ============================================================================================================================
    public partial class OpenZMainMenu
    {
        sealed class FcPoster
        {
            public int side; public float x, y, w, h;
            public RectTransform rt, print, tip, lockChip; public CanvasGroup printCg, tipCg;
            public RawImage back, mid, border, figure, band, grain;
            public OzText general, company, tipText, sideLine;
            public Image glow, shadow, plate, dim, lockShade;
            public float focusK, dimK, tipK, readyK, figK, hover, hoverVel; public Vector2 follow, followVel; public string tipShown = "";
        }

        readonly FcPoster[] fc = new FcPoster[2];
        RectTransform fcRoot; CanvasGroup fcCg; Image fcBed, fcWashUnder, fcWashOver; RawImage fcWashSide; HudButton fcBack;
        OzFactionPosters fcRig;
        int fcFocus = -1, fcHover = -1, fcTap = -1, fcPicked = -1, fcGlowSide = -1; float fcPickAt = -10f, fcPickLen, fcPickHold, fcMood, fcMoodVel; Vector2? fcProofPointer;   // 1.3 item 6: fcPickHold = the hold of this pick (0 once the line was heard)
        AudioSource fcVoice;
        static readonly int FcRimPx = Shader.PropertyToID("_RimPx");
        static readonly Vector3[] fcCorners = new Vector3[4];
        static readonly int FcPaperBehind = Shader.PropertyToID("_PaperBehind");                       // k75a2: the screen's colour behind each poster (its aged paper)
        static Texture2D fcGlowTex;                                                                     // k75a2: the mood's soft round glow (made once)

        /// <summary>Checks: the faction screen opened (PLAY or the level selection's BACK), BACKs from the level selection, Zod's lines played,
        /// locked picks, the last line's clip and volume.</summary>
        public static int FactionOpens { get; private set; }
        public static int FactionBacks { get; private set; }
        public static int FactionVoices { get; private set; }
        public static int FactionLocked { get; private set; }
        public static string FactionClip { get; private set; } = "";
        public static float FactionVolume { get; private set; }
        /// <summary>1.3 item 6, checks: picks that set a hold, and the last pick's hold (s; 0 = straight on).</summary>
        public static int FactionPickHolds { get; private set; }
        public static float FactionLastHold { get; private set; } = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFactionChecks() { FactionOpens = FactionBacks = FactionVoices = FactionLocked = FactionPickHolds = 0; FactionClip = ""; FactionVolume = 0f; FactionLastHold = -1f; }

        // ---------------------------------------------------------------- build (Build: after the level selection)
        void BuildFaction()
        {
            fcRoot = Hud.Node("Faction", root); fcRoot.Fill();
            fcCg = fcRoot.gameObject.AddComponent<CanvasGroup>(); fcCg.alpha = 0f;
            fcBed = Hud.Img(fcRoot, "Bed", OzUi.A(OzUi.GM950, 0.55f)); fcBed.rectTransform.Fill();          // over the level selection's blurred flyby
            fcWashUnder = Hud.Img(fcRoot, "MoodUnder", Color.clear); fcWashUnder.rectTransform.Fill();
            fcWashSide = Raw(fcRoot, "MoodGlow"); fcWashSide.texture = FcGlowTexture(); fcWashSide.color = Color.clear; fcGlowSide = -1;   // k75a2: the soft round glow behind the poster in mind (TickFaction places it)
            Hud.Label(fcRoot, "Eyebrow", OzFaction.Eyebrow, 12, OzUi.Amber, 0.3f).At(OzMenuLayout.SelEyebrowX, OzMenuLayout.SelEyebrowY);
            Hud.Label(fcRoot, "Title", OzFaction.Title, OzMenuLayout.SelTitleSize, OzUi.Ink, -0.005f).At(OzMenuLayout.SelTitleX, OzMenuLayout.SelTitleY, OzH.L, false, 1100);
            var br = OzMenuLayout.Back;
            fcBack = HudButton.Make(fcRoot, "Back", "BACK", new Vector2(br.w, br.h), HudButtonKind.Large, 13, 0.16f, HudButtonStyle.Normal, true, OzH.L);
            fcBack.Rt.Place(br);
            Hud.Keycap(fcBack.Content, "Esc", "ESC", br.w - 10, (br.h - 14) * 0.5f);
            AddHot(fcBack.Rt, View.Faction, GoMain, null, fcBack, "ui_menu_back");
            Hud.Label(fcRoot, "Keys", "← →  FACTION     ENTER  CHOOSE     ESC  BACK", 9, OzUi.Muted, 0.2f, OzStyle.Plain)
                .At(XR(OzMenuLayout.SelHintRX), OzMenuLayout.SelHintY, OzH.R, false, 1200);
            BuildFactionGroup();                                                                               // k75a3: the group (the DOSSIER cards first: they lie behind the posters), scaled to fit
            for (int i = 0; i < 2; i++) { var pr = fcLayout.Poster(i); fc[i] = BuildFactionPoster(i, pr.x, pr.y, pr.width, pr.height); }   // k75a3: the posters apart (OzFactionDeck.Spread)
            BuildFactionVs();                                                                                  // k75a3: the VS sign between them
            BuildFactionPolish();                                                                              // k75a4: the posters' shadow under the cards, their shade on them
            fcWashOver = Hud.Img(fcRoot, "MoodOver", Color.clear); fcWashOver.rectTransform.Fill();          // the lighter wash over the posters too
            fcRoot.gameObject.SetActive(false);
        }

        FcPoster BuildFactionPoster(int i, float x, float y, float w, float h)
        {
            var p = new FcPoster { side = i, x = x, y = y, w = w, h = h };
            string name = i == OzFaction.Red ? "Zod" : "EvilZod";
            Color mark = i == OzFaction.Red ? (Color)OzUi.RedHi : (Color)OzUi.BlueHi;
            p.rt = Hud.Node("Poster_" + name, fcGroup ? fcGroup : fcRoot).Place(new OzRect(x, y, w, h));            // k75a3: in the (scaled) group
            p.rt.pivot = new Vector2(0.5f, 0.5f); p.rt.anchoredPosition += new Vector2(w * 0.5f, -h * 0.5f);   // scales about its middle
            float g = OzMenuLayout.CardGlow;
            p.glow = Hud.Kit(p.rt, "Glow", "Select/card_glow", new OzRect(-g, -g, w + 2 * g, h + 2 * g), OzUi.A(OzUi.Ink, 0f), Image.Type.Sliced, false);
            p.glow.color = new Color(mark.r, mark.g, mark.b, 0f);
            if (OzKit.GetSprite("Select/card_glow") == null) p.glow.enabled = false;
            p.shadow = Hud.Img(p.rt, "Shadow", new Color(0f, 0f, 0f, 0.5f)); p.shadow.rectTransform.Place(new OzRect(10, 12, w, h));
            p.plate = Hud.Img(p.rt, "Plate", OzUi.GM900); p.plate.rectTransform.Place(new OzRect(0, 0, w, h));   // while the print loads
            p.print = Hud.Node("Print", p.rt).Place(new OzRect(0, 0, w, h));
            p.printCg = p.print.gameObject.AddComponent<CanvasGroup>();
            p.back = Raw(p.print, "Back"); p.mid = Raw(p.print, "Mid"); p.border = Raw(p.print, "Border");
            p.figure = Raw(p.print, "Figure"); p.band = Raw(p.print, "Band");
            p.general = Hud.Label(p.print, "General", OzFaction.General[i], OzFaction.GeneralSize, OzFaction.GeneralInk[i], 0.04f, OzStyle.Plain)
                .At(w * 0.5f, h * OzFaction.GeneralY, OzH.C, true, w * OzFaction.TitleW);
            p.company = Hud.Label(p.print, "Company", OzFaction.Company[i], OzFaction.CompanySize, OzFaction.CompanyInk[i], 0.3f, OzStyle.Plain)
                .At(w * 0.5f, h * OzFaction.CompanyY, OzH.C, true, w * OzFaction.TitleW);
            p.grain = Raw(p.print, "Grain");
            p.grain.uvRect = new Rect(0f, 0f, OzFaction.GridW / OzFaction.GrainTile, OzFaction.GridH / OzFaction.GrainTile);   // the tile at the layers' own grid
            p.dim = Hud.Img(p.rt, "Dim", Color.clear); p.dim.rectTransform.Place(new OzRect(0, 0, w, h));
            if (i == OzFaction.Blue)
            {
                p.lockShade = Hud.Img(p.rt, "Locked", OzUi.A(OzUi.GM950, 0.62f)); p.lockShade.rectTransform.Place(new OzRect(0, 0, w, h));
                var chip = Hud.Chip(p.rt, "LockedChip", new OzRect(w * 0.5f - 110f, h * 0.5f - 14f, 220f, 28f), OzFaction.LockedChip, OzUi.GM950, OzUi.Ink2, 11, 0.2f, OzUi.A(OzUi.Khaki, 0.35f));
                p.lockChip = chip.bg.transform.parent as RectTransform;
            }
            // the slot under the poster: its side line, or the TIP plate (the pick's words while Zod says them)
            var slot = Hud.Node("TipSlot_" + name, fcGroup ? fcGroup : fcRoot).Place(new OzRect(x, y + h + OzFaction.TipGap, w, OzFaction.TipH));
            p.sideLine = Hud.Label(slot, "Side", OzFaction.SideLine[i], 11, OzUi.Muted, 0.2f, OzStyle.Plain).At(w * 0.5f, OzFaction.TipH * 0.5f, OzH.C, true, w);
            p.tip = Hud.Node("Tip", slot).Place(new OzRect(0, 0, w, OzFaction.TipH));
            Hud.Plate(p.tip, "sm", 0f, mark, null, false, false);
            p.tipCg = p.tip.gameObject.AddComponent<CanvasGroup>(); p.tipCg.alpha = 0f; p.tipCg.blocksRaycasts = false;
            p.tipText = Hud.Label(p.tip, "Text", "", 13, OzUi.Ink, 0.04f, OzStyle.Plain);
            SizeTip(p, OzFaction.Tip(i));
            // a rebuild (a new screen size) while the screen shows: the print and the render are already there
            var rp = fcRig ? fcRig.P[i] : null;
            p.readyK = rp != null && rp.layersReady ? 1f : 0f; p.printCg.alpha = p.readyK;
            int idx = i;
            AddHot(p.rt, View.Faction, () => PickFaction(idx, "click"), null, null, "");                    // "": the screen plays its own hover sound and the pick's stamp
            return p;
        }

        /// <summary>The tip plate fits its words (centred under the poster).</summary>
        void SizeTip(FcPoster p, string words)
        {
            p.tipShown = words;
            p.tipText.Set(words);
            float tw = Mathf.Min(p.w, Mathf.Ceil(p.tipText.WidthAt(words)) + 36f);
            p.tip.Place(new OzRect((p.w - tw) * 0.5f, 0f, tw, OzFaction.TipH));
            p.tipText.At(tw * 0.5f, OzFaction.TipH * 0.5f, OzH.C, true, tw - 16f);
        }

        // ---------------------------------------------------------------- views
        /// <summary>PLAY: the main menu -> the faction screen.</summary>
        void GoFaction()
        {
            if (view != View.Main) return;
            CloseSide();
            view = View.Faction; viewAt = OzMotion.Now; selectFrom = selectK; selectTo = 1f;              // the blurred flyby of the level selection behind it
            EnterFaction("PLAY");
        }

        /// <summary>The level selection's BACK / ESC: back to the faction screen (the side can be changed there).</summary>
        void BackFromSelect()
        {
            if (view != View.Select) return;
            view = View.Faction; viewAt = OzMotion.Now; selectFrom = selectK; selectTo = 1f;
            Sfx("ui_menu_back");
            FactionBacks++;
            EnterFaction("the level selection's BACK");
        }

        void EnterFaction(string from)
        {
            if (!fcRoot) return;
            fcRoot.gameObject.SetActive(true);
            fcPicked = -1; fcTap = -1; fcHover = -1; fcPickLen = 0f;
            fcFocus = OzPlayBlue.Offered() ? OzFaction.Chosen : OzFaction.Red;                               // 'Zod exact': the focus starts on the poster that can be picked
            for (int i = 0; i < 2; i++) if (fc[i] != null) { fc[i].focusK = fc[i].dimK = fc[i].tipK = 0f; fc[i].follow = fc[i].followVel = Vector2.zero; fc[i].hover = fc[i].hoverVel = 0f; }
            fcMood = fcMoodVel = 0f;
            fcRig = OzFactionPosters.Open(transform);
            OpenFactionDeck();                                                                                 // k75a3: the VS sign's, the cards' and the live prints' pictures and fonts
            if (bgLevel >= 0 && previews[bgLevel].VideoOk) previews[bgLevel].Player.isLooping = true;      // no loop-end alternation off the main menu
            FactionOpens++;
            Debug.Log($"[OpenZ Menu] faction screen ({from}): the side picked last {OzFaction.General[OzFaction.Chosen]}{(OzPlayBlue.Offered() ? "" : "; 'Zod exact': the blue side locked")}");
        }

        /// <summary>After a pick: the level selection of that side.</summary>
        void FactionToSelect(string why)
        {
            if (view != View.Faction) return;
            Debug.Log($"[OpenZ Menu] faction screen -> the level selection ({why}): {OzFaction.General[OzFaction.Chosen]}");
            GoSelect();
        }

        /// <summary>ApplyView (the first build and a rebuild): the screen as the view stands.</summary>
        void ApplyFactionView()
        {
            if (!fcRoot) return;
            bool on = view == View.Faction;
            fcRoot.gameObject.SetActive(on); fcCg.alpha = on ? 1f : 0f;
            if (!on && fcRig) CloseFactionRig(false);
        }

        /// <summary>TickViews, every frame: the screen fades in over the main menu / the level selection, or out; once out, the rig closes.</summary>
        void TickFactionView(float kIn, float kOut)
        {
            if (!fcRoot) return;
            if (view == View.Faction)
            {
                if (!fcRoot.gameObject.activeSelf) fcRoot.gameObject.SetActive(true);
                if (!Mathf.Approximately(fcCg.alpha, kIn)) fcCg.alpha = kIn;
                if (mainCg) mainCg.alpha = Mathf.Min(mainCg.alpha, kOut);
                if (selectCg) selectCg.alpha = Mathf.Min(selectCg.alpha, kOut);
                if (kOut <= 0f)
                {
                    if (mainRoot && mainRoot.gameObject.activeSelf) mainRoot.gameObject.SetActive(false);
                    if (selectRoot && selectRoot.gameObject.activeSelf) selectRoot.gameObject.SetActive(false);
                }
            }
            else if (fcRoot.gameObject.activeSelf)
            {
                if (fcCg.alpha > kOut) fcCg.alpha = kOut;
                if (fcRig) fcRig.SetLive(false);
                if (kOut <= 0f) { fcRoot.gameObject.SetActive(false); CloseFactionRig(view == View.Main); }
            }
        }

        /// <summary>The rig goes (sweep: also the unused assets, back on the main menu); the screen's pictures let go of its textures first.</summary>
        void CloseFactionRig(bool sweep)
        {
            CloseFactionDeck(sweep);                                                                           // k75a3: the VS sign, the cards and the live prints let go first
            CloseFactionPolish();                                                                              // k75a4: the renders' own materials go
            for (int i = 0; i < 2; i++)
            {
                var p = fc[i]; if (p == null) continue;
                foreach (var r in new[] { p.back, p.mid, p.border, p.figure, p.band, p.grain }) if (r) { r.texture = null; r.material = null; r.enabled = false; }   // k75a2: + the print materials (they go with the rig)
                if (p.figure) p.figure.material = null;
                p.readyK = p.figK = 0f; if (p.printCg) p.printCg.alpha = 0f;
            }
            if (fcRig) { fcRig.Close(sweep); fcRig = null; }
        }

        // ---------------------------------------------------------------- input
        /// <summary>Keys (and the gamepad's D-pad / A / B through OzTouch): LEFT / RIGHT focus, ENTER picks, ESC back to the main menu;
        /// while Zod says the joke ENTER goes on at once and ESC still leaves.</summary>
        void FactionKeys(bool left, bool right, bool enter, bool esc)
        {
            if (fcPicked >= 0) { if (enter) FactionToSelect("ENTER"); else if (esc) GoMain(); return; }
            if (left || right)
            {
                int to = left ? OzFaction.Red : OzFaction.Blue;
                if (fcFocus == to) Sfx("ui_disabled");                                                   // the end of the row answers (as the level cards)
                else { fcFocus = to; Sfx("ui_hover"); }
            }
            if (enter) PickFaction(fcFocus >= 0 ? fcFocus : fcHover >= 0 ? fcHover : OzFaction.Chosen, "key");
            else if (esc) GoMain();
        }

        /// <summary>A pick (click / tap, ENTER, the gamepad's A). A finger: the first tap on a poster shows its tip, the second picks.</summary>
        void PickFaction(int side, string how)
        {
            if (view != View.Faction || side < 0 || side > 1) return;
            if (fcPicked >= 0) { if (how == "click") FactionToSelect("a click while Zod speaks"); return; }
            if (side == OzFaction.Blue && !OzPlayBlue.Offered()) { FactionLocked++; Sfx("ui_disabled"); return; }
            if (how == "click" && OzTouch.Now == OzTouch.Driver.Touch && fcTap != side) { fcTap = side; fcFocus = side; Sfx("ui_hover"); return; }
            fcPicked = side; fcPickAt = OzMotion.Now; fcFocus = side; fcTap = side;
            OzFaction.Choose(side, how);
            Sfx("ui_stamp");
            bool line = !OzFaction.LineHeard(side);                                                          // 1.3 item 6: Zod's line and its hold only the first time
            fcPickLen = line ? PlayFactionVoice(side) : 0f;
            if (line && fcPickLen > 0f) OzFaction.MarkHeard(side);                                           // remembered once it really played (a missing clip marks nothing)
            fcPickHold = OzFaction.PickHold(line, fcPickLen);
            FactionPickHolds++; FactionLastHold = fcPickHold;
            if (!line) Debug.Log($"[OpenZ Menu] faction pick ({how}): Zod's line heard before: no line, no hold, the level selection next");
            if (pbRoot) ShowPlayBlue();                                                                      // the level selection's tag line names the side at once
            if (cardRt) RefreshObjectiveCard(true);
        }

        /// <summary>Zod's joke (item 78b) on his voice bus (Voice/Commander, his -2 dB), as one of the director's outside voices (the radio and
        /// the announcer wait for it). Returns its length (0 when the clip is missing).</summary>
        float PlayFactionVoice(int side)
        {
            var clip = Resources.Load<AudioClip>(OzFaction.Voice[side]);
            if (!clip) { Debug.LogWarning("[OpenZ Menu] faction screen: Resources/" + OzFaction.Voice[side] + " missing: the pick without Zod's line"); FactionClip = ""; return 0f; }
            if (!fcVoice)
            {
                fcVoice = gameObject.AddComponent<AudioSource>();
                fcVoice.playOnAwake = false; fcVoice.loop = false; fcVoice.spatialBlend = 0f; fcVoice.priority = 8; fcVoice.dopplerLevel = 0f;
                fcVoice.bypassReverbZones = true; fcVoice.ignoreListenerPause = true;
                AudioDirector.RegisterExternalVoice(fcVoice);
            }
            float bus = AudioDirector.I ? AudioDirector.I.VoiceBusLinear("Voice/Commander") : PlayerPrefs.GetFloat("oz_vol_voice", 1f);
            fcVoice.Stop(); fcVoice.clip = clip;
            fcVoice.volume = Mathf.Clamp01(bus * Mathf.Pow(10f, OzFaction.VoiceDb / 20f));
            fcVoice.Play();
            FactionVoices++; FactionClip = clip.name; FactionVolume = fcVoice.volume;
            Debug.Log($"[OpenZ Menu] Zod on the pick: {clip.name} ({clip.length:0.00} s, volume {fcVoice.volume:0.00})");
            return clip.length;
        }

        // ---------------------------------------------------------------- every frame on the faction screen (Update)
        void TickFaction(float dt)
        {
            if (!fcRoot || fc[0] == null || fc[1] == null) return;
            Vector2 ptr = fcProofPointer ?? lastMouse;
            bool touch = OzTouch.Now == OzTouch.Driver.Touch && !fcProofPointer.HasValue;
            int over = -1;
            for (int i = 0; i < 2; i++) if (fc[i].rt && (RectTransformUtility.RectangleContainsScreenPoint(fc[i].rt, ptr, null) || FcCardHit(i, ptr))) over = i;   // k75a3: its DOSSIER card is part of it
            if (over != fcHover) { fcHover = over; if (over >= 0 && !kbMode && fcPicked < 0) Sfx("ui_hover"); }
            if (over >= 0 && !kbMode && fcPicked < 0) fcFocus = over;                                          // the pointer takes the focus (as the menu's items): ENTER / A then picks the poster it points at
            int active = fcPicked >= 0 ? fcPicked : kbMode ? fcFocus : touch ? fcTap : fcHover;
            bool locked = !OzPlayBlue.Offered();

            // the mood: the pointer between the posters' middles (keys / pad / finger / a pick: the poster in focus)
            Rect r0 = ScreenRectOf(fc[0].rt), r1 = ScreenRectOf(fc[1].rt);
            float target = fcPicked >= 0 || kbMode || touch ? (active == OzFaction.Red ? -1f : active == OzFaction.Blue ? 1f : 0f) : OzFaction.MoodTarget(ptr.x, r0.center.x, r1.center.x);
            fcMood = OzFaction.Damp(fcMood, target, ref fcMoodVel, OzFaction.MoodTime, dt);
            fcWashUnder.color = OzFaction.Wash(fcMood, OzFaction.WashUnder);
            int gs = fcMood < 0f ? OzFaction.Red : OzFaction.Blue;                                          // k75a2: the soft round glow behind the poster the mood points at (none half way)
            fcWashSide.color = OzFaction.Wash(fcMood, OzFaction.Glow[gs]);
            if (gs != fcGlowSide)
            {
                fcGlowSide = gs; var gp = fc[gs];                                                                 // it moves only while its strength is 0 (the mood crosses the middle)
                var gc = fcLayout.ToCanvas(new Vector2(gp.x + gp.w * 0.5f, gp.y + gp.h * 0.5f));             // k75a3: the poster's middle on the canvas (the group may be scaled)
                fcWashSide.rectTransform.Place(new OzRect(gc.x - OzFaction.GlowW * 0.5f, gc.y - OzFaction.GlowH * 0.5f, OzFaction.GlowW, OzFaction.GlowH));
            }
            fcWashOver.color = OzFaction.Wash(fcMood, OzFaction.WashOver);
            bool overOn = fcWashOver.color.a > 0.0005f; if (fcWashOver.enabled != overOn) fcWashOver.enabled = overOn;   // k75a2: WashOver 0 = nothing drawn over the posters

            // the rig: the render size (the poster's pixels, its hover scale included), the cameras while the screen shows
            if (fcRig)
            {
                int rh = Mathf.Clamp(Mathf.RoundToInt(OzFaction.PosterH * Screen.height / OzLayout.RefH * 1.04f), 256, 2048);
                fcRig.SetSize(Mathf.RoundToInt(rh * OzFaction.PosterAspect), rh);
                fcRig.SetLive(view == View.Faction && fcCg.alpha > 0.001f);
            }
            float now = OzMotion.Now, fade = Mathf.Max(0.01f, OzMotion.Dur(0.25f, true));
            for (int i = 0; i < 2; i++)
            {
                var p = fc[i]; var rp = fcRig ? fcRig.P[i] : null;
                // the print
                bool layers = rp != null && rp.layersReady;
                if (layers)
                {
                    Bind(p.back, rp.layers[0]); Bind(p.mid, rp.layers[1]); Bind(p.border, rp.layers[2]); Bind(p.band, rp.layers[3]);
                    Bind(p.grain, fcRig.Grain);
                    var pm = fcRig.Print[i];                                                                        // k75a2: the print's aged paper (OzPosterFigure, print mode)
                    if (pm)
                    {
                        UsePrint(p.back, FcLiveMat(i, 0, pm)); UsePrint(p.mid, FcLiveMat(i, 1, pm)); UsePrint(p.border, pm); UsePrint(p.band, pm);   // k75a3: the back plate and the mid layer come alive in focus
                        var behind = OzFaction.PaperBehind(fcMood, OzFaction.GlowAt((p.x - fc[gs].x) * fcLayout.scale, 0f));   // the screen's colour behind this poster
                        pm.SetColor(FcPaperBehind, behind); FcLivePaper(i, behind);
                    }
                }
                p.readyK = Mathf.MoveTowards(p.readyK, layers ? 1f : 0f, dt / fade);
                float pa = OzEase.OutCubic(p.readyK); if (!Mathf.Approximately(p.printCg.alpha, pa)) p.printCg.alpha = pa;
                // the live render
                bool fig = rp != null && rp.modelReady && rp.rt && fcRig.Live;
                if (fig)
                {
                    if (p.figure.texture != rp.rt) p.figure.texture = rp.rt;
                    var fm = FcFigureMat(i); if (fm && p.figure.material != fm) p.figure.material = fm;         // k75a4: the poster's own copy (its glint / glow)
                }
                p.figK = Mathf.MoveTowards(p.figK, fig ? 1f : 0f, dt / Mathf.Max(0.01f, OzMotion.Dur(0.35f, true)));
                bool figOn = p.figK > 0.001f && p.figure.texture;
                if (p.figure.enabled != figOn) p.figure.enabled = figOn;
                var fc4 = new Color(1f, 1f, 1f, OzEase.OutCubic(p.figK)); if (p.figure.color != fc4) p.figure.color = fc4;
                // focus, the pick's pop, the other poster dimmed a little
                bool on = active == i;
                p.focusK = Mathf.MoveTowards(p.focusK, on ? 1f : 0f, dt / Mathf.Max(0.01f, OzMotion.Dur(OzMotion.Medium)));
                p.dimK = Mathf.MoveTowards(p.dimK, active >= 0 && !on ? 1f : 0f, dt / Mathf.Max(0.01f, OzMotion.Dur(OzMotion.Medium)));
                float e = OzEase.OutCubic(p.focusK);
                float pop = fcPicked == i ? 1f - OzEase.OutCubic(OzMotion.T(fcPickAt, OzMotion.Dur(0.32f))) : 0f;
                float s = 1f + 0.025f * e + 0.05f * pop;
                if (!Mathf.Approximately(p.rt.localScale.x, s)) p.rt.localScale = new Vector3(s, s, 1f);
                if (p.glow.enabled) SetAlpha(p.glow, 0.85f * e);
                p.dim.color = new Color(0f, 0f, 0f, 0.3f * OzEase.OutCubic(p.dimK));
                if (p.lockShade)
                {
                    if (p.lockShade.gameObject.activeSelf != locked) p.lockShade.gameObject.SetActive(locked);
                    if (p.lockChip && p.lockChip.gameObject.activeSelf != locked) p.lockChip.gameObject.SetActive(locked);
                }
                // the slot: the side line, or the tip (the pick's words while Zod says them)
                string words = fcPicked == i ? OzFaction.Caption(i) : OzFaction.Tip(i);
                if (words != p.tipShown) SizeTip(p, words);
                bool tipOn = on && !(i == OzFaction.Blue && locked);
                p.tipK = Mathf.MoveTowards(p.tipK, tipOn ? 1f : 0f, dt / Mathf.Max(0.01f, OzMotion.Dur(OzMotion.Short, true)));
                float tk = OzEase.OutCubic(p.tipK);
                if (!Mathf.Approximately(p.tipCg.alpha, tk)) p.tipCg.alpha = tk;
                p.sideLine.Alpha = 1f - tk;
                string line = i == OzFaction.Blue && locked ? OzFaction.LockedLine : OzFaction.SideLine[i];
                if (p.sideLine.text != line) p.sideLine.Set(line);
                // the camera: the slow loop, and the pointer follow over this poster; the print's back layers slide with it
                if (rp != null && rp.cam)
                {
                    Rect sr = i == 0 ? r0 : r1;
                    bool hov = fcHover == i && !kbMode && !touch && fcPicked < 0;
                    var want = hov ? new Vector2((ptr.x - sr.center.x) / Mathf.Max(1f, sr.width * 0.5f), (ptr.y - sr.center.y) / Mathf.Max(1f, sr.height * 0.5f)) : Vector2.zero;
                    p.follow = OzFaction.Damp(p.follow, want, ref p.followVel, OzFaction.FollowTime, dt);
                    p.hover = OzFaction.Damp(p.hover, hov ? 1f : 0f, ref p.hoverVel, OzFaction.HoverTime, dt);
                    var loop = OzFaction.Loop(now, OzFaction.Cams[i].phase) * (OzMotion.Reduced ? OzFaction.CalmLoop : 1f);
                    var ang = OzFaction.Angles(loop, p.follow, p.hover);
                    fcRig.Pose(i, ang.x, ang.y);
                    p.back.uvRect = OzFaction.SlideUv(OzFaction.LayerSlide(ang.x, ang.y, true));
                    p.mid.uvRect = OzFaction.SlideUv(OzFaction.LayerSlide(ang.x, ang.y, false));
                    if (fcRig.Figure && rp.rt) fcRig.Figure.SetFloat(FcRimPx, OzFaction.RimFrac * rp.rt.height);
                    if (fcRig.Figure) fcRig.Figure.SetColor(FcPaperBehind, OzFaction.PaperBehind(fcMood, 0f));     // k75a2: the paper rim aged as the paper (shared by both renders)
                }
            }
            TickFactionDeck(dt, active);                                                                       // k75a3: the VS sign, the live prints, the DOSSIER cards
            TickFactionPolish(dt, active);                                                                     // k75a4: the renders' glint / glow / size, the shade, the row ticks
            // after a pick: the level selection once Zod's line is through
            if (fcPicked >= 0 && now - fcPickAt >= fcPickHold) FactionToSelect(fcPickHold > 0f ? "after Zod's line" : "the line was heard before: no hold");   // 1.3 item 6
        }

        /// <summary>k75a2: a print layer draws with its poster's print material.</summary>
        static void UsePrint(RawImage r, Material m) { if (r && r.material != m) r.material = m; }

        /// <summary>k75a2: the mood's soft round glow (white; alpha = OzFaction.GlowAt across the texture's ellipse), made once.</summary>
        static Texture2D FcGlowTexture()
        {
            if (fcGlowTex) return fcGlowTex;
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "OzFaction_MoodGlow" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = ((x + 0.5f) / n - 0.5f) * OzFaction.GlowW, dy = ((y + 0.5f) / n - 0.5f) * OzFaction.GlowH;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(OzFaction.GlowAt(dx, dy) * 255f));
                }
            t.SetPixels32(px); t.Apply(false, true);
            fcGlowTex = t;
            return t;
        }

        static void Bind(RawImage r, Texture t)
        {
            if (!r) return;
            if (r.texture != t) r.texture = t;
            bool on = t != null;
            if (r.enabled != on) r.enabled = on;
            if (on && r.color != Color.white) r.color = Color.white;
        }

        /// <summary>A RectTransform's rect in screen pixels (the menu's canvas is a screen overlay: its world space is the screen).</summary>
        static Rect ScreenRectOf(RectTransform rt)
        {
            rt.GetWorldCorners(fcCorners);
            return Rect.MinMaxRect(fcCorners[0].x, fcCorners[0].y, fcCorners[2].x, fcCorners[2].y);
        }

        // ---------------------------------------------------------------- proof hooks (View/OzFactionProof.cs, Play only)
        public bool ProofFactionShown => view == View.Faction && fcRoot && fcRoot.gameObject.activeInHierarchy && fcCg.alpha > 0.99f;
        public void ProofFaction() { if (side != Side.None) CloseSide(); GoFaction(); }
        /// <summary>A pointer for the faction screen's hover, tint and follow (null = the real one).</summary>
        public void ProofFactionPointer(Vector2? p) { fcProofPointer = p; if (p.HasValue) kbMode = false; }
        public void ProofFactionKeys(bool left, bool right, bool enter, bool esc) { if (left || right || enter) kbMode = true; FactionKeys(left, right, enter, esc); }
        public void ProofFactionClick(int side) => PickFaction(side, "click");
        public void ProofBackFromSelect() => BackFromSelect();
        public void ProofFactionBack() => GoMain();
        public float ProofMood => fcMood;
        public Color ProofWashUnder => fcWashUnder ? fcWashUnder.color : Color.clear;
        public Color ProofWashOver => fcWashOver ? fcWashOver.color : Color.clear;
        /// <summary>k75a2: the mood's round glow (its colour and alpha; its rect).</summary>
        public Color ProofGlow => fcWashSide ? fcWashSide.color : Color.clear;
        public RectTransform ProofGlowRt => fcWashSide ? fcWashSide.rectTransform : null;
        public int ProofPicked => fcPicked;
        public int ProofActive => fcPicked >= 0 ? fcPicked : kbMode ? fcFocus : fcHover;
        public float ProofTipAlpha(int i) => fc[i] != null && fc[i].tipCg ? fc[i].tipCg.alpha : 0f;
        public string ProofTipText(int i) => fc[i] != null ? fc[i].tipText.text : "";
        public RectTransform ProofTipRt(int i) => fc[i] != null ? fc[i].tip : null;
        public RectTransform ProofPoster(int i) => fc[i] != null ? fc[i].rt : null;
        public OzText ProofTitle(int i, bool general) => fc[i] == null ? null : general ? fc[i].general : fc[i].company;
        public RawImage ProofFigure(int i) => fc[i] != null ? fc[i].figure : null;
        public RawImage ProofLayer(int i, int k) => fc[i] == null ? null : k == 0 ? fc[i].back : k == 1 ? fc[i].mid : k == 2 ? fc[i].border : k == 3 ? fc[i].band : fc[i].grain;
        public float ProofPrintAlpha(int i) => fc[i] != null && fc[i].printCg ? fc[i].printCg.alpha : 0f;
        public RectTransform ProofFactionBackRt => fcBack != null ? fcBack.Rt : null;
        public OzFactionPosters ProofRig => fcRig;
        /// <summary>The level selection's tag line (the side's name) and its colour.</summary>
        public string ProofSideTag => pbLabel != null ? pbLabel.text : "";
        public Color ProofSideTagColor => pbLabel != null ? pbLabel.color : Color.clear;
        public AudioSource ProofFactionVoice => fcVoice;
    }
}
