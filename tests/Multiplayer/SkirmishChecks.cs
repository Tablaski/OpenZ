using System;
using System.IO;
using System.Linq;
using OpenZ;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class SkirmishChecks
{
    public static void Run(Action<bool, string> check)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var entry = OzAiMapCatalog.ThreePlayerSkirmish;
        string settings = File.ReadAllText(Path.Combine(root, "source/Assets/OpenZ/Resources/OpenZ/default_settings.txt"));
        CheckMapFilters(root, settings, check);
        CheckMatchSeats(check);
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

    static void CheckMapFilters(string root, string settings, Action<bool, string> check)
    {
        check(OzAiMapCatalog.SelectMultiplayer(2, "ALL", "all", 0).SequenceEqual(OzAiMapCatalog.Select(false)), "Two-start filter preserves the duel catalogue");
        foreach (var (starts, expected) in new[] { (3, 10), (4, 11), (8, 2) })
        {
            var maps = OzAiMapCatalog.SelectMultiplayer(starts, "ALL", "all", 0);
            check(maps.Length == expected, $"The {starts}-start filter lists all {expected} maps");
            foreach (var entry in maps)
            {
                string grid = File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps", entry.Id + "_grid.json"));
                var world = ZWorld.Create(settings, grid, 12);
                var forts = world.Objects.Where(o => o.IsFort).Select(o => (int)o.Owner).OrderBy(owner => owner);
                check(forts.SequenceEqual(Enumerable.Range(1, starts)), entry.Id + " retains each native fort owner");
                check(!DuelRules.IsMapAllowed(entry.Id), entry.Id + " remains unavailable to a two-seat duel");
                world.ConfigureSkirmishBots(Enumerable.Range(2, starts - 1).Select(position => ((Team)position, ZBotDifficulty.Normal)), 12);
                for (int tick = 0; tick < 240; tick++) world.Tick(ZConst.TICK);
                check(world.Bots.Count == starts - 1 && !world.GameOver, entry.Id + " runs with a bot at every non-human start");
            }
        }
        check(OzAiMapCatalog.SelectMultiplayer(8, "ALL", "jungle", 0).Single().Name == "Hunters", "Terrain and capacity filters combine");
        check(OzAiMapCatalog.SelectMultiplayer(6, "ALL", "all", 0).Length == 0, "Deferred P06 stays outside the map selector");
    }

    static void CheckMatchSeats(Action<bool, string> check)
    {
        var human = new MatchSeatAssignment(MatchSeatRole.Human, 0);
        var extra = new MatchSeatAssignment(MatchSeatRole.Human, -1);
        var bot1 = new MatchSeatAssignment(MatchSeatRole.Bot, 1);
        var bot2 = new MatchSeatAssignment(MatchSeatRole.Bot, 2);
        var observer = new MatchSeatAssignment(MatchSeatRole.Observer, -1);
        MatchStartBlocker Start(params MatchSeatAssignment[] assignments) => MultiplayerMatchSeats.Check(3, assignments);
        check(Start(human, bot1, observer) == MatchStartBlocker.EmptyStartingPositions, "Ghosts cannot fill a map start");
        check(Start(human, bot1, bot2, extra) == MatchStartBlocker.UnassignedPlayer, "A human in excess prevents launch");
        check(Start(human, bot1, bot2, new MatchSeatAssignment(MatchSeatRole.Human, 3)) == MatchStartBlocker.UnassignedPlayer,
            "A human cannot use an old position after switching to a smaller map");
        check(Start(human, bot1, bot2, observer) == MatchStartBlocker.None, "Enough active seats plus observers can launch");
        check(Start(human, human, bot1, bot2) == MatchStartBlocker.InvalidAssignment, "A starting place cannot be shared");
        check(Start(new MatchSeatAssignment(MatchSeatRole.Bot, 0), bot1, bot2, observer) == MatchStartBlocker.NoActiveHuman,
            "The room requires an active human");
        check(human.MayUsePlayerCommands && !observer.MayUsePlayerCommands && !bot1.MayUsePlayerCommands, "Only assigned humans have player commands");
        check(observer.MayFollow(human) && !observer.MayFollow(bot1) && !observer.MayFollow(observer), "Ghost can follow an active human only");
        check(observer.SharesMatchChatWith(observer) && !observer.SharesMatchChatWith(human) && !human.SharesMatchChatWith(observer), "Observer and active match chat are separate");
    }
}
