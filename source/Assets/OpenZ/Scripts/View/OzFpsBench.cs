// OpenZ (GPL-3.0) - the frame rate bench of a player build (version 1.2). The land beyond level 16's rim came with one rule: no frame
// rate lost at the widest zoom; frame times in the editor say nothing about that (the editor itself takes most of the frame). This mode
// measures the real game. It is off unless the player is started with -oz-fpsbench, and never runs in the editor's own Play.
//   OpenZ.exe -oz-fpsbench L16 -oz-fpsbench-secs 60 -oz-outskirts on|off -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile <file>
//   optional: -oz-fpsbench-height <m> (one camera height for every run, never above the level's widest), -oz-fpsbench-out <file> (the result file)
//   optional: -oz-fpsbench-focus <x>,<z> (one more view over the map point x, z in metres, z negative south of the north edge, e.g.
//   153,-47; measured after the five as long as each of them, its own VIEW line; the five views and the result's totals stay as without it)
// What it does, start to end:
//  - before the first scene: reads the settings; -oz-outskirts off sets OzL16Outskirts.On false (the land beyond the rim is not made:
//    level 16 as in version 1.1, widest zoom 200 m), on (the default) leaves the level as the game has it; the game keeps running
//    without the window focus (Application.runInBackground, put back at the end); the commander's briefing, idle and ending lines stay
//    off the air (ZodBriefing.Automated), so the level starts at once, with no fly-over;
//  - once the main menu is up: loads the level as the menu's DEPLOY does (one scene load; no clicks, nothing saved); the level starts
//    as always, the computer player with it;
//  - waits LevelSettleS of real time and, on level 16 with the land on, until that land is built (LandWaitS at most);
//  - takes the frame rate limit off (no cap, no vertical sync) until the end; a smoke test in the editor keeps the editor's limit (no
//    heat reading reaches this code there, and its numbers mean nothing). Heat (the rule of OzPerfProbe): at 83 C the bench holds
//    30 frames a second until 76 C and the run is NOT VALID; the game's own heat guard is never overridden;
//  - holds the RTS camera at the level's widest zoom (or -oz-fpsbench-height) over five views: middle, north edge, south edge, the
//    north-west and the south-east corner (the camera's own map limits apply). First a WarmS s look at each (not counted: the first
//    sight of a place), then each view in turn: SettleS s after the move (not counted), then secs / 5 s counted;
//  - per view and for all: average frames a second, the 1 % and 0.1 % low (the frame time 99 % / 99.9 % of the frames stay under, as
//    frames a second), the longest frame, the graphics chip's and the main thread's time of a frame (when the engine's frame timing
//    gives them), draw batches, triangles, the GPU temperature, rain;
//  - writes one "VIEW" line per view and one "FPS BENCH RESULT" line to the log and to <log name>_fpsbench.txt next to the log (with
//    a DATA line for the side-by-side table), then quits by itself: exit code 0 valid, 3 not valid, 4 failed. A run in test mode
//    (oz_testmode = 1: no atmosphere, ambient occlusion, shadows or edge smoothing, so not the player's picture) is NOT VALID.
// Nothing is saved (no PlayerPrefs) and no rule of the game is touched. A smoke test in Play (its numbers mean nothing in the editor):
//   return OpenZ.OzFpsBench.Smoke(10f);     then, about 35 s later:     return OpenZ.OzFpsBench.LastResult;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    [DefaultExecutionOrder(900)]
    public sealed partial class OzFpsBench : MonoBehaviour                                                  // 1.2 item 49: + View/OzFpsBench.WorldLight.cs (the world's light per view)
    {
        public const string Arg = "-oz-fpsbench", ArgSecs = "-oz-fpsbench-secs", ArgOutskirts = "-oz-outskirts", ArgHeight = "-oz-fpsbench-height", ArgOut = "-oz-fpsbench-out", ArgFocus = "-oz-fpsbench-focus";
        public const string ResultHead = "FPS BENCH RESULT";
        public const int ExitValid = 0, ExitNotValid = 3, ExitFailed = 4;
        const string Tag = "[OpenZ FpsBench]";

        /// <summary>True from before the first scene of a bench run to its end (ZodBriefing reads it: no briefing, idle or ending line).</summary>
        public static bool Running { get; private set; }
        public static OzFpsBench I { get; private set; }
        /// <summary>Real seconds: the quiet time after the level started; the first look at each view (not counted); the pause after each
        /// camera move (not counted); the longest waits for the main menu, the level and level 16's land.</summary>
        public static float LevelSettleS = 8f, WarmS = 2f, SettleS = 1f, MenuWaitS = 30f, LoadWaitS = 120f, LandWaitS = 60f;
        /// <summary>A run is VALID with at most this many frames in which something else had put a frame cap back (it is taken off again at once).</summary>
        public static int CapFramesAllowed = 3;
        /// <summary>The last result line (also of a smoke test).</summary>
        public static string LastResult = "";

        sealed class Options
        {
            public string level = "L16", outskirtsWord = "on", outPath, why = "command line";
            public float secs = 60f, height;
            public bool hasFocus; public Vector2 focus;                                               // -oz-fpsbench-focus: one more view over this map point (x, z)
            public bool outskirts = true, load = true, quit = true;
        }

        sealed class View
        {
            public string name; public float x, z; public Vector3 focus;
            public readonly List<float> dt = new List<float>(4096), gpu = new List<float>(4096), main = new List<float>(4096);
            public double batches, tris; public long rframes;
            public int heatFrames, capFrames, pausedFrames, camOffFrames, inputMoves, unfocused, tempMax = -1;
            public float rainMax;
            public ViewData d;
        }

        [Serializable] sealed class ViewData
        {
            public string name; public float focusX, focusZ, secs, avgFps, low1Fps, low01Fps, maxMs, gpuMs = -1f, gpuP95Ms = -1f, mainMs = -1f, batches = -1f, tris = -1f, rainMax;
            public int frames, tempMax = -1, heatFrames, capFrames, pausedFrames, cameraOffFrames, inputMoves, unfocusedFrames;
            public float worldLight = -1f; public int worldNan = -1;                                     // 1.2 item 49: the view's world light (mean of 255, sRGB) and its NaN pixels
        }

        [Serializable] sealed class Data
        {
            public string level, scene, outskirts, land, screen, quality, version, gpuName, cpuName, verdict;
            public float widestM, heightM, secsPerView; public int views; public bool valid;
            public ViewData all; public ViewData[] perView;
        }

        static Options pending;
        static bool bgBefore, outskirtsBefore;

        Options opt; OzLevelDef def; ZGame game; RtsCamera rts;
        readonly List<View> views = new List<View>();
        View hold, cur;
        float height, widest, deadline = float.MaxValue, levelAfter, nextTipReset;
        int baseViews;                                                                                  // the five views of the totals (-oz-fpsbench-focus adds one after them)
        string phase = "start", landWord = "-";
        bool done, uncapped, restored, hot;
        int capBefore = 60, targetBefore = 60, vsyncBefore;
        ProfilerRecorder batchRec, triRec;
        readonly FrameTiming[] ft = new FrameTiming[1];

        // ------------------------------------------------------------------------------------------------ boot (command line only)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Running = false; I = null; pending = null; LastResult = ""; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor) return;
            var a = Environment.GetCommandLineArgs();
            bool asked = false;
            for (int i = 0; i < a.Length; i++) if (a[i].Equals(Arg, StringComparison.OrdinalIgnoreCase)) { asked = true; break; }
            if (!asked) return;                                                                        // normal play: nothing else runs, nothing is made
            var o = new Options();
            string lv = Val(a, Arg); if (!string.IsNullOrEmpty(lv)) o.level = lv.Trim().ToUpperInvariant();
            o.secs = Mathf.Clamp(Num(Val(a, ArgSecs), 60f), 10f, 600f);
            o.height = Mathf.Max(0f, Num(Val(a, ArgHeight), 0f));
            o.hasFocus = FocusArg(a, out o.focus);
            o.outPath = Val(a, ArgOut);
            string os = Val(a, ArgOutskirts);
            if (os != null)
            {
                string w = os.Trim().ToLowerInvariant();
                if (w == "off" || w == "0" || w == "false") { o.outskirts = false; o.outskirtsWord = "off"; }
                else if (w == "on" || w == "1" || w == "true") { o.outskirts = true; o.outskirtsWord = "on"; }
                else Debug.LogWarning($"{Tag} {ArgOutskirts} '{os}' is not on / off: left on");
            }
            Prepare(o);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Host()
        {
            if (pending == null || I) return;
            var go = new GameObject("OzFpsBench");
            DontDestroyOnLoad(go);
            I = go.AddComponent<OzFpsBench>();
            I.opt = pending; pending = null;
        }

        static void Prepare(Options o)
        {
            pending = o; Running = true;
            outskirtsBefore = OzL16Outskirts.On; OzL16Outskirts.On = o.outskirts;                       // read by the level when it starts (OzL16Outskirts.TryAttach)
            bgBefore = Application.runInBackground; Application.runInBackground = true;
            Debug.Log($"{Tag} ON ({o.why}): level {o.level}, {o.secs:0} s measured, outskirts {o.outskirtsWord}, height {(o.height > 0f ? o.height.ToString("0") + " m" : "the widest")}{(o.hasFocus ? ", one more view over " + FocusText(o.focus) : "")}, " +
                      $"the game runs without the window focus, no briefing / fly-over, no frame rate limit while measuring");
        }

        /// <summary>Play check: the bench on the running level (no scene load, no quit; the land as the level has it). Editor numbers mean
        /// nothing: this only proves the steps and the result lines. Read OzFpsBench.LastResult afterwards.</summary>
        public static string Smoke(float secs = 10f)
        {
            if (!Application.isPlaying) return "start Play on a level first";
            if (I) return "a bench is running already";
            var g = ZGame.I;
            if (!g || g.World == null) return "no level is running";
            Prepare(new Options { level = g.level, secs = Mathf.Clamp(secs, 5f, 600f), outskirts = OzL16Outskirts.On, outskirtsWord = OzL16Outskirts.On ? "on" : "off", load = false, quit = false, why = "smoke test" });
            Host();
            return $"bench started on {g.level}: about {LevelSettleS + 5f * (WarmS + SettleS) + Mathf.Clamp(secs, 5f, 600f):0} s, then read OpenZ.OzFpsBench.LastResult";
        }

        /// <summary>Play check of -oz-fpsbench-focus: the smoke test with one more view over (focusX, focusZ), at a camera height (0 = the
        /// widest). Editor numbers mean nothing: this only proves the steps and the result lines.</summary>
        public static string Smoke(float secs, float focusX, float focusZ, float height = 0f)
        {
            if (!Application.isPlaying) return "start Play on a level first";
            if (I) return "a bench is running already";
            var g = ZGame.I;
            if (!g || g.World == null) return "no level is running";
            float s = Mathf.Clamp(secs, 5f, 600f);
            Prepare(new Options { level = g.level, secs = s, height = Mathf.Max(0f, height), hasFocus = true, focus = new Vector2(focusX, focusZ), outskirts = OzL16Outskirts.On, outskirtsWord = OzL16Outskirts.On ? "on" : "off", load = false, quit = false, why = "smoke test" });
            Host();
            return $"bench started on {g.level} with one more view over {FocusText(new Vector2(focusX, focusZ))}: about {LevelSettleS + 6f * (WarmS + SettleS) + s * 6f / 5f:0} s, then read OpenZ.OzFpsBench.LastResult";
        }

        /// <summary>-oz-fpsbench-focus x,z (metres): one more view. False: not asked, or not two numbers (then said in the log).</summary>
        static bool FocusArg(string[] a, out Vector2 focus)
        {
            focus = Vector2.zero;
            for (int i = 0; i < a.Length - 1; i++)
            {
                if (!a[i].Equals(ArgFocus, StringComparison.OrdinalIgnoreCase)) continue;
                var p = (a[i + 1] ?? "").Split(','); var ci = System.Globalization.CultureInfo.InvariantCulture; var ns = System.Globalization.NumberStyles.Float;
                if (p.Length == 2 && float.TryParse(p[0].Trim(), ns, ci, out float x) && float.TryParse(p[1].Trim(), ns, ci, out float z)) { focus = new Vector2(x, z); return true; }
                Debug.LogWarning($"{Tag} {ArgFocus} '{a[i + 1]}' is not x,z (two numbers, metres): no extra view");
                return false;
            }
            return false;
        }

        static string FocusText(Vector2 f) => f.x.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "," + f.y.ToString("0", System.Globalization.CultureInfo.InvariantCulture);

        static string Val(string[] a, string name)
        {
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i].Equals(name, StringComparison.OrdinalIgnoreCase) && !a[i + 1].StartsWith("-", StringComparison.Ordinal)) return a[i + 1];
            return null;
        }

        static float Num(string s, float def) =>
            !string.IsNullOrEmpty(s) && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : def;

        /// <summary>The words that make an argument's value secret (its name holds one of them, any case).</summary>
        static readonly string[] SecretWords = { "token", "session", "key", "password", "auth", "secret", "license" };

        static bool Secret(string name)
        {
            string n = name.ToLowerInvariant();
            foreach (var w in SecretWords) if (n.Contains(w)) return true;
            return false;
        }

        /// <summary>The command line for the result file (the program name left out): the value after an argument whose name holds a
        /// secret word is written as &lt;masked&gt;, and so is the value of such an argument written -name=value. The editor's own launch
        /// line carries such values; the result file never shows them.</summary>
        static string MaskedCommandLine(string[] a)
        {
            var parts = new List<string>();
            for (int i = 1; i < a.Length; i++)
            {
                string s = a[i] ?? "";
                bool flag = s.StartsWith("-", StringComparison.Ordinal);
                int eq = s.IndexOf('=');
                if (flag && eq > 0) { parts.Add(Secret(s.Substring(0, eq)) ? s.Substring(0, eq + 1) + "<masked>" : s); continue; }
                parts.Add(s);
                if (flag && Secret(s) && i + 1 < a.Length) { parts.Add("<masked>"); i++; }
            }
            return string.Join(" ", parts);
        }

        // ------------------------------------------------------------------------------------------------ the run
        void Start() { StartCoroutine(Run()); }

        IEnumerator Run()
        {
            deadline = Time.realtimeSinceStartup + MenuWaitS + LoadWaitS + LandWaitS + LevelSettleS + 30f;
            foreach (var d in OzLevelDef.All)
                if (d.Id.Equals(opt.level, StringComparison.OrdinalIgnoreCase) || d.Scene.Equals(opt.level, StringComparison.OrdinalIgnoreCase)) def = d;
            if (def == null) { Fail($"unknown level '{opt.level}' (one of L02, L07, L16, L17)"); yield break; }
            phase = "load";
            if (opt.load && !LevelRunning())
            {
                float t0 = Time.realtimeSinceStartup;
                while (!IsMenu() && Time.realtimeSinceStartup - t0 < MenuWaitS) { if (done) yield break; yield return null; }
                bool menu = IsMenu();
                if (menu) yield return new WaitForSecondsRealtime(1f);                                    // the menu's own first frames
                if (done) yield break;
                Debug.Log($"{Tag} loading {def.Scene} ({(menu ? "from the main menu" : "the main menu did not come in " + MenuWaitS.ToString("0") + " s")})");
                if (SceneManager.LoadSceneAsync(def.Scene, LoadSceneMode.Single) == null) { Fail("the scene " + def.Scene + " could not be loaded (not in the build?)"); yield break; }
            }
            float tl = Time.realtimeSinceStartup;
            while (!LevelRunning() && Time.realtimeSinceStartup - tl < LoadWaitS) { if (done) yield break; yield return null; }
            if (!LevelRunning()) { Fail($"level {def.Id} was not running {LoadWaitS:0} s after the load"); yield break; }
            game = ZGame.I; rts = FindFirstObjectByType<RtsCamera>();
            if (!rts) { Fail("no RTS camera on the level"); yield break; }
            levelAfter = Time.realtimeSinceStartup - tl;
            phase = "settle";
            bool landWanted = def.Id == OzL16Outskirts.Level && OzL16Outskirts.On;
            float t1 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t1 < LevelSettleS || (landWanted && !LandBuilt() && Time.realtimeSinceStartup - t1 < LandWaitS))
            {
                if (done) yield break;
                if (!LevelOk()) { Fail("the level ended before the measure"); yield break; }
                yield return null;
            }
            landWord = def.Id != OzL16Outskirts.Level ? "-" : !OzL16Outskirts.On ? "not made (outskirts off)"
                     : LandBuilt() ? $"built ({OzL16Outskirts.BuildMs:0} ms of work over {OzL16Outskirts.BuildFrames} frames)" : $"NOT BUILT after {LandWaitS:0} s";
            widest = rts.maxHeight;
            height = opt.height > 0f ? Mathf.Clamp(opt.height, rts.minHeight, widest) : widest;
            var map = game.World.Map; float W = map.WidthPix / 8f, H = map.HeightPix / 8f;               // the views of the editor's edge shots (OzV12EdgeShots)
            views.Clear();
            AddView("middle", W * 0.5f, -H * 0.5f); AddView("n", W * 0.5f, 0f); AddView("s", W * 0.5f, -H); AddView("nw", 0f, 0f); AddView("se", W, -H);
            baseViews = views.Count;                                                                    // the totals and the seconds per view are of these five
            if (opt.hasFocus) AddView("focus", opt.focus.x, opt.focus.y);                              // -oz-fpsbench-focus: one more, after them, as long as each of them
            float per = opt.secs / baseViews;
            Debug.Log($"{Tag} level {def.Id} running after {levelAfter:0.0} s; land {landWord}; widest zoom {widest:0} m, measured at {height:0} m" +
                      $"{(opt.height > 0f && Mathf.Abs(opt.height - height) > 0.01f ? " (asked " + opt.height.ToString("0") + " m)" : "")}; {views.Count} views x {per:0.0} s; " +
                      $"{Screen.width}x{Screen.height} {Screen.fullScreenMode}, graphics {OzQuality.Name}{(GameBoot.TestMode ? ", TEST MODE ON (oz_testmode = 1): this run will be NOT VALID" : "")}");
            Uncap();
            deadline = Time.realtimeSinceStartup + views.Count * (WarmS + SettleS + per) + 60f;
            phase = "warm";
            foreach (var v in views)
            {
                Hold(v);
                float t = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t < WarmS) { if (done) yield break; if (!LevelOk()) { Fail("the level ended during the first look"); yield break; } yield return null; }
            }
            foreach (var v in views)
            {
                phase = "settle " + v.name; Hold(v);
                float t = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t < SettleS) { if (done) yield break; if (!LevelOk()) { Fail("the level ended during the measure"); yield break; } yield return null; }
                yield return LightLook(v); if (done) yield break;                                          // 1.2 item 49: the world's light, read before the measure (never counted)
                phase = "measure " + v.name; cur = v;
                t = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t < per) { if (done) yield break; if (!LevelOk()) { cur = null; Fail("the level ended during the measure"); yield break; } yield return null; }
                cur = null;
                v.d = Stats(v.name, v.dt, v.gpu, v.main);
                v.d.focusX = v.focus.x; v.d.focusZ = v.focus.z;
                v.d.batches = v.rframes > 0 ? (float)(v.batches / v.rframes) : -1f; v.d.tris = v.rframes > 0 ? (float)(v.tris / v.rframes) : -1f;
                v.d.tempMax = v.tempMax; v.d.rainMax = v.rainMax; v.d.heatFrames = v.heatFrames; v.d.capFrames = v.capFrames; v.d.pausedFrames = v.pausedFrames;
                v.d.cameraOffFrames = v.camOffFrames; v.d.inputMoves = v.inputMoves; v.d.unfocusedFrames = v.unfocused;
                LightInto(v.d);                                                                            // 1.2 item 49
                Debug.Log(ViewLine(v));
            }
            phase = "result";
            Finish();
        }

        void AddView(string name, float x, float z) => views.Add(new View { name = name, x = x, z = z });

        void Hold(View v)
        {
            if (!rts) return;
            rts.follow = null;
            rts.focus = new Vector3(v.x, 0f, v.z);
            rts.SetHeightImmediate(height);                                                             // Place(): the camera's own map limits clamp the focus
            v.focus = rts.focus;
            hold = v;
        }

        bool LevelRunning()
        {
            var g = ZGame.I;
            return g && g.World != null && g.enabled && def != null && g.level == def.Id && !ZodBriefing.BriefingActive;
        }

        bool LevelOk() => game && ZGame.I == game && game.World != null && rts;

        static bool LandBuilt() => OzL16Outskirts.I != null && OzL16Outskirts.BuildMs > 0f;           // BuildMs is set when the land is shown (0 at each level start)

        static bool IsMenu()
        {
            string n = SceneManager.GetActiveScene().name;
            foreach (var m in OzScenes.MenuNames) if (n == m) return true;
            return false;
        }

        // ------------------------------------------------------------------------------------------------ every frame
        void Update()
        {
            if (done) return;
            if (Time.realtimeSinceStartup > deadline) { Fail("timed out (" + phase + ")"); return; }
            if (Time.realtimeSinceStartup >= nextTipReset)
            {   // the HUD's one-time "running slow?" plate is saved as shown once per install: a long bench must not use it up
                nextTipReset = Time.realtimeSinceStartup + 1f;
                if (OpenZHud.I) OpenZHud.I.SlowTipProofReset();
            }
            if (!uncapped) return;
            int t = OzPerfProbe.TempSource != null ? OzPerfProbe.TempSource() : HeatGuardRuntime.GpuTempC;
            bool guard = HeatGuardRuntime.HeldFps > 0;                                                   // the game's own heat guard holds a cap: never overridden
            if (!hot && t >= OzPerfProbe.HotC) { hot = true; Debug.Log($"{Tag} GPU {t} C: holding {OzPerfProbe.HeatFps} frames a second until {OzPerfProbe.CoolC} C (this run is NOT VALID)"); }
            else if (hot && t >= 0 && t < OzPerfProbe.CoolC) { hot = false; if (!guard) Application.targetFrameRate = Application.isEditor ? targetBefore : -1; Debug.Log($"{Tag} GPU {t} C: no frame rate limit again"); }
            bool capNow = false;
            if (hot) { if (!guard && Application.targetFrameRate != OzPerfProbe.HeatFps) Application.targetFrameRate = OzPerfProbe.HeatFps; }
            else if (!guard && !Application.isEditor && (Application.targetFrameRate > 0 || QualitySettings.vSyncCount > 0))
            {   // something put a cap back: off again at once, and the frame is counted (a smoke test in the editor keeps its limit: Uncap)
                capNow = true;
                Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
            }
            var v = cur;
            if (v == null) return;
            v.dt.Add(Time.unscaledDeltaTime * 1000f);
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, ft) > 0)
            {
                if (ft[0].gpuFrameTime > 0.0) v.gpu.Add((float)ft[0].gpuFrameTime);
                if (ft[0].cpuMainThreadFrameTime > 0.0) v.main.Add((float)ft[0].cpuMainThreadFrameTime);
            }
            if (batchRec.Valid && triRec.Valid) { v.batches += batchRec.LastValue; v.tris += triRec.LastValue; v.rframes++; }
            if (hot || guard) v.heatFrames++;
            if (capNow) v.capFrames++;
            if (!game || game.paused || !game.enabled || Time.timeScale <= 0f) v.pausedFrames++;
            if (!Application.isFocused) v.unfocused++;
            if (t > v.tempMax) v.tempMax = t;
            var jw = OzJungleWeather.I; if (jw && jw.Rain > v.rainMax) v.rainMax = jw.Rain;
        }

        void LateUpdate()
        {
            var v = hold;
            if (done || v == null || !rts) return;
            if (!rts.enabled) { if (cur != null) cur.camOffFrames++; return; }
            bool heightOff = Mathf.Abs(rts.height - height) > 0.01f;
            if (cur != null && (heightOff || rts.HoldTime > 0f || rts.follow)) cur.inputMoves++;     // a key, the screen edge or the wheel moved the view (the window had the focus)
            rts.follow = null;
            rts.focus = new Vector3(v.x, 0f, v.z);                                                       // the asked point again, every frame: the camera's own limits clamp it
            if (heightOff) rts.SetHeightImmediate(height); else rts.Place();
            v.focus = rts.focus;
        }

        // ------------------------------------------------------------------------------------------------ cap, stats, result
        void Uncap()
        {
            if (uncapped) return;
            capBefore = GameBoot.CapFps; targetBefore = Application.targetFrameRate; vsyncBefore = QualitySettings.vSyncCount;
            bool editor = Application.isEditor;                                                           // a smoke test: the editor keeps its limit (no heat reading reaches this code there)
            if (!editor)
            {
                GameBoot.CapFps = -1;                                                                     // the heat guard comes back to "no limit" when it lets go
                Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
            }
            batchRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            triRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            uncapped = true;
            Debug.Log($"{Tag} {(editor ? "editor smoke test: the editor's frame rate limit stays" : "no frame rate limit")} (was cap {capBefore}, target {targetBefore}, vSync {vsyncBefore}); render counters {(batchRec.Valid ? "on" : "not given by this player")}");
        }

        void Restore()
        {
            if (restored) return;
            restored = true;
            if (uncapped)
            {
                GameBoot.CapFps = capBefore;
                Application.targetFrameRate = HeatGuardRuntime.HeldFps > 0 ? HeatGuardRuntime.HeldFps : targetBefore;   // the heat guard's own cap stays while it holds
                QualitySettings.vSyncCount = vsyncBefore;
                batchRec.Dispose(); triRec.Dispose();
            }
            Application.runInBackground = bgBefore;
            OzL16Outskirts.On = outskirtsBefore;                                                          // for the next level of this session (a smoke test)
            Running = false;
        }

        static float Pct(List<float> sorted, float p) => sorted.Count == 0 ? -1f : sorted[Mathf.Clamp(Mathf.CeilToInt(p * sorted.Count) - 1, 0, sorted.Count - 1)];

        static float Avg(List<float> l) { if (l.Count == 0) return -1f; double s = 0; foreach (var x in l) s += x; return (float)(s / l.Count); }

        static ViewData Stats(string name, List<float> dt, List<float> gpu, List<float> main)
        {
            var d = new ViewData { name = name, frames = dt.Count };
            if (dt.Count == 0) return d;
            double sum = 0; foreach (var x in dt) sum += x;
            var s = new List<float>(dt); s.Sort();
            float p99 = Pct(s, 0.99f), p999 = Pct(s, 0.999f);
            d.secs = (float)(sum / 1000.0);
            d.avgFps = sum > 0 ? (float)(dt.Count * 1000.0 / sum) : 0f;
            d.low1Fps = p99 > 0f ? 1000f / p99 : 0f;
            d.low01Fps = p999 > 0f ? 1000f / p999 : 0f;
            d.maxMs = s[s.Count - 1];
            if (gpu.Count > 0) { var g = new List<float>(gpu); g.Sort(); d.gpuMs = Avg(gpu); d.gpuP95Ms = Pct(g, 0.95f); }
            if (main.Count > 0) d.mainMs = Avg(main);
            return d;
        }

        static string Ms(float ms) => ms > 0f ? ms.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " ms" : "n/a";

        string Flags(ViewData d)
        {
            var f = new List<string>();
            if (d.heatFrames > 0) f.Add($"HEAT {d.heatFrames} frames");
            if (d.capFrames > 0) f.Add($"cap put back {d.capFrames} frames");
            if (d.pausedFrames > 0) f.Add($"paused {d.pausedFrames} frames");
            if (d.cameraOffFrames > 0) f.Add($"camera taken {d.cameraOffFrames} frames");
            if (d.inputMoves > 0) f.Add($"view moved by input {d.inputMoves} times");
            if (d.rainMax > 0.01f) f.Add($"rain {d.rainMax:0.00}");
            LightFlag(d, f);                                                                               // 1.2 item 49: WORLD BLACK
            return f.Count == 0 ? "OK" : string.Join(", ", f);
        }

        string Outskirts => def != null && def.Id == OzL16Outskirts.Level ? opt.outskirtsWord : "-";

        string ViewLine(View v)
        {
            var d = v.d; var ci = System.Globalization.CultureInfo.InvariantCulture;
            return $"{Tag} VIEW {v.name} | {def.Id} outskirts {Outskirts} | focus {d.focusX.ToString("0", ci)},{d.focusZ.ToString("0", ci)} height {height.ToString("0", ci)} m | " +
                   $"{d.secs.ToString("0.0", ci)} s {d.frames} frames | avg {d.avgFps.ToString("0.0", ci)} fps | 1% low {d.low1Fps.ToString("0.0", ci)} | 0.1% low {d.low01Fps.ToString("0.0", ci)} | " +
                   $"max {d.maxMs.ToString("0.0", ci)} ms | GPU {Ms(d.gpuMs)}{(d.gpuP95Ms > 0f ? " (p95 " + Ms(d.gpuP95Ms) + ")" : "")} | main thread {Ms(d.mainMs)} | " +
                   $"batches {(d.batches >= 0f ? d.batches.ToString("0", ci) : "n/a")} tris {(d.tris >= 0f ? (d.tris / 1e6f).ToString("0.00", ci) + " M" : "n/a")} | " +
                   $"GPU temp {(d.tempMax >= 0 ? d.tempMax + " C" : "n/a")} | {LightText(d)} | {Flags(d)}";             // 1.2 item 49: the world's light
        }

        void Finish()
        {
            var all = new List<float>(); var allG = new List<float>(); var allM = new List<float>();
            double b = 0, tr = 0; long rf = 0; View slowest = null;
            int nb = baseViews > 0 ? Mathf.Min(baseViews, views.Count) : views.Count;                   // -oz-fpsbench-focus's view is not in the totals
            for (int vi = 0; vi < nb; vi++)
            {
                var v = views[vi];
                all.AddRange(v.dt); allG.AddRange(v.gpu); allM.AddRange(v.main); b += v.batches; tr += v.tris; rf += v.rframes;
                if (v.d != null && v.d.frames > 0 && (slowest == null || v.d.avgFps < slowest.d.avgFps)) slowest = v;
            }
            var a = Stats("all", all, allG, allM);
            a.batches = rf > 0 ? (float)(b / rf) : -1f; a.tris = rf > 0 ? (float)(tr / rf) : -1f;
            foreach (var v in views) if (v.d != null)
            {
                a.heatFrames += v.d.heatFrames; a.capFrames += v.d.capFrames; a.pausedFrames += v.d.pausedFrames; a.cameraOffFrames += v.d.cameraOffFrames;
                a.inputMoves += v.d.inputMoves; a.unfocusedFrames += v.d.unfocusedFrames; a.tempMax = Mathf.Max(a.tempMax, v.d.tempMax); a.rainMax = Mathf.Max(a.rainMax, v.d.rainMax);
            }
            var why = new List<string>();
            if (a.frames == 0) why.Add("no frames measured");
            if (a.heatFrames > 0) why.Add($"heat hold {a.heatFrames} frames (cool down and run again)");
            if (a.capFrames > CapFramesAllowed) why.Add($"a frame cap was put back {a.capFrames} times");
            if (a.pausedFrames > 0) why.Add($"the game was paused {a.pausedFrames} frames");
            if (a.cameraOffFrames > 0) why.Add($"the camera was taken {a.cameraOffFrames} frames");
            LightVerdict(why, a);                                                                          // 1.2 item 49: a black view, or one not read, is NOT VALID
            if (def.Id == OzL16Outskirts.Level && OzL16Outskirts.On && !LandBuilt()) why.Add("the land beyond the rim was not built");
            if (GameBoot.TestMode) why.Add("test mode is on (oz_testmode = 1: no atmosphere, ambient occlusion, shadows or edge smoothing; not the player's picture)");
            if (Application.isEditor) why.Add("an editor smoke test (the editor's frame rate limit stays on; the numbers mean nothing)");
            bool valid = why.Count == 0;
            string verdict = valid ? "VALID" : "NOT VALID (" + string.Join("; ", why) + ")";
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            float per = nb > 0 ? opt.secs / nb : 0f;
            string line = $"{ResultHead} {def.Id} outskirts {Outskirts} | {Screen.width}x{Screen.height} {Screen.fullScreenMode} | graphics {OzQuality.Name} | " +
                          $"widest {widest.ToString("0", ci)} m, measured at {height.ToString("0", ci)} m | {nb} views x {per.ToString("0.0", ci)} s, {a.frames} frames | " +
                          $"avg {a.avgFps.ToString("0.0", ci)} fps | 1% low {a.low1Fps.ToString("0.0", ci)} | 0.1% low {a.low01Fps.ToString("0.0", ci)} | max {a.maxMs.ToString("0.0", ci)} ms | " +
                          $"GPU {Ms(a.gpuMs)} | main thread {Ms(a.mainMs)} | slowest view {(slowest != null ? slowest.name + " " + slowest.d.avgFps.ToString("0.0", ci) + " fps" : "-")}{ExtraView(nb)} | " +
                          $"GPU temp max {(a.tempMax >= 0 ? a.tempMax + " C" : "n/a")} | {verdict}";
            var data = new Data
            {
                level = def.Id, scene = def.Scene, outskirts = Outskirts, land = landWord, screen = $"{Screen.width}x{Screen.height} {Screen.fullScreenMode}", quality = OzQuality.Name,
                version = OzVersion.Current + " " + OzVersion.Label, gpuName = SystemInfo.graphicsDeviceName, cpuName = SystemInfo.processorType,
                widestM = widest, heightM = height, secsPerView = per, views = nb, valid = valid, verdict = verdict, all = a,
                perView = views.ConvertAll(v => v.d ?? new ViewData { name = v.name }).ToArray(),
            };
            End(line, valid ? ExitValid : ExitNotValid, data, true);
        }

        /// <summary>The result line's part for -oz-fpsbench-focus's view (empty without it): not in the totals before it.</summary>
        string ExtraView(int nb)
        {
            if (views.Count <= nb || views[nb].d == null) return "";
            var d = views[nb].d; var ci = System.Globalization.CultureInfo.InvariantCulture;
            return $" | one more view {views[nb].name} (not in the totals) focus {d.focusX.ToString("0", ci)},{d.focusZ.ToString("0", ci)}: avg {d.avgFps.ToString("0.0", ci)} fps, 1% low {d.low1Fps.ToString("0.0", ci)}, " +
                   $"0.1% low {d.low01Fps.ToString("0.0", ci)}, max {d.maxMs.ToString("0.0", ci)} ms, GPU {Ms(d.gpuMs)}, tris {(d.tris >= 0f ? (d.tris / 1e6f).ToString("0.00", ci) + " M" : "n/a")}";
        }

        string Head => $"{ResultHead} {(def != null ? def.Id : opt != null ? opt.level : "?")} outskirts {(opt == null ? "?" : def == null || def.Id == OzL16Outskirts.Level ? opt.outskirtsWord : "-")}";

        void Fail(string why) => End($"{Head} | FAILED: {why}", ExitFailed, null, true);

        void End(string line, int code, Data data, bool quit)
        {
            if (done) return;
            done = true; cur = null; hold = null;
            LastResult = line;
            Debug.Log(line);
            Restore();
            WriteFile(line, data);
            if (!quit) return;
            if (opt == null || opt.quit) StartCoroutine(QuitSoon(code));
            else Destroy(gameObject, 0.1f);
        }

        IEnumerator QuitSoon(int code)
        {
            yield return new WaitForSecondsRealtime(0.5f);                                                 // the log line is flushed
            Debug.Log($"{Tag} quitting (exit code {code})");
            OzQuit.ExitCode = code;                                                                          // 1.5 kit quit_loading_crash_r5: the Windows end (View/OzQuit.cs step 6) leaves with it
            Application.Quit(code);
        }

        void WriteFile(string line, Data data)
        {
            string path = null;
            try
            {
                if (opt != null && !string.IsNullOrEmpty(opt.outPath)) path = Path.GetFullPath(opt.outPath);
                else if (Application.isEditor) path = OzRelease.Milestones("fps_bench") + "/OpenZ_fpsbench_smoke.txt";   // a smoke test: the job's own records
                else
                {
                    string log = Application.consoleLogPath;
                    path = !string.IsNullOrEmpty(log)
                        ? Path.Combine(Path.GetDirectoryName(log) ?? ".", Path.GetFileNameWithoutExtension(log) + "_fpsbench.txt")
                        : Path.Combine(Application.persistentDataPath, "OpenZ_fpsbench.txt");
                }
                string dir = Path.GetDirectoryName(path); if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var sb = new StringBuilder(4096);
                sb.Append("OpenZ frame rate bench\n");
                sb.Append("version: ").Append(OzVersion.Current).Append(' ').Append(OzVersion.Label).Append(" (").Append(Application.version).Append(")\n");
                sb.Append("command line: ").Append(MaskedCommandLine(Environment.GetCommandLineArgs())).Append('\n');
                sb.Append("screen: ").Append(Screen.width).Append('x').Append(Screen.height).Append(' ').Append(Screen.fullScreenMode).Append(", graphics ").Append(OzQuality.Name)
                  .Append(", the player's frame rate limit ").Append(capBefore).Append(Application.isEditor ? " (kept: an editor smoke test)" : " (off while measuring)")
                  .Append(GameBoot.TestMode ? ", TEST MODE ON (oz_testmode = 1)" : "").Append('\n');
                sb.Append("machine: ").Append(SystemInfo.graphicsDeviceName).Append(" | ").Append(SystemInfo.processorType).Append(" (").Append(SystemInfo.processorCount).Append(" threads)\n");
                sb.Append("level: ").Append(def != null ? def.Id + " (" + def.Scene + ")" : opt != null ? opt.level : "?").Append(", outskirts ").Append(opt != null ? opt.outskirtsWord : "?")
                  .Append(", land ").Append(landWord).Append(", running after ").Append(levelAfter.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(" s\n");
                foreach (var v in views) if (v.d != null) sb.Append(ViewLine(v)).Append('\n');
                sb.Append(line).Append('\n');
                if (data != null) sb.Append("DATA ").Append(JsonUtility.ToJson(data)).Append('\n');
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                Debug.Log($"{Tag} result file {path}");
            }
            catch (Exception e) { Debug.LogWarning($"{Tag} the result file could not be written ({path ?? "?"}: {e.GetType().Name} {e.Message}); the lines above are the result"); }
        }

        void OnApplicationQuit()
        {
            if (!done) End($"{Head} | STOPPED: the game was closed before the end ({phase})", ExitFailed, null, false);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            Restore();
        }
    }
}
