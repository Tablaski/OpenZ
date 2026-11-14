using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.2, item 72 (the request: a destroyed MLRS keeps its tracks "as one piece" ring; they must fall apart). Three causes, all
    /// in the MLRS break only (the light and heavy tanks break their own way, OzTankWreck.cs, untouched):
    /// <list type="bullet">
    /// <item>the right track (MLRS_RegularTread_R) has no baked wreck fragments (only the left one has): it flew whole. Now
    /// <see cref="Ensure"/> bins a track without fragments at its break exactly as the editor bake binned the left one (its triangles by
    /// their centre into the 12 bins of the mesh's own bounds), so WreckPieces' fragment path cuts it at the bin walls like the left;</item>
    /// <item>the break's 12-piece budget glued the smallest pieces (the track lengths among them) back onto the biggest one, in place: the
    /// ring came back on one body. Now (<see cref="Regroup"/>) every track length rides on the hull piece it lies under (most overlap,
    /// else the nearest), as the tanks' torn tracks do, and over the budget the smallest piece without a track length joins the piece it
    /// overlaps most (where it sits), never a track length onto another one;</item>
    /// <item>the death's tear-off (WheeledBreakup.Tear) threw a whole track loop off as one piece three times in ten: that candidate is
    /// left out (<see cref="TearWholeTrack"/>; the other torn parts stay), the tracks break with the wreck.</item>
    /// </list>
    /// The pieces are WreckPieces' own bodies (the same throw, clanks, settle, sink and fade, the same rocks rules). View only.
    /// </summary>
    public static class OzTreadBreak
    {
        public static bool On = true;
        /// <summary>The vehicle types whose tracks break this way.</summary>
        public static readonly HashSet<string> Types = new HashSet<string> { "missile_launcher" };
        /// <summary>A/B only: true = the old tear-off that threw a whole track loop off at the death (three times in ten).</summary>
        public static bool TearWholeTrack = false;

        // ------------------------------------------------------------------ proof
        public static int Binned, BinnedFrags, NotReadable, Breaks, Joined, BudgetJoins;
        /// <summary>The latest break: its track lengths (their transforms, per side), the track meshes they came from, and a summary.</summary>
        public static readonly List<Transform> LastTread = new List<Transform>();
        public static readonly List<string> LastSide = new List<string>();
        public static readonly List<Mesh> LastSources = new List<Mesh>();
        public static string Last = "-";
        /// <summary>k92: every break as it happens (its summary and both sides' lengths): two vehicles breaking in one frame are two calls.</summary>
        public static event System.Action<string> Broke;

        static readonly Dictionary<Mesh, List<Mesh>> cache = new Dictionary<Mesh, List<Mesh>>();
        static MaterialPropertyBlock blk;

        static bool Applies(UnitView v) => On && v && v.Obj != null && v.Obj.Type != null && Types.Contains(v.Obj.Type);

        static bool IsTreadName(string n) { n = n.ToLowerInvariant(); return n.Contains("tread") || n.Contains("track"); }

        /// <summary>WreckPieces.Break, before it looks for a renderer's fragments: a track renderer of this type without baked fragments
        /// gets them now (OZ_WreckFragments / frag_k, inactive, on identity, the renderer's own materials and paint).</summary>
        internal static void Ensure(UnitView view, MeshRenderer mr, MeshFilter mf)
        {
            if (!Applies(view) || !mr || !mf || !mf.sharedMesh || !IsTreadName(mr.name) || mr.transform.Find("OZ_WreckFragments")) return;
            var src = mf.sharedMesh;
            if (!src.isReadable) { NotReadable++; return; }
            if (!cache.TryGetValue(src, out var frags)) { frags = Bins(src); cache[src] = frags; }
            if (frags == null || frags.Count < 2) return;
            var holder = new GameObject("OZ_WreckFragments") { layer = mr.gameObject.layer };
            holder.SetActive(false);
            holder.transform.SetParent(mr.transform, false);
            var mats = mr.sharedMaterials;
            blk ??= new MaterialPropertyBlock();
            for (int i = 0; i < frags.Count; i++)
            {
                var go = new GameObject("frag_" + i) { layer = mr.gameObject.layer };
                go.transform.SetParent(holder.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = frags[i];
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = mats; r.shadowCastingMode = mr.shadowCastingMode;
                if (mr.HasPropertyBlock())
                {   // the track's own paint (team colour, the wreck's burn)
                    mr.GetPropertyBlock(blk); if (!blk.isEmpty) r.SetPropertyBlock(blk);
                    for (int k = 0; k < mats.Length; k++) { mr.GetPropertyBlock(blk, k); if (!blk.isEmpty) r.SetPropertyBlock(blk, k); }
                }
            }
            Binned++; BinnedFrags += frags.Count;
        }

        static int BinKey(Vector3 p, Bounds u)
        {   // WreckPieces.BinKey: x halves, z halves, 3 height layers of the source mesh's bounds
            var s = Vector3.Max(u.size, Vector3.one * 1e-4f); var q = p - u.min;
            return (q.x / s.x > 0.5f ? 1 : 0) + (q.z / s.z > 0.5f ? 2 : 0) + Mathf.Clamp(Mathf.FloorToInt(q.y / s.y * 3f), 0, 2) * 4;
        }

        /// <summary>The mesh's triangles WHOLE by their centre into the 12 bins (as the editor bake did): one mesh per bin that holds any,
        /// every vertex attribute and sub-mesh kept. The bin walls are cut later by WreckPieces' own set cut.</summary>
        static List<Mesh> Bins(Mesh src)
        {
            var vs = src.vertices; var ns = src.normals; var a0 = src.uv; var a1 = src.uv2; var cs = src.colors; var ts = src.tangents;
            int nv = vs.Length, subs = src.subMeshCount;
            bool hn = ns.Length == nv, h0 = a0.Length == nv, h1 = a1.Length == nv, hc = cs.Length == nv, ht = ts.Length == nv;
            var b = src.bounds;
            var maps = new Dictionary<int, int>[12]; var idx = new List<int>[12]; var tris = new List<int>[12, subs];
            for (int s = 0; s < subs; s++)
            {
                if (src.GetTopology(s) != MeshTopology.Triangles) continue;
                var tri = src.GetTriangles(s);
                for (int q = 0; q + 2 < tri.Length; q += 3)
                {
                    int k = BinKey((vs[tri[q]] + vs[tri[q + 1]] + vs[tri[q + 2]]) / 3f, b);
                    if (maps[k] == null) { maps[k] = new Dictionary<int, int>(); idx[k] = new List<int>(); }
                    if (tris[k, s] == null) tris[k, s] = new List<int>();
                    for (int j = 0; j < 3; j++)
                    {
                        int i = tri[q + j];
                        if (!maps[k].TryGetValue(i, out int d)) { d = idx[k].Count; maps[k][i] = d; idx[k].Add(i); }
                        tris[k, s].Add(d);
                    }
                }
            }
            var list = new List<Mesh>();
            for (int k = 0; k < 12; k++)
            {
                if (idx[k] == null) continue;
                var ix = idx[k]; int n = ix.Count;
                var m = new Mesh { name = src.name + "_tread_" + k };
                if (n > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                var pv = new Vector3[n]; for (int i = 0; i < n; i++) pv[i] = vs[ix[i]]; m.vertices = pv;
                if (hn) { var o = new Vector3[n]; for (int i = 0; i < n; i++) o[i] = ns[ix[i]]; m.normals = o; }
                if (h0) { var o = new Vector2[n]; for (int i = 0; i < n; i++) o[i] = a0[ix[i]]; m.uv = o; }
                if (h1) { var o = new Vector2[n]; for (int i = 0; i < n; i++) o[i] = a1[ix[i]]; m.uv2 = o; }
                if (hc) { var o = new Color[n]; for (int i = 0; i < n; i++) o[i] = cs[ix[i]]; m.colors = o; }
                if (ht) { var o = new Vector4[n]; for (int i = 0; i < n; i++) o[i] = ts[ix[i]]; m.tangents = o; }
                m.subMeshCount = subs;
                for (int s = 0; s < subs; s++) m.SetTriangles(tris[k, s] ?? new List<int>(), s);
                m.RecalculateBounds();
                list.Add(m);
            }
            return list;
        }

        static bool IsTreadPiece(Transform t) => t && t.parent && t.parent.name == "OZ_WreckFragments" && t.parent.parent && IsTreadName(t.parent.parent.name);

        static bool IsHullPiece(List<Transform> g) =>
            g.Count > 0 && g[0] && g[0].parent && g[0].parent.name == "OZ_WreckFragments" && g[0].parent.parent && g[0].parent.parent.name.ToLowerInvariant().Contains("hull");

        static bool HasTread(List<Transform> g) { foreach (var t in g) if (LastTread.Contains(t)) return true; return false; }

        static int Verts(List<Transform> g)
        {
            int n = 0; foreach (var t in g) if (t && t.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh) n += mf.sharedMesh.vertexCount;
            return n;
        }

        static Bounds BoxOf(List<Transform> g)
        {
            var b = new Bounds(); bool any = false;
            foreach (var t in g) if (t) foreach (var r in t.GetComponents<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            return b;
        }

        /// <summary>The group of `pool` (not `self`) that `g` overlaps most (a tie or no overlap: the nearest centre); -1 none.</summary>
        static int Best(List<Transform> g, List<List<Transform>> pool, List<Transform> self)
        {
            var b = BoxOf(g); int best = -1; float bi = -1f, bd = float.MaxValue;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] == self) continue;
                var h = BoxOf(pool[i]);
                var lo = Vector3.Max(b.min, h.min); var hi = Vector3.Min(b.max, h.max); var d = hi - lo;
                float inter = d.x > 0f && d.y > 0f && d.z > 0f ? d.x * d.y * d.z : 0f, dist = (b.center - h.center).sqrMagnitude;
                if (inter > bi + 1e-6f || (Mathf.Abs(inter - bi) <= 1e-6f && dist < bd)) { bi = inter; bd = dist; best = i; }
            }
            return best;
        }

        /// <summary>WreckPieces.Break, before its budget: the track lengths ride on the hull pieces they lie under, and the budget joins
        /// the smallest piece without a track length to the piece it sits in (see the class summary).</summary>
        internal static void Regroup(UnitView view, List<List<Transform>> groups)
        {
            if (!Applies(view)) return;
            LastTread.Clear(); LastSide.Clear(); LastSources.Clear();
            var treads = new List<List<Transform>>(); var hulls = new List<List<Transform>>();
            foreach (var g in groups)
            {
                if (g.Count == 1 && IsTreadPiece(g[0]))
                {
                    treads.Add(g); LastTread.Add(g[0]);
                    var owner = g[0].parent.parent; string n = owner.name.ToLowerInvariant();
                    LastSide.Add(n.EndsWith("_l") || n.Contains("left") ? "L" : n.EndsWith("_r") || n.Contains("right") ? "R" : owner.name);
                    if (owner.TryGetComponent<MeshFilter>(out var omf) && omf.sharedMesh && !LastSources.Contains(omf.sharedMesh)) LastSources.Add(omf.sharedMesh);
                }
                else if (IsHullPiece(g)) hulls.Add(g);
            }
            Breaks++;
            int joined = 0, budget = 0;
            if (hulls.Count > 0)
                foreach (var t in treads)
                {
                    int h = Best(t, hulls, null); if (h < 0) continue;
                    hulls[h].AddRange(t); groups.Remove(t); joined++;
                }
            while (groups.Count > WreckPieces.MaxGroups)
            {
                List<Transform> small = null; int sv = int.MaxValue;
                foreach (var g in groups) { if (HasTread(g)) continue; int v = Verts(g); if (v < sv) { sv = v; small = g; } }
                if (small == null) break;
                int to = Best(small, groups, small); if (to < 0) break;
                groups[to].AddRange(small); groups.Remove(small); budget++;
            }
            Joined += joined; BudgetJoins += budget;
            Last = $"{view.Obj.Type} #{view.Obj.Id}: {LastTread.Count} track lengths ({Count("L")} left, {Count("R")} right) on {hulls.Count} hull pieces ({joined} joined), {budget} small pieces joined where they sit, {groups.Count} pieces";
            Broke?.Invoke(Last + $" (L {Count("L")}, R {Count("R")})");                                 // k92: the proofs record every break
        }

        static int Count(string side) { int n = 0; foreach (var s in LastSide) if (s == side) n++; return n; }

        public static string State => $"track break {(On ? "on" : "OFF")}: tracks binned at their break {Binned} ({BinnedFrags} lengths, not readable {NotReadable}), breaks {Breaks}, lengths on hull pieces {Joined}, budget joins {BudgetJoins}; last: {Last}";
    }
}
