// OpenZ (GPL-3.0)
//  * Colour: a chip's colour went into the renderer-wide property block, but the glow fade of thrown pieces (OzGlowFade.Dim, called on the
//    first frame because TeamLit declares _OZTeamGlow) writes a block for material slot 0, which wins over the renderer-wide one: the chip
//    was drawn with the piece material's own white. Now every chip's values sit in the slot-0 block, which Dim reads and keeps.
//  * Texture: the chip carries the struck part's own material and paint block, its texture taken round the point that was hit (the
//    struck triangle's texture point and scale), painted face out, the broken sides and back dark (vertex colours).
//  * Sizes: one big chunk, two medium pieces, the rest small flakes, all chunky (thickness at least about a third of the width).
//  * Start: on the surface at the burst point, thrown out along that surface's normal (towards the shooter's side), fanned sideways.
// View only: no rule of the game reads any of it.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public partial class RobotPopDeath
    {
        /// <summary>Chips thrown off a struck vehicle / gun (all) and those with the struck part's own material (the rest: no readable
        /// part at the hit point, the gunmetal / team colour of the piece material).</summary>
        public static int HullChipsThrown { get; private set; }
        public static int HullChipsTextured { get; private set; }
        /// <summary>1.5 kit chips_rts_read (review FAIL 12: at the RTS camera the chips read as tiny dark specks): every chip's size x ChipScale
        /// (one chunk 0.6-0.8 m, two pieces 0.4-0.55 m, flakes 0.2-0.3 m), thrown a little slower (ChipSpeedK); the torn sides and back lighter
        /// worn metal (no black specks face down); a hit with no TeamLit triangle within 0.6 m takes the hull's nearest one within FarReach m
        /// (its armour texture, never the plain piece colour). Same pieces, same count, same pool.</summary>
        public static float ChipScale = 1.85f, ChipSpeedK = 0.85f, FarReach = 3f;
        /// <summary>1.5 last_small_fixes (final eye pass): the chip's window on the struck
        /// part's texture is ChipUvK x its size there, at most ChipUvMax of the texture (the heavy's albedos are 81-91 % near-black atlas space
        /// and gunmetal: a wide window drew mostly black), and stays near the hit point.</summary>
        public static float ChipUvK = 0.3f, ChipUvMax = 0.02f, ChipUvJitter = 0.25f;
        public static int HullChipsFar { get; private set; }
        static float hullReach = 0.6f;
        /// <summary>The last throw, for the Play check: where it burst, the surface normal used, the struck part's name ("" = none).</summary>
        public static Vector3 LastHullAt, LastHullNormal;
        public static string LastHullPart = "";
        public static readonly List<HullChipNote> LastHullChips = new List<HullChipNote>();
        public struct HullChipNote { public MeshRenderer r; public Vector3 start, v, size; public bool textured; public Vector4 st; }

        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST"), UseVcId = Shader.PropertyToID("_UseVertexColor"),
                            VcSrgbId = Shader.PropertyToID("_VertexColorSRGB"), EmptyDiscId = Shader.PropertyToID("_OZEmptyDisc");
        MaterialPropertyBlock chipBlock;
        Mesh[] chipMesh;

        /// <summary>HC1 on a vehicle / gun: n chips off the drawn surface of `root` at `at` (the burst point), thrown out of that surface.</summary>
        public static void HullChips(Transform root, Vector3 at, Vector3 from, Vector3 away, Color col, int n, uint seed)
        {
            if (!ZGame.I || n <= 0) return;
            I.DoHullChips(root, at, from, away, col, n, seed);
        }

        void DoHullChips(Transform root, Vector3 at, Vector3 from, Vector3 away, Color col, int n, uint seed)
        {
            if (!EnsureKit(null)) return;
            if (chipMesh == null) chipMesh = new[] { Chip(5, 11u), Chip(6, 23u), Chip(7, 37u), Chip(6, 51u) };
            bool hit = HullPoint(root, at, from, out var src, out int slot, out var huv, out float uvPerM, out var nrm) || HullPointFar(root, at, from, out src, out slot, out huv, out uvPerM, out nrm);   // 1.5 kit chips_rts_read: the nearest armour within FarReach
            if (hit && UpperPaint(root, at, ref src, ref slot, ref huv, ref uvPerM)) HullChipsUpper++;   // 1.5 pass 4: the paint the RTS camera sees at the hit (not the dark lower hull / track skirt)
            rs = seed | 1u; R();
            if (nrm.sqrMagnitude < 0.5f) { nrm = from - at; nrm.y = 0f; nrm = nrm.sqrMagnitude > 1e-4f ? nrm.normalized : Vector3.up; }   // no surface found: back towards the shooter
            var t1 = Vector3.Cross(nrm, Vector3.up); if (t1.sqrMagnitude < 0.04f) t1 = Vector3.Cross(nrm, Vector3.right); t1.Normalize();
            var t2 = Vector3.Cross(t1, nrm).normalized;
            var mat = hit ? src.sharedMaterials[slot] : pieceMat;
            var st0 = new Vector4(1f, 1f, 0f, 0f);
            if (hit && mat.HasProperty("_BaseMap")) { var sc = mat.GetTextureScale("_BaseMap"); var of = mat.GetTextureOffset("_BaseMap"); st0 = new Vector4(sc.x, sc.y, of.x, of.y); }
            var gunC = new Color(0.42f, 0.44f, 0.47f);
            chipBlock ??= new MaterialPropertyBlock();
            LastHullAt = at; LastHullNormal = nrm; LastHullPart = hit ? src.name : ""; LastHullChips.Clear();
            for (int i = 0; i < n; i++)
            {
                var p = Take(); if (p == null) break;
                bool big = i == 0, med = i == 1 || (i == 2 && n > 3);                                    // one chunk, two pieces, the rest flakes (3 chips: chunk, piece, flake)
                float w = (big ? R(0.34f, 0.46f) : med ? R(0.2f, 0.3f) : R(0.09f, 0.15f)) * ChipScale;   // 1.5 kit chips_rts_read: they read at the RTS camera
                float th = (big ? R(0.12f, 0.16f) : med ? R(0.075f, 0.1f) : R(0.045f, 0.065f)) * ChipScale;   // 1.5 kit chips_rts_read (never thinner than ~1/3 of the width: no paper)
                var s = new Vector3(w, th, w * R(0.65f, 0.95f));
                p.mf.sharedMesh = chipMesh[(int)(R() * chipMesh.Length) % chipMesh.Length]; p.mr.sharedMaterial = mat;
                Vector4 st = Vector4.zero;
                if (hit)
                {
                    src.GetPropertyBlock(chipBlock, slot);                                                  // the struck part's paint (team, empty look, char)
                    float k = Mathf.Clamp(w * uvPerM * ChipUvK, 0.002f, ChipUvMax);                         // the chip's size on the part's texture (1.5 last_small_fixes: was w x uvPerM, 0.004-0.25)
                    var c0 = huv + new Vector2(R(-1f, 1f), R(-1f, 1f)) * k * ChipUvJitter;                  // round the point that was hit (1.5 last_small_fixes: was 0.6)
                    st = new Vector4(st0.x * k, st0.y * k, st0.x * (c0.x - k * 0.5f) + st0.z, st0.y * (c0.y - k * 0.5f) + st0.w);
                    chipBlock.SetVector(BaseMapStId, st);
                    chipBlock.SetVector(EmptyDiscId, Vector4.zero);                                         // the heavy v2's drawn hatch disc is in turret mesh space, not a chip's
                }
                else { chipBlock.Clear(); chipBlock.SetColor(BaseColorId, i % 2 == 0 ? col : gunC); }
                chipBlock.SetFloat(UseVcId, 1f); chipBlock.SetFloat(VcSrgbId, 0f);                          // the painted face as is, the torn sides and back dark
                p.mr.SetPropertyBlock(chipBlock, 0);                                                        // slot 0: the block OzGlowFade.Dim reads and keeps
                if (hit) OzFireHeat.ColdCopy(p.mr, 1);                                                      // a burning hull's heat stays on the hull
                p.mr.shadowCastingMode = big || med ? ShadowCastingMode.On : ShadowCastingMode.Off;
                var start = at + nrm * 0.05f + (t1 * R(-1f, 1f) + t2 * R(-1f, 1f)) * 0.08f;
                var face = Quaternion.LookRotation(t1, nrm) * Quaternion.Euler(R(-25f, 25f), R(0f, 360f), R(-25f, 25f));   // painted face out, as it sat
                p.t.SetPositionAndRotation(start, face); p.t.localScale = s;
                p.rad = Mathf.Clamp(Mathf.Min(s.x, Mathf.Min(s.y, s.z)) * 0.5f + 0.03f, 0.04f, 0.4f);
                Launch(p, at, away, 0.85f, 1f);
                float sp = (big ? R(2.2f, 3.4f) : med ? R(3f, 4.6f) : R(3.8f, 6f)) * ChipSpeedK;   // 1.5 kit chips_rts_read: bigger pieces fly a little slower (the small flakes fastest)
                var dir = (nrm + t1 * R(-0.55f, 0.55f) + t2 * R(-0.3f, 0.55f)).normalized;
                p.v = dir * sp + Vector3.up * R(1.6f, 3.2f);
                p.w = Random.onUnitSphere * (big ? R(200f, 450f) : R(400f, 900f));
                AsChip(p);   // 1.5 kit chips_ground_rest: lands flat ON the surface, lies 8-12 s, sinks slowly (View/RobotPopDeath.cs)
                ChipsThrown++; HullChipsThrown++; if (hit) HullChipsTextured++;
                LastHullChips.Add(new HullChipNote { r = p.mr, start = start, v = p.v, size = s, textured = hit, st = st });
            }
        }

        /// <summary>A retired piece keeps no per-slot block: its next use (a robot's piece with a renderer-wide colour) is not overruled.</summary>
        static void ClearSlotBlocks(Piece p)
        {
            if (p == null || !p.mr) return;
            int n = p.mr.sharedMaterials.Length;
            for (int m = 0; m < n; m++) p.mr.SetPropertyBlock(null, m);
        }

        // ------------------------------------------------------------------------------------------------ the struck point on the drawn hull
        sealed class HullMesh { public Vector3[] v; public Vector2[] uv; public int[][] sub; }
        static readonly Dictionary<Mesh, HullMesh> hullMeshes = new Dictionary<Mesh, HullMesh>();
        static readonly List<MeshRenderer> hullRs = new List<MeshRenderer>();
        static readonly List<Vector3> tmpV = new List<Vector3>();
        static readonly List<Vector2> tmpUv = new List<Vector2>();

        static HullMesh Data(Mesh m)
        {
            if (hullMeshes.TryGetValue(m, out var d)) return d;
            if (hullMeshes.Count > 96) hullMeshes.Clear();
            d = null;
            if (m.isReadable && m.HasVertexAttribute(VertexAttribute.TexCoord0))
            {
                tmpV.Clear(); m.GetVertices(tmpV); tmpUv.Clear(); m.GetUVs(0, tmpUv);
                if (tmpV.Count > 0 && tmpUv.Count == tmpV.Count)
                {
                    d = new HullMesh { v = tmpV.ToArray(), uv = tmpUv.ToArray(), sub = new int[m.subMeshCount][] };
                    for (int s = 0; s < m.subMeshCount; s++) d.sub[s] = m.GetSubMesh(s).topology == MeshTopology.Triangles ? m.GetTriangles(s) : new int[0];
                }
            }
            hullMeshes[m] = d;
            return d;
        }

        static bool Solid(Material m) => m && m.shader && m.shader.name == "OpenZ/TeamLit" && m.renderQueue < 2450;

        /// <summary>The drawn TeamLit triangle of `root` nearest `at` (within 0.6 m): its part, material slot, texture point, texture units
        /// per metre and its outward normal (turned to the shooter's side). False: no readable part there.</summary>
        static bool HullPoint(Transform root, Vector3 at, Vector3 from, out MeshRenderer best, out int slot, out Vector2 uv, out float uvPerM, out Vector3 nrm)
        {
            best = null; slot = 0; uv = default; uvPerM = 0f; nrm = Vector3.zero;
            if (!root) return false;
            float bestW = hullReach * hullReach;   // 1.5 kit chips_rts_read: 0.6 m, FarReach in HullPointFar
            hullRs.Clear(); root.GetComponentsInChildren(false, hullRs);
            foreach (var mr in hullRs)
            {
                if (!mr.enabled || mr.bounds.SqrDistance(at) > bestW) continue;
                if (!mr.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) continue;
                var mats = mr.sharedMaterials;
                var d = Data(mf.sharedMesh); if (d == null) continue;
                var tr = mr.transform; var ls = tr.lossyScale;
                float sc = Mathf.Max(1e-4f, (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f);
                var lp = tr.InverseTransformPoint(at);
                for (int s = 0; s < d.sub.Length && s < mats.Length; s++)
                {
                    if (!Solid(mats[s])) continue;
                    var t = d.sub[s];
                    for (int i = 0; i + 2 < t.Length; i += 3)
                    {
                        Vector3 a = d.v[t[i]], b = d.v[t[i + 1]], c = d.v[t[i + 2]];
                        float rl = Mathf.Sqrt(bestW) / sc;                                                 // cheap box reject in mesh space
                        if (Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - lp.x > rl || lp.x - Mathf.Max(a.x, Mathf.Max(b.x, c.x)) > rl ||
                            Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - lp.y > rl || lp.y - Mathf.Max(a.y, Mathf.Max(b.y, c.y)) > rl ||
                            Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - lp.z > rl || lp.z - Mathf.Max(a.z, Mathf.Max(b.z, c.z)) > rl) continue;
                        var q = Closest(lp, a, b, c, out float wa, out float wb, out float wc);
                        float dd = (q - lp).sqrMagnitude * sc * sc;
                        if (dd >= bestW) continue;
                        bestW = dd; best = mr; slot = s;
                        uv = d.uv[t[i]] * wa + d.uv[t[i + 1]] * wb + d.uv[t[i + 2]] * wc;
                        Vector3 e1 = tr.TransformVector(b - a), e2 = tr.TransformVector(c - a);
                        var cr = Vector3.Cross(e1, e2); float area = cr.magnitude;
                        Vector2 u1 = d.uv[t[i + 1]] - d.uv[t[i]], u2 = d.uv[t[i + 2]] - d.uv[t[i]];
                        float uvArea = Mathf.Abs(u1.x * u2.y - u1.y * u2.x);
                        uvPerM = area > 1e-6f && uvArea > 1e-9f ? Mathf.Clamp(Mathf.Sqrt(uvArea / area), 0.02f, 2f) : 0.2f;
                        nrm = area > 1e-6f ? cr / area : Vector3.zero;
                    }
                }
            }
            if (!best) return false;
            if (Vector3.Dot(nrm, from - at) < 0f) nrm = -nrm;                                               // the face the shell came at
            return true;
        }

        /// <summary>
        /// just above and inside the hit (the deck edge / upper side the RTS camera sees), when there is one within reach; else the hit's own.</summary>
        public static int HullChipsUpper;
        public static float UpperUp = 0.55f, UpperIn = 0.35f;
        static bool UpperPaint(Transform root, Vector3 at, ref MeshRenderer src, ref int slot, ref Vector2 huv, ref float uvPerM)
        {
            var inward = root.position - at; inward.y = 0f; inward = inward.sqrMagnitude > 1e-4f ? inward.normalized : Vector3.zero;
            for (int k = 1; k <= 2; k++)
            {
                var q = at + Vector3.up * (UpperUp * k) + inward * (UpperIn * k);
                if (HullPoint(root, q, q + Vector3.up * 3f, out var s2, out int sl2, out var uv2, out float upm2, out var n2) && n2.y > 0.35f)
                { src = s2; slot = sl2; huv = uv2; uvPerM = upm2; return true; }
            }
            return false;
        }

        /// <summary>1.5 kit chips_rts_read: HullPoint with the reach FarReach (no TeamLit triangle within 0.6 m of the burst): the struck hull's
        /// nearest armour gives the texture; the normal is left to the caller (back towards the shooter).</summary>
        static bool HullPointFar(Transform root, Vector3 at, Vector3 from, out MeshRenderer best, out int slot, out Vector2 uv, out float uvPerM, out Vector3 nrm)
        {
            hullReach = FarReach;
            bool ok;
            try { ok = HullPoint(root, at, from, out best, out slot, out uv, out uvPerM, out nrm); }
            finally { hullReach = 0.6f; }
            nrm = Vector3.zero;
            if (ok) HullChipsFar++;
            return ok;
        }

        /// <summary>The point of triangle abc nearest p, with its barycentric weights (Ericson, Real-Time Collision Detection 5.1.5).</summary>
        static Vector3 Closest(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out float wa, out float wb, out float wc)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) { wa = 1f; wb = wc = 0f; return a; }
            var bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) { wb = 1f; wa = wc = 0f; return b; }
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) { float v = d1 / (d1 - d3); wa = 1f - v; wb = v; wc = 0f; return a + ab * v; }
            var cp = p - c; float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) { wc = 1f; wa = wb = 0f; return c; }
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) { float w = d2 / (d2 - d6); wa = 1f - w; wc = w; wb = 0f; return a + ac * w; }
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) { float w = (d4 - d3) / ((d4 - d3) + (d5 - d6)); wb = 1f - w; wc = w; wa = 0f; return b + (c - b) * w; }
            float den = 1f / (va + vb + vc); float vv = vb * den, ww = vc * den;
            wa = 1f - vv - ww; wb = vv; wc = ww;
            return a + ab * vv + ac * ww;
        }

        // ------------------------------------------------------------------------------------------------ the chip mesh
        /// <summary>A chunky armour chip, unit size (-.5..+.5): an uneven n-gon plate with a chamfered top edge. The top (+y) is the
        /// painted face (vertex colour 1, texture 0..1 over its outline), the chamfer a scuffed edge (0.6), the torn sides (0.3) and the
        /// back (0.22) dark metal; flat shaded.</summary>
        static Mesh Chip(int n, uint seed)
        {
            uint r = seed | 1u;
            float Rn() { r ^= r << 13; r ^= r >> 17; r ^= r << 5; return (r & 0xFFFFFF) / 16777216f; }
            var outer = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = (i + (Rn() - 0.5f) * 0.5f) * Mathf.PI * 2f / n, rad = 0.5f * (0.78f + 0.22f * Rn());
                outer[i] = new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad);
            }
            const float top = 0.5f, shoulder = 0.28f, bottom = -0.5f, inset = 0.8f;
            var mv = new List<Vector3>(); var mn = new List<Vector3>(); var mc = new List<Color>(); var mu = new List<Vector2>(); var mt = new List<int>();
            Vector2 Uv(Vector3 p) => new Vector2(p.x + 0.5f, p.z + 0.5f);
            void Tri(Vector3 a, Vector3 b, Vector3 c, float shade)
            {
                var nn = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(nn, (a + b + c) / 3f) < 0f) { var x = b; b = c; c = x; nn = -nn; }   // outward (the shape is star-shaped round its middle)
                var col = new Color(shade, shade, shade, 1f);
                foreach (var p in new[] { a, b, c }) { mt.Add(mv.Count); mv.Add(p); mn.Add(nn); mc.Add(col); mu.Add(Uv(p)); }
            }
            var ct = new Vector3(0f, top, 0f); var cb = new Vector3(0f, bottom, 0f);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 oi = new Vector3(outer[i].x, shoulder, outer[i].y), oj = new Vector3(outer[j].x, shoulder, outer[j].y);
                Vector3 ti = new Vector3(outer[i].x * inset, top, outer[i].y * inset), tj = new Vector3(outer[j].x * inset, top, outer[j].y * inset);
                Vector3 bi = new Vector3(outer[i].x, bottom, outer[i].y), bj = new Vector3(outer[j].x, bottom, outer[j].y);
                Tri(ct, ti, tj, 1f);                                                                         // painted face
                Tri(ti, oi, oj, 0.8f); Tri(ti, oj, tj, 0.8f);                                                // scuffed chamfer   // 1.5 kit chips_rts_read: 0.6 -> 0.8
                Tri(oi, bi, bj, 0.55f); Tri(oi, bj, oj, 0.55f);                                              // torn side   // 1.5 kit chips_rts_read: 0.3 -> 0.55 (worn metal, not a black speck)
                Tri(cb, bj, bi, 0.62f);                                                                      // back   // 1.5 kit chips_rts_read: 0.22 -> 0.62 (a chip lying face down still reads as armour)
            }
            var m = new Mesh { name = "OZ_HullChip" + n };
            m.SetVertices(mv); m.SetNormals(mn); m.SetColors(mc); m.SetUVs(0, mu); m.SetTriangles(mt, 0); m.RecalculateBounds();
            return m;
        }
    }
}
