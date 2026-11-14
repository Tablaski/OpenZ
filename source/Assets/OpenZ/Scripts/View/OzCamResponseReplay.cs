// OpenZ (GPL-3.0) - version 1.4, camera_response: camera input replayed through RtsCamera's own frame steps, for the counted check
// Editor/OzV14CamResponseCheck.cs and the kit's off-project proof. Never called by the game.
// One frame here is what RtsCamera.Update hands its steps (v1.3 item 35): the free turn or the middle-button drag of the frame's mouse
// travel, the wheel, then FeelStep with the scroll direction (keys or the screen edge) and SHIFT. The frame times and the input scripts
// are made from a seed (no clock, no engine random), so a run gives the same numbers every time.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public static class OzCamResponseReplay
    {
        /// <summary>One frame of input as RtsCamera.Update reads it: frame time (s), scroll direction (keys / screen edge, each axis
        /// -1..1), SHIFT, the middle-button travel (px) as a drag or as a free-look turn, the wheel (in the input's own units).</summary>
        public struct In { public float dt; public Vector2 dir; public bool shift; public Vector2 drag, turn; public float wheel; }
        /// <summary>The camera after the frame: the point it looks at, its height, heading and tilt, and (pose given) where it stands.</summary>
        public struct Out { public Vector3 focus; public float height, yaw, tilt; public Vector3 pos; }

        /// <summary>Runs the frames through the camera's own steps in Update's order; pose (may be null) places the camera and gives its
        /// position (the editor: Place, then the transform).</summary>
        public static List<Out> Run(RtsCamera cam, IList<In> frames, Func<RtsCamera, Vector3> pose)
        {
            var o = new List<Out>(frames.Count);
            for (int i = 0; i < frames.Count; i++)
            {
                var f = frames[i];
                if (f.turn != Vector2.zero) cam.FreeTurnBy(f.turn);
                else if (f.drag != Vector2.zero) cam.MiddleDrag(f.drag);
                cam.WheelInput(f.wheel, f.dt);
                cam.FeelStep(f.dt, f.dir, f.shift);
                o.Add(new Out { focus = cam.focus, height = cam.height, yaw = cam.yaw, tilt = cam.Tilt, pos = pose != null ? pose(cam) : Vector3.zero });
            }
            return o;
        }

        /// <summary>A small fixed random stream (xorshift), the same on every machine.</summary>
        public sealed class Rng
        {
            uint s;
            public Rng(int seed) { s = (uint)seed * 2654435761u + 0x9E3779B9u; if (s == 0) s = 1; }
            public float Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / 16777216f; }
        }

        /// <summary>The rates a check runs (names for Trace).</summary>
        public static readonly string[] Rates = { "60", "60j", "60r", "60h", "45", "40", "40j", "30u", "25", "25j" };
        /// <summary>The rates counted as fast frames (the path must not change at all).</summary>
        public static bool Fast(string rate) => rate == "60" || rate == "60j" || rate == "60r";

        /// <summary>Frame times (s) for `seconds`: "60" even 60 fps; "60j" 60 fps with up to 1.5 ms either way (a frame limiter's own
        /// jitter); "60r" 60 fps with frames of 15.0 / 18.4 ms in an uneven run (rule ticks 1 or 2 a frame); "60h" "60j" with a
        /// 100 ms frame every 2 s; "45", "40", "25" even; "40j", "25j" with 10 % either way; "30u" 28.3 / 38.3 ms in turn.</summary>
        public static List<float> Trace(string rate, float seconds, int seed)
        {
            var r = new Rng(seed); var l = new List<float>(); double t = 0; int k = 0; double nextHitch = 1.0;
            while (t < seconds - 1e-6)
            {
                float dt;
                switch (rate)
                {
                    case "60j": dt = 1f / 60f + (r.Next() * 2f - 1f) * 0.0015f; break;
                    case "60r": dt = r.Next() < 0.6f ? 0.0150f : 0.0184f; break;
                    case "60h":
                        dt = 1f / 60f + (r.Next() * 2f - 1f) * 0.0015f;
                        if (t >= nextHitch) { dt = 0.1f; nextHitch += 2.0; }
                        break;
                    case "45": dt = 1f / 45f; break;
                    case "40": dt = 1f / 40f; break;
                    case "40j": dt = (1f / 40f) * (1f + (r.Next() * 2f - 1f) * 0.1f); break;
                    case "30u": dt = (1f / 30f) * (k % 2 == 0 ? 0.85f : 1.15f); break;
                    case "25": dt = 1f / 25f; break;
                    case "25j": dt = (1f / 25f) * (1f + (r.Next() * 2f - 1f) * 0.1f); break;
                    default: dt = 1f / 60f; break;
                }
                l.Add(dt); t += dt; k++;
            }
            return l;
        }

        /// <summary>The input scripts, each from the first frame: "key" 2 s scroll to the east, then released; "keyshift" the same
        /// with SHIFT from 0.5 s; "edge" 1.5 s at the west screen edge; "drag" the middle button 600 px/s for 1 s; "wheel" 6 notches
        /// out (one each 50 ms, from 70 m to 185 m), then 6 in from 1 s; "turn" the free look 600 px/s across and 100 px/s down for 1 s; "mixed" 20 s of
        /// all of them in turn from the seed (scroll in eight directions, SHIFT, wheel bursts, drags, turns). perNotch: one notch in
        /// the input's units. Give the trace a second or more after the input (the release, the zoom's ease).</summary>
        public static List<In> Script(string name, IList<float> dts, float perNotch, int seed)
        {
            var l = new List<In>(dts.Count); double t = 0, prev = 0;
            var notches = new List<KeyValuePair<double, int>>();
            if (name == "wheel") for (int i = 0; i < 6; i++) { notches.Add(new KeyValuePair<double, int>(0.0001 + i * 0.05, -1)); notches.Add(new KeyValuePair<double, int>(1.0001 + i * 0.05, 1)); }
            var segs = name == "mixed" ? Mixed(seed, notches) : null;
            for (int k = 0; k < dts.Count; k++)
            {
                float dt = dts[k]; prev = t; t += dt;
                var f = new In { dt = dt };
                switch (name)
                {
                    case "key": if (t <= 2.0001) f.dir = Vector2.right; break;
                    case "keyshift": if (t <= 2.0001) { f.dir = Vector2.right; f.shift = t > 0.5; } break;
                    case "edge": if (t <= 1.5001) f.dir = Vector2.left; break;
                    case "drag": if (t <= 1.0001) f.drag = new Vector2(600f * dt, 0f); break;
                    case "turn": if (t <= 1.0001) f.turn = new Vector2(600f * dt, -100f * dt); break;
                    case "mixed":
                        foreach (var s in segs)
                            if (t > s.from && t <= s.to)
                            {
                                f.dir = s.dir; f.shift = s.shift;
                                if (s.drag != Vector2.zero) f.drag = s.drag * dt;
                                if (s.turn != Vector2.zero) f.turn = s.turn * dt;
                            }
                        break;
                }
                int n = 0; foreach (var p in notches) if (p.Key > prev && p.Key <= t) n += p.Value;
                f.wheel = n * perNotch;
                l.Add(f);
            }
            return l;
        }

        // ------------------------------------------------------------------ the counted replay check (editor and off-project proof)
        public static readonly string[] Scripts = { "key", "keyshift", "edge", "drag", "wheel", "turn", "mixed" };
        /// <summary>One check line.</summary>
        public struct Line { public bool pass; public string text; }
        /// <summary>A recorded trace (the Play check's frames) to replay too.</summary>
        public struct Recorded { public string name; public List<In> frames; }

        /// <summary>The longest time 4 frames in a row take in these frames (s).</summary>
        public static float Longest4(IList<In> f)
        {
            float m = 0f;
            for (int i = 3; i < f.Count; i++) m = Mathf.Max(m, f[i].dt + f[i - 1].dt + f[i - 2].dt + f[i - 3].dt);
            return m;
        }

        /// <summary>The largest difference of two runs: metres (the point looked at, the height, the camera's place) and degrees.</summary>
        public static void Diff(List<Out> a, List<Out> b, out float metres, out float degrees, out int firstFrame)
        {
            metres = 0f; degrees = 0f; firstFrame = -1;
            for (int i = 0; i < a.Count && i < b.Count; i++)
            {
                float m = Mathf.Max((a[i].focus - b[i].focus).magnitude, Mathf.Max(Mathf.Abs(a[i].height - b[i].height), (a[i].pos - b[i].pos).magnitude));
                float d = Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(a[i].yaw, b[i].yaw)), Mathf.Abs(a[i].tilt - b[i].tilt));
                if ((m > 1e-6f || d > 1e-6f) && firstFrame < 0) firstFrame = i;
                metres = Mathf.Max(metres, m); degrees = Mathf.Max(degrees, d);
            }
        }

        static float SecondsTo(IList<float> tr, int k) { double t = 0; for (int i = 0; i <= k && i < tr.Count; i++) t += tr[i]; return (float)t; }

        /// <summary>The replay check. make: a fresh camera at the standard view (70 m) in the middle of a map large enough that no
        /// border is reached; pose: places it and gives its position (null = no placing); window: switches the 1.4 pan window
        /// (OzCamResponse.PanWindowOn) on or off; perNotch: one wheel notch in the input's units. Before = window off (1.3), after =
        /// window on (1.4).
        ///  R1 fast frames (60 even, 60 with the limiter's jitter, 60 with rule-tick swings): every script's path before = after
        ///     within 1 mm and 0.0001 deg, frame by frame.
        ///  R2 the recorded traces: the same, where no 4 frames in a row took over the window (else only reported).
        ///  R3 the same camera at any frame rate (after): each script's distance, height or turn at 45 / 40 / uneven 30 / 25 fps
        ///     within 2 % of 60 fps.
        ///  R4 no extra lag when frames are slow: the key scroll at 60 fps with a 100 ms frame every 2 s, at 40 and at 25 fps with
        ///     10 % swings: the most the pan falls behind the frame-time-exact pan (PanAverageFrames 1), after not more than before.
        ///  R5 1.3's pan clock items with the window: frames of 70 / 80 ms in turn give the same distance and even steps; an even
        ///     60 fps steps by the frame time itself.
        ///  R6 the window acts: on the 60 fps trace with 100 ms frames before and after differ (not a dead switch).</summary>
        public static List<Line> Check(Func<RtsCamera> make, Func<RtsCamera, Vector3> pose, Action<bool> window, float perNotch, IList<Recorded> recorded, System.Text.StringBuilder table)
        {
            var lines = new List<Line>();
            void Add(bool ok, string s) { lines.Add(new Line { pass = ok, text = s }); }
            List<Out> Go(IList<In> frames, bool on) { window(on); var c = make(); return Run(c, frames, pose); }
            int pan0 = RtsCamera.PanAverageFrames;
            try
            {
                RtsCamera.PanAverageFrames = 4;
                table?.AppendLine("| trace | script | frames | before = after within | first frame that differs |").AppendLine("|---|---|---|---|---|");
                // R1
                int r1Bad = 0, r1N = 0; float r1Worst = 0f, r1WorstDeg = 0f;
                foreach (var rate in new[] { "60", "60j", "60r" })
                    foreach (var s in Scripts)
                    {
                        var fr = Script(s, Trace(rate, s == "mixed" ? 20f : 3f, 11), perNotch, 7);
                        Diff(Go(fr, false), Go(fr, true), out float m, out float d, out int first);
                        r1N++; if (m > 0.001f || d > 0.0001f) r1Bad++;
                        r1Worst = Mathf.Max(r1Worst, m); r1WorstDeg = Mathf.Max(r1WorstDeg, d);
                        table?.AppendLine($"| {rate} | {s} | {fr.Count} | {m * 1000f:0.000} mm, {d:0.00000} deg | {(first < 0 ? "none" : first.ToString())} |");
                    }
                Add(r1Bad == 0, $"R1 fast frames (60 even / jitter / rule-tick swings), {r1N} runs: the camera path before = after frame by frame, worst {r1Worst * 1000f:0.000} mm and {r1WorstDeg:0.00000} deg (must be <= 1 mm, 0.0001 deg); {r1Bad} off");
                // R2
                int r2N = 0, r2Bad = 0, r2Slow = 0; float r2Worst = 0f;
                if (recorded != null)
                    foreach (var rec in recorded)
                    {
                        float l4 = Longest4(rec.frames);
                        Diff(Go(rec.frames, false), Go(rec.frames, true), out float m, out float d, out int first);
                        table?.AppendLine($"| recorded {rec.name} (longest 4 frames {l4 * 1000f:0.0} ms) | as played | {rec.frames.Count} | {m * 1000f:0.000} mm, {d:0.00000} deg | {(first < 0 ? "none" : first.ToString())} |");
                        if (l4 > 0.09f) { r2Slow++; continue; }
                        r2N++; r2Worst = Mathf.Max(r2Worst, m); if (m > 0.001f || d > 0.0001f) r2Bad++;
                    }
                Add(r2Bad == 0, $"R2 recorded traces: {r2N} with fast frames only, path before = after within {r2Worst * 1000f:0.000} mm (must be <= 1 mm), {r2Bad} off; {r2Slow} with slower frames (only reported){(recorded == null || recorded.Count == 0 ? " (none recorded yet: run the Delay Check first)" : "")}");
                // R3
                table?.AppendLine().AppendLine("| script | 60 | 45 | 40 | 30 uneven | 25 | worst vs 60 |").AppendLine("|---|---|---|---|---|---|---|");
                int r3Bad = 0; float r3Worst = 0f;
                foreach (var s in new[] { "key", "keyshift", "edge", "drag", "wheel", "turn" })
                {
                    var vals = new List<float>();
                    foreach (var rate in new[] { "60", "45", "40", "30u", "25" })
                    {
                        var tr = Trace(rate, 3f, 11);
                        var o = Go(Script(s, tr, perNotch, 7), true); var last = o[o.Count - 1]; var c0 = make();
                        float v;
                        if (s == "wheel")
                        {   // the height the 6 notches out reached (0.95 s, before the notches back in)
                            int k = 0; while (k < o.Count - 1 && SecondsTo(tr, k + 1) <= 0.95f + 1e-4f) k++;
                            v = o[k].height;
                        }
                        else v = s == "turn" ? last.yaw : (last.focus - c0.focus).magnitude;
                        vals.Add(v);
                    }
                    float worst = 0f; for (int i = 1; i < vals.Count; i++) worst = Mathf.Max(worst, Mathf.Abs(vals[i] - vals[0]) / Mathf.Max(1e-6f, Mathf.Abs(vals[0])));
                    r3Worst = Mathf.Max(r3Worst, worst); if (worst > 0.02f) r3Bad++;
                    table?.AppendLine($"| {s} | {vals[0]:0.000} | {vals[1]:0.000} | {vals[2]:0.000} | {vals[3]:0.000} | {vals[4]:0.000} | {worst * 100f:0.00} % |");
                }
                Add(r3Bad == 0, $"R3 the same camera at any frame rate (1.4): distance, height or turn at 45 / 40 / uneven 30 / 25 fps within {r3Worst * 100f:0.00} % of 60 fps (must be <= 2 %); {r3Bad} off");
                // R4
                table?.AppendLine().AppendLine("| trace (key scroll 5 s) | most behind or ahead of the exact pan, before | after |").AppendLine("|---|---|---|");
                int r4Bad = 0; float gain = 0f;
                foreach (var rate in new[] { "60h", "40j", "25j" })
                {
                    var tr = Trace(rate, 6f, 13); var fr = Script("key", tr, perNotch, 7);
                    double tt = 0; for (int i = 0; i < fr.Count; i++) { var x = fr[i]; tt += x.dt; x.dir = tt <= 5.0001 ? Vector2.right : Vector2.zero; fr[i] = x; }
                    RtsCamera.PanAverageFrames = 1; var exact = Go(fr, false); RtsCamera.PanAverageFrames = 4;
                    var b = Go(fr, false); var a = Go(fr, true);
                    float lb = 0f, la = 0f; for (int i = 0; i < exact.Count; i++) { lb = Mathf.Max(lb, Mathf.Abs(b[i].focus.x - exact[i].focus.x)); la = Mathf.Max(la, Mathf.Abs(a[i].focus.x - exact[i].focus.x)); }
                    if (la > lb + 1e-4f) r4Bad++;
                    gain = Mathf.Max(gain, lb - la);
                    table?.AppendLine($"| {rate} | {lb:0.000} m | {la:0.000} m |");
                }
                Add(r4Bad == 0, $"R4 slow or uneven frames: the pan is off the exact pan no more after than before (largest gain {gain:0.000} m); {r4Bad} off");
                // R5
                {
                    window(true);
                    var clk = new OzCamFeel.PanClock(); double sumOld = 0, sumNew = 0; float mnO = 9, mxO = 0, mnN = 9, mxN = 0;
                    for (int i = 0; i < 300; i++)
                    {
                        float dt = i % 2 == 0 ? 0.07f : 0.08f, s = clk.Step(dt, RtsCamera.PanAverageFrames);
                        sumOld += dt; sumNew += s;
                        if (i >= 8) { mnO = Mathf.Min(mnO, dt); mxO = Mathf.Max(mxO, dt); mnN = Mathf.Min(mnN, s); mxN = Mathf.Max(mxN, s); }
                    }
                    var steady = new OzCamFeel.PanClock(); float st = 0f; for (int i = 0; i < 10; i++) st = steady.Step(1f / 60f, RtsCamera.PanAverageFrames);
                    Add(System.Math.Abs(sumOld - sumNew) < 0.08 && (mxN - mnN) < 0.25f * (mxO - mnO) && Mathf.Abs(st - 1f / 60f) < 1e-6f,
                        $"R5 1.3 pan clock items with the window: 70 / 80 ms in turn: distance {sumOld:0.000} / {sumNew:0.000} s, step spread {(mxO - mnO) * 1000f:0.0} -> {(mxN - mnN) * 1000f:0.0} ms; even 60 fps step {st * 1000f:0.0000} ms");
                }
                // R6
                {
                    var fr = Script("key", Trace("60h", 3f, 13), perNotch, 7);
                    Diff(Go(fr, false), Go(fr, true), out float m, out _, out int first);
                    Add(m > 0.001f, $"R6 the window acts on long frames: 60 fps with a 100 ms frame, before and after differ by {m * 1000f:0.0} mm from frame {first} (must be over 1 mm)");
                }
            }
            finally { RtsCamera.PanAverageFrames = pan0; window(true); }
            return lines;
        }

        struct Seg { public double from, to; public Vector2 dir, drag, turn; public bool shift; }
        static List<Seg> Mixed(int seed, List<KeyValuePair<double, int>> notches)
        {
            var r = new Rng(seed); var l = new List<Seg>(); double t = 0.1;
            while (t < 19.0)
            {
                float u = r.Next(); double len = 0.3 + r.Next() * 1.2;
                var s = new Seg { from = t, to = t + len };
                if (u < 0.45) { float a = Mathf.Floor(r.Next() * 8f) * 45f * Mathf.Deg2Rad; s.dir = new Vector2(Mathf.Round(Mathf.Cos(a)), Mathf.Round(Mathf.Sin(a))); s.shift = r.Next() < 0.3f; }
                else if (u < 0.65) { int k = 1 + (int)(r.Next() * 5f), sg = r.Next() < 0.5f ? 1 : -1; for (int i = 0; i < k; i++) notches.Add(new KeyValuePair<double, int>(t + 0.0001 + i * 0.04, sg)); }
                else if (u < 0.85) s.drag = new Vector2((r.Next() * 2f - 1f) * 700f, (r.Next() * 2f - 1f) * 500f);
                else s.turn = new Vector2((r.Next() * 2f - 1f) * 500f, (r.Next() * 2f - 1f) * 120f);
                l.Add(s); t += len + 0.05 + r.Next() * 0.4;
            }
            return l;
        }
    }
}
