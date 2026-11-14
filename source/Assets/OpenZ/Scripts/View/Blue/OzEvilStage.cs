// OpenZ (GPL-3.0)
// four blue scenes play on. Built the way the commander's studio is (ZodStage): a set far above the map on the ZodCameo layer, its own
// camera, its own lights (stage rendering layer only) and its own grade, rendered either into a RenderTexture (the comms window: the
// briefing, the nuke box, the menu card) or straight to the screen (the WIN / LOSE endings, full screen under the letterbox bars).
//    blue banners with the poster's winged eagle-and-star, striped pennants; moving cloth, OzEvilStage.Banners.cs), the round wall
//    emblem over a pair of sliding steel doors, a glowing tactical board with the level's map, a console with blinking lamps, the
//    button stand (the nuke scene), and behind the doors the alcove with the World-Domination-inator:
//  - The two robots: EvilZod (OzEvilStage.EvilZod.cs) and the sidekick BS-01 (OzEvilStage.Sidekick.cs), posed from their rest pose
//    every frame (OzPoseRig), so every gesture is the same each time; the director hands the beats of the scene data (OzBlueScenes) to
//    Beat() and the live voice level to Talk().
//  - The camera: named framings resolved from where the robots stand (two-shots, singles, the doors, the bomb), cut or eased, a light
//    hand-held sway, kicks on the big moments. Reduced motion keeps 15 % of the sway.
//    alcove spotlights, the alarm beacon (LOSE). Every visible action has its sound (Sfx: the game's own clips at the voice's level).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ
{
    [DefaultExecutionOrder(160)]
    public sealed partial class OzEvilStage : MonoBehaviour
    {
        public const string Folder = "OpenZ/Blue/Characters/", SetFolder = "OpenZ/Blue/Set/", SfxFolder = "OpenZ/Blue/Sfx/";
        /// <summary>The stage's place: beside the commander's studio (ZodStage.Origin), 400 m away (both cameras see 40 m at most).</summary>
        public static readonly Vector3 Origin = ZodStage.Origin + new Vector3(0f, 0f, 400f);
        public const int LightBit = ZodStage.LightLayerBit;
        public const uint LightMask = 1u << LightBit;

        public RenderTexture Feed { get; private set; }
        public bool Live => cam && cam.enabled;
        public bool OnScreen { get; private set; }
        public int Layer { get; private set; }
        public float Clock => clock;
        public string BoardLevel { get; private set; }
        public bool HasEvilZod => ez != null && ez.Ok;
        public bool HasSidekick => sk != null && sk.Ok;
        /// <summary>The voice's linear gain (the director's): every stage sound sits at a dB offset under it.</summary>
        [NonSerialized] public float VoiceGain = 0.8f;
        /// <summary>Raised on the frame EvilZod's fist meets the button's cap (the nuke scene: the clunk and the launch).</summary>
        public Action OnPress;
        public bool Pressed { get; private set; }
        public static int ContactFrame { get; private set; } = -1;
        public bool Frozen
        {
            get => frozen;
            set { frozen = value; }
        }

        Camera cam; Volume vol; DepthOfField dof; Light key, fill, rim, consoleGlow, beacon; readonly List<Light> alcove = new List<Light>(), banners = new List<Light>();
        Transform doorL, doorR, bomb, bombCradle, beaconRoot, boardScreen; Material boardMat; readonly List<Material> lamps = new List<Material>();
        /// <summary>The tactical board's centre (stage frame): EvilZod points and looks there on "THAT RED FORT".</summary>
        public static readonly Vector3 BoardPoint = new Vector3(2.35f, 2.05f, -3.2f);
        /// <summary>The LOSE klaxon's level under the voice gain (dB) and its duck while somebody talks (dB).</summary>
        public static float AlarmDb = -8f, AlarmDuckDb = 12f;
        float alarmDuck;
        /// <summary>The nuke scene's doors: the pause after the unlock clunk and the slide's length (s).</summary>
        public static float DoorPause = 0.3f, DoorSeconds = 3.8f;
        bool doorSlideSound, doorStopSound;
        float clock, nextLampAt; bool frozen, doorsOpen, spotsOn, alarmOn; float doorK, spotK, doorAt = -10f, spotAt = -10f;
        AudioSource sfxA, sfxB, loopSrc; readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
        static readonly List<Material> madeMats = new List<Material>();
        OzBlueScene scene; bool fullScreen; int mainMaskSaved; Camera mainSaved; bool mainMasked; float nextCamScan;

        // ================================================================================================ build
        public static OzEvilStage Create(Transform owner, string level)
        {
            var go = new GameObject("EvilZodStage");
            if (owner) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, owner.gameObject.scene);
            go.transform.position = Origin;
            var s = go.AddComponent<OzEvilStage>();
            s.Build(level);
            return s;
        }

        void Build(string level)
        {
            Layer = ZodStage.ResolveLayer();
            gameObject.layer = Layer;
            BuildSet();
            BuildLights();
            BuildCamera();
            BuildProps();
            BuildEvilZod();
            BuildSidekick();
            SetBoardLevel(level);
            sfxA = NewSource(30); sfxB = NewSource(31); loopSrc = NewSource(45); loopSrc.loop = true;
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            Debug.Log($"[EvilZod] stage built: EvilZod {(HasEvilZod ? ez.Rig.Height.ToString("F2") + " m" : "MISSING")}, sidekick {(HasSidekick ? sk.Rig.Height.ToString("F2") + " m" : "MISSING")}, "
                      + $"bomb {(bomb ? "yes" : "no (box stands in)")}, banners {BannerCount}, layer {Layer} ({LayerMask.LayerToName(Layer)}), board {level}");
        }

        AudioSource NewSource(int priority)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = false; s.spatialBlend = 0f; s.priority = priority; s.dopplerLevel = 0f; s.bypassReverbZones = true; s.ignoreListenerPause = false;
            return s;
        }

        // ------------------------------------------------------------------------------------------------ materials (the studio's templates: their shader variants ship)
        static Material Template(string name, string shader)
        {
            var t = Resources.Load<Material>(ZodStage.MatPath + name);
            Material m = null;
            if (t) m = new Material(t) { hideFlags = HideFlags.DontSave };
            else { var sh = Shader.Find(shader); if (sh) m = new Material(sh) { hideFlags = HideFlags.DontSave }; }
            if (m) madeMats.Add(m);
            return m;
        }
        static Material Lit(Color32 c, float smooth = 0.3f, float metal = 0.15f)
        {
            var m = Template("ZodStage_Lit", "Universal Render Pipeline/Lit"); if (!m) return null;
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal); return m;
        }
        static Material Glow(Color32 baseC, Color linear, float k)
        {
            var m = Template("ZodStage_Glow", "Universal Render Pipeline/Lit"); if (!m) return null;
            m.SetColor("_BaseColor", baseC); m.SetFloat("_Smoothness", 0.4f); m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; m.SetVector("_EmissionColor", (Vector4)(linear * k)); return m;
        }
        static Material Cutout(Texture2D tex)
        {
            var m = Template("ZodStage_Cutout", "Universal Render Pipeline/Lit"); if (!m) return null;
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.2f); m.SetFloat("_Metallic", 0f);
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = 2450; return m;
        }

        Renderer Box(string name, Vector3 pos, Vector3 size, Material m, Transform parent = null, bool cast = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent ? parent : transform, false); go.transform.localPosition = pos; go.transform.localScale = size;
            return Dress(go, m, cast);
        }
        Renderer Quad(string name, Vector3 pos, Vector2 size, Material m, float yawDeg = 180f, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = name; Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent ? parent : transform, false); go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            return Dress(go, m, false);
        }
        Renderer Dress(GameObject go, Material m, bool cast)
        {
            go.layer = Layer;
            var r = go.GetComponent<Renderer>();
            if (m) r.sharedMaterial = m;
            r.renderingLayerMask = LightMask; r.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = true; r.lightProbeUsage = LightProbeUsage.Off;
            return r;
        }

        /// <summary>The room. The robots face +Z, the camera sits at +Z: world +X is screen LEFT. Back wall at z -3.4 with the doors in
        /// the middle, the board left (+X), the banners at the sides, the alcove behind the doors.</summary>
        void BuildSet()
        {
            var steel = Lit(new Color32(34, 38, 44, 255), 0.35f, 0.55f);
            var rib = Lit(new Color32(52, 57, 64, 255), 0.45f, 0.7f);
            var dark = Lit(new Color32(16, 18, 21, 255), 0.4f, 0.4f);
            var hazard = Lit(new Color32(214, 170, 38, 255), 0.35f, 0.1f);
            BuildDeck();                                                                                 // owner item 178: the steel deck floor and its grating strip (OzEvilStage.Deck.cs)
            Box("WallL", new Vector3(3.65f, 2.6f, -3.4f), new Vector3(4.8f, 5.4f, 0.2f), steel);
            Box("WallR", new Vector3(-3.65f, 2.6f, -3.4f), new Vector3(4.8f, 5.4f, 0.2f), steel);
            Box("Lintel", new Vector3(0f, 4.15f, -3.4f), new Vector3(2.5f, 2.3f, 0.2f), steel);
            Box("SideWallL", new Vector3(6.0f, 2.6f, -0.4f), new Vector3(0.2f, 5.4f, 7.2f), steel);
            Box("SideWallR", new Vector3(-6.0f, 2.6f, -0.4f), new Vector3(0.2f, 5.4f, 7.2f), steel);
            Box("Ceiling", new Vector3(0f, 5.3f, -0.9f), new Vector3(14f, 0.1f, 9.6f), dark, null, false);
            foreach (float x in new[] { -5.2f, -3.9f, -2.6f, 2.6f, 3.9f, 5.2f }) Box("Rib" + x, new Vector3(x, 2.6f, -3.26f), new Vector3(0.12f, 5.4f, 0.08f), rib);
            Box("DoorFrameL", new Vector3(1.32f, 1.5f, -3.3f), new Vector3(0.14f, 3.0f, 0.3f), rib);
            Box("DoorFrameR", new Vector3(-1.32f, 1.5f, -3.3f), new Vector3(0.14f, 3.0f, 0.3f), rib);
            Box("DoorFrameTop", new Vector3(0f, 3.05f, -3.3f), new Vector3(2.78f, 0.14f, 0.3f), rib);
            // the sliding doors (two leaves, each 1.25 wide), a hazard stripe at the meeting edges
            var doorM = Lit(new Color32(46, 50, 58, 255), 0.4f, 0.65f);
            doorL = new GameObject("DoorL").transform; doorL.SetParent(transform, false); doorL.localPosition = new Vector3(0.625f, 1.5f, -3.42f);
            doorR = new GameObject("DoorR").transform; doorR.SetParent(transform, false); doorR.localPosition = new Vector3(-0.625f, 1.5f, -3.42f);
            foreach (var d in new[] { doorL, doorR })
            {
                Box("Leaf", Vector3.zero, new Vector3(1.25f, 2.98f, 0.12f), doorM, d);
                for (int i = -2; i <= 2; i++) Box("Groove" + i, new Vector3(0f, i * 0.55f, 0.065f), new Vector3(1.1f, 0.04f, 0.01f), dark, d, false);
                HazardBox("Stripe", new Vector3(d == doorL ? -0.55f : 0.55f, 0f, 0.07f), new Vector3(0.1f, 2.6f, 0.01f), d, false);   // worn hazard stripes (OzEvilStage.Deck.cs)
            }
            // the alcove behind the doors
            Box("AlcoveBack", new Vector3(0f, 2.6f, -5.9f), new Vector3(3.2f, 5.4f, 0.2f), steel);
            Box("AlcoveL", new Vector3(1.55f, 2.6f, -4.65f), new Vector3(0.2f, 5.4f, 2.6f), steel);
            Box("AlcoveR", new Vector3(-1.55f, 2.6f, -4.65f), new Vector3(0.2f, 5.4f, 2.6f), steel);
            HazardBox("AlcoveLip", new Vector3(0f, 0.08f, -3.5f), new Vector3(2.5f, 0.16f, 0.3f));
            // owner item 107: the banners of his poster behind the robots in every framing, moving cloth (OzEvilStage.Banners.cs); the round
            // emblem (the poster's eagle-and-star) over the doors
            BuildBanners(rib, hazard);
            var emTex = Resources.Load<Texture2D>(SetFolder + "evil_emblem");
            if (emTex) Quad("WallEmblem", new Vector3(0f, 4.2f, -3.28f), new Vector2(1.5f, 1.5f), Cutout(emTex));
            // the tactical board (screen left) and the console under it
            Box("BoardFrame", new Vector3(2.35f, 2.05f, -3.26f), new Vector3(1.5f, 1.3f, 0.07f), dark);
            boardMat = Glow(new Color32(6, 8, 12, 255), new Color(0.75f, 0.85f, 1f), 0.9f);
            boardScreen = Quad("BoardScreen", new Vector3(2.35f, 2.05f, -3.216f), new Vector2(1.36f, 1.16f), boardMat).transform;
            BuildBoardHood();                                                                            // owner item 178: a steel hood with a dim warm strip over the board (OzEvilStage.Dressing.cs)
            BuildConsole();                                                                              // owner item 178: the console as a military instrument cabinet (OzEvilStage.Dressing.cs)
            BuildDressing();                                                                             // owner item 147: the wall behind BS-01 (OzEvilStage.Dressing.cs)
            BuildBsBackdrop(rib, hazard);                                                               // owner item 178b: banners and the emblem where BS-01's camera looks (OzEvilStage.BsBackdrop.cs)
        }

        public void SetBoardLevel(string level)
        {
            if (string.IsNullOrEmpty(level)) level = "L02";
            if (level == BoardLevel || !boardMat) return;
            BoardLevel = level;
            Texture2D tex = null;
            try { tex = OzLevelInfo.Load(level)?.Mini; } catch (Exception e) { Debug.LogWarning("[EvilZod] board map " + level + ": " + e.Message); }
            boardMat.SetTexture("_EmissionMap", tex); boardMat.SetTexture("_BaseMap", tex);
            if (tex && boardScreen) { float h = 1.16f, w = Mathf.Clamp(h * tex.width / Mathf.Max(1f, tex.height), 0.8f, 1.5f); boardScreen.localScale = new Vector3(w, h, 1f); }
        }

        // ------------------------------------------------------------------------------------------------ lights
        Light AddLight(string name, LightType type, Vector3 pos, Vector3 aim, Color c, float intensity, float range, float angle = 50f, bool shadows = false, Transform parent = null)
        {
            var go = new GameObject(name) { layer = Layer };
            go.transform.SetParent(parent ? parent : transform, false); go.transform.localPosition = pos;
            var pw = go.transform.parent; if (aim != pos) go.transform.rotation = Quaternion.LookRotation(pw.TransformPoint(aim) - pw.TransformPoint(pos));
            var l = go.AddComponent<Light>();
            l.type = type; l.color = c; l.intensity = intensity; l.range = range;
            if (type == LightType.Spot) { l.spotAngle = angle; l.innerSpotAngle = angle * 0.5f; }
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            if (shadows) { l.shadowStrength = 0.8f; l.shadowBias = 0.02f; l.shadowNormalBias = 0.3f; l.shadowNearPlane = 0.2f; }
            l.renderingLayerMask = (int)LightMask;
            var ld = l.GetUniversalAdditionalLightData();
            if (ld) { ld.renderingLayers = LightMask; ld.shadowRenderingLayers = LightMask; }
            return l;
        }

        void BuildLights()
        {
            BuildCharacterLights();
            consoleGlow = AddLight("ConsoleGlow", LightType.Point, new Vector3(2.35f, 1.3f, -2.5f), new Vector3(2.35f, 1.3f, -2.5f), new Color(0.35f, 0.55f, 1f), 2.6f, 3.2f);
            foreach (float x in new[] { -1.85f, -3.9f, 4.55f })
                banners.Add(AddLight("BannerUp" + x, LightType.Spot, new Vector3(x, 0.2f, -2.5f), new Vector3(x, 3.4f, -3.3f), new Color(1f, 0.25f, 0.18f), 7f, 4.5f, 60f));
            foreach (float x in new[] { 0.9f, -0.9f })
            {
                var l = AddLight("AlcoveSpot" + x, LightType.Spot, new Vector3(x, 4.6f, -4.4f), new Vector3(0f, 1.1f, -4.5f), new Color(0.8f, 0.9f, 1f), 0f, 7f, 55f, true);
                alcove.Add(l);
            }
            beaconRoot = new GameObject("BeaconRoot") { layer = Layer }.transform; beaconRoot.SetParent(transform, false); beaconRoot.localPosition = new Vector3(0f, 4.6f, -1.6f);
            beacon = AddLight("Beacon", LightType.Spot, Vector3.zero, new Vector3(2.5f, -3f, 1.5f), new Color(1f, 0.12f, 0.08f), 0f, 11f, 50f, false, beaconRoot);
        }

        // ------------------------------------------------------------------------------------------------ camera + grade
        void BuildCamera()
        {
            var go = new GameObject("EvilStageCamera") { layer = Layer };
            go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = 1 << Layer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.025f, 0.035f, 1f);
            cam.fieldOfView = 32f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 40f; cam.allowHDR = true; cam.allowMSAA = true; cam.depth = -20f; cam.useOcclusionCulling = false;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; data.antialiasingQuality = AntialiasingQuality.High;
            data.volumeLayerMask = 1 << Layer; data.renderShadows = true; data.stopNaN = true; data.dithering = true; data.requiresColorOption = CameraOverrideOption.Off;
            // the grade: a LOCAL volume around this set only (the commander's studio shares the layer: its own global volume stays its own)
            var vgo = new GameObject("EvilStageVolume") { layer = Layer };
            vgo.transform.SetParent(transform, false); vgo.transform.localPosition = new Vector3(0f, 2f, -1f);
            var col = vgo.AddComponent<BoxCollider>(); col.isTrigger = true; col.size = new Vector3(30f, 20f, 30f);
            vol = vgo.AddComponent<Volume>(); vol.isGlobal = false; vol.blendDistance = 0f; vol.priority = 60f;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>(); prof.name = "EvilStage"; prof.hideFlags = HideFlags.DontSave;
            prof.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var bl = prof.Add<Bloom>(true); bl.threshold.Override(0.9f); bl.intensity.Override(0.9f); bl.scatter.Override(0.6f); bl.tint.Override(new Color(0.85f, 0.9f, 1f));
            var vg = prof.Add<Vignette>(true); vg.intensity.Override(0.32f); vg.smoothness.Override(0.45f); vg.color.Override(Color.black);
            var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(0.3f); ca.contrast.Override(12f); ca.saturation.Override(2f);
            dof = prof.Add<DepthOfField>(true); dof.mode.Override(DepthOfFieldMode.Gaussian); dof.gaussianStart.Override(4f); dof.gaussianEnd.Override(9f); dof.gaussianMaxRadius.Override(0.8f); dof.highQualitySampling.Override(true);
            vol.sharedProfile = prof;
        }

        public RenderTexture EnsureFeed(int w, int h)
        {
            w = Mathf.Clamp(w, 160, 1600); h = Mathf.Clamp(h, 120, 1000);
            if (Feed && Feed.width == w && Feed.height == h) return Feed;
            if (Feed) { if (cam && cam.targetTexture == Feed) cam.targetTexture = null; Feed.Release(); Destroy(Feed); }
            Feed = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "EvilFeed", antiAliasing = 4, useMipMap = false };
            Feed.Create();
            if (cam && !OnScreen) { cam.targetTexture = Feed; cam.aspect = w / (float)h; }
            return Feed;
        }

        /// <summary>Rendering on / off. Window mode renders into the feed; full screen (onScreen) renders over the game: the main camera
        /// is masked to nothing meanwhile (it keeps running for everything that reads it) and unmasked by SetLive(false).</summary>
        public void SetLive(bool on, bool onScreen = false)
        {
            if (!cam) return;
            if (on)
            {
                OnScreen = onScreen;
                if (onScreen)
                {
                    cam.targetTexture = null; cam.depth = 60f; cam.rect = new Rect(0f, 0f, 1f, 1f); cam.ResetAspect();
                    var main = Camera.main;
                    if (main && main != cam && !mainMasked) { mainSaved = main; mainMaskSaved = main.cullingMask; main.cullingMask = 0; mainMasked = true; }
                }
                else { if (!Feed) EnsureFeed(640, 480); cam.targetTexture = Feed; cam.depth = -20f; cam.aspect = Feed.width / (float)Feed.height; }
            }
            else if (mainMasked) { if (mainSaved) mainSaved.cullingMask = mainMaskSaved; mainMasked = false; mainSaved = null; }
            cam.enabled = on;
            if (vol) vol.enabled = on;
            if (ez != null) ez.SetActive(on && ezOnStage); if (sk != null) sk.SetActive(on && skOnStage);
            SetRunnersActive(on);
            if (on) { ExcludeFromOtherCameras(); nextCamScan = Time.unscaledTime + 2f; }
            else { StopLoop(); }
        }

        void ExcludeFromOtherCameras()
        {
            int bit = 1 << Layer;
            foreach (var c in Camera.allCameras) if (c && c != cam && (c.cullingMask & bit) != 0) c.cullingMask &= ~bit;
        }

        // ================================================================================================ scenes
        /// <summary>The nuke scene's marks (fix 2: nothing changes hands, the box never turns). EvilZod stands at NukeEzMark and turns to
        /// PressYaw for the press; the button stand's dome is PressReach ahead of his root along that facing, in line with his right shoulder
        /// (ezShoulderOut to his right), so the slam comes straight down the right arm in his bow (73-90 % of the reach, the knees 0.22 m
        /// short of the box). The sidekick sets the box on the stand from SkStandMark, facing SetYaw (three-quarter to the lens: his face
        /// 40 deg off it while he looks up at EvilZod), the box's bottom centre CarryBox.z ahead of him; he walks in from SkEntry (the wing,
        /// screen left and upstage: out of the bomb and EvilZod framings until the cut to the two-shot).</summary>
        public static readonly Vector3 NukeEzMark = new Vector3(-1.35f, 0f, 0.1f), SkEntry = new Vector3(2.6f, 0f, -1.4f);
        public static float PressYaw = 38f, PressReach = 0.84f, SetYaw = -25f;
        /// <summary>The dome's top on the stand (stage frame).</summary>
        public Vector3 DomePoint { get { var p = NukeEzMark + Quaternion.Euler(0f, PressYaw, 0f) * new Vector3(ezShoulderOut, 0f, PressReach); p.y = StandTop + DomeTopLocal.y; return p; } }
        /// <summary>The box's bottom centre on the stand (its front toward SetYaw: the dome on its back half, toward the sidekick).</summary>
        public Vector3 StandBoxBottom { get { var p = DomePoint - Quaternion.Euler(0f, SetYaw, 0f) * DomeTopLocal; p.y = StandTop; return p; } }
        public Vector3 StandPos { get { var p = StandBoxBottom; p.y = 0f; return p; } }
        /// <summary>The sidekick's mark behind the stand: the box's bottom centre is CarryBox.z ahead of him when he sets it down.</summary>
        public Vector3 SkStandMark { get { var p = StandBoxBottom - Quaternion.Euler(0f, SetYaw, 0f) * Vector3.forward * (CarryBox.z * SkHeight / 1.45f); p.y = 0f; return p; } }

        /// <summary>A scene begins: the robots at their marks, the props where the scene wants them, the camera on its first framing.</summary>
        public void BeginScene(OzBlueScene s, bool onScreen)
        {
            scene = s; fullScreen = onScreen; clock = 0f; Pressed = false;
            doorsOpen = spotsOn = alarmOn = false; doorK = spotK = 0f; doorAt = spotAt = -10f;
            if (doorL) doorL.localPosition = new Vector3(0.625f, 1.5f, -3.42f); if (doorR) doorR.localPosition = new Vector3(-0.625f, 1.5f, -3.42f);
            foreach (var l in alcove) l.intensity = 0f; if (beacon) beacon.intensity = 0f;
            bool nuke = s != null && s.id == OzBlueScenes.WinNuke;
            ResetEvilZod(nuke ? NukeEzMark : new Vector3(-0.55f, 0f, 0f));
            ResetSidekick(nuke ? SkEntry : new Vector3(0.9f, 0f, 0.3f), !nuke);
            PlaceStand(nuke);
            ResetBombLaunch();                                                                           // the World-Domination-inator back on its cradle
            ButtonOffstage();
            GunHidden();                                                                                 // LOSE: in his hands from the cut on "Fine" (gun_ready)
            DisposeRunners();                                                                            // LOSE builds its fleeing lookalikes on its own beat
            SetWallBeacons(s != null && s.id == OzBlueScenes.Lose);                                      // the alarm beacons: LOSE only
            BeginLavaWalk(s);                                                                            // level 7: this one scene walks on the real lava (OzEvilStage.L07Walk.cs)
            ResetCamera();
            Debug.Log($"[EvilZod] scene {(s != null ? s.id : "none")} begins ({(onScreen ? "full screen" : "window")}): {Status()}");
        }

        /// <summary>The director's beats (OzBlueScenes).</summary>
        public void Beat(OzBlueBeat b)
        {
            if (b == null) return;
            switch (b.who)
            {
                case "cam": CamBeat(b); break;
                case "set": SetBeat(b); break;
                case "ez": EvilZodBeat(b); break;
                case "sk": SidekickBeat(b); break;
            }
        }

        void SetBeat(OzBlueBeat b)
        {
            switch (b.beat)
            {
                case "doors_open":
                    // part over DoorSeconds, eased in and out; the slide sounds start with the movement (TickSet), pitched down to its length
                    doorsOpen = true; doorAt = clock; doorSlideSound = false; doorStopSound = false;
                    Sfx("OpenZ/Audio/Sfx/sd_door_heavy_close_b", -12f, 0.7f, "doors_unlock");
                    break;
                case "spots_on":
                    spotsOn = true; spotAt = clock;
                    Sfx("OpenZ/Audio/Sfx/sd_lamp_spark_a", -12f, 0.8f, "spots_on"); Sfx("OpenZ/Audio/Sfx/sd_turret_spin_whoosh", -18f, 0.7f, "spots_sweep", sfxB);
                    break;
                case "alarm_on":
                    alarmOn = true;
                    StartLoop("OpenZ/Audio/Sfx/sd_alarm_klaxon", AlarmDb, 0.9f, "alarm_klaxon"); alarmDuck = 0f;
                    break;
                case "alarm_off": alarmOn = false; StopLoop(); break;
                case "clones_run": StartRunners(); break;                                               // LOSE: the army flees at the back (OzEvilStage.Runners.cs)
            }
        }

        // ------------------------------------------------------------------------------------------------ frame
        void LateUpdate()
        {
            float dt = frozen ? 0f : Mathf.Min(OzBlueCameo.Step, 0.1f);                                 // the take's step while a take records
            clock += dt;
            TickSet(dt);
            TickBanners(dt);
            if (ez != null) TickEvilZod(dt);
            if (sk != null) TickSidekick(dt);
            TickLavaWalk(dt);
            TickRunners(dt);
            TickButton(dt);
            TickBombLaunch(dt);
            PlaceCharacterLights();
            PlaceFloorShadows();                                                                        // item 161: the robots' soft shadows on the floor, after their poses and lights (OzEvilStage.FloorShadows.cs)
            if (cam && cam.enabled) PlaceCamera(dt);
            if (cam && cam.enabled && Time.unscaledTime > nextCamScan) { nextCamScan = Time.unscaledTime + 2f; ExcludeFromOtherCameras(); }
        }

        void TickSet(float dt)
        {
            if (doorL && doorR)
            {
                float goal = doorsOpen ? 1f : 0f, u = Mathf.Clamp01((clock - doorAt - DoorPause) / DoorSeconds), e = u * u * (3f - 2f * u);
                doorK = doorsOpen ? e : Mathf.MoveTowards(doorK, goal, dt / 1.5f);
                if (doorsOpen && !doorSlideSound && clock - doorAt >= DoorPause)
                {   // every visible movement has its sound: the heavy slide and the hydraulics, pitched down to last the slide (2.3 / 2.2 s clips)
                    doorSlideSound = true;
                    Sfx("OpenZ/Audio/Sfx/sd_door_heavy_open_a", -7f, 2.3f / DoorSeconds, "doors_open");
                    Sfx("OpenZ/Audio/Sfx/sd_crane_arm_hydraulic", -15f, 2.2f / DoorSeconds, "doors_slide", sfxB);
                }
                if (doorsOpen && !doorStopSound && u >= 1f) { doorStopSound = true; Sfx("OpenZ/Audio/Sfx/sd_door_heavy_close_a", -14f, 0.75f, "doors_stop", sfxB); }
                doorL.localPosition = new Vector3(0.625f + 1.28f * doorK, 1.5f, -3.42f); doorR.localPosition = new Vector3(-0.625f - 1.28f * doorK, 1.5f, -3.42f);
            }
            if (alcove.Count > 0)
            {   // the spotlights snap on with a flicker, then settle at 26
                float u = spotsOn ? Mathf.Clamp01((clock - spotAt) / 0.5f) : 0f;
                float flick = u < 1f ? (Mathf.PerlinNoise(clock * 40f, 1.3f) > 0.45f ? 1f : 0.25f) : 1f;
                spotK = spotsOn ? u * flick : 0f;
                foreach (var l in alcove) l.intensity = 26f * spotK;
            }
            if (beacon)
            {
                beacon.intensity = alarmOn ? 9f * (0.55f + 0.45f * Mathf.Max(0f, Mathf.Sin(clock * 5.2f))) : 0f;
                if (alarmOn && beaconRoot) beaconRoot.localRotation = Quaternion.Euler(0f, clock * 150f, 0f);
                if (rim) rim.color = alarmOn ? Color.Lerp(new Color(1f, 0.45f, 0.3f), new Color(1f, 0.15f, 0.1f), 0.5f + 0.5f * Mathf.Sin(clock * 5.2f)) : new Color(1f, 0.45f, 0.3f);
                if (alarmOn && loopSrc && loopSrc.isPlaying && dt > 0f)
                {
                    bool talk = (ez != null && EzMouth > 0.04f) || SkMouth > 0.04f;
                    alarmDuck = Mathf.MoveTowards(alarmDuck, talk ? 1f : 0f, dt / (talk ? 0.12f : 0.6f));
                    loopSrc.volume = Mathf.Clamp01(VoiceGain * Mathf.Pow(10f, (AlarmDb - AlarmDuckDb * alarmDuck) / 20f));
                }
            }
            if (lamps.Count > 0 && clock >= nextLampAt && dt > 0f)
            {   // the console lamps change now and then
                nextLampAt = clock + UnityEngine.Random.Range(0.25f, 0.9f);
                var m = lamps[UnityEngine.Random.Range(0, lamps.Count)];
                ConsoleLampBlink(m);                                                                    // dim amber / dull green, the same two random draws (OzEvilStage.Dressing.cs)
            }
            if (consoleGlow && dt > 0f) consoleGlow.intensity = 2.6f * (0.9f + 0.1f * Mathf.PerlinNoise(clock * 2.3f, 0.7f));
        }

        // ------------------------------------------------------------------------------------------------ sounds
        AudioClip ClipOf(string res)
        {
            if (!clipCache.TryGetValue(res, out var c)) clipCache[res] = c = Resources.Load<AudioClip>(res);
            return c;
        }
        /// <summary>One of the game's own clips, 2D, at the voice's gain + db (every visible action has its sound; OzSoundLog).</summary>
        public void Sfx(string res, float db, float pitch = 1f, string key = null, AudioSource src = null)
        {
            var c = ClipOf(res);
            key ??= System.IO.Path.GetFileName(res);
            if (!c) { OzSoundLog.Add("EvilZod", key, res, "missing clip"); return; }
            src ??= sfxA;
            float g = Mathf.Clamp01(VoiceGain * Mathf.Pow(10f, db / 20f));
            src.pitch = pitch; src.PlayOneShot(c, g);
            OzSoundLog.Add("EvilZod", key, c.name, "played", g);
        }
        public void SfxRandom(string[] res, float db, float pitchMin, float pitchMax, string key, AudioSource src = null)
        {
            if (res == null || res.Length == 0) return;
            Sfx(res[UnityEngine.Random.Range(0, res.Length)], db, UnityEngine.Random.Range(pitchMin, pitchMax), key, src);
        }
        void StartLoop(string res, float db, float pitch, string key)
        {
            var c = ClipOf(res); if (!c || !loopSrc) { OzSoundLog.Add("EvilZod", key, res, "missing clip"); return; }
            loopSrc.clip = c; loopSrc.pitch = pitch; loopSrc.volume = Mathf.Clamp01(VoiceGain * Mathf.Pow(10f, db / 20f)); loopSrc.Play();
            OzSoundLog.Add("EvilZod", key, c.name, "loop on", loopSrc.volume);
        }
        void StopLoop() { if (loopSrc && loopSrc.isPlaying) { loopSrc.Stop(); OzSoundLog.Add("EvilZod", "alarm_klaxon", loopSrc.clip ? loopSrc.clip.name : "", "loop off"); } }
        public AudioSource StepSource => sfxB;

        /// <summary>The Play checks / the redo lab: the robots' rigs and the stage camera (null when missing).</summary>
        public OzPoseRig EzRig => ez != null && ez.Ok ? ez.Rig : null;
        public OzPoseRig SkRig => sk != null && sk.Ok ? sk.Rig : null;
        public Camera StageCam => cam;
        public string Status() => $"EvilZod {(ez != null ? ez.Status() : "none")}; sidekick {(sk != null ? sk.Status() : "none")}; cam {camName} fov {(cam ? cam.fieldOfView : 0f):F1}; doors {doorK:F2}; spots {spotK:F2}; alarm {alarmOn}; pressed {Pressed}";

        void OnDestroy()
        {
            LavaBedOff();
            ez?.Dispose(); sk?.Dispose(); DisposeBanners(); DisposeRunners(); DisposeKit();
            if (mainMasked && mainSaved) mainSaved.cullingMask = mainMaskSaved;
            if (cam) cam.targetTexture = null;
            if (Feed) { Feed.Release(); Destroy(Feed); }
            if (vol && vol.sharedProfile) Destroy(vol.sharedProfile);
            bool last = true;
            foreach (var o in FindObjectsByType<OzEvilStage>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (o && o != this) { last = false; break; }
            if (last) { foreach (var m in madeMats) if (m) Destroy(m); madeMats.Clear(); }
        }
    }
}
