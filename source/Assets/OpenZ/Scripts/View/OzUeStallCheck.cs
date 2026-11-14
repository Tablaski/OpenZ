#if !OZ_RELEASE
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUnitEditor.cs): the counted frame-cost check of the playground with a big fight on the field
    /// (menu OpenZ/V1.2/Unit Editor/4 Frame Cost Check (big fight), Editor/OzUeStallCheckMenu.cs). At 1366 x 768, then at 1920 x 1080
    /// the field is filled with SAMPLE FIGHT and x5 squads of both teams up to 100 units or more (filled again whenever the fight takes
    /// it under 100), empty light tanks and flags stand by it, name labels show on every unit, one unit is picked and the pointer rests
    /// on an enemy (every kind of dashed world mark is drawn every frame: rings of empty vehicles and flags, the picked and the hovered
    /// unit's range rings), and the fight runs 120 s. Per 10 s window: frames, frames per second, the mean and the longest frame, the
    /// units, the UI objects of the HUD canvas, the managed memory allocated per frame, the dash walk's steps against their bound, the
    /// marks and labels drawn. PASS when nothing grows: no frame over 250 ms (the frames that place a new fight left out), the last
    /// window's mean frame at most 1.25 x the first's + 5 ms and its frames per second at least 0.8 x the first's - 1, the UI objects
    /// and the tooltip registrations not growing, the allocation per frame not growing, the dash walk inside its bound in every frame
    /// and never out of its step budget, the stats box never rebuilt, 0 console errors. Writes stall_check.txt into the kit's proof
    /// folder (k6b_unit_editor_fixes/proof of the newest 1.2 stage) and logs one line "[OpenZ UnitEditor frame cost] PASS n/n ...".
    /// The player's editor settings it changes (name labels, speed, the Game view's size) are put back; the field is cleared.
    /// </summary>
    public static class OzUeStallCheck
    {
        const string Tag = "[OpenZ UnitEditor frame cost]";
        public const float Seconds = 120f, Window = 10f, MaxFrame = 0.25f, MinUnits = 100;
        public static string Report = "not run";
        /// <summary>Where stall_check.txt goes; empty = the kit's proof folder.</summary>
        public static string Folder;
        public static bool Running => runner;
        static OzUeStallRunner runner;
        static int ok, bad, errors;
        static readonly StringBuilder lines = new StringBuilder(), errorLines = new StringBuilder();
        static string dir, size = "";
        static bool labels0, kept; static float speed0;
#if UNITY_EDITOR
        static int gameView0 = -1;
#endif

        /// <summary>Starts the check (Play mode: the MainMenu scene or the playground).</summary>
        public static void Begin()
        {
            if (runner) { Debug.LogWarning(Tag + " a run is still going"); return; }
            ok = bad = errors = 0; lines.Clear(); errorLines.Clear(); kept = false; Report = "running";
            dir = OutDir(); Directory.CreateDirectory(dir);
            var go = new GameObject("OzUe_FrameCostCheck"); UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<OzUeStallRunner>();
            Application.logMessageReceived += OnLog;
            runner.Push(Main());
            Debug.Log(Tag + " started: the report goes to " + dir);
        }

        public static void Abort()
        {
            if (!runner) { Debug.Log(Tag + " no run is going"); return; }
            Check(false, "the check was stopped by hand");
            Finish();
        }

        internal static void Crashed(Exception e) { Check(false, "the check stopped on an exception: " + e.GetType().Name + ": " + e.Message); Finish(); }

        static string OutDir()
        {
            if (!string.IsNullOrEmpty(Folder)) return Path.GetFullPath(Folder);
            try
            {
                string staging = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging"));
                if (Directory.Exists(staging))
                {
                    var ds = Directory.GetDirectories(staging, "v1_2*");
                    Array.Sort(ds, StringComparer.Ordinal);
                    for (int i = ds.Length - 1; i >= 0; i--) if (Directory.Exists(Path.Combine(ds[i], "k6b_unit_editor_fixes"))) return Path.Combine(ds[i], "k6b_unit_editor_fixes", "proof");
                }
            }
            catch (Exception) { }
            return Path.Combine(Application.persistentDataPath, "UnitEditorFrameCost");
        }

        static void OnLog(string cond, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (cond != null && cond.StartsWith(Tag, StringComparison.Ordinal)) return;
            if (errors++ < 20) errorLines.AppendLine("  " + type + ": " + (cond ?? "").Split('\n')[0]);
        }

        static void Check(bool pass, string what)
        {
            if (pass) ok++; else bad++;
            lines.Append(pass ? "PASS  " : "FAIL  ").Append(size.Length > 0 ? "[" + size + "] " : "").AppendLine(what);
        }

        static void Info(string what) { lines.Append("INFO  ").Append(size.Length > 0 ? "[" + size + "] " : "").AppendLine(what); }

        static void Finish()
        {
            Application.logMessageReceived -= OnLog;
            size = "";
            try { Restore(); } catch (Exception e) { Info("the settings could not all be put back: " + e.Message); }
            Check(errors == 0, "0 console errors or exceptions in the whole run (" + errors + ")");
            if (errors > 0) lines.Append("INFO  the first console errors:\n").Append(errorLines);
            Report = Tag + " " + (bad == 0 ? "PASS" : "FAIL") + " " + ok + "/" + (ok + bad) + " checks -> " + dir;
            try { File.WriteAllText(Path.Combine(dir, "stall_check.txt"), Report + "\n\n" + lines); } catch (Exception e) { Debug.LogWarning(Tag + " stall_check.txt not written: " + e.Message); }
            if (bad == 0) Debug.Log(Report); else Debug.LogError(Report);
            Debug.Log(lines.ToString());
            if (runner) { runner.Stop(); UnityEngine.Object.Destroy(runner.gameObject); }
            runner = null;
        }

        // ================================================================== the run
        static IEnumerator Main()
        {
            if (!OzUnitEditor.UiBuilt)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>();
                var go = typeof(OpenZMainMenu).GetMethod("GoUnitEditor", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (!menu || go == null) { Check(false, "the check starts in the MainMenu scene or in the playground"); Finish(); yield break; }
                go.Invoke(menu, null);
                float t0 = Time.realtimeSinceStartup;
                while (!OzUnitEditor.UiBuilt && Time.realtimeSinceStartup - t0 < 45f) yield return 1;
                if (!OzUnitEditor.UiBuilt) { Check(false, "the playground opens from the main menu (45 s)"); Finish(); yield break; }
                yield return 1.5f;
            }
#if UNITY_EDITOR
            gameView0 = OzFinalGateView.SelectedIndex();
#endif
            labels0 = OzUeBackdrop.NameLabels; speed0 = OzUeControls.Speed; kept = true;
            runner.Sampler.ResetPeaks();
            yield return OneSize(1366, 768);
            yield return OneSize(1920, 1080);
            Finish();
        }

        static void Restore()
        {
            if (OzUnitEditor.UiBuilt)
            {
                OzUePick.Unpick(); OzUePick.ClearInspect(); OzUePlayground.ClearField();
                if (kept) { OzUeBackdrop.SetNameLabels(labels0); OzUeControls.SetSpeed(speed0); }
                OzUeControls.SetPlaying(true);
            }
#if UNITY_EDITOR
            if (gameView0 >= 0) OzFinalGateView.Select(gameView0);
            gameView0 = -1;
#endif
        }

        /// <summary>The HUD canvas has followed the screen's size: the CanvasScaler ran for it (its canvas = the rule's for this screen and
        /// UI scale within 1 u). Read before that, the canvas is still the last size's.</summary>
        static bool CanvasFollows()
        {
            var want = OzUnitEditor.CanvasFor(Screen.width, Screen.height, OzText.UiScale); var cs = OzUnitEditor.CanvasSize;
            return Mathf.Abs(cs.x - want.x) < 1f && Mathf.Abs(cs.y - want.y) < 1f;
        }

        static IEnumerator OneSize(int w, int h)
        {
            size = w + "x" + h;
#if UNITY_EDITOR
            OzFinalGateView.SetFixed(w, h, "OpenZ " + w + "x" + h, out string info);
            float tw = Time.realtimeSinceStartup;
            while ((Screen.width != w || Screen.height != h) && Time.realtimeSinceStartup - tw < 5f) yield return 1;
#else
            string info = "a player keeps its own size";
#endif
            float tc = Time.realtimeSinceStartup;                                                         // the canvas scaler runs after the size changed: the canvas is read once it follows
            while (!CanvasFollows() && Time.realtimeSinceStartup - tc < 3f) yield return 1;
            var canvasWant = OzUnitEditor.CanvasFor(Screen.width, Screen.height, OzText.UiScale);
            Info("Game view " + Screen.width + " x " + Screen.height + " (" + info + "), canvas " + OzUnitEditor.CanvasSize.x.ToString("0", CultureInfo.InvariantCulture) + " x "
                 + OzUnitEditor.CanvasSize.y.ToString("0", CultureInfo.InvariantCulture) + " u, " + OzUnitEditor.PxPerUnit.ToString("0.###", CultureInfo.InvariantCulture) + " px per u");
            Check(CanvasFollows(), "the HUD canvas follows the new size before the fight: " + OzUnitEditor.CanvasSize.x.ToString("0", CultureInfo.InvariantCulture) + " x "
                  + OzUnitEditor.CanvasSize.y.ToString("0", CultureInfo.InvariantCulture) + " u (the rule's " + canvasWant.x.ToString("0", CultureInfo.InvariantCulture) + " x "
                  + canvasWant.y.ToString("0", CultureInfo.InvariantCulture) + " u for " + Screen.width + " x " + Screen.height + " at UI SCALE "
                  + Mathf.RoundToInt(OzText.UiScale * 100f).ToString(CultureInfo.InvariantCulture) + " %)");
            Check(Screen.width == w && Screen.height == h, "the Game view shows " + w + " x " + h + " (" + Screen.width + " x " + Screen.height + ")");
            yield return 1.0f;
            OzUeControls.CompassHome(); OzUePick.Unpick(); OzUePick.ClearInspect(); OzUePlayground.ClearField();
            OzUeControls.SetSpeed(1f); OzUeControls.SetPlaying(true); OzUeBackdrop.SetNameLabels(true);
            yield return 1.5f;
            Vector3 f = Focus(), right = FlatRight(), fwd = new Vector3(-right.z, 0f, right.x);
            Fill(f, right, fwd, true);
            yield return 1.0f;
            int units0 = Units();
            Check(units0 >= MinUnits, "the field holds " + MinUnits + " units or more at the start (" + units0 + ": every robot of a squad counted, as the counts plate)");
            Pick();
            var s = runner.Sampler;
            int rebuilds0 = OzUeStatsRoot.Rebuilds, budget0 = OzUeDraw.DashBudgetFrames;
            s.Begin();
            float t0 = Time.realtimeSinceStartup, nextHover = 0f, nextFill = 15f, nextWindow = Window;
            int refills = 0, fewest = units0, hovered = 0;
            s.StartWindow(units0);
            while (true)
            {
                float t = Time.realtimeSinceStartup - t0;
                if (t >= nextWindow) { s.EndWindow(); int u = Units(); fewest = Mathf.Min(fewest, u); if (t >= Seconds - 0.01f) break; s.StartWindow(u); nextWindow += Window; }
                if (t >= nextHover) { nextHover = t + 1f; if (HoverEnemy()) hovered++; if (OzUePick.Picked.Count == 0) Pick(); }
                if (t >= nextFill)
                {
                    nextFill = t + 15f;
                    if (Units() < MinUnits) { s.Exclude(0.6f); Fill(f, right, fwd, false); refills++; }
                }
                yield return 1;
            }
            s.End();
            // ------------------------------------------------------------------ the lines
            foreach (var wl in s.Lines()) Info(wl);
            var first = s.Windows[0]; var last = s.Windows[s.Windows.Count - 1]; var second = s.Windows.Count > 1 ? s.Windows[1] : first;
            Info("refills " + refills + " (SAMPLE FIGHT again when the fight took the field under " + MinUnits + "), fewest units at a window's end " + fewest
                 + ", the pointer on an enemy in " + hovered + " of " + Mathf.CeilToInt(Seconds) + " seconds, allocation measured by " + s.AllocSource);
            Check(s.LongestFrame <= MaxFrame, "no frame longer than " + Ms(MaxFrame) + " in " + Seconds.ToString("0", CultureInfo.InvariantCulture) + " s (the longest " + Ms(s.LongestFrame) + " at "
                  + s.LongestAt.ToString("0.0", CultureInfo.InvariantCulture) + " s; " + s.ExcludedFrames + " frames that placed a new fight left out)");
            Check(last.Mean <= first.Mean * 1.25f + 0.005f, "the frame time stays flat: the last window's mean " + Ms(last.Mean) + " <= 1.25 x the first window's " + Ms(first.Mean) + " + 5 ms");
            Check(last.Fps >= first.Fps * 0.8f - 1f, "the frames per second stay flat: the last window " + F1(last.Fps) + " >= 0.8 x the first window " + F1(first.Fps) + " - 1");
            Check(last.PanelObjects <= second.PanelObjects + 16, "no growth of the panels' UI objects on the HUD canvas: " + first.PanelObjects + " in the first window, " + second.PanelObjects + " in the second, "
                  + last.PanelObjects + " in the last");
            int poolBound = Mathf.Max(second.fieldObjects, 8 * s.MostLabels + 4 * s.MostBars + 160);
            Check(last.fieldObjects <= poolBound, "the field layer's pools never pass what one frame needed: " + last.fieldObjects + " objects at the end <= the second window's " + second.fieldObjects
                  + " or 8 x the most labels of one frame (" + s.MostLabels + ") + 4 x the most bars (" + s.MostBars + ") + 160 (the floor's labels, the hint)");
            Check(last.tipRegs <= second.tipRegs, "no growth of the stats box's tooltip registrations: " + first.tipRegs + " / " + second.tipRegs + " / " + last.tipRegs);
            float early = Mathf.Max(second.AllocMean, s.Windows.Count > 2 ? s.Windows[2].AllocMean : 0f);                       // the second and third windows: after the first fill settled
            Check(last.AllocMean <= early * 1.5f + 4096f, "the managed allocation per frame does not grow: " + Kb(first.AllocMean) + " / " + Kb(second.AllocMean) + " / " + Kb(last.AllocMean)
                  + " a frame (first, second, last window; the last at most 1.5 x the second or third + 4 KB)");
            Check(s.DashOver == 0, "the dash walk stays inside its bound in every frame (" + s.DashOver + " over; the most steps of one frame " + s.DashMost + ", its bound " + s.DashMostBound + ")");
            Check(OzUeDraw.DashBudgetFrames == budget0, "no frame ran out of the dash walk's step budget (" + (OzUeDraw.DashBudgetFrames - budget0) + " frames)");
            Check(OzUeStatsRoot.Rebuilds == rebuilds0, "the stats box and the table were never rebuilt during the fight (" + (OzUeStatsRoot.Rebuilds - rebuilds0) + " rebuilds)");
            OzUePick.Unpick(); OzUePlayground.ClearField();
            yield return 1.0f;
        }

        // ================================================================== the field
        static void Fill(Vector3 f, Vector3 right, Vector3 fwd, bool first)
        {
            OzUePlayground.SampleFight();
            OzUePlayground.Drop("grunt", OzUeKeys.ShownBlue, f - right * 22f, 5);
            OzUePlayground.Drop("grunt", OzUeKeys.ShownRed, f + right * 22f, 5);
            OzUePlayground.Drop("psycho", OzUeKeys.ShownBlue, f - right * 30f + fwd * 8f, 5);
            OzUePlayground.Drop("pyro", OzUeKeys.ShownRed, f + right * 30f + fwd * 8f, 5);
            if (first)
            {
                OzUePlayground.Drop("light", Team.Neutral, f + fwd * 26f, 3);                                 // empty: their dashed rings every frame
                OzUePlayground.Drop("gatling", Team.Neutral, f - fwd * 26f, 3);
                OzUePlayground.Drop(OzUePlayground.Flag, Team.Neutral, f - fwd * 18f - right * 10f, 1);
                OzUePlayground.Drop(OzUePlayground.Flag, Team.Neutral, f - fwd * 18f + right * 10f, 1);
            }
            for (int i = 0; i < 4 && Units() < MinUnits; i++) OzUePlayground.SampleFight();
        }

        static int Units() => OzUePlayground.CountUnits(Team.Red) + OzUePlayground.CountUnits(Team.Blue);

        /// <summary>One live unit of the side drawn blue picked (its range ring, its name with HP, the blocked lines).</summary>
        static void Pick()
        {
            foreach (var o in OzUePlayground.Placed())
                if (o.IsUnit && !o.IsMinion && !Gone(o) && o.Owner == OzUeKeys.ShownBlue && o.Damage > 0) { OzUePick.Pick(new List<ZObject> { o }, false); return; }
        }

        /// <summary>The pointer on a live unit of the side drawn red (its hover card and range ring). False when none is on the screen.</summary>
        static bool HoverEnemy()
        {
            var ed = OzUnitEditor.I; if (!ed || !ed.Input) return false;
            foreach (var o in OzUePlayground.Placed())
            {
                if (!o.IsUnit || o.IsMinion || Gone(o) || o.Owner != OzUeKeys.ShownRed) continue;
                var r = ed.Input.ScreenRect(o); if (r.width <= 0f) continue;
                var p = r.center;
                var ff = OzUeInsets.FreeFieldScreen;
                if (ff.width > 4f && !ff.Contains(p)) continue;
                var m = Mouse.current; if (m == null) return false;
                InputSystem.QueueStateEvent(m, new MouseState { position = p });
                return true;
            }
            return false;
        }

        static Vector3 Focus()
        {
            var ed = OzUnitEditor.I;
            Vector3 f = ed && ed.Rts ? ed.Rts.focus : new Vector3(160, 0, -100);
            var map = ed && ed.World != null ? ed.World.Map : null;
            if (map != null) { f.x = Mathf.Clamp(f.x, 40f, map.width * 2f - 40f); f.z = Mathf.Clamp(f.z, -(map.height * 2f - 40f), -40f); }
            f.y = 0f; return f;
        }

        static Vector3 FlatRight()
        {
            var cam = OzUeDraw.Cam; Vector3 r = cam ? cam.transform.right : Vector3.right; r.y = 0f;
            return r.sqrMagnitude > 1e-4f ? r.normalized : Vector3.right;
        }

        static bool Gone(ZObject o) => o == null || o.Destroyed || o.KillAt >= 0;
        static string Ms(float s) => (s * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms";
        static string F1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        static string Kb(float b) => (b / 1024f).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
    }

    /// <summary>The frame-cost check's per-frame sampler (after every editor part drew its frame: order 2000).</summary>
    [DefaultExecutionOrder(2000)]
    internal sealed class OzUeStallSampler : MonoBehaviour
    {
        internal sealed class Win
        {
            public float from, to; public int frames, unitsAtStart, uiObjects, fieldObjects, tipRegs, dashMost, strokes, labels, bars, vertices; public float sum, longest; public double alloc; public int allocFrames;
            public int PanelObjects => uiObjects - fieldObjects;
            public float Mean => frames > 0 ? sum / frames : 0f;
            public float Fps => to > from ? frames / (to - from) : 0f;
            public float AllocMean => allocFrames > 0 ? (float)(alloc / allocFrames) : 0f;
            public float Longest => longest;
        }
        internal readonly List<Win> Windows = new List<Win>();
        internal float LongestFrame, LongestAt; internal int ExcludedFrames, DashOver, DashMost, DashMostBound, MostLabels, MostBars;
        internal string AllocSource = "none";
        Win cur; bool on; float t0, excludeUntil, nextObjects; long lastMem = -1; bool objectsFrame;
        ProfilerRecorder gcRec;

        internal void Begin()
        {
            Windows.Clear(); LongestFrame = LongestAt = 0f; ExcludedFrames = DashOver = DashMost = DashMostBound = 0;            // the label / bar peaks stay for the whole run (the pools do)
            try { gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"); } catch (Exception) { }
            AllocSource = gcRec.Valid ? "the profiler counter GC Allocated In Frame" : "the managed heap's growth between frames (frames with a collection left out)";
            lastMem = GC.GetTotalMemory(false);
            t0 = Time.realtimeSinceStartup; on = true; nextObjects = 0f;
        }

        internal void End() { on = false; if (gcRec.Valid) gcRec.Dispose(); }
        internal void ResetPeaks() { MostLabels = MostBars = 0; }
        internal void Exclude(float seconds) { excludeUntil = Time.realtimeSinceStartup + seconds; }

        internal void StartWindow(int units)
        {
            float t = Time.realtimeSinceStartup - t0;
            cur = new Win { from = t, unitsAtStart = units };
            Windows.Add(cur);
            nextObjects = t;                                                                               // the UI objects counted at once
        }

        internal void EndWindow() { if (cur != null) cur.to = Time.realtimeSinceStartup - t0; cur = null; }

        void LateUpdate()
        {
            if (!on || cur == null) return;
            float now = Time.realtimeSinceStartup, t = now - t0, dt = Time.unscaledDeltaTime;
            bool excluded = now < excludeUntil || objectsFrame;
            objectsFrame = false;
            cur.frames++; cur.sum += dt;
            if (excluded) ExcludedFrames++;
            else
            {
                if (dt > cur.longest) cur.longest = dt;
                if (dt > LongestFrame) { LongestFrame = dt; LongestAt = t; }
                double a = -1;
                if (gcRec.Valid) a = gcRec.LastValue;
                else { long m = GC.GetTotalMemory(false); if (lastMem >= 0 && m >= lastMem) a = m - lastMem; lastMem = m; }
                if (a >= 0) { cur.alloc += a; cur.allocFrames++; }
            }
            if (gcRec.Valid == false) lastMem = GC.GetTotalMemory(false);
            int steps = OzUeDraw.LastDashSteps, bound = OzUeDraw.LastDashBound;
            if (steps > bound) DashOver++;
            if (steps > DashMost) { DashMost = steps; DashMostBound = bound; }
            if (steps > cur.dashMost) cur.dashMost = steps;
            cur.strokes = Mathf.Max(cur.strokes, OzUeDraw.LastStrokes); cur.labels = Mathf.Max(cur.labels, OzUeDraw.LastLabels); cur.bars = Mathf.Max(cur.bars, OzUeDraw.LastBars);
            cur.vertices = Mathf.Max(cur.vertices, OzUeDraw.LastVertices);
            MostLabels = Mathf.Max(MostLabels, OzUeDraw.LastLabels); MostBars = Mathf.Max(MostBars, OzUeDraw.LastBars);
            if (t >= nextObjects)
            {
                nextObjects = t + 1f;
                var hud = OpenZHud.I;
                if (hud && hud.Root) cur.uiObjects = hud.Root.GetComponentsInChildren<Transform>(true).Length;      // counted once a second (that frame's allocation left out)
                var field = OzUnitEditor.Layer(UeLayer.Field);
                cur.fieldObjects = field ? field.GetComponentsInChildren<Transform>(true).Length : 0;            // the world labels' and bars' pools, the floor's labels, the hint
                cur.tipRegs = OzUeSideTip.Registered;
                objectsFrame = true;
            }
        }

        internal IEnumerable<string> Lines()
        {
            foreach (var w in Windows)
                yield return w.from.ToString("0", CultureInfo.InvariantCulture) + "-" + w.to.ToString("0", CultureInfo.InvariantCulture) + " s: " + w.frames + " frames, "
                    + w.Fps.ToString("0.0", CultureInfo.InvariantCulture) + " fps, mean " + (w.Mean * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms, longest "
                    + (w.Longest * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms; units " + w.unitsAtStart + " at the start; UI objects " + w.uiObjects + " (panels " + w.PanelObjects + ", field layer " + w.fieldObjects + "), tooltip registrations "
                    + w.tipRegs + "; allocated " + (w.AllocMean / 1024f).ToString("0.0", CultureInfo.InvariantCulture) + " KB a frame; dash steps up to " + w.dashMost + "; strokes up to "
                    + w.strokes + ", labels up to " + w.labels + ", bars up to " + w.bars + ", mark mesh up to " + w.vertices + " vertices";
        }

        void OnDestroy() { if (gcRec.Valid) gcRec.Dispose(); }
    }

    /// <summary>Steps the frame-cost check once a frame and survives a scene load (as the Play proof's runner). A step yields null / an
    /// int (frames), a float (seconds of real time) or another IEnumerator (run first).</summary>
    internal sealed class OzUeStallRunner : MonoBehaviour
    {
        readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        int waitFrame; float waitUntil; bool stopped;
        internal OzUeStallSampler Sampler { get; private set; }
        void Awake() { Sampler = gameObject.AddComponent<OzUeStallSampler>(); }
        internal void Push(IEnumerator e) { stack.Push(e); }
        internal void Stop() { stopped = true; stack.Clear(); }

        void Update()
        {
            if (stopped) return;
            if (stack.Count == 0) { stopped = true; return; }
            if (Time.frameCount < waitFrame || Time.realtimeSinceStartup < waitUntil) return;
            var top = stack.Peek(); bool more;
            try { more = top.MoveNext(); }
            catch (Exception e) { Debug.LogException(e); stopped = true; stack.Clear(); OzUeStallCheck.Crashed(e); return; }
            if (stopped) return;
            if (!more) { stack.Pop(); return; }
            var c = top.Current;
            if (c is IEnumerator child) stack.Push(child);
            else if (c is float s) waitUntil = Time.realtimeSinceStartup + s;
            else waitFrame = Time.frameCount + (c is int n ? Mathf.Max(1, n) : 1);
        }
    }
}
#endif
