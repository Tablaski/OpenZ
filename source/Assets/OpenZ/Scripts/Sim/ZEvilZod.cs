using System;

namespace OpenZ.Sim
{
    /// <summary>The power dash's phases (ZObject.EvilPhase). The plant after the burst is no phase: he stands on his own feet again and the gun waits.</summary>
    public enum ZEvilPhase { None = 0, Windup = 1, Burst = 2 }

    /// <summary>
    /// testing panel. No level, no factory, no computer build ever makes him, so while nobody places him no hook of this file does anything.
    /// <list type="bullet">
    /// <item>He is not a hero squad kind (those are grunts in costume): his own row, filled in code (<see cref="Fill"/>), and one flag on the
    /// object, <see cref="ZObject.IsEvilZod"/>, set once at creation. The row: the heavy tank's hit points and walking speed (his real walk is <see cref="WalkFactor"/> x that), the grunt's
    /// range, no running, no build time. It is made from the parsed text, so default_settings.txt does not change.</item>
    /// <item>His shot: one target, no hit roll, a tick-exact cycle (<see cref="CycleTicks"/>). The damage falls with the range
    /// (<see cref="Hit"/>): any robot dies in one shot up to <see cref="FullBandPx"/> px. The spread is the view's.</item>
    /// <item>The power dash: on a given attack order, an enemy unit in sight past the full band (<see cref="FullBandPx"/>) and inside <see cref="DashRangePx"/>,
    /// shot range or not, cooldown over, gun reloaded: a windup, a straight burst to a spot near the target, a plant, a shot at the full number. Without it he
    /// walks into shot range and fires by the range; once the dash is ready again it goes before the next shot.</item>
    /// <item><see cref="CooldownTicks"/><see cref="VeteranCooldownTicks"/>
    /// enemy out of his range the ready dash starts by itself at the nearest enemy he can attack (Sim/ZEvilZod.AutoDash.cs).</item>
    /// <item>Heal: a share of his full health each minute, a little every tick, on foot only. A veteran heals faster and dashes sooner.</item>
    /// <item>He never drives anything. He rides in his own team's crewed shuttle as a passenger, and his rank and cooldown ride with him.</item>
    /// <item>He never steps aside for a shell, is never headshot, knocked down, run over or charged by a jeep, and never takes grenades.</item>
    /// </list>
    /// Determinism: sim ticks only (ZWorld.TickIndex), integer steps on the line, no random draw anywhere in this file; events are appended to
    /// ZEventType, old numbers stand. Switch <see cref="ZWorld.EvilZodDeviation"/>: View/ZGame.cs sets it at every start, off in 'Zod exact' and
    /// in every headless check that does not set it itself.
    /// </summary>
    public static class ZEvilZod
    {
        public const string Type = "evilzod";
        /// <summary>Body radius in px for the traffic rules (a grunt 6): 1.5 x a grunt. The footprint stays one tile.</summary>
        public const int BodyRadius = 9;
        /// <summary></summary>
        public const double WalkFactor = 1.15;
        /// <summary>The HP of a shot: near at up to FullBandPx, far at his range, in a line between. Robots, then vehicles, guns and shuttles.</summary>
        public const int RobotNearHp = 4500, RobotFarHp = 900, HullNearHp = 3000, HullFarHp = 1200, FullBandPx = 64;
        /// <summary>Shot to shot, and the moment the reload is heard after a shot.</summary>
        public const int CycleTicks = 200, ReloadAfterTicks = 28;
        /// <summary>How far an enemy can be for a dash (the howitzer's range; the counted check compares it with the row). The nearest is FullBandPx: inside that the shot is at its maximum already.</summary>
        public const int DashRangePx = 200;
        /// <summary>He stops max(DashStopMinPx, the target's radius + DashStopGapPx) from its centre. A landing spot farther out than DashMaxLandPx is no dash.</summary>
        public const int DashStopMinPx = 56, DashStopGapPx = 40, DashMaxLandPx = 80;
        /// <summary></summary>
        public const int WindupTicks = 20, BurstPxPerTick = 3, PlantTicks = 45, DashShotHoldTicks = 200, RetryTicks = 10, SampleStepPx = 4, HullRoomPx = 2;
        public const int CooldownTicks = 750, VeteranCooldownTicks = 650;
        public const int HealPctPerMin = 10, VeteranHealPctPerMin = 12, HealStepTicks = 500;
        /// <summary>Per cent x seconds of a minute x milliseconds: one hit point of the heal's carry.</summary>
        public const long HealUnit = 100L * 60 * 1000;

