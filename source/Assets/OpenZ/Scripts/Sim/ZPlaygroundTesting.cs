using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>World-local laboratory rules. Absent in ordinary games and parity worlds.</summary>
    public sealed class ZPlaygroundTesting
    {
        public sealed class Score
        {
            public long Kills, Deaths;
            public double SuccessPercent => Kills == 0 && Deaths == 0 ? 0 : 100.0 * Kills / ((double)Kills + Deaths);
            public bool HasProjectileSamples => projectedDamage > 0;
            public double ProjectileAvoidancePercent => HasProjectileSamples ? 100 * avoidedDamage / projectedDamage : 0;
            internal double projectedDamage, avoidedDamage;
            internal int dodgeX, dodgeY;
            internal int virtualHp = -1, driverHp = -1, maxHp;
        }
        internal sealed class ProjectileSample
        {
            public int Epoch, DodgeX, DodgeY;
            public Team TargetTeam;
        }
        public struct Hit { public int Victim, Attacker, Damage, X, Y; public long Deaths; public bool Theoretical, Driver; }
        readonly ZWorld world;
        readonly Dictionary<int, Score> scores = new Dictionary<int, Score>();
        readonly Queue<Hit> hits = new Queue<Hit>();
        readonly bool[] immortal = new bool[3], circular = new bool[3];
        readonly double[] hpPercent = { 100, 100, 100 };
        public bool ShowDamage, ShowCounts, ResetAutomaticallyOnParametersChange;
        bool measureProjectileAvoidance;
        int projectileEpoch;
        public bool MeasureProjectileAvoidance
        {
            get => measureProjectileAvoidance;
            set
            {
                if (measureProjectileAvoidance == value) return;
                measureProjectileAvoidance = value;
                projectileEpoch++;
            }
        }
        public bool Respawning;
        sealed class CombatSetup
        {
            public bool RedImmortal, RedCircular, BlueImmortal, BlueCircular, Damage, Counts, AutoReset, Respawn;
            public (int x, int y)? RedPoint, BluePoint;
        }
        CombatSetup previousCombatSetup;
        public bool InfiniteCombatTesting => previousCombatSetup != null;
        readonly Dictionary<Team, (int x, int y)> respawnPoints = new Dictionary<Team, (int x, int y)>();
        readonly Dictionary<int, int> respawns = new Dictionary<int, int>();
        readonly HashSet<int> held = new HashSet<int>();
        public bool HasRespawnPoint(Team team) => respawnPoints.ContainsKey(team);
        public bool TryGetRespawnPoint(Team team, out int x, out int y)
        {
            bool found = respawnPoints.TryGetValue(team, out var p); x = p.x; y = p.y; return found;
        }
        public bool CanPlace(ZObject o, int x, int y) => CanPlace(x, y, o.WidthPix, o.HeightPix, o.IsRobot);
        bool CanPlace(int x, int y, int width, int height, bool robot)
        {
            int left = x - width / 2, top = y - height / 2;
            if (left < 0 || top < 0 || left + width >= world.Map.WidthPix || top + height >= world.Map.HeightPix) return false;
            for (int ty = top / 16; ty <= (top + height - 1) / 16; ty++)
                for (int tx = left / 16; tx <= (left + width - 1) / 16; tx++)
                    if (!world.Path.TilePassable(tx, ty, robot)) return false;
            return true;
        }
        public bool SetInfiniteCombatTesting(bool enabled, int centerX, int centerY)
        {
            if (enabled == InfiniteCombatTesting) return true;
            if (!enabled)
            {
                var setup = previousCombatSetup;
                previousCombatSetup = null;
                SetTeams(setup.RedImmortal, setup.RedCircular, setup.BlueImmortal, setup.BlueCircular);
                ShowDamage = setup.Damage;
                ShowCounts = setup.Counts;
                ResetAutomaticallyOnParametersChange = setup.AutoReset;
                Respawning = setup.Respawn;
                RestoreRespawnPoint(Team.Red, setup.RedPoint);
                RestoreRespawnPoint(Team.Blue, setup.BluePoint);
                return true;
            }
            if (!FindCombatPoints(centerX, centerY, out var red, out var blue)) return false;
            previousCombatSetup = new CombatSetup
            {
                RedImmortal = Immortal(Team.Red), RedCircular = Circular(Team.Red),
                BlueImmortal = Immortal(Team.Blue), BlueCircular = Circular(Team.Blue),
                Damage = ShowDamage, Counts = ShowCounts, AutoReset = ResetAutomaticallyOnParametersChange, Respawn = Respawning,
                RedPoint = respawnPoints.TryGetValue(Team.Red, out var oldRed) ? oldRed : ((int, int)?)null,
                BluePoint = respawnPoints.TryGetValue(Team.Blue, out var oldBlue) ? oldBlue : ((int, int)?)null
            };
            SetTeams(true, true, true, true);
            ShowDamage = ShowCounts = ResetAutomaticallyOnParametersChange = Respawning = true;
            respawnPoints[Team.Red] = red;
            respawnPoints[Team.Blue] = blue;
            return true;
        }
        void SetTeams(bool redImmortal, bool redCircular, bool blueImmortal, bool blueCircular)
        {
            if (Immortal(Team.Red) != redImmortal || Circular(Team.Red) != redCircular) Configure(Team.Red, redImmortal, redCircular);
            if (Immortal(Team.Blue) != blueImmortal || Circular(Team.Blue) != blueCircular) Configure(Team.Blue, blueImmortal, blueCircular);
        }
        void RestoreRespawnPoint(Team team, (int x, int y)? point)
        {
            if (point.HasValue) respawnPoints[team] = point.Value;
            else respawnPoints.Remove(team);
        }
        bool FindCombatPoints(int centerX, int centerY, out (int x, int y) red, out (int x, int y) blue)
        {
            const int halfDistance = (int)(19 / ZConst.PX_TO_M) / 2;
            int tile = ZConst.TILE;
            red = blue = default;
            long nearest = long.MaxValue;
            for (int y = tile / 2; y < world.Map.HeightPix; y += tile)
                for (int x = tile / 2; x < world.Map.WidthPix; x += tile)
                {
                    long dx = (long)x - centerX, dy = (long)y - centerY;
                    long distance = dx * dx + dy * dy;
                    if (distance >= nearest) continue;
                    if (ClearCombatLane(x, y, halfDistance, 0))
                    { red = (x - halfDistance, y); blue = (x + halfDistance, y); nearest = distance; }
                    else if (ClearCombatLane(x, y, 0, halfDistance))
                    { red = (x, y - halfDistance); blue = (x, y + halfDistance); nearest = distance; }
                }
            return nearest != long.MaxValue;
        }
        bool ClearCombatLane(int x, int y, int dx, int dy)
        {
            // Use the ordinary two-tile vehicle footprint for both points and the ground between them.
            int size = 2 * ZConst.TILE;
            int length = dx + dy;
            for (int offset = -length; offset < length; offset += ZConst.TILE / 2)
                if (!CanPlace(x + (dx == 0 ? 0 : offset), y + (dy == 0 ? 0 : offset), size, size, false)) return false;
            return CanPlace(x + dx, y + dy, size, size, false);
        }
        public bool SetRespawnPoint(Team team, int x, int y)
        {
            CheckTeam(team);
            if (x < 16 || y < 16 || x >= world.Map.WidthPix - 16 || y >= world.Map.HeightPix - 16 ||
                !world.Path.TilePassable(x / 16, y / 16, true)) return false;
            respawnPoints[team] = (x, y); return true;
        }
        public bool Held(ZObject o) => o != null && (held.Contains(o.Id) || respawns.ContainsKey(o.Id));
        public void Hold(ZObject o, bool value)
        {
            if (o == null) return;
            if (value)
            {
                held.Add(o.Id); o.TestingStop(); world.TestingClearMissiles(new[] { o.Id });
                foreach (var enemy in world.Objects)
                    if (enemy.AttackObject == o) enemy.Disengage();
            }
            else held.Remove(o.Id);
        }
        bool QueueRespawn(ZObject victim, int attacker)
        {
            var killer = world.Get(attacker);
            if (!Respawning || !Eligible(killer) || killer.Destroyed || killer == victim || killer.Owner == victim.Owner ||
                Held(killer) || !HasRespawnPoint(victim.Owner) || !HasRespawnPoint(killer.Owner)) return false;
            var a = respawnPoints[victim.Owner]; var b = respawnPoints[killer.Owner];
            if (!CanPlace(victim, a.x, a.y) || !CanPlace(killer, b.x, b.y)) return false;
            respawns[victim.Id] = killer.Id; respawns[killer.Id] = victim.Id;
            return true;
        }
        internal void FinishRespawns()
        {
            if (respawns.Count == 0) return;
            foreach (var pair in respawns)
            {
                var o = world.Get(pair.Key); if (o == null) continue;
                var point = respawnPoints[o.Owner];
                o.TestingStop(); o.SetCords(point.x - o.WidthPix / 2, point.y - o.HeightPix / 2);
                Initialize(o);
                for (int i = 0; i < o.Drivers.Count; i++) o.Drivers[i] = Math.Max(1, world.S.Unit(o.DriverType)?.MaxHealth ?? 1);
                For(o.Id).driverHp = -1; o.TestingResetWeapon();
            }
            world.TestingClearMissiles(respawns.Keys);
            foreach (var pair in respawns)
            {
                var o = world.Get(pair.Key); var enemy = world.Get(pair.Value);
                if (o == null || enemy == null) continue;
                o.Waypoints.Add(new ZWaypoint { mode = WpMode.Attack, refId = enemy.Id, playerGiven = true });
                o.UpdatedWaypoints = true;
            }
            respawns.Clear();
        }
        public void ResetCounters()
        {
            foreach (var s in scores.Values) s.Kills = s.Deaths = 0;
            ResetProjectileAvoidance();
        }
        public void ResetProjectileAvoidance()
        {
            projectileEpoch++;
            foreach (var s in scores.Values)
            {
                s.projectedDamage = s.avoidedDamage = 0;
                s.dodgeX = s.dodgeY = 0;
            }
        }
        internal void TrackProjectile(ZMissile missile)
        {
            if (!MeasureProjectileAvoidance || missile.team == Team.Neutral || missile.debris || missile.nuke ||
                missile.damage <= 0 || missile.radius <= 0 || missile.onlyId >= 0 && missile.onlyId != missile.targetId) return;
            var target = world.Get(missile.targetId);
            if (!Eligible(target) || target.Destroyed || Held(target) || target.Owner == missile.team) return;
            var score = For(target.Id);
            missile.testingSample = new ProjectileSample
            {
                Epoch = projectileEpoch, TargetTeam = target.Owner, DodgeX = score.dodgeX, DodgeY = score.dodgeY
            };
        }
        internal void RecordDodgeStep(ZObject unit, int dx, int dy)
        {
            if (!MeasureProjectileAvoidance || (dx == 0 && dy == 0) || !Eligible(unit)) return;
            var score = For(unit.Id);
            score.dodgeX += dx;
            score.dodgeY += dy;
        }
        internal void ScoreProjectile(ZMissile missile, ZObject target)
        {
            var sample = missile.testingSample;
            if (!MeasureProjectileAvoidance || sample.Epoch != projectileEpoch ||
                !Eligible(target) || target.Destroyed || Held(target) || target.ShuttleAirborne || target.Owner != sample.TargetTeam) return;
            var score = For(target.Id);
            // Remove only the actual dodge steps taken during this projectile's flight.
            int dodgeX = score.dodgeX - sample.DodgeX, dodgeY = score.dodgeY - sample.DodgeY;
            int projected = world.TestingProjectileDamage(missile, target, dodgeX, dodgeY);
            if (projected <= 0) return;
            int actual = dodgeX == 0 && dodgeY == 0 ? projected : world.TestingProjectileDamage(missile, target, 0, 0);
            score.projectedDamage += projected;
            score.avoidedDamage += Math.Max(0, projected - actual);
        }
        internal void BehaviorChanging(ZBehaviorProfile before, ZBehaviorProfile after)
        {
            if (!ResetAutomaticallyOnParametersChange) return;
            foreach (var o in world.Objects)
            {
                if (!Eligible(o) || o.Destroyed || o.IsFortDefender || o.IsEvilZod ||
                    (after.family == ZBehaviorFamily.Robots ? !o.IsRobot || !ZBehaviorSchema.IsRobot(o.Type) : o.Kind != ZKind.Vehicle || !ZBehaviorSchema.IsTank(o.Type))) continue;
                bool fighting = o.AttackObject != null || o.BehaviorDodging ||
                    o.Waypoints.Exists(w => w.mode == WpMode.Attack || w.mode == WpMode.Agro) ||
                    o.LastEnemyHit >= 0 && world.Time - o.LastEnemyHit <= 3;
                if (!fighting) foreach (var attacker in world.Objects)
                    if (!attacker.Destroyed && attacker.Owner != o.Owner && attacker.AttackObject == o) { fighting = true; break; }
                if (!fighting) continue;
                bool changed = before.NativeFor(o.Type) != after.NativeFor(o.Type);
                foreach (var d in ZBehaviorSchema.All)
                    if (before.Get(o.Type, d.Key) != after.Get(o.Type, d.Key)) { changed = true; break; }
                if (changed) { ResetCounters(); return; }
            }
        }
        public ZPlaygroundTesting(ZWorld world) { this.world = world ?? throw new ArgumentNullException(nameof(world)); }
        public static bool Eligible(ZObject o) => o != null && o.MaxHealth > 0 && o.Owner != Team.Neutral &&
            (o.Kind == ZKind.Robot || o.Kind == ZKind.Vehicle || o.Kind == ZKind.Cannon);
        public bool Immortal(Team team) => team != Team.Neutral && immortal[(int)team];
        public bool Circular(Team team) => team != Team.Neutral && circular[(int)team];
        public double HpPercent(Team team) => hpPercent[(int)team];
        public Score For(int id) { if (!scores.TryGetValue(id, out var s)) scores[id] = s = new Score(); return s; }
        public int ReferenceHp(ZObject o) => Math.Max(1, (int)Math.Round(o.MaxHealth * hpPercent[(int)o.Owner] / 100, MidpointRounding.AwayFromZero));
        public void Configure(Team team, bool immortal, bool circular)
        {
            CheckTeam(team); this.immortal[(int)team] = immortal; this.circular[(int)team] = circular;
            foreach (var o in world.Objects) if (Eligible(o) && o.Owner == team && !o.Destroyed)
            { For(o.Id).virtualHp = ReferenceHp(o); if (immortal && !circular) o.Health = ReferenceHp(o); }
        }
        public void SetHpPercent(Team team, double percent)
        {
            CheckTeam(team);
            if (double.IsNaN(percent) || double.IsInfinity(percent)) throw new ArgumentException("HP percentage must be finite.");
            hpPercent[(int)team] = Math.Max(1, Math.Min(100, percent));
            foreach (var o in world.Objects) if (Eligible(o) && o.Owner == team && !o.Destroyed) Initialize(o);
        }
        static void CheckTeam(Team team) { if (team != Team.Red && team != Team.Blue) throw new ArgumentException("Choose RED or BLUE."); }
        public void Initialize(ZObject o)
        { if (!Eligible(o) || o.Destroyed) return; o.Health = ReferenceHp(o); var s = For(o.Id); s.virtualHp = o.Health; s.maxHp = o.MaxHealth; }
        internal void Tick()
        {
            foreach (var o in world.Objects) if (Eligible(o) && !o.Destroyed)
            {
                if (For(o.Id).maxHp != o.MaxHealth) Initialize(o);
                if (Immortal(o.Owner) && !Circular(o.Owner)) o.Health = ReferenceHp(o);
            }
        }
        public bool TryReadHit(out Hit hit)
        { if (hits.Count == 0) { hit = default; return false; } hit = hits.Dequeue(); return true; }
        void Emit(ZObject o, int amount, int attacker, long deaths, bool theoretical, bool driver = false)
        {
            if (hits.Count >= 2048) hits.Dequeue();
            hits.Enqueue(new Hit { Victim = o.Id, Attacker = attacker, Damage = amount, Deaths = deaths,
                X = o.CenterX, Y = o.CenterY, Theoretical = theoretical, Driver = driver });
        }
        void Count(ZObject victim, int attacker, long deaths)
        {
            if (deaths <= 0) return;
            var score = For(victim.Id); score.Deaths = Add(score.Deaths, deaths);
            var killer = world.Get(attacker);
            if (Eligible(killer) && killer != victim && killer.Owner != victim.Owner) For(killer.Id).Kills = Add(For(killer.Id).Kills, deaths);
        }
        static long Add(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;
        public void HealthChange(ZObject o, ref int next, int attacker)
        {
            if (Held(o)) { next = o.Health; return; }
            if (!Eligible(o) || o.Health <= 0) return;
            int requested = (int)Math.Min(int.MaxValue, Math.Max(0L, (long)o.Health - next));
            if (requested <= 0) { if (Immortal(o.Owner) && !Circular(o.Owner)) next = ReferenceHp(o); return; }
            var s = For(o.Id); long deaths = 0;
            bool immortalNow = Immortal(o.Owner), cycling = Circular(o.Owner);
            int shown = Math.Min(o.Health, requested);
            if (immortalNow)
            {
                int baseline = ReferenceHp(o);
                int remaining = cycling ? o.Health : s.virtualHp > 0 ? s.virtualHp : baseline;
                long after = (long)remaining - requested;
                if (after <= 0)
                {
                    deaths = 1;
                    after = baseline;
                }
                s.virtualHp = (int)after;
                next = cycling ? (int)after : baseline;
                shown = Math.Min(remaining, requested);
            }
            else if (next <= 0) deaths = 1;
            Count(o, attacker, deaths); Emit(o, shown, attacker, deaths, immortalNow && !cycling);
            if (deaths > 0 && QueueRespawn(o, attacker)) next = ReferenceHp(o);
        }
        public bool DriverDamage(ZObject o, int amount, int attacker)
        {
            if (Held(o)) return true;
            if (!Eligible(o) || amount <= 0 || o.Drivers.Count == 0) return false;
            bool immortalNow = Immortal(o.Owner), cycling = Circular(o.Owner);
            var score = For(o.Id);
            int hp = immortalNow && !cycling ? score.driverHp > 0 ? score.driverHp : o.Drivers[0] : o.Drivers[0];
            bool lethal = amount >= hp;
            if (lethal) Count(o, attacker, 1);
            Emit(o, immortalNow ? amount : Math.Min(hp, amount), attacker, lethal ? 1 : 0, immortalNow && !cycling, true);
            if (lethal && QueueRespawn(o, attacker)) return true;
            if (!immortalNow) return false;
            score.driverHp = lethal ? Math.Max(1, world.S.Unit(o.DriverType)?.MaxHealth ?? hp) : hp - amount;
            if (cycling && !lethal) o.Drivers[0] = hp - amount;
            else if (lethal) o.Drivers[0] = Math.Max(1, world.S.Unit(o.DriverType)?.MaxHealth ?? hp);
            return true;
        }
    }
    public partial class ZObject
    {
        internal void TestingStop()
        {
            Waypoints.Clear(); lastWp = ZWaypoint.Cleared; cur = default;
            ClearOrderModes(); Disengage(); StopMove();
            behaviorPending = behaviorWasDodging = behaviorReversing = false;
            behaviorRegroupUntil = behaviorFireUntil = behaviorReverseUntil = 0;
            BehaviorActualSpeed = behaviorTurnSpeed = 0;
            World.Pathing?.OnOrder(this); UpdatedWaypoints = true;
        }
        internal void TestingResetWeapon()
        {
            nextDamageTime = nextPassiveCheck = World.Time;
            elPhase = ElectroPhase.Charged; elRelease = elReady = -1; elTarget = -1;
            Stamina = MaxStamina; EndStun(true);
            LastEnemyHit = LastMissileHit = LastFireHit = -100;
        }
    }
    public sealed partial class ZWorld
    {
        public ZPlaygroundTesting UnitTesting { get; private set; }
        internal void TestingClearMissiles(IEnumerable<int> ids)
        {
            var set = new HashSet<int>(ids);
            missiles.RemoveAll(m => set.Contains(m.attackerId) || set.Contains(m.targetId));
            newMissiles.RemoveAll(m => set.Contains(m.attackerId) || set.Contains(m.targetId));
        }
        public void EnableUnitTesting()
        {
            if (!NoGameEnd) throw new InvalidOperationException("Unit testing is restricted to a playground world.");
            if (UnitTesting == null) UnitTesting = new ZPlaygroundTesting(this);
        }
    }
}
