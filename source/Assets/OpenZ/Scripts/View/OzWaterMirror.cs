// OpenZ (GPL-3.0)
// The MIRROR PICTURE of the level for the water: while the player plays, the things that stand above the water (banks, quays, bridges,
// buildings, containers, units, wrecks) are drawn once more into a small picture (half the screen's width and height), seen through the
// water's surface as a mirror: the same camera, the level flipped at the water's height, everything under the water cut away. The water
// shaders (Shaders/OzWaterObjReflect.hlsl, both waters) read that picture at each water point's own place in it, so every pixel of a
// reflection is the real mirror image of the thing that stands there: a thing that is not above that water cannot appear on it (round 5's
// screen-space ray picked up the quay tops as brown sheets), and the image is as tall on screen as the thing itself.
// How it is drawn: one extra pass inside the player's own camera ("OZ Water Mirror", before the see-through things; put in from
// RenderPipelineManager.beginCameraRendering like the fog of war's pass, no renderer asset is changed, no second camera exists). It uses
// the camera's own list of visible things (no second culling), the sun's shadow map and the lights already set up for the frame (no
// shadow pass, no light setup of its own), solid things only (no smoke, fire, rings, health bars, HUD), no anti-aliasing.
// So units the fog of war holds off are not in it either, and a camera shake moves it with the picture.
// Off, and nothing drawn: GRAPHICS QUALITY LOW (_OZWaterLite.w), briefings / flyovers / the menu (_OZWaterObjRefl.x = 0, as before), a
// camera that is not the player's RTS camera, no water of these two shaders in view. There the water is exactly as before this kit
// (_OZWaterMirror.x = 0: the screen-space reflection of kit water_tune_r3). A pool at another height than the picture's keeps that too.
// View only: no rule of the game reads any of it. Nothing is saved.
// The picture was made for the water NEAREST to the point the camera looks at. On level 18 the river's own box ends at z -51
// (the north canal) while the map goes on to z 0, and the sea skirt under the whole map (-1.7 m) is under every point: wherever the camera
// looked at the forts north of the canal the picture was made for the skirt, and the river (-0.8 m) fell back to the old screen-space
// reflection, weaker and broken; one metre of pan across that line switched the whole river in one frame. The camera height switched
// nothing (the point looked at stays put when zooming). Now the UPPER water round the view wins (FindPlane, NearReach), the choice is made
// once a frame, and any change between the picture and the old reflection is a FadeSeconds cross-fade (the share in _OZWaterMirror.x),
// never a switch. The mirror is on from the closest zoom to the widest; above ThinAbove only its refresh is halved (the look is the same).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    public sealed class OzWaterMirror : MonoBehaviour
    {
        /// <summary>The switch (checks and before / after frames): false = the water exactly as before the kit.</summary>
        public static bool On = true;
        /// <summary>How strong the mirror image lies on the water (0..1; the sky reflection shows through the rest).</summary>
        public static float Strength = 0.75f;
        /// <summary>How far the ripples push the image about (metres on the water per unit of ripple slope).</summary>
        public static float Bend = 0.35f;
        /// <summary>1 = the true mirror image (as tall on screen as the thing, touching it at the waterline). LEAVE AT 1: tried 1.6 / 2.2 at the
        /// build, the images of things standing back from the edge then lie loose in the water (the kit's proof sheet); checks only.</summary>
        public static float Stretch = 1f;
        /// <summary>The picture is the screen's size divided by this (2 = a quarter of the pixels).</summary>
        public static int Scale = 2;
        /// <summary>The picture is drawn every Nth frame (1 = every frame). Between two pictures the old one still lies right on the water,
        /// also while the camera pans or zooms (it is read through the camera it was made with, and a pan does not turn a mirror image);
        /// only moving units' images are then a frame late. Above ThinAbove metres of camera height it is drawn half as often again
        /// (the images are a few pixels tall there).</summary>
        public static int EveryNth = 2;
        public static float ThinAbove = 110f;
        /// <summary>1.5 kit reflections_ease_r7: seconds the mirror takes to fade out, and again to fade in, when the picture moves to another
        /// water height while the camera plays on (between, the water mixes the picture and the old reflection); 0 = at once, as the r6 kit.</summary>
        public static float FadeSeconds = 0.35f;
        /// <summary>1.5 kit reflections_ease_r7: a water surface is ROUND THE VIEW when its box comes within this many times the camera's height
        /// over it of the point the camera looks at (the view's far corners lie 0.95 x that height away at pitch 55, FOV 32).</summary>
        public static float NearReach = 1.2f;
        /// <summary>Checks only (kit reflections_ease_r7's BEFORE sweeps): true = the r6 kit's choice of the water (the nearest to the point
        /// looked at; on level 18 the sea skirt then won north of z -51). With FadeSeconds 0 it is the r6 kit exactly.</summary>
        public static bool PickR6;
        /// <summary>Layers in the picture: all but TransparentFX, Water, UI, Minimap, ZodCameo.</summary>
        public static int Mask = ~((1 << 1) | (1 << 4) | (1 << 5) | (1 << 12) | (1 << 29));
        /// <summary>Things under the water are cut this far below its surface (m), so no gap opens at the waterline.</summary>
        public const float ClipBias = 0.02f;

        // ------------------------------------------------------------------ read-outs for the checks
        /// <summary>Pictures drawn since the program started; water surfaces known in this level; the water height of the last picture.</summary>
        public static int Pictures { get; private set; }
        public static int Pools => pools.Count;
        public static float PlaneY { get; private set; }
        /// <summary>True on a frame where the player's camera got the mirror picture.</summary>
        public static bool Live { get; private set; }
        /// <summary>The picture itself (null until the first one).</summary>
        public static RenderTexture Picture => colRT;
        /// <summary>1.5 kit reflections_ease_r7: the mirror's share now (0..1, before its ease curve), the water height the picture is for, and
        /// how many times it moved to another height with a fade since the start.</summary>
        public static float Share => share;
        public static float ShownY => shownY;
        public static int Fades { get; private set; }

        static readonly int IdRefl = Shader.PropertyToID("_OZWaterObjRefl"), IdLite = Shader.PropertyToID("_OZWaterLite"),
            IdMirror = Shader.PropertyToID("_OZWaterMirror"), IdTx = Shader.PropertyToID("_OZWaterMirrorTx"), IdVP = Shader.PropertyToID("_OZWaterMirrorVP"),
            IdTex = Shader.PropertyToID("_OZWaterMirrorTex"), IdDep = Shader.PropertyToID("_OZWaterMirrorDepth"),
            IdCamPos = Shader.PropertyToID("_WorldSpaceCameraPos"), IdScaled = Shader.PropertyToID("_ScaledScreenParams"), IdScreenSize = Shader.PropertyToID("_ScreenSize");
        static OzWaterMirror run;
        static MirrorPass pass;
        static RenderTexture colRT, depRT; static RTHandle colH, depH;
        static bool pictureOk; static float pictureY;
        // 1.5 kit reflections_ease_r7: the height in use, the mirror's share, the time of the last step (-1 = after a cut) and the player's camera
        static bool shown; static float shownY, share, stepAt = -1f; static Camera lastCam;
        /// <summary>Two water surfaces closer in height than this are one (the water shader's own test, OzWaterObjReflect.hlsl).</summary>
        const float SameY = 0.06f;

        struct Pool { public Renderer r; public float y; }
        static readonly List<Pool> pools = new List<Pool>();
        static readonly Plane[] planes = new Plane[6];
        static bool dirty = true; static int tries; static float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            Shader.SetGlobalVector(IdMirror, Vector4.zero);
            var args = System.Environment.GetCommandLineArgs();                                          // the fps bench's before run: OpenZ.exe ... -oz-water-mirror-off
            for (int i = 0; i < args.Length; i++) if (args[i] == "-oz-water-mirror-off") On = false;
            if (run) return;
            var go = new GameObject("OZ_WaterMirror"); DontDestroyOnLoad(go);
            run = go.AddComponent<OzWaterMirror>();
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera; RenderPipelineManager.beginCameraRendering += BeginCamera;
            SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneLoaded += SceneLoaded;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            SceneManager.sceneLoaded -= SceneLoaded;
            Shader.SetGlobalVector(IdMirror, Vector4.zero);
            Release();
        }

        void OnDestroy() { if (run == this) run = null; }

        static void SceneLoaded(Scene s, LoadSceneMode m) { pools.Clear(); dirty = true; tries = 0; nextScan = 0f; pictureOk = false; shown = false; stepAt = -1f; lastCam = null; }   // 1.5 kit reflections_ease_r7: + the height in use

        /// <summary>Is the reflection of objects asked for now (the player plays, not GRAPHICS QUALITY LOW)?</summary>
        static bool Wanted => On && Shader.GetGlobalVector(IdRefl).x > 0f && Shader.GetGlobalVector(IdLite).w < 0.5f;

        void LateUpdate()
        {
            Live = false;
            if (!Wanted) { stepAt = -1f; pictureOk = false; return; }
            for (int i = pools.Count - 1; i >= 0; i--) if (!pools[i].r) { pools.RemoveAt(i); dirty = true; tries = 0; }
            if (dirty && Time.unscaledTime >= nextScan) Scan();
            // 1.5 kit reflections_ease_r7: the water the picture is for, chosen ONCE a frame here (was: in every draw of the camera; the checks
            // draw the camera again inside one frame); the draws of this frame use it
            var cam = lastCam && PlayerView(lastCam) ? lastCam : Camera.main;
            if (!PlayerView(cam)) { stepAt = -1f; return; }
            lastCam = cam;
            bool ok = FindPlane(cam, out float want);
            Step(ok, want);
        }

        /// <summary>1.5 kit reflections_ease_r7: the height in use and the mirror's share this frame. Another height wanted: the share falls to 0
        /// over FadeSeconds (the water mixes the picture into the old reflection), then the picture moves there and the share comes back up over
        /// FadeSeconds. The first frame after a cut (the level's start, a briefing's or flyover's end, GRAPHICS changed, the camera given back,
        /// a frame of more than half a second) takes the height at once, as the view itself cuts there.</summary>
        static void Step(bool ok, float want)
        {
            float now = Time.unscaledTime, dt = now - stepAt;
            bool cut = stepAt < 0f || dt > 0.5f; stepAt = now;
            if (!ok) { shown = false; share = 0f; return; }                                             // no water of the two shaders round the view or in it
            if (cut || !shown || FadeSeconds <= 0f) { shown = true; shownY = want; share = 1f; return; }
            float k = dt / FadeSeconds;
            if (Mathf.Abs(want - shownY) > SameY)
            {
                share -= k;
                if (share <= 0f) { share = 0f; shownY = want; Fades++; }                                // the picture moves to the new height
            }
            else { shownY = want; share = Mathf.Min(1f, share + k); }
        }

        /// <summary>The level's water surfaces of the two shaders that mirror objects (flat ones; once per level, up to three tries 5 s apart
        /// while none is found: a level without water costs nothing after that).</summary>
        static void Scan()
        {
            pools.Clear();
            Shader a = Shader.Find("OpenZ/WaterStylized"), b = Shader.Find("OpenZ/WaterV4");
            if (a || b)
            {
                var all = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
                for (int i = 0; i < all.Length; i++)
                {
                    var r = all[i]; var m = r ? r.sharedMaterial : null; if (!m) continue;
                    var sh = m.shader; if (!sh || (sh != a && sh != b)) continue;
                    var bd = r.bounds; if (bd.size.y > 0.1f) continue;                                   // not a flat surface: it keeps the reflection it had
                    pools.Add(new Pool { r = r, y = bd.center.y });
                }
            }
            tries++; nextScan = Time.unscaledTime + 5f;
            if (pools.Count > 0 || tries >= 3) dirty = false;
        }

        /// <summary>The checks: look for the level's water again now.</summary>
        public static void Rescan() { dirty = true; tries = 0; nextScan = 0f; Scan(); }

        /// <summary>The water the picture is made for. 1.5 kit reflections_ease_r7: of the surfaces ROUND THE VIEW (their box within NearReach x
        /// the camera's height over them of the point the camera looks at, in view or just outside it), the UPPER one, as high: the nearer;
        /// none round it: of the surfaces in view, the one nearest to that point (the r6 kit's rule, PickR6). The r6 rule alone made level 18's
        /// sea skirt (-1.7 m, under the whole map) win wherever that point lay outside the river's own box (north of z -51). On the four levels
        /// with these two waters the only other height is that skirt (levels 17 and 18), so the river or the harbour always wins now.</summary>
        static bool FindPlane(Camera cam, out float y)
        {
            y = 0f;
            if (pools.Count == 0) return false;
            var t = cam.transform; Vector3 pos = t.position, fwd = t.forward;
            if (fwd.y > -0.05f) return false;                                                             // looking level or up: no mirror picture
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            float best = float.MaxValue, nearY = 0f, nearD = 0f; bool any = false, near = false;
            for (int i = 0; i < pools.Count; i++)
            {
                var p = pools[i]; var r = p.r;
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                float dy = pos.y - p.y; if (dy < 1f) continue;                                            // the camera at or under this water
                var b = r.bounds;
                Vector3 f = pos + fwd * (dy / -fwd.y);
                float d = (b.ClosestPoint(f) - f).sqrMagnitude;
                float reach = dy * NearReach;
                if (!PickR6 && d <= reach * reach && (!near || p.y > nearY + SameY || (p.y >= nearY - SameY && d < nearD))) { near = true; nearY = p.y; nearD = d; }   // round the view: the upper one
                if (!GeometryUtility.TestPlanesAABB(planes, b)) continue;
                if (!any || d < best - 0.01f || (d < best + 0.01f && p.y > y)) { best = Mathf.Min(best, d); y = p.y; any = true; }   // as near: the upper surface (it is the one seen)
            }
            if (near) { y = nearY; return true; }
            return any;
        }

        /// <summary>A camera the player plays through (the fog of war's rule): a game camera with an enabled RtsCamera, drawing to the screen or the main camera.</summary>
        static bool PlayerView(Camera c) =>
            c && c.cameraType == CameraType.Game && !c.orthographic && (c.targetTexture == null || c.CompareTag("MainCamera")) && c.TryGetComponent<RtsCamera>(out var r) && r.enabled;

        void BeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            bool mine = false; float y = shownY;                                                         // 1.5 kit reflections_ease_r7: the height LateUpdate chose this frame
            bool player = Wanted && PlayerView(cam); if (player) lastCam = cam;                            // 1.5 kit reflections_ease_r7: the camera LateUpdate looks from
            // The shader samples no mirror at zero blend. Defer the draw until it becomes visible again.
            if (player && shown && share > 0f && cam.TryGetComponent<UniversalAdditionalCameraData>(out var d) && d.scriptableRenderer != null)
            {
                var tt = cam.targetTexture; int div = Mathf.Clamp(Scale, 1, 4);
                int w = Mathf.Max(64, (tt ? tt.width : cam.pixelWidth) / div), h = Mathf.Max(64, (tt ? tt.height : cam.pixelHeight) / div);
                if (Ensure(w, h))
                {
                    int nth = Mathf.Max(1, EveryNth); if (cam.transform.position.y - y > ThinAbove) nth *= 2;
                    bool draw = !pictureOk || pictureY != y || nth == 1 || Time.frameCount % nth == 0;
                    if (draw)
                    {
                        if (pass == null) pass = new MirrorPass();
                        pass.planeY = y; d.scriptableRenderer.EnqueuePass(pass);
                        pictureOk = true; pictureY = y;
                    }
                    mine = true; PlaneY = y; Live = true;
                }
            }
            else if (player && shown) pictureOk = false;
            // every camera gets its own answer: only the player's camera reads the picture (it is true for that camera alone)
            float s = share * share * (3f - 2f * share);                                                 // 1.5 kit reflections_ease_r7: x = the mirror's share, eased (1 = the picture alone, as r6)
            Shader.SetGlobalVector(IdMirror, mine ? new Vector4(s, y, Mathf.Clamp01(Strength), Bend) : Vector4.zero);
        }

        static bool Ensure(int w, int h)
        {
            if (colRT && depRT && colRT.width == w && colRT.height == h && colRT.IsCreated() && depRT.IsCreated()) return true;
            Release();
            var fmt = SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormatUsage.Render) ? GraphicsFormat.B10G11R11_UFloatPack32 : GraphicsFormat.R16G16B16A16_SFloat;
            colRT = new RenderTexture(w, h, 0, fmt) { name = "OZ_WaterMirror", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, useMipMap = false, hideFlags = HideFlags.HideAndDontSave };
            depRT = new RenderTexture(w, h, GraphicsFormat.None, GraphicsFormat.D32_SFloat) { name = "OZ_WaterMirrorDepth", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, useMipMap = false, hideFlags = HideFlags.HideAndDontSave };
            if (!colRT.Create() || !depRT.Create()) { Release(); return false; }
            colH = RTHandles.Alloc(colRT); depH = RTHandles.Alloc(depRT);
            Shader.SetGlobalTexture(IdTex, colRT); Shader.SetGlobalTexture(IdDep, depRT);
            pictureOk = false;
            return true;
        }

        static void Release()
        {
            pictureOk = false;
            if (colH != null) { colH.Release(); colH = null; }
            if (depH != null) { depH.Release(); depH = null; }
            if (colRT) { colRT.Release(); Destroy(colRT); }
            if (depRT) { depRT.Release(); Destroy(depRT); }
            colRT = null; depRT = null;
        }

        /// <summary>The projection with its near plane laid into the plane c (camera space, the camera on its negative side): everything
        /// behind that plane is cut (Lengyel's oblique near plane).</summary>
        static Matrix4x4 Oblique(Matrix4x4 p, Vector4 c)
        {
            Vector4 q = p.inverse * new Vector4(Mathf.Sign(c.x), Mathf.Sign(c.y), 1f, 1f);
            Vector4 s = c * (2f / Vector4.Dot(c, q));
            p.m20 = s.x - p.m30; p.m21 = s.y - p.m31; p.m22 = s.z - p.m32; p.m23 = s.w - p.m33;
            return p;
        }

        // ---------------------------------------------------------------- the render graph pass
        sealed class MirrorPass : ScriptableRenderPass
        {
            public float planeY;
            static readonly List<ShaderTagId> Tags = new List<ShaderTagId> { new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit") };
            static readonly ProfilingSampler Sampler = new ProfilingSampler("OZ Water Mirror");
            static GlobalKeyword kwSsao; static bool kwMade;

            public MirrorPass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;                           // after the solid things, the sky and the opaque copy; before the water
                profilingSampler = Sampler;
            }

            sealed class PassData
            {
                public RendererListHandle list; public TextureHandle active; public Matrix4x4 view, proj, mView, mProj, sampleVP;
                public Vector4 camPos, mCamPos, scaled, mScaled, size, mSize; public bool ssao;
            }

            public override void RecordRenderGraph(RenderGraph rg, ContextContainer frame)
            {
                if (colH == null || depH == null) return;
                var cd = frame.Get<UniversalCameraData>(); var rd = frame.Get<UniversalRenderingData>(); var ld = frame.Get<UniversalLightData>();
                var res = frame.Get<UniversalResourceData>();
                if (!kwMade) { kwSsao = GlobalKeyword.Create("_SCREEN_SPACE_OCCLUSION"); kwMade = true; }
                Matrix4x4 view = cd.GetViewMatrix(), proj = cd.GetProjectionMatrix();
                float y0 = planeY, s = Mathf.Clamp(Stretch, 1f, 3f);
                var flip = Matrix4x4.identity; flip.m11 = -s; flip.m13 = (1f + s) * y0;                  // the level flipped at the water's height
                Matrix4x4 mView = view * flip;
                Vector4 cut = mView.inverse.transpose * new Vector4(0f, 1f, 0f, -(y0 - ClipBias));       // the water's plane in the mirror view: what is under it is cut
                if (cut.w >= 0f) return;                                                                 // the camera not above the water: no picture
                Matrix4x4 mProj = Oblique(proj, cut);
                var td = cd.cameraTargetDescriptor; float mw = colRT.width, mh = colRT.height;
                using (var b = rg.AddRasterRenderPass<PassData>("OZ Water Mirror", out var d, Sampler, file: "OzWaterMirror.cs"))   // the file name given: no build folder path in the game code
                {
                    var ds = RenderingUtils.CreateDrawingSettings(Tags, rd, cd, ld, cd.defaultOpaqueSortFlags);
                    var fs = new FilteringSettings(RenderQueueRange.opaque, Mask);
                    d.list = rg.CreateRendererList(new RendererListParams(rd.cullResults, ds, fs));
                    d.view = view; d.proj = proj; d.mView = mView; d.mProj = mProj; d.active = res.activeColorTexture;
                    d.sampleVP = proj * view;
                    Vector3 cp = cd.worldSpaceCameraPos, mcp = flip.inverse.MultiplyPoint3x4(cp);
                    d.camPos = new Vector4(cp.x, cp.y, cp.z, 0f); d.mCamPos = new Vector4(mcp.x, mcp.y, mcp.z, 0f);
                    d.scaled = new Vector4(td.width, td.height, 1f + 1f / td.width, 1f + 1f / td.height); d.mScaled = new Vector4(mw, mh, 1f + 1f / mw, 1f + 1f / mh);
                    d.size = new Vector4(td.width, td.height, 1f / td.width, 1f / td.height); d.mSize = new Vector4(mw, mh, 1f / mw, 1f / mh);
                    d.ssao = res.ssaoTexture.IsValid();
                    b.UseRendererList(d.list);
                    b.SetRenderAttachment(rg.ImportTexture(colH), 0, AccessFlags.Write);
                    b.SetRenderAttachmentDepth(rg.ImportTexture(depH), AccessFlags.Write);
                    if (res.mainShadowsTexture.IsValid()) b.UseTexture(res.mainShadowsTexture, AccessFlags.Read);
                    if (res.additionalShadowsTexture.IsValid()) b.UseTexture(res.additionalShadowsTexture, AccessFlags.Read);
                    b.AllowGlobalStateModification(true);
                    b.AllowPassCulling(false);
                    b.SetRenderFunc(static (PassData x, RasterGraphContext c) =>
                    {
                        var cmd = c.cmd;
                        // the picture is drawn the same way up as the camera's own target (so the engine's face and screen rules hold for
                        // it unchanged); where that is top-down the water shader reads it top-down (_OZWaterMirrorTx.z)
                        bool up = !SystemInfo.graphicsUVStartsAtTop || c.GetTextureUVOrigin(in x.active) == TextureUVOrigin.BottomLeft;
                        Matrix4x4 gp = GL.GetGPUProjectionMatrix(x.proj, up), gmp = GL.GetGPUProjectionMatrix(x.mProj, up);
                        cmd.ClearRenderTarget(RTClearFlags.All, Color.clear, 1f, 0);
                        // the mirror view for these draws: the matrices, the eye's place (the lights' depth slices and the shine go by it) and
                        // the picture's own size (the lights' screen tiles go by it); the screen-space shade is the main picture's, so it is left out
                        cmd.SetGlobalVector(IdCamPos, x.mCamPos); cmd.SetGlobalVector(IdScaled, x.mScaled); cmd.SetGlobalVector(IdScreenSize, x.mSize);
                        if (x.ssao) cmd.SetKeyword(kwSsao, false);
                        RenderingUtils.SetViewAndProjectionMatrices(cmd, x.mView, gmp, true);
                        cmd.SetInvertCulling(true);                                                      // a mirror turns every face round
                        cmd.DrawRendererList(x.list);
                        cmd.SetInvertCulling(false);
                        // everything back as the camera had it
                        RenderingUtils.SetViewAndProjectionMatrices(cmd, x.view, gp, true);
                        if (x.ssao) cmd.SetKeyword(kwSsao, true);
                        cmd.SetGlobalVector(IdCamPos, x.camPos); cmd.SetGlobalVector(IdScaled, x.scaled); cmd.SetGlobalVector(IdScreenSize, x.size);
                        cmd.SetGlobalMatrix(IdVP, x.sampleVP);                                           // where a water point lies in this picture
                        cmd.SetGlobalVector(IdTx, new Vector4(x.mSize.z, x.mSize.w, up ? 0f : 1f, 0f));
                        Pictures++;
                    });
                }
            }
        }
    }
}
