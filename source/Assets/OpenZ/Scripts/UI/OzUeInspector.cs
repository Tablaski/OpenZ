// 1.2 item 6, View/OzUnitEditor.cs: the inspector of one unit in the STATS box (DESIGN_SPEC E080-E100; owner 6g: it opens on a left OR
// right click on a unit). Head: back, the picture (drags onto the field), the name (+ OPENZ ONLY), "<class>, <n> on field", the live line
// of the picked unit (team, HP and its bar), REMOVE. Right under the name, what the unit is: the game's own build-menu words
// (UI/OzUnitFacts.cs, word for word; owner 6d / 6j / 6k: no caption, no button). The grid: the 14 stats with full names and small units
// (build time first, owner 6e; never cut, owner 6k), OpenZ / ZodEngine / Z-Wiki / Custom (inputs) / vs OpenZ, one right edge per column.
// The foot: Custom from OpenZ / ZodEngine / Z-Wiki and Reset. The box keeps its size (E099: the band under the foot stays empty).
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>The 88 u head of the inspector and of the flag / obstacle panel (E080-E087, E110).</summary>
    internal sealed partial class UeHead                                                              // 1.5 ue_exit_placement: + UI/OzUeInspector.ExitDriver.cs
    {
        public RectTransform Rt, Pic; public RawImage Art; public HudButton Back, Remove, PicHot;
        public OzText Name, Meta, LiveL, LiveR, Dim;
        RectTransform chip; OzText chipText;
        Image barTrack, barFill;
        float textX, textR, nameMid, metaMid, liveMid, barY;
        public string RemoveTip = "";
        /// <summary>The live line's left text at 13 u (the obstacle's HP stands there, left aligned: E110).</summary>
        public bool LeftBig;

        public static UeHead Make(Transform parent, UeBoxLayout L, RectTransform box, int prio)
        {
            var h = new UeHead();
            bool c = L.compact;
            float H = L.head;
            h.Rt = OzUeGfx.Node(parent, "Head", 0, 0, L.w, H);
            OzUeGfx.Swallow(h.Rt, "Swallow", 0, 0, L.w, H, prio);
            float bx = c ? 10f : 12f, ps = L.portrait;
            h.Back = OzUeGfx.Button(h.Rt, "Back", "<", bx, (H - L.ctl) * 0.5f, L.ctl, L.ctl, HudButtonStyle.Normal, prio, 14f, 0f);
            h.Back.OnClick = cl => OzUePick.ClearInspect();
            OzUeSideTip.Add(h.Back.Rt, "Back to all units (Esc)", OzUeSideTip.Place.Side, prio, box);
            float px = bx + L.ctl + (c ? 8f : 10f);
            h.PicHot = OzUeGfx.Hot(h.Rt, "PicHot", px, (H - ps) * 0.5f, ps, ps, prio);
            h.Pic = h.PicHot.Rt;
            var art = OzUeGfx.Node(h.Pic, "Art", 0, 0, ps, ps);
            h.Art = art.gameObject.AddComponent<RawImage>(); h.Art.raycastTarget = false;
            OzUeGfx.Frame(h.Pic, OzUi.GM400);
            h.textX = px + ps + 10f;
            h.textR = L.w - (c ? 10f : 12f);
            h.nameMid = c ? 12f : 21f; h.metaMid = c ? 26f : 40.4f; h.liveMid = c ? 39f : 62f; h.barY = c ? 48f : 72.5f;
            h.Name = OzUeGfx.Text(h.Rt, "Name", "", 14, OzUi.Ink, 0.10f, h.textX, h.nameMid, OzH.L, 180f, OzStyle.Underlay);
            h.chip = OzUeGfx.Node(h.Rt, "OpenZOnly", h.textX, h.nameMid - 7.5f, 80f, 15f);
            Hud.Kit(h.chip, "Bg", "Panels/chip_4", new OzRect(0, 0, 80, 15), OzUi.Amber).rectTransform.Fill();
            h.chipText = Hud.Label(h.chip, "Text", "OPENZ ONLY", 11, OzUeGfx.DarkInk, 0.08f, OzStyle.Plain).FillParent(OzH.C);
            h.chip.gameObject.SetActive(false);
            h.Meta = OzUeGfx.Text(h.Rt, "Meta", "", 12, OzUi.Muted, 0f, h.textX, h.metaMid, OzH.L, 200f);
            h.LiveL = OzUeGfx.Text(h.Rt, "LiveTeam", "", 12, OzUi.Ink2, 0f, h.textX, h.liveMid, OzH.L, 120f);
            h.LiveR = OzUeGfx.Text(h.Rt, "LiveHp", "", 13, OzUi.Ink2, 0f, h.textR, h.liveMid, OzH.R, 160f);
            h.Dim = OzUeGfx.Text(h.Rt, "LiveNone", "", 12, OzUi.Dim, 0f, h.textX, h.liveMid, OzH.L, 200f);
            h.barTrack = OzUeGfx.Rect(h.Rt, "BarTrack", new Color(1f, 1f, 1f, 0.10f), h.textX, h.barY, 100f, 4f);
            h.barFill = OzUeGfx.Rect(h.Rt, "BarFill", OzUi.Go, h.textX, h.barY, 100f, 4f);
            float rw = 76.7f;
            h.Remove = OzUeGfx.Button(h.Rt, "Remove", "REMOVE", L.w - (c ? 10f : 12f) - rw, (H - 28f) * 0.5f, rw, 28f, HudButtonStyle.Danger, prio);
            OzUeSideTip.Add(h.Remove.Rt, () => h.RemoveTip, OzUeSideTip.Place.Side, prio, box);
            h.Remove.Rt.gameObject.SetActive(false);
            return h;
        }

        float Right => Remove.Rt.gameObject.activeSelf ? Remove.Rt.anchoredPosition.x - 10f : textR;

        /// <summary>The name (14 u, E083) and its OPENZ ONLY chip 8 u after it. When the name and the chip do not both fit before REMOVE
        /// (a picked Electrobot, Alan &amp; Brad or shuttle in a narrow box), the chip stands over REMOVE, right aligned to it, so the name
        /// keeps its full size and is never cut; the compact head (no room over REMOVE) lets the name shrink as before.</summary>
        public void SetName(string name, bool openzOnly, string meta)
        {
            float r = Right;
            Name.Set(name);
            float full = Name.WidthAt(name), cw = chipText.WidthAt("OPENZ ONLY") + 10f;
            bool removeOn = Remove.Rt.gameObject.activeSelf;
            float removeTop = -Remove.Rt.anchoredPosition.y;
            float besideRoom = r + (removeOn ? 6f : 0f) - 8f - cw - textX;                      // the name's room with the chip after it (at least 4 u clear of REMOVE)
            bool above = openzOnly && full > besideRoom && removeOn && removeTop >= 15f + 6f;
            float chipX = Remove.Rt.anchoredPosition.x + Remove.Rt.sizeDelta.x - cw;              // over REMOVE, right aligned to it
            float room = !openzOnly ? r - textX : above ? Mathf.Min(r, chipX - 8f) - textX : besideRoom;
            Name.At(textX, nameMid, OzH.L, true, Mathf.Max(40f, room));
            chip.gameObject.SetActiveFast(openzOnly);
            if (openzOnly)
            {
                if (above) chip.TL(chipX, removeTop - 4f - 15f, cw, 15f);
                else chip.TL(textX + Mathf.Min(full, Mathf.Max(40f, room)) + 8f, nameMid - 7.5f, cw, 15f);
            }
            Meta.Set(meta);
            Meta.At(textX, metaMid, OzH.L, true, Mathf.Max(40f, MetaRight - textX));                   // 1.5 ue_exit_placement: = r unless EXIT DRIVER stands beside it
        }

        /// <summary>The live line: left word, right HP text, a bar (frac &lt; 0 = none); or one DIM line (none = null).</summary>
        public void SetLive(string left, Color lc, string right, Color rc, float frac, Color fill, string none)
        {
            float r = LiveRight;                                                                     // 1.5 ue_exit_placement: = Right unless EXIT DRIVER stands beside it
            bool live = none == null;
            LiveL.SetActive(live); LiveR.SetActive(live); Dim.SetActive(!live);
            bool bar = live && frac >= 0f;
            barTrack.enabled = bar; barFill.enabled = bar;
            if (!live) { Dim.Set(none); Dim.At(textX, liveMid, OzH.L, true, Mathf.Max(40f, r - textX)); return; }
            LiveL.FontSize = LeftBig ? 13f : 12f;
            LiveL.Set(LeftBig ? OzText.Tab(left) : left); LiveL.color = lc; LiveL.At(textX, liveMid, OzH.L, true, LeftBig ? Mathf.Max(40f, r - textX) : 120f);
            LiveR.Set(OzText.Tab(LiveHpWords(right, r))); LiveR.color = rc; LiveR.At(r, liveMid, OzH.R, true, LiveHpBox(r));   // 1.5 ue_exit_placement: as before unless EXIT DRIVER stands beside it
            if (bar)
            {
                float w = Mathf.Max(10f, r - textX);
                barTrack.rectTransform.TL(textX, barY, w, 4f);
                barFill.rectTransform.TL(textX, barY, Mathf.Max(0f, w * Mathf.Clamp01(frac)), 4f);
                barFill.color = fill;
            }
        }

        public void ShowRemove(bool on, string tip) { RemoveTip = tip ?? ""; Remove.Rt.gameObject.SetActiveFast(on); }
    }

    internal sealed partial class OzUeInspector                                                       // 1.5 ue_exit_placement: + UI/OzUeInspector.ExitDriver.cs
    {
        sealed class GridRow
        {
            public UeStat st; public RectTransform rt; public OzText name, vs; public readonly OzText[] vals = new OzText[3];
            public Image[] wash = new Image[4]; public OzUeNumberBox box;
        }

        UeBoxShell shell; UeBoxLayout L;
        RectTransform root, words, grid, gridHead, foot;
        UeHead head;
        OzText role, plus, minus, none; Image plusH, plusV, minusBar, wordsLine;
        HudButton wordsSwallow;
        readonly RectTransform[] setCells = new RectTransform[4];
        readonly Image[] setFill = new Image[4], setLine = new Image[4];
        readonly OzText[] setText = new OzText[4];
        readonly HudButton[] copyBtn = new HudButton[3];
        HudButton resetBtn;
        OzText note; float noteH; string noteFor;                                                           // the line by the Custom column: whom a change reaches, when the levels use it (UI/OzUePlan.cs Note*)
        /// <summary>Checks: the lines the last note took and its words.</summary>
        internal static int NoteLines; internal static string LastNote;
        UeScroll rows;
        readonly List<GridRow> gridRows = new List<GridRow>();
        string id; float top, wordsH;
        public bool Shown { get; private set; }

        public void Build(UeBoxShell shell)
        {
            this.shell = shell; L = shell.L;
            root = OzUeGfx.Node(shell.Body, "Inspector", 0, 0, L.w, L.h - L.bodyTop);
            head = UeHead.Make(root, L, shell.Box, UeBoxShell.PFix);
            head.PicHot.OnClick = cl => { };
            head.Remove.OnClick = cl => RemovePicked();
            head.MakeExit(L, shell.Box, UeBoxShell.PFix);                                                  // 1.5 ue_exit_placement: EXIT DRIVER under REMOVE
            OzUeSideTip.Add(head.Pic, "Drag onto the field", OzUeSideTip.Place.Side, UeBoxShell.PFix, shell.Box);
            BuildWords();
            BuildGrid();
            BuildFoot();
            BuildNote();
            root.gameObject.SetActive(false);
        }

        // ---- E088 what the unit is (the game's own words)
        void BuildWords()
        {
            words = OzUeGfx.Node(root, "Words", 0, L.head, L.w, 60f);
            wordsSwallow = OzUeGfx.Swallow(words, "Swallow", 0, 0, L.w, 60f, UeBoxShell.PFix);
            float lh = L.wordsLine, gap = WordsGap, mark = 0.45f * L.words;                  // lines 1 u apart (compact 0); the marks .45 em down the line
            role = OzUeGfx.Text(words, "Role", "", L.words, OzUi.Ink, 0f, 12f, lh * 0.5f, OzH.L, L.w - 24f);
            plus = OzUeGfx.Text(words, "Plus", "", L.words, OzUi.Ink2, 0f, 28f, (lh + gap) + lh * 0.5f, OzH.L, L.w - 40f);
            minus = OzUeGfx.Text(words, "Minus", "", L.words, OzUi.Ink2, 0f, 28f, 2f * (lh + gap) + lh * 0.5f, OzH.L, L.w - 40f);
            none = OzUeGfx.Text(words, "None", "The game has no description for this unit.", L.words, OzUi.Muted, 0f, 12f, lh * 0.5f, OzH.L, L.w - 24f);
            plusV = OzUeGfx.Rect(words, "PlusV", OzUi.Go, 12f + 1f + 3.5f, (lh + gap) + mark, 2f, 9f);          // a 9 x 9 GO plus
            plusH = OzUeGfx.Rect(words, "PlusH", OzUi.Go, 12f + 1f, (lh + gap) + mark + 3.5f, 9f, 2f);
            minusBar = OzUeGfx.Rect(words, "Minus", OzUi.Alert, 12f + 1f, 2f * (lh + gap) + mark + 3.5f, 9f, 2f);   // a 9 x 2 ALERT minus
            wordsLine = OzUeGfx.HLine(words, "Line", OzUeGfx.FootLine, 0, 59f, L.w);
        }

        float WordsGap => L.compact ? 0f : 1f;

        // ---- E089-E097 the stats grid
        void BuildGrid()
        {
            float gp = UeBoxLayout.GridPad, gh = L.gridHead, rh = L.gridRow;
            grid = OzUeGfx.Node(root, "Grid", 0, 150f, L.w, gh + 14 * rh + 2f);
            gridHead = OzUeGfx.Node(grid, "Head", 0, 0, L.w, gh);
            OzUeGfx.Swallow(gridHead, "Swallow", 0, 0, L.w, gh, UeBoxShell.PFix);
            OzUeGfx.Rect(gridHead, "Fill", OzUeGfx.GridHead, gp, 0, L.GridW, gh);
            OzUeGfx.HLine(gridHead, "Line", OzUi.GM500, gp, gh - OzPx.Line(1f), L.GridW);
            OzUeGfx.Text(gridHead, "Stat", "Stat", 12, OzUi.Muted, 0f, gp + 2f, gh * 0.5f, OzH.L, 60f);
            for (int i = 0; i < 4; i++)
            {
                var set = OzUeSets.All[i];
                float l = gp + L.ColRight(i), r = gp + L.ColRight(i + 1);
                var hot = OzUeGfx.Hot(gridHead, "Set" + i, l, 0, r - l, gh - 1f, UeBoxShell.PFix);
                setCells[i] = hot.Rt;
                setFill[i] = OzUeGfx.Rect(hot.Rt, "Fill", OzUeSets.Colour(set), 0, 0, r - l, gh - 1f);
                setLine[i] = OzUeGfx.Rect(hot.Rt, "Line", OzUeSets.Colour(set), 0, gh - 3f, r - l, 2f);
                setText[i] = OzUeGfx.Text(hot.Rt, "Text", OzUeSets.Name(set), 11.5f, OzUeSets.Colour(set), 0f, r - l - 6f, (gh - 1f) * 0.5f, OzH.R, r - l - 4f);
                hot.OnClick = cl => OzUeSets.Use(set);
                OzUeSideTip.Add(hot.Rt, () => OzUeSets.LongName(set) + (OzUeSets.InUse == set ? "|The playground uses it" : "|Click: the playground uses " + OzUeSets.Name(set)), OzUeSideTip.Place.Side, UeBoxShell.PFix, shell.Box);
            }
            if (L.VsW > 0)
            {
                var vs = OzUeGfx.Text(gridHead, "Vs", "vs OpenZ", 11.5f, OzUi.Muted, 0f, gp + L.ColRight(5) - 2f, gh * 0.5f, OzH.R, L.VsW);
                var vz = OzUeGfx.Node(gridHead, "VsTip", gp + L.ColRight(4), 0, L.VsW, gh);
                OzUeSideTip.Add(vz, "Custom compared with OpenZ, in %", OzUeSideTip.Place.Side, UeBoxShell.PFix, shell.Box);
            }
            rows = UeScroll.Make(grid, "Rows", 0, gh, L.w, 14 * rh + 2f, OzUeGfx.PanelFill, UeBoxShell.PFix);
            for (int i = 0; i < 14; i++) gridRows.Add(MakeGridRow((UeStat)i, i * rh));
            rows.SetContentHeight(14 * rh + 2f);
        }

        GridRow MakeGridRow(UeStat st, float y)
        {
            float gp = UeBoxLayout.GridPad, rh = L.gridRow;
            var g = new GridRow { st = st };
            g.rt = OzUeGfx.Node(rows.Content, "Row_" + OzUeLiveStats.Def(st).key, 0, y, L.w, rh);
            for (int i = 0; i < 4; i++)
            {
                float l = gp + L.ColRight(i), r = gp + L.ColRight(i + 1);
                g.wash[i] = OzUeGfx.Rect(g.rt, "Wash" + i, OzUeGfx.Wash, l, 0, r - l, rh - 1f); g.wash[i].enabled = false;
            }
            OzUeGfx.HLine(g.rt, "Line", OzUeGfx.GridLine, gp, rh - OzPx.Line(1f), L.GridW);
            var d = OzUeLiveStats.Def(st);
            string nm = d.full + (d.unit.Length > 0 ? " " + OzUeGfx.Col(d.unit, OzUi.Muted) : "");
            g.name = OzUeGfx.Text(g.rt, "Name", nm, 12, st == UeStat.Build ? (Color)OzUi.Ink : (Color)OzUi.Ink2, 0f, gp + 2f, rh * 0.5f, OzH.L, L.NameW - 4f);
            for (int i = 0; i < 3; i++)
                g.vals[i] = OzUeGfx.Text(g.rt, "V" + i, "", 13, OzUi.Ink2, 0f, gp + L.ColRight(i + 1) - 6f, rh * 0.5f, OzH.R, L.ColRight(i + 1) - L.ColRight(i) - 4f);
            float cl = gp + L.ColRight(3) + 4f, cw = L.ColRight(4) - L.ColRight(3) - 4f;
            g.box = OzUeNumberBox.Make(g.rt, "Custom", cl, (rh - L.input) * 0.5f, cw, L.input, UeBoxShell.PIn);
            if (L.VsW > 0) g.vs = OzUeGfx.Text(g.rt, "Vs", "", 12, OzUi.Muted, 0f, gp + L.ColRight(5) - 2f, rh * 0.5f, OzH.R, L.VsW);
            OzUeSideTip.Add(g.rt, p => Part(p.x), part => Tip(g, part), OzUeSideTip.Place.Side, UeBoxShell.PRow, shell.Box);
            return g;
        }

        int Part(float x)
        {
            float gp = UeBoxLayout.GridPad;
            if (x < gp + L.ColRight(0)) return 0;
            for (int i = 1; i <= 5; i++) if (x < gp + L.ColRight(i)) return i;
            return -1;
        }

        string Tip(GridRow g, int part)
        {
            if (id == null) return null;
            if (part == 0) return OzUeSets.StatLong(g.st);
            if (part >= 1 && part <= 3) return OzUeSets.ValueTip(id, g.st, OzUeSets.Sources[part - 1]);
            if (part == 4)
            {
                if (g.st == UeStat.Dps) return "DPS is worked out from damage, reload and hit chance";
                return OzUeSets.Editable(id, g.st) ? OzUeSets.InputTip(id, g.st) : OzUeSets.ValueTip(id, g.st, UeSet.Custom);
            }
            if (part == 5 && g.vs != null && g.vs.text.Length > 0)
            {
                bool up = g.vs.text.StartsWith("+");
                bool good = OzUeLiveStats.LowerBetter(g.st) ? !up : up;
                return (up ? "Higher" : "Lower") + " than the compared value: " + (good ? "better" : "worse") + " for this unit";
            }
            return null;
        }

        // ---- E098 the foot
        void BuildFoot()
        {
            foot = OzUeGfx.Node(root, "Foot", 0, 500f, L.w, L.foot);
            OzUeGfx.Swallow(foot, "Swallow", 0, 0, L.w, L.foot, UeBoxShell.PFix);
            OzUeGfx.HLine(foot, "Line", OzUeGfx.FootLine, 0, 0, L.w);
            var lab = OzUeGfx.Text(foot, "From", "Custom from", 12, OzUi.Muted, 0f, 10f, L.foot * 0.5f, OzH.L, 90f);
            float x = 10f + lab.WidthAt("Custom from") + 6f, by = (L.foot - 28f) * 0.5f;
            var lz = OzUeGfx.Node(foot, "FromTip", 10f, 0, x - 16f, L.foot);
            OzUeSideTip.Add(lz, "Copy one set into the Custom values of this unit", OzUeSideTip.Place.Side, UeBoxShell.PFix, shell.Box);
            for (int i = 0; i < 3; i++)
            {
                var s = OzUeSets.Sources[i];
                var b = OzUeGfx.Button(foot, "From" + i, OzUeSets.Name(s), x, by, 60f, 28f, HudButtonStyle.Normal, UeBoxShell.PFix, 12f, 0.02f);
                float bw = b.Label.WidthAt(OzUeSets.Name(s)) + 16f;
                b.Rt.TL(x, by, bw, 28f);
                b.OnClick = cl => CopyFrom(s);
                OzUeSideTip.Add(b.Rt, "Custom = " + OzUeSets.Name(s) + " values for this unit", OzUeSideTip.Place.Side, UeBoxShell.PFix, shell.Box);
                copyBtn[i] = b;
                x += bw + 3f;
            }
            resetBtn = OzUeGfx.Button(foot, "Reset", "Reset", L.w - 10f - 52f, by, 52f, 28f, HudButtonStyle.Normal, UeBoxShell.PFix, 12f, 0.02f);
            string rl = resetBtn.Label.WidthAt("Reset unit") + 16f <= L.w - 10f - (x - 3f) - 8f ? "Reset unit" : "Reset";   // the plainer words where the foot has room after the copy buttons
            resetBtn.Label.Set(rl);
            float rw = Mathf.Max(51.4f, resetBtn.Label.WidthAt(rl) + 16f);
            resetBtn.Rt.TL(L.w - 10f - rw, by, rw, 28f);
            resetBtn.OnClick = cl => ResetUnit();
            OzUeSideTip.Add(resetBtn.Rt, () =>
            {
                if (id == null) return null;
                int n = OzUeSets.ChangedCount(id);
                var b = OzUeSets.CustomBase;
                return "Reset this unit|Its Custom values back to " + OzUeSets.Name(b) + (b == UeSet.OpenZ ? ", the game's own numbers" : ", the set the loaded file was based on")
                       + (n > 0 ? " (" + n + " changed)" : " (none changed)") + ", for every " + OzUeSets.UnitName(id) + " here, both teams"
                       + "|RESET STATS at the top right resets every unit to OpenZ's own numbers and removes your saved Custom stats";
            }, OzUeSideTip.Place.Side, UeBoxShell.PFix, shell.Box);
        }

        void BuildNote()
        {
            noteH = OzUePlan.NoteH(L.compact);
            note = Hud.Label(root, "CustomNote", "", OzUePlan.NoteTextSize(L.compact), OzUi.Ink2, 0f, OzStyle.Plain).Body(10f, 0f, L.w - 20f);
            note.rectTransform.sizeDelta = new Vector2(L.w - 20f, noteH - OzUePlan.NotePadTop - OzUePlan.NotePadBottom + 2f);
            OzUeSideTip.Add(note.rectTransform, OzUePlan.ScopeTip, OzUeSideTip.Place.Side, UeBoxShell.PFix, shell.Box);
        }

        /// <summary>The line's words for the unit shown: the longest wording that fits two lines.</summary>
        void SetNote()
        {
            string name = OzUePlayground.FullName(id);
            if (name == noteFor) return;
            noteFor = name;
            string t = OzUePlan.NoteFor(name, L.w - 20f, L.compact, (s, size, k) => note.WidthAt(s), out int lines);
            note.Set(t);
            NoteLines = lines; LastNote = t;
        }

        void CopyFrom(UeSet s)
        {
            if (id == null) return;
            OzUeNumberBox.Blur(false);
            OzUeSets.CopyToCustom(id, s);
            OzUeToast.Show(OzUeSets.UnitToast(id, "CUSTOM = " + OzUeSets.Name(s) + " VALUES"));               // every unit of the type, both teams (View/OzUeSets.Reset.cs)
        }

        void ResetUnit()
        {
            if (id == null) return;
            OzUeNumberBox.Blur(false);
            OzUeSets.ResetUnit(id);
            OzUeToast.Show(OzUeSets.UnitToast(id, "CUSTOM BACK TO " + OzUeSets.Name(OzUeSets.CustomBase)));
        }

        void RemovePicked()
        {
            var o = OzUePick.Inspected;
            if (o == null) return;
            if (OzUePlayground.Remove(o) > 0) OzSfx.Ui("ui_cancel");
        }

        // ---- show / layout
        /// <summary>The box's new height (UeBoxShell.Resize); the next Show lays the inspector out with it.</summary>
        public void Resize(UeBoxLayout l) => L = l;

        public void Show(bool on, float top)
        {
            Shown = on;
            root.gameObject.SetActiveFast(on);
            if (!on) return;
            this.top = top;
            Layout();
        }

        void Layout()
        {
            float bh = L.h - L.bodyTop - top;
            root.TL(0, top, L.w, bh);
            words.TL(0, L.head, L.w, wordsH);
            wordsSwallow.Rt.TL(0, 0, L.w, wordsH);
            wordsLine.rectTransform.TL(0, wordsH - OzPx.Line(1f), L.w, OzPx.Line(1f));
            float gy = L.head + wordsH;
            float full = 14 * L.gridRow + 2f;
            float avail = Mathf.Max(L.gridRow * 3f, bh - gy - L.gridHead - L.foot - noteH);                 // the line by the Custom column takes its band over the foot
            float vis = Mathf.Min(full, avail);
            grid.TL(0, gy, L.w, L.gridHead + vis);
            rows.Place(0, L.gridHead, L.w, vis);
            rows.SetContentHeight(full);
            note.rectTransform.anchoredPosition = new Vector2(10f, -(gy + L.gridHead + vis + OzUePlan.NotePadTop));
            foot.TL(0, gy + L.gridHead + vis + noteH, L.w, L.foot);
        }

        public void Refresh(bool values, bool counts, bool art)
        {
            string now = OzUePick.InspectedId;
            bool changedUnit = now != id;
            id = now;
            if (id == null) return;
            if (changedUnit)
            {
                OzUeNumberBox.Blur(true);
                rows.Reset();
                SetWords();
                SetNote();
                Layout();
                foreach (var g in gridRows) g.box.Bind(id, g.st);
            }
            OzUeArt.Apply(head.Art, id, PictureTeam);                                         // the picked unit's team can change with the same type
            if (counts || changedUnit || values)
                head.SetName(OzUePlayground.FullName(id), OzUePlayground.OpenZOnly(id), OzUePlayground.ClassWord(id) + ", " + OzUePlayground.OnField(id) + " on field");
            if (values || changedUnit) RefreshGrid();
            TickLive();
        }

        Team PictureTeam
        {
            get
            {
                var k = OzUePick.Kind;
                if (k == UeInspect.EmptyHardware) return Team.Neutral;
                return k == UeInspect.Unit || k == UeInspect.Type ? OzUePick.InspectedTeam : UeBoxShell.DockTeam;
            }
        }

        void SetWords()
        {
            var w = OzUnitFacts.WordsOf(id);
            bool has = w != null;
            role.SetActive(has); plus.SetActive(has); minus.SetActive(has); none.SetActive(!has);
            plusH.enabled = has; plusV.enabled = has; minusBar.enabled = has;
            if (has) { role.Set(w.role); plus.Set(w.plus); minus.Set(w.minus); }
            int n = has ? 3 : 1;
            wordsH = n * L.wordsLine + (n - 1) * WordsGap + L.wordsPad + OzPx.Line(1f);          // E088: padding 0 12 8 12 and the bottom line
        }

        void RefreshGrid()
        {
            var use = OzUeSets.InUse;
            for (int i = 0; i < 4; i++)
            {
                var set = OzUeSets.All[i];
                bool on = set == use;
                setFill[i].enabled = on; setLine[i].enabled = !on;
                setText[i].color = on ? OzUeGfx.DarkInk : OzUeSets.Colour(set);
            }
            foreach (var g in gridRows)
            {
                for (int i = 0; i < 3; i++)
                {
                    var set = OzUeSets.Sources[i];
                    var c = OzUeSets.Cell(id, g.st, set);
                    g.vals[i].Set(OzText.Tab(OzUeSets.Text(g.st, c)));
                    var col = OzUeStatsList.ValueColour(id, g.st, set, c, false);
                    g.vals[i].color = set == use && col == (Color)OzUi.Ink2 ? (Color)OzUi.Ink : col;
                }
                for (int i = 0; i < 4; i++) g.wash[i].enabled = OzUeSets.All[i] == use;
                g.box.Refresh();
                if (g.vs != null)
                {
                    var cu = OzUeSets.Cell(id, g.st, UeSet.Custom); var oz = OzUeSets.Cell(id, g.st, UeSet.OpenZ);
                    int p = cu.HasNumber && oz.HasNumber && !OzUeSets.SameShown(g.st, cu.value, oz.value) ? OzUeLiveStats.PctOf(cu.value, oz.value) : int.MinValue;
                    if (p == int.MinValue || p == 0) g.vs.Set("");
                    else
                    {
                        bool good = OzUeLiveStats.LowerBetter(g.st) ? p < 0 : p > 0;
                        g.vs.Set(OzText.Tab((p > 0 ? "+" : "") + p + "%"));
                        g.vs.color = good ? OzUeGfx.DeltaUp : OzUeGfx.DeltaDown;
                    }
                }
            }
        }

        /// <summary>The live line (2.5 times a second): the picked unit's team and HP, an empty vehicle's, or "Drag the picture in".</summary>
        public void TickLive()
        {
            if (id == null) return;
            var k = OzUePick.Kind; var o = OzUePick.Inspected;
            bool live = (k == UeInspect.Unit || k == UeInspect.EmptyHardware) && o != null;
            bool dead = live && (o.Destroyed || o.KillAt >= 0);
            bool remove = live && !dead;
            string tip = remove ? "Remove this " + (k == UeInspect.EmptyHardware ? "empty " : "") + OzUePlayground.FullName(id).ToLowerInvariant() + " from the field (Delete)" : null;
            if (remove != head.Remove.Rt.gameObject.activeSelf)
            {
                head.ShowRemove(remove, tip);
                head.SetName(OzUePlayground.FullName(id), OzUePlayground.OpenZOnly(id), OzUePlayground.ClassWord(id) + ", " + OzUePlayground.OnField(id) + " on field");
            }
            else head.RemoveTip = tip ?? "";
            if (!live) { head.SetLive(null, default, null, default, -1f, default, "Drag the picture in"); return; }
            if (dead) { head.SetLive(null, default, null, default, -1f, default, "Destroyed"); return; }
            var t = k == UeInspect.EmptyHardware ? Team.Neutral : o.Owner;
            float f = o.MaxHealth > 0 ? Mathf.Clamp01(o.Health / (float)o.MaxHealth) : 0f;
            string left = k == UeInspect.EmptyHardware ? "Neutral empty" : OzUeUi.TeamWordSentence(t);
            head.SetLive(left, OzUeUi.TeamText(t), Mathf.Max(0, o.Health) + " / " + o.MaxHealth + " HP", OzUeUi.TeamText(t), f, f < 0.3f ? (Color)OzUi.Alert : (Color)OzUi.Go, null);
        }

        public void Tick()
        {
            rows.Tick(!OzUeNumberBox.AnyFocused || !OzUeGfx.Over(OzUeNumberBox.Focused.Rt));
            foreach (var g in gridRows) { g.rt.gameObject.SetActiveFast(rows.Shows(g.rt)); g.box.TickHover(); }
            if (id != null) UeBoxShell.PressPicture(head.PicHot, null, id, PictureTeam);
        }
    }
}
