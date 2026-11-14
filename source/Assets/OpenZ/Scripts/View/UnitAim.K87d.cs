namespace OpenZ
{
    /// <summary>
    /// 1.2 item 181 (the medium tank's turret "slightly slower ... so he is less fast compared to light"): the medium tank fired the light
    /// tank's gun with the light tank's turn too (SlewFor: 240 deg/s, smoothing 0.10 s). Its turret now turns at <see cref="MediumYawSpeed"/>
    /// with <see cref="MediumYawSmooth"/>: midway between the light tank (240 / 0.10) and the heavy tank (150 / 0.14), the same eased slew
    /// (Mathf.SmoothDamp) on a slower clock: every free turn takes about 1.2 x the light's time (a 90 deg turn reaches the gun's tolerance in
    /// 0.57 s instead of 0.47 s at 60 fps) and its top speed is about 17 % lower (181 against 220 deg/s drawn). The hurried slew while a shot
    /// waits (x2.5 / x0.45) scales the same way. The waiting shot's deadline (View/UnitAim.Steady.cs) still brings the gun onto a shell before
    /// its wait cap (no snap, no jump): a big turn onto a shot that is ready at once can need the same speed on both tanks there, as the
    /// shell's own flight time bounds the wait. The elevation, the tolerance, the kick and the slide are as before; the light tank (v1 and v2)
    /// is unchanged. View only: the rules never read a turret direction (the sim's targets, shots and their timing are untouched).
    /// </summary>
    public partial class UnitAim
    {
        /// <summary>The medium tank's turret turn: top speed (deg/s) and smoothing time (s) of its eased slew (the light tank: 240 / 0.10).</summary>
        public static float MediumYawSpeed = 195f, MediumYawSmooth = 0.12f;
    }
}
