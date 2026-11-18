// OpenZ-VK (GPL-3.0).
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ.Experimental
{
    /// <summary>Build a continuous surface from a Zod grid. Gameplay uses the native tile and object records.</summary>
    [DefaultExecutionOrder(-500)]
    public partial class OzConversionTerrainV2 : MonoBehaviour
    {
        public string mapId;
        public Material ground, cliff, pit, water;
        public bool bakeWithPaletteV3;

        static string paletteV3Map;
        public static void SelectPaletteV3(string id) => paletteV3Map = id;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPaletteSelection() => SelectPaletteV3(null);
        public bool IsPaletteV3 { get; private set; }

        const int Apron = 24;
        readonly List<Object> generated = new List<Object>();

        protected void Awake()
        {
            var asset = Resources.Load<TextAsset>("OpenZ/Maps/" + mapId + "_grid");
            Build(ZMapData.FromJson(asset.text), true);
            Physics.SyncTransforms();
        }

        public void Build(ZMapData map, bool render)
        {
            int step = map.width * map.height > 30000 ? 2 : 1;
            int width = (map.width * 2 + Apron * 2) / step, height = (map.height * 2 + Apron * 2) / step;
            int stride = width + 1;
            var vertices = new List<Vector3>(stride * (height + 1));
            var uv = new List<Vector2>(vertices.Capacity);
            var channels = new[] { new List<int>(), new List<int>(), new List<int>() };
            var bridgeSpans = BridgeSpans(map);
            bool UnderBridge(ZMapData data, int index) => bridgeSpans[index];
            for (int row = 0; row <= height; row++)
            for (int col = 0; col <= width; col++)
            {
                float x = col * step - Apron, z = row * step - Apron;
                vertices.Add(new Vector3(x, Elevation(map, x, z, Mask(map, x, z, UnderBridge)), -z));
                uv.Add(new Vector2(x / (map.width * 2), 1 - z / (map.height * 2)));
            }

            for (int row = 0; row < height; row++)
            for (int col = 0; col < width; col++)
            {
                int a = row * stride + col, b = a + 1, c = a + stride, d = c + 1;
                AddTriangle(a, b, c);
                AddTriangle(c, b, d);
            }

            // The render mesh is also the picking surface, including the dry banks and the pit beds.
            var mesh = NewMesh("Converted terrain V2", vertices);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = channels.Length;
            for (int i = 0; i < channels.Length; i++) mesh.SetTriangles(channels[i], i, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var terrain = new GameObject("COL_Terrain", typeof(MeshCollider));
            terrain.transform.SetParent(transform, false);
            terrain.GetComponent<MeshCollider>().sharedMesh = mesh;
            if (!render) return;

            var palette = (bakeWithPaletteV3 || paletteV3Map != null && mapId == paletteV3Map) && PaletteV3.Supports(map.planet) &&
                map.tiles?.Length == map.width * map.height ? new PaletteV3(map) : null;
            IsPaletteV3 = palette != null;
            var surfaceGround = map.planet switch
            {
                "volcanic" => VolcanicGround(map, palette),
                "arctic" => ArcticGround(map),
                _ => LandGround(map, palette)
            };
            var rockFace = map.planet == "jungle" ? surfaceGround
                : map.planet == "volcanic" ? cliff : TiledRock(cliff);
            var floor = map.planet == "jungle" ? surfaceGround
                : map.planet == "volcanic" ? pit : TiledRock(pit);
            terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
            terrain.AddComponent<MeshRenderer>().sharedMaterials = new[] { surfaceGround, rockFace, floor };
            BuildWater(map);

            void AddTriangle(int a, int b, int c)
            {
                float lo = Mathf.Min(vertices[a].y, Mathf.Min(vertices[b].y, vertices[c].y));
                float hi = Mathf.Max(vertices[a].y, Mathf.Max(vertices[b].y, vertices[c].y));
                var triangles = channels[hi < -0.35f ? 2 : hi > 0.9f || hi - lo > 0.8f ? 1 : 0];
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }
        }

        Material TiledRock(Material original)
        {
            var material = new Material(original);
            material.SetFloat("_Triplanar", 1);
            material.SetFloat("_TriScale", .11f);
            generated.Add(material);
            return material;
        }

        // World-space noise breaks straight grid edges. The centres of ordinary tiles stay near the
        // road grade, while solid tiles rise or sink according to their corresponding palette role.
        static float Elevation(ZMapData map, float x, float z, float span)
        {
            float warpX = (Mathf.PerlinNoise(x * .27f, z * .27f) - .5f) * .7f;
            float warpZ = (Mathf.PerlinNoise(z * .29f + 7, x * .29f) - .5f) * .7f;
            bool city = map.planet == "city", jungle = map.planet == "jungle", arctic = map.planet == "arctic";
            float wet = Mask(map, x + warpX, z + warpZ, Wet);
            float solid = Mask(map, x + warpX, z + warpZ, Solid);
            float road = Mask(map, x, z, Road);
            float outside = Mathf.Max(Mathf.Max(-x, x - map.width * 2), Mathf.Max(-z, z - map.height * 2));
            float rim = Mathf.SmoothStep(0, 1, Mathf.Max(0, outside - 1) / 9);
            float support = 0;
            if (solid > .28f)
                support = (Mask(map, x - 2, z, Solid) + Mask(map, x + 2, z, Solid)
                    + Mask(map, x, z - 2, Solid) + Mask(map, x, z + 2, Solid)) * .25f;
            float bankHeight = Mathf.SmoothStep(0, 1, (solid - .28f) / .72f)
                * Mathf.Lerp(.35f, 1, Mathf.Clamp01(support * 1.7f));
            float relief = Mathf.Max(bankHeight, rim);
            float hummock = Mathf.PerlinNoise(x * .085f, z * .085f);
            float baseHeight = (hummock - .42f) * (city ? .11f : .32f) * (1 - road);
            float deep = Mathf.Max(Mask(map, x + warpX, z + warpZ, Deep), span);
            float ravine = Mathf.SmoothStep(0, 1, (Mathf.Max(wet, span) - .20f) / .65f)
                * Mathf.Lerp(city ? 1.18f : jungle ? .88f : .65f, 2.5f, deep);
            float elevation = city ? 2.75f + hummock * 1.7f : (jungle ? 3.1f : 2.2f) + hummock * 3.4f;
            if (arctic)
            {
                float drift = Mathf.PerlinNoise((x + z * .52f) * .16f, z * .049f);
                baseHeight += drift * drift * .52f * (1 - road) * (1 - wet);
                elevation = Mathf.Lerp(3.6f, 6.4f, hummock);
                elevation += Mathf.SmoothStep(0, 1, (outside - 5) / 18)
                    * (2 + Mathf.PerlinNoise(x * .034f + 57, z * .046f) * 13);
            }
            return baseHeight + relief * elevation * (1 - Mathf.SmoothStep(0, 1, span * 2)) - ravine;
        }

        static bool[] BridgeSpans(ZMapData map)
        {
            var spans = new bool[map.width * map.height];
            foreach (var obj in map.objects)
            {
                bool vertical = obj.kind == "building:bridge_vert";
                if (!vertical && obj.kind != "building:bridge_horz") continue;
                int length = 5 + (obj.extra_links < 100 ? obj.extra_links : 0);
                for (int along = 1; along < length - 1; along++)
                for (int across = 0; across < 4; across++)
                {
                    int x = obj.x + (vertical ? across : along), y = obj.y + (vertical ? along : across);
                    if (x < map.width && y < map.height) spans[y * map.width + x] = true;
                }
            }
            return spans;
        }

        static bool Wet(ZMapData map, int index) =>
            (map.HasLava && map.lava[index] > 0) || map.classes[index] == (int)ZClass.Water || map.classes[index] == (int)ZClass.DeepWater;
        static bool Solid(ZMapData map, int index) => map.classes[index] == (int)ZClass.Impassable && !Wet(map, index);
        static bool Road(ZMapData map, int index) => map.classes[index] == (int)ZClass.Road;
        static bool Lava(ZMapData map, int index) => map.HasLava && map.lava[index] > 0;
        static bool Deep(ZMapData map, int index) => Lava(map, index) || map.classes[index] == (int)ZClass.DeepWater;
        delegate bool TileRole(ZMapData map, int index);

        static float Mask(ZMapData map, float x, float z, TileRole role)
        {
            float tx = x * .5f - .5f, ty = z * .5f - .5f;
            int ix = Mathf.FloorToInt(tx), iy = Mathf.FloorToInt(ty);
            float Cell(int col, int row) =>
                col >= 0 && col < map.width && row >= 0 && row < map.height && role(map, row * map.width + col) ? 1 : 0;
            return Mathf.Lerp(Mathf.Lerp(Cell(ix, iy), Cell(ix + 1, iy), tx - ix),
                Mathf.Lerp(Cell(ix, iy + 1), Cell(ix + 1, iy + 1), tx - ix), ty - iy);
        }

        Material VolcanicGround(ZMapData map, PaletteV3 palette)
        {
            // L07's detail albedo and normals are tileable. Its full-map paint and field are rebuilt
            // here from this layout's road and shore masks, so the original map cannot bleed through.
            const int samplesPerTile = 6;
            int w = map.width * samplesPerTile, h = map.height * samplesPerTile;
            var paint = new Color32[w * h];
            var field = new Color32[w * h];
            var ash = new Color32(107, 95, 85, 255);
            var asphalt = new Color32(91, 77, 75, 255);
            for (int row = 0; row < h; row++)
            for (int col = 0; col < w; col++)
            {
                float x = (col + .5f) * 2 / samplesPerTile, z = (h - row - .5f) * 2 / samplesPerTile;
                float mottle = Mathf.PerlinNoise(x * .13f, z * .13f);
                float road = Mathf.SmoothStep(0, 1, (Mask(map, x, z, Road) - .22f) / .54f);
                float heat = Mask(map, x, z, Lava);
                float shade = .81f + mottle * .38f + (Mathf.PerlinNoise(x * .72f, z * .72f) - .5f) * .18f;
                Color baseColor = Color.Lerp(ash, asphalt, road);
                var shadeColor = baseColor * shade;
                shadeColor = Color.Lerp(shadeColor, new Color(.25f, .115f, .065f), Mathf.Clamp01(heat * 1.6f));
                if (palette != null)
                    shadeColor = palette.VolcanicPaint(map, x, z, road, heat, mottle);
                shadeColor.a = Mathf.Clamp01(heat * 1.15f);
                int index = row * w + col;
                paint[index] = shadeColor;
                field[index] = new Color(road, 1, 0, 0);
            }
            var material = new Material(ground) { name = palette == null ? "Converted volcanic ground" : "Volcanic palette V3" };
            generated.Add(material);
            material.SetTexture("_BaseMap", NewTexture(w, h, paint, false, "Converted ash and asphalt"));
            material.SetTexture("_GroundSdf", NewTexture(w, h, field, true, "Converted road mask"));
            material.SetVector("_GroundSize", new Vector4(map.width * 2, map.height * 2));
            material.SetVector("_DetailStr", new Vector4(.95f, 1.55f, .4f, .2f));
            if (palette != null)
            {
                material.SetVector("_DetailStr", new Vector4(1.15f, 1.75f, .4f, .29f));
                material.SetFloat("_DetailNormalStr", 1.28f);
            }
            return material;
        }

        void BuildWater(ZMapData map)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var underDeck = BridgeWater(map);
            bool WetSurface(ZMapData data, int index) => Water(data, index) || underDeck[index];
            for (int y = 0; y < map.height * 2; y++)
            for (int x = 0; x < map.width * 2; x++)
            {
                if (Mask(map, x + .5f, y + .5f, WetSurface) < .12f) continue;
                int a = vertices.Count;
                const float waterY = -.33f;
                vertices.Add(new Vector3(x, waterY, -y));
                vertices.Add(new Vector3(x + 1, waterY, -y));
                vertices.Add(new Vector3(x, waterY, -y - 1));
                vertices.Add(new Vector3(x + 1, waterY, -y - 1));
                indices.AddRange(new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 });
            }
            if (vertices.Count == 0) return;
            var mesh = NewMesh("Converted water", vertices);
            mesh.SetUVs(0, vertices.ConvertAll(p => new Vector2(p.x, p.z) * .1f));
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            var surface = new GameObject("WAT_Converted", typeof(MeshFilter), typeof(MeshRenderer));
            surface.transform.SetParent(transform, false);
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            var look = new Material(water);
            // The original current map is tied to the reference scene's dimensions and river.
            look.DisableKeyword("_WATER_FLOWMAP");
            if (look.HasProperty("_UseFlowMap")) look.SetFloat("_UseFlowMap", 0);
            generated.Add(look);
            surface.GetComponent<MeshRenderer>().sharedMaterial = look;
        }

        static bool Water(ZMapData map, int index) =>
            map.classes[index] == (int)ZClass.Water || map.classes[index] == (int)ZClass.DeepWater;

        static bool[] BridgeWater(ZMapData map)
        {
            var water = new bool[map.width * map.height];
            bool WetTile(int x, int y) => map.ClassAt(x, y) == ZClass.Water || map.ClassAt(x, y) == ZClass.DeepWater;
            foreach (var obj in map.objects)
            {
                bool vertical = obj.Type == "bridge_vert";
                if (!vertical && obj.Type != "bridge_horz") continue;
                int length = 5 + (obj.extra_links < 100 ? obj.extra_links : 0);
                for (int along = 1; along < length - 1; along++)
                {
                    int x = obj.x + (vertical ? 0 : along), y = obj.y + (vertical ? along : 0);
                    bool crossesWater = vertical ? WetTile(x - 1, y) || WetTile(x + 4, y)
                        : WetTile(x, y - 1) || WetTile(x, y + 4);
                    if (!crossesWater) continue;
                    for (int across = 0; across < 4; across++)
                    {
                        int cx = x + (vertical ? across : 0), cy = y + (vertical ? 0 : across);
                        if (cx < map.width && cy < map.height) water[cy * map.width + cx] = true;
                    }
                }
            }
            return water;
        }

        Mesh NewMesh(string name, List<Vector3> vertices)
        {
            var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            generated.Add(mesh);
            return mesh;
        }

        Texture2D NewTexture(int w, int h, Color32[] pixels, bool linear, string name)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, !linear, linear)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, anisoLevel = 8
            };
            texture.SetPixels32(pixels);
            texture.Apply(!linear, true);
            generated.Add(texture);
            return texture;
        }

        protected void OnDestroy() => Clear();

        public void Clear()
        {
            foreach (var asset in generated)
            {
                if (Application.isPlaying) Destroy(asset);
                else DestroyImmediate(asset);
            }
            generated.Clear();
        }
    }
}
