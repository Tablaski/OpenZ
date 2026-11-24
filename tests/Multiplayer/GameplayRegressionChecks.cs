using System;
using System.IO;
using System.Linq;
using OpenZ;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class GameplayRegressionChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string settings = File.ReadAllText(Path.Combine(root, "source/Assets/OpenZ/Resources/OpenZ/default_settings.txt"));
        CheckHeadStart(settings, check);
        CheckHowitzer(settings, check);
        CheckCrane(settings, check);
        CheckApc(settings, check);
        CheckBridgeAttack(settings, check);
    }

    static void CheckHeadStart(string settings, Action<bool, string> check)
    {
        var original = new[] { "light", "light", "jeep", "jeep", "medium" };
        check(ZWorld.SelectHeadStart(original, _ => true).SequenceEqual(original), "OpenZ retains the original Head Start roster");
        var rules = new DuelRules();
        rules.SetUnitPreset(DuelUnitPreset.Z1996);
        var world = Battlefield(settings);
        world.ConfigureSoloUnits(rules.AllowedUnits, true, true);
        var roster = ZWorld.SelectHeadStart(original, world.DuelAllowsUnit);
        check(roster.SequenceEqual(new[] { "light", "light", "jeep", "jeep", "mtank" }), "Z 1996 replaces the mech with a medium tank");
        check(world.AddHeadStart(Team.Red, roster).Select(unit => unit.Type).SequenceEqual(roster), "The planned Head Start roster spawns at the fort");
        check(ZWorld.SelectHeadStart(original, type => type == "tough").SequenceEqual(Enumerable.Repeat("tough", 5)),
            "A robot-only roster still receives five reinforcements");
    }

    static void CheckHowitzer(string settings, Action<bool, string> check)
    {
        var world = Battlefield(settings);
        var gun = world.CreateObject("cannon", "howitzer", 480, 480, Team.Red);
        var target = world.CreateObject("vehicle", "heavy", 616, 480, Team.Blue);
        target.Damage = 0;
        target.Health = target.MaxHealth = 1000000;
        Order(world, gun, target, WpMode.Attack);
        check(Until(world, () => Fired(world, gun, target), 1), "The field howitzer fires its first round");
        double ready = world.Time + gun.AttackSpeed;
        world.Events.Clear();
        for (int cycle = 0; cycle < 3; cycle++)
        {
            check(world.Eject(Team.Red, gun), "The field howitzer releases its crew");
            var crew = world.Get(world.Events.Last(e => e.type == ZEventType.Ejected && e.objId == gun.Id).otherId);
            Order(world, crew, gun, WpMode.Enter);
            check(Until(world, () => gun.Owner == Team.Red, 0.5), "The crew re-enters the same howitzer");
            Order(world, gun, target, WpMode.Attack);
            world.Tick(ZConst.TICK);
        }
        while (world.Time + ZConst.TICK < ready - 1e-8) world.Tick(ZConst.TICK);
        check(!Fired(world, gun, target), "Repeated crew changes leave the full howitzer reload in place");
        check(Until(world, () => Fired(world, gun, target), 0.2), "The howitzer fires again as soon as its reload completes");
    }

    static void CheckCrane(string settings, Action<bool, string> check)
    {
        var world = Battlefield(settings);
        var crane = world.CreateObject("vehicle", "crane", 480, 480, Team.Red);
        bool exit = ZWorld.VehicleExitDeviation;
        try
        {
            ZWorld.VehicleExitDeviation = true;
            check(crane.CanExitDriver(out _) && world.Eject(Team.Red, crane), "The crane can release its driver on open terrain");
            var crew = world.Get(world.Events.Last(e => e.type == ZEventType.Ejected && e.objId == crane.Id).otherId);
            check(crane.Owner == Team.Neutral && crane.Drivers.Count == 0 && crane.DriverOutLocked && crew.Owner == Team.Red,
                "The crane stops, becomes neutral and leaves its driver beside it");
        }
        finally { ZWorld.VehicleExitDeviation = exit; }
    }

    static void CheckApc(string settings, Action<bool, string> check)
    {
        foreach (var (crewType, targetKind, targetType) in new[] { ("tough", "cannon", "gun"), ("pyro", "vehicle", "light") })
        {
            var world = Battlefield(settings);
            var apc = world.CreateObject("vehicle", "apc", 480, 480, Team.Red);
            apc.DriverType = crewType;
            apc.ResetDamageInfo();
            var target = world.CreateObject(targetKind, targetType, 544, 480, Team.Neutral);
            var wp = ZWaypoint.Move(target.CenterX, target.CenterY);
            check(ZWorld.EmptyClickWaypoint(apc, target, false, ref wp) && wp.mode == WpMode.Attack,
                $"An APC using {crewType} weapons gets an attack order on a vacant {targetType}");
            world.SendWaypoints(Team.Red, apc, new[] { wp });
            check(Until(world, () => Fired(world, apc, target), 3), $"The APC fires its {crewType} weapons at the vacant {targetType}");
        }
    }

    static void CheckBridgeAttack(string settings, Action<bool, string> check)
    {
        foreach (bool northbound in new[] { false, true })
        {
            var world = Battlefield(settings, true);
            var tank = world.CreateObject("vehicle", "heavy", 368, northbound ? 672 : 312, Team.Red);
            var target = world.CreateObject("vehicle", "heavy", 368, northbound ? 312 : 672, Team.Blue);
            target.Damage = 0;
            Order(world, tank, target, WpMode.Attack);
            var start = tank.OrderSnapshot();
            bool wasTracing = ZPathing.Trace;
            bool fired;
            try
            {
                ZPathing.Trace = true;
                fired = Until(world, () => Fired(world, tank, target), 15);
            }
            finally { ZPathing.Trace = wasTracing; }
            check(fired && (northbound ? tank.CenterY < 520 : tank.CenterY > 520),
                $"A player tank crosses the bridge and fires, northbound={northbound}, position={tank.CenterX},{tank.CenterY}, " +
                $"point={tank.PathTargetX},{tank.PathTargetY}, destination={tank.PathFinalPoint}, plans={tank.ApproachPlans}, " +
                $"{tank.OrderStandWhy(start, out _, out _)}, {world.Pathing.BridgeQueueStats()}\n" +
                (tank.PS.Log == null ? "" : string.Join("\n", tank.PS.Log)));
        }
    }

    static void Order(ZWorld world, ZObject unit, ZObject target, WpMode mode) =>
        world.SendWaypoints(unit.Owner, unit, new[] { new ZWaypoint { mode = mode, refId = target.Id, x = target.CenterX, y = target.CenterY } });

    static bool Fired(ZWorld world, ZObject unit, ZObject target) => world.Events.Any(e => e.objId == unit.Id && e.otherId == target.Id &&
        (e.type == ZEventType.MissileFired || e.type == ZEventType.Hitscan));

    static bool Until(ZWorld world, Func<bool> condition, double seconds)
    {
        for (int tick = 0; tick < seconds / ZConst.TICK && !condition(); tick++) world.Tick(ZConst.TICK);
        return condition();
    }

    static ZWorld Battlefield(string settings, bool withBridge = false)
    {
        const int size = 64;
        var tiles = new int[size * size];
        var classes = new int[tiles.Length];
        if (withBridge)
        {
            Array.Fill(classes, (int)ZClass.Water, 30 * size, 5 * size);
            foreach (int row in Enumerable.Range(30, 5)) Array.Fill(classes, (int)ZClass.Road, row * size + 31, 2);
        }
        string grid = $@"{{""file"":""combat-regressions"",""width"":{size},""height"":{size},""planet"":""desert"",
            ""classes"":[{string.Join(",", classes)}],""tiles"":[{string.Join(",", tiles)}],""zones"":[],""objects"":[
                {{""kind"":""building:fort_front"",""x"":16,""y"":8,""owner"":""red"",""health_percent"":100}},
                {{""kind"":""building:fort_back"",""x"":44,""y"":50,""owner"":""blue"",""health_percent"":100}}
                {(withBridge ? ", {\"kind\":\"building:bridge_vert\",\"x\":30,\"y\":30,\"owner\":\"neutral\",\"health_percent\":100}" : "")}]}}";
        return ZWorld.Create(settings, grid, 123);
    }
}