        /// <summary>ZSettings.Parse: his row from the rows of the text. A later change of those rows (Custom stats) does not move him.</summary>
        public static void Fill(ZSettings s)
        {
            if (s == null) return;
            var heavy = s.Unit("heavy"); var grunt = s.Unit("grunt");
            if (heavy == null || grunt == null) return;
            s.Units[Type] = new ZUnitSettings
            {
                name = Type, group_amount = 1, move_speed = heavy.move_speed, attack_radius = grunt.attack_radius, health = heavy.health,
                attack_damage = RobotNearHp / (double)ZConst.MAX_UNIT_HEALTH, attack_damage_chance = 1.0, attack_speed = CycleTicks * ZConst.TICK,
            };
        }

        /// <summary>The HP one shot removes at d px (whole px, centre to centre). A dash shot takes the near number.</summary>
        public static int Hit(ZObject t, int d, int range, bool dash)
        {
            bool robot = t.Kind == ZKind.Robot;
            int near = robot ? RobotNearHp : HullNearHp, far = robot ? RobotFarHp : HullFarHp;
            if (dash || d <= FullBandPx || range <= FullBandPx) return near;
            if (d >= range) return far;
            return near - (near - far) * (d - FullBandPx) / (range - FullBandPx);
        }
    }

    /// <summary>What rides on the shuttle beside the passenger list: the list keeps hit points only.</summary>
    public sealed class ZEvilSeat { public int kills; public bool veteran; public double at; public long dashReady, dashTotal, shotTick; }

    public partial class ZObject
    {
        /// <summary>DV161: this robot is EvilZod. Set once at creation (ZWorld.SetClassFlags); false for everything in every level.</summary>
        public bool IsEvilZod;
        /// <summary>DV161, on a shuttle: EvilZod's rank and clocks while he sits aboard.</summary>
        public ZEvilSeat EvilAboard;

        ZEvilPhase evilPhase = ZEvilPhase.None;
        long evilWindupEnd, evilDashReady, evilDashTotal = ZEvilZod.CooldownTicks, evilShotTick, evilReloadTick = -1, evilDashArmedUntil = -1, evilNextTry, evilHealCarry, evilHealNext;
        int evilDashTarget = -1, evilStep, evilSteps, evilHealGiven;

        /// <summary>The dash's phase now.</summary>
        public ZEvilPhase EvilPhase => evilPhase;
        /// <summary>In the windup or the burst: held by the dash, out of the traffic rules.</summary>
        public bool EvilDashing => evilPhase != ZEvilPhase.None;
        /// <summary>The line of the dash, centre px: the start and the landing spot.</summary>
        public int EvilDashFromX { get; private set; }
        public int EvilDashFromY { get; private set; }
        public int EvilDashToX { get; private set; }
        public int EvilDashToY { get; private set; }
        /// <summary>0..1 along the burst.</summary>
        public double EvilDashT01 => evilPhase != ZEvilPhase.Burst || evilSteps <= 0 ? 0.0 : (double)evilStep / evilSteps;
        /// <summary>The tick of the last stop (-1 = none).</summary>
        public long EvilArrivedAt { get; private set; } = -1;
        /// <summary>The target of the running or last dash (-1 = none).</summary>
        public int EvilDashTargetId => evilDashTarget;
        /// <summary>The tick he may dash again (0 = he may now).</summary>
        public long EvilDashReadyTick => evilDashReady;
        /// <summary>The tick of his next possible shot.</summary>
        public long EvilShotTick => evilShotTick;
        /// <summary>0..1 over the cooldown, 1 = ready: the small circle beside his name.</summary>
        public double EvilDashCharge01
        {
            get
            {
                if (evilDashReady == 0 || World == null) return 1.0;
                return Math.Max(0.0, Math.Min(1.0, 1.0 - (evilDashReady - World.TickIndex) / (double)Math.Max(1L, evilDashTotal)));
            }
        }
        /// <summary>0..1 over the shot cycle, 1 = the gun is ready.</summary>
        public double EvilReload01 => World == null ? 1.0 : Math.Max(0.0, Math.Min(1.0, 1.0 - (evilShotTick - World.TickIndex) / (double)ZEvilZod.CycleTicks));

