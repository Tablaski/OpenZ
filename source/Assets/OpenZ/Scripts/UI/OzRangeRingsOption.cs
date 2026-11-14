using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Options, GAME tab, under HEALTH BARS ON EVERY DAMAGED UNIT: RANGE RINGS ON SELECTED UNITS, a cycle row FADE / ALWAYS
    /// (saved as oz_range_rings, default FADE = 0, live, both menus).
    ///  * FADE (default): the rings of a new selection burst out of the units, hold and fade, exactly as before (View/AttackRangeRings.cs: 0.38 s
    ///    burst, 1.5 s hold, 0.55 s fade, about 2.4 s in all).
    ///  * ALWAYS: the rings of your selected units burst out as before and then stay at full size and strength while the units are selected; they
    ///    fade in 0.2 s when the units leave the selection, as before (View/AttackRangeRings.KeepOption.cs). Switching back to FADE lets the rings
    ///    still shown fade out in the normal 0.55 s.
    /// Only the drawing changes, never the rules (no Sim file reads this).
    /// </summary>
    public static class OzRangeRingsOption
    {
        public const string Label = "RANGE RINGS ON SELECTED UNITS", Pref = "oz_range_rings";
        public const string Tip = "RANGE RINGS|The dotted rings that show how far your selected units can fire. FADE (default): they show for about 2 seconds after you select, then fade. ALWAYS: they stay while your units are selected.|";
        public const string FadeName = "FADE", AlwaysName = "ALWAYS";

        static int cached = -1;                                                                              // read every frame by the rings: PlayerPrefs once

        /// <summary>ALWAYS (oz_range_rings 1); FADE (0, the default, also any other saved value).</summary>
        public static bool Always
        {
            get { if (cached < 0) cached = PlayerPrefs.GetInt(Pref, 0) == 1 ? 1 : 0; return cached == 1; }
            set
            {
                cached = value ? 1 : 0; PlayerPrefs.SetInt(Pref, cached); PlayerPrefs.Save();
                Debug.Log($"[OpenZ Options] range rings on selected units: {Name} ({Pref} {cached}, saved, live)");
            }
        }

        /// <summary>The row's value: FADE (default) or ALWAYS.</summary>
        public static string Name => Always ? AlwaysName : FadeName;

        /// <summary>The cycle row's step: two values, either arrow flips it.</summary>
        public static void Step(int dir) => Always = !Always;

        /// <summary>The check's hook: forget the cached value, so the next read is the saved one again.</summary>
        public static void Reload() => cached = -1;
    }
}
