using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Menu OpenZ/V1.3/Unit Editor/4 Extras Icons Check (no Play). Expect [OpenZ ExtrasIcons] PASS 6 of 6.</summary>
    static class OzUeExtrasIconsCheck
    {
        [MenuItem("OpenZ/V1.3/Unit Editor/4 Extras Icons Check (no Play)")]
        static void Check()
        {
            var ids = new[] { OzUePlayground.Flag, OzUePlayground.Obstacle, OzUePlayground.Grenades, OzUePlayground.Nuke, OzUePlayground.PlatformMid, OzUePlayground.PlatformHigh };
            var paths = new[] { OzUeArt.FlagTilePath, OzUeArt.ObstacleTilePath, OzUeArt.GrenadesTilePath, OzUeArt.NukeTilePath, OzUeArt.PlatformMidTilePath, OzUeArt.PlatformHighTilePath };
            var model = AssetImporter.GetAtPath("Assets/OpenZ/Resources/" + OzUeArt.GrenadesTilePath + ".png") as TextureImporter;
            int pass = 0;
            for (int k = 0; k < ids.Length; k++)
            {
                var pic = Resources.Load<Texture2D>(paths[k]);
                var imp = AssetImporter.GetAtPath("Assets/OpenZ/Resources/" + paths[k] + ".png") as TextureImporter;
                bool ok = System.Array.IndexOf(OzUePlayground.Extras, ids[k]) == k && pic && pic.width == 256 && pic.height == 256 && pic.mipmapCount > 1 && OzUeArt.Picture(ids[k], Team.Neutral) == pic && SameImport(model, imp);
                Debug.Log($"[OpenZ ExtrasIcons] {(ok ? "PASS" : "FAIL")} {OzUePlayground.ShortName(ids[k])}: {(pic ? pic.width + " x " + pic.height + ", " + pic.mipmapCount + " mip levels" : "missing")}, Resources/{paths[k]}");
                if (ok) pass++;
            }
            string line = $"[OpenZ ExtrasIcons] {(pass == ids.Length ? "PASS" : "FAIL")} {pass} of {ids.Length}";
            if (pass == ids.Length) Debug.Log(line); else Debug.LogError(line);
        }

        static bool SameImport(TextureImporter a, TextureImporter b) =>
            a && b && a.sRGBTexture == b.sRGBTexture && a.mipmapEnabled == b.mipmapEnabled && a.filterMode == b.filterMode && a.wrapMode == b.wrapMode && a.anisoLevel == b.anisoLevel
            && a.mipMapBias == b.mipMapBias && a.isReadable == b.isReadable && a.alphaIsTransparency == b.alphaIsTransparency && a.maxTextureSize == b.maxTextureSize && a.textureCompression == b.textureCompression;
    }
}
