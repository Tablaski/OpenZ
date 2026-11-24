using System;
using OpenZ;
using OpenZ.Sim;

static class DodgeCircleChecks
{
    public static void Run(string settings, string map, int x, int y, Action<bool, string> check)
    {
        foreach (var family in new[] { ZBehaviorFamily.Tanks, ZBehaviorFamily.Robots })
        {
            var classic = ZBehaviorProfile.Classic(family);
            string type = family == ZBehaviorFamily.Tanks ? "light" : "grunt";
            double burst = family == ZBehaviorFamily.Tanks ? 1.8 : 5;
            check(classic.Get(type, ZBehaviorKey.BoostSpeed) == burst && classic.Get(type, ZBehaviorKey.MaxDistance) == 6.25 &&
                classic.Get(type, ZBehaviorKey.BoostMaxDistance) == 6.25, "Published Z Classic bounds the burst to 1.25 common blast radii: " + family);
            var world = ZWorld.Create(settings, map, 173); world.Pathing.Enabled = false;
            classic.Set("", ZBehaviorKey.BoostChance, 100); classic.Set("", ZBehaviorKey.Endurance, 0);
            classic.Set("", ZBehaviorKey.MinDistance, 6.25); world.SetBehavior(classic);
            var unit = world.CreateObject(family == ZBehaviorFamily.Tanks ? "vehicle" : "robot", type, x + 32, y + 32, Team.Red);
            unit.Waypoints.Add(new ZWaypoint { mode = WpMode.Move, x = unit.CenterX + 160, y = unit.CenterY });
            check(unit.DodgeMissile(unit.CenterX, unit.CenterY, 10), "Z Classic defensive step starts: " + family);
            for (int tick = 1; tick <= 500; tick++)
            {
                world.Time = tick * .01; unit.ProcessServer(world.Time);
                if (tick < 40 && unit.BehaviorDodging) unit.DodgeMissile(unit.CenterX, unit.CenterY, 10);
            }
            check(unit.BehaviorBoostDistance > 0 && unit.BehaviorBoostDistance <= 6.250001 && unit.BehaviorDodgeDistance <= 6.250001 && !unit.BehaviorDodging,
                "Z Classic stops its burst and resumes the interrupted order without replenishing distance on replans: " + family);
        }
        CheckTrackedClassic(settings, map, x, y, check);
        var p = new ZBehaviorProfile { family = ZBehaviorFamily.Tanks, master = 100 };
        var bins = new int[16];
        for (int k = 0; k < 1000; k++)
        {
            double angle = 2 * 3.14159 * (1000.0 / (k == 0 ? 1000 : k));
            bins[(int)Math.Round(angle / (Math.PI / 8)) % 16]++;
        }
        for (int i = 0; i < 16; i++)
            check(p.Get("light", (ZBehaviorKey)((int)ZBehaviorKey.DodgeWeight0 + i)) == bins[i], "Zod reference bins preserve every native angle draw: " + i);
        p.Set("", ZBehaviorKey.Direction, 3);
        for (int i = 0; i < 16; i++) p.Set("", (ZBehaviorKey)((int)ZBehaviorKey.DodgeWeight0 + i), 0);
        check(ZObject.BehaviorWeightedDirection(p.Resolve("light"), .5) == -1, "All zero direction weights refuse dodging");
        for (int i = 0; i < 16; i++)
        {
            var key = (ZBehaviorKey)((int)ZBehaviorKey.DodgeWeight0 + i);
            p.Set("", key, 1);
            check(ZObject.BehaviorWeightedDirection(p.Resolve("light"), 0) == i && ZObject.BehaviorWeightedDirection(p.Resolve("light"), .99999) == i, "Single enabled direction is certain: " + i);
            p.Set("", key, 0);
        }
        p.Set("", ZBehaviorKey.DodgeWeight0, 1); p.Set("", ZBehaviorKey.DodgeWeight2, 3);
        check(ZObject.BehaviorWeightedDirection(p.Resolve("light"), .24999) == 0 && ZObject.BehaviorWeightedDirection(p.Resolve("light"), .25) == 2, "Relative weights produce exact 25/75 probability boundary");
        p.Set("", ZBehaviorKey.DodgeWeight0, 1e308); p.Set("", ZBehaviorKey.DodgeWeight2, 1e308);
        check(ZObject.BehaviorWeightedDirection(p.Resolve("light"), .75) == 2 && ZObject.BehaviorDirectionProbability(p.Resolve("light"), 0) == .5, "Large finite weights normalize without overflow");
        p.Set("", ZBehaviorKey.Forward, 0);
        check(ZObject.BehaviorWeightedDirection(p.Resolve("light"), 0) == 2, "Disabled direction categories are excluded before normalization");
        p.Set("", ZBehaviorKey.Forward, 1); p.Set("", ZBehaviorKey.DodgeWeight0, 1); p.Set("", ZBehaviorKey.DodgeWeight2, 0);
        check(ZBehaviorProfile.Decode(p.Encode()).Encode() == p.Encode(), "Circle settings round-trip through host profile format");
        var angles = ZWorld.Create(settings, map, 399); angles.Pathing.Enabled = false;
        for (int i = 0; i < 16; i++)
        {
            var profile = p.Clone(); profile.Set("", ZBehaviorKey.DodgeWeight0, 0);
            profile.Set("", (ZBehaviorKey)((int)ZBehaviorKey.DodgeWeight0 + i), 1);
            profile.Set("", ZBehaviorKey.MinDistance, .8); profile.Set("", ZBehaviorKey.MaxDistance, .8);
            angles.SetBehavior(profile);
            var unit = angles.CreateObject("vehicle", "light", x + 32, y + 32, Team.Red);
            unit.BehaviorHeadingSet = true; unit.BehaviorHeading = .37;
            check(unit.DodgeMissile(unit.CenterX, unit.CenterY, .1), "Circle direction starts: " + i);
            var wp = unit.Waypoints[0]; double a = .37 + i * Math.PI / 8, length = .8 / ZConst.PX_TO_M;
            check(Math.Abs(wp.x - unit.CenterX - Math.Cos(a) * length) <= 1.01 && Math.Abs(wp.y - unit.CenterY - Math.Sin(a) * length) <= 1.01, "Circle direction follows forward plus exact 22.5-degree step: " + i);
        }
        foreach (var limit in new[] { ZBehaviorKey.MaxDistance, ZBehaviorKey.MaxDuration, ZBehaviorKey.BoostMaxDistance, ZBehaviorKey.BoostDuration })
        {
            var world = ZWorld.Create(settings, map, 231);
            world.Pathing.Enabled = false;
            var profile = p.Clone();
            profile.Set("", ZBehaviorKey.BoostChance, 100); profile.Set("", ZBehaviorKey.Endurance, 0);
            profile.Set("", ZBehaviorKey.PremiumBoost, 0); profile.Set("", ZBehaviorKey.MinDistance, 1);
            profile.Set("", ZBehaviorKey.MaxDistance, .8);
            profile.Set("", ZBehaviorKey.Distance, 4); profile.Set("", limit, limit == ZBehaviorKey.MaxDistance || limit == ZBehaviorKey.BoostMaxDistance ? .15 : .05);
            world.SetBehavior(profile);
            var tank = world.CreateObject("vehicle", "light", x + 32, y + 32, Team.Red);
            tank.BehaviorHeadingSet = true; tank.BehaviorHeading = 0;
            tank.Waypoints.Add(new ZWaypoint { mode = WpMode.Move, x = tank.CenterX + 60, y = tank.CenterY });
            check(tank.DodgeMissile(tank.CenterX, tank.CenterY, 2), "Limited dodge starts: " + limit + " (" + tank.BehaviorDodgeReason + ")");
            double boostedAfterLimit = -1;
            for (int tick = 1; tick <= 35; tick++)
            {
                world.Time = tick * .01; tank.ProcessServer(world.Time);
                if (tank.BehaviorDodging && world.Time < .04) tank.DodgeMissile(tank.CenterX, tank.CenterY, 2);
                if (tick == 20) boostedAfterLimit = tank.BehaviorBoostDistance;
            }
            if (limit == ZBehaviorKey.MaxDistance) check(tank.BehaviorDodgeDistance <= .150001 && !tank.BehaviorDodging, "Total dodge distance capped across replans and original move resumes");
            if (limit == ZBehaviorKey.MaxDuration) check(!tank.BehaviorDodging && tank.BehaviorDodgeDistance > 0, "Dodge duration ends despite repeated threats");
            if (limit == ZBehaviorKey.BoostMaxDistance) check(tank.BehaviorBoostDistance <= .150001 && tank.BehaviorDodgeDistance > tank.BehaviorBoostDistance, "Boost distance caps sprint while dodge continues at normal pace");
            if (limit == ZBehaviorKey.BoostDuration) check(tank.BehaviorBoostDistance > 0 && tank.BehaviorBoostDistance == boostedAfterLimit && tank.BehaviorDodgeDistance > tank.BehaviorBoostDistance, "Boost duration cannot restart on replan and normal movement continues");
        }
    }

