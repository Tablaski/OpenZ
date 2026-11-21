// OpenZ (GPL-3.0)
// nothing is drawn, nothing is saved, the rules are only read. Three kinds of Player.log lines, in normal play:
//  * at every level start: "[OpenZ Perf] machine: ..." (graphics card, graphics API, video memory, processor, threads, memory, screen,
//    preset, frame rate limit), so a player's log says on which card and processor the game ran;
//  * once a real minute while a level runs: "[OpenZ Perf] minute N ...": fps (average and the worst 1 % frame), the frame split into
//    main thread / render thread / graphics card / wait for the screen, the rule ticks of a frame and their time, physics steps, draw
//    batches, units / objects / unit views, the path and approach searches and the pathing rebuilds of the minute, and a processor speed
//  * a frame over 250 ms: "[OpenZ Perf] long frame ..." with its split (at most 30 a level).
// Next to it the rules' own per-minute split (Sim/ZRuleProf.cs, "[OpenZ RuleTick] minute N").
// 1.3 item 38 (log only): the minute line also gives the garbage made a frame, the collections of the minute, the managed heap, and
// the unit overlay's and the mouse hover's time a frame (View/OzPerfNumbers.cs).
// Dev / test runs only: -oz-simprint writes the world's fingerprint every 100 ticks ("[OpenZ SimPrint]"), so two builds of the exe
// (Mono and IL2CPP, plan step b) can be compared tick for tick on the same computer match.
// OzFrameTimes: the engine's frame timing figures, captured once a frame for the FPS COUNTER chip and this line together.
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenZ.Sim;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OpenZ
{
    /// <summary>The engine's frame timing figures of the latest frame that has them, captured at most once a frame (ms; 0 = not given).</summary>
    public static class OzFrameTimes
    {
        static int frame = -1;
        static readonly FrameTiming[] timing = new FrameTiming[1];
        public static bool Have { get; private set; }
        public static double Main, Render, Gpu, PresentWait;

        public static void Sample()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            FrameTimingManager.CaptureFrameTimings();
            Have = FrameTimingManager.GetLatestTimings(1, timing) > 0;
            if (!Have) { Main = Render = Gpu = PresentWait = 0.0; return; }
            Main = timing[0].cpuMainThreadFrameTime; Render = timing[0].cpuRenderThreadFrameTime; Gpu = timing[0].gpuFrameTime; PresentWait = timing[0].cpuMainThreadPresentWaitTime;
        }
    }

    public sealed class OzPerfLine : MonoBehaviour
    {
        const string Tag = "[OpenZ Perf] ";
        public const float LongFrameMs = 250f; public const int LongFramesPerLevel = 30;

        ZGame game; float minuteStart; int minute;
        int frames, physicsThisFrame, physicsSum, ruleTicksSum, longFrames;
        double dtSum, ruleMsSum, mainSum, renderSum, gpuSum, waitSum; int mainN, renderN, gpuN, waitN;
        readonly float[] frameMs = new float[16384]; int frameMsN;
        long batchesSum, setPassSum; int batchN;
        int astar0, zones0, lanes0, reach0; long work0, walks0, tiles0;
        double cpuBest = double.MaxValue;
        static long sink;
        ProfilerRecorder batches, setPass, gcAlloc;                                                      // gcAlloc: 1.3 item 38

        // -oz-simprint
        static bool simPrint, simPrintRead;

        void OnEnable()
        {
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");     // 1.3 item 38: the editor and a development exe only (a release exe reads n/a)
        }

        void OnDisable() { batches.Dispose(); setPass.Dispose(); gcAlloc.Dispose(); }

        void FixedUpdate() { physicsThisFrame++; }

        static bool SimPrintArg
        {
            get
            {
                if (!simPrintRead)
                {
                    simPrintRead = true;
                    foreach (var a in Environment.GetCommandLineArgs()) if (a.Equals("-oz-simprint", StringComparison.OrdinalIgnoreCase)) simPrint = true;
                }
                return simPrint;
            }
        }

        void LateUpdate()
        {
            var g = ZGame.I;
            var w = g ? g.World : null;
            if (!g || w == null) { if (!ReferenceEquals(game, null)) LevelEnded(); physicsThisFrame = 0; return; }   // (game: the level's ZGame, destroyed with its scene)
            if (g != game) LevelStarted(g, w);
            OzFrameTimes.Sample();
            float dtMs = Time.unscaledDeltaTime * 1000f;
            frames++; dtSum += Time.unscaledDeltaTime;
            if (frameMsN < frameMs.Length) frameMs[frameMsN++] = dtMs;
            physicsSum += physicsThisFrame;
            ruleTicksSum += ZGame.RuleTicksLastFrame; ruleMsSum += ZGame.RuleMsLastFrame;
            OzPerfNumbers.Frame(gcAlloc.Valid, gcAlloc.Valid ? gcAlloc.LastValue : 0);                  // 1.3 item 38: garbage and heap (no allocation)
            if (OzFrameTimes.Have)
            {
                if (OzFrameTimes.Main > 0) { mainSum += OzFrameTimes.Main; mainN++; }
                if (OzFrameTimes.Render > 0) { renderSum += OzFrameTimes.Render; renderN++; }
                if (OzFrameTimes.Gpu > 0) { gpuSum += OzFrameTimes.Gpu; gpuN++; }
                if (OzFrameTimes.PresentWait > 0) { waitSum += OzFrameTimes.PresentWait; waitN++; }
            }
            if (batches.Valid) { batchesSum += batches.LastValue; setPassSum += setPass.Valid ? setPass.LastValue : 0; batchN++; }
            if (dtMs > LongFrameMs && frames > 1 && longFrames < LongFramesPerLevel)
            {
                longFrames++;
                Debug.Log(Tag + "long frame " + F(dtMs, "0") + " ms at game " + Clock(w.Time) + " (" + g.level + "): main " + F(OzFrameTimes.Main, "0.0") + ", render " + F(OzFrameTimes.Render, "0.0")
                          + ", gpu " + F(OzFrameTimes.Gpu, "0.0") + ", present wait " + F(OzFrameTimes.PresentWait, "0.0") + " ms (the engine's latest figures); rule ticks " + ZGame.RuleTicksLastFrame
                          + " (" + F(ZGame.RuleMsLastFrame, "0.0") + " ms), physics steps " + physicsThisFrame + (longFrames == LongFramesPerLevel ? "; no more long-frame lines this level" : ""));
            }
            physicsThisFrame = 0;
            if (Time.unscaledTime - minuteStart >= 60f) MinuteLine(g, w);
        }

        void LevelStarted(ZGame g, ZWorld w)
        {
            game = g; minuteStart = Time.unscaledTime; minute = 0; longFrames = 0;
            OzPerfNumbers.LevelStart();                                                                     // 1.3 item 38
            ResetMinute(w);
            Debug.Log(Tag + "machine: graphics " + SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ", " + SystemInfo.graphicsMemorySize + " MB), processor "
                      + SystemInfo.processorType + " (" + SystemInfo.processorCount + " threads, " + SystemInfo.processorFrequency + " MHz), memory " + SystemInfo.systemMemorySize + " MB, screen "
                      + Screen.width + "x" + Screen.height + " " + Screen.fullScreenMode + " at " + F(Screen.currentResolution.refreshRateRatio.value, "0") + " Hz, preset " + OzQuality.Name
                      + ", limit " + GameBoot.CapFps + ", vsync " + QualitySettings.vSyncCount + ", level " + g.level);
            ZWorld.AfterTickHook = SimPrintArg ? (Action<ZWorld>)SimPrintTick : null;
            if (SimPrintArg) Debug.Log("[OpenZ SimPrint] on (" + g.level + ", seed " + g.seed + "): the world's fingerprint after every 100th tick (the state itself, so two runs compare tick for tick whenever the hook came on)");
        }

        void LevelEnded()
        {
            game = null;
            if (ZWorld.AfterTickHook == (Action<ZWorld>)SimPrintTick) ZWorld.AfterTickHook = null;
        }

        void OnDestroy() { if (ZWorld.AfterTickHook == (Action<ZWorld>)SimPrintTick) ZWorld.AfterTickHook = null; }

        void SimPrintTick(ZWorld w)
        {
            if (w.TickIndex % 100 != 0) return;
            Debug.Log("[OpenZ SimPrint] tick " + w.TickIndex + " t " + F(w.Time, "0.00") + " h " + w.RulesFingerprint(1469598103934665603UL).ToString("x16"));
        }

        void ResetMinute(ZWorld w)
        {
            frames = 0; physicsSum = 0; ruleTicksSum = 0; frameMsN = 0; batchN = 0; batchesSum = setPassSum = 0;
            dtSum = ruleMsSum = mainSum = renderSum = gpuSum = waitSum = 0.0; mainN = renderN = gpuN = waitN = 0;
            astar0 = w.Path.AStarRuns; work0 = w.Path.Work; walks0 = w.ApproachWalks; tiles0 = w.ApproachTiles;
            OzPerfNumbers.Reset();                                                                          // 1.3 item 38
            var p = w.Pathing;
            zones0 = p != null ? p.ZoneBuilds : 0; lanes0 = p != null ? p.lanesBuilds : 0; reach0 = p != null ? p.Reach.Rebuilds : 0;
        }

        void MinuteLine(ZGame g, ZWorld w)
        {
            minute++; minuteStart = Time.unscaledTime;
            double cpuMs = CpuLoopMs(); if (cpuMs < cpuBest) cpuBest = cpuMs;
            float worst = 0f;
            if (frameMsN > 0) { Array.Sort(frameMs, 0, frameMsN); worst = frameMs[Math.Min(frameMsN - 1, (int)(frameMsN * 0.99))]; }
            int f = Math.Max(1, frames), ticks = ruleTicksSum;
            var p = w.Pathing;
            var sb = new StringBuilder(640);
            sb.Append(Tag).Append("minute ").Append(minute).Append(" (game ").Append(Clock(w.Time)).Append(", ").Append(g.level).Append(", speed ").Append(F(g.gameSpeed, "0.##")).Append("x): ");
            sb.Append("fps ").Append(F(frames / Math.Max(1e-6, dtSum), "0.0")).Append(" average, worst 1 % frame ").Append(F(worst, "0")).Append(" ms, frame ").Append(F(dtSum * 1000.0 / f, "0.0")).Append(" ms; ");
            sb.Append("main ").Append(Avg(mainSum, mainN)).Append(", render ").Append(Avg(renderSum, renderN)).Append(", gpu ").Append(Avg(gpuSum, gpuN)).Append(", present wait ").Append(Avg(waitSum, waitN)).Append(" ms; ");
            sb.Append("rules ").Append(F(ruleMsSum / f, "0.0")).Append(" ms a frame (").Append(F((double)ticks / f, "0.0")).Append(" ticks, ").Append(F(ticks > 0 ? ruleMsSum / ticks : 0.0, "0.00")).Append(" ms a tick); ");
            sb.Append("physics steps ").Append(F((double)physicsSum / f, "0.0")).Append(" a frame; ");
            OzPerfNumbers.AppendMinute(sb, frames);                                                         // 1.3 item 38: garbage, collections, heap, overlay, hover
            sb.Append("batches ").Append(batchN > 0 ? (batchesSum / batchN).ToString(CultureInfo.InvariantCulture) : "n/a").Append(", set-pass ").Append(batchN > 0 ? (setPassSum / batchN).ToString(CultureInfo.InvariantCulture) : "n/a").Append("; ");
            sb.Append("units ").Append(ZRuleProf.Units(w)).Append(", objects ").Append(w.Objects.Count).Append(", unit views ").Append(g.Views.Count).Append("; ");
            sb.Append("searches: A* ").Append(w.Path.AStarRuns - astar0).Append(" (work ").Append(F((w.Path.Work - work0) / 1e6, "0.00")).Append(" M), approach walks ").Append(w.ApproachWalks - walks0)
              .Append(" (").Append(w.ApproachTiles - tiles0).Append(" tiles); ");
            if (p != null) sb.Append("rebuilds: zones ").Append(p.ZoneBuilds - zones0).Append(", lanes ").Append(p.lanesBuilds - lanes0).Append(", reach ").Append(p.Reach.Rebuilds - reach0).Append("; ");
            sb.Append("cpu loop ").Append(F(cpuMs, "0.000")).Append(" ms (best this run ").Append(F(cpuBest, "0.000")).Append("); preset ").Append(OzQuality.Name).Append(", limit ").Append(GameBoot.CapFps)
              .Append(", active target ").Append(Application.targetFrameRate).Append(", vsync ").Append(QualitySettings.vSyncCount)
              .Append(HeatGuardRuntime.HeldFps > 0 ? ", heat guard holds " + HeatGuardRuntime.HeldFps : "");
            Debug.Log(sb.ToString());
            ResetMinute(w);
        }

        /// <summary>
        /// slows its processor down shows a larger number than its own best of the run.</summary>
        static double CpuLoopMs()
        {
            double best = double.MaxValue;
            for (int k = 0; k < 3; k++)
            {
                long t0 = Stopwatch.GetTimestamp();
                ulong x = 88172645463325252UL;
                for (int i = 0; i < 100000; i++) { x ^= x << 13; x ^= x >> 7; x ^= x << 17; }
                sink += (long)(x & 1023);
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                if (ms < best) best = ms;
            }
            return best;
        }

        static string Avg(double sum, int n) => n > 0 ? F(sum / n, "0.0") : "n/a";
        static string F(double v, string f) => v.ToString(f, CultureInfo.InvariantCulture);
        static string Clock(double s) => $"{(int)(s / 60)}:{(int)(s % 60):00}";
    }
}
