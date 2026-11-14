using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// 1.2 v2 units, the light tank v2 (its second model: the original light tank's shape with a short, thick gun in a round collar): only
    /// 0.81 model units of barrel show in front of the collar (the first v2 model showed 1.6), so the light type's barrel slide (recoilK 0.66:
    /// a peak of 1.133 x 0.66 = 0.75) would pull 92 % of it into the collar and the muzzle would nearly vanish on every shot. A v2 view
    /// slides <see cref="LightRecoilKV2"/> instead: a peak of 0.385 model units (0.43 m drawn at the game's size), 48 % of the shown barrel
    /// (the first v2 model's share), 0.42 still in front of the collar; its root stays 0.80 inside the collar, and its rear end stays inside
    /// the turret at every elevation (at -10 deg 0.17 under the roof) and far in front of the turret's back at the peak. v1 keeps 0.66, and
    /// every other number of the light gun's slew (turn and pitch speeds, easing, tolerance, kick) is the same on both models.
    /// View only: the sim's shots and their timing are untouched.
    /// </summary>
    public partial class UnitAim
    {
        /// <summary>The light tank v2's barrel slide factor (the light type and v1: 0.66).</summary>
        public static float LightRecoilKV2 = 0.34f;

        /// <summary>Bind: the slew with the drawn light tank v2's own barrel slide; every other unit and model as it was.</summary>
        static Slew LightSlew(Slew s, ZObject o, UnitView v)
        {
            if (o == null || !v || o.Kind != ZKind.Vehicle || o.Type != OzLightTank.Type || !OzLightTank.Models.IsV2(v.transform)) return s;
            s.recoilK = LightRecoilKV2;
            return s;
        }
    }
}
