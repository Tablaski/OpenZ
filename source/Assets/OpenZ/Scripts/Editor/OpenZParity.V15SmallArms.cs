#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit small_arms_balance_r8: the parity row DV212 "small arms hit robots on foot harder" (Sim/ZObject.SmallArms.cs, the hook in
    /// Sim/ZObject.RiflePair.cs BaseHitDamage), the Unit editor's stats with it (View/OzUeLiveStats.cs VsGrunt), the menu of its counted check
    /// (OzV15SmallArmsDuel), the row's batch, and the menus of the Play check (View/OzSmallArmsPlayCheck.cs).</summary>
    public static partial class OpenZParity
    {
        static void AddSmallArmsDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15SmallArmsDuel.Quick(SettingsText) + StatsPanelCheck(SettingsText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/Checks/V1.5/Small arms vs robots: the whole check (no Play))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV212", change = "Small arms hit ROBOTS ON FOOT harder: a body hit of a grunt on a grunt 155 HP (was 125: 7 hits fell a grunt, as in the original Z), of a grunt on any other robot 100 (was 11), of a psycho 40 (was 26), of the jeep's gun 45 (was 27), of the gatling 75 (was 45); hulls, drivers and buildings exactly as before; the owner: \"grunts vs grunts ... not so long; jeep must be doing more damage vs robots; psychos ... more damage vs other robots (no change to vehicles); gatling ... killing grunts and other robots faster, no change vs vehicles\"",
                zod = "a bullet that hits removes the shooter's attack_damage whatever it hits (zobject.cpp:2187-2199): grunt 11, psycho 26, jeep gun 27, gatling 39 (OpenZ DV170: 45); OpenZ DV78 already gave a grunt 125 on a grunt and the 1.2 sniper balance its pairs",
                openz = "Sim/ZObject.SmallArms.cs SmallArmsHit, called on the result of BaseHitDamage (Sim/ZObject.RiflePair.cs): when the shooter is a grunt or psycho on foot, the jeep or the gatling gun and the target is a robot on foot (not a fort roof defender, not Evil Zod), the hit removes the shooter's DV212 value (ZWorld.VsRobotHit / SmallArmsPair) when that is higher than today's. " +
                        "Unchanged: the hit roll, the fire interval, range, the snipe roll and every driver hit (DC2), a hull hit, buildings (bullets never target one), grenades, every other shooter, the hatch drivers' guns (DV87). Part 2 (SmallArmsZ95Deviation: bullets barely scratch hulls, the Z95 crew roll) is OFF: an offer to the owner, set off at every start. Integers only, no random draw of its own",
                measured = measured,
                toggle = "ZWorld.SmallArmsDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows and probes hold it off; off in every counted check unless the check sets it). Part 2: ZWorld.SmallArmsZ95Deviation, off (View/ZGame.cs)",
                status = !pass ? DevCheck : DevActive,
            });
        }

        /// <summary>The Unit editor's stats with DV212 (View/OzUeLiveStats.cs): "vs Grunt" shows what the sim does (on: grunt 155, psycho 40,
        /// jeep 45, gatling 75; off: today's), and the OpenZ set made into a plan leaves the game's rifle-pair table as it is.</summary>
        public static string StatsPanelCheck(string settings)
        {
            var sb = new System.Text.StringBuilder();
            var rows = ZSettings.Parse(settings);
            var on = UeRules.FromGame(); on.pairOn = true; on.smallArmsOn = true;
            var off = on.Clone(); off.smallArmsOn = false;
            var got = new List<string>(); bool ok = on.Clone().smallArmsOn && !UeRules.ZodRules().smallArmsOn;
            foreach (var id in new[] { "grunt", "psycho", "jeep", "gatling", "tough", "sniper" })
            {
                var tOn = OzUeLiveStats.Cells(rows, on, id); var tOff = OzUeLiveStats.Cells(rows, off, id);
                int vOn = (int)tOn[(int)UeStat.VsGrunt].value, vOff = (int)tOff[(int)UeStat.VsGrunt].value;
                int want = ZWorld.SmallArmsVsGrunt(id, vOff);
                ok &= vOn == want && (id == "tough" || id == "sniper" ? vOn == vOff : vOn > vOff);
                got.Add($"{id} {vOff} -> {vOn}");
            }
            string pairsBefore = UeRules.PairText(on.pairs);
            var openz = OzUeLiveStats.Table(rows, on);
            var plan = OzUeStatsApply.Make(settings, on, openz, null);
            bool pairsKept = UeRules.PairText(plan.rules.pairs) == pairsBefore && plan.rules.smallArmsOn;
            sb.AppendLine(OzV11Kit.Verdict(ok && pairsKept, $"the Unit editor's 'vs Grunt' with DV212 off -> on: {string.Join(", ", got)}; the OpenZ set as a plan keeps the game's rifle pairs ({(pairsKept ? "kept" : "CHANGED: " + UeRules.PairText(plan.rules.pairs))})"));
            return sb.ToString();
        }

        /// <summary>Batch: only the DV212 row (-executeMethod OpenZ.EditorTools.OpenZParity.SmallArmsRowBatch) into Logs/small_arms_DV212.txt with
        /// the whole check after it; exit 0 = ACTIVE and every line of the whole check PASS.</summary>
        public static void SmallArmsRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                using (ZWorld.SmallArmsOff()) AddSmallArmsDeviation(list);
                string full; using (OzV11Kit.GameSwitches()) full = OzV15SmallArmsDuel.Check(SettingsText) + StatsPanelCheck(SettingsText);
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                var d = list[0];
                string text = $"DV212 row: status {d.status}\nmeasured: {d.measured}\n\nthe whole check: {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/small_arms_DV212.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = d.status == DevActive && f == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV212 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }

    public static class OzV15SmallArmsMenu
    {
        static string PlayFolder => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/small_arms_balance_r8/proof/play"));

        [MenuItem("OpenZ/Checks/V1.5/Small arms vs robots: the whole check (no Play)")]
        static void Count()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] Small arms (DV212): leave Play first (the check sets the simulation's static switches)"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15SmallArmsDuel.Check(OzV11Kit.SettingsText) + OpenZParity.StatsPanelCheck(OzV11Kit.SettingsText);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/small_arms_DV212_check.txt"), r);
            Debug.Log("[OpenZ V1.5] Small arms (DV212):\n" + r);
        }

        /// <summary>Play (a level playing in the normal mode, L02): a grunt duel and a psycho squad against a grunt squad, each with DV212 off,
        /// then on; report and one frame per ON fight in the kit's proof/play.</summary>
        [MenuItem("OpenZ/Checks/V1.5/Small arms vs robots: Play check (a level playing)")]
        static void Play()
        {
            if (!Application.isPlaying) { Debug.LogWarning(OzSmallArmsPlayCheck.Tag + " start a level in Play first (L02, the normal mode, not 'Zod exact')"); return; }
            if (OzSmallArmsPlayCheck.Running) { Debug.LogWarning(OzSmallArmsPlayCheck.Tag + " already running"); return; }
            OzSmallArmsPlayCheck.Folder = PlayFolder;
            OzSmallArmsPlayCheck.Begin();
            Debug.Log(OzSmallArmsPlayCheck.Tag + " started (about 1.5 minutes: a grunt duel, then psychos against grunts, each off then on); report and frames in " + PlayFolder);
        }

        [MenuItem("OpenZ/Checks/V1.5/Small arms vs robots: last Play report")]
        static void Last() => Debug.Log(OzSmallArmsPlayCheck.Report);
    }
}
#endif
