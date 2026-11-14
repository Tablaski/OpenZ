// OpenZ (GPL-3.0)
// disc with its bloom): THE SUN FROM THE FIRST FRAME of the level-start flyover, red and blue.
// Before: the painted flyby sky that draws the sun disc and its bloom (FlybySky) started loading its panorama (8192 x 4096 BC7, 32 MiB,
// no mips) when the flight began (async) and faded in over FadeSeconds (1 s), so the first shot showed the plain sky for about a second
// and the disc from about 1 s in (red level 16's dawn_swamp, every blue shot 1 from the blue base into the sunrise).
// Now:
//  1. PRELOAD WITH THE LEVEL. When a level scene has loaded (SceneManager.sceneLoaded: after every Awake of the scene, before any Start,
//     while the loading screen's last picture is still up) and its start flyover will use the sky, the panorama's async load starts
//     there, so the disk read runs beside the first frame's own start work (the HUD build, the briefing stage). "Will use the sky" = the
//     level has a sky entry (sky_panoramas.json) AND a level-start briefing is pending (AudioDirector.HoldStartLines: the briefing holds
//     the start lines until it hands the level over; red today, the blue briefing too when it holds them) or ExpectStartFlyover answers
//     true. Anything else (a bot match, a test start, the briefing off, the playground, the menu) loads nothing: zero cost there.
//  2. FULL FROM THE FIRST FRAME. On the first frame a new flight (ZodFlyover.Live) wants the sky, a load that is not done yet is finished
//     on that frame (the wait is timed and logged) and the sky's weight is 1 at once: no fade-in from the plain sky and no fade at the cut
//     from the loading screen (the level's first picture is the flight's first frame). Every flight gets it, preloaded or not: the red
//     briefing, the blue briefing, a proof's own flight.
//  3. RELEASED AS BEFORE. The fade-out at the last shot and the unload 2 s after it are unchanged. A preload no flight takes is held at
//     most PreloadHoldSeconds after the level load, then released by the same rule.
//  SunFirstFrame = false is the earlier way exactly (no preload, no wait, the 1 s fade-in): kept only for the Play proof's A/B measure.
// Logs: "[OpenZ FlybySky] preload ..." at the level load and "[OpenZ FlybySky] ... start flyover, first frame: ..." with the numbers (the
// background read, the wait on the first frame, the size). Play check: FlybySky.PreloadStatus().
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    public sealed partial class FlybySky
    {
        /// <summary>The start flyover's sky loads with the level and is full from the flight's first frame (true). False = the earlier way
        /// (loaded when the flight begins, faded in over FadeSeconds): kept only for the Play proof's A/B measure.</summary>
        public static bool SunFirstFrame = true;
        /// <summary>A level-load preload no flight has taken is held this long (s of real time after the level load), then released.</summary>
        public static float PreloadHoldSeconds = 10f;
        /// <summary>A level-start briefing that does not hold AudioDirector.HoldStartLines answers true here when its flyover will fly;
        /// asked at the level load, after every Awake of the scene.</summary>
        public static System.Func<bool> ExpectStartFlyover;

        // ---- the numbers (real time; -1 = not measured): the log, the Play proof and PreloadStatus read them
        /// <summary>Level loads that started the panorama's load in the background.</summary>
        public static int Preloads { get; private set; }
        /// <summary>Preloads a start flyover took on its first frame / preloads released without a flyover.</summary>
        public static int PreloadsTaken { get; private set; }
        public static int PreloadsReleased { get; private set; }
        /// <summary>Flights whose first frame was handled (the sky full at once).</summary>
        public static int FirstFrames { get; private set; }
        /// <summary>When the last level scene loaded (Time.realtimeSinceStartup) and its frame number.</summary>
        public static float LevelLoadedAt { get; private set; } = -1f;
        public static int LevelLoadedFrame { get; private set; } = -1;
        /// <summary>The last preload: ms from its start at the level load until the panorama was resident.</summary>
        public static float LastPreloadReadyMs { get; private set; } = -1f;
        /// <summary>The last flight's first frame: ms the main thread waited there for the panorama (0 = it was resident).</summary>
        public static float LastWaitMs { get; private set; } = -1f;
        public static bool LastFirstFramePreloaded { get; private set; }
        /// <summary>The sky weight the last flight's first frame was drawn with, the panorama's size (MB), and when that frame ran.</summary>
        public static float LastFirstFrameWeight { get; private set; } = -1f;
        public static float LastPanoramaMB { get; private set; }
        public static float FirstFlightFrameAt { get; private set; } = -1f;
        public static string LastPreload = "none yet", LastFirstFrame = "none yet";

        string preloadLevel; float preloadUntil; long preloadTicks; bool preloadReady; ZodFlyover firstSeen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void HookLevelLoads()
        {   // added after the briefing's own scene hook (that one is added before the first scene loads): its decision is made when this runs
            SceneManager.sceneLoaded -= OnLevelLoaded; SceneManager.sceneLoaded += OnLevelLoaded;
        }

        static void OnLevelLoaded(Scene s, LoadSceneMode m)
        {
            if (m != LoadSceneMode.Single) return;
            var g = ZGame.I;
            if (!g || g.gameObject.scene != s) return;                                                     // not a level (the menu, a tool's scene)
            LevelLoadedAt = Time.realtimeSinceStartup; LevelLoadedFrame = Time.frameCount;
            if (!SunFirstFrame) return;                                                                    // the earlier way (the proof's A/B measure)
            bool expected = StartFlyoverExpected(out string why);
            if (!PreloadWanted(g.level, expected))
            {
                if (Entry(g.level) != null) { LastPreload = g.level + ": nothing loaded at the level load (" + why + ")"; Debug.Log("[OpenZ FlybySky] " + LastPreload); }
                return;
            }
            Ensure().Preload(g.level, why);
        }

        /// <summary>A level-start flyover is coming: the briefing is pending (it holds the start lines until it hands over) or another
        /// briefing announced one (ExpectStartFlyover).</summary>
        static bool StartFlyoverExpected(out string why)
        {
            try
            {
                if (AudioDirector.HoldStartLines != null && AudioDirector.HoldStartLines()) { why = "the level-start briefing is pending"; return true; }
                if (ExpectStartFlyover != null && ExpectStartFlyover()) { why = "a level-start flyover is announced"; return true; }
            }
            catch (System.Exception e) { why = "the briefing could not be asked: " + e.Message; return false; }
            why = "no level-start briefing (a bot match, a test start, the briefing off, or a start without one)";
            return false;
        }

        /// <summary>The rule (pure; the counted check asks it): the level's panorama loads with the level when a start flyover is coming
        /// and the level has a sky entry.</summary>
        public static bool PreloadWanted(string level, bool startFlyoverExpected)
        {
            if (!SunFirstFrame || !startFlyoverExpected || string.IsNullOrEmpty(level)) return false;
            var e = Entry(level);
            return e != null && !string.IsNullOrEmpty(e.textureHi);
        }

        void Preload(string lv, string why)
        {
            bool resident = tex && loadedLevel == lv;
            EnsureLoaded();                                                                                // this level's panorama (ZGame.Awake ran: Level() is lv)
            if (loadedLevel != lv || (!tex && req == null))
            {
                LastPreload = lv + ": nothing loaded at the level load (" + (failedLevel == lv ? "the panorama failed to load before" : "no panorama") + ")";
                Debug.Log("[OpenZ FlybySky] " + LastPreload);
                return;
            }
            preloadLevel = lv; preloadUntil = Time.unscaledTime + Mathf.Max(1f, PreloadHoldSeconds);
            preloadTicks = System.Diagnostics.Stopwatch.GetTimestamp(); preloadReady = resident; LastPreloadReadyMs = resident ? 0f : -1f;
            Preloads++;
            var e = Entry(lv);
            LastPreload = lv + ": " + (resident ? "the panorama is resident already" : "the panorama " + (e != null ? e.textureHi : "?") + " loads in the background")
                        + " from the level load (" + why + "); held up to " + PreloadHoldSeconds.ToString("0") + " s for the start flyover";
            Debug.Log("[OpenZ FlybySky] preload " + LastPreload);
        }

        /// <summary>The unload rule waits while this level's preload waits for its start flyover.</summary>
        bool PreloadHeld() => SunFirstFrame && preloadLevel != null && preloadLevel == loadedLevel && preloadLevel == Level() && Time.unscaledTime < preloadUntil;

        /// <summary>LateUpdate, after the load poll: the preload's bookkeeping, and on the first frame a new flight wants the sky: the
        /// panorama finished now if it is not resident yet, and the sky full at once.</summary>
        void FlightFirstFrame(ZodFlyover fly, bool flying, bool want)
        {
            if (preloadLevel != null)
            {
                if (!preloadReady && tex && loadedLevel == preloadLevel) { preloadReady = true; LastPreloadReadyMs = Ms(preloadTicks); }   // the background read is done (seen on this frame)
                if (Level() != preloadLevel || Time.unscaledTime >= preloadUntil)
                {
                    PreloadsReleased++;
                    Debug.Log("[OpenZ FlybySky] preload " + preloadLevel + ": no start flyover took it " + (Level() != preloadLevel ? "before the level ended" : "within " + PreloadHoldSeconds.ToString("0") + " s")
                              + ": released by the usual rule (2 s without a flyover)");
                    preloadLevel = null;
                }
            }
            if (!SunFirstFrame) return;
            if (!flying) { firstSeen = null; return; }
            if (fly == firstSeen) return;
            firstSeen = fly;
            if (!want) return;
            string lv = Level();
            bool pre = preloadLevel != null && preloadLevel == lv && loadedLevel == lv, resident = tex;
            float wait = 0f;
            if (!tex && req != null)
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                var a = req.asset as Texture2D;                                                            // read before isDone: the load is finished now, on this frame
                if (req != null && req.isDone) PollLoad();
                if (!tex && a) { tex = a; req = null; }
                wait = Ms(t0);
                if (pre && !preloadReady && tex) { preloadReady = true; LastPreloadReadyMs = Ms(preloadTicks); }
            }
            if (pre) { PreloadsTaken++; preloadLevel = null; }
            bool ok = tex && Mat();
            if (ok) Weight = 1f;                                                                           // full at once: no fade-in from the plain sky
            FirstFrames++; LastWaitMs = wait; LastFirstFramePreloaded = pre; LastFirstFrameWeight = ok ? 1f : Weight;
            LastPanoramaMB = tex ? SizeMB(tex) : 0f; FirstFlightFrameAt = Time.realtimeSinceStartup;
            string how = !ok ? "NOT drawn (" + (failedLevel == lv ? "the panorama failed to load" : "no panorama") + "): the gradient sky until it is fixed"
                : resident ? (pre ? "preloaded with the level, resident " + LastPreloadReadyMs.ToString("0") + " ms after the level load" : "resident already")
                : pre ? "preloaded with the level, finished on this frame (" + wait.ToString("0") + " ms wait)"
                : "loaded on this frame (" + wait.ToString("0") + " ms wait; no preload)";
            LastFirstFrame = lv + " start flyover, first frame: " + (ok ? "the painted sky and the sun disc full at once (weight 1.00, no fade-in)" : "no painted sky")
                           + "; panorama " + (tex ? tex.name + " " + tex.width + "x" + tex.height + " " + tex.format + " " + LastPanoramaMB.ToString("F1") + " MB " : "- ") + how;
            Debug.Log("[OpenZ FlybySky] " + LastFirstFrame);
        }

        static float Ms(long fromTicks) => (float)((System.Diagnostics.Stopwatch.GetTimestamp() - fromTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);

        /// <summary>The panorama's memory (MB): the profiler's size where it has one, else the format's size (as Unload logs it).</summary>
        static float SizeMB(Texture2D t)
        {
            long b = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t);
            return b > 0 ? b / 1048576f : EstimateMB(t.width, t.height, t.format);
        }

        /// <summary>The size of a map without mips by its format (the rule Unload uses): BC7 / DXT5 one byte a pixel, DXT1 / BC4 half.</summary>
        public static float EstimateMB(int width, int height, TextureFormat format)
        {
            long b = (long)width * height * (format == TextureFormat.DXT1 || format == TextureFormat.BC4 ? 1 : format == TextureFormat.BC7 || format == TextureFormat.DXT5 ? 2 : 8) / 2;
            return b / 1048576f;
        }

        /// <summary>Play check: one line on the preload and the last first frame.</summary>
        public static string PreloadStatus() =>
            "sun from the first frame " + (SunFirstFrame ? "on" : "OFF (the earlier way)") + "; preloads " + Preloads + " (taken " + PreloadsTaken + ", released unused "
            + PreloadsReleased + "), first frames " + FirstFrames + "; last preload: " + LastPreload + "; last first frame: " + LastFirstFrame;
    }
}
