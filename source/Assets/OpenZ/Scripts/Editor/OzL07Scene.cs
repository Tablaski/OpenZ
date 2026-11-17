// OpenZ (GPL-3.0) - version 1.3, item 12 part B6: the level 7 steps after OpenZ/L07 Volcanic/2 Build L07 Volcanic Scene (OpenZSetup). The
// rock kit and the huts, from the Blender pass's export folder (lava_biome_L07/blender_pass/BUDGETS.md section 11). Only level 7's own
// files are touched: no model, material or prefab of another level.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL07Scene
    {
        const string RocksDst = "Assets/OpenZ/Models/RocksZ", ModelDir = "Assets/OpenZ/Models/L07";
        const float ViewScale = 0.6f;
        static string Export => Path.Combine(OpenZPaths.RepoRoot, "work", "staging", "v1_3", "lava_biome_L07", "blender_pass", "export");

        /// <summary>The 14 RockZ_Volcanic meshes + atlas + emission map: copied, imported as the RockZ kit, all readable (OzRockSolid keeps
        /// their real shape), then every rock of the open level 7 scene rebuilt (OpenZRocks).</summary>
        [MenuItem("OpenZ/L07 Volcanic/3 Rock kit and rocks (open scene)")]
        public static string Rocks()
        {
            if (!EditorSceneManager.GetActiveScene().name.StartsWith(OzVolcanic.Level)) return "open L07_Volcanic first";   // RebuildOpenLevel works on whatever scene is open
            string src = Path.Combine(Export, "RocksZ");
            if (!Directory.Exists(src)) return "no " + src;
            Directory.CreateDirectory(RocksDst);
            var fbx = Directory.GetFiles(src, "RockZ_Volcanic_*.fbx");
            if (fbx.Length != 14) return $"{fbx.Length} of 14 rock meshes in {src}: nothing done";
            foreach (var f in fbx.Concat(Directory.GetFiles(src, "rockz2_*_Volcanic.png"))) File.Copy(f, $"{RocksDst}/{Path.GetFileName(f)}", true);
            AssetDatabase.Refresh();
            foreach (var f in fbx)
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath($"{RocksDst}/{Path.GetFileName(f)}");
                mi.globalScale = 1; mi.useFileUnits = true; mi.bakeAxisConversion = false; mi.importCameras = false; mi.importLights = false;
                mi.meshCompression = ModelImporterMeshCompression.Off; mi.importNormals = ModelImporterNormals.Import; mi.addCollider = false;
                mi.isReadable = true; mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.SaveAndReimport();
            }
            foreach (var n in new[] { "rockz2_atlas_Volcanic.png", "rockz2_emis_Volcanic.png" })
            {
                var ti = AssetImporter.GetAtPath($"{RocksDst}/{n}") as TextureImporter; if (!ti) continue;
                ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Trilinear; ti.anisoLevel = 8; ti.maxTextureSize = 2048; ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            var s = OpenZRocks.RebuildOpenLevel(); Debug.Log("[OpenZ] L07 rocks: " + s);
            RockTint(); s += RockHeights(); return s;
        }

        /// <summary>The one material of every rock stage (intact, Damaged, Rubble, Chunks): tint white, the atlas holds the colour, so one colour across a stage change. This stays the one knob for a last fine tune.</summary>
        public static void RockTint()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{RocksDst}/M_RockZ_Volcanic.mat"); if (!m) { Debug.LogError("[OpenZ] L07: M_RockZ_Volcanic missing"); return; }
            m.SetColor("_BaseColor", Color.white); EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// picture only: same tile, same height in every build, position, yaw, hit size and blocked tiles untouched. RockSeeThru, OzRockSolid and the cannon's
        /// landing test read the drawn shape, so they follow. Drawn top 3.8 to 7.04 m, the old kit's highest. A stump comes from the same height by Y scale, 2.35 to 4.68 m: one mesh, a wall keeps its steps, and the final break starts in rock. Blast, dust and chunks start under 2.5 m, in the rock.</summary>
        public static string RockHeights()
        {
            var root = GameObject.Find("ZodObjects"); if (!root) return "; heights NOT set: no ZodObjects";
            var result = ApplyRockHeights(root.transform);
            var scene = EditorSceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveOpenScenes();
            return result;
        }

        internal static string ApplyRockHeights(Transform root)
        {
            var rocks = new Dictionary<(int, int), RockPillar>();
            foreach (var rp in root.GetComponentsInChildren<RockPillar>(true))
            {
                var p = rp.transform.position; rocks[(Mathf.RoundToInt((p.x - 1f) / 2f), Mathf.RoundToInt((-p.z - 1f) / 2f))] = rp;
            }
            var meshes = new Dictionary<char, Mesh>();
            foreach (char c in RockMeshOrder)
            {
                var m = AssetDatabase.LoadAllAssetsAtPath($"{RocksDst}/RockZ_Volcanic_Pillar_{c}.fbx").OfType<Mesh>().FirstOrDefault(); if (!m) throw new System.InvalidOperationException("Volcanic Pillar_" + c + " missing");
                meshes[c] = m;
            }
            double lo = 99, hi = 0;
            foreach (var kv in RockHeightPlan(rocks.Keys))
            {
                var rp = rocks[kv.Key]; var (h, mesh, sy) = kv.Value; lo = System.Math.Min(lo, h); hi = System.Math.Max(hi, h);
                rp.pillar.GetComponent<MeshFilter>().sharedMesh = meshes[mesh]; SetY(rp.pillar.transform, (float)sy);
                Round(rp.pillar.transform); Round(rp.damaged.transform); SetY(rp.damaged.transform, (float)(RockStumpTop(h) / rp.damaged.GetComponent<MeshFilter>().sharedMesh.bounds.max.y));
            }
            return $"; heights {lo:0.00} to {hi:0.00} m on {rocks.Count} rocks";
        }

        static void SetY(Transform t, float y) { var s = t.localScale; s.y = y; t.localScale = s; }

        /// <summary>The picture only: a side the rebuild squashed for the old rock's 1.3 m reach goes back, and no rock stands over side scale 1. A squashed hexagon turns oval, and a rock over 1 only makes the walls bigger. Columns reach 1.03 m at yaw 0, a little past the tile on a rock's yaw; stones and the low skirt lie up to .38 m past it, drawn only, inside ZHeavyFallData.RockReachM.</summary>
        static void Round(Transform t) { var s = t.localScale; s.x = s.z = (float)RockSideScale(s.x, s.z); t.localScale = s; }

        // RockHeightPlan begin (no Unity type below: rocks_r3/tools/rehearse_rocks.py runs this text against the design's numbers)
        static double RockStumpTop(double h) => 2.35 + (h - 3.8) * 0.72;
        static double RockSideScale(double x, double z) => System.Math.Min(System.Math.Max(System.Math.Max(x, z), 0.95), 1.0);
        const string RockMeshOrder = "BACD";
        static double RockMeshTop(char c) => c == 'B' ? 4.2 : c == 'A' ? 5.0 : c == 'C' ? 5.8 : 6.6;

        static uint RockHash(int x, int y, int salt)
        {
            unchecked { uint v = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791); v ^= v >> 13; v *= 1274126177u; v ^= v >> 16; return v; }
        }

        static double RockU(int x, int y, int salt) => RockHash(x, y, salt) / 4294967296.0;

        static double RockCurve(double v)
        {
            double[] a = { 0.70, 0.84, 1.0, 1.13, 1.30 }; double t = v * 4; int i = System.Math.Min((int)t, 3); return a[i] + (a[i + 1] - a[i]) * (t - i);
        }

        static Dictionary<(int, int), (double h, char mesh, double sy)> RockHeightPlan(IEnumerable<(int, int)> rocks)
        {
            var tiles = new HashSet<(int, int)>(rocks); var order = tiles.OrderBy(t => t.Item2).ThenBy(t => t.Item1).ToList();
            var rise = new Dictionary<(int, int), double>(); var stack = new Stack<(int, int)>();
            foreach (var a in order)                                                   // a wall = 8-neighbour group; one wall in five stands low
            {
                if (rise.ContainsKey(a)) continue;
                double v = RockU(a.Item1, a.Item2, 11), w = RockU(a.Item1, a.Item2, 12), r = v < 0.2 ? -0.95 + w * 0.35 : -0.15 + w * 0.55;
                rise[a] = r; stack.Push(a);
                while (stack.Count > 0)
                {
                    var p = stack.Pop();
                    for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                    {
                        var q = (p.Item1 + dx, p.Item2 + dy);
                        if (tiles.Contains(q) && !rise.ContainsKey(q)) { rise[q] = r; stack.Push(q); }
                    }
                }
            }
            var plan = new Dictionary<(int, int), (double h, char mesh, double sy)>();
            foreach (var t in order)
            {
                int x = t.Item1, y = t.Item2; double v0 = RockU(x, y, 7), bestScore = double.NegativeInfinity; var best = (h: 0.0, mesh: 'A', sy: 1.0);
                var nb = new List<double>(); var nv = new List<char>();
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                    if ((dx != 0 || dy != 0) && plan.TryGetValue((x + dx, y + dy), out var o)) { nb.Add(o.h); if ((dx == -1 && dy == 0) || (dx == 0 && dy == -1)) nv.Add(o.mesh); }
                for (int i = 0; i < 24; i++)
                {
                    double v = (v0 + i * 0.6180339887) % 1.0, h = System.Math.Min(System.Math.Max(5.4 * RockCurve(v) + rise[t], 3.8), 7.04), d = 9;
                    foreach (var n in nb) d = System.Math.Min(d, System.Math.Abs(h - n));
                    var ms = RockMeshOrder.OrderBy(c => System.Math.Abs(RockMeshTop(c) - h)).ToArray(); char m = ms[0];
                    foreach (var c in ms) if (!nv.Contains(c)) { m = c; break; }
                    double sy = h / RockMeshTop(m), s = System.Math.Min(d, 0.6) * 3 - (sy >= 0.90 && sy <= 1.08 ? 0 : 5 + System.Math.Abs(System.Math.Log(sy)) * 3) - i * 0.002;
                    if (s > bestScore) { bestScore = s; best = (h, m, sy); }
                }
                plan[t] = best;
            }
            return plan;
        }
        // RockHeightPlan end

        /// <summary>The crystal's look at the game camera: the model's painted greens and wine red, no plastic shine, no sky rim.</summary>
        public static void CrystalLook(Material m)
        {
            m.SetColor("_BaseColor", new Color(0.70f, 0.66f, 0.58f, 1f)); m.SetFloat("_Smoothness", 0.06f);
            if (m.HasProperty("_RimColor")) m.SetColor("_RimColor", Color.black);
            EditorUtility.SetDirty(m);
        }

        /// <summary>Map objects 15 (tall) and 16 (low) are Zod's own destroyable map items, so they stay sim objects with the map's hit points
        /// and die by the map-object route. Only their picture changes: the crystal of DV2_L07_Kit.fbx in the place of the builder's plain marker,
        /// BODY_ up, RUIN_ and LOOSE_ put away for the break (View/OzL07Crystal.cs). No placement file and no DV67 detail prop: those would add a
        /// second target on the same tile.</summary>
        [MenuItem("OpenZ/L07 Volcanic/5 Crystals (open scene)")]
        public static string Crystals()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.StartsWith(OzVolcanic.Level)) return "open L07_Volcanic first";
            const string Dir = "Assets/OpenZ/Models/DetailsV2";
            string src = Path.Combine(Export, "DetailsV2"), model = $"{Dir}/DV2_L07_Kit.fbx", tex = $"{Dir}/dv2_atlas_L07.png";
            if (!File.Exists(Path.Combine(src, "DV2_L07_Kit.fbx"))) return "no DV2_L07_Kit.fbx in " + src;
            Directory.CreateDirectory(Dir);
            File.Copy(Path.Combine(src, "DV2_L07_Kit.fbx"), model, true); File.Copy(Path.Combine(src, "dv2_atlas_L07.png"), tex, true);
            AssetDatabase.Refresh();
            var imp = (ModelImporter)AssetImporter.GetAtPath(model);
            imp.globalScale = 1; imp.useFileUnits = true; imp.bakeAxisConversion = false; imp.importCameras = false; imp.importLights = false;
            imp.meshCompression = ModelImporterMeshCompression.Off; imp.isReadable = false; imp.addCollider = false;
            imp.importAnimation = false; imp.animationType = ModelImporterAnimationType.None; imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.SaveAndReimport();
            var kit = AssetDatabase.LoadAssetAtPath<GameObject>(model); if (!kit) return "cannot load " + model;
            Transform tall = kit.transform.Cast<Transform>().FirstOrDefault(c => c.name.StartsWith("DV2_V01_CrystalTall")), low = kit.transform.Cast<Transform>().FirstOrDefault(c => c.name.StartsWith("DV2_V02_CrystalLow"));   // Blender may add .001
            if (!tall || !low) return "DV2_L07_Kit.fbx misses DV2_V01_CrystalTall or DV2_V02_CrystalLow";
            var mat = OpenZL16Water.LitMat("M_L07_Crystal", tex, 0.3f, Dir); CrystalLook(mat);
            var zroot = GameObject.Find("ZodObjects"); if (!zroot) return "no ZodObjects in the open scene";
            var ground = GameObject.Find("COL_Terrain")?.GetComponent<Collider>();
            int n = 0;
            foreach (var t in zroot.transform.Cast<Transform>().Where(c => c.name.StartsWith("map_object15_") || c.name.StartsWith("map_object16_")).ToList())
            {
                if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
                foreach (var c in t.GetComponents<Component>()) if (!(c is Transform)) Object.DestroyImmediate(c);
                var parts = t.name.Split('_'); int tx = int.Parse(parts[2]), ty = int.Parse(parts[3]);   // map_object15_<x>_<y>: the type's own "_" counts
                float x = tx * 2 + 1, z = -(ty * 2 + 1), y = ground && ground.Raycast(new Ray(new Vector3(x, 200f, z), Vector3.down), out var h, 400f) ? h.point.y : t.position.y;
                t.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0f, (tx * 37 + ty * 61) % 360, 0f)); t.localScale = Vector3.one;
                var go = Object.Instantiate(t.name.StartsWith("map_object15_") ? tall.gameObject : low.gameObject, t, false);
                go.name = "Crystal"; go.transform.localPosition = Vector3.zero; go.transform.localScale = go.transform.localScale * ViewScale;   // only the picture: hit box, blocked tile and sim stay
                foreach (Transform c in go.transform.Cast<Transform>().ToList()) if (!c.name.StartsWith("BODY_") && !c.name.StartsWith("RUIN_") && !c.name.StartsWith("LOOSE_")) Object.DestroyImmediate(c.gameObject);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
                t.gameObject.AddComponent<OzL07Crystal>().Bind(go.transform);
                n++;
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            var s = $"L07: {n} crystals placed"; Debug.Log("[OpenZ] " + s); return s;
        }

        static Mesh MeshNamed(IEnumerable<Mesh> all, string name) => all.FirstOrDefault(m => m.name == name);

        /// <summary>Every hut of the open level 7 scene becomes the volcanic hut (three distances + a ruin, L16Hut: the jungle's route, far lighter
        /// than five BP2 sets). BasePropHuts leaves level 7 alone, so the prefab's desert default never shows.</summary>
        [MenuItem("OpenZ/L07 Volcanic/4 Huts (open scene)")]
        public static string Huts()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.StartsWith(OzVolcanic.Level)) return "open L07_Volcanic first";
            string src = Path.Combine(Export, "L07"), model = $"{ModelDir}/L07_hut.fbx", tex = $"{ModelDir}/L07_hut.png";
            if (!File.Exists(Path.Combine(src, "L07_hut.fbx"))) return "no L07_hut.fbx in " + src;
            Directory.CreateDirectory(ModelDir);
            File.Copy(Path.Combine(src, "L07_hut.fbx"), model, true); File.Copy(Path.Combine(src, "L07_hut.png"), tex, true);
            string emis = $"{ModelDir}/L07_hut_emis.png"; File.Copy(Path.Combine(src, "L07_hut_emis.png"), emis, true);   // nests: glow of the cave, the cracks and the eggs
            AssetDatabase.Refresh();
            var imp = (ModelImporter)AssetImporter.GetAtPath(model);
            imp.globalScale = 1; imp.useFileUnits = true; imp.bakeAxisConversion = true; imp.importCameras = false; imp.importLights = false;
            imp.meshCompression = ModelImporterMeshCompression.Off; imp.isReadable = false; imp.addCollider = false;
            imp.importAnimation = false; imp.animationType = ModelImporterAnimationType.None; imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.SaveAndReimport();
            var meshes = AssetDatabase.LoadAllAssetsAtPath(model).OfType<Mesh>().ToList();
            Mesh m0 = MeshNamed(meshes, "L07_Hut"), m1 = MeshNamed(meshes, "L07_Hut_LOD1"), m2 = MeshNamed(meshes, "L07_Hut_LOD2"), mr = MeshNamed(meshes, "L07_Hut_Ruin");
            if (!m0 || !m1 || !m2 || !mr) return "L07_hut.fbx misses a mesh (L07_Hut, _LOD1, _LOD2, _Ruin)";
            Mesh b0 = MeshNamed(meshes, "L07_Hut_B"), b1 = MeshNamed(meshes, "L07_Hut_B_LOD1"), b2 = MeshNamed(meshes, "L07_Hut_B_LOD2"), br = MeshNamed(meshes, "L07_Hut_B_Ruin");
            if (!b0 || !b1 || !b2 || !br) return "L07_hut.fbx misses a mesh of form B (L07_Hut_B, _LOD1, _LOD2, _Ruin)";
            var nodeTurn = AssetDatabase.LoadAssetAtPath<GameObject>(model).transform.Find("L07_Hut").localRotation;   // the FBX node turns the Z up mesh
            var mat = OpenZL16Water.LitMat("M_L07_Hut", tex, 0.04f, ModelDir);
            mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(emis)); mat.SetColor("_EmissionMapColor", new Color(1.3f, 1.3f, 1.3f, 1f)); mat.SetFloat("_EmissionNight", 0f); EditorUtility.SetDirty(mat);   // glows by day too, as the lava
            var zroot = GameObject.Find("ZodObjects"); if (!zroot) return "no ZodObjects in the open scene";
            var ground = GameObject.Find("COL_Terrain")?.GetComponent<Collider>();
            float Ground(float x, float z, float dflt) => ground && ground.Raycast(new Ray(new Vector3(x, 200f, z), Vector3.down), out var h, 400f) ? h.point.y : dflt;
            GameObject Part(Transform parent, string name, Mesh mesh, bool shadow)
            {
                var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localRotation = nodeTurn; go.layer = parent.gameObject.layer;
                go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
                r.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                return go;
            }
            int n = 0;
            foreach (var t in zroot.transform.Cast<Transform>().Where(c => c.name.StartsWith("hut_")).ToList())
            {
                if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
                foreach (var c in t.GetComponents<Component>()) if (!(c is Transform)) Object.DestroyImmediate(c);
                var parts = t.name.Split('_'); int tx = int.Parse(parts[1]), ty = int.Parse(parts[2]);
                float x = tx * 2 + 1, z = -(ty * 2 + 1), y0 = t.position.y;
                float mid = new[] { Ground(x - 0.8f, z - 0.8f, y0), Ground(x + 0.8f, z - 0.8f, y0), Ground(x - 0.8f, z + 0.8f, y0), Ground(x + 0.8f, z + 0.8f, y0) }.Average();   // nests: the mound stands 0.24 m out of the mean ground, floor and pit stay over the highest ground of the 12 places
                t.SetPositionAndRotation(new Vector3(x, mid - 0.02f, z), Quaternion.identity); t.localScale = Vector3.one;
                var hut = new GameObject("Hut"); hut.transform.SetParent(t, false); hut.layer = t.gameObject.layer;
                bool formB = false;
                float turn = (tx * 7 + ty * 11) % 23 - 14f + 180f;   // RUN7 U6: + 180, the cave mouth toward the game camera (-Z) as on the approved sheet; the FBX node (+90 about X) puts model +Y on game +Z
                hut.transform.localRotation = Quaternion.Euler(0f, turn, 0f);   // no two mouths alike; -14 to 8 degrees keeps the way out clear, picture only
                var l0 = Part(hut.transform, "L07_Hut", formB ? b0 : m0, true).GetComponent<Renderer>(); var l1 = Part(hut.transform, "L07_Hut_LOD1", formB ? b1 : m1, true).GetComponent<Renderer>(); var l2 = Part(hut.transform, "L07_Hut_LOD2", formB ? b2 : m2, false).GetComponent<Renderer>();
                var lg = hut.AddComponent<LODGroup>();
                lg.fadeMode = LODFadeMode.None;
                lg.SetLODs(new[] { new LOD(0.06f, new[] { l0 }), new LOD(0.036f, new[] { l1 }), new LOD(0.004f, new[] { l2 }) });   // BUDGETS.md section 8
                lg.RecalculateBounds();                                                                  // as OpenZL16Structures: else the group keeps size 1 and the switch heights mean nothing
                var ruin = Part(t, "RUIN", formB ? br : mr, true); ruin.transform.localRotation = Quaternion.Euler(0f, turn, 0f) * nodeTurn; ruin.SetActive(false);
                var lh = t.gameObject.AddComponent<L16Hut>(); lh.hut = hut; lh.ruin = ruin;
                lh.debris = new[] { new Color(0.34f, 0.31f, 0.29f), new Color(0.20f, 0.18f, 0.17f), new Color(0.48f, 0.26f, 0.14f) };
                lh.chunks = "stone"; lh.leanEvent = "dst_l07_hut_lean"; lh.breakEvent = "dst_l07_hut_break"; lh.collapseEvent = "dst_l07_hut_collapse"; lh.logTag = "L07";   // a stone hut breaks as stone
                lh.collapseA = "OpenZ/Audio/Sfx/stone-collapse"; lh.collapseB = "OpenZ/Audio/Sfx/v3-collapse-medium-a";
                lh.leanHeight = 0.6f; lh.breakHeight = 0.5f;                                             // the nest is 0.95 m high: bits start in it, not in the air over it
                n++;
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            var s = $"L07: {n} huts placed"; Debug.Log("[OpenZ] " + s); return s;
        }
    }
}
