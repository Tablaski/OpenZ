// OpenZ (GPL-3.0)
// EVERY ROBOT PLAYS AN ANTIC UNDER A LEVEL-START FLYOVER.
// Why they read frozen: under the flyover the simulation is held (ZGame disabled), so nothing walks, turns or jostles, and the robots
// stand in the calm idle loop. That loop animates (the Animator runs on real time, its state time advances: the item 97 proof counted
// it), but it is a slow head turn and breathing: measured on the clip (idle_calm_humanoid, the idle state's motion), the head turn swings
// 0.62 of its muscle range (about 25 deg), every other muscle under 0.14 (the chest about 2 deg); from the flyover's distances (74-104 m at
// a 28 deg field of view: 21-29 px a metre) that is 1-2 px of visor in 3.7 s and under half a pixel of body. The item 97 rule
// (AnticDirector.Flyover.cs) started the normal antics under the hold with the director's own scan: one start a scan at most, 4 a team,
// 4 on screen, 1.5-4 s of idling first, an 18 % chance a scan, the player's team preferred: 0-2 starts a flight, blue robots first.
// Now, while a level-start flyover owns the camera and the game object is off (FlyoverHold: a blue start, and a red start too), the
// director DRIVES the antics instead of scanning: every standing humanoid robot of both teams (not in a vehicle or a building, not the
// fort defender in his hatch, not a hero squad's held lie, not one the player holds selected or one with an enemy near) plays the phone
// or the rocket-fuel drink, and nothing else:
//  * the first antic of every robot begins at once, a budget of robots a frame (everyone within FlyoverFirstWindow), PRE-ROLLED: a random
//    way into a random segment of the clip (AnticRun.FlyoverPreroll), the weapon simply away and the prop simply in the hand, no pop and
//    no sound (nothing happens: the camera finds a robot already at it), so no two neighbours are in step and no burst of pops opens the
//    flight; the events from there on (the beeps, the glugs, the crush, the throw, the put-away pops) play as always, on screen (the sound
//    director drops off-screen sounds), quiet at the flyover's distances;
//  * the kinds alternate between neighbouring robots (consecutive ids: a squad is made in one go) and along each robot's chain, so every
//    group shows both; a hero squad's member drinks only (its own rule, OzHeroAntics);
//  * when an antic ends, its robot gets the next one on the following frame (the weapon stays stowed between two hand antics: no rifle
//    popping in and out for a tenth of a second), so no robot stands without an antic for more than a frame (FlyoverLongestGap measures
//    it against FlyoverRestartGap); a robot that cannot start (selected, a threat near, busy) is asked again every FlyoverRetry s;
//  * FlyoverEndLead s before the hand-back (ZodFlyover.SecondsToHome) every antic ends through the antic's own cancel: the props pop
//    away, the weapons pop back, the idle fades in, so the player gets control with no prop in a hand and no robot in a pose; a flight
//    that ends without the signal ends them on the frame after;
//  * the thrown cans' live cap is FlyoverCanCap for the hold (8 in play), so a can is not popped away a second after it lands;
//  * the props are pooled (AnticProps); the log is one line a second and a summary at the end, not a line a start (150 log lines in one
//    frame would be a hitch); the cost is measured (FlyoverDriveMsMax / FlyoverLateMsMax, FlyoverStatus2).
// The busy rules of the held sim that are no motion the player sees (an order, a squad order, a sim velocity) stand down while the drive
// runs (UnitView.Antics.cs AnticBusyReason: SimHeldForFlyover); the drawn body's own walking, a hit, a selection, a threat, a stance, a grab
// and the fort defender still count. After the hand-back the normal rules count again (the antic that ended at the hand-back leaves the
// usual cooldown). The red start's hold, lines and shots are untouched. View only: UnityEngine.Random, no sim write, the sim hash and
// replays untouched. Play check: AnticDirector.FlyoverStatus2(); the blue flyover proof's part 3 (OzBlueFlyoverProof.Checks3.cs).
using System.Collections.Generic;
using System.Diagnostics;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class AnticDirector
    {
        /// <summary>The drive: every robot plays under a level-start flyover's hold (false = the item 97 scan rule of AnticDirector.Flyover.cs).</summary>
        public static bool FlyoverAll = true;
        /// <summary>The first starts cover every robot within this long (s) after the hold begins, at most FlyoverStartsPerFrame a frame.</summary>
        public static float FlyoverFirstWindow = 0.3f;
        public static int FlyoverStartsPerFrame = 24;
        /// <summary>A robot's first antic under the hold begins up to this far into a clip segment (0..1 of the segment's length).</summary>
        public static float FlyoverPrerollMax = 0.85f;
        /// <summary>The bound on a robot's wait between two antics under the hold (s): the next one starts on the following frame; the proof reads FlyoverLongestGap against it.</summary>
        public static float FlyoverRestartGap = 0.3f;
        /// <summary>The antics end this long (s) before the hand-back: the props are away as the player gets control.</summary>
        public static float FlyoverEndLead = 0.45f;
        /// <summary>A robot that could not start (busy, a threat near, selected) is asked again after this long (s).</summary>
        public static float FlyoverRetry = 0.25f;
        /// <summary>AnticCan.MaxLive while the drive runs (8 in play): many robots drink at once under the hold.</summary>
        public static int FlyoverCanCap = 40;
        /// <summary>A hero squad member whose level entry starts it on a lounger (DV108, OzHeroSquadView) is left to its start camp this long (s)
        /// after the hold begins: it lies down there; only a member the camp never laid down drinks.</summary>
        public static float FlyoverLoungerGrace = 1.5f;
        /// <summary>The drive ignores the held simulation's threat rule too (an enemy standing in the distance cannot shoot under the hold; every antic ends before the hand-back as always).</summary>
        public static bool FlyoverIgnoresThreats = true;
        /// <summary>Checks: this hold's refusals by their reason, the latest first start (s after the hold began) and how many first starts came later than FlyoverFirstWindow.</summary>
        public static readonly Dictionary<string, int> FlyoverRefusedBy = new Dictionary<string, int>();
        public static float FlyoverLatestFirstStart; public static int FlyoverLateStarts;

        // counts (the Play proof, FlyoverStatus2)
        public static int FlyoverRobots, FlyoverPrerolled, FlyoverChained, FlyoverRefused, FlyoverEnded, FlyoverPhone, FlyoverDrink, FlyoverHeroDrinks, FlyoverRedRobots, FlyoverBlueRobots;
        public static float FlyoverLongestGap, FlyoverFirstCoveredAt = -1f, FlyoverDriveMsMax, FlyoverDriveMsSum, FlyoverLateMsMax; public static int FlyoverDriveFrames;
        /// <summary>The robots this hold (the last one that began) gave a first antic: all, red, blue (reset when a hold begins).</summary>
        public static int FlyoverHoldRobots, FlyoverHoldRed, FlyoverHoldBlue;
        public static readonly List<string> FlyoverLog = new List<string>();
        public static string FlyoverLastEnd = "no flyover drive yet";
        /// <summary>True while the drive owns a hold: UnitView's busy rules of the held sim (orders, sim velocity) stand down.</summary>
        public static bool SimHeldForFlyover => driveOn;
        /// <summary>AnticRun.Finish: a flyover antic that ends keeps the weapon stowed for the next one of its chain (never once the end has come).</summary>
        public static bool FlyoverChainsNow => driveOn && !ending;
        /// <summary>PropUpright: no per-frame note strings while many antics run.</summary>
        public static bool FlyoverQuietNotes => driveOn;
        /// <summary>The drive owns a hold right now.</summary>
        public static bool FlyoverDriving => driveOn;

        static bool driveOn, ending; static float driveSince = -1f, lastFlySay = -1f; static int canCapWas = -1; static AnticDirector driveOwner;
        readonly Dictionary<int, float> flyNextTry = new Dictionary<int, float>();
        readonly Dictionary<int, int> flyChain = new Dictionary<int, int>();
        readonly Dictionary<int, float> flyIdleSince = new Dictionary<int, float>();
        readonly Dictionary<int, AnticRun> flyKept = new Dictionary<int, AnticRun>();
        readonly Dictionary<int, float> flyFirstStart = new Dictionary<int, float>();
        readonly Dictionary<int, int> flyRefusals = new Dictionary<int, int>();
        readonly Dictionary<int, string> flyLastRefusal = new Dictionary<int, string>();
        readonly List<AnticRun> flyRuns = new List<AnticRun>(), flyTmp = new List<AnticRun>();
        static readonly Stopwatch flySw = new Stopwatch(), lateSw = new Stopwatch();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFlyoverAll()
        {
            driveOn = ending = false; driveSince = lastFlySay = -1f; canCapWas = -1; driveOwner = null;
            FlyoverRobots = FlyoverPrerolled = FlyoverChained = FlyoverRefused = FlyoverEnded = FlyoverPhone = FlyoverDrink = FlyoverHeroDrinks = FlyoverRedRobots = FlyoverBlueRobots = 0;
            FlyoverHoldRobots = FlyoverHoldRed = FlyoverHoldBlue = 0;
            FlyoverLongestGap = FlyoverDriveMsMax = FlyoverDriveMsSum = FlyoverLateMsMax = 0f; FlyoverFirstCoveredAt = -1f; FlyoverDriveFrames = 0;
            FlyoverLog.Clear(); FlyoverLastEnd = "no flyover drive yet";
            AliveReset(); FlyoverRefusedBy.Clear(); FlyoverLatestFirstStart = 0f; FlyoverLateStarts = 0;
        }

        /// <summary>Update, every frame before the scan: true while the drive owns the antics (a level-start flyover's hold).</summary>
        bool FlyoverDrive(float dt)
        {
            if (driveOn && driveOwner != this) DropStaleDrive();                                     // the level of the last drive was left under its hold
            bool hold = FlyoverAll && FlyoverHold;
            if (!hold) { if (driveOn) FlyoverRelease(); return false; }
            flySw.Restart();
            flyoverNow = true;
            float now = Time.time;
            if (!driveOn) FlyoverBegin(now);
            var fly = ZodFlyover.Live; float toHome = fly != null ? fly.SecondsToHome : -1f;
            if (!ending && toHome >= 0f && toHome <= FlyoverEndLead) FlyoverEndAll($"the hand-back comes in {toHome:F2} s");
            if (!ending)
            {
                int budget = FlyoverStartsPerFrame;
                foreach (var v in game.LiveViewValues)
                {
                    if (budget <= 0) break;
                    if (!v || !v.isActiveAndEnabled || v.Obj == null || v.Obj.Kind != ZKind.Robot || v.AnticActive || !v.CanAntic) continue;
                    int id = v.Obj.Id;
                    if (flyNextTry.TryGetValue(id, out float at) && now < at) continue;
                    string why = v.AnticBusyReason(); if (why == null && !FlyoverIgnoresThreats) why = ThreatReason(v);   // the held sim's soft rules stand down (UnitView.Antics.cs); every refusal is counted by its reason
                    if (why != null) { FlyRefuse(id, now, why); continue; }
                    flyChain.TryGetValue(id, out int n);
                    bool hero = OzHeroAntics.OwnRule(v.Obj);
                    if (hero && now - driveSince < FlyoverLoungerGrace && v.Obj.HeroFrom != null && v.Obj.HeroFrom.start == "lounger") continue;   // DV108: the start camp lays it on its lounger first
                    var kind = hero ? AnticKind.Drink : ((id + n) & 1) == 0 ? AnticKind.Phone : AnticKind.Drink;
                    var run = new AnticRun(v, kind, null);
                    bool pre = n == 0;
                    if (pre) run.FlyoverPreroll(FlyoverPrerollMax); else run.FlyoverChain();
                    if (flyKept.TryGetValue(id, out var prev)) { run.InheritStow(prev); flyKept.Remove(id); }
                    if (!v.AnticBegin(run)) { run.FlyoverUnstow(); FlyRefuse(id, now, "begin refused"); continue; }
                    Started(run); flyRuns.Add(run); flyChain[id] = n + 1; budget--;
                    if (pre)
                    {
                        FlyoverPrerolled++; FlyoverRobots++; FlyoverHoldRobots++;
                        FirstStarted(id, now - driveSince, v);                                              // when this robot's first antic came (the proof reads the latest and the late ones)
                        if (v.Obj.Owner == Team.Red) { FlyoverRedRobots++; FlyoverHoldRed++; } else if (v.Obj.Owner == Team.Blue) { FlyoverBlueRobots++; FlyoverHoldBlue++; }
                    }
                    else FlyoverChained++;
                    if (kind == AnticKind.Phone) FlyoverPhone++; else FlyoverDrink++;
                    if (hero) FlyoverHeroDrinks++;
                    if (flyIdleSince.TryGetValue(id, out float since)) { FlyoverLongestGap = Mathf.Max(FlyoverLongestGap, now - since); flyIdleSince.Remove(id); }
                    FlyNote($"{now:F2} {(pre ? $"preroll ({run.PrerollSeconds:F2} s into segment {run.PrerollStep + 1})" : "chain " + n)} {kind} on {v.Obj.Type} #{id} ({v.Obj.Owner}){(hero ? " hero" : "")}");
                }
                if (FlyoverFirstCoveredAt < 0f && budget > 0) FlyoverFirstCoveredAt = now - driveSince;   // the loop reached the end with budget left: every robot that can start has started
            }
            if (now - lastFlySay >= 1f) { lastFlySay = now; UnityEngine.Debug.Log("[OpenZ Antics] flyover drive: " + FlyoverStatus2()); }
            flySw.Stop(); float ms = (float)flySw.Elapsed.TotalMilliseconds;
            FlyoverDriveMsMax = Mathf.Max(FlyoverDriveMsMax, ms); FlyoverDriveMsSum += ms; FlyoverDriveFrames++;
            return true;
        }

        void FlyoverBegin(float now)
        {
            driveOn = true; ending = false; driveSince = now; lastFlySay = now - 1f; FlyoverFirstCoveredAt = -1f; driveOwner = this;
            FlyoverHoldRobots = FlyoverHoldRed = FlyoverHoldBlue = 0;
            flyNextTry.Clear(); flyChain.Clear(); flyIdleSince.Clear(); flyKept.Clear(); flyRuns.Clear();
            flyFirstStart.Clear(); flyRefusals.Clear(); flyLastRefusal.Clear(); FlyoverRefusedBy.Clear(); FlyoverLatestFirstStart = 0f; FlyoverLateStarts = 0;
            AliveReset(); AliveEnsure();                                                                // the alive layer on top of the clips (AnticDirector.FlyoverAlive.cs)
            canCapWas = AnticCan.MaxLive; AnticCan.MaxLive = Mathf.Max(canCapWas, FlyoverCanCap);
            FlyNote($"{now:F2} the hold begins ({(OzPlayBlue.Active ? "a blue start" : "a red start")}, level {(game ? game.level : "?")})");
            UnityEngine.Debug.Log("[OpenZ Antics] flyover drive: the hold begins, every standing robot gets the phone or the drink");
        }

        /// <summary>The hand-back comes (or came): every flyover antic ends through the antic's own cancel (props off, weapon back), no more starts.</summary>
        void FlyoverEndAll(string why)
        {
            ending = true;
            flyTmp.Clear(); flyTmp.AddRange(flyRuns);
            int n = 0;
            foreach (var r in flyTmp)
            {
                if (r == null || r.Finished || !r.View || r.View.Antic != r) continue;
                r.View.AnticCancel("the flyover ends: " + why, false); n++;
            }
            foreach (var kv in flyKept) if (kv.Value != null) kv.Value.FlyoverUnstow();
            flyKept.Clear();
            FlyNote($"{Time.time:F2} the end: {n} antics ended ({why})");
            UnityEngine.Debug.Log($"[OpenZ Antics] flyover drive: {n} antics ended, {why}");
        }

        /// <summary>The hold is over (the flight ended): the counts' summary, the can cap back.</summary>
        void FlyoverRelease()
        {
            if (!ending) FlyoverEndAll("the flight ended");
            if (canCapWas >= 0) AnticCan.MaxLive = canCapWas;
            canCapWas = -1;
            FlyoverLastEnd = $"the hold of {Time.time - driveSince:F1} s ended: " + FlyoverStatus2();
            driveOn = false; ending = false; flyoverNow = false;
            flyRuns.Clear(); flyIdleSince.Clear(); flyNextTry.Clear();
            UnityEngine.Debug.Log("[OpenZ Antics] flyover drive: " + FlyoverLastEnd);
        }

        /// <summary>The drive was left on by a director that is gone (its level was left or restarted under the hold): its flags, the can cap and
        /// the held-sim rule must not carry into this level (a left-over "ending" would keep every robot of the next hold from starting).</summary>
        void DropStaleDrive()
        {
            if (canCapWas >= 0) AnticCan.MaxLive = canCapWas;
            canCapWas = -1; driveOn = ending = false; flyoverNow = false; driveOwner = null;
            FlyoverLastEnd = "the level of the last drive was left under its hold";
            FlyNote($"{Time.time:F2} {FlyoverLastEnd}: its state dropped");
            UnityEngine.Debug.Log("[OpenZ Antics] flyover drive: " + FlyoverLastEnd + ", its state dropped");
        }

        void FlyRefuse(int id, float now, string why)
        {
            flyNextTry[id] = now + FlyoverRetry; FlyoverRefused++;
            FlyoverRefusedBy.TryGetValue(why, out int n); FlyoverRefusedBy[why] = n + 1;
            flyRefusals.TryGetValue(id, out int k); flyRefusals[id] = k + 1; flyLastRefusal[id] = why;
        }

        /// <summary>A robot's first antic under the hold began `at` s after the hold: the latest is kept, a late one (after FlyoverFirstWindow) is counted and noted with its refusals.</summary>
        void FirstStarted(int id, float at, UnitView v)
        {
            flyFirstStart[id] = at;
            FlyoverLatestFirstStart = Mathf.Max(FlyoverLatestFirstStart, at);
            if (at <= FlyoverFirstWindow + 0.05f + (OzHeroAntics.OwnRule(v.Obj) ? FlyoverLoungerGrace : 0f)) return;   // a hero squad member waits its lounger grace by design
            FlyoverLateStarts++;
            flyRefusals.TryGetValue(id, out int k); flyLastRefusal.TryGetValue(id, out string why);
            FlyNote($"{Time.time:F2} LATE first start {at:F2} s after the hold on {v.Obj.Type} #{id} ({v.Obj.Owner}{(v.Obj.IsMinion ? ", squad member" : "")}) after {k} refusals, the last for {why ?? "nothing"}");
        }

        /// <summary>Checks: this robot's first start under the hold (s after it began; -1 = none), its refusals and the last reason.</summary>
        public static float FlyoverFirstStartOf(int id) { if (!I) return -1f; return I.flyFirstStart.TryGetValue(id, out float t) ? t : -1f; }
        public static int FlyoverRefusalsOf(int id, out string lastWhy) { lastWhy = null; if (!I) return 0; I.flyLastRefusal.TryGetValue(id, out lastWhy); return I.flyRefusals.TryGetValue(id, out int n) ? n : 0; }
        public static string FlyoverRefusedByText() { var sb = new System.Text.StringBuilder(); foreach (var kv in FlyoverRefusedBy) sb.Append($" {kv.Key} {kv.Value};"); return sb.Length > 0 ? sb.ToString() : " none"; }
        /// <summary>The drive is in its ending: the antics end for the hand-back (the alive layer eases out, the proof's watch stops).</summary>
        public static bool FlyoverEnding => driveOn && ending;

        /// <summary>Started: a flyover antic under the drive is noted in the drive's own log (true = no director log line).</summary>
        bool FlyoverStarted(AnticRun r) => r != null && r.Flyover && driveOn;

        /// <summary>Stopped: a flyover antic ended (true = noted here, no director log line while the drive runs).</summary>
        bool FlyoverStopped(AnticRun r, bool cancelled, string reason)
        {
            if (r == null || !r.Flyover) return false;
            flyRuns.Remove(r);
            int id = r.View && r.View.Obj != null ? r.View.Obj.Id : -1;
            if (driveOn && id >= 0)
            {
                flyIdleSince[id] = Time.time;
                if (r.KeepsWeapon) flyKept[id] = r; else flyKept.Remove(id);
            }
            if (ending) FlyoverEnded++;
            FlyNote($"{Time.time:F2} {(cancelled ? "cancel" : "end")} {r.Kind} #{id} at {r.Age:F1} s: {reason}{(r.KeepsWeapon ? " (weapon kept for the next)" : "")}");
            return driveOn;
        }

        void FlyoverLateBegin() { if (driveOn) lateSw.Restart(); }
        void FlyoverLateEnd() { if (!driveOn) return; lateSw.Stop(); FlyoverLateMsMax = Mathf.Max(FlyoverLateMsMax, (float)lateSw.Elapsed.TotalMilliseconds); }

        static void FlyNote(string s) { if (FlyoverLog.Count >= 600) FlyoverLog.RemoveAt(0); FlyoverLog.Add(s); }

        /// <summary>Play check: one line.</summary>
        public static string FlyoverStatus2()
        {
            int running = I ? I.flyRuns.Count : 0;
            float mean = FlyoverDriveFrames > 0 ? FlyoverDriveMsSum / FlyoverDriveFrames : 0f;
            return $"drive {(FlyoverAll ? "on" : "OFF")}{(driveOn ? (ending ? ", holding (ending)" : ", holding") : "")}: robots {FlyoverRobots} (red {FlyoverRedRobots}, blue {FlyoverBlueRobots}), "
                 + $"pre-rolled {FlyoverPrerolled}, chained {FlyoverChained}, phone {FlyoverPhone}, drink {FlyoverDrink} (hero drinks {FlyoverHeroDrinks}), refused tries {FlyoverRefused}, "
                 + $"ended for the hand-back {FlyoverEnded}, running now {running}; longest wait between a robot's antics {FlyoverLongestGap:F2} s (bound {FlyoverRestartGap:F2}), "
                 + $"everyone covered {(FlyoverFirstCoveredAt >= 0f ? FlyoverFirstCoveredAt.ToString("F2") + " s" : "not yet")} after the hold began (bound {FlyoverFirstWindow:F2}); "
                 + $"cost: the drive {FlyoverDriveMsMax:F2} ms max / {mean:F2} ms mean a frame over {FlyoverDriveFrames} frames, the runs' late step {FlyoverLateMsMax:F2} ms max; {AnticProps.Status()}; "
                 + $"first starts: the latest {FlyoverLatestFirstStart:F2} s after the hold, {FlyoverLateStarts} later than the window; refused by:{FlyoverRefusedByText()} {FlyoverAliveStatus()}";
        }
    }
}
