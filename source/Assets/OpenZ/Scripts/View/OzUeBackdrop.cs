using System;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (View/OzUnitEditor.cs; owner 6, 6b: "studio backgrounds made in code ... change background in one click", "stylish and
    /// subtle lines and smaller lines and more of them"; spec section 8, E013-E019): the playground's code-drawn floor. Ten line styles
    /// drawn in world metres on the flat 320 x 200 m field (View/OzUeFloor.cs, Resources/OpenZ/UnitEditor/OzUeFloor.shader) and the three
    /// SHOW options of the background popover. The style table below is prototype C round 2d's (colours sRGB, the shader composites in
    /// display colour, so the floor looks as the prototype drew it). Remembered in oz_ue_backdrop, oz_ue_lines, oz_ue_labels, oz_ue_motion.
    /// Two LIGHT styles added at the end (11 Snow field, 12 Desert dunes; the ten keep their order, colours
    /// and the default 1), drawn with Micro-hex's and Topographic's lines in a dark ink. On a light floor the field's marks take a dark ink
    /// too (LightFloor, Ink, InkKeyline: View/OzUeDraw.cs, its labels on their dark plates; the game's selection rings and order dots:
    /// OzUeHooks.DarkMarks, View/SelectionOverlay.cs).
    /// </summary>
    public static class OzUeBackdrop
    {
        public const int StyleCount = 12;
        /// <summary>The ten dark styles of 1.2 (0..9); from here on the light ones (1.3 item 26).</summary>
        public const int FirstLight = 10;
        public static readonly string[] Names = { "Survey grid", "Dot lattice", "Micro-hex", "Topographic", "Range rings",
                                                  "Scan lines", "Triangle mesh", "Blueprint", "Tactical map", "Carbon hatch",
                                                  "Snow field", "Desert dunes" };
        /// <summary>One line per style for its tooltip (spec 8.2 "Name / note").</summary>
        public static readonly string[] Notes =
        {
            "Steel blue square grid 1 / 10 / 50 m with small + marks",
            "Amber dots every 1 m, larger every 10 m, crosses every 50 m",
            "Violet 1 m hex cells, hex rings every 10 m, outline every 50 m",
            "Gold contour lines of a flat floor, every 5th an index line",
            "Radar green rings every 5 m from the field centre, spokes every 10 degrees",
            "Teal cross lines every 0.5 m, brighter every 10 m, lengthwise every 10 m",
            "Cold grey 2 m triangle mesh with 20 m triangles",
            "Deep blue sheet, white 1 / 5 / 25 m lines, edge ticks every 5 m",
            "Olive 10 m squares, 1 m dots, 100 m blocks with corner brackets, hatched bands",
            "Graphite cross hatch every 0.75 m, ember squares every 15 m and 75 m",
            "Pale snow, slate blue 1 m hex cells, hex rings every 10 m, outline every 50 m",
            "Sand, umber contour lines of low dunes, every 5th an index line",
        };

        /// <summary>A style's colours (sRGB): the floor, the faint light in the middle, the motion highlight, the line colours; light = a light
        /// floor (1.3 item 26: dark lines, a dark highlight, the field's marks in a dark ink).</summary>
        public struct Look { public Color32 floor, tint, hi, c1, c2, c3; public bool live, light; }

        /// <summary>Spec 8.2 (prototype C round 2d, THEMES), in style order.</summary>
        public static readonly Look[] Looks =
        {
            L("#05090F", "#7FB0EA", "#A8D4FF", "#5F8FC8", "#7FB0EA", "#A8D4FF"),
            L("#0A0805", "#C9A25A", "#E2C07A", "#C9A25A", "#C9A25A", "#C9A25A"),
            L("#08060F", "#8A76D6", "#B3A2FF", "#8A76D6", "#B3A2FF", "#B3A2FF"),
            L("#0A0905", "#B89A52", "#F0D488", "#8F7A44", "#D7B25E", "#D7B25E"),
            L("#040A06", "#4F9A63", "#9BE0AD", "#4F9A63", "#74C48A", "#74C48A"),
            L("#040A0A", "#3FA6A0", "#7FE0D8", "#3FA6A0", "#5CC4BD", "#5CC4BD"),
            L("#07080A", "#9AA4B0", "#D4DCE6", "#9AA4B0", "#B8C2CE", "#B8C2CE"),
            L("#061226", "#6F8FD0", "#DBE7FF", "#DBE7FF", "#DBE7FF", "#DBE7FF", true),
            L("#0B0C08", "#8A8F62", "#C8CD94", "#8A8F62", "#A9AE7C", "#A9AE7C"),
            L("#0A0807", "#7A6A62", "#FFB070", "#7A6A62", "#C0703A", "#C0703A"),
            // 1.3 item 26: the light styles (lines of Micro-hex / Topographic in a dark ink; hi = the light effect's and the hologram's ink)
            Light("#B5BFC8", "#EEF3F7", "#1E3A5C", "#102542", "#16304E", "#16304E"),
            Light("#CDB78E", "#F2E6CC", "#5A3A14", "#3E2A12", "#3A240C", "#3A240C"),
        };

        /// <summary>Style 4 (Topographic): the height between two neighbour contours of the fixed height field, so the average gap between
        /// them is 2.6 m (the prototype's contours(): 2.6 x the mean slope of the field sampled every 3 m; computed once by
        /// tools/p2_floor_reference.py with the same hash and noise as the shader).</summary>
        public const float ContourStep = 0.015812737f;

        const string KeyStyle = "oz_ue_backdrop", KeyLines = "oz_ue_lines", KeyLabels = "oz_ue_labels", KeyMotion = "oz_ue_motion";
        static bool loaded; static int style; static bool lines = true, labels, motion = true;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            style = Mathf.Clamp(PlayerPrefs.GetInt(KeyStyle, 0), 0, StyleCount - 1);
            lines = PlayerPrefs.GetInt(KeyLines, 1) == 1;
            labels = PlayerPrefs.GetInt(KeyLabels, 0) == 1;
            motion = PlayerPrefs.GetInt(KeyMotion, 1) == 1;
        }

        /// <summary>The floor style in use (0..11), remembered (oz_ue_backdrop), default 0 (Survey grid). A saved 0..9 of 1.2 keeps its style.</summary>
        public static int Style { get { Load(); return style; } }
        public static void SetStyle(int index)
        {
            Load();
            index = ((index % StyleCount) + StyleCount) % StyleCount;
            if (index == style) return;
            style = index; PlayerPrefs.SetInt(KeyStyle, index); PlayerPrefs.Save();
            Debug.Log($"[OpenZ UnitEditor] floor style {index + 1}. {Names[index]}");
            Changed?.Invoke();
        }

        /// <summary>E017: floor lines on (default) / off (only the floor's base colour, its middle light and the vignette).</summary>
        public static bool FloorLines { get { Load(); return lines; } }
        public static void SetFloorLines(bool on) { Load(); if (lines == on) return; lines = on; Save(KeyLines, on, "floor lines"); }
        /// <summary>E018: a name label over every squad / vehicle (read by the world marks); default off.</summary>
        public static bool NameLabels { get { Load(); return labels; } }
        public static void SetNameLabels(bool on) { Load(); if (labels == on) return; labels = on; Save(KeyLabels, on, "name labels on every unit"); }
        /// <summary>E019: the style's slow light effect (it also stands still while the playground is paused); default on.</summary>
        public static bool Motion { get { Load(); return motion; } }
        public static void SetMotion(bool on) { Load(); if (motion == on) return; motion = on; Save(KeyMotion, on, "floor light motion"); }

        /// <summary>The style's "hi" colour (the obstacle hologram mixes it toward #E6EFE9, spec 3.2 HOLO).</summary>
        public static Color HiColor => Looks[Style].hi;
        /// <summary>The style in use's look.</summary>
        public static Look Current => Looks[Style];

        // ------------------------------------------------------------------------------------------------ 1.3 item 26: ink on a light floor
        /// <summary>The style in use is a light floor (Snow field, Desert dunes).</summary>
        public static bool LightFloor => Looks[Style].light;
        /// <summary>On a light floor the light mark colours of the field (INK, the team rings, the neutral ring, the blocked line) are drawn
        /// in the same hue, darkened to the luminance InkLum (linear), and their alpha a as InkAlphaMin + (1 - InkAlphaMin) a (a fainter
        /// mark stays fainter; every mark at least 3:1 against both light floors: Editor/OzV13LightFloorsCheck.cs).</summary>
        public const float InkLum = 0.05f, InkAlphaMin = 0.85f;
        /// <summary>The dark ink of a field mark's colour on a light floor (View/OzUeDraw.cs; Editor/OzV13LightFloorsCheck.cs).</summary>
        public static Color Ink(Color c)
        {
            var l = c.linear; float y = 0.2126f * l.r + 0.7152f * l.g + 0.0722f * l.b;
            float k = y > InkLum ? Mathf.Pow(InkLum / y, 1f / 2.2f) : 1f;
            return new Color(c.r * k, c.g * k, c.b * k, InkAlphaMin + (1f - InkAlphaMin) * Mathf.Clamp01(c.a));
        }
        /// <summary>The 1 px keyline round a floor mark on a light floor (View/OzUeDraw.cs; on a dark floor it stays the near-black one): a
        /// pale one (INK) at the same alpha, so a dark mark still parts from a dark unit, shadow or wreck. a = the mark's own alpha.</summary>
        public static Color InkKeyline(float a) => new Color(244f / 255f, 242f / 255f, 234f / 255f, Mathf.Min(0.6f, a * 0.8f));

        /// <summary>The style, a SHOW option or the motion changed.</summary>
        public static event Action Changed;

        static void Save(string key, bool on, string what)
        {
            PlayerPrefs.SetInt(key, on ? 1 : 0); PlayerPrefs.Save();
            Debug.Log($"[OpenZ UnitEditor] {what} {(on ? "on" : "off")}");
            Changed?.Invoke();
        }

        /// <summary>Leaving the editor: no listener of a dead session stays (ARCHITECTURE 5.6).</summary>
        internal static void ResetEvents() { Changed = null; }

        static Look L(string floor, string tint, string hi, string c1, string c2, string c3, bool live = false)
            => new Look { floor = Hex(floor), tint = Hex(tint), hi = Hex(hi), c1 = Hex(c1), c2 = Hex(c2), c3 = Hex(c3), live = live };
        static Look Light(string floor, string tint, string hi, string c1, string c2, string c3)
            { var l = L(floor, tint, hi, c1, c2, c3); l.light = true; return l; }

        /// <summary>"#RRGGBB" to an opaque Color32.</summary>
        public static Color32 Hex(string h)
        {
            int v = Convert.ToInt32(h.Substring(1), 16);
            return new Color32((byte)((v >> 16) & 255), (byte)((v >> 8) & 255), (byte)(v & 255), 255);
        }
    }
}
