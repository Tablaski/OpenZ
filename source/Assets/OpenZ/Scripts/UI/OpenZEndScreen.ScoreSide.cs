namespace OpenZ
{
    /// <summary>
    /// level's table (UI/OpenZEndScreen.Score.cs: OzScoreSides.Entry with the human player's team), its entry keeps the side, the TOP 10
    /// tooltip shows it on every row (OzHighScores.TableLines). Checks only here.
    /// </summary>
    public partial class OpenZEndScreen
    {
        /// <summary>Checks: the side of this ending's player (0 red ZOD, 1 blue EVILZOD; -1 no HUD).</summary>
        public int ProofScoreSide => hud ? OzScoreSides.Of(hud.Player) : -1;
        /// <summary>Checks: the entry this ending put into the table (null: none, or it did not make the top 10).</summary>
        public OzScoreEntry ProofScoreEntry => scoreEntry;
        /// <summary>Checks: the text of the TOP 10 tooltip's table (the level's rows with their sides; the new entry marked).</summary>
        public string ProofScoreTableTip => game ? OzHighScores.TableLines(game.level, game.ShownDifficulty, scoreEntry, game.SoloAi2026) : "";
    }
}
