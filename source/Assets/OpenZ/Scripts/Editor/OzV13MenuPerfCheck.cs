using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Video;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.3 item 23 (a player: "The main menu is a slide show from the performance side."): the counted check of the menu light
    /// (View/OzMenuPerf.cs). Menu: OpenZ/Checks/V1.3/Main menu light (Play, main menu). Nothing is saved: the graphics preset, CALM MODE,
    /// the flyby and the menu light are put back.
    ///  P1  THE SAME PICTURE, the gate: the menu's own camera drawn into a texture of the screen's size with the light OFF and ON: the
    ///      two must be equal pixel for pixel (0 pixels differ). It is the only thing the light touches (the menu is an overlay canvas
    ///      drawn after that camera, the posters' cameras never see the light).
    ///  P2  the whole screen, for the eye: CALM MODE (the still holds still), the flyby paused on a frame, a picture with the light OFF
    ///      and one with it ON a few frames later; the differing pixels and their box are reported (the only movers left are the
    ///      caption's countdown seconds), both pictures saved.
    ///  P3  the table: the main view for 6 s per row, the current preset and LOW, light OFF and ON: frames a second (at the 60 limit),
    ///      the processor's and the graphics chip's ms; gate: with the light ON the graphics chip is never slower (+0.2 ms noise).
    ///  P4  the measurement line: one "[OpenZ MenuPerf]" 10 s line was written with its parts (fps, worst frame, 1 % low, ms, flyby).
    ///  P5  every flyby has its light copy: 1920x1080, 30 fps, the same length within one frame, the same sound tracks.
    ///      flyby is still the full 2560x1440 60 fps clip.
    ///  P7  THE FORCED-SLOW PC (25 ms of processor time burnt every menu frame): the probe ends "falls behind" within 2 + 2 x 4 s of
    ///      playing video (+ slack), the choice is saved (oz_menu_video_auto + this graphics chip), every level's list holds the light
    ///      copies, NO STALL: the worst frame in the 3 s after the switch stays under 100 ms; then the flyby on screen takes its copy at
    ///      its next cut and the counted row: menu fps and new video pictures a second, full (the probe's windows) vs light.
    ///  P8  A LATER START on that PC (the MainMenu scene loaded again, still slow): the light copies at once (every preview, its frame
    ///      1920x1080), no probe runs, the first flyby picture shows; fps and pictures a second counted.
    /// The very first start (a player, 30 Oct 2026):
    ///  P9  A FIRST START, emulated cold: AUTO with nothing remembered (as fresh PlayerPrefs), the AUTO pick of a new run, the shader
    ///      and each first flyby open held 2 s (OzLevelPreview.ProofOpenDelaySec). Then the MainMenu scene loaded:
    ///      a) no frame over 100 ms while the flybys prepare (after the scene's Start frame) and no warm-up step meanwhile;
    ///      b) the flybys prepare one at a time, the level shown first going first, every waiting level shows its still;
    ///      c) a real click (the input system) on OPTIONS while they prepare is answered within 1 frame of reaching the game;
    ///      e) then the warm-up runs in small steps: at most 30 % of the time in the menu, and no step while the mouse moves.
    ///  P10 A LATER START (nothing emulated, the warm-up done): the flybys one at a time, the first picture and all ready counted, no frame
    ///      over 100 ms.
    /// </summary>
    public static class OzV13MenuPerfCheck
    {
        const string Tag = "[OpenZ menu light check]";
        public static string Report = "not run";
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static int ok, bad, waitFrame; static float waitUntil;
        static readonly StringBuilder fails = new StringBuilder(), lines = new StringBuilder();
        static string dir;
        static bool light0, calm0; static int preset0 = -1; static VideoPlayer pausedVp;
        static bool prefsKept, hadMode, hadAuto, hadGpu; static int mode0, auto0; static string gpu0, scene0;

        [MenuItem("OpenZ/Checks/V1.3/Main menu light (Play, main menu)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning(Tag + " enter Play on the MainMenu scene first"); return; }
            if (stack.Count > 0) { Debug.LogWarning(Tag + " a run is still going"); return; }
            var m = Object.FindFirstObjectByType<OpenZMainMenu>();
            if (!m || !OzMenuPerf.I) { Debug.LogWarning(Tag + " start it on the main menu (the MainMenu scene in Play)"); return; }
            ok = bad = 0; fails.Clear(); lines.Clear(); Report = "running";
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/v1_3_20261030/item23_menu_perf"));
            Directory.CreateDirectory(dir);
            light0 = OzMenuPerf.MenuLight; calm0 = OzMotion.Reduced; preset0 = OzQuality.Level;
            hadMode = PlayerPrefs.HasKey(OzMenuVideo.Pref); mode0 = PlayerPrefs.GetInt(OzMenuVideo.Pref, 0);
            hadAuto = PlayerPrefs.HasKey(OzMenuVideo.PrefAuto); auto0 = PlayerPrefs.GetInt(OzMenuVideo.PrefAuto, 0);
            hadGpu = PlayerPrefs.HasKey(OzMenuVideo.PrefAutoGpu); gpu0 = PlayerPrefs.GetString(OzMenuVideo.PrefAutoGpu, "");
            prefsKept = true; scene0 = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            stack.Push(All(m));
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

        // ------------------------------------------------------------------ P9 / P10: the very first start
        const int ColdVariants = 120;                                                                       // the emulated cold warm-up: 120 x 57 ms = 6.8 s of work (the real list: 558)
        const float OpenDelay = 2f;                                                                         // each first flyby open held this long (a slow first read emulated)
        static Mouse vMouse, sysMouse0; static bool coldRunning;

        static void VirtualMouse(bool on)
        {
            if (on && vMouse == null)
            {
                sysMouse0 = Mouse.current;
                vMouse = InputSystem.AddDevice<Mouse>("OzV13FirstStartMouse");
                if (sysMouse0 != null && sysMouse0 != vMouse) InputSystem.DisableDevice(sysMouse0);
                vMouse.MakeCurrent();
            }
            else if (!on && vMouse != null)
            {
                if (sysMouse0 != null) { InputSystem.EnableDevice(sysMouse0); sysMouse0.MakeCurrent(); }
                if (vMouse.added) InputSystem.RemoveDevice(vMouse);
                vMouse = null; sysMouse0 = null;
            }
        }

        static void MouseAt(Vector2 p, bool down)
        {
            var st = new MouseState { position = p };
            if (down) st = st.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(vMouse, st);
        }

        static int clickFrames = -1, clickLanded = -1; static float clickMs = -1f; static string clickNote = "";
        /// <summary>A real left click (press and release through the input system) on the main menu's OPTIONS item; counted: the frames
        /// from the release reaching the game to the OPTIONS panel open. A second click closes it again.</summary>
        static IEnumerator ClickOptions()
        {
            clickFrames = clickLanded = -1; clickMs = -1f; clickNote = "";
            var m = Object.FindFirstObjectByType<OpenZMainMenu>();
            RectTransform rt = null;
            if (m) foreach (var r in m.GetComponentsInChildren<RectTransform>(true)) if (r.name == "Item_OPTIONS") { rt = r; break; }
            if (!rt || !rt.gameObject.activeInHierarchy) { clickNote = "no OPTIONS item on screen"; yield break; }
            Vector2 p = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
            VirtualMouse(true);
            MouseAt(p, false); yield return 3;                                                                // over it
            MouseAt(p, true); yield return 2;                                                                 // pressed
            int f0 = Time.frameCount, landed = -1; float r0 = Time.realtimeSinceStartup;
            MouseAt(p, false);                                                                                // released: the click
            while (Time.frameCount - f0 < 30)
            {
                if (landed < 0 && !vMouse.leftButton.isPressed) landed = Time.frameCount;
                if (landed >= 0 && m.ProofSide == "Options") break;
                yield return 1;
            }
            clickLanded = landed >= 0 ? landed - f0 : -1;
            clickFrames = landed >= 0 && m.ProofSide == "Options" ? Time.frameCount - landed : -1;
            clickMs = (Time.realtimeSinceStartup - r0) * 1000f;
            clickNote = $"OPTIONS at {p.x:0},{p.y:0}: the release reached the game {clickLanded} frame(s) after it was sent, the panel was open {clickFrames} frame(s) after that ({clickMs:0} ms from sending)";
            MouseAt(p, true); yield return 2; MouseAt(p, false); yield return 4;                              // a second click closes it again
            VirtualMouse(false);
        }

        static IEnumerator LoadMenu()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene0);
            float t0 = Time.realtimeSinceStartup;
            yield return 1;
            while ((!Object.FindFirstObjectByType<OpenZMainMenu>() || !OzMenuPerf.I) && Time.realtimeSinceStartup - t0 < 20f) yield return 1;
            yield return 2;                                                                                   // the scene's Start frame (the maps, the minimaps, the UI) is behind
            if (OzMenuPerf.I) OzMenuPerf.I.ProofReset();
        }

        static IEnumerator FirstStart()
        {
            prefsKept = true;                                                                                   // PutBack restores the run's own values once more
            PlayerPrefs.SetInt(OzMenuVideo.Pref, OzMenuVideo.Auto); OzMenuVideo.ProofForget();                 // as fresh PlayerPrefs: AUTO, nothing remembered
            OzMenuPerf.ProofNewRun();                                                                           // the AUTO pick of a new run
            OzShaderWarm.ProofColdStepMs = OzShaderWarm.ColdStepMs; OzShaderWarm.ProofColdVariants = ColdVariants;
            OzShaderWarm.ProofRestart(); coldRunning = true;                                                    // the warm-up from the start, every variant cold
            OzLevelPreview.ProofOpenDelaySec = OpenDelay;
            int sLoad = OzShaderWarm.StepsDone;
            yield return LoadMenu();
            var m = Object.FindFirstObjectByType<OpenZMainMenu>(); var perf = OzMenuPerf.I;
            if (!m || !perf) { Check(false, "P9 the menu did not come back"); yield break; }

            // a) + b) + c): while the flybys prepare
            int s0 = OzShaderWarm.StepsDone, stillLooks = 0, wrong = 0; string firstShown = null; bool clicked = false;
            float tq = Time.realtimeSinceStartup, firstPic = -1f;
            while (OzLevelPreview.PreparesPending && Time.realtimeSinceStartup - tq < 40f)
            {
                foreach (var p in OzLevelPreview.Live)
                {
                    if (p.ShowingVideo) { if (firstShown == null) { firstShown = p.Level; firstPic = Time.realtimeSinceStartup - tq; } }
                    else if (p.Poster != null) { stillLooks++; if (p.Sharp != p.Poster) wrong++; }
                }
                if (!clicked) { clicked = true; yield return ClickOptions(); continue; }
                yield return 1;
            }
            float prepSec = Time.realtimeSinceStartup - tq; bool ready = !OzLevelPreview.PreparesPending;
            float worstA = perf.ProofWorstMs; int over100 = perf.ProofOver100, stepsA = OzShaderWarm.StepsDone - s0;
            string order = OzLevelPreview.ProofOrder; int atOnce = OzLevelPreview.ProofMaxAtOnce;
            Note($"P9 first start: the flybys prepared in {prepSec:0.0} s (each first open held {OpenDelay:0} s): {order}; most at once {atOnce}; first picture {firstPic:0.00} s ({firstShown ?? "none"}); warm-up steps before the menu's first frame {s0 - sLoad}");
            Check(ready && over100 == 0 && worstA < 100f && stepsA == 0,
                  $"P9a no frame over 100 ms while the flybys prepare: worst {worstA:0.0} ms, {over100} over 100 ms in {prepSec:0.0} s; shader warm-up steps meanwhile {stepsA} (0 wanted, held by the menu); all ready {ready}");
            bool shownFirst = firstShown != null && order.StartsWith(firstShown + " ");
            Check(ready && atOnce == 1 && shownFirst && wrong == 0 && stillLooks > 0,
                  $"P9b one at a time, the level shown first going first: most at once {atOnce} (1 wanted), order {order}, shown first {firstShown ?? "none"}; a waiting level showed its still {stillLooks - wrong} of {stillLooks} times");
            Check(clickFrames >= 0 && clickFrames <= 1, $"P9c a click on OPTIONS while the flybys prepare is answered within 1 frame: {clickNote}");

            // d) the AUTO pick
            if (m.ProofView != "Main") m.ProofMain(); int s1 = OzShaderWarm.StepsDone; float tp = Time.realtimeSinceStartup;
            yield return WaitProbe(60f);
            int stepsD = OzShaderWarm.StepsDone - s1; float held = perf.ProofWarmHeldSec;
            Note("P9 the AUTO pick's windows with the cold warm-up waiting: " + OzMenuPerf.ProbeNotes);
            Check(OzMenuPerf.ProbeState == OzMenuPerf.Probe.KeepsUp && !OzMenuVideo.AutoLight && (stepsD == 0 || held >= OzMenuPerf.WarmHoldMaxSec),
                  $"P9d the AUTO pick is not misled by the first start: {OzMenuPerf.ProbeState} after {Time.realtimeSinceStartup - tp:0.0} s (KeepsUp wanted on this PC, nothing saved {!OzMenuVideo.AutoLight}); warm-up steps while it measured {stepsD} (0 wanted; the menu held it {held:0.0} s of at most {OzMenuPerf.WarmHoldMaxSec:0})");

            // e) the warm-up in small steps, and held while the mouse moves
            yield return 1f;
            int s2 = OzShaderWarm.StepsDone; double w2 = OzShaderWarm.WorkMsDone; float t2 = Time.realtimeSinceStartup;
            perf.ProofReset(); yield return 6f;
            float span = Time.realtimeSinceStartup - t2; int stepsE = OzShaderWarm.StepsDone - s2;
            float share = (float)((OzShaderWarm.WorkMsDone - w2) / (span * 1000.0)); perf.ProofNow(out float fpsE, out _, out _); float worstE = perf.ProofWorstMs;
            VirtualMouse(true); var c = new Vector2(Screen.width * 0.5f - 80f, Screen.height * 0.5f);
            int k3 = 0;
            for (; k3 < 3; k3++) { MouseAt(c + new Vector2(4f * (k3 % 40), 0f), false); yield return 1; }   // the menu sees the mouse move (its hold starts the next frame)
            int s3 = OzShaderWarm.StepsDone; float t3 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t3 < 2f) { MouseAt(c + new Vector2(4f * (k3 % 40), 0f), false); k3++; yield return 1; }
            int stepsMove = OzShaderWarm.StepsDone - s3; VirtualMouse(false);
            Note($"P9 the warm-up after the hold: {stepsE} cold steps in {span:0.0} s, {share * 100f:0} % of the time, menu {fpsE:0.0} fps, worst frame {worstE:0.0} ms (one variant of {OzShaderWarm.ColdStepMs:0} ms cannot be split; without the rests it would take about {OzShaderWarm.ColdStepMs / (OzShaderWarm.ColdStepMs + 16.7f) * 100f:0} %)");
            Check(stepsE > 0 && share <= 0.30f && stepsMove == 0,
                  $"P9e then the warm-up runs in small steps: {stepsE} steps, {share * 100f:0} % of the menu's time (at most 30 %); steps while the mouse moved for 2 s: {stepsMove} (0 wanted)");
            OzShaderWarm.ProofCancel(); coldRunning = false; OzShaderWarm.ProofColdStepMs = 0f; OzShaderWarm.ProofColdVariants = 0;
            OzLevelPreview.ProofOpenDelaySec = 0f;

            // P10 a later start: nothing emulated, the warm-up done
            yield return LoadMenu();
            perf = OzMenuPerf.I; int s10 = OzShaderWarm.StepsDone; float t10 = Time.realtimeSinceStartup, pic10 = -1f;
            while ((OzLevelPreview.PreparesPending || pic10 < 0f) && Time.realtimeSinceStartup - t10 < 20f)
            {
                if (pic10 < 0f) foreach (var p in OzLevelPreview.Live) if (p.ShowingVideo) { pic10 = Time.realtimeSinceStartup - t10; break; }
                yield return 1;
            }
            bool ready10 = !OzLevelPreview.PreparesPending; float worst10 = perf ? perf.ProofWorstMs : -1f; int over10 = perf ? perf.ProofOver100 : -1;
            Check(ready10 && pic10 >= 0f && pic10 <= 2f && OzLevelPreview.ProofMaxAtOnce == 1 && over10 == 0 && OzShaderWarm.StepsDone == s10,
                  $"P10 a later start: {OzLevelPreview.ProofOrder}, most at once {OzLevelPreview.ProofMaxAtOnce}; first picture {pic10:0.00} s (2 s at most), all ready {OzLevelPreview.ProofAllReadySec:0.00} s; worst frame {worst10:0.0} ms, {over10} over 100 ms; warm-up steps {OzShaderWarm.StepsDone - s10} (done: {OzShaderWarm.State})");
            PutBack();
        }

        static void Check(bool pass, string what)
        {
            if (pass) ok++; else { bad++; fails.Append(" | ").Append(what); }
            string l = $"{Tag} {(pass ? "ok  " : "FAIL")} {what}"; lines.AppendLine(l); Debug.Log(l);
        }
        static void Note(string what) { string l = $"{Tag} note {what}"; lines.AppendLine(l); Debug.Log(l); }

        static void PutBack()
        {
            OzMenuPerf.MenuLight = light0;
            if (OzMotion.Reduced != calm0) OzMotion.Reduced = calm0;
            if (preset0 >= 0 && OzQuality.Level != preset0) OzQuality.Level = preset0;
            if (pausedVp) { pausedVp.Play(); pausedVp = null; }
            OzMenuPerf.ProofBurnMs = 0f;
            VirtualMouse(false); OzLevelPreview.ProofOpenDelaySec = 0f;                                          // item 23 P9
            if (coldRunning) { coldRunning = false; OzShaderWarm.ProofCancel(); }
            OzShaderWarm.ProofColdStepMs = 0f; OzShaderWarm.ProofColdVariants = 0;
            if (prefsKept)
            {
                prefsKept = false;
                if (hadMode) PlayerPrefs.SetInt(OzMenuVideo.Pref, mode0); else PlayerPrefs.DeleteKey(OzMenuVideo.Pref);
                if (hadAuto) PlayerPrefs.SetInt(OzMenuVideo.PrefAuto, auto0); else PlayerPrefs.DeleteKey(OzMenuVideo.PrefAuto);
                if (hadGpu) PlayerPrefs.SetString(OzMenuVideo.PrefAutoGpu, gpu0); else PlayerPrefs.DeleteKey(OzMenuVideo.PrefAutoGpu);
                PlayerPrefs.Save();
            }
        }

        // ------------------------------------------------------------------ the AUTO pick of the menu video
        static bool AllLight(out string what)
        {
            var live = OzLevelPreview.Live; int n = 0, light = 0; var s = new StringBuilder();
            foreach (var p in live)
            {
                if (!p.VideoOk) continue; n++;
                if (p.IsLight) light++;                                                                   // the list holds the copies (a playing player keeps its clip and frame until its cut)
                s.Append(' ').Append(p.Level).Append(':').Append(p.Player.clip ? p.Player.clip.name : "?").Append(p.Frame ? " " + p.Frame.width + "x" + p.Frame.height : "");
            }
            what = $"{light}/{n} previews hold the light copies;{s}";
            return n > 0 && light == n;
        }

        static bool NoneLight(out string what)
        {
            var live = OzLevelPreview.Live; int n = 0, full = 0; var s = new StringBuilder();
            foreach (var p in live)
            {
                if (!p.VideoOk) continue; n++;
                bool f = !p.IsLight && p.Player.clip && !p.Player.clip.name.EndsWith(OzMenuVideo.Suffix) && p.Frame && p.Frame.width == 2560 && p.Frame.height == 1440;
                if (f) full++;
                s.Append(' ').Append(p.Level).Append(':').Append(p.Player.clip ? p.Player.clip.name : "?").Append(p.Frame ? " " + p.Frame.width + "x" + p.Frame.height : "");
            }
            what = $"{full}/{n} previews on the full 2560x1440 flybys;{s}";
            return n > 0 && full == n;
        }

        static IEnumerator WaitProbe(float maxSec)
        {
            float t0 = Time.realtimeSinceStartup;
            while (OzMenuPerf.ProbeState == OzMenuPerf.Probe.Measuring && Time.realtimeSinceStartup - t0 < maxSec) yield return 5;
        }

        static IEnumerator Reload()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene0);
            float t0 = Time.realtimeSinceStartup;
            yield return 10;
            while ((!Object.FindFirstObjectByType<OpenZMainMenu>() || !OzMenuPerf.I) && Time.realtimeSinceStartup - t0 < 20f) yield return 5;
            var m = Object.FindFirstObjectByType<OpenZMainMenu>();
            if (m && m.ProofView != "Main") m.ProofMain();
        }

        static float firstPictureSec;
        static IEnumerator WaitPicture(float maxSec)
        {
            float t0 = Time.realtimeSinceStartup; firstPictureSec = -1f;
            while (Time.realtimeSinceStartup - t0 < maxSec)
            {
                foreach (var p in OzLevelPreview.Live) if (p.ShowingVideo) { firstPictureSec = Time.realtimeSinceStartup - t0; yield break; }
                yield return 2;
            }
        }

        static float vFps, vShown, vDeliv; static int vDrop;
        static IEnumerator CountVideo(float seconds)
        {
            OzMenuPerf.I.ProofReset(); yield return seconds;
            OzMenuPerf.I.ProofNow(out vFps, out _, out _); OzMenuPerf.I.ProofVideo(out vShown, out vDrop); vDeliv = OzMenuPerf.I.ProofDeliveredPerSec;
        }

        static IEnumerator AutoPick(OpenZMainMenu m)
        {
            // ---- P5 the copies
            int pairs = 0, good = 0; var bad5 = new StringBuilder();
            foreach (var d in OzLevelDef.All)
                for (int k = 1; k <= 9; k++)
                {
                    string n = k == 1 ? d.Id + "_preview" : d.Id + "_preview_" + k;
                    var full = Resources.Load<VideoClip>("OpenZ/Menu/" + n); if (!full) break;
                    var lite = Resources.Load<VideoClip>("OpenZ/Menu/" + n + OzMenuVideo.Suffix);
                    pairs++;
                    bool ok5 = lite && lite.width == 1920 && lite.height == 1080 && System.Math.Abs(lite.frameRate - 30.0) < 0.01 && System.Math.Abs(lite.length - full.length) <= 1.0 / 30 + 1e-6 && lite.audioTrackCount == full.audioTrackCount;
                    if (ok5) good++; else bad5.Append(' ').Append(n).Append(lite ? $" ({lite.width}x{lite.height} {lite.frameRate:0.##} fps {lite.length:0.000} s vs {full.length:0.000} s)" : " (missing)");
                }
            Check(pairs > 0 && good == pairs, $"P5 every flyby has its light copy (1920x1080, 30 fps, the same length within one frame, the same sound tracks): {good}/{pairs}{bad5}");

            // ---- P6 a strong PC (this one, as it is): AUTO, nothing remembered
            PlayerPrefs.SetInt(OzMenuVideo.Pref, OzMenuVideo.Auto); OzMenuVideo.ProofForget();
            if (!NoneLight(out _)) { yield return Reload(); yield return 2f; }
            OzMenuPerf.I.ProbeRestart();
            yield return WaitProbe(60f);
            string n6 = OzMenuPerf.ProbeNotes; bool none6 = NoneLight(out string w6);
            Note("P6 the probe's windows on this PC: " + n6);
            Check(OzMenuPerf.ProbeState == OzMenuPerf.Probe.KeepsUp && !OzMenuVideo.AutoLight && !PlayerPrefs.HasKey(OzMenuVideo.PrefAuto) && none6,
                  $"P6 no change on a strong PC: the probe says {OzMenuPerf.ProbeState} (KeepsUp wanted), nothing saved {!PlayerPrefs.HasKey(OzMenuVideo.PrefAuto)}; {w6}");
            yield return CountVideo(6f); float fFps = vFps, fShown = vShown, fDeliv = vDeliv; int fDrop = vDrop;

            // ---- P7 the forced-slow PC: the first start that switches
            OzMenuPerf.ProofBurnMs = OzMenuPerf.SlowBurnMs;
            yield return 1f;
            float t7 = Time.realtimeSinceStartup;
            OzMenuPerf.I.ProbeRestart();
            OzMenuPerf.I.ProofReset();
            yield return WaitProbe(40f);
            float decided = Time.realtimeSinceStartup - t7; string n7 = OzMenuPerf.ProbeNotes;
            OzMenuPerf.I.ProofReset(); yield return 3f; float worst7 = OzMenuPerf.I.ProofWorstMs;
            bool all7 = AllLight(out string w7);
            Note("P7 the probe's windows on the forced-slow PC: " + n7);
            Check(OzMenuPerf.ProbeState == OzMenuPerf.Probe.FellBehind && decided <= OzMenuVideo.WarmUp + OzMenuVideo.Decide * OzMenuVideo.Window + 6f,
                  $"P7 the forced-slow PC ({OzMenuPerf.SlowBurnMs:0} ms burnt a frame) falls behind: {OzMenuPerf.ProbeState} after {decided:0.0} s (at most {OzMenuVideo.WarmUp + OzMenuVideo.Decide * OzMenuVideo.Window + 6f:0} s)");
            Check(OzMenuVideo.AutoLight && PlayerPrefs.GetInt(OzMenuVideo.PrefAuto, 0) == OzMenuVideo.Light && PlayerPrefs.GetString(OzMenuVideo.PrefAutoGpu, "") == SystemInfo.graphicsDeviceName,
                  $"P7 the choice is remembered on this PC: oz_menu_video_auto {PlayerPrefs.GetInt(OzMenuVideo.PrefAuto, 0)}, graphics \"{PlayerPrefs.GetString(OzMenuVideo.PrefAutoGpu, "")}\"");
            Check(all7 && worst7 < 100f, $"P7 no stall at the switch: the worst frame in the 3 s after it {worst7:0.0} ms (under 100 ms, {OzMenuPerf.SlowBurnMs:0} ms of it the burn); {w7}");
            float t7b = Time.realtimeSinceStartup; bool onScreen = false;                                    // the flyby on screen takes its copy at its next cut
            while (Time.realtimeSinceStartup - t7b < 45f && !onScreen)
            {
                onScreen = true; bool any = false;
                foreach (var p in OzLevelPreview.Live) if (p.ShowingVideo) { any = true; if (!p.Player.clip || !p.Player.clip.name.EndsWith(OzMenuVideo.Suffix)) onScreen = false; }
                onScreen &= any; if (!onScreen) yield return 10;
            }
            yield return 1f;
            yield return CountVideo(6f); float lFps = vFps, lShown = vShown, lDeliv = vDeliv; int lDrop = vDrop;
            float cap = OzMenuVideo.ScreenCap(), lExp = cap > 0f ? Mathf.Min(30f, cap) : 30f;
            Note($"P7 the counted row (menu fps, new video pictures a second, dropped in 6 s): this PC full {fFps:0.0} fps, delivered {fDeliv:0.0} / 60 (index moved on {fShown:0.0} menu frames a second), {fDrop} dropped | forced slow, full: see the probe's windows above | forced slow, light {lFps:0.0} fps, delivered {lDeliv:0.0} / {lExp:0} (index moved {lShown:0.0}), {lDrop} dropped");
            Check(onScreen && lDeliv >= lExp * OzMenuVideo.MinShownShare, $"P7 the flyby on screen took its light copy at its cut ({onScreen}, {Time.realtimeSinceStartup - t7b:0} s) and the forced-slow PC keeps up with it: {lDeliv:0.0} video pictures delivered a second, at most one a menu frame (line {lExp * OzMenuVideo.MinShownShare:0.0}; the AUTO pick's own measure)");

            // ---- P8 a later start on that PC (still slow): the light copies at once
            yield return Reload();
            yield return WaitPicture(15f);
            yield return 1f;
            bool all8 = AllLight(out string w8);
            bool frames8 = true; foreach (var p in OzLevelPreview.Live) if (p.VideoOk && (!p.Frame || p.Frame.width != 1920 || p.Frame.height != 1080)) frames8 = false;
            yield return CountVideo(6f);
            Check(all8 && frames8 && OzMenuPerf.ProbeState != OzMenuPerf.Probe.Measuring && firstPictureSec >= 0f,
                  $"P8 a later start opens the light copies at once: {w8}; frames 1920x1080 {frames8}; probe {OzMenuPerf.ProbeState} (not measuring); first flyby picture after {firstPictureSec:0.00} s; then {vFps:0.0} fps, {vDeliv:0.0} video pictures delivered a second (index moved {vShown:0.0}), {vDrop} dropped in 6 s");

            PutBack();
            yield return Reload();
            yield return 2f;
            NoneLight(out string w9);
            Note($"put back: oz_menu_video / _auto / _auto_gpu as before the run, no burn, the menu loaded again: {w9}");
        }

        /// <summary>The menu camera alone, drawn into a texture of the screen's size, read back.</summary>
        static Color32[] CameraPixels(Camera cam, out int w, out int h)
        {
            w = Screen.width; h = Screen.height;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var was = cam.targetTexture; var act = RenderTexture.active;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = was;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(false);
            RenderTexture.active = act; RenderTexture.ReleaseTemporary(rt);
            var px = tex.GetPixels32(); Object.Destroy(tex);
            return px;
        }

        static int Diff(Color32[] a, Color32[] b, int w, out RectInt box)
        {
            int n = 0, x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                var p = a[i]; var q = b[i];
                if (p.r != q.r || p.g != q.g || p.b != q.b)
                {
                    n++; int x = i % w, y = i / w;
                    if (x < x0) x0 = x; if (y < y0) y0 = y; if (x > x1) x1 = x; if (y > y1) y1 = y;
                }
            }
            box = n > 0 ? new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1) : new RectInt(0, 0, 0, 0);
            return n;
        }

        static Texture2D shotA, shotB;
        /// <summary>A screen picture as the other proofs take it (written at the end of the frame), then read back (PNG: no loss).</summary>
        static IEnumerator Shot(bool b)
        {
            string path = Path.Combine(dir, (b ? "menu_light_on_" : "menu_light_off_") + Screen.width + "x" + Screen.height + ".png");
            try { if (File.Exists(path)) File.Delete(path); } catch (System.Exception) { }
            ScreenCapture.CaptureScreenshot(path);
            float t0 = Time.realtimeSinceStartup;
            while (!File.Exists(path) && Time.realtimeSinceStartup - t0 < 4f) yield return 1;
            yield return 0.3f;                                                                              // the file is complete
            if (!File.Exists(path)) yield break;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!t.LoadImage(File.ReadAllBytes(path))) { Object.Destroy(t); yield break; }
            if (b) shotB = t; else shotA = t;
        }

        static float mFps, mCpu, mGpu;
        static IEnumerator Measure(float seconds)
        {
            yield return 2f;
            OzMenuPerf.I.ProofReset();
            yield return seconds;
            OzMenuPerf.I.ProofNow(out mFps, out mCpu, out mGpu);
        }

        static IEnumerator All(OpenZMainMenu m)
        {
            if (m.ProofView != "Main") { m.ProofMain(); yield return 1f; }
            yield return 1f;
            var perf = OzMenuPerf.I; var cam = perf.ProofMenuCamera;
            Check(cam != null && cam.cullingMask == 0, $"P0 the menu's own camera found: {(cam ? cam.name : "none")}, culling mask {(cam ? cam.cullingMask : -1)} (draws no object)");
            if (!cam) { Finish(); yield break; }

            // ---- P1 the gate: the camera's own picture, OFF vs ON
            OzMenuPerf.MenuLight = false; yield return 3;
            var off = CameraPixels(cam, out int w, out int h);
            OzMenuPerf.MenuLight = true; yield return 3;
            int rendersOn0 = OzMenuPerf.LightRenders;
            var on = CameraPixels(cam, out _, out _);
            bool lightRan = OzMenuPerf.LightRenders > rendersOn0;
            int d1 = Diff(off, on, w, out var box1);
            Check(d1 == 0 && lightRan && !cam.allowMSAA, $"P1 the menu camera's picture {w}x{h}, light OFF vs ON: {d1} pixels differ (box {box1}); the light paused the features in that render {lightRan}; MSAA on the camera now {cam.allowMSAA}");

            // ---- P2 the whole screen for the eye
            OzMotion.Reduced = true;
            foreach (var v in m.GetComponentsInChildren<VideoPlayer>(true)) if (v && v.isPlaying) { v.Pause(); pausedVp = v; break; }
            yield return 1.5f;
            OzMenuPerf.MenuLight = false; yield return 3; yield return Shot(false);
            OzMenuPerf.MenuLight = true; yield return 3; yield return Shot(true);
            if (shotA && shotB && shotA.width == shotB.width)
            {
                int d2 = Diff(shotA.GetPixels32(), shotB.GetPixels32(), shotA.width, out var box2);
                Note($"P2 the whole screen (CALM MODE, the flyby paused), light OFF vs ON a few frames apart: {d2} of {shotA.width * shotA.height} pixels differ, box {box2} (0 expected but the caption's countdown seconds); pictures menu_light_off / _on beside checks.txt");
            }
            else Note("P2 the screen pictures could not be taken");
            if (shotA) Object.Destroy(shotA); if (shotB) Object.Destroy(shotB); shotA = shotB = null;
            if (pausedVp) { pausedVp.Play(); pausedVp = null; }
            OzMotion.Reduced = calm0;

            // ---- P3 the table
            var rows = new List<string>();
            int[] presets = preset0 == 2 ? new[] { 2 } : new[] { preset0, 2 };
            bool slower = false;
            foreach (int q in presets)
            {
                if (OzQuality.Level != q) { OzQuality.Level = q; yield return 1f; }
                OzMenuPerf.MenuLight = false; yield return Measure(6f); float fOff = mFps, cOff = mCpu, gOff = mGpu;
                OzMenuPerf.MenuLight = true; yield return Measure(6f); float fOn = mFps, cOn = mCpu, gOn = mGpu;
                rows.Add($"{OzQuality.Names[q],-6} {Screen.width}x{Screen.height} | OFF {fOff,5:0.0} fps, CPU {cOff,5:0.0} ms, GPU {gOff,5:0.0} ms | ON {fOn,5:0.0} fps, CPU {cOn,5:0.0} ms, GPU {gOn,5:0.0} ms | GPU saved {gOff - gOn:0.0} ms");
                if (gOff > 0f && gOn > gOff + 0.2f) slower = true;
            }
            if (OzQuality.Level != preset0) { OzQuality.Level = preset0; yield return 1f; }
            foreach (var r in rows) Note("P3 " + r);
            Check(!slower, "P3 with the light ON the graphics chip is never slower than OFF (+0.2 ms): " + (slower ? "SLOWER on a row" : "yes") + $" ({rows.Count} rows, at the 60 limit; the GPU ms is the measure)");

            // ---- P4 the line
            OzMenuPerf.MenuLight = true;
            float t0 = Time.realtimeSinceStartup; string before = OzMenuPerf.LastLine;
            while (OzMenuPerf.LastLine == before && Time.realtimeSinceStartup - t0 < OzMenuPerf.Every + 3f) yield return 10;
            string l = OzMenuPerf.LastLine;
            Check(l != before && l.Contains(" fps, worst frame ") && l.Contains("1 % low") && l.Contains("processor main") && l.Contains("graphics chip") && (l.Contains("; flyby ") || l.Contains("no flyby playing")) && l.Contains("menu light ON") && l.Contains("; menu video "),
                  "P4 the Player.log line: \"" + l + "\"");
            yield return AutoPick(m);
            yield return FirstStart();
            Finish();
        }

        static void Finish()
        {
            PutBack();
            bool pass = bad == 0 && ok > 0;
            Report = $"{Tag} {(pass ? "PASS" : "FAIL")} {ok}/{ok + bad} checks ({Screen.width}x{Screen.height}){(bad == 0 ? "" : "; FAILED:" + fails)}";
            lines.AppendLine(Report);
            if (pass) Debug.Log(Report); else Debug.LogError(Report);
            try { File.AppendAllText(Path.Combine(dir, "checks.txt"), lines.ToString()); } catch (System.Exception e) { Debug.LogWarning(Tag + " could not write checks.txt: " + e.Message); }
        }
    }
}
