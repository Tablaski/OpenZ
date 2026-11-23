// OpenZ-VK (GPL-3.0).
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ.Experimental
{
    public partial class OzConversionTerrainV2
    {
        void BuildCityOutfalls(ZMapData map)
        {
            var pipeVertices = new List<Vector3>();
            var pipeIndices = new List<int>();
            var flowVertices = new List<Vector3>();
            var flowIndices = new List<int>();
            var flowUv = new List<Vector2>();
            var bridgeClearance = OzAiTerrainV4.Bridges(map);
            bool IsWater(int x, int y) => map.ClassAt(x, y) is ZClass.Water or ZClass.DeepWater;
            for (int y = 1; y < map.height - 1; y++)
            for (int x = 1; x < map.width - 1; x++)
            {
                var (dx, dy) = OzAiTerrainV5.CityOutfallDirection(map.tiles[y * map.width + x]);
                if ((dx == 0 && dy == 0) || !IsWater(x, y) || IsWater(x - dx, y - dy)) continue;
                float tileX = x * 2 + 1, tileZ = y * 2 + 1;
                if (System.Array.Exists(bridgeClearance, bridge => bridge.Clearance(tileX, tileZ) > 0)) continue;
                var intoCanal = new Vector3(dx, 0, -dy);
                var side = Vector3.Cross(Vector3.up, intoCanal);
                var mouth = new Vector3(tileX, .13f, -tileZ) - intoCanal * .42f;
                AddTube(mouth, side, intoCanal);
                AddFlow(mouth + intoCanal * .028f - Vector3.up * .18f, side, intoCanal);
            }
            if (pipeVertices.Count == 0) return;

            var metal = new Material(Shader.Find("OpenZ/TeamLit")) { name = "Weathered sewer iron" };
            metal.SetColor("_BaseColor", new Color(.28f, .185f, .135f));
            metal.SetFloat("_Metallic", .32f);
            metal.SetFloat("_Smoothness", .27f);
            metal.SetFloat("_TeamMode", 0);
            AddV5SceneryMesh("Sewer mouths", pipeVertices, pipeIndices, metal, null);

            const int size = 32;
            var texels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float streak = .46f + .23f * Mathf.Sin(x * (Mathf.PI * 6 / size));
                float run = .08f * Mathf.Sin((x + y) * (Mathf.PI * 4 / size));
                byte shade = (byte)(Mathf.Clamp01(streak + run) * 255);
                texels[y * size + x] = new Color32(shade, shade, shade, 255);
            }
            var texture = NewTexture(size, size, texels, false, "Sewer flow streaks");
            texture.wrapMode = TextureWrapMode.Repeat;
            var flow = new Material(Shader.Find("OpenZ/WaterfallSheet")) { name = "City sewer effluent" };
            flow.SetTexture("_BaseMap", texture);
            flow.SetColor("_BaseColor", new Color(.71f, .71f, .43f));
            flow.SetColor("_FoamColor", new Color(.65f, .67f, .45f));
            flow.SetFloat("_LookV2", 1);
            flow.SetFloat("_Alpha", .67f);
            flow.SetFloat("_FlowScroll", 1.1f);
            AddV5SceneryMesh("Sewer runoff", flowVertices, flowIndices, flow, flowUv);

            void AddTube(Vector3 mouth, Vector3 side, Vector3 axis)
            {
                const int segments = 12;
                int start = pipeVertices.Count;
                for (int ring = 0; ring < 4; ring++)
                for (int i = 0; i < segments; i++)
                {
                    float a = 2 * Mathf.PI * i / segments;
                    float radius = ring switch { 0 => .31f, 1 => .37f, _ => .26f };
                    float offset = ring switch { 0 => -1.14f, 1 => .02f, 2 => .035f, _ => -.49f };
                    pipeVertices.Add(mouth + axis * offset + radius * (side * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)));
                }
                for (int ring = 0; ring < 3; ring++)
                for (int i = 0; i < segments; i++)
                {
                    int a = start + ring * segments + i, b = start + ring * segments + (i + 1) % segments;
                    pipeIndices.AddRange(new[] { a, b, a + segments, b, b + segments, a + segments });
                }
            }

            void AddFlow(Vector3 start, Vector3 side, Vector3 axis)
            {
                int first = flowVertices.Count;
                for (int row = 0; row <= 5; row++)
                {
                    float t = row / 5f;
                    float width = .16f + t * .13f;
                    var point = start + axis * (.95f * t) - Vector3.up * (.28f * t * t);
                    flowVertices.Add(point - side * width);
                    flowVertices.Add(point + side * width);
                    flowUv.Add(new Vector2(0, 1 - t));
                    flowUv.Add(new Vector2(1, 1 - t));
                    if (row == 5) continue;
                    int a = first + row * 2;
                    flowIndices.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
                }
            }
        }

        void AddV5SceneryMesh(string name, List<Vector3> vertices, List<int> indices, Material material,
            List<Vector2> uv, List<Color> colors = null)
        {
            var mesh = NewMesh(name, vertices);
            mesh.SetTriangles(indices, 0);
            if (uv != null) mesh.SetUVs(0, uv);
            if (colors != null) mesh.SetColors(colors);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var visual = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)) { layer = 2 };
            visual.transform.SetParent(transform, false);
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (uv != null) renderer.shadowCastingMode = ShadowCastingMode.Off;
            generated.Add(material);
        }
    }
}
