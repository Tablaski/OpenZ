using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// View only: the rules, the units, the effects and every picture of a battle of up to 70 units are never touched.
    /// The Options row MASS BATTLES · LIGHTER WHEN SLOW (GRAPHICS tab, <see cref="Pref"/>, default OFF = today's full look always). ON:
    /// once a second the controller counts the live units (Sim/ZRuleProf.cs Units: robots, vehicles, guns) and reads the frame time
    /// (Time.unscaledDeltaTime, eased over about a second). It switches the lighter look in when there are MORE than <see cref="UnitFloor"/>
    /// units AND the frames have been slow for <see cref="SlowHoldSec"/> s (the eased frame over <see cref="SlowShare"/> x the frame-rate
    /// limit's frame: 60 fps -> over 20.8 ms, 30 fps -> over 41.7 ms); it switches it out again when the frames have been smooth (under
    /// <see cref="SmoothShare"/> x the limit's frame) for <see cref="SmoothHoldSec"/> s, or at once when the units drop to the floor or under.
    /// Nothing switches while the game is paused or during the level's ending (the eased frame is not fed by a paused frame).
    /// The lighter look (<see cref="Lighter"/>), each piece only for units that are small on the screen (a body under <see cref="SmallPx"/>
    /// px tall at 1080, scaled with the screen), never for the unit under the pointer or a picked one:
    ///  * robot foot IK off (View/RobotLegIK.cs OnAnimatorIK releases its goals: the feet follow the walk clip; at that size the foot
    ///    planting is not seen).
    /// Further pieces are added only from the cap bench's numbers (1.3 item 8, View/OzUeCapBench.cs SPLIT rows), each behind <see cref="Lighter"/>.
    /// </summary>
    public sealed class OzMassLod : MonoBehaviour
    {
        public const string Label = "MASS BATTLES · LIGHTER WHEN SLOW", Pref = "oz_mass_lod";
        public const string Tip = "MASS BATTLES|On: when more than 70 units fight AND the game is running slowly, small far-away robots are drawn a little simpler (their feet follow the walk without foot placement). It switches back by itself when the game is smooth again. Off (default): the full look always. Battles of up to 70 units never change.|";
        /// <summary>The owner's floor: up to this many units nothing is simpler.</summary>
        public const int UnitFloor = 70;
        public const float SlowShare = 1.25f, SmoothShare = 1.08f, SlowHoldSec = 2f, SmoothHoldSec = 5f, EaseSec = 1f, SmallPx = 48f;

        static int pref = -1;
        /// <summary>The Options switch (oz_mass_lod, default OFF), saved, live.</summary>
        public static bool On
        {
            get { if (pref < 0) pref = PlayerPrefs.GetInt(Pref, 0) == 1 ? 1 : 0; return pref == 1; }
            set
            {
                pref = value ? 1 : 0; PlayerPrefs.SetInt(Pref, pref); PlayerPrefs.Save();
                if (!value) SetLight(false, "the option is off");
                Debug.Log($"[OpenZ Options] mass battles lighter when slow: {(value ? "ON" : "OFF")} ({Pref} {pref}, saved, live)");
            }
        }

        /// <summary>The lighter look is in now (only ever with the option on, more than 70 units and slow frames).</summary>
        public static bool Lighter { get; private set; }
        /// <summary>Checks: times it switched in / out, the last count and eased frame (ms), the last reason.</summary>
        public static int SwitchesIn, SwitchesOut, LastUnits; public static float LastFrameMs; public static string LastWhy = "";
        /// <summary>The bench / a check can hold the light look on or off (null = the controller decides). Session only.</summary>
        public static bool? Force;
        /// <summary>A check may feed a frame time (s) in place of the real one, to test the switching rules (null = the real frame). Session only.</summary>
        public static float? TestFrame;

        static OzMassLod I;
        float eased = -1f, slowFor, smoothFor, nextCount;
        // the per-frame screen measure for SmallOnScreen
        static int camFrame = -1; static Camera cam; static float focal, smallPx;
        static PlayerInput input; static readonly System.Collections.Generic.HashSet<int> picked = new System.Collections.Generic.HashSet<int>();
        static int hoverId = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded; }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SetLight(false, "a new scene");
            if (!ZGame.I && !FindFirstObjectByType<ZGame>()) return;                                                 // game levels and the Unit editor only
            if (I) return;
            I = new GameObject("OzMassLod").AddComponent<OzMassLod>();
        }

        void OnDestroy() { if (I == this) { I = null; SetLight(false, "gone"); } }

        static void SetLight(bool on, string why)
        {
            if (Lighter == on) return;
            Lighter = on; LastWhy = why;
            if (on) SwitchesIn++; else SwitchesOut++;
            Debug.Log($"[OpenZ] mass battles: lighter look {(on ? "IN" : "OUT")} ({why}; {LastUnits} units, frame {LastFrameMs:0.0} ms)");
        }

        void Update()
        {
            if (Force.HasValue) { SetLight(Force.Value, "held by a check"); return; }
            if (!On) { SetLight(false, "the option is off"); return; }
            float dt = Time.unscaledDeltaTime, fdt = TestFrame ?? dt;
            var g = ZGame.I;
            if (Time.timeScale <= 0f || g == null || g.paused || g.World == null || g.World.GameOver) { slowFor = smoothFor = 0f; return; }   // a paused frame or the ending says nothing
            eased = eased < 0f ? fdt : Mathf.Lerp(eased, fdt, 1f - Mathf.Exp(-dt / EaseSec));
            LastFrameMs = eased * 1000f;
            float now = Time.unscaledTime;
            if (now >= nextCount) { nextCount = now + 0.5f; LastUnits = ZRuleProf.Units(g.World); }
            if (LastUnits <= UnitFloor) { slowFor = smoothFor = 0f; SetLight(false, "70 units or fewer"); return; }
            float frame = RefFrame;
            if (eased > frame * SlowShare) { slowFor += dt; smoothFor = 0f; }
            else if (eased < frame * SmoothShare) { smoothFor += dt; slowFor = 0f; }
            else { slowFor = 0f; smoothFor = 0f; }
            if (!Lighter && slowFor >= SlowHoldSec) SetLight(true, "more than 70 units and slow frames");
            else if (Lighter && smoothFor >= SmoothHoldSec) SetLight(false, "smooth again");
        }

        /// <summary>The frame-rate limit's frame (s) the switching compares with: the frame target in force (the FRAME RATE LIMIT row, or a
        /// lower one the heat guard holds), else the game's cap; 60 fps when there is no target (1.3 item 5's UNLIMITED (VSYNC): CapFps -1).</summary>
        public static float RefFrame
        {
            get
            {
                int fps = Application.targetFrameRate > 0 ? Application.targetFrameRate : GameBoot.CapFps > 0 ? GameBoot.CapFps : 60;
                return 1f / fps;
            }
        }

        /// <summary>A unit view small on the screen now (its 1.85 m body under SmallPx at 1080, scaled), not under the pointer and not picked.
        /// Always false while the lighter look is out.</summary>
        public static bool SmallOnScreen(UnitView v)
        {
            if (!Lighter || !v) return false;
            if (camFrame != Time.frameCount)
            {
                camFrame = Time.frameCount; cam = Camera.main;
                focal = cam && !cam.orthographic ? Screen.height * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) : 0f;
                smallPx = SmallPx * Mathf.Max(0.75f, Screen.height / 1080f);
                if (!input) input = FindFirstObjectByType<PlayerInput>();
                picked.Clear(); hoverId = -1;
                if (input)
                {
                    foreach (var s in input.Selection) if (s != null) picked.Add(s.Id);
                    if (input.Hover != null) hoverId = input.Hover.Id;
                }
            }
            if (!cam || focal <= 0f) return false;
            var o = v.Obj;
            if (o != null && (picked.Contains(o.Id) || o.Id == hoverId || (o.Leader != null && (picked.Contains(o.Leader.Id) || o.Leader.Id == hoverId)))) return false;   // the picked squad and the one under the pointer keep the full look
            float depth = Vector3.Dot(v.transform.position - cam.transform.position, cam.transform.forward);
            if (depth <= 0.5f) return false;
            return 1.85f * focal / depth < smallPx;
        }

        /// <summary>View/RobotLegIK.cs: this robot's foot IK rests now (the lighter look, small on the screen).</summary>
        public static bool SkipLegIk(UnitView v) => Lighter && SmallOnScreen(v);
    }
}
