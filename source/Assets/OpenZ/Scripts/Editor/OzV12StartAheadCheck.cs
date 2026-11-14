using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// OpenZ/V1.2/L16 Blue Start: 89 Counted Check (no Play).
    ///  P1-P7 the stepping of a start job (OzStartAheadPump) on routines made here: slices go on at once; a routine it waits on is stepped
    ///        into and its first frame goes back to the frames (the warm renders), with the rest exactly as before; waits go back; the time
    ///        limit; an error drops the job; the first step as a routine's start runs it.
    ///  W     who does it: level 16 blue yes; level 16 red no (as before) unless RedToo; another level no; off no.
    ///  O     the order in the level's first frame: the flyover's camera (the briefings), the low look, the distance rule, then the start
    ///        work, then the land beyond the rim's own switch for the low shots.
    ///  H     the three jobs start through it (the land beyond the rim, the ending's prewarm, the nuke's prewarm); the start trace marks it and
    ///        gives its verdict.
    ///  T     the trace's verdict: the 1.2 test build's blue start trace (if its file is there) FAILS (frames of 100-163 ms after the first
    ///        picture); the same trace without the start jobs' frames (its own frames from 5 s on, as the jobs now run before the first
    ///        picture) PASSES.
    /// Writes its lines to the kit's proof folder (k89_l16_blue_start_freeze/proof/counted_check.txt) when the folder is there.
    /// </summary>
    public static class OzV12StartAheadCheck
    {
        const string Menu = "OpenZ/V1.2/L16 Blue Start: 89 Counted Check (no Play)";
        static readonly List<string> lines = new List<string>();
        static int pass, fail;

        static void Check(string what, bool ok) { if (ok) pass++; else fail++; lines.Add((ok ? "PASS " : "FAIL ") + what); }

        [MenuItem(Menu)]
        public static void Run()
        {
            lines.Clear(); pass = fail = 0;
            try { Pump(); Who(); Order(); Hooks(); Trace(); }
            catch (Exception e) { Check("the check ran to its end (" + e.GetType().Name + ": " + e.Message + ")", false); }
            string head = $"[OzV12StartAheadCheck] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}";
            var sb = new StringBuilder(head).Append('\n');
            foreach (var l in lines) sb.Append("  ").Append(l).Append('\n');
            Debug.Log(sb.ToString());
            string dir = Folder();
            if (dir != null) { try { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "counted_check.txt"), sb.ToString()); } catch (Exception) { } }
        }

        // ------------------------------------------------------------------ P: the stepping
        static int work, warmDrawn, after;
        static bool warmMade;

        static IEnumerator Slices(int n) { for (int i = 0; i < n; i++) { work++; yield return null; } }

        static IEnumerator WarmRender()
        {
            warmMade = true;
            for (int f = 0; f < 4; f++) { warmDrawn++; yield return null; }
            warmMade = false;
        }

        static IEnumerator Prewarm()
        {
            yield return null; yield return null;
            for (int b = 0; b < 18; b++) { work++; yield return null; }
            yield return WarmRender();
            after++;
        }

        static IEnumerator Waits(object wait) { yield return null; work++; yield return wait; after++; }

        static IEnumerator Spin() { while (true) { var s = Stopwatch.StartNew(); while (s.Elapsed.TotalMilliseconds < 1.0) { } work++; yield return null; } }

        static IEnumerator Throws() { yield return null; work++; throw new InvalidOperationException("planted"); }

        static IEnumerator EndsAtOnce() { work++; yield break; }

        static int Drain(IEnumerator rest, int max = 1000) { int k = 0; while (k < max && rest.MoveNext()) k++; return k; }

        static void Pump()
        {
            // P1: a job of 105 slices (the land beyond the rim's shape): its first slice at its start, the other 104 at once
            work = 0; var p = new OzStartAheadPump(Slices(105), OzStartAhead.IsWait);
            var r0 = p.First(); int afterFirst = work; var r = p.Run(1e6);
            Check($"P1 105 slices: the first step at the start ({r0}, {afterFirst} piece), the rest at once ({r}, {work} pieces, {p.Slices} slices gone on, nothing left {p.Empty})",
                r0 == OzStartAheadPump.Result.Queued && afterFirst == 1 && r == OzStartAheadPump.Result.Finished && work == 105 && p.Slices == 104 && p.Empty);

            // P2: the ending's prewarm's shape: two waits, 18 buildings, then a warm render whose first frame goes back to the frames
            work = 0; warmDrawn = 0; after = 0; warmMade = false;
            p = new OzStartAheadPump(Prewarm(), OzStartAhead.IsWait);
            p.First(); r = p.Run(1e6);
            bool stopped = r == OzStartAheadPump.Result.Waits && work == 18 && warmMade && warmDrawn == 1 && after == 0 && p.StopWhy.Contains("WarmRender");
            int frames = Drain(p.Rest());
            Check($"P2 buildings at once ({work} of 18), the warm render made and placed once ({warmDrawn}) before the stop ({p.StopWhy}); the rest in {frames} frames as before "
                + $"(placed {warmDrawn} times, removed {!warmMade}, the line after it {after})",
                stopped && frames == 4 && warmDrawn == 4 && !warmMade && after == 1);

            // P3: a wait for time at the job's own level goes back to the frames with that very wait first
            work = 0; after = 0; var wait = new WaitForSeconds(0.5f);
            p = new OzStartAheadPump(Waits(wait), OzStartAhead.IsWait); p.First(); r = p.Run(1e6);
            var rest = p.Rest(); bool firstIsWait = rest.MoveNext() && ReferenceEquals(rest.Current, wait);
            int more = Drain(rest);
            Check($"P3 a wait for time: back to the frames ({r}, {p.StopWhy}), that wait first ({firstIsWait}), then the rest ({more} more yields, the line after it {after})",
                r == OzStartAheadPump.Result.Waits && work == 1 && firstIsWait && after == 1 && more == 0);

            // P4: a wait that is a routine too (WaitUntil) is a wait, never stepped into
            work = 0; after = 0; var until = new WaitUntil(() => true);
            p = new OzStartAheadPump(Waits(until), OzStartAhead.IsWait); p.First(); r = p.Run(1e6);
            Check($"P4 WaitUntil is kept for the frames ({r}, stepped into {p.Nested})", r == OzStartAheadPump.Result.Waits && p.Nested == 0);

            // P5: the time limit
            work = 0; p = new OzStartAheadPump(Spin(), OzStartAhead.IsWait); p.First();
            var sw = Stopwatch.StartNew(); r = p.Run(20.0); double ms = sw.Elapsed.TotalMilliseconds;
            Check($"P5 the time limit: stopped after {ms:F1} ms of a 20 ms limit ({r}, {work} pieces), the rest goes on in the frames", r == OzStartAheadPump.Result.OverTime && ms < 40.0 && work >= 10);

            // P6: an error drops the job (as a routine that throws stops)
            work = 0; p = new OzStartAheadPump(Throws(), OzStartAhead.IsWait); p.First(); r = p.Run(1e6);
            Check($"P6 an error: {r}, {(p.Error != null ? p.Error.Message : "none")}, nothing left {p.Empty}", r == OzStartAheadPump.Result.Failed && p.Error is InvalidOperationException && p.Empty);

            // P7: the first step as a routine's start runs it
            work = 0; var a = new OzStartAheadPump(EndsAtOnce(), OzStartAhead.IsWait).First();
            var b = new OzStartAheadPump(Waits(new WaitForSeconds(1f)), OzStartAhead.IsWait).First();
            var c = new OzStartAheadPump(FirstWait(), OzStartAhead.IsWait).First();
            var d = new OzStartAheadPump(FirstThrows(), OzStartAhead.IsWait).First();
            Check($"P7 first steps: ends at once {a}, a slice first {b}, a wait first {c}, throws first {d}",
                a == OzStartAheadPump.Result.Finished && b == OzStartAheadPump.Result.Queued && c == OzStartAheadPump.Result.Waits && d == OzStartAheadPump.Result.Failed);
        }

        static IEnumerator FirstWait() { yield return new WaitForSeconds(1f); }
        static IEnumerator FirstThrows() { if (work >= 0) throw new InvalidOperationException("planted"); yield break; }

        // ------------------------------------------------------------------ W: who does it
        static void Who()
        {
            bool on = OzStartAhead.On, red = OzStartAhead.RedToo; string lv = OzStartAhead.Levels;
            try
            {
                OzStartAhead.On = true; OzStartAhead.RedToo = false; OzStartAhead.Levels = "L16";
                bool b16 = OzStartAhead.Wanted("L16", true, out var w1), r16 = OzStartAhead.Wanted("L16", false, out var w2), b02 = OzStartAhead.Wanted("L02", true, out var w3);
                OzStartAhead.RedToo = true; bool r16b = OzStartAhead.Wanted("L16", false, out _);
                OzStartAhead.RedToo = false; OzStartAhead.On = false; bool off = OzStartAhead.Wanted("L16", true, out var w5);
                Check($"W level 16 blue {b16}; level 16 red {r16} ({w2}); level 2 blue {b02} ({w3}); red with RedToo {r16b}; off {off} ({w5})", b16 && !r16 && !b02 && r16b && !off);
            }
            finally { OzStartAhead.On = on; OzStartAhead.RedToo = red; OzStartAhead.Levels = lv; }
        }

        // ------------------------------------------------------------------ O: the order in the first frame
        static int OrderOf(Type t) { var a = t.GetCustomAttribute<DefaultExecutionOrder>(); return a != null ? a.order : 0; }

        static void Order()
        {
            int blue = OrderOf(typeof(OzBlueCameo)), red = OrderOf(typeof(ZodBriefing)), low = OrderOf(typeof(OzL16FlyoverFlora)), rule = OrderOf(typeof(OzL16DrawAhead)),
                me = OrderOf(typeof(OzStartAhead)), land = OrderOf(typeof(OzL16Outskirts));
            Check($"O first frame order: the briefings' flyover camera {blue} / {red}, the low look {low}, the distance rule {rule}, the start work {me}, the land's own switch {land}",
                blue < me && red < me && low < rule && rule < me && me < land);
        }

        // ------------------------------------------------------------------ H: the hooks
        static string Src(string rel) { try { return File.ReadAllText(Path.Combine(Application.dataPath, "OpenZ/Scripts/" + rel)).Replace("\r\n", "\n"); } catch (Exception) { return ""; } }

        static string Code(string text)
        {
            var sb = new StringBuilder();
            foreach (var line in text.Split('\n')) { int i = line.IndexOf("//", StringComparison.Ordinal); sb.Append(i >= 0 ? line.Substring(0, i) : line).Append('\n'); }
            return sb.ToString();
        }

        static void Hooks()
        {
            string land = Code(Src("View/OzL16Outskirts.cs")), end = Code(Src("View/FxV2/OpenZEnding.cs")), nuke = Code(Src("View/FxV3/OpenZNukePrewarm.cs")), tr = Code(Src("View/OzStartFrameTrace.cs"));
            Check("H the land beyond the rim starts through OzStartAhead.Run (no StartCoroutine of BuildSliced left)", land.Contains("OzStartAhead.Run(this, BuildSliced()") && !land.Contains("StartCoroutine(BuildSliced())"));
            Check("H the ending's prewarm starts through OzStartAhead.Run (no StartCoroutine of Prewarm left)", end.Contains("OzStartAhead.Run(this, Prewarm()") && !end.Contains("StartCoroutine(Prewarm())"));
            Check("H the nuke's prewarm starts through OzStartAhead.Run (its routine Warm, no IEnumerator Start left)", nuke.Contains("OzStartAhead.Run(this, Warm()") && nuke.Contains("IEnumerator Warm()") && !nuke.Contains("IEnumerator Start()"));
            Check("H the start trace marks the start work and gives its verdict", tr.Contains("OzStartAhead.Done") && tr.Contains("SeenLine(csv)") && tr.Contains("TakePictures()") && tr.Contains("SeenLongEnough(t)"));
        }

        // ------------------------------------------------------------------ T: the trace's verdict
        static void Trace()
        {
            string csv = null;
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var p = Path.Combine(d.FullName, "work", "records", "milestones", "blue_flyovers_k75c", "frametrace_L16_blue_exe_1.csv");
                if (File.Exists(p)) { csv = p; break; }
            }
            if (csv == null) { lines.Add("NOTE T the 1.2 test build's blue start trace is not on this computer: T skipped"); return; }
            var dt = new List<float>(); var t = new List<float>();
            foreach (var l in File.ReadAllLines(csv))
            {
                var c = l.Split(',');
                if (c.Length < 3 || !float.TryParse(c[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tv)
                    || !float.TryParse(c[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dv)) continue;
                t.Add(tv); dt.Add(dv);
            }
            string before = OzStartFrameTrace.Verdict(dt, t, 10f, 100f, 40f, out bool passBefore, out float longBefore, out float lowBefore);
            Check($"T the 1.2 test build's blue start FAILS the verdict ({before})", !passBefore && longBefore > 100f);
            // the same start with the jobs before the first picture: its first two rows, then its own frames from the jobs' end on (5 s on)
            var dt2 = new List<float> { dt[0], dt[1] }; var t2 = new List<float> { t[0], t[1] }; float shift = 0f;
            for (int i = 2; i < dt.Count; i++) { if (t[i] < 5000f) continue; if (shift == 0f) shift = t[i] - t[1] - dt[i]; dt2.Add(dt[i]); t2.Add(t[i] - shift); }
            string after = OzStartFrameTrace.Verdict(dt2, t2, 10f, 100f, 40f, out bool passAfter, out _, out _);
            Check($"T the same start without the start jobs' frames PASSES ({after})", passAfter);
        }

        static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var staging = Path.Combine(d.FullName, "work", "staging");
                if (!Directory.Exists(staging)) continue;
                foreach (var stage in Directory.GetDirectories(staging))
                {
                    var kit = Path.Combine(stage, "k89_l16_blue_start_freeze");
                    if (Directory.Exists(kit)) return Path.Combine(kit, "proof");
                }
            }
            return null;
        }
    }
}
