#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit pits_no_block_r3: the parity row DV205 "low pits never block sight or shots" (Sim/ZWorld.LowPits.cs, the hook in Sim/ZSight.cs)
    /// and the menu of its counted check (OzV15LowPitsCount).</summary>
    public static partial class OpenZParity
    {
        static void AddLowPitsDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15LowPitsCount.Quick(SettingsText, MapText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/Checks/V1.5/Low pits never block sight (no Play))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV205", change = "Low pits never block sight or shots: impassable tiles that are LOWER ground (pits, ravines, gorges, moats, holes) stay impassable but are no wall for direct fire or the units' eyes; cliffs, raised rock, mountains and city blocks still are",
                zod = "no line of sight at all: range and the destroyable barriers between two tiles (EngageBarrierBetween, zmap.cpp:2266-2295); OpenZ's DV23 made every Impassable tile but lava (DV152) a wall for direct fire",
                openz = "Sim/ZWorld.LowPits.cs: per level the low impassable tiles (data made off Unity from each level's own drawn ground, the same rule for every level and biome: a tile 4 or more tiles in from the map's edge lying wholly 0.5 m or more under the playable ground (a pond's or lake's bed), or every tile of a pit, ravine, gorge or moat that walls in ground at least 0.5 m lower, its crest at most 7 m over its bank and not part of the map's edge ring); " +
                        "Sim/ZSight.cs leaves them out of the sight walls: direct fire, auto-targeting, agro, the attack planner's shot spots and the view's aim see across them. Level 16: its two gorges, the zone 9 ravine by the west gun, the north-east lake bed and the swamp holes (645 tiles); levels 2, 17, 18: only the bank faces under the bridges (open already, bridge rule); level 7: none (lava is DV152). " +
                        "Movement, paths and the computer's ways unchanged (the tiles stay Impassable); a rock, hut or building standing on one still blocks while it stands",
                measured = measured,
                toggle = "ZWorld.LowPitsDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows and probes hold it off; off in every counted check unless the check sets it)",
                status = !pass ? DevCheck : DevActive,
            });
        }

        /// <summary>Batch: only the DV205 row (-executeMethod OpenZ.EditorTools.OpenZParity.LowPitsRowBatch) into Logs/low_pits_DV205.txt;
        /// exit 0 = ACTIVE and every line of the whole check PASS.</summary>
        public static void LowPitsRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                using (ZWorld.LowPitsOff()) AddLowPitsDeviation(list);
                string full; using (OzV11Kit.GameSwitches()) full = OzV15LowPitsCount.Run(SettingsText, MapText);
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                var d = list[0];
                string text = $"DV205 row: status {d.status}\nmeasured: {d.measured}\n\nthe whole check: {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/low_pits_DV205.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = d.status == DevActive && f == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV205 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }

    public static class OzV15LowPitsMenu
    {
        [MenuItem("OpenZ/Checks/V1.5/Low pits never block sight (no Play)")]
        static void Count()
        {
            string r; using (OzV11Kit.GameSwitches()) r = OzV15LowPitsCount.Run(OzV11Kit.SettingsText, OzV11Kit.MapText);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/low_pits_DV205_check.txt"), r);
            Debug.Log("[OpenZ V1.5] Low pits (DV205):\n" + r);
        }
    }
}
#endif
