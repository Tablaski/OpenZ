using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The OPTIONS row HIGH GROUND SHOOTS FARTHER (ADVANCED tab, right under LAVA BURNS ROBOTS: the label is not in OzSettingTips.TabRows, so every existing
    /// row keeps its place; in the main menu and in the pause menu): the saved setting of the rule DV162 (Sim/ZObject.HighGround.cs). ON by default, saved
    /// in oz_high_ground at once, read by ZGame at every level start (never on in the engine-exact mode), so both sides of a match keep one rule from start to end.
    /// </summary>
    public static class OzHighGroundOption
    {
        public const string Pref = "oz_high_ground";
        public const string Label = "HIGH GROUND SHOOTS FARTHER";
        public const string Tip = "HIGH GROUND SHOOTS FARTHER|On (default): fort tower guns, and anything standing on top of a platform in the Unit editor, shoot 10 % farther at targets below them. Off: height gives no extra range, as in the original game. From the next match.|";

        public static bool On
        {
            get => PlayerPrefs.GetInt(Pref, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(Pref, value ? 1 : 0); PlayerPrefs.Save();
                Debug.Log($"[OpenZ Options] high ground shoots farther: {(value ? "ON" : "OFF")} ({Pref} {(value ? 1 : 0)}, saved, read at the next level start)");
            }
        }
    }
}
