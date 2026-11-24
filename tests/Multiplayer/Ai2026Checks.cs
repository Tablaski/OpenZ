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
        CheckSalvage(settings, check);
        CheckCommands(settings, grid, check);
        CheckProfiles(settings, grid, check);
        CheckRadar(settings, grid, check);
        CheckFogAttackOrders(settings, check);
        CheckRoom(root, settings, check);
        CheckTeamRelief(root, settings, check);
        bool roofDefense = ZWorld.FortDefenderDeviation;
        try
        {
            ZWorld.FortDefenderDeviation = false;
            CheckOpenFort(settings, ZBotDifficulty.Zod, reinforce: false, check: check);
            CheckOpenFort(settings, ZBotDifficulty.Skilled, reinforce: true, check: check);
            CheckBombardFort(settings, check);
            CheckEliminateArmy(settings, check);
        }
        finally { ZWorld.FortDefenderDeviation = roofDefense; }
    }

    static void CheckSalvage(string settings, Action<bool, string> check)
    {
        foreach (var level in new[] { ZBotDifficulty.Skilled, ZBotDifficulty.Zod })
        {
            var (world, target, enemy) = SalvageFixture(settings, "heavy", 130);
            var crew = world.CreateRobotGroup("grunt", 456, 758, Team.Blue, 1);
            world.Bots.Add(new ZBot(world, Team.Blue, 146, level));
            check(Watch(world, () => target.Owner == Team.Blue, 240),
                level + " rushes to board a Heavy before the opposing crew");

            (world, target, enemy) = SalvageFixture(settings, "crane", 130);
            crew = world.CreateRobotGroup("grunt", 456, 758, Team.Blue, 1);
            world.Bots.Add(new ZBot(world, Team.Blue, 146, level));
            check(!Watch(world, () => Ordered(crew, target, WpMode.Enter), 40),
                level + " does not issue a capture rush for an abandoned crane");
        }

        var (battle, hull, boarder) = SalvageFixture(settings, "heavy", 48);
        var tank = battle.CreateObject("vehicle", "heavy", 398, 762, Team.Blue);
        tank.AttackSpeed = .75;
        hull.Health = tank.Damage / 2;
        var rival = battle.CreateObject("vehicle", "heavy", 302, 762, Team.Red);
        battle.SendWaypoints(Team.Blue, tank, new[] { ZWaypoint.Move(rival.CenterX, rival.CenterY) });
        tank.Engage(rival);
        battle.Bots.Add(new ZBot(battle, Team.Blue, 146, ZBotDifficulty.Zod));
        check(Watch(battle, () => Ordered(tank, hull, WpMode.Attack), 30),
            "Zod can interrupt combat to deny a Heavy before the opposing crew boards");
        check(Watch(battle, () => hull.Destroyed, 100), "A denial attack destroys an unoccupied Heavy");

        (battle, hull, boarder) = SalvageFixture(settings, "howitzer", 160);
        tank = battle.CreateObject("vehicle", "heavy", 372, 762, Team.Blue);
        battle.Bots.Add(new ZBot(battle, Team.Blue, 146, ZBotDifficulty.Skilled));
        check(Watch(battle, () => Ordered(tank, hull, WpMode.Move), 40),
            "Skilled stations a Heavy near an abandoned howitzer when it can intercept the opposing crew");

        (battle, hull, boarder) = SalvageFixture(settings, "howitzer", 160);
        tank = battle.CreateObject("vehicle", "heavy", 372, 762, Team.Blue);
        tank.Health = 1;
        battle.CreateObject("cannon", "missile_cannon", 566, 804, Team.Red);
        battle.Bots.Add(new ZBot(battle, Team.Blue, 146, ZBotDifficulty.Zod));
        check(!Watch(battle, () => Ordered(tank, hull, WpMode.Move), 50),
            "Zod passes on a guard position that would immediately lose its own tank");

        (battle, hull, boarder) = SalvageFixture(settings, "heavy", 100);
        hull.Health = 1;
        battle.SetSkirmishAlliances(new[] { 1, 0, 0 });
        battle.ConfigureSkirmishRules(new SkirmishRules { FriendlyFire = true }, Array.Empty<int>());
        tank = battle.CreateObject("vehicle", "heavy", 398, 762, Team.Blue);
        var allyCrew = battle.CreateRobotGroup("grunt", 450, 758, Team.Green, 1);
        battle.SendWaypoints(Team.Green, allyCrew, new[] { new ZWaypoint
        {
            mode = WpMode.Enter, refId = hull.Id, x = hull.CenterX, y = hull.CenterY
        } });
        battle.Bots.Add(new ZBot(battle, Team.Blue, 146, ZBotDifficulty.Zod));
        check(!Watch(battle, () => Ordered(tank, hull, WpMode.Attack), 140) && hull.Owner == Team.Green,
            "Zod leaves an abandoned Heavy for an allied crew that can win the race");

        int Opportunities(ZBotDifficulty level, int count)
        {
            int attempts = 0;
            for (int seed = 1; seed <= count; seed++)
            {
                var (trial, empty, hostile) = SalvageFixture(settings, "heavy", 78);
                empty.Health = 1;
                var shooter = trial.CreateObject("vehicle", "heavy", 398, 762, Team.Blue);
                trial.Bots.Add(new ZBot(trial, Team.Blue, seed, level));
                if (Watch(trial, () => Ordered(shooter, empty, WpMode.Attack), 230)) attempts++;
            }
            return attempts;
        }
        check(Opportunities(ZBotDifficulty.Noob, 4) == 0, "Noob never plans a denial order");
        int alright = Opportunities(ZBotDifficulty.Alright, 24);
        check(alright > 0 && alright < 12, $"Alright only occasionally plans denial ({alright}/24 seeds)");
    }

    static (ZWorld World, ZObject Target, ZObject Enemy) SalvageFixture(string settings, string type, int enemyDistance)
    {
        var world = FortFixture(settings, out var enemyFort);
        world.StopBuilding(Team.Red, enemyFort);
        var target = world.CreateObject(type == "howitzer" ? "cannon" : "vehicle", type, 480, 760, Team.Neutral);
        // The leading robot is alive and can board. Its weapon is irrelevant to this fixture's race.
        var enemy = world.CreateRobotGroup("grunt", target.CenterX + enemyDistance, target.CenterY - 8, Team.Red, 1);
        return (world, target, enemy);
    }

    static bool Ordered(ZObject unit, ZObject target, WpMode mode) =>
        unit.Waypoints.Any(wp => wp.refId == target.Id && wp.mode == mode);

    static bool Watch(ZWorld world, Func<bool> condition, int ticks)
    {
        for (int tick = 0; tick < ticks && !condition(); tick++) world.Tick(ZConst.TICK);
        return condition();
    }

    static ZWorld Match(string settings, string grid, ZBotDifficulty level)
    {
        var world = ZWorld.Create(settings, grid, 45);
        world.Bots.Add(new ZBot(world, Team.Blue, 146, level));
        return world;
    }

    static void CheckOpenFort(string settings, ZBotDifficulty level, bool reinforce, Action<bool, string> check)
    {
        var world = FortFixture(settings, out var fort);
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

    static ZWorld FortFixture(string settings, out ZObject fort)
    {
        const int side = 64;
        string tiles = string.Join(",", new int[side * side]);
        string grid = $@"{{""file"":""fort-opportunity"",""width"":{side},""height"":{side},""planet"":""desert"",
            ""classes"":[{tiles}],""tiles"":[{tiles}],""zones"":[],""objects"":[
                {{""kind"":""building:fort_front"",""x"":24,""y"":8,""owner"":""red"",""health_percent"":100}},
                {{""kind"":""building:fort_back"",""x"":44,""y"":50,""owner"":""blue"",""health_percent"":100}}]}}";
        var world = ZWorld.Create(settings, grid, 45);
        fort = world.Objects.Single(o => o.IsFort && o.Owner == Team.Red);
        return world;
    }

    static void CheckBombardFort(string settings, Action<bool, string> check)
    {
        var world = FortFixture(settings, out var fort);
        fort.GetCreationMovePoint(out int x, out int y);
        var turret = world.CreateObject("cannon", "missile_cannon", x + 70, y + 48, Team.Red);
        turret.Health = turret.MaxHealth = 500000;
        fort.Health = 1000;
        var heavy = world.CreateObject("vehicle", "heavy", fort.LocX - 64, fort.CenterY - 32, Team.Blue);
        world.Bots.Add(new ZBot(world, Team.Blue, 146, ZBotDifficulty.Skilled));
        bool attacked = false;
        for (int i = 0; i < 1800 && !fort.Destroyed; i++)
        {
            world.Tick(ZConst.TICK);
            attacked |= heavy.Waypoints.Any(wp => wp.mode == WpMode.Attack && wp.refId == fort.Id);
        }
        check(attacked && fort.Destroyed && !world.Events.Any(e => e.type == ZEventType.FortEntered),
            "Skilled bombards a vulnerable fort with a guarded entrance");
    }

    static void CheckEliminateArmy(string settings, Action<bool, string> check)
    {
        var world = FortFixture(settings, out var fort);
        world.StopBuilding(Team.Red, fort);
        var jeep = world.CreateObject("vehicle", "jeep", 116, 778, Team.Red);
        jeep.Health = 1;
        var tank = world.CreateObject("vehicle", "light", 348, 778, Team.Blue);
        world.Bots.Add(new ZBot(world, Team.Blue, 146, ZBotDifficulty.Skilled));
        bool hunted = false;
        bool attackedFort = false;
        int lastFortHealth = fort.Health;
        for (int i = 0; i < 3000 && !world.GameOver; i++)
        {
            if (!jeep.Destroyed) lastFortHealth = fort.Health;
            world.Tick(ZConst.TICK);
            hunted |= tank.Waypoints.Any(wp => wp.mode == WpMode.Attack && wp.refId == jeep.Id);
            attackedFort |= world.UnitObjects.Any(o => o.Owner == Team.Blue &&
                o.Waypoints.Any(wp => wp.mode == WpMode.Attack && wp.refId == fort.Id));
        }
        check(hunted && jeep.Destroyed && world.GameOver && world.Winner == Team.Blue &&
            !attackedFort && lastFortHealth == fort.MaxHealth && !world.Events.Any(e => e.type == ZEventType.FortEntered),
            "Skilled wins by killing the last unit, causing the engine to destroy the undamaged fort");
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

    static void CheckFogAttackOrders(string settings, Action<bool, string> check)
    {
        const int side = 64;
        string tiles = string.Join(",", new int[side * side]);
        string grid = $@"{{""file"":""fog-orders"",""width"":{side},""height"":{side},""planet"":""desert"",
            ""classes"":[{tiles}],""tiles"":[{tiles}],""zones"":[],""objects"":[
                {{""kind"":""building:fort_front"",""x"":24,""y"":8,""owner"":""red"",""health_percent"":100}},
                {{""kind"":""building:fort_back"",""x"":44,""y"":50,""owner"":""blue"",""health_percent"":100}},
                {{""kind"":""building:radar"",""x"":40,""y"":45,""owner"":""blue"",""health_percent"":100}},
                {{""kind"":""map_item:rock"",""x"":12,""y"":24,""owner"":""neutral"",""health_percent"":100}}]}}";
        var world = ZWorld.Create(settings, grid, 12);
        world.ModernBotSoloRadar = RadarLossRule.Fog;
        var radar = world.Objects.Single(o => o.ProvidesRadarTo(Team.Blue));
        var attacker = world.CreateObject("vehicle", "jeep", 680, 630, Team.Blue);
        var mover = world.CreateObject("vehicle", "jeep", 750, 640, Team.Blue);
        var destination = ZWaypoint.Move(240, 420);
        var target = world.CreateObject("vehicle", "heavy", 250, 250, Team.Red);
        var lastKnown = (target.CenterX, target.CenterY);
        var waypoint = new ZWaypoint { mode = WpMode.Attack, refId = target.Id,
            x = lastKnown.CenterX, y = lastKnown.CenterY, playerGiven = true };
        world.Tick(ZConst.TICK);
        check(world.PlayerCanSeeOrderPoint(Team.Blue, lastKnown.CenterX, lastKnown.CenterY),
            "Working radar initially reveals the attack target");
        check(world.SendWaypoints(Team.Blue, attacker, new[] { waypoint }), "The attack is accepted while the radar works");
        check(world.SendWaypoints(Team.Blue, mover, new[] { destination }), "The move is accepted while the radar works");
        world.Tick(ZConst.TICK);
        foreach (var building in world.Objects.Where(o => o.ProvidesRadarTo(Team.Blue))) building.Health = 0;
        target.SetCords(target.LocX + 48, target.LocY);
        for (int i = 0; i < 7; i++) world.Tick(ZConst.TICK);
        bool hidden = world.HiddenFromOrders(Team.Blue, target);
        bool ghostOrder = world.TryFogAttack(Team.Blue, waypoint, target, out var moved);
        check(hidden && ghostOrder && moved == lastKnown,
            $"An attack in radar fog remembers the last visible position of a target that moves (hidden: {hidden}, ghost: {ghostOrder}, expected: {lastKnown}, actual: {moved})");
        var rock = world.Objects.First(o => o.Type == "rock" && !o.Destroyed &&
            !world.PlayerCanSeeOrderPoint(Team.Blue, o.LocX + 8, o.LocY + 40) &&
            world.Path.HasDestroyableBarrier(o.LocX >> 4, (o.LocY >> 4) + 2));
        int rockX = rock.LocX >> 4, rockY = (rock.LocY >> 4) + 2;
        rock.KillAt = world.Time;
        target.Health = 0;
        target.KillAt = world.Time;
        for (int i = 0; i < 7; i++) world.Tick(ZConst.TICK);
        check(world.Get(target.Id) == null && world.TryFogAttack(Team.Blue, waypoint, null, out var deleted) &&
            deleted == lastKnown && attacker.Waypoints.Any(wp => wp.mode == WpMode.Attack && wp.refId == target.Id),
            "An unseen deletion preserves the attack order and its remembered position");
        check(world.Get(rock.Id) == null && world.Path.TilePassableGrid(rockX, rockY, true, false),
            "A removed rock opens the actual terrain");
        check(mover.Waypoints.Count > 0 && mover.Waypoints[0].mode == WpMode.Move &&
            mover.Waypoints[0].x == destination.x && mover.Waypoints[0].y == destination.y,
            "A move keeps its original destination across radar loss and unseen rock removal");
        int row = rockY * 16 + 8, left = (rockX - 4) * 16 + 8, right = (rockX + 4) * 16 + 8;
        using (world.Path.UseKnownRocks(world.FogRockTiles(Team.Blue)))
            check(!world.Path.TilePassableGrid(rockX, rockY, true, false) &&
                world.Path.TilePassableGrid(rockX, rockY, true, true) &&
                !world.Path.DirectPathPossible(left, row, right, row, true, false),
                "Replanning retains a known rock while its removal is hidden");
        radar.Health = radar.MaxHealth;
        for (int i = 0; i < 7; i++) world.Tick(ZConst.TICK);
        check(!world.TryFogAttack(Team.Blue, waypoint, null, out _) &&
            !attacker.Waypoints.Any(wp => wp.mode == WpMode.Attack && wp.refId == target.Id),
            "The attack ends after radar vision confirms that the target has gone");
        using (world.Path.UseKnownRocks(world.FogRockTiles(Team.Blue)))
            check(world.Path.TilePassableGrid(rockX, rockY, true, false) &&
                world.Path.DirectPathPossible(left, row, right, row, true, false),
                "Replanning uses the opened terrain once the removal is visible");
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
