// OpenZ (GPL-3.0) - version 1.5, level 18 "Bridge Game" (kit level18_assets, Unity pass of 12 Nov): editor steps for the kit's models.
// 0: the long city bridge family (Models/Environment/BRG_city_long.fbx, made by the city bridge's own generator at 24 m) -> the four
//    prefabs Z_bridge_city_long_intact / _damaged / _destroyed / _chunks that OzBridgeKit asks for.
// 4: the ruin pieces (Models/L18/Ruins) placed from L18_ruins_placement.json as static scene objects with LOD groups. View only, no
//    colliders, no rule reads them. The two river stumps of the file are NOT placed here: they need their own rule row first.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL18Assets
    {
        const string Env = "Assets/OpenZ/Models/Environment", Pref = "Assets/OpenZ/Prefabs", MatEnv = "Assets/OpenZ/Materials/Env";
        const string Long = Env + "/BRG_city_long.fbx", Ruins = "Assets/OpenZ/Models/L18/Ruins", RuinTex = "Assets/OpenZ/Textures/L18/Ruins";
        const string RuinMats = "Assets/OpenZ/Materials/L18", RootName = "L18_Surroundings";

        [MenuItem("OpenZ/L18 Bridge Game/0 Long bridge family (prefabs)", false, 7699)]
        public static void LongBridgeMenu() => Debug.Log("[OpenZ] L18 long bridge family: " + LongBridge());

        public static string LongBridge()
        {
            var imp = AssetImporter.GetAtPath(Long) as ModelImporter;
            if (imp == null) return "missing " + Long;
            imp.importCameras = false; imp.importLights = false; imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; imp.bakeAxisConversion = true;
            imp.isReadable = true;                                                                  // chunks get run-time convex colliders, as the 12 m kit
            int mapped = 0;
            foreach (var n in new[] { "M_BRGZ_City_Plate_VC", "M_BRGZ_City_Steel_VC", "M_BRGZ_City_Concrete_VC" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{MatEnv}/{n}.mat");               // the city bridge's own materials, referenced, never changed
                if (!m) return "missing material " + n;
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), m); mapped++;
            }
            imp.SaveAndReimport();
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Long);
            int n3 = 0;
            foreach (var state in new[] { "intact", "damaged", "destroyed" })
            {
                var child = fbx.transform.Cast<Transform>().FirstOrDefault(t => t.name == "BRG_city_long_" + state);
                if (!child) return "missing BRG_city_long_" + state;
                var root = new GameObject("Z_bridge_city_long_" + state);
                var inst = Object.Instantiate(child.gameObject, root.transform, false); inst.name = "Model"; inst.transform.localPosition = Vector3.zero;   // modelled centred on the pivot
                foreach (var r in inst.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
                root.AddComponent<TeamPaint>();
                PrefabUtility.SaveAsPrefabAsset(root, $"{Pref}/Z_bridge_city_long_{state}.prefab"); Object.DestroyImmediate(root); n3++;
            }
            var chunks = new GameObject("Z_bridge_city_long_chunks"); int nc = 0;
            for (int i = 0; i < 8; i++)
            {
                var ch = fbx.transform.Cast<Transform>().FirstOrDefault(t => t.name == "BRG_city_long_chunk_" + i);
                if (!ch) continue;
                var piece = Object.Instantiate(ch.gameObject, chunks.transform, false); piece.name = ch.name; piece.transform.localPosition = Vector3.zero; nc++;
            }
            PrefabUtility.SaveAsPrefabAsset(chunks, $"{Pref}/Z_bridge_city_long_chunks.prefab"); Object.DestroyImmediate(chunks);
            AssetDatabase.SaveAssets();
            int added = 0;
            var zg = Object.FindFirstObjectByType<ZGame>();
            if (zg && zg.level == OzCity.BridgeGame)
            {
                var have = (zg.unitPrefabs ?? new GameObject[0]).Where(g => g).ToList();
                foreach (var part in new[] { "intact", "damaged", "destroyed", "chunks" })
                {
                    var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Pref}/Z_bridge_city_long_{part}.prefab");
                    if (pf && !have.Contains(pf)) { have.Add(pf); added++; }
                }
                if (added > 0) { zg.unitPrefabs = have.ToArray(); EditorUtility.SetDirty(zg); EditorSceneManager.MarkSceneDirty(zg.gameObject.scene); }
            }
            return $"{mapped} materials mapped, {n3} prefabs, {nc} collapse pieces, {added} added to the open level's run-time list";
        }

        // ------------------------------------------------------------------------------------------------ ruins
        [System.Serializable] class Item { public string id, piece, @base, ring; public float x, y, z, scale, tilt_deg, tilt_heading_deg, sink_extra_m, unity_yaw_deg; }
        [System.Serializable] class Sur { public Item[] items; }
        [System.Serializable] class Doc { public Sur surroundings; }

        static Material RuinMat(string name)
        {
            if (!AssetDatabase.IsValidFolder(RuinMats)) AssetDatabase.CreateFolder("Assets/OpenZ/Materials", "L18");
            string p = $"{RuinMats}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (!m) { m = new Material(Shader.Find("OpenZ/TeamLit")); AssetDatabase.CreateAsset(m, p); }
            string texName = name.Substring(2) + "_BaseColor";                                       // M_L18R_StumpA_LOD2 -> L18R_StumpA_LOD2_BaseColor
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{RuinTex}/{texName}.png");
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_UseVertexColor", 0); m.SetFloat("_TeamMode", 0); m.SetFloat("_Smoothness", 0.1f); m.SetFloat("_Metallic", 0);
            if (m.HasProperty("_Stylize")) m.SetFloat("_Stylize", 1);
            if (m.HasProperty("_Degrime")) m.SetFloat("_Degrime", 0);
            if (m.HasProperty("_Degreen")) m.SetFloat("_Degreen", 0);
            m.SetColor("_RimColor", Color.black);
            m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
        }

        /// <summary>One copy of a ruin piece: the model's three meshes behind one LOD group (12 % / 3.5 % / 0.4 % of the screen height, no cross-fade),
        /// its own materials, static, no collider. farOnly = the shell alone (far ring).</summary>
        static GameObject Piece(string piece, Transform parent, bool farOnly, ref int tris)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{Ruins}/{piece}.fbx");
            if (!fbx) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
            var all = go.GetComponentsInChildren<MeshRenderer>(true);
            MeshRenderer A = all.FirstOrDefault(r => r.name == piece), B = all.FirstOrDefault(r => r.name == piece + "_LOD1"), C = all.FirstOrDefault(r => r.name == piece + "_LOD2");
            if (!A || !C) { Object.DestroyImmediate(go); return null; }
            foreach (var r in all)
            {
                string mn = r == C ? $"M_{piece}_LOD2" : r == B && piece == "L18R_FactoryHall" ? "M_L18R_FactoryHall_LOD1" : $"M_{piece}";
                r.sharedMaterials = Enumerable.Repeat(RuinMat(mn), r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = r == C ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true; r.gameObject.isStatic = true;
            }
            var lg = go.AddComponent<LODGroup>(); lg.fadeMode = LODFadeMode.None;
            if (farOnly)
            {
                A.gameObject.SetActive(false); if (B) B.gameObject.SetActive(false);
                lg.SetLODs(new[] { new LOD(0.004f, new Renderer[] { C }) });
            }
            else lg.SetLODs(B ? new[] { new LOD(0.12f, new Renderer[] { A }), new LOD(0.035f, new Renderer[] { B }), new LOD(0.004f, new Renderer[] { C }) }
                              : new[] { new LOD(0.12f, new Renderer[] { A }), new LOD(0.004f, new Renderer[] { C }) });
            lg.RecalculateBounds();
            var mf = (farOnly ? C : A).GetComponent<MeshFilter>(); if (mf && mf.sharedMesh) tris += mf.sharedMesh.triangles.Length / 3;
            go.isStatic = true;
            return go;
        }

        [MenuItem("OpenZ/L18 Bridge Game/4 Surroundings (ruin pieces from the placement file, open scene)", false, 7704)]
        public static void SurroundingsMenu() => Debug.Log("[OpenZ] L18 surroundings: " + Surroundings());

        public static string Surroundings()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.StartsWith(OzCity.BridgeGame)) return "open the level 18 scene first (" + scene.name + ")";
            string json = $"{Ruins}/L18_ruins_placement.json", fitted = $"{Ruins}/L18_surroundings_placement.json";
            bool own = File.Exists(fitted); if (own) json = fitted;                    // kit level18_surroundings: the pieces fitted to the finished outer world, each with its own height
            if (!File.Exists(json)) return "missing " + json;
            var doc = JsonUtility.FromJson<Doc>(File.ReadAllText(json));
            if (doc?.surroundings?.items == null) return "no surroundings in " + json;
            var envGo = GameObject.Find("Environment");
            var old = envGo ? envGo.transform.Find(RootName) : null; if (old) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(RootName).transform; if (envGo) root.SetParent(envGo.transform, false);
            // the ground under a dry piece: the environment's own outer meshes (apron, border rock, ground), read through temporary colliders
            var temp = new List<MeshCollider>();
            if (envGo)
                foreach (var mf in envGo.GetComponentsInChildren<MeshFilter>(true))
                    if ((mf.name.StartsWith("TER_Apron") || mf.name.StartsWith("TER_Cliffs") || mf.name.StartsWith("TER_Ground") || mf.name.StartsWith("OUT3_Ground")) && mf.sharedMesh && !mf.GetComponent<Collider>())
                    { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temp.Add(mc); }
            Physics.SyncTransforms();
            float water = -0.8f; int n = 0, tris = 0, noGround = 0; var sb = new System.Text.StringBuilder();
            foreach (var it in doc.surroundings.items)
            {
                float y = water;
                if (own) y = it.y;                                                         // the fitted file carries the height (water line, apron, plateau)
                else if (it.@base == "ground")
                {
                    y = float.NaN;
                    foreach (var h in Physics.RaycastAll(new Ray(new Vector3(it.x, 300, it.z), Vector3.down), 600))
                        if (temp.Contains(h.collider as MeshCollider) && (float.IsNaN(y) || h.point.y > y)) y = h.point.y;
                    if (float.IsNaN(y)) { noGround++; sb.Append(it.id + " "); continue; }      // beyond the built ground (the outer ground is a later step): not placed, never left standing on the sea
                }
                var host = new GameObject($"{it.id}_{it.piece}").transform; host.SetParent(root, false);
                var d = new Vector3(Mathf.Cos(it.tilt_heading_deg * Mathf.Deg2Rad), 0, Mathf.Sin(it.tilt_heading_deg * Mathf.Deg2Rad));
                var tilt = Mathf.Abs(it.tilt_deg) > 0.01f ? Quaternion.AngleAxis(it.tilt_deg, Vector3.Cross(Vector3.up, d)) : Quaternion.identity;
                host.SetPositionAndRotation(new Vector3(it.x, y - it.sink_extra_m, it.z), tilt * Quaternion.Euler(0, it.unity_yaw_deg, 0));
                host.localScale = Vector3.one * (it.scale > 0 ? it.scale : 1f);
                var p = Piece(it.piece, host, it.ring == "far", ref tris);
                if (!p) { sb.Append(it.id + " model missing; "); Object.DestroyImmediate(host.gameObject); continue; }
                p.transform.localPosition = Vector3.zero; p.transform.localRotation = Quaternion.identity; p.transform.localScale = Vector3.one;
                host.gameObject.isStatic = true; n++;
            }
            foreach (var mc in temp) Object.DestroyImmediate(mc);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            return $"{n} of {doc.surroundings.items.Length} pieces placed under Environment/{RootName}, {tris} triangles at the nearest level, {noGround} NOT placed (no ground under them yet): {sb}";
        }
    }
}
