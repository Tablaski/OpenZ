// OpenZ (GPL-3.0) - version 1.5, level 18 "Bridge Game" (kit level18_map): the level's build menus. The scene itself is built by
// OpenZSetup.BuildLevel("L18") (the Car Park's route: environment model, colliders, sea skirt, sun, DayNight, Atmosphere City, camera over the
// red fort, ZGame, every map object). Only level 18's own files are touched: no model, material or prefab of another level is written.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL18Scene
    {
        const string A = "Assets/OpenZ/", Model = A + "Models/Environment/L18_environment.fbx", GroundMat = A + "Materials/Env/M_L18_Ground.mat";
        const string Paint = A + "Textures/L18_ground_paint.png", Sdf = A + "Textures/Ground/L18_ground_sdf.png", Grid = A + "Resources/OpenZ/Maps/L18_grid.json";
        const string Marks = A + "Resources/OpenZ/RoadMarkings/L18_road_markings.json", ScenePath = A + "Scenes/" + OzCity.BridgeGameScene + ".unity";
        /// <summary>The material names the environment model may carry: level 18's ground and the city's tuned materials (level 17's own).</summary>
        static readonly string[] Allowed = { "M_L18_Ground", "M_Cliff_CityRock", "M_Water_Harbour", "M_QuayWall", "CITY_BoulderRock", "Street_lamp_amber_glass",
                                             "Street_lamp_deep_teal", "Street_lamp_warm_brass", "VC13_metal.006", "No Name" };
        static readonly string[] LongBridge = { "Z_bridge_city_long_intact", "Z_bridge_city_long_damaged", "Z_bridge_city_long_destroyed", "Z_bridge_city_long_chunks" };

        static string Report(string title, List<string> lines, int ok, int n)
        {
            lines.Add($"{(ok == n ? "PASS" : "FAIL")} {ok}/{n}");
            var text = "[OpenZ] " + title + "\n" + string.Join("\n", lines);
            if (ok == n) Debug.Log(text); else Debug.LogError(text);
            return text;
        }

        /// <summary>Step 1, no write: the kit's data files are in, the model's materials are the city's own (the kit's .meta maps them: nothing
        /// is remapped here, so no tuned material of level 17 can be written over), the ground material is level 17's recipe on level 18's
        /// paint and SDF.</summary>
        [MenuItem("OpenZ/L18 Bridge Game/1 Check imports and environment materials (no write)", false, 7700)]
        public static string Imports()
        {
            var lines = new List<string>(); int ok = 0, n = 0;
            void Line(bool good, string what) { n++; if (good) ok++; lines.Add((good ? "PASS " : "FAIL ") + what); }
            foreach (var p in new[] { Grid, Model, GroundMat, Paint, Sdf, Marks }) Line(File.Exists(p), "in the project: " + p);
            var imp = AssetImporter.GetAtPath(Model) as ModelImporter;
            Line(imp != null && imp.bakeAxisConversion, "the model bakes the axis conversion (as level 17's)");
            if (imp != null)
            {
                var map = imp.GetExternalObjectMap();
                var names = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Material>().Select(m => m.name).Concat(map.Keys.Where(k => k.type == typeof(Material)).Select(k => k.name)).Distinct().ToList();
                var foreign = names.Where(x => !Allowed.Contains(x)).ToList();
                Line(foreign.Count == 0, "the model's material names are level 18's ground + the city's own" + (foreign.Count > 0 ? ": OTHER " + string.Join(", ", foreign) : ""));
                var unmapped = Allowed.Where(a => a != "No Name" && !map.Any(kv => kv.Key.name == a && kv.Value)).ToList();
                Line(unmapped.Count == 0, "every material is mapped to a material asset" + (unmapped.Count > 0 ? ": NOT " + string.Join(", ", unmapped) : ""));
                var g = map.Where(kv => kv.Key.name == "M_L18_Ground").Select(kv => kv.Value as Material).FirstOrDefault();
                Line(g && AssetDatabase.GetAssetPath(g) == GroundMat, "M_L18_Ground of the model is " + GroundMat);
                var w = map.Where(kv => kv.Key.name == "M_Water_Harbour").Select(kv => kv.Value as Material).FirstOrDefault();
                Line(w && AssetDatabase.GetAssetPath(w) == A + "Materials/Env/M_Water_Harbour.mat", "the water is the Car Park's M_Water_Harbour (referenced, not copied)");
            }
            var gm = AssetDatabase.LoadAssetAtPath<Material>(GroundMat);
            Line(gm && gm.shader && gm.shader.name == "OpenZ/Sand", "ground material on OpenZ/Sand");
            Line(gm && gm.GetTexture("_BaseMap") == AssetDatabase.LoadAssetAtPath<Texture>(Paint), "ground paint is level 18's");
            Line(gm && gm.HasProperty("_GroundSdf") && gm.GetTexture("_GroundSdf") == AssetDatabase.LoadAssetAtPath<Texture>(Sdf), "crisp-edge SDF is level 18's");
            Line(gm && gm.HasProperty("_GroundSize") && Mathf.Approximately(gm.GetVector("_GroundSize").x, 304f) && Mathf.Approximately(gm.GetVector("_GroundSize").y, 288f), "ground extent 304 x 288 m");
            var gm17 = AssetDatabase.LoadAssetAtPath<Material>(A + "Materials/Env/M_L17_Ground.mat");
            Line(gm && gm17 && gm.shader == gm17.shader && Mathf.Approximately(gm.GetFloat("_SdfMode"), gm17.GetFloat("_SdfMode")) && gm.IsKeywordEnabled("_OZ_NO_REFL_SPEC") == gm17.IsKeywordEnabled("_OZ_NO_REFL_SPEC"),
                 "ground material = the Car Park's recipe (shader, SDF mode with road markings, no day-sky reflection)");
            int have = LongBridge.Count(k => AssetDatabase.LoadAssetAtPath<GameObject>(A + "Prefabs/" + k + ".prefab"));
            lines.Add(have == LongBridge.Length ? "INFO the long city bridge family is in (4 prefabs)"
                                                : $"INFO the long city bridge family: {have} of 4 prefabs (kit level18_assets): build the scene AFTER it is in, else the three centre bridges wear the 12 m model stretched");
            return Report("L18 imports and environment materials", lines, ok, n);
        }

        /// <summary>Step 2: the scene. Refuses when a data file is missing or the long bridge family is not in (a stretched 12 m bridge is no
        /// picture for the owner): "Build anyway" below builds with the 12 m kit for a first look.</summary>
        [MenuItem("OpenZ/L18 Bridge Game/2 Build L18 Bridge Game Scene", false, 7701)]
        public static string Build() => BuildScene(false);
        [MenuItem("OpenZ/L18 Bridge Game/2b Build anyway (12 m bridge kit on the long decks, first look only)", false, 7702)]
        public static string BuildAnyway() => BuildScene(true);

        static string BuildScene(bool anyway)
        {
            foreach (var p in new[] { Grid, Model, GroundMat, Paint, Sdf })
                if (!File.Exists(p)) { string why = "[OpenZ] L18: missing " + p + " (kit level18_map): nothing built"; Debug.LogError(why); return why; }
            int have = LongBridge.Count(k => AssetDatabase.LoadAssetAtPath<GameObject>(A + "Prefabs/" + k + ".prefab"));
            if (have < LongBridge.Length && !anyway)
            {
                string why = $"[OpenZ] L18: the long city bridge family has {have} of 4 prefabs (Z_bridge_city_long_*; kit level18_assets): nothing built. Menu 2b builds a first look without it.";
                Debug.LogError(why); return why;
            }
            string log = OpenZSetup.BuildLevel(OzCity.BridgeGame);
            return log + "\n" + CheckOpenScene();
        }

        /// <summary>Step 3 (the open level 18 scene): the city rock walls (the Car Park's rust-brown rock kit on level 18's 519 rocks).</summary>
        [MenuItem("OpenZ/L18 Bridge Game/3 Rocks (open scene)", false, 7703)]
        public static string Rocks()
        {
            if (!EditorSceneManager.GetActiveScene().name.StartsWith(OzCity.BridgeGame)) return "open " + OzCity.BridgeGameScene + " first";
            string s = OpenZRocks.RebuildOpenLevel(); Debug.Log("[OpenZ] L18 rocks: " + s);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
            return s;
        }

        /// <summary>Batch proof (the kit's prove_on_copy.py, -executeMethod on a COPY of the project, never on the real one): the imports
        /// check; with OZ_L18_BATCH_BUILD=1 also the scene build (the 12 m kit on the long decks when the long family is not in) and the
        /// open-scene check. Writes Logs/level18_map_batch.txt and leaves with exit code 0 when the imports check passed and the build ran.</summary>
        public static void BatchProof()
        {
            var sb = new System.Text.StringBuilder(); bool ok = true;
            try
            {
                string imp = Imports(); sb.AppendLine(imp); ok &= imp.TrimEnd().Split('\n').Last().StartsWith("PASS ");   // the last line is the verdict (PASS n/n)
                if (System.Environment.GetEnvironmentVariable("OZ_L18_BATCH_BUILD") == "1")
                {
                    string built = BuildScene(true); sb.AppendLine("---- build"); sb.AppendLine(built);
                    ok &= built.Contains("markers OK") && File.Exists(ScenePath);
                    sb.AppendLine("---- level switches"); sb.AppendLine(OzV15L18SwitchCheck.Run());
                }
            }
            catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); ok = false; }
            sb.AppendLine(ok ? "BATCH PROOF OK" : "BATCH PROOF FAILED");
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/level18_map_batch.txt", sb.ToString());
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>The counts of the open level 18 scene against the map (edit mode, no Play).</summary>
        [MenuItem("OpenZ/L18 Bridge Game/9 Check the open scene (no Play)", false, 7709)]
        public static string CheckOpenScene()
        {
            var lines = new List<string>(); int ok = 0, n = 0;
            void Line(bool good, string what) { n++; if (good) ok++; lines.Add((good ? "PASS " : "FAIL ") + what); }
            var scene = EditorSceneManager.GetActiveScene();
            Line(scene.name == OzCity.BridgeGameScene, "the open scene is " + OzCity.BridgeGameScene + " (it is " + scene.name + ")");
            var root = GameObject.Find("ZodObjects"); var env = GameObject.Find("Environment"); var game = Object.FindFirstObjectByType<ZGame>();
            Line(root && env && game && game.level == OzCity.BridgeGame && game.player == Team.Red, "Environment, ZodObjects, ZGame (level L18, player red)");
            if (!root || !env) return Report("L18 open scene", lines, ok, n);
            int Count(string prefix) => root.transform.Cast<Transform>().Count(t => t.name.StartsWith(prefix));
            Line(Count("rock_") == 519, $"rocks {Count("rock_")} (519)");
            Line(Count("flag_") == 12, $"flags {Count("flag_")} (12)");
            Line(Count("fort_") == 2, $"forts {Count("fort_")} (2)");
            Line(Count("robot_factory_") == 8 && Count("vehicle_factory_") == 4 && Count("radar_") == 2 && Count("repair_") == 2,
                 $"robot factories {Count("robot_factory_")} (8), vehicle factories {Count("vehicle_factory_")} (4), radars {Count("radar_")} (2), repair bays {Count("repair_")} (2)");
            Line(Count("crane_") == 4, $"cranes {Count("crane_")} (4)");
            var bridges = root.transform.Cast<Transform>().Where(t => t.name.StartsWith("bridge_")).ToList();
            Line(bridges.Count == 5, $"bridges {bridges.Count} (5)");
            var longs = bridges.Where(t => t.name.StartsWith("bridge_vert_40_54") || t.name.StartsWith("bridge_vert_61_54") || t.name.StartsWith("bridge_vert_82_54")).ToList();
            bool unstretched = longs.Count == 3 && longs.All(t => Mathf.Abs(t.localScale.z - 1f) < 0.01f && Mathf.Abs(t.localScale.x - 1f) < 0.01f);
            Line(unstretched, "the three centre bridges wear the long family at scale 1" + (longs.Count == 3 ? " (scale z " + string.Join(" / ", longs.Select(t => t.localScale.z.ToString("0.00"))) + ")" : ""));
            foreach (var t in longs)
            {
                var rs = t.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) { Line(false, t.name + ": no renderer"); continue; }
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                Line(b.size.z > 20f && b.size.z < 27f && b.size.x < 11f, $"{t.name}: {b.size.x:0.0} x {b.size.z:0.0} m (8 x 24), centre ({b.center.x:0.0}, {b.center.z:0.0})");
            }
            Line(game.unitPrefabs != null && LongBridge.All(k => game.unitPrefabs.Any(g => g && g.name == k)), "ZGame holds the long family's four prefabs (break, chunks, repair)");
            Line(env.transform.Cast<Transform>().Any(t => t.name.StartsWith("WAT_")) && env.transform.Find("COL_Terrain"), "water sheet and walk collider");
            int lamps = env.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("DEC_round13_warm_street_lamp") && t.parent == env.transform);
            Line(lamps == 44, $"street lamps on Zod's lamp tiles {lamps} (44)");
            var skirt = GameObject.Find("SKIRT");
            Line(skirt && Mathf.Abs(skirt.transform.position.y + 1.7f) < 0.01f, "sea skirt at -1.7 m with the harbour water");
            Line(EditorBuildSettings.scenes.Any(s => s.path == ScenePath && s.enabled), "the scene is in the build settings");
            return Report("L18 open scene", lines, ok, n);
        }
    }
}
