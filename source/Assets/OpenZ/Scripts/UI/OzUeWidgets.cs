using System;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (UI/OzUeControlsRoot.cs; spec 3.4): the small controls of the editor's bottom band, built from the HUD kit: every
    /// clickable part is a HudButton (polled by the HUD, its tooltip "TITLE|body|KEY", its click sound), drawn the prototype's way.
    /// </summary>
    public static class OzUeW
    {
        public static readonly Color DarkInk = new Color32(0x17, 0x13, 0x0A, 255);       // text on the amber / khaki selected cells
        public static readonly Color NeutralTop = new Color32(0xEF, 0xE9, 0xCF, 255);

        /// <summary>A plain image child filling its parent (insets l, b, r, t).</summary>
        public static Image Fill(Transform parent, string name, Color c, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            var i = Hud.Img(parent, name, c); i.rectTransform.Fill(l, b, r, t); return i;
        }

        /// <summary>A vertical gradient (CSS linear-gradient top -> bottom) filling its parent.</summary>
        public static RawImage Gradient(Transform parent, string name, Color top, Color bottom, Color? mid = null, float midAt = 0.5f)
        {
            var rt = Hud.Node(name, parent); rt.Fill();
            var ri = rt.gameObject.AddComponent<RawImage>(); ri.raycastTarget = false;
            ri.texture = OzUeArt.Gradient(top, bottom, mid, midAt);
            return ri;
        }

        /// <summary>A RawImage child at a parent-local rect (top-left origin).</summary>
        public static RawImage Raw(Transform parent, string name, Texture tex, float x, float y, float w, float h, Color c)
        {
            var rt = Hud.Node(name, parent); rt.TL(x, y, w, h);
            var ri = rt.gameObject.AddComponent<RawImage>(); ri.raycastTarget = false; ri.texture = tex; ri.color = c;
            return ri;
        }

        /// <summary>A label centred in its parent.</summary>
        public static OzText Centred(Transform parent, string name, string text, float size, Color c, float tracking, float l = 0, float r = 0)
            => Hud.Label(parent, name, text, size, c, tracking, OzStyle.Plain).FillParent(OzH.C, l, 0, r, 0);

        /// <summary>A HudButton without the kit's art (the caller draws it): its rect, polling, tooltip and click sound only.</summary>
        public static HudButton Bare(Transform parent, string name, float w, float h)
        {
            var b = HudButton.Make(parent, name, null, new Vector2(w, h), HudButtonKind.Small, 12, 0f);
            if (b.Bg) b.Bg.enabled = false;
            if (b.Hatch) { UnityEngine.Object.Destroy(b.Hatch.gameObject); b.Hatch = null; }
            return b;
        }

        /// <summary>Spec E177 / the HUD tooltip: the prototype's lines "title|line|line" as the HUD's "TITLE|body|KEY" (the further lines
        /// one under the other in the body).</summary>
        public static string Tip(string title, string key = null, params string[] lines)
            => title + "|" + (lines != null && lines.Length > 0 ? string.Join("\n", lines) : "") + "|" + (key ?? "");
    }

    /// <summary>A segmented switch (spec 3.4 .seg): a GM900 well with a 1 u GM400 edge, flat cells with 1 u GM600 separators, the selected
    /// cell a vertical gradient (amber, or the team colours), hover = INK text on a 5 % white wash, dim cells DIM.</summary>
    public sealed class OzUeSeg
    {
        public enum Look { Amber, Red, Blue, Neutral }
        public RectTransform Rt;
        public readonly HudButton[] Cells;
        readonly OzText[] labels; readonly RawImage[] sel; readonly Image[] wash, sep;
        readonly Look[] looks; readonly bool[] dim;
        int selected = -1; int hovered = -1;
        public Action<int> OnPick;
        public int Selected => selected;

        public OzUeSeg(Transform parent, string name, string[] words, float textSize, float tracking)
        {
            int n = words.Length;
            Rt = Hud.Node(name, parent);
            OzUeW.Fill(Rt, "Well", OzUi.GM900);
            Hud.Border(Rt, OzUi.GM400);
            Cells = new HudButton[n]; labels = new OzText[n]; sel = new RawImage[n]; wash = new Image[n]; sep = new Image[n]; looks = new Look[n]; dim = new bool[n];
            for (int k = 0; k < n; k++)
            {
                int idx = k;
                var b = OzUeW.Bare(Rt, name + "_" + k, 40f, OzUeBottomLayout.SegCellH);
                sel[k] = OzUeW.Gradient(b.Rt, "Sel", OzUi.AmberHi, OzUi.Amber); sel[k].enabled = false;      // on the button itself: a cell does not sink when pressed
                wash[k] = OzUeW.Fill(b.Rt, "Hover", new Color(1f, 1f, 1f, 0.05f)); wash[k].enabled = false;
                sep[k] = Hud.Img(b.Rt, "Sep", OzUi.GM600);
                var sr = sep[k].rectTransform; sr.anchorMin = new Vector2(1f, 0f); sr.anchorMax = Vector2.one; sr.pivot = new Vector2(1f, 0.5f); sr.sizeDelta = new Vector2(1f, 0f); sr.anchoredPosition = Vector2.zero;
                sep[k].enabled = k < n - 1;
                labels[k] = OzUeW.Centred(b.Rt, "Label", words[k], textSize, OzUi.Muted, tracking, 0f, k < n - 1 ? 1f : 0f);
                b.OnClick = c => OnPick?.Invoke(idx);
                Cells[k] = b;
            }
        }

        /// <summary>Place the well and its cells (cell widths include their separator).</summary>
        public void Layout(UeBox well, float[] cells)
        {
            Rt.TL(well.x, well.y, well.w, well.h);
            float x = OzUeBottomLayout.Border;
            for (int k = 0; k < Cells.Length; k++)
            {
                Cells[k].Rt.TL(x, OzUeBottomLayout.Border, cells[k], well.h - 2f * OzUeBottomLayout.Border);
                x += cells[k];
            }
        }

        public void SetText(int k, string text) => labels[k].Set(text);
        public void SetTip(int k, string tip) => Cells[k].Tooltip = tip;
        public void SetLook(int k, Look l)
        {
            if (looks[k] == l && sel[k].texture) return;
            looks[k] = l;
            sel[k].texture = l == Look.Red ? OzUeArt.Gradient(OzUi.RedHi, OzUi.Red) : l == Look.Blue ? OzUeArt.Gradient(OzUi.BlueHi, OzUi.Blue)
                           : l == Look.Neutral ? OzUeArt.Gradient(OzUeW.NeutralTop, OzUi.Khaki) : OzUeArt.Gradient(OzUi.AmberHi, OzUi.Amber);
            Paint();
        }
        public void SetDim(int k, bool d) { if (dim[k] == d) return; dim[k] = d; Paint(); }
        public void Select(int k) { if (selected == k) return; selected = k; Paint(); }

        /// <summary>Every frame: the hovered cell's look (the HUD sets HudButton.Hovered).</summary>
        public void Tick()
        {
            int h = -1;
            for (int k = 0; k < Cells.Length; k++) if (Cells[k].Hovered) h = k;
            if (h != hovered) { hovered = h; Paint(); }
        }

        void Paint()
        {
            for (int k = 0; k < Cells.Length; k++)
            {
                bool on = k == selected, hov = k == hovered && !on;
                sel[k].enabled = on;
                wash[k].enabled = hov;
                Color c = on ? (looks[k] == Look.Red || looks[k] == Look.Blue ? Color.white : OzUeW.DarkInk)
                        : hov ? (Color)OzUi.Ink : dim[k] ? (Color)OzUi.Dim : (Color)OzUi.Muted;
                labels[k].color = c;
            }
        }

        public void SetActive(bool on) { if (Rt && Rt.gameObject.activeSelf != on) Rt.gameObject.SetActive(on); }
    }

    /// <summary>A key cap (spec 3.4 kbd: GM700 fill, 1 u GM400 edge, a 2 u bottom edge, 11 u INK), its colours settable (the green
    /// PLAY button's cap).</summary>
    public sealed class OzUeKeycap
    {
        public RectTransform Rt; readonly Image fill; readonly Image[] edge; readonly Image bottom; readonly OzText label;
        public OzUeKeycap(Transform parent, string name, string key, float tracking)
        {
            Rt = Hud.Node(name, parent);
            fill = OzUeW.Fill(Rt, "Fill", OzUi.GM700);
            edge = Hud.Border(Rt, OzUi.GM400);
            bottom = Hud.Img(Rt, "Bottom", OzUi.GM400);
            var br = bottom.rectTransform; br.anchorMin = Vector2.zero; br.anchorMax = new Vector2(1f, 0f); br.pivot = new Vector2(0.5f, 0f); br.sizeDelta = new Vector2(0f, 2f); br.anchoredPosition = Vector2.zero;
            label = OzUeW.Centred(Rt, "Key", key, OzUeBottomLayout.KeyText, OzUi.Ink, tracking, 0f, 0f);
            label.rectTransform.offsetMin = new Vector2(0f, 1f);                                      // the caps centred over the thicker bottom edge
        }
        public void Colours(Color fillC, Color edgeC, Color text)
        {
            fill.color = fillC; foreach (var e in edge) e.color = edgeC; bottom.color = edgeC; label.color = text;
        }
    }

    /// <summary>A flat icon button (spec E033-E036): 24 x 24, GM950 55 % fill, 1 u GM500 edge; hover AMBER_LO edge and an INK icon; the
    /// icon an "on" colour when lit (the turned compass).</summary>
    public sealed class OzUeIconButton
    {
        public HudButton B; public Image Icon; readonly Image fill; readonly Image[] edge;
        public bool Lit;
        bool shownHover, shownLit, first = true;
        public OzUeIconButton(Transform parent, string name, string glyph, float size, float iconSize)
        {
            B = OzUeW.Bare(parent, name, size, size);
            fill = OzUeW.Fill(B.Rt, "Fill", OzUi.A(OzUi.GM950, 0.55f));
            fill.transform.SetSiblingIndex(0);
            edge = Hud.Border(B.Rt, OzUi.GM500);
            float o = (size - iconSize) * 0.5f;
            Icon = Hud.Icon(B.Content, "Icon", glyph, o, o, iconSize, OzUi.Ink2);
        }
        public void Tick()
        {
            bool h = B.Hovered;
            if (!first && h == shownHover && Lit == shownLit) return;
            first = false; shownHover = h; shownLit = Lit;
            Color e = Lit || h ? (Color)OzUi.AmberLo : (Color)OzUi.GM500;
            foreach (var x in edge) x.color = e;
            Icon.color = Lit ? (Color)OzUi.AmberHi : h ? (Color)OzUi.Ink : (Color)OzUi.Ink2;
        }
    }

    /// <summary>A checkbox row of the background popover (spec E017-E019): a 16 u box (checked = AMBER with a dark tick, unchecked = GM900
    /// with a 1 u GM400 edge) and a 13 u INK_2 label 8 u after it; the whole row clicks.</summary>
    public sealed class OzUeCheck
    {
        public HudButton B; readonly Image box; readonly Image[] edge; readonly RawImage tick; readonly OzText label;
        bool on, hov, first = true;
        public OzUeCheck(Transform parent, string name, string text, float w)
        {
            B = OzUeW.Bare(parent, name, w, 22f);
            var br = Hud.Node("Box", B.Content); br.TL(0f, 3f, 16f, 16f);
            box = OzUeW.Fill(br, "Fill", OzUi.GM900);
            edge = Hud.Border(br, OzUi.GM400);
            tick = OzUeW.Raw(br, "Tick", OzUeArt.Tick(), 1f, 1f, 14f, 14f, OzUeW.DarkInk);
            label = Hud.Label(B.Content, "Label", text, 13f, OzUi.Ink2, 0f, OzStyle.Plain).At(24f, 11f, OzH.L, true, w - 24f);
        }
        public void Set(bool value)
        {
            if (!first && value == on && hov == B.Hovered) return;
            first = false; on = value; hov = B.Hovered;
            box.color = on ? (Color)OzUi.Amber : (Color)OzUi.GM900;
            foreach (var e in edge) e.color = on ? (Color)OzUi.AmberLo : hov ? (Color)OzUi.AmberLo : (Color)OzUi.GM400;
            tick.enabled = on;
            label.color = hov ? (Color)OzUi.Ink : (Color)OzUi.Ink2;
        }
    }
}
