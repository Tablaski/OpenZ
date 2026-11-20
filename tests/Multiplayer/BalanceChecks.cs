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

        int standard = Misses(false), wider = Misses(true);
        check(standard == 0 && wider >= 24 && wider < 110, "Optional howitzers must miss more often without eliminating accurate shots");
        Console.WriteLine("Howitzer impacts outside the blast radius (120 fixed-seed shots): normal " + standard + ", wider spread " + wider);

        int Misses(bool nerfed)
        {
            var firing = ZWorld.Create(settings, map, 21);
            firing.ConfigureDuel(new DuelRules { NerfedHowitzers = nerfed }, false);
            var cannon = firing.CreateObject("cannon", "howitzer", 256, 256, Team.Red);
            var target = firing.CreateObject("vehicle", "jeep", 352, 256, Team.Blue);
            var fire = typeof(ZObject).GetMethod("ProcessAttackDamage", BindingFlags.Instance | BindingFlags.NonPublic);
            int misses = 0;
            for (int i = 0; i < 120; i++)
            {
                cannon.AttackObject = target;
                fire.Invoke(cannon, new object[] { 100d + 10 * i, true });
                var shot = firing.Events.Last(e => e.type == ZEventType.MissileFired);
                int dx = shot.x - target.CenterX, dy = shot.y - target.CenterY;
                if (dx * dx + dy * dy > shot.radius * shot.radius) misses++;
            }
            return misses;
        }
    }

    static bool Nearly(double a, double b) => Math.Abs(a - b) < 1e-10;
}
