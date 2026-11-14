using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.4 camera_response (View/OzCamResponse.cs): the counted checks that the camera answers sooner and looks the same.
    /// Menus under OpenZ/V1.4/Camera response:
    ///  1 Delay Check (Play, a level, about 3 min). Click into the Game view after choosing it and leave the mouse and keyboard alone.
    ///    At 60, 40 and 25 fps (Application.targetFrameRate, vsync off), before (the engine's own frame queue and 1.3's pan window) and
    ///    after (1.4), in turn twice, each action 3 times: key scroll, screen edge, middle drag, wheel zoom, free turn (View/
    ///    OzCamResponseProbe.cs sends the input as events). Per action: frames from the input sample to the frame whose camera moved,
    ///    milliseconds from the input sample to the graphics chip's end of that frame (FrameTimingManager), frames started meanwhile.
    ///     D1 every action staged at every rate and mode (at least 4 of 6 tries)   D2 0 frames from the input to the moved camera
    ///     D3 the frame queue: after = OzCamResponse.Want (the shipped switch; off = the engine's own), before = the engine's own
    ///                                                                                D4 after not slower to the screen than before
    ///        (median ms within 1 ms or 5 %)                                          D5 frames a second after at least 97 % of before
    ///    The 60 fps frames are saved as traces for the Replay Check (R2).
    ///  2 Delay Check, graphics chip loaded (Play, a level, about 1 min): the same at 60 fps with a hidden second camera drawing the view
    ///    at twice the screen's size into a texture nobody sees, so the graphics chip is the slower part (the frame queue only shows
    ///    there). G1 the stage was graphics-bound; G2 after reaches the screen sooner (median ms lower by at least a third of a frame,
    ///    or fewer frames started meanwhile); G3 frames a second after at least 95 % of before. With the queue off (1.4 as shipped,
    ///    OzCamResponse.On false) G1 is info only and G2 is "not later" (median ms within 1 ms or 5 %; info when there are no ms).
    ///  3 Replay Check (no Play): OzCamResponseReplay.Check on hidden cameras (R1-R6: the path at 60 fps before = after within 1 mm,
    ///    the recorded traces, the same camera at 45 / 40 / 30 / 25 fps, no extra lag on slow frames, 1.3's pan clock items).
    ///  4 last report.
    /// Each run writes one line "[OpenZ v1.4 camera response] PASS / FAIL ..." to the console and to checks.txt in
    /// FREE CAMERA, the switches, the camera's place).
    /// </summary>
    public static class OzV14CamResponseCheck
    {
        const string Tag = "[OpenZ v1.4 camera response]";
        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/v1_4/camera_response"));
        public static string Report = "not run";
        static int ok, bad; static readonly StringBuilder fails = new StringBuilder(), lines = new StringBuilder();
        static OzCamResponseProbe probe; static string runName; static bool gpuRun; static float waitFocusUntil, runUntil;

        static void Check(bool pass, string what)
        {
            if (pass) ok++; else { bad++; fails.Append(" | ").Append(what); }
            string l = $"{Tag} {(pass ? "ok  " : "FAIL")} {what}"; lines.AppendLine(l); Debug.Log(l);
        }
        static void Info(string what) { string l = $"{Tag} info {what}"; lines.AppendLine(l); Debug.Log(l); }

        static void Finish(string name, string table)
        {
            Directory.CreateDirectory(Dir);
            Report = $"{Tag} {(bad == 0 ? "PASS" : "FAIL")} {name} {ok}/{ok + bad}{fails}";
            if (bad == 0) Debug.Log(Report); else Debug.LogError(Report);
            File.AppendAllText(Path.Combine(Dir, "checks.txt"), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  " + Report + "\n");
            File.WriteAllText(Path.Combine(Dir, name + ".md"), $"# {Tag} {name}\n\n{Report}\n\n```\n{lines}```\n\n{table}\n");
        }

        // ------------------------------------------------------------------ 1 / 2 the delay check (Play)
        [MenuItem("OpenZ/V1.4/Camera response/1 Delay Check (Play, a level, about 3 min)")]
        public static void Delay() => StartDelay(false);

        [MenuItem("OpenZ/V1.4/Camera response/2 Delay Check, graphics chip loaded (Play, a level, about 1 min)")]
        public static void DelayGpu() => StartDelay(true);

        static void StartDelay(bool gpu)
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning(Tag + " enter Play mode in a level first"); return; }
            if (probe) { Debug.LogWarning(Tag + " a run is still going"); return; }
            var cam = Object.FindFirstObjectByType<RtsCamera>();
            if (!cam || !cam.isActiveAndEnabled || RtsCamera.InputLocked) { Debug.LogError(Tag + " no RTS camera in charge (a level must be running, not its ending or briefing)"); return; }
            ok = bad = 0; fails.Clear(); lines.Clear(); gpuRun = gpu; runName = gpu ? "delay_gpu_loaded" : "delay";
            probe = OzCamResponseProbe.Attach(cam);
            waitFocusUntil = Time.realtimeSinceStartup + 20f;
            Debug.Log(Tag + " click into the Game view now and leave the mouse and keyboard alone until the PASS / FAIL line");
            EditorApplication.update += WaitFocus;
        }

        static void WaitFocus()
        {
            if (!EditorApplication.isPlaying || !probe) { EditorApplication.update -= WaitFocus; probe = null; return; }
            if (!Application.isFocused) { if (Time.realtimeSinceStartup > waitFocusUntil) { EditorApplication.update -= WaitFocus; Object.Destroy(probe.gameObject); probe = null; Debug.LogError(Tag + " FAIL: the Game view never had the focus (click into it within 20 s)"); } return; }
            EditorApplication.update -= WaitFocus;
            probe.StartCoroutine(probe.Run(gpuRun ? new[] { 60 } : new[] { 60, 40, 25 }, 3, gpuRun, 2f));
            runUntil = Time.realtimeSinceStartup + 900f;                                                  // 15 min at most (an exception inside the run stops its coroutine)
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (!EditorApplication.isPlaying || !probe) { EditorApplication.update -= Poll; probe = null; Report = Tag + " FAIL: Play ended during the run"; Debug.LogError(Report); return; }
            if (probe.Busy && Time.realtimeSinceStartup < runUntil) return;
            if (probe.Busy) { EditorApplication.update -= Poll; Object.Destroy(probe.gameObject); probe = null; Report = Tag + " FAIL: the run did not end in 15 min (see the console for an exception)"; Debug.LogError(Report); return; }
            EditorApplication.update -= Poll;
            try { Evaluate(probe); }
            finally { Object.Destroy(probe.gameObject); probe = null; }
        }

        static double Median(IEnumerable<double> v) { var a = v.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList(); return a.Count == 0 ? double.NaN : a.Count % 2 == 1 ? a[a.Count / 2] : 0.5 * (a[a.Count / 2 - 1] + a[a.Count / 2]); }

        static void Evaluate(OzCamResponseProbe p)
        {
            p.Analyse();
            var t = new StringBuilder();
            Info($"frame timing figures: {(p.SameClock ? "on the probe's clock" : "on another clock or missing: milliseconds n/a")}; FrameTimingManager on {FrameTimingManager.IsFeatureEnabled()}; graphics {SystemInfo.graphicsDeviceType}; the engine's own frame queue {OzCamResponse.EngineQueued}");
            var acts = (OzCamResponseProbe.Act[])System.Enum.GetValues(typeof(OzCamResponseProbe.Act));
            var fpsList = p.stages.Select(s => s.fps).Distinct().ToList();
            t.AppendLine("| fps | action | before: frames to move | after | before: ms to the screen (median) | after | before: frames started meanwhile | after | tries ok (before / after) |");
            t.AppendLine("|---|---|---|---|---|---|---|---|---|");
            int staged = 0, stagedBad = 0, zero = 0, zeroBad = 0, slower = 0, msRows = 0;
            foreach (int fps in fpsList)
                foreach (var a in acts)
                {
                    var b = p.reps.Where(r => r.fps == fps && r.act == a && !r.after).ToList();
                    var f = p.reps.Where(r => r.fps == fps && r.act == a && r.after).ToList();
                    bool Good(OzCamResponseProbe.Rep r) => r.note == null && r.sample > 0 && r.moved >= r.sample;
                    int gb = b.Count(Good), gf = f.Count(Good);
                    staged++; if (gb < Mathf.Min(4, b.Count) || gf < Mathf.Min(4, f.Count) || b.Count == 0 || f.Count == 0) { stagedBad++; fails.Append($" | D1 {fps} fps {OzCamResponseProbe.ActNames[(int)a]}: {gb}/{b.Count} before, {gf}/{f.Count} after ({(b.Concat(f).FirstOrDefault(r => r.note != null).note ?? "-")})"); }
                    foreach (var r in b.Concat(f).Where(Good)) { zero++; if (r.moved != r.sample) zeroBad++; }
                    double mb = Median(b.Where(Good).Select(r => r.ms)), mf = Median(f.Where(Good).Select(r => r.ms));
                    double ib = Median(b.Where(Good).Where(r => r.inFlight >= 0).Select(r => (double)r.inFlight)), iff = Median(f.Where(Good).Where(r => r.inFlight >= 0).Select(r => (double)r.inFlight));
                    double db = Median(b.Where(Good).Select(r => (double)(r.moved - r.sample))), df = Median(f.Where(Good).Select(r => (double)(r.moved - r.sample)));
                    if (!double.IsNaN(mb) && !double.IsNaN(mf)) { msRows++; if (mf > mb + System.Math.Max(1.0, 0.05 * mb)) slower++; }
                    t.AppendLine($"| {fps} | {OzCamResponseProbe.ActNames[(int)a]} | {db:0} | {df:0} | {(double.IsNaN(mb) ? "n/a" : mb.ToString("0.0"))} | {(double.IsNaN(mf) ? "n/a" : mf.ToString("0.0"))} | {(double.IsNaN(ib) ? "n/a" : ib.ToString("0"))} | {(double.IsNaN(iff) ? "n/a" : iff.ToString("0"))} | {gb}/{b.Count} / {gf}/{f.Count} |");
                }
            Check(stagedBad == 0, $"D1 every action staged: {staged - stagedBad}/{staged} rate x action rows with at least 4 of 6 tries seen and moved");
            Check(zero > 0 && zeroBad == 0, $"D2 the camera moved in the frame of the input sample: {zero - zeroBad}/{zero} tries (0 frames between them)");
            var after = p.stages.Where(s => s.after).ToList(); var before = p.stages.Where(s => !s.after).ToList();
            Check(after.Count > 0 && after.All(s => s.queued == OzCamResponse.Want) && before.All(s => s.queued == (OzCamResponse.EngineQueued > 0 ? OzCamResponse.EngineQueued : s.queued)),
                $"D3 frames queued: after {string.Join(" / ", after.Select(s => s.queued))} (must be {OzCamResponse.Want}, the shipped switch {(OzCamResponse.On ? "on" : "off")}), before {string.Join(" / ", before.Select(s => s.queued))} (the engine's own {OzCamResponse.EngineQueued})");
            if (msRows > 0) Check(slower == 0, $"D4 after not slower to the screen than before: {msRows - slower}/{msRows} rows within 1 ms or 5 % (or faster)");
            else Info("D4 not judged: no frame timing figures on this machine (milliseconds n/a)");
            t.AppendLine().AppendLine("| fps | mode | frames a second | frames queued |").AppendLine("|---|---|---|---|");
            int slowFps = 0;
            foreach (int fps in fpsList)
            {
                float fb = before.Where(s => s.fps == fps).Select(s => s.measuredFps).DefaultIfEmpty(0f).Average(), fa = after.Where(s => s.fps == fps).Select(s => s.measuredFps).DefaultIfEmpty(0f).Average();
                if (fa < (gpuRun ? 0.95f : 0.97f) * fb) slowFps++;
                t.AppendLine($"| {fps} | before | {fb:0.0} | {before.Where(s => s.fps == fps).Select(s => s.queued).FirstOrDefault()} |").AppendLine($"| {fps} | after | {fa:0.0} | {after.Where(s => s.fps == fps).Select(s => s.queued).FirstOrDefault()} |");
            }
            Check(slowFps == 0, gpuRun ? $"G3 frames a second after at least 95 % of before ({slowFps} rate(s) under)" : $"D5 frames a second after at least 97 % of before at each rate ({slowFps} rate(s) under)");
            if (gpuRun)
            {
                var stTimes = p.timings.Values.Where(x => x.gpuMs > 0).ToList();
                double gpu = Median(stTimes.Select(x => x.gpuMs)), cpu = Median(stTimes.Select(x => x.cpuMs));
                if (OzCamResponse.On) Check(!double.IsNaN(gpu) && gpu > cpu * 0.9, $"G1 the graphics chip was the slower part: median graphics {gpu:0.0} ms, processor {cpu:0.0} ms (raise the load's size in OzCamResponseProbe.Run when not)");
                else Info($"G1 (not judged, the frame queue is off): median graphics {gpu:0.0} ms, processor {cpu:0.0} ms");
                var good = p.reps.Where(r => r.note == null && r.sample > 0 && r.moved >= r.sample).ToList();
                double mb = Median(good.Where(r => !r.after).Select(r => r.ms)), ma = Median(good.Where(r => r.after).Select(r => r.ms));
                double ib = Median(good.Where(r => !r.after && r.inFlight >= 0).Select(r => (double)r.inFlight)), ia = Median(good.Where(r => r.after && r.inFlight >= 0).Select(r => (double)r.inFlight));
                double frame = Median(good.Select(r => (double)r.frameMs));
                if (OzCamResponse.On) Check(!double.IsNaN(mb) && !double.IsNaN(ma) && (ma <= mb - frame / 3.0 || ia < ib), $"G2 sooner on the screen with the graphics chip loaded: median {mb:0.0} -> {ma:0.0} ms (a frame {frame:0.0} ms), frames started meanwhile {ib:0} -> {ia:0}");
                else if (double.IsNaN(mb) || double.IsNaN(ma)) Info($"G2 not judged: the frame queue is off (the engine's own {OzCamResponse.Want}) and there are no frame timing figures");
                else Check(ma <= mb + System.Math.Max(1.0, 0.05 * mb), $"G2 not later on the screen with the graphics chip loaded (the frame queue off, only the pan window differs): median {mb:0.0} -> {ma:0.0} ms, frames started meanwhile {ib:0} -> {ia:0}");
            }
            // the 60 fps traces for the Replay Check
            Directory.CreateDirectory(Dir);
            int n = 0;
            foreach (var s in p.stages.Where(s => s.fps == 60 && !s.gpuLoad))
            {
                var tr = p.TraceOf(s); var sb = new StringBuilder("dt,dirx,diry,dragx,dragy,turnx,turny,wheel\n");
                foreach (var x in tr) sb.AppendLine(string.Join(",", new[] { x.dt, x.dir.x, x.dir.y, x.drag.x, x.drag.y, x.turn.x, x.turn.y, x.wheel }.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
                File.WriteAllText(Path.Combine(Dir, $"trace_60_{(s.after ? "after" : "before")}_{n++}.csv"), sb.ToString());
            }
            t.AppendLine().AppendLine("Notes of the tries that did not count:");
            foreach (var g in p.reps.Where(r => r.note != null).GroupBy(r => r.note)) t.AppendLine($"- {g.Count()} x {g.Key}");
            Info($"{n} trace(s) of the 60 fps stages saved for the Replay Check");
            Finish(runName, t.ToString());
        }

        // ------------------------------------------------------------------ 3 the replay check (no Play)
        [MenuItem("OpenZ/V1.4/Camera response/3 Replay Check (no Play)")]
        public static void Replay()
        {
            if (EditorApplication.isPlaying) { Debug.LogError(Tag + " run it in edit mode (not in Play): the replay owns its cameras"); return; }
            ok = bad = 0; fails.Clear(); lines.Clear();
            var made = new List<GameObject>(); var table = new StringBuilder();
            bool window0 = OzCamResponse.PanWindowOn;
            try
            {
                var rec = new List<OzCamResponseReplay.Recorded>();
                if (Directory.Exists(Dir))
                    foreach (var file in Directory.GetFiles(Dir, "trace_60_*.csv"))
                    {
                        var fr = new List<OzCamResponseReplay.In>();
                        foreach (var line in File.ReadAllLines(file).Skip(1))
                        {
                            var v = line.Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray(); if (v.Length < 8) continue;
                            fr.Add(new OzCamResponseReplay.In { dt = v[0], dir = new Vector2(v[1], v[2]), drag = new Vector2(v[3], v[4]), turn = new Vector2(v[5], v[6]), wheel = v[7] });
                        }
                        if (fr.Count > 10) rec.Add(new OzCamResponseReplay.Recorded { name = Path.GetFileNameWithoutExtension(file), frames = fr });
                    }
                RtsCamera Make()
                {
                    var go = new GameObject("OzV14CamResponseReplay") { hideFlags = HideFlags.HideAndDontSave }; made.Add(go);
                    var cam = go.AddComponent<RtsCamera>();
                    cam.mapSizeMeters = new Vector2(4000f, 4000f); cam.focus = new Vector3(2000f, 0f, -2000f); cam.SetHeightImmediate(70f);
                    return cam;
                }
                var res = OzCamResponseReplay.Check(Make, c => { c.Place(); return c.transform.position; }, on => OzCamResponse.PanWindowOn = on, OzTrackpadScroll.PerNotch(), rec, table);
                foreach (var l in res) Check(l.pass, l.text);
            }
            finally
            {
                OzCamResponse.PanWindowOn = window0;
                foreach (var g in made) if (g) Object.DestroyImmediate(g);
            }
            Finish("replay", table.ToString());
        }

        [MenuItem("OpenZ/V1.4/Camera response/4 last report")]
        public static void Last() => Debug.Log(Report);
    }
}
