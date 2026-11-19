using System;

namespace OpenZ
{
    /// <summary>A rectangle in canvas units, top-left origin (DESIGN_SPEC section 1).</summary>
    public struct UeBox
    {
        public float x, y, w, h;
        public UeBox(float x, float y, float w, float h) { this.x = x; this.y = y; this.w = w; this.h = h; }
        public float Right => x + w;
        public float Bottom => y + h;
        public bool Overlaps(UeBox o) => x < o.Right - 0.01f && o.x < Right - 0.01f && y < o.Bottom - 0.01f && o.y < Bottom - 0.01f;
        public override string ToString() => $"{x:0.#},{y:0.#} {w:0.#}x{h:0.#}";
    }

    /// <summary>
    /// 1.2 item 6 (UI/OzUeControlsRoot.cs; spec 2.2, 2.3, E021-E049): the bottom band's geometry as pure functions of the canvas size,
    /// the other panels' insets, the dock's state and the text widths of the game's font (a measure function: text, size in u, tracking
    /// in em -> width in u), so the counted check can lay it out at every size of spec 2.1 in edit mode (SelfCheck) and a headless test
    /// can run it without the engine. Every number is prototype C round 2d's (DESIGN_SPEC section 4, measured in the running prototype).
    /// </summary>
    // 1.5 ue_bar_r2 (tracker A13): TileBeside, ZoomWidth and the first fit step in LayDock
    // 1.5 kit ue_bottom_left_r10 (FAILS A36): the zoom control left the bottom band (the BACKGROUND popover's VIEW row: Zoom.inMenu), so LayDock's first
    // fit step (room for it beside the dock) is gone; the team switch 36 u with 13.5 u labels over a 28 u count row (TeamSegH, CountSegH)
    public static class OzUeBottomLayout
    {
        // ---------------------------------------------------------------------------------------------- the prototype's numbers (u)
        public const float Gap = 12f;                              // panels from the canvas edges
        public const float DockPadL = 7f, DockPadR = 6f, DockPadV = 4f, Border = 1f;
        public const float CellPadTeam = 7f, SegCellH = 28f, SegH = 30f, TeamColGap = 2f;
        public const float CellPadCount = 5f;                       // 1.4: X1..X20 fit the team column (the dock keeps its size)
        public const float TabPad = 11f, TabText = 11f, TabTrack = 0.10f;
        public const float TeamText = 11f, TeamTrack = 0.08f;
        /// <summary>
        /// RED / BLUE / NEUTRAL a 36 u well (34 u cells: the old 30 / 28 + 20 %), 13.5 u labels (11 + 23 %), 8 u padding; the count row
        /// X1..X20 under it a 28 u well (its 11 u labels and its look kept), stretched to the same width; the column of the two centred on the
        /// dock's plate (2 u free above and below at 72 u), the plate keeps its height.</summary>
        public const float TeamSegH = 36f, TeamBigText = 13.5f, CellPadTeamBig = 8f, CountSegH = 28f;
        public const float TileLabelText = 11f, TileLabelTrack = 0f, TileLabelH = 13f, TileLabelGap = 1f;   // the dock's labels: letter spacing 0 (proto_c .dock .tile .lbl)
        /// <summary>How far a tile's label may reach past its cell on each side (u, at most half the tile gap less 1 u, so two neighbour
        /// labels stay 1 u apart): the game's font is wider than the prototype's label font, so ELECTRO and OBSTACLE keep the prototype's
        /// cells (48 and 53 u at 1920) instead of widening the dock; a label longer than that widens its cell.</summary>
        public const float LabelHang = 2f;
        /// <summary>1.5 ue_bar_r2: with the smaller tiles (Dock.labelsInCell) every label keeps this much room inside its cell on each
        /// side, so two labels stand a tile gap + 2 u apart (6 u at 40-44 u tiles) and never read as one word.</summary>
        public const float LabelPad = 1f;
        public const float ExtrasCaptionW = 11f, ExtrasCaptionGap = 5f;
        public const float PinH = 28f, PinPad = 8f, PinText = 12f, PinTrack = 0.12f;
        public const float RailH = 32f, RailPad = 14f, RailGap = 10f, RailText = 12f, RailTrack = 0.20f, RailKeyH = 20f;
        public const float PlayH = 38f, PlayPadV = 3f, PlayPadH = 4f, PlayGap = 5f, PlayBtnH = 30f, PlayAbove = 8f;
        public const float PlayText = 12f, PlayTrack = 0.14f, PlayIcon = 12f, PlayPadL = 11f, PlayPadR = 10f, PlayInner = 8f;
        public const float KeyText = 11f, KeyTrack = 0.1527f, KeyH = 18f, KeyPad = 5f;   // the play button's SPACE cap (letter spacing 1.68 px at 11 px)
        public const float BtnText = 12f, BtnTrack = 0.12f, BtnPad = 10f, SpeedText = 12f, SpeedTrack = 0.10f, SpeedPad = 10f;
        public const float ZoomW = 232f, ZoomH = 30f, ZoomPadL = 8f, ZoomPadR = 3f, ZoomGapIn = 4f, RulerW = 64f, ZoomBtn = 24f, ZoomPctMin = 31.9f;
        public const float ZoomText = 11f, ZoomTrack = 0.142f;     // the ruler's "5 M" and "100 %" (letter spacing 1.56 px at 11 px)
        public const float TileStd = 48f, TileShort = 46f, TileLarge = 56f, TileMin = 36f;
        /// <summary>1.5 ue_bar_r2: the smallest tile the dock takes to leave the zoom control its place beside it (E030). 1.5 has
        /// 10 tiles in VEHICLES (MAMMOTH, BIZON) and in SPECIAL (the seven explosions): at 1920 x 1080 the full 48 u tiles pushed the
        /// zoom one row up. Labels stay 11 u; a label wider than its tile keeps widening its cell as before.</summary>
        public const float TileBeside = 40f;

