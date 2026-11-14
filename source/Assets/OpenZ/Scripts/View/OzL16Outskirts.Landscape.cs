// OpenZ (GPL-3.0) - Level 16: the land beyond the rim as a living jungle landscape (testing the free camera,
// the
// plants outside the map the map's own at their own sizes, nothing floating, nothing inside the map changed, no fps loss).
// Beyond the rim the land was a flat plain (round 4's 1.3 m rolls, then a flat far plain at LandY) with the rim floor's one picture on it.
// Now (LandscapeOn, the default):
//  - RELIEF (Relief55, the land's own height function, in place of round 4's Land() beyond the landmark pads): rolling hills that grow
//    with the distance from the plateau, a few low broad ridges, shallow swamp hollows, a creek in a shallow vale where the land is low,
//    and farther out the land rises here and there into big gentle hills; no cliff, no spike (slopes as the map's own banks at most).
//    It eases in exactly as round 4's rolls did (the rim's own heights at the plateau's edge: the edge does not change), and keeps round
//    4's land at every landmark beyond the rim and at the flume ends' rocks (PadAt), so nothing that stands there moves or sinks;
//  - the FAR LAND (FarLand): instead of round 4's flat far plain, rings of land round the land's grid out to the plain's own end, on its
//    border sharing the grid's own last points (no seam, no crack), with the same relief and colours; no water out there; k51's mist
//    runs over it at its own height (FarMistY, its lines at most LandMistCell apart);
//  - GROUND COLOUR (Tint55, on the rim floor's own picture and round 4's patches): wet mud round the water, bare earth on the steeper
//    slopes, dry grass on the tops, deep moss in the hollows and in big patches, soft earth paths, and far out a darker canopy tone;
//  - GROUND COVER: the map's own small plants (ferns, bushes, short reed tufts, a few bananas near the rim; reeds and a few young palms
//    by the water) in dense patches with open glades between, thickest near the rim and on slopes, thinning with the distance, never on
//    a path, a landmark, a rock or a tree's trunk;
//  - GROVES: small groves (3 to 8) and a few lone trees of the map's own small trees and palms (mangroves at the water), never a wall;
//  - ROCKS: outcrops of the map's own rocks (a boulder cluster or a low outcrop with a few mossy rocks round it) on slopes and ridges,
//    seated in the land as the flume rocks are (OzL16Outskirts.Props.cs);
//  - the mist moderate (LandMist instead of round 4's MistMax).
// Every plant is one of the map's own full models (RimSources: each true crown at most RimCap, the map plant's own size, only turned
// about the upright); never a '_lite' or far shape, at any zoom. COST: every plant and rock of this land and round 5e's groves are drawn
// GPU-instanced (Graphics.RenderMeshInstanced: one draw per model per 1023), per 32 m chunk tested against the camera, a model not drawn
// where its tallest plant there is under MinPx on the screen; the ground cover casts no shadow; a caster just outside the picture draws
// as far as its shadow reaches (the sun's side, a low sun too); the wind's room round a plant as its object had it. Round 5e's ring and
// grove plants have models of their own and are their own objects again in the low look, a flyover's last shot and from the ending on
// (the fort's blast throws them). Drawn only: no rule reads any of it.
// Switch: LandscapeOn (read when the level starts; false = the land exactly as before). Command line (the exe bench): -oz-l16-land
// r5e | k55, and -oz-l16-land-freetilt <deg> (the bench's views turned out from the map's middle at that free-camera tilt; needs the
// FREE CAMERA option). Play check: return OpenZ.OzL16Outskirts.OutskirtsLandscapeCheck();
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public sealed partial class OzL16Outskirts
    {
        /// <summary>On (default): the landscape beyond the rim. Off: the land as before (round 4's rolls, the flat far plain, round 5e's
        /// groves as objects). Set before the level starts.</summary>
        public static bool LandscapeOn = true;
        /// <summary>A model is not drawn in a chunk where its tallest plant there is under this many pixels tall on the screen.</summary>
        public static float MinPx = 2f;
        /// <summary>The relief (m, m beyond the plateau's edge, m of noise scale): rolls (near / far amplitude, from - to, scale), ridges,
        /// swamp hollows (depth, scale, noise from - to), the far rise, the creek (scale, warp, bed and vale widths in noise units, from),
        /// the landmark pads (m beyond a landmark's own mark: none - full; the flume ends' rocks' pad).</summary>
        public static float RollA = 2.6f, RollFar = 5.4f, RollFrom = 40f, RollTo = 230f, RollScale = 130f;
        public static float RidgeA = 6f, RidgeScale = 200f, RidgeFrom = 30f, RidgeTo = 110f;
        public static float DipA = 2f, DipScale = 58f, DipFrom = 0.28f, DipTo = 0.66f;
        public static float FarA = 18f, FarFrom = 110f, FarTo = 380f, FarScale = 300f;
        public static float CreekScale = 230f, CreekWarp = 0.35f, CreekBed = 0.09f, CreekVale = 0.24f, CreekFrom = 40f;
        public static float PadIn = 5f, PadOut = 26f, FlumePad = 18f;
        /// <summary>The mist's strength far out (round 4's MistMax 0.5 was too much in places) and its lines over the far land (m apart at most).</summary>
        public static float LandMist = 0.36f, LandMistCell = 16f;
        /// <summary>The far land's rings: their distance beyond the land's grid (m) and their points' spacing (m).</summary>
        static readonly float[] FarRings = { 8f, 18f, 30f, 46f, 66f, 92f, 126f, 170f, 226f, 296f, 384f, 494f, 630f, 772f };
        static readonly float[] FarSpacing = { 8f, 10f, 12f, 16f, 20f, 24f, 32f, 40f, 48f, 64f, 64f, 96f, 128f, 128f };
        /// <summary>The ground colour's tints (multiplied on the rim floor's picture): wet mud, bare earth, dry tops, paths, moss, far canopy.</summary>
        public static Color TintMud = new Color(0.60f, 0.55f, 0.48f), TintEarth = new Color(0.90f, 0.78f, 0.64f), TintDry = new Color(1.15f, 1.07f, 0.78f),
            TintPath = new Color(1.05f, 0.88f, 0.70f), TintMoss = new Color(0.78f, 0.94f, 0.78f), TintCanopy = new Color(0.60f, 0.77f, 0.58f);
        /// <summary>Where the rocks, groves and cover stand (m: lattice, from - out beyond the plateau's edge; shares; counts).</summary>
        public static float RockLattice = 56f, RockFrom = 18f, RockBuriedMax = 0.6f;
        public static int RockCompanions = 4;
        public static float GroveLattice = 44f, GroveFrom = 14f, GroveChance = 0.75f, GroveR0 = 2f, GroveR = 9f, LoneLattice = 30f, LoneChance = 0.10f;
        public static int LandGroveTries = 16, GroveMin = 3, GroveMore = 6;
        public static float CoverStep = 2f, CoverFrom = 2.5f, CoverOut = 235f, CoverMax = 0.62f, CoverFar = 0.10f, CoverThin = 120f, BananaOut = 40f;
        /// <summary>The ground cover's mixes (RimKinds indices and weights): dry land (ferns, short reed tufts as the meadows' grass, bushes,
        /// bananas), by the water (reeds, a few young palms).</summary>
        static readonly int[] DryMix = { 0, 4, 1, 2 }, DryMixW = { 45, 35, 15, 5 }, WetMix = { 3, 4, 5 }, WetMixW = { 87, 82, 6 };
        static readonly string[] RockKinds55 = { "JGH_BoulderCluster_A", "JGH_BoulderCluster_B", "JGH_LowOutcrop_A" };
        static readonly string[] Mossy55 = { "FL_RockMossyA", "FL_RockMossyB" };
        const int Batch = 1023;
        /// <summary>A shadow caster this far outside the picture still draws (its shadow falls into the picture), m; and on the sun's side
        /// as far as the shadow of the chunk's tallest caster reaches on the ground (a low sun: dusk, the blue hour), at most ShadowReach m.</summary>
        public static float ShadowPad = 12f, ShadowReach = 48f;
        /// <summary>Counters (this level): plants and rocks made, round 5e's grove plants drawn instanced, refused places, the land's
        /// numbers, the far land's triangles, the drawing last frame (calls, models, instances, triangles).</summary>
        public static int LandCover, LandCoverWet, LandTrees, LandTreesWet, LandRocks, LandGroves, Land5eGroves, Land5eRing, LandLots, LandChunks, LandPieces, FarLandTris, FarLandPoints;
        public static int LandCallsNow, LandLotsNow, LandInstNow, LandRefusedRocks;
        public static long LandTrisNow, LandTrisAll;
        public static float FarLandMin, FarLandMax;
        public static string LandWhy = "", LandFrom = "";
        static float benchTilt = -1f; static bool benchTiltSaid;

        sealed class Lot
        {
            public Mesh mesh; public Material[] mats; public ShadowCastingMode cast; public int layer; public string kind; public bool r5e; public long tris;
            public readonly List<Matrix4x4> list = new List<Matrix4x4>(); public readonly List<int> chunkOf = new List<int>(); public readonly List<Bounds> boxes = new List<Bounds>();
            public readonly List<float> feet = new List<float>(), crowns = new List<float>(); public readonly List<byte> wet = new List<byte>();
            public Matrix4x4[] m, buf; public int drawnNow; public Bounds boxNow; public RimSource src;
        }
        sealed class Piece { public Lot lot; public int start, count; public Bounds box; public float h; }
        sealed class Chunk { public Bounds box; public float hCast; public readonly List<Piece> pieces = new List<Piece>(); }

        readonly List<Lot> lots = new List<Lot>();
        readonly Dictionary<(Mesh, ShadowCastingMode, Material, bool), Lot> lotOf = new Dictionary<(Mesh, ShadowCastingMode, Material, bool), Lot>();
        readonly List<Chunk> chunks = new List<Chunk>();
        readonly Dictionary<Material, Material> instMat = new Dictionary<Material, Material>();
        readonly Dictionary<Mesh, Material[]> hookedMats = new Dictionary<Mesh, Material[]>();
        readonly Dictionary<long, List<Vector2>> trees55 = new Dictionary<long, List<Vector2>>();
        readonly Dictionary<long, List<Vector3>> rocks55 = new Dictionary<long, List<Vector3>>();
        readonly List<Renderer> groves5eOff = new List<Renderer>(), ring5e = new List<Renderer>();
        readonly Plane[] landPlanes = new Plane[6];
        bool landShown, landMade, held5e, ending5e; Camera landCam; Light landSun; float landSunAt = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void LandscapeResetStatics()
        {   // every Play session starts with the landscape on (the switch is read when the level starts)
            LandscapeOn = true; LandCover = LandCoverWet = LandTrees = LandTreesWet = LandRocks = LandGroves = Land5eGroves = Land5eRing = LandLots = LandChunks = LandPieces = FarLandTris = FarLandPoints = 0;
            LandCallsNow = LandLotsNow = LandInstNow = LandRefusedRocks = 0; LandTrisNow = LandTrisAll = 0; FarLandMin = FarLandMax = 0f; LandWhy = ""; LandFrom = ""; benchTilt = -1f; benchTiltSaid = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LandscapeArgs()
        {   // -oz-l16-land k55 | r5e: the landscape on / off for a whole run of the game (the exe bench); -oz-l16-land-freetilt <deg>
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < a.Length; i++)
            {
                if (a[i] == "-oz-l16-land")
                {
                    string w = a[i + 1].ToLowerInvariant();
                    if (w == "k55" || w == "r5e") { LandscapeOn = w == "k55"; LandFrom = "command line " + w; Debug.Log("[OzL16Outskirts] level 16 land beyond the map: " + (LandscapeOn ? "the landscape" : "as before (round 5e)") + " (" + LandFrom + ")"); }
                }
                else if (a[i] == "-oz-l16-land-freetilt" && float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float t))
                    benchTilt = Mathf.Clamp(t, 22f, 89f);
            }
        }

        // ------------------------------------------------------------------ the noise (written out: the same numbers off the engine)
        static readonly float[] GX55 = { 0.980785251f, 0.831469595f, 0.555570245f, 0.195090324f, -0.195090324f, -0.555570245f, -0.831469595f, -0.980785251f, -0.980785251f, -0.831469595f, -0.555570245f, -0.195090324f, 0.195090324f, 0.555570245f, 0.831469595f, 0.980785251f };
        static readonly float[] GZ55 = { 0.195090324f, 0.555570245f, 0.831469595f, 0.980785251f, 0.980785251f, 0.831469595f, 0.555570245f, 0.195090324f, -0.195090324f, -0.555570245f, -0.831469595f, -0.980785251f, -0.980785251f, -0.831469595f, -0.555570245f, -0.195090324f };

        static int G55(int x, int z, int s) { unchecked { uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ (uint)(s * 83492791); h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15; return (int)(h & 15u); } }

        /// <summary>A gradient noise about -1..1 (0 on every whole point), seed s.</summary>
        static float Nz(float x, float z, int s)
        {
            int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z); float fx = x - ix, fz = z - iz;
            float u = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f), w = fz * fz * fz * (fz * (fz * 6f - 15f) + 10f);
            int a = G55(ix, iz, s), b = G55(ix + 1, iz, s), c = G55(ix, iz + 1, s), d = G55(ix + 1, iz + 1, s);
            float va = GX55[a] * fx + GZ55[a] * fz, vb = GX55[b] * (fx - 1f) + GZ55[b] * fz, vc = GX55[c] * fx + GZ55[c] * (fz - 1f), vd = GX55[d] * (fx - 1f) + GZ55[d] * (fz - 1f);
            float top = va + u * (vb - va), bot = vc + u * (vd - vc);
            return 1.41f * (top + w * (bot - top));
        }

        static float S01(float t) => Mathf.SmoothStep(0f, 1f, t);
        static float Ramp(float a, float b, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, v));

        // ------------------------------------------------------------------ the land's height (HeightAt's hook)
        /// <summary>HeightAt: the land away from the rim. kk = how far the rim's own heights have given way here (0 at the edge, 1 out).</summary>
        float LandAt(float x, float z, float d, float fade, float kk)
        {
            float r4 = Land(x, z, fade * kk);
            if (!LandscapeOn) return r4;
            if (d > 4f) TintCache(x, z);                                                                // a grid point: its colours' noise made here, in the sliced grid loop (Tint55)
            float pad = PadAt(x, z);
            if (pad <= 0f) return r4;
            return Mathf.Lerp(r4, LandY + kk * Relief55(x, z, d), pad);
        }

        /// <summary>0 at a landmark beyond the rim (its mark) and at a flume end's rocks, 1 from PadOut m beyond them: round 4's land stays there.</summary>
        float PadAt(float x, float z)
        {
            float p = 1f;
            foreach (var c in marks)
            {
                float dx = x - c.x, dz = z - c.y, r = c.z + PadOut;
                if (dx * dx + dz * dz >= r * r) continue;
                p = Mathf.Min(p, Ramp(PadIn, PadOut, Mathf.Sqrt(dx * dx + dz * dz) - c.z));
            }
            foreach (var f in flumeEnds)
            {
                float dx = x - (f.at.x + f.o.x * 2f), dz = z - (f.at.z + f.o.z * 2f), r = FlumePad + PadOut;
                if (dx * dx + dz * dz >= r * r) continue;
                p = Mathf.Min(p, Ramp(0f, PadOut, Mathf.Sqrt(dx * dx + dz * dz) - FlumePad));
            }
            return p;
        }

        /// <summary>The creek's line value (0 on its middle line): a warped noise line.</summary>
        static float CreekLine(float x, float z)
        {
            float wx = CreekWarp * Nz(x / 90f, z / 90f, 8), wz = CreekWarp * Nz(x / 90f + 5.3f, z / 90f - 2.1f, 9);
            return Nz(x / CreekScale + wx, z / CreekScale + wz, 7);
        }

        /// <summary>The relief's parts at (x, z), d m beyond the plateau: rolls, ridges, hollows, the far rise, the creek's vale and bed shares.</summary>
        static void ReliefParts(float x, float z, float d, out float roll, out float ridge, out float dip, out float far, out float vale, out float bed)
        {
            float ra = Mathf.Lerp(RollA, RollFar, Ramp(RollFrom, RollTo, d)), sc = RollScale;
            roll = ra * (Nz(x / sc, z / sc, 1) + 0.45f * Nz(x / (sc * 0.41f), z / (sc * 0.41f), 2) + 0.18f * Nz(x / (sc * 0.15f), z / (sc * 0.15f), 3));
            if (roll < 0f) roll *= 0.3f;                                                                   // the rolls rise; their hollows stay shallow (the pools are the dips' and the creek's)
            float rn = Nz(x / RidgeScale, z / RidgeScale, 4);
            float rmask = Ramp(-0.05f, 0.35f, Nz(x / 420f, z / 420f, 11)) * Ramp(RidgeFrom, RidgeTo, d);
            float rr = Mathf.Max(0f, 1f - Mathf.Abs(rn) * 2.4f); ridge = RidgeA * rmask * rr * rr;
            float dn = Nz(x / DipScale, z / DipScale, 5) + 0.35f * Nz(x / 19f, z / 19f, 6);
            dip = -DipA * Ramp(DipFrom, DipTo, dn) * (1f - Ramp(Reach - 80f, Reach - 40f, d));
            far = FarA * Ramp(FarFrom, FarTo, d) * Mathf.Max(0f, Nz(x / FarScale, z / FarScale, 12) + 0.3f) * (0.75f + 0.25f * Nz(x / 110f, z / 110f, 13));
            float c = CreekLine(x, z), creekTo = Reach - 45f;
            float cm = Ramp(CreekFrom, CreekFrom + 30f, d) * (1f - Ramp(creekTo - 30f, creekTo, d)) * Ramp(-0.2f, 0.15f, Nz(x / 520f, z / 520f, 10));
            cm *= 1f - Ramp(1.2f, 3.2f, roll + ridge + far);                                              // no creek cuts into a hill: it fades out where the land rises
            vale = (1f - Ramp(0f, CreekVale, Mathf.Abs(c))) * cm;
            bed = (1f - Ramp(0f, CreekBed, Mathf.Abs(c))) * cm;
        }

        /// <summary>The land's height over LandY at (x, z), d m beyond the plateau (no pads, no rim blend).</summary>
        float Relief55(float x, float z, float d)
        {
            ReliefParts(x, z, d, out float roll, out float ridge, out float dip, out float far, out float vale, out float bed);
            float h = roll + ridge + dip + far;
            h = Mathf.Lerp(h, Mathf.Min(h, -0.12f), vale);                                                // the creek's vale: the land falls to a low floor
            h = Mathf.Lerp(h, (waterY - LandY) - 0.75f, bed);                                               // its bed under the water
            return Mathf.Lerp(h, Mathf.Max(h, waterY - LandY + 0.25f), Ramp(Reach - 75f, Reach - 40f, d));   // no water out to the grid's edge: the far land has none
        }

        /// <summary>The far land beyond the land's grid (and round 4's flat plain with the landscape off).</summary>
        float FarY(float x, float z)
        {
            if (!LandscapeOn) return LandY;
            EdgeS(x, z, out float d);
            return Bolder(x, z, d, LandY + Mathf.Max(Relief55(x, z, d), waterY - LandY + 0.25f));   // Bolder: OzL16Outskirts.Bolder.cs
        }

        /// <summary>FarMist: the far land's height under the mist (its points made ahead, a row a step, in LandscapeSteps: FarMist makes the
        /// whole mist at once in the build's last step).</summary>
        float FarMistY(float x, float z)
        {
            if (!LandscapeOn) return LandY;
            if (mistY != null)
            {
                int i = System.Array.BinarySearch(mistXs, x), j = System.Array.BinarySearch(mistZs, z);
                if (i >= 0 && j >= 0) { float y = mistY[j * mistXs.Length + i]; if (!float.IsNaN(y)) return y; }
            }
            return FarY(x, z);
        }

        float[] mistXs, mistZs, mistY;

        /// <summary>LandscapeSteps: the far land's heights under the mist's lines (FarMist's own lines), a row a step.</summary>
        IEnumerable<bool> MistAhead()
        {
            mistY = null;
            var xs = new List<float>(); var xi = new List<int>(); var zs = new List<float>(); var zi = new List<int>();
            Vector2 mid = new Vector2(map.x * 0.5f, -map.y * 0.5f);
            MistLines(mid.x - FarMistOut, gx0, gnx, mid.x + FarMistOut, xs, xi);
            MistLines(mid.y - FarMistOut, gz0, gnz, mid.y + FarMistOut, zs, zi);
            var ax = xs.ToArray(); var az = zs.ToArray(); var y = new float[ax.Length * az.Length];
            for (int j = 0; j < az.Length; j++)
            {
                for (int i = 0; i < ax.Length; i++) y[j * ax.Length + i] = xi[i] >= 0 && zi[j] >= 0 ? float.NaN : FarY(ax[i], az[j]);
                yield return true;
            }
            mistXs = ax; mistZs = az; mistY = y;
        }

        /// <summary>FarMist: the mist's strength far out.</summary>
        static float MistMaxNow => LandscapeOn ? (LandBold ? BoldMist : LandMist) : MistMax;   // BoldMist: OzL16Outskirts.Bolder.cs

        /// <summary>FarMist's lines over the far land at most this far apart.</summary>
        static float MistCellNow => LandscapeOn ? Mathf.Min(FarMistCell, LandMistCell) : FarMistCell;

        // ------------------------------------------------------------------ the ground's colour
        /// <summary>The noise of Tint55's shares at (x, z): dry tops, the paths' line (its distance from the middle), the paths' regions,
        /// moss, the canopy tone.</summary>
        static void TintNoise(float x, float z, out float n40, out float pathN, out float n46, out float n44, out float n45)
        {
            n40 = Nz(x / 40f, z / 40f, 40);
            float pw = 0.3f * Nz(x / 50f, z / 50f, 42), pz = 0.3f * Nz(x / 50f + 3.1f, z / 50f + 7.7f, 43);
            pathN = Mathf.Abs(Nz(x / 130f + pw, z / 130f + pz, 41));
            n46 = Nz(x / 300f, z / 300f, 46); n44 = Nz(x / 70f, z / 70f, 44); n45 = Nz(x / 150f, z / 150f, 45);
        }

        /// <summary>The land grid's point at exactly (x, z) (its index), else -1.</summary>
        int GridIndex(float x, float z)
        {
            if (gh == null || gnx < 1 || gnz < 1) return -1;
            int i = Mathf.RoundToInt((x - gx0) / Cell), j = Mathf.RoundToInt((z - gz0) / Cell);
            if (i < 0 || j < 0 || i >= gnx || j >= gnz || gx0 + i * Cell != x || gz0 + j * Cell != z) return -1;
            return j * gnx + i;
        }

        float[] tintN;                                                                                     // per grid point: TintNoise's 5 values (NaN: not made)

        /// <summary>LandAt, in BuildSliced's sliced grid loop: a grid point's TintNoise, kept for its vertex colour in the mesh frame (the
        /// same numbers; the mesh frame is one frame, the grid loop is sliced).</summary>
        void TintCache(float x, float z)
        {
            int k = GridIndex(x, z); if (k < 0) return;
            if (tintN == null || tintN.Length != gh.Length * 5) { tintN = new float[gh.Length * 5]; for (int q = 0; q < tintN.Length; q++) tintN[q] = float.NaN; }
            if (!float.IsNaN(tintN[5 * k])) return;
            TintNoise(x, z, out tintN[5 * k], out tintN[5 * k + 1], out tintN[5 * k + 2], out tintN[5 * k + 3], out tintN[5 * k + 4]);
        }

        /// <summary>Tint55's shares at a land point (ny = its normal's up part, d = m beyond the plateau's edge).</summary>
        void TintParts(float x, float y, float z, float ny, float d, out float wet, out float earth, out float dry, out float path, out float moss, out float canopy)
        {
            float n40, pathN, n46, n44, n45; int k = tintN != null ? GridIndex(x, z) : -1;
            if (k >= 0 && 5 * k + 4 < tintN.Length && !float.IsNaN(tintN[5 * k])) { n40 = tintN[5 * k]; pathN = tintN[5 * k + 1]; n46 = tintN[5 * k + 2]; n44 = tintN[5 * k + 3]; n45 = tintN[5 * k + 4]; }
            else TintNoise(x, z, out n40, out pathN, out n46, out n44, out n45);
            float inF = S01((d - 4f) / 30f), slope = 1f - ny;
            wet = inF * (1f - Ramp(waterY + 0.15f, waterY + 0.95f, y));
            earth = inF * Ramp(0.035f, 0.12f, slope);
            dry = inF * Ramp(LandY + 0.8f, LandY + 3.2f, y) * (0.65f + 0.35f * n40) * (1f - wet);
            path = inF * (1f - Ramp(0f, 0.045f, pathN)) * Ramp(18f, 45f, d) * Ramp(-0.1f, 0.25f, n46) * (1f - wet);
            moss = inF * Mathf.Max(Ramp(0.05f, 0.45f, n44), Ramp(LandY - 0.1f, LandY - 1f, y) * (1f - wet)) * (1f - dry);
            canopy = Ramp(150f, 300f, d) * Ramp(-0.15f, 0.3f, n45);
        }

        /// <summary>BuildSliced's vertex colours: round 4's colour c of a land point, with the landscape's tints.</summary>
        Color Tint55(Color c, float x, float y, float z, float ny, float d)
        {
            if (!LandscapeOn) return c;
            if (d <= 4f) { c.a = 1f; return c; }                                                       // every share is 0 within 4 m of the plateau's edge (the same colour, no noise made)
            TintParts(x, y, z, ny, d, out float wet, out float earth, out float dry, out float path, out float moss, out float canopy);
            c *= Color.Lerp(Color.white, TintMud, wet); c *= Color.Lerp(Color.white, TintEarth, earth); c *= Color.Lerp(Color.white, TintDry, dry);
            c *= Color.Lerp(Color.white, TintPath, path); c *= Color.Lerp(Color.white, TintMoss, moss); c *= Color.Lerp(Color.white, TintCanopy, canopy);
            c.a = 1f; return c;
        }

        /// <summary>Round 4's own colour of a land point (BuildSliced's patches), for the far land's points.</summary>
        static Color Tint4(float x, float z, float dd)
        {
            float inF = Mathf.SmoothStep(0f, 1f, (dd - 4f) / 30f);
            float g = 1f + inF * (ToneDrift * N(x, z, 90f, 6.6f, 2.4f) + 0.5f * ToneDrift * N(x, z, 23f, 1.9f, 8.8f));
            Color c = new Color(g, g, g, 1f);
            float pm = inF * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.54f, 0.66f, Mathf.PerlinNoise(x / 37f + 2.3f, z / 37f + 9.1f)));
            float pd = inF * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.68f, Mathf.PerlinNoise(x / 29f + 7.7f, z / 29f + 1.4f)));
            float pe = inF * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.63f, 0.71f, Mathf.PerlinNoise(x / 17f + 4.9f, z / 17f + 6.2f)));
            c *= Color.Lerp(Color.white, PatchMoss, pm); c *= Color.Lerp(Color.white, PatchDry, pd * (1f - pm)); c *= Color.Lerp(Color.white, PatchEarth, pe * 0.85f);
            c.a = 1f; return c;
        }

        // ------------------------------------------------------------------ the far land (BuildSliced: instead of round 4's flat plain)
        static void RingSides(float x0, float x1, float z0, float z1, float sp, List<Vector2>[] sides)
        {
            var c = new[] { new Vector4(x0, z1, x1, z1), new Vector4(x1, z1, x1, z0), new Vector4(x1, z0, x0, z0), new Vector4(x0, z0, x0, z1) };
            for (int s = 0; s < 4; s++)
            {
                var l = sides[s]; l.Clear();
                float len = Vector2.Distance(new Vector2(c[s].x, c[s].y), new Vector2(c[s].z, c[s].w)); int n = Mathf.Max(1, Mathf.CeilToInt(len / sp - 1e-6f));
                for (int i = 0; i <= n; i++) { float t = i / (float)n; l.Add(new Vector2(Mathf.Lerp(c[s].x, c[s].z, t), Mathf.Lerp(c[s].y, c[s].w, t))); }
            }
        }

        /// <summary>The far land: rings round the land's grid out to the plain's end (FarRings), the first the grid's own border points (its
        /// heights, normals and colours: no seam), the next ones on FarY; each ring zipped to the one inside it side by side. Null with the
        /// landscape off (round 4's plain is made).</summary>
        GameObject FarLand55(Vector3[] gridN, Color[] gridC)
        {
            if (!LandscapeOn || gh == null) return null;
            float gx1 = gx0 + (gnx - 1) * Cell, gz1 = gz0 + (gnz - 1) * Cell;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var col = new List<Color>(); var t = new List<int>();
            var sides = new[] { new List<Vector2>(), new List<Vector2>(), new List<Vector2>(), new List<Vector2>() };
            List<int>[] prev = null; FarLandMin = float.MaxValue; FarLandMax = float.MinValue;
            for (int r = 0; r <= FarRings.Length; r++)
            {
                if (r == 0) RingSides(gx0, gx1, gz0, gz1, Cell, sides);
                else { float o = FarRings[r - 1]; RingSides(gx0 - o, gx1 + o, gz0 - o, gz1 + o, FarSpacing[r - 1], sides); }
                var idx = new List<int>[4];
                for (int s = 0; s < 4; s++)
                {
                    idx[s] = new List<int>();
                    foreach (var p in sides[s])
                    {
                        idx[s].Add(v.Count);
                        if (r == 0)
                        {   // the grid's own border point
                            int i = Mathf.RoundToInt((p.x - gx0) / Cell), j = Mathf.RoundToInt((p.y - gz0) / Cell), k = j * gnx + i;
                            v.Add(new Vector3(p.x, gh[k], p.y)); n.Add(gridN != null ? gridN[k] : Vector3.up); col.Add(gridC != null ? gridC[k] : Color.white);
                            continue;
                        }
                        float y = FarY(p.x, p.y), e = Cell;
                        var nn = new Vector3(FarY(p.x - e, p.y) - FarY(p.x + e, p.y), 2f * e, FarY(p.x, p.y - e) - FarY(p.x, p.y + e)).normalized;
                        EdgeS(p.x, p.y, out float dd);
                        v.Add(new Vector3(p.x, y, p.y)); n.Add(nn); col.Add(TintBold(Tint55(Tint4(p.x, p.y, dd), p.x, y, p.y, nn.y, dd), p.x, y, p.y, nn.y, dd));   // TintBold: OzL16Outskirts.Bolder.cs
                        FarLandMin = Mathf.Min(FarLandMin, y); FarLandMax = Mathf.Max(FarLandMax, y);
                    }
                }
                if (prev != null)
                    for (int s = 0; s < 4; s++)
                    {   // zip the inner side a with the outer side b by their share along the side
                        var a = prev[s]; var b = idx[s]; int ia = 0, ib = 0, na = a.Count - 1, nb = b.Count - 1;
                        while (ia < na || ib < nb)
                        {
                            float ta = ia < na ? (ia + 1) / (float)na : 9f, tb = ib < nb ? (ib + 1) / (float)nb : 9f;
                            if (ta <= tb) { t.Add(a[ia]); t.Add(b[ib]); t.Add(a[ia + 1]); ia++; }
                            else { t.Add(a[ia]); t.Add(b[ib]); t.Add(b[ib + 1]); ib++; }
                        }
                    }
                prev = idx;
            }
            FarLandTris = t.Count / 3; FarLandPoints = v.Count;
            return MakeMesh("L16_Outskirts_FarLand", v.ToArray(), n.ToArray(), t, groundMat, true, RimTile, groundMat.HasProperty("_UseVertexColor") ? col.ToArray() : null);
        }

        // ------------------------------------------------------------------ the plants and rocks (BuildSliced: after RimFixSteps)
        sealed class RockSrc { public string name, kind; public Mesh mesh; public Material[] mats; public Quaternion rot; public Vector3 scale; public ShadowCastingMode cast; }
        readonly Dictionary<string, List<RockSrc>> rockSrc = new Dictionary<string, List<RockSrc>>();
        List<RimSource>[] deck55;

        float SlopeAt(float x, float z)
        {
            if (gh == null) return 0f;
            float fi = (x - gx0) / Cell, fj = (z - gz0) / Cell;
            int i = Mathf.Clamp(Mathf.FloorToInt(fi), 0, gnx - 2), j = Mathf.Clamp(Mathf.FloorToInt(fj), 0, gnz - 2); float u = fi - i, w = fj - j;
            float ha = gh[j * gnx + i], hb = gh[j * gnx + i + 1], hc = gh[(j + 1) * gnx + i + 1], hd = gh[(j + 1) * gnx + i];
            float dx = (u >= w ? hb - ha : hc - hd) / Cell, dz = (u >= w ? hc - hb : hd - ha) / Cell;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static int Pick55(int[] kinds, int[] weights, int h100)
        {
            int sum = 0; foreach (var w in weights) sum += w;
            float pick = h100 * sum / 100f, acc = 0f; int last = -1;
            for (int i = 0; i < kinds.Length; i++) { acc += weights[i]; last = kinds[i]; if (pick < acc) return kinds[i]; }
            return last;
        }

        /// <summary>The materials the wind gave the map's own plant of this model (its run-time copy), else its own.</summary>
        Material[] MatsOf(Mesh mesh, Material[] fallback) => mesh && hookedMats.TryGetValue(mesh, out var m) ? m : fallback;

        /// <summary>The instanced model of a mesh, its way of casting, its materials (by the first) and its round (r5e: round 5e's ring and
        /// grove plants, apart: from the ending on they are their own objects again, for the fort's blast to throw).</summary>
        Lot LotOf(Mesh mesh, Material[] mats, ShadowCastingMode cast, int layer, string kind, bool r5e = false)
        {
            var key = (mesh, cast, mats != null && mats.Length > 0 ? mats[0] : null, r5e);
            if (lotOf.TryGetValue(key, out var l)) return l;
            var im = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                var m0 = mats[i]; if (!m0) { im[i] = null; continue; }
                if (!instMat.TryGetValue(m0, out var c)) { c = new Material(m0) { name = m0.name + " (outskirts, instanced)", enableInstancing = true }; instMat[m0] = c; }
                im[i] = c;
            }
            l = new Lot { mesh = mesh, mats = im, cast = cast, layer = layer, kind = kind, r5e = r5e, tris = Tris(mesh) };
            lotOf[key] = l; lots.Add(l);
            return l;
        }

        void AddInstance(Lot l, Matrix4x4 m, float foot, bool wet, float crown)
        {
            var b = WorldBox(l.mesh.bounds, m);
            l.list.Add(m); l.boxes.Add(b); l.feet.Add(foot); l.wet.Add((byte)(wet ? 1 : 0)); l.crowns.Add(crown);
            l.chunkOf.Add(Mathf.FloorToInt(b.center.x / 32f) * 100000 + Mathf.FloorToInt(b.center.z / 32f));
        }

        bool ClearMarks(float x, float z, float extra)
        {
            foreach (var c in marks) { float r = c.z + extra; if ((x - c.x) * (x - c.x) + (z - c.y) * (z - c.y) < r * r) return false; }
            foreach (var f in flumeEnds) { var r = f.at + f.o * 2f; if ((x - r.x) * (x - r.x) + (z - r.z) * (z - r.z) < RockClear * RockClear) return false; }
            return true;
        }

        bool NearRock(float x, float z, float extra)
        {
            int bx = Mathf.FloorToInt(x / 8f), bz = Mathf.FloorToInt(z / 8f);
            for (int a = bx - 1; a <= bx + 1; a++)
                for (int b = bz - 1; b <= bz + 1; b++)
                    if (rocks55.TryGetValue(Key(a, b), out var l)) foreach (var q in l) { float r = q.z + extra; if ((x - q.x) * (x - q.x) + (z - q.y) * (z - q.y) < r * r) return true; }
            return false;
        }

        /// <summary>The level's own rocks inside the map (L16_RimRocks: the boulder clusters and the low outcrop, their LOD0) and the flora
        /// kit's mossy rocks inside the map: the sources of the outcrops, by name.</summary>
        void RockSources(GameObject flora)
        {
            rockSrc.Clear();
            foreach (var k in RockKinds55) rockSrc[k] = new List<RockSrc>();
            foreach (var k in Mossy55) rockSrc[k] = new List<RockSrc>();
            var rr = GameObject.Find("L16_RimRocks");
            if (rr)
                foreach (Transform t in rr.transform)
                {
                    var r = RockLod0(t, out MeshFilter mf); if (!r || !mf || !mf.sharedMesh) continue;
                    string kind = RockKind(mf); if (!rockSrc.ContainsKey(kind) || Outside(r.bounds.center) > 0f) continue;
                    rockSrc[kind].Add(new RockSrc { name = t.name, kind = kind, mesh = mf.sharedMesh, mats = r.sharedMaterials, rot = mf.transform.rotation, scale = mf.transform.lossyScale, cast = r.shadowCastingMode });
                }
            if (flora)
                foreach (var mf in flora.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = mf ? mf.sharedMesh : null; if (!mesh || System.Array.IndexOf(Mossy55, mesh.name) < 0 || Outside(mf.transform.position) > 0f) continue;
                    var r = mf.GetComponent<MeshRenderer>(); if (!r) continue;
                    rockSrc[mesh.name].Add(new RockSrc { name = mf.name, kind = mesh.name, mesh = mesh, mats = r.sharedMaterials, rot = mf.transform.rotation, scale = mf.transform.lossyScale, cast = r.shadowCastingMode });
                }
            foreach (var l in rockSrc.Values) l.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        /// <summary>A rock seated as the flume rocks are: its lowest point a share of its height under the lowest of 3 x 3 points of the land
        /// over the middle 60 % of its foot. False: it would stand more than RockBuriedMax of its height in the land (a steep place).</summary>
        bool SeatRock(RockSrc s, float x, float z, float yaw, out Matrix4x4 m, out float radius)
        {
            var rot = Quaternion.AngleAxis(yaw, Vector3.up) * s.rot;
            m = Matrix4x4.TRS(new Vector3(x, 0f, z), rot, s.scale);
            var b = WorldBox(s.mesh.bounds, m);
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++) { float g = Gen(b.center.x + i * 0.6f * b.extents.x, b.center.z + j * 0.6f * b.extents.z); lo = Mathf.Min(lo, g); hi = Mathf.Max(hi, g); }
            float size = b.size.y, sink = Mathf.Clamp(0.12f * size, 0.15f, 0.5f);
            float y = lo - sink - b.min.y;
            radius = Mathf.Sqrt(b.extents.x * b.extents.x + b.extents.z * b.extents.z);
            m = Matrix4x4.TRS(new Vector3(x, y, z), rot, s.scale);
            return (hi - lo + sink) / Mathf.Max(0.01f, size) <= RockBuriedMax;
        }

        void AddRock(RockSrc s, Matrix4x4 m, float x, float z, float radius, int layer)
        {
            var l = LotOf(s.mesh, s.mats, s.cast, layer, s.kind);
            AddInstance(l, m, float.NaN, false, -1f); LandRocks++;
            long k = Key(Mathf.FloorToInt(x / 8f), Mathf.FloorToInt(z / 8f));
            if (!rocks55.TryGetValue(k, out var lst)) rocks55[k] = lst = new List<Vector3>();
            lst.Add(new Vector3(x, z, radius));
        }

        /// <summary>BuildSliced, after RimFixSteps (round 5e's groves are made): the rocks, the groves and the ground cover, a step at a time.</summary>
        IEnumerable<bool> LandscapeSteps()
        {
            LandCover = LandCoverWet = LandTrees = LandTreesWet = LandRocks = LandGroves = LandRefusedRocks = 0; LandWhy = "";
            lots.Clear(); lotOf.Clear(); chunks.Clear(); trees55.Clear(); rocks55.Clear(); hookedMats.Clear(); landMade = false;
            if (!LandscapeOn || gh == null) yield break;
            var flora = GameObject.Find(FloraRoot); int layer = flora ? flora.layer : 0;
            // the wind's own copies of the map's plants' materials, per model (the instanced plants sway as the map's own)
            var wind = OzFloraWind.I;
            if (flora)
                foreach (var r in flora.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = r ? r.GetComponent<MeshFilter>() : null; var mesh = mf ? mf.sharedMesh : null;
                    if (!mesh || hookedMats.ContainsKey(mesh) || Outside(r.transform.position) > 0f) continue;
                    if (wind && wind.TryGet(r, out _)) hookedMats[mesh] = r.sharedMaterials;
                }
            yield return true;
            RockSources(flora);
            // the decks: the map's own plants of each kind in its base model, true crown measured and under the cap
            deck55 = null;
            if (rimDeck != null)
            {
                deck55 = new List<RimSource>[RimKinds.Length];
                for (int k = 0; k < RimKinds.Length; k++)
                {
                    deck55[k] = new List<RimSource>();
                    if (rimDeck[k] == null) continue;
                    foreach (var s in rimDeck[k]) if (s.mesh && s.mesh.name == RimKinds[k] && s.tw >= 0f && s.tw <= RimCap + 0.005f) deck55[k].Add(s);
                }
            }
            else LandWhy = "no plants (round 5e's sources were not made)";
            // round 5e's grove trees, palms and mangroves: the new trees keep apart from them, the cover off their trunks
            foreach (var rc in rimCells.Values)
                foreach (var (r, s) in rc.srcOf)
                    if (r && s.kind >= 6 && rc.holderGrove && r.transform.IsChildOf(rc.holderGrove.transform)) Mark5d(trees55, new Vector2(r.transform.position.x, r.transform.position.z));
            float gx1 = gx0 + (gnx - 1) * Cell, gz1 = gz0 + (gnz - 1) * Cell;
            // ---- rocks
            float S = RockLattice;
            for (int a = Mathf.FloorToInt(gx0 / S); a <= Mathf.CeilToInt(gx1 / S); a++)
            {
                for (int b = Mathf.FloorToInt(gz0 / S); b <= Mathf.CeilToInt(gz1 / S); b++)
                {
                    float x = a * S + 8f + H3(a, b, 600) / 99f * (S - 16f), z = b * S + 8f + H3(a, b, 601) / 99f * (S - 16f);
                    float d = Beyond(x, z); if (d < RockFrom || d > Reach - 30f) continue;
                    if (PadAt(x, z) < 0.999f) continue;
                    if (Gen(x, z) < waterY + 0.6f) continue;
                    ReliefParts(x, z, d, out _, out float ridge, out _, out _, out _, out _);
                    float chance = Mathf.Clamp01(0.12f + 2.2f * SlopeAt(x, z) + 0.5f * ridge / Mathf.Max(0.001f, RidgeA)) * 100f;
                    if (H3(a, b, 602) >= chance) continue;
                    if (!ClearMarks(x, z, 2f)) continue;
                    var dk = rockSrc[RockKinds55[H3(a, b, 603) % 3]]; if (dk.Count == 0) continue;
                    var src = dk[H3(a, b, 604, dk.Count)];
                    if (!SeatRock(src, x, z, H3(a, b, 605) * 3.6f, out Matrix4x4 m, out float rad)) { LandRefusedRocks++; continue; }
                    AddRock(src, m, x, z, rad, layer);
                    int comp = H3(a, b, 606) % RockCompanions;
                    for (int k = 0; k < comp; k++)
                    {
                        float ang = H3(a, b, 610 + k) * 3.6f * Mathf.Deg2Rad, r = rad + 0.6f + 2.2f * H3(a, b, 615 + k) / 99f;
                        float qx = x + Mathf.Cos(ang) * r, qz = z + Mathf.Sin(ang) * r;
                        if (Gen(qx, qz) < waterY + 0.4f) continue;
                        var md = rockSrc[Mossy55[H3(a, b, 620 + k) % 2]]; if (md.Count == 0) continue;
                        var ms = md[H3(a, b, 625 + k, md.Count)];
                        if (!SeatRock(ms, qx, qz, H3(a, b, 630 + k) * 3.6f, out Matrix4x4 m2, out float rad2)) continue;
                        AddRock(ms, m2, qx, qz, rad2, layer);
                    }
                }
                yield return true;
            }
            // ---- trees and palms: the groves, then a few lone ones
            if (deck55 != null)
            {
                S = GroveLattice;
                for (int a = Mathf.FloorToInt(gx0 / S); a <= Mathf.CeilToInt(gx1 / S); a++)
                {
                    for (int b = Mathf.FloorToInt(gz0 / S); b <= Mathf.CeilToInt(gz1 / S); b++)
                    {
                        float cx = a * S + 6f + H3(a, b, 640) / 99f * (S - 12f), cz = b * S + 6f + H3(a, b, 641) / 99f * (S - 12f);
                        float d = Beyond(cx, cz); if (d < GroveFrom || d > Reach - 25f) continue;
                        if (PadAt(cx, cz) < 0.999f) continue;
                        float ch = 100f * GroveChance * Ramp(-0.2f, 0.35f, Nz(cx / 170f, cz / 170f, 30)) * (1f - 0.5f * Ramp(80f, 230f, d));
                        if (H3(a, b, 642) >= ch) continue;
                        int n = GroveMin + H3(a, b, 643) % GroveMore, got = 0;
                        for (int t = 0; t < LandGroveTries && got < n; t++)
                        {
                            float ang = H3(a, b, 650 + t) * 3.6f * Mathf.Deg2Rad, r = GroveR0 + GroveR * Mathf.Sqrt(H3(a, b, 670 + t) / 100f);
                            if (TreeAt(cx + Mathf.Cos(ang) * r, cz + Mathf.Sin(ang) * r, H3(a, b, 690 + t), H3(a, b, 710 + t, 1000), H3(a, b, 730 + t), H3(a, b, 750 + t), layer)) got++;
                        }
                        if (got > 0) LandGroves++;
                    }
                    yield return true;
                }
                S = LoneLattice;
                for (int a = Mathf.FloorToInt(gx0 / S); a <= Mathf.CeilToInt(gx1 / S); a++)
                {
                    for (int b = Mathf.FloorToInt(gz0 / S); b <= Mathf.CeilToInt(gz1 / S); b++)
                    {
                        float x = a * S + 3f + H3(a, b, 770) / 99f * (S - 6f), z = b * S + 3f + H3(a, b, 771) / 99f * (S - 6f);
                        float d = Beyond(x, z); if (d < GroveFrom || d > Reach - 25f) continue;
                        if (H3(a, b, 772, 1000) >= 1000f * LoneChance * (1f - Ramp(40f, 240f, d))) continue;
                        TreeAt(x, z, H3(a, b, 773), H3(a, b, 774, 1000), H3(a, b, 775), H3(a, b, 776), layer);
                    }
                    yield return true;
                }
            }
            foreach (bool step in BolderSteps()) yield return step;                                  // the forest patches, reeds and thickets (OzL16Outskirts.Bolder.cs)
            // ---- the ground cover
            if (deck55 != null)
            {
                S = CoverStep;
                for (int i = Mathf.FloorToInt(gx0 / S); i <= Mathf.CeilToInt(gx1 / S); i++)
                {
                    for (int j = Mathf.FloorToInt(gz0 / S); j <= Mathf.CeilToInt(gz1 / S); j++)
                    {
                        float x = i * S + 1f + (H3(i, j, 700) / 99f - 0.5f) * 1.6f, z = j * S + 1f + (H3(i, j, 701) / 99f - 0.5f) * 1.6f;
                        float d = Beyond(x, z);
                        if (d < CoverFrom || d > CoverOut || x <= gx0 || x >= gx1 || z <= gz0 || z >= gz1) continue;
                        float band = Mathf.Lerp(1f, CoverFar, Ramp(12f, CoverThin, d));
                        float patch = Ramp(0f, 0.42f, Nz(x / 24f, z / 24f, 20) + 0.45f * Nz(x / 9f, z / 9f, 21));
                        float glade = 1f - 0.9f * Ramp(0.15f, 0.45f, Nz(x / 65f, z / 65f, 22));
                        float dens = CoverMax * band * patch * glade * (1f + Ramp(0.03f, 0.18f, SlopeAt(x, z)));
                        if (H3(i, j, 702, 1000) >= 1000f * dens) continue;
                        TintParts(x, Gen(x, z), z, 1f, d, out _, out _, out _, out float path, out _, out _);
                        if (path > 0.35f) continue;
                        if (!ClearMarks(x, z, 0f)) continue;
                        var p = new Vector2(x, z);
                        if (Near5d(trees55, p, UnderFromTree) || NearRock(x, z, 0.3f)) continue;
                        bool wet = WetFoot(p, out float y);
                        if (!wet && !Foot(p, out y, out _, out _)) continue;
                        int kind = wet ? Pick55(WetMix, WetMixW, H3(i, j, 703)) : Pick55(DryMix, DryMixW, H3(i, j, 703));
                        if (kind == 2 && d > BananaOut) kind = 0;                                          // the bananas near the rim only (their model is the dearest)
                        var dk = deck55[kind]; if (dk.Count == 0) continue;
                        var src = dk[H3(i, j, 704, 1000) % dk.Count];
                        AddPlant(src, new Vector3(x, y, z), H3(i, j, 705) * 3.6f, wet, layer, ShadowCastingMode.Off);   // the ground cover casts no shadow (cost)
                        LandCover++; if (wet) LandCoverWet++;
                    }
                    if ((i & 3) == 0) yield return true;
                }
            }
            foreach (bool step in MistAhead()) yield return step;                                     // the mist's far points ahead (FarMist is made at once)
            landMade = true;
        }

        bool TreeAt(float x, float z, int hKind, int hSrc, int hYaw, int hWet, int layer)
        {
            float d = Beyond(x, z); if (d < GroveFrom - 4f || d > Reach - 25f) return false;
            if (!ClearMarks(x, z, 0f)) return false;
            var p = new Vector2(x, z);
            if (Near5d(trees55, p, TreeSpacing) || NearRock(x, z, 1.5f)) return false;
            int kind; float y;
            if (WetFoot(p, out y)) kind = hWet < 60 ? RimMangrove : 5;
            else { if (!Foot(p, out y, out _, out _)) return false; kind = Pick55(RimTreeMix, RimTreeMixW, hKind); }
            var dk = deck55[kind]; if (dk.Count == 0) return false;
            var src = dk[hSrc % dk.Count];
            bool wet = Gen(x, z) <= waterY + WetOver;
            AddPlant(src, new Vector3(x, y, z), hYaw * 3.6f, wet, layer, src.cast);                                     // as the map's own tree
            Mark5d(trees55, p); LandTrees++; if (wet) LandTreesWet++;
            return true;
        }

        void AddPlant(RimSource src, Vector3 at, float yaw, bool wet, int layer, ShadowCastingMode cast)
        {
            var l = LotOf(src.mesh, MatsOf(src.mesh, src.mats), cast, layer, RimKinds[src.kind]);
            if (l.src == null) l.src = src;
            AddInstance(l, Matrix4x4.TRS(at, Quaternion.AngleAxis(yaw, Vector3.up) * src.rot, src.scale), at.y, wet, src.tw);   // a turn about the upright keeps its source's true crown
        }

        // ------------------------------------------------------------------ show (with the land)
        /// <summary>BuildSliced, at its end (after RimFixApply): round 5e's groves and the ring's game plants get instanced models of their
        /// own (the same meshes, places, sizes and materials; their objects stay, held off only while their copies draw: Hold5e; the low
        /// look, a flyover's last shot and everything from the ending on, the fort's blast included, have the objects as before), the models
        /// are cut into chunks; the counts.</summary>
        void LandscapeApply()
        {
            Land5eGroves = Land5eRing = 0; groves5eOff.Clear(); ring5e.Clear(); held5e = false; ending5e = false; tintN = null;
            if (!LandscapeOn || !landMade) return;
            foreach (var rc in rimCells.Values)
            {
                if (!rc.applied) continue;
                var srcOfR = new Dictionary<Renderer, RimSource>(); foreach (var (rr, ss) in rc.srcOf) if (rr) srcOfR[rr] = ss;
                for (int which = 0; which < 2; which++)
                {
                    var h = which == 0 ? rc.holderGrove : rc.holder; if (!h) continue;
                    foreach (var r in h.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var mf = r ? r.GetComponent<MeshFilter>() : null; if (!mf || !mf.sharedMesh || !srcOfR.ContainsKey(r)) continue;   // the new plants of round 5e only
                        var l = LotOf(mf.sharedMesh, r.sharedMaterials, r.shadowCastingMode, r.gameObject.layer, RimBaseName(mf.sharedMesh), true);   // its own materials (the wind's copy for its cell), as its object draws
                        float fy = r.transform.position.y; bool wet = which == 0 && Gen(r.transform.position.x, r.transform.position.z) <= waterY + WetOver;
                        AddInstance(l, r.transform.localToWorldMatrix, which == 0 ? fy : float.NaN, wet, srcOfR[r].tw);
                        if (which == 0) { groves5eOff.Add(r); Land5eGroves++; }                         // held off with the ring's while the land is shown (Hold5e)
                        else { ring5e.Add(r); Land5eRing++; }
                    }
                }
            }
            // per model: its instances in chunk order; per chunk the pieces of every model
            var byChunk = new Dictionary<int, Chunk>(); LandTrisAll = 0; LandPieces = 0;
            foreach (var l in lots)
            {
                int n = l.list.Count; var order = new int[n]; for (int i = 0; i < n; i++) order[i] = i;
                System.Array.Sort(order, (p, q) => l.chunkOf[p] != l.chunkOf[q] ? l.chunkOf[p].CompareTo(l.chunkOf[q]) : p.CompareTo(q));
                l.m = new Matrix4x4[n]; l.buf = new Matrix4x4[n];
                var boxes = new Bounds[n]; var feet = new float[n]; var crowns = new float[n]; var wet = new byte[n]; var ch = new int[n];
                for (int i = 0; i < n; i++) { l.m[i] = l.list[order[i]]; boxes[i] = l.boxes[order[i]]; feet[i] = l.feet[order[i]]; crowns[i] = l.crowns[order[i]]; wet[i] = l.wet[order[i]]; ch[i] = l.chunkOf[order[i]]; }
                l.list.Clear(); l.list.AddRange(l.m); l.boxes.Clear(); l.boxes.AddRange(boxes); l.feet.Clear(); l.feet.AddRange(feet); l.crowns.Clear(); l.crowns.AddRange(crowns);
                l.wet.Clear(); l.wet.AddRange(wet); l.chunkOf.Clear(); l.chunkOf.AddRange(ch);
                LandTrisAll += l.tris * n;
                for (int i = 0; i < n;)
                {
                    int k = i; var pc = new Piece { lot = l, start = i, box = boxes[i] };
                    while (k < n && ch[k] == ch[i]) { pc.box.Encapsulate(boxes[k]); pc.h = Mathf.Max(pc.h, boxes[k].size.y); k++; }
                    pc.count = k - i; i = k; LandPieces++;
                    if (!byChunk.TryGetValue(ch[pc.start], out var c)) { byChunk[ch[pc.start]] = c = new Chunk { box = pc.box }; chunks.Add(c); }
                    c.box.Encapsulate(pc.box); c.pieces.Add(pc);
                    if (l.cast != ShadowCastingMode.Off) c.hCast = Mathf.Max(c.hCast, pc.h);               // its tallest shadow caster (the shadow's reach on a low sun)
                }
            }
            LandLots = lots.Count; LandChunks = chunks.Count;
            Debug.Log($"[OzL16Outskirts] landscape: relief {(gh != null ? "on" : "off")} (far land {FarLandTris} triangles, {FarLandMin:0.0} to {FarLandMax:0.0} m), ground cover {LandCover} (by the water {LandCoverWet}), trees and palms {LandTrees} in {LandGroves} groves and alone (by the water {LandTreesWet}), rocks {LandRocks} (steep places refused {LandRefusedRocks}), round 5e's grove plants drawn instanced {Land5eGroves}; {LandLots} models, {LandChunks} chunks, {LandPieces} pieces, {LandTrisAll} triangles in all{(LandWhy.Length > 0 ? "; " + LandWhy : "")}");
        }

        /// <summary>LateUpdate: the land shown (the low look off) or standing back. What is drawn follows the same frame's state in
        /// LandscapeDraw (it runs before this in LateUpdate and reads the same low look), so no frame of a switch draws a plant twice or
        /// misses one.</summary>
        void LandscapeShow(bool on)
        {
            landShown = on && LandscapeOn && landMade;
            Hold5e(landShown && !ending5e && !Flying());
        }

        /// <summary>Round 5e's ring and grove plants: their objects held off while their instanced copies draw (held), else drawn as they
        /// are (the low look; a flyover's last shot, whose other cameras only objects reach; from the ending on, for the fort's blast to
        /// throw them).</summary>
        void Hold5e(bool held)
        {
            if (held == held5e) return;
            held5e = held;
            foreach (var r in ring5e) if (r) r.forceRenderingOff = held;
            foreach (var r in groves5eOff) if (r) r.forceRenderingOff = held;
        }

        /// <summary>A flyover holds the game camera (the start briefing, its last shot too).</summary>
        static bool Flying() { var f = ZodFlyover.Live; return f != null && f.Active; }

        /// <summary>The level's sun (the shadows' direction), looked up again every 10 s while none is found.</summary>
        Light LandSun()
        {
            if (landSun || Time.unscaledTime < landSunAt) return landSun;
            landSunAt = Time.unscaledTime + 10f;
            landSun = RenderSettings.sun;
            if (!landSun) { var dn = FindFirstObjectByType<DayNight>(); if (dn) landSun = dn.sun; }
            return landSun;
        }

        /// <summary>LateUpdate, every frame: the visible chunks' instances, one draw per model and material per 1023.</summary>
        void LandscapeDraw()
        {
            BenchTilt();
            LandCallsNow = LandLotsNow = LandInstNow = 0; LandTrisNow = 0;
            bool shown = LandscapeOn && landMade && !OzL16FlyoverFlora.LowLookOn;                     // this frame's state (LateUpdate's switch after this hook reads the same low look)
            if (!ending5e && OpenZEnding.Running) ending5e = true;                                      // from the ending on round 5e's plants are their own objects: the fort's blast throws them (OzTreeFall.AddBlastFlora)
            Hold5e(shown && !ending5e && !Flying());
            if (!shown || lots.Count == 0) return;
            if (!landCam) landCam = rts ? rts.GetComponentInChildren<Camera>() : Camera.main;
            var cam = landCam; if (!cam || !cam.isActiveAndEnabled) return;
            GeometryUtility.CalculateFrustumPlanes(cam, landPlanes);
            Vector3 cp = cam.transform.position; float k = cam.pixelHeight / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            // a shadow's reach on the ground per metre of a caster's height, away from the sun
            Vector3 sh = Vector3.zero; var sun = LandSun();
            if (sun && sun.isActiveAndEnabled && sun.shadows != LightShadows.None) { var f = sun.transform.forward; if (f.y < -0.02f) sh = new Vector3(f.x, 0f, f.z) / -f.y; }
            // the wind's room round a plant (OzFloraWind.boundsPad: a small plant's; every plant out here is at most about 6 m tall, and the
            // lean grows with the height: a 13 m tree's 4.7 m is about 2.2 m at 6 m), except in the wide overview, as round 5e's objects (RimFixBoxes)
            float pad = !rimOwnBox && OzFloraWind.I ? OzFloraWind.I.boundsPad.y : 0f;
            bool skip5e = !held5e;                                                                     // round 5e's plants drawn as their objects now
            foreach (var l in lots) l.drawnNow = 0;
            foreach (var c in chunks)
            {
                var vb = c.box; if (pad > 0f) vb.Expand(2f * pad);
                bool inView = GeometryUtility.TestPlanesAABB(landPlanes, vb);
                if (!inView)
                {   // just outside the picture only a shadow caster draws: ShadowPad round it, and on the sun's side its shadow's reach
                    if (c.hCast <= 0f) continue;
                    var cb = c.box; cb.Expand(new Vector3(2f * ShadowPad, 0f, 2f * ShadowPad));
                    var off = sh * c.hCast; float om = off.magnitude; if (om > ShadowReach) off *= ShadowReach / om;
                    cb.Encapsulate(c.box.min + off); cb.Encapsulate(c.box.max + off);
                    if (!GeometryUtility.TestPlanesAABB(landPlanes, cb)) continue;
                }
                float dist = Mathf.Max(0.5f, Mathf.Sqrt(c.box.SqrDistance(cp)));
                foreach (var p in c.pieces)
                {
                    var l = p.lot;
                    if (skip5e && l.r5e) continue;
                    if (!inView && l.cast == ShadowCastingMode.Off) continue;                               // just outside the picture only a shadow caster draws
                    if (p.h * k / dist < MinPx) continue;                                                  // under MinPx on the screen
                    System.Array.Copy(l.m, p.start, l.buf, l.drawnNow, p.count);
                    var pb = p.box; if (pad > 0f) pb.Expand(2f * pad);
                    if (l.drawnNow == 0) l.boxNow = pb; else l.boxNow.Encapsulate(pb);
                    l.drawnNow += p.count;
                }
            }
            foreach (var l in lots)
            {
                if (l.drawnNow == 0) continue;
                LandLotsNow++; LandInstNow += l.drawnNow; LandTrisNow += l.tris * l.drawnNow;
                int subs = Mathf.Min(l.mats.Length, l.mesh.subMeshCount);
                for (int s = 0; s < subs; s++)
                {
                    if (!l.mats[s]) continue;
                    var rp = new RenderParams(l.mats[s]) { camera = cam, layer = l.layer, shadowCastingMode = l.cast, receiveShadows = true, lightProbeUsage = LightProbeUsage.BlendProbes, worldBounds = l.boxNow, motionVectorMode = MotionVectorGenerationMode.Camera };
                    for (int o = 0; o < l.drawnNow; o += Batch) { Graphics.RenderMeshInstanced(rp, l.mesh, s, l.buf, Mathf.Min(Batch, l.drawnNow - o), o); LandCallsNow++; }
                }
            }
        }

        /// <summary>The exe bench with -oz-l16-land-freetilt: each of its views turned out from the map's middle at that free tilt.</summary>
        void BenchTilt()
        {
            if (benchTilt <= 0f || !OzFpsBench.Running || !rts) return;
            var off = new Vector2(rts.focus.x - map.x * 0.5f, rts.focus.z + map.y * 0.5f);
            float heading = off.sqrMagnitude > 1f ? Mathf.Atan2(off.x, off.y) * Mathf.Rad2Deg : 0f;
            float turn = Mathf.DeltaAngle(rts.yaw, heading), tilt = rts.Tilt - benchTilt;
            if (Mathf.Abs(turn) < 0.5f && Mathf.Abs(tilt) < 0.5f) return;
            if (rts.FreeTurn(new Vector2(turn / RtsCamera.FreeLookSpeed, tilt / RtsCamera.FreeLookSpeed))) rts.Place();
            else if (!benchTiltSaid) { benchTiltSaid = true; Debug.LogWarning("[OzL16Outskirts] -oz-l16-land-freetilt: the free tilt was refused (turn the FREE CAMERA option on)"); }
        }

        // ------------------------------------------------------------------ Play checks
        static string LandscapeStatus() => !LandscapeOn ? ", landscape off" + (LandFrom.Length > 0 ? " (" + LandFrom + ")" : "") :
            $", landscape: cover {LandCover} (wet {LandCoverWet}), trees {LandTrees} in {LandGroves} groves (wet {LandTreesWet}), rocks {LandRocks}, round 5e's instanced: groves {Land5eGroves}, ring {Land5eRing} ({(I && I.held5e ? "their copies drawn" : "their own objects drawn")}{(I && I.ending5e ? ", the ending" : "")}), {LandLots} models in {LandChunks} chunks, far land {FarLandTris} tris; drawn now {LandCallsNow} calls, {LandLotsNow} models, {LandInstNow} instances, {LandTrisNow} tris";

        /// <summary>Play check of the landscape (the land shown, the low look off): the relief by band and the edge unchanged, the water,
        /// the cover and trees by band per hectare and by kind, every plant one of the map's full models under the crown cap (no '_lite' or
        /// far shape), every foot on the land, every rock seated, nothing on the plateau, and the drawing now (calls, models, instances,
        /// triangles: the instanced ones and the camera's whole frame).</summary>
        public static string OutskirtsLandscapeCheck()
        {
            var o = I; if (!o) return "OzL16Outskirts landscape: not on this level";
            if (!o.built) return "OzL16Outskirts landscape: the land is not built yet";
            if (!LandscapeOn) return "OzL16Outskirts landscape: OFF" + (LandFrom.Length > 0 ? " (" + LandFrom + ")" : "") + " (the land as before)";
            if (OzL16FlyoverFlora.LowLookOn) return "OzL16Outskirts landscape: the low look holds (wait for the start flyover's end)";
            var sb = new StringBuilder(); int fail = 0; float m = OzL16FlyoverFlora.GroundMargin;
            // the relief by band (the land's grid beyond the plateau), the slopes, the water
            var bands = new[] { 8f, 40f, 120f, 250f }; var lo = new float[3]; var hi = new float[3]; var mean = new double[3]; var cnt = new int[3]; float steep = 0f; int water = 0, cells = 0;
            for (int b = 0; b < 3; b++) { lo[b] = float.MaxValue; hi[b] = float.MinValue; }
            for (int j = 0; j < o.gnz; j++)
                for (int i = 0; i < o.gnx; i++)
                {
                    float x = o.gx0 + i * Cell, z = o.gz0 + j * Cell, d = o.Beyond(x, z); if (d <= 0f) continue;
                    float h = o.gh[j * o.gnx + i];
                    for (int b = 0; b < 3; b++) if (d > bands[b] && d <= bands[b + 1]) { lo[b] = Mathf.Min(lo[b], h); hi[b] = Mathf.Max(hi[b], h); mean[b] += h; cnt[b]++; }
                    if (d > 40f && i + 1 < o.gnx && j + 1 < o.gnz)
                    {
                        float sx = (o.gh[j * o.gnx + i + 1] - h) / Cell, sz = (o.gh[(j + 1) * o.gnx + i] - h) / Cell; steep = Mathf.Max(steep, Mathf.Sqrt(sx * sx + sz * sz));
                        cells++; float l4 = Mathf.Min(Mathf.Min(h, o.gh[j * o.gnx + i + 1]), Mathf.Min(o.gh[(j + 1) * o.gnx + i], o.gh[(j + 1) * o.gnx + i + 1])); if (l4 < o.waterY + 0.02f) water++;
                    }
                }
            sb.Append(" | RELIEF (m):");
            for (int b = 0; b < 3; b++) sb.Append($" {bands[b]:0}-{bands[b + 1]:0} m beyond the plateau {lo[b]:0.0} to {hi[b]:0.0} (mean {(cnt[b] > 0 ? mean[b] / cnt[b] : 0):0.0});");
            sb.Append($" far land {FarLandMin:0.0} to {FarLandMax:0.0}; steepest grid step beyond 40 m {steep:0.00} (as the map's own banks at most 0.8); water {100f * water / Mathf.Max(1, cells):0.0} % of the land beyond 40 m");
            if (steep > 0.8f) fail++;
            // the edge unchanged: the land within 6 m of the plateau's edge with the landscape and without it
            float worstEdge = 0f; int edgePts = 0;
            for (float s = 0f; s < o.perim; s += 4f)
            {
                o.EdgePoint(s, out float ex, out float ez, out float ox, out float oz);
                foreach (float dd in new[] { 1f, 3f, 6f })
                {
                    float x = ex + ox * dd, z = ez + oz * dd;
                    float h4; LandscapeOn = false; try { h4 = o.HeightAt(x, z); } finally { LandscapeOn = true; }
                    float h5 = o.HeightAt(x, z);
                    worstEdge = Mathf.Max(worstEdge, Mathf.Abs(h5 - h4)); edgePts++;
                }
            }
            bool edgeOk = worstEdge <= 0.02f; if (!edgeOk) fail++;
            sb.Append($" | EDGE: the land within 6 m of the plateau's edge moved at most {worstEdge:0.000} m from round 4's ({edgePts} points; at most 0.02) {(edgeOk ? "ok" : "FAIL")}");
            // every instance: its model, its crown, its foot, its place
            int lite = 0, over = 0, floating = 0, onPlateau = 0, rocksBad = 0, plants = 0, rocks = 0; float sunkMax = 0f, overMax = -1f, rockGap = float.MinValue, widest = 0f; string liteName = "", overName = "", floatName = "";
            var bandT = new int[3]; var bandC = new int[3]; var kinds = new SortedDictionary<string, int>();
            foreach (var l in o.lots)
            {
                string mn = l.mesh ? l.mesh.name : "";
                bool isRock = System.Array.IndexOf(RockKinds55, mn) >= 0 || System.Array.IndexOf(Mossy55, mn) >= 0;
                if (mn.Contains("_lite") || mn.Contains("_far")) { lite += l.list.Count; if (liteName.Length == 0) liteName = mn; }
                for (int i = 0; i < l.list.Count; i++)
                {
                    var M = l.list[i]; Vector3 p = M.GetColumn(3);
                    bool ring = !isRock && float.IsNaN(l.feet[i]);                                       // a ring plant of round 5e: on the rim floor by its own rule (RimFixCheck judges it)
                    if (o.Outside(p) <= m + 0.01f && !ring) onPlateau++;
                    if (isRock)
                    {
                        rocks++; var b = l.boxes[i]; float g = float.MaxValue;
                        for (int a = -1; a <= 1; a++) for (int c = -1; c <= 1; c++) g = Mathf.Min(g, o.Gen(b.center.x + a * 0.6f * b.extents.x, b.center.z + c * 0.6f * b.extents.z));
                        float gap = b.min.y - g; rockGap = Mathf.Max(rockGap, gap); if (gap > 0.001f) rocksBad++;
                        continue;
                    }
                    plants++;
                    float w = l.crowns[i] >= 0f ? l.crowns[i] : TrueWidth(l.mesh, Matrix4x4.TRS(Vector3.zero, M.rotation, M.lossyScale));   // its source's true crown (a turn about the upright keeps it)
                    if (w > RimCap + 0.005f) { over++; if (overName.Length == 0) overName = $"{mn} {w:0.00} m"; }
                    widest = Mathf.Max(widest, w);
                    float y = p.y, hh = o.Gen(p.x, p.z);
                    if (!ring && l.wet[i] == 1) { if (y > hh + 0.001f) { floating++; if (floatName.Length == 0) floatName = $"{mn} +{y - hh:0.00} m"; } }
                    else if (!ring)
                    {
                        float under = o.Under(p.x, p.z, 64); sunkMax = Mathf.Max(sunkMax, hh - y); overMax = Mathf.Max(overMax, y - under);
                        if (y - under > 0.001f) { floating++; if (floatName.Length == 0) floatName = $"{mn} +{y - under:0.00} m"; }
                    }
                    float d = o.Beyond(p.x, p.z); bool tree = l.src != null ? l.src.kind >= 6 : System.Array.IndexOf(RimKinds, RimBaseName(l.mesh)) >= 6;
                    for (int b = 0; b < 3; b++) if (d > (b == 0 ? 0f : bands[b]) && d <= bands[b + 1]) { if (tree) bandT[b]++; else bandC[b]++; }
                    string kn = RimBaseName(l.mesh); kinds.TryGetValue(kn, out int kc); kinds[kn] = kc + 1;
                }
            }
            if (lite > 0) fail++; if (over > 0) fail++; if (floating > 0) fail++; if (onPlateau > 0) fail++; if (rocksBad > 0) fail++; if (LandCover == 0 || LandTrees == 0 || LandRocks == 0) fail++;
            sb.Append($" | MODELS: {lite} '_lite' or far shapes{(lite > 0 ? " (" + liteName + ")" : "")}, crowns over the cap {RimCap:0.00} m {over}{(over > 0 ? " (" + overName + ")" : "")} (widest {widest:0.00} m = x{widest / FactoryWidth:0.00} of a factory)");
            sb.Append($" | FEET: {plants} plants, floating {floating}{(floating > 0 ? " (" + floatName + ")" : "")} (deepest {sunkMax:0.000} m under the land, highest over it round the stem {overMax:0.000} m, at most 0), rocks {rocks} seated (lowest point over the land under it at most {(rocks > 0 ? rockGap : 0f):+0.000;-0.000} m, not seated {rocksBad}); on the plateau {onPlateau}");
            sb.Append(" | PER HECTARE beyond the plateau (trees and palms / ground cover, with round 5e's groves):");
            float W = o.px1 - o.px0, D = o.pz1 - o.pz0;
            for (int b = 0; b < 3; b++) { float a0 = b == 0 ? 0f : bands[b], a1 = bands[b + 1], area = ((W + 2f * a1) * (D + 2f * a1) - (W + 2f * a0) * (D + 2f * a0)) / 1e4f; sb.Append($" {a0:0}-{a1:0} m {bandT[b] / area:0.0} / {bandC[b] / area:0}"); }
            sb.Append($" | MADE: cover {LandCover} (by the water {LandCoverWet}), trees and palms {LandTrees} in {LandGroves} groves and alone (by the water {LandTreesWet}), rocks {LandRocks} (refused on steep places {LandRefusedRocks}); round 5e's plants drawn instanced: groves {Land5eGroves}, ring {Land5eRing} (their objects held off with the land: {RingHeld()} of {Land5eRing}); by model:");
            foreach (var kv in kinds) sb.Append($" {kv.Key} {kv.Value}");
            sb.Append($" | DRAWING now: {LandCallsNow} instanced calls, {LandLotsNow} of {LandLots} models, {LandInstNow} instances, {LandTrisNow} triangles (all {LandTrisAll}); {LandChunks} chunks, {LandPieces} pieces; far land {FarLandTris} triangles ({FarLandPoints} points), mist {MistTriangles} triangles");
            return $"OzL16Outskirts landscape: {(fail == 0 ? "PASS" : "FAIL")}" + sb;
        }

        /// <summary>The ring's own objects held off now (while the land is shown: all of them).</summary>
        static int RingHeld() { int n = 0; if (I) foreach (var r in I.ring5e) if (r && r.forceRenderingOff) n++; return n; }

        /// <summary>Play helper for the pictures: the instanced drawing now and the frame's own counters.</summary>
        public static string LandscapeViewCount() => $"landscape drawn now: {LandCallsNow} calls, {LandLotsNow} models, {LandInstNow} instances, {LandTrisNow} triangles";
    }
}
