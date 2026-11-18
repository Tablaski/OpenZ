// OpenZ-VK (GPL-3.0).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenZ.Experimental;
using OpenZ.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenZ.EditorTools
{
    public static partial class OzVolcanicConversionBake
    {
        const string OpenZAssets = "Assets/OpenZ/";
        const string Package = "Packages/org.openz.shittymaps/";
        const string PackageDisk = "../shittymaps/";
        static readonly string[] ReusableViews = { "grenades", "hut", "flag", "map_object15", "map_object16" };

        [MenuItem("OpenZ/Shitty Maps/Bake volcanic V2 maps")]
        public static void All()
        {
            foreach (var entry in OzShittyMapCatalog.VolcanicV2) Bake(entry.Id);
        }

        [MenuItem("OpenZ/Shitty Maps/Bake V2 reference maps")]
        public static void ReferenceMaps()
        {
            foreach (var entry in OzShittyMapCatalog.V2) Bake(entry.Id);
        }

        // A custom V2 grid can be baked without adding it to either game menu.
        public static void Bake(string id)
        {
            if (string.IsNullOrEmpty(id) || !System.Text.RegularExpressions.Regex.IsMatch(id, "^SM[A-Za-z0-9_]+V2$"))
                throw new ArgumentException("Expected a separate SM...V2 map ID.", nameof(id));
            var asset = Resources.Load<TextAsset>("OpenZ/Maps/" + id + "_grid");
            if (!asset) throw new InvalidOperationException("Missing converted grid " + id);
            var map = ZMapData.FromJson(asset.text);
            var profile = Profile.For(map.planet);
            var world = ZWorld.Create(Resources.Load<TextAsset>("OpenZ/default_settings").text, asset.text, 19);

            var scene = EditorSceneManager.OpenScene(OpenZAssets + "Scenes/" + profile.Scene + ".unity", OpenSceneMode.Single);
            var originals = GameObject.Find("ZodObjects");
            var templates = SelectTemplates(map, originals.transform);
            var scenery = ExtractScenery(map.planet, scene);

            var environment = new GameObject("Converted environment");
            if (map.planet == "city") AddCityOutskirts(scene, map, environment.transform);
            var terrain = map.planet == "volcanic" ? environment.AddComponent<OzVolcanicConversionTerrain>()
                : environment.AddComponent<OzConversionTerrainV2>();
            terrain.mapId = id;
            terrain.ground = Material("Materials/Env/" + profile.Ground + ".mat");
            terrain.cliff = Material("Materials/Env/" + profile.Cliff + ".mat");
            terrain.pit = Material("Materials/Env/" + profile.Bed + ".mat");
            terrain.water = Material("Materials/Env/" + profile.Water + ".mat");
            terrain.Build(map, false);
            var collider = environment.GetComponentInChildren<MeshCollider>();
            Physics.SyncTransforms();

            var views = new GameObject("Converted objects");
            bool jungle = map.planet == "jungle";
            foreach (var obj in map.objects)
            {
                bool rock = obj.Type == "rock";
                if (!rock && !templates.ContainsKey(obj.Type)) continue;
                var go = rock && !jungle ? new GameObject()
                    : Object.Instantiate(PickTemplate(templates[obj.Type], obj).gameObject);
                go.name = $"{obj.Type}_{obj.x}_{obj.y}";
                go.transform.SetParent(views.transform, false);
                int row = obj.y + (rock ? 2 : 0);
                var p = new Vector3(obj.x * 2 + 1, 0, -(row * 2 + 1));
                if (collider.Raycast(new Ray(p + Vector3.up * 200, Vector3.down), out var hit, 400)) p.y = hit.point.y;
                go.transform.position = p;
            }
            Object.DestroyImmediate(originals);
            views.name = "ZodObjects";
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Environment" || root.name == "SKIRT" || root.name == "L07_Rim" ||
                    root.name == "OZ_TowerMounts_Baked" || root.name == "L02_ShoreWetBand" ||
                    root.name.StartsWith("DetailsV2_", StringComparison.Ordinal))
                    Object.DestroyImmediate(root);
            }
            environment.name = "Environment";
            Physics.SyncTransforms();

            OpenZSetup.SpawnConversion(map, views.transform, profile.Level, obj => obj.Type != "rock" && !templates.ContainsKey(obj.Type));
            var rocks = jungle ? views.GetComponentsInChildren<RockPillar>(true).Length + " textured jungle rock slices"
                : OpenZRocks.Build(world, views.transform, collider, profile.RockKit,
                    Material("Models/RocksZ/M_RockZ_" + profile.RockKit + ".mat"));
            if (map.planet == "volcanic") rocks += OzL07Scene.ApplyRockHeights(views.transform);
            TowerGunSeat.SeatAll(views.transform, bakePlinths: false);
            if (map.planet == "volcanic") DressRim(map, environment.transform);
            else DressLand(map, environment.transform, collider, scenery);

            var game = Object.FindFirstObjectByType<ZGame>();
            game.level = id;
            var camera = Object.FindFirstObjectByType<RtsCamera>();
            camera.mapSizeMeters = new Vector2(map.width * 2, map.height * 2);
            var red = map.objects.First(o => o.owner == "red" && o.kind.StartsWith("building:fort"));
            camera.focus = new Vector3(red.x * 2 + 10, 0, -red.y * 2 - 12);
            var atmosphere = Object.FindFirstObjectByType<Atmosphere>();
            atmosphere.level = id;
            atmosphere.lampRoot = views.transform;
            // The original ground is reused as a material input, and the generated copy owns its look.
            atmosphere.groundMaterial = null;
            Object.FindFirstObjectByType<DayNight>().lampRoot = views.transform;

            VerifyViews(map, views.transform);
            var preview = OzLevelInfo.Bake(world, id, true);
            File.WriteAllBytes(PackageDisk + "Resources/OpenZ/ShittyMaps/Previews/" + id + ".png", preview.EncodeToPNG());
            Object.DestroyImmediate(preview);
            Object.DestroyImmediate(collider.gameObject);
            terrain.Clear();

            string scenePath = Package + "Scenes/" + id + "_Converted.unity";
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new IOException("Could not save " + scenePath);
            AssetDatabase.Refresh();
            if (OzShittyMapCatalog.Find(id) != null && !EditorBuildSettings.scenes.Any(s => s.path == scenePath))
            {
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(scenePath, true)).ToArray();
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"[Shitty Maps V2] {id}: {map.width} x {map.height}, {map.objects.Count} native records, {rocks}");
        }

        static Material Material(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(OpenZAssets + path);
            if (!material) throw new InvalidOperationException("Missing OpenZ material " + path);
            return material;
        }

        static void DressRim(ZMapData map, Transform parent)
        {
            // The backdrop is outside the playable grid. Its placement follows the map bounds
            // instead of carrying L07's authored positions into a differently sized conversion.
            var formations = new[]
            {
                ("L07_Volcano_3", .22f, 0f, 36f, 0f, 28f),
                ("L07_Volcano_2", .76f, 0f, 34f, 0f, 105f),
                ("L07_Volcano_1_mid", 0f, .28f, 0f, 31f, 64f),
                ("L07_Volcano_3_mid", 1f, .46f, 0f, -35f, 204f),
                ("L07_Volcano_2_mid", .30f, 1f, -39f, 0f, 267f),
                ("L07_Volcano_4", 1f, .83f, 0f, -36f, 81f)
            };
            foreach (var (name, x, y, north, west, yaw) in formations)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OpenZAssets + "Prefabs/L07/" + name + ".prefab");
                if (!prefab) throw new InvalidOperationException("Missing OpenZ rim prefab " + name);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.SetPositionAndRotation(new Vector3(x * map.width * 2 - west, 1, -y * map.height * 2 + north),
                    Quaternion.Euler(0, yaw, 0));
            }
        }

        static void VerifyViews(ZMapData map, Transform root)
        {
            var views = root.Cast<Transform>().ToDictionary(view => view.name);
            foreach (var obj in map.objects)
            {
                string name = $"{obj.Type}_{obj.x}_{obj.y}";
                if (obj.Category == "robot") name += "_0";
                if (!views.TryGetValue(name, out var view) || !view.GetComponentInChildren<Renderer>())
                    throw new InvalidOperationException("Missing rendered view " + name);
                if (obj.Type == "rock" && !view.GetComponent<RockPillar>())
                    throw new InvalidOperationException("Missing destruction stages for " + name);
                if (obj.Type == "grenades" && !view.GetComponent<GrenadeCrateV2>())
                    throw new InvalidOperationException("Missing animated grenade crate for " + name);
            }
        }
    }
}
