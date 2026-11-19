#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit ram_polish: the parity row DV188 and the menu of its counted check (OzV15RamPolishCount).</summary>
    public static partial class OpenZParity
    {
        static void AddRamPolishDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15RamPolishCount.Quick(SettingsText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.5/Ram Polish: Count (headless, DV188))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV188", change = "the Mammoth ram / Bizon crush polish: no fire at the ram target (the next enemy at once), the doomed vehicle fires nothing, the contact at the DRAWN hulls, the Mammoth's lift under it to the tipping point, the rolled hull squashes enemy robots behind it",
                zod = "Z has no ram or crush (DV182 / DV183 are OpenZ's)",
                openz = "fire: a big tank never fires at its ram / crush target from the start until it is dead (dropped, Zod's passive pick at once without it); a doomed vehicle attacks nothing; contact: centre distance <= the drawn front reach + the target's drawn half extent along the line (oriented boxes of the drawn hulls, whole px); lift: the Mammoth drives on under it for the target's drawn length along the ram "
                        + (ZObject.RamLiftOverPx >= 0 ? "+ " : "- ") + System.Math.Abs(ZObject.RamLiftOverPx) + " px (" + ZObject.RamLiftMinPx + "-" + ZObject.RamLiftMaxPx + " px, <= " + ZObject.RamPushMaxTicks + " ticks), easing from its boost to " + ZObject.RamTipSpeedPx + " px/s, the target held in place (never shoved); then the heave (the tipping point), the Mammoth braking to a stop at " + ZObject.RamStopAccelPx
                        + " px/s2, and the kill " + ZObject.RamFlipTicks + " ticks later; sweep: enemy robots of the Mammoth in the band behind the target (its width, the roll over its far edges: its length along the ram + its height) squashed as a run-over, never its own team",
                measured = measured,
                toggle = "ZWorld.RamPolishDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows and probes hold it off)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }

    public static class OzV15RamPolishMenu
    {
        [MenuItem("OpenZ/V1.5/Ram Polish: Count (headless, DV188)")]
        static void Count()
        {
            var settings = Resources.Load<TextAsset>("OpenZ/default_settings");
            if (!settings) { Debug.LogError("[OpenZ V1.5] ram polish count: no Resources/OpenZ/default_settings"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15RamPolishCount.Run(settings.text, true);
            int pass = 0, fail = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) pass++; else if (l.StartsWith("FAIL")) fail++; }
            Debug.Log($"[OpenZ V1.5] ram polish count (DV188): {pass} PASS, {fail} FAIL\n{r}");
        }

        [MenuItem("OpenZ/V1.5/Ram Polish: Play stats (log)")]
        static void Stats()
        {
            var s = new System.Text.StringBuilder("[OpenZ V1.5] ram polish play stats: pushes " + OzRamPush.Pushes + ", spark bursts " + OzRamPush.Bursts + ", heaves " + OzRamPush.Heaves + " (forced " + OzRamPush.ForcedHeaves + ")\n");
            foreach (var kv in OzRamPush.GapAtHeave) s.AppendLine($"  {kv.Key}: worst drawn gap at the heave {kv.Value:0.00} m (PASS <= 0.10), least push drawn {(OzRamPush.PushDrawn.TryGetValue(kv.Key, out var p) ? p : -1f):0.00} m");
            Debug.Log(s.ToString());
        }
    }
}
#endif
