using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using OpenZ;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class MultiplayerRoomLinkChecks
{
    sealed class LoopbackPeer : IDuelPeer
    {
        readonly Queue<DuelPacket> inbox = new Queue<DuelPacket>();
        public LoopbackPeer Other;
        public string RemoteName => "Loopback";
        public string Failure => null;
        public bool Closed { get; private set; }
        public long StatePayloadBytesSent => 0;
        public bool Send(DuelPacket packet)
        {
            if (Closed || Other.Closed) return false;
            using var stream = new MemoryStream();
            DuelWire.WriteAsync(stream, packet, CancellationToken.None).GetAwaiter().GetResult();
            stream.Position = 0;
            Other.inbox.Enqueue(DuelWire.ReadAsync(stream, CancellationToken.None).GetAwaiter().GetResult());
            return true;
        }
        public bool TryReceive(out DuelPacket packet) => inbox.TryDequeue(out packet);
        public void Dispose() => Closed = true;
    }

    public static void Run(Action<bool, string> check)
    {
        var room = new MultiplayerRoom("host-link", "Host", "SMP03M01");
        room.Join("guest-link", "Guest");
        using var hostPeer = new LoopbackPeer();
        using var guestPeer = new LoopbackPeer();
        hostPeer.Other = guestPeer; guestPeer.Other = hostPeer;
        var host = new MultiplayerRoomHostLink(room, hostPeer, "guest-link");
        var guest = new MultiplayerRoomGuestLink(guestPeer);
        bool DeliverHost() => hostPeer.TryReceive(out var packet) && host.Receive(packet);
        bool DeliverGuest() => guestPeer.TryReceive(out var packet) && guest.Receive(packet);

        check(host.Publish() && DeliverGuest() && guest.View.Starts == 3 && guest.View.StartBlocker == MatchStartBlocker.EmptyStartingPositions,
            "A guest receives the three-seat room through the existing packet framing");
        var oldView = MultiplayerRoomPackets.Snapshot(guest.View);
        room.SetBot("host-link", 2, 2, ZBotDifficulty.Easy);
        check(guest.SetReady(true) && !DeliverHost(), "A readiness request sent from an old room view is rejected");
        check(host.Publish() && guestPeer.TryReceive(out var sizeCheck) && !Encoding.UTF8.GetString(sizeCheck.Payload).Contains("guest-link") &&
            guest.Receive(sizeCheck) && guest.View.Bots.Single().Difficulty == ZBotDifficulty.Easy, "Snapshots carry bot difficulty and omit connection identities");
        check(!guest.Receive(oldView), "A delayed snapshot cannot roll the guest back");
        var forged = Encoding.UTF8.GetBytes("{\"Kind\":1,\"Revision\":" + room.Revision + ",\"Ready\":true,\"Sender\":\"host-link\"}");
        check(guestPeer.Send(new DuelPacket(DuelMessage.RoomCommand, forged)) && DeliverHost() && DeliverGuest() &&
            guest.View.Humans.Single(member => member.Local).Ready && !guest.View.Humans.Single(member => member.Host).Ready,
            "A request acts only on its authenticated connection, even with a forged sender field");
        check(!host.Receive(new DuelPacket(DuelMessage.RoomCommand, Encoding.UTF8.GetBytes("{"))) &&
            !host.Receive(new DuelPacket(DuelMessage.RoomCommand, new byte[MultiplayerRoomPackets.MaxCommandBytes + 1])), "Malformed and oversized room requests are rejected");

        room.SetReady("host-link", room.Revision, true);
        room.Launch("host-link");
        room.MarkLoaded("host-link", room.Revision);
        check(host.Publish() && DeliverGuest() && !guest.View.MatchLoaded, "The guest sees that the host is waiting for loading");
        check(guest.MarkLoaded() && DeliverHost() && DeliverGuest() && guest.View.MatchLoaded, "The guest acknowledgment releases the loading barrier on both sides");

        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var settings = File.ReadAllText(Path.Combine(root, "source/Assets/OpenZ/Resources/OpenZ/default_settings.txt"));
        var grid = File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps/SMP03M01_grid.json"));
        var world = ZWorld.Create(settings, grid, 12);
        var replica = ZWorld.Create(settings, grid, 12);
        guest.AttachBattlefield(replica);
        room.ConfigureFreeForAllWorld(world, 12);
        var red = room.HumanTeam("host-link"); var blue = room.HumanTeam("guest-link");
        check(red == Team.Red && blue == Team.Blue && world.Bots.Single().Team == Team.Green &&
            world.Bots.Single().Difficulty == ZBotDifficulty.Easy, "The authoritative P03 setup produces two human forces and one green bot");
        host.AttachBattlefield(world);
        check(host.PublishBattlefield() && DeliverGuest() && replica.DuelIsReplica && replica.Bots.Count == 0 &&
            replica.Objects.Count(o => o.IsFort && o.Owner == Team.Green) == 1, "The guest receives Green's battlefield state without running a bot locally");
        var unit = world.Objects.First(objectOnMap => objectOnMap.Owner == blue && objectOnMap.IsMobile && !objectOnMap.IsMinion);
        var enemyUnit = world.Objects.First(objectOnMap => objectOnMap.Owner == red && objectOnMap.IsMobile && !objectOnMap.IsMinion);
        var order = new[] { new ZWaypoint { mode = WpMode.Move, refId = -1, x = unit.CenterX + 16, y = unit.CenterY } };
        var move = new DuelOrder { Kind = DuelOrderKind.Waypoints, ObjectId = unit.Id, Points = order };
        check(guest.SendOrder(new DuelOrder { Kind = DuelOrderKind.Stop, ObjectId = enemyUnit.Id }) && !DeliverHost(),
            "Blue cannot send a command to a Red unit through the room link");
        check(guest.SendOrder(move) && DeliverHost() && unit.Waypoints.Count > 0, "The guest's Blue order reaches the authoritative battlefield");
        var previousMatchOrder = MultiplayerRoomPackets.Order(new RoomOrder { Revision = room.Revision, Order = move });
        check(!host.Receive(previousMatchOrder), "An order sequence cannot be replayed");
        var greenFort = world.Objects.Single(o => o.IsFort && o.Owner == Team.Green);
        greenFort.SetHealth(0);
        world.UpdateObjectHealth(greenFort);
        world.Tick(ZConst.TICK);
        var elimination = new ZEvent { type = ZEventType.TeamEliminated, objId = greenFort.Id, value = (float)Team.Green };
        check(host.PublishBattlefield(events: new[] { elimination }) && DeliverGuest() &&
            replica.SkirmishEliminated(Team.Green) && !replica.GameOver && replica.Events.Contains(elimination),
            "The guest receives Green's elimination state and event while the two human forces continue playing");
        var corruptBattlefield = new byte[16];
        BitConverter.GetBytes(room.Revision).CopyTo(corruptBattlefield, 0);
        check(!guest.Receive(new DuelPacket(DuelMessage.RoomBattlefield, corruptBattlefield)) &&
            host.PublishBattlefield(true) && DeliverGuest() && replica.SkirmishEliminated(Team.Green),
            "After malformed battlefield data the guest can recover from a full snapshot");
        room.ReturnToLobby("host-link");
        check(!host.Receive(previousMatchOrder), "Returning to the lobby rejects old battlefield traffic");

        room.SelectMap("host-link", "SMP08M01");
        for (int seat = 2; seat < room.Starts; seat++) room.SetBot("host-link", seat, seat, ZBotDifficulty.Normal);
        check(host.Publish() && DeliverGuest() && guest.View.Starts == 8 && guest.View.Bots.Length == 6,
            "The same connection can switch to an eight-start room with six bots");
        room.SetReady("host-link", room.Revision, true);
        check(guest.SetReady(true) && DeliverHost() && DeliverGuest() && guest.View.CanLaunch,
            "The eight-start map requires both humans and all six bots");
        room.Launch("host-link");
        check(host.Publish() && DeliverGuest(), "The guest receives the new launch revision");
        var largeGrid = File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps/SMP08M01_grid.json"));
        var largeWorld = ZWorld.Create(settings, largeGrid, 12);
        var largeReplica = ZWorld.Create(settings, largeGrid, 12);
        guest.AttachBattlefield(largeReplica);
        room.MarkLoaded("host-link", room.Revision);
        check(guest.MarkLoaded() && DeliverHost() && DeliverGuest(), "The eight-start battlefield uses the same load barrier");
        room.ConfigureFreeForAllWorld(largeWorld, 12);
        host.AttachBattlefield(largeWorld);
        check(host.PublishBattlefield() && DeliverGuest() && largeReplica.Objects.Count(o => o.IsFort) == 8 &&
            largeReplica.Bots.Count == 0 && largeWorld.Bots.Count == 6, "The largest opening battlefield fits on the wire and replicates all eight forts");
    }
}
