using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// The MAMMOTH and the BIZON in the view (rules: Sim/ZBigTanks.cs, DV180 / DV181; Unit editor only). Each has ONE model: the prefab
    /// Resources/OpenZ/BigTanks/Z_mammoth / Z_bizon, made by the editor menu OpenZ/1.5/Big Tanks/1 from Models/BigTanks/{mammoth,bizon}_game.glb
    /// (game metres, Model scale 1: the Mammoth 1.2 x and the Bizon 1.3 x the heavy tank v2). Their node names (Editor/../NODE_CONTRACT in the kit):
    /// TankSuspension, XTurretYaw, XBarrelPitch, XRecoil[_L/_R], XMuzzle[_L/_R], x_HatchHinge with the modelled lid and x_v2_HatchFrame,
    /// x_Board_*, TankDriverSeat, Track_Left / Track_Right with REAL moving links (View/OzTrackLinks.cs, View/VehicleMotion.Links.cs), the
    /// Bizon's MK_Exhaust_L/_R (View/OzBigTankExhaust.cs) and the wreck set OZ_WreckPieces (View/OzBigTankWreck.cs). View/OzV2Units.cs serves
    /// the prefab, the hatch frame and the lamp data like the other one-model units (the switch is always 2). Pictures in the game's own folders:
    /// UI/Icons/icon_{type}_{red,blue}, UI/PortraitsVehicles/portrait_{type}_{red,blue}, UI/Neutral/neutral_{type}, ProductionBadge/x1..x4/face_{type}_*.
    /// </summary>
    public static class OzBigTanks
    {
        public const string Mammoth = ZBigTanks.Mammoth, Bizon = ZBigTanks.Bizon;
        public const string Dir = "OpenZ/BigTanks/";
        public const string MammothKey = "Z_mammoth", BizonKey = "Z_bizon";
        /// <summary>The frame nodes of their modelled hatches (RoundHatch.UseModelled through OzV2Units.FrameNodeFor).</summary>
        public const string MammothFrame = "mammoth_v2_HatchFrame", BizonFrame = "bizon_v2_HatchFrame";
        /// <summary>Their materials' name prefixes (no team roof panel: the team colour is in their paint mask).</summary>
        public const string MammothMat = "MammothV2_", BizonMat = "BizonV2_";

        public static bool Is(string type) => ZBigTanks.Is(type);

        /// <summary>The weapon a shot is drawn and heard as: the Mammoth fires the heavy tank's gun (its shells, flash, sounds, its one-two),
        /// the Bizon the medium tank's shot, which is the light tank's gun (OzMediumTank.Gun).</summary>
        public static string Gun(string type) => type == Mammoth ? ZBigTanks.Heavy : type == Bizon ? ZBigTanks.Light : type;

        /// <summary>CombatView: the drawn body's height a round aims into (m; the heavy tank 2.6 m x their size).</summary>
        public static float BodyHeight(string type) => type == Mammoth ? 3.1f : 3.4f;

        /// <summary>The longest barrel slide back (m): the heavy tank's 0.38 m x their size (UnitAim's slide = kick x recoilK).</summary>
        public const float MammothSlideMaxM = 0.46f, BizonSlideMaxM = 0.49f;

        public static readonly OzUnitModels MammothModels = new OzUnitModels(Mammoth, MammothKey, MammothKey, Dir, MammothFrame, "mammoth_", Mammoth, Mammoth, "MAMMOTH",
                                                                             MammothMat, "-", () => 2);
        public static readonly OzUnitModels BizonModels = new OzUnitModels(Bizon, BizonKey, BizonKey, Dir, BizonFrame, "bizon_", Bizon, Bizon, "BIZON",
                                                                           BizonMat, "-", () => 2);
    }
}
