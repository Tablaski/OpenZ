// A column head (Build first and amber, owner 6e), the group
// captions ROBOTS / VEHICLES / GUNS / TRANSPORT, 20 rows (picture with its "i" corner, the build-menu short name, how many are on the
// field, Build / HP / Damage / Range / DPS of the set in use, a value that differs from OpenZ in that set's colour) and the foot line.
// A row click or right-click shows that unit's inspector; its picture drags onto the field.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    internal sealed class OzUeStatsList
    {
        sealed class Row
        {
            public string id; public float y;
            public HudButton hot, badge; public RectTransform pic; public RawImage art;
            public Image wash, bar; public OzText name, count; public readonly OzText[] vals = new OzText[5];
            public bool hov, press, sel; public int onField = -1;
        }

        static readonly UeStat[] Cols = { UeStat.Build, UeStat.Health, UeStat.Damage, UeStat.Range, UeStat.Dps };
        static readonly string[] ColHeads = { "Build", "HP", "Damage", "Range", "DPS" };
        static readonly string[][] Groups =
        {
            new[] { "ROBOTS", "grunt", "psycho", "sniper", "tough", "pyro", "laser", "electrobot", OzUeLiveStats.Heroes, ZEvilZod.Type },
            new[] { "VEHICLES", "jeep", "light", "medium", ZMTank.Type, "heavy", "apc", "missile_launcher", "crane" },   // DV145 the medium tank (Sim/ZMTank.cs)
            new[] { "GUNS", "gatling", "gun", "howitzer", "missile_cannon" },
            new[] { "TRANSPORT", OzUeLiveStats.Shuttle },
        };

        UeBoxShell shell; UeBoxLayout L;
        RectTransform root, head, foot; UeScroll scroll; OzText footText;
        readonly List<Row> rows = new List<Row>();
        readonly float[] colR = new float[5]; readonly float[] colW = new float[5];
        float nameX, nameR, footH;
        bool showDps;
        public bool Shown { get; private set; }
        public const float HeadH = 28f, GroupH = 20f, RowH = 30f, PicS = 26f;

        public void Build(UeBoxShell shell)
        {
            this.shell = shell; L = shell.L;
            showDps = !L.narrowList;
            float gap = showDps ? 6f : 5f, right = L.w - 12f;
            float[] w = showDps ? new[] { 46f, 50f, 56f, 48f, 42f } : new[] { 44f, 48f, 54f, 46f, 0f };
            int last = showDps ? 4 : 3;
            for (int i = last; i >= 0; i--) { colR[i] = right; colW[i] = w[i]; right -= w[i] + gap; }
            nameR = right;
            nameX = 10f + PicS + gap;
            var body = shell.Body;
            float bh = L.h - L.bodyTop;
            root = OzUeGfx.Node(body, "List", 0, 0, L.w, bh);
            // ---- E061 column head
            head = OzUeGfx.Node(root, "Head", 0, 0, L.w, HeadH);
            OzUeGfx.HLine(head, "Top", OzUeGfx.SepLine, 0, 0, L.w);
            OzUeGfx.HLine(head, "Bottom", OzUi.GM500, 0, HeadH - OzPx.Line(1f), L.w);
            OzUeGfx.Swallow(head, "Swallow", 0, 0, L.w, HeadH, UeBoxShell.PFix);
            OzUeGfx.Text(head, "Unit", "Unit", 12, OzUi.Muted, 0f, nameX, HeadH * 0.5f, OzH.L, 60f);
            for (int i = 0; i <= last; i++)
            {
                var st = Cols[i];
                var t = OzUeGfx.Text(head, "H" + i, ColHeads[i], 12, i == 0 ? (Color)OzUi.Amber : (Color)OzUi.Muted, 0f, colR[i], HeadH * 0.5f, OzH.R, colW[i] + 4f);
                var hz = OzUeGfx.Node(head, "Tip" + i, colR[i] - colW[i], 0, colW[i], HeadH);
                OzUeSideTip.Add(hz, OzUeSets.StatLong(st), OzUeSideTip.Place.Side, UeBoxShell.PFix, shell.Box);
            }
            // ---- E070 foot
            footText = Hud.Label(root, "FootText", "", 12, OzUi.Muted, 0f, OzStyle.Plain).Body(10f, 6f, L.w - 20f);
            footText.Set("Click a unit for all its stats. OpenZ values; a coloured value differs from OpenZ.");
            footH = Mathf.Max(40f, footText.preferredHeight + 12f);
            foot = OzUeGfx.Node(root, "Foot", 0, bh - footH, L.w, footH);
            OzUeGfx.Swallow(foot, "Swallow", 0, 0, L.w, footH, UeBoxShell.PFix);
            OzUeGfx.HLine(foot, "Top", OzUeGfx.FootLine, 0, 0, L.w);
            footText.rectTransform.SetParent(foot, false);
            footText.rectTransform.anchoredPosition = new Vector2(10f, -6f);
            // ---- E071 the scroll list between them
            scroll = UeScroll.Make(root, "Rows", 0, HeadH, L.w, bh - HeadH - footH, OzUeGfx.PanelFill, UeBoxShell.PFix);
            float y = 0;
            foreach (var g in Groups)
            {
                var cap = OzUeGfx.Node(scroll.Content, "Group" + g[0], 0, y, L.w, GroupH);
                OzUeGfx.Rect(cap, "Fill", OzUeGfx.GroupFill, 0, 0, L.w, GroupH);
                OzUeGfx.Text(cap, "Text", g[0], 11, OzUeGfx.GroupText, 0.16f, 10f, 11f, OzH.L, 200f);
                y += GroupH;
                for (int k = 1; k < g.Length; k++) { rows.Add(MakeRow(g[k], y, last)); y += RowH; }
            }
            scroll.SetContentHeight(y);
            root.gameObject.SetActive(false);
        }

        Row MakeRow(string id, float y, int last)
        {
            var r = new Row { id = id, y = y };
            var c = scroll.Content;
            r.hot = OzUeGfx.Hot(c, "Row_" + id, 0, y, L.w, RowH, UeBoxShell.PRow);
            var rt = r.hot.Rt;
            r.wash = OzUeGfx.Rect(rt, "Wash", OzUeGfx.Wash, 0, 0, L.w, RowH); r.wash.enabled = false;
            r.bar = OzUeGfx.Rect(rt, "Sel", OzUi.Amber, 0, 0, 3f, RowH); r.bar.enabled = false;
            OzUeGfx.HLine(rt, "Line", OzUeGfx.RowLine, 0, RowH - OzPx.Line(1f), L.w);
            r.pic = OzUeGfx.Node(rt, "Pic", 10f, (RowH - PicS) * 0.5f, PicS, PicS);
            var art = OzUeGfx.Node(r.pic, "Art", 0, 0, PicS, PicS);
            r.art = art.gameObject.AddComponent<RawImage>(); r.art.raycastTarget = false;
            OzUeGfx.Frame(r.pic, OzUi.GM500);
            r.badge = UeBadge.Make(rt, 10f + PicS - 15f + 7f, (RowH - PicS) * 0.5f + PicS - 15f + 3f, UeBoxShell.PIn);
            r.badge.OnClick = cl => OzUeStats.OpenCard(id, UeBoxShell.DockTeam);
            OzUeSideTip.Add(r.badge.Rt, "Unit card: what it is and all its stats", OzUeSideTip.Place.Side, UeBoxShell.PIn, shell.Box);
            r.name = OzUeGfx.Text(rt, "Name", OzUePlayground.ShortName(id), 12, OzUi.Ink, 0.06f, nameX + 4f, RowH * 0.5f, OzH.L, nameR - nameX - 4f, OzStyle.Underlay);
            r.count = OzUeGfx.Text(rt, "Count", "", 12, OzUi.Go, 0f, nameX + 40f, RowH * 0.5f, OzH.L, 30f);
            for (int i = 0; i <= last; i++)
                r.vals[i] = OzUeGfx.Text(rt, "V" + i, "", 13, OzUi.Ink2, 0f, colR[i], RowH * 0.5f, OzH.R, colW[i] + 6f);
            r.hot.OnClick = cl => Inspect(id);
            r.hot.OnRightClick = cl => Inspect(id);
            OzUeSideTip.Add(rt, p => Part(p.x), part => Tip(id, part), OzUeSideTip.Place.Side, UeBoxShell.PRow, shell.Box);
            return r;
        }

        static void Inspect(string id) => OzUePick.InspectType(id, UeBoxShell.DockTeam);

        int Part(float x)
        {
            for (int i = 0; i < 5; i++) if (colW[i] > 0 && x >= colR[i] - colW[i] - 3f && x <= colR[i] + 3f) return 1 + i;
            return 0;
        }

        string Tip(string id, int part)
        {
            if (part >= 1 && part <= 5) return OzUeSets.ValueTip(id, Cols[part - 1], OzUeSets.InUse);
            return OzUeSets.UnitName(id) + "|Click: what it is and all its stats here|The i corner: its unit card";
        }

        /// <summary>The box's new height (UeBoxShell.Resize); the next Show lays the list out with it.</summary>
        public void Resize(UeBoxLayout l) => L = l;

        public void Show(bool on, float top)
        {
            Shown = on;
            root.gameObject.SetActiveFast(on);
            if (!on) return;
            float bh = L.h - L.bodyTop - top;
            root.TL(0, top, L.w, bh);
            foot.TL(0, bh - footH, L.w, footH);
            scroll.Place(0, HeadH, L.w, bh - HeadH - footH);
            scroll.SetContentHeight(rows.Count * RowH + Groups.Length * GroupH);
        }

        public void Refresh(bool values, bool counts, bool art)
        {
            var set = OzUeSets.InUse;
            var team = UeBoxShell.DockTeam;
            if (values) footText.Set("Click a unit for all its stats. " + OzUeSets.Name(set) + " values; a coloured value differs from OpenZ.");
            foreach (var r in rows)
            {
                if (art) OzUeArt.Apply(r.art, r.id, team);
                if (counts)
                {
                    int n = OzUePlayground.OnField(r.id);
                    if (n != r.onField)
                    {
                        r.onField = n;
                        r.count.Set(n > 0 ? OzText.Tab(n.ToString()) : "");
                        float nw = r.name.WidthAt(r.name.text);
                        r.count.At(Mathf.Min(nameX + 4f + nw + 5f, nameR - 14f), RowH * 0.5f, OzH.L, true, 30f);
                    }
                }
                if (values)
                    for (int i = 0; i < 5; i++)
                    {
                        if (r.vals[i] == null) continue;
                        var st = Cols[i];
                        var c = OzUeSets.Cell(r.id, st, set);
                        r.vals[i].Set(OzText.Tab(OzUeSets.Text(st, c)));
                        r.vals[i].color = ValueColour(r.id, st, set, c, i == 0);
                    }
            }
        }

        /// <summary>E068: INK_2 (Build INK), n/a DIM, "--" MUTED, a value that differs from OpenZ in the set's DIFF colour.</summary>
        internal static Color ValueColour(string id, UeStat st, UeSet set, UeCell c, bool lead)
        {
            if (c.kind == UeCellKind.NotApplicable || c.kind == UeCellKind.NoRoll) return OzUi.Dim;
            if (c.kind == UeCellKind.NotListed || c.kind == UeCellKind.NotInSet) return OzUi.Muted;
            if (OzUeSets.Differs(id, st, set)) return OzUeSets.DiffColour(set);
            return lead ? (Color)OzUi.Ink : (Color)OzUi.Ink2;
        }

        public void Tick()
        {
            scroll.Tick(true);
            string sel = OzUePick.Kind == UeInspect.Type ? OzUePick.InspectedId : null;
            foreach (var r in rows)
            {
                r.hot.Rt.gameObject.SetActiveFast(scroll.Shows(r.hot.Rt));
                bool h = r.hot.Hovered || r.badge.Hovered, p = r.hot.Pressed, s = r.id == sel;
                if (h != r.hov || p != r.press || s != r.sel)
                {
                    r.hov = h; r.press = p; r.sel = s;
                    r.wash.enabled = h || p || s;
                    r.wash.color = p ? OzUeGfx.PressWash : s ? OzUeGfx.SelWash : OzUeGfx.Wash;
                    r.bar.enabled = s;
                }
                UeBoxShell.PressPicture(r.hot, r.pic, r.id, UeBoxShell.DockTeam);
            }
        }
    }

    /// <summary>The "i" corner of a unit picture (E065): opens the unit card.</summary>
    internal static class UeBadge
    {
        public static HudButton Make(Transform parent, float x, float y, int prio, float size = 15f)
        {
            var b = OzUeGfx.Hot(parent, "Card", x, y, size, size, prio);
            OzUeGfx.Rect(b.Rt, "Bg", OzUi.A(OzUi.GM950, 0.92f), 0, 0, size, size);
            OzUeGfx.Frame(b.Rt, OzUi.GM400);
            Hud.Label(b.Rt, "I", "i", 12, OzUi.Ink, 0f, OzStyle.Plain).FillParent(OzH.C);
            return b;
        }
    }
}
