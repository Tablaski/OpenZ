// OpenZ (GPL-3.0)
// Later the same day, after two rejected tries.
// The map and its rim (the first GroundMargin metres outside it) stand on a plateau; beyond the rim the level had only the flat ground
// skirt far below and thousands of trees with no ground under them, and the canyons that run off the map ended in a cut face. Now:
//  - the ground goes on beyond the rim at the rim's own heights, as a coarser height grid (Cell metres): gentle rolls, a few swamp
//    pools (the level's own swamp water), and a canyon that runs off the map keeps its floor for a while and then rises gently and fades
//    out, so nothing ends in a cut;
//  - it is drawn with the rim floor's own material (the jungle floor the player sees round the map, laid by world position), so the
//    slow tone drift farther out so its tiles do not show;
//  - about two in five tree cells beyond the rim stay as groves, standing on that ground (the ground also rises under any landmark
//    part whose foot stands higher); the others are not drawn (fewer trees, fewer triangles). Round 5 (OzL16Outskirts.Trees.cs): the
//    level's big far jungle beyond the rim's edge is not drawn any more; the groves hold the playable map's own trees at their own size.
//    Round 5b (OzL16Outskirts.FarLook.cs): far away (the widest zoom) they and the rim ring's own trees draw a far tree shape made from
//    their own near shape, inside the cells' own far meshes. Round 5d (OzL16Outskirts.RimFix.cs): none of those trees is drawn any
//    more; outside the map stand only the map's own full-model plants at their own size (every crown at most 0.35 of a factory), at
//    every zoom as they are, as close together as the map's own edge jungle allows within round 4's cost. Round 5e
//    (OzL16Outskirts.RimEdge.cs): the map's edge plants on its cliff tops and rims are drawn as such full models too, and the start
//    flyover and the endings show the same;
//  - beyond Reach metres a flat plain at the land's mean height runs out to the haze;
//  - the widest zoom on this level is MaxHeight (the other levels keep RtsCamera.ZoomWidest);
//  - the far pyramid at half its size, the land under each landmark's whole foot, the flume ends' rocks at the map's own sizes and
//    seated (OzL16Outskirts.Props.cs);
//  - the landscape beyond the rim: relief, the far land, ground colours, the map's own small plants, groves and rocks, drawn instanced
//    (OzL16Outskirts.Landscape.cs); made bolder: knolls, hills, swamps, forest patches, reeds, thickets, stronger ground colours
//    (OzL16Outskirts.Bolder.cs).
// The land is made a few milliseconds per frame (during the start flyover, which hides all of this anyway; on a blue start all of it
// in the level's first frame, before its first picture: OzStartAhead.cs, owner items 86 / 142); it appears in one step
// when it is ready. While the flyover's low-shot look is on (OzL16FlyoverFlora.LowLookOn: its own far land) all of this stands
// back, so the flyover is exactly as before. Inside the map nothing changes. Drawn only: no rule reads it. Level 16 only.
// Play check: return OpenZ.OzL16Outskirts.Status();
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    [DefaultExecutionOrder(220)]
    public sealed partial class OzL16Outskirts : MonoBehaviour
    {
        public const string Level = "L16", FloraRoot = "L16_Flora";
        public static OzL16Outskirts I { get; private set; }
        /// <summary>Off = the level as in 1.1 (for a before / after count); set before the level starts.</summary>
        public static bool On = true;
        /// <summary>The widest camera height on this level (m; RtsCamera.ZoomWidest = 200 elsewhere).</summary>
        public static float MaxHeight = 165f;
        /// <summary></summary>
        public static float NorthCut = 30f, SouthGive = 12f, SideCut = 28f;
        /// <summary>The land beyond the rim: its cell (m), its reach beyond the rim's edge (m), how far it starts under the rim (m).</summary>
        public static float Cell = 4f, Reach = 250f, Inset = 6f;
        /// <summary>Where the rim's own heights give way to the land's rolls: the length varies round the plateau between these (m).</summary>
        public static float FadeShort = 45f, FadeLong = 130f;
        /// <summary>The rolls of the land (m) and the swamp pools: how deep a pool's bed dips (m) and the water's depth under the mean land.</summary>
        public static float Relief = 1.3f, PoolDip = 2.6f, PoolBelow = 0.9f;
        /// <summary>The lowland's mean height beyond the rim (m): where the level's own landmarks out there stand (their feet at about 0).</summary>
        public static float LandY = 0.5f;
        /// <summary>The rim floor's texture tile (m: its uv is the world position over this, as on RIM2_Floor) and the slow tone drift
        /// over the wide land (a share of the brightness; none at the rim's edge).</summary>
        public static float RimTile = 16f, ToneDrift = 0.07f;
        /// <summary>
        /// rim floor's texture (tints of its colour: deep moss, dry grass, bare earth), none at the rim's edge; and the small plants'
        /// share kept beyond the rim (more ground cover than the groves' trees).</summary>
        public static Color PatchMoss = new Color(0.74f, 0.93f, 0.70f), PatchDry = new Color(1.13f, 1.06f, 0.76f), PatchEarth = new Color(0.86f, 0.74f, 0.60f);
        public static int PlantPct = 70;
        /// <summary>Round 4 : a mist that hugs the land beyond the
        /// rim: its height over the ground (m), its strength far out (alpha), where it starts and where it is full (m beyond the rim's edge).
        /// Nothing of it lies over the playable map.</summary>
        public static float MistOver = 1.4f, MistMax = 0.5f, MistFrom = 10f, MistFull = 170f;
        public static Color MistTone = new Color(0.70f, 0.76f, 0.72f);
        /// <summary>Round 4 : the ends of the rim's
        /// waterfall beds beyond the rim get the level's own rock formations, and the tree cells this near them (m) stay.</summary>
        public static float FlumeKeep = 40f, FlumeDip = 2.5f;
        /// <summary>The two raised waterfall flumes of the north side end at the plateau's edge here (x, z; measured in Play: the flume
        /// mesh cannot be read in the game and their dip in the rim's heights is too shallow to find them reliably).</summary>
        static readonly Vector2[] KnownFlumes = { new Vector2(49f, 17f), new Vector2(177f, 23f) };
        public static int FlumeEnds, FlumeRocks;
        /// <summary>Groves: the share of the tree cells beyond the rim kept (per cent), and none farther out than this (m).</summary>
        public static int GrovePct = 40;
        public static float GroveOut = 240f;
        /// <summary>Milliseconds per frame for making the pictures: during the flyover, when the level starts without one, and on a start as
        /// moment later instead, in one step as before; a red start is not changed).</summary>
        public static float SliceMs = 6f, SliceMsNoFlyover = 40f, SliceMsBlue = 6f;
        /// <summary>Counters: cells not drawn, groves kept, rim samples that found ground, triangles, water cells, raised feet, build time (ms
        /// of work, frames it took).</summary>
        public static int CellsHidden, Groves, EdgeSamples, EdgeHits, Triangles, WaterCells, Rises, BuildFrames;
        public static float BuildMs;

        GameObject groundGo, waterGo, farGo, mistGo; Material groundMat, mistMat; float mistNight = -1f;
        readonly List<(Vector3 at, Vector3 o)> flumeEnds = new List<(Vector3, Vector3)>(); Vector2 map; RtsCamera rts; bool built, standBack;
        float px0, px1, pz0, pz1;                                      // the plateau: the map and its rim
        float gx0, gz0; int gnx, gnz; float[] gh;                      // the land's grid: origin, points a side, heights
        float[] edge; float[] edgeSum; float perim; float meanY, waterY;
        readonly Dictionary<long, List<Vector4>> rises = new Dictionary<long, List<Vector4>>();   // ground under plants and landmarks: x, z, top, radius
        readonly List<GameObject> toHide = new List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; On = true; CellsHidden = Groves = EdgeSamples = EdgeHits = Triangles = WaterCells = Rises = BuildFrames = 0; BuildMs = 0f; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterFirstScene() { TryAttach(SceneManager.GetActiveScene()); }

        static void OnScene(Scene s, LoadSceneMode m) { if (m == LoadSceneMode.Single) TryAttach(s); }

        static void TryAttach(Scene s)
        {
            if (!Application.isPlaying || I || !On) return;
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!g || g.level != Level) return;
            var go = new GameObject("OzL16Outskirts");
            if (s.IsValid() && go.scene != s) SceneManager.MoveGameObjectToScene(go, s);
            go.AddComponent<OzL16Outskirts>();
        }

        void Awake() { I = this; }
        void OnDestroy() { if (I == this) I = null; }

        void Start()
        {
            CellsHidden = Groves = EdgeSamples = EdgeHits = Triangles = WaterCells = Rises = BuildFrames = FlumeEnds = FlumeRocks = 0; BuildMs = 0f;   // per level instance
            PropsEarly();                                                                          // the far pyramid at half its size, before anything reads its box (OzL16Outskirts.Props.cs)
            rts = FindFirstObjectByType<RtsCamera>();
            map = rts ? rts.mapSizeMeters : new Vector2(208f, 256f);
            float m = OzL16FlyoverFlora.GroundMargin;
            px0 = -m; px1 = map.x + m; pz0 = -map.y - m; pz1 = m;
            if (rts) { rts.maxHeight = Mathf.Min(rts.maxHeight, MaxHeight); rts.height = Mathf.Min(rts.height, rts.maxHeight); rts.northCut = NorthCut; rts.southGive = SouthGive; rts.sideCut = SideCut; }
            OzStartAhead.Run(this, BuildSliced(), "the land beyond the rim");                     // a blue start: all of it before the first picture (OzStartAhead.cs, owner items 86 / 142)
        }

        void LateUpdate()
        {
            RimEarlyShow();                                                                      // round 5e: before the land is built (OzL16Outskirts.RimFix.cs)
            if (!built) return;
            if (mistMat && Mathf.Abs(NightGrade.NightK - mistNight) > 0.01f)
            {   // the mist is unlit: it follows the day-night light itself (a dark, thin haze at night)
                mistNight = NightGrade.NightK;
                var c = Color.Lerp(MistTone, MistTone * 0.22f, mistNight); c.a = Mathf.Lerp(1f, 0.6f, mistNight);
                mistMat.SetColor("_Color", c);
            }
            RimFixBoxes();                                               // round 5d: the new plants' drawing boxes (OzL16Outskirts.RimFix.cs)
            LandscapeDraw();                                             // the landscape's instanced plants and rocks (OzL16Outskirts.Landscape.cs)
            bool back = OzL16FlyoverFlora.LowLookOn;                     // the flyover's own far land and plant rules hold the low shots
            if (back == standBack) return;
            standBack = back;
            if (groundGo) groundGo.SetActive(!back);
            if (waterGo) waterGo.SetActive(!back);
            if (farGo) farGo.SetActive(!back);
            if (mistGo) mistGo.SetActive(!back);
            LowRocks(!back);                                                                     // the flume rocks stand back with their land (OzL16Outskirts.LowRocks.cs)
            ShowTrees(!back);                                                                    // round 5 (OzL16Outskirts.Trees.cs)
            RimFixShow(!back);                                                                   // round 5d (OzL16Outskirts.RimFix.cs)
            LandscapeShow(!back);                                                                // the landscape (OzL16Outskirts.Landscape.cs)
            JungleShow(!back);                                                                   // round 6 (OzL16Outskirts.Jungle.cs)
        }

        float Outside(Vector3 p) => Mathf.Max(Mathf.Max(-p.x, p.x - map.x), Mathf.Max(p.z, -map.y - p.z), 0f);

        static int Hash(int x, int z) { unchecked { uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663); h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15; return (int)(h % 100u); } }

        // ------------------------------------------------------------------ the plant cells beyond the rim
        /// <summary>A cell's own 32 m grid square from its name (T_cx_cz / P_cx_cz).</summary>
        static bool Square(string name, out int cx, out int cz)
        {
            cx = cz = 0; var p = name.Split('_');
            return p.Length == 3 && int.TryParse(p[1], out cx) && int.TryParse(p[2], out cz);
        }

        /// <summary>Which plant cells stay (the rim's own, and the groves), and the ground under the plants that stay beyond the rim's edge.
        /// The cells that go are only collected here: they are switched off when the land is shown.</summary>
        void ChooseCells()
        {
            var root = GameObject.Find(FloraRoot); if (!root) { Debug.LogWarning("[OzL16Outskirts] no " + FloraRoot); return; }
            float m = OzL16FlyoverFlora.GroundMargin;
            foreach (Transform cell in root.transform)
            {
                var rs = cell.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) continue;
                Bounds b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                float outer, inner; int cx, cz;
                if (Square(cell.name, out cx, out cz))
                {
                    float x0 = cx * 32f, x1 = x0 + 32f, z0 = cz * 32f, z1 = z0 + 32f;
                    outer = Mathf.Max(Mathf.Max(-x0, x1 - map.x), Mathf.Max(z1, -map.y - z0), 0f);
                    inner = Mathf.Max(Mathf.Max(-x1, x0 - map.x), Mathf.Max(z0, -map.y - z1), 0f);
                }
                else { outer = inner = Outside(b.center); cx = Mathf.FloorToInt(b.center.x / 32f); cz = Mathf.FloorToInt(b.center.z / 32f); }
                bool small = cell.name.StartsWith("P");
                bool nearFlume = false; foreach (var f in flumeEnds) if (new Vector2(b.center.x - f.at.x, b.center.z - f.at.z).sqrMagnitude < FlumeKeep * FlumeKeep) { nearFlume = true; break; }
                bool keep = outer <= m + 8.01f || nearFlume || (inner <= GroveOut && Hash(cx, cz) < (small ? PlantPct : GrovePct));    // the rim's cells, and the groves (trees and small plants of the same squares)
                if (!keep) { toHide.Add(cell.gameObject); continue; }
                if (Slot(cell, rs)) continue;                                                    // round 5: its plants beyond the rim's edge go, the map's own trees stand there (OzL16Outskirts.Trees.cs)
                if (outer > m + 8.01f && cell.name.StartsWith("T")) Groves++;
                foreach (var r in rs)
                {
                    if (r.name == "FAR") continue;                                               // the cell's joined far mesh: its plants are the near ones
                    var pb = r.bounds; if (Outside(pb.center) <= m + 0.5f) continue;
                    AddRise(pb.center.x, pb.center.z, pb.min.y - 0.12f, Mathf.Clamp(Mathf.Max(pb.extents.x, pb.extents.z) * 0.3f, 1.2f, 3f));
                }
            }
        }

        /// <summary>The level's own landmarks beyond the rim (L16_Structures SUR_*): ground under each part's foot.</summary>
        void LandmarkRises()
        {
            var st = GameObject.Find("L16_Structures"); if (!st) return;
            float m = OzL16FlyoverFlora.GroundMargin;
            foreach (var r in st.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.transform.parent || !r.transform.parent.name.StartsWith("SUR_") || r.name == "LOD2") continue;   // one level of detail is enough
                var b = r.bounds; if (Outside(b.center) > m + 0.5f) Mark(b);                          // round 5: the trees keep off it (OzL16Outskirts.Trees.cs)
                if (Outside(b.center) <= m + 0.5f || b.min.y < waterY + 0.1f) continue;          // deep columns stand in the water already
                AddRise(b.center.x, b.center.z, b.min.y - 0.05f, Mathf.Max(b.extents.x, b.extents.z) * 0.9f);
            }
        }

        const float RiseSlope = 0.32f;                                   // the ground falls away from a raised foot (m per m)
        static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        float riseCap = float.MaxValue;

        /// <summary>Ground under a foot at (x, z), its top just under the foot. A tall tree's crown is its own mesh high in the air (its
        /// lowest point 15 to 31 m up): that is no foot, so nothing above the rim's own top (riseCap) raises the ground (round 3 built a
        /// 68 m ramp under the crowns north of the map).</summary>
        void AddRise(float x, float z, float top, float radius)
        {
            if (top > riseCap) return;
            float reach = radius + Mathf.Max(0f, top - (waterY - 2.5f)) / RiseSlope;
            var v = new Vector4(x, z, top, radius); Rises++;
            int a0 = Mathf.FloorToInt((x - reach) / 16f), a1 = Mathf.FloorToInt((x + reach) / 16f), b0 = Mathf.FloorToInt((z - reach) / 16f), b1 = Mathf.FloorToInt((z + reach) / 16f);
            for (int a = a0; a <= a1; a++)
                for (int b = b0; b <= b1; b++)
                {
                    long k = Key(a, b);
                    if (!rises.TryGetValue(k, out var l)) rises[k] = l = new List<Vector4>();
                    l.Add(v);
                }
        }

        float RiseAt(float x, float z, float h)
        {
            if (!rises.TryGetValue(Key(Mathf.FloorToInt(x / 16f), Mathf.FloorToInt(z / 16f)), out var l)) return h;
            foreach (var r in l)
            {
                float dx = x - r.x, dz = z - r.y, d = Mathf.Sqrt(dx * dx + dz * dz);
                float top = r.z - RiseSlope * Mathf.Max(0f, d - r.w);
                if (top > h) h = top;
            }
            return h;
        }

        // ------------------------------------------------------------------ the rim's edge
        static readonly RaycastHit[] hits = new RaycastHit[32];

        /// <summary>The level's ground (its COL_Terrain colliders) under x, z: the highest hit.</summary>
        static bool GroundAt(float x, float z, out float y)
        {
            y = 0f; bool any = false;
            int k = Physics.RaycastNonAlloc(new Vector3(x, 400f, z), Vector3.down, hits, 800f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < k; i++)
                if (hits[i].collider && hits[i].collider.name.StartsWith("COL_Terrain") && (!any || hits[i].point.y > y)) { y = hits[i].point.y; any = true; }
            return any;
        }

        /// <summary>A point on the plateau's edge from its distance s along it (clockwise from the north-west corner) and its outward side.</summary>
        void EdgePoint(float s, out float x, out float z, out float ox, out float oz)
        {
            float w = px1 - px0, d = pz1 - pz0; s = Mathf.Repeat(s, perim);
            if (s < w) { x = px0 + s; z = pz1; ox = 0f; oz = 1f; return; }
            s -= w; if (s < d) { x = px1; z = pz1 - s; ox = 1f; oz = 0f; return; }
            s -= d; if (s < w) { x = px1 - s; z = pz0; ox = 0f; oz = -1f; return; }
            s -= w; x = px0; z = pz0 + s; ox = -1f; oz = 0f;
        }

        /// <summary>Distance along the plateau's edge of the edge point nearest to (x, z), and how far outside the plateau (x, z) is.</summary>
        float EdgeS(float x, float z, out float d)
        {
            float w = px1 - px0, dp = pz1 - pz0;
            float cx = Mathf.Clamp(x, px0, px1), cz = Mathf.Clamp(z, pz0, pz1);
            d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
            if (d <= 0f)
            {   // inside the plateau (the band under the rim): the nearest side
                float dn = pz1 - z, de = px1 - x, ds = z - pz0, dw = x - px0, mn = Mathf.Min(Mathf.Min(dn, de), Mathf.Min(ds, dw));
                if (mn == dn) { cz = pz1; } else if (mn == de) { cx = px1; } else if (mn == ds) { cz = pz0; } else { cx = px0; }
            }
            if (cz >= pz1 - 1e-4f) return cx - px0;
            if (cx >= px1 - 1e-4f) return w + (pz1 - cz);
            if (cz <= pz0 + 1e-4f) return w + dp + (px1 - cx);
            return w + dp + w + (cz - pz0);
        }

        void SampleEdge()
        {
            float w = px1 - px0, d = pz1 - pz0; perim = 2f * (w + d);
            int n = Mathf.CeilToInt(perim / 2f); edge = new float[n]; edgeSum = new float[n + 1];
            for (int i = 0; i < n; i++)
            {
                EdgeSamples++;
                EdgePoint(i * 2f, out float x, out float z, out float ox, out float oz);
                float y = 0f; bool found = false;
                for (float k = 0f; k <= Inset && !found; k += 0.5f)                               // the rim's top at the edge, else just inside it
                    found = GroundAt(x - ox * k, z - oz * k, out y);
                if (found) EdgeHits++;
                edge[i] = found ? y : float.NaN;
            }
            for (int i = 0; i < n; i++) if (float.IsNaN(edge[i])) { float a = 0f; int c = 0; for (int k = 1; k < n && c == 0; k++) { if (!float.IsNaN(edge[(i + k) % n])) { a += edge[(i + k) % n]; c++; } if (!float.IsNaN(edge[(i - k + n) % n])) { a += edge[(i - k + n) % n]; c++; } } edge[i] = c > 0 ? a / c : 0f; }
            for (int i = 0; i < n; i++) edgeSum[i + 1] = edgeSum[i] + edge[i];
        }

        /// <summary>The rim's edge height at s, and the mean over +-half metres round it (the slope widens a canyon's mouth).</summary>
        float EdgeAt(float s, float half)
        {
            int n = edge.Length; float f = s / 2f; int i0 = Mathf.FloorToInt(f); float t = f - i0;
            float h = Mathf.Lerp(edge[((i0 % n) + n) % n], edge[(((i0 + 1) % n) + n) % n], t);
            int k = Mathf.FloorToInt(half / 2f); if (k <= 0) return h;
            k = Mathf.Min(k, n / 2 - 1);
            int a = i0 - k, b = i0 + k; float sum;
            if (a >= 0 && b < n) sum = edgeSum[b + 1] - edgeSum[a];
            else { sum = 0f; for (int j = a; j <= b; j++) sum += edge[((j % n) + n) % n]; }
            return Mathf.Min(h, sum / (b - a + 1));                                               // only ever lower: no lip at a canyon's mouth
        }

        // ------------------------------------------------------------------ the land
        static float N(float x, float z, float scale, float ox, float oz) => Mathf.PerlinNoise(x / scale + ox, z / scale + oz) * 2f - 1f;

        /// <summary>Where the rim's own heights give way to the land at s along the rim's edge: two slow waves round the plateau, so some
        /// </summary>
        float FadeLength(float s)
        {
            float a = Mathf.PerlinNoise(s / 70f + 3.7f, 0.31f), b = Mathf.PerlinNoise(s / 23f + 9.2f, 0.77f);
            float t = Mathf.Clamp01(0.75f * a + 0.25f * b);
            return Mathf.Lerp(FadeShort, FadeLong, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.75f, t)));
        }

        /// <summary>The land away from the rim: the lowland where the level's own landmarks stand (LandY), gentle rolls, a few pools.</summary>
        static float Land(float x, float z, float fade)
        {
            float roll = Relief * (N(x, z, 85f, 13.1f, 7.7f) + 0.35f * N(x, z, 26f, 3.3f, 21.9f));
            float dip = PoolDip * Mathf.Max(0f, N(x, z, 47f, 31.7f, 5.3f) - 0.28f) / 0.72f;
            return LandY + fade * (roll - dip);
        }

        float HeightAt(float x, float z)
        {
            float s = EdgeS(x, z, out float d);
            if (d <= 0f) return EdgeAt(s, 0f) - 0.25f;                                             // under the rim's edge: the rim's own ground covers it
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Reach - 70f, Reach - 10f, d));
            float k = Mathf.SmoothStep(0f, 1f, d * Mathf.Max(0.5f, 1f + 0.2f * N(x, z, 41f, 2.9f, 8.3f)) / FadeLength(s));
            float h = Bolder(x, z, d, Mathf.Lerp(EdgeAt(s, d * 0.5f), LandAt(x, z, d, fade, Mathf.SmoothStep(0f, 1f, k)), k) - 0.25f * (1f - Mathf.Clamp01(d / 4f)));   // LandAt: OzL16Outskirts.Landscape.cs; Bolder: OzL16Outskirts.Bolder.cs
            return RiseAt(x, z, h);
        }

        /// <summary>The land's grid height at any point (bilinear), the plain's beyond it.</summary>
        float GridAt(float x, float z)
        {
            float fi = (x - gx0) / Cell, fj = (z - gz0) / Cell;
            if (fi < 0f || fj < 0f || fi > gnx - 1 || fj > gnz - 1) return FarY(x, z);                                     // the far land (LandY with the landscape off)
            int i = Mathf.Min((int)fi, gnx - 2), j = Mathf.Min((int)fj, gnz - 2); float a = fi - i, b = fj - j;
            return Mathf.Lerp(Mathf.Lerp(gh[j * gnx + i], gh[j * gnx + i + 1], a), Mathf.Lerp(gh[(j + 1) * gnx + i], gh[(j + 1) * gnx + i + 1], a), b);
        }

        // ------------------------------------------------------------------ build, a few milliseconds per frame
        IEnumerator BuildSliced()
        {
            float t0 = Time.realtimeSinceStartup, work = 0f;
            bool Slice()
            {
                float now = Time.realtimeSinceStartup, ms = (OzL16FlyoverFlora.LowLookOn ? SliceMs : OzPlayBlue.Active ? SliceMsBlue : SliceMsNoFlyover) / 1000f;   // a blue start: gentle slices (item 86)
                if (now - t0 < ms) return false;
                work += now - t0; BuildFrames++; t0 = float.MaxValue;                              // t0 is set again when the frame resumes
                return true;
            }

            var tg = GameObject.Find("TER_Ground"); var gr = tg ? tg.GetComponent<Renderer>() : null;
            var tw = GameObject.Find("WAT_Water"); var wr = tw ? tw.GetComponent<Renderer>() : null;
            if (!gr || !gr.sharedMaterial) { Debug.LogWarning("[OzL16Outskirts] the level's ground (TER_Ground) was not found"); yield break; }
            waterY = LandY - PoolBelow;
            SampleEdge();                                                                          // first: the rim's top caps the raised feet below
            float top = float.MinValue; foreach (var e in edge) top = Mathf.Max(top, e); riseCap = top + 1.5f;
            FindFlumeEnds();
            ChooseCells();
            LandmarkRises();
            PropsRises();                                                                          // the land under each landmark part's whole foot (OzL16Outskirts.Props.cs)
            RimEarly();                                                                            // round 5e: the plants a low shot shows, before the first frame (OzL16Outskirts.RimFix.cs)
            if (Slice()) { yield return null; t0 = Time.realtimeSinceStartup; }

            // the land's grid
            gx0 = px0 - Reach; gz0 = pz0 - Reach; gnx = Mathf.CeilToInt((px1 - px0 + 2f * Reach) / Cell) + 1; gnz = Mathf.CeilToInt((pz1 - pz0 + 2f * Reach) / Cell) + 1;
            gh = new float[gnx * gnz];
            for (int j = 0; j < gnz; j++)
            {
                for (int i = 0; i < gnx; i++) gh[j * gnx + i] = HeightAt(gx0 + i * Cell, gz0 + j * Cell);
                if (Slice()) { yield return null; t0 = Time.realtimeSinceStartup; }
            }

            // the rim floor's own material (the jungle floor the player sees round the map: rim_floor_4096 laid by world position, one
            // tile every RimTile metres), so the texture runs on across the rim's edge without a seam; a slow tone drift in the vertex
            // colours (none at the edge) keeps the tiles from showing over the wide land
            var rf = GameObject.Find("RIM2_Floor"); var rr = rf ? rf.GetComponent<Renderer>() : null;
            if (!rr || !rr.sharedMaterial) { Debug.LogWarning("[OzL16Outskirts] the rim floor (RIM2_Floor) was not found"); yield break; }
            groundMat = new Material(rr.sharedMaterial) { name = "L16Outskirts ground (runtime)" };
            bool tint = groundMat.HasProperty("_UseVertexColor");
            if (tint) groundMat.SetFloat("_UseVertexColor", 1f);
            Vector2 mid = new Vector2(map.x * 0.5f, -map.y * 0.5f);

            // the meshes
            float ix0 = px0 + Inset, ix1 = px1 - Inset, iz0 = pz0 + Inset, iz1 = pz1 - Inset;     // cells wholly inside this are the plateau's own
            var v = new Vector3[gnx * gnz]; var nrm = new Vector3[v.Length];
            for (int j = 0; j < gnz; j++)
                for (int i = 0; i < gnx; i++)
                {
                    int k = j * gnx + i;
                    v[k] = new Vector3(gx0 + i * Cell, gh[k], gz0 + j * Cell);
                    float hl = gh[j * gnx + Mathf.Max(i - 1, 0)], hr = gh[j * gnx + Mathf.Min(i + 1, gnx - 1)], hd = gh[Mathf.Max(j - 1, 0) * gnx + i], hu = gh[Mathf.Min(j + 1, gnz - 1) * gnx + i];
                    nrm[k] = new Vector3(hl - hr, 2f * Cell, hd - hu).normalized;
                }
            var tri = new List<int>(gnx * gnz * 6); var wv = new List<Vector3>(); var wt = new List<int>();
            for (int j = 0; j + 1 < gnz; j++)
                for (int i = 0; i + 1 < gnx; i++)
                {
                    float x0 = gx0 + i * Cell, z0 = gz0 + j * Cell;
                    if (x0 >= ix0 && x0 + Cell <= ix1 && z0 >= iz0 && z0 + Cell <= iz1) continue;
                    int a = j * gnx + i, b = a + 1, c = a + gnx + 1, d = a + gnx;
                    tri.Add(a); tri.Add(d); tri.Add(c); tri.Add(a); tri.Add(c); tri.Add(b);                // clockwise from above
                    float lo = Mathf.Min(Mathf.Min(gh[a], gh[b]), Mathf.Min(gh[c], gh[d]));
                    if (lo < waterY + 0.02f && Outside(new Vector3(x0 + Cell * 0.5f, 0f, z0 + Cell * 0.5f)) > OzL16FlyoverFlora.GroundMargin + 8f)
                    {
                        int w0 = wv.Count; WaterCells++;
                        wv.Add(new Vector3(x0, waterY, z0)); wv.Add(new Vector3(x0 + Cell, waterY, z0)); wv.Add(new Vector3(x0 + Cell, waterY, z0 + Cell)); wv.Add(new Vector3(x0, waterY, z0 + Cell));
                        wt.Add(w0); wt.Add(w0 + 3); wt.Add(w0 + 2); wt.Add(w0); wt.Add(w0 + 2); wt.Add(w0 + 1);
                    }
                }
            var fv = new List<Vector3>(); var ft = new List<int>();
            float fx1 = gx0 + (gnx - 1) * Cell, fz1 = gz0 + (gnz - 1) * Cell, far = 1150f;
            var xs = Lines(mid.x - far, gx0, fx1, mid.x + far); var zs = Lines(mid.y - far, gz0, fz1, mid.y + far);
            for (int i = 0; i + 1 < xs.Count; i++)
                for (int j = 0; j + 1 < zs.Count; j++)
                {
                    float cx = (xs[i] + xs[i + 1]) * 0.5f, cz = (zs[j] + zs[j + 1]) * 0.5f;
                    if (cx > gx0 && cx < fx1 && cz > gz0 && cz < fz1) continue;                       // the land's grid
                    int b = fv.Count;
                    fv.Add(new Vector3(xs[i], LandY, zs[j])); fv.Add(new Vector3(xs[i + 1], LandY, zs[j])); fv.Add(new Vector3(xs[i + 1], LandY, zs[j + 1])); fv.Add(new Vector3(xs[i], LandY, zs[j + 1]));
                    ft.Add(b); ft.Add(b + 3); ft.Add(b + 2); ft.Add(b); ft.Add(b + 2); ft.Add(b + 1);
                }
            Color[] vc = null;
            if (tint)
            {
                vc = new Color[v.Length];
                for (int k = 0; k < v.Length; k++)
                {
                    EdgeS(v[k].x, v[k].z, out float dd);
                    float x = v[k].x, z = v[k].z, inF = Mathf.SmoothStep(0f, 1f, (dd - 4f) / 30f);
                    float g = 1f + inF * (ToneDrift * N(x, z, 90f, 6.6f, 2.4f) + 0.5f * ToneDrift * N(x, z, 23f, 1.9f, 8.8f));
                    Color c = new Color(g, g, g, 1f);
                    float pm = inF * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.54f, 0.66f, Mathf.PerlinNoise(x / 37f + 2.3f, z / 37f + 9.1f)));
                    float pd = inF * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.68f, Mathf.PerlinNoise(x / 29f + 7.7f, z / 29f + 1.4f)));
                    float pe = inF * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.63f, 0.71f, Mathf.PerlinNoise(x / 17f + 4.9f, z / 17f + 6.2f)));
                    c *= Color.Lerp(Color.white, PatchMoss, pm); c *= Color.Lerp(Color.white, PatchDry, pd * (1f - pm)); c *= Color.Lerp(Color.white, PatchEarth, pe * 0.85f);
                    c = TintBold(Tint55(c, x, v[k].y, z, nrm[k].y, dd), x, v[k].y, z, nrm[k].y, dd);   // mud, earth, dry tops, moss, paths, the far canopy tone (OzL16Outskirts.Landscape.cs); the bolder tints (OzL16Outskirts.Bolder.cs)
                    c.a = 1f; vc[k] = c;
                }
            }
            groundGo = MakeMesh("L16_Outskirts_Ground", v, nrm, tri, groundMat, true, RimTile, vc);
            var fn = new Vector3[fv.Count]; for (int i = 0; i < fn.Length; i++) fn[i] = Vector3.up;
            var farLand = FarLand55(nrm, vc);                                                        // the landscape's far land (OzL16Outskirts.Landscape.cs), else round 4's plain
            farGo = farLand ? farLand : MakeMesh("L16_Outskirts_Plain", fv.ToArray(), fn, ft, groundMat, true, RimTile, null);
            if (wr && wr.sharedMaterial && wv.Count > 0)
            {
                var wn = new Vector3[wv.Count]; for (int i = 0; i < wn.Length; i++) wn[i] = Vector3.up;
                waterGo = MakeMesh("L16_Outskirts_Water", wv.ToArray(), wn, wt, wr.sharedMaterial, false, 1f, null);   // the level's own swamp water
            }
            Triangles = (tri.Count + (farLand ? FarLandTris * 3 : ft.Count) + wt.Count) / 3;
            foreach (bool step in GrowTrees())                                                    // round 5: the map's own trees in the grove cells (OzL16Outskirts.Trees.cs)
                if (step && Slice()) { yield return null; t0 = Time.realtimeSinceStartup; }
            foreach (bool step in RimFixSteps())                                                  // round 5d: the map's own full-model plants outside the map (OzL16Outskirts.RimFix.cs)
                if (step && Slice()) { yield return null; t0 = Time.realtimeSinceStartup; }
            foreach (bool step in LandscapeSteps())                                               // the landscape's rocks, groves and ground cover (OzL16Outskirts.Landscape.cs)
                if (step && Slice()) { yield return null; t0 = Time.realtimeSinceStartup; }
            foreach (bool step in FarLookSteps())                                                 // round 5b: the trees outside the map get their far tree shape (OzL16Outskirts.FarLook.cs)
                if (step && Slice()) { yield return null; t0 = Time.realtimeSinceStartup; }

            // show it all in one step, the trees beyond the rim with it
            BuildMist(v);
            PlaceFlumeRocks();
            PropsRocks();                                                                          // the flume rocks at the map's own sizes, seated (OzL16Outskirts.Props.cs)
            foreach (var go in toHide) if (go) { Hide(go); CellsHidden++; }                       // round 5: its plants off, the cell stays on (OzL16Outskirts.Trees.cs)
            FarLookShow();                                                                         // round 5b: the new far meshes go in with the land (OzL16Outskirts.FarLook.cs)
            ApplyTrees();
            RimFixApply();                                                                         // round 5d: the cells whose new far mesh came are changed (OzL16Outskirts.RimFix.cs)
            LandscapeApply();                                                                      // the landscape's instanced models, round 5e's groves with them (OzL16Outskirts.Landscape.cs)
            SeatJungle();                                                                          // round 6 (run 8): the level's own jungle beyond the rim back, seated on the land (OzL16Outskirts.Jungle.cs)
            standBack = !OzL16FlyoverFlora.LowLookOn; built = true;                                // LateUpdate sets the right state on the next frame
            work += Time.realtimeSinceStartup - Mathf.Min(t0, Time.realtimeSinceStartup); BuildMs = work * 1000f;
            Debug.Log($"[OzL16Outskirts] built in {BuildMs:0} ms of work over {BuildFrames} frames: {Triangles} triangles, {WaterCells} water cells, {CellsHidden} plant cells beyond the rim not drawn, {Groves} groves, {Rises} plants and landmark parts with ground raised under them, rim edge ground found at {EdgeHits}/{EdgeSamples} samples, widest zoom {(rts ? rts.maxHeight : 0f):0} m");
        }

        /// <summary>The mist: the land's grid every other point, MistOver above the ground, its alpha rising with the distance beyond the
        /// rim's edge (none on the plateau, so nothing of it lies over the playable map), drawn with the game's own overlay shader.</summary>
        void BuildMist(Vector3[] land)
        {
            if (FarMist(land)) return;                                                           // it runs on over the far plain: no hard end in any view (OzL16Outskirts.FarMist.cs)
            var sh = Shader.Find("OpenZ/Overlay"); if (!sh) return;
            int sx = (gnx + 1) / 2, sz = (gnz + 1) / 2; var mv = new Vector3[sx * sz]; var mc = new Color[mv.Length]; var mn = new Vector3[mv.Length];
            for (int j = 0; j < sz; j++)
                for (int i = 0; i < sx; i++)
                {
                    int gi = Mathf.Min(i * 2, gnx - 1), gj = Mathf.Min(j * 2, gnz - 1), k = j * sx + i;
                    var p = land[gj * gnx + gi]; EdgeS(p.x, p.z, out float d);
                    mv[k] = p + Vector3.up * MistOver; mn[k] = Vector3.up;
                    float a = MistMax * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(MistFrom, MistFull, d)) * (0.85f + 0.15f * N(p.x, p.z, 60f, 5.5f, 3.3f));
                    mc[k] = new Color(1f, 1f, 1f, d <= 0f ? 0f : a);
                }
            var t = new List<int>();
            for (int j = 0; j + 1 < sz; j++)
                for (int i = 0; i + 1 < sx; i++)
                {
                    int a = j * sx + i, b = a + 1, c = a + sx + 1, dd = a + sx;
                    if (mc[a].a + mc[b].a + mc[c].a + mc[dd].a <= 0f) continue;                        // the plateau: no mist at all
                    t.Add(a); t.Add(dd); t.Add(c); t.Add(a); t.Add(c); t.Add(b);
                }
            mistMat = new Material(sh) { name = "L16Outskirts mist (runtime)" };
            if (mistMat.HasProperty("_Scan")) mistMat.SetFloat("_Scan", 0f);
            mistNight = -1f;
            mistGo = MakeMesh("L16_Outskirts_Mist", mv, mn, t, mistMat, false, 1f, mc);
        }

        /// <summary>Where a waterfall bed or a canyon runs off the plateau: a dip in the rim's own edge heights (sampled every 2 m round the
        /// plateau) at least FlumeDip metres under the edge's mean over +-24 m; an end is the dip's lowest point, just beyond the rim's edge.
        /// (The beds' mesh cannot be read in the game; the falls' water sheets lie far out: round 4's first try put the rocks there.)</summary>
        void FindFlumeEnds()
        {
            flumeEnds.Clear();
            foreach (var kf in KnownFlumes) flumeEnds.Add((new Vector3(kf.x, 0f, kf.y), Vector3.forward));
            int n = edge.Length, w = 12; bool inDip = false; int best = -1;
            for (int i = 0; i <= n; i++)
            {
                int k = i % n; float sum = 0f; for (int q = -w; q <= w; q++) sum += edge[((k + q) % n + n) % n];
                bool dip = i < n && edge[k] < sum / (2 * w + 1) - FlumeDip;
                if (dip) { if (!inDip || edge[k] < edge[best]) best = k; inDip = true; continue; }
                if (inDip && best >= 0)
                {
                    EdgePoint(best * 2f, out float x, out float z, out float ox, out float oz);
                    var at = new Vector3(x + ox * 3f, edge[best], z + oz * 3f);
                    bool dup = false; foreach (var f in flumeEnds) if ((f.at - at).sqrMagnitude < 20f * 20f) { dup = true; break; }
                    if (!dup) flumeEnds.Add((at, new Vector3(ox, 0f, oz)));
                }
                inDip = false; best = -1;
            }
            FlumeEnds = flumeEnds.Count;
        }

        /// <summary>The level's own rock formations (L16_RimRocks) round every waterfall bed's end: a cliff face or outcrop across the end
        /// and a boulder cluster on each side, sunk into the land, so the bed ends at rocks instead of in a cut. Drawn only.</summary>
        void PlaceFlumeRocks()
        {
            var rr = GameObject.Find("L16_RimRocks"); if (!rr || flumeEnds.Count == 0) return;
            var faces = new List<GameObject>(); var boulders = new List<GameObject>();
            foreach (Transform c in rr.transform) { if (c.name.Contains("CliffFace") || c.name.Contains("Outcrop")) faces.Add(c.gameObject); else if (c.name.Contains("Boulder")) boulders.Add(c.gameObject); }
            if (faces.Count == 0 && boulders.Count == 0) return;
            if (faces.Count == 0) faces = boulders;
            if (boulders.Count == 0) boulders = faces;
            var cliffs = faces.FindAll(g => g.name.Contains("CliffFace"));
            int n = 0, e = 0;
            foreach (var f in flumeEnds)
            {
                var side = new Vector3(f.o.z, 0f, -f.o.x);
                // round 5: both raised flumes of the north side end at one of the level's own cliff faces (round 4 gave the right one a
                // flat outcrop that only the crowns round it hid); the other dips in the rim keep round 4's choice
                var face = e < KnownFlumes.Length && cliffs.Count > 0 ? cliffs[e % cliffs.Count] : faces[n % faces.Count]; e++;
                Rock(face, f.at + f.o * 3f, f.o, 1.15f, n++);
                Rock(boulders[n % boulders.Count], f.at + f.o * 1.5f + side * 8f, f.o, 1.6f, n++);
                Rock(boulders[n % boulders.Count], f.at + f.o * 1.5f - side * 8f, f.o, 1.5f, n++);
            }
            FlumeRocks = n;
        }

        void Rock(GameObject proto, Vector3 at, Vector3 o, float scale, int n)
        {
            var go = Instantiate(proto, transform); go.name = "L16_Outskirts_Rock_" + n;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);                  // drawn only: nothing may hit or block on it
            float gy = GridAt(at.x, at.z); if (GroundAt(at.x, at.z, out float ty)) gy = Mathf.Max(gy, ty);   // on the flume's own rim where it stands higher
            go.transform.position = new Vector3(at.x, gy - 0.6f * scale, at.z);
            go.transform.rotation = Quaternion.LookRotation(-o, Vector3.up) * Quaternion.Euler(0f, (Hash(n, 7) - 50) * 0.6f, 0f);
            go.transform.localScale = proto.transform.lossyScale * scale;
            go.SetActive(true);
        }

        GameObject MakeMesh(string name, Vector3[] v, Vector3[] n, List<int> t, Material mat, bool receive, float tile, Color[] colors)
        {
            var mesh = new Mesh { name = name, indexFormat = v.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            var uv = new Vector2[v.Length]; for (int i = 0; i < v.Length; i++) uv[i] = new Vector2(v[i].x / tile, v[i].z / tile);   // by world position, as the rim floor
            mesh.vertices = v; mesh.normals = n; mesh.uv = uv; if (colors != null) mesh.colors = colors; mesh.SetTriangles(t, 0); mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = receive;
            go.SetActive(false);                                                                     // shown by LateUpdate once built
            return go;
        }

        /// <summary>Tile lines from a to d through b and c (b and c kept exactly: the land grid's edge), at most 32 m apart.</summary>
        static List<float> Lines(float a, float b, float c, float d)
        {
            var l = new List<float>();
            void Run(float s, float e) { int k = Mathf.Max(1, Mathf.CeilToInt((e - s) / 32f)); for (int i = 0; i < k; i++) l.Add(Mathf.Lerp(s, e, i / (float)k)); }
            Run(a, b); Run(b, c); Run(c, d); l.Add(d);
            return l;
        }

        public static string Status() => I == null ? "OzL16Outskirts: not on this level" :
            $"OzL16Outskirts: on {On}, built {I.built} ({BuildMs:0} ms over {BuildFrames} frames), flume ends {FlumeEnds} with {FlumeRocks} rocks, standing back {I.standBack}, cells not drawn {CellsHidden}, groves {Groves}, raised feet {Rises}, rim ground {EdgeHits}/{EdgeSamples}, triangles {Triangles}, water cells {WaterCells}, widest zoom {(I.rts ? I.rts.maxHeight : 0f):0} m" + TreeStatus() + FarLookStatus() + RimFixStatus() + LandscapeStatus() + BolderStatus();
    }
}
