// 1.2 item 6, View/OzUnitEditor.cs: the BALANCE tab of the STATS box (DESIGN_SPEC E120-E129; owner 6h: "the idea of balance bars ... as
// optional tools", 6j: - same box, same place, same size). One stat for every
// unit: four bars (OpenZ / ZodEngine / Z-Wiki / Custom) on ONE axis shared by all units and sets, the value of the set in use (an input
// when Custom is in use) and the biggest gap of another set over 5 %. A stat list (the game has no drop-down: a HudButton opens a 14-row
// list), [ / ] step it, the legend switches the set in use, Game order / High first, Linear / Log.
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    internal sealed class OzUeBalance
    {
        sealed class Row
        {
            public string id; public HudButton hot, badge; public RectTransform rt, pic; public RawImage art; public Image wash, bar, sep;
            public OzText name, value; public readonly Image[] bars = new Image[4]; public OzUeNumberBox box;
            public Image chipEdge; public OzText chip; public string chipTip; public bool hov, press, sel;
        }

        public const string KeyStat = "oz_ue_bstat";
        public const float RowH = 30f, PicS = 24f, BarH = 4f, BarGap = 2f, ChipH = 16f;
        UeBoxShell shell; UeBoxLayout L;
        RectTransform root, colHead, listPop;
        HudButton prevBtn, nextBtn, selBtn, sortBtn, scaleBtn;
        OzText selText, meaning, sortText, scaleText;
        RawImage sortLine, scaleLine;
        readonly HudButton[] legend = new HudButton[4];
        readonly Image[] legendSw = new Image[4];
        readonly OzText[] legendText = new OzText[4];
        UeScroll scroll;
        readonly List<Row> rows = new List<Row>();
        readonly List<HudButton> listRows = new List<HudButton>();
        readonly List<Image> listWash = new List<Image>();
        UeStat stat = UeStat.Build;
        bool highFirst, log;
        float picX, nameX, nameW, barsX, barsW, valueR, valueW, gapR, gapW;
        public bool Shown { get; private set; }

        public void Build(UeBoxShell shell)
        {
            this.shell = shell; L = shell.L;
            int s = PlayerPrefs.GetInt(KeyStat, 0);
            stat = s >= 0 && s < OzUeLiveStats.StatCount ? (UeStat)s : UeStat.Build;
            float W = L.w, bh = L.h - L.bodyTop;
            root = OzUeGfx.Node(shell.Body, "Balance", 0, 0, W, bh);
            OzUeGfx.Swallow(root, "Swallow", 0, 0, W, 110f, UeBoxShell.PFix);                 // row 1, meaning, legend, column head
            // ---- E120 row 1: < | the stat | >
            prevBtn = OzUeGfx.Button(root, "Prev", "<", 10f, 0, 28f, 28f, HudButtonStyle.Normal, UeBoxShell.PFix, 14f, 0f);
            prevBtn.OnClick = c => Step(-1);
            OzUeSideTip.Add(prevBtn.Rt, "Previous stat ([)", OzUeSideTip.Place.Side, UeBoxShell.PFix, shell.Box);
            nextBtn = OzUeGfx.Button(root, "Next", ">", W - 10f - 28f, 0, 28f, 28f, HudButtonStyle.Normal, UeBoxShell.PFix, 14f, 0f);
            nextBtn.OnClick = c => Step(1);
            OzUeSideTip.Add(nextBtn.Rt, "Next stat (])", OzUeSideTip.Place.Side, UeBoxShell.PFix, shell.Box);
            float sx = 10f + 28f + 6f, sw = W - 10f - 28f - 6f - sx;
            selBtn = OzUeGfx.Hot(root, "Stat", sx, 0, sw, 28f, UeBoxShell.PFix);
            OzUeGfx.Rect(selBtn.Rt, "Fill", OzUi.GM900, 0, 0, sw, 28f);
            OzUeGfx.Frame(selBtn.Rt, OzUi.GM400);
            selText = OzUeGfx.Text(selBtn.Rt, "Text", "", 13, OzUi.Ink, 0f, 12f, 14f, OzH.L, sw - 36f);
            var chev = Hud.Node("Chevron", selBtn.Rt).Anchor(new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(sw - 14f, -14f), new Vector2(10f, 10f));
            chev.localEulerAngles = new Vector3(0, 0, 180f);                                   // the kit's chevron, pointing down: a list opens
            var ci = Hud.Icon(chev, "Icon", "chevron", 0, 0, 10f, OzUi.Ink2); ci.rectTransform.Fill();
            selBtn.OnClick = c => ToggleList();
            OzUeSideTip.Add(selBtn.Rt, "The stat shown for every unit", OzUeSideTip.Place.Side, UeBoxShell.PFix, shell.Box);
            // ---- E121 meaning line, E122 legend
            meaning = OzUeGfx.Text(root, "Meaning", "", 12, OzUi.Ink2, 0f, 10f, 34f + 8f, OzH.L, W - 20f);
            var mz = OzUeGfx.Node(root, "MeaningTip", 10f, 34f, W - 20f, 16f);
            OzUeSideTip.Add(mz, () => OzUeSets.StatLong(stat), OzUeSideTip.Place.Side, UeBoxShell.PFix, shell.Box);
            float lx = 10f;
            for (int i = 0; i < 4; i++)
            {
                var set = OzUeSets.All[i];
                var b = OzUeGfx.Hot(root, "Legend" + i, lx, 56f, 80f, 18f, UeBoxShell.PFix);
                legendSw[i] = OzUeGfx.Rect(b.Rt, "Swatch", OzUeSets.Colour(set), 0, 7f, 10f, 4f);
                legendText[i] = OzUeGfx.Text(b.Rt, "Name", OzUeSets.Name(set), 12, OzUi.Muted, 0f, 15f, 9f, OzH.L, 100f);
                float tw = legendText[i].WidthAt(OzUeSets.Name(set)) + 2f, iw = 10f + 5f + tw;
                b.Rt.TL(lx, 56f, iw, 18f);
                legendText[i].At(15f, 9f, OzH.L, true, tw + 2f);
                b.OnClick = c => OzUeSets.Use(set);
                OzUeSideTip.Add(b.Rt, () => OzUeSets.InUse == set ? OzUeSets.Name(set) + ": the set in use (full bar)" : OzUeSets.Name(set) + "|Click: use " + OzUeSets.Name(set), OzUeSideTip.Place.Side, UeBoxShell.PFix, shell.Box);
                legend[i] = b;
                lx += iw + 12f;
            }
            // ---- E123 column head: grid 26 | minmax(60, 92) | minmax(50, 1fr) | 64 | 46, gap 6, padding 0 10 (narrow: 26 | 64 | 40.. | 60 | 42, gap 4)
            float gap = L.narrowBal ? 4f : 6f;
            picX = 10f;
            valueW = L.narrowBal ? 60f : 64f; gapW = L.narrowBal ? 42f : 46f;
            gapR = W - 10f; valueR = gapR - gapW - gap;
            float mid = valueR - valueW - gap - (picX + 26f + gap);
            nameW = L.narrowBal ? Mathf.Clamp(mid - 40f - gap, 64f, 92f) : Mathf.Clamp(mid - 50f - gap, 60f, 92f);   // 1.5 ue_bar_r2: the narrow box's name column 64..92 u (bars at least 40 u): MAMMOTH fits
            nameX = picX + 26f + gap;
            barsX = nameX + nameW + gap; barsW = Mathf.Max(40f, valueR - valueW - gap - barsX);
            colHead = OzUeGfx.Node(root, "ColHead", 0, 80f, W, 30f);
            OzUeGfx.HLine(colHead, "Top", OzUeGfx.SepLine, 0, 0, W);
            OzUeGfx.HLine(colHead, "Bottom", OzUi.GM500, 0, 30f - OzPx.Line(1f), W);
            sortBtn = OzUeGfx.Hot(colHead, "Sort", nameX, 1f, nameW, 28f, UeBoxShell.PFix);
            sortText = OzUeGfx.Text(sortBtn.Rt, "Text", "Game order", 12, OzUi.Ink2, 0f, 0f, 14f, OzH.L, nameW);
            sortLine = OzUeGfx.Dashed(sortBtn.Rt, "Line", OzUi.GM400, 0, 22f, nameW);
            sortBtn.OnClick = c => { highFirst = !highFirst; Fill(); };
            OzUeSideTip.Add(sortBtn.Rt, "Sort: game order or highest first", OzUeSideTip.Place.Side, UeBoxShell.PFix, shell.Box);
            scaleBtn = OzUeGfx.Hot(colHead, "Scale", barsX, 1f, Mathf.Min(barsW, 60f), 28f, UeBoxShell.PFix);
            scaleText = OzUeGfx.Text(scaleBtn.Rt, "Text", "Linear", 12, OzUi.Ink2, 0f, 0f, 14f, OzH.L, 60f);
            scaleLine = OzUeGfx.Dashed(scaleBtn.Rt, "Line", OzUi.GM400, 0, 22f, 40f);
            scaleBtn.OnClick = c => { log = !log; Fill(); };
            OzUeSideTip.Add(scaleBtn.Rt, "Bars: linear or log scale (log keeps small values visible)", OzUeSideTip.Place.Side, UeBoxShell.PFix, shell.Box);
            OzUeGfx.Text(colHead, "Value", "Value", 12, OzUi.Muted, 0f, valueR, 15f, OzH.R, valueW);
            OzUeGfx.Text(colHead, "Gap", "Gap", 12, OzUi.Muted, 0f, gapR, 15f, OzH.R, gapW);
            var gz = OzUeGfx.Node(colHead, "GapTip", gapR - gapW, 0, gapW, 30f);
            OzUeSideTip.Add(gz, "Biggest gap of another set to the set in use, over 5 %", OzUeSideTip.Place.Side, UeBoxShell.PFix, shell.Box);
            // ---- E124 rows
            scroll = UeScroll.Make(root, "Rows", 0, 110f, W, bh - 110f, OzUeGfx.PanelFill, UeBoxShell.PFix);
            for (int i = 0; i < OzUeLiveStats.Units.Length; i++) rows.Add(MakeRow(OzUeLiveStats.Units[i]));
            scroll.SetContentHeight(rows.Count * RowH);
            root.gameObject.SetActive(false);
        }

        Row MakeRow(string id)
        {
            float W = L.w;
            var r = new Row { id = id };
            r.hot = OzUeGfx.Hot(scroll.Content, "Row_" + id, 0, 0, W, RowH, UeBoxShell.PRow);
            r.rt = r.hot.Rt;
            r.wash = OzUeGfx.Rect(r.rt, "Wash", OzUeGfx.Wash, 0, 0, W, RowH); r.wash.enabled = false;
            r.bar = OzUeGfx.Rect(r.rt, "Sel", OzUi.Amber, 0, 0, 3f, RowH); r.bar.enabled = false;
            r.sep = OzUeGfx.HLine(r.rt, "Group", OzUi.GM500, 0, 0, W); r.sep.enabled = false;
            OzUeGfx.HLine(r.rt, "Line", new Color32(67, 75, 69, 51), 0, RowH - OzPx.Line(1f), W);
            r.pic = OzUeGfx.Node(r.rt, "Pic", picX, (RowH - PicS) * 0.5f, PicS, PicS);
            var a = OzUeGfx.Node(r.pic, "Art", 0, 0, PicS, PicS);
            r.art = a.gameObject.AddComponent<RawImage>(); r.art.raycastTarget = false;
            OzUeGfx.Frame(r.pic, OzUi.GM500);
            r.badge = UeBadge.Make(r.rt, picX + PicS - 15f + 7f, (RowH - PicS) * 0.5f + PicS - 15f + 3f, UeBoxShell.PIn);
            r.badge.OnClick = c => OzUeStats.OpenCard(id, UeBoxShell.DockTeam);
            OzUeSideTip.Add(r.badge.Rt, "Unit card: what it is and all its stats", OzUeSideTip.Place.Side, UeBoxShell.PIn, shell.Box);
            r.name = OzUeGfx.Text(r.rt, "Name", OzUePlayground.ShortName(id), 12, OzUi.Ink, 0.05f, nameX + 4f, RowH * 0.5f, OzH.L, nameW - 4f, OzStyle.Underlay);
            float by = (RowH - (4 * BarH + 3 * BarGap)) * 0.5f;
            for (int i = 0; i < 4; i++) r.bars[i] = OzUeGfx.Rect(r.rt, "Bar" + i, OzUeSets.Colour(OzUeSets.All[i]), barsX, by + i * (BarH + BarGap), 10f, BarH);
            r.value = OzUeGfx.Text(r.rt, "Value", "", 13, OzUi.Ink, 0f, valueR, RowH * 0.5f, OzH.R, valueW);
            r.box = OzUeNumberBox.Make(r.rt, "Custom", valueR - valueW, (RowH - 24f) * 0.5f, valueW, 24f, UeBoxShell.PIn);
            r.box.Rt.gameObject.SetActive(false);
            r.chipEdge = OzUeGfx.Rect(r.rt, "ChipEdge", OzUi.Amber, gapR - gapW, (RowH - ChipH) * 0.5f, 3f, ChipH); r.chipEdge.enabled = false;   // the gap cell's 3 u left border (prototype .dl)
            r.chip = OzUeGfx.Text(r.rt, "Chip", "", 12, OzUeGfx.DeltaUp, 0f, gapR, RowH * 0.5f, OzH.R, gapW - 6f, OzStyle.Underlay);
            r.hot.OnClick = c => Inspect(id);
            r.hot.OnRightClick = c => Inspect(id);
            OzUeSideTip.Add(r.rt, p => Part(p.x), part => Tip(r, part), OzUeSideTip.Place.Side, UeBoxShell.PRow, shell.Box);
            return r;
        }

        static void Inspect(string id)
        {
            OzUePick.InspectType(id, UeBoxShell.DockTeam);
            OzUeStats.SetTab(OzUeStats.Tab.Stats);                                              // row click = that unit in the STATS tab
        }

        int Part(float x)
        {
            if (x < nameX) return 0;
            if (x < valueR - valueW - 3f) return 1;
            if (x < gapR - gapW) return 2;
            return 3;
        }

        string Tip(Row r, int part)
        {
            var def = OzUeLiveStats.Def(stat);
            if (part <= 1)
            {
                var sb = new System.Text.StringBuilder(160);
                sb.Append(OzUeSets.UnitName(r.id)).Append(" - ").Append(def.full).Append(" (").Append(def.unitLong).Append(')');
                foreach (var s in OzUeSets.All) sb.Append('|').Append(OzUeSets.Name(s)).Append(": ").Append(OzUeSets.Text(stat, OzUeSets.Cell(r.id, stat, s)));
                sb.Append("|Click: its stats in the Stats tab");
                return sb.ToString();
            }
            if (part == 2)
            {
                if (OzUeSets.InUse == UeSet.Custom && OzUeSets.Editable(r.id, stat)) return OzUeSets.InputTip(r.id, stat);
                return OzUeSets.ValueTip(r.id, stat, OzUeSets.InUse);
            }
            return r.chipTip;
        }

        /// <summary>The box's new height (UeBoxShell.Resize); the next Show lays the rows out with it.</summary>
        public void Resize(UeBoxLayout l) => L = l;

        public void Show(bool on)
        {
            Shown = on;
            root.gameObject.SetActiveFast(on);
            if (!on) { CloseList(); return; }
            float bh = L.h - L.bodyTop;
            root.TL(0, 0, L.w, bh);
            scroll.Place(0, 110f, L.w, bh - 110f);
            scroll.SetContentHeight(rows.Count * RowH);
        }

        public bool Step(int dir)
        {
            if (!Shown) return false;
            int n = OzUeLiveStats.StatCount;
            stat = (UeStat)(((int)stat + dir + n) % n);
            PlayerPrefs.SetInt(KeyStat, (int)stat);
            CloseList();
            Fill();
            return true;
        }

        void SetStat(UeStat s) { stat = s; PlayerPrefs.SetInt(KeyStat, (int)stat); CloseList(); Fill(); }

        public void Refresh(bool values, bool counts, bool art)
        {
            if (art) foreach (var r in rows) OzUeArt.Apply(r.art, r.id, UeBoxShell.DockTeam);
            if (values) Fill();
        }

        /// <summary>The stat's rows: order, bars on the shared axis, values, gap chips (only on a change: set, stat, sort, scale, Custom).</summary>
        void Fill()
        {
            var def = OzUeLiveStats.Def(stat);
            var use = OzUeSets.InUse;
            selText.Set(def.full + " (" + def.unitLong + ")");
            meaning.Set(OzUeGfx.Col(def.unitLong, OzUi.AmberHi) + "  " + def.meaning);
            sortText.Set(highFirst ? "High first" : "Game order");
            scaleText.Set(log ? "Log" : "Linear");
            OzUeGfx.SetDashWidth(sortLine, Mathf.Min(nameW, sortText.WidthAt(sortText.text)));
            OzUeGfx.SetDashWidth(scaleLine, scaleText.WidthAt(scaleText.text));
            for (int i = 0; i < 4; i++)
            {
                bool on = OzUeSets.All[i] == use;
                legendText[i].color = on ? (Color)OzUi.Ink : (Color)OzUi.Muted;
                var c = OzUeSets.Colour(OzUeSets.All[i]); c.a = on ? 1f : 0.45f; legendSw[i].color = c;
            }
            double max = 0;
            foreach (var id in OzUeLiveStats.Units)
                foreach (var s in OzUeSets.All) { var c = OzUeSets.Cell(id, stat, s); if (c.HasNumber && c.value > max) max = c.value; }
            if (max <= 0) max = 1;
            var order = new List<Row>(rows);
            if (highFirst)
                order.Sort((a, b) =>
                {
                    var ca = OzUeSets.Cell(a.id, stat, use); var cb = OzUeSets.Cell(b.id, stat, use);
                    double va = ca.HasNumber ? ca.value : -1, vb = cb.HasNumber ? cb.value : -1;
                    int k = vb.CompareTo(va);
                    return k != 0 ? k : Array.IndexOf(OzUeLiveStats.Units, a.id).CompareTo(Array.IndexOf(OzUeLiveStats.Units, b.id));
                });
            string lastCls = null;
            for (int i = 0; i < order.Count; i++)
            {
                var r = order[i];
                r.rt.TL(0, i * RowH, L.w, RowH);
                string cls = Cls(r.id);
                r.sep.enabled = !highFirst && lastCls != null && cls != lastCls && !(cls == "transport" && lastCls == "hero");
                lastCls = cls;
                for (int k = 0; k < 4; k++)
                {
                    var set = OzUeSets.All[k];
                    var c = OzUeSets.Cell(r.id, stat, set);
                    float f = c.HasNumber && c.value > 0 ? Width(c.value, max) : 0f;
                    r.bars[k].enabled = f > 0f;
                    if (f > 0f) r.bars[k].rectTransform.sizeDelta = new Vector2(barsW * f, BarH);
                    var col = OzUeSets.Colour(set); col.a = set == use ? 1f : 0.45f; r.bars[k].color = col;
                }
                var cu = OzUeSets.Cell(r.id, stat, use);
                bool input = use == UeSet.Custom && OzUeSets.Editable(r.id, stat);
                r.box.Rt.gameObject.SetActiveFast(input);
                r.value.SetActive(!input);
                if (input) r.box.Bind(r.id, stat);
                else { r.value.Set(OzText.Tab(OzUeSets.Text(stat, cu))); r.value.color = OzUeStatsList.ValueColour(r.id, stat, use, cu, true); }
                // E129: the biggest gap of another set to the set in use, over 5 %
                UeSet best = use; int bestP = 0;
                foreach (var s in OzUeSets.All)
                {
                    if (s == use || !cu.HasNumber) continue;
                    var c = OzUeSets.Cell(r.id, stat, s);
                    if (!c.HasNumber) continue;
                    int p = OzUeLiveStats.PctOf(c.value, cu.value);
                    if (p != int.MinValue && Mathf.Abs(p) > 5 && Mathf.Abs(p) > Mathf.Abs(bestP)) { bestP = p; best = s; }
                }
                bool chip = best != use;
                r.chipEdge.enabled = chip; r.chip.SetActive(chip);
                if (chip)
                {
                    bool good = OzUeLiveStats.LowerBetter(stat) ? bestP < 0 : bestP > 0;
                    r.chip.Set(OzText.Tab((bestP > 0 ? "+" : "") + bestP + "%"));
                    r.chip.color = good ? OzUeGfx.DeltaUp : OzUeGfx.DeltaDown;
                    r.chipEdge.color = OzUeSets.Colour(best);
                    r.chipTip = OzUeSets.Name(best) + " is " + Mathf.Abs(bestP) + " % " + (bestP > 0 ? "higher" : "lower") + " than " + OzUeSets.Name(use);
                }
                else r.chipTip = null;
            }
        }

        float Width(double v, double max)
        {
            if (log) return Mathf.Max(0.02f, (float)(Math.Log10(1 + v) / Math.Log10(1 + max)));
            return Mathf.Max(0.015f, (float)(v / max));
        }

        static string Cls(string id) =>
            id == OzUeLiveStats.Heroes ? "hero" : id == OzUeLiveStats.Shuttle ? "transport" : OzUeLiveStats.IsRobot(id) ? "robot" : OzUeLiveStats.IsGun(id) ? "gun" : "vehicle";

        // ---- the stat list (a 14-row popover under the select)
        void ToggleList() { if (listPop != null && listPop.gameObject.activeSelf) CloseList(); else OpenList(); }

        void OpenList()
        {
            if (listPop == null)
            {
                var layer = OzUeStatsRoot.I != null && OzUeStatsRoot.I.PopsRoot != null ? OzUeStatsRoot.I.PopsRoot : OzUnitEditor.Layer(UeLayer.Popovers);
                float w = selBtn.Rt.sizeDelta.x, rh = 24f, h = 14 * rh + 8f;
                listPop = OzUeGfx.Node(layer, "OzUeBalanceStats", 0, 0, w, h);
                OzUeSolid.Under(Hud.Plate(listPop, "sm"), "sm");                                              // opaque over the box's rows (UI/OzUeSolid.cs)
                if (OpenZHud.I != null) OpenZHud.I.AddBlocker(listPop);
                for (int i = 0; i < OzUeLiveStats.StatCount; i++)
                {
                    var st = (UeStat)i; var d = OzUeLiveStats.Def(st);
                    var b = OzUeGfx.Hot(listPop, "Item" + i, 4f, 4f + i * rh, w - 8f, rh, 40);
                    var wash = OzUeGfx.Rect(b.Rt, "Wash", OzUeGfx.PressWash, 0, 0, w - 8f, rh); wash.enabled = false;
                    OzUeGfx.Text(b.Rt, "Text", d.full + " (" + d.unitLong + ")", 13, OzUi.Ink, 0f, 8f, rh * 0.5f, OzH.L, w - 24f);
                    b.OnClick = c => SetStat(st);
                    listRows.Add(b); listWash.Add(wash);
                }
            }
            var r = OzUeGfx.CanvasRect(selBtn.Rt);
            var cs = OzUnitEditor.CanvasSize;
            float x = r.xMin, y = cs.y - r.yMin + 4f, hh = listPop.sizeDelta.y;
            if (y + hh > cs.y - 8f) y = Mathf.Max(8f, cs.y - 8f - hh);
            listPop.TL(x, y, listPop.sizeDelta.x, hh);
            listPop.gameObject.SetActive(true);
            listPop.SetAsLastSibling();
        }

        public bool CloseList()
        {
            if (listPop == null || !listPop.gameObject.activeSelf) return false;
            listPop.gameObject.SetActive(false);
            return true;
        }

        public void DestroyPopover() { if (listPop != null) UnityEngine.Object.Destroy(listPop.gameObject); listPop = null; listRows.Clear(); listWash.Clear(); }

        public void Tick()
        {
            scroll.Tick(!OzUeNumberBox.AnyFocused || !OzUeGfx.Over(OzUeNumberBox.Focused.Rt));
            string sel = OzUePick.InspectedId;
            foreach (var r in rows)
            {
                r.rt.gameObject.SetActiveFast(scroll.Shows(r.rt));
                bool h = r.hot.Hovered || r.badge.Hovered, p = r.hot.Pressed, s = r.id == sel;
                if (h != r.hov || p != r.press || s != r.sel)
                {
                    r.hov = h; r.press = p; r.sel = s;
                    r.wash.enabled = h || p || s;
                    r.wash.color = p ? OzUeGfx.PressWash : s ? OzUeGfx.SelWash : OzUeGfx.Wash;
                    r.bar.enabled = s;
                }
                if (r.box.Rt.gameObject.activeSelf) r.box.TickHover();
                UeBoxShell.PressPicture(r.hot, r.pic, r.id, UeBoxShell.DockTeam);
            }
            // the toggles: hover AMBER_HI with an amber underline
            sortText.color = sortBtn.Hovered ? (Color)OzUi.AmberHi : (Color)OzUi.Ink2; sortLine.color = sortBtn.Hovered ? (Color)OzUi.Amber : (Color)OzUi.GM400;
            scaleText.color = scaleBtn.Hovered ? (Color)OzUi.AmberHi : (Color)OzUi.Ink2; scaleLine.color = scaleBtn.Hovered ? (Color)OzUi.Amber : (Color)OzUi.GM400;
            if (listPop != null && listPop.gameObject.activeSelf)
            {
                for (int i = 0; i < listRows.Count; i++)
                {
                    bool on = listRows[i].Hovered || i == (int)stat;
                    if (listWash[i].enabled != on) listWash[i].enabled = on;
                }
                var m = Mouse.current;
                if (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame) && !OzUeGfx.Over(listPop) && !OzUeGfx.Over(selBtn.Rt)) CloseList();
            }
        }
    }
}
