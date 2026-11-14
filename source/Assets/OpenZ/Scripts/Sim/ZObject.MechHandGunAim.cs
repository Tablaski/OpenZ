using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ deviation DV146 "the mech's hand gun", its aim (ZObject.MechHandGun.cs is the rule): THE GUN TURNS FIRST. The hand gun has an
    /// arm that turns: its heading on the map (degrees, seen from the gun's place, the same place the forward arc is measured at) moves
    /// toward the robot the hand gun wants at <see cref="ZWorld.HandGunTurnDegS"/> per second, holds on the last robot for
    /// <see cref="ZWorld.HandGunHoldS"/> after a shot, and goes back to the line to the rocket target (its rest) after that; it never leaves
    /// the forward arc (+ <see cref="ZWorld.HandGunArmSlackDeg"/>). A shot needs the arm ON its robot (within
    /// <see cref="ZWorld.HandGunAimTolDeg"/>); at a NEW robot (another than the one it shot last) the first shot also waits
    /// <see cref="ZWorld.HandGunSettleS"/> after the arm got there. So the first shot at a new robot comes the angle to turn / the turn rate
    /// + the settle after the robot was chosen, rounded up to whole ticks (a 40 deg turn: 0.2 + 0.12 s); a robot already on the arm's line
    /// waits the settle only; the same robot in the next reload window (the arm still holds on it) is shot at once.
    /// Deterministic: sim time and integer positions only, no frame time, no view state. The view (View/MechHandGunView.cs,
    /// View/MechHandGunAim.cs) draws the arm turning onto the robot the sim chose (<see cref="HandGunTarget"/>) in that time.
    /// </summary>
    public partial class ZObject
    {
        double hgArmHdg = double.NaN, hgArmAt = -100, hgOnSince = -1; ZObject hgShotAt, hgAimFor;

        /// <summary>DV146: the hand gun's arm, its heading on the map (deg, atan2 of map y down / x, seen from the gun's place); NaN = at rest
        /// with no rocket target to rest on.</summary>
        public double HandGunArmHeading => hgArmHdg;
        /// <summary>DV146 counters (checks): ticks a chosen robot waited for the arm; robots the arm was turned to (a new one after another);
        /// the angle (deg) the arm had to turn when the last new robot was chosen; the robot it turns to / is on now (null = none).</summary>
        public int HandGunAimWaitTicks { get; private set; }
        public int HandGunTargetSwitches { get; private set; }
        public double HandGunLastTurnDeg { get; private set; }
        public ZObject HandGunAimTarget => hgAimFor;

        /// <summary>DV146: the point the upper body looks at (the rocket target's centre); false = none (public for the checks).</summary>
        public bool HandGunLookAt(out int lx, out int ly) => HandGunLook(out lx, out ly);

        /// <summary>DV146: the gun's place on the map (the mech's centre + <see cref="ZWorld.HandGunSidePx"/> to its right, square to the
        /// line to the look point), as HandGunArcDeg measures it; false = the look point too close to measure.</summary>
        public bool HandGunGunPoint(int lx, int ly, out double gx, out double gy)
        {
            double fx = lx - CenterX, fy = ly - CenterY, fl = Math.Sqrt(fx * fx + fy * fy);
            gx = CenterX; gy = CenterY;
            if (fl < 1.0) return false;
            fx /= fl; fy /= fl;
            gx = CenterX - fy * ZWorld.HandGunSidePx; gy = CenterY + fx * ZWorld.HandGunSidePx;
            return true;
        }

        /// <summary>DV146: the map heading (deg) from the gun's place to the point (px, py); NaN = too close to measure.</summary>
        public double HandGunHeadingTo(int lx, int ly, double px, double py)
        {
            if (!HandGunGunPoint(lx, ly, out double gx, out double gy)) return double.NaN;
            double dx = px - gx, dy = py - gy;
            if (dx * dx + dy * dy < 1.0) return double.NaN;
            return Math.Atan2(dy, dx) * (180.0 / Math.PI);
        }

        /// <summary>DV146: b - a in degrees, wrapped to -180 .. 180 (+ = clockwise on the map = to the mech's right, as HandGunArcDeg).</summary>
        public static double HandGunDeltaDeg(double a, double b)
        {
            double d = (b - a) % 360.0;
            if (d > 180.0) d -= 360.0; else if (d < -180.0) d += 360.0;
            return d;
        }

        /// <summary>Every tick at the start of MechHandGunStep (after the carrier test): the arm turns toward the robot the hand gun wants
        /// (chosen last tick), holds on the last robot after a shot, goes back to its rest (the line to the look point) after that.</summary>
        void HandGunArmStep(double time)
        {
            double dt = time - hgArmAt; hgArmAt = time;
            if (!(dt > 0)) dt = 0; else if (dt > 0.1) dt = 0.1;                                              // the first tick / a gap (the tick is 0.01 s)
            bool held = time - HandGunLastShot < ZWorld.HandGunHoldS;
            double look = HandGunLook(out int lx, out int ly) ? HandGunHeadingTo(lx, ly, lx, ly) : double.NaN;
            if (double.IsNaN(look)) { if (!held) hgArmHdg = double.NaN; return; }                           // nothing to rest on: at rest (or held where it is)
            if (double.IsNaN(hgArmHdg)) hgArmHdg = look;                                                      // from rest: on the line to the look point
            double want = look;
            var t = HandGunTarget;
            if (t != null) { double b = HandGunHeadingTo(lx, ly, t.CenterX, t.CenterY); if (!double.IsNaN(b)) want = b; }
            else if (held) want = hgArmHdg;                                                                   // holds on the robot it shot last
            double hi = ZWorld.HandGunOutDeg + ZWorld.HandGunArmSlackDeg, lo = -ZWorld.HandGunInDeg - ZWorld.HandGunArmSlackDeg;
            double armRel = Math.Max(lo, Math.Min(hi, HandGunDeltaDeg(look, hgArmHdg)));                    // never out of the forward arc (the upper body turned)
            double wantRel = Math.Max(lo, Math.Min(hi, HandGunDeltaDeg(look, want)));
            double step = ZWorld.HandGunTurnDegS * dt, d = wantRel - armRel;
            armRel += Math.Max(-step, Math.Min(step, d));
            hgArmHdg = look + armRel;
            if (hgArmHdg > 180.0) hgArmHdg -= 360.0; else if (hgArmHdg <= -180.0) hgArmHdg += 360.0;
        }

        /// <summary>Right before a shot at t (the robot chosen this tick): true = the arm is on it (and, at a new robot, settled); false = no
        /// shot this tick (the cooldown is not used up, the robot stays chosen: the arm turns to it from the next tick on).</summary>
        bool HandGunAimed(double time, ZObject t, int lx, int ly)
        {
            double b = HandGunHeadingTo(lx, ly, t.CenterX, t.CenterY);
            if (double.IsNaN(b) || double.IsNaN(hgArmHdg)) { hgOnSince = -1; HandGunAimWaitTicks++; return false; }
            double off = Math.Abs(HandGunDeltaDeg(hgArmHdg, b));
            if (t != hgAimFor)
            {   // a robot the arm was not on: the turn starts now
                hgAimFor = t; hgOnSince = -1;
                if (t != hgShotAt) { HandGunTargetSwitches++; HandGunLastTurnDeg = off; }
            }
            if (off > ZWorld.HandGunAimTolDeg) { hgOnSince = -1; HandGunAimWaitTicks++; return false; }
            if (hgOnSince < 0) hgOnSince = time;
            if (t != hgShotAt && time + 1e-9 < hgOnSince + ZWorld.HandGunSettleS) { HandGunAimWaitTicks++; return false; }
            hgShotAt = t;
            return true;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV146, the hand gun's aim (ZObject.MechHandGunAim.cs): the arm's turn rate (deg per second; the gun arm of the mech, a
        /// little slower than its upper body's 260), how close (deg) it must be to its robot for a shot, the settle (s) at a new robot after
        /// it got there, how long (s) it holds on the last robot after a shot before it goes back to rest, and how far (deg) it may turn
        /// past the forward arc (the upper body slews, it does not snap).</summary>
        public static double HandGunTurnDegS = 200, HandGunAimTolDeg = 1.0, HandGunSettleS = 0.12, HandGunHoldS = 0.6, HandGunArmSlackDeg = 3;
    }
}
