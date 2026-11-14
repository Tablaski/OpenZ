#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV171 EXIT DRIVER (version 1.4; the rule: Sim/ZObject.VehicleExit.cs). Measured headless
    /// (Editor/OzV14VehicleExitCount.cs Quick, on a plain map); the whole check: menu OpenZ/V1.4/Vehicle Exit: Count (headless, DV171). Only the player's EXIT
    /// on a tank or the mech acts, so a bot match and the parity's other probes are as before.</summary>
    public static partial class OpenZParity
    {
        static void AddVehicleExitDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV14VehicleExitCount.Quick(SettingsText);   // the counted checks' game switches, put back after
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/V1.4/Vehicle Exit: Count (headless, DV171))";
                string stop; using (OzV11Kit.GameSwitches()) stop = OzV14VehicleExitStopCount.Quick(SettingsText);   // vehicle_exit_jump: the stop rule's and the hold's counted rows
                measured += "; stop and hold: " + OzV11Kit.VerdictCell(stop, out bool passStop) + " (menu OpenZ/V1.4/Vehicle Exit Jump: Stop Count (headless))"; pass &= passStop;
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV171", change = "EXIT DRIVER: the player can let the driver of a light, medium or heavy tank or the mech climb out through its hatch; for 30 s nobody climbs in by himself",
                zod = "only an APC and an ejectable gun eject their crew (vapc.cpp:261, zcannon.cpp:51; EJECT_VEHICLE zserver_events.cpp:631-731); a tank keeps its driver until he is "
                      + "sniped; an empty vehicle is auto-entered by any idle robot within auto_grab_vehicle_distance (zobject.cpp:1603-1853)",
                openz = "the gun's EJECT_VEHICLE (the EXIT DRIVER tab in EXIT CREW's place, 80 % of its size, or the EXIT CREW key) also lets out the driver of a light / medium / heavy tank "
                        + "or the mech of a human team: the same robot (type, hit points, kills, rank, hero) on the first free spot beside the hull (two square rings, S first), the vehicle "
                        + $"neutral, stopped and empty in the same tick; refused: on a bridge, no free spot with room on its side of the hull (no wall or building in the half of the first ring's "
                        + $"square facing it; a flag stops only a spot on it), on its factory / "
                        + $"repair exit move; while its last driver is still held. The driver is held as getting out (DV132's hold: no step, no shot; unlike DV132's own robots he is a target: enemies pick, hit and can kill him, drawn on his figure) "
                        + $"until his figure, jumping off the hull with one somersault (vehicle_exit_jump3: the lid kicked open in 0.12 s, he shows crouched as it opens, a 0.09 s push from his own deep crouch with his soles planted, a leap mostly up under 9.81 m/s2, his own clips only), "
                        + $"touches the ground: light tank {ZObject.DriverOutHoldTicks("light")}, medium tank {ZObject.DriverOutHoldTicks(ZMTank.Type)}, heavy tank {ZObject.DriverOutHoldTicks("heavy")}, "
                        + $"mech {ZObject.DriverOutHoldTicks(ZMTank.Mech)} ticks (before: a 5.2-8.4 s climb down), at once free when the hull is destroyed. The vehicle stops dead in the EXIT's own tick, and so does one whose "
                        + $"driver is sniped or killed inside: its order, path and target dropped, no new shot of any weapon (its gun, the heavy's second barrel, the mech's hand gun, the hatch driver's own weapon), "
                        + $"no step, no turn from that tick on (DV79's unmanned stop, on at every start; counted with DV146 and DV87 on, as at every start). "
                        + $"For {ZObject.DriverOutLockTicks} sim ticks (30 s) no unit "
                        + "of any team climbs into it by itself (idle auto-grab, DV55, DV84's grab on the way, an automatic ENTER on its way); a direct ENTER order (the player's right-click, the "
                        + "computer's capture orders) gets in at once and ends the lock (level 17 Easy's start crews, DV63, are the fort's orders: they skip a locked vehicle, and one already on its way "
                        + "walks to the spot next to it instead, DV59's walk). Guns, the APC, the jeep, the crane, the missile launcher and the shuttle as before. The computer never uses it.",
                measured = measured,
                toggle = "ZWorld.VehicleExitDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off); OpenZHud.KeyExitsDrivers (the EXIT CREW "
                         + "key also lets drivers out, on); the driver's hold follows ZWorld.ExitWalkHoldOn (DV132)",
                status = !pass ? DevCheck : DevActive,
            });
        }
    }
}
#endif
