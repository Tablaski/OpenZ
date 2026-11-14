// OpenZ (GPL-3.0) - version 1.4: the checks and the owner's clips of the FOG OF WAR radar circle (View/OzFogOfWar.Radar.cs, View/OzFowRadar.cs).
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4 FOG OF WAR, the radar circle: counted checks (lines in the console and in Logs/ next to Assets) and the owner's clips.
    /// <list type="bullet">
    /// <item>Ease Count (headless, no Play): Editor/OzV14FogRadarCount.cs, rows H1 - H13, H16 (H15 information) on L02's two real radar zones,
    /// and here H14 (the processor's model of the cloud's soft edge field against the shader's own field), Logs/oz_v14_fog_radar_headless.txt.</item>
    /// <item>Opening Count (in Play, L02 as a human): the fog switched on for this match (as the testing panel's FOG ON), the radar's ground
    /// covered (COVER AGAIN while it is not ours), its zone given to the player through the game's own capture rule (ZWorld.AwardZone, as the
    /// radar line proof does: the flag and the zone's buildings become his with the radar, and the stamp round opens their sights, the normal
    /// reveal of a capture); every frame: the code's radius, the circle alone (OzV14FogRadarCount.Circle: a map of its own that gets the
    /// circle's stamps and the game's reveal steps, from its start disc: its explored edge and its edge as the cloud draws it, the bars), the
    /// game's map (the zone's own reveal and the circle, as on screen: information, and the end state); then the radar lost, regained and
    /// COVER AGAIN; rows R0 - R12 and the frame table in Logs/oz_v14_fog_radar.txt. The match keeps the radar zone and the opened ground:
    /// start the level again after it.</item>
    /// <item>Owner Clips (in Play, L02): two frame-locked 30 fps clips with the game's sound (Unity's MediaEncoder, AudioRenderer) of the opening
    /// at the normal RTS camera (70 m, the level start height, and 110 m), the HUD as in a match, 6 stills each, into
    /// take records, the fog's clock (OzFogOfWar.FogStep: the growth and the per-cell reveal) steps 1/30 s a frame; the rows prove it.</item>
    /// </list>
    /// </summary>
    public static class OzV14FogRadarCheck
    {
        const string Tag = "[OpenZ V1.4 FogRadar]";
        const string LogFile = "oz_v14_fog_radar.txt", HeadlessFile = "oz_v14_fog_radar_headless.txt", ClipFile = "oz_v14_fog_radar_clip.txt";
        /// <summary>The processor's field model against the GPU's field: the largest difference of a texel allowed (R16 holds 1 / 65535).</summary>
        const double FieldDiffBar = 0.003;
        static readonly List<string> lines = new List<string>();
        static int pass, total;
        static bool running; static MonoBehaviour host;

        static void Begin(string what) { lines.Clear(); pass = total = 0; Say($"{Tag} {what}"); }
        static void Say(string s) { lines.Add(s); Debug.Log(s); }
        static void Row(string id, bool ok, string text) { total++; if (ok) pass++; Say($"  {(ok ? "PASS" : "FAIL")} {id} {text}"); }
        static void End(string file)
        {
            Say($"{Tag} {(pass == total ? "PASS" : "FAIL")} {pass}/{total}");
            try
            {
                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, file), string.Join("\n", lines) + "\n");
            }
            catch (System.Exception e) { Debug.LogWarning($"{Tag} could not write the log file: {e.Message}"); }
        }

        /// <summary>The cloud's edge of a style on a planet for the field model (View/OzFogOfWar.Looks.cs, the fog's round 4: its four sheets'
        /// ramps D0 .. D1, AMax (the edge mist's times the planet's MistMul, as ApplyLook), Thin; the deck is the third sheet, the shader's k == 2;
        /// OzFogOfWar.EdgeK).</summary>
        public static OzV14FogRadarCount.Edge EdgeOf(int style, int planet)
        {
            var k = OzFogOfWar.Looks[style]; var c = OzFogOfWar.Palettes[planet];
            var sh = new OzV14FogRadarCount.Edge.Sheet[k.L.Length];
            for (int i = 0; i < sh.Length; i++)
            {
                var l = k.L[i];
                sh[i] = new OzV14FogRadarCount.Edge.Sheet(l.D0, l.D1, Mathf.Clamp01(l.AMax * (l.IsMist ? c.MistMul : 1f)), l.Thin, i == 2);
            }
            return new OzV14FogRadarCount.Edge($"{k.Name} ({OzFogOfWar.PlanetNames[planet]})", OzFogOfWar.EdgeK, sh);
        }

        /// <summary>The edge of the style and the planet on screen (the editor without Play: the OPTIONS style, DESERT, as L02).</summary>
        static OzV14FogRadarCount.Edge EdgeNow() => EdgeOf(OzFogOfWar.LookIndex, OzFogOfWar.PlanetIndex);

        static OzV14FogRadarCount.FieldMeter NewField(OzFowMask m, ZObject r) =>
            new OzV14FogRadarCount.FieldMeter(m.W, m.H, r.CenterX, r.CenterY, EdgeNow());

        /// <summary>The largest difference between the processor's field model and a field read back from the GPU (fw texels across, its
        /// row j and column i the texel l = j - Pad, k = i - Pad of the model), over the model's window.</summary>
        static double FieldDiff(OzV14FogRadarCount.FieldMeter f, float[] gpu, int fw, int fh, out int texels)
        {
            double worst = 0; texels = 0; int pad = OzV14FogRadarCount.FieldMeter.Pad;
            for (int l = f.L0; l <= f.L1; l++)
                for (int k = f.K0; k <= f.K1; k++)
                {
                    int i = k + pad, j = l + pad; if (i < 0 || j < 0 || i >= fw || j >= fh) continue;
                    double c = f.FieldAt(k, l); if (double.IsNaN(c)) continue;
                    worst = System.Math.Max(worst, System.Math.Abs(c - gpu[j * fw + i])); texels++;
                }
            return worst;
        }

        // ================================================================ headless
        [MenuItem("OpenZ/V1.4/Fog Radar: Ease Count (headless)")]
        public static void EaseCount()
        {
            Begin("Ease Count (headless): the radar circle's growth, frame by frame as the game runs it, on L02's two radar zones");
            OzV14FogRadarCount.Run(Row, s => Say("  " + s), OzFogOfWar.RevealSeconds, OzFogOfWar.StampEvery, EdgeNow());

            // H14 the processor's model of the soft edge field = the shader's own field (OzFogOfWar.FieldOf: the material's three field passes, read back)
            {
                int W = OzV14FogRadarCount.MapW, H = OzV14FogRadarCount.MapH, cx = W / 2 + 3, cy = H / 2 - 5;
                var cases = new List<(string name, OzFowMask m, int x, int y)>();
                var a = new OzFowMask(W, H); a.StampFractional(cx, cy, OzV14FogRadarCount.RadarSightPx); a.ShowAll(); cases.Add(("the 20 m start disc", a, cx, cy));
                var b = new OzFowMask(W, H); var rb = new OzFowRadar();
                for (int i = 0; i < 70; i++) { rb.Begin(1f / 60f); rb.See(b, 1, cx, cy, OzV14FogRadarCount.RadarSightPx, false); rb.End(); b.Ease(6); }
                cases.Add(("an opening 70 frames in (shown still easing)", b, cx, cy));
                var z = OzV14FogRadarCount.L02Zone2; var d2 = new OzFowMask(z.MapW, z.MapH); OzV14FogRadarCount.StampZone(d2, z); d2.StampFractional(z.Cx, z.Cy, 300f);
                for (int i = 0; i < 20; i++) d2.Ease(6);
                cases.Add(("L02 zone 2: its flag, factory and a 37.5 m circle, easing (the map's top edge 39 m away)", d2, z.Cx, z.Cy));
                var c = new OzFowMask(W, H); c.StampFractional(60, 90, OzFowRadar.CirclePx); c.ShowAll(); cases.Add(("a 48 m circle at the map's corner (the closing outside)", c, 60, 90));
                var sb = new StringBuilder(); bool ok = true; int n = 0;
                foreach (var cs in cases)
                {
                    var f = new OzV14FogRadarCount.FieldMeter(cs.m.W, cs.m.H, cs.x, cs.y, EdgeNow()); f.Sample(cs.m.Shown);
                    var gpu = OzFogOfWar.FieldOf(cs.m, out int fw, out int fh);
                    if (gpu == null) { ok = false; sb.Append($"{cs.name}: no field from the shader ({OzFogOfWar.MatWhy}); "); continue; }
                    double d = FieldDiff(f, gpu, fw, fh, out int tx); n++;
                    ok &= d <= FieldDiffBar && tx > 1000;
                    sb.Append($"{cs.name}: largest difference {d:0.00000} over {tx} texels; ");
                }
                Row("H14", ok && n == cases.Count, $"the processor's model of the cloud's soft edge field (Editor/OzV14FogRadarCount.cs FieldMeter) = the shader's own field (OzFogOfWar.FieldOf, GPU): {sb}(bar {FieldDiffBar})");
            }
            End(HeadlessFile);
        }

        // ================================================================ Play
        [MenuItem("OpenZ/V1.4/Fog Radar: Opening Count (in Play, L02 as a human)")]
        public static void OpeningCount() => Launch(CountRoutine());

        [MenuItem("OpenZ/V1.4/Fog Radar: Owner Clips (in Play, L02, about 40 s)")]
        public static void OwnerClips() => Launch(ClipRoutine());

        static void Launch(IEnumerator it)
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning($"{Tag} press Play, start L02 as a human, then run this again"); return; }
            var g = ZGame.I;
            if (!g) { Debug.LogWarning($"{Tag} no level running: start L02 as a human"); return; }
            if (running && host && host == g) { Debug.LogWarning($"{Tag} a check is already running"); return; }
            Time.captureFramerate = 0;                                                  // a clip stopped half way leaves nothing behind
            running = true; host = g;
            g.StartCoroutine(Guard(it));                                                // a coroutine on the level's ZGame: frames, WaitForEndOfFrame
        }

        static IEnumerator Guard(IEnumerator it)
        {
            try
            {
                while (true)
                {
                    bool more;
                    try { more = it.MoveNext(); }
                    catch (System.Exception e) { Debug.LogError($"{Tag} the check stopped: {e}"); more = false; }
                    if (!more) break;
                    yield return it.Current;
                }
            }
            finally { running = false; Time.captureFramerate = 0; }
        }

        static IEnumerator Frames(int n) { int f = Time.frameCount + n; while (Time.frameCount < f) yield return null; }
        static IEnumerator Seconds(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }

        static ZObject FlagOf(ZWorld W, ZObject radar) { foreach (var f in W.Flags) foreach (var b in f.LinkedObjects) if (b == radar) return f; return null; }

        static void Cam(ZGame g, ZObject o, float height)
        {
            var rts = Object.FindFirstObjectByType<RtsCamera>(); if (!rts || o == null) return;
            rts.FreeLookOff(); var p = g.CenterWorld(o); rts.focus = new Vector3(p.x, 0f, p.z); rts.SetHeightImmediate(height);
        }

        sealed class Setup
        {
            public ZGame g; public ZWorld W; public OzFogOfWar f; public Team me, foe; public ZObject radar, flag; public int sight; public bool wasWanted; public string why, zone;
        }

        /// <summary>The fog on for this match, the player's camera, a standing radar with a flag (one not ours first).</summary>
        static IEnumerator Prepare(Setup s)
        {
            s.g = ZGame.I; s.f = OzFogOfWar.I;
            if (!s.g || s.g.World == null || !s.f) { s.why = "no level running (ZGame / OzFogOfWar missing): start L02 as a human"; yield break; }
            s.W = s.g.World; s.me = s.g.player; s.foe = s.me == Team.Red ? Team.Blue : Team.Red; s.wasWanted = OzFogOfWar.Wanted;
            if (!s.f.SetForced(true, out var refused)) { s.why = $"the fog could not be switched on: {refused} (a level as a human, not the Unit editor, 'Zod exact' off)"; yield break; }
            float t0 = Time.realtimeSinceStartup;
            while (s.f.GateWhy() != null && Time.realtimeSinceStartup - t0 < 90f) yield return null;
            if (s.f.GateWhy() != null) { s.why = $"still {s.f.GateWhy()} after 90 s (skip the briefing with SPACE and run again)"; yield break; }
            yield return Seconds(OzFogOfWar.FadeInSeconds + 0.3f);
            foreach (var o in s.W.Objects)
            {
                if (o == null || o.Kind != ZKind.Building || o.Type != "radar" || o.Destroyed || o.KillAt >= 0) continue;
                var fl = FlagOf(s.W, o); if (fl == null) continue;
                if (s.radar == null || (s.radar.Owner == s.me && o.Owner != s.me)) { s.radar = o; s.flag = fl; }
            }
            if (s.radar == null) { s.why = "no standing radar with a flag on this level (run it on L02)"; yield break; }
            int agro = s.W.S != null ? s.W.S.agro_distance : 40;
            s.sight = OzFogOfWar.SightPx(s.radar, agro);
            var zb = new StringBuilder($"its flag #{s.flag.Id} {Dist(s.flag, s.radar):0.0} m away (sight {OzFogOfWar.SightPx(s.flag, agro) / 8.0:0} m)");
            foreach (var b in s.flag.LinkedObjects) if (b != s.radar) zb.Append($", its {b.Type} #{b.Id} {Dist(b, s.radar):0.0} m away (sight {OzFogOfWar.SightPx(b, agro) / 8.0:0} m)");
            s.zone = zb.ToString();
        }

        static double Dist(ZObject a, ZObject b) { double dx = a.CenterX - b.CenterX, dy = a.CenterY - b.CenterY; return System.Math.Sqrt(dx * dx + dy * dy) / 8.0; }

        /// <summary>One frame of a recording; the first is the frame before the radar's first round (Age -1, no circle). Edge, Contour,
        /// Visible, CoverStep: the circle alone (from its start disc; -1 when no opening started); Map...: the game's map.</summary>
        sealed class Frame { public float T, Dt, Age; public double CodeM, EdgeM = -1, ContourM = -1, VisibleM = -1, CoverStep, MapEdgeM, MapContourM, MapVisibleM, MapCoverStep, RadarMs; public int Field, Mini, AreaM2; }

        sealed class Recording
        {
            public readonly List<Frame> F = new List<Frame>(512);
            public int Down, Started0, AlreadyOpen0; public double RadarMax, FromM = -1, EdgeAtStartM = -1, GpuDiff = -1; public int GpuTexels; public float GpuAge = -1; public string GpuWhy;
            public double[] Cost; public Team OwnerAfter;
        }

        /// <summary>Cells within 52 m of the radar open on the explored map (m2).</summary>
        static int Area(OzFowMask m, ZObject r)
        {
            int cx = r.CenterX / OzFowMask.CellPx, cy = r.CenterY / OzFowMask.CellPx, n = 0;
            for (int y = System.Math.Max(0, cy - 52); y <= System.Math.Min(m.H - 1, cy + 52); y++)
                for (int x = System.Math.Max(0, cx - 52); x <= System.Math.Min(m.W - 1, cx + 52); x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= 52 * 52 && m.Target[y * m.W + x] >= OzFowMask.Revealed) n++;
            return n;
        }

        static void GpuCompare(Recording rec, AsyncGPUReadbackRequest req, OzV14FogRadarCount.FieldMeter model, int fw, int fh)
        {
            if (req.hasError) { rec.GpuWhy = "the read-back failed"; return; }
            var data = req.GetData<float>().ToArray(); rec.GpuDiff = FieldDiff(model, data, fw, fh, out rec.GpuTexels);
        }

        /// <summary>Runs <paramref name="act"/>, makes the game map's meters from the map as it is then (before the radar's first round: for a
        /// capture the map before it, after COVER AGAIN the covered map) and records it as the first frame, waits for the radar's round, makes
        /// the circle alone (<see cref="OzV14FogRadarCount.Circle"/>) from the circle's start radius, then records every frame for
        /// <paramref name="seconds"/> (frame time, Time.unscaledTime): after the game's Update of that frame the circle alone gets the game's
        /// circle radius and the game's reveal step. gpuAtAge: once the circle is that old, the game's own field (OzFogOfWar.FieldTexture) is
        /// asked for without waiting (no frame of the opening is held up) and compared with the model of that frame's map when it arrives.</summary>
        static IEnumerator Record(Setup s, Recording rec, System.Action act, float seconds, float gpuAtAge = -1f)
        {
            var mask = OzFogOfWar.Mask; int id = s.radar.Id;
            var prev = (byte[])mask.Target.Clone();
            int field0 = OzFogOfWar.FieldRuns, mini0 = OzFogOfWar.MinimapPaints;
            rec.Started0 = OzFogOfWar.Radar != null ? OzFogOfWar.Radar.Started : 0; rec.AlreadyOpen0 = OzFogOfWar.Radar != null ? OzFogOfWar.Radar.AlreadyOpen : 0;
            act();
            var meter = new OzV14FogRadarCount.EdgeMeter(mask, s.radar.CenterX, s.radar.CenterY, 52 * 8);
            var mfield = NewField(mask, s.radar); mfield.Sample(mask.Shown);
            var pre = new Frame { Age = -1f, CodeM = -1, MapEdgeM = meter.RadiusM(mask.Target), MapContourM = mfield.ContourM, MapVisibleM = mfield.VisibleM, AreaM2 = Area(mask, s.radar) };
            rec.F.Add(pre);
            System.Buffer.BlockCopy(mask.Target, 0, prev, 0, prev.Length);
            OzFogOfWar.RadarCostReset(); OzFogOfWar.CostReset();
            int guard = 0;
            while (OzFogOfWar.RadarRadiusPx(id) < 0f && guard++ < 60) yield return null;  // the fog's next Update: the radar's round
            rec.FromM = OzFogOfWar.RadarFromPx(id) / 8.0; rec.EdgeAtStartM = OzFogOfWar.RadarEdgePx(id) / 8.0;
            OzV14FogRadarCount.Circle circle = null;
            if (rec.FromM > 0 && OzFogOfWar.RadarRadiusPx(id) < OzFowRadar.CirclePx)
            {
                circle = new OzV14FogRadarCount.Circle(mask.W, mask.H, s.radar.CenterX, s.radar.CenterY, (float)(rec.FromM * 8.0), EdgeNow());
                pre.EdgeM = circle.EdgeM; pre.ContourM = circle.Field.ContourM; pre.VisibleM = circle.Field.VisibleM;
            }
            float t0 = Time.unscaledTime;
            var req = default(AsyncGPUReadbackRequest); OzV14FogRadarCount.FieldMeter gpuModel = null; bool asked = false; int fw = 0, fh = 0;
            for (int k = 0; ; k++)
            {
                float code = OzFogOfWar.RadarRadiusPx(id);
                circle?.Step(code, OzV14FogRadarCount.RevealStep(OzFogOfWar.FogStep, OzFogOfWar.RevealSeconds));   // the same stamp and reveal step as the game this frame
                mfield.Sample(mask.Shown);
                var fr = new Frame
                {
                    T = Time.unscaledTime - t0, Dt = Time.unscaledDeltaTime, Age = OzFogOfWar.RadarAge(id), CodeM = code / 8.0,
                    RadarMs = OzFogOfWar.RadarMsLast, Field = OzFogOfWar.FieldRuns - field0, Mini = OzFogOfWar.MinimapPaints - mini0, AreaM2 = Area(mask, s.radar),
                    MapEdgeM = meter.RadiusM(mask.Target), MapContourM = mfield.ContourM, MapVisibleM = mfield.VisibleM, MapCoverStep = mfield.CoverStep
                };
                if (circle != null) { fr.EdgeM = circle.EdgeM; fr.ContourM = circle.Field.ContourM; fr.VisibleM = circle.Field.VisibleM; fr.CoverStep = circle.Field.CoverStep; }
                if (k == 0) { pre.T = -fr.Dt; pre.Dt = fr.Dt; }
                for (int i = 0; i < prev.Length; i++) if (mask.Target[i] < prev[i]) rec.Down++;
                System.Buffer.BlockCopy(mask.Target, 0, prev, 0, prev.Length);
                rec.F.Add(fr);
                if (gpuAtAge >= 0f && !asked && fr.Age >= gpuAtAge)
                {
                    asked = true; rec.GpuAge = fr.Age;
                    var rt = OzFogOfWar.FieldTexture;
                    if (!rt) rec.GpuWhy = "no field texture (the fog's material is missing)";
                    else { req = AsyncGPUReadback.Request(rt, 0, TextureFormat.RFloat); fw = rt.width; fh = rt.height; gpuModel = NewField(mask, s.radar); gpuModel.Sample(mask.Shown); }
                }
                if (gpuModel != null && rec.GpuDiff < 0 && rec.GpuWhy == null && req.done) GpuCompare(rec, req, gpuModel, fw, fh);
                if (fr.T >= seconds) break;
                yield return null;
            }
            if (gpuModel != null && rec.GpuDiff < 0 && rec.GpuWhy == null) { req.WaitForCompletion(); GpuCompare(rec, req, gpuModel, fw, fh); }   // after the recording only
            rec.RadarMax = OzFogOfWar.RadarMsMax; rec.Cost = OzFogOfWar.CostMs(); rec.OwnerAfter = s.radar.Owner;
        }

        /// <summary>The growth's numbers of one recording: the opening (OzV14FogRadarCount.Opening: the code, the circle alone, the game's map),
        /// the hitches' share of the time, the field and the minimap, the cost, the frame rate.</summary>
        sealed class Growth
        {
            public OzV14FogRadarCount.Opening O; public double Hitch, MaxDt, FieldShare, MiniPerS, MeanMs, Fps; public int GrowFrames;
        }

        static Growth Measure(Recording rec)
        {
            var g = new Growth(); var F = rec.F; double to = OzFowRadar.CirclePx / 8.0;
            var S = new List<OzV14FogRadarCount.Sample>(F.Count);
            foreach (var x in F)
                S.Add(new OzV14FogRadarCount.Sample { T = x.T, Dt = x.Dt, Age = x.Age, RadiusM = x.CodeM, FromM = rec.FromM, EdgeM = x.EdgeM, ContourM = x.ContourM, VisibleM = x.VisibleM, CoverStep = x.CoverStep,
                                                      MapEdgeM = x.MapEdgeM, MapContourM = x.MapContourM, MapVisibleM = x.MapVisibleM, MapCoverStep = x.MapCoverStep, RadarMs = x.RadarMs });
            g.O = OzV14FogRadarCount.Opening.Of(S);
            double msSum = 0, dtSum = 0; int field = 0, fieldFrames = 0; float growEnd = 0f; bool reached = false;
            for (int i = 2; i < F.Count && !reached; i++)
            {
                var x = F[i];
                g.GrowFrames++; msSum += x.RadarMs; dtSum += x.Dt; g.MaxDt = System.Math.Max(g.MaxDt, x.Dt);
                if (x.Dt > OzFowRadar.MaxStepSeconds) g.Hitch += x.Dt - OzFowRadar.MaxStepSeconds;
                if (F[i].Field > F[i - 1].Field) field++; fieldFrames++;
                if (x.CodeM >= to - 1e-6) { reached = true; growEnd = x.T; }
            }
            g.FieldShare = fieldFrames > 0 ? field / (double)fieldFrames : 0;
            int miniAtEnd = 0; foreach (var x in F) if (x.T <= growEnd) miniAtEnd = x.Mini;
            g.MiniPerS = growEnd > 0f ? miniAtEnd / growEnd : 0;
            g.MeanMs = g.GrowFrames > 0 ? msSum / g.GrowFrames : 0;
            g.Fps = dtSum > 0 ? g.GrowFrames / dtSum : 0;
            return g;
        }

        static void GrowthRows(string id, string what, Setup s, Recording rec, Growth g)
        {
            var o = g.O; double to = OzFowRadar.CirclePx / 8.0, bar = OzV14FogRadarCount.StepBarM, sightM = s.sight / 8.0;
            double want = OzFowRadar.GrowSeconds + g.Hitch, fromWant = OzFowRadar.StartPx(s.sight, (float)(rec.EdgeAtStartM * 8.0)) / 8.0;
            Row(id + "a", System.Math.Abs(o.FirstM - fromWant) < 0.01 && System.Math.Abs(o.FromM - fromWant) < 0.01 && !o.DownR && !o.DownE,
                $"{what}: the circle starts at {o.FirstM:0.00} m: the radar's building sight {sightM:0.0} m (the ground open all round it then {rec.EdgeAtStartM:0.00} m; the circle starts there only "
                + $"where that is further out); grows only (code {(o.DownR ? "WENT DOWN" : "never down")}, the circle's explored edge {(o.DownE ? "WENT DOWN" : "never down")})");
            Row(id + "b", o.Reach > 0 && System.Math.Abs(o.Reach - want) <= 0.1,
                $"{what}: 48 m at {o.Reach:0.000} s after it began (bar {OzFowRadar.GrowSeconds:0.0} s +- 0.1{(g.Hitch > 0 ? $", + {g.Hitch:0.000} s of frames longer than {OzFowRadar.MaxStepSeconds:0.0} s" : "")})");
            Row(id + "c", o.Step < bar && o.EStep < bar,
                $"{what}: largest step a frame {o.Step:0.000} m (the code's radius, the frame as it ran: {o.StepDt * 1000:0.0} ms, at {o.StepAtM:0.0} m, {o.Step / to * 100:0.00} % of 48 m, "
                + $"{(o.StepAtM > 0 ? o.Step / o.StepAtM * 100 : 0):0.00} % of the radius then), {o.EStep:0.000} m (the circle's explored edge); bar 2 % of 48 m = {bar:0.00} m; "
                + $"information: {o.Step60:0.000} m in a 60 fps frame; {g.GrowFrames} frames, {g.Fps:0} fps, the longest {g.MaxDt * 1000:0.0} ms (one of 46 ms or more at the fastest point, an editor hitch, would fail this row: run again)");
            Row(id + "d", o.P10 <= 0.03 && o.P90 >= 0.97 && o.E10 <= 0.05 && o.E90 >= 0.95 && o.LastStep < 0.01 && o.EGap < 0.25,
                $"{what}: eased: by 10 % of the time {o.P10 * 100:0.00} % of the growth (the circle's explored edge {o.E10 * 100:0.0} %), by 90 % {o.P90 * 100:0.00} % ({o.E90 * 100:0.0} %), "
                + $"linear would be 10 / 90; the last step {o.LastStep * 1000:0.000} mm (no snap at the end); the explored edge within {o.EGap:0.000} m of the code's radius (bar 0.25)");
            double restBar = OzV14FogRadarCount.RestBarS + g.Hitch, coverBar = OzV14FogRadarCount.CoverBarOf(EdgeNow(), OzFogOfWar.RevealSeconds);
            Row(id + "e", o.D.Ok(restBar, coverBar), $"{what}: the circle as the cloud draws it (the field model of the circle alone's shown map, from its start disc): " + o.D.Text(restBar, coverBar));
            Say($"  {id} information, the game's map as on screen ({s.zone} become the player's with the radar: their sights and the radar's own open with the normal reveal of a capture, "
                + $"0.8 s a cell, as before this kit): the explored edge jumps up to {o.MapJump:0.0} m in a frame; as drawn: {o.Map.Text(restBar, coverBar)}");
        }

        static IEnumerator CountRoutine()
        {
            Begin("Opening Count (in Play, a level as a human; L02)");
            var s = new Setup(); yield return Prepare(s);
            if (s.why != null) { Row("R0", false, s.why); End(LogFile); yield break; }
            var W = s.W; var mask = OzFogOfWar.Mask;
            Say($"  {SceneManager.GetActiveScene().name} ({s.g.level}), {Screen.width} x {Screen.height}, player {s.me}, world time {W.Time:F0} s; radar #{s.radar.Id} ({s.radar.Owner}) at ({s.radar.CenterX}, {s.radar.CenterY}) px, "
                + $"its building sight {s.sight / 8.0:0.0} m; {s.zone}; the fog was {(s.wasWanted ? "ON" : "off")} at the start, now ON for this match; the cloud's edge (the field model's): {EdgeNow().Text()}");
            if (s.radar.Owner == s.me) { W.AwardZone(s.flag, s.foe); yield return Frames(3); }
            Row("R0", OzFogOfWar.Wanted && mask != null && s.radar.Owner != s.me && OzFogOfWar.RadarRadiusPx(s.radar.Id) < 0f,
                $"the fog on, the radar not ours ({s.radar.Owner}) and without a circle before the check");
            int near = 0;
            foreach (var o in W.Objects)
                if (o != null && o.Owner == s.me && o.IsUnit && !o.Destroyed && o.KillAt < 0)
                {
                    long dx = o.CenterX - s.radar.CenterX, dy = o.CenterY - s.radar.CenterY;
                    if (dx * dx + dy * dy <= 75L * 8 * 75 * 8) near++;
                }
            Say($"  units of the player within 75 m of the radar: {near} (their own sight opens ground in the ring of the game's map too; the circle alone is not touched by them)");
            Cam(s.g, s.radar, 70f);
            s.f.CoverAgain(); yield return Seconds(OzFowRadar.GrowSeconds + OzFogOfWar.RevealSeconds + 0.3f);   // the radar's ground covered (the units' own sight, and any radar of ours, open again)
            {   // the code once before (JIT), so the cost row measures the game's frames
                var wm = new OzFowMask(512, 512); var wr = new OzFowRadar();
                for (int i = 0; i < 3; i++) { wr.Begin(0.016f); wr.See(wm, 1, 256, 256, 160, false); wr.End(); }
                new OzV14FogRadarCount.Circle(wm.W, wm.H, 256, 256, 160f, EdgeNow()).Step(170f, 6);
            }

            // ---- 1. the radar zone given to the player (the game's capture rule), on covered ground
            var gain = new Recording();
            yield return Record(s, gain, () => W.AwardZone(s.flag, s.me), 4.0f, 1.0f);
            var g1 = Measure(gain);
            Row("R1", gain.OwnerAfter == s.me && OzFogOfWar.Radar != null && OzFogOfWar.Radar.Started == gain.Started0 + 1,
                $"the radar zone given to the player (ZWorld.AwardZone, as a robot touching the flag): owner {gain.OwnerAfter}, one opening started (log line 'radar circle: radar #{s.radar.Id} stands and is ours')");
            GrowthRows("R2", "gain", s, gain, g1);
            double to = OzFowRadar.CirclePx / 8.0;
            var last = gain.F[gain.F.Count - 1];
            Row("R3", last.MapEdgeM >= to - 0.5 && last.MapContourM >= to - 3.0 && gain.Down == 0,
                $"the circle open on the game's map: explored {last.MapEdgeM:0.00} m (bar 47.5), drawn: the hiding contour {last.MapContourM:0.00} m (bar 45: the cloud's soft edge), the cleared cloud {last.MapVisibleM:0.00} m, "
                + $"after {last.T:0.0} s; opened area round the radar {last.AreaM2} m2 (a 48 m circle is {System.Math.PI * to * to:0} m2 less its soft edge); explored cells gone down {gain.Down}");
            Row("R4", g1.FieldShare >= 0.9 && g1.MiniPerS >= System.Math.Min(20.0, 0.6 * g1.Fps),
                $"the soft edge field made again on {g1.FieldShare * 100:0} % of the growth's frames (bar 90 %: it follows the circle), the minimap cover repainted {g1.MiniPerS:0.0} times a second "
                + $"while it grows (bar {System.Math.Min(20.0, 0.6 * g1.Fps):0.0}; 5 otherwise)");
            Row("R5", gain.RadarMax < 0.25,
                $"cost: the radar circle a frame mean {g1.MeanMs:0.0000} ms, worst {gain.RadarMax:0.0000} ms (bar 0.25 in the editor; its log lines are written after the time is taken); "
                + $"the fog's own processor time meanwhile {gain.Cost[5]:0.000} ms a frame (minimap {gain.Cost[4]:0.000}), worst frame {gain.Cost[6]:0.000} ms (information; Play Count P13 bars 0.25 / 2.0)");

            // ---- 2. lost
            var before = (byte[])mask.Target.Clone();
            W.AwardZone(s.flag, s.foe);
            yield return Seconds(1.0f);
            int down = 0, over = 0, closedRing = 0;
            for (int i = 0; i < before.Length; i++) { if (mask.Target[i] < before[i]) down++; if (mask.Shown[i] > mask.Target[i]) over++; }
            for (int a = 0; a < 64; a++)
            {
                double ang = a * System.Math.PI / 32;
                if (mask.TargetAtPx(s.radar.CenterX + (int)System.Math.Round(System.Math.Cos(ang) * 45 * 8), s.radar.CenterY + (int)System.Math.Round(System.Math.Sin(ang) * 45 * 8)) < OzFowMask.Revealed) closedRing++;
            }
            Row("R6", s.radar.Owner == s.foe && OzFogOfWar.RadarRadiusPx(s.radar.Id) < 0f && down == 0 && over == 0 && closedRing == 0,
                $"the radar lost ({s.radar.Owner}): its circle forgotten (radius {OzFogOfWar.RadarRadiusPx(s.radar.Id):0}), explore-once: explored cells gone down {down}, drawn above explored {over}, "
                + $"the ring at 45 m still open on {64 - closedRing} of 64 points (a point past the map's edge reads the edge's cell, inside the circle)");

            // ---- 3. regained: its growth begins again (explore-once kept all 48 m open: nothing to animate), nothing covered
            var regain = new Recording();
            yield return Record(s, regain, () => W.AwardZone(s.flag, s.me), 1.5f);
            double minEdge = double.MaxValue, minContour = double.MaxValue;
            for (int i = 1; i < regain.F.Count; i++) { minEdge = System.Math.Min(minEdge, regain.F[i].MapEdgeM); minContour = System.Math.Min(minContour, regain.F[i].MapContourM); }
            bool again = OzFogOfWar.Radar != null && OzFogOfWar.Radar.AlreadyOpen == regain.AlreadyOpen0 + 1 && OzFogOfWar.Radar.Started == regain.Started0;
            Row("R7", regain.OwnerAfter == s.me && again && regain.EdgeAtStartM >= to - 0.2 && regain.F.Count > 1 && regain.F[1].CodeM >= to - 1e-4 && regain.Down == 0 && minEdge >= to - 0.5 && minContour >= to - 3.0,
                $"regained: its growth begun again, from the ground open all round it ({regain.EdgeAtStartM:0.00} m: explore-once kept the 48 m open, so nothing to animate; log line 'all open already'), "
                + $"the circle {(regain.F.Count > 1 ? regain.F[1].CodeM : -1):0.00} m at once; the ground stays open all the time (explored edge never under {minEdge:0.00} m, the hiding contour never under {minContour:0.00} m), "
                + $"explored cells gone down {regain.Down}");

            // ---- 4. COVER AGAIN (the testing panel): the radar opens again, animated
            var cover = new Recording();
            yield return Record(s, cover, () => s.f.CoverAgain(), 3.6f, 1.25f);
            var g3 = Measure(cover);
            GrowthRows("R8", "after COVER AGAIN", s, cover, g3);
            var cl = cover.F[cover.F.Count - 1];
            Row("R9", cl.MapEdgeM >= to - 0.5 && cl.MapContourM >= to - 3.0, $"after COVER AGAIN the circle is open again on the game's map: {cl.MapEdgeM:0.00} m explored, the hiding contour {cl.MapContourM:0.00} m, the cleared cloud {cl.MapVisibleM:0.00} m after {cl.T:0.0} s");

            // ---- 5. the rules: view only
            int simFiles = 0, simHits = 0; var hits = new List<string>();
            try
            {
                foreach (var file in Directory.GetFiles(Path.Combine(Application.dataPath, "OpenZ", "Scripts", "Sim"), "*.cs", SearchOption.AllDirectories))
                {
                    simFiles++; var t = File.ReadAllText(file);
                    if (t.Contains("OzFogOfWar") || t.Contains("OzFowMask") || t.Contains("OzFowRadar")) { simHits++; hits.Add(Path.GetFileName(file)); }
                }
            }
            catch (System.Exception e) { hits.Add(e.Message); simHits++; }
            Row("R10", simFiles > 0 && simHits == 0, $"view only: none of the {simFiles} files of Scripts/Sim names the fog, its reveal map or the radar circle ({(hits.Count > 0 ? string.Join(", ", hits) : "0 files")}); the computer and the rules never read it");
            Row("R11", gain.OwnerAfter == s.me && s.radar.Owner == s.me && !s.radar.Destroyed, $"the zone changed hands only by the check's own captures (gain {gain.OwnerAfter}, loss to {s.foe}, regain {regain.OwnerAfter}); the radar stands");
            Row("R12", gain.GpuDiff >= 0 && cover.GpuDiff >= 0 && gain.GpuDiff <= FieldDiffBar && cover.GpuDiff <= FieldDiffBar && gain.GpuTexels > 1000 && cover.GpuTexels > 1000,
                $"the drawn edge's model is the game's own field: the game's map's field read back from the GPU (OzFogOfWar.FieldTexture, asked without waiting) {gain.GpuAge:0.00} s into the gain: largest difference {gain.GpuDiff:0.00000} over {gain.GpuTexels} texels"
                + $"{(gain.GpuWhy != null ? $" ({gain.GpuWhy})" : "")}; {cover.GpuAge:0.00} s after COVER AGAIN: {cover.GpuDiff:0.00000} over {cover.GpuTexels}{(cover.GpuWhy != null ? $" ({cover.GpuWhy})" : "")} (bar {FieldDiffBar})");

            // the frame table of the gain
            Say("  frames of the gain (the first: the frame before the radar's round): t s | frame ms | age s | code m | the circle alone: explored edge m, hiding contour m, cleared cloud m, cover step | "
                + "the game's map: explored edge m, hiding contour m, cleared cloud m | area m2 | field runs | minimap paints | radar ms");
            foreach (var x in gain.F)
                Say($"  {x.T,6:0.000} | {x.Dt * 1000,5:0.0} | {x.Age,6:0.000} | {x.CodeM,6:0.000} | {x.EdgeM,6:0.000} {x.ContourM,6:0.000} {x.VisibleM,6:0.000} {x.CoverStep,5:0.000} | "
                    + $"{x.MapEdgeM,6:0.000} {x.MapContourM,6:0.000} {x.MapVisibleM,6:0.000} | {x.AreaM2,5} | {x.Field,4} | {x.Mini,3} | {x.RadarMs:0.0000}");
            s.f.SetForced(s.wasWanted, out _);
            Say($"  the fog put back to {(s.wasWanted ? "ON" : "off")}; the radar zone stays the player's and the ground stays opened: start the level again for a match "
                + "(and before the fog's own Cost Bench or Play Count: they run COVER AGAIN, which restarts the circle of every radar of the player's)");
            End(LogFile);
        }

        // ================================================================ the owner's clips
        static string ClipFolder()
        {
            try
            {
                var d = new DirectoryInfo(Application.dataPath);
                for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
                {
                    var p = Path.Combine(d.FullName, "work", "staging", "v1_4", "owner_frames");
                    if (Directory.Exists(p)) return Path.Combine(p, "fog_radar");
                }
            }
            catch (System.Exception e) { Debug.LogWarning($"{Tag} {e.Message}"); }
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "fog_radar_clip"));
        }

        /// <summary>One take's numbers: frames, stills, frames from the capture to the circle at 48 m; the fog's clock per captured frame
        /// (mean, least, most; s), the real frame time per captured frame (mean s, information), the reveal's largest per-cell step between two
        /// captured frames, the fog's least strength.</summary>
        sealed class TakeResult { public int Frames, Stills, ReachAt = -1, RevealStep, ClockFrames; public double ClockMean, ClockMin = double.MaxValue, ClockMax, RealMean; public float StrengthMin = 1f; public bool Audio; }

        static IEnumerator ClipRoutine()
        {
            Begin("Owner Clips (in Play, L02): the radar circle opening at the normal RTS camera, 30 fps frame-locked, the game's sound");
            var s = new Setup(); yield return Prepare(s);
            if (s.why != null) { Row("C0", false, s.why); End(ClipFile); yield break; }
            if (Screen.width % 2 != 0 || Screen.height % 2 != 0) { Row("C0", false, $"the Game view is {Screen.width} x {Screen.height}: set it to 1920 x 1080 (even sizes for the encoder)"); End(ClipFile); yield break; }
            string dir = ClipFolder(); Directory.CreateDirectory(dir);
            Say($"  {s.g.level}, {Screen.width} x {Screen.height}, radar #{s.radar.Id}, its building sight {s.sight / 8f:0.0} m; {s.zone}; folder {dir}");
            const int fps = 30; int revealBar = Mathf.CeilToInt(255f * (1f / fps) / OzFogOfWar.RevealSeconds);
            foreach (float h in new[] { 70f, 110f })
            {
                if (s.radar.Owner == s.me) { s.W.AwardZone(s.flag, s.foe); yield return Seconds(1.0f); }
                Cam(s.g, s.radar, h);
                s.f.CoverAgain(); yield return Seconds(OzFogOfWar.RevealSeconds + 1.5f);   // the radar's ground covered, the rest settled
                string name = $"clip_radar_open_{s.g.level}_{h:0}m";
                var table = new StringBuilder("video frame | s after the capture | fog clock ms this frame | real ms this frame | code radius m | explored edge m | hiding contour m | cleared cloud m (the game's map)\n");
                var res = new TakeResult();
                yield return Take(s, Path.Combine(dir, name + ".mp4"), name, dir, fps, 1.0f, 4.5f, table, res);
                File.WriteAllText(Path.Combine(dir, name + ".txt"), table.ToString());
                double step = 1.0 / fps;
                bool clockOk = res.ClockFrames >= 160 && System.Math.Abs(res.ClockMean - step) <= 0.1 * step && res.ClockMin >= 0.9 * step && res.ClockMax <= 1.1 * step;
                Row(h < 90f ? "C1" : "C2", res.Frames == 165 && res.Stills == 6 && res.ReachAt >= 74 && res.ReachAt <= 77 && clockOk && res.RevealStep <= revealBar && res.StrengthMin >= 0.999f && res.Audio,
                    $"{name}.mp4: {res.Frames} of 165 frames (5.5 s at 30 fps, the capture at 1.0 s), {res.Stills} stills, the circle at 48 m {res.ReachAt} frames after the capture (75 = 2.5 s); "
                    + $"the fog's clock (OzFogOfWar.FogStep: the growth and the per-cell reveal) {res.ClockMean * 1000:0.000} ms a captured frame (least {res.ClockMin * 1000:0.000}, most {res.ClockMax * 1000:0.000}; bar 1/30 s +- 10 %), "
                    + $"the reveal's largest per-cell step between two video frames {res.RevealStep} (bar {revealBar} = the 1/30 s step; at real time it would be larger), the fog's strength never under {res.StrengthMin:0.000}; "
                    + $"audio {(res.Audio ? "recorded" : "NOT recorded")}; information: Time.unscaledDeltaTime {res.RealMean * 1000:0.0} ms a captured frame (it stays real time under Time.captureFramerate, "
                    + $"View/Blue/OzBlueCameo.cs Step; the fog does not read it while a take records); camera {h:0} m");
            }
            s.f.SetForced(s.wasWanted, out _);
            Say($"  look at the clips and stills in {dir} (normal RTS camera, the HUD as in a match); the match keeps the radar zone: start the level again");
            End(ClipFile);
        }

        /// <summary>One frame-locked take: lead seconds with the radar not ours, the capture, then after seconds.</summary>
        static IEnumerator Take(Setup s, string path, string name, string dir, int fps, float lead, float after, StringBuilder table, TakeResult res)
        {
            int ch = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2, rate = AudioSettings.outputSampleRate;
            int w = Screen.width, h = Screen.height, total = Mathf.RoundToInt((lead + after) * fps), at = Mathf.RoundToInt(lead * fps);
            var stills = new[] { 0f, 0.6f, 1.25f, 1.9f, 2.5f, 3.5f };
            var vattr = new VideoTrackAttributes { frameRate = new MediaRational(fps), width = (uint)w, height = (uint)h, includeAlpha = false, bitRateMode = VideoBitrateMode.High };
            var aattr = new AudioTrackAttributes { sampleRate = new MediaRational(rate), channelCount = (ushort)ch, language = "en" };
            MediaEncoder enc = null; int id = s.radar.Id; var mask = OzFogOfWar.Mask;
            OzV14FogRadarCount.EdgeMeter meter = null; OzV14FogRadarCount.FieldMeter field = null;
            var prevShown = (byte[])mask.Shown.Clone();
            double clock = OzFogOfWar.FogClockSeconds, clockSum = 0, realSum = 0;
            try
            {
                enc = new MediaEncoder(path, vattr, aattr);
                Time.captureFramerate = fps;
                yield return null;
                res.Audio = AudioRenderer.Start();
                clock = OzFogOfWar.FogClockSeconds; System.Buffer.BlockCopy(mask.Shown, 0, prevShown, 0, prevShown.Length);
                for (int k = 0; k < total; k++)
                {
                    if (k == at)
                    {
                        s.W.AwardZone(s.flag, s.me);
                        meter = new OzV14FogRadarCount.EdgeMeter(mask, s.radar.CenterX, s.radar.CenterY, 52 * 8);   // the map before the radar's round
                        field = NewField(mask, s.radar); field.Sample(mask.Shown);
                    }
                    yield return new WaitForEndOfFrame();
                    double dc = OzFogOfWar.FogClockSeconds - clock; clock = OzFogOfWar.FogClockSeconds;
                    clockSum += dc; realSum += Time.unscaledDeltaTime; res.ClockFrames++;
                    res.ClockMin = System.Math.Min(res.ClockMin, dc); res.ClockMax = System.Math.Max(res.ClockMax, dc);
                    var sh = mask.Shown; for (int i = 0; i < sh.Length; i++) { int d = sh[i] - prevShown[i]; if (d > res.RevealStep) res.RevealStep = d; }
                    System.Buffer.BlockCopy(sh, 0, prevShown, 0, prevShown.Length);
                    res.StrengthMin = Mathf.Min(res.StrengthMin, OzFogOfWar.Strength);
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    if (tex.format != TextureFormat.RGBA32 || tex.width != w || tex.height != h)
                    {
                        var t2 = new Texture2D(w, h, TextureFormat.RGBA32, false);
                        if (tex.width == w && tex.height == h) t2.SetPixels32(tex.GetPixels32());
                        t2.Apply(false); Object.Destroy(tex); tex = t2;
                    }
                    enc.AddFrame(tex); res.Frames++;
                    for (int i = 0; i < stills.Length; i++)
                        if (k == at + Mathf.RoundToInt(stills[i] * fps)) { File.WriteAllBytes(Path.Combine(dir, $"{name}_{stills[i]:0.00}s.jpg"), tex.EncodeToJPG(92)); res.Stills++; }
                    Object.Destroy(tex);
                    if (res.Audio)
                    {
                        int n = (int)AudioRenderer.GetSampleCountForCaptureFrame();
                        if (n > 0) using (var buf = new NativeArray<float>(n * ch, Allocator.Temp)) { AudioRenderer.Render(buf); enc.AddSamples(buf); }
                    }
                    float code = OzFogOfWar.RadarRadiusPx(id) / 8f;
                    if (k > at && res.ReachAt < 0 && code >= OzFowRadar.CirclePx / 8f - 1e-4f) res.ReachAt = k - at;
                    if (field != null) field.Sample(mask.Shown);
                    table.Append($"{k,4} | {(k - at) / (float)fps,6:0.000} | {dc * 1000,7:0.000} | {Time.unscaledDeltaTime * 1000,7:0.0} | {code,7:0.000} | {(meter != null ? meter.RadiusM(mask.Target) : -1),7:0.000} | "
                                 + $"{(field != null ? field.ContourM : -1),7:0.000} | {(field != null ? field.VisibleM : -1),7:0.000}\n");
                }
            }
            finally
            {
                Time.captureFramerate = 0;
                if (res.Audio) AudioRenderer.Stop();
                if (enc != null) enc.Dispose();
            }
            if (res.ClockFrames > 0) { res.ClockMean = clockSum / res.ClockFrames; res.RealMean = realSum / res.ClockFrames; }
            Say($"  {name}: {res.Frames} frames, audio {(res.Audio ? $"{rate} Hz x {ch}" : "NOT recorded")}, {res.Stills} stills, 48 m reached {res.ReachAt} frames after the capture; "
                + $"the fog's clock {res.ClockMean * 1000:0.000} ms a captured frame, real {res.RealMean * 1000:0.0} ms a captured frame");
        }
    }
}
