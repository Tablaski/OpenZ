using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUnitEditor.cs): the help card, DESIGN_SPEC E190-E193 (prototype C round 2d, word for word).
    /// A "md" plate 490 u wide, 8 u under "?", its right edge on the button's right edge (1259,41.5 at 1920). MOUSE: 10 rows, key cap
    /// column 124 u, text 13 u INK_2 (line 1.35), row gap 6; KEYS: two columns (key column 64 u). Closes on a click outside, Esc (the
    /// popover closer, priority 110), "?" again, or H. Opens ui_panel_open, closes ui_panel_close.
    /// </summary>
    internal sealed class OzUeHelpCard
    {
        internal const float W = 490f, Head = 36f, PadX = 12f, PadTop = 8f, PadBottom = 12f, RowGap = 6f, CapCol = 124f, CapColKeys = 64f, ColGap = 8f, TwoGap = 12f, Text = 13f;
        internal static readonly string[,] Mouse =
        {
            { "Drag", "A unit from the dock onto the field (or click it, then the field)" },
            { "Click", "A unit, blue or red: pick it, its stats open" },
            { "Shift-click", "Or drag a box on the ground: pick more, both teams" },
            { "R-click", "Units picked: the ground = move, an enemy = attack" },
            { "R-click", "The flag = take it; an empty vehicle or gun = a robot climbs in" },
            { "Ctrl + R-click", "Forced attack: an obstacle (explosives only), empty hardware, any unit" },
            { "R-click", "Nothing picked: pick the unit (its stats open)" },
            { "i", "The i on a unit: its unit card" },
            { "Middle drag", "Hold the middle button: turn the camera (left / right) and tilt it (up / down); the compass turns it back" },
            { "Wheel", "Zoom; WASD or the arrow keys move the view" },
        };
        internal static readonly string[,] Keys =
        {
            { "Space", "Play / pause" }, { "T", "Table" }, { "B", "Balance tab" }, { "I", "Stats box" }, { "P", "Dock" }, { "H", "Hide all" },
            { "Home", "Fit all" }, { "Del", "Remove picked" }, { "Esc", OzUePlan.HelpEsc }, { "1 2 3", "Blue, red, neutral" }, { "[ / ]", "Balance stat" },
        };

        readonly RectTransform root;
        readonly List<OzText> texts = new List<OzText>();
        readonly List<float> widths = new List<float>();                                                    // the column of a one-line text (0 = it wraps)
        internal float Height { get; private set; }
        internal bool Open { get; private set; }
        internal RectTransform Root => root;
        internal static int Opens;

        internal OzUeHelpCard(RectTransform parent, OpenZHud hud)
        {
            root = Hud.Node("OzUe_HelpCard", parent);
            root.Place(new OzRect(0, 0, W, 600));
            OzUeSolid.Under(Hud.Plate(root, "md", Head), "md");                                              // the first head is the plate's own (it follows the cut corner); opaque (UI/OzUeSolid.cs)
            Hud.Label(root, "Head_MOUSE", "MOUSE", 13, OzUi.Ink, 0.16f).At(PadX, 1f + Head * 0.5f, OzH.L, true, W - 2 * PadX);
            float y = 1f + Head;
            y += PadTop;
            float tw = W - 2f * PadX - CapCol - ColGap;
            for (int i = 0; i < Mouse.GetLength(0); i++)
            {
                var t = Hud.Label(root, "M" + i, Mouse[i, 1], Text, OzUi.Ink2, 0f, OzStyle.Plain).Body(PadX + CapCol + ColGap, y, tw);
                texts.Add(t); widths.Add(0f);
                float th = Mathf.Max(20f, Mathf.Ceil(t.preferredHeight));
                t.rectTransform.sizeDelta = new Vector2(tw, th);
                float ty = y + (th - Mathf.Ceil(t.preferredHeight)) * 0.5f;
                t.rectTransform.anchoredPosition = new Vector2(PadX + CapCol + ColGap, -ty);
                OzUeShell.Keycap(root, "MK" + i, Mouse[i, 0], PadX, y + (th - 20f) * 0.5f);
                y += th + RowGap;
            }
            y += PadBottom - RowGap;
            y = Band(root, "KEYS", y);
            y += PadTop;
            float colW = (W - 2f * PadX - TwoGap) * 0.5f, ktw = colW - CapColKeys - ColGap;
            int rows = (Keys.GetLength(0) + 1) / 2;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < 2; c++)
                {
                    int i = r * 2 + c; if (i >= Keys.GetLength(0)) break;
                    float x = PadX + c * (colW + TwoGap);
                    OzUeShell.Keycap(root, "KK" + i, Keys[i, 0], x, y);
                    texts.Add(Hud.Label(root, "K" + i, Keys[i, 1], Text, OzUi.Ink2, 0f, OzStyle.Plain).At(x + CapColKeys + ColGap, y + 10f, OzH.L, true, ktw)); widths.Add(ktw);
                }
                y += 20f + RowGap;
            }
            y += PadBottom - RowGap;
            var st = Hud.Label(root, "S0", OzUePlan.HelpStats, OzUePlan.HelpStatsSize, OzUi.Ink2, 0f, OzStyle.Plain).At(PadX, y + 10f, OzH.L, true, W - 2f * PadX);   // which stats the game uses: one line under the keys
            texts.Add(st); widths.Add(W - 2f * PadX);
            y += 20f + PadBottom - RowGap;
            Height = Mathf.Ceil(y);
            root.sizeDelta = new Vector2(W, Height);
            if (hud) hud.AddBlocker(root);
            root.gameObject.SetActive(false);
        }

        /// <summary>A section head inside the card: 36 u, a flat olive band (the plate head's colour) with a 1 u line, 13 u caps 0.16 INK.</summary>
        static float Band(RectTransform p, string title, float y)
        {
            var band = Hud.Img(p, "Band_" + title, OzUi.A(OzUi.OD600, 0.5f)); band.rectTransform.Place(new OzRect(1, y + 1, W - 2, Head));
            var line = Hud.Img(p, "BandLine_" + title, OzUi.A(OzUi.GM400, 0.4f)); line.rectTransform.Place(new OzRect(1, y + Head, W - 2, OzPx.Line(1f)));
            Hud.Label(p, "Head_" + title, title, 13, OzUi.Ink, 0.16f).At(PadX, y + 1 + Head * 0.5f, OzH.L, true, W - 2 * PadX);
            return y + 1f + Head;
        }

        internal void Toggle(Rect helpButton, Vector2 canvas) { if (Open) Close(true); else Show(helpButton, canvas); }

        internal void Show(Rect helpButton, Vector2 canvas)
        {
            Place(helpButton, canvas);
            root.gameObject.SetActive(true); root.SetAsLastSibling();
            Open = true; Opens++;
            AudioDirector.UI("ui_panel_open");
        }

        internal void Close(bool sound)
        {
            if (!Open) return;
            Open = false; root.gameObject.SetActive(false);
            if (sound) AudioDirector.UI("ui_panel_close");
        }

        internal bool EscClose() { if (!Open) return false; Close(true); return true; }

        /// <summary>Under "?" (8 u), right edges flush, 12 u inside the canvas.</summary>
        internal static Rect Plan(Rect helpButton, Vector2 canvas, float height)
        {
            float w = Mathf.Min(W, canvas.x - 24f);
            float x = Mathf.Clamp(helpButton.xMax - w, 12f, Mathf.Max(12f, canvas.x - 12f - w));
            return new Rect(x, helpButton.yMax + 8f, w, height);
        }

        internal void Place(Rect helpButton, Vector2 canvas)
        {
            var r = Plan(helpButton, canvas, Height);
            root.Place(new OzRect(Mathf.Round(r.x), Mathf.Round(r.y), W, Height));
        }

        /// <summary>A press outside the card (and not on "?") closes it.</summary>
        internal void Tick(RectTransform helpButton)
        {
            if (!Open) return;
            var m = UnityEngine.InputSystem.Mouse.current; if (m == null) return;
            if (!(m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame)) return;
            var p = m.position.ReadValue();
            if (OpenZHud.Contains(root, p) || (helpButton && OpenZHud.Contains(helpButton, p))) return;
            Close(true);
        }

        /// <summary>SelfCheck: every text of the card at its size (none under 11 u, none wider than its column).</summary>
        internal int Audit(System.Text.StringBuilder log, string where)
        {
            int bad = 0;
            for (int i = 0; i < texts.Count; i++)
            {
                var t = texts[i];
                if (t.Size < OzText.MinSize - 0.01f) { bad++; log.AppendLine("FAIL  P4 help card " + where + ": text under 11 u: " + t.text); }
                if (widths[i] > 0f && t.WidthAt(t.text) > widths[i] + 0.5f) { bad++; log.AppendLine("FAIL  P4 help card " + where + ": \"" + t.text + "\" is wider than its column (" + widths[i] + " u)"); }
            }
            return bad;
        }
    }
}
