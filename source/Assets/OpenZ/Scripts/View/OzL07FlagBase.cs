// OpenZ (GPL-3.0)
// The picture only: no collider, no blocked tile, nothing the rules or the mouse read.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// The low worn stone under a zone flag of level 7 (a fort's flag stands on its roof), and the soot round it. FlagView.AddZoneFlag adds it on level 7 only, so a
    /// flag dropped in the Unit editor gets one too and loses it with its own root. No other level has this component.
    /// <list type="bullet">
    /// <item>The stone: about 1 m across, 0.11 m high, each flag its own outline (Shape), 126 triangles built in code, on the level's own rock atlas: the top
    /// of a rock (rockz2_atlas_Volcanic, a clean middle of rock in each of the four top tiles, no black gap out to the rim), so it is this level's stone by day and by
    /// night. The material is a copy of the rocks' own (RockPillar.chunkMaterial) with three changes: _SeeThru 0, or a robot at the flag would cut a hole in it, no
    /// emission, and Tint, which draws the stone down to the ground's weight: small and subtle, not a pebble on top of it.</item>
    /// <item>The soot: ScorchDecals.Flat, the game's own mark (multiplies the ground, never glows, no fade), each corner laid on the rendered ground as the burn marks are.</item>
    /// <item>The centre and the first two rings hold to the pole; the outer rings and the soot follow the rendered ground, read again every 3 s (craters).</item>
    /// </list>
    /// Cost: 2 small meshes a flag, 8 flags on this map; meshes are written only when the ground moved. Play check: All, Measure.
    /// </summary>
    public sealed class OzL07FlagBase : MonoBehaviour
    {
        public static readonly List<OzL07FlagBase> All = new List<OzL07FlagBase>();
        const float MarkRadius = 1.3f, MarkOpacity = 0.8f, Relay = 3f, Moved = 0.01f;
        static readonly Color Tint = new Color(0.80f, 0.68f, 0.60f);                                 // _BaseColor; the rocks' own is white, the atlas holds their colour
        static readonly int SeeThruId = Shader.PropertyToID("_SeeThru"), EmissionId = Shader.PropertyToID("_EmissionMapColor"), BaseColorId = Shader.PropertyToID("_BaseColor");
        static Material stone;

        public int flagId; public MeshRenderer body; public GameObject mark;
        Mesh mesh, markMesh; Vector3[] v, mv; float[] x, z, up; float baseY, next; bool laid;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }

        void OnDisable() { All.Remove(this); }

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (markMesh) Destroy(markMesh);
            if (All.Count == 0 && stone) { Destroy(stone); stone = null; }
        }

        /// <summary>FlagView.AddZoneFlag on level 7: flag = the flag's root, id = its sim id (the shape's seed; nothing else reads it).</summary>
        public static void Add(Transform flag, int id)
        {
            var mat = Stone(); if (!mat) return;
            var go = new GameObject("FlagBase"); go.transform.SetParent(flag, false);
            var b = go.AddComponent<OzL07FlagBase>(); b.flagId = id; b.Build(id, mat);
        }

        static Material Stone()
        {
            if (stone) return stone;
            var rock = FindFirstObjectByType<RockPillar>();
            if (!rock || !rock.chunkMaterial) { Debug.LogWarning("[OpenZ] L07 flag base: no rock in the scene to take the stone from"); return null; }
            stone = new Material(rock.chunkMaterial) { name = "L07_FlagBase" };
            stone.SetFloat(SeeThruId, 0f); stone.SetColor(EmissionId, Color.black); stone.SetColor(BaseColorId, Tint);
            return stone;
        }

        void Build(int id, Material mat)
        {
            x = new float[Verts]; z = new float[Verts]; up = new float[Verts];
            var vc = new byte[Verts]; var tu = new float[Verts]; var tv = new float[Verts];
            Shape(id, x, z, up, vc, tu, tv);
            v = new Vector3[Verts]; var uv = new Vector2[Verts]; var col = new Color32[Verts];
            for (int i = 0; i < Verts; i++) { v[i] = new Vector3(x[i], up[i], z[i]); uv[i] = new Vector2(tu[i], tv[i]); col[i] = new Color32(vc[i], vc[i], vc[i], 255); }
            mesh = new Mesh { name = "L07_FlagBase" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetTriangles(Triangles(), 0);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            body = gameObject.AddComponent<MeshRenderer>(); body.sharedMaterial = mat;
            body.shadowCastingMode = ShadowCastingMode.On; body.lightProbeUsage = LightProbeUsage.Off; body.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var p = transform.position; baseY = p.y + 0.05f;                                         // FlagView's foot: the ground under the pole, 5 cm up
            float yaw = U(id, 21) * 360f; int kind = (int)(Hash(id, 22) % 2u);                       // soot kinds 0 and 1: soft, few streaks
            mark = ScorchDecals.Flat(transform, new Vector3(p.x, baseY, p.z), MarkRadius, kind, yaw, MarkOpacity);
            if (mark)
            {
                mark.name = "FlagBaseMark";
                markMesh = mark.GetComponent<MeshFilter>().sharedMesh; mv = markMesh.vertices;
            }
            Lay();
            next = Time.time + 1f + U(id, 23) * Relay;                                               // not all on one frame
        }

        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + Relay;
            Lay();
        }

        /// <summary>On the rendered ground as it is now.</summary>
        void Lay()
        {
            var p = transform.position; baseY = Mathf.Max(baseY, OzNeutralFlagWaves.Ground(p.x, p.z));
            bool moved = !laid;
            for (int i = 0; i < Verts; i++)
            {
                float gr = OzNeutralFlagWaves.Ground(p.x + x[i], p.z + z[i]), g = i <= Seg * 2 ? Mathf.Lerp(baseY, gr, 0.6f) : gr, y = g + up[i] - p.y;   // the top leans most of the way with the ground
                if (Mathf.Abs(y - v[i].y) > Moved) moved = true;
                v[i].y = y;
            }
            if (moved)
            {
                mesh.SetVertices(v); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            }
            if (mv != null)
            {
                var t = mark.transform; float y0 = t.position.y; bool mm = !laid;
                for (int i = 0; i < mv.Length; i++)
                {
                    var w = t.TransformPoint(new Vector3(mv[i].x, 0f, mv[i].z));
                    float y = OzNeutralFlagWaves.Ground(w.x, w.z) - y0 + ScorchDecals.Lift;
                    if (Mathf.Abs(y - mv[i].y) > Moved) mm = true;
                    mv[i].y = y;
                }
                if (mm) { markMesh.SetVertices(mv); markMesh.RecalculateBounds(); }
            }
            laid = true;
        }

        /// <summary>Play check: the stone's width and the height of its top over the rendered ground under it (m).</summary>
        public void Measure(out float width, out float top)
        {
            var b = body.bounds; width = Mathf.Max(b.size.x, b.size.z); top = 0f;
            for (int i = 0; i < Verts; i++) top = Mathf.Max(top, v[i].y + transform.position.y - OzNeutralFlagWaves.Ground(transform.position.x + x[i], transform.position.z + z[i]));
        }

        // FlagBaseShape begin (no Unity type below: tools/rehearse.py runs this text, and sheet.py draws the stone from what it gives)
        public const int Seg = 14, Rings = 5, Verts = 1 + Seg * Rings;
        public const float Radius = 0.50f;
        const float AtlasSize = 2048f, TexelsPerM = 48f, Tau = 6.2831853f;
        static readonly float[] RingR = { 0.30f, 0.66f, 0.90f, 1.00f, 1.16f }, RingH = { 0.11f, 0.105f, 0.08f, 0.03f, -0.06f };
        static readonly byte[] RingVc = { 222, 240, 232, 212, 196 };   // the rocks' vertex colours run 188 to 255: the foot stays inside that, or the rim reads as a black line; 176 = the socket round the pole
        // a clean middle of rock in each of the 4 rock top tiles of the atlas, in pixels from its top left: no black gap out to the rim, which reaches about 30 px. apply_flag_base.py writes
        // this line from the atlas in the tree (tools/pick_spots.py)
        static readonly int[] LumpX = { 351, 888, 1372, 1888 }, LumpY = { 773, 720, 796, 830 };

        static uint Hash(int id, int salt)
        {
            unchecked { uint h = (uint)(id * 73856093) ^ (uint)(salt * 19349663); h ^= h >> 13; h *= 1274126177u; h ^= h >> 16; return h; }
        }

        static float U(int id, int salt) => Hash(id, salt) / 4294967296f;

        /// <summary>Vertex 0 = the pole's foot, then ring after ring, Seg each, inner ring first. x, z from the pole; up = over the ground; vc = vertex colour; u, v = atlas.</summary>
        public static void Shape(int id, float[] x, float[] z, float[] up, byte[] vc, float[] u, float[] v)
        {
            float p1 = U(id, 1) * Tau, p2 = U(id, 2) * Tau, p3 = U(id, 3) * Tau, tx = (U(id, 4) - 0.5f) * 0.05f, tz = (U(id, 5) - 0.5f) * 0.05f, turn = U(id, 6) * Tau;
            int lump = (int)(Hash(id, 7) % 4u); float cu = (LumpX[lump] + 0.5f) / AtlasSize, cv = 1f - (LumpY[lump] + 0.5f) / AtlasSize, c = (float)System.Math.Cos(turn), s = (float)System.Math.Sin(turn), m = TexelsPerM / AtlasSize;
            x[0] = 0f; z[0] = 0f; up[0] = RingH[0]; vc[0] = 176; u[0] = cu; v[0] = cv;
            for (int k = 0; k < Rings; k++)
                for (int i = 0; i < Seg; i++)
                {
                    int q = 1 + k * Seg + i; float a = i * Tau / Seg + (k & 1) * Tau / (2 * Seg);
                    float o = 1f + 0.07f * (float)System.Math.Sin(2f * a + p1) + 0.05f * (float)System.Math.Sin(3f * a + p2) + 0.03f * (float)System.Math.Sin(5f * a + p3);   // the outline
                    float r = RingR[k] * Radius * (k == 0 ? 1f : o), px = r * (float)System.Math.Cos(a), pz = r * (float)System.Math.Sin(a);
                    x[q] = px; z[q] = pz; vc[q] = RingVc[k];
                    up[q] = RingH[k] + (k < 3 ? (U(id, 100 + q) - 0.5f) * 0.024f + tx * px + tz * pz : 0f);           // worn, a little out of level
                    u[q] = cu + (c * px - s * pz) * m; v[q] = cv + (s * px + c * pz) * m;
                }
        }

        public static int[] Triangles()
        {
            var t = new int[(Seg + 2 * Seg * (Rings - 1)) * 3]; int n = 0;
            for (int i = 0; i < Seg; i++) { t[n++] = 0; t[n++] = 1 + (i + 1) % Seg; t[n++] = 1 + i; }
            for (int k = 0; k < Rings - 1; k++)
                for (int i = 0; i < Seg; i++)
                {
                    int a = 1 + k * Seg + i, a1 = 1 + k * Seg + (i + 1) % Seg, b = a + Seg, b1 = a1 + Seg;
                    t[n++] = a; t[n++] = a1; t[n++] = b; t[n++] = b; t[n++] = a1; t[n++] = b1;
                }
            return t;
        }
        // FlagBaseShape end
    }
}
