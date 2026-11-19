// OpenZ (GPL-3.0)
// The points a robot DRAWS, so a dead body is put down by its lowest drawn point.
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit robot_pieces_ground_r11 (A35 remainder). What a robot's body and parts draw, as points: a mesh's points are its extreme vertices along
    /// <see cref="DirCount"/> directions spread over the sphere plus the 6 axes, so the lowest vertex of the mesh in any pose is among them within
    /// 1 - cos(half their spacing) of the mesh's size (under 0.8 cm for a 0.6 m part, exact when it lies square on a face). Where they come from:
    /// <list type="bullet">
    /// <item>a readable mesh (the baked body parts of the pop death, the procedural pieces): its own vertices that a triangle uses;</item>
    /// <item>a mesh players cannot read (the BS-01 kit, the guns, the skinned bodies: Read/Write off): the editor bake <see cref="Resource"/>
    /// (menu OpenZ/1.5/Bake/Robot drawn points, Editor/OzRobotDrawnPointsBake.cs), by mesh name and vertex count; a skinned body there per bone (every
    /// vertex in the space of the bone that carries most of its weight, as the pop death's body parts are cut);</item>
    /// <item>no bake: a robot type's skinned body falls back on its baked body parts (Resources/OpenZ/RobotParts); any other skin is baked now and its
    /// lowest vertices taken (slow, counted in <see cref="SkinBakes"/>); a rigid mesh falls back on its box's 8 corners (never above its lowest drawn
    /// point; counted in <see cref="FromCorners"/>).</item>
    /// </list>
    /// View only: nothing here is read by the rules.
    /// </summary>
    public static class OzDrawnPoints
    {
        /// <summary>The editor bake (a TextAsset .bytes under Resources).</summary>
        public const string Resource = "OpenZ/OzRobotDrawnPoints";
        public const string Magic = "OZDP";
        public const int Version = 1;
        /// <summary>Directions spread over the sphere (Fibonacci), the 6 axes come on top.</summary>
        public const int DirCount = 110;
        /// <summary>Counters (Play checks): meshes read from their own vertices / from the bake / from their box's corners; skins from the bake / from the
        /// robot type's body parts; skins baked now (slow path).</summary>
        public static int FromOwn, FromTable, FromCorners, SkinsFromTable, SkinsFromParts, SkinBakes;

        sealed class Entry { public int[] bone; public Vector3[][] pts; }
        static Dictionary<string, Entry> table;
        static readonly Dictionary<Mesh, Vector3[]> rigid = new Dictionary<Mesh, Vector3[]>();
        static readonly Dictionary<Mesh, Entry> skins = new Dictionary<Mesh, Entry>();
        static readonly List<Vector3> vb = new List<Vector3>();
        static readonly List<int> ib = new List<int>();
        static Vector3[] dirs;
        static Mesh bake;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Statics() { FromOwn = FromTable = FromCorners = SkinsFromTable = SkinsFromParts = SkinBakes = 0; rigid.Clear(); skins.Clear(); table = null; }

        public static string Key(Mesh m) => m ? m.name + "#" + m.vertexCount : "";

        /// <summary>The bake's entries (0 = no bake in this build: the fallbacks answer).</summary>
        public static int TableEntries { get { Load(); return table.Count; } }

        public static string Status() =>
            $"drawn points: bake {TableEntries} meshes; meshes from their own vertices {FromOwn}, from the bake {FromTable}, from their box corners {FromCorners}; " +
            $"skins from the bake {SkinsFromTable}, from the body parts {SkinsFromParts}, baked now {SkinBakes}";

        static void Load()
        {
            if (table != null) return;
            table = new Dictionary<string, Entry>();
            var ta = Resources.Load<TextAsset>(Resource);
            if (!ta) return;
            try
            {
                using (var r = new BinaryReader(new MemoryStream(ta.bytes)))
                {
                    if (new string(r.ReadChars(4)) != Magic || r.ReadInt32() != Version) return;
                    int n = r.ReadInt32();
                    for (int i = 0; i < n; i++)
                    {
                        string key = r.ReadString(); int groups = r.ReadInt32();
                        var e = new Entry { bone = new int[groups], pts = new Vector3[groups][] };
                        for (int g = 0; g < groups; g++)
                        {
                            e.bone[g] = r.ReadInt32(); int c = r.ReadInt32(); var a = new Vector3[c];
                            for (int k = 0; k < c; k++) a[k] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                            e.pts[g] = a;
                        }
                        table[key] = e;
                    }
                }
            }
            catch (System.Exception ex) { Debug.LogWarning("[OpenZ A35] drawn points bake unreadable: " + ex.Message); table.Clear(); }
        }

        /// <summary>The directions: DirCount spread over the sphere, then +-x, +-y, +-z.</summary>
        public static Vector3[] Dirs()
        {
            if (dirs != null) return dirs;
            var d = new Vector3[DirCount + 6]; float ga = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < DirCount; i++)
            {
                float y = 1f - (i + 0.5f) * 2f / DirCount, rr = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)), a = ga * i;
                d[i] = new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
            }
            d[DirCount] = Vector3.right; d[DirCount + 1] = Vector3.left; d[DirCount + 2] = Vector3.up; d[DirCount + 3] = Vector3.down; d[DirCount + 4] = Vector3.forward; d[DirCount + 5] = Vector3.back;
            return dirs = d;
        }

        /// <summary>The extreme points of v (only the indices in `only` when given) along every direction, each once.</summary>
        public static Vector3[] Extremes(List<Vector3> v, List<int> only = null)
        {
            var D = Dirs(); int nd = D.Length, n = only != null ? only.Count : v.Count;
            if (n == 0) return new Vector3[0];
            var best = new int[nd]; var bv = new float[nd];
            for (int k = 0; k < nd; k++) { bv[k] = float.MinValue; best[k] = -1; }
            for (int j = 0; j < n; j++)
            {
                int i = only != null ? only[j] : j; var p = v[i];
                for (int k = 0; k < nd; k++) { float s = D[k].x * p.x + D[k].y * p.y + D[k].z * p.z; if (s > bv[k]) { bv[k] = s; best[k] = i; } }
            }
            var seen = new HashSet<int>(); var o = new List<Vector3>(nd);
            for (int k = 0; k < nd; k++) if (best[k] >= 0 && seen.Add(best[k])) o.Add(v[best[k]]);
            return o.ToArray();
        }

        /// <summary>A mesh's drawn points in its own space (cached per mesh): its own used vertices when readable, else the bake, else its box's corners.</summary>
        public static Vector3[] Rigid(Mesh m)
        {
            if (!m) return null;
            if (rigid.TryGetValue(m, out var a)) return a;
            if (m.isReadable)
            {
                vb.Clear(); m.GetVertices(vb); var used = new HashSet<int>();
                for (int s = 0; s < m.subMeshCount; s++) { if (m.GetTopology(s) != MeshTopology.Triangles) continue; ib.Clear(); m.GetIndices(ib, s); foreach (int i in ib) used.Add(i); }
                a = Extremes(vb, used.Count > 0 ? new List<int>(used) : null); FromOwn++;
            }
            else
            {
                Load();
                if (table.TryGetValue(Key(m), out var e) && e.bone.Length == 1 && e.bone[0] < 0) { a = e.pts[0]; FromTable++; }
                else { a = Corners(m.bounds); FromCorners++; }
            }
            rigid[m] = a; return a;
        }

        static Vector3[] Corners(Bounds b)
        {
            var a = new Vector3[8];
            for (int i = 0; i < 8; i++) a[i] = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            return a;
        }

        /// <summary>A skinned body's points per bone (bone index into its bones, points in that bone's space): the bake, else the robot type's baked body
        /// parts (only for its main skin, the one with most vertices: the pop death's), else null.</summary>
        static Entry SkinOf(SkinnedMeshRenderer sk, string type, bool main)
        {
            var m = sk.sharedMesh; if (!m) return null;
            if (skins.TryGetValue(m, out var e)) return e;
            Load();
            if (table.TryGetValue(Key(m), out e) && e.bone.Length > 0 && e.bone[0] >= 0) { SkinsFromTable++; skins[m] = e; return e; }
            e = null;
            var ps = main ? RobotPopDeath.PartsOf(type) : null;
            if (ps != null)
            {
                var bones = sk.bones; var bl = new List<int>(); var pl = new List<Vector3[]>();
                for (int i = 0; i < ps.mesh.Length; i++)
                {
                    int j = -1; for (int b = 0; b < bones.Length; b++) if (bones[b] && bones[b].name == ps.bone[i]) { j = b; break; }
                    if (j < 0 || !ps.mesh[i]) continue;
                    var p = Rigid(ps.mesh[i]); if (p == null) continue;
                    var q = new Vector3[p.Length]; for (int k = 0; k < p.Length; k++) q[k] = ps.centre[i] + p[k];   // RobotPopDeath.ThrowBody: the part sits at bone.TransformPoint(centre), turned and scaled as the bone
                    bl.Add(j); pl.Add(q);
                }
                if (bl.Count > 0) { e = new Entry { bone = bl.ToArray(), pts = pl.ToArray() }; SkinsFromParts++; }
            }
            skins[m] = e; return e;   // null cached too: this skin is baked when asked
        }

        /// <summary>What counts as drawn (the Play checks' rule): an enabled mesh or skinned renderer, opaque (queue under 2450), not a shadow card,
        /// label, selection ring, marker, decal, glow or flame.</summary>
        public static bool Solid(Renderer r)
        {
            if (!r || !r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) return false;
            if (r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450) return false;
            var n = r.name.ToLowerInvariant();
            return !(n.Contains("shadow") || n.Contains("label") || n.Contains("selection") || n.Contains("ring") || n.Contains("marker") || n.Contains("decal") || n.Contains("glow") || n.Contains("flame"));
        }

        /// <summary>Every drawn point of what is under `root` now, in world space, into `into` (cleared first). `type` = the robot type (its body parts as
        /// the fallback); bakeSkins: a skin with neither bake nor parts is baked now (its lowest vertices), else left out. Returns the count.</summary>
        public static int Collect(Transform root, string type, List<Vector3> into, bool bakeSkins, List<Renderer> scratch)
        {
            into.Clear(); if (!root) return 0;
            scratch.Clear(); root.GetComponentsInChildren(false, scratch);
            SkinnedMeshRenderer main = null;
            foreach (var r in scratch) if (r is SkinnedMeshRenderer s && s.enabled && s.sharedMesh && (!main || s.sharedMesh.vertexCount > main.sharedMesh.vertexCount)) main = s;
            foreach (var r in scratch)
            {
                if (!Solid(r)) continue;
                if (r is SkinnedMeshRenderer sk) { AddSkin(sk, type, sk == main, into, bakeSkins); continue; }
                if (!r.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) continue;
                var p = Rigid(mf.sharedMesh); if (p == null) continue;
                var mt = r.transform.localToWorldMatrix;
                for (int i = 0; i < p.Length; i++) into.Add(mt.MultiplyPoint3x4(p[i]));
            }
            return into.Count;
        }

        static void AddSkin(SkinnedMeshRenderer sk, string type, bool main, List<Vector3> into, bool bakeSkins)
        {
            if (!sk.sharedMesh) return;
            var e = SkinOf(sk, type, main);
            if (e != null)
            {
                var bones = sk.bones;
                for (int g = 0; g < e.bone.Length; g++)
                {
                    int b = e.bone[g]; if (b < 0 || b >= bones.Length || !bones[b]) continue;
                    var mt = bones[b].localToWorldMatrix; var p = e.pts[g];
                    for (int i = 0; i < p.Length; i++) into.Add(mt.MultiplyPoint3x4(p[i]));
                }
                return;
            }
            if (bakeSkins) AddBaked(sk, into, 64);
        }

        /// <summary>The skin as it is drawn now (BakeMesh), its `keep` lowest vertices in world space (slow: Play checks and the fallback only).</summary>
        public static void AddBaked(SkinnedMeshRenderer sk, List<Vector3> into, int keep)
        {
            if (!sk || !sk.sharedMesh) return;
            if (!bake) bake = new Mesh { name = "OzDrawnPoints bake" };
            sk.BakeMesh(bake, true); vb.Clear(); bake.GetVertices(vb); SkinBakes++;
            var mt = sk.transform.localToWorldMatrix;
            AddLowest(vb, mt, into, keep);
        }

        /// <summary>The `keep` lowest (world y) of the points v under mt, added to `into`.</summary>
        public static void AddLowest(List<Vector3> v, Matrix4x4 mt, List<Vector3> into, int keep)
        {
            if (v.Count == 0) return;
            var low = new List<Vector3>(keep + 1); float top = float.MaxValue; int hi = -1;
            for (int i = 0; i < v.Count; i++)
            {
                var w = mt.MultiplyPoint3x4(v[i]);
                if (low.Count < keep) { low.Add(w); if (low.Count == keep) { hi = 0; for (int k = 1; k < keep; k++) if (low[k].y > low[hi].y) hi = k; top = low[hi].y; } continue; }
                if (w.y >= top) continue;
                low[hi] = w; hi = 0; for (int k = 1; k < keep; k++) if (low[k].y > low[hi].y) hi = k; top = low[hi].y;
            }
            into.AddRange(low);
        }

        /// <summary>The smallest gap of the points to the ground under each (ZGame.GroundY: terrain, roads, bridge and platform decks; the ground every
        /// robot piece lies on), and the point. exact: every point asks the ground; else only the points that can be the lowest over a plane through 9
        /// ground samples (every point when the samples show a step). wallM: a point more than this UNDER the ground there is inside a wall (a deck's side,
        /// a ramp's foot), not lying on it: left out. NaN when there is no point.</summary>
        public static float Gap(List<Vector3> pts, ZGame g, bool exact, out Vector3 low, float wallM = float.PositiveInfinity)
        {
            low = Vector3.zero; if (pts == null || pts.Count == 0 || !g) return float.NaN;
            float gap = float.MaxValue;
            if (!exact)
            {
                float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
                foreach (var p in pts) { x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z); }
                float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f, hx = Mathf.Max(0.5f, (x1 - x0) * 0.5f), hz = Mathf.Max(0.5f, (z1 - z0) * 0.5f);
                float gc = g.GroundY(cx, cz), gxp = g.GroundY(cx + hx, cz), gxn = g.GroundY(cx - hx, cz), gzp = g.GroundY(cx, cz + hz), gzn = g.GroundY(cx, cz - hz);
                float b = (gxp - gxn) / (2f * hx), c = (gzp - gzn) / (2f * hz);
                float res = Mathf.Max(Mathf.Abs((gxp + gxn) * 0.5f - gc), Mathf.Abs((gzp + gzn) * 0.5f - gc));
                for (int i = 0; i < 4; i++)
                {
                    float sx = (i & 1) == 0 ? -hx : hx, sz = (i & 2) == 0 ? -hz : hz;
                    res = Mathf.Max(res, Mathf.Abs(g.GroundY(cx + sx, cz + sz) - (gc + b * sx + c * sz)));
                }
                if (res < 0.25f)
                {   // smooth ground: only points within 2 x the plane's error (+10 cm) of the lowest over the plane can be the lowest over the ground
                    float minE = float.MaxValue;
                    foreach (var p in pts) minE = Mathf.Min(minE, p.y - (gc + b * (p.x - cx) + c * (p.z - cz)));
                    float lim = minE + 2f * res + 0.1f;
                    foreach (var p in pts)
                    {
                        if (p.y - (gc + b * (p.x - cx) + c * (p.z - cz)) > lim) continue;
                        float d = p.y - g.GroundY(p.x, p.z); if (d < gap && d >= -wallM) { gap = d; low = p; }
                    }
                    return gap == float.MaxValue ? float.NaN : gap;
                }
            }
            foreach (var p in pts) { float d = p.y - g.GroundY(p.x, p.z); if (d < gap && d >= -wallM) { gap = d; low = p; } }
            return gap == float.MaxValue ? float.NaN : gap;
        }
    }
}
