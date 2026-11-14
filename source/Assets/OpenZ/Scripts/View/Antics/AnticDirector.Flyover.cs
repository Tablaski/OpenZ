// OpenZ (GPL-3.0)
// ROBOT ANTICS UNDER THE BLUE FLYOVER.
// While a level-start flyover flies, the simulation is held (ZGame disabled: no ticks, nobody gets a head start), so no unit moves,
// turns or jostles; the robots stand in their calm idle loop, which reads as frozen from the flyover's distance. The antics director
// (phone, rocket-fuel drink, a turn, a look round, a neck roll) never ran there: it counts level PLAY time (none under the hold), needs
// 40 s of it and 12-20 s of idling, and sits out while the game object is off.
// Now, while a BLUE level's start flyover owns the camera and the game object is off (the hold), the director scans anyway, with the
// flyover's own numbers: a robot on screen that has stood idle FlyoverIdleMin s (+ its own jitter) may start one of the standing antics
// (the resting ones, fishing and the sunbed, keep their 5-minute rule, so they never come here); more may run at once and they start
// closer together, so the robots of BOTH teams the camera shows do their small things during the shots. Everything else is the
// director's normal way: the same clips, props and sounds, the same cancel on an order, a threat or a hit; after the hand-back the
// normal rules count again (an antic still running finishes or cancels as always). The RED start is not changed (FlyoverAnticsRed is
// off: the red briefing's flyover is as it was). View only: UnityEngine.Random, no sim writes, the sim hash and replays untouched.
// Item 100 (AnticDirector.FlyoverAll.cs): this scan rule fell short (0-2 starts a flight, blue robots first); under the hold the DRIVE
// there now gives EVERY standing robot the phone or the drink, on blue AND red starts (FlyoverAnticsRed on). The scan rule below stays
// as the fallback for FlyoverAll = false.
// Play check: AnticDirector.FlyoverStatus(); counts: FlyoverStarts, FlyoverScans.
using UnityEngine;

namespace OpenZ
{
    public sealed partial class AnticDirector
    {
        /// <summary>Antics under the blue flyover (true), and under the red briefing's flyover too (true since item 100: the same frozen look was wrong there; the red start's hold, lines and shots are untouched).</summary>
        public static bool FlyoverAntics = true, FlyoverAnticsRed = true;
        /// <summary>Under the flyover: idle seconds before a robot may start (+ 0..FlyoverIdleJitter of its own), the mean wait of the roll,
        /// the least gap between two starts, how many may run at once per team and on screen.</summary>
        public static float FlyoverIdleMin = 1.5f, FlyoverIdleJitter = 2.5f, FlyoverMeanWait = 2.5f, FlyoverStartGap = 0.9f;
        public static int FlyoverMaxPerTeam = 4, FlyoverMaxOnScreen = 4;
        /// <summary>Counts: antics started under a flyover's hold, scans run there.</summary>
        public static int FlyoverStarts, FlyoverScans;
        static bool flyoverNow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFlyover() { FlyoverStarts = FlyoverScans = 0; flyoverNow = false; }

        /// <summary>A level-start flyover owns the camera while the game object is off: the flyover's rules apply to the scan.</summary>
        public static bool FlyoverHold
        {
            get
            {
                if (!FlyoverAntics) return false;
                var g = ZGame.I; var fly = ZodFlyover.Live;
                if (!g || fly == null || !fly.Active || g.isActiveAndEnabled) return false;
                return OzPlayBlue.Active || FlyoverAnticsRed;
            }
        }

        /// <summary>Update's gate: true = no scan this time. Under the flyover's hold only the game-over and pause rules stay; the play-time
        /// rule and the game object's state do not (the sim is held on purpose).</summary>
        bool ScanBlocked()
        {
            flyoverNow = FlyoverHold;
            if (flyoverNow)
            {
                FlyoverScans++;
                return game.paused || game.World.GameOver || Time.time - lastStart < FlyoverStartGap;
            }
            return game.paused || !game.isActiveAndEnabled || game.World.GameOver || playT < MinLevelTime || Time.time - lastStart < MinStartGap;
        }

        float IdleNeed(int id) => flyoverNow ? FlyoverIdleMin + FlyoverIdleJitter * Hash01(id) : IdleMin + IdleJitter * Hash01(id);
        float MeanWaitNow => flyoverNow ? FlyoverMeanWait : MeanWait;
        int MaxOnScreenNow => flyoverNow ? FlyoverMaxOnScreen : MaxOnScreen;
        int MaxPerTeamNow => flyoverNow ? FlyoverMaxPerTeam : MaxPerTeam;

        /// <summary>Started: one more antic under the hold.</summary>
        void NoteFlyoverStart() { if (flyoverNow) FlyoverStarts++; }

        /// <summary>Play check: one line.</summary>
        public static string FlyoverStatus() =>
            $"flyover antics {(FlyoverAntics ? "on" : "OFF")} (red too: {FlyoverAnticsRed}), hold now {FlyoverHold}, scans under a hold {FlyoverScans}, starts under a hold {FlyoverStarts}; "
            + $"idle {FlyoverIdleMin:F1}+{FlyoverIdleJitter:F1} s, mean wait {FlyoverMeanWait:F1} s, gap {FlyoverStartGap:F1} s, at most {FlyoverMaxPerTeam} a team / {FlyoverMaxOnScreen} on screen (the scan rule, the fallback); " + FlyoverStatus2();
    }
}
