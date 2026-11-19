using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 (the Mammoth and the Bizon, View/OzTrackLinks.cs): the planted running gear (View/UnitView.GroundGear.cs) of a link-track tank. Its belt
    /// is no one mesh but a loop of links, so GearInit takes each side root's footprint from the menu-baked box of its links at rest
    /// (OzTrackLinks.Side.boxLo / boxHi, the side root's space): the ground samples run along the bottom of the real links, both belts stand on
    /// their own ground every frame, and the hull rides on them as on every planted tank (one line in GearInit).
    /// </summary>
    public partial class UnitView
    {
        /// <summary>GearInit: the box (the vehicle's rest space) of a gear node that has no mesh of its own: a link-track side root's links; else none.</summary>
        (Vector3 lo, Vector3 hi) LinkGearBox(GearNode g)
        {
            var l = motion ? motion.Links : null;
            if (g == null || g.spun || !l) return (Vector3.zero, Vector3.zero);
            foreach (var s in l.sides)
            {
                if (s == null || s.root != g.t || !(s.boxHi.x > s.boxLo.x)) continue;
                var m = g.chain * Matrix4x4.TRS(g.restPos, g.restRot, g.scale);
                Vector3 lo = Vector3.one * 1e9f, hi = -Vector3.one * 1e9f;
                var c = (s.boxLo + s.boxHi) * 0.5f; var e = (s.boxHi - s.boxLo) * 0.5f;
                for (int i = 0; i < 8; i++)
                {
                    var p = m.MultiplyPoint3x4(c + Vector3.Scale(e, new Vector3((i & 1) * 2 - 1, ((i >> 1) & 1) * 2 - 1, ((i >> 2) & 1) * 2 - 1)));
                    lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p);
                }
                LinkGearBoxes++;
                return (lo, hi);
            }
            return (Vector3.zero, Vector3.zero);
        }

        /// <summary>Proof: link-track sides given their footprint.</summary>
        public static int LinkGearBoxes;
    }
}
