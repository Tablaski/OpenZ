#if !OZ_RELEASE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// RTS camera, hands off the keyboard and the mouse (the check presses its own keys: a test keyboard and mouse made current for its run),
    /// about 2 minutes; the match goes on. The camera's pose is read at the start of every picture of the game camera (the pose that is drawn).
    ///  G  glide in (a grunt, Alt+F): the view on, the glide 0.5 .. 1.0 s, no picture jumps (the largest step at most 20 % of the way and at most
    ///     4 x the mean step), at the end the view's lens (field of view 60, near 0.05), the whoosh and ui_confirm once each, the gun copy lowered at
    ///     the start and in place at the end, no crosshair before 0.85 of the way; glide out (Esc): 0.5 .. 1.0 s, no jump, the RTS camera's lens,
    ///     height and pose back exactly, the RTS camera on the unit, the whoosh and ui_cancel once each, nothing holds the camera after it
    ///  T  turn round: Alt+F, 0.3 s, Alt+F: back at the start pose (the player's own place), no jump and no sudden change of speed; Alt+F,
    ///     in, Esc, 0.3 s, Alt+F: in again on the same unit, the look kept (yaw within 2 deg), no jump
    ///  I  an instant leave (a scene takes the camera): no glide, the RTS lens at once
    ///  F  fade: a grunt squad in the view: none of the mates' TeamLit parts held off by the view, a mate within 1 m of the eye cut 0.9 or more;
    ///     a grunt walking up to the eye and away again: its cut never changes by more than 0.35 in a frame (no snap) and reaches 0.8 near the eye;
    ///     after the leave nothing carries a cut and nothing is held
    ///  W  weapon in range: an enemy heavy tank (a dummy: it never fires) out of reach: the copy as it is; in reach: the copy eases whiter (0.98
    ///     within 0.7 s, at least 0.2 s from start to full, at most 0.2 in a frame), the lock blip once, the copy's emission up; the dummy gone:
    ///     back the same way to exactly its own emission
    ///  S  Shift: a light tank and a grunt squad: W 1 s, then W + Shift 1 s: the sim speed x 1.3 or more; Shift pressed sends a new order within
    ///     3 frames; the squad keeps together (members within 4 m of the leader); with the rule's switch off ('Zod exact') Shift changes nothing
    /// No PASS without its samples; errors and exceptions logged during the run fail it. Report: <see cref="Folder"/>/report_feel.txt. Test only:
    /// the units it made are taken out again.
    /// </summary>
    public sealed class OzFpsFeelPlayCheck : MonoBehaviour
    {
        public static string Report = "idle";

        public static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var k = Path.Combine(d.FullName, "work", "staging", "v1_5", "kits", "fps_feel");
                if (Directory.Exists(k)) return Path.Combine(k, "proof", "play");
            }
            return Path.Combine(Application.persistentDataPath, "fps_feel_play_check");
        }

        public static void Run() { Report = "running"; new GameObject("OzFpsFeelPlayCheck").AddComponent<OzFpsFeelPlayCheck>(); }

        ZGame g; ZWorld W; RtsCamera rts; PlayerInput pi; Camera cam; Team me, foe; int pass, fail, errors;
        readonly List<string> errorLines = new List<string>();
        Keyboard proofKb, realKb; Mouse proofMouse, realMouse;
        readonly StringBuilder log = new StringBuilder();
        readonly List<ZObject> made = new List<ZObject>();
        HashSet<int> startIds;

        struct Sample { public float t, fov, near, gunY, s; public Vector3 p; public Quaternion r; public int gui; }
        readonly List<Sample> samples = new List<Sample>(); bool sampling, hooked;

        void OnEnable() { if (!hooked) { RenderPipelineManager.beginCameraRendering += OnCam; hooked = true; } }
        void OnDisable() { if (hooked) { RenderPipelineManager.beginCameraRendering -= OnCam; hooked = false; } }
        void OnCam(ScriptableRenderContext ctx, Camera c)
        {
            if (!sampling || !cam || c != cam) return;
            samples.Add(new Sample { t = Time.unscaledTime, p = c.transform.position, r = c.transform.rotation, fov = c.fieldOfView, near = c.nearClipPlane, gunY = GunY(), s = OzFpsFeel.GlideS, gui = OzFpsView.GuiFrames });
        }

        void Start() { Application.logMessageReceived += OnLog; StartCoroutine(All()); }
        void OnDestroy() { Application.logMessageReceived -= OnLog; Devices(false); OzFpsView.AtRender = null; }
        void OnLog(string msg, string st, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) { errors++; if (errorLines.Count < 6) errorLines.Add(msg.Split('\n')[0]); } }

        void Log(string s) { log.AppendLine(s); Debug.Log("[OpenZ FPS feel check] " + s); }
        void Check(string what, bool ok) { if (ok) pass++; else fail++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < s) yield return null; }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator WaitFor(System.Func<bool> c, float max) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < max && !c()) yield return null; }
        static int Plays(string id) { var a = AudioDirector.I; return a != null && a.Plays.TryGetValue(id, out var n) ? n : 0; }

        // ------------------------------------------------------------------ the test keyboard and mouse
        void Devices(bool on)
        {
            if (on)
            {
                realKb = Keyboard.current; realMouse = Mouse.current;
                proofKb = InputSystem.AddDevice<Keyboard>("OzFpsFeelProofKeyboard"); proofKb.MakeCurrent();
                proofMouse = InputSystem.AddDevice<Mouse>("OzFpsFeelProofMouse"); proofMouse.MakeCurrent();
                InputSystem.QueueStateEvent(proofMouse, new MouseState { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) });   // Unity pass 1.5: a new mouse sits at (0, 0), the screen's corner: the RTS camera's edge scroll moved the view under the check
                return;
            }
            if (proofKb != null) { if (proofKb.added) InputSystem.RemoveDevice(proofKb); proofKb = null; }
            if (proofMouse != null) { if (proofMouse.added) InputSystem.RemoveDevice(proofMouse); proofMouse = null; }
            if (realKb != null && realKb.added) realKb.MakeCurrent();
            if (realMouse != null && realMouse.added) realMouse.MakeCurrent();
            realKb = null; realMouse = null;
        }
        void Keys(params Key[] k) { if (proofKb != null) InputSystem.QueueStateEvent(proofKb, new KeyboardState(k)); }
        IEnumerator Tap(params Key[] k) { Keys(k); yield return null; yield return null; Keys(); yield return null; }
        IEnumerator AltF() { yield return Tap(Key.LeftAlt, Key.F); }
        IEnumerator Esc() { yield return Tap(Key.Escape); }
        static Key Fwd => OzBindings.KeyOf(OzAction.ScrollUp) != Key.None ? OzBindings.KeyOf(OzAction.ScrollUp) : Key.W;

        // ------------------------------------------------------------------ places and units
        bool Free(int x, int y, bool vehicle) =>
            !W.Path.WithinImpassable(x - 8, y - 8, 16, 16, out _, out _, true) && !W.Map.LavaAt(x >> 4, y >> 4)
            && (!vehicle || !W.Path.WithinImpassable(x - 16, y - 16, 32, 32, out _, out _, false))
            && !W.UnitObjects.Any(o => o != null && !o.Destroyed && System.Math.Abs(o.CenterX - x) < 40 && System.Math.Abs(o.CenterY - y) < 40);

        bool Spot(out int x, out int y, bool vehicle, int disc)
        {
            int fx = (int)(rts.focus.x * 8f), fy = (int)(-rts.focus.z * 8f), mw = (int)(rts.mapSizeMeters.x * 8f), mh = (int)(rts.mapSizeMeters.y * 8f), edge = 320;
            for (int r = 0; r < 1600; r += 24)
                for (int a = 0; a < 16; a++)
                {
                    x = fx + (int)(r * Mathf.Cos(a * 0.3927f)); y = fy + (int)(r * Mathf.Sin(a * 0.3927f));
                    bool ok = x > edge && y > edge && x < mw - edge && y < mh - edge;
                    for (int k = 0; ok && k < 16; k++) for (int rr = 0; ok && rr <= disc; rr += 24) ok = Free(x + (int)(rr * Mathf.Cos(k * 0.3927f)), y + (int)(rr * Mathf.Sin(k * 0.3927f)), vehicle);
                    if (ok) return true;
                }
            x = y = 0; return false;
        }

        bool FindSpot(out int x, out int y, bool vehicle)
        {
            Clean();
            foreach (int disc in new[] { 120, 72, 48 }) if (Spot(out x, out y, vehicle, disc)) return true;
            x = y = 0; return false;
        }

        ZObject Make(string cat, string type, int x, int y, Team t, int amount = -1)
        {
            var o = cat == "robot" ? W.CreateRobotGroup(type, x - 8, y - 8, t, amount) : W.CreateObject(cat, type, x - 16, y - 16, t);
            if (o != null) { g.SpawnUnitView(o); foreach (var m in o.Minions) if (m != null) g.SpawnUnitView(m); made.Add(o); }
            return o;
        }

        void Drop(ZObject o)
        {
            if (o == null) return;
            var ids = new List<int> { o.Id }; foreach (var m in o.Minions) if (m != null) ids.Add(m.Id);
            pi.Selection.RemoveAll(s => s != null && ids.Contains(s.Id));
            W.RemoveNow(o);
            foreach (var id in ids) if (g.Views.TryGetValue(id, out var v)) { g.Views.Remove(id); if (v) { v.gameObject.SetActive(false); Destroy(v.gameObject); } }
        }

        void Clean()
        {
            var all = new List<ZObject>(made);
            if (startIds != null) foreach (var o in W.UnitObjects.ToList()) if (o != null && !startIds.Contains(o.Id)) all.Add(o);
            foreach (var o in all) Drop(o);
            made.Clear();
        }

        void Select(ZObject u) { pi.Selection.Clear(); if (u != null) pi.Selection.Add(u); }
        UnitView ViewOf(ZObject u) => u != null && g.Views.TryGetValue(u.Id, out var v) ? v : null;
        void Focus(ZObject u) { var v = ViewOf(u); if (v) { rts.focus = new Vector3(v.transform.position.x, 0f, v.transform.position.z); rts.follow = null; } }

        static float GunY()
        {
            var fv = OzFpsView.I; if (!fv || fv.GunRenderers.Count == 0 || !fv.GunRenderers[0]) return 0f;
            var piv = fv.GunRenderers[0].transform.parent; var root = piv ? piv.parent : null;
            return root ? root.localPosition.y : 0f;
        }

        static readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static float GunEm()
        {
            var fv = OzFpsView.I; if (!fv || fv.GunRenderers.Count == 0 || !fv.GunRenderers[0]) return -1f;
            var r = fv.GunRenderers[0];
            r.GetPropertyBlock(mpb, 0);
            if (mpb.isEmpty) r.GetPropertyBlock(mpb);
            if (mpb.isEmpty || !mpb.HasColor(EmissionId)) return 0f;
            var c = mpb.GetColor(EmissionId); return (c.r + c.g + c.b) / 3f;
        }

        /// <summary>The largest picture-to-picture step of the samples as a share of the whole way, the whole way (m), the moving pictures.</summary>
        static float StepShare(List<Sample> s, int from, out float total, out int moving, out float maxStep)
        {
            total = 0f; moving = 0; maxStep = 0f;
            for (int i = from + 1; i < s.Count; i++) { float d = (s[i].p - s[i - 1].p).magnitude; total += d; if (d > 0.001f) moving++; maxStep = Mathf.Max(maxStep, d); }
            return total > 1e-4f ? maxStep / total : 0f;
        }

        /// <summary>The largest change of speed (m/s) from one picture to the next against the largest speed (a reversal must not jump).</summary>
        static float SpeedJump(List<Sample> s, int from, out float peak)
        {
            peak = 0f; float worst = 0f, prev = -1f;
            for (int i = from + 1; i < s.Count; i++)
            {
                float dt = Mathf.Max(1e-4f, s[i].t - s[i - 1].t), v = (s[i].p - s[i - 1].p).magnitude / dt;
                peak = Mathf.Max(peak, v); if (prev >= 0f) worst = Mathf.Max(worst, Mathf.Abs(v - prev)); prev = v;
            }
            return worst;
        }

        bool NoJump(string tag, int from = 0)
        {
            float share = StepShare(samples, from, out float total, out int moving, out float maxStep);
            bool ok = moving >= 10 && share <= 0.2f && maxStep <= 4f * total / Mathf.Max(1, moving);
            Check($"{tag}: no jump: {moving} moving pictures over {total:0.0} m, the largest step {maxStep:0.00} m ({share * 100f:0.0} % of the way, mean {total / Mathf.Max(1, moving):0.00} m)", ok);
            return ok;
        }

        // ------------------------------------------------------------------ the run
        IEnumerator All()
        {
            g = ZGame.I; W = g ? g.World : null; pi = FindFirstObjectByType<PlayerInput>(); rts = pi ? pi.cam : null; cam = rts ? rts.GetComponent<Camera>() : null;
            if (W == null || !rts || !pi || !cam) { Log("no level in Play"); fail++; Finish(); yield break; }
            Directory.CreateDirectory(Folder());
            me = g.player; foe = me == Team.Red ? Team.Blue : Team.Red;
            yield return WaitFor(() => OpenZHud.ControlAt > 0f && Time.unscaledTime >= OpenZHud.ControlAt + 1f, 40f);
            W.Bots.Clear();
            startIds = new HashSet<int>(W.UnitObjects.Where(o => o != null).Select(o => o.Id));
            Log($"FPS FEEL CHECK level {g.level}, RTS height {rts.height:0.0}, field of view {cam.fieldOfView:0}; glide {OzFpsFeel.GlideSeconds:0.00} s, fade {OzFpsFeel.FadeClearM:0.0} -> {OzFpsFeel.FadeGoneM:0.0} m over {OzFpsFeel.FadeSeconds:0.00} s, sprint x {ZWorld.SprintPct / 100.0:0.00} (switch {ZWorld.SprintOrderDeviation})");
            OzFpsFeel.GlideOn = OzFpsFeel.FadeOn = OzFpsFeel.WhitenOn = OzFpsFeel.SprintOn = true;
            Devices(true);
            if (OzFpsView.On && OzFpsView.I) OzFpsView.I.Leave("the check starts");
            yield return Frames(3);

            yield return GlideRows();
            yield return TurnRows();
            yield return InstantRow();
            yield return FadeRows();
            yield return WhitenRows();
            yield return SprintRows("vehicle", "light", true);
            yield return SprintRows("robot", "grunt", false);

            Clean();
            if (errors > 0) Check($"no error or exception logged during the run ({errors}: {string.Join(" | ", errorLines)})", false);
            Finish();
        }

        IEnumerator In(ZObject u)
        {
            Select(u); yield return AltF();
            yield return WaitFor(() => OzFpsView.On && !OzFpsFeel.Gliding, 2.5f);
        }

        IEnumerator Out()
        {
            yield return Esc();
            yield return WaitFor(() => !OzFpsView.On, 2.5f);
            yield return Frames(2);
        }

        // ---- G: glide in and out
        IEnumerator GlideRows()
        {
            if (!FindSpot(out int x, out int y, false)) { Check("G: a free place near the camera", false); yield break; }
            var u = Make("robot", "grunt", x, y, me, 1); Focus(u); yield return Real(0.8f);
            float fov0 = cam.fieldOfView, near0 = cam.nearClipPlane, h0 = rts.height;
            var start = cam.transform.position;
            int wh0 = Plays(OzFpsGlideSound.In), cf0 = Plays("ui_confirm"), gui0 = OzFpsView.GuiFrames;   // 1.5 fps_feel_whoosh_vignette: the glide's own whoosh
            samples.Clear(); sampling = true;
            Select(u); yield return AltF();
            yield return WaitFor(() => OzFpsView.On && !OzFpsFeel.Gliding, 2.5f);
            yield return Frames(3); sampling = false;
            bool on = OzFpsView.On && OzFpsView.I && OzFpsView.I.Unit == u;
            Check($"G in: the view on the grunt ({on}), the glide {OzFpsFeel.LastGlideSeconds:0.00} s (0.5 .. 1.0)", on && OzFpsFeel.LastGlideSeconds >= 0.5f && OzFpsFeel.LastGlideSeconds <= 1.0f);
            int first = samples.FindIndex(q => (q.p - start).sqrMagnitude > 1e-6f);
            Check($"G in: the first moved picture {(first >= 0 ? (samples[first].p - start).magnitude : -1f):0.00} m from the RTS pose (no cut: at most 2 m)", first >= 0 && (samples[first].p - start).magnitude <= 2f);
            NoJump("G in");
            Check($"G in: the view's lens at the end: field of view {cam.fieldOfView:0.00}, near {cam.nearClipPlane:0.000}", Mathf.Abs(cam.fieldOfView - OzFpsView.FovDeg) < 1e-3f && Mathf.Abs(cam.nearClipPlane - OzFpsView.NearClip) < 1e-4f);
            Check($"G in: one whoosh ({Plays(OzFpsGlideSound.In) - wh0}) and one ui_confirm ({Plays("ui_confirm") - cf0})", Plays(OzFpsGlideSound.In) - wh0 == 1 && Plays("ui_confirm") - cf0 == 1);   // 1.5 fps_feel_whoosh_vignette
            Check($"G in: the vignette at the eye (weight {OzFpsVignette.Weight:0.000}, volume {(OzFpsVignette.VolumeOn ? "on" : "off")})", OzFpsVignette.Weight > 0.99f && OzFpsVignette.VolumeOn);   // 1.5 fps_feel_whoosh_vignette
            float lowest = samples.Count > 0 ? samples.Min(q => q.gunY) : 0f;
            Check($"G in: the gun copy lowered at the start ({lowest:0.00} m) and in place at the end ({GunY():0.000} m)", lowest <= -0.2f && Mathf.Abs(GunY()) < 1e-3f);
            bool guiEarly = samples.Any(q => q.s < 0.85f && q.gui > gui0 + 1);
            Check($"G in: no crosshair before 0.85 of the way ({!guiEarly})", !guiEarly);

            int wh1 = Plays(OzFpsGlideSound.Out), cn1 = Plays("ui_cancel");   // 1.5 fps_feel_whoosh_vignette: the glide's own whoosh
            samples.Clear(); sampling = true;
            yield return Out();
            sampling = false;
            var rtsNow = cam.transform.position;
            Check($"G out: the view off ({!OzFpsView.On}), nothing holds the camera ({!OzFpsView.HoldsCamera}), the glide {OzFpsFeel.LastGlideSeconds:0.00} s (0.5 .. 1.0)",
                  !OzFpsView.On && !OzFpsView.HoldsCamera && OzFpsFeel.LastGlideSeconds >= 0.5f && OzFpsFeel.LastGlideSeconds <= 1.0f);
            NoJump("G out");
            Check($"G out: the RTS lens and height back exactly (field of view {cam.fieldOfView:0.000} / {fov0:0.000}, near {cam.nearClipPlane:0.0000} / {near0:0.0000}, height {rts.height:0.00} / {h0:0.00})",
                  cam.fieldOfView == fov0 && cam.nearClipPlane == near0 && rts.height == h0);
            var uv = ViewOf(u); float off = uv ? new Vector2(rts.focus.x - uv.transform.position.x, rts.focus.z - uv.transform.position.z).magnitude : 99f;
            Check($"G out: the last picture is the RTS camera's pose ({(samples.Count > 0 ? (samples[samples.Count - 1].p - rtsNow).magnitude : -1f):0.000} m) and its focus on the unit ({off:0.00} m)",
                  samples.Count > 0 && (samples[samples.Count - 1].p - rtsNow).magnitude < 0.05f && off < 1.5f);
            Check($"G out: one whoosh ({Plays(OzFpsGlideSound.Out) - wh1}) and one ui_cancel ({Plays("ui_cancel") - cn1})", Plays(OzFpsGlideSound.Out) - wh1 == 1 && Plays("ui_cancel") - cn1 == 1);   // 1.5 fps_feel_whoosh_vignette
            Check($"G out: no vignette in the RTS view (weight {OzFpsVignette.Weight:0.000}, volume {(OzFpsVignette.VolumeOn ? "on" : "off")})", OzFpsVignette.Weight <= 0.001f && !OzFpsVignette.VolumeOn);   // 1.5 fps_feel_whoosh_vignette
        }

        // ---- T: turn round both ways
        IEnumerator TurnRows()
        {
            if (!FindSpot(out int x, out int y, false)) { Check("T: a free place near the camera", false); yield break; }
            var u = Make("robot", "grunt", x, y, me, 1); Focus(u); yield return Real(0.8f);
            var start = cam.transform.position; int rev0 = OzFpsFeel.Reversals;
            samples.Clear(); sampling = true;
            Select(u); yield return AltF(); yield return Real(0.3f); yield return AltF();
            yield return WaitFor(() => !OzFpsView.On, 2.5f); yield return Frames(3); sampling = false;
            float back = (cam.transform.position - start).magnitude, jump = SpeedJump(samples, 0, out float peak);
            Check($"T in -> out: turned round ({OzFpsFeel.Reversals - rev0}), back at the player's own place ({back:0.00} m off), speed change per picture {jump:0.0} m/s of {peak:0.0} m/s at most (half or less)",
                  OzFpsFeel.Reversals - rev0 >= 1 && back < 0.3f && jump <= 0.5f * peak + 1e-3f);
            NoJump("T in -> out");

            yield return In(u); yield return Real(0.3f);
            float yaw0 = OzFpsView.I ? OzFpsView.I.Yaw : 0f; int re0 = OzFpsFeel.ReEntries;
            samples.Clear(); sampling = true;
            yield return Esc(); yield return Real(0.3f); yield return AltF();
            yield return WaitFor(() => OzFpsView.On && !OzFpsFeel.Gliding, 2.5f); yield return Frames(3); sampling = false;
            bool on = OzFpsView.On && OzFpsView.I && OzFpsView.I.Unit == u; float dyaw = on ? Mathf.Abs(Mathf.DeltaAngle(OzFpsView.I.Yaw, yaw0)) : 999f;
            jump = SpeedJump(samples, 0, out peak);
            Check($"T out -> in: in again on the same unit ({on}, turned round {OzFpsFeel.ReEntries - re0}), the look kept ({dyaw:0.0} deg), speed change per picture {jump:0.0} of {peak:0.0} m/s",
                  on && OzFpsFeel.ReEntries - re0 >= 1 && dyaw <= 2f && jump <= 0.5f * peak + 1e-3f);
            NoJump("T out -> in");
            yield return Out();
        }

        // ---- I: a leave that belongs to the game is instant
        IEnumerator InstantRow()
        {
            if (!FindSpot(out int x, out int y, false)) { Check("I: a free place near the camera", false); yield break; }
            var u = Make("robot", "grunt", x, y, me, 1); Focus(u); yield return Real(0.8f);
            float fov0 = cam.fieldOfView, near0 = cam.nearClipPlane;
            yield return In(u); yield return Real(0.3f);
            if (OzFpsView.I) OzFpsView.I.Leave("a scene has the camera");
            yield return null;
            Check($"I: a scene takes the camera: no glide ({!OzFpsFeel.Gliding}), the view off ({!OzFpsView.On}), the RTS lens at once ({cam.fieldOfView:0.00} / {fov0:0.00}, {cam.nearClipPlane:0.000} / {near0:0.000})",
                  !OzFpsFeel.Gliding && !OzFpsView.On && cam.fieldOfView == fov0 && cam.nearClipPlane == near0);
            yield return Frames(3);
        }

        // ---- F: units by the eye fade
        int viewHeldTeamLit; HashSet<Renderer> watchTeamLit;
        void AtRender(Camera c)
        {
            if (watchTeamLit == null) return;
            foreach (var r in watchTeamLit) if (r && r.forceRenderingOff) viewHeldTeamLit++;
        }
        static List<Renderer> TeamLitOf(Transform t) => t ? t.GetComponentsInChildren<Renderer>(true).Where(r => OzShuttleCamFade.TakesFade(r)).ToList() : new List<Renderer>();

        IEnumerator FadeRows()
        {
            if (!FindSpot(out int x, out int y, false)) { Check("F: a free place near the camera", false); yield break; }
            var u = Make("robot", "grunt", x, y, me, -1); Focus(u); yield return Real(0.8f);
            var mates = u.Minions.Where(m => m != null).Select(ViewOf).Where(v => v).ToList();
            watchTeamLit = new HashSet<Renderer>(mates.SelectMany(v => TeamLitOf(v.transform))); viewHeldTeamLit = 0;
            OzFpsView.AtRender = AtRender;
            yield return In(u); yield return Real(0.8f);
            var eye = cam.transform.position; var rows = new List<string>(); bool nearOk = true; int near = 0;
            foreach (var v in mates)
            {
                float d = OzShuttleCamFade.BoxDistance(TeamLitOf(v.transform), eye), c = OzFpsFeel.CutOf(v.transform);
                rows.Add($"{v.name} {d:0.00} m cut {c:0.00}");
                if (d <= 1.0f) { near++; nearOk &= c >= 0.9f; }
            }
            Check($"F squad: {mates.Count} mates, none of their TeamLit parts held off by the view in any picture ({viewHeldTeamLit}), those within 1 m of the eye cut 0.9 or more ({near}: {string.Join("; ", rows)})",
                  mates.Count >= 1 && viewHeldTeamLit == 0 && nearOk);

            // a grunt walks up to the eye and away again
            var fv = OzFpsView.I; float yawR = fv ? fv.Yaw * Mathf.Deg2Rad : 0f;
            int wx = u.CenterX + Mathf.RoundToInt(Mathf.Sin(yawR) * 56f), wy = u.CenterY - Mathf.RoundToInt(Mathf.Cos(yawR) * 56f);
            if (!Free(wx, wy, false)) { wx = u.CenterX + 56; wy = u.CenterY; }
            var w2 = Make("robot", "grunt", wx, wy, me, 1); var w2v = ViewOf(w2);
            yield return Frames(2);
            watchTeamLit = new HashSet<Renderer>(TeamLitOf(w2v ? w2v.transform : null)); viewHeldTeamLit = 0;
            W.SendWaypoints(me, w2, new[] { ZWaypoint.Move(u.CenterX + 6, u.CenterY + 6) });
            float maxRise = 0f, peakCut = 0f, minD = 99f, prev = 0f; float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 6f && w2v)
            {
                yield return null;
                float c = OzFpsFeel.CutOf(w2v.transform); maxRise = Mathf.Max(maxRise, Mathf.Abs(c - prev)); prev = c; peakCut = Mathf.Max(peakCut, c);
                minD = Mathf.Min(minD, OzShuttleCamFade.BoxDistance(TeamLitOf(w2v.transform), cam.transform.position));
            }
            Check($"F walker in: nearest {minD:0.00} m, its cut reached {peakCut:0.00} (0.8 or more), at most {maxRise:0.00} in a picture (no snap: 0.35 or less), never held off by the view ({viewHeldTeamLit})",
                  minD <= 1.6f && peakCut >= 0.8f && maxRise <= 0.35f && viewHeldTeamLit == 0);
            W.SendWaypoints(me, w2, new[] { ZWaypoint.Move(wx, wy) });                                         // back to where it came from (free ground, 7 m off)
            float maxFall = 0f; t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 4f && w2v)
            {
                yield return null;
                float c = OzFpsFeel.CutOf(w2v.transform); maxFall = Mathf.Max(maxFall, Mathf.Abs(c - prev)); prev = c;
            }
            float left = w2v ? TeamLitOf(w2v.transform).Select(r => OzShuttleCamFade.ReadFade(r)).DefaultIfEmpty(-1f).Max() : -1f;
            Check($"F walker out: its cut at most {maxFall:0.00} in a picture (0.35 or less), at the end {prev:0.00}, its parts' cut {left:0.000} (0)", maxFall <= 0.35f && prev <= 0.001f && left <= 0.0005f);
            OzFpsView.AtRender = null; watchTeamLit = null;
            yield return Out(); yield return Real(1.0f);
            float worst = 0f; string who = "-";
            foreach (var v in g.LiveViewValues)
            {
                if (!v || v.Obj == null || v.Obj.IsShuttle) continue;
                foreach (var r in TeamLitOf(v.transform)) { float c = OzShuttleCamFade.ReadFade(r); if (c > worst) { worst = c; who = v.name + "/" + r.name; } }
            }
            Check($"F after the leave: no unit carries a cut ({worst:0.000}, {who}), no entry left ({OzFpsFeel.FadeEntries}), nothing held ({OzFpsFeel.HeldNow})", worst <= 0.0005f && OzFpsFeel.FadeEntries == 0 && OzFpsFeel.HeldNow == 0);
        }

        // ---- W: the weapon in range eases whiter
        IEnumerator WhitenRows()
        {
            if (!FindSpot(out int x, out int y, false)) { Check("W: a free place near the camera", false); yield break; }
            var u = Make("robot", "grunt", x, y, me, 1); Focus(u); yield return Real(0.8f);
            yield return In(u); yield return Real(0.5f);
            var fv = OzFpsView.I; float yawR = fv ? fv.Yaw * Mathf.Deg2Rad : 0f; float sx = Mathf.Sin(yawR), sy = -Mathf.Cos(yawR);
            int far = u.AttackRadius + 140, inR = Mathf.Max(48, u.AttackRadius * 6 / 10);
            var d1 = W.CreateObject("vehicle", "heavy", u.CenterX + Mathf.RoundToInt(sx * far) - 16, u.CenterY + Mathf.RoundToInt(sy * far) - 16, foe);
            if (d1 != null) { d1.Damage = 0; g.SpawnUnitView(d1); made.Add(d1); }                             // a dummy: it never fires (test only)
            yield return Real(1.0f);
            float em0 = GunEm();
            Check($"W out of reach ({far} px, reach {u.AttackRadius} px): the copy as it is (whiter {OzFpsFeel.WhitenNow:0.000}, in reach {OzFpsFeel.InRangeNow}), its emission {em0:0.000}",
                  d1 != null && OzFpsFeel.WhitenNow <= 0.001f && !OzFpsFeel.InRangeNow);
            Drop(d1);
            int lock0 = Plays("ui_target_lock");
            var d2 = W.CreateObject("vehicle", "heavy", u.CenterX + Mathf.RoundToInt(sx * inR) - 16, u.CenterY + Mathf.RoundToInt(sy * inR) - 16, foe);
            if (d2 != null) { d2.Damage = 0; g.SpawnUnitView(d2); made.Add(d2); }
            float t0 = Time.unscaledTime, firstUp = -1f, full = -1f, maxStep = 0f, prev = OzFpsFeel.WhitenNow;
            while (Time.unscaledTime - t0 < 1.2f)
            {
                yield return null;
                float w = OzFpsFeel.WhitenNow, t = Time.unscaledTime - t0; maxStep = Mathf.Max(maxStep, Mathf.Abs(w - prev)); prev = w;
                if (firstUp < 0f && w > 0.02f) firstUp = t;
                if (full < 0f && w >= 0.98f) full = t;
            }
            float em1 = GunEm();
            Check($"W in reach ({inR} px): whiter fully at {full:0.00} s (0.7 or less), {full - firstUp:0.00} s from start to full (0.2 or more), at most {maxStep:0.00} in a picture (0.2), the lock blip {Plays("ui_target_lock") - lock0}, emission {em0:0.000} -> {em1:0.000}",
                  d2 != null && full >= 0f && full <= 0.7f && full - firstUp >= 0.2f && maxStep <= 0.2f && Plays("ui_target_lock") - lock0 == 1 && em1 > em0 + 0.02f);
            Drop(d2);
            t0 = Time.unscaledTime; float gone = -1f; maxStep = 0f;
            while (Time.unscaledTime - t0 < 1.2f)
            {
                yield return null;
                float w = OzFpsFeel.WhitenNow; maxStep = Mathf.Max(maxStep, Mathf.Abs(w - prev)); prev = w;
                if (gone < 0f && w <= 0.001f) gone = Time.unscaledTime - t0;
            }
            float em2 = GunEm();
            Check($"W out again: back at {gone:0.00} s (0.7 or less), at most {maxStep:0.00} in a picture, emission {em2:0.0000} (its own {em0:0.0000})", gone >= 0f && gone <= 0.7f && maxStep <= 0.2f && Mathf.Abs(em2 - em0) < 1e-4f);
            yield return Out();
        }

        // ---- S: Shift
        IEnumerator SprintRows(string cat, string type, bool vehicle)
        {
            if (!FindSpot(out int x, out int y, vehicle)) { Check($"S {type}: a free place near the camera", false); yield break; }
            var u = Make(cat, type, x, y, me, vehicle ? 1 : -1); Focus(u); yield return Real(0.8f);
            yield return In(u); yield return Real(0.3f);
            Vector2 P() => new Vector2(u.CenterX, u.CenterY);
            Keys(Fwd); yield return Real(0.4f);
            var a = P(); float ta = Time.unscaledTime; yield return Real(0.6f);
            float v0 = (P() - a).magnitude / Mathf.Max(1e-3f, Time.unscaledTime - ta);
            int mo = OzFpsView.MoveOrders, so = OzFpsFeel.SprintOrders;
            Keys(Fwd, Key.LeftShift); yield return Frames(3);
            bool quick = OzFpsView.MoveOrders > mo && OzFpsFeel.SprintOrders > so;
            yield return Real(0.4f);
            a = P(); ta = Time.unscaledTime; float spread = 0f;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.6f)
            {
                yield return null;
                foreach (var m in u.Minions) if (m != null && !m.Destroyed) spread = Mathf.Max(spread, new Vector2(m.CenterX - u.CenterX, m.CenterY - u.CenterY).magnitude / 8f);
            }
            float v1 = (P() - a).magnitude / Mathf.Max(1e-3f, Time.unscaledTime - ta);
            Check($"S {type}: W {v0:0.0} px/s, W + Shift {v1:0.0} px/s (x {(v0 > 0f ? v1 / v0 : 0f):0.00}, 1.3 or more), the order at once ({quick}){(vehicle ? "" : $", the squad together (members {spread:0.0} m from the leader, 4 or less)")}",
                  v0 > 0f && v1 >= 1.3f * v0 && quick && (vehicle || spread <= 4f));
            if (!vehicle)
            {   // 'Zod exact': the rule's switch off, Shift changes nothing
                bool was = ZWorld.SprintOrderDeviation; ZWorld.SprintOrderDeviation = false;
                Keys(Fwd); yield return Real(0.5f);
                a = P(); ta = Time.unscaledTime; yield return Real(0.5f);
                float p0 = (P() - a).magnitude / Mathf.Max(1e-3f, Time.unscaledTime - ta);
                int so2 = OzFpsFeel.SprintOrders;
                Keys(Fwd, Key.LeftShift); yield return Real(0.5f);
                a = P(); ta = Time.unscaledTime; yield return Real(0.5f);
                float p1 = (P() - a).magnitude / Mathf.Max(1e-3f, Time.unscaledTime - ta);
                ZWorld.SprintOrderDeviation = was;
                Check($"S {type}, the switch off: no sprint order ({OzFpsFeel.SprintOrders - so2}), the speed as before ({p0:0.0} -> {p1:0.0} px/s)", OzFpsFeel.SprintOrders == so2 && p0 > 0f && p1 <= 1.15f * p0 && p1 >= 0.85f * p0);
            }
            Keys(); yield return Real(0.3f);
            yield return Out();
        }

        void Finish()
        {
            sampling = false; Devices(false); OzFpsView.AtRender = null;
            Report = $"{(fail == 0 && pass > 0 ? "PASS" : "FAIL")} {pass} passed, {fail} failed";
            Log(Report);
            try { File.WriteAllText(Path.Combine(Folder(), "report_feel.txt"), log.ToString()); } catch (IOException) { }
            Destroy(gameObject);
        }
    }
}
#endif
