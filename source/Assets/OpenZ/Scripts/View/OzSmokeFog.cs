using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace OpenZ
{
    /// <summary>
    /// THE CAUSE: the fog pass (Shaders/AtmosphereFullscreen.shader, URP Full Screen Pass "OpenZ
    /// Atmosphere", BeforeRenderingPostProcessing = after the transparents) marches the haze, the horizon band and the flyby sky treatment to
    /// the OPAQUE depth; see-through smoke writes no depth, so each smoke pixel was fogged by the distance of what stood behind it (the far
    /// cliff: heavy haze; the sky over it: the sky's haze + the horizon band) and the far land's outline cut a hard line through near smoke.
    /// THE FIX: for every perspective game / scene camera (the game camera, the briefings' drone, the flyovers, the film recorders: all draw
    /// through URP) this script adds one render-graph pass after the transparents: every transparent material with an "OzFxDist" pass (the
    /// smoke / fire shaders OpenZ/FxAdditive, OpenZ/FxBlast, OpenZ/FxLit: Shaders/OzFxDist.hlsl) draws its cover and eye depth into a
    /// per-camera R16G16 float target, set as the global _OzFxDistTex with _OzFxDistOn = 1. The fog pass fogs that cover by the smoke's own
    /// distance and only the rest by the distance behind it. A pixel without smoke is fogged exactly as before; the fog and the smoke stay on.
    /// Every camera this script does not serve reads _OzFxDistOn = 0 (the fog pass exactly as before).</summary>
    public static class OzSmokeFog
    {
        /// <summary>The switch (diagnosis and A/B frames only; the game keeps it on). false = the fog pass exactly as before the kit.</summary>
        public static bool On = true;
        /// <summary>Counters for the checks: cameras served / passes drawn since the start.</summary>
        public static int Served, Drawn, Skipped;
        public static string LastCamera = "";

        static readonly int IdOn = Shader.PropertyToID("_OzFxDistOn"), IdTex = Shader.PropertyToID("_OzFxDistTex");
        static DistPass pass;
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() => Hook();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void EditorBoot() => Hook();
#endif

        public static void Hook()
        {
            if (hooked) return;
            hooked = true;
            pass ??= new DistPass();
            RenderPipelineManager.beginCameraRendering -= Begin;
            RenderPipelineManager.beginCameraRendering += Begin;
        }

        /// <summary>The cameras served: game and scene-view cameras, perspective, base cameras of the universal renderer.</summary>
        public static bool Serves(Camera cam)
        {
            if (!cam || cam.orthographic) return false;
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return false;
            if (cam.TryGetComponent<UniversalAdditionalCameraData>(out var d) && d.renderType != CameraRenderType.Base) return false;
            return true;
        }

        static void Begin(ScriptableRenderContext ctx, Camera cam)
        {
            Shader.SetGlobalFloat(IdOn, 0f);                                                     // every camera starts with the fog pass as before
            if (!On || OzQuality.SkipBattleFog || !Serves(cam)) { Skipped++; return; }
            ScriptableRenderer r = null;
            if (cam.TryGetComponent<UniversalAdditionalCameraData>(out var d)) r = d.scriptableRenderer;
            else if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset a) r = a.scriptableRenderer;
            if (r == null || !(r is UniversalRenderer)) { Skipped++; return; }
            r.EnqueuePass(pass);
            Served++; LastCamera = cam.name;
        }

        /// <summary>One line for the checks and the console.</summary>
        public static string Status() => $"OzSmokeFog: on {On}, cameras served {Served}, passes drawn {Drawn}, skipped {Skipped}, last camera '{LastCamera}', targets {(pass != null ? pass.Targets : 0)}";

        sealed class DistPass : ScriptableRenderPass
        {
            static readonly ShaderTagId Tag = new ShaderTagId("OzFxDist");
            readonly Dictionary<int, RTHandle> targets = new Dictionary<int, RTHandle>();
            public int Targets => targets.Count;

            public DistPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;                     // after every smoke puff, before the fog pass (BeforeRenderingPostProcessing)
                profilingSampler = new ProfilingSampler("OZ Smoke Distance");
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            sealed class PassData { public RendererListHandle list; }

            RTHandle Target(Camera cam, int w, int h)
            {
                int id = cam.GetInstanceID();
                if (!targets.ContainsKey(id) && targets.Count >= 12)
                {   // cameras come and go (menu previews, briefing windows): the old targets are let go, never more than 12 kept
                    foreach (var kv in targets) kv.Value?.Release();
                    targets.Clear();
                }
                targets.TryGetValue(id, out var h0);
                var desc = new RenderTextureDescriptor(w, h, GraphicsFormat.R16G16_SFloat, GraphicsFormat.None) { msaaSamples = 1, useMipMap = false, autoGenerateMips = false };
                RenderingUtils.ReAllocateHandleIfNeeded(ref h0, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_OzFxDist");
                targets[id] = h0;
                return h0;
            }

            public override void RecordRenderGraph(RenderGraph rg, ContextContainer frame)
            {
                var res = frame.Get<UniversalResourceData>();
                if (!res.cameraDepthTexture.IsValid()) return;                                    // no scene depth: the fog pass keeps its old way (_OzFxDistOn 0)
                var cd = frame.Get<UniversalCameraData>();
                var rd = frame.Get<UniversalRenderingData>();
                var ld = frame.Get<UniversalLightData>();
                var td = cd.cameraTargetDescriptor;
                var h = Target(cd.camera, Mathf.Max(1, td.width), Mathf.Max(1, td.height));
                if (h == null) return;
                var tex = rg.ImportTexture(h);                                                    // imported: never aliased by later passes of the frame
                var ds = RenderingUtils.CreateDrawingSettings(Tag, rd, cd, ld, SortingCriteria.CommonTransparent);
                var fs = new FilteringSettings(RenderQueueRange.transparent, cd.camera.cullingMask);
                var list = rg.CreateRendererList(new RendererListParams(rd.cullResults, ds, fs));
                using (var b = rg.AddRasterRenderPass<PassData>("OZ Smoke Distance", out var d, profilingSampler, file: "OzSmokeFog.cs"))
                {
                    d.list = list;
                    b.UseRendererList(list);
                    b.UseAllGlobalTextures(true);                                                 // the smoke shaders read the scene depth (soft edges) and the light globals
                    b.UseTexture(res.cameraDepthTexture, AccessFlags.Read);
                    b.SetRenderAttachment(tex, 0, AccessFlags.Write);
                    b.AllowPassCulling(false);
                    b.AllowGlobalStateModification(true);
                    b.SetGlobalTextureAfterPass(tex, IdTex);
                    b.SetRenderFunc((PassData x, RasterGraphContext c) =>
                    {
                        c.cmd.ClearRenderTarget(false, true, Color.clear);
                        c.cmd.DrawRendererList(x.list);
                        c.cmd.SetGlobalFloat(IdOn, 1f);
                        Drawn++;
                    });
                }
            }
        }
    }
}
