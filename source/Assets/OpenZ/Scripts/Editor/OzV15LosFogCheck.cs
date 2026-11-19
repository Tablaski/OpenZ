// OpenZ (GPL-3.0) - version 1.5 kit los_fog: the line-of-sight fog's Play check and its fps A / B (View/OzFogOfWar.Los.cs, View/OzFowSeen.cs).
using System.Collections;
using System.Collections.Generic;
using System.IO;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.5 kit los_fog, the Play check (OpenZ/V1.5/LOS Fog: Check + fps A-B; in Play, a MAYHEM match as a human, after the briefing). Rows in
    /// the console and Logs/oz_v15_los_fog.txt next to Assets:
    /// <list type="bullet">
    /// <item>L1 the seen-now map runs on the reveal map's grid; L2 the centre of every unit, building and flag of the player's side is fully seen;
    /// L3 nothing of the player's side is ever covered; L4 the enemy robots and vehicles on explored ground: covered exactly where the seen map
    /// says (under the hide line covered, from the show line shown, the gap between either); L5 buildings, ground guns, flags, empty vehicles
    /// and map items on explored ground are never covered (also those in the line-of-sight fog); L6 the units held off on the player's camera.</item>
    /// <item>L7 the processor time a frame; L8 the fps A / B: 5 s with it, 5 s without (OzFogOfWar.LosOn), twice, the mean frame time, the fog's
    /// own GPU time (its sheets pass) and the line-of-sight fog's processor time each way. The match keeps running; LosOn is put back on.</item>
    /// </list>
    /// </summary>
    public static class OzV15LosFogCheck
    {
        const string Tag = "[OpenZ V1.5 LosFog]", LogFile = "oz_v15_los_fog.txt";
        static readonly List<string> lines = new List<string>();
        static int pass, total; static bool running; static MonoBehaviour host;

        static void Begin(string what) { lines.Clear(); pass = total = 0; Say($"{Tag} {what}"); }
        static void Say(string s) { lines.Add(s); Debug.Log(s); }
        static void Row(string id, bool ok, string text) { total++; if (ok) pass++; Say($"  {(ok ? "PASS" : "FAIL")} {id} {text}"); }
        static void Info(string text) => Say("  INFO " + text);
        static void End()
        {
            Say($"{Tag} {(pass == total ? "PASS" : "FAIL")} {pass}/{total}");
            try
            {
                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, LogFile), string.Join("\n", lines) + "\n");
            }
            catch (System.Exception e) { Debug.LogWarning($"{Tag} could not write the log file: {e.Message}"); }
        }

        [MenuItem("OpenZ/V1.5/LOS Fog: Check + fps A-B (in Play, a MAYHEM match, about 30 s)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning($"{Tag} press Play, start a MAYHEM match as a human, then run this again"); return; }
            var g = ZGame.I;
            if (!g) { Debug.LogWarning($"{Tag} no level running: start a MAYHEM match as a human"); return; }
            if (running && host && host == g) { Debug.LogWarning($"{Tag} the check is already running"); return; }
            running = true; host = g;
            g.StartCoroutine(Guard(Routine()));
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
            finally { running = false; OzFogOfWar.LosOn = true; }
        }

        /// <summary>Batch (the kit's proof on the project copy: Unity.exe -batchmode -executeMethod OpenZ.OzV15LosFogCheck.ShaderBatch -quit): the fog
        /// editor quits with 0 (no error) or 1.</summary>
        public static void ShaderBatch()
        {
            var ls = new List<string>(); int errs = 0;
            try
            {
                const string path = "Assets/OpenZ/Resources/OpenZ/FX/OzFogOfWar.shader";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var sh = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (!sh) { ls.Add("FAIL the shader did not load: " + path); errs++; }
                else
                {
                    var m = new Material(sh) { hideFlags = HideFlags.DontSave };
                    for (int i = 0; i < m.passCount; i++) { ShaderUtil.CompilePass(m, i, true); ls.Add($"pass {i} {m.GetPassName(i)}: compiled"); }
                    foreach (var msg in ShaderUtil.GetShaderMessages(sh))
                    {
                        ls.Add($"{msg.severity} {msg.platform} {msg.file} line {msg.line}: {msg.message}");
                        if (msg.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) errs++;
                    }
                    bool he = ShaderUtil.ShaderHasError(sh); if (he) errs++;
                    ls.Add($"supported {sh.isSupported}, has an error {he}, passes {m.passCount}, the graphics API {SystemInfo.graphicsDeviceType}");
                    Object.DestroyImmediate(m);
                }
            }
            catch (System.Exception e) { ls.Add("FAIL " + e); errs++; }
            ls.Insert(0, $"{Tag} shader batch, {System.DateTime.Now:dd MMM yyyy HH:mm}: {(errs == 0 ? "PASS" : "FAIL")} ({errs} error(s))");
            try
            {
                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs")); Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "oz_v15_los_fog_shader.txt"), string.Join("\n", ls) + "\n");
            }
            catch (System.Exception) { }
            Debug.Log(string.Join("\n", ls));
            EditorApplication.Exit(errs == 0 ? 0 : 1);
        }

        static IEnumerator Seconds(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }

        static bool Movable(ZObject o) => o.Kind == ZKind.Robot || o.Kind == ZKind.Vehicle;

        static IEnumerator Routine()
        {
            Begin($"Check + fps A-B, {System.DateTime.Now:dd MMM yyyy HH:mm}");
            var g = ZGame.I; var W = g.World; var me = g.player;
            if (!OzFogOfWar.Wanted || OzFogOfWar.Seen == null)
            { Row("L0", false, $"no MAYHEM match with the fog running ({OzFogOfWar.WhyOffNow}): start one as a human against the computer"); End(); yield break; }
            for (int i = 0; i < 1800 && !OzFogOfWar.GameplayView; i++) yield return null;
            Row("L0", OzFogOfWar.GameplayView, $"the player plays with the fog on screen: {OzFogOfWar.GateLine()}");
            if (!OzFogOfWar.GameplayView) { End(); yield break; }
            OzFogOfWar.LosOn = true;
            yield return Seconds(1.5f);                                                     // the easing settles (up 0.4 s, down 1 s)

            var mask = OzFogOfWar.Mask; var seen = OzFogOfWar.Seen;
            Row("L1", seen != null && mask != null && seen.W == mask.W && seen.H == mask.H && OzFogOfWar.SeenTexture,
                $"the seen-now map on the reveal map's grid: {seen.W} x {seen.H} cells, texture {(OzFogOfWar.SeenTexture ? "made" : "missing")}, seen now {seen.SeenShare() * 100f:0.0} %, explored {mask.ExploredShare() * 100f:0.0} %");

            int agro = W.S != null ? W.S.agro_distance : 40, own = 0, ownBad = 0, ownCov = 0;
            foreach (var o in W.Objects)
            {
                if (o == null || o.Owner != me || o.Destroyed || o.KillAt >= 0) continue;
                if (OzFogOfWar.Covers(o)) ownCov++;
                if (OzFogOfWar.SightPx(o, agro) <= 0) continue;
                own++; if (seen.ShownAtPx(o.CenterX, o.CenterY) < 255) ownBad++;
            }
            Row("L2", own > 0 && ownBad == 0, $"every unit, building and flag of the player's side stands fully seen: {own} with a sight, {ownBad} not at 255 under their centre");
            Row("L3", ownCov == 0, $"nothing of the player's side is covered: {ownCov}");

            int foes = 0, inLos = 0, hidden = 0, wrong = 0, gap = 0; var wrongNames = new List<string>();
            int stay = 0, stayInLos = 0, stayCov = 0;
            foreach (var o in W.Objects)
            {
                if (o == null || o.Owner == me || o.Destroyed) continue;
                if (mask.ShownAtPx(o.CenterX, o.CenterY) < OzFowMask.Revealed) continue;      // ground not explored: the cloud's own rule (1.4 checks)
                byte v = seen.ShownAtPx(o.CenterX, o.CenterY);
                bool cov = OzFogOfWar.Covers(o);
                if (Movable(o) && o.Owner != Team.Neutral)
                {
                    foes++; if (v < OzFogOfWar.LosHideBelow) inLos++; if (cov) hidden++;
                    bool ok = v < OzFogOfWar.LosHideBelow ? cov : v >= OzFogOfWar.LosShowAt ? !cov : true;
                    if (v >= OzFogOfWar.LosHideBelow && v < OzFogOfWar.LosShowAt) gap++;
                    if (!ok) { wrong++; if (wrongNames.Count < 6) wrongNames.Add($"{o.Type} #{o.Id} seen {v} covered {cov}"); }
                }
                else if (o.Kind == ZKind.Building || o.Kind == ZKind.Cannon || o.Kind == ZKind.MapItem || (Movable(o) && o.Owner == Team.Neutral))
                {
                    stay++; if (v < OzFogOfWar.LosHideBelow) stayInLos++; if (cov) stayCov++;
                }
            }
            Row("L4", wrong == 0, $"enemy robots and vehicles on explored ground: {foes}, in the line-of-sight fog {inLos}, covered {hidden}, in the gap {gap}, wrong {wrong}"
                                  + (wrongNames.Count > 0 ? " (" + string.Join("; ", wrongNames) + ")" : "") + (inLos == 0 ? " (none in the fog now: send a unit away from an enemy group, or run again later)" : ""));
            Row("L5", stayCov == 0, $"buildings, ground guns, flags, empty vehicles and map items on explored ground stay: {stay} ({stayInLos} in the line-of-sight fog), covered {stayCov}");
            yield return null; yield return null;
            Info($"L6 on the player's camera: units held off by the line-of-sight fog {OzFogOfWar.LosViews} (in view), their lamps held off {OzFogOfWar.LosLightsHeld}; units in covered ground held off {OzFogOfWar.HiddenViews}");

            // L7 / L8: the cost, and the fps with and without it (the fog's GPU timing on for the run)
            bool gpuWas = OzFogOfWar.GpuTiming; OzFogOfWar.GpuTiming = true;
            var res = new double[4, 4];
            for (int ph = 0; ph < 4; ph++)
            {
                bool on = ph % 2 == 0;
                OzFogOfWar.LosOn = on;
                yield return Seconds(1f);                                                   // settle (a switch on eases the map in)
                OzFogOfWar.GpuReset(); OzFogOfWar.LosCostReset();
                int f0 = Time.frameCount; float t0 = Time.realtimeSinceStartup;
                yield return Seconds(5f);
                int n = Mathf.Max(1, Time.frameCount - f0); double secs = Time.realtimeSinceStartup - t0;
                res[ph, 0] = secs * 1000.0 / n; res[ph, 1] = OzFogOfWar.GpuMsCloud; res[ph, 2] = OzFogOfWar.LosFrames > 0 ? OzFogOfWar.LosMsSum / OzFogOfWar.LosFrames : 0.0; res[ph, 3] = OzFogOfWar.LosMsMax;
                Info($"L8 {(on ? "WITH   " : "WITHOUT")} the line-of-sight fog: {n} frames in {secs:0.00} s = {1000.0 / res[ph, 0]:0.0} fps ({res[ph, 0]:0.000} ms a frame), "
                     + $"the fog's sheets pass on the GPU {res[ph, 1]:0.000} ms, the line-of-sight fog's processor time {res[ph, 2]:0.000} ms a frame (worst {res[ph, 3]:0.000})");
            }
            OzFogOfWar.LosOn = true; OzFogOfWar.GpuTiming = gpuWas;
            double onMs = (res[0, 0] + res[2, 0]) * 0.5, offMs = (res[1, 0] + res[3, 0]) * 0.5, onGpu = (res[0, 1] + res[2, 1]) * 0.5, offGpu = (res[1, 1] + res[3, 1]) * 0.5;
            double cpu = (res[0, 2] + res[2, 2]) * 0.5, worst = System.Math.Max(res[0, 3], res[2, 3]);
            Row("L7", cpu < 0.40, $"the line-of-sight fog's processor time: {cpu:0.000} ms a frame on average (worst frame {worst:0.000} ms; bar 0.40 ms in the editor's Mono, the built game runs it about 3 x faster)");
            Info($"L8 A / B: with {1000.0 / onMs:0.0} fps ({onMs:0.000} ms), without {1000.0 / offMs:0.0} fps ({offMs:0.000} ms): {onMs - offMs:+0.000;-0.000} ms a frame; "
                 + $"the sheets pass on the GPU {onGpu:0.000} ms with, {offGpu:0.000} ms without ({onGpu - offGpu:+0.000;-0.000} ms)");
            Row("L8", onGpu - offGpu < 0.25, $"the GPU cost of the darkening in the fog's own pass: {onGpu - offGpu:+0.000;-0.000} ms (bar 0.25 ms; the frame time difference above is the match's own noise and the processor's part)");
            End();
        }
    }
}
