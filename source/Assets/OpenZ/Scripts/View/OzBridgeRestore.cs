// OpenZ (GPL-3.0) - version 1.5 kit bridge_restore_r3: the moment a destroyed bridge becomes whole again (view only, no rule).
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// 1.5 bridge_restore_r3 (VIEW ONLY: the rules repair the bridge exactly when they did; units never wait for this). Before: the whole
    /// bridge appeared in one frame with round tan puffs over it. Now, over <see cref="Span"/> s:
    /// <list type="bullet">
    /// <item>the bridge's own intact model is cut into puzzle pieces (slices along the deck, each split down the middle; the cuts are real:
    /// triangles clipped at the planes, so the pieces together are exactly the intact bridge);</item>
    /// <item>the wreck turns into orange light and fades, a faint hologram of the whole bridge appears where it will stand (the Unit
    /// editor's hologram look, Shaders/OzBridgeRestore.shader mode 0, construction orange);</item>
    /// <item>the pieces fly in as half-transparent holograms on short eased arcs, one after another from BOTH ends toward the middle, and turn
    /// solid as they lock in: a small clank, a little low dust and a few sparks at the joint; the last piece in the middle locks with a
    /// heavier thud and a short metallic ring;</item>
    /// <item>meanwhile the construction-orange energy fill (the flag capture's wash, mode 1) sweeps along the deck from both ends and fades
    /// as the bridge becomes normal; then the normal bridge.</item>
    /// </list>
    /// The automatic repair is known in advance (ZObject.NextAutoRepairTime): the animation runs on the rules' clock in the last
    /// <see cref="Span"/> s before it, so the bridge is whole the moment it becomes passable. A crane's repair is only known when it happens:
    /// then the same animation runs right after it (the deck already carries units, as the rules say). Off (<see cref="On"/> false) = the
    /// earlier look (chunks rising 3.5 s before, the swap, the haze puffs).
    /// </summary>
    public static class OzBridgeRestore
    {
        public static bool On = true;
        /// <summary>The animation's length (s of game time; 1..2). The pieces land between 0.3 x and 0.88 x of it.</summary>
        public static float Span = 1.5f;
        /// <summary>The energy's fade after the bridge is normal (s).</summary>
        public static float FadeAfter = 0.45f;
        /// <summary>A piece's flight (s) and the slice length the deck is cut into (m).</summary>
        public static float Flight = 0.42f, SliceM = 2.4f;
        /// <summary>The cut's share of one frame (ms): it runs over the frames after the bridge is destroyed, long before its repair.</summary>
        public static float CutBudgetMs = 1.5f;
        /// <summary>Sound levels (under the voices: the announcer and the robots keep their own levels above these).</summary>
        public static float ClankVol = 0.145f, LockVol = 0.29f, RingVol = 0.19f, EnergyVol = 0.27f, BedVol = 0.42f;   // 1.5 kit crane_repair_look_r6 (A9): were 0.32 / 0.8 / 0.42 / 0.38 / 0.7; measured: the restore's loudest 400 ms was 1.3 dB OVER Rachel's line and 7.4 dB over the crane's loop, now about 7 dB under her, at the loop's level
        /// <summary>1.5 kit crane_repair_look_r6 (A9): the restore's sounds fade in from its start, fade out from the moment the bridge stands, and fade out from where they are when the restore is cut short (s). <see cref="SoundFades"/> false = plain one-shots.</summary>
        public static float SoundFadeIn = 0.4f, SoundFadeOut = 0.8f, SoundFadeCut = 0.5f;
        public static bool SoundFades = true;
        public static int SoundGroups, SoundShared, SoundCuts;

        /// <summary>The kit draws the restore here (on, and its shader is supported): RulesView leaves the earlier chunks / puffs out.</summary>
        public static bool Active => On && Materials() != null;

        public static int Runs, PreRolls, PostRolls, Rushed, Aborted, PiecesBuilt, Landings, Locks, DustPuffs, Sparks, Clanks, BuildFails;
        public static float BuildMsMax, StepMsMax;   // the cut's whole time / its longest share of one frame (ms)
        public static int SyncCuts;                  // cuts that had to finish in one frame (a repair before the sliced cut was done)
        public static string Last = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            On = true; Span = 1.5f; FadeAfter = 0.45f; Flight = 0.42f; SliceM = 2.4f; CutBudgetMs = 1.5f; StepMsMax = 0f; SyncCuts = 0;
            Runs = PreRolls = PostRolls = Rushed = Aborted = PiecesBuilt = Landings = Locks = DustPuffs = Sparks = Clanks = BuildFails = 0; BuildMsMax = 0f; Last = "";
            mats = null; matsTried = false;
            SoundFadeIn = 0.4f; SoundFadeOut = 0.8f; SoundFadeCut = 0.5f; SoundFades = true; SoundGroups = SoundShared = SoundCuts = 0;   // 1.5 kit crane_repair_look_r6 (A9)
        }

        static OzBridgeRestoreHost host;
        static OzBridgeRestoreHost Host()
        {
            if (host) return host;
            host = new GameObject("OZ_BridgeRestore").AddComponent<OzBridgeRestoreHost>();
            return host;
        }

        /// <summary>RulesView.LateUpdate, every frame a destroyed bridge waits for its automatic repair (`left` s of game time). True = this
        /// kit owns the restore (the old 3.5 s chunk rise does not run).</summary>
        public static bool PreRoll(ZGame g, ZObject o, GameObject[] v, double left)
        {
            if (!Active || g == null || o == null || v == null) return false;                          // no shader here: the earlier 3.5 s chunk rise
            var h = Host();
            if (h.Running(o.Id)) return true;
            if (h.Failed(o.Id)) return false;                                                            // this bridge could not be cut: the earlier look
            if (!h.Prepared(o.Id)) h.StartPrepare(o, v);                                                 // the cut, sliced over the frames while it is down
            if (left > Span) return true;
            if (left <= 0.05) return true;                                                               // due on the next rules tick (a crane's job ends so): the swap starts it
            return h.Begin(g, o, v, (float)left);
        }

        /// <summary>RulesView.SwapBridge: the bridge was just destroyed (a run stops) or repaired by the rules (the run goes on, or starts
        /// now for a crane's repair). True = this kit owns the moment (no haze puffs, no wrench).</summary>
        public static bool Swapped(ZGame g, ZObject o, bool destroyed, GameObject[] v)
        {
            if (!On || g == null || o == null) return false;
            var h = Host();
            if (destroyed) { h.Abort(o.Id); if (v != null && Active) h.StartPrepare(o, v); return true; }   // the cut starts now, a little each frame
            if (h.Running(o.Id)) { h.Repaired(o.Id); return true; }
            if (v == null || !h.Begin(g, o, v, -1f)) return false;
            return true;
        }

        /// <summary>AudioDirector.OnRevived: the kit plays the rebuild bed at the start of its animation (true = do not play it again).</summary>
        public static bool OwnsRebuildSound(int id) => On && ((host && host.SoundTaken(id)) || (Materials() != null && !(host && host.Failed(id))));

        public static string Status() => $"bridge restore {(On ? "on" : "off")}, {Span:0.00} s: runs {Runs} (before the rules' repair {PreRolls}, after a crane's {PostRolls}, rushed {Rushed}, stopped {Aborted}), "
                                         + $"pieces cut {PiecesBuilt} (cut {BuildMsMax:0.0} ms at most, {StepMsMax:0.0} ms in one frame at most, in one frame {SyncCuts}, failed {BuildFails}), landings {Landings}, last locks {Locks}, clanks {Clanks}, dust {DustPuffs}, sparks {Sparks}; last: {(Last.Length > 0 ? Last : "-")}";

        // ------------------------------------------------------------------ materials
        public const string MaterialPath = "OpenZ/FX/M_OzBridgeRestore", ShaderName = "OpenZ/BridgeRestore";
        internal static readonly int ModeId = Shader.PropertyToID("_Mode"), AlphaId = Shader.PropertyToID("_Alpha"), HitId = Shader.PropertyToID("_Hit"), CutId = Shader.PropertyToID("_Cut"),
            AxisId = Shader.PropertyToID("_Axis"), AxisBId = Shader.PropertyToID("_AxisB"), CentreId = Shader.PropertyToID("_Centre"), FrontId = Shader.PropertyToID("_Front"),
            GainId = Shader.PropertyToID("_Gain"), ClockId = Shader.PropertyToID("_Clock"), ZTestId = Shader.PropertyToID("_ZTest");
        static Material[] mats; static bool matsTried;

        /// <summary>[0] hologram (pieces, outline, wreck), [1] energy fill (over the solid deck). Null = the shader is not here (the old look).</summary>
        internal static Material[] Materials()
        {
            if (mats != null && mats[0] && mats[1]) return mats;
            if (matsTried && mats == null) return null;
            matsTried = true;
            var src = Resources.Load<Material>(MaterialPath);
            var sh = src && src.shader && src.shader.name == ShaderName ? src.shader : Shader.Find(ShaderName);
            if (!sh || !sh.isSupported) { Debug.LogWarning($"[OpenZ] bridge restore: the shader {ShaderName} is missing or not supported here: the earlier restore look plays"); return null; }
            mats = new Material[2];
            for (int i = 0; i < 2; i++)
            {
                var m = src && src.shader == sh ? new Material(src) : new Material(sh);
                m.name = i == 0 ? "BridgeRestore_Holo" : "BridgeRestore_Energy";
                m.SetFloat(ModeId, i);
                m.SetFloat(ZTestId, (float)CompareFunction.LessEqual);
                m.renderQueue = (int)RenderQueue.Transparent + 41 + i;
                mats[i] = m;
            }
            return mats;
        }

        // ------------------------------------------------------------------ the cut (also used by the no-Play check on the prefabs)
        internal struct Vtx { public Vector3 p, n; public Vector4 t; public Vector2 uv, uv1; public Color c; }

        public sealed class PieceData
        {
            public int along, across, rank;            // slice index from one end, 0 / 1 = the half, rank = slices from the nearer end
            public Vector3 pivot;                      // world, the piece's middle
            public Vector4 cut;                        // along min / max, across min / max in the piece's own coordinates (m)
            public float deckY;                        // world height of its deck top (area-weighted up faces)
            public Vector3 joint;                      // world: the middle of its seam toward the nearer end
            public float area;
            public Mesh solid, fx;                     // solid: one submesh per material of `materials`; fx: every triangle in one submesh
            public Material[] materials;
        }

        public sealed class CutResult
        {
            public List<PieceData> pieces = new List<PieceData>();
            public Vector3 centre, axis, axisB; public float half, width, areaIn, areaOut, ms; public int slices, tris;
            public bool done; public float stepMaxMs; public int steps;   // the sliced cut: finished, its longest share of one frame, frames used
        }

        /// <summary>Cuts every mesh renderer under `root` (its world pose now) into slices along its longer flat side and halves across.
        /// Null when there is nothing to cut.</summary>
        public static CutResult Cut(GameObject root, float sliceM)
        {
            var res = new CutResult(); var job = CutJob(root, sliceM, res, double.MaxValue);
            while (job.MoveNext()) { }
            return res.done ? res : null;
        }

        /// <summary>The same cut spread over frames: it yields once `budgetMs` of this frame are used (the host starts it the moment the
        /// bridge is destroyed, so the restore never waits and no frame carries the whole cut). `res.done` once finished.</summary>
        public static System.Collections.IEnumerator CutJob(GameObject root, float sliceM, CutResult res, double budgetMs)
        {
            if (!root) yield break;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var mfs = root.GetComponentsInChildren<MeshFilter>(true);
            var src = new List<(Mesh m, Matrix4x4 M, Material[] mt)>();
            var b = new Bounds(); bool any = false;
            foreach (var mf in mfs)
            {
                var r = mf.GetComponent<MeshRenderer>(); var m = mf.sharedMesh;
                if (!r || !r.enabled || !m || !m.isReadable || m.vertexCount == 0 || !ShownUnder(mf.transform, root.transform)) continue;
                var M = mf.transform.localToWorldMatrix; src.Add((m, M, r.sharedMaterials));
                var vs = m.vertices;
                foreach (var p in vs) { var w = M.MultiplyPoint3x4(p); if (!any) { b = new Bounds(w, Vector3.zero); any = true; } else b.Encapsulate(w); }
                if (sw.Elapsed.TotalMilliseconds > budgetMs)
                {
                    float used = (float)sw.Elapsed.TotalMilliseconds; res.ms += used; res.stepMaxMs = Mathf.Max(res.stepMaxMs, used); res.steps++;
                    yield return null;
                    sw.Restart();
                }
            }
            if (!any) yield break;
            bool alongX = b.size.x >= b.size.z;
            res.axis = alongX ? Vector3.right : Vector3.forward; res.axisB = alongX ? Vector3.forward : Vector3.right;
            res.centre = b.center; res.half = (alongX ? b.size.x : b.size.z) * 0.5f; res.width = alongX ? b.size.z : b.size.x;
            int n = Mathf.Clamp(Mathf.RoundToInt(res.half * 2f / Mathf.Max(0.5f, sliceM)), 3, 15); if (n % 2 == 0) n++;   // odd: one middle slice locks last
            res.slices = n;
            float step = res.half * 2f / n, a0 = -res.half;
            // gather: per cell (along i, across j): vertices + per material index triangles
            var allMats = new List<Material>();
            var cells = new Dictionary<int, (List<Vtx> v, Dictionary<int, List<int>> tri)>();
            var poly = new List<Vtx>(8); var tmp = new List<Vtx>(8);
            foreach (var s in src)
            {
                if (sw.Elapsed.TotalMilliseconds > budgetMs)
                {
                    float used = (float)sw.Elapsed.TotalMilliseconds; res.ms += used; res.stepMaxMs = Mathf.Max(res.stepMaxMs, used); res.steps++;
                    yield return null;
                    sw.Restart();
                }
                var m = s.m; var M = s.M; var N = M.inverse.transpose;
                var pv = m.vertices; var pn = m.normals; var pt = m.tangents; var u0 = m.uv; var u1 = m.uv2; var pc = m.colors;
                bool hn = pn != null && pn.Length == pv.Length, ht = pt != null && pt.Length == pv.Length, h0 = u0 != null && u0.Length == pv.Length,
                     h1 = u1 != null && u1.Length == pv.Length, hc = pc != null && pc.Length == pv.Length;
                var wv = new Vtx[pv.Length];
                for (int i = 0; i < pv.Length; i++)
                {
                    var x = new Vtx { p = M.MultiplyPoint3x4(pv[i]), c = hc ? pc[i] : Color.white };
                    x.n = hn ? N.MultiplyVector(pn[i]).normalized : Vector3.up;
                    if (ht) { var t3 = M.MultiplyVector(new Vector3(pt[i].x, pt[i].y, pt[i].z)).normalized; x.t = new Vector4(t3.x, t3.y, t3.z, pt[i].w); } else x.t = new Vector4(1, 0, 0, 1);
                    x.uv = h0 ? u0[i] : Vector2.zero; x.uv1 = h1 ? u1[i] : Vector2.zero;
                    wv[i] = x;
                }
                bool flip = M.determinant < 0f;
                for (int sm = 0; sm < m.subMeshCount; sm++)
                {
                    if (m.GetTopology(sm) != MeshTopology.Triangles) continue;
                    var mat = s.mt != null && s.mt.Length > 0 ? s.mt[Mathf.Min(sm, s.mt.Length - 1)] : null;
                    if (!mat) continue;
                    int mi = allMats.IndexOf(mat); if (mi < 0) { mi = allMats.Count; allMats.Add(mat); }
                    var tris = m.GetTriangles(sm);
                    for (int k = 0; k + 2 < tris.Length; k += 3)
                    {
                        var A = wv[tris[k]]; var B = wv[tris[k + (flip ? 2 : 1)]]; var C = wv[tris[k + (flip ? 1 : 2)]];
                        if ((k & 63) == 0 && sw.Elapsed.TotalMilliseconds > budgetMs)
                        {
                            float used = (float)sw.Elapsed.TotalMilliseconds; res.ms += used; res.stepMaxMs = Mathf.Max(res.stepMaxMs, used); res.steps++;
                            yield return null;
                            sw.Restart();
                        }
                        res.tris++; res.areaIn += Area(A.p, B.p, C.p);
                        float la = Along(res, A.p), lb = Along(res, B.p), lc = Along(res, C.p);
                        int i0 = Cell(Mathf.Min(la, Mathf.Min(lb, lc)), a0, step, n), i1 = Cell(Mathf.Max(la, Mathf.Max(lb, lc)), a0, step, n);
                        for (int ci = i0; ci <= i1; ci++)
                            for (int cj = 0; cj < 2; cj++)
                            {
                                poly.Clear(); poly.Add(A); poly.Add(B); poly.Add(C);
                                if (ci > 0) Clip(poly, tmp, res, true, a0 + ci * step, +1f);
                                if (ci < n - 1 && poly.Count >= 3) Clip(poly, tmp, res, true, a0 + (ci + 1) * step, -1f);
                                if (poly.Count >= 3) Clip(poly, tmp, res, false, 0f, cj == 0 ? -1f : +1f);
                                if (poly.Count < 3) continue;
                                int key = ci * 2 + cj;
                                if (!cells.TryGetValue(key, out var cell)) { cell = (new List<Vtx>(), new Dictionary<int, List<int>>()); cells[key] = cell; }
                                if (!cell.tri.TryGetValue(mi, out var tl)) { tl = new List<int>(); cell.tri[mi] = tl; }
                                int bse = cell.v.Count; cell.v.AddRange(poly);
                                for (int q = 1; q + 1 < poly.Count; q++) { tl.Add(bse); tl.Add(bse + q); tl.Add(bse + q + 1); }
                            }
                    }
                }
            }
            var cellKeys = new List<int>(cells.Keys); cellKeys.Sort();
            foreach (var key in cellKeys)
            {
                if (sw.Elapsed.TotalMilliseconds > budgetMs)
                {
                    float used = (float)sw.Elapsed.TotalMilliseconds; res.ms += used; res.stepMaxMs = Mathf.Max(res.stepMaxMs, used); res.steps++;
                    yield return null;
                    sw.Restart();
                }
                var kv = new KeyValuePair<int, (List<Vtx> v, Dictionary<int, List<int>> tri)>(key, cells[key]);
                int ci = kv.Key / 2, cj = kv.Key % 2; var vs = kv.Value.v;
                if (vs.Count < 3) continue;
                var pb = new Bounds(vs[0].p, Vector3.zero); foreach (var x in vs) pb.Encapsulate(x.p);
                var pd = new PieceData { along = ci, across = cj, rank = Mathf.Min(ci, n - 1 - ci), pivot = pb.center };
                float pa = Along(res, pd.pivot), pbx = Across(res, pd.pivot);
                pd.cut = new Vector4(ci > 0 ? a0 + ci * step - pa : -10000f, ci < n - 1 ? a0 + (ci + 1) * step - pa : 10000f, cj == 1 ? -pbx : -10000f, cj == 0 ? -pbx : 10000f);
                var verts = new List<Vector3>(vs.Count); var nor = new List<Vector3>(vs.Count); var tan = new List<Vector4>(vs.Count);
                var uv0 = new List<Vector2>(vs.Count); var uv1 = new List<Vector2>(vs.Count); var col = new List<Color>(vs.Count);
                foreach (var x in vs) { verts.Add(x.p - pd.pivot); nor.Add(x.n); tan.Add(x.t); uv0.Add(x.uv); uv1.Add(x.uv1); col.Add(x.c); }
                var solid = new Mesh { name = "OZ_BridgePiece_" + ci + "_" + cj, indexFormat = vs.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                solid.SetVertices(verts); solid.SetNormals(nor); solid.SetTangents(tan); solid.SetUVs(0, uv0); solid.SetUVs(1, uv1); solid.SetColors(col);
                var keys = new List<int>(kv.Value.tri.Keys); keys.Sort();
                solid.subMeshCount = keys.Count; pd.materials = new Material[keys.Count];
                var all = new List<int>();
                float upA = 0f, upY = 0f;
                for (int q = 0; q < keys.Count; q++)
                {
                    var tl = kv.Value.tri[keys[q]]; solid.SetTriangles(tl, q, false); pd.materials[q] = allMats[keys[q]]; all.AddRange(tl);
                    for (int t = 0; t + 2 < tl.Count; t += 3)
                    {
                        var P = vs[tl[t]].p; var Q = vs[tl[t + 1]].p; var R = vs[tl[t + 2]].p;
                        float ar = Area(P, Q, R); pd.area += ar;
                        var fn = Vector3.Cross(Q - P, R - P);
                        if (fn.sqrMagnitude > 1e-10f && fn.normalized.y > 0.85f) { upA += ar; upY += ar * (P.y + Q.y + R.y) / 3f; }
                    }
                }
                solid.RecalculateBounds();
                var fx = new Mesh { name = solid.name + "_fx", indexFormat = solid.indexFormat };
                fx.SetVertices(verts); fx.SetNormals(nor); fx.SetTriangles(all, 0); fx.RecalculateBounds();
                pd.solid = solid; pd.fx = fx;
                pd.deckY = upA > 0.01f ? upY / upA : pb.max.y;
                float jointA = (ci <= (n - 1) / 2) ? a0 + ci * step : a0 + (ci + 1) * step;                 // the seam toward its own end
                if (ci == (n - 1) / 2) jointA = a0 + ci * step;
                float midB = cj == 0 ? -res.width * 0.25f : res.width * 0.25f;
                pd.joint = res.centre + res.axis * jointA + res.axisB * midB; pd.joint.y = pd.deckY;
                res.areaOut += pd.area;
                res.pieces.Add(pd);
            }
            res.pieces.Sort((x, y) => x.rank != y.rank ? x.rank.CompareTo(y.rank) : x.along != y.along ? x.along.CompareTo(y.along) : x.across.CompareTo(y.across));
            float last = (float)sw.Elapsed.TotalMilliseconds; res.ms += last; res.stepMaxMs = Mathf.Max(res.stepMaxMs, last); res.steps++;
            res.done = true;
        }

        /// <summary>Every object between t and root (root itself excluded: the intact model waits switched off) is switched on.</summary>
        internal static bool ShownUnder(Transform t, Transform root)
        {
            for (var x = t; x && x != root; x = x.parent) if (!x.gameObject.activeSelf) return false;
            return true;
        }

        static float Area(Vector3 a, Vector3 b, Vector3 c) => Vector3.Cross(b - a, c - a).magnitude * 0.5f;
        static float Along(CutResult r, Vector3 p) => Vector3.Dot(p - r.centre, r.axis);
        static float Across(CutResult r, Vector3 p) => Vector3.Dot(p - r.centre, r.axisB);
        static int Cell(float a, float a0, float step, int n) => Mathf.Clamp(Mathf.FloorToInt((a - a0) / step), 0, n - 1);

        /// <summary>Keeps the part of the polygon where sign x (coordinate - at) >= 0 (Sutherland-Hodgman, every attribute interpolated).</summary>
        static void Clip(List<Vtx> poly, List<Vtx> tmp, CutResult r, bool along, float at, float sign)
        {
            tmp.Clear();
            int c = poly.Count;
            for (int i = 0; i < c; i++)
            {
                var P = poly[i]; var Q = poly[(i + 1) % c];
                float dp = sign * ((along ? Along(r, P.p) : Across(r, P.p)) - at), dq = sign * ((along ? Along(r, Q.p) : Across(r, Q.p)) - at);
                if (dp >= 0f) tmp.Add(P);
                if ((dp >= 0f) != (dq >= 0f)) tmp.Add(Lerp(P, Q, dp / (dp - dq)));
            }
            poly.Clear(); poly.AddRange(tmp);
        }

        static Vtx Lerp(Vtx a, Vtx b, float k) => new Vtx
        {
            p = Vector3.LerpUnclamped(a.p, b.p, k), n = Vector3.LerpUnclamped(a.n, b.n, k).normalized, t = Vector4.LerpUnclamped(a.t, b.t, k),
            uv = Vector2.LerpUnclamped(a.uv, b.uv, k), uv1 = Vector2.LerpUnclamped(a.uv1, b.uv1, k), c = Color.LerpUnclamped(a.c, b.c, k)
        };
    }

    /// <summary>The scene's restore runs (one per bridge) and their cut pieces (kept for the next time that bridge comes back).</summary>
    public sealed class OzBridgeRestoreHost : MonoBehaviour
    {
        readonly Dictionary<int, OzBridgeRestore.CutResult> cuts = new Dictionary<int, OzBridgeRestore.CutResult>();
        readonly Dictionary<int, Run> runs = new Dictionary<int, Run>();
        readonly Dictionary<int, float> soundTaken = new Dictionary<int, float>();
        readonly List<int> done = new List<int>();
        readonly HashSet<int> failed = new HashSet<int>();
        readonly Dictionary<int, (System.Collections.IEnumerator job, OzBridgeRestore.CutResult res, ZObject o)> jobs = new Dictionary<int, (System.Collections.IEnumerator, OzBridgeRestore.CutResult, ZObject)>();
        readonly List<int> jobsDone = new List<int>();

        // The restore's sounds (the bed, the energy rise, the seams' clanks, the last lock, the ring) are ONE group under one gain:
        // it fades in over SoundFadeIn s from the restore's start, stays, and fades out over SoundFadeOut s from the moment the bridge stands
        // (the end of the animation's span); a restore that is cut short (the bridge destroyed again) fades out over SoundFadeCut s from
        // where it is. A second bridge that starts its restore while a group is still at full level shares that group: no second bed /
        // energy / ring (its own clanks and its lock still sound), and the group holds until the later bridge stands.
        internal sealed class SoundGroup
        {
            public int id, sharers; public string bed; public ZGame game; public double startSim, endSim;
            public float gain, cutAt = -1f, cutGain, startReal; public Vector3 at;
        }
        readonly List<SoundGroup> sounds = new List<SoundGroup>();
        static int soundSerial;
        internal float lastLockAt = -100f;

        internal SoundGroup SharedSound(ZGame g)
        {
            foreach (var s in sounds) if (s.cutAt < 0f && s.game == g && g != null && g.World != null && g.World.Time < s.endSim) return s;
            return null;
        }

        internal SoundGroup NewSound(ZGame g, int bridgeId, double endSim, Vector3 at)
        {
            var s = new SoundGroup { id = ++soundSerial, game = g, startSim = g.World.Time, endSim = endSim, at = at, startReal = Time.time };
            s.bed = "bridgerestore:" + bridgeId + ":" + s.id;
            sounds.Add(s); OzBridgeRestore.SoundGroups++;
            return s;
        }

        internal void CutSound(SoundGroup s)
        {
            if (s == null || s.cutAt >= 0f) return;
            s.cutAt = Time.time; s.cutGain = s.gain; OzBridgeRestore.SoundCuts++;
            OzSoundLog.Add("BR", "bridge_restore_sound", null, $"group {s.id} cut short at gain {s.gain:0.00}: fades out in {OzBridgeRestore.SoundFadeCut:0.00} s", 0f, s.at);
        }

        void StepSound()
        {
            for (int i = sounds.Count - 1; i >= 0; i--)
            {
                var s = sounds[i]; float g; bool over;
                if (s.cutAt < 0f && (s.game == null || s.game.World == null)) { s.cutAt = Time.time; s.cutGain = s.gain; }
                if (s.cutAt >= 0f)
                {
                    float k = (Time.time - s.cutAt) / Mathf.Max(0.05f, OzBridgeRestore.SoundFadeCut);
                    g = s.cutGain * Mathf.Clamp01(1f - k); over = k >= 1f;
                }
                else
                {
                    double now = s.game.World.Time;
                    float fin = Mathf.Clamp01((float)(now - s.startSim) / Mathf.Max(0.02f, OzBridgeRestore.SoundFadeIn));
                    float k = (float)(now - s.endSim) / Mathf.Max(0.05f, OzBridgeRestore.SoundFadeOut);
                    g = fin * Mathf.Clamp01(1f - k); over = k >= 1f;
                }
                s.gain = g;
                if (AudioDirector.I) AudioDirector.I.HeldGain(s.bed, g);
                if (CombatAudio.I) CombatAudio.I.GroupGain(s.id, over ? 0f : Mathf.Max(g, 0.0001f));
                if (!over) continue;
                if (AudioDirector.I) AudioDirector.I.HeldStop(s.bed);
                OzSoundLog.Add("BR", "bridge_restore_sound", null, $"group {s.id} over after {Time.time - s.startReal:0.00} s ({(s.cutAt >= 0f ? "cut short" : "faded out")})", 0f, s.at);
                sounds.RemoveAt(i);
            }
        }

        void EndAllSound()
        {
            foreach (var s in sounds)
            {
                if (AudioDirector.I) AudioDirector.I.HeldStop(s.bed);
                if (CombatAudio.I) CombatAudio.I.GroupGain(s.id, 0f);
            }
            sounds.Clear();
        }


        internal bool Running(int id) => runs.ContainsKey(id);
        internal bool Prepared(int id) => cuts.ContainsKey(id);
        internal bool Failed(int id) => failed.Contains(id);
        internal bool SoundTaken(int id) => soundTaken.TryGetValue(id, out var t) && Time.time - t < 6f;

        /// <summary>Starts the bridge's cut, a share of each frame (<see cref="OzBridgeRestore.CutBudgetMs"/>), from the moment it is down.</summary>
        internal void StartPrepare(ZObject o, GameObject[] v)
        {
            if (cuts.ContainsKey(o.Id) || jobs.ContainsKey(o.Id) || failed.Contains(o.Id)) return;
            var intact = v != null && v.Length > 0 ? v[0] : null;
            if (!intact) return;
            var res = new OzBridgeRestore.CutResult();
            jobs[o.Id] = (OzBridgeRestore.CutJob(intact, OzBridgeRestore.SliceM, res, Mathf.Max(0.3f, OzBridgeRestore.CutBudgetMs)), res, o);
        }

        /// <summary>The cut ready now (a cut still running finishes in this frame; none started: the whole cut now).</summary>
        internal bool Prepare(ZGame g, ZObject o, GameObject[] v)
        {
            if (cuts.ContainsKey(o.Id)) return true;
            if (failed.Contains(o.Id) || OzBridgeRestore.Materials() == null) return false;
            if (!jobs.ContainsKey(o.Id)) StartPrepare(o, v);
            if (!jobs.TryGetValue(o.Id, out var j)) return false;
            OzBridgeRestore.SyncCuts++;
            bool more = true;
            try { while (more) more = j.job.MoveNext(); }
            catch (System.Exception e) { Debug.LogWarning($"[OpenZ] bridge restore: cutting bridge #{o.Id} failed ({e.Message}): the earlier restore look plays"); }
            jobs.Remove(o.Id);
            return Finish(o, j.res);
        }

        bool Finish(ZObject o, OzBridgeRestore.CutResult c)
        {
            if (c == null || !c.done || c.pieces.Count == 0) { OzBridgeRestore.BuildFails++; failed.Add(o.Id); return false; }
            cuts[o.Id] = c;
            OzBridgeRestore.PiecesBuilt += c.pieces.Count; OzBridgeRestore.BuildMsMax = Mathf.Max(OzBridgeRestore.BuildMsMax, c.ms);
            OzBridgeRestore.StepMsMax = Mathf.Max(OzBridgeRestore.StepMsMax, c.stepMaxMs);
            Debug.Log($"[OpenZ] bridge restore: bridge #{o.Id} ({o.Type}) cut into {c.pieces.Count} pieces ({c.slices} slices x 2), {c.tris} triangles, "
                      + $"{c.half * 2f:0.0} x {c.width:0.0} m, area {c.areaIn:0.0} -> {c.areaOut:0.0} m2, {c.ms:0.0} ms over {c.steps} frame(s), {c.stepMaxMs:0.0} ms at most in one");
            return true;
        }

        void StepJobs()
        {
            if (jobs.Count == 0) return;
            jobsDone.Clear();
            foreach (var kv in jobs)
            {
                bool more;
                try { more = kv.Value.job.MoveNext(); }
                catch (System.Exception e) { Debug.LogWarning($"[OpenZ] bridge restore: cutting bridge #{kv.Key} failed ({e.Message}): the earlier restore look plays"); more = false; }
                if (!more) jobsDone.Add(kv.Key);
                break;                                                                                   // one bridge's share a frame
            }
            foreach (var id in jobsDone) { var j = jobs[id]; jobs.Remove(id); Finish(j.o, j.res); }
        }

        /// <summary>left > 0: the rules repair it in `left` s (the animation ends then); -1: they repaired it now (it runs from now).</summary>
        internal bool Begin(ZGame g, ZObject o, GameObject[] v, float left)
        {
            if (runs.ContainsKey(o.Id)) return true;
            if (!Prepare(g, o, v)) return false;
            var r = new Run();
            if (!r.Start(this, g, o, v, cuts[o.Id], left)) { r.Dispose(); return false; }
            runs[o.Id] = r; soundTaken[o.Id] = Time.time;
            OzBridgeRestore.Runs++; if (left > 0f) OzBridgeRestore.PreRolls++; else OzBridgeRestore.PostRolls++;
            OzBridgeRestore.Last = $"#{o.Id} {o.Type} {OzBridgeKit.Biome(g.level)}: {(left > 0f ? $"{left:0.00} s before the rules' repair" : "after the repair")} at game time {g.World.Time:F2}";
            Debug.Log("[OpenZ] bridge restore: " + OzBridgeRestore.Last);
            return true;
        }

        internal void Repaired(int id) { if (runs.TryGetValue(id, out var r)) r.Repaired(); }
        internal void Abort(int id) { if (runs.TryGetValue(id, out var r)) { r.Dispose(); runs.Remove(id); OzBridgeRestore.Aborted++; } }

        void LateUpdate()
        {
            StepJobs();
            done.Clear();
            foreach (var kv in runs) if (!kv.Value.Step()) done.Add(kv.Key);
            foreach (var id in done) { runs[id].Dispose(); runs.Remove(id); }
            StepSound();   // 1.5 kit crane_repair_look_r6 (A9): the restore sound groups' fade in / fade out
        }

        void OnDestroy()
        {
            foreach (var r in runs.Values) r.Dispose();
            runs.Clear();
            EndAllSound();   // 1.5 kit crane_repair_look_r6 (A9)
            foreach (var c in cuts.Values) foreach (var p in c.pieces) { if (p.solid) Destroy(p.solid); if (p.fx) Destroy(p.fx); }
            cuts.Clear();
            foreach (var j in jobs.Values) foreach (var p in j.res.pieces) { if (p.solid) Destroy(p.solid); if (p.fx) Destroy(p.fx); }
            jobs.Clear();
        }

        // ------------------------------------------------------------------ one restore
        sealed class Piece
        {
            public OzBridgeRestore.PieceData d; public GameObject go, ghost; public Renderer solid, fx, ghostR;
            public Vector3 from; public Quaternion r0; public float t0, t1, arc, seed; public bool landed, last;
        }

        sealed class Run
        {
            ZGame game; ZObject o; OzBridgeRestore.CutResult cut; GameObject root; GameObject wreck; GameObject intact;
            readonly List<Piece> pieces = new List<Piece>();
            readonly List<Renderer> wreckLight = new List<Renderer>();                                 // copies of the wreck's meshes drawn as light (no colliders)
            readonly List<Renderer> wreckHidden = new List<Renderer>();
            readonly List<Renderer> intactHidden = new List<Renderer>();
            readonly List<Material> cullOff = new List<Material>();
            MaterialPropertyBlock mpb;
            double simT0, simRepaired = -1, rushFrom = -1; float rushT = -1f, lockAt, lastT; bool postRoll, repaired, intactShown, energyPlayed, lockDone;
            readonly List<(float t, float f)> frontKeys = new List<(float, float)>();
            Color dust; bool jungle;
            OzBridgeRestoreHost.SoundGroup snd; bool sndShared; OzBridgeRestoreHost sndHost;   // 1.5 kit crane_repair_look_r6 (A9): this restore's sound group (its own, or another bridge's it shares)

            float T => (float)(game.World.Time - simT0);

            internal bool Start(OzBridgeRestoreHost host, ZGame g, ZObject obj, GameObject[] v, OzBridgeRestore.CutResult c, float left)
            {
                game = g; o = obj; cut = c; mpb = new MaterialPropertyBlock();
                var mats = OzBridgeRestore.Materials(); if (mats == null) return false;
                float span = Mathf.Clamp(OzBridgeRestore.Span, 1f, 2f);
                postRoll = left <= 0f;
                simT0 = postRoll ? g.World.Time : g.World.Time - (span - left);
                intact = v.Length > 0 ? v[0] : null; wreck = v.Length > 2 ? v[2] : null;
                if (wreck == intact) wreck = null;
                jungle = g.level == "L16";
                var biome = OzBridgeKit.Biome(g.level);
                dust = biome == "city" ? new Color(0.6f, 0.58f, 0.55f) : biome == "jungle" ? new Color(0.5f, 0.42f, 0.32f) : new Color(0.78f, 0.66f, 0.5f);
                OzRuinFires.End(o.Id);                                                                   // the deck comes back: no fire under it
                root = new GameObject("OZ_BridgeRestore_" + o.Id);
                root.transform.SetParent(host.transform, false);
                // the timetable: ranks from the ends land between 0.3 and 0.88 of the span, the middle last
                int ranks = (cut.slices + 1) / 2;
                lockAt = 0.88f * span;
                float firstLand = 0.3f * span;
                foreach (var pd in cut.pieces)
                {
                    var p = new Piece { d = pd, seed = Random.value };
                    float land = ranks <= 1 ? lockAt : Mathf.Lerp(firstLand, lockAt, pd.rank / (float)(ranks - 1));
                    bool middle = pd.rank == ranks - 1;
                    if (!middle) land += (pd.along < cut.slices / 2 ? -0.025f : 0.025f) + (pd.across == 0 ? -0.015f : 0.015f);
                    p.last = middle;
                    float fl = OzBridgeRestore.Flight * Random.Range(0.92f, 1.08f);
                    p.t1 = land; p.t0 = Mathf.Max(0.02f, land - fl);
                    float side = pd.across == 0 ? -1f : 1f;
                    float toEnd = pd.along < (cut.slices - 1) * 0.5f ? -1f : pd.along > (cut.slices - 1) * 0.5f ? 1f : 0f;
                    p.from = pd.pivot + cut.axisB * side * Random.Range(2.6f, 4.2f) + Vector3.down * Random.Range(1.8f, 3.0f) + cut.axis * toEnd * Random.Range(0.3f, 1.2f);
                    p.r0 = Quaternion.Euler(Random.Range(-38f, 38f), Random.Range(-55f, 55f), Random.Range(-38f, 38f));
                    p.arc = Random.Range(1.3f, 2.2f);
                    p.go = new GameObject("piece_" + pd.along + "_" + pd.across); p.go.transform.SetParent(root.transform, false);
                    p.go.transform.SetPositionAndRotation(p.from, p.r0);
                    p.solid = Part(p.go, "solid", pd.solid, CullOff(pd.materials), true); p.solid.enabled = false;
                    p.fx = Part(p.go, "fx", pd.fx, new[] { mats[0] }, false); p.fx.enabled = false;
                    p.ghost = new GameObject("ghost_" + pd.along + "_" + pd.across); p.ghost.transform.SetParent(root.transform, false);
                    p.ghost.transform.SetPositionAndRotation(pd.pivot, Quaternion.identity);
                    p.ghostR = Part(p.ghost, "fx", pd.fx, new[] { mats[0] }, false);
                    pieces.Add(p);
                }
                // the fill front: from 0 to each rank's far edge as it lands (u: 0 at the ends, 1 in the middle)
                frontKeys.Add((0.12f * span, 0f));
                for (int k = 0; k < ranks; k++)
                {
                    float land = ranks <= 1 ? lockAt : Mathf.Lerp(firstLand, lockAt, k / (float)(ranks - 1));
                    frontKeys.Add((land, Mathf.Min(1.04f, 2f * (k + 1) / cut.slices)));
                }
                // the wreck turns into light: copies of its meshes drawn by the hologram (a crane's repair has already switched the wreck off;
                // before the rules' repair the wreck's own renderers are hidden until the rules swap it out, then put back as they were)
                if (wreck)
                    foreach (var r in wreck.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var mf = r.GetComponent<MeshFilter>();
                        if (!r.enabled || !mf || !mf.sharedMesh || !OzBridgeRestore.ShownUnder(r.transform, wreck.transform)) continue;
                        var copy = new GameObject("wreck_light"); copy.transform.SetParent(root.transform, false);
                        copy.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation); copy.transform.localScale = r.transform.lossyScale;
                        var hm = new Material[Mathf.Max(1, mf.sharedMesh.subMeshCount)]; for (int i = 0; i < hm.Length; i++) hm[i] = mats[0];
                        wreckLight.Add(Part(copy, "fx", mf.sharedMesh, hm, false));
                        if (wreck.activeInHierarchy) { r.enabled = false; wreckHidden.Add(r); }
                    }
                if (postRoll) HideIntact();
                // the sounds: the rebuild bed and the energy rising
                var at = cut.centre;
                StartSound(host, at, span);   // 1.5 kit crane_repair_look_r6 (A9): the bed + the energy rise in one group with a fade in / fade out (was: two plain one-shots at full level)
                var fx = OpenZFxPlayer.I;
                if (fx) fx.AddLight(at + Vector3.up * 1.5f, OrangeLight, 3f, Mathf.Max(12f, cut.half * 1.4f), 0.5f, 2.2f, 0.15f, false);
                return true;
            }

            static readonly Color OrangeLight = new Color(1f, 0.55f, 0.18f), Spark = new Color(1f * 3f, 0.62f * 3f, 0.2f * 3f, 1f);

            // ---- 1.5 kit crane_repair_look_r6 (A9): this restore's sounds in one group (OzBridgeRestoreHost.SoundGroup)
            void StartSound(OzBridgeRestoreHost h, Vector3 at, float span)
            {
                string bedId = jungle && AudioDirector.Has("bld_bridge_rebuild_wood") ? "bld_bridge_rebuild_wood" : "bld_bridge_rebuild";
                var au = CombatAudio.I;
                if (!OzBridgeRestore.SoundFades)
                {   // the switch off: plain one-shots, no group
                    if (AudioDirector.I) AudioDirector.Fire(bedId, at, o.Id, OzBridgeRestore.BedVol);
                    if (au) au.PlayPitched("br_restore_energy", at, OzBridgeRestore.EnergyVol, 0f, o.Id, 1f, "br_restore_energy_a");
                    return;
                }
                sndHost = h; double endSim = simT0 + span;
                var shared = h.SharedSound(game);
                if (shared != null)
                {   // another bridge's restore sounds at full level now: one bed, one energy rise, one ring for both
                    snd = shared; sndShared = true; shared.sharers++; if (endSim > shared.endSim) shared.endSim = endSim;
                    OzBridgeRestore.SoundShared++;
                    OzSoundLog.Add("BR", "bridge_restore_sound", null, $"bridge #{o.Id} shares group {shared.id}: no second bed / energy / ring", 0f, at);
                    return;
                }
                snd = h.NewSound(game, o.Id, endSim, at);
                if (AudioDirector.I) AudioDirector.I.HeldStart(snd.bed, bedId, at, OzBridgeRestore.BedVol, 0f);
                if (au) { au.PlayPitched("br_restore_energy", at, OzBridgeRestore.EnergyVol, 0f, o.Id, 1f, "br_restore_energy_a"); au.JoinGroup(snd.id, 0.0001f); }
                OzSoundLog.Add("BR", "bridge_restore_sound", null, $"group {snd.id} bridge #{o.Id}: fade in {OzBridgeRestore.SoundFadeIn:0.00} s, fade out {OzBridgeRestore.SoundFadeOut:0.00} s from the bridge standing, "
                               + $"levels bed {OzBridgeRestore.BedVol:0.00} energy {OzBridgeRestore.EnergyVol:0.00} clank {OzBridgeRestore.ClankVol:0.000} lock {OzBridgeRestore.LockVol:0.00} ring {OzBridgeRestore.RingVol:0.00}", 0f, at);
            }

            /// <summary>The one-shot CombatAudio just started joins this restore's group at its gain now.</summary>
            void Join(CombatAudio au) { if (snd != null && au) au.JoinGroup(snd.id, Mathf.Max(0.0001f, snd.gain)); }

            Renderer Part(GameObject parent, string name, Mesh m, Material[] mt, bool shadows)
            {
                var go = new GameObject(name); go.transform.SetParent(parent.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = mt;
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = shadows;
                r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                return r;
            }

            Material[] CullOff(Material[] src)
            {   // the cut faces of a landed piece stay closed to the eye until its neighbour lands: draw both sides
                var o2 = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    var m = src[i];
                    if (m && m.HasProperty("_Cull")) { var c = new Material(m) { name = m.name + "_restore" }; c.SetFloat("_Cull", 0f); cullOff.Add(c); o2[i] = c; }
                    else o2[i] = m;
                }
                return o2;
            }

            void HideIntact()
            {
                if (!intact) return;
                foreach (var r in intact.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; intactHidden.Add(r); }
            }

            void ShowIntact() { foreach (var r in intactHidden) if (r) r.enabled = true; intactHidden.Clear(); intactShown = true; }

            internal void Repaired()
            {
                if (repaired) return;
                repaired = true; simRepaired = game.World.Time;
                float span = Mathf.Clamp(OzBridgeRestore.Span, 1f, 2f);
                if (!postRoll && T < span - 0.3f)
                {   // repaired early (a crane during the last second): the rest lands in 0.25 s
                    rushFrom = simRepaired; rushT = T; OzBridgeRestore.Rushed++;
                    if (snd != null && !sndShared && snd.sharers == 0) snd.endSim = System.Math.Min(snd.endSim, simRepaired + 0.25);   // 1.5 kit crane_repair_look_r6 (A9): rushed: the bridge stands in 0.25 s, the sound fades out from then
                    HideIntact();
                }
            }

            float Clock()
            {
                float t = T, span = Mathf.Clamp(OzBridgeRestore.Span, 1f, 2f);
                if (rushT >= 0f)
                {
                    float dt = (float)(game.World.Time - rushFrom);
                    float k = (span - rushT) / 0.25f;
                    t = dt * k < span - rushT ? rushT + dt * k : span + (dt - (span - rushT) / k);
                }
                return t;
            }

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
                if (game == null || game.World == null || o == null) return false;
                if (o.Destroyed && (repaired || postRoll)) return false;                                // destroyed again: BuildingDeath shows it
                float span = Mathf.Clamp(OzBridgeRestore.Span, 1f, 2f);
                float t = Clock(); lastT = t;
                bool normal = !o.Destroyed;                                                              // the rules repaired it: the real deck is drawn
                if (!postRoll && !normal && t > span + 3f) return false;                                 // the rules never repaired it (a changed timer): stop quietly
                bool reduced = OzMotion.Reduced;
                // the wreck: light that flickers and fades in 0.5 s
                float wa = Mathf.Clamp01(1f - t / 0.5f) * 0.9f;
                if (!reduced && wa > 0f && Random.value < 0.18f) wa *= Random.Range(0.3f, 0.7f);
                foreach (var w in wreckLight)
                {
                    if (!w) continue;
                    w.enabled = wa > 0.001f;
                    if (!w.enabled) continue;
                    mpb.Clear(); Common(mpb, t, 0f); mpb.SetFloat(OzBridgeRestore.AlphaId, wa); mpb.SetFloat(OzBridgeRestore.HitId, Mathf.Clamp01(1f - t / 0.2f) * 0.6f);
                    mpb.SetVector(OzBridgeRestore.CutId, new Vector4(-1e4f, 1e4f, -1e4f, 1e4f));
                    w.SetPropertyBlock(mpb);
                }
                float front = Front(t);
                float gain = t <= span ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (t - span) / Mathf.Max(0.05f, OzBridgeRestore.FadeAfter));
                float flash = t >= lockAt ? Mathf.Exp(-(t - lockAt) * 6f) : 0f;
                float ghostA = Mathf.Clamp01(t / 0.25f) * 0.13f * (t > span ? 0f : 1f);                       // a faint outline only (first frames: 0.32 read as a finished orange bridge)
                foreach (var p in pieces)
                {
                    if (!p.go) continue;
                    float k = Mathf.Clamp01((t - p.t0) / Mathf.Max(0.05f, p.t1 - p.t0));
                    if (!p.landed && t >= p.t1) Land(p, normal);
                    if (p.landed)
                    {
                        float since = t - p.t1;
                        float settle = since < 0.14f ? -0.045f * Mathf.Sin(Mathf.PI * since / 0.14f) : 0f;   // a small settle as it locks
                        p.go.transform.SetPositionAndRotation(p.d.pivot + Vector3.up * settle, Quaternion.identity);
                        p.solid.enabled = !intactShown;
                        p.fx.enabled = gain > 0.001f;
                        mpb.Clear(); Common(mpb, t, front);
                        float hit = 0.55f * Mathf.Exp(-since * 9f) + (p.last ? flash : flash * 0.5f) * 0.6f;   // a short warm flash (first frames: flat white blocks)
                        mpb.SetFloat(OzBridgeRestore.HitId, Mathf.Clamp01(hit)); mpb.SetFloat(OzBridgeRestore.GainId, gain);
                        mpb.SetVector(OzBridgeRestore.CutId, p.d.cut);
                        p.fx.SetPropertyBlock(mpb);
                    }
                    else if (t >= p.t0)
                    {
                        float e = 1f - (1f - k) * (1f - k) * (1f - k);
                        var pos = Vector3.LerpUnclamped(p.from, p.d.pivot, e) + Vector3.up * Mathf.Sin(Mathf.PI * e) * p.arc;
                        float er = 1f - Mathf.Pow(1f - k, 4f);
                        p.go.transform.SetPositionAndRotation(pos, Quaternion.Slerp(p.r0, Quaternion.identity, er));
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
                // the normal bridge: the rules' deck is drawn (pre-roll: from its repair; after a crane: from the end of the span)
                if (postRoll || rushT >= 0f) { if (!intactShown && t >= span) ShowIntact(); }
                else if (normal) intactShown = true;
                if (intactShown) foreach (var p in pieces) if (p.solid) p.solid.enabled = false;
                return !(t > span + OzBridgeRestore.FadeAfter + 0.05f && normal && intactShown);
            }

            void Common(MaterialPropertyBlock b, float t, float front)
            {
                b.SetVector(OzBridgeRestore.AxisId, new Vector4(cut.axis.x, cut.axis.y, cut.axis.z, cut.half));
                b.SetVector(OzBridgeRestore.AxisBId, new Vector4(cut.axisB.x, cut.axisB.y, cut.axisB.z, 0f));
                b.SetVector(OzBridgeRestore.CentreId, cut.centre);
                float band = front >= 1f ? Mathf.Clamp01(1f - (lastT - lockAt) / 0.3f) : 1f;
                b.SetVector(OzBridgeRestore.FrontId, new Vector4(front, 0.09f, 0.15f, band));                // tint 0.15: the bridge's own deck reads under the energy
                b.SetFloat(OzBridgeRestore.ClockId, t);
                b.SetFloat(OzBridgeRestore.GainId, 1f);
            }

            void Land(Piece p, bool normal)
            {
                p.landed = true; OzBridgeRestore.Landings++;
                p.solid.enabled = !intactShown;
                var mats = OzBridgeRestore.Materials(); if (mats != null) p.fx.sharedMaterials = new[] { mats[1] };
                var fx = OpenZFxPlayer.I; var au = CombatAudio.I;
                var j = p.d.joint;
                // a little proper dust along the seam: low, irregular, quick (the levels' lit dust, never a round puff)
                int puffs = p.last ? 4 : 2;
                for (int i = 0; i < puffs; i++)
                {
                    var at = j + cut.axisB * Random.Range(-0.45f, 0.45f) * cut.width * 0.5f + Vector3.up * 0.08f;
                    var vel = new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0.25f, 0.55f), Random.Range(-0.35f, 0.35f));
                    if (OzLevelDust.Emit(at, vel, Random.Range(0.35f, 0.6f), Random.Range(0.55f, 0.9f), Random.Range(0.32f, 0.45f), dust, 1f)) OzBridgeRestore.DustPuffs++;
                }
                if (fx)
                {
                    int n = p.last ? 14 : 4;
                    for (int i = 0; i < n; i++)
                    {
                        var at = j + cut.axisB * Random.Range(-0.5f, 0.5f) * cut.width * 0.5f + Vector3.up * 0.1f;
                        var v = Random.insideUnitSphere * (p.last ? 3.5f : 2f) + Vector3.up * Random.Range(1f, 2.5f);
                        fx.EmitChannel("wv2_spark", at, v, Random.Range(0.2f, 0.45f), Random.Range(0.035f, 0.06f), Spark, 1f);
                        OzBridgeRestore.Sparks++;
                    }
                    if (p.last) fx.AddLight(j + Vector3.up * 1.2f, OrangeLight, 5f, 14f, 0.45f, 2.2f, 0.1f, true);
                }
                // the sounds: one clank per seam (the two halves of a slice land together: one of them speaks), a heavier one + the ring last
                if (p.last)
                {
                    if (p.d.across != 0 || lockDone) return;
                    lockDone = true; OzBridgeRestore.Locks++;
                    var c = cut.centre; c.y = p.d.deckY;
                    if (au)
                    {
                        bool second = sndHost != null && Time.time - sndHost.lastLockAt < 0.6f;   // 1.5 kit crane_repair_look_r6 (A9): two bridges lock within 0.6 s: the second lock at half level
                        if (sndHost != null) sndHost.lastLockAt = Time.time;
                        au.PlayPitched("br_restore_lock", c, OzBridgeRestore.LockVol * (second ? 0.5f : 1f), 0f, o.Id, 0.9f, OzSoundV3.BankMetalHeavy); Join(au);
                        if (!sndShared) { au.PlayPitched("br_restore_ring", c, OzBridgeRestore.RingVol, 0f, o.Id, 1f, "br_restore_ring_a"); Join(au); }   // 1.5 kit crane_repair_look_r6 (A9): one ring for a shared group
                    }
                    if (jungle && AudioDirector.I) AudioDirector.Fire("bld_bridge_plank_knock", c, o.Id, 1f, 0.8f);
                    return;
                }
                if (p.d.across != 0) return;
                OzBridgeRestore.Clanks++;
                if (jungle && AudioDirector.I) AudioDirector.Fire("bld_bridge_plank_knock", j, -1, 0.9f, Random.Range(0.95f, 1.1f));
                else if (au) { au.PlayPitched("br_restore_clank", j, OzBridgeRestore.ClankVol, 0f, -1, Random.Range(1.15f, 1.35f), "sd_tank_breech_clank_a", "sd_tank_breech_clank_b", "final-metal-land-a", "final-metal-land-b"); Join(au); }   // 1.5 kit crane_repair_look_r6 (A9): in the restore's group
            }

            internal void Dispose()
            {
                if (snd != null && sndShared) snd.sharers--;   // 1.5 kit crane_repair_look_r6 (A9)
                else if (snd != null && sndHost != null && snd.sharers <= 0 && lastT < Mathf.Clamp(OzBridgeRestore.Span, 1f, 2f) - 0.02f) sndHost.CutSound(snd);   // 1.5 kit crane_repair_look_r6 (A9): cut short (the bridge destroyed again): the sound fades out from where it is
                snd = null;
                foreach (var r in wreckHidden) if (r) r.enabled = true;                                   // the wreck as it was (switched off by the swap, or back for a stop)
                wreckHidden.Clear(); wreckLight.Clear();
                if (!intactShown && o != null && !o.Destroyed) ShowIntact();
                else foreach (var r in intactHidden) if (r) r.enabled = true;
                intactHidden.Clear();
                if (root) Object.Destroy(root);
                foreach (var m in cullOff) if (m) Object.Destroy(m);
                cullOff.Clear(); pieces.Clear();
            }
        }
    }
}
