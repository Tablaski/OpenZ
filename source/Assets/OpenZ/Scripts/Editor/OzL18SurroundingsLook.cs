using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 level 18 "Bridge Game", kit level18_surroundings, the steps after menu "4 Surroundings" (judged on Play frames at the RTS camera).
    ///  5 Surroundings look:
    ///    a) the eight ruin pieces are bleached-white models; beside the block shells, the border rock and the forts they were the brightest
    ///       things in the picture. Their materials (Assets/OpenZ/Materials/L18/M_L18R_*, made by menu 4 with a white tint) get Tint, so they
    ///       sit in the Car Park's weathered concrete family. Menu 4 writes white again: run this after it.
    ///    b) the level's own water: with the Car Park's olive harbour water the river and the sea read as grass from above. Every water
    ///       surface of the open level 18 scene (river, bridge spans, canal cuts, dock basins, sea, the far skirt) takes ONE material,
    ///       Materials/Env/M_Water_L18 (the game's own water shader: darker and cooler, ripples and glints at every zoom, a foam line
    ///       where it meets walls and banks). The Car Park's material file is only read, never written. View only: no rule, no wading change.
    ///  6 Outer world model again: after a new L18_outer_v3.fbx the placed copy under Environment/CliffsV3 is replaced by a fresh one
    ///    (as menu "Cliffs v3 / 2 Place" makes it); then menus 4 and 5.
    /// Editor only. Menu 5 a changes material assets; 5 b and 6 change and save the open level 18 scene.
    /// </summary>
    public static class OzL18SurroundingsLook
    {
        public static readonly Color Tint = new Color(0.80f, 0.775f, 0.72f, 1f);
        const string Dir = "Assets/OpenZ/Materials/L18";
        public const string WaterMat = "Assets/OpenZ/Materials/Env/M_Water_L18.mat", HarbourMat = "Assets/OpenZ/Materials/Env/M_Water_Harbour.mat";
        const string OuterFbx = "Assets/OpenZ/Models/Environment/CliffsV3/L18_outer_v3.fbx", SceneEnd = "L18_BridgeGame.unity";

        [MenuItem("OpenZ/L18 Bridge Game/5 Surroundings look (after 4: the ruin pieces' tint, the level's own water)", false, 7705)]
        public static void Menu() => Debug.Log("[OpenZ] L18 surroundings look: " + Apply() + "; " + Water());

        [MenuItem("OpenZ/L18 Bridge Game/6 Outer world model again (after a new L18_outer_v3.fbx; then 4 and 5)", false, 7706)]
        public static void MenuOuter() => Debug.Log("[OpenZ] L18 outer world: " + RefreshOuter());

        public static string Apply() => Set(Tint);

        public static string Set(Color c)
        {
            if (!Directory.Exists(Dir)) return "missing " + Dir + " (menu 4 first)";
            int n = 0;
            foreach (string g in AssetDatabase.FindAssets("t:Material", new[] { Dir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!Path.GetFileName(p).StartsWith("M_L18R_")) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (!m || !m.HasProperty("_BaseColor")) continue;
                if (m.GetColor("_BaseColor") != c) { m.SetColor("_BaseColor", c); EditorUtility.SetDirty(m); }
                n++;
            }
            AssetDatabase.SaveAssets();
            return n + " ruin piece materials at tint (" + c.r.ToString("0.00") + ", " + c.g.ToString("0.00") + ", " + c.b.ToString("0.00") + ")";
        }

        /// <summary>The check's line: every ruin piece material carries the tint.</summary>
        public static bool AllTinted()
        {
            var ms = AssetDatabase.FindAssets("t:Material", new[] { Dir }).Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetFileName(p).StartsWith("M_L18R_")).Select(AssetDatabase.LoadAssetAtPath<Material>).ToList();
            return ms.Count > 0 && ms.All(m => m && m.GetColor("_BaseColor") == Tint);
        }

        /// <summary>Level 18's own water material on every water surface of the open level 18 scene. Run again after any step that places water.</summary>
        public static string Water()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.path.EndsWith(SceneEnd)) return "water: open the level 18 scene first (nothing changed)";
            if (EditorApplication.isPlaying) return "water: not in Play (nothing changed)";
            var own = AssetDatabase.LoadAssetAtPath<Material>(WaterMat); var harbour = AssetDatabase.LoadAssetAtPath<Material>(HarbourMat);
            if (!own) return "water: missing " + WaterMat;
            int set = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var ms = r.sharedMaterials; bool ch = false;
                for (int i = 0; i < ms.Length; i++) if (ms[i] == harbour) { ms[i] = own; ch = true; }
                if (ch) { r.sharedMaterials = ms; EditorUtility.SetDirty(r); set++; }
            }
            if (set > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            return "water: " + set + " surfaces changed, " + WaterUsers(out int left) + " on M_Water_L18, " + left + " left on the Car Park's";
        }

        /// <summary>Water surfaces of the open scene on the level's own material; left = those still on the Car Park's.</summary>
        public static int WaterUsers(out int left)
        {
            var own = AssetDatabase.LoadAssetAtPath<Material>(WaterMat); var harbour = AssetDatabase.LoadAssetAtPath<Material>(HarbourMat);
            int n = 0; left = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r.sharedMaterials.Any(m => m && m == own)) n++;
                if (r.sharedMaterials.Any(m => m && m == harbour)) left++;
            }
            return n;
        }

        /// <summary>The placed outer world under Environment/CliffsV3 made again from the model file (new parts, new material slots).</summary>
        public static string RefreshOuter()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.path.EndsWith(SceneEnd)) return "open the level 18 scene first (nothing changed)";
            if (EditorApplication.isPlaying) return "not in Play (nothing changed)";
            var env = GameObject.Find("Environment"); var root = env ? env.transform.Find("CliffsV3") : null;
            if (!root) return "no Environment/CliffsV3: menu 'Cliffs v3 / 2 Place in open level' first";
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(OuterFbx);
            if (!src) return "missing " + OuterFbx;
            int old = 0;
            foreach (var t in root.Cast<Transform>().Where(t => t.name == src.name).ToList()) { old += t.GetComponentsInChildren<MeshRenderer>(true).Length; Object.DestroyImmediate(t.gameObject); }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.SetParent(root, true); go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            int parts = 0, loose = 0; long tris = 0;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool water = r.name.StartsWith("OUT3_SeaWater");
                r.shadowCastingMode = water ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                parts++; loose += r.sharedMaterials.Count(m => !m || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m)) || AssetDatabase.GetAssetPath(m).EndsWith(".fbx"));
                var mf = r.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh) for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) tris += mf.sharedMesh.GetIndexCount(s) / 3;
            }
            int colliders = go.GetComponentsInChildren<Collider>(true).Length;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return $"{old} old parts out, {parts} parts in ({tris} triangles), material slots without a project material {loose}, colliders {colliders}; now menus 4 and 5";
        }
    }
}
