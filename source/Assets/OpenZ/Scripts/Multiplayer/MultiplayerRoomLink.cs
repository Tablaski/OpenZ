using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.Multiplayer
{
    internal enum RoomCommandKind { Ready = 1, Loaded = 2, Rename = 3 }

    [Serializable] internal sealed class RoomCommand
    {
        public RoomCommandKind Kind;
        public int Revision;
        public bool Ready;
        public string Name;
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
            public string Id, Name;
            public int Position, Alliance;
            public bool Observer, Ready, Host, Local;

            internal static Human FromMember(RoomMember member, string host, string local) => new Human
            {
                Id = member.Id, Name = member.Name, Position = member.Seat.Position, Alliance = member.Alliance,
                Observer = member.Seat.Role == MatchSeatRole.Observer, Ready = member.Ready,
                Host = member.Id == host, Local = member.Id == local,
            };
        }

        [Serializable] internal sealed class Bot
        {
            public int Position, Alliance;
            public Sim.ZBotDifficulty Difficulty;
        }

        public string Map;
        public int Revision, Sequence, Starts, Capacity;
        public MatchStartBlocker StartBlocker;
        public bool CanLaunch, Launched, MatchLoaded;
        public Human[] Humans;
        public Bot[] Bots;
        public int[] Closed;
        public SkirmishRules Rules;
    }

    internal static class MultiplayerRoomPackets
    {
        internal const int MaxCommandBytes = 256, MaxOrderBytes = 16384, MaxSnapshotBytes = 280000;
        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static DuelPacket Command(RoomCommand command) =>
            Encode(DuelMessage.RoomCommand, command, MaxCommandBytes);
        public static DuelPacket Snapshot(RoomSnapshot snapshot) =>
            Encode(DuelMessage.RoomSnapshot, snapshot, MaxSnapshotBytes, compress: true);
        public static DuelPacket Order(RoomOrder order) => Encode(DuelMessage.RoomOrder, order, MaxOrderBytes);

        public static RoomCommand ReadCommand(DuelPacket packet) =>
            Decode<RoomCommand>(packet, DuelMessage.RoomCommand, MaxCommandBytes);
        public static RoomSnapshot ReadSnapshot(DuelPacket packet) =>
            Decode<RoomSnapshot>(packet, DuelMessage.RoomSnapshot, MaxSnapshotBytes);
        public static RoomOrder ReadOrder(DuelPacket packet) => Decode<RoomOrder>(packet, DuelMessage.RoomOrder, MaxOrderBytes);

        public static DuelPacket Battlefield(int revision, DuelDelta delta, IReadOnlyList<ZEvent> events, bool compress = false)
        {
            int length = DuelDeltaCodec.Measure(delta);
            using var stream = new MemoryStream(3 * sizeof(int) + length);
            using var writer = new BinaryWriter(stream);
            writer.Write(revision);
            writer.Write(length);
            DuelDeltaCodec.Write(writer, delta);
            DuelEventCodec.Write(writer, events ?? Array.Empty<ZEvent>());
            if (stream.Length > DuelWire.MaxPayload) throw new InvalidDataException("Room battlefield exceeds its size limit.");
            return new DuelPacket(DuelMessage.RoomBattlefield, stream.ToArray(), compress);
        }

        public static DuelDelta ReadBattlefield(DuelPacket packet, int revision, out ZEvent[] events)
        {
            events = null;
            var bytes = packet?.Payload;
            if (packet?.Kind != DuelMessage.RoomBattlefield || bytes == null || bytes.Length <= 2 * sizeof(int) || bytes.Length > DuelWire.MaxPayload)
                throw new InvalidDataException("Invalid room battlefield packet.");
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream);
            if (reader.ReadInt32() != revision) return null;
            int length = reader.ReadInt32();
            if (length <= 0 || length > stream.Length - stream.Position) throw new InvalidDataException("Invalid room battlefield delta length.");
            var delta = DuelDeltaCodec.Decode(bytes, (int)stream.Position, length);
            stream.Position += length;
            events = DuelEventCodec.Read(reader);
            if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected data after room battlefield events.");
            return delta;
        }

        static DuelPacket Encode(DuelMessage kind, object value, int maxBytes, bool compress = false)
        {
            var payload = StrictUtf8.GetBytes(JsonUtility.ToJson(value));
            if (payload.Length > maxBytes) throw new InvalidDataException("Room packet exceeds its size limit.");
            return new DuelPacket(kind, payload, compress);
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
        DuelReplica.CaptureBuffer capture;
        DuelDeltaSender deltaSender;
        bool sendFull;

        public MultiplayerRoomHostLink(MultiplayerRoom room, IDuelPeer peer, string authenticatedGuest, int initialSequence = 0)
        {
            this.room = room ?? throw new ArgumentNullException(nameof(room));
            this.peer = peer ?? throw new ArgumentNullException(nameof(peer));
            guest = authenticatedGuest;
            sequence = initialSequence;
            if (guest == room.HostId || !room.Members.Any(member => member.Id == guest))
                throw new ArgumentException("The connection must belong to a joined guest.", nameof(authenticatedGuest));
        }

        public bool Publish()
        {
            var view = new RoomSnapshot
            {
                Map = room.Map, Starts = room.Starts, Capacity = room.Capacity, Revision = room.Revision, Sequence = ++sequence,
                StartBlocker = room.StartBlocker, CanLaunch = room.CanLaunch, Launched = room.Launched, MatchLoaded = room.MatchLoaded,
                Humans = room.Members.Select(member => RoomSnapshot.Human.FromMember(member, room.HostId, guest)).ToArray(),
                Bots = room.Bots.Select(bot => new RoomSnapshot.Bot
                { Position = bot.Position, Alliance = bot.Alliance, Difficulty = bot.Difficulty }).ToArray(),
                Rules = room.Rules, Closed = room.ClosedPositions,
            };
            return peer.Send(MultiplayerRoomPackets.Snapshot(view));
        }

        public bool Receive(DuelPacket packet, bool publish = true)
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
                    case RoomCommandKind.Rename: room.Rename(guest, command.Name); break;
                    default: return false;
                }
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (InvalidDataException) { return false; }
            if (publish) Publish();
            return true;
        }

        public void AttachBattlefield(ZWorld world)
        {
            if (!room.MatchLoaded || world == null || OzAiMapCatalog.FindSkirmishMap(room.Map)?.File != world.Map.file || !world.Map.skirmish)
                throw new InvalidOperationException("A launched room needs its own loaded battlefield.");
            if (battlefieldRevision == room.Revision) throw new InvalidOperationException("The battlefield for this launch is already attached.");
            guestTeam = room.HumanTeam(guest);
            orders = guestTeam == Team.Neutral ? null : new DuelOrderReceiver(world, guestTeam);
            battlefield = world;
            battlefieldRevision = room.Revision;
            deltaSender = null;
            sendFull = true;
        }

        public bool PublishBattlefield(bool full = false, IReadOnlyList<ZEvent> events = null)
        {
            if (battlefield == null || !room.MatchLoaded || room.Revision != battlefieldRevision) return false;
            capture ??= new DuelReplica.CaptureBuffer();
            deltaSender ??= new DuelDeltaSender();
            var delta = deltaSender.CaptureImmutable(capture.Capture(battlefield), sendFull || full);
            sendFull = !peer.Send(MultiplayerRoomPackets.Battlefield(battlefieldRevision, delta, events));
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
            if (incoming.Starts < 2 || incoming.Starts > 8 || incoming.Capacity < 2 || incoming.Capacity > incoming.Starts ||
                incoming.Revision < 0 || incoming.Sequence <= 0 ||
                string.IsNullOrEmpty(incoming.Map) || incoming.Humans == null || incoming.Bots == null ||
                OzAiMapCatalog.FindSkirmishMap(incoming.Map)?.Starts != incoming.Starts || incoming.Humans.Length > 8 || incoming.Bots.Length > incoming.Starts ||
                incoming.Rules?.Valid != true || incoming.Closed == null || incoming.Closed.Any(position => position < 0 || position >= incoming.Starts) ||
                incoming.Humans.Any(member => member == null || member.Position < -1 || member.Position >= incoming.Starts) ||
                incoming.Bots.Any(bot => bot == null || bot.Position < 0 || bot.Position >= incoming.Starts ||
                    !Sim.ZAiDifficulty.IsValid(bot.Difficulty)) ||
                incoming.Humans.Count(member => member.Local) != 1 || incoming.Humans.Count(member => member.Host) != 1 ||
                (View != null && (incoming.Sequence <= View.Sequence || incoming.Revision < View.Revision))) return false;
            View = incoming;
            return true;
        }

        public void AttachBattlefield(ZWorld world)
        {
            if (View?.Launched != true || world == null || OzAiMapCatalog.FindSkirmishMap(View.Map)?.File != world.Map.file || battlefieldRevision == View.Revision)
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
                var delta = MultiplayerRoomPackets.ReadBattlefield(packet, battlefieldRevision, out var events);
                if (delta == null || !deltaReceiver.ApplyOwned(delta)) return false;
                DuelReplica.ApplyDelta(battlefield, delta);
                battlefield.Events.AddRange(events);
                return true;
            }
            catch (InvalidDataException) { deltaReceiver = new DuelDeltaReceiver(); return false; }
            catch (IOException) { deltaReceiver = new DuelDeltaReceiver(); return false; }
            catch (ArgumentException) { deltaReceiver = new DuelDeltaReceiver(); return false; }
        }

        public bool SetReady(bool ready) => Send(RoomCommandKind.Ready, ready, false);
        public bool MarkLoaded() => Send(RoomCommandKind.Loaded, false, true);
        public bool Rename(string name) => View?.Launched == false && peer.Send(MultiplayerRoomPackets.Command(
            new RoomCommand { Kind = RoomCommandKind.Rename, Name = DuelLobby.NormalizeName(name) }));

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
            if (local == null || View.Launched != launched ||
                (local.Observer ? kind != RoomCommandKind.Loaded : local.Position < 0)) return false;
            return peer.Send(MultiplayerRoomPackets.Command(new RoomCommand { Kind = kind, Revision = View.Revision, Ready = ready }));
        }
    }
}
