using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor's floating cards: UI/OzUeCard.cs, UI/OzUeHelpCard.cs, UI/OzUeBackdropMenu.cs, UI/OzUeBalance.cs): an
    /// opaque backing under a card's plate. The game's plate body (Panels/panel_*.png) is 94 % opaque, so a card that floats over the
    /// field let the words of what stood behind it show faintly through its rows (a unit's name and rank labels, its HP numbers, the fire
    /// of a fight). The backing is the plate's own shape (Panels/panel_*_mask: the same cut corners and the same slicing as the body) in
    /// GM950, drawn right under the body and over its shadow: the card looks as it did over the dark floor and hides what is behind it.
    /// The panels along the edges (top bar, counts, play bar, zoom, dock, stats box) keep the game's plate.
    /// </summary>
    public static class OzUeSolid
    {
        public const string Name = "OzSolid";
        /// <summary>The backing's colour: the dark floor's tone, so the card reads as before.</summary>
        public static readonly Color Colour = OzUi.GM950;
        /// <summary>Checks: backings made since the program started.</summary>
        public static int Made;

        /// <summary>Puts the opaque backing under the plate body `body` (as Hud.Plate returns it) of the kit size "lg" / "md" / "sm";
        /// returns the body, so the call wraps Hud.Plate.</summary>
        public static Image Under(Image body, string size)
        {
            if (!body) return body;
            var panel = body.rectTransform.parent as RectTransform; if (!panel) return body;
            string mask = Mask(size);
            Image back = OzKit.Available && OzKit.GetSprite(mask) != null ? OzKit.Img(panel, Name, mask, Image.Type.Sliced, Colour) : Hud.Img(panel, Name, Colour);
            OzKit.FillParent(back.rectTransform);
            back.raycastTarget = false;
            back.transform.SetSiblingIndex(body.transform.GetSiblingIndex());                                 // right under the body, over the shadow
            Made++;
            return body;
        }

        static string Mask(string size) => "Panels/panel_" + (size == "lg" || size == "sm" ? size : "md") + "_mask";

        /// <summary>The Play proof: the panel's backing as it stands (empty = none): opaque, filling the panel, right under the body.</summary>
        public static string Describe(RectTransform panel, out bool ok)
        {
            ok = false;
            if (!panel) return "no panel";
            var t = panel.Find(Name); var img = t ? t.GetComponent<Image>() : null;
            if (!img) return "no backing";
            int i = t.GetSiblingIndex();
            var next = i + 1 < panel.childCount ? panel.GetChild(i + 1) : null;
            bool under = next && (next.name == "OzBody" || next.name == "Body");
            var r = img.rectTransform;
            bool fills = r.anchorMin == Vector2.zero && r.anchorMax == Vector2.one && r.offsetMin == Vector2.zero && r.offsetMax == Vector2.zero;
            bool opaque = img.color.a > 0.999f && img.enabled && t.gameObject.activeSelf;
            ok = under && fills && opaque;
            return (opaque ? "opaque" : "NOT OPAQUE (alpha " + img.color.a.ToString("0.00") + ")") + ", " + (fills ? "the panel's size" : "NOT THE PANEL'S SIZE") + ", "
                   + (under ? "right under the plate body" : "NOT UNDER THE BODY") + (img.sprite ? " (" + img.sprite.name + ")" : " (flat)");
        }

        // ------------------------------------------------------------------------------------------------ counted check
        /// <summary>Menu 2: the backing's sprite has its plate's size, cut corners and slicing for the two plates the cards use ("md",
        /// "sm"), so it covers the body exactly.</summary>
        public static int SelfCheck(StringBuilder log)
        {
            if (log == null) log = new StringBuilder();
            if (!OzKit.Available) return OzUnitEditor.Line(log, true, "P3 opaque cards: no kit plates in this build: the flat fallback plate and a flat backing");
            var sb = new StringBuilder(); bool all = true;
            foreach (var size in new[] { "md", "sm" })
            {
                var body = OzKit.GetSprite("Panels/panel_" + size); var mask = OzKit.GetSprite(Mask(size));
                bool ok = body && mask && body.rect.size == mask.rect.size && body.border == mask.border && Mathf.Approximately(body.pixelsPerUnit, mask.pixelsPerUnit);
                all &= ok;
                sb.Append(sb.Length > 0 ? "; " : "").Append(size).Append(' ').Append(body ? body.rect.width.ToString("0") + " px, border " + body.border.x.ToString("0") : "missing")
                  .Append(ok ? " = its mask" : " DIFFERS FROM ITS MASK");
            }
            return OzUnitEditor.Line(log, all, "P3 opaque cards: the backing (Panels/panel_*_mask, GM950) has its plate's size, cut corners and slicing: " + sb);
        }
    }
}
