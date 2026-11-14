using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// DV152, level 7 "Pyro Technics": the glow of a robot standing in lava. 0 = cold (every robot everywhere, always) .. 1 = melting.
    /// Robot bodies only: TeamLit draws _OZHeat as a red-orange glow that is strongest at the feet (from the lava level _OZLavaY, a global
    /// set by View/OzLava.cs) and climbs the body as the heat rises; above 1 in HDR, so the bloom picks it up. The value comes from the
    /// simulation (ZObject.LavaHeat: lava HP lost / (that + HP left)), so the glow follows the robot's health; View/OzLava.cs sets it.
    /// </summary>
    public partial class TeamPaint
    {
        static readonly int HeatId = Shader.PropertyToID("_OZHeat");
        float heat, heatShown; Renderer[] heatParts; int[] heatSlots; MaterialPropertyBlock heatBlock;

        /// <summary>0 cold .. 1 melting. The value is kept every time (a later Apply writes it); the renderers are written in steps of 1/100
        /// (and always on the way to exactly 0 or 1) through their blocks, never through Apply: this runs every frame for a robot in lava.</summary>
        public float Heat
        {
            get => heat;
            set
            {
                heat = Mathf.Clamp01(value);
                if (Mathf.Abs(heat - heatShown) < 0.01f && !(heat == 0f && heatShown != 0f) && !(heat == 1f && heatShown != 1f)) return;
                if (heatShown == 0f || heatParts == null) BindHeat();                                // a new dip: parts added since then count too
                heatShown = heat;
                for (int i = 0; i < heatParts.Length; i++)
                {
                    var r = heatParts[i]; if (!r) continue;
                    for (int m = 0; m < heatSlots[i]; m++) { r.GetPropertyBlock(heatBlock, m); heatBlock.SetFloat(HeatId, heat); r.SetPropertyBlock(heatBlock, m); }
                }
            }
        }

        /// <summary>The body's renderers, by the rule Apply uses: this robot's own, never a figure riding on it. Once per dip.</summary>
        void BindHeat()
        {
            heatBlock ??= new MaterialPropertyBlock();
            var all = GetComponentsInChildren<Renderer>(true); int n = 0;
            heatParts = new Renderer[all.Length]; heatSlots = new int[all.Length];
            foreach (var r in all)
            {
                var host = r.GetComponentInParent<TeamPaint>(true);
                if (!(host ? host : this).IsRobotBody || (host != this && !IsRobotBody)) continue;
                heatParts[n] = r; heatSlots[n] = r.sharedMaterials.Length; n++;
            }
            System.Array.Resize(ref heatParts, n);
        }
    }
}
