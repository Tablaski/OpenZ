// OpenZ (GPL-3.0) - 1.3, open_fixes_2nov wreck_toss_landing update 1: a landed turret rocks only as far as its length allows, so it never hops out of the ground.
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The heavy landing (item 172) rocks a landed turret by up to 9 degrees about its pivot (5.6 at the rock's first peak, 4 already on the
    /// first frame at 30 fps) and keeps its real lowest vertex on the ground every frame. A turret lying on its side touches the ground with a
    /// point far from its pivot (the barrel's end, a long hull side: 2-4 m along the flight), so each degree of rock lifted the pivot by
    /// lever x sin(rock): 0.2-0.4 m in one frame, 0.3-0.4 m up in all (the check's T2 after-landing steps 0.18-0.41 m, rises 0.30-0.38 m on
    /// the medium tank, heavy, howitzer and missile cannon turrets). <see cref="TurretRockDeg"/> keeps the rock but caps it by the turret's
    /// own lever (the farthest of its low points from the pivot along the flight): the pivot moves at most <see cref="RockLift"/> m with the
    /// rock (asin(RockLift / lever) at the rock's peak). A short turret rocks as before (9 degrees up to a 0.5 m lever); a 3 m one about
    /// 1.8 degrees. The bounce, the skid and the ground fit are unchanged; the fort cannon's landing is unchanged. View only.
    /// Switch: <see cref="RockByLever"/> (off = 9 degrees for every turret, as before).
    /// </summary>
    public partial class OzHeavyLanding
    {
        public static bool RockByLever = true;
        /// <summary>The most the rock may move the pivot up or down (m), and the band over the lowest point whose points can touch while it rocks (m).</summary>
        public static float RockLift = 0.05f, RockBand = 0.5f;
        /// <summary>The rock's first peak as a share of its amplitude: max of exp(-t / 0.16) x sin(2 pi t / 0.34).</summary>
        const float RockPeak = 0.622f;
        /// <summary>Play check: turret rocks capped by their lever, the last lever (m) and the rock it got (deg).</summary>
        public static int RocksCapped; public static float LastRockLever, LastRockDeg = -1f;

        /// <summary>Begin (a turret, after the fit in its landing pose): the rock it may take, at most `deg`.</summary>
        float TurretRockDeg(float deg)
        {
            if (!RockByLever || !fits || deg <= 0f) return deg;
            int n = Fill(out float minY, out _, out _, out _, out _);
            if (n == 0) return deg;
            var piv = transform.position; float lever = 0f;
            for (int i = 0; i < n; i++)
            {
                var q = wp[i]; if (q.y > minY + RockBand) continue;
                float d = Mathf.Abs((q.x - piv.x) * skidDir.x + (q.z - piv.z) * skidDir.z);
                if (d > lever) lever = d;
            }
            LastRockLever = lever;
            if (lever <= 1e-3f) { LastRockDeg = deg; return deg; }
            float most = Mathf.Asin(Mathf.Clamp01(RockLift / lever)) * Mathf.Rad2Deg / RockPeak;
            if (most < deg) { RocksCapped++; deg = most; }
            LastRockDeg = deg;
            return deg;
        }
    }
}
