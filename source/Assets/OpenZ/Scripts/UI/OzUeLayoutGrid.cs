using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, UI/OzUeBottomLayout.cs): the counted check that no panel lies over another at every design size of
    /// DESIGN_SPEC 2.1 (1920 x 1080, 1600 x 900, 2000 x 1125, 2560 x 1440, 2560 x 1600, 1536 x 730, 1366 x 768) and every UI SCALE step
    /// of the options (OzText.ScalePresets, 90 .. 160 %), with the game font's real widths: the top bar, the counts plate, the STATS box
    /// (open, or only its rail), the play bar, the zoom control and the dock (open, or only its rail). One line per screen size, its six
    /// steps and three states each. The zoom control used to stay at the play bar's height when the bar had no room beside it (1366 x 768
    /// at 145 and 160 %, 1600 x 900, 1536 x 730 and 2560 x 1600 at 160 %): the bar lay partly on it. It goes one row up there now, over
    /// the play bar's left end (OzUeBottomLayout.LayPlay).
    /// </summary>
    internal static class OzUeLayoutGrid
    {
        public static readonly (int w, int h)[] Screens = { (1920, 1080), (1600, 900), (2000, 1125), (2560, 1440), (2560, 1600), (1536, 730), (1366, 768) };
        /// <summary>The counts plate's widest (both teams at the unit cap: "300/300", View/OzUeCap.cs), canvas u.</summary>
        public const float CountsW = 240f, CountsH = 36f;
        static readonly string[] States = { "open", "dock closed", "STATS box closed" };

        public static int SelfCheck(StringBuilder log)
        {
            if (log == null) log = new StringBuilder();
            int f = 0;
            GameObject temp = null;
            try
            {
                temp = new GameObject("UE_LayoutGridCheck", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                var m = new OzUeMeasure(temp.transform);
                var labels = OzUeDock.TileLabels();
                string flag = OzUePlayground.ShortName(OzUePlayground.Flag), obstacle = OzUePlayground.ShortName(OzUePlayground.Obstacle);
                foreach (var sc in Screens)
                {
                    int layouts = 0, stacked = 0; var bad = new StringBuilder();
                    foreach (float s in OzText.ScalePresets)
                        for (int st = 0; st < States.Length; st++)
                        {
                            var cs = OzUnitEditor.CanvasFor(sc.w, sc.h, s);
                            bool boxOpen = st != 2, dockOpen = st != 1;
                            float boxW = Mathf.Clamp(0.22f * cs.x, 360f, 470f);
                            var i = new OzUeBottomLayout.In
                            {
                                cw = cs.x, ch = cs.y, insetR = UeBoxLayout.Gap + (boxOpen ? boxW : UeBoxLayout.RailW), insetT = 40f, dockOpen = dockOpen,
                                tiles = labels, measure = m.Width, flagLabel = flag, obstacleLabel = obstacle,
                            };
                            var o = OzUeBottomLayout.Lay(i);
                            var L = UeBoxLayout.For(cs, 40f, o.insetB);
                            var box = boxOpen ? L.Box : L.Rail;
                            var panels = new (string n, UeBox b)[]
                            {
                                ("top bar", new UeBox(0f, 0f, cs.x, 40f)), ("counts plate", new UeBox(12f, 52f, CountsW, CountsH)),
                                (boxOpen ? "STATS box" : "STATS rail", new UeBox(box.x, box.y, box.width, box.height)),
                                ("play bar", o.play.plate), ("zoom", o.zoom.plate), (dockOpen ? "dock" : "dock rail", dockOpen ? o.dock.plate : o.dock.rail),
                            };
                            layouts++; if (o.zoom.stacked) stacked++;
                            for (int a = 0; a < panels.Length; a++)
                                for (int b = a + 1; b < panels.Length; b++)
                                    if (panels[a].b.Overlaps(panels[b].b) && bad.Length < 400)
                                        bad.Append(' ').Append(Pct(s)).Append(' ').Append(States[st]).Append(": ").Append(panels[a].n).Append(" x ").Append(panels[b].n).Append(';');
                            bool inside = o.play.plate.x >= OzUeBottomLayout.Gap - 0.01f && o.play.plate.Right <= cs.x - i.insetR - OzUeBottomLayout.Gap + 0.01f
                                          && o.zoom.plate.x >= OzUeBottomLayout.Gap - 0.01f && o.zoom.plate.y >= 40f + 12f;
                            if (!inside && bad.Length < 400) bad.Append(' ').Append(Pct(s)).Append(' ').Append(States[st]).Append(": the play bar or the zoom outside the free field;");
                        }
                    f += OzUnitEditor.Line(log, bad.Length == 0, "P2 layout grid " + sc.w + "x" + sc.h + " at every UI SCALE step (" + Steps() + ") x " + States.Length
                                           + " states (" + layouts + " layouts): no panel over another (top bar, counts plate, STATS box / rail, play bar, zoom, dock / rail)"
                                           + (stacked > 0 ? "; the zoom over the play bar in " + stacked : "") + (bad.Length > 0 ? ":" + bad : ""));
                }
            }
            finally { if (temp) { if (Application.isPlaying) Object.Destroy(temp); else Object.DestroyImmediate(temp); } }
            return f;
        }

        static string Pct(float s) => Mathf.RoundToInt(s * 100f).ToString(CultureInfo.InvariantCulture) + " %";

        static string Steps()
        {
            var l = new List<string>();
            foreach (var s in OzText.ScalePresets) l.Add(Mathf.RoundToInt(s * 100f).ToString(CultureInfo.InvariantCulture));
            return string.Join(" / ", l) + " %";
        }
    }
}
