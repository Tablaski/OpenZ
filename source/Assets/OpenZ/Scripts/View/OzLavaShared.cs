// OpenZ (GPL-3.0) - version 1.3, level 7: what the four lava kits share (LV_CONTRACT.md): the shader names, the colour ramp's numbers, the
// two switches. View only: nothing here reads or writes sim state, and nothing runs until OzLava boots on a map with lava.
using UnityEngine;

namespace OpenZ
{
    /// <summary>Every global the lava kits share, in one place. A kit writes only the globals the contract gives it.</summary>
    public static class OzLavaIds
    {
        public const int FieldSlots = 16, RampStops = 5;
        public static readonly int Ramp = Shader.PropertyToID("_OZLavaRamp"), Field = Shader.PropertyToID("_OZLavaField"),
            Wave = Shader.PropertyToID("_OZLavaWave"), Surge = Shader.PropertyToID("_OZLavaSurge"), Flash = Shader.PropertyToID("_OZLavaFlash"),
            Mood = Shader.PropertyToID("_OZLavaMood"), Wake = Shader.PropertyToID("_OZLavaWake"), WakeRect = Shader.PropertyToID("_OZLavaWakeRect"),
            Clock = Shader.PropertyToID("_OZLavaTime"), Lite = Shader.PropertyToID("_OZLavaLite");

        static readonly Vector4[] neutral = new Vector4[FieldSlots], zeroField = new Vector4[FieldSlots], zeroRamp = new Vector4[RampStops];

        /// <summary>The look of heat 1.0 with nothing moving (OzLava.Lv1Boot calls it first, so LV1 alone draws right). The arrays are set at
        /// their full size here, the first time, as Unity fixes an array's length at its first set.</summary>
        public static void Neutral()
        {
            for (int i = 0; i < FieldSlots; i++) neutral[i] = new Vector4(1f, 0f, 0f, 0f);
            Shader.SetGlobalVectorArray(Field, neutral);
            Shader.SetGlobalVector(Wave, Vector4.zero); Shader.SetGlobalVector(Surge, Vector4.zero); Shader.SetGlobalVector(Flash, Vector4.zero);
            Shader.SetGlobalFloat(Mood, 1f); Shader.SetGlobalFloat(Clock, 0f); Shader.SetGlobalFloat(Lite, 0f);
            Shader.SetGlobalTexture(Wake, Texture2D.blackTexture); Shader.SetGlobalVector(WakeRect, Vector4.zero);
        }

        /// <summary>The level ends: nothing of the lava stays in force (OzLava.Lv1Down, after the other kits).</summary>
        public static void ResetGlobals()
        {
            Neutral(); Shader.SetGlobalVectorArray(Field, zeroField); Shader.SetGlobalVectorArray(Ramp, zeroRamp); Shader.SetGlobalFloat(Mood, 0f);
        }
    }

    /// <summary>The numbers of the one colour ramp (Shaders/OzLavaRamp.hlsl). sRGB bytes from the owner's picture, re-measured (review_owner.md
    /// F7): crust dark, warm crust, molten, core; dull red is the midpoint the cooling droplets and the legs need. The gains are starting
    /// values: they are set in Play against captured frames, because the tone mapper moves orange (plan section 10).</summary>
    public static class OzLavaRampTable
    {
        public static readonly Color32[] Stops =
        {
            new Color32(60, 21, 18, 255), new Color32(102, 58, 51, 255), new Color32(130, 34, 16, 255), new Color32(202, 51, 17, 255), new Color32(253, 168, 41, 255),
        };
        public static float[] Gains = { 1f, 1f, 1.1f, 1.8f, 3.4f };

        static readonly Vector4[] push = new Vector4[OzLavaIds.RampStops];

        /// <summary>Linear rgb of a stop, no gain.</summary>
        static Color Linear(int i) { Color c = Stops[i]; return c.linear; }

        /// <summary>Puts the ramp in force (OzLava.Lv1Boot; again from a tuning script after changing Gains).</summary>
        public static void Push()
        {
            for (int i = 0; i < push.Length; i++) { var c = Linear(i); push[i] = new Vector4(c.r, c.g, c.b, Gains[i]); }
            Shader.SetGlobalVectorArray(OzLavaIds.Ramp, push);
        }

        /// <summary>The ramp in C#, the same sum as OzLavaRamp(T): linear, with the HDR gain. For a light's colour or a _EmissionMapColor tint.</summary>
        public static Color Eval(float T)
        {
            float s = Mathf.Clamp01(T) * 4f; int i = Mathf.Min((int)s, 3); float f = s - i;
            Color c = Color.Lerp(Linear(i) * Gains[i], Linear(i + 1) * Gains[i + 1], f) * (1f + 1.6f * Mathf.Max(T - 1f, 0f));
            c.a = 1f; return c;
        }

        /// <summary>The ramp as a particle colour-over-lifetime: from temperature <paramref name="from"/> at birth down to <paramref name="to"/>,
        /// no gain and 0..1 (a particle's HDR is its channel's intensity, OpenZFxPlayer.CreateChannel). Unity gradients hold 8 colour keys.</summary>
        public static Gradient Cooling(float from = 1f, float to = 0f, float alphaEnd = 0f)
        {
            var ck = new GradientColorKey[6];
            for (int k = 0; k < ck.Length; k++)
            {
                float u = k / (ck.Length - 1f), T = Mathf.Lerp(from, to, u), s = Mathf.Clamp01(T) * 4f; int i = Mathf.Min((int)s, 3);
                ck[k] = new GradientColorKey(Color.Lerp(Linear(i), Linear(i + 1), s - i), u);
            }
            var g = new Gradient { mode = GradientMode.Blend };
            g.SetKeys(ck, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(alphaEnd, 1f) });
            return g;
        }
    }

    /// <summary>The switches every lava kit reads.</summary>
    public static class OzLavaFx
    {
        /// <summary>Dev only, never set by the game or saved: every Lv*Boot returns at once, so the sim-hash run can compare look on and off.</summary>
        public static bool Off;
        /// <summary>Dev only: heat held at 1.0, the picture's moment (gate 1). LV2 obeys it.</summary>
        public static bool HoldHeat;
        /// <summary>The reduced versions: only while the existing LIGHTEST FOR SLOW PCS helper shows ON. Never the default. Read it at boot and at
        /// most once a second.</summary>
        public static bool Lite => OzLightRows.Lightest;
        /// <summary>CALM SCREEN: no flashes, no bloom kick, half the heat swing.</summary>
        public static bool Calm => OzMotion.Reduced;
    }
}
