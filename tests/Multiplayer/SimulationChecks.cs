using System;
using System.IO;
using System.Linq;
using OpenZ;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class SimulationChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../source/Assets/OpenZ/Resources/OpenZ"));
        string settings = File.ReadAllText(Path.Combine(root, "default_settings.txt"));
        foreach (string map in new[] { "L02", "L07", "L16", "L17", "L19" })
        {
            string json = File.ReadAllText(Path.Combine(root, "Maps", map + "_grid.json"));
            var military = ZWorld.Create(settings, json);
            military.Objects.RemoveAll(o => o.IsUnit);
            var heavy = military.CreateObject("vehicle", "heavy", 160, 160, Team.Red);
            military.CreateObject("vehicle", "heavy", 224, 160, Team.Red);
            for (int i = 0; i < 4; i++) military.CreateObject("vehicle", "light", 160 + i * 64, 240, Team.Blue);
            military.CreateRobotGroup("grunt", 160, 320, Team.Red);
            double redValue = 2 * military.S.Unit("heavy").build_time + military.S.Unit("grunt").build_time;
            check(military.MilitaryValue(Team.Red) == redValue, map + ": military value counted robot squad members more than once");
            check(military.MilitaryValue(Team.Blue) == 4 * military.S.Unit("light").build_time, map + ": military value did not weight light tanks by their normal cost");
            military.CreateObject("cannon", "missile_cannon", 400, 400, Team.Red);
            military.CreateObject("vehicle", "heavy", 480, 400, Team.Neutral);
            var destroyed = military.CreateObject("vehicle", "heavy", 560, 400, Team.Red); destroyed.Health = 0;
            check(military.MilitaryValue(Team.Red) == redValue && military.MilitaryValue(Team.Neutral) == 0,
                map + ": turrets, destroyed or neutral units changed military value");
            military.CreateObject("vehicle", "crane", 640, 400, Team.Red);
            var unarmed = military.CreateObject("vehicle", "jeep", 704, 400, Team.Red); unarmed.Damage = 0;
            check(military.MilitaryValue(Team.Red) == redValue, map + ": cranes or unarmed mobile units changed offensive production value");
            var apc = military.CreateObject("vehicle", "apc", 768, 400, Team.Blue);
            check(apc.CanAttack() && military.MilitaryValue(Team.Blue) == 4 * military.S.Unit("light").build_time + military.S.Unit("apc").build_time,
                map + ": armed APC was excluded because its weapon comes from its crew");
            heavy.Health = 1;
            check(military.MilitaryValue(Team.Red) == redValue, map + ": damage changed normal construction value");
            military.Objects.Remove(heavy);
            check(military.MilitaryValue(Team.Red) == redValue - military.S.Unit("heavy").build_time, map + ": deleted unit retained military value");
            check(Math.Abs(ZForceComparison.RelativePercent(1300, 1000) - 30) < 1e-9 && Math.Abs(ZForceComparison.RelativePercent(1000, 1300) + 30) < 1e-9,
                map + ": relative force gap is not symmetric against the smaller army");
            check(ZForceComparison.Share(0, 0) == .5 && ZForceComparison.RelativePercent(0, 0) == 0 &&
                double.IsPositiveInfinity(ZForceComparison.RelativePercent(1, 0)) && double.IsNegativeInfinity(ZForceComparison.RelativePercent(0, 1)),
                map + ": empty-army force comparison failed");
            var host = ZWorld.Create(settings, json);
            host.Bots.Add(new ZBot(host, Team.Blue, 1));
            host.TechBonus[1] = 5;
            var rules = new DuelRules { Map = map };
            rules.EnableHandicap(0); rules.Handicap.BuildDuration = .8;
            host.ConfigureDuel(rules, false);
            check(host.Bots.Count == 0 && host.TechBonus.All(b => b == 0), map + ": solo advantages retained");
            rules.Handicap.BuildDuration = .1;
            check(host.DuelConfiguration.Handicap.BuildDuration == .8, map + ": caller mutated match rules");
            var red = host.Objects.First(o => o.Owner == Team.Red && o.IsMobile && !o.IsMinion);
            var blue = host.Objects.First(o => o.Owner == Team.Blue && o.IsMobile && !o.IsMinion);
            var commands = new DuelOrderReceiver(host, 1);
            check(!commands.Execute(new DuelOrder { Sequence = 1, ObjectId = red.Id, Kind = DuelOrderKind.Stop }), map + ": blue controlled red");
            check(commands.Execute(new DuelOrder { Sequence = 2, ObjectId = blue.Id, Kind = DuelOrderKind.Stop }), map + ": blue cannot control own unit");
            check(!commands.Execute(new DuelOrder { Sequence = 2, ObjectId = blue.Id, Kind = DuelOrderKind.Hold }), map + ": duplicate executed");
            check(!commands.Execute(new DuelOrder { Sequence = 3, ObjectId = blue.Id, Kind = DuelOrderKind.Waypoints,
                Points = new[] { ZWaypoint.Move(-1, 0) } }), map + ": out of map accepted");
            var fort = host.Objects.First(o => o.Owner == Team.Red && o.IsProducer);
            check(Math.Abs(fort.BuildTimeModified(100) - .8 * (100 - 50 * fort.ZoneOwnage)) < .01,
                map + ": production hook inactive");
            fort.Building = true; fort.CurUnit = "grunt"; fort.BInit = 0; fort.BFinal = 100;
            fort.ZoneOwnage = 0; host.Time = 20;
            fort.ResetBuildTime(.5);
            check(Math.Abs(fort.ProductionProgress - .2) < 1e-9, map + ": capture erased completed work");
            check(fort.ProductionTimeLeft > 0, map + ": capture instantly completed construction");
            var replica = ZWorld.Create(settings, json);
            replica.ConfigureDuel(new DuelRules { Map = map }, true);
            replica.Tick(1);
            check(replica.Time == 0, map + ": client ran simulation");
            check(!new DuelOrderReceiver(replica, 0).Execute(new DuelOrder { Sequence = 1, ObjectId = red.Id, Kind = DuelOrderKind.Stop }),
                map + ": replica executed local command");
            reject(() => replica.ConfigureDuel(new DuelRules { Map = map }, false), map + ": reconfigured active world");
            for (int i = 0; i < 1000; i++) { host.Tick(.01); host.Events.Clear(); }
            check(host.Time > 29.9 && host.Objects.Count > 0, map + ": authoritative map tick failed");
            check(host.Bots.Count == 0 && host.IsHumanTeam(Team.Blue) && host.IsHumanTeam(Team.Red), map + ": CPU assigned to human duel seat");
            check(!host.CalmStart(Team.Blue) && !host.CalmStart(Team.Red), map + ": natural unit autonomy was suppressed after the normal calm start");
            var squad = host.Objects.First(o => o.Minions.Count > 0);
            host.AddMissileForTest(new ZMissile { x = 321, y = 456, attackerId = red.Id, explodeTime = host.Time + 3, kick = -1 });
            var deltaSender = new DuelDeltaSender(); var deltaReceiver = new DuelDeltaReceiver();
            var first = deltaSender.Capture(DuelReplica.Capture(host));
            byte[] initialPayload = DuelDeltaCodec.Encode(first);
            check(deltaReceiver.Apply(DuelDeltaCodec.Decode(initialPayload)), map + ": initial state rejected");
            DuelReplica.ApplyDelta(replica, first);
            check(replica.Time == host.Time && replica.Objects.Count == host.Objects.Count, map + ": world state lost");
            check(replica.QueuedMissiles.Count == host.QueuedMissiles.Count && replica.QueuedMissiles.Last().x == 321 && replica.QueuedMissiles.Last().kick == -1,
                map + ": projectile presentation lost");
            check(replica.Zones.Where(z => z.Flag != null).All(z => z.Flag == replica.Get(z.Flag.Id)), map + ": stale zone flag reference");
            check(replica.Get(blue.Id).Owner == Team.Blue, map + ": blue ownership lost");
            check(replica.Get(squad.Id).Minions.Count == squad.Minions.Count &&
                replica.Get(squad.Id).Minions[0].Leader == replica.Get(squad.Id), map + ": squad membership lost");
            check(replica.Get(fort.Id).CurUnit == fort.CurUnit && replica.Get(fort.Id).BFinal == fort.BFinal,
                map + ": production state lost");
            var fresh = host.CreateObject("vehicle", "jeep", 160, 160, Team.Blue);
            fresh.Health = fresh.MaxHealth / 3;
            var next = deltaSender.Capture(DuelReplica.Capture(host));
            check(deltaReceiver.Apply(DuelDeltaCodec.Decode(DuelDeltaCodec.Encode(next))), map + ": delta rejected");
            DuelReplica.ApplyDelta(replica, next);
            var remote = replica.Get(fresh.Id);
            check(remote != fresh && remote.Health == fresh.Health && remote.Drivers.Count == fresh.Drivers.Count,
                map + ": new entity state lost");
            var idle = deltaSender.Capture(DuelReplica.Capture(host));
            check(idle.Changed.Count == 0 && deltaReceiver.Apply(idle), map + ": unchanged simulation produced traffic");
            fresh.KillAt = host.Time; host.Tick(.01); host.Events.Clear();
            var removed = deltaSender.Capture(DuelReplica.Capture(host));
            check(deltaReceiver.Apply(removed), map + ": deletion rejected");
            DuelReplica.ApplyDelta(replica, removed);
            check(replica.Get(fresh.Id) == null && replica.GetAny(fresh.Id) != null, map + ": deletion lost event graveyard");
            Console.WriteLine(map + ": " + host.Objects.Count + " objects; initial state " + initialPayload.Length + " bytes; schema " + DuelReplica.SchemaHash.Substring(0, 12));
            var duel = ZWorld.Create(settings, json);
            var startingRules = new DuelRules { Map = map, Common = DuelMissionDefaults.Read(duel.Map) };
            check(startingRules.Common.FortTurrets.SequenceEqual(new[] { "howitzer", "missile_cannon", "", "" }) && startingRules.Common.StartingUnits.Count <= 4,
                map + ": duel defaults do not fit the four-slot layout");
            startingRules.EnableHandicap(1); startingRules.Handicap.StartingUnits.AddRange(new[] { "", "heavy", "", "" });
            duel.ConfigureDuel(startingRules, false);
            duel.DuelApplyStartingArmies();
            int redCount = duel.Objects.Count(o => o.Owner == Team.Red && o.IsUnit);
            check(redCount > 0 && redCount + 1 == duel.Objects.Count(o => o.Owner == Team.Blue && o.IsUnit), map + ": additional handicap unit was not added to the shared army");
            foreach (var team in new[] { Team.Red, Team.Blue })
            {
                check(duel.Objects.Count(o => o.Owner == team && o.Kind == ZKind.Cannon && !o.Ejectable &&
                    (o.Type == "howitzer" || o.Type == "missile_cannon")) == 2, map + ": shared fort cannons missing");
            }
            duel.DuelSurrender(0);
            check(duel.GameOver && duel.Winner == Team.Blue && duel.Objects.Where(o => o.IsFort && o.Owner == Team.Red).All(o => o.Destroyed), map + ": surrender did not destroy fort and award victory");
        }
    }
}
