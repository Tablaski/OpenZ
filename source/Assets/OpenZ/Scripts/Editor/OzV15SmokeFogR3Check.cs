using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit smoke_fog_r3: the check (no Play) and the frame pairs (Play) of "near smoke in front of the far fog".
    /// Every frame is drawn TWICE in the same game frame through a render request of the camera (same smoke, same light): BEFORE =
    /// OzSmokeFog.On false (the fog pass exactly as before the kit), AFTER = on; the diagnosis set draws the same frame with one step of the
    /// picture switched off at a time (the fog pass, its horizon band, its march, URP's own fog on the smoke, the explosions' column rule).</summary>
    public static class OzV15SmokeFogR3Check
    {
        const string Menu = "OpenZ/V1.5/Smoke Fog R3/";
        const string Tag = "[SmokeFogR3]";
        public const string Out = "../work/staging/v1_5/owner_frames/smoke_fog_r3/";
        public static readonly string[] Diag = { "before", "after", "atmo_off", "horizon_off", "march_off", "urpfog_off", "before_noshape", "after_noshape" };
        public static string LastReport = "";
        /// <summary>The border the border frames look out over (N, E, S, W; W = toward level 18's evening sun).</summary>
        public static char Edge = 'W';
        public static bool Busy;
        static readonly string[] Names = { "COLUMN", "FUEL", "CHAIN", "GEYSER", "FLAK", "SHOCK", "AIRBURST" };

        // ------------------------------------------------------------------------------------------------ 1 the check (no Play)
        [MenuItem(Menu + "1 Check (no Play)")]
        public static void Check() { Debug.Log(RunCheck()); }

        public static string RunCheck()
        {
            var sb = new StringBuilder(); int pass = 0, fail = 0;
            void Line(bool ok, string what) { sb.AppendLine((ok ? "PASS " : "FAIL ") + what); if (ok) pass++; else fail++; }
            var tag = new ShaderTagId("LightMode");
            foreach (var n in new[] { "OpenZ/FxAdditive", "OpenZ/FxBlast", "OpenZ/FxLit", "OpenZ/Nuke", "OpenZ/PyroFlame", "OpenZ/PyroTongues", "OpenZ/SoftTrail" })
            {
                var s = Shader.Find(n); bool fwd = false, dist = false; int passes = 0;
                if (s)
                {
                    passes = s.passCount;
                    for (int i = 0; i < passes; i++)
                    {
                        var v = s.FindPassTagValue(i, tag).name;
                        if (v == "UniversalForward") fwd = true;
                        if (v == "OzFxDist") dist = true;
                    }
                }
                bool err = s && ShaderUtil.ShaderHasError(s);
                Line(s && s.isSupported && fwd && dist && !err, $"{n}: found {(bool)s}, supported {(s && s.isSupported)}, {passes} passes, forward pass {fwd}, OzFxDist pass {dist}, compile errors {err}");
            }
            var atm = Shader.Find("OpenZ/AtmosphereFullscreen");
            string atmPath = atm ? AssetDatabase.GetAssetPath(atm) : "";
            string atmText = atmPath.Length > 0 && File.Exists(atmPath) ? File.ReadAllText(atmPath) : "";
            Line(atm && atm.isSupported && atmText.Contains("_OzFxDistTex") && atmText.Contains("fxA"), $"the fog pass {atmPath} reads the smoke's own distance (_OzFxDistTex), supported {(atm && atm.isSupported)}");
            Line(File.Exists("Assets/OpenZ/Shaders/OzFxDist.hlsl"), "Assets/OpenZ/Shaders/OzFxDist.hlsl is there");
            Line(OzSmokeFog.On, "OzSmokeFog.On (the game keeps it on)");
            // the atmosphere is a Full Screen Pass at BeforeRenderingPostProcessing: after the kit's pass (AfterRenderingTransparents)
            int feats = 0, early = 0;
            var rp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            foreach (var g in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(g));
                if (!data) continue;
                foreach (var f in data.rendererFeatures)
                {
                    if (!(f is FullScreenPassRendererFeature fs) || !fs.passMaterial || fs.passMaterial.shader != atm) continue;
                    feats++;
                    if (fs.injectionPoint != FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing) early++;
                }
            }
            Line(feats > 0 && early == 0, $"the fog pass runs as a Full Screen Pass in {feats} renderer(s), every one at BeforeRenderingPostProcessing (after the kit's smoke distance pass): {early} earlier");
            Line(rp != null, "the project's pipeline is URP (" + (rp ? rp.name : "none") + ")");
            sb.Insert(0, $"{Tag} CHECK {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}\n");
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/smoke_fog_r3_check.txt", sb.ToString());
            LastReport = sb.ToString();
            return LastReport;
        }

        // ------------------------------------------------------------------------------------------------ drawing one frame
        static readonly int IdAtmo = Shader.PropertyToID("_AtmoEnabled"), IdHS = Shader.PropertyToID("_HorizonStart"), IdHE = Shader.PropertyToID("_HorizonEnd"),
            IdDen = Shader.PropertyToID("_FogDensity"), IdLow = Shader.PropertyToID("_LowFogDensity"), IdCol = Shader.PropertyToID("_AtmoColumn"),
            IdCap = Shader.PropertyToID("_AtmoColumnCap"), IdTop = Shader.PropertyToID("_AtmoColumnTop");
        static string variant = "";

        static void Override(ScriptableRenderContext ctx, Camera cam)
        {   // runs after every other beginCameraRendering handler (added last): one step of the picture switched off for this one render
            switch (variant)
            {
                case "atmo_off": Shader.SetGlobalFloat(IdAtmo, 0f); break;
                case "horizon_off": Shader.SetGlobalFloat(IdHS, 1e7f); Shader.SetGlobalFloat(IdHE, 2e7f); break;
                case "march_off": Shader.SetGlobalFloat(IdDen, 0f); Shader.SetGlobalFloat(IdLow, 0f); break;
                case "before_noshape": case "after_noshape": Shader.SetGlobalVector(IdCol, Vector4.zero); Shader.SetGlobalVector(IdCap, Vector4.zero); Shader.SetGlobalFloat(IdTop, 0f); break;
            }
        }

        /// <summary>Stills only (13 Nov, resumed): the camera-motion part of the game's motion blur off while a still is drawn. The briefing
        /// stage runs on its own real-time clock and a batch run draws a few frames a second, so one drawn frame held the camera motion of
        /// many game frames and the stills came out smeared (the owner's game at 60+ fps shows them sharp: the recorded briefing takes).
        /// One temporary global volume (highest priority, on the camera's volume layer, created at run time, never saved) with motion blur
        /// intensity 0; nothing of the game changes.</summary>
        public static bool StillsSharp = true;
        static Volume noCamBlur;
        static void SharpOn(Camera cam, bool on)
        {
            if (!on) { if (noCamBlur) noCamBlur.enabled = false; return; }
            if (!noCamBlur)
            {
                var go = new GameObject("ZZ_SmokeFogR3_StillsSharp") { hideFlags = HideFlags.HideAndDontSave };
                noCamBlur = go.AddComponent<Volume>(); noCamBlur.isGlobal = true; noCamBlur.priority = 100000f;
                var pr = ScriptableObject.CreateInstance<VolumeProfile>(); pr.hideFlags = HideFlags.HideAndDontSave;
                var mb = pr.Add<MotionBlur>(true); mb.intensity.Override(0f);
                noCamBlur.sharedProfile = pr;
            }
            int mask = cam && cam.TryGetComponent<UniversalAdditionalCameraData>(out var d) ? (int)d.volumeLayerMask : 1;
            int layer = 0; for (int i = 0; i < 32; i++) if ((mask & (1 << i)) != 0) { layer = i; break; }
            noCamBlur.gameObject.layer = layer; noCamBlur.enabled = true;
        }

        /// <summary>The camera drawn once into a new texture (sRGB bytes) with the variant's switches; every global put back after.</summary>
        public static Texture2D Shot(Camera cam, int w, int h, string v)
        {
            if (!cam) return null;
            SharpOn(cam, StillsSharp);
            bool on0 = OzSmokeFog.On, fog0 = RenderSettings.fog;
            float atmo0 = Shader.GetGlobalFloat(IdAtmo), hs0 = Shader.GetGlobalFloat(IdHS), he0 = Shader.GetGlobalFloat(IdHE), den0 = Shader.GetGlobalFloat(IdDen), low0 = Shader.GetGlobalFloat(IdLow), top0 = Shader.GetGlobalFloat(IdTop);
            Vector4 col0 = Shader.GetGlobalVector(IdCol), cap0 = Shader.GetGlobalVector(IdCap);
            var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            Texture2D tex = null;
            try
            {
                OzSmokeFog.On = v == "after" || v == "after_noshape";
                if (v == "urpfog_off") RenderSettings.fog = false;
                variant = v;
                RenderPipelineManager.beginCameraRendering -= Override; RenderPipelineManager.beginCameraRendering += Override;
                var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (!RenderPipeline.SupportsRenderRequest(cam, req)) { Debug.LogWarning($"{Tag} the camera {cam.name} takes no render request"); return null; }
                RenderPipeline.SubmitRenderRequest(cam, req);
                var was = RenderTexture.active; RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                RenderTexture.active = was;
            }
            finally
            {
                RenderPipelineManager.beginCameraRendering -= Override; variant = "";
                SharpOn(cam, false);
                OzSmokeFog.On = on0; RenderSettings.fog = fog0;
                Shader.SetGlobalFloat(IdAtmo, atmo0); Shader.SetGlobalFloat(IdHS, hs0); Shader.SetGlobalFloat(IdHE, he0); Shader.SetGlobalFloat(IdDen, den0);
                Shader.SetGlobalFloat(IdLow, low0); Shader.SetGlobalFloat(IdTop, top0); Shader.SetGlobalVector(IdCol, col0); Shader.SetGlobalVector(IdCap, cap0);
                RenderTexture.ReleaseTemporary(rt);
            }
            return tex;
        }

        public static void Save(Texture2D t, string path)
        {
            if (!t) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, t.EncodeToJPG(92)); Object.Destroy(t);
        }

        /// <summary>The given variants of the camera's current frame, saved as dir/prefix_variant.jpg.</summary>
        public static void Set(Camera cam, string dir, string prefix, int w, int h, params string[] vs)
        {
            foreach (var v in vs) Save(Shot(cam, w, h, v), dir + prefix + "_" + v + ".jpg");
        }

        // ------------------------------------------------------------------------------------------------ 2 the frames now (Play)
        [MenuItem(Menu + "2 Frame pair + diagnosis now (Play)")]
        static void Now()
        {
            if (!Application.isPlaying || !Camera.main) { Debug.LogWarning($"{Tag} enter Play first"); return; }
            string dir = Out + "manual_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + "/";
            Set(Camera.main, dir, "frame", 1920, 1080, Diag);
            Debug.Log($"{Tag} {Diag.Length} pictures of this frame in {dir} ({OzSmokeFog.Status()})");
        }

        // ------------------------------------------------------------------------------------------------ the level 18 briefing (Play, the briefing flying)
        /// <summary>From the briefing stage's clock `from` to `to` every `step` s: BEFORE / AFTER of the drone view (+ the diagnosis set every
        /// `diagEvery`-th picture). Started when a level 18 briefing begins (the batch proof starts it on deploy).</summary>
        public static IEnumerator BriefingFrames(string dir, string side, float from, float to, float step, int diagEvery, StringBuilder log)
        {
            Busy = true;
            int cf0 = Time.captureFramerate; Time.captureFramerate = 30;                                 // 1/30 s of game time a frame: the game's own motion blur, whatever the batch frame rate
            float wait0 = Time.realtimeSinceStartup;
            while (OzL18BriefingStage.Tau < from - 0.01f && Time.realtimeSinceStartup - wait0 < 900f) yield return null;
            int n = 0;
            for (float tau = from; tau <= to + 1e-3f; tau += step)
            {
                float w0 = Time.realtimeSinceStartup;
                while (OzL18BriefingStage.Tau < tau && OzL18BriefingStage.Tau > -50f && Time.realtimeSinceStartup - w0 < 300f) yield return null;
                if (OzL18BriefingStage.Tau <= -50f) { log?.AppendLine($"{side}: the stage ended before tau {tau:0.0}"); break; }
                var cam = Camera.main;
                if (!cam) { yield return null; continue; }
                string p = $"{side}_tau{OzL18BriefingStage.Tau:00.00}";
                var vs = n % Mathf.Max(1, diagEvery) == 0 ? Diag : new[] { "before", "after" };
                Object.Destroy(Shot(cam, 1920, 1080, "after"));                                        // a frame drawn first: the next frame's camera motion is one frame's (motion blur as in play)
                yield return null;
                if (!cam) continue;
                p = $"{side}_tau{OzL18BriefingStage.Tau:00.00}";
                Set(cam, dir, p, 1920, 1080, vs);
                log?.AppendLine($"{side} {p}: camera {cam.name} at ({cam.transform.position.x:0.0}, {cam.transform.position.y:0.0}, {cam.transform.position.z:0.0}) " +
                                $"fwd ({cam.transform.forward.x:0.00}, {cam.transform.forward.y:0.00}, {cam.transform.forward.z:0.00}), fov {cam.fieldOfView:0.0}, {vs.Length} pictures");
                n++;
                yield return null;
            }
            Time.captureFramerate = cf0;
            Busy = false;
        }

        // ------------------------------------------------------------------------------------------------ the map border with the seven explosions
        [MenuItem(Menu + "3 Border frames, day + dusk, the seven explosions (Play, a level)")]
        static void BorderMenu()
        {
            var g = ZGame.I;
            if (!Application.isPlaying || !g || g.World == null) { Debug.LogWarning($"{Tag} enter Play in a level first"); return; }
            var log = new StringBuilder();
            g.StartCoroutine(BorderFrames(g, Out + "border_" + g.level + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + "/", log));
        }

        /// <summary>The game camera at the map border `Edge` (low: 12 m up, 50 m inside, looking out; RTS: 50 m up, 62 m inside, pitch 30) at day and dusk,
        /// each of exp_v2_1..7 fired 14 m inside the edge: BEFORE / AFTER / AFTER without the column rule at 1.6 s and 3.2 s.</summary>
        public static IEnumerator BorderFrames(ZGame g, string dir, StringBuilder log)
        {
            Busy = true;
            var rts = Object.FindFirstObjectByType<RtsCamera>(); var cam = Camera.main; var dn = Object.FindFirstObjectByType<DayNight>();
            if (!cam) { log?.AppendLine("no game camera"); Busy = false; yield break; }
            bool bb = g.blueBot, rb = g.redBot; g.blueBot = false; g.redBot = false;
            float tod0 = dn ? dn.timeOfDay : 0f; bool run0 = dn && dn.running;
            if (rts) rts.enabled = false;
            int cf0 = Time.captureFramerate; Time.captureFramerate = 30;
            Vector2 map = rts ? rts.mapSizeMeters : new Vector2(256f, 240f);
            Vector3 mid = new Vector3(map.x * 0.5f, 0f, -map.y * 0.5f), edge, outward;
            switch (char.ToUpperInvariant(Edge))
            {
                case 'N': outward = Vector3.forward; edge = new Vector3(mid.x, 0f, 0f); break;
                case 'E': outward = Vector3.right; edge = new Vector3(map.x, 0f, mid.z); break;
                case 'S': outward = Vector3.back; edge = new Vector3(mid.x, 0f, -map.y); break;
                default: outward = Vector3.left; edge = new Vector3(0f, 0f, mid.z); break;
            }
            var at = edge - outward * 14f; at.y = g.GroundY(at.x, at.z);
            var look = Quaternion.LookRotation(outward, Vector3.up);
            var pLow = edge - outward * 50f; pLow.y = g.GroundY(pLow.x, pLow.z) + 12f;                  // over the border rock: the outer world and the far haze behind the blast
            var pRts = edge - outward * 62f; pRts.y = g.GroundY(pRts.x, pRts.z) + 50f;                  // the game camera's height, tilted up to the horizon at the border
            var views = new (string name, Vector3 pos, Quaternion rot)[]
            {
                ("low", pLow, look * Quaternion.Euler(4f, 0f, 0f)),
                ("rts", pRts, look * Quaternion.Euler(30f, 0f, 0f)),
            };
            log?.AppendLine($"border {Edge}: blast at ({at.x:0.0}, {at.y:0.0}, {at.z:0.0}), low camera ({pLow.x:0.0}, {pLow.y:0.0}, {pLow.z:0.0}) pitch 4, rts camera ({pRts.x:0.0}, {pRts.y:0.0}, {pRts.z:0.0}) pitch 30");
            for (int tdi = 0; tdi < 2; tdi++)
            {
                float tod = tdi == 0 ? 0.08f : 0.32f; string tn = tdi == 0 ? "day" : "dusk";
                if (dn) { dn.running = false; dn.timeOfDay = tod; dn.Apply(tod); }
                for (int type = 0; type < 7; type++)
                {
                    float w0 = Time.realtimeSinceStartup;
                    while (OzUeBlastFx.Live > 0 && Time.realtimeSinceStartup - w0 < 25f) { cam.transform.SetPositionAndRotation(views[0].pos, views[0].rot); yield return null; }
                    for (int i = 0; i < 10; i++) { cam.transform.SetPositionAndRotation(views[0].pos, views[0].rot); yield return null; }
                    bool ok = OzExpInGame.Play(type, at, 1f, 1f);
                    float t0 = Time.time;
                    log?.AppendLine($"{tn} exp_v2_{type + 1} {Names[type]}: fired {ok} at ({at.x:0.0}, {at.y:0.0}, {at.z:0.0})");
                    foreach (float when in new[] { 1.6f, 3.2f })
                    {
                        while (Time.time - t0 < when) { cam.transform.SetPositionAndRotation(views[0].pos, views[0].rot); yield return null; }
                        foreach (var vw in views)
                        {   // each view: one frame drawn from it first (the camera still between two frames: no motion blur from the jump), then the pictures
                            cam.transform.SetPositionAndRotation(vw.pos, vw.rot);
                            Object.Destroy(Shot(cam, 1920, 1080, "after"));
                            yield return null;
                            cam.transform.SetPositionAndRotation(vw.pos, vw.rot);
                            Set(cam, dir, $"{tn}_exp_v2_{type + 1}_{vw.name}_{when:0.0}s", 1920, 1080, "before", "after", "after_noshape");
                        }
                        cam.transform.SetPositionAndRotation(views[0].pos, views[0].rot);
                        yield return null;
                    }
                }
            }
            Time.captureFramerate = cf0;
            g.blueBot = bb; g.redBot = rb;
            if (dn) { dn.timeOfDay = tod0; dn.Apply(tod0); dn.running = run0; }
            if (rts) rts.enabled = true;
            log?.AppendLine("border frames in " + dir + " (" + OzSmokeFog.Status() + ")");
            Debug.Log($"{Tag} border frames written: {dir}");
            Busy = false;
        }
    }
}
