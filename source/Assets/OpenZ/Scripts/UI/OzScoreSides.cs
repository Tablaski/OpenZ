using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// faction screen.
    ///  * Both sides enter the same tables under the same rules (OzHighScores: the same score, the same difficulty, the same 10 best of each
    ///    level, the same order); a victory won as blue is entered exactly as a red one. The end screen makes every entry here
    ///    (<see cref="Entry"/>), with the side of the human player at the end (hud.Player: red, or blue in the blue mode).
    ///  * Every entry keeps its side (OzScoreEntry.side: 0 red, 1 blue) and every row shows it: the HIGH SCORES popup as a small tag in the
    ///    side's team paint (ZOD on red, EVILZOD on blue; UI/OpenZMainMenu.HighScoresSide.cs), the end screen's TOP 10 tooltip as the word in
    ///    the side's text colour. The colours are the factions' own: the option YOUR TEAM COLOUR does not change them.
    ///  * Old entries: a file written before the side was kept has no side for its entries; they read as 0, ZOD (until then only red could
    ///    enter a table). Any other number reads as ZOD too. A build from before reads a file with sides as before: the field it does not
    ///    know is skipped, every score stays (no score is lost either way).
    /// View side only: the sim never reads any of it.
    /// </summary>
    public static class OzScoreSides
    {
        public const int Red = 0, Blue = 1;
        /// <summary>The rows' tags: the generals' names of the faction screen without GENERAL.</summary>
        public static readonly string[] Tag = { "ZOD", "EVILZOD" };

        /// <summary>The side of a human team: blue = 1, anything else = 0 (red).</summary>
        public static int Of(Team human) => human == Team.Blue ? Blue : Red;

        /// <summary>The side an entry was won with: 1 only for blue; an older entry (no side), null or any other number = 0 (red).</summary>
        public static int Side(OzScoreEntry e) => e != null && e.side == Blue ? Blue : Red;

        /// <summary>"ZOD" or "EVILZOD".</summary>
        public static string TagOf(OzScoreEntry e) => Tag[Side(e)];

        /// <summary>The tag's chip: the side's team paint (OzUi.Red / OzUi.Blue), never swapped.</summary>
        public static Color Paint(int side) => side == Blue ? (Color)OzUi.Blue : (Color)OzUi.Red;

        /// <summary>The side's text colour (OzUi.RedText / OzUi.BlueText, as the battlefield screen's GENERAL ZOD / GENERAL EVILZOD).</summary>
        public static Color Text(int side) => side == Blue ? (Color)OzUi.BlueText : (Color)OzUi.RedText;

        /// <summary>The tag as rich text in the side's text colour (the end screen's TOP 10 tooltip).</summary>
        public static string Rich(OzScoreEntry e)
        {
            int s = Side(e);
            return "<color=" + OzUi.Hex(s == Blue ? OzUi.BlueText : OzUi.RedText) + ">" + Tag[s] + "</color>";
        }

        /// <summary>The end screen's table entry (the only place an entry is made): the result as the end screen shows it, and the side of
        /// the human player who won it. missionSeconds is the mission time (whole seconds are kept, as before).</summary>
        public static OzScoreEntry Entry(int score, double missionSeconds, int kills, int losses, int built, int zones, int zonesTotal, Team human)
            => new OzScoreEntry { score = score, seconds = (int)missionSeconds, kills = kills, losses = losses, built = built, zones = zones, zonesTotal = zonesTotal, side = Of(human) };
    }
}
