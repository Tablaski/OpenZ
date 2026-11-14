using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Unit editor's windows in the game's own confirm look (UI/OzConfirm.cs): the "md" plate with its tab over an opaque backing
    /// (UI/OzUeSolid.cs), the small stencil word, the big title, at most one short line, one row of big kit buttons (the main one
    /// highlighted; ESC on the cap of the safe choice, ENTER on the main one's), an optional close box at the top right. The plan is
    /// UI/OzUeWinPlan.cs; the owner (UI/OzUeLeave.cs, UI/OzUeDialogs.cs) makes the buttons, lays the window out for each of its forms
    /// (Apply) and places it on the canvas (PlaceOn). The plate fades in over 0.12 s (nothing moves, so a click right after it opens lands
    /// where it was aimed).
    /// </summary>
    internal sealed class OzUeWindow
    {
        /// <summary>The windows' buttons react while the HUD is modal (OpenZHud: Priority 100 and over).</summary>
        internal const int Priority = 100;
        internal const float Fade = 0.12f;
        internal readonly RectTransform Root;
        readonly CanvasGroup cg;
        readonly OzText stencil, title, line;
        /// <summary>The close box (LOAD and SHARE): the kit's close glyph, its tooltip names Esc.</summary>
        internal readonly OzUeIconButton CloseBox;
        internal OzUeWinPlan.Win Plan;
        float shownAt = -10f;

        sealed class Btn { public HudButton b; public RectTransform cap; public bool capOn; }
        readonly List<Btn> buttons = new List<Btn>();

        internal OzUeWindow(RectTransform parent, string name, string titleText, Color tab, bool closeBox, Action onClose)
        {
            Root = Hud.Node(name, parent); Root.Place(new OzRect(0, 0, OzUeWinPlan.MinW, 200));
            cg = Root.gameObject.AddComponent<CanvasGroup>();
            OzUeSolid.Under(Hud.Plate(Root, "md", 0f, tab), "md");                                             // opaque: nothing of the fight shows through
            stencil = Hud.Label(Root, "Stencil", OzUePlan.Stencil, OzUeWinPlan.StencilSize, OzUi.Muted, OzUeWinPlan.StencilTrack, OzStyle.Plain);
            title = Hud.Label(Root, "Title", titleText, OzUeWinPlan.TitleSize, OzUi.Ink, OzUeWinPlan.TitleTrack);
            line = Hud.Label(Root, "Line", "", OzUeWinPlan.LineSize, OzUi.Ink2, 0f, OzStyle.Plain);
            if (closeBox)
            {
                CloseBox = new OzUeIconButton(Root, "Close", "close", OzUeWinPlan.CloseSize, 14f);
                CloseBox.B.Priority = Priority;
                CloseBox.B.Tooltip = "CLOSE||ESC";
                CloseBox.B.OnClick = _ => { try { onClose?.Invoke(); } catch (Exception e) { Debug.LogException(e); } };
            }
        }

        /// <summary>A big kit button (16 u caps, 50 u tall, the game's confirm buttons) with the key on its cap (null = none).</summary>
        internal HudButton Button(string name, string label, HudButtonStyle style, string key, string tip, Action onClick)
        {
            var b = HudButton.Make(Root, name, label, new Vector2(OzUeWinPlan.BtnMinW, OzUeWinPlan.BtnH), HudButtonKind.Large, OzUeWinPlan.BtnSize, OzUeWinPlan.BtnTrack, style, true);
            b.Priority = Priority; b.Tooltip = tip;
            if (style == HudButtonStyle.Normal) b.LabelColor = OzUi.Ink2;
            b.Refresh();
            if (onClick != null) b.OnClick = _ => { try { onClick(); } catch (Exception e) { Debug.LogException(e); } };
            var x = new Btn { b = b, capOn = !string.IsNullOrEmpty(key) };
            if (x.capOn) x.cap = Hud.Keycap(b.Content, "Key", key, OzUeWinPlan.BtnMinW - OzUeWinPlan.CapRight, (OzUeWinPlan.BtnH - OzUeWinPlan.CapH) * 0.5f);
            buttons.Add(x);
            return b;
        }

        /// <summary>A button's look in this form: its style (the main one Primary) and whether its key cap shows.</summary>
        internal void Style(HudButton b, HudButtonStyle style, bool capOn)
        {
            var x = buttons.Find(e => e.b == b); if (x == null) return;
            b.Style = style; b.LabelColor = style == HudButtonStyle.Normal ? (Color)OzUi.Ink2 : OzUi.Ink;
            if (b.Label != null) b.Label.Material = style == HudButtonStyle.Primary ? OzStyle.Plain : OzStyle.Underlay;   // as HudButton.Make gives each style
            x.capOn = capOn && x.cap != null;
            if (x.cap) x.cap.gameObject.SetActive(x.capOn);
            b.Refresh();
        }

        internal void SetLine(string text) => line.Set(text ?? "");

        /// <summary>Lays the window out with a plan; `order` = the buttons shown, in the plan's order (the others are hidden).</summary>
        internal void Apply(OzUeWinPlan.Win d, HudButton[] order)
        {
            Plan = d;
            Root.sizeDelta = new Vector2(d.w, d.h);
            stencil.At(d.stencil.x, d.stencil.y, OzH.L, false, d.stencil.w);
            title.At(d.title.x, d.title.y, OzH.L, false, d.title.w);
            line.SetActive(d.hasLine);
            if (d.hasLine) line.At(d.line.x, d.line.y, OzH.L, false, d.line.w);
            foreach (var x in buttons)
            {
                int i = Array.IndexOf(order, x.b);
                x.b.Rt.gameObject.SetActive(i >= 0 && i < d.buttons.Length);
                if (i < 0 || i >= d.buttons.Length) continue;
                var r = d.buttons[i];
                x.b.Rt.Place(new OzRect(r.x, r.y, r.w, r.h));
                var ht = x.b.Hatch ? x.b.Hatch.texture : null;
                if (ht) x.b.Hatch.uvRect = new Rect(0, 0, r.w * 2f / ht.width, r.h * 2f / ht.height);              // the disabled hatch tiles as on a button made this size
                float capW = x.capOn && x.cap ? x.cap.sizeDelta.x : 0f;
                if (x.capOn && x.cap) x.cap.TL(r.w - OzUeWinPlan.CapRight - capW, (r.h - OzUeWinPlan.CapH) * 0.5f, capW, OzUeWinPlan.CapH);
                if (x.b.Label != null) x.b.Label.FillParent(OzH.C, 8f, 0f, x.capOn ? OzUeWinPlan.CapRight + capW + 4f : 8f, 0f);   // centred left of the cap
            }
            if (CloseBox != null)
            {
                CloseBox.B.Rt.gameObject.SetActive(d.hasClose);
                if (d.hasClose) CloseBox.B.Rt.Place(new OzRect(d.close.x, d.close.y, d.close.w, d.close.h));
            }
        }

        /// <summary>Centred on the canvas (UI/OzUePlan.cs Place: 16 u from the edges at least, scaled only on a canvas narrower than the plate).</summary>
        internal void PlaceOn(Vector2 canvas)
        {
            var r = OzUePlan.Place(canvas.x, canvas.y, Plan.w, Plan.h, out float k);
            Root.Place(new OzRect(Mathf.Round(r.x), Mathf.Round(r.y), Plan.w, Plan.h));
            Root.localScale = Vector3.one * k;
        }

        /// <summary>The window was just shown: its fade starts.</summary>
        internal void Shown()
        {
            shownAt = OzMotion.Now;
            cg.alpha = OzMotion.Dur(Fade, true) <= 0.001f ? 1f : 0f;
        }

        /// <summary>Per frame while shown: the fade, the buttons' hover ink, the close box's hover.</summary>
        internal void Tick()
        {
            float d = OzMotion.Dur(Fade, true);
            float a = d <= 0.001f ? 1f : OzEase.OutCubic(Mathf.Clamp01((OzMotion.Now - shownAt) / d));
            if (Mathf.Abs(cg.alpha - a) > 0.001f) cg.alpha = a;
            foreach (var x in buttons) if (x.b.Rt.gameObject.activeSelf) OzUeShell.HoverInk(x.b);
            CloseBox?.Tick();
        }

        /// <summary>SelfCheck: every text of the window at 11 u or more (the game's floor).</summary>
        internal int Audit(StringBuilder log, string what)
        {
            int bad = 0;
            foreach (var t in new[] { stencil, title, line })
                if (t.Size < OzText.MinSize - 0.01f) { bad++; log?.AppendLine("FAIL  P4 " + what + ": text under 11 u: " + t.text); }
            foreach (var x in buttons)
                if (x.b.Label != null && x.b.Label.Size < OzText.MinSize - 0.01f) { bad++; log?.AppendLine("FAIL  P4 " + what + ": a button's text under 11 u: " + x.b.Label.text); }
            return bad;
        }
    }
}
