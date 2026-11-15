using System;
using System.IO;
using System.Linq;
using OpenZ;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class BalanceChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../source/Assets/OpenZ/Resources/OpenZ"));
        var world = ZWorld.Create(File.ReadAllText(Path.Combine(root, "default_settings.txt")), File.ReadAllText(Path.Combine(root, "Maps/L02_grid.json")));
        world.Objects.RemoveAll(o => o.IsUnit);
        world.CreateObject("vehicle", "heavy", 160, 160, Team.Red);
        for (int i = 0; i < 3; i++) world.CreateObject("vehicle", "heavy", 600 + i * 64, 600, Team.Blue);
        world.ConfigureDuel(new DuelRules { FortLoserMaximum = .5, TerritoryReductionCap = .25 }, false);
        var fort = world.Objects.First(o => o.IsFort && o.Owner == Team.Red);
        var factory = world.Objects.First(o => o.IsProducer && !o.IsFort);
        factory.Owner = Team.Red; factory.ZoneOwnage = fort.ZoneOwnage = 1;
        check(fort.BuildTimeModified(100) == 37.5 && factory.BuildTimeModified(100) == 75, "Fort bonus affected a factory or ignored territory cap");
        fort.Building = true; fort.CurUnit = "grunt"; fort.BInit = 0; fort.BFinal = 100; world.Time = 20;
        world.DuelUpdateFortBonus();
        check(Math.Abs(fort.ProductionProgress - .2) < 1e-10, "Activating bonus erased completed fort work");
        double boostedDuration = fort.BFinal - fort.BInit;
        world.Objects.RemoveAll(o => o.IsUnit && o.Owner == Team.Blue);
        world.DuelUpdateFortBonus();
        check(world.DuelFortLoserReduction(Team.Red) == 0 && Math.Abs(fort.BFinal - fort.BInit - boostedDuration * 2) < 1e-10, "Bonus failed to expire when army gap disappeared");
        check(Math.Abs(fort.ProductionProgress - .2) < 1e-10, "Expiring bonus erased completed fort work");
    }
}
