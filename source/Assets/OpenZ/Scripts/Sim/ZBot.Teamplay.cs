using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenZ.Sim
{
    public sealed partial class ZBot
    {
        const int TeamFortRadius = 360, TeamSupportRadius = 640, TeamBattleRadius = 240;

        internal void CoordinateWithAllies(List<ZObject> idle)
        {
            if (!W.HasActiveAlly(Team) || idle.Count == 0) return;
            var forts = W.Objects.Where(o => o.IsFort && TeamplayAlive(o) && o.Owner != Team.Neutral && W.AreAllied(o.Owner, Team)).ToArray();
            var home = forts.FirstOrDefault(o => o.Owner == Team);
            if (home == null) return;

            var combat = W.UnitObjects.Where(o => TeamplayAlive(o) && o.Owner != Team.Neutral && o.CanAttack()).ToArray();
            var enemies = combat.Where(o => !W.AreAllied(Team, o.Owner) && VisibleToBot(o)).ToArray();
            var homeThreats = enemies.Where(o => D(home, o) <= TeamFortRadius || o.AttackObject == home).ToArray();
            var ready = idle.Where(o => o.CanMove && o.CanAttack() && !o.Stunned && !o.IsMoving &&
                o.AttackObject == null && (long)o.Health * 3 >= o.MaxHealth).ToList();
            if (ready.Count == 0) return;

            int budget = Math.Max(1, ready.Count / 3);
            if (Difficulty == ZBotDifficulty.Easy) budget = 1;
            if (homeThreats.Length > 0)
            {
                // A threat to our own fort takes precedence over any commitment elsewhere.
                ready.RemoveAll(o => D(home, o) > TeamSupportRadius);
                foreach (var unit in ready.OrderBy(o => D(home, o)).Take((Difficulty == ZBotDifficulty.Easy ? 1 : Math.Max(2, budget))))
                    SendTeamDefense(unit, homeThreats, idle);
                return;
            }

            // Keep the closest available defenders at the fort when fielding a relief force.
            var reserve = ready.Where(o => D(home, o) <= TeamFortRadius).OrderBy(o => D(home, o)).Take(2).ToArray();
            ready.RemoveAll(o => reserve.Contains(o));
            if (ready.Count == 0) return;

            var fortThreats = enemies.Where(o => forts.Any(f => f != home &&
                (D(f, o) <= TeamFortRadius || o.AttackObject == f))).ToArray();
            var territoryThreats = enemies.Where(o =>
            {
                int zone = W.GetZoneIndex(o.CenterX, o.CenterY);
                return zone >= 0 && W.Zones[zone].Owner != Team.Neutral && W.AreAllied(Team, W.Zones[zone].Owner);
            }).ToArray();
            var advance = combat.Where(o => o.Owner != Team && W.AreAllied(Team, o.Owner) && o.IsMobile && !o.IsMinion)
                .Select(o => (Unit: o, Target: TeamAttackTarget(o)))
                .Where(p => p.Target != null && D(p.Unit, p.Target) <= TeamSupportRadius).ToArray();

            bool sent = false, diversion = false;
            foreach (var unit in ready.OrderBy(o => TeamplayPriority(o, fortThreats, territoryThreats, advance)).ThenBy(o => o.Id))
            {
                if (budget == 0) break;
                int before = Orders;
                if (!SendTeamDefense(unit, fortThreats, idle) && !SendTeamDefense(unit, territoryThreats, idle))
                {
                    foreach (var front in advance.OrderBy(p => D(unit, p.Unit)))
                    {
                        if (D(unit, front.Unit) > TeamSupportRadius) break;
                        double friendly = BattlePower(front.Target, combat, true);
                        double hostile = BattlePower(front.Target, enemies, false);
                        // Keep our units out of assaults they cannot materially reinforce.
                        if (friendly + TeamplayPower(unit) < hostile * .75) continue;
                        if (!diversion && Difficulty != ZBotDifficulty.Easy && friendly > hostile && fortThreats.Length == 0)
                        {
                            var flank = W.Flags.Where(f => W.AreAllied(f.Owner, front.Target.Owner) &&
                                D(f, front.Target) >= 2 * TeamBattleRadius && D(unit, f) <= TeamFortRadius &&
                                enemies.All(e => D(e, f) > TeamBattleRadius) && CanReachTeamPoint(unit, f, f.CenterX, f.CenterY))
                                .OrderBy(f => D(unit, f)).FirstOrDefault();
                            if (flank != null)
                            {
                                Command(unit, flank, coordinated: true);
                                if (Orders > before) { diversion = true; break; }
                            }
                        }
                        if (unit.CanAttackObject(front.Target) && CanReachTeamPoint(unit, front.Target, front.Target.CenterX, front.Target.CenterY))
                            Command(unit, front.Target, coordinated: true);
                        else if (D(unit, front.Unit) > 96)
                        {
                            // Reinforce the ally's rear without aiming for the tile occupied by its squad.
                            double distance = D(front.Unit, front.Target);
                            double offset = 80 / Math.Max(80, distance);
                            int x = front.Unit.CenterX + (int)((front.Unit.CenterX - front.Target.CenterX) * offset);
                            int y = front.Unit.CenterY + (int)((front.Unit.CenterY - front.Target.CenterY) * offset);
                            if (CanReachTeamPoint(unit, front.Unit, x, y))
                                IssueOrder(unit, front.Unit, WpMode.Move, x, y, false, true);
                        }
                        if (Orders > before) break;
                    }
                }
                if (Orders == before) continue;
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
            foreach (var threat in fortThreats) distance = Math.Min(distance, D(unit, threat));
            foreach (var threat in territoryThreats) distance = Math.Min(distance, D(unit, threat));
            foreach (var front in advance) distance = Math.Min(distance, D(unit, front.Unit));
            return distance;
        }

        bool SendTeamDefense(ZObject unit, ZObject[] threats, List<ZObject> idle)
        {
            var target = threats.Where(o => unit.CanAttackObject(o) && D(unit, o) <= TeamSupportRadius &&
                CanReachTeamPoint(unit, o, o.CenterX, o.CenterY)).OrderBy(o => D(unit, o)).FirstOrDefault();
            if (target == null) return false;
            int before = Orders;
            Command(unit, target, coordinated: true);
            if (Orders == before) return false;
            idle.Remove(unit);
            return true;
        }

        ZObject TeamAttackTarget(ZObject ally)
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
                !W.AreAllied(Team, target.Owner) && VisibleToBot(target) ? target : null;
        }

        bool CanReachTeamPoint(ZObject unit, ZObject target, int x, int y) =>
            BotPairRegion(unit, target, unit.LocX + 8, unit.LocY + 8, x, y);

        double BattlePower(ZObject target, ZObject[] units, bool friendly)
        {
            double power = 0;
            foreach (var unit in units)
                if (D(target, unit) <= TeamBattleRadius && (!friendly || W.AreAllied(Team, unit.Owner)))
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
