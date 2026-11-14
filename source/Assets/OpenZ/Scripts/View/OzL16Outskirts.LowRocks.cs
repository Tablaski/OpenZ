// OpenZ (GPL-3.0) - Level 16: the flume rocks stand back with their land in a low shot.
// Round 4's rock formations at the ends of the rim's waterfall beds (PlaceFlumeRocks: L16_Outskirts_Rock_*) stand on the land beyond
// the rim's edge. While the flyover's low-shot look is on, that land stands back (LateUpdate: the low look's own far water lies there),
// but the rocks stayed: 18 of the 21 then stood 4 to 12 m over the far water, all of them came in only when the land was done (in the
// middle of the dawn shot), and the cliff face at the north-west fall towered over the postcard of the second shot. So they stand back
// with their land: the low shots show the rim's ends as they did before the land was made; the steep camera keeps every rock exactly
// as round 4 placed it. Held off by Renderer.forceRenderingOff (the rock, its name and its place stay: TreeCheck still reads them).
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzL16Outskirts
    {
        public const string RockPrefix = "L16_Outskirts_Rock_";
        /// <summary>Flume rocks held off by the low look now.</summary>
        public static int LowRocksHeld;

        /// <summary>LateUpdate: the flume rocks drawn or held off with the land they stand on.</summary>
        void LowRocks(bool show)
        {
            int n = 0;
            foreach (Transform c in transform)
            {
                if (!c || !c.name.StartsWith(RockPrefix)) continue;
                foreach (var r in c.GetComponentsInChildren<Renderer>(true)) if (r) r.forceRenderingOff = !show;
                if (!show) n++;
            }
            LowRocksHeld = n;
        }

        /// <summary>OzL16FlyoverFlora.LandmarkCheck: the flume rocks there are, and how many of them the camera may draw now.</summary>
        public static void LowRockCount(out int rocks, out int drawn)
        {
            rocks = drawn = 0;
            if (!I) return;
            foreach (Transform c in I.transform)
            {
                if (!c || !c.name.StartsWith(RockPrefix)) continue;
                rocks++;
                foreach (var r in c.GetComponentsInChildren<Renderer>(true))
                    if (r && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy) { drawn++; break; }
            }
        }
    }
}
