#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV142 "CUSTOM UNIT STATS in the levels" (1.2 item 60; the rule: Sim/ZWorld.CustomStats.cs, the level start:
    /// View/OzLevelStats.cs and View/OzLevelStatsPlan.cs, the selector: UI/OpenZMainMenu.UnitStats.cs). Measured headless by the kit's
    /// counted check (Editor/OzV12CustomStatsCount.cs) in its quick form: the profile reading (good, broken, old and partial texts), the
    /// Unit editor's own plan, level 2 OPENZ / CUSTOM / PLAY AS BLUE + CUSTOM at the start, the units built later, the same match twice,
    /// OPENZ tick for tick as a start without the level start, every unit kind; the switch and the rule values are put back afterwards
    /// (the whole check on levels 2, 16 and 17: menu OpenZ/V1.2/Unit Stats: 60 Custom Stats Check (headless)).</summary>
    public static partial class OpenZParity
    {
        static void AddCustomStatsDeviation(List<Deviation> list, Func<string, int, int> getInt)
        {
            string measured; bool pass;
            try
            {
                measured = OzV11Kit.VerdictCell(OzV12CustomStatsCount.Check(SettingsText, MapText, true), out pass)
                           + " (the whole check on levels 2, 16 and 17: menu OpenZ/V1.2/Unit Stats: 60 Custom Stats Check (headless))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            finally { OzLevelStatsPlan.End(null); }
            bool on = getInt("oz_zod_exact", 0) == 0;
            list.Add(new Deviation
            {
                id = "DV142",
                change = "CUSTOM UNIT STATS: a level can be played with the unit stats the player saved in the Unit editor (its Custom set), for both teams: a UNIT STATS selector OPENZ | CUSTOM "
                         + "on the battlefield screen, shown only while a Custom profile with a change exists, OPENZ by default",
                zod = "every unit's numbers come from the one settings file of the game (default_settings.txt, zsettings.cpp); there is no other set",
                openz = "the player's DEPLOY with CUSTOM chosen arms it; before the level's world is built the Unit editor's saved Custom profile is read and checked value by value (unknown units "
                        + "and values, fixed values and non-numbers left out, numbers kept inside the Unit editor's limits, OpenZ's own number for every value not given, said in the log) and "
                        + "turned into the Unit editor's own plan (OzUeStatsApply: the settings parsed afresh with the Custom values written in, the rule values, the shuttle's values); "
                        + "the changed rows are copied into the world's own rows before any object is made, so every unit of every team (the player's, the CPU's, neutral vehicles and guns, "
                        + "the map's squads, the head start, the drivers, ALAN & BRAD by the hero rule, every unit built later, PLAY AS BLUE too) has the Custom numbers; the shuttle gets the "
                        + "set's own hit points and speed; the sniper balance, the bolt cycle and the rifle pairs are the set's while the level runs and the game's again after it; "
                        + "a Custom match is not entered in the high scores. OPENZ (and a text that cannot be used): nothing changes, tick for tick",
                measured = measured,
                toggle = $"ZWorld.CustomStatsDeviation: the game sets it at every start, on only for a level the player deployed with CUSTOM chosen (oz_unit_stats = 1, the Unit editor's saved profile oz_ue_profile), "
                         + $"off in 'Zod exact' (oz_zod_exact = {(on ? "0" : "1")}), never in a bot match, the Unit editor or a level opened without DEPLOY; held off in the other rows' probes and the counted checks",
                status = pass ? DevActive : DevCheck,
            });
        }
    }
}
#endif
