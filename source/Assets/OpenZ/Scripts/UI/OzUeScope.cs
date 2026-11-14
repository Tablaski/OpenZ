using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the STATS box, UI/OzUeStatsRoot.cs): the short line right under the set switch that says which stats are used, always in
    /// view: "In use here: Custom. Levels: OpenZ, or Custom on the level screen." (the set's name in its colour; shorter wordings when the box is
    /// narrow: UI/OzUePlan.cs ScopeFor). Its tooltip says the whole of it: the field here uses the set picked above for every unit, both
    /// teams, and a Custom change at once; the levels play OpenZ's own numbers unless the player picks Custom on the level screen, which
    /// plays the Custom stats he saved (View/OzUeProfile.cs); how to reset. The set switch's cells are 24 u tall for it (compact 20 u); the
    /// body below starts where it did.
    /// </summary>
    internal sealed class OzUeScope
    {
        OzText text; float width; UeSet shown = (UeSet)(-1);
        /// <summary>Checks: the line's text as shown (rich text) and whether the longest words were used.</summary>
        internal string Text => text != null ? text.text : null;
        internal bool Long { get; private set; }
        internal RectTransform Rt => text != null ? text.rectTransform : null;

        internal static OzUeScope Build(RectTransform box, UeBoxLayout L, int prio)
        {
            var s = new OzUeScope { width = L.w - 22f };
            float size = L.compact ? OzUePlan.ScopeSizeCompact : OzUePlan.ScopeSize;
            float cell = OzUePlan.CellH(L.compact), mid = OzUePlan.ScopeMid(L.setTop, L.bodyTop, L.compact);
            s.text = OzUeGfx.Text(box, "Scope", "", size, OzUi.Muted, 0f, 11f, mid, OzH.L, s.width);
            var zone = OzUeGfx.Node(box, "ScopeTip", 11f, L.setTop + cell, s.width, Mathf.Max(8f, L.bodyTop - L.setTop - cell));
            OzUeSideTip.Add(zone, OzUePlan.ScopeTip, OzUeSideTip.Place.Side, prio, box);
            return s;
        }

        /// <summary>The set in use now (the box's PaintSets).</summary>
        internal void Paint(UeSet set)
        {
            if (text == null || set == shown) return;
            shown = set;
            string name = OzUeGfx.Col(OzUeSets.Name(set), OzUeSets.Colour(set));
            string t = OzUePlan.ScopeFor(name, width, text.FontSize, (s, size, k) => text.WidthAt(s), out _);
            Long = t == OzUePlan.ScopeLong(name);
            text.Set(t);
        }
    }
}
