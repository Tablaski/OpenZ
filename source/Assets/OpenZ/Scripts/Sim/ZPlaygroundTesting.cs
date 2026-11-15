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
            internal int virtualHp = -1, driverHp = -1, maxHp;
        }
        public struct Hit { public int Victim, Attacker, Damage, X, Y; public long Deaths; public bool Theoretical, Driver; }
        readonly ZWorld world;
        readonly Dictionary<int, Score> scores = new Dictionary<int, Score>();
        readonly Queue<Hit> hits = new Queue<Hit>();
        readonly bool[] immortal = new bool[3], circular = new bool[3];
        readonly double[] hpPercent = { 100, 100, 100 };
        public bool ShowDamage, ShowCounts, ResetAutomaticallyOnParametersChange;
        public void ResetCounters()
        { foreach (var s in scores.Values) { s.Kills = 0; s.Deaths = 0; } }
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
        }
        public bool DriverDamage(ZObject o, int amount, int attacker)
        {
            if (!Eligible(o) || amount <= 0 || o.Drivers.Count == 0) return false;
            bool immortalNow = Immortal(o.Owner), cycling = Circular(o.Owner);
            var score = For(o.Id);
            int hp = immortalNow && !cycling ? score.driverHp > 0 ? score.driverHp : o.Drivers[0] : o.Drivers[0];
            bool lethal = amount >= hp;
            if (lethal) Count(o, attacker, 1);
            Emit(o, immortalNow ? amount : Math.Min(hp, amount), attacker, lethal ? 1 : 0, immortalNow && !cycling, true);
            if (!immortalNow) return false;
            score.driverHp = lethal ? Math.Max(1, world.S.Unit(o.DriverType)?.MaxHealth ?? hp) : hp - amount;
            if (cycling && !lethal) o.Drivers[0] = hp - amount;
            else if (lethal) o.Drivers[0] = Math.Max(1, world.S.Unit(o.DriverType)?.MaxHealth ?? hp);
            return true;
        }
    }
    public sealed partial class ZWorld
    {
        public ZPlaygroundTesting UnitTesting { get; private set; }
        public void EnableUnitTesting()
        {
            if (!NoGameEnd) throw new InvalidOperationException("Unit testing is restricted to a playground world.");
            if (UnitTesting == null) UnitTesting = new ZPlaygroundTesting(this);
        }
    }
}
