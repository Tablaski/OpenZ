using System;
using System.Linq;
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
    }
}
