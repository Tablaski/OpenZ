// OpenZ (GPL-3.0)
// The loading bar's per-frame log.
// (View/OzLoadBarPlayCheck.cs, menu OpenZ/1.5/Checks/Loading Bar). Not armed (the default): no object is made, nothing changes.
// Armed, one object that lives across the scene switch records every frame from a loading screen's start (OpenZMainMenu.LoadBarRun: DEPLOY or
// UNIT EDITOR) until AfterSeconds after the level's first picture: the time, the frame's length, the bar's value and words, the phase (menu,
// let-in, level). It prints them as ONE log entry at the end (no log call per frame, so the frames it measures stay as they are) with:
//  BACK   the bar never goes back;
//  FULL   the bar never shows 100 % before the level's first picture;
//  HOLD   the picture the level's first frame stands on shows at most 80 %, with its step words;
//  STILL  no other stretch over 1.0 s without a bar change;
//  WARM   (the Play check, levels) the level's warm list ran on this screen and the bar went through its share (45-72 %) in steps;
//  SOUND  the done sound plays once, on the let-in frame, never on the frame of a step tick;
//  FREEZE (r8b, critic round; the owner's 15 Nov case) the bar never stands still more than FreezeMaxS (1.0 s) while the loading picture
//         shows, the level's first frame included. Step 1 cannot meet it (the first frame is one engine frame of 6-10 s): a start that
//         passes every bar rule but FREEZE is KNOWN-FAIL, never PASS;
//  MAXFRAME (kit loading_bar_real_r8, the A24 target, reported apart from PASS) the longest frame while the loading picture shows (the click
//         to the level's first picture, the long first frame included) at most MaxFrameTargetMs; and the long frame's split (OzStartSplit);
// and the long frame (the level's first frame: the engine's scene activation, every Awake and Start, the start work ahead, the first picture).
// Measures only: nothing drawn, heard or simulated changes. The Play check also saves the hold's picture and the level's first picture.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    [DefaultExecutionOrder(32000)]
    public sealed class OzLoadBarLog : MonoBehaviour
    {
        public const string Tag = "[OpenZ LoadBar]";
        public const string Arg = "-oz-loadbar-log";
        /// <summary>Records every loading screen from now on.</summary>
        public static bool Armed { get; private set; }
        /// <summary>Play check only: the folder the hold's picture and the level's first picture go to (hold_/first_&lt;CaptureName&gt;.png) and the
        /// rows (rows_&lt;CaptureName&gt;.csv); empty = the log only (the exe).</summary>
        public static string CaptureDir = "", CaptureName = "";
        /// <summary>Seconds recorded after the level's first picture.</summary>
        public static float AfterSeconds = 3f;
        /// <summary>The Play check (levels, with a cold warm list emulated): WARM must show the warm list running. Off (the exe): WARM only reports.</summary>
        public static bool RequireWarm;
        /// <summary>Loading screens recorded to the end, and the last one's verdict.</summary>
        public static int Runs { get; private set; }
        public static bool LastPass { get; private set; }
        public static string LastSummary { get; private set; } = "none yet";
        public static string LastLevel { get; private set; } = "";
        public static float LastHoldBar { get; private set; } = -1f;
        public static float LastLongFrameMs { get; private set; } = -1f;
        public static float LastLongestOtherStill { get; private set; } = -1f;
        /// <summary>WARM's least frames: with the warm list running, and with the bar inside the warm list's share.</summary>
        public const int WarmFramesMin = 5, BandFramesMin = 4;
        /// <summary>kit loading_bar_real_r8: the A24 target, the longest frame while the loading picture shows (ms); and the last start's result.</summary>
        public const float MaxFrameTargetMs = 500f;
        public static float LastMaxFrameMs { get; private set; } = -1f;
        public static bool LastTargetMet { get; private set; }
        /// <summary>r8b FREEZE: the longest the bar may stand still while the loading picture shows, the level's first frame included (s).</summary>
        public const float FreezeMaxS = 1.0f;
        /// <summary>r8b: the last start's bar rules alone (BACK FULL HOLD STILL WARM SOUND), its FREEZE result and stretch (s), and its verdict word
        /// (PASS = every rule incl. FREEZE; KNOWN-FAIL = only FREEZE failed, the open A24 case; FAIL = a bar rule failed or the start was cut).</summary>
        public static bool LastBarPass { get; private set; }
        public static bool LastFreezeOk { get; private set; }
        public static float LastFreezeS { get; private set; } = -1f;
        public static string LastWord { get; private set; } = "";
        static OzLoadBarLog host;
        static bool argSeen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Armed = false; CaptureDir = CaptureName = ""; AfterSeconds = 3f; RequireWarm = false; Runs = 0; LastPass = false; LastSummary = "none yet"; LastLevel = "";
            LastHoldBar = LastLongFrameMs = LastLongestOtherStill = -1f; host = null; argSeen = false; LastMaxFrameMs = -1f; LastTargetMet = false;
            LastBarPass = LastFreezeOk = false; LastFreezeS = -1f; LastWord = "";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
                if (string.Equals(a, Arg, System.StringComparison.OrdinalIgnoreCase)) argSeen = true;
            if (!argSeen) return;
            Arm();
            Debug.Log(Tag + " per-frame log armed (" + Arg + "): every loading screen from the click to " + AfterSeconds.ToString("0", CultureInfo.InvariantCulture) + " s after the level's first picture");
        }

        /// <summary>Records every loading screen from now on (one object kept across scene loads).</summary>
        public static void Arm()
        {
            Armed = true;
            if (host) return;
            var go = new GameObject("OzLoadBarLog");
            DontDestroyOnLoad(go);
            host = go.AddComponent<OzLoadBarLog>();
        }

        /// <summary>Stops recording (the command line's arming stays off too until the next start of the game).</summary>
        public static void Disarm()
        {
            Armed = false;
            if (host) Destroy(host.gameObject);
            host = null;
        }

        sealed class Row { public int f; public float t, ms, bar, warm; public string label, phase, snd = ""; }
        readonly List<Row> rows = new List<Row>(1024);
        int run = -1, levelRow = -1; float t0, levelAt = -1f; string level = "", levelScene = "", holdTip = ""; bool holdShot, honestRun;

        void OnEnable() { SceneManager.sceneLoaded += OnScene; }
        void OnDisable() { SceneManager.sceneLoaded -= OnScene; }
        void OnDestroy() { if (host == this) host = null; }

        void OnScene(Scene s, LoadSceneMode m)
        {
            if (run < 0 || m != LoadSceneMode.Single) return;
            if (levelRow >= 0) { Finish("cut short: another scene loaded (" + s.name + ")"); return; }
            if (!OpenZMainMenu.LoadBarLetIn) return;
            levelRow = rows.Count; levelAt = Time.realtimeSinceStartup; levelScene = s.name;                  // this frame's picture is the level's first
            if (!string.IsNullOrEmpty(CaptureDir)) Shot("first_");
        }

        void LateUpdate()
        {
            if (!Armed) return;
            int r = OpenZMainMenu.LoadBarRun;
            if (r != run && r > 0 && !OpenZMainMenu.LoadBarLetIn && levelRow < 0)
            {
                if (run >= 0) Finish("cut short: a new loading screen began");
                Begin(r);
            }
            if (run < 0) return;
            float now = Time.realtimeSinceStartup;
            bool lv = levelRow >= 0;
            int fc = Time.frameCount;
            string snd = (OpenZMainMenu.LoadBarTickFrame == fc ? "tick" : "") + (OpenZMainMenu.LoadBarDoneFrame == fc ? (OpenZMainMenu.LoadBarTickFrame == fc ? "+done" : "done") : "");
            rows.Add(new Row { f = fc, t = now - t0, ms = Time.unscaledDeltaTime * 1000f, bar = lv ? -1f : OpenZMainMenu.LoadBarShown, warm = lv ? -1f : OpenZMainMenu.LoadBarWarm,
                               label = lv ? "" : OpenZMainMenu.LoadBarLabel, phase = lv ? "level" : OpenZMainMenu.LoadBarLetIn ? "let-in" : "menu", snd = lv ? "" : snd });
            if (!lv && OpenZMainMenu.LoadBarLetIn && !holdShot) { holdShot = true; holdTip = OpenZMainMenu.LoadBarTip ?? ""; if (!string.IsNullOrEmpty(CaptureDir)) Shot("hold_"); }   // the picture the long frame stands on (r8b: and its words)
            if (lv && now - levelAt >= AfterSeconds && rows.Count - levelRow >= 2) Finish(null);
            else if (now - t0 > 240f) Finish("cut short: no level picture 240 s after the click");
        }

        void Begin(int r)
        {
            run = r; levelRow = -1; levelAt = -1f; level = OpenZMainMenu.LoadBarLevel; levelScene = ""; holdShot = false; holdTip = ""; honestRun = OpenZMainMenu.LoadBarHonest; rows.Clear();
            t0 = OpenZMainMenu.LoadBarStartAt > 0f ? OpenZMainMenu.LoadBarStartAt : Time.realtimeSinceStartup;
        }

        void Shot(string prefix)
        {
            try
            {
                Directory.CreateDirectory(CaptureDir);
                ScreenCapture.CaptureScreenshot(Path.Combine(CaptureDir, prefix + (string.IsNullOrEmpty(CaptureName) ? level : CaptureName) + ".png"));   // taken at the end of this frame
            }
            catch (System.Exception e) { Debug.LogWarning(Tag + " picture not saved: " + e.Message); }
        }

        static string F(float v, string fmt) => v.ToString(fmt, CultureInfo.InvariantCulture);

        void Finish(string cut)
        {
            var sb = new StringBuilder(rows.Count * 64 + 1024);
            int nMenu = levelRow >= 0 ? levelRow : rows.Count;
            bool back = false, full = false;
            float holdBar = nMenu > 0 ? rows[nMenu - 1].bar : -1f; string holdLabel = nMenu > 0 ? rows[nMenu - 1].label : "";
            for (int i = 0; i < nMenu; i++)
            {
                if (rows[i].bar >= 0.995f) full = true;
                if (i > 0 && rows[i].bar < rows[i - 1].bar - 0.0001f) back = true;
            }
            // the picture of the level: drawn at the end of its first frame; the next frame's row comes right after it
            float pictureAt = levelRow >= 0 ? (levelRow + 1 < rows.Count ? rows[levelRow + 1].t : rows[levelRow].t) : -1f;
            float longMs = levelRow >= 0 && levelRow + 1 < rows.Count ? rows[levelRow + 1].ms : -1f;
            float longestOther = 0f, finalStill = -1f; int segStart = 0;
            for (int i = 1; i < nMenu; i++)
            {
                if (Mathf.Abs(rows[i].bar - rows[segStart].bar) <= 0.0001f) continue;
                longestOther = Mathf.Max(longestOther, rows[i].t - rows[segStart].t);
                segStart = i;
            }
            if (nMenu > 0 && pictureAt >= 0f) finalStill = pictureAt - rows[segStart].t;                       // the hold, the let-in frames and the long frame
            else if (nMenu > 0) longestOther = Mathf.Max(longestOther, rows[nMenu - 1].t - rows[segStart].t);
            bool okBack = !back, okFull = !full, okHold = holdBar >= 0f && holdBar <= 0.805f && !string.IsNullOrEmpty(holdLabel)
                && (!honestRun || holdTip.IndexOf("A FEW SECONDS", System.StringComparison.Ordinal) >= 0), okStill = longestOther <= 1.0f;   // r8b: the hold's words expect the wait
            bool okFreeze = finalStill >= 0f && finalStill <= FreezeMaxS;                                         // r8b FREEZE: the owner's 15 Nov case
            // WARM: frames with the level's warm list running (0 < warm < 1) and frames with the bar inside the warm list's share
            float lo = OpenZMainMenu.LoadBarShareScene + 0.01f, hi = OpenZMainMenu.LoadBarShareScene + OpenZMainMenu.LoadBarShareWarm - 0.01f;
            int warmFrames = 0, bandFrames = 0;
            for (int i = 0; i < nMenu; i++)
            {
                if (rows[i].warm > 0.0005f && rows[i].warm < 0.999f) warmFrames++;
                if (rows[i].bar > lo && rows[i].bar < hi) bandFrames++;
            }
            bool okWarm = !RequireWarm || (warmFrames >= WarmFramesMin && bandFrames >= BandFramesMin);
            string warmTxt = !RequireWarm && warmFrames == 0 ? "n/a (the warm list was done at once: no cold shaders on this start)"
                           : $"{(okWarm ? "ok" : "FAIL")} ({warmFrames} frames with the warm list running, {bandFrames} frames with the bar in its share {F(lo * 100f, "0")}-{F(hi * 100f, "0")} %)";
            // SOUND: the done sound once, on the let-in frame (the first let-in row), not on a step tick's frame
            int letRow = -1, doneRow = -1, dones = 0; bool doneOnTick = false, anySound = false; float lastTick = -1f;
            for (int i = 0; i < nMenu; i++)
            {
                if (letRow < 0 && rows[i].phase == "let-in") letRow = i;
                if (rows[i].snd.Length > 0) anySound = true;
                if (rows[i].snd.Contains("done")) { dones++; if (doneRow < 0) doneRow = i; if (rows[i].snd.Contains("tick")) doneOnTick = true; }
                else if (rows[i].snd.Contains("tick") && doneRow < 0) lastTick = rows[i].t;
            }
            bool okSound = !anySound || (dones == 1 && doneRow >= 0 && doneRow == letRow && !doneOnTick);
            string soundTxt = !anySound ? "n/a (no loading sound played: sounds off)"
                            : $"{(okSound ? "ok" : "FAIL")} (done sound {dones}x at {(doneRow >= 0 ? F(rows[doneRow].t, "0.00") : "-")} s{(doneOnTick ? " ON A TICK" : "")}, let-in {(letRow >= 0 ? F(rows[letRow].t, "0.00") : "-")} s, last tick before it {(lastTick >= 0f ? F(lastTick, "0.00") + " s" : "-")})";
            bool barPass = cut == null && levelRow >= 0 && okBack && okFull && okHold && okStill && okWarm && okSound;
            bool pass = barPass && okFreeze;
            string word = pass ? "PASS" : barPass ? "KNOWN-FAIL" : "FAIL";                                     // r8b: KNOWN-FAIL = only FREEZE (A24 open), never under PASS
            // kit loading_bar_real_r8: MAXFRAME, every frame while the loading picture shows (row i's ms = the frame before it; the long first
            // frame is the row after the level's first row); the A24 target, reported apart from the bar's PASS
            float maxMs = 0f, maxAt = -1f; int lastShown = levelRow >= 0 ? Mathf.Min(levelRow + 1, rows.Count - 1) : rows.Count - 1;
            for (int i = 1; i <= lastShown; i++) if (rows[i].ms > maxMs) { maxMs = rows[i].ms; maxAt = rows[i].t; }
            bool targetMet = cut == null && levelRow >= 0 && maxMs <= MaxFrameTargetMs;
            string split = OzStartSplit.LastLevel == level && OzStartSplit.Runs > 0 ? OzStartSplit.LastLine : "none (no OzStartSplit line for this start)";
            string verdict = $"{word} BACK {(okBack ? "ok" : "FAIL (the bar went back)")}, FULL {(okFull ? "ok" : "FAIL (100 % before the level's first picture)")}, "
                           + $"HOLD {(okHold ? "ok" : "FAIL")} ({Mathf.RoundToInt(holdBar * 100f)} % {holdLabel}, tip '{holdTip}'), STILL {(okStill ? "ok" : "FAIL")} (longest other {F(longestOther, "0.00")} s), "
                           + $"FREEZE {(okFreeze ? "ok" : "FAIL")} (the bar stood still {F(finalStill, "0.00")} s while the loading picture showed, the level's first frame included; max {F(FreezeMaxS, "0.0")} s), "
                           + $"WARM {warmTxt}, SOUND {soundTxt}; bar rules {(barPass ? "PASS" : "FAIL")}";
            string line = $"{level} ({levelScene}): {verdict}; the long first frame {F(longMs, "0")} ms, the bar stood still over it {F(finalStill, "0.00")} s, "
                        + $"level picture {F(pictureAt, "0.00")} s after the click; {nMenu} loading frames, {rows.Count - nMenu} level frames recorded{(cut != null ? "; " + cut : "")}"
                        + $"; MAXFRAME {(targetMet ? "met" : "NOT MET")} (A24 target: the longest frame while the loading picture shows {F(maxMs, "0")} ms at {F(maxAt, "0.00")} s, target <= {F(MaxFrameTargetMs, "0")} ms)"
                        + $"; split: {split}";
            sb.Append(Tag).Append(' ').Append(line).Append('\n');
            sb.Append("frame,t_s,prev_frame_ms,bar_pct,label,phase,warm_pct,sound\n");
            foreach (var w in rows)
                sb.Append(w.f).Append(',').Append(F(w.t, "0.000")).Append(',').Append(F(w.ms, "0.0")).Append(',').Append(w.bar < 0f ? "" : F(w.bar * 100f, "0.0"))
                  .Append(',').Append(w.label).Append(',').Append(w.phase).Append(',').Append(w.warm < 0f ? "" : F(w.warm * 100f, "0.0")).Append(',').Append(w.snd).Append('\n');
            Debug.Log(sb.ToString());
            if (!string.IsNullOrEmpty(CaptureDir))
            {
                try
                {
                    Directory.CreateDirectory(CaptureDir);
                    File.WriteAllText(Path.Combine(CaptureDir, "rows_" + (string.IsNullOrEmpty(CaptureName) ? level : CaptureName) + ".csv"), sb.ToString());
                }
                catch (System.Exception e) { Debug.LogWarning(Tag + " rows not saved: " + e.Message); }
            }
            LastMaxFrameMs = maxMs; LastTargetMet = targetMet; LastBarPass = barPass; LastFreezeOk = okFreeze; LastFreezeS = finalStill; LastWord = word;
            LastPass = pass; LastSummary = line; LastLevel = level; LastHoldBar = holdBar; LastLongFrameMs = longMs; LastLongestOtherStill = longestOther;
            Runs++;
            run = -1; levelRow = -1; rows.Clear();
        }
    }
}
