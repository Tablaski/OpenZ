// OpenZ (GPL-3.0) - version 1.4: a level's first start as smooth as its second one.
// What made the first start of a level stutter while its briefing played (and never the second start): the game warms its shader list
// (View/OzShaderWarm.cs) a few variants at a time behind the main menu; on a graphics driver that has not seen this build's shaders yet
// one variant costs 50 to 500 ms, the menu holds the list back so the menu stays smooth, and the rest of the list then went on inside
// the level at one variant per frame or more: every frame of the briefing paid one cold compile. The second start found every variant in
// the driver's own cache, so nothing was slow.
// Now, three parts, the same for every level:
//  1. Every level has its own warm list, Resources/OpenZ/Shaders/Levels/<id>_warm (a ShaderVariantCollection), made by the editor
//     from the shared list and what the level's scene uses (Editor/OzLevelWarmLists.cs: menu OpenZ/Shaders, and by itself before every
//     player build, so a new level gets its list with no hand work).
//  2. The menu's loading screen warms that list before the level is let in (OpenZMainMenu.TickLoading calls Step every loading frame;
//     the bar waits for it): about LoadingBudgetMs of each loading frame, at most MaxSeconds. A driver that knows the shaders takes a
//     fraction of a second, inside the loading screen's own minimum time: nothing changes there. A cold driver pays its compiles here,
//     once, instead of in the briefing.
//  3. Inside a level whose list was warmed on its loading screen, the shared list waits (OzShaderWarm.Update asks HoldsGlobal): what it
//     still holds is not used by this level; it goes on in the next menu.
// Also: levels added from 1.4 on (not the three 1.2 levels) do their start work before the first picture on both sides
// (View/OzStartAhead.cs asks StartAheadBothSides): the lava's prewarm and the nuke's prewarm no longer run under the briefing.
// The log: "[OpenZ LevelWarm]" for the loading screen's warm-up and, at the end of each briefing, its frames (worst frame, frames over
// 50 ms) with the first start's numbers beside it, for the check of the first and the second start.
// Off: OzLevelWarm.On = false (every part as before).
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace OpenZ
{
    public sealed class OzLevelWarm : MonoBehaviour
    {
        /// <summary>The level lists: Resources/OpenZ/Shaders/Levels/&lt;id&gt;_warm.</summary>
        public const string ListDir = "OpenZ/Shaders/Levels/";
        public const string Suffix = "_warm";
        /// <summary>Every part of this file (the loading screen's warm-up, the shared list's wait, the start work ahead).</summary>
        public static bool On = true;
        /// <summary>Time of each loading frame spent on the level's list (ms); one variant is always taken.</summary>
        public static float LoadingBudgetMs = 30f;
        /// <summary>The loading screen never waits longer than this for the list (s); the rest compiles where it is first drawn, as before.</summary>
        public static float MaxSeconds = 30f;
        /// <summary>The three levels of 1.2 keep their start exactly as it was (View/OzStartAhead.cs: Levels / RedToo).</summary>
        public static readonly string[] Older = { "L02", "L16", "L17" };
        /// <summary>A frame longer than this counts as a hitch in the briefing line.</summary>
        public const float HitchMs = 50f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            On = true; LoadingBudgetMs = 30f; MaxSeconds = 30f; ListLoadMaxSeconds = 5f; ProofInEditor = false;
            level = null; load = null; list = null; finished = prepared = false; warmedLevel = null; lastStepAt = -10f; LastLine = "not run";
            firstWorst.Clear(); starts.Clear(); LastBriefing = "no briefing measured yet"; LastBriefingWorstMs = -1f; FirstBriefingWorstMs = -1f;
        }

        /// <summary>A level of 1.4 or later: its start work runs before the first picture on both sides (View/OzStartAhead.Wanted).</summary>
        public static bool StartAheadBothSides(string id) => On && !string.IsNullOrEmpty(id) && System.Array.IndexOf(Older, id) < 0;

        // ------------------------------------------------------------------ 2. the loading screen
        static string level, warmedLevel;
        static ResourceRequest load;
        static ShaderVariantCollection list;
        static bool finished, capped, missing, prepared;
        static int total, steps, frames, coldDone;
        static double workMs, longestMs;
        static float startAt, lastStepAt = -10f;
        /// <summary>The last loading screen's warm-up, one line (also in the log).</summary>
        public static string LastLine { get; private set; } = "not run";
        /// <summary>Checks: the variants warmed and the frames it took on the last loading screen.</summary>
        public static int ProofSteps => steps;
        public static int ProofFrames => frames;
        public static double ProofLongestMs => longestMs;
        public static bool ProofCapped => capped;
        public static bool ProofMissing => missing;

        /// <summary>Checks: the loading screen's warm-up runs in the editor too (it runs there anyway while a cold driver is emulated).</summary>
        public static bool ProofInEditor;

        /// <summary>Checks: forget the warm-up of this run (the next Step starts again).</summary>
        public static void ProofReset() { level = null; load = null; list = null; finished = prepared = false; warmedLevel = null; lastStepAt = -10f; }

        /// <summary>Longest wait for the list asset itself (s); after it the level loads as before (the loading screen never hangs).</summary>
        public static float ListLoadMaxSeconds = 5f;

        /// <summary>6 Nov hang fix: the menu calls this on DEPLOY, BEFORE its SceneManager.LoadSceneAsync (allowSceneActivation = false).
        /// A Resources.LoadAsync started after that scene load queues behind it and never finishes while the scene waits to be let in,
        /// and the scene waited for this list: the exe's loading screen stood at 0 % for ever (the editor skips this part). The list is
        /// small, so it is read here at once, while no scene load is pending.</summary>
        public static void Prepare(string id)
        {
            if (!On || string.IsNullOrEmpty(id)) return;
            if (Application.isEditor && !ProofInEditor && OzShaderWarm.ProofColdStepMs <= 0f) return;                // as Step
            if (id != level) Begin(id);
            if (finished || list || load != null) return;
            list = Resources.Load<ShaderVariantCollection>(ListDir + id + Suffix);
            prepared = true;
        }

        /// <summary>One loading frame of level `id`: warms a part of its list; 0..1 how far it is (1 = done, no list, capped or off).</summary>
        public static float Step(string id)
        {
            if (!On || string.IsNullOrEmpty(id)) return 1f;
            if (Application.isEditor && !ProofInEditor && OzShaderWarm.ProofColdStepMs <= 0f) return 1f;             // the editor compiles its own way (Editor/OzShaderWarmup.cs), as OzShaderWarm does
            if (id != level) Begin(id);
            if (finished) return 1f;
            lastStepAt = Time.unscaledTime;
            if (!list && !prepared)
            {
                if (load == null) { load = Resources.LoadAsync<ShaderVariantCollection>(ListDir + id + Suffix); return 0f; }   // not prepared by the menu (the checks): as before
                if (!load.isDone)
                {
                    if (Time.unscaledTime - startAt > ListLoadMaxSeconds) { missing = true; Finish($"its list did not load in {ListLoadMaxSeconds:0} s: the level loads as before"); return 1f; }
                    return 0f;
                }
                list = load.asset as ShaderVariantCollection;
                prepared = true;
            }
            if (prepared && total == 0 && frames == 0)
            {
                if (!list) { missing = true; Finish($"no warm list Resources/{ListDir}{id}{Suffix}: the level loads as before (Editor/OzLevelWarmLists.cs makes it)"); return 1f; }
                total = list.variantCount;
                float cold = OzShaderWarm.ProofColdStepMs;
                if (cold > 0f && OzShaderWarm.ProofColdVariants > 0) total = Mathf.Min(total, OzShaderWarm.ProofColdVariants);
                if (total == 0) { Finish("its list is empty"); return 1f; }
                if (cold <= 0f && list.isWarmedUp) { Finish($"{total} variants were warm already"); return 1f; }
                return 0f;
            }
            frames++;
            var sw = Stopwatch.StartNew();
            bool all = false;
            while (!all)
            {
                double t0 = sw.Elapsed.TotalMilliseconds;
                if (OzShaderWarm.ProofColdStepMs > 0f) { while (sw.Elapsed.TotalMilliseconds - t0 < OzShaderWarm.ProofColdStepMs) { } all = ++coldDone >= total; }   // a cold driver emulated (checks, -ozwarmcold)
                else all = list.WarmUpProgressively(1);
                steps++;
                double d = sw.Elapsed.TotalMilliseconds - t0;
                if (d > longestMs) longestMs = d;
                if (steps > total + 64) all = true;                                                                  // a variant the build left out never counts as warm
                if (sw.Elapsed.TotalMilliseconds >= LoadingBudgetMs) break;
            }
            workMs += sw.Elapsed.TotalMilliseconds;
            if (all) Finish($"{Count()} of {total} variants warmed in {workMs:0} ms of work over {frames} loading frames (longest single variant {longestMs:0.0} ms)");
            else if (Time.unscaledTime - startAt > MaxSeconds) { capped = true; Finish($"stopped at {MaxSeconds:0} s: {Count()} of {total} variants warmed ({workMs:0} ms of work over {frames} frames); the rest compile where they are first drawn"); }
            return finished ? 1f : Mathf.Clamp01(Done() / (float)Mathf.Max(1, total));
        }

        static int Done() => OzShaderWarm.ProofColdStepMs > 0f ? coldDone : list ? list.warmedUpVariantCount : 0;
        static string Count() => OzShaderWarm.ProofColdStepMs > 0f ? coldDone + " emulated cold" : Done().ToString();

        static void Begin(string id)
        {
            level = id; load = null; list = null; finished = capped = missing = prepared = false;
            OzBootTime.Mark("loading-screen warm list " + id + ": start");   // 1.5 kit player_reports_1111 P2
            total = steps = frames = coldDone = 0; workMs = longestMs = 0.0; startAt = Time.unscaledTime;
        }

        static void Finish(string what)
        {
            finished = true; warmedLevel = missing ? null : level;
            LastLine = $"{level} loading screen: {what}";
            Debug.Log("[OpenZ LevelWarm] " + LastLine);
            OzBootTime.Mark("loading-screen warm list " + level + ": end");   // 1.5 kit player_reports_1111 P2
        }

        // ------------------------------------------------------------------ 3. the shared list waits
        /// <summary>The shared list (OzShaderWarm) waits this frame: the loading screen warms a level's own list now, or this is a level whose
        /// own list was warmed on its loading screen.</summary>
        public static bool HoldsGlobal
        {
            get
            {
                if (!On) return false;
                if (level != null && !finished && Time.unscaledTime - lastStepAt < 0.5f) return true;
                var g = ZGame.I;
                return warmedLevel != null && g && g.level == warmedLevel;
            }
        }

        // ------------------------------------------------------------------ the briefing's frames (the log line and the checks)
        static readonly Dictionary<string, float> firstWorst = new Dictionary<string, float>();
        static readonly Dictionary<string, int> starts = new Dictionary<string, int>();
        /// <summary>The last briefing, one line; its worst frame (ms) and the worst frame of this level's first briefing in this run.</summary>
        public static string LastBriefing { get; private set; } = "no briefing measured yet";
        public static float LastBriefingWorstMs { get; private set; } = -1f;
        public static float FirstBriefingWorstMs { get; private set; } = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; }

        static void OnScene(Scene s, LoadSceneMode m)
        {
            if (m != LoadSceneMode.Single || !Application.isPlaying) return;
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!g) return;
            var go = new GameObject("OzLevelWarm") { hideFlags = HideFlags.HideInHierarchy };
            if (s.IsValid() && go.scene != s) SceneManager.MoveGameObjectToScene(go, s);
            go.AddComponent<OzLevelWarm>().id = g.level;
        }

        string id; int seen; bool measuring, logged; float briefSec; readonly List<float> ms = new List<float>(4096);

        void Update()
        {
            if (logged) return;
            if (++seen <= 3) return;                                                                                 // the level's first frames carry the load itself
            bool on = ZodBriefing.BriefingActive;
            if (on) { measuring = true; float d = Time.unscaledDeltaTime; ms.Add(d * 1000f); briefSec += d; return; }
            if (measuring) { logged = true; Report(); }
        }

        void Report()
        {
            if (ms.Count == 0) return;
            starts.TryGetValue(id, out int n); starts[id] = ++n;
            var sorted = new List<float>(ms); sorted.Sort();
            float worst = sorted[sorted.Count - 1], p99 = sorted[Mathf.Clamp(Mathf.CeilToInt(sorted.Count * 0.99f) - 1, 0, sorted.Count - 1)];
            int over = 0; foreach (var x in ms) if (x > HitchMs) over++;
            if (!firstWorst.ContainsKey(id)) firstWorst[id] = worst;
            LastBriefingWorstMs = worst; FirstBriefingWorstMs = firstWorst[id];
            LastBriefing = $"{id} briefing (start {n} of {id} in this run): {ms.Count} frames in {briefSec:0.0} s, worst {worst:0.0} ms, 99th {p99:0.0} ms, {over} over {HitchMs:0} ms; "
                         + $"the first start's worst {FirstBriefingWorstMs:0.0} ms (this one / first {worst / Mathf.Max(0.1f, FirstBriefingWorstMs):0.00}); loading warm-up: {LastLine}";
            Debug.Log("[OpenZ LevelWarm] " + LastBriefing);
        }
    }
}
