using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 132 ("his turret jitters ... ease its rotation out so never jitter") for the MEDIUM TANK (DV145) and the MLRS launcher.
    /// The medium tank's turret test (menu "12", the ring of grunts) counted two flutters while switching targets and one while taking a
    /// target again, its fight film one:
    ///  1. the two while switching follow a shot that WAITED for the gun (the light tank's case: the shell's own scatter point, then straight
    ///     back while the target moved the other way): the medium tank fires the light tank's gun, so it joins the light tank's rest after
    ///     such a shot (View/UnitAim.ShotSettle.cs: <see cref="ShotSettleTypes"/>, registered here at the start of every session);
    ///  2. the other two are not in the slew itself (the recorded goals replayed through it give none): steps of 0.1 to 0.2 deg of the drawn
    ///     heading back and forth while the gun tracks slowly (its goal wobbling by a few tenths after a shot) and an arrival on a new target
    ///     that then moves back the other way. Now, for the types in <see cref="TurnRestTypes"/>: after the gun last turned one way it never
    ///     starts the other way within <see cref="TurnRestS"/> while its goal is less than <see cref="TurnBackDeg"/> back: it brakes to a stop
    ///     (never back) and rests, then makes one eased move. A shot waiting for the gun is followed at once, a goal further back too (a new
    ///     aim), a goal ahead of the turn as before. The gun lags its live target by at most a few degrees for a moment; shells still leave
    ///     only when the muzzle is on them.
    /// its launcher now takes the world-heading slew of View/UnitAim.Combat.cs as the tanks' turrets do (<see cref="CombatRack"/>: the
    /// launcher's turn holds its world heading whatever the hull does under it, a new aim is committed, the dead band holds it still), the
    /// rest after a rocket that waited for the launcher (the ripple's aim is kept for the ripple: UnitAim.KeepAim holds the mark) and the
    /// rest before a turn back. Its kick (View/MlrsSideKick.cs) tips the box and rolls it about the tubes' own axis: it never turns their
    /// heading (measured on both models' prefabs), so the drawn heading stays the slew's.
    /// View only: the sim's targets, shots and their timing are untouched.
    /// Switches: <see cref="TurnRest"/> (off = k81c's slew, with k84b's rest after a waiting shot), <see cref="CombatRack"/> (off = the
    /// MLRS launcher as before).
    /// </summary>
    public partial class UnitAim
    {
        /// <summary>The switch (off = the gun may turn back at once, as in k81c).</summary>
        public static bool TurnRest = true;
        /// <summary>The switch for the MLRS launcher's world-heading slew (off = the launcher's turret code before k81c).</summary>
        public static bool CombatRack = true;
        /// <summary>The vehicle types whose turret rests before it turns back (the medium tank and the MLRS; a type can join with one entry).</summary>
        public static readonly HashSet<string> TurnRestTypes = new HashSet<string> { OzMediumTank.Type, OzMlrs.Type };
        /// <summary>How long after its last turn the gun may not start the other way (s; the turret test's flutter window is 8 frames, 0.27 s),
        /// and how far back (deg) a goal must be to be followed at once.</summary>
        public static float TurnRestS = 0.3f, TurnBackDeg = 3f;
        /// <summary>Statistics (the Play checks): frames a gun rested instead of turning back.</summary>
        public static int TurnRestFrames;

        int tDir; float tDirT = -10f;                                                                   // the way the gun last turned (-1 / +1, 0 = none), when

        /// <summary>The medium tank fires the light tank's gun and the MLRS fires its rockets after the launcher turned to them: both rest
        /// after a shot that waited for them as the light tank does (k84b's list is kept as k84b wrote it; the entries are added once at the
        /// start of every session).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void K87bShotSettleTypes() { ShotSettleTypes.Add(OzMediumTank.Type); ShotSettleTypes.Add(OzMlrs.Type); }

        /// <summary>CombatSlew, right before the eased slew (after the idle slew and the rest after a waiting shot): true = the gun brakes or
        /// rests this frame instead of turning back (done for this frame).</summary>
        bool TurnRestSlew(float dt)
        {
            if (!TurnRest || obj == null || !TurnRestTypes.Contains(obj.Type) || !CombatOn || held.Count > 0) { tDir = 0; return false; }   // a waiting shot: at once, as before
            float now = Time.time;
            if (Mathf.Abs(cW) > IdleStillDps) { tDir = cW > 0f ? 1 : -1; tDirT = now; }                     // the way the gun turned last frame
            if (tDir == 0) return false;
            float err = Mathf.DeltaAngle(cH, cGoal);
            if (Mathf.Abs(err) < CombatDeadDeg || Mathf.Abs(err) >= TurnBackDeg || (err > 0f ? 1 : -1) == tDir) return false;   // on it, far back (a new aim) or ahead: as before
            if (now - tDirT >= TurnRestS) { tDir = 0; return false; }                                        // rested: one eased move back
            cW *= Mathf.Exp(-Mathf.Max(0f, dt) / IdleBrakeTau);                                             // brake to a stop, never back
            if (cW * tDir < 0f || Mathf.Abs(cW) < IdleStillDps) cW = 0f;
            cH = Wrap(cH + cW * Mathf.Max(0f, dt));
            TurnRestFrames++;
            return true;
        }

        /// <summary>OnFire, when a rocket leaves an MLRS tube (v1 or v2): the tube's muzzle and its axis go to the drawn rocket the same frame
        /// (View/OpenZFxPlayer.TubeExit.cs: the rocket leaves that tube's mouth along that tube from its first drawn frame).</summary>
        void MlrsTubeNote(Muzzle m)
        {
            if (wtype == OzMlrs.Type && m != null && m.t) OpenZFxPlayer.NoteTube(m.t, m.dirL);
        }
    }
}