        /// <summary>The dash cooldown now, in ticks.</summary>
        public int EvilCooldownTicks => VeteranActive ? ZEvilZod.VeteranCooldownTicks : ZEvilZod.CooldownTicks;

        /// <summary>He may get in here: every other robot true. He: his own team's crewed shuttle, as a passenger, and nothing else.</summary>
        public bool EvilMayEnter(ZObject t) => !IsEvilZod || (t != null && t.IsShuttle && !t.ShuttleIsEmpty && t.Owner == Owner && t.Drivers.Count > 0);

        /// <summary>ProcessServer, right after the getting-out hold: his clocks and his heal every tick; true = the dash holds him this tick.</summary>
        bool EvilStep(double time, double dt)
        {
            long now = World.TickIndex;
            EvilFaceStep();                                                                  // 1.4 DV174: his heading turns toward the dash line, his target, else his walk (Sim/ZEvilZod.Face.cs)
            if (evilDashReady != 0 && now >= evilDashReady) { evilDashReady = 0; World.Emit(ZEventType.EvilDashReady, this); }
            if (evilReloadTick >= 0 && now >= evilReloadTick)
            {
                evilReloadTick = -1; long left = Math.Max(0L, evilShotTick - now);
                World.Emit(ZEventType.EvilReload, this, null, CenterX, CenterY, (float)(left * ZConst.TICK), (int)left);
            }
            EvilHeal(dt, now);
            if (evilPhase == ZEvilPhase.Windup) return EvilWindupStep(time, now);
            if (evilPhase == ZEvilPhase.Burst) return EvilBurstStep(time, now);
            if (EvilAutoDash(time, now)) return true;                                       // 1.4: under fire, every enemy out of his range: the ready dash starts by itself (Sim/ZEvilZod.AutoDash.cs)
            return false;
        }

        void EvilHeal(double dt, long now)
        {
            if (MaxHealth <= 0 || Health >= MaxHealth || KillAt >= 0) { evilHealCarry = 0; evilHealGiven = 0; evilHealNext = 0; return; }
            long ms = (long)Math.Round(dt * 1000);
            evilHealCarry += ms * MaxHealth * (VeteranActive ? ZEvilZod.VeteranHealPctPerMin : ZEvilZod.HealPctPerMin);
            long add = evilHealCarry / ZEvilZod.HealUnit;
            if (add > 0)
            {
                evilHealCarry -= add * ZEvilZod.HealUnit;
                int h0 = Health; SetHealth((int)Math.Min(MaxHealth, Health + add)); evilHealGiven += Health - h0;
            }
            if (evilHealNext == 0) evilHealNext = now + ZEvilZod.HealStepTicks;
            if (now >= evilHealNext)
            {
                evilHealNext = now + ZEvilZod.HealStepTicks;
                if (evilHealGiven > 0) World.Emit(ZEventType.EvilHealStep, this, null, CenterX, CenterY, evilHealGiven);
                evilHealGiven = 0;
            }
        }

        /// <summary>Held by the dash this tick: no step, no shot, no engage; the stuck clocks are fed (as a robot getting out of the shuttle).</summary>
        void EvilHold(double time)
        {
            StopMove(); IsRunning = false; fortRush = false; LastAttackPlayerGiven = false;
            var s = PS;
            s.wait = ZPathWait.None; s.heldTicks = 0; s.lastProgress = time; s.stillX = double.NaN;
            if (AttackObject != null) Disengage();
        }

        /// <summary>ProcessAttackWP, before any walking step: true = a dash started. Asked at a new order and every RetryTicks while he walks.</summary>
        bool EvilDashOrder(ZWaypoint wp, ZObject t, bool isNew)
        {
            long now = World.TickIndex;
            if (!wp.playerGiven || evilPhase != ZEvilPhase.None || (!isNew && now < evilNextTry)) return false;
            evilNextTry = now + ZEvilZod.RetryTicks;
            if (!EvilDashAllowed(t, now) || !EvilDashPlan(t, out int lx, out int ly)) return false;
            EvilDashBegin(t, lx, ly, now, false);
            return true;
        }

