#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit ue_boxes: the parity row DV194 and the menu of its counted check (OzV15UeBoxesCount).</summary>
    public static partial class OpenZParity
    {
        static void AddUeBoxesDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15UeBoxesCount.Quick(SettingsText, MapText("UE"), MapText("L02"));
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.5/Unit Editor/Boxes: Count (headless, DV194))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV194", change = "the Unit editor's BOX: holo boxes that stack, and the boxes above fall when one under them is destroyed",
                zod = "no such object (the 1996 Z has no Unit editor); the playground's holo obstacle (1.2) is a destroyable map barrier on the harbour-wreck rule (DV39)",
                openz = $"a {ZWorld.BoxTiles} x {ZWorld.BoxTiles} tile box on the obstacle's rule (neutral, {ZWorld.PlaygroundBoxHp} HP, explosives only, its tiles closed); dropped on boxes it goes on top "
                        + $"(level = the highest box under it + 1, at most {ZWorld.BoxLevels}); a blast hurts the boxes of the aimed box's level only (floor level when aimed at nothing; the NUKE all); "
                        + "a box gone: the boxes above settle the same tick, whole levels, lowest first (the view draws the fall with gravity); integers only, no random draw; "
                        + "N29: a box is attacked from the attacker's weapon range measured to its near face, also when it stands in a unit's way (Zod blasts what is in the way at the contact), "
                        + "never a ram / crush / shove / chain target; the holo obstacle as before; "
                        + "N29b: of a group ordered onto a box every unit takes its own firing place inside its own range and fires from there until no box stands on the place "
                        + "(a unit that cannot blast a box waits off it)",
                measured = measured,
                toggle = "none needed: only the Unit editor places a box (View/OzUePlayground.Boxes.cs); in every level and match the hooks (ProcessMissileDamage BoxBlastOff, Tick BoxSettleTick) return at their first line",
                status = !pass ? DevCheck : DevActive,
            });
        }

        /// <summary>Batch: only the DV194 row (-executeMethod OpenZ.EditorTools.OpenZParity.UeBoxesRowBatch): the row and the whole counted
        /// check into Logs/ue_boxes_DV194.txt; exit 0 = the row ACTIVE and every line PASS, 1 = not. With -oz_box_icon_out &lt;folder&gt; (a run
        /// with graphics) it first renders the BOX tile picture there (Editor/OzV15UeBoxIcon.cs; a failed render = exit 1).</summary>
        public static void UeBoxesRowBatch()
        {
            int code = 1;
            try
            {
                var args = Environment.GetCommandLineArgs(); string iconDir = null;
                for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-oz_box_icon_out") iconDir = args[i + 1];
                bool icon = iconDir == null || OzV15UeBoxIcon.Render(iconDir);
                var list = new List<Deviation>();
                AddUeBoxesDeviation(list);
                var settings = Resources.Load<TextAsset>("OpenZ/default_settings").text;
                string full; using (OzV11Kit.GameSwitches()) full = OzV15UeBoxesCount.Run(settings, OzV11Kit.MapText("UE"), OzV11Kit.MapText("L02"));
                var d = list[0];
                bool active = d.status == DevActive;
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                string text = $"DV194 row: status {(active ? "ACTIVE" : "CHECK")}\nzod: {d.zod}\nopenz: {d.openz}\nmeasured: {d.measured}\n\nthe whole check: {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/ue_boxes_DV194.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = active && f == 0 && icon ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV194 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }

    public static class OzV15UeBoxesMenu
    {
        [MenuItem("OpenZ/V1.5/Unit Editor/Boxes: Count (headless, DV194)")]
        static void Count()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] boxes count: leave Play mode first (the check sets the simulation's static switches)"); return; }
            var settings = Resources.Load<TextAsset>("OpenZ/default_settings");
            if (!settings) { Debug.LogError("[OpenZ V1.5] boxes count: no Resources/OpenZ/default_settings"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15UeBoxesCount.Run(settings.text, OzV11Kit.MapText("UE"), OzV11Kit.MapText("L02"));
            if (r.Contains("] PASS")) Debug.Log("[OpenZ V1.5] " + r); else Debug.LogError("[OpenZ V1.5] " + r);
        }
    }
}
#endif
