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
        CheckTwoStartSkirmish(root, settings, check);
        string grid = File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps", entry.Id + "_grid.json"));
        var world = ZWorld.Create(settings, grid, 12);
        var teams = new[] { Team.Red, Team.Blue, Team.Green };
        check(world.Objects.Where(o => o.IsFort).Select(o => o.Owner).OrderBy(team => team).SequenceEqual(teams), "P03M01 has its three native fort owners");
        check(teams.All(team => world.UnitCount(team) > 0 && world.MilitaryValue(team) > 0), "All three seats have starting units and attacking power");
        world.ConfigureThreePlayerSkirmish(ZBotDifficulty.Easy, ZBotDifficulty.Normal, 12);
        world.SetSkirmishAlliances(new[] { 0, 1, 2 });
        world.ConfigureSkirmishRules(new SkirmishRules(), Array.Empty<int>());
        check(world.Bots.Select(b => (b.Team, b.Difficulty)).SequenceEqual(new[] { (Team.Blue, ZBotDifficulty.Easy), (Team.Green, ZBotDifficulty.Normal) }), "Both bots keep their separately selected difficulty");
        for (int i = 0; i < 1200; i++) world.Tick(ZConst.TICK);
        check(world.Bots[0].Rounds == 0 && world.Bots[1].Rounds > 0, "Green bot thinks while Easy blue is still waiting");
        while (world.Time < 75 && !world.GameOver) world.Tick(ZConst.TICK);
        check(world.Bots.All(bot => bot.Orders > 0), "Blue and green both issue orders on the real map: " +
            string.Join(", ", world.Bots.Select(bot => $"{bot.Team} {bot.Orders} orders, {bot.Rounds} rounds")));
        check(!world.GameOver && world.SurrenderSkirmish(Team.Red), "Human can surrender without ending a three-side skirmish");
        check(world.SkirmishEliminated(Team.Red) && !world.SurrenderSkirmish(Team.Red) &&
            !world.Objects.Any(o => o.Owner == Team.Red && o.IsUnit && !o.Destroyed), "Surrender eliminates the human seat exactly once");
        check(world.Events.Count(e => e.type == ZEventType.TeamEliminated) == 1 && !world.IsFinalSkirmishFort(Team.Red),
            "An early elimination produces one HUD event and no final camera cue");
        for (int i = 0; i < 150; i++) world.Tick(ZConst.TICK);
        check(!world.GameOver && world.UnitCount(Team.Blue) > 0 && world.UnitCount(Team.Green) > 0, "Both bots remain in the match after the human surrenders");
        var blue = world.Objects.Single(o => o.IsFort && o.Owner == Team.Blue);
        blue.SetHealth(0);
        world.UpdateObjectHealth(blue);
        world.Tick(ZConst.TICK);
        check(world.IsFinalSkirmishFort(Team.Blue) && !world.IsFinalSkirmishFort(Team.Red),
            "The authoritative end check marks only the decisive fort in the elimination tick");
        for (int i = 0; i < 150; i++) world.Tick(ZConst.TICK);
        check(world.GameOver && world.Winner == Team.Green, "The last remaining contender wins, including green");
    }

    static void CheckTwoStartSkirmish(string root, string settings, Action<bool, string> check)
    {
        string buildSettings = File.ReadAllText(Path.Combine(root, "source/ProjectSettings/EditorBuildSettings.asset"));
        var entries = OzAiMapCatalog.SkirmishMaps.Where(map => map.Starts == 2 && buildSettings.Contains("path: " + map.ScenePath)).ToArray();
        check(entries.Length > 2 && entries.Select(map => map.Id).Distinct().Count() == entries.Length,
            "Local Skirmish exposes every distinct registered two-start conversion");
        foreach (var entry in entries)
        {
            string grid = File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps", entry.Id + "_grid.json"));
            var world = ZWorld.Create(settings, grid, 12);
            var original = world.Map.FactoryTech();
            check(original.Vehicles == world.MapFactoryTech("vehicle_factory") &&
                original.Robots == world.MapFactoryTech("robot_factory") &&
                original.Fort == world.Objects.Where(unit => unit.IsFort).Max(unit => unit.Level + 1),
                entry.Id + " original technology matches native building records");
            world.Map.skirmish = true;
            var room = new MultiplayerRoom("host", "Host", entry.Id, allowSoloConversions: true);
            room.SetBot("host", 1, 1, ZBotDifficulty.Easy);
            var rules = new SkirmishRules { TerritoryCap = 25, FortSpeedup = 120, VehicleTech = 5, RobotTech = 4,
                RadarLoss = RadarLossRule.FogAndZoom, FriendlyFire = true, PlanningSeconds = 10 };
            rules.OverrideFortTurrets = true;
            rules.FortTurrets[0] = "gatling";
            rules.BuildPercent[0] = 70;
            room.SetRules("host", rules);
            room.SetReady("host", room.Revision, true);
            room.SwapStartingSides("host");
            check(!room.CanLaunch && room.HostPosition == 1 && room.Bots.Single().Position == 0 &&
                room.Bots.Single().Alliance == 1 && room.Rules.BuildPercent[1] == 70,
                entry.Id + " swaps forts and preserves each participant's alliance and build-time advantage");
            room.SwapStartingSides("host");
            check(room.CanLaunchWithHostReady && !room.CanLaunch, entry.Id + " only waits for the human host");
            room.SetReady("host", room.Revision, true);
            room.Launch("host");
            room.MarkLoaded("host", room.Revision);
            room.ConfigureWorld(world, 12);
            check(room.MatchLoaded && world.Bots.Single().Team == Team.Blue && world.Bots.Single().Difficulty == ZBotDifficulty.Easy &&
                world.HasSkirmishProduction && world.SkirmishFriendlyFire && world.DuelRadarLoss == RadarLossRule.FogAndZoom,
                entry.Id + " launches one human and one bot with the selected battle and balancing rules");
            check(world.MapFactoryTech("vehicle_factory") == (original.Vehicles == 0 ? 0 : 5) &&
                world.MapFactoryTech("robot_factory") == (original.Robots == 0 ? 0 : 4),
                entry.Id + " applies separate factory limits");
            check(world.Objects.Where(unit => unit.IsTowerGun && unit.Owner != Team.Neutral)
                .Select(unit => (unit.Owner, unit.Type)).OrderBy(unit => unit.Owner)
                .SequenceEqual(new[] { (Team.Red, "gatling"), (Team.Blue, "gatling") }),
                entry.Id + " applies the same configured turret to both occupied forts");
            var solo = ZWorld.Create(settings, grid, 12);
            solo.ApplySoloMapTech(0, 0, true);
            check(solo.UseNativeMapTech && solo.Map.FactoryTech() == original && !solo.Map.skirmish,
                entry.Id + " can restore native technology without activating skirmish");
        }
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
