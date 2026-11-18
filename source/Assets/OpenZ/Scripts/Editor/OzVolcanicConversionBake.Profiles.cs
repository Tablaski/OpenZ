// OpenZ-VK (GPL-3.0).
using System;
using System.Collections.Generic;
using System.Linq;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OpenZ.EditorTools
{
    public static partial class OzVolcanicConversionBake
    {
        sealed class Profile
        {
            public readonly string Level, Scene, Ground, Cliff, Bed, Water, RockKit;

            Profile(string scene, string ground, string cliff, string bed, string water, string rockKit)
            {
                Scene = scene;
                Level = scene.Split('_')[0];
                Ground = ground;
                Cliff = cliff;
                Bed = bed;
                Water = water;
                RockKit = rockKit;
            }

            public static Profile For(string world) => world switch
            {
                "jungle" => new Profile("L16_Jungle", "M_L16_Ground", "M_Cliff_JungleRock",
                    "M_L16_RimBed", "M_Water_Swamp", null),
                "city" => new Profile("L17_CarPark", "M_L17_Ground", "M_CliffV3_Rock_L17",
                    "M_CliffV3_Concrete_L17", "M_Water_Harbour", "City"),
                "desert" => new Profile("L02_Desert", "M_L02_Ground", "M_CliffV3_Rock_L02",
                    "M_Cliff_Sandstone", "WaterV4/M_Water_v3_backup", "Desert"),
                "volcanic" => new Profile("L07_Volcanic", "M_L07_Ground", "M_Cliff_VolcanicRock",
                    "M_L07_PitBed", "M_Water", "Volcanic"),
                "arctic" => new Profile("L02_Desert", "Snow", "Slate", "Shoal", "Meltwater", null),
                _ => throw new InvalidOperationException("No V2 asset mapping for " + world)
            };
        }

        static Dictionary<string, Transform[]> SelectTemplates(ZMapData map, Transform root)
        {
            var types = map.planet == "volcanic" ? ReusableViews
                : map.objects.Where(o => o.Category == "map_item" && (o.Type != "rock" || map.planet == "jungle"))
                    .Select(o => o.Type).Distinct().ToArray();
            var views = root.Cast<Transform>().ToArray();
            return types.Select(type => (type, views: views.Where(t =>
                    t.name.StartsWith(type + "_", StringComparison.Ordinal) &&
                    (type != "rock" || t.TryGetComponent<RockPillar>(out var rock) && rock.pillar && rock.damaged))
                .OrderBy(t => t.name, StringComparer.Ordinal).ToArray()))
                .Where(t => t.views.Length > 0).ToDictionary(t => t.type, t => t.views);
        }

        static Transform PickTemplate(Transform[] templates, ZMapData.Obj obj)
        {
            uint seed = unchecked((uint)(obj.x * 73856093 ^ obj.y * 19349663 ^ obj.oid * 83492791));
            return templates[seed % (uint)templates.Length];
        }

        // Reuse only visual mesh data from the authored scenes. Decorative copies do not add
        // colliders or static objects to the simulation.
        sealed class Scenery
        {
            public Mesh Mesh;
            public Material[] Materials;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        static Scenery[] ExtractScenery(string world, Scene scene)
        {
            if (world == "jungle")
            {
                var trees = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>())
                    .Where(t => t.name.StartsWith("PLT_Tree_group_", StringComparison.Ordinal))
                    .OrderBy(t => t.name, StringComparer.Ordinal);
                var samples = trees.Select(t => t.GetComponentsInChildren<MeshFilter>()
                        .FirstOrDefault(f => f.sharedMesh && f.TryGetComponent<MeshRenderer>(out _)))
                    .Where(f => f).GroupBy(f => f.sharedMesh).Select(g => g.First()).Take(12);
                return samples.Select(f => new Scenery
                {
                    Mesh = f.sharedMesh, Materials = f.GetComponent<MeshRenderer>().sharedMaterials,
                    Rotation = f.transform.rotation, Scale = f.transform.lossyScale
                }).ToArray();
            }
            if (world == "city")
                return new[] { "DV2_C01", "DV2_C04", "DV2_C06", "DV2_C09" }.Select(DecorPrefab).ToArray();
            if (world == "desert")
                return new[] { "DV2_SD1_PalmA", "DV2_SD1_PalmB" }.Select(DecorPrefab).ToArray();
            return Array.Empty<Scenery>();
        }

        static Scenery DecorPrefab(string key)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OpenZAssets + "Resources/OpenZ/DetailsV2/" + key + ".prefab");
            if (!prefab) throw new InvalidOperationException("Missing OpenZ scenery prefab " + key);
            var part = prefab.GetComponentsInChildren<MeshFilter>()
                .First(f => f.sharedMesh && f.TryGetComponent<MeshRenderer>(out _));
            return new Scenery
            {
                Mesh = part.sharedMesh, Materials = part.GetComponent<MeshRenderer>().sharedMaterials,
                Rotation = part.transform.rotation, Scale = part.transform.lossyScale
            };
        }

        static void AddCityOutskirts(Scene scene, ZMapData map, Transform parent)
        {
            // These meshes belong to the outer L17 set. Normalize their positions to the current
            // board size while keeping the modular ruin and quay art outside the play cells.
            var exterior = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true))
                .Where(f => f.name == "OUT3_Ruins" || f.name == "OUT3_Quays");
            var scale = new Vector3(map.width / 110f, 1, map.height / 128f);
            foreach (var source in exterior)
            {
                var renderer = source.GetComponent<MeshRenderer>();
                if (!source.sharedMesh || !renderer) continue;
                var go = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(Vector3.Scale(source.transform.position, scale), source.transform.rotation);
                go.transform.localScale = Vector3.Scale(source.transform.lossyScale, scale);
                go.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                go.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
        }

        static void DressLand(ZMapData map, Transform root, Collider surface, Scenery[] kit)
        {
            if (kit.Length == 0) throw new InvalidOperationException("OpenZ " + map.planet + " scenery kit is empty");
            var backdrop = new GameObject("Biome scenery").transform;
            backdrop.SetParent(root, false);
            var random = new System.Random(5701);
            void Put(float x, float z, float yaw, float scale, Scenery art)
            {
                var position = new Vector3(x, 0, -z);
                if (!surface.Raycast(new Ray(position + Vector3.up * 200, Vector3.down), out var hit, 400)) return;
                var go = new GameObject(art.Mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(backdrop, false);
                go.transform.SetPositionAndRotation(hit.point, Quaternion.Euler(0, yaw, 0) * art.Rotation);
                go.transform.localScale = art.Scale * scale;
                go.GetComponent<MeshFilter>().sharedMesh = art.Mesh;
                go.GetComponent<MeshRenderer>().sharedMaterials = art.Materials;
                var bounds = go.GetComponent<MeshRenderer>().bounds;
                go.transform.position += Vector3.up * (hit.point.y - bounds.min.y - .12f);
            }

            bool jungle = map.planet == "jungle", city = map.planet == "city";
            var positions = new List<Vector2>();
            bool NearObject(int x, int y, int distance) =>
                map.objects.Any(o => Mathf.Abs(o.x - x) < distance && Mathf.Abs(o.y - y) < distance);
            for (int y = 3; y < map.height - 3; y += 2)
            for (int x = 3; x < map.width - 3; x += 2)
            {
                var p = new Vector2(x, y);
                var tile = map.ClassAt(x, y);
                bool sandyBank = map.ClassAt(x + 2, y) == ZClass.Water || map.ClassAt(x, y + 2) == ZClass.Water;
                bool roadEdge = map.ClassAt(x + 1, y) == ZClass.Road || map.ClassAt(x, y + 1) == ZClass.Road;
                bool cliffTop = tile == ZClass.Impassable && map.ClassAt(x - 1, y) == ZClass.Impassable
                    && map.ClassAt(x, y - 1) == ZClass.Impassable;
                bool habitat = jungle ? cliffTop : tile == ZClass.Ground && (city ? roadEdge : sandyBank);
                if (!habitat || NearObject(x, y, jungle ? 3 : 5) || positions.Any(prev => (prev - p).sqrMagnitude < (jungle ? 14 : 49))) continue;
                if (random.NextDouble() > (jungle ? .72 : .48)) continue;
                positions.Add(p);
                Put(x * 2 + 1, y * 2 + 1, city ? roadEdge && map.ClassAt(x + 1, y) == ZClass.Road ? 90 : 0 : random.Next(360),
                    jungle ? .65f + (float)random.NextDouble() * .3f : .95f, kit[random.Next(kit.Length)]);
            }

            if (!jungle) return;
            // An irregular forest canopy extends around the playable rim.
            for (int side = 0; side < 4; side++)
            {
                float length = (side % 2 == 0 ? map.width : map.height) * 2;
                for (float along = 4; along < length; along += 4 + (float)random.NextDouble() * 5)
                {
                    float offset = 6 + (float)random.NextDouble() * 12;
                    float x = side % 2 == 0 ? along : side == 1 ? map.width * 2 + offset : -offset;
                    float z = side % 2 != 0 ? along : side == 0 ? -offset : map.height * 2 + offset;
                    Put(x, z, random.Next(360), .75f + (float)random.NextDouble() * .4f, kit[random.Next(kit.Length)]);
                }
            }
        }
    }
}
