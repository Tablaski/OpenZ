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
    /// 1.5 kit fps_shuttle_badges: the Play check of the private first-person view on the shuttle (View/OzFpsShuttleView.cs) and of the viewed
    /// (the check presses its own keys: a test keyboard and mouse made current for its run), about 3 minutes; the match goes on.
    ///  (kit fps_feel in: its glide and fade are off for the R and B rows, see Feel, and on again from the F row)
    ///  R  reproduction (the fix off, OzFpsBadgeHold.On false = 1.4): a lone veteran Pyro, the view on, the look at its own chevron: the view's
    ///     picture against the same picture with every renderer the view holds and the chevrons switched off for real: REPRODUCED (the chevrons
    ///     are in the picture: the cause is the one found) or NOT REPRODUCED (INFO: then the cause is another; the B rows still decide)
    ///  B  badges, every robot type (grunt, psycho, sniper, tough, pyro, laser, electrobot): a lone veteran robot of the type, its chevrons on its
    ///     shoulders, the view on, the look at its own chevron: each chevron carries a block (taken by the view's hold) and was held in the last
    ///     picture; by PIXELS the view's picture is the same as with the held renderers and the chevrons switched off for real (at most its noise
    ///     + 0.05 % of the screen); after Esc each chevron is exactly as before (on, not held, no block when it had none), nothing of the kit left
    ///  M  mates: a veteran Pyro squad, the view on the leader: every mate's chevrons stay on that mate (under its own view, on, not held between
    ///     the pictures)
    ///  S  shuttle: one placed on open ground near the camera (the testing panel's way: ZWorld.SpawnShuttle, OzPlayableShuttle.SpawnView); Alt+F
    ///     goes in (was refused); the eye NoseClearM past the nose's reach; W held: it takes off (Climb, Cruise, at least 8 m up within 8 s),
    ///     cruises at 80 % of its top speed or more (no crawl from planned stops), sends at most 3 orders a second; the mouse turns the look by
    ///     about 90 deg: the ship's heading follows by 45 deg or more within 5 s; W let go: it lands (or hovers: INFO with the rules' reason)
    ///     within 12 s; every sampled frame the camera at least 0.3 m from every measured part box of the ship; Esc: out, nothing held
    /// No PASS without its samples; errors and exceptions logged during the run fail it. Report: <see cref="Folder"/>/report_shuttle_badges.txt.
    /// Test only: the units it made are taken out again; a switch it set for the run (the veteran rule) is put back.
    /// </summary>
    public sealed class OzFpsShuttleBadgesPlayCheck : MonoBehaviour
    {
        public static string Report = "idle";

        public static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var k = Path.Combine(d.FullName, "work", "staging", "v1_5", "kits", "fps_shuttle_badges");
                if (Directory.Exists(k)) return Path.Combine(k, "proof", "play");
            }
            return Path.Combine(Application.persistentDataPath, "fps_shuttle_badges_play_check");
        }

        public static void Run() { Report = "running"; new GameObject("OzFpsShuttleBadgesPlayCheck").AddComponent<OzFpsShuttleBadgesPlayCheck>(); }

        static readonly string[] Types = { "grunt", "psycho", "sniper", "tough", "pyro", "laser", "electrobot" };
        ZGame g; ZWorld W; RtsCamera rts; PlayerInput pi; Camera cam; Team me; int pass, fail, errors;
        readonly List<string> errorLines = new List<string>();
        Keyboard proofKb, realKb; Mouse proofMouse, realMouse;
        readonly StringBuilder log = new StringBuilder();
        readonly List<ZObject> made = new List<ZObject>();
        HashSet<int> startIds;

        void Start() { Application.logMessageReceived += OnLog; StartCoroutine(All()); }
        void OnDestroy() { Application.logMessageReceived -= OnLog; Devices(false); OzFpsView.CheckDrawOwn = false; OzFpsBadgeHold.On = true; Feel(true); }
        void OnLog(string msg, string st, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) { errors++; if (errorLines.Count < 6) errorLines.Add(msg.Split('\n')[0]); } }

        void Log(string s) { log.AppendLine(s); Debug.Log("[OpenZ FPS shuttle check] " + s); }
        void Check(string what, bool ok) { if (ok) pass++; else fail++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < s) yield return null; }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator WaitFor(System.Func<bool> c, float max) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < max && !c()) yield return null; }

        // ------------------------------------------------------------------ the test keyboard and mouse
        void Devices(bool on)
        {
            if (on)
            {
                realKb = Keyboard.current; realMouse = Mouse.current;
                proofKb = InputSystem.AddDevice<Keyboard>("OzFpsShuttleProofKeyboard"); proofKb.MakeCurrent();
                proofMouse = InputSystem.AddDevice<Mouse>("OzFpsShuttleProofMouse"); proofMouse.MakeCurrent();
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
        void MouseDelta(Vector2 d)
        {
            if (proofMouse == null) return;
            InputSystem.QueueStateEvent(proofMouse, new MouseState { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), delta = d });
        }

        // ------------------------------------------------------------------ places and units
        bool Free(int x, int y, bool vehicle) =>
            !W.Path.WithinImpassable(x - 8, y - 8, 16, 16, out _, out _, true) && !W.Map.LavaAt(x >> 4, y >> 4)
            && (!vehicle || !W.Path.WithinImpassable(x - 16, y - 16, 32, 32, out _, out _, false))
            && !W.UnitObjects.Any(o => o != null && !o.Destroyed && System.Math.Abs(o.CenterX - x) < 40 && System.Math.Abs(o.CenterY - y) < 40);

        bool Spot(out int x, out int y, int disc)
        {
            int fx = (int)(rts.focus.x * 8f), fy = (int)(-rts.focus.z * 8f), mw = (int)(rts.mapSizeMeters.x * 8f), mh = (int)(rts.mapSizeMeters.y * 8f), edge = 320;
            for (int r = 0; r < 1600; r += 24)
                for (int a = 0; a < 16; a++)
                {
                    x = fx + (int)(r * Mathf.Cos(a * 0.3927f)); y = fy + (int)(r * Mathf.Sin(a * 0.3927f));
                    bool ok = x > edge && y > edge && x < mw - edge && y < mh - edge;
                    for (int k = 0; ok && k < 16; k++) for (int rr = 0; ok && rr <= disc; rr += 24) ok = Free(x + (int)(rr * Mathf.Cos(k * 0.3927f)), y + (int)(rr * Mathf.Sin(k * 0.3927f)), false);
                    if (ok) return true;
                }
            x = y = 0; return false;
        }

        bool FindSpot(out int x, out int y)
        {
            Clean();
            foreach (int disc in new[] { 120, 72, 48 }) if (Spot(out x, out y, disc)) return true;
            x = y = 0; return false;
        }

        ZObject Make(string type, int x, int y, int amount)
        {
            var o = W.CreateRobotGroup(type, x - 8, y - 8, me, amount);
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

        static List<MeshRenderer> Chevrons(UnitView v)
        {
            var l = new List<MeshRenderer>();
            if (v) foreach (var r in v.GetComponentsInChildren<MeshRenderer>(true)) if (r && r.name == "OZ_VetChevron") l.Add(r);
            return l;
        }

        /// <summary>Kit fps_feel (View/OzFpsFeel.cs), when it is in: its glide and its fade on or off for a row (test only, by name: this kit
        /// compiles without it). Its fade writes a block of its own on the viewed unit's TeamLit parts during the glide, which alone takes them off
        /// the GPU Resident Drawer: the R and B rows run without it so they measure the cause and this kit's fix alone.</summary>
        static readonly System.Type FeelType = System.Type.GetType("OpenZ.OzFpsFeel");
        static void Feel(bool on)
        {
            if (FeelType == null) return;
            foreach (var n in new[] { "GlideOn", "FadeOn" })
            {
                var f = FeelType.GetField(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (f != null && f.FieldType == typeof(bool)) f.SetValue(null, on);
            }
        }

        /// <summary>A renderer's state as the view must leave it: on, held off, carrying a block.</summary>
        static (bool on, bool held, bool block) State(MeshRenderer r) => r ? (r.enabled, r.forceRenderingOff, r.HasPropertyBlock()) : (false, false, false);

        void Veteran(ZObject o)
        {
            o.Veteran = true; foreach (var m in o.Minions) if (m != null) m.Veteran = true;                    // test only: the crew's rank, as 4 kills give it (no pop: PromotedAt stays -1)
        }

        /// <summary>The view looks at world point p (the view's own LookBy, so its yaw and pitch stay its own; pitch within the view's limits).</summary>
        static void LookAt(Vector3 p)
        {
            var fv = OzFpsView.I; if (!fv || !fv.Cam) return;
            var d = p - fv.Cam.transform.position; if (d.sqrMagnitude < 1e-6f) return;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(d.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, OzFpsView.PitchMin, OzFpsView.PitchMax);
            float k = Mathf.Max(1e-4f, RtsCamera.FreeLookSpeed);
            fv.LookBy(new Vector2(Mathf.DeltaAngle(fv.Yaw, yaw) / k, (pitch - fv.Pitch) / k));
        }

        // ------------------------------------------------------------------ pixels (the 1.4 check's way: the game still, no anti-aliasing, the camera pinned for C)
        static float Differ(Texture2D a, Texture2D b)
        {
            if (!a || !b || a.width != b.width || a.height != b.height) return 1f;
            var pa = a.GetPixels32(); var pb = b.GetPixels32(); int w = a.width, h = a.height, n = 0;
            bool Near(Color32 c1, Color32 c2) => Mathf.Abs(c1.r - c2.r) <= 24 && Mathf.Abs(c1.g - c2.g) <= 24 && Mathf.Abs(c1.b - c2.b) <= 24;
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    var p = pa[y * w + x]; if (Near(p, pb[y * w + x])) continue;
                    bool any = false;                                                                                 // a 1 px shift allowed (sub-pixel edges)
                    for (int dy = -1; dy <= 1 && !any; dy++) for (int dx = -1; dx <= 1 && !any; dx++) any = Near(p, pb[(y + dy) * w + x + dx]);
                    if (!any) n++;
                }
            return n / (float)(w * h);
        }

        /// <summary>A (the view's picture), A2 (the same again: its noise), B (the hold lifted: the unit drawn), C (every renderer held in the last
        /// picture and the chevrons switched off for real, the camera pinned to A's pose). Returns (A vs C, A vs A2, B vs C).</summary>
        IEnumerator Pixels(string tag, List<MeshRenderer> chev, System.Action<float, float, float, int> done)
        {
            var fv = OzFpsView.I;
            var cd = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); var aa0 = cd ? cd.antialiasing : UnityEngine.Rendering.Universal.AntialiasingMode.None;
            bool paused0 = g.paused; float ts0 = Time.timeScale;
            g.paused = true; Time.timeScale = 0f; if (cd) cd.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            yield return Frames(6);
            yield return new WaitForEndOfFrame(); var a = ScreenCapture.CaptureScreenshotAsTexture();
            yield return Frames(2); yield return new WaitForEndOfFrame(); var a2 = ScreenCapture.CaptureScreenshotAsTexture();
            var held = new List<Renderer>(); if (fv) fv.HeldInLastPicture(held);
            Vector3 camP = cam.transform.position; Quaternion camR = cam.transform.rotation;
            OzFpsView.CheckDrawOwn = true; yield return Frames(2); yield return new WaitForEndOfFrame(); var b = ScreenCapture.CaptureScreenshotAsTexture(); OzFpsView.CheckDrawOwn = false;
            System.Action<ScriptableRenderContext, Camera> pin = (ctx, cc) => { if (cc == cam) cc.transform.SetPositionAndRotation(camP, camR); };
            RenderPipelineManager.beginCameraRendering += pin;
            var off = new List<Renderer>(held); foreach (var r in chev) if (r && !off.Contains(r)) off.Add(r);
            var was = new List<bool>(off.Count); foreach (var r in off) { was.Add(r && r.enabled); if (r) r.enabled = false; }
            yield return Frames(2); yield return new WaitForEndOfFrame(); var c = ScreenCapture.CaptureScreenshotAsTexture();
            RenderPipelineManager.beginCameraRendering -= pin;
            for (int i = 0; i < off.Count; i++) if (off[i]) off[i].enabled = was[i];
            if (cd) cd.antialiasing = aa0; Time.timeScale = ts0; g.paused = paused0;
            float ac = Differ(a, c), aa = Differ(a, a2), bc = Differ(b, c);
            try
            {
                File.WriteAllBytes(Path.Combine(Folder(), "fps_" + tag + "_A.png"), a.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(Folder(), "fps_" + tag + "_B.png"), b.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(Folder(), "fps_" + tag + "_C.png"), c.EncodeToPNG());
            }
            catch (IOException) { }
            Destroy(a); Destroy(a2); Destroy(b); Destroy(c);
            yield return Frames(2);
            done(ac, aa, bc, held.Count);
        }

        // ------------------------------------------------------------------ the run
        IEnumerator All()
        {
            g = ZGame.I; W = g ? g.World : null; pi = FindFirstObjectByType<PlayerInput>(); rts = pi ? pi.cam : null; cam = rts ? rts.GetComponent<Camera>() : null;
            if (W == null || !rts || !pi || !cam) { Log("no level in Play"); fail++; Finish(); yield break; }
            Directory.CreateDirectory(Folder());
            me = g.player;
            yield return WaitFor(() => OpenZHud.ControlAt > 0f && Time.unscaledTime >= OpenZHud.ControlAt + 1f, 40f);
            W.Bots.Clear();
            startIds = new HashSet<int>(W.UnitObjects.Where(o => o != null).Select(o => o.Id));
            var urp = GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            Log($"FPS SHUTTLE + BADGES CHECK level {g.level}, GPU Resident Drawer {(urp ? urp.gpuResidentDrawerMode.ToString() : "?")}, veteran rule {ZWorld.VeteranDeviation}, chevrons {VeteranMarks.Chevrons}, shuttle rule {ZWorld.ShuttlePlayableDeviation}");
            bool vet0 = ZWorld.VeteranDeviation;
            if (!vet0) { ZWorld.VeteranDeviation = true; Log("INFO the veteran rule was off ('Zod exact'?): on for this run, put back after"); }
            Devices(true);
            if (OzFpsView.On && OzFpsView.I) OzFpsView.I.Leave("the check starts");
            yield return Frames(3);

            Log($"INFO kit fps_feel {(FeelType != null ? "is in: its glide and fade are switched off for the R and B rows (its fade's own block would hide the cause), on again for the F row and the shuttle" : "is not in")}");
            Feel(false);
            yield return BadgeRow("pyro", false, "R");                                                            // R: the fix off (1.4)
            foreach (var t in Types) yield return BadgeRow(t, true, "B");                                         // B: every robot type, this kit alone
            Feel(true);
            if (FeelType != null) yield return BadgeRow("pyro", true, "F");
            yield return MatesRow();
            yield return ShuttleRows();

            Clean();
            ZWorld.VeteranDeviation = vet0; OzFpsBadgeHold.On = true;
            Log(OzFpsBadgeHold.Status()); Log(OzFpsShuttleView.Status());
            if (errors > 0) Check($"no error or exception logged during the run ({errors}: {string.Join(" | ", errorLines)})", false);
            Finish();
        }

        IEnumerator In(ZObject u)
        {
            Select(u); yield return AltF();
            yield return WaitFor(() => OzFpsView.On, 2f);
            yield return Real(1.2f);                                                                              // kit fps_feel's glide in (0.75 s) is over
        }

        IEnumerator Out()
        {
            yield return Esc();
            yield return WaitFor(() => !OzFpsView.On, 2.5f);                                                       // kit fps_feel: after its glide out
            yield return Frames(3);
        }

        // ---- R / B: the viewed robot's own badges
        IEnumerator BadgeRow(string type, bool fix, string row)
        {
            string tag = row + " " + type;
            if (!FindSpot(out int x, out int y)) { Check(tag + ": a free place near the camera", false); yield break; }
            var u = Make(type, x, y, 1); if (u == null) { Check(tag + ": the robot was made", false); yield break; }
            Veteran(u); Focus(u);
            var v = ViewOf(u);
            yield return WaitFor(() => Chevrons(ViewOf(u)).Count > 0, 3f);
            var chev = Chevrons(v);
            if (chev.Count == 0) { Check($"{tag}: VeteranMarks put its chevrons on the veteran (none found under its view)", false); Drop(u); yield break; }
            var before = chev.Select(State).ToList();
            OzFpsBadgeHold.On = fix;
            yield return In(u);
            if (!OzFpsView.On || !OzFpsView.I || OzFpsView.I.Unit != u) { Check($"{tag}: Alt+F went in ({OzFpsView.LastRefusal})", false); OzFpsBadgeHold.On = true; Drop(u); yield break; }
            var near = chev.OrderBy(r => (r.bounds.center - cam.transform.position).sqrMagnitude).First();
            LookAt(near.bounds.center); yield return Frames(3);
            var lastHeld = new List<Renderer>(); OzFpsView.I.HeldInLastPicture(lastHeld);
            int blocks = chev.Count(r => r.HasPropertyBlock()), heldN = chev.Count(r => lastHeld.Contains(r));
            float ac = 0f, aa = 0f, bc = 0f; int nHeld = 0;
            yield return Pixels(row + "_" + type, chev, (p, q, s, n) => { ac = p; aa = q; bc = s; nHeld = n; });
            string pix = $"the view's picture against the same picture with the {nHeld} held renderers and the {chev.Count} chevrons switched off for real: {ac * 100f:0.000} % of the screen differs (its noise {aa * 100f:0.000} %); the unit drawn (hold lifted) shows on {bc * 100f:0.00} %";
            if (!fix)
            {
                bool rep = ac > aa + 0.0005f;
                Log($"INFO {tag} (fix off = 1.4): {(rep ? "REPRODUCED: the chevrons are in the picture while the body is held (the cause found: the GPU Resident Drawer's renderers are not reached by the per-picture hold)" : "NOT REPRODUCED: the chevrons are not in the picture with the fix off, so the GPU Resident Drawer is not the cause on this machine; the B rows still decide")}; {pix}; chevrons with a block {blocks} of {chev.Count}, held in the last picture {heldN}");
            }
            else
            {
                Check($"{tag}: every chevron carries a block (the view's hold takes it): {blocks} of {chev.Count}", blocks == chev.Count);
                Check($"{tag}: every chevron was held in the view's last picture: {heldN} of {chev.Count}", heldN == chev.Count);
                if (bc >= 0.005f) Check($"{tag}: by PIXELS nothing of it floats in the view: {pix} (at most noise + 0.05 %)", ac <= aa + 0.0005f);
                else Log($"INFO {tag}: the unit is not in this picture, so its pixels prove nothing either way: {pix}");
            }
            yield return Out();
            OzFpsBadgeHold.On = true;
            var after = chev.Select(State).ToList();
            bool same = after.Count == before.Count && !after.Where((st, i) => st != before[i]).Any();
            if (fix) Check($"{tag}: after Esc every chevron is exactly as before (on {after.Count(st => st.on)}/{after.Count}, held {after.Count(st => st.held)}, with a block {after.Count(st => st.block)} (before {before.Count(st => st.block)})); kit blocks left {OzFpsBadgeHold.MarkedNow}", same && OzFpsBadgeHold.MarkedNow == 0);
            Drop(u); made.Remove(u);
            yield return Frames(2);
        }

        // ---- M: the mates keep their badges on their own shoulders
        IEnumerator MatesRow()
        {
            if (!FindSpot(out int x, out int y)) { Check("M: a free place near the camera", false); yield break; }
            var u = Make("pyro", x, y, -1); if (u == null) { Check("M: the Pyro squad was made", false); yield break; }
            Veteran(u); Focus(u);
            var mates = u.Minions.Where(m => m != null).ToList();
            yield return WaitFor(() => mates.All(m => Chevrons(ViewOf(m)).Count > 0) && Chevrons(ViewOf(u)).Count > 0, 3f);
            yield return In(u);
            if (!OzFpsView.On) { Check($"M: Alt+F went in ({OzFpsView.LastRefusal})", false); Drop(u); yield break; }
            int seen = 0, onOwn = 0, drawn = 0;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 1.5f)
            {
                foreach (var m in mates)
                {
                    var mv = ViewOf(m);
                    foreach (var r in Chevrons(mv)) { seen++; if (r.transform.IsChildOf(mv.transform)) onOwn++; if (r.enabled && !r.forceRenderingOff) drawn++; }
                }
                yield return null;
            }
            Check($"M: {mates.Count} mates' chevrons stay on their own shoulders (under their own view {onOwn} of {seen} samples) and drawn between the pictures ({drawn} of {seen})", seen > 0 && onOwn == seen && drawn == seen);
            yield return Out();
            Drop(u); made.Remove(u);
        }

        // ---- S: the shuttle
        IEnumerator ShuttleRows()
        {
            if (!ZWorld.ShuttlePlayableDeviation) { Log("INFO S: the shuttle rule is off ('Zod exact'): no shuttle rows"); yield break; }
            Clean();
            ZObject s = null;
            var c = rts.focus; int cx = Mathf.RoundToInt(c.x * 8f), cy = Mathf.RoundToInt(-c.z * 8f);
            for (int r = 120; r <= 900 && s == null; r += 48)
                for (int a = 0; a < 360 && s == null; a += 30)
                {
                    var q = W.ShuttleClamp(cx + (int)(Mathf.Cos(a * Mathf.Deg2Rad) * r), cy + (int)(Mathf.Sin(a * Mathf.Deg2Rad) * r));
                    s = W.SpawnShuttle(q.x, q.y, me, rts.yaw - 90f, out _);
                }
            if (s == null) { Check("S: a shuttle placed on open ground near the camera", false); yield break; }
            made.Add(s); OzPlayableShuttle.SpawnView(g, s); Focus(s);
            yield return Real(1f);
            var v = ViewOf(s);
            Check($"S: the shuttle #{s.Id} has its view and its pilot (drivers {s.Drivers.Count})", v && s.Drivers.Count > 0);
            yield return In(s);
            Check($"S: Alt+F on the shuttle goes in (before this kit: refused) ({OzFpsView.LastRefusal})", OzFpsView.On && OzFpsView.I && OzFpsView.I.Unit == s);
            if (!OzFpsView.On) { Drop(s); yield break; }
            Log("INFO S: " + OzFpsShuttleView.Status());
            float past = OzFpsShuttleView.EyePastReachM(v, cam.transform.position);
            Check($"S: the eye sits {past:0.00} m past the nose's farthest part along the nose (want {OzFpsShuttleView.NoseClearM:0.0} +- 0.1; {OzFpsShuttleView.LastParts} parts measured, the farthest {OzFpsShuttleView.LastNosePart})", OzFpsShuttleView.LastParts > 0 && Mathf.Abs(past - OzFpsShuttleView.NoseClearM) <= 0.1f);
            float minPart = float.PositiveInfinity; int frames = 0;
            System.Action sample = () => { minPart = Mathf.Min(minPart, OzFpsShuttleView.NearestPartM(v, cam.transform.position)); frames++; };

            // take-off and cruise: W held
            int orders0 = OzFpsView.MoveOrders; float t0 = Time.unscaledTime; float peak = 0f, topH = 0f;
            Keys(Fwd);
            while (Time.unscaledTime - t0 < 8f && !(s.ShuttleState == ZShuttleState.Cruise && s.ShuttleHeightM >= 8.0)) { sample(); yield return null; }
            Check($"S: W held: it took off (state {s.ShuttleState}, {s.ShuttleHeightM:0.0} m up after {Time.unscaledTime - t0:0.0} s)", s.ShuttleState == ZShuttleState.Cruise && s.ShuttleHeightM >= 8.0);
            float t1 = Time.unscaledTime;
            while (Time.unscaledTime - t1 < 5f) { sample(); peak = Mathf.Max(peak, (float)s.ShuttleSpeedPx); topH = Mathf.Max(topH, (float)s.ShuttleHeightM); yield return null; }
            float vmax = (float)s.RealMoveSpeed, rate = (OzFpsView.MoveOrders - orders0) / Mathf.Max(0.1f, Time.unscaledTime - t0);
            Check($"S: it cruises at {peak:0.0} px/s of its top {vmax:0.0} (want 80 % or more: no crawl from planned stops), {rate:0.0} orders a second (at most 3)", peak >= 0.8f * vmax && rate <= 3f);

            // a turn by the mouse (about 90 deg of look), W still held
            float hd0 = s.ShuttleHeadingDeg; float k = Mathf.Max(1e-4f, RtsCamera.FreeLookSpeed); int steps = 20;
            for (int i = 0; i < steps; i++) { MouseDelta(new Vector2(90f / k / steps, 0f)); sample(); yield return null; }
            MouseDelta(Vector2.zero);
            float t2 = Time.unscaledTime, turned = 0f;
            while (Time.unscaledTime - t2 < 5f) { sample(); turned = Mathf.Max(turned, Mathf.Abs(Mathf.DeltaAngle(hd0, s.ShuttleHeadingDeg))); yield return null; }
            Check($"S: the look turned about 90 deg: the ship's heading followed by {turned:0} deg within 5 s (want 45 or more)", turned >= 45f);

            // landing: W let go
            Keys();
            float t3 = Time.unscaledTime;
            while (Time.unscaledTime - t3 < 12f && s.ShuttleState != ZShuttleState.Landed && s.ShuttleState != ZShuttleState.Hover) { sample(); yield return null; }
            if (s.ShuttleState == ZShuttleState.Hover) Log($"INFO S: W let go: it hovers where the braking ended (the rules: it cannot land there: {s.ShuttleNoLand})");
            else Check($"S: W let go: it braked and landed within 12 s (state {s.ShuttleState}, {s.ShuttleHeightM:0.0} m)", s.ShuttleState == ZShuttleState.Landed);
            Check($"S: the camera stayed clear of the ship in every one of {frames} sampled frames: the nearest measured part box {minPart:0.00} m (want 0.3 m or more)", frames > 30 && minPart >= 0.3f);

            yield return Out();
            int heldAfter = v ? v.GetComponentsInChildren<Renderer>(true).Count(r => r && r.forceRenderingOff) : 0;
            Check($"S: Esc: out of the view, nothing of the ship held by the view ({heldAfter} renderers held off, by other code included), kit blocks left {OzFpsBadgeHold.MarkedNow}", !OzFpsView.On && OzFpsBadgeHold.MarkedNow == 0);
            Drop(s); made.Remove(s);
        }

        void Finish()
        {
            Devices(false);
            string head = $"{(fail == 0 && pass > 0 ? "PASS" : "FAIL")}: {pass} passed, {fail} failed";
            Report = head;
            log.Insert(0, head + "\n");
            try { File.WriteAllText(Path.Combine(Folder(), "report_shuttle_badges.txt"), log.ToString()); } catch (IOException) { }
            Debug.Log("[OpenZ FPS shuttle check] " + head);
            Destroy(gameObject);
        }
    }
}
#endif
