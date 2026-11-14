using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// the gear: the floor styles (the ten dark ones, and since 1.3 item 26 two light ones) as pictures drawn by the floor's own shader (OzUeFloorThumb.shader, the prototype's thumbnail
    /// camera), one click switches the floor at once; then SHOW: floor lines, name labels on every unit, floor light motion. It closes on a
    /// click outside, Esc (P4's Esc chain, priority EscPopover) or the gear again. 2 columns, 3 when two would not fit the canvas's height.
    /// </summary>
    public sealed class OzUeBackdropMenu
    {
        public const float Width2 = 300f, Width3 = 440f, HeadH = 36f, ThumbLabelH = 22f, OptH = 22f;
        /// <summary>Checks: opened, style clicks.</summary>
        public static int Opens, Picks;

        OpenZHud hud;
        public RectTransform Panel;
        RectTransform head1, head2;
        OzText title, sub, show;
        readonly HudButton[] thumbs = new HudButton[OzUeBackdrop.StyleCount];
        readonly RawImage[] pics = new RawImage[OzUeBackdrop.StyleCount];
        readonly OzText[] names = new OzText[OzUeBackdrop.StyleCount];
        readonly Image[][] edges = new Image[OzUeBackdrop.StyleCount][];
        readonly Image[][] rings = new Image[OzUeBackdrop.StyleCount][];
        OzUeCheck lines, labels, motion;
        bool open, built; int cols = 2; float panelW = Width2, panelH;
        RectTransform anchor;
        public bool IsOpen => open;
        public UeBox Box { get; private set; }

        public void Build(OzUnitEditor ed, Transform popovers, RectTransform gear)
        {
            hud = ed.Hud; anchor = gear;
            Opens = Picks = 0;
            Panel = Hud.Node("UE_BackgroundPopover", popovers);
            OzUeSolid.Under(Hud.Plate(Panel, "md"), "md");                                                    // opaque over the field (UI/OzUeSolid.cs)
            hud.AddBlocker(Panel);
            head1 = Header("Head", out title, "BACKGROUND", out sub, "floor style");
            head2 = Header("Show", out show, "SHOW", out _, null);
            for (int i = 0; i < OzUeBackdrop.StyleCount; i++)
            {
                int idx = i;
                var b = OzUeW.Bare(Panel, "Style" + (i + 1), 135f, 98.8f);
                pics[i] = OzUeW.Raw(b.Rt, "Picture", null, 1f, 1f, 133f, 74.8f, Color.white);
                names[i] = Hud.Label(b.Rt, "Name", (i + 1) + ". " + OzUeBackdrop.Names[i], 12f, OzUi.Ink2, 0f, OzStyle.Plain);
                edges[i] = Hud.Border(b.Rt, OzUi.GM500);
                var ring = Hud.Node("Selected", b.Rt); ring.Fill(-1f, -1f, -1f, -1f);
                rings[i] = Hud.Border(ring, OzUi.Amber);
                b.Tooltip = OzUeW.Tip(OzUeBackdrop.Names[i], null, OzUeBackdrop.Notes[i]);
                b.OnClick = c => { Picks++; OzUeBackdrop.SetStyle(idx); Paint(); };
                thumbs[i] = b;
            }
            lines = new OzUeCheck(Panel, "FloorLines", "Floor lines", Width2 - 26f);
            lines.B.OnClick = c => OzUeBackdrop.SetFloorLines(!OzUeBackdrop.FloorLines);
            labels = new OzUeCheck(Panel, "NameLabels", "Name labels on every unit", Width2 - 26f);
            labels.B.OnClick = c => OzUeBackdrop.SetNameLabels(!OzUeBackdrop.NameLabels);
            motion = new OzUeCheck(Panel, "Motion", "Floor light motion", Width2 - 26f);
            motion.B.OnClick = c => OzUeBackdrop.SetMotion(!OzUeBackdrop.Motion);
            Panel.gameObject.SetActive(false);
            OzUeKeys.OnEsc(OzUeKeys.EscPopover, TryClose);
        }

        RectTransform Header(string name, out OzText t, string text, out OzText s, string subText)
        {
            var h = Hud.Node(name, Panel);
            OzUeW.Gradient(h, "Fill", OzUi.A(OzUi.GM600, 0.55f), OzUi.A(OzUi.GM800, 0.25f));
            var line = Hud.Img(h, "Line", OzUi.A(OzUi.GM400, 0.4f));
            var lr = line.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(1f, 0f); lr.pivot = new Vector2(0.5f, 0f); lr.sizeDelta = new Vector2(0f, 1f); lr.anchoredPosition = Vector2.zero;
            t = Hud.Label(h, "Title", text, 13f, OzUi.Ink, 0.16f, OzStyle.Plain);
            s = subText != null ? Hud.Label(h, "Sub", subText, 12f, OzUi.Muted, 0f, OzStyle.Plain) : null;
            return h;
        }

        /// <summary>Lay the popover out for this canvas (columns, size, place 8 u under the gear, 12 u inside the canvas).</summary>
        public void Layout(float cw, float ch)
        {
            float H2 = Height(2);
            float topY = GearBottom() + 8f;
            cols = topY + H2 + 12f > ch ? 3 : 2;                                                     // compact: 3 columns, 440 wide (spec 2.3)
            panelW = cols == 3 ? Width3 : Width2;
            float pad = cols == 3 ? 8f : 10f, gap = cols == 3 ? 6f : 8f;
            float tw = (panelW - 2f * OzUeBottomLayout.Border - 2f * pad - (cols - 1) * gap) / cols, picH = (tw - 2f) * 9f / 16f, th = picH + 2f + ThumbLabelH;
            float x0 = OzUeBottomLayout.Border, y = OzUeBottomLayout.Border;
            head1.TL(x0, y, panelW - 2f, HeadH);
            title.At(12f, HeadH * 0.5f, OzH.L, true, 0f);
            if (sub != null) sub.At(12f + title.WidthAt("BACKGROUND") + 10f, HeadH * 0.5f, OzH.L, true, 0f);       // the header's 10 u gap (.ph)
            y += HeadH + pad;
            int rows = (OzUeBackdrop.StyleCount + cols - 1) / cols;
            for (int i = 0; i < OzUeBackdrop.StyleCount; i++)
            {
                int r = i / cols, c = i % cols;
                thumbs[i].Rt.TL(x0 + pad + c * (tw + gap), y + r * (th + gap), tw, th);
                pics[i].rectTransform.TL(1f, 1f, tw - 2f, picH);
                names[i].At(1f + 6f, 1f + picH + ThumbLabelH * 0.5f, OzH.L, true, tw - 14f);
            }
            y += rows * th + (rows - 1) * gap + pad;
            head2.TL(x0, y, panelW - 2f, HeadH);
            show.At(12f, HeadH * 0.5f, OzH.L, true, 0f);
            y += HeadH + (cols == 3 ? 6f : 8f);
            float og = cols == 3 ? 6f : 8f;
            lines.B.Rt.TL(x0 + 12f, y, panelW - 26f, OptH); y += OptH + og;
            labels.B.Rt.TL(x0 + 12f, y, panelW - 26f, OptH); y += OptH + og;
            motion.B.Rt.TL(x0 + 12f, y, panelW - 26f, OptH); y += OptH + (cols == 3 ? 10f : 12f) + OzUeBottomLayout.Border;
            panelH = y;
            float gx = GearRight();
            float px = Mathf.Max(OzUeBottomLayout.Gap, Mathf.Min(cw - panelW - OzUeBottomLayout.Gap, gx - panelW));
            float py = Mathf.Min(topY, Mathf.Max(OzUeBottomLayout.Gap, ch - panelH - OzUeBottomLayout.Gap));
            Box = new UeBox(px, py, panelW, panelH);
            Panel.TL(px, py, panelW, panelH);
        }

        /// <summary>The 2-column popover's height (spec E013: 722 u with 10 styles; 1.3 item 26: 828.9 u with 12. 3 columns: 601 u, 4 rows, as with 10).</summary>
        public static float Height(int cols)
        {
            float pad = cols == 3 ? 8f : 10f, gap = cols == 3 ? 6f : 8f, w = cols == 3 ? Width3 : Width2;
            float tw = (w - 2f - 2f * pad - (cols - 1) * gap) / cols, th = (tw - 2f) * 9f / 16f + 2f + ThumbLabelH;
            int rows = (OzUeBackdrop.StyleCount + cols - 1) / cols;
            float og = cols == 3 ? 6f : 8f;
            return 2f + HeadH + pad + rows * th + (rows - 1) * gap + pad + HeadH + og + 3f * OptH + 2f * og + (cols == 3 ? 10f : 12f);
        }

        float GearBottom() { var r = CanvasRect(anchor); return r.y + r.height; }
        float GearRight() { var r = CanvasRect(anchor); return r.x + r.width; }

        /// <summary>A RectTransform's rect in canvas units, top-left origin.</summary>
        Rect CanvasRect(RectTransform rt)
        {
            if (!rt || !hud || !hud.Root) return new Rect(17f, 56f, 28f, 28f);
            var wc = new Vector3[4]; rt.GetWorldCorners(wc);
            Vector2 a = hud.ScreenToCanvas(RectTransformUtility.WorldToScreenPoint(null, wc[1])), b = hud.ScreenToCanvas(RectTransformUtility.WorldToScreenPoint(null, wc[3]));
            float ch = OzUnitEditor.CanvasSize.y;
            return new Rect(a.x, ch - a.y, b.x - a.x, a.y - b.y);
        }

        public void Toggle()
        {
            if (open) Close(); else Open();
        }

        public void Open()
        {
            if (open) return;
            open = true; Opens++;
            if (!built)
            {   // spec E015: the pictures are made once, when the popover first opens
                built = true;
                for (int i = 0; i < OzUeBackdrop.StyleCount; i++) { var m = OzUeFloor.ThumbMaterial(i); pics[i].material = m; pics[i].color = m ? Color.white : (Color)OzUeBackdrop.Looks[i].floor; }
            }
            var cs = OzUnitEditor.CanvasSize;
            Layout(cs.x, cs.y);
            Panel.gameObject.SetActive(true);
            Panel.SetAsLastSibling();
            Paint();
            Debug.Log("[OpenZ UnitEditor] background popover open");
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            Panel.gameObject.SetActive(false);
        }

        bool TryClose() { if (!open) return false; Close(); return true; }

        void Paint()
        {
            int s = OzUeBackdrop.Style;
            for (int i = 0; i < OzUeBackdrop.StyleCount; i++)
            {
                bool on = i == s, hov = thumbs[i].Hovered;
                foreach (var e in edges[i]) e.color = on ? (Color)OzUi.Amber : hov ? (Color)OzUi.Ink2 : (Color)OzUi.GM500;
                foreach (var e in rings[i]) e.enabled = on;
                names[i].color = on ? (Color)OzUi.AmberHi : (Color)OzUi.Ink2;
            }
        }

        public void Tick(bool hidden)
        {
            if (hidden && open) Close();
            if (!open) return;
            // a press anywhere outside the popover and the gear closes it
            var m = Mouse.current;
            if (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame))
            {
                var p = m.position.ReadValue();
                if (!OpenZHud.Contains(Panel, p) && !(anchor && OpenZHud.Contains(anchor, p))) { Close(); return; }
            }
            Paint();
            lines.Set(OzUeBackdrop.FloorLines); labels.Set(OzUeBackdrop.NameLabels); motion.Set(OzUeBackdrop.Motion);
        }
    }
}
