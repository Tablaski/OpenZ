using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit water_fx_1_5, the counted checks (test tools: nothing is saved in the project).
    /// <list type="bullet">
    /// <item>1 Count (no Play): every water / swamp material's numbers (ripple drift and shape change, the level 17 travelling waves,
    /// the fire reflection and the sun lane), both water shaders carry the rows and compile without an error, the splash channels
    /// are soft lit puffs (no stretched streak), the night storm rule: city levels yes, the others no.</item>
    /// <item>2 Storm (Play, a level playing): the night storm is on this level exactly when the level is a city level; with the
    /// option on a forced storm starts and rains, with the option off it is refused and the rain is gone; the option put back.</item>
    /// <item>3 Ripple clock (Play, any moment: a level, a briefing, a recording): the clock the water's ripples run on (game time,
    /// the shader's _Time) is measured over half a second of real time, twice.</item>
    /// <item>4 Splash test (Play, a level with water in view): a blast column and three piece splashes on the open water nearest
    /// the camera's focus, a few seconds apart, for frames. 5 toggles the new splash look (before / after pictures).</item>
    /// </list>
    /// Reports: the Console and Logs/water_fx_*.txt of the project.
    /// </summary>
    public static class OzV15WaterFxCheck
    {
        const string M = "OpenZ/V1.5/Water FX/", Tag = "[OpenZ Water FX]";
        const string Env = "Assets/OpenZ/Materials/Env/";

        struct Row { public string mat, level, prop; public Vector4 want; public bool vec; }

        static Row F(string mat, string level, string prop, float v) => new Row { mat = mat, level = level, prop = prop, want = new Vector4(v, 0, 0, 0) };
        static Row V(string mat, string level, string prop, float x, float y, float z) => new Row { mat = mat, level = level, prop = prop, want = new Vector4(x, y, z, 0), vec = true };

        /// <summary>The numbers the kit writes (README of the kit, section 'Numbers').</summary>
        static readonly Row[] Rows =
        {
            F("M_Water_L18", "L18", "_RippleProc", 1f), V("M_Water_L18", "L18", "_ProcDrift", 0.25f, 0.15f, 0.08f), V("M_Water_L18", "L18", "_ProcDriftTurn", -18f, 14f, 6f),
            F("M_Water_L18", "L18", "_ProcMorph", 0.6f), F("M_Water_L18", "L18", "_FireReflect", 1f), F("M_Water_L18", "L18", "_SunLane", 0.55f), F("M_Water_L18", "L18", "_SunLanePower", 70f),
            F("M_Water_Harbour", "L17", "_RippleProc", 1f), F("M_Water_Harbour", "L17", "_ProcSize", 0.42f), V("M_Water_Harbour", "L17", "_ProcSlope", 0.23f, 0.19f, 0.12f),
            F("M_Water_Harbour", "L17", "_ProcSpeed", 1f), F("M_Water_Harbour", "L17", "_ProcCalm", 0.85f), F("M_Water_Harbour", "L17", "_ProcCalmSize", 9f),
            V("M_Water_Harbour", "L17", "_ProcDrift", 0.25f, 0.15f, 0.08f), V("M_Water_Harbour", "L17", "_ProcDriftTurn", -18f, 14f, 6f), F("M_Water_Harbour", "L17", "_ProcMorph", 0.6f),
            F("M_Water_Harbour", "L17", "_RippleContrast", 0.35f),
            F("M_Water_Harbour", "L17", "_FireReflect", 0.8f), F("M_Water_Harbour", "L17", "_SunLane", 0.4f), F("M_Water_Harbour", "L17", "_SunLanePower", 60f),
            // level 16's swamp: fire + sun only (27), its ripples untouched
            F("M_Water_Swamp", "L16", "_RippleProc", 0f), F("M_Water_Swamp", "L16", "_FireReflect", 0.6f), F("M_Water_Swamp", "L16", "_SunLane", 0.3f), F("M_Water_Swamp", "L16", "_SunLanePower", 36f),
            // level 2's river (OpenZ/WaterV4): fire + sun only (27)
            F("M_Water", "L02", "_FireReflect", 0.9f), F("M_Water", "L02", "_SunLane", 0.45f), F("M_Water", "L02", "_SunLanePower", 110f),
        };

        static readonly string[] StylizedRows = { "_RippleProc", "_ProcDrift", "_ProcDriftTurn", "_ProcMorph", "_FireReflect", "_SunLane", "_SunLanePower" };
        static readonly string[] V4Rows = { "_FireReflect", "_SunLane", "_SunLanePower" };
        static readonly string[] Levels = { "L02", "L07", "L16", "L17", "L18" };
        static readonly HashSet<string> CityLevels = new HashSet<string> { "L17", "L18" };

        [MenuItem(M + "1 Count (no Play)", false, 7700)]
        public static void CountMenu() { int bad = Count(out string text); Save("water_fx_count.txt", text); if (bad == 0) Debug.Log(text); else Debug.LogError(text); }

        /// <summary>Batch entry (the kit's proof on the project copy): the count, then the editor exits with 0 = all PASS.</summary>
        public static void BatchProof()
        {
            int bad;
            string text, pre = "";
            try
            {   // compile every pass of both water shaders now (the default variant of this graphics device), so the count reads fresh messages
                foreach (var name in new[] { "OpenZ/WaterStylized", "OpenZ/WaterV4" })
                {
                    var sh = Shader.Find(name); if (!sh) continue;
                    var m = new Material(sh);
                    for (int p = 0; p < m.passCount; p++) ShaderUtil.CompilePass(m, p, true);
                    pre += $"compiled {name}: {m.passCount} pass(es) on {SystemInfo.graphicsDeviceType}\n";
                    Object.DestroyImmediate(m);
                }
            }
            catch (System.Exception e) { pre += "compile pass: " + e.Message + "\n"; }
            try { bad = Count(out text); text = pre + text; }
            catch (System.Exception e) { bad = 1; text = "EXCEPTION " + e; }
            text += "\n" + (bad == 0 ? "BATCH PROOF OK" : "BATCH PROOF FAILED (" + bad + ")");
            Save("water_fx_batch.txt", text);
            EditorApplication.Exit(bad == 0 ? 0 : 1);
        }

        static int Count(out string text)
        {
            var sb = new StringBuilder(); int ok = 0, bad = 0;
            void Line(bool pass, string s) { if (pass) ok++; else bad++; sb.AppendLine((pass ? "PASS " : "FAIL ") + s); }
            sb.AppendLine(Tag + " count, " + System.DateTime.Now.ToString("dd MMM yyyy HH:mm"));
            // the shaders: the rows are there, no compile error
            foreach (var (name, rows) in new[] { ("OpenZ/WaterStylized", StylizedRows), ("OpenZ/WaterV4", V4Rows) })
            {
                var sh = Shader.Find(name);
                if (!sh) { Line(false, name + ": shader not found"); continue; }
                var miss = new List<string>(); foreach (var r in rows) if (sh.FindPropertyIndex(r) < 0) miss.Add(r);
                Line(miss.Count == 0, $"{name}: rows {string.Join(", ", rows)} {(miss.Count == 0 ? "there" : "MISSING " + string.Join(", ", miss))}");
                bool err = ShaderUtil.ShaderHasError(sh);
                var msgs = ShaderUtil.GetShaderMessages(sh); int nErr = 0;
                foreach (var m in msgs) if (m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) { nErr++; if (nErr <= 5) sb.AppendLine("     " + m.message + " (line " + m.line + ")"); }
                Line(!err && nErr == 0, $"{name}: compiles without an error ({msgs.Length} message(s), {nErr} error(s))");
            }
            // the materials' numbers
            foreach (var r in Rows)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(Env + r.mat + ".mat");
                if (!m) { Line(false, r.mat + ": material missing"); continue; }
                if (!m.HasProperty(r.prop)) { Line(false, $"{r.level} {r.mat}.{r.prop}: the shader {m.shader.name} has no such row"); continue; }
                if (r.vec)
                {
                    var v = m.GetVector(r.prop);
                    bool same = Mathf.Abs(v.x - r.want.x) < 1e-4f && Mathf.Abs(v.y - r.want.y) < 1e-4f && Mathf.Abs(v.z - r.want.z) < 1e-4f;
                    Line(same, $"{r.level} {r.mat}.{r.prop} = ({v.x:0.###}, {v.y:0.###}, {v.z:0.###}) (want {r.want.x:0.###}, {r.want.y:0.###}, {r.want.z:0.###})");
                }
                else
                {
                    float v = m.GetFloat(r.prop);
                    Line(Mathf.Abs(v - r.want.x) < 1e-4f, $"{r.level} {r.mat}.{r.prop} = {v:0.###} (want {r.want.x:0.###})");
                }
            }
            // every other material on the two water shaders: the new rows stay 0 (they draw as before)
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/OpenZ" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid); var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m || !m.shader || (m.shader.name != "OpenZ/WaterStylized" && m.shader.name != "OpenZ/WaterV4")) continue;
                string n = Path.GetFileNameWithoutExtension(path);
                if (n == "M_Water_L18" || n == "M_Water_Harbour" || n == "M_Water_Swamp" || n == "M_Water") continue;
                float sum = 0f; foreach (var p in new[] { "_ProcMorph", "_FireReflect", "_SunLane" }) if (m.HasProperty(p)) sum += Mathf.Abs(m.GetFloat(p));
                if (m.HasProperty("_ProcDrift")) { var d = m.GetVector("_ProcDrift"); sum += Mathf.Abs(d.x) + Mathf.Abs(d.y) + Mathf.Abs(d.z); }
                Line(sum == 0f, $"{path} (not a level's live water): the new rows {(sum == 0f ? "0, draws as before" : "NOT 0")}");
            }
            // the splash channels: soft lit puffs, none stretched
            var defs = OzWaterSplash.Defs();
            foreach (var d in defs)
                Line(d.shape == FxShape.Puff && !d.stretch && d.lit >= 1f && d.edgeSoft >= 0.4f && d.softFade > 0f,
                     $"splash channel {d.name}: {d.shape}, stretched {d.stretch}, lit {d.lit}, soft edge {d.edgeSoft}, soft fade at contact {d.softFade} m");
            // the night storm: a rule of the city levels
            var cities = new List<string>();
            foreach (var lv in Levels) if (OzCity.Is(lv)) cities.Add(lv);
            bool rule = cities.Count == CityLevels.Count && cities.TrueForAll(CityLevels.Contains);
            Line(rule, $"night storm / rain levels (ZGame adds OzWeather where OzCity.Is): {string.Join(", ", cities)} (want L17, L18; L02, L07, L16 none); option NIGHT & RAIN default {(PlayerPrefs.HasKey("oz_weather") ? "set by this machine" : "ON")}");
            sb.Insert(0, $"{Tag} {ok} PASS, {bad} FAIL\n");
            text = sb.ToString();
            return bad;
        }

        static void Save(string file, string text)
        {
            try { var d = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs")); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, file), text); }
            catch (System.Exception e) { Debug.LogWarning(Tag + " report not written: " + e.Message); }
        }

        // ============================================================================ Play steps (EditorApplication.update, never a blocking call)
        static IEnumerator run; static double waitUntilReal; static float waitUntilGame; static readonly StringBuilder rep = new StringBuilder(); static int pOk, pBad; static string repFile;

        static void Start(IEnumerator body, string file)
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning(Tag + " enter Play first"); return; }
            if (run != null) { Debug.LogWarning(Tag + " a run is still going"); return; }
            rep.Clear(); pOk = pBad = 0; repFile = file; run = body; waitUntilReal = 0; waitUntilGame = 0f;
            EditorApplication.update -= Step; EditorApplication.update += Step;
        }

        static void Step()
        {
            if (run == null || !EditorApplication.isPlaying) { EditorApplication.update -= Step; if (run != null) { run = null; Debug.LogWarning(Tag + " stopped: Play was left"); } return; }
            if (EditorApplication.timeSinceStartup < waitUntilReal || Time.time < waitUntilGame) return;
            bool more;
            try { more = run.MoveNext(); }
            catch (System.Exception e) { Debug.LogException(e); more = false; P(false, "exception: " + e.Message); }
            if (!more) { EditorApplication.update -= Step; run = null; Finish(); return; }
            waitUntilReal = 0; waitUntilGame = 0f;
            if (run.Current is float g) waitUntilGame = Time.time + g;                    // a float = seconds of game time
            else if (run.Current is double r) waitUntilReal = EditorApplication.timeSinceStartup + r;   // a double = seconds of real time
        }

        static void P(bool pass, string s) { if (pass) pOk++; else pBad++; rep.AppendLine((pass ? "PASS " : "FAIL ") + s); }

        static void Finish()
        {
            string text = $"{Tag} {repFile}: {pOk} PASS, {pBad} FAIL\n" + rep;
            Save(repFile, text);
            if (pBad == 0) Debug.Log(text); else Debug.LogError(text);
        }

        [MenuItem(M + "2 Storm (Play, a level playing)", false, 7701)]
        static void StormMenu() => Start(StormBody(), "water_fx_storm.txt");

        static IEnumerator StormBody()
        {
            var g = ZGame.I;
            if (!g) { P(false, "no level playing"); yield break; }
            bool city = OzCity.Is(g.level), has = OzWeather.I;
            P(city == has, $"{g.level}: city level {city}, night storm on this level {has}");
            if (!has) yield break;
            bool opt0 = OzWeather.Enabled;
            OzWeather.Enabled = true;
            int st0 = OzWeather.I.StormsStarted;
            bool forced = OzWeather.Force(40f);
            yield return 3f;
            P(forced && OzWeather.I.StormsStarted > st0 && OzWeather.I.Rain > 0.01f, $"option ON: a forced storm starts (forced {forced}, storms {OzWeather.I.StormsStarted - st0}, rain {OzWeather.I.Rain:0.00} after 3 s; the clock jumped to night)");
            yield return 4f;
            P(OzWeather.I.Rain > 0.1f, $"option ON: it rains (rain {OzWeather.I.Rain:0.00} after 7 s)");
            OzWeather.Enabled = false;
            bool refused = !OzWeather.Force(40f);
            yield return 4.5f;
            P(refused && OzWeather.I.Rain < 0.02f, $"option OFF: a forced storm refused ({refused}), the rain gone in 4.5 s (rain {OzWeather.I.Rain:0.00})");
            OzWeather.Enabled = opt0;
            rep.AppendLine("     the option put back to " + (opt0 ? "ON" : "OFF") + "; " + OzWeather.I.Status());
        }

        [MenuItem(M + "3 Ripple clock (Play, any moment)", false, 7702)]
        static void ClockMenu() => Start(ClockBody(), "water_fx_clock.txt");

        static IEnumerator ClockBody()
        {
            for (int k = 0; k < 2; k++)
            {
                float t0 = Time.time; double r0 = EditorApplication.timeSinceStartup; int f0 = Time.frameCount;
                var tv0 = Shader.GetGlobalVector("_Time");
                yield return 0.5;
                float dt = Time.time - t0; double dr = EditorApplication.timeSinceStartup - r0; int df = Time.frameCount - f0;
                var tv1 = Shader.GetGlobalVector("_Time");
                bool brief = ZodFlyover.Live != null && ZodFlyover.Live.Active;
                P(dt > 0.05f, $"sample {k + 1}: game time (the water's clock) +{dt:0.000} s in {dr:0.000} s of real time, {df} frames; time scale {Time.timeScale:0.##}, capture rate {Time.captureFramerate}, " +
                              $"briefing flying {brief}, shader _Time.y {tv0.y:0.00} -> {tv1.y:0.00}");
            }
            rep.AppendLine("     the ripples run on game time: they stand still only while the game's time stands (the pause menu, the tactical pause), with the rest of the water");
        }

        [MenuItem(M + "4 Splash test (Play, water in view)", false, 7703)]
        static void SplashMenu() => Start(SplashBody(), "water_fx_splash.txt");

        [MenuItem(M + "5 Splash look: toggle new / as before", false, 7704)]
        static void ToggleSplash() { OzWaterSplash.On = !OzWaterSplash.On; Debug.Log(Tag + " " + OzWaterSplash.Status()); }

        static IEnumerator SplashBody()
        {
            var rts = Object.FindFirstObjectByType<RtsCamera>();
            Vector3 c = rts ? rts.focus : Vector3.zero; Vector3 at = c; bool found = false; float wy = 0f;
            for (float r = 0f; r <= 40f && !found; r += 2f)
                for (int a = 0; a < (r == 0f ? 1 : 16) && !found; a++)
                {
                    var p = c + new Vector3(Mathf.Cos(a * 0.3927f), 0f, Mathf.Sin(a * 0.3927f)) * r;
                    if (OzWaterLanding.OpenWaterAt(p, out wy)) { at = new Vector3(p.x, wy, p.z); found = true; }
                }
            P(found, found ? $"open water at ({at.x:0.0}, {at.y:0.00}, {at.z:0.0}), {(at - c).magnitude:0.0} m from the camera's focus" : "no open water within 40 m of the camera's focus: move the camera over water");
            if (!found) yield break;
            var tint = OpenZFxCatalog.RippleColorFor(ZGame.I ? ZGame.I.level : null);
            int j0 = OzWaterSplash.Jets, p0 = OzWaterSplash.PieceSplashes;
            bool jet = OzWaterSplash.Jet(at, 1f, 0f, tint);
            yield return 3f;
            int k = 0;
            foreach (var size in new[] { OzWaterSize.Small, OzWaterSize.Medium, OzWaterSize.Large })
            {   // the game's own way in (OzWaterLanding: droplets, rings, sound of the size); with the look toggled off it draws as before
                var p = at + Vector3.right * (3f * ++k);
                if (OzWaterLanding.OpenWaterAt(p, out float y2)) OzWaterLanding.Splash(new Vector3(p.x, y2, p.z), size, "water fx test", 6f);
                yield return 2.5f;
            }
            int pieces = OzWaterSplash.PieceSplashes - p0;
            P(jet == OzWaterSplash.On && (OzWaterSplash.On ? pieces >= 3 : pieces == 0), $"splashes drawn by the new look: column {(jet ? 1 : 0)}, piece splash calls {pieces} (look {(OzWaterSplash.On ? "NEW" : "AS BEFORE: the column is not drawn, the pieces in the old look")}); jets +{OzWaterSplash.Jets - j0}");
            rep.AppendLine("     " + OzWaterSplash.Status());
        }
    }
}
