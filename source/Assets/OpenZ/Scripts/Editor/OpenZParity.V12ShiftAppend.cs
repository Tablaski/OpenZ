#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV135 "a Shift queue is added to the units' current order" (1.2 item 9; the rule: View/OzShiftAppend.cs, the
    /// switch Sim/ZWorld.ShiftAppend.cs). Measured headless by the kit's counted check (Editor/OzV12ShiftAppendCount.cs) with one run per row,
    /// the rule off and on, the switches of a normal game; the switch is put back afterwards (the whole check, 3 runs per row: menu
    /// OpenZ/V1.2/Orders: 9 Shift Adds Check (headless)).</summary>
    public static partial class OpenZParity
    {
        static void AddShiftAppendDeviation(List<Deviation> list, Func<string, int, int> getInt)
        {
            string measured; bool pass;
            bool was = ZWorld.ShiftAppendDeviation;
            try
            {
                string check = OzV12ShiftAppendCount.Check(SettingsText, 1);
                measured = OzV11Kit.VerdictCell(check, out pass) + " (the whole report: menu OpenZ/V1.2/Orders: 9 Shift Adds Check (headless))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            finally { ZWorld.ShiftAppendDeviation = was; }
            bool on = getInt("oz_zod_exact", 0) == 0;
            list.Add(new Deviation
            {
                id = "DV135", change = "A Shift queue is added to the order the player's units already have (order flag 1, then Shift-click flag 2: they take both)",
                zod = "the Shift queue is sent when Shift goes up as one SEND_WAYPOINTS, which replaces the unit's whole list (zplayer.cpp:2942-3016): a plain order given before it (flag 1) was thrown away",
                openz = "when a Shift queue is sent, the player's input puts in front of each unit's new points the waypoints of its current order that the player gave, as they are (a group order's own spot), " +
                        "without the movement layer's own moves (step aside, back off, regroup, a bridge's waiting bay), the waypoints a unit gave itself (an attack-move's target, a dodge, a flag it went for by itself) " +
                        "and the first one when it cannot be overwritten (SEND_WAYPOINTS keeps that one itself); a plain click, a squad member, a patrol (it never ends) and the shuttle (DV125 does the same for its route) as before. " +
                        "SEND_WAYPOINTS and every rule of the simulation unchanged: the command carries the whole list, so every peer and a replay see the same order. The Shift preview draws the queue's line from where the list will go on",
                measured = measured,
                toggle = $"ZWorld.ShiftAppendDeviation: the game sets it at every start, off in 'Zod exact' (oz_zod_exact = {(on ? "0" : "1")}); read by the player's input only (the rows above never send a Shift queue)",
                status = !pass ? DevCheck : on ? DevActive : DevActive + " (off by pref)",
            });
        }
    }
}
#endif
