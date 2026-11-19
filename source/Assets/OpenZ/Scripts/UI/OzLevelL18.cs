// OpenZ (GPL-3.0) - version 1.5, level 18 "Bridge Game" (kit level18_map): the level's card. The row joins OzLevelDef.All through the kit's
// register_l18.py once the scene is built and in the build settings (as level 7 did): until then the level is in the project but not in the menu.
namespace OpenZ
{
    /// <summary>The fifth level's card (after the Car Park, by number). The facts and the card's small map are read from the level's own map
    /// (Resources/OpenZ/Maps/L18_grid); the chips below are the fallback, counted from the Zod map (128 x 120 tiles, 14 zones, 12 flags).
    /// Poster, preview clips, wordmark and loading frame are made from real game frames after the scene exists (Unity pass).</summary>
    public static class OzLevelL18
    {
        public const string Id = OzCity.BridgeGame, Scene = OzCity.BridgeGameScene, Wordmark = "Wordmarks/mask_bridgegame";
        public static readonly OzLevelDef Def = new OzLevelDef(Id, Scene, OzCity.BridgeGameNumber, OzCity.BridgeGameName, "DAWN · RIVER SMOKE", Wordmark,
            "CAPTURE THE FLAGS TO TAKE TERRITORY. BREAK INTO THE BLUE FORT TO WIN.", "Capture the flags. Break into the blue fort.",
            new[] { "14 ZONES", "12 FLAGS", "2 FORTS", "128×120 TILES" });

        /// <summary>Level 18 is one of the menu's levels (registered).</summary>
        public static bool Registered => System.Array.FindIndex(OzLevelDef.All, d => d.Id == Id) >= 0;
        /// <summary>The level that level 18 follows in the menu (the menu lists the levels by number).</summary>
        public const string After = OzCity.CarPark;
    }
}
