using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Level 7: the lava's own particle channels (all named lava_*), built only when OzLava boots: never in OpenZFxCatalog.Channels, so no other
    /// level pays for them and combat's channels are never starved. A particle's colour clamps at 1, so each channel's HDR is its fixed
    /// intensity and the cooling comes from the one ramp (OzLavaRampTable.Cooling). Half the particles under LIGHTEST.
    /// </summary>
    public partial class OpenZFxPlayer
    {
        public const int LavaDropCapacity = 800, LavaSmokeCapacity = 300, LavaSteamCapacity = 200, LavaEmberCapacity = 160;

        Channel lavaDrop, lavaSmoke, lavaSteam, lavaEmber;
        /// <summary>OzLava polls LIGHTEST at most once a second and sets this.</summary>
        public bool LavaLite;
        int lavaSkip;

        /// <summary>From OzLava's boot; safe to call again. Level 7 only.</summary>
        public void EnsureLavaChannels()
        {
            if (lavaDrop != null) return;
            lavaDrop = CreateChannel(new FxChannelDef { name = "lava_drop", shape = FxShape.Streak, capacity = LavaDropCapacity, queue = 3006, opaqueness = 0.5f, tint = Color.white,
                intensity = 3.5f, coreColor = Color.white, coreMix = 0f, squareness = 0.5f, stretch = true, lengthScale = 2.6f, gravity = 9.81f, minWidth = 0.05f,
                sizeCurve = FxSizeCurve.Constant, alphaCurve = FxAlphaCurve.One });
            lavaEmber = CreateChannel(new FxChannelDef { name = "lava_ember", shape = FxShape.Glow, capacity = LavaEmberCapacity, queue = 3044, opaqueness = 0f, tint = Color.white,
                intensity = 5f, edgeSoft = 0.5f, gravity = -0.35f, drag = 0.992f, minWidth = 0.05f, sizeCurve = FxSizeCurve.Lerp, sizeEnd = 0.55f, alphaCurve = FxAlphaCurve.One });
            lavaSmoke = CreateChannel(new FxChannelDef { name = "lava_smoke", shape = FxShape.Puff, capacity = LavaSmokeCapacity, queue = 3009, opaqueness = 1f, tint = new Color(0.20f, 0.17f, 0.16f, 1f),
                intensity = 1f, lit = 1f, wrap = 0.4f, edgeSoft = 0.45f, softFade = 1.2f, sortByDistance = true, noiseAmt = 0.3f, noiseScale = 2f, boil = 0.5f, spin = 0.4f, bump = 0.3f, selfShadow = 0.5f,
                ageColor = new Color(0.7f, 0.66f, 0.62f, 1f), heatColor = new Color(0.8f, 0.26f, 0.05f, 1f), heatTime = 0.2f,
                gravity = -0.3f, drag = 0.99f, sizeCurve = FxSizeCurve.SmokeGrow, alphaCurve = FxAlphaCurve.SmokeFade });
            lavaSteam = CreateChannel(new FxChannelDef { name = "lava_steam", shape = FxShape.Puff, capacity = LavaSteamCapacity, queue = 3009, opaqueness = 1f, tint = new Color(0.86f, 0.84f, 0.82f, 1f),
                intensity = 1f, lit = 1f, wrap = 0.5f, edgeSoft = 0.5f, softFade = 1.2f, sortByDistance = true, noiseAmt = 0.3f, noiseScale = 1.6f, boil = 0.6f, spin = 0.5f, bump = 0.2f, selfShadow = 0.3f,
                gravity = -0.9f, drag = 0.985f, sizeCurve = FxSizeCurve.SmokeGrow, alphaCurve = FxAlphaCurve.PopFade });
            Cools(lavaDrop, 1f, 0.42f, 0.6f); Cools(lavaEmber, 1f, 0.45f, 0f);
        }

        /// <summary>The ramp over a particle's life. Its keys are linear already, so this channel must not convert them a second time.</summary>
        static void Cools(Channel c, float from, float to, float alphaEnd)
        {
            if (c == null) return;
            var col = c.ps.colorOverLifetime; col.color = new ParticleSystem.MinMaxGradient(OzLavaRampTable.Cooling(from, to, alphaEnd));
            c.r.applyActiveColorSpace = false;
        }

        /// <summary>Live particles of a lava channel, the check reads it: 0 drop, 1 smoke, 2 steam, 3 ember.</summary>
        public int LavaLive(int channel)
        {
            var c = channel == 0 ? lavaDrop : channel == 1 ? lavaSmoke : channel == 2 ? lavaSteam : lavaEmber;
            return c != null ? c.ps.particleCount : 0;
        }

        /// <summary>Under LIGHTEST every second soft puff is left out.</summary>
        bool LavaThin() => LavaLite && (++lavaSkip & 1) == 0;

        /// <summary>Droplets on short arcs. s: their reach (1 = a small bubble's).</summary>
        public void LavaDroplets(Vector3 at, float s, int count)
        {
            if (lavaDrop == null) return;
            if (LavaLite) count = (count + 1) / 2;
            float width = 0.05f + 0.025f * Mathf.Sqrt(Mathf.Max(0.1f, s));
            for (int i = 0; i < count; i++)
            {
                float a = Rand() * Mathf.PI * 2f, side = s * (0.5f + Rand() * 0.9f), up = s * (1.6f + Rand() * 1.7f);
                EmitP(lavaDrop, at + Vector3.up * 0.05f, new Vector3(Mathf.Cos(a) * side, up, Mathf.Sin(a) * side), 2f * up / 9.81f + 0.04f, width * (0.8f + Rand() * 0.5f), Color.white, 1f);
            }
        }

        /// <summary>A bubble's burst as the player sees it from the game camera: fat sparks thrown 1-2 m up and out.</summary>
        public void LavaSparks(Vector3 at, float r, int count)
        {
            if (lavaDrop == null) return;
            if (LavaLite) count = (count + 1) / 2;
            for (int i = 0; i < count; i++)
            {
                float a = Rand() * Mathf.PI * 2f, side = 1.2f + Rand() * 2.2f, up = 3.2f + Rand() * 3f;
                EmitP(lavaDrop, at + new Vector3(Mathf.Cos(a) * r * 0.6f, 0.1f, Mathf.Sin(a) * r * 0.6f), new Vector3(Mathf.Cos(a) * side, up, Mathf.Sin(a) * side), 2f * up / 9.81f + 0.06f, 0.13f + Rand() * 0.09f, Color.white, 1f);
            }
        }

        /// <summary>A giant bubble's fountain: droplets straight up, about <paramref name="high"/> m, thrown over half a second.</summary>
        public void LavaSpout(Vector3 at, float high, int count)
        {
            if (lavaDrop == null) return;
            if (LavaLite) count = (count + 1) / 2;
            float up = Mathf.Sqrt(2f * 9.81f * Mathf.Max(0.2f, high));
            for (int i = 0; i < count; i++)
            {
                float a = Rand() * Mathf.PI * 2f, side = 0.3f + Rand() * 1.1f, v = up * (0.72f + Rand() * 0.28f);
                EmitP(lavaDrop, at + new Vector3(Mathf.Cos(a) * 0.3f, 0.08f, Mathf.Sin(a) * 0.3f), new Vector3(Mathf.Cos(a) * side, v, Mathf.Sin(a) * side), 2f * v / 9.81f + 0.05f, 0.075f + Rand() * 0.05f, Color.white, 1f, Rand() * 0.5f);
            }
        }

        /// <summary>One thin dark puff.</summary>
        public void LavaSmoke(Vector3 at, float s)
        {
            if (lavaSmoke == null || LavaThin()) return;
            EmitP(lavaSmoke, at, new Vector3((Rand() - 0.5f) * 0.3f, 0.3f + Rand() * 0.35f, (Rand() - 0.5f) * 0.3f), 2.8f + Rand() * 1.6f, s * (0.9f + Rand() * 0.6f), Color.white, 0.32f + Rand() * 0.14f);
        }

        /// <summary>The smoke of a leg in lava: thicker and quicker up than a bubble's, it must read at the game camera.</summary>
        public void LavaLegSmoke(Vector3 at, float s)
        {
            if (lavaSmoke == null || LavaThin()) return;
            EmitP(lavaSmoke, at, new Vector3((Rand() - 0.5f) * 0.4f, 1.1f + Rand() * 0.8f, (Rand() - 0.5f) * 0.4f), 2.6f + Rand() * 1.4f, s * (1.1f + Rand() * 0.6f), Color.white, 0.62f + Rand() * 0.18f);
        }

        /// <summary>One pale puff of steam.</summary>
        public void LavaSteam(Vector3 at, float s)
        {
            if (lavaSteam == null || LavaThin()) return;
            EmitP(lavaSteam, at, new Vector3((Rand() - 0.5f) * 0.4f, 0.8f + Rand() * 0.8f, (Rand() - 0.5f) * 0.4f), 1.4f + Rand() * 1.1f, s * (0.6f + Rand() * 0.4f), Color.white, 0.36f + Rand() * 0.14f);
        }

        /// <summary>Embers rising off the lava.</summary>
        public void LavaEmber(Vector3 at, int count)
        {
            if (lavaEmber == null) return;
            if (LavaLite) { if (count > 1) count = (count + 1) / 2; else if (LavaThin()) return; }
            for (int i = 0; i < count; i++)
                EmitP(lavaEmber, at + Vector3.up * 0.1f, new Vector3((Rand() - 0.5f) * 0.7f, 0.7f + Rand() * 1.1f, (Rand() - 0.5f) * 0.7f), 3f + Rand() * 2f, 0.06f + Rand() * 0.06f, Color.white, 1f);
        }

        /// <summary>A ring running out on the lava, no scar. No lava on this map: nothing.</summary>
        public void LavaRing(Vector3 at, float radius, float life)
        {
            if (OzLava.I) OzLava.I.RingAt(at, radius, life);
        }
    }
}
