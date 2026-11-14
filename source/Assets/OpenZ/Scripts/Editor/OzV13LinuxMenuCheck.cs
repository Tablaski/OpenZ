using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.3 item 31 (GitHub issue 2: on Linux "start menu slows to a crawl"; every flyby failed there): the counted check of the menu's
    /// flyby failure path and of the Linux VP8 copies. Menu: OpenZ/Checks/V1.3/Linux menu flybys (Play, main menu). Nothing is saved:
    /// the two check switches are put back and the menu is loaded once more at the end.
    ///  L1  every flyby has its Linux copy &lt;name&gt;_vp8: 1920x1080, 30 fps, the same length within 1/30 s, the same number of sound tracks.
    ///  L3  EVERY FLYBY FORCED TO FAIL (each player points at a missing file, so the engine's real error event fires): the menu reloaded,
    ///      then 20 s on the main view, which holds at least one still-to-still cut (a still holds 18 s). Pass needs all of:
    ///       - Prepare calls = one per level with flybys, all in the first 2 s; NONE in the 20 s (a failed player is never prepared again);
    ///       - every level on the still (levels on the still = levels with flybys);
    ///       - exactly one "[OpenZ Menu] preview video" line per level, each with the fixed reason, none with the engine's text;
    ///       - the menu fps over the 20 s at least 90 % of L2's (the still path costs no more than the video path).
    ///      at the start and at cuts (at most 1 + one per cut per level), the flyby shows its VP8 copy, video pictures delivered at least
    ///      25.5 a second of 30 (item 23's own line), the menu fps counted.
    /// What this cannot show: the Linux player's own decode speed (that needs a Linux PC: the player's [OpenZ MenuPerf] lines).
    /// </summary>
    public static class OzV13LinuxMenuCheck
    {
        const string Tag = "[OpenZ Linux menu check]";
        const float Seconds = 20f;
        public static string Report = "not run";
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static int ok, bad, waitFrame; static float waitUntil;
        static readonly StringBuilder fails = new StringBuilder(), lines = new StringBuilder();
        static readonly List<string> logs = new List<string>();
        static string dir, scene0;
        static bool prefsKept, hadMode, hadAuto, hadGpu; static int mode0, auto0; static string gpu0;   // item 23's AUTO pick may measure at a reload: put back as it was

        [MenuItem("OpenZ/Checks/V1.3/Linux menu flybys (Play, main menu)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning(Tag + " enter Play on the MainMenu scene first"); return; }
            if (stack.Count > 0) { Debug.LogWarning(Tag + " a run is still going"); return; }
            if (!Object.FindFirstObjectByType<OpenZMainMenu>() || !OzMenuPerf.I) { Debug.LogWarning(Tag + " start it on the main menu (the MainMenu scene in Play)"); return; }
            ok = bad = 0; fails.Clear(); lines.Clear(); Report = "running";
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/v1_3_20261030/item31_linux_menu"));
            Directory.CreateDirectory(dir);
            scene0 = SceneManager.GetActiveScene().name;
            hadMode = PlayerPrefs.HasKey(OzMenuVideo.Pref); mode0 = PlayerPrefs.GetInt(OzMenuVideo.Pref, 0);
            hadAuto = PlayerPrefs.HasKey(OzMenuVideo.PrefAuto); auto0 = PlayerPrefs.GetInt(OzMenuVideo.PrefAuto, 0);
            hadGpu = PlayerPrefs.HasKey(OzMenuVideo.PrefAutoGpu); gpu0 = PlayerPrefs.GetString(OzMenuVideo.PrefAutoGpu, ""); prefsKept = true;
            Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
            stack.Push(All());
            waitFrame = 0; waitUntil = 0f; EditorApplication.update += Step;
        }

        static void OnLog(string msg, string trace, LogType type) { if (msg != null && msg.StartsWith("[OpenZ Menu] preview video ")) logs.Add(msg); }

        static void Step()
        {
            if (stack.Count == 0 || !EditorApplication.isPlaying) { stack.Clear(); PutBack(); EditorApplication.update -= Step; return; }
            if (Time.frameCount < waitFrame || Time.realtimeSinceStartup < waitUntil) return;
            var top = stack.Peek(); bool more;
            try { more = top.MoveNext(); }
            catch (System.Exception ex) { Debug.LogException(ex); stack.Clear(); PutBack(); Report = Tag + " FAIL: exception " + ex.Message; Debug.LogError(Report); EditorApplication.update -= Step; return; }
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
        static void Note(string what) { string l = $"{Tag} note {what}"; lines.AppendLine(l); Debug.Log(l); }

        static void PutBack()
        {
            OzMenuVideo.ProofForceFail = false; OzMenuVideo.ProofLinuxSet = false;
            Application.logMessageReceived -= OnLog;
            if (prefsKept)
            {
                prefsKept = false;
                if (hadMode) PlayerPrefs.SetInt(OzMenuVideo.Pref, mode0); else PlayerPrefs.DeleteKey(OzMenuVideo.Pref);
                if (hadAuto) PlayerPrefs.SetInt(OzMenuVideo.PrefAuto, auto0); else PlayerPrefs.DeleteKey(OzMenuVideo.PrefAuto);
                if (hadGpu) PlayerPrefs.SetString(OzMenuVideo.PrefAutoGpu, gpu0); else PlayerPrefs.DeleteKey(OzMenuVideo.PrefAutoGpu);
                PlayerPrefs.Save();
            }
        }

        static int LevelsWithFlybys()
        {
            int n = 0;
            foreach (var d in OzLevelDef.All) if (Resources.Load<VideoClip>("OpenZ/Menu/" + d.Id + "_preview") != null) n++;
            return n;
        }

        /// <summary>The menu loaded again with the switches as they are now, then the main view.</summary>
        static IEnumerator Reload(string why)
        {
            OzLevelPreview.ProofResetCounts(); logs.Clear();
            SceneManager.LoadScene(scene0);
            float t0 = Time.realtimeSinceStartup;
            yield return 5;
            while ((!Object.FindFirstObjectByType<OpenZMainMenu>() || !OzMenuPerf.I) && Time.realtimeSinceStartup - t0 < 20f) yield return 5;
            var m = Object.FindFirstObjectByType<OpenZMainMenu>();
            if (m && m.ProofView != "Main") m.ProofMain();
            Note($"menu loaded again ({why}) in {Time.realtimeSinceStartup - t0:0.0} s");
        }

        /// <summary>20 s on the main view: fps from the frame count, and the counts at the start and the end.</summary>
        struct Row { public float Fps, Delivered; public int PrepStart, PrepEnd, ErrEnd, FailedEnd, Cuts; }
        static Row row;
        static IEnumerator Count(bool video)
        {
            yield return 2f;                                                                                  // the start: the three players prepare (or fail)
            row = new Row { PrepStart = OzLevelPreview.PrepareCalls };
            int f0 = Time.frameCount, cuts0 = OpenZMainMenu.HardCuts; float t0 = Time.realtimeSinceStartup, deliv = 0f; int nd = 0;
            OzMenuPerf.I.ProofReset();
            while (Time.realtimeSinceStartup - t0 < Seconds)
            {
                yield return 1f;
                if (video) { deliv += OzMenuPerf.I.ProofDeliveredPerSec; nd++; }
            }
            float dt = Time.realtimeSinceStartup - t0;
            row.Fps = (Time.frameCount - f0) / Mathf.Max(0.001f, dt);
            row.PrepEnd = OzLevelPreview.PrepareCalls; row.ErrEnd = OzLevelPreview.ErrorEvents; row.FailedEnd = OzLevelPreview.FailedLevels;
            row.Cuts = OpenZMainMenu.HardCuts - cuts0; row.Delivered = nd > 0 ? deliv / nd : 0f;
        }

        static string RowText(Row r) => $"{r.Fps:0.0} menu fps over {Seconds:0} s; Prepare calls {r.PrepStart} in the first 2 s, {r.PrepEnd - r.PrepStart} in the {Seconds:0} s; video error events {r.ErrEnd}; levels on the still {r.FailedEnd}; cuts {r.Cuts}";

        static IEnumerator All()
        {
            int levels = LevelsWithFlybys();
            // L1 the copies
            var s1 = new StringBuilder(); bool all1 = true; int n1 = 0;
            foreach (var d in OzLevelDef.All)
                for (int k = 1; k <= 9; k++)
                {
                    string n = k == 1 ? d.Id + "_preview" : d.Id + "_preview_" + k;
                    var full = Resources.Load<VideoClip>("OpenZ/Menu/" + n); if (full == null) break;
                    var lx = Resources.Load<VideoClip>("OpenZ/Menu/" + n + OzMenuVideo.LinuxSuffix); n1++;
                    bool good = lx != null && lx.width == 1920 && lx.height == 1080 && System.Math.Abs(lx.frameRate - 30.0) < 0.01
                                && System.Math.Abs(lx.length - full.length) <= 1.0 / 30.0 + 1e-3 && lx.audioTrackCount == full.audioTrackCount;
                    if (!good) all1 = false;
                    s1.Append(n).Append(lx == null ? " MISSING" : $" {lx.width}x{lx.height} {lx.frameRate:0.##} fps {lx.length:0.000} s (full {full.length:0.000} s) sound {lx.audioTrackCount}/{full.audioTrackCount}").Append(good ? "; " : " BAD; ");
                }
            Check(all1 && n1 > 0, $"L1 every flyby has its Linux VP8 copy ({n1}): {s1}");

            OzMenuVideo.ProofForceFail = false; OzMenuVideo.ProofLinuxSet = false;
            yield return Reload("this PC as it is");
            yield return Count(true);
            var r2 = row; Note("L2 this PC as it is: " + RowText(r2));

            // L3 every flyby forced to fail
            OzMenuVideo.ProofForceFail = true;
            yield return Reload("every flyby forced to fail");
            yield return Count(false);
            var r3 = row;
            int lines3 = logs.Count, fixed3 = 0, engine3 = 0;
            foreach (var l in logs) { if (l.Contains("failed on purpose for a check") && l.Contains("not prepared again")) fixed3++; if (l.Contains("CAMERA SHAKE") || l.Contains("|")) engine3++; }
            Check(r3.PrepStart == levels && r3.PrepEnd == r3.PrepStart && r3.FailedEnd == levels && r3.ErrEnd >= levels,
                  $"L3 a failed flyby is never prepared again: {RowText(r3)} (wanted: {levels} at the start, 0 after, {levels} levels on the still)");
            Check(lines3 == levels && fixed3 == levels && engine3 == 0,
                  $"L3 one clear line per level: {lines3} \"preview video\" lines (wanted {levels}), {fixed3} with the fixed reason, {engine3} with engine text");
            Check(r3.Cuts >= 1, $"L3 the still rotation ran meanwhile: {r3.Cuts} cut(s) in {Seconds:0} s (a still holds 18 s), no Prepare at a cut");
            Check(r2.Fps <= 0f || r3.Fps >= 0.9f * r2.Fps, $"L3 the still path costs no more than the video: {r3.Fps:0.0} fps vs {r2.Fps:0.0} fps with this PC's video (at least 90 %)");
            OzMenuVideo.ProofForceFail = false;

            OzMenuVideo.ProofLinuxSet = true;
            yield return Reload("the Linux VP8 copies");
            yield return Count(true);
            var r4 = row; bool onVp8 = false, anyLive = false;
            foreach (var p in OzLevelPreview.Live) if (p.ShowingVideo) { anyLive = true; if (p.Player.clip && p.Player.clip.name.EndsWith(OzMenuVideo.LinuxSuffix)) onVp8 = true; }
            Check(r4.ErrEnd == 0 && r4.FailedEnd == 0 && anyLive && onVp8 && r4.PrepEnd - r4.PrepStart <= r4.Cuts * 2 + levels && r4.Delivered >= 25.5f,
                  $"L4 the Linux copies play: {RowText(r4)}; showing a VP8 copy {onVp8}; video pictures delivered {r4.Delivered:0.0} a second (at least 25.5 of 30)");
            OzMenuVideo.ProofLinuxSet = false;

            // back as it was
            yield return Reload("put back");
            Note($"table | this PC as it is: {r2.Fps:0.0} fps | every flyby failing: {r3.Fps:0.0} fps, {r3.PrepEnd - r3.PrepStart} prepares in {Seconds:0} s | Linux VP8 copies: {r4.Fps:0.0} fps, {r4.Delivered:0.0} pictures a second");
            Report = $"{Tag} {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad}" + (bad > 0 ? fails.ToString() : "");
            lines.AppendLine(Report); Debug.Log(Report);
            try { File.WriteAllText(Path.Combine(dir, "checks.txt"), lines.ToString()); } catch (System.Exception e) { Debug.LogWarning(Tag + " could not write checks.txt: " + e.Message); }
            PutBack();
        }
    }
}
