using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 owner item 132 for the mech (its waist is its turret), v1 AND v2: the mech v2 Play
    /// checks still counted one flutter in the fight film and one in three of the waist test's lines (the mech was in neither of the turret's
    /// rest lists). The mech now rests after a shot that waited for its gun, as the light tank does (k84b's UnitAim.ShotSettleTypes), and never
    /// starts back the other way within 0.3 s of its last turn while its goal is only a little back, as the medium tank, the MLRS and the gun
    /// do (k87b's UnitAim.TurnRestTypes). Replayed off the engine on the recorded goals of those takes (the kit's tools/replay_turret_k92.py):
    /// flutters 3 -> 0, the combat take 0 -> 0. Both lists are kept as k84b / k87b wrote them; the mech's entry is added once at the start
    /// of every session. View only.
    /// </summary>
    public partial class UnitAim
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void K92MechTypes() { ShotSettleTypes.Add(OzMech.Type); TurnRestTypes.Add(OzMech.Type); }
    }
}
