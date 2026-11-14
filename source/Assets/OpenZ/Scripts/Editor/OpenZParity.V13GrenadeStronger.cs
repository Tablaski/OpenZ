#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV160 "thrown grenades x1.15 more on top of DV104" (1.3 owner item 32; the rule: Sim/ZWorld.GrenadeBoost.cs
    /// GrenadeStrongerPct / GrenadeStronger). Measured here: parts 1 and 2 of the counted check (the rules, a real throw, every landing point;
    /// seconds). The fights against tanks, robots and a tank push are the menu OpenZ/V1.3/Grenades stronger: counted (headless, DV160)
    /// (Editor/OzV13GrenadeCount.cs; minutes, so not run inside the parity report).</summary>
    public static partial class OpenZParity
    {
        static void AddGrenadeStrongerDeviation(List<Deviation> list, Func<string, int, int> getInt)
        {
            string measured; bool pass;
            try { measured = OzV11Kit.VerdictCell(OzV13GrenadeCount.Run(false), out pass); }
            catch (Exception ex) { measured = "the check threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            var s = ZSettings.Parse(SettingsText);
            int zd = (int)(s.grenade_damage * ZConst.MAX_UNIT_HEALTH);
            int d104 = (zd * ZWorld.GrenadeBoostDamagePct + 50) / 100, a104 = (zd * ZWorld.GrenadeAreaDamagePct + 50) / 100;
            bool exact = getInt("oz_zod_exact", 0) != 0, boost = getInt("oz_grenade_boost", 1) == 1, on = !exact && boost;
            list.Add(new Deviation
            {
                id = "DV160", change = $"Thrown grenades {ZWorld.ShippedGrenadeStrongerPct - 100} % stronger again (1.3): DV104's direct and area hit x {ZWorld.ShippedGrenadeStrongerPct / 100.0:0.00}",
                zod = $"a thrown grenade takes grenade_damage x MAX_UNIT_HEALTH = {zd} HP at the centre of its blast, falling in a straight line to nothing at {s.grenade_damage_radius} px " +
                      "(zsettings.cpp:269-272, zobject.cpp:2126-2157, zserver.cpp:1076-1098); Z-Wiki gives no grenade numbers (only: 20 a box, shared by the squad, Toughs cannot use them, " +
                      "'more powerful than their standard weapons' against vehicles and guns)",
                openz = $"with DV104 on, a direct hit {d104} -> {ZWorld.GrenadeStronger(d104)} HP and an area hit {a104} -> {ZWorld.GrenadeStronger(a104)} HP at the centre of the blast " +
                        $"(x {ZWorld.GrenadeStrongerPct} %, rounded half up; ZWorld.ThrownGrenadeDamage / ThrownGrenadeAreaDamage: a squad's grenade, the fort's roof defender's, and the heroes' x 1.20 / x 1.15 on top of it). " +
                        "Unchanged: the 36 px radius and its straight-line fall-off, the throw range, the +-24 px scatter, the 40 px/s flight, the 2.254 s between throws, 20 a box, " +
                        "who throws at what (DV40), the step aside of the unit thrown at, the size of the blast picture by this rule (View/OzGrenadeBlast.cs reads the radius only; the 1.3 look change of item 32 draws it x 1.30, OzGrenadeBlast.Bigger), a burst crate's blasts and every shell and rocket. " +
                        "No landing point does less than Zod's grenade. Deterministic: whole numbers, no random draw",
                measured = "OpenZ/V1.3/Grenades stronger, parts 1-2: " + measured + " (the fights: menu OpenZ/V1.3/Grenades stronger: counted (headless, DV160))",
                toggle = $"acts only while DV104 is on: PlayerPrefs oz_grenade_boost = {(boost ? "1 (on, default)" : "0 (off)")}, off in 'Zod exact' (oz_zod_exact = {(exact ? "1" : "0")}); " +
                         $"the step is ZWorld.ShippedGrenadeStrongerPct = {ZWorld.ShippedGrenadeStrongerPct} (100 = the 1.2 grenade)",
                status = !pass ? DevCheck : on ? DevActive : DevActive + " (off by pref)",
            });
        }
    }
}
#endif
