using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (View/OzUnitEditor.cs; spec E214, 2.2): the canvas space covered by the editor's floating panels, per side. Each module
    /// reports its own panels (P2 bottom: the dock or its rail; P3 right: the stats box or its rail; P4 top: the top bar), in canvas
    /// units from that edge; a side's inset is the largest report on it. The free field is what is left: the camera keeps its centre
    /// there (P2), the play bar centres on it (P2). While H hides the panels (OzUnitEditor.PanelsHidden) only the top bar's inset counts.
    /// </summary>
    public static class OzUeInsets
    {
        static readonly Dictionary<string, float>[] bySide = { new Dictionary<string, float>(), new Dictionary<string, float>(), new Dictionary<string, float>(), new Dictionary<string, float>() };
        static readonly float[] value = new float[4];

        /// <summary>The free field changed (a panel opened, closed, resized, H). Raised from the reporter's Update / LateUpdate.</summary>
        public static event Action Changed;

        /// <summary>A panel of `who` covers `units` canvas units from this side (0 = none any more).</summary>
        public static void Set(UeSide side, string who, float units)
        {
            if (string.IsNullOrEmpty(who)) who = "?";
            int s = (int)side;
            if (s < 0 || s > 3) return;
            units = float.IsNaN(units) ? 0f : Mathf.Max(0f, units);
            var d = bySide[s];
            if (d.TryGetValue(who, out float old) && Mathf.Abs(old - units) < 0.01f) return;
            d[who] = units;
            float m = 0f; foreach (var kv in d) m = Mathf.Max(m, kv.Value);
            if (Mathf.Abs(m - value[s]) < 0.01f) return;
            value[s] = m;
            Changed?.Invoke();
        }

        /// <summary>The inset of a side now (0 for the left, right and bottom while the panels are hidden).</summary>
        public static float Get(UeSide side)
        {
            int s = (int)side;
            if (s < 0 || s > 3) return 0f;
            if (OzUnitEditor.PanelsHidden && side != UeSide.Top) return 0f;
            return value[s];
        }

        /// <summary>The free field for a canvas of cw x ch units and these insets (pure; origin bottom-left like hud.Root).</summary>
        public static Rect FreeField(float cw, float ch, float left, float right, float top, float bottom)
        {
            float w = Mathf.Max(0f, cw - left - right), h = Mathf.Max(0f, ch - top - bottom);
            return new Rect(Mathf.Min(left, cw), Mathf.Min(bottom, ch), w, h);
        }

        /// <summary>Canvas units, origin bottom-left like hud.Root.</summary>
        public static Rect FreeFieldCanvas
        {
            get
            {
                var cs = OzUnitEditor.CanvasSize;
                return FreeField(cs.x, cs.y, Get(UeSide.Left), Get(UeSide.Right), Get(UeSide.Top), Get(UeSide.Bottom));
            }
        }

        /// <summary>Screen pixels, origin bottom-left.</summary>
        public static Rect FreeFieldScreen
        {
            get
            {
                var r = FreeFieldCanvas; float k = OzUnitEditor.PxPerUnit;
                return new Rect(r.x * k, r.y * k, r.width * k, r.height * k);
            }
        }

        /// <summary>OzUnitEditor.SetPanelsHidden: every free field changes at once.</summary>
        internal static void NotifyChanged() => Changed?.Invoke();

        internal static void Reset()
        {
            foreach (var d in bySide) d.Clear();
            for (int i = 0; i < 4; i++) value[i] = 0f;
            Changed = null;
        }

        /// <summary>Counted check: the free field rect at the canvas sizes of spec 2.1 with the panels of spec section 1 scaled by the
        /// anchor rules of 2.2 (top bar 40, stats box clamp(360, 0.22 CW, 470) + 2 x 12 on the right, the bottom band 130): inside the
        /// canvas, never negative; its centre left of the canvas centre (the stats box covers the right side); with H only the top bar.</summary>
        internal static int SelfCheck(StringBuilder log)
        {
            int f = 0;
            var sizes = new[] { new Vector2(1920, 1080), new Vector2(1607, 904), new Vector2(1807, 859), new Vector2(1728, 1080) };
            foreach (var cs in sizes)
            {
                float box = Mathf.Clamp(0.22f * cs.x, 360f, 470f);
                var r = FreeField(cs.x, cs.y, 0f, 12f + box + 12f, 40f, 130f);
                bool ok = r.width > 0f && r.height > 0f && r.xMax <= cs.x + 0.01f && r.yMax <= cs.y + 0.01f && r.x >= 0f && r.y >= 0f && r.center.x < cs.x * 0.5f;
                f += OzUnitEditor.Line(log, ok, $"free field rect at canvas {cs.x:0} x {cs.y:0}: {r.width:0} x {r.height:0} u from ({r.x:0}, {r.y:0}), centre x {r.center.x:0}");
            }
            var hidden = FreeField(1920f, 1080f, 0f, 0f, 40f, 0f);
            f += OzUnitEditor.Line(log, hidden.height == 1040f && hidden.width == 1920f, "panels hidden (H): the free field is the window under the top bar (1920 x 1040 u)");
            return f;
        }
    }
}
