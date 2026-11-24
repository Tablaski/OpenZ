// OpenZ-VK (GPL-3.0).
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ.Experimental
{
    public partial class OzConversionTerrainV2
    {
        void HideCityV5ExteriorProps()
        {
            foreach (Transform part in transform)
                if (part.name == "OUT3_Ruins" || part.name == "OUT3_Quays") part.gameObject.SetActive(false);
        }

        Mesh CityBorderSurface(ZMapData map, List<Vector3> vertices, List<Vector2> uv, List<int>[] channels)
        {
            var border = new List<int>();
            var mesh = NewMesh("City terrain with concrete perimeter",vertices);
            mesh.SetUVs(0,uv);
            mesh.subMeshCount = channels.Length + 1;
            bool Outside(Vector3 p) => p.x <= 0 || p.x >= map.width * 2 || p.z >= 0 || p.z <= -map.height * 2;
            for (int channel = 0; channel < channels.Length; channel++)
            {
                var inside = new List<int>();
                var triangles = channels[channel];
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    var group = Outside(vertices[a]) && Outside(vertices[b]) && Outside(vertices[c]) ? border : inside;
                    group.Add(a); group.Add(b); group.Add(c);
                }
                mesh.SetTriangles(inside,channel,false);
            }
            mesh.SetTriangles(border,channels.Length,false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void BuildCityBorderRuins(ZMapData map, System.Func<float,float,float> height)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var colors = new List<Color>();
            float width = 2 * map.width, depth = 2 * map.height;
            for (int side = 0; side < 4; side++)
            {
                float length = side < 2 ? width : depth;
                for (float along = 13; along < length - 15;)
                {
                    float variation = Mathf.PerlinNoise(along * .174f + 5, side * 4.7f + 11);
                    int bays = variation > .49f ? 4 : 3;
                    float span = bays * 2.7f;
                    float setback = 7.9f + variation * 4.4f;
                    float back = setback + 6.1f;
                    float end = along + span;
                    float baseY = TerrainY(along + span * .45f,setback + 3);
                    var masonry = Color.Lerp(new Color(.20f,.185f,.17f),new Color(.37f,.345f,.305f),variation);
                    var concrete = masonry * .82f;
                    var interior = masonry * .53f;
                    Block(along,end,setback - 1.7f,back + 1,baseY - 2.6f,baseY + .3f,concrete);

                    int floors = variation > .60f ? 3 : 2;
                    for (int level = 0; level < floors; level++)
                    {
                        float y = baseY + level * 2.6f;
                        float brokenEnd = end - level * (variation > .51f ? .7f : 1.6f);
                        Block(along,brokenEnd,setback,back,y,y + .22f,interior);
                        Block(along,brokenEnd,setback,setback + .45f,y + .22f,y + .76f,masonry);
                        for (int bay = 0; bay <= bays; bay++)
                        {
                            float start = along + bay * 2.7f;
                            if (start + .47f > brokenEnd) break;
                            float top = y + 2.59f;
                            if (level == floors - 1)
                                top -= Mathf.PerlinNoise(start * .84f,side + 7.1f) * 1.4f;
                            Block(start,start + .47f,setback,setback + .48f,y + .6f,top,masonry);
                            if (level < floors - 1 && bay < bays && start + 2.7f <= brokenEnd)
                                Block(start,start + 2.7f,setback,setback + .4f,y + 2.23f,y + 2.58f,masonry);
                        }
                        Block(along,along + .42f,setback,back - level * .65f,y + .22f,y + 2.45f,concrete);
                        Block(along,brokenEnd - 1.1f,back - .51f,back,y + .22f,y + (level == floors - 1 ? 1.15f : 2.48f),interior);
                    }
                    along += span + 2.6f + variation * 4.3f;
                }

                (float x,float z) At(float along, float offset) => side switch
                {
                    0 => (along,-offset), 1 => (along,depth + offset), 2 => (-offset,along), _ => (width + offset,along)
                };
                float TerrainY(float along,float offset)
                {
                    var p = At(along,offset);
                    return height(p.x,p.z);
                }
                void Block(float start,float end,float near,float far,float bottom,float top,Color color)
                {
                    var a = At(start,near);
                    var b = At(end,far);
                    float x0 = Mathf.Min(a.x,b.x), x1 = Mathf.Max(a.x,b.x);
                    float z0 = -Mathf.Max(a.z,b.z), z1 = -Mathf.Min(a.z,b.z);
                    var p = new Vector3(x0,bottom,z0); var q = new Vector3(x1,bottom,z0);
                    var r = new Vector3(x1,bottom,z1); var s = new Vector3(x0,bottom,z1);
                    var up = Vector3.up * (top - bottom);
                    Face(p,q,r,s,color * .62f);
                    Face(p + up,s + up,r + up,q + up,color * 1.04f);
                    Face(p,p + up,q + up,q,color);
                    Face(q,q + up,r + up,r,color * .94f);
                    Face(r,r + up,s + up,s,color * .83f);
                    Face(s,s + up,p + up,p,color * .91f);
                }
                void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
                {
                    int first = vertices.Count;
                    vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                    colors.Add(color); colors.Add(color); colors.Add(color); colors.Add(color);
                    indices.AddRange(new[] { first,first + 1,first + 2,first,first + 2,first + 3 });
                }
            }
            var facade = new Material(pit) { name = "City perimeter concrete ruins" };
            facade.SetFloat("_UseVertexColor",1);
            facade.SetFloat("_Triplanar",1);
            facade.SetFloat("_TriScale",.28f);
            facade.SetFloat("_OzAiRockV5",3);
            AddV5SceneryMesh("City perimeter ruins",vertices,indices,facade,null,colors);
        }

        void BuildCityOutfalls(ZMapData map)
        {
            var pipeVertices = new List<Vector3>();
            var pipeIndices = new List<int>();
            var flowVertices = new List<Vector3>();
            var flowIndices = new List<int>();
            var flowUv = new List<Vector2>();
            var flowColors = new List<Color>();
            var collectors = new Dictionary<int, List<Vector3>>();
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
                var mouth = new Vector3(tileX, .18f, -tileZ) - intoCanal * .48f;
                AddTube(mouth, side, intoCanal);
                if (dy > 0 && y > 1 && map.tiles[(y - 2) * map.width + x] == 326)
                {
                    var tee = new Vector3(tileX, .72f, -tileZ + 4);
                    var elbow = new Vector3(tileX, .62f, mouth.z + 1.4f);
                    if (!collectors.TryGetValue(y, out var run)) collectors.Add(y, run = new List<Vector3>());
                    run.Add(tee);
                    AddStraightPipe(tee, elbow, .145f);
                    AddStraightPipe(elbow, mouth - intoCanal * .84f, .17f);
                    AddStraightPipe(tee - Vector3.up * .78f, tee, .09f);
                }
                else
                {
                    var wall = mouth - intoCanal * 1.05f + Vector3.up * .16f;
                    AddStraightPipe(wall - Vector3.up * .55f, wall, .43f);
                }
                AddFlow(mouth + intoCanal * .025f - Vector3.up * .164f, side, intoCanal);
            }
            if (pipeVertices.Count == 0) return;
            foreach (var run in collectors.Values)
                AddStraightPipe(run[0] - Vector3.right * .9f, run[run.Count - 1] + Vector3.right * .9f, .19f);

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
            flow.SetColor("_BaseColor", new Color(.57f, .60f, .43f));
            flow.SetColor("_FoamColor", new Color(.66f, .69f, .51f));
            flow.SetFloat("_FoamStreaks", .34f);
            flow.SetFloat("_Alpha", .78f);
            flow.SetFloat("_EdgeBright", .13f);
            flow.SetFloat("_FlowScroll", 1.1f);
            AddV5SceneryMesh("Sewer runoff", flowVertices, flowIndices, flow, flowUv, flowColors);

            void AddStraightPipe(Vector3 a, Vector3 b, float radius)
            {
                const int sides = 12;
                var direction = (b - a).normalized;
                var side = Vector3.Cross(direction, Mathf.Abs(direction.y) > .8f ? Vector3.forward : Vector3.up).normalized;
                var up = Vector3.Cross(direction, side);
                int first = pipeVertices.Count;
                for (int i = 0; i < sides; i++)
                {
                    float angle = 2 * Mathf.PI * i / sides;
                    var edge = radius * (side * Mathf.Cos(angle) + up * Mathf.Sin(angle));
                    pipeVertices.Add(a + edge);
                    pipeVertices.Add(b + edge);
                }
                for (int i = 0; i < sides; i++)
                {
                    int u = first + i * 2, v = first + (i + 1) % sides * 2;
                    pipeIndices.AddRange(new[] { u, v, u + 1, v, v + 1, u + 1 });
                }
            }

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
                for (int row = 0; row <= 10; row++)
                {
                    float t = row / 10f;
                    float plume = Mathf.SmoothStep(0, 1, (t - .64f) / .36f);
                    float width = .081f + t * .044f + plume * .23f;
                    var point = start + axis * (1.15f * t) + Vector3.up * Mathf.Max(-.316f - start.y, -.5f * t * t);
                    flowVertices.Add(point - side * width);
                    flowVertices.Add(point + side * width);
                    flowUv.Add(new Vector2(0, 1 - t));
                    flowUv.Add(new Vector2(1, 1 - t));
                    var tint = new Color(.04f + .19f * plume, .40f, 0, .82f * (1 - plume * .97f));
                    flowColors.Add(tint);
                    flowColors.Add(tint);
                    if (row == 10) continue;
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
