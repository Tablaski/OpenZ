using System;
using System.IO;
using System.Linq;
using OpenZ;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class BehaviorChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        check(ZBehaviorSchema.All.Length == (int)ZBehaviorKey.Count && ZBehaviorSchema.All.Select((d, i) => (int)d.Key == i).All(v => v), "Behavior schema order and complete UI coverage");
        foreach (var family in new[] { ZBehaviorFamily.Tanks, ZBehaviorFamily.Robots })
        {
            var p = new ZBehaviorProfile { family = family, name = "Test | preset\nUnicode é", master = 0 };
            string type = family == ZBehaviorFamily.Tanks ? "light" : "grunt";
            check(p.Resolve(type) == null, family + ": zero master uses native code path");
            p.master = 100;
            foreach (var d in ZBehaviorSchema.All) check(p.Get(type, d.Key) == ZBehaviorSchema.Endpoint(d.Key, family, type, true), family + ": Zod endpoint " + d.Key);
            p.Set("", ZBehaviorKey.Reaction, .25); p.Set(type, ZBehaviorKey.Reaction, .75); p.master = 30;
            check(p.Get(type, ZBehaviorKey.Reaction) == .75, family + ": per-type override wins over family and master");
            p.Reset(type, ZBehaviorKey.Reaction);
            check(p.Get(type, ZBehaviorKey.Reaction) == .25, family + ": removing per-type override inherits family");
            p.FreezeReferences(); string encoded = p.Encode(); var restored = ZBehaviorProfile.Decode(encoded);
            check(restored.Encode() == encoded && restored.name == p.name, family + ": named preset/reference round-trip");
            restored.master = 80;
            check(restored.Get(type, ZBehaviorKey.Reaction) == .25 && restored.overrides.Count == 1, family + ": frozen references retain editable inheritance");
            reject(() => p.Set("", ZBehaviorKey.Reaction, double.NaN), family + ": NaN rejected");
            reject(() => p.Set("", ZBehaviorKey.Reaction, double.PositiveInfinity), family + ": infinity rejected");
            reject(() => p.Set("unknown-unit", ZBehaviorKey.Reaction, 1), family + ": foreign type rejected");
            bool malformed = false;
            try { ZBehaviorProfile.Decode("invalid"); } catch (FormatException) { malformed = true; }
            check(malformed, family + ": invalid preset rejected");
        }
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../source/Assets/OpenZ/Resources/OpenZ"));
        string settings = File.ReadAllText(Path.Combine(root, "default_settings.txt"));
        string map = File.ReadAllText(Path.Combine(root, "Maps/L02_grid.json"));
        var world = ZWorld.Create(settings, map, 231);
        var tank = world.CreateObject("vehicle", "light", 240, 240, Team.Red);
        var enemy = world.CreateObject("vehicle", "heavy", 600, 240, Team.Blue);
        var attack = new ZWaypoint { mode = WpMode.Attack, refId = enemy.Id, playerGiven = true, x = enemy.CenterX, y = enemy.CenterY };
        tank.Waypoints.Add(attack); tank.Engage(enemy);
        var tanks = new ZBehaviorProfile { family = ZBehaviorFamily.Tanks, master = 100 };
        world.SetBehavior(tanks);
        check(tank.DodgeMissile(tank.CenterX, tank.CenterY, 1), "Zod tank interrupts attack to dodge");
        check(tank.Waypoints.Count == 2 && tank.Waypoints[0].mode == WpMode.Dodge && tank.Waypoints[1].refId == enemy.Id, "Dodge preserves attack order");
        tanks.Set("", ZBehaviorKey.Enabled, 0); world.SetBehavior(tanks);
        check(tank.Waypoints.Count == 1 && tank.Waypoints[0].mode == WpMode.Attack, "Disabling dodge resumes attack without deleting it");
        tanks.Set("", ZBehaviorKey.Enabled, 1); tanks.Set("", ZBehaviorKey.AttackOrder, 0); world.SetBehavior(tanks);
        check(!tank.DodgeMissile(tank.CenterX, tank.CenterY, 1) && tank.BehaviorDodgeReason.Contains("Order"), "Custom attack restriction applied to existing unit");
        tanks.Set("", ZBehaviorKey.AttackOrder, 1); tanks.Set("", ZBehaviorKey.Reaction, 2); world.SetBehavior(tanks);
        check(!tank.DodgeMissile(250, 250, 1) && tank.BehaviorDodgeReason == "Reaction too late", "Reaction delay cannot dodge after impact");
        tanks.Set("", ZBehaviorKey.Reaction, 0); tanks.Set("", ZBehaviorKey.ThreatRocket, 0); world.SetBehavior(tanks);
        check(!tank.DodgeMissile(250, 250, 1, ZThreatKind.Rocket), "Rocket filter is active");
        check(tank.DodgeMissile(250, 250, 1, ZThreatKind.Shell), "Shell filter is independent");
        var robots = new ZBehaviorProfile { family = ZBehaviorFamily.Robots, master = 100 };
        world.SetBehavior(robots);
        var robot = world.CreateObject("robot", "grunt", 400, 400, Team.Red); robot.Engage(enemy);
        check(!robot.DodgeMissile(400, 400, 1), "Zod engaged robot retains historic no-dodge rule");
        robots.Set("", ZBehaviorKey.Engaged, 1); world.SetBehavior(robots);
        check(robot.DodgeMissile(400, 400, 1), "Robot engagement rule can be overridden independently");
        check(tank.Behavior.On(ZBehaviorKey.ThreatShell) && !tank.Behavior.On(ZBehaviorKey.ThreatRocket), "Robot edits do not mutate tank profile");
        double stamina = tank.Stamina;
        tanks.Set("", ZBehaviorKey.BoostSpeed, 3); world.SetBehavior(tanks);
        check(tank.Stamina == stamina, "Live profile changes never refill endurance");
        tanks.Set("heavy", ZBehaviorKey.Enabled, 0); world.SetBehavior(tanks);
        var future = world.CreateObject("vehicle", "heavy", 900, 900, Team.Red);
        check(!future.Behavior.On(ZBehaviorKey.Enabled) && tank.Behavior.On(ZBehaviorKey.Enabled), "Future units get per-type exceptions");
        var replica = ZWorld.Create(settings, map, 231);
        replica.ConfigureDuel(new DuelRules { Map = "L02" }, true);
        DuelReplica.Apply(replica, DuelReplica.Capture(world));
        check(replica.BehaviorProfile(ZBehaviorFamily.Tanks).Encode() == world.BehaviorProfile(ZBehaviorFamily.Tanks).Encode() &&
            replica.BehaviorProfile(ZBehaviorFamily.Robots).Encode() == world.BehaviorProfile(ZBehaviorFamily.Robots).Encode(), "Host profiles replicate to guest for both families");
        var original = ZWorld.Create(settings, map, 614);
        var native = ZWorld.Create(settings, map, 614);
        native.SetBehavior(new ZBehaviorProfile { family = ZBehaviorFamily.Tanks }); native.SetBehavior(new ZBehaviorProfile { family = ZBehaviorFamily.Robots });
        for (int i = 0; i < 90; i++) { original.Tick(ZConst.TICK); native.Tick(ZConst.TICK); }
        check(original.Objects.Count == native.Objects.Count && original.Objects.Zip(native.Objects, (a, b) => a.LocX == b.LocX && a.LocY == b.LocY && a.Health == b.Health && a.Stamina == b.Stamina).All(v => v), "Native profiles preserve a real-map simulation over 90 ticks");
        var motion = ZWorld.Create(settings, map, 719);
        if (motion.Pathing != null) motion.Pathing.Enabled = false;
        int openX = -1, openY = -1;
        for (int y = 128; y < motion.Map.height * 16 - 160 && openX < 0; y += 32)
            for (int x = 128; x < motion.Map.width * 16 - 160; x += 32)
                if (!motion.Path.WithinImpassable(x, y, 128, 128, out _, out _, false)) { openX = x; openY = y; break; }
        check(openX >= 0, "Physical movement fixture has a clear lane");
        var moving = motion.CreateObject("vehicle", "light", openX + 16, openY + 16, Team.Red);
        var physical = new ZBehaviorProfile { family = ZBehaviorFamily.Tanks, master = 1 };
        physical.Set("", ZBehaviorKey.Physical, 1); physical.Set("", ZBehaviorKey.Acceleration, 2);
        physical.Set("", ZBehaviorKey.TurnRate, 0); physical.Set("", ZBehaviorKey.Boost, 0);
        motion.SetBehavior(physical);
        int initialX = moving.LocX;
        moving.Waypoints.Add(new ZWaypoint { mode = WpMode.Dodge, x = moving.CenterX + 72, y = moving.CenterY });
        moving.ProcessServer(.1);
        check(moving.BehaviorActualSpeed > 0 && moving.BehaviorActualSpeed < moving.RealMoveSpeed / 2,
            "Acceleration limits the simulation velocity, not just the model");
        check(moving.LocX - initialX < moving.RealMoveSpeed * .1, "Acceleration limits actual displacement");
        for (int i = 2; i < 150; i++) moving.ProcessServer(i * .1);
        check(moving.LocX > initialX + 20 && moving.LocX <= initialX + 73, "Physical controller approaches the real target without overshoot");
    }
}
