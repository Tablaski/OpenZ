// OpenZ (GPL-3.0)
// View only: reads the drawn water, writes nothing
// to the rules.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The water where a sewage outfall pours in (level 17, View/HarbourDetails.cs), in place of the flat pale ring it threw out before
    /// (the m12_ripple channel in the foam colour, opacity 0.75, a new ring every 0.55-0.8 s growing to a radius of 2.8-4.3 m: several
    /// big pale circles at once round a 0.6 m pipe).
    /// <para>ONE mesh and one renderer for every outfall on screen (one draw call), rebuilt each frame on the water surface, soft edges
    /// from the vertex alpha, in the water's own colours (OzWadeWake.Colours: the level's water material), darker at night:</para>
    /// <list type="bullet">
    /// <item>a soft foam apron round the landing place, longer on the water side, its edge slowly changing shape;</item>
    /// <item>small foam flecks that leave the landing place, drift out with the flow, slow down, spread and melt into the water;</item>
    /// <item>two small soft ripple arcs on the water side only, never a closed circle, gone about 1 m from the landing place.</item>
    /// </list>
    /// HarbourDetails keeps its pouring ribbon, the boiling foam patch at the landing place, the splash droplets, the mist, the wet stain
    /// and the pouring sound exactly as they were. Everything here moves on HarbourDetails' own clock: paused = the same picture.
    /// Nothing is visible further than about 1.5 m from the landing place (Status() prints the farthest visible point measured).
    /// GRAPHICS QUALITY LOW: three flecks, one ripple arc. Off (<see cref="On"/> false) = the old rings (before / after proof).
    /// </summary>
    [DefaultExecutionOrder(86)]
    public sealed class OzOutfallFoam : MonoBehaviour
    {
        public static bool On = true;
        /// <summary>Apron: reach from its middle towards the water / sideways / towards the wall (m), opacity at its middle.</summary>
        public static float ApronAhead = 0.95f, ApronSide = 0.72f, ApronBack = 0.30f, ApronA = 0.36f;
        /// <summary>Flecks: how far they drift (m), radius young / old (m), opacity, life (s).</summary>
        public static float FleckStart = 0.30f, FleckDrift = 0.90f, FleckR0 = 0.09f, FleckR1 = 0.20f, FleckA = 0.40f, FleckLife = 3.4f;
        /// <summary>Ripple arcs: radius young / old (m), half width young / old (m), period (s), opacity.</summary>
        public static float RipR0 = 0.45f, RipR1 = 1.00f, RipW0 = 0.05f, RipW1 = 0.11f, RipPeriod = 2.6f, RipA = 0.26f;
        /// <summary>The middle of it all sits this far from the landing point on the water side (m; the boiling patch's own middle).</summary>
        public static float Ahead = 0.25f;
        /// <summary>The unlit mesh's light: by day / at full night (DetailKit.Night).</summary>
        public static float DayLight = 0.95f, NightLight = 0.32f;
        public const int MaxOutfalls = 16, FleckN = 6;

        /// <summary>Counted proof: outfalls fed, frames built, old rings held, outfalls drawn in the last frame; the farthest visible point
        /// (opacity over 0.05) from a landing point (m) and the strongest opacity drawn.</summary>
        public static int Fed, Builds, OldRingsHeld, Drawn;
        public static float WidestM, PeakA;

        struct E { public int frame; public bool any, gush; public Vector3 impact, dir; public float clock, gushK; }

        static OzOutfallFoam host;
        static readonly E[] es = new E[MaxOutfalls];
        static readonly List<Vector3> V = new List<Vector3>(2048);
        static readonly List<Color32> C = new List<Color32>(2048);
        static readonly List<int> T = new List<int>(8192);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            host = null; System.Array.Clear(es, 0, es.Length);
            Fed = Builds = OldRingsHeld = Drawn = 0; WidestM = PeakA = 0f;
        }

        static bool Low => OzLite.Active && OzLite.Level >= 2;

        static bool Ready()
        {
            if (!On) return false;
            if (!host)
            {
                System.Array.Clear(es, 0, es.Length);                                                        // a new scene: the old entries went with the old host
                var go = new GameObject("OZ_OutfallFoam");
                host = go.AddComponent<OzOutfallFoam>(); host.Bind();
            }
            return host.mr && host.mr.sharedMaterial;
        }

        /// <summary>HarbourDetails.StepOutfalls, when its ring is due: true = this draws the water round the landing place (the caller
        /// leaves its ring out); false = off or no material (the caller throws its ring as before).</summary>
        public static bool Holds()
        {
            if (!Ready()) return false;
            OldRingsHeld++;
            return true;
        }

        /// <summary>HarbourDetails.DrawOutfalls, every frame for an outfall on screen (also while paused): idx = its number, impact = where
        /// its flow meets the water, down = the flow's direction, clock = HarbourDetails' clock (it stands while the game is paused),
        /// gush = the grille just broke (a little more foam for those seconds).</summary>
        public static void Feed(int idx, Vector3 impact, Vector3 down, float clock, bool gush)
        {
            if (idx < 0 || idx >= MaxOutfalls || !Ready()) return;
            var d = new Vector3(down.x, 0f, down.z); if (d.sqrMagnitude < 1e-6f) return;
            float dt = es[idx].any ? Mathf.Clamp(clock - es[idx].clock, 0f, 0.1f) : 0f;
            es[idx].gushK = Mathf.MoveTowards(es[idx].gushK, gush ? 1f : 0f, dt / 0.6f);
            es[idx].any = true; es[idx].gush = gush; es[idx].frame = Time.frameCount; es[idx].impact = impact; es[idx].dir = d.normalized; es[idx].clock = clock;
            Fed++;
        }

        // ------------------------------------------------------------------ the host
        Mesh mesh; MeshRenderer mr;

        void Bind()
        {
            mesh = new Mesh { name = "OzOutfallFoam" }; mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            mr = gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = OzWadeWake.Mat();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; mr.enabled = false;
        }

        void OnDestroy() { if (mesh) Destroy(mesh); if (host == this) host = null; }

        void LateUpdate()
        {
            V.Clear(); C.Clear(); T.Clear();
            int f = Time.frameCount, drawn = 0;
            if (On)
            {
                lightK = Mathf.Lerp(DayLight, NightLight, Mathf.Clamp01(DetailKit.Night));
                OzWadeWake.Colours();
                cFoam = OzWadeWake.FoamTint; cRip = OzWadeWake.RippleTint;
                for (int i = 0; i < MaxOutfalls; i++)
                    if (es[i].any && es[i].frame == f) { Draw(i); drawn++; }
            }
            Drawn = drawn;
            mesh.Clear();
            if (T.Count == 0 || !mr.sharedMaterial) { mr.enabled = false; return; }
            mesh.SetVertices(V); mesh.SetColors(C); mesh.SetTriangles(T, 0, true);
            mr.enabled = true; Builds++;
        }

        // ------------------------------------------------------------------ the mesh
        float lightK; Color cFoam, cRip;
        Vector3 from;

        /// <summary>A fixed 0..1 number per whole number (the same on every machine: view only, but the picture repeats the same way).</summary>
        static float H(int n)
        {
            unchecked
            {
                n = (n << 13) ^ n;
                return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483648f;
            }
        }

        void Add(Vector3 at, Color c, float a)
        {
            a = Mathf.Clamp01(a);
            if (a > 0.05f)
            {
                float r = new Vector2(at.x - from.x, at.z - from.z).magnitude;
                if (r > WidestM) WidestM = r;
                if (a > PeakA) PeakA = a;
            }
            var lc = (c * lightK).linear;
            V.Add(at); C.Add(new Color32((byte)(Mathf.Clamp01(lc.r) * 255f), (byte)(Mathf.Clamp01(lc.g) * 255f), (byte)(Mathf.Clamp01(lc.b) * 255f), (byte)(a * 255f)));
        }

        static void Strip(int first, int cols, int rows)
        {
            for (int i = 0; i < cols - 1; i++)
                for (int r = 0; r < rows - 1; r++)
                {
                    int a = first + i * rows + r, b = a + rows;
                    T.Add(a); T.Add(b); T.Add(a + 1); T.Add(a + 1); T.Add(b); T.Add(b + 1);
                }
        }

        void Draw(int idx)
        {
            const float Lift = 0.035f;                                                                       // just over the boiling patch (water + 0.03)
            var e = es[idx];
            var dir = e.dir; var rgt = Vector3.Cross(Vector3.up, dir);
            float t = e.clock + idx * 1.7f, g = 1f + 0.25f * e.gushK;
            from = e.impact;
            var c = e.impact + dir * Ahead; c.y = e.impact.y + Lift;

            // 1 the soft foam apron round the landing place: longer on the water side, a slowly changing edge
            const int N = 22;
            {
                float A = ApronA * (0.86f + 0.14f * Mathf.Sin(t * 0.8f)) * Mathf.Min(g, 1.15f);
                int mid = V.Count; Add(c, cFoam, A * 0.9f);
                int first = V.Count;
                for (int i = 0; i <= N; i++)
                {
                    int k = i % N;                                                                           // the seam closes on itself
                    float th = k * (Mathf.PI * 2f / N), cs = Mathf.Cos(th), sn = Mathf.Sin(th);
                    var d = rgt * cs + dir * sn;
                    float ahead = Mathf.Max(0f, sn), back = Mathf.Max(0f, -sn);
                    float R = ApronSide + (ApronAhead - ApronSide) * ahead * ahead - (ApronSide - ApronBack) * back * back;
                    R *= (1f + 0.10f * Mathf.Sin(k * 2.3f + t * 0.9f) * Mathf.Sin(k * 0.7f - t * 0.6f) + 0.05f * Mathf.Sin(k * 1.3f + t * 1.7f)) * (0.95f + 0.05f * g);
                    float n = 0.74f + 0.26f * Mathf.Sin(k * 1.9f - t * 1.1f) * Mathf.Sin(k * 0.6f + t * 0.5f);
                    Add(c + d * (R * 0.42f), cFoam, A * n);
                    Add(c + d * (R * 0.74f), cFoam, A * 0.42f * n * n);
                    Add(c + d * R, cFoam, 0f);
                }
                for (int i = 0; i < N; i++) { T.Add(mid); T.Add(first + (i + 1) * 3); T.Add(first + i * 3); }
                Strip(first, N + 1, 3);
            }

            // 2 foam flecks: they leave the landing place, drift out with the flow, slow down, spread and melt away
            int flecks = Low ? 3 : FleckN;
            for (int j = 0; j < flecks; j++)
            {
                float ph = t / FleckLife + (float)j / flecks + H(j * 13 + idx * 5) * 0.11f;
                int cyc = Mathf.FloorToInt(ph); float u = ph - cyc;
                float lane = H(j * 17 + cyc * 31 + idx * 7) * 2f - 1f;                                        // a new lane every time round
                float run = 1f - (1f - u) * (1f - u);                                                        // quick at first, then it slows down
                var at = c + dir * (FleckStart + FleckDrift * run) + rgt * (lane * (0.16f + 0.40f * run) + 0.03f * Mathf.Sin(t * 1.3f + j * 2.1f));
                float r = Mathf.Lerp(FleckR0, FleckR1, u) * (0.85f + 0.3f * H(j * 7 + cyc * 3 + idx)) * Mathf.Min(g, 1.15f);
                float a = FleckA * Mathf.SmoothStep(0f, 1f, u / 0.14f) * Mathf.Pow(1f - u, 1.5f);
                if (a < 0.01f) continue;
                const int M = 9;
                int mid = V.Count; Add(at, cFoam, a);
                int first = V.Count;
                for (int i = 0; i <= M; i++)
                {
                    int k = i % M;
                    float th = k * (Mathf.PI * 2f / M) + j;
                    float rr = r * (0.82f + 0.18f * Mathf.Sin(k * 2.7f + j * 1.3f + cyc));
                    Add(at + rgt * (Mathf.Cos(th) * rr) + dir * (Mathf.Sin(th) * rr * 1.3f), cFoam, 0f);       // a little longer along the flow
                }
                for (int i = 0; i < M; i++) { T.Add(mid); T.Add(first + i + 1); T.Add(first + i); }
            }

            // 3 small soft ripple arcs on the water side only (never a closed circle), gone about 1 m out
            for (int j = 0; j < (Low ? 1 : 2); j++)
            {
                float u = Mathf.Repeat(t / RipPeriod + 0.5f * j, 1f);
                float a = RipA * (1f - u) * (1f - u) * Mathf.SmoothStep(0f, 1f, u / 0.12f) * Mathf.Min(g, 1.15f);
                if (a < 0.01f) continue;
                float r = Mathf.Lerp(RipR0, RipR1, u), h = Mathf.Lerp(RipW0, RipW1, u);
                int first = V.Count, cols = 0;
                const int S = 18;
                for (int i = 0; i <= S; i++)
                {
                    float th = Mathf.Lerp(-0.30f, Mathf.PI + 0.30f, (float)i / S), cs = Mathf.Cos(th), sn = Mathf.Sin(th);
                    var d = rgt * cs + dir * sn;
                    float ends = Mathf.SmoothStep(0f, 1f, (sn + 0.30f) / 0.55f);                             // fades out towards the wall on both sides
                    float br = 0.76f + 0.24f * Mathf.Sin(i * 2.1f + (u + j) * 6.28f) * Mathf.Sin(i * 0.9f - t * 0.7f);
                    Add(c + d * (r - h), cRip, 0f); Add(c + d * r, cRip, a * br * ends); Add(c + d * (r + h), cRip, 0f);
                    cols++;
                }
                Strip(first, cols, 3);
            }
        }

        public static string Status() =>
            $"outfall foam {(On ? "on" : "off")}: outfalls drawn now {Drawn}, fed {Fed}, builds {Builds}, old rings held {OldRingsHeld}; "
            + $"farthest visible point {WidestM:0.00} m from a landing point (the old ring grew to 2.8-4.3 m), strongest opacity {PeakA:0.00} (the old ring 0.75)";
    }
}
