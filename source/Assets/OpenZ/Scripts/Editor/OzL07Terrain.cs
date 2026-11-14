// OpenZ (GPL-3.0) - version 1.3, item 12 part B1 / B2: level 7's ground look and everything round the lava that is not map data. T1 turns the ground
// material made by OpenZ/L07 Volcanic/1 into the crisp ground; T2 stands the border volcanoes, spires, lake cones, border lava sheets, bank rocks and glowing
// banks in the open level 7 scene from Models/L07/L07_place.json. Round 3: the cracks live in the ground (OpenZ/Sand keyword _OZ_L07_GROUND, set here). View only: nothing here reaches the sim, and only level 7's own files are touched.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ.EditorTools
{
    public static class OzL07Terrain
    {
        const string ModelDir = "Assets/OpenZ/Models/L07", Tex = "Assets/OpenZ/Textures", MatEnv = "Assets/OpenZ/Materials/Env", PrefabDir = "Assets/OpenZ/Prefabs/L07";
        const string Paint = Tex + "/L07_ground_paint.png", Sdf = Tex + "/Ground/L07_ground_sdf.png", Rock = Tex + "/L07_cliff_rock.png", DetailT = Tex + "/Detail/T_L07_Detail.png", DetailN = Tex + "/Detail/T_L07_Detail_n.png";
        const string Dress = ModelDir + "/L07_lava_dress.fbx", Place = ModelDir + "/L07_place.json", RimRoot = "L07_Rim";
        static string Export => Path.Combine(OpenZPaths.RepoRoot, "work", "staging", "v1_3", "lava_biome_L07", "blender_pass", "export", "L07");

        [Serializable] class Row { public string model, row, id; public float x, y, z, yaw, scale; public bool erupt; }
        [Serializable] class PlaceFile { public Row[] place; }

        static void ImportTex(string path, int max, int aniso, TextureWrapMode wrap, bool srgb, bool compressed, bool mips, TextureImporterNPOTScale npot = TextureImporterNPOTScale.None)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!ti) { Debug.LogError("[OpenZ] L07: missing texture " + path); return; }
            ti.textureType = TextureImporterType.Default; ti.sRGBTexture = srgb; ti.mipmapEnabled = mips; ti.isReadable = false; ti.npotScale = npot;
            ti.wrapMode = wrap; ti.filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear; ti.anisoLevel = aniso; ti.maxTextureSize = max;
            ti.textureCompression = compressed ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
            ti.alphaIsTransparency = false; ti.alphaSource = TextureImporterAlphaSource.FromInput;   // the alpha of L07's paint and field is data
            ti.SaveAndReimport();
        }

        /// <summary>M_L07_Ground on OpenZ/Sand: the paint, the road edge SDF and the shared detail layer; every desert-only feature off (as L17).</summary>
        [MenuItem("OpenZ/L07 Volcanic/T1 Ground look (after step 1)")]
        public static string GroundLook()
        {
            ImportTex(Paint, 8192, 16, TextureWrapMode.Clamp, true, true, true);          // 8192 x 8192 RGBA, BC7, A = shore heat
            ImportTex(Sdf, 8192, 1, TextureWrapMode.Clamp, false, false, false);           // 4096 x 5084 RGBA, uncompressed, no mips: R road, G crack distance, B crack half width, A crack heat. BC7 broke thin cracks up (tools/bc7_standin.py)
            ImportTex(DetailT, 1024, 8, TextureWrapMode.Repeat, false, true, true); ImportTex(DetailN, 1024, 8, TextureWrapMode.Repeat, false, true, true);
            ImportTex(Rock, 2048, 8, TextureWrapMode.Repeat, true, true, true);
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{MatEnv}/M_L07_Ground.mat");
            var sand = Shader.Find("OpenZ/Sand");
            if (!m || !sand) { const string e = "M_L07_Ground or OpenZ/Sand missing: run OpenZ/L07 Volcanic/1 first"; Debug.LogError("[OpenZ] L07: " + e); return e; }
            m.shader = sand; m.shaderKeywords = new string[0]; m.EnableKeyword("_OZ_NO_REFL_SPEC"); m.EnableKeyword("_OZ_L07_GROUND");
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture>(Paint)); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.05f);
            foreach (var off in new[] { "_Grade", "_Blur", "_RippleLarge", "_RippleSmall", "_ShadowLift" }) m.SetFloat(off, 0);
            m.SetTexture("_GroundSdf", AssetDatabase.LoadAssetAtPath<Texture>(Sdf)); m.SetFloat("_SdfMode", 0); m.SetFloat("_SdfRange", 1);   // mode 0: the road edge is in the paint, the L07 path reads the field once
           
            m.SetVector("_GroundSize", new Vector4(216, 268, 0, 0));
            m.SetVector("_MarkIO", new Vector4(2.2f, 0.6f, 0.04f, 0.10f));      // L07: heat power, extra heat at night, night floor ground, road. First values, set against a real frame
            m.SetVector("_PlateBevel", new Vector4(0.45f, 0.49f, 0.27f, 0.16f)); // L07: crack T base, span, glow tail base, span
            m.SetVector("_CrispPx", new Vector4(0.05f, 0.10f, 0, 0));
            m.SetTexture("_DetailTex", AssetDatabase.LoadAssetAtPath<Texture>(DetailT));
            m.SetTexture("_DetailNrm", AssetDatabase.LoadAssetAtPath<Texture>(DetailN));
            m.SetFloat("_DetailOn", 1); m.SetVector("_DetailTiles", new Vector4(4f, 9.7f, 0, 0));
            m.SetVector("_DetailStr", new Vector4(0.60f, 1.55f, 0.4f, 0.00f)); m.SetFloat("_DetailNormalStr", 0.60f);   /* ground_v3: road, ground, unused, no gaps; the tile carries only what is under 0.15 m, albedo over normals */ m.SetVector("_DetailFade", new Vector4(110, 260, 0.35f, 0));
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
            Debug.Log("[OpenZ] L07: ground look set"); return "L07 ground look set";
        }

        static Material RimMat(string name, string atlas, string emis, float glow, Color? tintLinear = null)
        {
            string p = $"{ModelDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (!m) { m = new Material(Shader.Find("OpenZ/TeamLit")); AssetDatabase.CreateAsset(m, p); }
            m.SetColor("_BaseColor", tintLinear.HasValue ? tintLinear.Value.gamma : Color.white); m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture>(atlas));
            m.SetFloat("_UseVertexColor", 0); m.SetFloat("_TeamMode", 0); m.SetFloat("_Smoothness", 0.08f); m.enableInstancing = true;
            if (emis != null)
            {
                m.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture>(emis));
                m.SetColor("_EmissionMapColor", new Color(glow, glow, glow, 1)); m.SetFloat("_EmissionNight", 0);   // lava glows by day too
            }
            EditorUtility.SetDirty(m); return m;
        }

        static void ImportModel(string p)
        {
            var mi = AssetImporter.GetAtPath(p) as ModelImporter;
            if (!mi) { Debug.LogError("[OpenZ] L07: missing " + p); return; }
            mi.globalScale = 1; mi.useFileUnits = true; mi.bakeAxisConversion = true; mi.importCameras = false; mi.importLights = false;
            mi.meshCompression = ModelImporterMeshCompression.Off; mi.isReadable = false; mi.addCollider = false; mi.importNormals = ModelImporterNormals.Import;
            mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None; mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.SaveAndReimport();
        }

        static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, ShadowCastingMode shadows)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = shadows; r.receiveShadows = true;
            return go;
        }

        /// <summary>One prefab per asset and row, one fixed mesh, no LODGroup, so a piece stops drawing only outside the picture: "" = the full mesh, "_mid" and "_far" = the LOD1 mesh, "_far" casts no shadow.</summary>
        static GameObject MakePrefab(string node, string kind, Dictionary<string, MeshFilter> meshes, Material mat, Mesh clean = null)
        {
            string path = $"{PrefabDir}/{node}{kind}.prefab";
            var root = new GameObject(node + kind);
            try
            {
                string n = kind == "" || clean != null ? node : node + "_LOD1";   // LOD2 has long splinter faces: LOD1 stands in for it. Row 31 v2: a border volcano draws its clean mesh, made in LOD0's own space
                if (!meshes.TryGetValue(n, out var src)) { Debug.LogError("[OpenZ] L07: mesh missing " + n); return null; }
                var p = Part(root.transform, n, clean != null ? clean : src.sharedMesh, mat, kind == "_far" ? ShadowCastingMode.Off : ShadowCastingMode.On);
                p.transform.localRotation = src.transform.rotation; p.transform.localScale = src.transform.lossyScale;   // mesh + its node, as the model shows it in a scene: the FBX nodes carry a turn
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static readonly Dictionary<string, string> Node = new Dictionary<string, string> {
            { "volcano_1_z", "L07_Volcano_1" }, { "volcano_2_z", "L07_Volcano_2" }, { "volcano_3_z", "L07_Volcano_3" }, { "volcano_4_z", "L07_Volcano_4" },
            { "grey_pillars_1", "L07_GreyPillars_1" }, { "grey_pillars_2", "L07_GreyPillars_2" }, { "grey_pillars_3", "L07_GreyPillars_3" },
            { "brown_cone_1", "L07_BrownCone_1" }, { "brown_cone_2", "L07_BrownCone_2" }, { "brown_cone_row", "L07_BrownConeRow" } };

        [MenuItem("OpenZ/L07 Volcanic/T2 Rim: volcanoes, banks (open scene)")]
        public static string Rim()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.StartsWith("L07")) return "open L07_Volcanic first";
            if (!File.Exists(Place) || !File.Exists(Dress)) return "the terrain kit is not in: " + Place;
            Directory.CreateDirectory(PrefabDir);
            foreach (var f in new[] { "L07_volcanoes.fbx", "L07_rocks_decor.fbx", "vol_atlas_L07.png" })   // emission and the pillar tint come with this kit: L07_vol_emis, L07_decor_emis, L07_decor_atlas
            {
                string src = Path.Combine(Export, f);
                if (!File.Exists(src)) return "no " + src;
                File.Copy(src, $"{ModelDir}/{f}", true);
            }
            AssetDatabase.Refresh();
            string[] fbxs = { $"{ModelDir}/L07_volcanoes.fbx", $"{ModelDir}/L07_rocks_decor.fbx", Dress };
            foreach (var p in fbxs) ImportModel(p);
            foreach (var n in new[] { "L07_vol_atlas", "L07_decor_atlas" }) ImportTex($"{ModelDir}/{n}.png", 2048, 8, TextureWrapMode.Clamp, true, true, true);
            foreach (var n in new[] { "L07_vol_emis", "L07_decor_emis", "L07_bank_emis" }) ImportTex($"{ModelDir}/{n}.png", 512, 1, TextureWrapMode.Clamp, true, true, true);
            ImportTex($"{ModelDir}/L07_bank_atlas.png", 1024, 4, TextureWrapMode.Clamp, true, true, true);
            ImportTex($"{ModelDir}/T_L07_Veins.png", 512, 4, TextureWrapMode.Repeat, true, true, true);
            var matVol = RimMat("M_L07_Volcano", $"{ModelDir}/L07_vol_atlas.png", $"{ModelDir}/L07_vol_emis.png", 2.2f); matVol.SetFloat("_FlatShade", 1);
            var matDec = RimMat("M_L07_RockDecor", $"{ModelDir}/L07_decor_atlas.png", $"{ModelDir}/L07_decor_emis.png", 2.2f);
            var matBank = RimMat("M_L07_Bank", $"{ModelDir}/L07_bank_atlas.png", $"{ModelDir}/L07_bank_emis.png", 1.6f); matBank.SetFloat("_FlatShade", 1);
            var matLava = Resources.Load<Material>(OzLava.MaterialRes);
            if (!matLava) return "M_L07_Lava missing: run OpenZ/L07 Lava/Make lava material";
            matLava.SetFloat("_OEdge", 0.12f); matLava.SetFloat("_OBank1", 0.25f); EditorUtility.SetDirty(matLava);   // round 3: the dark crust band inside the shore opens, lava reaches the stones
            var meshes = new Dictionary<string, MeshFilter>();
            foreach (var p in fbxs.Take(2)) foreach (var mf in AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentsInChildren<MeshFilter>(true)) meshes[mf.name] = mf;
            var clean = CleanVolcanoes(meshes, out string cleanNote);   // row 31 v2: V1-V3's clean meshes (no shards, no torn crater) and their atlas without baked sun; null: they draw as before
            var matVol2 = clean != null ? RimMat("M_L07_VolcanoV2", VolAtlas2, VolEmis2, 2.2f) : null; if (matVol2) matVol2.SetFloat("_FlatShade", 1);
            var prefabs = new Dictionary<string, GameObject>();
            var file = JsonUtility.FromJson<PlaceFile>(File.ReadAllText(Place));
            GameObject PrefabOf(Row q)
            {
                string node = Node[q.model], kind = q.row == "second" || q.row == "edge" || q.row == "edge2" || q.row == "fill" ? "_mid" : q.row == "far" || q.row == "far2" ? "_far" : "";
                if (prefabs.TryGetValue(node + kind, out var pf)) return pf;
                bool vol = node.Contains("Volcano");
                Mesh cm = null; if (vol && clean != null) clean.TryGetValue(node, out cm);   // row 31 v2: every row of V1-V3 (the crater V4 keeps its mesh and M_L07_Volcano); place, turn and size stay
                pf = MakePrefab(node, kind, meshes, cm != null ? matVol2 : vol ? matVol : matDec, cm);
                prefabs[node + kind] = pf; return pf;
            }
            var old = GameObject.Find(RimRoot); if (old) UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject(RimRoot); int placed = 0;
            var groups = new Dictionary<string, Transform>();
            foreach (var q in file.place)
            {
                var pf = PrefabOf(q); if (!pf) return "prefab failed for " + q.model;
                if (!groups.TryGetValue(q.row, out var parent)) { parent = new GameObject(q.row).transform; parent.SetParent(root.transform, false); groups[q.row] = parent; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
                go.transform.SetPositionAndRotation(new Vector3(q.x, q.y, q.z), Quaternion.Euler(0, -q.yaw, 0));   // yaw is counter-clockwise from above, Blender axes
                go.transform.localScale = Vector3.one * q.scale; placed++;
            }
            // sheets, cracks and banks: the one model, unpacked, with level 7's own materials
            var dress = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Dress));
            PrefabUtility.UnpackPrefabInstance(dress, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            dress.name = "Dress"; dress.transform.SetParent(root.transform, false);
            int sheets = 0, banks = 0;
            foreach (var r in dress.GetComponentsInChildren<MeshRenderer>())
            {
                string n = r.name;
                if (n.StartsWith("LAV_Border")) { r.sharedMaterial = matLava; r.shadowCastingMode = ShadowCastingMode.Off; sheets++; }
                else if (n.StartsWith("BNK_")) { r.sharedMaterial = matBank; r.gameObject.SetActive(false); banks++; }   // row 31: the far glow fans read as flat red stickers, and the bank rocks stood mirrored past the map under the hills. Hidden: Outside() lays real pools with rock rims past the map
            }
            // the axis check: the sheets must lie round the map, 0..168 x, 0..-220 z (a model imported the other way round lands 180 degrees off)
            var bounds = new Bounds(); bool any = false;
            foreach (var r in dress.GetComponentsInChildren<MeshRenderer>()) { if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds); }
            if (any && (bounds.center - new Vector3(84, 0, -110)).sqrMagnitude > 900) { dress.transform.rotation = Quaternion.Euler(0, 180, 0); Debug.LogWarning("[OpenZ] L07: dress model turned 180 degrees, read the axis check"); }
            string tones = matVol2 ? ToneVolcanoes(file.place, groups, matVol2) : "tones: none";   // row 31 v2 fix 3: a share of the clean volcanoes a little whiter, fixed per piece from its place
            string outside = Outside(root.transform, matLava, matBank);   // row 31 v2: the ground past the map, its pools and rock rims
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            string s = $"L07 rim: {placed} placed ({string.Join(", ", groups.Select(g => g.Key + " " + g.Value.childCount))}), {sheets} border sheets, {banks} bank batches hidden; centre {bounds.center}; {cleanNote}; {tones}; {outside}";
            Debug.Log("[OpenZ] " + s); return s;
        }

        // ------------------------------------------------------------------ row 31 v2: everything past the map's edge improved, every border volcano kept where it stands
        // One data file, Models/L07/L07_outside.bytes (written off-line, v1_3/l07_finish/outside_area_v2/tools/pack_v2.py), becomes the Mesh assets in Models/L07/Outside:
        //  kind 0: the ground past the map on OpenZ/Sand (M_L07_Outer, a copy of M_L07_Ground on its own paint): TER_Outer's hills, re-meshed, its first ring = TER_Ground's rim
        //  kind 2: the lava of the outer pools and streams (M_L07_LavaOuter, a copy of the lava material with its own field)
        //  kind 3: rock rims, round 4's bank kit pieces on M_L07_Bank, round the new pools past the map
        //  kind 4: V1-V3 of the border volcanoes, LOD0's own shape with its folds softened, new UVs on L07_vol_atlas_v2 (no baked sun, no smear), in the imported LOD0's own space
        // Fixed meshes, no LODGroup: a piece stops drawing only when it is wholly outside the picture. The old outer hills, Environment/TER_Outer, go inactive. View only.
        const string OutsideData = ModelDir + "/L07_outside.bytes", OutsideDir = ModelDir + "/Outside";
        const string VolAtlas2 = ModelDir + "/L07_vol_atlas_v2.png", VolEmis2 = ModelDir + "/L07_vol_emis_v2.png";

        class OutRec { public string name; public int kind; public Vector3[] pos, nrm; public Vector2[] uv; public Color32[] col; public int[] idx; }
        class OutFile { public Vector4 paintRect, fieldRect; public List<OutRec> recs = new List<OutRec>(); }

        static OutFile ReadOutside(out string err)
        {
            err = null;
            if (!File.Exists(OutsideData)) { err = "no " + OutsideData; return null; }
            using (var br = new BinaryReader(new MemoryStream(File.ReadAllBytes(OutsideData))))
            {
                if (System.Text.Encoding.ASCII.GetString(br.ReadBytes(4)) != "OZOA" || br.ReadInt32() != 2) { err = "not an outside data file, version 2: " + OutsideData; return null; }
                var data = new OutFile();
                data.paintRect = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                data.fieldRect = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                int count = br.ReadInt32();
                for (int m = 0; m < count; m++)
                {
                    var r = new OutRec { name = System.Text.Encoding.ASCII.GetString(br.ReadBytes(br.ReadInt32())), kind = br.ReadInt32() };
                    int nv = br.ReadInt32(), nIdx = br.ReadInt32();
                    if (r.kind < 0 || r.kind > 4 || nv <= 0 || nIdx % 3 != 0) { err = "bad mesh " + r.name + " in " + OutsideData; return null; }
                    r.pos = new Vector3[nv]; r.nrm = new Vector3[nv]; r.uv = new Vector2[nv]; r.col = new Color32[nv]; r.idx = new int[nIdx];
                    for (int i = 0; i < nv; i++) r.pos[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    for (int i = 0; i < nv; i++) r.nrm[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    for (int i = 0; i < nv; i++) r.uv[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
                    for (int i = 0; i < nv; i++) { var b = br.ReadBytes(4); r.col[i] = new Color32(b[0], b[1], b[2], b[3]); }
                    for (int i = 0; i < nIdx; i++) r.idx[i] = br.ReadInt32();
                    data.recs.Add(r);
                }
                if (br.BaseStream.Position != br.BaseStream.Length) { err = "outside data longer than read: " + OutsideData; return null; }
                return data;
            }
        }

        static Mesh OutsideMesh(OutRec r, bool tangents)
        {
            string path = $"{OutsideDir}/{r.name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); bool fresh = !mesh;
            if (fresh) mesh = new Mesh();
            else
            {
                var so0 = new SerializedObject(mesh); var rd0 = so0.FindProperty("m_IsReadable");          // a run before set it not readable: open it again to write
                if (rd0 != null && !rd0.boolValue) { rd0.boolValue = true; so0.ApplyModifiedPropertiesWithoutUndo(); }
                mesh.Clear();
            }
            mesh.name = r.name; mesh.indexFormat = r.pos.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(r.pos); mesh.SetNormals(r.nrm); mesh.SetUVs(0, r.uv); mesh.SetColors(r.col); mesh.SetTriangles(r.idx, 0, true);
            if (tangents) mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            if (fresh) AssetDatabase.CreateAsset(mesh, path);
            var so = new SerializedObject(mesh); var rd = so.FindProperty("m_IsReadable");                  // the game never reads them: no CPU copy in the player
            if (rd != null) { rd.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(mesh); return mesh;
        }

        /// <summary>V1-V3's clean meshes: written once, then checked against the imported LOD0 (box, and its untouched foot ring vertex for vertex) before any prefab uses them.</summary>
        static Dictionary<string, Mesh> CleanVolcanoes(Dictionary<string, MeshFilter> meshes, out string note)
        {
            var data = ReadOutside(out string err);
            if (data == null) { note = "clean volcanoes not used: " + err; return null; }
            if (!File.Exists(VolAtlas2) || !File.Exists(VolEmis2)) { note = "clean volcanoes not used: no " + VolAtlas2; return null; }
            ImportTex(VolAtlas2, 4096, 8, TextureWrapMode.Clamp, true, true, true);   // V1-V3 albedo, 4096, BC7, no light in it
            ImportTex(VolEmis2, 1024, 1, TextureWrapMode.Clamp, true, true, true);    // the old foot glow, carried over point by point
            Directory.CreateDirectory(OutsideDir); AssetDatabase.Refresh();
            var made = new Dictionary<string, Mesh>(); var bad = new List<string>();
            foreach (var r in data.recs)
            {
                if (r.kind != 4) continue;
                string node = r.name.EndsWith("_v2") ? r.name.Substring(0, r.name.Length - 3) : r.name;
                if (!meshes.TryGetValue(node, out var src)) { bad.Add(node + " is not in the model"); continue; }
                var b0 = src.sharedMesh.bounds; var m = OutsideMesh(r, false);
                if ((m.bounds.min - b0.min).magnitude > 0.3f || (m.bounds.max - b0.max).magnitude > 0.3f) { bad.Add($"{node}: box {m.bounds.min} {m.bounds.max} against the model's {b0.min} {b0.max}"); continue; }
                try
                {   // the foot ring was not softened: each of its vertices in the imported LOD0 must be in the new mesh to the millimetre (a turned or mirrored mesh fails here)
                    using (var arr = Mesh.AcquireReadOnlyMeshData(src.sharedMesh))
                    {
                        var md = arr[0]; var sv = new Unity.Collections.NativeArray<Vector3>(md.vertexCount, Unity.Collections.Allocator.Temp);
                        md.GetVertices(sv); int tried = 0, found = 0;
                        for (int i = 0; i < sv.Length; i++)
                        {
                            if (sv[i].z < -0.1f) continue;                                             // the foot: the file's z under 0.1 m (Unity's mesh holds z turned)
                            tried++; float best = float.MaxValue;
                            foreach (var q in r.pos) best = Mathf.Min(best, (q - sv[i]).sqrMagnitude);
                            if (best < 1e-6f) found++;
                        }
                        sv.Dispose();
                        if (tried < 8 || found < tried) bad.Add($"{node}: {found} of {tried} foot vertices of the model found in the new mesh");
                    }
                }
                catch (Exception e) { Debug.LogWarning("[OpenZ] L07: foot check of " + node + " skipped (" + e.Message + "); the box check passed"); }
                made[node] = m;
            }
            if (bad.Count > 0 || made.Count == 0) { Debug.LogError("[OpenZ] L07: clean volcanoes NOT used, they draw as before: " + string.Join("; ", bad)); note = "clean volcanoes NOT used (see the error)"; return null; }
            note = $"clean volcanoes {made.Count} ({string.Join(", ", made.Keys)})"; return made;
        }

        // Same atlas, same mesh: copies of M_L07_VolcanoV2 with a light linear tint on _BaseColor (TeamLit: albedo = atlas x _BaseColor), picked per piece by a fixed hash of its place
        // (x and z in whole centimetres, from the place list): never random at run time, nothing in the sim. Tone 0 is M_L07_VolcanoV2 itself. Off-line twin: outside_area_v2/tools/c00_tones.py.
        static readonly float[] VolTint = { 1.00f, 1.04f, 1.10f, 1.17f }, VolCum = { 0.30f, 0.58f, 0.81f, 1.00f };
        const uint VolSalt = 13626u;

        static int VolTone(float x, float z)
        {
            unchecked
            {
                uint h = (uint)Mathf.RoundToInt(x * 100f) * 73856093u ^ (uint)Mathf.RoundToInt(z * 100f) * 19349663u ^ VolSalt;
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
                float u = (h & 0xFFFFu) / 65536f;
                for (int i = 0; i < VolCum.Length; i++) if (u < VolCum[i]) return i;
                return VolCum.Length - 1;
            }
        }

        static string ToneVolcanoes(Row[] place, Dictionary<string, Transform> groups, Material baseMat)
        {
            var mats = new Material[VolTint.Length]; mats[0] = baseMat;
            for (int t = 1; t < mats.Length; t++) { mats[t] = RimMat("M_L07_VolcanoV2_T" + t, VolAtlas2, VolEmis2, 2.2f, new Color(VolTint[t], VolTint[t], VolTint[t], 1)); mats[t].SetFloat("_FlatShade", 1); }
            var next = new Dictionary<string, int>(); var n = new int[mats.Length];
            foreach (var q in place)
            {
                next.TryGetValue(q.row, out int k); next[q.row] = k + 1;                     // the k-th piece of its row: the place loop put them under their row in this order
                var r = groups[q.row].GetChild(k).GetComponentInChildren<MeshRenderer>();
                if (!r || r.sharedMaterial != baseMat) continue;                            // V1-V3 on their clean mesh only: the crater V4, the pillars and the brown cones keep theirs
                int t = VolTone(q.x, q.z); n[t]++;
                if (t == 0) continue;
                r.sharedMaterial = mats[t]; PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            return $"tones {string.Join("/", n)} (M_L07_VolcanoV2, _T1 .. _T{mats.Length - 1})";
        }

        static string Outside(Transform root, Material matLava, Material matBank)
        {
            var data = ReadOutside(out string err);
            if (data == null) return "outside: " + err;
            var ground = AssetDatabase.LoadAssetAtPath<Material>($"{MatEnv}/M_L07_Ground.mat");
            if (!ground) return "outside: M_L07_Ground missing: run OpenZ/L07 Volcanic/T1 first";
            string paint = ModelDir + "/L07_outer_paint.png", sdf = ModelDir + "/L07_outer_sdf.png", field = ModelDir + "/T_L07_FieldOuter.png";
            ImportTex(paint, 8192, 16, TextureWrapMode.Mirror, true, true, true);         // 6144 x 6144 RGBA over 556 x 608 m, BC7, A = shore heat; mirrored past its edge, as it was painted
            ImportTex(sdf, 32, 1, TextureWrapMode.Clamp, false, false, false);           // 4 x 4: no road, no crack
            ImportTex(field, 2048, 0, TextureWrapMode.Clamp, false, false, false);       // the lava field: read as numbers, never compressed

            string pOuter = ModelDir + "/M_L07_Outer.mat", pLava = ModelDir + "/M_L07_LavaOuter.mat";
            var matOuter = AssetDatabase.LoadAssetAtPath<Material>(pOuter);
            if (!matOuter) { matOuter = new Material(ground) { name = "M_L07_Outer" }; AssetDatabase.CreateAsset(matOuter, pOuter); }
            else { matOuter.shader = ground.shader; matOuter.CopyPropertiesFromMaterial(ground); }
            matOuter.shaderKeywords = ground.shaderKeywords;                                // _OZ_L07_GROUND: the play area's own ground path, grain and heat
            matOuter.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture>(paint)); matOuter.SetTextureScale("_BaseMap", Vector2.one); matOuter.SetTextureOffset("_BaseMap", Vector2.zero);
            matOuter.SetTexture("_GroundSdf", AssetDatabase.LoadAssetAtPath<Texture>(sdf)); matOuter.SetVector("_GroundSize", new Vector4(data.paintRect.z, data.paintRect.w, 0, 0));
            EditorUtility.SetDirty(matOuter);
            var matLavaOut = AssetDatabase.LoadAssetAtPath<Material>(pLava);
            if (!matLavaOut) { matLavaOut = new Material(matLava) { name = "M_L07_LavaOuter" }; AssetDatabase.CreateAsset(matLavaOut, pLava); }
            else { matLavaOut.shader = matLava.shader; matLavaOut.CopyPropertiesFromMaterial(matLava); }
            matLavaOut.SetTexture("_FieldMap", AssetDatabase.LoadAssetAtPath<Texture>(field)); matLavaOut.SetVector("_FieldRect", data.fieldRect); EditorUtility.SetDirty(matLavaOut);

            Directory.CreateDirectory(OutsideDir); AssetDatabase.Refresh();
            var parent = new GameObject("Outside").transform; parent.SetParent(root, false);
            int[] n = new int[4], tris = new int[4];
            foreach (var r in data.recs)
            {
                if (r.kind == 4 || r.kind == 1) continue;                                       // the volcano meshes go to the prefabs (CleanVolcanoes)
                var mesh = OutsideMesh(r, false);
                var mat = r.kind == 0 ? matOuter : r.kind == 2 ? matLavaOut : matBank;
                Part(parent, r.name, mesh, mat, r.kind == 3 ? ShadowCastingMode.On : ShadowCastingMode.Off);
                n[r.kind]++; tris[r.kind] += r.idx.Length / 3;
            }
            var env = GameObject.Find("Environment"); var outer = env ? env.transform.Find("TER_Outer") : null;
            if (outer) outer.gameObject.SetActive(false); else Debug.LogWarning("[OpenZ] L07: Environment/TER_Outer not found, the old outer hills may still show");
            return $"outside: ground {n[0]} ({tris[0]} tris), lava {n[2]} ({tris[2]}), rocks {n[3]} ({tris[3]}); TER_Outer {(outer ? "off" : "NOT FOUND")}";
        }
    }
}
