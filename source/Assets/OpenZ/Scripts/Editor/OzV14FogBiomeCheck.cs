// OpenZ (GPL-3.0) - version 1.4, FOG OF WAR checks (editor only): the MAYHEM fog's look by biome, in Play, and the owner's frames and clip.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4 fog_biome_looks, the Play check (editor only, nothing in the game changes): in a running MAYHEM match (any level; the
    /// player's RTS camera, the fog on screen):
    /// <list type="bullet">
    /// <item>P1 the level's biome, its style on the material, the colour LAND on the material, the cover easing.</item>
    /// <item>P2 a sweep of 80 s of the biome clock in 10 s (the clock x 8, the check only): every frame the cover inside its range, the step
    /// within the bar (x 8), the material's see-through = 1 - the cover on screen; both ends reached.</item>
    /// <item>P3 the owner's frames: the cover at its low and at its high end (the clock set there), PNG of the game view as the player sees it.</item>
    /// <item><see cref="ClipSeconds"/><see cref="ClipClockScale"/>
    /// 1 = real speed), the ffmpeg line in the report. <see cref="ArcticToo"/>: the same for the ARCTIC biome (the testing panel's PLANET, the
    /// provisional arctic palette over this level's ground).</item>
    /// </list>
    /// Report and files: Logs/fog_biome/ (or <see cref="OutDir"/>).
    /// </summary>
    public static class OzV14FogBiomeCheck
    {
        const string Tag = "[OpenZ V1.4 fog biome looks]";
        public static string OutDir = "Logs/fog_biome";
        /// <summary>
        /// 4 = one full swing in 14 s).</summary>
        public static float ClipSeconds = 0f, ClipClockScale = 1f;
        public static bool ArcticToo = false;
        public static string Report = "not run";
        public static bool Running => running != null;

        [MenuItem("OpenZ/V1.4/Fog Biome Looks: Play Check + owner frames (a MAYHEM match in Play)")]
        static void Menu() => Run(Check());
        [MenuItem("OpenZ/V1.4/Fog Biome Looks: Play Check + owner frames + 20 s clip (real speed)")]
        static void MenuClip() { ClipSeconds = 20f; ClipClockScale = 1f; Run(Check()); }
        [MenuItem("OpenZ/V1.4/Fog Biome Looks: Play Check + owner frames + 14 s time-lapse (one swing, clock x 4)")]
        static void MenuLapse() { ClipSeconds = 14f; ClipClockScale = 4f; Run(Check()); }
        [MenuItem("OpenZ/V1.4/Fog Biome Looks: Play Check + owner frames, this level's biome and ARCTIC (provisional)")]
        static void MenuArctic() { ArcticToo = true; Run(Check()); }

        static IEnumerator running;
        static readonly Stack<IEnumerator> nest = new Stack<IEnumerator>();
        static void Run(IEnumerator it)
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning($"{Tag} press Play, start a MAYHEM match as a human, wait for the RTS camera, then run this again"); ClipSeconds = 0f; ClipClockScale = 1f; ArcticToo = false; return; }
            if (running != null) { Debug.LogWarning($"{Tag} a check is already running"); return; }
            running = it; nest.Clear(); nest.Push(it); EditorApplication.update += Step;
        }
        static void Step()
        {
            bool more = false;
            try
            {
                while (EditorApplication.isPlaying && nest.Count > 0)
                {
                    var top = nest.Peek();
                    if (!top.MoveNext()) { nest.Pop(); continue; }
                    if (top.Current is IEnumerator inner) { nest.Push(inner); continue; }
                    more = true; break;
                }
            }
            catch (System.Exception e) { Debug.LogError($"{Tag} the check stopped: {e}"); more = false; }
            if (!more) { EditorApplication.update -= Step; running = null; nest.Clear(); OzFogOfWar.BiomeClockScale = 1f; Time.captureFramerate = 0; ClipSeconds = 0f; ClipClockScale = 1f; ArcticToo = false; }
        }
        static IEnumerator Frames(int n) { int f = Time.frameCount + n; while (Time.frameCount < f) yield return null; }

        static readonly StringBuilder sb = new StringBuilder(); static int fails;
        static void Row(string id, bool ok, string text) { if (!ok) fails++; sb.Append(ok ? "ok    " : "FAIL  ").Append(id).Append(' ').Append(text).Append('\n'); }
        static readonly int IdSee = Shader.PropertyToID("_OzFowSee");

        static IEnumerator Shot(string path)
        {
            if (File.Exists(path)) File.Move(path, path + "." + System.DateTime.Now.ToString("HHmmss") + ".old");
            ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 120 && !File.Exists(path); i++) yield return null;
            yield return Frames(2);
        }

        /// <summary>The fog's clock at which the biome's cover is at its low (k = 0) or high (k = 0.5) end, at or after now.</summary>
        static double ClockAt(double k)
        {
            double P = OzFogBiomeLooks.PeriodS, x = OzFogOfWar.BiomeClock / P + OzFogOfWar.BiomePhase;
            double n = System.Math.Floor(x - k) + 1.0;
            return (n + k - OzFogOfWar.BiomePhase) * P;
        }

        static IEnumerator Check()
        {
            sb.Clear(); fails = 0;
            var g = ZGame.I; string dir = Path.GetFullPath(OutDir); Directory.CreateDirectory(dir);
            sb.Append($"OpenZ 1.4 fog_biome_looks Play check, {System.DateTime.Now:dd MMM yyyy HH:mm}, level {(g ? g.level : "-")}, game view {Screen.width} x {Screen.height}\n");
            if (!g || !OzFogOfWar.I || !OzFogOfWar.Wanted) { Row("P0", false, "no MAYHEM match with the fog running (start one: MAYHEM, a human against the CPU)"); yield return Finish(dir); yield break; }
            for (int i = 0; i < 600 && OzFogOfWar.Strength < 0.99f; i++) yield return null;
            Row("P0", OzFogOfWar.BiomeOn && OzFogOfWar.Strength >= 0.99f, $"a MAYHEM match with the fog on screen: BiomeOn {OzFogOfWar.BiomeOn}, strength {OzFogOfWar.Strength:0.00} (the briefing over, the player's camera)");
            var biomes = new List<int> { OzFogBiomeLooks.Clamp(OzFogBiomeLooks.BiomeOf(g.level)) };
            if (ArcticToo) biomes.Add(OzFogBiomeLooks.Arctic);
            int planetWas = OzFogOfWar.PlanetOverride;
            foreach (int b in biomes)
            {
                if (b != biomes[0]) { OzFogOfWar.PlanetOverride = b; yield return Frames(4); }
                yield return BiomeRows(g, b, dir);
            }
            OzFogOfWar.PlanetOverride = planetWas; yield return Frames(2);
            yield return Finish(dir);
        }

        static IEnumerator BiomeRows(ZGame g, int b, string dir)
        {
            var L = OzFogBiomeLooks.Biomes[b]; string tag = $"{g.level}_{L.Biome}";
            var m = OzFogOfWar.Mat;
            bool p1 = OzFogOfWar.BiomeIndex == b && OzFogOfWar.LookName == L.Style && OzFogOfWar.ColourNow == OzFogOfWar.ColourByName(L.Colour)
                      && OzFogOfWar.AppliedColour == OzFogOfWar.ColourNow && OzFogOfWar.BiomeEases && OzFogOfWar.MaterialHasLook(OzFogOfWar.LookIndex);
            Row("P1 " + L.Biome, p1, $"biome {OzFogBiomeLooks.Biomes[OzFogOfWar.BiomeIndex].Biome}, style {OzFogOfWar.LookName} (wanted {L.Style}), colour {OzFogOfWar.ColourNames[OzFogOfWar.ColourNow]} on the material "
                + $"{OzFogOfWar.AppliedColour == OzFogOfWar.ColourNow}, easing {OzFogOfWar.BiomeEases}, cover now {OzFogOfWar.CoverNow * 100f:0.00} % ({L.CoverMin * 100f:0}-{L.CoverMax * 100f:0} %), phase {OzFogOfWar.BiomePhase:0.000}");
            // P2: 80 s of the clock in 10 s
            double clockWas = OzFogOfWar.BiomeClock; OzFogOfWar.BiomeClockScale = 8f;
            float lo = 2f, hi = -1f, prev = OzFogOfWar.CoverNow, worst = 0f, seeErr = 0f; bool inside = true; int n = 0;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 10f)
            {
                yield return null;
                float v = OzFogOfWar.CoverNow; n++;
                inside &= v >= L.CoverMin && v <= L.CoverMax; lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v);
                worst = Mathf.Max(worst, Mathf.Abs(v - prev)); prev = v;
                if (m) seeErr = Mathf.Max(seeErr, Mathf.Abs(m.GetFloat(IdSee) - (1f - v)));
            }
            OzFogOfWar.BiomeClockScale = 1f;
            bool p2 = inside && worst <= OzFogBiomeLooks.MaxStepPerFrame * 8f && seeErr < 1e-4f && L.CoverMin - lo > -0.002f && hi - L.CoverMax > -0.002f && n > 100;
            Row("P2 " + L.Biome, p2, $"80 s of the clock in 10 s ({n} frames, clock x 8): low {lo * 100f:0.00} % high {hi * 100f:0.00} % (range {L.CoverMin * 100f:0}-{L.CoverMax * 100f:0} %), "
                + $"largest frame step {worst * 100f:0.000} % (bar x 8 {OzFogBiomeLooks.MaxStepPerFrame * 800f:0.00} %), the material's see-through off by at most {seeErr:0.000000}");
            // P3: the owner's frames at both ends
            string[] ends = { "low", "high" }; double[] ks = { 0.0, 0.5 }; var files = new List<string>();
            for (int e = 0; e < 2; e++)
            {
                OzFogOfWar.BiomeClock = ClockAt(ks[e]); yield return Frames(3);
                string f = Path.Combine(dir, $"fog_biome_{tag}_{ends[e]}_{Mathf.RoundToInt(OzFogOfWar.CoverNow * 100f)}.png");
                yield return Shot(f); files.Add(f);
            }
            Row("P3 " + L.Biome, files.TrueForAll(File.Exists), "the frames: " + string.Join(", ", files).Replace('\\', '/'));
            if (ClipSeconds > 0f)
            {
                string cd = Path.Combine(dir, $"clip_{tag}_x{ClipClockScale:0}"); Directory.CreateDirectory(cd);
                int capWas = Time.captureFramerate; Time.captureFramerate = 30; OzFogOfWar.BiomeClockScale = ClipClockScale;
                int frames = Mathf.RoundToInt(ClipSeconds * 30f);
                for (int i = 0; i < frames; i++) { ScreenCapture.CaptureScreenshot(Path.Combine(cd, $"f{i:00000}.png")); yield return null; }
                yield return Frames(4);
                Time.captureFramerate = capWas; OzFogOfWar.BiomeClockScale = 1f;
                int got = Directory.GetFiles(cd, "f*.png").Length;
                string mp4 = Path.Combine(dir, $"fog_biome_{tag}_{(ClipClockScale > 1f ? "timelapse_x" + ClipClockScale.ToString("0") : "real_speed")}.mp4").Replace('\\', '/');
                Row("P4 " + L.Biome, got >= frames - 2, $"the clip's PNG frames: {got} of {frames} in {cd.Replace('\\', '/')}; make it (one heavy job, below normal priority): "
                    + $"start /belownormal /wait ffmpeg -y -framerate 30 -i \"{cd.Replace('\\', '/')}/f%05d.png\" -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -threads 4 -movflags +faststart \"{mp4}\"");
            }
            OzFogOfWar.BiomeClock = clockWas; yield return Frames(2);
        }

        static IEnumerator Finish(string dir)
        {
            sb.Append(fails == 0 ? "PLAY CHECK PASS\n" : $"PLAY CHECK FAIL ({fails})\n");
            Report = sb.ToString();
            File.WriteAllText(Path.Combine(dir, "oz_v14_fog_biome_play.txt"), Report);
            if (fails == 0) Debug.Log($"{Tag}\n{Report}"); else Debug.LogError($"{Tag}\n{Report}");
            ClipSeconds = 0f; ClipClockScale = 1f; ArcticToo = false;
            yield break;
        }
    }
}