        public static readonly string[] TabWords = { "ROBOTS", "VEHICLES", "GUNS", "SPECIAL" };
        public static readonly string[] CountWords = { "X1", "X3", "X5", "X10", "X20" };
        public static readonly string[] SpeedWords = { "X1", "X2", "X4" };
        public const string PinWord = "PIN", RailWord = "UNITS", RailKey = "P", KeySpace = "SPACE";
        public const string ClearLong = "CLEAR FIELD", ClearShort = "CLEAR", SampleLong = "SAMPLE FIGHT", SampleShort = "SAMPLE", SoundOnWord = "SOUND ON", SoundOffWord = "SOUND OFF";
        public const string PauseWord = "PAUSE", PlayWord = "PLAY";

        /// <summary>LARGE (spec 2.3): CW >= 2200 and CH >= 1200 u.</summary>
        public static bool Large(float cw, float ch) => cw >= 2200f && ch >= 1200f;
        /// <summary>NARROW (spec 2.3): CW &lt; 1500 or CH &lt; 800 u.</summary>
        public static bool Narrow(float cw, float ch) => cw < 1500f || ch < 800f;

        /// <summary>What the layout needs to know.</summary>
        public sealed class In
        {
            public float cw = 1920f, ch = 1080f;
            public float insetL, insetR, insetT = 40f;
            public bool dockOpen = true, hidden;
            public string[] teamWords = { "RED", "BLUE", "NEUTRAL" };
            /// <summary>Per tab (robots, vehicles, guns, special) the tiles' labels.</summary>
            public string[][] tiles;
            public string flagLabel = "FLAG", obstacleLabel = "OBSTACLE", boxLabel = "BOX", grenadesLabel = "GRENADES", nukeLabel = "NUKE", platMidLabel = "PLATFORM", platHighLabel = "TOWER";   // 1.3 item 2: + NUKE; 1.3: + the two ramp platforms
            public Func<string, float, float, float> measure;
        }

        /// <summary>The dock (E040-E049): plate in canvas coordinates, every part relative to the plate's top-left.</summary>
        public sealed class Dock
        {
            public float tile, tileGap, sectionGap, tabPad;
            public UeBox plate, rail, railLabel, railKey;
            public float contentH;
            public UeBox teamWell, countWell, tabsWell;
            public float[] teamCells = new float[3], countCells = new float[CountWords.Length], tabCells = new float[4];
            public UeBox tileArea;
            public float[][] tileCells;                            // per tab, per tile: the cell's width (the art centred in it)
            public float extrasX, separatorX, captionX;
            public UeBox caption, flagTile, obstacleTile, boxTile, grenadesTile, nukeTile, platMidTile, platHighTile, pin;   // 1.5 ue_boxes: + BOX
            public bool narrow, large;
            /// <summary>1.5 ue_bar_r2: the tiles were made smaller to leave the zoom beside the dock: every tile label inside its own cell.</summary>
            public bool labelsInCell;
        }

