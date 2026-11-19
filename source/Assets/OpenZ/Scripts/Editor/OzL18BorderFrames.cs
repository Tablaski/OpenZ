using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 level 18 "Bridge Game", kit level18_surroundings (the Unity pass looks at the game with it): real Play frames of what lies
    /// outside the play area. Editor only, session only, nothing saved, no rule touched.
    ///  1 RTS border frames: the game camera itself (HUD on, as the player sees it) put on its limits at the four borders and the four
    ///    corners, at the normal (70 m) and the widest (200 m) zoom, by day and by night: 32 frames.
    ///  2 Camera views: the still camera (OzStillCam: the picture of the flight, HUD off) at both ends of every SHOTLIST_L18.md camera
    ///    at the start light of the level, the whole-map shot and six card-style obliques by day, night and dawn, a low free look
    ///    outward at eight border places by day and night, eight closer looks at the districts by day.
    /// Part 1 works on any level (the spots come from the map size of the level); part 2 is the list of level 18.
    /// From code: OzL18BorderFrames.Rts("folder") / Views("folder", "name filter"); poll OzL18BorderFrames.Status.
    /// </summary>
    public static class OzL18BorderFrames
    {
        const string Tag = "[OzL18BorderFrames]";
        const string M = "OpenZ/V1.5/L18 Surroundings/";
        public const float Day = 0.08f, Night = 0.62f, Dawn = 0.938f;   // Dawn = the light a level starts in (DayNight.timeOfDay at the first frame of levels 17 and 18)
        public static string Status = "idle";
        public static string OutRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/unity_pass_20261112_L18surr/frames"));

        static IEnumerator run; static int waitFrame; static double waitTime;
        static float todStart = -1f;
        static readonly StringBuilder log = new StringBuilder();
        static double Now => EditorApplication.timeSinceStartup;
        static bool Flying => ZodFlyover.Live != null && ZodFlyover.Live.Active;

        [MenuItem(M + "1 RTS border frames (Play, a level)", false, 7650)]
        static void MenuRts() => Rts("menu");
        [MenuItem(M + "2 Camera views (Play, level 18)", false, 7651)]
        static void MenuViews() => Views("menu", "");

        public static string Rts(string sub, string filter = "") => Begin(RtsRun(sub, filter ?? ""));
        public static string Views(string sub, string filter = "") => Begin(ViewsRun(sub, filter ?? ""));

        static string Begin(IEnumerator e)
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning(Tag + " enter Play in a level first"); return "not in Play"; }
            if (run != null) return "a run is still going: " + Status;
            run = e; waitFrame = 0; waitTime = 0; Status = "running"; log.Length = 0; todStart = -1f;
            EditorApplication.update -= Step; EditorApplication.update += Step;
            return "started";
        }

        static void Step()
        {
            if (run == null || !EditorApplication.isPlaying) { if (run != null) Status = "stopped: Play ended"; run = null; EditorApplication.update -= Step; return; }
            if (Time.frameCount < waitFrame || Now < waitTime) return;
            bool more;
            try { more = run.MoveNext(); }
            catch (System.Exception ex) { Debug.LogException(ex); Status = "FAILED: " + ex.Message; more = false; }
            if (!more) { run = null; EditorApplication.update -= Step; return; }
            var c = run.Current;
            if (c is int n) waitFrame = Time.frameCount + n; else if (c is float s) waitTime = Now + s; else waitFrame = Time.frameCount + 1;
        }

        /// <summary>The level is up, its briefing skipped as SPACE does, the flight over, the RTS camera back.</summary>
        static IEnumerator Ready()
        {
            double t0 = Now;
            while ((!ZGame.I || ZGame.I.World == null) && Now - t0 < 90) yield return 5;
            if (!ZGame.I || ZGame.I.World == null) { Status = "FAILED: no level in Play"; yield break; }
            yield return 2.5f;
            var dn = Object.FindFirstObjectByType<DayNight>();
            if (todStart < 0f) todStart = Dawn;                                                        // not the clock of this moment: a second run of one Play session starts later in the day
            for (int i = 0; i < 40; i++)
            {
                bool a = false;
                try { a = ZodBriefing.ProofSkipBriefing() || OzBlueCameo.ProofSkipBriefing(); } catch (System.Exception) { }
                if (a) break;
                if (!ZodBriefing.BriefingActive && !Flying && i > 6) break;
                yield return 0.5f;
            }
            t0 = Now; while (Flying && Now - t0 < 60) yield return 0.5f;
            yield return 2.0f;
        }

        static void Light(float t)
        {
            var dn = Object.FindFirstObjectByType<DayNight>(); if (!dn) return;
            dn.running = false; dn.timeOfDay = t; dn.Apply(t);
        }

        static IEnumerator RtsRun(string sub, string filter)
        {
            var r = Ready(); while (r.MoveNext()) yield return r.Current;
            if (Status.StartsWith("FAILED")) yield break;
            var rts = Object.FindFirstObjectByType<RtsCamera>(); var g = ZGame.I;
            if (!rts || !rts.enabled) { Status = "FAILED: no RTS camera (enabled " + (rts && rts.enabled) + ")"; yield break; }
            string dir = Path.Combine(OutRoot, sub); Directory.CreateDirectory(dir);
            var dn = Object.FindFirstObjectByType<DayNight>(); float tod0 = dn ? dn.timeOfDay : 0f; bool run0 = dn && dn.running;
            float edge0 = rts.edgePx, h0 = rts.height; Vector3 f0 = rts.focus; rts.edgePx = 0f;
            float mw = rts.mapSizeMeters.x, mh = rts.mapSizeMeters.y;
            log.AppendLine($"level {g.level}, map {mw:0} x {mh:0} m, start light {todStart:0.000}, day only now {OzNightRain.DayOnlyNow}, test mode {GameBoot.TestMode}, screen {Screen.width} x {Screen.height}");
            var spots = new (string n, float x, float z)[]
            {
                ("NW", -999f, 999f), ("N", mw * 0.5f, 999f), ("NE", 9999f, 999f), ("W", -999f, -mh * 0.5f), ("E", 9999f, -mh * 0.5f),
                ("SW", -999f, -9999f), ("S", mw * 0.5f, -9999f), ("SE", 9999f, -9999f),
            };
            int shots = 0;
            foreach (var (ln, lt) in new[] { ("day", Day), ("night", Night) })
            {
                if (filter.Length > 0 && !filter.Contains(ln)) continue;
                Light(lt); yield return 3.0f;
                foreach (float h in new[] { 70f, 200f })
                    foreach (var s in spots)
                    {
                        for (int k = 0; k < 6; k++) { rts.focus = new Vector3(s.x, 0f, s.z); rts.SetHeightImmediate(h); yield return 1; }
                        yield return 0.45f;
                        string name = $"rts_{ln}_h{h:000}_{s.n}.png";
                        ScreenCapture.CaptureScreenshot(Path.Combine(dir, name)); shots++;
                        log.AppendLine($"{name}: focus ({rts.focus.x:0.0}, {rts.focus.z:0.0}) camera {rts.transform.position.ToString("F1")} light {(dn ? dn.timeOfDay : -1f):0.000}");
                        yield return 3;
                    }
            }
            rts.edgePx = edge0; rts.focus = f0; rts.SetHeightImmediate(h0);
            if (dn) { dn.timeOfDay = tod0; dn.Apply(tod0); dn.running = run0; }
            File.WriteAllText(Path.Combine(dir, "rts_log.txt"), log.ToString());
            Status = $"DONE rts: {shots} frames in {dir}"; Debug.Log(Tag + " " + Status);
        }

        struct View { public string n; public Vector3 p, l; public float fov; public string light; }
        static View V(string n, float px, float py, float pz, float lx, float ly, float lz, float fov, string light) => new View { n = n, p = new Vector3(px, py, pz), l = new Vector3(lx, ly, lz), fov = fov, light = light };

        /// <summary>The cameras kit level18_surroundings was planned on (its tools/l18_render.py), in Unity metres.</summary>
        static List<View> ViewList()
        {
            var v = new List<View>();
            var brief = new (string n, Vector3 pa, Vector3 la, Vector3 pb, Vector3 lb, float fov)[]
            {
                ("three_bridges_dawn", new Vector3(14, 22, -112), new Vector3(206, 3, -136), new Vector3(34, 18.5f, -113.5f), new Vector3(214, 3, -137), 40),
                ("armies_cross", new Vector3(130, 70, -42), new Vector3(120, 0, -128), new Vector3(158, 66, -54), new Vector3(130, 0, -120), 36),
                ("nearly_there", new Vector3(168, 37, -162), new Vector3(127, 0.5f, -124), new Vector3(163, 36, -158), new Vector3(127, 0.5f, -126), 30),
                ("three_blasts", new Vector3(100, 70, -204), new Vector3(112, 0, -128), new Vector3(136, 64, -192), new Vector3(118, 0, -120), 40),
                ("tanks_fly", new Vector3(130, 44, -172), new Vector3(134, 0, -122), new Vector3(156, 42.4f, -170.4f), new Vector3(158, 0, -120.4f), 38),
                ("aftermath", new Vector3(108, 36, -171), new Vector3(110, 0, -122), new Vector3(126, 34.5f, -167), new Vector3(123, 0, -122), 36),
                ("red_fort", new Vector3(156, 66, -88), new Vector3(127, 3, -146), new Vector3(138, 61, -97), new Vector3(126, 3, -148), 36),
                ("destroyed_bridges", new Vector3(84, 56, -170), new Vector3(88, 0, -118), new Vector3(144, 56, -170), new Vector3(144, 0, -118), 40),
                ("the_gap", new Vector3(118, 110, -234), new Vector3(124, 0, -134), new Vector3(134, 98, -226), new Vector3(128, 0, -134), 40),
                ("crane_south", new Vector3(192, 54, -198), new Vector3(167, 2, -221), new Vector3(182, 52, -196), new Vector3(167, 2, -221), 30),
            };
            foreach (var b in brief)
            {
                v.Add(new View { n = "brief_" + b.n + "_A", p = b.pa, l = b.la, fov = b.fov, light = "dawn" });
                v.Add(new View { n = "brief_" + b.n + "_B", p = b.pb, l = b.lb, fov = b.fov, light = "dawn" });
            }
            foreach (var (tag, px, py, pz, hd) in new[] { ("A", 437f, 276f, -8f, -110f), ("B", 399f, 235f, -52f, -104f) })
            {
                float a = hd * Mathf.Deg2Rad, p = 40f * Mathf.Deg2Rad; var d = new Vector3(Mathf.Sin(a) * Mathf.Cos(p), -Mathf.Sin(p), Mathf.Cos(a) * Mathf.Cos(p));
                var pos = new Vector3(px, py, pz);
                foreach (string li in new[] { "dawn", "day", "night" }) v.Add(new View { n = "brief_rest_of_map_" + tag, p = pos, l = pos + d * 100f, fov = 50f, light = li });
            }
            foreach (var (n, cx, cz, yaw, pitch, dist) in new[] { ("blue_home", 126f, -80f, 0f, 44f, 72f), ("blue_home_fromN", 126f, -80f, 180f, 44f, 72f), ("red_home", 126f, -163f, 10f, 38f, 80f) })
            {
                float a = yaw * Mathf.Deg2Rad, p = pitch * Mathf.Deg2Rad;
                v.Add(new View { n = "brief_" + n + "_A", p = new Vector3(cx - Mathf.Sin(a) * dist * Mathf.Cos(p), 3f + dist * Mathf.Sin(p), cz - Mathf.Cos(a) * dist * Mathf.Cos(p)), l = new Vector3(cx, 3f, cz), fov = 30f, light = "dawn" });
            }
            foreach (var (n, px, pz, lx, lz) in new[] { ("fromS", 128f, -300f, 128f, -100f), ("fromW", -70f, -120f, 150f, -120f), ("fromN", 128f, 60f, 128f, -140f), ("fromSW", -40f, -280f, 150f, -110f), ("fromE", 330f, -120f, 100f, -120f), ("fromNE", 300f, 40f, 110f, -130f) })
                foreach (string li in new[] { "day", "night", "dawn" }) v.Add(V("card_" + n, px, 58f, pz, lx, 0f, lz, 40f, li));
            foreach (var (n, fx, fz, yaw) in new[] { ("N", 128f, -30f, 0f), ("E", 226f, -120f, 90f), ("S", 128f, -210f, 180f), ("W", 30f, -120f, 270f), ("NE", 206f, -40f, 45f), ("SW", 50f, -200f, 225f), ("NW", 50f, -40f, 315f), ("SE", 206f, -200f, 135f) })
            {
                float d = 85f, t = 24f * Mathf.Deg2Rad, a = yaw * Mathf.Deg2Rad;
                foreach (string li in new[] { "day", "night" }) v.Add(V("free_" + n, fx - Mathf.Sin(a) * d * Mathf.Cos(t), d * Mathf.Sin(t), fz - Mathf.Cos(a) * d * Mathf.Cos(t), fx, 0f, fz, 32f, li));
            }
            foreach (var (n, px, pz, ph, lx, lz) in new[] { ("harbour", 214f, -150f, 62f, 310f, -110f), ("harbour_n", 236f, -70f, 50f, 320f, 0f), ("works", 40f, -110f, 60f, -90f, -110f), ("works_far", -20f, -60f, 90f, -200f, -130f),
                                                              ("north", 128f, -50f, 62f, 128f, 90f), ("yard", 128f, -196f, 70f, 128f, -300f), ("yard_e", 200f, -220f, 60f, 260f, -300f), ("moles", 300f, -130f, 40f, 480f, -150f) })
                v.Add(V("detail_" + n, px, ph, pz, lx, 0f, lz, 40f, "day"));
            return v;
        }

        static IEnumerator ViewsRun(string sub, string filter)
        {
            var r = Ready(); while (r.MoveNext()) yield return r.Current;
            if (Status.StartsWith("FAILED")) yield break;
            string dir = Path.Combine(OutRoot, sub); Directory.CreateDirectory(dir);
            var dn = Object.FindFirstObjectByType<DayNight>(); float tod0 = dn ? dn.timeOfDay : 0f; bool run0 = dn && dn.running;
            log.AppendLine($"level {ZGame.I.level}, start light {todStart:0.000}, test mode {GameBoot.TestMode}; " + OzStillCam.Begin());
            yield return 0.6f;
            int shots = 0, bad = 0; var list = ViewList();
            foreach (string li in new[] { "dawn", "day", "night" })
            {
                bool lit = false;
                foreach (var w in list)
                {
                    if (w.light != li) continue;
                    string name = w.n + "_" + li;
                    if (filter.Length > 0) { bool hit = false; foreach (string f in filter.Split(',')) if (f.Length > 0 && name.Contains(f)) hit = true; if (!hit) continue; }
                    if (!lit) { Light(li == "dawn" ? todStart : li == "day" ? Day : Night); lit = true; yield return 3.0f; }
                    string pose = OzStillCam.Pose(w.p, w.l, w.fov);
                    yield return 0.35f;
                    OzStillCam.Shoot(Path.Combine(dir, name + ".png"), 1920, 1080, 8);
                    double t0 = Now; while (!OzStillCam.Status.StartsWith("shot OK") && !OzStillCam.Status.StartsWith("shot FAILED") && Now - t0 < 12) yield return 2;
                    if (OzStillCam.Status.StartsWith("shot OK")) shots++; else bad++;
                    log.AppendLine($"{name}: {pose} -> {OzStillCam.Status}");
                }
            }
            log.AppendLine(OzStillCam.End());
            if (dn) { dn.timeOfDay = tod0; dn.Apply(tod0); dn.running = run0; }
            File.WriteAllText(Path.Combine(dir, "views_log.txt"), log.ToString());
            Status = $"DONE views: {shots} frames, {bad} failed, in {dir}"; Debug.Log(Tag + " " + Status);
        }
    }
}
