// OpenZ (GPL-3.0)
// A slight vignette while the camera is in the first-person view: soft dark edges (URP Vignette 0.26, smoothness 0.6, black) on a runtime global
// volume of its own (like OzStartVignette / OzEndingVignette; the OpenZ_PostFX asset is never touched). Its weight follows the camera glide of
// OzFpsFeel (0 = the RTS camera's pose, 1 = the eye, smoothstep), so it eases in as the camera swoops down and out as it rises; the volume is
// switched off whenever the weight is 0, so the RTS view is exactly as before. Off while a 1.4 check of the view runs (OzFpsFeel.Legacy).
// The two glide sounds (their events in openz_sound_events.json) are named here.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ
{
    /// <summary>The camera glide's two sounds: a deep soft air rush falling in, the same rush rising out.</summary>
    public static class OzFpsGlideSound
    {
        public const string In = "fps_glide_in", Out = "fps_glide_out";
    }

    [DefaultExecutionOrder(31960)]                                                                       // after OzFpsFeel (31950) moved the glide this frame
    public sealed class OzFpsVignette : MonoBehaviour
    {
        /// <summary>Off = no vignette (a before / after look); on in the game.</summary>
        public static bool On = true;
        public const float Intensity = 0.26f, Smoothness = 0.6f, Priority = 80f;                       // under the level-start (100) and ending (90) vignettes
        /// <summary>The live weight (0 = none .. 1 = at the eye) and whether its volume is on (the Play check).</summary>
        public static float Weight { get; private set; }
        public static bool VolumeOn => inst && inst.vol && inst.vol.enabled;

        static OzFpsVignette inst;
        Volume vol; VolumeProfile profile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { inst = null; On = true; Weight = 0f; }

        /// <summary>OzFpsFeel, at each glide's start: the vignette's volume exists (one per scene).</summary>
        public static void Ensure()
        {
            if (inst || !Application.isPlaying) return;
            new GameObject("OZ_FpsVignette").AddComponent<OzFpsVignette>();
        }

        void Awake()
        {
            inst = this;
            profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "OZ_FpsVignette (runtime)";
            var v = profile.Add<Vignette>(true);
            v.intensity.Override(Intensity); v.smoothness.Override(Smoothness); v.color.Override(Color.black); v.rounded.Override(false);
            v.center.Override(new Vector2(0.5f, 0.5f));
            vol = gameObject.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = Priority; vol.weight = 0f; vol.sharedProfile = profile;
            vol.enabled = false;
        }

        void LateUpdate()
        {
            float g = On && !OzFpsFeel.Legacy && (OzFpsView.On || OzFpsFeel.Gliding) ? OzFpsFeel.GlideS : 0f;
            float w = g * g * (3f - 2f * g);
            Weight = w;
            bool show = w > 0.001f;
            vol.weight = show ? w : 0f;
            if (vol.enabled != show) vol.enabled = show;
        }

        void OnDestroy()
        {
            if (profile) Destroy(profile);
            if (inst == this) { inst = null; Weight = 0f; }
        }
    }
}