        public sealed class Play
        {
            public UeBox plate, play, speed, clear, sample, sound;
            public float[] speedCells = new float[3];
            public float labelW, keyW;
            public string clearWord, sampleWord;
        }

        /// <summary>The zoom control: low = at the bottom beside the dock; stacked = one row over the play bar's left end (the bar had no
        /// room beside it: a small canvas, e.g. 1366 x 768 at UI SCALE 145 % and more); else at the play bar's height, left of it.</summary>
        public sealed class Zoom
        {
            public UeBox plate; public float pctW; public bool low, stacked; public float[] buttonX = new float[4];
            /// <summary>1.5 kit ue_bottom_left_r10 (FAILS A36): the control is the BACKGROUND popover's VIEW row (UI/OzUeBackdropMenu.cs, UI/OzUeZoomBar.cs),
            /// not in the bottom band: plate is empty (0 x 0, over nothing); rowW its width there, buttonX from its left edge as before.</summary>
            public bool inMenu; public float rowW;
            public string Where => inMenu ? "in the BACKGROUND popover's VIEW row" : low ? "beside the dock" : stacked ? "over the play bar's left end" : "at the play bar's height";
        }

        public sealed class Out
        {
            public Dock dock = new Dock();
            public Play play = new Play();
            public Zoom zoom = new Zoom();
            public float insetB;
        }

        static float W(In i, string s, float size, float tr) => i.measure != null ? i.measure(s, size, tr) : s.Length * size * (0.62f + tr);

        /// <summary>A letter-spaced caps label's width as prototype C draws its cell: CSS letter-spacing comes after the last letter too,
        /// TextMeshPro's width (the measure) stops at the last letter, so the label's own tracking is added once. The dock's and the
        /// play bar's cells then take the prototype's widths (E040 1046 u, E021 638 u at 1920 x 1080; they were 9 and 12 u narrower).</summary>
        static float Wc(In i, string s, float size, float tr) => W(i, s, size, tr) + (tr > 0f && !string.IsNullOrEmpty(s) ? tr * size : 0f);

        /// <summary>One segmented switch's cells: each the text + padding on both sides + its 1 u separator (not the last); the well = the
        /// sum + its 1 u edges. Widths stretched to `minWell` (flex: 1) when given.</summary>
        static float Seg(In i, string[] words, float size, float tr, float pad, float[] cells, float minWell = 0f)
        {
            float sum = 0f;
            for (int k = 0; k < words.Length; k++) { cells[k] = Wc(i, words[k], size, tr) + 2f * pad + (k < words.Length - 1 ? 1f : 0f); sum += cells[k]; }
            float well = sum + 2f * Border;
            if (minWell > well)
            {
                float extra = (minWell - well) / words.Length;
                for (int k = 0; k < words.Length; k++) cells[k] += extra;
                well = minWell;
            }
            return well;
        }

        /// <summary>The whole bottom band.</summary>
        public static Out Lay(In i)
        {
            var o = new Out();
            LayDock(i, o.dock);
            o.insetB = i.hidden ? 0f : i.dockOpen ? Gap + o.dock.plate.h : Gap + RailH;
            LayZoom(i, o);
            LayPlay(i, o);
            return o;
        }

