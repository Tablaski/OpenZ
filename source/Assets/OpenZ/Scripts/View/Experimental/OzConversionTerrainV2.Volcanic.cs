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
            for (int z = 0; z < map.height; z++)
            for (int x = 0; x < map.width; x++)
            {
                if (Mathf.Min(Mathf.Min(x, z), Mathf.Min(map.width - 1 - x, map.height - 1 - z)) > 5) continue;
                int index = z * map.width + x;
                if (map.tiles[index] is not (260 or 263 or 267 or 334 or 354 or 389 or 406 or 425 or 448) || !Solid(map, index)) continue;
                if (!RimRock(x - 1, z) || !RimRock(x + 1, z) || !RimRock(x, z - 1) || !RimRock(x, z + 1)) continue;
                var p = new Vector2(x * 2 + 1, z * 2 + 1);
                if (placed.Exists(previous => (previous - p).sqrMagnitude < 130) ||
                    Array.Exists(bridges, bridge => bridge.Clearance(p.x, p.y) > 0)) continue;
                float q = Mathf.PerlinNoise(x * .274f + 12, z * .194f + 31);
                AddVent(p, 1.45f + 1.05f * q, 1.2f + 1.4f * q, q);
                placed.Add(p);
            }
            if (vertices.Count == 0) return;
            var basalt = new Material(Shader.Find("OpenZ/TeamLit")) { name = "Scoria vent rock" };
            basalt.SetFloat("_UseVertexColor", 1);
            basalt.SetFloat("_TeamMode", 0);
            basalt.SetFloat("_Smoothness", .21f);
            AddV5SceneryMesh("Volcanic rim vents", vertices, indices, basalt, null, colors);

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
                        0 => new Color(.061f, .052f, .057f),
                        1 => new Color(.081f, .067f, .068f),
                        2 => new Color(.15f, .127f, .119f),
                        3 => new Color(.106f, .062f, .047f),
                        _ => new Color(.061f, .038f, .035f)
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
    }
}
