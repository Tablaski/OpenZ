using System.Linq;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// stored in oz_difficulty (it keeps 0 = HARD, oz_mayhem 1 marks MAYHEM) and never reaches a rule or the computer: only
    /// OpenZDifficulty.Choice, ZGame.ShownDifficulty, the menus' texts and the score tables (OzHighScores keys them by Label) carry it.</summary>
    public enum GameDifficulty { Hard = 0, Easy = 1, Medium = 2, Mayhem = 3 }

    /// <summary>
    /// Difficulty choice shared by the menus: PlayerPrefs "oz_difficulty". ZGame reads it when a level starts; setting it during
    /// a game also re-tunes the running CPU (human vs CPU only; bot vs bot stays Zod).
    ///   EASY   = the Easy CPU handicap (ZBotDifficulty.Easy) + every unit unlocked in the player's factories and fort
    ///            (ZWorld.TechBonus +5, L02 too) + the L02 head start vehicles, always.
    ///   MEDIUM = the Zod CPU + the player's factories two tech levels up + the L02 head start (menu toggle).
    ///   HARD   = the Zod CPU, Zod build lists, map start units + one missile cannon on each fort's door tower (ZWorld.FortDoorGunDeviation);
    ///            the deliberate OpenZ rule changes (DV rows) apply on every difficulty.
    /// All three are OpenZ deviations (parity report, Deviations: DV4; Hard also DV73).
    ///   MAYHEM = HARD for the rules and the CPU (Level reads HARD) + the fog of war in that match (View/OzFogOfWar.cs WhyOff); 1.4 MAYHEM,
    /// </summary>
    public static class OpenZDifficulty
    {
        public const string Key = "oz_difficulty";

        public static GameDifficulty Level
        {
            get { int v = PlayerPrefs.GetInt(Key, (int)GameDifficulty.Medium); /* menu_exe_v1: MEDIUM is the default */ return v == 1 ? GameDifficulty.Easy : v == 2 ? GameDifficulty.Medium : GameDifficulty.Hard; }
            set { if (value == GameDifficulty.Mayhem) { Choice = value; return; } PlayerPrefs.SetInt(Key, (int)value); PlayerPrefs.SetInt(MayhemKey, 0); PlayerPrefs.Save(); ApplyLive(value); }   // 1.4 MAYHEM: a rules difficulty set here clears MAYHEM; Level = Mayhem is Choice = Mayhem
        }

        /// <summary>
        /// oz_difficulty so every place that stores or puts back the rules' difficulty as a number (DEPLOY, the -take snapshot and its
        /// restore, the one-time repair, a 1.3 game on the same computer) keeps working: they see HARD. Ignored while Level is not HARD.</summary>
        public const string MayhemKey = "oz_mayhem";
        /// <summary>The player chose MAYHEM (Level HARD and oz_mayhem 1).</summary>
        public static bool MayhemChosen => Level == GameDifficulty.Hard && PlayerPrefs.GetInt(MayhemKey, 0) == 1;
        /// <summary>The difficulty the player chose, as the menus show it: EASY, MEDIUM, HARD or MAYHEM (Level stays the rules' one:
        /// MAYHEM is HARD there). Set: saved at once (oz_difficulty the rules' value, oz_mayhem 1 only for MAYHEM).</summary>
        public static GameDifficulty Choice
        {
            get => MayhemChosen ? GameDifficulty.Mayhem : Level;
            set
            {
                var r = Rules(value);
                PlayerPrefs.SetInt(Key, (int)r); PlayerPrefs.SetInt(MayhemKey, value == GameDifficulty.Mayhem ? 1 : 0); PlayerPrefs.Save();
                ApplyLive(r);
            }
        }
        /// <summary>The difficulty the rules and the computer play: MAYHEM is HARD (1.4 MAYHEM).</summary>
        public static GameDifficulty Rules(GameDifficulty d) => d == GameDifficulty.Mayhem ? GameDifficulty.Hard : d;

        /// <summary>The CPU handicap of a difficulty: only Easy softens the bot.</summary>
        public static ZBotDifficulty Bot(GameDifficulty d) => d == GameDifficulty.Easy ? ZBotDifficulty.Easy : ZBotDifficulty.Normal;
        /// <summary>Kept for older callers: the CPU handicap of the stored difficulty.</summary>
        public static ZBotDifficulty Current => Bot(Level);

        /// <summary>Extra build-list levels for the human player (ZWorld.TechBonus).</summary>
        public static int TechBonus(GameDifficulty d) => d == GameDifficulty.Easy ? 5 : d == GameDifficulty.Medium ? 2 : 0;

        /// <summary>Does the human get the L02 head start? Easy always, Medium when the menu toggle is on, Hard never.</summary>
        /// <summary>
        /// default OFF) on every difficulty and both maps: L02 2 light tanks, 2 jeeps, 1 mech; L17 1 MLRS, 1 heavy tank.</summary>
        public const string HeadStartPref = "oz_headstart";
        public static bool HeadStartToggle { get => PlayerPrefs.GetInt(HeadStartPref, 0) == 1; set { PlayerPrefs.SetInt(HeadStartPref, value ? 1 : 0); PlayerPrefs.Save(); } }
        public static bool HeadStart(GameDifficulty d, bool toggle) => toggle;
        /// <summary>
        /// The head start a match of this chosen difficulty gets: the saved switch, never on MAYHEM. The switch itself
        /// is kept (it comes back on EASY, MEDIUM and HARD).</summary>
        public static bool HeadStartFor(GameDifficulty choice) => choice != GameDifficulty.Mayhem && HeadStartToggle;
        internal static string[] HeadStartDefaults(string level) => level == "L02" ? ZGame.HeadStartTypes :
            level == "L17" ? ZGame.EasyL17Types : level == "L16" ? ZGame.HeadStartL16Types :
            OzVolcanic.Is(level) ? ZGame.HeadStartL07Types : OzCity.IsBridgeGame(level) ? ZGame.HeadStartL18Types : System.Array.Empty<string>();

        static string headStartMap, headStartRoster;
        static int headStartCount;
        static string headStartHint;
        static void RefreshHeadStartPreview(string level)
        {
            string roster = PlayerPrefs.GetString(OzSoloAllowedUnits.Preference, "");
            if (level == headStartMap && roster == headStartRoster) return;
            headStartMap = level;
            headStartRoster = roster;
            var defaults = HeadStartDefaults(level);
            var units = ZWorld.SelectHeadStart(defaults, OzSoloAllowedUnits.Read().Allows);
            headStartCount = units.Length;
            headStartHint = units.Length == 0 ? "NO ELIGIBLE REINFORCEMENTS" : string.Join(" · ", units.GroupBy(type => type).Select(group =>
            {
                int count = group.Count();
                string name = UnitInfo.Name(group.Key);
                return $"{count} {name}{(count == 1 || name == "MLRS" ? "" : "S")}";
            }));
        }
        public static int HeadStartCount(string level) { RefreshHeadStartPreview(level); return headStartCount; }
        public static string HeadStartHint(string level) { RefreshHeadStartPreview(level); return headStartHint; }

        public static string Label(GameDifficulty d) => d == GameDifficulty.Easy ? "EASY" : d == GameDifficulty.Medium ? "MEDIUM" : d == GameDifficulty.Mayhem ? "MAYHEM" : "HARD";   // 1.4 MAYHEM

        /// <summary>One-line explanation for the level select / options.</summary>
        public static string Hint(GameDifficulty d) => d == GameDifficulty.Easy
            ? "EVERY UNIT UNLOCKED · GENTLE CPU"
            : d == GameDifficulty.Medium ? "FACTORIES 2 TECH LEVELS UP · ZOD CPU" : d == GameDifficulty.Mayhem ? "HARD + FOG OF WAR · FORT MISSILE CANNONS · ENEMY BUILDS 10 % FASTER" : "Z'S BUILD LISTS · FORT MISSILE CANNONS";

        public static GameDifficulty Next(GameDifficulty d) => d == GameDifficulty.Easy ? GameDifficulty.Medium : d == GameDifficulty.Medium ? GameDifficulty.Hard : d == GameDifficulty.Hard ? GameDifficulty.Mayhem : GameDifficulty.Easy;   // 1.4 MAYHEM: EASY, MEDIUM, HARD, MAYHEM, EASY

        static void ApplyLive(GameDifficulty d)
        {
            var g = ZGame.I;
            if (g == null || g.World == null || g.World.Map.skirmish || Multiplayer.DuelRuntime.Active) return;
            d = Rules(d); g.gameDifficulty = d; g.difficulty = Bot(d);   // 1.4 MAYHEM: a running match gets the rules' difficulty (HARD); its MAYHEM (the fog) is fixed at its start
            foreach (var b in g.World.Bots) b.Difficulty = g.BotDifficulty(b.Team);
            g.ApplyTechBonus();
        }
    }
}
