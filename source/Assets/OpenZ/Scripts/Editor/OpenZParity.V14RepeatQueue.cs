#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV169 REPEAT QUEUE (version 1.4; the rule: Sim/ZObject.RepeatQueue.cs). Measured headless on a quiet level 2
    /// (Editor/OzV14RepeatQueueCount.cs Quick); the whole check: menu OpenZ/V1.4/Repeat Queue: Count (headless, DV169). Only a player's switch
    /// in the build menu turns it on for a building, so a bot match and the parity's other probes are as before.</summary>
    public static partial class OpenZParity
    {
        static void AddRepeatQueueDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try { measured = OzV11Kit.VerdictCell(OzV14RepeatQueueCount.Quick(SettingsText, OzV11Kit.MapText("L02")), out pass) + " (the whole check: menu OpenZ/V1.4/Repeat Queue: Count (headless, DV169))"; }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV169", change = "REPEAT QUEUE: a factory's or fort's whole production queue loops when its player switches it on in the build menu",
                zod = "the queue is built once and the last entry repeats forever (zbuilding.cpp:146-168, 477-506; OpenZ DV21: in order, the last unit repeats only with REPEAT on)",
                openz = "a per-building switch, off by default (= DV21 exactly). On: every unit the building finishes goes back to the END of its queue, so the unit in production and "
                        + $"the queue behind it (up to {ZObject.MAX_QUEUE_ITEMS}) loop in order; REPEAT does not matter while it is on (a list of one loops too). A queue entry the player removes "
                        + "leaves the loop; STOP clears production and queue as before; an entry the building can no longer build is skipped and dropped; at the unit cap the loop "
                        + "waits as production always does; a captured building starts with it off. The computer never switches it on (it never queues).",
                measured = measured,
                toggle = "ZWorld.RepeatQueueDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off) and each building's own "
                         + "ZObject.RepeatQueue (the build menu's REPEAT switch: ALL)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
