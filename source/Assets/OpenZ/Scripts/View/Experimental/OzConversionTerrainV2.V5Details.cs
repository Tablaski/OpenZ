// OpenZ-VK (GPL-3.0).
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ.Experimental
{
    public partial class OzConversionTerrainV2
    {
        void DressV5(ZMapData map, MeshCollider groundCollider, OzAiTerrainV4.Bridge[] bridges,
            System.Func<float, float, float> rimHeight)
        {
            Physics.SyncTransforms();
            var scenery = V5Scenery(map.planet, "V5 biome details", false);
            var mesh = new SceneryMesh();
            var lamps = new List<Vector3>();
            var outfalls = new List<(Vector3, Vector3)>();
            var mist = new List<Vector3>();
            bool WetTile(int x, int y) => map.ClassAt(x, y) is ZClass.Water or ZClass.DeepWater;
            bool NearObject(int x, int y, float radius)
            {
                foreach (var obj in map.objects)
                {
                    if (obj.Category == "building" && Mathf.Abs(x - obj.x) <= 7 && Mathf.Abs(y - obj.y) <= 7) return true;
                    float dx = x - obj.x, dy = y - obj.y;
                    if (dx * dx + dy * dy < radius * radius) return true;
                }
                return false;
            }
            bool Seat(float x, float z, out Vector3 point)
            {
                point = new Vector3(x, 0, -z);
                if (System.Array.Exists(bridges, bridge => bridge.Clearance(x, z) > 0) ||
                    !groundCollider.Raycast(new Ray(point + Vector3.up * 120, Vector3.down), out var hit, 160)) return false;
                point.y = hit.point.y;
                return true;
            }

            for (int y = 2; y < map.height - 2; y++)
            for (int x = 2; x < map.width - 2; x++)
            {
                int index = y * map.width + x;
                int tile = map.tiles[index];
                float roll = ArtValue(x, y, 0);
                bool ice = map.planet == "arctic" && OzAiTerrainV5.ArcticIceArt(tile);
                bool pool = map.planet == "jungle" && WetTile(x, y);
                if (!ice && roll > (pool ? .16f : .048f)) continue;
                if (!Seat(x * 2 + .8f + ArtValue(x, y, 1) * .4f, y * 2 + .8f + ArtValue(x, y, 2) * .4f, out var p)) continue;
                float q = ArtValue(x, y, 3);
                if (ice)
                {
                    if (!WetTile(x - 1, y) && !WetTile(x + 1, y) && !WetTile(x, y - 1) && !WetTile(x, y + 1)) continue;
                    p.y = Mathf.Max(-.306f, p.y + .025f);
                    mesh.Fan(p, .40f + q * .35f, .035f, q, new Color(.37f, .61f, .72f), 6, .72f);
                    if (roll < .19f) mesh.Fan(p + new Vector3(.65f, .015f, .34f), .19f, .044f, q, new Color(.63f, .78f, .85f), 5, .8f);
                    continue;
                }
                if (pool)
                {
                    if (p.y >= -.40f || NearObject(x, y, 1.7f)) continue;
                    p.y = -.285f;
                    mesh.Lily(p, q);
                    if (roll < .019f) mist.Add(p);
                    continue;
                }
                var type = map.ClassAt(x, y);
                if (map.planet == "arctic" && type == ZClass.Road && roll < .007f)
                {
                    for (int i = 0; i < 3; i++)
                        mesh.Fan(p + new Vector3((q - .5f) * i * .15f, .018f, i * .19f), .14f, .013f, q,
                            new Color(.48f, .57f, .62f), 6, .44f);
                }
                if (type != ZClass.Ground || NearObject(x, y, 2.1f)) continue;
                switch (map.planet)
                {
                    case "desert":
                        if (roll < .003f) mesh.Palm(p, q, 3.3f + q * 1.5f);
                        else if (roll < .019f) mesh.Cacti(p, q);
                        break;
                    case "volcanic":
                        mesh.Fan(p, .17f + q * .38f, .13f + q * .4f, q, new Color(.13f, .09f, .093f), 5, .66f);
                        break;
                    case "arctic":
                        if (roll < .027f) mesh.Fan(p, .12f + q * .2f, .065f, q,
                            q < .6f ? new Color(.18f, .27f, .12f) : new Color(.39f, .18f, .22f), 6, .68f);
                        break;
                    case "city":
                        var east = WetTile(x + 1, y);
                        var west = WetTile(x - 1, y);
                        var south = WetTile(x, y + 1);
                        var north = WetTile(x, y - 1);
                        bool road = map.ClassAt(x + 1, y) == ZClass.Road || map.ClassAt(x - 1, y) == ZClass.Road ||
                            map.ClassAt(x, y + 1) == ZClass.Road || map.ClassAt(x, y - 1) == ZClass.Road;
                        if (road && roll < .026f) lamps.Add(p);
                        else if ((east || west || south || north) && roll < .019f && outfalls.Count < 14)
                        {
                            var direction = east ? Vector3.right : west ? Vector3.left : south ? Vector3.back : Vector3.forward;
                            outfalls.Add((p, direction));
                        }
                        else if (roll < .017f) mesh.Palm(p, q, 2.8f + q * 1.2f);
                        else mesh.Grate(p, q);
                        break;
                }
            }
            DressV5Rim(map, mesh, rimHeight);
            AddScenery(mesh, scenery, "V5 decorative geometry");
            foreach (var origin in lamps)
            {
                var lamp = new SceneryMesh();
                var up = origin + Vector3.up * 3.15f;
                var head = up + new Vector3(.58f, .07f, 0);
                lamp.Tube(origin, up, .065f, .042f, new Color(.15f, .17f, .16f));
                lamp.Tube(up, head, .045f, .045f, new Color(.16f, .18f, .17f));
                lamp.Fan(head, .18f, .085f, 0, new Color(.71f, .63f, .38f), 7, .53f);
                var fixture = AddScenery(lamp, scenery, "V5 crushable street lamp");
                fixture.AddComponent<OzAiCityLampV5>().Anchor = origin;
            }
            DressWetDetails(scenery, outfalls, mist);
        }

        static float ArtValue(int x, int y, int salt)
        {
            uint value = unchecked((uint)x * 374761393u + (uint)y * 668265263u + (uint)salt * 2246822519u);
            value = (value ^ value >> 13) * 1274126177u;
            return ((value ^ value >> 16) & 0xffff) / 65535f;
        }

        GameObject AddScenery(SceneryMesh source, Material material, string name)
        {
            var mesh = NewMesh(name, source.Vertices);
            mesh.SetColors(source.Colors);
            mesh.SetUVs(0, source.Uv);
            mesh.SetTriangles(source.Indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var visual = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            visual.layer = 2; // Unity Ignore Raycast. There is no collider on any V5 scenery.
            visual.transform.SetParent(transform, false);
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            return visual;
        }

        void DressV5Rim(ZMapData map, SceneryMesh mesh, System.Func<float, float, float> rimHeight)
        {
            for (int side = 0; side < 4; side++)
            {
                float length = (side < 2 ? map.width : map.height) * 2;
                for (int at = 12; at < length - 9; at += 11)
                {
                    float q = ArtValue(at, side, 6);
                    float depth = 9 + q * 9;
                    float along = at + (ArtValue(at, side, 7) - .5f) * 9;
                    var p = RimPoint(side, along, depth, map);
                    p.y = rimHeight(p.x, -p.z) - .6f;
                    switch (map.planet)
                    {
                        case "desert":
                            mesh.Fan(p, 1.8f + q * 2, 3.1f + q * 3, q, new Color(.45f, .24f, .125f), 9, .45f);
                            if (q > .5f) mesh.Cacti(p + new Vector3(.6f, .4f, -.2f), q);
                            break;
                        case "volcanic":
                            mesh.VolcanicCone(p, 2.8f + q * 3.0f, 2.8f + q * 4.5f, q);
                            break;
                        case "jungle":
                            mesh.Palm(p, q, 4.1f + q * 2);
                            mesh.Fan(p, 2.5f + q, 2.5f, q, new Color(.13f, .21f, .078f), 8, .72f);
                            break;
                        case "city":
                            mesh.Fan(p, 2.1f + q, 3.8f + q * 3, q, new Color(.29f, .26f, .21f), 5, .71f);
                            if (q < .43f) mesh.Palm(p, q, 3.7f);
                            break;
                        case "arctic":
                            if (at % 3 == 0)
                            {
                                var cave = RimPoint(side, along, 1.7f, map);
                                cave.y = rimHeight(cave.x, -cave.z) - .15f;
                                var inward = cave - RimPoint(side, along, 1, map);
                                inward.y = 0;
                                mesh.Cave(cave, inward.normalized, q);
                            }
                            break;
                    }
                }
            }
        }

        static Vector3 RimPoint(int side, float along, float distance, ZMapData map) => side switch
        {
            0 => new Vector3(along, 0, distance),
            1 => new Vector3(along, 0, -map.height * 2 - distance),
            2 => new Vector3(-distance, 0, -along),
            _ => new Vector3(map.width * 2 + distance, 0, -along)
        };
    }
}
