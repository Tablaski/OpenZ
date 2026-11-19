using System;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit ue_explosions_6 (View/OzUeBlastFx.cs): the Unit editor's six test explosions draw through the game's own particle
    /// pipeline (pooled particle channels, streaks, glows, the ground rings; their smoke and fire through Resources/OpenZ/FX/OzUeBlastFx.shader), on channels of their
    /// own so they never eat the levels' pools. The channels are made the first time a test explosion is dropped in a scene (never in
    /// a level: only the Unit editor's tiles ask for them) and go with this player when the scene is left.
    /// </summary>
    public partial class OpenZFxPlayer
    {
        /// <summary>Checks: the test explosions' channels made on this player.</summary>
        public int UeBlastChannels { get; private set; }

        /// <summary>Makes one more channel from a definition (the catalog's own builder). dress = the caller's own finish of the
        /// channel's material, particle system and renderer (the test explosions draw their smoke and fire with their own shader and
        /// flipbooks, and set their own curves). True when the channel is there (made now or before).</summary>
        public bool UeAddChannel(FxChannelDef d, Action<Material, ParticleSystem, ParticleSystemRenderer> dress = null)
        {
            if (string.IsNullOrEmpty(d.name)) return false;
            if (channels.ContainsKey(d.name)) return true;
            var c = CreateChannel(d);
            if (c == null) return false;
            if (dress != null && c.mat && c.ps && c.r) dress(c.mat, c.ps, c.r);
            channels[d.name] = c;
            UeBlastChannels++;
            return true;
        }

        /// <summary>The blasts' additive ground ring (radius grows to radiusM over life, after delay), when SHOCK RINGS are on.</summary>
        public void UeRing(Vector3 flat, float radiusM, float life, float delay, Color color)
        {
            if (shockRings) RingLeg(flat, radiusM, life, delay, color);
        }

        /// <summary>Checks: the live particles of every channel whose name starts with prefix.</summary>
        public int UeParticles(string prefix)
        {
            int n = 0;
            foreach (var kv in channels)
                if (kv.Value != null && kv.Value.ps && kv.Key.StartsWith(prefix, StringComparison.Ordinal)) n += kv.Value.ps.particleCount;
            return n;
        }
    }
}
