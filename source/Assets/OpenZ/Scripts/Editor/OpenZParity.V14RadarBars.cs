#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV172 "enemy health bars need a radar" (version 1.4, view only: View/OzRadarBars.cs, View/OzRadarBarsRule.cs;
    /// drawn by UI/OpenZUnitOverlay.cs and UI/OpenZHud.RadarBars.cs). Measured headless on level 2 (Editor/OzV14RadarBarsCount.cs Quick); the
    /// whole check: menu OpenZ/V1.4/Radar Enemy Bars: Count (headless, DV172). No Sim state: the rules, the computer and bot matches are as before.</summary>
    public static partial class OpenZParity
    {
        /// <summary>The row's number (the next free one after the 1.4 kits gatling_buff DV170 and vehicle_exit DV171; the kit's apply.py --dv
        /// writes another here and in every file of the kit when it was taken meanwhile).</summary>
        const string RadarBarsDvId = "DV172";

        static void AddRadarBarsDeviation(List<Deviation> list, Func<string, int, int> getInt)
        {
            string measured; bool pass;
            try { measured = OzV11Kit.VerdictCell(OzV14RadarBarsCount.Quick(SettingsText, OzV11Kit.MapText("L02")), out pass) + " (the whole check: menu OpenZ/V1.4/Radar Enemy Bars: Count (headless, " + RadarBarsDvId + "))"; }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            bool pref = getInt("oz_radar_dots", 1) == 1;
            list.Add(new Deviation
            {
                id = RadarBarsDvId, change = "Enemy health bars show only while the player owns a standing radar (view only)",
                zod = "an enemy's name and health bar show on mouse-over for any team, with no radar condition (zplayer.cpp:2178-2189 RenderHover; zobject.cpp:555-608, "
                      + "RenderHealth 611-700); the radar only plays its sounds (oflag.cpp:111-124, zserver.cpp:2053-2057, zplayer.cpp:1146-1188)",
                openz = "while the player's side owns no standing radar (a building of type radar, not a ruin), the health bars of enemy robots, vehicles, guns and shuttles are "
                        + "not drawn: not on mouse-over, not on a spread squad's members, not with HEALTH BARS ON EVERY DAMAGED UNIT (no mark at all there); the unit panel would read "
                        + "HEALTH · NEEDS RADAR. The player's own units and the neutral ones (empty vehicles and guns, neutral buildings) keep their bars. The radar truth is the one of "
                        + "the minimap's enemy dots (DV33) and the strike-group warning (OzStrikeWarning.OwnsRadar), once per frame; a radar gained or lost counts once it held 0.6 s "
                        + "of game time, then the enemy bars fade in / out over 0.35 s (smootherstep, unscaled "
                        + "time). Brackets, name chips, the grenade icon, the veteran star and the unit facts are unchanged. "
                        + "No sim state: the rules and the computer never read it",
                measured = measured,
                toggle = $"PlayerPrefs oz_radar_dots = {(pref ? "1 (on, default)" : "0 (off: every unit and every bar shown)")} (DV33's switch); off in the Unit editor, in 'Zod exact' "
                         + "(oz_zod_exact = 1, read once per match) and in a match without a human side",
                status = !pass ? DevCheck : pref ? DevActive : DevActive + " (off by pref)",
            });
        }
    }
}
#endif
