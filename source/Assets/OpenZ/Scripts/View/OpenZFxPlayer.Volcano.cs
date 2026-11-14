// OpenZ (GPL-3.0) - version 1.3, level 7: the two particle pools of the eruptions (LV_CONTRACT.md section 6), built only on a map with lava, never in
// OpenZFxCatalog.Channels. How each is drawn and why: OzVolcano.cs, top comment, and LV4/README.md.
using UnityEngine;

namespace OpenZ
{
    public partial class OpenZFxPlayer
    {
        Channel chVolFountain, chVolAsh;

        /// <summary>From OzVolcano.Boot. Safe to call twice.</summary>
        public void EnsureVolcanoChannels()
        {
            if (chVolFountain != null) return;
            Color core = OzLavaRampTable.Stops[4];
            chVolFountain = CreateChannel(new FxChannelDef
            {
                name = "lava_fountain", shape = FxShape.Streak, capacity = 640, queue = 3034, opaqueness = 0.6f, tint = Color.white,
                intensity = 4.5f, coreColor = core, coreMix = 0.4f, squareness = 0.2f, stretch = true, lengthScale = 1.5f, gravity = OzVolcanoArc.G, minWidth = 0.07f,
                sizeCurve = FxSizeCurve.Lerp, sizeEnd = 0.55f, alphaCurve = FxAlphaCurve.One,
            });
            if (chVolFountain != null)
            {
                chVolFountain.mat.SetFloat("_StreakTaper", 0.8f);
                chVolFountain.r.velocityScale = 0.055f;                                                          // long while it flies fast, a round blob at the top of its arc
                var col = chVolFountain.ps.colorOverLifetime; col.color = new ParticleSystem.MinMaxGradient(CoolingClot());
            }
            chVolAsh = CreateChannel(new FxChannelDef
            {
                name = "lava_ash", shape = FxShape.Puff, capacity = 260, queue = 3012, opaqueness = 1f, tint = Color.white, intensity = 1f,
                lit = 0.75f, wrap = 0.35f, edgeSoft = 0.3f, softFade = 1.2f, sortByDistance = true,
                noiseAmt = 0.5f, noiseScale = 2.2f, boil = 1.1f, spin = 0.5f, bump = 0.4f, selfShadow = 0.85f,                 // the coarse billow look of blast_smoke_b
                ageColor = new Color(0.55f, 0.52f, 0.5f, 1f), heatColor = new Color(1.4f, 0.42f, 0.07f, 1f), heatTime = 0.4f,     // lit orange from below: a puff is low while it is young
                gravity = 0f, drag = 0f, sizeCurve = FxSizeCurve.Lerp, sizeEnd = 2f, alphaCurve = FxAlphaCurve.BillowFade,      // no drag: with any, a puff at 4.4 m/s stops near 9 m and the column never reaches its 18 m
            });
        }

        /// <summary>The one ramp over a clot's life. The sRGB stops themselves: this renderer turns particle colours from sRGB to linear (applyActiveColorSpace
        /// in CreateChannel), so OzLavaRampTable.Cooling's linear keys would be converted twice.</summary>
        static Gradient CoolingClot()
        {
            var s = OzLavaRampTable.Stops;
            var g = new Gradient { mode = GradientMode.Blend };
            g.SetKeys(new[] { new GradientColorKey(s[4], 0f), new GradientColorKey(s[3], 0.3f), new GradientColorKey(s[2], 0.62f), new GradientColorKey(s[0], 0.9f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.72f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        /// <summary>One clot of the fountain: a real arc under gravity, its colour cools over its life.</summary>
        public void VolcanoClot(Vector3 at, Vector3 vel, float life, float width, Color tint) => EmitP(chVolFountain, at, vel, life, width, tint, tint.a);

        /// <summary>One puff of ash, at most 2 m wide at birth: it doubles over its life.</summary>
        public void VolcanoAsh(Vector3 at, Vector3 vel, float life, float width, Color tint, float alpha) => EmitP(chVolAsh, at, vel, life, Mathf.Min(width, 3.6f), tint, alpha);

        /// <summary>Check: live clots and puffs, and the screens of ash now: the sum of the puffs' drawn areas over the screen's area.</summary>
        public void VolcanoLive(Camera c, out int clots, out int puffs, out float ashScreens)
        {
            clots = chVolFountain != null ? chVolFountain.ps.particleCount : 0; puffs = 0; ashScreens = 0f;
            if (chVolAsh == null) return;
            if (puffBuf == null || puffBuf.Length < chVolAsh.def.capacity) puffBuf = new ParticleSystem.Particle[Mathf.Max(chVolAsh.def.capacity, 1500)];
            puffs = chVolAsh.ps.GetParticles(puffBuf);
            if (!c) return;
            float k = Screen.height / (2f * Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad)), sum = 0f;
            for (int i = 0; i < puffs; i++)
            {
                var p = puffBuf[i]; float d = Vector3.Distance(c.transform.position, p.position);
                if (d < 1f) continue;
                float px = p.GetCurrentSize(chVolAsh.ps) * k / d; sum += px * px * 0.785f;
            }
            ashScreens = sum / Mathf.Max(1f, Screen.width * (float)Screen.height);
        }
    }
}
