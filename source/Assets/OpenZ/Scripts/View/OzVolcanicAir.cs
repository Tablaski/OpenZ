// OpenZ (GPL-3.0) - version 1.3, item 12 part B6: the air of level 7 (Atmosphere.Preset.Volcanic): a warm ash haze, ash motes falling and
// embers rising round the camera. View only: shader globals and two small particle systems, nothing of the rules.
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// No low fog: the lava must stay readable, so the haze stays thin at the game camera.
    /// Statics, so Play can tune them without a rebuild; the numbers are starting values, set side by side with the owner's picture in Unity.</summary>
    public static class OzVolcanicAir
    {
        public static Color HazeDay = new Color(0.92f, 0.78f, 0.64f), HazeGolden = new Color(1.0f, 0.70f, 0.48f), HazeNight = new Color(0.70f, 0.40f, 0.26f);
        public static float DensityDay = 0.0011f, DensityGolden = 0.0014f, DensityNight = 0.0009f;
        public static float Falloff = 26f, NoiseScale = 0.03f, NoiseAmount = 0.75f;
        public static float SunScatter = 1.1f, SunScatterGolden = 1.8f, SunG = 0.4f, SunGGolden = 0.62f;
        public static Vector3 Wind = new Vector3(1.6f, 0f, 0.8f);
        /// <summary>The lava lights the haze at night (Atmosphere's lamp share).</summary>
        public static float LampShare = 0.55f;
        // ---- particles round the camera focus (m, per second, live at most). Embers read with bloom: their material is HDR.
        public static float AshRate = 26f, EmberRate = 9f, BoxSide = 120f;
        public static int AshMax = 240, EmberMax = 80;
        public static Color EmberHdr = new Color(3.4f, 1.5f, 0.42f, 1f);

        static readonly int IdColor = Shader.PropertyToID("_FogColor"), IdDensity = Shader.PropertyToID("_FogDensity"),
            IdFalloff = Shader.PropertyToID("_FogHeightFalloff"), IdNoiseScale = Shader.PropertyToID("_FogNoiseScale"),
            IdNoiseAmount = Shader.PropertyToID("_FogNoiseAmount"), IdWind = Shader.PropertyToID("_FogWind"),
            IdSun = Shader.PropertyToID("_FogSunScatter"), IdG = Shader.PropertyToID("_FogG"), IdLow = Shader.PropertyToID("_LowFogDensity"),
            IdLowH = Shader.PropertyToID("_LowFogHeight"), IdLowW = Shader.PropertyToID("_LowFogWaterBoost"),
            IdLowMode = Shader.PropertyToID("_LowFogMode"), IdShimmer = Shader.PropertyToID("_ShimmerAmount"), IdShift = Shader.PropertyToID("_OZFogShift");

        static ParticleSystem ash, ember;
        static Material emberMat;

        /// <summary>One frame of the air (Atmosphere.Update calls it in the place of its own branch). Returns how much the lights tint the fog.</summary>
        public static float Apply(float t, float night, float golden, float blue)
        {
            Color haze = Color.Lerp(Color.Lerp(HazeDay, HazeGolden, Mathf.Max(golden, blue * 0.6f)), HazeNight, night);
            Shader.SetGlobalColor(IdColor, haze);
            Shader.SetGlobalFloat(IdDensity, Mathf.Lerp(Mathf.Lerp(DensityDay, DensityGolden, golden), DensityNight, night));
            Shader.SetGlobalFloat(IdFalloff, Falloff);
            Shader.SetGlobalFloat(IdNoiseScale, NoiseScale); Shader.SetGlobalFloat(IdNoiseAmount, NoiseAmount);
            Shader.SetGlobalVector(IdWind, new Vector4(Wind.x, 0f, Wind.z, 0f));
            Shader.SetGlobalFloat(IdSun, Mathf.Lerp(SunScatter, SunScatterGolden, golden)); Shader.SetGlobalFloat(IdG, Mathf.Lerp(SunG, SunGGolden, golden));
            Shader.SetGlobalFloat(IdLow, 0f); Shader.SetGlobalFloat(IdLowH, 2.5f); Shader.SetGlobalFloat(IdLowW, 0f); Shader.SetGlobalFloat(IdLowMode, 0f);
            Shader.SetGlobalFloat(IdShimmer, 0f);
            Shader.SetGlobalVector(IdShift, Vector4.zero);
            Follow(night);
            return night * LampShare;
        }

        static ParticleSystem Make(Transform parent, string name, Material mat, int max, float rate, Vector2 life, Vector2 size, Color col)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave }; go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y); main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y); main.startColor = col; main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                                              new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        /// <summary>Atmosphere.OnEnable in Play: the two systems (the same soft dot as the desert's sand).</summary>
        public static void BuildParticles(Transform parent, Material soft)
        {
            ash = Make(parent, "AshMotes", soft, AshMax, AshRate, new Vector2(7f, 12f), new Vector2(0.05f, 0.12f), new Color(0.30f, 0.27f, 0.25f, 0.34f));
            var sa = ash.shape; sa.scale = new Vector3(BoxSide, 2f, BoxSide);
            var va = ash.velocityOverLifetime; va.enabled = true; va.space = ParticleSystemSimulationSpace.World;
            va.x = new ParticleSystem.MinMaxCurve(Wind.x * 0.5f, Wind.x); va.y = new ParticleSystem.MinMaxCurve(-1.3f, -0.7f); va.z = new ParticleSystem.MinMaxCurve(Wind.z * 0.5f, Wind.z);
            if (!emberMat) { emberMat = new Material(soft) { hideFlags = HideFlags.DontSave }; emberMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One); }
            emberMat.SetColor("_BaseColor", EmberHdr);
            ember = Make(parent, "Embers", emberMat, EmberMax, EmberRate, new Vector2(3.5f, 6.5f), new Vector2(0.05f, 0.11f), Color.white);
            var se = ember.shape; se.scale = new Vector3(BoxSide * 0.7f, 1f, BoxSide * 0.7f);
            var ve = ember.velocityOverLifetime; ve.enabled = true; ve.space = ParticleSystemSimulationSpace.World;
            ve.x = new ParticleSystem.MinMaxCurve(Wind.x * 0.4f, Wind.x * 1.2f); ve.y = new ParticleSystem.MinMaxCurve(1.1f, 2.6f); ve.z = new ParticleSystem.MinMaxCurve(Wind.z * 0.4f, Wind.z * 1.2f);
        }

        static void Follow(float night)
        {
            if (!Application.isPlaying || !ash || !ember) return;
            var cam = Camera.main; var rts = cam ? cam.GetComponent<RtsCamera>() : null; if (!rts) return;
            ash.transform.position = rts.focus + Vector3.up * 26f;
            ember.transform.position = rts.focus + Vector3.up * 0.4f;
            var em = ember.emission; em.rateOverTime = EmberRate * (1f + 0.6f * night);                  // the same embers read stronger in the dark
        }
    }
}
