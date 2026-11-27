using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    internal sealed partial class ZAi2026
    {
        readonly Dictionary<int, (int Enemy, int X, int Y, int RetreatX, int RetreatY, double Since, double Delay)> withdrawals = new();

        bool HandleTactics(ZObject unit)
        {
            bool skilled = level >= ZBotDifficulty.Skilled;
            if (skilled && HandleDriverCrossfire(unit)) return true;
            if (unit.Waypoints.Count > 0)
            {
                var waypoint = unit.Waypoints[0];
                var mode = waypoint.mode;
                if (mode != WpMode.Move && mode != WpMode.Attack && mode != WpMode.Agro) return false;
                if (mode == WpMode.Move)
                {
                    if (waypoint.refId < 0 && withdrawals.TryGetValue(unit.Id, out var withdrawal) &&
                        world.Time < withdrawal.Since + withdrawal.Delay &&
                        waypoint.x == withdrawal.RetreatX && waypoint.y == withdrawal.RetreatY)
                    {
                        var threat = world.Get(withdrawal.Enemy);
                        if (threat == null || !enemies.Contains(threat)) return true;
                        if (skilled && unit.WithinAttackRadius(threat) && SafeShot(unit, threat) &&
                            HasFireSupport(unit, threat) && Engagement(unit, threat) >= tuning.ReengageRatio)
                            return StandAndFight(unit, threat);
                        if (CanStandAndFight(unit, threat) && !CanOpenGap(unit, threat))
                            return StandAndFight(unit, threat);
                        return true;
                    }
                }
                else
                {
                    var target = world.Get(waypoint.refId);
                    if (target != null && target.IsUnit && enemies.Contains(target))
                    {
                        if (UnsupportedRobotChase(unit, target))
                        {
                            if (skilled && TryDriverCrossfire(unit, target)) return true;
                            return BackOut(unit, target);
                        }
                        if (ShouldWithdraw(unit, target) && BackOut(unit, target)) return true;
                    }
                }
            }

            if (defenseThreat != null &&
                (!assigned.TryGetValue(defenseThreat.Id, out int responding) || responding < defenseCommitment) &&
                unit.CanAttackObject(defenseThreat) && Attending(unit) &&
                ZAiGeometry.DistanceSquared(unit, defenseFort.CenterX, defenseFort.CenterY) <= 600L * 600 &&
                (defenseFort == home || teamPlay.CanRelease(unit, home, defenseFort, homeThreatened)) &&
                SafeShot(unit, defenseThreat) && (Occasion(unit.Id, defenseThreat.Id, 21) >= tuning.DefenseCheckChance ||
                    CanCommitAttack(unit, defenseThreat)) &&
                Reachable(unit, defenseThreat.CenterX, defenseThreat.CenterY, 6))
            {
                if (Issue(unit, defenseThreat, WpMode.Attack)) return true;
            }

            if (skilled && TryBridge(unit)) return true;
            if (skilled && Attending(unit) && !homeThreatened && TryAssault(unit)) return true;
            if (!unit.CanAttack() || !Attending(unit)) return false;
            if (skilled && CanSnipeFromFlank(unit))
                foreach (var enemy in enemies)
                    if ((unit.Waypoints.Count == 0 || unit.Waypoints[0].mode == WpMode.Attack &&
                        unit.Waypoints[0].refId == enemy.Id) && TryDriverCrossfire(unit, enemy)) return true;
            if (TrySupportFront(unit)) return true;
            if (TryFinishOpposition(unit)) return true;

            ZObject closest = null;
            double distance = Math.Max(180, unit.AttackRadius + 96);
            foreach (var enemy in enemies)
            {
                if (!enemy.IsUnit || !unit.CanAttackObject(enemy) || !SafeShot(unit, enemy) ||
                    UnsupportedRobotChase(unit, enemy) &&
                    (tuning.WithdrawalChance == 0 || !enemy.WithinAttackRadius(unit))) continue;
                double d = ZAiGeometry.Distance(unit, enemy);
                if (d >= distance) continue;
                closest = enemy;
                distance = d;
            }
            if (closest == null) return false;
            double exchange = Engagement(unit, closest);
            if (!CanCommitAttack(unit, closest, exchange))
            {
                if (distance > closest.RangeTo(unit) + 36 ||
                    !UnsupportedRobotChase(unit, closest) && !ShouldWithdraw(unit, closest, exchange)) return false;
                return BackOut(unit, closest);
            }

            bool standoff = unit.AttackRadius > closest.AttackRadius + 28 &&
                CanOpenGap(unit, closest);
            if (skilled && standoff && distance < closest.AttackRadius + 12 && unit.AiWeaponReloading)
                return TryRetreat(unit, closest, false);
            if (skilled && standoff && distance > closest.AttackRadius + 38 && TryRoadPosition(unit, closest)) return true;
            if (unit.Waypoints.Count > 0 && !skilled) return false;
            if (unit.Waypoints.Count > 0)
            {
                var current = unit.Waypoints[0];
                if (current.mode == WpMode.Attack)
                {
                    if (current.refId == closest.Id) return true;
                    var previous = world.Get(current.refId);
                    if (previous != null && enemies.Contains(previous) && unit.CanAttackObject(previous) &&
                        distance + 70 > ZAiGeometry.Distance(unit, previous)) return true;
                }
                else if (current.mode == WpMode.Move && distance > Math.Max(unit.AttackRadius, closest.AttackRadius) + 14)
                    return false;
            }
            if (distance > unit.AttackRadius && !Reachable(unit, closest.CenterX, closest.CenterY, 6)) return false;
            if (skilled && distance > unit.AttackRadius * .75 && exchange > 1.1 && TryCrossfire(unit, closest)) return true;
            return Issue(unit, closest, WpMode.Attack);
        }

        bool Attending(ZObject unit) => !sight.ZoomLimited ||
            ZAiGeometry.DistanceSquared(unit, focusX, focusY) <= 470L * 470 || unit.AttackObject != null;

        double Engagement(ZObject unit, ZObject target)
        {
            double weight = tuning.ApproachSupportWeight;
            double exchange = ZAiCombat.Exchange(unit, target, level >= ZBotDifficulty.Skilled);
            return weight == 0 ? exchange : exchange * (1 + weight * (LocalSupport(unit, target) - 1));
        }

        bool CanCommitAttack(ZObject unit, ZObject target) => CanCommitAttack(unit, target, Engagement(unit, target));

        bool CanCommitAttack(ZObject unit, ZObject target, double exchange)
        {
            if (UnsupportedRobotChase(unit, target)) return false;
            if (withdrawals.TryGetValue(unit.Id, out var withdrawal) &&
                world.Time < withdrawal.Since + 60 &&
                (withdrawal.Enemy == target.Id ||
                    NearWithdrawalPoint(withdrawal.X, withdrawal.Y, target.CenterX, target.CenterY)))
                return world.Time >= withdrawal.Since + withdrawal.Delay && exchange >= tuning.ReengageRatio;
            return exchange >= tuning.EngageRatio || Occasion(unit.Id, target.Id, 3) < tuning.BadFight;
        }

        bool ShouldWithdraw(ZObject unit, ZObject target) => tuning.WithdrawalChance > 0 &&
            ShouldWithdraw(unit, target, Engagement(unit, target));

        bool ShouldWithdraw(ZObject unit, ZObject target, double exchange) =>
            Occasion(unit.Id, target.Id, 20) < tuning.WithdrawalChance &&
            exchange < tuning.HoldRatio && Occasion(unit.Id, target.Id, 3) >= tuning.BadFight;

        internal static bool IsRobotAgainstTank(ZObject unit, ZObject target) => unit.IsRobot &&
            target.Kind == ZKind.Vehicle &&
            (target.Type is "light" or "medium" or "heavy" or ZMTank.Type || ZBigTanks.Is(target.Type));

        static bool UnsupportedRobotChase(ZObject unit, ZObject target) =>
            IsRobotAgainstTank(unit, target) && !unit.WithinAttackRadius(target);

        bool CanOpenGap(ZObject unit, ZObject threat)
        {
            double speed = unit.AiTravelSpeed;
            if (speed <= threat.AiTravelSpeed * 1.05) return false;
            foreach (var enemy in enemies)
            {
                if (enemy == threat) continue;
                bool pursuing = enemy.AttackObject == unit || enemy.AttackObject?.Leader == unit ||
                    enemy.Waypoints.Count > 0 && enemy.Waypoints[0].refId == unit.Id &&
                    enemy.Waypoints[0].mode is WpMode.Attack or WpMode.Agro;
                if (pursuing && enemy.CanAttackObject(unit) &&
                    ZAiGeometry.Distance(unit, enemy) <= enemy.RangeTo(unit) + 36 &&
                    speed <= enemy.AiTravelSpeed * 1.05) return false;
            }
            return true;
        }

        bool CanStandAndFight(ZObject unit, ZObject threat) => unit.CanAttackObject(threat) && SafeShot(unit, threat) &&
            (unit.WithinAttackRadius(threat) || !IsRobotAgainstTank(unit, threat) &&
                threat.WithinAttackRadius(unit) && Reachable(unit, threat.CenterX, threat.CenterY, 6));

        bool StandAndFight(ZObject unit, ZObject threat)
        {
            if (unit.Waypoints.Count > 0 && unit.Waypoints[0].mode == WpMode.Attack &&
                unit.Waypoints[0].refId == threat.Id) return true;
            if (!Issue(unit, threat, WpMode.Attack)) return false;
            withdrawals.Remove(unit.Id);
            return true;
        }

        bool CanApproachFlag(ZObject unit, ZObject flag) =>
            Occasion(unit.Id, flag.Id, 22) >= tuning.FlagCheckChance ||
            DangerousFor(flag) is not ZObject threat || CanCommitAttack(unit, threat);

        static bool NearWithdrawalPoint(int x, int y, int px, int py) =>
            (long)(x - px) * (x - px) + (long)(y - py) * (y - py) <= 180L * 180;

        bool InWithdrawalArea(ZObject unit, int x, int y) =>
            withdrawals.TryGetValue(unit.Id, out var withdrawal) && world.Time < withdrawal.Since + withdrawal.Delay &&
            NearWithdrawalPoint(withdrawal.X, withdrawal.Y, x, y) &&
            ZAiGeometry.DistanceSquared(unit, withdrawal.X, withdrawal.Y) >
                (long)(withdrawal.X - x) * (withdrawal.X - x) + (long)(withdrawal.Y - y) * (withdrawal.Y - y);

        bool BackOut(ZObject unit, ZObject threat)
        {
            bool close = ZAiGeometry.Distance(unit, threat) <= threat.RangeTo(unit) + 36;
            if (!(close && tuning.WithdrawalChance > 0 && TryRetreat(unit, threat, true)))
            {
                if (close && CanStandAndFight(unit, threat)) return StandAndFight(unit, threat);
                if (unit.Waypoints.Count == 0 ||
                    unit.Waypoints[0].mode != WpMode.Attack && unit.Waypoints[0].mode != WpMode.Agro ||
                    !orders.Stop(unit, unit.AttackObject)) return false;
            }
            planned.Add(unit.Id);
            double delay = 8;
            if (withdrawals.TryGetValue(unit.Id, out var previous) && world.Time < previous.Since + 60 &&
                (previous.Enemy == threat.Id || NearWithdrawalPoint(previous.X, previous.Y, threat.CenterX, threat.CenterY)))
                delay = Math.Min(48, previous.Delay * 2);
            var retreat = unit.Waypoints.Count > 0 ? unit.Waypoints[0] : ZWaypoint.Cleared;
            withdrawals[unit.Id] = (threat.Id, threat.CenterX, threat.CenterY, retreat.x, retreat.y, world.Time, delay);
            return true;
        }

        double LocalSupport(ZObject unit, ZObject target)
        {
            if (!target.IsUnit) return 1;
            double own = Math.Max(1, ZAiCombat.Power(unit)), enemy = Math.Max(1, ZAiCombat.Power(target));
            double friendly = own, hostile = enemy;
            double dx = target.CenterX - unit.CenterX, dy = target.CenterY - unit.CenterY;
            double distance = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
            double approach = Math.Max(0, 1 - Math.Max(0, unit.RangeTo(target) - 16) / distance);
            double endX = dx * approach, endY = dy * approach;
            double lengthSquared = endX * endX + endY * endY;
            foreach (var o in allies)
                if (o != unit && o.CanAttackObject(target) && o.WithinAttackRadius(target) &&
                    (o.AttackObject == null || o.AttackObject == target))
                    friendly += ZAiCombat.Power(o);
            foreach (var o in enemies)
            {
                if (o == target || !o.IsUnit || !o.CanAttack() || !o.CanAttackObject(unit)) continue;
                double offsetX = o.CenterX - unit.CenterX, offsetY = o.CenterY - unit.CenterY;
                double step = lengthSquared > 0 ? Math.Clamp((offsetX * endX + offsetY * endY) / lengthSquared, 0, 1) : 0;
                double x = offsetX - step * endX, y = offsetY - step * endY;
                double range = o.RangeTo(unit) + Math.Min(96, o.MoveSpeed * 3);
                if (x * x + y * y <= range * range) hostile += ZAiCombat.Power(o);
            }
            return Math.Sqrt(friendly * enemy / (own * hostile));
        }

        bool SafeShot(ZObject unit, ZObject enemy)
        {
            if (!world.SkirmishFriendlyFire || unit.DamageRadius <= 0) return true;
            int r = unit.DamageRadius + 12;
            foreach (var friend in allies)
                if (friend != unit && ZAiGeometry.DistanceSquared(friend, enemy.CenterX, enemy.CenterY) <= (long)r * r)
                    return false;
            return true;
        }

        bool AlreadyRetreating(ZObject unit, ZObject threat) =>
            unit.Waypoints.Count > 0 && unit.Waypoints[0].mode == WpMode.Move &&
            !unit.Waypoints[0].attackTo && unit.Waypoints[0].refId < 0 &&
            ZAiGeometry.DistanceSquared(threat, unit.Waypoints[0].x, unit.Waypoints[0].y) >
            ZAiGeometry.DistanceSquared(threat, unit.CenterX, unit.CenterY) + 1600;

        bool ProvidesFireSupport(ZObject ally, ZObject unit, ZObject threat) =>
            ally != unit && !ally.Stunned && ally.CanAttackObject(threat) &&
            (ally.AttackObject == null || ally.AttackObject == threat) && SafeShot(ally, threat);

        bool HasFireSupport(ZObject unit, ZObject threat)
        {
            foreach (var ally in allies)
                if (ProvidesFireSupport(ally, unit, threat) && ally.WithinAttackRadius(threat)) return true;
            return false;
        }

        bool TryRetreat(ZObject unit, ZObject threat, bool allowCover)
        {
            if (!CanOpenGap(unit, threat)) return false;
            if (AlreadyRetreating(unit, threat)) return true;

            bool checkSafety = Occasion(unit.Id, threat.Id, 24) < tuning.RetreatSafetyChance;

            double dx = unit.CenterX - threat.CenterX, dy = unit.CenterY - threat.CenterY;
            double length = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
            dx /= length;
            dy /= length;
            int bestX = 0, bestY = 0;
            double best = double.NegativeInfinity;
            double leastExposure = double.PositiveInfinity;
            for (int turn = -1; turn <= 1; turn++)
            {
                int x = unit.CenterX + (int)(dx * 92 - dy * turn * 52);
                int y = unit.CenterY + (int)(dy * 92 + dx * turn * 52);
                bool cover = false;
                if (allowCover && level >= ZBotDifficulty.Skilled)
                {
                    foreach (var ally in allies)
                        if (ProvidesFireSupport(ally, unit, threat) && ZAiGeometry.DistanceSquared(ally, x, y) <= 72L * 72 &&
                            ZAiGeometry.DistanceSquared(ally, unit.CenterX, unit.CenterY) > 100L * 100)
                        {
                            cover = true;
                            break;
                        }
                }
                if (!Reachable(unit, x, y)) continue;
                double value = ZAiGeometry.DistanceSquared(threat, x, y) / 1000d
                    - Math.Sqrt(ZAiGeometry.DistanceSquared(home, x, y)) / 28;
                if (cover) value += 25;
                if (!unit.IsRobot) value += 8 * world.Map.TileWalkSpeed(x, y);
                double exposure = checkSafety ? RetreatExposure(unit, threat, x, y) : 0;
                if (exposure > leastExposure) continue;
                if (exposure == leastExposure && value <= best) continue;
                leastExposure = exposure;
                best = value; bestX = x; bestY = y;
            }
            if (best == double.NegativeInfinity || !IssueMove(unit, bestX, bestY)) return false;
            unit.Disengage();
            foreach (var member in unit.Minions) member.Disengage();
            return true;
        }

        double RetreatExposure(ZObject unit, ZObject pursuer, int x, int y)
        {
            double dx = x - unit.CenterX, dy = y - unit.CenterY;
            double lengthSquared = dx * dx + dy * dy;
            double exposure = 0;
            foreach (var enemy in enemies)
            {
                if (enemy == pursuer || !enemy.CanAttackObject(unit) || !enemy.CanAttack()) continue;
                double ex = enemy.CenterX - unit.CenterX, ey = enemy.CenterY - unit.CenterY;
                double range = enemy.RangeTo(unit), start = ex * ex + ey * ey;
                if (range <= 0) continue;
                double t = Math.Clamp((ex * dx + ey * dy) / Math.Max(1, lengthSquared), 0, 1);
                double sx = ex - t * dx, sy = ey - t * dy;
                double closest = sx * sx + sy * sy;
                // Already exposed units can still take an exit that increases separation.
                double addedDepth = Math.Min(range, Math.Sqrt(start)) - Math.Min(range, Math.Sqrt(closest));
                if (addedDepth > 0) exposure += addedDepth * Math.Max(1, ZAiCombat.Power(enemy));
            }
            return exposure;
        }

        ZObject PickObjective(ZObject unit, out WpMode mode)
        {
            mode = WpMode.None;
            ZObject best = null;
            double bestScore = 0;
            foreach (var target in objectives)
            {
                if (target == sortieCannon || InWithdrawalArea(unit, target.CenterX, target.CenterY)) continue;
                assigned.TryGetValue(target.Id, out int commitments);
                if (commitments > 0 && (target.Type == "flag" || target.Type == "grenades" || target.CanBeEntered() || target.Type == "repair")) continue;
                double distance = ZAiGeometry.Distance(unit, target);
                if (sight.ZoomLimited && !Attending(unit) && distance > 300) continue;
                double value;
                WpMode option;
                if (target.Type == "flag" && !world.AreAllied(team, target.Owner) && world.CanCaptureFlag(team, target))
                {
                    if (!CanApproachFlag(unit, target)) continue;
                    option = WpMode.Move;
                    value = 64;
                    if (level >= ZBotDifficulty.Skilled) value += FlagImportance(target);
                    if (unit.Type == "jeep") value += 10;
                }
                else if (unit.IsRobot && !unit.NoAutoEnter && (target.CanBeEntered() || target.Owner == team && NeedsPassengers(target, unit)))
                {
                    if (target.Type == "crane" || contestedSalvage.Contains(target.Id) ||
                        Occasion(target.Id, 0, 4) < tuning.MissVehicle) continue;
                    option = WpMode.Enter;
                    value = target.Type == "apc" && target.Owner == team ? 52 : 79;
                }
                else if (target.Type == "grenades" && unit.CanPickupGrenades() && distance <= 340)
                {
                    if (Occasion(target.Id, 0, 5) < tuning.MissGrenades) continue;
                    option = WpMode.PickupGrenades;
                    value = 67;
                }
                else if (target.Type == "repair" && unit.CanBeRepaired() && !target.RepairingUnit && target.CanRepairUnit(team) &&
                    (level < ZBotDifficulty.Skilled || DangerousFor(target) == null) && SafeRepairApproach(unit, target) &&
                    unit.Health < unit.MaxHealth * (level >= ZBotDifficulty.Skilled ? .72 : .42) && level > ZBotDifficulty.Noob)
                {
                    option = WpMode.UnitRepair;
                    value = 190 * (1 - (double)unit.Health / unit.MaxHealth);
                }
                else if (unit.Type == "crane" && target.CanBeRepairedByCrane(team) && level >= ZBotDifficulty.Skilled &&
                    target.IsBridge && target.Destroyed && DangerousFor(target) == null && SafeRepairApproach(unit, target))
                {
                    option = WpMode.CraneRepair;
                    value = 55;
                    foreach (var ally in allies)
                        if (ally != unit && ally.CanMove && ZAiGeometry.DistanceSquared(ally, target.CenterX, target.CenterY) <= 180L * 180)
                        { value += 15; break; }
                }
                else if (level >= ZBotDifficulty.Skilled && target.IsProducer && !target.IsFort && unit.CanAttackObject(target) &&
                    !world.AreAllied(team, target.Owner) && target.Owner != Team.Neutral && SafeShot(unit, target) &&
                    unit.AttackRadius >= 175 && DangerousFor(target) == null)
                {
                    option = WpMode.Attack;
                    value = 42;
                }
                else continue;
                value /= 1 + distance / Math.Max(90, unit.MoveSpeed * 7);
                if (value <= bestScore || !Reachable(unit, target.CenterX, target.CenterY, option == WpMode.Move ? 2 : 6)) continue;
                best = target; mode = option; bestScore = value;
            }
            return best;
        }

        bool NeedsPassengers(ZObject target, ZObject unit) => level >= ZBotDifficulty.Skilled && target.Type == "apc" &&
            (unit.Type == "sniper" || unit.Type == "pyro" || unit.Type == "laser") && target.CanTakePassengers(unit) &&
            ZAiGeometry.DistanceSquared(target, unit.CenterX, unit.CenterY) <= 280L * 280;

        bool SafeRepairApproach(ZObject unit, ZObject site) =>
            Occasion(unit.Id, site.Id, 25) >= tuning.RetreatSafetyChance ||
            RetreatExposure(unit, null, site.CenterX, site.CenterY) <= 0;

        ZObject DangerousFor(ZObject position)
        {
            foreach (var enemy in enemies)
                if (enemy.IsUnit && enemy.CanAttack() &&
                    ZAiGeometry.DistanceSquared(enemy, position.CenterX, position.CenterY) <= 240L * 240) return enemy;
            return null;
        }

        bool TryBridge(ZObject unit)
        {
            if (!unit.CanAttack() || unit.Damage <= 0 || !Attending(unit)) return false;
            foreach (var bridge in objectives)
            {
                if (!bridge.IsBridge || bridge.Destroyed || !unit.WithinAttackRadius(bridge) ||
                    !unit.CanAttackObject(bridge) || !SafeShot(unit, bridge)) continue;
                int enemyOn = 0;
                foreach (var e in enemies) if (e.IsMobile && e.IntersectsObject(bridge)) enemyOn++;
                bool friendlyOn = false;
                foreach (var ally in allies)
                    if (ally.IsMobile && ally.IntersectsObject(bridge)) { friendlyOn = true; break; }
                if (friendlyOn) continue;
                if (enemyOn > 0 && bridge.Health <= unit.Damage)
                    return Issue(unit, bridge, WpMode.Attack);
                if (enemyOn == 0 && TryPrepareBridge(unit, bridge)) return true;
            }
            return false;
        }
    }

    public sealed partial class ZObject
    {
        internal double AiTravelSpeed
        {
            get
            {
                if (!CanMove) return 0;
                double speed = BehaviorBaseMoveSpeed;
                double x = speed, y = 0;
                CripplingCommand(ref x, ref y);
                return x;
            }
        }

        internal bool AiWeaponReloading => World.Time < nextDamageTime;
        internal double AiWeaponWait => Math.Max(0, nextDamageTime - World.Time);
        internal double AiReloadUntil => nextDamageTime;
    }
}
