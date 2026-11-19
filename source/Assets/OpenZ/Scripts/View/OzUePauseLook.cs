// OpenZ (GPL-3.0)
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// (SPACE or the PLAY / PAUSE button, View/OzUeControls.cs SetPlaying) gets the level's pause look: the TACTICAL PAUSE's soft dark vignette
    /// with a little less colour (View/OzTacticalPause.cs: the same numbers, VignetteIntensity 0.42, VignetteSaturation -30, VignetteExposure
    /// -0.18, the same URP post volume kind (UI/OzUi.cs OzPost) at the same priority 54, eased in and out over the same EaseSec 0.3 s with
    /// the same SmoothStep). View only: the pause itself (the game's paused flag, the time scale 0 at once, what keeps working) is exactly as
    /// before. Before the kit the editor's pause had no look at all: the tactical pause stays idle in the playground (it has its own PLAY /
    /// PAUSE), the pause menu is not there (the editor owns Esc), and the playground's floor (View/OzUeFloor.cs Grade) takes the camera's whole
    /// post chain back out of its colours every frame, so a pause volume alone would have left the floor (most of the screen) undimmed: while
    /// this look is on, the floor takes back only the grade WITHOUT it (<see cref="BaseGrade"/>).
    /// One per Unit editor (added by OzUnitEditor.Awake on its object); gone with the editor (the volume with it). Never in a level.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OzUePauseLook : MonoBehaviour
    {
        public const string VolumeName = "OZ_UePause";
        const int VolumePriority = 54;                                                                   // = OzTacticalPause's (under the pause menu's 55)

        public static OzUePauseLook I { get; private set; }
        /// <summary>Checks: the pauses and resumes the look followed since the editor opened.</summary>
        public static int PauseIns, PauseOuts;

        float k; bool pausedShown; OzPost post;

        /// <summary>0 = running, 1 = paused (the ease's own value, before the SmoothStep).</summary>
        public static float Amount => I ? I.k : 0f;
        /// <summary>The volume's weight now (0 = no look).</summary>
        public static float Weight => I && I.post != null ? I.post.Weight : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; PauseIns = PauseOuts = 0; }

        void Awake() { I = this; PauseIns = PauseOuts = 0; }

        void Update()
        {
            bool paused = !OzUeControls.Playing;
            if (paused != pausedShown) { pausedShown = paused; if (paused) PauseIns++; else PauseOuts++; }
            float d = OzTacticalPause.EaseSec > 0.001f ? Mathf.Min(Time.unscaledDeltaTime, 0.1f) / OzTacticalPause.EaseSec : 1f;
            k = paused ? Mathf.Min(1f, k + d) : Mathf.Max(0f, k - d);
            float w = Mathf.SmoothStep(0f, 1f, k);
            if (w <= 0.0001f) { if (post != null) post.Weight = 0f; return; }
            post ??= new OzPost(VolumeName, OzTacticalPause.VignetteSaturation, OzTacticalPause.VignetteExposure, OzTacticalPause.VignetteIntensity, VolumePriority);
            post.Weight = w;
        }

        void OnDestroy()
        {
            post?.Destroy(); post = null;
            if (I == this) I = null;
        }

        /// <summary>View/OzUeFloor.cs Grade: while the look is on, the camera's volume stack evaluated WITHOUT it into <paramref name="into"/>
        /// (the floor takes only that grade back out, so the pause's vignette and dim show on the floor as on everything else). The look's
        /// weight is put back at once (the camera evaluates its own stack when it renders, after this). False = the look is off: use the
        /// camera's stack as before.</summary>
        public static bool BaseGrade(VolumeStack into, Transform trigger, LayerMask mask)
        {
            if (!I || I.post == null || into == null) return false;
            float w = I.post.Weight; if (w <= 0f) return false;
            I.post.Weight = 0f;
            try { VolumeManager.instance.Update(into, trigger, mask); }
            finally { I.post.Weight = w; }
            return true;
        }

        /// <summary>One line for the checks.</summary>
        public static string Report() =>
            $"ue pause look: {(I ? "present" : "absent")}, playing {OzUeControls.Playing}, amount {Amount:0.00}, weight {Weight:0.00}, pauses {PauseIns}, resumes {PauseOuts}, " +
            $"look {OzTacticalPause.VignetteIntensity:0.##} / {OzTacticalPause.VignetteSaturation:0.#} / {OzTacticalPause.VignetteExposure:0.##} over {OzTacticalPause.EaseSec:0.##} s";
    }
}
