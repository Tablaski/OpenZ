// OpenZ (GPL-3.0) - version 1.4 kit mayhem_difficulty: its checks in the editor. Count (no Play): the four difficulties, the C key's steps,
// the saved choice, the rules and the computer see HARD, the fog of war's decision (the game's own WhyOff), MAYHEM's own score tables, the
// OPTIONS rows gone, the words and the button widths measured with the game's own text. Play Proof (MainMenu scene, Play): the lead's frames.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Logs next to Assets: Logs/oz_v14_mayhem_count.txt (the count),
    /// Logs/oz_v14_mayhem_play.txt (the Play proof), frames in Logs/mayhem_frames/. Every setting a check changes is put back at its end.
    /// <list type="bullet">
    /// <item>Count (menu OpenZ/V1.4/Mayhem: Count (no Play)): C1 the list and the C key's steps, C2 the saved choice (oz_difficulty + oz_mayhem)
    /// against every raw write of the old key (DEPLOY, the -take, its restore), C3 the rules and the computer see HARD, C4 the Sim never sees
    /// it, C5 the fog of war's decision table from the game's own WhyOff, C6 the fog no longer reads the OPTIONS prefs, C7 the OPTIONS rows
    /// and tips gone, C8 MAYHEM's own score tables on a file of their own, C9 the widths with the game's own text, C10 the words, C11 the
    /// kit fog_gameplay_only's two checks follow MAYHEM, C12 a MAYHEM match in which the testing panel was used (or that starts after the
    /// panel changed a session setting: a style, PLANET, COLOUR, COVER) is not in MAYHEM's table; only a human against the CPU is MAYHEM.</item>
    /// <item>Play Proof (menu OpenZ/V1.4/Mayhem: Play Proof): P1 OPTIONS (main menu) MAIN with the four buttons and ADVANCED with no FOG row,
    /// P2 the battlefield screen: the C key from HARD to MAYHEM (the button, the hint, the level card's BEST line), P3 the C key's full round,
    /// P4 DEPLOY L02 on MAYHEM (HARD's rules, the CPU HARD, the fog on), P5 the first gameplay frame after the briefing's hand-over + 0.5 s
    /// with the fog, P6 the pause menu's ADVANCED with no FOG row, P7 the end screen of a staged victory naming MAYHEM (its own table, a
    /// file in the temporary folder), P8 L02 again on HARD: no fog. Frames 01..07 in Logs/mayhem_frames/.</item>
    /// </list>
    /// </summary>
    public static class OzV14MayhemCheck
    {
        const string Tag = "[OpenZ V1.4 Mayhem]";
        public const string CountLog = "oz_v14_mayhem_count.txt", PlayLog = "oz_v14_mayhem_play.txt", FramesDir = "Logs/mayhem_frames";
        static readonly List<string> lines = new List<string>();
        static readonly List<string> skipped = new List<string>();
        static readonly List<string> shots = new List<string>();
        static int pass, fail, skip;
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        static void Begin(string what) { lines.Clear(); skipped.Clear(); pass = fail = skip = 0; Say($"{Tag} {what}"); }
        static void Say(string s) { lines.Add(s); Debug.Log(s); }
        static void Row(string id, bool ok, string text) { if (ok) pass++; else fail++; Say($"  {(ok ? "PASS" : "FAIL")} {id} {text}"); }
        static void Skip(string id, string text) { skip++; skipped.Add(id); Say($"  SKIP {id} {text}"); }
        static void End(string file)
        {
            string verdict = fail > 0 || pass == 0 ? "FAIL" : skip > 0 ? "INCOMPLETE" : "PASS";
            Say($"{Tag} {verdict} {pass}/{pass + fail}{(skip > 0 ? $" ({skip} skipped: {string.Join(", ", skipped)})" : "")}");
            try
            {
                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, file), string.Join("\n", lines) + "\n");
            }
            catch (Exception e) { Debug.LogWarning($"{Tag} could not write the log file: {e.Message}"); }
        }

        /// <summary>PlayerPrefs ints kept and put back as they were (a key that was not set is taken out again).</summary>
        sealed class Prefs
        {
            readonly List<(string key, bool had, int v)> kept = new List<(string, bool, int)>();
            public Prefs(params string[] keys) { foreach (var k in keys) kept.Add((k, PlayerPrefs.HasKey(k), PlayerPrefs.GetInt(k, 0))); }
            public void PutBack() { foreach (var (k, had, v) in kept) { if (had) PlayerPrefs.SetInt(k, v); else PlayerPrefs.DeleteKey(k); } PlayerPrefs.Save(); }
            public string Text => string.Join(", ", kept.Select(x => x.key + " " + (x.had ? x.v.ToString() : "not set")));
        }

        static string Src(string rel)
        {
            var p = Path.Combine(Application.dataPath, "OpenZ/Scripts", rel);
            return File.Exists(p) ? File.ReadAllText(p).Replace("\r\n", "\n") : null;
        }

        /// <summary>C# text without its comments (strings and chars kept as they are).</summary>
        static string Code(string s)
        {
            if (s == null) return "";
            var o = new StringBuilder(s.Length); int i = 0, n = s.Length;
            while (i < n)
            {
                char c = s[i];
                if (c == '/' && i + 1 < n && s[i + 1] == '/') { while (i < n && s[i] != '\n') i++; continue; }
                if (c == '/' && i + 1 < n && s[i + 1] == '*') { int j = s.IndexOf("*/", i + 2, StringComparison.Ordinal); i = j < 0 ? n : j + 2; o.Append(' '); continue; }
                if (c == '"')
                {
                    bool verb = i > 0 && (s[i - 1] == '@' || (s[i - 1] == '$' && i > 1 && s[i - 2] == '@'));
                    int j = i + 1;
                    while (j < n) { if (!verb && s[j] == '\\') { j += 2; continue; } if (s[j] == '"') { if (verb && j + 1 < n && s[j + 1] == '"') { j += 2; continue; } break; } if (!verb && s[j] == '\n') break; j++; }
                    o.Append(s, i, Math.Min(n, j + 1) - i); i = j + 1; continue;
                }
                if (c == '\'' && i + 2 < n) { int j = s[i + 1] == '\\' ? i + 3 : i + 2; if (j < n && s[j] == '\'') { o.Append(s, i, j + 1 - i); i = j + 1; continue; } }
                o.Append(c); i++;
            }
            return o.ToString();
        }

        /// <summary>The body of a method (from its signature to the brace that closes it), comments out.</summary>
        static string Body(string code, string sig)
        {
            int a = code.IndexOf(sig, StringComparison.Ordinal); if (a < 0) return null;
            int b = code.IndexOf('{', a); if (b < 0) return null;
            int depth = 0;
            for (int i = b; i < code.Length; i++) { if (code[i] == '{') depth++; else if (code[i] == '}' && --depth == 0) return code.Substring(a, i + 1 - a); }
            return null;
        }

        // ================================================================================================ Count (no Play)
        [MenuItem("OpenZ/V1.4/Mayhem: Count (no Play)")]
        public static void Count()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning($"{Tag} the count runs in edit mode: stop Play first"); return; }
            Begin("Count (no Play): the four difficulties, the saved choice, HARD for the rules, the fog on only in MAYHEM, the scores, OPTIONS, the widths");
            var keep = new Prefs(OpenZDifficulty.Key, OpenZDifficulty.MayhemKey, "oz_zod_exact", OzFogOfWarOption.Pref, OzFogLookOption.Pref);
            Say("  the player's settings before the count: " + keep.Text);
            try { CountRows(); }
            catch (Exception e) { Row("X", false, "the count stopped: " + e); }
            finally
            {
                keep.PutBack();
                OzHighScores.UseFile(null);
                Say("  put back: " + keep.Text + "; the score tables: the player's own file again");
            }
            End(CountLog);
        }

        static void CountRows()
        {
            var E = GameDifficulty.Easy; var M = GameDifficulty.Medium; var H = GameDifficulty.Hard; var X = GameDifficulty.Mayhem;
            var all = new[] { E, M, H, X };

            // ---- C1 the list, the labels, the C key's steps
            string labels = string.Join(" / ", all.Select(OpenZDifficulty.Label));
            Row("C1a", labels == "EASY / MEDIUM / HARD / MAYHEM" && (int)X == 3 && (int)H == 0 && (int)E == 1 && (int)M == 2 && Enum.GetValues(typeof(GameDifficulty)).Length == 4,
                $"four difficulties {labels}; stored numbers Hard 0, Easy 1, Medium 2 as before, Mayhem 3 (never stored in oz_difficulty)");
            var steps = new List<GameDifficulty> { E }; for (int i = 0; i < 4; i++) steps.Add(OpenZDifficulty.Next(steps[steps.Count - 1]));
            Row("C1b", steps.SequenceEqual(new[] { E, M, H, X, E }), "the C key's steps (OpenZDifficulty.Next): " + string.Join(", ", steps.Select(OpenZDifficulty.Label)));
            Row("C1c", OzDeployBar.CpuWords.SequenceEqual(all.Select(OpenZDifficulty.Label)), "the battlefield screen's four buttons say " + string.Join(" | ", OzDeployBar.CpuWords));

            // ---- C2 the saved choice
            var sb = new StringBuilder(); bool ok2 = true;
            void Expect(string what, GameDifficulty choice, GameDifficulty level)
            {
                bool good = OpenZDifficulty.Choice == choice && OpenZDifficulty.Level == level;
                ok2 &= good; sb.Append($"{what}: {OpenZDifficulty.Label(OpenZDifficulty.Choice)} (rules {OpenZDifficulty.Label(OpenZDifficulty.Level)}){(good ? "" : " WRONG")}; ");
            }
            void Raw(int d, int m) { PlayerPrefs.SetInt(OpenZDifficulty.Key, d); if (m >= 0) PlayerPrefs.SetInt(OpenZDifficulty.MayhemKey, m); }
            Raw(0, 1); Expect("oz_difficulty 0 + oz_mayhem 1", X, H);
            OpenZDifficulty.Level = H; Expect("Level = HARD (the editor menu, a proof)", H, H); ok2 &= PlayerPrefs.GetInt(OpenZDifficulty.MayhemKey, 0) == 0;
            OpenZDifficulty.Choice = X; Expect("Choice = MAYHEM", X, H); ok2 &= PlayerPrefs.GetInt(OpenZDifficulty.Key, -1) == 0 && PlayerPrefs.GetInt(OpenZDifficulty.MayhemKey, 0) == 1;
            PlayerPrefs.SetInt(OpenZDifficulty.Key, (int)OpenZDifficulty.Level); Expect("DEPLOY's own line (oz_difficulty = (int)Level)", X, H);
            Raw(1, -1); Expect("the -take's raw write 1", E, E);
            Raw(0, -1); Expect("the -take put back (raw 0, oz_mayhem untouched)", X, H);
            Raw(2, -1); Expect("a raw write 2", M, M);
            OpenZDifficulty.Level = X; Expect("Level = Mayhem (= Choice = MAYHEM)", X, H);
            OpenZDifficulty.Choice = E; Expect("Choice = EASY", E, E); ok2 &= PlayerPrefs.GetInt(OpenZDifficulty.MayhemKey, -1) == 0;
            PlayerPrefs.DeleteKey(OpenZDifficulty.Key); PlayerPrefs.SetInt(OpenZDifficulty.MayhemKey, 1); Expect("no oz_difficulty (a fresh install) with a stray oz_mayhem 1", M, M);
            Row("C2a", ok2, "the choice saved and read back: " + sb.ToString().TrimEnd(' ', ';'));
            string menuSrc = Src("UI/OpenZMainMenu.cs") ?? "", zg = Src("View/ZGame.cs") ?? "";
            Row("C2b", menuSrc.Contains("PlayerPrefs.SetInt(OpenZDifficulty.Key, (int)OpenZDifficulty.Level);") && zg.Contains("PlayerPrefs.HasKey(OpenZDifficulty.Key)"),
                "DEPLOY's line and ZGame's HasKey are unchanged (View/MenuExeProof.cs and the final gate's baseline read them)");

            // ---- C3 the rules and the computer see HARD
            bool same = OpenZDifficulty.Rules(X) == H && OpenZDifficulty.Bot(OpenZDifficulty.Rules(X)) == ZBotDifficulty.Normal && OpenZDifficulty.Bot(X) == OpenZDifficulty.Bot(H)
                        && OpenZDifficulty.TechBonus(X) == OpenZDifficulty.TechBonus(H) && OpenZDifficulty.TechBonus(H) == 0
                        && OpenZDifficulty.HeadStart(X, true) == OpenZDifficulty.HeadStart(H, true) && OpenZDifficulty.HeadStart(X, false) == OpenZDifficulty.HeadStart(H, false);
            var shuttle = new StringBuilder(); bool sameBox = true;
            foreach (var lv in new[] { "L02", "L07", "L16", "L17" })
                foreach (bool box in new[] { true, false })
                    foreach (bool zx in new[] { false, true })
                    {
                        bool a = OzShuttleStart.Offered(lv, X, zx) == OzShuttleStart.Offered(lv, H, zx) && OzShuttleStart.Rule(lv, X, box) == OzShuttleStart.Rule(lv, H, box)
                                 && OzShuttleStart.L17Start(lv, X, box) == OzShuttleStart.L17Start(lv, H, box);
                        sameBox &= a; if (!a) shuttle.Append($" {lv} box {box} zod exact {zx}");
                    }
            Row("C3a", same && sameBox, $"MAYHEM = HARD for the rules: Rules {OpenZDifficulty.Label(OpenZDifficulty.Rules(X))}, the CPU {OpenZDifficulty.Bot(OpenZDifficulty.Rules(X))}, tech bonus {OpenZDifficulty.TechBonus(X)}, head start as HARD, the SHUTTLE box (Offered, Rule, L17Start; 4 levels x box x 'Zod exact') as HARD{(sameBox ? "" : ": DIFFERENT" + shuttle)}");
            string zc = Code(zg);
            bool start = zc.Contains("mayhem = blueBot != redBot && (gameDifficulty == GameDifficulty.Mayhem || (PlayerPrefs.HasKey(OpenZDifficulty.Key) && OpenZDifficulty.MayhemChosen)); if (gameDifficulty == GameDifficulty.Mayhem) { gameDifficulty = GameDifficulty.Hard;");
            bool dv73 = zc.Contains("gameDifficulty == GameDifficulty.Hard && !(blueBot && redBot);");
            bool noMayhemRule = !zc.Replace("GameDifficulty.Mayhem ? GameDifficulty.Mayhem", "").Contains("== GameDifficulty.Mayhem &&");
            Row("C3b", start && dv73 && noMayhemRule, $"ZGame at the level start: MAYHEM read (only a human against the CPU, blueBot != redBot as the fog's WhyOff: never in a computer-only match or the Unit editor) and gameDifficulty kept Hard (an inspector Mayhem mapped to Hard) {start}; DV73's line reads gameDifficulty == Hard {dv73}; no rule of ZGame asks for Mayhem {noMayhemRule}");
            var go = new GameObject("OZ_MayhemCount_Probe") { hideFlags = HideFlags.HideAndDontSave }; go.SetActive(false);
            try
            {
                var g = go.AddComponent<ZGame>(); var fog = go.AddComponent<OzFogOfWar>();
                g.gameDifficulty = H; g.mayhem = true; bool m1 = g.IsMayhem && g.ShownDifficulty == X && g.BotDifficulty(Team.Blue) == OpenZDifficulty.Bot(H);
                g.mayhem = false; bool m2 = !g.IsMayhem && g.ShownDifficulty == H;
                g.gameDifficulty = E; g.mayhem = true; bool m3 = !g.IsMayhem && g.ShownDifficulty == E;
                Row("C3c", m1 && m2 && m3, $"ZGame.ShownDifficulty: a MAYHEM match (Hard + mayhem) MAYHEM, the CPU {g.BotDifficulty(Team.Blue)} {m1}; HARD HARD {m2}; mayhem with a live change to EASY: EASY {m3}");
                FogTable(g, fog);
            }
            finally { Object.DestroyImmediate(go); }

            // ---- C4 the Sim never sees it
            var simHits = new List<string>();
            foreach (var p in Directory.GetFiles(Path.Combine(Application.dataPath, "OpenZ/Scripts/Sim"), "*.cs", SearchOption.AllDirectories))
            {
                string c = Code(File.ReadAllText(p));
                if (c.Contains("GameDifficulty") || c.Contains("Mayhem") || c.Contains("oz_mayhem") || c.Contains("MayhemKey")) simHits.Add(Path.GetFileName(p));
            }
            Row("C4", simHits.Count == 0, "no Sim file names the difficulty, MAYHEM or oz_mayhem (the rules, the computer and headless bot matches cannot see it: parity as HARD)" + (simHits.Count > 0 ? ": " + string.Join(", ", simHits) : ""));

            // ---- C6 the fog no longer reads the OPTIONS prefs
            var fogCode = new StringBuilder();
            foreach (var p in Directory.GetFiles(Path.Combine(Application.dataPath, "OpenZ/Scripts/View"), "OzFogOfWar*.cs")) if (p.EndsWith(".cs")) fogCode.Append(Code(File.ReadAllText(p)));
            string fc = fogCode.ToString(), decide = Body(Code(Src("View/OzFogOfWar.cs")), "void Decide()") ?? "";
            Row("C6", !fc.Contains("OzFogOfWarOption.On") && !fc.Contains("OzFogLookOption.Index") && decide.Contains("sessionLook >= 0 ? sessionLook : Recommended") && decide.Contains("BiomeDecide();"),
                "the fog reads neither oz_fog_of_war (OzFogOfWarOption.On) nor oz_fog_look (OzFogLookOption.Index); a match starts with the recommended look "
                + OzFogOfWar.Looks[OzFogOfWar.Recommended].Name + " unless the testing panel chose one; a MAYHEM match then takes its biome's look (fog_biome_looks, View/OzFogOfWar.Biomes.cs)");

            // ---- C7 the OPTIONS rows and tips gone
            string mm = Code(menuSrc), pm = Code(Src("UI/OpenZPauseMenu.cs"));
            bool mainGone = Wrapped(mm, "AddToggle(p, toggles.Count, OzFogOfWarOption.Label", "AddCycle(p, toggles.Count, OzFogLookOption.Label, () => OzFogLookOption.Name, OzFogLookOption.Step);", out string mw);
            bool pauseGone = Wrapped(pm, "AddToggle(p, toggles.Count, OzFogOfWarOption.Label", "fl.row.OnRightClick = c => OzFogLookOption.Step(-1); }", out string pw);
            Row("C7a", !OzFogOfWarOption.InOptions && mainGone && pauseGone, $"OPTIONS: FOG OF WAR and FOG LOOK are not built (OzFogOfWarOption.InOptions {OzFogOfWarOption.InOptions}): main menu {mw}; pause menu {pw}");
            Row("C7b", OzSettingTips.For(OzFogOfWarOption.Label) == null && OzSettingTips.For(OzFogLookOption.Label) == null && OzFogOfWarOption.Pref == "oz_fog_of_war" && OzFogLookOption.Pref == "oz_fog_look",
                "their tips are not registered; both classes and their prefs are kept (readable, nothing reads them)");

            // ---- C8 MAYHEM's own score tables
            ScoreRows();

            // ---- C9 the widths, the game's own text
            WidthRows();

            // ---- C10 the words
            string score = Code(Src("UI/OpenZEndScreen.Score.cs")), side = Code(Src("UI/OpenZEndScreen.ScoreSide.cs")), chip = Code(Src("UI/OzBestChip.cs")), hs = Code(Src("UI/OpenZMainMenu.HighScores.cs"));
            var tabs = typeof(OpenZMainMenu).GetField("HsTabs", Any)?.GetValue(null) as GameDifficulty[];
            Row("C10a", !score.Contains("game.gameDifficulty") && !side.Contains("game.gameDifficulty") && score.Contains("OzHighScores.Submit(game.level, game.ShownDifficulty, scoreEntry)")
                        && score.Contains("\"NEW RECORD · \" + game.level + \" · \" + OpenZDifficulty.Label(game.ShownDifficulty)") && chip.Contains("var d = OpenZDifficulty.Choice;")
                        && tabs != null && tabs.SequenceEqual(all) && hs.Contains("System.Array.IndexOf(HsTabs, OpenZDifficulty.Choice)"),
                $"the end screen enters, names and looks up MAYHEM's own table (\"NEW RECORD · L02 · MAYHEM\", \"{OzHighScores.TableTitle("L02", X)}\"); the level card's line follows the choice; HIGH SCORES tabs {(tabs != null ? string.Join(" / ", tabs.Select(OpenZDifficulty.Label)) : "not found")}, opening on the choice");
            Row("C10b", OzDeployBar.CpuHint(X) == OzDeployBar.CpuMayhem && OzDeployBar.CpuMayhem == "HARD + FOG OF WAR" && OzDeployBar.CpuHint(H) == OzDeployBar.CpuHard
                        && OpenZDifficulty.Hint(X).Contains("FOG OF WAR") && OpenZDifficulty.Hint(H) == "Z'S BUILD LISTS · FORT MISSILE CANNONS"
                        && OzShuttleStart.LockedRowFor(X) == "NOT ON MAYHEM" && OzShuttleStart.LockedRowFor(H) == OzShuttleStart.LockedRow && OzShuttleStart.TipBody.Contains("HARD, MAYHEM or OFF")
                        && OzSettingTips.Difficulty.Contains("MAYHEM: the same as HARD, under the fog of war") && OzSettingTips.Difficulty.Contains("ground you have not explored"),
                $"the words: bar hint \"{OzDeployBar.CpuHint(X)}\", OPTIONS hint \"{OpenZDifficulty.Hint(X)}\", the locked SHUTTLE slot \"{OzShuttleStart.LockedRowFor(X)}\" / \"{OzShuttleStart.LockedHint}\", its tooltip and the DIFFICULTY tip name MAYHEM; HARD's words unchanged");

            // ---- C11 fog_gameplay_only's two checks follow MAYHEM
            string fgc = Src("Editor/OzV14FogGameplayCount.cs"), fgp = Src("Editor/OzV14FogGameplayCheck.cs");
            if (fgc == null || fgp == null) Skip("C11", "the kit fog_gameplay_only is not in this project (its N2 / G9 / G11 rows are edited when it is)");
            else
            {
                string a = Code(fgc), b = Code(fgp);
                bool n2 = a.Contains("whyOff.Contains(\"game.IsMayhem\")") && !a.Contains("&& whyOff.Contains(\"OzFogOfWarOption.On\")");
                bool g9 = b.Contains("PlayerPrefs.SetInt(OpenZDifficulty.MayhemKey, 1); probe = null;") && b.Contains("PlayerPrefs.SetInt(OpenZDifficulty.MayhemKey, 0); probe = null;")
                          && !b.Contains("PlayerPrefs.SetInt(OzFogOfWarOption.Pref, 1)") && !b.Contains("PlayerPrefs.SetInt(OzFogOfWarOption.Pref, 0)")
                          && b.Contains("if (hadMay) PlayerPrefs.SetInt(OpenZDifficulty.MayhemKey, mayWas);") && b.Contains("if (hadDiff) PlayerPrefs.SetInt(OpenZDifficulty.Key, diffWas);")
                          && b.Contains("Say($\"  1.4 MAYHEM: {OpenZDifficulty.Key} put back (");                                    // round 2: its log names both keys
                Row("C11", n2 && g9, $"fog_gameplay_only: its count's N2 looks for MAYHEM in WhyOff {n2}; its Play check's G9 reloads on MAYHEM, G11 on HARD, both keys put back and logged {g9}");
            }

            // ---- C12 the testing panel (Alt+P, in player builds too) keeps MAYHEM's table to matches played under the fog
            string fogAll = Code(Src("View/OzFogOfWar.cs")), panel = Code(Src("View/OzFogOfWar.Panel.cs"));
            string act = Body(panel, "void PanelAct(int i)") ?? "", dec = Body(fogAll, "void Decide()") ?? "";
            bool byPanel = act.Contains("if (game && game.IsMayhem && i != 5) OzHighScores.Unranked = PanelUnranked;") && act.IndexOf("OzHighScores.Unranked", StringComparison.Ordinal) < act.IndexOf("SetForced", StringComparison.Ordinal);
            bool byCover = dec.Contains("if (game && game.IsMayhem && panelSession) OzHighScores.Unranked = PanelUnranked;") && !dec.Contains("0.999f")   // no fixed cover number (round 2)
                           && act.Contains("if (i != 0 && i != 5) panelSession = true;") && panel.Contains("static void PanelSessionReset() => panelSession = false;");
            string endWhy = Body(Code(Src("UI/OpenZEndScreen.Score.cs")), "string UnrankedWhy()") ?? "";
            Row("C12", byPanel && byCover && endWhy.Contains("return OzHighScores.Unranked;") && OzFogOfWar.PanelUnranked == "TESTING OVERLAY USED · NOT IN THE TABLE",
                $"a MAYHEM match in which the testing panel's buttons were used (FOG OFF / ON, the looks, PLANET, COLOUR, COVER; not COVER AGAIN) is not in MAYHEM's table {byPanel}; nor one that starts after the panel changed a session setting (a style, PLANET, COLOUR, COVER: whatever the shipped cover is; reset with the session) {byCover}; the end screen shows \"{OzFogOfWar.PanelUnranked}\"");
        }

        /// <summary>The statements from `first` to `last` stand inside `if (OzFogOfWarOption.InOptions) { ... }` and nowhere else.</summary>
        static bool Wrapped(string code, string first, string last, out string why)
        {
            int f = code.IndexOf(first, StringComparison.Ordinal), l = code.IndexOf(last, StringComparison.Ordinal);
            int iff = code.LastIndexOf("if (OzFogOfWarOption.InOptions)", f < 0 ? 0 : f, StringComparison.Ordinal);
            if (f < 0 || l < f || iff < 0) { why = $"not found (statement {f >= 0}, last {l >= f}, if {iff >= 0})"; return false; }
            string head = code.Substring(iff + "if (OzFogOfWarOption.InOptions)".Length, f - iff - "if (OzFogOfWarOption.InOptions)".Length).Trim();
            string tail = code.Substring(l + last.Length).TrimStart();                                         // `last` ends the block's last statement
            bool once = code.IndexOf(first, f + 1, StringComparison.Ordinal) < 0;
            bool ok = head == "{" && tail.StartsWith("}") && once;
            why = ok ? "inside if (OzFogOfWarOption.InOptions) { ... }, not built" : $"WRONG (opens '{head}', closes '{(tail.Length > 0 ? tail.Substring(0, Math.Min(12, tail.Length)) : "")}', once {once})";
            return ok;
        }

        // ---------------------------------------------------------------- C5 the fog of war's decision: the game's own WhyOff
        static void FogTable(ZGame g, OzFogOfWar fog)
        {
            var whyOff = typeof(OzFogOfWar).GetMethod("WhyOff", Any); var fGame = typeof(OzFogOfWar).GetField("game", Any); var fForce = typeof(OzFogOfWar).GetField("forceSession", Any);
            if (whyOff == null || fGame == null || fForce == null) { Row("C5", false, "OzFogOfWar.WhyOff / game / forceSession not found (renamed?)"); return; }
            bool ue = OzUeHooks.Active || OzUnitEditor.IsPlayground(SceneManager.GetActiveScene());
            if (ue) { Skip("C5", "the Unit editor's scene is the active scene: every row would say 'the Unit editor'; open another scene and run the count again"); return; }
            fGame.SetValue(fog, g);
            string Why(GameDifficulty d, bool may, bool human, int force, bool zodExact, bool oldPref)
            {
                g.gameDifficulty = d; g.mayhem = may; g.blueBot = true; g.redBot = !human; fForce.SetValue(fog, force);
                PlayerPrefs.SetInt("oz_zod_exact", zodExact ? 1 : 0); PlayerPrefs.SetInt(OzFogOfWarOption.Pref, oldPref ? 1 : 0);
                return (string)whyOff.Invoke(fog, null);
            }
            var H = GameDifficulty.Hard;
            var rows = new (string what, string why, bool on)[]
            {
                ("MAYHEM, a human against the CPU", Why(H, true, true, 0, false, false), true),
                ("HARD", Why(H, false, true, 0, false, false), false),
                ("MEDIUM", Why(GameDifficulty.Medium, false, true, 0, false, false), false),
                ("EASY", Why(GameDifficulty.Easy, false, true, 0, false, false), false),
                ("HARD with the old OPTIONS FOG OF WAR ON saved (oz_fog_of_war 1)", Why(H, false, true, 0, false, true), false),
                ("EASY with a stale MAYHEM flag (a live change)", Why(GameDifficulty.Easy, true, true, 0, false, false), false),
                ("MAYHEM in 'Zod exact'", Why(H, true, true, 0, true, false), true),
                ("HARD in 'Zod exact'", Why(H, false, true, 0, true, false), false),
                ("MAYHEM, a computer match (both CPUs)", Why(H, true, false, 0, false, false), false),
                ("MAYHEM, the testing panel's FOG OFF", Why(H, true, true, -1, false, false), false),
                ("HARD, the testing panel's FOG ON (Alt+P, for us)", Why(H, false, true, 1, false, false), true),
            };
            bool ok = true; var sb = new StringBuilder();
            foreach (var r in rows) { bool good = (r.why == null) == r.on; ok &= good; sb.Append($"\n      {(good ? "ok   " : "WRONG")} {r.what}: {(r.why == null ? "ON" : "off (" + r.why + ")")}"); }
            bool notMayhem = rows[1].why != null && rows[1].why.StartsWith("not MAYHEM");
            string body = Body(Code(Src("View/OzFogOfWar.cs")), "string WhyOff()") ?? "";
            int iUe = body.IndexOf("OzUeHooks.Active", StringComparison.Ordinal), iMay = body.IndexOf("game.IsMayhem", StringComparison.Ordinal);
            bool ueFirst = iUe >= 0 && iMay > iUe;
            Row("C5", ok && notMayhem && ueFirst, "the fog of war on exactly in MAYHEM (the game's own OzFogOfWar.WhyOff on a probe ZGame; the Unit editor's test comes first in it "
                + ueFirst + "):" + sb);
            fGame.SetValue(fog, null); fForce.SetValue(fog, 0);
        }

        // ---------------------------------------------------------------- C8 MAYHEM's own score tables
        static void ScoreRows()
        {
            string path = Path.Combine(Application.temporaryCachePath, "oz_mayhem_count_scores.json");
            File.WriteAllText(path, "{\"version\":1,\"tables\":["
                + "{\"level\":\"L02\",\"difficulty\":\"HARD\",\"entries\":[{\"score\":15000,\"seconds\":900,\"kills\":30,\"losses\":10,\"built\":40,\"zones\":7,\"zonesTotal\":9}]},"
                + "{\"level\":\"L02\",\"difficulty\":\"MEDIUM\",\"entries\":[{\"score\":12000,\"seconds\":1000,\"kills\":20,\"losses\":10,\"built\":30,\"zones\":6,\"zonesTotal\":9}]},"
                + "{\"level\":\"L17\",\"difficulty\":\"EASY\",\"entries\":[{\"score\":9000,\"seconds\":1500,\"kills\":10,\"losses\":10,\"built\":20,\"zones\":5,\"zonesTotal\":12}]}]}");
            OzHighScores.UseFile(path);
            var X = GameDifficulty.Mayhem; var H = GameDifficulty.Hard;
            bool loads = OzHighScores.Top("L02", H).Count == 1 && OzHighScores.Top("L02", GameDifficulty.Medium).Count == 1 && OzHighScores.Top("L17", GameDifficulty.Easy).Count == 1 && OzHighScores.Best("L02", X) == null;
            int place = OzHighScores.Submit("L02", X, new OzScoreEntry { score = 18000, seconds = 700, kills = 40, losses = 8, built = 50, zones = 8, zonesTotal = 9 });
            bool hardSame = OzHighScores.Top("L02", H).Count == 1 && OzHighScores.Top("L02", H)[0].score == 15000;
            string text = File.Exists(path) ? File.ReadAllText(path) : "";
            bool named = Regex.IsMatch(text, "\"difficulty\"\\s*:\\s*\"MAYHEM\"") && Regex.IsMatch(text, "\"difficulty\"\\s*:\\s*\"HARD\"");   // OzHighScores.Save writes JsonUtility.ToJson(Data, true): pretty, "difficulty": "MAYHEM"
            OzHighScores.UseFile(path);                                                                       // read again from the file
            bool again = OzHighScores.Top("L02", X).Count == 1 && OzHighScores.Best("L02", X).score == 18000 && OzHighScores.Top("L02", H)[0].score == 15000;
            Row("C8", loads && place == 1 && hardSame && again && named && OzHighScores.TableTitle("L02", X) == "TOP 10 · L02 · MAYHEM",
                $"a table file from before (HARD, MEDIUM, EASY) loads as it was {loads}; a MAYHEM victory enters its own table (place {place}, the file's table \"MAYHEM\"), HARD's untouched {hardSame}, read again {again}; the saved file names both tables {named}; title \"{OzHighScores.TableTitle("L02", X)}\" (a file of its own: {path})");
            OzHighScores.UseFile(null);
        }

        // ---------------------------------------------------------------- C9 the widths, measured with the game's own text
        static void WidthRows()
        {
            var root = new GameObject("OZ_MayhemCount_Text", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                float W(string s, float size, float tr, OzStyle st = OzStyle.Underlay) { var t = OzText.Make(root.transform, "W", s, size, Color.white, tr, st); return t.preferredWidth; }
                var sb = new StringBuilder(); bool ok = true;
                var words = OzDeployBar.CpuWords.Select(w => W(w, OzDeployBar.CpuText, OzDeployBar.CpuTrack)).ToArray();
                for (int i = 0; i < 4; i++) { bool k = words[i] <= OzDeployBar.CpuWordW[i] + 0.5f; ok &= k; }
                sb.Append($"words at {OzDeployBar.CpuText} / {OzDeployBar.CpuTrack}: " + string.Join(", ", OzDeployBar.CpuWords.Select((w, i) => $"{w} {words[i]:0.0} u (kit {OzDeployBar.CpuWordW[i]:0.0})")));
                foreach (float bw in new[] { OzDeployBar.BlockWide, OzDeployBar.BlockNarrow, OzDeployBar.BlockCompact })
                {
                    float total = OzDeployBar.CpuCellX(bw, 3) + OzDeployBar.CpuCellW(bw, 3), room = float.MaxValue; bool apart = true;
                    for (int i = 0; i < 4; i++) { room = Mathf.Min(room, (OzDeployBar.CpuCellW(bw, i) - words[i]) * 0.5f); if (i > 0) apart &= Mathf.Abs(OzDeployBar.CpuCellX(bw, i) - OzDeployBar.CpuCellX(bw, i - 1) - OzDeployBar.CpuCellW(bw, i - 1) - OzDeployBar.CpuGap) < 0.01f; }
                    bool k = Mathf.Abs(total - bw) < 0.01f && apart && room >= 3f; ok &= k;
                    sb.Append($"; block {bw} u: cells {string.Join(" | ", Enumerable.Range(0, 4).Select(i => OzDeployBar.CpuCellW(bw, i).ToString("0.0")))}, {OzDeployBar.CpuGap} u apart, end at {total:0.0}, the least room beside a word {room:0.0} u{(k ? "" : " WRONG")}");
                }
                Row("C9a", ok, "the battlefield screen's DIFFICULTY row (same block width; every word at full size with 3 u or more each side): " + sb);
                float optW = 600f - 2f * OzLayout.OptX, ob = (optW - 12f) / 4f; float[] ow = OzDeployBar.CpuWords.Select(w => W(w, OzLayout.OptRowText, 0.14f)).ToArray();
                Row("C9b", ow.All(x => x + 6f <= ob), $"the OPTIONS row: four buttons {ob:0.0} u wide, the widest word {ow.Max():0.0} u at {OzLayout.OptRowText} / 0.14");
                float tabsEnd = OpenZMainMenu.HsTabX + 3f * (OpenZMainMenu.HsTabW + OpenZMainMenu.HsTabGap) + OpenZMainMenu.HsTabW;
                float note = W("THE " + OzHighScores.TableSize + " BEST VICTORIES OF EACH LEVEL", 9f, 0.2f, OzStyle.Plain), noteLeft = OpenZMainMenu.HsW - OpenZMainMenu.HsPad - note;
                float tabWord = OzDeployBar.CpuWords.Select(w => W(w, 11f, 0.14f)).Max();
                Row("C9c", noteLeft - tabsEnd >= 8f && tabWord + 6f <= OpenZMainMenu.HsTabW, $"HIGH SCORES: four tabs end at x {tabsEnd:0}, the note starts at x {noteLeft:0.0} ({noteLeft - tabsEnd:0.0} u apart); the widest tab word {tabWord:0.0} u in {OpenZMainMenu.HsTabW} u");
                float locked = W(OzShuttleStart.LockedRowMayhem, 11f, 0.1f, OzStyle.Plain), hint = W(OzDeployBar.CpuMayhem, 9f, 0.16f, OzStyle.Plain), opt = W(OpenZDifficulty.Hint(GameDifficulty.Mayhem), 9f, 0.16f, OzStyle.Plain);
                float box = OzDeployBar.BlockCompact - OzDeployBar.WordsX - OzDeployBar.TrackRX - 10f;
                float tip = OzShuttleStart.TipBody.Split('\n').Max(l => W(l, 11f, 0f, OzStyle.Plain));
                Row("C9d", locked <= box && hint <= OzDeployBar.BlockCompact && opt <= optW && tip <= 304f,
                    $"\"{OzShuttleStart.LockedRowMayhem}\" {locked:0.0} u in the locked row's {box:0} u (compact bar); the bar's hint {hint:0.0} u in {OzDeployBar.BlockCompact} u; the OPTIONS hint {opt:0.0} u in {optW:0} u; the SHUTTLE tooltip's longest line {tip:0.0} u in 304 u");
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ================================================================================================ Play proof
        static IEnumerator running;
        static readonly Stack<IEnumerator> nest = new Stack<IEnumerator>();
        static void Run(IEnumerator it)
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning($"{Tag} open the MainMenu scene, press Play, then run this again"); return; }
            if (running != null) { Debug.LogWarning($"{Tag} the proof is already running"); return; }
            running = it; nest.Clear(); nest.Push(it); EditorApplication.update += Step;
        }
        static void Step()
        {
            bool more = false;
            try
            {
                while (EditorApplication.isPlaying && nest.Count > 0)
                {
                    var top = nest.Peek();
                    if (!top.MoveNext()) { nest.Pop(); continue; }
                    if (top.Current is IEnumerator inner) { nest.Push(inner); continue; }
                    more = true; break;
                }
            }
            catch (Exception e) { Debug.LogError($"{Tag} the proof stopped: {e}"); more = false; }
            if (more) return;
            EditorApplication.update -= Step;
            while (nest.Count > 0) { var it = nest.Pop(); try { (it as IDisposable)?.Dispose(); } catch (Exception) { } }   // the routine's finally: settings put back
            running = null;
        }
        static IEnumerator Seconds(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
        static IEnumerator Until(Func<bool> f, float max) { float t = Time.realtimeSinceStartup + max; while (!f() && Time.realtimeSinceStartup < t) yield return null; }
        static string dir;
        static IEnumerator Shot(string name)
        {
            string path = Path.Combine(dir, name + ".png");
            var asked = DateTime.UtcNow.AddSeconds(-1);
            ScreenCapture.CaptureScreenshot(path);
            float until = Time.realtimeSinceStartup + 6f; long last = -1;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                var fi = new FileInfo(path);
                if (!fi.Exists || fi.LastWriteTimeUtc < asked) continue;
                if (fi.Length > 0 && fi.Length == last) { shots.Add(name + ".png"); yield break; }
                last = fi.Length;
            }
            Say("  NOTE the frame " + name + " was not written in 6 s");
        }

        /// <summary>The fog's gate is open (the kit fog_gameplay_only's OzFogOfWar.GameplayView; without that kit: the fog drawn at full strength).</summary>
        static bool GateOpen()
        {
            var p = typeof(OzFogOfWar).GetProperty("GameplayView", BindingFlags.Public | BindingFlags.Static);
            return p != null ? (bool)p.GetValue(null) : OzFogOfWar.Wanted && OzFogOfWar.Strength >= 0.999f;
        }

        /// <summary>Any briefing or flyover skipped (as the SPACE key), then until the player has the camera and no window is up.</summary>
        static IEnumerator ToPlay(float max)
        {
            float t0 = Time.realtimeSinceStartup, calm = -1f;
            while (Time.realtimeSinceStartup - t0 < max)
            {
                bool brief = ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive, fly = ZodFlyover.Live != null && ZodFlyover.Live.Active;
                bool window = ZodBriefing.CommsVisible || OzBlueCameo.SceneShowing;
                if (brief && Time.realtimeSinceStartup - t0 > 1f) { ZodBriefing.ProofSkipBriefing(); OzBlueCameo.ProofSkipBriefing(); }
                if (!brief && !fly && !window && ZodCinePreview.I == null) { if (calm < 0f) calm = Time.realtimeSinceStartup; if (Time.realtimeSinceStartup - calm > 0.3f) yield break; }
                else calm = -1f;
                yield return null;
            }
        }

        static OpenZMainMenu Menu() => Object.FindFirstObjectByType<OpenZMainMenu>();

        [MenuItem("OpenZ/V1.4/Mayhem: Play Proof (MainMenu scene, Play; about 3 to 5 minutes)")]
        public static void PlayProof() => Run(PlayRoutine());

        static IEnumerator PlayRoutine()
        {
            Begin("Play Proof (MainMenu scene, Play): the picker on MAYHEM, a MAYHEM match with the fog, the end screen naming MAYHEM, a HARD match without");
            shots.Clear();
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", FramesDir)); Directory.CreateDirectory(dir);
            var menu = Menu();
            if (!menu) { Row("P0", false, "no main menu: open the MainMenu scene, press Play, wait for the title screen, then run this again"); End(PlayLog); yield break; }
            var keep = new Prefs(OpenZDifficulty.Key, OpenZDifficulty.MayhemKey, "oz_zod_exact", "oz_botmatch", "oz_testmode", OpenZDifficulty.HeadStartPref);
            bool boxWas = OzPlayBlue.Box;
            Say("  the player's settings before the proof: " + keep.Text + $", PLAY AS BLUE {(boxWas ? "on" : "off")}; frames go to {dir}");
            string tablePath = Path.Combine(Application.temporaryCachePath, "oz_mayhem_play_scores.json"); bool staged = false;
            try
            {
                PlayerPrefs.SetInt("oz_zod_exact", 0); PlayerPrefs.SetInt("oz_botmatch", 0); PlayerPrefs.SetInt("oz_testmode", 0); OzPlayBlue.Box = false;
                OpenZDifficulty.Choice = GameDifficulty.Mayhem;

                // ---- P1 OPTIONS (main menu): MAIN has four buttons, ADVANCED no FOG row
                menu.ProofMain(); yield return Seconds(0.8f);
                menu.ProofOpen("options"); yield return Seconds(0.5f);
                menu.ProofOptTab(OzSettingTips.TabMain); yield return Seconds(0.4f);
                string optLit = menu.ProofOptDifficultyLit;
                yield return Shot("01_main_options_difficulty_mayhem");
                menu.ProofOptTab(OzSettingTips.TabAdvanced); yield return Seconds(0.4f);
                var rows = menu.ProofOptTabRows.Split(new[] { " | " }, StringSplitOptions.None).ToList();
                Row("P1", optLit == "OptMayhem" && rows.Count > 0 && !rows.Contains(OzFogOfWarOption.Label) && !rows.Contains(OzFogLookOption.Label),
                    $"OPTIONS (main menu): MAIN lights \"{optLit}\" (MAYHEM chosen); ADVANCED has no FOG OF WAR or FOG LOOK row (rows: {string.Join(" | ", rows)})");
                yield return Shot("02_main_options_advanced_no_fog_rows");
                menu.ProofOpen(""); yield return Seconds(0.4f);

                // ---- P2 the battlefield screen: C from HARD to MAYHEM
                menu.ProofSelect(); yield return Seconds(0.8f);
                int card = Array.FindIndex(OzLevelDef.All, d => d.Id == "L02");
                menu.ProofFocus(card); yield return Seconds(0.6f);
                OpenZDifficulty.Choice = GameDifficulty.Hard; yield return Seconds(0.4f);
                string litHard = menu.ProofDifficultyLit;
                menu.ProofDifficultyKey(); yield return Seconds(0.8f);
                var chip = Object.FindObjectsByType<OzBestChip>(FindObjectsSortMode.None).Select(c => c.Shown).FirstOrDefault(s => s.Contains("MAYHEM")) ?? "(no card says MAYHEM)";
                bool four = Enumerable.Range(0, 4).All(i => menu.ProofDifficultyButton(i));
                Row("P2", litHard == "CpuHard" && menu.ProofDifficultyLit == "CpuMayhem" && OpenZDifficulty.Choice == GameDifficulty.Mayhem && OpenZDifficulty.Level == GameDifficulty.Hard
                          && menu.ProofDifficultyHint == OzDeployBar.CpuMayhem && chip.Contains("MAYHEM") && four,
                    $"the C key from HARD (\"{litHard}\") lights \"{menu.ProofDifficultyLit}\", the hint \"{menu.ProofDifficultyHint}\", the level card \"{chip}\"; saved oz_difficulty {PlayerPrefs.GetInt(OpenZDifficulty.Key, -1)} + oz_mayhem {PlayerPrefs.GetInt(OpenZDifficulty.MayhemKey, -1)}; four buttons built {four}");
                yield return Shot("03_picker_mayhem_selected");

                // ---- P3 the C key's round
                var seen = new List<string>();
                for (int i = 0; i < 4; i++) { menu.ProofDifficultyKey(); yield return Seconds(0.3f); seen.Add(menu.ProofDifficultyLit); }
                Row("P3", seen.SequenceEqual(new[] { "CpuEasy", "CpuMedium", "CpuHard", "CpuMayhem" }) && OpenZDifficulty.Choice == GameDifficulty.Mayhem,
                    "the C key again, four times: " + string.Join(", ", seen) + " (back on MAYHEM)");

                // ---- P4 DEPLOY L02 on MAYHEM
                menu.ProofDeploy();
                yield return Until(() => ZGame.I && ZGame.I.World != null && OzFogOfWar.I && SceneManager.GetActiveScene().name != OzPlayBlue.MenuScene, 90f);
                yield return Until(() => OpenZHud.I && Time.timeSinceLevelLoad > 2f, 30f);
                yield return Until(() => OzFogOfWar.Wanted || OzFogOfWar.WhyOffNow != null, 15f);                // the fog decides once, at the match's start
                var g = ZGame.I;
                if (!g || g.World == null) { Row("P4", false, "the level did not start after DEPLOY"); yield break; }
                string scene = SceneManager.GetActiveScene().name;
                bool cpuHard = g.World.Bots.Count > 0 && g.World.Bots.All(b => b.Difficulty == ZBotDifficulty.Normal);
                Row("P4", g.level == "L02" && g.IsMayhem && g.gameDifficulty == GameDifficulty.Hard && g.ShownDifficulty == GameDifficulty.Mayhem && cpuHard && ZWorld.FortDoorGunDeviation
                          && g.World.TechBonus[(int)g.player] == 0 && OzFogOfWar.Wanted && OzFogOfWar.WhyOffNow == null,
                    $"DEPLOY {g.level} on MAYHEM: rules {OpenZDifficulty.Label(g.gameDifficulty)}, shown {OpenZDifficulty.Label(g.ShownDifficulty)}, the CPU {string.Join("/", g.World.Bots.Select(b => b.Difficulty.ToString()))}, DV73 fort missile cannons {ZWorld.FortDoorGunDeviation} ({g.World.FortDoorGuns.Count}), tech bonus +{g.World.TechBonus[(int)g.player]}; the fog {(OzFogOfWar.Wanted ? "ON" : "off (" + OzFogOfWar.WhyOffNow + ")")}");

                // ---- P5 gameplay: the fog after the hand-over
                yield return ToPlay(150f);
                yield return Until(GateOpen, 20f);
                yield return Seconds(0.5f);
                float s5 = OzFogOfWar.Strength;
                Row("P5", OzFogOfWar.Wanted && GateOpen() && s5 >= 0.99f, $"the first gameplay after the briefing's hand-over + 0.5 s: the fog drawn at {s5:0.00} (the gate {(GateOpen() ? "open" : "closed")})");
                yield return Shot("04_mayhem_L02_play_fog");

                // ---- P6 the pause menu's OPTIONS, ADVANCED
                var hud = OpenZHud.I; var pm = hud ? hud.Pause : null;
                if (pm)
                {
                    pm.OpenOptions(OzSettingTips.TabAdvanced); yield return Seconds(0.6f);
                    var prow = pm.ProofOptTabRows.Split(new[] { " | " }, StringSplitOptions.None).ToList();
                    Row("P6", pm.IsOpen && prow.Count > 0 && !prow.Contains(OzFogOfWarOption.Label) && !prow.Contains(OzFogLookOption.Label), $"the pause menu's OPTIONS, ADVANCED: no FOG row (rows: {string.Join(" | ", prow)})");
                    yield return Shot("05_pause_options_advanced_no_fog_rows");
                    pm.Close(); yield return Seconds(0.5f);
                }
                else Row("P6", false, "no pause menu found on the HUD");

                // ---- P7 the end screen of a staged victory: MAYHEM's own table (a file in the temporary folder, the player's table untouched)
                hud = OpenZHud.I; g = ZGame.I;
                var es = hud ? hud.EndScreen : null; var show = typeof(OpenZEndScreen).GetMethod("Show", Any);
                var shownProp = typeof(OpenZEndScreen).GetProperty("IsShown", Any); var setShown = shownProp != null ? shownProp.GetSetMethod(true) : null;
                var submitted = typeof(OpenZEndScreen).GetField("scoreSubmitted", Any); var noteF = typeof(OpenZEndScreen).GetField("scoreNote", Any); var tableHot = typeof(OpenZEndScreen).GetField("scoreTableHot", Any);
                if (!es || show == null || setShown == null || noteF == null) Row("P7", false, "the end screen cannot be staged here (end screen, Show, IsShown or scoreNote not found)");
                else
                {
                    File.WriteAllText(tablePath, "{\"version\":1,\"tables\":[]}");
                    OzHighScores.UseFile(tablePath); staged = true;
                    if (es.IsShown) setShown.Invoke(es, new object[] { false });
                    if (submitted != null) submitted.SetValue(es, false);
                    show.Invoke(es, new object[] { hud.Player });
                    yield return Seconds(4.6f);
                    string note = (noteF.GetValue(es) as OzText)?.text ?? "";
                    string tip = (tableHot?.GetValue(es) as HudButton)?.Tooltip ?? "";
                    string want = "NEW RECORD · " + g.level + " · MAYHEM";
                    Row("P7", note == want && tip.StartsWith("TOP " + OzHighScores.TableSize + " · " + g.level + " · MAYHEM|") && OzHighScores.Top(g.level, GameDifficulty.Mayhem).Count == 1 && OzHighScores.Top(g.level, GameDifficulty.Hard).Count == 0,
                        $"the end screen of a victory: the note \"{note}\" (wants \"{want}\"), the TOP 10 tooltip \"{(tip.Contains("|") ? tip.Substring(0, tip.IndexOf('|')) : tip)}\"; the staged table: MAYHEM {OzHighScores.Top(g.level, GameDifficulty.Mayhem).Count} entry, HARD {OzHighScores.Top(g.level, GameDifficulty.Hard).Count}"
                        + (OzHighScores.Unranked != null ? $" (NOTE: this match is unranked: {OzHighScores.Unranked})" : ""));
                    yield return Shot("06_end_screen_mayhem");
                    OzHighScores.UseFile(null); staged = false;
                }

                // ---- P8 the same level on HARD: no fog
                OpenZDifficulty.Choice = GameDifficulty.Hard;
                var old = ZGame.I;
                SceneManager.LoadScene(scene);
                yield return Until(() => ZGame.I && ZGame.I != old && ZGame.I.World != null && OzFogOfWar.I, 120f);
                yield return Until(() => OpenZHud.I && Time.timeSinceLevelLoad > 2f, 30f);
                yield return Until(() => OzFogOfWar.Wanted || OzFogOfWar.WhyOffNow != null, 15f);
                yield return ToPlay(150f);
                yield return Seconds(OzFogOfWar.FadeInSeconds + 0.5f);
                g = ZGame.I;
                string why = OzFogOfWar.WhyOffNow ?? "";
                Row("P8", g && !g.IsMayhem && g.ShownDifficulty == GameDifficulty.Hard && !OzFogOfWar.Wanted && OzFogOfWar.Strength <= 0f && why.StartsWith("not MAYHEM"),
                    $"{(g ? g.level : "-")} on HARD after the hand-over: the fog {(OzFogOfWar.Wanted ? "ON" : "off")} ({why}), drawn at {OzFogOfWar.Strength:0.00}; the log says \"[OpenZ FogOfWar] off for this match ({why})\"");
                yield return Shot("07_hard_L02_play_no_fog");
            }
            finally
            {
                if (staged) OzHighScores.UseFile(null);
                keep.PutBack(); OzPlayBlue.Box = boxWas;
                Say($"  put back: {keep.Text}, PLAY AS BLUE {(boxWas ? "on" : "off")}; the player's score table untouched (a file of its own: {tablePath}); frames ({shots.Count}): {string.Join(", ", shots)} in {dir}");
                End(PlayLog);
            }
        }
    }
}
