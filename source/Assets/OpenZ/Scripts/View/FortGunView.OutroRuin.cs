// OpenZ (GPL-3.0)
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 ending_fort_ruin: StartToss on a FALLEN fort (FortFallAtGameOver.Fallen). Thrown by the ending's beat (OutroBlastBeat, the fort
    /// stands on through the talk): the modelled wrecked mount takes the pedestal's place (FortGunMount, exactly as for a cannon shot off a
    /// standing fort: the broken ring, the pit, the soot mark, its fire and smoke; it goes with the fort's body at the collapse) and the fort
    /// turns to its heavy ruin with a burning socket (View/OzFortDamage.Ruin.cs). Thrown with the fort's own collapse (the bomb lands, a
    /// strike-off ending): nothing, as before (open_fixes_3nov row 77). Hook: one line in StartToss (FortGunView.cs). Switch: OzFortDamage.RuinOn.
    /// </summary>
    public partial class FortGunView
    {
        /// <summary>Proof counters: wrecked mounts built by the outro blast, forts it turned to the heavy ruin.</summary>
        public static int OutroMounts, OutroRuins;

        /// <summary>StartToss, on a fallen fort, before the cannon leaves its socket (yaw still seated on the roof).</summary>
        void OutroRuinSocket()
        {
            if (!outroBlast || !OzFortDamage.RuinOn || Fort == null) return;
            var socket = yaw ? yaw.position : transform.position;
            var m = FortGunMount.Build(Fort, transform);
            if (m) { socket = m.Centre; OutroMounts++; }
            if (OzFortDamage.Ruin(Fort.Id, socket, "its mega cannon blown off before the ending scene")) OutroRuins++;
        }
    }
}
