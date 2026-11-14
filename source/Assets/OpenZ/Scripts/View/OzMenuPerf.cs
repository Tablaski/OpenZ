// OpenZ (GPL-3.0) - 1.3 item 23: the main menu on slower computers (a player on Discord, 29 Oct 2026:
// (attached by itself to the OpenZMainMenu object; it goes with the scene):
//  * THE MEASUREMENT LINE in Player.log, every 10 s while the menu shows, plus one header line with the machine and one summary when
//    the menu is left: frames a second, the worst frame, the 1 % low, frames over 25 / 50 ms, the processor's (main / render thread) and
//    the graphics chip's milliseconds (the engine's frame timing), draw batches and set-pass calls, the cameras drawn a frame (the menu's
//    own, plus the faction screen's two poster cameras), garbage a frame, and the flyby video: which clip at which size, the decoded
//    frames shown a second against the clip's rate, the frames the player dropped. A player sends the log and we see where the time goes.
//  * THE MENU LIGHT: work the menu's own camera does for nothing, taken out. That camera (MainMenu.unity, MenuCamera) draws no object
//    (culling mask 0) and clears to one colour; the whole menu is a screen-space overlay canvas drawn after it. Yet with the project's
//    pipeline it ran, at the full screen size every frame: a 4x MSAA target and its resolve, the depth and the opaque copies, the SSAO
//    pass (depth-normals, full-resolution AO, blur) and the OpenZ Atmosphere full-screen pass (a colour copy and the pass; _AtmoEnabled
//    is 0 in the menu, so it hands the colour back unchanged). On a clear colour none of it changes a pixel. The
//    menu light: the menu camera gets no MSAA (its own camera setting), and the SSAO and Atmosphere features are paused
//    ONLY while that camera draws (RenderPipelineManager.beginCameraRendering / endCameraRendering): every other camera, the faction
//    screen's posters included, draws with them exactly as before, and the features are back before any other camera draws (nothing is
//    ever saved: the switch lives inside one camera's render). The picture stays the same (Editor/OzV13MenuPerfCheck.cs proves it
//    pixel by pixel). The menu light is on by default; -oznomenulight (dev builds) or MenuLight = false turns it off for an A / B.
//    At the
//    first menu of a run, while a flyby plays in the main view or the level selection, after 2 s of warm-up: windows of 4 s of playing
//    video. Each window counts the menu frames, the frames that put a NEW video picture on screen and the frames the VideoPlayer
//    flyby switches to its light copy (1920x1080, 30 fps) without a stall (UI/OzLevelPreview.ToLight: loaded in the background, a hidden
//    player takes it at once, the one on screen at its next cut). Two good windows in a row = it keeps up: nothing changes. Only in AUTO
//    and only while nothing is remembered; a remembered PC opens the light copies at once (OzLevelPreview.Create).
//  * THE VERY FIRST START (a player, 30 Oct 2026: "at the first start it was also very slow, after about 10 seconds it caught up ...
//    From the second start everything is fine"). Two things only the first start of a new build pays: the flybys' first prepare (8.6 -
//    over 256 frames, about 8 menu frames a second for half a minute). This component drives the flybys' one-at-a-time prepare
//    (OzLevelPreview.PumpPrepares, never on the loading screen) and holds the warm-up (OzShaderWarm.MenuHold) while the flybys prepare,
//    while the AUTO pick measures (the warm-up's slow frames must not make a strong PC look slow and switch it to the light copies for
//    good) and while the mouse or the keyboard is in use; in the menu each slow warm-up step is followed by a rest of 3x its time
//    (OzShaderWarm.MenuSpacing). The hold for the flybys and the AUTO pick ends after WarmHoldMaxSec at the latest. On a later start every
//    warm-up step is fast: nothing rests, nothing changes.
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace OpenZ
{
    public sealed class OzMenuPerf : MonoBehaviour
    {
        public const string Tag = "[OpenZ MenuPerf]";
        /// <summary>Seconds per measurement line.</summary>
        public const float Every = 10f;
        /// <summary>The menu light (session only, never saved): on unless -oznomenulight is given to a dev build; checks switch it for A / B.</summary>
        public static bool MenuLight = true;
        public static OzMenuPerf I { get; private set; }
        /// <summary>Checks: the last line's numbers (frames a second, the processor's and the graphics chip's ms) and the menu camera
        /// renders the light touched.</summary>
        public static float LastFps, LastCpuMs, LastGpuMs; public static int LightRenders;
        public static string LastLine = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (GameBoot.DevArg("-oznomenulight")) { MenuLight = false; Debug.Log(Tag + " -oznomenulight: the menu light is off for this session (A / B)"); }
            probeDone = false; ProbeState = Probe.Off; ProbeNotes = "";                                    // a new run (also an editor Play without a domain reload)
            if (GameBoot.DevArg("-ozmenuslow")) { ProofBurnMs = SlowBurnMs; Debug.Log($"{Tag} -ozmenuslow: {SlowBurnMs:0} ms of processor time burnt every menu frame (a forced-slow PC for the AUTO pick test)"); }
            SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded;
            Attach();
        }
        static void OnLoaded(Scene s, LoadSceneMode m) => Attach();
        static void Attach()
        {
            var menu = FindFirstObjectByType<OpenZMainMenu>();
            if (menu && !menu.GetComponent<OzMenuPerf>()) menu.gameObject.AddComponent<OzMenuPerf>();
        }

        OpenZMainMenu menu; Camera menuCam;
        int startSkipped; static bool firstPictureSaid;
        bool camKept, msaa0; bool lightApplied; float nextFind;
        readonly List<ScriptableRendererFeature> paused = new List<ScriptableRendererFeature>(), lightFeatures = new List<ScriptableRendererFeature>();
        UniversalRenderPipelineAsset lightUrp;
        static readonly FieldInfo RendererList = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);

        ProfilerRecorder recBatches, recSetPass, recGc;
        readonly FrameTiming[] timing = new FrameTiming[1];
        readonly int[] hist = new int[201];                                                              // frame times in 1 ms bins (200 = 200 ms or more)
        float winT, worst, totT; int frames, over25, over50, totFrames, camFrames, totOver50; double cpuMain, cpuRender, gpu, batches, setPass, gc; int nCpu, nGpu, nRec;
        float totWorst; double totCpu, totGpu; int totNCpu, totNGpu; readonly int[] totHist = new int[201];
        readonly HashSet<VideoPlayer> watched = new HashSet<VideoPlayer>();
        readonly Dictionary<VideoPlayer, long> frameAt = new Dictionary<VideoPlayer, long>();
        int dropped, totDropped; bool headerDone;
        static readonly StringBuilder sb = new StringBuilder(512);

        // ---- the AUTO pick of the menu video (1.3 item 23)
        public enum Probe { Off, Measuring, KeepsUp, FellBehind, GaveUp }
        /// <summary>The AUTO pick's state in this run (Off: FULL / LIGHT by hand, or the light copies remembered).</summary>
        public static Probe ProbeState { get; private set; } = Probe.Off;
        /// <summary>The windows measured, one line each (for the log and the check).</summary>
        public static string ProbeNotes = "";
        /// <summary>The forced-slow test: processor time burnt every menu frame (-ozmenuslow in a dev build, or the check).</summary>
        public static float ProofBurnMs;
        public const float SlowBurnMs = 25f;                                                               // about 30 menu fps on the dev PC: under the 45 fps line, and the 60 fps video can show at most half its frames
        static bool probeDone;
        float pWarm, pT, pSlideT; int pFrames, pShown, pSteps, pDropped, pDropMark, pWin, pFail, pPass; float pClipFps;
        readonly StringBuilder pSb = new StringBuilder(256);
        readonly Dictionary<VideoPlayer, long> seenFrame = new Dictionary<VideoPlayer, long>();
        int winShown, winSteps; string viewNow = "?"; float nextView;
        static readonly System.Diagnostics.Stopwatch burnSw = new System.Diagnostics.Stopwatch();

        // ---- the very first start (1.3 item 23): the shader warm-up's hold (View/OzShaderWarm.cs)
        /// <summary>The warm-up waits at most this long in a menu for the flybys' first prepare and the AUTO pick (then it goes on, spaced).</summary>
        public const float WarmHoldMaxSec = 30f;
        /// <summary>The warm-up waits while the mouse or the keyboard was used in the last this many seconds (clicks and hovers answer at once).</summary>
        public const float InputQuietSec = 0.4f;
        float warmHeld, lastInputAt = -10f; Vector2 lastMousePos;
        /// <summary>Checks: seconds this menu held the warm-up for the flybys and the AUTO pick.</summary>
        public float ProofWarmHeldSec => warmHeld;
        /// <summary>Checks: a new run of the game for the AUTO pick (this run's verdict forgotten, as at a start; nothing saved is touched).</summary>
        public static void ProofNewRun() { probeDone = false; ProbeState = Probe.Off; ProbeNotes = ""; }

        void OnEnable()
        {
            I = this;
            menu = GetComponent<OpenZMainMenu>();
            recBatches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            recSetPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            recGc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            RenderPipelineManager.beginCameraRendering += BeginCam;
            RenderPipelineManager.endCameraRendering += EndCam;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCam;
            RenderPipelineManager.endCameraRendering -= EndCam;
            OzShaderWarm.MenuHold = false; OzShaderWarm.MenuSpacing = false; OzLevelPreview.QueueHold = false;   // item 23: a level and its loading as before
            Resume();
            UndoCamera();
            Summary();
            recBatches.Dispose(); recSetPass.Dispose(); recGc.Dispose();
            foreach (var v in watched) if (v) v.frameDropped -= OnDropped;
            watched.Clear(); frameAt.Clear(); seenFrame.Clear();
            if (I == this) I = null;
        }

        void Start() { FindCamera(); ProbeStart(); }

        /// <summary>The menu's own camera: the scene's camera that draws no object (culling mask 0). Nothing else is touched.</summary>
        void FindCamera()
        {
            if (menuCam || Time.unscaledTime < nextFind) return;
            nextFind = Time.unscaledTime + 1f;                                                             // a scene search at most once a second
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c && c.cullingMask == 0 && c.targetTexture == null && c.gameObject.scene == gameObject.scene) { menuCam = c; break; }
        }

        // ------------------------------------------------------------------ the menu light
        /// <summary>The menu camera's own MSAA off while the light is on; put back when it is off or the menu goes. (Its SMAA setting is left
        /// alone: the camera has post-processing off, MainMenu.unity m_RenderPostProcessing 0, and URP runs SMAA only in the post-process pass.)</summary>
        void ApplyCamera()
        {
            if (!menuCam) return;
            if (MenuLight && !lightApplied)
            {
                if (!camKept) { msaa0 = menuCam.allowMSAA; camKept = true; }
                menuCam.allowMSAA = false;
                lightApplied = true;
            }
            else if (!MenuLight && lightApplied) UndoCamera();
        }

        void UndoCamera()
        {
            if (!lightApplied) return;
            if (menuCam && camKept) menuCam.allowMSAA = msaa0;
            lightApplied = false;
        }

        /// <summary>Before each camera draws: the menu camera with the light on = the SSAO and Atmosphere features paused for its render;
        /// any other camera = they are active (as always).</summary>
        void BeginCam(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == menuCam && MenuLight && menuCam && menuCam.cullingMask == 0) { Pause(); LightRenders++; }
            else Resume();
            camFrames++;
        }

        void EndCam(ScriptableRenderContext ctx, Camera cam) { if (cam == menuCam) Resume(); }

        void Pause()
        {
            if (paused.Count > 0) return;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != lightUrp) FindLightFeatures(urp);                                                  // once per pipeline asset (a preset change swaps it): no garbage a frame
            foreach (var f in lightFeatures) if (f && f.isActive) { f.SetActive(false); paused.Add(f); }
        }

        /// <summary>The SSAO and Atmosphere features of the pipeline's default renderer (the menu camera uses it), looked up once per
        /// pipeline asset (Object.name and the reflected index allocate, so never a frame).</summary>
        void FindLightFeatures(UniversalRenderPipelineAsset urp)
        {
            lightUrp = urp; lightFeatures.Clear();
            if (!urp || RendererList == null) return;
            if (!(RendererList.GetValue(urp) is ScriptableRendererData[] list) || list.Length == 0) return;
            var rd = list[Mathf.Clamp(urp.DefaultRendererIndexOrZero(), 0, list.Length - 1)];
            if (!rd) return;
            foreach (var f in rd.rendererFeatures)
                if (f && (f is ScreenSpaceAmbientOcclusion || f.name.Contains("Atmosphere"))) lightFeatures.Add(f);
        }

        void Resume()
        {
            if (paused.Count == 0) return;
            foreach (var f in paused) if (f) f.SetActive(true);
            paused.Clear();
        }

        // ------------------------------------------------------------------ the measurement
        void OnDropped(VideoPlayer v) { dropped++; totDropped++; }

        void WatchPlayers()
        {
            var live = OzLevelPreview.Live;                                                                // the menu's previews (no array a call)
            for (int i = 0; i < live.Count; i++) { var v = live[i].Player; if (v && watched.Add(v)) v.frameDropped += OnDropped; }
        }

        // ------------------------------------------------------------------ the AUTO pick of the menu video
        /// <summary>Checks: measure again from now (the verdict of this run forgotten; nothing saved is touched).</summary>
        public void ProbeRestart() { probeDone = false; ProbeStart(); }

        void ProbeStart()
        {
            ProbeState = !probeDone && OzMenuVideo.Probing ? Probe.Measuring : probeDone ? ProbeState : Probe.Off;
            pWarm = 0f; pSlideT = 0f; ProbeWindowReset(); pWin = pFail = pPass = 0; pSb.Clear(); pDropMark = totDropped;
            if (ProbeState == Probe.Measuring) ProbeNotes = "";
            string state = ProbeState == Probe.Measuring ? "AUTO: measuring the first seconds" : probeDone && ProbeState == Probe.KeepsUp ? "AUTO: this PC kept up earlier in this run" : OzMenuVideo.Why;
            Debug.Log($"{Tag} menu video {OzMenuVideo.Name}, playing the {(OzMenuVideo.UseLight ? "light copies 1920x1080 30 fps" : "full flybys 2560x1440 60 fps")} ({state})");
        }

        void ProbeWindowReset() { pT = 0f; pFrames = pShown = pSteps = pDropped = 0; pClipFps = 0f; }

        /// <summary>One menu frame: did a NEW video picture reach the screen (any playing flyby's frame index moved), is a flyby showing,
        /// and how many pictures the flybys delivered since the last menu frame (the largest frame-index step of a showing flyby: a
        /// seek or a loop counts as one; two flybys during a cut are not added up).</summary>
        bool VideoFrame(float dt, out bool showing, out float clipFps, out int steps)
        {
            bool fresh = false; showing = false; clipFps = 0f; steps = 0;
            var live = OzLevelPreview.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var p = live[i]; var v = p.Player;
                if (!p.ShowingVideo) continue;
                showing = true;
                if (v.clip) clipFps = Mathf.Max(clipFps, (float)v.clip.frameRate);
                long f = v.frame;
                bool known = seenFrame.TryGetValue(v, out long was);
                if (!known || was != f)
                {
                    fresh = true; seenFrame[v] = f;
                    long most = (long)System.Math.Ceiling(v.frameRate * dt) + 1;                                  // a step is at most what the clip's rate allows in this menu frame: a seek forward (a card's start at 5 s) is not "delivered"
                    int st = known && was >= 0 && f > was ? (int)System.Math.Min(f - was, most) : 1;
                    if (st > steps) steps = st;
                }
            }
            return fresh;
        }

        void ProbeTick(float dt, bool fresh, bool showing, float clipFps, int steps)
        {
            int dNew = totDropped - pDropMark; pDropMark = totDropped;                                     // frames the flybys dropped since the last menu frame
            if (ProbeState != Probe.Measuring) return;
            if (OzMenuVideo.Mode != OzMenuVideo.Auto) { ProbeState = Probe.Off; return; }                  // FULL / LIGHT set meanwhile (Mode reads no string: no garbage a frame)
            bool fair = showing && (Application.isFocused || Application.isEditor) && (viewNow == "Main" || viewNow == "Select") && !OzShaderWarm.Disturbs;   // item 23: never in the half second after a slow shader warm-up step; a flyby on screen, the window in front (the editor's Game view runs without the focus, as for the slow-computer tip); not the faction screen or loading
            if (!fair) return;
            if (dt > OzMenuVideo.HitchSec)                                                                  // a hitch (loading, garbage collection) is not "falling behind" ...
            {
                pSlideT = dt > OzMenuVideo.SlideMaxFrame ? 0f : pSlideT + dt;                              // ... but a slide show is: every frame over 0.25 s for 6 s in a row (a frame over 2 s is a stall, a dragged window)
                if (pSlideT < OzMenuVideo.SlideRunSec) return;
                pSlideT = 0f; ProbeWindowReset();
                ProbeWindow(true, "slide show", $"every frame over {OzMenuVideo.HitchSec:0.00} s for {OzMenuVideo.SlideRunSec:0} s");
                return;
            }
            pSlideT = 0f;
            if (pWarm < OzMenuVideo.WarmUp) { pWarm += dt; return; }
            pT += dt; pFrames++; if (fresh) pShown++; pSteps += steps; pDropped += dNew; pClipFps = Mathf.Max(pClipFps, clipFps);
            if (pT < OzMenuVideo.Window) return;
            int delivered = Mathf.Clamp(pSteps - pDropped, 0, pFrames);                                    // pictures delivered, at most one a menu frame (the beat of a 60 fps clip on a 60 fps menu costs nothing here)
            var s = new OzMenuVideo.Sample { Seconds = pT, MenuFrames = pFrames, Shown = delivered, Dropped = pDropped, ClipFps = pClipFps, Cap = OzMenuVideo.ScreenCap() };
            bool behind = OzMenuVideo.FallsBehind(s, out string why);
            string text = s.ToString() + " (index moved on " + pShown + " menu frames)";
            ProbeWindowReset();
            ProbeWindow(behind, why, text);
        }

        /// <summary>One window measured: counted, and two in a row decide.</summary>
        void ProbeWindow(bool behind, string why, string text)
        {
            pWin++; if (behind) { pFail++; pPass = 0; } else { pPass++; pFail = 0; }
            pSb.Append(pSb.Length > 0 ? "; " : "").Append("window ").Append(pWin).Append(": ").Append(text).Append(behind ? " BEHIND (" + why + ")" : " ok");
            ProbeNotes = pSb.ToString();
            if (pFail >= OzMenuVideo.Decide)
            {
                ProbeState = Probe.FellBehind; probeDone = true;
                OzMenuVideo.RememberLight(ProbeNotes);
                var live = OzLevelPreview.Live;
                for (int i = 0; i < live.Count; i++) live[i].ToLight();
            }
            else if (pPass >= OzMenuVideo.Decide)
            {
                ProbeState = Probe.KeepsUp; probeDone = true;
                Debug.Log($"{Tag} menu video AUTO: this PC keeps up with the full flybys ({ProbeNotes}): the 2560x1440 60 fps flybys stay, nothing saved");
            }
            else if (pWin >= OzMenuVideo.MaxWindows)
            {
                ProbeState = Probe.GaveUp; probeDone = true;
                Debug.Log($"{Tag} menu video AUTO: no clear answer in {pWin} windows ({ProbeNotes}): the full flybys stay, measured again at the next start");
            }
        }

        void LateUpdate()
        {
            FindCamera(); ApplyCamera();
            if (ProofBurnMs > 0f) { burnSw.Restart(); while (burnSw.Elapsed.TotalMilliseconds < ProofBurnMs) { } }   // the forced-slow test only
            float dt = Time.unscaledDeltaTime; if (dt <= 0f) return;
            if (startSkipped < 2)
            {   // the first two steps of the clock hold the loading before the first picture: no menu frame was on screen yet, so they have their own line
                if (++startSkipped < 2) return;
                if (!firstPictureSaid) { firstPictureSaid = true; Debug.Log($"{Tag} first picture {Time.realtimeSinceStartup:0.00} s after the game started"); }
                return;
            }
            if (Time.unscaledTime >= nextView) { nextView = Time.unscaledTime + 0.5f; viewNow = menu ? menu.ProofView : "?"; }   // the view's name allocates: twice a second, not a frame
            bool loadingView = viewNow == "Loading";
            OzLevelPreview.QueueHold = loadingView; OzLevelPreview.PumpPrepares();                         // item 23 first start: the flybys prepared one at a time (no decoder opens while a level loads)
            WarmHold(dt, loadingView);
            WatchPlayers();
            bool fresh = VideoFrame(dt, out bool showing, out float clipFps, out int steps);
            if (fresh) winShown++;
            winSteps += steps;
            ProbeTick(dt, fresh, showing, clipFps, steps);
            if (viewNow == "Main" || viewNow == "Select") { var live = OzLevelPreview.Live; for (int i = 0; i < live.Count; i++) live[i].TakePending(); }   // a hidden flyby takes its waiting light copy (never during loading)
            float ms = dt * 1000f;
            frames++; winT += dt; totFrames++; totT += dt;
            int bin = Mathf.Clamp(Mathf.RoundToInt(ms), 0, 200); hist[bin]++; totHist[bin]++;
            if (ms > worst) worst = ms;
            if (ms > totWorst) totWorst = ms;
            if (ms > 25f) over25++;
            if (ms > 50f) { over50++; totOver50++; }
            if (!OzQuality.FpsOn || OzSettings.TakeArg || CaptureKit.CineActive) FrameTimingManager.CaptureFrameTimings();   // the FPS COUNTER captures them itself while it shows: one capture a frame, its figures untouched
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
            {
                if (timing[0].cpuMainThreadFrameTime > 0.0) { cpuMain += timing[0].cpuMainThreadFrameTime; cpuRender += timing[0].cpuRenderThreadFrameTime; nCpu++; totCpu += System.Math.Max(timing[0].cpuMainThreadFrameTime, timing[0].cpuRenderThreadFrameTime); totNCpu++; }
                if (timing[0].gpuFrameTime > 0.0) { gpu += timing[0].gpuFrameTime; nGpu++; totGpu += timing[0].gpuFrameTime; totNGpu++; }
            }
            if (recBatches.Valid) { batches += recBatches.LastValue; setPass += recSetPass.Valid ? recSetPass.LastValue : 0; gc += recGc.Valid ? recGc.LastValue : 0; nRec++; }   // counters a release player does not give stay "not given" / "?"
            if (winT >= Every) Flush();
        }

        /// <summary>The shader warm-up waits while the flybys' first prepare waits or runs and while the AUTO pick measures (both together at
        /// most WarmHoldMaxSec in this menu), and while the mouse or the keyboard is in use; in the menu it rests after a slow step. On the
        /// loading screen neither: the warm-up runs there as before.</summary>
        void WarmHold(float dt, bool loadingView)
        {
            if (InputNow()) lastInputAt = Time.unscaledTime;
            bool busy = !loadingView && (OzLevelPreview.PreparesPending || ProbeState == Probe.Measuring);
            if (busy) warmHeld += dt;
            OzShaderWarm.MenuHold = !loadingView && ((busy && warmHeld < WarmHoldMaxSec) || Time.unscaledTime - lastInputAt < InputQuietSec);
            OzShaderWarm.MenuSpacing = !loadingView;
        }

        /// <summary>The mouse moved, scrolls or holds a button, or a key is down (no garbage).</summary>
        bool InputNow()
        {
            bool any = false;
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null)
            {
                Vector2 mp = m.position.ReadValue();
                any = (mp - lastMousePos).sqrMagnitude > 0.25f || m.delta.ReadValue().sqrMagnitude > 0f || m.leftButton.isPressed || m.rightButton.isPressed || m.scroll.ReadValue().sqrMagnitude > 0f;
                lastMousePos = mp;
            }
            var k = UnityEngine.InputSystem.Keyboard.current;
            return any || (k != null && k.anyKey.isPressed);
        }

        /// <summary>Checks: starts a fresh measurement window (nothing is logged for the part before).</summary>
        public void ProofReset()
        {
            winT = worst = 0f; frames = over25 = over50 = camFrames = dropped = winShown = winSteps = 0; cpuMain = cpuRender = gpu = batches = setPass = gc = 0.0; nCpu = nGpu = nRec = 0;
            System.Array.Clear(hist, 0, hist.Length);
        }
        /// <summary>Checks: the running window's frames a second, the processor's ms (the longer thread) and the graphics chip's ms.</summary>
        public void ProofNow(out float fps, out float cpuMs, out float gpuMs)
        {
            fps = frames / Mathf.Max(0.001f, winT);
            cpuMs = nCpu > 0 ? (float)(System.Math.Max(cpuMain, cpuRender) / nCpu) : 0f;
            gpuMs = nGpu > 0 ? (float)(gpu / nGpu) : 0f;
        }
        /// <summary>Checks: the running window's worst frame (ms).</summary>
        public float ProofWorstMs => worst;
        /// <summary>Checks: frames of the running window that took longer than 100 ms.</summary>
        public int ProofOver100 { get { int n = 0; for (int i = 101; i < hist.Length; i++) n += hist[i]; return n; } }
        /// <summary>Checks: the running window's new video pictures on screen a second, and the frames the flybys dropped in it.</summary>
        public void ProofVideo(out float shownPerSec, out int droppedNow) { shownPerSec = winShown / Mathf.Max(0.001f, winT); droppedNow = dropped; }
        /// <summary>Checks: the running window's video pictures delivered a second, at most one a menu frame (the AUTO pick's measure).</summary>
        public float ProofDeliveredPerSec => Mathf.Clamp(winSteps - dropped, 0, frames) / Mathf.Max(0.001f, winT);
        /// <summary>Checks: the menu's own camera (null before it was found).</summary>
        public Camera ProofMenuCamera => menuCam;

        static float Low1(int[] h, int n)
        {   // the 1 % low as a frame time: the frame time that 1 % of the frames reach or pass
            if (n <= 0) return 0f;
            int want = Mathf.Max(1, Mathf.CeilToInt(n * 0.01f)), seen = 0;
            for (int i = h.Length - 1; i >= 0; i--) { seen += h[i]; if (seen >= want) return i; }
            return 0f;
        }

        void Header()
        {
            headerDone = true;
            var r = Screen.currentResolution;
            Debug.Log($"{Tag} machine: graphics {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsMemorySize} MB), processor {SystemInfo.processorType} ({SystemInfo.processorCount} threads, {SystemInfo.processorFrequency} MHz), memory {SystemInfo.systemMemorySize} MB, " +
                      $"{SystemInfo.operatingSystem}; screen {r.width}x{r.height} @ {r.refreshRateRatio.value:0.#} Hz, window {Screen.width}x{Screen.height} {Screen.fullScreenMode}; graphics {OzQuality.Name}, frame rate limit {OzFpsCap.Name} (target {Application.targetFrameRate}, vsync {QualitySettings.vSyncCount}); menu light {(MenuLight ? "ON" : "OFF")}; menu video {OzMenuVideo.Name} ({OzMenuVideo.Why})");
        }

        void Flush()
        {
            if (!headerDone) Header();
            WatchPlayers();
            float fps = frames / Mathf.Max(0.001f, winT);
            float cm = nCpu > 0 ? (float)(cpuMain / nCpu) : 0f, cr = nCpu > 0 ? (float)(cpuRender / nCpu) : 0f, gm = nGpu > 0 ? (float)(gpu / nGpu) : 0f;
            sb.Clear();
            sb.Append(Tag).Append(' ').Append(Every.ToString("0")).Append(" s in ").Append(menu ? menu.ProofView : "?").Append(": ")
              .Append(fps.ToString("0.0")).Append(" fps, worst frame ").Append(worst.ToString("0.0")).Append(" ms, 1 % low ").Append(Low1(hist, frames).ToString("0")).Append(" ms, over 25 ms ").Append(over25).Append(", over 50 ms ").Append(over50)
              .Append("; processor main ").Append(cm > 0f ? cm.ToString("0.0") + " / render " + cr.ToString("0.0") + " ms" : "not given")
              .Append(", graphics chip ").Append(gm > 0f ? gm.ToString("0.0") + " ms" : "not given")
              .Append("; batches ").Append(nRec > 0 ? (batches / nRec).ToString("0") : "?").Append(", set-pass ").Append(nRec > 0 ? (setPass / nRec).ToString("0") : "?")
              .Append(", cameras a frame ").Append((camFrames / (float)Mathf.Max(1, frames)).ToString("0.0"))
              .Append(", garbage ").Append(nRec > 0 && recGc.Valid ? (gc / nRec / 1024.0).ToString("0.0") + " KB a frame" : "not given");
            VideoPlayer live = null;
            foreach (var v in watched) if (v && v.isPlaying) { live = v; break; }
            if (live)
            {
                long f = live.frame; long was = frameAt.TryGetValue(live, out var w) ? w : -1;
                string clip = live.clip ? live.clip.name + " " + live.clip.width + "x" + live.clip.height + " " + live.clip.frameRate.ToString("0") + " fps" : live.url;
                sb.Append("; flyby ").Append(clip);
                if (was >= 0 && f >= was) sb.Append(": ").Append(f - was).Append(" new frames (").Append(((f - was) / Mathf.Max(0.001f, winT)).ToString("0.0")).Append(" a second)");
                sb.Append(", ").Append(dropped).Append(" dropped, new pictures on screen ").Append((winShown / Mathf.Max(0.001f, winT)).ToString("0.0")).Append(" a second");
            }
            else sb.Append("; no flyby playing (a still)");
            foreach (var v in watched) if (v) frameAt[v] = v.frame;
            sb.Append("; menu light ").Append(MenuLight ? "ON" : "OFF");
            sb.Append("; flyby prepares ").Append(OzLevelPreview.PrepareCalls).Append(", video errors ").Append(OzLevelPreview.ErrorEvents).Append(", levels on the still ").Append(OzLevelPreview.FailedLevels);   // item 31 (since the start)
            sb.Append("; menu video ").Append(OzMenuVideo.Name).Append(OzMenuVideo.UseLight ? ", light copies" : ", full flybys").Append(" (AUTO pick ").Append(ProbeState).Append(')');
            sb.Append("; first start: flybys ").Append(OzLevelPreview.PreparesPending ? "preparing one at a time" : "ready").Append(", shader warm-up ").Append(OzShaderWarm.State);   // item 23
            LastLine = sb.ToString(); LastFps = fps; LastCpuMs = Mathf.Max(cm, cr); LastGpuMs = gm;
            Debug.Log(LastLine);
            ProofReset();
        }

        void Summary()
        {
            if (totFrames < 30) return;
            Debug.Log($"{Tag} the menu was shown {totT / 60f:0.0} min: {totFrames / Mathf.Max(0.001f, totT):0.0} fps on average, worst frame {totWorst:0.0} ms, 1 % low {Low1(totHist, totFrames):0} ms, over 50 ms {totOver50}, " +
                      $"processor {(totNCpu > 0 ? (totCpu / totNCpu).ToString("0.0") + " ms" : "not given")}, graphics chip {(totNGpu > 0 ? (totGpu / totNGpu).ToString("0.0") + " ms" : "not given")}, flyby frames dropped {totDropped}; menu light {(MenuLight ? "ON" : "OFF")}; menu video {OzMenuVideo.Name}, AUTO pick {ProbeState}");
        }
    }

    static class OzMenuPerfExt
    {
        static readonly FieldInfo DefaultIndex = typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        /// <summary>The pipeline asset's default renderer index (0 when it cannot be read).</summary>
        public static int DefaultRendererIndexOrZero(this UniversalRenderPipelineAsset a) => DefaultIndex != null && DefaultIndex.GetValue(a) is int i ? i : 0;
    }
}
