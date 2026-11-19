// 1.2 item 6, View/OzUnitEditor.cs: the Unit editor's pure plans and texts for the top bar's RESET STATS, the leave question and its two
// confirms, the two-line world words, the stats box's line on which stats are used, the inspector's line by the Custom column, the help
// card's stats words, the toasts of saving and SAMPLE FIGHT's spots. Nothing of the engine is used here: the off-engine layout model
// compiles this file alone with the game font's metrics, and the editor runs the same code with TextMeshPro's widths (OzUeShell.TextW).
using System;
using System.Globalization;

namespace OpenZ
{
    /// <summary>A rect in canvas units, top-left origin.</summary>
    public struct UeR
    {
        public float x, y, w, h;
        public UeR(float x, float y, float w, float h) { this.x = x; this.y = y; this.w = w; this.h = h; }
        public float R => x + w;
        public float B => y + h;
        /// <summary>The two rects share more than a hair (0.01 u each way).</summary>
        public bool Overlaps(UeR o) => Math.Min(R, o.R) - Math.Max(x, o.x) > 0.01f && Math.Min(B, o.B) - Math.Max(y, o.y) > 0.01f;
        /// <summary>This rect lies inside o, `margin` u from its edges at least.</summary>
        public bool Inside(UeR o, float margin) => x >= o.x + margin - 0.01f && y >= o.y + margin - 0.01f && R <= o.R - margin + 0.01f && B <= o.B - margin + 0.01f;
        public override string ToString() => x.ToString("0.0", CultureInfo.InvariantCulture) + "," + y.ToString("0.0", CultureInfo.InvariantCulture) + " "
                                             + w.ToString("0.0", CultureInfo.InvariantCulture) + "x" + h.ToString("0.0", CultureInfo.InvariantCulture);
    }

    public static class OzUePlan
    {
        /// <summary>A text's width in canvas units at a size (u) and a tracking (em).</summary>
        public delegate float Measure(string text, float size, float tracking);

        static float Ceil(float v) => (float)Math.Ceiling(v);
        static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>The lines a text takes wrapped at `width` (greedy at spaces, as TextMeshPro wraps; rich-text tags take no room).</summary>
        public static int WrapLines(string s, float size, float width, Measure m)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            var words = s.Split(' '); int lines = 0; string cur = "";
            foreach (var wd in words)
            {
                string t = cur.Length == 0 ? wd : cur + " " + wd;
                if (cur.Length == 0 || m(t, size, 0f) <= width) cur = t; else { lines++; cur = wd; }
            }
            return lines + (cur.Length > 0 ? 1 : 0);
        }

        // ================================================================ the top bar (UI/OzUeTopBar.cs, DESIGN_SPEC E001-E009)
        public const float BarH = 40f, BarHCompact = 36f, LogoX = 16f, LogoGap = 14f, Right = 12f, GroupGap = 6f, GroupMargin = 6f, Gap = 14f, BtnH = 28f;
        public const float LogoAspect = 100f / 26f;                                                          // Wordmarks/wm_openz, size_u
        /// <summary>
        /// A square button as tall as the bar's buttons, Right u
        /// from the canvas' right edge; MAIN MENU and the rest of the right group stand EyeSize + EyeGap further left.</summary>
        public const float EyeSize = BtnH, EyeGap = 10f;
        /// <summary>The eye button's rect (canvas units, top-left origin) on a canvas cw wide, in the bar of that height (compact: 36 u).</summary>
        public static UeR Eye(float cw, bool compact) { float h = compact ? BarHCompact : BarH; return new UeR(cw - Right - EyeSize, (h - 1f - BtnH) * 0.5f, EyeSize, BtnH); }
        public const string ResetLabel = "RESET STATS";
        /// <summary>RESET STATS stands this far left of SAVE: in the file group, a little apart from it.</summary>
        public const float ResetGap = GroupGap + GroupMargin;

        public struct Top
        {
            public float cw, h, logoW, logoH, logoY, crumbX, crumbW, btnY;
            public bool crumbLong;
            public UeR reset, save, load, share, help, menu;
        }

        /// <summary>A top bar / dialog button (HudButtonKind.Small, 12 u caps, tracking 0.12) as wide as its text + padding (OzUeShell.Button).</summary>
        public static float ButtonW(string t, Measure m, float padX) => Ceil(m(t, 12f, 0.12f) + 2f * padX + 2f);

