#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using Unity.Profiling;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the Unit editor's unit cap, View/OzUeCap.cs): the bench that tells how big a fight the editor can hold (menu OpenZ/V1.2/
    /// Unit Editor/5 Unit Cap Bench (Play), Editor/OzUeCapBenchMenu.cs; the editor only, never in a player). In Play (the MainMenu scene
    /// or the playground) it sets the Game view to 1920 x 1080 and, for 100, 200, 300 and 400 units a side in turn: the field cleared, two
    /// mixed armies of both teams facing each other 20 m apart (grunts, psychos, snipers, toughs, pyros, lasers, jeeps, light tanks,
    /// mechs, heavy tanks, MLRS, gatlings, howitzers; View/OzUePlayground.Cap.cs BenchArmy, past the cap for the bench only), the camera
    /// fitted on them (Home), the fight on, 4 s to settle, then 30 s measured: frames, the average frames a second, the 1 % low
    /// (the frame time 99 % of the frames stay under, as frames a second: the FPS bench's rule), the longest frame, the main thread's and
    /// the graphics chip's time of a frame (the engine's frame timing, else the profiler's counters), the managed memory allocated per
    /// frame, the units (each side is topped up to its number again once a second when the fight took it under 95 %; those frames are
    /// left out). The frame rate limit in force is written down and never lifted (heat: the game's own guard and the editor's limit stay).
    /// 1.3 item 8 (the unit cap without losing fps, above all at x4): a V1.3 run (menu OpenZ/V1.3/Unit Editor/8 Unit Cap Bench x1 + x4)
    /// runs every size at x1 AND at x4, and each row also gives the worst 1 % frame in ms, the rules' time per frame and per tick
    /// (ZGame.RuleMsLastFrame / RuleTicksLastFrame, the Stopwatch of the game's own minute line) with its split (orders, path, targets,
    /// bot, move, combat, other: Sim/ZRuleProf.cs), the rule ticks a frame, the speed the field really reached (ticks a second / 100: under
    /// 4 when the 40-ticks-a-frame cap bites), the rules' share of the main thread, and the engine's script, animation and physics markers
    /// plus the field's hover marker (OzUe.Hover, View/OzUeField.cs) where the profiler gives them. Then 6 SAMPLE FIGHTs at once at x4 (the
    /// 300-a-side field). Writes unit_cap_bench.txt (1.2 run) or unit_cap_bench_v13.txt (V1.3 run) and one console line "[OpenZ UnitEditor
    /// cap bench] DONE ...". The Game view's size, the speed and PLAY / PAUSE come back at the end; the field is cleared.
    /// </summary>
    public static class OzUeCapBench
    {
        const string Tag = "[OpenZ UnitEditor cap bench]";
        public static readonly int[] PerSide = { 100, 200, 300, 400 };
        public const float Settle = 4f, Measure = 30f, TopUpAt = 0.95f;
        /// <summary>1.3 item 8: the speeds of a V1.3 run (the 1.2 run: x1 only).</summary>
        public static readonly float[] SpeedsV13 = { 1f, 4f };
        public const int SampleFights = 6, HoverPoints = 400, HoverPerSide = 300;
        public static string Report = "not run";
        public static bool Running => runner;
        static OzUeCapRunner runner;
        static readonly StringBuilder lines = new StringBuilder();
        static string dir;
        static bool v13;
        static int gameView0 = -1; static float speed0 = 1f; static bool playing0 = true, kept;

        /// <summary>The 1.2 bench (x1, unit_cap_bench.txt in the 1.2 proof folder).</summary>
        public static void Begin() => Begin(false);

        /// <summary>v13Run = the 1.3 item 8 bench: x1 and x4, the extra columns, 6 SAMPLE FIGHTs at x4, the same-hover check.</summary>
        public static void Begin(bool v13Run)
        {
            if (runner) { Debug.LogWarning(Tag + " a run is still going"); return; }
            lines.Clear(); Report = "running"; kept = false; v13 = v13Run;
            dir = OutDir(); Directory.CreateDirectory(dir);
            var go = new GameObject("OzUe_CapBench"); UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<OzUeCapRunner>();
            runner.Push(Main());
            Debug.Log(Tag + " started: the report goes to " + dir);
        }

        public static void Abort()
        {
            if (!runner) { Debug.Log(Tag + " no run is going"); return; }
            lines.AppendLine("STOPPED by hand");
            Finish();
        }

        internal static void Crashed(Exception e) { lines.AppendLine("STOPPED on an exception: " + e.GetType().Name + ": " + e.Message); Finish(); }

        static string ReportName => v13 ? "unit_cap_bench_v13.txt" : "unit_cap_bench.txt";

        static string OutDir()
        {
            try
            {
                string staging = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging"));
                if (v13)
                {
                    string d13 = Path.Combine(staging, "v1_3", "solutions", "item08_ue_unit_cap");
                    if (Directory.Exists(d13)) return Path.Combine(d13, "proof");
                }
                if (Directory.Exists(staging))
                {
                    var ds = Directory.GetDirectories(staging, "v1_2*");
                    Array.Sort(ds, StringComparer.Ordinal);
                    for (int i = ds.Length - 1; i >= 0; i--) if (Directory.Exists(Path.Combine(ds[i], "k6e_unit_editor_owner_test"))) return Path.Combine(ds[i], "k6e_unit_editor_owner_test", "proof");
                }
            }
            catch (Exception) { }
            return Path.Combine(Application.persistentDataPath, "UnitEditorCapBench");
        }

        static void Finish()
        {
            try { Restore(); } catch (Exception e) { lines.AppendLine("the settings could not all be put back: " + e.Message); }
            Report = Tag + " DONE -> " + Path.Combine(dir ?? "", ReportName);
            try { File.WriteAllText(Path.Combine(dir, ReportName), Report + "\n\n" + lines); } catch (Exception e) { Debug.LogWarning(Tag + " " + ReportName + " not written: " + e.Message); }
            Debug.Log(Report);
            Debug.Log(lines.ToString());
            if (runner) { runner.Stop(); UnityEngine.Object.Destroy(runner.gameObject); }
            runner = null;
        }

        static void Restore()
        {
            OzUeCap.Bypass = false;
            OzUeField.HoverCull = true;
            if (OzUnitEditor.UiBuilt)
            {
                OzUePick.Unpick(); OzUePick.ClearInspect(); OzUePlayground.ClearField();
                if (kept) { OzUeControls.SetSpeed(speed0); OzUeControls.SetPlaying(playing0); }
            }
            if (gameView0 >= 0) OzFinalGateView.Select(gameView0);
            gameView0 = -1;
        }

        static IEnumerator Main()
        {
            if (!OzUnitEditor.UiBuilt)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>();
                var go = typeof(OpenZMainMenu).GetMethod("GoUnitEditor", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (!menu || go == null) { lines.AppendLine("FAILED: the bench starts in the MainMenu scene or in the playground"); Finish(); yield break; }
                go.Invoke(menu, null);
                float t0 = Time.realtimeSinceStartup;
                while (!OzUnitEditor.UiBuilt && Time.realtimeSinceStartup - t0 < 45f) yield return 1;
                if (!OzUnitEditor.UiBuilt) { lines.AppendLine("FAILED: the playground did not open from the main menu (45 s)"); Finish(); yield break; }
                yield return 1.5f;
            }
            gameView0 = OzFinalGateView.SelectedIndex();
            speed0 = OzUeControls.Speed; playing0 = OzUeControls.Playing; kept = true;
            OzFinalGateView.SetFixed(1920, 1080, "OpenZ 1920x1080", out string info);
            float tw = Time.realtimeSinceStartup;
            while ((Screen.width != 1920 || Screen.height != 1080) && Time.realtimeSinceStartup - tw < 5f) yield return 1;
            yield return 1.0f;
            lines.AppendLine("Game view " + Screen.width + " x " + Screen.height + " (" + info + "), graphics preset " + OzQualityName() + ", UI SCALE " + Mathf.RoundToInt(OzText.UiScale * 100f) + " %");
            lines.AppendLine("frame rate limit in force (never lifted by the bench): target " + Application.targetFrameRate + ", vSync " + QualitySettings.vSyncCount + ", the game's cap " + GameBoot.CapFps
                             + (Application.isEditor ? "; editor numbers: the editor's own work is in every frame" : ""));
            lines.AppendLine("the cap now: " + OzUeCap.PerSide + " a side, " + OzUeCap.Total + " in all (the bench passes it)");
            if (v13) lines.AppendLine("the field's hover cull (View/OzUeField.cs HoverCull): " + (OzUeField.HoverCull ? "on" : "off") + "; the rule tick timer (Sim/ZRuleProf.cs) " + (ZRuleProf.On ? "on" : "OFF: no rule split"));
            lines.AppendLine();
            var results = new List<Result>();
            float[] speeds = v13 ? SpeedsV13 : new[] { 1f };
            foreach (float sp in speeds)
                foreach (int n in PerSide)
                {
                    var r = new Result { perSide = n, speed = sp };
                    yield return Run(r);
                    lines.AppendLine(r.Line());
                    results.Add(r);
                }
            Result fights = null;
            if (v13)
            {
                fights = new Result { perSide = -1, speed = 4f };
                yield return RunSampleFights(fights);
                lines.AppendLine(fights.Line());
                yield return HoverSame();
            }
            lines.AppendLine();
            if (!v13)
            {
                lines.AppendLine("DATA per side | avg fps | 1 % low fps | longest ms | main thread ms | GPU ms | KB allocated a frame | units blue / red mean | top-ups");
                foreach (var d in results) lines.AppendLine("DATA " + d.Data12());
            }
            else
            {
                lines.AppendLine("DATA per side | speed | avg fps | 1 % low fps | worst 1 % ms | longest ms | main thread ms | GPU ms | rules ms a frame | rules ms a tick | ticks a frame | speed reached | rules share of main | KB allocated a frame | units blue / red mean | top-ups");
                foreach (var d in results) lines.AppendLine("DATA " + d.Data());
                if (fights != null) lines.AppendLine("DATA " + fights.Data());
                lines.AppendLine();
                lines.AppendLine("SPLIT per side | speed | rules ms per tick split | Update ms | LateUpdate ms | animation ms | physics ms | hover ms (frame means; - = the profiler gave none)");
                foreach (var d in results) lines.AppendLine("SPLIT " + d.Split());
                if (fights != null) lines.AppendLine("SPLIT " + fights.Split());
                lines.AppendLine();
                lines.AppendLine("THE CAP RULE (1.3 item 8): the new PerSide is the largest size whose x4 row, AFTER the speed-ups, keeps an average fps and a 1 % low");
                lines.AppendLine("at least as good as the 200-a-side x4 row BEFORE them (the 1.2 code); Total = 2 x PerSide; SAMPLE FIGHT max follows (PerSide / 30).");
            }
            lines.AppendLine("work of a frame = the slower of the main thread and the graphics chip; under a frame rate limit the average frames a second cannot pass it,");
            lines.AppendLine("so the cap is read from the work of a frame and the 1 % low. Pick the cap: the most units a side whose 1 % low stays fluid on the PC it is meant for.");
            Finish();
        }

        static string OzQualityName() { try { return QualitySettings.names[QualitySettings.GetQualityLevel()]; } catch (Exception) { return "?"; } }

        internal sealed class Result
        {
            public int perSide, frames, topUps, unitSamples; public float speed = 1f, seconds, timed, longest, fps, low1, p99, mainMs = -1f, gpuMs = -1f, allocKb = -1f; public double blueSum, redSum;
            public int blue0, red0; public string mainFrom = "none", gpuFrom = "none", allocFrom = "none";
            public double ruleMs; public long ruleTicks; public string ruleSplit = "-";
            public float updMs = -1f, lateMs = -1f, animMs = -1f, physMs = -1f, hoverMs = -1f;
            string Size => perSide < 0 ? SampleFights + " SAMPLE FIGHTs" : perSide + " a side";
            float RuleFrame => frames > 0 ? (float)(ruleMs / frames) : -1f;
            float RuleTick => ruleTicks > 0 ? (float)(ruleMs / ruleTicks) : -1f;
            float TicksFrame => frames > 0 ? ruleTicks / (float)frames : 0f;
            float Reached => timed > 0f ? ruleTicks / timed / 100f : 0f;                                   // over the measured frames only (the top-up frames are left out of both)
            float RuleShare => mainMs > 0f && frames > 0 ? RuleFrame / mainMs : -1f;
            float BlueMean => unitSamples > 0 ? (float)(blueSum / unitSamples) : 0f;
            float RedMean => unitSamples > 0 ? (float)(redSum / unitSamples) : 0f;
            public string Line() => Size + " x" + F0(speed) + ": placed blue " + blue0 + " / red " + red0 + "; " + frames + " frames in " + F1(seconds) + " s, " + F1(fps) + " fps average, 1 % low " + F1(low1)
                                    + " fps (worst 1 % " + F1(p99 * 1000f) + " ms), longest frame " + F1(longest * 1000f) + " ms, main thread " + Ms(mainMs) + " (" + mainFrom + "), GPU " + Ms(gpuMs) + " (" + gpuFrom + "), rules "
                                    + Ms(RuleFrame) + " a frame / " + Ms2(RuleTick) + " a tick, " + F1(TicksFrame) + " ticks a frame, speed reached x" + F2(Reached) + ", allocated "
                                    + (allocKb >= 0f ? F1(allocKb) + " KB a frame" : "not measured") + " (" + allocFrom + "); units on the field blue " + F1(BlueMean) + " / red " + F1(RedMean) + " (mean), top-ups " + topUps;
            /// <summary>The 1.2 bench's DATA row, unchanged.</summary>
            public string Data12() => perSide + " | " + F1(fps) + " | " + F1(low1) + " | " + F1(longest * 1000f) + " | " + Ms(mainMs) + " | " + Ms(gpuMs) + " | " + (allocKb >= 0f ? F1(allocKb) : "-") + " | "
                                    + F1(BlueMean) + " / " + F1(RedMean) + " | " + topUps;
            public string Data() => Size + " | x" + F0(speed) + " | " + F1(fps) + " | " + F1(low1) + " | " + F1(p99 * 1000f) + " | " + F1(longest * 1000f) + " | " + Ms(mainMs) + " | " + Ms(gpuMs) + " | "
                                    + Ms(RuleFrame) + " | " + Ms2(RuleTick) + " | " + F1(TicksFrame) + " | x" + F2(Reached) + " | " + (RuleShare >= 0f ? Mathf.RoundToInt(RuleShare * 100f) + " %" : "-") + " | "
                                    + (allocKb >= 0f ? F1(allocKb) : "-") + " | " + F1(BlueMean) + " / " + F1(RedMean) + " | " + topUps;
            public string Split() => Size + " | x" + F0(speed) + " | " + ruleSplit + " | " + MsD(updMs) + " | " + MsD(lateMs) + " | " + MsD(animMs) + " | " + MsD(physMs) + " | " + MsD(hoverMs);
        }

        static IEnumerator Run(Result r)
        {
            OzUeCap.Bypass = true;
            OzUePick.Unpick(); OzUePick.ClearInspect(); OzUePlayground.ClearField();
            OzUeControls.CompassHome(); OzUeControls.SetSpeed(r.speed); OzUeControls.SetPlaying(true);
            yield return 1.0f;
            var w = OzUnitEditor.I.World;
            Vector3 c = new Vector3(w.Map.width, 0f, -w.Map.height), right = Vector3.right, fwd = Vector3.forward;   // the map's middle (m), its own axes
            r.blue0 = OzUePlayground.BenchArmy(Team.Blue, r.perSide, c, right, fwd, -1f, 0, out int colB);
            r.red0 = OzUePlayground.BenchArmy(Team.Red, r.perSide, c, right, fwd, 1f, 0, out int colR);
            yield return 2;
            OzUeControls.Fit();
            yield return Settle;
            var s = runner.Sampler; s.Begin();
            float t0 = Time.realtimeSinceStartup, next = 1f;
            while (Time.realtimeSinceStartup - t0 < Measure)
            {
                float t = Time.realtimeSinceStartup - t0;
                if (t >= next)
                {
                    next = t + 1f;
                    int nb = OzUePlayground.CountUnits(Team.Blue), nr = OzUePlayground.CountUnits(Team.Red);
                    r.blueSum += nb; r.redSum += nr; r.unitSamples++;
                    if (nb < r.perSide * TopUpAt) { s.Exclude(0.5f); OzUePlayground.BenchArmy(Team.Blue, r.perSide - nb, c, right, fwd, -1f, colB, out colB); r.topUps++; }
                    if (nr < r.perSide * TopUpAt) { s.Exclude(0.5f); OzUePlayground.BenchArmy(Team.Red, r.perSide - nr, c, right, fwd, 1f, colR, out colR); r.topUps++; }
                }
                yield return 1;
            }
            s.End(r);
            OzUeCap.Bypass = false;
            OzUePlayground.ClearField();
            yield return 1.0f;
        }

        /// <summary></summary>
        static IEnumerator RunSampleFights(Result r)
        {
            OzUePick.Unpick(); OzUePick.ClearInspect(); OzUePlayground.ClearField();
            OzUeControls.CompassHome(); OzUeControls.SetSpeed(r.speed); OzUeControls.SetPlaying(false);
            yield return 1.0f;
            int groups = 0;
            for (int i = 0; i < SampleFights; i++) { groups += OzUePlayground.SampleFight(); yield return 2; }
            r.blue0 = OzUePlayground.CountUnits(Team.Blue); r.red0 = OzUePlayground.CountUnits(Team.Red);
            lines.AppendLine(SampleFights + " SAMPLE FIGHTs: " + OzUePlayground.SampleFightsOnField + " on the field, " + groups + " groups (" + (SampleFights * 12) + " wanted)"
                             + (OzUePlayground.LastSampleWhy != null ? ", last refusal: " + OzUePlayground.LastSampleWhy : ""));
            OzUeControls.Fit(); OzUeControls.SetPlaying(true);
            yield return Settle;
            var s = runner.Sampler; s.Begin();
            float t0 = Time.realtimeSinceStartup, next = 1f;
            while (Time.realtimeSinceStartup - t0 < Measure)
            {
                float t = Time.realtimeSinceStartup - t0;
                if (t >= next) { next = t + 1f; r.blueSum += OzUePlayground.CountUnits(Team.Blue); r.redSum += OzUePlayground.CountUnits(Team.Red); r.unitSamples++; }
                yield return 1;
            }
            s.End(r);
            OzUePlayground.ClearField();
            yield return 1.0f;
        }

        /// <summary>1.3 item 8: the hover cull changes nothing: over a 300-a-side field (paused, so both passes see the same frame), the hover
        /// at 400 screen points (random points of the free field, every 3rd on a unit's drawn point) with the cull on and off.</summary>
        static IEnumerator HoverSame()
        {
            OzUeCap.Bypass = true;
            OzUePick.Unpick(); OzUePick.ClearInspect(); OzUePlayground.ClearField();
            OzUeControls.CompassHome(); OzUeControls.SetSpeed(1f); OzUeControls.SetPlaying(true);
            yield return 1.0f;
            var ed = OzUnitEditor.I; var w = ed.World; var g = ed.Game;
            Vector3 c = new Vector3(w.Map.width, 0f, -w.Map.height);
            OzUePlayground.BenchArmy(Team.Blue, HoverPerSide, c, Vector3.right, Vector3.forward, -1f, 0, out _);
            OzUePlayground.BenchArmy(Team.Red, HoverPerSide, c, Vector3.right, Vector3.forward, 1f, 0, out _);
            yield return 2;
            OzUeControls.Fit();
            yield return Settle;
            OzUeControls.SetPlaying(false);
            yield return 3;
            var cam = OzUeDraw.Cam; var units = new List<ZObject>();
            foreach (var o in w.Objects) if (o.IsUnit && !o.Destroyed && o.KillAt < 0) units.Add(o);
            var rng = new System.Random(8);
            int same = 0, diff = 0, onUnit = 0, hits = 0; var bad = new StringBuilder();
            var ff = OzUeInsets.FreeFieldScreen;
            for (int i = 0; i < HoverPoints; i++)
            {
                Vector2 p;
                if (i % 3 == 0 && units.Count > 0 && cam)
                {
                    var u = units[rng.Next(units.Count)];
                    var at = g.Views.TryGetValue(u.Id, out var v) && v ? v.transform.position + Vector3.up * 0.8f : g.CenterWorld(u);
                    var sp = cam.WorldToScreenPoint(at); p = new Vector2(sp.x, sp.y); onUnit++;
                }
                else p = new Vector2(ff.x + (float)rng.NextDouble() * ff.width, ff.y + (float)rng.NextDouble() * ff.height);
                var a = OzUeField.HoverProbe(p, true); var b = OzUeField.HoverProbe(p, false);
                if (a == b) same++; else { diff++; if (bad.Length < 400) bad.Append(" (").Append(F0(p.x)).Append(", ").Append(F0(p.y)).Append(')'); }
                if (b != null) hits++;
            }
            lines.AppendLine((diff == 0 ? "PASS" : "FAIL") + " same hover with the cull on and off: " + same + " of " + HoverPoints + " points the same unit (" + onUnit + " aimed at a unit, " + hits + " on something), "
                             + diff + " different" + bad + "; the last pass: " + OzUeField.HoverSkipped + " units skipped, " + OzUeField.HoverAsked + " asked");
            OzUeControls.SetPlaying(true);
            OzUeCap.Bypass = false;
            OzUePlayground.ClearField();
            yield return 1.0f;
        }

        static string F0(float v) => v.ToString("0", CultureInfo.InvariantCulture);
        static string F1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        static string F2(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        static string Ms(float v) => v >= 0f ? F1(v) + " ms" : "not given";
        static string Ms2(float v) => v >= 0f ? F2(v) + " ms" : "not given";
        static string MsD(float v) => v >= 0f ? F2(v) : "-";

        /// <summary>The bench's per-frame sampler (after every editor part drew its frame: order 2000).</summary>
        [DefaultExecutionOrder(2000)]
        internal sealed class Sampler : MonoBehaviour
        {
            readonly List<float> dt = new List<float>(4096), main = new List<float>(4096), gpu = new List<float>(4096);
            readonly FrameTiming[] ft = new FrameTiming[1];
            ProfilerRecorder gcRec, mainRec, gpuRec, updRec, lateRec, animRec, physRec, hoverRec;
            double upd, late, anim, phys, hover; int updN, lateN, animN, physN, hoverN;
            bool on; float excludeUntil, t0; double alloc; int allocFrames;
            double ruleMs; long ruleTicks;
            readonly ZRuleProf.Window split = new ZRuleProf.Window();

            internal void Begin()
            {
                dt.Clear(); main.Clear(); gpu.Clear(); alloc = 0; allocFrames = 0; ruleMs = 0; ruleTicks = 0;
                upd = late = anim = phys = hover = 0; updN = lateN = animN = physN = hoverN = 0;
                try { gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"); } catch (Exception) { }
                try { mainRec = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1); } catch (Exception) { }
                try { gpuRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 1); } catch (Exception) { }
                // 1.3 item 8: the engine's player-loop markers and the field's hover marker (a name the profiler does not know records nothing: "-")
                try { updRec = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Update.ScriptRunBehaviourUpdate", 1); } catch (Exception) { }
                try { lateRec = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "PreLateUpdate.ScriptRunBehaviourLateUpdate", 1); } catch (Exception) { }
                try { animRec = ProfilerRecorder.StartNew(ProfilerCategory.Animation, "PreLateUpdate.DirectorUpdateAnimationBegin", 1); } catch (Exception) { }
                try { physRec = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "FixedUpdate.PhysicsFixedUpdate", 1); } catch (Exception) { }
                try { hoverRec = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "OzUe.Hover", 1); } catch (Exception) { }
                split.Mark();
                t0 = Time.realtimeSinceStartup; on = true;
            }

            internal void Exclude(float seconds) { excludeUntil = Time.realtimeSinceStartup + seconds; }

            static void Take(ProfilerRecorder r, ref double sum, ref int n) { if (r.Valid && r.LastValue > 0) { sum += r.LastValue * 1e-6; n++; } }

            void LateUpdate()
            {
                if (!on || Time.realtimeSinceStartup < excludeUntil) return;
                dt.Add(Time.unscaledDeltaTime);
                ruleMs += ZGame.RuleMsLastFrame; ruleTicks += ZGame.RuleTicksLastFrame;                                // 1.3 item 8: the rules' own Stopwatch (View/ZGame.cs Update)
                FrameTimingManager.CaptureFrameTimings();
                bool timed = FrameTimingManager.GetLatestTimings(1, ft) > 0;
                if (timed && ft[0].cpuMainThreadFrameTime > 0.0) main.Add((float)ft[0].cpuMainThreadFrameTime);
                else if (mainRec.Valid && mainRec.LastValue > 0) main.Add(mainRec.LastValue * 1e-6f);
                if (timed && ft[0].gpuFrameTime > 0.0) gpu.Add((float)ft[0].gpuFrameTime);
                else if (gpuRec.Valid && gpuRec.LastValue > 0) gpu.Add(gpuRec.LastValue * 1e-6f);
                if (gcRec.Valid) { alloc += gcRec.LastValue; allocFrames++; }
                Take(updRec, ref upd, ref updN); Take(lateRec, ref late, ref lateN); Take(animRec, ref anim, ref animN); Take(physRec, ref phys, ref physN); Take(hoverRec, ref hover, ref hoverN);
            }

            internal void End(Result r)
            {
                on = false;
                r.seconds = Time.realtimeSinceStartup - t0;
                r.frames = dt.Count;
                float sum = 0f, longest = 0f; foreach (var x in dt) { sum += x; if (x > longest) longest = x; }
                r.fps = sum > 0f ? dt.Count / sum : 0f;
                r.timed = sum;
                r.longest = longest;
                if (dt.Count > 0) { var s = new List<float>(dt); s.Sort(); float p99 = s[Mathf.Clamp(Mathf.CeilToInt(s.Count * 0.99f) - 1, 0, s.Count - 1)]; r.p99 = p99; r.low1 = p99 > 0f ? 1f / p99 : 0f; }
                r.mainMs = Mean(main); r.gpuMs = Mean(gpu);
                bool timing = FrameTimingManager.IsFeatureEnabled();
                r.mainFrom = main.Count == 0 ? "not given" : timing ? "the engine's frame timing" : "the profiler's Main Thread counter";
                r.gpuFrom = gpu.Count == 0 ? "not given" : timing ? "the engine's frame timing" : "the profiler's GPU Frame Time counter";
                r.allocKb = allocFrames > 0 ? (float)(alloc / allocFrames / 1024.0) : -1f;
                r.allocFrom = gcRec.Valid ? "the profiler counter GC Allocated In Frame" : "not given";
                r.ruleMs = ruleMs; r.ruleTicks = ruleTicks;
                r.ruleSplit = split.Ticks > 0 ? split.Take() : "-";
                r.updMs = updN > 0 ? (float)(upd / updN) : -1f; r.lateMs = lateN > 0 ? (float)(late / lateN) : -1f; r.animMs = animN > 0 ? (float)(anim / animN) : -1f;
                r.physMs = physN > 0 ? (float)(phys / physN) : -1f; r.hoverMs = hoverN > 0 ? (float)(hover / hoverN) : -1f;
                DisposeAll();
            }

            void DisposeAll()
            {
                if (gcRec.Valid) gcRec.Dispose();
                if (mainRec.Valid) mainRec.Dispose();
                if (gpuRec.Valid) gpuRec.Dispose();
                if (updRec.Valid) updRec.Dispose();
                if (lateRec.Valid) lateRec.Dispose();
                if (animRec.Valid) animRec.Dispose();
                if (physRec.Valid) physRec.Dispose();
                if (hoverRec.Valid) hoverRec.Dispose();
            }

            static float Mean(List<float> l) { if (l.Count == 0) return -1f; double s = 0; foreach (var x in l) s += x; return (float)(s / l.Count); }

            void OnDestroy() { DisposeAll(); }
        }
    }

    /// <summary>Steps the cap bench once a frame and survives a scene load (as the frame-cost check's runner).</summary>
    internal sealed class OzUeCapRunner : MonoBehaviour
    {
        readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        int waitFrame; float waitUntil; bool stopped;
        internal OzUeCapBench.Sampler Sampler { get; private set; }
        void Awake() { Sampler = gameObject.AddComponent<OzUeCapBench.Sampler>(); }
        internal void Push(IEnumerator e) { stack.Push(e); }
        internal void Stop() { stopped = true; stack.Clear(); }

        void Update()
        {
            if (stopped) return;
            if (stack.Count == 0) { stopped = true; return; }
            if (Time.frameCount < waitFrame || Time.realtimeSinceStartup < waitUntil) return;
            var top = stack.Peek(); bool more;
            try { more = top.MoveNext(); }
            catch (Exception e) { Debug.LogException(e); stopped = true; stack.Clear(); OzUeCapBench.Crashed(e); return; }
            if (stopped) return;
            if (!more) { stack.Pop(); return; }
            var c = top.Current;
            if (c is IEnumerator child) stack.Push(child);
            else if (c is float s) waitUntil = Time.realtimeSinceStartup + s;
            else waitFrame = Time.frameCount + (c is int n ? Mathf.Max(1, n) : 1);
        }
    }
}
#endif
