// OpenZ (GPL-3.0) - version 1.3: the one colour ramp for hot metal on a level with no lava. View only, no random. Reads the lava's tuned numbers, changes none of them.
using UnityEngine;

namespace OpenZ
{
    public partial class OzLava
    {
        static readonly Vector4[] fireRamp = new Vector4[OzLavaIds.RampStops];

        /// <summary>On a level with no lava the ramp is all zero and TeamLit's legs would go dark, not glow. OzFireHeat calls this at its first write of such a level: the same tuned
        /// stops and gains as level 7, written to the global only. OzLavaRampTable stays as it is, so the lava's boot and Cooling read what they always read. A level with no fire never sets it.</summary>
        public static void PushFireRamp()
        {
            fireRamp[0] = Stop(OzLavaRampTable.Stops[0], 0); fireRamp[1] = Stop(OzLavaRampTable.Stops[1], 1);
            fireRamp[2] = Stop(LavaRedStop, 2); fireRamp[3] = Stop(LavaMoltenStop, 3); fireRamp[4] = Stop(LavaCoreStop, 4);
            Shader.SetGlobalVectorArray(OzLavaIds.Ramp, fireRamp);
        }

        static Vector4 Stop(Color32 stop, int i) { Color c = stop; c = c.linear; return new Vector4(c.r, c.g, c.b, LavaGains[i]); }
    }
}
