// OpenZ (GPL-3.0)
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// 1.5 notes N18 (VIEW ONLY: no rule reads it; the bridge, its deck and every unit are as before): a bridge's side railing breaks PER
    /// SECTION where a vehicle's drawn hull overlaps it, never the whole railing.
    /// <list type="bullet">
    /// <item><b>Sections</b> (<see cref="Prepare"/>, from ZGame.MakeWalkable, once per shown bridge model: the intact and the damaged one, every
    /// level): the railing's own triangles are found on the model (above the deck's side girders, in the narrow band of the railing line on each
    /// side: the posts and the rails; never the girders, the deck, the end posts or the stone) and cut at the middle between two posts, so a
    /// section is one post with the rail lengths to its neighbours; each section is drawn by its own child (the same materials, light and paint
    /// blocks: nothing changes on screen), the bridge model keeps everything else. The plank bridge (L16): its posts and rope rails the same way
    /// (fixed lengths where it has no posts); the plank wobble still sways them.</item>
    /// <item><b>Contact</b>: every frame each vehicle near a bridge (its drawn hull: the measured hull + track box of ZObject.RamDrawnBox, the
    /// turret and the barrels left out; its height) against each standing section's rail line: an overlap breaks that section.</item>
    /// <item><b>Break</b>: the section bends out about its foot on the girder (0.3 s, eased: the metal gives), scrapes (bld_bridge_rail_scrape,
    /// five takes from the game's own metal grind and skid sounds, no take twice in a row) with a few sparks where the hull rubs, then tears off
    /// (bld_bridge_rail_tear, sparks at its two torn ends) and falls with real physics (the debris layer: it lands on the bank, the deck or in
    /// the water, its landings dst_debris_metal; in the water the shared water landing: splash, sinks). The plank bridge's: wood (the plank knock,
    /// a wood snap, dst_debris_wood), no sparks.</item>
    /// <item><b>For the match</b>: the gap stays; a bridge model shown later (damaged after intact, intact again after a repair) comes without the
    /// sections broken before (by place). A piece that came to rest on a deck pops out after a few seconds (units drive there); one on a bank or
    /// in the water stays.</item>
    /// </list>
    /// Sounds and sparks only on screen (the events are in-view only; sparks only drawn on screen). Switch <see cref="On"/> (off before a level
    /// loads = the railings as before; off in play = no new breaks).
    /// </summary>
    public sealed class OzBridgeRails : MonoBehaviour
    {
        public static bool On = true;
        /// <summary>The railing on the model: its lowest point at least RailMinAbove over the deck, its highest at least RailTopAbove, all of it within
        /// RailBand of the railing line (m); where a bridge has no posts, sections of SectionM.</summary>
        public static float RailMinAbove = 0.25f, RailTopAbove = 0.45f, RailBand = 0.3f, SectionM = 1.8f;
        /// <summary>The contact: the drawn hull box grown by ContactMargin (m).</summary>
        public static float ContactMargin = 0.05f;
        /// <summary>The break: bend time (s), the bend (deg), the throw out / up (m/s) and the share of the vehicle's speed it takes.</summary>
        public static float BendSeconds = 0.3f, BendDegMin = 34f, BendDegMax = 58f, ThrowOut = 1.3f, ThrowUp = 1.0f, ThrowSpeedK = 0.3f;
        /// <summary>Seconds a piece resting on a deck waits before it pops out.</summary>
        public static float DeckRestPop = 6f;
        public const string ScrapeEv = "bld_bridge_rail_scrape", TearEv = "bld_bridge_rail_tear";

        /// <summary>Proof: bridge models prepared, sections made, sections broken, scrapes / tears sent, pieces resting.</summary>
        public static int Prepared, SectionsMade, Broken, Scrapes, Tears, Rested;
        public static string LastLine = "";

        sealed class Section
        {
            public int side, index; public float s0, s1, across, yBase, yTop; public bool broken;
            public readonly List<GameObject> gos = new List<GameObject>();
        }
        sealed class Rail
        {
            public Transform art; public Vector2Int key; public bool alongZ, wood; public float c, mid, deckY, halfFull, halfWidth;
            public readonly List<Section> sections = new List<Section>();
        }
        sealed class Bend
        {
            public Transform pivot; public Vector3 axis, outward, along; public float t, T, deg, twist, speed, alongVel; public bool wood; public Rail rail; public Section sec;
        }
        sealed class Mark : MonoBehaviour { }

        static readonly List<Rail> rails = new List<Rail>();
        static readonly Dictionary<Vector2Int, List<Vector3>> brokenAt = new Dictionary<Vector2Int, List<Vector3>>();   // (side, s0, s1) per bridge place
        static OzBridgeRails inst;
        static ZGame owner;                                                                        // a new match (a new ZGame): nothing broken yet
        static readonly System.Predicate<Rail> Gone = r => !r.art;
        readonly List<Bend> bends = new List<Bend>();
        readonly Dictionary<int, Vector3> lastPos = new Dictionary<int, Vector3>();
        readonly Dictionary<int, float> nextScrape = new Dictionary<int, float>();
        Transform debrisRoot; ZGame game;

        // ------------------------------------------------------------------ boot (level scenes)
        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Early()
        {
            rails.Clear(); brokenAt.Clear(); Prepared = SectionsMade = Broken = Scrapes = Tears = Rested = 0; LastLine = "";
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => TryAttach(); }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => TryAttach();
        public static void TryAttach()
        {
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!g || FindFirstObjectByType<OzBridgeRails>()) return;
            new GameObject("OzBridgeRails").AddComponent<OzBridgeRails>();
        }
        void OnEnable() { inst = this; }
        void OnDisable() { if (inst == this) inst = null; }

        // ================================================================== sections (ZGame.MakeWalkable, once per shown bridge model)
        struct V { public Vector3 p, n; public Vector4 tg, u0, u1; public Color32 c; public float s; }

        static V Lerp(V a, V b, float t)
        {
            return new V
            {
                p = Vector3.LerpUnclamped(a.p, b.p, t), n = Vector3.LerpUnclamped(a.n, b.n, t), tg = Vector4.LerpUnclamped(a.tg, b.tg, t),
                u0 = Vector4.LerpUnclamped(a.u0, b.u0, t), u1 = Vector4.LerpUnclamped(a.u1, b.u1, t), c = Color32.LerpUnclamped(a.c, b.c, t), s = Mathf.LerpUnclamped(a.s, b.s, t)
            };
        }

        /// <summary>Splits a convex polygon at s = cut into the part below and the part above (either may come out empty).</summary>
        static void Split(List<V> poly, float cut, List<V> lo, List<V> hi)
        {
            lo.Clear(); hi.Clear();
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                bool aLo = a.s <= cut, bLo = b.s <= cut;
                if (aLo) lo.Add(a); else hi.Add(a);
                if (aLo != bLo) { var x = Lerp(a, b, (cut - a.s) / (b.s - a.s)); x.s = cut; lo.Add(x); hi.Add(x); }
            }
        }

        sealed class Build { public readonly List<V> v = new List<V>(); public readonly Dictionary<int, List<int>> tris = new Dictionary<int, List<int>>(); }

        /// <summary>ZGame.MakeWalkable, right after the walk deck is made: cut the railing of this bridge model into sections (once; nothing changes
        /// on screen). Static: needs no component.</summary>
        public static void Prepare(GameObject art, GameObject walk)
        {
            if (!On || !art || !walk || art.GetComponent<Mark>()) return;
            var g = ZGame.I; if (g != owner) { owner = g; rails.Clear(); brokenAt.Clear(); }
            art.AddComponent<Mark>();
            try { PrepareCore(art, walk); }
            catch (System.Exception e) { Debug.LogWarning("[OpenZ] bridge rails: " + art.name + " left as it is (" + e.Message + ")"); }
        }

        static void PrepareCore(GameObject art, GameObject walk)
        {
            var wmc = walk.GetComponent<MeshCollider>(); var wm = wmc ? wmc.sharedMesh : null; if (!wm) return;
            var wv = wm.vertices; if (wv.Length == 0) return;
            var wb = new Bounds(walk.transform.TransformPoint(wv[0]), Vector3.zero);
            foreach (var q in wv) wb.Encapsulate(walk.transform.TransformPoint(q));
            var R = new Rail { art = art.transform, alongZ = wb.size.z >= wb.size.x, deckY = wb.max.y, wood = art.name.Contains("jungle") || (ZGame.I && ZGame.I.level == "L16") };
            R.c = R.alongZ ? wb.center.z : wb.center.x; R.mid = R.alongZ ? wb.center.x : wb.center.z;
            R.halfFull = R.alongZ ? wb.extents.z : wb.extents.x; R.halfWidth = R.alongZ ? wb.extents.x : wb.extents.z;
            var ap = art.transform.position; R.key = new Vector2Int(Mathf.RoundToInt(ap.x * 4f), Mathf.RoundToInt(ap.z * 4f));

            var mfs = new List<MeshFilter>();
            foreach (var mf in art.GetComponentsInChildren<MeshFilter>(true))
                if (mf.gameObject != walk && mf.sharedMesh && mf.sharedMesh.isReadable && !mf.name.StartsWith("OZ_Rail") && !NotRailing(mf)) mfs.Add(mf);
            // ---- pass 1: the candidates (over the girders, beside the deck) and the railing line of each side
            var lines = new Dictionary<int, float>[2] { new Dictionary<int, float>(), new Dictionary<int, float>() };   // 4 cm bins of |across| -> the length they cover along the bridge
            var data = new List<(MeshFilter mf, Vector3[] w, float[] s, float[] a, int[][] tris)>();
            foreach (var mf in mfs)
            {
                var m = mf.sharedMesh; var vs = m.vertices; var M = mf.transform.localToWorldMatrix;
                var w = new Vector3[vs.Length]; var s = new float[vs.Length]; var a = new float[vs.Length];
                for (int i = 0; i < vs.Length; i++) { w[i] = M.MultiplyPoint3x4(vs[i]); s[i] = (R.alongZ ? w[i].z : w[i].x) - R.c; a[i] = (R.alongZ ? w[i].x : w[i].z) - R.mid; }
                var tris = new int[m.subMeshCount][];
                for (int sm = 0; sm < m.subMeshCount; sm++)
                {
                    tris[sm] = m.GetTriangles(sm);
                    var t = tris[sm];
                    for (int k = 0; k + 2 < t.Length; k += 3)
                        if (Candidate(R, w, a, t[k], t[k + 1], t[k + 2], out int side))
                        {   // the railing is the line its long rails run along: each bin weighed by the length it covers (end posts and caps are short)
                            int bin = Mathf.RoundToInt(Mathf.Abs((a[t[k]] + a[t[k + 1]] + a[t[k + 2]]) / 3f) / 0.04f);
                            float len = Mathf.Max(s[t[k]], Mathf.Max(s[t[k + 1]], s[t[k + 2]])) - Mathf.Min(s[t[k]], Mathf.Min(s[t[k + 1]], s[t[k + 2]]));
                            lines[side][bin] = (lines[side].TryGetValue(bin, out var have) ? have : 0f) + len;
                        }
                }
                data.Add((mf, w, s, a, tris));
            }
            float[] line = new float[2];
            for (int sd = 0; sd < 2; sd++)
            {
                line[sd] = -1f; float best = 0f;
                foreach (var kv in lines[sd])
                {   // the bin and its two neighbours together (a rail's faces spread over 2-3 bins)
                    float sum = kv.Value + (lines[sd].TryGetValue(kv.Key - 1, out var l0) ? l0 : 0f) + (lines[sd].TryGetValue(kv.Key + 1, out var l1) ? l1 : 0f);
                    if (sum > best) { best = sum; line[sd] = kv.Key * 0.04f; }
                }
                if (best < 3f) line[sd] = -1f;                                                          // nothing that runs along the bridge: no railing on this side
            }
            if (line[0] < 0f && line[1] < 0f) return;
            // ---- pass 2: the railing (in the band of its side's line); its long rails' ends; its posts
            var picked = new List<(int d, int sm, int k, int side)>();
            var railEnds = new float[2, 2] { { float.MaxValue, float.MinValue }, { float.MaxValue, float.MinValue } };
            var posts = new List<float>[2] { new List<float>(), new List<float>() };
            for (int di = 0; di < data.Count; di++)
            {
                var (mf, w, s, a, tris) = data[di];
                for (int sm = 0; sm < tris.Length; sm++)
                {
                    var t = tris[sm];
                    for (int k = 0; k + 2 < t.Length; k += 3)
                    {
                        int i0 = t[k], i1 = t[k + 1], i2 = t[k + 2];
                        if (!Candidate(R, w, a, i0, i1, i2, out int side) || line[side] < 0f) continue;
                        float L = line[side];
                        if (Mathf.Abs(Mathf.Abs(a[i0]) - L) > RailBand || Mathf.Abs(Mathf.Abs(a[i1]) - L) > RailBand || Mathf.Abs(Mathf.Abs(a[i2]) - L) > RailBand) continue;
                        float sLo = Mathf.Min(s[i0], Mathf.Min(s[i1], s[i2])), sHi = Mathf.Max(s[i0], Mathf.Max(s[i1], s[i2]));
                        float yLo = Mathf.Min(w[i0].y, Mathf.Min(w[i1].y, w[i2].y)), yHi = Mathf.Max(w[i0].y, Mathf.Max(w[i1].y, w[i2].y));
                        if (sHi - sLo > 2f) { railEnds[side, 0] = Mathf.Min(railEnds[side, 0], sLo); railEnds[side, 1] = Mathf.Max(railEnds[side, 1], sHi); }
                        else if (yHi - yLo > 0.25f && sHi - sLo < 0.3f) posts[side].Add((sLo + sHi) * 0.5f);
                        picked.Add((di, sm, k, side));
                    }
                }
            }
            if (picked.Count == 0) return;
            // the railing's length per side (long rails' ends; else everything picked) and its cuts (between posts; else every SectionM)
            var cuts = new List<float>[2] { new List<float>(), new List<float>() };
            for (int sd = 0; sd < 2; sd++)
            {
                if (railEnds[sd, 0] > railEnds[sd, 1])
                    foreach (var p in picked) if (p.side == sd) { var dd = data[p.d]; var t = dd.tris[p.sm]; for (int j = 0; j < 3; j++) { railEnds[sd, 0] = Mathf.Min(railEnds[sd, 0], dd.s[t[p.k + j]]); railEnds[sd, 1] = Mathf.Max(railEnds[sd, 1], dd.s[t[p.k + j]]); } }
                var ps = posts[sd]; ps.Sort();
                var centres = new List<float>();
                foreach (var x in ps) { if (centres.Count > 0 && x - centres[centres.Count - 1] < 0.35f) continue; centres.Add(x); }
                if (centres.Count >= 2) for (int i = 0; i + 1 < centres.Count; i++) cuts[sd].Add((centres[i] + centres[i + 1]) * 0.5f);
                if (centres.Count >= 2 && railEnds[sd, 1] > railEnds[sd, 0]) Subdivide(cuts[sd], centres, railEnds[sd, 0], railEnds[sd, 1]);   // sparse posts: no section longer than ~2.6 m
                else if (railEnds[sd, 1] > railEnds[sd, 0])
                {
                    int n = Mathf.Max(1, Mathf.RoundToInt((railEnds[sd, 1] - railEnds[sd, 0]) / SectionM));
                    for (int i = 1; i < n; i++) cuts[sd].Add(Mathf.Lerp(railEnds[sd, 0], railEnds[sd, 1], i / (float)n));
                }
            }
            // ---- pass 3: cut the picked triangles into the sections (those of an end post's cap beyond the rails' ends stay with the bridge)
            var keep = new HashSet<long>();
            var builds = new Dictionary<(int d, int side, int sec), Build>();
            var poly = new List<V>(); var lo = new List<V>(); var hi = new List<V>(); var rest = new List<V>();
            for (int di = 0; di < data.Count; di++)
            {
                var m = data[di].mf.sharedMesh;
                var nrm = m.normals; var tan = m.tangents; var col = m.colors32;
                var uv0 = new List<Vector4>(); m.GetUVs(0, uv0); var uv1 = new List<Vector4>(); m.GetUVs(1, uv1);
                var vs = m.vertices;
                foreach (var p in picked)
                {
                    if (p.d != di) continue;
                    var dd = data[di]; var t = dd.tris[p.sm];
                    float sc = (dd.s[t[p.k]] + dd.s[t[p.k + 1]] + dd.s[t[p.k + 2]]) / 3f;
                    if (sc < railEnds[p.side, 0] - 0.05f || sc > railEnds[p.side, 1] + 0.05f) continue;
                    keep.Add(((long)di << 40) | ((long)p.sm << 32) | (uint)p.k);
                    poly.Clear();
                    for (int j = 0; j < 3; j++)
                    {
                        int vi = t[p.k + j];
                        poly.Add(new V
                        {
                            p = vs[vi], n = nrm.Length > vi ? nrm[vi] : Vector3.up, tg = tan.Length > vi ? tan[vi] : Vector4.zero,
                            u0 = uv0.Count > vi ? uv0[vi] : Vector4.zero, u1 = uv1.Count > vi ? uv1[vi] : Vector4.zero,
                            c = col.Length > vi ? col[vi] : new Color32(255, 255, 255, 255), s = dd.s[vi]
                        });
                    }
                    var cs = cuts[p.side];
                    int sec = 0;
                    for (int ci = 0; ci <= cs.Count && poly.Count >= 3; ci++)
                    {
                        if (ci < cs.Count) { Split(poly, cs[ci], lo, hi); }
                        else { lo.Clear(); lo.AddRange(poly); hi.Clear(); }
                        if (lo.Count >= 3)
                        {
                            var key = (di, p.side, sec);
                            if (!builds.TryGetValue(key, out var b)) { b = new Build(); builds[key] = b; }
                            if (!b.tris.TryGetValue(p.sm, out var tl)) { tl = new List<int>(); b.tris[p.sm] = tl; }
                            int baseI = b.v.Count; b.v.AddRange(lo);
                            for (int f = 1; f + 1 < lo.Count; f++) { tl.Add(baseI); tl.Add(baseI + f); tl.Add(baseI + f + 1); }
                        }
                        rest.Clear(); rest.AddRange(hi); poly.Clear(); poly.AddRange(rest);
                        sec++;
                    }
                }
            }
            if (builds.Count == 0) return;
            // ---- the bridge model without the railing triangles (its vertices as they were) and one child per section and model part
            var secs = new Dictionary<(int side, int sec), Section>();
            var mpb = new MaterialPropertyBlock();
            for (int di = 0; di < data.Count; di++)
            {
                var (mf, w, s, a, tris) = data[di];
                bool any = false; foreach (var k in builds.Keys) if (k.d == di) { any = true; break; }
                if (!any) continue;
                var src = mf.sharedMesh; var copy = Object.Instantiate(src); copy.name = src.name + " (OZ rails cut)";
                for (int sm = 0; sm < tris.Length; sm++)
                {
                    var t = tris[sm]; var nt = new List<int>(t.Length);
                    for (int k = 0; k + 2 < t.Length; k += 3)
                        if (!keep.Contains(((long)di << 40) | ((long)sm << 32) | (uint)k)) { nt.Add(t[k]); nt.Add(t[k + 1]); nt.Add(t[k + 2]); }
                    copy.SetTriangles(nt, sm, false);
                }
                copy.bounds = src.bounds;
                var srcR = mf.GetComponent<MeshRenderer>(); var mats = srcR ? srcR.sharedMaterials : new Material[0];
                mf.sharedMesh = copy;
                foreach (var kv in builds)
                {
                    if (kv.Key.d != di) continue;
                    var b = kv.Value;
                    var go = new GameObject($"OZ_RailSec_{(kv.Key.side == 0 ? "L" : "R")}{kv.Key.sec}_{mf.name}") { layer = mf.gameObject.layer };
                    go.transform.SetParent(mf.transform, false);
                    var mesh = new Mesh { name = "OZ_RailSection" };
                    if (b.v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                    var pv = new List<Vector3>(b.v.Count); var pn = new List<Vector3>(b.v.Count); var pt = new List<Vector4>(b.v.Count); var pc = new List<Color32>(b.v.Count);
                    var p0 = new List<Vector4>(b.v.Count); var p1 = new List<Vector4>(b.v.Count);
                    foreach (var x in b.v) { pv.Add(x.p); pn.Add(x.n); pt.Add(x.tg); pc.Add(x.c); p0.Add(x.u0); p1.Add(x.u1); }
                    mesh.SetVertices(pv); mesh.SetNormals(pn);
                    if (src.HasVertexAttribute(VertexAttribute.Tangent)) mesh.SetTangents(pt);
                    if (src.HasVertexAttribute(VertexAttribute.Color)) mesh.SetColors(pc);
                    if (src.HasVertexAttribute(VertexAttribute.TexCoord0)) mesh.SetUVs(0, p0);
                    if (src.HasVertexAttribute(VertexAttribute.TexCoord1)) mesh.SetUVs(1, p1);
                    var subs = new List<int>(b.tris.Keys); subs.Sort();
                    mesh.subMeshCount = subs.Count;
                    var useMats = new Material[subs.Count];
                    for (int i = 0; i < subs.Count; i++) { mesh.SetTriangles(b.tris[subs[i]], i, false); useMats[i] = subs[i] < mats.Length ? mats[subs[i]] : null; }
                    mesh.RecalculateBounds();
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = useMats;
                    if (srcR)
                    {
                        r.shadowCastingMode = srcR.shadowCastingMode; r.receiveShadows = srcR.receiveShadows; r.lightProbeUsage = srcR.lightProbeUsage;
                        r.reflectionProbeUsage = srcR.reflectionProbeUsage; r.motionVectorGenerationMode = srcR.motionVectorGenerationMode; r.renderingLayerMask = srcR.renderingLayerMask;
                        for (int i = 0; i < subs.Count; i++) { if (subs[i] < mats.Length) { srcR.GetPropertyBlock(mpb, subs[i]); r.SetPropertyBlock(mpb, i); } }   // the bridge's paint blocks (TeamPaint)
                    }
                    var sk = (kv.Key.side, kv.Key.sec);
                    if (!secs.TryGetValue(sk, out var S)) { S = new Section { side = kv.Key.side, index = kv.Key.sec, s0 = float.MaxValue, s1 = float.MinValue, yBase = float.MaxValue, yTop = float.MinValue, across = line[kv.Key.side] }; secs[sk] = S; }
                    foreach (var x in b.v)
                    {
                        S.s0 = Mathf.Min(S.s0, x.s); S.s1 = Mathf.Max(S.s1, x.s);
                        float y = mf.transform.TransformPoint(x.p).y; S.yBase = Mathf.Min(S.yBase, y); S.yTop = Mathf.Max(S.yTop, y);
                    }
                    S.gos.Add(go);
                }
            }
            foreach (var S in secs.Values) { R.sections.Add(S); SectionsMade++; }
            R.sections.Sort((x, y) => x.side != y.side ? x.side.CompareTo(y.side) : x.s0.CompareTo(y.s0));
            // ---- sections broken before on this bridge (another of its models): gone at once
            if (brokenAt.TryGetValue(R.key, out var was))
                foreach (var S in R.sections)
                    foreach (var iv in was)
                        if (Mathf.RoundToInt(iv.x) == S.side && (S.s0 + S.s1) * 0.5f > iv.y && (S.s0 + S.s1) * 0.5f < iv.z) { S.broken = true; foreach (var g in S.gos) if (g) g.SetActive(false); }
            rails.Add(R); Prepared++;
            LastLine = $"bridge rails: {art.name}: {R.sections.Count} sections ({(R.wood ? "wood" : "steel")}), line {line[0]:0.00} / {line[1]:0.00} m";
            Debug.Log("[OpenZ] " + LastLine);
        }

        /// <summary>The bridge parts that are never railing: the deck plates and the stone / concrete (abutments, wing walls, piers).</summary>
        static bool NotRailing(MeshFilter mf)
        {
            string a = mf.sharedMesh.name.ToLowerInvariant(), b = mf.name.ToLowerInvariant();
            return a.Contains("stone") || a.Contains("plate") || b.Contains("stone") || b.Contains("plate");
        }

        /// <summary>Cuts between posts far apart (or from a rail end to the first cut) are split again so no section is longer than about 2.6 m;
        /// an added cut never lies within 0.3 m of a post.</summary>
        static void Subdivide(List<float> cuts, List<float> posts, float start, float end)
        {
            var b = new List<float> { start }; b.AddRange(cuts); b.Add(end); b.Sort();
            var add = new List<float>();
            for (int i = 0; i + 1 < b.Count; i++)
            {
                float len = b[i + 1] - b[i]; if (len <= 2.6f) continue;
                int n = Mathf.CeilToInt(len / SectionM);
                for (int k = 1; k < n; k++)
                {
                    float c = b[i] + len * k / n;
                    foreach (var p in posts) if (Mathf.Abs(c - p) < 0.3f) { c = p + (c >= p ? 0.4f : -0.4f); break; }
                    if (c > b[i] + 0.3f && c < b[i + 1] - 0.3f) add.Add(c);
                }
            }
            cuts.AddRange(add); cuts.Sort();
        }

        /// <summary>A triangle of the railing's kind: all of it over the side girders' top (RailMinAbove over the deck), reaching up to the rail,
        /// in the outer part of the width and short of the bridge's outer edge. side: 0 = the low across side, 1 = the high one.</summary>
        static bool Candidate(Rail R, Vector3[] w, float[] a, int i0, int i1, int i2, out int side)
        {
            side = (a[i0] + a[i1] + a[i2]) >= 0f ? 1 : 0;
            float yLo = Mathf.Min(w[i0].y, Mathf.Min(w[i1].y, w[i2].y)), yHi = Mathf.Max(w[i0].y, Mathf.Max(w[i1].y, w[i2].y));
            if (yLo < R.deckY + RailMinAbove || yHi < R.deckY + RailTopAbove) return false;
            float aLo = Mathf.Min(Mathf.Abs(a[i0]), Mathf.Min(Mathf.Abs(a[i1]), Mathf.Abs(a[i2]))), aHi = Mathf.Max(Mathf.Abs(a[i0]), Mathf.Max(Mathf.Abs(a[i1]), Mathf.Abs(a[i2])));
            if ((a[i0] >= 0f) != (a[i1] >= 0f) || (a[i0] >= 0f) != (a[i2] >= 0f)) return false;
            // beside the deck: not the walk deck's measured half width (on the desert bridge it takes in the splayed ramp tops between the stone
            // wing walls, 3.8 m, wider than the railing line at 3.2 m), so: the outer part of the width; the railing line itself comes from the
            // along-coverage of the candidates (the long rails), PrepareCore
            return aLo > Mathf.Max(1.0f, R.halfWidth * 0.4f) && aHi < R.halfWidth + 1.6f;
        }

        // ================================================================== contact, every frame
        void LateUpdate()
        {
            if (!game) { game = ZGame.I; if (!game) return; }
            float dt = game.paused ? 0f : Time.deltaTime;
            StepBends(dt);
            if (!On || rails.Count == 0 || game.World == null) return;
            if (dt <= 0f) return;
            rails.RemoveAll(Gone);
            var objs = game.World.Objects;
            for (int i = 0; i < objs.Count; i++)
            {
                var o = objs[i];
                if (o.Kind != ZKind.Vehicle || o.Destroyed) continue;
                if (!game.Views.TryGetValue(o.Id, out var v) || !v) continue;
                var p = v.transform.position;
                var vel = lastPos.TryGetValue(o.Id, out var lp) ? (p - lp) / dt : Vector3.zero; lastPos[o.Id] = p;
                ZObject.RamDrawnBox(o.Type, out int fr, out int re, out int hw);
                if (fr <= 0 || hw <= 0) continue;                                                              // a gun, anything without a measured hull
                float height = ZObject.RamDrawnHeight(o.Type) / 8f;
                float yaw = v.DrawnYaw * Mathf.Deg2Rad;
                var fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)); var right = new Vector3(fwd.z, 0f, -fwd.x);
                var centre = p + fwd * ((fr - re) / 16f);
                float hu = (fr + re) / 16f + ContactMargin, hv = hw / 8f + ContactMargin;
                foreach (var R in rails)
                {
                    if (!R.art.gameObject.activeInHierarchy) continue;
                    float s = (R.alongZ ? p.z : p.x) - R.c, a = (R.alongZ ? p.x : p.z) - R.mid;
                    if (Mathf.Abs(s) > R.halfFull + hu + 1f || Mathf.Abs(a) > R.halfWidth + hu + 2f) continue;
                    foreach (var S in R.sections)
                    {
                        if (S.broken) continue;
                        if (p.y > S.yTop || p.y + height < S.yBase + 0.1f) continue;                            // under the bridge or high over it
                        float ac = (S.side == 1 ? 1f : -1f) * S.across + R.mid;
                        var A = R.alongZ ? new Vector3(ac, 0f, R.c + S.s0) : new Vector3(R.c + S.s0, 0f, ac);
                        var B = R.alongZ ? new Vector3(ac, 0f, R.c + S.s1) : new Vector3(R.c + S.s1, 0f, ac);
                        if (!SegmentInBox(A - centre, B - centre, fwd, right, hu, hv, out float t0, out float t1)) continue;
                        var hit = Vector3.Lerp(A, B, (t0 + t1) * 0.5f); hit.y = Mathf.Lerp(S.yBase, S.yTop, 0.55f);
                        BreakSection(R, S, hit, vel, o.Id);
                    }
                }
            }
            if ((Time.frameCount & 255) == 0) { var dead = new List<int>(); foreach (var kv in lastPos) if (game.World.Get(kv.Key) == null) dead.Add(kv.Key); foreach (var k in dead) { lastPos.Remove(k); nextScrape.Remove(k); } }
        }

        /// <summary>The segment a..b (relative to the box centre) against the box (axes u, v; half sizes hu, hv) in xz: the overlap's part (t0..t1).</summary>
        static bool SegmentInBox(Vector3 a, Vector3 b, Vector3 u, Vector3 v, float hu, float hv, out float t0, out float t1)
        {
            t0 = 0f; t1 = 1f;
            float au = a.x * u.x + a.z * u.z, av = a.x * v.x + a.z * v.z, bu = b.x * u.x + b.z * u.z, bv = b.x * v.x + b.z * v.z;
            return Clip(-(bu - au), au + hu, ref t0, ref t1) && Clip(bu - au, hu - au, ref t0, ref t1)
                && Clip(-(bv - av), av + hv, ref t0, ref t1) && Clip(bv - av, hv - av, ref t0, ref t1);
        }
        static bool Clip(float p, float q, ref float t0, ref float t1)
        {   // Liang-Barsky: p t <= q
            if (Mathf.Abs(p) < 1e-6f) return q >= 0f;
            float r = q / p;
            if (p < 0f) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }

        // ================================================================== the break
        void BreakSection(Rail R, Section S, Vector3 hit, Vector3 vel, int by)
        {
            S.broken = true; Broken++;
            if (!brokenAt.TryGetValue(R.key, out var list)) { list = new List<Vector3>(); brokenAt[R.key] = list; }
            list.Add(new Vector3(S.side, S.s0 - 0.01f, S.s1 + 0.01f));
            var acrossDir = R.alongZ ? Vector3.right : Vector3.forward; var along = R.alongZ ? Vector3.forward : Vector3.right;
            var outward = acrossDir * (S.side == 1 ? 1f : -1f);
            // the hinge: the section's foot on the girder, its outer edge, the middle of its length (the pieces as drawn now, sag and all)
            var b = new Bounds(); bool first = true;
            foreach (var g in S.gos) { if (!g) continue; var r = g.GetComponent<Renderer>(); if (!r) continue; if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            if (first) return;
            var pivotAt = new Vector3(b.center.x, b.min.y, b.center.z) + outward * Vector3.Dot(b.extents, new Vector3(Mathf.Abs(outward.x), 0f, Mathf.Abs(outward.z)));
            if (!debrisRoot) debrisRoot = new GameObject("OZ_BridgeRailDebris").transform;
            var pivot = new GameObject("OZ_RailBreak").transform; pivot.SetParent(debrisRoot, false); pivot.position = pivotAt;
            foreach (var g in S.gos)
            {
                if (!g) continue;
                var mf = g.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh) { var own = Instantiate(mf.sharedMesh); own.name = "OZ_RailPiece"; mf.sharedMesh = own; }       // its own mesh: the deck's give / plank sway write elsewhere
                g.transform.SetParent(pivot, true);
            }
            float speed = new Vector2(vel.x, vel.z).magnitude;
            var bd = new Bend
            {
                pivot = pivot, axis = Vector3.Cross(Vector3.up, outward).normalized, outward = outward, along = along, rail = R, sec = S, wood = R.wood,
                T = BendSeconds / Mathf.Lerp(1f, 1.8f, Mathf.Clamp01(speed / 6f)), deg = Random.Range(BendDegMin, BendDegMax), twist = Random.Range(-9f, 9f),
                speed = speed, alongVel = Vector3.Dot(vel, along)
            };
            bends.Add(bd);
            // the scrape where the hull rubs, a few sparks there
            if (!nextScrape.TryGetValue(by, out var at) || Time.time >= at)
            {
                nextScrape[by] = Time.time + 0.22f;
                if (R.wood) { Send("bld_bridge_plank_knock", hit, by, 0.8f, Random.Range(0.92f, 1.06f), "a vehicle hits the plank bridge's rail"); Send("bld_bridge_wood_creak", hit, by, 0.7f, Random.Range(0.9f, 1.05f), "the rail post gives"); }
                else if (!Send(ScrapeEv, hit, by, 1f, Random.Range(0.94f, 1.08f), "a vehicle's hull scrapes the railing")) Send("shuttle_skid_scrape", hit, by, 0.8f, Random.Range(0.94f, 1.08f), "a vehicle's hull scrapes the railing (no scrape event)");
                Scrapes++;
            }
            if (!R.wood) Sparks(hit, -outward * 0.3f + Vector3.up * 0.5f, 8);
            LastLine = $"bridge rails: section {(S.side == 1 ? "R" : "L")}{S.index} broken by #{by} at {speed:0.0} m/s";
        }

        void StepBends(float dt)
        {
            for (int i = bends.Count - 1; i >= 0; i--)
            {
                var B = bends[i];
                if (!B.pivot) { bends.RemoveAt(i); continue; }
                B.t += dt;
                float u = Mathf.Clamp01(B.t / B.T), e = 1f - (1f - u) * (1f - u) * (1f - u);                  // the metal gives fast, then slows
                B.pivot.rotation = Quaternion.AngleAxis(B.twist * e, Vector3.up) * Quaternion.AngleAxis(B.deg * e, B.axis);
                if (u < 1f) continue;
                bends.RemoveAt(i);
                Tear(B);
            }
        }

        /// <summary>The bend is done: it tears off and falls (real physics on the debris layer: the bank, the deck or the water).</summary>
        void Tear(Bend B)
        {
            var go = B.pivot.gameObject; var b = new Bounds(); bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            if (first) { Destroy(go); return; }
            int layer = BuildingBreaker.DebrisLayer;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            var box = go.AddComponent<BoxCollider>();
            box.center = B.pivot.InverseTransformPoint(b.center);
            var ls = B.pivot.InverseTransformVector(b.size); box.size = new Vector3(Mathf.Max(0.08f, Mathf.Abs(ls.x)), Mathf.Max(0.08f, Mathf.Abs(ls.y)), Mathf.Max(0.08f, Mathf.Abs(ls.z)));
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = B.wood ? 25f : 60f; rb.linearDamping = 0.2f; rb.angularDamping = 0.8f; rb.maxAngularVelocity = 9f;
            rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearVelocity = B.outward * (ThrowOut + ThrowSpeedK * B.speed) + Vector3.up * ThrowUp + B.along * (B.alongVel * ThrowSpeedK);
            rb.angularVelocity = B.axis * (B.deg * Mathf.Deg2Rad / Mathf.Max(0.1f, B.T) * 0.55f) + Random.insideUnitSphere * 0.6f;
            AudioDirector.AttachImpacts(go, B.wood ? "dst_debris_wood" : "dst_debris_metal", "dst_debris_splash");      // its landings on the bank / deck, the splash in the water
            OzWaterLanding.Watch(go.transform, "bridge rail", rb, false, false, B.wood);                                  // the shared water landing (wood floats)
            var life = go.AddComponent<Piece>(); life.rb = rb;
            // the tear: sound at its two torn ends, sparks there (steel)
            var c = b.center; var half = B.along * Vector3.Dot(b.extents, new Vector3(Mathf.Abs(B.along.x), 0f, Mathf.Abs(B.along.z)));
            if (B.wood) Send("dst_debris_wood", c, -1, 0.9f, Random.Range(1.05f, 1.2f), "the rail post snaps off");
            else
            {
                if (!Send(TearEv, c, -1, 1f, Random.Range(0.94f, 1.08f), "the railing section tears off")) Send("dst_fortgun_break_metal", c, -1, 0.7f, Random.Range(1.0f, 1.15f), "the railing section tears off (no tear event)");
                Sparks(c - half, B.outward + Vector3.up, 5); Sparks(c + half, B.outward + Vector3.up, 5);
            }
            Tears++;
        }

        static bool Send(string ev, Vector3 at, int emitter, float vol, float pitch, string why)
        {
            if (!AudioDirector.Has(ev)) return false;
            AudioDirector.Fire(ev, at, emitter, vol, pitch);
            OzSoundLog.Add("RAIL", ev, null, "SENT World (" + why + ")", vol, at);
            return true;
        }

        static void Sparks(Vector3 at, Vector3 dir, int count)
        {
            var px = OpenZFxPlayer.I; if (!px || !px.isActiveAndEnabled || !WreckFx.OnScreen(at, 0.1f)) return;
            for (int i = 0; i < count; i++)
                px.EmitChannel("m12_spark", at + Random.insideUnitSphere * 0.08f, dir * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(0.8f, 2.4f) + Random.insideUnitSphere * 1.2f,
                               Random.Range(0.3f, 0.6f), 0.55f * 0.07f * 0.8f, new Color(1f, 0.56f, 0.2f), 1f);
            for (int i = 0; i < count / 2 + 1; i++)
                px.EmitChannel("leg_spark", at + Random.insideUnitSphere * 0.1f, dir * Random.Range(0.8f, 2f) + Vector3.up * Random.Range(0.4f, 1.6f) + Random.insideUnitSphere * 0.6f,
                               Random.Range(0.3f, 0.6f), Random.Range(0.035f, 0.06f), new Color(1f, 0.72f, 0.36f), 1f);
        }

        /// <summary>A torn-off piece: at rest on a bank it stays for the match (kinematic, no collider work); on a bridge deck (units drive there)
        /// it pops out after DeckRestPop s; one that fell out of the world goes.</summary>
        sealed class Piece : MonoBehaviour
        {
            public Rigidbody rb; float age, restAt = -1f; bool still;
            void Update()
            {
                age += Time.deltaTime;
                if (transform.position.y < -60f) { Destroy(gameObject); return; }
                if (still || !rb) return;
                if (age > 1.2f && rb.IsSleeping())
                {
                    if (restAt < 0f) restAt = age;
                    var tm = TrackMarks.I; bool deck = false;
                    if (tm && tm.Surface != null) tm.Surface.Sample(transform.position.x, transform.position.z, out _, out deck);
                    if (deck && Mathf.Abs(rb.position.y - (ZGame.I ? ZGame.I.GroundY(rb.position.x, rb.position.z) : rb.position.y)) < 1.2f)
                    {
                        if (age - restAt > DeckRestPop) { still = true; PopOut.Begin(transform, PopOut.Style.Debris, null, true, false); }
                        return;
                    }
                    still = true; rb.isKinematic = true; Rested++;
                }
            }
        }

        public static string Status() =>
            $"bridge rails {(On ? "on" : "off")}: models {Prepared}, sections {SectionsMade}, broken {Broken}, scrapes {Scrapes}, tears {Tears}, resting {Rested}; last: {LastLine}";
    }
}
