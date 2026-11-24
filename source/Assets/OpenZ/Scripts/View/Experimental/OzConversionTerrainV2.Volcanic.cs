// OpenZ-VK (GPL-3.0).
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.Experimental
{
    public partial class OzConversionTerrainV2
    {
        void BuildVolcanicVents(ZMapData map, Func<float, float, float> baseHeight, OzAiTerrainV4.Bridge[] bridges)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var colors = new List<Color>();
            var placed = new List<Vector2>();
            bool RimRock(int x, int z) => x < 0 || z < 0 || x >= map.width || z >= map.height ||
                Solid(map, z * map.width + x);
            bool SafeVent(Vector2 point, float radius)
            {
                foreach (Vector2 offset in new[] { Vector2.zero, Vector2.right, Vector2.left, Vector2.up, Vector2.down })
                {
                    Vector2 tip = point + offset * radius;
                    if (!RimRock(Mathf.FloorToInt(tip.x / 2), Mathf.FloorToInt(tip.y / 2)) ||
                        Array.Exists(bridges, bridge => bridge.Clearance(tip.x, tip.y) > 0)) return false;
                }
                return true;
            }
            for (int z = 0; z < map.height; z++)
            for (int x = 0; x < map.width; x++)
            {
                if (Mathf.Min(Mathf.Min(x, z), Mathf.Min(map.width - 1 - x, map.height - 1 - z)) > 5) continue;
                int index = z * map.width + x;
                if (map.tiles[index] is not (260 or 263 or 267 or 334 or 354 or 389 or 406 or 425 or 448) || !Solid(map, index)) continue;
                if (!RimRock(x - 1, z) || !RimRock(x + 1, z) || !RimRock(x, z - 1) || !RimRock(x, z + 1)) continue;
                float q = Mathf.Repeat(Mathf.Sin(x * 12.9898f + z * 78.233f) * 43758.5453f, 1);
                if (q < .13f) continue;
                var p = new Vector2(x * 2 + 1, z * 2 + 1);
                p += new Vector2(Mathf.Sin(q * 61), Mathf.Cos(q * 42)) * 1.3f;
                float radius = 1.40f + .83f * q;
                if (placed.Exists(previous => (previous - p).sqrMagnitude < 135) || !SafeVent(p, radius)) continue;
                AddVent(p, radius, 1.1f + 1.24f * q, q);
                int neighbours = q > .50f ? 4 : 3;
                for (int n = 0; n < neighbours; n++)
                {
                    float angle = q * 6.28f + n * 2.37f;
                    float satelliteSize = .54f + .54f * Mathf.Repeat(q * 9 + n * .39f, 1);
                    var satellite = p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                        (radius + .49f + n * .4f);
                    if (SafeVent(satellite, satelliteSize))
                        AddVent(satellite, satelliteSize, satelliteSize * .83f, q + n * .17f);
                }
                placed.Add(p);
            }
            float width = map.width * 2, depth = map.height * 2;
            var landmarks = new List<Vector2>();
            foreach (Transform part in transform)
                if (part.name.StartsWith("L07_Volcano_",StringComparison.Ordinal)) landmarks.Add(new Vector2(part.position.x,-part.position.z));
            for (int side = 0; side < 4; side++)
            {
                float length = side < 2 ? width : depth;
                for (float along = 7; along < length - 7;)
                {
                    float q = Mathf.PerlinNoise(along * .173f + 8, side * 2.77f + 12);
                    float across = 8.2f + 7.4f * q;
                    var p = side switch
                    {
                        0 => new Vector2(along,-across),
                        1 => new Vector2(along,depth + across),
                        2 => new Vector2(-across,along),
                        _ => new Vector2(width + across,along)
                    };
                    if (!landmarks.Exists(point => (point - p).sqrMagnitude < 33))
                    {
                        float prominence = Mathf.PerlinNoise(along * .081f, side * 3 + 9);
                        float height = .9f + 2.95f * prominence * prominence;
                        float radius = .95f + height * .46f;
                        AddVent(p,radius,height,q);
                        var axis = side < 2 ? Vector2.right : Vector2.up;
                        var normal = new Vector2(-axis.y,axis.x);
                        AddVent(p - axis * (radius + 1) + normal * (q - .5f) * 2,.64f + q * .55f,.49f + q * .83f,q + .31f);
                        if (q > .42f)
                            AddVent(p + axis * (radius + 1.1f) - normal * (q - .38f) * 2,.52f + q * .53f,.49f + q * .62f,q + .66f);
                    }
                    along += 7.2f + q * 5.4f;
                }
            }
            if (vertices.Count == 0) return;
            var basalt = new Material(Shader.Find("OpenZ/TeamLit")) { name = "Scoria vent rock" };
            basalt.SetFloat("_UseVertexColor", 1);
            basalt.SetFloat("_TeamMode", 0);
            basalt.SetFloat("_Smoothness", .21f);
            basalt.SetFloat("_Triplanar", 1);
            basalt.SetFloat("_OzAiRockV5", 2);
            AddV5SceneryMesh("Volcanic rim vents", vertices, indices, basalt, null, colors);
            Debug.Log($"[AI map terrain] {mapId}: {vertices.Count / 80} small and medium rim cones");

            void AddVent(Vector2 center, float radius, float height, float phase)
            {
                const int sides = 16;
                int first = vertices.Count;
                float ground = baseHeight(center.x, center.y);
                for (int ring = 0; ring < 5; ring++)
                for (int i = 0; i < sides; i++)
                {
                    float a = 2 * Mathf.PI * i / sides + phase * 2;
                    float uneven = Mathf.Sin(3 * a + phase) * .13f + Mathf.Cos(5 * a - phase) * .07f;
                    float reach = radius * (ring switch { 0 => 1, 1 => .69f, 2 => .37f, 3 => .28f, _ => .10f });
                    var point = center + reach * (1 + uneven) * new Vector2(Mathf.Cos(a), Mathf.Sin(a) * .83f);
                    float y = ring == 0 ? baseHeight(point.x, point.y) - .18f : ground + height *
                        (ring switch { 1 => .34f, 2 => 1 + uneven, 3 => .85f + uneven, _ => .53f });
                    vertices.Add(new Vector3(point.x, y, -point.y));
                    Color tone = ring switch
                    {
                        0 => new Color(.035f, .030f, .033f),
                        1 => new Color(.049f, .039f, .043f),
                        2 => new Color(.083f, .067f, .064f),
                        3 => new Color(.054f, .029f, .023f),
                        _ => new Color(.030f, .015f, .013f)
                    };
                    colors.Add(tone * (1 + uneven * 1.55f));
                }
                for (int ring = 0; ring < 4; ring++)
                for (int i = 0; i < sides; i++)
                {
                    int a = first + ring * sides + i, b = first + ring * sides + (i + 1) % sides;
                    indices.AddRange(new[] { a, b, a + sides, b, b + sides, a + sides });
                }
            }
        }

        void SeatVolcanicLandmarks(ZMapData map, Func<float, float, float> terrainHeight)
        {
            float width = 2 * map.width, depth = 2 * map.height;
            foreach (Transform landmark in transform)
            {
                if (!landmark.name.StartsWith("L07_Volcano_", StringComparison.Ordinal)) continue;
                var renderers = landmark.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length == 0 || landmark.GetComponentInChildren<Collider>()) continue;
                var old = landmark.position;
                var location = new Vector2(old.x, -old.z);
                float west = -location.x, east = location.x - width, north = -location.y, south = location.y - depth;
                float outside = Mathf.Max(Mathf.Max(west,east),Mathf.Max(north,south));
                if (outside <= 0) continue;
                const float crest = 12f;
                location.x = Mathf.Clamp(location.x,12,width-12);
                location.y = Mathf.Clamp(location.y,12,depth-12);
                if (outside == west) location.x = -crest;
                else if (outside == east) location.x = width + crest;
                else location.y = outside == north ? -crest : depth + crest;
                landmark.localScale *= .62f;
                landmark.position = new Vector3(location.x, 0, -location.y);
                float bottom = float.PositiveInfinity;
                foreach (var renderer in renderers) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
                landmark.position += Vector3.up * (terrainHeight(location.x, location.y) - bottom - .5f);
            }
        }
    }
}