        /// <summary>The dash starts: the windup, the cooldown from now, the line, EvilDashStart (flag: an auto dash, 1.4). The ordered dash and the auto dash
        /// (Sim/ZEvilZod.AutoDash.cs) alike; these are the ordered dash's own statements as they were.</summary>
        void EvilDashBegin(ZObject t, int lx, int ly, long now, bool auto)
        {
            evilPhase = ZEvilPhase.Windup; evilWindupEnd = now + ZEvilZod.WindupTicks; evilDashTarget = t.Id;
            evilDashTotal = EvilCooldownTicks; evilDashReady = now + evilDashTotal;
            EvilDashFromX = CenterX; EvilDashFromY = CenterY; EvilDashToX = lx; EvilDashToY = ly;
            int n = EvilBurstTicks(lx, ly);
            StopMove(); Disengage();
            World.EvilDashes++;
            World.Emit(ZEventType.EvilDashStart, this, t, lx, ly, (float)((ZEvilZod.WindupTicks + n) * ZConst.TICK), n, auto);
        }

        /// <summary>An enemy unit on the ground or on a landed ship, past the full band, inside the dash range, in a clear line (asked directly, so
        /// it holds with the shot's line rule off), the cooldown over, the gun ready by the end of the windup, not stunned.</summary>
        bool EvilDashAllowed(ZObject t, long now)
        {
            if (t == null || !t.IsUnit || t.Owner == Team.Neutral || t.ShuttleAirborne || Stunned) return false;
            if (evilDashReady != 0 || evilShotTick > now + ZEvilZod.WindupTicks) return false;
            double d = DistanceFrom(t);
            if (d <= ZEvilZod.FullBandPx || d > ZEvilZod.DashRangePx) return false;
            return World.Sight.Clear(this, t) && !World.EngageBarrierBetween(CenterX, CenterY, t.CenterX, t.CenterY);
        }

        int EvilBurstTicks(int lx, int ly) => Math.Max(1, (int)Math.Ceiling(Dist(CenterX, CenterY, lx, ly) / ZEvilZod.BurstPxPerTick));

        /// <summary>A walkable point on the line: the box ProcessMove tests, no lava, no hull in the way (robots are no obstacle: he goes past them).</summary>
        bool EvilPointFree(int x, int y, ZObject skip)
        {
            if (World.Path.WithinImpassable(x - 7, y - 7, 14, 14, out _, out _, true)) return false;
            if (World.Map.LavaAt(x >> 4, y >> 4)) return false;
            foreach (var o in World.UnitObjects)
            {
                if (o == skip || o == this || o.Kind == ZKind.Robot || o.Destroyed || o.KillAt >= 0 || o.ShuttleAirborne) continue;
                int r = ZPathing.Radius(o) + ZEvilZod.BodyRadius + ZEvilZod.HullRoomPx;
                if (Math.Abs(o.CenterX - x) < r && Math.Abs(o.CenterY - y) < r && Dist(o.CenterX, o.CenterY, x, y) < r) return false;
            }
            return true;
        }

        /// <summary>Nobody stands on this spot: a landing spot keeps the traffic rules' room to every unit.</summary>
        bool EvilSpotRoomy(int x, int y)
        {
            foreach (var o in World.UnitObjects)
            {
                if (o == this || !ZPathing.Separable(o)) continue;
                int r = ZPathing.MinSpacing(this, o) + 1;
                if (Math.Abs(o.CenterX - x) < r && Math.Abs(o.CenterY - y) < r && Dist(o.CenterX, o.CenterY, x, y) < r) return false;
            }
            return true;
        }

