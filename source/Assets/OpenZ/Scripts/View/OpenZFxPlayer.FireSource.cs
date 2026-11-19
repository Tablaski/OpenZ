// OpenZ (GPL-3.0)
using UnityEngine;

namespace OpenZ
{
    public partial class OpenZFxPlayer
    {
        /// <summary>
        /// <see cref="WreckFire"/> standing on a given foot (View/OzFireSource.cs): the same light, heat-ramp core, 9 rising glow flames and 12
        /// dark smoke puffs, but measured up from `foot` (WreckFire: from the ground under the point) and with its heights and its smoke's
        /// spread x heightK (WreckFire: a vehicle's 0.85 m core height whatever the scale). heightK 1 on a foot on the ground = WreckFire.
        /// </summary>
        public void WreckFireAt(Vector3 foot, float scale, float heightK)
        {
            float px = OpenZFxCatalog.LegPxToM;
            float gy = foot.y, ch = 0.85f * heightK;
            var center = new Vector3(foot.x, gy + ch, foot.z);
            var L = OpenZFxCatalog.WreckFireLight;
            if (pointLights) AddLight(center, L.color, L.intensityThree * scale, L.range * scale, L.life, L.decayPow, L.flicker, false);
            var wreckCore = Ch("blast_core");
            if (wreckCore != null) EmitP(wreckCore, center, Vector3.zero, 0.43f, 0.85f * scale * 2f * OpenZFxCatalog.WreckCoreSizeMul, OpenZFxCatalog.WreckCore, 1f);
            else EmitP(Ch("m12_core"), center, Vector3.zero, 0.43f, 0.85f * scale * 2f, OpenZFxCatalog.WreckCore, 1f);
            var fire = Ch("leg_fire");
            for (int i = 0; i < 9; i++)
            {
                float a = Rand() * Mathf.PI * 2f, spread = (0.18f + Rand() * 0.72f) * scale;
                var pos = new Vector3(foot.x + Mathf.Cos(a) * spread, gy + ch * (0.55f + Rand() * 0.7f), foot.z + Mathf.Sin(a) * spread);
                var vel = new Vector3(Mathf.Cos(a) * (0.35f + Rand() * 1.1f), 0.9f + Rand() * 2.2f, Mathf.Sin(a) * (0.35f + Rand() * 1.1f));
                var c = i % 3 == 0 ? OpenZFxCatalog.WreckFlameA : i % 2 == 1 ? OpenZFxCatalog.WreckFlameB : OpenZFxCatalog.WreckFlameC;
                EmitP(fire, pos, vel, 0.38f + Rand() * 0.36f, (11f + Rand() * 9f) * scale * px, c, 1f, i * 0.024f + Rand() * 0.06f);
            }
            var sm = Ch("leg_smoke");
            for (int i = 0; i < 12; i++)
            {
                float a = Rand() * Mathf.PI * 2f, h = 0.45f + Rand() * 1.8f;
                var pos = new Vector3(foot.x + (Rand() - 0.5f) * 1.5f * heightK, gy + (0.25f + Rand() * 1.1f) * heightK, foot.z + (Rand() - 0.5f) * 1.5f * heightK);
                EmitP(sm, pos, new Vector3(Mathf.Cos(a) * h, 0.5f + Rand() * 1.8f, Mathf.Sin(a) * h), 0.72f + Rand() * 0.65f, (8f + Rand() * 8f) * px * OpenZFxCatalog.LegSmokeScale,
                    i % 3 == 0 ? OpenZFxCatalog.WreckSmokeDark : OpenZFxCatalog.WreckSmokeGrey, 0.95f, 0.12f + Rand() * 0.18f);
            }
        }
    }
}
