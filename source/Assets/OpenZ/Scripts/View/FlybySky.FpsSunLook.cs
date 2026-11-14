// OpenZ (GPL-3.0) - version 1.4 kit fps_glow: the sun by day in the hidden first-person view, per level. Unity chain v8 U1 measured the view's
// sun on L02 by day (checks/u1/sky_L02/fps_sky_sun_day.png, the key light 30.5 deg up): the disc 0.99 against 0.95 a quarter screen beside it
// (and 0.97 - 0.96 at 2 - 6 deg from its centre): a white spot in a near-white haze, while L17 passes (0.99 against 0.92). The haze round it
// is the dome graded to DayNight's pale desert sky plus the sun glow: the tight glow (cos^360, sun colour x GlowGain 1.3) saturates to white
// out to about 4 deg and the wide one (GlowWide 0.45 x cos^10) lifts the whole half of the sky by about 0.1. So on L02, in the view only:
// the dome x FpsLook.sky (0.82), the tight glow x glow (0.55) in a warm tint (the flybys' dawn sun is warm), the wide glow x wide (0.25 of its
// share), the disc 1.45 deg (was 1.1) at the same HDR gain (the URP bloom's warm halo stays): a bright disc in a warm halo over a sky a step
// darker. Every other level keeps FpsLook.Plain (exactly fps_view_sky's look; L17 passed). Flybys, cine and the RTS camera: FpsMode is false,
// every line here is skipped.
using UnityEngine;

namespace OpenZ
{
    public sealed partial class FlybySky
    {
        /// <summary>The view's sun look of a level: dome gain, tight glow gain, wide glow share, disc radius (deg), glow tint (linear).</summary>
        public struct FpsLook { public float sky, glow, wide, discDeg; public Color tint; }
        /// <summary>Every level but those below: fps_view_sky's look unchanged.</summary>
        public static FpsLook FpsPlainLook => new FpsLook { sky = 1f, glow = 1f, wide = 1f, discDeg = DiscRadiusDeg, tint = Color.white };
        /// <summary>L02 (the desert by day: a pale sky).</summary>
        public static FpsLook FpsL02Look = new FpsLook { sky = 0.82f, glow = 0.55f, wide = 0.25f, discDeg = 1.45f, tint = new Color(1f, 0.84f, 0.62f) };

        /// <summary>The look the view uses on a level (L07 and L16 show the desert painting too, but their days were not measured: plain).</summary>
        public static FpsLook FpsLookOf(string level) => level == "L02" ? FpsL02Look : FpsPlainLook;

        /// <summary>Drive in FpsMode: the glow colour (x the tight gain and the tint) and the wide glow's share.</summary>
        static Vector4 FpsGlowVector(Color sunLin, string level)
        {
            var k = FpsLookOf(level); float g = GlowGain * k.glow;
            return new Vector4(sunLin.r * g * k.tint.r, sunLin.g * g * k.tint.g, sunLin.b * g * k.tint.b, GlowWide * k.wide);
        }

        /// <summary>Drive in FpsMode: the glow and disc shape (the disc radius of the level's look).</summary>
        static Vector4 FpsShapeVector(string level)
        {
            float d = FpsLookOf(level).discDeg;
            return new Vector4(GlowTightExp, GlowWideExp, Mathf.Cos(d * Mathf.Deg2Rad), Mathf.Cos((d + DiscEdgeDeg) * Mathf.Deg2Rad));
        }

        /// <summary>Drive in FpsMode: the dome's grading gains x the level's sky gain (the shader: pano x lerp(1, gain, _Away); by day _Away is 1).</summary>
        static void FpsSkyGain(Material m, string level)
        {
            float s = FpsLookOf(level).sky; if (Mathf.Abs(s - 1f) < 1e-4f || !m) return;
            var h = m.GetVector(IdGainH); var z = m.GetVector(IdGainZ);
            m.SetVector(IdGainH, new Vector4(h.x * s, h.y * s, h.z * s, h.w)); m.SetVector(IdGainZ, new Vector4(z.x * s, z.y * s, z.z * s, z.w));
            FpsSkyGained++;
        }
        /// <summary>Check: frames the view's dome gain was applied; the disc radius the view draws now (deg).</summary>
        public static int FpsSkyGained { get; private set; }
        public static float FpsDiscDegNow => FpsLookOf(ZGame.I ? ZGame.I.level : null).discDeg;
    }
}
