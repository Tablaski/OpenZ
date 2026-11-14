// OpenZ (GPL-3.0)
// Beyond the rim's edge (GroundMargin, 24 m outside the map) the level drew its own far jungle: trees half as tall again as the map's
// own, at the widest zoom as their far, thinned leaf triangles, some with no ground under them. Now:
//  - every plant whose foot stands beyond the rim's edge is not drawn; in a cell across the edge only those (also inside the cell's
//    joined far mesh: their runs, OzL16Outskirts.FarRuns.cs), the rim's own plants stay;
//  - the cells round 4 kept as groves hold the playable map's own trees instead: its tree species, each tree with the turn and size of
//    one of the map's own trees of its kind (only turned about the upright), standing on the land's own ground (the foot at most FootMax
//    m under it, never above it at the trunk's edge), in a grove round a point of the cell, clear of the pools, the level's landmarks
//    and the rocks at the flume ends;
//  - they are ordinary plants of their cell: near, the map's own near shape of the kind (its simpler one, as most trees on the map);
//    far (the wide overview, and outside the camera's ground), the far shape of the kind's full tree as the map's own trees show it,
//    joined into the cell's far mesh. So the level's distance rule (OzL16DrawAhead), the far shadows, the wind, the start flyover's
//    low look and the endings treat them exactly as the map's own trees;
//  - no fps reduction: in every cell they cost at most TreeShare of what the cell's hidden plants cost, near and far (triangles), with
//    the same one far mesh per cell and fewer near objects; a cell left with nothing is not drawn at all.
// Drawn only: no rule reads any of it. Play check: return OpenZ.OzL16Outskirts.TreeCheck();
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public sealed partial class OzL16Outskirts
    {
        /// <summary>Round 5 (default): the plants beyond the rim's edge go, the map's own trees stand in the grove cells. Off = round 4's
        /// plants there (for a before / after count); set before the level starts.</summary>
        public static bool TreesOn = true;
        /// <summary>A cell's trees cost at most this share of its hidden plants (triangles, near and far).</summary>
        public static float TreeShare = 0.85f;
        /// <summary>Where trees stand: at least this far beyond the rim's edge (m), this far apart (m), one per this much of the cell's
        /// land beyond the edge at the most (m2), within this reach of the grove's point (m), and how many places a cell tries.</summary>
        public static float TreeFrom = 3f, TreeSpacing = 4.2f, TreeArea = 70f, GroveReach = 11f;
        public static int GroveTries = 28;
        /// <summary>The foot: sunk this much under the land at the tree's middle, at most FootMax (m) under it, never above the land
        /// anywhere round the trunk (FootRing m from the middle); this much over a pool's water (m); clear of a landmark part by this much
        /// beyond its own size and of a flume end's rocks by this much (m).</summary>
        public static float FootSink = 0.06f, FootMax = 0.2f, FootRing = 0.5f, PoolClear = 0.25f, MarkClear = 4f, RockClear = 12f;
        /// <summary>The map's tree species (their full shapes; each also has its simpler near shape, name + "_lite").</summary>
        static readonly string[] TreeKinds = { "FL_TreeCanopyA", "FL_TreeCanopyB", "FL_TreeCanopyC", "FL_TreeButtressA", "FL_TreeKapokA", "FL_TreeKapokB", "FL_TreeVineA" };
        /// <summary>Counters: trees placed, cells holding them, cells left with nothing (not drawn), plants not drawn, cells whose far mesh
        /// lost its runs beyond the edge, cells left as round 4 had them (a run did not check out), species usable, slots.</summary>
        public static int TreesPlaced, TreeCells, CellsEmptied, PlantsHidden, RunsCut, RunsRefused, KindsUsable, Slots;
        /// <summary>Triangles of the hidden plants (near shapes, far runs) and of the trees that stand there now (near, far).</summary>
        public static long NearSaved, NearAdded, FarSaved, FarAdded;
        /// <summary>The deepest foot under the land (m), the most a foot stands over the land round its trunk (m, 0 or less), the largest
        /// share of a cell's budget used.</summary>
        public static float FootWorst, RingWorst, ShareWorst;
        public static string TreeWhy = "";

        sealed class Kind
        {
            public string name, why; public Mesh near; public Material[] mats; public int nearTris, farTris, weight;
            public readonly List<Quaternion> rots = new List<Quaternion>();
            public readonly List<Vector3> scales = new List<Vector3>();
            public float hMin = float.MaxValue, hMax = 0f;                       // the drawn heights of the map's own trees of this kind (m)
            public Vector3[] fv, fn; public Vector4[] ft; public Color32[] fc; public int[] fi;
            public bool Ok => near && mats != null && fv != null && rots.Count > 0;
        }

        sealed class Grove
        {
            public Transform cell; public LODGroup lg; public Renderer farR; public MeshFilter farMf; public int cx, cz;
            public List<Renderer> hidden, kept; public long nearBudget, farBudget, nearUsed, farUsed;
            public bool refused, empty; public Mesh newFar; public GameObject holder;
            public readonly List<Renderer> trees = new List<Renderer>();
        }

        readonly List<Kind> kinds = new List<Kind>();
        readonly List<Grove> slots = new List<Grove>();
        readonly List<Vector3> marks = new List<Vector3>();                     // clearances: x, z, radius
        readonly Dictionary<long, List<Vector2>> placed = new Dictionary<long, List<Vector2>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTreeStatics()
        {   // every Play session starts with round 5 on and the counters at 0 (the switch is read when the level starts)
            TreesOn = true; TreesPlaced = TreeCells = CellsEmptied = PlantsHidden = RunsCut = RunsRefused = KindsUsable = Slots = 0;
            NearSaved = NearAdded = FarSaved = FarAdded = 0; FootWorst = ShareWorst = 0f; RingWorst = -1f; TreeWhy = "";
        }

        static int H3(int i, int j, int s, int mod = 100)
        {
            unchecked { uint h = (uint)(i * 73856093) ^ (uint)(j * 19349663) ^ (uint)(s * 83492791); h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15; return (int)(h % (uint)Mathf.Max(1, mod)); }
        }

        static long Tris(Mesh m)
        {
            long t = 0; if (!m) return 0;
            for (int i = 0; i < m.subMeshCount; i++) if (m.GetTopology(i) == MeshTopology.Triangles) t += m.GetIndexCount(i) / 3;
            return t;
        }

        /// <summary>How far (x, z) stands beyond the plateau (the map and its rim): 0 on it.</summary>
        float Beyond(float x, float z)
        {
            float dx = Mathf.Max(Mathf.Max(px0 - x, 0f), x - px1), dz = Mathf.Max(Mathf.Max(pz0 - z, 0f), z - pz1);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>The land's drawn height at (x, z): on the very triangles BuildSliced lays ((a, d, c) and (a, c, b) of each grid cell).</summary>
        float Gen(float x, float z)
        {
            if (gh == null) return LandY;
            float fi = (x - gx0) / Cell, fj = (z - gz0) / Cell;
            if (fi < 0f || fj < 0f || fi > gnx - 1 || fj > gnz - 1) return LandY;
            int i = Mathf.Min((int)fi, gnx - 2), j = Mathf.Min((int)fj, gnz - 2); float u = fi - i, w = fj - j;
            float ha = gh[j * gnx + i], hb = gh[j * gnx + i + 1], hc = gh[(j + 1) * gnx + i + 1], hd = gh[(j + 1) * gnx + i];
            return u >= w ? ha + u * (hb - ha) + w * (hc - hb) : ha + w * (hd - ha) + u * (hc - hd);
        }

        static Bounds WorldBox(Bounds local, Matrix4x4 m)
        {
            var b = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int k = 0; k < 8; k++)
                b.Encapsulate(m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f))));
            return b;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = FindDeep(c, name); if (f) return f; }
            return null;
        }

        // ------------------------------------------------------------------ the cells (ChooseCells)
        /// <summary>ChooseCells, round 5: a cell round 4 keeps whose plants stand partly or wholly beyond the rim's edge becomes a place
        /// for the map's own trees: those plants go when the land is shown, their triangles are the cell's budget. False: the switch is
        /// off, or nothing of the cell stands beyond the edge (round 4's own rules then hold for it).</summary>
        bool Slot(Transform cell, Renderer[] rs)
        {
            if (!TreesOn) return false;
            float m = OzL16FlyoverFlora.GroundMargin; Renderer far = null;
            var hid = new List<Renderer>(); var kept = new List<Renderer>();
            foreach (var r in rs)
            {
                if (r.name == "FAR") { far = r; continue; }
                if (Outside(r.transform.position) > m + 0.01f) hid.Add(r); else kept.Add(r);   // the plant's own foot
            }
            if (hid.Count == 0) return false;
            var s = new Grove { cell = cell, lg = cell.GetComponent<LODGroup>(), farR = far, farMf = far ? far.GetComponent<MeshFilter>() : null, hidden = hid, kept = kept };
            if (!Square(cell.name, out s.cx, out s.cz)) { s.cx = Mathf.FloorToInt(hid[0].transform.position.x / 32f); s.cz = Mathf.FloorToInt(hid[0].transform.position.z / 32f); }
            foreach (var r in hid) { var mf = r.GetComponent<MeshFilter>(); if (mf) s.nearBudget += Tris(mf.sharedMesh); }
            foreach (var r in kept)
            {   // round 4's rule for the rim's own plants: the land rises under one whose drawing reaches out beyond the edge
                var pb = r.bounds; if (Outside(pb.center) <= m + 0.5f) continue;
                AddRise(pb.center.x, pb.center.z, pb.min.y - 0.12f, Mathf.Clamp(Mathf.Max(pb.extents.x, pb.extents.z) * 0.3f, 1.2f, 3f));
            }
            slots.Add(s); Slots = slots.Count;
            return true;
        }

        /// <summary>LandmarkRises: a landmark part beyond the rim's edge keeps the trees off (its own size and MarkClear).</summary>
        void Mark(Bounds b) { marks.Add(new Vector3(b.center.x, b.center.z, Mathf.Sqrt(b.extents.x * b.extents.x + b.extents.z * b.extents.z) + MarkClear)); }

        // ------------------------------------------------------------------ the map's own trees
        void FindKinds()
        {
            kinds.Clear(); KindsUsable = 0;
            var root = GameObject.Find(FloraRoot); if (!root) { TreeWhy = "no " + FloraRoot; return; }
            var byMesh = new Dictionary<string, Kind>();
            foreach (var n in TreeKinds) { var k = new Kind { name = n }; kinds.Add(k); byMesh[n] = k; byMesh[n + "_lite"] = k; }
            var wind = OzFloraWind.I;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh; if (!mesh || !byMesh.TryGetValue(mesh.name, out var k)) continue;
                var t = mf.transform; if (Outside(t.position) > 0f) continue;                   // the playable map's own trees only
                var r = mf.GetComponent<MeshRenderer>(); if (!r) continue;
                k.rots.Add(t.rotation); k.scales.Add(t.lossyScale); k.weight++;
                float h = WorldBox(mesh.bounds, t.localToWorldMatrix).size.y;                  // the shape's own height (the wind widens the drawing box)
                k.hMin = Mathf.Min(k.hMin, h); k.hMax = Mathf.Max(k.hMax, h);
                if (!k.near && mesh.name.EndsWith("_lite", System.StringComparison.Ordinal))
                {
                    k.near = mesh; k.nearTris = (int)Tris(mesh);
                    k.mats = wind && wind.TryGet(r, out var p) && p.Original != null ? (Material[])p.Original.Clone() : r.sharedMaterials;   // its own materials (the wind's copies are hooked again)
                }
            }
            foreach (var k in kinds)
            {
                if (!k.near) k.why = "no near shape of it inside the map";
                else FarShape(k, root.transform);
                if (k.Ok) KindsUsable++; else TreeWhy += (TreeWhy.Length > 0 ? "; " : "") + k.name + " not used: " + k.why;
            }
        }

        /// <summary>The far shape of a kind's full tree, taken from the joined far mesh of a cell inside the map (its run there,
        /// OzL16Outskirts.FarRuns.cs), checked, into the plant's own space.</summary>
        void FarShape(Kind k, Transform root)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<Vector4>(); var c = new List<Color32>();
            foreach (var s in FarSources)
            {
                if (s.shape != k.name) continue;
                var cell = root.Find(s.cell); var farT = cell ? cell.Find("FAR") : null; var mf = farT ? farT.GetComponent<MeshFilter>() : null; var m = mf ? mf.sharedMesh : null;
                if (!m || !m.isReadable || m.vertexCount != s.points || s.start < 0 || s.start + s.count > m.vertexCount) { k.why = s.cell + ": not the far mesh the runs were made from"; continue; }
                var plant = FindDeep(cell, s.plant); var pmf = plant ? plant.GetComponent<MeshFilter>() : null;
                if (!pmf || !pmf.sharedMesh) { k.why = s.plant + " is not in " + s.cell; continue; }
                var box = WorldBox(pmf.sharedMesh.bounds, plant.localToWorldMatrix); box.Expand(1.5f);
                m.GetVertices(v); bool inside = true;
                for (int i = s.start; i < s.start + s.count && inside; i++) inside = box.Contains(farT.TransformPoint(v[i]));
                if (!inside) { k.why = s.plant + ": its run is not its own points"; continue; }
                var idx = m.GetIndices(0); var tri = new List<int>(); bool cross = false;
                for (int i = 0; i + 2 < idx.Length; i += 3)
                {
                    int a = idx[i] - s.start, b = idx[i + 1] - s.start, d = idx[i + 2] - s.start;
                    bool ia = (uint)a < (uint)s.count, ib = (uint)b < (uint)s.count, id = (uint)d < (uint)s.count;
                    if (ia && ib && id) { tri.Add(a); tri.Add(b); tri.Add(d); } else if (ia || ib || id) cross = true;
                }
                if (cross || tri.Count == 0) { k.why = s.plant + ": its run's triangles are not its own"; continue; }
                m.GetNormals(n); m.GetTangents(t); m.GetColors(c);
                var toLocal = plant.worldToLocalMatrix * farT.localToWorldMatrix; var nm = toLocal.inverse.transpose;
                k.fv = new Vector3[s.count]; k.fn = new Vector3[s.count]; k.ft = t.Count == m.vertexCount ? new Vector4[s.count] : null; k.fc = c.Count == m.vertexCount ? new Color32[s.count] : null;
                for (int i = 0; i < s.count; i++)
                {
                    k.fv[i] = toLocal.MultiplyPoint3x4(v[s.start + i]);
                    k.fn[i] = n.Count == m.vertexCount ? nm.MultiplyVector(n[s.start + i]).normalized : Vector3.up;
                    if (k.ft != null) { var q = t[s.start + i]; var x = toLocal.MultiplyVector(new Vector3(q.x, q.y, q.z)).normalized; k.ft[i] = new Vector4(x.x, x.y, x.z, q.w); }
                    if (k.fc != null) k.fc[i] = c[s.start + i];
                }
                if (Vector3.Dot(Vector3.Cross(toLocal.GetColumn(0), toLocal.GetColumn(1)), toLocal.GetColumn(2)) < 0f)
                    for (int i = 0; i + 2 < tri.Count; i += 3) { int sw = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = sw; }   // a mirrored plant: its own space turns its triangles round
                k.fi = tri.ToArray(); k.farTris = tri.Count / 3;
                // the far shape and the near shape must stand in the same frame (the plant kit's shapes share it): their boxes agree
                var fb = new Bounds(k.fv[0], Vector3.zero); foreach (var p in k.fv) fb.Encapsulate(p);
                var nb = k.near.bounds; bool same = true;
                for (int ax = 0; ax < 3 && same; ax++)
                {
                    float fs = fb.size[ax], ns = nb.size[ax];
                    same = fs > 0.5f * ns && fs < 1.5f * ns + 0.2f && Mathf.Abs(fb.max[ax] - nb.max[ax]) <= 0.3f * Mathf.Max(ns, 0.5f) && Mathf.Abs(fb.min[ax] - nb.min[ax]) <= 0.3f * Mathf.Max(ns, 0.5f);
                }
                if (!same) { k.why = s.plant + ": its far shape does not stand where the near shape does"; k.fv = null; continue; }
                k.why = null;
                return;
            }
        }

        Kind Pick(int h100, int wsum)
        {
            float pick = h100 * wsum / 100f, acc = 0f; Kind last = null;
            foreach (var k in kinds) { if (!k.Ok) continue; acc += k.weight; last = k; if (pick < acc) return k; }
            return last;
        }

        bool Clear(Vector2 p)
        {
            foreach (var c in marks) if ((p.x - c.x) * (p.x - c.x) + (p.y - c.y) * (p.y - c.y) < c.z * c.z) return false;
            foreach (var f in flumeEnds) { var r = f.at + f.o * 2f; if ((p.x - r.x) * (p.x - r.x) + (p.y - r.z) * (p.y - r.z) < RockClear * RockClear) return false; }
            int bx = Mathf.FloorToInt(p.x / 8f), bz = Mathf.FloorToInt(p.y / 8f);
            for (int a = bx - 1; a <= bx + 1; a++)
                for (int b = bz - 1; b <= bz + 1; b++)
                    if (placed.TryGetValue(Key(a, b), out var l)) foreach (var q in l) if ((q - p).sqrMagnitude < TreeSpacing * TreeSpacing) return false;
            return true;
        }

        /// <summary>The foot on the land: false in a pool or on a slope too steep for the trunk to stand on it with its foot at most
        /// FootMax under the land. y = the tree's own point (its foot), sunk = how far under the land's middle, gap = how far over the
        /// land's lowest point under the trunk (never above 0; Under).</summary>
        bool Foot(Vector2 p, out float y, out float sunk, out float gap)
        {
            float h = Gen(p.x, p.y), lo = Under(p.x, p.y, 16); y = sunk = gap = 0f;
            if (h < waterY + PoolClear || lo < waterY + 0.1f) return false;
            y = Mathf.Min(h - FootSink, lo - 0.02f); sunk = h - y; gap = y - lo;
            return sunk <= FootMax;
        }

        /// <summary>The land's lowest point under a trunk standing at (x, z): its middle, n points on its ring (FootRing m out) and any
        /// point of the land's grid inside the ring (a pit between the ring's points).</summary>
        float Under(float x, float z, int n)
        {
            float lo = Gen(x, z);
            for (int k = 0; k < n; k++) { float a = k * 2f * Mathf.PI / n; lo = Mathf.Min(lo, Gen(x + Mathf.Cos(a) * FootRing, z + Mathf.Sin(a) * FootRing)); }
            if (gh == null) return lo;
            int i0 = Mathf.FloorToInt((x - gx0) / Cell), j0 = Mathf.FloorToInt((z - gz0) / Cell);
            for (int i = i0; i <= i0 + 1; i++)
                for (int j = j0; j <= j0 + 1; j++)
                {
                    if (i < 0 || j < 0 || i >= gnx || j >= gnz) continue;
                    float dx = gx0 + i * Cell - x, dz = gz0 + j * Cell - z;
                    if (dx * dx + dz * dz <= FootRing * FootRing) lo = Mathf.Min(lo, gh[j * gnx + i]);
                }
            return lo;
        }

        // ------------------------------------------------------------------ build (a step per cell: BuildSliced)
        IEnumerable<bool> GrowTrees()
        {
            TreesPlaced = TreeCells = CellsEmptied = PlantsHidden = RunsCut = RunsRefused = 0; NearSaved = NearAdded = FarSaved = FarAdded = 0;
            FootWorst = 0f; RingWorst = -1f; ShareWorst = 0f; placed.Clear(); TreeWhy = "";
            if (!TreesOn || slots.Count == 0) yield break;
            FindKinds();
            yield return true;
            foreach (var s in slots) { Grow(s); yield return true; }
        }

        void Grow(Grove s)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<Vector4>(); var c = new List<Color32>(); var tri = new List<int>();
            var src = s.farMf ? s.farMf.sharedMesh : null;
            if (s.kept.Count > 0) { if (!CutRuns(s, src, v, n, t, c, tri)) { s.refused = true; RunsRefused++; return; } }   // left as round 4 had it
            else s.farBudget = Tris(src);
            NearSaved += s.nearBudget; FarSaved += s.farBudget;
            // the grove: places round a point of the cell (or anywhere in it where the cell's land beyond the edge is a thin strip)
            float x0 = s.cx * 32f, z0 = s.cz * 32f; int room = 0;
            for (int i = 0; i < 16; i++) for (int j = 0; j < 16; j++) if (Beyond(x0 + 1f + 2f * i, z0 + 1f + 2f * j) >= TreeFrom) room++;
            int most = Mathf.FloorToInt(room * 4f / TreeArea), wsum = 0;
            foreach (var k in kinds) if (k.Ok) wsum += k.weight;
            float fb = TreeShare * s.farBudget, nb = TreeShare * s.nearBudget;
            var mid = new Vector2(x0 + 16f + (H3(s.cx, s.cz, 1) / 99f - 0.5f) * 18f, z0 + 16f + (H3(s.cx, s.cz, 2) / 99f - 0.5f) * 18f);
            bool grove = Beyond(mid.x, mid.y) >= TreeFrom;
            var trees = new List<(Kind k, Vector3 at, Quaternion rot, Vector3 scale)>();
            for (int k = 0; k < GroveTries && trees.Count < most && wsum > 0; k++)
            {
                Vector2 p;
                if (grove && k < GroveTries * 2 / 3)
                {
                    float a = H3(s.cx, s.cz, 10 + k) / 100f * 2f * Mathf.PI, r = GroveReach * Mathf.Sqrt(H3(s.cx, s.cz, 40 + k) / 100f);
                    p = mid + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                }
                else p = new Vector2(x0 + 1f + H3(s.cx, s.cz, 70 + k) / 99f * 30f, z0 + 1f + H3(s.cx, s.cz, 100 + k) / 99f * 30f);
                if (p.x < x0 + 1f || p.x > x0 + 31f || p.y < z0 + 1f || p.y > z0 + 31f) continue;      // inside the cell's own square (the wind and the far mesh go by it)
                float d = Beyond(p.x, p.y); if (d < TreeFrom || d > Reach - 20f) continue;
                if (!Clear(p)) continue;
                if (!Foot(p, out float y, out float sunk, out float gap)) continue;
                var kd = Pick(H3(s.cx, s.cz, 130 + k), wsum); if (kd == null) break;
                if (s.farUsed + kd.farTris > fb || s.nearUsed + kd.nearTris > nb) continue;          // never more than TreeShare of what went
                int vi = H3(s.cx, s.cz, 160 + k, kd.rots.Count);
                trees.Add((kd, new Vector3(p.x, y, p.y), Quaternion.AngleAxis(H3(s.cx, s.cz, 190 + k) * 3.6f, Vector3.up) * kd.rots[vi], kd.scales[vi]));
                s.farUsed += kd.farTris; s.nearUsed += kd.nearTris;
                long key = Key(Mathf.FloorToInt(p.x / 8f), Mathf.FloorToInt(p.y / 8f));
                if (!placed.TryGetValue(key, out var l)) placed[key] = l = new List<Vector2>();
                l.Add(p);
                FootWorst = Mathf.Max(FootWorst, sunk); RingWorst = Mathf.Max(RingWorst, gap);
            }
            if (s.farBudget > 0) ShareWorst = Mathf.Max(ShareWorst, s.farUsed / (float)s.farBudget);
            if (s.nearBudget > 0) ShareWorst = Mathf.Max(ShareWorst, s.nearUsed / (float)s.nearBudget);
            // the cell's new far mesh: the rim's own plants' runs, then the trees' far shapes
            var farT = s.farR ? s.farR.transform : s.cell;
            foreach (var tr in trees)
            {
                var C = farT.worldToLocalMatrix * Matrix4x4.TRS(tr.at, tr.rot, tr.scale); var N = C.inverse.transpose; int b0 = v.Count, nv = tr.k.fv.Length;
                for (int i = 0; i < nv; i++)
                {
                    v.Add(C.MultiplyPoint3x4(tr.k.fv[i])); n.Add(N.MultiplyVector(tr.k.fn[i]).normalized);
                    if (tr.k.ft != null) { var q = tr.k.ft[i]; var x = C.MultiplyVector(new Vector3(q.x, q.y, q.z)).normalized; t.Add(new Vector4(x.x, x.y, x.z, q.w)); } else t.Add(new Vector4(1f, 0f, 0f, 1f));
                    c.Add(tr.k.fc != null ? tr.k.fc[i] : new Color32(255, 255, 255, 255));
                }
                bool flip = Vector3.Dot(Vector3.Cross(C.GetColumn(0), C.GetColumn(1)), C.GetColumn(2)) < 0f;   // a mirrored tree: its triangles turn round (the far mesh is drawn one-sided)
                for (int i = 0; i + 2 < tr.k.fi.Length; i += 3)
                {
                    tri.Add(b0 + tr.k.fi[i]);
                    if (flip) { tri.Add(b0 + tr.k.fi[i + 2]); tri.Add(b0 + tr.k.fi[i + 1]); } else { tri.Add(b0 + tr.k.fi[i + 1]); tri.Add(b0 + tr.k.fi[i + 2]); }
                }
            }
            if (v.Count == 0 || tri.Count == 0) { s.empty = true; return; }
            var mesh = new Mesh { name = (src ? src.name : "FAR_" + s.cell.name) + " (round 5)", indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };   // as the land's own meshes: unloaded with the level (a kept one would stay on the GPU after every restart)
            mesh.SetVertices(v); mesh.SetNormals(n); if (t.Count == v.Count) mesh.SetTangents(t); if (c.Count == v.Count) mesh.SetColors(c);
            mesh.SetTriangles(tri, 0); mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            s.newFar = mesh;
            // the trees themselves: plants of the cell, waiting (switched off) until the land is shown
            if (trees.Count == 0) return;
            s.holder = new GameObject("OUTER_TREES"); s.holder.SetActive(false); s.holder.layer = s.cell.gameObject.layer;
            s.holder.transform.SetParent(s.cell, false);
            var ls = s.holder.transform.lossyScale;
            foreach (var tr in trees)
            {
                var go = new GameObject("OUT_" + tr.k.name.Substring(3) + "_" + TreesPlaced); go.layer = s.holder.layer;
                go.transform.SetParent(s.holder.transform, false);
                go.transform.SetPositionAndRotation(tr.at, tr.rot);
                go.transform.localScale = new Vector3(tr.scale.x / ls.x, tr.scale.y / ls.y, tr.scale.z / ls.z);
                go.AddComponent<MeshFilter>().sharedMesh = tr.k.near;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = tr.k.mats;
                mr.shadowCastingMode = ShadowCastingMode.On; mr.receiveShadows = true;                     // as the map's own trees
                s.trees.Add(mr); TreesPlaced++; NearAdded += tr.k.nearTris; FarAdded += tr.k.farTris;
            }
        }

        /// <summary>A cell across the rim's edge: its far mesh without the runs of its plants beyond the edge (each checked: the far mesh
        /// is the one the runs were made from, every such plant is listed, its run lies on it, no triangle crosses a run's edge).</summary>
        bool CutRuns(Grove s, Mesh src, List<Vector3> v, List<Vector3> n, List<Vector4> t, List<Color32> c, List<int> tri)
        {
            int e = -1; for (int i = 0; i < MixedRuns.Length; i++) if (MixedRuns[i].cell == s.cell.name) { e = i; break; }
            if (e < 0 || !src || !src.isReadable || src.vertexCount != MixedRuns[e].points) return false;
            var row = MixedRuns[e]; if (row.names.Length != s.hidden.Count) return false;
            var drop = new bool[src.vertexCount]; var sv = new List<Vector3>(); src.GetVertices(sv);
            var farT = s.farR.transform;
            for (int i = 0; i < row.names.Length; i++)
            {
                Renderer r = null; foreach (var h in s.hidden) if (h.name == row.names[i]) { r = h; break; }
                var mf = r ? r.GetComponent<MeshFilter>() : null; if (!mf || !mf.sharedMesh) return false;
                var box = WorldBox(mf.sharedMesh.bounds, r.transform.localToWorldMatrix); box.Expand(1.5f);
                int a = row.starts[i], k = row.counts[i]; if (a < 0 || a + k > drop.Length) return false;
                for (int j = a; j < a + k; j++) { if (!box.Contains(farT.TransformPoint(sv[j]))) return false; drop[j] = true; }
            }
            var sn = new List<Vector3>(); var st = new List<Vector4>(); var sc = new List<Color32>(); src.GetNormals(sn); src.GetTangents(st); src.GetColors(sc);
            var map = new int[drop.Length];
            for (int i = 0; i < drop.Length; i++)
            {
                if (drop[i]) { map[i] = -1; continue; }
                map[i] = v.Count; v.Add(sv[i]);
                n.Add(sn.Count == drop.Length ? sn[i] : Vector3.up); t.Add(st.Count == drop.Length ? st[i] : new Vector4(1f, 0f, 0f, 1f)); c.Add(sc.Count == drop.Length ? sc[i] : new Color32(255, 255, 255, 255));
            }
            long cut = 0;
            for (int sm = 0; sm < src.subMeshCount; sm++)
            {
                var idx = src.GetIndices(sm);
                for (int i = 0; i + 2 < idx.Length; i += 3)
                {
                    int a = map[idx[i]], b = map[idx[i + 1]], d = map[idx[i + 2]];
                    if (a >= 0 && b >= 0 && d >= 0) { tri.Add(a); tri.Add(b); tri.Add(d); }
                    else if (a < 0 && b < 0 && d < 0) cut++;
                    else { v.Clear(); n.Clear(); t.Clear(); c.Clear(); tri.Clear(); return false; }        // a triangle across a run's edge: not this mesh
                }
            }
            s.farBudget = cut; RunsCut++;
            return true;
        }

        // ------------------------------------------------------------------ show (with the land), and the low look
        /// <summary>BuildSliced, when the land is shown: the plants beyond the edge go, the trees join their cells (their near shapes
        /// in the cell's near level, their far shapes in its far mesh).</summary>
        void ApplyTrees()
        {
            foreach (var s in slots)
            {
                if (s.refused) continue;
                var box = s.lg ? LodBox(s.lg) : default;                                                 // before anything changes: the box OzL16DrawAhead took for the cell
                foreach (var r in s.hidden) if (r) { r.gameObject.SetActive(false); PlantsHidden++; }
                if (s.empty) { Hide(s.cell.gameObject); CellsEmptied++; continue; }                     // nothing left in it: not drawn at all
                if (s.farMf && s.newFar) s.farMf.sharedMesh = s.newFar;
                if (s.lg)
                {
                    var lods = s.lg.GetLODs();
                    if (lods.Length > 0)
                    {
                        var near = new List<Renderer>(s.kept); near.AddRange(s.trees);
                        lods[0].renderers = near.ToArray(); s.lg.SetLODs(lods); HoldLod(s.lg, box);
                    }
                }
                if (s.trees.Count > 0) TreeCells++;
            }
            if (TreesOn && slots.Count > 0) Groves = TreeCells;                                         // the log's and the Play check's groves: cells holding the map's trees
            if (TreesOn && slots.Count > 0)
                Debug.Log($"[OzL16Outskirts] round 5: {TreesPlaced} of the map's own trees in {TreeCells} cells ({KindsUsable} kinds), {PlantsHidden} plants beyond the rim's edge not drawn, {CellsEmptied} cells left with nothing (not drawn), far meshes cut {RunsCut}, cells left as before {RunsRefused}; triangles near {NearAdded} for {NearSaved}, far {FarAdded} for {FarSaved}; feet at most {FootWorst:0.00} m under the land, never over it ({RingWorst:0.00}){(TreeWhy.Length > 0 ? "; " + TreeWhy : "")}");
        }

        /// <summary>The box OzL16DrawAhead reads for a cell (its Collect): every renderer of every level, as they stand.</summary>
        static Bounds LodBox(LODGroup lg)
        {
            bool any = false; var b = new Bounds(lg.transform.position, Vector3.one);
            foreach (var lod in lg.GetLODs())
                if (lod.renderers != null) foreach (var r in lod.renderers) { if (!r) continue; if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; } }
            return b;
        }

        /// <summary>A cell whose levels changed keeps the level it is held on: the low look's full plants, or the distance rule's choice
        /// (the engine may let a held level go when the levels change), made on the same box as the rule makes it (b: LodBox before the
        /// change; the rule does not look at the cell again until its own choice for it changes).</summary>
        void HoldLod(LODGroup lg, Bounds b)
        {
            if (OzL16FlyoverFlora.LowLookOn) { lg.ForceLOD(0); return; }
            var da = OzL16DrawAhead.I; var cam = rts ? rts.GetComponentInChildren<Camera>() : null; if (!cam) cam = Camera.main;
            if (!da || !OzL16DrawAhead.On || !cam) { lg.ForceLOD(-1); return; }
            int last = Mathf.Max(0, lg.lodCount - 1);
            if ((rts ? rts.height : cam.transform.position.y) > OzL16DrawAhead.FarAbove) { lg.ForceLOD(last); return; }   // the wide overview: plant cells far
            Vector3 cp = cam.transform.position; float xMin = cp.x, xMax = cp.x, zMin = cp.z, zMax = cp.z;
            for (int k = 0; k < 4; k++)
            {   // the ground the camera sees, widened by the margin (OzL16DrawAhead's own rule)
                var ray = cam.ViewportPointToRay(new Vector3(k % 2, k / 2, 0f));
                float tt = ray.direction.y < -0.02f ? Mathf.Min(900f, -ray.origin.y / ray.direction.y) : 900f;
                var p = ray.origin + ray.direction * tt;
                xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x); zMin = Mathf.Min(zMin, p.z); zMax = Mathf.Max(zMax, p.z);
            }
            float mg = OzL16DrawAhead.Margin; Vector3 mn = b.min, mx = b.max;
            bool near = mx.x >= xMin - mg && mn.x <= xMax + mg && mx.z >= zMin - mg && mn.z <= zMax + mg;
            lg.ForceLOD(near ? 0 : last);
        }

        /// <summary>A plant cell that is not drawn: its plants and its far mesh switched off, the cell itself stays on (the low look and the
        /// distance rule hold levels on every cell; on a switched-off cell the engine refuses that, with a warning per cell). Their objects
        /// go off, not only their renderers: the start flyover's low look hides the plants beyond the rim's edge by their renderers and
        /// switches exactly those renderers back on when it ends (OzL16FlyoverFlora.Set), and the land is mostly built while it is on.</summary>
        static void Hide(GameObject cell)
        {
            foreach (var r in cell.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = false;
                if (r.gameObject != cell) r.gameObject.SetActive(false);
            }
        }

        /// <summary>LateUpdate: the trees stand back with the land while the low look is on (the start flyover is exactly as before).</summary>
        void ShowTrees(bool on) { foreach (var s in slots) if (s.holder && !s.refused && !s.empty) s.holder.SetActive(on); }

        // ------------------------------------------------------------------ Play checks
        static string TreeStatus() => !TreesOn ? ", round 5 off" :
            $", round 5: trees {TreesPlaced} in {TreeCells} cells ({KindsUsable} kinds), plants not drawn {PlantsHidden}, cells emptied {CellsEmptied}, far cut {RunsCut} / refused {RunsRefused}, tris near {NearAdded}/{NearSaved} far {FarAdded}/{FarSaved}, feet {FootWorst:0.00} m ({RingWorst:0.00})";

        /// <summary>Play check: every tree on the land and at its kind's size on the map, every cell within its budget, the flume rocks.</summary>
        public static string TreeCheck()
        {
            var o = I; if (!o) return "OzL16Outskirts: not on this level";
            if (!o.built) return "OzL16Outskirts: the land is not built yet";
            var sb = new StringBuilder(); int fail = 0, trees = 0; float sunkMax = 0f, overMax = float.MinValue, sizeOff = 0f; int sizeBad = 0, budgetBad = 0;
            foreach (var s in o.slots)
            {
                if (s.refused) continue;
                if (s.farUsed > TreeShare * s.farBudget + 0.5f || s.nearUsed > TreeShare * s.nearBudget + 0.5f) budgetBad++;
                foreach (var r in s.trees)
                {
                    if (!r) continue; trees++;
                    var p = r.transform.position; float h = o.Gen(p.x, p.z), lo = o.Under(p.x, p.z, 64);     // a finer ring than the placing rule's
                    sunkMax = Mathf.Max(sunkMax, h - p.y); overMax = Mathf.Max(overMax, p.y - lo);
                    var mf = r.GetComponent<MeshFilter>(); Kind kd = null;
                    foreach (var k in o.kinds) if (mf && k.near == mf.sharedMesh) { kd = k; break; }
                    if (kd == null) { sizeBad++; continue; }
                    float ht = WorldBox(mf.sharedMesh.bounds, r.transform.localToWorldMatrix).size.y;
                    if (ht < kd.hMin * 0.96f || ht > kd.hMax * 1.04f) { sizeBad++; sizeOff = Mathf.Max(sizeOff, Mathf.Max(kd.hMin - ht, ht - kd.hMax)); }
                }
            }
            if (overMax == float.MinValue) overMax = -1f;
            bool feet = sunkMax <= FootMax + 0.001f && overMax <= 0.001f;
            if (!feet) fail++; if (sizeBad > 0) fail++; if (budgetBad > 0) fail++; if (TreesOn && trees == 0) fail++;
            sb.Append($"OzL16Outskirts trees: {(fail == 0 ? "PASS" : "FAIL")} | {trees} trees in {TreeCells} cells, {KindsUsable} kinds");
            sb.Append($" | feet: deepest {sunkMax:0.000} m under the land (at most {FootMax:0.00}), highest over the land round the trunk {overMax:0.000} m (at most 0) {(feet ? "ok" : "FAIL")}");
            sb.Append($" | sizes: {sizeBad} outside the map's own range of their kind{(sizeBad > 0 ? $" (by {sizeOff:0.00} m)" : "")}");
            sb.Append($" | budgets: {budgetBad} cells over {TreeShare:0.00} of their hidden plants; triangles near {NearAdded} for {NearSaved}, far {FarAdded} for {FarSaved}; worst share {ShareWorst:0.00}");
            sb.Append($" | plants not drawn {PlantsHidden}, cells emptied {CellsEmptied}, far meshes cut {RunsCut}, cells left as before {RunsRefused}");
            foreach (var k in o.kinds) sb.Append($" | {k.name.Substring(3)}: {(k.Ok ? $"{k.weight} on the map, {k.hMin:0.0}-{k.hMax:0.0} m, near {k.nearTris} far {k.farTris} tris" : "NOT USED: " + k.why)}");
            // the rocks at the flume ends: their tops against the flume beds' top
            var beds = GameObject.Find("TER_RimFallBeds_v5"); var br = beds ? beds.GetComponent<Renderer>() : null;
            for (int i = 0; i < o.flumeEnds.Count && i < KnownFlumes.Length; i++)
            {
                var rock = o.transform.Find("L16_Outskirts_Rock_" + (i * 3)); float top = float.MinValue;
                if (rock) foreach (var r in rock.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y);
                sb.Append($" | flume end {i + 1} at ({KnownFlumes[i].x:0},{KnownFlumes[i].y:0}): {(rock ? rock.GetComponentsInChildren<Renderer>().Length > 0 ? $"rock top {top:0.0} m" : "rock without a picture" : "NO ROCK")}{(br ? $", flume beds' top {br.bounds.max.y:0.0} m" : "")}");
            }
            return sb.ToString();
        }

        /// <summary>Play helper for the pictures: the normal RTS camera over a place (its limits hold): "flume_left", "flume_right",
        /// "west" (the map's own trees beside the outer ones), "north"; height in m (0 = 80).</summary>
        public static string Frame(string what, float height = 0f)
        {
            var o = I; var cam = o ? o.rts : null; if (!cam) return "no RTS camera";
            float h = height > 0f ? height : 80f; Vector3 f;
            switch (what)
            {
                case "flume_left": f = new Vector3(KnownFlumes[0].x, 0f, -10f); break;
                case "flume_right": f = new Vector3(KnownFlumes[1].x, 0f, -10f); break;
                case "west": f = new Vector3(0f, 0f, -128f); break;
                case "north": f = new Vector3(104f, 0f, -10f); break;
                default: return "Frame: flume_left, flume_right, west or north";
            }
            cam.focus = f; cam.SetHeightImmediate(h);
            return $"camera over ({cam.focus.x:0},{cam.focus.z:0}) at {cam.height:0} m";
        }
    }
}
