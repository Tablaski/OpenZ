using UnityEngine;

namespace OpenZ
{
    public static class OzForceBarOption
    {
        public const string Label = "FORCE BAR", Pref = "oz_force_bar";
        public const string Tip = "FORCE BAR|UNIT COUNT: the existing unit-limit gauges. OFFENSIVE UNITS PRODUCTION VALUE: cumulative normal production time of offensive mobile units. Cranes, unarmed units, cannons and buildings are excluded; robot squads count once. The balance cursor compares both armies. Signed percentages use the smaller army as their reference. This display preference never changes production.|";
        internal const string DuelPref = "oz_duel_force_bar";
        static int cached = -1, duelCached = -1;
        public static bool MilitaryValue
        {
            get { if (cached < 0) cached = PlayerPrefs.GetInt(Pref, 0) == 1 ? 1 : 0; return cached == 1; }
            set { cached = value ? 1 : 0; PlayerPrefs.SetInt(Pref, cached); PlayerPrefs.Save(); }
        }
        public static string Name => MilitaryValue ? "OFFENSIVE UNITS PRODUCTION VALUE" : "UNIT COUNT";
        public static void Step(int direction) => MilitaryValue = !MilitaryValue;
        public static bool ForMatch(bool duel)
        {
            if (!duel) return MilitaryValue;
            if (duelCached < 0) duelCached = PlayerPrefs.GetInt(DuelPref, 1) == 0 ? 0 : 1;
            return duelCached == 1;
        }
        public static string MatchName(bool duel) => ForMatch(duel) ? "OFFENSIVE UNITS PRODUCTION VALUE" : "UNIT COUNT";
        public static void StepMatch(bool duel)
        {
            if (!duel) { Step(1); return; }
            duelCached = ForMatch(true) ? 0 : 1;
            PlayerPrefs.SetInt(DuelPref, duelCached);
            PlayerPrefs.Save();
        }
        public static void Reload() => cached = duelCached = -1;
    }
}
