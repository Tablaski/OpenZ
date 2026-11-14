using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Options, GAME tab: VEHICLE SIZE, a cycle row 60 to 100 % in fives (saved as oz_vehicle_size, default 90 %, live). Only the drawing changes
    /// (View/OzVehicleScaleTest.cs), never the rules: footprint, speed, health, ranges and paths stay. The steps stop at both ends.
    /// </summary>
    public static class OzVehicleSizeOption
    {
        public const string Label = "VEHICLE SIZE", Pref = "oz_vehicle_size";
        public const string Tip = "VEHICLE SIZE|How big vehicles and their drivers are drawn, 60 to 100 %. Only the look changes: speed, health, range and paths stay the same. Default 90 %.|";
        public const int DefaultPercent = 90;

        /// <summary>The row's value.</summary>
        public static string Name => OzVehicleScaleTest.Percent + " %";
        /// <summary>The cycle row's step: -1 smaller, +1 bigger.</summary>
        public static void Step(int dir) => OzVehicleScaleTest.StepQuiet(dir);

        /// <summary>The saved size in percent (a value off the steps reads as the default).</summary>
        public static int Saved
        {
            get
            {
                int p = PlayerPrefs.GetInt(Pref, DefaultPercent);
                return p >= 60 && p <= 100 && p % 5 == 0 ? p : DefaultPercent;
            }
        }

        public static void Save(int percent) { PlayerPrefs.SetInt(Pref, percent); PlayerPrefs.Save(); }
    }
}
