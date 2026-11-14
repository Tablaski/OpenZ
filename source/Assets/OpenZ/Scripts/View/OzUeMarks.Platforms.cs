using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// hit, hovered or picked, its words only when hovered or picked, "HIGH PLATFORM  5405 / 20000 HP" over "Ctrl + right-click attacks it".
    /// </summary>
    internal sealed partial class OzUeMarks
    {
        /// <summary>Checks (the last frame): the platform words shown (2 = both lines) and the first line.</summary>
        internal static int PlatformWords;
        internal static string LastPlatformTop;
        readonly List<ZObject> platforms = new List<ZObject>();

        void PlatformMarks(ZGame game, ZObject hover, UeInspect kind, ZObject insp, bool all)
        {
            PlatformWords = 0;
            foreach (var o in platforms)
            {
                if (Gone(o)) continue;
                bool on = o == hover || (kind == UeInspect.Platform && insp == o);
                var p = ZGame.PxToWorld(o.CenterX, o.CenterY);
                float hx = o.WidthPix * ZConst.PX_TO_M * 0.5f, hz = o.HeightPix * ZConst.PX_TO_M * 0.5f;
                if (on) OzUeDraw.RectOutline(p, hx + 0.3f, hz + 0.3f, OzUi.Ink, 5f, 4f, 1.5f);
                var info = o.World != null ? o.World.PlatformOf(o.Id) : null;
                Vector3 top = info != null ? OzUePlatformViews.Point(info, ZWorld.PlatformWideTiles * 0.5f, ZWorld.PlatformBackRows + ZWorld.PlatformDeckRows * 0.5f, (float)info.DeckM + OzUePlatformView.KerbM) : p;
                bool hit = o.Health < o.MaxHealth;
                if (on || hit) OzUeDraw.HealthBar(top, OzUePlan.ObstacleBarUp, 56f, OzUePlan.ObstacleBarH, Frac(o), BarColour(Frac(o)));
                if (on || all)
                {
                    string name = OzUePlayground.FullName(OzUePlayground.PlatformIdOf(o));
                    Words(top, on ? Text(o, name + "  ", "", true) : name, on ? OzUePlan.ObstacleHint : null, NeutralText, OzUePlan.ObstacleWordsUp);
                    if (on) { PlatformWords = 2; LastPlatformTop = Text(o, name + "  ", "", true); }
                }
            }
        }
    }
}