    static void CheckTrackedClassic(string settings, string map, int x, int y, Action<bool, string> check)
    {
        var profile = ZBehaviorProfile.Classic(ZBehaviorFamily.Tanks);
        double lastRate = double.PositiveInfinity, lastAcceleration = 0, lastBraking = 0;
        foreach (string type in new[] { "light", ZMTank.Type, "heavy" })
        {
            var motion = profile.Resolve(type);
            double rate = motion[ZBehaviorKey.TurnRate], acceleration = motion[ZBehaviorKey.Acceleration], braking = motion[ZBehaviorKey.Braking];
            check(motion.On(ZBehaviorKey.Physical) && motion.On(ZBehaviorKey.TurnFirst) && motion.On(ZBehaviorKey.DodgePhysical) &&
                !motion.On(ZBehaviorKey.Lateral) && !motion.On(ZBehaviorKey.Reverse) && motion[ZBehaviorKey.BoostSpeed] == 1.8,
                "Z Classic tracked motion faces the move and uses the Zod boost: " + type);
            check(rate > 0 && rate < lastRate && acceleration > lastAcceleration && braking > lastBraking,
                "Z Classic pivot is finite and the heavier tracked hull responds more slowly: " + type);
            lastRate = rate; lastAcceleration = acceleration; lastBraking = braking;
            var world = ZWorld.Create(settings, map, 409); world.Pathing.Enabled = false; world.SetBehavior(profile);
            var tank = world.CreateObject("vehicle", type, x + 32, y + 32, Team.Red);
            int startX = tank.CenterX, startY = tank.CenterY;
            tank.BehaviorHeadingSet = true; tank.BehaviorHeading = 0;
            tank.Waypoints.Add(new ZWaypoint { mode = WpMode.Move, x = startX, y = startY + 24 });
            world.Time = .01; tank.ProcessServer(world.Time);
            check(tank.CenterX == startX && tank.CenterY == startY && tank.BehaviorHeading > 0 && tank.BehaviorHeading < Math.PI / 2,
                "A perpendicular move starts by rotating the tracked hull in place: " + type);
            for (int tick = 2; tick <= 400; tick++)
            { world.Time = tick * .01; tank.ProcessServer(world.Time); }
            check(tank.CenterY > startY + 10, "The tracked hull advances after facing the destination: " + type);
        }
        check(!profile.Resolve("jeep").On(ZBehaviorKey.Physical) && !profile.Resolve(ZMTank.Mech).On(ZBehaviorKey.Physical),
            "Z Classic keeps prior movement on the jeep and the historic medium ID used by the mech");
    }
}
