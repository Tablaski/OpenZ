// OpenZ (GPL-3.0)
// The rim ring (the first GroundMargin m outside the map, on the rim's own floor) kept the level's own ring jungle of simplified
// ('_lite') trees, the biggest of them as wide as a factory, and round 5's groves beyond it the same simplified trees; at the widest
// zoom all of them were drawn as round 5b's crown blobs. Now:
//  - none of those trees is drawn any more, near or far (their crowns leave the cells' far meshes);
//  - outside the map stand only copies of the map's own plants in their full model (the ones the player sees on the map near its
//    edge: small canopy trees, palms, ferns, bushes, bananas; by the pools reeds and young palms), each with its own mesh, its own
//    materials and its own size (only turned about the upright), every crown at most RimCap (0.35 of a factory's width) measured on
//    its own points (the widest horizontal extent of the model as it stands: TrueWidth); never a '_lite' or far shape, never one of
//    the big kinds (EmergentA, KapokA / B, ButtressA);
//  - they are drawn as they are at every zoom: they stand outside their cell's levels (no far shape replaces them in the wide view);
//  - in the ring as the map's own jungle at the same distance inside its edge (RimNew, placed off the engine on the rim floor; per cell
//    the small plants first, then the trees and palms), in the groves one on each of round 5's own tree places with the map's small
//    plants round them;
//  - cost: no view the camera can reach at the wide zooms draws more plant triangles for the land outside the map than round 4 (each
//    cell's limit, RimBudget, is what its plants outside the map cost round 4 at the widest zoom times the largest scale that keeps
//    every such view so, measured off the engine), no cell more near triangles or objects than round 4's near; the first plant that
//    does not fit ends the cell; in the wide overview each new plant is culled by its own mesh's box (the wind's widened one lower
//    down: RimFixBoxes);
//  - round 5e: the map's own edge plants on its cliff tops and rims (OzL16Outskirts.RimEdge.cs) are drawn as under-cap full models too;
//  - round 5e: the start flyover's low look and the endings show the same: the ring's new plants (there every row of the map edge's own
//    density: no cost rule holds a low shot), the edge plants' new look, none of the old ring trees; the groves (beyond the rim's floor:
//    the low look has its own far land there) stand back as the land does. Everything a low shot shows is made in the level's first
//    frame (RimEarly), so no plant changes in a shot;
//  - round 5e's fixes (OzL16Outskirts.RimSeat.cs): the map's own small plants that stood in the air on its cliff tops are seated on the
//    ground under them, the old rim trees of the cells round 4 switches off with the land are held off from the first frame, the
//    groves' new plants stand a little into the land round the stem.
// Switches (read when the level starts): RimFixOn (default true; false = round 5b as it was), RimThinOn, RimNewOn, RimBudgetOn (false:
// every row of the full map-edge density, for the bench), RimLowFull (the low look's every ring row), RimEdgeOn (RimEdge.cs). Command
// line (the exe bench): -oz-l16-trees r5e | r5efull | r5d (round 5e without the edge plants' new look) | r5dfull. Drawn only: no rule
// reads any of it. Play check: return OpenZ.OzL16Outskirts.RimFixCheck();
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public sealed partial class OzL16Outskirts
    {
        /// <summary>Rounds 5d / 5e (default): outside the map only the map's own full-model plants under the crown cap, at every zoom. Off =
        /// round 5b as it was. RimThinOn: the old trees outside the map are not drawn; RimNewOn: the new plants; RimBudgetOn: they stay
        /// inside each cell's round-4 cost (off = the full map-edge density); RimLowFull: the start flyover's low look and the endings show
        /// every ring row. Set before the level starts.</summary>
        public static bool RimFixOn = true, RimThinOn = true, RimNewOn = true, RimBudgetOn = true, RimLowFull = true;
        /// <summary>The groves' small plants per tree (the map's land small plants per dry-land tree), where they stand round a tree (m,
        /// from - to), apart (m), off a trunk (m); a wet place's land within these heights of the water (m, under - over).</summary>
        public static float GroveSmall = 2.28f, UnderFrom = 1.2f, UnderReach = 7f, UnderSpacing = 1.4f, UnderFromTree = 1.0f, WetUnder = 0.45f, WetOver = 0.3f;
        /// <summary>A factory's footprint width (m), for the Play check's comparison.</summary>
        public const float FactoryWidth = 8f;
        /// <summary>Counters: old trees outside the map not drawn, new plants (ring, ring rows only the low look shows, groves), rows the
        /// cell's cost left out, cells full, cells changed, cells left as round 5b had them (their far mesh did not come), data rows
        /// refused, sources measured on their own points (and the widest of them, m).</summary>
        public static int RimNotDrawn, RimNewRing, RimNewLow, RimNewGroves, RimOverBudget, RimCellsFull, RimCellsApplied, RimCellsLeft, RimRefused, RimSourcesMeasured;
        /// <summary>The new plants outside the map tall enough for the fort's blast to throw (OzTreeFall.AddBlastFlora), this level.</summary>
        public static int RimBlastGiven;
        public static float RimSourceWidest;
        public static string RimWhy = "", RimFrom = "";

        sealed class RimSource { public string name; public int kind; public Quaternion rot; public Vector3 scale; public Mesh mesh; public Material[] mats; public ShadowCastingMode cast; public bool receive; public float h, w, tw = -1f; public long tris; }
        sealed class RimCell
        {
            public Transform cell; public Grove slot; public bool applied, budget, full;
            public long far, near, farUsed, nearUsed; public int objs, objsUsed;
            public readonly List<Renderer> thin = new List<Renderer>();
            public readonly List<Renderer> adds = new List<Renderer>();                                  // the game's: the ring's rows inside the cost, the groves'
            public readonly List<Renderer> addsLow = new List<Renderer>();                               // the ring's rows past the cost: the low look only
            public readonly List<(Renderer r, RimSource s)> srcOf = new List<(Renderer, RimSource)>();
            public GameObject holder, holderLow, holderGrove;
        }

        readonly Dictionary<Transform, RimCell> rimCells = new Dictionary<Transform, RimCell>();
        readonly HashSet<Renderer> rimThin = new HashSet<Renderer>();
        readonly Dictionary<string, RimSource> rimSrc = new Dictionary<string, RimSource>();
        List<RimSource>[] rimDeck; int[] rimDeckAt;
        readonly Dictionary<long, List<Vector2>> rimPlaced = new Dictionary<long, List<Vector2>>();     // the groves' new mangroves (8 m buckets: they keep apart)
        readonly Dictionary<Renderer, Bounds> rimWindBox = new Dictionary<Renderer, Bounds>();           // a new plant's drawing box as the wind widened it
        bool rimOwnBox, rimEarly;                                                                        // the new plants on their own drawing boxes now (the wide overview); RimEarly ran
        int rimShowLow = -1;                                                                             // the low look's plants shown (1) / not (0) / not set (-1), before the land is built

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRimStatics()
        {   // every Play session starts with round 5e on (the switches are read when the level starts)
            RimFixOn = RimThinOn = RimNewOn = RimBudgetOn = RimLowFull = true;
            RimNotDrawn = RimNewRing = RimNewLow = RimNewGroves = RimOverBudget = RimCellsFull = RimCellsApplied = RimCellsLeft = RimRefused = RimSourcesMeasured = RimBlastGiven = 0;
            RimSourceWidest = 0f; RimWhy = ""; RimFrom = "";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RimArgs()
        {   // -oz-l16-trees r5e: round 5e for a whole run of the game (the exe bench); r5efull: without the cells' cost limit; r5d / r5dfull: the
            // same without the edge plants' new look; r4 / r5 / r5b / r5bmap: off
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < a.Length; i++)
            {
                if (a[i] != "-oz-l16-trees") continue;
                string w = a[i + 1].ToLowerInvariant();
                if (w == "r5e" || w == "r5efull" || w == "r5d" || w == "r5dfull")
                {
                    TreesOn = true; FarLookOn = true; FarLookMapTrees = false; RimFixOn = true; RimBudgetOn = !w.EndsWith("full"); RimEdgeOn = w.StartsWith("r5e"); RimFrom = "command line " + w;
                    Debug.Log("[OzL16Outskirts] level 16 plants beyond the map: round " + (RimEdgeOn ? "5e" : "5e without the edge plants' new look") + (RimBudgetOn ? "" : ", without the cells' cost limit") + " (" + RimFrom + ")");
                }
                else if (w == "r4" || w == "r5" || w == "r5b" || w == "r5bmap") { RimFixOn = false; RimFrom = "command line " + w; }
            }
        }

        /// <summary>A plant shape's kind: its mesh name without a copy's number ("_001"); a '_lite' shape is not one of RimKinds.</summary>
        static string RimBaseName(Mesh m)
        {
            if (!m) return ""; string n = m.name; int u = n.LastIndexOf('_');
            if (u > 0 && n.Length - u == 4 && char.IsDigit(n[u + 1]) && char.IsDigit(n[u + 2]) && char.IsDigit(n[u + 3])) n = n.Substring(0, u);
            return n;
        }

        static int RimKindOf(Mesh m) => m ? System.Array.IndexOf(RimKinds, RimBaseName(m)) : -1;

        /// <summary>A plant's height and crown width (m): its mesh's own box through its transform (the kit's shapes stand with mesh -z
        /// up: upright plants without the turn, else the world box; Renderer.bounds is widened by the wind and is no measure).</summary>
        static void PlantSize(Transform t, Mesh m, out float h, out float w)
        {
            var b = m.bounds; var up = t.rotation * Vector3.back;
            if (Vector3.Dot(up.normalized, Vector3.up) > 0.98f) { var s = t.lossyScale; h = b.size.z * Mathf.Abs(s.z); w = Mathf.Max(b.size.x * Mathf.Abs(s.x), b.size.y * Mathf.Abs(s.y)); }
            else { var wb = WorldBox(b, t.localToWorldMatrix); h = wb.size.y; w = Mathf.Max(wb.size.x, wb.size.z); }
        }

        // ------------------------------------------------------------------ the true crown width (round 5e)
        static readonly Dictionary<Mesh, Vector3[]> rimPoints = new Dictionary<Mesh, Vector3[]>();
        static readonly List<Vector2> rimFlat = new List<Vector2>(), rimHull = new List<Vector2>();

        /// <summary>The TRUE crown width of a plant (m): the widest horizontal extent of its own mesh's points through its matrix (its turn,
        /// tilt and scale), i.e. the largest distance between two points of its outline seen from above (convex hull, every pair). A turn
        /// about the upright does not change it. -1 = the mesh's points cannot be read.</summary>
        static float TrueWidth(Mesh m, Matrix4x4 w)
        {
            if (!m) return -1f;
            if (!rimPoints.TryGetValue(m, out var v)) { v = m.isReadable ? m.vertices : null; rimPoints[m] = v; }
            if (v == null || v.Length == 0) return -1f;
            rimFlat.Clear();
            for (int i = 0; i < v.Length; i++) { var p = w.MultiplyVector(v[i]); rimFlat.Add(new Vector2(p.x, p.z)); }
            rimFlat.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            rimHull.Clear();
            for (int pass = 0; pass < 2; pass++)
            {   // Andrew's monotone chain: the lower outline, then the upper one
                int start = rimHull.Count;
                for (int k = 0; k < rimFlat.Count; k++)
                {
                    var p = rimFlat[pass == 0 ? k : rimFlat.Count - 1 - k];
                    while (rimHull.Count >= start + 2)
                    {
                        Vector2 o = rimHull[rimHull.Count - 2], a = rimHull[rimHull.Count - 1];
                        if ((a.x - o.x) * (p.y - o.y) - (a.y - o.y) * (p.x - o.x) > 0f) break;
                        rimHull.RemoveAt(rimHull.Count - 1);
                    }
                    rimHull.Add(p);
                }
                rimHull.RemoveAt(rimHull.Count - 1);
            }
            float best = 0f;
            for (int i = 0; i < rimHull.Count; i++) for (int j = i + 1; j < rimHull.Count; j++) best = Mathf.Max(best, (rimHull[i] - rimHull[j]).sqrMagnitude);
            return Mathf.Sqrt(best);
        }

        static float TrueWidth(Transform t, Mesh m) => t ? TrueWidth(m, Matrix4x4.TRS(Vector3.zero, t.rotation, t.lossyScale)) : -1f;

        // ------------------------------------------------------------------ Plan's hooks (OzL16Outskirts.FarLook.cs)
        /// <summary>Plan: a tree outside the map that is not drawn loses its far crown with its run.</summary>
        bool RimThinned(Renderer r) => r && rimThin.Contains(r);

        /// <summary>Plan: a cell round 5d took keeps its far mesh even when nothing is left in it (an empty one: no outside tree's crown).</summary>
        bool RimTook(Transform cell) => RimFixOn && cell && rimCells.ContainsKey(cell);

        /// <summary>FarLookCheck: with round 5d on no tree outside the map has a far crown (round 5b's own count is 0 then).</summary>
        static bool RimFarNone() => RimFixOn && RimThinOn && TreesOn && FarLookOn && RimCellsApplied > 0;

        // ------------------------------------------------------------------ build, first part: the level's first frame (BuildSliced, right after ChooseCells)
        /// <summary>What a low shot can show is made at once, before the first frame is drawn: the sources, the old ring trees not drawn,
        /// the ring's new plants (the game's inside the cost, the rest for the low look), the edge plants' new look. Any failure undoes
        /// all of it (round 5b as it is) and says why.</summary>
        void RimEarly()
        {
            RimNotDrawn = RimNewRing = RimNewLow = RimNewGroves = RimOverBudget = RimCellsFull = RimCellsApplied = RimCellsLeft = RimRefused = RimSourcesMeasured = RimBlastGiven = 0; RimSourceWidest = 0f; RimWhy = "";
            rimCells.Clear(); rimThin.Clear(); rimSrc.Clear(); rimPlaced.Clear(); rimWindBox.Clear(); rimOwnBox = false; rimEarly = false; rimShowLow = -1;
            RimEdgeReset();
            RimSeatReset();                                                                            // round 5e's fixes (OzL16Outskirts.RimSeat.cs)
            if (!RimFixOn || !TreesOn || !FarLookOn) return;
            var root = GameObject.Find(FloraRoot); if (!root) return;
            try
            {
                var byName = new Dictionary<string, Transform>();
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    if (mf && mf.sharedMesh && mf.name != "FAR" && !byName.ContainsKey(mf.name)) byName[mf.name] = mf.transform;
                var gone = new HashSet<Transform>(); foreach (var go in toHide) if (go) gone.Add(go.transform);
                var slotOf = new Dictionary<Transform, Grove>(); foreach (var s in slots) slotOf[s.cell] = s;
                // the decks: the map's own full-model plants of each kind under the cap (RimSources, each checked against the level; the
                // true width of each on its own points follows in RimFixSteps, a few a frame)
                rimDeck = new List<RimSource>[RimKinds.Length]; rimDeckAt = new int[RimKinds.Length];
                for (int k = 0; k < RimKinds.Length; k++)
                {
                    rimDeck[k] = new List<RimSource>();
                    foreach (var n in RimSources[k])
                    {
                        var src = RimSourceOf(byName, n, RimKinds[k]); if (src == null) continue;
                        src.kind = k; rimDeck[k].Add(src); rimSrc[n] = src;
                    }
                }
                // not drawn: every tree of the rim ring (its foot 0 - GroundMargin m outside the map; each is listed in its cell's far
                // runs), at once (not drawn, its object kept as it is until the land is shown: RimFixApply switches it off)
                if (RimThinOn)
                {
                    float m = OzL16FlyoverFlora.GroundMargin;
                    foreach (var row in EdgeRuns)
                    {
                        var cell = root.transform.Find(row.cell); if (!cell || gone.Contains(cell)) continue;
                        foreach (var n in row.names)
                        {
                            if (!byName.TryGetValue(n, out var pt) || !pt.IsChildOf(cell)) pt = FindDeep(cell, n);
                            if (!pt) continue;                                                             // Plan names it (refuses the cell)
                            float o = Outside(pt.position); if (o <= 0f || o > m + 0.01f) continue;         // a map plant, or beyond the ring (round 5 hides it)
                            var r = pt.GetComponent<Renderer>(); if (!r) continue;
                            CellOf(cell, slotOf).thin.Add(r); rimThin.Add(r); r.forceRenderingOff = true;
                        }
                    }
                }
                RimGoneHold(gone);                                                                         // round 5e's fixes: the gone cells' rim trees held off,
                RimSeatMake(root);                                                                         // the map plants that stood in the air seated (OzL16Outskirts.RimSeat.cs)
                if (RimNewOn) RingData(root, gone, slotOf);
                if (RimEdgeOn) RimEdgeSwap(root, byName);
                rimEarly = true;
                var wind = OzFloraWind.I;
                if (wind) wind.Scan();                                                                     // the wind hooks the new plants now (its own scan, else within 3 s)
                RimEarlyShow();
            }
            catch (System.Exception e)
            {   // never half of it: everything of rounds 5d / 5e goes, round 5b stays as it is
                Debug.LogError("[OzL16Outskirts] round 5e could not be made, the land outside the map stays as round 5b has it: " + e);
                RimUndoAll(); Refuse5d("not made: " + e.GetType().Name);
            }
        }

        /// <summary>A source of a kind: the map's own upright plant of that kind's full model, inside the map, crown under the cap (the box
        /// measure; the true width follows).</summary>
        RimSource RimSourceOf(Dictionary<string, Transform> byName, string n, string kind)
        {
            if (!byName.TryGetValue(n, out var t)) { Refuse5d(n + " is not in the level"); return null; }
            var smf = t.GetComponent<MeshFilter>(); var sr = t.GetComponent<MeshRenderer>();
            if (!smf || !sr || RimBaseName(smf.sharedMesh) != kind || Outside(t.position) > 0f || Vector3.Angle(t.rotation * Vector3.back, Vector3.up) > 10f) { Refuse5d(n + ": not an upright map plant of its kind"); return null; }
            PlantSize(t, smf.sharedMesh, out float h, out float w);
            if (w > RimCap + 0.005f) { Refuse5d(n + $": its crown {w:0.00} m is over the cap"); return null; }
            return new RimSource { name = n, rot = t.rotation, scale = t.lossyScale, mesh = smf.sharedMesh, mats = RimMats(sr), cast = sr.shadowCastingMode, receive = sr.receiveShadows, h = h, w = w, tris = Tris(smf.sharedMesh) };
        }

        /// <summary>Everything of rounds 5d / 5e undone (a failure while it was made): the old trees drawn again, the new plants gone.</summary>
        void RimUndoAll()
        {
            foreach (var rc in rimCells.Values)
            {
                foreach (var r in rc.thin) if (r) { r.forceRenderingOff = false; r.gameObject.SetActive(true); }
                if (rc.holder) Destroy(rc.holder); if (rc.holderLow) Destroy(rc.holderLow); if (rc.holderGrove) Destroy(rc.holderGrove);
                rc.thin.Clear(); rc.adds.Clear(); rc.addsLow.Clear(); rc.srcOf.Clear();
            }
            rimCells.Clear(); rimThin.Clear(); rimEarly = false;
            RimEdgeUndoAll();
            RimSeatUndo();                                                                             // round 5e's fixes (OzL16Outskirts.RimSeat.cs)
        }

        // ------------------------------------------------------------------ build, second part (BuildSliced: after GrowTrees, before FarLookSteps)
        IEnumerable<bool> RimFixSteps()
        {
            if (!RimFixOn || !TreesOn || !FarLookOn || !rimEarly) yield break;
            var root = GameObject.Find(FloraRoot); if (!root) yield break;
            var slotOf = new Dictionary<Transform, Grove>(); foreach (var s in slots) slotOf[s.cell] = s;
            // a cell round 5 left as round 4 had it, or does not draw at all: nothing of round 5d in it
            foreach (var s in slots)
            {
                if (!(s.refused || s.empty) || !rimCells.TryGetValue(s.cell, out var rc)) continue;
                foreach (var r in rc.thin) { if (r) r.forceRenderingOff = false; rimThin.Remove(r); }
                if (rc.holder) Destroy(rc.holder); if (rc.holderLow) Destroy(rc.holderLow);
                rimCells.Remove(s.cell);
            }
            // round 5's grove trees of every drawn grove cell: not drawn (they are shown with the land: RimFixApply switches them off)
            if (RimThinOn)
                foreach (var s in slots)
                {
                    if (s.refused || s.empty) continue;
                    foreach (var tr in s.trees) if (tr) { CellOf(s.cell, slotOf).thin.Add(tr); rimThin.Add(tr); }
                }
            yield return true;
            if (RimNewOn)
                foreach (var s in slots)
                {
                    if (s.refused || s.empty || s.trees.Count == 0) continue;
                    GroveUnder(s, slotOf); yield return true;
                }
            // the sources on their own points: the true width of each (a few a frame); one over the cap takes its plants away
            int done = 0; var bad = new HashSet<RimSource>();
            foreach (var src in rimSrc.Values)
            {
                src.tw = TrueWidth(src.mesh, Matrix4x4.TRS(Vector3.zero, src.rot, src.scale)); RimSourcesMeasured++;
                RimSourceWidest = Mathf.Max(RimSourceWidest, src.tw);
                if (src.tw > RimCap + 0.005f) { bad.Add(src); Refuse5d(src.name + $": its true crown {src.tw:0.00} m is over the cap"); }
                if (++done % 24 == 0) yield return true;
            }
            foreach (var src in RimEdgeSources())
            {
                src.tw = TrueWidth(src.mesh, Matrix4x4.TRS(Vector3.zero, src.rot, src.scale));
                if (src.tw > RimCap + 0.005f) { bad.Add(src); Refuse5d(src.name + $": its true crown {src.tw:0.00} m is over the cap"); }
            }
            if (bad.Count > 0)
            {
                foreach (var rc in rimCells.Values)
                    foreach (var (r, s) in rc.srcOf) if (r && bad.Contains(s)) { rc.adds.Remove(r); rc.addsLow.Remove(r); Destroy(r.gameObject); }
                RimEdgeDrop(bad);
            }
            yield return true;
        }

        Material[] RimMats(MeshRenderer r)
        {
            var wind = OzFloraWind.I;
            return wind && wind.TryGet(r, out var p) && p.Original != null ? (Material[])p.Original.Clone() : r.sharedMaterials;   // its own materials (the wind hooks the copy again)
        }

        void Refuse5d(string why) { RimRefused++; if (RimWhy.Length < 600) RimWhy += (RimWhy.Length > 0 ? "; " : "") + why; }

        /// <summary>The cells' far limits (RimBudget) are round 4's x 1.35: the edge plants' far runs leave the far meshes (RimEdgeOn) and
        /// what they free goes to the plants outside the map. Without the edge swap those runs stay, and the limit is round 5d's x 1.25
        /// (off the engine, tools/verify2_cost_exact.py: then every reachable wide view and its shadow casters stay at or under round 4;
        /// at x 1.35 some shadow casters' views go over).</summary>
        const double RimBudgetNoEdge = 1.25 / 1.35;

        RimCell CellOf(Transform cell, Dictionary<Transform, Grove> slotOf)
        {
            if (!rimCells.TryGetValue(cell, out var rc))
            {
                rimCells[cell] = rc = new RimCell { cell = cell };
                slotOf.TryGetValue(cell, out rc.slot);
                for (int i = 0; i < RimBudget.Length; i++)
                    if (RimBudget[i].cell == cell.name) { rc.far = RimEdgeOn ? RimBudget[i].far : (long)(RimBudget[i].far * RimBudgetNoEdge); rc.near = RimBudget[i].near; rc.objs = RimBudget[i].objs; rc.budget = true; break; }
            }
            return rc;
        }

        /// <summary>The next new plant of the cell: inside its round-4 cost (RimBudgetOn), else the cell is full from here on.</summary>
        bool Room(RimCell rc, RimSource src)
        {
            if (!RimBudgetOn) return true;
            if (rc.full) return false;
            if (rc.budget && rc.farUsed + src.tris <= rc.far && rc.nearUsed + src.tris <= rc.near && rc.objsUsed + 1 <= rc.objs) return true;
            rc.full = true; RimCellsFull++;
            return false;
        }

        /// <summary>The ring's data (OzL16Outskirts.RimData.cs), each row checked against the level as it is now: per cell the rows inside
        /// its cost are the game's, the rest and the low look's fill (RimLowFill) stand in the low look only (RimLowFull).</summary>
        void RingData(GameObject root, HashSet<Transform> gone, Dictionary<Transform, Grove> slotOf)
        {
            float m = OzL16FlyoverFlora.GroundMargin;
            foreach (var row in RimNew)
            {
                var cellT = root.transform.Find(row.cell);
                if (!cellT) { Refuse5d(row.cell + " is not in the level"); continue; }
                if (gone.Contains(cellT)) continue;
                if (row.kind < 0 || row.kind >= RimKinds.Length) { Refuse5d(row.cell + ": a new plant of no kind"); continue; }
                if (!rimSrc.TryGetValue(row.src, out var src) || src.kind != row.kind) { Refuse5d(row.src + ": not a source of its kind"); continue; }
                var at = new Vector3(row.x, row.y, row.z); float o = Outside(at);
                int cx, cz; if (!Square(row.cell, out cx, out cz) || at.x < cx * 32f || at.x > cx * 32f + 32f || at.z < cz * 32f || at.z > cz * 32f + 32f || o <= 0f || o > m + 0.01f) { Refuse5d(row.cell + ": a new plant outside its cell or the ring"); continue; }
                var rc = CellOf(cellT, slotOf);
                if (!Room(rc, src)) { RimOverBudget++; if (RimLowFull) { NewPlant(rc, src, at, row.yaw, 1); RimNewLow++; } continue; }
                NewPlant(rc, src, at, row.yaw, 0); RimNewRing++;
            }
            if (!RimLowFull) return;
            foreach (var row in RimLowFill)
            {   // the low look's fill: trees and palms of the same mix on the rim floor (the start flyover and the endings only, no cost rule)
                var cellT = root.transform.Find(row.cell);
                if (!cellT) { Refuse5d(row.cell + " is not in the level"); continue; }
                if (gone.Contains(cellT)) continue;
                if (row.kind < 6 || row.kind >= RimKinds.Length || !rimSrc.TryGetValue(row.src, out var src) || src.kind != row.kind) { Refuse5d(row.src + ": not a tree source of its kind"); continue; }
                var at = new Vector3(row.x, row.y, row.z); float o = Outside(at);
                int cx, cz; if (!Square(row.cell, out cx, out cz) || at.x < cx * 32f || at.x > cx * 32f + 32f || at.z < cz * 32f || at.z > cz * 32f + 32f || o <= 0f || o > m + 0.01f) { Refuse5d(row.cell + ": a fill plant outside its cell or the ring"); continue; }
                NewPlant(CellOf(cellT, slotOf), src, at, row.yaw, 1); RimNewLow++;
            }
        }

        /// <summary>A new plant: a copy of one of the map's plants (its mesh, its own materials, its size), turned about the upright, under
        /// the cell's holder for it (0: OUTER_UNDER, the ring's game plants; 1: OUTER_UNDER_LOW, the ring rows past the cost, the low look
        /// only; 2: OUTER_GROVE, the groves', with the land). It stays outside the cell's levels: every zoom draws its full model.</summary>
        void NewPlant(RimCell rc, RimSource src, Vector3 at, float yaw, int which)
        {
            var h = which == 1 ? rc.holderLow : which == 2 ? rc.holderGrove : rc.holder;
            if (!h)
            {
                h = new GameObject(which == 1 ? "OUTER_UNDER_LOW" : which == 2 ? "OUTER_GROVE" : "OUTER_UNDER"); h.SetActive(false); h.layer = rc.cell.gameObject.layer;
                h.transform.SetParent(rc.cell, false);
                if (which == 1) rc.holderLow = h; else if (which == 2) rc.holderGrove = h; else rc.holder = h;
            }
            var ls = h.transform.lossyScale;
            var go = new GameObject("OUTSIDE_" + RimKinds[src.kind].Substring(3) + "_" + (RimNewRing + RimNewLow + RimNewGroves)); go.layer = h.layer;
            go.transform.SetParent(h.transform, false);
            go.transform.SetPositionAndRotation(at, Quaternion.AngleAxis(yaw, Vector3.up) * src.rot);
            go.transform.localScale = new Vector3(src.scale.x / ls.x, src.scale.y / ls.y, src.scale.z / ls.z);
            go.AddComponent<MeshFilter>().sharedMesh = src.mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = src.mats;
            mr.shadowCastingMode = src.cast; mr.receiveShadows = src.receive;                          // as the map's own plant
            rc.srcOf.Add((mr, src));
            if (which == 1) { rc.addsLow.Add(mr); return; }
            rc.adds.Add(mr); rc.farUsed += src.tris; rc.nearUsed += src.tris; rc.objsUsed++;
        }

        RimSource Deal(int kind)
        {
            var d = rimDeck[kind]; if (d.Count == 0) return null;
            return d[rimDeckAt[kind]++ % d.Count];                                                     // the map's plants of the kind in turn: their own spread of sizes
        }

        int PickKind(int[] kinds, int[] weights, int h100)
        {
            int sum = 0; for (int i = 0; i < weights.Length; i++) if (rimDeck[kinds[i]].Count > 0) sum += weights[i];
            if (sum <= 0) return -1;
            float pick = h100 * sum / 100f, acc = 0f; int last = -1;
            for (int i = 0; i < kinds.Length; i++) { if (rimDeck[kinds[i]].Count == 0) continue; acc += weights[i]; last = kinds[i]; if (pick < acc) return kinds[i]; }
            return last;
        }

        static void Mark5d(Dictionary<long, List<Vector2>> d, Vector2 p)
        {
            long key = Key(Mathf.FloorToInt(p.x / 8f), Mathf.FloorToInt(p.y / 8f));
            if (!d.TryGetValue(key, out var l)) d[key] = l = new List<Vector2>();
            l.Add(p);
        }

        static bool Near5d(Dictionary<long, List<Vector2>> d, Vector2 p, float r)
        {
            int bx = Mathf.FloorToInt(p.x / 8f), bz = Mathf.FloorToInt(p.y / 8f); int n = Mathf.CeilToInt(r / 8f);
            for (int a = bx - n; a <= bx + n; a++)
                for (int b = bz - n; b <= bz + n; b++)
                    if (d.TryGetValue(Key(a, b), out var l)) foreach (var q in l) if ((q - p).sqrMagnitude < r * r) return true;
            return false;
        }

        /// <summary>A wet place for a reed or a young palm: the land at the pool's edge, at most WetOver over the water and WetUnder under
        /// it; the foot just under the land (the water hides it, as by the map's own pools).</summary>
        bool WetFoot(Vector2 p, out float y)
        {
            float h = Gen(p.x, p.y); y = h - 0.05f;
            return h <= waterY + WetOver && h >= waterY - WetUnder;
        }

        bool Clear5d(Vector2 p)
        {
            foreach (var c in marks) if ((p.x - c.x) * (p.x - c.x) + (p.y - c.y) * (p.y - c.y) < c.z * c.z) return false;
            foreach (var f in flumeEnds) { var r = f.at + f.o * 2f; if ((p.x - r.x) * (p.x - r.x) + (p.y - r.z) * (p.y - r.z) < RockClear * RockClear) return false; }
            return true;
        }

        /// <summary>A grove cell (round 5's trees on the land beyond the rim): on each of round 5's own tree places (its foot on the land
        /// by round 5's rule) a full-model tree or palm of the map's mix, and round it its share of the map's small plants (reeds and
        /// young palms where the land is wet), in the place order, until the cell's round-4 cost is used up.</summary>
        void GroveUnder(Grove s, Dictionary<Transform, Grove> slotOf)
        {
            var rc = CellOf(s.cell, slotOf); if (RimBudgetOn && (rc.full || !rc.budget)) return;
            float x0 = s.cx * 32f, z0 = s.cz * 32f; float want = 0f; int small = 0;
            bool InCell(Vector2 p) => p.x >= x0 + 1f && p.x <= x0 + 31f && p.y >= z0 + 1f && p.y <= z0 + 31f;
            var tp = new List<Vector2>(); var smallAt = new Dictionary<long, List<Vector2>>();
            for (int i = 0; i < s.trees.Count; i++)
            {
                var spot = s.trees[i]; if (!spot) continue;
                var at = spot.transform.position; var p = new Vector2(at.x, at.z);
                int kind = PickKind(RimTreeMix, RimTreeMixW, H3(s.cx, s.cz, 900 + i)); if (kind < 0) return;
                var src = Deal(kind); if (src == null) continue;
                if (!Room(rc, src)) return;                                                                // the cell's round-4 cost is used up
                RimGroveFoot(p, ref at.y, true);                                                           // round 5e: its foot a little into the land round its stem (OzL16Outskirts.RimSeat.cs)
                NewPlant(rc, src, at, H3(s.cx, s.cz, 950 + i) * 3.6f, 2); RimNewGroves++;
                tp.Add(p);
                // its share of the small plants, round it (the map's own: most within a few metres of a tree)
                want += GroveSmall;
                for (int k = 0; k < 24 && small < Mathf.FloorToInt(want); k++)
                {
                    float a = H3(s.cx, s.cz, 1000 + 32 * i + k) / 100f * 2f * Mathf.PI, r = UnderFrom + (UnderReach - UnderFrom) * Mathf.Sqrt(H3(s.cx, s.cz, 3000 + 32 * i + k) / 100f);
                    var q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (!InCell(q)) continue;
                    float d = Beyond(q.x, q.y); if (d < TreeFrom || d > Reach - 20f || !Clear5d(q)) continue;
                    bool onTree = false; foreach (var t in tp) if ((t - q).sqrMagnitude < UnderFromTree * UnderFromTree) { onTree = true; break; }
                    if (onTree || Near5d(smallAt, q, UnderSpacing) || Near5d(placed, q, UnderFromTree)) continue;   // nor on a tree of the next cell (round 5's places)
                    int sk; float y;
                    if (WetFoot(q, out y))
                    {   // by a pool: reeds, young palms, and (as far from every tree as the trees stand apart) mangroves
                        sk = PickKind(RimWetMix, RimWetMixW, H3(s.cx, s.cz, 5000 + 32 * i + k));
                        if (sk == RimMangrove)
                        {
                            bool near = Near5d(rimPlaced, q, TreeSpacing) || Near5d(placed, q, TreeSpacing);
                            foreach (var t in tp) if ((t - q).sqrMagnitude < TreeSpacing * TreeSpacing) { near = true; break; }
                            if (near) sk = RimWetMix[0];
                        }
                    }
                    else { if (!Foot(q, out y, out _, out _) || !RimGroveFoot(q, ref y, false)) continue; sk = PickKind(RimSmallMix, RimSmallMixW, H3(s.cx, s.cz, 5000 + 32 * i + k)); }
                    if (sk < 0) continue;
                    var ss = Deal(sk); if (ss == null) continue;
                    if (!Room(rc, ss)) return;
                    NewPlant(rc, ss, new Vector3(q.x, y, q.y), H3(s.cx, s.cz, 7000 + 32 * i + k) * 3.6f, 2); RimNewGroves++;
                    if (sk == RimMangrove) { tp.Add(q); Mark5d(rimPlaced, q); } else { Mark5d(smallAt, q); small++; }
                }
            }
        }

        // ------------------------------------------------------------------ show (with the land), and the low look
        /// <summary>BuildSliced, right after ApplyTrees: a cell whose new far mesh came (round 5b's job, without the crowns of the trees
        /// that are not drawn) is changed (its old trees switched off); any other cell stays exactly as round 5b has it (its trees drawn, no
        /// new plant). The edge plants likewise (RimEdgeApply).</summary>
        void RimFixApply()
        {
            if (!RimFixOn || !rimEarly) return;
            var jobMesh = new Dictionary<Transform, Mesh>();
            foreach (var j in farJobs) if (j.mesh) jobMesh[j.cell] = j.mesh;
            foreach (var rc in rimCells.Values)
            {
                var farT = rc.cell.Find("FAR"); var mf = farT ? farT.GetComponent<MeshFilter>() : null;
                bool planned = mf && jobMesh.TryGetValue(rc.cell, out var jm) && mf.sharedMesh == jm;
                if (!planned)
                {   // its far mesh does not come from this round: nothing of round 5d in it
                    RimCellsLeft++; if (RimWhy.Length < 600) RimWhy += (RimWhy.Length > 0 ? "; " : "") + rc.cell.name + ": left as round 5b has it";
                    foreach (var r in rc.thin) { if (r) r.forceRenderingOff = false; rimThin.Remove(r); }
                    if (rc.holder) Destroy(rc.holder); if (rc.holderLow) Destroy(rc.holderLow); if (rc.holderGrove) Destroy(rc.holderGrove);
                    rc.thin.Clear(); rc.adds.Clear(); rc.addsLow.Clear(); rc.srcOf.Clear(); rc.farUsed = rc.nearUsed = 0; rc.objsUsed = 0;
                    continue;
                }
                rc.applied = true; RimCellsApplied++; RimNotDrawn += rc.thin.Count;
                foreach (var r in rc.thin) if (r) { r.gameObject.SetActive(false); r.forceRenderingOff = false; }   // off, as round 5d had them (the flyover's own rules then pass them by)
            }
            RimEdgeApply(jobMesh);
            foreach (var rc in rimCells.Values)
            {   // the fort's blast in an ending throws the tall ones as it threw the old ring trees (OzTreeFall.RimEdge.cs AddBlastFlora)
                if (!rc.applied) continue;
                foreach (var r in rc.adds) if (r && OzTreeFall.AddBlastFlora(r.transform)) RimBlastGiven++;
                foreach (var r in rc.addsLow) if (r && OzTreeFall.AddBlastFlora(r.transform)) RimBlastGiven++;
            }
            var wind = OzFloraWind.I;
            if ((RimCellsApplied > 0 || RimEdgeShown > 0) && wind) wind.Scan();                           // the wind hooks the groves now (its own scan, else within 3 s): RimFixBoxes finds their boxes
            if (RimCellsApplied > 0 || RimEdgeShown > 0)
                Debug.Log($"[OzL16Outskirts] round 5e: trees outside the map not drawn {RimNotDrawn}, new full-model plants {RimNewRing + RimNewGroves} (ring {RimNewRing}, groves {RimNewGroves}; crowns at most {RimCap:0.00} m, true width; the low look's ring rows {RimNewLow}) in {RimCellsApplied} cells, cost limit {(RimBudgetOn ? "on" : "off")} (rows left out {RimOverBudget}, cells full {RimCellsFull}); cells left as round 5b had them {RimCellsLeft}, data rows refused {RimRefused}{(RimWhy.Length > 0 ? " (" + RimWhy + ")" : "")}; tall plants outside the map the fort's blast throws {RimBlastGiven}" + RimEdgeLine() + RimSeatStatus());
        }

        /// <summary>LateUpdate, before the land is built: the ring's game plants on, the low look's own on while it holds, the groves off.</summary>
        void RimEarlyShow()
        {
            if (!rimEarly || built) return;
            int low = OzL16FlyoverFlora.LowLookOn ? 1 : 0;
            if (low == rimShowLow) return;
            rimShowLow = low;
            foreach (var rc in rimCells.Values)
            {
                if (rc.holder) rc.holder.SetActive(true);
                if (rc.holderLow) rc.holderLow.SetActive(low == 1);
                if (rc.holderGrove) rc.holderGrove.SetActive(false);
            }
        }

        /// <summary>LateUpdate, once built: with the land (the low look off) the game's plants; while the low look holds the ring's every
        /// row and no grove (the low look's own far land stands beyond the rim's floor). The old trees stay off either way.</summary>
        void RimFixShow(bool on)
        {
            foreach (var rc in rimCells.Values)
            {
                if (!rc.applied) continue;
                if (rc.holder) rc.holder.SetActive(true);
                if (rc.holderLow) rc.holderLow.SetActive(!on && RimLowFull);
                if (rc.holderGrove) rc.holderGrove.SetActive(on);
            }
        }

        /// <summary>LateUpdate, every frame once the land is built: the new plants' drawing boxes. The wind widens a plant's box by its
        /// sway room (OzFloraWind.boundsPad: 5 m round a tree or palm, 2.4 m round a small plant), so the engine draws a plant that far
        /// outside the picture too. In the wide overview (OzL16DrawAhead's own switch: on above FarAbove + FarBand, off under FarAbove -
        /// FarBand) every cell's middle stands farther from the camera than the wind's movement reaches (it ends about 110 m out), so no
        /// plant outside the map moves there and each gets its own mesh's box back, as round 4's far meshes had theirs (off the engine,
        /// tools/culling_boxes.py: with the wind's boxes 316 of the 8068 reachable wide views drew more plant triangles than round 4,
        /// worst x1.27; on their own boxes none). Lower down the wind's boxes are given back. The edge plants' stand-ins likewise.</summary>
        void RimFixBoxes()
        {
            if ((RimCellsApplied == 0 && RimEdgeShown == 0) || !rts) return;
            float h = rts.height;
            bool own = rimOwnBox ? h >= OzL16DrawAhead.FarAbove - OzL16DrawAhead.FarBand : h > OzL16DrawAhead.FarAbove + OzL16DrawAhead.FarBand;
            if (own == rimOwnBox) return;
            rimOwnBox = own;
            foreach (var rc in rimCells.Values)
            {
                if (!rc.applied) continue;
                foreach (var r in rc.adds) RimBox(r, own);
            }
            foreach (var r in RimEdgeModels()) RimBox(r, own);
        }

        void RimBox(Renderer r, bool own)
        {
            if (!r) return;
            var wind = OzFloraWind.I;
            if (own)
            {
                if (!wind || !wind.TryGet(r, out _)) return;                                               // not hooked by the wind: its box is its own already
                if (!rimWindBox.ContainsKey(r)) rimWindBox[r] = r.localBounds;
                var mf = r.GetComponent<MeshFilter>(); if (mf && mf.sharedMesh) r.localBounds = mf.sharedMesh.bounds;
            }
            else if (rimWindBox.TryGetValue(r, out var wb)) r.localBounds = wb;
        }

        // ------------------------------------------------------------------ Play checks
        static string RimFixStatus() => !RimFixOn || !TreesOn || !FarLookOn ? ", round 5e off" :
            $", round 5e: old trees outside not drawn {RimNotDrawn}, new full-model plants {RimNewRing + RimNewGroves} (ring {RimNewRing}, groves {RimNewGroves}; low look's ring rows {RimNewLow}) in {RimCellsApplied} cells, cost limit {(RimBudgetOn ? "on" : "off")} (left out {RimOverBudget}, cells full {RimCellsFull}), left {RimCellsLeft}, refused {RimRefused}, sources measured {RimSourcesMeasured} (widest {RimSourceWidest:0.00} m)" + RimEdgeStatus() + RimSeatStatus();

        /// <summary>A renderer the camera may draw now: on, its object on, not held off (forceRenderingOff).</summary>
        static bool RimDrawn(Renderer r) => r && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy;

        /// <summary>Play check of everything drawn outside the map now (the land shown, the low look off): every plant one of the map's
        /// full models of RimKinds (no '_lite' or far shape, none of the big kinds), every crown at most RimCap on its own points (the
        /// true width; the box measure beside it); the old trees outside the map not drawn, the changed cells' far meshes without them;
        /// every cell inside its round-4 cost; the groves' feet on the land; the density by distance from the map's edge and the mix,
        /// printed; the largest crown against a factory; then the edge plants' band (RimEdgeCheck).</summary>
        public static string RimFixCheck()
        {
            var o = I; if (!o) return "OzL16Outskirts: not on this level";
            if (!o.built) return "OzL16Outskirts: the land is not built yet";
            if (!RimFixOn || !TreesOn || !FarLookOn) return "OzL16Outskirts round 5e: OFF" + (RimFrom.Length > 0 ? " (" + RimFrom + ")" : "");
            if (OzL16FlyoverFlora.LowLookOn) return "OzL16Outskirts round 5e: the low look holds the plants (wait for the start flyover's end)";
            var root = GameObject.Find(FloraRoot); if (!root) return "no " + FloraRoot;
            var sb = new StringBuilder(); int fail = 0;
            float m = OzL16FlyoverFlora.GroundMargin; var bandT = new int[6]; var bandS = new int[6]; var mix = new SortedDictionary<string, int>();
            int plants = 0, notFull = 0, over = 0, overBox = 0, oldOn = 0, unread = 0; float widest = 0f, widestBox = 0f, tallest = 0f; string widestName = "", notFullName = "", overName = "";
            foreach (Transform cell in root.transform)
                foreach (var mf in cell.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (!mf || !mf.sharedMesh || mf.name == "FAR") continue;
                    var r = mf.GetComponent<Renderer>(); if (!RimDrawn(r)) continue;
                    var t = mf.transform; float out_ = o.Outside(t.position); if (out_ <= 0f) continue;
                    plants++;
                    if (o.rimThin.Contains(r)) oldOn++;
                    int k = RimKindOf(mf.sharedMesh);
                    if (k < 0) { notFull++; if (notFullName.Length == 0) notFullName = t.name + " (" + mf.sharedMesh.name + ")"; continue; }
                    PlantSize(t, mf.sharedMesh, out float h, out float wb);
                    float w = TrueWidth(t, mf.sharedMesh); if (w < 0f) { unread++; w = wb; }
                    if (w > RimCap + 0.005f) { over++; if (overName.Length == 0) overName = $"{t.name} {w:0.00} m"; }
                    if (wb > RimCap + 0.005f) overBox++;
                    if (w > widest) { widest = w; widestName = t.name; }
                    widestBox = Mathf.Max(widestBox, wb); tallest = Mathf.Max(tallest, h);
                    bool tree = k >= 6;                                                                       // palms, mangroves and the canopy trees; the rest small plants
                    if (out_ <= m + 0.01f) { int b = Mathf.Min(5, Mathf.FloorToInt(out_ / 4f)); if (tree) bandT[b]++; else bandS[b]++; }
                    string kn = RimKinds[k].Substring(3); mix.TryGetValue(kn, out int c); mix[kn] = c + 1;
                }
            if (notFull > 0) fail++; if (over > 0) fail++; if (oldOn > 0) fail++; if (RimRefused > 0) fail++;
            // the changed cells: their far mesh is round 5b's job (no crown of an old tree outside the map), their cost, the groves' feet
            var jobOf = new Dictionary<Transform, FarJob>(); foreach (var j in o.farJobs) if (j.mesh) jobOf[j.cell] = j;
            int farBad = 0, costBad = 0, feet = 0, boxBad = 0; long new4 = 0, far4 = 0, farNow = 0; float sunkMax = 0f, overMax = float.MinValue;
            foreach (var rc in o.rimCells.Values)
            {
                if (!rc.applied) continue;
                var farT = rc.cell.Find("FAR"); var fmf = farT ? farT.GetComponent<MeshFilter>() : null;
                if (!jobOf.TryGetValue(rc.cell, out var job) || !fmf || (fmf.sharedMesh != job.mesh && !(fmf.sharedMesh && fmf.sharedMesh.name.StartsWith(job.mesh.name, System.StringComparison.Ordinal)))) { farBad++; continue; }
                long tris = 0; foreach (var r in rc.adds) { var amf = r ? r.GetComponent<MeshFilter>() : null; tris += amf ? Tris(amf.sharedMesh) : 0; }
                long now = Tris(fmf.sharedMesh), r4 = Tris(job.src);                                      // job.src: the cell's far mesh as round 4 draws it (Plan reads it before round 5's goes in)
                new4 += tris; far4 += r4; farNow += now;
                if (RimBudgetOn && rc.adds.Count > 0 && (!rc.budget || tris > rc.far || tris > rc.near || rc.adds.Count > rc.objs)) costBad++;      // the cell's limit (RimBudget)
                if (o.rimOwnBox)
                    foreach (var r in rc.adds) { var bmf = r ? r.GetComponent<MeshFilter>() : null; if (bmf && bmf.sharedMesh && r.localBounds != bmf.sharedMesh.bounds) boxBad++; }   // the wide overview: each on its own box (RimFixBoxes)
                foreach (var r in rc.adds)
                {
                    if (!r) continue; var p = r.transform.position;
                    if (o.Outside(p) <= m + 0.01f) continue;                                                // a ring plant: on the rim floor (placed off the engine on its triangles)
                    if (o.Gen(p.x, p.z) <= o.waterY + WetOver + 0.001f) continue;                        // a wet plant: its foot under the water
                    feet++; float hh = o.Gen(p.x, p.z), lo = o.Under(p.x, p.z, 64);
                    sunkMax = Mathf.Max(sunkMax, hh - p.y); overMax = Mathf.Max(overMax, p.y - lo);
                }
            }
            if (overMax == float.MinValue) overMax = -1f;
            bool feetOk = sunkMax <= FootMax + 0.001f && overMax <= 0.001f;
            if (farBad > 0) fail++; if (costBad > 0) fail++; if (!feetOk) fail++; if (RimCellsApplied == 0) fail++; if (boxBad > 0) fail++;
            string edge = o.RimEdgeCheck(out bool edgeOk); if (!edgeOk) fail++;
            string seat = o.RimSeatCheck(out bool seatOk); if (!seatOk) fail++;                       // round 5e's fixes (OzL16Outskirts.RimSeat.cs)
            sb.Append($"OzL16Outskirts round 5e: {(fail == 0 ? "PASS" : "FAIL")} | {plants} plants drawn outside the map");
            sb.Append($" | SIZES: {over} crowns over the cap {RimCap:0.00} m (true width: the widest horizontal extent of the model's own points{(unread > 0 ? $"; {unread} not readable, box measure" : "")}){(over > 0 ? " (" + overName + ")" : "")}, largest crown {widest:0.00} m ({widestName}) = x{widest / FactoryWidth:0.00} of a factory ({FactoryWidth:0} m); box measure: {overBox} over, largest {widestBox:0.00} m; tallest {tallest:0.0} m");
            sb.Append($" | MODELS: {notFull} not one of the map's full models of the outside kinds{(notFull > 0 ? " (" + notFullName + ")" : "")} (no '_lite' or far shape, none of the big kinds)");
            sb.Append($" | old trees outside the map still drawn {oldOn} (not drawn: {RimNotDrawn})");
            sb.Append($" | far meshes: {farBad} changed cells without round 5b's new far mesh");
            sb.Append($" | cost: {costBad} cells over their limit (RimBudget; far triangles of the changed cells at the widest zoom: round 4 {far4}, now {farNow} + new plants {new4}; limit {(RimBudgetOn ? "on" : "off")}, rows left out {RimOverBudget}, cells full {RimCellsFull})");
            sb.Append($" | drawing boxes: {(o.rimOwnBox ? $"the wide overview, the plants' own boxes, {boxBad} still widened by the wind" : "the wind's (not the wide overview)")}");
            sb.Append($" | grove feet: {feet} new plants, deepest {sunkMax:0.000} m under the land (at most {FootMax:0.00}), highest over it round the stem {overMax:0.000} m (at most 0) {(feetOk ? "ok" : "FAIL")}");
            sb.Append($" | data rows refused {RimRefused}{(RimWhy.Length > 0 ? " (" + RimWhy + ")" : "")}, cells changed {RimCellsApplied}, left as round 5b {RimCellsLeft}");
            sb.Append(" | rim ring per ha by 4 m band out (trees and palms / small plants; the map's own at the same distance in: 241/496, 176/318, 97/195, 71/80, then 71/80):");
            for (int b = 0; b < 6; b++)
            {
                float a0 = b * 4f, a1 = (b + 1) * 4f, area = (o.map.x + 2f * a1) * (o.map.y + 2f * a1) - (o.map.x + 2f * a0) * (o.map.y + 2f * a0);
                sb.Append($" {a0:0}-{a1:0} m {bandT[b] / area * 1e4f:0}/{bandS[b] / area * 1e4f:0}");
            }
            sb.Append(" (whole ring area, not drawn parts too) | outside by kind:");
            foreach (var kv in mix) sb.Append($" {kv.Key} {kv.Value}");
            sb.Append(" | ").Append(edge).Append(" | ").Append(seat);
            return sb.ToString();
        }

        /// <summary>Play helper for the counts: the plant triangles the game camera's view meets now: the plant cells' far meshes (as
        /// FarViewCount counts them), round 5e's plants outside the map and the edge plants' stand-ins (their own objects at every zoom,
        /// each counted by the drawing box the engine culls it by: its own in the wide overview, the wind's widened one lower down,
        /// RimFixBoxes), with their draws (one per material).</summary>
        public static string RimViewCount()
        {
            var o = I; var cam = o && o.rts ? o.rts.GetComponentInChildren<Camera>() : Camera.main; if (!cam) return "no camera";
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            var root = GameObject.Find(FloraRoot); if (!root) return "no " + FloraRoot;
            long farTris = 0, newTris = 0, edgeTris = 0; int farN = 0, newN = 0, edgeN = 0, draws = 0;
            foreach (Transform cell in root.transform)
            {
                var f = cell.Find("FAR"); var fr = f ? f.GetComponent<Renderer>() : null;
                if (!fr || !fr.enabled || !fr.gameObject.activeInHierarchy || !GeometryUtility.TestPlanesAABB(planes, fr.bounds)) continue;
                var fmf = f.GetComponent<MeshFilter>(); long t = fmf ? Tris(fmf.sharedMesh) : 0;
                if (t > 0) { farTris += t; farN++; }
            }
            if (o)
            {
                foreach (var rc in o.rimCells.Values)
                {
                    if (!rc.applied) continue;
                    foreach (var r in rc.adds)
                    {
                        if (!RimDrawn(r) || !GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                        var amf = r.GetComponent<MeshFilter>(); newTris += amf ? Tris(amf.sharedMesh) : 0; newN++; draws += r.sharedMaterials.Length;
                    }
                }
                foreach (var r in o.RimEdgeModels())
                {
                    if (!RimDrawn(r) || !GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                    var amf = r.GetComponent<MeshFilter>(); edgeTris += amf ? Tris(amf.sharedMesh) : 0; edgeN++; draws += r.sharedMaterials.Length;
                }
            }
            return $"plant triangles in view: far meshes {farN} ({farTris} tris) + round 5e's plants outside the map {newN} ({newTris} tris) + the edge plants' stand-ins {edgeN} ({edgeTris} tris), {draws} draws = {farTris + newTris + edgeTris} tris (without the stand-ins {farTris + newTris}) | camera {(o && o.rts ? o.rts.height : cam.transform.position.y):0} m";
        }

        /// <summary>Play helper for the pictures: the camera over the owner's picture of the west rim (156 m, focus 49.1 / -209.3; the
        /// side cut of the widest zoom stands back for it) or another place and height; height 0 gives the camera its limits again.</summary>
        public static string OwnerView(float height = 156f, float fx = 49.1f, float fz = -209.3f)
        {
            var o = I; var cam = o ? o.rts : null; if (!cam) return "no RTS camera";
            if (height <= 0f) { cam.unclamped = false; cam.Place(); return $"camera limits back: over ({cam.focus.x:0.0},{cam.focus.z:0.0}) at {cam.height:0} m"; }
            cam.unclamped = true; cam.focus = new Vector3(fx, 0f, fz); cam.SetHeightImmediate(height);
            return $"camera over ({cam.focus.x:0.0},{cam.focus.z:0.0}) at {cam.height:0} m (limits off until OwnerView(0))";
        }
    }
}
