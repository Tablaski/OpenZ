namespace OpenZ
{
    /// <summary>
    /// 1.2 v2 units (the MLRS, lesson 6: "on every shot the turret / head kicks back as well as the barrel, and the hull rocks back with a short
    /// eased wobble (view only, never the sim position), v1 the same so the switch stays equal"): the MLRS's sizes in the heavy tank's eased
    /// recoil (View/HeavyRecoil.cs), both models.
    ///  * The launcher's kick is its own (View/MlrsSideKick.cs: per rocket the box tips up about its hinge and rolls to the firing side, with
    ///    a springy settle) plus UnitAim's shot kick (the box nods up): the MLRS has no head slide or head rock here (its turntable would
    ///    slide over the deck).
    ///  * The hull rocks back (the side it fires toward up, about the far track end) ONCE per salvo, on the sim's own rocket (the eight-rocket
    ///    ripple's rockets 2 to 8 only kick the launcher: MlrsSalvo.RippleFiring), and settles with the eased wobble of the heavy's spring:
    ///    the same at every game speed (a ripple at 4x would otherwise stack eight impulses into one 5 deg lurch). Before, every one of the
    ///    eight rockets jolted the hull 0.12 m and nodded it 4 deg TOWARD the shot on an envelope.
    ///  * No slide (a rocket salvo has little recoil; measured off the engine, kit tools/gear_sim_mlrs.py: a 2 cm slide across the hull, the
    ///    launcher firing to a side, took v2's hull walls through its planted side plates).
    /// The planted running gear (View/UnitView.GroundGear.cs) keeps the tracks, wheels and side plates on the ground: the rock only moves the
    /// hull and launcher.
    /// </summary>
    public partial class HeavyRecoil
    {
        /// <summary>The MLRS: the body's slide back (m, none) and rock back (deg) at the peak, once per salvo (RecoilFeel's MLRS gains are 1).</summary>
        public static float MlrsBodyPushM = 0f, MlrsBodyRockDeg = 1.2f;
    }
}
