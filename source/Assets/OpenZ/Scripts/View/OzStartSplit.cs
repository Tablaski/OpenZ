// OpenZ (GPL-3.0) - version 1.5, kit loading_bar_real_r8 (FAILS A24 + row EXE-QUIT): where a level start's long first frame goes.
// The level's first frame (the engine's scene activation and every Awake, the sceneLoaded handlers, every
// Start, the first Update / LateUpdate with OzStartAhead and OzLevelPerfTune's warm, the first picture) is ONE frame of 5.5-10.5 s on exes
// 14c-15d (3.5-5.5 s on 14b); a quit waits for it too. Before it can be cut or spread, its parts must be known IN THE BUILT PLAYER (exe-only
// paths need exe proof). This host measures them on every level start, always on, in every build:
//  - OpenZMainMenu calls Begin(level) on the let-in frame (just before allowSceneActivation = true);
//  - one object kept across the scene switch stamps (real time): the menu scene unloaded, the active scene changed, the sceneLoaded handlers,
//    the Update phase of the activation frame (execution order -32000: every Start of the level ran before it), its LateUpdate phase, the
//    render (RenderPipelineManager begin / end of the context), the next frame; and every log line written on the main thread in between;
//  - the next frame it prints ONE log line: "[OpenZ StartSplit] <level>: long frame N ms | +ms <stamp> | ...; the biggest gaps between the
//    ..." and removes itself.
// Measures only: nothing drawn, heard or simulated changes; no wait, no load, no file; one small object for about one second per level start.
// Off: OzStartSplit.On = false. Play checks: LastLine, LastLongMs, LastPhases. The exe check: kit_tools/check_exe_start.py.
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    [DefaultExecutionOrder(-32000)]
    public sealed class OzStartSplit : MonoBehaviour
    {
        public const string Tag = "[OpenZ StartSplit]";
        /// <summary>Off = no object, no line.</summary>
        public static bool On = true;
        /// <summary>The last start's line (without the tag), its long frame (ms) and its stamps as "name=ms" pairs (ms after the frame's start).</summary>
        public static string LastLine { get; private set; } = "none yet";
        public static float LastLongMs { get; private set; } = -1f;
        public static string LastPhases { get; private set; } = "";
        public static string LastLevel { get; private set; } = "";
        public static int Runs { get; private set; }
        /// <summary>Lines kept per start (the rest only counted) and the longest wait for the level (s) before the line is written anyway.</summary>
        const int MaxLines = 800; const float CapS = 120f; const int Gaps = 8;

        static OzStartSplit host;

        string level = ""; float t0; int letFrame, loadedFrame = -1, between, logged; float betweenMax;
        float tUnloaded = -1f, tActive = -1f, tLoaded = -1f, tUpdate = -1f, tLate = -1f, tRenderBegin = -1f, tRenderEnd = -1f;
        string activeName = "";
        bool finished;
        readonly List<float> logT = new List<float>(256);
        readonly List<string> logS = new List<string>(256);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { On = true; LastLine = "none yet"; LastLongMs = -1f; LastPhases = LastLevel = ""; Runs = 0; host = null; }

        /// <summary>The menu, on the let-in frame: measures the level start that follows (a second call replaces the first).</summary>
        public static void Begin(string level)
        {
            if (!On || !Application.isPlaying) return;
            try
            {
                if (host) { host.finished = true; Destroy(host.gameObject); host = null; }
                var go = new GameObject("OzStartSplit");
                DontDestroyOnLoad(go);
                var s = go.AddComponent<OzStartSplit>();
                s.level = string.IsNullOrEmpty(level) ? "?" : level; s.t0 = Time.realtimeSinceStartup; s.letFrame = Time.frameCount;
                host = s;
            }
            catch (System.Exception e) { Debug.LogWarning(Tag + " not started: " + e.Message); }
        }

        float Now => Time.realtimeSinceStartup - t0;

        void OnEnable()
        {
            Application.logMessageReceived += OnLog;
            SceneManager.sceneUnloaded += OnUnloaded;
            SceneManager.activeSceneChanged += OnActive;
            SceneManager.sceneLoaded += OnLoaded;
            RenderPipelineManager.beginContextRendering += OnRenderBegin;
            RenderPipelineManager.endContextRendering += OnRenderEnd;
        }

        void OnDisable()
        {
            Application.logMessageReceived -= OnLog;
            SceneManager.sceneUnloaded -= OnUnloaded;
            SceneManager.activeSceneChanged -= OnActive;
            SceneManager.sceneLoaded -= OnLoaded;
            RenderPipelineManager.beginContextRendering -= OnRenderBegin;
            RenderPipelineManager.endContextRendering -= OnRenderEnd;
        }

        void OnDestroy() { if (host == this) host = null; }

        void OnLog(string text, string stack, LogType type)
        {
            if (finished || loadedFrame >= 0 && Time.frameCount > loadedFrame) return;
            if (text != null && text.StartsWith(Tag, System.StringComparison.Ordinal)) return;
            logged++;
            if (logT.Count >= MaxLines) return;
            logT.Add(Now); logS.Add(Short(text));
        }

        void OnUnloaded(Scene s) { if (!finished && tUnloaded < 0f) tUnloaded = Now; }
        void OnActive(Scene a, Scene b) { if (!finished && tActive < 0f) { tActive = Now; activeName = b.name; } }

        void OnLoaded(Scene s, LoadSceneMode m)
        {
            if (finished || m != LoadSceneMode.Single || loadedFrame >= 0) return;
            loadedFrame = Time.frameCount; tLoaded = Now;                                                   // registered after the game's own handlers: about their end
        }

        void OnRenderBegin(ScriptableRenderContext c, List<Camera> cams) { if (!finished && Time.frameCount == loadedFrame && tRenderBegin < 0f) tRenderBegin = Now; }
        void OnRenderEnd(ScriptableRenderContext c, List<Camera> cams) { if (!finished && Time.frameCount == loadedFrame) tRenderEnd = Now; }

        void Update()
        {
            if (finished) return;
            int fc = Time.frameCount;
            if (loadedFrame < 0)
            {
                if (fc > letFrame) { between++; betweenMax = Mathf.Max(betweenMax, Time.unscaledDeltaTime * 1000f); }   // frames after the let-in, before the activation
                if (Now > CapS) Finish(-1f, "no level scene " + CapS.ToString("0", CultureInfo.InvariantCulture) + " s after the let-in");
                return;
            }
            if (fc == loadedFrame) { if (tUpdate < 0f) tUpdate = Now; return; }                             // the activation frame's Update phase: every Start ran before it
            Finish(Time.unscaledDeltaTime * 1000f, null);                                                    // the next frame: the long frame's length is this frame's delta
        }

        void LateUpdate() { if (!finished && Time.frameCount == loadedFrame && tLate < 0f) tLate = Now; }

        static string Short(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int n = s.IndexOf('\n'); if (n >= 0) s = s.Substring(0, n);
            s = s.Replace('"', '\'');
            return s.Length > 90 ? s.Substring(0, 90) : s;
        }

        static string Ms(float v) => v.ToString("0", CultureInfo.InvariantCulture);

        void Finish(float longMs, string cut)
        {
            finished = true;
            try
            {
                float tNext = Now, start = longMs > 0f ? tNext - longMs / 1000f : 0f;                     // the long frame's start (s after the let-in)
                var st = new List<(float t, string what)>();
                void Add(float t, string what) { if (t >= 0f) st.Add((t, what)); }
                Add(tUnloaded, "menu scene unloaded");
                Add(tActive, "active scene " + activeName);
                Add(tLoaded, "sceneLoaded handlers done");
                Add(tUpdate, "Update phase (the Starts done)");
                Add(tLate, "LateUpdate phase");
                Add(tRenderBegin, "render begins");
                Add(tRenderEnd, "render ends");
                st.Sort((a, b) => a.t.CompareTo(b.t));
                var sb = new StringBuilder(1024);
                sb.Append(level).Append(": long frame ").Append(longMs > 0f ? Ms(longMs) : "?").Append(" ms");
                var ph = new StringBuilder();
                foreach (var p in st)
                {
                    sb.Append(" | +").Append(Ms((p.t - start) * 1000f)).Append(' ').Append(p.what);
                    ph.Append(p.what.Split(' ')[0]).Append(p.what.Contains("ends") ? "End" : p.what.Contains("begins") ? "Begin" : "").Append('=').Append(Ms((p.t - start) * 1000f)).Append(' ');
                }
                sb.Append(" | +").Append(longMs > 0f ? Ms(longMs) : "?").Append(" next frame");
                sb.Append("; frames between the let-in and the activation ").Append(between).Append(" (longest ").Append(Ms(betweenMax)).Append(" ms)");
                // the biggest gaps inside the long frame: between consecutive stamps (log lines and the phases above), the work that ran before each
                var all = new List<(float t, string what)>(st);
                for (int i = 0; i < logT.Count; i++) if (logT[i] >= start - 0.0005f) all.Add((logT[i], "\"" + logS[i] + "\""));
                all.Add((start, "frame start")); if (longMs > 0f) all.Add((tNext, "next frame"));
                all.Sort((a, b) => a.t.CompareTo(b.t));
                var gaps = new List<(float ms, string what)>();
                for (int i = 1; i < all.Count; i++) gaps.Add(((all[i].t - all[i - 1].t) * 1000f, all[i].what));
                gaps.Sort((a, b) => b.ms.CompareTo(a.ms));
                sb.Append("; the biggest gaps (work that ran before the named line): ");
                for (int i = 0; i < gaps.Count && i < Gaps; i++) sb.Append(i > 0 ? "; " : "").Append(Ms(gaps[i].ms)).Append(" ms before ").Append(gaps[i].what);
                sb.Append("; log lines in it ").Append(logged);
                if (cut != null) sb.Append("; cut short: ").Append(cut);
                LastLine = sb.ToString(); LastLongMs = longMs; LastPhases = ph.ToString().Trim(); LastLevel = level; Runs++;
                Debug.Log(Tag + " " + LastLine);
            }
            catch (System.Exception e) { Debug.LogWarning(Tag + " no line: " + e.Message); }
            Destroy(gameObject);
        }
    }
}
