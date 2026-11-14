// OpenZ (GPL-3.0)
// Row 0 of a trace is the level's first frame: its picture is the first one shown. Row 1's frame length holds the loading itself (the
// first frame after a level loads is counted from the loading screen's last frame) and the first frame's own work: none of it is seen
// as a frame of the game. From row 2 on the rows are the frames after it (row 2 also holds the wait for the graphics card to show the
// first picture: it is made while the next frame's scripts run).
// The pictures themselves: where the build gives the frame timings (Frame Timing Stats), every finished frame's moment on screen (the
// later of its hand-over and the graphics card's end) is kept too; the first picture after the level's load is the first one, and the
// time between pictures is what the player sees. The verdict of the next start bench: in the first Seconds after the first picture no
// frame over SeenMaxMs and the 1 % low (the 99th percentile frame, as the frame rate bench counts it) over SeenLow1Fps, on the
// pictures when they are given, else on the rows from row 2. Both are written; the pictures also to <trace>_pictures.csv.
// Play check: OzStartFrameTrace.LastSeen, LastPass.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzStartFrameTrace
    {
        public static float SeenMaxMs = 100f, SeenLow1Fps = 40f;
        public static string LastSeen = "none yet";
        public static bool LastPass, LastOnPictures;
        public static float LastLongestSeen, LastLow1Seen;

        readonly FrameTiming[] ftBuf = new FrameTiming[8];
        readonly List<(ulong start, ulong shown)> pics = new List<(ulong, ulong)>(2048);
        readonly List<(ulong start, ulong shown)> picNew = new List<(ulong, ulong)>(8);
        ulong lastStart, tick0;

        /// <summary>The trace ends this long after the first picture too (the first rows hold the loading).</summary>
        bool SeenLongEnough(float t) => ts.Count >= 2 && t - ts[1] / 1000f >= Seconds;

        /// <summary>From Update, every frame: the frames the graphics card has finished since the last call (their start and their moment on screen).</summary>
        void TakePictures()
        {
            if (tick0 == 0) tick0 = (ulong)System.Diagnostics.Stopwatch.GetTimestamp();
            FrameTimingManager.CaptureFrameTimings();
            uint n = FrameTimingManager.GetLatestTimings((uint)ftBuf.Length, ftBuf);
            picNew.Clear();
            for (int i = 0; i < n && i < ftBuf.Length; i++)
            {
                var f = ftBuf[i];
                if (f.frameStartTimestamp == 0 || f.frameStartTimestamp <= lastStart) continue;
                ulong shown = f.cpuTimeFrameComplete > f.cpuTimePresentCalled ? f.cpuTimeFrameComplete : f.cpuTimePresentCalled;
                if (shown > 0) picNew.Add((f.frameStartTimestamp, shown));
            }
            picNew.Sort((a, b) => a.start.CompareTo(b.start));
            foreach (var p in picNew) { pics.Add(p); if (p.start > lastStart) lastStart = p.start; }
        }

        string SeenLine(string csv)
        {
            string rowsLine = Verdict(dts, ts, Seconds, SeenMaxMs, SeenLow1Fps, out bool rowsPass, out float rowsLong, out float rowsLow);
            double freq = FrameTimingManager.GetCpuTimerFrequency();
            var gaps = new List<float>(); var at = new List<float>();
            int first = -1;
            if (freq > 0)
                for (int i = 0; i < pics.Count; i++)
                {
                    if (pics[i].shown < tick0) continue;                                                  // the loading screen's own pictures
                    if (first < 0) { first = i; continue; }
                    float sinceFirst = (float)(((double)pics[i].shown - pics[first].shown) * 1000.0 / freq);
                    if (sinceFirst > Seconds * 1000f) break;
                    gaps.Add((float)(((double)pics[i].shown - pics[i - 1].shown) * 1000.0 / freq)); at.Add(sinceFirst);
                }
            LastOnPictures = gaps.Count >= 10;
            if (!LastOnPictures)
            {
                LastPass = rowsPass; LastLongestSeen = rowsLong; LastLow1Seen = rowsLow;
                LastSeen = rowsLine + "\n  the pictures' moments: not given by this build (" + pics.Count + " frame timings): the verdict is the rows'";
                return LastSeen;
            }
            float longest = 0f, when = 0f; int over = 0; double sum = 0;
            for (int i = 0; i < gaps.Count; i++) { sum += gaps[i]; if (gaps[i] > longest) { longest = gaps[i]; when = at[i] / 1000f; } if (gaps[i] > SeenMaxMs) over++; }
            var s = new List<float>(gaps); s.Sort();
            float p99 = s[Mathf.Clamp(Mathf.CeilToInt(0.99f * s.Count) - 1, 0, s.Count - 1)], low1 = p99 > 0f ? 1000f / p99 : 0f;
            LastPass = longest <= SeenMaxMs && low1 >= SeenLow1Fps; LastLongestSeen = longest; LastLow1Seen = low1;
            float firstAt = (float)(((double)pics[first].shown - tick0) * 1000.0 / freq);
            LastSeen = rowsLine + $"\n  the pictures: the first one {firstAt / 1000f:F2} s after the trace began (the level's load); in the {Seconds:F0} s after it {gaps.Count} more, "
                     + $"{(sum > 0 ? gaps.Count * 1000.0 / sum : 0):F1} fps mean, the longest time between two {longest:F0} ms at {when:F2} s after the first, over {SeenMaxMs:F0} ms: {over}, "
                     + $"1 % low {low1:F1} fps: {(LastPass ? "PASS" : "FAIL")} (the verdict, on the pictures)";
            try
            {
                var sb = new System.Text.StringBuilder("picture,start_ms,shown_ms,since_first_ms\n");
                for (int i = 0; i < pics.Count; i++)
                {
                    double st = ((double)pics[i].start - tick0) * 1000.0 / freq, sh = ((double)pics[i].shown - tick0) * 1000.0 / freq;
                    double sf = first >= 0 && i >= first ? ((double)pics[i].shown - pics[first].shown) * 1000.0 / freq : -1.0;
                    sb.Append(i).Append(',').Append(st.ToString("0.0")).Append(',').Append(sh.ToString("0.0")).Append(',').Append(sf.ToString("0.0")).Append('\n');
                }
                if (!string.IsNullOrEmpty(csv)) System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(csv, null) + "_pictures.csv", sb.ToString());
            }
            catch (System.Exception) { }
            return LastSeen;
        }

        /// <summary>The verdict on rows 2.. of a trace (frame lengths dt and times t, both ms) within `seconds` after row 1.</summary>
        public static string Verdict(List<float> dt, List<float> t, float seconds, float maxMs, float low1Fps, out bool pass, out float longest, out float low1)
        {
            pass = false; longest = 0f; low1 = 0f;
            if (dt == null || t == null || dt.Count < 3 || t.Count < dt.Count) return "after the first picture: too few frames (" + (dt != null ? dt.Count : 0) + ")";
            float shown = t[1], at = 0f; int over = 0; double sum = 0;
            var seen = new List<float>(dt.Count);
            for (int i = 2; i < dt.Count; i++)
            {
                if (t[i] - shown > seconds * 1000f) break;
                float d = dt[i]; seen.Add(d); sum += d;
                if (d > longest) { longest = d; at = t[i] / 1000f; }
                if (d > maxMs) over++;
            }
            var s = new List<float>(seen); s.Sort();
            float p99 = s.Count > 0 ? s[Mathf.Clamp(Mathf.CeilToInt(0.99f * s.Count) - 1, 0, s.Count - 1)] : 0f;
            low1 = p99 > 0f ? 1000f / p99 : 0f;
            pass = seen.Count > 0 && longest <= maxMs && low1 >= low1Fps;
            return $"after the first picture, the rows ({shown / 1000f:F2} s after the level's load; row 1, the loading and the first frame: {dt[1]:F0} ms, of it "
                 + $"{t[1] - t[0]:F0} ms from the first frame's scripts to the next frame's; row 2: {dt[2]:F0} ms): {seen.Count} frames in {sum / 1000.0:F2} s "
                 + $"({(sum > 0 ? seen.Count * 1000.0 / sum : 0):F1} fps mean), longest {longest:F0} ms at {at:F2} s, over {maxMs:F0} ms: {over}, "
                 + $"1 % low {low1:F1} fps: {(pass ? "PASS" : "FAIL")} (no frame over {maxMs:F0} ms and the 1 % low over {low1Fps:F0} fps in the first "
                 + $"{seconds:F0} s after the first picture)";
        }
    }
}
