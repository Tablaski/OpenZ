#if !OPENZ_HARNESS
// OpenZ (GPL-3.0) - version 1.3, level 7: the counted Play check of the hot air over the lava (View/OzLavaAir.cs). Editor only, never in a build.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Menu OpenZ/V1.3/Heat haze, in Play on L07, game speed 1, not paused. Reports and frames under the milestones folder, v1_3/heat_haze.
    ///  * 1 Counted (about 130 s): H1 .. H13, every hot air followed frame by frame from its first frame to its last, the camera as it stands (do not move it).
    ///  * 2 Frame cost (about 70 s): six held up against none, two rounds each: GPU and main thread times, the share of the screen the six cover.
    ///  * 3 Frames (about 3 minutes): over the biggest lake at 70 m and 35 m, day and night, three frames 1.5 s apart, and one with none.
    /// Reads OzLavaAir through its read-only probes; the lake rule (H8) is judged by this file's own scan of every spot, not the air's.
    /// </summary>
    public static class OzLavaHazeCheck
    {
        const string Tag = "[HeatHaze]", Root = "OpenZ/V1.3/Heat haze/";
        static string Dir => OzRelease.Milestones("v1_3/heat_haze");

        sealed class Runner : MonoBehaviour { }
        static Runner runner;
        static Runner R()
        {
            if (!runner) { var go = new GameObject("OzLavaHazeCheckRunner") { hideFlags = HideFlags.HideAndDontSave }; runner = go.AddComponent<Runner>(); }
            return runner;
        }

        static int ok, bad; static readonly StringBuilder notes = new StringBuilder();
        static void Begin() { ok = 0; bad = 0; notes.Length = 0; }
        static void Check(bool pass, string what) { if (pass) ok++; else bad++; notes.Append(pass ? "\n  [ok]   " : "\n  [FAIL] ").Append(what); Debug.Log(Tag + (pass ? " ok   " : " FAIL ") + what); }
        static void Info(string what) { notes.Append("\n  [info] ").Append(what); Debug.Log(Tag + " " + what); }
        static void End(string part)
        {
            var text = $"{Tag} {part}: {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad}{notes}";
            if (bad == 0) Debug.Log(text); else Debug.LogError(text);
            try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, part.Replace(' ', '_') + ".txt"), text + "\n"); }
            catch (Exception e) { Debug.LogWarning(Tag + " could not write the report: " + e.Message); }
        }

        static ZGame g; static OzLava lava; static OzLavaAir air; static RtsCamera rts;

        static bool Ready()
        {
            g = ZGame.I; lava = OzLava.I; air = lava ? lava.Air : null;
            if (!Application.isPlaying || !g || g.World == null || !g.World.Map.HasLava) { Debug.LogWarning(Tag + " enter Play on L07 first"); return false; }
            if (air == null) { Debug.LogWarning(Tag + " the lava air is not running (lava effects off?)"); return false; }
            rts = UnityEngine.Object.FindFirstObjectByType<RtsCamera>();
            return true;
        }

        /// <summary>The air's own view test (OzLava.OnScreen), written again here so H8 does not lean on the code it judges.</summary>
        static bool InView(Camera cam, Vector3 p)
        {
            var v = cam.WorldToViewportPoint(p);
            return v.z > 0f && v.z < 160f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        /// <summary>The weight the owner's line asks for: SmoothStep up over the first third, full, SmoothStep down over the last third.</summary>
        static float Wanted(float u) => u < 1f / 3f ? Smooth(u * 3f) : u > 2f / 3f ? Smooth((1f - u) * 3f) : 1f;

        // ------------------------------------------------------------------ 1 counted
        [MenuItem(Root + "1 Counted (Play, L07, about 130 s)")]
        static void CountedRow() { if (Ready()) R().StartCoroutine(Counted()); }

        sealed class Seen { public float start, life, wide, tall, lastAge = -1f; public int lake, frames, alphaOff, turnOff, footOff, strengthOff; public bool sameLakeWithFree, sliverOnly, crowded; }

        static IEnumerator Counted()
        {
            Begin(); g.gameSpeed = 1f; g.paused = false;
            int slots = air.HazeSlots; var now = new Seen[Mathf.Max(slots, OzLavaAir.MaxShimmers)]; var done = new List<Seen>(); var starts = new List<float>();
            int started0 = air.HazeStarted, mostUp = 0, framesSeen = 0, pausedBad = 0; bool pauseDone = false;
            var busy = new HashSet<int>(); var spotsOf = new Dictionary<int, int>(); var seenOf = new Dictionary<int, int>();
            for (int k = 0; k < air.SpotCount; k++) { int l = air.LakeOfSpot(k); spotsOf.TryGetValue(l, out int c0); spotsOf[l] = c0 + 1; }
            float t0 = Time.time, worstAlpha = 0f, worstTurn = 0f, worstStrength = 0f;
            Info($"spots {air.SpotCount}, strength {OzLavaAir.HazeStrength} at {OzLavaAir.HazeRefDistance} m, LIGHTEST {OzLavaFx.Lite}, height {(rts ? rts.height : 0f):F0} m");
            while (Time.time - t0 < 120f)
            {
                yield return new WaitForEndOfFrame();
                var cam = Camera.main; if (!cam || g.paused) continue;
                framesSeen++; int up = 0; busy.Clear();
                for (int i = 0; i < air.HazeSlots; i++) if (air.HazeProbe(i, out _, out _, out _, out _, out int lk, out _, out _, out _)) busy.Add(lk);
                for (int i = 0; i < air.HazeSlots; i++)
                {
                    if (!air.HazeProbe(i, out var foot, out float age, out float life, out var size, out int lake, out float strength, out var turn, out _))
                    {
                        if (now[i] != null) { done.Add(now[i]); now[i] = null; }
                        continue;
                    }
                    up++;
                    var s = now[i];
                    if (s == null || age < s.lastAge)
                    {   // a new one in this slot: is it on a lake another stands on while a free lake is in view? is it clear of the others?
                        if (s != null) done.Add(s);
                        s = now[i] = new Seen { start = Time.time - age, life = life, wide = size.x, tall = size.y, lake = lake };
                        starts.Add(s.start);
                        int others = 0; for (int j = 0; j < air.HazeSlots; j++) if (j != i && air.HazeProbe(j, out var f2, out _, out _, out _, out int l2, out _, out _, out _)) { if (l2 == lake) others++; float dx = f2.x - foot.x, dz = f2.z - foot.z; if (dx * dx + dz * dz < OzLavaAir.HazeApart * OzLavaAir.HazeApart - 0.01f) s.crowded = true; }
                        if (others > 0)
                        {   // a free lake counts as in view when 5 % of its spots are (the air tries 32 spread spots a lake: a sliver at the screen's edge may be missed, that is listed apart)
                            seenOf.Clear();
                            for (int k = 0; k < air.SpotCount; k++) { int l = air.LakeOfSpot(k); if (l >= 0 && !busy.Contains(l) && InView(cam, air.SpotAt(k))) { seenOf.TryGetValue(l, out int c1); seenOf[l] = c1 + 1; } }
                            foreach (var kv in seenOf) { if (kv.Value >= 0.05f * spotsOf[kv.Key]) s.sameLakeWithFree = true; else s.sliverOnly = true; }
                        }
                    }
                    s.lastAge = age; s.frames++;
                    float want = Wanted(age / life), shown = OzLavaAir.Ease(age / life);
                    float da = Mathf.Abs(want - shown); worstAlpha = Mathf.Max(worstAlpha, da); if (da > 0.002f) s.alphaOff++;
                    float dt = Quaternion.Angle(turn, cam.transform.rotation); worstTurn = Mathf.Max(worstTurn, dt); if (dt > 2f) s.turnOff++;                     // the air turns in Update, the camera moves after it (a shake): a frame's lag
                    if (!OzLava.SurfaceAt(foot, out float ly) || Mathf.Abs(foot.y - (lava.lavaY + 0.1f)) > 0.01f) s.footOff++;
                    float d = Vector3.Distance(cam.transform.position, foot), ws = OzLavaAir.HazeStrength * Mathf.Clamp(OzLavaAir.HazeRefDistance / Mathf.Max(d, 1f), OzLavaAir.HazeFarLeast, OzLavaAir.HazeNearMost);
                    float dsn = Mathf.Abs(ws - strength); worstStrength = Mathf.Max(worstStrength, dsn); if (dsn > 0.02f * ws) s.strengthOff++;
                }
                mostUp = Mathf.Max(mostUp, up);
                if (!pauseDone && Time.time - t0 > 100f)
                {   // a pause, once: hidden, and their clocks stand
                    pauseDone = true;
                    float[] ages = new float[air.HazeSlots]; for (int i = 0; i < air.HazeSlots; i++) air.HazeProbe(i, out _, out ages[i], out _, out _, out _, out _, out _, out _);
                    g.paused = true; float p0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - p0 < 1.5f) yield return null;
                    for (int i = 0; i < air.HazeSlots; i++) if (air.HazeProbe(i, out _, out float a2, out _, out _, out _, out _, out _, out bool drawn) && (drawn || Mathf.Abs(a2 - ages[i]) > 1e-4f)) pausedBad++;
                    g.paused = false;
                }
            }
            foreach (var s in now) if (s != null) done.Add(s);
            int n = air.HazeStarted - started0, lifeBad = 0, sizeBad = 0, same = 0, sliver = 0, crowded = 0, aOff = 0, tOff = 0, fOff = 0, sOff = 0; var lakes = new HashSet<int>();
            foreach (var s in done)
            {
                if (s.life < OzLavaAir.HazeLifeMin - 1e-3f || s.life > OzLavaAir.HazeLifeMax + 1e-3f) lifeBad++;
                if (s.wide < 9f - 1e-3f || s.wide > 16f + 1e-3f || s.tall < 5f - 1e-3f || s.tall > 9f + 1e-3f) sizeBad++;
                if (s.sameLakeWithFree) same++; else if (s.sliverOnly) sliver++; if (s.crowded) crowded++; lakes.Add(s.lake);
                aOff += s.alphaOff; tOff += s.turnOff; fOff += s.footOff; sOff += s.strengthOff;
            }
            starts.Sort(); float minGap = 99f, sum = 0f, sum2 = 0f; int gaps = 0, longGaps = 0;
            for (int i = 1; i < starts.Count; i++) { float d = starts[i] - starts[i - 1]; minGap = Mathf.Min(minGap, d); sum += d; sum2 += d * d; gaps++; if (d > OzLavaAir.HazeGapMax + 0.1f) longGaps++; }
            float mean = gaps > 0 ? sum / gaps : 0f, sd = gaps > 1 ? Mathf.Sqrt(Mathf.Max(0f, sum2 / gaps - mean * mean)) : 0f;
            Check(n >= 20, $"H1 hot air started {n} times in 120 s ({done.Count} followed over {framesSeen} frames); lakes used {lakes.Count}");
            Check(lifeBad == 0, $"H2 every life 5 to 9 s: {lifeBad} outside");
            Check(sizeBad == 0, $"H3 every size 9-16 m wide, 5-9 m tall: {sizeBad} outside");
            Check(minGap >= OzLavaAir.HazeGapMin - 0.04f, $"H4 gap between new ones at least 0.8 s: shortest {minGap:F2} s, mean {mean:F2} s, spread {sd:F2} s; {longGaps} gaps over 3 s (all six up, or no free spot in view)");
            Check(sd >= 0.3f, $"H5 no fixed rhythm: the gaps spread {sd:F2} s (at least 0.3)");
            Check(mostUp <= OzLavaAir.MaxShimmers, $"H6 at most 6 at once: {mostUp}");
            Check(aOff == 0, $"H7 eased in and out with SmoothStep over the first and last third, full between: worst {worstAlpha:F4}, {aOff} frames off");
            Check(same == 0, $"H8 never two on one lake while a free lake is in view (this file's own scan of {air.SpotCount} spots): {same} starts broke it; {sliver} more had only a sliver of a free lake (under 5 % of it) in view");
            Check(crowded == 0, $"H9 never within {OzLavaAir.HazeApart} m of another: {crowded}");
            Check(tOff == 0, $"H10 square to the camera on every frame (2 deg for a frame's lag): worst {worstTurn:F2} deg, {tOff} frames off");
            Check(fOff == 0, $"H11 foot on the lava (0.1 m over the surface): {fOff} frames off");
            Check(sOff == 0, $"H12 strength {OzLavaAir.HazeStrength} at {OzLavaAir.HazeRefDistance} m, by distance inside x{OzLavaAir.HazeFarLeast}..x{OzLavaAir.HazeNearMost} (2 %): worst {worstStrength:E1}, {sOff} frames off");
            Check(pauseDone && pausedBad == 0, $"H13 pause: hidden and their clocks stand: {pausedBad} wrong");
            End("counted");
        }

        // ------------------------------------------------------------------ 2 frame cost
        [MenuItem(Root + "2 Frame cost, six against none (Play, L07, about 70 s)")]
        static void CostRow() { if (Ready()) R().StartCoroutine(Cost()); }

        static IEnumerator Cost()
        {
            Begin(); g.gameSpeed = 1f; g.paused = false;
            Info("FrameTimingManager on: " + FrameTimingManager.IsFeatureEnabled() + " (Player Settings > Frame Timing Stats; off = the GPU column reads 0)");
            var timing = new FrameTiming[1]; var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 4);
            float[] gpu = new float[2], cpu = new float[2], dtMs = new float[2]; int[] gn = new int[2], cn = new int[2]; float cover = 0f; int coverN = 0;
            bool shimmerWas = OzLavaAir.ShimmerOn;
            try
            {
                for (int round = 0; round < 2; round++)
                    for (int mode = 0; mode < 2; mode++)
                    {
                        OzLavaAir.ShimmerOn = mode == 1; OzLavaAir.NoGapForCheck = mode == 1;
                        float w0 = Time.time;
                        while (Time.time - w0 < 12f && (mode == 1 ? air.ShimmersUp < OzLavaAir.MaxShimmers : air.ShimmersUp > 0)) yield return null;
                        if (mode == 1 && air.ShimmersUp < OzLavaAir.MaxShimmers) Info($"round {round + 1}: only {air.ShimmersUp} up after 12 s (lakes in view too few for six 8 m apart)");
                        if (mode == 0 && air.ShimmersUp > 0) Info($"round {round + 1}: {air.ShimmersUp} still easing out");
                        for (int f = 0; f < 300; f++)
                        {
                            yield return new WaitForEndOfFrame();
                            dtMs[mode] += Time.unscaledDeltaTime * 1000f; cn[mode]++;
                            if (main.Valid) cpu[mode] += main.LastValue * 1e-6f;
                            FrameTimingManager.CaptureFrameTimings();
                            if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].gpuFrameTime > 0.0) { gpu[mode] += (float)timing[0].gpuFrameTime; gn[mode]++; }
                            if (mode == 1 && (f % 30) == 0) { cover += Cover(); coverN++; }
                        }
                    }
            }
            finally { OzLavaAir.ShimmerOn = shimmerWas; OzLavaAir.NoGapForCheck = false; if (main.Valid) main.Dispose(); }
            string Row(int m) => $"frame {dtMs[m] / Mathf.Max(1, cn[m]):F2} ms, main thread {cpu[m] / Mathf.Max(1, cn[m]):F2} ms, GPU {(gn[m] > 0 ? (gpu[m] / gn[m]).ToString("F2") + " ms" : "n/a")}";
            Info("none: " + Row(0));
            Info("six:  " + Row(1));
            Info($"the six cover {(coverN > 0 ? cover / coverN * 100f : 0f):F1} % of the screen (sum of their quads, overlaps counted twice), height {(rts ? rts.height : 0f):F0} m, {Screen.width}x{Screen.height}");
            Check(cn[0] >= 600 && cn[1] >= 600, $"measured {cn[0]} frames with none, {cn[1]} with six (the numbers go to RESULTS, the 30 fps cap hides most of the frame column: read the GPU and main thread)");
            End("frame cost");
        }

        /// <summary>The share of the screen the live quads cover now: each quad's four corners on the screen, the polygon's area.</summary>
        static float Cover()
        {
            var cam = Camera.main; if (!cam) return 0f;
            float sum = 0f; var c = new Vector3[4];
            for (int i = 0; i < air.HazeSlots; i++)
            {
                if (!air.HazeProbe(i, out var foot, out _, out _, out var size, out _, out _, out var turn, out _)) continue;
                Vector3 r = turn * Vector3.right * size.x, u = turn * Vector3.up * size.y;
                c[0] = foot - r * 0.5f; c[1] = foot + r * 0.5f; c[2] = foot + r * 0.5f + u; c[3] = foot - r * 0.5f + u;
                float a = 0f;
                for (int k = 0; k < 4; k++) { var p = cam.WorldToViewportPoint(c[k]); var q = cam.WorldToViewportPoint(c[(k + 1) & 3]); a += p.x * q.y - q.x * p.y; }
                sum += Mathf.Abs(a) * 0.5f;
            }
            return sum;
        }

        // ------------------------------------------------------------------ 3 frames
        [MenuItem(Root + "3 Frames, 70 m and 35 m, day and night (Play, L07, about 3 minutes)")]
        static void FramesRow() { if (Ready()) R().StartCoroutine(Frames()); }

        static void Shot(string name)
        {
            Directory.CreateDirectory(Dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, name + ".png"));
            Info("frame " + name);
        }

        static IEnumerator Frames()
        {
            Begin(); g.gameSpeed = 1f; g.paused = false;
            var dn = UnityEngine.Object.FindFirstObjectByType<DayNight>(); float tod = dn ? dn.timeOfDay : 0.08f; bool running = dn && dn.running;
            if (dn) dn.running = false;
            float h0 = rts ? rts.height : 70f; var f0 = rts ? rts.focus : Vector3.zero;
            int big = 0, most = -1; for (int id = 0; id < 6; id++) if (lava.FieldTiles(id) > most) { most = lava.FieldTiles(id); big = id; }
            if (rts) { var c = lava.FieldCentre(big); rts.focus = new Vector3(c.x, 0f, c.z); rts.Place(); }
            bool shimmerWas = OzLavaAir.ShimmerOn;
            try
            {
                foreach (var (night, dayName) in new[] { (0.08f, "day"), (0.62f, "night") })
                {
                    NightLights.JumpTo(night);
                    foreach (var h in new[] { 70f, 35f })
                    {
                        if (rts) rts.SetHeightImmediate(h);
                        string n = $"L07_haze_{dayName}_{h:F0}m";
                        OzLavaAir.ShimmerOn = false; OzLavaAir.NoGapForCheck = false;
                        float w0 = Time.time; while (Time.time - w0 < 10f && air.ShimmersUp > 0) yield return null;
                        yield return new WaitForSeconds(0.5f); Shot(n + "_none");
                        OzLavaAir.ShimmerOn = true; OzLavaAir.NoGapForCheck = true;
                        yield return new WaitForSeconds(4f);                                     // the first ones past their ease-in
                        OzLavaAir.NoGapForCheck = false;
                        for (int k = 0; k < 3; k++) { Shot($"{n}_{k + 1}_up{air.ShimmersUp}"); yield return new WaitForSeconds(1.5f); }
                    }
                }
            }
            finally
            {
                OzLavaAir.ShimmerOn = shimmerWas; OzLavaAir.NoGapForCheck = false;
                if (dn) { dn.timeOfDay = tod; dn.Apply(tod); dn.running = running; }
                if (rts) { rts.focus = f0; rts.SetHeightImmediate(h0); }
            }
            Check(true, "frames written: look at every one. Ground, rims and units behind the hot air waver; nothing smears, no quad edge, no halo; the _none frame beside each set");
            End("frames");
        }
    }
}
#endif
