using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// k97b, owner item 176 (the gun falls apart at its death).
    /// Before: a dead gun (v1 and v2) stood whole and burning for Zod's 2-4 s (ECannonDeath), then its whole turret was tossed. Zod's own
    /// picture of those seconds is not a whole gun: its "wasted" gun (assets/units/cannons/gun/wasted.png) is a shattered gun with its bits
    /// lying round it. Now, at the death itself (the frame of the death blast):
    ///  * the barrel (CannonBarrelRecoil with its muzzle), the mantlet collar (CannonBarrelPitch_Surface) and the turret's outer parts fly
    ///    off as the game's wreck pieces (WreckPieces: rigid bodies with gravity, the ground and the rocks, the debris clank when they land,
    ///    a heavy part's clang and dust on screen; they settle and freeze, then sink by the wreck rules of View/WreckSinkGate.cs as one wreck
    ///    with the gun): the gun v2 its two bolted side frames, its box top with the hex block and its front panel; the gun v1 its two side
    ///    cheeks (with their pods and team strips) and its front plate;
    ///  * each is thrown by the blast inside the turret: the frames sideways (peeling outward), the barrel, the collar and the front panel
    ///    forward (the barrel tumbling end over end), the top up and off; 4.4-8 m/s up and at least 2.8 m/s out, so no part falls back onto
    ///    the base; seeded per gun (never two guns alike); the parts of one gun do not knock each other at the start;
    ///  * the two heaviest parts burn a few seconds (as a break's heaviest pieces do); one whoosh and one metal snap at the break;
    ///  * what stays on the base (v2: the back panel, the bottom and the neck ring; v1: the housing with its chair) is Zod's turret missile as
    ///    before: 2-4 s later it is tossed to the rules' debris point and lands when and where the rules' debris blast is (its 40 HP blast and
    ///    the trees it fells, DV124); v1's chair breaks off then (GunChairDebris); the base falls apart one in two (DV18) or stays as the
    ///    burnt wreck.
    /// The turret mesh is split once per model (cached; the meshes are shared by every death of that model) along the model's own surface
    /// patches (v2: the triangles that share vertices; v1: its closed boxes), each patch whole to one part by where its middle lies, so every
    /// edge is a modelled edge (no cut triangle, no spike); the open v2 shells (its side frames, open toward the box, its top, its front
    /// panel and the box left on the base) are drawn from both sides. Cost: the split once per model (<see cref="SplitMs"/>), then a handful of objects per death (<see cref="LastMs"/>);
    /// nothing per frame afterwards but the wreck pieces' own physics. View only: no rule, timing or random draw of the rules changes.
    /// <see cref="Enabled"/> = false: the gun stands whole until its turret is tossed (as before); <see cref="V1"/> = false: v2 only.
    /// </summary>
    public static partial class OzGunBreakup                                                          // k112, item 176: + View/OzGunBreakup.Prewarm.cs (the first break's work done ahead)
    {
        /// <summary>The switch (false = as before: the whole gun stands burning until its turret is tossed).</summary>
        public static bool Enabled = true;
        /// <summary>The original gun (v1) falls apart the same way (Zod's wasted gun is a broken gun); false = the gun v2 only.</summary>
        public static bool V1 = true;

        /// <summary>Proof counters: guns broken, parts thrown, turret splits made; the last break's cost (ms, without the split, which is
        /// counted on its own in SplitMs), the worst, the last split's own cost; the last break in words.</summary>
        public static int Breaks, PartsThrown, Splits;
        public static float LastMs, MaxMs, SplitMs;
        /// <summary>Guns whose static-solid colliders were dropped at the break (their death event had not dropped them yet: OzStaticSolid
        /// gives every gun part a collider of its drawn, concave shape for the big pieces of other deaths; riding on a thrown part it is a
        /// concave collider on a dynamic body, which the physics refuses with an error), and concave colliders still found on a thrown part
        /// and taken off (wanted 0). The thrown parts keep only their wreck piece's box (convex).</summary>
        public static int StaticsDropped, ConcaveDropped;
        public static string LastBreak = "";

        /// <summary>One thrown part of a recent break (the counted check samples them; nothing in a game reads them).</summary>
        public sealed class Part
        {
            public Rigidbody rb; public Transform t; public string kind; public int gun, model; public float t0, real0; public Vector3 p0, v0;
        }
        /// <summary>The parts of the last breaks, oldest first (at most <see cref="RecentMax"/>).</summary>
        public static readonly List<Part> Recent = new List<Part>();
        public const int RecentMax = 48;

        /// <summary>Where a patch's middle (turret frame, m; -z = the barrel's side) puts it: past frameX across = a side frame, over topY =
        /// the top, before frontZ = the front panel, else the part that stays; a welded piece wholly under lowY (the neck ring, whose faces
        /// reach 0.91 m out) always stays. weld = patches are the welded closed boxes (v1), else the triangles that share vertices (v2, one
        /// welded shell); twoSided = the open shells are drawn from both sides. Measured on the models (the kit's tools/parts_view.py).</summary>
        sealed class Spec { public bool weld, twoSided; public float frameX, topY, frontZ, lowY; }
        static readonly Spec V2Spec = new Spec { weld = false, twoSided = true, frameX = 0.72f, topY = 1.10f, frontZ = -0.79f, lowY = 0.25f };
        static readonly Spec V1Spec = new Spec { weld = true, twoSided = false, frameX = 0.85f, topY = 99f, frontZ = -0.75f, lowY = 0.25f };
        const int Core = 0, FrameL = 1, FrameR = 2, Top = 3, Front = 4, Kinds = 5, Barrel = 5, Collar = 6;
        static readonly string[] KindName = { "core", "frame_l", "frame_r", "top", "front", "barrel", "collar" };
        static readonly Dictionary<Mesh, Mesh[]> cache = new Dictionary<Mesh, Mesh[]>();
        static readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        /// <summary>UnitView.BeginWreck of a cannon (the death frame): breaks a gun's turret apart (`turret` = its CannonTurretYaw; what is
        /// left of it stays the view's turret, tossed at Zod's time). Returns the parts thrown (0 = not a gun, switched off, nothing found).</summary>
        public static int AtDeath(UnitView v, Transform turret, ZObject o)
        {
            if (!Enabled || WreckFx.Force == 2 || !v || !turret || !OzGun.Is(o)) return 0;              // (Force 2: the deaths test aid's plain wrecks)
            bool v2 = OzGun.Models.IsV2(v.transform);
            if (!v2 && !V1) return 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int id = o.Id; uint s = RobotDeathRule.Hash(id, 6, 176u) | 1u;
            if (OzStaticSolid.DropForBreak(id)) StaticsDropped++;                                          // its drawn-shape colliders (concave, for other deaths' big pieces) go now, before any part of it is a moving body
            float msDrop = (float)sw.Elapsed.TotalMilliseconds;
            float R() => WreckFx.Rnd(ref s);
            var tb = new Bounds(turret.position, Vector3.zero); bool any = false;
            foreach (var r in turret.GetComponentsInChildren<MeshRenderer>()) { if (!r.enabled) continue; if (!any) { tb = r.bounds; any = true; } else tb.Encapsulate(r.bounds); }
            var centre = tb.center;
            Vector3 up = Vector3.up, fwd = -turret.forward; fwd.y = 0f;                                   // the barrel's side is the turret's -z
            if (fwd.sqrMagnitude < 1e-4f) fwd = -v.transform.forward;
            fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward;
            var side = Vector3.Cross(up, fwd).normalized;

            var parts = new List<(Transform t, int kind)>();
            var recoil = Deep(turret, "CannonBarrelRecoil"); var collar = Deep(turret, "CannonBarrelPitch_Surface");
            if (recoil) { Quiet(recoil); parts.Add((recoil, Barrel)); }
            if (collar) { Quiet(collar); parts.Add((collar, Collar)); }
            var surf = Deep(turret, "CannonTurretYaw_Surface");
            var mf = surf ? surf.GetComponent<MeshFilter>() : null; var mr = surf ? surf.GetComponent<MeshRenderer>() : null;
            bool madeNow = false;
            var split = mf && mr && mr.enabled && mf.sharedMesh ? Split(mf.sharedMesh, v2 ? V2Spec : V1Spec, out madeNow) : null;
            if (split != null)
            {
                var holders = new Transform[Kinds];
                for (int k = 1; k < Kinds; k++)
                {
                    if (!split[k]) continue;
                    var go = new GameObject("OZ_GunPart_" + KindName[k]) { layer = mf.gameObject.layer };
                    go.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
                    go.transform.localScale = mf.transform.lossyScale;
                    go.AddComponent<MeshFilter>().sharedMesh = split[k];
                    CopyLook(mr, go.AddComponent<MeshRenderer>());
                    holders[k] = go.transform; parts.Add((go.transform, k));
                }
                mf.sharedMesh = split[Core];                                                                // the part that stays (tossed at Zod's time)
                var strips = new List<Transform>();
                foreach (Transform c in turret) if (c.name.StartsWith("Cannon_owner_strip")) strips.Add(c);   // (listed first: moving a child while walking them skips the next)
                foreach (var c in strips)
                {   // v1's team strips sit on its side pods: they go with the cheek of their side
                    var h = holders[mf.transform.InverseTransformPoint(c.position).x < 0f ? FrameL : FrameR];
                    if (h) c.SetParent(h, true);
                }
            }

            var made = new List<(Rigidbody rb, WreckPieces wp, Collider[] cols, Part rec)>();
            Vector3 barrelAt = centre;
            float msParts = (float)sw.Elapsed.TotalMilliseconds - (madeNow ? SplitMs : 0f);
            foreach (var (t, kind) in parts)
            {
                ConcaveDropped += OzStaticSolid.ConcaveOff(t);                                               // a moving body carries convex colliders only (its piece's box)
                if (!WreckPieces.Single(t, centre, 1f, id))
                {
                    if (kind < Kinds) Object.Destroy(t.gameObject);                                       // never: a split part is a real mesh
                    continue;
                }
                var piece = t.parent; if (!piece) continue;
                var rb = piece.GetComponent<Rigidbody>(); if (!rb) continue;
                Vector3 vel, spin, pc = piece.position;
                switch (kind)
                {
                    case Barrel:
                        barrelAt = pc;
                        vel = fwd * (3.2f + 1.2f * R()) + side * ((R() - 0.5f) * 2f) + up * (5.5f + 1.5f * R());
                        spin = side * ((R() < 0.5f ? -1f : 1f) * (5f + 3f * R())) + up * ((R() - 0.5f) * 2f);   // end over end
                        break;
                    case Collar:
                        vel = fwd * (2.8f + 1f * R()) + side * ((R() - 0.5f) * 1.6f) + up * (4.8f + 1.2f * R());
                        spin = new Vector3(R() - 0.5f, R() - 0.5f, R() - 0.5f).normalized * (4f + 2f * R());
                        break;
                    case FrameL: case FrameR:
                    {
                        var outward = Vector3.Dot(pc - centre, side) >= 0f ? side : -side;
                        vel = outward * (3.4f + 1.2f * R()) + fwd * ((R() - 0.5f) * 1.5f) + up * (4.4f + 1.2f * R());
                        spin = Vector3.Cross(up, outward) * (4f + 2f * R()) + up * ((R() - 0.5f) * 1.5f);   // its top peels outward
                        break;
                    }
                    case Top:
                    {
                        float a = R() * 6.2831853f; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        vel = d * (2.8f + 1f * R()) + up * (6.5f + 1.5f * R());
                        spin = Vector3.Cross(up, d) * (3f + 2f * R()) + up * ((R() - 0.5f) * 3f);
                        break;
                    }
                    default:                                                                                // the front panel
                        vel = fwd * (3f + 1f * R()) + side * ((R() - 0.5f) * 1.2f) + up * (4f + 1f * R());
                        spin = Vector3.Cross(up, fwd) * (4f + 2f * R());
                        break;
                }
                rb.linearVelocity = vel; rb.angularVelocity = spin; rb.WakeUp();
                var rec = new Part { rb = rb, t = piece, kind = KindName[kind], gun = id, model = v2 ? 2 : 1, t0 = Time.time, real0 = Time.unscaledTime, p0 = pc, v0 = vel };
                made.Add((rb, piece.GetComponent<WreckPieces>(), piece.GetComponentsInChildren<Collider>(), rec));
                Recent.Add(rec);
            }
            while (Recent.Count > RecentMax) Recent.RemoveAt(0);
            float msBodies = (float)sw.Elapsed.TotalMilliseconds - (madeNow ? SplitMs : 0f);
            for (int i = 0; i < made.Count; i++)                                                              // the parts of one gun never knock each other
                for (int j = i + 1; j < made.Count; j++)
                    foreach (var a in made[i].cols) foreach (var b in made[j].cols) if (a && b) Physics.IgnoreCollision(a, b, true);
            made.Sort((a, b) => b.rb.mass.CompareTo(a.rb.mass));
            for (int i = 0; i < made.Count && i < 2; i++) if (made[i].wp) made[i].wp.OzIgnite(3.5f + 2.5f * R());   // the two heaviest burn (burnPieces)
            if (made.Count > 0)
            {
                AudioDirector.Fire("dst_turret_toss", barrelAt, id);                                         // the barrel whooshes up
                AudioDirector.Fire("dst_debris_metal", centre, id);                                          // the parts snap off (they clank where they land)
            }
            sw.Stop();
            Breaks++; PartsThrown += made.Count;
            LastMs = (float)sw.Elapsed.TotalMilliseconds - (madeNow ? SplitMs : 0f); MaxMs = Mathf.Max(MaxMs, LastMs);   // the break itself; the split (once per model, on its first death) is SplitMs
            var names = new System.Text.StringBuilder();
            foreach (var m in made) names.Append(m.rec.kind).Append(' ');
            LastBreak = $"gun #{id} v{(v2 ? 2 : 1)}: {made.Count} parts ({names.ToString().Trim()}) in {LastMs:0.00} ms{(madeNow ? $" (its turret split made now, {SplitMs:0.00} ms)" : "")}" +
                        $" (its colliders off by {msDrop:0.00} ms, the parts made by {msParts:0.00} ms, their bodies by {msBodies:0.00} ms; statics dropped {StaticsDropped}, concave colliders taken off parts {ConcaveDropped})";
#if UNITY_EDITOR
            Debug.Log("[OpenZ] gun break-apart at its death: " + LastBreak);
#endif
            return made.Count;
        }

        /// <summary>The gun's own glows and effects on a part that flies are switched off (light quads, decals, particles: never debris).</summary>
        static void Quiet(Transform t)
        {
            foreach (var r in t.GetComponentsInChildren<Renderer>())
                if (!(r is MeshRenderer) || (r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450)) r.enabled = false;
        }

        static void CopyLook(MeshRenderer from, MeshRenderer to)
        {
            var mats = from.sharedMaterials;
            to.sharedMaterials = mats; to.shadowCastingMode = from.shadowCastingMode; to.receiveShadows = from.receiveShadows;
            to.lightProbeUsage = from.lightProbeUsage; to.reflectionProbeUsage = from.reflectionProbeUsage;
            to.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            block.Clear(); from.GetPropertyBlock(block); if (!block.isEmpty) to.SetPropertyBlock(block);
            for (int i = 0; i < mats.Length; i++) { block.Clear(); from.GetPropertyBlock(block, i); if (!block.isEmpty) to.SetPropertyBlock(block, i); }   // TeamPaint: per material slot (the paint, the burn)
        }

        static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Deep(c, name); if (f) return f; }
            return null;
        }

        // ------------------------------------------------------------------ the split (once per model)
        /// <summary>The turret mesh in Kinds parts (null = a part with nothing; index Core = what stays), or null (not readable / not one
        /// sub-mesh / nothing to throw). Cached per source mesh; made again if the cached meshes were unloaded.</summary>
        static Mesh[] Split(Mesh src, Spec sp, out bool made)
        {
            made = false;
            if (cache.TryGetValue(src, out var c) && (c == null || c[Core])) return c;
            cache[src] = null;
            if (!src.isReadable || src.subMeshCount != 1) return null;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var v = src.vertices; var tr = src.triangles; int nv = v.Length, nt = tr.Length / 3;
            var patch = Union(v, tr, sp.weld);                                                              // v1: its closed boxes; v2: the patches
            var island = sp.weld ? patch : Union(v, tr, true);                                              // the welded pieces (the lowY rule)
            var sum = new Vector4[nv]; var islandTop = new float[nv];
            for (int i = 0; i < nv; i++) islandTop[i] = float.NegativeInfinity;
            for (int t = 0; t < nt; t++)
            {   // each patch's middle, weighted by area; each welded piece's highest point
                int i0 = tr[3 * t], i1 = tr[3 * t + 1], i2 = tr[3 * t + 2];
                Vector3 p0 = v[i0], p1 = v[i1], p2 = v[i2];
                float a = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f + 1e-9f;
                var m = (p0 + p1 + p2) * (a / 3f);
                sum[patch[i0]] += new Vector4(m.x, m.y, m.z, a);
                int w = island[i0]; islandTop[w] = Mathf.Max(islandTop[w], Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y)));
            }
            var kindOf = new int[nt]; var count = new int[Kinds];
            for (int t = 0; t < nt; t++)
            {
                int i0 = tr[3 * t]; var q = sum[patch[i0]];
                int k = islandTop[island[i0]] < sp.lowY ? Core : KindOf(new Vector3(q.x, q.y, q.z) / q.w, sp);
                kindOf[t] = k; count[k]++;
            }
            if (count[Core] == 0 || count[Core] == nt) return null;                                         // nothing would stay / nothing to throw
            var data = new Src(src);
            var res = new Mesh[Kinds];
            for (int k = 0; k < Kinds; k++)
                if (count[k] > 0) res[k] = Build(src.name + "_" + KindName[k], data, tr, kindOf, k, sp.twoSided);   // v2's side frames too: their inner side (against the box) is open
            cache[src] = res;
            sw.Stop(); SplitMs = (float)sw.Elapsed.TotalMilliseconds; Splits++; made = true;
            return res;
        }

        /// <summary>Each vertex's group (its root, fully resolved): vertices joined by the triangles, and with weld also by their place (1 mm).</summary>
        static int[] Union(Vector3[] v, int[] tr, bool weld)
        {
            int nv = v.Length; var par = new int[nv]; for (int i = 0; i < nv; i++) par[i] = i;
            int Find(int x) { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; }
            if (weld)
            {
                var at = new Dictionary<Vector3Int, int>(nv);
                for (int i = 0; i < nv; i++)
                {
                    var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000f), Mathf.RoundToInt(v[i].y * 1000f), Mathf.RoundToInt(v[i].z * 1000f));
                    if (at.TryGetValue(k, out int j)) par[Find(i)] = Find(j); else at[k] = i;
                }
            }
            for (int i = 0; i + 2 < tr.Length; i += 3) { int a = Find(tr[i]); par[Find(tr[i + 1])] = a; par[Find(tr[i + 2])] = a; }
            for (int i = 0; i < nv; i++) par[i] = Find(i);
            return par;
        }

        static int KindOf(Vector3 c, Spec sp)
        {
            if (Mathf.Abs(c.x) > sp.frameX) return c.x < 0f ? FrameL : FrameR;
            if (c.y > sp.topY) return Top;
            if (c.z < sp.frontZ) return Front;
            return Core;
        }

        sealed class Src
        {
            public readonly Vector3[] v, n; public readonly Vector4[] t; public readonly Vector2[] u0, u1; public readonly Color[] c;
            public readonly bool hn, ht, h0, h1, hc;
            public Src(Mesh m)
            {
                v = m.vertices; n = m.normals; t = m.tangents; u0 = m.uv; u1 = m.uv2; c = m.colors;
                hn = n != null && n.Length == v.Length; ht = t != null && t.Length == v.Length; h0 = u0 != null && u0.Length == v.Length;
                h1 = u1 != null && u1.Length == v.Length; hc = c != null && c.Length == v.Length;
            }
        }

        /// <summary>The triangles of one part as their own mesh (only the vertices they use); twoSided adds the back faces (the same corners,
        /// the normals turned, the winding reversed).</summary>
        static Mesh Build(string name, Src s, int[] tr, int[] kindOf, int kind, bool twoSided)
        {
            var map = new int[s.v.Length]; for (int i = 0; i < map.Length; i++) map[i] = -1;
            var pv = new List<Vector3>(); var pn = new List<Vector3>(); var pt = new List<Vector4>(); var p0 = new List<Vector2>(); var p1 = new List<Vector2>(); var pc = new List<Color>();
            var idx = new List<int>();
            for (int t = 0; t < kindOf.Length; t++)
            {
                if (kindOf[t] != kind) continue;
                for (int j = 0; j < 3; j++)
                {
                    int i = tr[3 * t + j];
                    if (map[i] < 0)
                    {
                        map[i] = pv.Count; pv.Add(s.v[i]);
                        if (s.hn) pn.Add(s.n[i]); if (s.ht) pt.Add(s.t[i]); if (s.h0) p0.Add(s.u0[i]); if (s.h1) p1.Add(s.u1[i]); if (s.hc) pc.Add(s.c[i]);
                    }
                    idx.Add(map[i]);
                }
            }
            if (twoSided)
            {
                int nb = pv.Count, ni = idx.Count;
                for (int i = 0; i < nb; i++)
                {
                    pv.Add(pv[i]);
                    if (s.hn) pn.Add(-pn[i]); if (s.ht) { var q = pt[i]; pt.Add(new Vector4(q.x, q.y, q.z, -q.w)); } if (s.h0) p0.Add(p0[i]); if (s.h1) p1.Add(p1[i]); if (s.hc) pc.Add(pc[i]);
                }
                for (int i = 0; i + 2 < ni; i += 3) { idx.Add(idx[i] + nb); idx.Add(idx[i + 2] + nb); idx.Add(idx[i + 1] + nb); }
            }
            var m = new Mesh { name = name, indexFormat = pv.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(pv);
            if (s.hn) m.SetNormals(pn); if (s.ht) m.SetTangents(pt); if (s.h0) m.SetUVs(0, p0); if (s.h1) m.SetUVs(1, p1); if (s.hc) m.SetColors(pc);
            m.SetTriangles(idx, 0, false);
            if (!s.hn) m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
