using System;

namespace OpenZ.Sim
{
    internal sealed partial class ZAi2026
    {
        readonly double[] military = new double[2], territory = new double[2], production = new double[2], security = new double[2];
        readonly double[] forecasts = new double[3];
        ZObject assaultFort;
        double assaultUntil, nextAssault, behindSince = -1, strategicRatio = 1, lastTerritory, lastTerritoryAt;
        bool earlyAssaultUsed;

        void EvaluateStrategy()
        {
            Array.Clear(military, 0, 2); Array.Clear(territory, 0, 2);
            Array.Clear(production, 0, 2); Array.Clear(security, 0, 2);
            foreach (var a in allies) military[0] += ZAiCombat.Power(a);
            foreach (var producer in producers) production[0] += Output(producer, 90);
            foreach (var o in world.Objects)
                if (o.Owner != team && o.Owner != Team.Neutral && world.AreAllied(team, o.Owner) && Alive(o) && o.IsProducer)
                    production[0] += Output(o, 90);
            foreach (var item in contacts.Values)
            {
                double confidence = Math.Max(0, 1 - (world.Time - item.SeenAt) / tuning.Memory);
                military[1] += item.Power * confidence;
                if (!item.Producer) continue;
                // Enemy build choices and their queues are private. Estimate capacity from known factories.
                var expected = world.S.Unit(item.Type == "robot_factory" ? "tough" : "light");
                if (expected != null) production[1] += confidence * 90 / Math.Max(1, expected.build_time) * Value(expected);
            }

            foreach (var flag in world.Flags)
            {
                bool ours = flag.Owner != Team.Neutral && world.AreAllied(team, flag.Owner);
                if (ours || flag.Owner != Team.Neutral && sight.CanSee(flag.CenterX, flag.CenterY, false))
                    territory[ours ? 0 : 1] += level >= ZBotDifficulty.Skilled ? FlagImportance(flag) : 1;
            }
            foreach (var fort in forts)
                security[0] += 1 / (1 + ZAiTeamPlay.FortThreat(fort, enemies).Urgency);
            foreach (var enemy in enemies)
                if (enemy.IsFort) security[1] += 1 + FortGuards(enemy) / Math.Max(1, military[1]);

            double force = Ratio(military), build = Ratio(production), land = Ratio(territory), fortsRatio = Ratio(security);
            strategicRatio = .4 * force + .25 * build + .20 * land + .15 * fortsRatio;
            for (int i = 0; i < forecasts.Length; i++)
            {
                double span = (i + 1) / 3d;
                forecasts[i] = (military[0] + production[0] * span + 1) / (military[1] + production[1] * span + 1);
            }
            if (level < ZBotDifficulty.Skilled) return;

            bool falling = lastTerritoryAt > 0 && land < .75 && land < lastTerritory * .90;
            if (lastTerritoryAt == 0 || world.Time - lastTerritoryAt >= 30)
            {
                lastTerritoryAt = world.Time;
                lastTerritory = land;
            }
            bool behind = force < .8 && build < .7 && forecasts[1] < .8;
            if (behind)
            {
                if (behindSince < 0) behindSince = world.Time;
            }
            else behindSince = -1;
            bool desperate = behindSince >= 0 && world.Time - behindSince >= 30 ||
                falling && forecasts[1] < .85 || ZAiTeamPlay.FortThreat(home, enemies).Urgency > 2 && forecasts[0] < .85;

            if (assaultFort != null && world.Time < assaultUntil && assaultFort.CanEnterFort(team) &&
                sight.CanSee(assaultFort.CenterX, assaultFort.CenterY, false)) return;
            assaultFort = null;
            if (world.Time < nextAssault) return;

            bool early = world.Time < 300;
            if (early && !desperate && (level != ZBotDifficulty.Zod || earlyAssaultUsed || world.Time < 120)) return;
            double best = 0;
            foreach (var fort in enemies)
            {
                if (!fort.IsFort || !fort.CanEnterFort(team) || !fort.GetCreationMovePoint(out int x, out int y)) continue;
                double defenders = FortGuards(fort);
                double attackers = 0;
                int fast = 0;
                foreach (var unit in units)
                {
                    if (!EligibleAssault(unit) || ZAiGeometry.DistanceSquared(unit, x, y) > 820L * 820) continue;
                    attackers += Math.Max(1, ZAiCombat.Power(unit));
                    if (unit.MoveSpeed >= 24 || unit.Type == "crane") fast++;
                }
                if (fast < (desperate ? 1 : 2) || attackers <= 0) continue;
                double opportunity = (attackers + 15) / (defenders + 15);
                double needed = desperate ? .75 : early ? 2 : 1.45;
                if (opportunity < needed || opportunity <= best) continue;
                if (early && !desperate && Occasion(fort.Id, (int)(world.Time / 30), 8) >= .30)
                {
                    nextAssault = world.Time + 15;
                    continue;
                }
                best = opportunity;
                assaultFort = fort;
            }
            if (assaultFort == null) return;
            earlyAssaultUsed |= early;
            assaultUntil = world.Time + 22;
            nextAssault = world.Time + 32;
        }

