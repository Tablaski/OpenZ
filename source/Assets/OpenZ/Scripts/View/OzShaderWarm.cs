using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace OpenZ
{
    /// <summary>
    /// 1.2 items 171 / 171b, the built game's half: the first time a shader variant draws, the game loads it and makes its programs, which
    /// can cost a short hitch the first time a unit, an effect or a picture appears. This loads every variant of the list the editor keeps
    /// (Resources/OpenZ/Shaders/OzWarmVariants, see Editor/OzShaderWarmup.cs) a few at a time behind the main menu: it waits while the intro
    /// film plays (<see cref="SkipScene"/>) and for <see cref="StartDelaySeconds"/> after it, then spends at most about
    /// <see cref="FrameBudgetMs"/> of a frame on it until the list is done (it goes on in a level if the player starts one sooner). The log
    /// line "[OpenZ Shaders]" says what it cost. The editor warms its own way (it compiles the variants), so in the editor this runs only when
    /// <see cref="StartNow"/> is called (menu OpenZ/Shaders/Player warm-up test, in Play).
    /// 1.3 item 23, the very first start (a player, 30 Oct 2026: "at the first start it was also very slow, after about 10 seconds it caught
    /// up ... From the second start everything is fine"): on a graphics driver that has not seen these shaders yet (the first start of a new
    /// About 8 menu frames a second for half a minute, right after the menu came
    /// up. The main menu (View/OzMenuPerf.cs) now holds it (<see cref="MenuHold"/>) while the flybys prepare, while the AUTO pick of the
    /// menu video measures and while the mouse or the keyboard is in use; and in the menu a slow step (one variant over
    /// <see cref="SlowStepMs"/>) is followed by a rest of <see cref="RestFactor"/> x its time (<see cref="MenuSpacing"/>), so the warm-up
    /// takes at most a quarter of the menu's time. In a level, on the loading screen, and on a PC whose driver knows the shaders (every
    /// later start: every step fast) nothing changes.
    /// </summary>
    public sealed class OzShaderWarm : MonoBehaviour
    {
        public const string ListResource = "OpenZ/Shaders/OzWarmVariants";
        /// <summary>Time per frame spent on the list (ms); one variant is always taken, so a frame never gets less done than one.</summary>
        public static float FrameBudgetMs = 2f;
        /// <summary>Wait after the first frame outside the intro film (s), so the menu comes up first.</summary>
        public static float StartDelaySeconds = 1.5f;
        /// <summary>The scene the warm-up never runs in (the intro film plays undisturbed).</summary>
        public static string SkipScene = "Intro";
        /// <summary>The finished run, one line (also in the log).</summary>
        public static string LastReport { get; private set; } = "";
        public static bool Done { get; private set; }

        // ---- 1.3 item 23: the main menu's hold and rests
        /// <summary>The main menu holds the warm-up this frame (set every menu frame by View/OzMenuPerf.cs, cleared when the menu goes).</summary>
        public static bool MenuHold;
        /// <summary>The main menu: a slow step is followed by a rest (cleared on the loading screen and when the menu goes).</summary>
        public static bool MenuSpacing;
        /// <summary>A step whose one variant took longer than this (ms) is slow (the driver compiling it); a known shader takes far less.</summary>
        public const float SlowStepMs = 8f;
        /// <summary>A step this slow in the menu means a driver that has not seen the shaders: one such variant is a visible hitch, so the list goes on at the next loading screen.</summary>
        public const float ColdMenuStepMs = 120f;
        public static bool MenuCold;
        /// <summary>The rest after a slow step in the menu, in times the step's own work: the warm-up then takes at most 1 / (1 + this) of the time.</summary>
        public const float RestFactor = 3f;
        /// <summary>When the last slow step ran (unscaled time).</summary>
        public static float LastSlowStepAt { get; private set; } = -10f;
        /// <summary>A slow step ran in the last half second: View/OzMenuPerf.cs leaves those frames out of the AUTO pick.</summary>
        public static bool Disturbs => !Done && Time.unscaledTime - LastSlowStepAt < 0.5f;
        /// <summary>Checks: variants taken and the warm-up's work in ms (this run of the game).</summary>
        public static int StepsDone { get; private set; }
        public static double WorkMsDone { get; private set; }
        /// <summary>The state in a word, for the menu's [OpenZ MenuPerf] line.</summary>
        public static string State => Done ? "done" : !live ? "not started" : MenuHold ? "held by the menu" : "running";
        /// <summary>Checks and -ozwarmcold (dev builds only): every variant of the list emulated as a cold driver compile of this many ms (the
        /// <see cref="ColdStepMs"/>
        /// many (0 = the list's count).</summary>
        public static float ProofColdStepMs; public static int ProofColdVariants;
        public const float ColdStepMs = 57f;

        static OzShaderWarm live;
        ShaderVariantCollection list;
        ResourceRequest load;
        float wait;
        double workMs, longestMs;
        int frames, steps, total;
        float restUntil; double heldSec, restSec; int coldDone;                                                // item 23

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { live = null; Done = false; LastReport = ""; MenuHold = MenuSpacing = MenuCold = false; LastSlowStepAt = -10f; StepsDone = 0; WorkMsDone = 0.0; ProofColdStepMs = 0f; ProofColdVariants = 0; }   // item 23: the hold, the counts, the emulation

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (!Application.isEditor) StartNow(); }

        /// <summary>Starts the warm-up (once per run of the game).</summary>
        public static void StartNow()
        {
            if (live || Done) return;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Done = true; LastReport = "no graphics device: nothing to warm"; return; }
            var go = new GameObject("OzShaderWarm") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            live = go.AddComponent<OzShaderWarm>();
            if (ProofColdStepMs <= 0f && GameBoot.DevArg("-ozwarmcold")) { ProofColdStepMs = ColdStepMs; Debug.Log($"[OpenZ Shaders] -ozwarmcold: every variant emulated as a cold driver compile of {ColdStepMs:0} ms (the first start of a new build; 1.3 item 23)"); }
        }

        /// <summary>Checks (item 23): the warm-up once more from the start, as at the start of a run (a running one stops first).</summary>
        public static void ProofRestart()
        {
            if (live) { Destroy(live.gameObject); live = null; }
            Done = false; LastReport = "";
            StartNow();
        }

        /// <summary>Checks (item 23): a running warm-up ends now; its line says so.</summary>
        public static void ProofCancel() { if (live) live.Finish($"cancelled by a check after {live.steps} steps, {live.workMs:0} ms of work over {live.frames} frames{live.MenuNote()}"); }

        string MenuNote() => heldSec > 0.0 || restSec > 0.0 ? $"; the main menu held it {heldSec:0.0} s and let it rest {restSec:0.0} s (1.3 item 23)" : "";

        void Awake() { wait = StartDelaySeconds; }

        void Update()
        {
            if (Done || live != this) return;                                                                  // item 23: after a check's restart the old one stops at once
            if (SceneManager.GetActiveScene().name == SkipScene) { wait = StartDelaySeconds; return; }
            if (wait > 0f) { wait -= Time.unscaledDeltaTime; return; }
            if (MenuHold) { heldSec += Time.unscaledDeltaTime; return; }                                    // item 23: the main menu needs this frame (the flybys prepare, the AUTO pick measures, a click)
            if (OzLevelWarm.HoldsGlobal) return;                                                                  // 1.4: a level's own list warms on its loading screen; inside that level the rest of this list waits for the next menu (View/OzLevelWarm.cs)
            if (Time.unscaledTime < restUntil) { restSec += Time.unscaledDeltaTime; return; }               // item 23: the rest after a slow step (main menu only)
            if (MenuCold && MenuSpacing) { heldSec += Time.unscaledDeltaTime; return; }                     // a driver that compiles: the rest of the list waits for the loading screen, the menu keeps its frames
            if (!list)   // loaded only now (after the intro film and the wait), in the background: it pulls in every listed shader
            {
                if (load == null) { load = Resources.LoadAsync<ShaderVariantCollection>(ListResource); OzBootTime.Once("warm", "shader warm-up start"); return; }   // 1.5 kit player_reports_1111 P2
                if (!load.isDone) return;
                list = load.asset as ShaderVariantCollection;
                if (!list) { Finish("no warm-up list (Resources/" + ListResource + "): nothing warmed"); return; }
                total = list.variantCount;
                if (ProofColdStepMs > 0f) { if (ProofColdVariants > 0) total = ProofColdVariants; Debug.Log($"[OpenZ Shaders] emulating a cold driver: {total} variants of {ProofColdStepMs:0} ms each (a check or -ozwarmcold; the engine's warm-up is not called)"); return; }   // item 23
                if (list.isWarmedUp) Finish($"{total} variants were warm already");
                return;   // the warm-up itself starts next frame (the load had this frame)
            }
            frames++;
            var sw = Stopwatch.StartNew();
            bool all = false; double slowest = 0.0;
            while (!all)
            {
                double t0 = sw.Elapsed.TotalMilliseconds;
                if (ProofColdStepMs > 0f) { while (sw.Elapsed.TotalMilliseconds - t0 < ProofColdStepMs) { } all = ++coldDone >= total; }   // item 23: a cold driver emulated (checks, -ozwarmcold)
                else all = list.WarmUpProgressively(1);
                steps++; StepsDone++;
                double d = sw.Elapsed.TotalMilliseconds - t0;
                if (d > longestMs) longestMs = d;
                if (d > slowest) slowest = d;
                if (steps > total + 64) all = true;   // a variant the build left out never counts as warm: stop after one pass over the list
                if (sw.Elapsed.TotalMilliseconds >= FrameBudgetMs) break;
            }
            double work = sw.Elapsed.TotalMilliseconds;
            workMs += work; WorkMsDone += work;
            if (slowest > SlowStepMs)                                                                         // item 23: a slow step (a driver compiling)
            {
                LastSlowStepAt = Time.unscaledTime;
                if (slowest > ColdMenuStepMs) MenuCold = true;
                if (MenuSpacing) restUntil = Time.unscaledTime + (float)(work * (RestFactor + 1f) / 1000.0);    // the next step (1 + RestFactor) x this work after this frame began: at most a quarter of the menu's time
            }
            if (all) Finish($"{(ProofColdStepMs > 0f ? coldDone + " emulated cold" : list.warmedUpVariantCount.ToString())} of {total} variants warmed in {workMs:0} ms of work over {frames} frames (longest single variant {longestMs:0.0} ms){MenuNote()}");
        }

        void Finish(string what)
        {
            Done = true;
            LastReport = what;
            Debug.Log("[OpenZ Shaders] " + what);
            OzBootTime.Mark("shader warm-up end");   // 1.5 kit player_reports_1111 P2
            if (live == this) live = null;
            Destroy(gameObject);
        }
    }
}
