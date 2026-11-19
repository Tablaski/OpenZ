// OpenZ (GPL-3.0) - version 1.5, level 18 "Bridge Game" (kit level18_map): everything the two city levels share, in one place, so no look,
// sound or colour switch of the Car Park falls through to the desert on level 18. Plain values; view only: no rule reads this file. The Car
// Park's own rules (parked start, its shuttles, crews, wrecks, prop set, tips, briefing shots) stay on "L17" and are not asked here.
using UnityEngine;

namespace OpenZ
{
    /// <summary>The city biome: level 17 "Car Park" and level 18 "Bridge Game". <see cref="Is"/> answers every look / sound / colour switch
    /// that used to ask for "L17"; the level 18 names live here as <see cref="OzVolcanic"/> holds level 7's.</summary>
    public static class OzCity
    {
        public const string CarPark = "L17", BridgeGame = "L18", Planet = "city";
        public const string BridgeGameScene = "L18_BridgeGame", BridgeGameNumber = "18", BridgeGameName = "BRIDGE GAME";
        public const string BridgeGameLabel = "L18 · BRIDGE GAME", BridgeGameEndName = "THE BRIDGES";
        /// <summary>The blue side's own briefing scene of level 18 (Resources/OpenZ/Blue/blue_scenes.json; kit level18_briefings brings it).</summary>
        public const string BlueStartScene = "L18_start";

        /// <summary>A city level: the Car Park's look, sounds and colours apply.</summary>
        public static bool Is(string level) => level == CarPark || level == BridgeGame;
        /// <summary>The level running now is a city level.</summary>
        public static bool Here => ZGame.I && Is(ZGame.I.level);
        public static bool IsBridgeGame(string level) => level == BridgeGame;
        /// <summary>The level running now is level 18.</summary>
        public static bool BridgeGameHere => ZGame.I && ZGame.I.level == BridgeGame;

        /// <summary>A scene of a city level, by its name (L17_CarPark, L18_BridgeGame).</summary>
        public static bool IsScene(string sceneName) => !string.IsNullOrEmpty(sceneName) && (sceneName.StartsWith(CarPark) || sceneName.StartsWith(BridgeGame));
        /// <summary>The city level a scene name belongs to, null for any other scene.</summary>
        public static string LevelOfScene(string sceneName) =>
            string.IsNullOrEmpty(sceneName) ? null : sceneName.StartsWith(BridgeGame) ? BridgeGame : sceneName.StartsWith(CarPark) ? CarPark : null;

        /// <summary>The dust of a blast, a shell, a landing on city concrete (the grey the Car Park uses).</summary>
        public static readonly Color DustConcrete = new Color(0.62f, 0.57f, 0.52f);

        /// <summary>The blue side's briefing scene of a level: level 18's own once its data and voice are in, else what level 7's rule answers
        /// (its own lava walk on level 7, the shared scene everywhere else).</summary>
        public static string BlueBriefingScene(string level) =>
            IsBridgeGame(level) && OzBlueScenes.Shared.Ready(BlueStartScene, out _) ? BlueStartScene : OzVolcanic.BlueBriefingScene(level);
    }

    /// <summary>Which bridge model a bridge of the rules wears. The desert, jungle and city kits are one model each, scaled to the footprint; a
    /// city bridge of <see cref="LongFromTiles"/> tiles or more (level 18's three centre bridges, 4 x 12 tiles = 8 x 24 m) wears the long city
    /// family (prefabs Z_bridge_city_long_intact / _damaged / _destroyed / _chunks, kit level18_assets) at scale 1, never the 12 m model
    /// stretched to twice its length. While the long family is not among the scene's prefabs the 12 m kit is the fallback (one log line).</summary>
    public static class OzBridgeKit
    {
        public const int LongFromTiles = 10;
        /// <summary>Model length along the deck (m): the long city family, the city kit, the jungle planks, the desert bridge.</summary>
        public const float LongModelLength = 24f, CityModelLength = 12f, JungleModelLength = 22f, DesertModelLength = 20.6f;
        /// <summary>The free span as a share of the deck's flat half (OzBridgeSag): the city kit's piers stand at 3.7 of 6 m, the desert's
        /// abutments at 1.83 of 4.45 m. The long family: two river piers, the same share as the city kit until its model says otherwise.</summary>
        public const float CityFreeSpan = 0.62f, LongFreeSpan = 0.62f, OtherFreeSpan = 0.41f;

        public static string Biome(string level) => OzCity.Is(level) ? "city" : level == "L16" ? "jungle" : "desert";
        /// <summary>The deck's length in tiles (Zod: 5 + the map's extra links; a vertical bridge runs north-south).</summary>
        public static int Tiles(OpenZ.Sim.ZObject o) => o == null ? 0 : (o.Type == "bridge_vert" ? o.HeightPix : o.WidthPix) / 16;
        public static bool Long(string level, int tiles) => OzCity.Is(level) && tiles >= LongFromTiles;
        /// <summary>The prefab key without "Z_": bridge_city_intact, bridge_city_long_destroyed, bridge_jungle_chunks ... (state: intact, damaged,
        /// destroyed, chunks).</summary>
        public static string Key(string level, int tiles, string state) => "bridge_" + Biome(level) + (Long(level, tiles) ? "_long" : "") + "_" + state;
        public static float ModelLength(string level, int tiles, bool longFamily) =>
            longFamily ? LongModelLength : OzCity.Is(level) ? CityModelLength : level == "L16" ? JungleModelLength : DesertModelLength;
        public static float FreeSpan(string level, float flatHalf) => !OzCity.Is(level) ? OtherFreeSpan : flatHalf > 9f ? LongFreeSpan : CityFreeSpan;

        static bool warned;
        /// <summary>The prefab of a bridge's state among the game's prefabs (ZGame.Prefab), null when the level's kit has none.</summary>
        public static GameObject Prefab(ZGame game, OpenZ.Sim.ZObject bridge, string state)
        {
            if (!game) return null;
            int tiles = Tiles(bridge);
            var pf = game.Prefab("Z_" + Key(game.level, tiles, state));
            if (pf || !Long(game.level, tiles)) return pf;
            if (!warned) { warned = true; Debug.LogWarning("[OpenZ] bridges: the long city bridge family (Z_bridge_city_long_*) is not among this scene's prefabs: the 12 m city kit is used on the long decks"); }
            return game.Prefab("Z_" + Key(game.level, 0, state));
        }
        /// <summary>The level's chunk set when no bridge is at hand (checks): the short kit's.</summary>
        public static GameObject Chunks(ZGame game) => game ? game.Prefab("Z_" + Key(game.level, 0, "chunks")) : null;
    }
}
