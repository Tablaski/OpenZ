using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 132 ("his turret jitters ... ease its rotation out so never jitter") for the light tank: its turret test (the menu "11"
    /// ring of grunts) still counted one flutter while switching targets and two while taking a target again (v1: none). Read frame by frame,
    /// every one of them follows a shot that WAITED for the gun: a tank shell lands on the sim's own scatter point, 2 to 10 deg off the live
    /// target at 6 to 16 m, and the shot is held until the barrel points at that point (UnitAim.Hold). k81c's rule then sent the gun
    /// straight back to its live target the frame the shell left (CombatSkipMark: a step under CombatJumpDeg, so no commit wait), while the
    /// target itself moved (knocked back by that shell, or walking) the other way: the turret turned back and then again within a few
    /// frames. One of the three was a shot held and released inside one frame (the gun's solve runs before the held shots are let go).
    /// The light tank's muzzle reach (2.62 m, x OzLightTank.V2Size) times 1.25 stays inside UnitAim.Steady's 4 m minimum range: not a cause.
    /// Now, after a shot that waited for the gun: the turn eases out (it brakes, never turning back and never past that shell's heading)
    /// and the gun rests there while its live target is not ahead of the turn (behind it or level with it), for the shot's mark (0.35 s),
    /// and on while that target is closing in (up to <see cref="ShotSettleMaxS"/>); the moment the live target is ahead of the turn, or it
    /// stops closing after the mark, the gun takes its live target at once (no commit wait for that return): one eased move, never a
    /// back-and-forth. Shots that left without waiting keep k81c's rule (the gun stays on its live target). View only: the sim's targets,
    /// shots and their timing are untouched. For the types in <see cref="ShotSettleTypes"/> (the light tank, both models; a type with the
    /// same gun can join with one entry). Switch: <see cref="ShotSettle"/> (off = k81c's rule).
    /// </summary>
    public partial class UnitAim
    {
        /// <summary>The switch (off = after every shot the gun goes straight back to its live target, as in k81c).</summary>
        public static bool ShotSettle = true;
        /// <summary>The vehicle types whose turret settles after a shot that waited for it.</summary>
        public static readonly HashSet<string> ShotSettleTypes = new HashSet<string> { OzLightTank.Type };
        /// <summary>The brake's time constant (s), the live target's closing speed (deg/s) that keeps the gun waiting after the mark, and
        /// the longest wait (s) after the shot.</summary>
        public static float ShotSettleTau = 0.05f, ShotSettleCloseDps = 3f, ShotSettleMaxS = 3f;
        /// <summary>Statistics (the Play checks): frames a gun rested after a shot that waited for it, the returns to the live target.</summary>
        public static int ShotSettleFrames, ShotSettleReturns;

        float sChaseT = -1f, sStart = -1f, sDir, sLiveLast; bool sLiveHad, sKept, sKeptLast; int sFrame = -1;

        bool ShotSettleOn => ShotSettle && obj != null && ShotSettleTypes.Contains(obj.Type) && CombatOn;

        /// <summary>The live target's bearing from the turn axis (world deg, 0 = +z), as Target() aims at it with no shot pending.</summary>
        bool ShotSettleLive(out float bearing)
        {
            bearing = 0f;
            var t = obj.AttackObject;
            if (t == null || t.Destroyed) t = predicted;
            if (t == null || t.Destroyed || t.KillAt >= 0) return false;
            var p = obj.DamageIsMissile && !HasGrenades(obj) ? OzShuttleAim.AirAim(t, SteadyGround(t) + Vector3.up * 0.25f, 0f) : AimPointOf(t);
            var d = p - yawJ.t.position; d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return false;
            bearing = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>CombatSkipMark: true = the mark of a shot that waited for the gun is kept this frame (the gun rests after its turn). Worked
        /// out once a frame (Target() asks first, the slew after).</summary>
        bool ShotSettleKeeps()
        {
            if (sFrame == Time.frameCount) return sKept;
            sFrame = Time.frameCount; sKept = false;
            if (!ShotSettleOn || held.Count > 0 || sChaseT < 0f || Mathf.Abs(lastShot - sChaseT) > 1e-4f) { sStart = -1f; return false; }
            float now = Time.time;
            if (sStart < 0f)
            {   // a shot that waited for the gun has just left: the turn it was in, from the live target toward that shell's point
                sStart = lastShot; sDir = 0f; sLiveHad = false;
                var hp = holdPoint - yawJ.t.position; hp.y = 0f;
                if (hp.sqrMagnitude > 1e-4f && ShotSettleLive(out float l0)) sDir = Mathf.Sign(Mathf.DeltaAngle(l0, Mathf.Atan2(hp.x, hp.z) * Mathf.Rad2Deg));
            }
            if (!ShotSettleLive(out float live) || sDir == 0f) sKept = now < holdUntil;                  // no live target: the mark as before
            else
            {
                float d = Mathf.DeltaAngle(cH, live);
                float closing = sLiveHad ? Mathf.DeltaAngle(sLiveLast, live) / Mathf.Max(1e-4f, Time.deltaTime) * sDir : 0f;
                sLiveLast = live; sLiveHad = true;
                bool ahead = Mathf.Abs(d) >= CombatDeadDeg && Mathf.Sign(d) == sDir;                      // the live target is ahead of the turn
                sKept = !ahead && (now < holdUntil || (closing > ShotSettleCloseDps && now < sStart + ShotSettleMaxS));
            }
            if (!sKept) { sChaseT = -1f; sStart = -1f; }                                                  // done: the live target from now on
            return sKept;
        }

        /// <summary>CombatSlew, before the committed aim: notes a shot waiting for the gun (the turn toward that shell's point) and returns
        /// true on the frame the gun's rest after such a shot ends (it then takes its live target at once, no commit wait).</summary>
        bool ShotSettleBack()
        {
            if (!ShotSettleOn) { sKeptLast = false; return false; }
            if (held.Count > 0) { sChaseT = Time.time; sKeptLast = false; return false; }
            bool kept = ShotSettleKeeps();
            bool back = sKeptLast && !kept;
            if (back) ShotSettleReturns++;
            sKeptLast = kept;
            return back;
        }

        /// <summary>CombatSlew, in place of the eased slew while the gun rests after a shot that waited for it: the turn brakes out, never back
        /// and never past the heading it was turning to (that shell's), then holds. True = done for this frame.</summary>
        bool ShotSettleSlew(float dt)
        {
            if (!ShotSettleOn || held.Count > 0 || !ShotSettleKeeps()) return false;
            float lim = Mathf.DeltaAngle(cH, cGoal);
            cW *= Mathf.Exp(-Mathf.Max(0f, dt) / Mathf.Max(1e-3f, ShotSettleTau));
            float step = cW * Mathf.Max(0f, dt);
            if (lim * step <= 0f) { step = 0f; cW = 0f; }
            else if (Mathf.Abs(step) > Mathf.Abs(lim)) { step = lim; cW = 0f; }
            if (Mathf.Abs(cW) < IdleStillDps) cW = 0f;
            cH = Wrap(cH + step); ShotSettleFrames++;
            return true;
        }
    }
}