        /// <summary>Where everything of the bar goes (the rule of DESIGN_SPEC E001-E009, as before), with RESET STATS left of SAVE.</summary>
        public static Top TopBar(float cw, float ch, bool compact, string crumb, string crumbLong, Measure m)
        {
            var p = new Top { cw = cw, h = compact ? BarHCompact : BarH };
            p.logoH = p.h - 14f; p.logoW = p.logoH * LogoAspect; p.logoY = (p.h - 1f - p.logoH) * 0.5f;
            p.crumbX = LogoX + p.logoW + LogoGap;
            p.crumbLong = cw >= 1700f && ch >= 800f;
            p.crumbW = m(p.crumbLong ? crumbLong : crumb, 12f, 0.16f);
            p.btnY = (p.h - 1f - BtnH) * 0.5f;                                                                 // centred over the bottom line, 5.5 at 40 u
            float mw = ButtonW("MAIN MENU", m, 10f), x = cw - Right - EyeSize - EyeGap - mw;                     // ue_names_eye: the eye button takes the corner (Eye above)
            p.menu = new UeR(x, p.btnY, mw, BtnH);
            x -= Gap + BtnH; p.help = new UeR(x, p.btnY, BtnH, BtnH);
            x -= Gap + GroupMargin;
            float sw = ButtonW("SHARE", m, 8f); x -= sw; p.share = new UeR(x, p.btnY, sw, BtnH);
            float lw = ButtonW("LOAD", m, 8f); x -= GroupGap + lw; p.load = new UeR(x, p.btnY, lw, BtnH);
            float vw = ButtonW("SAVE", m, 8f); x -= GroupGap + vw; p.save = new UeR(x, p.btnY, vw, BtnH);
            float rw = ButtonW(ResetLabel, m, 8f); x -= ResetGap + rw; p.reset = new UeR(x, p.btnY, rw, BtnH);
            return p;
        }

        /// <summary>The bar holds: every button inside it, none over another, RESET STATS ResetGap left of SAVE, the crumb ending Gap u
        /// or more before RESET STATS, the wordmark before the crumb. why = the first problem.</summary>
        public static bool TopOk(Top p, out string why)
        {
            why = null;
            var bar = new UeR(0f, 0f, p.cw, p.h - 1f);
            var b = new[] { p.reset, p.save, p.load, p.share, p.help, p.menu, Eye(p.cw, p.h < BarH) };              // ue_names_eye: + the eye button
            string[] n = { ResetLabel, "SAVE", "LOAD", "SHARE", "?", "MAIN MENU", "the eye button" };
            for (int i = 0; i < b.Length; i++)
            {
                if (!b[i].Inside(bar, 0f)) { why = n[i] + " " + b[i] + " is not inside the bar"; return false; }
                for (int j = i + 1; j < b.Length; j++) if (b[i].Overlaps(b[j])) { why = n[i] + " lies over " + n[j]; return false; }
            }
            if (Math.Abs(p.reset.R + ResetGap - p.save.x) > 0.01f) { why = ResetLabel + " is not " + F(ResetGap) + " u left of SAVE"; return false; }
            if (p.crumbX + p.crumbW + Gap > p.reset.x + 0.01f) { why = "the crumb (to " + F(p.crumbX + p.crumbW) + ") runs into " + ResetLabel + " at " + F(p.reset.x); return false; }
            if (LogoX + p.logoW + LogoGap > p.crumbX + 0.01f) { why = "the wordmark runs into the crumb"; return false; }
            return true;
        }

        // ================================================================ the old dialogs' plan (1.2 item 6; Place is the new windows' rule too)
        public const float DlgW = 640f, LeaveW = 780f, DlgHead = 36f, DlgPadX = 20f, DlgTop = 18f, DlgBottom = 20f, ParaGap = 10f, RowGap = 16f, BtnRowH = 32f,
                           BtnGap = 8f, BtnPad = 12f, KeysGap = 10f, KeysH = 16f, LeftGap = 24f;
        public const float TitleSize = 13f, Para1Size = 14f, ParaSize = 13f, KeysSize = 12f;

        // ================================================================ 1.2 item 109: the windows' words, short and plain as a game's menus
        // UI/OzUeLeave.cs and UI/OzUeDialogs.cs, their plan UI/OzUeWinPlan.cs)
        /// <summary>The small stencil word over every window's title (the game's own confirm says CONFIRM there).</summary>
        public const string Stencil = "UNIT EDITOR";
        /// <summary>The keys on the buttons' caps: Esc on the safe choice, Enter on the main button.</summary>
        public const string KeyEsc = "ESC", KeyEnter = "ENTER";

