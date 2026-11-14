using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Version 1.2, item 54 (the closest zoom still too close for an RTS game, above all with the free camera): the closest zoom
    /// 30 m -> 35 m (RtsCamera.ZoomClosest, one wheel notch) and the free camera's back-off at low tilts near it (RtsCamera.FreeNear.cs).
    /// Counted Play check and pictures, editor only; it changes nothing in the game: the camera, its closest limit, the back-off switch,
    /// the FREE CAMERA option and the testing overlay are put back when it ends.
    /// Menu "OpenZ/V1.2/Closest zoom: fixed and free camera check + pictures (Play)", in Play on a level after the briefing (run it on
    /// L02 and on L16), the HUD shown, no menu open, the Game view at 1920 x 1080, hands off the mouse and the keys. One log line
    /// "[OpenZ V1.2 closest zoom] PASS n/n" or "FAIL" (also in Report and in the report file next to the pictures, Folder()).
    /// Lengths are on the y = 0 plane the camera looks at (as the minimap's view box draws it); the camera's height over the real
    /// ground under it as well. "Nearest ground" = the bottom row's middle ray to the plane; "across" = the ground between the left and
    /// the right edge rays of a screen row.
    ///  K1 the limits in force: the closest 35 m, the widest unchanged (200 m; level 16 its own OzL16Outskirts.MaxHeight).
    ///  K2 a zoom asked to 5 m stops on 35 m; the wheel in from the 70 m start: 4 notches to 36.5 m and a short 5th onto 35 m.
    ///  K3 the fixed closest view (the map's middle): the camera 35 m over the focus plane, the ground across the bottom row and the
    ///     centre and the nearest ground as the camera's own numbers give them (1920 x 1080: 36.3 / 43.6 / 37.0 m).
    ///  K4 the free camera at the closest zoom and the lowest tilt inside the map (the map's middle), north up and turned 90 / 180 / 270:
    ///     the view centre on the focus, the camera over the plane at least as high as the back-off puts it (24.3 m; higher where a
    ///     floor lifts it) and at least 3 m over the ground under it; the nearest ground and the bottom row never nearer / narrower
    ///     than the fixed closest view's, the centre row at least as wide.
    ///  K5 the same at the four map borders (the camera outside the map, looking in); there also at least the border floor (22 m).
    ///  K6 every tilt at the closest zoom (25 .. 55 by 5, 70, the steepest): the tilt drawn is the one asked (where no floor lifts the
    ///     camera: turning and tilting work), the nearest ground never nearer than the fixed closest view's.
    ///  K7 the zoom with the lowest tilt: each wheel notch out from the closest to 70 m moves the camera farther (no dead notch); at
    ///     70 m and the widest it stands at the zoom's own distance (h / sin 55: nothing changed there); at the closest zoom the
    ///     steepest tilt keeps the zoom's distance as well.
    ///  K8 the pictures, the normal RTS camera at the closest zoom, one focus (a robot of ours beside one of our vehicles when there is
    ///     one): closest_LEVEL_fixed_h30_old (the limit lowered to 30 m for it only), closest_LEVEL_fixed_h35_new, closest_LEVEL_free_h30_old
    ///     (30 m and the back-off off: the free camera as it was), closest_LEVEL_free_h35_new (FREE CAMERA on, the lowest tilt, north
    ///     up), the pairs closest_LEVEL_pair_fixed_30_35.jpg and closest_LEVEL_pair_free_30_35.jpg, and closest_LEVEL_pair_k18_30_35.jpg
    ///     (the k18 picture zoom_in_v12/zoomin_LEVEL_h30_new.png beside the new fixed one) when that picture is there: all written, no
    ///     two the same, old and new differ; the robot at 35 m is the size the camera's distance gives (30 / 35 of its size at 30 m,
    ///     within 5 %).
    ///  K9 put back: the closest limit 35 m, the camera at its height and focus from before, the back-off on, FREE CAMERA as it was.
    /// In the Unit editor's playground (the same menu): U1 its own closest zoom, 30 m (RtsCamera.UeClosest, as it was); U2 its free
    /// look at that zoom and its lowest tilt keeps the zoom's distance (no back-off there).
    /// </summary>
    public static class OzV12ClosestZoomProof
    {
        /// <summary>The closest zoom this item asks for and the one before it (m).</summary>
        public const float WantClosest = 35f, OldClosest = 30f;
        public static string Report = "not run";
        const string Tag = "[OpenZ V1.2 closest zoom]";
        static readonly Stack<IEnumerator> run = new Stack<IEnumerator>(); static int waitFrame, ok, bad;
        static readonly StringBuilder fails = new StringBuilder(), notes = new StringBuilder();
        static string dir = "", level = "";
        static RtsCamera rts; static Camera cam; static ZGame g;
        static float minH0 = -1f, h00; static Vector3 focus0; static Transform follow0; static Vector2 drift0; static bool shown0, saved, hadPref, near0; static int prefWas;

        [MenuItem("OpenZ/V1.2/Closest zoom: fixed and free camera check + pictures (Play)")]
        public static void RunMenu()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning(Tag + " enter Play mode on a level first (after the briefing)"); return; }
            if (run.Count > 0) { Debug.LogWarning(Tag + " a run is still going"); return; }
            ok = bad = 0; fails.Clear(); notes.Clear(); Report = "running"; saved = false; dir = "";
            var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor"); if (gv != null) EditorWindow.FocusWindowIfItsOpen(gv);
            run.Push(OzUnitEditor.Active ? Playground() : Proof()); waitFrame = 0; EditorApplication.update -= Step; EditorApplication.update += Step;
        }

        /// <summary>The pictures and the report: the work tree's records, milestones/closest_zoom_v12 (OpenZPaths: no machine path in code).</summary>
        public static string Folder() => Path.Combine(OpenZPaths.Records, "milestones", "closest_zoom_v12");

        /// <summary>Runs the proof as a coroutine from the editor's update: a yielded int waits that many frames, null one frame, a
        /// yielded IEnumerator runs inside it at once (as a nested coroutine does).</summary>
        static void Step()
        {
            if (run.Count == 0 || !EditorApplication.isPlaying) { run.Clear(); EditorApplication.update -= Step; return; }
            if (Time.frameCount < waitFrame) return;
            for (int guard = 0; guard < 64 && run.Count > 0; guard++)
            {
                var top = run.Peek(); bool more;
                try { more = top.MoveNext(); }
                catch (System.Exception ex) { Debug.LogException(ex); bad++; fails.Append(" | exception ").Append(ex.Message); run.Clear(); more = false; }
                if (!more) { if (run.Count > 0) run.Pop(); continue; }
                if (top.Current is IEnumerator child) { run.Push(child); continue; }
                waitFrame = Time.frameCount + (top.Current is int n ? Mathf.Max(1, n) : 1);
                return;
            }
            if (run.Count > 0) { waitFrame = Time.frameCount + 1; return; }
            EditorApplication.update -= Step; PutBack(); Finish();
        }

        static void Check(bool pass, string what) { if (pass) ok++; else { bad++; fails.Append(" | ").Append(what); } notes.Append(pass ? " [ok] " : " [FAIL] ").Append(what).Append(';'); }
        static void Note(string what) => notes.Append(" (").Append(what).Append(");");

        static void Save()
        {
            minH0 = rts.minHeight; h00 = rts.height; focus0 = rts.focus; follow0 = rts.follow; drift0 = rts.autoDrift; shown0 = OzCheatSpawner.Visible;
            hadPref = PlayerPrefs.HasKey(OzFreeCam.PrefKey); prefWas = PlayerPrefs.GetInt(OzFreeCam.PrefKey, 0); near0 = RtsCamera.FreeNearOn; saved = true;
            rts.follow = null; rts.autoDrift = Vector2.zero; rts.FreeLookOff();
            if (OzCheatSpawner.Visible && OzCheatSpawner.I) OzCheatSpawner.I.Show(false);
        }

        /// <summary>The camera as it was: its closest limit, height, focus, follow and drift, the back-off switch, FREE CAMERA, the testing
        /// overlay. Safe to call twice.</summary>
        static void PutBack()
        {
            if (!saved || !rts) return;
            saved = false;
            RtsCamera.FreeNearOn = near0;
            rts.FreeLookOff(); rts.minHeight = minH0; OzCamEdge.Snap();
            rts.focus = focus0; rts.SetHeightImmediate(h00); rts.follow = follow0; rts.autoDrift = drift0;
            if (hadPref) { if (OzFreeCam.On != (prefWas == 1)) OzFreeCam.On = prefWas == 1; }
            else if (PlayerPrefs.HasKey(OzFreeCam.PrefKey)) { OzFreeCam.On = true; PlayerPrefs.DeleteKey(OzFreeCam.PrefKey); PlayerPrefs.Save(); }   // never set: the default (ON)
            if (shown0 && OzCheatSpawner.I) OzCheatSpawner.I.Show(true);
        }

        static void Finish()
        {
            Report = $"{Tag} {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad} checks, level {level}, at {Screen.width}x{Screen.height}:{notes}{(bad > 0 ? " FAILED:" + fails : "")}";
            if (bad == 0) Debug.Log(Report); else Debug.LogError(Report);
            if (dir.Length > 0)
            {
                try { File.WriteAllText(Path.Combine(dir, $"closest_{level}_report.txt"), Report.Replace("; ", ";\n") + "\n"); } catch (System.Exception e) { Debug.LogWarning(Tag + " report not written: " + e.Message); }
            }
        }

        /// <summary>Waits real seconds (unscaled), one frame at a time.</summary>
        static IEnumerator Wait(float s) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < s) yield return 1; }

        // ------------------------------------------------------------------------------------------------ measures
        static bool PlaneHit(Ray r, out Vector3 p)
        {
            p = default;
            if (r.direction.y > -1e-5f) return false;
            p = r.GetPoint(-r.origin.y / r.direction.y);
            return true;
        }

        /// <summary>The game camera's ray through a viewport point, its shake taken off.</summary>
        static Ray RayAt(float vx, float vy) { var r = cam.ViewportPointToRay(new Vector3(vx, vy, 0f)); r.origin -= rts.ShakeOffset; return r; }

        /// <summary>The camera's position without its shake.</summary>
        static Vector3 Eye => cam.transform.position - rts.ShakeOffset;

        /// <summary>The ground across a screen row (m, on the plane; -1: a ray misses it).</summary>
        static float Across(float vy) => PlaneHit(RayAt(0f, vy), out var a) && PlaneHit(RayAt(1f, vy), out var b) ? Vector3.Distance(a, b) : -1f;

        /// <summary>The same row on the real ground (m; -1: a ray misses it).</summary>
        static float AcrossReal(float vy) => g.GroundPoint(RayAt(0f, vy), out var a) && g.GroundPoint(RayAt(1f, vy), out var b) ? Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)) : -1f;

        /// <summary>The nearest ground on screen (m): the bottom row's middle ray to the plane, measured from the camera's own position
        /// (Eye; the ray itself starts on the near plane, about 0.5 m ahead) (999: it misses).</summary>
        static float Nearest() { var r = RayAt(0.5f, 0f); return PlaneHit(r, out var p) ? Vector3.Distance(Eye, p) : 999f; }

        /// <summary>How far the view centre's ground point lies from the focus (m).</summary>
        static float CentreOff() => PlaneHit(RayAt(0.5f, 0.5f), out var c) ? Vector2.Distance(new Vector2(c.x, c.z), new Vector2(rts.focus.x, rts.focus.z)) : 999f;

        /// <summary>The camera's height over the real ground under it (m).</summary>
        static float OverGround() { var e = Eye; return e.y - g.TerrainY(e.x, e.z); }

        /// <summary>What a camera y m over the plane, tilted t deg below the horizon, shows by its own field of view and aspect: the
        /// nearest ground (m, the bottom row's middle ray), the ground across the bottom row and across the centre (m).</summary>
        static void Shows(float y, float t, out float near, out float bottom, out float mid)
        {
            float a = cam.fieldOfView * 0.5f, k = 2f * Mathf.Tan(a * Mathf.Deg2Rad) * cam.aspect;
            near = y / Mathf.Sin((t + a) * Mathf.Deg2Rad);
            bottom = near * Mathf.Cos(a * Mathf.Deg2Rad) * k;
            mid = y / Mathf.Sin(t * Mathf.Deg2Rad) * k;
        }

        /// <summary>The floor PlaceFree keeps under a low-tilted camera standing at (x, z) at zoom h (RtsCamera.cs).</summary>
        static float FloorAt(float x, float z, float h)
        {
            float inside = Mathf.Min(Mathf.Min(x, rts.mapSizeMeters.x - x), Mathf.Min(-z, rts.mapSizeMeters.y + z));
            float floor = Mathf.Lerp(Mathf.Max(RtsCamera.FreeEdgeHeight, RtsCamera.FreeFloor), RtsCamera.FreeFloor, Mathf.Clamp01(inside / RtsCamera.FreeEdgeBlend));
            floor = Mathf.Max(floor, g.TerrainY(x, z) + RtsCamera.FreeClear);
            return Mathf.Min(floor, h);
        }

        static void Pose(Vector3 at, float h) { rts.focus = new Vector3(at.x, 0f, at.z); rts.SetHeightImmediate(h); rts.TakeShake(); }

        /// <summary>The free view through the same call the middle button makes: heading and tilt in degrees.</summary>
        static bool View(float yaw, float tilt)
        {
            rts.FreeLookOff();
            return rts.FreeTurn(new Vector2(yaw / RtsCamera.FreeLookSpeed, (rts.pitch - tilt) / RtsCamera.FreeLookSpeed));
        }

        /// <summary>The camera's drawn tilt (deg below the horizon).</summary>
        static float Drawn() { float x = cam.transform.eulerAngles.x; return x > 180f ? x - 360f : x; }

        /// <summary>The unit is drawn: its view is active and at least one of its mesh renderers is on.</summary>
        static bool IsDrawn(UnitView v) => v && v.gameObject.activeInHierarchy && v.GetComponentsInChildren<Renderer>(false).Any(r => r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer));

        /// <summary>The unit's drawn box on screen (px wide, px tall) from its mesh renderers' world boxes (none: zero), and its middle.</summary>
        static Vector2 ScreenBox(UnitView v, out Vector3 middle)
        {
            middle = v ? v.transform.position : Vector3.zero;
            if (!v || !cam) return Vector2.zero;
            bool any = false; var b = new Bounds();
            foreach (var r in v.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                var rb = r.bounds; if (rb.size.magnitude > 15f) continue;                           // a light cone or a helper, not the body
                if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
            }
            if (!any) return Vector2.zero;
            middle = b.center;
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                var s = cam.WorldToScreenPoint(c);
                x0 = Mathf.Min(x0, s.x); x1 = Mathf.Max(x1, s.x); y0 = Mathf.Min(y0, s.y); y1 = Mathf.Max(y1, s.y);
            }
            return new Vector2(x1 - x0, y1 - y0);
        }

        static IEnumerator Picture(string name, List<string> files)
        {
            string p = Path.Combine(dir, name + ".png");
            if (File.Exists(p)) { try { File.Move(p, Path.Combine(dir, name + "_older_" + System.DateTime.Now.ToString("HHmmss") + ".png")); } catch { } }
            ScreenCapture.CaptureScreenshot(p);
            yield return 3;
            yield return OzProofPictures.Written(p, 5f);
            files.Add(p);
        }

        // ------------------------------------------------------------------------------------------------ a level
        static IEnumerator Proof()
        {
            g = ZGame.I; var hud = OpenZHud.I; rts = hud ? hud.rts : null;
            level = g != null ? g.level : "";
            if (g == null || g.World == null || !hud || !rts) { Check(false, "no level running (Play a level, after the briefing)"); yield break; }
            if (RtsCamera.InputLocked || !rts.enabled) { Check(false, "the game camera is held by a cinematic or the ending: run it in normal play"); yield break; }
            cam = rts.GetComponent<Camera>();
            Save();
            dir = Folder(); Directory.CreateDirectory(dir);
            OzFreeCam.On = true;                                                                       // the FREE CAMERA option, as the player sets it
            yield return 2;
            float lo = Mathf.Max(22f, RtsCamera.FreeTiltMin), hi = Mathf.Min(89f, RtsCamera.FreeTiltMax), sinP = Mathf.Sin(rts.pitch * Mathf.Deg2Rad);
            var middle = new Vector3(rts.mapSizeMeters.x * 0.5f, 0f, -rts.mapSizeMeters.y * 0.5f);

            // K1 / K2 the limits and the wheel
            float widest = OzL16Outskirts.I ? Mathf.Min(RtsCamera.ZoomWidest, OzL16Outskirts.MaxHeight) : RtsCamera.ZoomWidest;
            Check(Mathf.Approximately(RtsCamera.ZoomClosest, WantClosest) && Mathf.Approximately(rts.minHeight, WantClosest) && Mathf.Approximately(rts.maxHeight, widest),
                  $"K1 the zoom limits {rts.minHeight:0.##} / {rts.maxHeight:0.##} m (want {WantClosest:0} / {widest:0}: the closest {OldClosest:0} -> {WantClosest:0} m, the widest unchanged)");
            rts.SetHeightImmediate(5f); float asked5 = rts.height;
            rts.SetHeightImmediate(70f); yield return 3;
            float step = 1f - rts.wheelStep; var heights = new List<float>();
            for (int i = 0; i < 12; i++)
            {
                rts.ZoomBy(step);
                yield return Wait(1.2f);
                heights.Add(rts.height);
                if (rts.height <= rts.minHeight + 0.01f) break;
            }
            string hs = string.Join(", ", heights.Select(x => x.ToString("0.0")));
            bool notchOk = heights.Count == 5 && Mathf.Abs(heights[3] - 70f * Mathf.Pow(step, 4)) < 0.05f && Mathf.Abs(heights[4] - WantClosest) < 0.01f;
            Check(Mathf.Approximately(asked5, WantClosest) && notchOk,
                  $"K2 asked 5 m: {asked5:0.##} m; wheel in from 70 m (x {step:0.00} a notch): {hs} = {heights.Count} notches (want 5: 4 to 36.5 m and a short 5th to {WantClosest:0}; before: 6)");

            // K3 the fixed closest view, the map's middle
            float h = rts.minHeight, d = h / sinP;
            OzCamEdge.Snap(); rts.FreeLookOff(); Pose(middle, h); yield return 3;
            float fNear = Nearest(), fBottom = Across(0f), fMid = Across(0.5f), fY = Eye.y;
            Shows(h, rts.pitch, out float eNear, out float eBottom, out float eMid);
            Check(Mathf.Abs(fY - h) < 0.01f && Mathf.Abs(fNear / eNear - 1f) < 0.01f && Mathf.Abs(fBottom / eBottom - 1f) < 0.01f && Mathf.Abs(fMid / eMid - 1f) < 0.01f,
                  $"K3 the fixed closest view: camera {fY:0.##} m over the focus plane ({OverGround():0.##} m over the ground under it), ground across the bottom row {fBottom:0.##} m and the centre {fMid:0.##} m, " +
                  $"nearest ground {fNear:0.##} m (the camera's numbers give {eBottom:0.##} / {eMid:0.##} / {eNear:0.##}; at 30 m they were {eBottom * OldClosest / h:0.##} / {eMid * OldClosest / h:0.##} / {eNear * OldClosest / h:0.##})");
            float yRule = RtsCamera.FreeNearDistance(h, d, lo, h, rts.pitch) * Mathf.Sin(lo * Mathf.Deg2Rad);

            // K4 the free camera at the closest zoom and the lowest tilt, inside the map
            {
                bool all = true; var sb = new StringBuilder();
                foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
                {
                    OzCamEdge.Snap(); Pose(middle, h); bool took = View(yaw, lo); yield return 3;
                    var e = Eye; float off = CentreOff(), near = Nearest(), bottom = Across(0f), mid = Across(0.5f), over = OverGround();
                    bool good = took && off <= 0.05f && e.y >= yRule - 0.05f && over >= RtsCamera.FreeClear - 0.05f && near >= fNear - 0.05f && bottom >= fBottom - 0.05f && mid >= fMid - 0.05f;
                    all &= good;
                    sb.Append($" heading {yaw:0}: camera {e.y:0.##} m up ({over:0.##} m over the ground under it), {Vector3.Distance(e, rts.focus):0.#} m from the focus, centre {off:0.###} m off, nearest ground {near:0.##} m, " +
                              $"across the bottom {bottom:0.##} m (real ground {AcrossReal(0f):0.#}), the centre {mid:0.##} m (real {AcrossReal(0.5f):0.#}){(good ? "" : " <- SHORT")};");
                }
                Check(all, $"K4 free camera, closest zoom, lowest tilt {lo:0}, inside the map (want at least {yRule:0.##} m up, 3 m over the ground, nearest ground {fNear:0.##} m, across {fBottom:0.##} / {fMid:0.##} m: the fixed closest view's):{sb}");
            }

            // K5 the same at the four map borders, the camera outside the map
            {
                bool all = true; var sb = new StringBuilder();
                var border = new (string name, Vector3 at, float yaw)[]
                {
                    ("lower", new Vector3(middle.x, 0f, -99999f), 0f), ("upper", new Vector3(middle.x, 0f, 99999f), 180f),
                    ("left", new Vector3(-99999f, 0f, middle.z), 90f), ("right", new Vector3(99999f, 0f, middle.z), -90f),
                };
                foreach (var b in border)
                {
                    OzCamEdge.Snap(); Pose(b.at, h); bool took = View(b.yaw, lo); yield return 3;
                    var e = Eye; float off = CentreOff(), near = Nearest(), bottom = Across(0f), mid = Across(0.5f), over = OverGround();
                    bool outside = e.x < 0f || e.x > rts.mapSizeMeters.x || e.z > 0f || e.z < -rts.mapSizeMeters.y;
                    bool good = took && off <= 0.05f && e.y >= yRule - 0.05f && (!outside || e.y >= RtsCamera.FreeEdgeHeight - 0.05f) && over >= RtsCamera.FreeClear - 0.05f
                                && near >= fNear - 0.05f && bottom >= fBottom - 0.05f && mid >= fMid - 0.05f;
                    all &= good;
                    sb.Append($" {b.name}: camera {(outside ? "outside" : "INSIDE")} the map, {e.y:0.##} m up ({over:0.##} m over the ground under it), nearest ground {near:0.##} m, across the bottom {bottom:0.##} m " +
                              $"(real ground {AcrossReal(0f):0.#}), the centre {mid:0.##} m (real {AcrossReal(0.5f):0.#}){(good ? "" : " <- SHORT")};");
                }
                OzCamEdge.Snap();
                Check(all, $"K5 free camera, closest zoom, lowest tilt, at the map borders (also at least the border floor {RtsCamera.FreeEdgeHeight:0} m once outside):{sb}");
            }

            // K6 every tilt at the closest zoom: the tilt is the one asked, the nearest ground never nearer than the fixed view's
            {
                bool all = true; float worst = 99f; var sb = new StringBuilder();
                foreach (float t in new[] { lo, 30f, 35f, 40f, 45f, 50f, 55f, 70f, hi })
                {
                    Pose(middle, h); View(0f, t); yield return 3;
                    var e = Eye; float asked = rts.Tilt, drawn = Drawn(), near = Nearest();
                    bool lifted = e.y <= FloorAt(e.x, e.z, h) + 0.05f && drawn > asked + 0.05f;
                    bool good = (Mathf.Abs(drawn - asked) <= 0.05f || lifted) && near >= fNear - 0.05f;
                    all &= good; worst = Mathf.Min(worst, near / Mathf.Max(0.01f, fNear));
                    sb.Append($" {asked:0}: drawn {drawn:0.##}{(lifted ? " (on its floor)" : "")}, nearest {near:0.##} m, {Vector3.Distance(e, rts.focus):0.#} m from the focus{(good ? "" : " <- WRONG")};");
                }
                Check(all, $"K6 every tilt at the closest zoom: drawn as asked, the nearest ground at least x {worst:0.0000} of the fixed closest view's (needs 1):{sb}");
            }

            // K7 the zoom with the lowest tilt: no dead notch, nothing changed from 70 m on, the steepest tilt keeps the zoom's distance
            {
                var sb = new StringBuilder(); float last = 0f; bool grows = true;
                var zooms = new List<float>(); for (float z = h; z < 70f - 0.01f; z /= step) zooms.Add(z);
                zooms.Add(70f);
                foreach (float z in zooms)
                {
                    Pose(middle, z); View(0f, lo); yield return 2;
                    float dist = Vector3.Distance(Eye, rts.focus); grows &= dist > last + 0.05f; last = dist;
                    sb.Append($" {z:0.#} m: {dist:0.##} m from the focus, {Eye.y:0.##} m up;");
                }
                float worst = 0f;
                foreach (float z in new[] { 70f, widest })
                {
                    Pose(middle, z); View(0f, lo); yield return 2;
                    var e = Eye; bool floored = e.y <= FloorAt(e.x, e.z, z) + 0.05f;
                    if (!floored) worst = Mathf.Max(worst, Mathf.Abs(Vector3.Distance(e, rts.focus) - z / sinP));
                }
                Pose(middle, h); View(0f, hi); yield return 2;
                float steep = Mathf.Abs(Vector3.Distance(Eye, rts.focus) - d);
                Check(grows && worst < 0.05f && steep < 0.05f,
                      $"K7 the wheel with the lowest tilt, each notch out moves the camera farther {(grows ? "yes" : "NO")}:{sb} at 70 and {widest:0} m the zoom's own distance (worst {worst:0.###} m off); the steepest tilt at the closest zoom {steep:0.###} m off the zoom's distance");
            }
            rts.FreeLookOff(); OzCamEdge.Snap();

            // K8 the pictures: the focus on a robot of ours beside one of our vehicles (a grunt and a light tank preferred)
            var robots = g.World.Objects.Where(o => o.Owner == g.player && o.Kind == ZKind.Robot && !o.Destroyed && g.Views.TryGetValue(o.Id, out var uv) && IsDrawn(uv)).ToList();
            var vehicles = g.World.Objects.Where(o => o.Owner == g.player && o.Kind == ZKind.Vehicle && !o.Destroyed && g.Views.TryGetValue(o.Id, out var uv) && IsDrawn(uv)).ToList();
            ZObject bot = null, veh = null; float best = float.MaxValue, apart = 0f;
            foreach (var r in robots)
                foreach (var v in vehicles)
                {
                    float dd = Vector3.Distance(g.Views[r.Id].transform.position, g.Views[v.Id].transform.position);
                    float score = dd + (r.Type == "grunt" ? 0f : 6f) + (v.Type == "light" ? 0f : 6f);
                    if (dd <= 10f && score < best) { best = score; bot = r; veh = v; apart = dd; }
                }
            if (bot == null && robots.Count > 0) bot = robots.FirstOrDefault(r => r.Type == "grunt") ?? robots[0];
            UnitView bv = bot != null ? g.Views[bot.Id] : null, vv = veh != null ? g.Views[veh.Id] : null;
            Vector3 aim;
            if (bv && vv) aim = (bv.transform.position + vv.transform.position) * 0.5f;
            else if (bv) aim = bv.transform.position;
            else aim = middle;
            var focus = new Vector3(aim.x, 0f, aim.z + Mathf.Max(0f, aim.y) / Mathf.Tan(rts.pitch * Mathf.Deg2Rad));   // a unit standing higher is seen that much farther north on the plane
            Note($"pictures' focus {focus.x:0.#}, {focus.z:0.#}: {(bot != null ? bot.Type + " #" + bot.Id : "no robot of ours")}" +
                 $"{(veh != null ? $" and {veh.Type} #{veh.Id} {apart:0.#} m apart" : vehicles.Count == 0 ? " (no vehicle of ours: drop a light tank beside a grunt squad with the testing overlay ALT+P)" : " (no vehicle of ours within 10 m of a robot of ours)")}");

            var files = new List<string>();
            var shots = new (string name, float h, bool free, bool backOff)[] { ("fixed_h30_old", OldClosest, false, true), ("fixed_h35_new", WantClosest, false, true), ("free_h30_old", OldClosest, true, false), ("free_h35_new", WantClosest, true, true) };
            var botPx = new Vector2[shots.Length]; var depth = new float[shots.Length];
            for (int i = 0; i < shots.Length; i++)
            {
                rts.minHeight = shots[i].h < minH0 ? shots[i].h : minH0;                                 // the old closest only for its own picture
                RtsCamera.FreeNearOn = shots[i].backOff;                                                  // off only for the old free picture
                OzCamEdge.Snap(); rts.FreeLookOff(); Pose(focus, shots[i].h);
                if (shots[i].free) View(0f, lo);
                yield return Wait(0.6f);
                Pose(focus, shots[i].h); if (shots[i].free) View(0f, lo);
                yield return 3;
                botPx[i] = ScreenBox(bv, out var bm); var vb = ScreenBox(vv, out _);
                depth[i] = cam ? cam.transform.InverseTransformPoint(bm).z : 0f;
                var e = Eye;
                Note($"{shots[i].name}: camera {e.y:0.##} m up ({OverGround():0.##} over the ground), {Vector3.Distance(e, rts.focus):0.#} m from the focus, tilt {Drawn():0.#}, nearest ground {Nearest():0.#} m, " +
                     $"across the bottom {Across(0f):0.#} m, the centre {Across(0.5f):0.#} m; {(bv ? bot.Type : "robot")} {botPx[i].x:0} x {botPx[i].y:0} px; {(vv ? veh.Type : "vehicle")} {vb.x:0} x {vb.y:0} px");
                yield return Picture($"closest_{level}_{shots[i].name}", files);
            }
            RtsCamera.FreeNearOn = near0; rts.FreeLookOff(); rts.minHeight = minH0;
            bool written = files.Count == shots.Length && files.All(File.Exists);
            var dups = OzProofPictures.Duplicates(files);
            float diffFixed = written ? OzProofPictures.MeanLumaDiff(files[0], files[1]) : -1f, diffFree = written ? OzProofPictures.MeanLumaDiff(files[2], files[3]) : -1f;
            string pairFixed = Path.Combine(dir, $"closest_{level}_pair_fixed_30_35.jpg"), pairFree = Path.Combine(dir, $"closest_{level}_pair_free_30_35.jpg");
            bool paired = written && OzProofPictures.Pair(files[0], files[1], pairFixed, 1080) && OzProofPictures.Pair(files[2], files[3], pairFree, 1080);
            string k18 = Path.Combine(OzV12ZoomInProof.Folder(), $"zoomin_{level}_h30_new.png"), pairK18 = Path.Combine(dir, $"closest_{level}_pair_k18_30_35.jpg");
            string k18Note = File.Exists(k18) ? (written && OzProofPictures.Pair(k18, files[1], pairK18, 1080) ? "k18's 30 m picture paired" : "k18's 30 m picture NOT paired") : "k18's 30 m picture not on this machine";
            float ratio = botPx[0].y > 1f ? botPx[1].y / botPx[0].y : 0f, wantRatio = depth[1] > 0.01f ? depth[0] / depth[1] : 0f;
            bool sizeOk = bv == null || (ratio > 0f && wantRatio > 0f && Mathf.Abs(ratio / wantRatio - 1f) <= 0.05f);
            Check(written && dups.Count == 0 && diffFixed > 2f && diffFree > 2f && paired && sizeOk,
                  $"K8 pictures {files.Count}/{shots.Length} written in {dir}, the same files: {(dups.Count == 0 ? "none" : string.Join(", ", dups))}, old vs new differ by {diffFixed:0.0} (fixed) and {diffFree:0.0} (free) of 255, " +
                  $"pairs {(paired ? "written" : "NOT written")}, {k18Note}; " +
                  (bv ? $"the {bot.Type} on screen at {WantClosest:0} m is {ratio:0.000} x its size at {OldClosest:0} m (the camera's distance gives {wantRatio:0.000})" : "no robot of ours to measure (pictures only)"));

            // K9 put back
            PutBack(); yield return 3;
            Check(Mathf.Approximately(rts.minHeight, WantClosest) && Mathf.Abs(rts.height - Mathf.Clamp(h00, rts.minHeight, rts.maxHeight)) < 0.01f && RtsCamera.FreeNearOn && !rts.FreeLook && OzFreeCam.On == (!hadPref || prefWas == 1),
                  $"K9 put back: the closest limit {rts.minHeight:0.##} m, the camera at {rts.height:0.##} m (was {h00:0.##}), the back-off {(RtsCamera.FreeNearOn ? "on" : "OFF")}, FREE CAMERA {(OzFreeCam.On ? "on" : "off")} (was {(!hadPref || prefWas == 1 ? "on" : "off")})");
        }

        // ------------------------------------------------------------------------------------------------ the Unit editor's playground
        static IEnumerator Playground()
        {
            g = ZGame.I; var ue = OzUnitEditor.I; rts = ue ? ue.Rts : null;
            level = "UnitEditor";
            if (g == null || !rts) { Check(false, "the playground has no game or no RTS camera"); yield break; }
            cam = rts.GetComponent<Camera>();
            Save();
            yield return 2;
            Check(Mathf.Approximately(rts.minHeight, RtsCamera.UeClosest), $"U1 the playground's closest zoom {rts.minHeight:0.##} m (want {RtsCamera.UeClosest:0}, as it was; a match's is {RtsCamera.ZoomClosest:0})");
            float h = rts.minHeight, d = h / Mathf.Sin(rts.pitch * Mathf.Deg2Rad), lo = Mathf.Max(22f, RtsCamera.FreeTiltMin);
            var middle = new Vector3(rts.mapSizeMeters.x * 0.5f, 0f, -rts.mapSizeMeters.y * 0.5f);
            Pose(middle, h); bool took = View(0f, lo); yield return 3;
            var e = Eye; float dist = Vector3.Distance(e, rts.focus);
            bool floored = e.y <= FloorAt(e.x, e.z, h) + 0.05f;
            Check(took && (floored || Mathf.Abs(dist - d) < 0.05f), $"U2 the playground's free look at {h:0} m and its lowest tilt {rts.Tilt:0}: {dist:0.##} m from the focus (the zoom's {d:0.##}: no back-off){(floored ? ", on its floor" : "")}");
            rts.FreeLookOff();
        }
    }
}
