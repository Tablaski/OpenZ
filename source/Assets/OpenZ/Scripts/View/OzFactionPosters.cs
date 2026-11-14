using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ
{
    /// <summary>
    /// k75a (1.2 owner items 88 / 89, option A): the faction screen's two posters as REAL 3D. Made by the main menu when the faction screen
    /// opens (UI/OpenZMainMenu.Faction.cs) and closed when it is left: nothing of it exists in a level and nothing of it stays loaded.
    ///  * Loads (asynchronously, so the menu never stalls) from Resources/OpenZ/Faction: per poster the light model (zod_poster_light /
    ///    evil_poster_light: 85 000 triangles, one URP Lit material with its 4 maps) and the 2D layers of its print (Layers/: back plate,
    ///    mid layer, frame border, frame band; the paper grain tile is shared), and the UI material of the renders (OzPosterFigure).
    ///  * Builds each poster far from everything (<see cref="Origin"/>, 40 m apart) on its own layer (<see cref="Layer"/>, an unnamed one,
    ///    27 when free) and its own rendering layer (bit 3, "Light Layer 3": defined, otherwise unused): only the posters' three suns light
    ///    the models and only the posters' cameras see them.
    ///  * Light, as the light models' own comparison renders (key from the characters' left above the camera, a soft fill from the other
    ///    side, a rim from behind and above, a grey world): three directional lights (key with soft shadows; the key is the scene's sun
    ///    while the screen shows), the grey world as each model's own ambient (its light probe data, so nothing global changes) and as
    ///    the reflections of the metal (a small custom reflection probe of the same grey).
    ///  * One camera per poster, the measured poster camera (OzFaction.Cams: position, rotation and lens local to the model's root),
    ///    rendering into a RenderTexture the size the poster shows on screen (k75a4: Supersample times it, its mipmaps the downsample; was
    ///    4x MSAA, which the graphics preset's own MSAA count gates; cleared to transparent black; the atmosphere pass
    ///    keeps its alpha while the screen shows: _AtmoKeepAlpha 1, the intro's cut-out path), no post effects. The menu turns it each
    ///    frame (<see cref="Pose"/>) and draws the render between the print's layers with OzPosterFigure (premultiplied edges, a thin paper rim).
    ///  * <see cref="Close"/>: the cameras stop, the scene's sun and the alpha global are put back, the render textures are released, the
    ///    instances destroyed, then every texture, mesh and material it loaded is unloaded (Resources.UnloadAsset; back on the main menu also
    ///    one Resources.UnloadUnusedAssets for the models' empty shells).
    /// </summary>
    public sealed class OzFactionPosters : MonoBehaviour
    {
        public const string Folder = "OpenZ/Faction/", LayerFolder = "OpenZ/Faction/Layers/";
        /// <summary>The print's layers, back to front around the render: back plate, mid layer, frame border (behind the render: the
        /// pointing fist may break out of the frame, as in the owner's pictures), frame band (in front: it hides the legs).</summary>
        public static readonly string[] LayerParts = { "background", "mid", "frame_border", "frame_band" };
        public const string GrainTile = "paper_grain_tile", FigureMaterial = "OzPosterFigure";
        public static readonly Vector3 Origin = new Vector3(6400f, -2400f, 6400f);
        public const float Spacing = 40f;
        public const int PreferredLayer = 27, RenderingLayerBit = 3;
        /// <summary>The comparison renders' suns (strength / pi in this engine's units) and their directions in the posters' frame (the
        /// characters face -Z, the camera looks +Z): key 1.02 from the characters' left, above, in front; fill 0.32 from their right; rim
        /// 0.80 from behind, above.</summary>
        public static readonly Vector3 KeyDir = new Vector3(-0.4394f, -0.6428f, 0.6275f), FillDir = new Vector3(0.5826f, -0.4226f, 0.6943f), RimDir = new Vector3(-0.0996f, -0.8192f, -0.5649f);
        public const float KeyIntensity = 1.02f, FillIntensity = 0.32f, RimIntensity = 0.80f, KeyShadow = 0.9f;
        /// <summary>The grey world of the comparison renders (linear): the models' ambient and their reflections.</summary>
        public static readonly Color World = new Color(0.224f, 0.224f, 0.238f, 1f);
        public const float Near = 0.3f, Far = 7f;
        public const int Msaa = 4;
        /// <summary>
        /// the poster's size on screen (each side, at most MaxRt px) with mipmaps, so the screen samples a 2 x 2 average of the render: smooth
        /// edges on the hats, fists and robots, no shimmer in their textures, on every graphics preset (the pipeline's MSAA count gates the
        /// render texture's own 4x: none on LOW). Msaa is used only when there is no room to supersample.</summary>
        public const float Supersample = 2f;
        public const int MaxRt = 2560;
        /// <summary>The render texture's size for a poster w x h px on screen.</summary>
        public static Vector2Int RtSize(int w, int h)
        {
            float k = Mathf.Max(1f, Mathf.Min(Supersample, MaxRt / (float)Mathf.Max(1, Mathf.Max(w, h))));
            return new Vector2Int(Mathf.RoundToInt(w * k), Mathf.RoundToInt(h * k));
        }

        public static OzFactionPosters I { get; private set; }
        /// <summary>Checks: rigs opened / closed / unloaded since the game started; render texture builds; the last load time.</summary>
        public static int Opened { get; private set; }
        public static int Closed { get; private set; }
        public static int Unloaded { get; private set; }
        public static int RtBuilds { get; private set; }
        public static float LastLoadSeconds { get; private set; } = -1f;
        public static int LastUnloadCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { I = null; Opened = Closed = Unloaded = RtBuilds = LastUnloadCount = 0; LastLoadSeconds = -1f; }

        public sealed class Poster
        {
            public int side; public string id; public OzFaction.PosterCam cam0;
            public readonly Texture2D[] layers = new Texture2D[4];
            public bool layersReady, modelReady;
            public Transform root; public GameObject model; public Renderer[] renderers = new Renderer[0]; public Material mat;
            public Camera cam; public RenderTexture rt; public ReflectionProbe probe;
            public int triangles; public float yaw, pitch;
        }

        public readonly Poster[] P = new Poster[2];
        public Texture2D Grain { get; private set; }
        /// <summary>The renders' UI material (a runtime copy of Resources OzPosterFigure, so its rim width can follow the render size).</summary>
        public Material Figure { get; private set; }
        /// <summary>k75a2: each poster's print material (a runtime copy of OzPosterFigure in its print mode: the aged paper, which takes a share
        /// of the screen's colour behind that poster; the menu sets that colour each frame).</summary>
        public readonly Material[] Print = new Material[2];
        public int Layer { get; private set; }
        public bool Live { get; private set; }
        public bool Loading { get; private set; }
        public bool Ready => P[0] != null && P[1] != null && P[0].modelReady && P[1].modelReady;
        public float LoadSeconds { get; private set; } = -1f;
        public int Triangles => (P[0] != null ? P[0].triangles : 0) + (P[1] != null ? P[1].triangles : 0);
        public Light Key => key;

        Light key, fill, rim; Cubemap env; Transform lightRoot;
        Light sunWas; float keepAlphaWas, atmoWas; bool globalsSet, closing;
        readonly List<Object> loaded = new List<Object>();
        float openedAt;
        static readonly int IdKeepAlpha = Shader.PropertyToID("_AtmoKeepAlpha"), IdAtmo = Shader.PropertyToID("_AtmoEnabled");

        /// <summary>The rig of the faction screen (a new one when the last one is closing), under the menu's own object.</summary>
        public static OzFactionPosters Open(Transform parent)
        {
            if (I && !I.closing) return I;
            var go = new GameObject("OzFactionPosters");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Origin, Quaternion.identity);
            return go.AddComponent<OzFactionPosters>();
        }

        /// <summary>An unnamed layer for the posters: 27 when free, else the highest free one from 26 down to 13.</summary>
        public static int ResolveLayer()
        {
            if (string.IsNullOrEmpty(LayerMask.LayerToName(PreferredLayer))) return PreferredLayer;
            for (int i = 26; i >= 13; i--) if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) return i;
            return PreferredLayer;
        }

        void Awake()
        {
            I = this; Opened++; openedAt = Time.realtimeSinceStartup;
            Layer = ResolveLayer();
            gameObject.layer = Layer;
            for (int i = 0; i < 2; i++) P[i] = new Poster { side = i, id = OzFaction.Cams[i].id, cam0 = OzFaction.Cam(i) };
            StartCoroutine(Load());
        }

        T Keep<T>(T o) where T : Object { if (o) loaded.Add(o); return o; }

        IEnumerator Load()
        {
            Loading = true;
            var gr = Resources.LoadAsync<Texture2D>(LayerFolder + GrainTile);
            var fm = Resources.LoadAsync<Material>(Folder + FigureMaterial);
            yield return gr; yield return fm;
            Grain = Keep(gr.asset as Texture2D);
            var fmat = fm.asset as Material;
            if (fmat)
            {
                Keep(fmat); Figure = new Material(fmat) { name = "OzPosterFigure (faction screen)", hideFlags = HideFlags.DontSave };
                OzFaction.SetPaper(Figure, false);                                                                  // k75a2: its paper rim aged as the paper
                for (int i = 0; i < 2; i++) { Print[i] = new Material(fmat) { name = "OzPosterFigure (faction print " + i + ")", hideFlags = HideFlags.DontSave }; OzFaction.SetPaper(Print[i], true); }
            }
            else Debug.LogWarning("[OpenZ Menu] faction posters: Resources/" + Folder + FigureMaterial + " missing: the 3D renders are drawn without their premultiplied edge");
            for (int i = 0; i < 2 && !closing; i++)
            {
                var p = P[i];
                var reqs = new ResourceRequest[LayerParts.Length];
                for (int k = 0; k < reqs.Length; k++) reqs[k] = Resources.LoadAsync<Texture2D>(LayerFolder + p.id + "_" + LayerParts[k]);
                for (int k = 0; k < reqs.Length; k++)
                {
                    yield return reqs[k];
                    p.layers[k] = Keep(reqs[k].asset as Texture2D);
                    if (!p.layers[k]) Debug.LogWarning("[OpenZ Menu] faction posters: Resources/" + LayerFolder + p.id + "_" + LayerParts[k] + " missing");
                }
                if (Print[i]) Print[i].SetTexture("_PaperInk", p.layers[1]);                                         // k105: the mid layer's white is print (OzFaction.PaperSeeInk)
                p.layersReady = true;
            }
            if (!closing) BuildLights();
            for (int i = 0; i < 2 && !closing; i++)
            {
                var p = P[i];
                var pr = Resources.LoadAsync<GameObject>(Folder + p.id + "_poster_light");
                var mr = Resources.LoadAsync<Material>(Folder + p.id + "_poster");
                yield return pr; yield return mr;
                if (!closing) BuildPoster(p, pr.asset as GameObject, mr.asset as Material);
                else { KeepMaterial(mr.asset as Material); KeepMeshes(pr.asset as GameObject); }            // left while it loaded: unloaded with the rest
            }
            LoadSeconds = LastLoadSeconds = Time.realtimeSinceStartup - openedAt;
            Loading = false;
            if (!closing)
                Debug.Log($"[OpenZ Menu] faction posters loaded in {LoadSeconds * 1000f:0} ms: {P[0].id} {(P[0].modelReady ? P[0].triangles + " triangles" : "NO MODEL")}, {P[1].id} {(P[1].modelReady ? P[1].triangles + " triangles" : "NO MODEL")}, "
                          + $"layer {Layer} ({(string.IsNullOrEmpty(LayerMask.LayerToName(Layer)) ? "unnamed" : LayerMask.LayerToName(Layer))}), rendering layer {RenderingLayerBit}, {loaded.Count} assets kept for the unload");
        }

        void KeepMaterial(Material m)
        {
            if (!m) return;
            Keep(m);
            foreach (var n in new[] { "_BaseMap", "_BumpMap", "_EmissionMap", "_MetallicGlossMap" })
                if (m.HasProperty(n) && m.GetTexture(n) is Texture t && !loaded.Contains(t)) Keep(t);
        }

        void KeepMeshes(GameObject prefab)
        {
            if (!prefab) return;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh && !loaded.Contains(mf.sharedMesh)) Keep(mf.sharedMesh);
        }

        void BuildLights()
        {
            lightRoot = new GameObject("Lights") { layer = Layer }.transform;
            lightRoot.SetParent(transform, false);
            key = AddSun("Key", KeyDir, KeyIntensity, true);
            fill = AddSun("Fill", FillDir, FillIntensity, false);
            rim = AddSun("Rim", RimDir, RimIntensity, false);
            env = new Cubemap(16, TextureFormat.RGBAHalf, true) { name = "OzFactionWorld", hideFlags = HideFlags.DontSave };
            var px = new Color[16 * 16];
            for (int k = 0; k < px.Length; k++) px[k] = World;
            for (int f = 0; f < 6; f++) env.SetPixels(px, (CubemapFace)f, 0);
            env.Apply(true, false);
        }

        Light AddSun(string name, Vector3 dir, float intensity, bool shadows)
        {
            var go = new GameObject(name) { layer = Layer };
            go.transform.SetParent(lightRoot, false);
            go.transform.rotation = Quaternion.LookRotation(dir.normalized);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional; l.color = Color.white; l.intensity = intensity;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            if (shadows) l.shadowStrength = KeyShadow;
            l.cullingMask = 1 << Layer;
            l.renderingLayerMask = 1 << RenderingLayerBit;
            var ld = l.GetUniversalAdditionalLightData();
            if (ld) { ld.renderingLayers = 1u << RenderingLayerBit; ld.shadowRenderingLayers = 1u << RenderingLayerBit; }
            l.enabled = Live;
            return l;
        }

        void BuildPoster(Poster p, GameObject prefab, Material mat)
        {
            p.root = new GameObject("Poster_" + p.id) { layer = Layer }.transform;
            p.root.SetParent(transform, false);
            p.root.localPosition = new Vector3(p.side * Spacing, 0f, 0f);
            KeepMaterial(mat);
            if (!prefab) { Debug.LogWarning("[OpenZ Menu] faction posters: Resources/" + Folder + p.id + "_poster_light missing: the poster shows its print only"); return; }
            if (!mat) Debug.LogWarning("[OpenZ Menu] faction posters: Resources/" + Folder + p.id + "_poster missing: the model keeps its imported material");
            p.mat = mat;
            p.model = Instantiate(prefab, p.root, false);
            p.model.name = p.id + "_poster_light";
            foreach (var t in p.model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            var sh = new SphericalHarmonicsL2();
            sh.Clear(); sh.AddAmbientLight(World);
            var mpb = new MaterialPropertyBlock();
            mpb.CopySHCoefficientArraysFrom(new[] { sh });
            p.renderers = p.model.GetComponentsInChildren<Renderer>(true);
            foreach (var r in p.renderers)
            {
                if (mat) { var ms = new Material[Mathf.Max(1, r.sharedMaterials.Length)]; for (int k = 0; k < ms.Length; k++) ms[k] = mat; r.sharedMaterials = ms; }
                r.renderingLayerMask = 1u << RenderingLayerBit;
                r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
                r.lightProbeUsage = LightProbeUsage.CustomProvided; r.SetPropertyBlock(mpb);
                r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                r.allowOcclusionWhenDynamic = false;
            }
            p.triangles = 0;
            foreach (var mf in p.model.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh; if (!m) continue;
                Keep(m);
                for (int s = 0; s < m.subMeshCount; s++) p.triangles += (int)(m.GetIndexCount(s) / 3);
            }
            // the reflections of the metal: the grey world
            var pg = new GameObject("Reflections_" + p.id) { layer = Layer };
            pg.transform.SetParent(p.root, false);
            p.probe = pg.AddComponent<ReflectionProbe>();
            p.probe.mode = ReflectionProbeMode.Custom; p.probe.customBakedTexture = env;
            p.probe.size = new Vector3(6f, 6f, 6f); p.probe.center = Vector3.zero; p.probe.boxProjection = false; p.probe.blendDistance = 0f; p.probe.importance = 100;
            // the measured poster camera
            var cg = new GameObject("Camera_" + p.id) { layer = Layer };
            cg.transform.SetParent(p.root, false);
            var cam = cg.AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = 1 << Layer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = p.cam0.fov; cam.nearClipPlane = Near; cam.farClipPlane = Far; cam.depth = -30f;
            cam.allowHDR = false; cam.allowMSAA = true; cam.useOcclusionCulling = false;
            var ad = cam.GetUniversalAdditionalCameraData();
            ad.renderPostProcessing = false; ad.antialiasing = AntialiasingMode.None; ad.renderShadows = true;
            ad.volumeLayerMask = 0; ad.stopNaN = false; ad.dithering = false; ad.requiresColorOption = CameraOverrideOption.Off;
            p.cam = cam;
            Pose(p.side, 0f, 0f);
            p.modelReady = true;
            if (Live && p.rt) { cam.targetTexture = p.rt; cam.aspect = p.rt.width / (float)p.rt.height; cam.enabled = true; }
        }

        /// <summary>The render textures at the poster's size on screen (pixels): rebuilt only when the size changes.</summary>
        public void SetSize(int w, int h)
        {
            w = Mathf.Clamp(w, 64, 2048); h = Mathf.Clamp(h, 64, 2048);
            var sz = RtSize(w, h); bool ss = sz.y > h;                                                          // k75a4: supersampled (mipmaps: the downsample)
            for (int i = 0; i < 2; i++)
            {
                var p = P[i]; if (p == null || !p.cam) continue;
                if (p.rt && p.rt.width == sz.x && p.rt.height == sz.y) continue;
                var old = p.rt;
                p.rt = new RenderTexture(sz.x, sz.y, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                { name = "OzFactionPoster_" + p.id, antiAliasing = ss ? 1 : Msaa, useMipMap = ss, autoGenerateMips = ss, wrapMode = TextureWrapMode.Clamp, filterMode = ss ? FilterMode.Trilinear : FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
                p.rt.Create();
                p.cam.targetTexture = p.rt; p.cam.aspect = sz.x / (float)sz.y;
                p.cam.enabled = Live;
                if (old) { old.Release(); Destroy(old); }
                RtBuilds++;
            }
        }

        /// <summary>The cameras render (the faction screen shows), else they stop; the posters' sun and the alpha global follow.</summary>
        public void SetLive(bool on)
        {
            if (closing) on = false;
            if (on == Live) return;
            Live = on;
            for (int i = 0; i < 2; i++) { var p = P[i]; if (p != null && p.cam) p.cam.enabled = on && p.cam.targetTexture; }
            foreach (var l in new[] { key, fill, rim }) if (l) l.enabled = on;
            if (on) SetGlobals(); else RestoreGlobals();
        }

        void SetGlobals()
        {
            if (globalsSet) return;
            globalsSet = true;
            sunWas = RenderSettings.sun; keepAlphaWas = Shader.GetGlobalFloat(IdKeepAlpha); atmoWas = Shader.GetGlobalFloat(IdAtmo);
            if (key) RenderSettings.sun = key;
            Shader.SetGlobalFloat(IdKeepAlpha, 1f); Shader.SetGlobalFloat(IdAtmo, 0f);
        }

        void RestoreGlobals()
        {
            if (!globalsSet) return;
            globalsSet = false;
            if (key && RenderSettings.sun == key) RenderSettings.sun = sunWas;
            Shader.SetGlobalFloat(IdKeepAlpha, keepAlphaWas); Shader.SetGlobalFloat(IdAtmo, atmoWas);
        }

        /// <summary>Turns a poster's camera about its pivot (OzFaction.Orbit: yaw / pitch in degrees).</summary>
        public void Pose(int side, float yaw, float pitch)
        {
            var p = side >= 0 && side < 2 ? P[side] : null;
            if (p == null || !p.cam) return;
            OzFaction.Orbit(p.cam0.pos, p.cam0.rot, p.cam0.pivot, yaw, pitch, out var pos, out var rot);
            p.cam.transform.localPosition = pos; p.cam.transform.localRotation = rot;
            p.yaw = yaw; p.pitch = pitch;
        }

        /// <summary>Leaves: the cameras stop, the globals come back at once; then (after any load in flight) the instances go and every
        /// asset it loaded is unloaded; sweep = also Resources.UnloadUnusedAssets (back on the main menu).</summary>
        public void Close(bool sweep)
        {
            if (closing) return;
            SetLive(false);
            closing = true; Closed++;
            StartCoroutine(CloseRoutine(sweep));
        }

        IEnumerator CloseRoutine(bool sweep)
        {
            while (Loading) yield return null;
            for (int i = 0; i < 2; i++)
            {
                var p = P[i]; if (p == null) continue;
                if (p.cam) p.cam.targetTexture = null;
                if (p.rt) { p.rt.Release(); Destroy(p.rt); p.rt = null; }
                if (p.root) Destroy(p.root.gameObject);
                p.model = null; p.cam = null; p.probe = null; p.renderers = new Renderer[0];
                for (int k = 0; k < p.layers.Length; k++) p.layers[k] = null;
                p.layersReady = p.modelReady = false;
            }
            if (lightRoot) Destroy(lightRoot.gameObject);
            if (Figure) { Destroy(Figure); Figure = null; }
            for (int i = 0; i < 2; i++) if (Print[i]) { Destroy(Print[i]); Print[i] = null; }                    // k75a2: the print materials
            if (env) { Destroy(env); env = null; }
            Grain = null;
            yield return null; yield return null;                                                             // the destroyed objects are gone
            int n = 0;
            foreach (var o in loaded) if (o && !(o is GameObject) && !(o is Component)) { Resources.UnloadAsset(o); n++; }
            loaded.Clear();
            LastUnloadCount = n;
            if (sweep) yield return Resources.UnloadUnusedAssets();
            Unloaded++;
            Debug.Log($"[OpenZ Menu] faction posters closed: {n} assets unloaded{(sweep ? " + unused assets swept" : "")}");
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            RestoreGlobals();
            for (int i = 0; i < 2; i++) { var p = P[i]; if (p != null && p.rt) { p.rt.Release(); Destroy(p.rt); p.rt = null; } }
            if (Figure) Destroy(Figure);
            foreach (var pm in Print) if (pm) Destroy(pm);                                                           // k75a2
            if (env) Destroy(env);
            if (I == this) I = null;
        }
    }
}
