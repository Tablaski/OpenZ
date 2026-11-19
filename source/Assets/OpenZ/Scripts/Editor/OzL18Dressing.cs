// Assets/OpenZ/Models/L18/Ruins/L18_dressing_placement.json (made off Unity by the kit's tools/plan_dressing.py from the map data):
//   * traffic lights: level 17's two models (Resources/OpenZ/DetailsV2/DV2_C01 / DV2_C02, the same prefabs, so the same lit state at night,
//     the same light cycle and break-up); on level 18 they are VIEW ONLY props (no sim map item: ZDetailPropsData has no level 18 set, so
//     DetailPropHub leaves them unbound, as the details were before DV67);
//   * the unique pieces: the level's own ruin models (Models/L18/Ruins/L18R_*.fbx, the surroundings' LOD groups and materials) and the
//     kit's harbour pieces (L18D_*.fbx, the outer world's materials); static, no collider, no map or rule change;
//   * sign swaps: of each close pair of Zod stop signs on one road, one shows the crate model (its scene object keeps the name the view
//     binds to the Zod map object, which stays: view only).
// Menus: OpenZ/L18 Bridge Game/20 Dressing (open scene) places everything under Environment/L18_Dressing (its old copy is replaced) and
// saves nothing by itself (save the scene); 21 checks the open scene against the file (counts, no collider, sign swaps), no write.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL18Dressing
    {
        const string Ruins = "Assets/OpenZ/Models/L18/Ruins", RuinMats = "Assets/OpenZ/Materials/L18", Pref = "Assets/OpenZ/Prefabs";
        const string Dv2 = "Assets/OpenZ/Resources/OpenZ/DetailsV2", File_ = Ruins + "/L18_dressing_placement.json", RootName = "L18_Dressing";

        [System.Serializable] public class Light { public string uid, prefab; public float x, y, z, yaw, scale; public int[] pole_tile; public string sign, mount; public float[] face; }   // kit traffic_lights_53: scale (0 = 1, owner addition 53); kit l18_street_r3: the street sign (its road's name; "pole_top" on level 18) and the way the lights face (x, z)
        [System.Serializable] public class Piece
        {
            public string id, piece, @base, what; public float x, y, z, unity_yaw_deg, scale, tilt_deg, tilt_heading_deg, sink_extra_m;
            /// <summary>
            /// on (x, z) and its foot put on the ground there; destructible = a view-only DetailProp (as level 18's traffic lights: no sim map item,
            /// so nothing a unit could bump into and no rule change): explosives break it (hp, hit cylinder radius / height in metres), the
            /// ruin model (an L18R_ piece, or empty = none) shows in its place, sunk / rolled / turned by these values.</summary>
            public bool removed, centre_on_bounds, destructible; public string ruin; public float hp, hit_r, hit_h, ruin_yaw_deg, ruin_roll_deg, ruin_sink_m;
        }
        [System.Serializable] public class Swap { public string name, keep, with_key; public float distance_tiles; }
        [System.Serializable] public class Doc { public Light[] traffic_lights; public Piece[] pieces; public Swap[] sign_swaps; }

        public static Doc Read() => File.Exists(File_) ? JsonUtility.FromJson<Doc>(File.ReadAllText(File_)) : null;

        [MenuItem("OpenZ/L18 Bridge Game/20 Dressing: traffic lights, harbour pieces, sign swaps (open scene)", false, 7720)]
        static void PlaceMenu() => Debug.Log("[OpenZ] L18 dressing: " + Place());

        [MenuItem("OpenZ/L18 Bridge Game/21 Dressing: check the open scene (no write)", false, 7721)]
        static void CheckMenu() => Debug.Log("[OpenZ] L18 dressing check: " + Check());

        static bool L18Open(out string why)
        {
            var s = EditorSceneManager.GetActiveScene();
            why = s.name;
            return !Application.isPlaying && s.name.StartsWith(OzCity.BridgeGameScene);
        }

        /// <summary>Ground height under (x, z): the highest hit on the Environment's own colliders (the level's COL_Terrain walk
        /// collider), the water sheet left out; NaN when nothing is under it.</summary>
        static float Ground(GameObject env, float x, float z)
        {
            float y = float.NaN;
            foreach (var h in Physics.RaycastAll(new Ray(new Vector3(x, 200f, z), Vector3.down), 400f))
            {
                if (!env || !h.collider.transform.IsChildOf(env.transform) || h.collider.isTrigger) continue;
                if (h.collider.name.StartsWith("WAT_")) continue;
                if (float.IsNaN(y) || h.point.y > y) y = h.point.y;
            }
            return y;
        }

        static Material RuinMat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{RuinMats}/{name}.mat");

        /// <summary>One L18R ruin piece as OzL18Assets (menu 4) builds it: three meshes behind one LOD group (12 % / 3.5 % / 0.4 %), its
        /// own M_L18R_* materials (made by menu 4, read here), static, no collider.</summary>
        static GameObject RuinPiece(string piece, Transform parent, ref int tris, StringBuilder sb)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{Ruins}/{piece}.fbx");
            if (!fbx) { sb.Append(piece + " model missing; "); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
            var all = go.GetComponentsInChildren<MeshRenderer>(true);
            MeshRenderer A = all.FirstOrDefault(r => r.name == piece), B = all.FirstOrDefault(r => r.name == piece + "_LOD1"), C = all.FirstOrDefault(r => r.name == piece + "_LOD2");
            if (!A || !C) { Object.DestroyImmediate(go); sb.Append(piece + " LOD meshes missing; "); return null; }
            foreach (var r in all)
            {
                string mn = r == C ? $"M_{piece}_LOD2" : r == B && piece == "L18R_FactoryHall" ? "M_L18R_FactoryHall_LOD1" : $"M_{piece}";
                var m = RuinMat(mn);
                if (!m) { sb.Append(mn + " missing (run menu 4 first); "); continue; }
                r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = r == C ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true; r.gameObject.isStatic = true;
            }
            var lg = go.AddComponent<LODGroup>(); lg.fadeMode = LODFadeMode.None;
            lg.SetLODs(B ? new[] { new LOD(0.12f, new Renderer[] { A }), new LOD(0.035f, new Renderer[] { B }), new LOD(0.004f, new Renderer[] { C }) }
                         : new[] { new LOD(0.12f, new Renderer[] { A }), new LOD(0.004f, new Renderer[] { C }) });
            lg.RecalculateBounds();
            var mf = A.GetComponent<MeshFilter>(); if (mf && mf.sharedMesh) tris += mf.sharedMesh.triangles.Length / 3;
            go.isStatic = true;
            return go;
        }

        /// <summary>One L18D harbour piece of the kit: its FBX as imported (the .meta maps its materials to the outer world's), static,
        /// shadows on, no collider.</summary>
        static GameObject HarbourPiece(string piece, Transform parent, ref int tris, StringBuilder sb)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{Ruins}/{piece}.fbx");
            if (!fbx) { sb.Append(piece + " model missing; "); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; r.gameObject.isStatic = true;
                var mf = r.GetComponent<MeshFilter>(); if (mf && mf.sharedMesh) tris += mf.sharedMesh.triangles.Length / 3;
            }
            go.isStatic = true;
            return go;
        }

        /// <summary>kit l18_street_r3 (R3.33): only the traffic lights again from the file (the old ones under TrafficLights replaced), each with its
        /// street sign; the pieces and the sign swaps stay as they are. Saves nothing by itself (OzStreetR3's menu saves the scene).</summary>
        public static string PlaceLights()
        {
            if (!L18Open(out var sn)) return "open the level 18 scene first, out of Play (" + sn + ")";
            var doc = Read(); if (doc == null) return "missing " + File_;
            var env = GameObject.Find("Environment");
            var root = env ? env.transform.Find(RootName) : null;
            if (!root) return "no Environment/" + RootName + " in the open scene (run menu 20 first)";
            var tlRoot = root.Find("TrafficLights");
            if (!tlRoot) { tlRoot = new GameObject("TrafficLights").transform; tlRoot.SetParent(root, false); }
            for (int i = tlRoot.childCount - 1; i >= 0; i--) Object.DestroyImmediate(tlRoot.GetChild(i).gameObject);
            Physics.SyncTransforms();
            var sb = new StringBuilder(); int nl = 0, noGround = 0, signs = 0;
            foreach (var l in doc.traffic_lights ?? new Light[0])
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dv2}/{l.prefab}.prefab");
                if (!pf) { sb.Append(l.prefab + " prefab missing; "); continue; }
                float y = Ground(env, l.x, l.z); if (float.IsNaN(y)) { y = l.y; noGround++; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, tlRoot);
                go.name = l.uid;
                go.transform.SetPositionAndRotation(new Vector3(l.x, y, l.z), Quaternion.Euler(0, l.yaw, 0));
                if (OzStreetR3.Apply(go, l.sign, l.mount, sb)) signs++;
                go.transform.localScale = Vector3.one * (l.scale > 0f ? l.scale : 1f);
                nl++;
            }
            int removed = 0;
            foreach (var c in tlRoot.GetComponentsInChildren<Collider>(true)) { Object.DestroyImmediate(c); removed++; }   // view only: never a physics body
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return $"{nl} of {(doc.traffic_lights ?? new Light[0]).Length} traffic lights placed ({signs} street signs, {removed} colliders removed, {noGround} without ground under them). " + sb;
        }

        public static string Place()
        {
            if (!L18Open(out var sn)) return "open the level 18 scene first, out of Play (" + sn + ")";
            var doc = Read(); if (doc == null) return "missing " + File_;
            var scene = EditorSceneManager.GetActiveScene();
            var env = GameObject.Find("Environment");
            if (!env) return "no Environment object in the open scene";
            var old = env.transform.Find(RootName); if (old) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(RootName).transform; root.SetParent(env.transform, false);
            var tlRoot = new GameObject("TrafficLights").transform; tlRoot.SetParent(root, false);
            var pcRoot = new GameObject("Pieces").transform; pcRoot.SetParent(root, false);
            Physics.SyncTransforms();
            var sb = new StringBuilder(); int nl = 0, np = 0, tris = 0, noGround = 0;
            foreach (var l in doc.traffic_lights ?? new Light[0])
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dv2}/{l.prefab}.prefab");
                if (!pf) { sb.Append(l.prefab + " prefab missing; "); continue; }
                float y = Ground(env, l.x, l.z); if (float.IsNaN(y)) { y = l.y; noGround++; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, tlRoot);
                go.name = l.uid;                                                                       // never a level 17 uid: nothing in the sim binds to it
                go.transform.SetPositionAndRotation(new Vector3(l.x, y, l.z), Quaternion.Euler(0, l.yaw, 0));
                OzStreetR3.Apply(go, l.sign, l.mount, sb);                                   // kit l18_street_r3 (R3.33 / R3.35): the light's street sign
                go.transform.localScale = Vector3.one * (l.scale > 0f ? l.scale : 1f);
                nl++;
            }
            foreach (var it in doc.pieces ?? new Piece[0])
            {
                if (it.removed) continue;                                                              // kit l18_pieces_r6: owner addition 55 (part-maker pieces), kept in the file as history
                float y = it.y;
                if (it.@base == "ground") { float g = Ground(env, it.x, it.z); if (float.IsNaN(g)) noGround++; else y = g; }
                var host = new GameObject($"{it.id}_{it.piece}").transform; host.SetParent(pcRoot, false);
                var d = new Vector3(Mathf.Cos(it.tilt_heading_deg * Mathf.Deg2Rad), 0, Mathf.Sin(it.tilt_heading_deg * Mathf.Deg2Rad));
                var tilt = Mathf.Abs(it.tilt_deg) > 0.01f ? Quaternion.AngleAxis(it.tilt_deg, Vector3.Cross(Vector3.up, d)) : Quaternion.identity;
                host.SetPositionAndRotation(new Vector3(it.x, y - it.sink_extra_m, it.z), tilt * Quaternion.Euler(0, it.unity_yaw_deg, 0));
                host.localScale = Vector3.one * (it.scale > 0 ? it.scale : 1f);
                var p = it.piece.StartsWith("L18R_") ? RuinPiece(it.piece, host, ref tris, sb) : HarbourPiece(it.piece, host, ref tris, sb);
                if (!p) { Object.DestroyImmediate(host.gameObject); continue; }
                p.transform.localPosition = Vector3.zero; p.transform.localRotation = Quaternion.identity; p.transform.localScale = Vector3.one;
                if (it.centre_on_bounds)
                {   // kit l18_pieces_r6: the model's own box (not its pivot) stands on the planned blocked tiles; its foot on the ground there
                    var b = Bounds(p); host.position += new Vector3(it.x - b.center.x, 0f, it.z - b.center.z);
                    float g = Ground(env, it.x, it.z); if (!float.IsNaN(g)) { b = Bounds(p); host.position += new Vector3(0f, g - it.sink_extra_m - b.min.y, 0f); }
                }
                if (it.destructible) Destructible(it, host, ref tris, sb);
                host.gameObject.isStatic = true; np++;
            }
            int removed = 0;
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) { Object.DestroyImmediate(c); removed++; }   // view only: never a physics body
            // sign swaps: the scene object keeps its name (the view binds the Zod map object by "<type>_<x>_<y>"), its model becomes the crate
            int sw = 0;
            foreach (var s in doc.sign_swaps ?? new Swap[0])
            {
                var go = FindByName(scene, s.name);
                if (!go) { sb.Append("sign " + s.name + " not in the scene; "); continue; }
                if (go.transform.Find("L18_SignSwap") != null) { sw++; continue; }                                   // swapped before
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Pref}/Z_{s.with_key}.prefab");
                if (!pf) { sb.Append("Z_" + s.with_key + " missing; "); continue; }
                var parent = go.transform.parent; var pos = go.transform.position; int sib = go.transform.GetSiblingIndex();
                var repl = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
                repl.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, 0, 0));
                var b0 = Bounds(repl); float fit = 1.9f / Mathf.Max(0.01f, Mathf.Max(b0.size.x, b0.size.z));   // OpenZSetup: small map items fit their tile (1.9 m)
                if (fit < 1f) repl.transform.localScale = repl.transform.localScale * fit;
                var b1 = Bounds(repl); var p2 = repl.transform.position; p2.y += pos.y - b1.min.y; repl.transform.position = p2;   // foot on the old sign's ground
                var mark = new GameObject("L18_SignSwap"); mark.transform.SetParent(repl.transform, false);
                Object.DestroyImmediate(go);
                repl.name = s.name; repl.transform.SetSiblingIndex(sib);
                sw++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return $"{nl} of {(doc.traffic_lights ?? new Light[0]).Length} traffic lights, {np} of {(doc.pieces ?? new Piece[0]).Length} pieces ({tris} triangles at the nearest level), " +
                   $"{sw} of {(doc.sign_swaps ?? new Swap[0]).Length} sign swaps, {removed} colliders removed, {noGround} without ground under them (data height used). " +
                   "SAVE THE SCENE. " + sb;
        }

        static GameObject FindByName(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var r in scene.GetRootGameObjects())
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }

        /// <summary>kit l18_pieces_r6: the piece becomes a view-only destructible prop (OpenZ.DetailProp, the traffic lights' own class):
        /// rounds only knock it (shudder, ricochet, bits), explosives within reach take its hit points; at 0 the standing model goes, rust and
        /// steel bits fly, dust and a scorch, the heavy metal break sound, and its RUIN_ model (hidden until then) lies in its place.</summary>
        static void Destructible(Piece it, Transform host, ref int tris, StringBuilder sb)
        {
            float s = Mathf.Max(0.01f, host.lossyScale.x);
            if (!string.IsNullOrEmpty(it.ruin))
            {
                var r = it.ruin.StartsWith("L18R_") ? RuinPiece(it.ruin, host, ref tris, sb) : null;
                if (r)
                {
                    r.name = "RUIN_" + it.ruin;
                    r.transform.localPosition = new Vector3(0f, -it.ruin_sink_m / s, 0f);
                    r.transform.localRotation = Quaternion.Euler(it.ruin_roll_deg, it.ruin_yaw_deg, 0f);
                    r.transform.localScale = Vector3.one;
                    r.SetActive(false);                                                                  // DetailProp shows it at the break
                }
            }
            var dp = host.GetComponent<OpenZ.DetailProp>() ?? host.gameObject.AddComponent<OpenZ.DetailProp>();
            dp.id = "l18_" + it.id.ToLowerInvariant(); dp.kind = it.piece.Contains("Crane") ? "crane" : "pipes";
            dp.health = it.hp > 0f ? it.hp : 6f; dp.blastOnly = true;
            dp.hitRadius = (it.hit_r > 0f ? it.hit_r : 2f) / s; dp.hitHeight = (it.hit_h > 0f ? it.hit_h : 4f) / Mathf.Max(0.01f, host.lossyScale.y);   // DetailProp.Start multiplies by the scale again
            dp.mat = "metal"; dp.debrisCount = 28; dp.scorch = 1.2f; dp.knockWobble = 0.12f; dp.glowAtNight = false;
            dp.debris = new[] { new Color(0.46f, 0.26f, 0.15f), new Color(0.27f, 0.27f, 0.29f), new Color(0.62f, 0.48f, 0.22f) };   // rust, steel, faded crane paint
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        public static string Check()
        {
            if (!L18Open(out var sn)) return "open the level 18 scene first, out of Play (" + sn + ")";
            var doc = Read(); if (doc == null) return "FAIL: missing " + File_;
            var scene = EditorSceneManager.GetActiveScene();
            var env = GameObject.Find("Environment"); var root = env ? env.transform.Find(RootName) : null;
            var rows = new List<string>(); int pass = 0, n = 0;
            void Row(bool ok, string what) { n++; if (ok) pass++; rows.Add((ok ? "PASS " : "FAIL ") + what); }
            Row(root, "Environment/" + RootName + " is in the scene");
            int lights = root ? root.Find("TrafficLights")?.childCount ?? 0 : 0, pieces = root ? root.Find("Pieces")?.childCount ?? 0 : 0;
            Row(lights == (doc.traffic_lights?.Length ?? 0), $"traffic lights {lights} of {doc.traffic_lights?.Length ?? 0}");
            int want = (doc.pieces ?? new Piece[0]).Count(p => !p.removed);
            Row(pieces == want, $"pieces {pieces} of {want} (removed in the file: {(doc.pieces ?? new Piece[0]).Count(p => p.removed)})");
            int dps = root ? root.Find("Pieces")?.GetComponentsInChildren<OpenZ.DetailProp>(true).Length ?? 0 : 0;
            int wantDp = (doc.pieces ?? new Piece[0]).Count(p => !p.removed && p.destructible);
            Row(dps == wantDp, $"destructible pieces (view-only DetailProp, l18_pieces_r6) {dps} of {wantDp}");
            int cols = root ? root.GetComponentsInChildren<Collider>(true).Length : -1;
            Row(cols == 0, $"colliders under the dressing: {cols}");
            int lit = root ? root.GetComponentsInChildren<OpenZ.TrafficLightCycle>(true).Length : 0;
            Row(lit == lights, $"light cycles on the traffic lights: {lit}");
            int sw = 0, signsLeft = 0;
            foreach (var s in doc.sign_swaps ?? new Swap[0])
            {
                var go = FindByName(scene, s.name);
                if (go && go.transform.Find("L18_SignSwap")) sw++;
                if (FindByName(scene, s.keep)) signsLeft++;
            }
            Row(sw == (doc.sign_swaps?.Length ?? 0), $"sign swaps done {sw} of {doc.sign_swaps?.Length ?? 0} (crate model under the Zod object's name)");
            Row(signsLeft == (doc.sign_swaps?.Length ?? 0), $"the kept sign of each pair still there: {signsLeft}");
            int missingMesh = 0;
            if (root) foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) if (!mf.sharedMesh) missingMesh++;
            Row(missingMesh == 0, $"meshes missing: {missingMesh}");
            int missingMat = 0;
            if (root) foreach (var r in root.GetComponentsInChildren<Renderer>(true)) foreach (var m in r.sharedMaterials) if (!m) missingMat++;
            Row(missingMat == 0, $"material slots empty: {missingMat}");
            var night = Resources.Load<TextAsset>("OpenZ/Night/L18_night");
            int posts = 0; if (night) posts = night.text.Split(new[] { "\"id\"" }, System.StringSplitOptions.None).Length - 1;
            rows.Add($"INFO lamps: the night posts file holds {posts} new posts (cloned in Play from the level's street lamp) + the environment's DEC lamps");
            return $"{(pass == n ? "PASS" : "FAIL")} {pass}/{n}: " + string.Join(" | ", rows);
        }
    }
}
