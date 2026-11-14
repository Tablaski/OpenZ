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
    /// Version 1.2: the closest zoom a little less close (RtsCamera.ZoomClosest 25 m -> 30 m, one wheel notch less; the widest zoom,
    /// the 70 m start, the wheel step, the easing and the scroll speed unchanged; the second step, 30 m -> 35 m of item 54, has its
    /// own check, OzV12ClosestZoomProof.cs: this one keeps showing the first step, its limits checked against the closest now in
    /// force). Counted Play check and pictures, editor only; it changes nothing in the game: the camera, its closest limit, the
    /// testing overlay and the follow are put back when it ends.
    /// Menu "OpenZ/V1.2/Zoom-in: closest zoom check + pictures (Play)", in Play on a level after the briefing (run it on L02 and on
    /// L16), the HUD shown, no menu open, the Game view at 1920 x 1080, hands off the mouse and the keys. One log line
    /// "[OpenZ V1.2 zoom-in] PASS n/n" or "FAIL" (also in Report and in the report file next to the pictures, Folder()).
    ///  Z1 the limits in force: the closest RtsCamera.ZoomClosest (this step's 30 m or higher: 35 m since item 54), the widest
    ///     unchanged (200 m; level 16 its own OzL16Outskirts.MaxHeight).
    ///  Z2 a zoom asked past either end stops on it (5 m gives the closest, 900 m gives the widest).
    ///  Z3 the wheel's steps in from the 70 m start (ZoomBy with the wheel's own step, the same clamp as a notch): full notches down
    ///     to the closest and a short last one onto it (30 m: 5 notches to 31.1 m and a short 6th; 35 m: 4 to 36.5 m and a short 5th;
    ///     version 1.1: 6 notches and a short 7th to 25 m).
    ///  Z4 the easing is the same: 70 m -> 30 m (this step's closest; the limit lowered to it for that move while the closest is
    ///     higher) settles (within 0.05 m) in the same time as 70 m -> 25 m did (the 1.1 move, the limit lowered for it only), both
    ///     0.5 .. 1.2 s.
    ///  Z5 the scroll speed at 25, 30, 70 m and the widest is the one of the old 22..230 m span (unchanged at every height).
    ///  Z6 the pictures, the normal RTS camera (pitch 55, north up), one focus (a robot of ours beside one of our vehicles when there is
    ///     one): zoomin_LEVEL_h25_old (the 1.1 closest, the limit lowered for that picture only), zoomin_LEVEL_h30_new (lowered the
    ///     same way while the closest is higher), zoomin_LEVEL_h70
    ///     and the pair zoomin_LEVEL_pair_25_30.jpg; all written, no two the same, the old and the new differ; the robot on screen at
    ///     30 m is the size the camera's distance gives (25 / 30 of its size at 25 m, within 5 %). The robot's and the vehicle's boxes
    ///     on screen (px) are in the report.
    ///  Z7 put back: the closest limit in force again (RtsCamera.ZoomClosest), the camera at its height and focus from before.
    /// </summary>
    public static class OzV12ZoomInProof
    {
        /// <summary>The closest zoom of version 1.1 (m): only for the old picture and the old easing, with the limit lowered for them.</summary>
        public const float OldClosest = 25f;
        /// <summary>The closest zoom of this step (m; the second step, item 54, moved the closest on to 35 m: OzV12ClosestZoomProof.cs).</summary>
        public const float WantClosest = 30f;
        public static string Report = "not run";
        const string Tag = "[OpenZ V1.2 zoom-in]";
        static readonly Stack<IEnumerator> run = new Stack<IEnumerator>(); static int waitFrame, ok, bad;
        static readonly StringBuilder fails = new StringBuilder(), notes = new StringBuilder();
        static string dir = "", level = "";
        static RtsCamera rts; static Camera cam;
        static float minH0 = -1f, h00; static Vector3 focus0; static Transform follow0; static Vector2 drift0; static bool shown0, saved;

        [MenuItem("OpenZ/V1.2/Zoom-in: closest zoom check + pictures (Play)")]
        public static void RunMenu()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning(Tag + " enter Play mode on a level first (after the briefing)"); return; }
            if (run.Count > 0) { Debug.LogWarning(Tag + " a run is still going"); return; }
            ok = bad = 0; fails.Clear(); notes.Clear(); Report = "running"; saved = false;
            var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor"); if (gv != null) EditorWindow.FocusWindowIfItsOpen(gv);
            run.Push(Proof()); waitFrame = 0; EditorApplication.update -= Step; EditorApplication.update += Step;
        }

        /// <summary>The pictures and the report: the work tree's records, milestones/zoom_in_v12 (OpenZPaths: no machine path in code).</summary>
        public static string Folder() => Path.Combine(OpenZPaths.Records, "milestones", "zoom_in_v12");

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

        /// <summary>The camera as it was: its closest limit, height, focus, follow and drift, the testing overlay. Safe to call twice.</summary>
        static void PutBack()
        {
            if (!saved || !rts) return;
            saved = false;
            rts.minHeight = minH0; rts.FreeLookOff();
            rts.focus = focus0; rts.SetHeightImmediate(h00); rts.follow = follow0; rts.autoDrift = drift0;
            if (shown0 && OzCheatSpawner.I) OzCheatSpawner.I.Show(true);
        }

        static void Finish()
        {
            Report = $"{Tag} {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad} checks, level {level}, at {Screen.width}x{Screen.height}:{notes}{(bad > 0 ? " FAILED:" + fails : "")}";
            if (bad == 0) Debug.Log(Report); else Debug.LogError(Report);
            if (dir.Length > 0)
            {
                try { File.WriteAllText(Path.Combine(dir, $"zoomin_{level}_report.txt"), Report.Replace("; ", ";\n") + "\n"); } catch (System.Exception e) { Debug.LogWarning(Tag + " report not written: " + e.Message); }
            }
        }

        /// <summary>Waits real seconds (unscaled), one frame at a time.</summary>
        static IEnumerator Wait(float s) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < s) yield return 1; }

        /// <summary>The unit is drawn: its view is active and at least one of its mesh renderers is on (not a unit hidden inside another).</summary>
        static bool Drawn(UnitView v) => v && v.gameObject.activeInHierarchy && v.GetComponentsInChildren<Renderer>(false).Any(r => r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer));

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

        /// <summary>Settles the eased zoom from the 70 m start to h (ZoomTo): secs[slot] = the real seconds until it is within 0.05 m of
        /// the height the limit gives (3 s at most).</summary>
        static IEnumerator Ease(float h, float[] secs, int slot)
        {
            rts.SetHeightImmediate(70f); yield return 3;
            float want = Mathf.Clamp(h, rts.minHeight, rts.maxHeight), t0 = Time.unscaledTime;
            rts.ZoomTo(h);
            while (Mathf.Abs(rts.height - want) > 0.05f && Time.unscaledTime - t0 < 3f) yield return 1;
            secs[slot] = Time.unscaledTime - t0;
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

        static IEnumerator Proof()
        {
            var g = ZGame.I; var hud = OpenZHud.I; rts = hud ? hud.rts : null;
            level = g != null ? g.level : "";
            if (g == null || g.World == null || !hud || !rts) { Check(false, "no level running (Play a level, after the briefing)"); yield break; }
            if (RtsCamera.InputLocked || !rts.enabled) { Check(false, "the game camera is held by a cinematic or the ending: run it in normal play"); yield break; }
            cam = rts.GetComponent<Camera>();
            minH0 = rts.minHeight; h00 = rts.height; focus0 = rts.focus; follow0 = rts.follow; drift0 = rts.autoDrift; shown0 = OzCheatSpawner.Visible; saved = true;
            rts.follow = null; rts.autoDrift = Vector2.zero; rts.FreeLookOff();
            if (OzCheatSpawner.Visible && OzCheatSpawner.I) OzCheatSpawner.I.Show(false);
            dir = Folder(); Directory.CreateDirectory(dir);
            yield return 2;

            // Z1 / Z2 the limits
            float widest = OzL16Outskirts.I ? Mathf.Min(RtsCamera.ZoomWidest, OzL16Outskirts.MaxHeight) : RtsCamera.ZoomWidest;
            Check(RtsCamera.ZoomClosest >= WantClosest && Mathf.Approximately(rts.minHeight, RtsCamera.ZoomClosest) && Mathf.Approximately(rts.maxHeight, widest),
                  $"Z1 the zoom limits {rts.minHeight:0.##} / {rts.maxHeight:0.##} m (want {RtsCamera.ZoomClosest:0} / {widest:0}: the closest {OldClosest:0} -> {WantClosest:0} m in this step, {RtsCamera.ZoomClosest:0} m now, the widest unchanged)");
            rts.SetHeightImmediate(5f); float lo = rts.height; rts.SetHeightImmediate(900f); float hi = rts.height;
            Check(Mathf.Approximately(lo, RtsCamera.ZoomClosest) && Mathf.Approximately(hi, widest), $"Z2 asked 5 m: {lo:0.##} m; asked 900 m: {hi:0.##} m");

            // Z3 the wheel's steps in from the 70 m start (each one settled before the next)
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
            int full = 0; while (70f * Mathf.Pow(step, full + 1) > rts.minHeight + 0.01f) full++;                                // full notches above the closest (30 m: 5, 35 m: 4)
            bool notchOk = full >= 1 && heights.Count == full + 1 && Mathf.Abs(heights[full - 1] - 70f * Mathf.Pow(step, full)) < 0.05f && Mathf.Abs(heights[full] - rts.minHeight) < 0.01f;
            Check(notchOk, $"Z3 wheel steps in from 70 m (x {step:0.00} a notch): {hs} = {heights.Count} notches (want {full + 1}: {full} to {70f * Mathf.Pow(step, full):0.0} m and a short one to {rts.minHeight:0}; this step at 30 m: 6, version 1.1: 7)");

            // Z4 the easing: the 1.1 move (70 -> 25, the limit lowered for it only) and the new one (70 -> 30)
            var secs = new float[2];
            rts.minHeight = OldClosest; yield return Ease(OldClosest, secs, 0);
            rts.minHeight = Mathf.Min(minH0, WantClosest); yield return Ease(WantClosest, secs, 1);           // this step's move (the limit lowered to 30 m while the closest is higher)
            rts.minHeight = minH0;
            Check(secs[0] >= 0.5f && secs[0] <= 1.2f && secs[1] >= 0.5f && secs[1] <= 1.2f && Mathf.Abs(secs[0] - secs[1]) <= 0.15f,
                  $"Z4 the eased zoom (wheel step {rts.wheelStep:0.00}, easing {rts.zoomSmooth:0.00} s): 70 -> {OldClosest:0} m settled in {secs[0]:0.00} s (1.1), 70 -> {WantClosest:0} m in {secs[1]:0.00} s");

            // Z5 the scroll speed keeps the old 22..230 m span at every height
            float worst = 0f; string speeds = "";
            foreach (float h in new[] { OldClosest, WantClosest, 70f, widest })
            {
                float was = rts.panSpeed * Mathf.Lerp(0.6f, 2.6f, Mathf.InverseLerp(22f, 230f, h)), now = rts.PanSpeedAt(h);
                worst = Mathf.Max(worst, Mathf.Abs(now - was)); speeds += $" {h:0} m {now:0.00} m/s;";
            }
            Check(worst < 1e-4f, $"Z5 scroll speed as before at every height:{speeds}");

            // Z6 the pictures: the focus on a robot of ours beside one of our vehicles (a grunt and a light tank preferred)
            var robots = g.World.Objects.Where(o => o.Owner == g.player && o.Kind == ZKind.Robot && !o.Destroyed && g.Views.TryGetValue(o.Id, out var uv) && Drawn(uv)).ToList();
            var vehicles = g.World.Objects.Where(o => o.Owner == g.player && o.Kind == ZKind.Vehicle && !o.Destroyed && g.Views.TryGetValue(o.Id, out var uv) && Drawn(uv)).ToList();
            ZObject bot = null, veh = null; float best = float.MaxValue, apart = 0f;
            foreach (var r in robots)
                foreach (var v in vehicles)
                {
                    float d = Vector3.Distance(g.Views[r.Id].transform.position, g.Views[v.Id].transform.position);
                    float score = d + (r.Type == "grunt" ? 0f : 6f) + (v.Type == "light" ? 0f : 6f);
                    if (d <= 10f && score < best) { best = score; bot = r; veh = v; apart = d; }
                }
            if (bot == null && robots.Count > 0) bot = robots.FirstOrDefault(r => r.Type == "grunt") ?? robots[0];
            UnitView bv = bot != null ? g.Views[bot.Id] : null, vv = veh != null ? g.Views[veh.Id] : null;
            Vector3 aim;
            if (bv && vv) aim = (bv.transform.position + vv.transform.position) * 0.5f;
            else if (bv) aim = bv.transform.position;
            else { var map = g.World.Map; aim = new Vector3(map.WidthPix / 16f, 0f, -map.HeightPix / 16f); }
            // the camera looks at a point of the y = 0 plane: a unit standing higher is seen that much farther north on it
            var focus = new Vector3(aim.x, 0f, aim.z + Mathf.Max(0f, aim.y) / Mathf.Tan(rts.pitch * Mathf.Deg2Rad));
            Note($"focus {focus.x:0.#}, {focus.z:0.#}: {(bot != null ? bot.Type + " #" + bot.Id : "no robot of ours")}" +
                 $"{(veh != null ? $" and {veh.Type} #{veh.Id} {apart:0.#} m apart" : NoVehicleNote(robots.Count, vehicles.Count))}");

            var files = new List<string>(); var shots = new (string name, float h)[] { ("h25_old", OldClosest), ("h30_new", WantClosest), ("h70", 70f) };
            var botPx = new Vector2[3]; var vehPx = new Vector2[3]; var depth = new float[3];
            for (int i = 0; i < shots.Length; i++)
            {
                rts.minHeight = shots[i].h < minH0 ? shots[i].h : minH0;                                 // the old closest only for its own picture
                rts.focus = focus; rts.FreeLookOff(); rts.SetHeightImmediate(shots[i].h);
                yield return Wait(0.6f);
                rts.focus = focus; rts.SetHeightImmediate(shots[i].h); yield return 2;
                botPx[i] = ScreenBox(bv, out var bm); vehPx[i] = ScreenBox(vv, out _);
                depth[i] = cam ? cam.transform.InverseTransformPoint(bm).z : 0f;
                yield return Picture($"zoomin_{level}_{shots[i].name}", files);
                Note($"{shots[i].name}: camera {rts.height:0.##} m; {(bv ? bot.Type : "robot")} {botPx[i].x:0} x {botPx[i].y:0} px; {(vv ? veh.Type : "vehicle")} {vehPx[i].x:0} x {vehPx[i].y:0} px");
            }
            rts.minHeight = minH0;
            bool written = files.Count == 3 && files.All(File.Exists);
            var dups = OzProofPictures.Duplicates(files);
            float diff = written ? OzProofPictures.MeanLumaDiff(files[0], files[1]) : -1f;
            string pair = Path.Combine(dir, $"zoomin_{level}_pair_25_30.jpg");
            bool paired = written && OzProofPictures.Pair(files[0], files[1], pair, 1080);
            float ratio = botPx[0].y > 1f ? botPx[1].y / botPx[0].y : 0f, wantRatio = depth[1] > 0.01f ? depth[0] / depth[1] : 0f;
            bool sizeOk = bv == null || (ratio > 0f && wantRatio > 0f && Mathf.Abs(ratio / wantRatio - 1f) <= 0.05f);
            Check(written && dups.Count == 0 && diff > 2f && paired && sizeOk,
                  $"Z6 pictures {files.Count}/3 written in {dir}, the same files: {(dups.Count == 0 ? "none" : string.Join(", ", dups))}, old vs new differ by {diff:0.0} of 255, pair {(paired ? "written" : "NOT written")}; " +
                  (bv ? $"the {bot.Type} on screen at {WantClosest:0} m is {ratio:0.000} x its size at {OldClosest:0} m (the camera's distance gives {wantRatio:0.000})" : "no robot of ours to measure (pictures only)"));

            // Z7 put back
            PutBack(); yield return 3;
            Check(Mathf.Approximately(rts.minHeight, RtsCamera.ZoomClosest) && Mathf.Abs(rts.height - Mathf.Clamp(h00, rts.minHeight, rts.maxHeight)) < 0.01f,
                  $"Z7 put back: the closest limit {rts.minHeight:0.##} m, the camera at {rts.height:0.##} m (was {h00:0.##})");
        }

        static string NoVehicleNote(int robots, int vehicles) => vehicles == 0 ? " (no vehicle of ours: drop a light tank beside a grunt squad with the testing overlay ALT+P for the tank's size)"
            : $" (no vehicle of ours within 10 m of a robot of ours: {robots} robots, {vehicles} vehicles)";
    }
}
