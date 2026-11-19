// 1.2 item 6, View/OzUnitEditor.cs: the TABLE tool (DESIGN_SPEC E140-E148; owner 6h: "the table ... added to C as optional tools"): every
// unit and every stat of the set in use in one sheet over the lower field (nothing else moves; the field dims), build time first. The
// header: the set switch, DELTAS VS (a small +/- % chip under every value), "Only units on the field", the legend, CLOSE. A head click
// sorts high first, again low first, again game order. With Custom in use every value is an input. T or Esc closes it.
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    internal sealed class OzUeTable
    {
        sealed class Row
        {
            public string id; public RectTransform rt, pic; public HudButton hot, unitHot, badge; public RawImage art; public Image wash, chipBg, unitFill, line;
            public OzText name, chip; public readonly OzText[] vals = new OzText[14], deltas = new OzText[14];
            public OzUeNumberBox[] boxes; public bool hov, inFilter = true; public int onField = -1;
        }

        public const float Top = 140f, HeaderH = 52f, HeadH = 56f, GroupH = 18.5f, RowH = 32f, RowDeltaH = 40f, UnitMinW = 200f, SlideSec = 0.22f;
        /// <summary>The unit cell's name starts here: the picture (6 + 26), 8 u, the "i" badge (15), 8 u (E145).</summary>
        public const float NameX = 64f;
        public const int Prio = 10;
        /// <summary>The header and the head row stand over the rows scrolled under them.</summary>
        public const int PrioFix = Prio + 4;
        static readonly string[][] Groups =
        {
            new[] { "ROBOTS", "grunt", "psycho", "sniper", "tough", "pyro", "laser", "electrobot" },
            new[] { "VEHICLES", "jeep", "light", "medium", ZMTank.Type, "heavy", OzBigTanks.Mammoth, OzBigTanks.Bizon, "apc", "missile_launcher", "crane" },   // DV145 the medium tank (Sim/ZMTank.cs)
            new[] { "GUNS", "gatling", "gun", "howitzer", "missile_cannon" },
            new[] { "SPECIAL", OzUeLiveStats.Heroes, ZEvilZod.Type, OzUeLiveStats.Shuttle },   // DV161: his row of the big table (Sim/ZEvilZod.cs)
        };

        OpenZHud hud;
        RectTransform parent, sheet, header, table, headRow, empty, dim, shadow;
        UeSeg sets, deltas; HudButton close, only; Image onlyMark; OzText headSub, legend;
        readonly RectTransform[] headCells = new RectTransform[14];
        readonly OzText[] headText = new OzText[14], headUnit = new OzText[14];
        readonly Image[] sortLine = new Image[14];
        readonly Image[][] sortTri = new Image[14][];
        readonly List<Row> rows = new List<Row>();
        readonly List<RectTransform> groupRows = new List<RectTransform>();
        UeScroll scroll;
        float cw, ch, colW, tableX, slideAt = -10f, UnitW = UnitMinW;
        bool shownOpen, onlyField, boxesBuilt;
        int sortCol = -1, sortDir = -1;                // -1 high first, +1 low first
        int deltaSet = -1;                             // -1 none, else 0..2 (OpenZ, ZodEngine, Z-Wiki)
        float nextOnly;
        OzPost post;
        public Vector2 BuiltSize { get; private set; }
        public static int Opens, Sorts;

        public void Build(RectTransform parent, OpenZHud hud)
        {
            this.parent = parent; this.hud = hud;
            Make();
        }

        /// <summary>The 14 stat columns' width (E143: clamp(64, 0.04 CW, 80) u, 76.8 at 1920), never under the widest head or unit line
        /// at 12 u with its 6 u right pad and 2 u to spare (the game's font is wider than the prototype's: no head is cut or shrunk,
        /// owner 6k), narrower only when 15 columns would not fit the canvas. The checks use it too.</summary>
        internal static float ColumnWidth(float canvasW, OzText probe, float unitW)
        {
            float need = 0f;
            if (probe != null)
                foreach (var d in OzUeLiveStats.Defs)
                {
                    probe.FontSize = 12f; probe.Tracking = 0.06f; need = Mathf.Max(need, probe.WidthAt(d.head));
                    probe.Tracking = 0f; need = Mathf.Max(need, probe.WidthAt(d.unitLong));
                }
            float col = Mathf.Clamp(Mathf.Max(0.04f * canvasW, Mathf.Ceil(need + 6f + 2f)), 64f, 80f);
            if (unitW + 14 * col > canvasW - 32f) col = Mathf.Max(48f, (canvasW - 32f - unitW) / 14f);
            return col;
        }

        /// <summary>The unit column (E145: 200 u), never under the picture, the "i" badge, the longest unit name at 12 u caps 0.08 and a
        /// two-digit on-field chip after it (6 u gap, 5 u padding each side, 4 u to the edge): the game's font is wider than the
        /// The checks use it too.</summary>
        internal static float UnitWidth(OzText probe)
        {
            float need = 0f;
            if (probe != null)
            {
                probe.FontSize = 12f; probe.Tracking = 0.08f;
                foreach (var id in OzUeLiveStats.Units) need = Mathf.Max(need, probe.WidthAt(OzUePlayground.FullName(id)));
                probe.Tracking = 0f;
                need += 6f + probe.WidthAt(OzText.Tab("88")) + 10f + 4f;
            }
            return Mathf.Max(UnitMinW, Mathf.Ceil(NameX + need));
        }

        public void Rebuild()
        {
            if (sheet) UnityEngine.Object.Destroy(sheet.gameObject);
            if (shadow) UnityEngine.Object.Destroy(shadow.gameObject);
            rows.Clear(); groupRows.Clear(); boxesBuilt = false;
            OzUeSideTip.Prune();
            Make();
            OzUeSideTip.Prune();
            if (shownOpen) { slideAt = -10f; BuildBoxesIfCustom(); Layout(); Fill(); }        // open while the canvas changed: shown again at once, no slide
            Sync();
        }

        void Make()
        {
            var cs = OzUnitEditor.CanvasSize;
            cw = cs.x; ch = cs.y; BuiltSize = cs;
            var probe = Hud.Label(parent, "Probe", "", 12, OzUi.Ink, 0.06f, OzStyle.Plain);
            UnitW = UnitWidth(probe);
            colW = ColumnWidth(cw, probe, UnitW);
            UnityEngine.Object.Destroy(probe.gameObject);
            float tw = UnitW + 14 * colW;
            tableX = Mathf.Max(16f, (cw - tw) * 0.5f);
            float sh = ch - Top;
            var c0 = new Color(0, 0, 0, 0); var c1 = new Color(0, 0, 0, 0.45f);
            shadow = OzUeGfx.Node(parent, "OzUeTableShadow", 0, Top - 40f, cw, 40f);
            var sri = shadow.gameObject.AddComponent<RawImage>(); sri.raycastTarget = false; sri.texture = OzUeGfx.GradTex(c0, c1);
            sheet = OzUeGfx.Node(parent, "OzUeTable", 0, Top, cw, sh);
            OzUeGfx.Rect(sheet, "Fill", OzUeGfx.SheetFill, 0, 0, cw, sh);
            OzUeGfx.HLine(sheet, "Top", OzUi.AmberLo, 0, 0, cw);
            hud.AddBlocker(sheet);
            OzUeGfx.Hot(sheet, "Swallow", 0, 0, cw, sh, Prio);                              // nothing under the sheet reacts
            OzUeSideTip.Add(sheet, (string)null, OzUeSideTip.Place.Below, Prio);
            BuildHeader();
            BuildTable(sh);
            dim = OzUeGfx.Node(sheet, "Dim", 0, 0, cw, sh);
            OzUeGfx.Rect(dim, "Fill", OzUi.A(OzUi.GM950, 0.55f), 0, 0, cw, sh);
            dim.gameObject.SetActive(false);
            sheet.gameObject.SetActive(false); shadow.gameObject.SetActive(false);
        }

        void BuildHeader()
        {
            header = OzUeGfx.Node(sheet, "Header", 0, 0, cw, HeaderH);
            OzUeGfx.Swallow(header, "Swallow", 0, 0, cw, HeaderH, PrioFix);
            OzUeGfx.HLine(header, "Line", OzUi.GM600, 0, HeaderH - OzPx.Line(1f), cw);
            var title = OzUeGfx.Text(header, "Title", "ALL UNITS", 13, OzUi.Ink, 0.18f, 16f, HeaderH * 0.5f, OzH.L, 120f, OzStyle.Underlay);
            float x = 16f + title.WidthAt("ALL UNITS") + 13f, sy = (HeaderH - 30f) * 0.5f;
            var probe = Hud.Label(header, "Probe", "", 12, OzUi.Ink, 0.03f, OzStyle.Plain);
            float W(string s) => probe.WidthAt(s) + 20f;
            sets = UeSeg.Make(header, "Sets", x, sy, 30f, new[] { "OpenZ", "ZodEngine", "Z-Wiki", "Custom" },
                              new[] { W("OpenZ"), W("ZodEngine"), W("Z-Wiki"), W("Custom") }, 12f, 0.03f, PrioFix);
            for (int i = 0; i < 4; i++)
            {
                var s = OzUeSets.All[i];
                sets.Hots[i].OnClick = c => OzUeSets.Use(s);
                OzUeSideTip.Add(sets.Hots[i].Rt, OzUeSets.LongName(s) + "|The playground uses this set", OzUeSideTip.Place.Below, PrioFix);
            }
            x += sets.Rt.sizeDelta.x + 13f;
            var dl = OzUeGfx.Text(header, "DeltasLabel", "DELTAS VS", 11, OzUi.Muted, 0.14f, x, HeaderH * 0.5f, OzH.L, 90f);
            x += dl.WidthAt("DELTAS VS") + 12f;
            deltas = UeSeg.Make(header, "Deltas", x, sy, 30f, new[] { "None", "OpenZ", "ZodEngine", "Z-Wiki" },
                                new[] { W("None"), W("OpenZ"), W("ZodEngine"), W("Z-Wiki") }, 12f, 0.03f, PrioFix);
            for (int i = 0; i < 4; i++) { int k = i - 1; deltas.Hots[i].OnClick = c => { deltaSet = k; Layout(); Fill(); }; }
            UnityEngine.Object.Destroy(probe.gameObject);
            x += deltas.Rt.sizeDelta.x + 13f;
            only = OzUeGfx.Hot(header, "OnlyOnField", x, (HeaderH - 22f) * 0.5f, 170f, 22f, PrioFix);
            OzUeGfx.Rect(only.Rt, "Box", OzUi.GM900, 0, 4f, 14f, 14f);
            var bx = OzUeGfx.Node(only.Rt, "Edge", 0, 4f, 14f, 14f); OzUeGfx.Frame(bx, OzUi.GM400);
            onlyMark = OzUeGfx.Rect(only.Rt, "Mark", OzUi.Amber, 3f, 7f, 8f, 8f); onlyMark.enabled = false;
            var ol = OzUeGfx.Text(only.Rt, "Text", "Only units on the field", 13, OzUi.Ink2, 0f, 20f, 11f, OzH.L, 160f);
            float ow = 20f + ol.WidthAt("Only units on the field") + 2f;
            only.Rt.TL(x, (HeaderH - 22f) * 0.5f, ow, 22f);
            only.OnClick = c => { onlyField = !onlyField; onlyMark.enabled = onlyField; scroll.Reset(); Layout(); Fill(); };
            x += ow;
            close = OzUeGfx.Button(header, "Close", "CLOSE", cw - 16f - 72f, (HeaderH - 32f) * 0.5f, 72f, 32f, HudButtonStyle.Normal, PrioFix);
            close.OnClick = c => OzUeStats.SetTable(false);
            OzUeSideTip.Add(close.Rt, "Close (Esc or T)", OzUeSideTip.Place.Below, PrioFix);
            string lg = OzUeGfx.Col("--", OzUi.Ink2) + "  not listed   " + OzUeGfx.Col("n/a", OzUi.Ink2) + "  does not apply   " + OzUeGfx.Col("coloured", OzUi.Ink2) + "  differs from OpenZ";
            float lr = cw - 16f - 72f - 12f;
            legend = OzUeGfx.Text(header, "Legend", lg, 12, OzUi.Muted, 0f, lr, HeaderH * 0.5f, OzH.R, 400f);
            legend.SetActive(lr - legend.WidthAt(lg) > x + 16f);                                  // hidden when narrow
        }

        void BuildTable(float sh)
        {
            float tw = UnitW + 14 * colW;
            table = OzUeGfx.Node(sheet, "Table", tableX, HeaderH, tw, sh - HeaderH);
            headRow = OzUeGfx.Node(table, "Head", 0, 0, tw, HeadH);
            OzUeGfx.Swallow(headRow, "Swallow", 0, 0, tw, HeadH, PrioFix);
            OzUeGfx.Rect(headRow, "Fill", OzUeGfx.TableHead, 0, 0, tw, HeadH);
            OzUeGfx.HLine(headRow, "Line", OzUi.GM500, 0, HeadH - OzPx.Line(1f), tw);
            OzUeGfx.Text(headRow, "Unit", "UNIT", 12, OzUi.Ink2, 0.06f, 6f, 22f, OzH.L, UnitW - 12f);
            headSub = OzUeGfx.Text(headRow, "Sub", "", 12, OzUi.Muted, 0f, 6f, 41f, OzH.L, UnitW - 12f);
            for (int i = 0; i < 14; i++)
            {
                var st = (UeStat)i; var d = OzUeLiveStats.Def(st);
                float l = UnitW + i * colW;
                var hot = OzUeGfx.Hot(headRow, "H_" + d.key, l, 0, colW, HeadH, PrioFix);
                headCells[i] = hot.Rt;
                headText[i] = OzUeGfx.Text(hot.Rt, "Name", d.head, 12, OzUi.Ink2, 0.06f, colW - 6f, 22f, OzH.R, colW - 6f);
                headUnit[i] = OzUeGfx.Text(hot.Rt, "Unit", d.unitLong, 12, OzUi.Muted, 0f, colW - 6f, 41f, OzH.R, colW - 6f);
                sortLine[i] = OzUeGfx.Rect(hot.Rt, "Sorted", OzUi.Amber, 0, HeadH - 2f, colW, 2f); sortLine[i].enabled = false;
                sortTri[i] = new Image[3];
                for (int k = 0; k < 3; k++) { sortTri[i][k] = OzUeGfx.Rect(hot.Rt, "Tri" + k, OzUi.AmberHi, 4f + k, 8f + k, 6f - 2f * k, 1f); sortTri[i][k].enabled = false; }
                int col = i;
                hot.OnClick = c => SortBy(col);
                OzUeSideTip.Add(hot.Rt, OzUeSets.StatLong(st) + "|Click: sort high first, again: low first, again: game order", OzUeSideTip.Place.Below, PrioFix);
            }
            scroll = UeScroll.Make(table, "Rows", 0, HeadH, tw + UeScroll.BarW, sh - HeaderH - HeadH, OzUeGfx.SheetFill, PrioFix);   // the 10 u scroll bar right of the last column
            foreach (var g in Groups)
            {
                var gr = OzUeGfx.Node(scroll.Content, "Group" + g[0], 0, 0, tw, GroupH);
                OzUeGfx.Rect(gr, "Fill", OzUi.A(OzUi.GM700, 0.55f), 0, 0, tw, GroupH);
                OzUeGfx.HLine(gr, "Line", OzUeGfx.RowLine, 0, GroupH - OzPx.Line(1f), tw);
                OzUeGfx.Text(gr, "Text", g[0], 11, OzUi.A(OzUi.Ink, 0.62f), 0.18f, 8f, GroupH * 0.5f, OzH.L, 200f);
                groupRows.Add(gr);
                for (int k = 1; k < g.Length; k++) rows.Add(MakeRow(g[k], tw));
            }
            empty = OzUeGfx.Node(scroll.Content, "Empty", 0, 0, tw, 80f);
            OzUeGfx.Text(empty, "Text", "No units on the field yet.", 13, OzUi.Muted, 0f, tw * 0.5f, 40f, OzH.C, tw - 20f);
            empty.gameObject.SetActive(false);
        }

        Row MakeRow(string id, float tw)
        {
            var r = new Row { id = id };
            r.rt = OzUeGfx.Node(scroll.Content, "Row_" + id, 0, 0, tw, RowDeltaH);
            r.hot = OzUeGfx.Hot(r.rt, "Hot", 0, 0, tw, RowH, Prio + 1);
            r.wash = OzUeGfx.Rect(r.rt, "Wash", OzUi.A(OzUi.Amber, 0.06f), 0, 0, tw, RowH); r.wash.enabled = false;
            r.unitFill = OzUeGfx.Rect(r.rt, "UnitFill", OzUeGfx.UnitCell, 0, 0, UnitW, RowH);
            r.line = OzUeGfx.HLine(r.rt, "Line", new Color32(67, 75, 69, 82), 0, RowH - OzPx.Line(1f), tw);
            r.unitHot = OzUeGfx.Hot(r.rt, "Unit", 0, 0, UnitW, RowH, Prio + 2);
            r.unitHot.OnClick = c => OzUePick.InspectType(id, UeBoxShell.DockTeam);
            r.unitHot.OnRightClick = c => OzUePick.InspectType(id, UeBoxShell.DockTeam);
            r.pic = OzUeGfx.Node(r.rt, "Pic", 6f, 3f, 26f, 26f);
            var a = OzUeGfx.Node(r.pic, "Art", 0, 0, 26f, 26f);
            r.art = a.gameObject.AddComponent<RawImage>(); r.art.raycastTarget = false;
            OzUeGfx.Frame(r.pic, OzUi.GM500);
            OzUeSideTip.Add(r.pic, "Drag onto the field", OzUeSideTip.Place.Below, Prio + 3);
            r.badge = UeBadge.Make(r.rt, 41f, 8.5f, Prio + 3);
            r.badge.OnClick = c => OzUeStats.OpenCard(id, UeBoxShell.DockTeam);
            OzUeSideTip.Add(r.badge.Rt, "Unit card: what it is and all its stats", OzUeSideTip.Place.Below, Prio + 3);
            r.name = OzUeGfx.Text(r.rt, "Name", OzUePlayground.FullName(id), 12, OzUi.Ink, 0.08f, NameX, RowH * 0.5f, OzH.L, UnitW - NameX - 6f, OzStyle.Underlay);   // UnitWidth: the name and its chip always fit
            var chipRt = OzUeGfx.Node(r.rt, "OnField", 160f, 7f, 20f, 18f);
            r.chipBg = Hud.Kit(chipRt, "Bg", "Panels/chip_4", new OzRect(0, 0, 20, 18), OzUi.Go); r.chipBg.rectTransform.Fill();
            r.chip = Hud.Label(chipRt, "Text", "", 12, OzUeGfx.DarkInk, 0f, OzStyle.Plain).FillParent(OzH.C);
            OzUeSideTip.Add(chipRt, "On the field now", OzUeSideTip.Place.Below, Prio + 3);
            chipRt.gameObject.SetActive(false);
            for (int i = 0; i < 14; i++)
            {
                float right = UnitW + (i + 1) * colW - 6f;
                r.vals[i] = OzUeGfx.Text(r.rt, "V" + i, "", 13, OzUi.Ink2, 0f, right, RowH * 0.5f, OzH.R, colW - 8f);
                r.deltas[i] = OzUeGfx.Text(r.rt, "D" + i, "", 11, OzUeGfx.DeltaUp, 0f, right, 32f, OzH.R, colW - 8f);
                r.deltas[i].SetActive(false);
            }
            OzUeSideTip.Add(r.rt, p => Part(p), part => Tip(r, part), OzUeSideTip.Place.Below, Prio + 1);
            return r;
        }

        void BuildBoxes()
        {
            if (boxesBuilt) return;
            boxesBuilt = true;
            foreach (var r in rows)
            {
                r.boxes = new OzUeNumberBox[14];
                for (int i = 0; i < 13; i++)
                {
                    float l = UnitW + i * colW + 6f;
                    r.boxes[i] = OzUeNumberBox.Make(r.rt, "In" + i, l, (RowH - 24f) * 0.5f, colW - 12f, 24f, Prio + 3);
                    r.boxes[i].Bind(r.id, (UeStat)i);
                    r.boxes[i].Rt.gameObject.SetActive(false);
                }
            }
        }

        int Part(Vector2 p)
        {
            if (p.x < UnitW) return -1;
            int i = (int)((p.x - UnitW) / colW);
            return i >= 0 && i < 14 ? i : -1;
        }

        string Tip(Row r, int i)
        {
            var st = (UeStat)i;
            var use = OzUeSets.InUse;
            string t = use == UeSet.Custom && OzUeSets.Editable(r.id, st) ? OzUeSets.InputTip(r.id, st) : OzUeSets.ValueTip(r.id, st, use);
            if (r.deltas[i].gameObject.activeSelf && r.deltas[i].text.Length > 0)
            {
                bool up = r.deltas[i].text.Contains("+"), good = OzUeLiveStats.LowerBetter(st) ? !up : up;
                t += "|" + (up ? "Higher" : "Lower") + " than the compared value: " + (good ? "better" : "worse") + " for this unit";
            }
            return t;
        }

        void SortBy(int col)
        {
            if (sortCol == col) { if (sortDir == -1) sortDir = 1; else { sortCol = -1; sortDir = -1; } }
            else { sortCol = col; sortDir = -1; }
            Sorts++;
            Layout(); Fill();
        }

        public void Dispose()
        {
            if (post != null) { post.Destroy(); post = null; }
        }

        /// <summary>Open / closed (slides up from the bottom), the card's dim over it.</summary>
        public void Sync()
        {
            bool open = OzUeStats.TableOpen;
            if (open != shownOpen)
            {
                shownOpen = open;
                if (open) { Opens++; slideAt = OzMotion.Now; BuildBoxesIfCustom(); scroll.Reset(); Layout(); Fill(); }
                else { OzUeNumberBox.Blur(true); if (post != null) post.Weight = 0f; }                // the field is bright again
                if (post == null && open) post = new OzPost("OzUeTableDim", -30f, -1.25f, 0f, 55);
            }
            sheet.gameObject.SetActiveFast(open);
            shadow.gameObject.SetActiveFast(open);
            dim.gameObject.SetActiveFast(open && OzUeStats.CardOpen);
            if (open) dim.SetAsLastSibling();
        }

        void BuildBoxesIfCustom() { if (OzUeSets.InUse == UeSet.Custom) BuildBoxes(); }

        public void Refresh(bool values, bool counts, bool art)
        {
            if (!shownOpen) return;
            if (values) BuildBoxesIfCustom();
            if (counts && onlyField) Layout();
            Fill(values, counts, art);
        }

        void Fill() => Fill(true, true, true);

        /// <summary>Row order (groups in game order, or sorted by a column), the filter, and every cell of the set in use.</summary>
        void Layout()
        {
            var use = OzUeSets.InUse;
            float rh = deltaSet >= 0 && (UeSet)deltaSet != use ? RowDeltaH : RowH;
            var order = new List<Row>();
            foreach (var r in rows)
            {
                bool on = !onlyField || OzUePlayground.OnField(r.id) > 0;
                r.inFilter = on;
                r.rt.gameObject.SetActiveFast(on);
                if (on) order.Add(r);
            }
            float y = 0f;
            if (sortCol >= 0)
            {
                var st = (UeStat)sortCol;
                order.Sort((a, b) =>
                {
                    var ca = OzUeSets.Cell(a.id, st, use); var cb = OzUeSets.Cell(b.id, st, use);
                    if (!ca.HasNumber && !cb.HasNumber) return Array.IndexOf(OzUeLiveStats.Units, a.id).CompareTo(Array.IndexOf(OzUeLiveStats.Units, b.id));
                    if (!ca.HasNumber) return 1; if (!cb.HasNumber) return -1;
                    int k = sortDir < 0 ? cb.value.CompareTo(ca.value) : ca.value.CompareTo(cb.value);
                    return k != 0 ? k : Array.IndexOf(OzUeLiveStats.Units, a.id).CompareTo(Array.IndexOf(OzUeLiveStats.Units, b.id));
                });
                foreach (var g in groupRows) g.gameObject.SetActiveFast(false);
                foreach (var r in order) { Place(r, y, rh); y += rh; }
            }
            else
            {
                int gi = 0;
                foreach (var g in Groups)
                {
                    var gr = groupRows[gi++];
                    bool any = false;
                    foreach (var r in order) if (Array.IndexOf(g, r.id) > 0) { any = true; break; }
                    gr.gameObject.SetActiveFast(any);
                    if (!any) continue;
                    gr.TL(0, y, gr.sizeDelta.x, GroupH); y += GroupH;
                    for (int k = 1; k < g.Length; k++)
                        foreach (var r in order) if (r.id == g[k]) { Place(r, y, rh); y += rh; }
                }
            }
            empty.gameObject.SetActiveFast(order.Count == 0);
            if (order.Count == 0) { empty.TL(0, 0, empty.sizeDelta.x, 80f); y = 80f; }
            scroll.SetContentHeight(y);
            for (int i = 0; i < 14; i++)
            {
                bool sorted = i == sortCol;
                sortLine[i].enabled = sorted;
                headText[i].color = sorted ? (Color)OzUi.AmberHi : (Color)OzUi.Ink2;
                for (int k = 0; k < 3; k++)
                {
                    sortTri[i][k].enabled = sorted;
                    int row = sortDir < 0 ? k : 2 - k;                                        // down for high first, up for low first
                    sortTri[i][k].rectTransform.TL(4f + row, 8f + k, 6f - 2f * row, 1f);
                }
            }
        }

        void Place(Row r, float y, float rh)
        {
            float tw = r.rt.sizeDelta.x;
            r.rt.TL(0, y, tw, rh);
            r.hot.Rt.TL(0, 0, tw, rh); r.wash.rectTransform.TL(0, 0, tw, rh);
            r.unitFill.rectTransform.TL(0, 0, UnitW, rh);
            r.line.rectTransform.TL(0, rh - OzPx.Line(1f), tw, OzPx.Line(1f));
        }

        void Fill(bool values, bool counts, bool art)
        {
            var use = OzUeSets.InUse;
            sets.Paint((int)use, OzUeSets.Light(use), OzUeSets.Colour(use), OzUeGfx.DarkInk, i => OzUi.A(OzUeSets.Colour(OzUeSets.All[i]), 0.85f));
            deltas.Paint(deltaSet + 1, OzUi.AmberHi, OzUi.Amber, OzUeGfx.DarkInk, i => OzUi.Muted);
            int types = 0; foreach (var r in rows) if (OzUePlayground.OnField(r.id) > 0) types++;
            headSub.Set(onlyField ? types + " types on the field" : "Stats: " + OzUeSets.Name(use));
            bool inputs = use == UeSet.Custom;
            bool showDeltas = deltaSet >= 0 && (UeSet)deltaSet != use;
            var team = UeBoxShell.DockTeam;
            foreach (var r in rows)
            {
                if (art) OzUeArt.Apply(r.art, r.id, team);
                if (counts)
                {
                    int n = OzUePlayground.OnField(r.id);
                    if (n != r.onField)
                    {
                        r.onField = n;
                        var chipRt = r.chipBg.rectTransform.parent as RectTransform;
                        chipRt.gameObject.SetActive(n > 0);
                        if (n > 0)
                        {
                            r.chip.Set(OzText.Tab(n.ToString()));
                            float w = r.chip.WidthAt(r.chip.text) + 10f;
                            float x = Mathf.Min(NameX + r.name.WidthAt(r.name.text) + 6f, UnitW - w - 4f);
                            chipRt.TL(x, (RowH - 18f) * 0.5f, w, 18f);
                        }
                    }
                }
                if (!values) continue;
                for (int i = 0; i < 14; i++)
                {
                    var st = (UeStat)i;
                    bool box = inputs && i < 13 && OzUeSets.Editable(r.id, st) && r.boxes != null;
                    if (r.boxes != null && i < 13)
                    {
                        r.boxes[i].Rt.gameObject.SetActiveFast(box);
                        if (box) r.boxes[i].Refresh();
                    }
                    r.vals[i].SetActive(!box);
                    var c = OzUeSets.Cell(r.id, st, use);
                    if (!box)
                    {
                        r.vals[i].Set(OzText.Tab(OzUeSets.Text(st, c)));
                        r.vals[i].color = OzUeStatsList.ValueColour(r.id, st, use, c, i == 0);
                    }
                    r.deltas[i].SetActive(showDeltas);
                    if (showDeltas)
                    {
                        var o = OzUeSets.Cell(r.id, st, (UeSet)deltaSet);
                        int p = c.HasNumber && o.HasNumber && !OzUeSets.SameShown(st, c.value, o.value) ? OzUeLiveStats.PctOf(c.value, o.value) : int.MinValue;
                        if (p == int.MinValue || p == 0) r.deltas[i].Set("");
                        else
                        {
                            bool good = OzUeLiveStats.LowerBetter(st) ? p < 0 : p > 0;
                            r.deltas[i].Set(OzText.Tab((p > 0 ? "+" : "") + p + "%"));
                            r.deltas[i].color = good ? OzUeGfx.DeltaUp : OzUeGfx.DeltaDown;
                        }
                    }
                }
            }
        }

        public void Tick()
        {
            if (!shownOpen || !sheet) return;
            float t = OzMotion.T(slideAt, OzMotion.Dur(SlideSec));
            float off = (1f - OzEase.OutCubic(t)) * (ch - Top);
            sheet.anchoredPosition = new Vector2(0f, -(Top + off));
            shadow.anchoredPosition = new Vector2(0f, -(Top - 40f + off));
            if (post != null) post.Weight = t;
            sets.Tick(); deltas.Tick();
            bool typing = OzUeNumberBox.AnyFocused && OzUeGfx.Over(OzUeNumberBox.Focused.Rt);
            scroll.Tick(!typing && !OzUeStats.CardOpen);
            foreach (var r in rows)
            {
                bool vis = r.inFilter && scroll.Shows(r.rt);
                r.rt.gameObject.SetActiveFast(vis);
                if (!vis) continue;
                bool h = r.hot.Hovered || r.unitHot.Hovered || r.badge.Hovered;
                if (!h && r.boxes != null) foreach (var b in r.boxes) if (b != null && b.Hot.Hovered) { h = true; break; }
                if (h != r.hov) { r.hov = h; r.wash.enabled = h; }
                if (r.boxes != null && OzUeSets.InUse == UeSet.Custom) foreach (var b in r.boxes) if (b != null && b.Rt.gameObject.activeSelf) b.TickHover();
                UeBoxShell.PressPicture(r.unitHot, r.pic, r.id, UeBoxShell.DockTeam);
            }
            if (onlyField && OzMotion.Now >= nextOnly)
            {
                nextOnly = OzMotion.Now + 1.5f;                                               // E148: the table refreshes every 1.5 s while the filter is on
                if (!OzUeNumberBox.AnyFocused) { Layout(); Fill(false, true, false); }
            }
        }
    }
}
