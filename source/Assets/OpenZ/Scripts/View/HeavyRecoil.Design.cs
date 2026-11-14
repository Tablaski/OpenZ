using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// The recoil's design sizes of a vehicle type, as View/HeavyRecoil.Types.cs gives them to the springs (read by the recoil test so it
    /// judges every unit against its OWN numbers: the light tank's, not the heavy's). The head's slide is in the units of the head's parent
    /// node (the drawn size is that times the model's scale, e.g. the light tank's Model node 0.9302 x OzLightTank.V2Size); the body's slide
    /// (m) and rock (deg) are drawn as they are (RecoilFeel's gains included).
    /// </summary>
    public partial class HeavyRecoil
    {
        /// <summary>The head's slide back at the peak (parent units) for this type and model (1 / 2).</summary>
        public static float DesignHeadSlide(string type, int model) => HeadSlideFor(type, model);
        /// <summary>The head's rock back (deg, its front up) at the peak.</summary>
        public static float DesignHeadRock(string type) => HeadRockFor(type);
        /// <summary>The body's slide back (m) at the peak, RecoilFeel's gain included.</summary>
        public static float DesignBodySlide(string type) => BodyPushFor(type) * RecoilFeel.Hull(type, ZKind.Vehicle) * ShowSlideGain(type);   // k93: a shot along the hull (View/HeavyRecoil.Show.cs)
        /// <summary>The body's rock back (deg, the side it fires toward up) at the peak, RecoilFeel's gain included.</summary>
        public static float DesignBodyRock(string type) => BodyRockFor(type) * RecoilFeel.HullTilt(type, ZKind.Vehicle) * ShowRockGain(type);   // k93: a shot along the hull (View/HeavyRecoil.Show.cs)
    }
}
