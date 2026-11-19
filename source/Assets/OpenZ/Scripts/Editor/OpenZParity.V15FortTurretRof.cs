#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Sim/ZObject.FortTurretRof.cs). Measured headless on a plain map (Editor/OzV15FortTurretRofCount.cs Quick); the whole check: menu
    /// OpenZ/V1.5/Fort Turret Reload: Count (headless, DV177). The parity's Zod rows and the block's other probes run with the switch held off
    /// (ZWorld.FortTurretRofOff).</summary>
    public static partial class OpenZParity
    {
        static void AddFortTurretRofDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15FortTurretRofCount.Quick(SettingsText);   // the counted checks' game switches, put back after
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.5/Fort Turret Reload: Count (headless, DV177))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV177", change = "A MISSILE CANNON standing on a fort's tower reloads twice as long (half the rate of fire); on the ground it is unchanged",
                zod = "unit.missile_cannon.attack_speed=1.124 s for every missile cannon, on a fort's tower spot or on the ground (default_settings.txt; zobject.cpp:2102-2213: "
                      + "next shot = now + attack_speed); a tower spot only makes the cannon not ejectable (bfort.cpp:534-548)",
                openz = $"a missile cannon on one of a fort's four tower spots (not ejectable: the map's own, one placed from the fort's store, DV73's door-tower gun on HARD / MAYHEM) "
                        + $"waits {ZObject.FortTurretReloadFactor} x attack_speed between shots: 1.124 -> 2.248 s, every 225 ticks instead of 113 on the 10 ms clock; its first shot, damage, "
                        + "blast, range (DV162's high ground included), rocket speed and health stay as they are. Ground missile cannons, the Mobile Missile vehicle and the fort's other "
                        + "tower guns are untouched. One hook after Zod's reload line and its two no-shot exits (only a shot that leaves waits twice); the row's attack_speed is not changed. Deterministic: one more addition of the unit's own reload",
                measured = measured,
                toggle = "ZWorld.FortTurretRofDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off: Zod's reload)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
