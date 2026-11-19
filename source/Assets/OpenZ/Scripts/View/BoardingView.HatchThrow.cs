using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit exit_leap_death, the look of rule DV179 (Sim/ZObject.DriverThrown.cs): a tank or the mech destroyed while its driver is still
    /// getting out (the lid opening, his crouch on the rim; the rules decide: the hull's death before his take-off) THROWS him: no calm leap
    /// with a somersault any more (1.4's "the same jump at once", HatchBailJump), but one fast, uncontrolled flight away from the blast, his
    /// own blast clip (blast_airborne, as a knockdown of UnitView.React), the fall onto his back (death_back), a moment on the ground, the
    /// scramble up (death_back backwards, as React) timed so that he stands when the rules let him go (his thrown hold's end, DriverThrownAt),
    /// then his gun back in his hand and the walk on to his robot's spot (the 1.4 walk on, HatchWalkKeys). View only: nothing here touches the
    /// rules. The view matches the rules both ways: a driver the rules threw is thrown (from wherever his figure is: in the hatch, on the rim,
    /// in the air or already on the ground when the drawn explosion comes late, VehicleDeathSync); a driver the rules did not throw (DV179 off,
    /// the hull destroyed after his take-off) keeps 1.4's look exactly; a driver the rules kill (the round's splash, a shot while he lies)
    /// dies on his thrown figure (HatchExitDeath / HatchBodyTakesOver, the flight's velocity carried on: HatchThrowCarry).
    /// Sounds (the game's own): the whoosh of the throw (dst_ragdoll_whoosh), the body hitting the ground (rob_knockdown_land), the servo
    /// scramble up (rob_getup), the gun back in his hand (HatchGunUp: the same gun-ready sound as after his leap). The hull's own explosion
    /// and its sound are the wreck's (unchanged).
    /// The landing: the plan's own landing point (free ground on his side of the hull, clear of the hull and its open lid, HatchPlanJump)
    /// pushed <see cref="HatchThrowExtraM"/> farther from the blast when that ground is free too (HatchFootFree); on the ground already: a
    /// push away from the hull, shortened in 0.25 m steps to free ground. The flight goes up first (<see cref="HatchThrowUpMps"/>) from the
    /// rim, over the side the leap was planned for (the same side the leap's own clearance test cleared), so he never cuts through the hull.
    /// Off (<see cref="HatchThrowOn"/> false: the proof's control only) = 1.4's look; the rules are not changed by it.
    /// </summary>
    public partial class BoardingView
    {
        /// <summary>The look's switch (the proof's control; the rules' DV179 decides who is thrown).</summary>
        public static bool HatchThrowOn = true;
        /// <summary>The throw (m/s up from the hull; extra m/s up when the blast catches him in the air; m/s up from the ground), the push past
        /// the plan's landing (m), the push of a man already on the ground (m), the fall onto his back (s), the least time on the ground and
        /// the scramble up (s), the whoosh's gain, the longest wait for the rules' next tick (s).</summary>
        public static float HatchThrowUpMps = 2.6f, HatchThrowAirKickUpMps = 1.0f, HatchThrowGroundUpMps = 2.2f, HatchThrowExtraM = 1.0f, HatchThrowGroundPushM = 1.6f,
                            HatchThrowFallSec = 0.45f, HatchThrowLieMinSec = 0.35f, HatchThrowUpMinSec = 0.55f, HatchThrowUpMaxSec = 0.8f, HatchThrowWhooshGain = 0.9f,
                            HatchThrowPendingMaxSec = 0.1f;

        /// <summary>Proof: one record per thrown driver (the Play check reads it, View/OzExitLeapDeathCheck.cs).</summary>
        public sealed class HatchThrowRec
        {
            public int robotId, vehicleId; public string from = "";
            public double simStart, simRelease; public float flight, lieSec, upSec, apexM, landM, standLateSec = float.NaN, feetAboveAtLand = float.NaN;
            public bool whoosh, thud, getup, landed, stood, dead; public int startFrame = -1, landFrame = -1, standFrame = -1;
            public Vector3 start, land;
        }
        /// <summary>Proof: the throw records by robot id (cleared past 64).</summary>
        public static readonly Dictionary<int, HatchThrowRec> HatchThrows = new Dictionary<int, HatchThrowRec>();
        /// <summary>Proof counters: throws started, landed, stood up; deaths drawn on a thrown figure; frames waited for the rules' next tick.</summary>
        public static int HatchThrowsStarted, HatchThrowsLanded, HatchThrowsStood, HatchThrowDeaths, HatchThrowPendingFrames;

        sealed class ThrowState { public Vector3 p0, v0, land; public Quaternion face; public float t0, T, lieEnd, upEnd; public bool landed, getup; public HatchThrowRec rec; }
        readonly Dictionary<Ghost, ThrowState> blastThrows = new Dictionary<Ghost, ThrowState>();
        readonly Dictionary<Ghost, float> throwPending = new Dictionary<Ghost, float>();
        readonly HashSet<Ghost> throwDone = new HashSet<Ghost>();                                   // thrown once: never again for the same figure
        readonly List<Ghost> throwPrune = new List<Ghost>();

        /// <summary>Review fix: the throw entries of figures that ended (done) are dropped (StepGhost's EndGhost when his model dies or the hull's
        /// rig goes does not pass through HatchThrowFrame). Only finished figures; a figure still drawn is never touched.</summary>
        void HatchThrowPrune()
        {
            throwPrune.Clear();
            foreach (var kv in blastThrows) if (kv.Key == null || kv.Key.done) throwPrune.Add(kv.Key);
            foreach (var kv in throwPending) if (kv.Key == null || kv.Key.done) throwPrune.Add(kv.Key);
            foreach (var k in throwPrune) { if (k == null) continue; blastThrows.Remove(k); throwPending.Remove(k); }
            throwPrune.Clear();
        }

        /// <summary>HatchExitStep (one line, before 1.4's jump at once is asked): true = this frame of the exit figure is the throw's (StepGhost
        /// stops here). False: no throw (the rules did not throw him, or DV179 is off): 1.4's steps go on unchanged.</summary>
        bool HatchThrowStep(Ghost g, Rig r, HatchExit st, float step)
        {
            if (blastThrows.Count + throwPending.Count > 16) HatchThrowPrune();                       // review: a figure ended elsewhere (StepGhost ends it when his model dies) leaves its entry
            if (blastThrows.Count > 0 && blastThrows.TryGetValue(g, out var th)) return HatchThrowFrame(g, r, st, th, step);
            if (!HatchThrowOn || !ZWorld.DriverThrownDeviation || st.dead || (throwDone.Count > 0 && throwDone.Contains(g))) return false;
            var o = g.live ? g.live.Obj : null;
            if (o == null || game == null || game.World == null) return false;
            bool rulesGone = r == null || r.obj == null || r.obj.Destroyed || r.obj.KillAt >= 0;
            if (!rulesGone) { throwPending.Remove(g); return false; }
            if (o.DriverThrownFromId != st.vehicleId || o.DriverThrownAt < 0)
            {   // the hull is dead in the rules but his hold has not looked yet (his object comes before the hull's death in that tick: the next tick
                // does): this frame waits (his figure stays as drawn), so 1.4's jump at once does not start first; at most HatchThrowPendingMaxSec
                bool stillHeld = o.DriverOutFromId == st.vehicleId && o.DriverOutHeld && o.ExitWalking(game.World.Time);
                if (!stillHeld || st.phase >= JumpPhase.Push) { throwPending.Remove(g); return false; }
                throwPending.TryGetValue(g, out float waited);
                if (waited >= HatchThrowPendingMaxSec) return false;
                throwPending[g] = waited + Mathf.Max(step, 0.001f); HatchThrowPendingFrames++;
                st.tau += step;
                return true;
            }
            throwPending.Remove(g);
            if (st.hullDeadAt < 0f) st.hullDeadAt = Time.time;
            // the drawn explosion: as 1.4's jump at once waits for it (a hull killed by a round explodes when the round arrives, VehicleDeathSync.Hold)
            bool gone = r == null || r.obj == null || !r.view || r.view.Dying || !HatchBailOnDrawnDeath || Time.time - st.hullDeadAt > VehicleDeathSync.MaxDelay + 0.5f;
            if (!gone) return false;
            HatchThrowStart(g, r, st, o);
            return HatchThrowFrame(g, r, st, blastThrows[g], 0f);
        }

        void HatchThrowStart(Ghost g, Rig r, HatchExit st, ZObject o)
        {
            var w = game.World;
            bool shown = g.go && g.go.activeSelf && st.phase >= JumpPhase.Crouch;
            Vector3 p0; string from;
            if (shown) { p0 = g.go.transform.position; from = st.phase.ToString().ToLowerInvariant(); }
            else if (r != null && r.root) { p0 = HatchRimW(r); from = "inside (out of the hatch)"; }
            else { p0 = Ground(g.live.transform.position); from = "his spot (no hull drawn)"; }
            if (st.phase == JumpPhase.Inside && r != null && r.crew && r.lid && r.obj != null && r.obj.Drivers.Count == 0) { Destroy(r.crew); r.crew = null; r.crewPop = 0f; HatchCommandersTaken++; }   // the commander still sinking IS him (as HatchBailJump)
            // away from the blast (the hull's centre); in the hatch the plan's own direction (the side whose clearance the plan tested)
            Vector3 c = r != null && r.root ? r.root.position : p0, away = p0 - c; away.y = 0f;
            if (st.phase < JumpPhase.Air || away.sqrMagnitude < 0.09f) { away = st.dir; away.y = 0f; }
            if (away.sqrMagnitude < 1e-4f) { away = g.live.transform.position - c; away.y = 0f; }
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.forward;
            if (r != null && r.obj != null) HatchSideBlocks(w, r.obj); else sideBlocks.Clear();
            bool onGround = st.phase >= JumpPhase.Land;
            Vector3 L;
            if (!onGround)
            {
                var plan = st.land.sqrMagnitude > 1e-6f ? st.land : p0 + away * 3f;
                var far = plan + away * HatchThrowExtraM;
                L = HatchFootFree(w, far) ? far : plan;
            }
            else
            {
                L = p0;
                for (float d = HatchThrowGroundPushM; d > 0.01f; d -= 0.25f) { var q = p0 + away * d; if (HatchFootFree(w, q)) { L = q; break; } }
            }
            L = Ground(L);
            Vector3 vPrev = Vector3.zero;
            if (st.phase == JumpPhase.Air && st.arc != null) vPrev = HatchVelAt(st.arc, Mathf.Clamp(st.tau - (st.takeoffAt - st.arc.tp), 0f, st.arc.tp + st.arc.T));
            float vy = st.phase == JumpPhase.Air ? Mathf.Max(0f, vPrev.y) + HatchThrowAirKickUpMps : onGround ? HatchThrowGroundUpMps : HatchThrowUpMps;
            float gr = Mathf.Max(1f, HatchJumpGravity), drop = p0.y - L.y, disc = vy * vy + 2f * gr * drop;
            float T = disc > 0f ? (vy + Mathf.Sqrt(disc)) / gr : 0.35f;
            T = Mathf.Max(0.3f, T);
            var flat = L - p0; flat.y = 0f;
            vy = (L.y - p0.y + 0.5f * gr * T * T) / T;                                                         // exactly onto L at T (T kept)
            var th = new ThrowState { p0 = p0, land = L, T = T, t0 = st.tau, face = HatchFace(away), v0 = flat / T + Vector3.up * vy };
            // his time on the ground and the scramble up: stretched so he stands when the rules let him go (React's DV51 rule: the shares kept)
            float left = (float)(o.DriverThrownAt - w.Time), rest = left - T - HatchThrowFallSec;
            float up = Mathf.Clamp(rest * 0.45f, HatchThrowUpMinSec, HatchThrowUpMaxSec), lie = Mathf.Max(HatchThrowLieMinSec, rest - up);
            th.lieEnd = T + HatchThrowFallSec + lie; th.upEnd = th.lieEnd + up;
            st.bailed = true; st.due = -1; st.phase = JumpPhase.Land; st.touchAt = st.tau;                       // no 1.4 jump at once; "land": the figure is the throw's (HatchExitOnHull: off the hull)
            if (r != null && !st.lidShut) HatchLidShut(r, st);                                                  // the wreck keeps no open lid
            if (g.go && !g.go.activeSelf) { g.go.SetActive(true); g.clipNow = null; }
            if (g.anim) { g.anim.applyRootMotion = false; g.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            var rec = new HatchThrowRec { robotId = st.robotId, vehicleId = st.vehicleId, from = from, simStart = w.Time, simRelease = o.DriverThrownAt, flight = T, lieSec = lie, upSec = up,
                                          start = p0, land = L, landM = flat.magnitude, startFrame = Time.frameCount };
            rec.whoosh = Voice("dst_ragdoll_whoosh", p0, st.robotId, HatchThrowWhooshGain);
            if (HatchThrows.Count > 64) HatchThrows.Clear();
            HatchThrows[st.robotId] = rec; th.rec = rec;
            if (throwDone.Count > 256) throwDone.RemoveWhere(x => x == null || x.done);
            blastThrows[g] = th; throwDone.Add(g); HatchThrowsStarted++;
            string line = $"thrown by the hull's blast from {from}: {T:0.00} s flight, {flat.magnitude:0.00} m out, {lie:0.00} s down, {up:0.00} s up, stands at the rules' release {o.DriverThrownAt:0.00}";
            HatchExitLastPlan += "; " + line;
            if (st.rec != null) st.rec.plan += "; " + line;
        }

        bool HatchThrowFrame(Ghost g, Rig r, HatchExit st, ThrowState th, float step)
        {
            st.tau += step; st.lastStep = step;
            if (st.dead)
            {   // his robot died (HatchExitDeath put his model where the figure is; UnitView.Die shows it and StepGhost ends this ghost): the figure
                // goes on along the throw until then (his hidden model follows it); safety: 3 s
                st.deadFor += step;
                if (th.rec != null) th.rec.dead = true;
                if (st.deadFor > 3f) { blastThrows.Remove(g); EndGhost(g, true); return true; }
            }
            if (!g.go) return true;
            var t = g.go.transform; float s = st.tau - th.t0;
            t.localScale = Vector3.one * g.baseScale;
            if (s < th.T)
            {   // the flight: thrown off his feet, away from the blast (his own blast clip, as a knockdown)
                var p = th.p0 + th.v0 * s + Vector3.down * (0.5f * HatchJumpGravity * s * s);
                t.SetPositionAndRotation(p, th.face);
                HatchSample(g.anim, Clip(g.anim, "blast_airborne"), Mathf.Clamp01(s / th.T));
                HatchBandOff(g.skin);
                st.markPos = Ground(p);
                if (th.rec != null) th.rec.apexM = Mathf.Max(th.rec.apexM, p.y - th.p0.y);
            }
            else if (s < th.lieEnd)
            {   // on the ground: the fall onto his back, then he lies
                if (!th.landed)
                {
                    th.landed = true; HatchThrowsLanded++;
                    bool thud = Voice("rob_knockdown_land", th.land, st.robotId, 1f);
                    if (th.rec != null) { th.rec.landed = true; th.rec.thud = thud; th.rec.landFrame = Time.frameCount; th.rec.feetAboveAtLand = th.land.y - Ground(th.land).y; }
                }
                t.SetPositionAndRotation(th.land, th.face);
                float k = Mathf.Clamp01((s - th.T) / Mathf.Max(0.05f, HatchThrowFallSec));
                HatchSample(g.anim, Clip(g.anim, "death_back"), Mathf.Lerp(0.15f, 0.95f, k));
                HatchBandOff(g.skin);
                st.markPos = th.land;
            }
            else if (s < th.upEnd)
            {   // the scramble up: his fall played backwards (the controllers have no get-up clip: React's way)
                if (!th.getup) { th.getup = true; bool up = Voice("rob_getup", th.land, st.robotId, 1f); if (th.rec != null) th.rec.getup = up; }
                float x = Mathf.Clamp01((s - th.lieEnd) / Mathf.Max(0.05f, th.upEnd - th.lieEnd)), k = x * x * (3f - 2f * x);
                t.SetPositionAndRotation(th.land, th.face);
                HatchSample(g.anim, Clip(g.anim, "death_back"), Mathf.Lerp(0.95f, 0.02f, k));
                if (k > 0.6f) Band(g.skin, t, g.hips, t.localScale.x); else HatchBandOff(g.skin);
                st.markPos = th.land;
            }
            else if (!st.dead)
            {   // he stands: his gun in his hand again, then 1.4's walk on from here to his robot (K441 hands over on it)
                if (!st.gunUp) HatchGunUp(g, st, 0f);
                st.land = th.land;
                HatchWalkKeys(g, st);
                st.phase = JumpPhase.Walk; st.markPos = th.land;
                if (g.anim) g.anim.speed = 1f;
                g.time = 0f; g.clipNow = null;
                Band(g.skin, t, g.hips, t.localScale.x);
                HatchThrowsStood++;
                if (th.rec != null) { th.rec.stood = true; th.rec.standFrame = Time.frameCount; th.rec.standLateSec = game && game.World != null ? (float)(game.World.Time - th.rec.simRelease) : float.NaN; }
                blastThrows.Remove(g);
                return false;
            }
            if (st.dead) HatchDeadFollow(g);
            return true;
        }

        /// <summary>HatchBodyTakesOver (one line, before his model is shown): a driver killed in the throw's flight: the flight's velocity at this
        /// moment goes on with his death (the pop's pieces, the rag doll's bodies), as 1.4 carries the leap's. On the ground: nothing to carry.</summary>
        void HatchThrowCarry(Ghost g, HatchExit st)
        {
            if (blastThrows.Count == 0 || !blastThrows.TryGetValue(g, out var th)) return;
            HatchThrowDeaths++; if (th.rec != null) th.rec.dead = true;
            float s = st.tau - th.t0;
            if (s < 0f || s >= th.T || !g.go || !g.go.activeInHierarchy || !g.live) return;
            var carry = th.v0 + Vector3.down * (HatchJumpGravity * s);
            popCarryView = g.live; popCarryVel = carry; popCarryFrame = Time.frameCount; HatchDeathCarries++; HatchExitLastDeathCarry = carry;
            if (st.rec != null) st.rec.deathCarry = carry;
            StartCoroutine(HatchCarryBody(g.live, carry));
        }

        /// <summary>Proof: the throw of this robot now (true while his figure is in the throw: flying, down or getting up).</summary>
        public static bool HatchThrownNow(int robotId)
        {
            var bv = I; if (!bv || bv.blastThrows.Count == 0) return false;
            foreach (var kv in bv.blastThrows) if (kv.Key != null && !kv.Key.done && kv.Value.rec != null && kv.Value.rec.robotId == robotId) return true;
            return false;
        }

        /// <summary>Proof: the throw counters back to 0 (with HatchExitStatsReset's own).</summary>
        public static void HatchThrowStatsReset()
        {
            HatchThrowsStarted = HatchThrowsLanded = HatchThrowsStood = HatchThrowDeaths = HatchThrowPendingFrames = 0;
            HatchThrows.Clear();
        }
    }
}
