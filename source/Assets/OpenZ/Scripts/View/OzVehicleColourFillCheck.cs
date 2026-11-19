#if UNITY_EDITOR && !OZ_RELEASE
// OpenZ (GPL-3.0) - version 1.5 kit vehicle_colour_fill: the Play check (editor only, never in a build).
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using Unity.Profiling;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The Play check, on a level playing in the normal mode, from the menus
    /// OpenZ/V1.5/Vehicle Colour Fill (Editor/OzV15VehicleColourFillMenu.cs). It stages every vehicle and gun type EMPTY near the camera, in
    /// two batches of six (jeep, light tank, medium tank, heavy tank, APC, missile launcher; crane, mech, gatling, field gun, howitzer, missile
    /// cannon), each with a red grunt beside it, and reads after every LateUpdate, every frame, the paint TeamLit gets on one painted part of
    /// each (_OZTeamVivid.w: 1 neutral, 2 owned, between = the fill) with the paint's team, the shown owner and the sim's owner.
    /// The game clock runs at exactly 60 frames a second of game time while the check runs (Time.captureFramerate 60), so a frame count is a
    /// time and the pictures are 1/60 s apart in game time whatever the editor's real speed.
    /// <list type="bullet">
    /// <item>0 the staging: six empty hulls (neutral paint, w 1) and six red grunts.</item>
    /// <item>1 CAPTURE: each grunt ordered in (ENTER): neutral -> red when he sits inside (CaptureHold).</item>
    /// <item>2 DRIVER OUT: ZWorld.Eject (the HUD's EXIT DRIVER / EJECT): red -> neutral at once in the sim.</item>
    /// <item>3 BACK IN: the robot that got out ordered in again: neutral -> red (a second capture).</item>
    /// <item>4 SNIPED: the driver's health to 0 (ZObject.DamageDriverHealth + the sim's own driver-health step, as a sniper's hit):
    /// red -> neutral.</item>
    /// <item>5 the counters: one renderer walk per fill, frames written, the frame's GC allocation during the fills (shown, not judged).</item>
    /// </list>
    /// A fill row PASSES (NORMAL) when the change runs over at least <see cref="MinMidFrames"/> frames between the two looks, takes
    /// <see cref="MinSec"/> .. <see cref="MaxSec"/> s, never steps back, the paint's team is the shown owner every frame and the sim's owner
    /// changed no later than the paint's team. OFF (1.4's one-frame change) reproduces the owner's fault: the same rows PASS only when the
    /// change takes ONE frame (no frame between). FRAMES = NORMAL + a picture every 2nd frame (1/30 s of game time apart) through every fill,
    /// from 6 frames before it to 6 after (proof/play/frames/row*: 960 x 540 JPG, at most <see cref="PictureCap"/>). The owner's 60 fps clip
    /// is recorded with OBS from a plain game, not from this check (the kit's README: no test markers, the game's own sounds).
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class OzVehicleColourFillCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ V1.5 vehicle colour fill play]";
        public static string Report = "not run", Folder;
        public static bool Running;
        /// <summary>0 NORMAL, 1 OFF (1.4's one-frame change), 2 FRAMES (normal + a picture every 2nd frame through every fill).</summary>
        public static int Mode;
        public static int MinMidFrames = 15, PictureCap = 600;
        public static float MinSec = 0.33f, MaxSec = 0.5f, BoardWaitMaxSec = 25f, AfterSec = 1.2f;
        static readonly string[][] Batches =
        {
            new[] { "jeep", "light", ZMTank.Type, "heavy", "apc", "missile_launcher" },
            new[] { "crane", ZMTank.Mech, "gatling", "gun", "howitzer", "missile_cannon" },
        };
        static readonly HashSet<string> Guns = new HashSet<string> { "gatling", "gun", "howitzer", "missile_cannon" };
        static readonly int VividId = Shader.PropertyToID("_OZTeamVivid"), TeamColorId = Shader.PropertyToID("_TeamColor");

        ZGame g; ZWorld W; Team me;
        readonly StringBuilder sb = new StringBuilder(); int pass, total;
        readonly List<ZObject> made = new List<ZObject>();
        int startMaxId = int.MaxValue, pictures, rowNo, batchNo; string rowName = "";
        bool shoot; int quietFrames;
        MaterialPropertyBlock blk;
        ProfilerRecorder gc; long gcFill, gcIdle; int gcFillN, gcIdleN;

        struct Sample { public float t, w, lum; public Team team, shown, sim; public bool filling; }
        sealed class Car
        {
            public string type; public ZObject o, robot; public UnitView v; public TeamPaint p; public Renderer probe;
            public bool watch; public readonly List<Sample> s = new List<Sample>();
        }
        readonly List<Car> cars = new List<Car>();

        static string ModeName => Mode == 1 ? "OFF (1.4's one-frame change)" : Mode == 2 ? "FRAMES (the fill + a picture every 2nd frame)" : "NORMAL (the fill)";
        public static string ReportFile => Mode == 1 ? "play_check_off_report.txt" : Mode == 2 ? "play_check_frames_report.txt" : "play_check_report.txt";
        static string Kind(string t) => t == "light" ? "light tank" : t == ZMTank.Type ? "medium tank" : t == "heavy" ? "heavy tank" : t == ZMTank.Mech ? "mech" : t == "missile_launcher" ? "missile launcher" : t == "gun" ? "field gun" : t == "missile_cannon" ? "missile cannon" : t;

        public static void Begin()
        {
            if (Running || FindFirstObjectByType<OzVehicleColourFillCheck>()) { Debug.LogWarning(Tag + " already running"); return; }   // 1.5 pass 6b (gate A24): a second call in the same frame (a tool's retry) started a second copy
            Running = true;
            new GameObject("OZ_VehicleColourFillCheck").AddComponent<OzVehicleColourFillCheck>();
        }

        IEnumerator Start()
        {
            Running = true; Report = "running";
            float speed = 1f; bool on0 = OzVehicleColourFill.On; int cap0 = Time.captureFramerate;
            try
            {
                g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>(); W = g ? g.World : null;
                if (!g || W == null) { Report = Tag + " FAIL: no level playing (ZGame)"; Debug.LogWarning(Report); yield break; }
                me = OpenZHud.I ? OpenZHud.I.Player : g.player;
                speed = g.gameSpeed; g.gameSpeed = 1f;
                Time.captureFramerate = 60;                                                                   // 1/60 s of game time a frame (put back at the end)
                startMaxId = W.Objects.Count > 0 ? W.Objects.Max(o => o.Id) : 0;
                OzVehicleColourFill.On = Mode != 1;
                if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
                try { gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"); } catch { }
                Note($"run: {ModeName}; OzVehicleColourFill.On {OzVehicleColourFill.On}, {OzVehicleColourFill.Seconds:0.00} s; player {me}; game clock 60 frames a second (Time.captureFramerate)");
                StartCoroutine(Peace());
                for (int b = 0; b < Batches.Length; b++) yield return Batch(b);
                Note(OzVehicleColourFill.Status());
                Note($"frame GC allocation (the whole game's, 'GC Allocated In Frame'): during fills {(gcFillN > 0 ? gcFill / gcFillN : -1)} bytes a frame over {gcFillN} frames, "
                     + $"other check frames {(gcIdleN > 0 ? gcIdle / gcIdleN : -1)} over {gcIdleN} (shown, not judged: the fill's own frames allocate nothing by construction)");
                if (Mode != 1) Log("5 the counters: one renderer walk per fill, frames written by the fills", OzVehicleColourFill.Fills > 0 && OzVehicleColourFill.Binds == OzVehicleColourFill.Fills && OzVehicleColourFill.Steps > 0, OzVehicleColourFill.Status());
            }
            finally
            {
                if (g) g.gameSpeed = speed;
                Time.captureFramerate = cap0;
                OzVehicleColourFill.On = on0;
                if (gc.Valid) gc.Dispose();
                if (half) { half.Release(); Destroy(half); }
                Cleanup();
                Report = $"{Tag} {ModeName}: {(pass == total && total > 0 ? "PASS" : "FAIL")} {pass}/{total}\n" + sb;
                if (!string.IsNullOrEmpty(Folder)) { try { File.WriteAllText(Path.Combine(Folder, ReportFile), Report); } catch { } }
                Debug.Log(Report);
                Running = false; Mode = 0; Destroy(gameObject);
            }
        }

        void Log(string what, bool ok, string detail) { total++; if (ok) pass++; sb.AppendLine($"{(ok ? "PASS" : "FAIL")}  {what}: {detail}"); }
        void Note(string s) => sb.AppendLine("      " + s);
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        // ------------------------------------------------------------------ the count: after every LateUpdate, each frame
        void LateUpdate()
        {
            if (!Running) return;
            blk ??= new MaterialPropertyBlock();
            bool any = false;
            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                if (!c.watch || c.o == null || !c.v || !c.p || !c.probe) continue;
                c.probe.GetPropertyBlock(blk, 0);
                var tc = blk.GetColor(TeamColorId);
                var sm = new Sample { t = Time.time, w = blk.GetVector(VividId).w, lum = 0.2126f * tc.r + 0.7152f * tc.g + 0.0722f * tc.b, team = c.p.Team,
                                      shown = CaptureHold.Shown(c.o), sim = c.o.Owner, filling = c.p.Filling };
                c.s.Add(sm);
                if (sm.filling || (sm.w > 1.002f && sm.w < 1.998f)) any = true;
            }
            if (gc.Valid) { long a = gc.LastValue; if (any) { gcFill += a; gcFillN++; } else if (cars.Any(c => c.watch)) { gcIdle += a; gcIdleN++; } }
            if (any) quietFrames = 0; else quietFrames++;
            if (shoot && Mode == 2 && !string.IsNullOrEmpty(Folder) && pictures < PictureCap && (any || quietFrames <= 6) && (Time.frameCount & 1) == 0)
            {
                var dir = Path.Combine(Folder, "frames", $"batch{batchNo}_row{rowNo}_{rowName}");
                pictures++;
                StartCoroutine(Shot(Path.Combine(dir, $"f{Time.frameCount:D6}_t{Time.time:000.000}.jpg")));
            }
        }

        RenderTexture half;
        /// <summary>This frame as drawn (after its pictures), 960 x 540, JPG 92: about 150 KB a picture.</summary>
        IEnumerator Shot(string path)
        {
            yield return new WaitForEndOfFrame();
            Texture2D full = null, small = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                full = ScreenCapture.CaptureScreenshotAsTexture();
                if (!half) half = new RenderTexture(960, 540, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(full, half);
                var prev = RenderTexture.active; RenderTexture.active = half;
                small = new Texture2D(960, 540, TextureFormat.RGB24, false);
                small.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); small.Apply(false);
                RenderTexture.active = prev;
                File.WriteAllBytes(path, small.EncodeToJPG(92));
            }
            catch (System.Exception ex) { Note("picture " + path + " failed: " + ex.Message); }
            finally { if (full) Destroy(full); if (small) Destroy(small); }
        }

        // ------------------------------------------------------------------ the staging (the hatch_lid check's free-ground search)
        ZObject Unit(string cat, string type, Team team, int tx, int ty)
        {
            var o = W.CreateObject(cat, type, tx * 16, ty * 16, team);
            if (o == null) return null;
            g.SpawnUnitView(o);
            made.Add(o);
            return o;
        }

        void Kill(ZObject o) { if (o == null || o.Destroyed || o.KillAt >= 0) return; o.SetHealth(0); W.UpdateObjectHealth(o); }

        bool TileFree(int x, int y) => W.Path.TilePassable(x, y, true) && W.Path.TilePassable(x, y, false) && !W.LavaAtPx(x * 16 + 8, y * 16 + 8);
        bool AreaFree(int x, int y)
        {
            if (x < 3 || y < 3 || x + 18 >= W.Path.W - 3 || y + 10 >= W.Path.H - 3) return false;
            for (int k = 0; k < 3; k++) if (!SpotFree(x + 6 * k, y) || !SpotFree(x + 6 * k, y + 6)) return false;
            return true;
        }
        bool SpotFree(int x, int y)
        {
            for (int j = 0; j < 4; j++) for (int i = 0; i < 4; i++) if (!TileFree(x + i, y + j)) return false;
            return !W.Objects.Any(o => !o.Destroyed && o.KillAt < 0 && o.Kind != ZKind.MapItem && o.LocX < (x + 4) * 16 && o.LocX + o.WidthPix > x * 16 && o.LocY < (y + 4) * 16 && o.LocY + o.HeightPix > y * 16);
        }
        bool Block(out int x0, out int y0)
        {
            var cam = Camera.main; var c = Vector3.zero;
            if (cam) g.GroundPoint(cam.ScreenPointToRay(new Vector3(UnityEngine.Screen.width * 0.5f, UnityEngine.Screen.height * 0.5f, 0f)), out c);
            var f = ZGame.WorldToPx(c); int fx = f.x >> 4, fy = f.y >> 4;
            for (int r = 0; r <= 40; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int x = fx + dx - 8, y = fy + dy - 5;
                        if (AreaFree(x, y)) { x0 = x; y0 = y; return true; }
                    }
            x0 = fx; y0 = fy; return false;
        }

        /// <summary>One painted part of the car (a mesh renderer TeamPaint paints, not a robot figure): its block is what TeamLit gets.</summary>
        void Bind(Car c)
        {
            c.v = null; c.p = null; c.probe = null;
            if (c.o == null || !g.Views.TryGetValue(c.o.Id, out var v) || !v) return;
            c.v = v; c.p = v.GetComponent<TeamPaint>();
            if (!c.p) return;
            foreach (var r in v.GetComponentsInChildren<Renderer>(false))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled || r.GetComponentInParent<TeamPaint>(true) != c.p || !r.sharedMaterial || !r.sharedMaterial.HasProperty(VividId)) continue;
                c.probe = r; break;
            }
        }

        readonly Dictionary<ZObject, int> quieted = new Dictionary<ZObject, int>();
        /// <summary>1.5 pass 6b (gate A24): while row 3 runs, every frame: a robot on its way back that loses health is noted with the units that
        /// have it as their attack target (type, id, team, damage) and where it is.</summary>
        bool watchBack;
        IEnumerator WatchBack(Dictionary<Car, ZObject> back, string row)
        {
            var hp = new Dictionary<ZObject, long>();
            foreach (var kv in back) if (kv.Value != null) hp[kv.Value] = (long)kv.Value.Health;
            while (Running && watchBack && W != null)
            {
                foreach (var kv in back)
                {
                    var r = kv.Value; if (r == null || !hp.ContainsKey(r)) continue;
                    long now = r.Destroyed ? 0 : (long)r.Health;
                    if (now >= hp[r]) continue;
                    var who = new StringBuilder();
                    foreach (var o in W.Objects) if (!o.Destroyed && o.AttackObject == r) who.Append($"{o.Type} #{o.Id} {o.Owner} at {o.CenterX},{o.CenterY} damage {o.Damage}; ");
                    Note($"{row} {Kind(kv.Key.type)} #{kv.Key.o.Id}: its robot #{r.Id} health {hp[r]} -> {now} (destroyed {r.Destroyed}, killAt {r.KillAt}) at {r.CenterX},{r.CenterY}, t {Time.time:F2}; units aiming at it: {(who.Length > 0 ? who.ToString() : "none")}");
                    hp[r] = now;
                }
                yield return null;
            }
        }

        IEnumerator Peace()
        {
            while (Running && W != null)
            {
                foreach (var o in W.Objects) if (o.Owner != me && o.Owner != Team.Neutral && !o.Destroyed && o.Damage > 0 && !made.Contains(o) && !quieted.ContainsKey(o)) { quieted[o] = o.Damage; o.Damage = 0; }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        void Cleanup()
        {
            if (W == null) return;
            foreach (var c in cars) c.watch = false;
            foreach (var kv in quieted) if (kv.Key != null && !kv.Key.Destroyed) kv.Key.Damage = kv.Value;
            if (quieted.Count > 0) Note($"the level's own enemies quieted for the check: {quieted.Count} (their Damage put back)");
            quieted.Clear();
            var ids = new HashSet<int>(made.Where(o => o != null).Select(o => o.Id));
            foreach (var o in new List<ZObject>(W.Objects))
                if (o.IsRobot && o.Id > startMaxId && made.Any(v => v != null && v.Kind != ZKind.Robot && Mathf.Abs(v.CenterX - o.CenterX) < 96 && Mathf.Abs(v.CenterY - o.CenterY) < 96)) ids.Add(o.Id);
            foreach (var o in new List<ZObject>(W.Objects)) if (ids.Contains(o.Id)) Kill(o);
        }

        // ------------------------------------------------------------------ one batch: staging, capture, driver out, back in, sniped
        IEnumerator Batch(int b)
        {
            var types = Batches[b]; batchNo = b + 1;
            if (!Block(out int x0, out int y0)) { Log($"0 batch {b + 1}: free ground near the camera", false, "no six free 4 x 4-tile stands near the camera: move the camera to open ground and run again"); yield break; }
            var rts = FindFirstObjectByType<RtsCamera>();
            if (rts) rts.FlyTo(ZGame.PxToWorld((x0 + 8) * 16, (y0 + 5) * 16));
            yield return Frames(90);
            var batch = new List<Car>();
            for (int k = 0; k < types.Length; k++)
            {
                int sx = x0 + 6 * (k % 3), sy = y0 + 6 * (k / 3);
                var c = new Car { type = types[k], o = Unit(Guns.Contains(types[k]) ? "cannon" : "vehicle", types[k], Team.Neutral, sx + 1, sy + 1) };
                batch.Add(c); cars.Add(c);
            }
            yield return Frames(60);                                                                          // drawn empty for a second (a fill needs the old look drawn just before)
            foreach (var c in batch) Bind(c);
            bool staged = batch.All(c => c.o != null && c.v && c.p && c.probe && c.o.Owner == Team.Neutral && c.o.Drivers.Count == 0 && c.p.Team == Team.Neutral);
            Log($"0 batch {b + 1}: six empty hulls staged, each with a painted part read",
                staged, string.Join("; ", batch.Select(c => $"{Kind(c.type)} #{(c.o != null ? c.o.Id : -1)}: {(c.probe ? c.probe.name : "NO PAINTED PART")}")));
            if (!staged) yield break;
            for (int k = 0; k < batch.Count; k++)
            {
                int sx = x0 + 6 * (k % 3), sy = y0 + 6 * (k / 3);
                batch[k].robot = Unit("robot", "grunt", me, sx + 3, sy + 1);
            }
            yield return null;

            // 1 capture
            watchBack = true; StartCoroutine(WatchBack(batch.ToDictionary(c => c, c => c.robot), "1 capture"));   // 1.5 pass 6b (gate A24): who hurts a robot on its way in
            yield return Row(1, "capture", batch, true, () =>
            {
                foreach (var c in batch) if (c.robot != null && !c.robot.Destroyed) W.SendWaypoints(me, c.robot, new[] { new ZWaypoint { mode = WpMode.Enter, refId = c.o.Id, x = c.o.CenterX, y = c.o.CenterY } });
            }, c => c.o.Owner == me && c.p.Team == me && !CaptureHold.IsHeld(c.o.Id));

            watchBack = false;
            // 2 driver out
            var asked = new Dictionary<Car, bool>();
            yield return Row(2, "driver_out", batch, false, () =>
            {
                foreach (var c in batch) asked[c] = !c.o.Destroyed && c.o.Owner == me && W.Eject(me, c.o);
            }, c => !asked[c] || (c.o.Owner == Team.Neutral && c.p.Team == Team.Neutral), c => asked.TryGetValue(c, out var a) && a,
               c => "EJECT refused for this type (no driver out in this mode): row 4 covers its red -> neutral");

            // 3 back in: the robot that got out, ordered in again
            var back = new Dictionary<Car, ZObject>();
            foreach (var c in batch)
            {
                if (!asked[c]) continue;
                ZObject rob = null;
                foreach (var o in W.Objects)
                    if (o.IsRobot && o.Owner == me && !o.Destroyed && o.KillAt < 0 && (o.DriverOutFromId == c.o.Id || (o.Id > startMaxId && Mathf.Abs(o.CenterX - c.o.CenterX) < 96 && Mathf.Abs(o.CenterY - c.o.CenterY) < 96 && !back.ContainsValue(o) && !made.Contains(o))))
                    { rob = o; break; }
                back[c] = rob; if (rob != null) made.Add(rob);
            }
            foreach (var c in batch)                                                                       // 1.5 pass 6b (gate A24): which robot each hull gets back
                if (asked[c]) { back.TryGetValue(c, out var rb); Note($"3 back_in {Kind(c.type)} #{c.o.Id} at {c.o.CenterX},{c.o.CenterY}: the robot to board it again " + (rb != null ? $"#{rb.Id} at {rb.CenterX},{rb.CenterY} (DriverOutFromId {rb.DriverOutFromId})" : "NOT FOUND")); }
            yield return null;
            watchBack = true; StartCoroutine(WatchBack(back, "3 back_in"));                                            // 1.5 pass 6b (gate A24): who hurts a robot on its way back
            yield return Frames(60);
            yield return Row(3, "back_in", batch, true, () =>
            {
                foreach (var c in batch) if (back.TryGetValue(c, out var rob) && rob != null && !rob.Destroyed) W.SendWaypoints(me, rob, new[] { new ZWaypoint { mode = WpMode.Enter, refId = c.o.Id, x = c.o.CenterX, y = c.o.CenterY } });
            }, c => !back.ContainsKey(c) || (c.o.Owner == me && c.p.Team == me && !CaptureHold.IsHeld(c.o.Id)), c => back.ContainsKey(c),
               c => "no driver out in row 2: nothing to board again");
            watchBack = false;
            foreach (var c in batch)                                                                       // 1.5 pass 6b (gate A24): a hull nobody boarded again: where its robot is
                if (back.TryGetValue(c, out var rb2) && c.o.Owner != me)
                    Note($"3 back_in {Kind(c.type)} #{c.o.Id} not boarded: robot " + (rb2 == null ? "NOT FOUND" : $"#{rb2.Id} alive {!rb2.Destroyed && rb2.KillAt < 0}, in the world {W.Get(rb2.Id) == rb2}, at {rb2.CenterX},{rb2.CenterY}, waypoints {rb2.Waypoints.Count}, moving {rb2.IsMoving}, the hull's drivers {c.o.Drivers.Count}"));

            // 4 sniped: the driver's health to 0 by the sim's own steps (as a sniper's hit)
            var step = typeof(ZWorld).GetMethod("UpdateObjectDriverHealth", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            yield return Row(4, "sniped", batch, false, () =>
            {
                foreach (var c in batch)
                {
                    if (c.o.Destroyed || c.o.Owner != me || c.o.Drivers.Count == 0) continue;
                    c.o.DamageDriverHealth(100000);
                    try { step?.Invoke(W, new object[] { c.o, null }); } catch (System.Exception ex) { Note("the sim's driver-health step failed: " + ex.Message); }
                }
            }, c => c.o.Owner == Team.Neutral && c.p.Team == Team.Neutral, null, null);
            if (Mode == 2) Note($"pictures so far {pictures} (every 2nd frame: 1/30 s of game time apart) in {Path.Combine(Folder ?? "", "frames")}");
            foreach (var c in batch) { c.watch = false; Kill(c.o); Kill(c.robot); }
            foreach (var o in made.ToList()) if (o != null && o.IsRobot && back.ContainsValue(o)) Kill(o);
            yield return Frames(60);
        }

        /// <summary>One row: start watching, do the change, wait until every car is done (or the time is up), judge each car.</summary>
        IEnumerator Row(int no, string name, List<Car> batch, bool up, System.Action act, System.Func<Car, bool> done, System.Func<Car, bool> counts = null, System.Func<Car, string> why = null)
        {
            foreach (var c in batch) { c.s.Clear(); c.watch = true; }
            rowNo = no; rowName = name; shoot = true; quietFrames = 99;
            yield return Frames(6);
            act();
            float t0 = Time.time;
            while (Time.time - t0 < BoardWaitMaxSec && batch.Any(c => c.o != null && !c.o.Destroyed && !done(c))) yield return null;
            yield return Frames(Mathf.RoundToInt(AfterSec * 60f));
            shoot = false;
            foreach (var c in batch)
            {
                c.watch = false;
                if (counts != null && !counts(c)) { Note($"{no} {name} {Kind(c.type)} #{c.o.Id}: {(why != null ? why(c) : "not asked")}"); continue; }
                Judge(no, name, c, up);
            }
        }

        void Judge(int no, string name, Car c, bool up)
        {
            var s = c.s;
            Team target = up ? me : Team.Neutral;
            bool AtStart(Sample x) => up ? x.w <= 1.002f : x.w >= 1.998f;
            bool AtEnd(Sample x) => up ? x.w >= 1.998f : x.w <= 1.002f;
            int j = -1;
            for (int k = 1; k < s.Count; k++) if (AtEnd(s[k]) && s.Take(k).Any(AtStart)) { j = k; break; }
            string what = $"{no} {name.Replace('_', ' ')}: {Kind(c.type)} #{c.o.Id} {(up ? "neutral -> " + me : me + " -> neutral")}";
            if (j < 0) { Log(what, false, $"the change never ended in {s.Count} frames (last w {(s.Count > 0 ? s[s.Count - 1].w : -1f):0.000}, paint team {c.p.Team}, sim {c.o.Owner})"); return; }
            int i = j - 1; while (i > 0 && !AtStart(s[i])) i--;
            int mid = j - i - 1; float dur = s[j].t - s[i].t;
            bool mono = true; for (int k = i; k < j; k++) if (up ? s[k + 1].w < s[k].w - 1e-4f : s[k + 1].w > s[k].w + 1e-4f) mono = false;
            bool stays = true; for (int k = j; k < s.Count; k++) if (!AtEnd(s[k])) stays = false;
            int teamAt = s.FindIndex(x => x.team == target), simAt = s.FindIndex(x => x.sim == target);
            // Unity pass 1.5: count the frames where the paint's team is not the shown owner; one frame behind the shown owner's flip (the paint
            // takes the new team on the frame after the hold ends) is the sampling order, more is a fault
            int off = 0, lag = 0, firstOff = -1;
            for (int k = 0; k < s.Count; k++)
                if (s[k].team != s[k].shown) { off++; if (firstOff < 0) firstOff = k; if (k > 0 && s[k].team == s[k - 1].shown) lag++; }
            bool teamShown = off == lag && lag <= 1;
            string offTxt = off == 0 ? "" : $" ({off} frame(s) differ, {lag} of them one frame behind the shown owner's flip; first at frame {firstOff}: paint {s[firstOff].team}, shown {s[firstOff].shown}, sim {s[firstOff].sim})";
            string detail = $"{mid} frames between the looks, {dur:0.000} s ({s[i].t:F3} -> {s[j].t:F3}), never back {mono}, stays {stays}; paint team = shown owner every frame {teamShown}{offTxt}; "
                            + $"sim owner {target} at frame {simAt}, paint team at frame {teamAt} (the fill from frame {i}); _TeamColor luminance {s[i].lum:0.000} -> {s[j].lum:0.000}";
            if (Mode == 1) Log(what + ": 1.4 changes it in ONE frame (the owner's snap, reproduced)", mid == 0 && stays && teamShown, detail);
            else Log(what + $": fills over {MinSec:0.00} .. {MaxSec:0.00} s, eased, never back, the sim at once",
                     mid >= MinMidFrames && dur >= MinSec && dur <= MaxSec && mono && stays && teamShown && simAt >= 0 && teamAt >= 0 && simAt <= teamAt, detail);
        }
    }
}
#endif
