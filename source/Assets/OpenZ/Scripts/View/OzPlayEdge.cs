using System;

namespace OpenZ
{
    /// <summary>
    /// Plain C#, no engine types: AttackRangeRings.PlayEdge.cs reads it; the counted checks run it on every level.
    /// Read from the level's own tile grid, once per map, no level is named:
    /// <list type="number">
    /// <item>open tiles: every class but Impassable, and lava (a lava tile keeps class Impassable, a robot can be sent in);</item>
    /// <item>the play area: the largest area of open tiles joined side by side (4 ways, so a wall that turns on a corner holds). The small
    /// pockets of ground and water behind the border walls are not in it: nothing can walk there;</item>
    /// <item>the outside: every tile that is not play area and is joined (8 ways) to the map's rim band, RimTiles deep. So the border cliffs,
    /// the pockets behind them and a rock that stands in the rim band, and anything off the map. This is the terrain builder's own line
    /// ("impassable components: touching the map edge = border cliffs, others = low banks"), taken from walkable ground so the pockets go
    /// too, and with the band so the quay rocks that stand 1 to 3 tiles off the rim go too;</item>
    /// <item>everything else stays: a hill, a rock or a bank inside the play area, not joined to the rim, is drawn on as before.</item>
    /// </list>
    /// A level's walkable ground decides, so a border that moves in the grid moves this line with it, on every side.
    /// </summary>
    public sealed class OzPlayEdge
    {
        /// <summary>A tile in metres (16 px, 8 px = 1 m).</summary>
        public const float TileM = 2f;
        /// <summary>ZClass.Impassable.</summary>
        public const int Impassable = 3;
        /// <summary>How deep the rim band is, in tiles from the map's side.</summary>
        public const int RimTiles = 4;
        /// <summary>Dist answers exactly up to here (m), and with this value beyond it.</summary>
        public const float Reach = TileM;

        const byte Clear = 0, NearOut = 1, Out = 2;
        readonly byte[] k;                                                                               // per tile: Clear, NearOut (a play or kept tile with an outside tile round it), Out

        public readonly int W, H;
        /// <summary>Tiles of the play area / outside / kept (not play area, not outside: hills, rocks and banks inside it).</summary>
        public readonly int PlayTiles, OuterTiles, KeptTiles;

        public OzPlayEdge(int width, int height, int[] classes, int[] lava)
        {
            W = width; H = height;
            int n = width * height;
            k = new byte[n];
            var open = new bool[n];
            bool hasLava = lava != null && lava.Length == n;
            for (int i = 0; i < n; i++) open[i] = classes[i] != Impassable || (hasLava && lava[i] > 0);
            // the largest area of open tiles, 4 ways
            var area = new int[n]; var stack = new int[n];
            int best = 0, bestN = 0, id = 0;
            for (int s = 0; s < n; s++)
            {
                if (!open[s] || area[s] != 0) continue;
                id++; area[s] = id; int sp = 0, count = 0; stack[sp++] = s;
                while (sp > 0)
                {
                    int i = stack[--sp]; count++;
                    int x = i % width, y = i / width;
                    if (x > 0 && open[i - 1] && area[i - 1] == 0) { area[i - 1] = id; stack[sp++] = i - 1; }
                    if (x < width - 1 && open[i + 1] && area[i + 1] == 0) { area[i + 1] = id; stack[sp++] = i + 1; }
                    if (y > 0 && open[i - width] && area[i - width] == 0) { area[i - width] = id; stack[sp++] = i - width; }
                    if (y < height - 1 && open[i + width] && area[i + width] == 0) { area[i + width] = id; stack[sp++] = i + width; }
                }
                if (count > bestN) { bestN = count; best = id; }
            }
            // the outside: from the rim band through everything that is not play area, 8 ways
            int top = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (area[i] == best && best != 0) continue;
                    if (x < RimTiles || y < RimTiles || x >= width - RimTiles || y >= height - RimTiles) { k[i] = Out; stack[top++] = i; }
                }
            while (top > 0)
            {
                int i = stack[--top]; int x = i % width, y = i / width;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int a = x + dx, b = y + dy;
                        if (a < 0 || b < 0 || a >= width || b >= height) continue;
                        int j = b * width + a;
                        if (k[j] == Out || (area[j] == best && best != 0)) continue;
                        k[j] = Out; stack[top++] = j;
                    }
            }
            // the tiles next to it (off the map counts): only these need the exact distance
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (k[i] == Out) { OuterTiles++; continue; }
                    if (area[i] == best && best != 0) PlayTiles++; else KeptTiles++;
                    bool near = false;
                    for (int dy = -1; dy <= 1 && !near; dy++)
                        for (int dx = -1; dx <= 1 && !near; dx++)
                            if (OutAt(x + dx, y + dy)) near = true;
                    if (near) k[i] = NearOut;
                }
        }

        /// <summary>Tile (tx, ty) is outside the play area (off the map too).</summary>
        public bool OutAt(int tx, int ty) => tx < 0 || ty < 0 || tx >= W || ty >= H || k[ty * W + tx] == Out;

        /// <summary>World (x, z) lies outside the play area (off the map too).</summary>
        public bool OutsideAt(float x, float z) => OutAt((int)Math.Floor(x / TileM), (int)Math.Floor(-z / TileM));

        /// <summary>Metres from world (x, z) to the nearest outside tile: 0 on one, Reach when none is nearer than that. One array read for
        /// nearly every point, the eight tiles round it next to the edge; nothing is allocated.</summary>
        public float Dist(float x, float z)
        {
            float mx = x, my = -z;
            int tx = (int)Math.Floor(mx / TileM), ty = (int)Math.Floor(my / TileM);
            if (OutAt(tx, ty)) return 0f;
            if (k[ty * W + tx] == Clear) return Reach;
            float px = mx - tx * TileM, py = my - ty * TileM, q = Reach * Reach;                          // inside its tile, 0 .. 2 m
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if ((dx == 0 && dy == 0) || !OutAt(tx + dx, ty + dy)) continue;
                    float ax = dx < 0 ? px : dx > 0 ? TileM - px : 0f, ay = dy < 0 ? py : dy > 0 ? TileM - py : 0f;
                    float d = ax * ax + ay * ay;
                    if (d < q) q = d;
                }
            return (float)Math.Sqrt(q);
        }
    }
}
