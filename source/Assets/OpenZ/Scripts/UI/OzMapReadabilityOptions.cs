using System;
using System.Globalization;
using UnityEngine;

namespace OpenZ
{
    /// <summary>Local presentation preferences, applied live in solo, the laboratory and multiplayer.</summary>
    public static class OzMapReadabilityOptions
    {
        public const string FlagLabel = "FLAG SIZE", BorderLabel = "DOTTED TERRITORY BORDERS";
        public const string RadarPref = "oz_vk_minimap_radars";
        public const string RadarSweepLabel = "MINIMAP RADARS SWEEP", RadarSweepPref = "oz_vk_minimap_radar_interval", LegacyRadarSweepPref = "oz_vk_radar_sweep_seconds";
        public const string RadarSweepTip = RadarSweepLabel + "|Choose OFF, 1.5 s, 3 s or 5 s between sweeps from your active radars. OFF hides radar highlights and sweeps. Reduced motion keeps the radar outlines.|";
        public const string FlagPref = "oz_vk_flag_scale", LegacyFlagPref = "oz_flag_size", BorderPref = "oz_territory_borders";
        public const string LastBorderSizePref = "oz_vk_last_territory_border_size";
        public const string FlagTip = "FLAG SIZE|Choose 1x (original), 1.5x, 2x, 2.5x or 3x for flags and poles. Applies immediately to territory and fort flags, including flags added in the Unit Editor. Capture distance, territory ownership and gameplay are unchanged.|";
        public const string BorderTip = "DOTTED TERRITORY BORDERS|Show outlined dots in each territory owner's colour, with dark grey on Arctic and white on City for neutral territory. The marks follow the ground and water, skip walls and buildings, and recolour after a capture. Choose OFF, x1, x2 or x4.|";
        internal static readonly float[] RadarSweepIntervals = { 0f, 1.5f, 3f, 5f };
        internal static readonly float[] FlagSizes = { 1f, 1.5f, 2f, 2.5f, 3f };
        static float flagSize, radarSweepSeconds = -1f;
        static int borders = -1;
        public static bool Radars
        {
            get => RadarSweepSeconds > 0f;
            set => RadarSweepSeconds = value ? (RadarSweepSeconds > 0f ? RadarSweepSeconds : 3f) : 0f;
        }
        public static float RadarSweepSeconds
        {
            get
            {
                if (radarSweepSeconds < 0f)
                {
                    float saved = PlayerPrefs.GetFloat(RadarSweepPref, PlayerPrefs.GetInt(LegacyRadarSweepPref, 3));
                    radarSweepSeconds = PlayerPrefs.GetInt(RadarPref, 1) == 0 ? 0f : saved == 10f ? 5f :
                        Array.IndexOf(RadarSweepIntervals, saved) >= 0 ? saved : 3f;
                }
                return radarSweepSeconds;
            }
            set
            {
                if (Array.IndexOf(RadarSweepIntervals, value) < 0) return;
                radarSweepSeconds = value;
                PlayerPrefs.SetFloat(RadarSweepPref, value);
                PlayerPrefs.SetInt(RadarPref, value > 0f ? 1 : 0);
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
        public static string FlagName => FormatChoice(FlagSize) + "x";
        public static string RadarSweepName => RadarSweepSeconds == 0f ? "OFF" : FormatChoice(RadarSweepSeconds) + " s";
        public static void StepFlag(int direction)
        {
            int index = Array.IndexOf(FlagSizes, FlagSize);
            FlagSize = FlagSizes[(index + (direction < 0 ? FlagSizes.Length - 1 : 1)) % FlagSizes.Length];
        }
        public static void StepRadarSweep(int direction)
        {
            int index = Array.IndexOf(RadarSweepIntervals, RadarSweepSeconds);
            RadarSweepSeconds = RadarSweepIntervals[(index + (direction < 0 ? RadarSweepIntervals.Length - 1 : 1)) % RadarSweepIntervals.Length];
        }
        public static int BorderSize
        {
            get { if (borders < 0) { int v = PlayerPrefs.GetInt(BorderPref, 1); borders = v == 1 || v == 2 || v == 4 ? v : 0; } return borders; }
            set
            {
                borders = ValidBorderSize(value) ? value : 0;
                if (borders > 0) PlayerPrefs.SetInt(LastBorderSizePref, borders);
                else if (ValidBorderSize(PlayerPrefs.GetInt(BorderPref, 1)))
                    PlayerPrefs.SetInt(LastBorderSizePref, PlayerPrefs.GetInt(BorderPref, 1));
                PlayerPrefs.SetInt(BorderPref, borders);
                PlayerPrefs.Save();
            }
        }
        static bool ValidBorderSize(int size) => size == 1 || size == 2 || size == 4;
        public static int VisibleBorderSize
        {
            get
            {
                if (BorderSize > 0) return BorderSize;
                int saved = PlayerPrefs.GetInt(LastBorderSizePref, 1);
                return ValidBorderSize(saved) ? saved : 1;
            }
        }
        public static string BorderName => BorderSize == 0 ? "OFF" : "x" + BorderSize;
        public static void StepBorder(int direction)
        {
            int index = BorderSize == 0 ? 0 : BorderSize == 1 ? 1 : BorderSize == 2 ? 2 : 3;
            int[] sizes = { 0, 1, 2, 4 }; BorderSize = sizes[(index + (direction < 0 ? 3 : 1)) % 4];
        }
        public static bool Borders { get => BorderSize > 0; set => BorderSize = value ? VisibleBorderSize : 0; }
        public static void Reload() { flagSize = 0f; radarSweepSeconds = -1f; borders = -1; }
    }
}
