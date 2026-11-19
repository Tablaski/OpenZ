// OpenZ (GPL-3.0) - 1.5 kit issue8_resolution_low: the counted check of its rules, without Play (Editor only, the screen is never changed).
// Menu: OpenZ/V1.5/Check Issue 8 Resolution + LOW (no Play), or OpenZ.EditorTools.OzV15Issue8Check.Run(). One log line
// "[OpenZ issue 8] PASS n/n ..." or "... FAIL ..." (Report); every row's own line follows it in the same log entry.
//  L1  a Linux display (4K, an unsorted mode list with one mode per refresh rate and odd shapes, the editor's stand-in LinuxTest): the
//      row is NATIVE, then 1440, 1080, 900, 720 (sorted, one per size, the display's shape only, 720 or taller)
//  L2  left clicks from NATIVE: the smallest first (the row wraps), then one size larger each, NATIVE, the smallest again
//  L3  right clicks: one size smaller each, past the smallest NATIVE again
//  D1  this editor's display (the Windows / Mac rule): one left click at NATIVE wraps to the smallest, a right click comes back
//  W1  a window at NATIVE on a 1920x1080 display with a 1920x1032 free area: made smaller, same shape, fits with frame and title bar
//  W2  a 1280x720 window there: kept
//  W3  a 3840x2160 display that reports no free area: a 1920x1080 window kept, a NATIVE window made smaller
//  W4  a free area in other units (half the display, as points on a Retina Mac) is left out (the display is used)
//  S1  LOW's render scale: 1366x768 and 1600x900 keep 1.0, 1920x1080 0.75, 2560x1440 0.5625, 3840x2160 0.375, 5120x2880 0.3 (the floor)
// The saved RESOLUTION (oz_res_h), the saved NATIVE size (oz_native_w / _h) and the Linux stand-in are put back at the end.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzV15Issue8Check
    {
        const string Tag = "[OpenZ issue 8]";
        public static string Report = "not run";

        static Vector2Int V(int x, int y) => new Vector2Int(x, y);
        static string Join(IEnumerable<int> a) => string.Join(",", a);

        [MenuItem("OpenZ/V1.5/Check Issue 8 Resolution + LOW (no Play)")]
        public static void Run()
        {
            int ok = 0, n = 0; var rows = new StringBuilder(); var failed = new List<string>();
            void Check(bool c, string what) { n++; if (c) ok++; else failed.Add(what.Split(' ')[0]); rows.AppendLine((c ? "PASS " : "FAIL ") + what); }
            string[] keys = { OzSettings.ResPref, OzSettings.NativeWPref, OzSettings.NativeHPref };
            var had = new Dictionary<string, int?>(); foreach (var k in keys) had[k] = PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k) : (int?)null;
            var testBefore = OzSettings.LinuxTest;
            try
            {
                // ---- L1-L3: a Linux display through the editor's stand-in
                PlayerPrefs.DeleteKey(OzSettings.ResPref); PlayerPrefs.DeleteKey(OzSettings.NativeWPref); PlayerPrefs.DeleteKey(OzSettings.NativeHPref);
                OzSettings.LinuxRowForget();
                OzSettings.LinuxTest = new OzSettings.LinuxDisplay
                {
                    now = V(3840, 2160),
                    modes = new[] { V(1920, 1080), V(3840, 2160), V(1280, 720), V(1920, 1080), V(2560, 1440), V(3840, 1600), V(1280, 1024), V(3840, 2160), V(1600, 900), V(2560, 1440), V(800, 600) }
                };
                var nat = OzSettings.NativeResolution(); var steps = OzSettings.ResolutionSteps(nat.y);
                Check(nat == V(3840, 2160) && Join(steps) == "0,1440,1080,900,720", $"L1 the Linux row: NATIVE {nat.x}x{nat.y}, then {Join(steps)} (wanted 0,1440,1080,900,720: sorted, one per size, the display's shape only, 720 or taller)");
                var walk = new List<int>();
                for (int i = 0; i <= steps.Length; i++) { OzSettings.ResolutionClick(1); walk.Add(OzSettings.ResolutionHeight(nat.y)); }
                Check(Join(walk) == "720,900,1080,1440,0,720", $"L2 left clicks from NATIVE: {Join(walk)} (wanted 720,900,1080,1440,0,720: the smallest after NATIVE, then one size larger each; the row reads \"{OzSettings.ResolutionName}\")");
                walk.Clear();
                for (int i = 0; i <= steps.Length; i++) { OzSettings.ResolutionClick(-1); walk.Add(OzSettings.ResolutionHeight(nat.y)); }
                Check(Join(walk) == "0,1440,1080,900,720,0", $"L3 right clicks from the smallest: {Join(walk)} (wanted 0,1440,1080,900,720,0: past the smallest NATIVE, then one size smaller each)");
                OzSettings.LinuxTest = testBefore; OzSettings.LinuxRowForget();

                // ---- D1: this editor's display, the Windows / Mac rule
                PlayerPrefs.DeleteKey(OzSettings.ResPref);
                var here = OzSettings.NativeResolution(); var mine = OzSettings.ResolutionSteps(here.y);
                if (mine.Length < 2) Check(true, $"D1 this display {here.x}x{here.y} offers NATIVE only: nothing to step");
                else
                {
                    OzSettings.ResolutionClick(1); int a = OzSettings.ResolutionHeight(here.y);
                    OzSettings.ResolutionClick(-1); int b = OzSettings.ResolutionHeight(here.y);
                    Check(a == mine[mine.Length - 1] && b == 0, $"D1 this display {here.x}x{here.y} (steps {Join(mine)}): a left click at NATIVE goes to {a} (wanted {mine[mine.Length - 1]}, the smallest), a right click back to {b} (wanted 0 = NATIVE)");
                }

                // ---- W1-W4: the window fits the desktop's free area
                var w1 = OzSettings.WindowFit(V(1920, 1080), V(1920, 1080), new RectInt(0, 48, 1920, 1032), out string r1);
                Check(w1.x <= 1904 && w1.y <= 992 && w1.y >= 960 && Mathf.Abs(w1.x / (float)w1.y - 16f / 9f) < 0.01f && w1.x % 2 == 0 && w1.y % 2 == 0,
                      $"W1 a NATIVE window on 1920x1080 with a 1920x1032 free area: {w1.x}x{w1.y} ({r1}; wanted at most 1904x992, 16:9, even)");
                var w2 = OzSettings.WindowFit(V(1280, 720), V(1920, 1080), new RectInt(0, 48, 1920, 1032), out _);
                Check(w2 == V(1280, 720), $"W2 a 1280x720 window there: {w2.x}x{w2.y} (wanted kept)");
                var w3a = OzSettings.WindowFit(V(1920, 1080), V(3840, 2160), default, out string r3);
                var w3b = OzSettings.WindowFit(V(3840, 2160), V(3840, 2160), default, out _);
                Check(w3a == V(1920, 1080) && w3b.x < 3840 && w3b.y <= 2160 - 108 - 40 && Mathf.Abs(w3b.x / (float)w3b.y - 16f / 9f) < 0.01f,
                      $"W3 a 3840x2160 display with no free area reported ({r3}): 1920x1080 kept ({w3a.x}x{w3a.y}), NATIVE made smaller ({w3b.x}x{w3b.y})");
                var w4 = OzSettings.WindowFit(V(2880, 1800), V(2880, 1800), new RectInt(0, 25, 1440, 875), out string r4);
                Check(r4.Contains("no free area") && w4.x < 2880 && w4.x > 2400, $"W4 a free area in points (1440x875 on a 2880x1800 display) is left out: {r4}; window {w4.x}x{w4.y}");

                // ---- S1: LOW's render scale
                var want = new (int w, int h, float s)[] { (1366, 768, 1f), (1600, 900, 1f), (1920, 1080, 0.75f), (2560, 1440, 0.5625f), (3840, 2160, 0.375f), (5120, 2880, 0.3f) };
                var said = new StringBuilder(); int good = 0;
                foreach (var c in want) { float s = OzLowScale.For(c.w, c.h); if (Mathf.Abs(s - c.s) < 0.002f) good++; said.Append($" {c.w}x{c.h} {s:0.####}"); }
                Check(good == want.Length, $"S1 LOW's render scale ({good} of {want.Length} as wanted):{said}");
            }
            finally
            {
                OzSettings.LinuxTest = testBefore; OzSettings.LinuxRowForget();
                foreach (var k in keys) { if (had[k].HasValue) PlayerPrefs.SetInt(k, had[k].Value); else PlayerPrefs.DeleteKey(k); }
                PlayerPrefs.Save();
            }
            Report = $"{Tag} {(failed.Count == 0 ? "PASS" : "FAIL")} {ok}/{n} checks{(failed.Count > 0 ? " (failed: " + string.Join(" ", failed) + ")" : "")}";
            if (failed.Count == 0) Debug.Log(Report + "\n" + rows); else Debug.LogError(Report + "\n" + rows);
        }
    }
}
