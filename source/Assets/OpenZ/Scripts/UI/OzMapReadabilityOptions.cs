using UnityEngine;

namespace OpenZ
{
    /// <summary>Local presentation preferences, applied live in solo, the laboratory and multiplayer.</summary>
    public static class OzMapReadabilityOptions
    {
        public const string FlagLabel = "FLAG SIZE", BorderLabel = "DOTTED TERRITORY BORDERS";
        public const string FlagPref = "oz_flag_size", BorderPref = "oz_territory_borders";
        public const string FlagTip = "FLAG SIZE|Display flags and their poles at x1 (original), x2 or x4. Applies immediately to territory and fort flags, including flags added in the Unit Editor. Capture distance, territory ownership and gameplay are unchanged.|";
        public const string BorderTip = "DOTTED TERRITORY BORDERS|Show subtle dotted boundaries in each territory owner's colour, or grey for neutral territory, like Zod Engine's zone markers. The marks follow the ground and water, skip walls/buildings and recolour immediately after a capture. Visual only; applies immediately and is saved.|";
        static int size = -1, borders = -1;
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
        public static bool Borders
        {
            get { if (borders < 0) borders = PlayerPrefs.GetInt(BorderPref, 0) == 1 ? 1 : 0; return borders == 1; }
            set { borders = value ? 1 : 0; PlayerPrefs.SetInt(BorderPref, borders); PlayerPrefs.Save(); }
        }
        public static void Reload() { size = borders = -1; }
    }
}
