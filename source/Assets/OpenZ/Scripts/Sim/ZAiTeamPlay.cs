using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenZ.Sim
{
    internal sealed class ZAiTeamPlay
    {
        const int TeamFortRadius = 360, TeamSupportRadius = 640, TeamBattleRadius = 240;

        readonly ZWorld W;
        readonly Team Team;

        internal ZAiTeamPlay(ZWorld world, Team team) { W = world; Team = team; }

        internal readonly struct Threat
        {
            internal readonly ZObject Fort, Enemy;
            internal readonly double Urgency;
            internal Threat(ZObject fort, ZObject enemy, double urgency)
            { Fort = fort; Enemy = enemy; Urgency = urgency; }
        }

        internal static Threat FortThreat(ZObject fort, List<ZObject> visibleEnemies)
        {
            if (fort == null) return default;
            ZObject first = null;
            double urgency = 0, priority = 0;
            foreach (var enemy in visibleEnemies)
            {
                if (!enemy.IsUnit || !enemy.CanAttack() && !enemy.CanMove) continue;
                double distance = ZAiGeometry.Distance(fort, enemy);
                if (distance > TeamFortRadius && enemy.AttackObject != fort) continue;
                double entry = Math.Max(0, distance - 40) / Math.Max(1, enemy.MoveSpeed);
                double pressure = enemy.CanAttack() ? ZAiCombat.Firepower(enemy) * 28 / Math.Max(1, fort.Health) : 0;
                double value = 1 / (1 + entry / 12) + pressure;
                if (enemy.AttackObject == fort) value += 1;
                if (enemy.IsMoving && enemy.CanMove) value *= 1.2;
                urgency += value;
                if (value <= priority) continue;
                priority = value;
                first = enemy;
            }
            return new Threat(fort, first, urgency);
        }

        internal Threat Relief(List<ZObject> forts, List<ZObject> visibleEnemies, ZAiTuning tuning,
            Func<int, int, int, double> occasion)
        {
            if (forts.Count < 2 || !W.HasActiveAlly(Team)) return default;
            Threat best = default;
            foreach (var fort in forts)
            {
                if (fort.Owner == Team) continue;
                var threat = FortThreat(fort, visibleEnemies);
                if (threat.Enemy == null || threat.Urgency <= best.Urgency) continue;
                if (occasion(fort.Id, threat.Enemy.Id, 6) >= (W.SkirmishAllForOne ? tuning.AllForOne : tuning.Help)) continue;
                best = threat;
            }
            return best;
        }

        internal Threat SupportFront(List<ZObject> allies, List<ZObject> visibleEnemies, ZBotDifficulty level, ZAiTuning tuning,
            Func<int, int, int, double> occasion)
        {
            if (!W.HasActiveAlly(Team) || level < ZBotDifficulty.Alright) return default;
            Threat best = default;
            foreach (var ally in allies)
            {
                if (ally.Owner == Team || ally.IsMinion || !ally.IsMobile) continue;
                var enemy = ally.AttackObject;
                if (enemy == null || !visibleEnemies.Contains(enemy) || enemy.IsFort) continue;
                if (occasion(ally.Id, enemy.Id, 11) >= tuning.Help) continue;
                double exchange = ZAiCombat.Exchange(ally, enemy, level >= ZBotDifficulty.Skilled);
                double urgency = ZAiCombat.Power(enemy) / Math.Max(1, ZAiCombat.Power(ally)) / Math.Max(.5, exchange);
                if (urgency <= best.Urgency) continue;
                best = new Threat(ally, enemy, urgency);
            }
            return best;
        }

        internal bool CanRelease(ZObject unit, ZObject home, ZObject position, bool homeThreatened)
        {
            double homeDistance = ZAiGeometry.Distance(unit, home);
            if (homeDistance > 160 || ZAiGeometry.Distance(unit, position) < homeDistance) return true;
            return W.SkirmishAllForOne && !homeThreatened && unit.Type != "heavy";
        }

        internal void CoordinateLegacy(ZBot bot, List<ZObject> idle)
        {
            if (!W.HasActiveAlly(Team) || idle.Count == 0) return;
            var forts = W.Objects.Where(o => o.IsFort && TeamplayAlive(o) && o.Owner != Team.Neutral && W.AreAllied(o.Owner, Team)).ToArray();
            var home = forts.FirstOrDefault(o => o.Owner == Team);
            if (home == null) return;

            var combat = W.UnitObjects.Where(o => TeamplayAlive(o) && o.Owner != Team.Neutral && o.CanAttack()).ToArray();
            var enemies = combat.Where(o => !W.AreAllied(Team, o.Owner) && bot.VisibleToBot(o)).ToArray();
            var homeThreats = enemies.Where(o => ZAiGeometry.Distance(home, o) <= TeamFortRadius || o.AttackObject == home).ToArray();
            var ready = idle.Where(o => o.CanMove && o.CanAttack() && !o.Stunned && !o.IsMoving &&
                o.AttackObject == null && (long)o.Health * 3 >= o.MaxHealth).ToList();
            if (ready.Count == 0) return;

            int budget = Math.Max(1, ready.Count / 3);
            if (bot.Difficulty == ZBotDifficulty.Easy) budget = 1;
            if (homeThreats.Length > 0)
            {
                // A threat to our own fort takes precedence over any commitment elsewhere.
                ready.RemoveAll(o => ZAiGeometry.Distance(home, o) > TeamSupportRadius);
                foreach (var unit in ready.OrderBy(o => ZAiGeometry.Distance(home, o)).Take((bot.Difficulty == ZBotDifficulty.Easy ? 1 : Math.Max(2, budget))))
                    SendTeamDefense(bot, unit, homeThreats, idle);
                return;
            }

            // Keep the closest available defenders at the fort when fielding a relief force.
            var reserve = ready.Where(o => ZAiGeometry.Distance(home, o) <= TeamFortRadius).OrderBy(o => ZAiGeometry.Distance(home, o)).Take(2).ToArray();
            ready.RemoveAll(o => reserve.Contains(o));
            if (ready.Count == 0) return;

            var fortThreats = enemies.Where(o => forts.Any(f => f != home &&
                (ZAiGeometry.Distance(f, o) <= TeamFortRadius || o.AttackObject == f))).ToArray();
            var territoryThreats = enemies.Where(o =>
            {
                int zone = W.GetZoneIndex(o.CenterX, o.CenterY);
                return zone >= 0 && W.Zones[zone].Owner != Team.Neutral && W.AreAllied(Team, W.Zones[zone].Owner);
            }).ToArray();
            var advance = combat.Where(o => o.Owner != Team && W.AreAllied(Team, o.Owner) && o.IsMobile && !o.IsMinion)
                .Select(o => (Unit: o, Target: TeamAttackTarget(bot, o)))
                .Where(p => p.Target != null && ZAiGeometry.Distance(p.Unit, p.Target) <= TeamSupportRadius).ToArray();

            bool sent = false, diversion = false;
            foreach (var unit in ready.OrderBy(o => TeamplayPriority(o, fortThreats, territoryThreats, advance)).ThenBy(o => o.Id))
            {
                if (budget == 0) break;
                int before = bot.Orders;
                if (!SendTeamDefense(bot, unit, fortThreats, idle) && !SendTeamDefense(bot, unit, territoryThreats, idle))
                {
                    foreach (var front in advance.OrderBy(p => ZAiGeometry.Distance(unit, p.Unit)))
                    {
                        if (ZAiGeometry.Distance(unit, front.Unit) > TeamSupportRadius) break;
                        double friendly = BattlePower(front.Target, combat, true);
                        double hostile = BattlePower(front.Target, enemies, false);
                        // Keep our units out of assaults they cannot materially reinforce.
                        if (friendly + TeamplayPower(unit) < hostile * .75) continue;
                        if (!diversion && bot.Difficulty != ZBotDifficulty.Easy && friendly > hostile && fortThreats.Length == 0)
                        {
                            var flank = W.Flags.Where(f => W.AreAllied(f.Owner, front.Target.Owner) &&
                                ZAiGeometry.Distance(f, front.Target) >= 2 * TeamBattleRadius && ZAiGeometry.Distance(unit, f) <= TeamFortRadius &&
                                enemies.All(e => ZAiGeometry.Distance(e, f) > TeamBattleRadius) && CanReachTeamPoint(bot, unit, f, f.CenterX, f.CenterY))
                                .OrderBy(f => ZAiGeometry.Distance(unit, f)).FirstOrDefault();
                            if (flank != null)
                            {
                                bot.Command(unit, flank, coordinated: true);
                                if (bot.Orders > before) { diversion = true; break; }
                            }
                        }
                        if (unit.CanAttackObject(front.Target) && CanReachTeamPoint(bot, unit, front.Target, front.Target.CenterX, front.Target.CenterY))
                            bot.Command(unit, front.Target, coordinated: true);
                        else if (ZAiGeometry.Distance(unit, front.Unit) > 96)
                        {
                            // Reinforce the ally's rear without aiming for the tile occupied by its squad.
                            double distance = ZAiGeometry.Distance(front.Unit, front.Target);
                            double offset = 80 / Math.Max(80, distance);
                            int x = front.Unit.CenterX + (int)((front.Unit.CenterX - front.Target.CenterX) * offset);
                            int y = front.Unit.CenterY + (int)((front.Unit.CenterY - front.Target.CenterY) * offset);
                            if (CanReachTeamPoint(bot, unit, front.Unit, x, y))
                                bot.IssueOrder(unit, front.Unit, WpMode.Move, x, y, false, true);
                        }
                        if (bot.Orders > before) break;
                    }
                }
                if (bot.Orders == before) continue;
                idle.Remove(unit);
                sent = true;
                budget--;
            }
            if (sent) idle.RemoveAll(o => reserve.Contains(o));
        }

        double TeamplayPriority(ZObject unit, ZObject[] fortThreats, ZObject[] territoryThreats,
            (ZObject Unit, ZObject Target)[] advance)
        {
            double distance = double.PositiveInfinity;
            foreach (var threat in fortThreats) distance = Math.Min(distance, ZAiGeometry.Distance(unit, threat));
            foreach (var threat in territoryThreats) distance = Math.Min(distance, ZAiGeometry.Distance(unit, threat));
            foreach (var front in advance) distance = Math.Min(distance, ZAiGeometry.Distance(unit, front.Unit));
            return distance;
        }

        bool SendTeamDefense(ZBot bot, ZObject unit, ZObject[] threats, List<ZObject> idle)
        {
            var target = threats.Where(o => unit.CanAttackObject(o) && ZAiGeometry.Distance(unit, o) <= TeamSupportRadius &&
                CanReachTeamPoint(bot, unit, o, o.CenterX, o.CenterY)).OrderBy(o => ZAiGeometry.Distance(unit, o)).FirstOrDefault();
            if (target == null) return false;
            int before = bot.Orders;
            bot.Command(unit, target, coordinated: true);
            if (bot.Orders == before) return false;
            idle.Remove(unit);
            return true;
        }

        ZObject TeamAttackTarget(ZBot bot, ZObject ally)
        {
            var target = ally.AttackObject;
            if (target == null && ally.Waypoints.Count > 0)
            {
                var order = ally.Waypoints[0];
                var ordered = W.Get(order.refId);
                if (order.mode == WpMode.Attack || order.mode == WpMode.Agro || order.mode == WpMode.EnterFort ||
                    order.mode == WpMode.Move && ordered?.Type == "flag")
                    target = ordered;
            }
            return target != null && TeamplayAlive(target) && target.Owner != Team.Neutral &&
                !W.AreAllied(Team, target.Owner) && bot.VisibleToBot(target) ? target : null;
        }

        bool CanReachTeamPoint(ZBot bot, ZObject unit, ZObject target, int x, int y) =>
            bot.BotPairRegion(unit, target, unit.LocX + 8, unit.LocY + 8, x, y);

        double BattlePower(ZObject target, ZObject[] units, bool friendly)
        {
            double power = 0;
            foreach (var unit in units)
                if (ZAiGeometry.Distance(target, unit) <= TeamBattleRadius && (!friendly || W.AreAllied(Team, unit.Owner)))
                    power += TeamplayPower(unit);
            return power;
        }

        double TeamplayPower(ZObject unit)
        {
            double power = Math.Max(1, W.S.Unit(unit.Type)?.build_time ?? 1);
            if (unit.IsRobot) power /= Math.Max(1, W.S.Unit(unit.Type)?.group_amount ?? 1);
            return power * unit.Health / Math.Max(1, unit.MaxHealth);
        }

        static bool TeamplayAlive(ZObject unit) => !unit.Destroyed && unit.KillAt < 0;
    }
}
