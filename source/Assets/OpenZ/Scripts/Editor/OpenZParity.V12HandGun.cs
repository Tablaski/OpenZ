#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV146 "the mech's hand gun" (1.2 owner items 157 / 158; the rule: Sim/ZObject.MechHandGun.cs). Measured headless:
    /// the counted check of Editor/OzMechHandGunCount.cs (the Psycho's numbers, only during the rockets' reload, robots only, the forward arc,
    /// range, the switch off, the computer's mech, determinism; its balance table is in the menu's report).</summary>
    public static partial class OpenZParity
    {
        static void AddHandGunDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            bool was = ZWorld.MechHandGunDeviation;
            try
            {
                string check;
                using (OzV11Kit.GameSwitches()) check = OzMechHandGunCount.Run(SettingsText, 4);
                measured = OzV11Kit.VerdictCell(check, out pass) + " (the whole report: menu OpenZ/Mech v2/16 Hand gun: counted rules check)";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            finally { ZWorld.MechHandGunDeviation = was; }
            var s = ZSettings.Parse(SettingsText); var p = s.Unit(ZWorld.HandGunType); var m = s.Unit(ZWorld.HandGunCarrier);
            list.Add(new Deviation
            {
                id = "DV146", change = "The mech's right-hand machine gun fires: between two rocket shots it fires the Psycho's machine gun at enemy robots inside a forward arc",
                zod = "Z's medium tank (OpenZ's mech) has one weapon, its missile; the drawn gun in its hand never fires",
                openz = $"while the rockets reload ({ZWorld.HandGunAfterRocket:0.##} s after a rocket shot to {ZWorld.HandGunBeforeRocket:0.##} s before the next of its {m?.attack_speed:0.###} s cycle) and while it has its rocket target, " +
                        $"the Psycho's row: {p?.Damage} HP a hit (the rifle-pair value where DV78 has one), hit chance {p?.attack_damage_chance:0.###} (Zod's roll), a shot every {p?.attack_speed:0.###} s, range {p?.attack_radius} px; " +
                        $"enemy robots only (never vehicles, guns, buildings), at least {ZWorld.HandGunMinPx:0} px from the gun, inside +{ZWorld.HandGunOutDeg:0} deg outward / -{ZWorld.HandGunInDeg:0} deg inward of the gun's line to the rocket target " +
                        "(the arms of both models reach that without turning into the body), a clear line and no barrier; target: the rocket target if it is such a robot, else the last one, else the nearest (every 0.5 s). " +
                        "Player's and computer's mechs alike, no order needed. Deterministic: one Zod rand() hit roll per shot. " +
                        $"The gun turns first: its arm turns at {ZWorld.HandGunTurnDegS:0} deg/s toward its robot, a shot needs it within {ZWorld.HandGunAimTolDeg:0.#} deg of it, " +
                        $"at a new robot the first shot also waits {ZWorld.HandGunSettleS:0.00} s after the arm got there (a 40 deg turn: {40 / ZWorld.HandGunTurnDegS + ZWorld.HandGunSettleS:0.00} s), " +
                        $"it holds on the last robot {ZWorld.HandGunHoldS:0.0} s after a shot (ZObject.MechHandGunAim.cs)",
                measured = measured,
                toggle = "ZWorld.MechHandGunDeviation (code switch; ZGame: on, off in 'Zod exact'; headless default off)",
                status = pass ? DevActive : DevCheck,
            });
        }
    }
}
#endif
