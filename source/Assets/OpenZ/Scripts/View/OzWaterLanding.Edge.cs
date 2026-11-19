// OpenZ (GPL-3.0)
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 review R8: a piece moved by hand (the blown-off turret, robot parts, anything that calls <see cref="Enter"/>) that comes down at the
    /// river's edge with its middle and its pivot over the bank but a good part of it over open water was taken as a dry landing: it lay at the
    /// bank's height over the water, no splash, no water line round it. Now its footprint is looked at too
    /// (<see cref="EdgeWater"/>: 8 points round the bottom of its box, 85 % out): 3 or more over open water (never lava) = it went in there.
    /// The rule then does what it does for every piece in the water, from the middle of the wet points: the splash, the rings and the sound of
    /// its size, the dip, the bob, resting partly under the surface (60 % of its height, on the bed where shallower), the soft water line and
    /// the foam ring of the water shaders; and, as for any piece that lands too near a bank, it settles that much further out into the water
    /// (up to 1.5 m). Off (<see cref="EdgeOn"/> false) = the dry landing as before. View only.
    /// </summary>
    public sealed partial class OzWaterLanding
    {
        /// <summary>The switch (false = 1.5 before the review).</summary>
        public static bool EdgeOn = true;
        /// <summary>Points of the footprint that must be over open water.</summary>
        public static int EdgeWetMin = 3;
        /// <summary>Counted proof: pieces taken into the water by their footprint.</summary>
        public static int EdgeEntries { get; private set; }

        static readonly Vector2[] edgePts = { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1f), new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f) };

        /// <summary>Enter, when its middle and its pivot are dry: enough of its footprint over open water? over = the middle of the wet points,
        /// wy = their water.</summary>
        static bool EdgeWater(Bounds b, out Vector3 over, out float wy)
        {
            over = b.center; wy = 0f;
            if (!EdgeOn || !ZGame.I) return false;
            Vector3 sum = Vector3.zero; float wsum = 0f; int wet = 0;
            for (int k = 0; k < edgePts.Length; k++)
            {
                var q = new Vector3(b.center.x + edgePts[k].x * b.extents.x * 0.85f, b.min.y, b.center.z + edgePts[k].y * b.extents.z * 0.85f);
                if (!OpenWaterAt(q, out float w)) continue;
                if (OzLava.SurfaceAt(q, out _)) return false;                                       // lava: its own melt rule
                sum += q; wsum += w; wet++;
            }
            if (wet < EdgeWetMin) return false;
            over = sum / wet; wy = wsum / wet; EdgeEntries++;
            return true;
        }

        /// <summary>The box the rule takes the piece in with: its own size, its middle over the water (the splash, the rings and the room it
        /// needs are measured from there).</summary>
        static Bounds EdgeBox(Bounds b, Vector3 over) => new Bounds(new Vector3(over.x, b.center.y, over.z), b.size);
    }
}
