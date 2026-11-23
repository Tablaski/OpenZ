// OpenZ-VK (GPL-3.0).
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.Experimental
{
    public partial class OzConversionTerrainV2
    {
        // Terrain roles reviewed against the Fenio editor atlases. Only tile identifiers are
        // used here. Surface paint is generated at world scale and uses the OpenZ material detail.
        sealed class PaletteV3
        {
            static readonly HashSet<int> DesertScrub = new HashSet<int>
            {
                140, 141, 142, 143, 144, 145, 160, 161, 162, 163, 164, 165, 179, 180, 181, 182, 183
            };
            static readonly HashSet<int> DesertGravel = new HashSet<int>
            {
                14, 15, 16, 33, 34, 37, 53, 54, 57, 62, 63, 64, 73, 74, 77, 82, 83, 94, 95, 96, 105,
                114, 116, 117, 119, 134, 137, 138, 139, 154, 158, 166, 170, 175, 178, 196, 199, 302, 303
            };
            static readonly HashSet<int> CityServiceYard = new HashSet<int>
            {
                79, 98, 99, 110, 111, 112, 113, 114, 115, 118, 119, 147, 148, 149, 150, 151, 152, 153, 167,
                168, 169, 170, 180, 181, 182, 183, 184, 185, 187, 193, 200, 201, 202, 203, 204, 205, 206, 207,
                220, 221, 222, 225, 226, 227, 310, 311, 320, 321, 330, 331, 340, 341, 342, 350, 351, 360, 361,
                362, 363, 364, 369, 383, 384, 387, 405, 425, 445, 446, 447, 448, 449, 451
            };
            static readonly HashSet<int> CityRubble = new HashSet<int>
            {
                0, 1, 2, 3, 4, 5, 6, 7, 12, 13, 27, 28, 32, 33, 41, 42, 47, 48, 52, 53, 61, 62, 63,
                67, 68, 72, 73, 107, 108, 109, 127, 128, 129, 134, 135, 154, 155, 188, 189, 190, 191, 192,
                236, 237, 238, 299, 316, 317, 319, 338, 339, 354, 355, 356, 358, 359
            };
            static readonly HashSet<int> JungleMoss = new HashSet<int>
            {
                0, 1, 2, 3, 4, 5, 20, 21, 22, 23, 24, 25, 40, 41, 42, 43, 44, 45, 60, 61, 62, 63, 64,
                217, 218, 219, 232, 233, 234, 235, 236, 237, 238, 239, 275, 276, 310, 311, 312, 313, 314, 315,
                330, 331, 332, 333, 334, 335, 336, 345, 346, 366, 367, 368, 369, 387, 388, 389, 448, 449
            };
            static readonly HashSet<int> JungleMud = new HashSet<int>
            {
                46, 47, 48, 49, 66, 67, 68, 69, 86, 87, 88, 89, 105, 106, 107, 108, 109, 117, 118, 119,
                131, 132, 133, 134, 249, 297, 317, 337, 338, 390, 391, 392, 393, 395, 396, 397, 398, 410, 411,
                412, 413, 415, 416, 417, 418, 430, 431, 432, 434, 435, 436, 437, 438
            };
            static readonly HashSet<int> VolcanicBasalt = new HashSet<int>
            {
                7, 8, 9, 10, 11, 27, 28, 29, 30, 31, 47, 48, 49, 50, 51, 67, 68, 69, 70, 71, 87, 88, 89,
                90, 91, 107, 108, 109, 110, 111, 170, 171, 172, 173, 174, 190, 191, 192, 193, 194, 210, 211,
                212, 213, 214, 218, 219, 227, 228, 229, 230, 231, 232, 233, 234, 237, 238, 239
            };
            static readonly HashSet<int> VolcanicScoria = new HashSet<int>
            {
                0, 1, 2, 3, 20, 21, 22, 23, 93, 94, 113, 114, 131, 132, 133, 134, 151, 152, 153, 154,
                183, 184, 185, 186, 187, 188, 189, 347, 350, 351, 352, 353, 370, 371, 372, 373, 393, 394,
                402, 403, 413, 414, 423, 433, 443, 463
            };

            public static bool Supports(string world) => world == "desert" || world == "city" || world == "jungle" || world == "volcanic";

            readonly byte[] roles;
            readonly TileRole primary;
            readonly TileRole secondary;

            public PaletteV3(ZMapData map)
            {
                var (first, second) = map.planet switch
                {
                    "desert" => (DesertGravel, DesertScrub),
                    "city" => (CityServiceYard, CityRubble),
                    "jungle" => (JungleMoss, JungleMud),
                    _ => (VolcanicBasalt, VolcanicScoria)
                };
                roles = new byte[map.tiles.Length];
                for (int i = 0; i < roles.Length; i++)
                {
                    if (map.classes[i] != (int)ZClass.Ground && !(map.planet == "volcanic" && Solid(map, i))) continue;
                    int tile = map.tiles[i];
                    roles[i] = first.Contains(tile) ? (byte)1 : second.Contains(tile) ? (byte)2 : (byte)0;
                }
                primary = (_, index) => roles[index] == 1;
                secondary = (_, index) => roles[index] == 2;
            }

            public Vector2 Sample(ZMapData map, float x, float z, float noise)
            {
                float warp = (noise - .5f) * .92f;
                return new Vector2(Mask(map, x + warp, z - warp, primary), Mask(map, x + warp, z - warp, secondary));
            }

            public Color VolcanicPaint(ZMapData map, float x, float z, float road, float heat, float mottle)
            {
                var weights = Sample(map, x, z, mottle);
                float grit = Mathf.PerlinNoise(x * .65f + 13, z * .72f);
                float drift = Mathf.PerlinNoise(x * .051f, z * .057f + 35);
                Color ash = Color.Lerp(new Color32(126, 107, 98, 255), new Color32(91, 82, 88, 255), drift);
                Color dry = Color.Lerp(ash, new Color32(70, 68, 75, 255), weights.x * .86f);
                dry = Color.Lerp(dry, new Color32(128, 88, 70, 255), weights.y * .8f);
                float scorched = Mathf.SmoothStep(0, 1, heat * 2.7f);
                dry = Color.Lerp(dry, new Color32(64, 39, 37, 255), scorched);
                var tarmac = new Color32(85, 78, 85, 255);
                return Color.Lerp(dry, tarmac, road) * (.84f + .23f * mottle + .11f * grit);
            }

            public static Color LandPaint(string world, Vector2 weights, float wet, float solid, float rawRoad, float road,
                float noise, float coarse, float x, float z, bool textureV5)
            {
                float shoulder = 4 * rawRoad * (1 - rawRoad);
                float shore = Mathf.SmoothStep(0, 1, wet * 2.65f);
                float grain = Mathf.PerlinNoise(x * .57f + 31, z * .59f);
                Color color;
                Color roadColor;
                switch (world)
                {
                    case "city":
                        // Paving is staggered independently of the tile grid, with chipped joints only in the service yards.
                        float slab = Mathf.Floor(z / 4.7f);
                        float joint = Mathf.Min(GridDistance(x + slab * 3.3f, 6.6f), GridDistance(z, 4.7f));
                        float seam = 1 - Mathf.SmoothStep(0, 1, joint / (.10f + .18f * grain));
                        Color concrete = new Color32(145, 145, 139, 255);
                        Color serviceYard = (Color)new Color32(108, 119, 120, 255) * (1 - seam * .48f);
                        color = Color.Lerp(concrete, serviceYard, weights.x);
                        color = Color.Lerp(color, new Color32(149, 124, 112, 255), weights.y * .82f);
                        color = Color.Lerp(color, new Color32(112, 108, 91, 255), shore * .79f);
                        roadColor = new Color32(70, 76, 85, 255);
                        break;
                    case "jungle":
                        color = Color.Lerp(new Color32(104, 114, 49, 255), new Color32(57, 104, 50, 255), weights.x);
                        color = Color.Lerp(color, new Color32(106, 79, 52, 255), Mathf.Max(shore, weights.y * .85f));
                        color = Color.Lerp(color, new Color32(74, 69, 56, 255), solid * .72f);
                        roadColor = new Color32(111, 107, 86, 255);
                        break;
                    default:
                        color = Color.Lerp(new Color32(209, 173, 119, 255), new Color32(162, 132, 109, 255), weights.x * .9f);
                        var darkSand = textureV5 ? new Color32(154, 118, 87, 255) : new Color32(145, 136, 77, 255);
                        color = Color.Lerp(color, darkSand, weights.y * .9f);
                        color = Color.Lerp(color, new Color32(155, 112, 78, 255), Mathf.Max(shore, solid * .55f));
                        var windblown = new Color32(199, 155, 103, 255);
                        color = Color.Lerp(color, windblown, shoulder * (1 - wet) * .6f);
                        roadColor = Color.Lerp(new Color32(104, 103, 103, 255), windblown, shoulder * grain * .5f);
                        break;
                }
                return Color.Lerp(color, roadColor, road) * (.8f + noise * .16f + coarse * .18f + grain * .07f);
            }
        }
    }
}
