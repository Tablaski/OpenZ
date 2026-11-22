using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// While the player's side owns no standing radar, enemy health bars are not shown; capture one and they fade in,
    /// lose the last one and they fade out (0.35 s, smootherstep, unscaled time). View only: the rules, the computer and bot matches are untouched.
    /// <list type="bullet">
    /// <item>ONE radar truth with the minimap's enemy dots (DV33, UI/OpenZMinimap.cs) and the strike-group warning (View/OzStrikeWarning.cs): the
    /// warning's own <see cref="OzStrikeWarning.OwnsRadar"/> (a building of type radar, owned by the player's team, not destroyed), asked once per
    /// frame (<see cref="Tick"/>, cached by Time.frameCount), never per bar. A change counts once it held <see cref="OzRadarBarsRule.HoldSec"/> s
    /// of game time (World.Time): a radar flag both sides stand on changes owner every 0.2 s and must not make the bars pulse.</item>
    /// <item>ONE switch: DV33's (PlayerPrefs oz_radar_dots 0 = the old minimap) turns this off too. Off as well: the Unit editor, 'Zod exact'
    /// (read once per match), a match without a human side (ZWorld.IsHumanTeam, the strike warning's own gate). The rule itself, the enemy test,
    /// the hold and the fade: <see cref="OzRadarBarsRule"/> (no engine types).</item>
    /// <item>Who reads it: UI/OpenZUnitOverlay.cs Draw (every small world bar: selected, hovered, a spread squad's members, HEALTH BARS ON EVERY
    /// DAMAGED UNIT; the grenade icon and the veteran star keep their place, the bar's own images fade) and UI/OpenZHud.RadarBars.cs (the unit
    /// panel's bar, number and label, a guard: in a match the panel only ever shows the player's own units). The overlay ticks it every frame
    /// (its LateUpdate, also in the pause), the unit panel before it reads (the HUD's Update runs first): the same numbers for both in one frame.</item>
    /// </list>
    /// No allocation per frame: strings only for the log lines, at most one a second (changes in between are counted and named in the next line,
    /// and the last state of a burst is logged once the second is over).
    /// </summary>
    public static class OzRadarBars
    {
        public const float Hidden = OzRadarBarsRule.Hidden;
        /// <summary>The log's pace: at most one line this often (s, unscaled), the match's first line always.</summary>
        public const float LogEvery = 1f;
        /// <summary>The game's one state (the checks make their own <see cref="OzRadarBarsRule"/>).</summary>
        public static readonly OzRadarBarsRule Live = new OzRadarBarsRule();
        /// <summary>The shared fade factor of every enemy bar (1 = shown as before, 0 = not drawn).</summary>
        public static float Factor => Live.K;
        public static bool RuleOn => Live.RuleOn;
        /// <summary>The radar as the rule follows it (held), the truth of this frame, and whether a change waits for its hold.</summary>
        public static bool Owns => Live.Owns;
        public static bool Raw => Live.Raw;
        public static bool Pending => Live.Pending;
        /// <summary>How often the truth changed in this match while the rule acted (a contested radar flag: 5 a second).</summary>
        public static int Flips => Live.Flips - flips0;
        public static string Why => Live.Why;
        /// <summary>The fade's length: 0.35 s, always (decision 6; one line to make it OzMotion.Dur(OzRadarBarsRule.FadeSeconds, true), see the README).</summary>
        public static float FadeSeconds => OzRadarBarsRule.FadeSeconds;

        /// <summary>Play check counters: radars gained / lost (held changes) while the rule acted; marks whose enemy bar was drawn / held back
        /// (Note); enemy marks left out of HEALTH BARS ON EVERY DAMAGED UNIT (UI/OpenZUnitOverlay.cs); log lines written.</summary>
        public static int Gained, Lost, EnemyShown, EnemyHidden, SkippedAlways, LogLines;

        static int frame = -1, flips0; static ZWorld world; static bool zodExact, playground;
        static readonly OzRadarBarsRule.LogPace pace = new OzRadarBarsRule.LogPace(LogEvery);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            frame = -1; world = null; zodExact = playground = false; Live.Reset(); flips0 = 0; pace.Reset();
            Gained = Lost = EnemyShown = EnemyHidden = SkippedAlways = LogLines = 0;
        }

        /// <summary>The frame cache: true the first time a frame number is seen.</summary>
        public static bool NewFrame(int f) { if (f == frame) return false; frame = f; return true; }

        /// <summary>Why the rule is off now (null = it acts), from the live switches: DV33's (OpenZMinimap.RadarRule), the Unit editor
        /// (OzUeHooks.Active or its playground scene), 'Zod exact', the match's human side.</summary>
        public static string WhyOffNow(ZWorld w, Team human, bool playgroundScene, bool zodExactPref)
            => OzRadarBarsRule.WhyOff(OpenZMinimap.RadarRule, OzUeHooks.Active || playgroundScene, zodExactPref, w != null && w.IsHumanTeam(human));

        /// <summary>Once per frame (the first call of a frame works, the others return at once): the switches, the radar truth, the hold, the fade.</summary>
        public static void Tick(ZGame g)
        {
            if (!NewFrame(Time.frameCount)) return;
            var w = g ? g.World : null;
            if (w == null) { world = null; return; }
            bool fresh = w != world;
            if (fresh)
            {
                world = w;
                zodExact = PlayerPrefs.GetInt("oz_zod_exact", 0) != 0;                                     // read once per match (ZGame reads it at the level start too: 0 = OpenZ)
                playground = OzUnitEditor.IsPlayground(SceneManager.GetActiveScene());                     // Scene.name allocates: once per match
            }
            var human = g.player;
            string why = WhyOffNow(w, human, playground, zodExact);
            bool owns = why == null && OzStrikeWarning.OwnsRadar(w, human);                                 // the strike warning's and the minimap's truth: one scan, early exit
            int ch = Live.Advance(why, owns, human, Time.unscaledDeltaTime, FadeSeconds, fresh, w.Time);
            if (ch == OzRadarBarsRule.NewMatch) { flips0 = Live.Flips; pace.Reset(); }
            float now = Time.unscaledTime;
            if (ch == OzRadarBarsRule.NoChange) { if (pace.Quiet(now)) LogLine(ch, w, human, now); return; }
            if (ch == OzRadarBarsRule.RadarGained) Gained++; else if (ch == OzRadarBarsRule.RadarLost) Lost++;
            if (pace.Change(now, ch == OzRadarBarsRule.NewMatch)) LogLine(ch, w, human, now);
        }

        /// <summary>One log line (only on a change, at most one a second): what changed, or (NoChange) the state a burst of changes ended in.</summary>
        static void LogLine(int ch, ZWorld w, Team human, float now)
        {
            LogLines++;
            string what = ch == OzRadarBarsRule.NewMatch || ch == OzRadarBarsRule.RuleChanged
                ? $"{(Live.RuleOn ? "ON" : "off (" + Live.Why + ")")}, player {human}, radar {(Live.Owns ? "owned: bars shown" : "none: enemy bars hidden")}"
                : ch == OzRadarBarsRule.RadarGained ? "radar gained: they fade in" : ch == OzRadarBarsRule.RadarLost ? "radar lost: they fade out"
                : $"radar {(Live.Owns ? "owned: bars shown" : "none: enemy bars hidden")} after the changes above";
            Debug.Log($"[OpenZ] enemy health bars need a radar (DV172): {what} at {w.Time:0.0} s (fade {FadeSeconds:0.00} s after a {OzRadarBarsRule.HoldSec:0.0} s hold; "
                      + $"the radar truth changed {Flips} times in this match{(pace.Held > 0 ? ", " + pace.Held + " held change(s) since the last line not logged one by one" : "")})");
            pace.Wrote(now);
        }

        /// <summary>The factor o's health bar is drawn with: the shared fade factor for an enemy, 1 for the player's own and neutral objects
        /// (and whenever the rule is off).</summary>
        public static float BarAlpha(ZObject o) => o == null || world?.AreAllied(o.Owner, Live.Human) == true ? 1f : Live.Alpha(o.Owner);
        /// <summary>The rule hides o's health now (an enemy, no standing radar of the player's).</summary>
        public static bool Hides(ZObject o) => o != null && world?.AreAllied(o.Owner, Live.Human) != true && Live.Hides(o.Owner);
        /// <summary>o's health bar is not drawn this frame (an enemy at factor Hidden or under): the unit panel's NEEDS RADAR label goes with it.</summary>
        public static bool BarOff(ZObject o) => o != null && world?.AreAllied(o.Owner, Live.Human) != true && Live.BarOff(o.Owner);
        /// <summary>An enemy mark this frame: its bar drawn or held back (Play check counters only).</summary>
        public static void Note(ZObject o, bool drawn) { if (o == null || world?.AreAllied(o.Owner, Live.Human) == true || !OzRadarBarsRule.IsEnemy(o.Owner, Live.Human)) return; if (drawn) EnemyShown++; else EnemyHidden++; }
    }
}
