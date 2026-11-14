#if UNITY_EDITOR
using System.Collections.Generic;

namespace OpenZ
{
    /// <summary>
    /// The heavy tank v2 films' and Play proof's FIXED places (1.2 items 126-137; editor only). Chosen OFF the editor on a scan of the
    /// WHOLE map, tile by tile, with the simulation's own tests (the kit's tools/headless/run_scan.py: ZPathFinder.StepOk,
    /// ZWorld.SnipeSafeSpot on a heavy probe and 2 tiles round, InExitLane, the robot / vehicle grids, every object's box; the maps
    /// gun or flag near). Generated: do not edit by hand. Absolute tiles (x east, y south), the top-left tile of a vehicle. Nothing is
    /// searched in Play: the films / proof ask every heavy slot again (SnipeSafeSpot, StepOk) and FAIL the take if one is protected.
    /// </summary>
    public static class HeavyV2Spots
    {
        static readonly Dictionary<string, (int x, int y)> T = new Dictionary<string, (int x, int y)>
        {
            { "L02/battle/blue goal", (20, 54) },
            { "L02/battle/blue grunts", (15, 43) },
            { "L02/battle/blue heavy", (18, 39) },
            { "L02/battle/blue light 1", (17, 42) },
            { "L02/battle/blue light 2", (23, 41) },
            { "L02/battle/blue medium", (20, 40) },
            { "L02/battle/blue toughs", (25, 43) },
            { "L02/battle/red goal", (20, 44) },
            { "L02/battle/red grunts", (25, 59) },
            { "L02/battle/red v2 A", (17, 57) },
            { "L02/battle/red v2 B", (20, 58) },
            { "L02/battle/red v2 C", (23, 57) },
            { "L02/battle/centre", (20, 49) },
            { "L02/board/drive", (25, 19) },
            { "L02/board/empty v2", (32, 12) },
            { "L02/board/grunt east", (26, 12) },
            { "L02/board/grunt west", (37, 12) },
            { "L02/board/centre", (32, 12) },
            { "L02/combat/enemy 0", (23, 23) },
            { "L02/combat/enemy 1", (30, 24) },
            { "L02/combat/enemy 2", (31, 13) },
            { "L02/combat/enemy 3", (24, 12) },
            { "L02/combat/enemy 4", (33, 18) },
            { "L02/combat/enemy 5", (21, 19) },
            { "L02/combat/leg a", (27, 22) },
            { "L02/combat/leg b", (27, 14) },
            { "L02/combat/v2", (27, 18) },
            { "L02/combat/centre", (27, 18) },
            { "L02/fight/blue goal", (28, 13) },
            { "L02/fight/blue grunts", (23, 22) },
            { "L02/fight/blue heavy", (27, 26) },
            { "L02/fight/blue light", (24, 23) },
            { "L02/fight/blue medium", (31, 24) },
            { "L02/fight/blue toughs", (33, 22) },
            { "L02/fight/red goal", (28, 19) },
            { "L02/fight/red v2", (28, 11) },
            { "L02/fight/centre", (28, 17) },
            { "L02/plant/bridge north", (52, 50) },
            { "L02/plant/jeep", (47, 53) },
            { "L02/plant/v1", (52, 37) },
            { "L02/plant/v1 end", (52, 28) },
            { "L02/plant/v1 south", (52, 65) },
            { "L02/plant/v2", (48, 37) },
            { "L02/plant/v2 end", (48, 28) },
            { "L02/plant/v2 south", (52, 68) },
            { "L02/plant/centre", (48, 37) },
            { "L02/proof/blue_v1", (27, 20) },
            { "L02/proof/blue_v2", (32, 20) },
            { "L02/proof/jeep", (36, 10) },
            { "L02/proof/neutral_v1", (38, 15) },
            { "L02/proof/neutral_v2", (43, 15) },
            { "L02/proof/red_v1", (27, 15) },
            { "L02/proof/red_v2", (32, 15) },
            { "L02/proof/centre", (33, 19) },
            { "L02/recoil/jeep", (50, 30) },
            { "L02/recoil/v1", (53, 37) },
            { "L02/recoil/v2", (47, 37) },
            { "L02/recoil/centre", (50, 37) },
            { "L02/snipe/away", (28, 33) },
            { "L02/snipe/blue grunts", (22, 22) },
            { "L02/snipe/grunts goal", (25, 20) },
            { "L02/snipe/red grunt (boards)", (28, 12) },
            { "L02/snipe/red v2", (28, 17) },
            { "L02/snipe/sniper 1", (30, 24) },
            { "L02/snipe/sniper 2", (25, 23) },
            { "L02/snipe/centre", (28, 17) },
            { "L02/tread/east", (55, 37) },
            { "L02/tread/north", (46, 28) },
            { "L02/tread/v2", (46, 37) },
            { "L02/tread/centre", (46, 37) },
            { "L02/turret/ring 0", (53, 29) },
            { "L02/turret/ring 1", (56, 31) },
            { "L02/turret/ring 2", (55, 36) },
            { "L02/turret/ring 3", (49, 37) },
            { "L02/turret/ring 4", (46, 28) },
            { "L02/turret/v2", (52, 31) },
            { "L02/turret/centre", (52, 31) },
            { "L02/turret_v1/ring 0", (13, 62) },
            { "L02/turret_v1/ring 1", (16, 64) },
            { "L02/turret_v1/ring 2", (15, 69) },
            { "L02/turret_v1/ring 3", (9, 70) },
            { "L02/turret_v1/ring 4", (6, 61) },
            { "L02/turret_v1/v2", (12, 64) },
            { "L02/turret_v1/centre", (12, 64) },
            { "L17/proof/blue_v1", (83, 62) },
            { "L17/proof/blue_v2", (88, 62) },
            { "L17/proof/jeep", (92, 52) },
            { "L17/proof/neutral_v1", (94, 57) },
            { "L17/proof/neutral_v2", (99, 57) },
            { "L17/proof/red_v1", (83, 57) },
            { "L17/proof/red_v2", (88, 57) },
            { "L17/proof/centre", (89, 61) },
        };

        /// <summary>The tile of `what` in the film / proof `job` on `level` ("L02/battle", "red v2 A"); false = not in the table.</summary>
        public static bool Get(string level, string job, string what, out int x, out int y)
        {
            x = y = 0;
            if (!T.TryGetValue(level + "/" + job + "/" + what, out var t)) return false;
            x = t.x; y = t.y; return true;
        }
        /// <summary>Every name in the table (the log lists them).</summary>
        public static IEnumerable<string> Keys => T.Keys;
    }
}
#endif
