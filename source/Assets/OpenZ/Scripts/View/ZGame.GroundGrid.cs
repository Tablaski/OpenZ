using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.4 speed-up 1 (kit perf_ground_grid): GroundY reads a baked height grid instead of casting a physics ray on every call (about 500 call sites in the
    /// view: every unit, effect, decal, ring, shot end and cloth point). Where the grid answers it gives the ray's answer within GridTol (0.5 mm); everywhere
    /// else the ray answers exactly as before. View only: the rules never call GroundY, so no fight changes.
    ///
    /// Which cells answer (GroundGridCore.Bake, from the walk layer's own triangles: point samples miss posts and walls between them): one layer of ground
    /// covering the cell exactly once on top, anything else wholly under it, the bilinear of the 4 corners within GridTol of it everywhere (exact), one group of
    /// colliders. Groups: the terrain with its crater chunks is one (CraterField.GroundAdd treats them alike), every other walk collider (a bridge deck, a
    /// platform deck) is its own (OzBridgeWobble.DipAt reads the deck). The crater dent while it eases in and the plank bridge's dip are added on top of the grid
    /// exactly as the ray branch adds them.
    ///
    /// Lazy: a tile (16 x 16 cells, 2 m) is baked the first time GroundY asks inside it, at most GridBakeBudgetMs of the main thread a frame; past that the ray
    /// answers. Every place that changes the walk surface at run time tells the grid, and the cells there answer by the ray at once; the tiles are baked again
    /// only after a later frame and a physics step (a destroyed collider is gone, a moved one synced):
    ///  - craters (View/CraterField.GroundGrid.cs): held from the spawn until the crater is gone, over its bowl and every chunk triangle its cut replaces;
    ///  - bridges intact / damaged / destroyed / rebuilt (View/RulesView.cs SetBridge);
    ///  - the Unit editor's platforms dropped, destroyed or removed (View/OzUePlatformView.cs);
    ///  - the deformable ground made ready, at the start or when switched on in a match (all of it).
    /// A collider the bake cannot read (not a mesh or an upright box, a convex or unreadable mesh, a mirrored transform) keeps the ray under its bounds.
    /// Switch GroundGridOn (checks only; there is no Options row, the grid gives the ray's answer). Numbers: GridStats. Counted check: OpenZ/V1.4/Ground grid.
    /// </summary>
    public partial class ZGame
    {
        /// <summary>Checks only (grid against ray in the same frame). On by default: where the grid answers it is the ray's answer within GridTol.</summary>
        public static bool GroundGridOn = true;
        /// <summary>Cell side (m), decided by measuring every level's terrain collider off Unity (the kit's proof/offline_B_exact_cells.txt): at 0.125 m the grid answers
        /// 88-97 % of the cells on ground and road tiles, at 0.25 m 77-92 %.</summary>
        public const float GridCell = 0.125f;
        /// <summary>A cell answers by the grid only when the grid is within this of the ray everywhere in the cell (m).</summary>
        public const float GridTol = 0.0005f;
        /// <summary>Main-thread time a frame may spend baking tiles (ms); past it GroundY casts its ray as before.</summary>
        public static float GridBakeBudgetMs = 1f;
        /// <summary>Counters for the check and the perf line.</summary>
        public static readonly GroundGridStats GridStats = new GroundGridStats();
        /// <summary>Checks only: when set, every GroundY call's point is added (the ms check times them grid against ray).</summary>
        public static List<Vector2> GridRecord;

        const float GridInv = 1f / GridCell, GridMarginM = 2f;
        const int GridShift = GroundGridCore.Shift, GridMask = GroundGridCore.Mask, GridNC = GroundGridCore.NC;
        static readonly List<GroundGridMap> gridMaps = new List<GroundGridMap>();
        GroundGridMap gridMap;

        static int gridRayOnly;

        /// <summary>While the returned scope lives (a using block), GroundY casts its ray as before, grid or not. For the one place where view heights become
        /// a rule's input: level 7's dragons (DV163) plan their pass from the ground heights the view sees and hand it to the rules (ZDragonOrder, rounded
        /// to 1/64 m), so AmbientLife's frame keeps the ray on a map with dragons and those numbers stay bit for bit what they were.</summary>
        public static GroundRayScope GroundRayOnlyIf(bool on) => new GroundRayScope(on);

        public readonly struct GroundRayScope : System.IDisposable
        {
            readonly bool on;
            public GroundRayScope(bool on) { this.on = on; if (on) gridRayOnly++; }
            public void Dispose() { if (on) gridRayOnly--; }
        }

        /// <summary>GroundY's first line: true = answered by the grid (y set), false = the ray below answers, as before.</summary>
        bool GroundGridTry(float x, float z, out float y)
        {
            y = 0f;
            if (!GroundGridOn || walkMask == 0 || gridRayOnly > 0) return false;
            var rec = GridRecord; if (rec != null && rec.Count < 200000) rec.Add(new Vector2(x, z));
            var m = gridMap;
            if (m == null)
            {
                if (!Application.isPlaying || World == null || World.Map == null || CraterField.GridWaiting) return false;
                gridMaps.RemoveAll(o => !o.Alive);                                                   // the grid of a level left behind goes now (its tiles and mesh copies freed)
                m = gridMap = new GroundGridMap(this, walkMask, World.Map.width * 2f, World.Map.height * 2f);
                gridMaps.Add(m);
                CraterField.GridHoldAll();                                                           // craters made before the grid hold their ground too
                Debug.Log($"[OpenZ GroundGrid] {level}: {World.Map.width * 2f:0} x {World.Map.height * 2f:0} m, cells {GridCell:0.###} m, tiles of {GroundGridCore.N} cells, tolerance {GridTol * 1000f:0.#} mm, bake budget {GridBakeBudgetMs:0.#} ms a frame");
            }
            return m.Try(x, z, out y);
        }

        // ------------------------------------------------------------------------------------------------ hooks: where the walk surface changes
        /// <summary>The walk colliders under these roots were shown, hidden, made or are about to go: their cells answer by the ray until baked again.</summary>
        public static void GroundGridChanged(params GameObject[] roots)
        {
            if (gridMaps.Count == 0 || roots == null) return;
            bool any = false; var b = new Bounds();
            foreach (var r in roots) if (r && WalkBounds(r, ref b, ref any) == false) { GroundGridDirtyAll("a walk collider of unknown shape under " + r.name); return; }
            if (any) GroundGridDirty(b.min.x, b.min.z, b.max.x, b.max.z);
        }

        /// <summary>The walk surface in this rectangle (world x, z) changed.</summary>
        public static void GroundGridDirty(float x0, float z0, float x1, float z1)
        {
            for (int i = gridMaps.Count - 1; i >= 0; i--) { var m = gridMaps[i]; if (!m.Alive) { gridMaps.RemoveAt(i); continue; } m.Dirty(x0, z0, x1, z1); }
        }

        /// <summary>The walk surface may change anywhere in this rectangle until GroundGridRelease(key): the ray answers there until then.</summary>
        public static void GroundGridHold(object key, float x0, float z0, float x1, float z1)
        {
            for (int i = gridMaps.Count - 1; i >= 0; i--) { var m = gridMaps[i]; if (!m.Alive) { gridMaps.RemoveAt(i); continue; } m.Hold(key, x0, z0, x1, z1); }
        }

        public static void GroundGridRelease(object key)
        {
            for (int i = gridMaps.Count - 1; i >= 0; i--) { var m = gridMaps[i]; if (!m.Alive) { gridMaps.RemoveAt(i); continue; } m.Release(key); }
        }

        /// <summary>Everything again (the deformable ground's chunks replaced the terrain collider, a collider of unknown shape changed).</summary>
        public static void GroundGridDirtyAll(string why)
        {
            for (int i = gridMaps.Count - 1; i >= 0; i--)
            {
                var m = gridMaps[i]; if (!m.Alive) { gridMaps.RemoveAt(i); continue; }
                m.DirtyAll(); GridStats.DirtyAll++;
                Debug.Log("[OpenZ GroundGrid] all tiles again (" + why + ")");
            }
        }

        /// <summary>The world rectangle of the walk colliders under root, active or not (from their own shapes, which an inactive collider's bounds do not
        /// give). False = a walk collider whose shape it cannot read.</summary>
        static bool WalkBounds(GameObject root, ref Bounds b, ref bool any)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                if (!c || c.gameObject.layer != WalkLayer) continue;
                Bounds local;
                if (c is MeshCollider mc) { if (!mc.sharedMesh) continue; local = mc.sharedMesh.bounds; }
                else if (c is BoxCollider bc) local = new Bounds(bc.center, bc.size);
                else if (c.enabled && c.gameObject.activeInHierarchy) { Add(ref b, ref any, c.bounds); continue; }
                else return false;
                var m = c.transform.localToWorldMatrix; Vector3 e = local.extents, o = local.center;
                for (int k = 0; k < 8; k++)
                {
                    var p = m.MultiplyPoint3x4(o + new Vector3((k & 1) == 0 ? -e.x : e.x, (k & 2) == 0 ? -e.y : e.y, (k & 4) == 0 ? -e.z : e.z));
                    Add(ref b, ref any, new Bounds(p, Vector3.zero));
                }
            }
            return true;
        }

        static void Add(ref Bounds b, ref bool any, Bounds x) { if (!any) { b = x; any = true; } else b.Encapsulate(x); }

        // ------------------------------------------------------------------------------------------------ the grid of one level
        sealed class GridTile
        {
            public readonly float[] y = new float[GridNC * GridNC];
            public readonly byte[] g = new byte[GroundGridCore.N * GroundGridCore.N];
        }

        struct GridBox { public float x0, z0, x1, z1; public int frame; public float fixedTime; public bool all; }

        sealed class GridMesh
        {
            public Mesh mesh; public Matrix4x4 m; public bool ok;
            public Vector3[] w; public int[] t;                                                       // world vertices; front-facing triangles, 3 indices each
            public readonly Dictionary<int, List<int>> byTile = new Dictionary<int, List<int>>();     // tile -> starts into t
        }

        sealed class GroundGridMap
        {
            readonly ZGame game; readonly int mask;
            readonly float ox, oz; readonly int ncx, ncz, ntx, ntz;
            readonly GridTile[] tiles;
            readonly List<GridBox> pending = new List<GridBox>();
            readonly Dictionary<object, GridBox> holds = new Dictionary<object, GridBox>();
            readonly List<Collider> reps = new List<Collider> { null };                              // group -> the collider GroundAdd / DipAt are asked with
            readonly Dictionary<Collider, byte> repOf = new Dictionary<Collider, byte>();
            readonly Dictionary<Collider, GridMesh> meshes = new Dictionary<Collider, GridMesh>();
            readonly GroundGridCore core = new GroundGridCore();
            readonly List<GridBox> blocks = new List<GridBox>();
            static readonly Collider[] hits = new Collider[64];
            byte terrainGroup; bool allPending; int frame = -1, dueFrame = -1; double spent;

            public bool Alive => game && game.gridMap == this;

            public GroundGridMap(ZGame g, int walkMask, float w, float d)
            {
                game = g; mask = walkMask;
                ox = -GridMarginM; oz = -d - GridMarginM;
                ncx = Mathf.CeilToInt((w + 2f * GridMarginM) * GridInv); ncz = Mathf.CeilToInt((d + 2f * GridMarginM) * GridInv);
                ntx = (ncx + GridMask) >> GridShift; ntz = (ncz + GridMask) >> GridShift;
                ncx = ntx << GridShift; ncz = ntz << GridShift;
                tiles = new GridTile[ntx * ntz];
            }

            public bool Try(float x, float z, out float y)
            {
                y = 0f; var s = GridStats; s.Calls++;
                int f = Time.frameCount;
                if (f != frame) { frame = f; spent = 0; }
                if (pending.Count > 0 && dueFrame != f && !Time.inFixedTimeStep) Due(f);
                float fx = (x - ox) * GridInv, fz = (z - oz) * GridInv;
                if (!(fx >= 0f && fz >= 0f && fx < ncx && fz < ncz)) { s.RayOutside++; return false; }
                int cx = (int)fx, cz = (int)fz, tx = cx >> GridShift, tz = cz >> GridShift;
                var t = tiles[tz * ntx + tx];
                if (t == null && (t = Bake(tx, tz)) == null) { s.RayUnbaked++; return false; }
                int lx = cx & GridMask, lz = cz & GridMask;
                byte gi = t.g[(lz << GridShift) | lx];
                if (gi == 0) { s.RayCell++; return false; }
                int c0 = lz * GridNC + lx;
                float u = fx - cx, v = fz - cz, a = t.y[c0], b = t.y[c0 + 1], c = t.y[c0 + GridNC], d = t.y[c0 + GridNC + 1];
                float lo = a + (b - a) * u;
                var rep = reps[gi];
                y = lo + (c + (d - c) * u - lo) * v + CraterField.GroundAdd(rep, x, z) - OzBridgeWobble.DipAt(rep, x, z);
                s.Grid++;
                return true;
            }

            // -------------------------------------------------------------------------------------------- baking
            GridTile Bake(int tx, int tz)
            {
                if (allPending || spent >= GridBakeBudgetMs) return null;
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                var t = new GridTile();
                Fill(t, tx, tz);
                tiles[tz * ntx + tx] = t;
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                spent += ms;
                var s = GridStats; s.Tiles++; s.BakeMs += ms; if (ms > s.BakeMsMax) s.BakeMsMax = ms; s.Pieces += core.Pieces; s.CellsPassed += core.Passed; s.CellsBaked += GroundGridCore.N * GroundGridCore.N;
                return t;
            }

            void Fill(GridTile t, int tx, int tz)
            {
                double C = GridCell, X0 = ox + (double)(tx << GridShift) * C, Z0 = oz + (double)(tz << GridShift) * C, S = (GroundGridCore.N + 1) * C;
                core.Clear(); blocks.Clear();
                foreach (var h in holds.Values) if (Overlaps(h, X0, Z0, S)) blocks.Add(h);
                foreach (var p in pending) if (p.all || Overlaps(p, X0, Z0, S)) blocks.Add(p);
                var centre = new Vector3((float)(X0 + S * 0.5), 0f, (float)(Z0 + S * 0.5));
                var half = new Vector3((float)(S * 0.5 + 0.01), 200f, (float)(S * 0.5 + 0.01));
                int n = Physics.OverlapBoxNonAlloc(centre, half, hits, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
                if (n >= hits.Length) { System.Array.Clear(hits, 0, hits.Length); GridStats.FullTiles++; for (int k = 0; k < t.g.Length; k++) t.g[k] = 0; return; }
                for (int i = 0; i < n; i++) Gather(hits[i], tx, tz, X0, Z0, S);
                System.Array.Clear(hits, 0, n);
                for (int j = 0; j < GroundGridCore.N; j++)
                    for (int i = 0; i < GroundGridCore.N; i++)
                    {
                        double cx0 = X0 + i * C, cz0 = Z0 + j * C;
                        foreach (var b in blocks) if (b.all || (b.x0 <= cx0 + C && b.x1 >= cx0 && b.z0 <= cz0 + C && b.z1 >= cz0)) { core.Blocked[(j << GridShift) | i] = true; break; }
                    }
                core.Bake(X0, Z0, C, GridTol, t.y, t.g);
            }

            static bool Overlaps(GridBox b, double X0, double Z0, double S) => b.x0 <= X0 + S && b.x1 >= X0 && b.z0 <= Z0 + S && b.z1 >= Z0;

            void Gather(Collider col, int tx, int tz, double X0, double Z0, double S)
            {
                byte g = GroupOf(col);
                if (g != 0 && col is MeshCollider mc)
                {
                    var gm = MeshOf(mc);
                    if (gm != null && gm.ok)
                    {
                        if (gm.byTile.TryGetValue(tz * ntx + tx, out var list))
                            foreach (int q in list)
                            {
                                Vector3 a = gm.w[gm.t[q]], b = gm.w[gm.t[q + 1]], c = gm.w[gm.t[q + 2]];
                                core.Add(a.x, a.y, a.z, b.x, b.y, b.z, c.x, c.y, c.z, g);
                            }
                        return;
                    }
                }
                else if (g != 0 && col is BoxCollider bc && UprightTop(bc, out var p00, out var p10, out var p01, out var p11))
                {
                    core.Add(p00.x, p00.y, p00.z, p01.x, p01.y, p01.z, p11.x, p11.y, p11.z, g);
                    core.Add(p00.x, p00.y, p00.z, p11.x, p11.y, p11.z, p10.x, p10.y, p10.z, g);
                    return;
                }
                var cb = col.bounds;                                                                  // not readable here: the ray keeps its cells
                blocks.Add(new GridBox { x0 = cb.min.x - 0.01f, z0 = cb.min.z - 0.01f, x1 = cb.max.x + 0.01f, z1 = cb.max.z + 0.01f });
                GridStats.Unreadable++;
            }

            byte GroupOf(Collider c)
            {
                if (repOf.TryGetValue(c, out var gi)) return gi;
                bool terrain = c == game.ground || (CraterField.IsGroundCollider(c) && !CraterField.IsBowl(c));
                if (terrain && terrainGroup != 0) gi = terrainGroup;
                else if (reps.Count < 255) { gi = (byte)reps.Count; reps.Add(c); if (terrain) terrainGroup = gi; }
                else gi = 0;
                repOf[c] = gi;
                return gi;
            }

            /// <summary>The mesh collider's world triangles that face up, binned by tile (a tile reaches one cell past its far edges); rebuilt when its mesh or
            /// its transform changed.</summary>
            GridMesh MeshOf(MeshCollider mc)
            {
                var mesh = mc.sharedMesh; var m = mc.transform.localToWorldMatrix;
                if (meshes.TryGetValue(mc, out var gm) && gm.mesh == mesh && gm.m.Equals(m)) return gm;
                gm = new GridMesh { mesh = mesh, m = m };
                meshes[mc] = gm;
                if (mc.convex || !mesh || !mesh.isReadable || m.determinant <= 0f) return gm;
                var v = mesh.vertices; var tri = mesh.triangles;
                gm.w = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++) gm.w[i] = m.MultiplyPoint3x4(v[i]);
                var up = new List<int>(tri.Length);
                double C = GridCell, T = GroundGridCore.N * C;
                for (int q = 0; q + 2 < tri.Length; q += 3)
                {
                    Vector3 a = gm.w[tri[q]], b = gm.w[tri[q + 1]], c = gm.w[tri[q + 2]];
                    double ny = (double)(b.z - a.z) * (c.x - a.x) - (double)(b.x - a.x) * (c.z - a.z);   // Cross(b - a, c - a).y: Unity's front face
                    if (!(ny > 0)) continue;
                    if (Mathf.Max(a.y, Mathf.Max(b.y, c.y)) > 199f || Mathf.Min(a.y, Mathf.Min(b.y, c.y)) < -199f) { gm.ok = false; GridStats.Unreadable++; return gm; }   // past the ray's reach: keep the ray
                    int at = up.Count; up.Add(tri[q]); up.Add(tri[q + 1]); up.Add(tri[q + 2]);
                    double x0 = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), x1 = Mathf.Max(a.x, Mathf.Max(b.x, c.x)), z0 = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), z1 = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                    int ta = System.Math.Max(0, (int)System.Math.Floor((x0 - GroundGridCore.EpsX - ox - C) / T)), tb = System.Math.Min(ntx - 1, (int)System.Math.Floor((x1 + GroundGridCore.EpsX - ox) / T));
                    int za = System.Math.Max(0, (int)System.Math.Floor((z0 - GroundGridCore.EpsX - oz - C) / T)), zb = System.Math.Min(ntz - 1, (int)System.Math.Floor((z1 + GroundGridCore.EpsX - oz) / T));
                    for (int j = za; j <= zb; j++)
                        for (int i = ta; i <= tb; i++)
                        {
                            int key = j * ntx + i;
                            if (!gm.byTile.TryGetValue(key, out var l)) gm.byTile[key] = l = new List<int>();
                            l.Add(at);
                        }
                }
                gm.t = up.ToArray(); gm.ok = true;
                GridStats.Meshes++; GridStats.MeshTriangles += gm.t.Length / 3;
                return gm;
            }

            /// <summary>A box whose own up is the world's up: its top face (what a ray straight down meets on it).</summary>
            static bool UprightTop(BoxCollider bc, out Vector3 p00, out Vector3 p10, out Vector3 p01, out Vector3 p11)
            {
                var tr = bc.transform; var m = tr.localToWorldMatrix;
                p00 = p10 = p01 = p11 = default;
                var up = m.MultiplyVector(Vector3.up);
                if (m.determinant <= 0f || up.y <= 0f || Mathf.Abs(up.x) > 1e-6f * up.y || Mathf.Abs(up.z) > 1e-6f * up.y) return false;
                Vector3 c = bc.center, e = bc.size * 0.5f;
                p00 = m.MultiplyPoint3x4(c + new Vector3(-e.x, e.y, -e.z)); p10 = m.MultiplyPoint3x4(c + new Vector3(e.x, e.y, -e.z));
                p01 = m.MultiplyPoint3x4(c + new Vector3(-e.x, e.y, e.z)); p11 = m.MultiplyPoint3x4(c + new Vector3(e.x, e.y, e.z));
                if (p00.x > p10.x) { var q = p00; p00 = p10; p10 = q; q = p01; p01 = p11; p11 = q; }   // a box turned half round: the order of its corners
                if (p00.z > p01.z) { var q = p00; p00 = p01; p01 = q; q = p10; p10 = p11; p11 = q; }
                return true;
            }

            // -------------------------------------------------------------------------------------------- changes
            GridBox Box(float x0, float z0, float x1, float z1) => new GridBox { x0 = x0 - GridCell, z0 = z0 - GridCell, x1 = x1 + GridCell, z1 = z1 + GridCell, frame = Time.frameCount, fixedTime = Time.fixedTime };

            /// <summary>Every baked cell of the box answers by the ray from now on.</summary>
            void Zero(GridBox b)
            {
                int i0 = Mathf.Max(0, Mathf.FloorToInt((b.x0 - ox) * GridInv)), i1 = Mathf.Min(ncx - 1, Mathf.FloorToInt((b.x1 - ox) * GridInv));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((b.z0 - oz) * GridInv)), j1 = Mathf.Min(ncz - 1, Mathf.FloorToInt((b.z1 - oz) * GridInv));
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        var t = tiles[(j >> GridShift) * ntx + (i >> GridShift)];
                        if (t != null) t.g[((j & GridMask) << GridShift) | (i & GridMask)] = 0;
                    }
            }

            public void Dirty(float x0, float z0, float x1, float z1) { var b = Box(x0, z0, x1, z1); Zero(b); pending.Add(b); GridStats.Dirty++; }

            public void Hold(object key, float x0, float z0, float x1, float z1) { var b = Box(x0, z0, x1, z1); holds[key] = b; Zero(b); GridStats.Holds++; }

            public void Release(object key)
            {
                if (!holds.TryGetValue(key, out var b)) return;
                holds.Remove(key);
                b.frame = Time.frameCount; b.fixedTime = Time.fixedTime;
                pending.Add(b); GridStats.Releases++;
            }

            public void DirtyAll()
            {
                for (int i = 0; i < tiles.Length; i++) tiles[i] = null;
                reps.Clear(); reps.Add(null); repOf.Clear(); meshes.Clear(); terrainGroup = 0;
                pending.Add(new GridBox { all = true, frame = Time.frameCount, fixedTime = Time.fixedTime }); allPending = true;
            }

            /// <summary>Changes a later frame and a physics step old are settled: their tiles go, to be baked again when asked.</summary>
            void Due(int f)
            {
                dueFrame = f; float ft = Time.fixedTime;
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    var p = pending[i];
                    if (f <= p.frame || ft <= p.fixedTime) continue;
                    pending.RemoveAt(i);
                    if (p.all)
                    {
                        allPending = false;
                        for (int k = 0; k < tiles.Length; k++) tiles[k] = null;
                        continue;
                    }
                    int a = Mathf.Max(0, Mathf.FloorToInt((p.x0 - ox) * GridInv) >> GridShift), b = Mathf.Min(ntx - 1, Mathf.FloorToInt((p.x1 - ox) * GridInv) >> GridShift);
                    int c = Mathf.Max(0, Mathf.FloorToInt((p.z0 - oz) * GridInv) >> GridShift), d = Mathf.Min(ntz - 1, Mathf.FloorToInt((p.z1 - oz) * GridInv) >> GridShift);
                    for (int j = c; j <= d; j++) for (int k = a; k <= b; k++) if (tiles[j * ntx + k] != null) { tiles[j * ntx + k] = null; GridStats.TilesDropped++; }
                }
                if (pending.Count == 0) PruneMeshes();
            }

            /// <summary>Forget destroyed colliders (a crater's bowl, a removed platform's deck).</summary>
            void PruneMeshes()
            {
                List<Collider> gone = null;
                foreach (var kv in meshes) if (!kv.Key) (gone ??= new List<Collider>()).Add(kv.Key);
                if (gone != null) foreach (var c in gone) meshes.Remove(c);
            }

            public int Baked { get { int n = 0; foreach (var t in tiles) if (t != null) n++; return n; } }
            public int Pending => pending.Count;
            public int Held => holds.Count;
        }

        /// <summary>Checks: the grid's state (baked tiles, changes waiting, holds); -1 when no grid is made yet.</summary>
        public int GroundGridBaked => gridMap != null ? gridMap.Baked : -1;
        public int GroundGridPending => gridMap != null ? gridMap.Pending : -1;
        public int GroundGridHolds => gridMap != null ? gridMap.Held : -1;

        /// <summary>Checks: the ray's own answer here (the grid not asked), in the same frame as a grid answer.</summary>
        public float GroundYRay(float x, float z)
        {
            bool on = GroundGridOn; GroundGridOn = false;
            try { return GroundY(x, z); } finally { GroundGridOn = on; }
        }
    }

    /// <summary>The ground grid's counters (ZGame.GridStats): calls, who answered, bakes.</summary>
    public sealed class GroundGridStats
    {
        public long Calls, Grid, RayCell, RayUnbaked, RayOutside;
        public long Tiles, TilesDropped, FullTiles, Pieces, CellsPassed, CellsBaked, Meshes, MeshTriangles, Unreadable;
        public long Dirty, Holds, Releases, DirtyAll;
        public double BakeMs, BakeMsMax;
        public void Reset()
        {
            Calls = Grid = RayCell = RayUnbaked = RayOutside = 0; Tiles = TilesDropped = FullTiles = Pieces = CellsPassed = CellsBaked = Meshes = MeshTriangles = Unreadable = 0;
            Dirty = Holds = Releases = DirtyAll = 0; BakeMs = BakeMsMax = 0;
        }
        public override string ToString() =>
            $"calls {Calls}: grid {Grid} ({(Calls > 0 ? 100.0 * Grid / Calls : 0):0.0} %), ray in a ray cell {RayCell}, not baked yet {RayUnbaked}, outside {RayOutside}; tiles baked {Tiles} " +
            $"({BakeMs:0.0} ms, worst {BakeMsMax:0.00} ms, cells passing {(CellsBaked > 0 ? 100.0 * CellsPassed / CellsBaked : 0):0.0} %), dropped {TilesDropped}, full {FullTiles}; meshes {Meshes} " +
            $"({MeshTriangles} up-facing triangles), unreadable {Unreadable}; changes {Dirty}, holds {Holds}, released {Releases}, all {DirtyAll}";
    }
}
