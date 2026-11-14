using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ deviation DV146 "the mech's hand gun" (version 1.2): the machine gun in the mech's right hand fires. The mech is Zod's
    /// medium tank (type "medium", both of its models); Zod gives it one weapon, the rocket pods (two rockets per shot, View/MechRockets.cs).
    /// Now, between two rocket shots, the gun in its right hand fires the PSYCHO's machine gun at enemy ROBOTS:
    /// <list type="bullet">
    /// <item>numbers: the Psycho's own row of the rule table (<see cref="ZWorld.HandGunType"/>): its damage (and its rifle-pair value
    /// against a robot type where the pair rule has one, DV78), its hit chance (Zod's hit roll, DV122's smaller kneeling / lying sniper),
    /// its cycle and its range (measured from the mech's centre, like every Zod range). No driver sniping, no splash.</item>
    /// <item>when: only while the rockets reload: from <see cref="ZWorld.HandGunAfterRocket"/> s after a rocket shot to
    /// <see cref="ZWorld.HandGunBeforeRocket"/> s before the next one is due (the mech's own cycle), and only while the mech has its
    /// rocket target (the upper body looks at it). Grenade throws are not rocket shots.</item>
    /// <item>at what: enemy robots only (never vehicles, guns, buildings or map items), in its range, no Zod barrier and a clear line in
    /// between (DV23), at least <see cref="ZWorld.HandGunMinPx"/> from the gun, and inside the gun's FORWARD ARC: measured at the gun
    /// (the mech's centre + <see cref="ZWorld.HandGunSidePx"/> to the mech's right, square to the line to its rocket target), from the
    /// line gun - rocket target: up to <see cref="ZWorld.HandGunOutDeg"/> outward (the mech's right) and <see cref="ZWorld.HandGunInDeg"/>
    /// inward (toward its body). A robot outside the arc is not shot by it. The arc is inside what both models' arms can reach without the
    /// gun turning into the body or backwards (measured on the models: k86c tools/arm_reach.py).</item>
    /// <item>target: the rocket target itself when it is such a robot, else the robot it shot at before while it stays valid, else the
    /// valid robot nearest to the mech (looked for every 0.5 s, at once when a reload window opens; the first in object order when two
    /// are equally near).</item>
    /// <item>who: an owned, crewed mech (a sniped / empty one, a destroyed one: never), the player's and the computer's alike (no order:
    /// the computer uses it by itself). A stunned mech fires it too (as its rockets and its hatch driver, DV45 / DV87).</item>
    /// </list>
    /// Each shot: <see cref="ZEventType.MechHandGun"/> (objId = the mech, otherId = the robot, x / y = its centre, value = HP removed,
    /// flag = hit, flag2 = killed); a hit books the kill and the squad's help call as every direct hit. Deterministic: sim time and the
    /// world's Zod rand() stream only (one hit roll per shot, as the Psycho's), no frame time, no view state.
    /// Switch: <see cref="ZWorld.MechHandGunDeviation"/> (the game turns it on at every start, off in 'Zod exact'; headless default off).
    /// View: View/MechHandGunView.cs (the arm aims within the arc, the Psycho's tracers, muzzle flash, burst sound, casings).
    /// </summary>
    public partial class ZObject
    {
        double hgRocketAt = -100, hgNextShot, hgNextScan, hgWindowSeen = -100; int hgAimX, hgAimY; ZObject hgTarget;

        /// <summary>DV146: this unit carries the hand gun (the mech, with the rule on), and DV149 has not knocked it off (ZObject.MechGunLoss.cs).</summary>
        public bool HasHandGun => ZWorld.MechHandGunDeviation && Kind == ZKind.Vehicle && Type == ZWorld.HandGunCarrier && !HandGunLost;
        /// <summary>DV146: sim time of this mech's last rocket shot (-100 = none yet).</summary>
        public double HandGunRocketAt => hgRocketAt;
        /// <summary>DV146: the robot the hand gun shoots at now (null = none). View: the arm aims at it.</summary>
        public ZObject HandGunTarget => hgTarget != null && !hgTarget.Destroyed && hgTarget.KillAt < 0 ? hgTarget : null;
        /// <summary>DV146 counters (checks): shots and hits of this mech's hand gun; sim time of its last shot (-100 = never).</summary>
        public int HandGunShots { get; private set; }
        public int HandGunHits { get; private set; }
        public double HandGunLastShot { get; private set; } = -100;

        /// <summary>DV146: the reload window of the rockets is open now (after the last rocket shot's lead-out, before the next one's lead-in).</summary>
        public bool HandGunWindow(double time) =>
            hgRocketAt >= 0 && time + 1e-9 >= hgRocketAt + ZWorld.HandGunAfterRocket && time + 1e-9 < hgRocketAt + AttackSpeed - ZWorld.HandGunBeforeRocket;

        /// <summary>ProcessAttackDamage, right after a rocket left (MissileFired, not a grenade): the reload window starts.</summary>
        void HandGunRocketFired(double time, int tx, int ty)
        {
            if (!HasHandGun) return;
            hgRocketAt = time; hgAimX = tx; hgAimY = ty;
        }

        /// <summary>The point the upper body looks at: the rocket target's centre (null = it has none: no hand-gun shot).</summary>
        bool HandGunLook(out int lx, out int ly)
        {
            lx = hgAimX; ly = hgAimY;
            var t = AttackObject;
            if (t == null || t.Destroyed || t.KillAt >= 0) return false;
            lx = t.CenterX; ly = t.CenterY;
            return true;
        }

        /// <summary>DV146: the angle (deg) of the robot o from the gun's aim line at the look point: + = outward (the mech's right),
        /// - = inward; NaN = the look point or o too close to the gun to measure. Public for the checks (the same maths).</summary>
        public double HandGunArcDeg(ZObject o, int lx, int ly)
        {
            double fx = lx - CenterX, fy = ly - CenterY, fl = Math.Sqrt(fx * fx + fy * fy);
            if (o == null || fl < 1.0) return double.NaN;
            fx /= fl; fy /= fl;
            double gx = CenterX - fy * ZWorld.HandGunSidePx, gy = CenterY + fx * ZWorld.HandGunSidePx;   // the mech's right in map pixels (y down): (-fy, fx)
            double ax = lx - gx, ay = ly - gy, bx = o.CenterX - gx, by = o.CenterY - gy;
            if (ax * ax + ay * ay < 1.0 || bx * bx + by * by < 1.0) return double.NaN;
            return Math.Atan2(ax * by - ay * bx, ax * bx + ay * by) * (180.0 / Math.PI);                // + = clockwise on the map = to the right
        }

        /// <summary>DV146: the distance (px) from the gun (as in HandGunArcDeg) to o.</summary>
        public double HandGunGunDist(ZObject o, int lx, int ly)
        {
            double fx = lx - CenterX, fy = ly - CenterY, fl = Math.Sqrt(fx * fx + fy * fy);
            if (o == null || fl < 1.0) return 0;
            fx /= fl; fy /= fl;
            double gx = CenterX - fy * ZWorld.HandGunSidePx, gy = CenterY + fx * ZWorld.HandGunSidePx;
            return Math.Sqrt((o.CenterX - gx) * (o.CenterX - gx) + (o.CenterY - gy) * (o.CenterY - gy));
        }

        /// <summary>A robot the hand gun may shoot: an enemy robot, alive, in the Psycho's range from the mech's centre, not too close to the
        /// gun, inside the forward arc, no Zod barrier, a clear line.</summary>
        bool HandGunValid(ZObject o, int radius, int lx, int ly)
        {
            if (o == null || o == this || o.Kind != ZKind.Robot || o.Destroyed || o.KillAt >= 0 || o.Owner == Team.Neutral || o.Owner == Owner) return false;
            if (!Within(CenterX, CenterY, o.CenterX, o.CenterY, HighRange(radius, o))) return false;
            if (HandGunGunDist(o, lx, ly) < ZWorld.HandGunMinPx) return false;
            double a = HandGunArcDeg(o, lx, ly);
            if (double.IsNaN(a) || a > ZWorld.HandGunOutDeg || a < -ZWorld.HandGunInDeg) return false;
            if (World.EngageBarrierBetween(CenterX, CenterY, o.CenterX, o.CenterY)) return false;
            return HasLineOfSight(o);
        }

        ZObject HandGunNearest(int radius, int lx, int ly)
        {
            ZObject best = null; long bd = long.MaxValue;
            foreach (var o in World.Objects)
            {
                if (o.Kind != ZKind.Robot || o.Owner == Owner || o.Owner == Team.Neutral) continue;
                long dx = o.CenterX - CenterX, dy = o.CenterY - CenterY, d = dx * dx + dy * dy;
                if (d >= bd || !HandGunValid(o, radius, lx, ly)) continue;
                bd = d; best = o;
            }
            return best;
        }

        /// <summary>DV146: HP one hit removes: the Psycho's damage, his rifle-pair value against this robot type when the pair rule has a
        /// bigger one (DV78; never on a fort's roof defender, as on foot).</summary>
        public int HandGunDamageOn(ZObject t)
        {
            var u = World == null ? null : World.S.Unit(ZWorld.HandGunType);
            if (u == null) return 0;
            int d = u.Damage;
            if (t != null && ZWorld.RiflePairDeviation && t.IsRobot && !t.IsFortDefender && ZWorld.RiflePair.TryGetValue((ZWorld.HandGunType, t.Type), out int pair) && pair > d) d = pair;
            return d;
        }

        /// <summary>End of ProcessServer and of the stun hold, every tick, right after the hatch driver's step (DV87).</summary>
        void MechHandGunStep(double time)
        {
            if (!HasHandGun) return;
            HandGunArmStep(time);                                                                             // the arm turns first (ZObject.MechHandGunAim.cs)
            if (Destroyed || Owner == Team.Neutral || Drivers.Count == 0 || !HandGunWindow(time)) { hgTarget = null; return; }
            if (hgWindowSeen != hgRocketAt) { hgWindowSeen = hgRocketAt; hgNextScan = 0; }                 // a new window: look for a robot at once
            if (time < hgNextShot) return;                                                                    // the Psycho's own cooldown test (zobject.cpp:2113)
            var u = World.S.Unit(ZWorld.HandGunType);
            if (u == null) return;
            double cycle = ZWorld.ReloadOf(ZWorld.HandGunType, u);
            if (u.Damage <= 0 || cycle <= 0) return;
            if (!HandGunLook(out int lx, out int ly)) { hgTarget = null; return; }
            int radius = u.attack_radius;
            var t = AttackObject;                                                                             // the rocket target first, when it is such a robot
            if (!HandGunValid(t, radius, lx, ly))
            {
                t = hgTarget;
                if (!HandGunValid(t, radius, lx, ly))
                {
                    t = null;
                    if (time >= hgNextScan) { hgNextScan = time + 0.5; t = HandGunNearest(radius, lx, ly); }  // Zod's passive check interval (zobject.cpp:1603)
                }
            }
            hgTarget = t;
            if (t == null) return;
            if (!HandGunAimed(time, t, lx, ly)) return;                                                       // no shot before the arm is on it (ZObject.MechHandGunAim.cs)
            hgNextShot = time + cycle; HandGunLastShot = time;
            HandGunShots++; if (ZWorld.HandGunShotsFired < int.MaxValue) ZWorld.HandGunShotsFired++;
            if (CoverMiss(t, World.Rng.Roll(), ZWorld.HitChanceOf(ZWorld.HandGunType, u)))                  // Zod's hit roll (zobject.cpp:2187), DV122
            {
                World.Emit(ZEventType.MechHandGun, this, t, t.CenterX, t.CenterY, 0, 0, false, false);
                return;
            }
            HandGunHits++;
            int hp0 = t.Health;
            t.DamageHealth(HandGunDamageOn(t)); t.LastEnemyHit = time;
            World.Emit(ZEventType.MechHandGun, this, t, t.CenterX, t.CenterY, hp0 - t.Health, 0, true, t.Destroyed);
            World.UpdateObjectHealth(t, Id);                                                                  // the death relay with the mech as the attacker (the kill goes to its crew, DV12)
            if (t.Health < hp0) World.HelpCall(t, this);                                                      // DV53
            if (t.Destroyed) hgTarget = null;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV146 the mech's hand gun (ZObject.MechHandGun.cs). The game turns it on at every start (off in 'Zod exact'); headless default off.</summary>
        public static bool MechHandGunDeviation = false;
        /// <summary>DV146: the unit type that carries it (the mech = Zod's medium tank) and the robot type whose row of the rule table it fires.</summary>
        public const string HandGunCarrier = "medium", HandGunType = "psycho";
        /// <summary>DV146: seconds after a rocket shot before the hand gun may fire (the twin rocket leaves the right pod 0.15 to 0.25 s after
        /// the first, the upper body's kick settles) and seconds before the next rocket shot is due when it stops.</summary>
        public static double HandGunAfterRocket = 0.35, HandGunBeforeRocket = 0.15;
        /// <summary>DV146: the forward arc (deg) from the gun's line to the rocket target: outward (the mech's right) and inward (toward its body).
        /// </summary>
        public static double HandGunOutDeg = 35, HandGunInDeg = 15;
        /// <summary>DV146: the gun's place: px to the mech's right of its centre (v1's muzzle 1.77 m off the waist's axis = 14 px), and the
        /// closest a robot may be to it (px; the arm's pitch down stays inside its reach).</summary>
        public static double HandGunSidePx = 14, HandGunMinPx = 24;
        /// <summary>DV146 statistics (checks, all worlds): hand-gun shots fired.</summary>
        public static int HandGunShotsFired;

        /// <summary>DV146: the rule off for a block of code (the parity report's Zod rows and other probes; a Play session leaves the switch on).</summary>
        public static IDisposable HandGunOff() => new HandGunOffHold();
        sealed class HandGunOffHold : IDisposable
        {
            readonly bool was = MechHandGunDeviation;
            public HandGunOffHold() { MechHandGunDeviation = false; }
            public void Dispose() { MechHandGunDeviation = was; }
        }
    }
}
