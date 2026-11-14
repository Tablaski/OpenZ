// 1.2 item 6, View/OzUnitEditor.cs: small building blocks of the unit editor's STATS box, TABLE and unit card, made only of the game's
// own UI kit (Hud.Node / Img / Label / Plate / Kit, HudButton, OzText, OzUi tokens): flat fills, hairlines, the segmented switch cells
// (DESIGN_SPEC 3.4: a kit well, flat cells, the selected cell a vertical gradient), invisible hot zones that carry clicks, and the
// editor's colour tokens of spec 3.2 that the stats panels use. Sizes in canvas units, positions from the parent's top-left.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    internal static class OzUeGfx
    {
        // ---- tokens (DESIGN_SPEC 3.2 / 3.4; set colours: OzUeSets.Colour)
        public static readonly Color DarkInk = new Color32(0x17, 0x13, 0x0A, 255);
        public static readonly Color PanelFill = new Color32(29, 33, 31, 255);                 // the "md" plate's own body (Panels/panel_md)
        public static readonly Color RowLine = new Color32(67, 75, 69, 56);                    // rgba(67, 75, 69, .22)
        public static readonly Color GridLine = new Color32(67, 75, 69, 66);                   // rgba(67, 75, 69, .26)
        public static readonly Color SepLine = new Color32(86, 96, 90, 89);                    // rgba(86, 96, 90, .35)
        public static readonly Color FootLine = new Color32(86, 96, 90, 102);                  // rgba(86, 96, 90, .40)
        public static readonly Color Wash = new Color(1f, 1f, 1f, 0.045f);                     // hover / set-in-use column wash
        public static readonly Color PressWash = OzUi.A(OzUi.Amber, 0.10f);
        public static readonly Color SelWash = OzUi.A(OzUi.Amber, 0.06f);
        public static readonly Color GroupFill = OzUi.A(OzUi.GM700, 0.35f);
        public static readonly Color GroupText = OzUi.A(OzUi.Ink, 0.55f);
        public static readonly Color GridHead = new Color32(0x16, 0x1A, 0x18, 255);
        public static readonly Color SheetFill = new Color32(0x0D, 0x0F, 0x0E, 255);
        public static readonly Color TableHead = new Color32(0x1B, 0x20, 0x1D, 255);
        public static readonly Color UnitCell = new Color32(0x14, 0x18, 0x16, 255);
        public static readonly Color CardHead = new Color32(0x15, 0x19, 0x17, 255);
        public static readonly Color DeltaUp = new Color32(0x9B, 0xE3, 0x8A, 255), DeltaDown = new Color32(0xFF, 0x9C, 0x86, 255);
        public static readonly Color CustomText = new Color32(0xBF, 0xF2, 0xFB, 255);
        public static readonly Color InputEdge = new Color32(150, 160, 150, 89);               // rgba(150, 160, 150, .35)

        static float Hair => OzPx.Line(1f);

        public static RectTransform Node(Transform p, string n, float x, float y, float w, float h) => Hud.Node(n, p).TL(x, y, w, h);

        public static Image Rect(Transform p, string n, Color c, float x, float y, float w, float h)
        {
            var i = Hud.Img(p, n, c); i.rectTransform.TL(x, y, w, h); return i;
        }

        public static Image HLine(Transform p, string n, Color c, float x, float y, float w) => Rect(p, n, c, x, y, w, Hair);
        public static Image VLine(Transform p, string n, Color c, float x, float y, float h) => Rect(p, n, c, x, y, Hair, h);

        /// <summary>A 1 u frame round the parent (crisp at any screen height).</summary>
        public static Image[] Frame(Transform p, Color c) => Hud.Border(p, c, Hair);

        /// <summary>One line of text with its caps centred on yMid (OzText.At: the width given keeps it inside, shrinking then cutting).</summary>
        public static OzText Text(Transform p, string n, string s, float size, Color c, float tracking, float x, float yMid, OzH al, float width, OzStyle st = OzStyle.Plain) =>
            Hud.Label(p, n, s, size, c, tracking, st).At(x, yMid, al, true, width);

        // ---- vertical gradients (the selected switch cells, the PLAY-style fills): one small texture per colour pair, released on leaving
        static readonly Dictionary<(Color, Color), Texture2D> grads = new Dictionary<(Color, Color), Texture2D>();

        public static Texture2D GradTex(Color top, Color bottom)
        {
            if (grads.TryGetValue((top, bottom), out var t) && t) return t;
            t = new Texture2D(1, 16, TextureFormat.RGBA32, false) { name = "OzUeGrad", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            for (int i = 0; i < 16; i++) t.SetPixel(0, i, Color.Lerp(bottom, top, i / 15f));
            t.Apply(false, true);
            grads[(top, bottom)] = t;
            return t;
        }

        public static RawImage Grad(Transform p, string n, Color top, Color bottom, float x, float y, float w, float h)
        {
            var rt = Node(p, n, x, y, w, h);
            var ri = rt.gameObject.AddComponent<RawImage>(); ri.raycastTarget = false; ri.texture = GradTex(top, bottom);
            return ri;
        }

        public static void SetGrad(RawImage ri, Color top, Color bottom) { var t = GradTex(top, bottom); if (ri.texture != t) ri.texture = t; }

        // ---- a 1 u dashed line (2 on, 2 off), tinted
        static Texture2D dash;
        public static RawImage Dashed(Transform p, string n, Color c, float x, float y, float w)
        {
            if (!dash)
            {
                dash = new Texture2D(4, 1, TextureFormat.RGBA32, false) { name = "OzUeDash", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
                dash.SetPixels(new[] { Color.white, Color.white, Color.clear, Color.clear }); dash.Apply(false, true);
            }
            var rt = Node(p, n, x, y, w, Hair);
            var ri = rt.gameObject.AddComponent<RawImage>(); ri.raycastTarget = false; ri.texture = dash; ri.color = c;
            SetDashWidth(ri, w);
            return ri;
        }

        public static void SetDashWidth(RawImage ri, float w)
        {
            var r = ri.rectTransform; r.sizeDelta = new Vector2(w, r.sizeDelta.y);
            ri.uvRect = new UnityEngine.Rect(0, 0, Mathf.Max(0.25f, w / 4f), 1);
        }

        /// <summary>The editor is closing: the runtime textures go.</summary>
        public static void Release()
        {
            foreach (var t in grads.Values) if (t) Object.Destroy(t);
            grads.Clear();
            if (dash) Object.Destroy(dash);
            dash = null;
        }

        // ---- clicks
        /// <summary>An invisible HudButton over an element: it takes the clicks (left on release, right) and the hover; the HUD polls it
        /// while the pointer is over a registered blocker. Priority decides where panels overlap.</summary>
        public static HudButton Hot(Transform p, string n, float x, float y, float w, float h, int prio)
        {
            var b = HudButton.Make(p, n, null, new Vector2(w, h), HudButtonKind.Small, 12);
            b.Rt.TL(x, y, w, h);
            b.Bg.enabled = false;
            if (b.Hatch) { Object.Destroy(b.Hatch.gameObject); b.Hatch = null; }
            b.Priority = prio;
            return b;
        }

        /// <summary>An invisible hot zone over a fixed part of a panel (a head, a foot): it takes the hover and the clicks there, and no
        /// tooltip shows, so nothing scrolled under it (a row half out of its list) can react. Its tooltip region too.</summary>
        public static HudButton Swallow(Transform p, string n, float x, float y, float w, float h, int prio)
        {
            var b = Hot(p, n, x, y, w, h, prio);
            OzUeSideTip.Add(b.Rt, (string)null, OzUeSideTip.Place.Side, prio);
            return b;
        }

        /// <summary>A kit button (HudButton.Make, guide 6 states) at a parent-local rect. Its tooltip is the editor's own (OzUeSideTip).</summary>
        public static HudButton Button(Transform p, string n, string text, float x, float y, float w, float h, HudButtonStyle style, int prio, float size = 12f, float tracking = 0.12f)
        {
            var b = HudButton.Make(p, n, text, new Vector2(w, h), HudButtonKind.Small, size, tracking, style);
            b.Rt.TL(x, y, w, h);
            b.Priority = prio;
            return b;
        }

        // ---- pointer
        public static Vector2 PointerScreen { get { var m = Mouse.current; return m != null ? m.position.ReadValue() : Vector2.zero; } }

        public static bool Over(RectTransform rt) => rt && OpenZHud.Contains(rt, PointerScreen);

        /// <summary>A rect's corners in canvas units (origin bottom-left, like hud.Root).</summary>
        public static Rect CanvasRect(RectTransform rt)
        {
            var hud = OpenZHud.I;
            if (!rt || hud == null) return default;
            var wc = new Vector3[4]; rt.GetWorldCorners(wc);
            var a = hud.ScreenToCanvas(RectTransformUtility.WorldToScreenPoint(null, wc[0]));
            var b = hud.ScreenToCanvas(RectTransformUtility.WorldToScreenPoint(null, wc[2]));
            return UnityEngine.Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        public static Vector2 PointerCanvas => OpenZHud.I != null ? OpenZHud.I.ScreenToCanvas(PointerScreen) : Vector2.zero;

        /// <summary>The pointer's x / y inside a rect, from its top-left, in canvas units.</summary>
        public static Vector2 LocalTL(RectTransform rt)
        {
            var r = CanvasRect(rt); var p = PointerCanvas;
            return new Vector2(p.x - r.xMin, r.yMax - p.y);
        }

        public static string Hex(Color c) => OzUi.Hex(c);
        public static string Col(string s, Color c) => "<color=" + Hex(c) + ">" + s + "</color>";
    }

    /// <summary>A segmented switch (DESIGN_SPEC 3.4): a kit well, flat cells with 1 u separators; the selected cell is a vertical
    /// gradient with dark text. Cells are invisible HudButtons with the editor's own look.</summary>
    internal sealed class UeSeg
    {
        public RectTransform Rt;
        public readonly List<HudButton> Hots = new List<HudButton>();
        readonly List<RawImage> fills = new List<RawImage>();
        readonly List<Image> washes = new List<Image>();
        public readonly List<OzText> Labels = new List<OzText>();
        public readonly List<float> X = new List<float>(), W = new List<float>();
        public int Selected = -1;

        public static UeSeg Make(Transform parent, string name, float x, float y, float h, string[] labels, float[] widths, float size, float tracking, int prio)
        {
            var s = new UeSeg();
            float w = 0; foreach (var c in widths) w += c;
            s.Rt = OzUeGfx.Node(parent, name, x, y, w, h);
            var well = Hud.Kit(s.Rt, "Well", "Panels/well_0", new OzRect(0, 0, w, h), null); well.rectTransform.Fill();
            if (!OzKit.Available) { well.color = OzUi.GM900; OzUeGfx.Frame(s.Rt, OzUi.GM400); }
            float cx = 0;
            for (int i = 0; i < labels.Length; i++)
            {
                var hot = OzUeGfx.Hot(s.Rt, "Cell" + i, cx, 0, widths[i], h, prio);
                var fill = OzUeGfx.Grad(hot.Rt, "Sel", OzUi.AmberHi, OzUi.Amber, 1, 1, widths[i] - 2, h - 2); fill.enabled = false;
                var wash = OzUeGfx.Rect(hot.Rt, "Wash", OzUeGfx.Wash, 0, 0, widths[i], h); wash.enabled = false;
                var lab = Hud.Label(hot.Rt, "Label", labels[i], size, OzUi.Muted, tracking, OzStyle.Plain).FillParent(OzH.C);
                if (i > 0) OzUeGfx.VLine(s.Rt, "Sep" + i, OzUi.GM600, cx, 3, h - 6);
                s.Hots.Add(hot); s.fills.Add(fill); s.washes.Add(wash); s.Labels.Add(lab); s.X.Add(cx); s.W.Add(widths[i]);
                cx += widths[i];
            }
            return s;
        }

        Color selText = OzUeGfx.DarkInk; System.Func<int, Color> textOf;

        /// <summary>The selected cell (its gradient top / bottom, its text colour) and the text colour of the other cells.</summary>
        public void Paint(int sel, Color top, Color bottom, Color selTextColour, System.Func<int, Color> text)
        {
            Selected = sel; selText = selTextColour; textOf = text;
            for (int i = 0; i < Hots.Count; i++)
            {
                bool on = i == sel;
                if (fills[i].enabled != on) fills[i].enabled = on;
                if (on) OzUeGfx.SetGrad(fills[i], top, bottom);
            }
            Tick();
        }

        /// <summary>Hover wash and text colours (every frame while shown; cheap: only changed values are written).</summary>
        public void Tick()
        {
            for (int i = 0; i < Hots.Count; i++)
            {
                bool on = i == Selected, w = Hots[i].Hovered && !on;
                if (washes[i].enabled != w) washes[i].enabled = w;
                Labels[i].color = on ? selText : Hots[i].Hovered ? (Color)OzUi.Ink : textOf != null ? textOf(i) : (Color)OzUi.Muted;
            }
        }
    }

    /// <summary>A vertical scroll list (DESIGN_SPEC 3.4): only the list part scrolls, the wheel over it moves it, a slim GM500 thumb
    /// on the right (draggable), a 28 u fade at the bottom while more is below. Clipped by a RectMask2D.</summary>
    internal sealed class UeScroll
    {
        public RectTransform View, Content;
        Image thumb; RawImage fade;
        HudButton grab;
        float contentH, scroll, target, w, h;
        int drag; float dragY0, dragS0;
        public const float Notch = 60f, FadeH = 28f, ThumbW = 6f, BarW = 10f;                 // the prototype's 10 u bar: a 6 u GM500 thumb inside a 2 u edge
        public float Scroll => scroll;
        public float Max => Mathf.Max(0f, contentH - h);

        /// <summary>grabPrio: the priority of the thumb's own hot zone (the 10 u bar under it), above the rows it lies on, so a press on
        /// the thumb never clicks the row under it (0 = no zone).</summary>
        public static UeScroll Make(Transform parent, string name, float x, float y, float width, float height, Color fadeTo, int grabPrio = 0)
        {
            var s = new UeScroll { w = width, h = height };
            s.View = OzUeGfx.Node(parent, name, x, y, width, height);
            s.View.gameObject.AddComponent<RectMask2D>();
            s.Content = OzUeGfx.Node(s.View, "Content", 0, 0, width, height);
            var c0 = fadeTo; c0.a = 0f;
            s.fade = OzUeGfx.Grad(s.View, "Fade", c0, fadeTo, 0, height - FadeH, width, FadeH); s.fade.enabled = false;
            s.thumb = OzUeGfx.Rect(s.View, "Thumb", OzUi.GM500, width - ThumbW - 2f, 0, ThumbW, 20); s.thumb.enabled = false;
            if (grabPrio > 0) { s.grab = OzUeGfx.Swallow(s.View, "ThumbGrab", width - BarW, 0, BarW, 20, grabPrio); s.grab.Rt.gameObject.SetActive(false); }
            return s;
        }

        RectTransform GrabRect => grab != null ? grab.Rt : thumb.rectTransform;

        /// <summary>The view's place and size in its parent (a panel laid out again): the fade, the thumb and the limits follow.</summary>
        public void Place(float x, float y, float width, float height)
        {
            w = width; h = Mathf.Max(1f, height);
            View.TL(x, y, w, h);
            fade.rectTransform.TL(0, h - FadeH, w, FadeH);
            Content.sizeDelta = new Vector2(w, Mathf.Max(h, contentH));
            target = Mathf.Clamp(target, 0f, Max); scroll = Mathf.Clamp(scroll, 0f, Max);
            Apply();
        }

        public void SetContentHeight(float hc)
        {
            contentH = hc;
            Content.sizeDelta = new Vector2(w, Mathf.Max(h, hc));
            target = Mathf.Clamp(target, 0f, Max); scroll = Mathf.Clamp(scroll, 0f, Max);
            Apply();
        }

        public void ScrollToShow(float top, float bottom)
        {
            if (top < target) target = top; else if (bottom > target + h) target = bottom - h;
            target = Mathf.Clamp(target, 0f, Max);
        }

        public void Reset() { target = scroll = 0f; Apply(); }

        /// <summary>An item of the content (placed from the content's top-left) shows in the view now: the rows outside it are switched
        /// off, so a row scrolled away never takes a click or a hover above or below the list.</summary>
        public bool Shows(RectTransform item)
        {
            float top = -item.anchoredPosition.y, s = Mathf.Round(scroll);
            return top + item.sizeDelta.y > s + 0.5f && top < s + h - 0.5f;
        }

        /// <summary>Every frame while shown. wheel: the wheel may move it (false while a number box over it has the keyboard).</summary>
        public void Tick(bool wheel)
        {
            var m = Mouse.current;
            if (m == null || !View.gameObject.activeInHierarchy) return;
            float max = Max;
            if (max > 0f)
            {
                bool over = OzUeGfx.Over(View);
                if (wheel && over)
                {
                    float wv = m.scroll.ReadValue().y;
                    if (Mathf.Abs(wv) > 0.01f) target = Mathf.Clamp(target - Mathf.Sign(wv) * Notch, 0f, max);
                }
                if (m.leftButton.wasPressedThisFrame && thumb.enabled && OzUeGfx.Over(GrabRect)) { drag = 1; dragY0 = OzUeGfx.PointerCanvas.y; dragS0 = target; }
                if (drag == 1)
                {
                    if (!m.leftButton.isPressed) drag = 0;
                    else
                    {
                        float travel = Mathf.Max(1f, h - thumb.rectTransform.sizeDelta.y);
                        target = Mathf.Clamp(dragS0 - (OzUeGfx.PointerCanvas.y - dragY0) / travel * max, 0f, max);
                        scroll = target;
                    }
                }
            }
            else drag = 0;
            Color tc = drag == 1 || (thumb.enabled && OzUeGfx.Over(GrabRect)) ? (Color)OzUi.GM400 : (Color)OzUi.GM500;   // hover GM400 (prototype)
            if (thumb.color != tc) thumb.color = tc;
            float dt = Time.unscaledDeltaTime;
            scroll = OzMotion.Reduced ? target : Mathf.Lerp(scroll, target, 1f - Mathf.Exp(-dt * 16f));
            if (Mathf.Abs(scroll - target) < 0.1f) scroll = target;
            Apply();
        }

        void Apply()
        {
            Content.anchoredPosition = new Vector2(0f, Mathf.Round(scroll));
            float max = Max;
            bool more = max > 0.5f && scroll < max - 1f;
            if (fade.enabled != more) fade.enabled = more;
            bool th = max > 0.5f;
            if (thumb.enabled != th) thumb.enabled = th;
            if (grab != null) grab.Rt.gameObject.SetActiveFast(th);
            if (th)
            {
                float tH = Mathf.Max(24f, h * h / Mathf.Max(h, contentH)), tY = (h - tH) * (scroll / max);
                thumb.rectTransform.TL(w - ThumbW - 2f, tY, ThumbW, tH);
                if (grab != null) grab.Rt.TL(w - BarW, tY, BarW, tH);
            }
        }
    }
}
