// OpenZ (GPL-3.0) - version 1.5 kit factory_restore_crane_r3: the moment a destroyed building becomes whole again (view only, no rule).
using System.Collections;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// 1.5 factory_restore_crane_r3 (VIEW ONLY: the rules revive the building exactly when they did; it produces, owns and is targeted as
    /// before). A destroyed robot / vehicle factory, radar or repair station that the rules revive (a crane's repair, or Zod's own rebuild)
    /// used to rise 35 % out of its rubble with haze puffs. Now it gets the bridges' restore (kit bridge_restore_r3, same shader, materials,
    /// sounds and look), scaled to the building, over <see cref="Span"/> s:
    /// <list type="bullet">
    /// <item>the building's own model is cut into puzzle pieces: slices along its longer side, each split down the middle, and a lower and
    /// an upper layer (real cuts: triangles clipped at the planes, every piece keeps each part's own mesh space, materials and team paint,
    /// so a landed piece is exactly that part of the building);</item>
    /// <item>a faint orange hologram of the whole building appears; the pieces fly in as half-transparent holograms (the lower ones rise out
    /// of the ground, the upper ones drop from above) on short eased arcs, rank by rank from BOTH ends toward the middle, and turn solid as
    /// they lock: a small clank, low dust at the ground seams, a few sparks; the last piece (middle, top) locks with a heavier thud and the
    /// metallic ring;</item>
    /// <item>meanwhile the construction-orange energy fill sweeps over it from both ends and fades as the building becomes normal.</item>
    /// </list>
    /// The cut runs a little each frame from the moment the building falls (it is long done when a crane arrives). Off (<see cref="On"/>
    /// false), without the shader, or for a model that cannot be cut = the earlier rise.
    /// </summary>
    public static class OzBuildingRestore
    {
        public static bool On = true;
        /// <summary>The animation's length (s of game time; 1..2).</summary>
        public static float Span = 1.8f;
        /// <summary>The energy's fade after the building is normal (s).</summary>
        public static float FadeAfter = 0.5f;
        /// <summary>A piece's flight (s) and the slice length the building is cut into (m).</summary>
        public static float Flight = 0.5f, SliceM = 2.6f;
        /// <summary>The cut's share of one frame (ms).</summary>
        public static float CutBudgetMs = 1.5f;
        /// <summary>Sound levels (world sounds under the voices).</summary>
        public static float ClankVol = 0.34f, LockVol = 0.85f, RingVol = 0.45f, EnergyVol = 0.42f;
        /// <summary>A model with more triangles than this is not cut (the earlier rise plays).</summary>
        public const int MaxTris = 160000;

        /// <summary>The imported (GLB) buildings' meshes are not Read/Write in a player: the cut reads the readable copies baked by menu
        /// OpenZ/V1.5/Building Restore/Bake readable meshes (Resources/OpenZ/RestoreMeshes/&lt;type&gt;__&lt;mesh&gt;_&lt;vertex count&gt;).</summary>
        public const string ReadableDir = "OpenZ/RestoreMeshes/";
        public static string MeshKey(string type, Mesh m) => type + "__" + San(m ? m.name : "") + "_" + (m ? m.vertexCount : 0);
        static string San(string s) { var b = new System.Text.StringBuilder(s.Length); foreach (var ch in s) b.Append(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_'); return b.ToString(); }
        static readonly Dictionary<string, Mesh> copies = new Dictionary<string, Mesh>();
        /// <summary>The mesh itself when it can be read, else its baked readable copy (null: none).</summary>
        public static Mesh Readable(string type, Mesh m)
        {
            if (!m) return null;
            if (m.isReadable) return m;
            var k = MeshKey(type, m);
            if (copies.TryGetValue(k, out var c)) return c;
            c = Resources.Load<Mesh>(ReadableDir + k);
            if (c && (!c.isReadable || c.vertexCount != m.vertexCount)) c = null;
            copies[k] = c;
            return c;
        }

        /// <summary>The kit draws the restore (on, and the bridge kit's shader is supported).</summary>
        public static bool Active => On && OzBridgeRestore.Materials() != null;
        /// <summary>The buildings this restore is for: every building with a destroyed state the rules revive, except bridges (their own kit)
        /// and forts (a fort's death ends the game).</summary>
        public static bool Eligible(ZObject o) => o != null && o.Kind == ZKind.Building && !o.IsBridge && !o.IsFort;

        public static int Noted, Runs, PiecesBuilt, Landings, Locks, Clanks, Sparks, DustPuffs, BuildFails, SyncCuts, Fallbacks, Aborted;
        public static float BuildMsMax, StepMsMax;
        public static string Last = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            On = true; Span = 1.8f; FadeAfter = 0.5f; Flight = 0.5f; SliceM = 2.6f; CutBudgetMs = 1.5f;
            ClankVol = 0.34f; LockVol = 0.85f; RingVol = 0.45f; EnergyVol = 0.42f;
            Noted = Runs = PiecesBuilt = Landings = Locks = Clanks = Sparks = DustPuffs = BuildFails = SyncCuts = Fallbacks = Aborted = 0;
            BuildMsMax = StepMsMax = 0f; Last = ""; host = null; copies.Clear();
        }

        static OzBuildingRestoreHost host;
        static OzBuildingRestoreHost Host()
        {
            if (host) return host;
            host = new GameObject("OZ_BuildingRestore").AddComponent<OzBuildingRestoreHost>();
            return host;
        }

        /// <summary>RulesView.BuildingDeath, BEFORE the collapse hides the model: a restore still running on it stops (its model is shown
        /// again, so the collapse takes all of it), its renderers and poses are noted, and the cut starts (a share of each frame).</summary>
        public static void Died(ZGame g, ZObject o, GameObject root)
        {
            if (!On || !Eligible(o) || !root) return;
            var h = Host();
            h.Abort(o.Id);
            if (Active) h.Note(o, root);
        }

        /// <summary>RulesView (Revived), right after BuildingBreaker.Restore showed the model again. True = this kit draws the restore (the
        /// earlier rise does not run).</summary>
        public static bool Begin(ZGame g, ZObject o, GameObject root)
        {
            if (!Active || g == null || g.World == null || !Eligible(o) || !root) return false;
            if (Host().Begin(g, o, root)) return true;
            Fallbacks++;
            return false;
        }

        /// <summary>A restore of this building is being drawn now.</summary>
        public static bool Running(int id) => host && host.Running(id);

        /// <summary>BuildingBreaker.Restore / RestoreV1: no haze puff when this kit draws the restore.</summary>
        public static bool QuietDust(ZObject o) => o != null && Active && Eligible(o) && host && host.Noted(o.Id);

        public static string Status() => $"building restore {(On ? "on" : "off")}, {Span:0.00} s: noted {Noted}, runs {Runs} (stopped {Aborted}, earlier rise instead {Fallbacks}), pieces cut {PiecesBuilt} "
                                         + $"(cut {BuildMsMax:0.0} ms at most, {StepMsMax:0.0} ms in one frame at most, in one frame {SyncCuts}, failed {BuildFails}), landings {Landings}, last locks {Locks}, "
                                         + $"clanks {Clanks}, dust {DustPuffs}, sparks {Sparks}; last: {(Last.Length > 0 ? Last : "-")}";

        /// <summary>The no-Play check: the model under `root` (as it stands now) cut whole. Pieces, layers, slices, the area before / after the
        /// cut, the cut's time, the frames the sliced cut takes; false = nothing could be cut.</summary>
        public static bool CheckCut(GameObject root, string type, out int pieces, out int slices, out int layers, out float areaIn, out float areaOut, out float ms, out int steps, out float stepMax, out int renderers, out int hideOnly)
        {
            pieces = slices = layers = steps = renderers = hideOnly = 0; areaIn = areaOut = ms = stepMax = 0f;
            var c = OzBuildingRestoreHost.NewCut(root, type);
            if (c == null) return false;
            var job = OzBuildingRestoreHost.CutJob(c, CutBudgetMs);
            while (job.MoveNext()) { }
            bool ok = c.done && c.pieces.Count > 0;
            pieces = c.pieces.Count; slices = c.slices; layers = c.layers; areaIn = c.areaIn; areaOut = c.areaOut; ms = c.ms; steps = c.steps; stepMax = c.stepMax; renderers = c.src.Count; hideOnly = c.hideOnly.Count;
            OzBuildingRestoreHost.Release(c);
            return ok;
        }
    }

    /// <summary>The scene's building restores (one per building) and their cut pieces (kept for the next time that building comes back).</summary>
    public sealed class OzBuildingRestoreHost : MonoBehaviour
    {
        // ------------------------------------------------------------------ the cut
        internal struct V { public Vector3 p, w, n, nw; public Vector4 t; public Vector2 uv, uv1; public Color c; }

        internal sealed class Src
        {
            public MeshRenderer r; public Mesh m; public Matrix4x4 M; public Material[] mats; public int layer;
            public bool hn, ht, h1, hc; public ShadowCastingMode shadows; public bool receive; public LightProbeUsage probes;
        }

        internal sealed class Solid { public int src; public Mesh mesh; }

        internal sealed class PieceData
        {
            public int along, across, layer, rank;
            public Vector3 pivot, joint;              // world at rest: the piece's middle, the middle of its seam toward the nearer end (ground or layer line)
            public Vector4 cut;                       // along min / max, across min / max in the piece's own coordinates (m)
            public Mesh fx;                           // every triangle, world - pivot (the hologram / energy shader's space)
            public readonly List<Solid> solids = new List<Solid>();
            public float area;
        }

        internal sealed class Cut
        {
            public readonly List<Src> src = new List<Src>();
            public readonly List<PieceData> pieces = new List<PieceData>();
            public readonly List<Renderer> hideOnly = new List<Renderer>();      // parts with no readable mesh (no copy baked): hidden during the restore, back with the rest
            public Vector3 centre, axis, axisB; public float half, width, height, yMin, ySplit, areaIn, areaOut, ms, stepMax;
            public int slices, layers, tris, steps; public bool done;
        }

        /// <summary>The renderers of the standing model under `root`: every shown mesh renderer (its mesh, or the baked readable copy of it),
        /// the LODs past the first and a fort's gun parts left out. Null when there is nothing to cut or too much.</summary>
        internal static Cut NewCut(GameObject root, string type)
        {
            if (!root) return null;
            var skip = new HashSet<Renderer>();
            foreach (var lg in root.GetComponentsInChildren<LODGroup>())
            {
                var lods = lg.GetLODs();
                for (int i = 1; i < lods.Length; i++) foreach (var r in lods[i].renderers) if (r) skip.Add(r);
            }
            var c = new Cut(); int tris = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || skip.Contains(r) || BuildingRuins.IsGunPart(r.transform, root.transform)) continue;
                var mf = r.GetComponent<MeshFilter>(); var m0 = mf ? mf.sharedMesh : null;
                if (!m0 || m0.vertexCount == 0) continue;
                var m = OzBuildingRestore.Readable(type, m0);
                if (!m) { c.hideOnly.Add(r); continue; }
                int t = 0; for (int sm = 0; sm < m.subMeshCount; sm++) if (m.GetTopology(sm) == MeshTopology.Triangles) t += (int)m.GetIndexCount(sm) / 3;
                if (t == 0) continue;
                tris += t;
                c.src.Add(new Src
                {
                    r = r, m = m, M = r.transform.localToWorldMatrix, mats = r.sharedMaterials, layer = r.gameObject.layer,
                    shadows = r.shadowCastingMode, receive = r.receiveShadows, probes = r.lightProbeUsage
                });
            }
            c.tris = tris;
            if (c.src.Count == 0 || tris > OzBuildingRestore.MaxTris) return null;
            return c;
        }

        internal static void Release(Cut c)
        {
            if (c == null) return;
            foreach (var p in c.pieces) { Gone(p.fx); foreach (var s in p.solids) Gone(s.mesh); }
            c.pieces.Clear();
        }

        static void Gone(Object o) { if (!o) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }   // the no-Play check runs in edit mode

        static float Along(Cut c, Vector3 w) => Vector3.Dot(w - c.centre, c.axis);
        static float Across(Cut c, Vector3 w) => Vector3.Dot(w - c.centre, c.axisB);
        static float Area(Vector3 a, Vector3 b, Vector3 d) => Vector3.Cross(b - a, d - a).magnitude * 0.5f;

        sealed class CellSrc { public readonly List<V> v = new List<V>(); public List<int>[] tri; }
        sealed class Cell
        {
            public readonly Dictionary<int, CellSrc> bySrc = new Dictionary<int, CellSrc>();
            public readonly List<V> fxV = new List<V>(); public readonly List<int> fxT = new List<int>();
            public Bounds wb; public bool any; public float area;
        }

        /// <summary>The cut, spread over frames: it yields once `budgetMs` of this frame are used. `c.done` once finished.</summary>
        internal static IEnumerator CutJob(Cut c, float budgetMs)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool Over() => sw.Elapsed.TotalMilliseconds > budgetMs;
            void Share() { float used = (float)sw.Elapsed.TotalMilliseconds; c.ms += used; c.stepMax = Mathf.Max(c.stepMax, used); c.steps++; }
            // the bounds of the whole model (world, at rest: the poses noted before the collapse)
            var b = new Bounds(); bool any = false;
            foreach (var s in c.src)
            {
                foreach (var p in s.m.vertices) { var w = s.M.MultiplyPoint3x4(p); if (!any) { b = new Bounds(w, Vector3.zero); any = true; } else b.Encapsulate(w); }
                if (Over()) { Share(); yield return null; sw.Restart(); }
            }
            if (!any) yield break;
            bool alongX = b.size.x >= b.size.z;
            c.axis = alongX ? Vector3.right : Vector3.forward; c.axisB = alongX ? Vector3.forward : Vector3.right;
            c.centre = b.center; c.half = (alongX ? b.size.x : b.size.z) * 0.5f; c.width = alongX ? b.size.z : b.size.x;
            c.yMin = b.min.y; c.height = b.size.y;
            int n = Mathf.Clamp(Mathf.RoundToInt(c.half * 2f / Mathf.Max(0.5f, OzBuildingRestore.SliceM)), 3, 11); if (n % 2 == 0) n++;   // odd: the middle locks last
            c.slices = n;
            c.layers = c.height > 3.5f ? 2 : 1;
            c.ySplit = c.yMin + c.height * 0.45f;
            float step = c.half * 2f / n, a0 = -c.half;
            var cells = new Dictionary<int, Cell>();
            var poly = new List<V>(10); var tmp = new List<V>(10);
            for (int si = 0; si < c.src.Count; si++)
            {
                var s = c.src[si]; var m = s.m; var M = s.M; var N = M.inverse.transpose;
                var pv = m.vertices; var pn = m.normals; var pt = m.tangents; var u0 = m.uv; var u1 = m.uv2; var pc = m.colors;
                s.hn = pn != null && pn.Length == pv.Length; s.ht = pt != null && pt.Length == pv.Length; s.h1 = u1 != null && u1.Length == pv.Length; s.hc = pc != null && pc.Length == pv.Length;
                bool h0 = u0 != null && u0.Length == pv.Length;
                var vv = new V[pv.Length];
                for (int i = 0; i < pv.Length; i++)
                {
                    var x = new V { p = pv[i], w = M.MultiplyPoint3x4(pv[i]), n = s.hn ? pn[i] : Vector3.up, t = s.ht ? pt[i] : new Vector4(1, 0, 0, 1), uv = h0 ? u0[i] : Vector2.zero, uv1 = s.h1 ? u1[i] : Vector2.zero, c = s.hc ? pc[i] : Color.white };
                    x.nw = N.MultiplyVector(x.n).normalized;
                    vv[i] = x;
                }
                if (Over()) { Share(); yield return null; sw.Restart(); }
                for (int sm = 0; sm < m.subMeshCount; sm++)
                {
                    if (m.GetTopology(sm) != MeshTopology.Triangles) continue;
                    var tris = m.GetTriangles(sm);
                    for (int k = 0; k + 2 < tris.Length; k += 3)
                    {
                        if ((k & 63) == 0 && Over()) { Share(); yield return null; sw.Restart(); }
                        var A = vv[tris[k]]; var B = vv[tris[k + 1]]; var C = vv[tris[k + 2]];
                        c.areaIn += Area(A.w, B.w, C.w);
                        float la = Along(c, A.w), lb = Along(c, B.w), lc = Along(c, C.w);
                        int i0 = CellOf(Mathf.Min(la, Mathf.Min(lb, lc)), a0, step, n), i1 = CellOf(Mathf.Max(la, Mathf.Max(lb, lc)), a0, step, n);
                        float ylo = Mathf.Min(A.w.y, Mathf.Min(B.w.y, C.w.y)), yhi = Mathf.Max(A.w.y, Mathf.Max(B.w.y, C.w.y));
                        int l0 = c.layers > 1 && ylo >= c.ySplit ? 1 : 0, l1 = c.layers > 1 && yhi > c.ySplit ? 1 : 0;
                        for (int ci = i0; ci <= i1; ci++)
                            for (int cj = 0; cj < 2; cj++)
                                for (int ly = l0; ly <= l1; ly++)
                                {
                                    poly.Clear(); poly.Add(A); poly.Add(B); poly.Add(C);
                                    if (ci > 0) Clip(poly, tmp, c, 0, a0 + ci * step, +1f);
                                    if (ci < n - 1 && poly.Count >= 3) Clip(poly, tmp, c, 0, a0 + (ci + 1) * step, -1f);
                                    if (poly.Count >= 3) Clip(poly, tmp, c, 1, 0f, cj == 0 ? -1f : +1f);
                                    if (c.layers > 1 && poly.Count >= 3) Clip(poly, tmp, c, 2, c.ySplit, ly == 0 ? -1f : +1f);
                                    if (poly.Count < 3) continue;
                                    int key = (ci * 2 + cj) * 2 + ly;
                                    if (!cells.TryGetValue(key, out var cell)) { cell = new Cell(); cells[key] = cell; }
                                    if (!cell.bySrc.TryGetValue(si, out var cs)) { cs = new CellSrc { tri = new List<int>[m.subMeshCount] }; cell.bySrc[si] = cs; }
                                    var tl = cs.tri[sm] ??= new List<int>();
                                    int bse = cs.v.Count, fb = cell.fxV.Count; cs.v.AddRange(poly); cell.fxV.AddRange(poly);
                                    for (int q = 1; q + 1 < poly.Count; q++)
                                    {
                                        tl.Add(bse); tl.Add(bse + q); tl.Add(bse + q + 1);
                                        cell.fxT.Add(fb); cell.fxT.Add(fb + q); cell.fxT.Add(fb + q + 1);
                                        cell.area += Area(poly[0].w, poly[q].w, poly[q + 1].w);
                                    }
                                    foreach (var x in poly) { if (!cell.any) { cell.wb = new Bounds(x.w, Vector3.zero); cell.any = true; } else cell.wb.Encapsulate(x.w); }
                                }
                    }
                }
            }
            var keys = new List<int>(cells.Keys); keys.Sort();
            foreach (var key in keys)
            {
                if (Over()) { Share(); yield return null; sw.Restart(); }
                var cell = cells[key]; if (!cell.any || cell.fxV.Count < 3) continue;
                int ly = key % 2, cj = (key / 2) % 2, ci = key / 4;
                var pd = new PieceData { along = ci, across = cj, layer = ly, rank = Mathf.Min(ci, n - 1 - ci), pivot = cell.wb.center, area = cell.area };
                float pa = Along(c, pd.pivot), pbx = Across(c, pd.pivot);
                pd.cut = new Vector4(ci > 0 ? a0 + ci * step - pa : -10000f, ci < n - 1 ? a0 + (ci + 1) * step - pa : 10000f, cj == 1 ? -pbx : -10000f, cj == 0 ? -pbx : 10000f);
                // the hologram / energy mesh: world - pivot (the bridge kit's shader space), world normals
                var fv = new List<Vector3>(cell.fxV.Count); var fn = new List<Vector3>(cell.fxV.Count);
                foreach (var x in cell.fxV) { fv.Add(x.w - pd.pivot); fn.Add(x.nw); }
                var fx = new Mesh { name = "OZ_BuildingPiece_" + key + "_fx", indexFormat = fv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                fx.SetVertices(fv); fx.SetNormals(fn); fx.SetTriangles(cell.fxT, 0); fx.RecalculateBounds();
                pd.fx = fx;
                // the solid parts: each renderer's own mesh space, submeshes and attributes (its materials and team paint fit exactly)
                var sk = new List<int>(cell.bySrc.Keys); sk.Sort();
                foreach (var si in sk)
                {
                    var cs = cell.bySrc[si]; var s = c.src[si];
                    var vs = new List<Vector3>(cs.v.Count); foreach (var x in cs.v) vs.Add(x.p);
                    var mesh = new Mesh { name = "OZ_BuildingPiece_" + key + "_" + si, indexFormat = vs.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                    mesh.SetVertices(vs);
                    if (s.hn) { var l = new List<Vector3>(cs.v.Count); foreach (var x in cs.v) l.Add(x.n.normalized); mesh.SetNormals(l); }
                    if (s.ht) { var l = new List<Vector4>(cs.v.Count); foreach (var x in cs.v) l.Add(x.t); mesh.SetTangents(l); }
                    { var l = new List<Vector2>(cs.v.Count); foreach (var x in cs.v) l.Add(x.uv); mesh.SetUVs(0, l); }
                    if (s.h1) { var l = new List<Vector2>(cs.v.Count); foreach (var x in cs.v) l.Add(x.uv1); mesh.SetUVs(1, l); }
                    if (s.hc) { var l = new List<Color>(cs.v.Count); foreach (var x in cs.v) l.Add(x.c); mesh.SetColors(l); }
                    mesh.subMeshCount = cs.tri.Length;
                    for (int sm = 0; sm < cs.tri.Length; sm++) mesh.SetTriangles(cs.tri[sm] ?? new List<int>(), sm, false);
                    if (!s.hn) mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    pd.solids.Add(new Solid { src = si, mesh = mesh });
                }
                float jointA = ci <= (n - 1) / 2 ? a0 + ci * step : a0 + (ci + 1) * step;
                if (ci == (n - 1) / 2) jointA = a0 + ci * step;
                pd.joint = c.centre + c.axis * jointA + c.axisB * (cj == 0 ? -c.width * 0.25f : c.width * 0.25f);
                pd.joint.y = ly == 0 ? c.yMin : c.ySplit;
                c.areaOut += pd.area;
                c.pieces.Add(pd);
            }
            c.pieces.Sort((x, y) => x.rank != y.rank ? x.rank.CompareTo(y.rank) : x.layer != y.layer ? x.layer.CompareTo(y.layer) : x.along != y.along ? x.along.CompareTo(y.along) : x.across.CompareTo(y.across));
            Share();
            c.done = true;
        }

        static int CellOf(float a, float a0, float step, int n) => Mathf.Clamp(Mathf.FloorToInt((a - a0) / step), 0, n - 1);

        /// <summary>Keeps the part of the polygon where sign x (coordinate - at) >= 0; kind 0 along, 1 across, 2 height (world); every
        /// attribute interpolated (object and world space alike: the pose is affine, so one ratio fits both).</summary>
        static void Clip(List<V> poly, List<V> tmp, Cut c, int kind, float at, float sign)
        {
            tmp.Clear();
            int cnt = poly.Count;
            for (int i = 0; i < cnt; i++)
            {
                var P = poly[i]; var Q = poly[(i + 1) % cnt];
                float dp = sign * (Coord(c, kind, P.w) - at), dq = sign * (Coord(c, kind, Q.w) - at);
                if (dp >= 0f) tmp.Add(P);
                if ((dp >= 0f) != (dq >= 0f)) tmp.Add(Lerp(P, Q, dp / (dp - dq)));
            }
            poly.Clear(); poly.AddRange(tmp);
        }

        static float Coord(Cut c, int kind, Vector3 w) => kind == 0 ? Along(c, w) : kind == 1 ? Across(c, w) : w.y;

        static V Lerp(V a, V b, float k) => new V
        {
            p = Vector3.LerpUnclamped(a.p, b.p, k), w = Vector3.LerpUnclamped(a.w, b.w, k), n = Vector3.LerpUnclamped(a.n, b.n, k), nw = Vector3.LerpUnclamped(a.nw, b.nw, k).normalized,
            t = Vector4.LerpUnclamped(a.t, b.t, k), uv = Vector2.LerpUnclamped(a.uv, b.uv, k), uv1 = Vector2.LerpUnclamped(a.uv1, b.uv1, k), c = Color.LerpUnclamped(a.c, b.c, k)
        };

        // ------------------------------------------------------------------ the scene's notes, cuts and runs
        readonly Dictionary<int, Cut> cuts = new Dictionary<int, Cut>();
        readonly Dictionary<int, IEnumerator> jobs = new Dictionary<int, IEnumerator>();
        readonly Dictionary<int, Run> runs = new Dictionary<int, Run>();
        readonly HashSet<int> failed = new HashSet<int>();
        readonly List<int> finished = new List<int>();

        internal bool Noted(int id) => cuts.ContainsKey(id) && !failed.Contains(id);
        internal bool Running(int id) => runs.ContainsKey(id);

        /// <summary>The model's renderers and poses now (before the collapse moves or hides anything) and its cut started. A model cut before
        /// keeps its pieces (it is the same model).</summary>
        internal void Note(ZObject o, GameObject root)
        {
            if (failed.Contains(o.Id)) return;
            if (cuts.TryGetValue(o.Id, out var have) && (have.done || jobs.ContainsKey(o.Id))) return;
            var c = NewCut(root, o.Type);
            if (c == null) { failed.Add(o.Id); OzBuildingRestore.BuildFails++; Debug.Log($"[OpenZ] building restore: {o.Type} #{o.Id} has nothing that can be cut (or too much; readable copies: menu OpenZ/V1.5/Building Restore/Bake readable meshes): the earlier rise plays"); return; }
            if (c.hideOnly.Count > 0) Debug.Log($"[OpenZ] building restore: {o.Type} #{o.Id}: {c.hideOnly.Count} part(s) without a readable mesh are not cut (shown with the rest at the end)");
            cuts[o.Id] = c; jobs[o.Id] = CutJob(c, Mathf.Max(0.3f, OzBuildingRestore.CutBudgetMs));
            OzBuildingRestore.Noted++;
        }

        bool Ready(ZObject o, GameObject root)
        {
            if (failed.Contains(o.Id)) return false;
            if (!cuts.ContainsKey(o.Id)) Note(o, root);                                                   // never seen falling (it started as a ruin): the model as it stands now
            if (!cuts.TryGetValue(o.Id, out var c)) return false;
            if (jobs.TryGetValue(o.Id, out var job))
            {   // the repair came before the sliced cut was done: the rest now
                OzBuildingRestore.SyncCuts++;
                try { while (job.MoveNext()) { } }
                catch (System.Exception e) { Debug.LogWarning($"[OpenZ] building restore: cutting {o.Type} #{o.Id} failed ({e.Message}): the earlier rise plays"); }
                jobs.Remove(o.Id);
                Finish(o.Id, o.Type, c);
            }
            return cuts.ContainsKey(o.Id) && c.done && c.pieces.Count > 0;
        }

        void Finish(int id, string type, Cut c)
        {
            if (!c.done || c.pieces.Count == 0) { failed.Add(id); OzBuildingRestore.BuildFails++; Release(c); cuts.Remove(id); return; }
            OzBuildingRestore.PiecesBuilt += c.pieces.Count; OzBuildingRestore.BuildMsMax = Mathf.Max(OzBuildingRestore.BuildMsMax, c.ms); OzBuildingRestore.StepMsMax = Mathf.Max(OzBuildingRestore.StepMsMax, c.stepMax);
            Debug.Log($"[OpenZ] building restore: {type} #{id} cut into {c.pieces.Count} pieces ({c.slices} slices x 2 x {c.layers} layer(s)) from {c.src.Count} parts, {c.tris} triangles, "
                      + $"{c.half * 2f:0.0} x {c.width:0.0} x {c.height:0.0} m, area {c.areaIn:0.0} -> {c.areaOut:0.0} m2, {c.ms:0.0} ms over {c.steps} frame(s), {c.stepMax:0.0} ms at most in one");
        }

        void StepJobs()
        {
            if (jobs.Count == 0) return;
            int id = -1; IEnumerator job = null;
            foreach (var kv in jobs) { id = kv.Key; job = kv.Value; break; }                                 // one building's share a frame
            bool more;
            try { more = job.MoveNext(); }
            catch (System.Exception e) { Debug.LogWarning($"[OpenZ] building restore: cutting building #{id} failed ({e.Message}): the earlier rise plays"); more = false; }
            if (more) return;
            jobs.Remove(id);
            if (cuts.TryGetValue(id, out var c)) Finish(id, "building", c);
        }

        internal bool Begin(ZGame g, ZObject o, GameObject root)
        {
            if (runs.ContainsKey(o.Id)) return true;
            if (!Ready(o, root)) return false;
            var r = new Run();
            if (!r.Start(this, g, o, cuts[o.Id])) { r.Dispose(); return false; }
            runs[o.Id] = r;
            OzBuildingRestore.Runs++;
            OzBuildingRestore.Last = $"{o.Type} #{o.Id} {o.Owner} at game time {g.World.Time:F2}";
            Debug.Log("[OpenZ] building restore: " + OzBuildingRestore.Last);
            return true;
        }

        internal void Abort(int id) { if (runs.TryGetValue(id, out var r)) { r.Dispose(); runs.Remove(id); OzBuildingRestore.Aborted++; } }

        void LateUpdate()
        {
            StepJobs();
            finished.Clear();
            foreach (var kv in runs) if (!kv.Value.Step()) finished.Add(kv.Key);
            foreach (var id in finished) { runs[id].Dispose(); runs.Remove(id); }
        }

        void OnDestroy()
        {
            foreach (var r in runs.Values) r.Dispose();
            runs.Clear();
            foreach (var c in cuts.Values) Release(c);
            cuts.Clear(); jobs.Clear();
        }

        // ------------------------------------------------------------------ one restore
        sealed class Piece
        {
            public PieceData d; public GameObject go, ghost; public Renderer fx, ghostR; public readonly List<Renderer> solid = new List<Renderer>();
            public Vector3 from; public Quaternion r0; public float t0, t1, arc; public bool landed, last;
        }

        sealed class Run
        {
            ZGame game; ZObject o; Cut cut; GameObject root;
            readonly List<Piece> pieces = new List<Piece>();
            readonly List<Renderer> hidden = new List<Renderer>();
            readonly List<Material> made = new List<Material>();
            readonly Dictionary<Material, Material> cullOff = new Dictionary<Material, Material>();
            readonly List<(float t, float f)> frontKeys = new List<(float, float)>();
            MaterialPropertyBlock mpb; double simT0; float lockAt, lastT; bool intactShown, lockDone; Color dust;

            float T => (float)(game.World.Time - simT0);

            internal bool Start(OzBuildingRestoreHost host, ZGame g, ZObject obj, Cut c)
            {
                game = g; o = obj; cut = c; mpb = new MaterialPropertyBlock();
                var mats = OzBridgeRestore.Materials(); if (mats == null) return false;
                float span = Mathf.Clamp(OzBuildingRestore.Span, 1f, 2f);
                simT0 = g.World.Time;
                var biome = OzBridgeKit.Biome(g.level);
                dust = biome == "city" ? new Color(0.6f, 0.58f, 0.55f) : biome == "jungle" ? new Color(0.5f, 0.42f, 0.32f) : new Color(0.78f, 0.66f, 0.5f);
                OzRuinFires.End(o.Id);
                root = new GameObject("OZ_BuildingRestore_" + o.Id);
                root.transform.SetParent(host.transform, false);
                float k = Mathf.Clamp(cut.width / 7.6f, 1f, 2.2f);                                           // the flights scale with the building
                int ranks = (cut.slices + 1) / 2; bool two = cut.layers > 1;
                lockAt = 0.88f * span;
                float firstLand = 0.28f * span, upper = two ? 0.12f * span : 0f;
                var blk = new MaterialPropertyBlock();
                foreach (var pd in cut.pieces)
                {
                    var p = new Piece { d = pd };
                    float land = ranks <= 1 ? lockAt - upper : Mathf.Lerp(firstLand, lockAt - upper, pd.rank / (float)(ranks - 1));
                    if (pd.layer == 1) land += upper;
                    bool middle = pd.rank == ranks - 1;
                    p.last = middle && pd.layer == cut.layers - 1;
                    if (!p.last) land += (pd.along < cut.slices / 2 ? -0.025f : 0.025f) + (pd.across == 0 ? -0.015f : 0.015f);
                    float fl = OzBuildingRestore.Flight * Random.Range(0.92f, 1.08f);
                    p.t1 = Mathf.Min(land, lockAt); p.t0 = Mathf.Max(0.02f, p.t1 - fl);
                    float side = pd.across == 0 ? -1f : 1f;
                    float toEnd = pd.along < (cut.slices - 1) * 0.5f ? -1f : pd.along > (cut.slices - 1) * 0.5f ? 1f : 0f;
                    if (pd.layer == 0)
                    {   // the lower pieces rise out of the ground beside the building
                        p.from = pd.pivot + cut.axisB * side * Random.Range(2.6f, 4.2f) * k + Vector3.down * Random.Range(1.8f, 3.0f) * k + cut.axis * toEnd * Random.Range(0.3f, 1.2f) * k;
                        p.arc = Random.Range(1.3f, 2.2f) * k;
                    }
                    else
                    {   // the upper pieces come down from above
                        p.from = pd.pivot + cut.axisB * side * Random.Range(2.0f, 3.2f) * k + Vector3.up * Random.Range(3.0f, 4.5f) * k + cut.axis * toEnd * Random.Range(0.3f, 1.2f) * k;
                        p.arc = Random.Range(0.4f, 0.9f) * k;
                    }
                    p.r0 = Quaternion.Euler(Random.Range(-38f, 38f), Random.Range(-55f, 55f), Random.Range(-38f, 38f));
                    p.go = new GameObject("piece_" + pd.along + "_" + pd.across + "_" + pd.layer); p.go.transform.SetParent(root.transform, false);
                    // the fx part sits at the pivot (its mesh is world - pivot); the solid parts at their own renderer's pose (the go is the world at rest)
                    var fxGo = new GameObject("fx"); fxGo.transform.SetParent(p.go.transform, false); fxGo.transform.localPosition = pd.pivot;
                    p.fx = Part(fxGo, pd.fx, new[] { mats[0] }, ShadowCastingMode.Off, false, LightProbeUsage.Off); p.fx.enabled = false;
                    foreach (var s in pd.solids)
                    {
                        var src = cut.src[s.src];
                        var sg = new GameObject("solid_" + s.src); sg.layer = src.layer;
                        sg.transform.SetParent(p.go.transform, false);
                        sg.transform.localPosition = src.M.GetColumn(3); sg.transform.localRotation = src.M.rotation; sg.transform.localScale = src.M.lossyScale;
                        var sr = Part(sg, s.mesh, CullOff(src.mats), src.shadows, src.receive, src.probes);
                        CopyPaint(src.r, sr, src.mats.Length, blk);
                        sr.enabled = false;
                        p.solid.Add(sr);
                    }
                    var at0 = Pose(p, 0f, out var rot0); p.go.transform.SetPositionAndRotation(at0, rot0);
                    p.ghost = new GameObject("ghost_" + pd.along + "_" + pd.across + "_" + pd.layer); p.ghost.transform.SetParent(root.transform, false);
                    p.ghost.transform.localPosition = pd.pivot;
                    p.ghostR = Part(p.ghost, pd.fx, new[] { mats[0] }, ShadowCastingMode.Off, false, LightProbeUsage.Off);
                    pieces.Add(p);
                }
                frontKeys.Add((0.12f * span, 0f));
                for (int r = 0; r < ranks; r++)
                {
                    float land = ranks <= 1 ? lockAt : Mathf.Lerp(firstLand, lockAt, r / (float)(ranks - 1));
                    frontKeys.Add((land, Mathf.Min(1.04f, 2f * (r + 1) / cut.slices)));
                }
                // the standing model waits hidden until the end of the span (the rules have it whole already)
                foreach (var s in cut.src) if (s.r && s.r.enabled) { s.r.enabled = false; hidden.Add(s.r); }
                foreach (var r in cut.hideOnly) if (r && r.enabled) { r.enabled = false; hidden.Add(r); }
                var at = cut.centre;
                CombatAudio.I?.PlayPitched("bd_restore_energy", at, OzBuildingRestore.EnergyVol, 0f, o.Id, 0.95f, "br_restore_energy_a");
                OpenZFxPlayer.I?.AddLight(at + Vector3.up * Mathf.Min(4f, cut.height * 0.5f), OrangeLight, 3.2f, Mathf.Max(14f, cut.half * 1.6f), 0.55f, 2.2f, 0.15f, false);
                return true;
            }

            static readonly Color OrangeLight = new Color(1f, 0.55f, 0.18f), Spark = new Color(1f * 3f, 0.62f * 3f, 0.2f * 3f, 1f);

            Renderer Part(GameObject go, Mesh m, Material[] mt, ShadowCastingMode shadows, bool receive, LightProbeUsage probes)
            {
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = mt;
                r.shadowCastingMode = shadows; r.receiveShadows = receive; r.lightProbeUsage = probes; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                return r;
            }

            Material[] CullOff(Material[] src)
            {   // the cut faces of a landed piece stay closed to the eye until its neighbours land: both sides drawn
                var o2 = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    var m = src[i];
                    if (!m || !m.HasProperty("_Cull")) { o2[i] = m; continue; }
                    if (!cullOff.TryGetValue(m, out var c)) { c = new Material(m) { name = m.name + "_restore" }; c.SetFloat("_Cull", 0f); cullOff[m] = c; made.Add(c); }
                    o2[i] = c;
                }
                return o2;
            }

            /// <summary>The standing part's paint (TeamPaint's blocks: the team accents, the night class, the burn) on its piece.</summary>
            static void CopyPaint(Renderer from, Renderer to, int slots, MaterialPropertyBlock b)
            {
                if (!from || !from.HasPropertyBlock()) return;
                b.Clear(); from.GetPropertyBlock(b); if (!b.isEmpty) to.SetPropertyBlock(b);
                for (int i = 0; i < slots; i++) { b.Clear(); from.GetPropertyBlock(b, i); if (!b.isEmpty) to.SetPropertyBlock(b, i); }
            }

            /// <summary>Where the piece's go stands at clock t (the go is the world at rest when landed: position = flight offset).</summary>
            Vector3 Pose(Piece p, float k, out Quaternion rot)
            {
                float e = 1f - (1f - k) * (1f - k) * (1f - k), er = 1f - Mathf.Pow(1f - k, 4f);
                rot = Quaternion.Slerp(p.r0, Quaternion.identity, er);
                var at = Vector3.LerpUnclamped(p.from, p.d.pivot, e) + Vector3.up * Mathf.Sin(Mathf.PI * e) * p.arc;
                return at - rot * p.d.pivot;                                                                 // turn about the piece's own middle
            }

            void ShowIntact() { foreach (var r in hidden) if (r) r.enabled = true; hidden.Clear(); intactShown = true; }

            float Front(float t)
            {
                if (t <= frontKeys[0].t) return 0f;
                for (int i = 1; i < frontKeys.Count; i++)
                    if (t <= frontKeys[i].t)
                    {
                        float k = Mathf.InverseLerp(frontKeys[i - 1].t, frontKeys[i].t, t);
                        return Mathf.Lerp(frontKeys[i - 1].f, frontKeys[i].f, k * k * (3f - 2f * k));
                    }
                return 1.04f;
            }

            /// <summary>One frame. False = finished.</summary>
            internal bool Step()
            {
                if (game == null || game.World == null || o == null || o.Destroyed) return false;           // destroyed again: Died stopped it already
                float span = Mathf.Clamp(OzBuildingRestore.Span, 1f, 2f);
                float t = T; lastT = t;
                bool reduced = OzMotion.Reduced;
                float front = Front(t);
                float gain = t <= span ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (t - span) / Mathf.Max(0.05f, OzBuildingRestore.FadeAfter));
                float flash = t >= lockAt ? Mathf.Exp(-(t - lockAt) * 6f) : 0f;
                float ghostA = Mathf.Clamp01(t / 0.25f) * 0.13f * (t > span ? 0f : 1f);
                foreach (var p in pieces)
                {
                    if (!p.go) continue;
                    float k = Mathf.Clamp01((t - p.t0) / Mathf.Max(0.05f, p.t1 - p.t0));
                    if (!p.landed && t >= p.t1) Land(p);
                    if (p.landed)
                    {
                        float since = t - p.t1;
                        float settle = since < 0.14f ? -0.05f * Mathf.Sin(Mathf.PI * since / 0.14f) : 0f;   // a small settle as it locks
                        p.go.transform.SetPositionAndRotation(Vector3.up * settle, Quaternion.identity);
                        foreach (var s in p.solid) if (s) s.enabled = !intactShown;
                        p.fx.enabled = gain > 0.001f;
                        mpb.Clear(); Common(mpb, t, front);
                        float hit = 0.55f * Mathf.Exp(-since * 9f) + (p.last ? flash : flash * 0.5f) * 0.6f;
                        mpb.SetFloat(OzBridgeRestore.HitId, Mathf.Clamp01(hit)); mpb.SetFloat(OzBridgeRestore.GainId, gain);
                        mpb.SetVector(OzBridgeRestore.CutId, p.d.cut);
                        p.fx.SetPropertyBlock(mpb);
                    }
                    else if (t >= p.t0)
                    {
                        var pos = Pose(p, k, out var rot);
                        p.go.transform.SetPositionAndRotation(pos, rot);
                        p.fx.enabled = true;
                        float a = Mathf.Clamp01(k / 0.25f) * 0.85f;
                        if (!reduced && Random.value < 0.08f) a *= Random.Range(0.35f, 0.7f);                // the hologram's glitch flicker
                        mpb.Clear(); Common(mpb, t, 0f); mpb.SetFloat(OzBridgeRestore.AlphaId, a); mpb.SetFloat(OzBridgeRestore.HitId, k > 0.85f ? (k - 0.85f) / 0.15f * 0.5f : 0f);
                        mpb.SetVector(OzBridgeRestore.CutId, p.d.cut);
                        p.fx.SetPropertyBlock(mpb);
                    }
                    if (p.ghostR)
                    {
                        bool show = !p.landed && ghostA > 0.002f;
                        p.ghostR.enabled = show;
                        if (show)
                        {
                            float ga = ghostA; if (!reduced && Random.value < 0.04f) ga *= 0.5f;
                            mpb.Clear(); Common(mpb, t, 0f); mpb.SetFloat(OzBridgeRestore.AlphaId, ga); mpb.SetFloat(OzBridgeRestore.HitId, 0f);
                            mpb.SetVector(OzBridgeRestore.CutId, p.d.cut);
                            p.ghostR.SetPropertyBlock(mpb);
                        }
                    }
                }
                if (!intactShown && t >= span) ShowIntact();                                                  // the normal building, identical to the pieces
                if (intactShown) foreach (var p in pieces) foreach (var s in p.solid) if (s) s.enabled = false;
                return !(t > span + OzBuildingRestore.FadeAfter + 0.05f && intactShown);
            }

            void Common(MaterialPropertyBlock b, float t, float front)
            {
                b.SetVector(OzBridgeRestore.AxisId, new Vector4(cut.axis.x, cut.axis.y, cut.axis.z, cut.half));
                b.SetVector(OzBridgeRestore.AxisBId, new Vector4(cut.axisB.x, cut.axisB.y, cut.axisB.z, 0f));
                b.SetVector(OzBridgeRestore.CentreId, cut.centre);
                float band = front >= 1f ? Mathf.Clamp01(1f - (lastT - lockAt) / 0.3f) : 1f;
                b.SetVector(OzBridgeRestore.FrontId, new Vector4(front, 0.09f, 0.15f, band));
                b.SetFloat(OzBridgeRestore.ClockId, t);
                b.SetFloat(OzBridgeRestore.GainId, 1f);
            }

            void Land(Piece p)
            {
                p.landed = true; OzBuildingRestore.Landings++;
                foreach (var s in p.solid) if (s) s.enabled = !intactShown;
                var mats = OzBridgeRestore.Materials(); if (mats != null) p.fx.sharedMaterials = new[] { mats[1] };
                var fx = OpenZFxPlayer.I; var au = CombatAudio.I;
                var j = p.d.joint;
                if (p.d.layer == 0)
                {   // a little proper dust along the ground seam: low, irregular, quick (the levels' lit dust, never a round puff)
                    int puffs = p.last ? 4 : 2;
                    for (int i = 0; i < puffs; i++)
                    {
                        var at = j + cut.axisB * Random.Range(-0.45f, 0.45f) * cut.width * 0.5f + Vector3.up * 0.08f;
                        var vel = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.25f, 0.55f), Random.Range(-0.4f, 0.4f));
                        if (OzLevelDust.Emit(at, vel, Random.Range(0.45f, 0.75f), Random.Range(0.6f, 0.95f), Random.Range(0.32f, 0.45f), dust, 1f)) OzBuildingRestore.DustPuffs++;
                    }
                }
                if (fx)
                {
                    int n = p.last ? 16 : 4;
                    for (int i = 0; i < n; i++)
                    {
                        var at = j + cut.axisB * Random.Range(-0.5f, 0.5f) * cut.width * 0.5f + Vector3.up * 0.1f;
                        var v = Random.insideUnitSphere * (p.last ? 3.5f : 2f) + Vector3.up * Random.Range(1f, 2.5f);
                        fx.EmitChannel("wv2_spark", at, v, Random.Range(0.2f, 0.45f), Random.Range(0.035f, 0.06f), Spark, 1f);
                        OzBuildingRestore.Sparks++;
                    }
                    if (p.last) fx.AddLight(cut.centre + Vector3.up * Mathf.Min(5f, cut.height * 0.6f), OrangeLight, 5f, Mathf.Max(16f, cut.half * 1.6f), 0.45f, 2.2f, 0.1f, true);
                }
                if (p.last)
                {
                    if (lockDone) return;
                    lockDone = true; OzBuildingRestore.Locks++;
                    var c = cut.centre; c.y = cut.ySplit;
                    if (au)
                    {
                        au.PlayPitched("bd_restore_lock", c, OzBuildingRestore.LockVol, 0f, o.Id, 0.85f, OzSoundV3.BankMetalHeavy);
                        au.PlayPitched("bd_restore_ring", c, OzBuildingRestore.RingVol, 0f, o.Id, 0.92f, "br_restore_ring_a");
                    }
                    return;
                }
                if (p.d.across != 0) return;                                                                 // the two halves of a slice land together: one speaks
                OzBuildingRestore.Clanks++;
                au?.PlayPitched("bd_restore_clank", j, OzBuildingRestore.ClankVol, 0f, -1, Random.Range(1.05f, 1.25f), "sd_tank_breech_clank_a", "sd_tank_breech_clank_b", "final-metal-land-a", "final-metal-land-b");
            }

            internal void Dispose()
            {
                if (o != null && !o.Destroyed && !intactShown) ShowIntact();
                else foreach (var r in hidden) if (r) r.enabled = true;                                      // stopped: the model as it was (the collapse takes it next)
                hidden.Clear();
                if (root) Object.Destroy(root);
                foreach (var m in made) if (m) Object.Destroy(m);
                made.Clear(); cullOff.Clear(); pieces.Clear();
            }
        }
    }
}
