#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit fort_gun_target_r3: the parity row DV204 "fort guns are answered on the way, range to the tower's body" (Sim/ZObject.FortGunAnswer.cs) and the menu
    /// of its counted check (OzV15FortGunTargetCount).</summary>
    public static partial class OpenZParity
    {
        static void AddFortGunAnswerDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15FortGunTargetCount.Quick(SettingsText, MapText("L07"));
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/Checks/V1.5/Fort guns answered (no Play))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV204", change = "Fort guns are answered on the way: the range to an enemy fort tower gun is measured to its tower's body (every attacker); an enemy tower gun in reach, or shooting at a unit (or its side, the unit in its reach), is its first target, also on a move",
                zod = "range is measured centre to centre (points_within_distance, zobject.cpp:4760-4765); a unit fires at the first enemy in its range (CheckPassiveEngage, zobject.cpp:1603-1853) and keeps that target while it stays in range; an attack-move picks at random among the enemies within range + 40 px (CheckAttackTo, quirk 11.11); a plain move never turns off its way",
                openz = "range at an enemy fort tower gun (every weapon of every attacker: vehicles, robots, guns) = to the tower's body, a disc of ZWorld.FortTowerBodyPx (24 px, 3 m) round the gun's centre, as DV26 measures buildings to their walls; a unit with a tower gun's body in its reach fires at it before any target it picks itself (not over a named target); every half second: the nearest enemy fort tower gun (missile cannon, howitzer, gun, gatling on a tower spot) whose target is this unit, or a unit of its side while this unit is in the gun's reach, and that this unit can damage: an idle unit, a unit on a move (plain or attack-move, not a sprint), an automatic chase or an attack picked on the way gets an ATTACK on the gun in front of it (Zod's planner to a spot in range with a clear shot; the calm-move rule does not end it), then its order goes on; " +
                        "a unit holding position, stunned or on an order of its own kind fires at the gun first when it is in range. Both sides; ranges, damage and the guns' own choice unchanged (Sim/ZObject.FortGunAnswer.cs)",
                measured = measured,
                toggle = "ZWorld.FortGunAnswerDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows and probes hold it off)",
                status = !pass ? DevCheck : DevActive,
            });
        }

        /// <summary>Batch: only the DV204 row (-executeMethod OpenZ.EditorTools.OpenZParity.FortGunAnswerRowBatch) into Logs/fort_gun_DV204.txt;
        /// exit 0 = ACTIVE and every line of the whole check PASS.</summary>
        public static void FortGunAnswerRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                using (ZWorld.FortGunAnswerOff()) AddFortGunAnswerDeviation(list);
                string full; using (OzV11Kit.GameSwitches()) full = OzV15FortGunTargetCount.Run(Resources.Load<TextAsset>("OpenZ/default_settings").text, OzV11Kit.MapText);
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                var d = list[0];
                string text = $"DV204 row: status {d.status}\nmeasured: {d.measured}\n\nthe whole check: {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/fort_gun_DV204.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = d.status == DevActive && f == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV204 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }

    public static class OzV15FortGunTargetMenu
    {
        [MenuItem("OpenZ/Checks/V1.5/Fort guns answered (no Play)")]
        static void Count()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] fort guns answered: leave Play mode first (the check sets the simulation's static switches)"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15FortGunTargetCount.Run(OzV11Kit.SettingsText, OzV11Kit.MapText);
            int pass = 0, fail = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) pass++; else if (l.StartsWith("FAIL")) fail++; }
            Debug.Log($"[OpenZ V1.5] fort guns answered (DV204): {pass} PASS, {fail} FAIL\n{r}");
        }
    }
}
#endif
