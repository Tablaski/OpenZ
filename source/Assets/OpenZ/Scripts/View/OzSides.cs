using System.Text.RegularExpressions;
using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>The sides of one match: the team the human commands and the teams the CPU drives (<see cref="OzSides.For"/>).</summary>
    public struct OzSideSetup
    {
        /// <summary>The human's team (Neutral in a bot match: nobody plays).</summary>
        public Team Human;
        /// <summary>The CPU's team against the human (Neutral in a bot match: both are CPUs).</summary>
        public Team Cpu;
        /// <summary>ZGame.blueBot / ZGame.redBot: the teams a ZBot drives.</summary>
        public bool BlueBot, RedBot;
        /// <summary>The experimental PLAY AS BLUE mode is in force for this match.</summary>
        public bool PlayBlue;
        /// <summary>Both teams CPU (the bot match).</summary>
        public bool Demo;
        public override string ToString() => Demo ? "bot match (red and blue CPU)" : $"human {Human}, CPU {Cpu}{(PlayBlue ? " (PLAY AS BLUE)" : "")}";
    }

    /// <summary>
    /// 1.2 item 11, EXPERIMENTAL PLAY AS BLUE: the pure rules of the side choice (no engine, no state), shared by the game (View/OzPlayBlue.cs,
    /// View/ZGame.cs) and the counted check (Editor/OzV12PlayBlueCount.cs, also run outside the editor), so the check measures the code the
    /// game runs.
    ///  * <see cref="For"/>: the match's sides. Normal start: the human is RED, the CPU drives BLUE (Z, ZGame's scene values: player Red,
    ///    blueBot on, redBot off). PLAY AS BLUE: the human is BLUE, the CPU drives RED. A bot match: both CPUs (the mode never applies).
    ///  * <see cref="BotDifficulty"/>: ZGame.BotDifficulty's rule for any side: the chosen difficulty for the CPU that plays against the
    ///    human, Normal for every other CPU (a bot match: Normal for both).
    ///  * <see cref="Outcome"/>: the end screen's word for the human ("victory" / "defeat" / "draw"), as UI/OpenZEndScreen.cs decides it.
    ///  * <see cref="StartShuttle"/> / <see cref="FortShuttle"/>: the SHUTTLE box's answer, the same for both sides (1.2 item 111, DV144: a blue
    ///    player's ship stands by the blue fort, Sim/ZWorld.SideShuttles.cs).
    ///  * <see cref="StartFocus"/>: the camera starts over the human's fort with the framing the scene gives the red fort (the map is not
    ///    mirrored or rotated, the camera keeps looking north).
    ///  * <see cref="FlipColourWords"/>: RED and BLUE trade places in a text written for the red player ("BREAK INTO THE BLUE FORT").
    /// Nothing here changes a rule of the simulation: the sides are who gives the orders; the sim treats both teams alike (ZWorld.IsHumanTeam
    /// asks the CPU list).
    /// </summary>
    public static class OzSides
    {
        /// <summary>The sides of a match. <paramref name="playBlue"/>: the PLAY AS BLUE box was on when the level was deployed;
        /// <paramref name="demo"/>: a bot match (both CPU), where the box never applies.</summary>
        public static OzSideSetup For(bool playBlue, bool demo)
        {
            if (demo) return new OzSideSetup { Human = Team.Neutral, Cpu = Team.Neutral, BlueBot = true, RedBot = true, PlayBlue = false, Demo = true };
            return playBlue
                ? new OzSideSetup { Human = Team.Blue, Cpu = Team.Red, BlueBot = false, RedBot = true, PlayBlue = true }
                : new OzSideSetup { Human = Team.Red, Cpu = Team.Blue, BlueBot = true, RedBot = false, PlayBlue = false };
        }

        /// <summary>The other team (Neutral stays Neutral).</summary>
        public static Team EnemyOf(Team t) => t == Team.Red ? Team.Blue : t == Team.Blue ? Team.Red : Team.Neutral;

        /// <summary>ZGame.BotDifficulty for the CPU of team <paramref name="bot"/>: the chosen difficulty against the human, else Normal.</summary>
        public static ZBotDifficulty BotDifficulty(Team bot, OzSideSetup s, ZBotDifficulty chosen) => s.Demo ? ZBotDifficulty.Normal : bot != s.Human ? chosen : ZBotDifficulty.Normal;

        /// <summary>The team whose factories and fort read their build lists higher up (ZGame.ApplyTechBonus): the human's (none in a bot match).</summary>
        public static Team TechBonusTeam(OzSideSetup s) => s.Demo ? Team.Neutral : s.Human;

        /// <summary>The end screen's word for the human: "victory", "defeat" or "draw" (UI/OpenZEndScreen.cs Show).</summary>
        public static string Outcome(Team winner, Team human) => winner == Team.Neutral ? "draw" : winner == human ? "victory" : "defeat";

        /// <summary>ZWorld.ShuttleOn at the start: the battlefield screen's rule (<paramref name="rule"/>, UI/OzShuttleStart.Rule), for both
        /// sides (1.2 item 111, DV144): with the human blue, level 16's pocket by the red fort stays empty and his ship stands by the blue fort
        /// (Sim/ZWorld.SideShuttles.cs). <paramref name="level"/> and <paramref name="playBlue"/> are kept for the callers.</summary>
        public static bool StartShuttle(string level, bool playBlue, bool rule) => rule;

        /// <summary>ZWorld.L17StartShuttle at the start: the SHUTTLE box's answer on level 17, for both sides (1.2 item 111, DV144): the red
        /// fort's spot stays red's (Sim/ZWorld.L17Shuttles.cs), a blue human's ship stands by the blue fort (Sim/ZWorld.SideShuttles.cs).
        /// Level 17's two EMPTY car-park shuttles are not the box's and stand there on every side.</summary>
        public static bool FortShuttle(bool playBlue, bool rule) => rule;

        /// <summary>The camera's start focus over the human's fort: the scene's own start focus is made for the red fort (sceneX, sceneZ, world
        /// metres) and the red fort's middle is (redX, redZ); the same offset from the human's fort middle (fortX, fortZ) gives the same
        /// picture of that fort (the camera is not turned, the map not mirrored).</summary>
        public static void StartFocus(double sceneX, double sceneZ, double redX, double redZ, double fortX, double fortZ, out double x, out double z)
        {
            x = fortX + (sceneX - redX);
            z = fortZ + (sceneZ - redZ);
        }

        static readonly Regex ColourWord = new Regex(@"\b(red|blue)(s?)\b", RegexOptions.IgnoreCase);

        /// <summary>RED and BLUE (any case, also "reds" / "blues") trade places, the case kept: "BREAK INTO THE BLUE FORT" -> "BREAK INTO THE RED FORT".</summary>
        public static string FlipColourWords(string s) => string.IsNullOrEmpty(s) ? s : ColourWord.Replace(s, m =>
        {
            string w = m.Groups[1].Value, o = w.ToLowerInvariant() == "red" ? "blue" : "red";
            o = w == w.ToUpperInvariant() ? o.ToUpperInvariant() : char.IsUpper(w[0]) ? char.ToUpperInvariant(o[0]) + o.Substring(1) : o;
            return o + m.Groups[2].Value;
        });
    }
}
