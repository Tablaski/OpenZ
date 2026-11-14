// OpenZ (GPL-3.0) - Level 16: the land beyond the rim made bolder (testing the free camera behind the red fort.
// On top of the landscape (OzL16Outskirts.Landscape.cs), with LandBold on (the default):
//  - RELIEF that reads from the widest free camera: near the rim (6 m out, full from 30 m) rolling knolls of 4 to 10 m and hollows; from
//    90 m on rolling hills and long ridges of 15 to 40 m, out over the far land too; a broad valley with a winding creek in it; the
//    hills and knolls are calmer where the landscape's own creek runs (its banks stay as they were); the land within 6 m of the plateau's
//    edge does not move at all, and the landmarks and the flume ends keep their own land (PadBold);
//  - SWAMPS: lowland basins pulled to the water's level with islands and channels (the level's own swamp water fills them), wide and
//    shallow, along the creek too; none near the land grid's edge (the far land has no water);
//  - FOREST PATCHES: dense patches of the map's own small trees and palms (every crown at most RimCap, the map's full models at their own
//    sizes), 4 or 5 a side, 14 to 24 m across their middle, bananas round their edge, mangroves and young palms where they stand wet, with
//    open land between; the ground under them a dark forest floor;
//  - REEDS and THICKETS: the map's own reeds (a few mangroves and young palms) along the swamps' and the creek's edges, clumps of the map's
//    own bushes (a few ferns and bananas) near the rim;
//  - GROUND COLOUR: deep moss, bare earth on the slopes, sunny dry tops, a swamp olive, the dark forest floor (on the landscape's tints);
//  - the outskirts' mist a little lighter (BoldMist), so the new land shows through it.
// Every plant goes into the landscape's own instanced models (Graphics.RenderMeshInstanced per 32 m chunk, the 2 px rule), the reeds and
// the thickets cast no shadow. Drawn only: no rule reads any of it. Nothing on the plateau or inside the map changes.
// Switch: LandBold (read when the level starts; off = the landscape as before). Command line: -oz-l16-land k55b (this) | k55 (the
// landscape without it) | r5e (neither). Play check: return OpenZ.OzL16Outskirts.OutskirtsBolderCheck();
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public sealed partial class OzL16Outskirts
    {
        /// <summary>On (default): the bolder land. Off: the landscape as before. Set before the level starts.</summary>
        public static bool LandBold = true;
        /// <summary>The relief (m; m beyond the plateau's edge; m of noise scale): knolls (height, scale, from - full), hollows, the swamps'
        /// basins (scale, from, the islands' height and scale), the far hills (height, scale, from - full), the long ridges (height, scale,
        /// width in noise units), the valley (its line's scale, width in noise units, how far out), the creek's channel width, where the
        /// creek starts, how much calmer the knolls and hills are in the landscape's own creek regions, the landmarks' pad (m beyond a mark).</summary>
        public static float KnollA = 4.6f, KnollScale = 40f, KnollFrom = 6f, KnollFull = 30f, HollowA = 3f, HollowScale = 36f;
        public static float SwampScale = 260f, SwampFrom = 12f, SwampDepth = 0.30f, SwampIsle = 12f;
        public static float HillA = 15f, HillScale = 180f, HillFrom = 90f, HillFull = 210f, LongA = 20f, LongScale = 340f, LongWidth = 0.17f;
        public static float ValleyScale = 360f, ValleyW = 0.40f, ValleyOut = 330f, ChannelW = 0.025f, CreekBFrom = 35f, CreekCalm = 0.6f, BoldPadOut = 34f;
        /// <summary>The swamps' shores wander, so no shore runs along the plateau's edge in a line: the near shore's ramp is 35 m x
        /// exp(ShoreWob x the wander + ShoreBias) wide (it still starts SwampFrom m out), the far shore ShoreFarIn m nearer on average and by
        /// up to about ShoreFarWob m; the wanders' noise scales (m).</summary>
        public static float ShoreWob = 1f, ShoreBias = 0.2f, ShoreWobScale = 55f, ShoreFarIn = 15f, ShoreFarWob = 30f, ShoreFarScale = 70f;
        /// <summary>The outskirts' mist far out with the bolder land (the landscape's LandMist 0.36: it hid the new land).</summary>
        public static float BoldMist = 0.22f;
        /// <summary>The forest patches: candidates on a lattice (m), from - out (m beyond the plateau), a patch's radius (m: R0 + up to R),
        /// its trees' lattice and jitter (m), the least room between trees (m), the steepest foot (rise per m), patches a side (+0 or 1),
        /// the least distance between patches (m); the mix of kinds (RimKinds: the three canopy trees, the fan, slender and tall palms).</summary>
        public static float ForestLattice = 64f, ForestFrom = 45f, ForestOut = 200f, ForestR0 = 14f, ForestR = 10f, ForestStep = 3.6f, ForestJitter = 2f, ForestGap = 2.4f, ForestSteep = 0.5f, ForestApart = 76f;
        public static int ForestPerSide = 4;
        static readonly int[] ForestMix = { 11, 12, 13, 8, 6, 7 }, ForestMixW = { 24, 18, 24, 18, 10, 6 };
        /// <summary>The reeds by the water (lattice m, from - out m, density) and the thickets near the rim (lattice, from - out, density,
        /// the clumps' noise scale), their mixes (RimKinds: reeds B, reeds A, young palms, mangroves; bushes, bananas, ferns).</summary>
        public static float ReedStep = 2.5f, ReedFrom = 20f, ReedOut = 230f, ReedDensity = 0.45f;
        public static float ThicketStep = 2.6f, ThicketFrom = 12f, ThicketOut = 120f, ThicketDensity = 0.55f, ThicketScale = 30f;
        static readonly int[] ReedMix = { 4, 3, 5, 10 }, ReedMixW = { 48, 43, 3, 6 }, ThicketMix = { 1, 2, 0 }, ThicketMixW = { 86, 2, 12 };
        /// <summary>The ground's tints (multiplied on the landscape's colours): deep moss, bare earth, sunny dry tops, swamp olive, forest floor.</summary>
        public static Color TintDeepMoss = new Color(0.42f, 0.58f, 0.36f), TintBareEarth = new Color(0.84f, 0.68f, 0.52f), TintSunTop = new Color(1.24f, 1.14f, 0.74f),
            TintSwampOlive = new Color(0.72f, 0.80f, 0.56f), TintForestFloor = new Color(0.36f, 0.44f, 0.28f);
        /// <summary>Counters (this level): forest patches by side (N, E, S, W), their trees, the reeds, the thickets, refused places.</summary>
        public static int BoldPatchCount, BoldTrees, BoldTreesWet, BoldReeds, BoldThickets;
        public static readonly int[] BoldPerSide = new int[4];
        public static string BoldFrom = "";

        readonly List<Vector4> boldPatches = new List<Vector4>();   // x, z, radius, side
        bool boldPatchesMade;
        float[] boldN;                                              // per grid point: the tints' moss noise, dry noise, the swamp's share (NaN: not made)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void BolderResetStatics()
        {   // every Play session starts with the bolder land on (the switch is read when the level starts)
            LandBold = true; BoldPatchCount = BoldTrees = BoldTreesWet = BoldReeds = BoldThickets = 0; System.Array.Clear(BoldPerSide, 0, 4); BoldFrom = "";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BolderArgs()
        {   // -oz-l16-land k55b: the bolder land (the default); k55 or r5e: without it (the landscape's own switch reads the same argument)
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < a.Length; i++)
            {
                if (a[i] != "-oz-l16-land") continue;
                string w = a[i + 1].ToLowerInvariant();
                if (w == "k55b") { LandBold = true; LandscapeOn = true; BoldFrom = "command line k55b"; Debug.Log("[OzL16Outskirts] level 16 land beyond the map: the bolder landscape (command line k55b)"); }
                else if (w == "k55" || w == "r5e") { LandBold = false; BoldFrom = "command line " + w; }
            }
        }

        // ------------------------------------------------------------------ the relief (HeightAt's and FarY's hook)
        /// <summary>The landmarks' and the flume ends' pad for the bolder land: 0 at a landmark beyond the rim (its mark) and at a flume end's
        /// rocks, 1 from BoldPadOut m beyond them.</summary>
        float PadBold(float x, float z)
        {
            float p = 1f;
            foreach (var c in marks)
            {
                float dx = x - c.x, dz = z - c.y, r = c.z + BoldPadOut;
                if (dx * dx + dz * dz >= r * r) continue;
                p = Mathf.Min(p, Ramp(PadIn, BoldPadOut, Mathf.Sqrt(dx * dx + dz * dz) - c.z));
            }
            foreach (var f in flumeEnds)
            {
                float dx = x - (f.at.x + f.o.x * 2f), dz = z - (f.at.z + f.o.z * 2f), r = FlumePad + BoldPadOut;
                if (dx * dx + dz * dz >= r * r) continue;
                p = Mathf.Min(p, Ramp(0f, BoldPadOut, Mathf.Sqrt(dx * dx + dz * dz) - FlumePad));
            }
            return p;
        }

        /// <summary>A shore's wander at (x, z): two octaves of noise (seed s and s + 1), about -1..1.</summary>
        static float ShoreWobAt(float x, float z, float sc, int s) => Nz(x / sc, z / sc, s) + 0.5f * Nz(x / (sc * 0.45f), z / (sc * 0.45f), s + 1);

        /// <summary>The swamps' lowland basins (0..1): a slow noise, from SwampFrom m on, the near shore wandering (no hills in them, out
        /// to the far land too).</summary>
        static float SwampRegion(float x, float z, float d)
        {
            float w = 35f * Mathf.Exp(ShoreWob * ShoreWobAt(x, z, ShoreWobScale, 87) + ShoreBias);           // the near shore's ramp: its width wanders
            return Ramp(-0.25f, 0.30f, Nz(x / SwampScale, z / SwampScale, 65)) * Ramp(SwampFrom, SwampFrom + w, d);
        }

        /// <summary>The bolder relief's parts at (x, z), d m beyond the plateau: knolls, hollows (negative), the far hills and long ridges, the
        /// basins' share, the water's share (the basins and the creek's banks), the valley's share, the creek's channel share.</summary>
        static void BoldParts(float x, float z, float d, out float knoll, out float hollow, out float hills, out float basin, out float swamp, out float valley, out float channel)
        {
            knoll = hollow = hills = valley = channel = 0f;
            float e = Ramp(KnollFrom, KnollFull, d) * (1f - Ramp(190f, Reach, d));                         // the near terms: none at the edge, none on the far land
            if (e > 0f)
            {
                float ks = KnollScale, nk = Nz(x / ks, z / ks, 61) + 0.5f * Nz(x / (ks * 0.43f), z / (ks * 0.43f), 62), kk = Mathf.Max(0f, nk + 0.5f);
                knoll = KnollA * e * kk * Mathf.Sqrt(kk);
                float hs = HollowScale, nh = Nz(x / hs, z / hs, 63) + 0.25f * Nz(x / (hs * 0.5f), z / (hs * 0.5f), 64);
                hollow = -HollowA * e * Ramp(0.12f, 0.85f, nh) * (1f - Ramp(Reach - 85f, Reach - 50f, d));
            }
            float fe = Ramp(HillFrom, HillFull, d);
            if (fe > 0f)
            {
                float fine = 1f - Ramp(300f, 450f, d), hsc = HillScale;
                float hh = Mathf.Max(0f, Nz(x / hsc, z / hsc, 68) + 0.35f * fine * Nz(x / (hsc * 0.45f), z / (hsc * 0.45f), 69) + 0.3f);
                float wx = 0.25f * Nz(x / 140f, z / 140f, 72), wz = 0.25f * Nz(x / 140f + 4.1f, z / 140f - 1.7f, 73);
                float nr = Nz(x / LongScale + wx, z / LongScale + wz, 70) / LongWidth;
                hills = fe * (HillA * hh * Mathf.Sqrt(hh) + LongA * Mathf.Exp(-nr * nr) * Ramp(-0.2f, 0.3f, Nz(x / 600f, z / 600f, 71)));
            }
            float cm = Ramp(CreekBFrom, CreekBFrom + 30f, d) * (1f - Ramp(Reach - 80f, Reach - 48f, d));
            if (d < ValleyOut)
            {
                float vl = Mathf.Abs(Nz(x / ValleyScale, z / ValleyScale, 76));                               // the valley's line: a slow noise's zero line (it winds)
                valley = (1f - Ramp(0f, ValleyW, vl)) * Ramp(CreekBFrom, CreekBFrom + 20f, d) * (1f - Ramp(ValleyOut - 110f, ValleyOut, d));
                float vc = Mathf.Abs(Nz(x / ValleyScale + 0.10f * Nz(x / 70f, z / 70f, 77), z / ValleyScale + 0.10f * Nz(x / 70f + 2.7f, z / 70f - 5.3f, 78), 76));   // the creek's line: the valley's, meandering in it
                channel = (1f - Ramp(0f, ChannelW, vc)) * cm;
            }
            if (d < 280f)
            {   // the landscape's own creek regions: its banks stay as gentle as it made them
                float calm = 1f - CreekCalm * Ramp(-0.2f, 0.15f, Nz(x / 520f, z / 520f, 10)) * Ramp(10f, 60f, d) * (1f - Ramp(180f, 280f, d));
                knoll *= calm; hollow *= calm; hills *= calm;
            }
            basin = SwampRegion(x, z, d);
            float o2 = ShoreFarIn + ShoreFarWob * ShoreWobAt(x, z, ShoreFarScale, 93);                     // the far shore wanders too
            swamp = Mathf.Max(basin * (1f - Ramp(Reach - 110f - o2, Reach - 50f - o2, d)), 0.85f * valley * valley * valley * cm);   // the water's share: none near the grid's edge
        }

        /// <summary>The swamps' floor: just round the water's level, its islands and channels.</summary>
        float Isle(float x, float z) => waterY - 0.05f + SwampDepth * (Nz(x / SwampIsle, z / SwampIsle, 66) + 0.6f * Nz(x / (SwampIsle * 0.5f), z / (SwampIsle * 0.5f), 67));

        /// <summary>HeightAt (the land's grid, before the raised feet) and FarY (the far land): the landscape's height h at (x, z), d m beyond
        /// the plateau, made bolder; h itself within KnollFrom m of the plateau's edge, with the switch off or the landscape off.</summary>
        float Bolder(float x, float z, float d, float h)
        {
            if (!LandBold || !LandscapeOn || d <= KnollFrom) return h;
            BoldParts(x, z, d, out float knoll, out float hollow, out float hills, out float basin, out float swamp, out float valley, out float channel);
            BoldCache(x, z, swamp);
            float pad = PadBold(x, z);
            float open = 1f - 0.8f * valley;                                                                 // the creek's valley: the knolls and hills stand back
            float b = h + (knoll * (1f - basin) + hills * (1f - 0.8f * basin)) * open + hollow * (1f - basin);
            b = Mathf.Lerp(b, Mathf.Min(b, Isle(x, z)), swamp);                                                // the swamp: the lowland round the water's level
            b = Mathf.Lerp(b, Mathf.Min(b, waterY + 0.7f), 0.9f * valley * valley * Ramp(CreekBFrom, CreekBFrom + 30f, d));   // the valley's floor
            b = Mathf.Lerp(b, Mathf.Min(b, waterY - 0.55f), channel * (1f - Ramp(waterY + 1f, waterY + 2.5f, b)));  // the creek under the water, where the floor is low
            b = Mathf.Lerp(h, b, pad);
            return Mathf.Lerp(b, Mathf.Max(b, waterY + 0.25f), Ramp(Reach - 75f, Reach - 40f, d));           // no water out to the grid's edge
        }

        /// <summary>Bolder, in BuildSliced's sliced grid loop: a grid point's tint noise and swamp share, kept for its vertex colour in the mesh
        /// frame (the same numbers; the mesh frame is one frame, the grid loop is sliced).</summary>
        void BoldCache(float x, float z, float swamp)
        {
            int k = GridIndex(x, z); if (k < 0) return;
            if (boldN == null || boldN.Length != gh.Length * 3) { boldN = new float[gh.Length * 3]; for (int q = 0; q < boldN.Length; q++) boldN[q] = float.NaN; }
            boldN[3 * k] = Nz(x / 55f, z / 55f, 90) + 0.45f * Nz(x / 22f, z / 22f, 91); boldN[3 * k + 1] = Nz(x / 80f, z / 80f, 92); boldN[3 * k + 2] = swamp;
        }

        // ------------------------------------------------------------------ the ground's colour (the vertex colours' hook)
        /// <summary>The vertex colours: the landscape's colour c of a land point (ny = its normal's up part, d = m beyond the plateau's edge),
        /// with the bolder land's tints: deep moss, bare earth on the slopes, sunny dry tops, swamp olive, the forest floor.</summary>
        Color TintBold(Color c, float x, float y, float z, float ny, float d)
        {
            if (!LandBold || !LandscapeOn || d <= 4f) return c;
            float mn, dn, swamp; int k = boldN != null ? GridIndex(x, z) : -1;
            if (k >= 0 && 3 * k + 2 < boldN.Length && !float.IsNaN(boldN[3 * k])) { mn = boldN[3 * k]; dn = boldN[3 * k + 1]; swamp = boldN[3 * k + 2]; }
            else
            {
                mn = Nz(x / 55f, z / 55f, 90) + 0.45f * Nz(x / 22f, z / 22f, 91); dn = Nz(x / 80f, z / 80f, 92); swamp = 0f;
                if (d < Reach && d > KnollFrom) BoldParts(x, z, d, out _, out _, out _, out _, out swamp, out _, out _);
            }
            float inF = S01((d - 4f) / 30f);
            float slope = Mathf.Sqrt(Mathf.Max(0f, 1f / Mathf.Max(1e-4f, ny * ny) - 1f));
            float earth = inF * Ramp(0.14f, 0.38f, slope);
            float top = inF * Ramp(LandY + 2f, LandY + 8f, y) * Ramp(-0.1f, 0.35f, dn) * (1f - earth);
            float moss = inF * Ramp(0.05f, 0.28f, mn) * (1f - top);
            float sw = inF * swamp * (1f - Ramp(waterY + 1.2f, waterY + 3f, y));
            float forest = inF * ForestAt(x, z);
            c *= Color.Lerp(Color.white, TintDeepMoss, moss); c *= Color.Lerp(Color.white, TintBareEarth, earth); c *= Color.Lerp(Color.white, TintSunTop, top);
            c *= Color.Lerp(Color.white, TintSwampOlive, sw); c *= Color.Lerp(Color.white, TintForestFloor, forest);
            c.a = 1f; return c;
        }

        // ------------------------------------------------------------------ the forest patches
        /// <summary>0 N, 1 E, 2 S, 3 W: the plateau's side a point lies beyond (by the map's middle and the plateau's shape).</summary>
        int SideIndex(float x, float z)
        {
            float dx = (x - map.x * 0.5f) / (px1 - px0), dz = (z + map.y * 0.5f) / (pz1 - pz0);
            return Mathf.Abs(dx) > Mathf.Abs(dz) ? (dx > 0f ? 1 : 3) : (dz > 0f ? 0 : 2);
        }

        /// <summary>The forest patches: candidates on a ForestLattice lattice (ForestFrom - ForestOut m beyond the plateau, clear of the
        /// landmarks' pads and of the swamps' basins), per side the first ForestPerSide (+0 or 1) by their hash, ForestApart m apart.</summary>
        void BuildPatches()
        {
            boldPatchesMade = true; boldPatches.Clear(); BoldPatchCount = 0; System.Array.Clear(BoldPerSide, 0, 4);
            if (gh == null) return;
            float S = ForestLattice, gx1 = gx0 + (gnx - 1) * Cell, gz1 = gz0 + (gnz - 1) * Cell;
            var cand = new List<(int side, int h, int a, int b, Vector3 p)>();
            for (int a = Mathf.FloorToInt(gx0 / S); a <= Mathf.CeilToInt(gx1 / S); a++)
                for (int b = Mathf.FloorToInt(gz0 / S); b <= Mathf.CeilToInt(gz1 / S); b++)
                {
                    float cx = a * S + 10f + H3(a, b, 800) / 99f * (S - 20f), cz = b * S + 10f + H3(a, b, 801) / 99f * (S - 20f);
                    float d = Beyond(cx, cz); if (d < ForestFrom || d > ForestOut) continue;
                    if (PadBold(cx, cz) < 0.999f || SwampRegion(cx, cz, d) > 0.4f) continue;
                    cand.Add((SideIndex(cx, cz), H3(a, b, 802, 1000), a, b, new Vector3(cx, cz, ForestR0 + ForestR * H3(a, b, 803) / 99f)));
                }
            cand.Sort((p, q) => p.side != q.side ? p.side.CompareTo(q.side) : p.h != q.h ? p.h.CompareTo(q.h) : p.a != q.a ? p.a.CompareTo(q.a) : p.b.CompareTo(q.b));
            foreach (var c in cand)
            {
                if (BoldPerSide[c.side] >= ForestPerSide + H3(c.side, 0, 804) % 2) continue;
                bool near = false; foreach (var q in boldPatches) if ((c.p.x - q.x) * (c.p.x - q.x) + (c.p.y - q.y) * (c.p.y - q.y) < ForestApart * ForestApart) { near = true; break; }
                if (near) continue;
                boldPatches.Add(new Vector4(c.p.x, c.p.y, c.p.z, c.side)); BoldPerSide[c.side]++; BoldPatchCount++;
            }
        }

        /// <summary>A patch's share at (x, z): 1 inside, easing out over its last 18 % (a wobbly outline).</summary>
        static float ForestOne(float x, float z, Vector4 p)
        {
            float dx = x - p.x, dz = z - p.y, dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist > p.z * 1.45f) return 0f;
            return 1f - Ramp(0.82f, 1f, dist / (p.z * (1f + 0.28f * Nz(x / 13f, z / 13f, 81))));
        }

        /// <summary>The forest patches' share at (x, z) (the most of any patch).</summary>
        float ForestAt(float x, float z)
        {
            if (!boldPatchesMade) BuildPatches();
            float f = 0f; foreach (var p in boldPatches) f = Mathf.Max(f, ForestOne(x, z, p));
            return f;
        }

        // ------------------------------------------------------------------ the plants (LandscapeSteps' hook: after its lone trees, before its ground cover)
        /// <summary>LandscapeSteps, before its ground cover (which keeps off these trunks as off its own trees): the forest patches' trees and
        /// palms, the reeds by the water, the thickets near the rim, a step at a time; every one the map's own full model in the
        /// landscape's instanced models.</summary>
        IEnumerable<bool> BolderSteps()
        {
            BoldTrees = BoldTreesWet = BoldReeds = BoldThickets = 0;
            if (!LandBold || !LandscapeOn || gh == null || deck55 == null) yield break;
            var flora = GameObject.Find(FloraRoot); int layer = flora ? flora.layer : 0;
            if (!boldPatchesMade) BuildPatches();
            float gx1 = gx0 + (gnx - 1) * Cell, gz1 = gz0 + (gnz - 1) * Cell;
            // ---- the forest patches
            float St = ForestStep;
            foreach (var pt in boldPatches)
            {
                float cx = pt.x, cz = pt.y, R = pt.z;
                for (int i = Mathf.FloorToInt((cx - 1.45f * R) / St); i <= Mathf.CeilToInt((cx + 1.45f * R) / St); i++)
                    for (int j = Mathf.FloorToInt((cz - 1.45f * R) / St); j <= Mathf.CeilToInt((cz + 1.45f * R) / St); j++)
                    {
                        float x = i * St + St * 0.5f + (H3(i, j, 820) / 99f - 0.5f) * ForestJitter, z = j * St + St * 0.5f + (H3(i, j, 821) / 99f - 0.5f) * ForestJitter;
                        float f = ForestOne(x, z, pt); if (f < 0.5f) continue;
                        float d = Beyond(x, z); if (d < ForestFrom - 30f || d > Reach - 20f) continue;
                        if (!ClearMarks(x, z, 0f)) continue;
                        var p = new Vector2(x, z);
                        if (NearRock(x, z, 1f) || Near5d(trees55, p, ForestGap) || SlopeAt(x, z) > ForestSteep) continue;
                        int kind; bool wet = WetFoot(p, out float y);
                        if (wet) kind = H3(i, j, 822) < 65 ? RimMangrove : 5;
                        else
                        {
                            if (!Foot(p, out y, out _, out _)) continue;
                            kind = Pick55(ForestMix, ForestMixW, H3(i, j, 823));
                            if (f < 0.8f && H3(i, j, 824) < 35) kind = 2;                                          // bananas round the patch's edge
                        }
                        var dk = deck55[kind]; if (dk.Count == 0) continue;
                        var src = dk[H3(i, j, 825, 1000) % dk.Count];
                        AddPlant(src, new Vector3(x, y, z), H3(i, j, 826) * 3.6f, wet, layer, kind >= 6 ? src.cast : ShadowCastingMode.Off);   // the trees as the map's own, the bananas and young palms cast none
                        Mark5d(trees55, p); BoldTrees++; if (wet) BoldTreesWet++;
                    }
                yield return true;
            }
            // ---- the reeds by the water (a few mangroves and young palms)
            St = ReedStep;
            int ri0 = Mathf.FloorToInt(gx0 / St), ri1 = Mathf.CeilToInt(gx1 / St), rj0 = Mathf.FloorToInt(gz0 / St), rj1 = Mathf.CeilToInt(gz1 / St);
            for (int j = rj0; j <= rj1; j++)
            {
                for (int i = ri0; i <= ri1; i++)
                {
                    float x = i * St + St * 0.5f + (H3(i, j, 830) / 99f - 0.5f) * 1.8f, z = j * St + St * 0.5f + (H3(i, j, 831) / 99f - 0.5f) * 1.8f;
                    float d = Beyond(x, z); if (d < ReedFrom || d > ReedOut || x <= gx0 || x >= gx1 || z <= gz0 || z >= gz1) continue;
                    var p = new Vector2(x, z);
                    if (!WetFoot(p, out float y)) continue;
                    if (H3(i, j, 832, 1000) >= 1000f * ReedDensity * Ramp(-0.1f, 0.4f, Nz(x / 16f, z / 16f, 83))) continue;
                    if (!ClearMarks(x, z, 0f) || NearRock(x, z, 0.3f) || Near5d(trees55, p, UnderFromTree)) continue;
                    int kind = Pick55(ReedMix, ReedMixW, H3(i, j, 833));
                    if (kind == RimMangrove && (d < 30f || Near5d(trees55, p, ForestGap))) kind = 4;          // a mangrove: a tree, apart from the others
                    var dk = deck55[kind]; if (dk.Count == 0) continue;
                    var src = dk[H3(i, j, 834, 1000) % dk.Count];
                    AddPlant(src, new Vector3(x, y, z), H3(i, j, 835) * 3.6f, true, layer, kind == RimMangrove ? src.cast : ShadowCastingMode.Off);
                    if (kind == RimMangrove) Mark5d(trees55, p);
                    BoldReeds++;
                }
                if ((j & 3) == 0) yield return true;
            }
            // ---- the thickets near the rim (bushes, a few ferns and bananas, in clumps)
            St = ThicketStep;
            int ti0 = Mathf.FloorToInt((px0 - ThicketOut - 4f) / St), ti1 = Mathf.CeilToInt((px1 + ThicketOut + 4f) / St), tj0 = Mathf.FloorToInt((pz0 - ThicketOut - 4f) / St), tj1 = Mathf.CeilToInt((pz1 + ThicketOut + 4f) / St);
            for (int j = tj0; j <= tj1; j++)
            {
                for (int i = ti0; i <= ti1; i++)
                {
                    float x = i * St + St * 0.5f + (H3(i, j, 840) / 99f - 0.5f) * 1.8f, z = j * St + St * 0.5f + (H3(i, j, 841) / 99f - 0.5f) * 1.8f;
                    float d = Beyond(x, z); if (d < ThicketFrom || d > ThicketOut) continue;
                    if (H3(i, j, 842, 1000) >= 1000f * ThicketDensity * Ramp(0.05f, 0.40f, Nz(x / ThicketScale, z / ThicketScale, 85) + 0.3f * Nz(x / 11f, z / 11f, 86))) continue;
                    var p = new Vector2(x, z);
                    if (!Foot(p, out float y, out _, out _)) continue;
                    TintParts(x, Gen(x, z), z, 1f, d, out _, out _, out _, out float path, out _, out _);
                    if (path > 0.35f || !ClearMarks(x, z, 0f) || NearRock(x, z, 0.3f) || Near5d(trees55, p, UnderFromTree)) continue;
                    int kind = Pick55(ThicketMix, ThicketMixW, H3(i, j, 843));
                    var dk = deck55[kind]; if (dk.Count == 0) continue;
                    AddPlant(dk[H3(i, j, 844, 1000) % dk.Count], new Vector3(x, y, z), H3(i, j, 845) * 3.6f, false, layer, ShadowCastingMode.Off);
                    BoldThickets++;
                }
                if ((j & 3) == 0) yield return true;
            }
        }

        // ------------------------------------------------------------------ Play checks
        static string BolderStatus() => !LandBold ? ", bolder land off" + (BoldFrom.Length > 0 ? " (" + BoldFrom + ")" : "") :
            $", bolder land: forest patches {BoldPatchCount} (N {BoldPerSide[0]}, E {BoldPerSide[1]}, S {BoldPerSide[2]}, W {BoldPerSide[3]}) with {BoldTrees} trees and palms (wet {BoldTreesWet}), reeds {BoldReeds}, thickets {BoldThickets}, mist {BoldMist:0.00}";

        /// <summary>Play check of the bolder land (the land shown, the low look off): the relief by band (heights, the steepest step), the
        /// water's share, the edge unchanged (the bolder land on against off), the forest patches by side and their trees, the reeds and
        /// thickets. The crowns, feet, '_lite' shapes and the plateau of every plant are judged by OutskirtsLandscapeCheck (they are its models).</summary>
        public static string OutskirtsBolderCheck()
        {
            var o = I; if (!o) return "OzL16Outskirts bolder land: not on this level";
            if (!o.built) return "OzL16Outskirts bolder land: the land is not built yet";
            if (!LandscapeOn || !LandBold) return "OzL16Outskirts bolder land: OFF" + (BoldFrom.Length > 0 ? " (" + BoldFrom + ")" : "") + (LandscapeOn ? " (the landscape as before)" : " (the land as before)");
            var sb = new StringBuilder(); int fail = 0;
            var bands = new[] { 30f, 120f, 250f }; var vals = new[] { new List<float>(), new List<float>() }; float steep = 0f; int water = 0, cells = 0;
            for (int j = 0; j + 1 < o.gnz; j++)
                for (int i = 0; i + 1 < o.gnx; i++)
                {
                    float x = o.gx0 + i * Cell, z = o.gz0 + j * Cell, d = o.Beyond(x, z); if (d <= 0f) continue;
                    float h = o.gh[j * o.gnx + i];
                    for (int b = 0; b < 2; b++) if (d > bands[b] && d <= bands[b + 1]) vals[b].Add(h);
                    if (d > 40f) { float sx = (o.gh[j * o.gnx + i + 1] - h) / Cell, sz = (o.gh[(j + 1) * o.gnx + i] - h) / Cell; steep = Mathf.Max(steep, Mathf.Sqrt(sx * sx + sz * sz)); }
                    if (d > 30f && d <= 210f)
                    {
                        cells++; float l4 = Mathf.Min(Mathf.Min(h, o.gh[j * o.gnx + i + 1]), Mathf.Min(o.gh[(j + 1) * o.gnx + i], o.gh[(j + 1) * o.gnx + i + 1]));
                        if (l4 < o.waterY + 0.02f) water++;
                    }
                }
            sb.Append(" | RELIEF (m):");
            for (int b = 0; b < 2; b++)
            {
                var v = vals[b]; v.Sort(); if (v.Count == 0) continue;
                sb.Append($" {bands[b]:0}-{bands[b + 1]:0} m beyond the plateau p5 {v[v.Count / 20]:0.0} / p50 {v[v.Count / 2]:0.0} / p95 {v[v.Count * 19 / 20]:0.0} / max {v[v.Count - 1]:0.0};");
            }
            sb.Append($" far land {FarLandMin:0.0} to {FarLandMax:0.0}; steepest grid step beyond 40 m {steep:0.00} (at most 0.8)");
            if (steep > 0.8f) fail++;
            float wshare = 100f * water / Mathf.Max(1, cells);
            sb.Append($" | WATER: {wshare:0.0} % of the land 30-210 m beyond the plateau (at least 10)"); if (wshare < 10f) fail++;
            float worst = 0f; int pts = 0;
            for (float s = 0f; s < o.perim; s += 4f)
            {
                o.EdgePoint(s, out float ex, out float ez, out float ox, out float oz);
                foreach (float dd in new[] { 1f, 3f, 6f })
                {
                    float x = ex + ox * dd, z = ez + oz * dd, h0; LandBold = false; try { h0 = o.HeightAt(x, z); } finally { LandBold = true; }
                    worst = Mathf.Max(worst, Mathf.Abs(o.HeightAt(x, z) - h0)); pts++;
                }
            }
            bool edgeOk = worst <= 0.001f; if (!edgeOk) fail++;
            sb.Append($" | EDGE: the land within 6 m of the plateau's edge moved at most {worst:0.0000} m from the landscape's ({pts} points; at most 0.001) {(edgeOk ? "ok" : "FAIL")}");
            bool sides = true; for (int q = 0; q < 4; q++) if (BoldPerSide[q] < 3 || BoldPerSide[q] > 6) sides = false;
            if (!sides || BoldTrees == 0) fail++;
            sb.Append($" | FOREST: {BoldPatchCount} patches (N {BoldPerSide[0]}, E {BoldPerSide[1]}, S {BoldPerSide[2]}, W {BoldPerSide[3]}: 3 to 6 a side {(sides ? "ok" : "FAIL")}), {BoldTrees} trees and palms (wet {BoldTreesWet}); reeds {BoldReeds}, thickets {BoldThickets}; mist {BoldMist:0.00}");
            sb.Append(" | run OutskirtsLandscapeCheck() too: it judges every plant's model, crown, foot and place (these are its models)");
            return $"OzL16Outskirts bolder land: {(fail == 0 ? "PASS" : "FAIL")}" + sb;
        }
    }
}
