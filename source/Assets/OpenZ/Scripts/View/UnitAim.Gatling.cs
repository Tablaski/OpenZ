using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Base | TurretYaw > BarrelSpin > Muzzle, with no BarrelPitch, so UnitAim's pitchJ stayed null, the gatling's limits in SlewFor were never read and GunError let it fire level at
    /// anything. OpenZ/V1.3/Gatling tilt/1 cuts the head out of the yaw mesh (Editor/OzGatlingTiltSetup.cs): TurretYaw (collar, neck, yoke, drum, seat kit) > BarrelPitch (shield, receiver,
    /// tunnel, on the yoke's hub bolt) > BarrelSpin > Muzzle, so the shot's start (UnitView.MuzzlePos = this Muzzle) rides on the tilt. Two small parts of the kit stand beside it:
    ///  * GatlingSkirt, the shield's lower apron: it tilts with the head and stops on the collar at <see cref="GatlingSkirtStop"/>; the head goes on down behind it;
    ///  * GatlingBreech, a dark sheet about the hub under the head's roof, and a plate in the drum's mouth behind the head: the head leaves a wedge over the drum's flange when it tilts down, these fill it.
    /// other gun widens (OzUePlatformViews.PitchMin / PitchMax). View only. Before the menu step ran there is no BarrelPitch and all of this does nothing.
    /// </summary>
    public partial class UnitAim
    {
        /// <summary>The head's stops (deg about its rest, + up). Up: the shield clears every fixed part to +45, +46.5 with the kick (0.017 m), the same top as any gun with a platform standing; the receiver goes into the drum, hidden. Down: past -30 the head's floor goes deep into the neck.</summary>
        public const float GatlingPitchMin = -30f, GatlingPitchMax = 45f;
        /// <summary>The skirt follows the head down to here: at -17 its back edge touches the collar.</summary>
        public const float GatlingSkirtStop = -16f;
        public const string GatlingSkirtNode = "GatlingSkirt", GatlingBreechNode = "GatlingBreech";

        Transform gSkirt; Quaternion gSkirtRest;

        bool IsGatling => obj != null && obj.Kind == ZKind.Cannon && obj.Type == "gatling";
        float GunPitchLow => IsGatling ? slew.pitchMin : OzUePlatformViews.PitchMin(slew.pitchMin);
        float GunPitchHigh => IsGatling ? slew.pitchMax : OzUePlatformViews.PitchMax(slew.pitchMax);

        /// <summary>Bind, after BindGun: the skirt of a gatling that has its head.</summary>
        void GatlingBind()
        {
            gSkirt = null;
            if (!IsGatling || pitchJ == null) return;
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == GatlingSkirtNode && t.parent == pitchJ.t.parent) { gSkirt = t; gSkirtRest = t.localRotation; break; }
        }

        /// <summary>Pose: pitch = the head's angle on its joint (kick in); the skirt turns on the head's axis, to its stop.</summary>
        void GatlingSkirtPose(float pitch) => gSkirt.localRotation = Quaternion.AngleAxis(Mathf.Max(pitch * pitchSign, GatlingSkirtStop) * pitchSign, pitchJ.axisL) * gSkirtRest;

        /// <summary>The Play check: the head and its skirt are bound.</summary>
        public bool GatlingRig => IsGatling && pitchJ != null && gSkirt != null;
    }
}
