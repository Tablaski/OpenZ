// OpenZ (GPL-3.0) - version 1.4 kit fog_gameplay_only: the fog gate's checks in the editor. Gate Count (headless): the plain rows of
// Editor/OzV14FogGameplayCount.cs on the project's own sources. Play Check (in Play, a level as a human): with the fog ON, every scene it can
// start, measured frame by frame, and real game frames of each for the owner.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace OpenZ
{
    /// <summary>
    /// outros). Logs next to Assets: Logs/oz_v14_fog_gameplay_only_count.txt (the count); the Play check per level and side played, so a run
    /// on another level or side never overwrites one: Logs/oz_v14_fog_gameplay_only_&lt;level&gt;_&lt;side&gt;.txt (e.g. _L16_red), frames in
    /// Logs/fog_gameplay_only_frames/&lt;level&gt;_&lt;side&gt;/.
    /// <list type="bullet">
    /// <item>Gate Count (no Play): the gate's answer for every state found, the fade, whole scenes frame by frame on the live clock, the
    /// sources (the reader reads no gameplay state, the off path returns first, every output reads the gate), the scenes with no fog component.</item>
    /// <item>Play Check (in Play on a level as a human; about 4-5 minutes; reloads the level twice; Zod's own idle barb is switched off for
    /// the run and put back): the fog switched on, then G2 gameplay, G3 the pause menu (the fog stays), G4 Commander Zod's idle barb (the
    /// switch OzFowGate.CornerWindowKeepsFog true: gameplay, the fog stays; false: the fog fades out and back) or, on a level played as blue
    /// (no Zod there), a blue scene's window over the match (OzBlueCameo.DebugNuke: the fade out, the fold, the fade back), G5 a mid-match
    /// hard cut (ZodCinePreview), G6 a takeover begun late in the frame that keeps the RTS camera on (made up: the sim switched off at the
    /// render loop's start), G7 a mid-match briefing, G8 the win ending (and the L16 / L07 ending scene), G9 the level-start briefing after a
    /// reload with OPTIONS FOG OF WAR ON (and its hand-back), G10 the loss ending (and the L16 ending scene), G11 a reload with FOG OF WAR OFF
    /// (no fog anywhere), G12 the new gate closed wherever the gate before the kit was (GateWhyBefore, every sampled frame). Each frame sampled
    /// at the end of the game view's drawing: the fog's strength, the passes put into the player's camera, the units held off, the covered
    /// enemy itself (in that camera's view by a frustum test that ignores forceRenderingOff, and drawn or not there), Covers on it, the
    /// minimap cover, the briefing and scene windows. A scene's rows: the drawn fog is 0 within 0.4 s of a blend and 0 on a hard cut's first
    /// frame, no unit is held off and Covers is no from its first frame, the minimap shows no fog, the scene's window has folded before the
    /// fog comes back, and the fog is back within 0.4 s once the player has the camera, with the units held off and Covers on again from the
    /// first frame of play (holdoff and covers changes: the covered enemy never drawn under the thin cloud of the fade in, no bar or marker
    /// over it, no hover, click or attack order on it). The fade's time is counted as the game counts it (a frame at most 0.05 s). The camera
    /// is put on the covered enemy before each row and on the first frame of play after each scene (a jump, as a minimap click). The covered
    /// enemy itself has its own rows (G2p .. G10p), counted only on frames of play with it in the camera's view: SKIPPED, not PASS, when
    /// there was no such frame.</item>
    /// <item>The verdict: PASS only with no FAIL and no SKIP; with a SKIP it is INCOMPLETE (a scene was not checked).</item>
    /// </list>
    /// </summary>
    public static class OzV14FogGameplayCheck
    {
        const string Tag = "[OpenZ V1.4 FogGameplayOnly]";
        public const string PlayLogBase = "oz_v14_fog_gameplay_only", CountLog = "oz_v14_fog_gameplay_only_count.txt", FramesDir = "Logs/fog_gameplay_only_frames";
        /// <summary>One Play check run's name in its file names: the level and the side played (e.g. L16_red, L07_blue).</summary>
        public static string RunName(string level, Team side)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in string.IsNullOrEmpty(level) ? "level" : level) sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return sb.Append('_').Append(side.ToString().ToLowerInvariant()).ToString();
        }
        /// <summary>The Play check's log for one level and side: Logs/oz_v14_fog_gameplay_only_&lt;level&gt;_&lt;side&gt;.txt.</summary>
        public static string PlayLogFor(string level, Team side) => PlayLogBase + "_" + RunName(level, side) + ".txt";
        static readonly List<string> lines = new List<string>();
        static readonly List<string> skipped = new List<string>();
        static int pass, fail, skip;

        static void Begin(string what) { lines.Clear(); skipped.Clear(); pass = fail = skip = 0; Say($"{Tag} {what}"); }
        static void Say(string s) { lines.Add(s); Debug.Log(s); }
        static void Row(string id, bool ok, string text) { if (ok) pass++; else fail++; Say($"  {(ok ? "PASS" : "FAIL")} {id} {text}"); }
        static void Skip(string id, string text) { skip++; skipped.Add(id); Say($"  SKIP {id} {text}"); }
        static void End(string file)
        {
            string verdict = fail > 0 || pass == 0 ? "FAIL" : skip > 0 ? "INCOMPLETE" : "PASS";
            Say($"{Tag} {verdict} {pass}/{pass + fail}{(skip > 0 ? $" ({skip} skipped: {string.Join(", ", skipped)}; a skipped scene was NOT checked)" : "")}");
            try
            {
                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, file), string.Join("\n", lines) + "\n");
            }
            catch (System.Exception e) { Debug.LogWarning($"{Tag} could not write the log file: {e.Message}"); }
        }

        // ================================================================ headless
        [MenuItem("OpenZ/V1.4/Fog Gameplay Only: Gate Count (headless)")]
        public static void GateCount()
        {
            Begin("Gate Count (headless): the gate's answer for every state found, the fade, whole scenes, the sources, the scenes with no fog");
            var src = new Dictionary<string, string>();
            foreach (var p in Directory.GetFiles(Path.Combine(Application.dataPath, "OpenZ/Scripts/View"), "OzFogOfWar*.cs"))
                if (p.EndsWith(".cs")) src[Path.GetFileName(p)] = File.ReadAllText(p);
            OzV14FogGameplayCount.Run(Row, Say, src, Application.dataPath);
            Say("  the decision table (id;answer;reason):");
            foreach (var l in OzV14FogGameplayCount.LastTable.Split('\n')) if (l.Length > 0) Say("    " + l);
            End(CountLog);
        }

        // ================================================================ Play: the runner
        static IEnumerator running;
        static readonly Stack<IEnumerator> nest = new Stack<IEnumerator>();
        static void Run(IEnumerator it)
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning($"{Tag} press Play, start a level as a human, then run this again"); return; }
            if (running != null) { Debug.LogWarning($"{Tag} the check is already running"); return; }
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
            catch (System.Exception e) { Debug.LogError($"{Tag} the check stopped: {e}"); more = false; }
            if (more) return;
            EditorApplication.update -= Step;
            while (nest.Count > 0) { var it = nest.Pop(); try { (it as System.IDisposable)?.Dispose(); } catch (System.Exception) { } }   // the routine's finally: settings put back
            running = null;
        }
        static IEnumerator Frames(int n) { int f = Time.frameCount + n; while (Time.frameCount < f) yield return null; }
        static IEnumerator Seconds(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
        /// <summary>A game view capture, held until its file is written and stable (at most 6 s).</summary>
        static IEnumerator Shot(string path)
        {
            var asked = System.DateTime.UtcNow.AddSeconds(-1);
            ScreenCapture.CaptureScreenshot(path);
            float until = Time.realtimeSinceStartup + 6f; long last = -1;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                var fi = new FileInfo(path);
                if (!fi.Exists || fi.LastWriteTimeUtc < asked) continue;
                if (fi.Length > 0 && fi.Length == last) { shots.Add(Path.GetFileName(path)); yield break; }
                last = fi.Length;
            }
            Debug.LogWarning($"{Tag} capture not written in 6 s: {path}");
        }
        static readonly List<string> shots = new List<string>();

        // ================================================================ Play: the frame sampler (the end of the game view's drawing)
        struct Smp
        {
            public int frame, queued, hidden, outside; public float t, strength, miniA;
            public bool wanted, open, cut, hiding, hold, covers, probeIn, mini, holding, fly, decided, briefing, corner, bwin, probeMeasured, probeInView, probeHeld; public string why;
        }
        static readonly List<Smp> smp = new List<Smp>(8192);
        static bool recording; static int lastQueued, lateFrame = -1; static ZObject probe;
        static int oldFrames, oldBad; static string oldBadAt;                             // the gate before the kit closed, the new one open (must stay 0)
        static System.Action lateStart;                                                  // armed: runs once at the start of the game view's next drawing

        static void Hook(bool on)
        {
            RenderPipelineManager.endContextRendering -= OnEnd; RenderPipelineManager.beginContextRendering -= OnBegin; RenderPipelineManager.beginCameraRendering -= OnCam;
            hookedFor = null; probeCamFrame = -1; aimArmed = false;
            if (on) { RenderPipelineManager.endContextRendering += OnEnd; RenderPipelineManager.beginContextRendering += OnBegin; RenderPipelineManager.beginCameraRendering += OnCam; hookedFor = OzFogOfWar.I; }
        }

        // ---- the covered enemy itself at the player's camera: read after the fog's own BeginCamera (which holds the covered units off), so
        //      this handler is put behind the fog's whenever the fog component is a new one (a reload)
        static OzFogOfWar hookedFor; static int probeCamFrame = -1; static bool probeDrawnNow, probeInViewNow;
        static UnitView probeView; static ZObject probeViewOf; static Renderer[] probeR; static readonly Plane[] planes = new Plane[6];
        /// <summary>The covered enemy at the player's camera: in its view by a frustum test that ignores forceRenderingOff (a renderer of it
        /// the camera draws: enabled, active, on a layer of the camera's mask, its bounds in the frustum), and drawn (one of those not held off).</summary>
        static void OnCam(ScriptableRenderContext ctx, Camera cam)
        {
            if (!recording || probe == null || !cam || cam.cameraType != CameraType.Game || (cam.targetTexture != null && !cam.CompareTag("MainCamera"))
                || !cam.TryGetComponent<RtsCamera>(out var rts) || !rts.enabled) return;
            if (probeViewOf != probe || !probeView)
            {
                probeViewOf = probe; probeView = null; probeR = null; var g = ZGame.I;
                if (g) foreach (var v in g.LiveViewValues) if (v && v.Obj == probe) { probeView = v; probeR = v.GetComponentsInChildren<Renderer>(true); break; }
            }
            if (!probeView || probeR == null || probeR.Length == 0) return;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            bool inView = false, drawn = false;                                           // forceRenderingOff is NOT part of the view test: held off and out of view are told apart
            foreach (var r in probeR)
            {
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy || (cam.cullingMask & (1 << r.gameObject.layer)) == 0 || !GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                inView = true;
                if (!r.forceRenderingOff) drawn = true;
            }
            probeCamFrame = Time.frameCount; probeInViewNow = inView; probeDrawnNow = drawn;
        }

        // ---- the camera on the covered enemy: the probe rows count only frames of play with it in the player's camera's view
        static readonly Plane[] aimPlanes = new Plane[6];
        static bool aimArmed; static int aims;
        static RtsCamera Rts() { var m = Camera.main; return m && m.TryGetComponent<RtsCamera>(out var r) ? r : UnityEngine.Object.FindFirstObjectByType<RtsCamera>(); }
        static UnitView ViewOf(ZObject o) { var g = ZGame.I; if (g && o != null) foreach (var v in g.LiveViewValues) if (v && v.Obj == o) return v; return null; }
        /// <summary>The unit stands in covered ground (not revealed), alive.</summary>
        static bool InCover(ZObject o) { var m = OzFogOfWar.Mask; return o != null && !o.Destroyed && o.KillAt < 0 && m != null && m.ShownAtPx(o.CenterX, o.CenterY) < OzFowMask.Revealed && OzFogOfWar.HeldUnder(o); }   // 1.4 mayhem_headstart: not in the early band (drawn under the soft edge)
        static bool Sees(Camera cam, Vector3 p) { GeometryUtility.CalculateFrustumPlanes(cam, aimPlanes); return GeometryUtility.TestPlanesAABB(aimPlanes, new Bounds(p + Vector3.up, new Vector3(3f, 3f, 3f))); }
        /// <summary>The player's camera put on the covered enemy when it is not in the view (a jump to it, as a minimap click without the
        /// fly: gameplay, the gate stays open). Only while the RTS camera is the player's (on, the ending not holding it).</summary>
        static void AimIfOut()
        {
            var rts = Rts(); var cam = rts ? rts.GetComponent<Camera>() : null; var v = ViewOf(probe);
            if (!rts || !rts.isActiveAndEnabled || !cam || !v || RtsCamera.InputLocked || Sees(cam, v.transform.position)) return;
            rts.focus = new Vector3(v.transform.position.x, 0f, v.transform.position.z); rts.Place(); aims++;
        }
        /// <summary>The covered enemy for the next rows: kept while it still stands in covered ground, else one in the player's camera's view,
        /// else the one nearest to the view's centre; then the camera put on it when it is out of the view.</summary>
        static void ProbeInView(ZGame g)
        {
            if (!InCover(probe)) probe = Covered(g);
            if (probe != null) AimIfOut();
        }
        static bool BlueWin() { var b = OzBlueCameo.I; return b && b.Comms != null && b.Comms.Visible; }
        static bool HasGame(List<Camera> cams) { if (cams != null) foreach (var c in cams) if (c && c.cameraType == CameraType.Game) return true; return false; }
        static void OnBegin(ScriptableRenderContext ctx, List<Camera> cams)
        {
            if (recording && probe != null && HasGame(cams))
            {   // the first frame of play after a scene (a flight hands the camera back at the level's start pose): the camera put on the
                // covered enemy before this frame is drawn, so the fade in's frames measure it in view (before the fog's own BeginCamera)
                bool open = OzFogOfWar.GameplayView;
                if (!open && OzFogOfWar.Wanted) aimArmed = true;
                else if (open && aimArmed) { aimArmed = false; AimIfOut(); }
            }
            if (lateStart == null || !HasGame(cams)) return;
            var a = lateStart; lateStart = null; lateFrame = Time.frameCount; a();
        }
        static void OnEnd(ScriptableRenderContext ctx, List<Camera> cams)
        {
            if (OzFogOfWar.I != hookedFor)                                                // a new fog component (a reload): OnCam behind its BeginCamera again
            {
                RenderPipelineManager.beginCameraRendering -= OnCam; RenderPipelineManager.beginCameraRendering += OnCam;
                hookedFor = OzFogOfWar.I; probeCamFrame = -1;
            }
            if (!recording || !HasGame(cams)) return;
            int q = OzFogOfWar.PassesQueued, dq = q - lastQueued; lastQueued = q;
            bool measured = probe != null && probeCamFrame == Time.frameCount;
            var mc = OzFogOfWar.MinimapCover; var g = ZGame.I; var W = g ? g.World : null; var mask = OzFogOfWar.Mask;
            var s = new Smp
            {
                frame = Time.frameCount, t = Time.unscaledTime, strength = OzFogOfWar.Strength, queued = dq, hidden = OzFogOfWar.HiddenViews, outside = OzFogOfWar.HiddenSkippedOutside,
                wanted = OzFogOfWar.Wanted, open = OzFogOfWar.GameplayView, cut = OzFogOfWar.GateCut, hiding = OzFogOfWar.Hiding, hold = OzFogOfWar.HoldingOff,
                covers = probe != null && OzFogOfWar.Covers(probe), mini = mc && mc.enabled, miniA = mc ? mc.color.a : 0f,
                probeIn = probe != null && !probe.Destroyed && mask != null && mask.ShownAtPx(probe.CenterX, probe.CenterY) < OzFowMask.Revealed,
                holding = ZodBriefing.EndingHolding, fly = ZodFlyover.Live != null && ZodFlyover.Live.Active, decided = W != null && PlayerInput.MatchDecided(W),
                briefing = ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive, corner = ZodBriefing.CommsVisible, bwin = OzBlueCameo.SceneShowing || BlueWin(), why = OzFogOfWar.GateReason,
                probeMeasured = measured, probeInView = measured && probeInViewNow, probeHeld = measured && !probeDrawnNow,
            };
            var fi = OzFogOfWar.I;
            if (fi && fi.GateWhyBefore() != null) { oldFrames++; if (s.open) { oldBad++; oldBadAt ??= $"frame {s.frame} ({fi.GateWhyBefore()})"; } }
            if (smp.Count > 0 && smp[smp.Count - 1].frame == s.frame) { s.queued += smp[smp.Count - 1].queued; smp[smp.Count - 1] = s; }   // a second drawing in one frame
            else smp.Add(s);
        }
        static void Rec() { smp.Clear(); lastQueued = OzFogOfWar.PassesQueued; aimArmed = false; recording = true; }
        static List<Smp> Stop() { recording = false; return new List<Smp>(smp); }

        // ================================================================ Play: the judges
        /// <summary>No unit held off on this frame (the gate's HoldOff off and no view held off).</summary>
        static bool NoHold(Smp x) => !x.hold && x.hidden == 0;
        static bool NoHide(Smp x) => NoHold(x) && !x.hiding && !x.covers;
        static bool NoFog(Smp x) => x.queued == 0 && x.strength <= 0.001f && NoHide(x) && !x.mini;
        /// <summary>The covered enemy itself not drawn on this frame (while it stands in covered ground and was measured at the player's camera):
        /// no renderer of it in that camera's view is drawn (every one there held off, or none there at all).</summary>
        static bool ProbeHeld(Smp x) => !x.probeIn || !x.probeMeasured || x.probeHeld;
        /// <summary>Covers answers for the covered enemy exactly where the units are held off (OzFowGate.CoversAt = HoldsOff: the gate open
        /// and the fog drawable, whatever the fade).</summary>
        static bool CoversHold(Smp x) => !x.probeIn || x.covers == x.hold;
        /// <summary>The fog as in play: the gate open, full strength, the pass, Covers, the units held off, and the covered enemy (while it
        /// still stands in covered ground) covered and held off.</summary>
        static bool FullFog(Smp x) => x.open && x.strength >= 0.999f && x.queued > 0 && x.hiding && x.hold && (!x.probeIn || x.covers) && ProbeHeld(x);
        /// <summary>The 3D view only (the minimap's UI for a frame is built before the cameras draw, so a scene begun inside the drawing
        /// clears it on the next frame).</summary>
        static bool NoFog3D(Smp x) => x.queued == 0 && x.strength <= 0.001f && NoHide(x);
        static string D(Smp x) => $"frame {x.frame}: strength {x.strength:0.000}, passes {x.queued}, covers {(x.hiding ? "on" : "off")} (the covered enemy {(x.probeIn ? (x.covers ? "covered" : "NOT covered") + (x.probeMeasured ? (x.probeInView ? ", in view" : ", out of view") + (x.probeHeld ? ", not drawn" : ", DRAWN") : ", not measured") : "-")}), "
                                + $"units held off {(x.hold ? "on" : "off")} ({x.hidden} views, {x.outside} outside the view), minimap cover {(x.mini ? "on " + x.miniA.ToString("0.00") : "off")}, gate {(x.open ? "open" : x.why + (x.cut ? " (cut)" : ""))}";
        static float MaxDt(List<Smp> s) { float m = 0f; for (int i = 1; i < s.Count; i++) m = Mathf.Max(m, s[i].t - s[i - 1].t); return m; }
        /// <summary>The fade's own time from sample a to sample b: each frame's step counted at most OzFowGate.MaxStep, as the game counts it.</summary>
        static float Span(List<Smp> s, int a, int b) { float t = 0f; for (int i = Mathf.Max(1, a + 1); i <= b && i < s.Count; i++) t += Mathf.Min(Mathf.Max(0f, s[i].t - s[i - 1].t), OzFowGate.MaxStep); return t; }
        const float FadeBound = OzFowGate.FadeSeconds + OzFowGate.MaxStep + 0.002f;    // 0.4 s of the fade's time + one frame
        static int First(List<Smp> s, int from, System.Func<Smp, bool> ok) { for (int i = Mathf.Max(0, from); i < s.Count; i++) if (ok(s[i])) return i; return -1; }
        static int Count(List<Smp> s, int a, int b, System.Func<Smp, bool> ok) { int n = 0; for (int i = Mathf.Max(0, a); i <= b && i < s.Count; i++) if (ok(s[i])) n++; return n; }

        /// <summary>A hard cut started between frames after <paramref name="fromFrame"/>: the frame before had the fog, the cut's first frame and
        /// every frame of the scene have none (no pass, strength 0, nothing held off, Covers no, no minimap cover).</summary>
        static bool JudgeCut(List<Smp> s, int fromFrame, System.Func<Smp, bool> inScene, out int lastIdx, out string text)
        {
            int a = First(s, 0, x => x.frame > fromFrame); lastIdx = -1;
            if (a < 1) { text = "no frame sampled after the start"; return false; }
            var before = s[a - 1];
            int b = a; while (b + 1 < s.Count && inScene(s[b + 1])) b++;
            lastIdx = b;
            int bad = Count(s, a, b, x => !NoFog(x)); int firstBad = First(s, a, x => !NoFog(x));
            bool ok = FullFog(before) && NoFog(s[a]) && bad == 0 && b > a;
            text = $"the frame before: {D(before)}; the cut's first frame: {D(s[a])}; the scene {b - a + 1} frames, {bad} with any fog{(bad > 0 && firstBad >= 0 && firstBad <= b ? " (first " + D(s[firstBad]) + ")" : "")}";
            return ok;
        }

        /// <summary>Back to play after a scene (from index <paramref name="from"/>): the fade in from about 0, rising only, full within 0.4 s of
        /// the fade's time (+ one frame); the units held off and Covers on from the first frame of play on, the fade in included (holdoff and
        /// covers changes: the covered enemy, when measured, not drawn on any of them, never under the thin cloud; Covers on it with the hold-off).</summary>
        static bool JudgeBack(List<Smp> s, int from, out string text)
        {
            int o = First(s, from, x => x.open);
            if (o < 0) { text = "the gate never opened again"; return false; }
            int full = First(s, o, x => x.strength >= 0.999f);
            float span = full >= 0 ? Span(s, o, full) : float.NaN;
            bool rising = true; for (int i = o + 1; full >= 0 && i <= full; i++) if (s[i].strength < s[i - 1].strength - 1e-5f) rising = false;
            int notHeld = Count(s, o, s.Count - 1, x => !x.hold || !ProbeHeld(x)), notHeldFade = full >= 0 ? Count(s, o, full - 1, x => !x.hold || !ProbeHeld(x)) : -1;
            int covBad = Count(s, o, s.Count - 1, x => !CoversHold(x)), probeFrames = Count(s, o, s.Count - 1, x => x.probeIn && x.probeInView);
            int probeFade = full >= 0 ? Count(s, o, full - 1, x => x.probeIn && x.probeInView) : 0;
            bool ok = s[o].strength < 0.5f && full >= 0 && span <= FadeBound && rising && notHeld == 0 && covBad == 0;
            text = $"the first frame of play: {D(s[o])}; full fog {(full >= 0 ? $"{span:0.000} s of the fade's time later ({s[full].t - s[o].t:0.000} s real; bound {FadeBound:0.000})" : "NEVER")}, rising only {rising}; "
                 + $"from the first frame of play on, frames without the hiding (or the covered enemy drawn): {notHeld} ({notHeldFade} of them in the fade in; {probeFrames} frames with the covered enemy in the camera's view, {probeFade} in the fade in); "
                 + $"frames where Covers on the covered enemy did not follow the hold-off: {covBad}";
            return ok;
        }

        /// <summary>The covered enemy's own row on samples <paramref name="a"/> .. <paramref name="b"/>, counted on the frames of play (the gate
        /// open) with it in the player's camera's view (the frustum test, forceRenderingOff ignored): it is not drawn on any of them (held off,
        /// the fade in included) and Covers on it is on with the hold-off (and off with it in a scene) on every frame. SKIPPED, never PASS,
        /// when there was no frame of play with it in the view.</summary>
        static void ProbeRow(string id, string what, List<Smp> s, int a, int b)
        {
            int play = Count(s, a, b, x => x.open && x.probeIn && x.probeInView);
            int meas = Count(s, a, b, x => x.probeIn && x.probeMeasured);
            if (probe == null || play == 0)
            {
                Skip(id, $"{what}: {(probe == null ? "no enemy unit in covered ground" : $"the covered enemy {probe.Type} #{probe.Id} was in the player's camera's view on no frame of play here (measured on {meas} frames; killed, out of covered ground, or the camera not on it)")}: "
                         + "the covered enemy itself NOT checked (drop an enemy unit in covered ground with Alt+P, close the panel, run again)");
                return;
            }
            int drawn = Count(s, a, b, x => x.open && x.probeIn && x.probeInView && !x.probeHeld);
            int fade = Count(s, a, b, x => x.open && x.strength < 0.999f && x.probeIn && x.probeInView), fadeDrawn = Count(s, a, b, x => x.open && x.strength < 0.999f && x.probeIn && x.probeInView && !x.probeHeld);
            int covBad = Count(s, a, b, x => !CoversHold(x));
            Row(id, drawn == 0 && covBad == 0, $"{what}, the covered enemy itself ({probe.Type} #{probe.Id}): measured at the player's camera on {meas} frames, in its view on {play} frames of play ({fade} in a fade in); "
                + $"drawn on {drawn} of them ({fadeDrawn} in a fade in); Covers on it not following the hold-off on {covBad} frames");
        }

        /// <summary>A scene that blends in (the RTS camera stays on), from index <paramref name="from"/>: from the gate's first closed frame and
        /// through the scene (while <paramref name="inScene"/>) no unit held off and Covers no (the units show); the drawn fog
        /// falls only, to 0 within 0.4 s of the fade's time (+ one frame); after that no pass and no minimap cover.</summary>
        static bool JudgeBlend(List<Smp> s, int from, System.Func<Smp, bool> inScene, out int closeIdx, out int lastIdx, out string text)
        {
            int c = First(s, from, x => x.wanted && !x.open); closeIdx = c; lastIdx = -1;
            if (c < 1) { text = "the gate never closed (or was closed from the start)"; return false; }
            int e = c; while (e + 1 < s.Count && inScene(s[e + 1])) e++;
            lastIdx = e;
            int z = First(s, c, x => x.strength <= 0.001f); if (z > e) z = -1;
            float span = z >= 0 ? Span(s, c, z) : float.NaN;
            bool falling = true; for (int i = c + 1; z >= 0 && i <= z; i++) if (s[i].strength > s[i - 1].strength + 1e-5f) falling = false;
            int held = Count(s, c, e, x => !NoHold(x)), covBad = Count(s, c, e, x => x.hiding || !CoversHold(x)), after = z >= 0 ? Count(s, z, e, x => x.queued > 0 || x.mini || x.hiding) : -1;
            bool soft = !s[c].cut;
            bool ok = FullFog(s[c - 1]) && held == 0 && covBad == 0 && z >= 0 && span <= FadeBound && falling && after == 0
                      && (soft ? s[c].strength >= 0.85f && s[c].queued > 0 : s[c].strength <= 0.001f);
            text = $"the frame before: {D(s[c - 1])}; the first closed frame: {D(s[c])}; drawn fog 0 {(z >= 0 ? $"{span:0.000} s of the fade's time later ({s[z].t - s[c].t:0.000} s real; bound {FadeBound:0.000})" : "NEVER")}, "
                 + $"falling only {falling}; through the scene ({e - c + 1} frames): frames with a unit held off {held}, frames with Covers on {covBad}, "
                 + $"frames with a pass, Covers or the minimap cover after 0: {after}";
            return ok;
        }

        /// <summary>A scene's window when play comes back (fix round 2): from the scene's last frame <paramref name="last"/> (its last briefing or
        /// flyover frame) to the gate's open, no fog at all while the window folds, and no briefing or scene window on screen at the open.</summary>
        static bool WindowGone(List<Smp> s, int last, out string text)
        {
            int o = last >= 0 ? First(s, last + 1, x => x.open) : -1;
            if (o < 0) { text = "the gate never opened after the scene"; return false; }
            int fold = Count(s, last + 1, o - 1, x => x.corner || x.bwin), bad = Count(s, last + 1, o - 1, x => !NoFog(x));
            bool up = s[o].corner || s[o].bwin;
            text = $"{o - last - 1} frames from the scene's last frame to the gate's open ({fold} with its window still folding, {bad} with any fog); a window still on screen at the open: {(up ? "YES" : "no")}";
            return !up && bad == 0;
        }
        static int LastWhere(List<Smp> s, System.Func<Smp, bool> ok) { int k = -1; for (int i = 0; i < s.Count; i++) if (ok(s[i])) k = i; return k; }

        /// <summary>An enemy unit in covered ground: one in the player's camera's view when there is one, else the one nearest to the view's
        /// centre (the camera is then put on it: <see cref="AimIfOut"/>).</summary>
        static ZObject Covered(ZGame g)
        {
            if (!g || OzFogOfWar.Mask == null) return null;
            var rts = Rts(); var cam = rts ? rts.GetComponent<Camera>() : null; Vector3 at = rts ? rts.focus : Vector3.zero;
            ZObject near = null; float best = float.MaxValue;
            foreach (var v in g.LiveViewValues)
            {
                var o = v ? v.Obj : null; if (o == null || o.Owner == g.player || !o.IsUnit || !InCover(o)) continue;
                var p = v.transform.position;
                if (cam && cam.isActiveAndEnabled && Sees(cam, p)) return o;
                float d = (p.x - at.x) * (p.x - at.x) + (p.z - at.z) * (p.z - at.z); if (d < best) { best = d; near = o; }
            }
            return near;
        }

        /// <summary>Any briefing or flyover skipped (as the SPACE key), then until the player has the camera and no transmission window is up
        /// (at most <paramref name="max"/> s).</summary>
        static IEnumerator ToPlay(float max)
        {
            float t0 = Time.realtimeSinceStartup, calm = -1f;
            while (Time.realtimeSinceStartup - t0 < max)
            {
                bool brief = ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive, fly = ZodFlyover.Live != null && ZodFlyover.Live.Active;
                bool window = ZodBriefing.CommsVisible || OzBlueCameo.SceneShowing || BlueWin();
                if (brief && Time.realtimeSinceStartup - t0 > 1f) { ZodBriefing.ProofSkipBriefing(); OzBlueCameo.ProofSkipBriefing(); }
                if (!brief && !fly && !window && ZodCinePreview.I == null) { if (calm < 0f) calm = Time.realtimeSinceStartup; if (Time.realtimeSinceStartup - calm > 0.5f) yield break; }
                else calm = -1f;
                yield return null;
            }
        }

        static IEnumerator LoadLevel(string scene, float max)
        {
            var old = ZGame.I;
            SceneManager.LoadScene(scene);
            float t0 = Time.realtimeSinceStartup;
            while ((!ZGame.I || ZGame.I == old || ZGame.I.World == null || !OzFogOfWar.I) && Time.realtimeSinceStartup - t0 < max) yield return null;
        }

        // ================================================================ Play: the check
        [MenuItem("OpenZ/V1.4/Fog Gameplay Only: Play Check (in Play, a level as a human)")]
        public static void PlayCheck() => Run(PlayRoutine());

        static IEnumerator PlayRoutine()
        {
            Begin("Play Check (a level as a human, the fog ON): the fog only while the player plays");
            shots.Clear(); oldFrames = oldBad = 0; oldBadAt = null; aims = 0;
            var g = ZGame.I; var f = OzFogOfWar.I;
            if (!g || g.World == null || !f) { Row("G0", false, "no level running (ZGame / OzFogOfWar missing): start a level as a human, then run this again"); End(PlayLogBase + "_no_level.txt"); yield break; }
            string scene = g.gameObject.scene.name, level = g.level;
            string run = RunName(level, g.player), log = PlayLogFor(level, g.player);   // per level and side: a run on another level or side never overwrites this one
            bool hadPref = PlayerPrefs.HasKey(OzFogOfWarOption.Pref); int prefWas = PlayerPrefs.GetInt(OzFogOfWarOption.Pref, 0);
            bool hadDiff = PlayerPrefs.HasKey(OpenZDifficulty.Key), hadMay = PlayerPrefs.HasKey(OpenZDifficulty.MayhemKey); int diffWas = PlayerPrefs.GetInt(OpenZDifficulty.Key, 2), mayWas = PlayerPrefs.GetInt(OpenZDifficulty.MayhemKey, 0);   // 1.4 MAYHEM: G9 / G11 switch MAYHEM, put back below
            bool hadIdle = PlayerPrefs.HasKey(ZodBriefing.PrefIdle); int idleWas = PlayerPrefs.GetInt(ZodBriefing.PrefIdle, 1);
            bool keepWas = OzFowGate.CornerWindowKeepsFog;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", FramesDir, run)); Directory.CreateDirectory(dir);
            Say($"  level {level} ({scene}), the player {g.player}; the owner's frames go to {dir}, the log to Logs/{log}; OzFowGate.CornerWindowKeepsFog {OzFowGate.CornerWindowKeepsFog} (constant {OzFowGate.CornerWindowKeepsFogDefault})");
            if (OzCheatSpawner.Visible) Say("  NOTE: the testing panel (Alt+P) is open: close it and run again, the owner's frames must show the game only");
            PlayerPrefs.SetInt(ZodBriefing.PrefIdle, 0);                                 // Zod's own idle barb would come after 60 s without input in the middle of a row; G4 starts one itself
            Hook(true);
            try
            {
                // ---- G1 / G2 gameplay with the fog on
                yield return ToPlay(90f);
                g = ZGame.I; f = OzFogOfWar.I;
                string refused = "no fog component"; bool on = false; if (f) on = f.SetForced(true, out refused);
                Row("G1", on && OzFogOfWar.Wanted, on ? "the fog switched on for this match (as the testing panel's FOG ON)" : "refused: " + refused + " (a level as a human, not the Unit editor, 'Zod exact' off)");
                if (!on) yield break;
                yield return Seconds(OzFowGate.FadeSeconds + 0.3f);
                f.CoverAgain(); yield return Seconds(OzFogOfWar.RevealSeconds + 0.6f);
                probe = null; ProbeInView(g); yield return Frames(3);                         // the camera on it (when it was out of the view)
                Say(probe != null ? $"  a covered enemy for the Covers and hiding rows: {probe.Type} #{probe.Id} (the camera put on it before each row and on the first frame of play after each scene)" : "  no enemy unit in covered ground: the covered enemy's own rows (G2p .. G10p) are SKIPPED, the verdict at best INCOMPLETE (drop an enemy unit in covered ground with Alt+P, close the panel, run again)");
                Rec(); yield return Frames(45); var s2 = Stop();
                int full2 = Count(s2, 0, s2.Count - 1, FullFog); var mc = OzFogOfWar.MinimapCover;
                int in2 = Count(s2, 0, s2.Count - 1, x => x.probeIn), meas2 = Count(s2, 0, s2.Count - 1, x => x.probeIn && x.probeMeasured), held2 = Count(s2, 0, s2.Count - 1, x => x.probeIn && x.probeMeasured && x.probeHeld);
                int view2 = Count(s2, 0, s2.Count - 1, x => x.probeIn && x.probeInView);
                Row("G2", s2.Count >= 30 && full2 >= s2.Count - 1 && mc && mc.enabled && mc.color.a >= 0.999f && (in2 == 0 || (meas2 >= in2 - 1 && held2 == meas2)),
                    $"gameplay: {full2} of {s2.Count} frames with full fog (the pass, strength 1, Covers, the units held off{(probe != null ? ", the covered enemy covered and held off" : "")}); "
                    + $"the covered enemy in covered ground on {in2} frames, measured at the player's camera on {meas2}, in its view on {view2}, not drawn on {held2}; the minimap cover {(mc && mc.enabled ? "on, alpha " + mc.color.a.ToString("0.00") : "OFF")}; {OzFogOfWar.GateLine()}");
                ProbeRow("G2p", "gameplay", s2, 0, s2.Count - 1);
                yield return Shot(dir + "/01_gameplay_fog_on.png");

                // ---- G3 the pause menu (gameplay: the fog stays)
                var pause = OpenZHud.I ? OpenZHud.I.Pause : null;
                if (pause == null) Skip("G3", "no pause menu (OpenZHud.Pause missing)");
                else
                {
                    ProbeInView(ZGame.I); pause.Open(); yield return Frames(3);
                    bool opened = pause.IsOpen;
                    Rec(); yield return Seconds(1f);
                    yield return Shot(dir + "/02_pause_menu_fog_on.png");
                    var s3 = Stop();
                    pause.Close(); yield return Frames(3);
                    int full3 = Count(s3, 0, s3.Count - 1, FullFog);
                    Row("G3", opened && s3.Count > 10 && full3 >= s3.Count - 1, $"the pause menu open (time scale {Time.timeScale:0.##} while open): {full3} of {s3.Count} frames with full fog, the gate open on every one");
                    ProbeRow("G3p", "the pause menu", s3, 0, s3.Count - 1);
                }

                // ---- G4 on red: Commander Zod's idle barb over the running match (OzFowGate.CornerWindowKeepsFog: true, the constant since fix
                //      round 2: gameplay, the fog stays; false: the fade out and back). On a level played as blue there is no Zod (ZodBriefing is not
                //      installed there): a blue scene's window over the match instead (OzBlueCameo.DebugNuke: a scene's window, the fade, the fold)
                yield return Seconds(0.3f);
                ProbeInView(ZGame.I);                                                         // the camera on the covered enemy (when it is out of the view)
                Rec(); yield return Frames(5);
                if (!ZodBriefing.I && OzBlueCameo.I)
                {
                    string bn = OzBlueCameo.DebugNuke();
                    if (!bn.StartsWith("playing")) { Stop(); Skip("G4", "the blue scene window did not play: " + bn); }
                    else
                    {
                        float t4 = Time.realtimeSinceStartup; while (!BlueWin() && Time.realtimeSinceStartup - t4 < 4f) yield return null;
                        yield return Seconds(1.5f);
                        yield return Shot(dir + "/03_blue_scene_window_no_fog.png");
                        t4 = Time.realtimeSinceStartup; while ((OzBlueCameo.SceneShowing || BlueWin()) && Time.realtimeSinceStartup - t4 < 30f) yield return null;
                        yield return Seconds(OzFowGate.FadeSeconds + 0.4f);
                        var s4 = Stop();
                        int win = Count(s4, 0, s4.Count - 1, x => x.bwin);
                        bool b4 = JudgeBlend(s4, 0, x => !x.open, out int c4, out int end4, out var tx4);
                        int o4 = end4 >= 0 ? end4 + 1 : -1; bool gone4 = o4 >= 0 && o4 < s4.Count && !s4[o4].bwin && Count(s4, c4, end4, x => x.bwin) == win;
                        Row("G4", win > 20 && b4 && gone4, $"a blue scene's window over the running match ({bn}; a level played as blue has no Commander Zod): the window up on {win} frames, all of them inside the closed gate "
                            + $"(the fog back only after its fold: {(gone4 ? "yes" : "NO")}); {tx4}");
                        string bx4 = "the window never closed"; bool back4 = end4 >= 0 && JudgeBack(s4, end4, out bx4);
                        Row("G4b", back4, "the blue window folded, the player plays on: " + bx4);
                        ProbeRow("G4p", "the blue scene window and the return to play", s4, 0, s4.Count - 1);
                    }
                }
                else
                {
                    string idle = ZodBriefing.DebugIdle();
                    if (!idle.StartsWith("playing")) { Stop(); Skip("G4", "the idle barb did not play: " + idle); }
                    else
                    {
                        float t4 = Time.realtimeSinceStartup; while (!ZodBriefing.CommsVisible && Time.realtimeSinceStartup - t4 < 4f) yield return null;
                        yield return Seconds(1.5f);
                        yield return Shot(dir + (OzFowGate.CornerWindowKeepsFog ? "/03_zod_idle_barb_fog_on.png" : "/03_zod_idle_barb_no_fog.png"));
                        t4 = Time.realtimeSinceStartup; while ((ZodBriefing.Speaking || ZodBriefing.CommsVisible) && Time.realtimeSinceStartup - t4 < 30f) yield return null;
                        yield return Seconds(OzFowGate.FadeSeconds + 0.4f);
                        var s4 = Stop();
                        bool keep = OzFowGate.CornerWindowKeepsFog;
                        int win = Count(s4, 0, s4.Count - 1, x => x.corner);
                        if (keep)
                        {
                            int full4 = Count(s4, 0, s4.Count - 1, x => x.corner && FullFog(x)), drawn4 = Count(s4, 0, s4.Count - 1, x => x.corner && x.probeIn && x.probeMeasured && !x.probeHeld);
                            Row("G4", win > 20 && full4 >= win - 1 && drawn4 == 0,
                                $"Commander Zod's idle barb ({idle}), OzFowGate.CornerWindowKeepsFog true (gameplay: the camera and the controls stay the player's): the window up on {win} frames, full fog on {full4} of them "
                                + $"(the pass, strength 1, Covers, the units held off, the minimap cover), the covered enemy drawn on {drawn4}");
                            Say("  (G4b not run: the switch true, the fog never left)");
                            ProbeRow("G4p", "Commander Zod's idle barb (the fog stays)", s4, 0, s4.Count - 1);
                        }
                        else
                        {
                            bool b4 = JudgeBlend(s4, 0, x => !x.open, out _, out int end4, out var tx4);
                            Row("G4", win > 20 && b4, $"Commander Zod's idle barb ({idle}), a transmission (OzFowGate.CornerWindowKeepsFog false): the window up on {win} frames; {tx4}");
                            string bx4 = "the window never closed"; bool back4 = end4 >= 0 && JudgeBack(s4, end4, out bx4);
                            Row("G4b", back4, "the window folded, the player plays on: " + bx4);
                            ProbeRow("G4p", "Commander Zod's idle barb (a transmission) and the return to play", s4, 0, s4.Count - 1);
                        }
                    }
                }

                // ---- G5 a mid-match hard cut: ZodCinePreview (the HUD stays up, so the minimap is seen too)
                yield return Seconds(0.3f);
                ProbeInView(ZGame.I);                                                         // the camera on the covered enemy (when it is out of the view)
                Rec(); yield return Frames(5);
                int call5 = Time.frameCount; string cp = ZodCinePreview.Play(4f);
                if (!cp.StartsWith("preview")) { Stop(); Skip("G5", "ZodCinePreview did not start: " + cp); }
                else
                {
                    float t5 = Time.realtimeSinceStartup; bool shot5 = false;
                    while (ZodCinePreview.I != null && Time.realtimeSinceStartup - t5 < 20f)
                    {
                        if (!shot5 && Time.realtimeSinceStartup - t5 > 1.5f) { shot5 = true; yield return Shot(dir + "/04_cine_preview_mid_match.png"); }
                        yield return null;
                    }
                    yield return Seconds(OzFowGate.FadeSeconds + 0.4f);
                    var s5 = Stop();
                    bool c5 = JudgeCut(s5, call5, x => x.fly || !x.open, out int last5, out var tx5);
                    Row("G5", c5, $"a mid-match hard cut ({cp}): {tx5}");
                    string bx5 = "no scene measured"; bool b5 = last5 >= 0 && JudgeBack(s5, last5, out bx5);
                    Row("G5b", b5, "the player has the camera again after the preview: " + bx5);
                    ProbeRow("G5p", "the mid-match hard cut and the return to play", s5, 0, s5.Count - 1);
                }

                // ---- G6 a takeover begun late in the frame that keeps the RTS camera on (made up: the sim switched off at the start of the game
                //      view's drawing, after every Update and LateUpdate): the gate read at the player's camera must catch it on that frame
                yield return Seconds(0.3f);
                ProbeInView(ZGame.I);                                                         // the camera on the covered enemy (when it is out of the view)
                Rec(); yield return Frames(5);
                int lc0 = OzFogOfWar.GateLateCloses, cc0 = OzFogOfWar.GateCameraCuts;
                lateFrame = -1; lateStart = () => { var gg = ZGame.I; if (gg) gg.enabled = false; };
                float t6 = Time.realtimeSinceStartup; while (lateFrame < 0 && Time.realtimeSinceStartup - t6 < 3f) yield return null;
                yield return Frames(4);
                { var gg = ZGame.I; if (gg) gg.enabled = true; }
                yield return Seconds(OzFowGate.FadeSeconds + 0.4f);
                var s6 = Stop();
                int i6 = First(s6, 0, x => x.frame == lateFrame);
                bool ok6 = i6 >= 1 && i6 + 1 < s6.Count && FullFog(s6[i6 - 1]) && NoFog3D(s6[i6]) && NoFog(s6[i6 + 1]) && OzFogOfWar.GateLateCloses > lc0 && OzFogOfWar.GateCameraCuts > cc0;
                Row("G6", ok6, i6 >= 1 && i6 + 1 < s6.Count ? $"the frame before: {D(s6[i6 - 1])}; the takeover's first frame (begun at the drawing's start, the RTS camera on; the 3D view, the frame's UI is built already): {D(s6[i6])}; the next frame (the minimap too): {D(s6[i6 + 1])}; closes found after Update {OzFogOfWar.GateLateCloses - lc0}, cuts found at the camera {OzFogOfWar.GateCameraCuts - cc0}"
                                       : "the late start did not run (no game view drawing in 3 s)");
                string bx6 = "-"; bool b6 = i6 >= 0 && JudgeBack(s6, i6 + 1, out bx6);
                Row("G6b", b6, "the sim on again: " + bx6);
                ProbeRow("G6p", "the late takeover and the return to play", s6, 0, s6.Count - 1);

                // ---- G7 a mid-match briefing (red, or blue on a level played as blue): skipped after 3 s as the SPACE key does
                yield return Seconds(0.3f);
                ProbeInView(ZGame.I);                                                         // the camera on the covered enemy (when it is out of the view)
                Rec(); yield return Frames(5);
                int call7 = Time.frameCount; string db = ZodBriefing.DebugBriefing();
                if (!db.Contains("started") && OzBlueCameo.I) db = OzBlueCameo.DebugBriefing();
                if (!db.Contains("started")) { Stop(); Skip("G7", "no mid-match briefing: " + db); }
                else
                {
                    float t7 = Time.realtimeSinceStartup; bool shot7 = false, skippedB = false;
                    while (Time.realtimeSinceStartup - t7 < 60f)
                    {
                        bool brief = ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive, fl = ZodFlyover.Live != null && ZodFlyover.Live.Active;
                        if (!shot7 && Time.realtimeSinceStartup - t7 > 2f) { shot7 = true; yield return Shot(dir + "/05_briefing_mid_match.png"); }
                        if (!skippedB && Time.realtimeSinceStartup - t7 > 3f) skippedB = ZodBriefing.ProofSkipBriefing() | OzBlueCameo.ProofSkipBriefing();
                        if (!brief && !fl && Time.realtimeSinceStartup - t7 > 1f) break;
                        yield return null;
                    }
                    float tw = Time.realtimeSinceStartup; while ((ZodBriefing.CommsVisible || OzBlueCameo.SceneShowing || BlueWin()) && Time.realtimeSinceStartup - tw < 10f) yield return null;   // the briefing's window folding
                    yield return Seconds(OzFowGate.FadeSeconds + 0.4f);
                    var s7 = Stop();
                    bool c7 = JudgeCut(s7, call7, x => x.briefing || x.fly || !x.open, out int last7, out var tx7);
                    Row("G7", c7, $"a mid-match briefing ({db}; skipped {skippedB}): {tx7}");
                    string bx7 = "no scene measured", wx7 = "-"; bool b7 = last7 >= 0 && JudgeBack(s7, last7, out bx7);
                    bool w7 = WindowGone(s7, LastWhere(s7, x => x.briefing || x.fly), out wx7);
                    Row("G7b", b7 && w7, "the hand-back: the briefing's window (red or blue) folded before the fog came back: " + wx7 + "; " + bx7);
                    ProbeRow("G7p", "the mid-match briefing and its hand-back", s7, 0, s7.Count - 1);
                }

                // ---- G8 the win ending (and the L16 lounger / L07 hot tub scene on a red win)
                yield return Ending(true, dir, "06_win_glide_1s", "07_win_strike_5s", "08_win_ending_scene", "09_win_end_screen", "G8");

                // ---- G9 the level-start briefing: the level loaded again with OPTIONS FOG OF WAR ON
                PlayerPrefs.SetInt(OpenZDifficulty.Key, (int)GameDifficulty.Hard); PlayerPrefs.SetInt(OpenZDifficulty.MayhemKey, 1); probe = null;   // 1.4 MAYHEM: the fog of war is MAYHEM's (oz_difficulty 0 + oz_mayhem 1)
                int ff0 = OzFogOfWar.GateFirstFrameCovers;
                Rec();
                yield return LoadLevel(scene, 120f);
                g = ZGame.I; f = OzFogOfWar.I;
                if (!g || !f || g.World == null) { Stop(); Row("G9", false, "the level did not load again: " + scene); yield break; }
                float t9 = Time.realtimeSinceStartup; while (!(ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive) && Time.realtimeSinceStartup - t9 < 5f) yield return null;
                if (!(ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive))
                {
                    yield return Frames(20); var s9n = Stop();
                    int w9 = First(s9n, 0, x => x.wanted && x.open);
                    Skip("G9", "no level-start briefing after the reload (OPTIONS briefing off, or a test / capture mode)");
                    Row("G9n", OzFogOfWar.Wanted && w9 >= 0 && s9n[w9].strength >= 0.999f && s9n[w9].hold && OzFogOfWar.GateFirstFrameCovers > ff0,
                        w9 >= 0 ? $"a level start with no briefing: covered at once on the first frame of play ({D(s9n[w9])}), matches started covered {OzFogOfWar.GateFirstFrameCovers - ff0}" : "the fog never showed after the reload");
                }
                else
                {
                    yield return Frames(2);
                    probe = Covered(g);                                                     // the reveal map runs under the briefing: a covered enemy for the hand-back (the camera put on it on the first frame of play)
                    Say(probe != null ? $"  after the reload, a covered enemy for G9b: {probe.Type} #{probe.Id}" : "  after the reload, no enemy unit in covered ground: G9p is SKIPPED (G9b measures no covered enemy)");
                    float tb = Time.realtimeSinceStartup; int k = 0; string[] names = { "10_briefing_shot_3s", "11_briefing_shot_7s", "12_briefing_shot_11s" }; float[] at = { 3f, 7f, 11f };
                    while ((ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive || (ZodFlyover.Live != null && ZodFlyover.Live.Active)) && Time.realtimeSinceStartup - tb < 150f)
                    {
                        if (k < at.Length && Time.realtimeSinceStartup - tb > at[k]) { yield return Shot(dir + "/" + names[k] + ".png"); k++; }
                        yield return null;
                    }
                    float to = Time.realtimeSinceStartup; while (!OzFogOfWar.GameplayView && Time.realtimeSinceStartup - to < 40f) yield return null;   // (a transmission window may still fold)
                    yield return Seconds(OzFowGate.FadeSeconds + 0.1f);
                    yield return Shot(dir + "/13_hand_back_fog_back.png");
                    yield return Seconds(0.3f);
                    var s9 = Stop();
                    int a9 = First(s9, 0, x => x.wanted), sc9 = Count(s9, 0, s9.Count - 1, x => x.wanted && (x.briefing || x.fly)), bad9 = Count(s9, 0, s9.Count - 1, x => x.wanted && (x.briefing || x.fly) && !NoFog(x));
                    int lastScene = -1; for (int i = 0; i < s9.Count; i++) if (s9[i].briefing || s9[i].fly) lastScene = i;
                    Row("G9", a9 >= 0 && sc9 > 30 && bad9 == 0 && OzFogOfWar.Wanted,
                        $"the level-start briefing on MAYHEM (the fog on): the fog on for the match from frame {(a9 >= 0 ? s9[a9].frame : -1)}, {sc9} frames of briefing and flyover, {bad9} with any fog (no pass, strength 0, nothing held off, Covers no, no minimap cover); {k} frames for the owner");
                    string bx9 = "no briefing frame measured", wx9 = "-"; bool b9 = lastScene >= 0 && JudgeBack(s9, lastScene, out bx9);
                    bool w9b = WindowGone(s9, lastScene, out wx9);
                    Row("G9b", b9 && w9b, "the hand-back: the briefing's window folded before the fog came back: " + wx9 + "; " + bx9);
                    ProbeRow("G9p", "the level-start briefing's hand-back", s9, 0, s9.Count - 1);
                }

                // ---- G10 the loss ending (this new match)
                yield return ToPlay(30f);
                f = OzFogOfWar.I; g = ZGame.I;
                if (f) { f.CoverAgain(); yield return Seconds(OzFogOfWar.RevealSeconds + 0.6f); probe = null; ProbeInView(g); }
                yield return Ending(false, dir, "14_loss_glide_1s", "15_loss_strike_5s", "16_loss_ending_scene", "17_loss_end_screen", "G10");   // 16: the L16 lounger scene after a loss (taken only where it plays)

                // ---- G11 OPTIONS FOG OF WAR off: nothing anywhere (rule 5)
                PlayerPrefs.SetInt(OpenZDifficulty.Key, (int)GameDifficulty.Hard); PlayerPrefs.SetInt(OpenZDifficulty.MayhemKey, 0); probe = null;   // 1.4 MAYHEM: HARD, no fog
                yield return LoadLevel(scene, 120f);
                Rec(); yield return Seconds(2f);
                yield return ToPlay(60f);
                yield return Seconds(1.5f);
                var s11 = Stop();
                int any11 = Count(s11, 0, s11.Count - 1, x => x.wanted || x.queued > 0 || x.strength > 0f || x.mini || x.hiding || x.hold || x.hidden > 0);
                Row("G11", s11.Count > 30 && any11 == 0 && !OzFogOfWar.Wanted, $"HARD, not MAYHEM: the fog off (a reload): {s11.Count} frames (the start, any briefing, play), {any11} with anything of the fog ({OzFogOfWar.GateReason})");
                Row("G12", oldFrames > 0 && oldBad == 0, $"over every sampled frame: the gate before this kit (GateWhyBefore) closed on {oldFrames}, the new gate open on {oldBad} of them{(oldBadAt != null ? " (first " + oldBadAt + ")" : "")}");
            }
            finally
            {
                recording = false; lateStart = null; Hook(false);
                { var gg = ZGame.I; if (gg && !gg.enabled && !ZodBriefing.BriefingActive && !OzBlueCameo.BriefingActive && ZodCinePreview.I == null) gg.enabled = true; }
                OzFowGate.CornerWindowKeepsFog = keepWas;
                if (hadPref) PlayerPrefs.SetInt(OzFogOfWarOption.Pref, prefWas); else PlayerPrefs.DeleteKey(OzFogOfWarOption.Pref);
                if (hadDiff) PlayerPrefs.SetInt(OpenZDifficulty.Key, diffWas); else PlayerPrefs.DeleteKey(OpenZDifficulty.Key);   // 1.4 MAYHEM: the difficulty put back
                if (hadMay) PlayerPrefs.SetInt(OpenZDifficulty.MayhemKey, mayWas); else PlayerPrefs.DeleteKey(OpenZDifficulty.MayhemKey);
                Say($"  1.4 MAYHEM: {OpenZDifficulty.Key} put back ({(hadDiff ? diffWas.ToString() : "not set")}), {OpenZDifficulty.MayhemKey} put back ({(hadMay ? mayWas.ToString() : "not set")}): the difficulty is {OpenZDifficulty.Label(OpenZDifficulty.Choice)} again");
                if (hadIdle) PlayerPrefs.SetInt(ZodBriefing.PrefIdle, idleWas); else PlayerPrefs.DeleteKey(ZodBriefing.PrefIdle);
                Say($"  OPTIONS FOG OF WAR put back ({(hadPref ? prefWas.ToString() : "not set")}), Zod's idle barb setting put back ({(hadIdle ? idleWas.ToString() : "not set")}); the camera put on the covered enemy {aims} times; frames for the owner ({shots.Count}, this run): {string.Join(", ", shots)} in {dir}");
                End(log);
            }
        }

        /// <summary>The fort of the other side (win) or ours (loss) falls (OpenZVictory.DebugFortFalls: the real chain), recorded through the
        /// glide, the strike, an ending scene and the end screen.</summary>
        static IEnumerator Ending(bool win, string dir, string glide, string strike, string sceneShot, string endShot, string id)
        {
            yield return Seconds(0.3f);
            // the lounger / hot tub scene can play here (P View/Zod/ZodBriefing.EndingScene.cs EndingSceneWanted: L16 after any result, L07 after a
            // red win; never on a level played as blue, where ZodBriefing is not installed)
            var g0 = ZGame.I; string lv = g0 ? g0.level : "?";
            bool sceneHere = ZodBriefing.I && ZodBriefing.EndingSceneEnabled && g0
                             && (lv == ZodBriefing.EndingLevel || (lv == ZodBriefing.EndingTubLevel && win && g0.player == Team.Red && !OzPlayBlue.Active));
            ProbeInView(g0);                                                                  // the camera on the covered enemy for the frames of play before the fall
            Rec(); yield return Frames(5);
            int n = OpenZVictory.DebugFortFalls(win);
            if (n == 0) { Stop(); Skip(id, $"no {(win ? "enemy" : "own")} fort part to fell on this level"); yield break; }
            float t0 = Time.realtimeSinceStartup, decidedAt = -1f, holdAt = -1f, endAt = -1f; int k = 0; bool shotHold = false, shotEnd = false;
            while (Time.realtimeSinceStartup - t0 < 40f)
            {
                var g = ZGame.I; bool dec = g && g.World != null && PlayerInput.MatchDecided(g.World);
                if (dec && decidedAt < 0f) decidedAt = Time.realtimeSinceStartup;
                if (ZodBriefing.EndingHolding && holdAt < 0f) holdAt = Time.realtimeSinceStartup;
                var es = OpenZHud.I ? OpenZHud.I.EndScreen : null; if (es && es.IsShown && endAt < 0f) endAt = Time.realtimeSinceStartup;
                float r = Time.realtimeSinceStartup;
                if (decidedAt >= 0f && k == 0 && r - decidedAt > 1f) { k++; yield return Shot(dir + "/" + glide + ".png"); }
                else if (decidedAt >= 0f && k == 1 && r - decidedAt > 5f) { k++; yield return Shot(dir + "/" + strike + ".png"); }
                else if (sceneShot != null && holdAt >= 0f && !shotHold && r - holdAt > 1f) { shotHold = true; yield return Shot(dir + "/" + sceneShot + ".png"); }
                else if (endAt >= 0f && !shotEnd && r - endAt > 1f) { shotEnd = true; yield return Shot(dir + "/" + endShot + ".png"); }
                if (shotEnd && k >= 2) break;
                yield return null;
            }
            var s = Stop();
            bool ok = JudgeBlend(s, 0, x => !x.open, out int c, out _, out var tx);
            Row(id, ok, $"the {(win ? "win" : "loss")} ending ({n} fort part(s) felled{(decidedAt < 0f ? ", the match was NEVER decided" : "")}): {tx}");
            int hold = Count(s, 0, s.Count - 1, x => x.holding), badHold = Count(s, 0, s.Count - 1, x => x.holding && !NoFog(x));
            if (hold > 0) Row(id + "b", badHold == 0, $"the ending scene (the L16 loungers / the L07 hot tub, a hard cut): {hold} frames, {badHold} with any fog");
            else if (sceneHere) Skip(id + "b", $"the ending scene can play on {lv} after a {(win ? "win" : "loss")} but did not (its cue sheet, line or set missing, or no fort fell): NOT checked");
            else Say($"  ({id}b not run: no ending scene on {lv} after a {(win ? "win" : "loss")}{(ZodBriefing.I ? "" : ", a level played as blue")}: it plays on L16 after any result and on L07 after a red win)");
            int end = endAt >= 0f ? Count(s, 0, s.Count - 1, x => x.decided) : 0;
            Row(id + "c", endAt >= 0f && Count(s, Mathf.Max(0, c), s.Count - 1, x => x.queued > 0 && x.strength <= 0.001f) == 0,
                endAt >= 0f ? $"the end screen came ({end} frames decided); no pass at strength 0 at any point" : "the end screen did not come within 40 s");
            ProbeRow(id + "p", $"the {(win ? "win" : "loss")} ending", s, 0, s.Count - 1);
        }
    }
}
