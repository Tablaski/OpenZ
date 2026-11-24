// OpenZ-VK (GPL-3.0).
using System;
using OpenZ.Sim;

namespace OpenZ.Experimental
{
    internal static class OzAiTerrainV5
    {
        public static readonly bool Active = Array.IndexOf(Environment.GetCommandLineArgs(), "-oz-ai-maps-v4") < 0;

        // Fractured-ice artwork in the Arctic atlas. These identifiers carry no movement rule.
        public static bool ArcticIceArt(int tile) => tile is >= 246 and <= 248 or >= 264 and <= 268 or 284 or 285 or 288 or 289
            or >= 449 and <= 453 or 459 or >= 468 and <= 477;

        public static bool ArcticIceAt(ZMapData map, int index) =>
            ArcticIceArt(map.tiles[index]) && map.classes[index] == (int)ZClass.Ground;

        // Where the atlas draws the pouring lip, in native tile coordinates.
        public static (int X, int Y) CityOutfallDirection(int tile) => tile switch
        {
            >= 240 and <= 243 => (0, 1),
            >= 244 and <= 247 => (-1, 0),
            >= 264 and <= 267 => (1, 0),
            _ => (0, 0)
        };
    }
}
