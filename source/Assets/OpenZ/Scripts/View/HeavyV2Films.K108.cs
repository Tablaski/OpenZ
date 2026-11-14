#if UNITY_EDITOR
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The recoil test (menus "12" / "13"), a shot window with two shots close together. HeavyRecoil's springs add each shot as an impulse
    /// (View/HeavyRecoil.cs), so a second barrel fired while the first one's rock is still rising adds to it: the heavy v1's two barrels
    /// 3 frames (0.1 s) apart peak at 1.53 x one shot's hull rock and 1.38 x its head rock (the springs' own sums), more than the rule's
    /// fixed "up to 1.4 x" (hull) and "1.36 x" (head), which were made for a pair about 0.36 s apart (no gain: the first rock is past its peak
    /// by then). The drawn motion is the design's; the bands' top now follows the window's own closest pair: 1.15 x the springs' summed peak
    /// for that gap (never below the rule's old top). One shot, or shots 5 frames or more apart: the rule as before.
    /// </summary>
    public partial class HeavyV2Films
    {
        /// <summary>The peak of two equal kicks into one damped spring (frequency `hz`, damping `damp`), the window's closest two shots apart,
        /// over one kick's peak (1 = one shot, or no gain).</summary>
        static float PairPeakK108(RecWin c, float hz, float damp)
        {
            if (c == null || c.shots.Count < 2) return 1f;
            int gap = int.MaxValue;
            for (int i = 1; i < c.shots.Count; i++) gap = Mathf.Min(gap, Mathf.Abs(c.shots[i] - c.shots[i - 1]));
            if (gap <= 0 || gap == int.MaxValue) return 1f;
            float dt = gap / (float)Fps;
            float w = 2f * Mathf.PI * Mathf.Max(0.1f, hz), z = Mathf.Clamp(damp, 0.01f, 0.99f), a = z * w, wd = w * Mathf.Sqrt(1f - z * z);
            float one = 0f, two = 0f;
            for (int i = 1; i <= 2000; i++)
            {   // 1 ms steps over 2 s: g(t) = e^(-a t) sin(wd t), the impulse response
                float t = i * 0.001f;
                float g = Mathf.Exp(-a * t) * Mathf.Sin(wd * t), g2 = t > dt ? Mathf.Exp(-a * (t - dt)) * Mathf.Sin(wd * (t - dt)) : 0f;
                one = Mathf.Max(one, g); two = Mathf.Max(two, g + g2);
            }
            return one > 1e-6f ? Mathf.Max(1f, two / one) : 1f;
        }
    }
}
#endif
