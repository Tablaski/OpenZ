#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV164 "the heavy tank clears the rock it clips" (1.3; the rule: Sim/ZObject.HeavyRock.cs), measured headless by the short form of the counted check
    /// (Editor/OzV13HeavyRockCount.cs, every line but the real levels, which the menu runs).</summary>
    public static partial class OpenZParity
    {
        static void AddHeavyRockDeviation(List<Deviation> list)
        {
            try { list.Add(DevHeavyRock()); }
            catch (Exception ex) { list.Add(new Deviation { id = "DV164", change = "DevHeavyRock", zod = "-", openz = "-", measured = "probe threw " + ex.GetType().Name + ": " + ex.Message, toggle = "-", status = DevCheck }); }
        }

        static Deviation DevHeavyRock()
        {
            string report, measured; bool pass;
            string Level(string lv) { var a = UnityEngine.Resources.Load<UnityEngine.TextAsset>("OpenZ/Maps/" + lv + "_grid"); return a != null ? a.text : null; }
            using (OzV11Kit.GameSwitches()) report = OzV13HeavyRockCount.Check(SettingsText, Level, false);
            measured = OzV11Kit.VerdictCell(report, out pass);
            return new Deviation
            {
                id = "DV164", change = "A heavy tank standing idle clears a rock it clips: it turns its gun and shoots that rock until it is gone, then stands idle again",
                zod = "a rock is blasted only when a path runs into it (zobject.cpp:3716-3747); a unit standing by one leaves it alone",
                openz = "Sim/ZObject.HeavyRock.cs: a crewed heavy tank, no other unit, after " + ZWorld.HeavyRockIdleSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s standing idle with no enemy in its agro reach, " +
                        "at a rock whose foot lies within the reach of its barrels from the turret ring (they sweep all round when idle) + the rock's own radius, whole pixels, at the drawn vehicle size. " +
                        "It never moves. An order, a target, an agro win at once. The shot, its sound, the rock's damage and break are the game's; the shell is aimed at the rock's foot and hurts that rock alone. Both teams and the computer. No random draw of its own",
                measured = measured,
                toggle = "ZWorld.HeavyRockDeviation: on in a game, off in 'Zod exact' (View/ZGame.cs); off by default headless, in the parity rows and in the counted checks",
                status = pass ? DevActive : DevCheck,
            };
        }
    }
}
#endif
