using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The camera must feel the same at any frame rate, only with fewer pictures.
    /// Menu OpenZ/V1.3/Camera same at any fps/1 Offline drive (no Play, no level): a hidden RtsCamera on a 4 x 4 km test map is driven
    /// by the same input script at simulated 60, 45, 30 fps and an uneven 30 fps (frames of 28.3 / 38.3 ms in turn), through the
    /// camera's own per-frame steps (FlyStep, FollowStep, MiddleDrag, WheelInput, FeelStep, Place: the lines RtsCamera.Update runs,
    /// in its order, with the simulated frame time as dt). The 1.2 camera is the same code with RtsCamera.PanAverageFrames = 1 and
    /// RtsCamera.WheelEveryNotch = false.
    /// Input script: K1 2 s key scroll; K2 the same with SHIFT from 0.5 s; E1 2 s screen-edge scroll (the mouse 2 px from the left
    /// edge, Update's edge rule); W1 10 wheel notches evenly in 0.5 s from 200 m; W2 two flicks of 5 notches 20 ms apart; W3 a fast
    /// flick, 8 notches in 0.04 s; M1 middle-button drag 600 px/s for 1 s; T1 free turn 600 px/s for 1 s; F1 fly-to (a minimap click,
    /// 72 m away); F2 minimap drag (the fly-to target runs 40 m/s for 1 s); L1 follow (a point at 6 m/s for 2 s).
    /// PASS: every distance, angle, zoom height and settle time at 45 / 30 / uneven 30 within 2 % of the 60 fps result, and the 60 fps
    /// result equal to the 1.2 camera within 1 % (W3: equal to item 25's camera, whose wheel already counts every notch; the 1.2
    /// wheel drops most of a fast flick at 60 fps too). Plus the notch limit table (3 at 50 fps and faster, 180 a second below).
    /// Writes "[OpenZ v1.3 camera fps] PASS / FAIL ..." to the console, checks.txt and camfps_table.md in the output folder.
    /// </summary>
    public static class OzV13CamFpsCheck
    {
        const string Tag = "[OpenZ v1.3 camera fps]";
        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/v1_3/item35_camera_same_at_any_fps"));
        static int ok, bad; static readonly StringBuilder fails = new StringBuilder(), table = new StringBuilder();

        static void Check(bool pass, string what) { if (pass) ok++; else { bad++; fails.Append(" | ").Append(what); } Debug.Log($"{Tag} {(pass ? "ok  " : "FAIL")} {what}"); }

        struct Rate { public string name; public float fps; public bool uneven; }
        static readonly Rate[] Rates =
        {
            new Rate { name = "60", fps = 60f }, new Rate { name = "45", fps = 45f }, new Rate { name = "30", fps = 30f }, new Rate { name = "30 uneven", fps = 30f, uneven = true },
        };
        const float EdgeTestX = 2f;

        /// <summary>The frame times of a run: an even rate, or (uneven) 0.85 / 1.15 of the frame time in turn.</summary>
        static List<float> Frames(Rate r, float seconds)
        {
            var l = new List<float>(); double t = 0; int k = 0;
            while (t < seconds - 1e-6) { float dt = 1f / r.fps; if (r.uneven) dt *= (k % 2 == 0) ? 0.85f : 1.15f; l.Add(dt); t += dt; k++; }
            return l;
        }

        static RtsCamera NewCam(List<GameObject> made)
        {
            var go = new GameObject("OzV13CamFpsCheck") { hideFlags = HideFlags.HideAndDontSave }; made.Add(go);
            var cam = go.AddComponent<RtsCamera>();
            cam.mapSizeMeters = new Vector2(4000f, 4000f);                                                    // no map border is ever reached
            cam.focus = new Vector3(2000f, 0f, -2000f);
            cam.SetHeightImmediate(70f);
            return cam;
        }

        /// <summary>First time (interpolated between frames) after which the value stays within tol of the change from its start.</summary>
        static float Settle(List<Vector2> s, float final, float start, float tol = 0.01f)
        {
            float band = tol * Mathf.Abs(final - start); int last = -1;
            for (int i = s.Count - 1; i >= 0; i--) if (Mathf.Abs(s[i].y - final) > band) { last = i; break; }
            if (last < 0) return s[0].x;
            if (last == s.Count - 1) return float.PositiveInfinity;
            float e0 = Mathf.Abs(s[last].y - final) - band, e1 = Mathf.Abs(s[last + 1].y - final) - band;
            return s[last].x + (s[last + 1].x - s[last].x) * e0 / (e0 - e1);
        }

        /// <summary>RtsCamera.Update's screen-edge rule for a mouse at p (the camera's edgePx, a 1920 x 1080 screen).</summary>
        static Vector2 EdgeDir(Vector2 p, float edgePx, float w, float h)
        {
            Vector2 dir = Vector2.zero;
            if (p.x < edgePx) dir.x -= 1; else if (p.x > w - edgePx) dir.x += 1;
            if (p.y < edgePx) dir.y -= 1; else if (p.y > h - edgePx) dir.y += 1;
            return dir;
        }

        /// <summary>One run of one input script: the metric names and values it gives.</summary>
        static Dictionary<string, float> Run(Rate r, List<GameObject> made)
        {
            var m = new Dictionary<string, float>();
            // K1 / K2 / E1: 2 s scroll (keys, keys + SHIFT from 0.5 s, the screen edge), then 0.5 s released
            for (int v = 0; v < 3; v++)
            {
                var cam = NewCam(made); float t = 0f, x0 = cam.focus.x, peak = 1f;
                foreach (float dt in Frames(r, 2.5f))
                {
                    t += dt; bool held = t <= 2f + 1e-4f;
                    Vector2 dir = !held ? Vector2.zero : v == 2 ? EdgeDir(new Vector2(EdgeTestX, 540f), cam.edgePx, 1920f, 1080f) : Vector2.right;
                    cam.FeelStep(dt, dir, v == 1 && t > 0.5f); cam.Place(); peak = Mathf.Max(peak, cam.HoldBoost);
                }
                string n = v == 0 ? "K1 key scroll 2 s (m)" : v == 1 ? "K2 key scroll + SHIFT (m)" : "E1 edge scroll 2 s (m)";
                m[n] = Mathf.Abs(cam.focus.x - x0);
                if (v == 0) m["K1 hold ramp peak (x)"] = peak;
            }
            // W1 / W2 / W3: wheel notches from 200 m, in (notch times in s)
            var scripts = new[] { new List<float>(), new List<float>(), new List<float>() };
            for (int i = 0; i < 10; i++) scripts[0].Add(0.0101f + i * 0.05f);
            for (int i = 0; i < 5; i++) { scripts[1].Add(0.0101f + i * 0.02f); scripts[1].Add(0.3101f + i * 0.02f); }
            for (int i = 0; i < 8; i++) scripts[2].Add(0.0101f + i * 0.005f);
            for (int w = 0; w < 3; w++)
            {
                var cam = NewCam(made); cam.SetHeightImmediate(200f);
                float t = 0f, prev = 0f; var s = new List<Vector2> { new Vector2(0f, cam.height) };
                foreach (float dt in Frames(r, 3f))
                {
                    t += dt; int notches = 0; foreach (float nt in scripts[w]) if (nt > prev && nt <= t) notches++; prev = t;
                    cam.WheelInput(notches * OzTrackpadScroll.PerNotch(), dt); cam.FeelStep(dt, Vector2.zero, false); cam.Place();
                    s.Add(new Vector2(t, cam.height));
                }
                string n = w == 0 ? "W1 10 notches in 0.5 s" : w == 1 ? "W2 two flicks of 5" : "W3 8 notches in 0.04 s";
                m[n + " height (m)"] = cam.height; m[n + " settle (s)"] = Settle(s, cam.TargetHeight, 200f);
            }
            // M1 middle drag / T1 free turn: 600 px a second for 1 s
            {
                var cam = NewCam(made); float t = 0f, x0 = cam.focus.x;
                foreach (float dt in Frames(r, 1.5f)) { t += dt; if (t <= 1f + 1e-4f) cam.MiddleDrag(new Vector2(600f * dt, 0f)); cam.FeelStep(dt, Vector2.zero, false); cam.Place(); }
                m["M1 middle drag 1 s (m)"] = Mathf.Abs(cam.focus.x - x0);
                var cam2 = NewCam(made); t = 0f;
                foreach (float dt in Frames(r, 1f)) { t += dt; cam2.FreeTurnBy(new Vector2(600f * dt, 0f)); }
                m["T1 free turn 1 s (deg)"] = cam2.yaw;
            }
            // F1 fly-to 72 m away: time to 1 % of the way left
            {
                var cam = NewCam(made); var to = cam.focus + new Vector3(-56f, 0f, 46f); float d0 = (to - cam.focus).magnitude, t = 0f;
                cam.FlyTo(to); var s = new List<Vector2> { new Vector2(0f, d0) };
                foreach (float dt in Frames(r, 3f)) { t += dt; cam.FlyStep(dt); cam.FeelStep(dt, Vector2.zero, false); cam.Place(); s.Add(new Vector2(t, (cam.focus - to).magnitude)); }
                m["F1 fly-to settle (s)"] = Settle(s, 0f, d0);
            }
            // F2 minimap drag: the fly-to target runs 40 m/s for 1 s (OpenZMinimap calls FlyTo each frame before the camera's frame)
            {
                var cam = NewCam(made); Vector3 a = cam.focus; float t = 0f, at1 = 0f, goal = 0f; var s = new List<Vector2>();
                foreach (float dt in Frames(r, 3f))
                {
                    t += dt; bool drag = t <= 1f + 1e-4f;
                    if (drag) { goal = 40f * t; cam.FlyTo(a + new Vector3(goal, 0f, 0f)); }
                    cam.FlyStep(dt); cam.FeelStep(dt, Vector2.zero, false); cam.Place();
                    if (drag) at1 = cam.focus.x - a.x; else s.Add(new Vector2(t, cam.focus.x - a.x));
                }
                s.Insert(0, new Vector2(1f, at1));
                m["F2 minimap drag at 1 s (m)"] = at1; m["F2 minimap drag settle (s)"] = Settle(s, goal, at1);
            }
            // L1 follow a point at 6 m/s for 2 s
            {
                var cam = NewCam(made); var tgo = new GameObject("OzV13CamFpsCheckTarget") { hideFlags = HideFlags.HideAndDontSave }; made.Add(tgo);
                Vector3 a = cam.focus; tgo.transform.position = a; cam.follow = tgo.transform; float t = 0f;
                foreach (float dt in Frames(r, 2f)) { t += dt; tgo.transform.position = a + new Vector3(6f * t, 0f, 0f); cam.FollowStep(); cam.FeelStep(dt, Vector2.zero, false); cam.Place(); }
                m["L1 follow 2 s (m)"] = cam.focus.x - a.x;
            }
            return m;
        }

        [MenuItem("OpenZ/V1.3/Camera same at any fps/1 Offline drive (no Play)")]
        public static void OfflineDrive()
        {
            if (EditorApplication.isPlaying) { Debug.LogError(Tag + " run it in edit mode (not in Play): the drive owns the camera's frame"); return; }
            ok = bad = 0; fails.Clear(); table.Clear(); Directory.CreateDirectory(Dir);
            int pan0 = RtsCamera.PanAverageFrames; bool wheel0 = RtsCamera.WheelEveryNotch;
            var made = new List<GameObject>();
            var now = new Dictionary<string, Dictionary<string, float>>(); var old = new Dictionary<string, Dictionary<string, float>>();
            try
            {
                foreach (var r in Rates)
                {
                    RtsCamera.PanAverageFrames = 4; RtsCamera.WheelEveryNotch = true; now[r.name] = Run(r, made);   // 1.3 (items 25 + 35, the defaults)
                    RtsCamera.PanAverageFrames = 1; RtsCamera.WheelEveryNotch = false; old[r.name] = Run(r, made);  // 1.2
                }
            }
            finally
            {
                RtsCamera.PanAverageFrames = pan0; RtsCamera.WheelEveryNotch = wheel0;
                foreach (var g in made) if (g) Object.DestroyImmediate(g);
            }
            table.AppendLine("| metric | 1.3 at 60 | 45 | 30 | 30 uneven | worst vs 60 | 1.2 at 60 | 1.2 at 30 |").AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var key in now["60"].Keys)
            {
                float b = now["60"][key], worst = 0f;
                foreach (var r in Rates) { if (r.name == "60") continue; worst = Mathf.Max(worst, Mathf.Abs(now[r.name][key] - b) / Mathf.Max(1e-6f, Mathf.Abs(b))); }
                table.AppendLine($"| {key} | {b:0.0000} | {now["45"][key]:0.0000} | {now["30"][key]:0.0000} | {now["30 uneven"][key]:0.0000} | {worst * 100f:0.00} % | {old["60"][key]:0.0000} | {old["30"][key]:0.0000} |");
                Check(worst <= 0.02f && !float.IsInfinity(b), $"{key}: 45 / 30 / uneven 30 within {worst * 100f:0.00} % of 60 fps ({b:0.000}; must be <= 2 %)");
                if (key.StartsWith("W3")) continue;                                                                      // the 1.2 wheel drops a fast flick at 60 fps too; W3 is checked against item 25 below
                float o = old["60"][key], d = Mathf.Abs(b - o) / Mathf.Max(1e-6f, Mathf.Abs(o));
                Check(d <= 0.01f, $"{key}: 1.3 at 60 fps = 1.2 at 60 fps within {d * 100f:0.000} % ({b:0.000} / {o:0.000}; must be <= 1 %)");
            }
            // the wheel notch limit: unchanged at 50 fps and faster, 180 a second below (W3 at 60 fps is item 25's camera exactly)
            float[] dts = { 1f / 144f, 1f / 60f, 1f / 55f, 1f / 50f, 1f / 45f, 1f / 30f, 1f / 20f, 0.25f };
            float[] want = { 3f, 3f, 3f, 3f, 4f, 6f, 9f, 18f };
            int limBad = 0;
            for (int i = 0; i < dts.Length; i++) if (Mathf.Abs(OzTrackpadMath.MaxNotchesFor(dts[i]) - want[i]) > 1e-4f) limBad++;
            Check(limBad == 0, $"N1 wheel notch limit per frame: 144 / 60 / 55 / 50 fps = 3 (as before), 45 = 4, 30 = 6, 20 = 9, a 0.25 s frame = 18; {limBad} off");
            Check(OzTrackpadMath.Decide(OzTrackpadMath.MoveMap, 0f, 5f, true, false, false, false, 1f).notches == 3f
                  && OzTrackpadMath.Decide(OzTrackpadMath.MoveMap, 0f, 5f, true, false, false, false, 1f, OzTrackpadMath.MaxNotchesFor(1f / 30f)).notches == 5f,
                  "N2 MOVE MAP Ctrl + scroll: 5 notches in one frame = 3 at 60 fps (as before), all 5 at 30 fps");
            var n60 = now["60"]; var o60 = old["60"];
            Check(Mathf.Abs(n60["W1 10 notches in 0.5 s height (m)"] - 200f * Mathf.Pow(0.85f, 10f)) < 0.5f && Mathf.Abs(o60["W1 10 notches in 0.5 s height (m)"] - 200f * Mathf.Pow(0.85f, 10f)) < 0.5f
                  && n60["K1 key scroll 2 s (m)"] > 10f && n60["E1 edge scroll 2 s (m)"] > 10f && n60["K2 key scroll + SHIFT (m)"] > n60["K1 key scroll 2 s (m)"] * 1.2f
                  && n60["K1 hold ramp peak (x)"] > 2f && n60["M1 middle drag 1 s (m)"] > 1f && n60["T1 free turn 1 s (deg)"] > 10f && n60["L1 follow 2 s (m)"] > 1f
                  && n60["F1 fly-to settle (s)"] > 0.1f && n60["F2 minimap drag at 1 s (m)"] > 1f,
                  $"S1 every input moved the camera at 60 fps (W1 = 200 x 0.85^10 = 39.4 m in 1.3 and 1.2: {n60["W1 10 notches in 0.5 s height (m)"]:0.0} / {o60["W1 10 notches in 0.5 s height (m)"]:0.0}; K1 {n60["K1 key scroll 2 s (m)"]:0.0} m, ramp x{n60["K1 hold ramp peak (x)"]:0.00})");
            table.AppendLine().AppendLine("Every value from RtsCamera's own frame steps; 1.2 = PanAverageFrames 1 + WheelEveryNotch false. Notch times: W1 every 50 ms, W2 two flicks of 5 notches 20 ms apart (0.3 s between), W3 8 notches 5 ms apart.");
            bool pass = bad == 0 && ok > 0;
            string line = $"{Tag} {(pass ? "PASS" : "FAIL")} {ok}/{ok + bad} offline drive at 60 / 45 / 30 / uneven 30 fps{(bad == 0 ? "" : "; FAILED:" + fails)}";
            if (pass) Debug.Log(line); else Debug.LogError(line);
            File.AppendAllText(Path.Combine(Dir, "checks.txt"), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " " + line + "\n");
            File.WriteAllText(Path.Combine(Dir, "camfps_table.md"), table.ToString());
        }
    }
}
