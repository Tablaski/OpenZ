namespace OpenZ
{
    /// <summary>
    /// The mech's two models (1.2 v2 units). The unit stays type "medium" in the rules (Zod's medium tank, drawn and played as the walking
    /// MECH: stats, the two pod rockets per shot, sounds, voices, the computer's play, the minimap): only the drawn model and its pictures
    /// change.
    ///   v1 = the original model: Prefabs/Z_medium, its materials and pictures, untouched.
    ///   v2 = the SAME model with its own texture: Resources/OpenZ/MechV2/Z_medium_v2, a copy of Prefabs/Z_medium (every node, mesh, rig,
    ///        part, wreck piece and component of v1; the editor menu OpenZ/Mech v2/1 makes it again) with only v1's toy material swapped for
    ///        Materials/Units/mech_v2b_toy (v1's settings; its albedo Models/Legacy/Toy/mech_v2b_toy.png = v1's atlas a little more detailed:
    ///        crisper edges, rivets, worn corners, the claw arm dark grey like the gun, a clean visor glass; its team texels exactly v1's)
    ///        and the empty marker node <see cref="FrameNode"/>; its pictures in the same folder (icon_medium_mech_v2_*,
    ///        portrait_medium_mech_v2_* = v1's avatar) and ProductionBadge/x1..x4/face_medium_v2_*.
    /// So the walk (MechWalk), the waist turn, the cannon (UnitAim), the pod rockets, the hand gun (v1's fallback points), the hand gun's
    /// loss, the footsteps, boarding, the round top hatch (RoundHatch builds v1's on both; on v2 the lid, plate and collar take the v2's
    /// pictures: View/RoundHatch.MechV2.cs), the lamps (the "medium@v2" entries = v1's), damage, death, break-apart and wreck are v1's
    /// exactly. Team colour where v1 has it (the top plate texels, the shoulder housings); empty = v1's own empty look.
    /// ONE switch: <see cref="Version"/>. 2 = the new model everywhere (units, the mechs placed in a level at the start, unit cards, build
    /// menu, badges, Unit editor), 1 = back to the original everywhere. The testing overlay (P) places either model whatever the switch says.
    /// </summary>
    public static class OzMech
    {
        /// <summary>THE switch: 2 = mech v2 (the new model), 1 = v1 (the original model). One line to go back.</summary>
        public const int Version = 2;

        public const string Type = "medium", V1Key = "Z_medium", V2Key = "Z_medium_v2", Dir = "OpenZ/MechV2/";
        /// <summary>A node only the v2 model has: an empty marker under its root (it marks a v2 view; the hatch is v1's, built by RoundHatch).</summary>
        public const string FrameNode = "mech_v2b_Look";
        /// <summary>The testing overlay's two mech tiles (their drop spawns type "medium" drawn as that model).</summary>
        public const string TileV1 = "medium@v1", TileV2 = "medium@v2";

        /// <summary>The mech's two models (View/OzV2Units.cs). Pictures: icon_medium_mech_v2_&lt;team&gt; (the production card), portrait_medium_mech_v2_&lt;team&gt;
        /// (the crewed unit card), face_medium_v2_&lt;team&gt; (the badge); v1's own pictures (icon_medium_tank_*, portrait_medium_mech_*,
        /// face_medium_*) stay as they are. No modelled hatch on v2 (hatch prefix none): RoundHatch builds v1's round hatch on both models.</summary>
        public static readonly OzUnitModels Models = new OzUnitModels(Type, V1Key, V2Key, Dir, FrameNode, null, "medium_mech", "medium", "MECH",
                                                                     "mech_v2_", "Mech_body", () => Version);
    }
}
