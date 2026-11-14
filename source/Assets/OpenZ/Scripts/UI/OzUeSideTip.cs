// 1.2 item 6, View/OzUnitEditor.cs: the tooltips of the STATS box, the TABLE and the unit card (DESIGN_SPEC E177 / E178). Inside the box a
// tooltip opens BESIDE it (left of the box, level with the row: owner 6k, "a tooltip covered rows"), 12 u, max 300 u; over the table and
// the card it opens under the control (above it when there is no room), 13 u, max 380 u. Lines split at "|"; a leading "Key:" part is
// MUTED. Drawn with the game's tooltip plate (Kit Panels/tooltip). Hidden on any press, while placing / dragging and under a dialog.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    internal static class OzUeSideTip
    {
        public enum Place { Side, Below }

        sealed class Reg
        {
            public RectTransform rt, side; public Func<Vector2, int> part; public Func<int, string> text; public Place place; public int prio, order;
        }

        static readonly List<Reg> regs = new List<Reg>();
        static RectTransform root; static Image plate; static OzText label; static CanvasGroup cg;
        static Reg hot; static int hotPart = int.MinValue; static float since; static string shown; static int order;
        public const float Delay = 0.35f;
        /// <summary>Tooltips shown (the checks).</summary>
        public static int Shown;
        /// <summary>Tooltip registrations now (the frame-cost check: they must not grow while the panels stay as they are).</summary>
        internal static int Registered => regs.Count;

        public static void Build(RectTransform layer)
        {
            root = Hud.Node("OzUeTip", layer); root.anchorMin = root.anchorMax = Vector2.zero; root.pivot = new Vector2(0, 1);
            plate = Hud.Kit(root, "Bg", "Panels/tooltip", new OzRect(0, 0, 48, 48), null); plate.rectTransform.Fill();
            if (!OzKit.Available) plate.color = OzUi.A(OzUi.GM900, 0.97f);
            label = Hud.Label(root, "Text", "", 12, OzUi.Ink2, 0f, OzStyle.Plain).Body(9, 6, 280);
            cg = root.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;
            root.gameObject.SetActive(false);
        }

        public static void Clear()
        {
            regs.Clear(); hot = null; shown = null; root = null; plate = null; label = null; cg = null; order = 0;
        }

        /// <summary>A tooltip for an element. part(local point from its top-left) picks a part of it (a column), -1 = none there;
        /// text(part) builds the text once per part. side = the panel a Side tooltip opens beside. prio: the highest wins where
        /// elements overlap (the card over the table, the table over the box).</summary>
        public static void Add(RectTransform rt, Func<Vector2, int> part, Func<int, string> text, Place place, int prio, RectTransform side = null)
        {
            if (rt == null || text == null) return;
            regs.Add(new Reg { rt = rt, part = part, text = text, place = place, prio = prio, side = side, order = order++ });
        }

        public static void Add(RectTransform rt, Func<string> text, Place place, int prio, RectTransform side = null) =>
            Add(rt, null, _ => text(), place, prio, side);

        public static void Add(RectTransform rt, string text, Place place, int prio, RectTransform side = null) =>
            Add(rt, null, _ => text, place, prio, side);

        /// <summary>Forget the tooltips of elements that are gone (a rebuilt panel).</summary>
        public static void Prune() { regs.RemoveAll(r => r.rt == null); if (hot != null && hot.rt == null) hot = null; }

        public static void Hide() { if (root) root.gameObject.SetActive(false); hot = null; shown = null; }

        /// <summary>Every frame (OzUeStatsRoot.Update).</summary>
        public static void Tick(bool blocked)
        {
            if (!root) return;
            var m = Mouse.current;
            if (blocked || m == null || m.leftButton.isPressed || m.rightButton.isPressed || m.middleButton.isPressed) { Hide(); since = OzMotion.Now; return; }
            var p = m.position.ReadValue();
            Reg best = null;
            for (int i = regs.Count - 1; i >= 0; i--)
            {
                var r = regs[i];
                if (r.rt == null || !r.rt.gameObject.activeInHierarchy) continue;
                if (best != null && r.prio <= best.prio) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(r.rt, p, null)) best = r;
            }
            int part = int.MinValue;
            if (best != null) part = best.part != null ? best.part(OzUeGfx.LocalTL(best.rt)) : 0;
            if (best != hot || part != hotPart) { hot = best; hotPart = part; since = OzMotion.Now; shown = null; root.gameObject.SetActive(false); }
            if (hot == null || hotPart < 0 || OzMotion.Now - since < Delay) return;
            if (shown == null)
            {
                string t = null;
                try { t = hot.text(hotPart); } catch (Exception e) { Debug.LogException(e); }
                if (string.IsNullOrEmpty(t)) { hotPart = -1; return; }
                Show(t, hot);
            }
        }

        static void Show(string text, Reg r)
        {
            shown = text;
            bool side = r.place == Place.Side;
            float size = side ? 12f : 13f, maxW = side ? 300f : 380f, padX = side ? 9f : 10f, padY = side ? 6f : 8f;
            label.FontSize = size;
            var lines = text.Split('|');
            var sb = new StringBuilder(text.Length + 64);
            float w = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                int c = l.IndexOf(':');
                string shownLine = c > 0 && c <= 26 ? OzUeGfx.Col(l.Substring(0, c + 1), OzUi.Muted) + l.Substring(c + 1) : l;
                if (i > 0) sb.Append('\n');
                sb.Append(shownLine);
                w = Mathf.Max(w, label.WidthAt(shownLine));
            }
            float inner = Mathf.Min(w + 1f, maxW - 2 * padX);
            label.rectTransform.sizeDelta = new Vector2(inner, 400);
            label.rectTransform.anchoredPosition = new Vector2(padX, -padY);
            label.Set(sb.ToString());
            float h = label.preferredHeight;
            var size2 = new Vector2(inner + 2 * padX, h + 2 * padY);
            root.sizeDelta = size2;
            var cs = OzUnitEditor.CanvasSize;
            var er = OzUeGfx.CanvasRect(r.rt);
            Vector2 pos;   // top-left, canvas units, origin bottom-left
            if (side)
            {
                var sr = r.side ? OzUeGfx.CanvasRect(r.side) : er;
                pos = new Vector2(sr.xMin - size2.x - 10f, er.center.y + size2.y * 0.5f);
                if (pos.x < 8f) pos.x = sr.xMax + 10f;
            }
            else
            {
                pos = new Vector2(er.center.x - size2.x * 0.5f, er.yMin - 8f);
                if (pos.y - size2.y < 8f) pos.y = er.yMax + 8f + size2.y;
            }
            pos.x = Mathf.Clamp(pos.x, 8f, Mathf.Max(8f, cs.x - 8f - size2.x));
            pos.y = Mathf.Clamp(pos.y, 8f + size2.y, Mathf.Max(8f + size2.y, cs.y - 8f));
            root.anchoredPosition = pos;
            root.SetAsLastSibling();
            root.gameObject.SetActive(true);
            cg.alpha = 1f;
            Shown++;
        }
    }
}
