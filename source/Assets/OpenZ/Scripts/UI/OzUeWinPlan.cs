// 1.2 item 109 (the Unit editor's windows, as a game's menus): the pure plan of the editor's game-style windows (UI/OzUeWindow.cs builds
// them; UI/OzUeLeave.cs and UI/OzUeDialogs.cs use them): the leave question, the reset confirm, LOAD and SHARE. The game's own confirm
// look (UI/OzConfirm.cs): the "md" plate with its tab, a small stencil word, a big short title (26 u), at most one short line, one row of
// big buttons (50 u) with one main button and the keys on their caps (ESC on the safe choice, ENTER on the main one). Nothing of the
// engine is used here: the off-engine layout model compiles this file alone with UI/OzUePlan.cs and the game font's metrics, and the
// editor runs the same code with TextMeshPro's widths (OzUeShell.TextW).
using System;
using System.Globalization;

namespace OpenZ
{
    public static class OzUeWinPlan
    {
        /// <summary>The side margin of everything on the plate, as the game's confirm.</summary>
        public const float PadX = 28f;
        public const float StencilTop = 20f, StencilSize = 11f, StencilTrack = 0.3f;
        public const float TitleTop = 38f, TitleSize = 26f, TitleTrack = 0.06f;
        public const float LineTop = 80f, LineSize = 14f;
        /// <summary>Where the body starts under the title: without a line, and with the line.</summary>
        public const float BodyTop = 82f, BodyTopLine = 112f;
        public const float BtnH = 50f, BtnSize = 16f, BtnTrack = 0.16f, BtnPadX = 18f, BtnMinW = 120f, BtnGap = 10f, LeftGap = 24f, Bottom = 26f;
        public const float CapH = 14f, CapSize = 11f, CapTrack = 0.06f, CapPad = 8f, CapGap = 10f, CapRight = 12f;
        public const float CloseSize = 28f, CloseInset = 12f, CloseRoom = 8f;
        /// <summary>LOAD's list: rows 34 u, at most six shown (the wheel scrolls more), the toggle under it.</summary>
        public const float RowH = 34f, ListGap = 12f, CheckH = 22f, CheckGap = 16f;
        public const int MaxRows = 6;
        public const float MinW = 600f;
        /// <summary>The tallest plate allowed: the shortest design canvas (UI SCALE 160 % on a 1536 x 730 screen: 537 u) less 32.</summary>
        public const float MaxH = 505f;

        static float Ceil(float v) => (float)Math.Ceiling(v);
        static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        public struct Win
        {
            public float w, h;
            /// <summary>The title's room (its measured width must fit) and the line's (one line, empty when none).</summary>
            public UeR stencil, title, line, list, check, close;
            public float titleW, lineW;
            public bool hasLine, hasList, hasCheck, hasClose;
            public int rows;
            public UeR[] buttons;
            public string[] labels, caps;
            /// <summary>The first `left` buttons stand from the left edge, the others from the right edge; main = the highlighted one.</summary>
            public int left, main;
        }

        /// <summary>A key cap's width (UI/OpenZHudKit.cs Hud.Keycap: the key's width + 8, 14 u at least).</summary>
        public static float CapW(string key, OzUePlan.Measure m) => string.IsNullOrEmpty(key) ? 0f : Math.Max(CapH, Ceil(m(key, CapSize, CapTrack) + CapPad));

        /// <summary>A big button: its label (16 u caps, tracking 0.16) + the padding + its key cap, 120 u at least.</summary>
        public static float ButtonW(string label, string cap, OzUePlan.Measure m)
            => Math.Max(BtnMinW, Ceil(m(label, BtnSize, BtnTrack) + 2f * BtnPadX + (string.IsNullOrEmpty(cap) ? 0f : CapW(cap, m) + CapGap)));

        /// <summary>One window: title (and the line when there is one), LOAD's list of `rows` rows and its toggle, one row of buttons (labels
        /// with their caps; null = none), the close box at the top right. The plate is as wide as its widest part, MinW at least.</summary>
        public static Win Plan(string title, string line, int rows, bool check, bool close, string[] labels, string[] caps, int left, int main, OzUePlan.Measure m)
        {
            var d = new Win { labels = labels, caps = caps, left = left, main = main, hasLine = !string.IsNullOrEmpty(line), hasList = rows > 0, hasCheck = check, hasClose = close };
            d.rows = Math.Max(0, Math.Min(rows, MaxRows));
            int n = labels.Length;
            var bw = new float[n];
            float lw = 0f, rw = 0f;
            for (int i = 0; i < n; i++)
            {
                bw[i] = ButtonW(labels[i], caps != null && i < caps.Length ? caps[i] : null, m);
                if (i < left) lw += bw[i] + (i > 0 ? BtnGap : 0f); else rw += bw[i] + (i > left ? BtnGap : 0f);
            }
            float row = lw + rw + (left > 0 && left < n ? LeftGap : 0f);
            d.titleW = m(title, TitleSize, TitleTrack);
            d.lineW = d.hasLine ? m(line, LineSize, 0f) : 0f;
            float closeRoom = close ? CloseSize + CloseRoom : 0f;
            d.w = Ceil(Math.Max(MinW, Math.Max(row + 2f * PadX, Math.Max(d.titleW + closeRoom + 2f * PadX, d.lineW + 2f * PadX))));
            float inner = d.w - 2f * PadX;
            d.stencil = new UeR(PadX, StencilTop, inner - closeRoom, 0.727f * StencilSize);
            d.title = new UeR(PadX, TitleTop, inner - closeRoom, 0.727f * TitleSize);
            d.line = d.hasLine ? new UeR(PadX, LineTop, inner, Ceil(0.98f * LineSize)) : new UeR(PadX, LineTop, inner, 0f);
            d.close = close ? new UeR(d.w - CloseInset - CloseSize, CloseInset, CloseSize, CloseSize) : new UeR(0f, 0f, 0f, 0f);
            float y = d.hasLine ? BodyTopLine : BodyTop;
            if (d.hasList) { d.list = new UeR(PadX, y, inner, Math.Max(1, d.rows) * RowH); y = d.list.B + ListGap; }
            if (check) { d.check = new UeR(PadX, y, inner, CheckH); y = d.check.B + CheckGap; }
            d.buttons = new UeR[n];
            float x = PadX;
            for (int i = 0; i < left && i < n; i++) { d.buttons[i] = new UeR(x, y, bw[i], BtnH); x += bw[i] + BtnGap; }
            x = d.w - PadX;
            for (int i = n - 1; i >= left; i--) { x -= bw[i]; d.buttons[i] = new UeR(x, y, bw[i], BtnH); x -= BtnGap; }
            d.h = Ceil(y + BtnH + Bottom);
            return d;
        }

