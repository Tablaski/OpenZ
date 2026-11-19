//   * traffic lights: only at the main crossings, each on the pavement corner right BEFORE its crossing, the arm over the lane driving INTO it,
//     the lights facing that traffic (level 18: the four bridge-head junctions, placed from Models/L18/Ruins/L18_dressing_placement.json, the
//     owner's arrow spot for BOLT AVE; level 17: its two lights were already right, they stay); the plan and its rule check are made off Unity
//     by the kit's tools/plan_street.py from the map data;
//   * street signs: each road its own short joke name; a light's sign names the road whose lane its arm reaches over. Level 17's signs stay on
//     the mast arm (they face the camera there); level 18's arms run north-south, so its sign sits on the pole top (L18S_StreetSign), parallel
//     to the road it names, facing the camera, and the arm's own blade is switched off. Each name is one copy of level 17's details atlas
//     (Models/DetailsV2/dv2_atlas_L17_<NAME>.png, material Materials/DetailsV2/M_DV2_Paint_L17_<NAME>.mat): the same model, look and night
//     glow, only the name differs. The sign is a LOOSE_ part of the light (DetailProp throws it when the light breaks);
//   * R3.34 level 18's dark untextured boxes = the 12 Zod map objects 14 (OpenZSetup: city map object 14 -> Z_city_block, two flat dark Unity
//     cubes; the rocks that share the prefab were rebuilt long ago, these 12 never were): each gets the finished textured concrete block
//     (L18S_ConcreteBlock, Z's own city block: weathered grey concrete, chipped corner, lifting loops); the Zod object stays (name, place, turn,
//     size, sim binding): only its two cubes go. Level 17's own 22 map objects 14 are NOT touched (not in this kit's rows; same fix on request).
//   * kit l18_placeholder_boxes_r6 (14 Nov, FAILS A17: the owner on exe 14b, 'these two placeholder nonsense boses on l18'): that textured
//     block read as a pale box with a face; the 12 are sandbag positions now (Editor/OzL18SandbagNests.cs). Blocks() builds those and the
//     check counts them, so menu 1 never brings the block back. Level 17's 22 stay as they are.
// View only: no sim, map or rule change; no collider. Menus OpenZ/Street furniture r3/1 (open level 18 scene) and /2 (open level 17 scene)
// write the scene and save it; /3 checks the open scene (no write).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzStreetR3
    {
        const string Models = "Assets/OpenZ/Models/DetailsV2", Mats = "Assets/OpenZ/Materials/DetailsV2";
        const string SignFbx = Models + "/L18S_StreetSign.fbx", BlockFbx = Models + "/L18S_ConcreteBlock.fbx", BlockMat = Mats + "/M_L18S_ConcreteBlock.mat";
        public const string SignName = "LOOSE_StreetSign", BlockName = "L18S_ConcreteBlock";
        /// <summary>The DV2 traffic light's pole cap (prefab metres): the pole-top sign's collar sits on it.</summary>
        public const float PoleTop = 6.30f;

        public static string Key(string sign) => (sign ?? "").Trim().Replace(' ', '_');
        public static Material SignMaterial(string sign) => AssetDatabase.LoadAssetAtPath<Material>($"{Mats}/M_DV2_Paint_L17_{Key(sign)}.mat");

        static Transform FindPrefix(Transform root, string prefix) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => OpenZ.DetailKit.Base(t.name).StartsWith(prefix));

        /// <summary>Gives one placed DV2 traffic light its street sign. mount "pole_top": the arm's blade off, the L18S sign on the pole cap facing
        /// the camera (world -z); else ("arm", level 17) the arm's blade wears the name's material. Idempotent. False + a note when it cannot.</summary>
        public static bool Apply(GameObject light, string sign, string mount, StringBuilder sb)
        {
            if (!light || string.IsNullOrEmpty(sign)) return false;
            var mat = SignMaterial(sign);
            if (!mat) { sb?.Append($"{light.name}: material M_DV2_Paint_L17_{Key(sign)} missing; "); return false; }
            var blade = FindPrefix(light.transform, "LOOSE_Blade_");
            var old = light.transform.Find(SignName); if (old) Object.DestroyImmediate(old.gameObject);
            if (mount == "pole_top")
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(SignFbx);
                if (!fbx) { sb?.Append(SignFbx + " missing; "); return false; }
                if (blade) blade.gameObject.SetActive(false);                                       // the arm's own blade would name the road edge-on
                var s = (GameObject)PrefabUtility.InstantiatePrefab(fbx, light.transform);
                PrefabUtility.UnpackPrefabInstance(s, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                s.name = SignName;                                                                 // DetailProp: a LOOSE_ part flies when the light breaks
                s.transform.localPosition = new Vector3(0f, PoleTop, 0f); s.transform.localRotation = Quaternion.identity; s.transform.localScale = Vector3.one;
                var fr = FindPrefix(s.transform, "DIR_SignFront");
                if (fr)
                {   // turn about the vertical until the blade's face looks at the camera (south, world -z): parallel to the east-west road it names
                    var d = fr.position - s.transform.position; d.y = 0f;
                    if (d.sqrMagnitude > 1e-6f) s.transform.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(d.normalized, Vector3.back, Vector3.up), Vector3.up) * s.transform.rotation;
                }
                foreach (var r in s.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true;
                }
                foreach (var c in s.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            }
            else
            {
                if (!blade) { sb?.Append(light.name + ": no LOOSE_Blade; "); return false; }
                blade.gameObject.SetActive(true);
                var r = blade.GetComponent<MeshRenderer>();
                if (r) r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
            }
            return true;
        }

        // ------------------------------------------------------------------ R3.34: level 18's Zod map objects 14
        // kit l18_placeholder_boxes_r6: the textured concrete block (L18S_ConcreteBlock) read as a placeholder box; the 12 are sandbag positions
        // now (Editor/OzL18SandbagNests.cs: SandbagsV2 kit, level 17's material, Intact / Damaged / Rubble, View/OzSandbagNest.cs breaks them).
        public static string Blocks() => OzL18SandbagNests.Build(false);

        static string TopName(Transform root, Transform t) { while (t.parent && t.parent != root) t = t.parent; return t.name; }   // exe C 13 Nov

        // ------------------------------------------------------------------ menus
        static bool Open(string level, out string why)
        {
            var s = EditorSceneManager.GetActiveScene(); why = s.name;
            return !Application.isPlaying && OzCity.LevelOfScene(s.name) == level;
        }

        [MenuItem("OpenZ/Street furniture r3/1 Level 18: traffic lights + street signs + concrete blocks (open L18 scene, saves it)", false, 7790)]
        public static string Level18()
        {
            if (!Open(OzCity.BridgeGame, out var sn)) { Debug.LogError("[OpenZ] street r3: open the level 18 scene first, out of Play (" + sn + ")"); return "not level 18"; }
            string a = OzL18Dressing.PlaceLights(), b = Blocks();
            var scene = EditorSceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            var msg = "[OpenZ] street r3 level 18: " + a + " | " + b + " | scene saved. Then menu 3 (check).";
            Debug.Log(msg); return msg;
        }

        [MenuItem("OpenZ/Street furniture r3/2 Level 17: street signs on the traffic lights (open L17 scene, saves it)", false, 7791)]
        public static string Level17()
        {
            if (!Open(OzCity.CarPark, out var sn)) { Debug.LogError("[OpenZ] street r3: open the level 17 scene first, out of Play (" + sn + ")"); return "not level 17"; }
            var pf = JsonUtility.FromJson<OpenZDetailsV2.PFile>(File.ReadAllText(OpenZDetailsV2.PrefabDir + "/placement_L17.json"));
            var root = GameObject.Find("DetailsV2_L17");
            if (!root) return "no DetailsV2_L17 in the open scene";
            var sb = new StringBuilder(); int n = 0, want = 0;
            foreach (var it in pf.items ?? new OpenZDetailsV2.PItem[0])
            {
                if (it == null || string.IsNullOrEmpty(it.sign)) continue;
                want++;
                var go = root.transform.Cast<Transform>().FirstOrDefault(t => t.name == (string.IsNullOrEmpty(it.uid) ? it.id : it.uid));
                if (!go) { sb.Append(it.uid + " not in the scene; "); continue; }
                if (Apply(go.gameObject, it.sign, it.mount, sb)) n++;
            }
            var scene = EditorSceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            var msg = $"[OpenZ] street r3 level 17: {n} of {want} traffic lights wear their road's name, scene saved. {sb}Then menu 3 (check).";
            Debug.Log(msg); return msg;
        }

        [MenuItem("OpenZ/Street furniture r3/3 Check the open level 17 or 18 scene (no write)", false, 7792)]
        public static string CheckMenu() { var s = Check(); if (s.StartsWith("PASS")) Debug.Log("[OpenZ] street r3 check: " + s); else Debug.LogError("[OpenZ] street r3 check: " + s); return s; }

        [System.Serializable] class Grid { public int width, height; public int[] classes; }

        public static string Check()
        {
            var scene = EditorSceneManager.GetActiveScene();
            string lv = OzCity.LevelOfScene(scene.name);
            if (!OzCity.Is(lv) || Application.isPlaying) return "FAIL: open the level 17 or 18 scene, out of Play (" + scene.name + ")";
            var g = JsonUtility.FromJson<Grid>(Resources.Load<TextAsset>($"OpenZ/Maps/{lv}_grid").text);
            int Cls(Vector3 p) { int tx = Mathf.FloorToInt(p.x / 2f), ty = Mathf.FloorToInt(-p.z / 2f); return tx < 0 || ty < 0 || tx >= g.width || ty >= g.height ? 3 : g.classes[ty * g.width + tx]; }
            var rows = new List<string>(); int pass = 0, n = 0;
            void Row(bool ok, string what) { n++; if (ok) pass++; rows.Add((ok ? "PASS " : "FAIL ") + what); }
            var lights = new List<(GameObject go, string sign, string mount, float[] face)>();
            if (lv == OzCity.BridgeGame)
            {
                var doc = OzL18Dressing.Read();
                var tl = GameObject.Find("Environment")?.transform.Find("L18_Dressing/TrafficLights");
                Row(tl && doc != null && tl.childCount == (doc.traffic_lights?.Length ?? -1), $"traffic lights in the scene {(tl ? tl.childCount : 0)} of {doc?.traffic_lights?.Length ?? 0} in the file");
                foreach (var l in doc?.traffic_lights ?? new OzL18Dressing.Light[0])
                {
                    var t = tl ? tl.Find(l.uid) : null;
                    Row(t, l.uid + " in the scene");
                    if (t) lights.Add((t.gameObject, l.sign, l.mount, l.face));
                }
            }
            else
            {
                var pf = JsonUtility.FromJson<OpenZDetailsV2.PFile>(File.ReadAllText(OpenZDetailsV2.PrefabDir + "/placement_L17.json"));
                var root = GameObject.Find("DetailsV2_L17");
                foreach (var it in pf.items ?? new OpenZDetailsV2.PItem[0])
                {
                    if (it == null || string.IsNullOrEmpty(it.sign)) continue;
                    var t = root ? root.transform.Cast<Transform>().FirstOrDefault(x => x.name == it.uid) : null;
                    Row(t, it.uid + " in the scene");
                    if (t) lights.Add((t.gameObject, it.sign, it.mount, null));
                }
                Row(lights.Count == 2, $"level 17 lights with a street name: {lights.Count} (2)");
            }
            var names = new HashSet<string>();
            foreach (var (go, sign, mount, face) in lights)
            {
                var foot = go.transform.position;
                Row(Cls(foot) == 0, $"{go.name}: pole on the pavement (tile class {Cls(foot)})");
                var heads = go.GetComponentsInChildren<Transform>(true).Where(t => OpenZ.DetailKit.Base(t.name).StartsWith("LOOSE_Head")).ToList();
                Row(heads.Count == 2 && heads.All(h => Cls(h.position) == 1), $"{go.name}: both heads over road tiles ({string.Join(", ", heads.Select(h => Cls(h.position)))})");
                var f = go.transform.rotation * Vector3.back;                                           // DV2: the heads look along the prefab's -z
                if (face != null && face.Length == 2) Row(Vector3.Dot(f, new Vector3(face[0], 0f, face[1])) > 0.99f, $"{go.name}: lights face the incoming traffic ({f.x:0.##}, {f.z:0.##})");
                var mat = SignMaterial(sign);
                var blade = FindPrefix(go.transform, "LOOSE_Blade_");
                if (mount == "pole_top")
                {
                    var s = go.transform.Find(SignName);
                    var r = s ? s.GetComponentInChildren<MeshRenderer>(true) : null;
                    var fr = s ? FindPrefix(s, "DIR_SignFront") : null;
                    Row(s && r && mat && r.sharedMaterial == mat, $"{go.name}: pole-top sign '{sign}' ({(r && r.sharedMaterial ? r.sharedMaterial.name : "none")})");
                    Row(fr && Vector3.Dot(Vector3.ProjectOnPlane(fr.position - s.position, Vector3.up).normalized, Vector3.back) > 0.95f, $"{go.name}: the sign faces the camera (south)");   // exe C 13 Nov: flat direction (the front marker sits 0.8 m up)
                    Row(s && Mathf.Abs(s.position.y - (foot.y + PoleTop * go.transform.lossyScale.y)) < 0.05f && new Vector2(s.position.x - foot.x, s.position.z - foot.z).magnitude < 0.05f, $"{go.name}: the sign sits on the pole cap");
                    Row(blade && !blade.gameObject.activeSelf, $"{go.name}: the arm's own blade is off");
                }
                else
                {
                    var r = blade ? blade.GetComponent<MeshRenderer>() : null;
                    Row(r && mat && r.sharedMaterial == mat && blade.gameObject.activeSelf, $"{go.name}: arm blade '{sign}' ({(r && r.sharedMaterial ? r.sharedMaterial.name : "none")})");
                }
                Row(go.GetComponentsInChildren<Collider>(true).Length == 0 || lv == OzCity.CarPark, $"{go.name}: no collider (view only)");
                names.Add(sign);
            }
            rows.Add("INFO street names on the lights: " + string.Join(", ", names));
            if (lv == OzCity.BridgeGame)
            {
                var zo = GameObject.Find("ZodObjects");
                var blocks = zo ? zo.transform.Cast<Transform>().Where(t => t.name.StartsWith("map_object14_")).ToList() : new List<Transform>();
                // kit l18_placeholder_boxes_r6: the 12 are sandbag positions (OzL18SandbagNests; its menu 2 checks them in full)
                int done = blocks.Count(t => t.Find(OzL18SandbagNests.NestName)), old = blocks.Count(t => t.Find(BlockName)), cubes = blocks.Sum(t => t.GetComponentsInChildren<MeshFilter>(true).Count(m => m.sharedMesh && m.sharedMesh.name == "Cube"));
                var sm = AssetDatabase.LoadAssetAtPath<Material>(OzL18SandbagNests.MatPath);
                int wrong = blocks.Sum(t => t.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.sharedMaterial != sm));
                Row(blocks.Count == 12 && done == 12, $"Zod map objects 14 drawn as sandbag positions (kit l18_placeholder_boxes_r6): {done} of {blocks.Count} (12)");
                Row(old == 0, $"old concrete blocks (L18S_ConcreteBlock) left on them: {old}");
                Row(cubes == 0, $"flat dark cubes left on them: {cubes}");
                Row(wrong == 0, $"renderers on them not wearing M_SandbagsV2_L17: {wrong}");
                int other = 0;
                if (zo) foreach (var mf in zo.GetComponentsInChildren<MeshFilter>(false))
                        if (mf.sharedMesh && mf.sharedMesh.name == "Cube" && mf.GetComponent<MeshRenderer>() && mf.GetComponent<MeshRenderer>().enabled && TopName(zo.transform, mf.transform).StartsWith("map_object")) other++;   // exe C 13 Nov: map objects only (flag banners and the bridge underlay are cube meshes by design)
                Row(other == 0, $"visible flat Unity cubes left among all the Zod map objects (placeholder boxes): {other}");
            }
            return $"{(pass == n ? "PASS" : "FAIL")} {pass}/{n}: " + string.Join(" | ", rows);
        }
    }
}
