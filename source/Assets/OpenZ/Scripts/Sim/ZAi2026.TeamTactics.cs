using System;
using System.Collections.Generic;
using OpenZ.Multiplayer;

namespace OpenZ.Sim
{
    internal sealed partial class ZAi2026
    {
        readonly Dictionary<int, (int Target, double Time)> flanks = new Dictionary<int, (int, double)>();

        ZObject bridgeShooter, preparedBridge;
        double bridgeShotBefore, bridgePlanExpires;
        ZObject sortieCrew, sortieCannon, sortieFlag, diversionUnit;
        double nextSortie = 120, sortieDeadline;

        void ChooseInfrastructureDefense()
        {
            var radarRule = world.Map.skirmish || world.HasDuelRules ? world.DuelRadarLoss : world.ModernBotSoloRadar;
            int activeRadars = 0;
            foreach (var o in infrastructure) if (o.ProvidesRadarTo(team)) activeRadars++;
            double priority = 0;
            foreach (var site in infrastructure)
            {
                bool radarWorthDefending = site.Type == "radar" && activeRadars == 1 &&
                    (radarRule != RadarLossRule.Off || world.ModernBotFog && world.ModernBotLineOfSight);
                bool repairWorthDefending = site.Type == "repair" && level >= ZBotDifficulty.Skilled &&
                    site.CanRepairUnit(team) && site.RepairingUnit;
                if (!radarWorthDefending && !repairWorthDefending) continue;

                var threat = ZAiTeamPlay.FortThreat(site, enemies);
                if (threat.Enemy == null || threat.Urgency <= priority ||
                    ZAiGeometry.DistanceSquared(threat.Enemy, site.CenterX, site.CenterY) > 160L * 160) continue;
                if (site.Owner != team && Occasion(site.Id, threat.Enemy.Id, 12) >= tuning.Help) continue;
                priority = threat.Urgency;
                defenseFort = site;
                defenseThreat = threat.Enemy;
            }
        }

        bool TrySupportFront(ZObject unit)
        {
            if (supportFront.Enemy == null || unit.Waypoints.Count != 0 ||
                !teamPlay.CanRelease(unit, home, supportFront.Fort, homeThreatened) ||
                !unit.CanAttackObject(supportFront.Enemy) || !SafeShot(unit, supportFront.Enemy)) return false;
            int limit = level == ZBotDifficulty.Alright ? 240 : 430;
            var enemy = supportFront.Enemy;
            if (ZAiGeometry.DistanceSquared(unit, enemy.CenterX, enemy.CenterY) > (long)limit * limit ||
                !Reachable(unit, enemy.CenterX, enemy.CenterY, 5)) return false;
            if (ZAiCombat.Exchange(unit, enemy, level >= ZBotDifficulty.Skilled) * LocalSupport(unit, enemy) < .9) return false;
            return TryCrossfire(unit, enemy) || Issue(unit, enemy, WpMode.Attack);
        }

        bool TryCrossfire(ZObject unit, ZObject enemy)
        {
            if (level < ZBotDifficulty.Skilled || unit.Waypoints.Count != 0 || unit.MoveSpeed < 12 ||
                flanks.TryGetValue(unit.Id, out var last) && last.Target == enemy.Id && world.Time - last.Time < 25) return false;
            bool engaged = false;
            foreach (var ally in allies)
                if (ally != unit && ally.AttackObject == enemy) { engaged = true; break; }
            if (!engaged) return false;

            double dx = enemy.CenterX - unit.CenterX, dy = enemy.CenterY - unit.CenterY;
            double distance = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
            double step = Math.Min(96, distance / 2), side = (unit.Id % 2 == 0 ? 1 : -1) * Math.Min(110, unit.AttackRadius * .7);
            int x = unit.CenterX + (int)((dx * step - dy * side) / distance);
            int y = unit.CenterY + (int)((dy * step + dx * side) / distance);
            if (ZAiGeometry.DistanceSquared(enemy, x, y) < (long)enemy.AttackRadius * enemy.AttackRadius * .65 ||
                !Reachable(unit, x, y) || !IssueMove(unit, x, y)) return false;
            flanks[unit.Id] = (enemy.Id, world.Time);
            return true;
        }

