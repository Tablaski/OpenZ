using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// The MEDIUM TANK (1.2 owner item 139, the rules: Sim/ZMTank.cs, DV145): a new vehicle type "mtank" (the mech keeps Zod's "medium"),
    /// built on the light tank v2: the same node names (TankSuspension, TankTurretYaw, TankBarrelPitch, TankRecoil, TankMuzzle, TankWheel_N,
    /// Track_Left / Track_Right with their belts, tank_HatchHinge with the modelled hatch tank_v2_HatchFrame, tank_Board_*, TankDriverSeat,
    /// TankGunnerSeat, the exits), so turret easing, barrel slide, head kick and hull rock (HeavyRecoil), the planted running gear and the
    /// rolling tread (item 146), the hatch and its commander, boarding, damage effects, death, break-apart and wreck run on it as on the
    /// light tank v2. It has ONE model (there is no v1): Resources/OpenZ/MTank/Z_mtank (made by the editor menu OpenZ/Medium Tank/1 from
    /// Models/Legacy/mtank_v2.glb, game metres, Model scale 1), its pictures in the game's own folders (UI/Icons/icon_mtank_*,
    /// UI/PortraitsVehicles/portrait_mtank_*, ProductionBadge/x1..x4/face_mtank_*). View/OzV2Units.cs serves its prefab, hatch frame, roof
    /// panel rule and lamp data like the other v2 models (its switch is always 2: the one model).
    /// </summary>
    public static class OzMediumTank
    {
        public const string Type = ZMTank.Type;
        public const string Key = "Z_mtank", Dir = "OpenZ/MTank/";
        /// <summary>The frame node of the modelled real hatch on the dome (the light tank v2's name: RoundHatch's "tank_" path serves both).</summary>
        public const string FrameNode = "tank_v2_HatchFrame";
        /// <summary>Its belt material: its own dark tread picture, scrolled on screen with the ground speed in the built game (VehicleMotion.Tread.cs).</summary>
        public const string BeltMaterial = "mtank_v2_track";
        /// <summary>Its materials' name prefix (no team roof panel on them: the team colour is the red block at the turret's back).</summary>
        public const string MatPrefix = "mtank_v2_";
        /// <summary>The barrel's longest slide back into the dome (m): its hidden root stays inside the turret up to this (the model step's
        /// measure); UnitAim's medium tank slide peaks at 1.13 x 0.55 = 0.62 m.</summary>
        public const float SlideMaxM = 0.65f;
        /// <summary>k87b : the Model node's scale (menu OpenZ/Medium Tank/1 builds it). Its silhouette
        /// from the RTS camera (pitch 55, 24 headings, every drawn renderer) is then midway between the light tank v2 at OzLightTank.V2Size (12.48 m2)
        /// and the heavy tank v2 (19.39 m2): 15.94 m2 (at 1: 13.49, only 8 % over the light). The rules' footprint never changes; the barrel's
        /// slide and the dome scale with it (its root stays inside).</summary>
        public const float ModelScale = 1.087f;

        /// <summary>The weapon a shot is drawn and heard as: the medium tank fires the light tank's gun exactly (its shell, flash, smoke ring,
        /// sparks, launch and flight sounds, impact blast); every other type is itself.</summary>
        public static string Gun(string type) => type == Type ? ZMTank.Gun : OzBigTanks.Gun(type);   // 1.5 DV180 / DV181: the Mammoth fires the heavy tank's gun, the Bizon this one's (View/OzBigTanks.cs)

        /// <summary>The medium tank in View/OzV2Units.cs (prefab, hatch frame, roof panel, lamp data). Its "two" keys are the same prefab: one model.</summary>
        public static readonly OzUnitModels Models = new OzUnitModels(Type, Key, Key, Dir, FrameNode, "tank_", "mtank", "mtank", "MEDIUM TANK",
                                                                      MatPrefix, "Tank_turret", () => 2);
    }
}
