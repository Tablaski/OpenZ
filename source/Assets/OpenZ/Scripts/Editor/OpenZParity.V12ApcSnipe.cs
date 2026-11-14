#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV143 "the APC's lone driver can be sniped while he is up in the roof hatch" (1.2 item 69; the rule:
    /// Sim/ZObject.ApcSnipe.cs). Measured headless by the kit's counted check (Editor/OzV12ApcSnipeCount.cs) in its quick form: 10 fights
    /// each of a sniper squad, a jeep and a grunt squad against an APC with its driver alone, the rule off and on, the rule lines, and an
    /// APC with a grunt squad aboard (the rule on tick for tick the rule off), the switches of a game in Play; the switch is put back
    /// afterwards (the whole check: menu OpenZ/V1.2/Combat: 69 APC Driver Snipe Check (headless)).</summary>
    public static partial class OpenZParity
    {
        static void AddApcSnipeDeviation(List<Deviation> list, Func<string, int, int> getInt)
        {
            string measured; bool pass;
            bool was = ZWorld.ApcSnipeDeviation;
            try
            {
                string check = OzV12ApcSnipeCount.Quick(SettingsText);
                measured = OzV11Kit.VerdictCell(check, out pass) + " (the whole check: menu OpenZ/V1.2/Combat: 69 APC Driver Snipe Check (headless))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            finally { ZWorld.ApcSnipeDeviation = was; }
            bool on = getInt("oz_zod_exact", 0) == 0;
            list.Add(new Deviation
            {
                id = "DV143", change = "The APC's lone driver can be shot out while he is up in the roof hatch firing (DV87 part C), like the crane's driver and the driver of a tank with its lid open",
                zod = "the APC fires its crew's weapon from inside, shows nobody and can never be sniped (vapc.cpp sets no can_be_sniped, zobject.cpp:77; CanBeSniped zobject.cpp:4693-4696)",
                openz = "while the APC's driver rides alone and is up (from the moment the APC has a target until CraneGunLinger after it last had one) the APC is a vehicle whose driver can be hit: " +
                        "Zod's list of who can snipe, the hit and snipe rolls with each attacker's own chance (zobject.cpp:2187-2199) and the OpenZ factors on them (DV27, DV121), " +
                        $"the driver's own health as the pool; one driver hit removes at least {ZWorld.ApcDriverHitFloor} HP (the crane driver's floor, DV141; snipers x{ZWorld.SniperDriverFactor}); " +
                        "the emptied APC turns neutral like every sniped vehicle (stopped, orders gone, any team can drive it: a squad's leader drives, the others ride, DV15); " +
                        "with passengers aboard, for a veteran crew and on a bridge or in a door lane (DV82) he stays inside as before and the APC cannot be sniped",
                measured = measured,
                toggle = $"ZWorld.ApcSnipeDeviation: the game sets it at every start, off in 'Zod exact' (oz_zod_exact = {(on ? "0" : "1")}); needs DC2 (SnipeDeviation) and DV87 (DriverFireDeviation); held off in the other rows' probes and the counted checks",
                status = !pass ? DevCheck : on ? DevActive : DevActive + " (off by pref)",
            });
        }
    }
}
#endif