        bool TryRoadPosition(ZObject unit, ZObject enemy)
        {
            if (!ZBehaviorSchema.IsTank(unit.Type) || unit.Waypoints.Count != 0 ||
                world.Map.TileWalkSpeed(unit.CenterX, unit.CenterY) > 1) return false;
            double range = Math.Max(1, Math.Sqrt(ZAiGeometry.DistanceSquared(enemy, unit.CenterX, unit.CenterY)));
            if (range > unit.AttackRadius + 45) return false;
            int x = unit.CenterX, y = unit.CenterY;
            for (int i = 0; i < 8; i++)
            {
                int px = x + ScoutX[i] * 24, py = y + ScoutY[i] * 24;
                long distance = ZAiGeometry.DistanceSquared(enemy, px, py);
                if (distance <= (long)(enemy.AttackRadius + 20) * (enemy.AttackRadius + 20) ||
                    distance > (long)(unit.AttackRadius + 20) * (unit.AttackRadius + 20) ||
                    world.Map.TileWalkSpeed(px, py) <= 1 || !Reachable(unit, px, py)) continue;
                return IssueMove(unit, px, py);
            }
            return false;
        }

        bool TryScout(ZObject unit)
        {
            if (!sight.HasLimitedInformation || !Attending(unit) || unit.MoveSpeed < 15 || unit.Health < unit.MaxHealth * .6)
                return false;
            int range = Math.Min(420, Math.Max(240, unit.MoveSpeed * 12));
            int x = unit.CenterX, y = unit.CenterY;
            double best = double.NegativeInfinity;
            int targetX = 0, targetY = 0;
            for (int i = 0; i < 8; i++)
            {
                int direction = (i + unit.Id) % 8;
                int px = x + ScoutX[direction] * range, py = y + ScoutY[direction] * range;
                if (sight.CanSee(px, py) || !Reachable(unit, px, py)) continue;
                double value = -Math.Sqrt(ZAiGeometry.DistanceSquared(home, px, py)) / 45 + 2 * world.Map.TileWalkSpeed(px, py);
                foreach (var contact in contacts.Values)
                {
                    double age = world.Time - contact.SeenAt;
                    if (!contact.Mobile || age < 1 || age > tuning.Memory) continue;
                    double look = Math.Min(level == ZBotDifficulty.Zod ? 5 : 2.5, age);
                    double dx = px - (contact.X + contact.Vx * look), dy = py - (contact.Y + contact.Vy * look);
                    double uncertainty = 128 + age * Math.Max(1, contact.Speed);
                    if (dx * dx + dy * dy < uncertainty * uncertainty) value += 3 * (1 - age / tuning.Memory);
                }
                if (value <= best) continue;
                best = value; targetX = px; targetY = py;
            }
            return best > double.NegativeInfinity && IssueMove(unit, targetX, targetY);
        }

        static readonly int[] ScoutX = { 1, 1, 0, -1, -1, -1, 0, 1 }, ScoutY = { 0, 1, 1, 1, 0, -1, -1, -1 };

        bool TryPrepareBridge(ZObject unit, ZObject bridge)
        {
            if (bridgeShooter != null || !unit.DamageIsMissile || unit.AiWeaponReloading ||
                bridge.Health <= unit.Damage || bridge.Health > 2 * unit.Damage ||
                ZAiGeometry.DistanceSquared(home, bridge.CenterX, bridge.CenterY) > 650L * 650 ||
                !orders.CanSpendTwice || unit.Waypoints.Count != 0) return false;
            int approaching = 0;
            foreach (var enemy in enemies)
                if (enemy.IsMobile && ZAiGeometry.DistanceSquared(enemy, bridge.CenterX, bridge.CenterY) <= 250L * 250)
                    approaching++;
            if (approaching < 2) return false;
            double reloadBefore = unit.AiReloadUntil;
            if (!Issue(unit, bridge, WpMode.Attack)) return false;
            bridgeShooter = unit;
            preparedBridge = bridge;
            bridgeShotBefore = reloadBefore;
            bridgePlanExpires = world.Time + 2.5;
            return true;
        }