        // ---- LEAVE THE UNIT EDITOR? (MAIN MENU at the top right, or Esc with nothing else open)
        public const string LeaveTitle = "LEAVE THE UNIT EDITOR?";
        /// <summary>The question's one line, only while Custom changes are not saved (LEAVE then drops them).</summary>
        public const string NotSaved = "Your Custom stats are not saved.";
        public const string Stay = "STAY", NoSave = "LEAVE", SaveAndGo = "SAVE & LEAVE";

        // ---- RESET ALL CUSTOM STATS? (RESET STATS in the top bar only)
        public const string ResetTitle = "RESET ALL CUSTOM STATS?";
        public const string ResetLine = "Every unit goes back to OpenZ's own numbers.";
        public const string Cancel = "CANCEL", ResetNow = "RESET";
        public const string NothingToReset = "NOTHING TO RESET";
        public const string ResetDone = "ALL STATS BACK TO OPENZ";

        // ---- the toast of every save (SAVE, SHARE, SAVE & LEAVE: UI/OzUeDialogs.cs SavedToast), the file's name small beside it
        public const string SavedToast = "SAVED";

        // ---- the toasts of a Custom change (View/OzUeSets.Reset.cs EditToast / UnitToast): one line on the narrowest canvas
        /// <summary>The tail when the change switched the field from another set to Custom.</summary>
        public const string FieldNowCustom = " · FIELD NOW ON CUSTOM";

        /// <summary>A typed Custom value ("EVERY JEEP, BOTH TEAMS: CUSTOM HEALTH 2000 HP"): unit = the unit's name, stat = the stat's name,
        /// val = the value with its unit; the squad size counts for squads placed from now on; the build time counts in the levels (nothing
        /// is built here); back = the Custom value put back to the base set `baseSet` by a right-click (that never switches the field's set,
        /// so its words name the Custom value); switched = it switched the field to Custom.</summary>
        public static string EditToast(string unit, string stat, string val, bool squad, bool build, bool back, string baseSet, bool switched)
        {
            string t;
            if (back) t = "EVERY " + unit + ", BOTH TEAMS: CUSTOM " + stat + " BACK TO " + baseSet + " (" + val + ")";
            else if (squad) t = "NEW " + unit + " SQUADS, BOTH TEAMS: " + val + " ROBOTS. SQUADS ON THE FIELD KEEP THEIR SIZE";
            else if (build) t = "EVERY " + unit + ": CUSTOM BUILD TIME " + val + ". IT COUNTS IN LEVELS (NOT BUILT HERE)";
            else t = "EVERY " + unit + ", BOTH TEAMS: CUSTOM " + stat + " " + val;
            return (t + (switched ? FieldNowCustom : "")).ToUpperInvariant();
        }

        /// <summary>A whole unit's Custom values changed in the inspector ("Custom from ZodEngine", Reset unit).</summary>
        public static string UnitToast(string unit, string what, bool switched) => ("EVERY " + unit + ", BOTH TEAMS: " + what + (switched ? FieldNowCustom : "")).ToUpperInvariant();

        public struct Dlg
        {
            public float w, h;
            public UeR title;
            public UeR[] paras, buttons;
            public string[] labels;
            public int left;
            public UeR keys;
        }

        /// <summary>A 640 u dialog (see the other Dialog).</summary>
        public static Dlg Dialog(float[] paraH, string[] labels, int left, Measure m) => Dialog(DlgW, paraH, labels, left, m);

