// OpenZ (GPL-3.0) - version 1.2, Options GRAPHICS row CHROMATIC ABERRATION · LENS EDGES: a camera lens look, default OFF.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// Version 1.2: the CHROMATIC ABERRATION · LENS EDGES switch (Options, GRAPHICS tab of the main menu and of the pause menu, under
    /// MOTION BLUR; PlayerPrefs <see cref="Pref"/>, default OFF, saved and applied at once). On = URP's ChromaticAberration override at
    /// <see cref="Base"/> on a runtime global Volume of its own (the scene profiles and Settings/DefaultVolumeProfile.asset are never
    /// written): a soft red / blue colour fringe towards the screen edges, like a camera lens.
    /// <para>Why <see cref="DefaultBase"/> = 0.12: the effect is radial. The uber post pass moves the green sample by a third and the blue sample by two
    /// thirds of c * |c|^2 * intensity * 0.05 (c = 2 uv - 1, the screen centre is c = 0; red stays), so the colour split grows with the cube
    /// of the distance from the centre. At 0.12 on a 1920 x 1080 picture, red to blue: at most 0.65 px over the middle third of the screen
    /// (the centre stays sharp), 1 px half way to a side, 7.7 px at the left and right edges, 4.3 px at the top and bottom edges, 17.6 px
    /// in the very corners: a subtle lens fringe at the edges, about half of the first setting (0.25: 16 / 9 / 37 px), which read too
    /// strong when on. The pass takes only three samples, so a larger split turns an edge into three coloured copies: from about 0.3 up a
    /// building near a corner reads doubled rather than fringed. The shift is a share of the picture, so every resolution looks the same.</para>
    /// <list type="bullet">
    /// <item>Off: the volume is disabled, so every frame is drawn as before.</item>
    /// <item>Every scene: one object kept across scene loads, made before the first scene loads, so every level, the level-start
    /// flyover, the endings and the Unit editor follow the switch from their first frame (the main menu's camera draws no post-processing).</item>
    /// <item>The player's view only: per camera (<see cref="RenderPipelineManager.beginCameraRendering"/>, before the camera's volume
    /// update) the volume weighs 1 for a game camera that draws to the screen or is the main camera (also while that one renders a
    /// picture into a texture), 0 for every other camera (the transmission windows' own cameras, the parked intro's picture cameras,
    /// previews, the scene view). The commander's comms window renders only its own layer and never sees this volume.</item>
    /// <item>Combined with every effect: the volume sits above every volume of the game (<see cref="Priority"/>) and, for each camera, sets
    /// max(<see cref="Base"/>, the value the other volumes give that camera: the default profiles, then each global volume of the
    /// camera's volume layers with a ChromaticAberration override, by priority and weight, as URP blends them). The other volumes are read
    /// every frame; the list of those that carry the override is refreshed every <see cref="Rescan"/> s and at every scene load. So an
    /// effect that wants more chromatic aberration takes over while it runs, and its end falls back to <see cref="Base"/>, never to 0.
    /// No other volume of the game sets chromatic aberration today: the level-start and ending vignettes, the nuke flash, the night,
    /// weather and briefing grades drive their own overrides (Vignette, ColorAdjustments, Bloom, WhiteBalance, MotionBlur), which URP
    /// blends beside this one.</item>
    /// </list>
    /// The exe keeps the effect's shader variant because Settings/DefaultVolumeProfile.asset holds a ChromaticAberration component
    /// (URP strips the post-processing variants that no VolumeProfile asset of the project uses).
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class OzLensEdges : MonoBehaviour
    {
        public const string Label = "CHROMATIC ABERRATION · LENS EDGES", Pref = "oz_chroma";
        public const string Tip = "CHROMATIC ABERRATION|A camera lens look. On: a subtle red and blue colour fringe towards the edges of the screen, like a real camera lens; the centre of the screen stays sharp. Off (default): a clean picture. Applies at once and costs almost nothing.|";
        /// <summary>The option's intensity (URP 0..1), see the class comment.</summary>
        public const float DefaultBase = 0.12f;                                                     // 1.2 item 50: more subtle (was 0.25: too strong when on)
        /// <summary>Above every volume of the game (the level-start vignette 100, the ending vignette 90, the end screens 60, the pause menu
        /// 55, the comms set 50, the nuke flash 40, bloom and briefing grade 30, motion blur 25, night 20) and below the 1000 of the bloom
        /// check's own test volume.</summary>
        public const float Priority = 900f;
        /// <summary>Seconds between two looks for the volumes that carry a ChromaticAberration override (also at every scene load).</summary>
        public const float Rescan = 0.1f;

        static int pref = -1;
        static float baseNow = DefaultBase;

        /// <summary>The Options switch (oz_chroma, default OFF). Setting it saves it and applies it on the next frame drawn.</summary>
        public static bool On
        {
            get { if (pref < 0) pref = PlayerPrefs.GetInt(Pref, 0) == 1 ? 1 : 0; return pref == 1; }
            set
            {
                pref = value ? 1 : 0; PlayerPrefs.SetInt(Pref, pref); PlayerPrefs.Save();
                Debug.Log($"[OpenZ Options] chromatic aberration (lens edges): {(value ? "ON at " + baseNow.ToString("0.00") : "OFF")} (oz_chroma {pref}, saved, live)");
                if (I) I.Apply();
            }
        }

        /// <summary>The intensity while the switch is on. Session only, never saved: <see cref="DefaultBase"/> at every start; a Play call
        /// (the proof's pictures at 0.08 / 0.12 / 0.16 / 0.25) may set it and it shows on the next frame.</summary>
        public static float Base { get => baseNow; set => baseNow = Mathf.Clamp01(value); }

        /// <summary>Reads the saved switch again (after a check deleted or rewrote the key).</summary>
        public static void Reload() { pref = -1; if (I) I.Apply(); }

        public static OzLensEdges I { get; private set; }
        /// <summary>Checks: the chromatic aberration intensity the last player-view camera was drawn with (-1 = none drawn yet).</summary>
        public static float Seen { get; private set; } = -1f;
        /// <summary>Checks: the frame of <see cref="Seen"/> and its camera.</summary>
        public static int SeenFrame { get; private set; } = -1;
        public static Camera SeenCamera { get; private set; }
        /// <summary>Checks: what the other volumes gave the last player-view camera (before the max with <see cref="Base"/>).</summary>
        public static float Others { get; private set; }
        /// <summary>Checks: how many other volumes carry a ChromaticAberration override (last look).</summary>
        public static int Users => I ? I.users.Count : 0;
        /// <summary>Checks: the switch's own volume (null before the first scene).</summary>
        public static Volume Vol => I ? I.vol : null;

        Volume vol; VolumeProfile prof; ChromaticAberration ca;
        readonly List<Volume> users = new List<Volume>(4);
        float baseline, nextScan;
        static readonly System.Comparison<Volume> ByPriority = (a, b) => a.priority.CompareTo(b.priority);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { pref = -1; baseNow = DefaultBase; I = null; Seen = -1f; SeenFrame = -1; SeenCamera = null; Others = 0f; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            Ensure();
            if (I) I.Scan();
        }

        static void Ensure()
        {
            if (I || !Application.isPlaying) return;
            var go = new GameObject("OZ_LensEdges");
            DontDestroyOnLoad(go);
            go.AddComponent<OzLensEdges>();
        }

        void Awake()
        {
            if (I && I != this) { Destroy(gameObject); return; }
            I = this;
            gameObject.layer = 0;                                                                       // Default: the game cameras' volume layer
            prof = ScriptableObject.CreateInstance<VolumeProfile>(); prof.name = "OZ_LensEdges (runtime)"; prof.hideFlags = HideFlags.DontSave;
            ca = prof.Add<ChromaticAberration>(false);
            ca.intensity.Override(baseNow);
            vol = gameObject.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = Priority; vol.weight = 0f; vol.sharedProfile = prof;
            Apply();
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            if (prof)
            {
                foreach (var c in prof.components) if (c) Destroy(c);
                Destroy(prof);
            }
        }

        /// <summary>The switch onto the volume: off = disabled (nothing of it is drawn).</summary>
        void Apply()
        {
            bool on = On;
            if (vol && vol.enabled != on) vol.enabled = on;
            if (on) Scan();
        }

        void LateUpdate()
        {
            bool on = On;
            if (vol && vol.enabled != on) vol.enabled = on;
            if (on && Time.unscaledTime >= nextScan) Scan();
        }

        /// <summary>The other volumes that carry a ChromaticAberration override (sorted by priority) and the default profiles' value.</summary>
        void Scan()
        {
            nextScan = Time.unscaledTime + Rescan;
            users.Clear();
            foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (!v || v == vol) continue;
                var p = v.HasInstantiatedProfile() ? v.profile : v.sharedProfile;
                if (p && p.Has<ChromaticAberration>()) users.Add(v);
            }
            if (users.Count > 1) users.Sort(ByPriority);
            baseline = DefaultValue();
        }

        /// <summary>The stack's starting value: the global default profile, then the quality level's, then the custom ones (URP's order).</summary>
        static float DefaultValue()
        {
            float v = 0f;
            var vm = VolumeManager.instance;
            Take(vm.globalDefaultProfile, ref v);
            Take(vm.qualityDefaultProfile, ref v);
            var cs = vm.customDefaultProfiles;
            if (cs != null) for (int i = 0; i < cs.Count; i++) Take(cs[i], ref v);
            return v;
        }

        static void Take(VolumeProfile p, ref float v)
        {
            if (p && p.TryGet<ChromaticAberration>(out var c) && c.active && c.intensity.overrideState) v = c.intensity.value;
        }

        /// <summary>What the other volumes give a camera with this volume layer mask (global volumes, by priority and weight, as URP
        /// blends them; the game has no local volumes).</summary>
        float OthersFor(int mask)
        {
            float v = baseline;
            for (int i = 0; i < users.Count; i++)
            {
                var u = users[i];
                if (!u || !u.isActiveAndEnabled || !u.isGlobal || u.weight <= 0f || u.priority > Priority) continue;
                if ((mask & (1 << u.gameObject.layer)) == 0) continue;
                var p = u.HasInstantiatedProfile() ? u.profile : u.sharedProfile;
                if (!p || !p.TryGet<ChromaticAberration>(out var c) || !c.active || !c.intensity.overrideState) continue;
                v += (c.intensity.value - v) * Mathf.Clamp01(u.weight);
            }
            return v;
        }

        /// <summary>A camera the player looks through: a game camera drawing to the screen, or the main camera whatever it draws into.</summary>
        static bool PlayerView(Camera c) => c && c.cameraType == CameraType.Game && (c.targetTexture == null || c.CompareTag("MainCamera"));

        static int MaskOf(Camera c) => c.TryGetComponent<UniversalAdditionalCameraData>(out var d) ? (int)d.volumeLayerMask : 1;

        void BeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (!vol || !vol.enabled) return;
            if (!PlayerView(cam)) { vol.weight = 0f; return; }
            int mask = MaskOf(cam);
            float others = OthersFor(mask);
            float v = Mathf.Max(baseNow, others);
            Others = others;
            ca.intensity.Override(v);
            vol.weight = 1f;
        }

        void EndCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (!PlayerView(cam)) return;
            var st = VolumeManager.instance.stack;
            var c = st != null ? st.GetComponent<ChromaticAberration>() : null;
            Seen = c != null ? c.intensity.value : -1f;
            SeenFrame = Time.frameCount; SeenCamera = cam;
        }

        /// <summary>One line for logs and checks.</summary>
        public static string Describe()
        {
            return $"lens edges {(On ? "ON" : "OFF")}, base {baseNow:0.00}, volume {(Vol ? (Vol.enabled ? "enabled" : "disabled") : "missing")}, {Users} other chromatic aberration volume(s), "
                 + $"drawn {(Seen < 0f ? "-" : Seen.ToString("0.000"))} on {(SeenCamera ? SeenCamera.name : "-")} (frame {SeenFrame})";
        }
    }
}
