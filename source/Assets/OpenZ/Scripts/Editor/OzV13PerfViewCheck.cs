using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// v1.3 item 25 (view, drawing and feel half): the counted checks that the speed-up changes nothing seen or played, and that the
    /// new Options rows start on today's behaviour. Menus under OpenZ/V1.3/Perf:
    ///  1 Offline math (no Play)            the nearest-N picks against LINQ OrderBy().Take() on random input with ties; the wheel notch
    ///                                      table (one notch = one 15 % step as before); the pan clock (same distance, even steps).
    ///  2 View equivalence (Play, 120 s)    each frame ZGame.LiveViews against Views filtered the old way; every 0.5 s the three
    ///                                      registries against FindObjectsByType; every 2 s DebrisAudit's bodies (registry against the
    ///                                      old whole-scene search). Run it in a bot match, ideally through a level ending.
    ///  3 Options defaults (clean profile)  with no saved value every new row reads today's value and changes nothing.
    ///  4 / 5 GPU Resident Drawer on / off  the URP assets and the BatchRendererGroup variants setting (edit mode, saved).
    ///  6 GPU Resident Drawer A-B frames    Play, game paused: the same view with the drawer off, off again (the noise floor of the
    ///                                      look itself: fog jitter and the like) and on, at the 70 / 35 / 200 m heights; PNGs + table.
    ///  7 Heat reading                      the in-game driver reading against one nvidia-smi reading (Windows, NVIDIA).
    /// Every run writes one line "[OpenZ v1.3 perf view] PASS / FAIL ..." to the console and to checks.txt in the output folder.
    /// </summary>
    public static class OzV13PerfViewCheck
    {
        const string Tag = "[OpenZ v1.3 perf view]";
        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/v1_3/item25_perf_view"));
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static int waitFrame, ok, bad; static float waitUntil;
        static readonly StringBuilder fails = new StringBuilder(), table = new StringBuilder();

        static void Check(bool pass, string what) { if (pass) ok++; else { bad++; fails.Append(" | ").Append(what); } Debug.Log($"{Tag} {(pass ? "ok  " : "FAIL")} {what}"); }
        static void Begin() { ok = bad = 0; fails.Clear(); table.Clear(); Directory.CreateDirectory(Dir); }
        static void Finish(string what)
        {
            bool pass = bad == 0 && ok > 0;
            string line = $"{Tag} {(pass ? "PASS" : "FAIL")} {ok}/{ok + bad} {what}{(bad == 0 ? "" : "; FAILED:" + fails)}";
            if (pass) Debug.Log(line); else Debug.LogError(line);
            File.AppendAllText(Path.Combine(Dir, "checks.txt"), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " " + line + "\n");
            if (table.Length > 0) File.WriteAllText(Path.Combine(Dir, what.Split(' ')[0] + "_table.md"), table.ToString());
        }
        static void Run(IEnumerator e) { stack.Clear(); stack.Push(e); waitFrame = 0; waitUntil = 0f; EditorApplication.update -= Step; EditorApplication.update += Step; }
        static void Step()
        {
            if (stack.Count == 0 || !EditorApplication.isPlaying) { stack.Clear(); EditorApplication.update -= Step; return; }
            if (Time.frameCount < waitFrame || Time.realtimeSinceStartup < waitUntil) return;
            var top = stack.Peek(); bool more;
            try { more = top.MoveNext(); }
            catch (System.Exception ex) { Debug.LogException(ex); stack.Clear(); EditorApplication.update -= Step; Debug.LogError(Tag + " FAIL: exception " + ex.Message); return; }
            if (!more) { stack.Pop(); if (stack.Count == 0) EditorApplication.update -= Step; return; }
            if (top.Current is float s) waitUntil = Time.realtimeSinceStartup + s;
            else waitFrame = Time.frameCount + (top.Current is int n ? n : 1);
        }

        // ------------------------------------------------------------------ 1 offline math
        [MenuItem("OpenZ/V1.3/Perf/1 Offline math (no Play)")]
        public static void OfflineMath()
        {
            Begin();
            // nearest-N against LINQ: random lengths 0..40, capacities 0..5, keys from a small set (many ties), a few NaN and infinities
            var rnd = new System.Random(25); int trials = 100000, wrong = 0;
            float[] pool = { 0f, 1f, 1f, 2f, 3.5f, 3.5f, 7f, float.PositiveInfinity, float.NaN, -1f };
            for (int t = 0; t < trials; t++)
            {
                int len = rnd.Next(41), cap = rnd.Next(6);
                var keys = new float[len]; for (int i = 0; i < len; i++) keys[i] = rnd.Next(4) == 0 ? (float)rnd.NextDouble() : pool[rnd.Next(pool.Length)];
                var want = Enumerable.Range(0, len).OrderBy(i => keys[i]).Take(cap).ToArray();
                var best = new int[cap]; var bk = new float[cap]; int n = 0;
                for (int i = 0; i < len; i++) n = OzNearestN.Insert(best, bk, n, i, keys[i]);
                if (n != want.Length) { wrong++; continue; }
                for (int i = 0; i < n; i++) if (best[i] != want[i]) { wrong++; break; }
            }
            Check(wrong == 0, $"N1 nearest-N = OrderBy().Take(): {trials} random streams with ties / NaN, {wrong} differ (must be 0)");
            // the wheel: one notch of a normal wheel = one step as before; every notch counts; a touchpad zooms by what it scrolled
            table.AppendLine("| scroll in one frame | per notch | before (steps) | now (steps) |").AppendLine("|---|---|---|---|");
            var cases = new (float wheel, float per, float before, float now)[]
            {
                (1f, 1f, 1f, 1f), (-1f, 1f, -1f, -1f), (120f, 120f, 1f, 1f), (2f, 1f, 1f, 2f), (3f, 1f, 1f, 3f), (5f, 1f, 1f, 3f),
                (0.3f, 1f, 1f, 0.3f), (0.015f, 1f, 1f, 0f), (240f, 120f, 2f, 2f), (30f, 120f, 0.25f, 0.25f),   // review: 1.2 kept 30 / 120 = 0.25 (only under 0.05 became a full step)
            };
            int wheelBad = 0;
            foreach (var c in cases)
            {
                float old = OldNotches(c.wheel), now = Mathf.Abs(c.wheel) > 0.01f ? OzCamFeel.WheelNotches(c.wheel, c.per, OzTrackpadMath.MaxNotchesPerFrame) : 0f;
                bool okRow = Mathf.Abs(old - c.before) < 1e-5f && Mathf.Abs(now - c.now) < 1e-5f; if (!okRow) wheelBad++;
                table.AppendLine($"| {c.wheel} | {c.per} | {old:0.###} | {now:0.###} |");
            }
            Check(wheelBad == 0, $"W1 wheel table: {cases.Length} rows, {wheelBad} off; one notch = one 15 % step before and now (rows 1-3)");
            // the pan clock: frame times swinging 70 / 80 ms (7 / 8 rule ticks), 300 frames: the distance and the steps
            var clk = new OzCamFeel.PanClock(); double sumOld = 0, sumNew = 0; float mnO = 9, mxO = 0, mnN = 9, mxN = 0;
            for (int i = 0; i < 300; i++)
            {
                float dt = i % 2 == 0 ? 0.07f : 0.08f, s = clk.Step(dt, RtsCamera.PanAverageFrames);
                sumOld += dt; sumNew += s;
                if (i >= 8) { mnO = Mathf.Min(mnO, dt); mxO = Mathf.Max(mxO, dt); mnN = Mathf.Min(mnN, s); mxN = Mathf.Max(mxN, s); }
            }
            table.AppendLine().AppendLine($"Pan clock over 300 frames of 70 / 80 ms: distance before {sumOld:0.0000} s of pan, now {sumNew:0.0000} s; step spread before {(mxO - mnO) * 1000f:0.0} ms, now {(mxN - mnN) * 1000f:0.0} ms");
            Check(System.Math.Abs(sumOld - sumNew) < 0.08 && (mxN - mnN) < 0.25f * (mxO - mnO), $"P1 pan clock: the same distance ({sumOld:0.000} / {sumNew:0.000} s) and even steps (spread {(mxO - mnO) * 1000f:0.0} -> {(mxN - mnN) * 1000f:0.0} ms)");
            var steady = new OzCamFeel.PanClock(); float st = 0f; for (int i = 0; i < 10; i++) st = steady.Step(1f / 60f, RtsCamera.PanAverageFrames);
            Check(Mathf.Abs(st - 1f / 60f) < 1e-6f, $"P2 at an even 60 fps the pan step is the frame time itself ({st * 1000f:0.0000} ms)");
            Finish("offline_math");
        }
        /// <summary>The wheel code of 1.2 (RtsCamera.cs:187-193), for the table.</summary>
        static float OldNotches(float wheel)
        {
            if (Mathf.Abs(wheel) <= 0.01f) return 0f;
            float n = Mathf.Clamp(wheel / 120f, -3f, 3f);
            if (Mathf.Abs(n) < 0.05f) n = Mathf.Sign(wheel);
            return n;
        }

        // ------------------------------------------------------------------ 2 view equivalence (Play)
        [MenuItem("OpenZ/V1.3/Perf/2 View equivalence (Play, 120 s)")]
        public static void ViewEquivalence()
        {
            if (!EditorApplication.isPlaying || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning(Tag + " enter Play mode in a level first (a bot match is best)"); return; }
            Begin(); Run(Equivalence(120f));
        }
        static IEnumerator Equivalence(float seconds)
        {
            var g = ZGame.I; float t0 = Time.realtimeSinceStartup, nextReg = 0f, nextDeb = 0f;
            int frames = 0, liveBad = 0, regRuns = 0, regMiss = 0, regExtra = 0, debRuns = 0, debMiss = 0, debExtra = 0, maxLive = 0, maxViews = 0;
            var refKeys = new List<int>(); var missNames = new SortedDictionary<string, int>();
            int rebuild0 = g.LiveRebuilds;
            while (Time.realtimeSinceStartup - t0 < seconds && g && g.World != null)
            {
                refKeys.Clear(); foreach (var p in g.Views) if (p.Value && p.Value.isActiveAndEnabled) refKeys.Add(p.Key);
                var live = g.LiveViews; var liveV = g.LiveViewValues; bool same = live.Count == refKeys.Count && liveV.Count == refKeys.Count;
                for (int i = 0; same && i < refKeys.Count; i++) same = live[i].Key == refKeys[i] && live[i].Value == g.Views[refKeys[i]] && liveV[i] == live[i].Value;
                if (!same) liveBad++;
                frames++; maxLive = Mathf.Max(maxLive, refKeys.Count); maxViews = Mathf.Max(maxViews, g.Views.Count);
                float now = Time.realtimeSinceStartup;
                if (now >= nextReg)
                {
                    nextReg = now + 0.5f; regRuns++;
                    Diff(OzLiveSet<RobotRagdoll>.All, ref regMiss, ref regExtra);
                    Diff(OzLiveSet<BuildingBreaker.Piece>.All, ref regMiss, ref regExtra);
                    Diff(OzLiveSet<RockChunkLife>.All, ref regMiss, ref regExtra);
                }
                if (now >= nextDeb)
                {
                    nextDeb = now + 2f; debRuns++;
                    int layer = BuildingBreaker.DebrisLayer;
                    var old = new HashSet<Rigidbody>();
                    foreach (var rb in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                        if (rb && rb.gameObject.layer == layer || (rb && rb.GetComponentInChildren<Collider>() is Collider c && c.gameObject.layer == layer)) old.Add(rb);
                    var reg = new HashSet<Rigidbody>();
                    foreach (var rb in OzDebrisBodies.All)
                        if (rb && rb.gameObject.activeInHierarchy && (rb.gameObject.layer == layer || (rb.GetComponentInChildren<Collider>() is Collider c && c.gameObject.layer == layer))) reg.Add(rb);
                    foreach (var rb in old) if (!reg.Contains(rb)) { debMiss++; string k = rb.transform.root.name + "/" + rb.name; missNames.TryGetValue(k, out int m); missNames[k] = m + 1; }
                    foreach (var rb in reg) if (!old.Contains(rb)) debExtra++;
                }
                yield return 1;
            }
            Check(frames > 100 && liveBad == 0, $"E1 LiveViews = Views filtered the old way, in the same order: {frames} frames, {liveBad} differ (views up to {maxViews}, shown up to {maxLive}, rebuilds {g.LiveRebuilds - rebuild0})");
            Check(regRuns > 0 && regMiss == 0 && regExtra == 0, $"E2 ragdoll / building cell / rock chunk registries = the whole-scene search: {regRuns} looks, {regMiss} missing, {regExtra} extra");
            Check(debRuns > 0 && debMiss == 0 && debExtra == 0, $"E3 DebrisAudit bodies: registry = old search: {debRuns} looks, {debMiss} missing, {debExtra} extra" + (missNames.Count > 0 ? " (missing: " + string.Join(", ", missNames.Take(12).Select(kv => kv.Key + " x" + kv.Value)) + "; set DebrisAudit.UseRegistry = false until each has its OzDebrisBodies.Add line)" : ""));
            Finish("view_equivalence " + (g ? g.level : "?"));
        }
        static void Diff<T>(HashSet<T> reg, ref int miss, ref int extra) where T : Component
        {
            var scan = Object.FindObjectsByType<T>(FindObjectsSortMode.None); var s = new HashSet<T>(scan);
            foreach (var x in scan) if (!reg.Contains(x)) miss++;
            foreach (var x in reg) if (x && x.gameObject.activeInHierarchy && !s.Contains(x)) extra++;
        }

        // ------------------------------------------------------------------ 3 options defaults
        static readonly string[] NewKeys = { OzFpsCap.CapPref, OzSmoothFrames.Pref, OzEndingDebris.Pref, OzSunSteps.Pref, OzGfxApi.Pref };
        [MenuItem("OpenZ/V1.3/Perf/3 Options defaults (clean profile)")]
        public static void OptionsDefaults()
        {
            Begin();
            var kept = new Dictionary<string, int>(); foreach (var k in NewKeys) if (PlayerPrefs.HasKey(k)) kept[k] = PlayerPrefs.GetInt(k);
            try
            {
                foreach (var k in NewKeys) PlayerPrefs.DeleteKey(k);
                Check(OzFpsCap.Saved == 60 && OzFpsCap.Name == "60", $"D1 FRAME RATE LIMIT reads {OzFpsCap.Name} (60)");
                Check(!OzSmoothFrames.On && Mathf.Approximately(OzSmoothFrames.FrameStep, ZGame.MaxFrameStep), $"D2 SMOOTH FRAMES ON SLOW PCS off, frame step {OzSmoothFrames.FrameStep:0.00} s (0.10 as before)");
                Check(!OzEndingDebris.Light && OzEndingDebris.Name == "FULL", $"D3 ENDING DEBRIS {OzEndingDebris.Name}");
                Check(!OzSunSteps.Two && OzSunSteps.Name == "4", $"D4 SUN SHADOW STEPS {OzSunSteps.Name}");
                Check(!OzGfxApi.Dx11 && OzGfxApi.Name == "DX12", $"D5 GRAPHICS API {OzGfxApi.Name}");
                var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                int c0 = urp ? urp.shadowCascadeCount : -1; OzSunSteps.Apply();
                Check(OzSunSteps.Touched == null && (urp ? urp.shadowCascadeCount : -1) == c0, $"D6 SUN SHADOW STEPS on 4 changes nothing: cascades {c0} -> {(urp ? urp.shadowCascadeCount : -1)}");
                if (EditorApplication.isPlaying && ZGame.I)
                    Check(Mathf.Approximately(Time.maximumDeltaTime, ZGame.MaxFrameStep) || PlayerPrefs.GetInt(OzSmoothFrames.Pref, 0) == 1 || kept.ContainsKey(OzSmoothFrames.Pref),
                          $"D7 in this match the largest frame step is {Time.maximumDeltaTime:0.00} s");
                if (EditorApplication.isPlaying && !GameBoot.TestMode && GameBoot.CapFps <= 60 && !kept.ContainsKey(OzFpsCap.CapPref))
                    Check(QualitySettings.vSyncCount == 0 && Application.targetFrameRate == 60 && !OzFpsCap.Vsync, $"D8 vsync {QualitySettings.vSyncCount}, frame cap {Application.targetFrameRate} (0 / 60 as before)");
            }
            finally
            {
                foreach (var k in NewKeys) { if (kept.TryGetValue(k, out int v)) PlayerPrefs.SetInt(k, v); else PlayerPrefs.DeleteKey(k); }
                PlayerPrefs.Save();
            }
            Finish("options_defaults");
        }

        // ------------------------------------------------------------------ 4 / 5 GPU Resident Drawer settings
        static readonly string[] UrpAssets = { "Assets/Settings/PC_RPAsset.asset", "Assets/OpenZ/Resources/OpenZ/Quality/OZ_URP_Medium.asset", "Assets/OpenZ/Resources/OpenZ/Quality/OZ_URP_Low.asset" };
        [MenuItem("OpenZ/V1.3/Perf/4 GPU Resident Drawer: switch on (edit mode)")]
        public static void DrawerOn() => SetDrawer(true);
        [MenuItem("OpenZ/V1.3/Perf/5 GPU Resident Drawer: switch off (edit mode)")]
        public static void DrawerOff() => SetDrawer(false);
        static void SetDrawer(bool on)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning(Tag + " leave Play mode first (the settings files are saved)"); return; }
            var sb = new StringBuilder();
            foreach (var path in UrpAssets)
            {
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (!a) { sb.Append(path).Append(": missing; "); continue; }
                a.gpuResidentDrawerMode = on ? GPUResidentDrawerMode.InstancedDrawing : GPUResidentDrawerMode.Disabled;
                a.smallMeshScreenPercentage = 0f;                                                           // no small-object culling: the same objects drawn
                a.gpuResidentDrawerEnableOcclusionCullingInCameras = false;                                 // no GPU occlusion culling: the same objects drawn
                EditorUtility.SetDirty(a);
                sb.Append(a.name).Append(" ").Append(a.gpuResidentDrawerMode).Append("; ");
            }
            if (on && EditorGraphicsSettings.batchRendererGroupShaderStrippingMode != BatchRendererGroupStrippingMode.KeepAll)
            {
                // an assignment), so the Graphics settings asset is written the way its inspector writes it: m_BrgStripping 2 = Keep All
                var gs = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
                var brg = gs.FindProperty("m_BrgStripping");
                if (brg != null) { brg.intValue = (int)BatchRendererGroupStrippingMode.KeepAll; gs.ApplyModifiedPropertiesWithoutUndo(); }
                else Debug.LogError(Tag + " m_BrgStripping not found in the Graphics settings: set Project Settings > Graphics > BatchRendererGroup Variants to Keep All by hand");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} GPU Resident Drawer {(on ? "ON" : "OFF")}: {sb}BatchRendererGroup variants {EditorGraphicsSettings.batchRendererGroupShaderStrippingMode} (switching it off leaves the variants kept)");
        }

        // ------------------------------------------------------------------ 6 GPU Resident Drawer A-B frames (Play)
        [MenuItem("OpenZ/V1.3/Perf/6 GPU Resident Drawer A-B frames (Play)")]
        public static void DrawerFrames()
        {
            if (!EditorApplication.isPlaying || !ZGame.I || !Object.FindFirstObjectByType<RtsCamera>()) { Debug.LogWarning(Tag + " enter Play mode in a level first, with the RTS camera (no briefing flight)"); return; }
            Begin(); Run(Frames());
        }
        static IEnumerator Frames()
        {
            var g = ZGame.I; var rts = Object.FindFirstObjectByType<RtsCamera>(); var cam = rts.GetComponent<Camera>(); if (!cam) cam = Camera.main;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!urp || !cam) { Check(false, "no URP asset or camera"); Finish("drawer_frames"); yield break; }
            var mode0 = urp.gpuResidentDrawerMode; bool paused0 = g.paused; float ts0 = Time.timeScale, h0 = rts.height;
            var cd = cam.GetComponent<UniversalAdditionalCameraData>(); var aa0 = cd ? cd.antialiasing : AntialiasingMode.None;
            g.paused = true; Time.timeScale = 0f; if (cd) cd.antialiasing = AntialiasingMode.None;           // a frozen picture; no frame-to-frame AA history in the comparison
            int w = Screen.width, h = Screen.height;
            table.AppendLine($"# GPU Resident Drawer A-B, {g.level}, {w}x{h}, preset {OzQuality.Name}").AppendLine()
                 .AppendLine("| height | mean diff A-A' | mean diff A-B | 99.9 % A-A' | 99.9 % A-B | pixels > 8/255 A-A' | pixels > 8/255 A-B | result |").AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (float height in new[] { 70f, 35f, 200f })
            {
                rts.SetHeightImmediate(height); yield return 20;
                urp.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled; yield return 20;
                var a = Grab(cam, w, h); yield return 10; var a2 = Grab(cam, w, h);
                urp.gpuResidentDrawerMode = GPUResidentDrawerMode.InstancedDrawing; yield return 30;
                var b = Grab(cam, w, h);
                var aa = Stats(a, a2); var ab = Stats(a, b);
                bool pass = ab.mean <= aa.mean + 0.1f && ab.p999 <= aa.p999 + 2 && ab.big <= aa.big + w * h / 10000;
                table.AppendLine($"| {height:0} m | {aa.mean:0.000} | {ab.mean:0.000} | {aa.p999} | {ab.p999} | {aa.big} | {ab.big} | {(pass ? "same" : "DIFFERENT")} |");
                string stem = Path.Combine(Dir, $"grd_{g.level}_{height:0}m");
                File.WriteAllBytes(stem + "_A_off.png", a.EncodeToPNG()); File.WriteAllBytes(stem + "_B_on.png", b.EncodeToPNG()); File.WriteAllBytes(stem + "_diff_x8.png", DiffImage(a, b).EncodeToPNG());
                Check(pass, $"G {g.level} {height:0} m: the drawer on differs from off no more than the picture differs from itself (mean {ab.mean:0.000} vs {aa.mean:0.000}, 99.9 % {ab.p999} vs {aa.p999}, big {ab.big} vs {aa.big})");
                Object.DestroyImmediate(a); Object.DestroyImmediate(a2); Object.DestroyImmediate(b);
            }
            urp.gpuResidentDrawerMode = mode0; rts.SetHeightImmediate(h0); if (cd) cd.antialiasing = aa0; Time.timeScale = ts0; g.paused = paused0;
            Finish("drawer_frames " + g.level);
        }
        static Texture2D Grab(Camera cam, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var was = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = was;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(false);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            return tex;
        }
        static (float mean, int p999, int big) Stats(Texture2D x, Texture2D y)
        {
            var a = x.GetPixels32(); var b = y.GetPixels32(); var hist = new int[256]; double sum = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int d = Mathf.Max(Mathf.Abs(a[i].r - b[i].r), Mathf.Max(Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)));
                hist[d]++; sum += d;
            }
            int big = 0; for (int d = 9; d < 256; d++) big += hist[d];
            int p = 0, acc = 0, lim = (int)(a.Length * 0.999); for (; p < 256; p++) { acc += hist[p]; if (acc >= lim) break; }
            return ((float)(sum / Mathf.Max(1, a.Length)), p, big);
        }
        static Texture2D DiffImage(Texture2D x, Texture2D y)
        {
            var a = x.GetPixels32(); var b = y.GetPixels32(); var o = new Color32[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                int d = Mathf.Max(Mathf.Abs(a[i].r - b[i].r), Mathf.Max(Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)));
                byte v = (byte)Mathf.Min(255, d * 8); o[i] = new Color32(v, v, v, 255);
            }
            var t = new Texture2D(x.width, x.height, TextureFormat.RGBA32, false); t.SetPixels32(o); t.Apply(false);
            return t;
        }

        // ------------------------------------------------------------------ 7 heat reading
        [MenuItem("OpenZ/V1.3/Perf/7 Heat reading: driver vs nvidia-smi")]
        public static void HeatReading()
        {
            Begin();
            int drv = -1, smi = -1;
            if (OzNvml.Open()) { OzNvml.Read(out drv); OzNvml.Close(); }
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("nvidia-smi", "--query-gpu=temperature.gpu --format=csv,noheader") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
                using (var p = System.Diagnostics.Process.Start(psi)) { string o = p.StandardOutput.ReadToEnd(); p.WaitForExit(3000); int.TryParse(o.Trim().Split('\n')[0].Trim(), out smi); }
            }
            catch (System.Exception e) { Debug.LogWarning(Tag + " nvidia-smi: " + e.Message); }
            Check(drv >= 0 && smi >= 0 && Mathf.Abs(drv - smi) <= 1, $"H1 GPU temperature: driver library {drv} C, nvidia-smi {smi} C (within 1 C)");
            Finish("heat_reading");
        }
    }
}
