#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit mammoth_ram_fight_n26c: the parity row DV196 (the Mammoth fights on while it rams; Sim/ZObject.RamFight.cs) and the menu of
    /// its counted check (OzV15RamFightCount). Measured headless on plain maps (Quick); the whole check: menu OpenZ/V1.5/Ram Fight: Count (headless,
    /// DV196). Only a Mammoth's gun target during its own ram changes (no level has a Mammoth), so a level, a bot match and the parity's other
    /// probes are as before. The parity report's Zod rows and every other row's probe hold it off.</summary>
    public static partial class OpenZParity
    {
        static void AddRamFightDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15RamFightCount.Quick(SettingsText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.5/Ram Fight: Count (headless, DV196))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV196", change = "the Mammoth fights on while it rams: from its ram's start until the vehicle it rammed is destroyed its gun takes the next enemy in range at once and fires at it at its normal rate; none in range: the fire held, the turret held where it is (no swing aside)",
                zod = "Z has no ram (DV182 / DV188 are OpenZ's); DV188 dropped the rammed target and took the next one only at the ram's start and then in Zod's 0.5 s passive look, and the view swung the idle turret " + UnitAim.RamClearTurnDeg + " deg aside with the barrels on their top stop",
                openz = "a crewed Mammoth in a ram (its ram running, a chained ram, or a vehicle it doomed not yet destroyed) looks at its gun's target every " + ZObject.RamLookTicks
                        + " ticks: a live enemy in range it may fire at is kept, else the next one in range is engaged at once with Zod's own passive pick (an exposed driver first, else the first in range; DV188 leaves the rammed out); engage only (no waypoint, no chase, no random draw); "
                        + "its own reload; none in range: no target, the fire held; the view (View/UnitAim.RamFight.cs) aims at that target or holds the turret, the barrels lifted only as far as the rammed hull needs (never inside it, DV185); after the ram the normal targeting",
                measured = measured,
                toggle = "ZWorld.RamFightDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; acts with DV182 / DV188; the parity report's Zod rows and probes hold it off); view switch UnitAim.RamFightViewOn",
                status = !pass ? DevCheck : DevActive,
            });
        }

        /// <summary>Batch: only the DV196 row (-executeMethod OpenZ.EditorTools.OpenZParity.RamFightRowBatch): the row and the whole counted check into
        /// Logs/ram_fight_DV196.txt; exit 0 = the row ACTIVE and every line of the whole check PASS, 1 = not.</summary>
        public static void RamFightRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                using (ZWorld.RamFightOff()) AddRamFightDeviation(list);
                var settings = Resources.Load<TextAsset>("OpenZ/default_settings").text;
                string full; using (OzV11Kit.GameSwitches()) full = OzV15RamFightCount.Run(settings, true);
                bool active = list.Count == 1 && list[0].status == DevActive;
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                var sb = new System.Text.StringBuilder();
                foreach (var d in list) sb.Append($"{d.id} row: status {(d.status == DevActive ? "ACTIVE" : "CHECK")}\nzod: {d.zod}\nopenz: {d.openz}\nmeasured: {d.measured}\n\n");
                string text = sb + $"the whole check: {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/ram_fight_DV196.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = active && f == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV196 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }

    public static class OzV15RamFightMenu
    {
        [MenuItem("OpenZ/V1.5/Ram Fight: Count (headless, DV196)")]
        static void Count()
        {
            var settings = Resources.Load<TextAsset>("OpenZ/default_settings");
            if (!settings) { Debug.LogError("[OpenZ V1.5] ram fight count: no Resources/OpenZ/default_settings"); return; }
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] ram fight count: leave Play mode first (the check sets the simulation's static switches)"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15RamFightCount.Run(settings.text, true);
            int pass = 0, fail = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) pass++; else if (l.StartsWith("FAIL")) fail++; }
            Debug.Log($"[OpenZ V1.5] ram fight count (DV196): {pass} PASS, {fail} FAIL\n{r}");
        }

        [MenuItem("OpenZ/V1.5/Ram Fight: Count (short, DV196)")]
        static void Short()
        {
            var settings = Resources.Load<TextAsset>("OpenZ/default_settings");
            if (!settings || Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] ram fight count (short): leave Play mode first"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15RamFightCount.Quick(settings.text);
            int pass = 0, fail = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) pass++; else if (l.StartsWith("FAIL")) fail++; }
            Debug.Log($"[OpenZ V1.5] ram fight count, short (DV196): {pass} PASS, {fail} FAIL\n{r}");
        }
    }
}
#endif
