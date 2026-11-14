using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// DV163, level 7: a robot that fires at a dragon (Sim/ZWorld.Dragons.cs: no target of its own, a second gun channel) turns its upper body
    /// and gun to the drawn dragon, like to any aim point. The rule's AttackObject stays empty, so the view's aim asks here. Steep shots get
    /// their own limit: DragonPitchTop deg up, in the place of the usual 35, only while it aims at a dragon. Every other unit: as it was.
    /// </summary>
    public partial class UnitAim
    {
        public const float DragonPitchTop = 65f, NormalPitchTop = 35f;

        bool DragonTarget(out Vector3 p)
        {
            p = default;
            if (obj == null || !obj.DragonAiming) return false;
            var life = AmbientLife.I;
            return life && life.DragonAimPoint(out p);
        }

        float PitchTop => obj != null && obj.DragonAiming ? DragonPitchTop : NormalPitchTop;
    }
}