        /// <summary>One dialog on its plate `w` u wide (plate units): the head with the title, the paragraphs one under the other (their
        /// heights as measured: TextMeshPro's wrapped height), one row of buttons (the first `left` from the body's left edge, the others
        /// from its right edge in order), the keys line under the row.</summary>
        public static Dlg Dialog(float w, float[] paraH, string[] labels, int left, Measure m)
        {
            var d = new Dlg { w = w, paras = new UeR[paraH.Length], buttons = new UeR[labels.Length], labels = labels, left = left };
            float bw = w - 2f * DlgPadX - 2f, x = DlgPadX + 1f, y = DlgHead + DlgTop;
            d.title = new UeR(12f, 0f, w - 24f, DlgHead);
            for (int i = 0; i < paraH.Length; i++) { d.paras[i] = new UeR(x, y, bw, paraH[i]); y += paraH[i] + (i < paraH.Length - 1 ? ParaGap : 0f); }
            y += RowGap;
            float lx = x;
            for (int i = 0; i < left && i < labels.Length; i++) { float bt = ButtonW(labels[i], m, BtnPad); d.buttons[i] = new UeR(lx, y, bt, BtnRowH); lx += bt + BtnGap; }
            float rx = x + bw;
            for (int i = labels.Length - 1; i >= left; i--) { float bt = ButtonW(labels[i], m, BtnPad); rx -= bt; d.buttons[i] = new UeR(rx, y, bt, BtnRowH); rx -= BtnGap; }
            y += BtnRowH + KeysGap;
            d.keys = new UeR(x, y, bw, KeysH);
            y += KeysH;
            d.h = Ceil(y + DlgBottom);
            return d;
        }

        /// <summary>The body width of a dialog `w` u wide (its paragraphs' wrap width).</summary>
        public static float BodyW(float w) => w - 2f * DlgPadX - 2f;

        /// <summary>The plate on the canvas: centred, 16 u from the edges at least (OzUeDialogs.Plan's rule); scale under 1 only when the
        /// canvas is narrower than the plate + 32 u.</summary>
        public static UeR Place(float cw, float ch, float w, float h, out float scale)
        {
            float pw = Math.Min(w, cw - 32f);
            scale = Math.Min(1f, pw / w);
            return new UeR((cw - pw) * 0.5f, Math.Max(16f, (ch - h * scale) * 0.5f), pw, h * scale);
        }

        /// <summary>The dialog holds on a canvas: the plate full size and 16 u inside the canvas, every button inside the body and none
        /// over another, the left buttons LeftGap u or more before the right ones, the paragraphs over the row and inside the body, the
        /// keys line under the row. why = the first problem.</summary>
        public static bool DialogOk(Dlg d, float cw, float ch, out string why)
        {
            why = null;
            var r = Place(cw, ch, d.w, d.h, out float k);
            if (k < 0.999f) { why = "the plate is scaled to " + F(k * 100f) + " %"; return false; }
            if (!r.Inside(new UeR(0f, 0f, cw, ch), 16f)) { why = "the plate " + r + " is not 16 u inside the canvas " + F(cw) + " x " + F(ch); return false; }
            var body = new UeR(DlgPadX + 1f - 0.01f, DlgHead, BodyW(d.w) + 0.02f, d.h - DlgHead);
            float rowY = d.buttons.Length > 0 ? d.buttons[0].y : d.h;
            for (int i = 0; i < d.buttons.Length; i++)
            {
                if (!d.buttons[i].Inside(body, 0f)) { why = d.labels[i] + " " + d.buttons[i] + " is not inside the body"; return false; }
                for (int j = i + 1; j < d.buttons.Length; j++) if (d.buttons[i].Overlaps(d.buttons[j])) { why = d.labels[i] + " lies over " + d.labels[j]; return false; }
                if (Math.Abs(d.buttons[i].y - rowY) > 0.01f) { why = d.labels[i] + " is not on the row"; return false; }
            }
            if (d.left > 0 && d.left < d.buttons.Length && d.buttons[d.left].x - d.buttons[d.left - 1].R < LeftGap - 0.01f)
            { why = d.labels[d.left - 1] + " and " + d.labels[d.left] + " are " + F(d.buttons[d.left].x - d.buttons[d.left - 1].R) + " u apart (" + F(LeftGap) + " wanted)"; return false; }
            foreach (var p in d.paras)
            {
                if (!p.Inside(body, 0f)) { why = "a paragraph " + p + " is not inside the body"; return false; }
                if (p.B > rowY - RowGap + 0.01f) { why = "a paragraph reaches the button row"; return false; }
            }
            if (d.keys.y < rowY + BtnRowH - 0.01f || d.keys.B > d.h - DlgBottom + 0.01f) { why = "the keys line " + d.keys + " is not under the row"; return false; }
            return true;
        }

