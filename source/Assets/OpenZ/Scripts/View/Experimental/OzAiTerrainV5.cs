// OpenZ-VK (GPL-3.0).
using System;
using OpenZ.Sim;

namespace OpenZ.Experimental
{
    internal static class OzAiTerrainV5
    {
        static readonly bool Requested = Array.IndexOf(Environment.GetCommandLineArgs(), "-oz-ai-maps-v5") >= 0;

        public static bool HasExample(string id) => id is "SM02" or "SM08" or "SM11" or "SM16" or "SM17";
        public static bool Active(string id) => Requested && HasExample(id);

        // Fractured-ice artwork in the Arctic atlas. These identifiers carry no movement rule.
        public static bool ArcticIceArt(int tile) => tile is >= 246 and <= 248 or >= 264 and <= 268 or 284 or 285 or 288 or 289
            or >= 449 and <= 453 or 459 or >= 468 and <= 477;

        public static bool ArcticIceAt(ZMapData map, int index) =>
            ArcticIceArt(map.tiles[index]) && map.classes[index] == (int)ZClass.Ground;
    }
}
