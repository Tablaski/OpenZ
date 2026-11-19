// OpenZ (GPL-3.0) - version 1.5, level 18 "Bridge Game" (kit level18_surroundings): the counted import check of the level's border rock and
// outer world (edit mode, no Play, no scene needed): both models are in, their parts are there, every material slot is one of the project's
// own materials with its level 18 texture, the tile markers stand on the map's corners, the fitted placement file reads and names models
// that exist, the props list of the cliffs v3 menu is beside the models. Pattern: Editor/OzV15L18SwitchCheck.cs.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL18SurroundingsCheck
    {
        const string Dir = "Assets/OpenZ/Models/Environment/CliffsV3", MatDir = "Assets/OpenZ/Materials/Env", Ruins = "Assets/OpenZ/Models/L18/Ruins";
        static readonly string[] Own = { "M_CliffV3_Rock_L18", "M_CliffV3_Top_L18", "M_CliffV3_Concrete_L18", "M_CliffV3_Facade_L18", "M_CliffV3_Ground_L18", "M_CliffV3_Metal_L18", "M_CliffV3_Brick_L18", "M_CliffV3_Roof_L18" };   // kit l18_outer_r3: the ruins' roofs (R3.36)
        static readonly string[] Shared = { "M_L18_Ground", "M_Water_Harbour" };

        [System.Serializable] class Item { public string id, piece, @base, ring; public float x, y, z, scale; }
        [System.Serializable] class Sur { public Item[] items; }
        [System.Serializable] class Doc { public Sur surroundings; }

        [MenuItem("OpenZ/Checks/V1.5/L18 surroundings imports (no Play)", false, 7711)]
        public static string Run()
        {
            var lines = new List<string>(); int ok = 0, n = 0;
            void Line(bool good, string what) { n++; if (good) ok++; lines.Add((good ? "PASS " : "FAIL ") + what); }
            var cliffs = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "/L18_cliffs_v3.fbx");
            var outer = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "/L18_outer_v3.fbx");
            Line(cliffs, "model L18_cliffs_v3.fbx"); Line(outer, "model L18_outer_v3.fbx");
            if (cliffs && outer)
            {
                var cn = cliffs.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
                var on = outer.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
                foreach (var want in new[] { "CLF3_Rock_N", "CLF3_Rock_S", "CLF3_Rock_W", "CLF3_Rock_E", "CLF3_RetainingWalls", "GND3_FloorPatch", "COL_Terrain", "MRK3_tile_0_0" })
                    Line(cn.Contains(want), "border rock part " + want);
                foreach (var want in new[] { "OUT3_Rock_", "OUT3_Ground_", "OUT3_RoadMarks_", "OUT3_Ruins_", "OUT3_Works_", "OUT3_Harbour_", "OUT3_Debris_", "OUT3_SeaWater_", "OUT3_Bed_" })
                    Line(on.Any(x => x.StartsWith(want)), "outer world parts " + want + "* (" + on.Count(x => x.StartsWith(want)) + ")");
                var a = cliffs.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "MRK3_tile_0_0");
                var b = cliffs.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "MRK3_tile_127_119");
                Line(a && (a.position - new Vector3(1, 0, -1)).magnitude < 0.01f, "marker of tile 0, 0 at (1, 0, -1)" + (a ? ": " + a.position.ToString("F2") : ""));
                Line(b && (b.position - new Vector3(255, 0, -239)).magnitude < 0.01f, "marker of tile 127, 119 at (255, 0, -239)" + (b ? ": " + b.position.ToString("F2") : ""));
                long tc = 0, to = 0; var used = new Dictionary<string, int>(); var loose = new List<string>();
                foreach (var go in new[] { cliffs, outer })
                    foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var mf = r.GetComponent<MeshFilter>(); long tris = 0;
                        if (mf && mf.sharedMesh) for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) tris += mf.sharedMesh.GetIndexCount(s) / 3;
                        if (r.name == "COL_Terrain") continue;                                     // the collider mesh: no material, not drawn
                        if (go == cliffs) tc += tris; else to += tris;
                        foreach (var m in r.sharedMaterials)
                        {
                            string p = m ? AssetDatabase.GetAssetPath(m) : "";
                            if (!m || !p.EndsWith(".mat")) { loose.Add(r.name + ":" + (m ? m.name : "none")); continue; }
                            used[m.name] = used.TryGetValue(m.name, out int c) ? c + 1 : 1;
                        }
                    }
                var looseOwn = loose.Where(x => !Shared.Any(s => x.EndsWith(":" + s))).ToList();
                Line(looseOwn.Count == 0, "every slot of the level's own materials is a project material" + (looseOwn.Count > 0 ? ": " + string.Join(", ", looseOwn.Take(8)) : ""));
                var looseShared = loose.Count - looseOwn.Count;
                lines.Add("INFO " + (looseShared == 0 ? "the two shared materials (level ground, harbour water) are mapped" : looseShared + " slots wait for the shared materials " + string.Join(" / ", Shared) + " (made by the level's own menus)"));
                Line(used.Keys.All(k => Own.Contains(k) || Shared.Contains(k)), "materials in use: " + string.Join(", ", used.Keys.OrderBy(k => k)));
                Line(Own.All(k => used.ContainsKey(k)), "all eight level 18 materials are in use");
                lines.Add("INFO triangles: border rock " + tc + ", outer world " + to);
            }
            foreach (var name in Own)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + name + ".mat");
                var tex = m ? m.GetTexture("_BaseMap") : null;
                string tp = tex ? AssetDatabase.GetAssetPath(tex) : "";
                Line(m && m.shader && m.shader.name == "OpenZ/TeamLit" && tp.Contains("/Textures/CliffsV3/") && tp.Contains("_L18_"), name + " -> " + (tp.Length > 0 ? Path.GetFileName(tp) : "no texture"));
            }
            string list = Dir + "/cliffs_v3_scene_L18.txt";
            Line(File.Exists(list), "props list " + list + (File.Exists(list) ? " (" + File.ReadAllLines(list).Length + " lines)" : ""));
            string pj = Ruins + "/L18_surroundings_placement.json";
            if (File.Exists(pj))
            {
                var doc = JsonUtility.FromJson<Doc>(File.ReadAllText(pj));
                var items = doc != null && doc.surroundings != null && doc.surroundings.items != null ? doc.surroundings.items : new Item[0];
                Line(items.Length >= 40, "placement file: " + items.Length + " pieces");
                var missing = items.Select(i => i.piece).Distinct().Where(p => !File.Exists(Ruins + "/" + p + ".fbx")).ToList();
                Line(missing.Count == 0, "every piece model is in " + Ruins + (missing.Count > 0 ? " (missing " + string.Join(", ", missing) + ")" : ""));
                Line(items.All(i => i.x < -8 || i.x > 264 || i.z > 8 || i.z < -248 || i.piece == "L18R_PipeRun"), "every piece stands outside the play area (the two pipe runs end at the border)");
                Line(items.All(i => i.@base == "water" ? Mathf.Abs(i.y + 0.8f) < 0.01f : i.y > 1f), "heights: water pieces on the water line, ground pieces on land");
            }
            else Line(false, "placement file " + pj);
            lines.Add((ok == n ? "PASS " : "FAIL ") + ok + "/" + n);
            string text = string.Join("\n", lines);
            Debug.Log("[OpenZ] L18 surroundings imports\n" + text);
            return text;
        }

        /// <summary>The batch proof of the kit: the check into Logs/level18_surroundings_batch.txt, exit code 0 when every line passes.</summary>
        public static void BatchProof()
        {
            string text; bool ok;
            try { text = Run(); ok = text.TrimEnd().Split('\n').Last().StartsWith("PASS "); }
            catch (System.Exception e) { text = "EXCEPTION " + e; ok = false; }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/level18_surroundings_batch.txt", text + "\n" + (ok ? "BATCH PROOF OK" : "BATCH PROOF FAILED") + "\n");
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
