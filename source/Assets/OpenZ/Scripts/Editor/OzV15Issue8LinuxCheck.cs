// OpenZ (GPL-3.0) - 1.5 kit issue8_linux_settings: the counted check of its rules, without Play (Editor only; no saved setting and
// no file is touched: the settings side runs on a table, the paths are only worked out).
// Menu: OpenZ/V1.5/Check Issue 8 Linux Settings + Slow Chip (no Play), or OpenZ.EditorTools.OzV15Issue8LinuxCheck.Run(). One log
// line "[OpenZ issue 8 linux] PASS n/n ..." or "... FAIL ..." (Report); every row's own line follows it in the same log entry.
//  P1  a Linux settings file is read: 7 value lines, a name with &amp; read as &
//  P2  an older file only fills in: 3 missing oz_ values written, a value the store has is kept, engine keys, the alive mark and a
//      broken number are left out
//  P3  a newer file has the last word: the one differing value is replaced; a second time nothing is written
//  P4  the configuration folder: $XDG_CONFIG_HOME when absolute, else ~/.config
//  P5  the engine's candidate files: company/product and unknown/unknown, with and without XDG_CONFIG_HOME
//  P6  the safety copy: the game's folder first, then ~/.config/openz
//  P7  an empty text and a text that is no settings file give no lines
//  C1  slow graphics chips by name (16 names)
//  C3  the seven settings of the choice are the option rows' own keys (1.5 kit issue8_slow_chip_picture: + 3D PICTURE SOFTEST)
//  C4  the LIGHTEST rows belong to LOW: a row keeps its bit only while it holds the chosen value; above LOW (MEDIUM, HIGH) the rows
//      still as chosen go back, on LOW none
//  C5  the start's look on a table: LOW keeps the rows; MEDIUM sets all four back (quality and frame rate limit untouched); HIGH
//      keeps a row the player set himself (trees LIGHT; shadows changed and set OFF again); the player's own lightest rows with no
//      row bit are never touched
//  C6  the row keys are Keys[1..5] (+ 3D PICTURE), LOW is GRAPHICS QUALITY step 2, the log line names the step and the defaults, the watcher is a
//      MonoBehaviour
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzV15Issue8LinuxCheck
    {
        const string Tag = "[OpenZ issue 8 linux]";
        public static string Report = "not run";

        sealed class Table : OzPrefsMirror.IStore
        {
            public readonly Dictionary<string, object> d = new Dictionary<string, object>();
            public bool Has(string key) => d.ContainsKey(key);
            public int GetInt(string key, int fallback) => d.TryGetValue(key, out var v) && v is int i ? i : fallback;
            public float GetFloat(string key, float fallback) => d.TryGetValue(key, out var v) && v is float f ? f : fallback;
            public string GetString(string key, string fallback) => d.TryGetValue(key, out var v) && v is string s ? s : fallback;
            public void SetInt(string key, int v) => d[key] = v;
            public void SetFloat(string key, float v) => d[key] = v;
            public void SetString(string key, string v) => d[key] = v;
        }

        const string Sample =
            "<unity_prefs version_major=\"1\" version_minor=\"1\">\n" +
            "\t<pref name=\"Screenmanager Resolution Width\" type=\"int\">1280</pref>\n" +
            "\t<pref name=\"oz_gfx_quality\" type=\"int\">2</pref>\n" +
            "\t<pref name=\"oz_vol_music\" type=\"float\">0.5</pref>\n" +
            "\t<pref name=\"oz_name\" type=\"string\">Wk9E</pref>\n" +
            "\t<pref name=\"oz_a&amp;b\" type=\"int\">7</pref>\n" +
            "\t<pref name=\"oz_store_alive_v1\" type=\"int\">123</pref>\n" +
            "\t<pref name=\"oz_bad\" type=\"int\">x</pref>\n" +
            "</unity_prefs>\n";

        [MenuItem("OpenZ/V1.5/Check Issue 8 Linux Settings + Slow Chip (no Play)")]
        public static void Run()
        {
            int ok = 0, n = 0; var rows = new StringBuilder(); var failed = new List<string>();
            void Check(bool c, string what) { n++; if (c) ok++; else failed.Add(what.Split(' ')[0]); rows.AppendLine((c ? "PASS " : "FAIL ") + what); }

            // ---- P1-P3, P7: the settings file
            var read = OzPrefsMirror.Parse(Sample);
            Check(read.Count == 7 && read.Exists(r => r.name == "oz_a&b" && r.type == "int" && r.text == "7"), $"P1 a Linux settings file is read: {read.Count} value lines (wanted 7), the name with &amp; read as &");
            var t = new Table(); t.SetInt("oz_gfx_quality", 0);
            int filled = OzPrefsMirror.PutBack(read, t, false);
            Check(filled == 3 && t.GetInt("oz_gfx_quality", -1) == 0 && Mathf.Approximately(t.GetFloat("oz_vol_music", -1f), 0.5f) && t.GetString("oz_name", "") == "ZOD" && t.GetInt("oz_a&b", -1) == 7
                  && !t.Has("Screenmanager Resolution Width") && !t.Has(OzPrefsMirror.AliveKey) && !t.Has("oz_bad"),
                  $"P2 an older file only fills in: {filled} written (wanted 3), the store's own graphics quality kept ({t.GetInt("oz_gfx_quality", -1)}), engine key / alive mark / broken number left out");
            int replaced = OzPrefsMirror.PutBack(read, t, true), again = OzPrefsMirror.PutBack(read, t, true);
            Check(replaced == 1 && again == 0 && t.GetInt("oz_gfx_quality", -1) == 2, $"P3 a newer file has the last word: {replaced} replaced (wanted 1: graphics quality now {t.GetInt("oz_gfx_quality", -1)}), a second time {again} (wanted 0)");
            Check(OzPrefsMirror.Parse("").Count == 0 && OzPrefsMirror.Parse(null).Count == 0 && OzPrefsMirror.Parse("{\"oz_gfx_quality\": 2}").Count == 0, "P7 an empty text and a text that is no settings file give no lines");

            // ---- P4-P6: the paths
            string c1 = OzPrefsMirror.ConfigRoot("/x/cfg/", "/home/a"), c2 = OzPrefsMirror.ConfigRoot("relative", "/home/a/"), c3 = OzPrefsMirror.ConfigRoot("", "");
            Check(c1 == "/x/cfg" && c2 == "/home/a/.config" && c3 == "", $"P4 the configuration folder: [{c1}] [{c2}] [{c3}] (wanted /x/cfg, /home/a/.config, empty)");
            var e1 = OzPrefsMirror.EngineFiles("", "/home/a", "OpenZ", "OpenZ", "/home/a/.config/unity3d/OpenZ/OpenZ");
            var e2 = OzPrefsMirror.EngineFiles("/x/cfg", "/home/a", "OpenZ", "OpenZ", "/x/cfg/unity3d/OpenZ/OpenZ");
            Check(e1.Count == 2 && e1[0] == "/home/a/.config/unity3d/OpenZ/OpenZ/prefs" && e1[1] == "/home/a/.config/unity3d/unknown/unknown/prefs"
                  && e2.Count == 4 && e2.Contains("/x/cfg/unity3d/unknown/unknown/prefs") && e2.Contains("/home/a/.config/unity3d/OpenZ/OpenZ/prefs"),
                  $"P5 the engine's candidate files: {string.Join(", ", e1)} | with XDG_CONFIG_HOME {e2.Count} files (wanted 2 and 4)");
            var k1 = OzPrefsMirror.CopyFiles("", "/home/a", "/games/OpenZ");
            Check(k1.Count == 2 && k1[0] == "/games/OpenZ/" + OzPrefsMirror.CopyName && k1[1] == "/home/a/.config/openz/" + OzPrefsMirror.CopyName, $"P6 the safety copy: {string.Join(", ", k1)}");

            // ---- C1-C3: the slow graphics chip
            var chips = new (string name, bool slow)[]
            {
                ("Intel(R) UHD Graphics 620 (WHL GT2)", true), ("Mesa Intel(R) Xe Graphics (TGL GT2)", true), ("Intel(R) Iris(R) Xe Graphics", true), ("Intel(R) HD Graphics 4000", true),
                ("Intel(R) Arc(TM) A750 Graphics", false), ("NVIDIA GeForce GTX 1660 Ti", false), ("NVIDIA GeForce RTX 2050", false),
                ("AMD Radeon(TM) Graphics", true), ("AMD Radeon Graphics (RADV RENOIR)", true), ("AMD Radeon(TM) Vega 8 Graphics", true), ("AMD Radeon R7 Graphics", true),
                ("AMD Radeon RX 6600 XT", false), ("AMD Radeon(TM) 780M", false), ("Apple M1", false),
                ("llvmpipe (LLVM 23.1.2, 256 bits)", true), ("Microsoft Basic Render Driver", true),
            };
            int good = 0; var wrong = new StringBuilder();
            foreach (var c in chips) { if (OzSlowChipStart.IsSlowChip(c.name) == c.slow) good++; else wrong.Append(" [" + c.name + "]"); }
            Check(good == chips.Length && !OzSlowChipStart.IsSlowChip("") && !OzSlowChipStart.IsSlowChip(null), $"C1 slow graphics chips by name: {good} of {chips.Length} as wanted{(wrong.Length > 0 ? ", wrong:" + wrong : "")}");
            bool d = OzSlowChipStart.Decide(0, true, false, false) == OzSlowChipStart.Step.Pick && OzSlowChipStart.Decide(0, true, true, false) == OzSlowChipStart.Step.PlayersOwn
                     && OzSlowChipStart.Decide(0, false, false, false) == OzSlowChipStart.Step.Nothing && OzSlowChipStart.Decide(0, false, true, false) == OzSlowChipStart.Step.Nothing
                     && OzSlowChipStart.Decide(1, true, true, true) == OzSlowChipStart.Step.Nothing && OzSlowChipStart.Decide(1, true, true, false) == OzSlowChipStart.Step.PlayersOwn
                     && OzSlowChipStart.Decide(1, false, true, true) == OzSlowChipStart.Step.TakeBack && OzSlowChipStart.Decide(1, false, true, false) == OzSlowChipStart.Step.PlayersOwn
                     && OzSlowChipStart.Decide(2, true, false, false) == OzSlowChipStart.Step.Nothing && OzSlowChipStart.Decide(2, false, true, true) == OzSlowChipStart.Step.Nothing;
            Check(d, "C2 what a start does: slow chip + nothing saved = chosen; slow chip + own settings = the player's; fast chip = nothing; as chosen on the fast chip = taken back; changed by the player = his for good");
            var keys = OzSlowChipStart.Keys; var pick = OzSlowChipStart.Pick;
            Check(keys.Length == 7 && pick.Length == 7 && keys[0] == "oz_gfx_quality" && pick[0] == System.Array.IndexOf(OzQuality.Names, "LOW") && keys[1] == "oz_lite_shadows" && keys[2] == "oz_lite_ao"
                  && keys[3] == "oz_lite_trees" && pick[3] == System.Array.IndexOf(OzLightRows.TreesNames, "LIGHTEST") && keys[4] == "oz_lite_wrecks" && keys[5] == "oz_lite_picture" && pick[5] == System.Array.IndexOf(OzLowPicture.Names, "SOFTEST") && keys[6] == "oz_fps_cap" && pick[6] == 30,   // 1.5 kit issue8_slow_chip_picture
                  $"C3 the seven settings of the choice: {string.Join(", ", keys)} = {string.Join(", ", pick)}");

            // ---- C4-C6: the LIGHTEST rows belong to GRAPHICS QUALITY LOW (the owner's condition, 12 Nov)
            bool c4 = OzSlowChipStart.StillChosen(15, new[] { 1, 1, 2, 1 }) == 15 && OzSlowChipStart.StillChosen(15, new[] { 0, 1, 2, 1 }) == 14
                      && OzSlowChipStart.StillChosen(15, new[] { 1, 1, 1, 1 }) == 11 && OzSlowChipStart.StillChosen(0, new[] { 1, 1, 2, 1 }) == 0
                      && OzSlowChipStart.StillChosen(15, null) == 0
                      && OzSlowChipStart.GoBack(15, 2) == 0 && OzSlowChipStart.GoBack(15, 1) == 15 && OzSlowChipStart.GoBack(15, 0) == 15
                      && OzSlowChipStart.GoBack(11, 0) == 11 && OzSlowChipStart.GoBack(0, 0) == 0;
            Check(c4, "C4 the LIGHTEST rows belong to LOW: a row keeps its bit only while it holds the chosen value; MEDIUM / HIGH set the rows still as chosen back, LOW none");
            Table Chosen(int quality, int rowsBits, int shadows, int ao, int trees, int wrecks)
            {
                var x = new Table();
                x.SetInt(OzQuality.Pref, quality); x.SetInt(OzFpsCap.CapPref, 30); x.SetInt(OzSlowChipStart.MarkKey, 1);
                x.SetInt(OzLightRows.ShadowsPref, shadows); x.SetInt(OzLightRows.AoPref, ao); x.SetInt(OzLightRows.TreesPref, trees); x.SetInt(OzLightRows.WrecksPref, wrecks);
                if (rowsBits >= 0) x.SetInt(OzSlowChipStart.RowsKey, rowsBits);
                return x;
            }
            string Rows(Table x) => $"{x.GetInt(OzLightRows.ShadowsPref, -1)}{x.GetInt(OzLightRows.AoPref, -1)}{x.GetInt(OzLightRows.TreesPref, -1)}{x.GetInt(OzLightRows.WrecksPref, -1)}";
            var low = Chosen(2, 15, 1, 1, 2, 1); int bLow = OzSlowChipStart.Settle(low, out int lLow);
            var med = Chosen(1, 15, 1, 1, 2, 1); int bMed = OzSlowChipStart.Settle(med, out int lMed);
            var own = Chosen(0, 15, 1, 1, 1, 1); int bOwn = OzSlowChipStart.Settle(own, out int lOwn);
            var flip = Chosen(0, 14, 1, 1, 2, 1); int bFlip = OzSlowChipStart.Settle(flip, out int lFlip);
            var mine = Chosen(0, -1, 1, 1, 2, 1); mine.d.Remove(OzSlowChipStart.MarkKey); int nMine = mine.d.Count; int bMine = OzSlowChipStart.Settle(mine, out int lMine);
            bool c5 = bLow == 0 && lLow == 15 && Rows(low) == "1121" && low.GetInt(OzSlowChipStart.RowsKey, -1) == 15
                      && bMed == 15 && lMed == 0 && Rows(med) == "0000" && med.GetInt(OzSlowChipStart.RowsKey, -1) == 0 && med.GetInt(OzQuality.Pref, -1) == 1 && med.GetInt(OzFpsCap.CapPref, -1) == 30
                      && bOwn == 11 && lOwn == 0 && Rows(own) == "0010"
                      && bFlip == 14 && lFlip == 0 && Rows(flip) == "1000"
                      && bMine == 0 && lMine == 0 && Rows(mine) == "1121" && mine.d.Count == nMine && !mine.Has(OzSlowChipStart.RowsKey);
            Check(c5, $"C5 the start's look on a table: LOW kept {Rows(low)} (back {bLow}); MEDIUM back {bMed} -> {Rows(med)}, quality {med.GetInt(OzQuality.Pref, -1)}, limit {med.GetInt(OzFpsCap.CapPref, -1)}; "
                      + $"HIGH with the player's trees LIGHT back {bOwn} -> {Rows(own)}; HIGH with the player's own shadows back {bFlip} -> {Rows(flip)}; the player's own lightest rows (no row bit) back {bMine} -> {Rows(mine)} (wanted 1121, 0000, 0010, 1000, 1121)");
            string line = OzSlowChipStart.BackLine(1, 15);
            bool c6 = OzSlowChipStart.RowKeys.Length == 5 && OzSlowChipStart.RowKeys[0] == keys[1] && OzSlowChipStart.RowKeys[1] == keys[2] && OzSlowChipStart.RowKeys[2] == keys[3] && OzSlowChipStart.RowKeys[3] == keys[4] && OzSlowChipStart.RowKeys[4] == keys[5]   // 1.5 kit issue8_slow_chip_picture: + 3D PICTURE
                      && OzSlowChipStart.LowLevel == System.Array.IndexOf(OzQuality.Names, "LOW") && OzSlowChipStart.AllRows == 31
                      && OzSlowChipStart.RowsKey != OzSlowChipStart.MarkKey && System.Array.IndexOf(keys, OzSlowChipStart.RowsKey) < 0
                      && line.Contains("GRAPHICS QUALITY MEDIUM") && line.Contains("SHADOWS on, AMBIENT OCCLUSION on, TREES AND PLANTS FULL, WRECKS AND DEBRIS FULL")
                      && typeof(MonoBehaviour).IsAssignableFrom(typeof(OzSlowChipWatch));
            Check(c6, $"C6 the rows: {string.Join(", ", OzSlowChipStart.RowKeys)} = Keys[1..5], LOW = step {OzSlowChipStart.LowLevel}, the log line: {line}");

            Report = $"{Tag} {(failed.Count == 0 ? "PASS" : "FAIL")} {ok}/{n} checks{(failed.Count > 0 ? " (failed: " + string.Join(" ", failed) + ")" : "")}";
            if (failed.Count == 0) Debug.Log(Report + "\n" + rows); else Debug.LogError(Report + "\n" + rows);
        }
    }
}
