namespace OpenZ
{
    /// <summary>
    /// The two big tanks' turret turn (SlewFor's yaw speed cap and smoothing time).
    /// <list type="bullet">
    /// <item>MAMMOTH: the heavy tank's turn x <see cref="MammothTurnK"/> (0.8: 20 % slower): top speed <see cref="HeavyYawSpeed"/> x 0.8 = 120 deg/s,
    /// the eased slew's smoothing time <see cref="HeavyYawSmooth"/> / 0.8 = 0.175 s (the same curve on a slower clock: every turn takes 1.25 x
    /// the heavy's time). Was 130 / 0.15.</item>
    /// <item>BIZON: the medium tank's turn exactly (<see cref="MediumYawSpeed"/> / <see cref="MediumYawSmooth"/>: 195 / 0.12, View/UnitAim.K87d.cs;
    /// read live, so a later change of the medium tank's turn moves the Bizon's too). Was 140 / 0.14.</item>
    /// </list>
    /// The rules have no turret (Zod's neither: a shot leaves when the reload is up and the target in range); the shot's TIMING on screen
    /// follows the drawn gun as for every tank: a shot waits for the drawn barrel to come round (View/UnitAim.Steady.cs, its deadline), so the
    /// slower Mammoth turret shows its shot later after a big turn, the Bizon's as the medium tank's. The heavy tank's own numbers are
    /// unchanged (named here only so the Mammoth's are made from them).
    /// </summary>
    public partial class UnitAim
    {
        /// <summary>The heavy tank's turret turn (SlewFor "heavy": unchanged).</summary>
        public const float HeavyYawSpeed = 150f, HeavyYawSmooth = 0.14f;
        /// <summary>The Mammoth's turret turns at this share of the heavy tank's.</summary>
        public const float MammothTurnK = 0.8f;
    }
}
