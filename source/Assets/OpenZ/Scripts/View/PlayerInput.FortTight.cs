using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View and input only, no
    /// rule changes: the ENTER_FORT order itself is as before.
    /// <para>Why: an enemy fort became the hovered target (and the cursor ENTER) in three ways that reach past the fort itself: (1) its pick box
    /// is the screen rectangle round its whole model (towers included), so the ground and the road beside its back wall and between its towers
    /// counted as the fort; (2) 1.4's gate zone on the ground (DV168's 4 tiles round the gate's exit point and the gate mouth) turned bare road
    /// in front of the gate into the fort, so the cursor came up well before the pointer reached the fort and stayed while it moved away along
    /// the road; (3) the door's box reached 2 m out of the gate and 1.5 m past each side and over the top.</para>
    /// <para>Now the cursor is ENTER only while the pointer is really over the fort: (1) inside its screen box the pointer's ray must hit one of
    /// the fort's own drawn parts (each solid part's own mesh box in its own frame, as the unit pick boxes are made from the real model:
    /// <see cref="FortUnder"/>), else the fort is not hovered (the pointer is beside it: a plain move); (2) the bare ground round the gate is no
    /// longer the fort; (3) the door's box is the door's own picture (<see cref="DoorSideTightM"/> past each side, <see cref="DoorTopTightM"/>
    /// over it, <see cref="DoorOutTightM"/> out). A right-click gives exactly what the cursor shows (the same hover). Every enemy fort, every
    /// level, infantry or vehicles selected. <see cref="FortTightOn"/> off = 1.4's zone as before.</para>
    /// </summary>
    public partial class PlayerInput
    {
        /// <summary>N21: the fort is the target only where the pointer really is over it (on).</summary>
        public static bool FortTightOn = true;
        /// <summary>N21: the door's box past its opening (m): each side, over the top, out of the tunnel.</summary>
        public static float DoorSideTightM = 0.5f, DoorTopTightM = 0.5f, DoorOutTightM = 0.5f;
        /// <summary>N21 proof: frames an enemy fort's screen box held the pointer while no part of the fort was under it (not hovered).</summary>
        public static int FortBesideFrames;

        sealed class FortParts { public GameObject go; public readonly List<Renderer> rs = new List<Renderer>(); public readonly List<Bounds> boxes = new List<Bounds>(); public readonly List<FortHeights> hf = new List<FortHeights>(); }
        /// <summary>A big part's (the fort's whole body, ~36 000 triangles) top surface as a height field in its own frame: per cell the highest
        /// point of its triangles over that cell (-inf = nothing there), so the ray finds the walls, towers, roof and ramp and never the open
        /// ground between the towers or beside the walls that its box holds.</summary>
        sealed class FortHeights { public Mesh mesh; public Vector3 min; public float cell; public int nx, nz; public float[] h; }
        /// <summary>N21: a part is read as a height field when its mesh is readable, holds at least this many triangles and is this big (m);
        /// the cell (m) of the field; cells lower than FortFieldLowM (m) over the part's foot are ground (its apron).</summary>
        public static int FortFieldMinTris = 2000; public static float FortFieldMinM = 6f, FortFieldCellM = 0.35f, FortFieldLowM = 0.5f;
        static readonly Dictionary<int, FortParts> fortParts = new Dictionary<int, FortParts>();

        /// <summary>The pointer's ray hits one of enemy fort f's own solid drawn parts (no picture known: true, as before).</summary>
        bool FortUnder(ZObject f, Ray ray)
        {
            if (!FortTightOn) return true;
            if (game == null || !game.StaticViews.TryGetValue(f.Id, out var go) || !go) return true;
            var fp = FortPartsOf(f.Id, go);
            if (fp.rs.Count == 0) return true;
            for (int i = 0; i < fp.rs.Count; i++)
            {
                var r = fp.rs[i];
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;                            // a part broken off (damage stages) no longer counts
                var t = r.transform; var o = t.InverseTransformPoint(ray.origin); var d = t.InverseTransformPoint(ray.origin + ray.direction) - o;
                var b = fp.boxes[i];
                if (!Slab(o, d, b.min, b.max)) continue;
                var hf = fp.hf[i];
                if (hf == null) return true;                                                                  // a small part (the big gun's turret, the gate's doors): its box
                var mf = r.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh != hf.mesh) { hf = fp.hf[i] = BuildField(mf.sharedMesh, r.transform); if (hf == null) return true; }   // the body's mesh swapped (a damage stage): read again
                if (FieldHit(hf, o, d, b)) return true;
            }
            return false;
        }

        /// <summary>The fort's solid parts (mesh renderers, opaque, not flags, glows, shadows or effects) and their own mesh boxes, read once.</summary>
        static FortParts FortPartsOf(int id, GameObject go)
        {
            if (fortParts.TryGetValue(id, out var fp) && fp.go == go) return fp;
            fp = new FortParts { go = go };
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                var m = r.sharedMaterial; if (m && m.renderQueue >= 2450) continue;                            // glows, halos, decals, water
                if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                var n = r.name;
                if (n.Contains("Flag") || n.Contains("flag") || n.Contains("Glow") || n.Contains("Halo") || n.Contains("Shadow") || n.Contains("Fx") || n.Contains("FX")
                    || n.Contains("Decal") || n.Contains("Ring") || n.Contains("Beam") || n.Contains("Pool") || n.Contains("Scorch")) continue;
                var b = mf.sharedMesh.bounds; if (b.size.sqrMagnitude < 1e-6f) continue;
                fp.rs.Add(r); fp.boxes.Add(b); fp.hf.Add(BuildField(mf.sharedMesh, r.transform));
            }
            fortParts[id] = fp;
            return fp;
        }

        /// <summary>N21: the height field of a big readable part (null = a small part or not readable: its box is used).</summary>
        static FortHeights BuildField(Mesh m, Transform t)
        {
            if (!m || !m.isReadable || !t) return null;
            var b = m.bounds; float sc = Mathf.Max(1e-4f, Mathf.Abs(t.lossyScale.x));
            if (m.triangles.Length / 3 < FortFieldMinTris || Mathf.Max(b.size.x, b.size.z) * sc < FortFieldMinM) return null;
            float cell = FortFieldCellM / sc;
            var f = new FortHeights { mesh = m, min = b.min, cell = cell, nx = Mathf.Max(1, Mathf.CeilToInt(b.size.x / cell) + 1), nz = Mathf.Max(1, Mathf.CeilToInt(b.size.z / cell) + 1) };
            f.h = new float[f.nx * f.nz];
            for (int i = 0; i < f.h.Length; i++) f.h[i] = float.NegativeInfinity;
            var v = m.vertices; var tr = m.triangles;
            for (int i = 0; i < v.Length; i++) Raise(f, v[i].x, v[i].z, v[i].y);
            for (int k = 0; k < tr.Length; k += 3)
            {
                Vector3 a = v[tr[k]], c = v[tr[k + 1]], e = v[tr[k + 2]];
                float x0 = Mathf.Min(a.x, Mathf.Min(c.x, e.x)), x1 = Mathf.Max(a.x, Mathf.Max(c.x, e.x)), z0 = Mathf.Min(a.z, Mathf.Min(c.z, e.z)), z1 = Mathf.Max(a.z, Mathf.Max(c.z, e.z));
                float den = (c.z - e.z) * (a.x - e.x) + (e.x - c.x) * (a.z - e.z);
                if (Mathf.Abs(den) < 1e-9f) continue;                                                         // a wall seen edge-on from above: its corners raised it (above)
                int i0 = Mathf.Clamp(Mathf.FloorToInt((x0 - f.min.x) / cell), 0, f.nx - 1), i1 = Mathf.Clamp(Mathf.CeilToInt((x1 - f.min.x) / cell), 0, f.nx - 1);
                int j0 = Mathf.Clamp(Mathf.FloorToInt((z0 - f.min.z) / cell), 0, f.nz - 1), j1 = Mathf.Clamp(Mathf.CeilToInt((z1 - f.min.z) / cell), 0, f.nz - 1);
                for (int ix = i0; ix <= i1; ix++)
                    for (int jz = j0; jz <= j1; jz++)
                    {
                        float px = f.min.x + ix * cell, pz = f.min.z + jz * cell;
                        float w1 = ((c.z - e.z) * (px - e.x) + (e.x - c.x) * (pz - e.z)) / den, w2 = ((e.z - a.z) * (px - e.x) + (a.x - e.x) * (pz - e.z)) / den, w3 = 1f - w1 - w2;
                        if (w1 < -0.02f || w2 < -0.02f || w3 < -0.02f) continue;
                        float y = w1 * a.y + w2 * c.y + w3 * e.y;
                        int idx = ix + jz * f.nx; if (y > f.h[idx]) f.h[idx] = y;
                    }
            }
            float low = b.min.y + FortFieldLowM / sc;                                                          // the fort's own ground-level plates (its apron) are ground to the eye
            for (int i = 0; i < f.h.Length; i++) if (f.h[i] < low) f.h[i] = float.NegativeInfinity;
            FieldsBuilt++;
            return f;
        }

        static void Raise(FortHeights f, float x, float z, float y)
        {
            int ix = Mathf.RoundToInt((x - f.min.x) / f.cell), jz = Mathf.RoundToInt((z - f.min.z) / f.cell);
            if (ix < 0 || jz < 0 || ix >= f.nx || jz >= f.nz) return;
            int idx = ix + jz * f.nx; if (y > f.h[idx]) f.h[idx] = y;
        }

        /// <summary>The ray (o, d in the part's frame) meets the part's top surface inside its box b: walked in half-cell steps from where it
        /// enters the box to where it leaves it; a step whose point lies at or under the highest point of its cell or of a neighbour cell is a hit (a thin wall top is never stepped over).</summary>
        static bool FieldHit(FortHeights f, Vector3 o, Vector3 d, Bounds b)
        {
            float t0 = 0f, t1 = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                if (Mathf.Abs(d[i]) < 1e-7f) { if (o[i] < b.min[i] || o[i] > b.max[i]) return false; continue; }
                float a = (b.min[i] - o[i]) / d[i], c = (b.max[i] - o[i]) / d[i];
                if (a > c) { var s = a; a = c; c = s; }
                t0 = Mathf.Max(t0, a); t1 = Mathf.Min(t1, c);
                if (t0 > t1) return false;
            }
            float horiz = Mathf.Sqrt(d.x * d.x + d.z * d.z), stepT = horiz > 1e-6f ? f.cell * 0.5f / horiz : (t1 - t0);
            int guard = 0;
            for (float t = t0; t <= t1 + stepT * 0.5f && guard < 4000; t += stepT, guard++)
            {
                var p = o + d * Mathf.Min(t, t1);
                int ix = Mathf.RoundToInt((p.x - f.min.x) / f.cell), jz = Mathf.RoundToInt((p.z - f.min.z) / f.cell);
                if (ix < 0 || jz < 0 || ix >= f.nx || jz >= f.nz) continue;
                float top = f.h[ix + jz * f.nx];                                                              // the cell and its four neighbours: a thin wall top is never stepped over
                if (ix > 0) top = Mathf.Max(top, f.h[ix - 1 + jz * f.nx]); if (ix < f.nx - 1) top = Mathf.Max(top, f.h[ix + 1 + jz * f.nx]);
                if (jz > 0) top = Mathf.Max(top, f.h[ix + (jz - 1) * f.nx]); if (jz < f.nz - 1) top = Mathf.Max(top, f.h[ix + (jz + 1) * f.nx]);
                if (p.y <= top) return true;
            }
            return false;
        }

        /// <summary>N21 proof: height fields read.</summary>
        public static int FieldsBuilt;

        /// <summary>Proof: the parts read for enemy fort f (count, and the largest part's box size in its frame).</summary>
        public string FortPartsInfo(ZObject f)
        {
            if (f == null || game == null || !game.StaticViews.TryGetValue(f.Id, out var go) || !go) return "no picture";
            var fp = FortPartsOf(f.Id, go); float big = 0f; string bn = "";
            for (int i = 0; i < fp.rs.Count; i++) { var s = fp.boxes[i].size; float v = s.x * s.y * s.z; if (v > big && fp.rs[i]) { big = v; bn = fp.rs[i].name + " " + s.ToString("F1"); } }
            return $"{fp.rs.Count} parts, largest {bn}";
        }
    }
}
