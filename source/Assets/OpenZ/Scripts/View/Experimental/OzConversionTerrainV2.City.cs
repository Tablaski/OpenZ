// OpenZ-VK (GPL-3.0).
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ.Experimental
{
    public partial class OzConversionTerrainV2
    {
        Mesh CityBorderSurface(ZMapData map, List<Vector3> vertices, List<Vector2> uv, List<int>[] channels)
        {
            var rim = new bool[map.width * map.height];
            var pending = new Queue<int>();
            for (int x = 0; x < map.width; x++)
            {
                Visit(x, 0);
                Visit(x, map.height - 1);
            }
            for (int z = 0; z < map.height; z++)
            {
                Visit(0, z);
                Visit(map.width - 1, z);
            }
            while (pending.TryDequeue(out int index))
            {
                int x = index % map.width, z = index / map.width;
                Visit(x - 1, z);
                Visit(x + 1, z);
                Visit(x, z - 1);
                Visit(x, z + 1);
            }

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
                    var center = (vertices[a] + vertices[b] + vertices[c]) / 3;
                    bool exterior = Outside(vertices[a]) && Outside(vertices[b]) && Outside(vertices[c]);
                    bool retainingFace = channel == 1 && Mask(map, center.x, -center.z, PerimeterRock) > .25f;
                    var group = exterior || retainingFace ? border : inside;
                    group.Add(a); group.Add(b); group.Add(c);
                }
                mesh.SetTriangles(inside,channel,false);
            }
            mesh.SetTriangles(border,channels.Length,false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;

            bool PerimeterRock(ZMapData data, int index) => rim[index];
            void Visit(int x, int z)
            {
                if (x < 0 || z < 0 || x >= map.width || z >= map.height) return;
                if (Mathf.Min(Mathf.Min(x, z), Mathf.Min(map.width - 1 - x, map.height - 1 - z)) >= 6) return;
                int index = z * map.width + x;
                if (rim[index] || !Solid(map, index)) return;
                rim[index] = true;
                pending.Enqueue(index);
            }
        }

        Material CityBorderConcrete()
        {
            var concrete = TiledRock(pit, true);
            concrete.name = "Weathered perimeter concrete";
            concrete.SetColor("_BaseColor", new Color(.55f, .51f, .46f));
            concrete.SetFloat("_Smoothness", .23f);
            return concrete;
        }

        void BuildCityBorderRuins(ZMapData map, System.Func<float, float, float> height)
        {
            MeshFilter source = null;
            foreach (Transform part in transform)
            {
                if (part.name == "OUT3_Ruins" && !source) source = part.GetComponent<MeshFilter>();
                if (part.name == "OUT3_Ruins" || part.name == "OUT3_Quays") part.gameObject.SetActive(false);
            }

            if (!source || !source.sharedMesh || !source.sharedMesh.isReadable)
            {
                Debug.LogWarning($"[AI map terrain] {mapId}: Car Park perimeter art is unavailable");
                return;
            }

            // The imported mesh contains a street grid outside L17. Select one complete
            // street strip per side to keep the spacing and orientation of its buildings.
            // The south side is the harbour.
            var art = source.sharedMesh;
            var sourceVertices = art.vertices;
            var sourceNormals = art.normals;
            var sourceUv = art.uv;
            var sourceColors = art.colors32;
            var parent = new int[sourceVertices.Length];
            var weld = new Dictionary<Vector3Int, int>();
            var reference = new Vector3[sourceVertices.Length];
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                parent[i] = i;
                var point = source.transform.rotation * sourceVertices[i];
                reference[i] = point;
                var key = Vector3Int.RoundToInt(point * 1000);
                if (weld.TryGetValue(key, out int previous)) parent[i] = previous;
                else weld.Add(key, i);
            }

            var sourceTriangles = new int[art.subMeshCount][];
            for (int submesh = 0; submesh < sourceTriangles.Length; submesh++)
            {
                var triangles = sourceTriangles[submesh] = art.GetTriangles(submesh);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Join(triangles[i], triangles[i + 1]);
                    Join(triangles[i], triangles[i + 2]);
                }
            }

            var bounds = new Dictionary<int, Bounds>();
            for (int i = 0; i < reference.Length; i++)
            {
                int root = Root(i);
                if (!bounds.TryGetValue(root, out var extent)) extent = new Bounds(reference[i], Vector3.zero);
                extent.Encapsulate(reference[i]);
                bounds[root] = extent;
            }

            const float referenceWidth = 220, referenceDepth = 256;
            const float facadeHeight = .40f;
            float width = map.width * 2, depth = map.height * 2;
            var offsets = new Dictionary<int, (Vector3 scale, Vector3 offset)>();
            foreach (var (root, extent) in bounds)
            {
                Vector3 lo = extent.min, hi = extent.max;
                Vector3 scale, offset;
                if (lo.x >= -100 && hi.x <= -60 && lo.z >= -referenceDepth + 5 && hi.z <= 3)
                {
                    scale = new Vector3(15f / 40, facadeHeight, depth / referenceDepth);
                    offset = new Vector3(-19 + 100 * scale.x, 0, 0);
                }
                else if (lo.x >= 271 && hi.x <= 308 && lo.z >= -referenceDepth + 5 && hi.z <= 3)
                {
                    scale = new Vector3(15f / 37, facadeHeight, depth / referenceDepth);
                    offset = new Vector3(width + 4 - 271 * scale.x, 0, 0);
                }
                else if (lo.x >= 2 && hi.x <= referenceWidth - 2 && lo.z >= 84 && hi.z <= 125)
                {
                    scale = new Vector3(width / referenceWidth, facadeHeight, 15f / 41);
                    offset = new Vector3(0, 0, 4 - 84 * scale.z);
                }
                else continue;

                Vector3 seat = Vector3.Scale(extent.center, scale) + offset;
                offset.y = height(seat.x, -seat.z) - 5.4f * facadeHeight - .58f;
                offsets.Add(root, (scale, offset));
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var colors = new List<Color32>();
            var lookup = new int[reference.Length];
            System.Array.Fill(lookup, -1);
            for (int i = 0; i < reference.Length; i++)
            {
                if (!offsets.TryGetValue(Root(i), out var placement)) continue;
                lookup[i] = vertices.Count;
                vertices.Add(Vector3.Scale(reference[i], placement.scale) + placement.offset);
                var normal = source.transform.rotation * sourceNormals[i];
                normals.Add(new Vector3(normal.x / placement.scale.x, normal.y / placement.scale.y, normal.z / placement.scale.z).normalized);
                uv.Add(sourceUv[i]);
                if (sourceColors.Length == reference.Length) colors.Add(sourceColors[i]);
            }

            if (vertices.Count == 0)
            {
                Debug.LogWarning($"[AI map terrain] {mapId}: Car Park perimeter rows are empty, source bounds {art.bounds}");
                return;
            }
            var mesh = NewMesh("The Car Park perimeter ruins", vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            if (colors.Count != 0) mesh.SetColors(colors);
            mesh.subMeshCount = sourceTriangles.Length;
            for (int submesh = 0; submesh < sourceTriangles.Length; submesh++)
            {
                var selected = new List<int>();
                foreach (int i in sourceTriangles[submesh])
                    if (lookup[i] >= 0) selected.Add(lookup[i]);
                mesh.SetTriangles(selected, submesh, false);
            }

            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var ruins = new GameObject("The Car Park perimeter ruins", typeof(MeshFilter), typeof(MeshRenderer));
            ruins.transform.SetParent(transform, false);
            ruins.GetComponent<MeshFilter>().sharedMesh = mesh;
            ruins.GetComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
            Debug.Log($"[AI map terrain] {mapId}: {offsets.Count} Car Park ruin pieces in perimeter rows");

            int Root(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }
                return i;
            }

            void Join(int a, int b) => parent[Root(b)] = Root(a);
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
