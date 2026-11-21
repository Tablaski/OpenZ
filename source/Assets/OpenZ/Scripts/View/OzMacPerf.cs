// OpenZ (GPL-3.0) - 1.5 kits mac_perf_1_5 and mac_perf_1_5b (GitHub issue 4: the Mac build ran at about 4 frames a second on a 5K iMac
// and at most 8 on an Apple-chip MacBook Pro in 1.3; the reporter's 1.4 Player.log of 16 Nov 2026, an Intel iMac 2020 with an AMD Radeon
// Pro 5300 (4 GB) at 5120x2880: the menu at 36-42 fps, level 2 at about one frame a second, its first drawn frame 27.6 s). Mac player only:
// in the Windows and Linux players and in the editor this whole file is left out by the compiler (the #if below), nothing of it is built there.
//
// mac_perf_1_5 (10 Nov): the Mac player draws at the screen's own pixels (Retina support is on: ProjectSettings macRetinaSupport 1;
// RESOLUTION is NATIVE by default) and GRAPHICS QUALITY never lowered the drawn size (render scale 1.0 on every preset, OzQuality.cs), so the
// 3D picture is drawn within a pixel budget through the URP render scale of the preset in force; the interface (screen-space overlay
// canvases) is still drawn at the screen's own size, so its text stays sharp. A lower RESOLUTION step or a smaller window is measured as it is.
//
// mac_perf_1_5b (16 Nov, from the reporter's log; the kit's README ranks the causes with the log's line numbers):
//  1. THE PICTURE: at most 1920x1080 worth of pixels on EVERY preset (it was 2560x1440 on HIGH and MEDIUM); his graphics chip took 94-164 ms
//     a frame at 5120x2880. Above the budget MSAA is off on every preset (the camera's own SMAA edge smoothing stays): a 5K picture with 4x
//     MSAA hardly fits next to the desktop in 4 GB, and a preset step no longer changes the sample count, so it no longer makes the Mac
//     build every shader state anew. A screen within the budget (1920x1080, 1920x1200) keeps the preset's own MSAA and the full picture.
//  2. FIRST START on a Mac without an Apple chip (Intel or AMD graphics): GRAPHICS QUALITY LOW and the light menu flybys (1920x1080, 30 fps,
//     the copies slower Windows PCs get) are chosen and saved once, only where the player has no value of his own. OPTIONS change them as
//     always; the game never chooses again (oz_mac_first_v1). Apple chips: nothing is chosen.
//  3. SHADERS: the level's own shader list warms on its loading screen (View/OzLevelWarm.cs, the bar shows it) for up to WarmMaxSeconds on a
//     Mac (30 s elsewhere): the Mac's graphics driver builds each shader the first time it is used, which on his iMac held the level's first
//     drawn frame for 27.6 s.
//  4. MENU FLYBYS: a flyby that plays but has shown no decoded picture after NoFrameSec gives up for this run: its level shows its still, as
//     after a video error, and the menu moves on (UI/OzLevelPreview.cs MacGiveUp).
//  5. Player.log: "[OpenZ Mac]" lines at the start, after every change of the picture, at each level start (what the loading screen warmed,
//     the picture, graphics memory) and once a minute in a level, so the next log shows what helped and what did not.
// Nothing else changes: the presets' files, the Options rows, the rules, the Windows and Linux players.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ
{
    /// <summary>1.5 kits mac_perf_1_5 / mac_perf_1_5b: the Mac player's drawn 3D size within a pixel budget, MSAA off above it, the first start
    /// of an Intel / AMD Mac, the loading screen's shader warm-up time, the menu flybys that never show a picture (see the head of this file).
    /// One object for the whole session; the log lines "[OpenZ Mac] ..." in Player.log name every change.</summary>
    public sealed class OzMacPerf : MonoBehaviour
    {
        public const string Tag = "[OpenZ Mac]";

        // ------------------------------------------------------------------ 1. the picture
        /// <summary>The most pixels of the drawn 3D picture per GRAPHICS QUALITY (HIGH, MEDIUM, LOW) and their names for the log
        /// (mac_perf_1_5b: 1920x1080 on all three; mac_perf_1_5 had 2560x1440 on HIGH and MEDIUM).</summary>
        public static readonly long[] Budget = { 1920L * 1080L, 1920L * 1080L, 1920L * 1080L };
        public static readonly string[] BudgetName = { "1920x1080", "1920x1080", "1920x1080" };
        /// <summary>A screen this much over the budget keeps the full picture; the scale never goes under MinScale.</summary>
        public const float Slack = 1.12f, MinScale = 0.3f;
        /// <summary>The render scale and the MSAA samples set last (1 = the full picture / no MSAA) and the last picture line (for checks).</summary>
        public static float Scale = 1f;
        public static int Msaa = 1;
        public static string Line = "";

        readonly Dictionary<UniversalRenderPipelineAsset, float> own = new Dictionary<UniversalRenderPipelineAsset, float>();
        readonly Dictionary<UniversalRenderPipelineAsset, int> ownMsaa = new Dictionary<UniversalRenderPipelineAsset, int>();
        UniversalRenderPipelineAsset seen; int seenW = -1, seenH = -1, seenLevel = -1, seenMsaa = -1;
        bool seenReduceMsaa;

        /// <summary>The render scale that keeps a w x h picture within the budget: 1 when it fits (with the slack), else the square
        /// root of budget / pixels (both sides by the same factor), never under MinScale.</summary>
        public static float For(int w, int h, long budget)
        {
            long px = (long)w * h;
            if (w <= 0 || h <= 0 || px <= budget * Slack) return 1f;
            return Mathf.Clamp((float)System.Math.Sqrt((double)budget / px), MinScale, 1f);
        }

        /// <summary>The MSAA samples for a picture: off (1) when the screen is over the budget, else the preset's own.</summary>
        public static int MsaaFor(float fit, int presetMsaa) => fit < 1f ? 1 : Mathf.Max(1, presetMsaa);

        static string MsaaName(int m) => m > 1 ? m + "x" : "off";

        // ------------------------------------------------------------------ 2. the first start
        /// <summary>0 / not there: not looked at yet; 1: an Intel / AMD Mac's first start chose (or kept) its settings; 2: an Apple chip.</summary>
        public const string FirstKey = "oz_mac_first_v1";
        /// <summary>What this start's first-start step did (for the log and the checks).</summary>
        public static string FirstLine { get; private set; } = "";

        /// <summary>The graphics chip is Apple's own (M1 and later; also under Rosetta).</summary>
        public static bool AppleChip(string vendor, string chip)
        {
            string v = (vendor ?? "").ToLowerInvariant(), c = (chip ?? "").ToLowerInvariant();
            return v.Contains("apple") || c.StartsWith("apple");
        }

        /// <summary>Before any setting is read (OzQuality reads GRAPHICS QUALITY at BeforeSceneLoad): on an Intel / AMD Mac's first start
        /// GRAPHICS QUALITY LOW and the light menu flybys, each only when the player has no value of his own; then never again.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void FirstStart()
        {
            try
            {
                if (PlayerPrefs.GetInt(FirstKey, 0) != 0) { FirstLine = "first-start settings: chosen before (" + FirstKey + ")"; return; }
                string chip = SystemInfo.graphicsDeviceName ?? "", vendor = SystemInfo.graphicsDeviceVendor ?? "";
                if (AppleChip(vendor, chip))
                {
                    PlayerPrefs.SetInt(FirstKey, 2); PlayerPrefs.Save();
                    FirstLine = $"first start on an Apple chip ({chip}): nothing chosen, the defaults stand";
                    Debug.Log(Tag + " " + FirstLine);
                    return;
                }
                var picked = new List<string>(); var kept = new List<string>();
                if (PlayerPrefs.HasKey(OzQuality.Pref)) kept.Add("GRAPHICS QUALITY " + OzQuality.Names[Mathf.Clamp(PlayerPrefs.GetInt(OzQuality.Pref, 0), 0, 2)]);
                else { PlayerPrefs.SetInt(OzQuality.Pref, 2); picked.Add("GRAPHICS QUALITY LOW"); }
                if (PlayerPrefs.HasKey(OzMenuVideo.Pref) || PlayerPrefs.HasKey(OzMenuVideo.PrefAuto)) kept.Add("the menu flybys as they were");
                else { PlayerPrefs.SetInt(OzMenuVideo.PrefAuto, OzMenuVideo.Light); PlayerPrefs.SetString(OzMenuVideo.PrefAutoGpu, chip); picked.Add("the light menu flybys (1920x1080, 30 fps)"); }
                PlayerPrefs.SetInt(FirstKey, 1); PlayerPrefs.Save();
                FirstLine = $"first start on a Mac with {chip} ({vendor}, not an Apple chip): "
                            + (picked.Count > 0 ? string.Join(" and ", picked) + " chosen and saved" : "nothing chosen")
                            + (kept.Count > 0 ? "; kept as the player had them: " + string.Join(", ", kept) : "")
                            + "; OPTIONS change them as always, the game never chooses again";
                Debug.Log(Tag + " " + FirstLine);
            }
            catch (System.Exception e) { FirstLine = "first-start settings: nothing chosen (" + e.GetType().Name + ")"; Debug.Log(Tag + " " + FirstLine); }
        }

        // ------------------------------------------------------------------ 3. the loading screen's shader warm-up
        /// <summary>The longest a level's loading screen waits for its shader list on a Mac (s; View/OzLevelWarm.cs MaxSeconds is 30 elsewhere).
        /// A Mac that has built the shaders before needs a second or two; a cold Intel / AMD Mac may need a minute.</summary>
        public const float WarmMaxSeconds = 120f;

        // ------------------------------------------------------------------ 4. the menu flybys
        /// <summary>A flyby playing this long without one decoded picture gives up for this run (s, counted from menu frames, each at most
        /// NoFrameStepCap, so one long frame never counts for more).</summary>
        public const float NoFrameSec = 3f, NoFrameStepCap = 0.25f;
        static readonly HashSet<string> gaveUp = new HashSet<string>();
        /// <summary>Levels whose flyby gave up in this run (checks, the log).</summary>
        public static int FlybysGivenUp => gaveUp.Count;

        // ------------------------------------------------------------------ 5. the log
        /// <summary>A frame longer than this counts in the level's minute line (s).</summary>
        public const float LongFrameSec = 0.25f;
        ProfilerRecorder gfxUsed;
        ZGame levelGame; string levelSeen; float minuteAt; int minute, framesIn, longFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (OzLevelWarm.MaxSeconds < WarmMaxSeconds) OzLevelWarm.MaxSeconds = WarmMaxSeconds;          // after OzLevelWarm.ResetStatics (SubsystemRegistration)
            var go = new GameObject("OzMacPerf"); DontDestroyOnLoad(go); go.AddComponent<OzMacPerf>();
        }

        void Awake()
        {
            try { gfxUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory"); } catch (System.Exception) { }
            string chip = SystemInfo.graphicsDeviceName ?? "", vendor = SystemInfo.graphicsDeviceVendor ?? "";
            Debug.Log($"{Tag} mac_perf_1_5b: graphics {chip} ({vendor}, {SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsMemorySize} MB), "
                      + $"{(AppleChip(vendor, chip) ? "an Apple chip" : "not an Apple chip")}, processor {SystemInfo.processorType}; the 3D picture at most {BudgetName[0]} "
                      + $"on every preset, MSAA off above it; a level's shader list may warm up to {OzLevelWarm.MaxSeconds:0} s on its loading screen; "
                      + $"{(FirstLine.Length > 0 ? FirstLine : "first-start settings: not looked at")}");
        }

        void OnDestroy() { if (gfxUsed.Valid) gfxUsed.Dispose(); }

        void LateUpdate()
        {
            Picture();
            WatchFlybys();
            LevelLines();
        }

        // ------------------------------------------------------------------ 1. the picture, looked at again only after a change
        void Picture()
        {
            var cur = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            int w = Screen.width, h = Screen.height, l = Mathf.Clamp(OzQuality.Level, 0, 2);
            bool reduceMsaa = OzQuality.ReduceMsaa;
            if (cur == seen && w == seenW && h == seenH && l == seenLevel && reduceMsaa == seenReduceMsaa && (!cur || cur.msaaSampleCount == seenMsaa)) return;
            seenReduceMsaa = reduceMsaa;
            seen = cur; seenW = w; seenH = h; seenLevel = l;
            if (!cur) return;
            if (!own.TryGetValue(cur, out float baseScale)) { baseScale = cur.renderScale; own[cur] = baseScale; ownMsaa[cur] = cur.msaaSampleCount; }   // the preset's own (scale 1.0, MSAA 4x / 2x / off as shipped)
            int baseMsaa = ownMsaa[cur];
            float fit = For(w, h, Budget[l]);
            float s = Mathf.Min(baseScale, fit);
            int m = MsaaFor(fit, baseMsaa);
            if (reduceMsaa) m = Mathf.Min(m, 2);
            if (Mathf.Abs(cur.renderScale - s) > 0.0005f) cur.renderScale = s;
            if (cur.msaaSampleCount != m) cur.msaaSampleCount = m;
            seenMsaa = cur.msaaSampleCount;
            Scale = s; Msaa = m;
            Line = $"{Tag} screen {w}x{h}, graphics {OzQuality.Names[l]}: the 3D picture is drawn at {Mathf.RoundToInt(w * s)}x{Mathf.RoundToInt(h * s)} "
                   + $"(render scale {s:0.###}, at most {BudgetName[l]} for this preset), MSAA {MsaaName(m)}"
                   + (m != Mathf.Max(1, baseMsaa) ? $" (the preset's {MsaaName(baseMsaa)} is reduced; the SMAA edge smoothing stays)" : "")
                   + $", the interface at {w}x{h}";
            Debug.Log(Line);
        }

        string PictureNow() => $"the 3D picture {Mathf.RoundToInt(Screen.width * Scale)}x{Mathf.RoundToInt(Screen.height * Scale)} of {Screen.width}x{Screen.height}, "
                               + $"MSAA {MsaaName(Msaa)}, graphics {OzQuality.Name}";

        // ------------------------------------------------------------------ 4. the menu flybys
        void WatchFlybys()
        {
            var live = OzLevelPreview.Live;
            if (live.Count == 0) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, NoFrameStepCap);
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                if (p == null || !p.VideoOk) continue;
                if (gaveUp.Contains(p.Level)) { p.MacGiveUp("a flyby of this level showed no picture earlier in this run"); continue; }   // a new menu: its still at once
                var v = p.Player;
                if (!v.isPlaying || v.frame >= 0) { p.MacNoFrameSec = 0f; continue; }
                p.MacNoFrameSec += dt;
                if (p.MacNoFrameSec < NoFrameSec) continue;
                gaveUp.Add(p.Level);
                string c = v.clip ? $"{v.clip.name} ({v.clip.width}x{v.clip.height}, {v.clip.frameRate:0.##} fps)" : "its clip";
                float sec = p.MacNoFrameSec;
                p.MacGiveUp($"{c} played {sec:0.0} s without one decoded picture on this Mac");
                Debug.Log($"{Tag} menu flyby {p.Level}: {c} played {sec:0.0} s without one decoded picture: this level shows its still for the rest of "
                          + "this run and the menu moves on, as after a video error");
            }
        }

        // ------------------------------------------------------------------ 5. the level lines
        void LevelLines()
        {
            var g = ZGame.I;
            if (!g) { levelGame = null; levelSeen = null; return; }
            float now = Time.unscaledTime;
            if (g != levelGame || g.level != levelSeen)
            {
                levelGame = g; levelSeen = g.level; minute = 0; minuteAt = now; framesIn = longFrames = 0;
                Debug.Log($"{Tag} level {g.level} start: the loading screen's shader warm-up: {OzLevelWarm.LastLine} ({OzLevelWarm.ProofSteps} steps over "
                          + $"{OzLevelWarm.ProofFrames} loading frames, longest {OzLevelWarm.ProofLongestMs:0} ms{(OzLevelWarm.ProofCapped ? ", stopped at its time cap" : "")}"
                          + $"{(OzLevelWarm.ProofMissing ? ", no list" : "")}, at most {OzLevelWarm.MaxSeconds:0} s); the shared list {OzShaderWarm.State} "
                          + $"({OzShaderWarm.StepsDone} steps so far); {PictureNow()}; {Memory()}");
                return;
            }
            framesIn++;
            if (Time.unscaledDeltaTime > LongFrameSec) longFrames++;
            if (now - minuteAt < 60f) return;
            minute++; minuteAt = now;
            Debug.Log($"{Tag} {g.level} minute {minute}: {framesIn} frames, {longFrames} of them over {LongFrameSec * 1000f:0} ms; {PictureNow()}; {Memory()}"
                      + $"{(FlybysGivenUp > 0 ? "; menu flybys given up this run " + FlybysGivenUp : "")}");
            framesIn = longFrames = 0;
        }

        string Memory()
        {
            string used = gfxUsed.Valid && gfxUsed.LastValue > 0 ? (gfxUsed.LastValue / (1024L * 1024L)) + " MB" : "n/a";   // 0 = the counter is not given in this build
            return $"graphics memory in use {used} (the engine's count) of the card's {SystemInfo.graphicsMemorySize} MB";
        }
    }
}
#endif
