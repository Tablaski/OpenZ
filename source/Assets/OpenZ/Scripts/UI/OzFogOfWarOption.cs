using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4: the OPTIONS row FOG OF WAR (ADVANCED tab, right under HIGH GROUND SHOOTS FARTHER: the label is not in OzSettingTips.TabRows,
    /// so every existing row keeps its place; in the main menu and in the pause menu). OFF by default, as every match was before. Saved in
    /// oz_fog_of_war at once and read by View/OzFogOfWar.cs at every level start, so a match keeps one setting from start to end. Never on in
    /// the Unit editor, in the engine-exact mode or in a match without a human side. View only: the rules and the computer never read it,
    /// so the same fight gives the same result with the fog on or off.
    /// </summary>
    public static class OzFogOfWarOption
    {
        public const string Pref = "oz_fog_of_war";
        public const string Label = "FOG OF WAR";
        /// <summary>The value of a fresh install (no saved key): OFF, the game as before.</summary>
        public const bool DefaultOn = false;
        /// <summary>
        /// The OPTIONS rows FOG OF WAR
        /// and FOG LOOK are not built (main menu and pause menu) and their tips are not registered; the fog of war is on exactly in a MAYHEM
        /// match (View/OzFogOfWar.cs WhyOff), with the recommended look. oz_fog_of_war and oz_fog_look stay readable (this class and
        /// OzFogLookOption are kept) but the game no longer reads them. The testing panel (Alt+P) still switches the fog for a match.</summary>
        public static bool InOptions => false;
        public const string Tip = "FOG OF WAR|Off (default): the whole battlefield is in view, as in the original game. On: the map starts under drifting clouds; whatever your units and buildings have seen stays open for the rest of the match. Enemy units in ground you have not explored yet are hidden. The computer plays exactly as before. Never in the Unit editor. From the next match.|";

        public static bool On
        {
            get => PlayerPrefs.GetInt(Pref, DefaultOn ? 1 : 0) == 1;
            set
            {
                PlayerPrefs.SetInt(Pref, value ? 1 : 0); PlayerPrefs.Save();
                Debug.Log($"[OpenZ Options] fog of war: {(value ? "ON" : "OFF")} ({Pref} {(value ? 1 : 0)}, saved, read at the next level start)");
            }
        }
    }
}