        static double Ratio(double[] value) => Math.Min(3, (value[0] + 1) / (value[1] + 1));

        double FortGuards(ZObject fort)
        {
            double sum = 0;
            foreach (var unit in enemies)
            {
                if (!unit.IsUnit || !world.AreAllied(unit.Owner, fort.Owner)) continue;
                double distance = ZAiGeometry.Distance(unit, fort);
                if (distance < 430) sum += ZAiCombat.Power(unit) * (1 - distance / 625);
            }
            return sum;
        }

        bool EligibleAssault(ZObject unit)
        {
            if (unit.Type == "crane") return level == ZBotDifficulty.Zod && unit.Health > unit.MaxHealth / 2;
            return unit.MoveSpeed >= 19 && unit.Health > unit.MaxHealth / 3;
        }

        bool TryAssault(ZObject unit)
        {
            if (assaultFort == null || !EligibleAssault(unit) || !assaultFort.GetCreationMovePoint(out int x, out int y) ||
                ZAiGeometry.DistanceSquared(unit, x, y) > 820L * 820 || !Reachable(unit, x, y, 2)) return false;
            return Issue(unit, assaultFort, WpMode.EnterFort, true);
        }

        double FlagImportance(ZObject flag)
        {
            double value = 10;
            foreach (var o in flag.LinkedObjects)
            {
                if (o.IsProducer && !o.IsFort) value += 13 + 2 * o.BuildLevel;
                else if (o.Type == "repair" || o.Type == "radar") value += 6;
                else if (o.IsBridge) value += 5;
            }
            return value;
        }

        double Value(ZUnitSettings stats)
        {
            double reload = Math.Max(.2, stats.attack_speed);
            double hit = stats.attack_missile_speed > 0 ? 1 : Math.Max(.05, stats.attack_damage_chance);
            double firepower = Math.Max(1, stats.Damage * hit / reload);
            double reach = 1 + Math.Min(450, stats.attack_radius) / 600d;
            return Math.Sqrt(Math.Max(1, stats.MaxHealth) * firepower) * reach * Math.Max(1, stats.group_amount);
        }

        double Output(ZObject factory, int horizon)
        {
            if (factory.CurUnit == null) return 0;
            var running = world.S.Unit(factory.CurUnit);
            if (running == null) return 0;
            double arrival = Math.Max(0, factory.BFinal - world.Time), output = 0;
            if (arrival > horizon) return 0;
            output += Value(running);
            foreach (var queued in factory.Queue)
            {
                var stats = world.S.Unit(queued);
                if (stats == null) continue;
                arrival += Math.Max(1, factory.BuildTimeModified(stats.build_time));
                if (arrival > horizon) return output;
                output += Value(stats);
            }
            if (factory.RepeatProduction)
                output += Math.Floor((horizon - arrival) / Math.Max(1, factory.BuildTimeModified(running.build_time))) * Value(running);
            return output;
        }

        void PlanProduction()
        {
            foreach (var factory in producers)
            {
                if (!orders.Available) return;
                bool advanced = level >= ZBotDifficulty.Skilled;
                ZObject enemy = null;
                if (level > ZBotDifficulty.Noob)
                    foreach (var o in enemies)
                        if (o.IsMobile && ZAiGeometry.DistanceSquared(o, factory.CenterX, factory.CenterY) < 240L * 240 &&
                            world.GetZoneIndex(o.CenterX, o.CenterY) == factory.ZoneIndex)
                        { enemy = o; break; }
                if (enemy != null && !factory.IsFort && enemy.AttackObject != factory &&
                    Occasion(factory.Id, enemy.Id, 9) < tuning.CancelChance &&
                    (!factory.Building || factory.BFinal - world.Time > 4))
                {
                    if (factory.Queue.Count > 0 && advanced) orders.CancelLast(factory);
                    if (factory.Building) orders.Production(factory, null);
                    continue;
                }
                if (factory.Building && factory.CurUnit != "grunt" && factory.CurUnit != "jeep") continue;
                if (factory.Building && factory.BFinal - world.Time <= 4) continue;
                if (Occasion(factory.Id, (int)factory.BInit, 7) < tuning.MissProduction) continue;
                string best = null;
                double bestValue = double.NegativeInfinity;
                foreach (string type in world.ProductionUnits(factory))
                {
                    var stats = world.S.Unit(type);
                    if (stats == null || stats.Damage <= 0) continue;
                    double seconds = Math.Max(1, factory.BuildTimeModified(stats.build_time));
                    double value = Value(stats) / Math.Pow(seconds, enemy != null ? .92 : advanced ? .55 : .72);
                    if (type == "grunt" || type == "psycho") value *= advanced ? .65 : .9;
                    if (advanced && enemy != null)
                    {
                        if (stats.attack_missile_speed > 0 && !enemy.IsRobot) value *= 1.16;
                        if (stats.attack_radius > enemy.AttackRadius) value *= 1.2;
                    }
                    if (type == factory.CurUnit) value *= 1.12;
                    if (value <= bestValue) continue;
                    bestValue = value; best = type;
                }
                if (best != null) orders.Production(factory, best);
            }
        }
    }
}
