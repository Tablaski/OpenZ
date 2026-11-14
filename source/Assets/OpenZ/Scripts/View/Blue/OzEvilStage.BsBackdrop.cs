// OpenZ (GPL-3.0)
// BS-01's shots look down at him from EvilZod's side of the lens: behind him the frame
// holds the deck and the wall only up to about 1.3 m, from the doors to x 4.6, and there the console reads as a plain box with the
// dim green scope as a round spot. The same pieces as EvilZod's own background are put where that camera looks: two short banners on
// stands in front of the console (a red star banner and a blue eagle banner, the stage's own cloth, swaying like the others), the
// round eagle-and-star emblem on the console's face (over the scope), a soft warm light from the floor on them. Darker and softer than
// BS-01 (the stage's depth of field blurs them as it does EvilZod's banners). EvilZod's side of the room is not touched.
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzEvilStage
    {
        /// <summary>The light on BS-01's banners and emblem (intensity, range m).</summary>
        public static float BsBackdropLightI = 2.2f, BsBackdropLightRange = 3.6f;

        /// <summary>Called at the end of BuildSet, after the military dressing.</summary>
        void BuildBsBackdrop(Material rodM, Material capM)
        {
            var blueTex = Resources.Load<Texture2D>(SetFolder + "evil_banner");
            var redTex = Resources.Load<Texture2D>(SetFolder + "evil_banner_red");
            var blueM = blueTex ? CutoutTwoSided(blueTex) : Lit(new Color32(36, 70, 150, 255), 0.2f, 0f);
            var redM = redTex ? CutoutTwoSided(redTex) : Lit(new Color32(196, 30, 28, 255), 0.2f, 0f);
            const float standZ = -2.42f, topY = 1.62f, h = 1.30f, w = 0.52f;
            BsStand("BsBannerRed", redM, new Vector3(1.62f, topY, standZ), w, h, rodM, capM, 0.7f);
            BsStand("BsBannerBlue", blueM, new Vector3(3.10f, topY, standZ), w, h, rodM, capM, 2.3f);
            var emTex = Resources.Load<Texture2D>(SetFolder + "evil_emblem");
            if (emTex) Quad("BsConsoleEmblem", new Vector3(2.35f, 0.58f, -2.578f), new Vector2(0.62f, 0.62f), Cutout(emTex));
            AddLight("BsBackdropLight", LightType.Spot, new Vector3(2.36f, 0.15f, -1.55f), new Vector3(2.36f, 1.05f, -2.70f),
                     new Color(1f, 0.82f, 0.62f), BsBackdropLightI, BsBackdropLightRange, 75f);
        }

        /// <summary>A banner on a free-standing pole: the pole and its foot under the rod, the cloth as every other banner (Banner()).</summary>
        void BsStand(string name, Material cloth, Vector3 top, float w, float h, Material rodM, Material capM, float phase)
        {
            Banner(name, cloth, top, w, h, 0f, rodM, capM, phase);
            Box(name + "Pole", new Vector3(top.x, top.y * 0.5f, top.z - 0.035f), new Vector3(0.035f, top.y, 0.035f), rodM);
            Box(name + "Foot", new Vector3(top.x, 0.02f, top.z - 0.035f), new Vector3(0.26f, 0.04f, 0.18f), rodM);
        }
    }
}
