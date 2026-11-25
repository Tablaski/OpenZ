using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>Authoritative AI controller. It plans through observed contacts and submits normal game orders.</summary>
    internal sealed partial class ZAi2026
    {
        readonly ZWorld world;
        readonly Team team;
        readonly ZBotDifficulty level;
        readonly ZAiTuning tuning;
        readonly ZAiSight sight;
        readonly ZAiOrders orders;
        readonly ZAiTeamPlay teamPlay;
        readonly int seed;
        readonly List<ZObject> units = new List<ZObject>(), producers = new List<ZObject>(),
            forts = new List<ZObject>(), objectives = new List<ZObject>(), allies = new List<ZObject>(),
            enemies = new List<ZObject>(), infrastructure = new List<ZObject>();
        readonly Dictionary<int, Contact> contacts = new Dictionary<int, Contact>();
        readonly List<int> forgotten = new List<int>();
        readonly HashSet<int> planned = new HashSet<int>();
        readonly Dictionary<int, int> assigned = new Dictionary<int, int>();
        readonly List<ZObject> group = new List<ZObject>(4);
        ZObject home, defenseFort, defenseThreat;
        ZAiTeamPlay.Threat supportFront;
        bool homeThreatened;
        int defenseCommitment, observedOpposition;
        double nextTactics, nextStrategy, nextFocus, nextProduction;
        int cursor, focusX, focusY;

        internal ZAi2026(ZWorld world, Team team, int seed, ZBotDifficulty level)
        {
            this.world = world;
            this.team = team;
            this.seed = seed;
            this.level = level;
            tuning = ZAiTuning.For(level);
            sight = new ZAiSight(world, team);
            orders = new ZAiOrders(world, team, tuning);
            teamPlay = new ZAiTeamPlay(world, team);
            double openingSpread = level == ZBotDifficulty.Noob ? 4.9 : level == ZBotDifficulty.Alright ? 1.9 : .35;
            double opening = tuning.Opening + Occasion(0, 0, 0) * openingSpread;
            int interval = (int)Math.Round(tuning.Tactics / ZConst.TICK);
            int phase = (int)team * 3 % interval;
            int firstTick = (int)Math.Ceiling(opening / ZConst.TICK);
            nextTactics = (firstTick + (phase - firstTick % interval + interval) % interval) * ZConst.TICK;
            if (level <= ZBotDifficulty.Alright && nextTactics > tuning.Opening + openingSpread)
                nextTactics -= tuning.Tactics;
        }

        internal int Actions => orders.Actions;
        internal double FirstActionAt => orders.FirstActionAt;
        internal double StrategicRatio => strategicRatio;

        internal void Tick()
        {
            MaintainBridgeTrap();
            if (world.Time + 1e-6 < nextTactics) return;
            nextTactics = world.Time + tuning.Tactics;
            Observe();
            if (home == null) return;
            if (world.Time >= nextStrategy)
            {
                nextStrategy = world.Time + tuning.Strategy;
                EvaluateStrategy();
            }
            ChooseDefense();
            TryTakeOpenFort();
            if (level >= ZBotDifficulty.Skilled)
            {
                ManageCrewSortie();
                TryDiversion();
            }
            if (tuning.SalvageChance > 0) PlanSalvage();
            Act();
            if (world.Time >= nextProduction && orders.Available)
            {
                nextProduction = world.Time + Math.Max(.6, tuning.Tactics * 2);
                PlanProduction();
            }
        }

        void Observe()
        {
            sight.Observe();
            units.Clear(); producers.Clear(); forts.Clear(); objectives.Clear(); allies.Clear(); enemies.Clear(); infrastructure.Clear();
            assigned.Clear(); planned.Clear();
            home = null;
            observedOpposition = 0;
            foreach (var o in world.Objects)
            {
                if (!Alive(o))
                {
                    if (o.IsBridge && sight.CanSee(o.CenterX, o.CenterY, false)) objectives.Add(o);
                    continue;
                }
                bool own = o.Owner == team;
                if (own || o.Owner != Team.Neutral && world.AreAllied(team, o.Owner))
                {
                    if (o.IsFort)
                    {
                        forts.Add(o);
                        if (own) home = o;
                    }
                    if (o.IsUnit) allies.Add(o);
                    if (o.Type == "radar" || o.Type == "repair") infrastructure.Add(o);
                    if (own && o.IsProducer) producers.Add(o);
                    if (own && Commandable(o))
                    {
                        units.Add(o);
                        if (o.Waypoints.Count > 0 && o.Waypoints[0].refId >= 0)
                        {
                            int id = o.Waypoints[0].refId;
                            assigned.TryGetValue(id, out int count);
                            assigned[id] = count + 1;
                        }
                    }
                    if (o.Type == "repair" || o.Type == "apc" || o.IsBridge) objectives.Add(o);
                    continue;
                }
                if (!sight.CanSee(o.CenterX, o.CenterY, o.IsMobile)) continue;
                if (o.Type == "flag" || o.Type == "grenades" || o.IsBridge || o.CanBeEntered() || o.IsProducer)
                    objectives.Add(o);
                if (o.Owner == Team.Neutral || (!o.IsUnit && o.Kind != ZKind.Building)) continue;
                if (o.IsUnit || o.IsFort) enemies.Add(o);
                if (ZWorld.CountsForElimination(o)) observedOpposition++;
                contacts.TryGetValue(o.Id, out var previous);
                contacts[o.Id] = new Contact(o, world.Time, o.IsUnit ? ZAiCombat.Power(o) : 0, previous);
            }
            forgotten.Clear();
            foreach (var pair in contacts)
            {
                var known = pair.Value;
                if (known.SeenAt == world.Time) continue;
                if (world.Time - known.SeenAt > tuning.Memory || sight.CanSee(known.X, known.Y, known.Mobile))
                    forgotten.Add(pair.Key);
            }
            foreach (int id in forgotten) contacts.Remove(id);
        }

        void ChooseDefense()
        {
            var own = ZAiTeamPlay.FortThreat(home, enemies);
            var help = teamPlay.Relief(forts, enemies, tuning, Occasion);
            homeThreatened = own.Enemy != null;
            supportFront = teamPlay.SupportFront(allies, enemies, level, tuning, Occasion);
            defenseFort = home;
            defenseThreat = own.Enemy;
            double defenseUrgency = own.Urgency;
            if (help.Enemy != null && (own.Enemy == null || world.SkirmishAllForOne && help.Urgency > own.Urgency * 1.2))
            {
                defenseFort = help.Fort;
                defenseThreat = help.Enemy;
                defenseUrgency = help.Urgency;
            }
            defenseCommitment = Math.Min(level >= ZBotDifficulty.Skilled ? 4 : 2, 1 + (int)defenseUrgency);
            if (defenseThreat == null && level >= ZBotDifficulty.Alright)
                ChooseInfrastructureDefense();

            if (!sight.ZoomLimited || world.Time >= nextFocus)
            {
                nextFocus = world.Time + tuning.Attention;
                var focus = defenseThreat ?? supportFront.Enemy ?? assaultFort ?? home;
                focusX = focus.CenterX;
                focusY = focus.CenterY;
            }
        }

        void Act()
        {
            int count = units.Count;
            if (count == 0 || !orders.Available) return;
            cursor %= count;
            int inspected = Math.Min(count, tuning.UnitsPerTurn);
            for (int i = 0; i < inspected && orders.Available; i++)
            {
                var unit = units[cursor];
                cursor = (cursor + 1) % count;
                if (planned.Contains(unit.Id) || unit == sortieCrew || unit == diversionUnit && AssaultReady ||
                    unit.Stunned || unit.BehaviorDodging || !unit.CanOverwriteWP()) continue;
                if (tuning.SalvageChance > 0 && HoldingSalvageOrder(unit)) continue;
                if (HandleTactics(unit) || unit.Waypoints.Count != 0 || OpeningIdle(unit)) continue;
                var objective = PickObjective(unit, out var mode);
                if (objective != null)
                    Issue(unit, objective, mode, mode == WpMode.EnterFort ||
                        mode == WpMode.Move && level >= ZBotDifficulty.Alright && DangerousFor(objective) != null);
                else if (!TryExploreFlag(unit) && level >= ZBotDifficulty.Skilled) TryScout(unit);
            }
        }

        bool OpeningIdle(ZObject unit)
        {
            if (tuning.IdleChance == 0 || level >= ZBotDifficulty.Skilled || unit.AttackObject != null) return false;
            // Unit IDs are stable for the lifetime of a match. A wait is rolled once for a newly seen unit.
            if (!firstReady.TryGetValue(unit.Id, out double readyAt))
            {
                double mapFactor = Math.Min(2, Math.Max(1, Math.Max(world.Map.WidthPix, world.Map.HeightPix) / 1800d));
                double longest = level == ZBotDifficulty.Noob ? 40 : 15;
                double shortest = level == ZBotDifficulty.Noob ? 20 : 5;
                readyAt = world.Time;
                if (world.Time >= 20 && Occasion(unit.Id, 0, 1) < tuning.IdleChance)
                    readyAt += mapFactor * (shortest + Occasion(unit.Id, 0, 2) * longest);
                firstReady[unit.Id] = readyAt;
            }
            return world.Time < readyAt;
        }

        readonly Dictionary<int, double> firstReady = new Dictionary<int, double>();

        bool Reachable(ZObject unit, int x, int y, int radius = 0)
        {
            if (x < 0 || y < 0 || x >= world.Map.WidthPix || y >= world.Map.HeightPix) return false;
            using (world.Path.Kind(world.GridKindOf(unit)))
            {
                var reach = world.Pathing?.Reach;
                bool robot = unit.IsRobot, explosives = unit.HasExplosives();
                int component = reach?.UnitComp(unit, explosives) ?? -1;
                if (component < 0)
                    return world.Path.ShouldBeAbleToMoveTo(unit.LocX + 8, unit.LocY + 8, x, y, robot);
                return reach.PointOk(x, y, robot, explosives, component) ||
                    radius > 0 && reach.Nearest(x, y, robot, explosives, component, out _, out _, radius);
            }
        }

        bool Issue(ZObject unit, ZObject target, WpMode mode, bool mayGroup = false)
        {
            int x = target.CenterX, y = target.CenterY;
            if (mode == WpMode.EnterFort && !target.GetCreationMovePoint(out x, out y)) return false;
            var wp = new ZWaypoint { mode = mode, refId = target.Id, x = x, y = y,
                attackTo = mode == WpMode.Attack };
            group.Clear();
            group.Add(unit);
            if (mayGroup && level > ZBotDifficulty.Noob && (target.Type == "flag" || mode == WpMode.EnterFort))
            {
                int limit = level == ZBotDifficulty.Alright ? 2 : level == ZBotDifficulty.Skilled ? 3 : 4;
                foreach (var other in units)
                {
                    if (group.Count >= limit) break;
                    if (other == unit || other == sortieCrew || other == diversionUnit && AssaultReady ||
                        other.IsRobot != unit.IsRobot || planned.Contains(other.Id) ||
                        mode == WpMode.EnterFort && !EligibleAssault(other) || OpeningIdle(other) ||
                        other.Stunned || other.BehaviorDodging || other.Waypoints.Count != 0 || other.AttackObject != null ||
                        ZAiGeometry.DistanceSquared(other, unit.CenterX, unit.CenterY) > 100L * 100 ||
                        InWithdrawalArea(other, x, y) ||
                        !Reachable(other, x, y, 4)) continue;
                    if (target.Type == "flag" && !CanApproachFlag(other, target)) continue;
                    group.Add(other);
                }
            }
            if (!orders.Send(group, wp, planned)) return false;
            assigned.TryGetValue(target.Id, out int count);
            assigned[target.Id] = count + group.Count;
            return true;
        }

        bool IssueMove(ZObject unit, int x, int y) =>
            orders.Send(unit, new ZWaypoint { mode = WpMode.Move, refId = -1, x = x, y = y }, planned);

        double Occasion(int a, int b, int use)
        {
            unchecked
            {
                uint value = (uint)seed ^ (uint)a * 0x9E3779B9u ^ (uint)b * 0x85EBCA6Bu ^ (uint)use * 0xC2B2AE35u;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value / 4294967296d;
            }
        }

        internal static bool Alive(ZObject o) => !o.Destroyed && o.KillAt < 0;
        static bool Commandable(ZObject o) => o.CanMove && !o.IsMinion && !o.IsFortDefender && !o.EvilGuarding &&
            !o.IsShuttle && !o.UnmannedHeld && !o.DoAutoRepair;

        readonly struct Contact
        {
            internal readonly string Type;
            internal readonly int X, Y, Speed;
            internal readonly double SeenAt, Power, Vx, Vy;
            internal readonly bool Mobile, Producer;
            internal Contact(ZObject o, double now, double power, Contact previous)
            {
                Type = o.Type; X = o.CenterX; Y = o.CenterY;
                Speed = o.MoveSpeed; Mobile = o.IsMobile; Producer = o.IsProducer;
                SeenAt = now; Power = power;
                double dt = now - previous.SeenAt;
                Vx = Mobile && previous.Type != null && dt > 0 && dt <= 2 ? (X - previous.X) / dt : 0;
                Vy = Mobile && previous.Type != null && dt > 0 && dt <= 2 ? (Y - previous.Y) / dt : 0;
            }
        }
    }
}
