using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>1.2 items 132 / 132b: the heavy tank's eased head kick and body rock (View/HeavyRecoil.cs), the two hooks UnitView calls.</summary>
    public partial class UnitView
    {
        HeavyRecoil heavyRecoil;

        /// <summary>The heavy tank's recoil component (v1 and v2), made on its first placed frame; null for every other unit or with the switch off.</summary>
        HeavyRecoil HeavyRecoilOf()
        {
            if (!HeavyRecoil.Enabled || Obj == null || (Obj.Kind != ZKind.Vehicle && !OzGun.Is(Obj)) || !HeavyRecoil.Types.Contains(Obj.Type)) return null;   // 1.2 v2 units: the light tank too; the gun cannon (v1 and v2): its turret kicks, its base never moves (View/HeavyRecoil.Gun.cs)
            if (!heavyRecoil) heavyRecoil = HeavyRecoil.Attach(this, Obj, turret);
            return heavyRecoil;
        }

        /// <summary>ApplyRecoil (UnitView.cs), after the head and barrels went back to their rest: the heavy tank's springs move on, its head
        /// slide and its body slide and rock are drawn (the head's rock follows in HeavyRecoil.LateUpdate). True = done (the envelope shove,
        /// jolt and nod below the hook are the switch-off path).</summary>
        bool HeavyHullRecoil(float dt)
        {
            var h = HeavyRecoilOf(); if (!h) return false;
            h.StepAndPlace(transform, dt);
            return true;
        }

        /// <summary>HeavyBarrelKick (UnitView.HeavyKick.cs): the shot's impulse along the barrel that fired.</summary>
        void HeavyRecoilKick()
        {
            var h = HeavyRecoilOf(); if (!h || MlrsSalvo.RippleFiring) return;                         // 1.2 v2 units: the MLRS hull rocks once per salvo, on the sim's own rocket (rockets 2 to 8 of the ripple kick the launcher only)
            var fallback = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            h.Kick(gunAim ? gunAim.RecoilDirection(fallback) : fallback);
        }

        /// <summary>Proof (the films): this view's heavy tank recoil component, null if none.</summary>
        public HeavyRecoil HeavyRecoilView => heavyRecoil;
    }
}
