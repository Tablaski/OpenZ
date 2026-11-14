#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV138 "the computer flies its shuttles" (1.2 item 12; the rule: Sim/ZBot.CpuShuttle.cs, on top of DV118,
    /// K256f's empty shuttle and DV125's routes). Measured headless by the kit's counted check (Editor/OzV12CpuShuttleCount.cs) with shorter
    /// matches; the switches are put back afterwards (the whole check: menu OpenZ/V1.2/CPU: 12 Shuttles Check (headless)).</summary>
    public static partial class OpenZParity
    {
        static void AddCpuShuttleDeviation(List<Deviation> list, Func<string, int, int> getInt)
        {
            string measured; bool pass;
            bool was = ZWorld.CpuShuttleDeviation;
            try
            {
                string check;
                using (OzV11Kit.GameSwitches()) check = OzV12CpuShuttleCount.Check(SettingsText, MapText("L02"), MapText("L16"), MapText("L17"), OzV12CpuShuttleCount.RowMatchSec);
                measured = OzV11Kit.VerdictCell(check, out pass) + " (the whole report: menu OpenZ/V1.2/CPU: 12 Shuttles Check (headless))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            finally { ZWorld.CpuShuttleDeviation = was; }
            bool on = getInt("oz_zod_exact", 0) == 0;
            list.Add(new Deviation
            {
                id = "DV138", change = "The computer flies its shuttles: it takes empty ones, flies a squad to a territory nobody defends, lands, lets it out to capture the flag, then waits at least two minutes",
                zod = "no shuttle in Zod (DV118 is an OpenZ unit); before: the computer never used one (its order rounds left every shuttle out, its orders to one were refused, only a human team's squad could board one)",
                openz = $"each computer player runs one shuttle plan beside its order rounds, one step a second (Normal and Easy): an EMPTY ship nobody of the other side defends is taken by its nearest idle squad (the player's ENTER order; " +
                        $"the leader becomes the pilot); its crewed ship, at a home spot by its fort, takes an idle squad aboard; when the cooldown is over it flies to the flag of a NEUTRAL or ENEMY territory with NO DEFENCE (none of the other side's robots or vehicles " +
                        $"within {ZWorld.CpuShuttleSafeUnitPx} px, no manned gun or fort whose reach + 32 px gets there, at the flag and at the landing spot 3 to {ZWorld.CpuShuttleSpotMaxTiles} tiles from it, in its territory; never a fort's territory; the way kept " +
                        $"out of every enemy fort's and gun's reach, by one middle point when needed), lands on a spot the landing rule takes, gives EXIT and sends the squad to the flag, then flies home. On the way it measures again once a second " +
                        $"(defended now: the next target, or home); over a spot it cannot land on: the next spot. The next run takes off no sooner than {ZWorld.CpuShuttleCooldownSec:F0} s after a run ended (the first from {ZWorld.CpuShuttleFirstSec:F0} s; the Easy computer 1.3 x). " +
                        "In the air the ship strafes under fire as every ship; the idle sweep (DV70) leaves it alone; another side's ship never speaks to the player (its sounds stay). Deterministic: game time, the object list's order, ids on ties; no random draw",
                measured = measured,
                toggle = $"ZWorld.CpuShuttleDeviation: the game sets it at every start, off in 'Zod exact' (oz_zod_exact = {(on ? "0" : "1")}); headless default off; off in every counted check (OzV11Kit.GameSwitches) and in the parity report",
                status = !pass ? DevCheck : on ? DevActive : DevActive + " (off by pref)",
            });
        }
    }
}
#endif
