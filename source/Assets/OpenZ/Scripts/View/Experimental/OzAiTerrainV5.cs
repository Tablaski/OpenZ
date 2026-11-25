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

        // The native deck tiles are road and solid. Reconnect the visible lava sheet only
        // where both banks of a bridge row lead into the same crossing.
        public static byte[] VolcanicBridgeLava(ZMapData map)
        {
            var coverage = new byte[map.width * map.height];
            foreach (var bridge in map.objects)
            {
                bool vertical = bridge.Type == "bridge_vert";
                if (!vertical && bridge.Type != "bridge_horz") continue;
                int length = 5 + (bridge.extra_links < 100 ? bridge.extra_links : 0);
                for (int along = 1; along < length - 1; along++)
                {
                    int left = Flank(-1, -2, along), right = Flank(4, 5, along);
                    if (left == 0 || right == 0) continue;
                    int first = Kind(-1, along) == 0 ? -1 : 0;
                    int last = Kind(4, along) == 0 ? 4 : 3;
                    for (int across = first; across <= last; across++)
                    {
                        var (x, y) = Tile(across, along);
                        if (x >= 0 && x < map.width && y >= 0 && y < map.height)
                            coverage[y * map.width + x] = (byte)Math.Min(left, right);
                    }
                }

                (int x, int y) Tile(int across, int along) =>
                    (bridge.x + (vertical ? across : along), bridge.y + (vertical ? along : across));

                int Kind(int across, int along)
                {
                    var (x, y) = Tile(across, along);
                    return map.LavaKind(x, y);
                }

                int Flank(int side, int outside, int along)
                {
                    var (x, y) = Tile(side, along);
                    int kind = map.LavaKind(x, y);
                    return kind == 0 && map.ClassAt(x, y) == ZClass.Impassable ? Kind(outside, along) : kind;
                }
            }
            return coverage;
        }

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
