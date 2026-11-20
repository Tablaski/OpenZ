using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Level 18 "Bridge Game" (kit level18_rules), the view side of the rules of Sim/ZWorld.L18Rules.cs. View only: it reads the rules, it never
    /// writes them.
    /// <list type="bullet">
    /// <item>DV198: the level scene holds the map's own tanks (blue's heavy tank and red's medium tank on their map tiles, named "heavy_x_y" /
    /// "medium_x_y"). When the rules made a big tank there, ZGame.BindViews finds the scene's tank by <see cref="SceneAlias"/> and the big tank's own
    /// model takes its place (OzV2Units.SceneView: the same parent, place, turn and paint; the scene's tank is hidden and removed). EMPTY start
    /// (the default): each replaced tank's driver (a robot the rules made after the map, so no scene object) gets its view in <see cref="AtStart"/>.</item>
    /// <item>DV197: the two river stumps are static scene objects (menu OpenZ/L18 Bridge Game/10 River stumps, Environment/L18_RiverStumps,
    /// one child per stump named by its id, "R1_..." / "R2_..."). <see cref="AtStart"/> shows a stump only while the rules closed its tiles
    /// ('Zod exact' or a refused footprint: it is hidden, so nothing stands in open water).</item>
    /// </list>
    /// </summary>
    public static class OzL18Rules
    {
        public const string StumpsRoot = "L18_RiverStumps";
        /// <summary>Proof counters: stumps shown / hidden at the last start, big tanks the scene's tank was swapped for.</summary>
        public static int StumpsShown, StumpsHidden, TankSwaps;

        /// <summary>ZGame.BindViews: the scene name of the map tank a big tank of the rules replaced ("heavy_53_33"), else "" (no such name).</summary>
        public static string SceneAlias(ZObject o)
        {
            if (o == null || o.Source == null || o.Kind != ZKind.Vehicle || !OzBigTanks.Is(o.Type) || o.Source.Type == o.Type) return "";
            TankSwaps++;
            return $"{o.Source.Type}_{o.Source.x}_{o.Source.y}";
        }

        /// <summary>ZGame, right after the views are bound: the river stumps as the rules have them, one log line.</summary>
        public static void AtStart(ZGame g)
        {
            StumpsShown = StumpsHidden = 0;
            if (g == null || g.World == null || !OzCity.IsBridgeGame(g.level)) return;
            var w = g.World;
            var placements = GameObject.Find("ZodObjects");
            if (placements)
                foreach (var spot in ZWorld.L18BigTankSpots)
                {
                    if (w.DuelAllowsUnit(spot.to) || w.Objects.Exists(o => o.IsUnit && o.Source?.x == spot.tx && o.Source.y == spot.ty)) continue;
                    var placement = placements.transform.Find($"{spot.from}_{spot.tx}_{spot.ty}");
                    if (placement) placement.gameObject.SetActive(false);
                }
            var env = GameObject.Find("Environment");
            var root = env ? env.transform.Find(StumpsRoot) : null;
            if (root)
                for (int i = 0; i < root.childCount; i++)
                {
                    var c = root.GetChild(i);
                    int k = System.Array.FindIndex(ZWorld.L18StumpIds, id => c.name.StartsWith(id + "_"));
                    bool on = false;
                    if (k >= 0 && 4 * k + 3 < ZWorld.L18StumpTiles.Length)
                    {
                        int tx = ZWorld.L18StumpTiles[4 * k], ty = ZWorld.L18StumpTiles[4 * k + 1];
                        on = w.L18StumpAt(tx, ty);
                    }
                    on = true;
                    c.gameObject.SetActive(on);
                    if (on) StumpsShown++; else StumpsHidden++;
                }
            int drivers = 0;
            foreach (var d in w.L18BigTankDrivers) if (w.Get(d.Id) == d && !d.Destroyed && g.SpawnUnitView(d)) drivers++;   // DV198 EMPTY start: the replaced tanks' drivers beside the empty big tanks (made after the map: no scene object)
            string tanks = "";
            foreach (var t in w.L18BigTanks) tanks += $" {t.Type} of {t.Owner} at ({t.LocX / 16},{t.LocY / 16}){(t.Drivers.Count > 0 ? " crewed" : " empty")};";
            Debug.Log($"[OpenZ] level 18 rules: river stumps (DV197) {w.L18Stumps.Count} in the rules, {StumpsShown} shown, {StumpsHidden} hidden{(root ? "" : " (no Environment/" + StumpsRoot + " in the scene: run menu OpenZ/L18 Bridge Game/10 River stumps)")}; "
                      + $"big tanks (DV198){(tanks.Length > 0 ? tanks : " none ('Zod exact')")} scene swaps {TankSwaps}, drivers beside them {drivers}; "
                      + $"the computer and the big tanks (DV199) {(ZWorld.BotBigTanksDeviation ? "ON" : "off")}; the start shuttle (DV200) {(w.L18FortShuttle != null ? $"#{w.L18FortShuttle.Id} of {w.L18FortShuttle.Owner} at ({w.L18FortShuttle.CenterX / 16},{w.L18FortShuttle.CenterY / 16})" : "none (" + (w.L18ShuttleWhy.Length > 0 ? w.L18ShuttleWhy : "the box or a switch off") + ")")}");
            TankSwaps = 0;
        }
    }
}
