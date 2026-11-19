#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit l18_cpu_bigtank_r2: the parity row DV202 (the computer drives its big tank), measured by the counted check
    /// (OzV15BigTankDriveCount) on level 18's map, with level 2's bot line on and off. Only a computer's Mammoth / Bizon is touched: the other levels,
    /// the bot matches and the parity's other probes are as before (the parity report's Zod rows and every other row's probe hold it off with DV199).</summary>
    public static partial class OpenZParity
    {
        static void AddBigTankDriveDeviation(List<Deviation> list)
        {
            string map = null, map2 = null;
            try { map = Resources.Load<TextAsset>("OpenZ/Maps/L18_grid")?.text; map2 = Resources.Load<TextAsset>("OpenZ/Maps/L02_grid")?.text; } catch (Exception) { }
            string cell; bool pass;
            if (string.IsNullOrEmpty(map)) { cell = "no level 18 map (Resources/OpenZ/Maps/L18_grid)"; pass = false; }
            else
            {
                try { string r; using (OzV11Kit.GameSwitches()) r = OzV15BigTankDriveCount.Run(SettingsText, map, map2); cell = OzV11Kit.VerdictCell(r, out pass) + " (the whole check: menu OpenZ/Checks/V1.5/Computer big tanks (no Play))"; }
                catch (Exception ex) { cell = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            }
            list.Add(new Deviation
            {
                id = "DV202", change = "the computer drives its big tank: a computer's Mammoth or Bizon fights and marches on its own round",
                zod = "Z's computer orders a random 15-35 % of its idle units every 4-12 s, each to the target it is the nearest unit of: a slow tank far away stood for minutes; Z has no big tank",
                openz = "every second, for the computer's own Mammoth / Bizon only (not on its repair trip, not while it fights or waits in a bridge queue): no order, a target gone or 10 s without getting 2 tiles on on its order -> a new order: the nearest enemy robot / vehicle / gun within 14 tiles it can drive into range of (ATTACK: it shoots, rams, crushes by its own rules), else an enemy within 16 tiles of its own fort while it is within 45 tiles of home (it turns back to defend), else a not-own zone flag within 30 tiles by road, of two alike the one toward the enemy fort (MOVE with attack-to), else the enemy with the shortest road (ATTACK), else any other flag, else the enemy fort (ENTER FORT within 7 tiles, else an attack-move to it); on a march a far attack target gives way to an enemy within 14 tiles; road = its own driving way on its grid as it is now (the strike groups' way field), reachability as DV199; Zod's planner leaves it out",
                measured = cell, toggle = "ZWorld.BigTankDriveDeviation (View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off); held off with DV199 (ZWorld.BotBigTanksOff)",
                status = pass ? DevActive : DevCheck,
            });
        }
    }

    public static class OzV15BigTankDriveMenu
    {
        [MenuItem("OpenZ/Checks/V1.5/Computer big tanks (no Play)")]
        static void Check()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] Computer big tanks: leave Play mode first (the check sets the simulation's static switches)"); return; }
            var st = Resources.Load<TextAsset>("OpenZ/default_settings"); var map = Resources.Load<TextAsset>("OpenZ/Maps/L18_grid"); var map2 = Resources.Load<TextAsset>("OpenZ/Maps/L02_grid");
            if (!st || !map) { Debug.LogError("[OpenZ V1.5] Computer big tanks: no settings or no level 18 map in Resources"); return; }
            string r; using (ZWorld.BotBigTanksOff()) using (OzV11Kit.GameSwitches()) r = OzV15BigTankDriveCount.Run(st.text, map.text, map2 ? map2.text : null);
            Debug.Log("[OpenZ V1.5] Computer big tanks (DV202):\n" + r);
        }
    }
}
#endif
