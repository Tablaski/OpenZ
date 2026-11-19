// OpenZ (GPL-3.0)
namespace OpenZ
{
    /// <summary>
    /// 1.5 review R9, the TACTICAL PAUSE's two fixes (switches for the checks; the defaults are the fix):
    /// <list type="bullet">
    /// <item><see cref="ClockSaysPaused"/>: while the freeze holds, the match clock says PAUSED (the HUD's own amber PAUSED line, the digits
    /// blinking as in the pause menu's pause), so a frozen battle never reads as a frozen game. UI/OpenZHud.cs clock (before: no PAUSED word
    /// while frozen). Still no voice line, no toast.</item>
    /// <item><see cref="OwnSource"/>: the freeze and unfreeze takes play on the sound director's own source (View/AudioDirector.TimeFreeze.cs:
    /// why the unfreeze went unheard), the unfreeze <see cref="UnfreezeLiftDb"/> dB louder; one of the 4 takes every time, random, never the
    /// same one twice in a row (the event's shuffled bag).</item>
    /// </list>
    /// </summary>
    public sealed partial class OzTacticalPause
    {
        public static bool ClockSaysPaused = true, OwnSource = true;
        public static float UnfreezeLiftDb = 3f;
    }
}
