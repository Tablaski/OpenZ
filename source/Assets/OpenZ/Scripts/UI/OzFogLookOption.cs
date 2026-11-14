using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4: the OPTIONS row FOG LOOK (ADVANCED tab, right under FOG OF WAR; like it, the label is not in OzSettingTips.TabRows, so
    /// every existing row keeps its place; in the main menu and in the pause menu). CLOUD SEA (default), CLOUD BANKS or MIST LAYERS: how the
    /// fog of war's clouds look (View/OzFogOfWar.Looks.cs). Each planet has its own colours for every style, chosen by the level. Saved in
    /// oz_fog_look at once, read at every level start, and live: a change in the pause menu shows on the next frame (only the cloud's
    /// numbers change). View only.
    /// </summary>
    public static class OzFogLookOption
    {
        public const string Pref = "oz_fog_look";
        public const string Label = "FOG LOOK";
        /// <summary>The value of a fresh install: CLOUD SEA.</summary>
        public const int Default = 0;
        public static readonly string[] Names = { "CLOUD SEA", "CLOUD BANKS", "MIST LAYERS" };
        public const string Tip = "FOG LOOK|How the fog of war looks. CLOUD SEA (default): a deep rolling sea of clouds. CLOUD BANKS: big soft cloud heaps that cast shadows. MIST LAYERS: low mist with clouds racing above. Every planet has its own colours: warm dust over the desert, damp mist in the jungle, storm clouds over the city, ash and fire light over the volcano.|";

        public static int Index
        {
            get { int v = PlayerPrefs.GetInt(Pref, Default); return v >= 0 && v < Names.Length ? v : Default; }
            set
            {
                int v = ((value % Names.Length) + Names.Length) % Names.Length;
                PlayerPrefs.SetInt(Pref, v); PlayerPrefs.Save();
                Debug.Log($"[OpenZ Options] fog look: {Names[v]} ({Pref} {v}, saved)");
            }
        }

        public static string Name => Names[Index];

        /// <summary>The next (dir &gt; 0) or previous style; a running match shows it at once.</summary>
        public static void Step(int dir)
        {
            Index = Index + (dir < 0 ? -1 : 1);
            OzFogOfWar.LookFromOptions(Index);                                                       // shown at once in a running match; the testing panel's session style is dropped
        }
    }
}