        // ================================================================ the two-line world words (View/OzUeMarks.cs)
        /// <summary>The hint (the second line) keeps the label's old height over its point; the name and its HP one line (16 u) over it.</summary>
        public const float WordsLine = 16f, WordsSize = 12f;
        public const string ObstacleHint = "Ctrl + right-click attacks it", FlagHint = "Robots take it by touch", EmptyHint = "A robot can climb in";
        /// <summary>The obstacle's 40 x 3 u bar, 8 u over the obstacle's top (its 1 u dark frame round it), and its words' height.</summary>
        public const float ObstacleBarUp = 8f, ObstacleBarH = 3f, ObstacleWordsUp = 14f, FlagWordsUp = 6f, EmptyWordsUp = 14f;

        /// <summary>A world label's letters (caps) from its box bottom `up` u over its point: the box is 1.5 x the size (OzUeDraw), the caps
        /// (0.727 em, Inter Tight) in its middle.</summary>
        public static void Glyphs(float up, float size, out float lo, out float hi)
        {
            float box = 1.5f * size, cap = 0.727f * size;
            lo = up + (box - cap) * 0.5f; hi = lo + cap;
        }

        /// <summary>The two lines and the bar under them: the hint's letters 1 u or more over the bar (and its frame), the first line's letters
        /// 3 u or more over the hint's. barH 0 = no bar. why = the problem.</summary>
        public static bool WordsOk(float hintUp, float barUp, float barH, out string why)
        {
            why = null;
            Glyphs(hintUp, WordsSize, out float hLo, out float hHi);
            Glyphs(hintUp + WordsLine, WordsSize, out float tLo, out float tHi);
            if (barH > 0f && hLo < barUp + barH + 2f + 1f) { why = "the hint's letters (from " + F(hLo) + " u) touch the bar (to " + F(barUp + barH + 2f) + " u)"; return false; }
            if (tLo < hHi + 3f) { why = "the two lines' letters are " + F(tLo - hHi) + " u apart"; return false; }
            return true;
        }

        // ================================================================ the stats box's line on which stats are used (UI/OzUeScope.cs)
        // Every wording names the level screen: right under the set switch a bare "pick Custom" would read as its Custom cell, which
        // changes only this field.
        public static string ScopeLong(string set) => "In use here: " + set + ". Levels: OpenZ, or Custom on the level screen.";
        public static string ScopeMedium(string set) => "Here: " + set + ". Levels: OpenZ, or Custom on the level screen.";
        public static string ScopeShort(string set) => "Here: " + set + ". Levels: OpenZ or Custom (level screen).";
        public const string ScopeTip = "Which stats are used|Here: the set picked above, for every unit on the field, both teams. A Custom change is used at once (the field then runs Custom).|"
                                       + "In the levels: OpenZ, the game's own numbers, unless you pick Custom on the level screen; then the Custom stats you saved (SAVE).|"
                                       + "To reset: RESET STATS at the top (every unit), Reset unit in a unit's stats, or a right-click on one Custom value.";
        public const float ScopeSize = 12f, ScopeSizeCompact = 11f;
        /// <summary>The set switch's cells for the line under them (the body below starts where it did): 24 u, compact 20 u.</summary>
        public static float CellH(bool compact) => compact ? 20f : 24f;
        /// <summary>The line's middle: halfway down the band between the cells and the body.</summary>
        public static float ScopeMid(float setTop, float bodyTop, bool compact) => setTop + CellH(compact) + (bodyTop - setTop - CellH(compact)) * 0.5f;
        /// <summary>The wording that fits `width` (the long one, else the middle one, else the short one); fits = false when none does.</summary>
        public static string ScopeFor(string set, float width, float size, Measure m, out bool fits)
        {
            string a = ScopeLong(set);
            if (m(a, size, 0f) <= width) { fits = true; return a; }
            string b = ScopeMedium(set);
            if (m(b, size, 0f) <= width) { fits = true; return b; }
            string c = ScopeShort(set);
            fits = m(c, size, 0f) <= width;
            return c;
        }