        void MaintainBridgeTrap()
        {
            if (bridgeShooter == null) return;
            if (bridgeShooter.Owner != team || !Alive(bridgeShooter))
            {
                bridgeShooter = preparedBridge = null;
                return;
            }
            bool cancel = world.Time >= bridgePlanExpires || !Alive(preparedBridge) ||
                bridgeShooter.AiReloadUntil > bridgeShotBefore;
            if (!cancel) return;
            if (bridgeShooter.Waypoints.Count > 0 && bridgeShooter.Waypoints[0].refId == preparedBridge.Id &&
                !orders.Stop(bridgeShooter, preparedBridge)) return;
            bridgeShooter = preparedBridge = null;
        }

        void ManageCrewSortie()
        {
            if (sortieCrew != null)
            {
                if (sortieCrew.Owner != team || !Alive(sortieCrew) || !sortieCannon.CanBeEntered())
                {
                    sortieCrew = sortieCannon = sortieFlag = null;
                    return;
                }
                if (world.Time >= sortieDeadline || world.AreAllied(team, sortieFlag.Owner) ||
                    !world.CanCaptureFlag(team, sortieFlag) ||
                    DangerousFor(sortieCannon) != null || DangerousFor(sortieFlag) != null)
                {
                    Issue(sortieCrew, sortieCannon, WpMode.Enter);
                    return;
                }
                if (sortieCrew.Waypoints.Count == 0) Issue(sortieCrew, sortieFlag, WpMode.Move);
                return;
            }
            if (world.Time < nextSortie) return;
            nextSortie = world.Time + 20;
            if (homeThreatened || sight.HasLimitedInformation || !orders.CanSpendTwice) return;

            foreach (var gun in allies)
            {
                if (gun.Owner != team || gun.Kind != ZKind.Cannon || !gun.CanEjectDrivers() ||
                    gun.AttackObject != null || gun.Drivers.Count == 0 || DangerousFor(gun) != null ||
                    ZAiGeometry.DistanceSquared(gun, home.CenterX, home.CenterY) < 260L * 260) continue;
                foreach (var flag in objectives)
                {
                    if (flag.Type != "flag" || world.AreAllied(team, flag.Owner) ||
                        !world.CanCaptureFlag(team, flag) || assigned.ContainsKey(flag.Id) ||
                        ZAiGeometry.DistanceSquared(gun, flag.CenterX, flag.CenterY) > 104L * 104 ||
                        DangerousFor(flag) != null) continue;
                    var crew = orders.Eject(gun);
                    if (crew == null) return;
                    sortieCannon = gun;
                    sortieCrew = crew;
                    sortieFlag = flag;
                    sortieDeadline = world.Time + 13;
                    if (Reachable(crew, flag.CenterX, flag.CenterY, 1)) Issue(crew, flag, WpMode.Move);
                    else sortieDeadline = world.Time;
                    return;
                }
            }
        }

        void TryDiversion()
        {
            if (world.Time < 120 || !AssaultReady || diversionUnit != null || homeThreatened ||
                world.Time < assaultArrival - 10 || world.Time > assaultArrival - 4 || !orders.Available ||
                !assaultFort.GetCreationMovePoint(out int gateX, out int gateY)) return;

            var defender = DangerousFor(assaultFort);
            if (defender == null || defender.AttackRadius <= 0) return;
            int partners = 0;
            foreach (var unit in units)
                if (EligibleAssault(unit) && ZAiGeometry.DistanceSquared(unit, gateX, gateY) < 500L * 500) partners++;
            if (partners < 3) return;
            foreach (var unit in units)
            {
                bool crane = unit.Type == "crane";
                if (!crane && unit.Type != "jeep" && unit.Type != "grunt") continue;
                if (unit == sortieCrew || !Attending(unit) || unit.Health < unit.MaxHealth * .66 ||
                    HasMissionOtherThanCombat(unit) || !crane && strategicRatio < .8 ||
                    ZAiGeometry.DistanceSquared(unit, defender.CenterX, defender.CenterY) > 320L * 320) continue;
                double dx = gateX - assaultFort.CenterX, dy = gateY - assaultFort.CenterY;
                double length = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
                for (int side = -1; side <= 1; side += 2)
                {
                    int x = gateX + (int)(-dy * side * 120 / length);
                    int y = gateY + (int)(dx * side * 120 / length);
                    if (ZAiGeometry.DistanceSquared(defender, x, y) > (long)defender.AttackRadius * defender.AttackRadius ||
                        !Reachable(unit, x, y) || !IssueMove(unit, x, y)) continue;
                    diversionUnit = unit;
                    return;
                }
            }
        }
    }
}
