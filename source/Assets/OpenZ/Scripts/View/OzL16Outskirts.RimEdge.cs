// OpenZ (GPL-3.0)
// Just inside the map line the level stands simplified ('_lite')
// trees and a few palms and bushes wider than the cap on the cliff tops round the playable floor; on screen they read as the jungle
// outside, at the widest zoom as their cells' far shapes, and with the outside thinned they were the biggest thing on the rim. They are
// decoration: the floor units can reach does not hold them. So their LOOK changes, nothing else about them:
//  - which: every plant drawn within RimEdgeBand m inside the map line on the rim's plateau (its foot above the playable floor's own
//    top, RimFloorTop: the highest the walkable floor stands 12 m or more inside the line; or its tile not on the floor units can reach:
//    the level's own map, its walkable tiles joined 8-ways from the largest area, a tile a tree of the rules closes counted as floor), and
//    every rim ring tree (RIM2_) with its foot inside the line, that is a '_lite' model or wider than RimCap (its true width), and every
//    '_lite' tree of the band wherever it stands (one stands on the floor at the east line, as wide as a factory); the list is made off
//    the engine (RimEdge) and each row is checked again here;
//  - its own renderer is held off (Renderer.forceRenderingOff: its object, name, transform, colliders, the rules' binding and the
//    felling stay exactly as they are), and a copy of an under-cap full model of the map (the closest kind that fits the cap, else a
//    canopy tree) stands in for it as its CHILD: on its foot, upright, the source's own size (no rescale), so a felling, a lean or a
//    knock tips the stand-in with it; a felled one is cut from the stand-in's own mesh (OzTreeFall: EdgeModelOf);
//  - the stand-in is drawn at every zoom (outside the cell's levels, as round 5d's plants outside the map; RimFixBoxes gives it its own
//    box in the wide overview); the plant's run leaves its cell's far mesh (Plan's hook RimEdgeCut, or RimEdgeFarCut for a cell Plan
//    does not make: the small plant cells), every point kept in its place so the felling's runs stay true;
//  - the parrots that sit on four of them (JungleFaunaData.TreeSeats) sit on the new crown (RimEdgeSeats, its highest points);
//  - the start flyover's low look and the endings show it the same (it is made in the level's first frame, RimEarly).
// Switch: RimEdgeOn (default true; read when the level starts; exe: -oz-l16-trees r5d = off). Drawn only: no rule reads any of it.
// Play checks: RimFixCheck (its band part: RimEdgeCheck); RimEdgeFell() then, a few seconds later, RimEdgeFellResult().
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public sealed partial class OzL16Outskirts
    {
        /// <summary>Round 5e: the map's edge plants on its cliff tops and rims drawn as under-cap full models. Set before the level starts.</summary>
        public static bool RimEdgeOn = true;
        /// <summary>Counters: edge plants given their stand-in, of them shown (their far run out), refused (a row that does not check out),
        /// given back (their far mesh did not come), parrot seats moved; runs cut in Plan / in the cells Plan does not make.</summary>
        public static int RimEdgeMade, RimEdgeShown, RimEdgeRefused, RimEdgeBack, RimEdgeSeatsMoved, RimEdgeCutPlan, RimEdgeCutOwn;
        public static string RimEdgeWhy = "";
        /// <summary>The plant kit's shapes that have no crown (the band's size test passes them by).</summary>
        static readonly string[] RimNoCrown = { "FL_LogFallenA", "FL_RockMossyA", "FL_RockMossyB", "FL_MoundSmall", "FL_MoundTall", "FL_LilyPadsA", "FL_LilyPadsB", "FL_PLT_Flowers_placeholder" };

        sealed class RimEdgePlant
        {
            public RimEdgeRow row; public Transform plant, cell; public Renderer lite; public RimSource src;
            public GameObject model; public Renderer modelR; public bool cut, shown; public int seatSpot = -1;
        }

        readonly List<RimEdgePlant> rimEdge = new List<RimEdgePlant>();
        readonly Dictionary<Transform, RimEdgePlant> rimEdgeBy = new Dictionary<Transform, RimEdgePlant>();
        readonly Dictionary<string, RimSource> rimEdgeSrc = new Dictionary<string, RimSource>();
        readonly List<Mesh> rimEdgeMeshes = new List<Mesh>();
        static readonly Dictionary<int, Vector3[]> rimSeatsOrig = new Dictionary<int, Vector3[]>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRimEdgeStatics()
        {   // every Play session: the switch on, the counters at 0, the parrots' seats as the data has them
            RimEdgeOn = true; RimEdgeMade = RimEdgeShown = RimEdgeRefused = RimEdgeBack = RimEdgeSeatsMoved = RimEdgeCutPlan = RimEdgeCutOwn = 0; RimEdgeWhy = "";
            RimSeatsBack();
        }

        /// <summary>OzTreeFall: the stand-in a felled edge plant is cut from and falls as (null: the plant draws itself).</summary>
        public static Transform EdgeModelOf(Transform plant) => I && plant && I.rimEdgeBy.TryGetValue(plant, out var e) && e.model ? e.model.transform : null;

        void RimEdgeReset()
        {
            RimEdgeMade = RimEdgeShown = RimEdgeRefused = RimEdgeBack = RimEdgeSeatsMoved = RimEdgeCutPlan = RimEdgeCutOwn = 0; RimEdgeWhy = "";
            rimEdge.Clear(); rimEdgeBy.Clear(); rimEdgeSrc.Clear(); rimEdgeMeshes.Clear(); rimFloor = null;
            RimEdgeFineReset();                                                                        // round 5e's fixes (OzL16Outskirts.RimSeat.cs)
            RimSeatsBack();
        }

        void RefuseEdge(string why) { RimEdgeRefused++; if (RimEdgeWhy.Length < 600) RimEdgeWhy += (RimEdgeWhy.Length > 0 ? "; " : "") + why; }

        // ------------------------------------------------------------------ the floor units can reach (the level's own map)
        bool[] rimFloor; int rimFloorW, rimFloorH;

        /// <summary>The floor units can reach: the map's walkable tiles (ground, road, shallow water; a tile a tree of the rules closes
        /// counts: the tree can be felled), joined 8-ways, the largest area. False = the map is not known.</summary>
        bool RimFloorMap()
        {
            if (rimFloor != null) return true;
            var w = ZGame.I ? ZGame.I.World : null; var map = w != null ? w.Map : null;
            if (map == null || map.classes == null || map.width <= 0 || map.height <= 0 || map.classes.Length != map.width * map.height) return false;
            int W = map.width, H = map.height; var walk = new bool[W * H];
            for (int i = 0; i < walk.Length; i++) { var c = (Sim.ZClass)map.classes[i]; walk[i] = c == Sim.ZClass.Ground || c == Sim.ZClass.Road || c == Sim.ZClass.Water; }
            var tt = Sim.ZTreeTilesData.For(map.file);
            if (tt != null && tt.Tiles != null)
                for (int i = 0; i + 1 < tt.Tiles.Length; i += 2)
                {
                    int x = tt.Tiles[i], y = tt.Tiles[i + 1];
                    if (x >= 0 && y >= 0 && x < W && y < H && (Sim.ZClass)map.classes[y * W + x] == Sim.ZClass.Ground) walk[y * W + x] = true;
                }
            var lab = new int[W * H]; for (int i = 0; i < lab.Length; i++) lab[i] = -1;
            var stack = new Stack<int>(); int n = 0, best = -1, bestSize = 0;
            for (int s = 0; s < lab.Length; s++)
            {
                if (!walk[s] || lab[s] >= 0) continue;
                int size = 0; lab[s] = n; stack.Push(s);
                while (stack.Count > 0)
                {
                    int q = stack.Pop(); size++; int qx = q % W, qy = q / W;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int ax = qx + dx, ay = qy + dy; if (ax < 0 || ay < 0 || ax >= W || ay >= H) continue;
                            int a = ay * W + ax; if (!walk[a] || lab[a] >= 0) continue;
                            lab[a] = n; stack.Push(a);
                        }
                }
                if (size > bestSize) { bestSize = size; best = n; }
                n++;
            }
            rimFloor = new bool[W * H]; for (int i = 0; i < lab.Length; i++) rimFloor[i] = lab[i] == best;
            rimFloorW = W; rimFloorH = H;
            return true;
        }

        /// <summary>A plant of the band: its foot inside the map line, at most RimEdgeBand m in, on a cliff top or rim (its foot above the
        /// playable floor's own top RimFloorTop, or its tile not the floor units can reach), a rim ring tree (RIM2_), or a simplified
        /// ('_lite') tree wherever it stands in the band (its own shape: a stand-in's plant keeps it).</summary>
        bool RimOnEdge(Transform t)
        {
            var p = t.position; if (Outside(p) > 0f) return false;
            float din = Mathf.Min(Mathf.Min(p.x, map.x - p.x), Mathf.Min(-p.z, map.y + p.z)); if (din > RimEdgeBand + 0.01f) return false;
            if (t.name.StartsWith("RIM2_", System.StringComparison.Ordinal) || p.y > RimFloorTop) return true;   // the rim's plateau: the Zod map calls some of its tiles walkable
            var own = t.GetComponent<MeshFilter>(); if (own && own.sharedMesh && FarKindOf(own.sharedMesh) >= 0) return true;   // a '_lite' tree on the floor reads as the jungle outside
            if (!RimFloorMap()) return false;
            int tx = Mathf.FloorToInt(p.x * 0.5f), ty = Mathf.FloorToInt(-p.z * 0.5f);                    // the rules' 2 m tiles (OzTreeFall's own)
            if (tx < 0 || ty < 0 || tx >= rimFloorW || ty >= rimFloorH) return true;
            return !rimFloor[ty * rimFloorW + tx];
        }

        // ------------------------------------------------------------------ the swap (RimEarly: the level's first frame)
        void RimEdgeSwap(GameObject root, Dictionary<string, Transform> byName)
        {
            foreach (var row in RimEdge)
            {
                var cellT = root.transform.Find(row.cell); if (!cellT) { RefuseEdge(row.cell + " is not in the level"); continue; }
                var t = FindDeep(cellT, row.plant);                                                        // a tree of the rules stands in its holder (OzTreeFall)
                if (!t || !t.gameObject.activeInHierarchy) { RefuseEdge(row.plant + " is not in its cell"); continue; }
                var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
                if (!mf || !mf.sharedMesh || !mr || !mr.enabled) { RefuseEdge(row.plant + " has no drawn shape"); continue; }
                if (rimEdgeBy.ContainsKey(t)) { RefuseEdge(row.plant + " twice"); continue; }
                if (Vector3.Angle(t.rotation * Vector3.back, Vector3.up) > 1f) { RefuseEdge(row.plant + " does not stand upright"); continue; }
                if (!RimOnEdge(t)) { RefuseEdge(row.plant + " is not on the map's cliff tops or rims"); continue; }
                bool lite = FarKindOf(mf.sharedMesh) >= 0;
                if (!lite && TrueWidth(t, mf.sharedMesh) <= RimCap + 0.005f) { RimEdgeFineAdd(t); continue; }                 // a full model under the cap already: drawn as it is, it needs no stand-in (OzL16Outskirts.RimSeat.cs)
                if (!rimEdgeSrc.TryGetValue(row.src, out var src))
                {
                    src = RimSourceOf(byName, row.src, row.kind);
                    if (src == null) { RefuseEdge(row.src + ": not a source of " + row.kind); continue; }
                    src.kind = -1; rimEdgeSrc[row.src] = src;
                }
                var ps = t.lossyScale;
                if (Mathf.Abs(ps.x - ps.y) > 1e-3f * Mathf.Abs(ps.x) || Mathf.Abs(src.scale.x - src.scale.y) > 1e-3f * Mathf.Abs(src.scale.x)) { RefuseEdge(row.plant + ": not even in its flat axes"); continue; }
                // its own renderer held off; the stand-in as its child, on its foot, upright, the source's own size
                OzFloraWind.Still(t.gameObject);                                                            // its own shape no more in the wind (no drips or seats from a crown not drawn)
                mr.forceRenderingOff = true;
                var go = new GameObject("EDGE_" + RimBaseName(src.mesh).Substring(3)) { layer = t.gameObject.layer };
                go.transform.SetParent(t, false);
                go.transform.SetPositionAndRotation(t.position, Quaternion.AngleAxis(row.yaw, Vector3.up) * src.rot);
                go.transform.localScale = new Vector3(src.scale.x / ps.x, src.scale.y / ps.y, src.scale.z / ps.z);
                go.AddComponent<MeshFilter>().sharedMesh = src.mesh;
                var nr = go.AddComponent<MeshRenderer>(); nr.sharedMaterials = src.mats; nr.shadowCastingMode = src.cast; nr.receiveShadows = src.receive;
                var e = new RimEdgePlant { row = row, plant = t, cell = cellT, lite = mr, src = src, model = go, modelR = nr };
                rimEdge.Add(e); rimEdgeBy[t] = e; RimEdgeMade++;
                OzTreeFall.EdgeResize(t, nr);                                                              // the fall's size is the stand-in's
                for (int i = 0; i < RimEdgeSeats.Length; i++)
                    if (RimEdgeSeats[i].plant == row.plant) { e.seatSpot = RimEdgeSeats[i].spot; RimSeatsSet(e.seatSpot, RimEdgeSeats[i].seats); }
            }
        }

        /// <summary>The parrots' seats of a tree spot on the stand-in's crown (the data's own seats kept to give back).</summary>
        static void RimSeatsSet(int spot, Vector3[] seats)
        {
            var d = JungleFaunaData.TreeSeats; if (spot < 0 || spot >= d.Length || d[spot] == null || seats == null) return;
            if (!rimSeatsOrig.ContainsKey(spot)) rimSeatsOrig[spot] = (Vector3[])d[spot].Clone();
            for (int k = 0; k < d[spot].Length && k < seats.Length; k++) d[spot][k] = seats[k];
            RimEdgeSeatsMoved++;
        }

        static void RimSeatBack(int spot)
        {
            if (!rimSeatsOrig.TryGetValue(spot, out var o)) return;
            var d = JungleFaunaData.TreeSeats; if (spot < d.Length && d[spot] != null) for (int k = 0; k < d[spot].Length && k < o.Length; k++) d[spot][k] = o[k];
        }

        static void RimSeatsBack() { foreach (var kv in rimSeatsOrig) RimSeatBack(kv.Key); }

        /// <summary>An edge plant back to its own look (its far mesh did not come, or its source failed the true width).</summary>
        void RimEdgeBackOne(RimEdgePlant e, string why)
        {
            if (e.model) Destroy(e.model);
            e.model = null; e.modelR = null; e.shown = false;
            if (e.lite) e.lite.forceRenderingOff = false;
            if (e.seatSpot >= 0) RimSeatBack(e.seatSpot);
            if (e.plant) OzTreeFall.EdgeResize(e.plant, null);
            RimEdgeBack++; if (RimEdgeWhy.Length < 600) RimEdgeWhy += (RimEdgeWhy.Length > 0 ? "; " : "") + e.row.plant + ": " + why;
        }

        void RimEdgeUndoAll()
        {
            foreach (var e in rimEdge) if (e.model) RimEdgeBackOne(e, "round 5e not made");
            rimEdge.Clear(); rimEdgeBy.Clear(); RimSeatsBack();
        }

        IEnumerable<RimSource> RimEdgeSources() => rimEdgeSrc.Values;

        void RimEdgeDrop(HashSet<RimSource> bad)
        {
            foreach (var e in rimEdge) if (e.model && bad.Contains(e.src)) RimEdgeBackOne(e, "its source's true crown is over the cap");
        }

        IEnumerable<Renderer> RimEdgeModels() { foreach (var e in rimEdge) if (e.shown && e.modelR) yield return e.modelR; }

        // ------------------------------------------------------------------ the far meshes
        /// <summary>The proven run of an edge plant in its cell's far mesh (RimEdgeRuns), and the mesh's own counts.</summary>
        static bool RimEdgeRunOf(string cell, string plant, out int points, out int triangles, out int start, out int count)
        {
            points = triangles = start = count = 0;
            foreach (var r in RimEdgeRuns)
                if (r.cell == cell && r.plant == plant) { points = r.points; triangles = r.triangles; start = r.start; count = r.count; return true; }
            return false;
        }

        /// <summary>A run checked against the plant: inside the mesh, every point on the plant's own shape's box (widened 1.5 m).</summary>
        static bool RimRunIsIts(RimEdgePlant e, List<Vector3> sv, Transform farT, int start, int count)
        {
            if (start < 0 || count <= 0 || start + count > sv.Count || !e.lite) return false;
            var mf = e.plant.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) return false;
            var box = WorldBox(mf.sharedMesh.bounds, e.plant.localToWorldMatrix); box.Expand(1.5f);
            for (int j = start; j < start + count; j++) if (!box.Contains(farT.TransformPoint(sv[j]))) return false;
            return true;
        }

        /// <summary>Plan (FarLook.cs), for a cell of EdgeRuns: every edge plant of the cell loses its run (its triangles leave the new far
        /// mesh, its points stay). Returns how many, or -1 when a run is not its plant's own (Plan then leaves the cell as it is and
        /// RimEdgeApply gives its edge plants their own look back).</summary>
        int RimEdgeCut(Transform cell, int nv, List<Vector3> sv, Transform farT, bool[] drop)
        {
            if (!RimFixOn || rimEdge.Count == 0) return 0;
            int n = 0;
            foreach (var e in rimEdge)
            {
                if (e.cell != cell || !e.model) continue;
                if (!RimEdgeRunOf(cell.name, e.row.plant, out int pts, out _, out int a, out int c) || pts != nv || !RimRunIsIts(e, sv, farT, a, c)) return -1;
                for (int j = a; j < a + c; j++) drop[j] = true;
                e.cut = true; n++; RimEdgeCutPlan++;
            }
            return n;
        }

        /// <summary>A cell with edge plants whose far mesh Plan does not make (a small plant cell, a tree cell without rim plants): its new
        /// far mesh made here from the one it has now (the saved one, or the felling's copy of it), the edge plants' triangles out,
        /// every point in its place; readable (the felling folds a felled plant's run in it).</summary>
        bool RimEdgeFarCut(Transform cell, List<RimEdgePlant> es)
        {
            var farT = cell.Find("FAR"); var mf = farT ? farT.GetComponent<MeshFilter>() : null; var src = mf ? mf.sharedMesh : null;
            if (!src || !src.isReadable || src.subMeshCount != 1 || src.GetTopology(0) != MeshTopology.Triangles) return false;
            var sv = new List<Vector3>(); src.GetVertices(sv);
            var drop = new bool[sv.Count];
            foreach (var e in es)
            {
                if (!RimEdgeRunOf(cell.name, e.row.plant, out int pts, out int tris, out int a, out int c) || pts != sv.Count || tris != Tris(src) || !RimRunIsIts(e, sv, farT, a, c)) return false;
                for (int j = a; j < a + c; j++) drop[j] = true;
            }
            var idx = src.GetIndices(0); var keep = new List<int>(idx.Length);
            for (int i = 0; i + 2 < idx.Length; i += 3)
            {
                bool d0 = drop[idx[i]], d1 = drop[idx[i + 1]], d2 = drop[idx[i + 2]];
                if (d0 && d1 && d2) continue;
                if (d0 || d1 || d2) return false;                                                          // a triangle across a run's edge: not this mesh
                keep.Add(idx[i]); keep.Add(idx[i + 1]); keep.Add(idx[i + 2]);
            }
            var mesh = Instantiate(src); mesh.name = src.name + " (round 5e)";
            mesh.SetTriangles(keep, 0); mesh.RecalculateBounds(); mesh.UploadMeshData(false);
            mf.sharedMesh = mesh; rimEdgeMeshes.Add(mesh);
            foreach (var e in es) { e.cut = true; RimEdgeCutOwn++; }
            return true;
        }

        /// <summary>RimFixApply, with the land: each edge plant whose run left its cell's far mesh (Plan's new one in place, or one made
        /// here) is shown in its new look; any other gets its own look back.</summary>
        void RimEdgeApply(Dictionary<Transform, Mesh> jobMesh)
        {
            var byCell = new Dictionary<Transform, List<RimEdgePlant>>();
            foreach (var e in rimEdge) { if (!e.model) continue; if (!byCell.TryGetValue(e.cell, out var l)) byCell[e.cell] = l = new List<RimEdgePlant>(); l.Add(e); }
            foreach (var kv in byCell)
            {
                var farT = kv.Key.Find("FAR"); var mf = farT ? farT.GetComponent<MeshFilter>() : null;
                bool planned = mf && jobMesh.TryGetValue(kv.Key, out var jm) && (mf.sharedMesh == jm || (mf.sharedMesh && mf.sharedMesh.name.StartsWith(jm.name, System.StringComparison.Ordinal)));
                bool ok;
                if (planned) ok = true;
                else
                {   // Plan made nothing here (or left the cell as it was): this round's own cut
                    foreach (var e in kv.Value) e.cut = false;
                    ok = !jobMesh.ContainsKey(kv.Key) && RimEdgeFarCut(kv.Key, kv.Value);
                }
                foreach (var e in kv.Value)
                {
                    if (ok && e.cut) { e.shown = true; RimEdgeShown++; }
                    else RimEdgeBackOne(e, "its cell's far mesh did not come");
                }
            }
        }

        string RimEdgeLine() => !RimEdgeOn ? "; edge plants: off" :
            $"; edge plants on the cliff tops and rims in their new look {RimEdgeShown + RimEdgeFine} of {RimEdge.Length} (stand-ins {RimEdgeShown}, full models under the cap already {RimEdgeFine}; runs out of the far meshes: Plan {RimEdgeCutPlan}, this round's own {RimEdgeCutOwn}), given back {RimEdgeBack}, refused {RimEdgeRefused}, parrot seats moved {RimEdgeSeatsMoved}{(RimEdgeWhy.Length > 0 ? " (" + RimEdgeWhy + ")" : "")}";

        static string RimEdgeStatus() => !RimEdgeOn ? ", edge plants off" : $", edge plants {RimEdgeShown + RimEdgeFine} of {RimEdge.Length} (stand-ins {RimEdgeShown}, full models under the cap already {RimEdgeFine}, back {RimEdgeBack}, refused {RimEdgeRefused}, seats moved {RimEdgeSeatsMoved})";

        // ------------------------------------------------------------------ the Play checks
        /// <summary>The band (RimFixCheck): every plant drawn now with its foot within RimEdgeBand m inside the map line on a cliff top or
        /// rim, by the level's own map: none a '_lite' model, every crown at most RimCap on its own points; every edge plant's stand-in drawn
        /// and upright on its plant's foot, its own shape held off, its run gone from its cell's far mesh (no triangle uses a point of it).</summary>
        string RimEdgeCheck(out bool ok)
        {
            ok = true;
            if (!RimEdgeOn) return "EDGE: off (RimEdgeOn false)";
            var root = GameObject.Find(FloraRoot); if (!root) { ok = false; return "EDGE: no " + FloraRoot; }
            if (!RimFloorMap()) { ok = false; return "EDGE: the level's map is not known"; }
            int seen = 0, lite = 0, over = 0, unread = 0; float widest = 0f; string liteName = "", overName = "", widestName = "";
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (!mf || !mf.sharedMesh || mf.name == "FAR") continue;
                var r = mf.GetComponent<Renderer>(); if (!RimDrawn(r)) continue;
                var t = mf.transform; var foot = rimEdgeBy.ContainsKey(t.parent ? t.parent : t) ? t.parent : t;   // a stand-in: its plant's foot and band
                if (!RimOnEdge(foot) || OzTreeFallDown(foot) || System.Array.IndexOf(RimNoCrown, RimBaseName(mf.sharedMesh)) >= 0) continue;   // a felled plant lies; a log, rock, mound, lily pad or the joined flowers has no crown
                seen++;
                if (FarKindOf(mf.sharedMesh) >= 0 || mf.sharedMesh.name.Contains("_lite")) { lite++; if (liteName.Length == 0) liteName = t.name; continue; }
                float w = TrueWidth(t, mf.sharedMesh); if (w < 0f) { unread++; PlantSize(t, mf.sharedMesh, out _, out w); }
                if (w > RimCap + 0.005f) { over++; if (overName.Length == 0) overName = $"{t.name} {w:0.00} m"; }
                if (w > widest) { widest = w; widestName = t.name; }
            }
            int notShown = 0, liteOn = 0, offFoot = 0, runLeft = 0;
            foreach (var e in rimEdge)
            {
                if (!e.shown) continue;
                if (!RimDrawn(e.modelR)) notShown++;
                if (RimDrawn(e.lite)) liteOn++;
                if (e.model && e.plant && ((e.model.transform.position - e.plant.position).sqrMagnitude > 1e-4f || (!OzTreeFallDown(e.plant) && Vector3.Angle(e.model.transform.rotation * Vector3.back, Vector3.up) > 1f))) offFoot++;
                if (RimRunLeft(e)) runLeft++;
            }
            int fineBad = RimEdgeFineBad(out string fineWhy);                                          // the ones that need no stand-in: drawn as they are, full models under the cap (OzL16Outskirts.RimSeat.cs)
            if (lite > 0 || over > 0 || notShown > 0 || liteOn > 0 || offFoot > 0 || runLeft > 0 || RimEdgeRefused > 0 || RimEdgeShown == 0 || RimEdgeShown + RimEdgeFine != RimEdge.Length || fineBad > 0) ok = false;
            return $"EDGE (the map's cliff tops and rims, {RimEdgeBand:0} m inside the line): {(ok ? "PASS" : "FAIL")} | {seen} plants drawn in the band, '_lite' models {lite}{(lite > 0 ? " (" + liteName + ")" : "")}, crowns over the cap {over}{(over > 0 ? " (" + overName + ")" : "")}, widest {widest:0.00} m ({widestName}) = x{widest / FactoryWidth:0.00} of a factory{(unread > 0 ? $", {unread} not readable (box measure)" : "")}" +
                   $" | stand-ins {RimEdgeShown} + full models under the cap already {RimEdgeFine} = {RimEdgeShown + RimEdgeFine} of {RimEdge.Length}{(RimEdgeFine > 0 ? " (they need none: " + RimEdgeFineNames + (fineBad > 0 ? "; NOT drawn as full models under the cap: " + fineWhy : "") + ")" : "")} (not drawn {notShown}, their own shape still drawn {liteOn}, off their foot or not upright {offFoot}, a run still in its far mesh {runLeft}), given back {RimEdgeBack}, refused {RimEdgeRefused}, parrot seats moved {RimEdgeSeatsMoved}{(RimEdgeWhy.Length > 0 ? " (" + RimEdgeWhy + ")" : "")}";
        }

        static bool OzTreeFallDown(Transform plant)
        {   // a felled edge plant lies in its holder: the stand-in lies with it
            var p = plant ? plant.parent : null;
            return p && (p.name.StartsWith("TreeTile_", System.StringComparison.Ordinal) || p.name.StartsWith("TreeBlast_", System.StringComparison.Ordinal)) && Vector3.Angle(p.up, Vector3.up) > 1f;
        }

        /// <summary>Does any triangle of the cell's far mesh still use a point of the plant's run?</summary>
        bool RimRunLeft(RimEdgePlant e)
        {
            var farT = e.cell ? e.cell.Find("FAR") : null; var mf = farT ? farT.GetComponent<MeshFilter>() : null; var m = mf ? mf.sharedMesh : null;
            if (!m || !RimEdgeRunOf(e.cell.name, e.row.plant, out _, out _, out int a, out int c)) return true;
            if (!m.isReadable) return true;                                                               // every edge cell holds map plants: its far mesh stays readable (else it cannot be told: counted)
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var idx = m.GetIndices(s);
                for (int i = 0; i < idx.Length; i++) if (idx[i] >= a && idx[i] < a + c) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ the felling check (Play, test only)
        static readonly List<(RimEdgePlant e, Quaternion rot, Vector3 foot)> rimFell = new List<(RimEdgePlant, Quaternion, Vector3)>();   // foot: where the stand-in stood at the break
        static long rimFellHash, rimFellHashAfter;

        /// <summary>The world's state for a before / after (every object's id, centre, hit points, destroyed).</summary>
        public static long RimWorldHash()
        {
            var w = ZGame.I ? ZGame.I.World : null; if (w == null) return 0;
            long h = 17;
            unchecked { foreach (var o in w.Objects) { h = h * 31 + o.Id; h = h * 31 + o.CenterX; h = h * 31 + o.CenterY; h = h * 31 + o.Health; h = h * 31 + (o.Destroyed ? 1 : 0); } }
            return h;
        }

        /// <summary>Test only: up to n edge plants of the rules' felling list (they carry their DetailProp) broken by the view (DetailProp.Break,
        /// the same break the rules' Death gives them): their stand-ins must fall with them. The world's hash right before and right after
        /// the breaks, in the same frame (no tick between): the view's fell changes nothing in the rules. RimEdgeFellResult() a few seconds
        /// later reads the falls.</summary>
        public static string RimEdgeFell(int n = 3)
        {
            var o = I; if (!o || !o.built) return "edge fell: the land is not built yet";
            rimFell.Clear(); rimFellHash = RimWorldHash(); var sb = new StringBuilder();
            foreach (var e in o.rimEdge)
            {
                if (rimFell.Count >= n) break;
                if (!e.shown || !e.plant || !e.model) continue;
                var dp = e.plant.GetComponent<DetailProp>(); if (!dp || dp.Broken) continue;
                rimFell.Add((e, e.model.transform.rotation, e.model.transform.position));
                dp.Break(e.plant.position + new Vector3(3f, 0f, 1f));
                sb.Append(sb.Length > 0 ? ", " : "").Append(e.row.plant);
            }
            rimFellHashAfter = RimWorldHash();
            return $"edge fell: {rimFell.Count} edge plants of the rules broken ({sb}), world hash before / after the breaks {rimFellHash} / {rimFellHashAfter} {(rimFellHash == rimFellHashAfter ? "(the same)" : "(DIFFERENT)")}; ask RimEdgeFellResult() in about 6 s";
        }

        /// <summary>Test only: each broken edge plant's stand-in has tipped with it (turned more than 45 degrees from where it stood), it is
        /// drawn, the plant's own shape is still held off, its stump is cut from the stand-in's mesh and stands where the stand-in stood; the world's hash at the breaks.</summary>
        public static string RimEdgeFellResult()
        {
            var o = I; if (!o) return "edge fell: not on this level";
            int tipped = 0, drawn = 0, held = 0, stump = 0; var sb = new StringBuilder();
            var stumps = new List<MeshFilter>(); var root = GameObject.Find(FloraRoot);
            if (root) foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) if (mf && mf.name.StartsWith("TreeStump_", System.StringComparison.Ordinal) && mf.sharedMesh) stumps.Add(mf);
            foreach (var (e, rot, foot) in rimFell)
            {
                float turn = e.model ? Quaternion.Angle(rot, e.model.transform.rotation) : 0f;
                if (turn > 45f) tipped++;
                if (e.modelR && e.modelR.enabled && e.model.activeInHierarchy) drawn++;
                if (e.lite && (e.lite.forceRenderingOff || !e.lite.gameObject.activeInHierarchy)) held++;
                string want = e.src != null && e.src.mesh ? e.src.mesh.name + "_stump" : "?";
                foreach (var s in stumps) if (s.sharedMesh.name == want && (s.transform.position - foot).sqrMagnitude < 0.01f) { stump++; break; }   // at its foot: the fall turns the plant about its hinge (OzTreeFall.Fall), its own point ends up to 0.7 m away
                sb.Append($" {e.row.plant}: turned {turn:0} deg");
            }
            bool ok = rimFell.Count > 0 && tipped == rimFell.Count && drawn == rimFell.Count && held == rimFell.Count && stump == rimFell.Count && rimFellHash == rimFellHashAfter;
            return $"edge fell: {(ok ? "PASS" : "FAIL")} | {rimFell.Count} broken, stand-ins tipped with their tree {tipped}, drawn {drawn}, own shape held off {held}, stumps cut from the stand-in {stump} | world hash at the breaks {rimFellHash} / {rimFellHashAfter} {(rimFellHash == rimFellHashAfter ? "the same" : "DIFFERENT")} |{sb}";
        }
    }
}
