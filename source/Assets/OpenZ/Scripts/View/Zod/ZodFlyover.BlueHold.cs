// OpenZ (GPL-3.0)
// The red start: Commander Zod's briefing holds the level-start sting, the announcer's start line and the robot's game-start line
// (AudioDirector.HoldStartLines = ZodBriefing.HoldingStart: true from the level's first frame until the briefing hands the level
// over), so they play when the player gets control. A level played as BLUE has no Commander Zod cameo (OzPlayBlue.NoStoryIn), so
// nothing held them: AudioDirector.Start played them 0.2-0.3 s into the level, under the blue flyover ("Combat zone operational",
// "Showtime, lads" / "Let's rock" at 3 s of the cutscene).
// Now AudioDirector.HoldStartLines is chained (the briefing's own hold stays first): in a blue level the lines are held
//  (a) while a flight owns the camera (ZodFlyover.Live: Begin to End; End is the hand-back, as ZodBriefing's phase Play), and
//  (b) from the level's first frame while a blue start flyover is ANNOUNCED and has not flown and ended yet (ExpectBlueStart, called
//      before the level loads or in the driver's Awake / Start, or the delegate BlueStartExpected): without this the lines would be
//      scheduled on the first frame, before the flight begins. Never longer than BlueHoldMaxSeconds after the level load (a flight
//      that never comes), never in a red level, never in the menu (OzPlayBlue.Active is off there).
// Who announces: the k75c runner (OzBlueFlyoverProof, before DEPLOY) and the blue briefing kit (its level director, in its Awake:
// ZodFlyover.ExpectBlueStart("blue briefing"); a driver that begins its flight in a Start of execution order -60, as ZodBriefing does,
// is covered by (a) alone, since AudioDirector.Start runs at order 40). FlybySky's level-load preload reads the same hold, so a blue
// start whose flight is announced gets the painted sky from its first frame too.
// View only: nothing of the simulation changes. Play check: ZodFlyover.BlueHoldStatus().
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    public sealed partial class ZodFlyover
    {
        /// <summary>The blue levels' start lines wait for the flyover's hand-back (false = as before: they play on the first frame).</summary>
        public static bool BlueStartHold = true;
        /// <summary>An announced blue start flyover holds the lines at most this long after the level load (real seconds); then they play.</summary>
        public static float BlueHoldMaxSeconds = 30f;
        /// <summary>A blue level director that will fly the start flyover answers true here (asked every frame while the lines wait).</summary>
        public static Func<bool> BlueStartExpected;
        /// <summary>Checks: how often the hold released the lines (the flight ended / the time ran out / nothing held).</summary>
        public static int BlueHoldsReleasedByFlight { get; private set; }
        public static int BlueHoldsTimedOut { get; private set; }
        public static string LastBlueHold = "no blue hold yet";

        static Func<bool> chainedHold, holdBefore;
        static bool announced, pendingAnnounce, flownThisLevel, timedOut, releasedLogged;
        static float levelLoadedAt = -1f;
        static string announcedWhy = "";
        static Scene holdScene;                                                                       // the level scene the clock above belongs to

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBlueHold()
        {
            announced = pendingAnnounce = flownThisLevel = timedOut = releasedLogged = false; levelLoadedAt = -1f; announcedWhy = "";
            chainedHold = holdBefore = null; BlueStartExpected = null; BlueHoldsReleasedByFlight = BlueHoldsTimedOut = 0; LastBlueHold = "no blue hold yet";
        }

        /// <summary>After every BeforeSceneLoad boot (ZodBriefing sets AudioDirector.HoldStartLines there): the blue hold is chained behind it.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void HookBlueHold()
        {
            SceneManager.sceneLoaded -= OnBlueHoldScene; SceneManager.sceneLoaded += OnBlueHoldScene;
            ChainHold();
        }

        static void ChainHold()
        {
            var cur = AudioDirector.HoldStartLines;
            if (cur != null && cur == chainedHold) return;                                        // chained already behind the same hold
            holdBefore = cur;
            chainedHold = () => BlueHolding() || (holdBefore != null && holdBefore());
            AudioDirector.HoldStartLines = chainedHold;
        }

        static void OnBlueHoldScene(Scene s, LoadSceneMode m)
        {
            if (m != LoadSceneMode.Single) return;
            ChainHold();                                                                          // a boot order change can never leave the blue hold out
            var g = ZGame.I;
            bool level = g && g.gameObject.scene == s;
            flownThisLevel = false; timedOut = false; releasedLogged = false;
            if (level && OzPlayBlue.Active)
            {
                levelLoadedAt = Time.realtimeSinceStartup; holdScene = s;
                announced = pendingAnnounce; pendingAnnounce = false;
                if (announced) { LastBlueHold = g.level + ": the start lines wait for the blue flyover (" + announcedWhy + ")"; Debug.Log("[ZodFlyover] " + LastBlueHold); }
            }
            else { announced = pendingAnnounce = false; levelLoadedAt = -1f; }
        }

        /// <summary>A blue start flyover is coming: called before the level loads (the next blue level's lines wait from its first frame) or
        /// inside the level before AudioDirector's Start (a director's Awake / Start). `why` is written to the log. A call during a new
        /// level's Awake (its sceneLoaded not run yet) is kept for that level.</summary>
        public static void ExpectBlueStart(string why)
        {
            announcedWhy = why ?? "";
            var g = ZGame.I;
            if (g && OzPlayBlue.Active && levelLoadedAt >= 0f && g.gameObject.scene == holdScene) { announced = true; LastBlueHold = g.level + ": the start lines wait for the blue flyover (" + announcedWhy + ")"; Debug.Log("[ZodFlyover] " + LastBlueHold); }
            else pendingAnnounce = true;
        }

        /// <summary>Begin (ZodFlyover.Blue.cs BlueBeforeBegin): a flight of this blue level begins: its End releases the lines.</summary>
        static void BlueHoldBegan()
        {
            if (!OzPlayBlue.Active) return;
            flownThisLevel = true;
        }

        /// <summary>The chained AudioDirector.HoldStartLines: true while the blue level's start lines must wait.</summary>
        static bool BlueHolding()
        {
            if (!BlueStartHold || !OzPlayBlue.Active) return false;
            var g = ZGame.I; if (!g) return false;
            var live = Live;
            if (live != null && live.Active && live.game == g) return true;                      // a flight owns the camera: until its End
            if (flownThisLevel)
            {   // the flight of this level has ended (its End put Live down): the hand-back, the lines come now
                if (!releasedLogged) { releasedLogged = true; BlueHoldsReleasedByFlight++; LastBlueHold = g.level + ": the blue flyover handed back, the start lines play now"; Debug.Log("[ZodFlyover] " + LastBlueHold); }
                return false;
            }
            bool expected = announced || pendingAnnounce;
            if (!expected && BlueStartExpected != null) { try { expected = BlueStartExpected(); } catch (Exception) { expected = false; } }
            if (!expected) return false;
            if (timedOut) return false;
            if (levelLoadedAt >= 0f && Time.realtimeSinceStartup - levelLoadedAt > BlueHoldMaxSeconds)
            {
                timedOut = true; BlueHoldsTimedOut++;
                LastBlueHold = g.level + ": no blue flyover came within " + BlueHoldMaxSeconds.ToString("0") + " s of the level load: the start lines play now";
                Debug.LogWarning("[ZodFlyover] " + LastBlueHold);
                return false;
            }
            return true;
        }

        /// <summary>Play check: one line on the blue hold.</summary>
        public static string BlueHoldStatus() =>
            $"blue start hold {(BlueStartHold ? "on" : "OFF")}: holding {(AudioDirector.HoldStartLines != null && AudioDirector.HoldStartLines() ? "YES" : "no")}, announced {announced || pendingAnnounce}, "
            + $"a flight began in this level {flownThisLevel}, live flight {(Live != null && Live.Active ? "yes" : "no")}, chained {(chainedHold != null && AudioDirector.HoldStartLines == chainedHold ? "yes" : "NO")}; "
            + $"released by a flight {BlueHoldsReleasedByFlight}, timed out {BlueHoldsTimedOut}; last: {LastBlueHold}";
    }
}
