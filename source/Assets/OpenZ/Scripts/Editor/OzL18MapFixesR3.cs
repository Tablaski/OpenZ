// kit l18_map_fixes_r3 (OpenZ 1.5, level 18, 13 Nov): the scene step and the check of the level 18 map fixes.
//   menu OpenZ/L18 Bridge Game/31 Map fixes r3: turn the Bizon (open scene, saves)
//        the scene's ZodObjects/crane_83_110 (the model the EMPTY Bizon is drawn with at the start: DV198 makes the map's right bottom crane
//        entry a Bizon, ZGame.BindViews finds this object by OzL18Rules.SceneAlias) turned to 270 degrees: the Bizon lies ALONG the east-west road
//        (the sim places it on tile 78, 110, the owner's red box on the bottom road in front of the robot factory, and faces it east:
//        ZWorld.L18BigTankMoves; the view takes its place from
//        the sim and its turn from this object at bind). Its position stays on the map entry's tiles (menu 13's check of the scene).
//   menu OpenZ/L18 Bridge Game/32 Map fixes r3: check (open scene, no write)
//        PASS / FAIL lines: the turn, the two moved street lamps of the environment model (2 m clear of containers S2 / N2), the containers'
//        hosts, the new ground textures' sizes, the closed container / ship meshes (two-sided: triangle counts), the Bizon's sim tile.
// Editor only, view only: nothing here runs in the game.
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL18MapFixesR3
    {
        public const float BizonTurn = 270f;
        static readonly (string name, float x, float z)[] MovedLamps =
        {
            ("DEC_round13_warm_street_lamp_081", 189.0f, -89.0f),    // was (188, -89): 1.45 m from container N2's box, now 2.4 m
            ("DEC_round13_warm_street_lamp_109", 59.0f, -205.0f),    // was (55, -205) on the canal bed INSIDE container S2, now on the quay, 2.6 m clear
        };

        static bool L18Open(out UnityEngine.SceneManagement.Scene scene)
        {
            scene = EditorSceneManager.GetActiveScene();
            return !Application.isPlaying && scene.name.StartsWith(OzCity.BridgeGame);
        }

        static Transform Find(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var r in scene.GetRootGameObjects())
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            return null;
        }

        [MenuItem("OpenZ/L18 Bridge Game/31 Map fixes r3: turn the Bizon (open scene, saves)", false, 7731)]
        static void TurnMenu() => Debug.Log("[OpenZ] L18 map fixes r3, the Bizon's turn:\n" + Turn());

        public static string Turn()
        {
            if (!L18Open(out var scene)) return "FAIL open the level 18 scene first, out of Play (" + scene.name + ")";
            var root = GameObject.Find("ZodObjects");
            var cr = root ? root.transform.Find("crane_83_110") : null;
            if (!cr) return "FAIL no ZodObjects/crane_83_110 in the open scene";
            float before = cr.eulerAngles.y;
            Undo.RecordObject(cr, "L18 Bizon turn");
            cr.rotation = Quaternion.Euler(0f, BizonTurn, 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            return $"PASS crane_83_110 turn {before:0} -> {cr.eulerAngles.y:0} at ({cr.position.x:0.00}, {cr.position.z:0.00}); scene saved {saved}";
        }

        [MenuItem("OpenZ/L18 Bridge Game/32 Map fixes r3: check (open scene, no write)", false, 7732)]
        static void CheckMenu() => Debug.Log("[OpenZ] L18 map fixes r3, check:\n" + Check());

        public static string Check()
        {
            if (!L18Open(out var scene)) return "FAIL open the level 18 scene first, out of Play (" + scene.name + ")";
            var sb = new StringBuilder(); int pass = 0, fail = 0;
            void Row(bool ok, string s) { if (ok) pass++; else fail++; sb.AppendLine($"  {(ok ? "PASS" : "FAIL")} {s}"); }
            var root = GameObject.Find("ZodObjects");
            var cr = root ? root.transform.Find("crane_83_110") : null;
            Row(cr && Mathf.Abs(Mathf.DeltaAngle(cr.eulerAngles.y, BizonTurn)) < 1f,
                cr ? $"R3.32 crane_83_110 (the Bizon's scene model) turn {cr.eulerAngles.y:0} (want {BizonTurn:0}: along the east-west road)" : "R3.32 no ZodObjects/crane_83_110");
            var mv = ZWorld.L18BigTankTile(83, 110);
            Row(mv.x == 78 && mv.y == 110, $"R3.41 the sim places the EMPTY Bizon on tile ({mv.x},{mv.y}) (Unity x {mv.x * 2 + 2}, z {-(mv.y * 2 + 2)}; want (78,110): the owner's red box, on the bottom road in front of the robot factory, off the crossing)");
            foreach (var l in MovedLamps)
            {
                var t = Find(scene, l.name);
                float d = t ? new Vector2(t.position.x - l.x, t.position.z - l.z).magnitude : 99f;
                Row(t && d < 0.15f, t ? $"R3.17 {l.name} at ({t.position.x:0.00}, {t.position.y:0.00}, {t.position.z:0.00}) ({d:0.00} m from ({l.x}, {l.z}), out of the container's way)" : $"R3.17 {l.name} not in the scene");
            }
            var env = GameObject.Find("Environment");
            var cont = env ? env.transform.Find("L18_Containers") : null;
            Row(cont && cont.childCount >= 7, cont ? $"R3.17 Environment/L18_Containers: {cont.childCount} hosts" : "R3.17 no Environment/L18_Containers");
            foreach (var n in new[] { "A", "B", "C", "W" })
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/OpenZ/Models/L18/Ruins/L18C_Container_{n}.fbx");
                int tris = 0; if (go) foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh && !mf.name.Contains("LOD")) tris += mf.sharedMesh.triangles.Length / 3;
                Row(go && tris >= 5200, $"R3.17 L18C_Container_{n}: {tris} triangles at LOD0 (closed + both sides: about 2 x 2622 + the filled holes)");
            }
            var ship = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OpenZ/Models/L18/Ruins/L18R_ShipWreck.fbx");
            int st = 0; if (ship) foreach (var mf in ship.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh && !mf.name.Contains("LOD")) st += mf.sharedMesh.triangles.Length / 3;
            Row(ship && st >= 16000, $"R3.9 L18R_ShipWreck: {st} triangles at LOD0 (hull closed + both sides)");
            var paint = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/OpenZ/Textures/L18_ground_paint.png");
            var sdf = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/OpenZ/Textures/Ground/L18_ground_sdf.png");
            var mk = Resources.Load<TextAsset>("OpenZ/RoadMarkings/L18_road_markings");
            Row(paint && sdf && mk && paint.width == 4096 && sdf.width == 2048, $"R3.38 ground: paint {(paint ? paint.width : 0)} px, SDF {(sdf ? sdf.width : 0)} px, road markings {(mk ? mk.text.Length : 0)} chars (the 24 road joint tiles drawn as road)");
            sb.Insert(0, $"{(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}\n");
            return sb.ToString();
        }
    }
}
