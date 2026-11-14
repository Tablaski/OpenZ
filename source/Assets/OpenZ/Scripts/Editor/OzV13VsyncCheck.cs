using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// the counted check of the FRAME RATE LIMIT row's new UNLIMITED (VSYNC) choice. Menu: OpenZ/Checks/V1.3/Frame rate UNLIMITED (VSYNC)
    /// (Play mode), in a running level (the rules part needs the match) or in the main menu (the row part only).
    ///  V1  no saved value: 60, vsync off (today's default, new and old players).
    ///  V2  the row's steps: 60 -> UNLIMITED (VSYNC) -> 30 -> 40 -> 60 with the right arrow, 60 -> 40 -> 30 -> UNLIMITED (VSYNC) -> 60 with the
    ///      left arrow; at each step the saved value, the game's cap, the vsync count and the frame-rate target.
    ///  V3  the rules do not change with the frame rate (level only): 8 s at 60, then 8 s on UNLIMITED (VSYNC): the game clock (game
    ///      seconds per real second at the game speed) and the rule ticks run per real second (fixed 10 ms ticks, ZGame.Advance:
    ///      100 x the game speed) are the same at both; the ticks per drawn frame are reported (fewer per frame at a higher rate,
    ///      the same ticks in all). Unit speeds, fire rates and timers live in those ticks only (Sim/ has no frame time).
    ///  V4  the heat guard on UNLIMITED (VSYNC) (played: 92 C, then 80 C): hot = vsync off and 30 fps; the row moved while hot keeps the
    ///      30; cool again = vsync on and no target; back to 60 = vsync off and 60.
    /// The player's saved limit, the game's cap, the target, the vsync count and the guard are put back.
    /// In the editor the Game view may not follow vsync: the frame rates on UNLIMITED (VSYNC) are reported, not judged; the player
    /// build on the high-refresh screen gives the real numbers (SOLUTION.md section 7, part B).
    /// </summary>
    public static class OzV13VsyncCheck
    {
        const string Tag = "[OpenZ vsync choice]";
        public static string Report = "not run";
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static int waitFrame, ok, bad;
        static float waitUntil;
        static readonly StringBuilder fails = new StringBuilder(), lines = new StringBuilder();
        static string dir;
        static bool kept; static int cap0, capFps0, target0, vsync0;
        static HeatGuardRuntime guard;

        [MenuItem("OpenZ/Checks/V1.3/Frame rate UNLIMITED (VSYNC) (Play mode)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning(Tag + " enter Play mode first"); return; }
            if (stack.Count > 0) { Debug.LogWarning(Tag + " a run is still going"); return; }
            ok = bad = 0; fails.Clear(); lines.Clear(); Report = "running";
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/v1_3_20261030/item05_vsync"));
            Directory.CreateDirectory(dir);
            var hud = OpenZHud.I;
            bool level = hud && hud.Canvas && hud.W != null && !hud.W.GameOver;
            if (!level && !Object.FindFirstObjectByType<OpenZMainMenu>()) { Report = Tag + " FAIL: a level or the main menu is needed"; Debug.LogError(Report); return; }
            stack.Push(All(level ? hud : null));
            waitFrame = 0; waitUntil = 0f; EditorApplication.update += Step;
        }

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

        static void Keep()
        {
            cap0 = PlayerPrefs.HasKey(OzFpsCap.CapPref) ? PlayerPrefs.GetInt(OzFpsCap.CapPref, 60) : -1;
            capFps0 = GameBoot.CapFps; target0 = Application.targetFrameRate; vsync0 = QualitySettings.vSyncCount; kept = true;
        }

        static void PutBack()
        {
            if (guard) { SetTemp(-1); Object.Destroy(guard.gameObject); guard = null; HeatGuardRuntime.HeldFps = 0; }
            if (!kept) return;
            kept = false;
            if (cap0 < 0) PlayerPrefs.DeleteKey(OzFpsCap.CapPref); else PlayerPrefs.SetInt(OzFpsCap.CapPref, cap0);
            PlayerPrefs.Save();
            GameBoot.CapFps = capFps0; Application.targetFrameRate = target0; QualitySettings.vSyncCount = vsync0; HeatGuardRuntime.HeldFps = 0;
        }

        static readonly FieldInfo TempField = typeof(HeatGuardRuntime).GetField("temp", BindingFlags.NonPublic | BindingFlags.Static);
        static void SetTemp(int c) { if (TempField != null) TempField.SetValue(null, c); }

        static string State() => $"saved {(PlayerPrefs.HasKey(OzFpsCap.CapPref) ? PlayerPrefs.GetInt(OzFpsCap.CapPref, 60).ToString() : "none")}, row \"{OzFpsCap.Name}\", the game's cap {GameBoot.CapFps}, vsync {QualitySettings.vSyncCount}, target {Application.targetFrameRate}";
        /// <summary>The state a choice must give (no heat guard holding).</summary>
        static bool Is(string name, int capFps, int vsync, int target)
            => OzFpsCap.Name == name && GameBoot.CapFps == capFps && QualitySettings.vSyncCount == vsync && Application.targetFrameRate == target;

        static float mFps, mClock, mTicks, mPerFrame;
        /// <summary>Over `seconds` of real time: frames a second, game seconds per real second (at the game speed), rule ticks run per
        /// real second (at the game speed) and per drawn frame.</summary>
        static IEnumerator Measure(OpenZHud hud, float seconds)
        {
            int f0 = Time.frameCount; float r0 = Time.realtimeSinceStartup; double w0 = hud != null ? hud.W.Time : 0.0; long t0 = hud != null ? hud.W.TickIndex : 0;
            yield return seconds;
            float dr = Time.realtimeSinceStartup - r0; double dw = hud != null ? hud.W.Time - w0 : 0.0; long dt = hud != null ? hud.W.TickIndex - t0 : 0; int df = Time.frameCount - f0;
            float speed = hud != null ? Mathf.Max(0.01f, hud.game.gameSpeed) : 1f;
            mFps = df / dr;
            mClock = hud != null ? (float)(dw / dr / speed) : 0f;
            mTicks = dt / dr / speed;
            mPerFrame = df > 0 ? dt / (float)df : 0f;
        }

        static IEnumerator All(OpenZHud hud)
        {
            Keep();
            // ---- V1 the default
            PlayerPrefs.DeleteKey(OzFpsCap.CapPref);
            GameBoot.CapFps = OzFpsCap.Saved; OzFpsCap.ApplyLimit(0);                                       // what GameBoot.Init does at the start
            Check(OzFpsCap.Saved == 60 && !OzFpsCap.Vsync && Is("60", 60, 0, 60), "V1 no saved value (a new or a 1.2 player): " + State() + " (must be 60, vsync 0, target 60)");
            yield return 2;
            // ---- V2 the steps
            var right = new[] { (OzFpsCap.VsyncName, -1, 1, -1, 0), ("30", 30, 0, 30, 30), ("40", 40, 0, 40, 40), ("60", 60, 0, 60, 60) };   // 1.3 item 25: 40 too
            var left = new[] { ("40", 40, 0, 40, 40), ("30", 30, 0, 30, 30), (OzFpsCap.VsyncName, -1, 1, -1, 0), ("60", 60, 0, 60, 60) };
            int k = 0;
            foreach (var w in right) { OzFpsCap.Step(1); yield return 2; k++; Check(Is(w.Item1, w.Item2, w.Item3, w.Item4) && PlayerPrefs.GetInt(OzFpsCap.CapPref, -9) == w.Item5, $"V2.{k} right arrow -> {w.Item1}: " + State()); }
            foreach (var w in left) { OzFpsCap.Step(-1); yield return 2; k++; Check(Is(w.Item1, w.Item2, w.Item3, w.Item4) && PlayerPrefs.GetInt(OzFpsCap.CapPref, -9) == w.Item5, $"V2.{k} left arrow -> {w.Item1}: " + State()); }
            // ---- V3 the rules at both settings (a level only)
            if (hud != null)
            {
                OzFpsCap.Set(60); yield return 2f; yield return Measure(hud, 8f);
                float fps60 = mFps, clock60 = mClock, ticks60 = mTicks, pf60 = mPerFrame;
                OzFpsCap.Set(OzFpsCap.VsyncValue); yield return 2f; yield return Measure(hud, 8f);
                float fpsV = mFps, clockV = mClock, ticksV = mTicks, pfV = mPerFrame;
                Check(clock60 > 0.98f && clock60 < 1.02f && clockV > 0.98f && clockV < 1.02f, $"V3.1 the game clock: {clock60:0.000} at 60, {clockV:0.000} on UNLIMITED (VSYNC) (both 1.000 +-0.02)");
                Check(Mathf.Abs(ticks60 - 100f) < 2f && Mathf.Abs(ticksV - 100f) < 2f, $"V3.2 rule ticks run per real second (at the game speed): {ticks60:0.0} at 60, {ticksV:0.0} on UNLIMITED (VSYNC) (both 100 +-2); per drawn frame {pf60:0.00} and {pfV:0.00}");
                Check(fps60 > 45f && fps60 < 61.5f, $"V3.3 frames a second: {fps60:0.0} at 60 (the 60 cap), {fpsV:0.0} on UNLIMITED (VSYNC) (reported only: in the editor the Game view need not follow vsync; the player build on the high-refresh screen is part B)");
            }
            else Check(true, "V3 skipped: the main menu has no rules running (run it again in a level for V3)");
            // ---- V4 the heat guard on UNLIMITED (VSYNC)
            OzFpsCap.Set(OzFpsCap.VsyncValue); yield return 2;
            guard = new GameObject("OzV13VsyncHeatGuard").AddComponent<HeatGuardRuntime>();
            SetTemp(92); yield return 4;
            int hotT = Application.targetFrameRate, hotV = QualitySettings.vSyncCount, hotH = HeatGuardRuntime.HeldFps;
            OzFpsCap.Step(1); OzFpsCap.Step(-1); yield return 2;                                            // the row moved away and back while hot
            int stepT = Application.targetFrameRate, stepV = QualitySettings.vSyncCount;
            float clockHot = 0f; if (hud != null) { yield return Measure(hud, 4f); clockHot = mClock; }
            SetTemp(80); yield return 4;
            int coolT = Application.targetFrameRate, coolV = QualitySettings.vSyncCount, coolH = HeatGuardRuntime.HeldFps;
            OzFpsCap.Set(60); yield return 2;
            int backT = Application.targetFrameRate, backV = QualitySettings.vSyncCount;
            SetTemp(-1); Object.Destroy(guard.gameObject); guard = null; HeatGuardRuntime.HeldFps = 0;
            Check(hotT == 30 && hotV == 0 && hotH == 30, $"V4.1 92 C on UNLIMITED (VSYNC): target {hotT}, vsync {hotV}, the guard holds {hotH} (must be 30, 0, 30)");
            Check(stepT == 30 && stepV == 0, $"V4.2 the row moved while hot: target {stepT}, vsync {stepV} (the guard's 30 stays)");
            Check(hud == null || (clockHot > 0.98f && clockHot < 1.02f), $"V4.3 the game clock while the guard holds 30: {(hud != null ? clockHot.ToString("0.000") : "no level")}");
            Check(coolT == -1 && coolV == 1 && coolH == 0, $"V4.4 80 C (under 85): target {coolT}, vsync {coolV}, the guard holds {coolH} (UNLIMITED (VSYNC) again: -1, 1, 0)");
            Check(backT == 60 && backV == 0, $"V4.5 back to 60: target {backT}, vsync {backV} (60, 0: vsync off again)");
            // ---- end
            PutBack();
            bool pass = bad == 0 && ok > 0;
            Report = $"{Tag} {(pass ? "PASS" : "FAIL")} {ok}/{ok + bad} checks ({(hud != null ? "level " + hud.game.level : "main menu")}, {Screen.width}x{Screen.height}, screen {Screen.currentResolution.refreshRateRatio.value:0.#} Hz){(bad == 0 ? "" : "; FAILED:" + fails)}";
            lines.AppendLine(Report);
            if (pass) Debug.Log(Report); else Debug.LogError(Report);
            try { File.AppendAllText(Path.Combine(dir, "checks.txt"), lines.ToString()); } catch (System.Exception e) { Debug.LogWarning(Tag + " could not write checks.txt: " + e.Message); }
        }
    }
}
