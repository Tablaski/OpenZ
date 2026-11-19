// OpenZ (GPL-3.0) - version 1.5, level 18 "Bridge Game": Zod with Alan and Brad at his sides, watching the cloud over the three blown bridges.
// The red side's mission-start briefing, in the transmission window like every briefing (never full screen): the window shows this set
// between the west and the middle bridge (Resources/OpenZ/L18Cut/L18_WatchPost: a concrete strongpoint with a floor of riveted steel
// plates, a parapet with a coping, sandbags, ammunition crates, a field radio, an aerial; the team's red bands). Their own fort stands
// well back behind them (33 m), the burning bridges left and right of them, the cloud ahead. Zod is the commander: he stands a step
// ahead of his two men, in a wide stance, and moves from one held pose into the next (ZodPoser: poses in the avatar's own muscle space,
// eased, the feet pinned on the floor); he never plays a stock gesture clip. The window's camera has real depth of field (a set-only
// volume) and films past the post's own things (a sandbag, the coping, the aerial, a crate); it jolts with the blasts' shake.
// The window's camera draws the level too: it sees the stage's real fireballs, pillars and cloud beyond them (OzL18BriefingStage).
// Nothing here is a rule, a collider or a save. The same method and the same four locks as
// OzL07HotTubSet: (1) every object of the set is on the ZodCameo layer, which no other camera draws; (2) a build that fails leaves
// nothing behind (Scrap); (3) while no briefing runs every renderer is off, checked again every frame; (4) OnlyInTheWindow in the camera
// events, for the light, which no layer stops. Counted in Play: OzL18WatchSet.MainCanDraw() = 0 at every moment.
// THREE speakers: ZodBriefing.TrioHostBegin hands it the line with "host": "trio" (ZodBriefing.L18Trio.cs). Zod's jaw runs on his own
// talk rig from his curve of the voice clip (zod_mouth_curves.json, zero outside his cues); Alan's and Brad's jaws move inside their own
// subtitle cues only ("Alan: ..." / "Brad: ..."), from the robots' curve (Boys/alan_mouth_curves.json).
// ACTING: Alan and Brad each play ONE AUTHORED TAKE for this scene (Resources/OpenZ/L18Cut/Anim/<Robot>_watch_take, made on their own
// rigs by kit_tools/blender/04_takes.py, every frame through the joint-limit gate, 0 over): the take is set to the voice clock every
// frame (a manual playable graph: a held clock shows a held pose). The takes come to rest before their old closing lean (takeStop): the
// two men end on a look at each other behind the commander's back and down at the water (head turns of the scene file).
// EVERYTHING IS A PURE FUNCTION OF THE LINE'S CLOCK (poses, head turns, takes, the camera): a held clock is a held picture.
// Play check: return OpenZ.OzL18WatchSet.Status();   Tuning in Play: LabSet / LabData / LabRender (stills of any second of the line).
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
    [Serializable] public class OzWatchActor { public string who; public float x, z, yaw; }
    /// <summary>kind: hand (a small hand-held drift), push (the lens narrows to zoomFov over the shot), face (a crash zoom from the shot's own
    /// lens to lfov in landSeconds, aimed at who's head, then hand-held). Positions in the set's frame (m): origin on the deck under Zod's
    /// mark, +Z the way the three look (north, the river), +X their right. (mx, my, mz) / (ax, ay, az): the lens and its aim travel this far
    /// over the shot (moving from its first frame). focus: who is sharp (zod / alan / brad; empty = the aim), aperture (f-stop, 0 = the
    /// set's), focal (mm, 0 = the set's), shake (0 = the set's share of the blasts' shake).</summary>
    [Serializable] public class OzWatchShot { public string name, kind, who, focus; public float t0, px, py, pz, tx, ty, tz, fov, zoomFov, landSeconds, lfov, mx, my, mz, ax, ay, az, aperture, focal, shake; }
    /// <summary>At t the character goes over into `clip` (a state of his own Animator controller) in `fade` s; hold above 0: after that many
    /// seconds back into `back` (his bed). Only for a character without a take and without poses.</summary>
    [Serializable] public class OzWatchBeat { public float t, fade, hold; public string who, clip, back; }
    /// <summary>At t the character's head turns to `yaw` degrees (+ = to his right) and `pitch` (+ = chin down) over `seconds`.</summary>
    [Serializable] public class OzWatchLook { public float t, yaw, seconds, pitch; public string who; }
    [Serializable] public class OzWatchSound { public float t, db, pitch; public string clip, key; }
    /// <summary>A robot's authored take: the clip (Resources path) and the voice second its first frame stands for.</summary>
    [Serializable] public class OzWatchTake { public string who, res; public float t0, seconds; }
    /// <summary>A blend shape of a robot's body on the line's clock: up from t0 to t1, down from t2 to t3 (Alan's pointing finger).</summary>
    [Serializable] public class OzWatchShape { public string who, shape; public float t0, t1, t2, t3; }
    /// <summary>A held pose of Zod: the frame `nt` (0..1) of the state `state` of his own Animator controller, then the muscles `m` set to `v`
    /// (the avatar's own muscle names, -1..1).</summary>
    [Serializable] public class OzZodPose { public string name, state; public float nt; public string[] m; public float[] v; }
    /// <summary>From t Zod moves into `pose` over `ease` seconds (eased in and out), and holds it.</summary>
    [Serializable] public class OzZodKey { public float t, ease; public string pose; }
    [Serializable]
    public class OzWatchFile
    {
        public int version; public string level; public float placeX, placeZ, yaw, zodHeight, glowPower, glowRange;
        public string parapetRes, alanBody, alanBodyNode;
        /// <summary>The post: its deck over the ground (m), the model's scale; the takes come to rest at takeStop (voice s; 0 = they run out)
        /// over takeStopEase s; Zod's toes point out by toeOut degrees.</summary>
        public float postUp, postScale, takeStop, takeStopEase, toeOut;
        public OzWatchActor[] actors; public OzWatchShot[] shots; public OzWatchBeat[] beats; public OzWatchLook[] looks; public OzWatchSound[] sounds; public OzWatchTake[] takes; public OzWatchShape[] shapes;
        public OzZodPose[] zposes; public OzZodKey[] zkeys; public string[] stanceM; public float[] stanceV;
    }

    [DefaultExecutionOrder(150)]
    public sealed class OzL18WatchSet : MonoBehaviour
    {
        public const string Level = OzCity.BridgeGame, DataPath = "OpenZ/Zod/l18_watch_scene";
        public static OzL18WatchSet I { get; private set; }
        public static string LastWarning = "";
        /// <summary>Metres and degrees of the hand-held shots' drift; the most a head turns from its body (deg); the robots' model nose (they face -Z)
        /// and Zod's (he faces +Z) as extra yaw; the sign of Zod's gaze (+ = his right, ZodTalkRig.gazeYaw).</summary>
        public static float Drift = 0.022f, Sway = 0.4f, HeadTurnMax = 40f, RobotYaw = 180f, ZodYaw = 0f, ZodGazeSign = 1f;
        /// <summary>The watch post's model (the name is the first round's: the data check reads it).</summary>
        public const string ParapetRes = "OpenZ/L18Cut/L18_WatchPost";
        /// <summary>The window's lens: focal length (mm) and f-stop of its depth of field (a shot's own values win), on / off; the share of the
        /// blasts' shake the window takes (1 = the same share of its height as the big picture).</summary>
        public static float Focal = 70f, Aperture = 2.8f, WindowShake = 0.8f; public static bool Bokeh = true;
        /// <summary>Zod's talk rig on this set: nod (deg), sway (deg): a commander's head hardly moves.</summary>
        public static float ZodNod = 2.2f, ZodSway = 0.35f, ZodBreath = 0.012f;
        /// <summary>Zod's body square over his feet (no tilt of the stock idle); his ankles this much higher than in the stock idle (m): the soles lie on the plates.</summary>
        public static bool Upright = true; public static float SoleLift = 0.03f;
        /// <summary>Play checks: authored takes playing, shape tracks found, the set stands on its post (false = nothing is built).</summary>
        public static int TakesBuilt, ShapesBuilt, PosesBuilt; public static bool OnFort; public static string PlaceNote = "";
        /// <summary>Play checks: lines held, beats played, the worst head turn asked this line (deg).</summary>
        public static int Lines, BeatsPlayed; public static float WorstTurn;
        /// <summary>Tuning in Play: a second of the line shown with no briefing running (NaN = off), and a shot forced by index (-1 = the clock's).</summary>
        public static float LabT = float.NaN; public static int LabShot = -1;

        sealed class Actor
        {
            public string who; public Transform root, head, mouth; public Animator anim; public Renderer[] rends; public ZodTalkRig rig;
            public Quaternion mouthRest = Quaternion.identity; public float jawSign = 1f, jawDeg = 10f, jaw, yaw, pitch;
            public string bed = "", backTo; public float backAt = -1f;
            public PlayableGraph graph; public AnimationClipPlayable take; public bool hasTake; public float takeT0, takeLen; public SkinnedMeshRenderer body;
            public readonly List<Vector2> cues = new List<Vector2>();
        }

        /// <summary>Zod's held poses in the avatar's muscle space, and his feet's place on the floor.</summary>
        sealed class ZodPoser
        {
            public HumanPoseHandler h; public HumanPose hp; public readonly Dictionary<string, float[]> poses = new Dictionary<string, float[]>(); public float[] cur, first;
            public Vector3 bodyPos; public Quaternion bodyRot = Quaternion.identity; public Transform root, hips, footL, footR;
            public Quaternion footRefL = Quaternion.identity, footRefR = Quaternion.identity; public Vector3 feetMid; public float toeOut; public bool ok;
        }

        OzWatchFile data; bool built, buildTried, drawn = true, briefOn; ZGame game; Transform root; Camera cam; AudioSource sfx; Light glow; Volume vol; DepthOfField dof;
        readonly List<Actor> actors = new List<Actor>(); readonly List<Renderer> hidden = new List<Renderer>(); readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        ZodStage stage; AudioSource voice; Func<float> clock; float voiceGain = 1f, lastT = -99f, overFor; int beatI, soundI, shotI = -1; ZodPoser poser;
        readonly List<OzWatchBeat> beats = new List<OzWatchBeat>(); readonly List<OzWatchLook> looks = new List<OzWatchLook>(); readonly List<OzWatchSound> sounds = new List<OzWatchSound>();
        readonly List<OzZodKey> zkeys = new List<OzZodKey>();
        Vector3 faceAim; bool faceAimHas; int SetLayer;
        sealed class ShapeTrack { public SkinnedMeshRenderer skin; public int index; public OzWatchShape d; }
        readonly List<ShapeTrack> shapeTracks = new List<ShapeTrack>(); readonly List<Material> madeMats = new List<Material>();
        const float ClosingMost = 4f, FaceUp = 0.10f;

        public bool Ready => EnsureBuilt();
        public bool BriefingOn => briefOn;
        public float Mouth01 { get { float m = 0f; foreach (var a in actors) m = Mathf.Max(m, a.rig ? a.rig.Mouth01 : a.jaw); return m; } }
        /// <summary>Recorder log (editor checks): the voice clip's second and the three jaws (Alan, Brad, Zod; 0..1) this frame.</summary>
        public static string JawLog
        {
            get
            {
                var s = I; if (!s || !s.briefOn) return "-1,0,0,0";
                float A = 0f, B = 0f, Z = 0f;
                foreach (var a in s.actors) { float j = a.rig ? a.rig.Mouth01 : a.jaw; if (a.who == "alan") A = j; else if (a.who == "brad") B = j; else Z = Mathf.Max(Z, j); }
                var v = s.voice; float vt = v && v.clip && v.isPlaying ? v.timeSamples / (float)Mathf.Max(1, v.clip.frequency) : -1f;
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                return vt.ToString("F3", inv) + "," + A.ToString("F3", inv) + "," + B.ToString("F3", inv) + "," + Z.ToString("F3", inv);
            }
        }
        public Camera WindowCamera => cam;
        public string ShotName => shotI >= 0 && data != null ? data.shots[shotI].name : "";

        /// <summary>The set of this level, or null.</summary>
        public static OzL18WatchSet For(string level) => level == Level ? I : null;

        // ------------------------------------------------------------------------------------------------ attach (level 18, red side)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; LastWarning = ""; Lines = BeatsPlayed = TakesBuilt = ShapesBuilt = PosesBuilt = 0; WorstTurn = 0f; OnFort = false; PlaceNote = ""; LabT = float.NaN; LabShot = -1; }
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
            var go = new GameObject("OzL18WatchSet");
            if (s.IsValid() && go.scene != s) SceneManager.MoveGameObjectToScene(go, s);
            go.AddComponent<OzL18WatchSet>();
        }

        void Awake() { I = this; game = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>(); }

        void OnDestroy()
        {
            if (I == this) I = null;
            OnlyInTheWindow(false);
            if (stage && briefOn) stage.LoungerFeed(null);
            foreach (var a in actors) if (a.graph.IsValid()) a.graph.Destroy();
            if (poser != null && poser.h != null) { poser.h.Dispose(); poser.h = null; }
            foreach (var m in madeMats) if (m) Destroy(m);
            if (vol && vol.sharedProfile) Destroy(vol.sharedProfile);
        }

        static void Warn(string s) { LastWarning = s; Debug.LogWarning("[OpenZ L18 watch] " + s); }

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

        /// <summary>A build that stopped half way: nothing of the set stays in the world, the line falls back to the commander's HQ.</summary>
        void Scrap()
        {
            foreach (var a in actors) if (a.graph.IsValid()) a.graph.Destroy();
            actors.Clear(); shapeTracks.Clear();
            if (poser != null && poser.h != null) { poser.h.Dispose(); } poser = null;
            if (root) { root.gameObject.SetActive(false); Destroy(root.gameObject); root = null; }
            cam = null;
            Warn("the set could not be built: nothing of it stays in the level, the briefing plays in the HQ");
        }

        bool Build()
        {
            var ta = Resources.Load<TextAsset>(DataPath);
            if (!ta) { Warn("data file Resources/" + DataPath + " missing: no watch set"); return false; }
            data = JsonUtility.FromJson<OzWatchFile>(ta.text);
            if (data == null || data.actors == null || data.shots == null || data.shots.Length == 0) { Warn("data file not readable"); return false; }
            var pz = Resources.Load<GameObject>(ZodStage.PrefabPath); var pa = Resources.Load<GameObject>(ZodStage.AlanPrefabPath); var pb = Resources.Load<GameObject>(ZodStage.BradPrefabPath);
            if (!pz || !pa || !pb) { Warn("a model is missing (Zod " + (pz ? "ok" : "MISSING") + ", Alan " + (pa ? "ok" : "MISSING") + ", Brad " + (pb ? "ok" : "MISSING") + ")"); return false; }
            float gy = game ? game.GroundY(data.placeX, data.placeZ) : 0f;
            float up = data.postUp > 0.1f ? data.postUp : 4.6f, sc = data.postScale > 0.1f ? data.postScale : 1.35f;
            root = new GameObject("L18WatchSet").transform; root.SetParent(transform, false);
            root.SetPositionAndRotation(new Vector3(data.placeX, gy + up, data.placeZ), Quaternion.Euler(0f, data.yaw, 0f));
            OnFort = true;
            SetLayer = ZodStage.ResolveLayer();
            // the post itself (modelled; no stand-in is built: without it the briefing plays in the HQ)
            string pres = string.IsNullOrEmpty(data.parapetRes) ? ParapetRes : data.parapetRes; var ppf = Resources.Load<GameObject>(pres);
            if (!ppf) { Warn("the watch post's model Resources/" + pres + " is missing"); return false; }
            var deck = Instantiate(ppf, root, false); deck.name = "L18WatchPost";
            deck.transform.localPosition = Vector3.zero; deck.transform.localRotation = ppf.transform.localRotation; deck.transform.localScale = ppf.transform.localScale * sc;
            foreach (var c in deck.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var mats = new Dictionary<string, Material>
            {
                { "_Concrete", Lit(new Color32(128, 122, 112, 255), 0.12f, 0f) }, { "_Plates", Lit(new Color32(92, 86, 76, 255), 0.3f, 0.25f) }, { "_Rivets", Lit(new Color32(128, 124, 116, 255), 0.5f, 0.8f) },
                { "_Dark", Lit(new Color32(20, 19, 18, 255), 0.3f, 0.3f) }, { "_Steel", Lit(new Color32(78, 76, 66, 255), 0.4f, 0.55f) }, { "_Red", Lit(new Color32(156, 32, 24, 255), 0.3f, 0.1f) },
                { "_Crate", Lit(new Color32(84, 86, 50, 255), 0.18f, 0f) }, { "_Strap", Lit(new Color32(38, 36, 30, 255), 0.25f, 0.3f) }, { "_Sand", Sack(new Color32(126, 108, 82, 255)) },
                { "_SandDark", Sack(new Color32(98, 84, 64, 255)) },
            };
            foreach (var rr in deck.GetComponentsInChildren<Renderer>(true))
            {
                string n = rr.gameObject.name; Material m = null;
                foreach (var kv in mats) if (n.EndsWith(kv.Key)) m = kv.Value;
                if (m) rr.sharedMaterial = m;
                rr.renderingLayerMask = 1u; rr.shadowCastingMode = ShadowCastingMode.On; rr.receiveShadows = true; rr.allowOcclusionWhenDynamic = false;
                rr.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
            }
            var st = FindFirstObjectByType<ZodStage>();
            float bradScale = st ? st.boysModelScale : 0.84f, alanMul = st ? st.alanScale : 1.19f;
            foreach (var ad in data.actors)
            {
                bool zod = ad.who == "zod", alan = ad.who == "alan";
                var go = Instantiate(zod ? pz : alan ? pa : pb, root, false);
                go.name = "L18Watch_" + ad.who;
                go.transform.localPosition = new Vector3(ad.x, 0f, ad.z);
                go.transform.localRotation = Quaternion.Euler(0f, ad.yaw + (zod ? ZodYaw : RobotYaw), 0f);
                go.transform.localScale = Vector3.one * (zod ? 1f : alan ? bradScale * alanMul : bradScale);
                var a = new Actor { who = ad.who, root = go.transform, jawDeg = alan ? (st ? st.alanJawDeg : 16f) : (st ? st.bradJawDeg : 10f) };
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
                a.rends = go.GetComponentsInChildren<Renderer>(true);
                foreach (var rr in a.rends)
                {
                    rr.renderingLayerMask = 1u; rr.shadowCastingMode = ShadowCastingMode.On; rr.receiveShadows = true; rr.allowOcclusionWhenDynamic = false;
                    if (rr is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
                    rr.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
                }
                a.anim = go.GetComponentInChildren<Animator>(true);
                if (!a.anim || !a.anim.runtimeAnimatorController) { Warn(ad.who + ": no Animator controller on the model"); return false; }
                a.anim.updateMode = AnimatorUpdateMode.UnscaledTime; a.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; a.anim.applyRootMotion = false;
                if (!zod)
                {
                    if (alan && !string.IsNullOrEmpty(data.alanBody))
                    {   // the body with the pointing finger: same skeleton, bound by bone name (the way of the level 7 body)
                        var bodyT = FindDeep(go.transform, string.IsNullOrEmpty(data.alanBodyNode) ? "Alan_Body" : data.alanBodyNode); a.body = bodyT ? bodyT.GetComponent<SkinnedMeshRenderer>() : null;
                        var bm = Resources.Load<GameObject>(data.alanBody); var srcSmr = bm ? bm.GetComponentInChildren<SkinnedMeshRenderer>(true) : null; bool ok = a.body && srcSmr && srcSmr.sharedMesh;
                        if (ok)
                        {
                            var bones = new Transform[srcSmr.bones.Length];
                            for (int i = 0; i < bones.Length; i++) { bones[i] = srcSmr.bones[i] ? FindDeep(go.transform, srcSmr.bones[i].name) : null; if (!bones[i]) ok = false; }
                            if (ok) { a.body.sharedMesh = srcSmr.sharedMesh; a.body.bones = bones; }
                        }
                        if (!ok) Warn("alan: the body with the pointing finger (Resources/" + data.alanBody + ") could not be bound: his fingers stay as modelled");
                    }
                    OzWatchTake td = null; if (data.takes != null) foreach (var x in data.takes) if (x != null && x.who == ad.who) td = x;
                    var clip = td != null ? Resources.Load<AnimationClip>(td.res) : null;
                    if (clip)
                    {   // his authored take, set to the voice clock every frame (manual graph: a held clock is a held pose)
                        a.anim.runtimeAnimatorController = null;
                        a.graph = PlayableGraph.Create("L18Watch_" + ad.who); a.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                        a.take = AnimationClipPlayable.Create(a.graph, clip); a.take.SetApplyFootIK(false);
                        AnimationPlayableOutput.Create(a.graph, "pose", a.anim).SetSourcePlayable(a.take);
                        a.takeT0 = td.t0; a.takeLen = clip.length; a.hasTake = true; TakesBuilt++;
                    }
                    else Warn(ad.who + ": his authored take " + (td != null ? "Resources/" + td.res : "(none in the data)") + " is missing: he stands in his stock states (no gesture on his words)");
                }
                if (zod)
                {
                    if (data.zodHeight > 0.5f)
                    {
                        var b = new Bounds(go.transform.position, Vector3.zero); bool any = false;
                        foreach (var rr in a.rends) { if (rr is ParticleSystemRenderer) continue; if (!any) { b = rr.bounds; any = true; } else b.Encapsulate(rr.bounds); }
                        if (any && b.size.y > 0.5f) go.transform.localScale = Vector3.one * (data.zodHeight / b.size.y);
                    }
                    a.rig = a.anim.GetComponent<ZodTalkRig>(); if (!a.rig) a.rig = a.anim.gameObject.AddComponent<ZodTalkRig>();
                    if (!a.rig.mouthCurves) a.rig.SetCurves(Resources.Load<TextAsset>(ZodStage.CurvesPath));
                    a.rig.nodDeg = ZodNod; a.rig.swayDeg = ZodSway;
                    a.head = FindDeep(go.transform, "mixamorigHead");
                }
                else
                {
                    a.head = FindDeep(go.transform, "Head"); a.mouth = FindDeep(go.transform, "MOUTH");
                    foreach (var n in new[] { "FUEL_CAN", "FUEL_CAN_R" }) { var can = FindDeep(go.transform, n); if (can) can.gameObject.SetActive(false); }   // no drink in this scene
                    if (a.mouth)
                    {   // which way opens the jaw: a point in front of the hinge must drop (as OzL16LoungerSet)
                        a.mouthRest = a.mouth.localRotation;
                        Vector3 front = -go.transform.forward, local = Quaternion.Inverse(a.mouth.rotation) * front * 0.1f;
                        float y0 = a.mouth.TransformPoint(local).y;
                        a.mouth.localRotation = a.mouthRest * Quaternion.Euler(10f, 0f, 0f);
                        a.jawSign = a.mouth.TransformPoint(local).y < y0 ? 1f : -1f;
                        a.mouth.localRotation = a.mouthRest;
                    }
                    else Warn(ad.who + ": no MOUTH node: his jaw stays shut");
                }
                actors.Add(a);
            }
            BuildPoser();
            // the cloud's glow on the three: a warm light ahead of them, over the water, that dies down through the line
            var lg = new GameObject("CloudGlow"); lg.transform.SetParent(root, false); lg.transform.localPosition = GlowAt;
            glow = lg.AddComponent<Light>(); glow.type = LightType.Point; glow.color = new Color(1f, 0.5f, 0.2f); glow.range = data.glowRange > 1f ? data.glowRange : 20f; glow.intensity = 0f; glow.shadows = LightShadows.None;
            // a warm key from the fire's side and a warm rim from behind (nothing blue on this set): set lights, the window only, as the glow
            SetLight("WatchKey", KeyAt, new Color(1f, 0.84f, 0.66f), KeyPower, 18f);
            SetLight("WatchRim", RimAt, new Color(1f, 0.9f, 0.78f), RimPower, 10f);                       // two rims close behind the three: the commander's dark bronze reads by its edges
            SetLight("WatchRimB", RimAtB, new Color(1f, 0.9f, 0.78f), RimPower, 10f);
            SetLight("WatchFill", FillAt, new Color(1f, 0.72f, 0.5f), FillPower, 12f);                    // the fires on the water, low from the river side: faces under a hat brim stay readable at dawn
            {
                string hs = "";
                foreach (var a in actors)
                {
                    bool any = false; var b = new Bounds(a.root.position, Vector3.zero);
                    foreach (var rr in a.rends) { if (!rr || rr is ParticleSystemRenderer) continue; if (!any) { b = rr.bounds; any = true; } else b.Encapsulate(rr.bounds); }
                    hs += $"{a.who}: top {b.max.y - a.root.position.y:F2} m, lowest {b.min.y - root.position.y:F3} m, scale {a.root.localScale.y:F2}; ";
                }
                Debug.Log("[OpenZ L18 watch] heights over the deck: " + hs);
            }
            // the window's camera: the level, and the set on its own layer; its own depth of field (a volume on the set's layer)
            var cgo = new GameObject("L18WatchWindowCamera"); cgo.transform.SetParent(root, false);
            cam = cgo.AddComponent<Camera>(); var main = Camera.main;
            if (main) cam.CopyFrom(main);
            cam.enabled = false; cam.targetTexture = null; cam.depth = -30f; cam.nearClipPlane = 0.08f; cam.farClipPlane = Mathf.Min(main ? main.farClipPlane : 600f, 600f); cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.cullingMask = ((main ? main.cullingMask : ~0) & ~(1 << 5)) | (1 << SetLayer); cam.useOcclusionCulling = false;
            var cd = cam.GetUniversalAdditionalCameraData(); cd.renderPostProcessing = true; cd.renderShadows = true; cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var md = main ? main.GetUniversalAdditionalCameraData() : null;
            cd.volumeLayerMask = (md ? md.volumeLayerMask.value : 1) | (1 << SetLayer);
            var vgo = new GameObject("L18WatchLens"); vgo.transform.SetParent(root, false);
            vol = vgo.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 60f; var prof = ScriptableObject.CreateInstance<VolumeProfile>(); prof.hideFlags = HideFlags.DontSave; vol.sharedProfile = prof;
            dof = prof.Add<DepthOfField>(true); dof.mode.Override(DepthOfFieldMode.Bokeh); dof.focalLength.Override(Focal); dof.aperture.Override(Aperture); dof.focusDistance.Override(5f);
            vol.enabled = false;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = SetLayer;
            sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false; sfx.spatialBlend = 0f; sfx.priority = 40;
            TakeLists();
            if (data.shapes != null)
                foreach (var d in data.shapes)
                {   // shapes on the line's clock, read off the body the robot wears after the swap above
                    var a = Find(d.who); var m = a != null && a.body ? a.body.sharedMesh : null; int ix = -1;
                    if (m) for (int i = 0; i < m.blendShapeCount; i++) { string n = m.GetBlendShapeName(i); if (n == d.shape || n.EndsWith("." + d.shape)) ix = i; }
                    if (ix < 0) { Warn(d.who + ": no shape " + d.shape + " on his body"); continue; }
                    shapeTracks.Add(new ShapeTrack { skin = a.body, index = ix, d = d }); ShapesBuilt++;
                }
            PlaceNote = $"on the red watch post ({root.position.x:F1}, {root.position.y:F2}, {root.position.z:F1})";
            Show(false);
            Debug.Log($"[OpenZ L18 watch] built {PlaceNote}: {actors.Count} characters, {TakesBuilt} authored takes, {ShapesBuilt} shape tracks, {PosesBuilt} poses of the commander in {zkeys.Count} moves, {looks.Count} head turns, {data.shots.Length} shots");
            return true;
        }

        void TakeLists()
        {
            beats.Clear(); looks.Clear(); sounds.Clear(); zkeys.Clear();
            if (data.beats != null) { beats.AddRange(data.beats); beats.Sort((a, b) => a.t.CompareTo(b.t)); }
            if (data.looks != null) { looks.AddRange(data.looks); looks.Sort((a, b) => a.t.CompareTo(b.t)); foreach (var l in looks) WorstTurn = Mathf.Max(WorstTurn, Mathf.Abs(l.yaw)); }
            if (data.sounds != null) { sounds.AddRange(data.sounds); sounds.Sort((a, b) => a.t.CompareTo(b.t)); foreach (var s in sounds) Clip(s.clip); }
            if (data.zkeys != null) { zkeys.AddRange(data.zkeys); zkeys.Sort((a, b) => a.t.CompareTo(b.t)); }
        }

        Actor Find(string who) { foreach (var a in actors) if (a.who == who) return a; return null; }

        // ------------------------------------------------------------------------------------------------ Zod's poses
        static int Muscle(string name) { var n = HumanTrait.MuscleName; for (int i = 0; i < n.Length; i++) if (n[i] == name) return i; return -1; }
        static readonly string[] LegWords = { "Upper Leg", "Lower Leg", "Foot", "Toes" };
        /// <summary>A muscle of a pose by its name: set to v, or (a name with a leading +) moved by v.</summary>
        static void SetMuscle(float[] m, string name, float v)
        {
            bool add = name.StartsWith("+"); int k = Muscle(add ? name.Substring(1) : name);
            if (k < 0) { Warn("zod: no muscle named " + name); return; }
            m[k] = add ? m[k] + v : v;
        }
        /// <summary>Play check: the muscles of one of Zod's built poses, by name (for tuning the scene file).</summary>
        public static string LabMuscles(string pose)
        {
            if (!I || I.poser == null || !I.poser.poses.TryGetValue(pose, out var m)) return "no such pose";
            var sb = new System.Text.StringBuilder(); var n = HumanTrait.MuscleName;
            for (int i = 0; i < m.Length; i++) if (i < 55 || Mathf.Abs(m[i]) > 0.02f) sb.Append(n[i] + " " + m[i].ToString("F2") + "; ");
            return sb.ToString();
        }

        /// <summary>Samples Zod's poses out of his own controller's states (hidden, at build) into muscle space, gives every pose the one wide
        /// stance, and notes where his feet stand: from then on his Animator rests and the poser writes him.</summary>
        void BuildPoser()
        {
            var z = Find("zod"); poser = null; PosesBuilt = 0;
            if (z == null || data.zposes == null || data.zposes.Length == 0 || data.zkeys == null || data.zkeys.Length == 0) return;
            if (!z.anim.avatar || !z.anim.avatar.isHuman) { Warn("zod: his avatar is not a humanoid one: he plays his stock states"); return; }
            var p = new ZodPoser { root = z.anim.transform, toeOut = data.toeOut };
            p.hips = z.anim.GetBoneTransform(HumanBodyBones.Hips); p.footL = z.anim.GetBoneTransform(HumanBodyBones.LeftFoot); p.footR = z.anim.GetBoneTransform(HumanBodyBones.RightFoot);
            if (!p.hips || !p.footL || !p.footR) { Warn("zod: hips or feet not found on his avatar"); return; }
            z.anim.enabled = true;
            p.h = new HumanPoseHandler(z.anim.avatar, z.anim.transform); p.hp = new HumanPose();
            float[] legs = null; bool firstPose = true;
            foreach (var d in data.zposes)
            {
                if (d == null || string.IsNullOrEmpty(d.name)) continue;
                if (!z.anim.HasState(0, Animator.StringToHash(d.state))) { Warn("zod: no animation state " + d.state + " for the pose " + d.name); continue; }
                z.anim.Play(d.state, 0, Mathf.Clamp01(d.nt)); z.anim.Update(0f);
                p.h.GetHumanPose(ref p.hp);
                var m = (float[])p.hp.muscles.Clone();
                if (firstPose)
                {   // the first pose is his stance: where the feet stand flat in it is where they stay; its legs, widened, are every pose's legs
                    firstPose = false; p.bodyPos = p.hp.bodyPosition; p.bodyRot = Upright ? z.anim.transform.localRotation : p.hp.bodyRotation;   // the commander stands square: no hip-shot tilt of the stock idle
                    p.footRefL = Quaternion.Inverse(p.root.rotation) * p.footL.rotation; p.footRefR = Quaternion.Inverse(p.root.rotation) * p.footR.rotation;
                    p.feetMid = p.root.InverseTransformPoint((p.footL.position + p.footR.position) * 0.5f); p.feetMid.x = 0f; p.feetMid.y += SoleLift / Mathf.Max(0.01f, p.root.lossyScale.y);
                    if (data.stanceM != null && data.stanceV != null)
                        for (int i = 0; i < data.stanceM.Length && i < data.stanceV.Length; i++) SetMuscle(m, data.stanceM[i], data.stanceV[i]);
                    legs = (float[])m.Clone();
                }
                else
                {
                    var names = HumanTrait.MuscleName;
                    for (int i = 0; i < m.Length; i++) foreach (var w in LegWords) if (names[i].Contains(w)) { m[i] = legs[i]; break; }
                }
                if (d.m != null && d.v != null)
                    for (int i = 0; i < d.m.Length && i < d.v.Length; i++) SetMuscle(m, d.m[i], d.v[i]);
                p.poses[d.name] = m; if (p.first == null) p.first = m; PosesBuilt++;
            }
            if (p.first == null) { p.h.Dispose(); return; }
            p.cur = (float[])p.first.Clone(); p.ok = true; poser = p;
            z.anim.enabled = false;                                                                      // from here the poser writes him (Show keeps it so)
        }

        static float Ease5(float u) { u = Mathf.Clamp01(u); return u * u * u * (u * (u * 6f - 15f) + 10f); }

        /// <summary>Zod at the line's second t: the poses folded in order (each move eased in and out from wherever he was), a slow breath, the
        /// feet set flat on their marks and the hips carried so that they never slide.</summary>
        void PoseZod(float t)
        {
            var p = poser; if (p == null || !p.ok) return;
            Array.Copy(p.first, p.cur, p.cur.Length);
            foreach (var k in zkeys)
            {
                if (k.t > t) break;
                if (!p.poses.TryGetValue(k.pose, out var to)) continue;
                float w = Ease5((t - k.t) / Mathf.Max(0.05f, k.ease));
                if (w >= 1f) Array.Copy(to, p.cur, to.Length); else for (int i = 0; i < to.Length; i++) p.cur[i] += (to[i] - p.cur[i]) * w;
            }
            if (ZodBreath > 0f) { int c = Muscle("Chest Front-Back"); if (c >= 0) p.cur[c] += ZodBreath * Mathf.Sin(t * 1.5f); }
            p.hp.bodyPosition = p.bodyPos; p.hp.bodyRotation = p.bodyRot; p.hp.muscles = p.cur;
            p.h.SetHumanPose(ref p.hp);
            p.footL.rotation = p.root.rotation * Quaternion.Euler(0f, -p.toeOut, 0f) * p.footRefL;
            p.footR.rotation = p.root.rotation * Quaternion.Euler(0f, p.toeOut, 0f) * p.footRefR;
            Vector3 mid = (p.footL.position + p.footR.position) * 0.5f, want = p.root.TransformPoint(p.feetMid);
            p.hips.position += want - mid;
        }

        /// <summary>
        /// the bags' own flat mapping; rough, no shine.</summary>
        Texture2D sackTex, sackBump;
        Material Sack(Color32 c)
        {
            var m = Lit(c, 0.02f, 0f); if (!m) return null;
            if (!sackTex)
            {
                const int n = 256, th = 16; float cell = n / (float)th;
                sackTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, hideFlags = HideFlags.DontSave };
                sackBump = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, hideFlags = HideFlags.DontSave };
                var hgt = new float[n * n]; var col = new Color32[n * n]; var nrm = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int i = (int)(x / cell), j = (int)(y / cell); float fx = x / cell - i, fy = y / cell - j;
                        bool over = ((i + j) & 1) == 0;                                                // the thread on top here: along x or along y
                        hgt[y * n + x] = (over ? Mathf.Sin(Mathf.PI * fy) : Mathf.Sin(Mathf.PI * fx)) * (0.75f + 0.25f * Mathf.Sin(Mathf.PI * (over ? fx : fy)));
                    }
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float h = hgt[y * n + x], hx = hgt[y * n + (x + 1) % n] - hgt[y * n + (x + n - 1) % n], hy = hgt[((y + 1) % n) * n + x] - hgt[((y + n - 1) % n) * n + x];
                        float dirt = 0.82f + 0.18f * Mathf.PerlinNoise(x * 6f / n * 2.3f + 3.1f, y * 6f / n * 2.3f + 7.7f) * (0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * x / n)) + 0.06f * Mathf.Sin(2f * Mathf.PI * (x + 2 * y) / n);
                        byte v = (byte)Mathf.Clamp(255f * (0.76f + 0.24f * h) * dirt, 0f, 255f); col[y * n + x] = new Color32(v, v, v, 255);
                        var nv = new Vector3(-hx * 1.6f, -hy * 1.6f, 1f).normalized; nrm[y * n + x] = new Color32((byte)(127.5f + 127.5f * nv.x), (byte)(127.5f + 127.5f * nv.y), (byte)(127.5f + 127.5f * nv.z), 255);
                    }
                sackTex.SetPixels32(col); sackTex.Apply(true, true); sackBump.SetPixels32(nrm); sackBump.Apply(true, true);
            }
            if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", sackTex); m.SetTextureScale("_BaseMap", new Vector2(SackTile, SackTile)); }
            if (m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", sackBump); m.SetTextureScale("_BumpMap", new Vector2(SackTile, SackTile)); m.EnableKeyword("_NORMALMAP"); if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 0.9f); }
            return m;
        }
        /// <summary>Sack weave: tiles of 16 threads a unit of the bags' mapping (about 1.76 m of the set): 5.2 = threads of about 2 cm.</summary>
        public static float SackTile = 5.2f;

        Material Lit(Color32 c, float smooth, float metal)
        {
            var t = Resources.Load<Material>(ZodStage.MatPath + "ZodStage_Lit"); Material m = null;
            if (t) m = new Material(t) { hideFlags = HideFlags.DontSave };
            else { var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh) m = new Material(sh) { hideFlags = HideFlags.DontSave }; }
            if (!m) return null;
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal); madeMats.Add(m);
            return m;
        }

        AudioClip Clip(string res)
        {
            if (string.IsNullOrEmpty(res)) return null;
            if (!clips.TryGetValue(res, out var c)) clips[res] = c = Resources.Load<AudioClip>(res);
            return c;
        }

        void Sound(OzWatchSound s)
        {   // part of the transmission: 2D, at the voice's gain + db; every moment logged
            var c = Clip(s.clip); string key = "brief_" + (string.IsNullOrEmpty(s.key) ? s.clip : s.key);
            if (!c) { OzSoundLog.Add("L18Watch", key, s.clip, "missing clip"); return; }
            float g = Mathf.Clamp01(voiceGain * Mathf.Pow(10f, s.db / 20f));
            sfx.pitch = s.pitch > 0f ? s.pitch : 1f; sfx.PlayOneShot(c, g);
            OzSoundLog.Add("L18Watch", key, c.name, "played", g);
        }

        void Play(Actor a, string state, float fade)
        {
            if (a == null || !a.anim || string.IsNullOrEmpty(state)) return;
            if (!a.anim.HasState(0, Animator.StringToHash(state))) { Warn(a.who + ": no animation state " + state); return; }
            if (fade <= 0f) a.anim.Play(state, 0, 0f); else a.anim.CrossFadeInFixedTime(state, fade, 0);
            BeatsPlayed++;
        }

        // ------------------------------------------------------------------------------------------------ the briefing
        /// <summary>ZodBriefing: the three hold this line. Cues (start, end on the voice clip) of Alan, Brad and Zod; the line's clock.</summary>
        public bool BriefBegin(ZodStage st, AudioSource voiceSource, List<Vector2> cuesA, List<Vector2> cuesB, List<Vector2> cuesZ, Func<float> lineClock, float gain)
        {
            if (!EnsureBuilt() || !st) return false;
            LabT = float.NaN; LabShot = -1;
            stage = st; voice = voiceSource; clock = lineClock; voiceGain = gain;
            foreach (var a in actors)
            {
                a.cues.Clear(); var src = a.who == "alan" ? cuesA : a.who == "brad" ? cuesB : cuesZ; if (src != null) a.cues.AddRange(src);
                a.jaw = 0f; a.yaw = a.pitch = 0f; a.backAt = -1f; a.backTo = null;
                if (a.rig) { a.rig.voice = voiceSource; a.rig.gazeYaw = 0f; a.rig.gazePitch = 0f; a.rig.nodDeg = ZodNod; a.rig.swayDeg = ZodSway; }
            }
            beatI = soundI = 0; shotI = -1; lastT = -99f; overFor = 0f; faceAimHas = false;
            Show(true);
            briefOn = true; Lines++;
            OnlyInTheWindow(true);
            stage.LoungerFeed(cam);
            TickBrief(0f);                                                                              // the poses of the line's first second before the first picture
            Debug.Log($"[OpenZ L18 watch] briefing on the watch post: cues Alan {cuesA?.Count ?? 0}, Brad {cuesB?.Count ?? 0}, Zod {cuesZ?.Count ?? 0}; window camera into the transmission feed");
            return true;
        }

        void BriefEnd()
        {
            if (!briefOn) return;
            briefOn = false; clock = null; voice = null;
            OnlyInTheWindow(false);
            if (stage) stage.LoungerFeed(null);
            foreach (var a in actors) { a.cues.Clear(); a.jaw = 0f; if (a.rig) { a.rig.voice = null; a.rig.gazeYaw = 0f; a.rig.gazePitch = 0f; } }
            if (glow) glow.intensity = 0f;
            Show(false);
            Debug.Log("[OpenZ L18 watch] briefing over: the set is hidden, the window is the commander's again");
        }

        // The set stands on the level's ground, so the full-screen fly-over would draw it too. It belongs to the window only: every other
        // camera renders the frame without it and its light (the method of OzL07HotTubSet).
        readonly List<Light> lightsOff = new List<Light>(); Renderer[] setRends; Light[] setLights;
        /// <summary>The set's own lights on the three (point lights under the set's root: lit in the window only, off with the set) and their places.</summary>
        public static float KeyPower = 55f, RimPower = 45f, FillPower = 30f;
        public static Vector3 KeyAt = new Vector3(3.5f, 5.5f, 5.5f), RimAt = new Vector3(-2.6f, 4.6f, -1.8f), RimAtB = new Vector3(2.8f, 4.6f, -2.0f), GlowAt = new Vector3(-3.5f, 3.4f, 6.5f), FillAt = new Vector3(-2.4f, 2.6f, 4.4f);
        void SetLight(string name, Vector3 at, Color c, float power, float range)
        {
            var g = new GameObject(name); g.transform.SetParent(root, false); g.transform.localPosition = at;
            var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.range = range; l.intensity = power; l.shadows = LightShadows.None;
        }

        void OnlyInTheWindow(bool on)
        {
            if (on) foreach (var c in Camera.allCameras) if (c && c != cam && c.cameraType == CameraType.Game && c.cullingMask != 1 << SetLayer) c.cullingMask &= ~(1 << SetLayer);
            RenderPipelineManager.beginCameraRendering -= HideFrom; RenderPipelineManager.endCameraRendering -= ShowAgain;
            if (!on) { if (setRends != null) foreach (var r in setRends) if (r) r.forceRenderingOff = false; foreach (var l in lightsOff) if (l) l.enabled = true; lightsOff.Clear(); if (vol) vol.enabled = false; return; }
            if (!root) return;
            setRends = root.GetComponentsInChildren<Renderer>(true); setLights = root.GetComponentsInChildren<Light>(true);
            RenderPipelineManager.beginCameraRendering += HideFrom; RenderPipelineManager.endCameraRendering += ShowAgain;
        }
        bool WindowOwn(Camera c) => c == cam || c.cameraType == CameraType.SceneView;
        /// <summary>Play check, at every moment of a level: how many of the set's renderers the game camera could draw now. 0 is the only good answer.</summary>
        public static int MainCanDraw()
        {
            var main = Camera.main; if (!I || !I.root || !main) return 0;
            int n = 0; foreach (var r in I.root.GetComponentsInChildren<Renderer>(true)) if (r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff && (main.cullingMask & (1 << r.gameObject.layer)) != 0) n++;
            return n;
        }
        void HideFrom(ScriptableRenderContext ctx, Camera c)
        {
            if (vol) vol.enabled = c == cam && Bokeh;                                                     // the lens's depth of field is the window camera's only
            if (WindowOwn(c) || setRends == null) return;
            foreach (var r in setRends) if (r) r.forceRenderingOff = true;
            foreach (var l in setLights) if (l && l.enabled) { l.enabled = false; lightsOff.Add(l); }
        }
        void ShowAgain(ScriptableRenderContext ctx, Camera c)
        {
            if (vol) vol.enabled = false;
            if (WindowOwn(c) || setRends == null) return;
            foreach (var r in setRends) if (r) r.forceRenderingOff = false;
            foreach (var l in lightsOff) if (l) l.enabled = true;
            lightsOff.Clear();
        }

        void Show(bool on)
        {
            drawn = on;
            if (!root) return;
            if (!on) { hidden.Clear(); foreach (var r in root.GetComponentsInChildren<Renderer>(true)) if (r.enabled) { r.enabled = false; hidden.Add(r); } }
            else { foreach (var r in hidden) if (r) r.enabled = true; hidden.Clear(); }
            foreach (var l in root.GetComponentsInChildren<Light>(true)) l.enabled = on;
            foreach (var a in actors) if (a.anim) a.anim.enabled = on && !(a.who == "zod" && poser != null);
        }

        /// <summary>The takes' own clock: the line's, coming to rest at takeStop (their old closing lean is not played).</summary>
        float TakeClock(float tk)
        {
            float s = data.takeStop, d = Mathf.Max(0.1f, data.takeStopEase);
            if (s <= 0f || tk <= s - d) return tk;
            return s - d + d * (1f - Mathf.Exp(-(tk - (s - d)) / d));
        }

        /// <summary>Everything that is a pure function of the line's second: the takes, the shapes, Zod's pose, the head turns, the shot.</summary>
        void EvalAt(float tk, float ts)
        {
            float tt = TakeClock(tk);
            foreach (var a in actors)
                if (a.hasTake && a.graph.IsValid()) { a.take.SetTime(Mathf.Clamp(tt - a.takeT0, 0f, Mathf.Max(0f, a.takeLen - 0.002f))); a.graph.Evaluate(0f); }
            foreach (var s in shapeTracks)
                if (s.skin) s.skin.SetBlendShapeWeight(s.index, 100f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.d.t0, s.d.t1, tk)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.d.t2, s.d.t3, tk))));
            PoseZod(tk);
            foreach (var a in actors) { a.yaw = 0f; a.pitch = 0f; }
            foreach (var l in looks)
            {   // the head turns, folded in order: eased in and out, never a snap
                if (l.t > tk) break;
                var a = Find(l.who); if (a == null) continue;
                float w = Ease5((tk - l.t) / Mathf.Max(0.2f, l.seconds));
                a.yaw += (Mathf.Clamp(l.yaw, -HeadTurnMax, HeadTurnMax) - a.yaw) * w; a.pitch += (Mathf.Clamp(l.pitch, -25f, 25f) - a.pitch) * w;
            }
            foreach (var a in actors) if (a.rig) { a.rig.gazeYaw = a.yaw * ZodGazeSign; a.rig.gazePitch = a.pitch; }
            int want = 0;
            for (int i = 0; i < data.shots.Length; i++) if (data.shots[i].t0 <= ts) want = i;
            if (LabShot >= 0 && !briefOn) want = Mathf.Min(LabShot, data.shots.Length - 1);
            if (want != shotI)
            {
                shotI = want; faceAimHas = false;
                var cd = cam ? cam.GetUniversalAdditionalCameraData() : null; if (cd) cd.resetHistory = true;      // no smeared frame across a cut
            }
            // the cloud's glow: bright in the first seconds, a slow flicker, down to a third by the end of the line
            if (glow) glow.intensity = (data.glowPower > 0f ? data.glowPower : 6f) * Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(ts / 14f)) * (0.85f + 0.15f * Mathf.PerlinNoise(ts * 3.1f, 1.7f)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.95f, -0.75f, ts));
        }

        void TickBrief(float dt)
        {
            float t = clock != null ? clock() : float.NaN;
            if (float.IsNaN(t)) { BriefEnd(); return; }
            bool over = float.IsPositiveInfinity(t);
            if (over) { overFor += dt; if (overFor > ClosingMost) { BriefEnd(); return; } }
            if (!over)
            {
                lastT = t;
                while (beatI < beats.Count && beats[beatI].t <= t)
                {
                    var b = beats[beatI++]; var a = Find(b.who); if (a == null || a.hasTake || (a.who == "zod" && poser != null)) continue;   // a character with a take or poses plays that, never a stock state
                    Play(a, b.clip, b.fade);
                    if (b.hold > 0f && !string.IsNullOrEmpty(b.back)) { a.backAt = b.t + b.hold; a.backTo = b.back; } else { a.backAt = -1f; a.bed = b.clip; }
                }
                foreach (var a in actors) if (a.backAt >= 0f && t >= a.backAt) { a.backAt = -1f; Play(a, a.backTo, 0.35f); a.bed = a.backTo; }
                while (soundI < sounds.Count && sounds[soundI].t <= t) Sound(sounds[soundI++]);
            }
            EvalAt(over ? lastT + overFor : t, over ? lastT : t);
        }

        void PlaceCamera()
        {
            if (!cam || shotI < 0) return;
            var s = data.shots[shotI];
            float ts = lastT, next = shotI + 1 < data.shots.Length ? data.shots[shotI + 1].t0 : s.t0 + 4f;
            float len = Mathf.Max(0.3f, next - s.t0), u = Mathf.Clamp01((ts - s.t0) / len), e = Mathf.Lerp(u, 1f - (1f - u) * (1f - u), 0.45f);
            Vector3 p = root.TransformPoint(new Vector3(s.px + s.mx * e, s.py + s.my * e, s.pz + s.mz * e)), at = root.TransformPoint(new Vector3(s.tx + s.ax * e, s.ty + s.ay * e, s.tz + s.az * e));
            float fov = s.fov, calm = 1f;
            if (s.kind == "push" && s.zoomFov > 0f) fov = Mathf.Lerp(s.fov, s.zoomFov, e);
            if (s.kind == "face")
            {   // the crash zoom on the speaker: hard off the mark, lands in landSeconds, the aim on his head bone as the clip has it
                var who = Find(s.who); Vector3 want = who != null && who.head ? who.head.position + Vector3.up * (FaceUp + s.ay) : at;   // ay on a face shot: the aim's height over the head's mark (under 0 = his eyes ride higher in the picture)
                if (!faceAimHas) { faceAim = want; faceAimHas = true; }
                faceAim = Vector3.Lerp(faceAim, want, 1f - Mathf.Exp(-Mathf.Min(Time.unscaledDeltaTime, 0.1f) / 0.2f));
                float k = Mathf.Clamp01((ts - s.t0) / Mathf.Max(0.05f, s.landSeconds)), q = 1f - (1f - k) * (1f - k) * (1f - k);
                at = Vector3.Lerp(at, faceAim, q); fov = Mathf.Lerp(s.fov, s.lfov > 0f ? s.lfov : s.fov, q); calm = fov / s.fov;
            }
            var look = Quaternion.LookRotation(at - p, Vector3.up);
            if (!OzMotion.Reduced)
            {   // hand-held: a slow drift on the line's own clock (a held clock is a held picture)
                float n1 = Mathf.PerlinNoise(ts * 0.55f, 3.7f + shotI) - 0.5f, n2 = Mathf.PerlinNoise(8.3f + shotI, ts * 0.5f) - 0.5f;
                p += look * new Vector3(n2 * 2f * Drift * calm, n1 * 2f * Drift * calm, 0f);
                look *= Quaternion.Euler(n1 * 2f * Sway * calm, n2 * 2f * Sway * calm, 0f);
            }
            // the blasts' shake: the post jolts with the bridges (the same share of the window's height as of the big picture)
            Vector3 ks = OzL18BriefingStage.KickShare; float kw = s.shake > 0f ? s.shake : WindowShake;
            if (kw > 0f && ks.sqrMagnitude > 1e-10f) look *= Quaternion.Euler(ks.y * fov * kw, ks.x * fov * kw, ks.z * kw);
            cam.transform.SetPositionAndRotation(p, look); cam.fieldOfView = fov;
            if (dof != null)
            {
                Vector3 f = at; var fw = string.IsNullOrEmpty(s.focus) ? null : Find(s.focus); if (fw != null && fw.head) f = fw.head.position;
                dof.focusDistance.value = Mathf.Max(0.3f, Vector3.Dot(f - p, look * Vector3.forward));
                dof.aperture.value = s.aperture > 0f ? s.aperture : Aperture; dof.focalLength.value = s.focal > 0f ? s.focal : Focal;
            }
        }

        float JawFromVoice(Actor a)
        {
            var v = voice;
            if (!v || !v.clip || !v.isPlaying || a.cues.Count == 0) return 0f;
            float t = v.timeSamples / (float)Mathf.Max(1, v.clip.frequency), tl = ZodBriefing.LineWarp("zod_brief_L18", t); bool mine = false;   // the cues are seconds of the uncut line
            foreach (var c in a.cues) if (tl >= c.x - 0.12f && tl <= c.y + 0.2f) { mine = true; break; }
            if (!mine) return 0f;
            float j = ZodStage.LoungerJaw(v.clip, tl);                                                  // the robots' curve is also on the uncut line's seconds
            return j >= 0f ? j : 0f;
        }

        // ------------------------------------------------------------------------------------------------ frame
        void Update()
        {
            if (!built) return;
            if (!briefOn)
            {
                if (float.IsNaN(LabT)) return;
                lastT = LabT; EvalAt(LabT, LabT);                                                       // tuning in Play: one second of the line, held
                return;
            }
            bool paused = AudioListener.pause;
            foreach (var a in actors) if (a.anim) a.anim.speed = paused ? 0f : 1f;
            TickBrief(paused ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.1f));
        }

        void LateUpdate()
        {
            bool lab = !briefOn && !float.IsNaN(LabT);
            if (built && !briefOn && !lab && drawn) Show(false);                                            // no line on the air, no set in the level: every frame, whatever path ended the line
            if (!built || !(briefOn || lab)) return;
            float dt = AudioListener.pause ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            foreach (var a in actors)
            {
                if (a.rig) continue;                                                                    // Zod: his own rig moves his jaw and turns his head
                float target = JawFromVoice(a);
                if (dt > 0f) a.jaw += (target - a.jaw) * (1f - Mathf.Exp(-dt / (target > a.jaw ? 0.025f : 0.045f)));
                a.jaw = Mathf.Clamp01(a.jaw);
                if (a.mouth) a.mouth.localRotation = a.mouthRest * Quaternion.Euler(a.jaw * a.jawDeg * a.jawSign, 0f, 0f);
                if (a.head && (Mathf.Abs(a.yaw) > 0.01f || Mathf.Abs(a.pitch) > 0.01f))
                    a.head.rotation = Quaternion.AngleAxis(a.yaw, Vector3.up) * Quaternion.AngleAxis(a.pitch, a.root.right * (a.rig ? 1f : -1f)) * a.head.rotation;   // the take wrote the head this frame: the turn is added once (the robots' models face -Z: their right is the root's left)
            }
            PlaceCamera();
        }

        // ------------------------------------------------------------------------------------------------ tuning in Play (no briefing running)
        /// <summary>Shows the set at second t of the line (NaN = off); shot = the picture's index (-1 = the clock's).</summary>
        public static string LabSet(float t, int shot = -1)
        {
            if (!I || !I.EnsureBuilt()) return "no set";
            if (I.briefOn) return "a briefing is running";
            LabT = t; LabShot = shot;
            if (float.IsNaN(t)) { I.OnlyInTheWindow(false); I.Show(false); return "lab off"; }
            if (!I.drawn) { I.Show(true); I.OnlyInTheWindow(true); }
            return "lab at " + t.ToString("F2") + " s, shot " + shot;
        }

        /// <summary>A new scene file's text while in Play: shots, head turns, sounds, the takes' rest and Zod's poses are taken from it (the
        /// actors move to its marks); the post and the takes stay as built.</summary>
        public static string LabData(string json)
        {
            if (!I || !I.built) return "no set";
            var d = JsonUtility.FromJson<OzWatchFile>(json); if (d == null || d.shots == null) return "not readable";
            var old = I.data; I.data = d; I.TakeLists();
            foreach (var ad in d.actors) { var a = I.Find(ad.who); if (a != null) { a.root.localPosition = new Vector3(ad.x, 0f, ad.z); a.root.localRotation = Quaternion.Euler(0f, ad.yaw + (ad.who == "zod" ? ZodYaw : RobotYaw), 0f); } }
            if (I.poser != null && I.poser.h != null) I.poser.h.Dispose();
            bool was = I.drawn; if (!was) I.Show(true);
            I.BuildPoser();
            if (!was) I.Show(false);
            I.shotI = -1;
            return $"data in: {d.shots.Length} shots, {PosesBuilt} poses, {I.zkeys.Count} moves, {I.looks.Count} head turns" + (LastWarning.Length > 0 ? " | last warning: " + LastWarning : "");
        }

        /// <summary>The window camera's picture of this frame into a PNG (w x h); cx.. = a free lens in the set's frame instead of the shot's.</summary>
        public static string LabRender(string path, int w = 640, int h = 480, bool free = false, float px = 0f, float py = 0f, float pz = 0f, float tx = 0f, float ty = 0f, float tz = 0f, float fov = 40f)
        {
            if (!I || !I.built || !I.cam) return "no set";
            var c = I.cam; var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); rt.antiAliasing = 4;
            var was = c.targetTexture; float asp = c.aspect; bool en = c.enabled;
            if (free) { c.transform.SetPositionAndRotation(I.root.TransformPoint(new Vector3(px, py, pz)), Quaternion.LookRotation(I.root.TransformPoint(new Vector3(tx, ty, tz)) - I.root.TransformPoint(new Vector3(px, py, pz)), Vector3.up)); c.fieldOfView = fov; if (I.dof != null) I.dof.focusDistance.value = Vector3.Distance(new Vector3(px, py, pz), new Vector3(tx, ty, tz)); }
            c.targetTexture = rt; c.aspect = w / (float)h; c.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev; c.targetTexture = was; c.aspect = asp; c.enabled = en;
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG()); Destroy(tex); RenderTexture.ReleaseTemporary(rt);
            return "saved " + path + " (shot " + I.ShotName + ", clock " + I.lastT.ToString("F2") + ")";
        }

        /// <summary>The lowest point of each character's skin over the deck's floor now (m; under 0 = in the floor), and of each foot bone.</summary>
        public static string LabFeet()
        {
            if (!I || !I.built) return "no set";
            var sb = new System.Text.StringBuilder(); var mesh = new Mesh();
            foreach (var a in I.actors)
            {
                float lo = float.MaxValue;
                foreach (var r in a.rends)
                {
                    var s = r as SkinnedMeshRenderer; if (!s || !s.sharedMesh) continue;
                    s.BakeMesh(mesh, true); var vs = mesh.vertices; var m = s.transform.localToWorldMatrix;
                    for (int i = 0; i < vs.Length; i += 3) { float y = m.MultiplyPoint3x4(vs[i]).y; if (y < lo) lo = y; }
                }
                sb.Append($"{a.who}: lowest skin {lo - I.root.position.y:F3} m; ");
            }
            Destroy(mesh);
            return sb.ToString();
        }

        /// <summary>One-line state for the Play check.</summary>
        public static string Status()
        {
            if (!I) return "L18 watch set: none in this scene (level 18, red side)";
            if (!I.built) return "L18 watch set: NOT built (" + LastWarning + ")";
            return $"L18 watch set {PlaceNote}: game camera can draw {MainCanDraw()} of its renderers, briefing {I.briefOn}, shot {I.ShotName}, clock {I.lastT:F2} s, lines {Lines}, authored takes {TakesBuilt} of 2, shape tracks {ShapesBuilt}, the commander's poses {PosesBuilt}, worst head turn asked {WorstTurn:F0} deg"
                 + (LastWarning.Length > 0 ? " | last warning: " + LastWarning : "");
        }
    }
}
