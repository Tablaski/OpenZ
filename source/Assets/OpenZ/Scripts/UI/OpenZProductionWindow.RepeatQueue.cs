using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// pure layout numbers in window units of the build menu. x is the window's x, y is measured from the QUEUE row's top (the five queue
    /// slots, baked in the frame, are at x 15 + 58 i, y 16..68, 52 u; the bottom row is ProdActionDY = 82 u under the row's top).
    ///  - label line (y 0): QUEUE at 14 (as before); the hint MAX 5 · CLICK TO REMOVE left-aligned at 70 in a box to 299 (the last slot's
    ///    right edge: 6 u before the caption, the slots' own gap); the caption REPEAT at 305 in QUEUE's own style (9, Muted, +0.24, plain);
    ///  - three tiles OFF / LAST / ALL, Small buttons 43 x 52 at the slots' top and height, 2 u apart: 305..348, 350..393, 395..438 (right
    ///    edge 438 like the hints); in each the repeat glyph (20 u, centred 15 u under the tile's top) and its word (11, +0.08, caps centred
    ///    41 u under the top, in a 35 u box); the LAST tile's "1" (11) on the glyph's centre; OFF's glyph at 45 % alpha;
    ///  - marks: LAST lit = the amber repeat badge (13 u over a 15 u dark shade) centred 45 u right of and 6 u under the last filled slot's
    ///    top-left, and that slot's tag REPEAT (tracking 0.10, as the mockup); ALL lit = the return rail, 2 u (the mockup's), never thinner
    ///    than 2 screen pixels (RailWidth: at 1280x720 and UI 90 % 2 u would be 1.3 px; the build-and-risk judge asked for a pixel floor),
    ///    amber 90 %: from the last filled slot's centre down (from y 68) to y 73, along to the first slot's centre and up to y 69, with an
    ///    arrow head of +-4 u x 1 u (two or more filled slots); OFF lit = no mark;
    ///  - bottom row: SET RALLY 15..223 (k252's green face and pin, 208 u), the clear-rally X 32 x 32 at 229..261 (the close glyph, 16 u).
    /// The painter of the mockup (kits/queue_redesign/tools/paint_mockups.py, idea1) drew these same numbers.
    /// </summary>
    public static class OzRepeatSwitchLayout
    {
        public const float HintX = 70, HintW = 229, CaptionX = 305;
        public const float TileX0 = 305, TileStep = 45, TileW = 43, TileH = 52;
        public const float GlyphSize = 20, GlyphMidY = 15, WordMidY = 41, WordSize = 11, WordTracking = 0.08f, WordBox = 35, OneSize = 11, OffGlyphAlpha = 0.45f;
        public const float BadgeDX = 45, BadgeDY = 6, BadgeSize = 13, ShadeSize = 15, ShadeAlpha = 0.9f;
        public const float RepeatTagTracking = 0.10f, TagTracking = 0.14f;
        public const float RailW = 2, RailMinPx = 2, RailY = 73, RailTopRight = 68, RailTopLeft = 69, RailAlpha = 0.9f, HeadDX = 4, HeadDY = 1;
        public const float ClearGlyph = 16;

        /// <summary>The rail's thickness (window units) at `pxPerU` screen pixels a window unit: the mockup's 2 u, never thinner than
        /// <see cref="RailMinPx"/> pixels (2 u is 4.4 px at 3840x2160 / 100 %, but 1.3 px at 1280x720 / 90 %). OzPx.Line is not used: it
        /// counts the HUD's pixels without the menu's own scale and stops at 1 px.</summary>
        public static float RailWidth(float pxPerU) => Mathf.Max(RailW, RailMinPx / Mathf.Max(0.01f, pxPerU));
        /// <summary>The arrow head's tip (y) for a rail `w` thick: the mockup's y 69 at 2 u; a thicker rail moves it down by half the extra
        /// thickness, so the head's top edge stays where the mockup has it (on the slots' bottom edge, y 68) and never reaches into slot 1.</summary>
        public static float HeadY(float w) => RailTopLeft + (w - RailW) * 0.5f;
        public static readonly OzRect Rally = new OzRect(15, 0, 208, 32);
        public static readonly OzRect ClearX = new OzRect(229, 0, 32, 32);
        public static readonly Color32 ShadeInk = new Color32(20, 22, 24, 255);

        /// <summary>Tile i (0 OFF, 1 LAST, 2 ALL) in the QUEUE row.</summary>
        public static OzRect Tile(int i) => new OzRect(TileX0 + TileStep * i, OzLayout.ProdQueueSlotY, TileW, TileH);
        /// <summary>Queue slot i's left edge.</summary>
        public static float SlotX(int i) => OzLayout.ProdQueueX0 + OzLayout.ProdQueueStep * i;
    }

    /// <summary>
    /// 1.4 queue REPEAT switch of the build menu (forts and factories alike), numbers: OzRepeatSwitchLayout. One caption REPEAT and three
    /// tiles at the end of the QUEUE row, exactly one lit (the Selected face, amber glyph and word), answering "what happens when the list
    /// runs out?": OFF = the factory goes idle, LAST = the last unit keeps being built (DV21's REPEAT), ALL = the whole list loops (DV169's
    /// REPEAT QUEUE). Shown: lit = REPEAT QUEUE ? ALL : REPEAT ? LAST : OFF. A click picks one, left or right alike (the chosen concept,
    /// queue_redesign/CONCEPT_idea1_spec.md "clicks"; the queue slots take both too, and a gamepad's
    /// X / Square is the right button); radio: the lit one does nothing more than the HUD's click sound. A disabled tile (ALL with DV169 off) is
    /// never clicked: OpenZHud plays ui_disabled for a left click and does nothing for a right click. It sends only the existing commands, only
    /// the ones that change, REPEAT always before REPEAT QUEUE goes off
    /// (OFF: ZWorld.SetBuildingRepeat false, then SetBuildingRepeatQueue false; LAST: SetBuildingRepeat true, then SetBuildingRepeatQueue
    /// false; ALL: SetBuildingRepeatQueue true), with the toggles' sounds (ui_toggle_on / ui_toggle_off, ui_error when refused), a toast and
    /// the factory reminder's note. No rule changes, no new DV; the computer never sees it.
    /// The REPEAT QUEUE rule (DV169) off: ALL shows the disabled face and its tooltip says why. 'Zod exact' (oz_zod_exact = 1 when the menu was
    /// built) or no DV21: no switch; the QUEUE row is exactly 1.3's (the repeat glyph at 311 / 30, REPEAT ON / OFF 98 x 32 at 340, the hint
    /// right-aligned at 438, the slot tags as 1.3). The 1.4 toggles (REPEAT 105 x 24, REPEAT QUEUE 133 x 24) and the loose glyph at 305 are gone.
    /// The bottom row (both modes, a layout change like the STOP move): SET RALLY 15..223, the clear-rally X 32 x 32 at 229..261 (the same
    /// button, click and tooltip; live only while rally points exist, Refresh's SetB).
    /// Hooks: Build calls RepeatSwitchBuild after DressButtons; RefreshText takes its hint from QueueHint and calls RepeatSwitchText at its end.
    /// The layout check (View/OzRepeatQueueCheck.cs) reads <see cref="LayoutProbe"/> and the proof hooks below.
    /// Keyboard / controller focus: the build menu's HudButtons are pointer buttons only (OpenZHud polls them under the pointer; no focus
    /// order exists for them), so the tiles have none either.
    /// </summary>
    public partial class OpenZProductionWindow
    {
        public const string RepeatOffTip = "REPEAT OFF|When the list runs out, the factory goes idle.|";
        public const string RepeatLastTip = "REPEAT LAST|When the list runs out, the last unit keeps being built (Zod auto-production).|";
        public const string RepeatAllTip = "REPEAT ALL|When the list runs out, it starts again: every finished unit goes back to the end of the list, so the whole list "
                                           + "loops. With one unit it is the same as LAST.|";
        public const string RepeatAllOffTip = "REPEAT ALL|When the list runs out, it would start again, but looping the whole list is switched off in this game.|";
        public const string ClearRallyTip = "CLEAR RALLY|Remove the rally points: new units stop at the door.|";
        public const string HintPlain = "MAX 5 · CLICK TO REMOVE", HintArmed = "RALLY: CLICK THE MAP · ESC", HintRouteFormat = "ROUTE · {0} · RELEASE SHIFT";
        public const int RepeatOff = 0, RepeatLast = 1, RepeatAll = 2;
        public static readonly string[] RepeatWords = { "OFF", "LAST", "ALL" };

        readonly HudButton[] rsTiles = new HudButton[3];
        readonly Image[] rsGlyphs = new Image[3];
        readonly Image[] rsRail = new Image[5];
        readonly float[] rsTagTracking = new float[ZObject.MAX_QUEUE_ITEMS];
        readonly string[] rqTipKey = new string[ZObject.MAX_QUEUE_ITEMS];
        OzText rsCaption, rsOne;
        Image rsBadge, rsShade;
        UnitIcon clearIcon;
        bool rsShown, rsLayoutSet, rsAllLive = true, rqTextOn, proofClassic, proofZod, zodExactRow, clearX;
        int rsLit = -1, rsMarkMode = -2, rsMarkN = -1;
        float rsMarkPx = -1f, rsRailW = OzRepeatSwitchLayout.RailW;

        /// <summary>The 1.4 STOP move (UI/OpenZProductionWindow.StopMove.cs). Needed with the switch: the bottom row below puts SET RALLY at
        /// 15..223, where STOP stood (OzBuildMenuLayout.Stop 15..116), so STOP must sit beside the bar (apply_kit.py requires the file).</summary>
        partial void StopMoveBuild();
        partial void StopMoveLayout(bool kit);

        /// <summary>The switch is wanted: DV21 on (REPEAT exists), not 'Zod exact', not a proof picture of the 1.3 row.</summary>
        bool SwitchWanted => ZWorld.FifoQueueDeviation && !zodExactRow && !proofClassic && !proofZod;

        /// <summary>The lit tile: REPEAT QUEUE (with its rule) ? ALL : REPEAT ? LAST : OFF.</summary>
        static int LitMode(ZObject b) => ZWorld.RepeatQueueDeviation && b.RepeatQueue ? RepeatAll : b.RepeatProduction ? RepeatLast : RepeatOff;

        /// <summary>Build, once, after DressButtons: the caption, the three tiles, the marks, the clear-rally X, then the STOP move and the places.</summary>
        void RepeatSwitchBuild()
        {
            zodExactRow = PlayerPrefs.GetInt("oz_zod_exact", 0) == 1;                                      // as ZGame read it at this start
            rsCaption = Hud.Label(queueRoot, "RepeatCaption", "REPEAT", 9, OzUi.Muted, 0.24f, OzStyle.Plain).At(OzRepeatSwitchLayout.CaptionX, 0);
            float g = OzRepeatSwitchLayout.GlyphSize;
            for (int i = 0; i < 3; i++)
            {
                int mode = i;
                var r = OzRepeatSwitchLayout.Tile(i);
                var t = HudButton.Make(queueRoot, "Repeat" + RepeatWords[i], RepeatWords[i], r.Size, HudButtonKind.Small,
                                       OzRepeatSwitchLayout.WordSize, OzRepeatSwitchLayout.WordTracking, HudButtonStyle.Normal);
                t.Rt.Place(r);
                t.Priority = 10;
                t.LabelColor = OzUi.Ink2;
                if (t.Label != null) t.Label.At(r.w * 0.5f, OzRepeatSwitchLayout.WordMidY, OzH.C, true, OzRepeatSwitchLayout.WordBox);
                rsGlyphs[i] = Hud.Icon(t.Content, "Glyph", "repeat", (r.w - g) * 0.5f, OzRepeatSwitchLayout.GlyphMidY - g * 0.5f, g, OzUi.Muted);
                t.Tooltip = i == RepeatOff ? RepeatOffTip : i == RepeatLast ? RepeatLastTip : RepeatAllTip;
                t.OnClick = c => PickRepeat(mode);                                                         // left or right click: the same pick (CONCEPT_idea1_spec.md
                t.OnRightClick = c => PickRepeat(mode);                                                    // "clicks"; as the queue slots; the gamepad's X is the right button)
                t.Refresh();
                rsTiles[i] = t;
            }
            rsOne = Hud.Label(rsTiles[RepeatLast].Content, "One", "1", OzRepeatSwitchLayout.OneSize, OzUi.Muted, 0f)
                       .At(OzRepeatSwitchLayout.TileW * 0.5f, OzRepeatSwitchLayout.GlyphMidY, OzH.C, true, g);
            rsShade = Hud.Icon(queueRoot, "RepeatBadgeShade", "repeat", 0, 0, OzRepeatSwitchLayout.ShadeSize, OzUi.A(OzRepeatSwitchLayout.ShadeInk, OzRepeatSwitchLayout.ShadeAlpha));
            rsBadge = Hud.Icon(queueRoot, "RepeatBadge", "repeat", 0, 0, OzRepeatSwitchLayout.BadgeSize, OzUi.Amber);
            for (int k = 0; k < rsRail.Length; k++) rsRail[k] = Hud.Img(queueRoot, "RepeatRail" + k, OzUi.A(OzUi.Amber, OzRepeatSwitchLayout.RailAlpha));
            for (int k = 0; k < rsTagTracking.Length; k++) rsTagTracking[k] = OzRepeatSwitchLayout.TagTracking;

            // the clear-rally X: the same button (its click hud.Rally.Clear, its rule in Refresh), the close glyph instead of the word
            var cx = OzRepeatSwitchLayout.ClearX; float cg = OzRepeatSwitchLayout.ClearGlyph;
            clearIcon = new UnitIcon(bClear.Content, "X", new Vector2(cg, cg), false, 0f);
            clearIcon.Rt.Place(new OzRect((cx.w - cg) * 0.5f, (cx.h - cg) * 0.5f, cg, cg));
            clearIcon.SetGlyph("close");
            bClear.Icon = clearIcon;                                                                       // HudButton.Refresh: 40 % while there is nothing to clear
            bClear.Tooltip = ClearRallyTip;

            StopMoveBuild();
            BottomRowLayout(true);
            RepeatSwitchLayout(SwitchWanted);
        }

        /// <summary>The QUEUE row: on = the switch (1.4), off = 1.3's row ('Zod exact', the check's pictures of the 1.3 row).</summary>
        void RepeatSwitchLayout(bool on)
        {
            rsShown = on; rsLayoutSet = true;
            rsCaption.SetActive(on);
            foreach (var t in rsTiles) t.Rt.gameObject.SetActive(on);
            repeatGlyph.gameObject.SetActive(!on);                                                         // 1.3: the glyph at 311 / 30 (RefreshText's own rule)
            bRepeat.Rt.gameObject.SetActive(!on && ZWorld.FifoQueueDeviation);                            // 1.3: REPEAT ON / OFF 98 x 32 at 340
            if (on) queueHint.At(OzRepeatSwitchLayout.HintX, 0, OzH.L, false, OzRepeatSwitchLayout.HintW);
            else queueHint.At(OzLayout.ProdHintRX, 0, OzH.R, false, 320);
            rsLit = -1; rsMarkMode = -2; rsMarkN = -1;
            Marks(-1, 0);
            foreach (var c in chips) c.tipUnit = null;                                                     // RefreshText writes the slot tooltips again
            for (int i = 0; i < rqTipKey.Length; i++) rqTipKey[i] = null;
            rqTextOn = false;
            nextText = 0f;
        }

        /// <summary>The bottom row: kit = SET RALLY 15..223 and the clear-rally X at 229..261; else k252's (1.3) CLEAR 101 u and SET RALLY.</summary>
        void BottomRowLayout(bool kit)
        {
            clearX = kit;
            var c = kit ? OzRepeatSwitchLayout.ClearX : OzBuildMenuLayout.Clear;
            var r = kit ? OzRepeatSwitchLayout.Rally : OzBuildMenuLayout.Rally;
            bClear.Rt.Place(c); Rehatch(bClear, c.Size);
            bRally.Rt.Place(r); Rehatch(bRally, r.Size);
            if (bClear.Label != null) bClear.Label.SetActive(!kit);
            if (clearIcon != null) clearIcon.Rt.gameObject.SetActive(kit);
        }

        /// <summary>RefreshText: the queue hint. 1.3's row keeps its texts; beside the switch the hint starts at x 70 and the rally texts are
        /// the short ones that fit before the caption at full size (OzRepeatSwitchLayout.HintW).</summary>
        string QueueHint(ZObject b, IReadOnlyList<ZWaypoint> pend)
        {
            if (rsShown) return hud.Rally.Armed ? HintArmed : pend != null ? string.Format(HintRouteFormat, pend.Count) : HintPlain;
            return hud.Rally.Armed ? "RALLY TOOL ARMED · CLICK THE MAP · ESC CANCELS"
                 : pend != null ? $"RALLY ROUTE · {pend.Count} POINTS · RELEASE SHIFT TO SEND"
                 : b.RallyPoints.Count > 0 ? $"RALLY · {b.RallyPoints.Count} POINT{(b.RallyPoints.Count > 1 ? "S" : "")} · MAX 5 · CLICK TO REMOVE" : HintPlain;
        }

        /// <summary>A click on a tile: pick that answer. Only the flags that change are sent, REPEAT first.</summary>
        void PickRepeat(int mode)
        {
            var b = Building; if (b == null || !rsShown) return;
            if (mode == LitMode(b)) return;                                                                // the lit tile: nothing changes
            if (mode == RepeatAll && !ZWorld.RepeatQueueDeviation) { OzSfx.Ui("ui_error"); return; }      // a second guard only: ALL is disabled then, and OpenZHud
                                                                                                           // never clicks a disabled button (it plays ui_disabled)
            bool ok = true;
            if (mode == RepeatAll) ok = W.SetBuildingRepeatQueue(hud.Player, b, true);                    // REPEAT is left as it is: it does not matter while the list loops
            else
            {
                bool rep = mode == RepeatLast;
                if (b.RepeatProduction != rep) ok = W.SetBuildingRepeat(hud.Player, b, rep);              // REPEAT first ...
                if (ok && b.RepeatQueue && ZWorld.RepeatQueueDeviation) ok = W.SetBuildingRepeatQueue(hud.Player, b, false);   // ... then REPEAT QUEUE off
            }
            nextText = 0f;                                                                                 // the texts follow at once
            if (!ok) { OzSfx.Ui("ui_error"); return; }
            OzFactoryReminder.ProductionSet(b);   // factory reminder (k247, UI/OzFactoryReminder.cs): a production change of his, as the toggles were
            OzSfx.Ui(mode == RepeatOff ? "ui_toggle_off" : "ui_toggle_on");
            hud.Toast(mode == RepeatOff ? "REPEAT OFF · STOPS AFTER THE QUEUE" : mode == RepeatLast ? "REPEAT LAST · THE LAST UNIT KEEPS BEING BUILT" : "REPEAT ALL · THE WHOLE QUEUE LOOPS",
                      mode == RepeatOff ? HudStyle.Muted : HudStyle.Amber);
            Debug.Log($"[OpenZ] REPEAT {RepeatWords[mode]} (DV21 REPEAT {(b.RepeatProduction ? "on" : "off")}, DV169 REPEAT QUEUE {(b.RepeatQueue ? "on" : "off")}): "
                      + $"{b.Type} #{b.Id}, building {b.CurUnit ?? "nothing"}, queue {string.Join(",", b.Queue)}");
        }

        /// <summary>RefreshText, at its end (10 Hz or on a change): the tiles, the marks, the slot tags, and the texts while the list loops.</summary>
        void RepeatSwitchText(ZObject b, bool paused)
        {
            if (rsCaption == null) return;
            bool want = SwitchWanted;
            if (!rsLayoutSet || want != rsShown) RepeatSwitchLayout(want);
            int n = Mathf.Min(b.Queue.Count, chips.Count);
            if (!rsShown) { TagTracking(n, -1); return; }                                                 // 1.3's row: every text is RefreshText's own

            bool allLive = ZWorld.RepeatQueueDeviation;
            int lit = LitMode(b);
            if (lit != rsLit || allLive != rsAllLive)
            {
                rsLit = lit; rsAllLive = allLive;
                for (int i = 0; i < 3; i++)
                {
                    var t = rsTiles[i];
                    bool on = i == lit, live = i != RepeatAll || allLive;
                    if (t.Selected != on || t.Interactable != live) { t.Selected = on; t.Interactable = live; t.Refresh(); }
                    Color gc = !live ? (Color)OzUi.Dim : on ? (Color)OzUi.AmberHi : (Color)OzUi.Muted;
                    var face = gc; if (i == RepeatOff) face.a *= OzRepeatSwitchLayout.OffGlyphAlpha;
                    if (rsGlyphs[i].color != face) rsGlyphs[i].color = face;
                    if (i == RepeatLast) rsOne.color = gc;
                }
                string allTip = allLive ? RepeatAllTip : RepeatAllOffTip;
                if (rsTiles[RepeatAll].Tooltip != allTip) rsTiles[RepeatAll].Tooltip = allTip;
            }

            // ALL: the line under the name says the list loops; the slots: NEXT on the first only, the tooltips say every entry comes back
            bool loop = lit == RepeatAll;
            if (loop != rqTextOn)
            {   // RefreshText's own slot tags and tooltips come back as soon as it is off (its cache is cleared)
                rqTextOn = loop;
                foreach (var c in chips) c.tipUnit = null;
                for (int i = 0; i < rqTipKey.Length; i++) rqTipKey[i] = null;
            }
            if (loop)
            {
                if (b.Building && b.CurUnit != null)
                {
                    var us = W.S.Unit(b.CurUnit);
                    int group = us != null && ZBuildLists.IsRobot(b.CurUnit) ? Mathf.Max(1, us.group_amount) : 1;
                    curSub.Set(paused ? "PAUSED · UNIT LIMIT 70"
                               : (b.Queue.Count > 0 ? $"NEXT {UnitInfo.Name(b.Queue[0])} · {(b.Queue.Count > 1 ? $"+{b.Queue.Count - 1} · " : "")}QUEUE REPEATS" : $"{UnitInfo.Name(b.CurUnit)} REPEATS")
                                 + (group > 1 ? $" · ×{group}" : ""));
                }
                for (int i = 0; i < n; i++)
                {
                    var c = chips[i]; var u = b.Queue[i];
                    c.tag.Set(i == 0 ? "NEXT" : "");
                    string key = (i == 0 ? "1:" : "n:") + u;
                    if (rqTipKey[i] == key) continue;
                    rqTipKey[i] = key;
                    c.btn.Tooltip = $"{UnitInfo.Name(u)}|{(i == 0 ? "Built next. " : "")}Goes back to the end of the queue once it is built (REPEAT ALL). Click to remove it from the queue and the loop.|";
                }
            }
            TagTracking(n, lit);
            Marks(lit, n);
        }

        /// <summary>The slot tags' tracking: the mockup's REPEAT tag at 0.10 beside the switch, 1.3's 0.14 everywhere else.</summary>
        void TagTracking(int n, int lit)
        {
            for (int i = 0; i < chips.Count && i < rsTagTracking.Length; i++)
            {
                float want = rsShown && lit == RepeatLast && i == n - 1 && chips[i].tag.text == "REPEAT" ? OzRepeatSwitchLayout.RepeatTagTracking : OzRepeatSwitchLayout.TagTracking;
                if (Mathf.Approximately(rsTagTracking[i], want)) continue;
                rsTagTracking[i] = want; chips[i].tag.Tracking = want;
            }
        }

        /// <summary>The menu's screen pixels per window unit once it has opened (the HUD's scale x the menu's own scale, without the 0.96 -> 1
        /// open animation): the rail's pixel floor is worked out from it.</summary>
        float SteadyUnitPx => win && win.parent ? win.parent.lossyScale.x * curScale : 1f;

        /// <summary>The marks on the slots: LAST = the badge on the last filled slot, ALL = the return rail (two or more filled slots), else none.
        /// The rail is laid out again when the menu's pixels per unit change (a screen size or UI SCALE change), for its pixel floor.</summary>
        void Marks(int lit, int n)
        {
            int mode = !rsShown ? -1 : lit == RepeatLast && n > 0 ? RepeatLast : lit == RepeatAll && n >= 2 ? RepeatAll : -1;
            float px = SteadyUnitPx;
            if (mode == rsMarkMode && n == rsMarkN && (mode != RepeatAll || Mathf.Abs(px - rsMarkPx) <= 0.002f * px)) return;
            rsMarkMode = mode; rsMarkN = n; rsMarkPx = px;
            bool badge = mode == RepeatLast, rail = mode == RepeatAll;
            rsShade.gameObject.SetActive(badge); rsBadge.gameObject.SetActive(badge);
            foreach (var im in rsRail) im.gameObject.SetActive(rail);
            if (badge)
            {
                float x = OzRepeatSwitchLayout.SlotX(n - 1) + OzRepeatSwitchLayout.BadgeDX, y = OzLayout.ProdQueueSlotY + OzRepeatSwitchLayout.BadgeDY;
                float s = OzRepeatSwitchLayout.ShadeSize, k = OzRepeatSwitchLayout.BadgeSize;
                rsShade.rectTransform.Place(new OzRect(x - s * 0.5f, y - s * 0.5f, s, s));
                rsBadge.rectTransform.Place(new OzRect(x - k * 0.5f, y - k * 0.5f, k, k));
            }
            if (rail)
            {
                float h = OzLayout.ProdQueueSize * 0.5f, w = rsRailW = OzRepeatSwitchLayout.RailWidth(px), hw = w * 0.5f, y = OzRepeatSwitchLayout.RailY;
                float xa = OzRepeatSwitchLayout.SlotX(n - 1) + h, xb = OzRepeatSwitchLayout.SlotX(0) + h;
                float top = OzRepeatSwitchLayout.RailTopRight, topL = OzRepeatSwitchLayout.RailTopLeft, hx = OzRepeatSwitchLayout.HeadDX, hy = OzRepeatSwitchLayout.HeadDY;
                Straight(rsRail[0], new OzRect(xa - hw, top, w, y + hw - top));                           // down from the last filled slot
                Straight(rsRail[1], new OzRect(xb - hw, y - hw, xa - xb + w, w));                         // along under the slots
                Straight(rsRail[2], new OzRect(xb - hw, topL, w, y + hw - topL));                         // up to the first slot
                float tip = OzRepeatSwitchLayout.HeadY(w);                                                // the arrow head (69 at 2 u)
                Seg(rsRail[3], new Vector2(xb - hx, tip + hy), new Vector2(xb, tip), w);
                Seg(rsRail[4], new Vector2(xb, tip), new Vector2(xb + hx, tip + hy), w);
            }
        }

        static void Straight(Image im, OzRect r) { var rt = im.rectTransform; rt.localRotation = Quaternion.identity; rt.Place(r); }

        /// <summary>A straight piece from a to b (window units, y down from the row's top), w thick.</summary>
        static void Seg(Image im, Vector2 a, Vector2 b, float w)
        {
            var rt = im.rectTransform; var d = b - a;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2((a.x + b.x) * 0.5f, -(a.y + b.y) * 0.5f);
            rt.sizeDelta = new Vector2(d.magnitude, w);
            rt.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        // ------------------------------------------------------------------ the layout check's view of the menu (View/OzRepeatQueueCheck.cs)
        /// <summary>One measured part of the menu: its screen rectangle (pixels), whether this kit placed or made it, whether it is a text's
        /// drawn glyphs, the part it belongs to (never an overlap with it; it must sit inside it, `margin` u from its left and right, unless
        /// `onTop`: it sits on it by design, e.g. the badge on its slot).</summary>
        public struct LayoutPart { public string name, owner; public Rect r; public bool kit, text, onTop; public float margin; }

        /// <summary>Show 1.3's whole look (true: the QUEUE row, STOP and CLEAR in the bottom row) for the check's before picture, or the kit's.</summary>
        public void ProofClassic(bool classic)
        {
            proofClassic = classic;
            StopMoveLayout(!classic);
            BottomRowLayout(!classic);
            RepeatSwitchLayout(SwitchWanted);
        }

        /// <summary>Show the 'Zod exact' row as the game shows it with the kit in (1.3's QUEUE row, the kit's bottom row, STOP beside the bar).</summary>
        public void ProofZodExact(bool on) { proofZod = on; RepeatSwitchLayout(SwitchWanted); }

        /// <summary>The code behind both clicks of tile `mode` (0 OFF, 1 LAST, 2 ALL), called directly: only for its second guard (ALL while
        /// DV169 is off), which no player's click reaches. The player's clicks are tested with the check's own mouse, through OpenZHud.</summary>
        public void ProofPick(int mode) => PickRepeat(mode);

        /// <summary>The texts are refreshed on the next frame (the check changes the building's flags behind the menu's back).</summary>
        public void ProofRefresh() => nextText = 0f;

        public bool ZodExactRow => zodExactRow;
        public bool RepeatSwitchShown => rsShown && rsTiles[0] != null && rsTiles[0].Rt.gameObject.activeInHierarchy;
        /// <summary>The lit tile (0 OFF, 1 LAST, 2 ALL), -1 when none, -2 when more than one is lit.</summary>
        public int RepeatLitShown { get { int lit = -1, k = 0; for (int i = 0; i < 3; i++) if (rsTiles[i] != null && rsTiles[i].Selected) { lit = i; k++; } return k > 1 ? -2 : lit; } }
        public bool RepeatTileLive(int i) => rsTiles[i] != null && rsTiles[i].Interactable;
        public string RepeatTileTip(int i) => rsTiles[i] != null ? rsTiles[i].Tooltip : null;
        /// <summary>Tile i's middle on the screen (pixels, the input system's frame): where the check's own mouse clicks it.</summary>
        public Vector2 RepeatTileCentre(int i) => rsTiles[i] != null ? Bounds(rsTiles[i].Rt).center : Vector2.zero;
        /// <summary>OpenZHud has tile i under the pointer (its hover rule: the button the click goes to).</summary>
        public bool RepeatTileHovered(int i) => rsTiles[i] != null && rsTiles[i].Hovered;
        /// <summary>The clear-rally X's middle on the screen (pixels), and whether OpenZHud has it under the pointer.</summary>
        public Vector2 ClearCentre => bClear != null ? Bounds(bClear.Rt).center : Vector2.zero;
        public bool ClearHovered => bClear != null && bClear.Hovered;
        /// <summary>The rail's thickness as laid out (window units; OzRepeatSwitchLayout.RailWidth at the menu's pixels per unit).</summary>
        public float RepeatRailWidth => rsRailW;
        public bool RepeatBadgeShown => rsBadge && rsBadge.gameObject.activeInHierarchy;
        public bool RepeatRailShown => rsRail[0] && rsRail[0].gameObject.activeInHierarchy;
        /// <summary>The slot the badge sits on (0-based), -1 when it is hidden.</summary>
        public int RepeatBadgeSlot => RepeatBadgeShown ? rsMarkN - 1 : -1;
        public string SlotTag(int i) => i >= 0 && i < chips.Count && chips[i].btn.Rt.gameObject.activeInHierarchy ? chips[i].tag.text : null;
        public bool ClassicRowShown => bRepeat != null && bRepeat.Rt.gameObject.activeInHierarchy;
        public bool ClearIsX => clearX && clearIcon != null && clearIcon.Rt.gameObject.activeInHierarchy;
        public bool ClearLive => bClear != null && bClear.Interactable;
        public string ClearTip => bClear != null ? bClear.Tooltip : null;
        public string QueueHintShown => queueHint != null ? queueHint.text : null;

        /// <summary>The hint texts beside the switch (their longest forms) with their widths at the hint's own size (window units, the
        /// game's text engine), and the box they must fit in at that size.</summary>
        public List<(string text, float width)> HintWidths(out float box)
        {
            box = OzRepeatSwitchLayout.HintW;
            var l = new List<(string, float)>();
            foreach (var s in new[] { HintPlain, HintArmed, string.Format(HintRouteFormat, 99) }) l.Add((s, queueHint.WidthAt(s)));
            return l;
        }

        /// <summary>The STOP move is in: STOP sits in the header.</summary>
        public bool StopInHeader => bStop != null && bStop.Rt && bStop.Rt.parent == body;

        /// <summary>The menu's screen pixels per window unit (the HUD canvas x the menu's own scale).</summary>
        public float UnitPx => win ? win.lossyScale.x : 1f;

        /// <summary>The QUEUE row's box on the screen (pixels): its top edge is y 0 of OzRepeatSwitchLayout (the check measures every part from it).</summary>
        public Rect QueueRowScreen => queueRoot ? Bounds(queueRoot) : default;

        /// <summary>The return rail's five pieces as drawn (screen pixels, in Marks' order: down from the last filled slot, along, up to the first
        /// slot, the arrow head's left and right stroke); empty while the rail is hidden.</summary>
        public List<Rect> RepeatRailScreen()
        {
            var l = new List<Rect>();
            if (RepeatRailShown) foreach (var im in rsRail) l.Add(Bounds(im.rectTransform));
            return l;
        }

        /// <summary>The parts of the open menu as drawn this frame (screen pixels), for the layout check.</summary>
        public List<LayoutPart> LayoutProbe()
        {
            var parts = new List<LayoutPart>();
            if (win == null) return parts;
            Canvas.ForceUpdateCanvases();
            Add(parts, "window", win, false);
            for (int i = 0; i < chips.Count; i++)
            {
                if (!chips[i].btn.Rt.gameObject.activeInHierarchy) continue;
                string slot = "queue slot " + (i + 1);
                Add(parts, slot, chips[i].btn.Rt, false);
                AddText(parts, slot + " tag", chips[i].tag.rectTransform, slot, true, 0f);
            }
            var ql = queueRoot.Find("Label") as RectTransform; if (ql) AddText(parts, "QUEUE label", ql, null, false);
            AddText(parts, "queue hint", queueHint.rectTransform, null, true);
            if (rsShown)
            {
                AddText(parts, "REPEAT caption", rsCaption.rectTransform, null, true);
                for (int i = 0; i < 3; i++)
                {
                    string tn = "REPEAT " + RepeatWords[i] + " tile";
                    Add(parts, tn, rsTiles[i].Rt, true);
                    if (rsTiles[i].Label != null) AddText(parts, RepeatWords[i] + " word", rsTiles[i].Label.rectTransform, tn, true, OzRepeatQueueCheckMargins.WordsU);
                    Add(parts, RepeatWords[i] + " glyph", rsGlyphs[i].rectTransform, true, tn);
                }
                AddText(parts, "LAST 1", rsOne.rectTransform, "LAST glyph", true, 0f);
                if (RepeatBadgeShown)
                {
                    string slot = "queue slot " + rsMarkN;
                    Add(parts, "repeat badge shade", rsShade.rectTransform, true, slot, true);                   // on the slot's corner by design
                    Add(parts, "repeat badge", rsBadge.rectTransform, true, "repeat badge shade", true);          // on its shade
                }
                if (RepeatRailShown) AddUnion(parts, "return rail", rsRail, true);
            }
            else
            {
                if (repeatGlyph.gameObject.activeInHierarchy && repeatGlyph.enabled) Add(parts, "repeat glyph (1.3)", repeatGlyph.rectTransform, true);
                if (bRepeat.Rt.gameObject.activeInHierarchy)
                {
                    Add(parts, "REPEAT (1.3)", bRepeat.Rt, true);
                    if (bRepeat.Label != null) AddText(parts, "REPEAT (1.3) words", bRepeat.Label.rectTransform, "REPEAT (1.3)", true, OzRepeatQueueCheckMargins.WordsU);
                }
            }
            foreach (var t in tiles) Add(parts, "tile " + t.unit, t.btn.Rt, false);
            Add(parts, "PLACE tile", bPlace.Rt, false);
            Add(parts, "unit picture", curIcon.Rt, false);
            AddText(parts, "unit name", curName.rectTransform, null, false);
            AddText(parts, "time left", curTime.rectTransform, null, false);
            AddText(parts, "next-unit line", curSub.rectTransform, null, false);
            bool moved = StopInHeader;
            AddText(parts, "formula", curFormula.rectTransform, null, moved);
            Add(parts, "progress bar", progFill.rectTransform.parent as RectTransform, moved);
            AddText(parts, "title", title.rectTransform, null, false);
            AddText(parts, "HP", hpText.rectTransform, null, false);
            Add(parts, "state chip", chipBg.rectTransform, false);
            Add(parts, "close", bClose.Rt, false);
            Add(parts, "STOP", bStop.Rt, moved);
            if (bStop.Label != null) AddText(parts, "STOP words", bStop.Label.rectTransform, "STOP", moved, OzRepeatQueueCheckMargins.WordsU);
            if (clearX)
            {
                Add(parts, "CLEAR RALLY X", bClear.Rt, true);
                Add(parts, "CLEAR RALLY X glyph", clearIcon.Rt, true, "CLEAR RALLY X");
            }
            else
            {
                Add(parts, "CLEAR", bClear.Rt, false);
                if (bClear.Label != null) AddText(parts, "CLEAR words", bClear.Label.rectTransform, "CLEAR", false, OzRepeatQueueCheckMargins.WordsU);
            }
            Add(parts, "SET RALLY", bRally.Rt, clearX);
            if (bRally.Label != null) AddText(parts, "SET RALLY words", bRally.Label.rectTransform, "SET RALLY", clearX, OzRepeatQueueCheckMargins.WordsU);
            return parts;
        }

        static readonly Vector3[] corners = new Vector3[4];
        static Rect Bounds(RectTransform rt)
        {
            rt.GetWorldCorners(corners);
            float x0 = corners[0].x, y0 = corners[0].y, x1 = x0, y1 = y0;
            for (int i = 1; i < 4; i++) { x0 = Mathf.Min(x0, corners[i].x); x1 = Mathf.Max(x1, corners[i].x); y0 = Mathf.Min(y0, corners[i].y); y1 = Mathf.Max(y1, corners[i].y); }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        static void Add(List<LayoutPart> parts, string name, RectTransform rt, bool kit, string owner = null, bool onTop = false)
        {
            if (!rt || !rt.gameObject.activeInHierarchy) return;
            parts.Add(new LayoutPart { name = name, owner = owner, r = Bounds(rt), kit = kit, onTop = onTop });
        }

        static void AddUnion(List<LayoutPart> parts, string name, Image[] ims, bool kit)
        {
            Rect u = default; bool any = false;
            foreach (var im in ims)
            {
                if (!im || !im.gameObject.activeInHierarchy) continue;
                var r = Bounds(im.rectTransform);
                u = any ? Rect.MinMaxRect(Mathf.Min(u.xMin, r.xMin), Mathf.Min(u.yMin, r.yMin), Mathf.Max(u.xMax, r.xMax), Mathf.Max(u.yMax, r.yMax)) : r; any = true;
            }
            if (any) parts.Add(new LayoutPart { name = name, r = u, kit = kit });
        }

        /// <summary>A label's drawn glyphs (TextMeshPro's text bounds); an empty text adds nothing; without TextMeshPro, the label's box.</summary>
        static void AddText(List<LayoutPart> parts, string name, RectTransform rt, string owner, bool kit, float margin = 0f)
        {
            if (!rt || !rt.gameObject.activeInHierarchy) return;
            var tmp = rt.GetComponent<TMPro.TMP_Text>();
            if (tmp == null)
            {
                parts.Add(new LayoutPart { name = name + " (box)", owner = owner, r = Bounds(rt), kit = kit, text = true, margin = margin });
                return;
            }
            if (string.IsNullOrEmpty(tmp.text)) return;
            tmp.ForceMeshUpdate();
            var bnd = tmp.textBounds;
            if (bnd.size.x <= 0f) return;
            Vector3 a = rt.TransformPoint(bnd.min), c = rt.TransformPoint(bnd.max);
            parts.Add(new LayoutPart { name = name, owner = owner, r = Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y)),
                                       kit = kit, text = true, margin = margin });
        }
    }

    /// <summary>The layout check's margins, shared by the probe (window units).</summary>
    public static class OzRepeatQueueCheckMargins
    {
        /// <summary>A button's words stay this far inside the button, left and right.</summary>
        public const float WordsU = 4f;
    }
}
