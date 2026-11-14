#if UNITY_EDITOR
// OpenZ (GPL-3.0)
// The second pass: the measures that disagreed with the drive's own counters now say WHEN and WHY). Hooked into
// OzBlueFlyoverProof: Begin3 when the level has loaded, Watch3 every frame of the flight, HandedBack3 at the hand-back, Checks3 before the
// report, TraceLines3 in the frame traces.
//  WINDOW       the watch counts from the first frame the drive owns the hold (the frames of the flight before it are counted apart: 0 in the
//               game, where the briefing and the drive begin on the level's first frame; a frame or two here, where the proof's own flight
//               begins in a coroutine) to the drive's ending (FlyoverEndLead s before the hand-back, when every antic ends by design).
//  POSE         every frame, every robot inside the visible picture: a signature of its pose (the local rotations of the head, the hands, the
//               spine and the forearms; a Generic rig's clip time) and the longest span it did not change while it stayed in the picture: NO
//               robot still for StillWindow s or more; the worst ones are named with WHEN (s after the hold), in which clip step, in an antic
//               or not, the skin drawn or not, in which shot. The pose is read at the END of the frame (PoseTap3), as it is drawn: the proof's
//               LateUpdate runs at execution order 0, after the Animator wrote the clip's pose but BEFORE the layers that pose the bones on top
//               of it (RobotLegIK 70, WeaponHold 80, the gestures 95, the flyover's alive layer 96), so a read there sees the bare clip.
//  ANTICS       every antic-capable robot the camera showed was in an antic for AnticShare of its frames in the window (the short ones named
//               with their first antic's time and the drive's own record: the first start, the refusals and the last reason); every first
//               start within FlyoverFirstWindow of the hold, the refusals by reason; the drive covered every standing robot of both teams;
//               only the phone and the drink, mixed (each at least a quarter of the starts, no other kind).
//  HAND-BACK    HandBackGrace s after the hand-back: no robot in a flyover antic, no phone or can in a hand, no weapon left hidden.
//  COST         the drive's time a frame (mean and max) and the runs' late step (max); the prop pool's counts; the alive layer's counts.
//  SOUNDS       the antics' own sounds under the hold, played (on screen) and dropped, by key.
// Test only (editor). The checks join the proof's PASS / FAIL list; the numbers go to the report.
using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzBlueFlyoverProof
    {
        public const string Part3 = "k75c3b";
        /// <summary>No robot in the picture may keep one pose this long (s); an antic-capable robot is in an antic for this share of its frames in the
        /// window; the leftovers are counted this long after the hand-back (s); the drive must own the hold within this many frames of the flight.</summary>
        public static float StillWindow = 0.5f, AnticShare = 0.95f, HandBackGrace = 0.6f;
        public static int DriveWithinFrames = 3;

        sealed class PoseWatch
        {
            public float sig, changedAt, lastSeen = -1f, longestStill, idleSince = -1f, longestIdle, firstSeen = -1f, firstAntic = -1f, stillFrom = -1f;
            public int frames, anticFrames, stillShot = -1;
            public bool capable, stillAntic, stillDrawn; public string type, stillStep = "-"; public Team team; public Transform[] bones; public SkinnedMeshRenderer skin;
        }
        readonly Dictionary<int, PoseWatch> poses = new Dictionary<int, PoseWatch>();
        int robots0, pre0, chain0, phone0, drink0, red0, blue0, refused0, ended0, hero0; readonly int[] startsBy0 = new int[7];
        float handBack3 = -1f, hold3 = -1f; int leftInAntic = -1, leftProps = -1, leftWeaponsHidden = -1, framesBeforeHold3, framesEnding3; string leftWho = "";
        int watchFrame3 = -1, watchShot3 = -1, tapped3; bool tap3On;

        // ================================================================================================================ hooks
        void Begin3()
        {
            poses.Clear(); handBack3 = -1f; hold3 = -1f; framesBeforeHold3 = framesEnding3 = 0; leftInAntic = leftProps = leftWeaponsHidden = -1; leftWho = "";
            watchFrame3 = watchShot3 = -1; tapped3 = 0;
            robots0 = AnticDirector.FlyoverRobots; pre0 = AnticDirector.FlyoverPrerolled; chain0 = AnticDirector.FlyoverChained; phone0 = AnticDirector.FlyoverPhone; drink0 = AnticDirector.FlyoverDrink;
            red0 = AnticDirector.FlyoverRedRobots; blue0 = AnticDirector.FlyoverBlueRobots; refused0 = AnticDirector.FlyoverRefused; ended0 = AnticDirector.FlyoverEnded; hero0 = AnticDirector.FlyoverHeroDrinks;
            for (int i = 0; i < 7; i++) startsBy0[i] = AnticDirector.StartsBy[i];
            AnticDirector.FlyoverLongestGap = 0f; AnticDirector.FlyoverDriveMsMax = AnticDirector.FlyoverDriveMsSum = AnticDirector.FlyoverLateMsMax = 0f; AnticDirector.FlyoverDriveFrames = 0;   // this run's measures
            Log(Part3 + ": " + AnticDirector.FlyoverStatus2());
        }

        /// <summary>Every frame of the flight (the proof's LateUpdate): the frame and its shot are noted; PoseTap3 reads the poses at the frame's end.</summary>
        void Watch3(int shot)
        {
            watchFrame3 = Time.frameCount; watchShot3 = shot;
            if (!tap3On) { tap3On = true; StartCoroutine(PoseTap3()); }
        }

        /// <summary>At the end of every frame the proof watched (after every LateUpdate and the frame's drawing): the poses as drawn.</summary>
        IEnumerator PoseTap3()
        {
            var end = new WaitForEndOfFrame();
            while (true)
            {
                yield return end;
                if (watchFrame3 == Time.frameCount) { tapped3++; Sample3(watchShot3); }
            }
        }

        /// <summary>The pose of every robot in the visible picture, from the drive's first frame to its ending.</summary>
        void Sample3(int shot)
        {
            if (!cam || !game) return;
            if (!AnticDirector.FlyoverDriving) { if (hold3 < 0f) framesBeforeHold3++; return; }    // the window begins with the drive's first frame
            float now = Time.time;
            if (hold3 < 0f) hold3 = now;
            if (AnticDirector.FlyoverEnding) { framesEnding3++; return; }                          // ... and ends with the drive's ending: every antic ends there by design
            foreach (var v in game.Views.Values)
            {
                if (!v || v.Obj == null || v.Obj.Kind != ZKind.Robot || v.Obj.Destroyed || v.Dying) continue;
                var vp = cam.WorldToViewportPoint(v.transform.position);
                if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < BarBottom || vp.y > 1f - BarTop) continue;
                var an = v.GetComponentInChildren<Animator>(); if (!an || !an.isInitialized) continue;
                int id = v.Obj.Id;
                if (!poses.TryGetValue(id, out var w))
                {
                    w = new PoseWatch { type = v.Obj.Type, team = v.Obj.Owner, capable = v.CanAntic, skin = v.GetComponentInChildren<SkinnedMeshRenderer>() };
                    if (an.isHuman) w.bones = new[] { an.GetBoneTransform(HumanBodyBones.Head), an.GetBoneTransform(HumanBodyBones.LeftHand), an.GetBoneTransform(HumanBodyBones.RightHand),
                                                      an.GetBoneTransform(HumanBodyBones.Spine), an.GetBoneTransform(HumanBodyBones.LeftLowerArm), an.GetBoneTransform(HumanBodyBones.RightLowerArm) };
                    poses[id] = w;
                }
                float sig = Signature(w, an);
                bool fresh = w.lastSeen < 0f || now - w.lastSeen > 0.2f;                           // (re)entered the picture (a cut): its windows start anew
                if (w.firstSeen < 0f) w.firstSeen = now - hold3;
                if (fresh || Mathf.Abs(sig - w.sig) > 1e-4f) w.changedAt = now;
                else if (now - w.changedAt > w.longestStill)
                {   // the longest still span so far: when it began (s after the hold), in which clip step, in an antic or not, drawn or not, which shot
                    w.longestStill = now - w.changedAt; w.stillFrom = w.changedAt - hold3; w.stillShot = shot;
                    w.stillAntic = v.AnticActive; w.stillDrawn = w.skin && w.skin.isVisible; w.stillStep = v.Antic != null ? v.Antic.Step : "idle";
                }
                w.sig = sig; w.lastSeen = now; w.frames++;
                if (v.AnticActive) { w.anticFrames++; if (w.firstAntic < 0f) w.firstAntic = now - hold3; if (w.idleSince >= 0f) { w.longestIdle = Mathf.Max(w.longestIdle, now - w.idleSince); w.idleSince = -1f; } }
                else if (w.capable) { if (fresh || w.idleSince < 0f) w.idleSince = now; else w.longestIdle = Mathf.Max(w.longestIdle, now - w.idleSince); }
            }
        }

        static float Signature(PoseWatch w, Animator an)
        {
            if (w.bones == null) return an.GetCurrentAnimatorStateInfo(0).normalizedTime;         // a Generic rig: its clip time
            float s = 0f; int k = 1;
            foreach (var b in w.bones) { if (!b) continue; var q = b.localRotation; s += (q.x * 1.7f + q.y * 2.3f + q.z * 3.1f + q.w) * k; k++; }
            return s;
        }

        bool IsMinion3(int id) => game != null && game.Views.TryGetValue(id, out var v) && v && v.Obj != null && v.Obj.IsMinion;

        /// <summary>The hand-back: the leftovers are counted HandBackGrace s later.</summary>
        void HandedBack3()
        {
            handBack3 = Time.time;
            ZodFlyover.SecondsToHandBack = null;
            StartCoroutine(LeftOvers3());
        }

        IEnumerator LeftOvers3()
        {
            float t0 = Time.time;
            while (Time.time - t0 < HandBackGrace) yield return null;
            int inAntic = 0, props = 0, hidden = 0; var who = new StringBuilder();
            if (game != null && game.Views != null)
                foreach (var v in game.Views.Values)
                {
                    if (!v || v.Obj == null || v.Obj.Kind != ZKind.Robot) continue;
                    if (v.AnticActive && v.Antic != null && v.Antic.Flyover) { inAntic++; who.Append(" #" + v.Obj.Id + " in " + v.Antic.Kind); }
                    foreach (var t in v.GetComponentsInChildren<Transform>(false))
                        if (t.name == "AnticPhone" || t.name == "AnticFuelCan" || t.name == "AnticFuelCanCrushed") { props++; who.Append(" #" + v.Obj.Id + " holds " + t.name); break; }
                    if (v.AnticActive) continue;
                    var wpn = AnticSockets.Weapon(v);
                    if (!wpn) continue;
                    bool any = false, on = false;
                    foreach (var r in wpn.GetComponentsInChildren<Renderer>(true)) { any = true; if (r.enabled) { on = true; break; } }
                    if (any && !on) { hidden++; who.Append(" #" + v.Obj.Id + " weapon hidden"); }
                }
            leftInAntic = inAntic; leftProps = props; leftWeaponsHidden = hidden; leftWho = who.ToString();
        }

        // ================================================================================================================ the checks
        IEnumerator Checks3()
        {
            if (!game) yield break;
            if (handBack3 >= 0f) { float w0 = Time.realtimeSinceStartup; while (leftInAntic < 0 && Time.realtimeSinceStartup - w0 < 6f) yield return null; }
            int robots = AnticDirector.FlyoverRobots - robots0, pre = AnticDirector.FlyoverPrerolled - pre0, chain = AnticDirector.FlyoverChained - chain0;
            int phone = AnticDirector.FlyoverPhone - phone0, drink = AnticDirector.FlyoverDrink - drink0, red = AnticDirector.FlyoverRedRobots - red0, blue = AnticDirector.FlyoverBlueRobots - blue0;
            int refused = AnticDirector.FlyoverRefused - refused0, ended = AnticDirector.FlyoverEnded - ended0, hero = AnticDirector.FlyoverHeroDrinks - hero0;
            // the pose rule and the antic share, between the drive's first frame and its ending
            int seen = 0, still = 0, capable = 0, played = 0, others = 0; float longestStill = 0f, longestIdle = 0f;
            var stillOnes = new List<KeyValuePair<int, PoseWatch>>(); var shortOnes = new List<KeyValuePair<int, PoseWatch>>();
            foreach (var kv in poses)
            {
                var w = kv.Value; if (w.frames < 10) continue;
                seen++; longestStill = Mathf.Max(longestStill, w.longestStill);
                if (w.longestStill >= StillWindow) { still++; stillOnes.Add(kv); }
                if (!w.capable) { others++; continue; }
                capable++; longestIdle = Mathf.Max(longestIdle, w.longestIdle);
                if (w.anticFrames >= AnticShare * w.frames) played++; else shortOnes.Add(kv);
            }
            stillOnes.Sort((a, b) => b.Value.longestStill.CompareTo(a.Value.longestStill));
            shortOnes.Sort((a, b) => (a.Value.anticFrames / (float)a.Value.frames).CompareTo(b.Value.anticFrames / (float)b.Value.frames));
            var stillWho = new StringBuilder(); int shown = 0;
            foreach (var kv in stillOnes)
            {
                var w = kv.Value;
                stillWho.Append($" #{kv.Key} {w.type} ({w.team}) {w.longestStill:F2} s from {w.stillFrom:F2} s in {w.stillStep} ({(w.stillAntic ? "in an antic" : "no antic")}, {(w.stillDrawn ? "drawn" : "not drawn")}, shot {w.stillShot + 1});");
                if (++shown >= 10) { if (stillOnes.Count > shown) stillWho.Append($" + {stillOnes.Count - shown} more"); break; }
            }
            var shortWho = new StringBuilder(); shown = 0;
            foreach (var kv in shortOnes)
            {
                var w = kv.Value; int refusals = AnticDirector.FlyoverRefusalsOf(kv.Key, out string why); float first = AnticDirector.FlyoverFirstStartOf(kv.Key);
                shortWho.Append($" #{kv.Key} {w.type} ({w.team}{(IsMinion3(kv.Key) ? ", squad member" : "")}) {Pct(w.anticFrames, w.frames)}, seen from {w.firstSeen:F2} s, first antic {(w.firstAntic >= 0f ? w.firstAntic.ToString("F2") + " s" : "never")}, the drive: first start {(first >= 0f ? first.ToString("F2") + " s" : "none")} after {refusals} refusals{(why != null ? ", the last for " + why : "")};");
                if (++shown >= 12) { if (shortOnes.Count > shown) shortWho.Append($" + {shortOnes.Count - shown} more"); break; }
            }
            Check(lv + $": no robot the camera showed kept one pose for {StillWindow:F1} s from the hold's first frame to the drive's ending ({seen} robots seen 10+ frames, the poses read as drawn at the end of {tapped3} frames, the longest still span {longestStill:F2} s{(still > 0 ? "; STILL:" + stillWho : "")})", seen > 0 && still == 0);
            Check(lv + $": every antic-capable robot the camera showed was in an antic through the flight ({played} of {capable} in one for {AnticShare:P0}+ of their frames from the hold's first frame to the drive's ending, the longest wait without one {longestIdle:F2} s{(shortWho.Length > 0 ? "; SHORT:" + shortWho : "")}; {others} robots without antic clips on their own idle)", capable > 0 && played == capable);
            Check(lv + $": every robot's first antic came within {AnticDirector.FlyoverFirstWindow:F2} s of the hold (the latest first start {AnticDirector.FlyoverLatestFirstStart:F2} s after it, {AnticDirector.FlyoverLateStarts} later than that; a hero squad member may wait its lounger grace; refused tries by reason:{AnticDirector.FlyoverRefusedByText()})",
                robots > 0 && AnticDirector.FlyoverLateStarts == 0);
            Check(lv + $": the drive owned the hold within the flight's first {DriveWithinFrames} frames ({framesBeforeHold3} frame(s) of the flight before it; 0 in the game, where the briefing and the drive begin on the level's first frame), its ending left {framesEnding3} frame(s) out of the watch", hold3 >= 0f && framesBeforeHold3 <= DriveWithinFrames);
            Check(lv + $": the drive gave every standing robot of both teams an antic ({robots} robots: red {red}, blue {blue}; pre-rolled {pre}, chained {chain}, refused tries {refused}, hero drinks {hero}); the longest wait between a robot's antics {AnticDirector.FlyoverLongestGap:F2} s (bound {AnticDirector.FlyoverRestartGap:F2}); everyone covered {AnticDirector.FlyoverFirstCoveredAt:F2} s after the hold began (bound {AnticDirector.FlyoverFirstWindow:F2})",
                robots > 0 && red > 0 && blue > 0 && AnticDirector.FlyoverLongestGap <= AnticDirector.FlyoverRestartGap && AnticDirector.FlyoverFirstCoveredAt >= 0f && AnticDirector.FlyoverFirstCoveredAt <= AnticDirector.FlyoverFirstWindow + 0.05f);
            int otherKinds = 0; for (int i = 0; i < 7; i++) if (i != (int)AnticKind.Phone && i != (int)AnticKind.Drink) otherKinds += AnticDirector.StartsBy[i] - startsBy0[i];
            int total = phone + drink;
            Check(lv + $": only the phone and the drink, mixed ({phone} phone, {drink} drink of {total} starts under the hold, each at least a quarter; other kinds {otherKinds})", total > 0 && phone > 0 && drink > 0 && Mathf.Min(phone, drink) >= 0.25f * total && otherKinds == 0);
            if (handBack3 >= 0f)
                Check(lv + $": the antics ended for the hand-back ({ended} ended by the drive); {HandBackGrace:F1} s after it no robot in a flyover antic ({leftInAntic}), no phone or can in a hand ({leftProps}), no weapon left hidden ({leftWeaponsHidden}){(leftWho.Length > 0 ? "; LEFT:" + leftWho : "")}", leftInAntic == 0 && leftProps == 0 && leftWeaponsHidden == 0);
            float mean = AnticDirector.FlyoverDriveFrames > 0 ? AnticDirector.FlyoverDriveMsSum / AnticDirector.FlyoverDriveFrames : 0f;
            Check(lv + $": the cost for {robots} robots: the drive {mean:F2} ms mean / {AnticDirector.FlyoverDriveMsMax:F2} ms max a frame over {AnticDirector.FlyoverDriveFrames} frames (the max is the first frames' prop builds), the runs' late step {AnticDirector.FlyoverLateMsMax:F2} ms max; {AnticProps.Status()}; {AnticDirector.FlyoverAliveStatus()} (the mean under 2 ms, the late step under 6 ms)", mean < 2f && AnticDirector.FlyoverLateMsMax < 6f);
            // the sounds under the hold
            int playedS = 0, droppedS = 0; var byKey = new Dictionary<string, int>();
            foreach (var e in OzSoundLog.Since(t02))
            {
                if (handBackAt2 >= 0f && e.t >= handBackAt2) continue;
                bool antic = (e.key != null && e.key.StartsWith("rob_antic_")) || e.src == "AN" || e.key == "wpn_grenade_throw";
                if (!antic) continue;
                bool play = e.outcome != null && e.outcome.StartsWith("PLAY");
                if (play) playedS++; else droppedS++;
                string k = (e.key ?? "?") + (play ? "" : " dropped"); byKey.TryGetValue(k, out int n); byKey[k] = n + 1;
            }
            var ks = new StringBuilder(); foreach (var kv in byKey) ks.Append($" {kv.Key} {kv.Value};");
            Log(Part3 + $": antic sounds under the hold: {playedS} played, {droppedS} dropped (off screen, cooldown, gap):" + ks);
            Check(lv + $": the antics' own sounds played under the hold ({playedS} played on screen, {droppedS} dropped off screen or by a cooldown)", playedS > 0);
            Log(Part3 + ": " + AnticDirector.FlyoverStatus2());
            var log = AnticDirector.FlyoverLog; var sbl = new StringBuilder();
            for (int i = Mathf.Max(0, log.Count - 30); i < log.Count; i++) sbl.Append(" | " + log[i]);
            Log(Part3 + ": the drive's last lines:" + sbl);
            var late = new StringBuilder(); int nLate = 0;
            foreach (var l in log) if (l.Contains("LATE first start")) { nLate++; if (nLate <= 12) late.Append(" | " + l); }
            Log(Part3 + $": the drive's late first starts ({nLate}):" + (late.Length > 0 ? late.ToString() : " none"));
        }

        /// <summary>The frame traces: a RED start's briefing flyover drives the antics too now.</summary>
        void TraceLines3()
        {
            int robots = AnticDirector.FlyoverRobots - robots0, red = AnticDirector.FlyoverRedRobots - red0, blue = AnticDirector.FlyoverBlueRobots - blue0;
            if (job == Job.TraceRed && AnticDirector.FlyoverDriving)
            {   // the briefing's hold may have begun before the proof saw the level: its own counts (this level's hold, still running) count too
                robots = Mathf.Max(robots, AnticDirector.FlyoverHoldRobots); red = Mathf.Max(red, AnticDirector.FlyoverHoldRed); blue = Mathf.Max(blue, AnticDirector.FlyoverHoldBlue);
            }
            if (job == Job.TraceRed)
                Check($"RED start: the robots play the flyover antics under the red briefing's flyover too ({robots} robots started one within the trace: red {red}, blue {blue}; the briefing's hold, lines and shots unchanged); " + AnticDirector.FlyoverStatus2(), robots > 0 && red > 0);
            else Log(Part3 + $": BLUE start without a flight driver: {robots} robots started a flyover antic (none expected without a flight); " + AnticDirector.FlyoverStatus2());
        }
    }
}
#endif
