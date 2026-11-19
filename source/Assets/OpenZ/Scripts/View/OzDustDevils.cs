using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// DUST DEVIL (ONE since 1.5 kit one_dust_devil_r7: <see cref="Count"/>; two before), drawn as real swirling dust. View only: the rules
    /// never see it.
    /// <para>Before 1.5: Atmosphere built each devil as one URP Particles/Unlit system of soft round dots (0.6-1.6 m growing to 3.5 m, alpha
    /// 0.22, 50 a second, 3.5 s: about 175 live a devil) orbiting and rising: a pale streak of round beige dots, unlit.</para>
    /// <para>1.5 pass 6a REWORK (pass 5: the kit's lit-puff column read from the RTS camera as a STACK OF ROUND PUFFS, worse than the old
    /// streak). No round sprite anywhere now. Each devil ("DustDevil0"; "DustDevil1" only with <see cref="Count"/> 2; children of the
    /// Atmosphere as before, same start places,
    /// same 2.5 m/s meandering wander inside the same box, the same day / night amount) is:</para>
    /// <list type="bullet">
    /// <item>COLUMN: <see cref="Layers"/> nested open funnel meshes (narrow at the foot, flaring with height, 9-11 m) drawn with OpenZ/DustDevil
    /// (Shaders/OpenZDustDevil.shader): a scrolling, twisting streak noise (helical streaks that spin round and rise, slower at the top),
    /// soft alpha (the walls fade where they are seen edge-on, so no outline; fade in at the foot, out towards the top; soft against the
    /// ground and units), lit by the sun with wrap + a soft self shadow on the far side + sky; the ground's own dust colour under the foot.
    /// The column leans back against its travel and with the level's wind and sways slowly (vertex shear, eased in with the height).</item>
    /// <item>GROUND SWIRL: a flat soft ring of the same streak noise round the foot (3 m), spinning.</item>
    /// <item>WISPS + GRIT: stretched streak particles only (the desert sand streaks' material): dust wisps whipping round the foot and fine
    /// sand grains (3-7 cm) thrown up and round.</item>
    /// </list>
    /// Cost: 4 mesh draws a devil (a few hundred vertices each) + 2 small particle systems (caps 40 + 40). Switch <see cref="On"/> (false
    /// before the level loads = the old devils); if the devil material never comes (<see cref="FailAfterS"/>), <see cref="Failed"/> and the
    /// Atmosphere builds the old devils, so the devils are never lost. All numbers are public statics, read every frame (tune in Play).
    /// </summary>
    public sealed class OzDustDevils : MonoBehaviour
    {
        public static bool On = true;
        /// <summary>
        /// How many devils the desert builds, read once when they are built (here and
        /// for the old devils in Atmosphere.BuildDevils). 1 = "DustDevil0" alone, at its old start place (40, -60), with the same seed and
        /// heading draw, look, size, wander, speed, box and ground trace as before. Two devils wander on their own in the same box, so they met
        /// and stood together. 2 = as before the kit (a before / after look: set it in Play before the level loads; every Play start puts 1 back).</summary>
        public static int Count = 1;
        /// <summary>The Atmosphere's day / night amount (1 by day, 0.3 at full night), set every frame by Atmosphere.UpdateDesertParticles.</summary>
        public static float Strength = 1f;
        /// <summary>True when the devil material never came: the Atmosphere then builds the old devils.</summary>
        public static bool Failed { get; private set; }

        // ------------------------------------------------------------------ numbers (public statics: tune in Play, then copy here)
        /// <summary>Wander: speed (m/s, as before), the slow drift of the heading (deg/s at most), the box the devils stay in (as before).</summary>
        public static float Speed = 2.5f, TurnRate = 22f;
        const float BoxX0 = 10f, BoxX1 = 134f, BoxZ0 = -170f, BoxZ1 = -10f;
        /// <summary>The column's height (m).</summary>
        public static float Height = 10f;
        /// <summary>The funnel layers (inner core, body, outer veil): foot radius, top radius (m), height share, opacity, streak bands up the
        /// height, whole streak turns, streak cut (higher = thinner wisps), second-layer amount, spin at the foot / top (turns a second).</summary>
        public const int Layers = 3;
        public static float[] R0 = { 0.32f, 0.55f, 0.85f }, R1 = { 1.5f, 2.4f, 3.5f }, HShare = { 0.86f, 1f, 1.06f }, Alpha = { 2.4f, 1.7f, 1.0f },
                              Rep = { 1.8f, 1.3f, 1.0f }, Twist = { 2f, 2f, 1f }, Cut = { 0.2f, 0.24f, 0.3f }, Detail = { 0.55f, 0.5f, 0.4f },
                              SpinFoot = { 0.62f, 0.5f, 0.36f }, SpinTop = { 0.26f, 0.2f, 0.14f };
        /// <summary>The funnel's flare (radius grows with height^Flare), the streaks' rise (bands a second), the view-angle edge softness, the
        /// foot fade-in / top fade-out shares, light wrap and self shadow, soft fade (m).</summary>
        public static float Flare = 1.5f, Rise = 0.55f, Edge = 1.0f, FootFade = 0.05f, TopFade = 0.5f, Wrap = 0.8f, Shade = 0.15f, SoftFade = 1.0f;   // pass 6a live tune: wrap .6 -> .8, shade .45 -> .15 (no dark hollow in the funnel's top)
        /// <summary>The soft body under the streaks (0 = streaks only) and the lumpy, twisting outline (radial bulge share, speed).</summary>
        public static float Body = 0.5f, WobbleK = 0.28f, WobbleSpeed = 1.4f;
        /// <summary>Lean at the top (m): back against the travel, with the wind (x the wind vector), at most; sway at the top (m) and its speed.</summary>
        public static float LeanBack = 1.5f, LeanWind = 0.5f, LeanMax = 2.6f, SwayM = 0.55f, SwaySpeed = 0.7f;
        /// <summary>The ground swirl ring: outer radius (m), opacity, streak bands, spin (turns a second).</summary>
        public static float RingR = 3.0f, RingAlpha = 0.45f, RingRep = 2.5f, RingSpin = 0.4f, RingSoft = 0.1f;
        /// <summary>A devil over ground that gives no dust (a road, the river) keeps this share of its column (the dust it already carries).</summary>
        public static float ColMinDust = 0.4f;
        /// <summary>Wisps (dust streaks round the foot): a second, life, ring (m), rise, size (m), stretch, opacity, spin (rad/s).</summary>
        public static float WispRate = 14f, WispLifeMin = 0.9f, WispLifeMax = 1.6f, WispR0 = 0.5f, WispR1 = 2.0f, WispRiseMin = 0.3f, WispRiseMax = 1.4f,
                            WispSizeMin = 0.12f, WispSizeMax = 0.24f, WispLength = 7f, WispAlpha = 0.16f, WispSpin = 3.2f;
        /// <summary>Grit: particles a second, life, ring, rise, size (m), opacity, spin (rad/s).</summary>
        public static float GritRate = 16f, GritLifeMin = 1.0f, GritLifeMax = 1.8f, GritR0 = 0.3f, GritR1 = 1.4f, GritRiseMin = 0.5f, GritRiseMax = 1.5f,
                            GritSizeMin = 0.03f, GritSizeMax = 0.07f, GritAlpha = 0.5f, GritSpin = 5.5f;
        public const int WispCap = 40, GritCap = 40;
        /// <summary>The dust colour: the ground's own dust colour mixed this far towards a pale sand (the old devils' colour without a surface).</summary>
        public static float Pale = 0.3f;
        public static readonly Color PaleSand = new Color(0.9f, 0.8f, 0.64f), Fallback = new Color(0.86f, 0.74f, 0.56f);
        /// <summary>Seconds (real) to wait for the devil material before giving up (<see cref="Failed"/>).</summary>
        public const float FailAfterS = 6f;
        /// <summary>The devil material (Resources; its shader OpenZ/DustDevil is kept in player builds by it).</summary>
        public const string MaterialPath = "OpenZ/FX/DustDevil/OZ_DustDevil";

        // ------------------------------------------------------------------ the Play check's numbers
        public static int Built, Emitted, LiveNow, MaxLive, Draws;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Built = Emitted = LiveNow = MaxLive = Draws = 0; Failed = false; Strength = 1f; mat = null; Count = 1; }   // Count: 1.5 kit one_dust_devil_r7

        static Material mat; static Texture2D noise;
        static readonly int IdColor = Shader.PropertyToID("_Color"), IdAlpha = Shader.PropertyToID("_Alpha"), IdHeight = Shader.PropertyToID("_Height"),
            IdTwist = Shader.PropertyToID("_Twist"), IdRep = Shader.PropertyToID("_StreakRep"), IdSpin = Shader.PropertyToID("_Spin"), IdRise = Shader.PropertyToID("_Rise"),
            IdDetail = Shader.PropertyToID("_Detail"), IdCut = Shader.PropertyToID("_Cut"), IdEdge = Shader.PropertyToID("_Edge"), IdFoot = Shader.PropertyToID("_Foot"),
            IdTop = Shader.PropertyToID("_Top"), IdWrap = Shader.PropertyToID("_Wrap"), IdShade = Shader.PropertyToID("_Shade"), IdSoft = Shader.PropertyToID("_SoftFade"),
            IdLean = Shader.PropertyToID("_Lean"), IdSway = Shader.PropertyToID("_Sway"), IdRing = Shader.PropertyToID("_Ring"), IdStrength = Shader.PropertyToID("_Strength"),
            IdSeed = Shader.PropertyToID("_Seed"), IdNoise = Shader.PropertyToID("_NoiseTex"), IdBody = Shader.PropertyToID("_Body"), IdWobble = Shader.PropertyToID("_Wobble");

        sealed class Devil
        {
            public Transform root; public MeshRenderer[] funnels = new MeshRenderer[Layers]; public MeshRenderer ring; public ParticleSystem wisps, grit;
            public Vector3 dir, lean; public float seed, wispAcc, gritAcc, ds = 1f, dsDrawn = 1f, sampleAt;
            public Color dust = Fallback, dustDrawn = Fallback;
        }

        Atmosphere atmo; Material gritMat; float bornAt; bool built;
        readonly List<Devil> devils = new List<Devil>();
        MaterialPropertyBlock mpb;

        /// <summary>Atmosphere.BuildDesertParticles (Play, the desert preset): the devils' keeper under the Atmosphere (one only); the devils are
        /// built as soon as the devil material is there.</summary>
        public static OzDustDevils Make(Transform atmosphere, Material grit)
        {
            if (!atmosphere) return null;
            var have = atmosphere.GetComponentInChildren<OzDustDevils>(true);
            if (have) return have;
            var go = new GameObject("OZ_DustDevils") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(atmosphere, false);
            var k = go.AddComponent<OzDustDevils>();
            k.atmo = atmosphere.GetComponent<Atmosphere>(); k.gritMat = grit; k.bornAt = Time.unscaledTime;
            Failed = false;
            return k;
        }

        static bool MaterialReady()
        {
            if (mat) return true;
            var src = Resources.Load<Material>(MaterialPath);
            Shader sh = src ? src.shader : Shader.Find("OpenZ/DustDevil");
            if (!sh || !sh.isSupported) return false;
            mat = src ? new Material(src) : new Material(sh);
            mat.name = "OZ_DustDevil (runtime)"; mat.hideFlags = HideFlags.DontSave; mat.renderQueue = 3007;
            mat.SetTexture(IdNoise, Noise());
            return true;
        }

        // ------------------------------------------------------------------ the streak noise (built once, 256 x 256, tileable both ways)
        static float Hash(int x, int y, int s) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263 + s * 2147483647); h = (h ^ (h >> 13)) * 1274126177u; return ((h ^ (h >> 16)) & 0xffffff) / 16777215f; } }
        static float Value(float x, float y, int px, int py, int s)
        {   // periodic value noise: px x py cells over the unit square
            float fx = x * px, fy = y * py; int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy); float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
            int x1 = x0 + 1, y1 = y0 + 1; x0 = ((x0 % px) + px) % px; x1 = ((x1 % px) + px) % px; y0 = ((y0 % py) + py) % py; y1 = ((y1 % py) + py) % py;
            float a = Hash(x0, y0, s), b = Hash(x1, y0, s), c = Hash(x0, y1, s), d = Hash(x1, y1, s);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }
        static Texture2D Noise()
        {
            if (noise) return noise;
            const int N = 256;
            var px = new Color32[N * N];
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    float u = i / (float)N, v = j / (float)N;
                    // R: streaks: long round the column (few cells in x), thin across (many in y), 4 octaves
                    float r = Value(u, v, 3, 22, 1) * 0.5f + Value(u, v, 6, 44, 2) * 0.27f + Value(u, v, 12, 88, 3) * 0.15f + Value(u, v, 24, 128, 4) * 0.08f;
                    r = Mathf.Clamp01((r - 0.2f) / 0.62f);
                    // G: soft clumps
                    float g = Value(u, v, 4, 4, 5) * 0.65f + Value(u, v, 8, 8, 6) * 0.35f;
                    px[j * N + i] = new Color32((byte)(r * 255f), (byte)(Mathf.Clamp01(g) * 255f), 0, 255);
                }
            noise = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { name = "OZ_DustDevilNoise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2, hideFlags = HideFlags.DontSave };
            noise.SetPixels32(px); noise.Apply(true, true);
            return noise;
        }

        // ------------------------------------------------------------------ meshes (built once)
        static Mesh[] funnelMeshes; static Mesh ringMesh;
        static Mesh Funnel(float r0, float r1, float h)
        {
            const int S = 40, R = 16;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int j = 0; j < R; j++)
            {
                float t = j / (R - 1f), y = t * h;
                float r = r0 + (r1 - r0) * Mathf.Pow(t, Flare);
                float dr = (r1 - r0) * Flare * Mathf.Pow(Mathf.Max(t, 1e-3f), Flare - 1f) / Mathf.Max(0.1f, h);   // dr / dy
                for (int i = 0; i <= S; i++)
                {
                    float a = i / (float)S, ang = a * Mathf.PI * 2f;
                    var o = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    v.Add(o * r + Vector3.up * y); n.Add((o - Vector3.up * dr).normalized); uv.Add(new Vector2(a, t));
                }
            }
            for (int j = 0; j < R - 1; j++)
                for (int i = 0; i < S; i++)
                {
                    int a = j * (S + 1) + i, b = a + S + 1;
                    tri.Add(a); tri.Add(b); tri.Add(a + 1); tri.Add(a + 1); tri.Add(b); tri.Add(b + 1);
                }
            var m = new Mesh { name = "OZ_DustDevilFunnel", hideFlags = HideFlags.DontSave };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.bounds = new Bounds(Vector3.up * h * 0.5f, new Vector3(r1 * 2f + LeanMax * 2f + 3f, h + 1f, r1 * 2f + LeanMax * 2f + 3f));   // room for the lean / sway (vertex shader)
            return m;
        }
        static Mesh Ring(float r)
        {
            const int S = 40, R = 7; const float rIn = 0.2f;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int j = 0; j < R; j++)
            {
                float t = j / (R - 1f), rr = Mathf.Lerp(rIn, r, t);
                for (int i = 0; i <= S; i++) { float a = i / (float)S, ang = a * Mathf.PI * 2f; v.Add(new Vector3(Mathf.Cos(ang) * rr, 0.12f, Mathf.Sin(ang) * rr)); n.Add(Vector3.up); uv.Add(new Vector2(a, t)); }
            }
            for (int j = 0; j < R - 1; j++)
                for (int i = 0; i < S; i++) { int a = j * (S + 1) + i, b = a + S + 1; tri.Add(a); tri.Add(a + 1); tri.Add(b); tri.Add(a + 1); tri.Add(b + 1); tri.Add(b); }
            var m = new Mesh { name = "OZ_DustDevilRing", hideFlags = HideFlags.DontSave };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.bounds = new Bounds(new Vector3(0f, 0.12f, 0f), new Vector3(r * 2f, 1f, r * 2f));   // never a flat box (culling)
            return m;
        }

        void Update()
        {
            if (Failed) return;
            if (atmo && !atmo.isActiveAndEnabled) return;                                             // the atmosphere is off (the test mode): nothing drawn
            if (!built)
            {
                if (!MaterialReady())
                {
                    if (Time.unscaledTime - bornAt > FailAfterS) { Failed = true; Debug.LogWarning("[OpenZ] dust devils: the devil material never came: the old devils are drawn"); }
                    return;
                }
                Build();
            }
            float dt = Time.deltaTime;
            int live = 0, draws = 0;
            var wind = OzShuttleExhaust.Wind();
            foreach (var d in devils)
            {
                if (!d.root) continue;
                if (dt > 0f) { Move(d, dt); Emit(d, dt); OzDustDevilTrace.Step(d.root.position, d.dir, d.dsDrawn, d.dustDrawn, d.seed, dt); }   // 1.5 kit view_notes11 N9: the trace on the sand (View/OzDustDevilTrace.cs)
                Look(d, dt, wind);
                live += (d.wisps ? d.wisps.particleCount : 0) + (d.grit ? d.grit.particleCount : 0);
                foreach (var f in d.funnels) if (f && f.enabled) draws++;
                if (d.ring && d.ring.enabled) draws++;
            }
            LiveNow = live; if (live > MaxLive) MaxLive = live; Draws = draws;
        }

        // ------------------------------------------------------------------ build (once)
        void Build()
        {
            built = true;
            mpb = new MaterialPropertyBlock();
            if (funnelMeshes == null || funnelMeshes.Length != Layers || !funnelMeshes[0])
            {
                funnelMeshes = new Mesh[Layers];
                for (int l = 0; l < Layers; l++) funnelMeshes[l] = Funnel(R0[l], R1[l], Height * HShare[l]);
            }
            if (!ringMesh) ringMesh = Ring(RingR);
            var parent = transform.parent ? transform.parent : transform;
            for (int i = 0; i < Count; i++)                                                          // 1.5 kit one_dust_devil_r7: ONE devil (was 2)
            {
                var d = new Devil { seed = Random.Range(0f, 100f), dir = Quaternion.Euler(0, Random.Range(0, 360), 0) * Vector3.forward };
                d.root = new GameObject("DustDevil" + i) { hideFlags = HideFlags.DontSave }.transform;   // the old name: LookCheck / ShadowFlickerProbe find it
                d.root.SetParent(parent, false);
                d.root.position = new Vector3(40 + 60 * i, 0, -60 - 50 * i);                           // the old start places
                for (int l = 0; l < Layers; l++) d.funnels[l] = Part(d.root, "DustDevil" + i + "_Funnel" + l, funnelMeshes[l]);
                d.ring = Part(d.root, "DustDevil" + i + "_GroundSwirl", ringMesh);
                d.wisps = System(d.root, "DustDevil" + i + "_Wisps", gritMat, WispCap, Keys(0f, 0f, 0.15f, 1f, 0.6f, 0.7f, 1f, 0f), WispSpin, WispSpin * 0.6f, 0.4f, 0.9f, WispLength);
                d.grit = System(d.root, "DustDevil" + i + "_Grit", gritMat, GritCap, Keys(0f, 0f, 0.1f, 1f, 0.7f, 0.8f, 1f, 0f), GritSpin, GritSpin * 0.55f, 0.3f, 0.8f, 2f);
                devils.Add(d);
                Ground(d, d.root.position);
                d.dsDrawn = d.ds; d.dustDrawn = d.dust; d.lean = -d.dir * LeanBack;
            }
            Built++;
        }

        static MeshRenderer Part(Transform root, string name, Mesh m)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off; r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return r;
        }

        static GradientAlphaKey[] Keys(params float[] tv)
        {
            var k = new GradientAlphaKey[tv.Length / 2];
            for (int i = 0; i < k.Length; i++) k[i] = new GradientAlphaKey(tv[2 * i + 1], tv[2 * i]);
            return k;
        }

        /// <summary>Stretched streak particles (no round sprite): orbit fast at the foot, slower higher up, thrown outwards.</summary>
        static ParticleSystem System(Transform root, string name, Material m, int cap, GradientAlphaKey[] alpha, float spin0, float spin1, float out0, float out1, float length)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>(); var r = go.GetComponent<ParticleSystemRenderer>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.Local;   // the whirl carries its dust
            main.maxParticles = cap; main.startSpeed = 0f; main.startLifetime = 1f; main.startSize = 1f;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient(); grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha);
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
            v.x = new ParticleSystem.MinMaxCurve(0f); v.y = new ParticleSystem.MinMaxCurve(0f); v.z = new ParticleSystem.MinMaxCurve(0f);
            var flat = AnimationCurve.Constant(0f, 1f, 0f);
            v.orbitalX = new ParticleSystem.MinMaxCurve(0f, flat); v.orbitalZ = new ParticleSystem.MinMaxCurve(0f, flat);
            v.orbitalY = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, spin0, 1f, spin1));
            v.radial = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, out0, 1f, out1));
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.09f; r.lengthScale = length; r.sortMode = ParticleSystemSortMode.None;
            r.sharedMaterial = m;
            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------------ every frame
        void Move(Devil d, float dt)
        {
            float turn = (Mathf.PerlinNoise(d.seed, Time.time * 0.12f) - 0.5f) * 2f * TurnRate * dt;   // a slow meander
            d.dir = Quaternion.Euler(0f, turn, 0f) * d.dir;
            var p = d.root.position + d.dir * (Speed * dt);
            if (p.x < BoxX0 || p.x > BoxX1 || p.z > BoxZ1 || p.z < BoxZ0) d.dir = -d.dir;               // the old box, the old bounce
            Ground(d, p);
        }

        void Ground(Devil d, Vector3 p)
        {
            var g = ZGame.I;
            if (g) p.y = g.GroundY(p.x, p.z);
            else if (Physics.Raycast(new Vector3(p.x, 200, p.z), Vector3.down, out var hit, 400, Physics.DefaultRaycastLayers & ~OzRockSolid.Mask)) p.y = hit.point.y;
            d.root.position = p;
            if (Time.time >= d.sampleAt)
            {   // the ground's dust under the foot, 4 times a second
                d.sampleAt = Time.time + 0.25f;
                var tm = TrackMarks.I; var s = tm ? tm.Surface : null;
                if (s != null) { d.ds = Mathf.Clamp01(s.DustStrength(p.x, p.z)); d.dust = Color.Lerp(s.DustColor(p.x, p.z), PaleSand, Pale); }
                else { d.ds = 1f; d.dust = Fallback; }
            }
        }

        /// <summary>The column's look this frame: colour and amount eased (no pops over a road or the river), the lean eased (back against the
        /// travel, with the wind: a bounce at the box turns it over a few seconds), every number from the statics.</summary>
        void Look(Devil d, float dt, Vector3 wind)
        {
            float k = 1f - Mathf.Exp(-dt * 1.5f);
            d.dsDrawn = Mathf.Lerp(d.dsDrawn, d.ds, k); d.dustDrawn = Color.Lerp(d.dustDrawn, d.dust, k);
            var want = -d.dir * LeanBack + wind * LeanWind; want.y = 0f; want = Vector3.ClampMagnitude(want, LeanMax);
            d.lean = Vector3.Lerp(d.lean, want, 1f - Mathf.Exp(-dt * 0.6f));
            float s = Mathf.Clamp01(Strength);
            float col = s * Mathf.Max(ColMinDust, d.dsDrawn);
            for (int l = 0; l < Layers; l++)
            {
                var r = d.funnels[l]; if (!r) continue;
                mpb.Clear();
                Common(d, col);
                mpb.SetFloat(IdAlpha, Alpha[l]); mpb.SetFloat(IdHeight, Height * HShare[l]); mpb.SetFloat(IdTwist, Twist[l]); mpb.SetFloat(IdRep, Rep[l]);
                mpb.SetVector(IdSpin, new Vector4(SpinFoot[l], SpinTop[l], 0f, 0f)); mpb.SetFloat(IdDetail, Detail[l]); mpb.SetFloat(IdCut, Cut[l]);
                mpb.SetFloat(IdRing, 0f); mpb.SetFloat(IdSeed, d.seed + l * 3.7f);
                r.SetPropertyBlock(mpb);
                r.enabled = col > 0.003f;
            }
            if (d.ring)
            {
                mpb.Clear();
                float ringK = s * d.dsDrawn;
                Common(d, ringK);
                mpb.SetFloat(IdAlpha, RingAlpha); mpb.SetFloat(IdHeight, 1f); mpb.SetFloat(IdTwist, 3f); mpb.SetFloat(IdRep, RingRep);
                mpb.SetVector(IdSpin, new Vector4(RingSpin, RingSpin * 0.5f, 0f, 0f)); mpb.SetFloat(IdDetail, 0.5f); mpb.SetFloat(IdCut, 0.38f);
                mpb.SetFloat(IdRing, 1f); mpb.SetFloat(IdSeed, d.seed + 11f); mpb.SetFloat(IdSoft, RingSoft);   // lies 12 cm over the ground: a short soft fade
                d.ring.SetPropertyBlock(mpb);
                d.ring.enabled = ringK > 0.003f;
            }
        }

        void Common(Devil d, float strength)
        {
            mpb.SetColor(IdColor, d.dustDrawn); mpb.SetFloat(IdStrength, strength); mpb.SetFloat(IdRise, Rise); mpb.SetFloat(IdEdge, Edge);
            mpb.SetFloat(IdFoot, FootFade); mpb.SetFloat(IdTop, TopFade); mpb.SetFloat(IdWrap, Wrap); mpb.SetFloat(IdShade, Shade); mpb.SetFloat(IdSoft, SoftFade);
            mpb.SetVector(IdLean, new Vector4(d.lean.x, 0f, d.lean.z, 0f)); mpb.SetVector(IdSway, new Vector4(SwayM, SwaySpeed, 0f, 0f));
            mpb.SetFloat(IdBody, Body); mpb.SetVector(IdWobble, new Vector4(WobbleK, WobbleSpeed, 0f, 0f));
            if (noise) mpb.SetTexture(IdNoise, noise);
        }

        void Emit(Devil d, float dt)
        {
            float s = Mathf.Clamp01(Strength);
            d.wispAcc = Mathf.Min(3f, d.wispAcc + WispRate * s * d.ds * dt);
            d.gritAcc = Mathf.Min(3f, d.gritAcc + GritRate * s * d.ds * dt);
            while (d.wispAcc >= 1f) { d.wispAcc -= 1f; Wisp(d); }
            while (d.gritAcc >= 1f) { d.gritAcc -= 1f; Grit(d); }
        }

        static Vector3 RingAt(float r0, float r1, float y0, float y1, out Vector3 outward)
        {
            float a = Random.value * Mathf.PI * 2f, r = Random.Range(r0, r1);
            outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            return outward * r + Vector3.up * Random.Range(y0, y1);
        }

        void Wisp(Devil d)
        {
            var c = Color.Lerp(d.dust, PaleSand, 0.2f); c.a = WispAlpha * Random.Range(0.6f, 1f);
            var p = RingAt(WispR0, WispR1, 0.1f, 1.2f, out var o);
            var ep = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false, position = p,
                velocity = Vector3.up * Random.Range(WispRiseMin, WispRiseMax) + o * 0.15f,
                startLifetime = Random.Range(WispLifeMin, WispLifeMax), startSize = Random.Range(WispSizeMin, WispSizeMax), startColor = c,
            };
            d.wisps.Emit(ep, 1); Emitted++;
        }

        void Grit(Devil d)
        {
            var c = Color.Lerp(d.dust, PaleSand, 0.4f); c.a = GritAlpha * Random.Range(0.6f, 1f);
            var p = RingAt(GritR0, GritR1, 0.05f, 0.6f, out var o);
            var ep = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false, position = p,
                velocity = Vector3.up * Random.Range(GritRiseMin, GritRiseMax) + o * 0.2f,
                startLifetime = Random.Range(GritLifeMin, GritLifeMax), startSize = Random.Range(GritSizeMin, GritSizeMax), startColor = c,
            };
            d.grit.Emit(ep, 1); Emitted++;
        }

        void OnDestroy()
        {
            foreach (var d in devils) if (d.root) Destroy(d.root.gameObject);                             // the devils go with their keeper
        }
    }
}
