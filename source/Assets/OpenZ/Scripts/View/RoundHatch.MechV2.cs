using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public partial class RoundHatch
    {
        /// <summary>The mech v2's hatch pictures (Resources/OpenZ/MechV2/): the lid (worn light grey metal, a bolted rim, a slot in the middle)
        /// and the plate (darker grey, a bolt in each corner; the collar's ring is drawn in it too).</summary>
        public const string LidPicture = "mech_v2b_hatch_lid", PlatePicture = "mech_v2b_hatch_plate";
        /// <summary>The lid's hinge knuckle and grab handle on the v2: the plate picture's mean grey (sRGB).</summary>
        static readonly Color PlateGrey = new Color32(104, 103, 99, 255);
        static readonly Dictionary<Material, Material[]> v2Mats = new Dictionary<Material, Material[]>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMechV2() => v2Mats.Clear();

        /// <summary>The mech v2 (View/OzMech.cs: the v1 model with its own texture): the round hatch built here for the mech, exactly as on
        /// v1 (the same meshes, sizes, hinge and material settings, never team paint), takes the v2's two pictures on planar top-down UVs:
        /// the lid on its disc, the plate and the collar on the plate's rounded square. Nothing changes for v1 or any other unit.</summary>
        void MechV2Look(Transform root)
        {
            if (spec == null || spec.prefix != "mech_" || !Frame || !Hinge || !OzMech.Models.IsV2(root)) return;
            var lidTex = Resources.Load<Texture2D>(OzMech.Dir + LidPicture); var plateTex = Resources.Load<Texture2D>(OzMech.Dir + PlatePicture);
            if (!lidTex || !plateTex) { Debug.LogWarning("[OpenZ] RoundHatch: the mech v2 hatch pictures are missing in Resources/" + OzMech.Dir); return; }
            MeshRenderer lid = null, plate = null, collar = null;
            foreach (var mr in Frame.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.name == "OZ_Hatch_Plate") plate = mr;
                else if (mr.name == "OZ_Hatch_Collar") collar = mr;
            }
            foreach (var mr in Hinge.GetComponentsInChildren<MeshRenderer>(true)) if (mr.name == "OZ_Hatch_Lid") lid = mr;
            if (!lid || !plate || !collar || lid.sharedMaterials.Length < 2 || !lid.sharedMaterials[0] || !plate.sharedMaterial) return;
            var m = MechV2Mats(lid.sharedMaterials[0], plate.sharedMaterial, lidTex, plateTex);
            if (!Planar(plate, spec.plateHx, spec.plateHz, 0f) || !Planar(collar, spec.plateHx, spec.plateHz, 0f) || !Planar(lid, spec.lidR, spec.lidR, -(spec.rOut + 0.02f))) return;
            plate.sharedMaterials = new[] { m[1] };
            var cm = collar.sharedMaterials; cm[0] = m[1]; collar.sharedMaterials = cm;          // the well (its second material) stays near-black
            var lm = lid.sharedMaterials; lm[0] = m[0]; lm[1] = m[2]; lid.sharedMaterials = lm;
        }

        /// <summary>Planar top-down UVs on a mesh built for this hatch (its own instance): u = 0.5 + x / (2 hx), v = 0.5 + (z - cz) / (2 hz).</summary>
        static bool Planar(MeshRenderer mr, float hx, float hz, float cz)
        {
            var mf = mr ? mr.GetComponent<MeshFilter>() : null;
            var mesh = mf ? mf.sharedMesh : null;
            if (!mesh) return false;
            var v = mesh.vertices; var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++) uv[i] = new Vector2(0.5f + v[i].x / (2f * hx), 0.5f + (v[i].z - cz) / (2f * hz));
            mesh.uv = uv;
            return true;
        }

        /// <summary>[0] the lid with its picture, [1] the plate / collar metal with its picture, [2] the knuckle / handle metal in the plate's
        /// grey: clones of the hatch's own lid and metal materials (once per source), only the picture and a white base colour changed.</summary>
        static Material[] MechV2Mats(Material lidSrc, Material metalSrc, Texture2D lidTex, Texture2D plateTex)
        {
            if (v2Mats.TryGetValue(lidSrc, out var m) && m[0] && m[1] && m[2]) return m;
            m = new[] { Pictured(lidSrc, "OZ_HatchLid_v2", lidTex), Pictured(metalSrc, "OZ_HatchMetal_v2", plateTex), new Material(metalSrc) { name = "OZ_HatchMetal_v2_plain" } };
            if (m[2].HasProperty(BaseColorId)) m[2].SetColor(BaseColorId, PlateGrey);
            v2Mats[lidSrc] = m;
            return m;
        }

        static Material Pictured(Material src, string name, Texture2D tex)
        {
            var m = new Material(src) { name = name };
            if (m.HasProperty(BaseMapId)) { m.SetTexture(BaseMapId, tex); m.SetTextureScale(BaseMapId, Vector2.one); m.SetTextureOffset(BaseMapId, Vector2.zero); }
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, Color.white);
            return m;
        }
    }
}
