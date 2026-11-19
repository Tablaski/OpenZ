using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// at true size, snapped to the 2 x 2 tiles it will take and drawn on the level it will stand on: over a stack the cube's outline sits on
    /// top of it (level 2, 3 ...), with a dashed guide from the floor up to it; its words say "HOLO BOX" on the floor, "HOLO BOX ON TOP: LEVEL n
    /// OF 6" on a stack. No room = #FF7A5E and the refusal's words (a stack 6 high says so).
    /// </summary>
    internal sealed partial class OzUePlaceDriver
    {
        /// <summary>Checks: the last frame's box preview: the level shown (-1 = refused), its outline's floor height (m).</summary>
        internal static int BoxLevelShown = -1; internal static float BoxBaseShown;
        static float boxTallM = OzUePlayground.BoxHeightM;

        void BoxPreview(System.Collections.Generic.List<Vector3> spots, bool ok)
        {
            Color holo = ok ? Color.Lerp(OzUeBackdrop.HiColor, HoloPale, 0.42f) : NoRoom;
            float half = OzUePlayground.BoxSideM * 0.5f, H = OzUePlayground.BoxHeightM;
            BoxLevelShown = -1; boxTallM = H;
            foreach (var s in spots)
            {
                int lv = OzUePlayground.BoxLevelAt(s);
                var c = OzUePlayground.BoxFootMiddle(s);
                float y0 = Mathf.Max(0, lv) * H;
                var b = c + Vector3.up * y0;
                OzUeDraw.RectOutline(b, half, half, OzUi.A(holo, 0.9f), 0f, 0f, 1f);
                OzUeDraw.RectOutline(b + Vector3.up * H, half, half, OzUi.A(holo, 0.6f), 0f, 0f, 1f);
                for (int k = 0; k < 4; k++) OzUeDraw.Up(b + new Vector3(k % 3 == 0 ? -half : half, 0f, k < 2 ? -half : half), H, OzUi.A(holo, 0.6f), 1f);
                if (y0 > 0.01f)
                {   // over a stack: the footprint on the floor, dashed, and a dashed guide up to the box
                    OzUeDraw.RectOutline(c, half, half, OzUi.A(holo, 0.35f), 4f, 4f, 1f);
                    OzUeDraw.Up(c, y0, OzUi.A(holo, 0.35f), 1f);
                }
                BoxLevelShown = ok ? lv : -1; BoxBaseShown = y0; boxTallM = y0 + H;
            }
        }

        /// <summary>The preview's words for the box.</summary>
        static string BoxLine() => BoxLevelShown > 0 ? "HOLO BOX ON TOP: LEVEL " + (BoxLevelShown + 1) + " OF " + ZWorld.BoxLevels : "HOLO BOX";
        /// <summary>The preview's height for the words' place (the stack's top).</summary>
        static float BoxTallM => boxTallM;
    }
}