        // ---------------------------------------------------------------------------------------------- the dock and its fit rule
        static void LayDock(In i, Dock d)
        {
            d.large = Large(i.cw, i.ch); d.narrow = Narrow(i.cw, i.ch);
            float avail = i.cw - 2f * Gap;
            float t = d.large ? TileLarge : i.ch < 800f ? TileShort : TileStd, tp = TabPad;
            // 1.5 ue_bar_r2 (tracker A13) shrank the tiles here (down to TileBeside) to leave the zoom control its place beside the dock. 1.5 kit ue_bottom_left_r10
            // (FAILS A36): the zoom control is the BACKGROUND popover's VIEW row now and nothing needs that place: the fit rule below alone, as
            // before ue_bar_r2 (the full 48 u tiles at 1920 x 1080, a label may hang LabelHang past its cell)
            d.labelsInCell = false;
            for (; t >= TileMin; t -= 2f) if (DockWidth(i, d, t, tp) <= avail + 0.01f) break;
            if (t < TileMin)
            {
                t = TileMin;
                for (tp = 8f; tp >= 4f; tp -= 2f) if (DockWidth(i, d, t, tp) <= avail + 0.01f) break;
                if (tp < 4f) { tp = 4f; DockWidth(i, d, t, tp); }
            }
            float w = DockWidth(i, d, t, tp);
            d.contentH = Math.Max(SegH * 2f + TeamColGap, t + TileLabelGap + TileLabelH);
            float h = 2f * Border + 2f * DockPadV + d.contentH;
            d.plate = new UeBox((i.cw - w) * 0.5f, i.ch - Gap - h, w, h);
            float top = Border + DockPadV, x = Border + DockPadL;
            float teamColW = Math.Max(d.teamWell.w, d.countWell.w);
            // 1.5 kit ue_bottom_left_r10 (FAILS A36): the 36 u team switch over the 28 u count row (66 u), centred on the plate (2 u free above and below
            // at 72 u); the plate keeps its height, so the stats box, the play bar and the bottom inset keep theirs
            float colH = TeamSegH + TeamColGap + CountSegH, colY = Border + (h - 2f * Border - colH) * 0.5f;
            d.teamWell = new UeBox(x, colY, teamColW, TeamSegH);
            d.countWell = new UeBox(x, colY + TeamSegH + TeamColGap, teamColW, CountSegH);
            x += teamColW + d.sectionGap;
            d.tabsWell = new UeBox(x, top + (d.contentH - SegH) * 0.5f, d.tabsWell.w, SegH);
            x += d.tabsWell.w + d.sectionGap;
            float palH = t + TileLabelGap + TileLabelH, palY = top + (d.contentH - palH) * 0.5f;
            d.tileArea = new UeBox(x, palY, d.tileArea.w, palH);
            x += d.tileArea.w;
            d.extrasX = x;
            d.separatorX = x + d.sectionGap;                                   // the 1 u KHAKI line (margin-left, then border-left)
            d.captionX = d.separatorX + Border + d.sectionGap;
            d.caption = new UeBox(d.captionX, palY, ExtrasCaptionW, palH);
            float fx = d.captionX + ExtrasCaptionW + ExtrasCaptionGap;
            d.flagTile = new UeBox(fx, palY, d.flagTile.w, palH);
            d.obstacleTile = new UeBox(fx + d.flagTile.w + d.tileGap, palY, d.obstacleTile.w, palH);
            d.boxTile = new UeBox(d.obstacleTile.Right + d.tileGap, palY, d.boxTile.w, palH);               // 1.5 ue_boxes: BOX after OBSTACLE
            d.grenadesTile = new UeBox(d.boxTile.Right + d.tileGap, palY, d.grenadesTile.w, palH);        // the third extra: GRENADES (1.5: after BOX)
            d.nukeTile = new UeBox(d.grenadesTile.Right + d.tileGap, palY, d.nukeTile.w, palH);            // 1.3 item 2: the fourth extra: NUKE
            d.platMidTile = new UeBox(d.nukeTile.Right + d.tileGap, palY, d.platMidTile.w, palH);          // 1.3: the two ramp platforms
            d.platHighTile = new UeBox(d.platMidTile.Right + d.tileGap, palY, d.platHighTile.w, palH);
            x = d.platHighTile.Right + d.sectionGap;
            d.pin = new UeBox(x, top + (d.contentH - PinH) * 0.5f, d.pin.w, PinH);
            // the rail of the closed dock (E046): "UNITS" + the P key cap, centred at the bottom
            float lw = Wc(i, RailWord, RailText, RailTrack), kw = Math.Max(RailKeyH, W(i, RailKey, KeyText, 0f) + 2f * KeyPad + 2f * Border);
            float rw = 2f * Border + 2f * RailPad + lw + RailGap + kw;
            d.rail = new UeBox((i.cw - rw) * 0.5f, i.ch - Gap - RailH, rw, RailH);
            d.railLabel = new UeBox(Border + RailPad, 0f, lw, RailH);
            d.railKey = new UeBox(Border + RailPad + lw + RailGap, (RailH - RailKeyH) * 0.5f, kw, RailKeyH);
        }

        /// <summary>One tile's cell (E047, proto_c `.dock .tile { min-width: var(--dt) }`): the tile side, or wider when the label (centred
        /// under the picture) would reach more than LabelHang past it on a side (at most half of the gap less 1 u).</summary>
        /// <summary>1.5 ue_bar_r2: a cell of the dock: Cell as before, or with the smaller tiles the label inside it with LabelPad u on
        /// each side (no hang into the gap).</summary>
        static float CellOf(In i, Dock d, string label, float tile, float gap) =>
            d.labelsInCell ? Math.Max(tile, W(i, label ?? "", TileLabelText, TileLabelTrack) + 2f * LabelPad) : Cell(i, label, tile, gap);

