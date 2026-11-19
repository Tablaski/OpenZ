#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit missile_build_time: the parity row DV191 and the menu of its counted check (OzV15MissileBuildTimeCount).</summary>
    public static partial class OpenZParity
    {
        static void AddMissileBuildTimeDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15MissileBuildTimeCount.Quick(SettingsText, MapText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.5/Missile Build Time: Count (headless, DV191))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV191", change = "the MISSILE CANNON takes 40 % longer to build than the HOWITZER",
                zod = $"the missile cannon's own row: unit.missile_cannon.build_time = {OzV15MissileBuildTimeCount.ZodMissile} s ({OzV15MissileBuildTimeCount.Mmss(OzV15MissileBuildTimeCount.ZodMissile)}); the howitzer {OzV15MissileBuildTimeCount.ZodHowitzer} s",
                openz = $"the missile cannon's base build time = the howitzer's x 1.4 to the nearest whole second ((h x 14 + 5) / 10: {OzV15MissileBuildTimeCount.ZodHowitzer} -> {ZMissileBuildTime.BuildTime(OzV15MissileBuildTimeCount.ZodHowitzer)} s = "
                        + $"{OzV15MissileBuildTimeCount.Mmss(ZMissileBuildTime.BuildTime(OzV15MissileBuildTimeCount.ZodHowitzer))}), set once in ZSettings.Parse, so the factory's countdown, the production window and the computer's estimates read it; "
                        + "every modifier on top as before (flags, damage, MAYHEM's x 0.9 for the computer, DV175); every other unit unchanged",
                measured = measured,
                toggle = "ZWorld.MissileTurretBuildTimeDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows and probes hold it off; the Unit editor's ZodEngine set reads Zod's)",
                status = !pass ? DevCheck : DevActive,
            });
        }

        /// <summary>Batch: only the DV191 row (-executeMethod OpenZ.EditorTools.OpenZParity.MissileBuildTimeRowBatch): the row and the whole
        /// counted check into Logs/missile_build_time_DV191.txt; exit 0 = the row ACTIVE and every line of the whole check PASS, 1 = not.</summary>
        public static void MissileBuildTimeRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                using (ZWorld.MissileTurretBuildTimeOff()) AddMissileBuildTimeDeviation(list);   // as the Deviations block runs it (the rule's switch held off; its probe sets it itself)
                var settings = Resources.Load<TextAsset>("OpenZ/default_settings").text;
                string full; using (OzV11Kit.GameSwitches()) full = OzV15MissileBuildTimeCount.Run(settings, OzV11Kit.MapText, true);
                var d = list[0];
                bool active = d.status == DevActive;
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                string text = $"DV191 row: status {(active ? "ACTIVE" : "CHECK")}\nzod: {d.zod}\nopenz: {d.openz}\nmeasured: {d.measured}\n\nthe whole check: {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/missile_build_time_DV191.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = active && f == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV191 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }

    public static class OzV15MissileBuildTimeMenu
    {
        [MenuItem("OpenZ/V1.5/Missile Build Time: Count (headless, DV191)")]
        static void Count()
        {
            var settings = Resources.Load<TextAsset>("OpenZ/default_settings");
            if (!settings) { Debug.LogError("[OpenZ V1.5] missile build time count: no Resources/OpenZ/default_settings"); return; }
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] missile build time count: leave Play mode first (the check sets the simulation's static switches)"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15MissileBuildTimeCount.Run(settings.text, OzV11Kit.MapText, true);
            int pass = 0, fail = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) pass++; else if (l.StartsWith("FAIL")) fail++; }
            Debug.Log($"[OpenZ V1.5] missile build time count (DV191): {pass} PASS, {fail} FAIL\n{r}");
        }
    }
}
#endif
