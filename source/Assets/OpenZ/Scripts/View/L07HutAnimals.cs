// OpenZ (GPL-3.0) - version 1.3, item 12 part B: the animals of level 7's stone huts. View only: no rule reads this file.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// Level 7: the animals of the stone huts (Zod ahutanimal.cpp: on the volcanic planet a raptor, a mini raptor, a pig dino or a yellow
    /// worm; they come out of a hut, walk and look round near it and go home again). The ways are the jungle's hares (JungleHutAnimals),
    /// the models are posed by time as level 16's animals (L16AnimalKit.Rig):
    /// <list type="bullet">
    /// <item>Two animals per hut, by turns raptor + mini raptor and pig dino + worm: 6 of each on the 12 huts. The mini raptor is the raptor's
    /// model at 0.55 in its green coat (BUDGETS.md section 9). Now and then one comes out of the door (sooner while the camera looks at the
    /// hut), walks, looks round, stands, and after a while goes home.</item>
    /// <item>A walking robot within 7 m or a moving vehicle within 12 m, a shot passing or a blast: it looks up, then runs, home if home is
    /// not toward the threat, else away. The worm has no run take: it crawls at three times its pace.</item>
    /// <item>A blast within reach, a shot through it or a moving vehicle over it: the die take, a puff and bits, it lies there 5 s and sinks;
    /// a new one comes out of the same hut minutes later. A broken hut lets none out any more.</item>
    /// <item>Sounds through the event table, so every request is a line of the sound log: critter_l07_walk, _run, _worm_crawl, _raptor_look,
    /// _pig_look, _worm_look, _raptor_die, _pig_die, _worm_die. AudioDirector's level 7 branch calls amb_l07_hut_critters at the nearest
    /// standing hut (<see cref="LiveHutNear"/>).</item>
    /// </list>
    /// They walk on map ground and road the rules call free (lava is an impassable tile), never into a parked vehicle or a standing robot.
    /// View only: no sim object, no collider, never a target. The hidden ones have their renderers off and cost a few float sums; one off the
    /// screen is not posed. Own seeded random source for each. A model or picture that is not in the game is one clear warning, its animals
    /// stay out and the list is in <see cref="Missing"/> (the check menu fails on it); the level plays on. The two briefing sets stand in lava
    /// field 5, 60 m from the nearest hut, so no scene rule like the jungle's lounger set is needed.
    /// </summary>
    [DefaultExecutionOrder(76)]
    public sealed class L07HutAnimals : MonoBehaviour
    {
        public static L07HutAnimals I { get; private set; }
        public static bool AutoBoot = true;
        public const string Res = "OpenZ/Critters/";
        public bool playSounds = true;
        public int perHut = 2, seed = 1996;
        public float fleeRobot = 7f, fleeVehicle = 12f;
        [Tooltip("seconds an animal stays in (random in range); x 1/3 while the camera looks at its hut")] public Vector2 inside = new Vector2(10f, 35f);
        public Vector2 respawn = new Vector2(100f, 200f);
        /// <summary>Proof: animals out now, came out, went home, ran, looked round, killed.</summary>
        public static int Out, CameOut, WentHome, Ran, Looked, Killed;
        /// <summary>What this run could not load: one line each, empty when all is in.</summary>
        public static readonly List<string> Missing = new List<string>();

        public enum Kind { Raptor, MiniRaptor, PigDino, YellowWorm }
        const float Fps = 24f, LieFor = 5f, SinkFor = 1f;

        /// <summary>One kind of animal. Speeds in m/s at scale 1 and rate 1 are the takes' own (foot stride / stance time, clips.py), so the
        /// feet stay planted; frames as BUDGETS.md section 9.</summary>
        sealed class Spec
        {
            public string name, model, tex, head, tail, sndWalk, sndRun, sndLook, sndDie;
            public float scale, walk, run, walkRate = 1f, runRate = 1f, roam, pitch = 1f, smooth;
            /// <summary>Where in the walk and run loops a foot lands, as shares of the loop (rigkit.foot_track: a foot lands at the start of its stance;
            /// the leg offsets are in clips.py). The run clip is a patter of claws longer than a stride, so it goes once a loop, on the first foot.</summary>
            public float[] walkFeet, runFeet;
            public int wa, wb, ra, rb, la, lb, ia, ib, da, db;
        }
        static Spec Biped(string name, string tex, float scale, float rate, float pitch) => new Spec
        {
            name = name, model = "Raptor", tex = tex, head = "head", tail = "tail_2", scale = scale, walk = 0.9f, run = 4f, walkRate = rate, runRate = rate, roam = 10f, pitch = pitch,
            walkFeet = new[] { 0f, 0.5f }, runFeet = new[] { 0f }, smooth = 0.2f, sndWalk = "critter_l07_walk", sndRun = "critter_l07_run", sndLook = "critter_l07_raptor_look", sndDie = "critter_l07_raptor_die",
            wa = 1, wb = 25, ra = 31, rb = 43, la = 51, lb = 110, ia = 111, ib = 170, da = 181, db = 200
        };
        static readonly Spec[] Kinds =
        {
            Biped("raptor", "raptor_tex", 1f, 1f, 1f),
            Biped("mini raptor", "raptor_mini_tex", 0.55f, 1.3f, 1.35f),
            new Spec
            {
                name = "pig dino", model = "PigDino", tex = "pigdino_tex", head = "head", tail = "tail_2", scale = 1f, walk = 0.36f, run = 1.57f, roam = 8f, walkFeet = new[] { 0f, 0.25f, 0.5f, 0.75f }, runFeet = new[] { 0f }, smooth = 0.14f,
                sndWalk = "critter_l07_walk", sndRun = "critter_l07_run", sndLook = "critter_l07_pig_look", sndDie = "critter_l07_pig_die",
                wa = 1, wb = 25, ra = 31, rb = 43, la = 51, lb = 110, ia = 111, ib = 170, da = 181, db = 200
            },
            new Spec
            {
                name = "yellow worm", model = "YellowWorm", tex = "yellowworm_tex", head = "head", tail = "body_01", scale = 1f, walk = 0.22f, run = 0.22f, runRate = 3f, roam = 5f, walkFeet = new[] { 0f, 0.5f }, runFeet = new[] { 0f, 0.5f }, smooth = 0.3f,
                sndWalk = "critter_l07_worm_crawl", sndRun = "critter_l07_worm_crawl", sndLook = "critter_l07_worm_look", sndDie = "critter_l07_worm_die",
                wa = 1, wb = 49, ra = 1, rb = 49, la = 61, lb = 100, ia = 61, ib = 61, da = 111, db = 125
            },
        };

        ZGame game; Camera cam; RtsCamera rts; Transform root; DetailKit.Bits bits; float clock, unitsT; bool subscribed;
        readonly List<Vector3> unitPos = new List<Vector3>(); readonly List<bool> unitVeh = new List<bool>(); readonly List<Vector3> obs = new List<Vector3>();
        readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        Vector3 lastBlast; float lastBlastT = -99f; Vector3 shotA, shotB; float shotT = -99f;

        sealed class Hut { public int id, tx, ty; public Vector3 pos, door; public bool broken; }
        readonly List<Hut> huts = new List<Hut>();

        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => TryAttach(); }
            TryAttach();
        }

        public static void TryAttach()
        {
            if (!AutoBoot || !ZGame.I || !OzVolcanic.Is(ZGame.I.level) || FindFirstObjectByType<L07HutAnimals>()) return;
            new GameObject("L07HutAnimals").AddComponent<L07HutAnimals>();
        }

        void Awake() { I = this; }

        void Start()
        {
            game = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!game || game.World == null || !OzVolcanic.Is(game.level)) { enabled = false; return; }
            cam = Camera.main; rts = cam ? cam.GetComponent<RtsCamera>() : FindFirstObjectByType<RtsCamera>();
            root = new GameObject("L07HutAnimals_Root").transform; root.SetParent(transform, false);
            Out = CameOut = WentHome = Ran = Looked = Killed = 0; Missing.Clear();
            bits = new DetailKit.Bits(new[] { new Color(0.42f, 0.30f, 0.20f), new Color(0.32f, 0.44f, 0.16f), new Color(0.74f, 0.62f, 0.2f) });
            foreach (var o in game.World.Objects)
            {
                if (o.Kind != ZKind.MapItem || o.Type != "hut" || o.Destroyed) continue;
                var h = new Hut { id = o.Id, tx = o.LocX >> 4, ty = o.LocY >> 4 };
                h.pos = new Vector3(h.tx * 2f + 1f, 0f, -(h.ty * 2f + 1f)); h.pos.y = game.GroundY(h.pos.x, h.pos.z);
                h.door = DoorOf(h);
                huts.Add(h);
            }
            Missing.AddRange(FileFaults());
            foreach (var line in Missing) Debug.LogWarning("[OpenZ] L07HutAnimals MISSING: " + line + ": those animals stay out, check menu OpenZ/L07 Lava/Checks/Hut animal files fails");
            for (int i = 0; i < huts.Count; i++)
                for (int k = 0; k < perHut; k++)
                {
                    var kind = (Kind)((i % 2) * 2 + k % 2);                                                // hut 0 raptor + mini, hut 1 pig + worm, and on
                    var a = NewAnimal(i, kind, all.Count); if (a != null) all.Add(a);
                }
            FxManager.Exploded += OnExplosion; subscribed = true;
            Debug.Log($"[OpenZ] L07HutAnimals: {all.Count} animals in {huts.Count} huts, {Missing.Count} files missing");
        }

        void OnDestroy()
        {
            if (subscribed) FxManager.Exploded -= OnExplosion;
            foreach (var a in all) a.rig.Dispose();
            if (root) Destroy(root.gameObject);
            foreach (var m in mats.Values) if (m) Destroy(m);
            if (I == this) I = null;
        }

        /// <summary>The door side of a hut: the hut model has its door toward the game camera (-Z, the huts stand at yaw 0); where that
        /// tile is shut, the next free side.</summary>
        Vector3 DoorOf(Hut h)
        {
            foreach (var d in new[] { Vector3.back, Vector3.right, Vector3.left, Vector3.forward })
            {
                var p = h.pos + d * 1.7f;
                if (WalkTile(h, p.x, p.z)) { p.y = game.GroundY(p.x, p.z); return p; }
            }
            return h.pos + Vector3.back * 1.7f;
        }

        static float Rf(System.Random g, float a, float b) => a + (float)g.NextDouble() * (b - a);
        static float Flat(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return dx * dx + dz * dz; }
        float Ground(float x, float z) => game.GroundY(x, z);
        bool Visible(Vector3 p) => DetailKit.OnScreen(cam, p, 0.1f) && (!rts || rts.height < 140f);

        // ------------------------------------------------------------------ files
        /// <summary>The files the animals need, checked by loading them: one line per fault, none when all is in. Also called by the check
        /// menu outside Play.</summary>
        public static List<string> FileFaults()
        {
            var bad = new List<string>(); var seen = new HashSet<string>();
            foreach (var s in Kinds)
            {
                if (!Resources.Load<Texture2D>(Res + s.tex)) bad.Add("picture Resources/" + Res + s.tex);
                if (!seen.Add(s.model)) continue;
                var prefab = Resources.Load<GameObject>(Res + s.model);
                if (!prefab) { bad.Add("model Resources/" + Res + s.model + ".fbx"); continue; }
                if (!L16AnimalKit.Find(prefab.transform, s.head)) bad.Add(s.model + ": bone " + s.head);
                if (!L16AnimalKit.Find(prefab.transform, s.tail)) bad.Add(s.model + ": bone " + s.tail);
                var clip = TakeOf(s.model);
                if (!clip) bad.Add(s.model + ": no take");
                else if (clip.length < (s.db - 1) / Fps - 0.05f) bad.Add(s.model + ": take " + clip.length.ToString("F2") + " s, needs " + ((s.db - 1) / Fps).ToString("F2") + " s");
            }
            return bad;
        }

        static AnimationClip TakeOf(string model)
        {
            foreach (var c in Resources.LoadAll<AnimationClip>(Res + model)) if (c && !c.name.StartsWith("__preview__")) return c;
            return null;
        }

        Material MatOf(Spec s)
        {
            if (mats.TryGetValue(s.tex, out var m)) return m;
            m = new Material(DetailKit.Lit) { name = "L07Animal_" + s.tex, hideFlags = HideFlags.DontSave };
            var tex = Resources.Load<Texture2D>(Res + s.tex);
            if (tex) { if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); else m.mainTexture = tex; }
            DetailKit.F(m, "_UseVertexColor", 0f); DetailKit.F(m, "_TeamMode", 0f); DetailKit.F(m, "_Degreen", 0f); DetailKit.F(m, "_Stylize", 1f); DetailKit.F(m, "_Degrime", 0f); DetailKit.F(m, "_FlatShade", 0f);
            DetailKit.F(m, "_SoftFill", 0.2f); DetailKit.F(m, "_AmbientBoost", 1f); DetailKit.F(m, "_Smoothness", s.smooth); DetailKit.F(m, "_Metallic", 0f); DetailKit.F(m, "_RimPower", 3.4f); DetailKit.F(m, "_NightClass", 0f);
            DetailKit.C(m, "_KeyWarm", new Color(1.06f, 1f, 0.92f, 1f)); DetailKit.C(m, "_RimColor", new Color(0.5f, 0.46f, 0.38f, 1f));
            DetailKit.C(m, "_BaseColor", Color.white); DetailKit.C(m, "_EmissionColor", Color.black);
            mats[s.tex] = m;
            return m;
        }

        // ------------------------------------------------------------------ a model posed by time (L16AnimalKit.Rig with one take)
        sealed class Rig
        {
            public Transform root, orient, model; public Renderer[] rends; public float len, shift;
            PlayableGraph graph; AnimationMixerPlayable mix; AnimationClipPlayable a, b; bool shown = true;

            public static Rig Make(Spec s, string name, Transform parent, Material mat)
            {
                var prefab = Resources.Load<GameObject>(Res + s.model); var clip = TakeOf(s.model);
                if (!prefab || !clip) return null;
                var r = new Rig { root = new GameObject(name).transform, len = clip.length };
                r.root.SetParent(parent, false);
                // the turn that makes a file face +Z lives on its own node: the take keys the model's own root
                r.orient = new GameObject("Orient").transform; r.orient.SetParent(r.root, false);
                var go = Instantiate(prefab, r.orient, false); go.name = "Model"; r.model = go.transform;
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
                r.rends = go.GetComponentsInChildren<Renderer>(true);
                foreach (var rd in r.rends)
                {
                    var arr = new Material[Mathf.Max(1, rd.sharedMaterials.Length)];
                    for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                    rd.sharedMaterials = arr; rd.receiveShadows = true; rd.shadowCastingMode = ShadowCastingMode.On;
                    if (rd is SkinnedMeshRenderer sk) sk.updateWhenOffscreen = true;
                }
                var head = L16AnimalKit.Find(r.model, s.head); var tail = L16AnimalKit.Find(r.model, s.tail);
                if (head && tail)
                {   // whatever the axis import did: head ahead of the tail along +Z, to the nearest quarter turn
                    var d = r.root.InverseTransformPoint(head.position) - r.root.InverseTransformPoint(tail.position);
                    float turn = Mathf.Round(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg / 90f) * 90f;
                    r.orient.localRotation = Quaternion.Euler(0f, -turn, 0f);
                }
                r.shift = Mathf.Max(0f, Mathf.Round((clip.length - (s.db - 1) / Fps) * Fps));                 // the take starts a frame early on some imports (AmbientLife.MakeRig)
                var an = go.GetComponent<Animator>(); if (!an) an = go.AddComponent<Animator>();
                an.applyRootMotion = false; an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                r.graph = PlayableGraph.Create("OpenZ." + name);
                r.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(r.graph, "L07Animal", an);
                r.mix = AnimationMixerPlayable.Create(r.graph, 2);
                r.a = AnimationClipPlayable.Create(r.graph, clip); r.b = AnimationClipPlayable.Create(r.graph, clip);
                r.a.SetApplyFootIK(false); r.b.SetApplyFootIK(false); r.a.SetSpeed(0.0); r.b.SetSpeed(0.0);
                r.graph.Connect(r.a, 0, r.mix, 0); r.graph.Connect(r.b, 0, r.mix, 1);
                r.mix.SetInputWeight(0, 1f); r.mix.SetInputWeight(1, 0f);
                output.SetSourcePlayable(r.mix);
                r.graph.Play();
                return r;
            }

            public void Show(bool on) { if (on == shown) return; shown = on; for (int i = 0; i < rends.Length; i++) if (rends[i]) rends[i].enabled = on; }

            /// <summary>The take at time t, mixed with the moment it came from (share wFrom): state changes blend over 0.18 s.</summary>
            public void Pose(float t, float tFrom, float wFrom)
            {
                a.SetTime(Mathf.Clamp(t, 0f, len)); b.SetTime(Mathf.Clamp(tFrom, 0f, len));
                mix.SetInputWeight(0, 1f - wFrom); mix.SetInputWeight(1, wFrom);
                graph.Evaluate(0f);
            }

            public void Dispose() { if (graph.IsValid()) graph.Destroy(); }
        }

        // ------------------------------------------------------------------ an animal
        enum St { Hidden, Walk, Look, Idle, Alert, Run, Die, Gone }
        sealed class Animal
        {
            public int hut, idx, leg; public Kind kind; public Spec s; public System.Random rng; public St st; public Rig rig;
            public float t, dur, animT, fromT, lastT, fade, yaw, drawYaw, pitch;
            public Vector3 pos, goal, threat;
        }
        // leg: 0 free, 1 out of the door, 2 on the way to the door, 3 in through the door
        readonly List<Animal> all = new List<Animal>();

        Animal NewAnimal(int hut, Kind kind, int idx)
        {
            var s = Kinds[(int)kind];
            var rig = Rig.Make(s, s.model + idx, root, MatOf(s));
            if (rig == null) return null;
            var a = new Animal { hut = hut, idx = idx, kind = kind, s = s, rig = rig, rng = new System.Random(seed * 41 + idx * 7919 + 17), st = St.Hidden, pos = huts[hut].pos };
            a.t = 2f + (float)a.rng.NextDouble() * 20f;
            rig.root.localScale = Vector3.one * s.scale; rig.Show(false);
            return a;
        }

        /// <summary>Walkable ground: a ground or road tile the rules call free for robots, or the hut's own tile.</summary>
        bool WalkTile(Hut h, float x, float z)
        {
            int tx = Mathf.FloorToInt(x / 2f), ty = Mathf.FloorToInt(-z / 2f);
            if (tx == h.tx && ty == h.ty) return true;
            var map = game.World.Map;
            if (tx < 0 || ty < 0 || tx >= map.width || ty >= map.height) return false;
            var c = map.ClassAt(tx, ty);
            return (c == ZClass.Ground || c == ZClass.Road) && !map.LavaAt(tx, ty) && game.World.Path.TilePassable(tx, ty, true);
        }

        bool Walk(Animal a, Hut h, float x, float z)
        {
            if (!WalkTile(h, x, z)) return false;
            float reach = a.s.roam + 6f;
            if (Flat(new Vector3(x, 0f, z), h.pos) > reach * reach) return false;
            for (int i = 0; i < obs.Count; i++) if (Flat(obs[i], new Vector3(x, 0f, z)) < 1.8f * 1.8f) return false;
            return true;
        }

        bool WalkLine(Animal a, Hut h, Vector3 from, Vector3 to)
        {
            var d = to - from; d.y = 0f; int n = Mathf.Max(1, Mathf.CeilToInt(d.magnitude / 0.5f));
            for (int k = 1; k <= n; k++) { var p = from + d * (k / (float)n); if (!Walk(a, h, p.x, p.z)) return false; }
            return true;
        }

        bool HutGone(Hut h)
        {
            if (h.broken) return true;
            var o = game.World.Get(h.id);
            if (o == null || o.Destroyed) h.broken = true;
            return h.broken;
        }

        bool ThreatNear(Vector3 p, out Vector3 at)
        {
            at = Vector3.zero; float best = float.MaxValue; bool hit = false;
            for (int i = 0; i < unitPos.Count; i++)
            {
                float d = Flat(unitPos[i], p), r = unitVeh[i] ? fleeVehicle : fleeRobot;
                if (d < r * r && d < best) { best = d; at = unitPos[i]; hit = true; }
            }
            return hit;
        }

        void Set(Animal a, St st, float dur = 0f)
        {
            a.fromT = a.lastT; a.fade = 1f; a.animT = 0f; a.t = 0f; a.dur = dur; a.st = st;
        }

        void ComeOut(Animal a, Hut h)
        {
            a.pos = h.pos; a.leg = 1; a.goal = h.door; a.yaw = a.drawYaw = Mathf.Atan2(h.door.x - h.pos.x, h.door.z - h.pos.z) * Mathf.Rad2Deg;
            a.rig.Show(true); CameOut++; Set(a, St.Walk);
        }

        void Hide(Animal a) { a.st = St.Hidden; a.leg = 0; a.t = Rf(a.rng, inside.x, inside.y); a.rig.Show(false); }

        void Wander(Animal a, Hut h)
        {
            a.leg = 0;
            for (int k = 0; k < 12; k++)
            {
                float ang = Rf(a.rng, 0f, Mathf.PI * 2f), d = Rf(a.rng, 2f, a.s.roam);
                var g = h.pos + new Vector3(Mathf.Cos(ang) * d, 0f, Mathf.Sin(ang) * d);
                if (WalkLine(a, h, a.pos, g)) { a.goal = g; Set(a, St.Walk); return; }
            }
            Set(a, St.Idle, Rf(a.rng, 2f, 6f));
        }

        void GoHome(Animal a, St how) { a.leg = 2; a.goal = huts[a.hut].door; Set(a, how); }

        void Next(Animal a, Hut h)
        {
            double r = a.rng.NextDouble();
            if (r < 0.2 && !HutGone(h)) GoHome(a, St.Walk);
            else if (r < 0.55) StartLook(a);
            else if (r < 0.75) Set(a, St.Idle, Rf(a.rng, 2f, 6f));
            else Wander(a, h);
        }

        void StartLook(Animal a)
        {
            Set(a, St.Look, (a.s.lb - a.s.la) / Fps); Looked++;
            if (Visible(a.pos)) Snd(a, a.s.sndLook, 1f);
        }

        void StartRun(Animal a, Hut h)
        {
            var away = a.pos - a.threat; away.y = 0f; away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
            var toDoor = h.door - a.pos; toDoor.y = 0f; Ran++;
            if (!HutGone(h) && toDoor.magnitude < 14f && (toDoor.magnitude < 1f || Vector3.Dot(toDoor.normalized, away) > -0.2f)) { GoHome(a, St.Run); return; }
            a.leg = 0;
            for (int k = 0; k < 7; k++)
            {
                var g = a.pos + Quaternion.Euler(0f, (k % 2 == 1 ? 1 : -1) * ((k + 1) / 2) * 30f, 0f) * away * Mathf.Min(9f, a.s.roam);
                if (WalkLine(a, h, a.pos, g)) { a.goal = g; Set(a, St.Run); return; }
            }
            Set(a, St.Idle, Rf(a.rng, 2f, 6f));
        }

        /// <summary>One step toward the goal. True on arrival, or when the way is shut.</summary>
        bool Move(Animal a, Hut h, float speed, float dt)
        {
            var to = a.goal - a.pos; to.y = 0f; float d = to.magnitude;
            if (d < 0.12f) return true;
            var dir = to / d; var p = a.pos + dir * Mathf.Min(speed * dt, d);
            if (a.leg == 0 && !Walk(a, h, p.x, p.z)) return true;
            p.y = Ground(p.x, p.z); a.pos = p; a.yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            return false;
        }

        void Arrived(Animal a, Hut h)
        {
            switch (a.leg)
            {
                case 1: Wander(a, h); return;
                case 2: if (HutGone(h)) { a.leg = 0; Next(a, h); } else { a.leg = 3; a.goal = h.pos; } return;
                case 3: Hide(a); WentHome++; return;
            }
            if (a.st == St.Run) Set(a, St.Idle, Rf(a.rng, 2f, 6f)); else Next(a, h);
        }

        void Step(Animal a, float dt)
        {
            var h = huts[a.hut]; var s = a.s;
            a.animT += dt; a.fade = Mathf.Max(0f, a.fade - dt / 0.18f);
            switch (a.st)
            {
                case St.Gone: a.t -= dt; if (a.t <= 0f) { a.st = St.Hidden; a.t = 1f; } return;
                case St.Hidden:
                    a.t -= dt * (Visible(h.pos) ? 3f : 1f);                                                // sooner while the camera looks at the hut
                    if (a.t > 0f) return;
                    if (HutGone(h)) { a.t = 9999f; return; }
                    if (ThreatNear(h.pos, out _) || clock - lastBlastT < 8f && Flat(lastBlast, h.pos) < 22f * 22f) { a.t = 4f + Rf(a.rng, 0f, 6f); return; }
                    ComeOut(a, h);
                    return;
                case St.Die:
                    if (a.animT >= LieFor + SinkFor) { a.rig.Show(false); a.st = St.Gone; a.t = Rf(a.rng, respawn.x, respawn.y); }
                    return;
            }
            if (a.leg != 1 && a.leg != 3 && (a.st == St.Walk || a.st == St.Look || a.st == St.Idle))
            {
                bool byUnit = ThreatNear(a.pos, out var th);
                if (byUnit || (clock - lastBlastT < 0.3f && Flat(lastBlast, a.pos) < 16f * 16f) || (clock - shotT < 0.3f && DetailKit.DistPointSegment(a.pos + Vector3.up * 0.3f, shotA, shotB) < 3.5f))
                {
                    if (!byUnit) th = clock - lastBlastT < 0.3f ? lastBlast : shotA;
                    a.threat = th; a.leg = 0; Set(a, St.Alert, Rf(a.rng, 0.25f, 0.6f)); Looked++;
                    if (Visible(a.pos)) Snd(a, s.sndLook, 1f);
                }
            }
            switch (a.st)
            {
                case St.Look:
                case St.Idle:
                    a.t += dt; if (a.t >= a.dur) Next(a, h);
                    break;
                case St.Alert:
                    a.t += dt;
                    { var d = a.threat - a.pos; d.y = 0f; if (d.sqrMagnitude > 0.01f) a.yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
                    if (a.t >= a.dur) StartRun(a, h);
                    break;
                case St.Walk:
                case St.Run:
                    bool run = a.st == St.Run;
                    float rate = run ? s.runRate : s.walkRate, loop = (run ? s.rb - s.ra : s.wb - s.wa) / Fps;
                    bool foot = FootDown(run ? s.runFeet : s.walkFeet, a.animT > dt ? (a.animT - dt) * rate : -0.001f, a.animT * rate, loop);   // on the gait's first frame its first foot lands
                    if (Move(a, h, (run ? s.run * s.runRate : s.walk * s.walkRate) * s.scale, dt)) Arrived(a, h);
                    if (foot && a.st != St.Hidden && Visible(a.pos)) Snd(a, run ? s.sndRun : s.sndWalk, 1f);
                    break;
            }
        }

        /// <summary>A foot lands between two moments of a loop of this length: the same clock as <see cref="Pose"/>, so the sound stays on the foot at any rate.</summary>
        static bool FootDown(float[] feet, float t0, float t1, float loop)
        {
            for (int i = 0; i < feet.Length; i++) if (Mathf.Floor(t1 / loop - feet[i]) > Mathf.Floor(t0 / loop - feet[i])) return true;
            return false;
        }

        static float Loop(Rig r, int first, int last, float t) => (first - 1 + r.shift) / Fps + Mathf.Repeat(t, (last - first) / Fps);
        static float Once(Rig r, int first, int last, float t) => (first - 1 + r.shift) / Fps + Mathf.Clamp(t, 0f, (last - first) / Fps);

        void Pose(Animal a, float dt)
        {
            if (a.st == St.Hidden || a.st == St.Gone) return;
            var s = a.s; var r = a.rig; float k = a.animT;
            a.drawYaw = Mathf.MoveTowardsAngle(a.drawYaw, a.yaw, (a.st == St.Run ? 540f : 280f) * dt);
            float near = Mathf.Sqrt(Flat(a.pos, huts[a.hut].pos));                                         // through the low door: small at the hut, full size out of it
            float size = s.scale * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01((near - 0.4f) / 1.3f));
            float sink = a.st == St.Die ? Mathf.Clamp01((k - LieFor) / SinkFor) * 0.6f * s.scale : 0f;
            sink -= Mathf.Clamp01((1.25f - near) / 0.6f) * 0.24f;                                         // nests: up on the mound
            if (a.leg == 1 || a.leg == 3) sink += (1f - Mathf.Clamp01(near / 0.46f)) * 0.75f * s.scale;   // nests: out of the pit at the centre
            r.root.SetPositionAndRotation(a.pos + Vector3.down * sink, Quaternion.Euler(a.pitch, a.drawYaw, 0f));
            r.root.localScale = Vector3.one * size;
            if (!Visible(a.pos)) return;
            var f = Quaternion.Euler(0f, a.drawYaw, 0f) * Vector3.forward * 0.8f * s.scale;             // the body on the slope
            float slope = Mathf.Atan2(Ground(a.pos.x - f.x, a.pos.z - f.z) - Ground(a.pos.x + f.x, a.pos.z + f.z), 1.6f * s.scale) * Mathf.Rad2Deg;
            a.pitch = Mathf.MoveTowards(a.pitch, Mathf.Clamp(slope, -18f, 18f), 60f * dt);
            float t;
            switch (a.st)
            {
                case St.Walk: t = Loop(r, s.wa, s.wb, k * s.walkRate); break;
                case St.Run: t = Loop(r, s.ra, s.rb, k * s.runRate); break;
                case St.Idle: t = s.ia == s.ib ? Once(r, s.ia, s.ib, 0f) : Loop(r, s.ia, s.ib, k); break;
                case St.Die: t = Once(r, s.da, s.db, k); break;
                default: t = Once(r, s.la, s.lb, k); break;
            }
            a.lastT = t;
            r.Pose(t, a.fromT, a.fade);
        }

        void Snd(Animal a, string id, float vol)
        {
            if (!playSounds) return;
            if (AudioDirector.Has(id)) AudioDirector.Fire(id, a.pos, a.idx, vol, a.s.pitch);
            else OzSoundLog.Add("L07", id, null, "DROP event or clips missing", vol, a.pos);
        }

        void Kill(Animal a)
        {
            if (a.st == St.Die || a.st == St.Gone || a.st == St.Hidden) return;
            var p = a.pos + Vector3.up * 0.25f * a.s.scale;
            var fx = OpenZFxPlayer.I; if (fx) fx.Dust(p, 0.4f * a.s.scale); else FxManager.I?.Dust(p, 0.4f * a.s.scale);
            byte col = (byte)(a.kind == Kind.MiniRaptor ? 1 : a.kind == Kind.YellowWorm ? 2 : 0);
            bits.Burst(p, 10, col, 1.8f, 0.05f * Mathf.Max(a.s.scale, 0.8f), a.rng);
            Snd(a, a.s.sndDie, 1f);
            a.leg = 0; Set(a, St.Die); Killed++;
        }

        void OnExplosion(Vector3 at, float strength)
        {
            lastBlast = at; lastBlastT = clock;
            float reach = 1.2f + 2.4f * strength + 0.3f;
            foreach (var a in all) if (Flat(a.pos, at) < reach * reach) Kill(a);
        }

        void RefreshUnits()
        {
            unitPos.Clear(); unitVeh.Clear(); obs.Clear();
            foreach (var kv in game.LiveViews)
            {
                var v = kv.Value; if (!v || v.Dying || !v.gameObject.activeInHierarchy || v.Obj == null) continue;
                var o = v.Obj; if (o.Kind != ZKind.Robot && o.Kind != ZKind.Vehicle && o.Kind != ZKind.Cannon) continue;
                var p = v.transform.position;
                if (o.IsMoving && o.Kind != ZKind.Cannon) { unitPos.Add(p); unitVeh.Add(o.Kind == ZKind.Vehicle); }
                else obs.Add(p);
            }
            for (int i = 0; i < unitPos.Count; i++)                                                    // a moving vehicle rolling over an animal
            {
                if (!unitVeh[i]) continue;
                foreach (var a in all) if (a.leg != 1 && a.leg != 3 && Flat(a.pos, unitPos[i]) < 1.1f * 1.1f) Kill(a);
            }
        }

        void ReadShots()
        {
            var ev = game.World.Events;
            for (int i = 0; i < ev.Count; i++)
            {
                var e = ev[i];
                if (e.type != ZEventType.Hitscan || !DetailKit.ShotSegment(game, e, out var p, out var q)) continue;
                shotA = p; shotB = q; shotT = clock;
                foreach (var a in all) if (DetailKit.DistPointSegment(a.pos + Vector3.up * 0.4f * a.s.scale, p, q) < 0.35f * a.s.scale) Kill(a);
            }
        }

        void Update()
        {
            if (!game || game.World == null) return;
            if (!cam) { cam = Camera.main; if (cam && !rts) rts = cam.GetComponent<RtsCamera>(); }
            float dt = game.paused ? 0f : Mathf.Min(Time.deltaTime, 0.05f) * Mathf.Clamp(game.gameSpeed, 0.25f, 2f);
            clock += dt;
            unitsT -= dt; if (unitsT <= 0f) { unitsT = 0.2f; RefreshUnits(); }
            ReadShots();                                                                               // before CombatView (LateUpdate) clears the events
            if (dt > 0f) foreach (var a in all) Step(a, dt);
            int n = 0;
            foreach (var a in all) { Pose(a, dt); if (a.st != St.Hidden && a.st != St.Gone) n++; }
            Out = n;
            bits.StepAndDraw(dt, game);
        }

        /// <summary>AudioDirector's level 7 branch: the standing hut nearest to a place, if it is closer than the square root of sqrReach.</summary>
        public static bool LiveHutNear(Vector3 p, float sqrReach, out Vector3 at)
        {
            at = Vector3.zero;
            if (!I) return false;
            float best = sqrReach; bool hit = false;
            foreach (var h in I.huts)
            {
                float d = (h.pos - p).sqrMagnitude;
                if (d < best && !I.HutGone(h)) { best = d; at = h.pos; hit = true; }
            }
            return hit;
        }

        // ------------------------------------------------------------------ checks (OzL07HutAnimalsCheck)
        public int Animals => all.Count;
        public int Huts => huts.Count;
        public int OfKind(Kind k) { int n = 0; foreach (var a in all) if (a.kind == k) n++; return n; }
        public Transform RootNode => root;
        public int InBrokenHuts { get { int n = 0; foreach (var a in all) if ((a.st == St.Hidden || a.st == St.Gone) && HutGone(huts[a.hut])) n++; return n; } }

        /// <summary>Check: every hidden animal steps out now, threat or not.</summary>
        public void ProofAllOut() { foreach (var a in all) if (a.st == St.Hidden && !HutGone(huts[a.hut])) ComeOut(a, huts[a.hut]); }

        Animal First(Kind k) { foreach (var a in all) if (a.kind == k && a.st != St.Gone && a.st != St.Die) return a; return null; }

        /// <summary>Check: the hut of the first animal of a kind (the camera goes there).</summary>
        public bool ProofPlace(Kind k, out Vector3 at) { var a = First(k); at = a != null ? huts[a.hut].pos : Vector3.zero; return a != null; }

        /// <summary>Check: the first animal of a kind does one thing now: 0 walks, 1 looks round, 2 runs, 3 dies.</summary>
        public bool ProofDo(Kind k, int what)
        {
            var a = First(k); if (a == null) return false;
            var h = huts[a.hut];
            if (a.st == St.Hidden) ComeOut(a, h);
            switch (what)
            {
                case 0: Wander(a, h); break;
                case 1: a.leg = 0; StartLook(a); break;
                case 2: a.leg = 0; a.threat = a.pos + Vector3.forward * 3f; StartRun(a, h); break;
                default: Kill(a); break;
            }
            return true;
        }

        public static string Status()
        {
            if (!I) return "L07HutAnimals not running (not level 7)";
            return $"[L07HutAnimals] {I.all.Count} animals in {I.huts.Count} huts: {Out} out now; came out {CameOut}, went home {WentHome}, looked {Looked}, ran {Ran}, killed {Killed}; missing {Missing.Count}";
        }
    }
}
