// OpenZ (GPL-3.0)
// The flyby sky in the hidden first-person view.
// While OzFpsView is on (and no flyby, cine recording or ForceOn wants the sky: those keep their own way exactly) the sky dome, the sun disc
// and its bloom glow are drawn for the player's camera, loaded on demand when the view goes in (FpsFadeSeconds), faded out and unloaded by the
// flybys' own path when it is left (LateFadeSeconds, then Unload 2 s later: LastUnload logged). The RTS camera: nothing changes (Weight 0, no
// dome, no texture resident, every global as Atmosphere / DayNight set it). In the view:
//  - the sun disc on the DayNight key light's REAL direction (the shadows fall away from it), not the flybys' low drawn sun; its strength
//    SunUpK: full by day, eased out at dusk (DuskOut0 .. DuskOut1 on DayNight's clock: the blue hour) and in at dawn (DawnIn0 .. DawnIn1);
//    at night the key light is the moon (DayNight's night keys): no disc, no glow;
//  - the painted panorama graded to DayNight's sky colours away from sunrise (the flybys' own _Away grading: as painted at dawn, the gameplay
//    sky's colours by day, the night's dark sky at night), so a night level never shows a bright dawn;
//  - a level with no painted sky (no entry in sky_panoramas.json, or its panorama failed to load): a 8 x 4 white map graded by the same gains
//    to the gameplay gradient (haze colour at the horizon, DayNight's sky colour above) with the sun disc and glow (FpsSkyChoice names it).
// The MAYHEM fog of war's full-screen pass draws after the dome (BeforeRenderingPostProcessing + 1) and the dome writes no depth, so the fog's
// wall over ground not explored yet covers the dome's sky low over it as it covered the gradient sky.
using UnityEngine;

namespace OpenZ
{
    public sealed partial class FlybySky
    {
        /// <summary>Fade in when the view goes in (s, after the panorama is resident); the fade out is LateFadeSeconds as after every flyby.</summary>
        public static float FpsFadeSeconds = 0.4f;
        /// <summary>The disc's and the glow's strength in the view by day (the flybys' tuned low sun is 0.63).</summary>
        public static float FpsSunGain = 0.7f;
        /// <summary>DayNight's clock (0 noon, 0.3 golden sunset, 0.42 blue hour, 0.56 - 0.80 night, 0.9 dawn): the sun's disc eases in over
        /// DawnIn0 .. DawnIn1 and out over DuskOut0 .. DuskOut1 (the key light is the moon from the 0.49 key to the 0.86 key).</summary>
        public const float DawnIn0 = 0.885f, DawnIn1 = 0.905f, DuskOut0 = 0.37f, DuskOut1 = 0.41f;

        /// <summary>The sky is on for the first-person view this frame (LateUpdate; false whenever a flyby, a cine recording or ForceOn has it).</summary>
        public bool FpsMode { get; private set; }
        /// <summary>The view's sky is the plain gradient (a level with no painted sky).</summary>
        public bool PlainSky => plainTex && tex;
        /// <summary>Check: frames the view's sky was drawn (Drive in FpsMode).</summary>
        public static int FpsDrawFrames { get; private set; }
        bool plainTex;

        static bool FpsWanted(bool flying, bool cine) => OzFpsView.On && !flying && !cine && !ForceOn;

        /// <summary>The sun's share of the day on DayNight's clock: 1 by day, 0 at night, eased at dawn and dusk.</summary>
        public static float SunUpK(float t)
        {
            t = Mathf.Repeat(t, 1f);
            if (t >= DawnIn0) return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DawnIn0, DawnIn1, t));
            if (t <= DuskOut0) return 1f;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DuskOut0, DuskOut1, t));
        }

        /// <summary>The view's sun: the key light's own direction (toward it), the flybys' default when a level has no sun.</summary>
        static Vector3 FpsSun(Light sun) => sun ? -sun.transform.forward : new Vector3(0.94f, 0.16f, -0.3f).normalized;

        /// <summary>The view's disc and glow strength: FpsSunGain by day, 0 at night and under the horizon.</summary>
        public static float FpsSunK(float t, float elevDeg) => FpsSunGain * SunUpK(t) * Mathf.Clamp01((elevDeg + 0.5f) / 3f);

        /// <summary>EnsureLoaded in FpsMode: a level with no usable painted sky gets the plain map (true: nothing else to load).</summary>
        bool FpsPlain(string lv)
        {
            if (!FpsMode) return false;
            var e = Entry(lv);
            if (e != null && !string.IsNullOrEmpty(e.textureHi) && failedLevel != lv) return false;
            if (tex && plainTex) return true;
            if (tex || req != null) Unload();
            var w = new Texture2D(8, 4, TextureFormat.RGBA32, false) { name = "OzFpsPlainSky", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[32]; for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            w.SetPixels32(px); w.Apply(false, true);
            tex = w; plainTex = true;
            Debug.Log("[OpenZ FlybySky] " + lv + ": first-person view: no painted sky (" + (e == null ? "no entry in " + JsonPath : "its panorama failed to load") + "): the gradient sky with the sun");
            return true;
        }

        /// <summary>What the view shows on a level: its painted sky or the gradient with the sun (README, check).</summary>
        public static string FpsSkyChoice(string level)
        {
            var e = Entry(level);
            if (e == null || string.IsNullOrEmpty(e.textureHi)) return level + ": no sky entry: the gradient sky (DayNight's horizon and sky colours) with the sun disc and glow";
            string own = level == "L02" || level == "L17" ? "its own" : level == "L16" ? "its entry (the desert sunrise painting)" : "the desert sunrise painting (the flybys' rule for level 7)";
            return level + ": " + own + " painted sky " + e.textureHi + ", graded to DayNight's colours away from sunrise, with the sun disc and glow";
        }

        /// <summary>The resident map's size (MB; 0 when none): the panorama (or the plain map) the view's sky holds.</summary>
        public float ResidentMB => tex ? SizeMB(tex) : 0f;
    }
}
