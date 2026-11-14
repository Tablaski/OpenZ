#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV145 "the MEDIUM TANK" (1.2 owner item 139; the rule: Sim/ZMTank.cs). Measured headless: the counted check of
    /// Editor/OzV12MTankCount.cs (the rules row, a placed one against the mech and the light tank, the build lists, production, speed, the CPU
    /// on level 17 with two 15-minute matches; the switch off = Zod's lists).</summary>
    public static partial class OpenZParity
    {
        static void AddMTankDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            bool was = ZWorld.MTankDeviation;
            try
            {
                string check;
                using (OzV11Kit.GameSwitches()) check = OzV12MTankCount.Run(SettingsText, MapText, 900);
                measured = OzV11Kit.VerdictCell(check, out pass) + " (the whole report: menu OpenZ/Medium Tank/3 Rules check (counted, headless))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            finally { ZWorld.MTankDeviation = was; }
            var s = ZSettings.Parse(SettingsText); var u = s.Unit(ZMTank.Type); var m = s.Unit(ZMTank.Mech);
            list.Add(new Deviation
            {
                id = "DV145", change = "A new vehicle, the MEDIUM TANK (type mtank): built right after the Mech, in the Mech's time, at the Mech's tech level; 10 % more health than the Mech, about 15 % slower, the Light Tank's cannon",
                zod = "Z's medium tank slot is OpenZ's walking Mech (type medium); Z has no other tank between the light and the heavy tank",
                openz = $"unit.{ZMTank.Type} in default_settings.txt: build {u?.build_time} s (the mech's {m?.build_time} s), {u?.MaxHealth} HP (the mech's {m?.MaxHealth} x 1.10), speed {u?.move_speed} (the mech's {m?.move_speed} x 0.85 to the whole number), " +
                        $"the light tank's gun (range {u?.attack_radius} px, {u?.Damage} damage, blast {u?.attack_damage_radius} px, shell {u?.attack_missile_speed} px/s, reload {u?.attack_speed:0.###} s); a lid tank like the light tank " +
                        "(hatch, its driver snipable as the light tank's, explosives, turret debris); listed right after the mech in every fort and vehicle factory list that has the mech (levels 2-5), never in robot factories; " +
                        "the CPU builds it where Zod's pick is the mech, whichever of the two its team has fewer of (about one each; a build under way is finished first). Deterministic: no random draw",
                measured = measured,
                toggle = "ZWorld.MTankDeviation (code switch; ZGame: on, off in 'Zod exact'; headless default off: Zod's build lists)",
                status = pass ? DevActive : DevCheck,
            });
        }
    }
}
#endif
