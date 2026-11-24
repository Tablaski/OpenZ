using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    internal static class OzAiSoloChoice
    {
        internal const string Key = "oz_vk_solo_ai_2026";
        internal const string LastModernKey = "oz_vk_solo_ai_2026_last_level";

        internal static void SelectGeneration(bool modern)
        {
            if (ZAiDifficulty.IsModern(Level) == modern) return;
            var last = (ZBotDifficulty)PlayerPrefs.GetInt(LastModernKey, (int)ZBotDifficulty.Skilled);
            Level = modern ? (ZAiDifficulty.IsModern(last) ? last : ZBotDifficulty.Skilled) : ZBotDifficulty.Normal;
        }

        internal static ZBotDifficulty Level
        {
            get
            {
                var stored = (ZBotDifficulty)PlayerPrefs.GetInt(Key, 0);
                return ZAiDifficulty.IsModern(stored) ? stored : ZBotDifficulty.Normal;
            }
            set
            {
                var previous = Level;
                if (previous == value) return;
                if (ZAiDifficulty.IsModern(previous) && !ZAiDifficulty.IsModern(value))
                    PlayerPrefs.SetInt(LastModernKey, (int)previous);
                PlayerPrefs.SetInt(Key, ZAiDifficulty.IsModern(value) ? (int)value : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
