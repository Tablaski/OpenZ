// OpenZ (GPL-3.0) - 1.5, kit player_reports_1111, P3: the gamepad's R3 presses the LAST MESSAGE key (View/OzTouch.Gamepad.cs TapKey), and
// with TACTICAL PAUSE on (the default) that key freezes time. The TOUCH AND GAMEPAD card (UI/OpenZHud.TouchKeys.cs) said "Exit crew · last
// message" for L3 · R3 all the same; now its text follows the switch each time the card opens.
namespace OpenZ
{
    public partial class OpenZHud
    {
        /// <summary>The TOUCH AND GAMEPAD card's L3 · R3 row: its key text and its two texts.</summary>
        public const string PadR3Key = "L3 · R3", PadR3Text = "Exit crew · last message", PadR3TextPause = "Exit crew · tactical pause";
        OzText padR3Label;

        /// <summary>HelpColumn: the gamepad column's L3 · R3 label is kept for the live text.</summary>
        void PadR3Keep(string column, string key, OzText label) { if (column == "Pad" && key == PadR3Key) padR3Label = label; }

        /// <summary>ShowTouchHelp(true): the L3 · R3 text as the switch has it now.</summary>
        void PadR3Refresh() { if (padR3Label != null) padR3Label.Set(OzTacticalPause.TakesLastMessageKey ? PadR3TextPause : PadR3Text); }
    }
}
