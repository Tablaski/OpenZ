#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV163 "level 7 dragons attack" (the rule: Sim/ZWorld.Dragons.cs), measured headless by the rule count (Editor/OzDragonCount.cs).</summary>
    public static partial class OpenZParity
    {
        static void AddL07DragonDeviation(List<Deviation> list)
        {
            try { list.Add(DevL07Dragons()); }
            catch (Exception ex) { list.Add(new Deviation { id = "DV163", change = "DevL07Dragons", zod = "-", openz = "-", measured = "probe threw " + ex.GetType().Name + ": " + ex.Message, toggle = "-", status = DevCheck }); }
        }

        static Deviation DevL07Dragons()
        {
            string measured; bool pass;
            if (UnityEngine.Resources.Load<UnityEngine.TextAsset>("OpenZ/Maps/L07_grid") == null) { measured = "level 7 map not installed"; pass = false; }
            else
            {
                string report;
                using (OzV11Kit.GameSwitches()) report = OzDragonCount.Run(SettingsText, MapText, false);
                measured = OzV11Kit.VerdictCell(report, out pass);
            }
            return new Deviation
            {
                id = "DV163", change = "Level 7: a dragon that happens to fly close to a robot may try to burn it, robots shoot back and can kill it",
                zod = "the volcanic birds are decor (abird.cpp): no target, no damage",
                openz = "Sim/ZWorld.Dragons.cs: from plain soaring, a robot or a flying shuttle on the dragon's own straight line, on screen, after the first minute, 1 in 2, 90 s a dragon, 120 s a map, one at a time; a flying shuttle, which they hate, 8 in 10 and 60 s from the last pass: a dip to 9.5 m or a level pass. " +
                        "1.8 s of the Pyro's flame, 9 bites inside 40 degrees from straight down, 6.5 % of MaxHealth on a robot (a healthy robot never dies of one pass), the same on every other robot within 2.5 m of the flame's end, 2 % on a flying shuttle, never under a quarter of its health (no dragon destroys one); never a vehicle or a landed shuttle. " +
                        "Robots with no target of their own shoot back on a second gun channel; its life counts 3.5 gun-seconds. No Zod draw, no waypoint or target of a robot touched, no kill credit, no alert. " +
                        "The camera is an input: one order at one tick with plain data, like a player's click",
                measured = measured,
                toggle = "ZWorld.DragonDeviation: on in the game, off in 'Zod exact' (View/ZGame.cs); off by default headless, in the parity rows and in the counted checks; no lava: every order refused",
                status = pass ? DevActive : DevCheck,
            };
        }
    }
}
#endif
