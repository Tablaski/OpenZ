// OpenZ (GPL-3.0)
// At the widest zoom every plant cell of the level shows its one joined far mesh (OzL16DrawAhead: plants far above FarAbove), and in
// it every tree is its thinned far shape: a scatter of loose leaf triangles on a stick. Round 5 put the map's own trees in the groves
// beyond the rim, but far away they showed those same triangles, and so did the rim ring's own trees next to them. Now every tree
// whose foot stands outside the map (the rim ring's own trees and the groves' trees) has a far tree shape made from its own near
// shape and its own picture (OzL16Outskirts.FarLookShapes.cs): a closed crown of leaf clumps round an inner blob on its trunk, the
// light and dark leaf patches and the colours of the near shape, as big on screen as the near shape and never bigger.
//  - It is drawn exactly as before: inside each cell's own far mesh (no new object, no new material, no new draw), the cell's other
//    plants untouched (their runs, OzL16Outskirts.FarLookRuns.cs, checked again here). Every original point keeps its place in the
//    mesh, so the felling of a map tree (OzTreeFall: it folds the tree's run onto one point) still takes it out of the far view.
//  - Inside the map nothing changes (the level's rule): the map's own trees keep their far shapes. FarLookMapTrees (off by default)
//    gives the map's own trees of the same 8 kinds in the plant cells (most stand along the map's edge, next to the rim ring) the new
//    far shape too, written INTO each one's own run (the run's spare points repeat its first one), so the felling still folds it out
//    and a felled one stays gone.
//  - The triangles per tree are about those of the far shapes it replaces (132 against 67-461): at the widest zoom the plant cells
//    draw fewer triangles than round 5 and than round 4 in each of the nine edge views (off-engine count: proof).
//  - Near (the normal zoom) nothing changes: the near shapes are the trees' own.
// Switches: FarLookOn (default true; false = round 5 as it was), FarLookMapTrees (default false), read when the level starts.
// Command line (the exe bench): -oz-l16-trees r4 | r5 | r5b | r5bmap. Drawn only: no rule reads it.
// Play check: return OpenZ.OzL16Outskirts.FarLookCheck();
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public sealed partial class OzL16Outskirts
    {
        /// <summary>Round 5b (default): the trees outside the map (the rim ring's, the groves') draw their own far tree shape at the
        /// widest zoom. Off = round 5 as it was (for a before / after count); set before the level starts.</summary>
        public static bool FarLookOn = true;
        /// <summary>Also the map's own trees of the 8 kinds in the plant cells (with FarLookOn). Off by default: inside the map nothing
        /// changes. Set before the level starts.</summary>
        public static bool FarLookMapTrees = false;
        /// <summary>Counters: cells whose far mesh holds the new shapes, trees on them (the rim ring's, the groves', the map's own), cells
        /// left as they were (a run did not check out), the far tree shapes loaded, the far triangles of those cells as round 5 draws them
        /// and now.</summary>
        public static int FarLookCells, FarLookRim, FarLookGroves, FarLookMap, FarLookRefused, FarLookKinds;
        public static long FarLookTrisBefore, FarLookTrisAfter;
        public static string FarLookWhy = "", FarLookFrom = "";

        sealed class FarPart { public string name; public Vector3[] v, n; public Color32[] c; public int[] t; }
        sealed class FarJob { public Transform cell; public Grove slot; public MeshFilter mf; public Mesh src, mesh; public int rim, groves, map; }

        FarPart[] farParts;
        readonly List<FarJob> farJobs = new List<FarJob>();
        readonly HashSet<Transform> farAsIs = new HashSet<Transform>();                                  // cells looked at with nothing to change

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFarLookStatics()
        {   // every Play session starts with round 5b on, the map's own trees as they are (the switches are read when the level starts)
            FarLookOn = true; FarLookMapTrees = false; FarLookCells = FarLookRim = FarLookGroves = FarLookMap = FarLookRefused = FarLookKinds = 0;
            FarLookTrisBefore = FarLookTrisAfter = 0; FarLookWhy = ""; FarLookFrom = "";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void FarLookArgs()
        {   // after every reset above: -oz-l16-trees r4 | r5 | r5b | r5bmap picks the round for a whole run of the game (the exe bench's A / B)
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < a.Length; i++)
            {
                if (a[i] != "-oz-l16-trees") continue;
                string w = a[i + 1].ToLowerInvariant();
                if (w == "r4") { TreesOn = false; FarLookOn = false; FarLookMapTrees = false; }
                else if (w == "r5") { TreesOn = true; FarLookOn = false; FarLookMapTrees = false; }
                else if (w == "r5b") { TreesOn = true; FarLookOn = true; FarLookMapTrees = false; }
                else if (w == "r5bmap") { TreesOn = true; FarLookOn = true; FarLookMapTrees = true; }
                else continue;
                FarLookFrom = "command line " + w;
                Debug.Log("[OzL16Outskirts] level 16 trees beyond the map: round " + w.Substring(1) + " (" + FarLookFrom + ")");
            }
        }

        /// <summary>How far a plant stands outside the map (m; 0 on it), by its own point (its foot).</summary>
        float OutsideFoot(Transform t) => Outside(t.position);

        static int FarKindOf(Mesh m)
        {   // the near shape's name, a copy of a shape ("_001") is the same shape
            if (!m) return -1; string n = m.name;
            int u = n.LastIndexOf('_');
            if (u > 0 && n.Length - u == 4 && char.IsDigit(n[u + 1]) && char.IsDigit(n[u + 2]) && char.IsDigit(n[u + 3])) n = n.Substring(0, u);
            return System.Array.IndexOf(FarKinds, n);
        }

        bool LoadFarParts()
        {
            if (farParts != null) return true;
            var ps = new FarPart[FarKinds.Length];
            for (int k = 0; k < FarKinds.Length; k++)
            {
                var d = FarShapeData[k]; int nv = d.v.Length / 3;
                if (d.n.Length != nv * 3 || d.c.Length != nv * 4 || d.t.Length % 3 != 0) { FarLookWhy = FarKinds[k] + ": its far shape data is broken"; return false; }
                var p = new FarPart { name = FarKinds[k], v = new Vector3[nv], n = new Vector3[nv], c = new Color32[nv], t = d.t };
                for (int i = 0; i < nv; i++)
                {
                    p.v[i] = new Vector3(d.v[3 * i], d.v[3 * i + 1], d.v[3 * i + 2]);
                    p.n[i] = new Vector3(d.n[3 * i], d.n[3 * i + 1], d.n[3 * i + 2]);
                    p.c[i] = new Color32(d.c[4 * i], d.c[4 * i + 1], d.c[4 * i + 2], d.c[4 * i + 3]);
                }
                foreach (int q in d.t) if (q < 0 || q >= nv) { FarLookWhy = FarKinds[k] + ": a triangle points past its shape"; return false; }
                ps[k] = p;
            }
            farParts = ps; FarLookKinds = ps.Length;
            return true;
        }

        // ------------------------------------------------------------------ build (a step per cell: BuildSliced, after GrowTrees)
        IEnumerable<bool> FarLookSteps()
        {
            FarLookCells = FarLookRim = FarLookGroves = FarLookMap = FarLookRefused = 0; FarLookTrisBefore = FarLookTrisAfter = 0; FarLookWhy = "";
            farJobs.Clear(); farAsIs.Clear();
            if (!TreesOn || !FarLookOn) yield break;
            if (!LoadFarParts()) { Debug.LogWarning("[OzL16Outskirts] round 5b: " + FarLookWhy); yield break; }
            var root = GameObject.Find(FloraRoot); if (!root) yield break;
            var slotOf = new Dictionary<Transform, Grove>(); foreach (var s in slots) slotOf[s.cell] = s;
            var gone = new HashSet<GameObject>(toHide);
            var done = new HashSet<Transform>();
            foreach (var row in EdgeRuns)
            {   // the cells with rim plants or the map's own trees of the kinds (and, across the rim's edge, the groves' trees too); a cell
                // with only map trees and FarLookMapTrees off has nothing to change (Plan: as it is)
                var cell = root.transform.Find(row.cell);
                if (!cell || gone.Contains(cell.gameObject)) continue;
                slotOf.TryGetValue(cell, out var s);
                done.Add(cell); Plan(cell, row, s);
                yield return true;
            }
            foreach (var s in slots)
            {   // the grove cells wholly beyond the rim's edge: only the groves' trees
                if (done.Contains(s.cell) || gone.Contains(s.cell.gameObject)) continue;
                Plan(s.cell, null, s);
                yield return true;
            }
        }

        /// <summary>One cell: its new far mesh made from its ORIGINAL far mesh: the runs of the trees of the kinds and of the plants
        /// round 5 hides leave its triangles (every point keeps its place: the felling's runs stay true); each tree outside the map
        /// that stays (the rim ring's, the groves') gets its far tree shape at its own place, turn and size, after the original points;
        /// with FarLookMapTrees each of the map's own trees of the kinds gets it inside its own run (so the felling's fold of that run
        /// still takes it out).</summary>
        void Plan(Transform cell, EdgeCell row, Grove s)
        {
            if (s != null && (s.empty || (s.trees.Count == 0 && s.kept.Count == 0))) return;                // round 5 does not draw it at all
            if (s != null && s.refused) s = null;                                                         // round 5 left it as round 4 had it: its trees outside the map change their far shape only
            var farT = cell.Find("FAR"); var mf = farT ? farT.GetComponent<MeshFilter>() : null; var src = mf ? mf.sharedMesh : null;
            if (!src) return;
            void Refuse(string why) { FarLookRefused++; if (FarLookWhy.Length < 600) FarLookWhy += (FarLookWhy.Length > 0 ? "; " : "") + cell.name + ": " + why; }
            if (!src.isReadable) { Refuse("its far mesh cannot be read"); return; }
            int nv = src.vertexCount;
            var drop = new bool[nv]; bool any = false;
            var adds = new List<(int kind, Matrix4x4 m)>(); var inRun = new List<(int kind, Matrix4x4 m, int at, int count)>(); int rim = 0, grove = 0, map = 0;
            var sv = new List<Vector3>(); src.GetVertices(sv);
            if (row != null)
            {
                long tris = Tris(src);
                if (nv != row.points || tris != row.triangles) { Refuse($"not the far mesh the runs were made from ({nv} points, {tris} triangles)"); return; }
                var hidden = new HashSet<Renderer>(); if (s != null) foreach (var r in s.hidden) if (r) hidden.Add(r);
                int listedHidden = 0;
                for (int i = 0; i < row.names.Length; i++)
                {   // FindDeep: the felling puts a map tree under a holder of its own inside the cell (OzTreeFall)
                    var pt = FindDeep(cell, row.names[i]);
                    if (!pt) { Refuse(row.names[i] + " is not in the cell"); return; }
                    float outer = OutsideFoot(pt);
                    if (outer <= 0f && !FarLookMapTrees) continue;                                               // a tree of the map: stays exactly as it is (felled or not)
                    int ra = row.starts[i], rn = row.counts[i];
                    if (ra < 0 || rn <= 0 || ra + rn > nv) { Refuse(row.names[i] + ": its run lies outside the mesh"); return; }
                    if (outer <= 0f && Folded(sv, ra, rn)) continue;                                             // a map tree already felled (its run folded onto one point): it stays gone
                    var r = pt.GetComponent<Renderer>(); var pmf = pt.GetComponent<MeshFilter>();
                    if (!r || !pmf || !pmf.sharedMesh) { Refuse(row.names[i] + " has no drawn shape"); return; }
                    bool thin = RimThinned(r), hid = hidden.Contains(r) || thin, tree = !hid && row.kinds[i] >= 0 && outer > 0f, own = FarLookMapTrees && !hid && row.kinds[i] >= 0 && outer <= 0f;   // round 5d: not drawn (OzL16Outskirts.RimFix.cs)
                    if (hid && !thin) listedHidden++;
                    if (!hid && !tree && !own) continue;                                                         // stays as it is
                    if ((tree || own) && FarKindOf(pmf.sharedMesh) != row.kinds[i]) { Refuse(row.names[i] + ": its shape is not the one the runs list"); return; }
                    var box = WorldBox(pmf.sharedMesh.bounds, pt.localToWorldMatrix); box.Expand(1.5f);
                    for (int j = ra; j < ra + rn; j++) { if (!box.Contains(farT.TransformPoint(sv[j]))) { Refuse(row.names[i] + ": its run is not its own points"); return; } drop[j] = true; }
                    any = true;
                    if (tree) { adds.Add((row.kinds[i], pt.localToWorldMatrix)); rim++; }
                    else if (own)
                    {
                        if (farParts[row.kinds[i]].v.Length > rn) { Refuse(row.names[i] + ": its far tree shape does not fit in its run"); return; }
                        inRun.Add((row.kinds[i], pt.localToWorldMatrix, ra, rn)); map++;
                    }
                }
                int edgeCut = RimEdgeCut(cell, nv, sv, farT, drop);                                             // round 5e: the edge plants' runs out (OzL16Outskirts.RimEdge.cs)
                if (edgeCut < 0) { Refuse("an edge plant's run is not its own points"); return; }
                if (edgeCut > 0) any = true;
                if (s != null && listedHidden != hidden.Count) { Refuse($"{hidden.Count - listedHidden} of the plants round 5 hides are not listed"); return; }
            }
            else
            {   // wholly beyond the rim's edge: every plant of it is hidden by round 5
                if (s == null || s.kept.Count > 0) { if (s != null) Refuse("its rim plants have no runs"); return; }
                for (int j = 0; j < nv; j++) drop[j] = true;
                any = true;
            }
            if (s != null)
                foreach (var tr in s.trees)
                {
                    if (RimThinned(tr)) continue;                                                              // round 5d: not drawn (OzL16Outskirts.RimFix.cs)
                    var tmf = tr ? tr.GetComponent<MeshFilter>() : null; int k = tmf ? FarKindOf(tmf.sharedMesh) : -1;
                    if (k < 0) { Refuse(tr ? tr.name + ": not one of the tree kinds" : "a grove tree is gone"); return; }
                    adds.Add((k, tr.transform.localToWorldMatrix)); grove++;
                }
            if (!any && adds.Count == 0 && inRun.Count == 0) { farAsIs.Add(cell); return; }                 // nothing to change (its map trees of the kinds all felled already)
            // the triangles that stay: every one wholly outside the dropped runs (one across a run's edge: not this mesh)
            var keepTri = new List<int>(); long before = 0;
            for (int sm = 0; sm < src.subMeshCount; sm++)
            {
                if (src.GetTopology(sm) != MeshTopology.Triangles) { Refuse("not a triangle mesh"); return; }
                var idx = src.GetIndices(sm);
                for (int i = 0; i + 2 < idx.Length; i += 3)
                {
                    before++;
                    bool d0 = drop[idx[i]], d1 = drop[idx[i + 1]], d2 = drop[idx[i + 2]];
                    if (d0 && d1 && d2) continue;
                    if (d0 || d1 || d2) { Refuse("a triangle crosses a run's edge"); return; }
                    keepTri.Add(idx[i]); keepTri.Add(idx[i + 1]); keepTri.Add(idx[i + 2]);
                }
            }
            // the points: the original ones in their places (when any of their triangles stays, or a map tree's shape goes into its run),
            // then the new shapes of the trees outside the map
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<Vector4>(); var c = new List<Color32>(); var tri = new List<int>(keepTri);
            bool keepOld = keepTri.Count > 0 || inRun.Count > 0;
            if (keepOld)
            {
                v.AddRange(sv); src.GetNormals(n); src.GetTangents(t); src.GetColors(c);
                if (n.Count != nv) { n.Clear(); for (int i = 0; i < nv; i++) n.Add(Vector3.up); }
                if (t.Count != nv) { t.Clear(); for (int i = 0; i < nv; i++) t.Add(new Vector4(1f, 0f, 0f, 1f)); }
                if (c.Count != nv) { c.Clear(); for (int i = 0; i < nv; i++) c.Add(new Color32(255, 255, 255, 255)); }
            }
            var toFar = farT.worldToLocalMatrix;
            foreach (var ip in inRun)
            {   // a map tree: its far tree shape written into its own run; the run's points past the shape repeat the shape's first point (no
                // triangle uses them), so the run still lies on the tree and the felling's fold (all its points onto one) takes the tree out
                var p = farParts[ip.kind]; var M = toFar * ip.m; var NM = M.inverse.transpose;
                bool flip = Vector3.Dot(Vector3.Cross(M.GetColumn(0), M.GetColumn(1)), M.GetColumn(2)) < 0f;
                var tan = M.MultiplyVector(Vector3.right).normalized; var tan4 = new Vector4(tan.x, tan.y, tan.z, 1f);
                for (int i = 0; i < ip.count; i++)
                {
                    int q = i < p.v.Length ? i : 0;
                    v[ip.at + i] = M.MultiplyPoint3x4(p.v[q]); n[ip.at + i] = NM.MultiplyVector(p.n[q]).normalized; t[ip.at + i] = tan4; c[ip.at + i] = p.c[q];
                }
                for (int i = 0; i + 2 < p.t.Length; i += 3)
                {
                    tri.Add(ip.at + p.t[i]);
                    if (flip) { tri.Add(ip.at + p.t[i + 2]); tri.Add(ip.at + p.t[i + 1]); } else { tri.Add(ip.at + p.t[i + 1]); tri.Add(ip.at + p.t[i + 2]); }
                }
            }
            foreach (var ad in adds)
            {
                var p = farParts[ad.kind]; var M = toFar * ad.m; var NM = M.inverse.transpose; int b0 = v.Count;
                bool flip = Vector3.Dot(Vector3.Cross(M.GetColumn(0), M.GetColumn(1)), M.GetColumn(2)) < 0f;      // a mirrored plant: its triangles turn round
                var tan = M.MultiplyVector(Vector3.right).normalized; var tan4 = new Vector4(tan.x, tan.y, tan.z, 1f);
                for (int i = 0; i < p.v.Length; i++) { v.Add(M.MultiplyPoint3x4(p.v[i])); n.Add(NM.MultiplyVector(p.n[i]).normalized); t.Add(tan4); c.Add(p.c[i]); }
                for (int i = 0; i + 2 < p.t.Length; i += 3)
                {
                    tri.Add(b0 + p.t[i]);
                    if (flip) { tri.Add(b0 + p.t[i + 2]); tri.Add(b0 + p.t[i + 1]); } else { tri.Add(b0 + p.t[i + 1]); tri.Add(b0 + p.t[i + 2]); }
                }
            }
            if ((v.Count == 0 || tri.Count == 0) && !RimTook(cell)) return;                               // round 5d: an empty far mesh, no old crown (OzL16Outskirts.RimFix.cs)
            // a cell that holds plants of the map itself keeps its mesh readable: the felling folds a felled tree's run in it
            bool mapPlants = false;
            foreach (Transform ch in cell) if (ch != farT && ch.name != "OUTER_TREES" && Outside(ch.position) <= 0f) { mapPlants = true; break; }
            var mesh = new Mesh { name = src.name + " (round 5b)", indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTangents(t); mesh.SetColors(c);
            mesh.SetTriangles(tri, 0); mesh.RecalculateBounds(); mesh.UploadMeshData(!mapPlants);
            farJobs.Add(new FarJob { cell = cell, slot = s, mf = mf, src = src, mesh = mesh, rim = rim, groves = grove, map = map });
            FarLookTrisBefore += s != null && s.newFar ? Tris(s.newFar) : before;                           // what round 5 draws in this cell
            FarLookTrisAfter += tri.Count / 3;
        }

        /// <summary>BuildSliced, just before ApplyTrees: the grove cells take their new far mesh in place of round 5's (ApplyTrees puts it
        /// in, with the land), the other cells now. Everything changes in the one step in which the land appears.</summary>
        void FarLookShow()
        {
            foreach (var j in farJobs)
            {
                if (!j.mf || !j.mesh) continue;
                if (j.slot == null && j.mf.sharedMesh != j.src)
                {   // the cell's far mesh changed after its new one was made (a map tree felled in between: the felling's own copy): left as it is
                    FarLookRefused++; if (FarLookWhy.Length < 600) FarLookWhy += (FarLookWhy.Length > 0 ? "; " : "") + j.cell.name + ": its far mesh changed while the land was made";
                    Destroy(j.mesh); j.mesh = null; continue;
                }
                if (j.slot != null) { if (j.slot.newFar && j.slot.newFar != j.mesh) Destroy(j.slot.newFar); j.slot.newFar = j.mesh; }
                else j.mf.sharedMesh = j.mesh;
                FarLookCells++; FarLookRim += j.rim; FarLookGroves += j.groves; FarLookMap += j.map;
            }
            if (TreesOn && FarLookOn)
                Debug.Log($"[OzL16Outskirts] round 5b: {FarLookRim + FarLookGroves + FarLookMap} trees on their far tree shape ({FarLookRim} of the rim ring, {FarLookGroves} of the groves, {FarLookMap} of the map's own, map trees switch {(FarLookMapTrees ? "on" : "off")}) in {FarLookCells} cells, {FarLookKinds} kinds; far triangles of those cells {FarLookTrisAfter} (round 5: {FarLookTrisBefore}); cells left as they were {FarLookRefused}{(FarLookWhy.Length > 0 ? " (" + FarLookWhy + ")" : "")}");
        }

        /// <summary>A run whose points all stand on its first one: a felled map tree (OzTreeFall folded it).</summary>
        static bool Folded(List<Vector3> pts, int at, int count)
        {
            var p = pts[at];
            for (int i = at + 1; i < at + count; i++) if (pts[i] != p) return false;
            return true;
        }

        static string FarLookStatus() => !TreesOn || !FarLookOn ? ", round 5b off" :
            $", round 5b: far tree shapes {FarLookRim + FarLookGroves + FarLookMap} (rim {FarLookRim}, groves {FarLookGroves}, map {FarLookMap}, map trees {(FarLookMapTrees ? "on" : "off")}) in {FarLookCells} cells, far tris {FarLookTrisAfter}/{FarLookTrisBefore}, refused {FarLookRefused}";

        /// <summary>Play check: every tree of the tree kinds outside the map (and, with FarLookMapTrees, the map's own) is on its new far
        /// shape (or its cell is named), the map's own trees are as they were when the switch is off, the far meshes hold what the
        /// counters say, and the far triangles of the changed cells are not more than round 5's.</summary>
        public static string FarLookCheck()
        {
            var o = I; if (!o) return "OzL16Outskirts: not on this level";
            if (!o.built) return "OzL16Outskirts: the land is not built yet";
            if (!TreesOn || !FarLookOn) return "OzL16Outskirts far look: OFF (round " + (TreesOn ? "5" : "4") + ")" + (FarLookFrom.Length > 0 ? ", " + FarLookFrom : "");
            var sb = new StringBuilder(); int fail = 0, left = 0, leftMap = 0, mapAsIs = 0, wrongMesh = 0, cellsOut = 0; long farTris = 0;
            var jobCells = new HashSet<Transform>(o.farAsIs);
            foreach (var j in o.farJobs)
            {   // the felling's own copy of a cell's far mesh (its first felled tree) is that mesh too; a job left out at the show: as it was
                if (!j.mesh) continue;
                jobCells.Add(j.cell);
                var now = j.mf ? j.mf.sharedMesh : null;
                if (now != j.mesh && !(now && j.mesh && now.name.StartsWith(j.mesh.name, System.StringComparison.Ordinal))) wrongMesh++;
            }
            var root = GameObject.Find(FloraRoot);
            var perKind = new int[FarKinds.Length]; var perMap = new int[FarKinds.Length];
            if (root)
                foreach (Transform cell in root.transform)
                {
                    var farT = cell.Find("FAR"); var fr = farT ? farT.GetComponent<Renderer>() : null; if (!fr || !fr.gameObject.activeInHierarchy) continue;
                    bool outsidePlants = false; var hold = cell.Find("OUTER_TREES");
                    foreach (var pmf in cell.GetComponentsInChildren<MeshFilter>(false))
                    {   // the cell's plants shown now (a map tree may stand under the felling's own holder), not its far mesh nor round 5's trees
                        var ch = pmf.transform; if (ch == farT || (hold && ch.IsChildOf(hold)) || !ch.GetComponent<Renderer>()) continue;
                        bool outer = o.Outside(ch.position) > 0f; if (outer) outsidePlants = true;
                        int k = FarKindOf(pmf.sharedMesh); if (k < 0) continue;
                        if (!outer && !FarLookMapTrees) { mapAsIs++; continue; }                                 // the map's own tree: as it was (switch off)
                        if (jobCells.Contains(cell)) { if (outer) perKind[k]++; else perMap[k]++; }
                        else if (outer) left++; else leftMap++;
                    }
                    if (hold) outsidePlants = true;
                    if (outsidePlants) { cellsOut++; var mf = farT.GetComponent<MeshFilter>(); farTris += mf ? Tris(mf.sharedMesh) : 0; }
                }
            foreach (var s in o.slots)                                                                       // round 5's grove trees in a drawn cell without its new far mesh
                if (!s.refused && !s.empty && s.trees.Count > 0 && !jobCells.Contains(s.cell)) left += s.trees.Count;
            if (wrongMesh > 0) fail++;
            if ((left > 0 || leftMap > 0) && FarLookRefused == 0) fail++;                                    // only a refused cell may keep old far trees
            if (FarLookTrisAfter > FarLookTrisBefore) fail++;
            if ((FarLookRim + FarLookGroves == 0 && !RimFarNone()) || (FarLookMapTrees ? FarLookMap == 0 : FarLookMap != 0)) fail++;   // round 5d: none (OzL16Outskirts.RimFix.cs)
            sb.Append($"OzL16Outskirts far look: {(fail == 0 ? "PASS" : "FAIL")} | {FarLookRim + FarLookGroves + FarLookMap} trees on their far tree shape (rim {FarLookRim}, groves {FarLookGroves}, map {FarLookMap}) in {FarLookCells} cells");
            sb.Append($" | map trees switch {(FarLookMapTrees ? "on" : $"off: the map's own trees of the kinds as they were {mapAsIs}")}");
            sb.Append($" | trees of the kinds left on the old far shape: outside the map {left}, in the map {leftMap}{(FarLookRefused > 0 ? $" (cells refused {FarLookRefused}: {FarLookWhy})" : "")}");
            sb.Append($" | far meshes not the new one {wrongMesh}");
            sb.Append($" | far triangles of the changed cells {FarLookTrisAfter} (round 5: {FarLookTrisBefore}){(FarLookTrisAfter <= FarLookTrisBefore ? " ok" : " MORE")}; all cells with plants outside the map drawn: {cellsOut}, their far triangles {farTris}");
            for (int k = 0; k < FarKinds.Length; k++) sb.Append($" | {FarKinds[k].Substring(3)}: {perKind[k]} rim trees, {perMap[k]} map trees, shape {(o.farParts != null ? o.farParts[k].t.Length / 3 : 0)} tris");
            return sb.ToString();
        }

        /// <summary>Play helper for the counts: the plant cells' far meshes the game camera's view meets now, by where they stand (in the
        /// map, in the rim ring or its cells, beyond), their triangles; and whether the wide overview holds the plants far.</summary>
        public static string FarViewCount()
        {
            var o = I; var cam = o && o.rts ? o.rts.GetComponentInChildren<Camera>() : Camera.main; if (!cam) return "no camera";
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            var root = GameObject.Find(FloraRoot); if (!root) return "no " + FloraRoot;
            int[] cells = new int[3]; long[] tris = new long[3];
            bool wide = o && o.rts && o.rts.height > OzL16DrawAhead.FarAbove + OzL16DrawAhead.FarBand;
            foreach (Transform cell in root.transform)
            {
                var farT = cell.Find("FAR"); var fr = farT ? farT.GetComponent<Renderer>() : null;
                if (!fr || !fr.enabled || !fr.gameObject.activeInHierarchy || !GeometryUtility.TestPlanesAABB(planes, fr.bounds)) continue;
                var b = fr.bounds; float outer = o ? o.Outside(b.center) : 0f;
                int ring = outer <= 0f ? 0 : outer <= OzL16FlyoverFlora.GroundMargin + 16f ? 1 : 2;
                var mf = farT.GetComponent<MeshFilter>(); cells[ring]++; tris[ring] += mf ? Tris(mf.sharedMesh) : 0;
            }
            return $"far meshes in view: map {cells[0]} ({tris[0]} tris), rim cells {cells[1]} ({tris[1]} tris), beyond {cells[2]} ({tris[2]} tris), all {cells[0] + cells[1] + cells[2]} ({tris[0] + tris[1] + tris[2]} tris) | camera {(o && o.rts ? o.rts.height : cam.transform.position.y):0} m, wide overview {(wide ? "on (plants far)" : "OFF (near plants in view: count not valid)")}";
        }
    }
}
