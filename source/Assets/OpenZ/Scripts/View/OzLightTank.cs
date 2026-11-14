namespace OpenZ
{
    /// <summary>
    /// The light tank's two models (1.2 v2 units). The unit stays type "light" in the rules (stats, weapon, sounds, voices, the computer's
    /// play): only the drawn model and its pictures change.
    ///   v1 = the original model: Prefabs/Z_light, its materials and pictures, untouched.
    ///   v2 = the new model: Resources/OpenZ/LightV2/Z_light_v2 (made by the editor menu OpenZ/Light Tank v2/1 from
    ///        Models/Legacy/light_tank_v2.glb), its pictures in the same folder and ProductionBadge/x1..x4/face_light_v2_*.
    /// v2 carries every node name v1's code reads (TankSuspension, TankTurretYaw, TankBarrelPitch, TankRecoil, TankMuzzle, TankWheel_N,
    /// Track_Left / Track_Right with their belts, tank_HatchHinge, tank_Board_*, TankDriverSeat, TankGunnerSeat, the exits), so turret
    /// turn, barrel and head recoil, the hull rock, the hatch and commander, the ladder climb, the tread, damage effects, death,
    /// break-apart and wreck run on it as on v1 (View/OzV2Units.cs does the rest).
    /// ONE switch: <see cref="Version"/>. 2 = the new model everywhere (units, the light tanks placed in a level at the start, unit cards,
    /// build menu, badges, Unit editor), 1 = back to the original everywhere. The testing overlay (P) places either model whatever the
    /// switch says.
    /// </summary>
    public static class OzLightTank
    {
        /// <summary>THE switch: 2 = light tank v2 (the new model), 1 = v1 (the original model). One line to go back.</summary>
        public const int Version = 2;

        public const string Type = "light", V1Key = "Z_light", V2Key = "Z_light_v2", Dir = "OpenZ/LightV2/";
        /// <summary>A node only the v2 model has: the frame of its modelled real hatch (RoundHatch uses it; it marks a v2 view).</summary>
        public const string FrameNode = "tank_v2_HatchFrame";
        /// <summary>The testing overlay's two light tank tiles (their drop spawns type "light" drawn as that model).</summary>
        public const string TileV1 = "light@v1", TileV2 = "light@v2";
        /// <summary>v2's belt material: its own tread picture, scrolled on screen with the ground speed (View/VehicleMotion.Tread.cs).</summary>
        public const string BeltMaterial = "light_tank_v2_track";
        /// <summary>v2's drawn size over v1's (the prefab's Model node scale = v1's 0.9302 x this, set by the build menu: menu 1 again after a
        /// change). 1.21, kept for the v2 model of item 155 (the original light tank's shape and colouring,
        /// a better model and textures), whose model file is made for this scale: its silhouette from the RTS camera (pitch 55, 24 headings,
        /// every drawn renderer) is 12.92 m2 against v1's 12.55 m2 (103 %, every heading within 99 .. 106 %) and 81 % of the medium tank's
        /// 15.94 m2; drawn 4.15 x 3.49 x 2.89 m (v1 4.00 x 3.45 x 3.27). The spacing radius stays v1's 1.95 m; the rules' footprint never
        /// changes.</summary>
        public const float V2Size = 1.21f;
        /// <summary>The v2 model's road wheels (TankWheel_1..N, the parity check counts them): 6, three a side as on v1 and its reference.</summary>
        public const int V2Wheels = 6;

        /// <summary>The light tank's two models (View/OzV2Units.cs).</summary>
        public static readonly OzUnitModels Models = new OzUnitModels(Type, V1Key, V2Key, Dir, FrameNode, "tank_", "light_tank", "light", "LIGHT",
                                                                     "light_tank_v2_", "Tank_turret", () => Version);
    }
}