        public static float Cell(In i, string label, float tile, float gap)
        {
            float hang = Math.Min(LabelHang, Math.Max(0f, (gap - 1f) * 0.5f));
            return Math.Max(tile, W(i, label ?? "", TileLabelText, TileLabelTrack) - 2f * hang);
        }

        /// <summary>The dock's width for tile side t and tab padding tp (sets the parts' widths on d).</summary>
        static float DockWidth(In i, Dock d, float t, float tp)
        {
            d.tile = t; d.tileGap = t >= 46f ? 5f : 4f; d.sectionGap = t >= 46f ? 10f : 8f; d.tabPad = tp;
            float tw = Seg(i, i.teamWords, TeamBigText, TeamTrack, Math.Min(CellPadTeamBig, tp), d.teamCells);            // 1.5 kit ue_bottom_left_r10 (A36): the bigger team switch; its padding never over the tab padding (the last fit step: 8, 6, 4 u), so the dock is no wider at its floor
            float cwell = Seg(i, CountWords, TeamText, TeamTrack, CellPadCount, d.countCells);
            float col = Math.Max(tw, cwell);
            Seg(i, i.teamWords, TeamBigText, TeamTrack, Math.Min(CellPadTeamBig, tp), d.teamCells, col);
            Seg(i, CountWords, TeamText, TeamTrack, CellPadCount, d.countCells, col);
            d.teamWell.w = col; d.countWell.w = col;
            d.tabsWell.w = Seg(i, TabWords, TabText, TabTrack, tp, d.tabCells);
            float pw = 0f;
            int n = i.tiles != null ? i.tiles.Length : 0;
            if (d.tileCells == null || d.tileCells.Length != n) d.tileCells = new float[n][];
            for (int k = 0; k < n; k++)
            {
                var labels = i.tiles[k] ?? new string[0];
                if (d.tileCells[k] == null || d.tileCells[k].Length != labels.Length) d.tileCells[k] = new float[labels.Length];
                float s = 0f;
                for (int j = 0; j < labels.Length; j++) { d.tileCells[k][j] = CellOf(i, d, labels[j], t, d.tileGap); s += d.tileCells[k][j] + (j > 0 ? d.tileGap : 0f); }
                pw = Math.Max(pw, s);
            }
            d.tileArea.w = pw;                                                 // the widest tab: the dock never moves when the tab changes
            // the extras' labels (OBSTACLE, GRENADES) are wider than their tiles and stand side by side: each keeps inside its own cell
            // (no hang into the gap), so a full tile gap stays between them and they never read as one word
            d.flagTile.w = CellOf(i, d, i.flagLabel, t, 1f);
            d.obstacleTile.w = CellOf(i, d, i.obstacleLabel, t, 1f);
            d.boxTile.w = CellOf(i, d, i.boxLabel, t, 1f);                                                        // 1.5 ue_boxes
            d.grenadesTile.w = CellOf(i, d, i.grenadesLabel, t, 1f);
            d.nukeTile.w = CellOf(i, d, i.nukeLabel, t, 1f);
            d.platMidTile.w = CellOf(i, d, i.platMidLabel, t, 1f);
            d.platHighTile.w = CellOf(i, d, i.platHighLabel, t, 1f);
            float extras = d.sectionGap + Border + d.sectionGap + ExtrasCaptionW + ExtrasCaptionGap + d.flagTile.w + d.tileGap + d.obstacleTile.w + d.tileGap + d.boxTile.w + d.tileGap + d.grenadesTile.w
                           + d.tileGap + d.nukeTile.w                                        // 1.3 item 2: + NUKE
                           + d.tileGap + d.platMidTile.w + d.tileGap + d.platHighTile.w;     // 1.3: + the two ramp platforms
            d.pin.w = Wc(i, PinWord, PinText, PinTrack) + 2f * PinPad + 2f * Border;
            return 2f * Border + DockPadL + col + d.sectionGap + d.tabsWell.w + d.sectionGap + pw + extras + d.sectionGap + d.pin.w + DockPadR;
        }

        // ---------------------------------------------------------------------------------------------- zoom / scale / fit (E030-E036)
        /// <summary>1.5 ue_bar_r2: the zoom control's width (the same sum as LayZoom's).</summary>
        public static float ZoomWidth(In i)
        {
            float pct = Math.Max(ZoomPctMin, W(i, "100 %", ZoomText, ZoomTrack));
            return Border + ZoomPadL + RulerW + ZoomGapIn + pct + 2f + ZoomGapIn + 3f * (ZoomBtn + ZoomGapIn) + ZoomBtn + ZoomPadR + Border;
        }

