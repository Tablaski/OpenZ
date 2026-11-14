#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Mech v2, the worn shoulder housings. v1's housing paint (mech_DV3_olive) is a flat 4 x 4 team swatch: it has no picture that could
    /// carry the edge wear, scratches and grime the rest of the v2 shows. The v2 wears its own copy on MechRocketHousing_L / _R:
    /// Materials/Units/mech_v2b_housing = v1's mech_DV3_olive in every setting, its picture Models/Legacy/Toy/mech_v2b_housing.png (the same
    /// team paint, alpha 1 = team panel, with the wear, in the housing mesh's own UVs). TeamLit's toy path paints it in the team colour
    /// (owned) or the empty grey (neutral) exactly as before; only the surface detail is new. v1 keeps mech_DV3_olive.
    /// Menu 1 swaps the two slots in (<see cref="HousingSwap"/>), menu 2 counts them (<see cref="HousingPair"/>, <see cref="HousingCheck"/>).
    /// </summary>
    public static partial class OzMechV2bSetup
    {
        public const string V1Housing = Mats + "mech_DV3_olive.mat", V2Housing = Mats + "mech_v2b_housing.mat";
        public const string V2HousingPicture = "Assets/OpenZ/Models/Legacy/Toy/mech_v2b_housing.png";
        /// <summary>The two renderers that wear the housing paint (the shoulder blocks).</summary>
        public static readonly string[] HousingNodes = { "MechRocketHousing_L", "MechRocketHousing_R" };

        static bool IsHousing(Renderer r) => r && System.Array.IndexOf(HousingNodes, r.name) >= 0;

        /// <summary>Menu 1: on a housing renderer, v1's housing material in slot i becomes the v2's (true when the slot changed).</summary>
        static bool HousingSwap(Renderer r, Material[] ms, int i)
        {
            if (!IsHousing(r) || !ms[i]) return false;
            var h1 = AssetDatabase.LoadAssetAtPath<Material>(V1Housing); var h2 = AssetDatabase.LoadAssetAtPath<Material>(V2Housing);
            if (!h1 || !h2 || ms[i] != h1) return false;
            ms[i] = h2;
            return true;
        }

        /// <summary>Menu 2: the slot pair (v1's material a, v2's material b) on the v2 renderer rb is the housing swap.</summary>
        static bool HousingPair(Renderer rb, Material a, Material b)
            => IsHousing(rb) && a && b && AssetDatabase.GetAssetPath(a) == V1Housing && AssetDatabase.GetAssetPath(b) == V2Housing;

        /// <summary>Menu 2: both housing slots swapped, the v2 housing material = v1's in every setting but its picture, the picture found.</summary>
        static bool HousingCheck(int swapped, out string what)
        {
            var h1 = AssetDatabase.LoadAssetAtPath<Material>(V1Housing); var h2 = AssetDatabase.LoadAssetAtPath<Material>(V2Housing);
            var diffs = new List<string>();
            if (!h1 || !h2) diffs.Add(!h1 ? "no " + V1Housing : "no " + V2Housing);
            var sh = h1 ? h1.shader : null;
            if (h1 && h2 && h2.shader != sh) diffs.Add("shader");
            for (int i = 0; h1 && h2 && sh && i < sh.GetPropertyCount(); i++)
            {
                string n = sh.GetPropertyName(i); var ty = sh.GetPropertyType(i);
                if (ty == UnityEngine.Rendering.ShaderPropertyType.Float || ty == UnityEngine.Rendering.ShaderPropertyType.Range) { if (!Mathf.Approximately(h1.GetFloat(n), h2.GetFloat(n))) diffs.Add(n); }
                else if (ty == UnityEngine.Rendering.ShaderPropertyType.Color || ty == UnityEngine.Rendering.ShaderPropertyType.Vector) { if (h1.GetVector(n) != h2.GetVector(n)) diffs.Add(n); }
                else if (ty == UnityEngine.Rendering.ShaderPropertyType.Texture && n != "_BaseMap") { if (h1.GetTexture(n) != h2.GetTexture(n)) diffs.Add(n); }
            }
            if (h1 && h2 && !h1.shaderKeywords.OrderBy(x => x).SequenceEqual(h2.shaderKeywords.OrderBy(x => x))) diffs.Add("keywords");
            var tex = h2 ? h2.GetTexture("_BaseMap") : null;
            bool pic = tex && AssetDatabase.GetAssetPath(tex) == V2HousingPicture;
            what = $"the shoulder housings: {swapped} slot(s) mech_DV3_olive -> mech_v2b_housing on {string.Join(" / ", HousingNodes)} (2 expected); "
                 + $"mech_v2b_housing: v1's mech_DV3_olive in every setting ({(diffs.Count > 0 ? "DIFFERENT: " + string.Join(", ", diffs) : "0 differences")}), "
                 + $"its picture {(tex ? AssetDatabase.GetAssetPath(tex) + " " + tex.width + "x" + tex.height : "none")} (the team paint with the wear)";
            return swapped == 2 && diffs.Count == 0 && pic;
        }
    }
}
#endif
