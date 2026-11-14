using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    // ============================================================================================================================
    // 1.2 item 60: the UNIT STATS selector of the battlefield screen (its state, words and rules: View/OzLevelStats.cs; the level start:
    // View/OzLevelStatsPlan.cs; the rule DV142: Sim/ZWorld.CustomStats.cs). Shown ONLY while a Custom profile with at least one change
    // exists (the Unit editor's Custom stats on this computer) and outside 'Zod exact'; else the screen is exactly as before.
    //   [ UNIT STATS      [OPENZ][CUSTOM] ]      a small plate standing on the deploy bar right over DEPLOY, exactly DEPLOY's width,
    //   [            DEPLOY · JUNGLE      ]      8 u over the bar (the tooltips' line): the numbers the DEPLOY below it plays with
    //  * The bar's row has no room for a fourth block on 16:10 (2560 x 1600) and narrower canvases, and the OBJECTIVE card's lines are
    //    taken; over DEPLOY the screen is free at every size (the commander's strip is at the left, the level cards end 128 u higher).
    //  * Two small buttons in DIFFICULTY's style (the chosen one lit); a click on a button, on the label or the key N picks
    //    (ui_toggle_on / ui_toggle_off; the chosen one again: ui_click). Saved at once (oz_unit_stats), remembered between starts;
    //    back to OPENZ when the profile is gone. Its tooltip (the bar's tooltip plate) opens left of the plate, on the tooltips' line.
    //  * While it shows, the HEAD START / SHUTTLE tooltips stop 10 u left of it and the editor's "not in the build settings" line moves
    //    over it, so nothing ever lies on it. The key hint row names N.
    // Hooks: DeployBar.cs (BuildDeployConsole builds it, RefreshDeployConsole refreshes it, PlaceDeployTips keeps the tips off it),
    // OpenZMainMenu.cs (the key, the key hint row, DEPLOY arms the choice, Awake ends a Custom level's values).
    // ============================================================================================================================
    public partial class OpenZMainMenu
    {
        /// <summary>The UNIT STATS selector's key on the battlefield screen (S is the menu's down key).</summary>
        public const Key UnitStatsKey = Key.N;
        /// <summary>The plate: DEPLOY's width, 34 u tall, its lower edge 8 u over the bar; the label 12 u in, the buttons 24 u tall, 5 u from
        /// the right edge, 4 u apart.</summary>
        public const float UsH = 34f, UsGap = 8f, UsPadL = 12f, UsPadR = 5f, UsBtnH = 24f, UsBtnGap = 4f, UsBtnPad = 10f, UsBtnMin = 56f, UsLabelGap = 10f;
        /// <summary>The tooltip's body box (the bar's tooltip plate: 16 u in, 304 u wide).</summary>
        const float UsTipBodyW = 304f;

        RectTransform usRoot, usTip; CanvasGroup usTipCg; HudButton usOpenZ, usCustom; OzText usLabel, usTipBody;
        bool usChecked, usShown, usTipPlate, usTipA, usTipB, usTipForce; float usTipK;
        OzStatsProfile usProfile;
        /// <summary>Checks: picks by click or key, key presses while it is not shown.</summary>
        public static int UnitStatsToggles { get; private set; }
        public static int UnitStatsKeyRefused { get; private set; }

        /// <summary>The selector is shown on this screen: offered and a profile exists (read once per visit of the main menu scene).</summary>
        bool UnitStatsShown
        {
            get
            {
                if (usChecked) return usShown;
                usChecked = true;
                usProfile = OzLevelStats.ReadProfile();
                usShown = OzLevelStats.Offered() && usProfile.Exists;
                if (!usProfile.Exists && OzLevelStats.Box)
                {
                    OzLevelStats.Box = false;                                                                  // the profile is gone: OPENZ (a new profile starts at OPENZ again)
                    Debug.Log($"[OpenZ Menu] UNIT STATS: no Custom profile ({usProfile.Error ?? "no value differs from OpenZ's"}): the choice is OPENZ again ({OzLevelStats.Pref} 0)");
                }
                Debug.Log(usShown ? $"[OpenZ Menu] UNIT STATS shown: a Custom profile \"{usProfile.Name}\" with {usProfile.Changed} change(s) (choice {(OzLevelStats.Box ? "CUSTOM" : "OPENZ")})"
                                  : $"[OpenZ Menu] UNIT STATS not shown ({(!OzLevelStats.Offered() ? "'Zod exact'" : usProfile.Error ?? "no value differs from OpenZ's")}): the battlefield screen as before");
                return usShown;
            }
        }

        /// <summary>BuildSelect's key hint row: "N  UNIT STATS     " while the selector shows, else nothing.</summary>
        string UnitStatsKeyHint() => UnitStatsShown ? OzKeyNames.Name(UnitStatsKey) + "  UNIT STATS     " : "";

        /// <summary>BuildDeployConsole, after DEPLOY and the error line: the plate over DEPLOY and its tooltip (nothing when not shown).</summary>
        void BuildUnitStats(RectTransform p, OzDeployBar.Layout L)
        {
            usRoot = usTip = null; usOpenZ = usCustom = null; usTipK = 0f; usTipPlate = usTipA = usTipB = usTipForce = false;
            if (!UnitStatsShown) return;
            float top = OzLayout.RefH - OzMenuLayout.DeployBarH - UsGap - UsH, w = L.deployW, mid = UsH * 0.5f;
            usRoot = Hud.Node("UnitStats", p).Place(new OzRect(L.deployX, top, w, UsH));
            Hud.Plate(usRoot, "sm", 0f, OzUi.Amber);                                                          // the kit's small plate with its amber tab, as the bar's card
            AddHot(usRoot, View.Select, () => SetUnitStats(!OzLevelStats.Box, false), h => usTipPlate = h, null, null);   // first: the buttons (added after it) take the pointer over them
            usLabel = Hud.Label(usRoot, "Label", OzLevelStats.Label, 10, OzUi.Ink2, 0.24f, OzStyle.Plain);
            float lw = usLabel.preferredWidth;
            usLabel.At(UsPadL, mid, OzH.L, true, lw + 4f);
            // the two buttons, right-aligned: the same width, each as wide as the longer word and its pads (at least 56 u); where DEPLOY is
            // narrow (5:4) and that does not fit beside the label, each as wide as its own word with the room shared out evenly
            usOpenZ = HudButton.Make(usRoot, "UnitStatsOpenZ", OzLevelStats.OpenZWord, new Vector2(UsBtnMin, UsBtnH), HudButtonKind.Small, 10, 0.14f);
            usCustom = HudButton.Make(usRoot, "UnitStatsCustom", OzLevelStats.CustomWord, new Vector2(UsBtnMin, UsBtnH), HudButtonKind.Small, 10, 0.14f);
            float w0 = usOpenZ.Label != null ? usOpenZ.Label.preferredWidth : 0f, w1 = usCustom.Label != null ? usCustom.Label.preferredWidth : 0f;
            float room = w - UsPadL - lw - UsLabelGap - UsPadR - UsBtnGap;                                       // for both buttons, never into the label
            float b0 = Mathf.Max(UsBtnMin, Mathf.Max(w0, w1) + 2f * UsBtnPad), b1 = b0;
            if (2f * b0 > room) { float pad = Mathf.Max(0f, (room - w0 - w1) * 0.25f); b0 = w0 + 2f * pad; b1 = w1 + 2f * pad; }
            float bx = w - UsPadR - b0 - b1 - UsBtnGap, by = (UsH - UsBtnH) * 0.5f;
            usOpenZ.Rt.Place(new OzRect(bx, by, b0, UsBtnH)); usCustom.Rt.Place(new OzRect(bx + b0 + UsBtnGap, by, b1, UsBtnH));
            float bw = b1;
            AddHot(usOpenZ.Rt, View.Select, () => SetUnitStats(false, false), h => usTipA = h, usOpenZ, null);
            AddHot(usCustom.Rt, View.Select, () => SetUnitStats(true, false), h => usTipB = h, usCustom, null);
            // the editor's "not in the build settings" line: over the plate now (right-aligned at DEPLOY's right edge as before)
            if (errorLine != null) errorLine.At(L.deployX + L.deployW, top - 16f, OzH.R, false, Mathf.Max(200f, L.deployX + L.deployW - (stripRight + OzDeployBar.TipStripGap)));
            // the tooltip: the bar's tooltip plate, on the tooltips' line, its right edge 10 u left of the plate
            float tipX = Mathf.Max(Mathf.Max(L.m, stripRight + OzDeployBar.TipStripGap), L.deployX - OzDeployBar.TipStripGap - OzDeployBar.TipW);
            usTip = Hud.Node("UnitStatsTip", p).Place(new OzRect(tipX, OzDeployBar.TipBottom - OzDeployBar.TipH, OzDeployBar.TipW, OzDeployBar.TipH));
            Hud.Plate(usTip, "md", 0f, OzUi.Amber);
            usTipCg = usTip.gameObject.AddComponent<CanvasGroup>(); usTipCg.alpha = 0f; usTipCg.blocksRaycasts = false;
            Hud.Label(usTip, "Title", OzLevelStats.TipTitle + OzKeyNames.Name(UnitStatsKey), 12, OzUi.Amber, 0.2f).At(16, 14, OzH.L, false, 300);
            usTipBody = Hud.Label(usTip, "Body", "", 11, OzUi.Ink2, 0f, OzStyle.Plain).Body(16, 36, UsTipBodyW);
            usTipBody.Set(OzLevelStats.TipLead + "\n" + YoursLine(usTipBody) + "\n" + OzLevelStats.TipTable);
            usTip.gameObject.SetActive(false);
            RefreshUnitStats();
            Debug.Log($"[OpenZ Menu] UNIT STATS selector over DEPLOY: {L.deployX:0}..{L.deployX + w:0} x {top:0}..{top + UsH:0} u, buttons {bw:0} u from {L.deployX + bx:0} u; tooltip {tipX:0}..{tipX + OzDeployBar.TipW:0} u");
        }

        /// <summary>The tooltip's line about the player's own profile, the name shortened until the line fits the body's width.</summary>
        string YoursLine(OzText measure)
        {
            string name = usProfile != null ? usProfile.Name : "";
            int n = usProfile != null ? usProfile.Changed : 0;
            string Line(string s) => "Yours: \"" + s + "\", " + n + (n == 1 ? " value" : " values") + " changed.";
            string line = Line(name);
            while (measure != null && name.Length > 3 && measure.WidthAt(line) > UsTipBodyW) { name = name.Substring(0, name.Length - 1).TrimEnd(); line = Line(name + "..."); }
            return line;
        }

        /// <summary>RefreshDeployConsole, every frame on the battlefield screen: the lit button, the tooltip.</summary>
        void RefreshUnitStats()
        {
            if (!usRoot) return;
            bool offered = OzLevelStats.Offered();                                                              // 'Zod exact' turned on in OPTIONS meanwhile: hidden (as PLAY AS BLUE's line)
            if (usRoot.gameObject.activeSelf != offered) usRoot.gameObject.SetActive(offered);
            if (!offered) { usTipPlate = usTipA = usTipB = false; }
            bool custom = OzLevelStats.Box;
            SetSelected(usOpenZ, !custom); SetSelected(usCustom, custom);
            if (usLabel != null) usLabel.color = custom ? OzLevelStats.Cyan : (usTipPlate ? (Color)OzUi.Ink : (Color)OzUi.Ink2);
            if (!usTip) return;
            float dur = Mathf.Max(0.01f, OzMotion.Dur(OzMotion.Short, true));
            usTipK = Mathf.MoveTowards(usTipK, usTipPlate || usTipA || usTipB || usTipForce ? 1f : 0f, Time.unscaledDeltaTime / dur);
            if (usTipK > 0.001f && !usTip.gameObject.activeSelf) usTip.gameObject.SetActive(true);
            float ta = OzEase.OutCubic(usTipK); if (!Mathf.Approximately(usTipCg.alpha, ta)) usTipCg.alpha = ta;
            if (usTipK <= 0.001f && usTip.gameObject.activeSelf) usTip.gameObject.SetActive(false);
        }

        /// <summary>PlaceDeployTips: a block's tooltip ends 10 u left of the plate while it shows (else as before).</summary>
        float UnitStatsTipX(float x) => usRoot && usRoot.gameObject.activeInHierarchy ? Mathf.Min(x, usRoot.anchoredPosition.x - OzDeployBar.TipStripGap - OzDeployBar.TipW) : x;

        /// <summary>A pick by a click (a button, the label) or the key N (where the selector is not shown: ui_disabled).</summary>
        void SetUnitStats(bool custom, bool byKey)
        {
            if (!UnitStatsShown || !OzLevelStats.Offered()) { if (byKey) { UnitStatsKeyRefused++; Sfx("ui_disabled"); } return; }
            if (OzLevelStats.Box == custom) { Sfx("ui_click"); return; }
            OzLevelStats.Box = custom;                                                                        // saved at once
            UnitStatsToggles++;
            Sfx(custom ? "ui_toggle_on" : "ui_toggle_off");
            RefreshUnitStats();
            Debug.Log($"[OpenZ Menu] UNIT STATS: {(custom ? "CUSTOM (the Unit editor's Custom stats, both teams)" : "OPENZ (the game's own numbers)")} ({OzLevelStats.Pref} {(custom ? 1 : 0)}, saved)");
        }

        // ---------------------------------------------------------------- proof hooks (View/OzLevelStatsProof.cs, Play only)
        public RectTransform ProofUnitStatsRoot => usRoot;
        public RectTransform ProofUnitStatsTip => usTip;
        public RectTransform ProofUnitStatsOpenZ => usOpenZ != null ? usOpenZ.Rt : null;
        public RectTransform ProofUnitStatsCustom => usCustom != null ? usCustom.Rt : null;
        public RectTransform ProofErrorLine => errorLine != null ? errorLine.rectTransform : null;
        public bool ProofUnitStatsShown => UnitStatsShown;
        public string ProofUnitStatsTipText => usTipBody != null ? usTipBody.text : "";
        public void ProofUnitStatsTipHover(bool on) => usTipForce = on;
        public void ProofSetUnitStats(bool custom, bool byKey) => SetUnitStats(custom, byKey);
        /// <summary>The profile is read again and the screen built again (the proof changes the saved profile between its steps).</summary>
        public void ProofUnitStatsRebuild() { usChecked = false; Build(); }
    }
}
