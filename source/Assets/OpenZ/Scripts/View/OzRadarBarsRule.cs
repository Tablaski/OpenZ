using System;
using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// no engine types, so the headless check (Editor/OzV14RadarBarsCount.cs) and the kit's off-Unity harness run the very same code as the game.
    /// The game's side (the radar truth, the switches, the clocks) is View/OzRadarBars.cs; it feeds one instance of this class once per frame.
    /// <list type="bullet">
    /// <item>The rule: while the human player's side owns no standing (not ruined) radar, the health bars of ENEMY objects are not shown. Enemy =
    /// owned by a team that is not the player's and not Neutral; the player's own objects and the neutral ones (empty vehicles and guns, neutral
    /// buildings, wrecks, obstacles) keep their bars as before. The radar truth is the one the minimap's enemy dots (DV33) and the strike-group
    /// warning use (OzStrikeWarning.OwnsRadar): it comes in as <c>owns</c> (<see cref="Raw"/>).</item>
    /// <item>A held change: the rule follows a radar gained or lost once the truth has stayed so for <see cref="HoldSec"/> s of game time
    /// (<see cref="Owns"/>). The sim's capture check runs every 0.2 s and gives a contested flag to the first unit of the other team on it
    /// bar would pulse while that lasts. The same truth, only held: never a second count of radars. Game time, so the pause (the sim clock
    /// stops) holds the wait too, and at any game speed the hold is three of the sim's capture checks.</item>
    /// <item>Off (every bar as before, at once, no hold): DV33's switch off (PlayerPrefs oz_radar_dots 0), the Unit editor, 'Zod exact' (the Zod
    /// Engine shows an enemy's health on mouse-over with no radar condition: zplayer.cpp:2178-2189, zobject.cpp:555-608, 611-700), a match
    /// without a human side.</item>
    /// <item>No pop: one shared factor <see cref="K"/> for every enemy bar. Its progress <see cref="P"/> moves toward 1 (bars shown) or 0 (hidden)
    /// by at most dt / FadeSeconds a frame, dt the frame's unscaled time capped at <see cref="MaxStep"/> (a hitch or the first frame after a
    /// pause moves it no further than 0.1 s would), and K = smootherstep(P) = 6P^5 - 15P^4 + 10P^3. A fade that turns (the switch flipped half
    /// way) goes on from where it is. A new match starts at its end state (no fade at the start).</item>
    /// </list>
    /// View only: nothing here is read by the rules or the computer player.
    /// </summary>
    public sealed class OzRadarBarsRule
    {
        /// <summary>The fade (s, unscaled time): 0.35 s, always (the lead's decision 6; CALM MODE does not shorten it: a fade, not a flash).</summary>
        public const float FadeSeconds = 0.35f;
        /// <summary>The longest frame step the fade takes (s): a hitch or an unpause never jumps.</summary>
        public const float MaxStep = 0.1f;
        /// <summary>A radar gained or lost counts once the truth has stayed so this long (game seconds): three of the sim's 0.2 s capture checks,
        /// so a contested radar flag (a new owner every 0.2 s) never makes the enemy bars pulse.</summary>
        public const double HoldSec = 0.6;
        /// <summary>At or under this factor an enemy bar is not drawn at all (no layout, no draw).</summary>
        public const float Hidden = 0.004f;
        /// <summary>The unit panel's small health label while it shows an enemy without a radar (the label style of UI/OpenZHud.HeroRegen.cs), and
        /// its short form where the long one would reach the health value (measured once by the panel, as the hero line does).</summary>
        public const string LabelNeedsRadar = "HEALTH · NEEDS RADAR", LabelNeedsRadarShort = "NEEDS RADAR", LabelHealth = "HEALTH";

        /// <summary>What <see cref="Advance"/> saw change this frame (the game logs these, nothing else).</summary>
        public const int NoChange = 0, NewMatch = 1, RuleChanged = 2, RadarGained = 3, RadarLost = 4;

        /// <summary>The fade's progress 0..1 (1 = enemy bars shown) and the drawn factor K = smootherstep(P).</summary>
        public float P { get; private set; } = 1f;
        public float K { get; private set; } = 1f;
        /// <summary>The rule acts (null <see cref="Why"/>); the player's side owns a standing radar as the rule follows it (held); the player's team.</summary>
        public bool RuleOn { get; private set; }
        public bool Owns { get; private set; }
        public Team Human { get; private set; } = Team.Red;
        /// <summary>The radar truth of this frame as it came in (not held; false while the rule is off).</summary>
        public bool Raw { get; private set; }
        /// <summary>The truth differs from <see cref="Owns"/> and waits for its hold (since game time <see cref="PendingSince"/>).</summary>
        public bool Pending { get; private set; }
        public double PendingSince { get; private set; }
        /// <summary>How often the truth changed while the rule acted (this instance's life; a contested flag counts 5 a second).</summary>
        public int Flips { get; private set; }
        /// <summary>Why the rule is off (null = on).</summary>
        public string Why { get; private set; } = "no match yet";
        bool started;

        public void Reset() { P = 1f; K = 1f; RuleOn = false; Owns = false; Raw = false; Pending = false; PendingSince = 0; Flips = 0; Human = Team.Red; Why = "no match yet"; started = false; }

        /// <summary>One frame. why = the off reason (null = the rule acts), owns = the player's side owns a standing radar (the truth, this
        /// frame), dt = the frame's unscaled time, fade = the fade's length (s), fresh = a new match (snap to the end state), now = the game time
        /// (s; the sim clock, stopped in the pause; NaN or infinite = no clock: a change counts at once). Returns what changed (the constants).</summary>
        public int Advance(string why, bool owns, Team human, float dt, float fade, bool fresh, double now)
        {
            bool on = why == null, raw = on && owns;
            int ch = NoChange;
            if (fresh || !started) { ch = NewMatch; Owns = raw; Pending = false; }
            else if (on != RuleOn) { ch = RuleChanged; Owns = raw; Pending = false; }                    // a switch: at once (the fade still eases it)
            else if (on)
            {
                if (raw != Raw) Flips++;
                if (raw == Owns) Pending = false;                                                         // back as it was before the hold ran out: nothing happened
                else
                {
                    bool clock = !double.IsNaN(now) && !double.IsInfinity(now);
                    if (!Pending || !clock || !(now >= PendingSince)) { Pending = true; PendingSince = clock ? now : 0; }
                    if (!clock || now - PendingSince >= HoldSec) { Owns = raw; Pending = false; ch = raw ? RadarGained : RadarLost; }
                }
            }
            started = true; RuleOn = on; Raw = raw; Why = why; Human = human;
            float target = !on || Owns ? 1f : 0f;
            P = ch == NewMatch ? target : Step(P, target, dt, fade);
            K = Ease(P);
            return ch;
        }

        /// <summary>The factor an object's health bar is drawn with: K for an enemy, 1 for the player's own and neutral objects.</summary>
        public float Alpha(Team owner) => IsEnemy(owner, Human) ? K : 1f;
        /// <summary>The rule hides this owner's health now (the bar fades out or is gone; the label waits for the bar: <see cref="BarOff"/>).</summary>
        public bool Hides(Team owner) => RuleOn && !Owns && IsEnemy(owner, Human);
        /// <summary>This owner's health bar is not drawn this frame (an enemy at factor <see cref="Hidden"/> or under): the unit panel's label reads
        /// NEEDS RADAR exactly then, so the label and the bar switch in the same frame (after the fade out, and as the fade in begins).</summary>
        public bool BarOff(Team owner) => !(Alpha(owner) > Hidden);

        /// <summary>The enemy test of the minimap's dots (UI/OpenZMinimap.cs: o.Owner != hud.Player &amp;&amp; o.Owner != Team.Neutral).</summary>
        public static bool IsEnemy(Team owner, Team human) => owner != Team.Neutral && owner != human;

        /// <summary>smootherstep: 6t^5 - 15t^4 + 10t^3 (0 at 0, 1/2 at 1/2, 1 at 1, flat at both ends, symmetric).</summary>
        public static float Ease(float t)
        {
            if (!(t > 0f)) return 0f;
            if (t >= 1f) return 1f;
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        /// <summary>p moved toward target by dt / seconds (dt capped at MaxStep; a NaN or negative dt does not move it; seconds 0 = at once).</summary>
        public static float Step(float p, float target, float dt, float seconds)
        {
            if (!(seconds > 0f)) return target;
            if (!(dt > 0f)) dt = 0f; else if (dt > MaxStep) dt = MaxStep;
            float d = dt / seconds;
            return p < target ? Math.Min(target, p + d) : Math.Max(target, p - d);
        }

        /// <summary>Why the rule is off (null = it acts), in the order the switches are asked.</summary>
        public static string WhyOff(bool radarDotsSwitch, bool unitEditor, bool zodExact, bool humanSide)
        {
            if (!radarDotsSwitch) return "DV33's switch is off (PlayerPrefs oz_radar_dots 0: every unit and every bar shown)";
            if (unitEditor) return "the Unit editor";
            if (zodExact) return "'Zod exact' (the Zod Engine shows an enemy's health on mouse-over with no radar)";
            if (!humanSide) return "no human side (a computer match)";
            return null;
        }

        /// <summary>The unit panel's small health label: NEEDS RADAR while the shown unit's bar is off (<see cref="BarOff"/>).</summary>
        public static string PanelLabel(bool barOff) => barOff ? LabelNeedsRadar : LabelHealth;

        /// <summary>The log's pace (no engine types, so the check counts it): a line at most every <see cref="Every"/> s; changes in between
        /// are held and named in the next line; once the time is up, the held changes get their line (the state a burst ended in).</summary>
        public sealed class LogPace
        {
            public readonly float Every;
            float next = float.NegativeInfinity;
            /// <summary>Changes since the last line that got no line of their own.</summary>
            public int Held { get; private set; }
            public LogPace(float every) { Every = every; }
            public void Reset() { next = float.NegativeInfinity; Held = 0; }
            /// <summary>A change at time now (force: the match's first line): true = write its line now (then <see cref="Wrote"/>).</summary>
            public bool Change(float now, bool force) { if (force || !(now < next)) return true; Held++; return false; }
            /// <summary>A frame without a change: true = write the line of the held changes now (then <see cref="Wrote"/>).</summary>
            public bool Quiet(float now) => Held > 0 && !(now < next);
            public void Wrote(float now) { next = now + Every; Held = 0; }
        }
    }
}
