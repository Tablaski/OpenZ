using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 owner item 169 : a vehicle or gun the game puts down by itself has no
    /// facing of its own. Its view starts at its prefab's turn (ZGame.SpawnUnitView, UnitView.Bind: yaw = the root's turn - modelYaw), and
    /// every unit prefab has its front (barrel, muzzle) toward the bottom of the screen, straight at the RTS camera (north-up, pitch 55): seen
    /// from above only the turret's top shows and the barrel hides under it. Such placements now turn <see cref="SideYaw"/> (the front to the
    /// right of the screen, east): the barrel lies across the picture at its full length, on every map (the RTS camera is always north-up).
    /// Called for: every vehicle and gun the testing overlay drops (P: the EMPTY row and the team columns share the code,
    /// UI/OzCheatSpawner.cs) and the parked empty head-start vehicles (L17, View/ZGame.cs). Unchanged: the maps' own vehicles and guns (their
    /// placed facing, as in Z), factory products, repaired vehicles, placed guns, robots, the shuttle (its pad). View only: the rules never
    /// read a facing; the turret starts along the hull as before. Switch: <see cref="SideFacingOn"/> (off = the prefab's turn, as before).
    /// </summary>
    public partial class UnitView
    {
        /// <summary>The switch (off = a placement starts at its prefab's turn, the barrel toward the camera, as before).</summary>
        public static bool SideFacingOn = true;
        /// <summary>The facing such a placement takes (deg, UnitView space: front = (sin, cos) on the ground; 90 = east, the screen's right).</summary>
        public const float SideYaw = 90f;
        /// <summary>The check: the units turned so far (their ids).</summary>
        public static readonly HashSet<int> SideFacedIds = new HashSet<int>();

        /// <summary>The hull's drawn facing (deg, front = (sin, cos)); the check reads it.</summary>
        public float DrawnYaw => yaw;

        /// <summary>Right after the view is made (before its first frame): a vehicle or gun faces <see cref="SideYaw"/>.</summary>
        public void FaceSide()
        {
            if (!SideFacingOn || Obj == null || Obj.Destroyed || death != Death.None) return;
            if (Obj.Kind != ZKind.Vehicle && Obj.Kind != ZKind.Cannon) return;
            yaw = heading = prevYaw = SideYaw;
            transform.rotation = Quaternion.Euler(0f, yaw + modelYaw, 0f);
            if (hullSteer != null) hullSteer.Adopt(yaw);                                                // made later from yaw when not there yet (SteerHull)
            if (slip != null) slip.Reset(transform.position.x, transform.position.z, yaw);             // made later from yaw when not there yet (NoCrab)
            turretInit = false;                                                                        // the turret starts along the new hull (Sync)
            SideFacedIds.Add(Obj.Id);
        }
    }
}
