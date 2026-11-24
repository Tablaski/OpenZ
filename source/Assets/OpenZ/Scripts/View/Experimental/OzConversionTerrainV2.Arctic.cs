// OpenZ-VK (GPL-3.0).
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.Experimental
{
    public partial class OzConversionTerrainV2
    {
        sealed class ArcticEdges
        {
            public const int SamplesPerMetre = 4;
            readonly int columns, rows;
            readonly Vector2[] coverage;

            public ArcticEdges(ZMapData map)
            {
                columns = 2 * map.width * SamplesPerMetre + 1;
                rows = 2 * map.height * SamplesPerMetre + 1;
                coverage = new Vector2[columns * rows];
                for (int z = 0; z < rows; z++)
                for (int x = 0; x < columns; x++)
                {
                    float px = x / (float)SamplesPerMetre, pz = z / (float)SamplesPerMetre;
                    float tx = (px + Warp(px, pz)) * .5f - .5f;
                    float tz = (pz + Warp(pz + 43, px - 8)) * .5f - .5f;
                    int ix = Mathf.FloorToInt(tx), iz = Mathf.FloorToInt(tz);
                    float fx = tx - ix, fz = tz - iz;
                    Vector2 field = Curve(Row(iz - 1), Row(iz), Row(iz + 1), Row(iz + 2), fz);
                    coverage[z * columns + x] = new Vector2(Mathf.Clamp01(field.x), Mathf.Clamp01(field.y));

                    Vector2 Row(int row) => Curve(Tile(ix - 1, row), Tile(ix, row), Tile(ix + 1, row), Tile(ix + 2, row), fx);
                }

                Vector2 Tile(int x, int z)
                {
                    if (x < 0 || x >= map.width || z < 0 || z >= map.height) return Vector2.zero;
                    int index = z * map.width + x;
                    return new Vector2(Water(map, index) ? 1 : 0, ArcticIceTile(map, index) ? 1 : 0);
                }
            }

            static float Warp(float x, float z) =>
                (Mathf.PerlinNoise(x * .34f + 31, z * .34f + 17) - .5f) * 1.13f
                + (Mathf.PerlinNoise(x * 1.23f, z * 1.23f) - .5f) * .24f;

            // Cubic interpolation through the native samples. The neighbourhood shapes corners
            // without shrinking one-tile channels or removing an isolated ice tile.
            static Vector2 Curve(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t) =>
                b + .5f * t * (c - a + t * (2 * a - 5 * b + 4 * c - d + t * (3 * (b - c) + d - a)));

            public Vector2 Sample(float x, float z)
            {
                x *= SamplesPerMetre;
                z *= SamplesPerMetre;
                if (x < 0 || x > columns - 1 || z < 0 || z > rows - 1) return Vector2.zero;
                int ix = Mathf.Min(Mathf.FloorToInt(x), columns - 2);
                int iz = Mathf.Min(Mathf.FloorToInt(z), rows - 2);
                int index = iz * columns + ix;
                return Vector2.Lerp(
                    Vector2.Lerp(coverage[index], coverage[index + 1], x - ix),
                    Vector2.Lerp(coverage[index + columns], coverage[index + columns + 1], x - ix), z - iz);
            }
        }

        Mesh ArcticSnowBanks(ZMapData map, List<Vector3> original, List<int>[] channels, int stride, int step,
            ArcticEdges edges, System.Func<float, float, float, float> height)
        {
            int columns = stride - 1, rows = original.Count / stride - 1;
            var detail = new bool[rows * columns];
            for (int row = 0; row < rows; row++)
            for (int col = 0; col < columns; col++)
            {
                float x = col * step - Apron, z = row * step - Apron;
                float wet = edges.Sample(x + step * .5f, z + step * .5f).x;
                // Keep the detailed patch wide enough that its boundary lies away from the waterline.
                detail[row * columns + col] = wet > .001f ||
                    Mask(map, x + step * .5f, z + step * .5f, Water) > 0;
            }

            var points = new List<Vector3>(original);
            var finePoints = new Dictionary<Vector2Int, int>();
            var surfaces = new[] { new List<int>(channels[0].Count), new List<int>(channels[1].Count), new List<int>(channels[2].Count) };
            int divisions = step * ArcticEdges.SamplesPerMetre;
            var patch = new int[(divisions + 1) * (divisions + 1)];
            for (int row = 0; row < rows; row++)
            for (int col = 0; col < columns; col++)
            {
                int corner = row * stride + col;
                if (!Detailed(col, row))
                {
                    Triangle(corner, corner + 1, corner + stride);
                    Triangle(corner + stride, corner + 1, corner + stride + 1);
                    continue;
                }

                int patchStride = divisions + 1;
                for (int z = 0; z <= divisions; z++)
                for (int x = 0; x <= divisions; x++)
                    patch[z * patchStride + x] = Point(col * divisions + x, row * divisions + z);

                for (int z = 0; z < divisions; z++)
                for (int x = 0; x < divisions; x++)
                {
                    int i = z * patchStride + x;
                    Triangle(patch[i], patch[i + 1], patch[i + patchStride]);
                    Triangle(patch[i + patchStride], patch[i + 1], patch[i + patchStride + 1]);
                }
            }

            var mesh = NewMesh("Arctic terrain with smooth lake shores", points);
            mesh.SetUVs(0, points.ConvertAll(p => new Vector2(p.x / (2 * map.width), 1 + p.z / (2 * map.height))));
            mesh.subMeshCount = surfaces.Length;
            for (int i = 0; i < surfaces.Length; i++) mesh.SetTriangles(surfaces[i], i, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;

            bool Detailed(int col, int row) =>
                col >= 0 && col < columns && row >= 0 && row < rows && detail[row * columns + col];

            int Point(int x, int z)
            {
                var key = new Vector2Int(x, z);
                if (finePoints.TryGetValue(key, out int known)) return known;
                int col = x / divisions, row = z / divisions;
                bool columnEdge = x % divisions == 0, rowEdge = z % divisions == 0;
                bool cornerPoint = columnEdge && rowEdge;
                bool touchesCoarse = !Detailed(col, row) ||
                    (columnEdge && !Detailed(col - 1, row)) ||
                    (rowEdge && !Detailed(col, row - 1)) ||
                    (cornerPoint && !Detailed(col - 1, row - 1));
                int index = row * stride + col;
                float px = x / (float)ArcticEdges.SamplesPerMetre - Apron;
                float pz = z / (float)ArcticEdges.SamplesPerMetre - Apron;
                float y;
                if (touchesCoarse)
                {
                    // Leave the shared perimeter on the straight edge of the unsplit neighbour.
                    if (cornerPoint) return index;
                    int next = columnEdge ? index + stride : index + 1;
                    y = Mathf.Lerp(original[index].y, original[next].y,
                        (columnEdge ? z % divisions : x % divisions) / (float)divisions);
                }
                else
                {
                    float road = Mask(map, px, pz, Road);
                    var field = edges.Sample(px, pz);
                    float rounded = Mathf.Lerp(field.x, Mask(map, px, pz, Water), Mathf.SmoothStep(0, 1, road));
                    y = height(px, pz, rounded);
                }

                int result = cornerPoint ? index : points.Count;
                var point = new Vector3(px, y, -pz);
                if (cornerPoint) points[index] = point;
                else points.Add(point);
                finePoints.Add(key, result);
                return result;
            }

            void Triangle(int a, int b, int c)
            {
                float lo = Mathf.Min(points[a].y, Mathf.Min(points[b].y, points[c].y));
                float hi = Mathf.Max(points[a].y, Mathf.Max(points[b].y, points[c].y));
                // Use snow on low wedges at the shoulders and cliff shading on the taller banks.
                bool cliff = (hi > .9f || hi - lo > .8f) && (lo >= .55f || hi >= 1.8f);
                int material = hi < -.35f ? 2 : cliff ? 1 : 0;
                surfaces[material].Add(a);
                surfaces[material].Add(b);
                surfaces[material].Add(c);
            }
        }
        Material ArcticGround(ZMapData map, bool useV4, ArcticEdges edges)
        {
            int samplesPerTile = IsPaletteV5 ? Mathf.Clamp(2048 / Mathf.Max(map.width, map.height), 7, 14) : 7;
            int w = map.width * samplesPerTile, h = map.height * samplesPerTile;
            var albedo = new Color32[w * h];
            var roadField = useV4 ? new Color32[w * h] : null;
            for (int row = 0; row < h; row++)
            for (int col = 0; col < w; col++)
            {
                float x = (col + .5f) * 2 / samplesPerTile;
                float z = (h - row - .5f) * 2 / samplesPerTile;
                float drifts = Mathf.PerlinNoise((x + z * .65f) * .165f, z * .036f);
                float grain = Mathf.PerlinNoise(x * .74f, z * .74f);
                if (IsPaletteV5)
                    drifts = Mathf.PerlinNoise(x * .17f, z * .17f) * .7f + grain * .3f;
                float offset = (grain - .5f) * .55f;
                float road = Mask(map, x + offset, z - offset, Road);
                float roadWeight = RoadBlend(road, useV4, IsPaletteV5 ? .35f : .27f, IsPaletteV5 ? .39f : .55f);
                var shore = edges?.Sample(x, z) ?? Vector2.zero;
                float wet = edges != null ? shore.x : Mask(map, x + offset, z, Water);
                float crust = Mathf.SmoothStep(0, 1, (wet - .05f) * 2.8f);
                float stones = Mathf.SmoothStep(0, 1, (drifts - .48f) * 1.2f);
                var snow = Color.Lerp(new Color(.75f, .83f, .89f), new Color(.96f, .98f, 1f), drifts);
                var gravel = Color.Lerp(new Color(.26f, .30f, .34f), new Color(.51f, .56f, .58f), grain);
                var ice = Color.Lerp(new Color(.60f, .74f, .80f), new Color(.75f, .87f, .90f), grain);
                float lakeIce = edges != null ? shore.y : 1;
                float frost = useV4 ? Mathf.SmoothStep(0, 1, crust * 1.35f) : crust;
                if (IsPaletteV5)
                {
                    float edgeGrit = (Mathf.PerlinNoise(x * 5.1f, z * 5.1f) - .5f) * .05f;
                    float wetSnow = Mathf.SmoothStep(0, 1, (wet - .25f + edgeGrit) / .34f);
                    frost = Mathf.Lerp(wetSnow * .53f, frost, Mathf.Clamp01(lakeIce * 3));
                }
                if (IsPaletteV5) gravel = Color.Lerp(new Color(.27f, .28f, .29f), new Color(.41f, .43f, .45f), grain);
                var color = Color.Lerp(Color.Lerp(snow, ice, frost * .85f), gravel,
                    roadWeight * (IsPaletteV5 ? .98f : .83f + stones * .17f));
                albedo[row * w + col] = color;
                if (useV4) roadField[row * w + col] = new Color(road, shore.x, 0, lakeIce);
            }
            var material = new Material(ground) { name = "Wind-packed Arctic snow" };
            material.SetTexture("_BaseMap", NewTexture(w, h, albedo, false, "Snow drifts, gravel and frozen banks"));
            if (useV4) material.SetTexture("_GroundSdf", NewTexture(w, h, roadField, true, "Arctic road field"));
            if (IsPaletteV5) material.SetFloat("_Blur", 0);
            generated.Add(material);
            return material;
        }

        static bool ArcticIceTile(ZMapData map, int index) => OzAiTerrainV5.ArcticIceAt(map, index);

        static void ArcticWaterLook(Material water)
        {
            // Keep blue open water and its cold, pale banks distinct at tactical zoom.
            water.SetColor("_ShallowColor", new Color(.49f, .74f, .85f));
            water.SetColor("_MidColor", new Color(.12f, .38f, .60f));
            water.SetColor("_DeepColor", new Color(.032f, .13f, .27f));
            water.SetFloat("_MidDepth", .23f);
            water.SetFloat("_DeepDepth", .72f);
            water.SetFloat("_RippleStrengthA", .17f);
            water.SetFloat("_RippleStrengthB", .10f);
            water.SetFloat("_RippleSpeed", .085f);
            water.SetFloat("_CausticsStrength", .21f);
        }

        static void ArcticOpenWaterV5(Material water)
        {
            water.SetColor("_ShallowColor",new Color(.39f,.64f,.69f));
            water.SetColor("_MidColor",new Color(.056f,.25f,.34f));
            water.SetColor("_DeepColor",new Color(.018f,.085f,.157f));
            water.SetColor("_FoamColor",new Color(.71f,.82f,.84f,.34f));
            water.SetFloat("_FoamWidth",.047f);
            water.SetFloat("_EdgeFade",.105f);
            water.SetFloat("_RippleProc",1);
            water.SetFloat("_ProcSize",.26f);
            water.SetVector("_ProcSlope",new Vector4(.24f,.135f,.025f,0));
            water.SetFloat("_ProcSpeed",.45f);
            water.SetFloat("_ProcCalm",.74f);
            water.SetFloat("_ProcCalmSize",8);
            water.SetFloat("_ProcMorph",.12f);
            water.SetFloat("_RippleContrast",.50f);
            water.SetFloat("_GlintIntensity",.68f);
            water.SetFloat("_GlintPower",385);
            water.SetFloat("_GlintThreshold",.28f);
            water.SetFloat("_GlintSparsity",.40f);
            water.SetFloat("_Refraction",.055f);
        }

    }
}
