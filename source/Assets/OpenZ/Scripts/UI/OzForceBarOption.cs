using UnityEngine;

namespace OpenZ
{
    public static class OzForceBarOption
    {
        internal enum Mode { Units, AttackingPower, Both }

        public const string Label = "FORCE BAR", Pref = "oz_force_bar";
        public const string Tip = "FORCE BAR|UNITS: unit counts and the unit limit. ATTACKING POWER: cumulative normal production time of offensive mobile units. Robot squads count once. The percentage compares attacking power using the smaller army as the reference. BOTH shows the two measures together.|";
        internal const string DuelPref = "oz_duel_force_bar";
        static Mode? soloMode, duelMode;

        internal static Mode ForMatch(bool duel) => duel
            ? (duelMode ??= ReadMode(DuelPref, Mode.AttackingPower))
            : (soloMode ??= ReadMode(Pref, Mode.Units));

        static Mode ReadMode(string key, Mode fallback)
        {
            int value = PlayerPrefs.GetInt(key, (int)fallback);
            return value >= (int)Mode.Units && value <= (int)Mode.Both ? (Mode)value : fallback;
        }

        public static string Name => MatchName(false);
        public static string MatchName(bool duel) => ForMatch(duel) switch
        {
            Mode.AttackingPower => "ATTACKING POWER",
            Mode.Both => "BOTH",
            _ => "UNITS"
        };

        public static void Step(int direction) => StepMatch(false, direction);

        public static void StepMatch(bool duel, int direction)
        {
            int step = direction < 0 ? 2 : 1;
            var next = (Mode)(((int)ForMatch(duel) + step) % 3);
            if (duel) duelMode = next;
            else soloMode = next;
            PlayerPrefs.SetInt(duel ? DuelPref : Pref, (int)next);
            PlayerPrefs.Save();
        }

        public static void Reload() => soloMode = duelMode = null;
    }
}
