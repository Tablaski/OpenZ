using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.3 item 37 (GitHub issue 2): counted check of the RESOLUTION row's Linux rule (View/OzSettings.cs, LinuxRow). Editor only,
    /// no Play needed: menu OpenZ/V1.3/Linux Resolution Row Check (no Play), or OpenZ.EditorTools.OzV13LinuxResolutionCheck.Run().
    /// It puts a stand-in Linux display into OzSettings.LinuxTest (the game never sets it), and after every step it moves the
    /// stand-in's reported size to the mode the Linux player would switch the screen to (the smallest mode at least as large as
    /// the step), which is what made 1.2's row drift. One line "[OpenZ Linux resolution row] PASS n/n checks" or "... FAIL ...";
    /// every check's line goes to item37_linux_resolution_row/proof/editor_check.txt.
    ///  L1  the row of a 4K TV over HDMI (35 modes, 18 sizes, duplicates per refresh rate, unsorted): NATIVE 3840x2160 first, then
    ///      its real 16:9 modes from 720 lines up, tallest first, one per height
    ///  L2  the screen's reported size follows every step (1.2's drift): NATIVE, the steps and the label never move
    ///  L3  50 random clicks left / right (seed 37): every click moves exactly one step the clicked way, the ends stop
    ///  L4  both ends and NATIVE (3840 x 2160) reached and selected in the 50
    ///  L5  next start on the lower mode (the engine restores the last size): NATIVE is 3840x2160 again, the saved step kept
    ///  L6  1.2's left-over state (a lower step saved, no size saved, the screen started at 1280x720): NATIVE 3840x2160
    ///  L7  a TV without 2560x1440 and a display with no mode list: the right steps (no mode list = the standard steps)
    ///  L8  (review) 1.2 left oz_res_h 0 and the screen on 3840x1600 (another shape): NATIVE 3840x2160 all the same
    ///  L9  (review) the row never used, a 1920x1080 desktop on the 4K TV: NATIVE stays the desktop, as in 1.2
    ///  W1  Windows / Mac path (no stand-in): the steps of this display are the standard heights as before, and 50 random
    ///      clicks give the same saved values as the 1.2 rule
    /// oz_res_h, oz_native_w and oz_native_h are put back when it ends, also when a step throws.
    /// </summary>
    public static class OzV13LinuxResolutionCheck
    {
        const string Tag = "[OpenZ Linux resolution row]";
        static int ok, bad; static readonly StringBuilder lines = new StringBuilder();
        static readonly int[] Standard = { 2160, 1800, 1600, 1440, 1200, 1080, 900, 720 };

        // a 4K TV over HDMI as X11 lists it: every size once per refresh rate, in no clean order
        static readonly int[][] TvModes =
        {
            new[] { 1920, 1080 }, new[] { 3840, 2160 }, new[] { 4096, 2160 }, new[] { 1280, 720 }, new[] { 3840, 2160 }, new[] { 2560, 1440 },
            new[] { 1920, 1080 }, new[] { 720, 480 }, new[] { 3840, 2160 }, new[] { 1600, 900 }, new[] { 1024, 768 }, new[] { 1920, 1080 },
            new[] { 4096, 2160 }, new[] { 1280, 1024 }, new[] { 3840, 2160 }, new[] { 1680, 1050 }, new[] { 1920, 1080 }, new[] { 1440, 900 },
            new[] { 1280, 720 }, new[] { 720, 576 }, new[] { 2560, 1440 }, new[] { 3840, 2160 }, new[] { 640, 480 }, new[] { 1920, 1080 },
            new[] { 800, 600 }, new[] { 1366, 768 }, new[] { 1280, 800 }, new[] { 4096, 2160 }, new[] { 1152, 864 }, new[] { 3840, 2160 },
            new[] { 1280, 720 }, new[] { 1920, 1080 }, new[] { 2560, 1080 }, new[] { 3840, 1600 }, new[] { 4096, 2160 },
        };

        [MenuItem("OpenZ/V1.3/Linux Resolution Row Check (no Play)")]
        public static void Run()
        {
            ok = bad = 0; lines.Length = 0;
            bool hadRes = PlayerPrefs.HasKey(OzSettings.ResPref), hadW = PlayerPrefs.HasKey(OzSettings.NativeWPref), hadH = PlayerPrefs.HasKey(OzSettings.NativeHPref);
            int res0 = PlayerPrefs.GetInt(OzSettings.ResPref, 0), w0 = PlayerPrefs.GetInt(OzSettings.NativeWPref, 0), h0 = PlayerPrefs.GetInt(OzSettings.NativeHPref, 0);
            try { LinuxChecks(); WindowsChecks(); }
            catch (Exception e) { Check(false, "threw: " + e); }
            finally
            {
                OzSettings.LinuxTest = null; OzSettings.LinuxRowForget();
                if (hadRes) PlayerPrefs.SetInt(OzSettings.ResPref, res0); else PlayerPrefs.DeleteKey(OzSettings.ResPref);
                if (hadW) PlayerPrefs.SetInt(OzSettings.NativeWPref, w0); else PlayerPrefs.DeleteKey(OzSettings.NativeWPref);
                if (hadH) PlayerPrefs.SetInt(OzSettings.NativeHPref, h0); else PlayerPrefs.DeleteKey(OzSettings.NativeHPref);
                PlayerPrefs.Save();
            }
            string sum = $"{Tag} {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad} checks";
            if (bad == 0) Debug.Log(sum); else Debug.LogError(sum);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_3/solutions/item37_linux_resolution_row/proof"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "editor_check.txt"), sum + "\n" + lines);
            }
            catch (Exception e) { Debug.LogWarning(Tag + " report not written: " + e.Message); }
        }

        // ------------------------------------------------------------------ Linux
        static void LinuxChecks()
        {
            var tv = Modes(TvModes); var native = new Vector2Int(3840, 2160);
            var want = new List<int> { 0, 1440, 1080, 900, 768, 720 };            // 2560x1440, 1920x1080, 1600x900, 1366x768, 1280x720
            Fresh(native, tv, 0);
            var steps = OzSettings.ResolutionSteps(OzSettings.NativeResolution().y);
            Check(OzSettings.NativeResolution() == native && Same(steps, want) && OzSettings.ResolutionSize(native, 768) == new Vector2Int(1366, 768)
                  && Name() == "NATIVE (3840 x 2160)",
                  $"L1 the TV's row: {OzSettings.LinuxRowText}; steps {string.Join(",", steps)} (want {string.Join(",", want)})");

            // L2 - L4: 50 random clicks, the reported size following each step the way the Linux player switches the screen
            var rnd = new System.Random(37); int idx = 0, moved = 0, drift = 0; bool top = false, bottom = false, nativeSeen = false; var path = new StringBuilder();
            for (int c = 0; c < 50; c++)
            {
                int dir = rnd.Next(2) == 0 ? -1 : 1, expect = Mathf.Clamp(idx + dir, 0, want.Count - 1);
                OzSettings.StepResolution(dir);
                var size = OzSettings.ResolutionWanted(OzSettings.NativeResolution());
                OzSettings.LinuxTest.now = Switched(tv, size);                       // the screen now reports the mode it switched to
                int got = Array.IndexOf(want.ToArray(), PlayerPrefs.GetInt(OzSettings.ResPref, 0));
                if (got == expect) moved++;
                if (OzSettings.NativeResolution() != native || !Same(OzSettings.ResolutionSteps(OzSettings.NativeResolution().y), want)) drift++;
                idx = expect; if (idx == 0) { top = true; nativeSeen |= Name() == "NATIVE (3840 x 2160)" && size == native; }
                if (idx == want.Count - 1) bottom = true;
                path.Append(dir < 0 ? " L>" : " R>").Append(idx == 0 ? "NATIVE" : size.x + "x" + size.y);
            }
            Check(drift == 0, $"L2 the row never moved while the reported size followed the steps: {50 - drift} of 50 clicks");
            Check(moved == 50, $"L3 every click one step the clicked way (ends stop): {moved} of 50;{path}");
            Check(top && bottom && nativeSeen, $"L4 ends reached: NATIVE {top} (3840 x 2160 selected {nativeSeen}), 1280x720 {bottom}");

            // L5 next start: the engine starts on the last size again, the screen with it
            PlayerPrefs.SetInt(OzSettings.ResPref, 1080); PlayerPrefs.Save();
            var low = Switched(tv, new Vector2Int(1920, 1080));
            OzSettings.LinuxRowForget(); OzSettings.LinuxTest.now = low;
            Check(OzSettings.NativeResolution() == native && OzSettings.ResolutionHeight(native.y) == 1080 && Name() == "1920 x 1080",
                  $"L5 next start on 1920x1080: {OzSettings.LinuxRowText}; saved step {OzSettings.ResolutionHeight(OzSettings.NativeResolution().y)}");

            // L6 1.2's left-over state
            Fresh(new Vector2Int(1280, 720), tv, 720);
            Check(OzSettings.NativeResolution() == native && OzSettings.ResolutionHeight(native.y) == 720,
                  $"L6 a lower step left by 1.2, no size saved, the screen at 1280x720: {OzSettings.LinuxRowText}");

            // L8 (review) 1.2's most common end state: oz_res_h 0, the screen left on a mode of another shape (3840x1600)
            Fresh(new Vector2Int(3840, 1600), tv, 0); PlayerPrefs.SetInt(OzSettings.ResPref, 0); PlayerPrefs.Save();
            Check(OzSettings.NativeResolution() == native && OzSettings.ResolutionHeight(native.y) == 0 && Name() == "NATIVE (3840 x 2160)",
                  $"L8 oz_res_h 0 left by 1.2, no size saved, the screen at 3840x1600: {OzSettings.LinuxRowText}");

            // L9 (review) a player who never used the row keeps the desktop as NATIVE (a 1920x1080 desktop on the 4K TV), as in 1.2
            Fresh(new Vector2Int(1920, 1080), tv, 0);
            Check(OzSettings.NativeResolution() == new Vector2Int(1920, 1080),
                  $"L9 the row never used, desktop 1920x1080 on the 4K TV: {OzSettings.LinuxRowText}");

            // L7 other lists
            var noQhd = new List<Vector2Int>(); foreach (var m in tv) if (m != new Vector2Int(2560, 1440)) noQhd.Add(m);
            Fresh(native, noQhd.ToArray(), 0); var s1 = OzSettings.ResolutionSteps(OzSettings.NativeResolution().y);
            Fresh(native, new Vector2Int[0], 0); var s2 = OzSettings.ResolutionSteps(OzSettings.NativeResolution().y);
            var std = new List<int> { 0 }; foreach (int h in Standard) if (h < 2160) std.Add(h);
            Check(Same(s1, new List<int> { 0, 1080, 900, 768, 720 }) && Same(s2, std) && OzSettings.ResolutionSize(native, 1800) == new Vector2Int(3200, 1800),
                  $"L7 without 2560x1440: {string.Join(",", s1)}; no mode list: {string.Join(",", s2)}");
            OzSettings.LinuxTest = null; OzSettings.LinuxRowForget();
        }

        static void Fresh(Vector2Int now, Vector2Int[] modes, int savedStep)
        {
            OzSettings.LinuxRowForget();
            PlayerPrefs.DeleteKey(OzSettings.NativeWPref); PlayerPrefs.DeleteKey(OzSettings.NativeHPref);
            if (savedStep > 0) PlayerPrefs.SetInt(OzSettings.ResPref, savedStep); else PlayerPrefs.DeleteKey(OzSettings.ResPref);
            PlayerPrefs.Save();
            OzSettings.LinuxTest = new OzSettings.LinuxDisplay { now = now, modes = modes };
        }

        /// <summary>The mode the Linux player switches the screen to for a fullscreen size: the smallest listed mode at least as large.</summary>
        static Vector2Int Switched(Vector2Int[] modes, Vector2Int size)
        {
            Vector2Int best = default; bool any = false;
            foreach (var m in modes)
                if (m.x >= size.x && m.y >= size.y && (!any || m.x < best.x || (m.x == best.x && m.y < best.y))) { best = m; any = true; }
            return any ? best : size;
        }

        // ------------------------------------------------------------------ Windows / Mac (no stand-in: the rule before item 37)
        static void WindowsChecks()
        {
            OzSettings.LinuxTest = null; OzSettings.LinuxRowForget();
            PlayerPrefs.DeleteKey(OzSettings.ResPref); PlayerPrefs.Save();
            var n = OzSettings.NativeResolution(); var steps = OzSettings.ResolutionSteps(n.y);
            var std = new List<int> { 0 }; foreach (int h in Standard) if (h < n.y) std.Add(h);
            var rnd = new System.Random(37); int idx = 0, same = 0;
            for (int c = 0; c < 50; c++)
            {
                int dir = rnd.Next(2) == 0 ? -1 : 1; idx = Mathf.Clamp(idx + dir, 0, std.Count - 1);
                OzSettings.StepResolution(dir);
                if (PlayerPrefs.GetInt(OzSettings.ResPref, 0) == std[idx]) same++;
            }
            Check(!OzSettings.LinuxRule && Same(steps, std) && same == 50,
                  $"W1 this display {n.x}x{n.y} (no Linux rule): steps {string.Join(",", steps)}, 50 clicks the same as the 1.2 rule {same} of 50");
        }

        // ------------------------------------------------------------------ helpers
        static Vector2Int[] Modes(int[][] l) { var r = new Vector2Int[l.Length]; for (int i = 0; i < l.Length; i++) r[i] = new Vector2Int(l[i][0], l[i][1]); return r; }
        /// <summary>The row's label made fresh (its cache is keyed on the screen size and the saved value, not on the stand-in).</summary>
        static string Name()
        {
            int keep = PlayerPrefs.GetInt(OzSettings.ResPref, 0); bool had = PlayerPrefs.HasKey(OzSettings.ResPref);
            PlayerPrefs.SetInt(OzSettings.ResPref, -37); var _ = OzSettings.ResolutionName;
            if (had) PlayerPrefs.SetInt(OzSettings.ResPref, keep); else PlayerPrefs.DeleteKey(OzSettings.ResPref);
            return OzSettings.ResolutionName;
        }
        static bool Same(int[] a, List<int> b) { if (a.Length != b.Count) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }

        static void Check(bool pass, string what)
        {
            if (pass) ok++; else bad++;
            string l = (pass ? "PASS " : "FAIL ") + what; lines.AppendLine(l);
            if (pass) Debug.Log(Tag + " " + l); else Debug.LogError(Tag + " " + l);
        }
    }
}