        /// <summary>The landing spot on the straight line to t: sampled every SampleStepPx from his centre, the last roomy point before anything stops
        /// the line. False when that spot is farther than DashMaxLandPx from the target, or when there is no line to go, not even one sample of it.</summary>
        bool EvilDashPlan(ZObject t, out int lx, out int ly)
        {
            lx = CenterX; ly = CenterY;
            double d = DistanceFrom(t);
            int stop = Math.Max(ZEvilZod.DashStopMinPx, ZPathing.Radius(t) + ZEvilZod.DashStopGapPx);
            double len = d - stop;
            if (len < ZEvilZod.SampleStepPx) return false;
            double ux = (t.CenterX - CenterX) / d, uy = (t.CenterY - CenterY) / d;
            int samples = (int)Math.Ceiling(len / ZEvilZod.SampleStepPx), reach = 0;
            while (reach < samples) { EvilSample(ux, uy, len, reach + 1, out int x, out int y); if (!EvilPointFree(x, y, t)) break; reach++; }
            for (int k = reach; k >= 1; k--)                                                   // from the far end back: the first spot the burst itself can reach
            {
                EvilSample(ux, uy, len, k, out int x, out int y);
                if (Dist(x, y, t.CenterX, t.CenterY) > ZEvilZod.DashMaxLandPx) return false;
                if (EvilSpotRoomy(x, y) && EvilBurstFree(x, y)) { lx = x; ly = y; return true; }
            }
            return false;
        }

        void EvilSample(double ux, double uy, double len, int k, out int x, out int y)
        {
            double s = Math.Min(k * (double)ZEvilZod.SampleStepPx, len);
            x = (int)Math.Round(CenterX + ux * s); y = (int)Math.Round(CenterY + uy * s);
        }

        /// <summary>The very points EvilBurstStep will walk to (lx, ly) are free: a plan can not promise a line the burst finds blocked.</summary>
        bool EvilBurstFree(int lx, int ly)
        {
            int n = EvilBurstTicks(lx, ly), cx = CenterX, cy = CenterY;
            for (int k = 1; k <= n; k++)
                if (!EvilPointFree(cx + (lx - cx) * k / n, cy + (ly - cy) * k / n, null)) return false;
            return true;
        }

        bool EvilWindupStep(double time, long now)
        {
            var t = World.Get(evilDashTarget);
            bool order = Waypoints.Count > 0 && Waypoints[0].mode == WpMode.Attack && Waypoints[0].refId == evilDashTarget;
            if (!order || !CanAttackObject(t) || Stunned) { EvilDashCancelled(t); return false; }
            EvilHold(time);
            if (now < evilWindupEnd) return true;
            if (!EvilDashPlan(t, out int lx, out int ly)) { EvilDashCancelled(t); return true; }   // the target moved behind something: he walks
            EvilDashFromX = CenterX; EvilDashFromY = CenterY; EvilDashToX = lx; EvilDashToY = ly;
            evilSteps = EvilBurstTicks(lx, ly); evilStep = 0; evilPhase = ZEvilPhase.Burst;
            World.Emit(ZEventType.EvilDashGo, this, t, lx, ly, 0, evilSteps);
            return true;
        }

        /// <summary>The cooldown is given back.</summary>
        void EvilDashCancelled(ZObject t)
        {
            evilPhase = ZEvilPhase.None; evilDashReady = 0;
            World.Emit(ZEventType.EvilDashCancel, this, t);
        }

        bool EvilBurstStep(double time, long now)
        {
            EvilHold(time);
            var t = World.Get(evilDashTarget);
            if (Stunned) { EvilDashStop(now, t, false); return true; }
            int fx = EvilDashFromX - (WidthPix >> 1), fy = EvilDashFromY - (HeightPix >> 1), ex = EvilDashToX - (WidthPix >> 1), ey = EvilDashToY - (HeightPix >> 1);
            int k = evilStep + 1;
            int nx = fx + (ex - fx) * k / evilSteps, ny = fy + (ey - fy) * k / evilSteps;
            if (!EvilPointFree(nx + (WidthPix >> 1), ny + (HeightPix >> 1), null)) { EvilDashStop(now, t, false); return true; }   // a bridge fell, a tank drove in: he stops here
            evilStep = k;
            SetCords(nx, ny); XOver = YOver = 0;
            if (k >= evilSteps) EvilDashStop(now, t, true);
            return true;
        }

        /// <summary>He stands: the gun comes up for the plant, the next shot at that target takes the full number, the waypoint starts again from here.</summary>
        void EvilDashStop(long now, ZObject t, bool reached)
        {
            evilPhase = ZEvilPhase.None; EvilArrivedAt = now;
            evilShotTick = Math.Max(evilShotTick, now + ZEvilZod.PlantTicks);
            evilDashArmedUntil = now + ZEvilZod.DashShotHoldTicks;
            var s = PS;
            s.lastProgress = World.Time; s.stillX = double.NaN; s.rebase = true; s.trafficSince = -1; s.heldTicks = 0; s.wait = ZPathWait.None;
            s.sideSince = -1; s.bridge.jamSince = World.Time; s.bridge.jamX = CenterX; s.bridge.jamY = CenterY;
            if (Waypoints.Count > 0) PathReissue();
            World.Emit(ZEventType.EvilDashEnd, this, t, CenterX, CenterY, 0, 0, reached);
        }

