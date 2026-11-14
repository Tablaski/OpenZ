using System;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Part 9 of the DV146 counted check (Editor/OzMechHandGunCount.cs calls it before its balance part): THE GUN TURNS FIRST
    /// (Sim/ZObject.MechHandGunAim.cs). Headless, public members of the simulation only, every angle measured again here with this file's
    /// own maths (the gun's place HandGunSidePx to the mech's right of the line to its rocket target):
    ///  9a two robots that fall after 3 hits and one that does not, at +28 / -12 / +8 deg from the gun's line to the rocket target (a jeep):
    ///     every shot leaves with the arm on its robot (within HandGunAimTolDeg); at every new robot the first shot waits the turn (the arm's
    ///     angle to it / HandGunTurnDegS) + HandGunSettleS, to the tick; the arm never turns faster than its rate nor out of the arc + slack;
    ///  9b the rocket target is the robot (on the arm's rest line): the first shot waits only the settle; in every later reload window the
    ///     first shot comes as the window opens (the arm still holds on it);
    ///  9c the same seed twice: the same shots at the same ticks.
    /// </summary>
    public static class OzMechHandGunAimCount
    {
        const int Big = 1000000000;
        const int FX = 700, FY = 700;

        static ZObject Mech(ZWorld w)
        {
            var m = OzV11Kit.Place(w, "medium", FX, FY, Team.Blue);
            m.Drivers[0] = Big; m.MaxHealth = m.Health = Big; m.MoveSpeed = 0; m.RealMoveSpeed = 0; m.DamageRadius = 0;
            m.ResetDamageInfo();
            return m;
        }

        static ZObject Still(ZWorld w, string type, int cx, int cy, int hp, int amount = -1)
        {
            var o = OzV11Kit.Place(w, type, cx, cy, Team.Red, amount);
            foreach (var q in OzV11Kit.Squad(o)) { q.MaxHealth = q.Health = hp; q.Damage = 0; q.MoveSpeed = 0; q.RealMoveSpeed = 0; if (q.Drivers.Count > 0) q.Drivers[0] = Big; }
            return o;
        }

        static ZWorld World(string settings, int seed)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(96, 96), seed);
            var d = w.CreateRobotGroup("grunt", 40, 1480, Team.Red, 1);
            d.MoveSpeed = 0; d.Damage = 0; d.MaxHealth = d.Health = Big;
            OzV11Kit.Dummy(w, 80, 1480);
            return w;
        }

        /// <summary>This file's own maths: the gun's place for the look point (lx, ly) and the map heading (deg) from it to (px, py).</summary>
        static double Heading(ZObject mech, int lx, int ly, double px, double py)
        {
            double fx = lx - mech.CenterX, fy = ly - mech.CenterY, l = Math.Sqrt(fx * fx + fy * fy); fx /= l; fy /= l;
            double gx = mech.CenterX + (-fy) * ZWorld.HandGunSidePx, gy = mech.CenterY + fx * ZWorld.HandGunSidePx;
            return Math.Atan2(py - gy, px - gx) * 180.0 / Math.PI;
        }

        static double Wrap(double d) { d %= 360.0; if (d > 180) d -= 360; else if (d < -180) d += 360; return d; }

        /// <summary>A robot `px` from the gun at `deg` from its line to the point (tx, ty) (+ = outward = the mech's right).</summary>
        static (int x, int y) At(int tx, int ty, double deg, double px)
        {
            double gx = FX, gy = FY + ZWorld.HandGunSidePx, ax = tx - gx, ay = ty - gy, al = Math.Sqrt(ax * ax + ay * ay);
            double c = Math.Cos(deg * Math.PI / 180), s = Math.Sin(deg * Math.PI / 180);
            return ((int)(gx + (ax * c - ay * s) / al * px), (int)(gy + (ax * s + ay * c) / al * px));
        }

        sealed class Run
        {
            public int shots, offArm, fast, outArc, switches, timed, early, late, carried, windows, windowLate;
            public double worstOff, worstFast, worstEarly, worstLate, maxTurn;
            public long hash = 17;
            public readonly List<string> rows = new List<string>();
        }

        /// <summary>Ticks the world, checks every tick and every shot of the mech (see the class summary).</summary>
        static Run Tick(ZWorld w, ZObject m, int lx, int ly, double seconds, bool rows)
        {
            var r = new Run();
            double tol = ZWorld.HandGunAimTolDeg, rate = ZWorld.HandGunTurnDegS, settle = ZWorld.HandGunSettleS, tick = ZConst.TICK;
            double prev = double.NaN, look = Heading(m, lx, ly, lx, ly);
            ZObject aimFor = null, lastShotAt = null; double switchAt = -1, turn = 0; bool pending = false, sawWindow = false; double windowFrom = -1; bool windowShot = false;
            while (w.Time < seconds)
            {
                w.Tick(tick);
                double now = w.Time, arm = m.HandGunArmHeading;
                if (!double.IsNaN(arm))
                {
                    if (!double.IsNaN(prev))
                    {
                        double step = Math.Abs(Wrap(arm - prev));
                        if (step > rate * tick + 1e-6) { r.fast++; r.worstFast = Math.Max(r.worstFast, step); }
                    }
                    double rel = Wrap(arm - look);
                    if (rel > ZWorld.HandGunOutDeg + ZWorld.HandGunArmSlackDeg + 1e-6 || rel < -ZWorld.HandGunInDeg - ZWorld.HandGunArmSlackDeg - 1e-6) r.outArc++;
                }
                prev = arm;
                bool open = m.HandGunWindow(now);
                if (open && !sawWindow) { windowFrom = now; windowShot = false; }
                if (!open && pending) { pending = false; r.carried++; }                                     // the window closed during a turn: not timed
                sawWindow = open;
                var af = m.HandGunAimTarget;
                if (af != null && af != aimFor && af != lastShotAt)
                {   // a new robot: the turn the arm needs now (its heading before this tick's step, the sim's own record)
                    pending = true; switchAt = now; turn = m.HandGunLastTurnDeg; r.switches++; r.maxTurn = Math.Max(r.maxTurn, turn);
                }
                aimFor = af;
                foreach (var e in w.Events)
                {
                    if (e.objId != m.Id || e.type != ZEventType.MechHandGun) continue;
                    r.shots++;
                    var t = w.GetAny(e.otherId);
                    if (t != null)
                    {
                        double off = Math.Abs(Wrap(Heading(m, lx, ly, t.CenterX, t.CenterY) - arm));
                        if (off > tol + 1e-6) { r.offArm++; r.worstOff = Math.Max(r.worstOff, off); }
                    }
                    if (open && !windowShot)
                    {
                        windowShot = true; r.windows++;
                        if (t == lastShotAt && now > windowFrom + tick + 1e-6) r.windowLate++;                    // the same robot as before: at once
                    }
                    if (pending && t == af)
                    {
                        pending = false; r.timed++;
                        double want = Math.Max(0, turn - tol) / rate + settle, got = now - switchAt;
                        if (got < want - 1e-6) { r.early++; r.worstEarly = Math.Max(r.worstEarly, want - got); }
                        if (got > want + tick + 1e-6) { r.late++; r.worstLate = Math.Max(r.worstLate, got - want); }
                        if (rows && r.rows.Count < 8) r.rows.Add($"turn {turn:0.0} deg: first shot {got:0.00} s after the robot was chosen (the rule {want:0.00} s)");
                    }
                    lastShotAt = t;
                    unchecked { r.hash = r.hash * 31 + (long)(now * 1000) * 7 + e.otherId * 3 + (e.flag ? 1 : 0); }
                }
                w.Events.Clear();
            }
            return r;
        }

        public static void Parts(StringBuilder sb, string settings, int runs)
        {
            var ps = ZSettings.Parse(settings).Unit(ZWorld.HandGunType);
            int hp3 = ps.Damage * 3;
            sb.AppendLine($"Part 9, the gun turns first: the arm turns at {ZWorld.HandGunTurnDegS:0} deg/s, shoots within {ZWorld.HandGunAimTolDeg:0.#} deg of its robot, " +
                          $"settles {ZWorld.HandGunSettleS:0.00} s at a new robot, holds {ZWorld.HandGunHoldS:0.0} s after a shot, stays within the arc + {ZWorld.HandGunArmSlackDeg:0} deg.");
            // ---------------------------------------------------------------- 9a switching robots
            var all = new Run(); var rows = new List<string>();
            for (int k = 0; k < Math.Max(3, runs); k++)
            {
                var w = World(settings, 900 + k);
                var m = Mech(w);
                var jeep = Still(w, "jeep", FX + 110, FY, Big);
                var a = At(jeep.CenterX, jeep.CenterY, 28, 70); var b = At(jeep.CenterX, jeep.CenterY, -12, 70); var c = At(jeep.CenterX, jeep.CenterY, 8, 80);
                Still(w, "grunt", a.x, a.y, hp3, 1); Still(w, "grunt", b.x, b.y, hp3, 1); Still(w, "grunt", c.x, c.y, Big, 1);
                OzV11Kit.PlayerAttack(w, m, jeep);
                var r = Tick(w, m, jeep.CenterX, jeep.CenterY, 16, k == 0);
                if (k == 0) rows.AddRange(r.rows);
                all.shots += r.shots; all.offArm += r.offArm; all.fast += r.fast; all.outArc += r.outArc; all.switches += r.switches; all.timed += r.timed;
                all.early += r.early; all.late += r.late; all.carried += r.carried;
                all.worstOff = Math.Max(all.worstOff, r.worstOff); all.worstFast = Math.Max(all.worstFast, r.worstFast); all.worstEarly = Math.Max(all.worstEarly, r.worstEarly);
                all.worstLate = Math.Max(all.worstLate, r.worstLate); all.maxTurn = Math.Max(all.maxTurn, r.maxTurn);
            }
            sb.AppendLine($"Part 9a, {Math.Max(3, runs)} fights of 16 s (robots at +28 and -12 deg that fall after 3 hits, one at +8 that does not): {all.shots} shots, {all.switches} new robots " +
                          $"(the largest turn {all.maxTurn:0.0} deg), {all.timed} of them timed ({all.carried} turns ran into a closed window). First fight: " + string.Join("; ", rows));
            sb.AppendLine(OzV11Kit.Verdict(all.shots > 0 && all.offArm == 0, $"every shot with the arm on its robot: {all.offArm} of {all.shots} more than {ZWorld.HandGunAimTolDeg:0.#} deg off (worst {all.worstOff:0.00})"));
            sb.AppendLine(OzV11Kit.Verdict(all.timed >= 2 && all.early == 0 && all.late == 0, $"the first shot at a new robot waits the turn + the settle, to the tick: {all.early} early (worst {all.worstEarly:0.000} s), {all.late} late (worst {all.worstLate:0.000} s) of {all.timed}"));
            sb.AppendLine(OzV11Kit.Verdict(all.maxTurn >= 20, $"it really turns between robots: the largest turn {all.maxTurn:0.0} deg (wanted >= 20)"));
            sb.AppendLine(OzV11Kit.Verdict(all.fast == 0 && all.outArc == 0, $"the arm: {all.fast} ticks faster than {ZWorld.HandGunTurnDegS:0} deg/s (worst {all.worstFast:0.00} deg a tick), {all.outArc} ticks outside the arc + slack"));

            // ---------------------------------------------------------------- 9b the robot on the rest line
            int windows = 0, late = 0, early9b = 0, timed9b = 0, shots9b = 0;
            for (int k = 0; k < Math.Max(3, runs); k++)
            {
                var w = World(settings, 950 + k);
                var m = Mech(w);
                var sq = Still(w, "grunt", FX + 100, FY, Big, 1);
                OzV11Kit.PlayerAttack(w, m, sq);
                var r = Tick(w, m, sq.CenterX, sq.CenterY, 16, false);
                windows += r.windows; late += r.windowLate; timed9b += r.timed; early9b += r.early + r.late; shots9b += r.shots;
            }
            sb.AppendLine(OzV11Kit.Verdict(shots9b > 0 && timed9b >= Math.Max(3, runs) && early9b == 0, $"a robot on the arm's rest line (the rocket target): the first shot waits the settle only ({timed9b} timed, {early9b} off the rule)"));
            sb.AppendLine(OzV11Kit.Verdict(windows > timed9b && late == 0, $"the same robot in the next reload window is shot as the window opens: {late} of {windows - timed9b} later windows late"));

            // ---------------------------------------------------------------- 9c deterministic
            long h1 = 0, h2 = 0;
            for (int i = 0; i < 2; i++)
            {
                var w = World(settings, 977);
                var m = Mech(w);
                var jeep = Still(w, "jeep", FX + 110, FY, Big);
                var a = At(jeep.CenterX, jeep.CenterY, 28, 70); var b = At(jeep.CenterX, jeep.CenterY, -12, 70);
                Still(w, "grunt", a.x, a.y, hp3, 1); Still(w, "grunt", b.x, b.y, Big, 1);
                OzV11Kit.PlayerAttack(w, m, jeep);
                long h = Tick(w, m, jeep.CenterX, jeep.CenterY, 12, false).hash;
                if (i == 0) h1 = h; else h2 = h;
            }
            sb.AppendLine(OzV11Kit.Verdict(h1 == h2, $"deterministic with the aim: the same seed twice, the same shots at the same ticks ({h1:X} / {h2:X})"));
        }
    }
}
