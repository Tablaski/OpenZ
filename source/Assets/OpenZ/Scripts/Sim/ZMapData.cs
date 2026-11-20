using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// The decoded original Zod map (Resources/OpenZ/Maps/Lxx_grid.json, written from p02_bb_origNN.map): tile classes,
    /// palette tiles, zones and the object list with Zod's own tile coordinates (object loc = tile * 16, zserver.cpp:831-833).
    /// </summary>
    [Serializable]
    public sealed class ZMapData
    {
        [Serializable] public sealed class Zone { public int x, y, w, h; }
        [Serializable]
        public sealed class Obj
        {
            public int x, y; public string owner, kind; public int ot, oid, blevel, extra_links, health_percent;
            /// <summary>DV108 (Sim/ZHeroSquad.cs), entries of <see cref="ZMapData.heroes"/> only: the hero squad kind, its members' start
            /// tiles (x0, y0, x1, y1, ...), how the view starts it ("lounger") and the tile of the campfire beside them; empty in every Zod entry.</summary>
            public string squad, start; public int[] at, camp;
            public string Category => kind.Split(':')[0];
            public string Type => kind.Split(':')[1];
        }

        public string file, planet;
        public bool aiMap, skirmish;
        public int width, height;
        public int[] classes, tiles;
        public List<Zone> zones;
        public List<Obj> objects;
        /// <summary>DV108 (Sim/ZHeroSquad.cs): extra hero squads placed on top of <see cref="objects"/> while the switch is on (Zod's maps: none).</summary>
        public List<Obj> heroes;
        /// <summary>DV152 (Sim/ZWorld.Lava.cs), level 7 only: per tile, row by row, 0 = no lava, 1 = lava crust, 2 = hot lava pool (the
        /// volcanic tileset's lava tiles, written by the level 7 import tool). Every lava tile keeps class Impassable as in Zod; only the
        /// lava rule, the sight grid and the view read this. Null or empty on every other map.</summary>
        public int[] lava;

        /// <summary>DV152: this map has lava tiles.</summary>
        public bool HasLava => lava != null && width > 0 && lava.Length == width * height;
        /// <summary>DV152: lava under tile (tx, ty): 0 none (also off the map and on maps without lava), 1 crust, 2 hot pool.</summary>
        public int LavaKind(int tx, int ty) => !HasLava || tx < 0 || ty < 0 || tx >= width || ty >= height ? 0 : lava[ty * width + tx];
        public bool LavaAt(int tx, int ty) => LavaKind(tx, ty) > 0;

        public int WidthPix => width * ZConst.TILE;
        public int HeightPix => height * ZConst.TILE;

        public static ZMapData FromJson(string json) => UnityEngine.JsonUtility.FromJson<ZMapData>(json);

        public static Team ParseOwner(string owner) => owner switch
        {
            "red" => Team.Red, "blue" => Team.Blue, "green" => Team.Green, "yellow" => Team.Yellow,
            "purple" => Team.Purple, "teal" => Team.Teal, "white" => Team.White, "black" => Team.Black,
            _ => Team.Neutral,
        };

        public ZClass ClassAt(int tx, int ty) =>
            tx < 0 || ty < 0 || tx >= width || ty >= height ? ZClass.Impassable : (ZClass)classes[ty * width + tx];

        /// <summary>zmap.cpp:598-615 GetTileWalkSpeed at a pixel: impassable 0, road 1.689, water 0.7, else 1. DV152: a lava tile (an
        /// Impassable tile in Zod) walks at <see cref="ZWorld.LavaWalkSpeed"/> while the lava rule is on (only a robot the player sent in
        /// is ever there).</summary>
        public double TileWalkSpeed(int px, int py)
        {
            if (px < 0 || py < 0) return 0;
            int tx = px >> 4, ty = py >> 4;
            if (tx >= width || ty >= height) return 0;
            switch (ClassAt(tx, ty))
            {
                case ZClass.Impassable: return ZWorld.LavaDeviation && LavaAt(tx, ty) ? ZWorld.LavaWalkSpeed : 0;   // DV152 (Sim/ZWorld.Lava.cs)
                case ZClass.DeepWater: return 0;
                case ZClass.Road: return ZConst.ROAD_SPEED;
                case ZClass.Water: return ZConst.WATER_SPEED;
                default: return 1.0;
            }
        }
    }
}
