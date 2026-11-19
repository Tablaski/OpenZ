#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit bizon_mammoth_driver_up_r7: the parity row DV211 "the big tanks' driver is up while they fight" (Sim/ZObject.BigTankUp.cs, the
    /// hook in Sim/ZObject.DriverFire.cs DriverFireStep), the menu of its counted check (OzV15BigTankUpCount), the row's batch, and the menus of the
    /// Play check (View/OzBigTankUpPlayCheck.cs).</summary>
    public static partial class OpenZParity
    {
        static void AddBigTankUpDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15BigTankUpCount.Quick(SettingsText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/Checks/V1.5/Big tanks: driver up while they fight (no Play))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV211", change = "The MAMMOTH's and the BIZON's driver is up in the open hatch the whole time they fight (a target, the drive in to one, and 8 s after the last), so snipers can shoot him out much more often; the owner: \"to make them weaker ... their driver is almost always on top\"",
                zod = "Zod opens a lid tank's hatch 4 times in 5 when the tank takes a new target that can snipe (zvehicle.cpp:198-207: never for a tank, a Tough, a gun or a building) and shuts it 0-1.4 s after the tank lets go of it (zvehicle.cpp:209-240; a close still pending at the next target is not called off); the driver can be sniped only while it is open (zvehicle.cpp:178-186). The big tanks are OpenZ's (DV180 / DV181) and had the heavy tank's lid",
                openz = "Sim/ZObject.BigTankUp.cs, called in DriverFireStep right after DV87 part B: for an owned, crewed Mammoth or Bizon (a veteran crew too: the view shows him while held, View/BoardingView.cs ShowHatchDriver) the lid is open while it has a target or drives in to one on an attack / agro order inside its agro reach, and Zod's own close of the lid is set to 8 s after the last such tick (BigTankUpLingerSeconds); then it shuts as before. " +
                        "Unchanged: how a driver who is up can be sniped (CanBeSniped as for an open lid, every roll, pool and floor, DV12's veteran factor), his own gun while up (DV87 part A; a veteran's stays silent), no driver up on a bridge or in a door lane (DV87 part B), Zod's lid roll, every other vehicle. No random draw of its own, sim time and sim state only",
                measured = measured,
                toggle = "ZWorld.BigTankUpDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows and probes hold it off; off in every counted check unless the check sets it)",
                status = !pass ? DevCheck : DevActive,
            });
        }

        /// <summary>Batch: only the DV211 row (-executeMethod OpenZ.EditorTools.OpenZParity.BigTankUpRowBatch) into Logs/big_tank_up_DV211.txt with the
        /// whole check after it; exit 0 = ACTIVE and every line of the whole check PASS.</summary>
        public static void BigTankUpRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                using (ZWorld.BigTankUpOff()) using (ZWorld.BigTanksOff()) AddBigTankUpDeviation(list);
                string full; using (OzV11Kit.GameSwitches()) full = OzV15BigTankUpCount.Check(SettingsText);
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                var d = list[0];
                string text = $"DV211 row: status {d.status}\nmeasured: {d.measured}\n\nthe whole check: {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/big_tank_up_DV211.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = d.status == DevActive && f == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV211 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }

    public static class OzV15BigTankUpMenu
    {
        static string PlayFolder => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/bizon_mammoth_driver_up_r7/proof/play"));

        [MenuItem("OpenZ/Checks/V1.5/Big tanks: driver up while they fight (no Play)")]
        static void Count()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] Big tanks driver up (DV211): leave Play first (the check sets the simulation's static switches)"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15BigTankUpCount.Check(OzV11Kit.SettingsText);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/big_tank_up_DV211_check.txt"), r);
            Debug.Log("[OpenZ V1.5] Big tanks driver up (DV211):\n" + r);
        }

        /// <summary>Play (a level playing in the normal mode, L02 or L18): a Bizon and a Mammoth each fight three sniper squads for 60 s with DV211 on
        /// (as the game set it); report and one frame each in the kit's proof/play.</summary>
        [MenuItem("OpenZ/Checks/V1.5/Big tanks: driver up, Play check (a level playing)")]
        static void Play() => Run(0);

        /// <summary>The same with DV211 off for the run (today's numbers, put back after).</summary>
        [MenuItem("OpenZ/Checks/V1.5/Big tanks: driver up, Play check BEFORE (DV211 off for the run)")]
        static void Before() => Run(1);

        [MenuItem("OpenZ/Checks/V1.5/Big tanks: driver up, last Play report")]
        static void Last() => Debug.Log(OzBigTankUpPlayCheck.Report);

        static void Run(int mode)
        {
            if (!Application.isPlaying) { Debug.LogWarning(OzBigTankUpPlayCheck.Tag + " start a level in Play first (the normal mode, not 'Zod exact')"); return; }
            if (OzBigTankUpPlayCheck.Running) { Debug.LogWarning(OzBigTankUpPlayCheck.Tag + " already running"); return; }
            OzBigTankUpPlayCheck.Folder = PlayFolder; OzBigTankUpPlayCheck.Mode = mode;
            OzBigTankUpPlayCheck.Begin();
            Debug.Log(OzBigTankUpPlayCheck.Tag + " started (about 2.5 minutes: a Bizon, then a Mammoth, each against three sniper squads); report and frames in " + PlayFolder);
        }
    }
}
#endif
