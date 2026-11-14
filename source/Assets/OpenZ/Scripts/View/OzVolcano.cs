// OpenZ (GPL-3.0)
// Look and sound only: nothing here writes sim state or uses the sim's random,
// nothing lands where units walk, nothing hurts anyone. It exists only on a map with lava: OzLava.Lv4Boot adds it.
//
// Which: Resources/OpenZ/Lava/L07_eruptors.json (tools/make_eruptors.py, from the terrain lane's placing): the small brown cones in the lakes, volcano_4_z on the top
// band, two spatter vents on Z's hot tiles. Each row finds its model under L07_Rim by name and place at boot; with no model (L07_Lab) it still erupts from its point.
//
// How one big eruption is built, layer by layer, at the 70 m game camera (about 20 px a metre):
//   rumble    2.5 s: crater heat eases up (property block on _EmissionMapColor where the emission map has a crater, a soft halo in every mouth), smoke thickens and darkens, grit rolls off the rim, the lake bubbles x 3 round the cone
//             (OzLava.PopBurst), low rumble. Nothing flashes, so the eye is led to the cone before it goes.
//   burst     fireballs on the game's own heat-ramp fireball channel (m12_fire, as every blast: dark rim over the ground, additive heart) and one blast_core flash, so it has
//             the same quality as the game's explosions; a ring of ash thrown off the rim; the crater light's peak; a short bloom push (Sustain, never Flash).
//             surges, so the mouth burns with real flame shapes and not with round sprites. It reads by day because the shader has a body; it blooms at night.
//   fountain  lava_fountain: comet streaks under true gravity: inner jet (thick, fast, fades at the top) and outer clots (thin, full arcs, fall back cooling yellow, orange, deep
//             red, dark). Opaqueness 0.6, so a cooled clot still darkens what is behind it by day; streak length follows speed, so a clot is a round blob at the top of its arc.
//             3-5 eased surges, never constant (OzVolcanoSurges).
//   bombs     rocks on exact arcs, script stepped, DrawMeshInstanced (OpenZ/LavaBomb: dark crust, hot cracks by temperature, the one ramp), smoke trail at most 5 puffs a second.
//             Landing spots: map lava tiles at least 4 m from every unit at launch, band tiles for the band volcano only. In lava: LV3's LavaHit. On the band: a chunk cools 6-10 s.
//   ash       lava_ash: at most 40 puffs at once in 2 layers on the game's smoke flipbook (coarse billow look): a tight dark core and a paler outer sleeve, born orange-hot (heat
//             glow while young = low in the column), lit by the crater light, drifting on a slow wind, ending at 18 m and under camera height - 15 m. It is also the dark
//             backdrop that makes the clots read by day: it is drawn first.
//   flow      OpenZ/VolcanoFlow, a second renderer per LOD over the same mesh, on only during an eruption: a bright front runs down the gullies by the Blender flow time map at
//             about 1.5 m/s, then cools to a crust skin behind itself over 25 s.
//   light     a Light from a pool of 2 (range 18-26 m, x 1.5 at night), following the surges, and _OZLavaFlash, because the lake takes no point light.
//   tail      15-25 s: embers, falling ash motes, crater heat eases back, smoke thins.
// A spit is the same machine at about a quarter, no ash column, a third of the way down the side, one or two small rocks.
// CALM SCREEN: no bloom push, no core flash, slow light. LIGHTEST: half the particles, half the bombs. Paused: its clocks stop.
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public partial class OzLava
    {
        partial void Lv4Boot()
        {
            if (OzLavaFx.Off) return;
            gameObject.AddComponent<OzVolcano>().Boot(this, game, map);
            gameObject.AddComponent<OzLavaPrewarm>();
        }
        partial void Lv4Tick(float dt, float t) { var v = OzVolcano.I; if (v) v.Tick(dt); }
        partial void Lv4Down() { var v = OzVolcano.I; if (v) v.Down(); }
    }

    [Serializable] public sealed class OzVolcanoRow { public string id, node, kit; public int kind; public float x, y, z, yaw, scale, cx, cy, cz, rad, run; public bool smoke, canBig; }
    [Serializable] public sealed class OzVolcanoList { public OzVolcanoRow[] rows; }

    [DefaultExecutionOrder(62)]
    public sealed class OzVolcano : MonoBehaviour
    {
        public static OzVolcano I { get; private set; }
        public const string ListRes = "OpenZ/Lava/L07_eruptors", FlowRes = "OpenZ/Lava/M_L07_Flow", BombRes = "OpenZ/Lava/M_L07_Bomb", VolFlowRes = "OpenZ/Lava/T_L07_VolFlow", DecorFlowRes = "OpenZ/Lava/T_L07_DecorFlow";
        public const string SndRumble = "volcano_rumble", SndBurst = "volcano_burst", SndFountain = "volcano_fountain", SndWhoosh = "lava_bomb_whoosh", SndLand = "lava_bomb_land", SndLandRock = "lava_bomb_land_rock", SndVent = "volcano_vent_idle";

        // ---- tunables (set in Play, then here): all the look numbers in one place
        public static float BigHeightMin = 13f, BigHeightMax = 19f, SpitHeightMin = 2.6f, SpitHeightMax = 4.4f;        // fountain top over the crater, m
        public static float BigRateMin = 220f, BigRateMax = 420f, SpitRateMin = 18f, SpitRateMax = 38f;                 // clots a second, at surge 0 and 1
        public static float AshCeiling = 18f, CameraClear = 15f, AshRate = 10f;                                         // m over the crater; m under the camera; puffs a second (rise about 4.5 s: at most 40 alive)
        public static float LightBig = 90f, LightSpit = 7f, LightNight = 1.5f, RangeBigMin = 18f, RangeBigMax = 26f, RangeSpit = 12f;
        public static float CraterGain = 3.2f, FrontSpeed = 1.5f, CoolSeconds = 25f, SpitReach = 0.34f, BloomPush = 1.1f, BigShake = 0.55f;
        public const float BombClear = 4f, UnitsClear = 15f, InViewShare = 0.6f;

        /// <summary>Counted checks (OzLavaCheckLV4).</summary>
        public static int SpitsStarted, BigsStarted, BombsLaunched, BombsOnLava, BombsOnRim, BombsBad, MostAtOnceBig, MostAtOnceSpit, BigsInView, ChunksMade;
        public static float FirstStartAt = -1f, HighestAsh;

        sealed class Site
        {
            public OzVolcanoRow row; public int index; public Vector3 crater; public float mouth, groundY, sizeK;
            public Transform root; public MeshRenderer[] rocks = new MeshRenderer[0], flow = new MeshRenderer[0];
            public float heat, heatGoal, smokeBoost, smokeGoal, smokeAcc, flameK, flameGoal, nextOk;
            public bool blocked, flowOn; public float flowAge, flowRun, flowReach, flowSeed;
            public Mesh flame; public Bounds flameBounds;
            public Vector3[] landLava = new Vector3[0], landRim = new Vector3[0];
            public string fountKey, ventKey; public bool loopOn;
        }

        sealed class Run
        {
            public bool on, big, burst, fountEnd; public Site s; public float age, rumble, fount, fall, tail, height, dropAcc, ashAcc, moteAcc, glowAcc, nextBomb, range;
            public int bombsLeft, light = -1; public OzVolcanoSurges surges;
            public float End => rumble + fount + fall;
            public float Gone => End + tail;
            public bool Active => on && age < End;
        }

        struct Bomb { public bool on, toLava; public Vector3 from, vel, to, axis; public float age, seconds, size, spin, phase, trail; public int mesh; }
        struct Chunk { public bool on; public Vector3 at, axis; public float age, life, size, phase; public int mesh; }

        OzLava lava; ZGame game; ZMapData map; Camera cam; OpenZFxPlayer fx; OzVolcanoPlan plan;
        readonly List<Site> sites = new List<Site>();
        readonly Run[] runs = { new Run(), new Run(), new Run(), new Run() };
        readonly Bomb[] bombs = new Bomb[32]; readonly Chunk[] chunks = new Chunk[16];
        readonly Light[] lights = new Light[2];
        readonly int[] pick = new int[16], drawN = new int[3];
        Material flowMat, bombMat, flameMat; Texture2D volFlow, decorFlow;
        readonly Mesh[] rocks = new Mesh[3];
        readonly Matrix4x4[][] drawM = { new Matrix4x4[48], new Matrix4x4[48], new Matrix4x4[48] };
        readonly float[][] drawT = { new float[48], new float[48], new float[48] };
        readonly MaterialPropertyBlock[] drawB = new MaterialPropertyBlock[3];
        MaterialPropertyBlock rockBlock, flowBlock, flameBlock, keepBlock;
        float clock, pollAt, windAt; bool lite, calm, warming, flashSet; Vector2 wind, windGoal; Run lastRun;

        static readonly int EmisId = Shader.PropertyToID("_EmissionMapColor"), KId = Shader.PropertyToID("_K"), BombT = Shader.PropertyToID("_BombT"),
            FrontId = Shader.PropertyToID("_FlowFront"), RunId = Shader.PropertyToID("_FlowRun"), ReachId = Shader.PropertyToID("_FlowReach"), CoolId = Shader.PropertyToID("_FlowCool"),
            FadeId = Shader.PropertyToID("_FlowFade"), SeedId = Shader.PropertyToID("_FlowSeed"), MapId = Shader.PropertyToID("_FlowMap"), KeepId = Shader.PropertyToID("_OzKeepCpuCulling");

        public int Count => sites.Count;
        /// <summary>An eruption runs (its tail does not count).</summary>
        public bool Busy { get { for (int i = 0; i < runs.Length; i++) if (runs[i].Active) return true; return false; } }

        void Awake() { I = this; }
        void OnDestroy() { Down(); if (I == this) I = null; }                                                  // Down runs once from OzLava.Lv4Down; this covers the other destroy order on the shared object

        // ------------------------------------------------------------------ boot
        public void Boot(OzLava owner, ZGame g, ZMapData m)
        {
            lava = owner; game = g; map = m; cam = Camera.main; fx = OpenZFxPlayer.I;
            SpitsStarted = BigsStarted = BombsLaunched = BombsOnLava = BombsOnRim = BombsBad = MostAtOnceBig = MostAtOnceSpit = BigsInView = ChunksMade = 0; FirstStartAt = -1f; HighestAsh = 0f;
            plan = new OzVolcanoPlan(lava.seed * 31 + 7);
            lite = OzLavaFx.Lite; calm = OzLavaFx.Calm;
            var ta = Resources.Load<TextAsset>(ListRes);
            if (!ta) { Debug.LogWarning("[OpenZ] volcano: " + ListRes + " missing, no eruptions"); enabled = false; return; }
            var list = JsonUtility.FromJson<OzVolcanoList>(ta.text);
            if (list == null || list.rows == null || list.rows.Length == 0) { enabled = false; return; }
            if (fx) fx.EnsureVolcanoChannels();
            flowMat = Resources.Load<Material>(FlowRes); bombMat = Resources.Load<Material>(BombRes);
            volFlow = Resources.Load<Texture2D>(VolFlowRes); decorFlow = Resources.Load<Texture2D>(DecorFlowRes);
            var tmpl = Resources.Load<Material>("OpenZ/FX/M_InfernoFlame");
            if (tmpl)
            {
                flameMat = new Material(tmpl) { name = "VolcanoFlame (runtime)" };
                if (!flameMat.GetTexture("_MainTex")) flameMat.SetTexture("_MainTex", Resources.Load<Texture2D>("OpenZ/FX/Pyro/legacy-flame"));
                if (!flameMat.GetTexture("_NoiseTex")) flameMat.SetTexture("_NoiseTex", Resources.Load<Texture2D>("OpenZ/FX/Pyro/legacy-noise"));
                flameMat.SetFloat("_SoftFade", 0.3f);
            }
            rockBlock = new MaterialPropertyBlock(); flowBlock = new MaterialPropertyBlock(); flameBlock = new MaterialPropertyBlock(); keepBlock = new MaterialPropertyBlock(); keepBlock.SetFloat(KeepId, 1f);
            for (int i = 0; i < rocks.Length; i++) { rocks[i] = MakeRock(1907 + i * 13); drawB[i] = new MaterialPropertyBlock(); drawB[i].SetFloatArray(BombT, drawT[i]); }
            var rim = GameObject.Find("L07_Rim");
            int matched = 0;
            foreach (var r in list.rows)
            {
                var s = new Site { row = r, index = sites.Count, sizeK = r.kind == 1 ? 1.15f : r.kind == 2 ? 0.7f : 1f, mouth = Mathf.Max(r.rad * 0.6f, 0.22f) };
                s.fountKey = "vol_fount_" + s.index; s.ventKey = "vol_vent_" + s.index;
                var local = new Vector3(r.cx, r.cy, r.cz);
                s.root = FindModel(rim, r);
                if (s.root) { s.crater = s.root.TransformPoint(local); matched++; }
                else s.crater = new Vector3(r.x, r.y, r.z) + Quaternion.Euler(0f, -r.yaw, 0f) * (local * r.scale);
                s.groundY = r.kind == 1 ? game.GroundY(r.x, r.z) : lava.lavaY;
                if (s.root) DressModel(s);
                BuildFlame(s);
                BuildLanding(s);
                sites.Add(s);
                if (r.smoke && lava.playSounds && AudioDirector.I) { AudioDirector.I.StartLoop(s.ventKey, SndVent, s.crater); s.loopOn = true; }
            }
            for (int i = 0; i < lights.Length; i++)
            {
                var go = new GameObject("VOL_Light_" + i); go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>(); l.type = LightType.Point; l.shadows = LightShadows.None; l.enabled = false;
                var c = OzLavaRampTable.Eval(0.75f); float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)); l.color = new Color(c.r / mx, c.g / mx, c.b / mx); lights[i] = l;
            }
            windGoal = wind = new Vector2(0.7f, -0.3f);
            Debug.Log($"[OpenZ] volcano: {sites.Count} eruptors, {matched} on their models, flow {(flowMat && volFlow && decorFlow ? "on" : "OFF (run OpenZ/L07 Lava/LV4 Make volcano materials)")}, bombs {(bombMat ? "on" : "OFF")}, flame {(flameMat ? "on" : "OFF")}");
        }

        static Transform FindModel(GameObject rim, OzVolcanoRow r)
        {
            if (!rim || string.IsNullOrEmpty(r.node)) return null;
            foreach (Transform group in rim.transform)
                foreach (Transform t in group)
                {
                    if (!t.name.StartsWith(r.node, StringComparison.Ordinal)) continue;
                    var p = t.position;
                    if ((p.x - r.x) * (p.x - r.x) + (p.z - r.z) * (p.z - r.z) < 0.25f) { t.name = r.id; return t; }
                }
            return null;
        }

        /// <summary>The cone's renderers leave the GPU drawer (their crater heat is a property block, fix 12), and each gets a flow overlay, one per LOD, off until it erupts.</summary>
        void DressModel(Site s)
        {
            s.rocks = s.root.GetComponentsInChildren<MeshRenderer>(true);
            OzGrd.KeepCpuCulling(s.rocks);
            for (int i = 0; i < s.rocks.Length; i++) { var mr = s.rocks[i]; if (!mr.enabled) continue; mr.enabled = false; mr.enabled = true; }   // the drawer looks again at a renderer switched on
            var flowMap = s.row.kit == "vol" ? volFlow : decorFlow;
            if (!flowMat || !flowMap) return;
            var made = new List<MeshRenderer>(); var lod = s.root.GetComponent<LODGroup>(); var lods = lod ? lod.GetLODs() : null;
            MeshRenderer Overlay(MeshRenderer of, int n)
            {
                var mf = of.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh || of.isPartOfStaticBatch) { Debug.LogWarning("[OpenZ] volcano: " + s.row.id + " has no plain mesh for the flow overlay"); return null; }
                var go = new GameObject($"VOL_Flow_{s.index:00}_{n}"); go.layer = of.gameObject.layer; go.transform.SetParent(of.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = flowMat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off; r.enabled = false;
                flowBlock.Clear(); flowBlock.SetTexture(MapId, flowMap); r.SetPropertyBlock(flowBlock);
                made.Add(r); return r;
            }
            if (lods != null && lods.Length > 0)
            {
                for (int i = 0; i < lods.Length; i++)
                {
                    var had = lods[i].renderers; var all = new List<Renderer>(had);
                    foreach (var h in had) if (h is MeshRenderer hm) { var o = Overlay(hm, i); if (o) all.Add(o); }
                    lods[i].renderers = all.ToArray();
                }
                lod.SetLODs(lods);
            }
            else for (int i = 0; i < s.rocks.Length; i++) Overlay(s.rocks[i], i);
            s.flow = made.ToArray();
        }

        /// <summary>Tall licking flames in the crater mouth: the fort blaze's own column mesh (OzFortInferno.BuildColumns): world roots, one static mesh, the shader animates it.</summary>
        void BuildFlame(Site s)
        {
            if (!flameMat) return;
            int n = s.row.kind == 1 ? 9 : s.row.kind == 2 ? 4 : 5;
            float hMin = s.row.kind == 1 ? 4.5f : 2.2f, hMax = s.row.kind == 1 ? 7.5f : s.row.kind == 2 ? 3f : 4.2f, spread = Mathf.Max(s.row.rad * 0.7f, 0.12f);
            var rnd = new System.Random(s.index * 71 + 3);
            float Rf() => (float)rnd.NextDouble();
            var pos = new Vector3[n * 4]; var d0 = new List<Vector4>(n * 4); var d1 = new List<Vector4>(n * 4); var idx = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                float a = Rf() * 6.2832f, rr = spread * Mathf.Sqrt(Rf()), hgt = Mathf.Lerp(hMin, hMax, Rf()), wid = hgt * (0.32f + 0.12f * Rf()), seed = Rf() * 10f, speed = 0.7f + 0.7f * Rf(), phase = Rf();
                var root = s.crater + new Vector3(Mathf.Cos(a) * rr, -0.15f, Mathf.Sin(a) * rr);
                for (int c = 0; c < 4; c++)
                {
                    pos[i * 4 + c] = root;
                    d0.Add(new Vector4(c == 1 || c == 2 ? 1f : 0f, c >= 2 ? 1f : 0f, seed, wid)); d1.Add(new Vector4(hgt, speed, phase, 0f));
                }
                int b = i * 4; idx[i * 6] = b; idx[i * 6 + 1] = b + 2; idx[i * 6 + 2] = b + 1; idx[i * 6 + 3] = b; idx[i * 6 + 4] = b + 3; idx[i * 6 + 5] = b + 2;
            }
            s.flame = new Mesh { name = "OZ_VolcanoFlame" };
            s.flame.vertices = pos; s.flame.SetUVs(0, d0); s.flame.SetUVs(1, d1); s.flame.SetIndices(idx, MeshTopology.Triangles, 0, false);
            s.flameBounds = new Bounds(s.crater + Vector3.up * 4f, new Vector3(hMax + 6f, 12f, hMax + 6f)); s.flame.bounds = s.flameBounds;
        }

        /// <summary>Where this one may throw: map lava tiles, 4 to 26 m away; band tiles (blocked, no lava) for the band volcano only. Never walkable ground (fix 14).</summary>
        void BuildLanding(Site s)
        {
            if (s.row.kind == 2) return;                                                                        // a vent throws no rocks
            var lavaSpots = new List<Vector3>(); var rimSpots = new List<Vector3>();
            for (int ty = 0; ty < map.height; ty++)
                for (int tx = 0; tx < map.width; tx++)
                {
                    float x = tx * 2f + 1f, z = -(ty * 2f + 1f), dx = x - s.crater.x, dz = z - s.crater.z, d2 = dx * dx + dz * dz;
                    if (d2 < 16f || d2 > 26f * 26f) continue;
                    if (map.LavaAt(tx, ty)) lavaSpots.Add(new Vector3(x, lava.lavaY, z));
                    else if (s.row.kind == 1 && map.ClassAt(tx, ty) == ZClass.Impassable) rimSpots.Add(new Vector3(x, game.GroundY(x, z), z));
                }
            s.landLava = lavaSpots.ToArray(); s.landRim = rimSpots.ToArray();
        }

        /// <summary>A lump of 32 triangles: an octahedron split once, pushed about at random. Made up front, so no eruption pays for it.</summary>
        static Mesh MakeRock(int seed)
        {
            var rnd = new System.Random(seed);
            var v = new List<Vector3> { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            int[] face = { 0, 2, 4, 2, 1, 4, 1, 3, 4, 3, 0, 4, 2, 0, 5, 1, 2, 5, 3, 1, 5, 0, 3, 5 };
            var tri = new List<int>(); var mid = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 20) | (uint)b : ((long)b << 20) | (uint)a;
                if (mid.TryGetValue(key, out int m)) return m;
                v.Add(((v[a] + v[b]) * 0.5f).normalized); mid[key] = v.Count - 1; return v.Count - 1;
            }
            for (int i = 0; i < face.Length; i += 3)
            {
                int a = face[i], b = face[i + 1], c = face[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                tri.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
            }
            for (int i = 0; i < v.Count; i++) v[i] = Vector3.Scale(v[i], new Vector3(1f, 0.82f, 0.92f)) * (0.72f + 0.42f * (float)rnd.NextDouble());
            var mesh = new Mesh { name = "OZ_LavaBomb" }; mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            return mesh;
        }

        // ------------------------------------------------------------------ the frame
        float R() => plan.Next();

        public void Tick(float dt)
        {
            if (!enabled || sites.Count == 0) return;
            if (!cam) cam = Camera.main;
            if (!fx) { fx = OpenZFxPlayer.I; if (fx) fx.EnsureVolcanoChannels(); }
            clock += dt;
            if (clock >= pollAt) { pollAt = clock + 1f; lite = OzLavaFx.Lite; calm = OzLavaFx.Calm; }
            if (dt > 0f)
            {
                if (clock >= windAt) { windAt = clock + 8f + R() * 10f; float a = R() * 6.2832f, sp = 0.5f + R() * 0.8f; windGoal = new Vector2(Mathf.Cos(a) * sp, Mathf.Sin(a) * sp); }
                wind = Vector2.MoveTowards(wind, windGoal, dt * 0.08f);
                Clock(dt);
                for (int i = 0; i < sites.Count; i++) { var s = sites[i]; s.heatGoal = 0f; s.smokeGoal = 0f; s.flameGoal = 0f; }
                for (int i = 0; i < runs.Length; i++) if (runs[i].on) Step(runs[i], dt);
                StepBombs(dt);
                for (int i = 0; i < sites.Count; i++) StepSite(sites[i], dt);
            }
            Show();
        }

        void Clock(float dt)
        {
            int due = plan.Step(dt, ActiveSpits == 0, ActiveBigs == 0);
            if (due == OzVolcanoPlan.None) return;
            bool big = due == OzVolcanoPlan.Big;
            var s = big ? PickBig() : PickSpit();
            if (s == null || !Erupt(s, big)) { plan.Retry(due, big ? 6f : 3f); return; }
            plan.Started(due);
        }

        int ActiveBigs { get { int n = 0; for (int i = 0; i < runs.Length; i++) if (runs[i].Active && runs[i].big) n++; return n; } }
        int ActiveSpits { get { int n = 0; for (int i = 0; i < runs.Length; i++) if (runs[i].Active && !runs[i].big) n++; return n; } }

        bool SiteBusy(Site s) { for (int i = 0; i < runs.Length; i++) if (runs[i].on && runs[i].s == s) return true; return false; }

        bool OnScreen(Vector3 p)
        {
            if (!cam) return false;
            var v = cam.WorldToViewportPoint(p);
            return v.z > 0f && v.z < 160f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }

        /// <summary>Is a unit that fights (a robot or a vehicle) within r of p?</summary>
        bool UnitNear(Vector3 p, float r)
        {
            var objs = game.World.Objects; float r2 = r * r;
            for (int i = 0; i < objs.Count; i++)
            {
                var o = objs[i];
                if (o.Destroyed || !(o.IsRobot || o.Kind == ZKind.Vehicle)) continue;
                var c = game.CenterWorld(o, false);
                if ((c.x - p.x) * (c.x - p.x) + (c.z - p.z) * (c.z - p.z) < r2) return true;
            }
            return false;
        }

        Site PickSpit()
        {
            int n = 0;
            for (int i = 0; i < sites.Count && n < pick.Length; i++) { var s = sites[i]; if (!SiteBusy(s) && clock >= s.nextOk) pick[n++] = i; }
            return n == 0 ? null : sites[pick[(int)(R() * n) % n]];
        }

        /// <summary>A big one picks a volcano in view 60 % of the time, so players see it, but never in view with units within 15 m (fix 14).</summary>
        Site PickBig()
        {
            int nView = 0, nOff = 0; bool wantView = R() < InViewShare;
            for (int i = 0; i < sites.Count; i++)
            {
                var s = sites[i];
                if (!s.row.canBig || SiteBusy(s)) continue;
                if (OnScreen(s.crater)) { if (!UnitNear(s.crater, UnitsClear) && nView < 8) pick[nView++] = i; }
                else if (nOff < 8) pick[8 + nOff++] = i;
            }
            if (nView == 0 && nOff == 0) return null;
            if (nView > 0 && (wantView || nOff == 0)) { BigsInView++; return sites[pick[(int)(R() * nView) % nView]]; }
            if (nOff == 0) return null;
            return sites[pick[8 + (int)(R() * nOff) % nOff]];
        }

        /// <summary>For a flyby beat: 0 spit, 1 big. False when refused: no such volcano, it is busy, a run of that kind is on, or a vent asked for a big one.</summary>
        /// <summary>The level 7 flyover: one big eruption on the north lake as its shot starts. No volcano object on any other level.</summary>
        public static void FlyoverBeat(string shot) { if (I && (shot == "lake_north" || shot == "the_world")) I.Trigger(1, 1); }

        public bool Trigger(int index, int kind)
        {
            if (index < 0 || index >= sites.Count) return false;
            var s = sites[index]; bool big = kind == 1;
            if ((big && !s.row.canBig) || SiteBusy(s) || (big ? ActiveBigs : ActiveSpits) > 0) return false;
            return Erupt(s, big);
        }

        /// <summary>The eruptor nearest a point (a flyby asks by map position).</summary>
        public int NearestTo(float x, float z, bool canBig)
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < sites.Count; i++)
            {
                var c = sites[i].crater; float d = (c.x - x) * (c.x - x) + (c.z - z) * (c.z - z);
                if (d < bd && (!canBig || sites[i].row.canBig)) { bd = d; best = i; }
            }
            return best;
        }

        public Vector3 CraterOf(int index) => sites[index].crater;
        public bool ModelOf(int index) => sites[index].root;
        public int LandingsOf(int index) => sites[index].landLava.Length + sites[index].landRim.Length;
        public int FlowRenderersOf(int index) => sites[index].flow.Length;
        public bool CanBig(int index) => sites[index].row.canBig;

        bool Erupt(Site s, bool big)
        {
            Run r = null;
            for (int i = 0; i < runs.Length; i++) if (!runs[i].on) { r = runs[i]; break; }
            if (r == null) return false;
            r.on = true; r.big = big; r.s = s; r.age = 0f; r.burst = r.fountEnd = false; r.dropAcc = r.ashAcc = r.moteAcc = r.glowAcc = 0f; r.light = -1; lastRun = r;
            float b = R();
            if (big)
            {
                r.rumble = 2.5f; r.fount = 7f + 2f * R(); r.fall = 5f + 2f * R(); r.tail = 15f + 10f * R();      // 12-16 s before the tail (fix 14: 12-20 s)
                r.height = Mathf.Lerp(BigHeightMin, BigHeightMax, b) * s.sizeK; r.bombsLeft = 12 + (int)(R() * 10f); r.range = Mathf.Lerp(RangeBigMin, RangeBigMax, R());
                BigsStarted++;
            }
            else
            {
                r.rumble = 0.6f; r.fount = 1.6f + 1.4f * R(); r.fall = 0.9f; r.tail = 4f + 3f * R();
                r.height = Mathf.Lerp(SpitHeightMin, SpitHeightMax, b) * s.sizeK; r.bombsLeft = s.row.kind == 2 ? 0 : 1 + (int)(R() * 2f); r.range = RangeSpit;
                SpitsStarted++;
            }
            if (lite) r.bombsLeft = (r.bombsLeft + 1) / 2;
            r.nextBomb = 0f;
            r.surges.Make(plan, r.fount, big);
            s.nextOk = clock + 40f + R() * 50f;                                                                 // a spit 40-90 s apart on one volcano
            if (FirstStartAt < 0f) FirstStartAt = clock;
            MostAtOnceBig = Mathf.Max(MostAtOnceBig, ActiveBigs); MostAtOnceSpit = Mathf.Max(MostAtOnceSpit, ActiveSpits);
            for (int i = 0; i < lights.Length; i++) if (!lights[i].enabled) { lights[i].enabled = true; lights[i].transform.position = s.crater + Vector3.up * 2f; lights[i].range = r.range; lights[i].intensity = 0f; r.light = i; break; }
            if (!warming)
            {
                Sfx(SndRumble, s.crater, big ? 1f : 0.45f);
                if (big) lava.PopBurst(new Vector3(s.crater.x, lava.lavaY, s.crater.z), 12f, 3f, r.rumble + 1f);
            }
            return true;
        }

        void Sfx(string id, Vector3 at, float vol = 1f, float pitch = 1f) { if (lava.playSounds && !warming) AudioDirector.Fire(id, at, -1, vol, pitch); }

        // ------------------------------------------------------------------ one eruption
        void Step(Run r, float dt)
        {
            var s = r.s; r.age += dt; float a = r.age, k = lite ? 0.5f : 1f;
            if (a >= r.Gone) { End(r); return; }
            float heat, smoke, flame = 0f;
            if (a < r.rumble)
            {
                float u = a / r.rumble;
                heat = 0.2f + 0.45f * u * u; smoke = 0.35f + 0.65f * u;
                Grit(r, dt * k);
                MouthHeat(r, u, dt);
            }
            else
            {
                if (!r.burst) Burst(r);
                float f = a - r.rumble;
                if (f < r.fount)
                {
                    float S = r.surges.At(f);
                    Fountain(r, S, dt * k);
                    flame = 0.3f + 0.7f * S; heat = 0.7f + 0.3f * S; smoke = 1f;
                    if (r.bombsLeft > 0 && f >= r.nextBomb && (!r.big || f < 1.8f)) { r.bombsLeft--; r.nextBomb = f + 0.05f + 0.16f * R(); Launch(r); }
                    if (r.big && lava.playSounds && !warming && AudioDirector.I) AudioDirector.I.SetLoop(s.fountKey, 0.55f + 0.45f * S, 1f);
                }
                else
                {
                    if (!r.fountEnd) { r.fountEnd = true; if (r.big && !warming && AudioDirector.I) AudioDirector.I.StopLoop(s.fountKey, 1.6f); }
                    float u = Mathf.Clamp01((a - r.rumble - r.fount) / r.fall), w = 1f - u * u * (3f - 2f * u);
                    flame = 0.3f * w; heat = Mathf.Lerp(0.7f, 0.3f, u); smoke = Mathf.Lerp(1f, 0.6f, u);
                    if (w > 0f) Fountain(r, 0.14f * w, dt * k);
                    if (a >= r.End) { float v = (a - r.End) / r.tail; heat = 0.3f * (1f - v) * (1f - v); smoke = 0.6f * (1f - v); flame = 0f; Tail(r, 1f - v, dt * k); }
                }
                if (r.big && f < r.fount + 2.2f) Ash(r, dt * k);
                if (f < r.fount + r.fall) Glow(r, f, dt);
            }
            if (s.heatGoal < heat) s.heatGoal = heat;
            if (s.smokeGoal < smoke) s.smokeGoal = smoke;
            if (s.flameGoal < flame) s.flameGoal = flame;
            StepLight(r);
        }

        void End(Run r)
        {
            if (r.light >= 0) lights[r.light].enabled = false;
            r.light = -1; r.on = false; r.s = null;
        }

        /// <summary>Stage 1: dark grit rolls off the rim and falls down the side.</summary>
        void Grit(Run r, float dt)
        {
            r.dropAcc += dt * (r.big ? 26f : 8f);
            while (r.dropAcc >= 1f)
            {
                r.dropAcc -= 1f;
                float a = R() * 6.2832f, rr = Mathf.Max(r.s.row.rad, 0.3f) * 1.1f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Clot(r.s.crater + dir * rr, dir * (0.5f + R()) + Vector3.up * (0.3f + R() * 0.9f), 0.7f + R() * 0.5f, 0.06f + 0.05f * R(), new Color(0.22f, 0.16f, 0.14f, 0.9f));
            }
        }

        /// <summary>Stage 1's crater heat where it shows: the emission maps of BrownCone_1 and _2 hold nothing in the crater (blender_pass README, measured 0.0), and a vent has no model,
        /// so the property block alone leaves them dark until the burst. A soft halo grows in the mouth instead, slowly, so nothing flashes.</summary>
        void MouthHeat(Run r, float u, float dt)
        {
            r.glowAcc += dt * 7f;
            if (r.glowAcc < 1f || !fx) return;
            r.glowAcc -= 1f;
            fx.EmitChannel("leg_cloud", (warming ? Hide(r.s.crater) : r.s.crater) + Vector3.up * 0.25f, Vector3.up * 0.4f, 0.55f, (0.9f + 2.1f * u) * (r.big ? 1f : 0.5f) * r.s.sizeK, new Color(1f, 0.4f, 0.1f), (calm ? 0.1f : 0.2f) * (0.3f + 0.7f * u));
        }

        void Clot(Vector3 at, Vector3 vel, float life, float width, Color c)
        {
            if (fx) fx.VolcanoClot(warming ? Hide(at) : at, vel, life, width, c);
        }

        /// <summary>The warm pass draws under the ground, at the spot the camera looks at: a particle system outside the view is culled and would warm nothing.</summary>
        Vector3 warmAt;
        Vector3 Hide(Vector3 at) => warmAt;

        void FindWarmSpot()
        {
            warmAt = new Vector3(sites[0].crater.x, -20f, sites[0].crater.z);
            if (!cam) return;
            var f = cam.transform.forward; var p = cam.transform.position;
            if (f.y < -0.1f) warmAt = p + f * ((p.y + 20f) / -f.y);
        }

        /// <summary>Stage 2: one moment, all at once.</summary>
        void Burst(Run r)
        {
            r.burst = true;
            if (r.big && !calm) { var rts = FindAnyObjectByType<RtsCamera>(); if (rts) rts.Shake(BigShake, r.s.crater, "volcano_big"); }   // the game's own shake gate: on screen, cooldown
            var s = r.s; var c = s.crater + Vector3.up * 0.4f; float k = r.big ? 1f : 0.45f;
            if (warming) c = Hide(c);
            if (fx)
            {
                int balls = r.big ? 7 : 2;
                for (int i = 0; i < balls; i++)
                {
                    float a = R() * 6.2832f, sp = R() * 2.4f;
                    var vel = new Vector3(Mathf.Cos(a) * sp, (3f + R() * 5f) * k + 1.5f, Mathf.Sin(a) * sp);
                    fx.EmitChannel("m12_fire", c + new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)) * s.mouth, vel, 0.75f + 0.45f * R(), (2.1f + 1.2f * R()) * Mathf.Sqrt(k) * s.sizeK, Color.white, 1f);
                }
                if (!calm) fx.EmitChannel("blast_core", c + Vector3.up * 0.5f, Vector3.up, 0.35f, (r.big ? 4.2f : 2f) * s.sizeK, Color.white, 1f);
                if (r.big)
                    for (int i = 0; i < 12; i++)                                                                // the ring of ash thrown off the rim
                    {
                        float a = (i + R() * 0.6f) * 0.5236f; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        fx.VolcanoAsh(c + d * Mathf.Max(s.row.rad, 0.5f), d * (2.3f + 1.3f * R()) + Vector3.up * (0.2f + 0.7f * R()), 1.5f + 0.6f * R(), 0.9f + 0.5f * R(), new Color(0.2f, 0.17f, 0.16f), 0.75f);
                    }
                if (!warming && s.row.kind != 1) fx.LavaRing(new Vector3(s.crater.x, lava.lavaY, s.crater.z), r.big ? 5f : 2f, 1.2f);   // the band volcano stands on rock, no lava under it
            }
            Sfx(SndBurst, s.crater, r.big ? 1f : 0.5f);
            if (r.big && lava.playSounds && !warming && AudioDirector.I) AudioDirector.I.StartLoop(s.fountKey, SndFountain, s.crater);
            if (!warming && s.flow.Length > 0)
            {
                s.flowOn = true; s.flowAge = 0f; s.flowSeed = R() * 7f; s.flowReach = r.big ? 1f : SpitReach;
                s.flowRun = Mathf.Max(1f, s.row.run / FrontSpeed);
                for (int i = 0; i < s.flow.Length; i++) s.flow[i].enabled = true;
            }
        }

        /// <summary>Stage 3: the fountain, at surge S. An inner jet and outer clots.</summary>
        void Fountain(Run r, float S, float dt)
        {
            var s = r.s; float rate = r.big ? Mathf.Lerp(BigRateMin, BigRateMax, S) : Mathf.Lerp(SpitRateMin, SpitRateMax, S);
            r.dropAcc += rate * dt;
            float top = r.height * (0.55f + 0.45f * S), drop = Mathf.Max(s.crater.y - s.groundY, 0f);
            int guard = 16;
            while (r.dropAcc >= 1f && guard-- > 0)
            {
                r.dropAcc -= 1f;
                float a = R() * 6.2832f, rr = s.mouth * Mathf.Sqrt(R()), b = R() * 6.2832f; bool jet = R() < 0.3f;
                var at = s.crater + new Vector3(Mathf.Cos(a) * rr, 0.05f, Mathf.Sin(a) * rr);
                float h = jet ? top * (0.7f + 0.3f * R()) : top * (0.3f + 0.7f * R() * R());
                float vy = Mathf.Sqrt(2f * OzVolcanoArc.G * h), lean = (jet ? 0.05f : 0.2f) * (0.4f + R());
                var vel = new Vector3(Mathf.Cos(b) * vy * lean, vy, Mathf.Sin(b) * vy * lean);
                float life = jet ? vy / OzVolcanoArc.G * 1.05f : (vy + Mathf.Sqrt(vy * vy + 2f * OzVolcanoArc.G * drop)) / OzVolcanoArc.G;
                float wide = jet ? 0.42f + 0.2f * R() : 0.16f + 0.22f * R() * R();
                Clot(at, vel, life, wide, Color.white);
            }
            if (r.dropAcc > 8f) r.dropAcc = 8f;
        }

        /// <summary>Stage 4: one rock goes up. Spots: see BuildLanding; each one again checked against every unit at launch.</summary>
        void Launch(Run r)
        {
            var s = r.s; int slot = -1;
            for (int i = 0; i < bombs.Length; i++) if (!bombs[i].on) { slot = i; break; }
            if (slot < 0 || (warming && slot > 0)) return;
            float dMin = r.big ? 8f : 4f, dMax = r.big ? 25f : 11f;
            for (int tries = 0; tries < 8; tries++)
            {
                bool rim = s.landRim.Length > 0 && (s.landLava.Length == 0 || R() < 0.5f);
                var list = rim ? s.landRim : s.landLava;
                if (list.Length == 0) return;
                var to = list[(int)(R() * list.Length) % list.Length];
                to.x += (R() - 0.5f) * 1.2f; to.z += (R() - 0.5f) * 1.2f;                                       // inside the 2 m tile
                float dx = to.x - s.crater.x, dz = to.z - s.crater.z, d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < dMin || d > dMax || (!warming && UnitNear(to, BombClear))) continue;
                float apex = (r.big ? 3f + 6f * R() : 1.5f + 2f * R()) + d * 0.22f;
                OzVolcanoArc.Solve(s.crater.y + 0.2f, to.y, apex, out float vy, out float T);
                ref var b = ref bombs[slot];
                b.on = true; b.toLava = !rim; b.from = s.crater + Vector3.up * 0.2f; b.to = to; b.vel = new Vector3(dx / T, vy, dz / T); b.age = 0f; b.seconds = T; b.trail = 0f;
                b.size = (r.big ? 0.5f + 0.7f * R() : 0.2f + 0.16f * R()) * (s.row.kind == 1 ? 1.15f : 1f); b.mesh = (int)(R() * 3f) % 3;
                b.axis = new Vector3(R() - 0.5f, R() - 0.5f, R() - 0.5f).normalized; b.spin = 120f + 260f * R(); b.phase = R() * 360f;
                BombsLaunched++;
                if (R() < 0.4f) Sfx(SndWhoosh, s.crater, 0.7f);
                return;
            }
        }

        void StepBombs(float dt)
        {
            for (int i = 0; i < bombs.Length; i++)
            {
                ref var b = ref bombs[i];
                if (!b.on) continue;
                b.age += dt;
                if (b.age >= b.seconds) { Land(ref b); b.on = false; continue; }
                b.trail += dt * (lite ? 2.5f : 5f);
                if (b.trail >= 1f && fx)
                {
                    b.trail -= 1f; var p = BombAt(ref b);
                    fx.VolcanoAsh(warming ? Hide(p) : p, Vector3.up * 0.3f, 1.1f + 0.5f * R(), 0.35f + b.size * 0.5f, new Color(0.16f, 0.13f, 0.12f), 0.6f);
                }
            }
            for (int i = 0; i < chunks.Length; i++) { ref var c = ref chunks[i]; if (c.on) { c.age += dt; if (c.age >= c.life) c.on = false; } }
        }

        static Vector3 BombAt(ref Bomb b)
        {
            float t = b.age; return b.from + b.vel * t + 0.5f * OzVolcanoArc.G * t * t * Vector3.down;
        }

        void Land(ref Bomb b)
        {
            if (warming) return;
            int tx = Mathf.FloorToInt(b.to.x / 2f), ty = Mathf.FloorToInt(-b.to.z / 2f);
            bool isLava = map.LavaAt(tx, ty), isRim = !isLava && map.ClassAt(tx, ty) == ZClass.Impassable;
            if (isLava) BombsOnLava++; else if (isRim) BombsOnRim++; else BombsBad++;
            float s = Mathf.Clamp(b.size * 1.5f, 0.5f, 1.2f);
            if (b.toLava)
            {
                if (fx) fx.LavaHit(b.to, s, b.size > 0.55f);
            }
            else
            {
                if (fx) { fx.LavaDroplets(b.to, s, 8); fx.LavaSmoke(b.to, s); fx.LavaEmber(b.to, 4); }
                for (int i = 0; i < chunks.Length; i++)
                    if (!chunks[i].on) { ref var c = ref chunks[i]; c.on = true; c.at = b.to + Vector3.up * (b.size * 0.3f); c.axis = b.axis; c.age = 0f; c.life = 6f + 4f * R(); c.size = b.size; c.phase = b.phase; c.mesh = b.mesh; ChunksMade++; break; }
            }
            Sfx(b.toLava ? SndLand : SndLandRock, b.to, Mathf.Clamp01(b.size * 1.4f));
        }

        /// <summary>Stage 5: at most 40 puffs alive, two layers, a slow wind. Rising speed and life are set so that no puff passes the ceiling.</summary>
        void Ash(Run r, float dt)
        {
            r.ashAcc += AshRate * dt;
            var s = r.s; float y0 = s.crater.y + 0.8f, ceiling = Mathf.Min(s.crater.y + AshCeiling, (cam ? cam.transform.position.y : 70f) - CameraClear);
            while (r.ashAcc >= 1f)
            {
                r.ashAcc -= 1f; if (ceiling < y0 + 3f) continue;
                bool core = R() < 0.55f;
                float a = R() * 6.2832f, rr = (core ? 0.5f : 1.2f) * s.sizeK * Mathf.Sqrt(R()), rise = core ? 3.8f + 1.2f * R() : 3f + 0.8f * R();
                float life = (ceiling - y0) / rise * (0.8f + 0.2f * R());
                var at = new Vector3(s.crater.x + Mathf.Cos(a) * rr, y0, s.crater.z + Mathf.Sin(a) * rr);
                float spread = core ? 0.45f : 1.05f;
                var vel = new Vector3(Mathf.Cos(a) * spread + wind.x, rise, Mathf.Sin(a) * spread + wind.y);
                float grey = core ? 0.11f + 0.05f * R() : 0.2f + 0.07f * R();
                var c = new Color(grey * 1.06f, grey, grey * 0.96f);
                if (fx) fx.VolcanoAsh(warming ? Hide(at) : at, vel, life, core ? 2.3f + 0.8f * R() : 3f + 0.6f * R(), c, core ? 0.92f : 0.7f);
                HighestAsh = Mathf.Max(HighestAsh, y0 + life * rise - s.crater.y);
            }
        }

        /// <summary>The halo round the mouth, what the bloom picks up: a soft HDR cloud, its size following the surges.</summary>
        void Glow(Run r, float f, float dt)
        {
            r.glowAcc += dt * 7f;
            if (r.glowAcc < 1f || !fx) return;
            r.glowAcc -= 1f;
            float S = f < r.fount ? r.surges.At(f) : 0.1f, k = r.big ? 1f : 0.5f;
            fx.EmitChannel("leg_cloud", (warming ? Hide(r.s.crater) : r.s.crater) + Vector3.up * (0.8f + R()), Vector3.up * 1.2f, 0.5f, (3f + 3f * S) * k * r.s.sizeK, new Color(1f, 0.46f, 0.13f), calm ? 0.16f : 0.32f);
        }

        /// <summary>Stage 7: embers and falling ash thin out.</summary>
        void Tail(Run r, float left, float dt)
        {
            var s = r.s;
            r.moteAcc += dt * (r.big ? 16f : 4f) * left;
            while (r.moteAcc >= 1f && fx)
            {
                r.moteAcc -= 1f;
                if (R() < 0.45f) { if (!warming) fx.LavaEmber(s.crater + Vector3.up * (0.3f + R()), 1); continue; }
                float a = R() * 6.2832f, rr = R() * (r.big ? 9f : 3f), up = r.big ? 5f + 7f * R() : 2f + 2f * R();
                var at = s.crater + new Vector3(Mathf.Cos(a) * rr, up, Mathf.Sin(a) * rr);
                fx.VolcanoAsh(warming ? Hide(at) : at, new Vector3(wind.x * 0.5f, -0.75f - 0.4f * R(), wind.y * 0.5f), 2.4f + R(), 0.16f + 0.12f * R(), new Color(0.34f, 0.31f, 0.29f), 0.55f);
            }
        }

        void StepLight(Run r)
        {
            if (r.light < 0) return;
            var l = lights[r.light]; float a = r.age, I0;
            float base0 = (r.big ? LightBig : LightSpit) * (1f + (LightNight - 1f) * NightGrade.NightK);
            if (a >= r.rumble)
            {
                float f = a - r.rumble, env = f < r.fount ? 0.45f + 0.55f * r.surges.At(f) : Mathf.Max(0f, 0.45f * (1f - (f - r.fount) / r.fall));
                float kick = calm ? 0f : Mathf.Max(0f, 1f - f / 0.45f);
                I0 = base0 * (env + 0.9f * kick) * (0.88f + 0.24f * Mathf.PerlinNoise(clock * 5.3f, r.height));
            }
            else I0 = base0 * 0.12f * (a / r.rumble);
            if (calm) I0 = Mathf.MoveTowards(l.intensity, I0 * 0.6f, base0 * 0.6f * Time.deltaTime);
            l.intensity = I0;
            if (I0 <= 0f && a > r.End) { l.enabled = false; r.light = -1; }
        }

        void StepSite(Site s, float dt)
        {
            s.heat = Mathf.MoveTowards(s.heat, s.heatGoal, dt * (s.heatGoal > s.heat ? 0.9f : 0.22f));
            s.smokeBoost = Mathf.MoveTowards(s.smokeBoost, s.smokeGoal, dt * 0.6f);
            s.flameK = Mathf.MoveTowards(s.flameK, s.flameGoal, dt * 2.2f);
            if (s.flowOn)
            {
                s.flowAge += dt;
                if (s.flowAge > s.flowRun * s.flowReach + CoolSeconds + 3f) { s.flowOn = false; for (int i = 0; i < s.flow.Length; i++) s.flow[i].enabled = false; }
            }
            // thin smoke always on 2-4 craters, thicker and darker while one works up to it; only where a camera can see it
            float rate = (s.row.smoke ? 0.9f : 0f) + s.smokeBoost * 3.2f;
            if (rate <= 0f || !fx || !OnScreen(s.crater)) return;
            s.smokeAcc += rate * dt * (lite ? 0.5f : 1f);
            if (s.smokeAcc < 1f) return;
            s.smokeAcc -= 1f;
            float b = s.smokeBoost, g = Mathf.Lerp(0.55f, 0.1f, b), a = R() * 6.2832f, rr = s.mouth * R();         // idle: the thin pale thread of the owner's picture; darkens as it works up to an eruption
            var at = s.crater + new Vector3(Mathf.Cos(a) * rr, 0.25f, Mathf.Sin(a) * rr);
            var vel = new Vector3(wind.x * 0.8f + (R() - 0.5f) * 0.3f, 1.3f + 1.2f * b + 0.5f * R(), wind.y * 0.8f + (R() - 0.5f) * 0.3f);
            fx.VolcanoAsh(at, vel, 3.8f + 1.8f * R(), Mathf.Lerp(0.5f, 1f, b) * s.sizeK, new Color(g * 1.05f, g, g * 0.97f), Mathf.Lerp(0.2f, 0.62f, b));
        }

        // ------------------------------------------------------------------ what is written or drawn each frame, paused or not
        void Show()
        {
            for (int i = 0; i < sites.Count; i++)
            {
                var s = sites[i];
                if (s.rocks.Length > 0 && (s.heat > 0.002f || s.blocked)) CraterHeat(s);
                if (s.flowOn) FlowValues(s);
                if (s.flame && s.flameK > 0.02f)
                {
                    flameBlock.SetFloat(KId, s.flameK);
                    var rp = new RenderParams(flameMat) { layer = 0, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, matProps = flameBlock, worldBounds = s.flameBounds };
                    Graphics.RenderMesh(rp, s.flame, 0, Matrix4x4.identity);
                }
            }
            DrawBombs();
            // the crust's light and the bloom push: the strongest run only
            float best = 0f, push = 0f; Run br = null;
            for (int i = 0; i < runs.Length; i++)
            {
                var r = runs[i]; if (!r.on || !r.burst || r.age >= r.End) continue;
                float f = r.age - r.rumble, S = f < r.fount ? r.surges.At(f) : 0.1f, kick = Mathf.Max(0f, 1f - f / 0.5f), str = (r.big ? 0.55f : 0.2f) * (0.4f + 0.6f * S) + (r.big ? 0.45f : 0.1f) * kick;
                if (str > best) { best = str; br = r; }
                if (r.big) push = Mathf.Max(push, BloomPush * kick * kick + 0.22f * S);
            }
            if (br != null)
            {
                Shader.SetGlobalVector(OzLavaIds.Flash, new Vector4(br.s.crater.x, br.s.crater.z, br.big ? 22f : 9f, calm ? best * 0.4f : best)); flashSet = true;
                if (!calm && push > 0.01f) OpenZBloomDirector.Sustain(push);
            }
            else if (flashSet) { Shader.SetGlobalVector(OzLavaIds.Flash, Vector4.zero); flashSet = false; }
        }

        /// <summary>The crater's own glow on the cone: the shared value (which LV2 lets breathe) times the heat.</summary>
        void CraterHeat(Site s)
        {
            if (s.heat <= 0.002f)
            {
                for (int i = 0; i < s.rocks.Length; i++) s.rocks[i].SetPropertyBlock(keepBlock);
                s.blocked = false; return;
            }
            var m = s.rocks[0].sharedMaterial; if (!m) return;
            Color c = m.GetColor(EmisId) * (1f + CraterGain * s.heat); c.a = 1f;
            rockBlock.Clear(); rockBlock.SetColor(EmisId, c);
            for (int i = 0; i < s.rocks.Length; i++) s.rocks[i].SetPropertyBlock(rockBlock);
            s.blocked = true;
        }

        void FlowValues(Site s)
        {
            float last = s.flowRun * s.flowReach + CoolSeconds;
            float fade = 1f - Mathf.SmoothStep(0f, 1f, (s.flowAge - last * 0.8f) / (last * 0.2f + 3f));
            for (int i = 0; i < s.flow.Length; i++)
            {
                var r = s.flow[i];
                r.GetPropertyBlock(flowBlock);
                flowBlock.SetFloat(FrontId, s.flowAge); flowBlock.SetFloat(RunId, s.flowRun); flowBlock.SetFloat(ReachId, s.flowReach);
                flowBlock.SetFloat(CoolId, CoolSeconds); flowBlock.SetFloat(FadeId, fade); flowBlock.SetFloat(SeedId, s.flowSeed);
                r.SetPropertyBlock(flowBlock);
            }
        }

        void DrawBombs()
        {
            if (!bombMat) return;
            drawN[0] = drawN[1] = drawN[2] = 0;
            for (int i = 0; i < bombs.Length; i++)
            {
                ref var b = ref bombs[i]; if (!b.on) continue;
                Put(b.mesh, Matrix4x4.TRS(BombAt(ref b), Quaternion.AngleAxis(b.phase + b.spin * b.age, b.axis), Vector3.one * b.size), Mathf.Lerp(1.05f, 0.42f, b.age / b.seconds));
            }
            for (int i = 0; i < chunks.Length; i++)
            {
                ref var c = ref chunks[i]; if (!c.on) continue;
                float u = c.age / c.life;
                Put(c.mesh, Matrix4x4.TRS(c.at, Quaternion.AngleAxis(c.phase, c.axis), Vector3.one * c.size * (u > 0.85f ? (1f - u) / 0.15f : 1f)), 0.7f * (1f - u) * (1f - u));
            }
            for (int k = 0; k < rocks.Length; k++)
            {
                if (drawN[k] == 0) continue;
                drawB[k].SetFloatArray(BombT, drawT[k]);
                Graphics.DrawMeshInstanced(rocks[k], 0, bombMat, drawM[k], drawN[k], drawB[k], ShadowCastingMode.Off, false, 0, null, LightProbeUsage.Off);
            }
        }

        void Put(int mesh, Matrix4x4 m, float T)
        {
            int n = drawN[mesh];
            if (n >= drawM[mesh].Length) return;
            drawM[mesh][n] = m; drawT[mesh][n] = T; drawN[mesh] = n + 1;
        }

        // ------------------------------------------------------------------ warm-up and the end
        /// <summary>OzLavaPrewarm: one silent eruption of each kind through every state, far under the ground, with no light, sound, flow or crater heat, so the first real one
        /// finds its methods compiled and its materials loaded.</summary>
        public void WarmPass()
        {
            if (sites.Count == 0 || warming || Busy) return;                                                     // a flyby beat got in first: never touch a real eruption's rocks
            warming = true;
            if (!cam) cam = Camera.main;
            FindWarmSpot();
            int s0 = SpitsStarted, b0 = BigsStarted, l0 = BombsLaunched; float f0 = FirstStartAt;
            int site = 0; for (int i = 0; i < sites.Count; i++) if (!SiteBusy(sites[i]) && sites[i].row.canBig) { site = i; break; }
            var s = sites[site]; float ok = s.nextOk;
            if (!SiteBusy(s))
                for (int big = 0; big < 2; big++)
                {
                    if (!Erupt(s, big == 1)) continue;
                    var r = lastRun;
                    if (r.light >= 0) { lights[r.light].enabled = false; r.light = -1; }
                    for (float a = 0f, gone = r.Gone; a < gone && r.on; a += gone / 14f) { r.age = a; Step(r, 0.02f); }
                    if (r.on) End(r);
                    for (int i = 0; i < bombs.Length; i++) if (bombs[i].on) { bombs[i].from = Hide(bombs[i].from); bombs[i].vel = Vector3.zero; bombs[i].seconds = 1f; }
                    StepBombs(0.02f); DrawBombs();
                    for (int i = 0; i < bombs.Length; i++) bombs[i].on = false;
                }
            s.heatGoal = 0f; s.nextOk = ok; s.smokeGoal = 0f; s.flameGoal = 0f;
            if (fx) { fx.LavaDroplets(warmAt, 0.5f, 1); fx.LavaSmoke(warmAt, 0.5f); fx.LavaSteam(warmAt, 0.5f); fx.LavaEmber(warmAt, 1); }   // fix 24: one particle of each of LV2's channels
            SpitsStarted = s0; BigsStarted = b0; BombsLaunched = l0; FirstStartAt = f0; MostAtOnceBig = MostAtOnceSpit = 0; HighestAsh = 0f;
            warming = false;
        }

        public void Down()
        {
            for (int i = 0; i < runs.Length; i++) if (runs[i].on) End(runs[i]);
            if (AudioDirector.I)
                for (int i = 0; i < sites.Count; i++) { var s = sites[i]; AudioDirector.I.StopLoop(s.fountKey); if (s.loopOn) AudioDirector.I.StopLoop(s.ventKey); }
            for (int i = 0; i < sites.Count; i++) if (sites[i].flame) Destroy(sites[i].flame);
            for (int i = 0; i < rocks.Length; i++) if (rocks[i]) Destroy(rocks[i]);
            if (flameMat) Destroy(flameMat);
            if (flashSet) Shader.SetGlobalVector(OzLavaIds.Flash, Vector4.zero);
            sites.Clear(); enabled = false;
        }

        /// <summary>Play check: return OpenZ.OzVolcano.Status();</summary>
        public static string Status()
        {
            var v = I; if (!v) return "no volcano here";
            int live = 0; foreach (var r in v.runs) if (r.on) live++;
            return $"{v.sites.Count} eruptors, runs {live}, spits {SpitsStarted}, bigs {BigsStarted} ({BigsInView} in view), bombs {BombsLaunched}: lava {BombsOnLava}, rim {BombsOnRim}, bad {BombsBad}, chunks {ChunksMade}, most at once big {MostAtOnceBig} spit {MostAtOnceSpit}, highest ash {HighestAsh:F1} m, mood {v.plan.Mood:F2}";
        }
    }
}
