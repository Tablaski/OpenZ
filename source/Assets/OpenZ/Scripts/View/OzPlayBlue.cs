using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 11, EXPERIMENTAL PLAY AS BLUE: the human commands the BLUE team from the blue fort, the CPU commands RED. The map is not
    /// mirrored or rotated: the camera simply starts over the blue fort. Default OFF; the battlefield screen's box remembers it (<see cref="Pref"/>),
    /// like HEAD START and SHUTTLE (UI/OpenZMainMenu.PlayBlue.cs draws it in the OBJECTIVE card). Not offered in 'Zod exact'.
    /// How a level becomes a blue level:
    ///  * The player's own DEPLOY arms the mode for the level it loads (<see cref="Arm"/>; the box on and offered); the main menu disarms it
    ///    when it loads (<see cref="Disarm"/>), so RESTART / NEXT keep the side and the menu starts clean. A level opened any other way (the
    ///    editor's Play on a level scene, a proof or the soak calling the menu's Deploy, the frame-rate bench, a bot match) is never blue.
    ///  * ZGame.Awake asks <see cref="Sides"/> before it builds the world: the human = blue, the CPU drives red with ZGame's own difficulty
    ///    rule (ZGame.BotDifficulty: the chosen difficulty for the CPU against the human), blue gets the human's build-list bonus and HEAD
    ///    START at the blue fort (ZWorld.AddHeadStart looks for the human's fort). The rules of the simulation treat both teams alike
    ///    (ZWorld.IsHumanTeam asks the CPU list), so nothing of the simulation changes; the sides are View/OzSides.cs (pure).
    ///  * Everything that already follows ZGame.player follows blue: selection and orders (PlayerInput), the HUD (OpenZHud.Player: the unit
    ///    card, the production panel, the mission banner "DESTROY THE RED FORT", "YOU COMMAND BLUE"), the minimap and its radar dots, the
    ///    announcer's "our" / "enemy" lines (AudioDirector.Player), the music, the flag rings for the player's enemy flags, the fort alarm,
    ///    the end screen's VICTORY / DEFEAT and its subtitles.
    ///  * The colours are the sim's own: blue is drawn blue, red red; the visual option YOUR TEAM COLOUR does not apply in this mode
    ///    (TeamColors.Swap is off while <see cref="Active"/>), and every text or voice line that names a colour from the red player's side is
    ///    turned or left out like the swapped colours (TeamColors.WordsFlip: "BREAK INTO THE RED FORT", no "Destroy the blue fort.").
    ///  * OFF in this mode, ready for blue's own scenes later: Commander Zod's whole cameo in the level (the start briefing and its flyover,
    ///    the transmission window, the idle barb, the nuke button, the victory / defeat punchline), level 16's ending scene with the two
    ///    robots on their sun loungers (it needs the cameo). A plain start (the RTS camera over the blue fort, the HUD's mission banner) and
    ///    the game's own end (the fallen fort, the winners' cheer, the announcer, the end screen) remain.
    ///  * The SHUTTLE box works for blue too (1.2 item 111, DV144, Sim/ZWorld.SideShuttles.cs): on levels 16 and 17 (EASY / MEDIUM, the box
    ///    on) the player's ship stands by the BLUE fort on a spot of its own; the red fort's spots stay empty; level 17's car park has an
    ///    empty shuttle on each side; the testing panel still drops shuttles for both sides.
    ///    UI/OpenZEndScreen.Score.cs; <see cref="Unranked"/> is no longer used).
    /// Logs one line at the level start; Play check: <see cref="OzPlayBlueLevel.Status"/>.
    /// </summary>
    public static class OzPlayBlue
    {
        public const string Pref = "oz_play_blue";
        public const string Label = "PLAY AS BLUE", Experimental = "EXPERIMENTAL";
        public const string TipTitle = "PLAY AS BLUE · KEY ";
        /// <summary>The tooltip's words (EXPERIMENTAL in the bar's amber, OzUi.Amber).</summary>
        public const string TipBody = "<color=#F2B84B>EXPERIMENTAL.</color> You command the blue fort,\nthe CPU commands red, on the same map:\nthe camera starts over the blue fort.\nRed's story scenes are off;\nits victories enter the high scores.";
        /// <summary>The SHUTTLE block's lock words of the first PLAY AS BLUE (not used since 1.2 item 111: the box works for blue too).</summary>
        public const string ShuttleLockedRow = "NOT AS BLUE", ShuttleLockedHint = "RED SIDE ONLY";
        /// <summary>The end screen's score strip for a blue match until owner items 144 / 144b (no longer used: a blue victory enters
        /// the table).</summary>
        public const string Unranked = "PLAY AS BLUE · NOT IN THE TABLE";
        public const string MenuScene = "MainMenu";

        /// <summary>The battlefield screen's box (saved at once; OFF when nothing is saved yet).</summary>
        public static bool Box
        {
            get => PlayerPrefs.GetInt(Pref, 0) == 1;
            set { PlayerPrefs.SetInt(Pref, value ? 1 : 0); PlayerPrefs.Save(); }
        }
        /// <summary>The box is offered (not in 'Zod exact': there the human is red, as in Z).</summary>
        public static bool Offered(bool zodExact) => !zodExact;
        public static bool Offered() => Offered(PlayerPrefs.GetInt("oz_zod_exact", 0) == 1);
        /// <summary>The box is on and offered: what the battlefield screen shows and what DEPLOY arms.</summary>
        public static bool BoxOn => Box && Offered();

        /// <summary>The level now loading or running is played as blue (armed by DEPLOY, cleared by the main menu).</summary>
        public static bool Active { get; private set; }
        /// <summary>The level DEPLOY armed (log and checks).</summary>
        public static string ActiveLevel { get; private set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { Active = false; ActiveLevel = ""; }

        /// <summary>DEPLOY (UI/OpenZMainMenu.cs), before the loading screen is built: the next level is blue while the box is on and offered,
        /// and only when the player deployed it himself (<paramref name="byPlayer"/>: the DEPLOY button, ENTER or a double click on the
        /// level's card). A proof, the soak or any other tool that calls the menu's Deploy itself starts a red level, whatever the box says.</summary>
        public static void Arm(string level, bool byPlayer)
        {
            Active = byPlayer && BoxOn; ActiveLevel = Active ? level : "";
            Debug.Log($"[OpenZ Menu] side for {level}: {(Active ? "BLUE (PLAY AS BLUE, experimental): the CPU commands red" : "RED (the CPU commands blue)")} ({Pref} {(Box ? 1 : 0)}{(Offered() ? "" : ", not offered in 'Zod exact'")}{(byPlayer || !BoxOn ? "" : "; not the player's own DEPLOY (a proof or a test): red")})");
        }

        /// <summary>A test start of a dev exe only (View/CaptureKit.cs, -ozblue): the next level is blue, whatever the box says.</summary>
        public static void ProofArm(string level) { Active = true; ActiveLevel = level; }

        /// <summary>The main menu when it loads (and ZGame for a bot match): no level is blue any more.</summary>
        public static void Disarm(string why)
        {
            if (Active) Debug.Log($"[OpenZ] PLAY AS BLUE ends ({why})");
            Active = false; ActiveLevel = "";
        }

        /// <summary>ZodBriefing.Install: no Commander Zod cameo in a blue level (the menu keeps its greeting and quips).</summary>
        // duel no solo briefing
            public static bool NoStoryIn(string sceneName) => OpenZ.Multiplayer.DuelRuntime.Active || (Active && sceneName != MenuScene);

        /// <summary>ZGame.Awake, before the world is built: the sides of this match. Box off (or a bot match): nothing changes (the scene's
        /// red human and blue CPU, Z's sides). Box on: the human blue, the CPU red; the level component places the camera.</summary>
        public static OzSideSetup Sides(ZGame g, bool demo)
        {
            if (!Active || !g) return OzSides.For(false, demo);
            if (demo) { Disarm("a bot match: both sides CPU"); return OzSides.For(false, true); }
            var s = OzSides.For(true, false);
            g.player = s.Human; g.blueBot = s.BlueBot; g.redBot = s.RedBot;
            if (!g.GetComponent<OzPlayBlueLevel>()) g.gameObject.AddComponent<OzPlayBlueLevel>();
            Debug.Log($"[OpenZ] PLAY AS BLUE (experimental) on {g.level}: you command {s.Human} from the blue fort, the CPU commands {s.Cpu}; "
                      + "Commander Zod's briefing, flyover and end scenes off, the SHUTTLE box's ship by the blue fort (DV144), the team colour option not used, its victories enter the high scores tagged EVILZOD");
            return s;
        }

        // ---------------------------------------------------------------- the battlefield screen's words (box on: the blue side's)
        /// <summary>The OBJECTIVE card's headline: blue's goal while the box is on ("BREAK INTO THE RED FORT"), else as before.</summary>
        public static string CardWords(string headline) => BoxOn ? OzSides.FlipColourWords(headline) : TeamColors.Words(headline);
        /// <summary>The card's fort glyph: the enemy's fort in its colour (red while the box is on; the colour option does not apply then).</summary>
        public static Color CardEnemyMark => BoxOn ? (Color)OzUi.RedHi : OzUi.Mark(Team.Blue);
        /// <summary>The card's small line while the box is on: EXPERIMENTAL (amber) and the level's flag count.</summary>
        public static string CardSub(int flags) => "<color=" + OzUi.Hex(OzUi.Amber) + ">" + Experimental + "</color>" + (flags > 0 ? " · " + flags + " FLAGS" : " · CAPTURE THE FLAGS");
        /// <summary>The SHUTTLE block is never locked by the side (1.2 item 111, DV144: the box works for blue too; HARD still locks it).</summary>
        public static bool LocksShuttle(string level) => false;

        // ---------------------------------------------------------------- the level start rules (UI/OzShuttleStart.cs hooks)
        /// <summary>ZWorld.ShuttleOn: the box's answer for either side (OzSides.StartShuttle; DV144 puts a blue player's ship by the blue fort).</summary>
        public static bool ShuttleRule(string level, bool rule) => OzSides.StartShuttle(level, Active, rule);
        /// <summary>ZWorld.L17StartShuttle: the box's answer for either side (OzSides.FortShuttle; the red fort's spot stays red's).</summary>
        public static bool FortShuttleRule(bool rule) => OzSides.FortShuttle(Active, rule);

        /// <summary>The testing panel's column titles: the shown colour and who commands it ("BLUE · PLAYER", "RED · CPU").</summary>
        public static string CheatColumn(Team t)
        {
            var g = ZGame.I; Team human = g ? g.player : Team.Red;
            return TeamColors.Name(t) + " · " + (g && g.blueBot && g.redBot ? "CPU" : t == human ? "PLAYER" : "CPU");
        }
    }

    /// <summary>
    /// The blue level's own start (added by <see cref="OzPlayBlue.Sides"/>): the RTS camera starts over the blue fort with the picture the
    /// scene gives the red fort (the same offset from the fort's middle, the same height; OzSides.StartFocus), before the camera's own Start.
    /// View only. Play check: <see cref="Status"/>.
    /// </summary>
    [DefaultExecutionOrder(-55)]
    public sealed class OzPlayBlueLevel : MonoBehaviour
    {
        public static OzPlayBlueLevel I { get; private set; }
        /// <summary>The scene's start focus (made for the red fort), the blue one, and the forts' middles (world metres).</summary>
        public Vector3 SceneFocus, StartFocus, RedFort, HumanFort;
        public bool Placed;

        void Awake() { I = this; }
        void OnDestroy() { if (I == this) I = null; }

        void Start()
        {
            var g = ZGame.I; var rts = FindFirstObjectByType<RtsCamera>();
            if (!g || g.World == null || !rts) { Debug.LogWarning("[OpenZ] PLAY AS BLUE: no game or camera: the camera stays where the scene put it"); return; }
            if (!FortMiddle(g, Team.Red, out RedFort) || !FortMiddle(g, g.player, out HumanFort)) { Debug.LogWarning("[OpenZ] PLAY AS BLUE: a fort is missing: the camera stays where the scene put it"); return; }
            SceneFocus = rts.focus;
            OzSides.StartFocus(SceneFocus.x, SceneFocus.z, RedFort.x, RedFort.z, HumanFort.x, HumanFort.z, out double x, out double z);
            rts.focus = new Vector3((float)x, 0f, (float)z);
            rts.Place();
            StartFocus = rts.focus; Placed = true;
            Debug.Log($"[OpenZ] PLAY AS BLUE: the camera starts over the {g.player} fort: focus {StartFocus.x:0.0} / {StartFocus.z:0.0} (the scene's {SceneFocus.x:0.0} / {SceneFocus.z:0.0} for the red fort; "
                      + $"forts red {RedFort.x:0.0} / {RedFort.z:0.0}, {g.player} {HumanFort.x:0.0} / {HumanFort.z:0.0}), height {rts.height:0} m");
        }

        /// <summary>The middle of a team's fort (every fort part; world metres, y 0).</summary>
        static bool FortMiddle(ZGame g, Team t, out Vector3 at)
        {
            double sx = 0, sy = 0; int n = 0;
            foreach (var o in g.World.Objects) if (o.IsFort && o.Owner == t) { sx += o.CenterX; sy += o.CenterY; n++; }
            at = n > 0 ? ZGame.PxToWorld(sx / n, sy / n) : default;
            return n > 0;
        }

        /// <summary>Play check: one line (the sides, the CPUs, the camera, the cameo, the shuttles).</summary>
        public static string Status()
        {
            var g = ZGame.I; var W = g ? g.World : null;
            if (!g || W == null) return "no level";
            var bots = new List<string>(); foreach (var b in W.Bots) bots.Add(b.Team + " " + b.Difficulty);
            var rts = FindFirstObjectByType<RtsCamera>();
            return $"PLAY AS BLUE {(OzPlayBlue.Active ? "on" : "off")} ({OzPlayBlue.ActiveLevel}): human {g.player}, CPU [{string.Join(", ", bots)}], blueBot {g.blueBot}, redBot {g.redBot}; "
                   + $"camera {(I && I.Placed ? $"placed over the {g.player} fort at {I.StartFocus.x:0.0} / {I.StartFocus.z:0.0}" : "the scene's")}, now {(rts ? rts.focus.x.ToString("0.0") + " / " + rts.focus.z.ToString("0.0") : "none")}; "
                   + $"Zod cameo {(ZodBriefing.I ? "PRESENT" : "none")}; team colours swapped {TeamColors.Swap}; words flip {TeamColors.WordsFlip}; "
                   + $"shuttle rule {ZWorld.ShuttleOn}, the player's start ship {(W.PlayableShuttle != null ? "#" + W.PlayableShuttle.Id + " " + W.PlayableShuttle.Owner : "none")}, by the blue fort {(W.BlueStartShuttle != null ? "PRESENT" : "none")}, "
                   + $"level 17 red fort shuttle {(W.L17FortShuttle != null ? "PRESENT" : "none")}, car-park ships {(W.L17CarParkShuttle != null ? 1 : 0) + (W.L17BlueCarParkShuttle != null ? 1 : 0)}";
        }
    }
}
