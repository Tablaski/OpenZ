using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The OPTIONS row LAVA BURNS ROBOTS (ADVANCED tab, its last row: the label is not in OzSettingTips.TabRows, so every existing row keeps
    /// its place; in the main menu and in the pause menu): the saved setting
    /// of the rule DV152 (Sim/ZWorld.Lava.cs). ON by default, saved in oz_lava_burns at once, read by ZGame at every level start (never on
    /// in the engine-exact mode). It only matters on a map with lava (level 7). ON: a robot you send onto lava walks in, burns, glows redder
    /// and melts if it stays. OFF: lava is a wall for everybody, as in the original game. A change during a match waits for the next level
    /// start, so no robot is ever left standing in lava with the rule switched off under it.
    /// </summary>
    public static class OzLavaOption
    {
        public const string Pref = "oz_lava_burns";
        public const string Label = "LAVA BURNS ROBOTS";
        public const string Tip = "LAVA BURNS ROBOTS|On (default): on the volcano level you can send a robot onto the lava. It walks in, gets hotter and redder every second and melts if it stays too long. Vehicles and the enemy never go in. Off: lava blocks everybody, as in the original game.|";

        public static bool On
        {
            get => PlayerPrefs.GetInt(Pref, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(Pref, value ? 1 : 0); PlayerPrefs.Save();
                Debug.Log($"[OpenZ Options] lava burns robots: {(value ? "ON" : "OFF")} ({Pref} {(value ? 1 : 0)}, saved, read at the next level start)");
            }
        }
    }
}
