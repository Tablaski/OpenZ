using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// box or it is picked, an INK outline round its foot (on the box under it, or the floor), its health bar over its top and its words
    /// "BOX  hp / max HP" over "Ctrl + right-click attacks it"; a hit box shows its bar alone; with "Name labels on every unit" the other boxes
    /// show "BOX". Drawn where the hologram is drawn (its fall too).
    /// </summary>
    internal sealed partial class OzUeMarks
    {
        /// <summary>Checks (the last frame): box outlines drawn, the word lines of the box pointed at or picked (2 = both), its first line.</summary>
        internal static int BoxOutlines, BoxWords;
        internal static string LastBoxTop;
        readonly List<ZObject> boxes = new List<ZObject>();

        void BoxMarks(ZGame game, ZObject hover, UeInspect kind, ZObject insp, bool all)
        {
            BoxOutlines = BoxWords = 0;
            foreach (var o in boxes)
            {
                if (Gone(o)) continue;
                bool on = o == hover || (kind == UeInspect.Obstacle && insp == o);
                bool hit = o.Health < o.MaxHealth;
                if (!on && !hit && !all) continue;
                Vector3 p = Pos(game, o);
                var v = OzUeObstacleViews.Of(o);
                p.y = v && v.IsBox ? v.BoxBaseY : Mathf.Max(0, OzUePlayground.BoxLevel(o)) * OzUePlayground.BoxHeightM;
                float hx = Mathf.Max(1f, o.WidthPix * ZConst.PX_TO_M * 0.5f), hz = Mathf.Max(1f, o.HeightPix * ZConst.PX_TO_M * 0.5f);
                if (on) { OzUeDraw.RectOutline(p, hx + 0.3f, hz + 0.3f, OzUi.Ink, 5f, 4f, 1.5f); BoxOutlines++; }
                Vector3 top = p + Vector3.up * OzUePlayground.BoxHeightM;
                if (on || hit) OzUeDraw.HealthBar(top, OzUePlan.ObstacleBarUp, 40f, OzUePlan.ObstacleBarH, Frac(o), BarColour(Frac(o)));
                if (on || all)
                {
                    string first = on ? Text(o, "BOX  ", "", true) : "BOX";
                    Words(top, first, on ? OzUePlan.ObstacleHint : null, NeutralText, OzUePlan.ObstacleWordsUp);
                    if (on) { BoxWords = 2; LastBoxTop = first; }
                }
            }
        }
    }
}
