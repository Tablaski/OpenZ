#if UNITY_EDITOR
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// k93, item 152 (the hull's rock on each shot drawn bigger along the hull, across it as before: View/HeavyRecoil.Show.cs), in the v2 tests:
    ///  * the recoil test (menus "12" / "13"): each shot window's hull rock and slide are judged against the design for THAT shot's direction in
    ///    the hull's frame (HeavyRecoil.DesignBodyToward: the along size for a shot ahead, the old across size for a shot to the side, between for
    ///    the rest), the heavy included (its two barrels: up to 1.4 x, as its rule had); the head's rule is unchanged;
    ///  * the track tests: the hull's tilt over its planted belts may reach the bigger rock (p95 at most 2.5 deg or 1.15 x the design's rock along
    ///    the hull, the larger); the belts' own rules are unchanged.
    /// </summary>
    public partial class HeavyV2Films
    {
        /// <summary>RecVerdict, after RecRuleFor: the hull bands for this window's shot direction (RecoilShow off: the rule as it was).</summary>
        void RecRuleK93(RecWatch w, RecWin c, ref RecRule R)
        {
            if (!RecoilShow.Enabled || c == null) return;
            var v = w.v ? w.v : View(w.o); if (!v) return;
            string type = w.o != null ? w.o.Type : S.Type;
            var local = RecLocalK102(c, v);   // k102: the direction in the hull as it stood at the shot (View/HeavyV2Films.K102.cs)
            HeavyRecoil.DesignBodyToward(type, local, out float rock, out float slide);
            float hi = Mathf.Max(type == OzHeavyTank.Type ? 1.4f : 1.25f, 1.15f * PairPeakK108(c, HeavyRecoil.BodyHz, HeavyRecoil.BodyDamp));   // the heavy's second barrel adds to the first (closer than the rock's rise: the springs' sum, View/HeavyV2Films.K108.cs)
            float headPair = PairPeakK108(c, HeavyRecoil.HeadHz, HeavyRecoil.HeadDamp);
            if (headPair > 1.0001f) { R.rockHi = Mathf.Max(R.rockHi, 1.15f * headPair * HeavyRecoil.DesignHeadRock(type)); R.head += $"; two shots close together: rock up to {R.rockHi:0.00} deg (1.15 x {headPair:0.00} x {HeavyRecoil.DesignHeadRock(type):0.0}, the springs' sum)"; }
            R.hullLo = 0.85f * rock; R.hullHi = hi * rock; R.stepHull = 0.85f * rock;
            if (slide > 1e-4f) { R.slideLo = 0.75f * slide; R.slideHi = 1.5f * slide; R.stepSlide = 0.92f * slide; }
            local.y = 0f; local.Normalize();
            R.hull = $"k93: its design for this shot ({Mathf.Abs(local.z) * 100f:0}% along the hull): {R.hullLo:0.00}-{R.hullHi:0.00} deg (0.85-{hi:0.00} x {rock:0.00}), opposite under 45 %, settled under 0.2 deg, slide {R.slideLo:0.000}-{R.slideHi:0.000} m (0.75-1.5 x {slide:0.000})";
            R.steps = $"no jump: <= {R.stepHull:0.00} deg, {R.stepSlide:0.000} m, {R.stepHead:0.000} m (0.85 / 0.92 / 1.15 x its design)";
        }

        /// <summary>The track tests' limit for the hull's tilt over its belts (p95, deg).</summary>
        float K93HullCap() => Mathf.Max(2.5f, 1.15f * HeavyRecoil.DesignBodyRock(S.Type));
    }
}
#endif
