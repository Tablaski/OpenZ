using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 132 ("his turret jitters ... ease its rotation out so never jitter"), the idle turret. The world-heading slew of
    /// View/UnitAim.Combat.cs passed every fight, but its combat test and the battle film each counted ONE flutter, both with no target
    /// at all: the idle turret follows the hull's front, and when the tank turned at the end of a leg one way and settled back the other,
    /// the turret turned with it, then back (141.0 -> 127.9 -> 130.3 deg in 10 frames). Now, with no target and no waiting shot, the
    /// barrels' world heading still eases to the idle goal (the hull's front while driving, Zod's 45 deg scan steps while standing) with
    /// the same slew a target gets, but it never turns back while it moves: when the goal comes round to the other side (the hull turning
    /// back) the turn brakes to a stop where it is, and only after <see cref="IdleRestS"/> at rest may it start the other way. So an idle
    /// turret holds its world heading through a hull's wiggle and then eases to the front in one move. View only; targets, shots and
    /// their timing are untouched. Switch: <see cref="IdleNoReverse"/> (off = k81c's idle slew).
    /// </summary>
    public partial class UnitAim
    {
        /// <summary>The switch (off = the idle turret follows the hull's front both ways, as in k81c).</summary>
        public static bool IdleNoReverse = true;
        /// <summary>Seconds an idle turret rests before it may turn the other way; the braking time constant (s); under this speed (deg/s)
        /// it counts as at rest.</summary>
        public static float IdleRestS = 0.35f, IdleBrakeTau = 0.08f, IdleStillDps = 1.5f;
        /// <summary>Statistics (the Play checks): frames an idle turret braked instead of turning back, frames it waited at rest.</summary>
        public static int IdleBrakeFrames, IdleWaitFrames;

        int iDir; float iRestT;                                                                        // the idle slew's last direction (-1 / +1, 0 = none yet), its time at rest

        /// <summary>CombatSlew, after the goal is committed: the idle turret's slew (no target, no waiting shot). False = not idle (the
        /// combat slew runs as before).</summary>
        bool CombatIdleSlew(float smooth, float speed, float dt)
        {
            if (!IdleNoReverse || !wasIdle || held.Count > 0) { iDir = 0; iRestT = 0f; return false; }
            float err = Mathf.DeltaAngle(cH, cGoal);
            int want = Mathf.Abs(err) < CombatDeadDeg ? 0 : (err > 0f ? 1 : -1);
            int moving = Mathf.Abs(cW) > IdleStillDps ? (cW > 0f ? 1 : -1) : 0;
            if (moving != 0 && want != moving)
            {   // the goal is behind it now (or reached): brake to a stop, never back
                cW *= Mathf.Exp(-Mathf.Max(0f, dt) / IdleBrakeTau);
                cH = Wrap(cH + cW * dt); iRestT = 0f; IdleBrakeFrames++;
                return true;
            }
            if (moving == 0)
            {
                if (want == 0 || (iDir != 0 && want != iDir && iRestT < IdleRestS))
                {   // at rest: on the goal, or waiting before it turns the other way
                    cW = 0f; iRestT += Mathf.Max(0f, dt);
                    if (want != 0) IdleWaitFrames++; else CombatDeadFrames++;
                    return true;
                }
            }
            float w = moving == 0 ? 0f : cW;
            float h = Mathf.SmoothDamp(cH, cH + err, ref w, smooth, speed, dt);                          // the slew a target gets (it never overshoots its goal)
            if (w * want < 0f) { w = 0f; h = cH; }                                                       // never a step the other way
            cH = Wrap(h); cW = w; iDir = want; iRestT = 0f;
            return true;
        }
    }
}
