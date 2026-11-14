namespace OpenZ
{
    /// <summary>
    /// 1.2 v2 units (the gun, owner item 140 + lesson 6: "on every shot the turret / head kicks back as well as the barrel ... v1 the same so the
    /// switch stays equal"; the computed rule for the gun: the gun's sizes
    /// in the heavy tank's eased recoil (View/HeavyRecoil.cs, the same springs, the same code), both models.
    ///  * HEAD = the turret (CannonTurretYaw, the node UnitView takes as the turret): on every shot it slides back along its barrel 0.08 m and
    ///    rocks its front up 2.0 deg about its own turn axis (the gun is not in HeavyRecoil.IsHead: no pivot behind it), then eases home with one
    ///    small overshoot; the barrel's own slide back (0.43 m) and its muzzle climb (3.4 deg) come on top as before (UnitAim). Measured on both
    ///    models' meshes at every turret heading, the peak, the overshoot and the way back (kit tools/kick_clip_gun.py): v1's housing stays
    ///    6.9 cm over its pedestal (9.5 at rest), v2's box 10.9 cm over its ring (14.2 at rest) and v2's neck rim at least 7 cm under the ring's
    ///    top (no gap opens under it); nothing reaches the base.
    ///  * BODY = none: the base is a building and never moves (no slide, no rock). Before, the WHOLE gun (base included) jolted back up to 0.12 m
    ///    and tilted up to 3.5 deg on every shot (UnitView.ApplyRecoil's cannon path: RecoilFeel's x1.3 / x1.25), and the turret never kicked on
    ///    its own.
    /// The gunner sits on CannonGunnerSeat under the turret: he rides with the kick (BoardingView places him after HeavyRecoil).
    /// </summary>
    public partial class HeavyRecoil
    {
        /// <summary>The gun: the turret's slide back per model (in its parent's units, the Model node: x 1 on the ground, x 0.72 on a fort
        /// tower) and its rock back (deg, its front up); the base's slide and rock (none: a building).</summary>
        public static float GunHeadSlideV1 = 0.08f, GunHeadSlideV2 = 0.08f, GunHeadRockDeg = 2.0f, GunBodyPushM = 0f, GunBodyRockDeg = 0f;
    }
}
