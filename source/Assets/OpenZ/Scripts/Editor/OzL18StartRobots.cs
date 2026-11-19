// 1.5 level 18 (kit level18_start_robots): the level scene's start robots drawn with the current BS-01 prefabs (Z_bs01_<type>) like
// level 16's, instead of the old Z_robot model the level builder baked for every level but 16. Same name, place, turn, team and order.
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL18StartRobots
    {
        static readonly Regex RobotName = new Regex(@"^(grunt|psycho|tough|sniper|pyro|laser)_\d+_\d+(_\d+)?$");

        static string Want(string name) => "Z_bs01_" + name.Substring(0, name.IndexOf('_'));

        /// <summary>Swaps every start robot of the open level 18 scene that is not its kind's Z_bs01 prefab (idempotent: a second run swaps 0).</summary>
        [MenuItem("OpenZ/L18 Bridge Game/14 Start robots: the BS-01 prefabs (open scene)", false, 7714)]
        public static string Swap()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var root = GameObject.Find("ZodObjects");
            if (scene.name != "L18_BridgeGame" || !root) { string why = "[OpenZ] L18 start robots: open L18_BridgeGame first (nothing changed)"; Debug.LogError(why); return why; }
            var list = new System.Collections.Generic.List<Transform>();
            foreach (Transform t in root.transform) if (RobotName.IsMatch(t.name)) list.Add(t);
            var sb = new StringBuilder(); int swapped = 0;
            foreach (var t in list)
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                string want = Want(t.name);
                if (src && src.name == want) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OpenZ/Prefabs/" + want + ".prefab");
                if (!prefab) { sb.AppendLine("  NO PREFAB " + want + " for " + t.name); continue; }
                int team = 0; var tpOld = t.GetComponent<TeamPaint>();
                if (tpOld) team = new SerializedObject(tpOld).FindProperty("team").intValue;
                int sib = t.GetSiblingIndex();
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = t.localPosition; go.transform.localRotation = t.localRotation; go.name = t.name;
                var tpNew = go.GetComponent<TeamPaint>();
                if (tpNew) { var so = new SerializedObject(tpNew); so.FindProperty("team").intValue = team; so.ApplyModifiedProperties(); }
                sb.AppendLine($"  {t.name}: {(src ? src.name : "(no prefab)")} -> {want}, team {team}");
                Object.DestroyImmediate(t.gameObject);
                go.transform.SetSiblingIndex(sib);
                swapped++;
            }
            if (swapped > 0) EditorSceneManager.MarkSceneDirty(scene);
            string log = $"[OpenZ] L18 start robots: {swapped} of {list.Count} swapped to their Z_bs01 prefab{(swapped > 0 ? " (save the scene)" : "")}\n" + sb + Check();
            Debug.Log(log); return log;
        }

        /// <summary>Counted check of the open level 18 scene: every start robot is its kind's Z_bs01 prefab with a team set (no Play).</summary>
        [MenuItem("OpenZ/L18 Bridge Game/15 Start robots: check the scene (open scene)", false, 7715)]
        public static string CheckMenu() { string s = Check(); Debug.Log(s); return s; }

        public static string Check()
        {
            var root = GameObject.Find("ZodObjects");
            if (!root) return "[OpenZ] L18 start robots: FAIL no ZodObjects";
            int n = 0, ok = 0, red = 0, blue = 0; var bad = new StringBuilder();
            foreach (Transform t in root.transform)
            {
                if (!RobotName.IsMatch(t.name)) continue; n++;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                var tp = t.GetComponent<TeamPaint>();
                int team = tp ? new SerializedObject(tp).FindProperty("team").intValue : -1;
                if (src && src.name == Want(t.name) && team > 0) { ok++; if (team == 1) red++; else blue++; }
                else bad.AppendLine($"  {t.name}: {(src ? src.name : "(no prefab)")}, team {team}");
            }
            return $"[OpenZ] L18 start robots in the scene: {(ok == n && n == 28 ? "PASS" : "FAIL")} {ok} of {n} on their Z_bs01 prefab (28 expected: 14 red, 14 blue; now red {red}, blue {blue})\n" + bad;
        }
    }
}
