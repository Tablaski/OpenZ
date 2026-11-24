// OpenZ-VK (GPL-3.0).
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.Experimental
{
    public partial class OzConversionTerrainV2
    {
        Material ArcticGround(ZMapData map, bool useV4)
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
                float offset = (grain - .5f) * .55f;
                float road = Mask(map, x + offset, z - offset, Road);
                float roadWeight = RoadBlend(road, useV4, IsPaletteV5 ? .35f : .27f, IsPaletteV5 ? .39f : .55f);
                float wet = Mask(map, x + offset, z, Water);
                float crust = Mathf.SmoothStep(0, 1, (wet - .05f) * 2.8f);
                float stones = Mathf.SmoothStep(0, 1, (drifts - .48f) * 1.2f);
                var snow = Color.Lerp(new Color(.75f, .83f, .89f), new Color(.96f, .98f, 1f), drifts);
                var gravel = Color.Lerp(new Color(.26f, .30f, .34f), new Color(.51f, .56f, .58f), grain);
                var ice = Color.Lerp(new Color(.60f, .74f, .80f), new Color(.75f, .87f, .90f), grain);
                float frost = useV4 ? Mathf.SmoothStep(0, 1, crust * 1.35f) : crust;
                if (IsPaletteV5) gravel = Color.Lerp(new Color(.27f, .28f, .29f), new Color(.41f, .43f, .45f), grain);
                var color = Color.Lerp(Color.Lerp(snow, ice, frost * .85f), gravel,
                    roadWeight * (IsPaletteV5 ? .98f : .83f + stones * .17f));
                albedo[row * w + col] = color;
                if (useV4) roadField[row * w + col] = new Color(road, 0, 0,
                    IsPaletteV5 ? Mask(map, x + offset, z, ArcticIceTile) : 1);
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

    }
}
