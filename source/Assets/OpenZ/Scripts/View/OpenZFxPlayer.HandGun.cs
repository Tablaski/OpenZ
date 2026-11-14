using UnityEngine;

namespace OpenZ
{
    /// <summary>DV146, the mech's hand gun (View/MechHandGunAim.cs): how many particles a channel holds now and whether it has room for one
    /// more (a channel's pool is fixed; a particle emitted into a full pool is not drawn), so the muzzle flash can be counted as really
    /// emitted and fall back to a channel with room.</summary>
    public partial class OpenZFxPlayer
    {
        /// <summary>The live particles of the channel (-1 = no such channel).</summary>
        public int ChannelCount(string channel)
        {
            var c = Ch(channel);
            return c != null && c.ps ? c.ps.particleCount : -1;
        }

        /// <summary>The channel exists and its pool has room for one more particle now.</summary>
        public bool ChannelRoom(string channel)
        {
            var c = Ch(channel);
            return c != null && c.ps && c.ps.particleCount < c.ps.main.maxParticles;
        }
    }
}
