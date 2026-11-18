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
        const string Package = "Packages/org.openz.aimaps/";
        const string PackageDisk = "../aimaps/";
        static readonly string[] ReusableViews = { "grenades", "hut", "flag", "map_object15", "map_object16" };

        [MenuItem("OpenZ/AI Maps/Bake volcanic V2 maps")]
        public static void All()
        {
            foreach (var entry in OzAiMapCatalog.VolcanicV2) Bake(entry.Id);
        }

        [MenuItem("OpenZ/AI Maps/Bake V2 reference maps")]
        public static void ReferenceMaps()
        {
            foreach (var entry in OzAiMapCatalog.V2) Bake(entry.Id);
        }

        [MenuItem("OpenZ/AI Maps/Bake Chilly Willy")]
        public static void Arctic() => Bake(OzAiMapCatalog.ChillyWillyV2.Id);

        [MenuItem("OpenZ/AI Maps/Upgrade the existing catalog to V3")]
        public static void UpgradeCatalog()
        {
            OzArcticArt.Ensure();
            foreach (var group in OzAiMapCatalog.Playable.GroupBy(entry => entry.World))
            {
                var profile = Profile.For(group.Key);
                var reference = EditorSceneManager.OpenScene(OpenZAssets + "Scenes/" + profile.Scene + ".unity");
                var scenery = ExtractScenery(group.Key, reference);
                var templates = reference.GetRootGameObjects().First(root => root.name == "ZodObjects").transform;
                var jungleRocks = group.Key == "jungle"
                    ? templates.Cast<Transform>().Where(t => t.GetComponent<RockPillar>()).ToArray() : Array.Empty<Transform>();
                foreach (var entry in group)
                {
                    var scene = EditorSceneManager.OpenScene(entry.ScenePath, OpenSceneMode.Additive);
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
                    UpgradeScene(entry, scene, reference, scenery, jungleRocks);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + entry.ScenePath);
                    EditorSceneManager.CloseScene(scene, true);
                    Debug.Log("[Catalog V3] " + entry.Id + " | " + entry.File);
                }
            }
        }

        static void UpgradeScene(OzAiMap entry, UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.Scene reference, Scenery[] scenery, Transform[] jungleRocks)
        {
            var roots = scene.GetRootGameObjects();
            var environment = roots.Single(root => root.name == "Environment");
            var terrain = environment.GetComponent<OzConversionTerrainV2>();
            if (terrain)
            {
                terrain.bakeWithPaletteV3 = true;
                return;
            }

            var asset = Resources.Load<TextAsset>("OpenZ/Maps/" + entry.Id + "_grid");
            var map = ZMapData.FromJson(asset.text);
            var profile = Profile.For(map.planet);
            bool arctic = map.planet == "arctic";
            var oldTerrain = environment.GetComponent<OzLegacyAiTerrain>();
            if (!oldTerrain) throw new InvalidOperationException("Missing original terrain in " + entry.Id);
            Object.DestroyImmediate(oldTerrain);
            terrain = environment.AddComponent<OzConversionTerrainV2>();
            terrain.mapId = entry.Id;
            terrain.bakeWithPaletteV3 = true;
            Material Surface(string name) => arctic ? OzArcticArt.Mat(name) : Material("Materials/Env/" + name + ".mat");
            terrain.ground = Surface(profile.Ground);
            terrain.cliff = Surface(profile.Cliff);
            terrain.pit = Surface(profile.Bed);
            terrain.water = Surface(profile.Water);
            terrain.Build(map, false);
            var collider = environment.GetComponentInChildren<MeshCollider>();
            Physics.SyncTransforms();

            var views = roots.Single(root => root.name == "ZodObjects").transform;
            foreach (Transform view in views)
            {
                var pos = view.position;
                if (collider.Raycast(new Ray(pos + Vector3.up * 200, Vector3.down), out var hit, 400))
                    view.position = new Vector3(pos.x, hit.point.y, pos.z);
            }
            var rocks = views.Cast<Transform>().Where(t => t.name.StartsWith("rock_", StringComparison.Ordinal)).ToArray();
            if (arctic || map.planet == "jungle")
            {
                foreach (var rock in rocks)
                {
                    if (arctic)
                    {
                        foreach (Transform part in rock.Cast<Transform>().ToArray()) Object.DestroyImmediate(part.gameObject);
                        foreach (var component in rock.GetComponents<Component>())
                            if (!(component is Transform)) Object.DestroyImmediate(component);
                        rock.localScale = Vector3.one;
                    }
                    else
                    {
                        if (jungleRocks.Length == 0) throw new InvalidOperationException("Missing OpenZ jungle rock kit.");
                        int variant = (int)(Math.Abs(rock.position.x * 31 + rock.position.z * 47) % jungleRocks.Length);
                        var copy = Object.Instantiate(jungleRocks[variant].gameObject, views);
                        copy.name = rock.name;
                        copy.transform.position = rock.position;
                        Object.DestroyImmediate(rock.gameObject);
                    }
                }
                if (arctic) OzArcticArt.DressRocks(views);
            }
            else
            {
                var world = ZWorld.Create(Resources.Load<TextAsset>("OpenZ/default_settings").text, asset.text, 19);
                OpenZRocks.Build(world, views, collider, profile.RockKit, Material("Models/RocksZ/M_RockZ_" + profile.RockKit + ".mat"));
            }
            if (map.planet == "volcanic")
            {
                OzL07Scene.ApplyRockHeights(views);
                DressRim(map, environment.transform);
            }
            else if (arctic)
            {
                OzArcticArt.Snowfall(environment.transform, map.width, map.height);
                var game = roots.SelectMany(root => root.GetComponentsInChildren<ZGame>()).Single();
                game.unitPrefabs = game.unitPrefabs.Concat(OzArcticArt.BridgeKit()).ToArray();
                var day = roots.SelectMany(root => root.GetComponentsInChildren<DayNight>()).Single();
                day.warmth = .14f;
                day.arcticLight = true;
            }
            else
            {
                if (map.planet == "city") AddCityOutskirts(reference, map, environment.transform);
                DressLand(map, environment.transform, collider, scenery);
            }
            var atmosphere = roots.SelectMany(root => root.GetComponentsInChildren<Atmosphere>()).Single();
            atmosphere.groundMaterial = null;
            if (arctic) atmosphere.preset = Atmosphere.Preset.Arctic;
            VerifyViews(map, views);
            Object.DestroyImmediate(collider.gameObject);
            terrain.Clear();
        }

        // Custom V2 and V3 grids can be baked without adding them to either game menu.
        public static void Bake(string id)
        {
            if (string.IsNullOrEmpty(id) || !System.Text.RegularExpressions.Regex.IsMatch(id, "^SM[A-Za-z0-9_]+V[23]$"))
                throw new ArgumentException("Expected a separate SM...V2 or SM...V3 map ID.", nameof(id));
            var asset = Resources.Load<TextAsset>("OpenZ/Maps/" + id + "_grid");
            if (!asset) throw new InvalidOperationException("Missing converted grid " + id);
            var map = ZMapData.FromJson(asset.text);
            var profile = Profile.For(map.planet);
            bool arctic = map.planet == "arctic";
            bool paletteV3 = id.EndsWith("V3", StringComparison.Ordinal);
            if (arctic) OzArcticArt.Ensure();
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
            terrain.bakeWithPaletteV3 = paletteV3;
            Material Surface(string name) => arctic ? OzArcticArt.Mat(name) : Material("Materials/Env/" + name + ".mat");
            terrain.ground = Surface(profile.Ground);
            terrain.cliff = Surface(profile.Cliff);
            terrain.pit = Surface(profile.Bed);
            terrain.water = Surface(profile.Water);
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
            var rocks = arctic ? OzArcticArt.DressRocks(views.transform) + " snow-capped Arctic rock stages"
                : jungle ? views.GetComponentsInChildren<RockPillar>(true).Length + " textured jungle rock slices"
                : OpenZRocks.Build(world, views.transform, collider, profile.RockKit,
                    Material("Models/RocksZ/M_RockZ_" + profile.RockKit + ".mat"));
            if (map.planet == "volcanic") rocks += OzL07Scene.ApplyRockHeights(views.transform);
            TowerGunSeat.SeatAll(views.transform, bakePlinths: false);
            if (map.planet == "volcanic") DressRim(map, environment.transform);
            else if (arctic) OzArcticArt.Snowfall(environment.transform, map.width, map.height);
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
            var dayNight = Object.FindFirstObjectByType<DayNight>();
            dayNight.lampRoot = views.transform;
            if (arctic)
            {
                game.unitPrefabs = game.unitPrefabs.Concat(OzArcticArt.BridgeKit()).ToArray();
                atmosphere.preset = Atmosphere.Preset.Arctic;
                dayNight.warmth = .14f;
                dayNight.arcticLight = true;
            }

            VerifyViews(map, views.transform);
            var preview = OzLevelInfo.Bake(world, id, true);
            File.WriteAllBytes(PackageDisk + "Resources/OpenZ/AiMaps/Previews/" + id + ".png", preview.EncodeToPNG());
            Object.DestroyImmediate(preview);
            Object.DestroyImmediate(collider.gameObject);
            terrain.Clear();

            string scenePath = Package + "Scenes/" + id + "_Converted.unity";
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new IOException("Could not save " + scenePath);
            AssetDatabase.Refresh();
            if (OzAiMapCatalog.Find(id) != null && !EditorBuildSettings.scenes.Any(s => s.path == scenePath))
            {
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(scenePath, true)).ToArray();
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"[AI Maps] {id}: {map.width} x {map.height}, {map.objects.Count} native records, {rocks}");
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
            var views = root.Cast<Transform>().ToLookup(view => view.name);
            foreach (var obj in map.objects)
            {
                string name = $"{obj.Type}_{obj.x}_{obj.y}";
                if (obj.Category == "robot") name += "_0";
                var view = views[name].FirstOrDefault(item => item.GetComponentInChildren<Renderer>());
                if (!view)
                    throw new InvalidOperationException("Missing rendered view " + name);
                if (obj.Type == "rock" && !view.GetComponent<RockPillar>())
                    throw new InvalidOperationException("Missing destruction stages for " + name);
                if (obj.Type == "grenades" && !view.GetComponent<GrenadeCrateV2>())
                    throw new InvalidOperationException("Missing animated grenade crate for " + name);
            }
        }
    }
}
