#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV137 "the computer sends its strongest damaged vehicle to its repair station and back, once every 5 minutes"
    /// (1.2 item 13; the rule: Sim/ZBot.CpuRepair.cs). Measured headless by the quick form of the kit's counted check (Editor/OzV12CpuRepairCount.cs:
    /// the Car Park, one seed, a computer match and the stand-in player, the switch on and off); the switch is put back afterwards (the whole check:
    /// menu OpenZ/V1.2/CPU: 13 Repairs Check (headless)).</summary>
    public static partial class OpenZParity
    {
        static void AddCpuRepairDeviation(List<Deviation> list, Func<string, int, int> getInt)
        {
            string measured; bool pass;
            bool was = ZWorld.CpuRepairDeviation;
            try
            {
                string check = OzV12CpuRepairCount.Check(SettingsText, MapText, true);
                measured = OzV11Kit.VerdictCell(check, out pass) + " (the whole report: menu OpenZ/V1.2/CPU: 13 Repairs Check (headless))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            finally { ZWorld.CpuRepairDeviation = was; }
            bool on = getInt("oz_zod_exact", 0) == 0;
            double every = ZWorld.CpuRepairEverySec;
            list.Add(new Deviation
            {
                id = "DV137", change = $"The computer repairs: once every {every / 60:0} minutes a computer player that owns a repair station sends its strongest damaged heavy tank, missile launcher or shuttle (else a medium tank) to the station and back, like the R key",
                zod = "the computer sends a damaged vehicle to repair only when its order round happens to pair it with its own station (zbot.cpp, the mutual-nearest match), with attack_to set and no way back; rare",
                openz = $"every {every:0} s of game time ({every * ZWorld.EasyDecisionSlow:0} s on EASY, K434), from {every / 60:0}:00 on, one trip at a time: of its own vehicles that have lost at least {ZWorld.CpuRepairMinLost * 100:0} % of their hit points, " +
                        $"the first by kind (heavy tank, missile launcher, shuttle, medium tank), then the most hit points lost, then the lower id; not a unit already on a repair trip, a squad member, a fort defender, a member of a forming / marching strike group (DV107), a guard of the defend orders (DV136), a vehicle in a fight (hit by an enemy in the last {ZWorld.CpuRepairCalmSec:0} s or firing at a target now), " +
                        "a vehicle whose order cannot be replaced; the nearest own station it can drive to and whose waiting spot it can stand on (DV62 waits 40 px out of the entrance; a cannon nearer than the movement layer's spacing blocks it). The R key's order through SEND_WAYPOINTS (UNIT_REPAIR, attack_to off, then a MOVE back to where it stood): the same repair trip of DV62 (queue, bay, pad, healed to full) " +
                        $"and the drive back to where it was. On the way it is held: the computer's order rounds and its idle rules (DV56, DV37, DV72, DV60, DV70) leave it alone until it is back, destroyed, lost, or after {ZWorld.CpuRepairMaxTripSec:0} s (a far station: {ZWorld.CpuRepairTripSlackSec:0} s + {ZWorld.CpuRepairTripDriveFactor:0} x the straight drive there and back, when longer). " +
                        "A shuttle is sent by the shuttle rule's own repair order (DV138; never a ship on one of that rule's runs; with that rule off the order is refused and the ship skipped for a period). Deterministic: game time and the object order, no random draw",
                measured = measured,
                toggle = $"ZWorld.CpuRepairDeviation: the game sets it at every start, off in 'Zod exact' (oz_zod_exact = {(on ? "0" : "1")}); needs DV62 (oz_repair_pad); headless default off, held off by the parity report, the Deviations block, the computer's own checks and the counted checks",
                status = !pass ? DevCheck : on ? DevActive : DevActive + " (off by pref)",
            });
        }
    }
}
#endif
