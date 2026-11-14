using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 owner items 140 and 162b for the gun v2 (a reference is copied exactly; "remove canister and repaint the top like in v1"): the model
    /// (Models/Legacy/gun_v2.glb) has no chair and no red vented block at its back: the box's back is closed flush with its bolted panel,
    /// and the box's top between the two side frames, round the dark block on it, carries the team colour in its paint, the place and
    /// size of v1's team top plate. Its gunner sits BEHIND the box as v1's sits behind its housing: CannonGunnerSeat (his hips) stands
    /// 1.72 m behind the turn axis and 1.58 m over the ground (v1: 1.12 m and 1.05 m) on a small steel seat (a pan on a strut from the bottom
    /// of the box's back plate, part of the turret mesh; he is drawn at 0.95 of his standing size: BoardingView.SeatKey "gun_v2"), facing the
    /// barrel, turning with the turret; measured
    /// on the model with the seated gunner's figure, no part of the gun is within 2 cm of him at any turret heading, at rest and through
    /// the shot's kick (the turret back 0.08 m, its front up 2 deg), and his feet stay over the base ring. He is in view and can be
    /// sniped like v1's (the sim's CanBeSniped for a cannon). On a fort tower he sits the same (no perch is built on v2: OzGun.NoPerch).
    /// <see cref="NoChair"/>: v2 has no chair to break off at the death (the whole turret is tossed, as before).
    /// </summary>
    public static class GunV2Seat
    {
        /// <summary>CannonGunnerSeat's place under CannonTurretYaw (the turn joint, 0.6 m up; +z = the back): his hips 1.58 m over the ground,
        /// 1.72 m behind the turn axis, behind the box's closed back, on the seat (0.16 m higher and 0.36 m further back than the place the
        /// smaller gunner had: the bigger gunner's hands clear the box's back plate, his feet the neck and the base ring).</summary>
        public static readonly Vector3 SeatYawLocal = new Vector3(0f, 0.98f, 1.72f);
        /// <summary>The turret reaches 1.94 m behind its turn axis (the gunner's seat pan; the box and its neck 0.91 m; the red block reached
        /// 1.52 m, the old chair rail 2.57 m).</summary>
        public const float BackReachMax = 2.0f;
        /// <summary>The parity's spacing radius (menu OpenZ/Gun v2/2): the gun v2's own may exceed v1's by this much (m), the seat and the
        /// bigger gunner behind the box reaching further back (UnitView's radius and OzGun.KeepClear read the turret with its seat). View only.</summary>
        public const float SpacingGiveM = 0.4f;

        /// <summary>The parity (menu OpenZ/Gun v2/2): the v2 prefab's turret has no chair rail behind the box (every vertex of
        /// CannonTurretYaw_Surface within <see cref="BackReachMax"/> behind the turn axis: the gunner's small seat is inside it).</summary>
        public static bool NoChairModel(GameObject v2, out string what)
        {
            what = "not found"; if (!v2) return false;
            Transform yaw = null; MeshFilter mf = null;
            foreach (var t in v2.GetComponentsInChildren<Transform>(true))
            {
                if (!yaw && t.name == "CannonTurretYaw") yaw = t;
                if (!mf && t.name == "CannonTurretYaw_Surface") mf = t.GetComponent<MeshFilter>();
            }
            if (!yaw || !mf || !mf.sharedMesh || !mf.sharedMesh.isReadable) return false;
            float back = float.NegativeInfinity;
            foreach (var p in mf.sharedMesh.vertices) back = Mathf.Max(back, yaw.InverseTransformPoint(mf.transform.TransformPoint(p)).z);
            what = $"no chair rail and no red back block: the turret reaches {back:0.00} m behind its turn axis (the gunner's small seat; within {BackReachMax:0.00}; the red block reached 1.52 m, the old chair 2.57 m), nothing breaks off it at the death (GunV2Seat.NoChair: the seat goes with the turret)";
            return back <= BackReachMax;
        }

        /// <summary>CannonPolish.GunChairDebris.BreakOff: a gun v2 turret has no chair to break off (the whole turret is tossed, as before).</summary>
        public static bool NoChair(Transform turret, string type)
        {
            if (!turret || type != OzGun.Type) return false;
            var v = turret.GetComponentInParent<UnitView>();
            return v && OzGun.Models.IsV2(v.transform);
        }
    }
}
