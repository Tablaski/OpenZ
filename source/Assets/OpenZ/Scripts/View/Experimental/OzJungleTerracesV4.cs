// OpenZ-VK (GPL-3.0).
using OpenZ.Sim;

namespace OpenZ.Experimental
{
    // The directional palette roles are cross-checked against the official L16 terrain.
    // The asset depicts cliff faces around low ground, with the road on the high shelf.
    internal sealed class OzJungleTerracesV4
    {
        const float Lip = .9f;
        const float Shelf = 1.1f;
        const float Floor = -3.1f;
        readonly float[] elevation;
        readonly bool[] assigned;
        public bool HasTerraces { get; private set; }

        public OzJungleTerracesV4(ZMapData map)
        {
            elevation = new float[map.width * map.height];
            assigned = new bool[elevation.Length];
            if (map.planet != "jungle" || map.tiles?.Length != elevation.Length) return;
            Scan(map.height, map.width, 1, map.width, vertical: false);
            Scan(map.width, map.height, map.width, 1, vertical: true);
            ConnectRoadShelves(map.width, map.height, map.width, 1);
            ConnectRoadShelves(map.height, map.width, 1, map.width);

            void Scan(int lines, int width, int step, int stride, bool vertical)
            {
                for (int line = 0; line < lines; line++)
                {
                    int origin = line * stride;
                    int previousEnd = -1;
                    for (int col = 1; col < width - 2; col++)
                    {
                        int from = origin + col * step;
                        if (Face(map, from, vertical) != -1) continue;
                        int end = col + 1;
                        while (end < width - 1 && end - col <= 12 && IsBasinFloor(map.classes[origin + end * step]))
                            end++;
                        if (end - col < 3 || end - col > 12 || end >= width - 1 ||
                            Face(map, origin + end * step, vertical) != 1) continue;
                        if (previousEnd >= 0 && col - previousEnd <= 10)
                        {
                            bool road = false, dry = true;
                            for (int mid = previousEnd + 1; mid < col; mid++)
                            {
                                var type = (ZClass)map.classes[origin + mid * step];
                                road |= type == ZClass.Road;
                                dry &= type != ZClass.Water && type != ZClass.DeepWater;
                            }
                            if (dry && road)
                            {
                                for (int mid = previousEnd; mid <= col; mid++)
                                    Set(origin + mid * step, Shelf);
                            }
                        }
                        Set(from, Lip);
                        for (int mid = col + 1; mid < end; mid++) Set(origin + mid * step, Floor);
                        Set(origin + end * step, Lip);
                        HasTerraces = true;
                        previousEnd = end;
                        col = end;
                    }
                }
            }

            void ConnectRoadShelves(int lines, int length, int step, int stride)
            {
                for (int line = 0; line < lines; line++)
                {
                    int previous = -1;
                    for (int at = 0; at < length; at++)
                    {
                        int index = line * stride + at * step;
                        if (map.classes[index] != (int)ZClass.Road) { previous = -1; continue; }
                        if (elevation[index] != Shelf) continue;
                        if (previous >= 0 && at - previous <= 9)
                            for (int gap = previous + 1; gap < at; gap++) Set(line * stride + gap * step, Shelf);
                        previous = at;
                    }
                }
            }
        }

        public bool TryGetHeight(int index, out float height)
        {
            height = elevation[index];
            return assigned[index];
        }

        void Set(int index, float value)
        {
            if (!assigned[index] || value < 0 || (elevation[index] >= 0 && value > elevation[index]))
                elevation[index] = value;
            assigned[index] = true;
        }

        static bool IsBasinFloor(int type) =>
            type == (int)ZClass.Ground || type == (int)ZClass.Water || type == (int)ZClass.DeepWater;

        static int Face(ZMapData map, int index, bool vertical)
        {
            if (map.classes[index] != (int)ZClass.Impassable) return 0;
            int tile = map.tiles[index];
            if (vertical)
                return tile switch
                {
                    77 or 78 or 79 or 91 or 92 or 93 or 94 or 97 or 99 or 111 or 270 or 271 or 272 or 273 or 278 or 279 => -1,
                    74 or 206 or 208 or 220 or 221 or 226 or 227 or 228 or 322 or 402 => 1,
                    _ => 0
                };
            return tile switch
            {
                75 or 95 or 229 or 299 or 319 or 321 or 339 or 344 or 364 or 384 or 404 or 447 or 467 => -1,
                70 or 90 or 205 or 225 or 254 or 269 or 320 or 340 or 360 or 380 or 400 or 420 => 1,
                _ => 0
            };
        }
    }
}
