// OpenZ (GPL-3.0) - Level 7: Brad and Alan in the hot tub, out on the lava. The red side's mission-start briefing, in the transmission window like every
// briefing (never full screen): the window shows this set through its own camera while the whole screen flies the level's fly-over. It stands
// on the level's real lava (OzLava's sheet is what the camera sees), drawn only while the briefing runs: nothing here is a rule, a collider or a
// save. It belongs to the window alone, four locks: (1) every object of it stands on the ZodCameo layer, which no other camera draws; (2) a build that fails leaves nothing behind (Scrap); (3) while no briefing runs every renderer is off, checked every frame;
// (4) OnlyInTheWindow in the camera events, for the lights, which no layer stops. Counted in Play: OzL07HotTubSet.MainCanDraw() = 0 at every moment. The same method as OzL16LoungerSet (the two robots' prefabs, clips by a two-input mixer, the jaw from the voice inside each speaker's
// own cues, the window fed through ZodStage.LoungerFeed); ZodBriefing.BoysHost hands it the line with "host": "boys".
// The story, on the voice clip's clock (Resources/OpenZ/L07Cut/l07_tub_scene.json, shots of item 12's shot list):
//   wide on the lava, push into the tub; Alan; his view: a blue soldier wading forward, shins in the lava, hot metal legs (the LV3 look of TeamLit at its full heat: yellow-orange at the line, band under the hips, body skin only; never the melting robot's flat glow), a dark
//   collar, ripples and smoke at each leg; Brad points at him on "See?"; on "all right" the CRASH ZOOM, about 0.3 s, across the lava to the blue forearm standing out of it, thumb up;
//   the lens lands over the hand looking down, so the bright lava lies behind the whole hand, and is LOCKED (no drift, no hand-held); the arm is already sinking on the crash shot's first frame (no still hold), straight down at one slow even speed, about 4.7 s,
//   thumb never bends (a rigid model of the real robot's arm), the thumb tip goes last, the ring closes and one lava pop bursts as the tip goes under; a beat; cut back to the tub for "Told you he's fine.": both of them in the tub, one calm hand-held shot to the end of the line, no zoom on Brad (the last shot: past its t1 its lens holds)
// Play check: OzL07HotTubSet.Status().
// WRITTEN BY READING ONLY: compiled off-line with the project's code, never run in the engine. Remove this line once it runs.
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    [Serializable] public class OzTubPiece { public string name, mesh, baseMap; public float spinDeg; }
    [Serializable] public class OzTubRobot { public string who, body, bodyNode; public float x, y, z, yaw; public OzLoungerCan[] cans; }
    /// <summary>kind: push (the lens narrows to zoomFov), hand (a small hand-held drift), walker (aims at the wading robot), crash (from the shot's own lens
    /// to the landing l* in landSeconds, then LOCKED), face (a crash zoom from the shot's own lens to the landing l*, aimed at who's head, then hand-held).</summary>
    [Serializable] public class OzTubShot { public string name, kind, who; public float t0, t1, px, py, pz, tx, ty, tz, fov, zoomFov, landSeconds, lpx, lpy, lpz, ltx, lty, ltz, lfov; }
    [Serializable] public class OzTubBeat { public float t, fade; public string who, clip; }
    [Serializable] public class OzTubSound { public float t, db, pitch; public string clip, key; }
    /// <summary>heatA / heatB: the legs' heat 0..1, topA / topB: the height of the hot band over the lava (m). Model front = -Z, as every unit (UnitView.modelYaw).</summary>
    [Serializable] public class OzTubWalker { public string prefab, take; public float ax, az, bx, bz, t0, t1, sinkA, sinkB, heatA, heatB, topA, topB, walkRate, legSize; }
    /// <summary>heatTop: the hot band over the lava (m). back*: the soft bright pool of lava laid behind the hand as the lens sees it: centre, size along and across the view, turn, gain. It is a round glow, radius backWide / 2 from that centre (shape 6 has no other form); the quad's extra length stays empty.
    /// ring*: where the lava cuts the rigid arm, by height under the thumb tip (ringH, rising): the cut's middle in set axes from the arm's origin, its half width (tools/16_arm_sections.py). The ring and the glow stand on it, so they close on the thumb, not beside it.</summary>
    [Serializable] public class OzTubArm { public string res, baseMap; public float x, z, viewDeg, tipStart, under, t0, t1, heatA, heatB, heatTop, popT, backX, backZ, backLen, backWide, backYaw, backGain, ringGain, ringThin, glowGain, glowWide, lightBack, lightUp, lightPower; public float[] ringH, ringX, ringZ, ringR; }
    /// <summary>The set's own lights and steam. up*: the lava's glow on the tub, on the lava side of the island and the far side from cone 02: never near a surface, a point light makes a burnt patch there (tools/19_fx_numbers.py counts it on the real triangles of the cone and the island). rock*: the lava's glow on cone 02's face in the first shot, so the rock reads against the crust; only while that shot runs. steam*: thin low wisps off the water: rise and size keep them under the robots' shoulders (checklist: nothing stands in front of the main subject). walkerGlow*: the soft glow at the wader's legs, from the start of his walk to its end; popFlash: the lava pop's flash at its first frame.</summary>
    [Serializable] public class OzTubLights { public float upX, upY, upZ, upPower, upRange, rockX, rockY, rockZ, rockPower, rockRange, steamRate, steamSize, steamLife, steamRise, steamAlpha, walkerGlowA, walkerGlowB, popFlash; }
    /// <summary>A blend shape of a robot's body on the line's clock: up from t0 to t1, down from t2 to t3 (Brad's pointing finger).</summary>
    [Serializable] public class OzTubShape { public string who, shape; public float t0, t1, t2, t3; }
    [Serializable]
    public class OzTubFile
    {
        public int version; public string level, setRes; public float placeX, placeZ, yaw, islandLift;
        public OzTubPiece[] pieces; public OzTubRobot[] robots; public OzLoungerClip[] clips; public OzTubBeat[] beats; public OzTubShot[] shots; public OzTubSound[] sounds;
        public OzTubWalker walker; public OzTubArm arm; public OzTubShape[] shapes; public OzTubLights lights;
    }

    [DefaultExecutionOrder(150)]
    public sealed partial class OzL07HotTubSet : MonoBehaviour
    {
        public const string Level = "L07", DataPath = "OpenZ/L07Cut/l07_tub_scene";
        public static OzL07HotTubSet I { get; private set; }
        public static string LastWarning = "";
        /// <summary>Metres and degrees of the hand-held shots' drift (never on the landed thumb shot).</summary>
        public static float Drift = 0.012f, Sway = 0.3f;
        /// <summary>Play checks: the crash landed at this voice second; the thumb tip's height over the lava now; the shot's lens moved since it landed (m, 0 = locked); the wader's sink, his legs' heat, his footfalls.</summary>
        public static float LandedAt = -1f, TipOverLava = -1f, LockedMove, WalkerSink, WalkerHeat; public static int WalkerFootfalls;
        /// <summary>Play check, the failed-build row of kit/PLAY_CHECKS.md: set it before the level starts and Build throws after the tub, island and robots are made, where the old fault threw. Off in the game.</summary>
        public static bool ProofFailBuild;

        sealed class Robot
        {
            public string who; public Transform root, head, mouth; public Animator anim; public SkinnedMeshRenderer body; public Renderer[] rends;
            public Transform[] can = new Transform[2]; public OzLoungerCan[] seat = new OzLoungerCan[2]; public int[] grip = { -1, -1 };
            public Quaternion mouthRest = Quaternion.identity; public float jawSign = 1f, jawDeg = 10f, jaw;
            public PlayableGraph graph; public AnimationMixerPlayable mixer; public AnimationClipPlayable[] slot = new AnimationClipPlayable[2]; public int cur = -1; public float fadeT, fadeDur = 0.01f;
            public OzLoungerClip clip; public readonly List<Vector2> cues = new List<Vector2>();
        }
        struct ShapeTrack { public SkinnedMeshRenderer skin; public int index; public OzTubShape d; }
        readonly List<ShapeTrack> shapeTracks = new List<ShapeTrack>();

        OzTubFile data; bool built, buildTried, drawn = true, briefOn; ZGame game; Transform root; Camera cam; AudioSource sfx;
        readonly List<Robot> robots = new List<Robot>(); readonly List<Material> mats = new List<Material>(); readonly List<Renderer> hidden = new List<Renderer>();
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(); readonly Dictionary<string, Transform> pieces = new Dictionary<string, Transform>();
        const float ClosingMost = 4f;
        ZodStage stage; AudioSource voice; Func<float> clock; float voiceGain = 1f, lastT = -99f, shotAt, overFor; int beatI, soundI, shotI = -1; readonly float[] jawBuf = new float[256];
        readonly List<OzTubBeat> beats = new List<OzTubBeat>(); readonly List<OzTubSound> sounds = new List<OzTubSound>();
        // the wading soldier
        Transform walker; Renderer[] walkerRends; SkinnedMeshRenderer[] walkerSkins; TeamPaint walkerPaint; PlayableGraph walkerGraph; AnimationClipPlayable walkerTake; Transform walkerHead; MeshRenderer walkerGlow; bool walkerShown = true; Vector3 walkerAim, faceAim; bool walkerAimHas, faceAimHas; double walkerLen = 1.0; OzL07CutFx.Wade walkerWade; float walkerHeat, walkerTop;
        // the arm
        Transform arm; MeshRenderer armR; MaterialPropertyBlock armBlock; MeshRenderer armGlow, armRing, armBack; bool armShown = true, popDone; OzL07CutFx.Pop pop; Light armLight, rockLight; Vector3 landedAt; MaterialPropertyBlock legBlock;
        // the tub's life
        readonly List<Transform> bubbles = new List<Transform>(); readonly List<float> bubbleT = new List<float>(), bubbleLife = new List<float>(); ParticleSystem steam; AudioSource bed; Transform water; float waterSpin; float lavaY; System.Random rng = new System.Random(1907);
        static readonly int TeamColorId = Shader.PropertyToID("_TeamColor");

        public bool Ready => EnsureBuilt();
        public bool BriefingOn => briefOn;
        public float Mouth01 { get { float m = 0f; foreach (var r in robots) m = Mathf.Max(m, r.jaw); return m; } }
        public Camera WindowCamera => cam;
        public string ShotName => shotI >= 0 && data != null ? data.shots[shotI].name : "";

        /// <summary>The set of this level, or null.</summary>
        public static OzL07HotTubSet For(string level) => level == Level ? I : null;

        // ------------------------------------------------------------------------------------------------ attach (level L07, red side)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; LastWarning = ""; LandedAt = TipOverLava = -1f; LockedMove = WalkerSink = WalkerHeat = 0f; WalkerFootfalls = 0; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterFirstScene() { TryAttach(SceneManager.GetActiveScene()); }
        static void OnScene(Scene s, LoadSceneMode m) { if (m == LoadSceneMode.Single) TryAttach(s); }

        static void TryAttach(Scene s)
        {
            if (!Application.isPlaying || I) return;
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!g || g.level != Level || OzPlayBlue.Active) return;
            var go = new GameObject("OzL07HotTubSet");
            if (s.IsValid() && go.scene != s) SceneManager.MoveGameObjectToScene(go, s);
            go.AddComponent<OzL07HotTubSet>();
        }

        void Awake() { I = this; game = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>(); legBlock = new MaterialPropertyBlock(); BindEnding(); }   // Unity forbids making a block in a field initializer: AddComponent throws. The ending binds here: the set is built only when a line asks for it

        void OnDestroy()
        {
            if (I == this) I = null;
            UnbindEnding();
            OnlyInTheWindow(false);
            foreach (var r in robots) if (r.graph.IsValid()) r.graph.Destroy();
            if (walkerGraph.IsValid()) walkerGraph.Destroy();
            foreach (var m in mats) if (m) Destroy(m);
            if (stage && briefOn) stage.LoungerFeed(null);
        }

        static void Warn(string s) { LastWarning = s; Debug.LogWarning("[OpenZ L07 tub] " + s); }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++) { var f = FindDeep(t.GetChild(i), name); if (f) return f; }
            return null;
        }

        // ------------------------------------------------------------------------------------------------ build
        bool EnsureBuilt()
        {
            if (built) return true;
            if (buildTried) return false;
            buildTried = true;
            try { built = Build(); }
            catch (Exception e) { Debug.LogException(e); built = false; }
            if (!built) Scrap();
            return built;
        }

        /// <summary>A build that stopped half way: the tub and the robots already stood on the lake with every renderer on, nothing would ever hide them and they showed on the game camera for the whole match
        /// </summary>
        void Scrap()
        {
            foreach (var r in robots) if (r.graph.IsValid()) r.graph.Destroy();
            robots.Clear();
            if (walkerGraph.IsValid()) walkerGraph.Destroy();
            if (root) { root.gameObject.SetActive(false); Destroy(root.gameObject); root = null; }
            cam = null;
            Warn("the set could not be built: nothing of it stays in the level, the briefing plays in the HQ");
        }

        Material Mat(string name, string baseRes)
        {   // the game's own lit surface with the model's picture (the recipe of OzL16LoungerSet.Mat)
            var m = new Material(DetailKit.Lit) { name = name, hideFlags = HideFlags.DontSave };
            var tex = Resources.Load<Texture2D>(baseRes);
            if (tex) { if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); else m.mainTexture = tex; } else Warn("picture " + baseRes + " missing");
            DetailKit.F(m, "_UseVertexColor", 0f); DetailKit.F(m, "_TeamMode", 0f); DetailKit.F(m, "_Degreen", 0f); DetailKit.F(m, "_Stylize", 1f); DetailKit.F(m, "_Degrime", 0f); DetailKit.F(m, "_FlatShade", 0f);
            DetailKit.F(m, "_SoftFill", 0.2f); DetailKit.F(m, "_AmbientBoost", 1f); DetailKit.F(m, "_Smoothness", 0.22f); DetailKit.F(m, "_Metallic", 0f); DetailKit.F(m, "_NightClass", 0f);
            DetailKit.C(m, "_BaseColor", Color.white); DetailKit.C(m, "_EmissionColor", Color.black);
            mats.Add(m);
            return m;
        }

        bool Build()
        {
            var ta = Resources.Load<TextAsset>(DataPath);
            if (!ta) { Warn("data file Resources/" + DataPath + " missing: no hot tub"); return false; }
            data = JsonUtility.FromJson<OzTubFile>(ta.text);
            if (data == null || data.pieces == null || data.robots == null || data.clips == null || data.shots == null) { Warn("data file not readable"); return false; }
            var model = Resources.Load<GameObject>(data.setRes);
            if (!model) { Warn("model Resources/" + data.setRes + " missing: no hot tub"); return false; }
            var pa = Resources.Load<GameObject>(ZodStage.AlanPrefabPath); var pb = Resources.Load<GameObject>(ZodStage.BradPrefabPath);
            if (!pa || !pb) { Warn("the two robots' prefabs are missing"); return false; }
            lavaY = OzLava.I ? OzLava.I.lavaY : -0.3f;
            var mark = GameObject.Find("MRK_lava_level"); if (mark) lavaY = mark.transform.position.y;
            root = new GameObject("L07HotTubSet").transform; root.SetParent(transform, false);
            root.SetPositionAndRotation(new Vector3(data.placeX, lavaY + data.islandLift, data.placeZ), Quaternion.Euler(0f, data.yaw, 0f));

            foreach (var p in data.pieces)
            {
                var src = FindDeep(model.transform, p.mesh);
                if (!src) { Warn("mesh " + p.mesh + " not in " + data.setRes); return false; }
                var go = Instantiate(src.gameObject, root, false); go.name = p.name;
                go.transform.localPosition = Vector3.zero; go.transform.localRotation = src.localRotation; go.transform.localScale = src.localScale;
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
                var m = Mat("L07Tub_" + p.name, p.baseMap);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true; r.gameObject.layer = 0; }
                pieces[p.name] = go.transform;
                if (p.spinDeg != 0f) { water = go.transform; waterSpin = p.spinDeg; }
            }

            var st = FindFirstObjectByType<ZodStage>();
            float bradScale = st ? st.boysModelScale : 0.84f, alanMul = st ? st.alanScale : 1.19f;
            foreach (var rd in data.robots)
            {
                bool alan = rd.who == "alan";
                var go = Instantiate(alan ? pa : pb, root, false);
                go.name = alan ? "L07Tub_Alan" : "L07Tub_Brad";
                go.transform.localPosition = new Vector3(rd.x, rd.y, rd.z); go.transform.localRotation = Quaternion.Euler(0f, rd.yaw, 0f);
                go.transform.localScale = Vector3.one * (alan ? bradScale * alanMul : bradScale);
                var r = new Robot { who = rd.who, root = go.transform, jawDeg = alan ? (st ? st.alanJawDeg : 16f) : (st ? st.bradJawDeg : 10f) };
                r.head = FindDeep(go.transform, "Head"); r.mouth = FindDeep(go.transform, "MOUTH");
                if (r.mouth)
                {   // which way opens the jaw: a point in front of the hinge must drop (as OzL16LoungerSet)
                    r.mouthRest = r.mouth.localRotation;
                    Vector3 front = -go.transform.forward, local = Quaternion.Inverse(r.mouth.rotation) * front * 0.1f;
                    float y0 = r.mouth.TransformPoint(local).y;
                    r.mouth.localRotation = r.mouthRest * Quaternion.Euler(10f, 0f, 0f);
                    r.jawSign = r.mouth.TransformPoint(local).y < y0 ? 1f : -1f;
                    r.mouth.localRotation = r.mouthRest;
                }
                foreach (var tr in go.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 0;
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
                r.rends = go.GetComponentsInChildren<Renderer>(true);
                foreach (var rr in r.rends)
                {
                    rr.renderingLayerMask = 1u; rr.shadowCastingMode = ShadowCastingMode.On; rr.receiveShadows = true; rr.allowOcclusionWhenDynamic = false;
                    if (rr is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
                    rr.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
                }
                var bodyT = FindDeep(go.transform, rd.bodyNode); r.body = bodyT ? bodyT.GetComponent<SkinnedMeshRenderer>() : null;
                if (rd.cans != null && rd.cans.Length > 0 && !string.IsNullOrEmpty(rd.body))
                {   // the can in his hand: the lounger kit's body with the grip shapes, same skeleton, bound by bone name
                    var bm = Resources.Load<GameObject>(rd.body); var srcSmr = bm ? bm.GetComponentInChildren<SkinnedMeshRenderer>(true) : null; bool gripOk = false;
                    if (r.body && srcSmr && srcSmr.sharedMesh)
                    {
                        var bones = new Transform[srcSmr.bones.Length]; bool ok = true;
                        for (int i = 0; i < bones.Length; i++) { bones[i] = srcSmr.bones[i] ? FindDeep(go.transform, srcSmr.bones[i].name) : null; if (!bones[i]) ok = false; }
                        if (ok) { r.body.sharedMesh = srcSmr.sharedMesh; r.body.bones = bones; gripOk = true; }
                    }
                    if (!gripOk) Warn(rd.who + ": the body with the grip shapes is missing: the fingers stay as modelled");
                    foreach (var cd in rd.cans)
                    {
                        int k = cd.hand == "R" ? 1 : 0;
                        r.can[k] = FindDeep(go.transform, cd.node); r.seat[k] = cd;
                        if (!r.can[k]) { Warn(rd.who + ": can node " + cd.node + " missing"); continue; }
                        r.can[k].localPosition = new Vector3(cd.px, cd.py, cd.pz); r.can[k].localRotation = new Quaternion(cd.qx, cd.qy, cd.qz, cd.qw);
                        r.can[k].localScale = Vector3.one * (alan ? 1f / Mathf.Max(0.01f, alanMul) : 1f);
                        r.can[k].gameObject.SetActive(true);
                        if (gripOk) { r.grip[k] = -1; var m = r.body.sharedMesh; for (int i = 0; i < m.blendShapeCount; i++) { string n = m.GetBlendShapeName(i); if (n == cd.grip || n.EndsWith("." + cd.grip)) r.grip[k] = i; } if (r.grip[k] >= 0) r.body.SetBlendShapeWeight(r.grip[k], 100f); }
                    }
                }
                r.anim = go.GetComponentInChildren<Animator>(true);
                if (!r.anim) { Warn(rd.who + ": no Animator on the prefab"); return false; }
                r.anim.runtimeAnimatorController = null; r.anim.applyRootMotion = false; r.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                r.graph = PlayableGraph.Create("L07Tub_" + rd.who); r.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                r.mixer = AnimationMixerPlayable.Create(r.graph, 2);
                AnimationPlayableOutput.Create(r.graph, "pose", r.anim).SetSourcePlayable(r.mixer);
                r.graph.Play();
                robots.Add(r);
            }
            BuildShapes(); BuildCamera(); BuildWalker(); BuildArm(); BuildLife();
            if (ProofFailBuild) throw new InvalidOperationException("ProofFailBuild: the failed-build check of kit/PLAY_CHECKS.md");
            SetLayer = ZodStage.ResolveLayer();                                                             // the layer the game camera never draws: everything of the set, whatever made it
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = SetLayer;
            sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false; sfx.spatialBlend = 0f; sfx.priority = 40;
            bed = gameObject.AddComponent<AudioSource>(); bed.playOnAwake = false; bed.spatialBlend = 0f; bed.loop = true; bed.priority = 60;
            int lost = 0; foreach (var c in data.clips) if (!Resources.Load<AnimationClip>(c.res)) lost++;
            if (lost > 0) Warn(lost + " of " + data.clips.Length + " clips are missing");
            if (data.beats != null) { beats.AddRange(data.beats); beats.Sort((a, b) => a.t.CompareTo(b.t)); }
            if (data.sounds != null) { sounds.AddRange(data.sounds); sounds.Sort((a, b) => a.t.CompareTo(b.t)); foreach (var s in sounds) Clip(s.clip); }
            foreach (var r in robots) Play(r, "sit_loop", 0f);
            Show(false);
            Debug.Log($"[OpenZ L07 tub] built at ({root.position.x:F1}, {root.position.y:F2}, {root.position.z:F1}) heading {data.yaw:F0}, lava at {lavaY:F2}: {pieces.Count} pieces, {robots.Count} robots, {data.clips.Length} clips, {data.shots.Length} shots");
            return true;
        }

        void BuildShapes()
        {   // shapes that run on the line's clock: read off the body each robot wears after the swap above
            if (data.shapes == null) return;
            foreach (var d in data.shapes)
            {
                var r = Find(d.who); var m = r != null && r.body ? r.body.sharedMesh : null; int ix = -1;
                if (m) for (int i = 0; i < m.blendShapeCount; i++) { string n = m.GetBlendShapeName(i); if (n == d.shape || n.EndsWith("." + d.shape)) ix = i; }
                if (ix < 0) { Warn(d.who + ": no shape " + d.shape + " on the body"); continue; }
                shapeTracks.Add(new ShapeTrack { skin = r.body, index = ix, d = d });
            }
        }

        void TickShapes(float t)
        {
            foreach (var s in shapeTracks)
                s.skin.SetBlendShapeWeight(s.index, 100f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.d.t0, s.d.t1, t)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.d.t2, s.d.t3, t))));
        }

        void BuildCamera()
        {
            var cgo = new GameObject("L07TubWindowCamera"); cgo.transform.SetParent(root, false);
            cam = cgo.AddComponent<Camera>(); var main = Camera.main;
            if (main) cam.CopyFrom(main);
            cam.enabled = false; cam.targetTexture = null; cam.depth = -30f; cam.nearClipPlane = 0.1f; cam.farClipPlane = Mathf.Min(main ? main.farClipPlane : 600f, 600f); cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.cullingMask = ((main ? main.cullingMask : ~0) & ~(1 << 5)) | (1 << ZodStage.ResolveLayer()); cam.useOcclusionCulling = false;   // the level, and the set on its own layer
            var cd = cam.GetUniversalAdditionalCameraData(); cd.renderPostProcessing = true; cd.renderShadows = true; cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        void BuildWalker()
        {
            var w = data.walker; if (w == null) return;
            var prefab = game ? game.Prefab(w.prefab) : null;
            if (!prefab) { Warn("no unit model " + w.prefab + ": no walker"); return; }
            walker = new GameObject("TubWalker").transform; walker.SetParent(root, false);
            var go = Instantiate(prefab, walker, false); go.name = "Model";
            walkerPaint = go.GetComponent<TeamPaint>(); if (walkerPaint) walkerPaint.Team = game.player == Team.Red ? Team.Blue : Team.Red;
            var an = go.GetComponentInChildren<Animator>(true);
            AnimationClip take = null;
            if (an && an.runtimeAnimatorController) foreach (var c in an.runtimeAnimatorController.animationClips) if (c && c.name == w.take) take = c;
            if (!an || !take) { Warn("the model " + w.prefab + " has no take " + w.take); Destroy(walker.gameObject); walker = null; return; }
            an.runtimeAnimatorController = null; an.applyRootMotion = false; an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var keep = new List<Renderer>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.motionVectorGenerationMode = MotionVectorGenerationMode.Camera; r.allowOcclusionWhenDynamic = false; keep.Add(r);
            }
            walkerRends = keep.ToArray(); walkerSkins = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);   // the leg heat goes to the body skin only, as OzLavaWade: the rifle stays cold
            walkerHead = an.GetBoneTransform(HumanBodyBones.Head); walkerLen = Mathf.Max(0.1f, take.length);
            walkerGraph = PlayableGraph.Create("L07TubWalker"); walkerGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            walkerTake = AnimationClipPlayable.Create(walkerGraph, take); walkerTake.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(walkerGraph, "walk", an).SetSourcePlayable(walkerTake);
            walkerGraph.Play();
            walkerGlow = OzL07CutFx.Flat(root, "WalkerGlow", 0, 6f, OzL07CutFx.LavaGlow);
            walkerWade = new OzL07CutFx.Wade(root, "Walker", 0, 1u, w.legSize > 0f ? w.legSize : 1f);
            walkerWade.Bind(an.GetBoneTransform(HumanBodyBones.LeftFoot), an.GetBoneTransform(HumanBodyBones.RightFoot));
        }

        void BuildArm()
        {
            var a = data.arm; if (a == null) return;
            var prefab = Resources.Load<GameObject>(a.res);
            if (!prefab) { Warn("the arm Resources/" + a.res + " is missing: no thumbs-up"); return; }
            arm = new GameObject("TubThumbArm").transform; arm.SetParent(root, false);
            var go = Instantiate(prefab, arm, false); go.name = "Model"; go.transform.localPosition = Vector3.zero;
            armR = go.GetComponentInChildren<MeshRenderer>(true);
            var unit = game ? game.Prefab(data.walker != null ? data.walker.prefab : "Z_bs01_grunt") : null;
            Material body = null;
            if (unit) foreach (var s in unit.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.name == "BS01_Body") body = s.sharedMaterial;
            if (armR && body) armR.sharedMaterial = ArmSkin(body, a.baseMap); else Warn("the blue robot's body material was not found: the arm keeps its import material");
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            if (armR) { armR.shadowCastingMode = ShadowCastingMode.On; armR.receiveShadows = true; armR.allowOcclusionWhenDynamic = false; armR.motionVectorGenerationMode = MotionVectorGenerationMode.Camera; }
            var tp = go.AddComponent<TeamPaint>(); tp.Team = game && game.player == Team.Red ? Team.Blue : Team.Red;
            armBlock = new MaterialPropertyBlock();
            armBack = OzL07CutFx.Flat(root, "ArmBack", 0, 6f, OzL07CutFx.LavaGlow);                         // the soft pool of bright lava the lens sees behind the hand: the silhouette stands on it
            if (armR)
            {   // TeamPaint knows a robot by its skin: this rigid arm gets the robots' brighter paint by hand, or it is darker than the soldier it belongs to
                Color tc = TeamColors.Paint(tp.Team).linear; float tg = TeamPaint.RobotTeamGain;
                armR.GetPropertyBlock(armBlock, 0); armBlock.SetColor(TeamColorId, new Color(tc.r * tg, tc.g * tg, tc.b * tg, tc.a)); armR.SetPropertyBlock(armBlock, 0);
            }
            armGlow = OzL07CutFx.Flat(root, "ArmGlow", 0, 6f, OzL07CutFx.LavaGlow); armRing = OzL07CutFx.Flat(root, "ArmRing", 0, 5f, OzL07CutFx.LavaRing);
            if (armRing) DetailKit.F(armRing.sharedMaterial, "_BandInner", a.ringThin);                    // a thin hot line round the arm; the soft glow under it does the rest (the wide band read as a flat yellow disc on the real frames)
            var lg = new GameObject("ArmLight"); lg.transform.SetParent(root, false); armLight = lg.AddComponent<Light>();
            armLight.type = LightType.Point; armLight.color = new Color(1f, 0.42f, 0.13f); armLight.range = 5f; armLight.intensity = 0f; armLight.shadows = LightShadows.None;
            pop = new OzL07CutFx.Pop(root, 0, 14, data.lights != null ? data.lights.popFlash : 0.6f);
        }

        /// <summary>The hand's own picture on the blue robot's own material: alpha is the paint, as on the body's, so TeamPaint paints it in the team colour.</summary>
        Material ArmSkin(Material body, string map)
        {
            var tex = string.IsNullOrEmpty(map) ? null : Resources.Load<Texture2D>(map);
            if (!tex) { if (!string.IsNullOrEmpty(map)) Warn("picture " + map + " missing"); return body; }
            var m = new Material(body) { name = "L07Tub_ArmSkin", hideFlags = HideFlags.DontSave }; m.SetTexture("_BaseMap", tex); mats.Add(m);
            return m;
        }

        void BuildLife()
        {
            if (water)
            {   // the bubbles: small low domes that swell and burst on the water; the steam off it
                var mesh = Dome(); var m = Mat("L07Tub_Bubble", "OpenZ/L07Cut/T_L07_TubWater"); DetailKit.C(m, "_BaseColor", new Color(0.86f, 0.97f, 0.97f, 1f));
                for (int i = 0; i < 16; i++)
                {
                    var go = new GameObject("Bubble" + i); go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
                    bubbles.Add(go.transform); bubbleT.Add(-Rand(0f, 1.5f)); bubbleLife.Add(Rand(0.5f, 1.1f)); go.SetActive(false);
                }
                var q = data.lights;
                steam = OzL07CutFx.Smoke(root, "TubSteam", 0, 1u, new Vector3(0f, WaterH, 0f), 1.3f, q.steamRate, q.steamSize, q.steamLife, q.steamRise, new Color(0.9f, 0.92f, 0.92f, q.steamAlpha));
            }
            var d = data.lights;
            var lg = new GameObject("LavaUplight"); lg.transform.SetParent(root, false); lg.transform.localPosition = new Vector3(d.upX, d.upY, d.upZ);
            var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.42f, 0.14f); l.range = d.upRange; l.intensity = d.upPower; l.shadows = LightShadows.None;
            var lr = new GameObject("RockLight"); lr.transform.SetParent(root, false); lr.transform.localPosition = new Vector3(d.rockX, d.rockY, d.rockZ);
            rockLight = lr.AddComponent<Light>(); rockLight.type = LightType.Point; rockLight.color = new Color(1f, 0.42f, 0.14f); rockLight.range = d.rockRange; rockLight.intensity = d.rockPower; rockLight.shadows = LightShadows.None;
        }

        /// <summary>The water's height over the set's origin (the model: art/tub_numbers.json).</summary>
        public const float WaterH = 0.95f, BubbleR = 1.45f;
        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static Mesh Dome()
        {   // an 8-sided dome, 24 triangles
            const int N = 8; var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            for (int j = 0; j <= 2; j++) { float a = j * Mathf.PI * 0.25f; for (int i = 0; i < N; i++) { float b = i * Mathf.PI * 2f / N; v.Add(new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(a), Mathf.Cos(a) * Mathf.Sin(b))); uv.Add(new Vector2(0.5f, 0.5f)); } }
            for (int j = 0; j < 2; j++) for (int i = 0; i < N; i++) { int a = j * N + i, b = j * N + (i + 1) % N, c = (j + 1) * N + i, d = (j + 1) * N + (i + 1) % N; t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(d); }
            var m = new Mesh { name = "L07TubBubble" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); return m;
        }

        // ------------------------------------------------------------------------------------------------ clips and sounds
        OzLoungerClip ClipDef(string who, string name) { foreach (var c in data.clips) if (c.who == who && c.name == name) return c; return null; }

        bool Play(Robot r, string name, float fade)
        {
            var cd = ClipDef(r.who, name);
            var ac = cd != null ? Resources.Load<AnimationClip>(cd.res) : null;
            if (!ac) { Warn(r.who + ": clip " + name + " missing"); return false; }
            int next = r.cur == 0 ? 1 : 0;
            if (r.slot[next].IsValid()) { r.mixer.DisconnectInput(next); r.slot[next].Destroy(); }
            var p = AnimationClipPlayable.Create(r.graph, ac); p.SetApplyFootIK(false); p.SetTime(0);
            r.graph.Connect(p, 0, r.mixer, next); r.slot[next] = p;
            r.fadeDur = r.cur < 0 ? 0f : Mathf.Max(0f, fade); r.fadeT = 0f; r.cur = next;
            if (r.fadeDur <= 0f) { r.mixer.SetInputWeight(next, 1f); if (r.slot[1 - next].IsValid()) r.mixer.SetInputWeight(1 - next, 0f); }
            else { r.mixer.SetInputWeight(next, 0f); if (r.slot[1 - next].IsValid()) r.mixer.SetInputWeight(1 - next, 1f); }
            r.clip = cd;
            return true;
        }

        Robot Find(string who) { foreach (var r in robots) if (r.who == who) return r; return null; }

        AudioClip Clip(string res)
        {
            if (string.IsNullOrEmpty(res)) return null;
            if (!clips.TryGetValue(res, out var c)) clips[res] = c = Resources.Load<AudioClip>(res);
            return c;
        }

        void Sound(OzTubSound s)
        {   // part of the transmission: 2D, at the voice's gain + db; every moment logged
            var c = Clip(s.clip); string key = "brief_" + (string.IsNullOrEmpty(s.key) ? s.clip : s.key);
            if (!c) { OzSoundLog.Add("L07Tub", key, s.clip, "missing clip"); return; }
            float g = Mathf.Clamp01(voiceGain * Mathf.Pow(10f, s.db / 20f));
            sfx.pitch = s.pitch > 0f ? s.pitch : 1f; sfx.PlayOneShot(c, g);
            OzSoundLog.Add("L07Tub", key, c.name, "played", g);
        }

        // ------------------------------------------------------------------------------------------------ the briefing
        /// <summary>ZodBriefing: the two robots hold this line (the same contract as OzL16LoungerSet.BriefBegin).</summary>
        public bool BriefBegin(ZodStage st, AudioSource voiceSource, List<Vector2> cuesA, List<Vector2> cuesB, Func<float> lineClock, float gain)
        {
            if (!EnsureBuilt() || !st) return false;
            stage = st; voice = voiceSource; clock = lineClock; voiceGain = gain;
            var a = Find("alan"); var b = Find("brad");
            a.cues.Clear(); b.cues.Clear(); if (cuesA != null) a.cues.AddRange(cuesA); if (cuesB != null) b.cues.AddRange(cuesB);
            beatI = soundI = 0; shotI = -1; lastT = -99f; overFor = 0f; popDone = false; LandedAt = TipOverLava = -1f; LockedMove = 0f;
            foreach (var r in robots) Play(r, "sit_loop", 0f);
            TickShapes(-99f);
            Show(true); SetWalker(false); SetArm(false);
            if (walkerGraph.IsValid()) walkerTake.SetTime(0.0);
            if (steam) steam.Play();
            var bedClip = Clip(BedRes); if (bedClip && bed) { bed.clip = bedClip; bed.pitch = 1.35f; bed.volume = Mathf.Clamp01(voiceGain * Mathf.Pow(10f, BedDb / 20f)); bed.Play(); OzSoundLog.Add("L07Tub", "brief_tub_bubbling", bedClip.name, "loop on", bed.volume); }
            else OzSoundLog.Add("L07Tub", "brief_tub_bubbling", BedRes, "missing clip");
            briefOn = true;
            OnlyInTheWindow(true);
            stage.LoungerFeed(cam);
            Debug.Log($"[OpenZ L07 tub] briefing in the hot tub: {a.cues.Count} + {b.cues.Count} cues, {beats.Count} beats, {sounds.Count} sounds, window camera into the transmission feed");
            return true;
        }

        /// <summary>The bubbling bed under the tub's shots (the sound lane's lava bubbling, pitched up to water), dB under the voice's gain.</summary>
        public static string BedRes = "OpenZ/Audio/Ambience/sd_amb_lava_bubbling_loop"; public static float BedDb = -20f, WalkerBedDb = -6f;

        void BriefEnd()
        {
            if (!briefOn) return;
            briefOn = false; clock = null; voice = null;
            OnlyInTheWindow(false);
            if (stage) stage.LoungerFeed(null);
            if (steam) steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            walkerWade?.Show(false);
            if (bed && bed.isPlaying) { bed.Stop(); OzSoundLog.Add("L07Tub", "brief_tub_bubbling", "", "loop off"); }
            foreach (var r in robots) { r.cues.Clear(); r.jaw = 0f; }
            Show(false);
            ZodBriefing.BoysHostEnd();
            Debug.Log("[OpenZ L07 tub] briefing over: the set is hidden, the window is the commander's again");
        }

        // The set stands on the level's lava, so the full-screen fly-over would draw it too (dawn_lava looks straight at it, our_base_home's first frames touch it). It belongs to the window only:
        // every other camera renders the frame without it, its lights and its shadows. Taken off in the camera's end event, so Show, SetArm and SetWalker never see this.
        readonly List<Light> lightsOff = new List<Light>(); Renderer[] setRends; Light[] setLights;
        void OnlyInTheWindow(bool on)
        {
            if (on) foreach (var c in Camera.allCameras) if (c && c != cam && c.cameraType == CameraType.Game && c.cullingMask != 1 << SetLayer) c.cullingMask &= ~(1 << SetLayer);   // the stage does this too, every 2 s; the commander's own camera keeps its one layer
            RenderPipelineManager.beginCameraRendering -= HideFrom; RenderPipelineManager.endCameraRendering -= ShowAgain;
            if (!on) { if (setRends != null) foreach (var r in setRends) if (r) r.forceRenderingOff = false; foreach (var l in lightsOff) if (l) l.enabled = true; lightsOff.Clear(); return; }
            setRends = root.GetComponentsInChildren<Renderer>(true); setLights = root.GetComponentsInChildren<Light>(true);
            RenderPipelineManager.beginCameraRendering += HideFrom; RenderPipelineManager.endCameraRendering += ShowAgain;
        }
        bool WindowOwn(Camera c) => c == cam || c.cameraType == CameraType.SceneView;

        int SetLayer;
        /// <summary>Play check, at every moment of a level: how many of the set's renderers the game camera could draw now (its layer in the camera's mask, renderer on, object active). 0 is the only good answer.</summary>
        public static int MainCanDraw()
        {
            var main = Camera.main; if (!I || !I.root || !main) return 0;
            int n = 0; foreach (var r in I.root.GetComponentsInChildren<Renderer>(true)) if (r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff && (main.cullingMask & (1 << r.gameObject.layer)) != 0) n++;
            return n;
        }
        void HideFrom(ScriptableRenderContext ctx, Camera c)
        {
            if (WindowOwn(c)) return;
            foreach (var r in setRends) if (r) r.forceRenderingOff = true;
            foreach (var l in setLights) if (l && l.enabled) { l.enabled = false; lightsOff.Add(l); }
        }
        void ShowAgain(ScriptableRenderContext ctx, Camera c)
        {
            if (WindowOwn(c)) return;
            foreach (var r in setRends) if (r) r.forceRenderingOff = false;
            foreach (var l in lightsOff) if (l) l.enabled = true;
            lightsOff.Clear();
        }

        void Show(bool on)
        {
            drawn = on;
            if (!on) { hidden.Clear(); foreach (var r in root.GetComponentsInChildren<Renderer>(true)) if (r.enabled) { r.enabled = false; hidden.Add(r); } }
            else { foreach (var r in hidden) if (r) r.enabled = true; hidden.Clear(); }
            foreach (var l in root.GetComponentsInChildren<Light>(true)) l.enabled = on;
        }

        void SetWalker(bool on)
        {
            if (walkerShown == on) return;
            walkerShown = on;
            if (walkerRends != null) foreach (var r in walkerRends) if (r) r.enabled = on;
            if (walkerGlow) walkerGlow.enabled = on;
            walkerWade?.Show(on);
        }

        void SetArm(bool on)
        {
            if (armShown == on) return;
            armShown = on;
            if (armR) armR.enabled = on; if (armGlow) armGlow.enabled = on; if (armRing) armRing.enabled = on; if (armBack) armBack.enabled = on; if (armLight) armLight.enabled = on;
        }

        void TickBrief(float dt)
        {
            float t = clock != null ? clock() : float.NaN;
            if (float.IsNaN(t)) { BriefEnd(); return; }
            bool over = float.IsPositiveInfinity(t);
            if (over) { overFor += dt; if (overFor > ClosingMost) { BriefEnd(); return; } }                 // the line is over and its window never reported closed: the set does not wait for it
            if (!over)
            {
                lastT = t;
                while (beatI < beats.Count && beats[beatI].t <= t) { var b = beats[beatI++]; var r = Find(b.who); if (r != null) Play(r, b.clip, b.fade); }
                while (soundI < sounds.Count && sounds[soundI].t <= t) Sound(sounds[soundI++]);
            }
            float ts = over ? lastT : t; int want = 0;
            for (int i = 0; i < data.shots.Length; i++) if (data.shots[i].t0 <= ts) want = i;
            if (want != shotI)
            {
                shotI = want; shotAt = ts; walkerAimHas = faceAimHas = false;
                var cd = cam ? cam.GetUniversalAdditionalCameraData() : null; if (cd) cd.resetHistory = true;      // no smeared frame across a cut (the crash's own blur stays)
                string kind = data.shots[shotI].kind;
                SetWalker(kind == "walker"); SetArm(kind == "crash"); if (rockLight) rockLight.enabled = kind == "push";
                if (bed) { bed.mute = kind == "crash"; bed.pitch = kind == "walker" ? 1f : 1.35f; bed.volume = Mathf.Clamp01(voiceGain * Mathf.Pow(10f, (kind == "walker" ? WalkerBedDb : BedDb) / 20f)); }   // the tub's bubbling is not heard out at the hand: the one beat of quiet is quiet. On the wader the same loop is lava at its own pitch, nearer and louder
            }
            var s = data.shots[shotI];
            TickWalker(ts, dt, s.kind == "walker"); TickArm(ts, dt, s.kind == "crash");
        }

        void TickWalker(float t, float dt, bool on)
        {
            var w = data.walker; if (!on || walker == null || w == null) return;
            float u = Mathf.Clamp01((t - w.t0) / Mathf.Max(0.1f, w.t1 - w.t0)), e = u * u * (3f - 2f * u);
            Vector3 a = new Vector3(w.ax, 0f, w.az), b = new Vector3(w.bx, 0f, w.bz), p = Vector3.Lerp(a, b, u);
            WalkerSink = Mathf.Lerp(w.sinkA, w.sinkB, u);
            p.y = -data.islandLift - WalkerSink;
            walker.localPosition = p; walker.localRotation = Quaternion.LookRotation(a - b, Vector3.up);     // every unit model faces -Z (UnitView.modelYaw): the model's +Z points back along the walk
            if (walkerGraph.IsValid()) { double wt = System.Math.Max(0.0, (t - w.t0) * w.walkRate) % walkerLen; walkerTake.SetTime(wt); walkerGraph.Evaluate(0f); }   // on the line's clock: the steps' sounds are placed on these footfalls
            WalkerHeat = walkerHeat = Mathf.Lerp(w.heatA, w.heatB, e); walkerTop = Mathf.Lerp(w.topA, w.topB, e);
            Vector3 foot = new Vector3(p.x, -data.islandLift + 0.03f, p.z);
            if (walkerGlow) { walkerGlow.transform.localPosition = foot; walkerGlow.transform.localScale = Vector3.one * 2.6f; OzL07CutFx.Tint(walkerGlow, OzL07CutFx.LavaGlow, Mathf.Lerp(data.lights.walkerGlowA, data.lights.walkerGlowB, u)); }
        }

        /// <summary>LateUpdate, after the take's bones: the legs' three floats in every skin's block, the contact at each leg.</summary>
        void LateWalker(float dt)
        {
            if (walker == null || !walkerShown || data.walker == null) return;
            float line = root.position.y - data.islandLift;
            if (walkerSkins != null) foreach (var s in walkerSkins) OzL07CutFx.LegHeat(s, legBlock, walkerHeat, line, walkerTop);
            if (walkerWade != null) { walkerWade.Tick(dt, line, walker.position.y, walkerHeat); WalkerFootfalls = walkerWade.Footfalls; }
        }

        void TickArm(float t, float dt, bool on)
        {
            var a = data.arm;
            if (pop != null) pop.Tick(dt, cam);
            if (!on || arm == null || a == null) return;
            // still until t0, then one even speed, straight down, from thumb tip tipStart over the lava to `under` below it
            float u = (t - a.t0) / Mathf.Max(0.1f, a.t1 - a.t0), v = Mathf.Clamp01(u);
            float tip = u < 1f ? Mathf.Lerp(a.tipStart, 0f, v) : -a.under * Mathf.Clamp01((u - 1f) * (a.t1 - a.t0) / 0.3f);
            TipOverLava = tip;
            arm.localPosition = new Vector3(a.x, -data.islandLift + tip, a.z);
            arm.localRotation = Quaternion.Euler(0f, a.viewDeg, 0f);
            bool under = tip <= 0f;
            if (armR)
            {   // the hot band stays at the lava line, so the blue paint and the fingers read above it: the LV3 look, never the flat glow over the whole arm
                armR.enabled = !under;
                OzL07CutFx.LegHeat(armR, armBlock, Mathf.Lerp(a.heatA, a.heatB, v), root.position.y - data.islandLift, a.heatTop);
            }
            Cut(a, Mathf.Max(tip, 0f), out float cx, out float cz, out float half);                         // the forearm, then the fist, then the thumb: the ring closes on what still stands in the lava
            Vector3 line = new Vector3(a.x + cx, -data.islandLift + 0.03f, a.z + cz);
            float gone = under ? Mathf.Clamp01((t - a.t1) / 0.35f) : 0f;
            if (armGlow) { float gr = half * a.glowWide + 0.12f; armGlow.transform.localPosition = line; armGlow.transform.localScale = Vector3.one * (gr * 2f); OzL07CutFx.GlowAt(armGlow, root.TransformPoint(line), gr); OzL07CutFx.Tint(armGlow, OzL07CutFx.LavaGlow, a.glowGain * (1f - gone)); }
            if (armRing) { armRing.transform.localPosition = line; armRing.transform.localScale = Vector3.one * ((half * 1.45f + 0.02f) * 2f); OzL07CutFx.Tint(armRing, OzL07CutFx.LavaRing, a.ringGain * (1f - gone)); }
            if (armBack) { armBack.transform.localPosition = new Vector3(a.backX, -data.islandLift + 0.02f, a.backZ); armBack.transform.localRotation = Quaternion.Euler(90f, a.backYaw, 0f); armBack.transform.localScale = new Vector3(a.backWide, a.backLen, 1f); OzL07CutFx.Tint(armBack, OzL07CutFx.LavaGlow, a.backGain * (1f - gone)); }
            if (armLight)
            {   // the lava's light on the hand comes from the lens's side, a metre off: a point light inside the fist (0.25 m over the line) lit every plate's top face from a few cm: the pale green and white stripes of the real frames
                var s = data.shots[shotI]; Vector3 toLens = new Vector3(s.lpx - line.x, 0f, s.lpz - line.z).normalized;
                armLight.transform.localPosition = line + toLens * a.lightBack + Vector3.up * a.lightUp; armLight.intensity = a.lightPower * (1f - gone);
            }
            if (!popDone && t >= a.popT) { popDone = true; pop?.Fire(root.TransformPoint(line)); }
        }

        /// <summary>Where the lava cuts the arm with the thumb tip h over it: the table in the data file, a straight line between its rows.</summary>
        static void Cut(OzTubArm a, float h, out float x, out float z, out float half)
        {
            x = z = 0f; half = 0.1f; var H = a.ringH;
            if (H == null || H.Length == 0 || a.ringX == null || a.ringZ == null || a.ringR == null) return;
            int i = 0; while (i < H.Length - 2 && H[i + 1] < h) i++;
            int j = Mathf.Min(i + 1, H.Length - 1); float u = j > i ? Mathf.Clamp01((h - H[i]) / Mathf.Max(1e-4f, H[j] - H[i])) : 0f;
            x = Mathf.Lerp(a.ringX[i], a.ringX[j], u); z = Mathf.Lerp(a.ringZ[i], a.ringZ[j], u); half = Mathf.Lerp(a.ringR[i], a.ringR[j], u);
        }

        void PlaceCamera()
        {
            if (!cam || shotI < 0) return;
            var s = data.shots[shotI];
            Vector3 p = root.TransformPoint(new Vector3(s.px, s.py, s.pz)), at = root.TransformPoint(new Vector3(s.tx, s.ty, s.tz));
            float ts = lastT, len = Mathf.Max(0.3f, s.t1 - s.t0), u = Mathf.Clamp01((ts - s.t0) / len), e = u * u * (3f - 2f * u);
            float fov = s.fov;
            if (s.kind == "crash")
            {
                Vector3 lp = root.TransformPoint(new Vector3(s.lpx, s.lpy, s.lpz)), lt = root.TransformPoint(new Vector3(s.ltx, s.lty, s.ltz));
                float k = Mathf.Clamp01((ts - s.t0) / Mathf.Max(0.05f, s.landSeconds));
                if (k >= 1f)
                {   // LOCKED OFF: not one float of drift from here on
                    cam.transform.SetPositionAndRotation(lp, Quaternion.LookRotation(lt - lp, Vector3.up)); cam.fieldOfView = s.lfov;
                    if (LandedAt < 0f) { LandedAt = ts; landedAt = lp; }
                    LockedMove = Mathf.Max(LockedMove, Vector3.Distance(lp, landedAt));
                    return;
                }
                float q = 1f - (1f - k) * (1f - k) * (1f - k);                                               // hard off the mark, settling onto the landing
                cam.transform.SetPositionAndRotation(Vector3.Lerp(p, lp, q), Quaternion.Slerp(Quaternion.LookRotation(at - p, Vector3.up), Quaternion.LookRotation(lt - lp, Vector3.up), q));
                cam.fieldOfView = Mathf.Lerp(s.fov, s.lfov, q);
                return;
            }
            if (s.zoomFov > 0f) fov = Mathf.Lerp(s.fov, s.zoomFov, s.kind == "push" ? u : e);
            if (s.kind == "walker" && walkerHead)
            {
                Vector3 want = walkerHead.position - Vector3.up * 0.75f;
                if (!walkerAimHas) { walkerAim = want; walkerAimHas = true; }
                walkerAim = Vector3.Lerp(walkerAim, want, 1f - Mathf.Exp(-Mathf.Min(Time.unscaledDeltaTime, 0.1f) / 0.3f)); at = walkerAim;
            }
            int seed = shotI; float calm = 1f;
            if (s.kind == "face")
            {   // the crash zoom on the speaker's face: hard off the mark, lands in landSeconds on the word, the aim on his head bone as the clip has it. The hand-held runs on from the shot before it and shrinks with the lens
                var who = Find(s.who); Vector3 want = who != null && who.head ? who.head.position + Vector3.up * FaceUp : root.TransformPoint(new Vector3(s.ltx, s.lty, s.ltz));
                if (!faceAimHas) { faceAim = want; faceAimHas = true; }
                faceAim = Vector3.Lerp(faceAim, want, 1f - Mathf.Exp(-Mathf.Min(Time.unscaledDeltaTime, 0.1f) / 0.2f));
                float k = Mathf.Clamp01((ts - s.t0) / Mathf.Max(0.05f, s.landSeconds)), q = 1f - (1f - k) * (1f - k) * (1f - k);
                p = Vector3.Lerp(p, root.TransformPoint(new Vector3(s.lpx, s.lpy, s.lpz)), q); at = Vector3.Lerp(at, faceAim, q); fov = Mathf.Lerp(s.fov, s.lfov, q);
                seed = shotI - 1; calm = fov / s.fov;
            }
            var look = Quaternion.LookRotation(at - p, Vector3.up); float now = Time.unscaledTime;
            if (!OzMotion.Reduced)
            {
                float n1 = Mathf.PerlinNoise(now * 0.55f, 3.7f + seed) - 0.5f, n2 = Mathf.PerlinNoise(8.3f + seed, now * 0.5f) - 0.5f;
                p += look * new Vector3(n2 * 2f * Drift * calm, n1 * 2f * Drift * calm, 0f);
                look *= Quaternion.Euler(n1 * 2f * Sway * calm, n2 * 2f * Sway * calm, 0f);
            }
            cam.transform.SetPositionAndRotation(p, look); cam.fieldOfView = fov;
        }

        /// <summary>The face shot's aim over the head bone: the middle of the head.</summary>
        const float FaceUp = 0.14f;

        float JawFromVoice(Robot r)
        {
            var v = voice;
            if (!v || !v.clip || !v.isPlaying || r.cues.Count == 0) return 0f;
            float t = v.timeSamples / (float)Mathf.Max(1, v.clip.frequency); bool mine = false;
            foreach (var c in r.cues) if (t >= c.x - 0.12f && t <= c.y + 0.2f) { mine = true; break; }
            if (!mine) return 0f;
            float j = ZodStage.LoungerJaw(v.clip, t);
            if (j >= 0f) return j;
            v.GetOutputData(jawBuf, 0);
            double sum = 0; for (int i = 0; i < jawBuf.Length; i++) sum += jawBuf[i] * jawBuf[i];
            float rms = Mathf.Sqrt((float)(sum / jawBuf.Length)) / Mathf.Max(0.05f, v.volume);
            float k = Mathf.Clamp01((20f * Mathf.Log10(rms + 1e-6f) + 40f) / 24f);
            return k * k;
        }

        void TickLife(float dt)
        {
            if (water) water.Rotate(Vector3.up, waterSpin * dt, Space.World);
            for (int i = 0; i < bubbles.Count; i++)
            {
                float t = bubbleT[i] += dt, life = bubbleLife[i]; var b = bubbles[i];
                if (t < 0f) continue;
                if (!b.gameObject.activeSelf)
                {
                    float a = Rand(0f, Mathf.PI * 2f), r = BubbleR * Mathf.Sqrt(Rand(0.02f, 1f));
                    b.localPosition = new Vector3(Mathf.Cos(a) * r, WaterH, Mathf.Sin(a) * r); b.gameObject.SetActive(true);
                }
                float s = Mathf.Sin(Mathf.Clamp01(t / life) * Mathf.PI * 0.5f) * 0.11f;
                b.localScale = new Vector3(s, s * 0.8f, s);
                if (t >= life) { bubbleT[i] = -Rand(0.1f, 1.2f); bubbleLife[i] = Rand(0.5f, 1.1f); b.gameObject.SetActive(false); }
            }
        }

        // ------------------------------------------------------------------------------------------------ frame
        void Update()
        {
            if (built && endOn) { TickEnding(); return; }                                                   // the red win: OzL07HotTubSet.Ending.cs
            if (!built || !briefOn) return;
            bool paused = AudioListener.pause;
            float dt = paused ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            TickBrief(dt);
            if (!briefOn) return;
            TickLife(dt);
            foreach (var r in robots)
            {
                if (r.cur >= 0 && r.fadeDur > 0f && r.fadeT < r.fadeDur)
                {
                    r.fadeT = Mathf.Min(r.fadeDur, r.fadeT + dt); float k = r.fadeT / r.fadeDur; k = k * k * (3f - 2f * k);
                    r.mixer.SetInputWeight(r.cur, k); if (r.slot[1 - r.cur].IsValid()) r.mixer.SetInputWeight(1 - r.cur, 1f - k);
                }
                // a held pose is not a statue: a move that has played out goes over into its loop of small life
                if (r.clip != null && !r.clip.loop && !string.IsNullOrEmpty(r.clip.hold) && r.cur >= 0 && r.slot[r.cur].IsValid() && r.slot[r.cur].GetTime() >= r.clip.seconds)
                    Play(r, r.clip.hold, r.clip.holdFade > 0f ? r.clip.holdFade : 0.3f);
                r.graph.Evaluate(dt);
            }
        }

        void LateUpdate()
        {
            if (built && endOn) { LateEnding(); return; }                                                   // the guard below holds until the ending's own cut
            if (built && !briefOn && drawn) Show(false);                                                    // no line on the air, no set in the level: every frame, whatever path ended the line
            if (!built || !briefOn) return;
            float dt = AudioListener.pause ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            foreach (var r in robots)
            {
                for (int k = 0; k < 2; k++)
                    if (r.can[k] && r.seat[k] != null) r.can[k].localPosition = new Vector3(r.seat[k].px, r.seat[k].py, r.seat[k].pz);   // the seat is held every frame (OzL16LoungerSet.LateUpdate says why)
                float target = JawFromVoice(r);
                if (dt > 0f) r.jaw += (target - r.jaw) * (1f - Mathf.Exp(-dt / (target > r.jaw ? 0.025f : 0.045f)));
                r.jaw = Mathf.Clamp01(r.jaw);
                if (r.mouth) r.mouth.localRotation = r.mouthRest * Quaternion.Euler(r.jaw * r.jawDeg * r.jawSign, 0f, 0f);
            }
            TickShapes(lastT); LateWalker(dt);
            PlaceCamera();
        }

        /// <summary>One-line state for the Play check.</summary>
        public static string Status()
        {
            if (!I) return "hot tub: none in this scene (level L07, red side)";
            if (!I.built) return "hot tub: NOT built (" + LastWarning + ")";
            return $"hot tub: game camera can draw {MainCanDraw()} of its renderers, briefing {I.briefOn}, shot {I.ShotName}, clock {I.lastT:F2} s, wader sink {WalkerSink:F2} m, legs heat {WalkerHeat:F2}, {WalkerFootfalls} footfalls, landed {LandedAt:F2}, thumb tip {TipOverLava:F2} m over the lava, locked lens moved {LockedMove:F4} m, pop {I.popDone}" + (LastWarning.Length > 0 ? " | last warning: " + LastWarning : "");
        }
    }
}
