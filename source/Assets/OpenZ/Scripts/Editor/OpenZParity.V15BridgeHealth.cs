#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit bridge_health_r3: the parity row DV207 "bridges as weak as in Z" (Sim/ZWorld.BridgeHealth.cs, the hook in
    /// Sim/ZWorld.cs CreateObject) and the menu of its counted check (OzV15BridgeHealthCount).</summary>
    public static partial class OpenZParity
    {
        static void AddBridgeHealthDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15BridgeHealthCount.Quick(SettingsText, MapText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/Checks/V1.5/Bridge health as in Z (no Play))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV207", change = "Bridges as weak as in Z: every bridge (short or long, every level) has 46546 hit points instead of Zod's 83333, so the Mobile Missile launcher brings one down with Z's 6 missiles (Zod: 10-11) and every other weapon in the same share fewer shots (counted: Tough squad 54 -> 32 rockets, light tank 43-44 -> 25-26 shells, heavy tank 36 -> 20 shots, howitzer 22-23 -> 13 shells)",
                zod = "bridge_building_health 2000 / 240 x MAX_UNIT_HEALTH = 83333 hit points (zsettings.cpp:261, zobject.cpp:150-153), Z's 2000; Zod cut the MLRS missile from Z's 360 / 240 to 62 / 74 (zsettings.cpp), so a bridge took 10-11 missiles in Zod against 5.6 in Z",
                openz = "Sim/ZWorld.BridgeHealth.cs: BridgeHitPoints = 83333 x (62 / 74) / (360 / 240) = 46546 (Z's share of a bridge per MLRS missile with Zod's missile); ZWorld.CreateObject gives it to every bridge_vert / bridge_horz as its maximum. " +
                        "Damage, blasts, the damaged look below half, the start-destroyed bridges (L17, L18), the death blasts, the auto repair time and the crane's repair unchanged; a returned bridge comes back at its full (new) health",
                measured = measured,
                toggle = "ZWorld.BridgeHealthDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows and probes hold it off; off in every counted check unless the check sets it)",
                status = !pass ? DevCheck : DevActive,
            });
        }

        /// <summary>Batch: only the DV207 row (-executeMethod OpenZ.EditorTools.OpenZParity.BridgeHealthRowBatch) into Logs/bridge_health_DV207.txt;
        /// exit 0 = ACTIVE and every line of the whole check PASS.</summary>
        public static void BridgeHealthRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                using (ZWorld.BridgeHealthOff()) AddBridgeHealthDeviation(list);
                string full; using (OzV11Kit.GameSwitches()) full = OzV15BridgeHealthCount.Run(SettingsText, MapText);
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                var d = list[0];
                string text = $"DV207 row: status {d.status}\nmeasured: {d.measured}\n\nthe whole check: {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/bridge_health_DV207.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = d.status == DevActive && f == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV207 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }

    public static class OzV15BridgeHealthMenu
    {
        [MenuItem("OpenZ/Checks/V1.5/Bridge health as in Z (no Play)")]
        static void Count()
        {
            string r; using (OzV11Kit.GameSwitches()) r = OzV15BridgeHealthCount.Run(OzV11Kit.SettingsText, OzV11Kit.MapText);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/bridge_health_DV207_check.txt"), r);
            Debug.Log("[OpenZ V1.5] Bridge health (DV207):\n" + r);
        }
    }
}
#endif
