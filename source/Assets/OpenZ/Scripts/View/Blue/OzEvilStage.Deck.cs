// OpenZ (GPL-3.0) - version 1.2, the evil HQ: the metal deck floor and the builder of the military dressing (OzEvilStage).
// Owner item 178. The floor (14 x 11 m, every blue scene and the
// battlefield screen's card: one stage) is now a steel deck: 1.2 m plates with seams, bolts, worn edges, scuffs and grime, with real bumps
// and a soft sheen (the robots' own metal material, OzEvilStage.SteelTex.cs's pictures), and a 0.4 m grating strip across the room between
// the console and the robots. Both lie at the old floor's top (y 0), so the robots' floor shadows (OzEvilStage.FloorShadows.cs, 1 cm over
// it) read on it as before.
// The dressing's small parts (panels, pipes, the hatch, the rack...) are collected per material and merged into one mesh each, so the
// whole dressing costs a few draws (MilitaryLookStatus()). The pictures are drawn once per session; the meshes go with the stage.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public sealed partial class OzEvilStage
    {
        /// <summary>The deck's and the grating's brightness (x its picture), sheen (x the picture's smoothness) and bump strength.</summary>
        public static float DeckTint = 1.25f, DeckSheen = 1.35f, DeckBump = 1f;
        /// <summary>The wall panels' and the console's brightness, sheen and bump strength.</summary>
        public static float WallTint = 1f, WallSheen = 1f, WallBump = 1f;
        /// <summary>false: the hazard parts (the door edges, the alcove sill, the hatch's bands) are the old plain yellow (read when the stage is built).</summary>
        public static bool HazardStriped = false;   // EvilZod's side (the bomb doors, the alcove lip) keeps its plain yellow as before

        static SteelMaps mapsDeck, mapsGrating, mapsWall, mapsConsole; static Texture2D texHazard; static float steelPaintMs; static bool pbrOk;
        Material deckMat, gratingMat, wallPbrMat, consoleMat, hazardMat;
        readonly Dictionary<Material, List<CombineInstance>> kitParts = new Dictionary<Material, List<CombineInstance>>();
        readonly Dictionary<Material, string> kitNames = new Dictionary<Material, string>();
        readonly List<Mesh> kitTemp = new List<Mesh>(), kitMeshes = new List<Mesh>();
        int kitPartCount, kitRenderers, kitVerts;
        static Mesh primCyl, primSph, unitBox;

        /// <summary>The steel pictures, drawn once per session (every stage instance shares them).</summary>
        static void EnsureSteelMaps()
        {
            bool Ok(SteelMaps m) => m != null && m.albedo && m.normal && m.metal;
            if (Ok(mapsDeck) && Ok(mapsGrating) && Ok(mapsWall) && Ok(mapsConsole) && texHazard) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (SteelFromAhead()) { steelPaintMs = (float)sw.Elapsed.TotalMilliseconds; Debug.Log($"[EvilZod] the HQ's steel pictures made in {steelPaintMs:F0} ms from the ones drawn ahead ({SteelAheadMs:F0} ms on a background thread, off the game's frames; once per session)"); return; }
            var nz = new TileNoise(1306);
            if (!Ok(mapsDeck)) mapsDeck = PaintMaps("evil_hq_deck", (x, y) => DeckSurf(nz, x, y), 2.4f, 2.4f, 768, 768, true, true, true, 8);
            if (!Ok(mapsGrating)) mapsGrating = PaintMaps("evil_hq_grating", (x, y) => GratingSurf(nz, x, y), 0.4f, 0.4f, 256, 256, false, true, false, 8);
            if (!Ok(mapsWall)) mapsWall = PaintMaps("evil_hq_wall", (x, y) => WallSurf(nz, x, y), 1.3f, 1.3f, 512, 512, true, true, true, 4);
            if (!Ok(mapsConsole)) mapsConsole = PaintMaps("evil_hq_console", (x, y) => ConsoleSurf(nz, x, y), 1.8f, 0.9f, 512, 256, false, false, false, 4);
            if (!texHazard) texHazard = PaintColour("evil_hq_hazard", (x, y) => HazardSurf(nz, x, y), 0.2f, 0.2f, 128, 128);
            steelPaintMs = (float)sw.Elapsed.TotalMilliseconds;
            Debug.Log($"[EvilZod] the HQ's steel pictures drawn in {steelPaintMs:F0} ms (once per session)");
        }

        /// <summary>A steel material with the three pictures: a copy of the robots' own metal material (its shader variant ships with the
        /// game: colour + bumps + metal map); without it, the stage's plain template with the colour picture only.</summary>
        Material SteelPbr(SteelMaps maps)
        {
            Material m = null;
            var t = Resources.Load<Material>(Folder + "M_EvilZod_Body");
            if (t && maps != null)
            {
                m = new Material(t) { hideFlags = HideFlags.DontSave }; madeMats.Add(m); pbrOk = true;
                m.SetTexture("_BaseMap", maps.albedo); m.SetTexture("_MainTex", maps.albedo); m.SetTexture("_BumpMap", maps.normal); m.SetTexture("_MetallicGlossMap", maps.metal);
                if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", null);
                m.SetTextureScale("_BaseMap", Vector2.one); m.SetTextureOffset("_BaseMap", Vector2.zero);
                return m;
            }
            pbrOk = false;
            m = Lit(new Color32(255, 255, 255, 255), 0.35f, 0.5f); if (!m) return null;
            if (maps != null) m.SetTexture("_BaseMap", maps.albedo);
            return m;
        }
        static void TunePbr(Material m, float tint, float sheen, float bump, float plainSmooth)
        {
            if (!m) return;
            m.SetColor("_BaseColor", new Color(tint, tint, tint, 1f));
            m.SetFloat("_Smoothness", pbrOk ? sheen : plainSmooth * sheen);                               // with the metal map: x its smoothness
            if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", bump);
        }

        // ------------------------------------------------------------------------------------------------ the floor
        /// <summary>The deck (the old floor's place and size: 14 x 11 m, its top at y 0; plate seams along the back wall's foot and through
        /// the doors' middle) and the grating strip (x -5.9 .. 5.9, z -2.45 .. -2.05, 2 mm over the deck).</summary>
        void BuildDeck()
        {
            EnsureSteelMaps();
            deckMat = SteelPbr(mapsDeck); gratingMat = SteelPbr(mapsGrating);
            FlatPiece("Floor", new Vector3(0f, 0f, -1.2f), Vector3.up, Vector3.right, 14f, 11f, 2.4f, 2.4f, new Vector2(0f, -3.3f), deckMat);
            FlatPiece("FloorGrating", new Vector3(0f, 0.002f, -2.25f), Vector3.up, Vector3.right, 11.8f, 0.4f, 0.4f, 0.4f, new Vector2(0f, -2.45f), gratingMat);
        }

        /// <summary>One flat face as its own renderer (picture repeats every tileU x tileV m, from uv0 m), shadows received, none cast.</summary>
        Renderer FlatPiece(string name, Vector3 c, Vector3 n, Vector3 u, float w, float h, float tileU, float tileV, Vector2 uv0, Material mat)
        {
            var vs = new List<Vector3>(); var ns = new List<Vector3>(); var ts = new List<Vector4>(); var uvs = new List<Vector2>(); var tri = new List<int>();
            AddFace(vs, ns, ts, uvs, tri, c, n, u, w, h, tileU, tileV, uv0);
            var mesh = NewMesh(name, vs, ns, ts, uvs, tri); kitMeshes.Add(mesh);
            var go = new GameObject(name) { layer = Layer }; go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>();
            return Dress(go, mat, false);
        }

        // ------------------------------------------------------------------------------------------------ the hazard stripes
        Material HazardMat()
        {
            if (hazardMat) return hazardMat;
            if (!HazardStriped) return hazardMat = Lit(new Color32(214, 170, 38, 255), 0.35f, 0.1f);
            EnsureSteelMaps();
            hazardMat = Lit(new Color32(255, 255, 255, 255), 0.3f, 0.1f);
            if (hazardMat) hazardMat.SetTexture("_BaseMap", texHazard);
            return hazardMat;
        }
        /// <summary>A box in worn black-and-yellow stripes at their true size (a 0.2 m repeat on every face), its own renderer (the door
        /// leaves move with theirs).</summary>
        Renderer HazardBox(string name, Vector3 pos, Vector3 size, Transform parent = null, bool cast = true)
        {
            var mesh = MetricBox(size, 0.2f); mesh.name = name; kitMeshes.Add(mesh);
            var go = new GameObject(name) { layer = Layer }; go.transform.SetParent(parent ? parent : transform, false); go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>();
            return Dress(go, HazardMat(), cast);
        }

        // ------------------------------------------------------------------------------------------------ the kit: parts merged per material
        /// <summary>A box (stage-local centre and size, m), turned by `rot`; tile > 0: its pictures at true size (one repeat per `tile` m).</summary>
        void KitBox(Vector3 c, Vector3 size, Material mat, float tile = 0f, Quaternion? rot = null)
        {
            if (!mat) return;
            var q = rot ?? Quaternion.identity;
            if (tile > 0f) { var m = MetricBox(size, tile); kitTemp.Add(m); KitAdd(m, Matrix4x4.TRS(c, q, Vector3.one), mat); }
            else { if (!unitBox) { unitBox = MetricBox(Vector3.one, 1f); unitBox.hideFlags = HideFlags.DontSave; } KitAdd(unitBox, Matrix4x4.TRS(c, q, size), mat); }
        }
        /// <summary>A round bar from a to b (a pipe, a rail, a rod), radius r.</summary>
        void KitCyl(Vector3 a, Vector3 b, float r, Material mat)
        {
            if (!primCyl) primCyl = PrimMesh(PrimitiveType.Cylinder);
            Vector3 d = b - a; float len = d.magnitude; if (len < 1e-4f) return;
            KitAdd(primCyl, Matrix4x4.TRS((a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, d / len), new Vector3(2f * r, len * 0.5f, 2f * r)), mat);
        }
        void KitSph(Vector3 c, float r, Material mat) { if (!primSph) primSph = PrimMesh(PrimitiveType.Sphere); KitAdd(primSph, Matrix4x4.TRS(c, Quaternion.identity, Vector3.one * 2f * r), mat); }
        void KitQuad(Vector3 c, Vector3 n, Vector3 u, float w, float h, float tileU, float tileV, Vector2 uv0, Material mat)
        {
            var vs = new List<Vector3>(); var ns = new List<Vector3>(); var ts = new List<Vector4>(); var uvs = new List<Vector2>(); var tri = new List<int>();
            AddFace(vs, ns, ts, uvs, tri, c, n, u, w, h, tileU, tileV, uv0);
            var m = NewMesh("kit_quad", vs, ns, ts, uvs, tri); kitTemp.Add(m); KitAdd(m, Matrix4x4.identity, mat);
        }
        void KitAdd(Mesh mesh, Matrix4x4 m, Material mat)
        {
            if (!mesh || !mat) return;
            if (!kitParts.TryGetValue(mat, out var l)) kitParts[mat] = l = new List<CombineInstance>();
            l.Add(new CombineInstance { mesh = mesh, transform = m, subMeshIndex = 0 }); kitPartCount++;
        }
        void KitName(Material m, string name) { if (m) kitNames[m] = name; }

        /// <summary>Every collected part merged: one mesh and one renderer per material (no shadows cast, shadows received).</summary>
        void FlushKit()
        {
            foreach (var kv in kitParts)
            {
                var list = kv.Value; if (list.Count == 0) continue;
                int verts = 0; foreach (var ci in list) verts += ci.mesh.vertexCount;
                string name = kitNames.TryGetValue(kv.Key, out var nm) ? nm : "Part";
                var mesh = new Mesh { name = "evil_hq_" + name };
                if (verts > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.CombineMeshes(list.ToArray(), true, true, false); mesh.RecalculateBounds();
                kitMeshes.Add(mesh); kitVerts += verts; kitRenderers++;
                var go = new GameObject("Dressing_" + name) { layer = Layer }; go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>();
                Dress(go, kv.Key, false);
            }
            kitParts.Clear();
            foreach (var m in kitTemp) if (m) Destroy(m);
            kitTemp.Clear();
        }
        void DisposeKit() { foreach (var m in kitMeshes) if (m) Destroy(m); kitMeshes.Clear(); }

        static Mesh PrimMesh(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t); go.SetActive(false);
            var m = go.GetComponent<MeshFilter>().sharedMesh; Destroy(go);
            return m;
        }

        /// <summary>One face: centre c, facing n, its picture's u along `u` (v = u x n, so it reads unmirrored from the front), w x h m, the
        /// picture repeating every tileU x tileV m counted from uv0 (m, along u and v).</summary>
        static void AddFace(List<Vector3> vs, List<Vector3> ns, List<Vector4> ts, List<Vector2> uvs, List<int> tri, Vector3 c, Vector3 n, Vector3 u, float w, float h, float tileU, float tileV, Vector2 uv0)
        {
            Vector3 v = Vector3.Cross(u, n); int b0 = vs.Count;
            for (int k = 0; k < 4; k++)
            {
                float su = (k == 1 || k == 2) ? 0.5f : -0.5f, sv = k >= 2 ? 0.5f : -0.5f;
                var p = c + u * (su * w) + v * (sv * h);
                vs.Add(p); ns.Add(n); ts.Add(new Vector4(u.x, u.y, u.z, -1f));                             // bitangent = cross(n, u) x -1 = v
                uvs.Add(new Vector2((Vector3.Dot(p, u) - uv0.x) / tileU, (Vector3.Dot(p, v) - uv0.y) / tileV));
            }
            tri.Add(b0); tri.Add(b0 + 3); tri.Add(b0 + 2); tri.Add(b0); tri.Add(b0 + 2); tri.Add(b0 + 1);   // seen from +n
        }
        /// <summary>A box of `size` round its centre, each face's picture at true size (one repeat per `tile` m).</summary>
        static Mesh MetricBox(Vector3 size, float tile)
        {
            var vs = new List<Vector3>(24); var ns = new List<Vector3>(24); var ts = new List<Vector4>(24); var uvs = new List<Vector2>(24); var tri = new List<int>(36);
            Vector3 hs = size * 0.5f;
            AddFace(vs, ns, ts, uvs, tri, new Vector3(0f, 0f, hs.z), Vector3.forward, Vector3.left, size.x, size.y, tile, tile, Vector2.zero);
            AddFace(vs, ns, ts, uvs, tri, new Vector3(0f, 0f, -hs.z), Vector3.back, Vector3.right, size.x, size.y, tile, tile, Vector2.zero);
            AddFace(vs, ns, ts, uvs, tri, new Vector3(hs.x, 0f, 0f), Vector3.right, Vector3.forward, size.z, size.y, tile, tile, Vector2.zero);
            AddFace(vs, ns, ts, uvs, tri, new Vector3(-hs.x, 0f, 0f), Vector3.left, Vector3.back, size.z, size.y, tile, tile, Vector2.zero);
            AddFace(vs, ns, ts, uvs, tri, new Vector3(0f, hs.y, 0f), Vector3.up, Vector3.right, size.x, size.z, tile, tile, Vector2.zero);
            AddFace(vs, ns, ts, uvs, tri, new Vector3(0f, -hs.y, 0f), Vector3.down, Vector3.right, size.x, size.z, tile, tile, Vector2.zero);
            return NewMesh("evil_hq_box", vs, ns, ts, uvs, tri);
        }
        static Mesh NewMesh(string name, List<Vector3> vs, List<Vector3> ns, List<Vector4> ts, List<Vector2> uvs, List<int> tri)
        {
            var m = new Mesh { name = name };
            m.SetVertices(vs); m.SetNormals(ns); m.SetTangents(ts); m.SetUVs(0, uvs); m.SetTriangles(tri, 0); m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------------------------------------ the checks
        /// <summary>The checks / the redo lab: what the dressing costs on this stage.</summary>
        public string MilitaryLookStatus()
        {
            int drawn = 0, lit = 0, lights = 0; var mats = new HashSet<Material>();
            foreach (var r in GetComponentsInChildren<Renderer>(false)) { if (!r.enabled) continue; drawn++; foreach (var m in r.sharedMaterials) if (m) mats.Add(m); }
            foreach (var l in GetComponentsInChildren<Light>(false)) { lights++; if (l.enabled && l.intensity > 0f) lit++; }
            return $"military dressing: {kitRenderers} merged renderer(s) from {kitPartCount} part(s), {kitVerts} verts; the stage now: {drawn} renderer(s) drawn with "
                 + $"{mats.Count} material(s), {lit} of {lights} light(s) lit; steel material {(pbrOk ? "metal + bumps" : "PLAIN (template missing)")}; pictures {steelPaintMs:F0} ms once per session";
        }
    }
}
