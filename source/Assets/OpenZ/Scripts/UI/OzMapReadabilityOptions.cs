using System;
using System.Globalization;
using UnityEngine;

namespace OpenZ
{
    /// <summary>Local presentation preferences, applied live in solo, the laboratory and multiplayer.</summary>
    public static class OzMapReadabilityOptions
    {
        public const string FlagLabel = "FLAG SIZE", BorderLabel = "DOTTED TERRITORY BORDERS";
        public const string RadarLabel = "MINIMAP RADARS", RadarPref = "oz_vk_minimap_radars";
        public const string RadarTip = "MINIMAP RADARS|Highlight visible radars and show a white sweep from active radars you own. Reduced motion keeps the radar outlines.|";
        public const string RadarSweepLabel = "MINIMAP RADAR SWEEP INTERVAL", RadarSweepPref = "oz_vk_minimap_radar_interval", LegacyRadarSweepPref = "oz_vk_radar_sweep_seconds";
        public const string RadarSweepTip = RadarSweepLabel + "|Seconds between sweeps from your active radars when Minimap Radars is on. Reduced motion keeps the radar outlines.|";
        public const string FlagPref = "oz_vk_flag_scale", LegacyFlagPref = "oz_flag_size", BorderPref = "oz_territory_borders";
        public const string FlagTip = "FLAG SIZE|Choose 1x (original), 1.5x, 2x, 2.5x or 3x for flags and poles. Applies immediately to territory and fort flags, including flags added in the Unit Editor. Capture distance, territory ownership and gameplay are unchanged.|";
        public const string BorderTip = "DOTTED TERRITORY BORDERS|Show outlined dots in each territory owner's colour, with dark grey on Arctic and white on City for neutral territory. The marks follow the ground and water, skip walls and buildings, and recolour after a capture. Choose OFF, x1, x2 or x4.|";
        internal static readonly float[] RadarSweepIntervals = { 1.5f, 3f, 5f, 10f };
        internal static readonly float[] FlagSizes = { 1f, 1.5f, 2f, 2.5f, 3f };
        static float flagSize, radarSweepSeconds;
        static int borders = -1, radars = -1;
        public static bool Radars
        {
            get { if (radars < 0) radars = PlayerPrefs.GetInt(RadarPref, 1); return radars != 0; }
            set { radars = value ? 1 : 0; PlayerPrefs.SetInt(RadarPref, radars); PlayerPrefs.Save(); }
        }
        public static float RadarSweepSeconds
        {
            get
            {
                if (radarSweepSeconds == 0f)
                {
                    float saved = PlayerPrefs.GetFloat(RadarSweepPref, PlayerPrefs.GetInt(LegacyRadarSweepPref, 3));
                    radarSweepSeconds = Array.IndexOf(RadarSweepIntervals, saved) >= 0 ? saved : 3f;
                }
                return radarSweepSeconds;
            }
            set
            {
                if (Array.IndexOf(RadarSweepIntervals, value) < 0) return;
                radarSweepSeconds = value;
                PlayerPrefs.SetFloat(RadarSweepPref, value);
                PlayerPrefs.Save();
            }
        }
        public static float FlagSize
        {
            get
            {
                if (flagSize == 0f)
                {
                    int previous = PlayerPrefs.GetInt(LegacyFlagPref, 1);
                    float saved = PlayerPrefs.GetFloat(FlagPref, previous == 4 ? 3f : previous);
                    flagSize = Array.IndexOf(FlagSizes, saved) >= 0 ? saved : 1f;
                }
                return flagSize;
            }
            set
            {
                if (Array.IndexOf(FlagSizes, value) < 0) return;
                flagSize = value;
                PlayerPrefs.SetFloat(FlagPref, value);
                PlayerPrefs.Save();
            }
        }
        internal static string FormatChoice(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
        public static int BorderSize
        {
            get { if (borders < 0) { int v = PlayerPrefs.GetInt(BorderPref, 1); borders = v == 1 || v == 2 || v == 4 ? v : 0; } return borders; }
            set { borders = value == 1 || value == 2 || value == 4 ? value : 0; PlayerPrefs.SetInt(BorderPref, borders); PlayerPrefs.Save(); }
        }
        public static string BorderName => BorderSize == 0 ? "OFF" : "x" + BorderSize;
        public static void StepBorder(int direction)
        {
            int index = BorderSize == 0 ? 0 : BorderSize == 1 ? 1 : BorderSize == 2 ? 2 : 3;
            int[] sizes = { 0, 1, 2, 4 }; BorderSize = sizes[(index + (direction < 0 ? 3 : 1)) % 4];
        }
        public static bool Borders { get => BorderSize > 0; set => BorderSize = value ? 1 : 0; }
        public static void Reload() { flagSize = radarSweepSeconds = 0f; borders = radars = -1; }
    }
}
