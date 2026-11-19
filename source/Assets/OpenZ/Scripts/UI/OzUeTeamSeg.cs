using System;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// The dock's team switch
    /// RED / BLUE / NEUTRAL, the Unit editor's major buttons. 36 u high (UI/OzUeBottomLayout.cs TeamSegH; the count row under it 28 u), 13.5 u
    /// labels, every cell tinted by its OWN team as the player sees it (YOUR TEAM COLOUR: OzUeDock.ApplyLooks gives each cell its look):
    ///  OFF   a very dark, desaturated tint of the team colour (red a deep dark red, blue an almost grey dark blue, neutral a dark grey), the
    ///        label a muted light tint of the same colour;
    ///  HOVER (the mouse on an OFF cell) one step brighter, the label brighter;
    ///  ON    the full team colour (OzUi RedHi / Red, BlueHi / Blue; neutral the khaki it had), a bright label (white; dark ink on the khaki),
    ///        a 1 u outline (RedText / BlueText; dark olive on the khaki), a step brighter under the mouse;
    ///  a CHANGE (a click, or the 1 / 2 / 3 keys) flashes a 2 u outline on the new cell for 0.35 s (reduced motion: 0.12 s), so a team
    ///        picked from the keyboard is seen at once (the Unit editor's HUD has no focus walk: its buttons are polled under the mouse).
    /// The same calls as OzUeSeg (UI/OzUeWidgets.cs), which the count row, the tabs and DAY / NIGHT keep with their look.
    /// </summary>
    public sealed class OzUeTeamSeg
    {
        /// <summary>One team's tints (sRGB, as drawn): the OFF and HOVER fill (top, bottom) and label, the ON fill, label, outline and flash.</summary>
        public struct Tint
        {
            public Color32 offTop, offBot, offInk, hovTop, hovBot, hovInk, onTop, onBot, onInk, onEdge, flash;
            public Color OffMid => Color.Lerp(offTop, offBot, 0.5f);
            public Color HovMid => Color.Lerp(hovTop, hovBot, 0.5f);
            public Color OnMid => Color.Lerp(onTop, onBot, 0.5f);
        }

        public static readonly Tint Red = new Tint
        {
            offTop = new Color32(60, 28, 24, 255), offBot = new Color32(43, 20, 17, 255), offInk = new Color32(200, 154, 144, 255),
            hovTop = new Color32(84, 38, 31, 255), hovBot = new Color32(61, 28, 23, 255), hovInk = new Color32(240, 200, 190, 255),
            onTop = OzUi.RedHi, onBot = OzUi.Red, onInk = new Color32(255, 255, 255, 255), onEdge = OzUi.RedText, flash = new Color32(255, 255, 255, 255),
        };
        public static readonly Tint Blue = new Tint
        {
            offTop = new Color32(31, 38, 55, 255), offBot = new Color32(23, 28, 41, 255), offInk = new Color32(152, 164, 192, 255),
            hovTop = new Color32(43, 54, 80, 255), hovBot = new Color32(32, 40, 59, 255), hovInk = new Color32(200, 212, 242, 255),
            onTop = OzUi.BlueHi, onBot = OzUi.Blue, onInk = new Color32(255, 255, 255, 255), onEdge = OzUi.BlueText, flash = new Color32(255, 255, 255, 255),
        };
        public static readonly Tint Neutral = new Tint
        {
            offTop = new Color32(42, 42, 39, 255), offBot = new Color32(32, 32, 30, 255), offInk = new Color32(172, 168, 158, 255),
            hovTop = new Color32(58, 58, 54, 255), hovBot = new Color32(44, 44, 41, 255), hovInk = new Color32(222, 217, 204, 255),
            onTop = OzUeW.NeutralTop, onBot = OzUi.Khaki, onInk = OzUeW.DarkInk, onEdge = OzUi.OD500, flash = OzUi.AmberLo,
        };
        public static Tint For(OzUeSeg.Look l) => l == OzUeSeg.Look.Red ? Red : l == OzUeSeg.Look.Blue ? Blue : Neutral;
        public const float FlashSec = 0.35f, FlashW = 2f, HoverOn = 0.08f;

        public RectTransform Rt;
        public readonly HudButton[] Cells;
        readonly RectTransform[] inner; readonly RawImage[] fill; readonly OzText[] labels; readonly Image[][] edge, flash; readonly Image[] sep;
        readonly OzUeSeg.Look[] looks;
        int selected = -1, hovered = -1, flashCell = -1; float flashAt = -10f, flashShown = -1f;
        public Action<int> OnPick;
        public int Selected => selected;
        public int Hovered => hovered;
        /// <summary>Checks: the flash's alpha now (0 = none), and how many changes flashed.</summary>
        public float FlashAlpha => flashShown < 0f ? 0f : flashShown;
        public static int Flashes;

        public OzUeTeamSeg(Transform parent, string name, string[] words, float textSize, float tracking)
        {
            int n = words.Length;
            Rt = Hud.Node(name, parent);
            OzUeW.Fill(Rt, "Well", OzUi.GM950);
            Hud.Border(Rt, OzUi.GM400);
            Cells = new HudButton[n]; inner = new RectTransform[n]; fill = new RawImage[n]; labels = new OzText[n]; edge = new Image[n][]; flash = new Image[n][];
            sep = new Image[n]; looks = new OzUeSeg.Look[n];
            for (int k = 0; k < n; k++)
            {
                int idx = k;
                looks[k] = k == 0 ? OzUeSeg.Look.Red : k == 1 ? OzUeSeg.Look.Blue : OzUeSeg.Look.Neutral;   // OzUeDock.ApplyLooks sets them as shown
                var t = For(looks[k]);
                var b = OzUeW.Bare(Rt, name + "_" + k, 40f, OzUeBottomLayout.TeamSegH - 2f);
                inner[k] = Hud.Node("Inner", b.Rt); inner[k].Fill(0f, 0f, k < n - 1 ? 1f : 0f, 0f);            // the cell less its 1 u separator
                fill[k] = OzUeW.Gradient(inner[k], "Tint", t.offTop, t.offBot);                             // on the button itself: a cell does not sink when pressed
                edge[k] = Hud.Border(inner[k], t.onEdge, 1f); foreach (var e in edge[k]) e.enabled = false;
                flash[k] = Hud.Border(inner[k], t.flash, FlashW); foreach (var e in flash[k]) e.enabled = false;
                sep[k] = Hud.Img(b.Rt, "Sep", OzUi.GM950);
                var sr = sep[k].rectTransform; sr.anchorMin = new Vector2(1f, 0f); sr.anchorMax = Vector2.one; sr.pivot = new Vector2(1f, 0.5f); sr.sizeDelta = new Vector2(1f, 0f); sr.anchoredPosition = Vector2.zero;
                sep[k].enabled = k < n - 1;
                labels[k] = OzUeW.Centred(inner[k], "Label", words[k], textSize, t.offInk, tracking, 0f, 0f);
                b.OnClick = c => OnPick?.Invoke(idx);
                Cells[k] = b;
            }
        }

        /// <summary>Place the well and its cells (cell widths include their separator), as OzUeSeg.Layout.</summary>
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
        public void SetLook(int k, OzUeSeg.Look l)
        {
            if (l == OzUeSeg.Look.Amber) l = OzUeSeg.Look.Neutral;                                         // the team switch has no amber cell
            if (looks[k] == l) return;
            looks[k] = l;
            var t = For(l);
            foreach (var e in edge[k]) e.color = t.onEdge;
            Paint();
        }
        public OzUeSeg.Look LookOf(int k) => looks[k];

        public void Select(int k)
        {
            if (selected == k) return;
            bool first = selected < 0;
            selected = k;
            if (!first && k >= 0) { flashCell = k; flashAt = OzMotion.Now; Flashes++; }                     // a change, by click or by the 1 / 2 / 3 keys
            Paint();
        }

        /// <summary>Every frame: the hovered cell's look (the HUD sets HudButton.Hovered) and the change's flash.</summary>
        public void Tick()
        {
            int h = -1;
            for (int k = 0; k < Cells.Length; k++) if (Cells[k].Hovered) h = k;
            if (h != hovered) { hovered = h; Paint(); }
            float a = 0f;
            if (flashCell >= 0)
            {
                float d = OzMotion.Dur(FlashSec, true);
                float u = d <= 0f ? 1f : Mathf.Clamp01((OzMotion.Now - flashAt) / d);
                a = 1f - u * u;
                if (u >= 1f) a = 0f;
            }
            if (Mathf.Abs(a - flashShown) < 0.004f) return;
            flashShown = a;
            for (int k = 0; k < Cells.Length; k++)
            {
                bool on = k == flashCell && a > 0.004f;
                var c = (Color)For(looks[k]).flash; c.a = a;
                foreach (var e in flash[k]) { e.enabled = on; if (on) e.color = c; }
            }
            if (a <= 0f) flashCell = -1;
        }

        /// <summary>The colours a cell shows now (checks): its fill's mid colour (the gradient's middle) and its label.</summary>
        public Color FillMid(int k)
        {
            var t = For(looks[k]); bool on = k == selected, hov = k == hovered;
            Color top = on ? (Color)t.onTop : hov ? (Color)t.hovTop : (Color)t.offTop, bot = on ? (Color)t.onBot : hov ? (Color)t.hovBot : (Color)t.offBot;
            if (on && hov) { top = Color.Lerp(top, Color.white, HoverOn); bot = Color.Lerp(bot, Color.white, HoverOn); }
            return Color.Lerp(top, bot, 0.5f);
        }
        public Color LabelColour(int k) => labels[k].color;
        public bool OutlineOn(int k) => edge[k].Length > 0 && edge[k][0].enabled;
        public RectTransform Inner(int k) => inner[k];

        void Paint()
        {
            for (int k = 0; k < Cells.Length; k++)
            {
                var t = For(looks[k]);
                bool on = k == selected, hov = k == hovered;
                Color top = on ? (Color)t.onTop : hov ? (Color)t.hovTop : (Color)t.offTop, bot = on ? (Color)t.onBot : hov ? (Color)t.hovBot : (Color)t.offBot;
                if (on && hov) { top = Color.Lerp(top, Color.white, HoverOn); bot = Color.Lerp(bot, Color.white, HoverOn); }   // a step brighter under the mouse
                fill[k].texture = OzUeArt.Gradient(top, bot);
                foreach (var e in edge[k]) e.enabled = on;
                labels[k].color = on ? (Color)t.onInk : hov ? (Color)t.hovInk : (Color)t.offInk;
            }
        }

        public void SetActive(bool on) { if (Rt && Rt.gameObject.activeSelf != on) Rt.gameObject.SetActive(on); }
    }
}
