// OpenZ (GPL-3.0)
// View only.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// A dead gun's seat / chair kit rests on what holds it up, like the vehicle wrecks (kit wreck_rest_ground_r6).
    /// <para>The cause: GunChairDebris.Split (View/CannonPolish.cs) cut the seat kit off the turret mesh as a COPY of the whole turret mesh with only
    /// the seat's triangles (Object.Instantiate + SetTriangles): every vertex of the turret stayed in it, unused, and the turret kept the seat's
    /// vertices the same way. Every rule that measures a wreck by its points reads ALL the vertices of a mesh (OzWreckGround.Lowest / Sample /
    /// Outer for a wreck piece, OzWreckTurretRest.Picked for a thrown turret): the seat piece was measured with the whole turret's points round it,
    /// so its "lowest point" was a turret point it does not draw, up to a metre and more under the seat; the support rules saw it lying (or in
    /// the ground) while the drawn seat stood in the air, and never brought it down. The thrown turret was measured with the seat's points the
    /// same way. And the imported guns' turret parts carry the WHOLE model's box as their mesh bounds (OpenZTurretSplit: TeamPaint's accent band),
    /// so a part of them that becomes a wreck piece got a box collider of the whole gun (WreckPieces.Single / Break size the collider from the
    /// renderer's bounds): it lay on that box with the drawn part in the air.</para>
    /// <para>The fix: (1) <see cref="Compact"/>: the seat and the turret that is left each keep only the vertices their own triangles use (every vertex
    /// channel kept; the turret keeps its old mesh bounds so its paint band is unchanged, the seat gets its own box); (2) <see cref="World"/>: the box
    /// of a gun wreck piece (its seat kit, a gun's part broken off) is the box of what it draws, when its mesh bounds are bigger than that.
    /// Every gun with a seat: gatling, howitzer, missile cannon (their modelled seat kits), the gun v1 (its chair), a tower gun's raised perch and the
    /// old outrigger saddle (their own nodes); the gun v2 has no chair (GunV2Seat.NoChair). The fort's big cannon has no seat. Nothing of a live
    /// gun changes (the split is made when the turret is thrown).</para>
    /// <see cref="On"/> = false: exactly as before (the old split and boxes).
    /// </summary>
    public static class OzGunSeatWreck
    {
        public static bool On = true;
        /// <summary>Set by GunChairDebris.BreakOff while it makes the seat pieces.</summary>
        public static bool Fit;
        /// <summary>Proof: splits made compact, the vertices left out of them, piece boxes fitted to what they draw, the most a box shrank (m).</summary>
        public static int Compacted, VertsLeftOut, BoxesFitted; public static float MaxBoxCutM;

        static readonly Dictionary<Mesh, (bool ok, Bounds b)> drawn = new Dictionary<Mesh, (bool ok, Bounds b)>();
        static readonly List<Vector3> vb = new List<Vector3>();
        static readonly List<int> ib = new List<int>();

        /// <summary>A gun's wreck (WreckPieces.Break of a dead cannon).</summary>
        public static bool Gun(UnitView v) => On && v && v.Obj != null && v.Obj.Kind == ZKind.Cannon;

        /// <summary>The box of what `r` draws (world). As r.bounds unless `fit` and its mesh's bounds are more than 1 cm bigger than the vertices its
        /// triangles use.</summary>
        public static Bounds World(Renderer r, bool fit)
        {
            var wb = r.bounds;
            if (!On || !fit || !(r is MeshRenderer) || !r.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh || !DrawnLocal(mf.sharedMesh, out var lb)) return wb;
            var mb = mf.sharedMesh.bounds;
            if ((lb.min - mb.min).sqrMagnitude < 1e-4f && (lb.max - mb.max).sqrMagnitude < 1e-4f) return wb;      // its box is what it draws: as before
            var m = r.localToWorldMatrix; Vector3 c = lb.center, e = lb.extents;
            var w = new Bounds(m.MultiplyPoint3x4(c), Vector3.zero);
            for (int k = 0; k < 8; k++) w.Encapsulate(m.MultiplyPoint3x4(c + Vector3.Scale(e, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f))));
            BoxesFitted++; MaxBoxCutM = Mathf.Max(MaxBoxCutM, wb.size.magnitude - w.size.magnitude);
            return w;
        }

        /// <summary>The local box of the vertices the mesh's triangles use (kept per mesh); false = not readable or nothing drawn.</summary>
        public static bool DrawnLocal(Mesh m, out Bounds b)
        {
            b = default;
            if (!m) return false;
            if (drawn.TryGetValue(m, out var c)) { b = c.b; return c.ok; }
            bool any = false;
            if (m.isReadable)
            {
                vb.Clear(); m.GetVertices(vb);
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    if (m.GetTopology(s) != MeshTopology.Triangles) continue;
                    ib.Clear(); m.GetIndices(ib, s);
                    for (int i = 0; i < ib.Count; i++)
                    {
                        int k = ib[i]; if (k < 0 || k >= vb.Count) continue;
                        if (!any) { b = new Bounds(vb[k], Vector3.zero); any = true; } else b.Encapsulate(vb[k]);
                    }
                }
            }
            drawn[m] = (any, b);
            return any;
        }

        /// <summary>A mesh of `src`'s triangles `tris` (one submesh) with only the vertices they use, every vertex channel kept (positions,
        /// normals, tangents, colours, every UV set). keepBounds: src's own mesh bounds (the paint band of the part that stays), else its own box.</summary>
        public static Mesh Compact(Mesh src, List<int> tris, string name, bool keepBounds)
        {
            int nv = src.vertexCount;
            var map = new int[nv]; for (int i = 0; i < nv; i++) map[i] = -1;
            var order = new List<int>(); var idx = new int[tris.Count];
            for (int i = 0; i < tris.Count; i++) { int v = tris[i]; if (map[v] < 0) { map[v] = order.Count; order.Add(v); } idx[i] = map[v]; }
            var m = new Mesh { name = name, indexFormat = order.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            var v3 = new List<Vector3>(nv); src.GetVertices(v3); m.SetVertices(Pick(v3, order));
            if (src.HasVertexAttribute(VertexAttribute.Normal)) { v3.Clear(); src.GetNormals(v3); if (v3.Count == nv) m.SetNormals(Pick(v3, order)); }
            if (src.HasVertexAttribute(VertexAttribute.Tangent)) { var v4 = new List<Vector4>(nv); src.GetTangents(v4); if (v4.Count == nv) m.SetTangents(Pick(v4, order)); }
            if (src.HasVertexAttribute(VertexAttribute.Color)) { var cl = new List<Color>(nv); src.GetColors(cl); if (cl.Count == nv) m.SetColors(Pick(cl, order)); }
            for (int ch = 0; ch < 8; ch++)
            {
                var a = (VertexAttribute)((int)VertexAttribute.TexCoord0 + ch);
                if (!src.HasVertexAttribute(a)) continue;
                int d = src.GetVertexAttributeDimension(a);
                if (d <= 2) { var u = new List<Vector2>(nv); src.GetUVs(ch, u); if (u.Count == nv) m.SetUVs(ch, Pick(u, order)); }
                else if (d == 3) { var u = new List<Vector3>(nv); src.GetUVs(ch, u); if (u.Count == nv) m.SetUVs(ch, Pick(u, order)); }
                else { var u = new List<Vector4>(nv); src.GetUVs(ch, u); if (u.Count == nv) m.SetUVs(ch, Pick(u, order)); }
            }
            m.SetTriangles(idx, 0, false);   // (checker 15 Nov: colours copied as float Color, never cut to 8 bits: the imported guns store them as floats)
            if (keepBounds) m.bounds = src.bounds; else m.RecalculateBounds();
            Compacted++; VertsLeftOut += nv - order.Count;
            return m;
        }

        static List<T> Pick<T>(List<T> all, List<int> order)
        {
            var o = new List<T>(order.Count);
            for (int i = 0; i < order.Count; i++) o.Add(all[order[i]]);
            return o;
        }

        public static string Status() => $"gun seat wreck {(On ? "on" : "off")}: compact splits {Compacted} (unused vertices left out {VertsLeftOut}), piece boxes fitted to what they draw {BoxesFitted} (most cut {MaxBoxCutM:0.00} m)";
    }
}
