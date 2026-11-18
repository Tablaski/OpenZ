using System;
using System.IO;
using System.Linq;
using OpenZ;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class LobbyRulesChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        ProposalChecks.Run(check, reject);
        var rules = new DuelRules();
        check(rules.AllowedUnits.Count == 22 && DuelUnitRoster.All.All(rules.Allows), "OpenZ preset allows the complete 22-type game catalogue");
        rules.Common.StartingUnits.AddRange(new[] { "mtank", "electrobot", "light" });
        rules.Handicap.StartingUnits.Add("medium");
        rules.SetUnitPreset(DuelUnitPreset.Z1996); rules.Validate();
        check(rules.AllowedUnits.Count == 17 && rules.Allows("light") && rules.Allows("mtank") && rules.Allows("heavy") && !rules.Allows("medium") && !rules.Allows("electrobot") && !rules.Allows("evilzod") && !rules.Allows("shuttle") && !rules.Allows("heroes"),
            "Z 1996 matches Zod's 6 robots, 7 vehicles and 4 cannons, including the Medium Tank model");
        check(rules.Common.StartingUnits.SequenceEqual(new[] { "mtank", "", "light" }) && rules.Handicap.StartingUnits[0] == "", "Preset switches clear excluded shared and handicap slots without shifting positions");
        rules.SetUnitAllowed("gun", false);
        check(rules.UnitPreset == DuelUnitPreset.Custom && !rules.Allows("gun"), "Manual toggle selects Custom");
        rules.SetUnitAllowed("gun", true);
        check(rules.UnitPreset == DuelUnitPreset.Custom, "Manual restoration keeps the explicit Custom preset");
        var copy = rules.Copy(); copy.AllowedUnits.Clear();
        check(rules.AllowedUnits.Count == 17, "Rules copy owns its allowed-unit collection");
        var bad = rules.Copy(); bad.AllowedUnits.Add("unknown"); reject(bad.Validate, "Unknown roster entry rejected");
        bad = rules.Copy(); bad.AllowedUnits.Add("grunt"); reject(bad.Validate, "Duplicate roster entry rejected");
        bad = rules.Copy(); bad.UnitPreset = DuelUnitPreset.OpenZ; reject(bad.Validate, "Named preset cannot claim a different unit selection");
        rules.SetUnitAllowed("howitzer", false);
        check(rules.Common.FortTurrets.All(t => t != "howitzer"), "Excluding a cannon clears its fort mount");
        var empty = new DuelRules(); empty.SetUnitPreset(DuelUnitPreset.Custom); empty.AllowedUnits.Clear();
        check(!empty.HasStartingArmies, "An empty permitted starting army cannot be marked ready in the lobby UI");
        empty.SetUnitPreset(DuelUnitPreset.OpenZ); empty.Common.StartingUnits.Add("light");
        check(empty.HasStartingArmies, "A valid shared starting unit enables readiness for both teams");
        var lobby = new DuelLobby(); lobby.Join(0, "Host"); lobby.Join(1, "Guest");
        lobby.SetReady(0, lobby.Revision, true); lobby.ChangeRules(0, rules);
        check(!lobby.Ready(0) && !lobby.Ready(1), "Roster changes invalidate both ready confirmations");
        reject(() => lobby.ChangeRules(1, rules), "Guest cannot change allowed units or dynamics");
        lobby.SetReady(0, lobby.Revision, true); lobby.SetReady(1, lobby.Revision, true);
        reject(() => lobby.ChangeRules(0, rules), "Host cannot change rules after loading starts");

        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../source/Assets/OpenZ/Resources/OpenZ"));
        string settings = File.ReadAllText(Path.Combine(root, "default_settings.txt"));
        foreach (string map in new[] { "L02", "L07", "L16", "L17" })
        {
            var configured = new DuelRules { Map = map };
            configured.Common.StartingUnits.Add("light");
            configured.SetUnitAllowed("grunt", false); configured.SetUnitAllowed("heavy", false); configured.SetUnitAllowed("gun", false);
            var tanks = new ZBehaviorProfile { family = ZBehaviorFamily.Tanks, name = "Host tanks", master = 100 };
            tanks.Set("light", ZBehaviorKey.RedShake, 3.5);
            var robots = new ZBehaviorProfile { family = ZBehaviorFamily.Robots, name = "Host robots", master = 50 };
            configured.TankDynamics = tanks.Encode(); configured.RobotDynamics = robots.Encode();
            string json = File.ReadAllText(Path.Combine(root, "Maps", map + "_grid.json"));
            var solo = ZWorld.Create(settings,json,672);
            solo.TechBonus[0]=1;var tech=solo.TechBonus.ToArray();
            var soloBot=new ZBot(solo,Team.Blue,774);solo.Bots.Add(soloBot);
            var soloProfile=new ZBehaviorProfile{family=ZBehaviorFamily.Tanks,master=31};solo.SetBehavior(soloProfile);
            var allowed=new System.Collections.Generic.List<string>{"light","psycho"};
            solo.ConfigureSoloUnits(allowed);allowed.Clear();
            check(solo.DuelConfiguration==null&&!solo.DuelIsReplica&&solo.Bots.Contains(soloBot)&&solo.TechBonus.SequenceEqual(tech),map+": solo selection preserves campaign rules, bots and tech bonuses");
            check(solo.BehaviorProfile(ZBehaviorFamily.Tanks).Encode()==soloProfile.Encode(),map+": solo selection preserves personal combat dynamics");
            check(solo.Objects.Where(o=>o.IsUnit).All(o=>solo.DuelAllowsUnit(o.IsHero?"heroes":o.Type)),map+": solo selection removes excluded map and starting units");
            foreach(var producer in solo.Objects.Where(o=>o.IsProducer))producer.Level=5;
            soloBot.ChooseBuildOrders();
            check(solo.Objects.Where(o=>o.IsProducer).All(o=>solo.ProductionUnits(o).All(t=>t=="light"||t=="psycho")&&(o.CurUnit==null||solo.DuelAllowsUnit(o.CurUnit))),map+": solo CPU selects available permitted units");
            var soloFort=solo.Objects.First(o=>o.IsFort&&o.Owner==Team.Red);
            check(!soloFort.SetBuildingProduction("heavy")&&!soloFort.AddBuildingQueue("electrobot")&&solo.DuelAllowsUnit("light"),map+": solo filtering owns its selection and rejects forbidden production");
            var original=ZWorld.Create(settings,json,672);int originalCount=original.Objects.Count;var production=original.Objects.Where(o=>o.IsProducer).Select(o=>o.CurUnit).ToArray();
            original.ConfigureSoloUnits(DuelUnitRoster.All);
            check(original.Objects.Count==originalCount&&original.Objects.Where(o=>o.IsProducer).Select(o=>o.CurUnit).SequenceEqual(production),map+": default OpenZ solo selection preserves the original mission");
            var host = ZWorld.Create(settings, json, 672);
            host.CreateObject("vehicle", "heavy", 240, 240, Team.Neutral);
            var fort = host.Objects.First(o => o.IsFort && o.Owner == Team.Red);
            fort.Level = 5; fort.StoredCannons.Add("gun");
            host.ConfigureDuel(configured, false); host.DuelApplyStartingArmies();
            check(host.Objects.Where(o => o.IsUnit).All(o => host.DuelAllowsUnit(o.IsHero ? "heroes" : o.Type)), map + ": starting and neutral map units obey the roster");
            check(!fort.StoredCannons.Contains("gun") && !host.ProductionUnits(fort).Contains("grunt") && !host.ProductionUnits(fort).Contains("heavy"), map + ": stored cannons and visible production lists are filtered");
            fort.StopBuildingProduction();
            check(fort.SetBuildingDefaultProduction() && fort.CurUnit != "grunt", map + ": default production chooses the first permitted type");
            check(!fort.SetBuildingProduction("heavy") && !fort.AddBuildingQueue("heavy"), map + ": direct production and queue calls reject excluded units");
            var receiver = new DuelOrderReceiver(host, 0);
            check(!receiver.Execute(new DuelOrder { Sequence = 1, ObjectId = fort.Id, Kind = DuelOrderKind.Build, Unit = "heavy" }) &&
                !receiver.Execute(new DuelOrder { Sequence = 2, ObjectId = fort.Id, Kind = DuelOrderKind.Queue, Unit = "grunt" }), map + ": server rejects forbidden network build/queue commands");
            check(host.BehaviorProfile(ZBehaviorFamily.Tanks).Encode() == configured.TankDynamics && host.BehaviorProfile(ZBehaviorFamily.Robots).Encode() == configured.RobotDynamics,
                map + ": match uses the two explicit lobby profiles");
            reject(() => host.SetBehavior(new ZBehaviorProfile { family = ZBehaviorFamily.Tanks }), map + ": authoritative dynamics are immutable during the match");
            configured.SetUnitAllowed("heavy", true); tanks.Set("light", ZBehaviorKey.RedShake, 0);
            check(!host.DuelAllowsUnit("heavy") && host.BehaviorProfile(ZBehaviorFamily.Tanks).Get("light", ZBehaviorKey.RedShake) == 3.5, map + ": later caller edits cannot mutate a live match");
            var guest = ZWorld.Create(settings, json, 672); guest.ConfigureDuel(host.DuelConfiguration, true);
            DuelReplica.Apply(guest, DuelReplica.Capture(host));
            check(!guest.DuelAllowsUnit("heavy") && guest.BehaviorProfile(ZBehaviorFamily.Tanks).Encode() == host.BehaviorProfile(ZBehaviorFamily.Tanks).Encode() &&
                guest.ProductionUnits(guest.Get(fort.Id)).SequenceEqual(host.ProductionUnits(fort)), map + ": guest inspection, production and replicated dynamics match the host");
            reject(() => host.ConfigureDuel(new DuelRules { Map = map }, false), map + ": match roster cannot be replaced during play");
        }
        reject(() => ZWorld.DuelProfile(new ZBehaviorProfile { family = ZBehaviorFamily.Robots }.Encode(), ZBehaviorFamily.Tanks), "Wrong-family profile rejected");
    }
}
