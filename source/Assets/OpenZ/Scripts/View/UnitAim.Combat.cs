using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The steady turret of View/UnitAim.Steady.cs passed its ring test, but the battle and fight films still counted
    /// flutters (3 / 3 / 14 and 6). Their per-frame headings, read frame by frame (the kit's tools/flutter_frames.py), show where they came
    /// from; none was the slew itself:
    ///  1. THE HULL UNDER THE GUN (most of them): one-frame steps of 0.4 to 1.3 deg in the barrels' world heading with no change of aim,
    ///     then the slew's eased way back, at each shot pair and while the hull pivots in place. The barrels are pitched up then (their
    ///     ballistic elevation plus the muzzle climb of up to 8 deg) and the hull rocks under them (the shot's rock and slide, the per-barrel
    ///     side rock of the suspension, the slope): a rock about an axis that is not square to the barrels turns a raised barrel sideways.
    ///     The solve then saw the target off by that much and slewed back: a back-and-forth of a degree on every shot.
    ///  2. THE SHOT'S OWN POINT: after a shot that left on target the gun was sent to that shell's landing point (the sim's scatter) for
    ///     0.35 s and then back to the target.
    ///  3. GOAL FLIPS: a new target or an idle step taken and given up again within a few frames (the slew turned toward each).
    ///  4. TRACKING NOISE: sub-degree wiggles of the aim point, followed by the slew one by one.
    /// Fix (view only; the sim's targets, shots and timing are untouched; v1 and v2 the same code): every vehicle turret's yaw is slewed
    /// in WORLD space: a critically damped, speed-limited slew (the gun's own smoothing and top speed, the waiting shot's deadline of
    /// UnitAim.Steady.cs) of the barrels' world heading toward the goal's world heading under this frame's hull; after the last pose of
    /// the frame (pitch and muzzle climb included) the yaw joint is set so the DRAWN barrels point exactly along that heading, whatever
    /// the hull does under them (1). After a shot that left on target the gun stays on its live target (2). A new goal more than
    /// <see cref="CombatJumpDeg"/> away is followed only when it has held <see cref="CombatCommitS"/> s (a committed aim with hysteresis:
    /// a flip back within that time is never drawn); a waiting shot is followed at once (3). Within <see cref="CombatDeadDeg"/> of the goal
    /// and nearly still, the turret holds (4). Shells still leave only when the muzzle is on them (the same tolerance and caps; the slew
    /// arrives before the cap). The MLRS rack (its ripple keeps one aim) and the guns (cannons) keep the code before.
    /// Switch: <see cref="CombatSteady"/>, and the layer only runs with <see cref="SteadyOn"/> (off = the turret code before k81b).
    /// </summary>
    public partial class UnitAim
    {
        /// <summary>The switch (the Play checks turn it off for their comparison runs and back on).</summary>
        public static bool CombatSteady = true;
        /// <summary>A goal that jumps more than CombatJumpDeg must hold CombatCommitS s before the turret follows it; within CombatDeadDeg of the
        /// goal and slower than CombatDeadSpeed deg/s the turret holds still.</summary>
        public static float CombatCommitS = 0.25f, CombatJumpDeg = 8f, CombatDeadDeg = 0.35f, CombatDeadSpeed = 6f;
        /// <summary>Statistics (the Play checks): frames a goal jump was held back, frames held still in the dead band, re-syncs after a snap
        /// or a switch, frames the gun stayed on its live target after a shot, and the largest heading correction drawn (deg).</summary>
        public static int CombatHoldFrames, CombatDeadFrames, CombatResyncs, CombatMarksSkipped;
        public static float CombatCorrMax;

        bool cInit, cOnLast; float cH, cW, cGoal, cCand, cCandT; bool cCandOn; int cSnaps = -1;

        /// <summary>The layer runs for this gun: k81b's steady fixes and this switch on, a vehicle's turret (not the MLRS rack, not a cannon).</summary>
        bool CombatOn => SteadyOn && CombatSteady && !robot && yawJ != null && yawJ.t && yawJ.t.parent && obj != null && (obj.Kind == ZKind.Vehicle || CombatGunOn)   // 1.2 v2 units: + the gun cannon's turret, v1 and v2 (View/UnitAim.Gun.cs)
                         && (wtype != "missile_launcher" || CombatRack) && muzzles.Count > 0;   // k87b, item 148: the MLRS launcher too (View/UnitAim.K87b.cs)

        /// <summary>The drawn barrels' mean world heading (deg) for a yaw / pitch / muzzle-climb pose (the joints are left in that pose).</summary>
        bool CombatMeasure(float yaw, float pitch, float kick, out float heading)
        {
            Pose(yaw, pitch, kick);
            return ProofBarrelHeading(out heading);
        }

        /// <summary>SolveGun, in place of the local SmoothDamp of the yaw: gy / gp = the solve's goal (local angles), smooth / speed = the slew
        /// (with the waiting shot's deadline). False = the layer is off for this gun (the code before runs).</summary>
        bool CombatSlew(float gy, float gp, float smooth, float speed, float dt)
        {
            bool on = CombatOn;
            if (on != cOnLast) { cInit = false; cOnLast = on; }
            if (!on) return false;
            if (Time.time < holdUntil && CombatSkipMark()) CombatMarksSkipped++;
            float pNow = pitchJ != null ? pitchJ.ang : 0f;
            if (!CombatMeasure(gy, gp, 0f, out float goalW) || !CombatMeasure(yawJ.ang, pNow, 0f, out float nowW)) { cInit = false; return false; }
            int snaps = MyStat().Snaps;
            if (!cInit || snaps != cSnaps)
            {   // first frame, after a switch, or a shot's snap (UnitAim.Snap set the joint): start from what is drawn
                if (cInit) CombatResyncs++;
                cH = nowW; cW = 0f; cGoal = goalW; cCandOn = false; cInit = true;
            }
            cSnaps = snaps;
            // the committed aim: small changes are tracked at once; a jump (a new target, an idle step) only after it has held CombatCommitS s;
            // a waiting shot is followed at once (the shell must leave from a gun that points at it)
            bool settledBack = ShotSettleBack();                                                       // k84b: the light tank's gun rested after a shot that waited for it: back to its live target at once (View/UnitAim.ShotSettle.cs)
            if (settledBack || held.Count > 0 || Mathf.Abs(Mathf.DeltaAngle(cGoal, goalW)) <= CombatJumpDeg) { cGoal = goalW; cCandOn = false; }
            else
            {
                if (cCandOn && Mathf.Abs(Mathf.DeltaAngle(cCand, goalW)) <= CombatJumpDeg) cCandT += dt;
                else { cCandOn = true; cCandT = 0f; }
                cCand = goalW;
                if (cCandT >= CombatCommitS) { cGoal = goalW; cCandOn = false; }
                else CombatHoldFrames++;
            }
            if (CombatIdleSlew(smooth, speed, dt)) { yawJ.ang = Wrap(yawJ.ang + Mathf.DeltaAngle(nowW, cH)); yawJ.vel = cW; return true; }   // k81d, item 132: the idle turret never turns back under a turning hull (View/UnitAim.IdleHold.cs)
            if (ShotSettleSlew(dt)) { yawJ.ang = Wrap(yawJ.ang + Mathf.DeltaAngle(nowW, cH)); yawJ.vel = cW; return true; }   // k84b, item 132: after a shot that waited for the gun, its turn eases out and it rests, never back and forth (View/UnitAim.ShotSettle.cs)
            if (TurnRestSlew(dt)) { yawJ.ang = Wrap(yawJ.ang + Mathf.DeltaAngle(nowW, cH)); yawJ.vel = cW; return true; }   // k87b, item 132: the medium tank's and the MLRS launcher's gun never starts back within 0.3 s of its last turn (View/UnitAim.K87b.cs)
            float err = Mathf.DeltaAngle(cH, cGoal);
            if (held.Count == 0 && Mathf.Abs(err) < CombatDeadDeg && Mathf.Abs(cW) < CombatDeadSpeed) { cW = 0f; CombatDeadFrames++; }
            else cH = Wrap(Mathf.SmoothDamp(cH, cH + err, ref cW, smooth, speed, dt));
            yawJ.ang = Wrap(yawJ.ang + Mathf.DeltaAngle(nowW, cH));                                      // first estimate; CombatHeading makes it exact
            yawJ.vel = cW;
            return true;
        }

        /// <summary>SolveGun's last pose: the yaw that puts the drawn barrels (this frame's pitch and muzzle climb included) exactly on the
        /// slew's world heading. Returns the yaw to pose (unchanged when the layer is off).</summary>
        float CombatHeading(float ya, float pa, float kick)
        {
            if (!cInit || !CombatOn) return ya;
            float k = kick > 0.01f && pitchJ != null ? kick : 0f;
            for (int i = 0; i < 2; i++)
            {
                if (!CombatMeasure(ya, pa, k, out float h)) return ya;
                float d = Mathf.DeltaAngle(h, cH);
                if (i == 0) CombatCorrMax = Mathf.Max(CombatCorrMax, Mathf.Abs(d));
                ya = Wrap(ya + d);
            }
            yawJ.ang = ya;
            return ya;
        }

        /// <summary>Target(): after a shot that left on target (UnitAim.Mark), the gun stays on its live target instead of turning to that
        /// shell's own landing point for 0.35 s and back. A kill shot (no live target) keeps the point as before.</summary>
        bool CombatSkipMark()
        {
            if (!CombatOn || obj == null || ShotSettleKeeps()) return false;                            // k84b: a shot that waited for the gun keeps its mark while the gun rests (View/UnitAim.ShotSettle.cs)
            var t = obj.AttackObject;
            if (t == null || t.Destroyed) t = predicted;
            return t != null && !t.Destroyed && t.KillAt < 0;
        }
    }
}
