using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The counted check on a clean profile. Menu:
    /// OpenZ/Checks/V1.3/Faction pick: Zod's line only the first time (Play, main menu). Two parts, the same menu item both times:
    ///  part 1 (a clean profile for the line; the player's own marks are kept and put back at the end of part 2):
    ///   F1  the FIRST pick (GENERAL ZOD): the stamp, Zod's "Wise choice!" (zod_pick_red) plays, the mark is saved, the screen holds for
    ///       the line (its length + 0.25 s, at least 0.9 s) and then shows the level selection.
    ///   F2  the second pick (BACK, then GENERAL EVILZOD): the stamp and the plate's words, NO line, and the level selection on the next
    ///       frames (hold about 0 s). With OzFaction.OncePerSide on, blue's first pick has its own line instead (and that is checked).
    ///   F3  a third pick (BACK, GENERAL ZOD again): no line, no hold.
    ///  part 2 (stop Play, start Play on the MainMenu scene again = a game restart, run the menu again):
    ///   F4  the mark is still saved; a pick: no line, the level selection on the next frames.
    /// Then the marks, the side and 'Zod exact' are put back. One line "[OpenZ faction line once] PASS n/n ..." per part, the lines in
    /// </summary>
    public static class OzV13FactionLineOnceCheck
    {
        const string Tag = "[OpenZ faction line once]";
        const string PartKey = "oz_v13_i06_part", KeepKey = "oz_v13_i06_keep", BoxKey = "oz_v13_i06_box", ExactKey = "oz_v13_i06_exact";
        public static string Report = "not run";
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static int ok, bad; static float waitUntil; static int waitFrame;
        static readonly StringBuilder fails = new StringBuilder(), lines = new StringBuilder();
        static string dir;

        [MenuItem("OpenZ/Checks/V1.3/Faction pick: Zod's line only the first time (Play, main menu)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning(Tag + " enter Play on the MainMenu scene first"); return; }
            if (stack.Count > 0) { Debug.LogWarning(Tag + " a run is still going"); return; }
            var m = Object.FindFirstObjectByType<OpenZMainMenu>();
            if (!m) { Debug.LogWarning(Tag + " start it on the main menu (the MainMenu scene in Play)"); return; }
            ok = bad = 0; fails.Clear(); lines.Clear(); Report = "running";
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/v1_3_20261030/item06_faction_line_once"));
            Directory.CreateDirectory(dir);
            stack.Push(SessionState.GetInt(PartKey, 1) == 2 ? Part2(m) : Part1(m));
            waitFrame = 0; waitUntil = 0f; EditorApplication.update += Step;
        }

        [MenuItem("OpenZ/Checks/V1.3/Faction pick: put the player's marks back (after a broken run)")]
        public static void PutBackMenu() { PutBack(); SessionState.EraseInt(PartKey); Debug.Log(Tag + " the marks, the side and 'Zod exact' put back; the next run starts with part 1"); }

        static void Step()
        {
            if (stack.Count == 0 || !EditorApplication.isPlaying) { stack.Clear(); EditorApplication.update -= Step; return; }
            if (Time.frameCount < waitFrame || Time.realtimeSinceStartup < waitUntil) return;
            var top = stack.Peek(); bool more;
            try { more = top.MoveNext(); }
            catch (System.Exception ex) { Debug.LogException(ex); stack.Clear(); Report = Tag + " FAIL: exception " + ex.Message + " (menu '...put the player's marks back' restores them)"; Debug.LogError(Report); EditorApplication.update -= Step; return; }
            if (!more) { stack.Pop(); if (stack.Count == 0) EditorApplication.update -= Step; return; }
            if (top.Current is IEnumerator child) stack.Push(child);
            else if (top.Current is float s) waitUntil = Time.realtimeSinceStartup + s;
            else waitFrame = Time.frameCount + (top.Current is int n ? n : 1);
        }

        static void Check(bool pass, string what)
        {
            if (pass) ok++; else { bad++; fails.Append(" | ").Append(what); }
            string l = $"{Tag} {(pass ? "ok  " : "FAIL")} {what}"; lines.AppendLine(l); Debug.Log(l);
        }

        static void Keep()
        {
            SessionState.SetString(KeepKey, string.Join(",", OzFaction.ProofHeardKeep()));
            SessionState.SetInt(BoxKey, OzPlayBlue.Box ? 1 : 0);
            SessionState.SetInt(ExactKey, PlayerPrefs.HasKey("oz_zod_exact") ? PlayerPrefs.GetInt("oz_zod_exact", 0) : -1);
        }

        static void PutBack()
        {
            string k = SessionState.GetString(KeepKey, "");
            if (k.Length > 0) OzFaction.ProofHeardPutBack(k.Split(',').Select(s => int.TryParse(s, out int v) ? v : -1).ToArray());
            int box = SessionState.GetInt(BoxKey, -1); if (box >= 0) OzPlayBlue.Box = box == 1;
            int ex = SessionState.GetInt(ExactKey, -2);
            if (ex == -1) PlayerPrefs.DeleteKey("oz_zod_exact"); else if (ex >= 0) PlayerPrefs.SetInt("oz_zod_exact", ex);
            PlayerPrefs.Save();
            SessionState.EraseString(KeepKey); SessionState.EraseInt(BoxKey); SessionState.EraseInt(ExactKey);
        }

        static string Plain(string s) => Regex.Replace(s ?? "", "<[^>]+>", "");
        static int Stamps(float l0) => OzSoundLog.Since(l0).Count(e => e.key == "ui_stamp" && e.outcome != null && e.outcome.StartsWith("PLAY"));

        static IEnumerator Until(System.Func<bool> c, float max) { float t = Time.realtimeSinceStartup + max; while (Time.realtimeSinceStartup < t && !c()) yield return 1; }

        static IEnumerator OpenFaction(OpenZMainMenu m, bool fromSelect)
        {
            if (fromSelect) m.ProofBackFromSelect(); else { if (m.ProofView != "Main") { m.ProofMain(); yield return 0.8f; } m.ProofFaction(); }
            yield return Until(() => m.ProofFactionShown, 3f);
            yield return Until(() => m.ProofRig && m.ProofRig.Ready, 12f);
            yield return 0.6f;
        }

        /// <summary>One pick, measured: whether Zod's line started, the hold the screen set, the real time and the frames until the level
        /// selection showed, the stamps and the plate's words.</summary>
        static bool pLine; static float pLen, pHold, pHeld; static int pFrames, pStamps; static string pWords = "";
        static IEnumerator Pick(OpenZMainMenu m, int side)
        {
            int voices = OpenZMainMenu.FactionVoices, holds = OpenZMainMenu.FactionPickHolds; float l0 = Time.unscaledTime;
            float t0 = Time.realtimeSinceStartup; int f0 = Time.frameCount;
            m.ProofFactionClick(side);
            var src = m.ProofFactionVoice;
            pLine = OpenZMainMenu.FactionVoices == voices + 1 && src && src.isPlaying && src.clip && src.clip.name == (side == OzFaction.Blue ? "zod_pick_blue" : "zod_pick_red");
            pLen = pLine ? src.clip.length : 0f;
            pHold = OpenZMainMenu.FactionPickHolds == holds + 1 ? OpenZMainMenu.FactionLastHold : -1f;
            pWords = Plain(m.ProofTipText(side));
            while (m.ProofView != "Select" && Time.realtimeSinceStartup - t0 < pLen + 4f) { yield return 1; if (pWords.Length == 0 || !pWords.StartsWith("ZOD:")) pWords = Plain(m.ProofTipText(side)); }
            pHeld = Time.realtimeSinceStartup - t0; pFrames = Time.frameCount - f0;
            yield return 0.4f;
            pStamps = Stamps(l0);
        }

        static string Said(int side) => "ZOD: " + OzFaction.Joke[side];

        static IEnumerator Part1(OpenZMainMenu m)
        {
            Keep();
            if (PlayerPrefs.GetInt("oz_zod_exact", 0) == 1) { PlayerPrefs.SetInt("oz_zod_exact", 0); PlayerPrefs.Save(); }   // the EvilZod poster is locked in 'Zod exact'
            OzFaction.ProofForgetLine();
            Check(!OzFaction.LineHeard(OzFaction.Red) && !OzFaction.LineHeard(OzFaction.Blue), $"F0 a clean profile for the line: no mark saved ({OzFaction.HeardPref}), once per side {OzFaction.OncePerSide}");
            // F1 the first pick
            yield return OpenFaction(m, false);
            yield return Pick(m, OzFaction.Red);
            float want = OzFaction.PickHold(true, pLen);
            Check(pLine && OzFaction.LineHeard(OzFaction.Red) && pStamps >= 1 && Mathf.Abs(pHold - want) < 0.01f && pHeld >= pLen && pHeld < pLen + 1.5f && m.ProofView == "Select",
                  $"F1 the FIRST pick (GENERAL ZOD): Zod's line {pLine} ({pLen:0.00} s), the stamp x{pStamps}, the mark saved {OzFaction.LineHeard(OzFaction.Red)}, the hold {pHold:0.00} s (wanted {want:0.00}), the level selection after {pHeld:0.00} s / {pFrames} frames (view {m.ProofView})");
            // F2 the second pick: blue
            yield return OpenFaction(m, true);
            yield return Pick(m, OzFaction.Blue);
            if (OzFaction.OncePerSide)
                Check(pLine && pHold >= OzFaction.PickHoldMin && m.ProofView == "Select", $"F2 (once per side) blue's FIRST pick has its own line: {pLine}, the hold {pHold:0.00} s");
            else
                Check(!pLine && pStamps >= 1 && pHold == 0f && pHeld < 0.3f && pFrames <= 4 && pWords == Said(OzFaction.Blue) && m.ProofView == "Select",
                      $"F2 the second pick (GENERAL EVILZOD): no line {!pLine}, the stamp x{pStamps}, the plate \"{pWords}\", the hold {pHold:0.00} s, the level selection after {pHeld:0.000} s / {pFrames} frames (view {m.ProofView})");
            // F3 a third pick: red again
            yield return OpenFaction(m, true);
            yield return Pick(m, OzFaction.Red);
            Check(!pLine && pStamps >= 1 && pHold == 0f && pHeld < 0.3f && pFrames <= 4 && pWords == Said(OzFaction.Red) && m.ProofView == "Select",
                  $"F3 a third pick (GENERAL ZOD): no line {!pLine}, the stamp x{pStamps}, the plate \"{pWords}\", the hold {pHold:0.00} s, the level selection after {pHeld:0.000} s / {pFrames} frames");
            SessionState.SetInt(PartKey, 2);
            Finish("part 1", "now stop Play, start Play on the MainMenu scene again (a game restart) and run the same menu item for part 2");
        }

        static IEnumerator Part2(OpenZMainMenu m)
        {
            bool saved = OzFaction.LineHeard(OzFaction.Red) && PlayerPrefs.GetInt(OzFaction.HeardPref + (OzFaction.OncePerSide ? "_red" : ""), 0) == 1;
            Check(saved && OzFaction.Picks == 0, $"F4.1 after the restart: the mark is still saved {saved}, picks in this session {OzFaction.Picks} (0: a new session)");
            yield return OpenFaction(m, false);
            yield return Pick(m, OzFaction.Red);
            Check(!pLine && pStamps >= 1 && pHold == 0f && pHeld < 0.3f && pFrames <= 4 && m.ProofView == "Select",
                  $"F4.2 a pick after the restart: no line {!pLine}, the stamp x{pStamps}, the hold {pHold:0.00} s, the level selection after {pHeld:0.000} s / {pFrames} frames");
            m.ProofBackFromSelect(); yield return 0.6f; m.ProofMain(); yield return 0.6f;
            PutBack(); SessionState.EraseInt(PartKey);
            Finish("part 2", "the player's marks, side and 'Zod exact' put back");
        }

        static void Finish(string part, string next)
        {
            bool pass = bad == 0 && ok > 0;
            Report = $"{Tag} {part}: {(pass ? "PASS" : "FAIL")} {ok}/{ok + bad} checks; {next}{(bad == 0 ? "" : "; FAILED:" + fails)}";
            lines.AppendLine(Report);
            if (pass) Debug.Log(Report); else Debug.LogError(Report);
            try { File.AppendAllText(Path.Combine(dir, "checks.txt"), lines.ToString()); } catch (System.Exception e) { Debug.LogWarning(Tag + " could not write checks.txt: " + e.Message); }
        }
    }
}
