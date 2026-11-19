using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 v2 units (lesson 6: "on every shot the turret / head kicks back as well as the barrel, and the hull rocks back with a short eased
    /// wobble, v1 the same so the switch stays equal"): the heavy tank's eased head kick and body rock (View/HeavyRecoil.cs, the same springs,
    /// the same code) for the light tank too, both models. Before, the light tank's head never moved on a shot (only its barrel slid), and
    /// its hull jolted 14.9 cm and nodded 4.5 deg TOWARD the shot (its front down) on an envelope.
    /// The light tank's sizes, smaller than the heavy's (a lighter gun: UnitAim's kick 5.2 against 7 deg), before RecoilFeel's light gains
    /// (x1.65 / x1.6): the body slides back 0.080 m and rocks back 1.40 deg at the peak (the heavy: 0.12 m, 2.0 deg), then settles with the
    /// same eased wobble (one smaller opposite rock, then a faint third). The head slides back 0.10 m (v1: its gun mantlet is in its barrel
    /// mesh, which slides on its own) or 0.20 m (v2: the mantlet is its own part on the barrel's pitch joint, so the turret itself must
    /// kick to read the same from the game camera) and rocks back 2.0 deg (front up) about a point behind its turn axis.
    /// With the planted running gear (View/UnitView.GroundGear.cs) the body's rock and slide only move the hull and turret: the tracks and
    /// road wheels stay on the ground.
    /// </summary>
    public partial class HeavyRecoil
    {
        /// <summary>The vehicle types that kick and rock this way (both models of each).</summary>
        public static readonly HashSet<string> Types = new HashSet<string> { OzHeavyTank.Type, OzLightTank.Type, OzMediumTank.Type, OzMlrs.Type, OzMech.Type, OzGun.Type, OzBigTanks.Mammoth, OzBigTanks.Bizon };   /* 1.5 DV180 / DV181 */   // DV145: the medium tank kicks and rocks too; 1.2 v2 units: the MLRS hull rocks too (View/HeavyRecoil.Mlrs.cs); the mech's upper body kicks and its body rocks (View/HeavyRecoil.Mech.cs); the gun's turret kicks, its base never moves (View/HeavyRecoil.Gun.cs)
        /// <summary>The light tank: the body's slide back (m) and rock back (deg) at the peak before RecoilFeel's gains (x1.65 / x1.6:
        /// 0.080 m, 1.40 deg); the head's slide back per model (m) and its rock back (deg, its front up).</summary>
        public static float LightBodyPushM = 0.0485f, LightBodyRockDeg = 0.875f, LightHeadSlideV1 = 0.10f, LightHeadSlideV2 = 0.20f, LightHeadRockDeg = 2.0f;

        /// <summary>The model the view is drawn as (2 = v2): the heavy by its v2 hatch frame (OzHeavyTank), the others by theirs (OzV2Units).</summary>
        static int ModelOf(ZObject o, Transform root)
        {
            if (o == null || !root) return 1;
            if (o.Type == OzHeavyTank.Type) return OzHeavyTank.IsV2(root) ? 2 : 1;
            var u = OzV2Units.Of(o.Type);
            return u != null && u.IsV2(root) ? 2 : 1;
        }
        static float HeadSlideFor(string t, int model) => t == OzGun.Type ? (model == 2 ? GunHeadSlideV2 : GunHeadSlideV1) : t == OzMech.Type ? (model == 2 ? MechHeadSlideV2 : MechHeadSlideV1) : t == OzMlrs.Type ? 0f : t == OzMediumTank.Type ? MediumHeadSlide : LightGun(t) ? (model == 2 ? LightHeadSlideV2 : LightHeadSlideV1) : (model == 2 ? HeadSlideV2 : HeadSlideV1);   // DV145: the medium tank fires the light tank's gun: its sizes; 1.2 v2 units: the MLRS launcher kicks on its own (MlrsSideKick); the mech's upper body on its waist (View/HeavyRecoil.Mech.cs); the gun's turret (View/HeavyRecoil.Gun.cs)
        static float HeadRockFor(string t) => t == OzGun.Type ? GunHeadRockDeg : t == OzMech.Type ? MechHeadRockDeg : t == OzMlrs.Type ? 0f : LightGun(t) ? LightHeadRockDeg : HeadRockDeg;
        static float BodyPushFor(string t) => t == OzGun.Type ? GunBodyPushM : t == OzMech.Type ? MechBodyPushM : t == OzMlrs.Type ? MlrsBodyPushM : LightGun(t) ? LightBodyPushM : BodyPushM;
        static float BodyRockFor(string t) => t == OzGun.Type ? GunBodyRockDeg : t == OzMech.Type ? MechBodyRockDeg : t == OzMlrs.Type ? MlrsBodyRockDeg : LightGun(t) ? LightBodyRockDeg : BodyRockDeg;
        /// <summary>The hull mesh (its size sets the rock's pivot) and the head mesh (its rear sets the head's rock pivot) of each type.</summary>
        static bool IsHull(string n) => n == "HeavyTankHull" || n == "Tank_hull" || n == "MLRS_hull" || n == "Mech_pelvis" || n == "Mammoth_hull" || n == "Bizon_hull";   // 1.5 DV180 / DV181   // 1.2 v2 units: the MLRS hull (v1 and v2) sets its rock's pivot; the mech's pelvis box (v1 and v2): its body rocks about the far end of it at the ground
        static bool IsHead(string n) => n == "HeavyTankTurret" || n == "Tank_turret" || n == "Mech_body" || n == "Mammoth_turret" || n == "Bizon_turret";   // 1.5 DV180 / DV181   // 1.2 v2 units: the mech's upper body (v1 and v2) rocks about a point half-way to its rear
        /// <summary>DV145: the light tank's gun (the light tank and the medium tank: their own sizes, RecoilFeel's gains per type on top).</summary>
        static bool LightGun(string t) => t == OzLightTank.Type || t == OzMediumTank.Type;
    }
}
