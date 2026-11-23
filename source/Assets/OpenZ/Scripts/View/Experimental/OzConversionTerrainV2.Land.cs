// OpenZ-VK (GPL-3.0).
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.Experimental
{
    public partial class OzConversionTerrainV2
    {
        Material LandGround(ZMapData map, PaletteV3 palette, bool useV4)
        {
            bool jungle = map.planet == "jungle", city = map.planet == "city";
            int resolution = city ? 12 : 6;
            if (palette != null) resolution = Mathf.Min(resolution, 1536 / Mathf.Max(map.width, map.height));
            int w = map.width * resolution, h = map.height * resolution;
            var paint = new Color32[w * h];
            var weightsA = new Color32[w * h];
            var weightsB = jungle ? new Color32[w * h] : null;
            var weightsC = jungle ? new Color32[w * h] : null;
            var leaves = jungle && palette != null ? new Color32[w * h] : null;
            var roadEdges = RoadEdges(map);
            var groundColor = jungle ? new Color32(71, 105, 45, 255)
                : city ? new Color32(141, 139, 130, 255) : new Color32(212, 158, 100, 255);
            var roadColor = jungle ? new Color32(110, 103, 74, 255)
                : city ? new Color32(73, 78, 83, 255) : new Color32(128, 122, 109, 255);
            var bankColor = jungle ? new Color32(81, 69, 42, 255)
                : city ? new Color32(134, 127, 106, 255) : new Color32(175, 125, 79, 255);
            for (int row = 0; row < h; row++)
            for (int col = 0; col < w; col++)
            {
                float x = (col + .5f) * 2 / resolution, z = (h - row - .5f) * 2 / resolution;
                float noise = Mathf.PerlinNoise(x * .12f, z * .12f);
                float coarse = Mathf.PerlinNoise(z * .048f + 29, x * .048f);
                float warp = (noise - .5f) * (city ? .06f : .16f);
                float rawRoad = Mask(map, x + warp, z - warp, Road);
                float road = RoadBlend(rawRoad, useV4, .21f, .58f);
                float wet = Mask(map, x, z, Wet);
                float solid = Mask(map, x, z, Solid);
                float bank = Mathf.Clamp01(wet * 2.15f);
                int index = row * w + col;
                var terrainRoles = palette?.Sample(map, x, z, noise) ?? Vector2.zero;
                Color color = Color.Lerp(Color.Lerp(groundColor, bankColor, bank), roadColor, road);
                float shade = .89f + noise * .12f + coarse * .13f;
                if (city)
                {
                    // The offset concrete joints are independent of the gameplay tile edges.
                    float slabX = x + (Mathf.Floor(z / 6) % 2) * 4;
                    float joints = Mathf.Min(GridDistance(slabX, 8), GridDistance(z, 6));
                    shade *= Mathf.Lerp(.82f, 1, Mathf.Lerp(Mathf.SmoothStep(0, 1, joints / .12f), 1, road));
                }
                color *= shade;
                if (palette != null)
                    color = PaletteV3.LandPaint(map.planet, terrainRoles, wet, solid, rawRoad, road, noise, coarse, x, z);
                float mark = !jungle && road > .65f ? RoadMark(map, roadEdges, x, z, city) : -.5f;
                color = Color.Lerp(color, city ? new Color(.73f, .67f, .43f) : new Color(.84f, .76f, .56f),
                    Mathf.SmoothStep(0, 1, (mark + .035f) * 12) * .8f);
                color.a = 1;
                paint[index] = color;
                if (jungle)
                {
                    float grass = (1 - road) * (1 - bank) * (1 - solid);
                    float verge = (1 - road) * Mathf.Max(bank * (1 - wet), solid * .48f);
                    weightsA[index] = new Color(grass, verge, bank * (1 - road) * .55f, solid * (1 - road));
                    weightsB[index] = new Color(wet * .30f, road, wet, solid * (1 - road) * .45f);
                    weightsC[index] = new Color(0, Mathf.Lerp(.84f, 1, coarse), wet * .34f, 0);
                    if (leaves != null)
                    {
                        float mud = Mathf.Max(bank * .86f, terrainRoles.y * (.72f + noise * .28f)) * (1 - road) * (1 - solid);
                        float moss = terrainRoles.x * (.57f + coarse * .4f);
                        float vergeBlend = Mathf.Max(verge, moss * grass * .66f);
                        weightsA[index] = new Color(grass * (1 - mud) * (1 - moss * .58f), vergeBlend * (1 - mud * .68f),
                            mud, solid * (1 - road));
                        weightsB[index] = new Color(wet * .4f, road, wet * 1.1f, solid * (1 - road) * (.45f + moss * .55f));
                        weightsC[index] = new Color(0, Mathf.Lerp(.78f, 1, coarse), Mathf.Max(wet, mud * .6f) * .5f, 0);
                        leaves[index] = new Color(moss, 0, 0, 1);
                    }
                }
                else weightsA[index] = new Color(rawRoad, city ? 0 : .5f + mark * .5f,
                    city ? .5f + mark * .5f : 0, 0);
            }

            var look = new Material(ground) { name = palette == null ? "Converted " + map.planet + " ground" : map.planet + " palette V3" };
            generated.Add(look);
            look.SetTexture("_BaseMap", NewTexture(w, h, paint, false, "Converted ground albedo"));
            if (jungle)
            {
                look.SetTexture("_SplatA", NewTexture(w, h, weightsA, true, "Jungle grass and banks"));
                look.SetTexture("_SplatB", NewTexture(w, h, weightsB, true, "Jungle road and swamp"));
                look.SetTexture("_SplatC", NewTexture(w, h, weightsC, true, "Jungle shading and wetness"));
                var studs = NewTexture(map.width, map.height, roadEdges, true, "Jungle road studs");
                studs.filterMode = FilterMode.Point;
                look.SetTexture("_RoadStuds", studs);
                look.SetVector("_GroundRect", new Vector4(0, -map.height * 2, .5f / map.width, .5f / map.height));
                look.SetVector("_MapTiles", new Vector4(map.width, map.height));
                look.SetVector("_FarPx", new Vector4(.16f, .34f));
                look.SetVector("_StudRule", new Vector4(.22f, .37f, 1.333f, .85f));
                look.SetFloat("_RimCutOn", 0);
                look.SetTexture("_LeafyMask", leaves == null ? Texture2D.blackTexture
                    : NewTexture(w, h, leaves, true, "Jungle moss and foliage cover"));
                look.SetVector("_CrackRule", Vector4.zero);
            }
            else
            {
                look.SetTexture("_GroundSdf", NewTexture(w, h, weightsA, true, "Converted road field"));
                look.SetVector("_GroundSize", new Vector4(map.width * 2, map.height * 2));
                look.SetVector("_DetailStr", city ? new Vector4(.76f, .60f, .4f, .42f) : new Vector4(.7f, .62f, .56f, .34f));
                if (palette != null)
                {
                    look.SetVector("_DetailStr", city ? new Vector4(.93f, .69f, .4f, .53f) : new Vector4(.88f, .7f, .74f, .4f));
                    look.SetFloat("_DetailNormalStr", city ? 1.05f : 1.22f);
                    if (!city) look.SetFloat("_Grade", .32f);
                }
            }
            return look;
        }

        // Both transitions are centred on the same native road boundary.
        static float RoadBlend(float sample, bool useV4, float start, float width)
        {
            float inset = useV4 ? .1f : 0;
            return Mathf.SmoothStep(0, 1, (sample - start - inset) / (width - 2 * inset));
        }

        static float GridDistance(float value, float period) =>
            Mathf.Abs(Mathf.Repeat(value + period * .5f, period) - period * .5f);

        static Color32[] RoadEdges(ZMapData map)
        {
            var edge = new Color32[map.width * map.height];
            for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
            {
                if (map.ClassAt(x, y) != ZClass.Road) continue;
                edge[(map.height - y - 1) * map.width + x] =
                    new Color(map.ClassAt(x, y - 1) == ZClass.Road ? 0 : 1,
                        map.ClassAt(x, y + 1) == ZClass.Road ? 0 : 1,
                        map.ClassAt(x - 1, y) == ZClass.Road ? 0 : 1,
                        map.ClassAt(x + 1, y) == ZClass.Road ? 0 : 1);
            }
            return edge;
        }

        static float RoadMark(ZMapData map, Color32[] edges, float x, float z, bool city)
        {
            int col = (int)(x * .5f), row = (int)(z * .5f);
            var side = edges[(map.height - row - 1) * map.width + col];
            float atX = Mathf.Repeat(x, 2), atZ = Mathf.Repeat(z, 2);
            float dx = Mathf.Min(side.b > 0 ? Mathf.Abs(atX - .43f) : 3, side.a > 0 ? Mathf.Abs(atX - 1.57f) : 3);
            float dz = Mathf.Min(side.r > 0 ? Mathf.Abs(atZ - .43f) : 3, side.g > 0 ? Mathf.Abs(atZ - 1.57f) : 3);
            float dashes = dx < dz ? GridDistance(z + .3f, city ? 3.6f : 2.6f) : GridDistance(x + .3f, city ? 3.6f : 2.6f);
            return Mathf.Min((city ? .13f : .08f) - Mathf.Min(dx, dz), (city ? .91f : .32f) - dashes);
        }
    }
}
