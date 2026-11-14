using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.3 item 10 (L17 Car Park stop signs "facing away from the default camera ... they only say 'op'"). Two faults, both fixed in data:
    ///  a) the scene placed the two signs (map_object13 at tiles 15,10 and 17,10) with a random yaw (OpenZSetup: rng.Next(360) = 199 and
    ///     249 deg), so their face looked north / east, away from the RTS camera in the south;
    ///  b) the prefab's octagon plates (Stop_red_face, Stop_ivory_border, Stop_steel_edge) had their 22.5 deg octagon spin applied as a
    ///     YAW, so each plate stood 22.5 deg turned against the letters: on the left half the red plate came up to 8.6 cm in FRONT of
    ///     the letters and hid the "S" and the "T".
    /// Counted check (edit mode, nothing saved; opens the L17 scene additively when it is not open):
    ///  T1 the signs in the scene = the map's map_object13 entries (L17: 2);
    ///  T2 every sign's face turns to the default game camera (yaw 0 view from the south): yaw error 1 deg or less;
    ///  T3 every plate stands parallel to the letters (angle 0.5 deg or less);
    ///  T4 letter vertices buried behind a plate's front surface (inside its radius, less than 2 mm in front): 0 (before the fix about
    ///     half of them: the S and the T);
    /// Plus game-camera shots in Play (menu "Shots in Play"): each sign at the default 70 m height and the closest 35 m, default pitch,
    /// </summary>
    public static class OzL17StopSignCheck
    {
        const string ScenePath = "Assets/OpenZ/Scenes/L17_CarPark.unity";
        const string PrefabPath = "Assets/OpenZ/Prefabs/Z_stop_sign.prefab";
        static readonly string[] Plates = { "Stop_red_face", "Stop_ivory_border", "Stop_steel_edge" };

        [MenuItem("OpenZ/V1.3/Check L17 Stop Signs (item 10)")]
        static void Menu()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ 1.3 item 10] leave Play mode first"); return; }
            Debug.Log(Run());
        }

        public static string Run()
        {
            var sb = new StringBuilder("[OpenZ 1.3 item 10] L17 stop signs\n");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = false;
            if (!scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            bool all = true;
            try
            {
                var signs = new List<GameObject>();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject) == prefab)
                            signs.Add(t.gameObject);
                int mapCount = 0;
                var mapText = Resources.Load<TextAsset>("OpenZ/Maps/L17_grid");
                if (mapText) { var s = mapText.text; int i = 0; while ((i = s.IndexOf("\"map_item:map_object13\"", i, System.StringComparison.Ordinal)) >= 0) { mapCount++; i++; } }
                bool t1 = signs.Count == mapCount && mapCount > 0;
                all &= t1;
                sb.AppendLine($"T1 signs in the scene {signs.Count}, map_object13 in the map {mapCount}: {(t1 ? "ok" : "FAIL")}");
                foreach (var sign in signs)
                {
                    var front = sign.transform.TransformDirection(Vector3.back);                          // the face side (letters at local z -0.1)
                    var flat = Vector3.ProjectOnPlane(front, Vector3.up).normalized;
                    float yawErr = Vector3.SignedAngle(flat, Vector3.back, Vector3.up);                    // the camera sits south (-z) of what it looks at
                    bool t2 = Mathf.Abs(yawErr) <= 1f;
                    var letters = Find(sign.transform, "STOP_letters");
                    var lmf = letters ? letters.GetComponent<MeshFilter>() : null;
                    float worstPlate = 0f; int buried = 0, total = 0; string missing = "";
                    if (lmf == null || lmf.sharedMesh == null) missing = " letters missing";
                    var lv = lmf != null && lmf.sharedMesh != null ? lmf.sharedMesh.vertices : new Vector3[0];
                    var lw = new Vector3[lv.Length];
                    for (int i = 0; i < lv.Length; i++) lw[i] = letters.TransformPoint(lv[i]);
                    total = lw.Length;
                    var hidden = new bool[lw.Length];
                    foreach (var pn in Plates)
                    {
                        var p = Find(sign.transform, pn); var mf = p ? p.GetComponent<MeshFilter>() : null;
                        if (mf == null || mf.sharedMesh == null) { missing += " " + pn; continue; }
                        var b = mf.sharedMesh.bounds; var sz = b.size;
                        int ax = sz.x <= sz.y && sz.x <= sz.z ? 0 : sz.y <= sz.z ? 1 : 2;              // the thin axis = the plate's normal
                        var axisLocal = ax == 0 ? Vector3.right : ax == 1 ? Vector3.up : Vector3.forward;
                        var axis = p.TransformDirection(axisLocal).normalized;
                        if (Vector3.Dot(axis, front) < 0f) axis = -axis;
                        worstPlate = Mathf.Max(worstPlate, Vector3.Angle(axis, front));
                        var c = p.TransformPoint(b.center);
                        float half = sz[ax] * 0.5f * p.lossyScale[ax];
                        int o1 = (ax + 1) % 3, o2 = (ax + 2) % 3;                                       // the plate's two in-plane axes
                        float radius = 0.5f * Mathf.Max(sz[o1] * p.lossyScale[o1], sz[o2] * p.lossyScale[o2]);
                        for (int i = 0; i < lw.Length; i++)
                        {
                            var d = lw[i] - c; float along = Vector3.Dot(d, axis);
                            float radial = (d - axis * along).magnitude;
                            if (radial < radius && along < half + 0.002f) hidden[i] = true;
                        }
                    }
                    foreach (var h in hidden) if (h) buried++;
                    bool t3 = worstPlate <= 0.5f, t4 = buried == 0 && total > 0 && missing.Length == 0;
                    all &= t2 && t3 && t4;
                    sb.AppendLine($"  {sign.name} at ({sign.transform.position.x:0.0}, {sign.transform.position.z:0.0}): T2 yaw error {yawErr:0.0} deg {(t2 ? "ok" : "FAIL")}, "
                                + $"T3 worst plate tilt {worstPlate:0.00} deg {(t3 ? "ok" : "FAIL")}, T4 buried letter vertices {buried} of {total}{missing} {(t4 ? "ok" : "FAIL")}");
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            sb.AppendLine(all ? "PASS" : "FAIL");
            return sb.ToString();
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // ------------------------------------------------------------------------------------------- game-camera shots (Play, L17)
        static readonly List<(Vector3 at, string name)> shotList = new List<(Vector3, string)>();
        static int shotStep, shotWait; static string shotDir; static float savedHeight; static Vector3 savedFocus;

        [MenuItem("OpenZ/V1.3/Stop Signs: Shots in Play (item 10)")]
        static void ShotsMenu()
        {
            if (!Application.isPlaying || !SceneManager.GetSceneByPath(ScenePath).isLoaded) { Debug.LogWarning("[OpenZ 1.3 item 10] enter Play on L17_CarPark first"); return; }
            var cam = Object.FindFirstObjectByType<RtsCamera>();
            if (!cam) { Debug.LogWarning("[OpenZ 1.3 item 10] no RtsCamera"); return; }
            shotList.Clear();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("map_object13_")) shotList.Add((t.position, t.name));
            shotDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/v1_3_item10_stop_signs"));
            Directory.CreateDirectory(shotDir);
            savedHeight = cam.height; savedFocus = cam.focus;
            shotStep = 0; shotWait = 0;
            EditorApplication.update -= ShotTick; EditorApplication.update += ShotTick;
            Debug.Log($"[OpenZ 1.3 item 10] {shotList.Count} signs, 2 shots each -> {shotDir}");
        }

        static void ShotTick()
        {
            var cam = Application.isPlaying ? Object.FindFirstObjectByType<RtsCamera>() : null;
            if (!cam || shotStep >= shotList.Count * 2)
            {
                EditorApplication.update -= ShotTick;
                if (cam) { cam.focus = savedFocus; cam.SetHeightImmediate(savedHeight); }
                Debug.Log("[OpenZ 1.3 item 10] shots done");
                return;
            }
            var (at, name) = shotList[shotStep / 2]; float h = shotStep % 2 == 0 ? 70f : 35f;
            if (shotWait == 0) { cam.focus = new Vector3(at.x, 0f, at.z); cam.SetHeightImmediate(h); }
            if (++shotWait < 30) return;                                                                   // settle (camera, fog, LOD)
            ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, $"{name}_h{h:0}.png"));
            shotWait = 0; shotStep++;
        }
    }
}
