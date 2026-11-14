namespace OpenZ
{
    /// <summary>
    /// 1.2 v2 units (the mech, lesson 6: "on every shot the turret / head kicks back as well as the barrel, and the hull rocks back with a short
    /// eased wobble (view only, never the sim position), v1 the same so the switch stays equal"): the mech's sizes in the heavy tank's eased
    /// recoil (View/HeavyRecoil.cs, the same springs, the same code), both models. Before, the mech's whole body jolted 0.12 m and nodded
    /// 3.75 deg TOWARD the shot on an envelope (UnitView.ApplyRecoil), the upper body never moved on its own.
    ///  * HEAD = the upper body on its waist (MechWaistYaw, the node UnitView takes as the turret): on every shot it slides back along its
    ///    facing (0.06 model units = 5.2 cm drawn) and rocks its front up 1.5 deg about a point behind the waist axis (half-way to the rear
    ///    of Mech_body: the red backpack stays put), then eases home with one small overshoot; the cannon's own slide back, muzzle climb and
    ///    spin (UnitAim) and the pod kicks (MechRockets) come on top as before. Measured on the meshes at every waist yaw, the cannon level and
    ///    at its limits (kit tools/kick_clip.py): on v2 the kick adds at most 3 mm at the waist seam, nothing reaches the legs.
    ///  * BODY: the whole mech rocks back 1.2 deg (the side it fires toward up, about the far end of its pelvis box at the ground) and slides
    ///    back 2 cm, then settles with the eased wobble (one smaller opposite rock, then a faint third). The slide stays small: MechWalk reads
    ///    the root's motion as walking speed, and a larger shove would make the standing feet take a step after each shot. The FEET stay planted: MechWalk (order
    ///    79, after the rock) keeps each standing foot locked on its ground spot and its sole flat on the ground, the legs' IK takes the rock
    ///    (the mech's "tracks never wobble": owner item 146); measured off the engine with the walk's own maths (kit tools/feet_sim_mech.py).
    /// RecoilFeel's mech gains (x1.55 slide / x1.5 rock) apply on top of the body sizes below, as for every vehicle.
    /// </summary>
    public partial class HeavyRecoil
    {
        /// <summary>The mech: the body's slide back (m) and rock back (deg) at the peak before RecoilFeel's gains (x1.55 / x1.5: 0.020 m,
        /// 1.2 deg); the upper body's slide back per model (in its parent's units, MechPelvis: x the Model node's 0.87356 drawn) and its rock
        /// back (deg, its front up).</summary>
        public static float MechBodyPushM = 0.013f, MechBodyRockDeg = 0.8f, MechHeadSlideV1 = 0.06f, MechHeadSlideV2 = 0.06f, MechHeadRockDeg = 1.5f;
    }
}
