using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    internal static class OzAiSoloChoice
    {
        internal const string Key = "oz_vk_solo_ai_2026";

        internal static ZBotDifficulty Level
        {
            get
            {
                var stored = (ZBotDifficulty)PlayerPrefs.GetInt(Key, 0);
                return ZAiDifficulty.IsModern(stored) ? stored : ZBotDifficulty.Normal;
            }
            set
            {
                if (Level == value) return;
                PlayerPrefs.SetInt(Key, ZAiDifficulty.IsModern(value) ? (int)value : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