        // ================================================================ the inspector's line by the Custom column (UI/OzUeInspector.cs)
        /// <summary>Two lines at most (12 u, compact 11 u, the body's line height 1.35 em) between the stats grid and its foot: whom a
        /// Custom change reaches and when the levels use it. The long wording takes two lines for every unit at the 7 design sizes x the 6
        /// UI SCALE steps with room to spare; the two shorter ones stay for a text measured wider than the plan's.</summary>
        public static string NoteLong(string unit) => "Changes reach every " + unit + " here, both teams, at once. Levels: SAVE, then pick Custom on the level screen.";
        public static string NoteMid(string unit) => "Changes reach every " + unit + " here, both teams, at once. Levels: SAVE, then Custom on the level screen.";
        public static string NoteShort(string unit) => "Changes: every " + unit + " here, both teams, at once. Levels: SAVE, then Custom on the level screen.";
        public const float NoteSize = 12f, NoteSizeCompact = 11f, NotePadTop = 6f, NotePadBottom = 6f;
        public static float NoteTextSize(bool compact) => compact ? NoteSizeCompact : NoteSize;
        /// <summary>The band the line takes (two lines and its padding), u.</summary>
        public static float NoteH(bool compact) { float s = NoteTextSize(compact); return NotePadTop + Ceil((1.35f + 1.211f) * s) + NotePadBottom; }
        /// <summary>The wording for this unit and width; lines = the lines it takes (2 at most when it fits).</summary>
        public static string NoteFor(string unit, float width, bool compact, Measure m, out int lines)
        {
            float s = NoteTextSize(compact);
            string a = NoteLong(unit); lines = WrapLines(a, s, width, m); if (lines <= 2) return a;
            string b = NoteMid(unit); lines = WrapLines(b, s, width, m); if (lines <= 2) return b;
            string c = NoteShort(unit); lines = WrapLines(c, s, width, m); return c;
        }

        // ================================================================ the help card's stats words (UI/OzUeHelpCard.cs)
        /// <summary>One line under the keys (12 u, the card's full width): which stats the levels use.</summary>
        public const string HelpStats = "Levels play OpenZ, or your saved Custom stats if picked on the level screen.";
        public const float HelpStatsSize = 12f;
        public const string HelpEsc = "Close, else main menu";

        // ================================================================ SAMPLE FIGHT's spots (View/OzUePlayground.Cap.cs)
        /// <summary>Blue on the left of the (turned) view, red on the right: rows 6.5 m apart along the view's depth (spec E025).</summary>
        public static readonly string[] SampleBlue = { "grunt", "psycho", "light", "gatling", "sniper", "jeep" }, SampleRed = { "grunt", "tough", "medium", "pyro", "howitzer", "laser" };
        /// <summary>Across the view (m, + = right): blue -(6 + 3 (i mod 2)), the gatling 5 m further back; red 6 + 3 ((i + 1) mod 2), the howitzer 8 m further back.</summary>
        public static float SampleAcross(bool blue, int i) => blue ? -(6f + 3f * (i % 2)) - (SampleBlue[i] == "gatling" ? 5f : 0f) : 6f + 3f * ((i + 1) % 2) + (SampleRed[i] == "howitzer" ? 8f : 0f);
        /// <summary>Along the view's depth (m): (i - 2.5) x 6.5, the red row 1.5 m further up.</summary>
        public static float SampleAlong(bool blue, int i) => (i - 2.5f) * 6.5f + (blue ? 0f : 1.5f);
        /// <summary>The grid of fight centres round the view's middle: one fight with the room of its groups fits one cell.</summary>
        public const float SampleStep = 44f;
        /// <summary>The clear room a group needs round its spot (m), and the room a standing object takes (m).</summary>
        public static float SpotRoom(bool robot) => robot ? 3f : 3.5f;
        public const float RobotRoom = 1f, HardwareRoom = 2.5f, ShuttleRoom = 4f, FlagRoom = 1.5f, ObstacleRoom = 4.3f;

        /// <summary>The fight's groups with their room fit one SampleStep cell (so two fights on the grid never touch). why = the extent.</summary>
        public static bool SampleFits(Func<string, bool> robot, out string why)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            for (int s = 0; s < 2; s++)
                for (int i = 0; i < 6; i++)
                {
                    bool blue = s == 0; string id = blue ? SampleBlue[i] : SampleRed[i]; float r = SpotRoom(robot(id));
                    float a = SampleAcross(blue, i), b = SampleAlong(blue, i);
                    x0 = Math.Min(x0, a - r); x1 = Math.Max(x1, a + r); z0 = Math.Min(z0, b - r); z1 = Math.Max(z1, b + r);
                }
            why = "across " + F(x0) + " .. " + F(x1) + " m, along " + F(z0) + " .. " + F(z1) + " m in a " + F(SampleStep) + " m cell";
            return x1 - x0 <= SampleStep && z1 - z0 <= SampleStep;
        }
    }
}
