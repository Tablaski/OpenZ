using System;
using System.IO;
using System.Linq;
using OpenZ;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class MultiplayerRoomChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        const string host = "host-link-id", guest = "guest-link-id", extra = "extra-link-id";
        var room = new MultiplayerRoom(host, "Host", "SMP03M01");
        room.Join(guest, "Guest");
        check(room.Starts == 3 && room.StartBlocker == MatchStartBlocker.EmptyStartingPositions, "A missing third participant blocks the three-start room");
        reject(() => room.SetBot(guest, 2, 1, ZBotDifficulty.Easy), "Guests cannot assign bots");
        room.AssignHuman(host, guest, 1, 0);
        room.SetReady(host, room.Revision, true);
        int stale = room.Revision;
        room.SetBot(host, 2, 1, ZBotDifficulty.Easy);
        reject(() => room.SetReady(guest, stale, true), "A bot edit invalidates previous room readiness");
        check(room.StartBlocker == MatchStartBlocker.None && !room.CanLaunch, "A full roster still waits for ready humans");
        room.Join(extra, "Observer");
        check(room.StartBlocker == MatchStartBlocker.UnassignedPlayer, "A human joining a full room blocks launch until assigned");
        room.AssignObserver(host, extra);
        reject(() => room.SetReady(extra, room.Revision, true), "Ghosts cannot mark themselves ready");
        room.SetReady(host, room.Revision, true);
        room.SetReady(guest, room.Revision, true);
        check(room.CanLaunch && room.Bots.Single().Difficulty == ZBotDifficulty.Easy &&
            room.Members.Single(member => member.Id == guest).Alliance == 0, "Two humans and one bot can launch with an observer");
        reject(() => room.Launch(guest), "Only the host launches the match");
        room.Launch(host);
        room.MarkLoaded(host, room.Revision);
        check(!room.MatchLoaded, "The match waits for the second human to load");
        room.MarkLoaded(extra, room.Revision);
        check(!room.MatchLoaded && room.HumanTeam(extra) == Team.Neutral,
            "A ghost may acknowledge loading without taking part in the active-player barrier");
        reject(() => room.MarkLoaded(guest, stale), "Stale loading acknowledgments are rejected");
        room.MarkLoaded(guest, room.Revision);
        check(room.MatchLoaded, "Both humans loaded, with no barrier for bots or ghosts");
        reject(() => room.SelectMap(host, "L02"), "The map cannot change behind clients after launch");
        reject(() => room.AssignObserver(host, guest), "Launched seating cannot be edited as lobby settings");
        room.ReturnToLobby(host);
        check(!room.CanLaunch && room.Members.All(member => !member.Ready), "Returning to the room resets consent");

        var larger = new MultiplayerRoom(host, "Host", "SMP04M01");
        larger.Join(guest, "Guest");
        larger.Join(extra, "Extra");
        larger.SetBot(host, 3, 3, ZBotDifficulty.Normal);
        larger.SelectMap(host, "L02");
        check(larger.Starts == 2 && larger.Bots.Length == 0 && larger.Members.Length == 3 &&
            larger.StartBlocker == MatchStartBlocker.UnassignedPlayer, "Shrinking the map clears spare bots and preserves extra humans for explicit reassignment");
        larger.RemoveHuman(host, extra);
        check(larger.StartBlocker == MatchStartBlocker.None, "Removing the extra human resolves the capacity conflict");
        reject(() => larger.SelectMap(host, "SM18"), "Solo-only maps stay outside multiplayer setup");
        var resize = new MultiplayerRoom(host, "Host", "SMP08M01");
        var participants = Enumerable.Range(1, 7).Select(i => "guest-" + i).ToArray();
        foreach (string id in participants) resize.Join(id, id);
        resize.SetReady(host, resize.Revision, true);
        var active = new[] { host, participants[2], participants[4], participants[6] };
        int before = resize.Revision;
        reject(() => resize.ConfigureMap(host, "SMP04M01", 4, active.Append("unknown")), "An invalid roster cannot partially change the map");
        check(resize.Starts == 8 && resize.Revision == before, "A rejected map switch leaves the room unchanged");
        resize.ConfigureMap(host, "SMP04M01", 4, active);
        check(resize.Starts == 4 && resize.Revision == before + 1 && resize.Members.Count(member => member.Seat.MayUsePlayerCommands) == 4 &&
            resize.Members.Count(member => member.Seat.Role == MatchSeatRole.Observer) == 4 &&
            resize.Members.All(member => !member.Ready) && resize.StartBlocker == MatchStartBlocker.None,
            "A smaller map assigns active humans and ghosts in one revision");
        int teamsBefore = resize.Revision;
        resize.SetAlliances(host, teamsBefore, new[] { 0, 1, 0, 1 });
        check(resize.Revision == teamsBefore + 1 && resize.Members.Where(member => member.Seat.MayUsePlayerCommands)
            .All(member => member.Alliance == member.Seat.Position % 2), "Team edits publish as one revision");
        CheckCoop(check, reject);
        CheckLobbyRules(check, reject);
    }

    static void CheckCoop(Action<bool, string> check, Action<Action, string> reject)
    {
        const string host = "host", guest = "guest";
        var room = new MultiplayerRoom(host, "Host", "SMP03M01");
        room.Join(guest, "Guest");
        room.AssignHuman(host, guest, 1, 0);
        room.SetBot(host, 2, 0, ZBotDifficulty.Easy);
        room.SetReady(host, room.Revision, true);
        room.SetReady(guest, room.Revision, true);
        reject(() => room.Launch(host), "A cooperative room still requires an opposing alliance");

        room.SetBot(host, 2, 1, ZBotDifficulty.Easy);
        room.SetReady(host, room.Revision, true);
        room.SetReady(guest, room.Revision, true);
        room.Launch(host);
        room.MarkLoaded(host, room.Revision);
        room.MarkLoaded(guest, room.Revision);

        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var settings = File.ReadAllText(Path.Combine(root, "source/Assets/OpenZ/Resources/OpenZ/default_settings.txt"));
        var grid = File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps/SMP03M01_grid.json"));
        var world = ZWorld.Create(settings, grid, 12);
        room.ConfigureWorld(world, 12);
        var red = world.Objects.First(o => o.Owner == Team.Red && o.IsUnit && o.CanAttack());
        var blue = world.Objects.First(o => o.Owner == Team.Blue && o.IsUnit && o.CanAttack());
        var blueFort = world.Objects.Single(o => o.IsFort && o.Owner == Team.Blue);
        check(room.HumanTeam(host) == Team.Red && room.HumanTeam(guest) == Team.Blue &&
            world.AreAllied(Team.Red, Team.Blue) && !world.AreAllied(Team.Red, Team.Green),
            "Co-op allies retain separate owners and face the opposing bot");
        check(!red.CanAttackObject(blue) && !blueFort.CanEnterFort(Team.Red),
            "Co-op allies cannot target each other or enter each other's forts");
        world.Tick(ZConst.TICK);
        check(!world.GameOver, "The opposing bot keeps the co-op match running");
        check(world.SurrenderSkirmish(Team.Green), "Eliminating the last opposing seat resolves co-op");
        for (int tick = 0; tick < 150; tick++) world.Tick(ZConst.TICK);
        check(world.IsWinningSeat(Team.Red) && world.IsWinningSeat(Team.Blue) &&
            !world.IsWinningSeat(Team.Green), "Both co-op seats share victory");
    }

    static void CheckLobbyRules(Action<bool, string> check, Action<Action, string> reject)
    {
        const string host = "host", guest = "guest";
        var room = new MultiplayerRoom(host, "Host", "SMP03M01");
        room.Join(guest, "Guest");
        room.SetClosed(host, 2, true);
        room.SetReady(host, room.Revision, true);
        var rules = room.Rules;
        rules.BuildPercent[1] = 80;
        rules.Common[0] = "light";
        rules.Extras[4] = "medium";
        rules.TerritoryCap = 25;
        rules.ProgressiveTerritory = true;
        rules.FortSpeedup = 200;
        rules.ProgressiveFort = true;
        reject(() => room.SetRules(guest, rules), "Only the creator can edit Skirmish rules");
        room.SetRules(host, rules);
        check(!room.Members.First().Ready && room.Rules.StartingUnits(0).SequenceEqual(new[] { "light" }) &&
            room.Rules.StartingUnits(1).SequenceEqual(new[] { "light", "medium" }), "Shared army and seat extras are isolated and edits reset readiness");
        rules.BuildPercent[1] = 50;
        check(room.Rules.BuildPercent[1] == 80 && room.StartBlocker == MatchStartBlocker.None, "Closed starts count as set up and rule copies cannot mutate the room");
        room.SetReady(host, room.Revision, true);
        room.SetReady(guest, room.Revision, true);
        room.Launch(host);
        room.MarkLoaded(host, room.Revision);
        room.MarkLoaded(guest, room.Revision);
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var settings = File.ReadAllText(Path.Combine(root, "source/Assets/OpenZ/Resources/OpenZ/default_settings.txt"));
        var grid = File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps/SMP03M01_grid.json"));
        var world = ZWorld.Create(settings, grid, 12);
        var original = world.Objects.Count(unit => unit.Owner == Team.Blue && unit.IsUnit);
        room.ConfigureWorld(world, 12);
        check(world.SkirmishEliminated(Team.Green) && !world.Objects.Any(unit => unit.IsFort && unit.Owner == Team.Green), "Closed Green does not compete");
        check(world.Objects.Count(unit => unit.Owner == Team.Blue && unit.IsUnit) == original + 2, "Common and individual starting vehicles are added beside the fort");

        world = ZWorld.Create(settings, grid, 12);
        rules = room.Rules;
        rules.Common[0] = rules.Extras[4] = "";
        world.SetSkirmishAlliances(new[] { 0, 0, 1 });
        world.ConfigureSkirmishRules(rules, Array.Empty<int>());
        var greenFort = world.Objects.First(unit => unit.IsFort && unit.Owner == Team.Green);
        double our = world.MilitaryValue(Team.Green);
        double rival = world.MilitaryValue(Team.Red) + world.MilitaryValue(Team.Blue);
        double lead = ZForceComparison.RelativePercent(our, rival);
        var policy = rules.Production;
        double territory = policy.TerritoryTimeReduction(world.TeamFlagFraction(Team.Green), lead);
        double rivalReduction = policy.TerritoryTimeReduction(world.TeamFlagFraction(Team.Red) + world.TeamFlagFraction(Team.Blue), -lead);
        double expected = 100 * Math.Min(1 - territory, 1 / policy.FortCatchupSpeed(-lead, rivalReduction));
        check(Math.Abs(greenFort.BuildTimeModified(100) - expected) < .000001 && lead < 0,
            "Catch-up uses combined offensive value and territory of the opposing alliance");
    }
}
