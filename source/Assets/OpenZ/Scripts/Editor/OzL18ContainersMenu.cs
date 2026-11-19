// OpenZ (GPL-3.0) - version 1.5, level 18 "Bridge Game" (kit level18_containers): the editor steps for the shipping containers in the water.
// 22: the containers (Models/L18/Ruins/L18C_Container_A/B/C/W.fbx) placed from L18_containers_placement.json as scene objects under
//     Environment/L18_Containers: per container a host "<id>_container" at the footprint of the rule (Sim/ZWorld.L18Containers.cs, DV201), a
//     child "Standing" (the variant, its LOD group 12 % / 3.5 % / 0.4 %, red or blue-grey) and a hidden child "Wreck" (the crushed state the
//     view shows after the break-up, at its rest: its top 0.4 m over the water). No colliders (the rules know the containers; the view binds
//     each host to its rule object at the start: View/OzL18Containers.cs).
// 23: the check of the open scene (no write).
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL18ContainersMenu
    {
        const string Dir = "Assets/OpenZ/Models/L18/Ruins", Tex = "Assets/OpenZ/Textures/L18/Ruins", Mats = "Assets/OpenZ/Materials/L18";
        const string Json = Dir + "/L18_containers_placement.json";
        const float BoxH = 2.6f, WreckTop = 0.4f;

        [System.Serializable] public class Item { public string id, variant, colour; public int[] tile; public float yaw, pitch, roll, below, dx, dz, wreck_roll, wreck_pitch; }
        [System.Serializable] public class Doc { public float water_y = -0.8f; public Item[] containers; }

        public static Doc Load() => File.Exists(Json) ? JsonUtility.FromJson<Doc>(File.ReadAllText(Json)) : null;

        /// <summary>The middle of the footprint in the world (2 m tiles: x = tile x * 2, z = - tile y * 2).</summary>
        public static Vector3 FootCentre(int[] t) => new Vector3((t[0] + t[2] * 0.5f) * 2f, 0f, -(t[1] + t[3] * 0.5f) * 2f);

        /// <summary>The standing box's turn: yaw (the long side along +X at 0), then the pitch (one end down, about the short axis) and the roll
        /// (about the long axis: onto a side).</summary>
        public static Quaternion Turn(float yaw, float pitch, float roll) => Quaternion.Euler(0f, yaw, 0f) * Quaternion.AngleAxis(pitch, Vector3.forward) * Quaternion.AngleAxis(roll, Vector3.right);

        static Material Mat(string colour)
        {
            if (!AssetDatabase.IsValidFolder(Mats)) AssetDatabase.CreateFolder("Assets/OpenZ/Materials", "L18");
            string name = "M_L18C_Container_" + colour, p = $"{Mats}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (!m) { m = new Material(Shader.Find("OpenZ/TeamLit")); AssetDatabase.CreateAsset(m, p); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Tex}/L18C_Container_{colour}_BaseColor.png")); m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_UseVertexColor", 0); m.SetFloat("_TeamMode", 0); m.SetFloat("_Smoothness", 0.12f); m.SetFloat("_Metallic", 0);
            if (m.HasProperty("_Stylize")) m.SetFloat("_Stylize", 1);
            if (m.HasProperty("_Degrime")) m.SetFloat("_Degrime", 0);
            if (m.HasProperty("_Degreen")) m.SetFloat("_Degreen", 0);
            m.SetColor("_RimColor", Color.black);
            m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
        }

        /// <summary>One container model (a variant's three meshes behind one LOD group, its material, no collider) under the parent.</summary>
        static GameObject Model(string variant, Material mat, Transform parent, string name, ref int tris)
        {
            string piece = "L18C_Container_" + variant;
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/{piece}.fbx");
            if (!fbx) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
            go.name = name;
            var all = go.GetComponentsInChildren<MeshRenderer>(true);
            MeshRenderer A = all.FirstOrDefault(r => r.name == piece), B = all.FirstOrDefault(r => r.name == piece + "_LOD1"), C = all.FirstOrDefault(r => r.name == piece + "_LOD2");
            if (!A || !B || !C) { Object.DestroyImmediate(go); return null; }
            foreach (var r in all)
            {
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = r == C ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var lg = go.AddComponent<LODGroup>(); lg.fadeMode = LODFadeMode.None;
            lg.SetLODs(new[] { new LOD(0.12f, new Renderer[] { A }), new LOD(0.035f, new Renderer[] { B }), new LOD(0.004f, new Renderer[] { C }) });
            lg.RecalculateBounds();
            tris += A.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
            return go;
        }

        static Bounds Lod0Bounds(GameObject go)
        {
            var lg = go.GetComponent<LODGroup>(); var r0 = lg.GetLODs()[0].renderers;
            var b = r0[0].bounds; foreach (var r in r0) b.Encapsulate(r.bounds); return b;
        }

        [MenuItem("OpenZ/L18 Bridge Game/22 Containers in the water (open scene)", false, 7722)]
        public static void PlaceMenu() => Debug.Log("[OpenZ] L18 containers: " + Place());

        public static string Place()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.StartsWith(OzCity.BridgeGame)) return "open the level 18 scene first (" + scene.name + ")";
            var doc = Load();
            if (doc?.containers == null) return "missing or empty " + Json;
            var envGo = GameObject.Find("Environment");
            var old = envGo ? envGo.transform.Find(OzL18Containers.RootName) : null; if (old) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(OzL18Containers.RootName).transform; if (envGo) root.SetParent(envGo.transform, false);
            int n = 0, tris = 0; var sb = new StringBuilder();
            foreach (var it in doc.containers)
            {
                int k = System.Array.IndexOf(ZWorld.L18ContainerIds, it.id);
                if (k < 0 || it.tile == null || it.tile.Length != 4) { sb.Append(it.id + " not in the rule's table; "); continue; }
                var t = ZWorld.L18ContainerTiles;
                if (t[k * 4] != it.tile[0] || t[k * 4 + 1] != it.tile[1] || t[k * 4 + 2] != it.tile[2] || t[k * 4 + 3] != it.tile[3]) sb.Append(it.id + " footprint differs from the rule's table (placed by the file); ");
                var host = new GameObject(it.id + "_container").transform; host.SetParent(root, false);
                var c = FootCentre(it.tile) + new Vector3(it.dx, 0f, it.dz);
                host.position = new Vector3(c.x, doc.water_y, c.z);
                var mat = Mat(it.colour);
                var st = Model(it.variant, mat, host, "Standing", ref tris);
                if (!st) { sb.Append(it.id + " model missing; "); Object.DestroyImmediate(host.gameObject); continue; }
                var rot = Turn(it.yaw, it.pitch, it.roll);
                var centre = new Vector3(c.x, doc.water_y - it.below, c.z);
                st.transform.SetPositionAndRotation(centre - rot * new Vector3(0f, BoxH * 0.5f, 0f), rot);   // the pivot is the middle of the base
                int wt = 0;
                var wr = Model("W", mat, host, "Wreck", ref wt);
                if (wr)
                {
                    var wrot = Turn(it.yaw, it.wreck_pitch, it.roll + it.wreck_roll);
                    wr.transform.SetPositionAndRotation(centre - wrot * new Vector3(0f, BoxH * 0.5f, 0f), wrot);
                    var wb = Lod0Bounds(wr);
                    wr.transform.position += Vector3.up * (doc.water_y + WreckTop - wb.max.y);                     // its top 0.4 m over the water
                    wr.SetActive(false);
                }
                n++;
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            return $"{n} of {doc.containers.Length} containers placed under Environment/{OzL18Containers.RootName}, {tris} triangles at the nearest level, 0 colliders; {sb} SAVE THE SCENE";
        }

        [MenuItem("OpenZ/L18 Bridge Game/23 Containers: check the open scene (no write)", false, 7723)]
        public static void CheckMenu() => Debug.Log("[OpenZ] L18 containers check: " + Check());

        public static string Check()
        {
            var env = GameObject.Find("Environment");
            var root = env ? env.transform.Find(OzL18Containers.RootName) : null;
            if (!root) return "FAIL no Environment/" + OzL18Containers.RootName;
            var doc = Load(); int pass = 0, all = 0; var sb = new StringBuilder();
            foreach (var it in doc.containers)
            {
                all++;
                var h = root.Find(it.id + "_container");
                var st = h ? h.Find("Standing") : null; var wr = h ? h.Find("Wreck") : null;
                bool ok = h && st && wr && !wr.gameObject.activeSelf && h.GetComponentsInChildren<Collider>(true).Length == 0;
                var why = new StringBuilder();
                if (ok)
                {
                    var b = Lod0Bounds(st.gameObject); var c = FootCentre(it.tile);
                    float off = new Vector2(b.center.x - c.x, b.center.z - c.z).magnitude;
                    bool half = b.min.y < doc.water_y - 0.4f && b.max.y > doc.water_y + 0.6f;                     // partly under, partly over the water
                    bool lit = st.GetComponentsInChildren<Renderer>(true).All(r => r.sharedMaterial && r.sharedMaterial.shader && r.sharedMaterial.shader.name == "OpenZ/TeamLit");
                    ok = off < 1.2f && half && lit;
                    why.Append($"middle {off:0.00} m off the footprint's, {b.min.y - doc.water_y:0.00} .. {b.max.y - doc.water_y:+0.00} m round the water, TeamLit {lit}");
                }
                else why.Append("host / Standing / Wreck missing, the wreck shown or a collider");
                if (ok) pass++;
                sb.Append($"\n  {(ok ? "PASS" : "FAIL")} {it.id} {it.variant} {it.colour}: {why}");
            }
            return $"{(pass == all ? "PASS" : "FAIL")} {pass}/{all}" + sb;
        }
    }
}
