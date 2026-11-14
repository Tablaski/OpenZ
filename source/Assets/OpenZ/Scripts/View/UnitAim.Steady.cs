using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only: the rules never read a turret direction, every shot is still held until the gun is on it (the same
    /// tolerance and caps), the sim's targets and timing are untouched; v1 and v2 run the same code.
    ///
    /// Causes found (View/UnitAim.cs):
    ///  1. THE SHAKE (heavy v2 only): SolveGunFor turns the turret by the error seen FROM THE MUZZLE, three times a frame. That step
    ///     overshoots by d / (d - L) (d = the target's distance from the turn axis, L = the muzzles' reach from it) and only settles while
    ///     d &gt; 2 L. v1's barrels reach 2.05 m from the axis, so it settles beyond 4.1 m (its 4 m minimum range keeps it there); v2's long
    ///     barrels reach 4.12 m, so every target closer than about 8 m made the solved angle flip from side to side frame after frame, and
    ///     the 4 m minimum range even sat inside the muzzles' reach (no solution at all). Robots walking up to the tank, a second target
    ///     close by: the turret shook back and forth. The frame model (tools/turret_model.py of the kit): v2 at 4.5-6 m 27-70 reversals in
    ///     4 s, a shake up to 7.5 deg; a ring of near targets switching every 1.2 s 41-54 reversals in 24 s; v1 none.
    ///     Fix: the step is scaled by the solve's own slope (a Newton step, <see cref="SteadyGain"/>: the same answer, reached without
    ///     overshoot; a step with the barrels turned away is unchanged, so K433's behind case is as before) and the minimum range is
    ///     at least 1.25 x the muzzles' reach (<see cref="SteadyMinRange"/>: v2 5.15 m; 4 m for every other gun, v1 included).
    ///  2. BETWEEN TWO TARGETS (v1 and v2): a dying target (its kill time set, not yet removed) or none for a moment made the gun idle at
    ///     once: standing, it stopped dead; driving, it swung back to the hull front, then out again to the next target the sim picked.
    ///     Fix: the gun stays on the last point it aimed at for up to <see cref="SteadyLingerS"/> s (<see cref="SteadyHold"/>).
    ///  3. A SHOT AT ITS WAIT CAP (v1 and v2): a shell that waited for the turret until its cap snapped the turret the rest of the way in
    ///     one frame when the next target was far round and the flight short. Fix: while a shot waits, the slew is set to arrive before
    ///     the cap (smoothing at most a 3.5th of the time left, speed what the gap needs, at most <see cref="SteadyDeadlineMax"/> deg/s):
    ///     still eased in and out, and the cap's snap is left with nothing (or a degree) to do (<see cref="SteadyDeadline"/>).
    ///  4. TICK STEPS (all shell guns): the pre-aim point of a shell gun was the target's place in whole sim ticks, so the goal stepped at
    ///     the tick rate; now the target's smooth place (ZGame.CenterWorldSmooth, as its view is drawn) (<see cref="SteadyGround"/>).
    /// Switch: <see cref="SteadyOn"/> (off = all four as before).
    /// </summary>
    public partial class UnitAim
    {
        /// <summary>The switch (the Play check turns it off for its comparison run and back on).</summary>
        public static bool SteadyOn = true;
        /// <summary>Seconds the gun stays on its last point while it has no target; the minimum range as a multiple of the muzzles' reach;
        /// the fastest slew a waiting shot may ask for (deg/s).</summary>
        public static float SteadyLingerS = 1.0f, SteadyRangeK = 1.25f, SteadyDeadlineMax = 720f, SteadyDeadlineMinSmooth = 0.05f;
        /// <summary>Statistics (the Play check): yaw steps scaled down, frames the gun stayed on its last point, slews sped up for a waiting shot.</summary>
        public static int SteadyGainCuts, SteadyLingerFrames, SteadyDeadlineSlews;

        float steadyReach;
        Vector3 steadyP; bool steadyBall; float steadyT, steadyG, steadyAt = -100f;

        /// <summary>Proof: the yaw joint's angle (deg) and its turn speed (deg/s) as the slew left them this frame.</summary>
        public float ProofYawAngle => yawJ != null ? yawJ.ang : 0f;
        public float ProofYawSpeed => yawJ != null ? yawJ.vel : 0f;
        /// <summary>Proof: shots waiting for the gun now.</summary>
        public int ProofHeld => held.Count;

        /// <summary>SolveGunFor: the minimum range round the turn axis, at least 1.25 x the farthest muzzle's flat reach seen (4 m before).</summary>
        float SteadyMinRange(Vector3 pivot)
        {
            if (!SteadyOn) return 4f;
            foreach (var m in muzzles)
            {
                if (!m.t) continue;
                var d = m.t.position - pivot; d.y = 0f;
                steadyReach = Mathf.Max(steadyReach, d.magnitude);
            }
            return Mathf.Max(4f, SteadyRangeK * steadyReach);
        }

        /// <summary>SolveGunFor: the yaw step's scale. The error seen from muzzle i changes with the yaw at -(1 + u.r / |u|^2) (u = muzzle to
        /// aim point, r = turn axis to muzzle, both flat), so the step divided by the mean of that slope lands on the answer instead of past
        /// it. A slope at or under 1 (barrels turned away from the point) keeps the plain step.</summary>
        float SteadyGain(Vector3 P, Vector3 yawAxis)
        {
            if (!SteadyOn || yawJ == null || !yawJ.t || muzzles.Count == 0) return 1f;
            var pivot = yawJ.t.position; float den = 0f; int n = 0;
            foreach (var m in muzzles)
            {
                if (!m.t) continue;
                var r = Vector3.ProjectOnPlane(m.t.position - pivot, yawAxis);
                var u = Vector3.ProjectOnPlane(P - m.t.position, yawAxis);
                float uu = u.sqrMagnitude; if (uu < 1e-6f) continue;
                den += 1f + Vector3.Dot(u, r) / uu; n++;
            }
            if (n == 0) return 1f;
            den /= n;
            if (den <= 1f) return 1f;
            SteadyGainCuts++;
            return 1f / den;
        }

        /// <summary>SolveGun: with a target, its point is remembered; without one (a dying target, a gap before the next), the gun stays
        /// on the remembered point for up to SteadyLingerS s instead of idling (an empty, grey vehicle never lingers: DV79's stop).</summary>
        bool SteadyHold(bool has, ref Vector3 P, ref bool ballistic, ref float T, ref float g)
        {
            if (!SteadyOn) return has;
            if (has) { steadyP = P; steadyBall = ballistic; steadyT = T; steadyG = g; steadyAt = Time.time; return true; }
            if (obj == null || obj.Owner == Team.Neutral || Time.time - steadyAt > SteadyLingerS) return false;
            P = steadyP; ballistic = steadyBall; T = steadyT; g = steadyG; SteadyLingerFrames++;
            return true;
        }

        /// <summary>SolveGun: while a shot waits for the gun, the slew's smoothing and top speed are set so it is on the shot before the
        /// wait cap (a critically damped slew covers about 98 % of the gap in 3.5 smoothing times).</summary>
        void SteadyDeadline(float gap, ref float smooth, ref float speed)
        {
            if (!SteadyOn || held.Count == 0) return;
            float rem = Mathf.Max(0.06f, held[0].maxWait - (Time.time - held[0].at));
            float s = Mathf.Max(SteadyDeadlineMinSmooth, rem / 3.5f), v = Mathf.Min(SteadyDeadlineMax, 2.2f * Mathf.Abs(gap) / rem);
            if (s < smooth || v > speed) SteadyDeadlineSlews++;
            smooth = Mathf.Min(smooth, s); speed = Mathf.Max(speed, v);
        }

        /// <summary>Target (a shell gun's pre-aim): the target's smooth place on the ground (as its view is drawn), not its whole-tick place.</summary>
        Vector3 SteadyGround(ZObject t)
        {
            if (obj.HeavyRockOn(t)) return GroundAt(t.CenterX, t.LocY + ZWorld.RockFootPx);            // DV164: the rock's drawn foot, where the rules aim the shell (Sim/ZObject.HeavyRock.cs)
            if (!SteadyOn) return GroundAt(t.CenterX, t.CenterY);
            var w = game.CenterWorldSmooth(t); w.y = game.GroundY(w.x, w.z);
            return w;
        }
    }
}
