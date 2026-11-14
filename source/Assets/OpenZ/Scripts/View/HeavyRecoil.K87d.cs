namespace OpenZ
{
    /// <summary>
    /// 1.2 item 181 (the medium tank: a little more recoil on its head, the back and forward movement eased, so each shot has more impact):
    /// the medium tank's turret slid back 0.20 (the light tank v2's size, in the head's parent units: 0.217 m drawn at its Model scale
    /// 1.087) on every shot. It now slides back <see cref="MediumHeadSlide"/> (0.283 m drawn, 30 % more) with the same eased spring: the kick
    /// peaks about 0.06 s after the shot, then the turret eases forward home with one small overshoot (about 9 %) and rests by about 0.25 s,
    /// no jump (its first frame stays under the recoil test's step limit). Its rock (2 deg, its front up), the barrel's own slide in the dome,
    /// the hull's rock and slide (View/HeavyRecoil.Show.cs) and the planted tracks are as before; the light tank is unchanged.
    /// Measured on the model: at the peak (slide and rock together) the dome's lower rear edge stays within 2 mm of the deck at every turret
    /// heading, as with the old 0.20.
    /// </summary>
    public partial class HeavyRecoil
    {
        /// <summary>The medium tank's head slide back at the peak (the head's parent units; the light tank v2: 0.20).</summary>
        public static float MediumHeadSlide = 0.26f;
    }
}
