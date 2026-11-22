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
        CheckTeamplay(settings, grid, check);
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

    internal static void CheckTeamplay(string settings, string grid, Action<bool, string> check)
    {
        CheckAlliedFlagTransfer(settings, grid, check);
        CheckTeamDefense(settings, grid, check);
        CheckTeamAdvance(settings, grid, check, defendTerritory: false);
        CheckTeamAdvance(settings, grid, check, defendTerritory: true);
        CheckTeamDiversion(settings, grid, check);
        foreach (int cap in new[] { 50, 25 })
        {
            var world = ZWorld.Create(settings, grid, 12);
            world.Map.skirmish = true;
            world.SetSkirmishAlliances(new[] { 0, 0, 1 });
            world.ConfigureSkirmishRules(new SkirmishRules { TerritoryCap = cap, FriendlyFire = true }, Array.Empty<int>());
            for (int i = 0; i < world.Flags.Count; i++)
                world.AwardZone(world.Flags[i], i < 2 ? Team.Red : i == world.Flags.Count - 1 ? Team.Green : Team.Blue);
            world.UpdateSkirmishProduction();
            var red = world.Objects.Single(o => o.IsFort && o.Owner == Team.Red);
            var blue = world.Objects.Single(o => o.IsFort && o.Owner == Team.Blue);
            var green = world.Objects.Single(o => o.IsFort && o.Owner == Team.Green);
            double share = (world.Flags.Count - 1d) / world.Flags.Count;
            check(Math.Abs(red.ZoneOwnage - share) < 1e-9 && blue.ZoneOwnage == red.ZoneOwnage &&
                Math.Abs(red.BuildTimeModified(100) - (100 - Math.Min(cap, 50 * share))) < 1e-9 &&
                red.BuildTimeModified(100) == blue.BuildTimeModified(100), "Allies share the territory multiplier at cap " + cap);
            check(green.ZoneOwnage == world.TeamFlagFraction(Team.Green), "An opposing seat keeps its own territory share");
            var flag = world.Flags[2];
            var unit = world.CreateObject("vehicle", "light", flag.LocX, flag.LocY, Team.Red);
            var attack = new ZWaypoint { mode = WpMode.Attack, refId = blue.Id, x = blue.CenterX, y = blue.CenterY, forceFire = true };
            var pyro = world.CreateRobotGroup("pyro", flag.LocX, flag.LocY, Team.Red);
            world.SendWaypoints(Team.Red, unit, new[] { attack });
            world.SendWaypoints(Team.Red, pyro, new[] { attack });
            check(unit.Waypoints.Count == 0 && pyro.Waypoints.Count == 0 && !blue.CanEnterFort(Team.Red),
                "A normal or forced attack and a fort entry cannot target an ally even with friendly fire enabled");
            var alliedRobot = world.CreateRobotGroup("tough", flag.LocX, flag.LocY, Team.Blue, 1);
            var enemyRobot = world.CreateRobotGroup("tough", flag.LocX, flag.LocY, Team.Green, 1);
            bool runOver = ZWorld.RunOverDeviation;
            try
            {
                ZWorld.RunOverDeviation = true;
                unit.Dx = 1;
                unit.Waypoints.Add(ZWaypoint.Move(flag.CenterX, flag.CenterY));
                check(!ZWorld.RunOverPair(unit, alliedRobot) && ZWorld.RunOverPair(unit, enemyRobot),
                    "Vehicles drive around allied robots and can still run over enemies");
                world.SendWaypoints(Team.Red, unit, Array.Empty<ZWaypoint>());
                enemyRobot.SetCords(green.LocX, green.LocY);
            }
            finally { ZWorld.RunOverDeviation = runOver; }

            var station = world.CreateObject("building", "repair", 160, 160, Team.Blue);
            unit.Health = Math.Max(1, unit.MaxHealth / 2);
            world.SendWaypoints(Team.Red, unit, new[] { new ZWaypoint { mode = WpMode.UnitRepair, refId = station.Id, x = station.CenterX, y = station.CenterY } });
            check(unit.Waypoints.Any(w => w.mode == WpMode.UnitRepair) && station.CanRepairUnit(Team.Red) && !station.CanRepairUnit(Team.Green),
                "The authority accepts repairs from the station owner’s allies only");

            if (cap == 50) CheckRepairOwnership(world, station, unit, check);

            var radar = world.CreateObject("building", "radar", 400, 160, Team.Blue);
            world.CreateObject("building", "radar", 480, 160, Team.Red);
            var picture = new OzRadarLineRule();
            picture.Observe(world);
            check(picture.Covered(Team.Red) && picture.Covered(Team.Blue) && !radar.ProvidesRadarTo(Team.Green), "Allied radar appears for both seats");
            radar.Health = 0;
            picture.Observe(world);
            check(picture.Covered(Team.Red) && picture.Covered(Team.Blue), "The allied backup radar keeps the picture online");
            foreach (var o in world.Objects.Where(o => o.ProvidesRadarTo(Team.Red))) o.Health = 0;
            picture.Observe(world);
            check(!picture.Covered(Team.Red) && !picture.Covered(Team.Blue), "Losing the last allied radar ends the picture for both seats");
        }
    }

    static void CheckAlliedFlagTransfer(string settings, string grid, Action<bool, string> check)
    {
        var world = ZWorld.Create(settings, grid, 12);
        world.Map.skirmish = true;
        world.SetSkirmishAlliances(new[] { 0, 0, 1 });
        world.ConfigureSkirmishRules(new SkirmishRules(), Array.Empty<int>());
        var red = world.Objects.Single(o => o.IsFort && o.Owner == Team.Red);
        var blue = world.Objects.Single(o => o.IsFort && o.Owner == Team.Blue);
        var flags = world.Flags.Where(f => world.UnitObjects.All(o =>
            !ZObject.Within(f.CenterX, f.CenterY, o.CenterX, o.CenterY, 80))).ToArray();
        double DistanceSquared(ZObject a, ZObject b) =>
            Math.Pow(a.CenterX - b.CenterX, 2) + Math.Pow(a.CenterY - b.CenterY, 2);
        var near = flags.First(f => DistanceSquared(f, red) < DistanceSquared(f, blue) &&
            f.LinkedObjects.Any(o => o.IsProducer && !o.IsFort));
        var far = flags.First(f => DistanceSquared(f, red) > DistanceSquared(f, blue));
        world.AwardZone(near, Team.Blue);
        world.AwardZone(far, Team.Blue);
        var factories = world.Objects.Where(o => o.IsProducer && !o.IsFort).ToArray();
        int blueFactories = factories.Count(o => o.Owner == Team.Blue);
        int redFactories = factories.Count(o => o.Owner == Team.Red);
        for (int i = redFactories; i < blueFactories; i++) world.CreateObject("building", "robot_factory", 160, 160, Team.Red);
        check(!world.CanCaptureFlag(Team.Red, near), "An allied flag is blocked when factory counts are equal even if the claimant is nearer");
        var bonus = world.CreateObject("building", "vehicle_factory", 240, 160, Team.Blue);
        bonus.Health = 0;
        check(world.CanCaptureFlag(Team.Red, near) && !world.CanCaptureFlag(Team.Red, far),
            "Fewer owned factories allows transfer only on the closer side, and a ruined factory still counts");
        double aggregate = red.ZoneOwnage;
        var capturing = world.CreateRobotGroup("tough", near.LocX, near.LocY, Team.Red, 1);
        var blocked = world.CreateRobotGroup("tough", far.LocX, far.LocY, Team.Red, 1);
        ZWaypoint FlagOrder(ZObject f) => new ZWaypoint { mode = WpMode.Move, refId = f.Id, x = f.CenterX, y = f.CenterY };
        world.SendWaypoints(Team.Red, capturing, new[] { FlagOrder(near) });
        world.SendWaypoints(Team.Red, blocked, new[] { FlagOrder(far) });
        check(capturing.Waypoints.Count == 1 && blocked.Waypoints.Count == 0, "The authority accepts the eligible capture order and rejects the blocked one");
        while (world.Time < .3) world.Tick(ZConst.TICK);
        check(near.Owner == Team.Red && near.LinkedObjects.Where(o => o.IsProducer).All(o => o.Owner == Team.Red) &&
            far.Owner == Team.Blue && !world.CanCaptureFlag(Team.Blue, near), "Contact transfers eligible territory and its factories, without allowing an immediate reversal");
        check(red.ZoneOwnage == aggregate && blue.ZoneOwnage == aggregate, "A transfer keeps the alliance’s combined territory discount");
    }

    static void CheckRepairOwnership(ZWorld world, ZObject station, ZObject unit, Action<bool, string> check)
    {
        bool inPlace = ZWorld.RepairInPlaceDeviation;
        try
        {
            ZWorld.RepairInPlaceDeviation = false;
            check(station.SetRepairUnit(unit), "An allied station can start the legacy repair cycle");
            world.SendWaypoints(Team.Red, unit, Array.Empty<ZWaypoint>());
            while (world.Time <= station.RepairTime + .1) world.Tick(ZConst.TICK);
            check(world.UnitObjects.Any(o => o.Id > station.Id && o.Type == unit.Type && o.Owner == Team.Red && o.Health == o.MaxHealth),
                "The legacy repair cycle returns the vehicle to its original owner");
        }
        finally { ZWorld.RepairInPlaceDeviation = inPlace; }
    }

    static void CheckTeamDefense(string settings, string grid, Action<bool, string> check)
    {
        foreach (bool allied in new[] { false, true })
        foreach (bool allyIsBot in new[] { false, true })
        {
            var world = ZWorld.Create(settings, grid, 12);
            world.Map.skirmish = true;
            world.SetSkirmishAlliances(allied ? new[] { 0, 0, 1 } : new[] { 0, 1, 2 });
            world.ConfigureSkirmishRules(new SkirmishRules(), Array.Empty<int>());
            var fort = world.Objects.Single(o => o.IsFort && o.Owner == Team.Red);
            var spawn = world.UnitObjects.First(o => o.Owner == Team.Red && o.IsRobot && !o.IsMinion);
            var defender = world.CreateRobotGroup("tough", spawn.LocX, spawn.LocY, Team.Blue, 1);
            var enemy = world.CreateRobotGroup("grunt", spawn.LocX, spawn.LocY, Team.Green, 1);
            enemy.AttackObject = fort;
            var bot = new ZBot(world, Team.Blue, 12);
            world.Bots.Add(bot);
            if (allyIsBot) world.Bots.Add(new ZBot(world, Team.Red, 13));
            bot.CoordinateWithAllies(new System.Collections.Generic.List<ZObject> { defender });
            check(allied == defender.Waypoints.Any(wp => wp.mode == WpMode.Attack && wp.refId == enemy.Id),
                "Fort relief requires an alliance and works with either a human or a bot teammate");
        }
    }

    static void CheckTeamAdvance(string settings, string grid, Action<bool, string> check, bool defendTerritory)
    {
        var world = ZWorld.Create(settings, grid, 12);
        world.Map.skirmish = true;
        world.SetSkirmishAlliances(new[] { 0, 0, 1 });
        world.ConfigureSkirmishRules(new SkirmishRules(), Array.Empty<int>());
        var forts = world.Objects.Where(o => o.IsFort && o.Owner != Team.Green).ToArray();
        var flag = world.Flags.First(f => f.Owner == Team.Neutral && forts.All(o =>
            !ZObject.Within(f.CenterX, f.CenterY, o.CenterX, o.CenterY, 400)));
        var teammate = world.CreateRobotGroup("tough", flag.LocX, flag.LocY, Team.Red, 1);
        var reinforcement = world.CreateRobotGroup("tough", flag.LocX, flag.LocY, Team.Blue, 1);
        var enemy = world.CreateRobotGroup("grunt", flag.LocX, flag.LocY, Team.Green, 1);
        if (defendTerritory) world.AwardZone(flag, Team.Red);
        else teammate.AttackObject = enemy;
        var bot = new ZBot(world, Team.Blue, 12);
        bot.CoordinateWithAllies(new System.Collections.Generic.List<ZObject> { reinforcement });
        check(reinforcement.Waypoints.Any(wp => wp.mode == WpMode.Attack && wp.refId == enemy.Id),
            defendTerritory ? "A bot defends occupied allied territory" : "A bot reinforces an ally’s attack in neutral territory");
    }

    static void CheckTeamDiversion(string settings, string grid, Action<bool, string> check)
    {
        var world = ZWorld.Create(settings, grid, 12);
        world.Map.skirmish = true;
        world.SetSkirmishAlliances(new[] { 0, 0, 1 });
        world.ConfigureSkirmishRules(new SkirmishRules(), Array.Empty<int>());
        var forts = world.Objects.Where(o => o.IsFort && o.Owner != Team.Green).ToArray();
        var guards = world.UnitObjects.Where(o => o.Owner == Team.Green && o.CanAttack()).ToArray();
        var flags = world.Flags.Where(f => f.Owner == Team.Neutral && forts.All(o =>
            !ZObject.Within(f.CenterX, f.CenterY, o.CenterX, o.CenterY, 400))).ToArray();
        var pair = (from front in flags
                    from flank in flags
                    where !ZObject.Within(front.CenterX, front.CenterY, flank.CenterX, flank.CenterY, 480) &&
                        ZObject.Within(front.CenterX, front.CenterY, flank.CenterX, flank.CenterY, 620) &&
                        guards.All(o => !ZObject.Within(o.CenterX, o.CenterY, flank.CenterX, flank.CenterY, 240) &&
                            !ZObject.Within(o.CenterX, o.CenterY, front.CenterX, front.CenterY, 240))
                    select (front, flank)).First();
        world.AwardZone(pair.front, Team.Green);
        world.AwardZone(pair.flank, Team.Green);
        var ally = world.CreateRobotGroup("tough", pair.front.LocX, pair.front.LocY, Team.Red, 1);
        ally.Waypoints.Add(new ZWaypoint { mode = WpMode.Move, refId = pair.front.Id, x = pair.front.CenterX, y = pair.front.CenterY });
        var reinforcement = world.CreateRobotGroup("tough", pair.flank.LocX, pair.flank.LocY, Team.Blue, 1);
        var bot = new ZBot(world, Team.Blue, 12);
        bot.CoordinateWithAllies(new System.Collections.Generic.List<ZObject> { reinforcement });
        check(reinforcement.Waypoints.Any(wp => wp.mode == WpMode.Move && wp.refId == pair.flank.Id),
            "A teammate’s advance can trigger a diversion to an undefended flank of the same opponent");
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
                RadarLoss = RadarLossRule.FogAndZoom, FriendlyFire = true, PlanningSeconds = 10, FreeForAll = true };
            rules.OverrideFortTurrets = true;
            rules.FortTurrets[0] = "gatling";
            rules.BuildPercent[0] = 70;
            room.SetRules("host", rules);
            room.SetReady("host", room.Revision, true);
            room.SwapStartingSides("host");
            check(!room.CanLaunch && room.HostPosition == 1 && room.Bots.Single().Position == 0 &&
                room.Bots.Single().Alliance == 0 && room.Rules.BuildPercent[1] == 70,
                entry.Id + " swaps free-for-all sides and carries each participant's build-time advantage");
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
