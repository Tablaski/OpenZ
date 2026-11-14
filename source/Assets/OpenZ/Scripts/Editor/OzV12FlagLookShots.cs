using System.Collections.Generic;
using System.IO;
using OpenZ.Sim;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// setup of OzV12FlagShots' "current" pictures: the same three flags per level (the tiles those pictures show, from their log),
    /// their owners set to neutral / red / blue with ZWorld.AwardZone where this match differs (as the current red flags were made),
    /// the RTS camera at 70 m on each flag at day (clock 0.08) and night (0.62, the clock frozen), one wide 140 m picture per time;
    /// plus the widest zoom per time and a dusk strip on each flag (clock 0.25 to 0.45, 6 frames). A red / blue flag's close
    /// pictures at night and at dusk wait for the bright moment of its lamp's blink (as the ideas page's picture 05 shows it).
    /// Run(): in Play on a level (menu OpenZ/V1.2/Flag Look Shots). Measure(): at night, the widest zoom over the flags' middle: the
    /// frame time (mean of 120 frames, as OzV12EdgeShots), the main thread and GPU time where the editor gives them, and the render
    /// counters (batches, SetPass calls, triangles) with the looks on and off (FlagView.Looks), three rounds.
    /// Editor only; the owners it sets belong to this Play session (nothing is saved). Pictures and SHOTS_ / MEASURE_ text files:
    /// </summary>
    public static class OzV12FlagLookShots
    {
        const string Tag = "[OzV12FlagLookShots]";
        public static readonly string Out = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/flags_build/shots"));
        static readonly Queue<System.Action> steps = new Queue<System.Action>();
        static double nextAt;
        static int frames, gpuFrames; static float ms, mainMs, gpuMs; static bool measuring;
        static ProfilerRecorder mainRec;
        static readonly FrameTiming[] timing = new FrameTiming[1];
        static readonly List<string> lines = new List<string>();
        // a red / blue flag's picture waits (up to 3 s) for the bright moment of its lamp's blink, as the ideas page's picture shows it
        static float gateSeed = -1f; static double gateUntil;

        /// <summary>FlagView's per-flag phase (NewFlag: seed) and OzFlagLamp.hlsl's flash window (60 to 120 ms into the 1.6 s period).</summary>
        static bool InFlash(float seed)
        {
            float s = Mathf.Repeat(Time.timeSinceLevelLoad / 1.6f + seed * 0.1591549f, 1f) * 1.6f;
            return s > 0.05f && s < 0.12f;
        }
        static float SeedOf(ZObject f) => (f.Id * 0.6180339f) % 1f * 6.2831853f;
        static void Gate(ZObject f) { gateSeed = f != null && f.Owner != Team.Neutral ? SeedOf(f) : -1f; gateUntil = EditorApplication.timeSinceStartup + 3.0; }

        static readonly Dictionary<string, Vector2Int[]> Tiles = new Dictionary<string, Vector2Int[]>
        {
            { "L02_Desert", new[] { new Vector2Int(39, 40), new Vector2Int(64, 42), new Vector2Int(46, 20) } },
            { "L16_Jungle", new[] { new Vector2Int(54, 62), new Vector2Int(54, 83), new Vector2Int(49, 38) } },
            { "L17_CarPark", new[] { new Vector2Int(72, 66), new Vector2Int(13, 108), new Vector2Int(35, 39) } },
        };
        static readonly Team[] Teams = { Team.Neutral, Team.Red, Team.Blue };
        static readonly string[] Names = { "neutral", "red", "blue" };
        public static readonly float[] DuskClock = { 0.25f, 0.29f, 0.33f, 0.37f, 0.41f, 0.45f };

        static ZObject FlagAt(Vector2Int tile)
        {
            ZObject best = null; int bd = int.MaxValue;
            foreach (var o in ZGame.I.World.Flags)
            {
                int dx = o.CenterX / 16 - tile.x, dy = o.CenterY / 16 - tile.y, d = dx * dx + dy * dy;
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        static string Owners(ZObject[] picks)
        {
            var s = "";
            for (int i = 0; i < picks.Length; i++) if (picks[i] != null) s += $"{Names[i]} flag #{picks[i].Id} at {picks[i].CenterX / 16},{picks[i].CenterY / 16} owner {picks[i].Owner}; ";
            return s;
        }

        static void SetOwners(ZObject[] picks)
        {
            for (int i = 0; i < picks.Length; i++)
                if (picks[i] != null && picks[i].Owner != Teams[i]) ZGame.I.World.AwardZone(picks[i], Teams[i]);
        }

        static void Shot(string path, string what)
        {
            ScreenCapture.CaptureScreenshot(path);
            string fv = FlagView.I ? FlagView.I.LooksDiagnostics() : "no FlagView";
            lines.Add($"{Path.GetFileName(path)} | {what} | {Screen.width}x{Screen.height} | {fv}");
        }

        [MenuItem("OpenZ/V1.2/Flag Look Shots (this level, Play)")]
        public static void RunMenu() => Run(true, true);

        public static void Run(bool stills, bool dusk)
        {
            if (!Application.isPlaying || ZGame.I == null || ZGame.I.World == null) { Debug.LogWarning(Tag + " start Play on a level first"); return; }
            var cam = Object.FindFirstObjectByType<RtsCamera>(); var dn = Object.FindFirstObjectByType<DayNight>();
            if (!cam || !dn) { Debug.LogWarning(Tag + " no RtsCamera / DayNight in this scene"); return; }
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!Tiles.TryGetValue(scene, out var tiles)) { Debug.LogWarning(Tag + " no current-picture tiles for " + scene); return; }
            Directory.CreateDirectory(Out);
            var picks = new ZObject[3];
            for (int i = 0; i < 3; i++) picks[i] = FlagAt(tiles[i]);
            lines.Clear(); lines.Add($"{scene} | {Owners(picks)}");
            steps.Clear();
            steps.Enqueue(() => { SetOwners(picks); lines.Add("owners set: " + Owners(picks)); nextAt = EditorApplication.timeSinceStartup + 4.5; });   // capture sequence ~3.5 s
            if (stills)
                foreach (var (time, label) in new[] { (0.08f, "day"), (0.62f, "night") })
                {
                    steps.Enqueue(() => { SetOwners(picks); dn.running = false; NightLights.JumpTo(time); nextAt = EditorApplication.timeSinceStartup + 3.0; });
                    for (int i = 0; i < 3; i++)
                    {
                        var f = picks[i]; if (f == null) continue; string name = Names[i];
                        var at = ZGame.PxToWorld(f.CenterX, f.CenterY);
                        steps.Enqueue(() => { cam.focus = new Vector3(at.x, 0f, at.z); cam.height = 70f; cam.ZoomTo(70f); nextAt = EditorApplication.timeSinceStartup + 2.5; });
                        string p = $"{Out}/{scene}_{name}_{label}.png";
                        if (label == "night") steps.Enqueue(() => { Gate(f); nextAt = 0; });
                        steps.Enqueue(() => { Shot(p, $"{name} flag #{f.Id} owner {f.Owner}, 70 m, clock {dn.timeOfDay:0.000}"); nextAt = EditorApplication.timeSinceStartup + 1.0; });
                    }
                    double cx = 0, cy = 0; int nPick = 0;
                    foreach (var f in picks) if (f != null) { cx += f.CenterX; cy += f.CenterY; nPick++; }
                    if (nPick > 0)
                    {
                        var c = ZGame.PxToWorld(cx / nPick, cy / nPick);
                        steps.Enqueue(() => { cam.focus = new Vector3(c.x, 0f, c.z); cam.height = 140f; cam.ZoomTo(140f); nextAt = EditorApplication.timeSinceStartup + 3.0; });
                        string pw = $"{Out}/{scene}_wide_{label}.png";
                        steps.Enqueue(() => { Shot(pw, $"wide 140 m, clock {dn.timeOfDay:0.000}"); nextAt = EditorApplication.timeSinceStartup + 1.0; });
                        steps.Enqueue(() => { cam.height = cam.maxHeight; cam.ZoomTo(cam.maxHeight); nextAt = EditorApplication.timeSinceStartup + 3.0; });
                        string px = $"{Out}/{scene}_widest_{label}.png";
                        steps.Enqueue(() => { Shot(px, $"widest {cam.maxHeight:0} m, clock {dn.timeOfDay:0.000}"); nextAt = EditorApplication.timeSinceStartup + 1.0; });
                    }
                }
            if (dusk)
                for (int i = 0; i < 3; i++)
                {
                    var f = picks[i]; if (f == null) continue; string name = Names[i];
                    var at = ZGame.PxToWorld(f.CenterX, f.CenterY);
                    steps.Enqueue(() => { SetOwners(picks); dn.running = false; NightLights.JumpTo(DuskClock[0]); cam.focus = new Vector3(at.x, 0f, at.z); cam.height = 70f; cam.ZoomTo(70f); nextAt = EditorApplication.timeSinceStartup + 3.0; });
                    for (int k = 0; k < DuskClock.Length; k++)
                    {
                        float tk = DuskClock[k]; int kk = k;
                        steps.Enqueue(() => { NightLights.JumpTo(tk); nextAt = EditorApplication.timeSinceStartup + 2.0; });
                        string pd = $"{Out}/{scene}_dusk_{name}_{kk}.png";
                        steps.Enqueue(() => { Gate(f); nextAt = 0; });
                        steps.Enqueue(() => { Shot(pd, $"dusk {name} flag owner {f.Owner}, clock {dn.timeOfDay:0.000}, flags' night {FlagView.NightOf(dn.timeOfDay):0.00}"); nextAt = EditorApplication.timeSinceStartup + 1.0; });
                    }
                }
            steps.Enqueue(() =>
            {
                cam.ZoomTo(70f);
                File.WriteAllLines($"{Out}/SHOTS_{scene}.txt", lines);
                Debug.Log($"{Tag} {scene}: done\n" + string.Join("\n", lines));
            });
            nextAt = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        [MenuItem("OpenZ/V1.2/Flag Look Measure (this level, Play)")]
        public static void Measure()
        {
            if (!Application.isPlaying || ZGame.I == null || ZGame.I.World == null) { Debug.LogWarning(Tag + " start Play on a level first"); return; }
            var cam = Object.FindFirstObjectByType<RtsCamera>(); var dn = Object.FindFirstObjectByType<DayNight>();
            if (!cam || !dn) return;
            Directory.CreateDirectory(Out);
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            double sx = 0, sy = 0; int n = 0;
            foreach (var f in ZGame.I.World.Flags) { sx += f.CenterX; sy += f.CenterY; n++; }
            if (n == 0) return;
            var c = ZGame.PxToWorld(sx / n, sy / n);
            lines.Clear();
            steps.Clear();
            steps.Enqueue(() => { dn.running = false; NightLights.JumpTo(0.62f); FlagView.Looks = true; cam.focus = new Vector3(c.x, 0f, c.z); cam.height = cam.maxHeight; cam.ZoomTo(cam.maxHeight); nextAt = EditorApplication.timeSinceStartup + 4.0; });
            steps.Enqueue(() =>
            {
                var vc = Camera.main; int inView = 0;
                if (vc) foreach (var f in ZGame.I.World.Flags)
                    {
                        var v = vc.WorldToViewportPoint(ZGame.I.CenterWorld(f));
                        if (v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f) inView++;
                    }
                lines.Add($"{scene} | night 0.62 | widest {cam.maxHeight:0} m over the flags' middle {c.x:0},{c.z:0} | zone flags in view {inView} of {n} | {Screen.width}x{Screen.height}");
                lines.Add("looks | mean frame ms (120) | main thread ms | GPU ms | batches | setpass | triangles | vertices");
                Shot($"{Out}/{scene}_measure_widest_night.png", "measure view, looks on");
                nextAt = EditorApplication.timeSinceStartup + 1.0;
            });
            for (int round = 0; round < 3; round++)
                foreach (bool on in new[] { true, false })
                {
                    bool look = on;
                    steps.Enqueue(() => { FlagView.Looks = look; nextAt = EditorApplication.timeSinceStartup + 2.0; });
                    steps.Enqueue(() => { Begin(); nextAt = double.MaxValue; });
                    steps.Enqueue(() =>
                    {
                        lines.Add($"{(look ? "ON " : "OFF")} | {ms / Mathf.Max(1, frames):0.00} | {(mainRec.Valid ? (mainMs / Mathf.Max(1, frames)).ToString("0.00") : "n/a")} | " +
                                  $"{(gpuFrames > 0 ? (gpuMs / gpuFrames).ToString("0.00") : "n/a")} | {UnityStats.batches} | {UnityStats.setPassCalls} | {UnityStats.triangles} | {UnityStats.vertices}");
                        nextAt = EditorApplication.timeSinceStartup + 0.5;
                    });
                }
            steps.Enqueue(() =>
            {
                FlagView.Looks = true;
                if (mainRec.Valid) mainRec.Dispose();
                File.WriteAllLines($"{Out}/MEASURE_{scene}.txt", lines);
                Debug.Log($"{Tag} {scene} measure: done\n" + string.Join("\n", lines));
            });
            nextAt = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Begin()
        {
            frames = 0; gpuFrames = 0; ms = mainMs = gpuMs = 0f; measuring = true;
            if (!mainRec.Valid) mainRec = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 4);
        }

        static void Tick()
        {
            if (!Application.isPlaying)
            {
                steps.Clear(); measuring = false; FlagView.Looks = true; EditorApplication.update -= Tick;
                if (mainRec.Valid) mainRec.Dispose();
                return;
            }
            if (measuring)
            {
                frames++; ms += Time.unscaledDeltaTime * 1000f;
                if (mainRec.Valid) mainMs += mainRec.LastValue * 1e-6f;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].gpuFrameTime > 0.0) { gpuMs += (float)timing[0].gpuFrameTime; gpuFrames++; }
                if (frames >= 120) { measuring = false; nextAt = 0; }
                return;
            }
            if (EditorApplication.timeSinceStartup < nextAt) return;
            if (gateSeed >= 0f)
            {
                if (!InFlash(gateSeed) && EditorApplication.timeSinceStartup < gateUntil) return;
                gateSeed = -1f;
            }
            if (steps.Count == 0) { EditorApplication.update -= Tick; return; }
            steps.Dequeue()();
        }
    }
}
