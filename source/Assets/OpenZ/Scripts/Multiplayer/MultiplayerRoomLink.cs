using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.Multiplayer
{
    internal enum RoomCommandKind { Ready = 1, Loaded = 2 }

    [Serializable] internal sealed class RoomCommand
    {
        public RoomCommandKind Kind;
        public int Revision;
        public bool Ready;
    }

    [Serializable] internal sealed class RoomOrder
    {
        public int Revision;
        public DuelOrder Order;
    }

    [Serializable] internal sealed class RoomSnapshot
    {
        [Serializable] internal sealed class Human
        {
            public string Name;
            public int Position, Alliance;
            public bool Observer, Ready, Host, Local;
        }

        [Serializable] internal sealed class Bot
        {
            public int Position, Alliance;
            public Sim.ZBotDifficulty Difficulty;
        }

        public string Map;
        public int Revision, Sequence, Starts;
        public MatchStartBlocker StartBlocker;
        public bool CanLaunch, Launched, MatchLoaded;
        public Human[] Humans;
        public Bot[] Bots;
    }

    internal static class MultiplayerRoomPackets
    {
        internal const int MaxCommandBytes = 256, MaxSnapshotBytes = 16384;
        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static DuelPacket Command(RoomCommand command) =>
            Encode(DuelMessage.RoomCommand, command, MaxCommandBytes);
        public static DuelPacket Snapshot(RoomSnapshot snapshot) =>
            Encode(DuelMessage.RoomSnapshot, snapshot, MaxSnapshotBytes);
        public static DuelPacket Order(RoomOrder order) => Encode(DuelMessage.RoomOrder, order, MaxSnapshotBytes);

        public static RoomCommand ReadCommand(DuelPacket packet) =>
            Decode<RoomCommand>(packet, DuelMessage.RoomCommand, MaxCommandBytes);
        public static RoomSnapshot ReadSnapshot(DuelPacket packet) =>
            Decode<RoomSnapshot>(packet, DuelMessage.RoomSnapshot, MaxSnapshotBytes);
        public static RoomOrder ReadOrder(DuelPacket packet) => Decode<RoomOrder>(packet, DuelMessage.RoomOrder, MaxSnapshotBytes);

        public static DuelPacket Battlefield(int revision, DuelDelta delta)
        {
            var payload = DuelDeltaCodec.Encode(delta);
            if (payload.Length > DuelWire.MaxPayload - sizeof(int)) throw new InvalidDataException("Room battlefield exceeds its size limit.");
            var framed = new byte[sizeof(int) + payload.Length];
            BinaryPrimitives.WriteInt32LittleEndian(framed, revision);
            payload.CopyTo(framed, sizeof(int));
            return new DuelPacket(DuelMessage.RoomBattlefield, framed);
        }

        public static DuelDelta ReadBattlefield(DuelPacket packet, int revision)
        {
            var bytes = packet?.Payload;
            if (packet?.Kind != DuelMessage.RoomBattlefield || bytes == null || bytes.Length <= sizeof(int) || bytes.Length > DuelWire.MaxPayload)
                throw new InvalidDataException("Invalid room battlefield packet.");
            if (BinaryPrimitives.ReadInt32LittleEndian(bytes) != revision) return null;
            return DuelDeltaCodec.Decode(bytes.AsSpan(sizeof(int)).ToArray());
        }

        static DuelPacket Encode(DuelMessage kind, object value, int maxBytes)
        {
            var payload = StrictUtf8.GetBytes(JsonUtility.ToJson(value));
            if (payload.Length > maxBytes) throw new InvalidDataException("Room packet exceeds its size limit.");
            return new DuelPacket(kind, payload);
        }

        static T Decode<T>(DuelPacket packet, DuelMessage kind, int maxBytes) where T : class
        {
            if (packet?.Kind != kind || packet.Payload == null || packet.Payload.Length == 0 || packet.Payload.Length > maxBytes)
                throw new InvalidDataException("Invalid room packet kind or length.");
            try { return JsonUtility.FromJson<T>(StrictUtf8.GetString(packet.Payload)) ?? throw new InvalidDataException("Missing room data."); }
            catch (ArgumentException error) { throw new InvalidDataException("Invalid room data.", error); }
        }
    }

    /// <summary>Connects one authenticated guest to a room on the match thread.</summary>
    internal sealed class MultiplayerRoomHostLink
    {
        readonly MultiplayerRoom room;
        readonly IDuelPeer peer;
        readonly string guest;
        int sequence;
        int battlefieldRevision = -1;
        ZWorld battlefield;
        Team guestTeam;
        DuelOrderReceiver orders;
        readonly DuelReplica.CaptureBuffer capture = new DuelReplica.CaptureBuffer();
        DuelDeltaSender deltaSender;
        bool sendFull;

        public MultiplayerRoomHostLink(MultiplayerRoom room, IDuelPeer peer, string authenticatedGuest)
        {
            this.room = room ?? throw new ArgumentNullException(nameof(room));
            this.peer = peer ?? throw new ArgumentNullException(nameof(peer));
            guest = authenticatedGuest;
            if (guest == room.HostId || !room.Members.Any(member => member.Id == guest))
                throw new ArgumentException("The connection must belong to a joined guest.", nameof(authenticatedGuest));
        }

        public bool Publish()
        {
            var view = new RoomSnapshot
            {
                Map = room.Map, Starts = room.Starts, Revision = room.Revision, Sequence = ++sequence,
                StartBlocker = room.StartBlocker, CanLaunch = room.CanLaunch, Launched = room.Launched, MatchLoaded = room.MatchLoaded,
                Humans = room.Members.Select(member => new RoomSnapshot.Human
                {
                    Name = member.Name, Position = member.Seat.Position, Alliance = member.Alliance,
                    Observer = member.Seat.Role == MatchSeatRole.Observer, Ready = member.Ready,
                    Host = member.Id == room.HostId, Local = member.Id == guest,
                }).ToArray(),
                Bots = room.Bots.Select(bot => new RoomSnapshot.Bot
                { Position = bot.Position, Alliance = bot.Alliance, Difficulty = bot.Difficulty }).ToArray(),
            };
            return peer.Send(MultiplayerRoomPackets.Snapshot(view));
        }

        public bool Receive(DuelPacket packet)
        {
            try
            {
                if (packet?.Kind == DuelMessage.RoomOrder)
                {
                    if (orders == null || !room.MatchLoaded || room.Revision != battlefieldRevision || battlefield.SkirmishEliminated(guestTeam)) return false;
                    var order = MultiplayerRoomPackets.ReadOrder(packet);
                    return order.Revision == battlefieldRevision && orders.Execute(order.Order);
                }
                var command = MultiplayerRoomPackets.ReadCommand(packet);
                switch (command.Kind)
                {
                    case RoomCommandKind.Ready: room.SetReady(guest, command.Revision, command.Ready); break;
                    case RoomCommandKind.Loaded: room.MarkLoaded(guest, command.Revision); break;
                    default: return false;
                }
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (InvalidDataException) { return false; }
            Publish();
            return true;
        }

        public void AttachBattlefield(ZWorld world)
        {
            if (!room.MatchLoaded || world == null || OzAiMapCatalog.Find(room.Map)?.File != world.Map.file || !world.Map.skirmish)
                throw new InvalidOperationException("A launched room needs its own loaded battlefield.");
            if (battlefieldRevision == room.Revision) throw new InvalidOperationException("The battlefield for this launch is already attached.");
            guestTeam = room.HumanTeam(guest);
            orders = guestTeam == Team.Neutral ? null : new DuelOrderReceiver(world, guestTeam);
            battlefield = world;
            battlefieldRevision = room.Revision;
            deltaSender = new DuelDeltaSender();
            sendFull = true;
        }

        public bool PublishBattlefield(bool full = false)
        {
            if (battlefield == null || !room.MatchLoaded || room.Revision != battlefieldRevision) return false;
            var delta = deltaSender.CaptureImmutable(capture.Capture(battlefield), sendFull || full);
            sendFull = !peer.Send(MultiplayerRoomPackets.Battlefield(battlefieldRevision, delta));
            return !sendFull;
        }
    }

    internal sealed class MultiplayerRoomGuestLink
    {
        readonly IDuelPeer peer;
        long orderSequence;
        ZWorld battlefield;
        int battlefieldRevision = -1;
        DuelDeltaReceiver deltaReceiver;
        public RoomSnapshot View { get; private set; }

        public MultiplayerRoomGuestLink(IDuelPeer peer) => this.peer = peer ?? throw new ArgumentNullException(nameof(peer));

        public bool Receive(DuelPacket packet)
        {
            if (packet?.Kind == DuelMessage.RoomBattlefield) return ReceiveBattlefield(packet);
            RoomSnapshot incoming;
            try { incoming = MultiplayerRoomPackets.ReadSnapshot(packet); }
            catch (InvalidDataException) { return false; }
            if (incoming.Starts < 2 || incoming.Starts > 8 || incoming.Revision < 0 || incoming.Sequence <= 0 ||
                string.IsNullOrEmpty(incoming.Map) || incoming.Humans == null || incoming.Bots == null ||
                incoming.Humans.Any(member => member == null) || incoming.Bots.Any(bot => bot == null) ||
                incoming.Humans.Count(member => member.Local) != 1 || incoming.Humans.Count(member => member.Host) != 1 ||
                (View != null && (incoming.Sequence <= View.Sequence || incoming.Revision < View.Revision))) return false;
            View = incoming;
            return true;
        }

        public void AttachBattlefield(ZWorld world)
        {
            if (View?.Launched != true || world == null || OzAiMapCatalog.Find(View.Map)?.File != world.Map.file || battlefieldRevision == View.Revision)
                throw new InvalidOperationException("A launched room needs a fresh replica of its own battlefield.");
            world.ConfigureSkirmishReplica();
            battlefield = world;
            battlefieldRevision = View.Revision;
            deltaReceiver = new DuelDeltaReceiver();
        }

        bool ReceiveBattlefield(DuelPacket packet)
        {
            if (battlefield == null || View?.MatchLoaded != true || View.Revision != battlefieldRevision) return false;
            try
            {
                var delta = MultiplayerRoomPackets.ReadBattlefield(packet, battlefieldRevision);
                if (!deltaReceiver.ApplyOwned(delta)) return false;
                DuelReplica.ApplyDelta(battlefield, delta);
                return true;
            }
            catch (InvalidDataException) { deltaReceiver = new DuelDeltaReceiver(); return false; }
            catch (IOException) { deltaReceiver = new DuelDeltaReceiver(); return false; }
            catch (ArgumentException) { deltaReceiver = new DuelDeltaReceiver(); return false; }
        }

        public bool SetReady(bool ready) => Send(RoomCommandKind.Ready, ready, false);
        public bool MarkLoaded() => Send(RoomCommandKind.Loaded, false, true);

        public bool SendOrder(DuelOrder order)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            var local = View?.Humans.FirstOrDefault(member => member.Local);
            if (local == null || local.Observer || local.Position < 0 || !View.MatchLoaded) return false;
            order.Sequence = ++orderSequence;
            return peer.Send(MultiplayerRoomPackets.Order(new RoomOrder { Revision = View.Revision, Order = order }));
        }

        bool Send(RoomCommandKind kind, bool ready, bool launched)
        {
            var local = View?.Humans.FirstOrDefault(member => member.Local);
            if (local == null || local.Observer || local.Position < 0 || View.Launched != launched) return false;
            return peer.Send(MultiplayerRoomPackets.Command(new RoomCommand { Kind = kind, Revision = View.Revision, Ready = ready }));
        }
    }
}
