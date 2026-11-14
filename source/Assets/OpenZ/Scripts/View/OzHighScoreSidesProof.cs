#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OpenZ.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// (Play), Editor/OzV12HighScoreSidesCheck.cs, or OzHighScoreSidesProof.Run(). Started on the MainMenu scene in Play it walks by itself,
    /// hands off (about ten minutes), on a table file of its own in the temporary folder (the editor's own table is not touched):
    ///  S  the staged file: Desert EASY holds one entry from before the side was kept (10 250, no side in the file: it reads ZOD); Desert
    ///     MEDIUM ten entries of both sides with the widest numbers and the longest rank names (the layout check M3).
    ///  B1 DEPLOY Desert on EASY as GENERAL EVILZOD (the faction pick, the player's own DEPLOY), the briefing skipped as SPACE does, then a
    ///     FORCED VICTORY through the game's own win path (OpenZVictory.DebugFortFalls: the red fort's parts take a killing hit; the real
    ///     chain follows: the fort falls, EvilZod's nuke scene, the strike, the win scene, the end screen). The end screen: VICTORY for
    ///     blue, entered (no "NOT IN THE TABLE"), place 1 of Desert EASY, the entry blue with the shown score; the TOP 10 tooltip's lines
    ///     EVILZOD (marked) and ZOD (the old entry); the file names the side; MEDIUM / HARD untouched. Picture.
    ///  B2 REPLAY (still blue), the blue fort falls (DebugFortFalls on our own fort): DEFEAT, "WIN THE LEVEL TO ENTER THE TABLE", no place,
    ///     the table unchanged. Picture.
    ///  M1 the main menu, the battlefield screen's HIGH SCORES on EASY: Desert row 1 EVILZOD on the blue chip, row 2 ZOD on the red chip,
    ///     every cell as the table; Jungle and Car Park empty. Picture and a close-up of the Desert block.
    ///  R1 DEPLOY Desert on EASY as GENERAL ZOD, a forced victory the same way: VICTORY for red, entered with the red side, the blue row
    ///     kept with its side. Picture. R2 REPLAY, the red fort falls: DEFEAT, not entered, the table unchanged. Picture.
    ///  M2 HIGH SCORES on EASY: three rows, every tag the side of its entry (EVILZOD once, ZOD twice). Picture and close-up.
    ///  M3 HIGH SCORES on MEDIUM (ten rows of both sides, the widest values): every text of the popup inside the plate (the table's inside
    ///     the view), at its full size, none meets another; every tag inside its chip, every chip inside the view, the chips in one
    ///     straight column. Picture and close-up.
    /// At the end the side, the difficulty and the table file are put back. Checks and pictures go to the kit's proof folder
    /// (k90_blue_high_scores/proof/play_n). Test only (editor).
    /// </summary>
    public sealed class OzHighScoreSidesProof : MonoBehaviour
    {
        public static string Report = "idle";
        public static OzHighScoreSidesProof I { get; private set; }
        const string T = "[OzHighScoreSidesProof] ";
        const string Level = "L02";
        public const string TableFile = "oz_hs_sides_play_proof.json";

        bool started, putBack, boxWas, testWas, botWas; GameDifficulty diffWas;
        string dir, path; readonly StringBuilder sb = new StringBuilder(); int passes, fails, pics;
        OzScoreEntry blueEntry, redEntry;

        /// <summary></summary>
        public static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var staging = Path.Combine(d.FullName, "work", "staging");
                if (!Directory.Exists(staging)) continue;
                foreach (var stage in Directory.GetDirectories(staging))
                {
                    var kit = Path.Combine(stage, "k90_blue_high_scores");
                    if (Directory.Exists(kit)) return Path.Combine(kit, "proof");
                }
            }
            return Path.Combine(Application.persistentDataPath, "k90_blue_high_scores");
        }

        public static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning(T + "enter Play on the MainMenu scene first"); return; }
            if (I) { Debug.LogWarning(T + "already running"); return; }
            if (!FindFirstObjectByType<OpenZMainMenu>()) { Debug.LogWarning(T + "start it on the main menu (the MainMenu scene in Play)"); return; }
            Report = "running";
            var go = new GameObject("OzHighScoreSidesProof"); DontDestroyOnLoad(go);
            go.AddComponent<OzHighScoreSidesProof>();
        }

        public static void Stop()
        {
            if (!I) { Debug.Log(T + "not running"); return; }
            I.StopAllCoroutines(); I.Log("stopped by the menu"); I.Finish();
        }

        void Awake() { I = this; }

        void Start()
        {
            string root = Folder(); int n = 1;
            while (Directory.Exists(Path.Combine(root, "play_" + n))) n++;
            dir = Path.Combine(root, "play_" + n); Directory.CreateDirectory(dir);
            boxWas = OzPlayBlue.Box; diffWas = OpenZDifficulty.Level; testWas = GameBoot.TestMode; botWas = PlayerPrefs.GetInt("oz_botmatch", 0) == 1;
            started = true;
            Log("k90 high score sides, Play proof, Game view " + Screen.width + " x " + Screen.height + ", into " + dir);
            if (testWas) { PlayerPrefs.SetInt("oz_testmode", 0); Log("test mode was on: off for this run (a test session never enters the table), put back at the end"); }
            if (botWas) { PlayerPrefs.SetInt("oz_botmatch", 0); Log("the bot match setting was on: off for this run, put back at the end"); }
            StartCoroutine(All());
        }

        void OnDestroy()
        {
            if (started && !putBack) { PutBack(); Debug.Log(T + "stopped before its end: the side, the difficulty and the table file put back"); }
            if (I == this) I = null;
        }

        void PutBack()
        {
            putBack = true;
            OzHighScores.UseFile(null);
            OzPlayBlue.Box = boxWas;
            OpenZDifficulty.Level = diffWas;
            if (testWas) PlayerPrefs.SetInt("oz_testmode", 1);
            if (botWas) PlayerPrefs.SetInt("oz_botmatch", 1);
        }

        void Finish()
        {
            var m = Menu(); if (m && m.ProofHighScoresOpen) m.ProofHighScores(false);
            if (!putBack) PutBack();
            Report = $"{T}{(fails == 0 ? "PASS" : "FAIL")} {passes}/{passes + fails} checks, {pics} pictures; {dir.Replace('\\', '/')}";
            Log(Report);
            File.WriteAllText(Path.Combine(dir, "checks.txt"), sb.ToString());
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------------------------------------ helpers
        void Log(string t) { sb.AppendLine(t); Debug.Log(T + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
        static IEnumerator Until(System.Func<bool> c, float max) { float t = Time.realtimeSinceStartup + max; while (Time.realtimeSinceStartup < t && !SafeTrue(c)) yield return null; }
        static bool SafeTrue(System.Func<bool> c) { try { return c(); } catch (System.Exception) { return false; } }
        static OpenZMainMenu Menu() => FindFirstObjectByType<OpenZMainMenu>();
        static string Plain(string s) => Regex.Replace(s ?? "", "<[^>]+>", "");
        static string Label(GameDifficulty d) => OpenZDifficulty.Label(d);

        IEnumerator Shot(string name)
        {
            string p = Path.Combine(dir, name + "_" + Screen.width + "x" + Screen.height + ".png");
            ScreenCapture.CaptureScreenshot(p); pics++; Log("   picture " + Path.GetFileName(p));
            yield return OzProofPictures.AfterGrab(); yield return OzProofPictures.Written(p);
        }

        /// <summary>A close-up of a screen rect (screen pixels, bottom-left origin), from a capture of the frame.</summary>
        IEnumerator Crop(string name, Rect r)
        {
            yield return new WaitForEndOfFrame();
            var full = ScreenCapture.CaptureScreenshotAsTexture();
            int x0 = Mathf.Clamp(Mathf.FloorToInt(r.xMin), 0, full.width - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(r.yMin), 0, full.height - 1);
            int w = Mathf.Clamp(Mathf.CeilToInt(r.width), 1, full.width - x0), h = Mathf.Clamp(Mathf.CeilToInt(r.height), 1, full.height - y0);
            var c = new Texture2D(w, h, TextureFormat.RGBA32, false);
            c.SetPixels(full.GetPixels(x0, y0, w, h)); c.Apply(false);
            File.WriteAllBytes(Path.Combine(dir, name + "_" + Screen.width + "x" + Screen.height + ".png"), c.EncodeToPNG());
            Destroy(c); Destroy(full); pics++;
            Log("   picture " + name + " (a close-up)");
        }

        static readonly Vector3[] corners = new Vector3[4];
        /// <summary>A rect on the overlay canvas in screen pixels (bottom-left origin).</summary>
        static Rect Px(RectTransform rt)
        {
            rt.GetWorldCorners(corners);
            return Rect.MinMaxRect(Mathf.Min(corners[0].x, corners[2].x), Mathf.Min(corners[0].y, corners[2].y), Mathf.Max(corners[0].x, corners[2].x), Mathf.Max(corners[0].y, corners[2].y));
        }
        /// <summary>The line box of a shown text (false: no text, hidden or empty), as the popup's own proof measures it.</summary>
        static bool Ink(Component c, out Rect r)
        {
            r = default;
            var t = c ? c.GetComponent<TMP_Text>() : null;
            if (!t || !t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) return false;
            t.ForceMeshUpdate();
            var b = t.textBounds; Vector3 a = t.transform.TransformPoint(b.min), z = t.transform.TransformPoint(b.max);
            r = Rect.MinMaxRect(Mathf.Min(a.x, z.x), Mathf.Min(a.y, z.y), Mathf.Max(a.x, z.x), Mathf.Max(a.y, z.y));
            return r.width > 0.01f && r.height > 0.01f;
        }
        static bool Meet(Rect a, Rect b, float slack = 0.5f) => a.xMin < b.xMax - slack && b.xMin < a.xMax - slack && a.yMin < b.yMax - slack && b.yMin < a.yMax - slack;
        static bool Inside(Rect a, Rect b, float slack = 0.5f) => a.xMin >= b.xMin - slack && a.xMax <= b.xMax + slack && a.yMin >= b.yMin - slack && a.yMax <= b.yMax + slack;

        /// <summary>A table as "ZOD 10250, EVILZOD 19876" (side and score, best first).</summary>
        static string Line(IReadOnlyList<OzScoreEntry> list)
        {
            var s = new StringBuilder();
            foreach (var e in list) { if (s.Length > 0) s.Append(", "); s.Append(OzScoreSides.TagOf(e)).Append(' ').Append(e.score).Append(" (").Append(UnitInfo.Duration(e.seconds)).Append(')'); }
            return s.ToString();
        }

        // ------------------------------------------------------------------------------------------------ the run
        IEnumerator All()
        {
            Stage();
            yield return Deploy(OzFaction.Blue, "B1");
            if (ZGame.I && ZGame.I.player == Team.Blue)
            {
                yield return Win(true, "B1", "b1_blue_win_end_screen");
                yield return Replay("B2", Team.Blue);
                yield return Lose(true, "B2", "b2_blue_loss_end_screen");
            }
            yield return ToMenu("M1");
            yield return Popup(GameDifficulty.Easy, "M1", "m1_high_scores_easy_after_blue_win");
            yield return Deploy(OzFaction.Red, "R1");
            if (ZGame.I && ZGame.I.player == Team.Red)
            {
                yield return Win(false, "R1", "r1_red_win_end_screen");
                yield return Replay("R2", Team.Red);
                yield return Lose(false, "R2", "r2_red_loss_end_screen");
            }
            yield return ToMenu("M2");
            yield return Popup(GameDifficulty.Easy, "M2", "m2_high_scores_easy_both_sides");
            yield return Popup(GameDifficulty.Medium, "M3", "m3_high_scores_medium_widest_mixed");
            Finish();
        }

        /// <summary>S: the table file of this run.</summary>
        void Stage()
        {
            path = Path.Combine(Application.temporaryCachePath, TableFile);
            string old = "{\"version\":1,\"tables\":[{\"level\":\"" + Level + "\",\"difficulty\":\"" + Label(GameDifficulty.Easy) + "\",\"entries\":["
                         + "{\"score\":10250,\"seconds\":1800,\"kills\":5,\"losses\":5,\"built\":9,\"zones\":3,\"zonesTotal\":7}]}]}";
            File.WriteAllText(path, old, new UTF8Encoding(false));                                           // written fresh every run (an entry from before the side: no side field)
            OzHighScores.UseFile(path);
            var had = OzHighScores.Top(Level, GameDifficulty.Easy);
            for (int i = 0; i < 10; i++)
                OzHighScores.Submit(Level, GameDifficulty.Medium, new OzScoreEntry
                {
                    score = 25000 - 1300 * i, seconds = i == 0 ? 5025 : 400 + 61 * i, kills = i == 0 ? 999 : 40 - i, losses = i == 0 ? 999 : 10 + i,
                    built = i == 0 ? 999 : 60 + i, zones = i == 0 ? 16 : 7 - i % 3, zonesTotal = i == 0 ? 16 : 9, side = i % 2 == 0 ? OzScoreSides.Blue : OzScoreSides.Red,
                });
            Check($"S the staged table file {TableFile} (in the temporary folder): Desert EASY \"{Line(had)}\" (an entry from before the side, read as ZOD), Desert MEDIUM 10 entries of both sides "
                  + $"({OzHighScores.Top(Level, GameDifficulty.Medium).Count}), HARD none",
                  had.Count == 1 && had[0].score == 10250 && OzScoreSides.TagOf(had[0]) == "ZOD" && OzHighScores.Top(Level, GameDifficulty.Medium).Count == 10 && OzHighScores.Top(Level, GameDifficulty.Hard).Count == 0);
        }

        IEnumerator Deploy(int side, string tag)
        {
            var m = Menu();
            if (!m) { yield return Until(() => Menu(), 60f); m = Menu(); }
            if (!m) { Check(tag + " on the main menu before DEPLOY", false); yield break; }
            bool blue = side == OzFaction.Blue;
            OzFaction.Choose(side, "a Play proof");
            OpenZDifficulty.Level = GameDifficulty.Easy;
            yield return Real(0.8f);
            m.ProofSelect(); yield return Real(0.8f);
            int card = System.Array.FindIndex(OzLevelDef.All, d => d.Id == Level);
            m.ProofFocus(card); yield return Real(0.6f);
            m.ProofDeploy();
            yield return Until(() => ZGame.I && ZGame.I.World != null && SceneManager.GetActiveScene().name != OzPlayBlue.MenuScene, 90f);
            yield return Until(() => OpenZHud.I && Time.timeSinceLevelLoad > 2f, 30f);
            var g = ZGame.I;
            bool ok = g && g.level == Level && g.gameDifficulty == GameDifficulty.Easy
                      && (blue ? OzPlayBlue.Active && g.player == Team.Blue : !OzPlayBlue.Active && g.player == Team.Red);
            Check($"{tag} DEPLOY {Level} on EASY as {OzFaction.SideName(blue)}: the level {(g ? g.level : "none")}, {(g ? Label(g.gameDifficulty) : "-")}, the human {(g ? g.player.ToString() : "-")}, PLAY AS BLUE {OzPlayBlue.Active}", ok);
            if (ok) yield return Settle(tag);
        }

        /// <summary>The level's start: the briefing skipped as SPACE does (after a second), the flyover over, then a moment.</summary>
        IEnumerator Settle(string tag)
        {
            float t0 = Time.realtimeSinceStartup; bool skipped = false;
            while (Time.realtimeSinceStartup - t0 < 60f)
            {
                float t = Time.realtimeSinceStartup - t0;
                if (t > 1f && ZodBriefing.BriefingActive && ZodBriefing.ProofSkipBriefing()) skipped = true;
                if (t > 1f && OzBlueCameo.BriefingActive && OzBlueCameo.ProofSkipBriefing()) skipped = true;
                bool fly = ZodFlyover.Live != null && ZodFlyover.Live.Active;
                if (t > 2f && !ZodBriefing.BriefingActive && !OzBlueCameo.BriefingActive && !fly) break;
                yield return null;
            }
            yield return Real(1.5f);
            Log($"{tag} the start: briefing {(skipped ? "skipped (as SPACE)" : "not running")}, flyover {(ZodFlyover.Live != null && ZodFlyover.Live.Active ? "STILL RUNNING" : "over")}, after {Time.realtimeSinceStartup - t0:0.0} s; game time {(ZGame.I && ZGame.I.World != null ? ZGame.I.World.Time : 0):0.0} s");
        }

        IEnumerator WaitEndScreen(string tag)
        {
            var hud = OpenZHud.I;
            yield return Until(() => OpenZHud.I && OpenZHud.I.EndScreen && OpenZHud.I.EndScreen.IsShown, 180f);
            hud = OpenZHud.I;
            if (hud && hud.EndScreen && hud.EndScreen.IsShown) yield return Until(() => hud.EndScreen.ProofScoreLive, 15f);
            yield return Real(1.2f);
        }

        /// <summary>B1 / R1: a forced victory through the game's own win path, then the end screen and the table.</summary>
        IEnumerator Win(bool blue, string tag, string shot)
        {
            var g = ZGame.I; var W = g.World; Team me = g.player, enemy = me == Team.Blue ? Team.Red : Team.Blue;
            var easyBefore = new List<OzScoreEntry>(OzHighScores.Top(Level, GameDifficulty.Easy));
            int n = OpenZVictory.DebugFortFalls(true);
            Log($"{tag} a FORCED VICTORY through the game's own win path: OpenZVictory.DebugFortFalls gave the {n} part(s) of the {enemy} fort a killing hit at game time {W.Time:0.0} s; "
                + $"the real chain follows (the fort falls, {(blue ? "EvilZod's nuke scene" : "Zod's nuke transmission")}, the strike, the ending, the end screen)");
            yield return WaitEndScreen(tag);
            var hud = OpenZHud.I; var es = hud ? hud.EndScreen : null;
            if (!es || !es.IsShown) { Check(tag + " the end screen came (within 180 s)", false); yield break; }
            var e = es.ProofScoreEntry; var top = OzHighScores.Top(Level, GameDifficulty.Easy);
            string tip = es.ProofScoreTableTip; var tipLines = tip.Split('\n');
            string file = File.Exists(path) ? File.ReadAllText(path) : "";
            Check($"{tag} {W.Winner} won: VICTORY for {me} ({OzSides.Outcome(W.Winner, g.player)}), score {es.ProofScore} {es.ProofRank}", W.GameOver && W.Winner == me && OzSides.Outcome(W.Winner, g.player) == "victory");
            Check($"{tag} the end screen enters it: not kept out (\"{es.ProofUnranked ?? "entered"}\"), place {es.ProofPlace}, the side of this match {es.ProofScoreSide} ({OzScoreSides.Tag[Mathf.Clamp(es.ProofScoreSide, 0, 1)]})",
                  es.ProofUnranked == null && es.ProofPlace >= 1 && es.ProofScoreSide == OzScoreSides.Of(me));
            bool entryOk = e != null && e.side == OzScoreSides.Of(me) && e.score == es.ProofScore && top.Contains(e) && top.Count == easyBefore.Count + 1;
            foreach (var x in easyBefore) entryOk &= top.Contains(x);
            if (blue) entryOk &= es.ProofPlace == 1 && top[0] == e;                                       // above the old 10 250
            else entryOk &= blueEntry != null && top.Contains(blueEntry) && blueEntry.side == OzScoreSides.Blue;   // the blue row kept with its side
            Check($"{tag} Desert EASY now \"{Line(top)}\": the new entry {(e != null ? OzScoreSides.TagOf(e) + " " + e.score : "none")} at place {es.ProofPlace}, every earlier row kept with its side", entryOk);
            bool tipOk = tipLines.Length == top.Count;
            for (int i = 0; i < tipLines.Length && i < top.Count; i++)
                tipOk &= Plain(tipLines[i]).StartsWith((i + 1) + ".  " + OzScoreSides.TagOf(top[i]) + "  ", System.StringComparison.Ordinal)
                         && tipLines[i].Contains("<color=" + OzUi.Hex(OzScoreSides.Side(top[i]) == OzScoreSides.Blue ? OzUi.BlueText : OzUi.RedText) + ">");
            Check($"{tag} the TOP 10 tooltip's lines carry their sides in their colours: \"{Plain(tip).Replace("\n", " / ")}\"", tipOk);
            int ones = Regex.Matches(file, "\"side\"\\s*:\\s*1").Count;
            Check($"{tag} the table file names the sides ({ones} entr{(ones == 1 ? "y" : "ies")} with side 1: Desert MEDIUM's five and the blue win); MEDIUM {OzHighScores.Top(Level, GameDifficulty.Medium).Count} rows, HARD {OzHighScores.Top(Level, GameDifficulty.Hard).Count} (untouched)",
                  ones == 6 && OzHighScores.Top(Level, GameDifficulty.Medium).Count == 10 && OzHighScores.Top(Level, GameDifficulty.Hard).Count == 0);
            if (blue) blueEntry = e; else redEntry = e;
            yield return Shot(shot);
        }

        /// <summary>B2 / R2: our own fort falls: a defeat is never entered.</summary>
        IEnumerator Lose(bool blue, string tag, string shot)
        {
            var g = ZGame.I; if (!g || g.World == null) { Check(tag + " a level after REPLAY", false); yield break; }
            var W = g.World; Team me = g.player;
            string before = Line(OzHighScores.Top(Level, GameDifficulty.Easy));
            int n = OpenZVictory.DebugFortFalls(false);
            Log($"{tag} a forced defeat the same way: DebugFortFalls on our own fort ({n} part(s)) at game time {W.Time:0.0} s");
            yield return WaitEndScreen(tag);
            var hud = OpenZHud.I; var es = hud ? hud.EndScreen : null;
            if (!es || !es.IsShown) { Check(tag + " the end screen came (within 180 s)", false); yield break; }
            string after = Line(OzHighScores.Top(Level, GameDifficulty.Easy));
            Check($"{tag} {W.Winner} won: DEFEAT for {me} ({OzSides.Outcome(W.Winner, g.player)}); the end screen \"{es.ProofUnranked}\", place {es.ProofPlace}, score {es.ProofScore} {es.ProofRank}; the table unchanged (\"{after}\")",
                  W.GameOver && W.Winner != me && OzSides.Outcome(W.Winner, g.player) == "defeat" && es.ProofUnranked == "WIN THE LEVEL TO ENTER THE TABLE"
                  && es.ProofPlace == 0 && es.ProofScoreEntry == null && es.ProofScore < OzHighScores.VictoryPoints && after == before);
            yield return Shot(shot);
        }

        IEnumerator Replay(string tag, Team side)
        {
            var g0 = ZGame.I;
            OzScenes.Restart();
            yield return Until(() => ZGame.I && ZGame.I != g0 && ZGame.I.World != null, 90f);
            yield return Until(() => OpenZHud.I && Time.timeSinceLevelLoad > 2f, 30f);
            var g = ZGame.I;
            Check($"{tag} REPLAY keeps the side (the human {(g ? g.player.ToString() : "none")}, PLAY AS BLUE {OzPlayBlue.Active})", g && g != g0 && g.player == side && OzPlayBlue.Active == (side == Team.Blue));
            if (g) yield return Settle(tag);
        }

        IEnumerator ToMenu(string tag)
        {
            if (!Menu()) OzScenes.MainMenu();
            yield return Until(() => Menu(), 60f);
            yield return Real(1.2f);
            Check($"{tag} back on the main menu (the blue mode ended there: {!OzPlayBlue.Active})", Menu() && !OzPlayBlue.Active);
        }

        /// <summary>M1 / M2 / M3: the battlefield screen's HIGH SCORES popup on a difficulty: every Desert row's tag is its entry's side.</summary>
        IEnumerator Popup(GameDifficulty d, string tag, string shot)
        {
            var m = Menu(); if (!m) { Check(tag + " the main menu is up", false); yield break; }
            if (m.ProofHighScoresOpen) { m.ProofHighScores(false); yield return Real(0.4f); }
            m.ProofSelect(); yield return Real(0.8f);
            OpenZDifficulty.Level = d; yield return Real(0.3f);
            m.ProofHighScores(true); yield return Real(0.9f);
            int tab = System.Array.IndexOf(new[] { GameDifficulty.Easy, GameDifficulty.Medium, GameDifficulty.Hard }, d);
            var ids = OzLevelDef.All; var top = OzHighScores.Top(Level, d); int rows = m.ProofHighScoresRows(0);
            bool good = m.ProofHighScoresOpen && m.ProofHighScoresTab == tab && ids[0].Id == Level && rows == Mathf.Min(top.Count, OzHighScores.TableSize);
            var seen = new StringBuilder(); int blueRows = 0, redRows = 0;
            for (int r = 0; r < rows && r < top.Count; r++)
            {
                var e = top[r]; string tagText = Plain(m.ProofHighScoresSide(0, r)); Color paint = m.ProofHighScoresSidePaint(0, r);
                bool rowOk = tagText == OzScoreSides.TagOf(e) && paint == OzScoreSides.Paint(OzScoreSides.Side(e))
                             && Plain(m.ProofHighScoresCell(0, r, 1)) == OzHighScores.Rank(e.score) && Plain(m.ProofHighScoresCell(0, r, 2)) == OzHighScores.Group(e.score)
                             && Plain(m.ProofHighScoresCell(0, r, 3)) == UnitInfo.Duration(e.seconds);
                good &= rowOk;
                if (OzScoreSides.Side(e) == OzScoreSides.Blue) blueRows++; else redRows++;
                seen.Append(r == 0 ? "" : " / ").Append(r + 1).Append(". ").Append(tagText).Append(paint == (Color)OzUi.Blue ? " (blue chip) " : paint == (Color)OzUi.Red ? " (red chip) " : " (? chip) ").Append(Plain(m.ProofHighScoresCell(0, r, 1))).Append(' ').Append(Plain(m.ProofHighScoresCell(0, r, 2)));
                if (!rowOk) seen.Append(" WRONG (the table: ").Append(OzScoreSides.TagOf(e)).Append(' ').Append(OzHighScores.Rank(e.score)).Append(' ').Append(OzHighScores.Group(e.score)).Append(')');
            }
            for (int li = 1; li < ids.Length; li++) good &= m.ProofHighScoresRows(li) == OzHighScores.Top(ids[li].Id, d).Count;
            if (tag == "M1") good &= rows == 2 && blueRows == 1 && redRows == 1 && m.ProofHighScoresSide(0, 0) == "EVILZOD" && m.ProofHighScoresSide(0, 1) == "ZOD";
            if (tag == "M2") good &= rows == 3 && blueRows == 1 && redRows == 2 && redEntry != null && top.Contains(redEntry) && blueEntry != null && top.Contains(blueEntry);
            if (tag == "M3") good &= rows == 10 && blueRows == 5 && redRows == 5;
            Check($"{tag} HIGH SCORES on {Label(d)} (tab {m.ProofHighScoresTab}): Desert {rows} row(s): {seen}; the other levels as their tables", good);
            if (tag == "M3") Layout(m, tag);
            yield return Shot(shot);
            var block = m.ProofHighScoresBlock(0);
            if (block) { var br = Px(block); yield return Crop(shot + "_desert", new Rect(br.xMin - 8f, br.yMin - 8f, br.width + 16f, br.height + 16f)); }
            m.ProofHighScores(false); yield return Real(0.5f);
        }

        /// <summary>M3: the popup's texts and the chips, measured as drawn.</summary>
        void Layout(OpenZMainMenu m, string tag)
        {
            var panel = m.ProofHighScoresPanel; var view = m.ProofHighScoresView;
            if (!panel || !view) { Check(tag + " the plate and the view exist", false); return; }
            Rect pr = Px(panel), vr = Px(view);
            var texts = new List<OzText>(); m.ProofHighScoresTexts(texts);
            var boxes = new List<(string name, Rect r)>(); var wrong = new StringBuilder(); int measured = 0;
            foreach (var t in texts)
            {
                var tt = t.graphic as TMP_Text; Rect r;
                if (!tt || !Ink(tt, out r)) continue;
                measured++;
                string name = t.rectTransform.name + " \"" + Plain(t.text) + "\"";
                if (tt.fontSize < t.Size - 0.01f || tt.overflowMode == TextOverflowModes.Ellipsis) wrong.Append("\n    shrunk or cut: ").Append(name);
                if (!Inside(r, pr)) wrong.Append("\n    leaves the plate: ").Append(name);
                if (t.rectTransform.IsChildOf(view) && !Inside(r, vr, 1f)) wrong.Append("\n    leaves the view: ").Append(name);
                boxes.Add((name, r));
            }
            for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                    if (Meet(boxes[i].r, boxes[j].r)) wrong.Append("\n    meet: ").Append(boxes[i].name).Append(" and ").Append(boxes[j].name);
            float x0 = float.NaN; int chips = 0; float u = vr.width / (OpenZMainMenu.HsW - 2f * OpenZMainMenu.HsPad - 14f);   // screen pixels per view unit (the view is 954 u wide)
            for (int r = 0; r < m.ProofHighScoresRows(0); r++)
            {
                var chip = m.ProofHighScoresSideRt(0, r); var ct = m.ProofHighScoresSideText(0, r);
                if (!chip || ct == null) { wrong.Append("\n    row " + (r + 1) + " has no tag"); continue; }
                chips++;
                Rect c = Px(chip);
                if (!Inside(c, vr, 1f)) wrong.Append("\n    the tag of row " + (r + 1) + " leaves the view");
                if (Ink(ct.graphic, out Rect ir) && !Inside(ir, c, 0.5f)) wrong.Append("\n    the words of row " + (r + 1) + "'s tag leave its chip");
                if (float.IsNaN(x0)) x0 = c.xMin; else if (Mathf.Abs(c.xMin - x0) > 0.5f) wrong.Append("\n    the tag of row " + (r + 1) + " is out of the column");
                if (Mathf.Abs(c.width / u - OpenZMainMenu.HsSideW) > 0.6f) wrong.Append("\n    the tag of row " + (r + 1) + " is " + (c.width / u).ToString("0.0") + " u wide");
            }
            Check($"{tag} the popup's {measured} texts (the {chips} tags' words with them) inside the plate (the table's inside the view), at their full size, none meets another; "
                  + $"every tag inside the view, its words inside its chip, the chips in one straight column, {OpenZMainMenu.HsSideW:0} u wide; plate {pr.width / u:0} x {pr.height / u:0} u{wrong}",
                  measured >= 60 && chips == 10 && wrong.Length == 0);
        }
    }
}
#endif
