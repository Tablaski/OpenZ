#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV157 "the computer mans empty guns" (1.3 item 11; the rule: Sim/ZBot.CpuGunMan.cs). Measured headless by the
    /// counted check (Editor/OzV13GunManCount.cs) in its quick form: level 17, computer against computer, one seed, 16 minutes, the rule off and
    /// on; the switch is put back afterwards (the whole check, three levels: menu OpenZ/V1.3/CPU: Mans Empty Guns Check). Its own switch:
    /// PlayerPrefs oz_cpu_man_guns (menu OpenZ/Deviations/CPU Mans Empty Guns), off in 'Zod exact'.</summary>
    public static partial class OpenZParity
    {
        static void AddCpuGunManDeviation(List<Deviation> list, Func<string, int, int> getInt)
        {
            string measured; bool pass;
            bool was = ZWorld.CpuGunManDeviation;
            try
            {
                string check = OzV13GunManCount.Quick(SettingsText, MapText);
                measured = OzV11Kit.VerdictCell(check, out pass) + " (the whole check, three levels: menu OpenZ/V1.3/CPU: Mans Empty Guns Check (item 11, headless))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            finally { ZWorld.CpuGunManDeviation = was; }
            bool on = getInt("oz_zod_exact", 0) == 0 && getInt(ZGame.CpuGunManPref, 1) == 1;
            list.Add(new Deviation
            {
                id = "DV157", change = "The computer mans empty guns: the ones its squads walk past and the ones at its frontier",
                zod = "an empty gun is one target among all the others of the computer's order rounds (zbot.cpp:513-576), given to a unit only when the two are each other's nearest (zbot.cpp:222-414): a gun away from the idle units stays empty all game",
                openz = $"in every order round, after the round's delay, before DV37's car-park pass and the engine's random wave: a squad of the computer that walks within {ZWorld.CpuGunPassPx} px of an empty gun " +
                        "on its way to something that is neither a fight nor a flag (an empty vehicle, a grenade crate, a plain move) turns off and mans it; an empty gun in a frontier zone of the computer's " +
                        $"(its zone touching one that is not its own) or within {ZWorld.CpuGunFrontierPx} px of one gets the nearest idle robot squad that reaches it (at most {ZWorld.CpuGunSendPx} px away), from {ZWorld.CpuGunFrontierFromSec:0} s on and on the car park map once the side has {ZWorld.CpuGunCarsFirst} vehicles (with the ones its squads walk to). " +
                        $"One squad per gun, at most {ZWorld.CpuGunPerRound} gun orders a round (Easy {ZWorld.CpuGunPerRoundEasy}) and {ZWorld.CpuGunMaxEnRoute} squads on their way to a gun at once; never a squad firing, " +
                        $"on a flag run, attacking or entering a fort, never the last unit at one of its flags or its fort ({ZWorld.CpuGunGuardPx} px), never the Electrobot, a hero squad, a strike group member, a DV136 guard " +
                        "or a unit on a repair trip. Through the computer's own ENTER order (ZBot.Command). Both teams when the computer plays them, every level, Normal and Easy. Deterministic: object order, whole-number distances, no random draw",
                measured = measured,
                toggle = $"PlayerPrefs {ZGame.CpuGunManPref} = {(getInt(ZGame.CpuGunManPref, 1) == 1 ? "1 (on, default)" : "0 (off)")}; menu OpenZ/Deviations/CPU Mans Empty Guns; off in 'Zod exact' (oz_zod_exact = 1); held off in the parity rows and the counted checks (ZWorld.CpuGunManOff)",
                status = !pass ? DevCheck : on ? DevActive : DevActive + " (off by pref)",
            });
        }

        const string CpuGunManMenu = "OpenZ/Deviations/CPU Mans Empty Guns (oz_cpu_man_guns)";

        [MenuItem(CpuGunManMenu)]
        static void ToggleCpuGunMan()
        {
            bool on = PlayerPrefs.GetInt(ZGame.CpuGunManPref, 1) != 1;
            PlayerPrefs.SetInt(ZGame.CpuGunManPref, on ? 1 : 0); PlayerPrefs.Save();
            Debug.Log($"OpenZ computer mans empty guns (deviation DV157) from the next Play: {(on ? "ON: squads man the guns they pass and the ones at the frontier" : "OFF: the engine's computer")}");
        }

        [MenuItem(CpuGunManMenu, true)]
        static bool ToggleCpuGunManCheck() { Menu.SetChecked(CpuGunManMenu, PlayerPrefs.GetInt(ZGame.CpuGunManPref, 1) == 1); return true; }
    }
}
#endif