        /// <summary>The window holds on a canvas: the plate at full size (never scaled down) and 16 u inside the canvas, MaxH at most; the
        /// title and the line fit their room on one line; every button inside the plate's margins, on one row, none over another, the left
        /// group LeftGap u or more before the right one; the list, the toggle and the texts over the row; the close box inside the top
        /// right corner, clear of the title. why = the first problem.</summary>
        public static bool WinOk(Win d, float cw, float ch, out string why)
        {
            why = null;
            var r = OzUePlan.Place(cw, ch, d.w, d.h, out float k);
            if (k < 0.999f) { why = "the plate is scaled to " + F(k * 100f) + " %"; return false; }
            if (!r.Inside(new UeR(0f, 0f, cw, ch), 16f)) { why = "the plate " + r + " is not 16 u inside the canvas " + F(cw) + " x " + F(ch); return false; }
            if (d.h > MaxH + 0.01f) { why = "the plate is " + F(d.h) + " u tall (" + F(MaxH) + " at most)"; return false; }
            if (d.titleW > d.title.w + 0.01f) { why = "the title (" + F(d.titleW) + " u) does not fit its " + F(d.title.w) + " u"; return false; }
            if (d.hasLine && d.lineW > d.line.w + 0.01f) { why = "the line (" + F(d.lineW) + " u) does not fit one line of " + F(d.line.w) + " u"; return false; }
            var body = new UeR(PadX - 0.01f, 0f, d.w - 2f * PadX + 0.02f, d.h - Bottom + 0.01f);
            float rowY = d.buttons.Length > 0 ? d.buttons[0].y : d.h - Bottom - BtnH;
            for (int i = 0; i < d.buttons.Length; i++)
            {
                if (!d.buttons[i].Inside(body, 0f)) { why = d.labels[i] + " " + d.buttons[i] + " is not inside the plate's margins"; return false; }
                if (Math.Abs(d.buttons[i].y - rowY) > 0.01f) { why = d.labels[i] + " is not on the row"; return false; }
                for (int j = i + 1; j < d.buttons.Length; j++) if (d.buttons[i].Overlaps(d.buttons[j])) { why = d.labels[i] + " lies over " + d.labels[j]; return false; }
            }
            if (d.left > 0 && d.left < d.buttons.Length && d.buttons[d.left].x - d.buttons[d.left - 1].R < LeftGap - 0.01f)
            { why = d.labels[d.left - 1] + " and " + d.labels[d.left] + " are " + F(d.buttons[d.left].x - d.buttons[d.left - 1].R) + " u apart (" + F(LeftGap) + " wanted)"; return false; }
            float textB = d.hasLine ? d.line.B : d.title.B;
            if (d.hasList && (d.list.y < textB + 8f || d.list.B > (d.hasCheck ? d.check.y : rowY) - 0.01f)) { why = "the list " + d.list + " is not between the title and the row"; return false; }
            if (d.hasCheck && (d.check.B > rowY - 0.01f || d.check.y < (d.hasList ? d.list.B : textB))) { why = "the toggle " + d.check + " is not over the row"; return false; }
            if (textB > rowY - 8f) { why = "the texts reach the button row"; return false; }
            if (d.hasClose && (d.close.R > d.w - 0.01f || d.close.y < 0f || d.close.x < d.title.R - 0.01f)) { why = "the close box " + d.close + " is not in the top right corner clear of the title"; return false; }
            return true;
        }

        /// <summary>The window in words for the checks: "580 x 188 u: STAY | LEAVE, SAVE &amp; LEAVE (main)".</summary>
        public static string Describe(Win d)
        {
            var s = F(d.w) + " x " + F(d.h) + " u:";
            for (int i = 0; i < d.labels.Length; i++)
                s += (i == 0 ? " " : i == d.left ? " | " : ", ") + d.labels[i] + (d.caps != null && i < d.caps.Length && !string.IsNullOrEmpty(d.caps[i]) ? " [" + d.caps[i] + "]" : "") + (i == d.main ? " (main)" : "");
            return s;
        }
    }
}
