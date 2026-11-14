#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV152 "level 7 lava: a robot goes in only on the player's order and melts if it stays" (the rule:
    /// Sim/ZWorld.Lava.cs), measured headless by the short form of the level 7 counted check (Editor/OzL07LavaCount.cs, every line but the
    /// whole computer match, which the full check runs).</summary>
    public static partial class OpenZParity
    {
        static void AddL07LavaDeviation(List<Deviation> list)
        {
            try { list.Add(DevL07Lava()); }
            catch (Exception ex) { list.Add(new Deviation { id = "DV152", change = "DevL07Lava", zod = "-", openz = "-", measured = "probe threw " + ex.GetType().Name + ": " + ex.Message, toggle = "-", status = DevCheck }); }
        }

        static Deviation DevL07Lava()
        {
            string measured; bool pass;
            if (UnityEngine.Resources.Load<UnityEngine.TextAsset>("OpenZ/Maps/" + OzL07LavaCount.Level + "_grid") == null) { measured = "level 7 map not installed"; pass = false; }
            else
            {
                string report;
                using (OzV11Kit.GameSwitches()) report = OzL07LavaCount.Run(SettingsText, MapText, false);
                measured = OzV11Kit.VerdictCell(report, out pass);
            }
            return new Deviation
            {
                id = "DV152", change = "Level 7 lava: a click on lava sends a robot in, it burns, glows redder and melts if it stays; left alone it walks out after 5 s",
                zod = "a lava tile is an Impassable tile (volcanic.tileinfo): nobody enters it and it does no harm (the original game: lava is impassable, not hazardous)",
                openz = "Sim/ZWorld.Lava.cs: the map keeps every lava tile Impassable (vehicles, the computer and the path finder never go in); a click on a lava tile means walk in: a human team's robot walks to the shore " +
                        "spot with the least way (dry walk + wade), then straight in (Zod's FORCE_MOVE, never through a rock, building or cliff tile), each robot to its own lava tile; in lava it walks at 0.7 and loses MaxHealth / 12 (rounded up) after " +
                        "each whole second, a fire hit: a full-health robot melts 12 s after it stepped in; an order takes it out at the shore with the least way to the goal, forward over the lava when that is shorter, never on a way it would melt on; " +
                        "with no order and no target it walks to the closest shore after 5 s; a new order ends a lava walk. View: the lava sheet, pops, glow lights, " +
                        "the robot's red glow from its heat, the lava melt death, their sounds, the robot's line at the order. Also on every map: a lava tile never blocks direct-fire sight (DV23)",
                measured = measured,
                toggle = "ZWorld.LavaDeviation: OPTIONS 'LAVA BURNS ROBOTS' (oz_lava_burns, default on), off in 'Zod exact' (View/ZGame.cs); off by default headless, in the parity rows and in the counted checks",
                status = pass ? DevActive : DevCheck,
            };
        }
    }
}
#endif
