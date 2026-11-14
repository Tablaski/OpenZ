using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>What a dragon pass tells the view (ZEventType.DragonEvent, radius = kind + 256 x dragon).</summary>
    public enum ZDragonKind
    {
        Start = 0,      // the pass is on; otherId = target
        FlameOn = 1,    // the jet starts; otherId = target
        Bite = 2,       // otherId = target; x, y = where the flame ends (px); value = degrees from straight down; flag = it hit; flag2 = it killed
        FlameOff = 3,   // the jet ends
        Shot = 4,       // objId = the robot that fired; value = flight s; flag = it will hit
        Hit = 5,        // objId = the robot whose shot arrived; value = gun-seconds of life left
        End = 6,        // the pass is over, the dragon flies on
        Down = 7,       // robots killed it; objId = the robot of the last hit
        BreakOff = 8,   // a shuttle came within DragonShipBreakM: it climbs away
        Splash = 9,     // a bite's flame hurt another robot under it; otherId = that robot; x, y = where the flame ends (px); flag2 = it killed
    }

    /// <summary>One order from the view: the plain data of one pass, made between two ticks. Path: h0, m0, h1, m1, h2, m2 as x, y, z in world metres
    /// (x = px / 8, y up, z = -py / 8), the same two Hermite pieces as the view's swoop. GroundY: the ground under the fire point. Rounded to 1/64 m by
    /// DragonOrder, in place, so the view flies what the rules fly and the log holds every number exactly.</summary>
    public sealed class ZDragonOrder
    {
        public int Dragon, Target;
        public readonly double[] Path = new double[18];
        public double T1, T2, GroundY;
    }

    /// <summary>The one running pass: a bird's flight for about 6 s, its flame, its life. Exists only while it runs; a dragon that lives through it is whole next time.</summary>
    public sealed class ZDragonPass
    {
        public int Serial, Dragon, TargetId, Bites, BitesHit, Shots, Hits;
        public bool AtShip, Lit, FlameOut;
        public double StartAt, T1, T2, GroundY, Life, NextScan;
        internal double[] P;
        internal readonly List<ZDragonIncoming> Incoming = new List<ZDragonIncoming>();

        public double FlameOnAt => T1 - ZWorld.DragonFlameLead;
        public double FlameOffAt => FlameOnAt + ZWorld.DragonFlameSeconds;
        public double EndAt => T1 + T2;

        static double H(double a, double ma, double b, double mb, double u)
        {
            double u2 = u * u, u3 = u2 * u;
            return (2 * u3 - 3 * u2 + 1) * a + (u3 - 2 * u2 + u) * ma + (-2 * u3 + 3 * u2) * b + (u3 - u2) * mb;
        }

        /// <summary>The dragon's middle at t s after the order: the view's Hermite (AmbientLife.FollowPath) in doubles.</summary>
        public void PointAt(double t, out double x, out double y, out double z)
        {
            int a; double u, dt;
            if (t < T1) { a = 0; dt = T1; u = Math.Max(0, t) / T1; }
            else if (t < T1 + T2) { a = 6; dt = T2; u = (t - T1) / T2; }
            else { x = P[12]; y = P[13]; z = P[14]; return; }
            x = H(P[a], P[a + 3] * dt, P[a + 6], P[a + 9] * dt, u);
            y = H(P[a + 1], P[a + 4] * dt, P[a + 7], P[a + 10] * dt, u);
            z = H(P[a + 2], P[a + 5] * dt, P[a + 8], P[a + 11] * dt, u);
        }

        /// <summary>The mouth at t: the head's place ahead of and under the middle (critter_defs.py, the glide pose), along the flight.</summary>
        public void MouthAt(double t, out double x, out double y, out double z, out double fx, out double fz)
        {
            PointAt(t, out double x0, out double y0, out double z0);
            PointAt(t + 0.05, out double x1, out _, out double z1);
            double dx = x1 - x0, dz = z1 - z0, l = Math.Sqrt(dx * dx + dz * dz);
            if (l < 1e-6) { dx = P[15]; dz = P[17]; l = Math.Sqrt(dx * dx + dz * dz); if (l < 1e-6) { dx = 1; dz = 0; l = 1; } }
            fx = dx / l; fz = dz / l;
            x = x0 + fx * ZWorld.DragonMouthAhead; y = y0 - ZWorld.DragonMouthBelow; z = z0 + fz * ZWorld.DragonMouthAhead;
        }
    }

    struct ZDragonIncoming { public double At, Gun; public int Shooter; }

    /// <summary>
    /// DV163, version 1.3, level 7 "Pyro Technics": a dragon that happens to fly close to a robot may try to burn it, and robots shoot back.
    ///   order      the view flies the dragons and knows the camera, so it picks the moment (View/AmbientLife.DragonAttack.cs: from plain soaring, a robot or a flying
    ///              shuttle already on the dragon's straight line, on screen) and may only ask, once, between two ticks, with plain data: DragonOrder. The camera is an
    ///              input like the mouse; nothing in the rules reads it. The rules check everything again, roll the chance (a hash, no draw from Zod's rand) and own the
    ///              pass from then on: where the dragon is, each bite, each shot, its life. The same orders at the same ticks give the same world, tick for tick.
    ///   fire       DragonFlameSeconds of flame, DragonBites bites. A bite lands only inside a cone of DragonConeDeg from straight down (top to bottom), at DragonBiteChance,
    ///              as a share of MaxHealth: DragonBitePerMille on a robot (a healthy robot never dies of one pass, a hurt one can, a fire death with no killer), DragonShipPerMille
    ///   ground     every bite off a ship lights DV117's burning ground where the flame ends, hit or miss, as a Pyro's shot does: same patch, same bite a second, but it burns robots of both teams and never a vehicle, no kill credit.
    ///   back       once every DragonScanSeconds the 8 nearest robots in reach, on a second gun channel like the hatch driver's: only with no target of their own (AttackObject
    ///              null), a walking one only if it fires on the move, after DragonReactSeconds, at most DragonElevDeg up. It writes no waypoint, no target, no gun clock of the robot.
    ///              Life counts gun-seconds: a hit takes off the cycle of the gun that fired, at most DragonHitMax, so every robot type does about the same: 1 robot never kills it, 3 grunts about one pass in two, 6 nearly always (the count, line I, set the 3.5). No Zod draw either.
    ///              The Electrobot's lightning never misses and needs his 0.35 s windup, on his 4 s cycle; only while his real gun is charged, and it stays charged: a real enemy gets his bolt as if no dragon were there.
    ///              DragonShipChancePerMille and after its own shorter map gap, DragonShipCoolMap from the last pass of any kind (robots keep DragonCoolMap from it too). A dragon never takes
    ///              a shuttle under DragonShipFloorPerMille of its MaxHealth, so no dragon ever destroys one, on camera or off (the rules do not see the camera); at that floor it is no target.
    /// Deterministic: sim time, object order, hashes. With no order not one line runs (DragonTick's first line). Headless default off; the game turns it on (off in 'Zod exact').
    /// Maps without lava refuse every order (Map.HasLava), and only level 7 has dragons to ask.
    /// </summary>
    public sealed partial class ZWorld
    {
        public static bool DragonDeviation = false;

        // ---- when
        public const double DragonFirstSeconds = 60, DragonCoolOwn = 90, DragonCoolMap = 120, DragonShipCoolMap = 60;
        public const int DragonChancePerMille = 500, DragonShipChancePerMille = 800, DragonShipFloorPerMille = 250;
        // ---- the flame
        public const double DragonFlameLead = 0.7, DragonFlameSeconds = 1.8, DragonConeDeg = 40, DragonBehindM = 2, DragonReachM = 15, DragonShipReachM = 19;
        public const int DragonBites = 9, DragonBiteChancePerMille = 700, DragonBitePerMille = 65, DragonShipPerMille = 20, DragonSplashPx = 20;
        public const double DragonMouthAhead = 1.35, DragonMouthBelow = 0.5;
        /// <summary>The fire point lies this far short of the target on the dragon's line: the flame comes down on it from ahead and it is still under the neck at the last bite.</summary>
        public const double DragonFireShortM = 5;
        // ---- robots shoot back
        public const double DragonLifeGunSeconds = 3.5, DragonHitMax = 0.7, DragonScanSeconds = 0.1, DragonReactSeconds = 0.5, DragonMinCycle = 0.25, DragonElevDeg = 65, DragonAimHold = 0.3, DragonRocketChance = 0.7, DragonRoundMps = 37.5;
        public const int DragonShooters = 8;
        /// <summary>The robot's aim point over the ground, the hull's middle over the pads (m); a passing shuttle this near ends the pass.</summary>
        public const double DragonBodyY = 0.95, DragonShipMidM = 2.2, DragonShipBreakM = 14;

        // ---- answers of DragonOrder
        public const int DragonAccepted = 0, WhyOff = 1, WhyNoLava = 2, WhyEarly = 3, WhyGameOver = 4, WhyBusy = 5, WhyMapCool = 6, WhyOwnCool = 7, WhyTarget = 8, WhyRoll = 9, DragonWhyCount = 10;
        public static readonly string[] DragonWhyName = { "accepted", "rule off", "no lava", "first minute", "game over", "a pass is running", "map cooldown", "own cooldown", "not a target", "chance" };

        // ---- state and counters (checks)
        ZDragonPass dragonPass;
        readonly Dictionary<int, double> dragonNext = new Dictionary<int, double>();
        double dragonMapNext, dragonShipMapNext;
        int dragonSerial;
        readonly ZObject[] dgNear = new ZObject[DragonShooters]; readonly double[] dgNearD = new double[DragonShooters];
        /// <summary>The running pass, null when none.</summary>
        public ZDragonPass DragonPass => dragonPass;
        /// <summary>Checks: skips the chance and both cooldowns, nothing else (never set in a game).</summary>
        public bool DragonProofForce;
        public int DragonAsked, DragonOrders, DragonBiteCount, DragonBitesHit, DragonDealt, DragonShotsFired, DragonShotsHit, DragonKills, DragonBreakOffs, DragonEnds, DragonRobotDeaths;
        public int DragonShipBites, DragonSplashHits; public double DragonMaxDeg, DragonGunTaken;
        public readonly int[] DragonRefused = new int[DragonWhyCount];

        public static int DragonCode(ZDragonKind k, int dragon) => (int)k + 256 * dragon;
        public static ZDragonKind DragonKindOf(ZEvent e) => (ZDragonKind)(e.radius & 255);
        public static int DragonOf(ZEvent e) => e.radius >> 8;
        /// <summary>The roll of an encounter: a hash of the tick and the two ids.</summary>
        public static bool DragonRoll(long tick, int dragon, int target, bool ship = false) => CrocHash((uint)tick, (uint)dragon + 11u, (uint)target) % 1000u < (ship ? DragonShipChancePerMille : DragonChancePerMille);

        /// <summary>The least a dragon leaves a shuttle: DragonShipFloorPerMille of its MaxHealth, at least 1.</summary>
        public static int DragonShipFloorHp(ZObject o) => Math.Max(1, (int)(((long)o.MaxHealth * DragonShipFloorPerMille + 999) / 1000));

        /// <summary>Hit points of one flame bite on this robot or ship.</summary>
        public static int DragonBiteHp(ZObject o)
        {
            int m = o.IsShuttle ? DragonShipPerMille : DragonBitePerMille;
            return Math.Max(1, (int)(((long)o.MaxHealth * m + 999) / 1000));
        }

        /// <summary>A robot that may be attacked, or a shuttle in the air. Never a vehicle, never a landed shuttle.</summary>
        public static bool DragonTargetOk(ZObject o)
        {
            if (o == null || o.Destroyed || o.KillAt >= 0 || o.MaxHealth <= 0) return false;
            if (o.IsShuttle) return o.ShuttleAirborne && o.Health > DragonShipFloorHp(o);
            return o.Kind == ZKind.Robot && !o.IsFortDefender && o.Owner != Team.Neutral;
        }

        /// <summary>The view's cheap test before it plans anything: could this dragon ask now.</summary>
        public bool DragonMayAsk(int dragon)
        {
            if (!DragonDeviation || !Map.HasLava || GameOver || dragonPass != null || Time + 1e-9 < DragonFirstSeconds) return false;
            if (DragonProofForce) return true;
            return Time + 1e-9 >= dragonShipMapNext && !(dragonNext.TryGetValue(dragon, out double n) && Time + 1e-9 < n);   // the shorter gap: a flying shuttle may be asked about first
        }

        /// <summary>DragonMayAsk, and the robots' longer map gap is over too.</summary>
        public bool DragonMayAskRobot(int dragon) => DragonMayAsk(dragon) && (DragonProofForce || Time + 1e-9 >= dragonMapNext);

        static double DragonSnap(double v) => Math.Round(v * 64.0) / 64.0;

        /// <summary>The view's only way in. 0 = accepted, the pass exists from this tick; else the reason (Why*). The encounter costs the dragon its cooldown even when the chance says no.</summary>
        // duel environmental attacks belong to host
            public int DragonOrder(ZDragonOrder q)
        {
            if (DuelIsReplica) return WhyOff;
            DragonAsked++;
            int why = DragonWhy(q);
            if (why != DragonAccepted) { DragonRefused[why]++; return why; }
            if (!DragonProofForce)
            {
                dragonNext[q.Dragon] = Time + DragonCoolOwn;
                if (!DragonRoll(TickIndex, q.Dragon, q.Target, Get(q.Target).IsShuttle)) { DragonRefused[WhyRoll]++; return WhyRoll; }
                dragonMapNext = Time + DragonCoolMap; dragonShipMapNext = Time + DragonShipCoolMap;     // every pass holds both gaps
            }
            for (int i = 0; i < q.Path.Length; i++) q.Path[i] = DragonSnap(q.Path[i]);
            q.T1 = DragonSnap(q.T1); q.T2 = DragonSnap(q.T2); q.GroundY = DragonSnap(q.GroundY);
            var o = Get(q.Target);
            dragonPass = new ZDragonPass { Serial = ++dragonSerial, Dragon = q.Dragon, TargetId = q.Target, AtShip = o.IsShuttle, StartAt = Time, T1 = q.T1, T2 = q.T2, GroundY = q.GroundY,
                                           Life = DragonLifeGunSeconds, NextScan = Time + DragonScanSeconds, P = (double[])q.Path.Clone() };
            DragonOrders++;
            Emit(ZEventType.DragonEvent, null, o, o.CenterX, o.CenterY, 0, DragonCode(ZDragonKind.Start, q.Dragon));
            return DragonAccepted;
        }

        int DragonWhy(ZDragonOrder q)
        {
            if (!DragonDeviation) return WhyOff;
            if (!Map.HasLava) return WhyNoLava;
            if (Time + 1e-9 < DragonFirstSeconds) return WhyEarly;
            if (GameOver) return WhyGameOver;
            if (dragonPass != null) return WhyBusy;
            if (!DragonTargetOk(Get(q.Target))) return WhyTarget;
            if (DragonProofForce) return DragonAccepted;
            if (Time + 1e-9 < (Get(q.Target).IsShuttle ? dragonShipMapNext : dragonMapNext)) return WhyMapCool;
            if (dragonNext.TryGetValue(q.Dragon, out double n) && Time + 1e-9 < n) return WhyOwnCool;
            return DragonAccepted;
        }

        /// <summary>Once a tick, after the lava. The first line is all that runs with no pass.</summary>
        void DragonTick()
        {
            var p = dragonPass;
            if (p == null) return;
            if (GameOver || !DragonDeviation) { DragonClose(p, ZDragonKind.End, null); return; }
            double t = Time - p.StartAt;
            for (int i = 0; i < p.Incoming.Count; i++)
            {
                var h = p.Incoming[i];
                if (Time + 1e-9 < h.At) continue;
                p.Incoming.RemoveAt(i--);
                p.Life -= h.Gun; p.Hits++; DragonShotsHit++; DragonGunTaken += h.Gun;
                var who = GetAny(h.Shooter);
                Emit(ZEventType.DragonEvent, who, null, 0, 0, (float)Math.Max(0, p.Life), DragonCode(ZDragonKind.Hit, p.Dragon));
                if (p.Life <= 1e-9) { DragonKills++; DragonClose(p, ZDragonKind.Down, who); return; }
            }
            if (!p.Lit && t + 1e-9 >= p.FlameOnAt)
            {
                p.Lit = true;
                Emit(ZEventType.DragonEvent, null, Get(p.TargetId), 0, 0, 0, DragonCode(ZDragonKind.FlameOn, p.Dragon));
            }
            if (p.Lit && !p.FlameOut)
            {
                while (p.Bites < DragonBites && t + 1e-9 >= p.FlameOnAt + (p.Bites + 1) * (DragonFlameSeconds / DragonBites)) DragonBite(p, t);
                if (!p.FlameOut && t + 1e-9 >= p.FlameOffAt) DragonFlameDone(p);
            }
            if (Time + 1e-9 >= p.NextScan)
            {
                p.NextScan += DragonScanSeconds;
                if (DragonShipNear(p, t)) { DragonBreakOffs++; DragonClose(p, ZDragonKind.BreakOff, null); return; }
                DragonShots(p, t);
            }
            if (t + 1e-9 >= p.EndAt) { DragonEnds++; DragonClose(p, ZDragonKind.End, null); }
        }

        void DragonFlameDone(ZDragonPass p)
        {
            p.FlameOut = true;
            Emit(ZEventType.DragonEvent, null, null, 0, 0, 0, DragonCode(ZDragonKind.FlameOff, p.Dragon));
        }

        void DragonClose(ZDragonPass p, ZDragonKind how, ZObject by)
        {
            if (p.Lit && !p.FlameOut) DragonFlameDone(p);
            dragonPass = null;
            Emit(ZEventType.DragonEvent, by, null, 0, 0, 0, DragonCode(how, p.Dragon));
        }

        /// <summary>The target's point in world metres: a robot's body over the order's ground, the middle of the hull of a flying ship.</summary>
        void DragonBody(ZDragonPass p, ZObject o, out double x, out double y, out double z)
        {
            x = o.CenterX / 8.0; z = -o.CenterY / 8.0;
            y = p.GroundY + (o.IsShuttle ? o.ShuttleHeightM + DragonShipMidM : DragonBodyY);
        }

        /// <summary>One bite: only inside the cone from straight down, near enough, at the bite's chance. Out of the cone the flame falls on the dragon's own line; a ship that left the cone ends the flame.</summary>
        void DragonBite(ZDragonPass p, double t)
        {
            p.Bites++; DragonBiteCount++;
            var o = Get(p.TargetId);
            p.MouthAt(t, out double mx, out double my, out double mz, out double fx, out double fz);
            double deg = 180; bool inCone = false;
            if (DragonTargetOk(o))
            {
                DragonBody(p, o, out double bx, out double by, out double bz);
                double hx = bx - mx, hz = bz - mz, drop = my - by, flat = Math.Sqrt(hx * hx + hz * hz);
                if (drop > 0)
                {
                    deg = Math.Atan2(flat, drop) * (180.0 / Math.PI);
                    double reach = Math.Sqrt(flat * flat + drop * drop), behind = hx * fx + hz * fz;
                    inCone = deg <= DragonConeDeg && behind >= -DragonBehindM && reach <= (p.AtShip ? DragonShipReachM : DragonReachM);
                }
            }
            if (!inCone && p.AtShip) { DragonFlameDone(p); return; }
            uint roll = CrocHash((uint)TickIndex, (uint)p.Bites + 97u, (uint)p.TargetId) % 1000u;
            bool hit = inCone && roll < DragonBiteChancePerMille;
            int ex, ey;
            if (inCone) { uint s = CrocHash((uint)TickIndex, 3u, (uint)p.Bites); ex = o.CenterX + (hit ? 0 : (int)(s % 25u) - 12); ey = o.CenterY + (hit ? 0 : (int)((s >> 8) % 25u) - 12); }
            else { ex = (int)Math.Round((mx + fx * 3) * 8); ey = (int)Math.Round(-(mz + fz * 3) * 8); }
            if (!p.AtShip) GroundFireLight(ex, ey, Team.Neutral, -1);                                  // DV117's burning ground, the view draws it here: nothing under a flying ship
            bool killed = false;
            if (hit)
            {
                p.BitesHit++; DragonBitesHit++; if (deg > DragonMaxDeg) DragonMaxDeg = deg;
                int hp0 = o.Health;
                o.DamageHealth(o.IsShuttle ? Math.Min(DragonBiteHp(o), o.Health - DragonShipFloorHp(o)) : DragonBiteHp(o));   // a shuttle never under its floor
                if (o.IsShuttle) DragonShipBites++; else o.LastFireHit = Time;                          // a death in it is a fire death, as in lava
                DragonDealt += hp0 - o.Health;
                killed = o.Destroyed;
                if (killed && !o.IsShuttle) DragonRobotDeaths++;
                UpdateObjectHealth(o, -1);                                                               // no killer, no kill credit
            }
            Emit(ZEventType.DragonEvent, null, o, ex, ey, (float)Math.Min(deg, 180), DragonCode(ZDragonKind.Bite, p.Dragon), hit, killed);
            if (!p.AtShip) DragonSplash(p, ex, ey);
        }

        /// <summary>The flame lands on the whole squad: every other robot of either team within DragonSplashPx of its end takes the same bite at the same chance, a hash of tick, bite and that robot. No killer, no alert, Objects order.</summary>
        void DragonSplash(ZDragonPass p, int ex, int ey)
        {
            int n = Objects.Count;
            for (int i = 0; i < n && i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.Id == p.TargetId || o.IsShuttle || !DragonTargetOk(o)) continue;
                long dx = o.CenterX - ex, dy = o.CenterY - ey;
                if (dx * dx + dy * dy > DragonSplashPx * DragonSplashPx) continue;
                if (CrocHash((uint)TickIndex, (uint)p.Bites + 197u, (uint)o.Id) % 1000u >= DragonBiteChancePerMille) continue;
                int hp0 = o.Health;
                o.DamageHealth(DragonBiteHp(o));
                o.LastFireHit = Time;
                DragonDealt += hp0 - o.Health; DragonSplashHits++;
                bool killed = o.Destroyed;
                if (killed) DragonRobotDeaths++;
                UpdateObjectHealth(o, -1);
                Emit(ZEventType.DragonEvent, null, o, ex, ey, 0, DragonCode(ZDragonKind.Splash, p.Dragon), true, killed);
            }
        }

        /// <summary>A flying shuttle that is not the target, within DragonShipBreakM of the dragon, by the rules' own numbers.</summary>
        bool DragonShipNear(ZDragonPass p, double t)
        {
            if (ShuttlesMadeCount == 0) return false;
            p.PointAt(t, out double x, out double y, out double z);
            foreach (var o in Objects)
            {
                if (!o.IsShuttle || o.Id == p.TargetId || o.Destroyed || o.KillAt >= 0 || !o.ShuttleAirborne) continue;
                double dx = o.CenterX / 8.0 - x, dz = -o.CenterY / 8.0 - z, dy = p.GroundY + o.ShuttleHeightM + DragonShipMidM - y;
                if (dx * dx + dy * dy + dz * dz < DragonShipBreakM * DragonShipBreakM) return true;
            }
            return false;
        }

        /// <summary>Robots shoot back: a second gun channel. Written on a robot: dg clocks and its aim mark, nothing else.</summary>
        void DragonShots(ZDragonPass p, double t)
        {
            p.PointAt(t, out double x, out double y, out double z);
            double px = x * 8, py = -z * 8, up = (y - (p.GroundY + DragonBodyY)) * 8;
            if (up <= 0) return;
            int n = 0;
            int count = Objects.Count;
            for (int i = 0; i < count; i++)
            {
                var o = Objects[i];
                if (o.Kind != ZKind.Robot || o.Destroyed || o.KillAt >= 0 || o.Owner == Team.Neutral || o.IsFortDefender || !o.CanAttack()) continue;
                double dx = o.CenterX - px, dy = o.CenterY - py, flat2 = dx * dx + dy * dy, d2 = flat2 + up * up;
                if (d2 > (double)o.AttackRadius * o.AttackRadius) continue;
                if (Math.Atan2(up, Math.Sqrt(flat2)) * (180.0 / Math.PI) > DragonElevDeg) continue;
                int at = n < DragonShooters ? n++ : DragonShooters - 1;
                if (at == DragonShooters - 1 && n == DragonShooters && dgNear[at] != null && dgNearD[at] <= d2) continue;
                while (at > 0 && dgNearD[at - 1] > d2) { dgNear[at] = dgNear[at - 1]; dgNearD[at] = dgNearD[at - 1]; at--; }   // object order on a tie
                dgNear[at] = o; dgNearD[at] = d2;
            }
            for (int i = 0; i < n; i++)
            {
                var o = dgNear[i]; dgNear[i] = null;
                if (o.dgSeenPass != p.Serial) { o.dgSeenPass = p.Serial; o.dgSeenAt = Time; }
                if (o.AttackObject != null || o.Stunned || o.KnockedDown(Time)) continue;                 // a robot in a real fight stays in it
                bool moving = o.IsMoving, electro = o.IsElectrobot;
                if (moving && !o.FiresOnMove) continue;                                                    // it does not stop for a dragon
                if (electro && o.ElPhase != ElectroPhase.Charged) continue;                                // his real gun is busy; that clock is only read
                o.DragonAimUntil = Time + DragonAimHold;
                if (Time + 1e-9 < o.dgSeenAt + DragonReactSeconds || Time + 1e-9 < o.dgNextShot) continue;
                double cycle = electro ? ZElectro.Seconds(ZElectro.CycleTicks) : Math.Max(DragonMinCycle, o.AttackSpeed);
                o.dgNextShot = Time + cycle;
                bool rocket = o.DamageIsMissile;
                double chance = rocket ? DragonRocketChance : o.HitChance();
                if (moving) chance *= WalkHitFactor;
                p.Shots++; DragonShotsFired++;
                bool hit = electro || CrocHash((uint)TickIndex, (uint)o.Id + 41u, (uint)p.Serial) % 1000u < (uint)(chance * 1000);   // lightning never misses (ZObject.Electro.cs)
                double mps = rocket && o.MissileSpeed > 0 ? o.MissileSpeed / 8.0 : DragonRoundMps;
                double flight = electro ? ZElectro.Seconds(ZElectro.WindupTicks) : Math.Sqrt(dgNearD[i]) / 8.0 / mps;      // his: the windup, the bolt itself is instant
                if (hit) p.Incoming.Add(new ZDragonIncoming { At = Time + flight, Gun = Math.Min(cycle, DragonHitMax), Shooter = o.Id });
                Emit(ZEventType.DragonEvent, o, null, 0, 0, (float)flight, DragonCode(ZDragonKind.Shot, p.Dragon), hit);
            }
        }

        /// <summary>Turns the rule off until it is disposed, then puts back what was there (the parity report holds one while it measures).</summary>
        public sealed class DragonHold : IDisposable
        {
            readonly bool was;
            internal DragonHold() { was = DragonDeviation; DragonDeviation = false; }
            public void Dispose() { DragonDeviation = was; }
        }
        public static DragonHold DragonOff() => new DragonHold();
    }

    public partial class ZObject
    {
        internal double dgNextShot, dgSeenAt; internal int dgSeenPass;
        /// <summary>DV163: sim time until which this robot aims at the dragon.</summary>
        public double DragonAimUntil;
        /// <summary>DV163: the robot is aiming at a dragon now. Never while it has a real target. View only: the torso turns, no rule reads it.</summary>
        public bool DragonAiming => World != null && DragonAimUntil > World.Time && AttackObject == null;
    }
}
