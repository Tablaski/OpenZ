using UnityEngine;

namespace OpenZ
{
    public static class OzForceBarOption
    {
        public const string Label = "FORCE BAR", Pref = "oz_force_bar";
        public const string Tip = "FORCE BAR|UNIT COUNT: the existing unit-limit gauges. MILITARY VALUE: cumulative normal production time of mobile units; cannons and buildings are excluded, robot squads count once. The balance cursor compares both armies. Signed percentages use the smaller army as their reference. This display preference never changes production.|";
        static int cached = -1;
        public static bool MilitaryValue
        {
            get { if (cached < 0) cached = PlayerPrefs.GetInt(Pref, 0) == 1 ? 1 : 0; return cached == 1; }
            set { cached = value ? 1 : 0; PlayerPrefs.SetInt(Pref, cached); PlayerPrefs.Save(); }
        }
        public static string Name => MilitaryValue ? "MILITARY VALUE" : "UNIT COUNT";
        public static void Step(int direction) => MilitaryValue = !MilitaryValue;
        public static void Reload() => cached = -1;
    }
}
