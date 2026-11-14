// 1.2 item 6, View/OzUnitEditor.cs: the unit card (DESIGN_SPEC E161-E166; owner 6d
// Opened by every "i" badge, centred in the free field under the toasts, above the
// table (the table behind it steps back). Left: the picture (drags onto the field), the name, the class line, the game's own words
// (UI/OzUnitFacts.cs; no "BUILD MENU" caption, owner 6k). Right: the 14 stats of the four sets, build time first as the headline row.
// < > step through all 20 units in game order; X or Esc closes it; the camera and the panels do not move.
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    internal sealed class OzUeCard
    {
        public const int Prio = 20;
        public const float HeaderH = 36f, FootH = 31f, RowH = 24f, PicS = 96f;
        OpenZHud hud;
        RectTransform parent, card, left, right, foot, chip;
        HudButton prev, next, close, picHot;
        OzText count, name, meta, role, plus, minus, none, footText, chipText;
        Image plusH, plusV, minusBar;
        RawImage art;
        readonly OzText[] headText = new OzText[4];
        readonly Image[] headFill = new Image[4], colWash = new Image[4];
        readonly OzText[,] vals = new OzText[14, 4];
        float w, h, leftW, rightW, statW;
        readonly float[] colX = new float[5];
        string shownId; Team shownTeam; Vector2 builtCanvas;
        public static int Opens;

        public void Build(RectTransform parent, OpenZHud hud)
        {
            this.parent = parent; this.hud = hud;
            Make();
        }

        void Make()
        {
            var cs = OzUnitEditor.CanvasSize; builtCanvas = cs;
            w = Mathf.Clamp(0.38f * cs.x, 600f, 720f);
            leftW = Mathf.Max(210f, w * 287.2f / 720f);
            rightW = w - leftW;
            float tableW = rightW - 24f;
            statW = tableW * 110.2f / 406.8f;
            float[] sw = { 64.6f, 94.3f, 64.7f, 73f };
            float rest = tableW - statW, k = rest / (64.6f + 94.3f + 64.7f + 73f);
            colX[0] = statW;
            for (int i = 0; i < 4; i++) colX[i + 1] = colX[i] + sw[i] * k;
            float rightH = 8f + RowH + 14 * RowH + 10f;
            h = HeaderH + rightH + FootH;
            card = OzUeGfx.Node(parent, "OzUeCard", 0, 0, w, h);
            OzUeSolid.Under(Hud.Plate(card, "md", HeaderH), "md");                                        // opaque: nothing behind the card shows through (UI/OzUeSolid.cs)
            hud.AddBlocker(card);
            OzUeGfx.Hot(card, "Swallow", 0, 0, w, h, Prio);
            OzUeSideTip.Add(card, (string)null, OzUeSideTip.Place.Below, Prio);
            // ---- E162 header
            var t = OzUeGfx.Text(card, "Title", "UNIT CARD", 12, OzUi.Ink, 0.16f, 12f, HeaderH * 0.5f, OzH.L, 120f, OzStyle.Underlay);
            count = OzUeGfx.Text(card, "Count", "", 12, OzUi.Muted, 0f, 12f + t.WidthAt("UNIT CARD") + 10f, HeaderH * 0.5f, OzH.L, 80f);
            close = OzUeGfx.Button(card, "Close", "X", w - 12f - 28f, 4f, 28f, 28f, HudButtonStyle.Normal, Prio + 1, 14f, 0f);
            next = OzUeGfx.Button(card, "Next", ">", w - 12f - 28f - 4f - 28f, 4f, 28f, 28f, HudButtonStyle.Normal, Prio + 1, 14f, 0f);
            prev = OzUeGfx.Button(card, "Prev", "<", w - 12f - 3f * 28f - 8f, 4f, 28f, 28f, HudButtonStyle.Normal, Prio + 1, 14f, 0f);
            close.OnClick = c => OzUeStats.CloseCard();
            next.OnClick = c => Step(1);
            prev.OnClick = c => Step(-1);
            OzUeSideTip.Add(prev.Rt, "Previous unit", OzUeSideTip.Place.Below, Prio + 1);
            OzUeSideTip.Add(next.Rt, "Next unit", OzUeSideTip.Place.Below, Prio + 1);
            OzUeSideTip.Add(close.Rt, "Close (Esc)", OzUeSideTip.Place.Below, Prio + 1);
            // ---- E163 / E164 left column
            left = OzUeGfx.Node(card, "Left", 0, HeaderH, leftW, rightH);
            OzUeGfx.VLine(card, "Sep", OzUeGfx.FootLine, leftW, HeaderH, rightH);
            picHot = OzUeGfx.Hot(left, "Picture", 14f, 12f, PicS, PicS, Prio + 1);
            var a = OzUeGfx.Node(picHot.Rt, "Art", 0, 0, PicS, PicS);
            art = a.gameObject.AddComponent<RawImage>(); art.raycastTarget = false;
            OzUeGfx.Frame(picHot.Rt, OzUi.GM400);
            OzUeSideTip.Add(picHot.Rt, "Drag onto the field", OzUeSideTip.Place.Below, Prio + 1);
            picHot.OnClick = c => { };                                                                     // a plain click answers with the HUD's UI click, as the inspector's picture (a drag places)
            name = OzUeGfx.Text(left, "Name", "", 16, OzUi.Ink, 0.10f, 14f, 118f + 10.6f, OzH.L, leftW - 28f, OzStyle.Underlay);
            chip = OzUeGfx.Node(left, "OpenZOnly", 14f, 121f, 80f, 15f);
            Hud.Kit(chip, "Bg", "Panels/chip_4", new OzRect(0, 0, 80, 15), OzUi.Amber).rectTransform.Fill();
            chipText = Hud.Label(chip, "Text", "OPENZ ONLY", 11, OzUeGfx.DarkInk, 0.08f, OzStyle.Plain).FillParent(OzH.C);
            meta = OzUeGfx.Text(left, "Meta", "", 13, OzUi.Muted, 0f, 14f, 141.3f + 8.75f, OzH.L, leftW - 28f);
            role = Hud.Label(left, "Role", "", 13, OzUi.Ink, 0f, OzStyle.Plain).Body(14f, 172.8f, leftW - 28f);
            plus = Hud.Label(left, "Plus", "", 12, OzUi.Ink2, 0f, OzStyle.Plain).Body(36f, 199f, leftW - 50f);
            minus = Hud.Label(left, "Minus", "", 12, OzUi.Ink2, 0f, OzStyle.Plain).Body(36f, 223.4f, leftW - 50f);
            none = Hud.Label(left, "None", "The game has no description for this unit.", 13, OzUi.Muted, 0f, OzStyle.Plain).Body(14f, 172.8f, leftW - 28f);
            plusH = OzUeGfx.Rect(left, "PlusH", OzUi.Go, 14f, 0, 12f, 2f);
            plusV = OzUeGfx.Rect(left, "PlusV", OzUi.Go, 19f, 0, 2f, 12f);
            minusBar = OzUeGfx.Rect(left, "Minus", OzUi.Alert, 14f, 0, 12f, 2f);
            // ---- E165 right column: Stat | OpenZ | ZodEngine | Z-Wiki | Custom
            right = OzUeGfx.Node(card, "Right", leftW + 12f, HeaderH + 8f, tableW, RowH * 15f);
            OzUeGfx.Rect(right, "HeadFill", OzUeGfx.CardHead, 0, 0, tableW, RowH);
            OzUeGfx.HLine(right, "HeadLine", OzUi.GM500, 0, RowH - OzPx.Line(1f), tableW);
            OzUeGfx.Text(right, "Stat", "STAT", 11, OzUi.Muted, 0.08f, 6f, RowH * 0.5f, OzH.L, statW - 8f);
            for (int i = 0; i < 4; i++)
            {
                var set = OzUeSets.All[i];
                float l = colX[i], r = colX[i + 1];
                colWash[i] = OzUeGfx.Rect(right, "Wash" + i, OzUi.A(OzUi.Amber, 0.07f), l, RowH, r - l, 14 * RowH); colWash[i].enabled = false;
                headFill[i] = OzUeGfx.Rect(right, "SetFill" + i, OzUeSets.Colour(set), l, 0, r - l, RowH - 1f); headFill[i].enabled = false;
                headText[i] = OzUeGfx.Text(right, "Set" + i, OzUeSets.Name(set), 12, OzUi.Muted, 0.02f, r - 6f, RowH * 0.5f, OzH.R, r - l - 8f);
            }
            for (int s = 0; s < 14; s++)
            {
                var st = (UeStat)s; var d = OzUeLiveStats.Def(st);
                float y = RowH * (s + 1);
                bool lead = st == UeStat.Build;
                if (lead)
                {
                    OzUeGfx.Rect(right, "LeadWash", OzUi.A(OzUi.Amber, 0.05f), statW, y, tableW - statW, RowH);
                    OzUeGfx.Rect(right, "LeadBar", OzUi.Amber, 0, y, 3f, RowH);
                }
                OzUeGfx.HLine(right, "Line" + s, new Color32(67, 75, 69, 77), 0, y + RowH - OzPx.Line(1f), tableW);
                string nm = Label(st) + (d.unit.Length > 0 ? " " + OzUeGfx.Col(d.unit, OzUi.Muted) : "");
                var n = OzUeGfx.Text(right, "Name" + s, nm, 13, lead ? (Color)OzUi.AmberHi : (Color)OzUi.Ink2, 0f, lead ? 9f : 6f, y + RowH * 0.5f, OzH.L, statW - 10f);
                var nz = OzUeGfx.Node(right, "NameTip" + s, 0, y, statW, RowH);
                OzUeSideTip.Add(nz, OzUeSets.StatLong(st), OzUeSideTip.Place.Below, Prio + 1);
                for (int i = 0; i < 4; i++)
                    vals[s, i] = OzUeGfx.Text(right, "V" + s + "_" + i, "", 13, OzUi.Ink2, 0f, colX[i + 1] - 6f, y + RowH * 0.5f, OzH.R, colX[i + 1] - colX[i] - 8f);
                int row = s;
                var vz = OzUeGfx.Node(right, "ValueTip" + s, statW, y, tableW - statW, RowH);
                OzUeSideTip.Add(vz, p => Col(p.x + statW), c => shownId == null ? null : OzUeSets.ValueTip(shownId, (UeStat)row, OzUeSets.All[c]), OzUeSideTip.Place.Below, Prio + 1);
            }
            // ---- E166 foot
            foot = OzUeGfx.Node(card, "Foot", 0, h - FootH, w, FootH);
            OzUeGfx.HLine(foot, "Line", OzUeGfx.FootLine, 0, 0, w);
            footText = OzUeGfx.Text(foot, "Text", "", 12, OzUi.Muted, 0f, 14f, FootH * 0.5f, OzH.L, w - 28f);
            card.gameObject.SetActive(false);
        }

        /// <summary>The card's stat names (prototype C 2d's card: "Build time", then the short names).</summary>
        internal static readonly string[] Labels = { "Build time", "Squad", "Health", "Damage", "vs Grunt", "Reload", "Hit", "Range", "Blast", "Shell", "Snipe", "Speed", "Run", "DPS" };
        internal static string Label(UeStat st) => Labels[(int)st];

        int Col(float x)
        {
            for (int i = 0; i < 4; i++) if (x >= colX[i] && x < colX[i + 1]) return i;
            return -1;
        }

        void Step(int dir)
        {
            var u = OzUeLiveStats.Units;
            int i = System.Array.IndexOf(u, OzUeStats.CardId);
            OzUeStats.OpenCard(u[((i < 0 ? 0 : i) + dir + u.Length) % u.Length], OzUeStats.CardTeam);
        }

        public void Sync()
        {
            if (card && Vector2.Distance(OzUnitEditor.CanvasSize, builtCanvas) > 0.5f)
            {
                UnityEngine.Object.Destroy(card.gameObject); OzUeSideTip.Prune(); Make(); OzUeSideTip.Prune(); shownId = null;
            }
            bool open = OzUeStats.CardOpen;
            card.gameObject.SetActiveFast(open);
            if (!open) { shownId = null; return; }
            if (OzUeStats.CardId != shownId || OzUeStats.CardTeam != shownTeam) { Opens++; Show(OzUeStats.CardId, OzUeStats.CardTeam); }
            Place();
            card.SetAsLastSibling();
        }

        /// <summary>Centred in the free field, top = the top bar + 112, at most down to 10 u over the play bar.</summary>
        void Place()
        {
            var cs = OzUnitEditor.CanvasSize;
            var free = OzUeInsets.FreeFieldCanvas;
            float cx = free.width > 10f ? free.center.x : (cs.x - OzUeInsets.Get(UeSide.Right)) * 0.5f;
            float top = (OzUeInsets.Get(UeSide.Top) > 1f ? OzUeInsets.Get(UeSide.Top) : 40f) + 112f;
            float x = Mathf.Clamp(cx - w * 0.5f, 12f, Mathf.Max(12f, cs.x - 12f - w));
            float maxH = cs.y - (OzUeInsets.Get(UeSide.Bottom) + 8f + 38f) - 10f - top;
            if (h > maxH && maxH > 200f) top = Mathf.Max(52f, top - (h - maxH));
            card.TL(x, top, w, h);
        }

        void Show(string id, Team team)
        {
            shownId = id; shownTeam = team;
            int i = System.Array.IndexOf(OzUeLiveStats.Units, id);
            count.Set((i + 1) + " of " + OzUeLiveStats.Units.Length);
            OzUeArt.Apply(art, id, team);
            bool oz = OzUePlayground.OpenZOnly(id);
            string nm = OzUePlayground.FullName(id);
            name.Set(nm);
            float nw = name.WidthAt(nm);
            name.At(14f, 118f + 10.6f, OzH.L, true, leftW - 28f - (oz ? 90f : 0f));
            chip.gameObject.SetActive(oz);
            if (oz) chip.TL(14f + Mathf.Min(nw, leftW - 118f) + 8f, 118f + 3f, chipText.WidthAt("OPENZ ONLY") + 10f, 15f);
            int squad = Mathf.RoundToInt((float)OzUeSets.InPlay(id, UeStat.Squad));
            string cls = OzUePlayground.ClassWord(id);
            bool many = (OzUeLiveStats.IsRobot(id) || id == OzUeLiveStats.Heroes) && squad > 1;
            meta.Set(cls + (many ? " of " + squad + ", stats per robot" : ""));
            var words = OzUnitFacts.WordsOf(id);
            bool has = words != null;
            role.SetActive(has); plus.SetActive(has); minus.SetActive(has); none.SetActive(!has);
            plusH.enabled = plusV.enabled = minusBar.enabled = has;
            if (has)
            {
                role.Set(words.role); plus.Set(words.plus); minus.Set(words.minus);
                float ry = 172.8f, py = ry + Mathf.Max(26.2f, role.preferredHeight + 8.6f), my = py + Mathf.Max(24.2f, plus.preferredHeight + 6f);
                role.rectTransform.anchoredPosition = new Vector2(14f, -ry);
                plus.rectTransform.anchoredPosition = new Vector2(36f, -py);
                minus.rectTransform.anchoredPosition = new Vector2(36f, -my);
                float mid = 8.4f;                                                                 // the first line's caps centre (12 u, line 1.4)
                plusH.rectTransform.TL(14f, py + mid - 1f, 12f, 2f);
                plusV.rectTransform.TL(19f, py + mid - 6f, 2f, 12f);
                minusBar.rectTransform.TL(14f, my + mid - 1f, 12f, 2f);
            }
            footText.Set((has ? "Words: the game's own unit card." : "The game has no words for this unit.") + " A coloured value differs from OpenZ. -- = not listed.");
            Refresh();
        }

        public void Refresh()
        {
            if (shownId == null || !card || !card.gameObject.activeSelf) return;
            var use = OzUeSets.InUse;
            for (int i = 0; i < 4; i++)
            {
                bool on = OzUeSets.All[i] == use;
                headFill[i].enabled = on; colWash[i].enabled = on;
                headText[i].color = on ? OzUeGfx.DarkInk : (Color)OzUi.Muted;
            }
            for (int s = 0; s < 14; s++)
            {
                var st = (UeStat)s;
                for (int i = 0; i < 4; i++)
                {
                    var set = OzUeSets.All[i];
                    var c = OzUeSets.Cell(shownId, st, set);
                    vals[s, i].Set(OzText.Tab(OzUeSets.Text(st, c)));
                    var col = OzUeStatsList.ValueColour(shownId, st, set, c, st == UeStat.Build);
                    vals[s, i].color = col;
                }
            }
        }

        public void Tick()
        {
            if (!card || !card.gameObject.activeSelf || shownId == null) return;
            UeBoxShell.PressPicture(picHot, null, shownId, shownTeam);
        }
    }
}
