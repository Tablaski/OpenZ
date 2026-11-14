#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV141 "the crane's driver can be sniped while he is up in the cab hatch, and stays inside while the
    /// crane works" (1.2 items 32-34; the rule: Sim/ZObject.CraneSnipe.cs). Measured headless by the kit's counted check
    /// (Editor/OzV12CraneSnipeCount.cs) in its quick form: the rule lines, 10 fights each of a sniper squad, a jeep and a grunt squad
    /// against a crewed crane, the rule off and on, and W1 (a crane under fire begins a job: its driver drops back in and stays inside
    /// while it works), the switches of a game in Play; the switch is put back afterwards (the whole check: menu OpenZ/V1.2/Combat: 32
    /// Crane Driver Snipe Check (headless)).</summary>
    public static partial class OpenZParity
    {
        static void AddCraneSnipeDeviation(List<Deviation> list, Func<string, int, int> getInt)
        {
            string measured; bool pass;
            bool was = ZWorld.CraneSnipeDeviation;
            try
            {
                string check = OzV12CraneSnipeCount.Quick(SettingsText);
                measured = OzV11Kit.VerdictCell(check, out pass) + " (the whole check: menu OpenZ/V1.2/Combat: 32 Crane Driver Snipe Check (headless))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            finally { ZWorld.CraneSnipeDeviation = was; }
            bool on = getInt("oz_zod_exact", 0) == 0;
            list.Add(new Deviation
            {
                id = "DV141", change = "The crane's driver can be shot out while he is up in the cab hatch firing (DV31), like the driver of a tank with its lid open; while the crane works he stays inside",
                zod = "the crane has no weapon and its driver can never be hit (vcrane.cpp sets no can_be_sniped, zobject.cpp:77; CanBeSniped zobject.cpp:4693-4696)",
                openz = "while the driver is up (from the moment he pops out to fire until CraneGunLinger after his last shot) the crane is a vehicle whose driver can be hit: " +
                        "Zod's list of who can snipe, the hit and snipe rolls with each attacker's own chance (zobject.cpp:2187-2199) and the OpenZ factors on them (DV27, DV12, DV121), " +
                        $"the driver's own health as the pool; one driver hit removes at least {ZWorld.CraneDriverHitFloor} HP (the jeep's open-seat floor of DC2; snipers x{ZWorld.SniperDriverFactor}); " +
                        "the emptied crane turns neutral like every sniped vehicle (stopped, orders gone, any team can drive it); " +
                        "with the driver inside or resting (DV75) it cannot be sniped; while the crane works (a crane job begun, from its CraneAnim on to its end: the rebuild of a " +
                        "ruin, the mend of a damaged building) the driver does not come up, drops back in when he is up as the job begins, and the crane cannot be sniped; " +
                        "on a DV82 spot (a standing bridge, a fort or factory exit lane) and in a ruin (a destroyed bridge and 88 px before its ends, a destroyed radar, " +
                        "repair station or factory) the driver stays inside and does not fire",
                measured = measured,
                toggle = $"ZWorld.CraneSnipeDeviation: the game sets it at every start, off in 'Zod exact' (oz_zod_exact = {(on ? "0" : "1")}); needs DC2 (SnipeDeviation) and DV31 (oz_crane_gun); held off in the other rows' probes and the counted checks",
                status = !pass ? DevCheck : on ? DevActive : DevActive + " (off by pref)",
            });
        }
    }
}
#endif
