using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Options, GAME tab: ROBOT SIZE, a cycle row 60 to 100 % in fives, right under VEHICLE SIZE (saved as oz_robot_size, default 100 % =
    /// the robots exactly as they were, live). The same steps and the same clicks as VEHICLE SIZE. Only the drawing changes
    /// (View/OzRobotScale.cs), never the rules: footprint, speed, health, ranges, hit chances and paths stay. The steps stop at both ends.
    /// </summary>
    public static class OzRobotSizeOption
    {
        public const string Label = "ROBOT SIZE", Pref = "oz_robot_size";
        public const string Tip = "ROBOT SIZE|How big robots are drawn, 60 to 100 %. Only the look changes: speed, health, range and paths stay the same. Default 100 %.|";
        public const int DefaultPercent = 100;

        /// <summary>The row's value.</summary>
        public static string Name => OzRobotScale.Percent + " %";
        /// <summary>The cycle row's step: -1 smaller, +1 bigger.</summary>
        public static void Step(int dir) => OzRobotScale.StepQuiet(dir);

        /// <summary>The saved size in percent (a value off the steps reads as the default).</summary>
        public static int Saved => Valid(PlayerPrefs.GetInt(Pref, DefaultPercent));

        /// <summary>A saved value as the row takes it: 60 to 100 in fives, anything else the default.</summary>
        public static int Valid(int p) => p >= 60 && p <= 100 && p % 5 == 0 ? p : DefaultPercent;

        public static void Save(int percent) { PlayerPrefs.SetInt(Pref, percent); PlayerPrefs.Save(); }
    }
}
