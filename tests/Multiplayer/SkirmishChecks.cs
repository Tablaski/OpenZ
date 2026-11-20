using System;
using System.IO;
using System.Linq;
using OpenZ;
using OpenZ.Sim;

static class SkirmishChecks
{
    public static void Run(Action<bool, string> check)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var entry = OzAiMapCatalog.ThreePlayerSkirmish;
        string settings = File.ReadAllText(Path.Combine(root, "source/Assets/OpenZ/Resources/OpenZ/default_settings.txt"));
        string grid = File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps", entry.Id + "_grid.json"));
        var world = ZWorld.Create(settings, grid, 12);
        var teams = new[] { Team.Red, Team.Blue, Team.Green };
        check(world.Objects.Where(o => o.IsFort).Select(o => o.Owner).OrderBy(team => team).SequenceEqual(teams), "P03M01 has its three native fort owners");
        check(teams.All(team => world.UnitCount(team) > 0 && world.MilitaryValue(team) > 0), "All three seats have starting units and attacking power");
        world.ConfigureThreePlayerSkirmish(ZBotDifficulty.Easy, ZBotDifficulty.Normal, 12);
        check(world.Bots.Select(b => (b.Team, b.Difficulty)).SequenceEqual(new[] { (Team.Blue, ZBotDifficulty.Easy), (Team.Green, ZBotDifficulty.Normal) }), "Both bots keep their separately selected difficulty");
        for (int i = 0; i < 1200; i++) world.Tick(ZConst.TICK);
        check(world.Bots[0].Rounds == 0 && world.Bots[1].Rounds > 0, "Green bot thinks while Easy blue is still waiting");
        while (world.Time < 75 && !world.GameOver) world.Tick(ZConst.TICK);
        check(world.Bots.All(bot => bot.Orders > 0), "Blue and green both issue orders on the real map: " +
            string.Join(", ", world.Bots.Select(bot => $"{bot.Team} {bot.Orders} orders, {bot.Rounds} rounds")));
        check(!world.GameOver && world.SurrenderSkirmish(Team.Red), "Human can surrender without ending a three-side skirmish");
        check(world.SkirmishEliminated(Team.Red) && !world.SurrenderSkirmish(Team.Red) &&
            !world.Objects.Any(o => o.Owner == Team.Red && o.IsUnit && !o.Destroyed), "Surrender eliminates the human seat exactly once");
        for (int i = 0; i < 150; i++) world.Tick(ZConst.TICK);
        check(!world.GameOver && world.UnitCount(Team.Blue) > 0 && world.UnitCount(Team.Green) > 0, "Both bots remain in the match after the human surrenders");
        var blue = world.Objects.Single(o => o.IsFort && o.Owner == Team.Blue);
        blue.SetHealth(0);
        world.UpdateObjectHealth(blue);
        for (int i = 0; i < 150; i++) world.Tick(ZConst.TICK);
        check(world.GameOver && world.Winner == Team.Green, "The last remaining contender wins, including green");
    }
}