        static void LayZoom(In i, Out o)
        {
            var z = o.zoom;
            z.pctW = Math.Max(ZoomPctMin, W(i, "100 %", ZoomText, ZoomTrack));
            float bx = Border + ZoomPadL + RulerW + ZoomGapIn + z.pctW + 2f + ZoomGapIn;
            for (int k = 0; k < 4; k++) z.buttonX[k] = bx + k * (ZoomBtn + ZoomGapIn);
            float w = z.buttonX[3] + ZoomBtn + ZoomPadR + Border;
            // The control is the BACKGROUND popover's VIEW row (UI/OzUeBackdropMenu.cs, UI/OzUeZoomBar.cs) and takes no room
            // in the bottom band (an empty plate over nothing); the same buttons, the same keys (Home, the wheel, the middle-button turn)
            z.inMenu = true; z.low = false; z.stacked = false; z.rowW = w;
            z.plate = new UeBox(0f, 0f, 0f, 0f);
        }

        // ---------------------------------------------------------------------------------------------- the play bar (E021-E027)
        static void LayPlay(In i, Out o)
        {
            var p = o.play;
            bool narrow = Narrow(i.cw, i.ch);
            p.clearWord = narrow ? ClearShort : ClearLong; p.sampleWord = narrow ? SampleShort : SampleLong;
            p.labelW = Math.Max(Wc(i, PauseWord, PlayText, PlayTrack), Wc(i, PlayWord, PlayText, PlayTrack));   // one width for both states: nothing moves on Space
            p.keyW = Wc(i, KeySpace, KeyText, KeyTrack) + 2f * KeyPad + 2f * Border;
            float pw = 2f * Border + PlayPadL + PlayIcon + PlayInner + p.labelW + PlayInner + p.keyW + PlayPadR;
            float sw = Seg(i, SpeedWords, SpeedText, SpeedTrack, SpeedPad, p.speedCells);
            float cw = Wc(i, p.clearWord, BtnText, BtnTrack) + 2f * BtnPad + 2f * Border;
            float smw = Wc(i, p.sampleWord, BtnText, BtnTrack) + 2f * BtnPad + 2f * Border;
            float sndw = Math.Max(Wc(i, SoundOnWord, BtnText, BtnTrack), Wc(i, SoundOffWord, BtnText, BtnTrack)) + 2f * BtnPad + 2f * Border;
            float y = Border + PlayPadV, x = Border + PlayPadH;
            p.play = new UeBox(x, y, pw, PlayBtnH); x += pw + PlayGap;
            p.speed = new UeBox(x, y, sw, PlayBtnH); x += sw + PlayGap;
            p.clear = new UeBox(x, y, cw, PlayBtnH); x += cw + PlayGap;
            p.sample = new UeBox(x, y, smw, PlayBtnH); x += smw + PlayGap;
            p.sound = new UeBox(x, y, sndw, PlayBtnH); x += sndw + PlayPadH + Border;
            float barW = x;
            // E027: centred on the free field; right of the zoom control when that sits at the same height; never under the stats box.
            // When the bar has no room beside the zoom control (a small canvas: 1366 x 768 at UI SCALE 145 % and more, 1600 x 900,
            // 1536 x 730 and 2560 x 1600 at 160 %), the zoom goes one row up, over the bar's left end, and the bar takes the free field's width
            float px = i.insetL + (i.cw - i.insetL - i.insetR) * 0.5f - barW * 0.5f;
            float free1 = i.cw - i.insetR - Gap;
            o.zoom.stacked = !o.zoom.inMenu && !o.zoom.low && free1 - (i.insetL + Gap + o.zoom.plate.w + Gap) < barW;   // 1.5 kit ue_bottom_left_r10: never (the zoom is in the popover)
            float free0 = i.insetL + Gap + (o.zoom.inMenu || o.zoom.low || o.zoom.stacked ? 0f : o.zoom.plate.w + Gap);
            if (px < free0) px = free0;
            if (px + barW > free1) px = Math.Max(i.insetL + Gap, free1 - barW);
            p.plate = new UeBox(px, i.ch - (o.insetB + PlayAbove) - PlayH, barW, PlayH);
            if (o.zoom.stacked) o.zoom.plate = new UeBox(o.zoom.plate.x, p.plate.y - PlayAbove - ZoomH, o.zoom.plate.w, ZoomH);
        }
    }
}
