#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit sim_notes11: the parity rows DV192 (the Bizon is amphibious) and DV193 (the big tanks crush wrecks) and the menu of
    /// their counted check (OzV15BigTankGroundCount).</summary>
    public static partial class OpenZParity
    {
        static void AddBigTankGroundDeviations(List<Deviation> list)
        {
            string report = null, err = null;
            try { using (OzV11Kit.GameSwitches()) report = OzV15BigTankGroundCount.Quick(SettingsText, MapText); }
            catch (Exception ex) { err = "the probe threw " + ex.GetType().Name + ": " + ex.Message; }
            string Cell(string prefix, out bool pass)
            {
                pass = false;
                if (err != null) return err;
                var keep = new List<string>();
                foreach (var l in report.Split('\n')) { var s = l.Trim(); if ((s.StartsWith("PASS  ") || s.StartsWith("FAIL  ")) && BigTankGroundMine(s, prefix)) keep.Add(s); }
                return OzV11Kit.VerdictCell(string.Join("\n", keep), out pass) + " (the whole check: menu OpenZ/V1.5/Big Tank Ground: Count (headless, DV192 + DV193))";
            }
            string m192 = Cell("192", out bool p192), m193 = Cell("193", out bool p193);
            list.Add(new Deviation
            {
                id = "DV192", change = "the BIZON is amphibious: it drives through water (river, swamp, harbour) and lava",
                zod = "no tank enters a water tile (zpath_finding.cpp: a water tile is impassable in the vehicle grids); lava tiles are impassable for everyone; Zod has no Bizon",
                openz = $"the Bizon (DV181) has its own grids (water and lava tiles open unless an object blocks them, its own regions and ZReach components); on a water / lava tile it drives at x {ZWorld.BizonWadePace} of its pace, it fires as on land; lava slowly burns it (N12b: MaxHealth / {ZWorld.BizonLavaDieTicks} a tick, its own heat damage with no attacker: held in lava it is destroyed after {ZWorld.BizonLavaDieTicks / 100} s, off the lava the burn stops at once, water and land never; its path weighs a lava tile {ZPathFinder.AmphibLavaSide / 100} x a dry one, so it crosses lava only where the way round is long), it never starts a ram / crush while it or its target stands in water or lava; every other unit reads Zod's grids (the shared lanes, zones and bridges are built on them): no other unit's path changes",
                measured = m192,
                toggle = "ZWorld.BizonAmphibDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows and probes hold it off; needs DV181)",
                status = !p192 ? DevCheck : DevActive,
            });
            list.Add(new Deviation
            {
                id = "DV193", change = "the HEAVY TANK, the BIZON and the MAMMOTH crush vehicle wrecks instead of stopping at or going round them",
                zod = "Zod has no wreck object (DV39's harbour car wrecks are OpenZ's): a destroyable barrier is gone round or blasted (zobject.cpp:3716-3747)",
                openz = "for the heavy tank (v1 / v2), the Bizon and the Mammoth a vehicle wreck's tiles are open ground (their own grids; a wreck in water only for the Bizon); the tick their box touches it the wreck is crushed: destroyed by the game's death path with the tank as the attacker (no kill credit, no debris shell, no random draw), its tiles open; every other unit as before; never the Unit editor's holo obstacle, a rock, a hut or a prop",
                measured = m193,
                toggle = "ZWorld.BigTankWreckDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows and probes hold it off)",
                status = !p193 ? DevCheck : DevActive,
            });
        }

        /// <summary>Which verdict lines belong to which row: the wreck lines to DV193, the rest (crossings, wading, no crush in water, others
        /// unchanged) and the twice-run line to DV192.</summary>
        static bool BigTankGroundMine(string line, string id)
        {
            bool wreck = line.Contains("wreck") || line.Contains("holo obstacle");
            return id == "193" ? wreck : !wreck || line.Contains("the same scene twice");
        }

        /// <summary>Batch: only the DV192 / DV193 rows (-executeMethod OpenZ.EditorTools.OpenZParity.BigTankGroundRowBatch): the rows and the whole
        /// counted check into Logs/big_tank_ground_DV192_DV193.txt; exit 0 = both rows ACTIVE and every line of the whole check PASS, 1 = not.</summary>
        public static void BigTankGroundRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                using (ZWorld.BigTankGroundOff()) AddBigTankGroundDeviations(list);
                var settings = Resources.Load<TextAsset>("OpenZ/default_settings").text;
                string full; using (OzV11Kit.GameSwitches()) full = OzV15BigTankGroundCount.Run(settings, OzV11Kit.MapText, true);
                bool active = list.Count == 2 && list[0].status == DevActive && list[1].status == DevActive;
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                var sb = new System.Text.StringBuilder();
                foreach (var d in list) sb.Append($"{d.id} row: status {(d.status == DevActive ? "ACTIVE" : "CHECK")}\nzod: {d.zod}\nopenz: {d.openz}\nmeasured: {d.measured}\n\n");
                string text = sb + $"the whole check: {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/big_tank_ground_DV192_DV193.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = active && f == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV192 / DV193 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }

    public static class OzV15BigTankGroundMenu
    {
        [MenuItem("OpenZ/V1.5/Big Tank Ground: Count (headless, DV192 + DV193)")]
        static void Count()
        {
            var settings = Resources.Load<TextAsset>("OpenZ/default_settings");
            if (!settings) { Debug.LogError("[OpenZ V1.5] big tank ground count: no Resources/OpenZ/default_settings"); return; }
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] big tank ground count: leave Play mode first (the check sets the simulation's static switches)"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15BigTankGroundCount.Run(settings.text, OzV11Kit.MapText, true);
            int pass = 0, fail = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) pass++; else if (l.StartsWith("FAIL")) fail++; }
            Debug.Log($"[OpenZ V1.5] big tank ground count (DV192 + DV193): {pass} PASS, {fail} FAIL\n{r}");
        }
    }
}
#endif
