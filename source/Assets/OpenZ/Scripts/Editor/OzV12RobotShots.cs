using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Clean studio pictures of every robot model the game uses, rendered in EDIT mode inside a preview scene
    /// (nothing is added to the open level, nothing is saved, no Play): the game's own prefab, red team paint (TeamPaint and
    /// OzHeroLook as the game sets them), the idle state of the robot's own Animator controller at its loop start (foot IK on, as
    /// UnitView starts it), the weapon placed by the game's own WeaponHold (one LateUpdate, the Tough's carry pitch from its
    /// BS01Stance), the kit hoses rebuilt; a neutral studio light (key, fill, two rims, a low bounce) that turns with the camera,
    /// a dark gradient backdrop, 2048 x 2048 PNG, every frame fitted to the posed mesh itself.
    /// Per robot: full body + mid shot (hips up, face and weapon in); the laser robot first and also a close-up of head and weapon
    /// All numbers are public statics, so a look can be tuned without a recompile.
    /// </summary>
    public static class OzV12RobotShots
    {
        public static readonly string OutDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/robot_shots"));
        public static int Size = 2048, ThumbSize = 512;
        /// <summary>Seconds into the idle state (0 = its loop start: the calm idle's forward rest pose, feet planted).</summary>
        public static float IdleSeconds = 0f;
        /// <summary>Camera orbit (deg) from the front of the robot's CHEST in the posed idle (the calm idle turns the shoulders a little,
        /// so the chest, not the root, is the robot's front in the picture); + = toward the robot's right side (his weapon side: he
        /// turns a little toward the picture's right).</summary>
        public static float FrontYaw = 18f, CloseYaw = -30f, BackYaw = 150f;
        public static float FullPitch = 6f, MidPitch = 4f, ClosePitch = 0f, BackPitch = 10f;
        public static float FullFov = 20f, MidFov = 20f, CloseFov = 28f;
        /// <summary>Free border on the limiting side (share of the picture).</summary>
        public static float FullMargin = 0.06f, MidMargin = 0.06f, CloseMargin = 0.05f;
        public static float KeyI = 1.6f, FillI = 0.5f, RimI = 1.2f, Rim2I = 0.7f, BounceI = 0.2f;
        public static bool KeyShadows = true;
        public static Color BgCenter = new Color(0.17f, 0.18f, 0.21f), BgEdge = new Color(0.025f, 0.027f, 0.034f);
        /// <summary>Per robot front yaw (key -> deg), when one robot reads better from another side: the Laser's head turns about 30 deg
        /// to his left of his chest in this idle frame, so he is seen a little from his left (face toward the lens, the arm cannon
        /// still in front; the close-up at CloseYaw shows the face square on); the Tough's tube on his right
        /// shoulder hides his head from his right, so he is seen from his left; the Sniper aims down the scope with his shoulders
        /// turned 40 deg, so his camera stays nearer his aim line (the whole rifle reads, its muzzle toward the picture's right).</summary>
        public static readonly Dictionary<string, float> YawFor = new Dictionary<string, float> { { "laser", -15f }, { "tough", -25f }, { "sniper", 5f } };

        public sealed class Bot
        {
            public string key, name, path;
            public Bot(string key, string name, string path) { this.key = key; this.name = name; this.path = path; }
        }

        const string Pref = "Assets/OpenZ/Prefabs", Hero = "Assets/OpenZ/Resources/OpenZ/HeroSquad";
        public static readonly Bot[] Bots =
        {
            new Bot("laser", "Laser robot", Pref + "/Z_bs01_laser.prefab"),
            new Bot("grunt", "Grunt", Pref + "/Z_bs01_grunt.prefab"),
            new Bot("psycho", "Psycho", Pref + "/Z_bs01_psycho.prefab"),
            new Bot("tough", "Tough", Pref + "/Z_bs01_tough.prefab"),
            new Bot("sniper", "Sniper", Pref + "/Z_bs01_sniper.prefab"),
            new Bot("pyro", "Pyro", Pref + "/Z_bs01_pyro.prefab"),
            new Bot("electrobot", "Electrobot", Pref + "/Z_bs01_electrobot.prefab"),
            new Bot("alan", "Alan (hero)", Hero + "/Z_hero_alan.prefab"),
            new Bot("brad", "Brad (hero)", Hero + "/Z_hero_brad.prefab"),
        };

        public enum Part { All, Mid, Close }

        public sealed class Shot
        {
            public string file, label; public Part part; public float yaw, pitch, fov, margin;
        }

        public static List<Shot> ShotsFor(Bot b)
        {
            float y = YawFor.TryGetValue(b.key, out var v) ? v : FrontYaw;
            var l = new List<Shot>
            {
                new Shot { file = b.key + "_full.png", label = "full body", part = Part.All, yaw = y, pitch = FullPitch, fov = FullFov, margin = FullMargin },
                new Shot { file = b.key + "_mid.png", label = "mid shot", part = Part.Mid, yaw = y, pitch = MidPitch, fov = MidFov, margin = MidMargin },
            };
            if (b.key == "laser")
            {
                l.Add(new Shot { file = "laser_close.png", label = "close-up: head and weapon", part = Part.Close, yaw = CloseYaw, pitch = ClosePitch, fov = CloseFov, margin = CloseMargin });
                l.Add(new Shot { file = "laser_back.png", label = "3/4 back view", part = Part.All, yaw = BackYaw, pitch = BackPitch, fov = FullFov, margin = FullMargin });
            }
            return l;
        }

        [MenuItem("OpenZ/V1.2/Robot Shots: render all (full + mid, laser close + back)")]
        static void MenuAll() => Run(null);

        [MenuItem("OpenZ/V1.2/Robot Shots: laser only")]
        static void MenuLaser() => Run(new[] { "laser" });

        /// <summary>Renders the robots in <paramref name="only"/> (keys; null = all), then rewrites index.html from the files on disk.</summary>
        public static string Run(string[] only)
        {
            if (Application.isPlaying) return Say("leave Play mode first");
            Directory.CreateDirectory(OutDir); Directory.CreateDirectory(OutDir + "/thumbs");
            var log = new List<string> { $"OzV12RobotShots {DateTime.Now:yyyy-MM-dd HH:mm}: {Size} px, idle +{IdleSeconds:0.00} s, front yaw {FrontYaw:0} deg" };
            bool easeWas = ToughAimEase.On; ToughAimEase.On = false;                  // a still picture: the Tough's tube on its carry line, no easing state
            try
            {
                foreach (var b in Bots)
                {
                    if (only != null && !only.Contains(b.key)) continue;
                    try { One(b, log); }
                    catch (Exception e) { log.Add($"FAIL {b.key}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
                }
            }
            finally { ToughAimEase.On = easeWas; }
            WriteIndex();
            string text = string.Join("\n", log) + "\n";
            string logPath = Path.Combine(OutDir, "robot_shots_log.txt");                // a full run starts the log afresh, a partial run adds to it
            if (only == null) File.WriteAllText(logPath, text); else File.AppendAllText(logPath, "\n" + text);
            return Say(text);
        }

        // ------------------------------------------------------------------------------------------------ one robot
        static void One(Bot b, List<string> log)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(b.path);
            if (!pf) { log.Add($"FAIL {b.key}: {b.path} missing"); return; }
            var scene = EditorSceneManager.NewPreviewScene();
            PlayableGraph graph = default;
            var temp = new List<Object>();
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, scene);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var an = go.GetComponentInChildren<Animator>();
                string pose = Pose(go, an, ref graph, temp);
                var pts = Points(go, an, temp);
                if (pts.Count == 0) { log.Add($"FAIL {b.key}: no visible mesh"); return; }
                var all = pts.Select(p => p.p).ToList();
                var bb = new Bounds(all[0], Vector3.zero); foreach (var p in all) bb.Encapsulate(p);
                var hips = an && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Hips) : null;
                var head = an && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Head) : null;
                float cutY = hips ? hips.position.y : bb.center.y;
                float chestOff = 0f;                                                   // the chest's front (shoulder line) against the root's front (-z)
                var la = an && an.isHuman ? an.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
                var ra = an && an.isHuman ? an.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
                if (la && ra)
                {
                    var cf = Vector3.Cross(ra.position - la.position, Vector3.up); cf.y = 0f;
                    if (cf.sqrMagnitude > 1e-6f) chestOff = Mathf.DeltaAngle(180f, Mathf.Atan2(cf.x, cf.z) * Mathf.Rad2Deg);
                }
                log.Add($"{b.key} ({b.name}): {pose}; chest front {chestOff:+0.0;-0.0} deg from the root's; mesh {bb.min.y:0.00}..{bb.max.y:0.00} m high, {bb.size.x:0.00} x {bb.size.z:0.00} m wide, head bone at {(head ? head.position.y : -1f):0.00} m, hips {cutY:0.00} m, {pts.Count(p => p.head)} head / {pts.Count(p => p.weapon)} weapon points");

                var camGo = new GameObject("OzV12RobotShotCam"); SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>(); cam.scene = scene; cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.nearClipPlane = 0.05f; cam.farClipPlane = 300f; cam.allowHDR = false; cam.allowMSAA = true; cam.aspect = 1f;
                var lamps = MakeLamps(scene);

                foreach (var s in ShotsFor(b))
                {
                    List<Vector3> sel = s.part == Part.All ? all
                                      : s.part == Part.Mid ? pts.Where(p => p.weapon || p.p.y >= cutY).Select(p => p.p).ToList()
                                      : pts.Where(p => p.head || p.weapon).Select(p => p.p).ToList();
                    if (sel.Count < 10) { log.Add($"   FAIL {s.file}: only {sel.Count} points to frame"); continue; }
                    float dist = Fit(cam, sel, chestOff + s.yaw, s.pitch, s.fov, s.margin, s.part != Part.All);
                    AimLamps(lamps, cam.transform.forward);
                    var px = Render(cam, Size, out float cover, out string edges, out float lum);
                    var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false, false); temp.Add(tex);
                    tex.SetPixels(px); tex.Apply(false);
                    File.WriteAllBytes(Path.Combine(OutDir, s.file), tex.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(OutDir, "thumbs", Path.ChangeExtension(s.file, ".jpg")), Thumb(tex, ThumbSize));
                    log.Add($"   {s.file}: {s.label}, camera {dist:0.00} m at yaw {s.yaw:+0;-0} from the chest / pitch {s.pitch:0} deg, fov {s.fov:0}; robot covers {cover * 100f:0.0} % of the picture, mean brightness {lum:0.00}; touches the edge: {edges}");
                }
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (var o in temp) if (o) Object.DestroyImmediate(o);
            }
        }

        /// <summary>The robot as the game draws it standing: red paint, the idle state at IdleSeconds, the weapon in the hands.</summary>
        static string Pose(GameObject go, Animator an, ref PlayableGraph graph, List<Object> temp)
        {
            var team = TeamColors.Shown(Team.Red);                                     // the sim team DRAWN red (the colour-swap option trades them)
            var look = go.GetComponent<OzHeroLook>(); if (look) look.Apply(team);      // the heroes' red material set
            var tp = go.GetComponent<TeamPaint>(); if (tp) tp.Team = team;
            if (!an || !an.runtimeAnimatorController) return "NO Animator: bind pose";
            an.cullingMode = AnimatorCullingMode.AlwaysAnimate;                         // nothing is drawn by the scene view: a culled Animator writes no pose
            graph = PlayableGraph.Create("OzV12RobotShots");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "pose", an);
            var acp = AnimatorControllerPlayable.Create(graph, an.runtimeAnimatorController);
            output.SetSourcePlayable(acp);
            graph.Play(); graph.Evaluate(0f);
            acp.Play("idle", 0, 0f); graph.Evaluate(0f);
            if (IdleSeconds > 0f) graph.Evaluate(IdleSeconds);
            graph.Evaluate(0f);
            var st = acp.GetCurrentAnimatorStateInfo(0);
            var clips = acp.GetCurrentAnimatorClipInfo(0);
            string clip = clips.Length > 0 && clips[0].clip ? clips[0].clip.name : "none";
            string sb = $"{an.runtimeAnimatorController.name} idle={(st.IsName("idle") ? "yes" : "NO")} clip {clip} @{st.normalizedTime * st.length:0.00} s";
            // the weapon in the hands: the game's own WeaponHold, one LateUpdate (the Tough's carry pitch from its BS01Stance)
            var hold = WeaponHold.Attach(go);
            var stance = go.GetComponent<BS01Stance>(); if (hold && stance) hold.carryPitch = stance.carryPitch;
            if (hold) Call(hold, "LateUpdate");
            sb += hold ? ", WeaponHold placed the weapon" : ", no WeaponHold";
            var all = go.GetComponentsInChildren<Transform>(true);
            Transform F(params string[] names) => all.FirstOrDefault(t => names.Contains(t.name));
            Transform grip = F("Grip", "Rifle_grip", "Bazooka_grip"), fore = F("Foregrip", "Rifle_forward_grip", "Bazooka_forward_grip"),
                      palm = F("RightHandWeapon"), fist = F("LeftHandGrip");
            if (grip && palm) sb += $" (grip {Vector3.Distance(grip.position, palm.position) * 100f:0.0} cm from the palm socket";
            if (fore && fist) sb += $", left fist {Vector3.Distance(fore.position, fist.position) * 100f:0.0} cm from the fore-grip";
            if (grip && palm) sb += ")";
            foreach (var c in go.GetComponentsInChildren<ElectrobotCoils>(true)) Call(c, "LateUpdate");
            foreach (var k in go.GetComponentsInChildren<BS01TypeKit>(true)) Call(k, "LateUpdate");
            int cables = 0;
            foreach (var c in go.GetComponentsInChildren<BS01KitCable>(true))
            {   // edit mode runs no OnEnable on a plain MonoBehaviour: make its mesh, then build it through the posed sockets
                Call(c, "OnEnable"); c.Rebuild(); cables++;
                var mf = c.GetComponent<MeshFilter>(); if (mf && mf.sharedMesh) temp.Add(mf.sharedMesh);
            }
            if (cables > 0) sb += $", {cables} kit hose(s) rebuilt";
            if (tp) tp.Apply();
            return sb;
        }

        static void Call(Object o, string method)
        {
            var mi = o.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi != null) mi.Invoke(o, null);
        }

        // ------------------------------------------------------------------------------------------------ framing
        struct Pt { public Vector3 p; public bool head, weapon; }

        static bool Visible(Renderer r) => r && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff
                                           && !(r is ParticleSystemRenderer) && !(r is LineRenderer) && !(r is TrailRenderer);

        static bool Under(Transform t, Transform root) { for (; t; t = t.parent) if (t == root) return true; return false; }

        /// <summary>World points of every visible mesh as posed (skinned meshes baked), each marked head (its main bone is the Head
        /// bone or below it) and weapon (under a RightHandWeapon socket).</summary>
        static List<Pt> Points(GameObject go, Animator an, List<Object> temp)
        {
            var pts = new List<Pt>();
            var head = an && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Head) : null;
            var sockets = go.GetComponentsInChildren<Transform>(true).Where(t => t.name == "RightHandWeapon").ToArray();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!Visible(r)) continue;
                bool w = sockets.Any(x => Under(r.transform, x));
                if (r is SkinnedMeshRenderer s)
                {
                    if (!s.sharedMesh) continue;
                    var m = new Mesh(); temp.Add(m); s.BakeMesh(m);
                    var v = m.vertices; var mtx = Matrix4x4.TRS(s.transform.position, s.transform.rotation, Vector3.one);
                    var bones = s.bones; var bw = s.sharedMesh.boneWeights;
                    var hb = new bool[bones.Length]; for (int i = 0; i < bones.Length; i++) hb[i] = head && bones[i] && Under(bones[i], head);
                    int step = Mathf.Max(1, v.Length / 40000);
                    for (int i = 0; i < v.Length; i += step)
                        pts.Add(new Pt { p = mtx.MultiplyPoint3x4(v[i]), weapon = w, head = bw.Length == v.Length && bw[i].boneIndex0 < hb.Length && hb[bw[i].boneIndex0] });
                }
                else if (r is MeshRenderer && r.TryGetComponent(out MeshFilter f) && f.sharedMesh)
                {
                    var v = f.sharedMesh.vertices; var mtx = r.transform.localToWorldMatrix; bool h = head && Under(r.transform, head);
                    int step = Mathf.Max(1, v.Length / 20000);
                    for (int i = 0; i < v.Length; i += step) pts.Add(new Pt { p = mtx.MultiplyPoint3x4(v[i]), weapon = w, head = h });
                }
            }
            return pts;
        }

        /// <summary>Puts the camera on the orbit (yaw from the robot's front, pitch up) and moves it so the points fill the square
        /// picture with the margin on the limiting side, centred. Returns the camera's distance from the framed centre.</summary>
        static float Fit(Camera cam, List<Vector3> pts, float yaw, float pitch, float fov, float margin, bool topAnchor)
        {
            cam.fieldOfView = fov; cam.aspect = 1f;
            var h = Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.back;             // unit models face -z
            var toCam = (h * Mathf.Cos(pitch * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(pitch * Mathf.Deg2Rad)).normalized;
            var rot = Quaternion.LookRotation(-toCam, Vector3.up);
            Vector3 R = rot * Vector3.right, U = rot * Vector3.up, F = rot * Vector3.forward;
            float tu = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * (1f - 2f * margin);
            var b = new Bounds(pts[0], Vector3.zero); foreach (var p in pts) b.Encapsulate(p);
            Vector3 T = b.center; float d = 0f;
            for (int it = 0; it < 8; it++)
            {
                d = Need(pts, T, R, U, F, tu);
                float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
                foreach (var p in pts)
                {
                    var v = p - T; float z = Vector3.Dot(v, F) + d; if (z < 0.01f) continue;
                    float x = Vector3.Dot(v, R) / z, y = Vector3.Dot(v, U) / z;
                    if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
                }
                T += (R * ((x0 + x1) * 0.5f) + U * ((y0 + y1) * 0.5f)) * d;
            }
            d = Need(pts, T, R, U, F, tu);
            if (topAnchor)
            {   // a wide subject leaves room above and below: the top of the subject goes to the top margin, the free room below
                float top = float.MinValue;
                foreach (var p in pts) { var v = p - T; float z = Vector3.Dot(v, F) + d; if (z > 0.01f) top = Mathf.Max(top, Vector3.Dot(v, U) / z); }
                if (top > float.MinValue && top < tu) T += U * ((top - tu) * d);
            }
            cam.transform.SetPositionAndRotation(T - F * d, rot);
            return d;
        }

        static float Need(List<Vector3> pts, Vector3 T, Vector3 R, Vector3 U, Vector3 F, float tu)
        {
            float d = 0f;
            foreach (var p in pts)
            {
                var v = p - T; float x = Mathf.Abs(Vector3.Dot(v, R)), y = Mathf.Abs(Vector3.Dot(v, U)), z = Vector3.Dot(v, F);
                d = Mathf.Max(d, x / tu - z, y / tu - z);
            }
            return d;
        }

        // ------------------------------------------------------------------------------------------------ studio light
        sealed class Lamp { public Light l; public Vector2 euler; }

        static List<Lamp> MakeLamps(Scene scene)
        {
            Lamp L(string n, Color c, float i, float pitch, float yaw, bool shadows)
            {
                var g = new GameObject("OzV12RobotShot" + n); SceneManager.MoveGameObjectToScene(g, scene);
                var li = g.AddComponent<Light>(); li.type = LightType.Directional; li.color = c; li.intensity = i;
                li.shadows = shadows ? LightShadows.Soft : LightShadows.None; li.shadowStrength = 0.75f;
                return new Lamp { l = li, euler = new Vector2(pitch, yaw) };
            }
            return new List<Lamp>
            {
                L("Key", new Color(1f, 0.96f, 0.90f), KeyI, 38f, 38f, KeyShadows),        // from the camera's left, above
                L("Fill", new Color(0.86f, 0.90f, 1f), FillI, 12f, -48f, false),          // soft, from the camera's right
                L("Rim", new Color(0.85f, 0.92f, 1f), RimI, 28f, 212f, false),            // from behind, right
                L("Rim2", new Color(1f, 0.95f, 0.88f), Rim2I, 32f, 148f, false),          // from behind, left
                L("Bounce", new Color(1f, 0.95f, 0.90f), BounceI, -28f, 0f, false),       // low, from the front: lifts the undersides
            };
        }

        static void AimLamps(List<Lamp> lamps, Vector3 camForward)
        {
            camForward.y = 0f; if (camForward.sqrMagnitude < 1e-6f) camForward = Vector3.forward;
            var baseRot = Quaternion.LookRotation(camForward.normalized, Vector3.up);
            foreach (var l in lamps) l.l.transform.rotation = baseRot * Quaternion.Euler(l.euler.x, l.euler.y, 0f);
        }

        // ------------------------------------------------------------------------------------------------ picture
        /// <summary>Renders twice (MSAA 8), once over black and once over white: the difference is the robot's matte (URP writes no
        /// alpha of its own), the black pass its colour; lays the robot over the dark gradient by that matte, and reports how much
        /// of the picture the robot covers, which picture edges it touches and its mean brightness.</summary>
        static Color[] Render(Camera cam, int size, out float cover, out string edges, out float lum)
        {
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
            Texture2D tx = null;
            try
            {
                Color[] Pass(Color clear)
                {
                    cam.backgroundColor = clear;
                    cam.targetTexture = rt; cam.Render(); cam.Render();               // second frame: textures and shadows resident
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    if (!tx) tx = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
                    tx.ReadPixels(new Rect(0, 0, size, size), 0, 0); tx.Apply(false);
                    RenderTexture.active = prev; cam.targetTexture = null;
                    return tx.GetPixels();
                }
                var px = Pass(Color.black); var wh = Pass(Color.white);
                int n = 0; double sum = 0; bool l = false, r = false, bt = false, tp = false;
                var rnd = new System.Random(7);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int i = y * size + x; Color c = px[i], w = wh[i];
                        float a = Mathf.Clamp01(1f - ((w.r - c.r) + (w.g - c.g) + (w.b - c.b)) / 3f);
                        if (a > 0.5f)
                        {
                            n++; sum += 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                            if (x == 0) l = true; if (x == size - 1) r = true; if (y == 0) bt = true; if (y == size - 1) tp = true;
                        }
                        float dx = (x - size * 0.5f) / size, dy = (y - size * 0.56f) / size, d = Mathf.Sqrt(dx * dx + dy * dy);
                        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - d / 0.72f));
                        var bg = Color.Lerp(BgEdge, BgCenter, k);
                        float dither = (float)(rnd.NextDouble() - 0.5) / 255f;
                        px[i] = new Color(c.r + (1f - a) * (bg.r + dither), c.g + (1f - a) * (bg.g + dither), c.b + (1f - a) * (bg.b + dither), 1f);
                    }
                cover = n / (float)(size * size); lum = n > 0 ? (float)(sum / n) : 0f;
                var e = new List<string>(); if (l) e.Add("left"); if (r) e.Add("right"); if (bt) e.Add("bottom"); if (tp) e.Add("top");
                edges = e.Count == 0 ? "none" : string.Join(", ", e);
                return px;
            }
            finally
            {
                cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt);
                if (tx) Object.DestroyImmediate(tx);
            }
        }

        static byte[] Thumb(Texture2D src, int size)
        {
            var a = RenderTexture.GetTemporary(src.width / 2, src.height / 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var b = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, a); Graphics.Blit(a, b);
            var prev = RenderTexture.active; RenderTexture.active = b;
            var t = new Texture2D(size, size, TextureFormat.RGB24, false, false); t.ReadPixels(new Rect(0, 0, size, size), 0, 0); t.Apply(false);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(a); RenderTexture.ReleaseTemporary(b);
            var jpg = t.EncodeToJPG(90); Object.DestroyImmediate(t);
            return jpg;
        }

        // ------------------------------------------------------------------------------------------------ the review page
        static void WriteIndex()
        {
            var sb = new StringBuilder();
            sb.Append(@"<!doctype html>
<html lang=""en""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>OpenZ Robot Shots</title>
<style>
:root{--bg:#0e0f12;--card:#17191e;--line:#2a2d35;--text:#e8e9ec;--muted:#9aa0ab}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:15px/1.45 system-ui,Segoe UI,Arial,sans-serif}
header{padding:20px 16px 4px;max-width:1500px;margin:0 auto}h1{font-size:22px;margin:0 0 4px}header p{margin:0;color:var(--muted)}
section{max-width:1500px;margin:0 auto;padding:14px 16px}h2{font-size:18px;margin:6px 0 10px;border-bottom:1px solid var(--line);padding-bottom:6px}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:14px}
figure{margin:0;background:var(--card);border:1px solid var(--line);border-radius:8px;overflow:hidden;cursor:zoom-in}
figure img{display:block;width:100%;aspect-ratio:1/1;object-fit:cover}figcaption{padding:8px 10px}
figcaption b{display:block}figcaption span{color:var(--muted);font-size:13px}
#lb{position:fixed;inset:0;background:rgba(0,0,0,.92);display:none;align-items:center;justify-content:center;flex-direction:column;z-index:9}
#lb.on{display:flex}#lb img{max-width:96vw;max-height:88vh;object-fit:contain}#lb div{margin-top:8px;color:var(--muted)}
</style></head><body>
<header><h1>OpenZ robot models</h1><p>Game prefabs in their idle pose, red team paint, studio light. 2048 x 2048 PNG. Click a picture to see it full size (arrow keys: next / previous, Esc: close).</p></header>
");
            int n = 0;
            foreach (var b in Bots)
            {
                var shots = ShotsFor(b).Where(s => File.Exists(Path.Combine(OutDir, s.file))).ToList();
                if (shots.Count == 0) continue;
                sb.Append($"<section><h2>{b.name}</h2><div class=\"grid\">\n");
                foreach (var s in shots)
                {
                    string thumb = "thumbs/" + Path.ChangeExtension(s.file, ".jpg");
                    if (!File.Exists(Path.Combine(OutDir, thumb))) thumb = s.file;
                    sb.Append($"<figure data-full=\"{s.file}\" data-cap=\"{b.name}: {s.label}\"><img src=\"{thumb}\" alt=\"{b.name} {s.label}\" loading=\"lazy\"><figcaption><b>{b.name}</b><span>{s.label} - {s.file}</span></figcaption></figure>\n");
                    n++;
                }
                sb.Append("</div></section>\n");
            }
            sb.Append(@"<div id=""lb""><img alt=""""><div></div></div>
<script>
const figs=[...document.querySelectorAll('figure')],lb=document.getElementById('lb'),im=lb.querySelector('img'),cap=lb.querySelector('div');let cur=-1;
function show(i){cur=(i+figs.length)%figs.length;const f=figs[cur];im.src=f.dataset.full;cap.textContent=f.dataset.cap+' ('+(cur+1)+' / '+figs.length+')';lb.classList.add('on');}
figs.forEach((f,i)=>f.addEventListener('click',()=>show(i)));
lb.addEventListener('click',()=>{lb.classList.remove('on');cur=-1;});
document.addEventListener('keydown',e=>{if(cur<0)return;if(e.key==='Escape'){lb.classList.remove('on');cur=-1;}else if(e.key==='ArrowRight')show(cur+1);else if(e.key==='ArrowLeft')show(cur-1);});
</script></body></html>
");
            File.WriteAllText(Path.Combine(OutDir, "index.html"), sb.ToString(), new UTF8Encoding(false));
        }

        static string Say(string s) { Debug.Log("[OzV12RobotShots] " + s); return s; }
    }
}
