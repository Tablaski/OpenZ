using UnityEngine;

namespace OpenZ
{
    /// <summary>Local presentation preferences, applied live in solo, the laboratory and multiplayer.</summary>
    public static class OzMapReadabilityOptions
    {
        public const string FlagLabel = "FLAG SIZE", BorderLabel = "DOTTED TERRITORY BORDERS";
        public const string RadarLabel = "MINIMAP RADARS", RadarPref = "oz_vk_minimap_radars";
        public const string RadarTip = "MINIMAP RADARS|With a Radar Loss Consequence selected, highlight visible radars and show a white sweep from active radars you own. Reduced motion keeps the outlines.|";
        public const string FlagPref = "oz_flag_size", BorderPref = "oz_territory_borders";
        public const string FlagTip = "FLAG SIZE|Display flags and their poles at x1 (original), x2 or x4. Applies immediately to territory and fort flags, including flags added in the Unit Editor. Capture distance, territory ownership and gameplay are unchanged.|";
        public const string BorderTip = "DOTTED TERRITORY BORDERS|Show bright round LED boundaries in each territory owner's colour, or grey for neutral territory, like Zod Engine's zone markers. The marks follow the ground and water, skip walls/buildings and recolour immediately after a capture. Choose OFF, x1, x2 or x4. Visual only; applies immediately and is saved.|";
        static int size = -1, borders = -1, radars = -1;
        public static bool Radars
        {
            get { if (radars < 0) radars = PlayerPrefs.GetInt(RadarPref, 1); return radars != 0; }
            set { radars = value ? 1 : 0; PlayerPrefs.SetInt(RadarPref, radars); PlayerPrefs.Save(); }
        }
        public static int FlagSize
        {
            get { if (size < 0) { int v = PlayerPrefs.GetInt(FlagPref, 1); size = v == 2 || v == 4 ? v : 1; } return size; }
            set { size = value == 2 || value == 4 ? value : 1; PlayerPrefs.SetInt(FlagPref, size); PlayerPrefs.Save(); }
        }
        public static string FlagName => "x" + FlagSize;
        public static void StepFlag(int direction)
        {
            int index = FlagSize == 1 ? 0 : FlagSize == 2 ? 1 : 2;
            index = (index + (direction < 0 ? 2 : 1)) % 3; FlagSize = index == 0 ? 1 : index == 1 ? 2 : 4;
        }
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
        public static void Reload() { size = borders = radars = -1; }
    }
}
