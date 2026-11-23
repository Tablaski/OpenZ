using System;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenZ;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class BalanceChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../source/Assets/OpenZ/Resources/OpenZ"));
        string settings = File.ReadAllText(Path.Combine(root, "default_settings.txt"));
        string map = File.ReadAllText(Path.Combine(root, "Maps/L02_grid.json"));
        var rules = new DuelRules { FortSpeedup = 5, ProgressiveFortSpeedup = true, TerritoryReductionCap = .2, ProgressiveTerritoryCap = true };
        check(Nearly(rules.TerritoryTimeReduction(1, 0), .5) && Nearly(rules.TerritoryTimeReduction(1, 25), .35) &&
              Nearly(rules.TerritoryTimeReduction(1, 50), .2) && Nearly(rules.TerritoryTimeReduction(1, -50), .5), "Progressive cap must follow the leading army from equal forces to a +50% lead");
        check(Nearly(rules.FortCatchupSpeed(0, .2), 1) && Nearly(rules.FortCatchupSpeed(10, .2), 1.625) &&
              Nearly(rules.FortCatchupSpeed(20, .2), 2.25) && Nearly(rules.FortCatchupSpeed(80, .2), 2.25),
            "Progressive fort boost must reach five times the opponent's territorial speed bonus at a −20% gap");
        var world = ZWorld.Create(settings, map);
        world.Objects.RemoveAll(o => o.IsUnit);
        foreach (var flag in world.Flags) flag.Owner = Team.Blue;
        world.CreateObject("vehicle", "heavy", 160, 160, Team.Red);
        world.CreateObject("vehicle", "heavy", 600, 600, Team.Blue);
        world.ConfigureDuel(rules, false);
        var fort = world.Objects.First(o => o.IsFort && o.Owner == Team.Red);
        var factory = world.Objects.First(o => o.IsProducer && !o.IsFort);
        factory.Owner = Team.Red;
        check(Nearly(fort.BuildTimeModified(100), 100) && Nearly(factory.BuildTimeModified(100), 100), "Equal armies received a catch-up bonus");
        fort.Building = true; fort.CurUnit = "grunt"; fort.BInit = 0; fort.BFinal = 100; world.Time = 20;
        world.CreateObject("vehicle", "heavy", 664, 600, Team.Blue);
        world.DuelUpdateFortBonus();
        check(Nearly(fort.BuildTimeModified(100), 100 / 2.25) && Nearly(factory.BuildTimeModified(100), 100),
            "The fort must use the enemy's capped territory bonus without boosting factories");
        check(Nearly(fort.ProductionProgress, .2), "Activating the boost erased completed fort work");
        world.Objects.RemoveAll(o => o.IsUnit && o.Owner == Team.Blue);
        world.DuelUpdateFortBonus();
        check(Nearly(fort.BuildTimeModified(100), 100) && Nearly(fort.ProductionProgress, .2) && world.DuelFortLoserReduction(Team.Red) == 0,
            "Expiring the boost erased completed fort work");

        var tech = ZWorld.Create(settings, map);
        var vehicles = tech.Objects.Where(o => o.Type == "vehicle_factory").ToArray();
        var robots = tech.Objects.Where(o => o.Type == "robot_factory").ToArray();
        check(vehicles.Length >= 2 && robots.Length > 0, "Tech regression map is missing factories");
        for (int i = 0; i < vehicles.Length; i++) vehicles[i].Level = i == 0 ? 1 : 3;
        var robotLevels = robots.Select(o => o.Level).ToArray();
        var factoryRules = new DuelRules { VehicleTech = 5 };
        tech.ConfigureDuel(factoryRules, false);
        check(vehicles[0].Level == 2 && vehicles.Skip(1).All(o => o.Level == 4) &&
              robots.Select(o => o.Level).SequenceEqual(robotLevels) && tech.Objects.Where(o => o.IsFort).All(o => o.Level == 4),
            "Changing the vehicle maximum must shift its group, leave robots unchanged and move both forts to the maximum");
        factoryRules.RobotTech = 2;
        factoryRules.SelectMap("L18");
        check(factoryRules.VehicleTech == 0 && factoryRules.RobotTech == 0, "New maps must inherit their own tech settings");

        foreach (bool skirmish in new[] { false, true })
        {
            string lobby = skirmish ? "Skirmish" : "Duel";
            foreach (bool mounted in new[] { false, true })
            {
                string placement = mounted ? "fort-mounted" : "ground";
                var standard = Shots(skirmish, false, "howitzer", mounted);
                var wider = Shots(skirmish, true, "howitzer", mounted);
                check(standard.All(s => s.dx >= -16 && s.dx < 16 && s.dy >= -16 && s.dy < 16) &&
                      wider.All(s => s.dx >= -50 && s.dx < 50 && s.dy >= -50 && s.dy < 50) &&
                      !standard.Any(Missed) && wider.Count(Missed) >= 24 && wider.Count(Missed) < 110 &&
                      standard.Select(s => s.radius).SequenceEqual(wider.Select(s => s.radius)),
                    $"{lobby}: {placement} howitzers gain wider spread and miss more often without changing splash radius");
                foreach (string type in DuelRules.AllowedFortTurrets.Where(type => type != "howitzer"))
                    check(Shots(skirmish, false, type, mounted).SequenceEqual(Shots(skirmish, true, type, mounted)),
                        $"{lobby}: {placement} {type} firing is unaffected by the howitzer rule");
            }
            check(Shots(skirmish, false, "roof", false).SequenceEqual(Shots(skirmish, true, "roof", false)),
                $"{lobby}: the separate fort roof weapon is unaffected by the howitzer rule");
        }

        static bool Missed((ZEventType type, int dx, int dy, int radius, bool flag) shot) =>
            shot.dx * shot.dx + shot.dy * shot.dy > shot.radius * shot.radius;

        (ZEventType type, int dx, int dy, int radius, bool flag)[] Shots(bool skirmish, bool nerfed, string type, bool mounted)
        {
            var firing = ZWorld.Create(settings, map, 21);
            if (skirmish)
            {
                var room = new SkirmishRules { NerfedHowitzers = nerfed, OverrideFortTurrets = true };
                Array.Copy(DuelRules.AllowedFortTurrets, room.FortTurrets, 4);
                firing.SetSkirmishAlliances(new[] { 0, 1 });
                firing.ConfigureSkirmishRules(room, Array.Empty<int>());
            }
            else
            {
                var duel = new DuelRules { NerfedHowitzers = nerfed };
                duel.Common.FortTurrets = DuelRules.AllowedFortTurrets.ToArray();
                firing.ConfigureDuel(duel, false);
                firing.DuelApplyStartingArmies();
            }

            bool roof = type == "roof";
            var attacker = roof ? firing.Objects.First(o => o.IsFort && o.Owner == Team.Red) :
                mounted ? firing.Objects.Single(o => o.Type == type && o.Owner == Team.Red && !o.Ejectable) :
                firing.CreateObject("cannon", type, 256, 256, Team.Red);
            firing.Objects.RemoveAll(o => o.IsUnit && o != attacker);
            int distance = roof ? attacker.WidthPix / 2 + ZObject.FortGunMinEdgePx + 8 : 96;
            var target = firing.CreateObject("vehicle", "jeep", attacker.CenterX + distance - 16, attacker.CenterY - 16, Team.Blue);
            var fire = typeof(ZObject).GetMethod("ProcessAttackDamage", BindingFlags.Instance | BindingFlags.NonPublic);
            firing.Events.Clear();
            for (int i = 0; i < 120; i++)
            {
                double time = 100 + 40 * i;
                if (roof)
                {
                    attacker.ProcessFortGun(time);
                    attacker.ProcessFortGun(time + ZObject.FortGunLay + 0.1);
                }
                else
                {
                    attacker.AttackObject = target;
                    fire.Invoke(attacker, new object[] { time, true });
                }
            }
            var shots = firing.Events.Where(e => e.objId == attacker.Id && (e.type == ZEventType.MissileFired || e.type == ZEventType.Hitscan))
                .Select(e => (e.type, e.x - target.CenterX, e.y - target.CenterY, e.radius, e.flag)).ToArray();
            check(shots.Length == 120, $"Expected 120 shots from {type}, got {shots.Length}");
            return shots;
        }
    }

    static bool Nearly(double a, double b) => Math.Abs(a - b) < 1e-10;
}