        /// <summary>He died in the dash: it ends here, for the view.</summary>
        internal void EvilDeathStop()
        {
            evilPhase = ZEvilPhase.None;
            World.Emit(ZEventType.EvilDashCancel, this, World.Get(evilDashTarget));
        }

        /// <summary>ProcessAttackDamage instead of the game's hitscan: no roll, no draw; never into a wall (range, barrier and line are asked here).</summary>
        void EvilFire(double time)
        {
            var t = AttackObject; long now = World.TickIndex;
            if (EvilDashing) return;                                                           // the tick the dash starts: the passive check may have taken a target in range, the shot waits for the stop
            if (now < evilShotTick) return;                                                    // the reload, or the plant after a dash
            if (!CanAttackObject(t) || !WithinAttackRadius(t)) { Disengage(); return; }
            if (!EvilFaceReady(t)) return;                                                   // 1.4 DV174: he turns onto the target first, then fires (Sim/ZEvilZod.Face.cs)
            int d = (int)DistanceFrom(t);
            bool dash = now <= evilDashArmedUntil && t.Id == evilDashTarget;
            int hp0 = t.Health;
            t.DamageHealth(ZEvilZod.Hit(t, d, AttackRadius, dash), Id);
            t.LastEnemyHit = time; UpdatedAttackObjectHealth = true;                           // the server pass relays the death, the kill credit, the help call
            evilShotTick = now + ZEvilZod.CycleTicks; evilReloadTick = now + ZEvilZod.ReloadAfterTicks; evilDashArmedUntil = -1;
            World.EvilShots++;
            World.Emit(ZEventType.EvilShot, this, t, t.CenterX, t.CenterY, hp0 - t.Health, d, dash, t.Destroyed);
            EvilConeFire(t, d, dash, time);                                                   // DV167: every other enemy robot in the cone of his pellets takes the shot at its own distance (Sim/ZEvilZod.Cone.cs)
        }

        /// <summary>The seat for the ride: rank and clocks.</summary>
        internal ZEvilSeat EvilSeat() => new ZEvilSeat { kills = Kills, veteran = Veteran, at = PromotedAt, dashReady = evilDashReady, dashTotal = evilDashTotal, shotTick = evilShotTick };

        /// <summary>He is back on the ground: rank and clocks as they were; the cooldown ticked on while he sat.</summary>
        internal void EvilSeatBack(ZEvilSeat s, long now)
        {
            Kills = s.kills; Veteran = s.veteran; PromotedAt = s.at;
            evilDashReady = s.dashReady > now ? s.dashReady : 0; evilDashTotal = s.dashTotal; evilShotTick = s.shotTick;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV161 switch: EvilZod exists (Sim/ZEvilZod.cs). View/ZGame.cs sets it at every start, off in 'Zod exact'; off by default in every
        /// headless world. Off: the flag stays false and neither panel offers him.</summary>
        public static bool EvilZodDeviation = false;
        /// <summary>DV161 statistics of this world (checks): dashes started, shots fired.</summary>
        public int EvilDashes, EvilShots;

        /// <summary>BoardAsPassengers: his rank and clocks ride beside the passenger list. Nothing for anyone else.</summary>
        internal void EvilSeatIn(ZObject r, ZObject v)
        {
            if (r != null && v != null && r.IsEvilZod) v.EvilAboard = r.EvilSeat();
        }

        /// <summary>The three places that make passengers again: PassengersOut, ShuttleHeroesOut, ShuttleDoorwayBoarders.</summary>
        internal void EvilSeatOut(ZObject v, ZObject leader)
        {
            var s = v?.EvilAboard;
            if (s == null) return;
            v.EvilAboard = null;
            if (leader != null && leader.IsEvilZod) leader.EvilSeatBack(s, TickIndex);
        }
    }
}
