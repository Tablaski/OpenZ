using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenZ;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class Ai2026Checks
{
    internal static void Run(Action<bool, string> check)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string settings = File.ReadAllText(Path.Combine(root, "source/Assets/OpenZ/Resources/OpenZ/default_settings.txt"));
        string grid = File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps/SM07_grid.json"));
        foreach (var level in ZAiDifficulty.Modern)
        {
            var first = Match(settings, grid, level);
            var second = Match(settings, grid, level);
            for (int tick = 0; tick < 1500; tick++)
            {
                first.Tick(ZConst.TICK);
                second.Tick(ZConst.TICK);
            }
            var actual = first.Bots[0].ModernController;
            var repeat = second.Bots[0].ModernController;
            check(actual != null && actual.Actions > 0, level + " has issued a real action");
            check(actual.Actions == repeat.Actions && actual.FirstActionAt == repeat.FirstActionAt &&
                actual.StrategicRatio == repeat.StrategicRatio &&
                first.UnitObjects.Select(o => (o.Id, o.LocX, o.LocY, o.Health, o.Owner))
                    .SequenceEqual(second.UnitObjects.Select(o => (o.Id, o.LocX, o.LocY, o.Health, o.Owner))),
                level + " reproduces its decisions at a fixed seed");
            if (level == ZBotDifficulty.Noob)
                check(actual.FirstActionAt >= 5 && actual.FirstActionAt < 10.05, "Noob starts after five to ten seconds");
            if (level == ZBotDifficulty.Alright)
                check(actual.FirstActionAt >= 2 && actual.FirstActionAt < 4.05, "Alright starts after two to four seconds");
        }
        CheckCommands(settings, grid, check);
        CheckProfiles(settings, grid, check);
        CheckRadar(settings, grid, check);
        CheckRoom(root, settings, check);
        CheckTeamRelief(root, settings, check);
        bool roofDefense = ZWorld.FortDefenderDeviation;
        try
        {
            ZWorld.FortDefenderDeviation = false;
            CheckOpenFort(settings, ZBotDifficulty.Zod, reinforce: false, check: check);
            CheckOpenFort(settings, ZBotDifficulty.Skilled, reinforce: true, check: check);
        }
        finally { ZWorld.FortDefenderDeviation = roofDefense; }
    }

    static ZWorld Match(string settings, string grid, ZBotDifficulty level)
    {
        var world = ZWorld.Create(settings, grid, 45);
        world.Bots.Add(new ZBot(world, Team.Blue, 146, level));
        return world;
    }

    static void CheckOpenFort(string settings, ZBotDifficulty level, bool reinforce, Action<bool, string> check)
    {
        const int side = 64;
        string tiles = string.Join(",", new int[side * side]);
        string grid = $@"{{""file"":""fort-opportunity"",""width"":{side},""height"":{side},""planet"":""desert"",
            ""classes"":[{tiles}],""tiles"":[{tiles}],""zones"":[],""objects"":[
                {{""kind"":""building:fort_front"",""x"":24,""y"":8,""owner"":""red"",""health_percent"":100}},
                {{""kind"":""building:fort_back"",""x"":24,""y"":46,""owner"":""blue"",""health_percent"":100}}]}}";
        var world = ZWorld.Create(settings, grid, 45);
        var fort = world.Objects.Single(o => o.IsFort && o.Owner == Team.Red);
        fort.GetCreationMovePoint(out int x, out int y);
        var turret = world.CreateObject("cannon", "gatling", x + 90, y - 24, Team.Red);
        turret.Health = turret.MaxHealth = 500000;
        // Keep the opponent in the match after the turret falls, away from the entrance.
        var reserve = world.CreateObject("vehicle", "jeep", 48, 64, Team.Red);
        var heavy = world.CreateObject("vehicle", "heavy", x - 16, y + 80, Team.Blue);
        if (reinforce) world.CreateRobotGroup("sniper", x - 56, y + 64, Team.Blue, 1);
        world.Bots.Add(new ZBot(world, Team.Blue, 146, level));
        int Entries() => world.UnitObjects.Count(o => o.Owner == Team.Blue && !o.IsMinion &&
            o.Waypoints.Any(wp => wp.mode == WpMode.EnterFort && wp.refId == fort.Id));
        for (int i = 0; i < 220; i++) world.Tick(ZConst.TICK);
        check(Entries() == 0, level + " does not take the exposed-fort shortcut while a turret guards the entrance");
        turret.SetHealth(0);
        world.SendWaypoints(Team.Blue, heavy, new[] { new ZWaypoint
        {
            mode = WpMode.Attack, refId = reserve.Id, x = reserve.CenterX, y = reserve.CenterY
        } });
        int entrants = 0;
        for (int i = 0; i < 100; i++)
        {
            world.Tick(ZConst.TICK);
            entrants = Math.Max(entrants, Entries());
        }
        check(entrants == 1 && (reinforce || heavy.Waypoints.Any(wp => wp.mode == WpMode.EnterFort)),
            level + " commits one nearby entrant immediately when the last turret falls" +
            $" (entrants {entrants}, heavy HP {heavy.Health}, position {heavy.CenterX},{heavy.CenterY}, gate {x},{y}," +
            $" turret HP {turret.Health}, fort HP {fort.Health}, orders {string.Join(",", heavy.Waypoints.Select(wp => wp.mode))})");
        for (int i = 0; i < 5000 && !fort.Destroyed; i++) world.Tick(ZConst.TICK);
        check(fort.Destroyed && world.Events.Any(e => e.type == ZEventType.FortEntered && e.otherId == fort.Id),
            level + " follows the entry order through to defeating the fort");
    }

    static void CheckCommands(string settings, string grid, Action<bool, string> check)
    {
        var world = ZWorld.Create(settings, grid, 12);
        var order = new ZAiOrders(world, Team.Blue, ZAiTuning.For(ZBotDifficulty.Zod));
        var planned = new HashSet<int>();
        var fort = world.Objects.First(o => o.IsFort && o.Owner == Team.Blue);
        var blue = world.CreateObject("vehicle", "light", fort.LocX + 16, fort.LocY, Team.Blue);
        var second = world.CreateObject("vehicle", "jeep", fort.LocX + 32, fort.LocY, Team.Blue);
        var red = world.CreateObject("vehicle", "light", fort.LocX - 16, fort.LocY, Team.Red);
        var wp = ZWaypoint.Move(fort.CenterX + 110, fort.CenterY + 50);
        check(!order.Send(red, wp, planned) && order.Actions == 0, "Rejected enemy orders do not count");
        check(order.Send(new List<ZObject> { blue, second }, wp, planned) && order.Actions == 1 && planned.Count == 2,
            "A group order counts as one successful action");
        planned.Clear();
        check(!order.Send(blue, wp, planned) && order.Actions == 1, "Repeated orders do not count");
        for (int i = 0; i < 20; i++)
        {
            order.Send(blue, ZWaypoint.Move(wp.x + 40 * (i + 1), wp.y), planned);
            planned.Clear();
        }
        check(order.Actions == ZAiTuning.For(ZBotDifficulty.Zod).Burst, "Burst is bounded independently of the minute budget");

        var sortieOrder = new ZAiOrders(world, Team.Blue, ZAiTuning.For(ZBotDifficulty.Skilled));
        var cannon = world.CreateObject("cannon", "gatling", fort.LocX + 130, fort.LocY, Team.Blue);
        var crew = sortieOrder.Eject(cannon);
        check(crew != null && crew.IsRobot && crew.Owner == Team.Blue && cannon.CanBeEntered() &&
            sortieOrder.Actions == 1, "A cannon sortie uses the existing crew command and counts once");
    }

    static void CheckProfiles(string settings, string grid, Action<bool, string> check)
    {
        var world = ZWorld.Create(settings, grid, 12);
        var light = world.CreateObject("vehicle", "light", 220, 220, Team.Blue);
        var medium = world.CreateObject("vehicle", "medium", 380, 220, Team.Red);
        check(ZAiCombat.Avoidance(light, medium) == 0, "Native combat does not assume a dodging bonus");
        var profile = ZBehaviorProfile.Classic(ZBehaviorFamily.Tanks);
        world.SetBehavior(profile);
        double classic = ZAiCombat.Avoidance(light, medium);
        profile.Set("light", ZBehaviorKey.Chance, 0);
        world.SetBehavior(profile);
        check(classic > 0 && ZAiCombat.Avoidance(light, medium) == 0, "AI reads the active tank profile and its per-type override");
    }

    static void CheckRoom(string root, string settings, Action<bool, string> check)
    {
        var entry = OzAiMapCatalog.ThreePlayerSkirmish;
        var room = new MultiplayerRoom("host", "Host", entry.Id, allowSoloConversions: true);
        room.SetBot("host", 1, 1, ZBotDifficulty.Zod);
        room.SetBot("host", 2, 2, ZBotDifficulty.Easy);
        room.SetReady("host", room.Revision, true);
        room.Launch("host");
        room.MarkLoaded("host", room.Revision);
        string grid = File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps", entry.Id + "_grid.json"));
        var world = ZWorld.Create(settings, grid, 12);
        room.ConfigureWorld(world, 12);
        for (int tick = 0; tick < 400; tick++) world.Tick(ZConst.TICK);
        check(world.Bots.Single(b => b.Difficulty == ZBotDifficulty.Zod).ModernController?.Actions > 0 &&
            world.Bots.Single(b => b.Difficulty == ZBotDifficulty.Easy).ModernController == null,
            "A normal room launches modern and legacy opponents together");
    }

    static void CheckRadar(string settings, string grid, Action<bool, string> check)
    {
        var world = ZWorld.Create(settings, grid, 12);
        foreach (var radar in world.Objects.Where(o => o.ProvidesRadarTo(Team.Blue))) radar.Health = 0;
        var fort = world.Objects.First(o => o.IsFort && o.Owner == Team.Red);
        var sight = new ZAiSight(world, Team.Blue);
        world.ModernBotSoloRadar = RadarLossRule.Zoom;
        sight.Observe();
        check(sight.ZoomLimited && sight.CanSee(fort.CenterX, fort.CenterY),
            "Zoom-only radar loss restricts attention and leaves information available");
        world.ModernBotSoloRadar = RadarLossRule.FogAndZoom;
        sight.Observe();
        var friendlyUnit = world.Objects.First(o => o.IsUnit && o.Owner == Team.Blue);
        check(sight.ZoomLimited && !sight.CanSee(fort.CenterX, fort.CenterY) &&
            sight.CanSee(friendlyUnit.CenterX, friendlyUnit.CenterY),
            "Fog and zoom apply both effects without hiding the bot's own sector");
        world.CreateObject("building", "radar", 160, 160, Team.Blue);
        sight.Observe();
        check(!sight.ZoomLimited && sight.CanSee(fort.CenterX, fort.CenterY), "A working radar restores the picture");
    }

    static void CheckTeamRelief(string root, string settings, Action<bool, string> check)
    {
        string id = OzAiMapCatalog.ThreePlayerSkirmish.Id;
        string grid = File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps", id + "_grid.json"));
        var world = ZWorld.Create(settings, grid, 12);
        world.SetSkirmishAlliances(new[] { 0, 0, 1 });
        world.ConfigureSkirmishRules(new SkirmishRules { AllForOne = true }, Array.Empty<int>());
        var forts = world.Objects.Where(o => o.IsFort && world.AreAllied(Team.Red, o.Owner)).ToList();
        var home = forts.Single(o => o.Owner == Team.Red);
        var ally = forts.Single(o => o.Owner == Team.Blue);
        var enemy = world.CreateObject("vehicle", "heavy", ally.LocX + 50, ally.LocY, Team.Green);
        var observed = new List<ZObject> { enemy };
        var module = new ZAiTeamPlay(world, Team.Red);
        var support = module.Relief(forts, observed, ZAiTuning.For(ZBotDifficulty.Zod), (_, _, _) => 0);
        check(support.Fort == ally && support.Enemy == enemy, "TeamPlay finds a visible threat to an allied fort");
        check(module.Relief(forts, new List<ZObject>(), ZAiTuning.For(ZBotDifficulty.Zod), (_, _, _) => 0).Enemy == null,
            "TeamPlay cannot act on an unobserved threat");
        var reserve = world.CreateObject("vehicle", "heavy", home.LocX + 20, home.LocY, Team.Red);
        check(!module.CanRelease(reserve, home, ally, true), "TeamPlay keeps a heavy reserve at a threatened home fort");
    }
}
