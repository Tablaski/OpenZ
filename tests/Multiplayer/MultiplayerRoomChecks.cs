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
        reject(() => room.MarkLoaded(extra, room.Revision), "Ghosts do not acknowledge the active-player loading barrier");
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
        CheckCoop(check, reject);
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
}
