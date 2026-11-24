using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    internal sealed partial class ZAi2026
    {
        const int SalvageRadius = 420;
        const double SalvageHorizon = 15;
        readonly HashSet<int> contestedSalvage = new HashSet<int>();
        readonly Dictionary<int, (ZObject Target, double Until)> salvageGuards = new Dictionary<int, (ZObject, double)>();
        readonly List<int> finishedGuards = new List<int>();

        bool PreciseSalvage => level == ZBotDifficulty.Zod;
        double SalvageMargin => PreciseSalvage ? .25 : level == ZBotDifficulty.Skilled ? .65 : 1.2;

        static bool SalvageTarget(ZObject target) => target != null && Alive(target) && target.CanBeEntered() &&
            !target.IsTowerGun && target.Type != "crane" && target.U != null && target.U.Damage > 0;

        bool PlansSalvage(ZObject target) =>
            SalvageTarget(target) && Occasion(target.Id, 0, 19) < tuning.SalvageChance;

        internal bool AllowsPassiveBoarding(ZObject target) => target.Type != "crane" && !PlansSalvage(target);

        static bool SalvageOrder(ZObject unit, ZObject target, WpMode mode) =>
            unit.Waypoints.Count > 0 && unit.Waypoints[0].mode == mode && unit.Waypoints[0].refId == target.Id;

        bool HoldingSalvageOrder(ZObject unit) =>
            salvageGuards.ContainsKey(unit.Id) ||
            unit.Waypoints.Count > 0 && unit.Waypoints[0].mode == WpMode.Attack &&
            SalvageTarget(world.Get(unit.Waypoints[0].refId));

        void PlanSalvage()
        {
            contestedSalvage.Clear();
            ReleaseSalvageGuards();
            ZObject bestUnit = null, bestTarget = null;
            WpMode bestMode = WpMode.None;
            double bestScore = 0, guardArrival = 0;
            int guardX = 0, guardY = 0;

            foreach (var target in objectives)
            {
                if (target == sortieCannon || !PlansSalvage(target)) continue;

                double quality = Math.Clamp(Value(target.U) / 1500, .35, 5) *
                    Math.Sqrt((double)target.Health / Math.Max(1, target.MaxHealth));
                var hostileCrew = FastestHostileBoarder(target, out double hostileEta);
                ZObject capturer = null;
                double friendlyEta = double.PositiveInfinity, ownEta = double.PositiveInfinity, claimEta = double.PositiveInfinity;
                foreach (var ally in allies)
                {
                    double eta = BoardEta(ally, target, hostile: false);
                    if (eta > SalvageHorizon) continue;
                    friendlyEta = Math.Min(friendlyEta, eta);
                    if (SalvageOrder(ally, target, WpMode.Enter)) claimEta = Math.Min(claimEta, eta);
                    if (ally.Owner != team || eta >= ownEta || !SalvageAvailable(ally)) continue;
                    capturer = ally;
                    ownEta = eta;
                }
                double raceMargin = SalvageMargin / (1 + quality);
                bool friendlyWins = friendlyEta + raceMargin < hostileEta;
                bool friendlyClaim = claimEta < double.PositiveInfinity && claimEta <= ownEta + SalvageMargin;
                bool guarded = false, denying = false;
                foreach (var ally in allies)
                {
                    if (SalvageOrder(ally, target, WpMode.Move) ||
                        salvageGuards.TryGetValue(ally.Id, out var guard) && guard.Target == target)
                        guarded = true;
                    if (ally.Kind != ZKind.Vehicle || !SalvageOrder(ally, target, WpMode.Attack)) continue;
                    if (ally.Owner == team && (hostileCrew == null || friendlyWins ||
                        !CanDenySalvage(ally, target, hostileEta, quality, out _)))
                        StopSalvage(ally, target);
                    denying |= SalvageOrder(ally, target, WpMode.Attack);
                }

                if (hostileCrew != null || friendlyClaim || denying) contestedSalvage.Add(target.Id);
                if (denying) continue;
                if (capturer != null && ownEta + raceMargin < hostileEta && !friendlyClaim)
                {
                    Consider(capturer, target, WpMode.Enter, (2 + quality * 2) / (1 + ownEta / 5));
                    continue;
                }
                if (friendlyWins || hostileCrew == null || guarded) continue;

                foreach (var unit in units)
                {
                    if (unit.Kind != ZKind.Vehicle || !unit.CanAttack() ||
                        ZAiGeometry.DistanceSquared(unit, target.CenterX, target.CenterY) > SalvageRadius * SalvageRadius ||
                        !SalvageAvailable(unit)) continue;
                    if (double.IsPositiveInfinity(friendlyEta) && TrySalvageGuard(unit, target, hostileCrew, hostileEta,
                        out int x, out int y, out double arrive))
                    {
                        Consider(unit, target, WpMode.Move, (2.5 + quality * 2) / (1 + arrive / 5), x, y, arrive);
                    }
                    else if (CanDenySalvage(unit, target, hostileEta, quality, out double destroyTime))
                    {
                        Consider(unit, target, WpMode.Attack, (2 + quality * 2) / (1 + destroyTime / 5));
                    }
                }
            }

            if (bestUnit == null || !orders.Available) return;
            if (bestMode == WpMode.Move)
            {
                var move = ZWaypoint.Move(guardX, guardY);
                move.refId = bestTarget.Id;
                if (orders.Send(bestUnit, move, planned))
                    salvageGuards[bestUnit.Id] = (bestTarget, world.Time + guardArrival + 8);
            }
            else Issue(bestUnit, bestTarget, bestMode);

            void Consider(ZObject unit, ZObject target, WpMode mode, double score, int x = 0, int y = 0, double arrive = 0)
            {
                if (score <= bestScore) return;
                bestUnit = unit; bestTarget = target; bestMode = mode; bestScore = score;
                guardX = x; guardY = y; guardArrival = arrive;
            }
        }

        void ReleaseSalvageGuards()
        {
            finishedGuards.Clear();
            foreach (var entry in salvageGuards)
            {
                var unit = world.Get(entry.Key);
                var guard = entry.Value;
                bool stillGuarding = unit != null && Alive(unit) && unit.Owner == team;
                if (stillGuarding && SalvageTarget(guard.Target) && world.Time < guard.Until && !homeThreatened &&
                    IncomingSalvageDamage(unit, unit.CenterX, unit.CenterY) * 2 < unit.Health * .65) continue;
                if (stillGuarding && SalvageOrder(unit, guard.Target, WpMode.Move))
                {
                    StopSalvage(unit, guard.Target);
                    if (SalvageOrder(unit, guard.Target, WpMode.Move)) continue;
                }
                finishedGuards.Add(entry.Key);
            }
            foreach (int id in finishedGuards) salvageGuards.Remove(id);
        }

        void StopSalvage(ZObject unit, ZObject target)
        {
            if (unit.CanOverwriteWP() && orders.Stop(unit, target)) planned.Add(unit.Id);
        }

        bool SalvageAvailable(ZObject unit) => unit != sortieCrew && unit != diversionUnit &&
            !planned.Contains(unit.Id) && !salvageGuards.ContainsKey(unit.Id) && !unit.Stunned &&
            !unit.BehaviorDodging && unit.CanOverwriteWP() && Attending(unit) && !OpeningIdle(unit) &&
            (!homeThreatened || ZAiGeometry.DistanceSquared(unit, home.CenterX, home.CenterY) > 300L * 300) &&
            (unit.Waypoints.Count == 0 || unit.Waypoints[0].mode == WpMode.Move ||
                unit.Waypoints[0].mode == WpMode.Attack && !HoldingSalvageOrder(unit));

        ZObject FastestHostileBoarder(ZObject target, out double quickest)
        {
            ZObject best = null;
            quickest = double.PositiveInfinity;
            foreach (var unit in enemies)
            {
                double eta = BoardEta(unit, target, hostile: true);
                if (eta >= quickest || eta > SalvageHorizon) continue;
                best = unit; quickest = eta;
            }
            return best;
        }

        double BoardEta(ZObject unit, ZObject target, bool hostile)
        {
            if (!unit.IsRobot || unit.NoAutoEnter && !hostile && !SalvageOrder(unit, target, WpMode.Enter) ||
                unit.Stunned || !Commandable(unit) || !unit.EvilMayEnter(target) ||
                ZAiGeometry.DistanceSquared(unit, target.CenterX, target.CenterY) > SalvageRadius * SalvageRadius)
                return double.PositiveInfinity;
            double eta = SalvageTravel(unit, target.CenterX, target.CenterY, boarding: true);
            if (hostile && PreciseSalvage && contacts.TryGetValue(unit.Id, out var contact) && eta > .5)
            {
                double distance = ZAiGeometry.Distance(unit, target);
                double closing = (contact.Vx * (target.CenterX - unit.CenterX) + contact.Vy * (target.CenterY - unit.CenterY)) /
                    Math.Max(1, distance);
                eta *= closing > unit.MoveSpeed * .45 ? .9 : closing < -unit.MoveSpeed * .45 ? 1.2 : 1;
            }
            return eta;
        }

        double SalvageTravel(ZObject unit, int x, int y, bool boarding = false)
        {
            if (x < 0 || y < 0 || x >= world.Map.WidthPix || y >= world.Map.HeightPix) return double.PositiveInfinity;
            using var grid = world.Path.Kind(world.GridKindOf(unit));
            bool direct = world.Path.DirectPathPossible(unit.LocX + 8, unit.LocY + 8, x, y, unit.IsRobot, false);
            if (!direct && !Reachable(unit, x, y, boarding ? 4 : 0)) return double.PositiveInfinity;
            double distance = Math.Sqrt(ZAiGeometry.DistanceSquared(unit, x, y));
            if (boarding) distance = Math.Max(0, distance - 8);
            double startTerrain = world.Map.TileWalkSpeed(unit.CenterX, unit.CenterY);
            double terrain = (startTerrain + world.Map.TileWalkSpeed((unit.CenterX + x) / 2, (unit.CenterY + y) / 2) * 2 +
                world.Map.TileWalkSpeed(x, y)) * .25;
            double speed = Math.Max(1, (unit.RealMoveSpeed > 0 && !unit.IsRunning
                ? unit.RealMoveSpeed / Math.Max(.25, startTerrain) : unit.MoveSpeed) * terrain);
            if (boarding && distance <= unit.MoveSpeed * unit.Stamina) distance *= .8;
            return distance * (direct ? 1 : 1.65) / speed + ZConst.TICK;
        }

        bool TrySalvageGuard(ZObject unit, ZObject target, ZObject boarder, double deadline,
            out int x, out int y, out double arrival)
        {
            double distance = Math.Max(1, ZAiGeometry.Distance(unit, target));
            x = target.CenterX + (int)((unit.CenterX - target.CenterX) * 52 / distance);
            y = target.CenterY + (int)((unit.CenterY - target.CenterY) * 52 / distance);
            arrival = SalvageTravel(unit, x, y);
            int reach = unit.RangeTo(boarder);
            // Leave the hull clear for incoming friendly crew while covering the enemy approach.
            double stop = Math.Max(arrival, deadline - Math.Max(0, reach - 52) / Math.Max(1, boarder.MoveSpeed));
            double kill = SalvageFireTime(unit, boarder, reach, stop);
            return unit.AttackObject == null && reach > 64 && unit.CanAttackObject(boarder) &&
                arrival + 2 + SalvageMargin < deadline && stop + kill + SalvageMargin < deadline &&
                unit.AiSalvageLine(x, y, target) && SafeShot(unit, boarder) &&
                SalvageSurvives(unit, x, y, arrival, Math.Max(3, kill));
        }

        bool CanDenySalvage(ZObject unit, ZObject target, double deadline, double quality, out double finish)
        {
            finish = double.PositiveInfinity;
            if (!unit.CanAttackObject(target) || !SafeShot(unit, target)) return false;
            double distance = Math.Max(1, ZAiGeometry.Distance(unit, target));
            int range = Math.Max(1, unit.RangeTo(target) - 16);
            int x = unit.CenterX, y = unit.CenterY;
            double arrival = 0;
            if (!unit.WithinAttackRadius(target))
            {
                if (distance > range)
                {
                    x = target.CenterX + (int)((unit.CenterX - target.CenterX) * range / distance);
                    y = target.CenterY + (int)((unit.CenterY - target.CenterY) * range / distance);
                }
                if (!unit.AiSalvageLine(x, y, target)) return false;
                arrival = SalvageTravel(unit, x, y);
            }

            double kill = SalvageFireTime(unit, target, arrival == 0 ? distance : range, arrival);
            finish = arrival + kill;
            if (finish + SalvageMargin >= deadline || !SalvageSurvives(unit, x, y, arrival, kill)) return false;
            var opponent = unit.AttackObject;
            if (opponent == null && unit.Waypoints.Count > 0 && unit.Waypoints[0].mode == WpMode.Attack)
                opponent = world.Get(unit.Waypoints[0].refId);
            if (opponent == null || !enemies.Contains(opponent) || !unit.WithinAttackRadius(opponent)) return true;
            double currentFight = SalvageFireTime(unit, opponent, ZAiGeometry.Distance(unit, opponent), 0);
            // Finish a quick engagement if there is still time to deny the empty unit afterwards.
            if (finish + currentFight + SalvageMargin < deadline) return false;
            return quality >= .8 || currentFight > finish * 1.5;
        }

        double SalvageFireTime(ZObject unit, ZObject target, double distance, double arrival)
        {
            double hit = unit.DamageIsMissile ? 1 : Math.Clamp(unit.DamageChance, .05, 1);
            if (PreciseSalvage && target.Owner != Team.Neutral) hit *= 1 - ZAiCombat.Avoidance(target, unit);
            double shots = Math.Ceiling(Math.Ceiling((double)target.Health / Math.Max(1, unit.Damage)) / hit);
            return Math.Max(0, unit.AiWeaponWait - arrival) + Math.Max(0, shots - 1) * unit.AttackSpeed +
                (unit.DamageIsMissile ? distance / Math.Max(1, unit.MissileSpeed) : ZConst.TICK) + .15;
        }

        bool SalvageSurvives(ZObject unit, int x, int y, double arrival, double firing) =>
            IncomingSalvageDamage(unit, (unit.CenterX + x) / 2, (unit.CenterY + y) / 2) * arrival +
            IncomingSalvageDamage(unit, x, y) * firing < unit.Health * .65;

        double IncomingSalvageDamage(ZObject unit, int x, int y)
        {
            double damage = 0;
            foreach (var enemy in enemies)
            {
                if (!enemy.IsUnit || !enemy.CanAttackObject(unit)) continue;
                int reach = enemy.RangeTo(unit) + 24;
                if (ZAiGeometry.DistanceSquared(enemy, x, y) <= (long)reach * reach)
                    damage += ZAiCombat.Firepower(enemy) * (PreciseSalvage ? 1 - ZAiCombat.Avoidance(unit, enemy) : 1);
            }
            return damage;
        }
    }

    public sealed partial class ZObject
    {
        internal bool AiSalvageLine(int x, int y, ZObject target) => !LosActive ||
            World.Sight.Clear(x, y, World.Sight.ExemptOf(this), target.CenterX, target.CenterY, World.Sight.ExemptOf(target));
    }

    public sealed partial class ZWorld
    {
        internal bool AllowsPassiveBotBoarding(Team owner, ZObject target)
        {
            foreach (var bot in Bots)
                if (bot.Team == owner) return bot.AllowsPassiveBoarding(target);
            return true;
        }
    }
}
