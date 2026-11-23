using System;

namespace OpenZ.Sim
{
    internal sealed partial class ZAi2026
    {
        bool HandleTactics(ZObject unit)
        {
            bool skilled = level >= ZBotDifficulty.Skilled;
            if (unit.Waypoints.Count > 0)
            {
                var mode = unit.Waypoints[0].mode;
                if (mode != WpMode.Move && mode != WpMode.Attack) return false;
            }

            if (defenseThreat != null && unit.CanAttackObject(defenseThreat) && Attending(unit) &&
                ZAiGeometry.DistanceSquared(unit, defenseFort.CenterX, defenseFort.CenterY) <= 600L * 600 &&
                (defenseFort == home || teamPlay.CanRelease(unit, home, defenseFort, homeThreatened)) &&
                SafeShot(unit, defenseThreat) && Reachable(unit, defenseThreat.CenterX, defenseThreat.CenterY, 6))
            {
                if (Issue(unit, defenseThreat, WpMode.Attack)) return true;
            }

            if (skilled && TryBridge(unit)) return true;
            if (skilled && Attending(unit) && !homeThreatened && TryAssault(unit)) return true;
            if (!unit.CanAttack() || !Attending(unit)) return false;
            if (TrySupportFront(unit)) return true;

            ZObject closest = null;
            double distance = Math.Max(180, unit.AttackRadius + 96);
            foreach (var enemy in enemies)
            {
                if (!enemy.IsUnit || !unit.CanAttackObject(enemy) || !SafeShot(unit, enemy)) continue;
                double d = ZAiGeometry.Distance(unit, enemy);
                if (d >= distance) continue;
                closest = enemy;
                distance = d;
            }
            if (closest == null) return false;

            double support = skilled ? LocalSupport(unit, closest) : 1;
            double exchange = ZAiCombat.Exchange(unit, closest, skilled) * support;
            bool takeBadFight = Occasion(unit.Id, closest.Id, 3) < tuning.BadFight;
            if (exchange < (skilled ? .96 : .78) && !takeBadFight)
            {
                if (level == ZBotDifficulty.Noob || distance > closest.AttackRadius + 36) return false;
                return TryRetreat(unit, closest, unit.Waypoints.Count > 0 && unit.Waypoints[0].mode == WpMode.Move);
            }

            bool standoff = unit.AttackRadius > closest.AttackRadius + 28 &&
                unit.MoveSpeed >= closest.MoveSpeed * .85 && unit.CanMove;
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

        double LocalSupport(ZObject unit, ZObject target)
        {
            double friendly = ZAiCombat.Power(unit), hostile = ZAiCombat.Power(target);
            double own = friendly, enemy = hostile;
            int near = Math.Max(180, target.AttackRadius + 72);
            long radius = (long)near * near;
            foreach (var o in allies)
                if (o != unit && ZAiGeometry.DistanceSquared(o, target.CenterX, target.CenterY) <= radius)
                    friendly += ZAiCombat.Power(o);
            foreach (var o in enemies)
                if (o != target && o.IsUnit && ZAiGeometry.DistanceSquared(o, unit.CenterX, unit.CenterY) <= radius)
                    hostile += ZAiCombat.Power(o);
            return Math.Sqrt(Math.Max(.15, friendly * Math.Max(1, enemy) / Math.Max(1, own * hostile)));
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

        bool TryRetreat(ZObject unit, ZObject threat, bool alreadyMoving)
        {
            if (!unit.CanMove) return false;
            if (alreadyMoving && unit.Waypoints.Count > 0 &&
                ZAiGeometry.DistanceSquared(threat, unit.Waypoints[0].x, unit.Waypoints[0].y) >
                ZAiGeometry.DistanceSquared(threat, unit.CenterX, unit.CenterY) + 1600) return true;

            double dx = unit.CenterX - threat.CenterX, dy = unit.CenterY - threat.CenterY;
            double length = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
            dx /= length;
            dy /= length;
            int bestX = 0, bestY = 0;
            double best = double.NegativeInfinity;
            for (int turn = -1; turn <= 1; turn++)
            {
                int x = unit.CenterX + (int)(dx * 92 - dy * turn * 52);
                int y = unit.CenterY + (int)(dy * 92 + dx * turn * 52);
                if (!Reachable(unit, x, y)) continue;
                double value = ZAiGeometry.DistanceSquared(threat, x, y) / 1000d
                    - Math.Sqrt(ZAiGeometry.DistanceSquared(home, x, y)) / 28;
                if (!unit.IsRobot) value += 8 * world.Map.TileWalkSpeed(x, y);
                if (value <= best) continue;
                best = value; bestX = x; bestY = y;
            }
            return best > double.NegativeInfinity && IssueMove(unit, bestX, bestY);
        }

        ZObject PickObjective(ZObject unit, out WpMode mode)
        {
            mode = WpMode.None;
            ZObject best = null;
            double bestScore = 0;
            foreach (var target in objectives)
            {
                assigned.TryGetValue(target.Id, out int commitments);
                if (commitments > 0 && (target.Type == "flag" || target.Type == "grenades" || target.CanBeEntered() || target.Type == "repair")) continue;
                double distance = ZAiGeometry.Distance(unit, target);
                if (sight.ZoomLimited && !Attending(unit) && distance > 300) continue;
                double value;
                WpMode option;
                if (target.Type == "flag" && !world.AreAllied(team, target.Owner) && world.CanCaptureFlag(team, target))
                {
                    option = WpMode.Move;
                    value = 64;
                    if (level >= ZBotDifficulty.Skilled) value += FlagImportance(target);
                    if (unit.Type == "jeep") value += 10;
                }
                else if (unit.IsRobot && !unit.NoAutoEnter && (target.CanBeEntered() || target.Owner == team && NeedsPassengers(target, unit)))
                {
                    if (Occasion(target.Id, 0, 4) < tuning.MissVehicle) continue;
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
                    (level < ZBotDifficulty.Skilled || DangerousFor(target) == null) &&
                    unit.Health < unit.MaxHealth * (level >= ZBotDifficulty.Skilled ? .72 : .42) && level > ZBotDifficulty.Noob)
                {
                    option = WpMode.UnitRepair;
                    value = 190 * (1 - (double)unit.Health / unit.MaxHealth);
                }
                else if (unit.Type == "crane" && target.CanBeRepairedByCrane(team) && level >= ZBotDifficulty.Skilled &&
                    target.IsBridge && target.Destroyed && DangerousFor(target) == null)
                {
                    option = WpMode.CraneRepair;
                    value = 55;
                }
                else if (level >= ZBotDifficulty.Skilled && target.IsProducer && unit.CanAttackObject(target) &&
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
        internal bool AiWeaponReloading => World.Time < nextDamageTime;
        internal double AiReloadUntil => nextDamageTime;
    }
}
