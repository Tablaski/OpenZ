using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace OpenZ.Multiplayer
{
    public enum DuelMessage : ushort
    {
        Hello = 1, Welcome, Lobby, ChangeRules, Ready, Chat, Load, Loaded, Start,
        Command, Delta, Ping, Pong, Disconnect, Surrender, RestartRequest, RestartReply, RestartBegin, RestartNotice, MatchStats, EndReady, ReturnLobby, ProposeRules, DiagnosticsReport,
        DeltaBinaryEvents, DeltaCompressed, Rename, MatchRequest, MatchRequestState, MatchReconnectState, MatchReconnectReady, MatchPause,
        RoomCommand, RoomSnapshot, RoomOrder, RoomBattlefield
    }

    public sealed class DuelPacket
    {
        public readonly DuelMessage Kind;
        public readonly byte[] Payload;
        internal readonly bool Compress;
        internal int WirePayloadLength;
        // Local transport timestamp. It is set before publishing a received delta to the game thread.
        internal long ReceivedAtTicks;
        public DuelPacket(DuelMessage kind, byte[] payload) : this(kind, payload, false) { }
        internal DuelPacket(DuelMessage kind, byte[] payload, bool compress)
        { Kind = kind; Payload = payload; Compress = compress; WirePayloadLength = payload?.Length ?? 0; }
    }

    /// <summary>
    /// Versioned, bounded framing for reliable ordered byte streams (TCP initially).
    /// Wire order is little endian. Packet payload codecs and simulation remain separate.
    /// One read loop per stream; writes are serialized by DuelStreamConnection.
    /// </summary>
    public static class DuelWire
    {
        public const int Version = 4, MaxPayload = 4 * 1024 * 1024;
        const uint Magic = 0x31445A4F; // OZ D1
        const int MinCompressedDelta = 1024, MaxCompressedDelta = 128 * 1024;
        const int CompressionHeader = 5, MinimumSavings = 32;

        internal static bool IsDelta(DuelMessage kind) => kind == DuelMessage.Delta || kind == DuelMessage.DeltaBinaryEvents;

        // Called from the ordered transport writers, outside the game thread.
        internal static DuelPacket PackDelta(DuelPacket packet)
        {
            int size = packet.Payload.Length;
            if (!packet.Compress || !IsDelta(packet.Kind) || size < MinCompressedDelta || size > MaxCompressedDelta) return packet;
            using (var buffer = new MemoryStream())
            using (var header = new BinaryWriter(buffer))
            {
                header.Write(packet.Kind == DuelMessage.DeltaBinaryEvents);
                header.Write(size);
                using (var compressor = new DeflateStream(buffer, CompressionLevel.Fastest, true))
                    compressor.Write(packet.Payload, 0, size);
                return buffer.Length <= size - MinimumSavings ? new DuelPacket(DuelMessage.DeltaCompressed, buffer.ToArray()) : packet;
            }
        }

        internal static DuelPacket UnpackDelta(DuelPacket packet)
        {
            if (packet.Kind != DuelMessage.DeltaCompressed) return packet;
            var body = packet.Payload;
            if (body.Length <= CompressionHeader || body.Length > MaxCompressedDelta || body[0] > 1)
                throw new InvalidDataException("Invalid compressed state header.");
            using (var buffer = new MemoryStream(body, false))
            using (var header = new BinaryReader(buffer))
            {
                var kind = header.ReadBoolean() ? DuelMessage.DeltaBinaryEvents : DuelMessage.Delta;
                int size = header.ReadInt32();
                if (size < MinCompressedDelta || size > MaxCompressedDelta || body.Length > size - MinimumSavings)
                    throw new InvalidDataException("Invalid compressed state length.");
                var payload = new byte[size];
                using (var decompressor = new DeflateStream(buffer, CompressionMode.Decompress))
                {
                    int offset = 0;
                    while (offset < size)
                    {
                        int count = decompressor.Read(payload, offset, size - offset);
                        if (count == 0) throw new InvalidDataException("Truncated compressed state.");
                        offset += count;
                    }
                    if (decompressor.ReadByte() != -1) throw new InvalidDataException("Oversized compressed state.");
                }
                return new DuelPacket(kind, payload) { WirePayloadLength = body.Length };
            }
        }

        public static async Task WriteAsync(Stream stream, DuelPacket packet, CancellationToken cancel)
        {
            if (stream == null || packet == null || packet.Payload == null) throw new ArgumentNullException();
            if (!Enum.IsDefined(typeof(DuelMessage), packet.Kind) || packet.Payload.Length > MaxPayload)
                throw new InvalidDataException("Invalid duel packet.");
            byte[] header;
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer))
            {
                writer.Write(Magic); writer.Write((ushort)Version); writer.Write((ushort)packet.Kind);
                writer.Write(packet.Payload.Length); header = buffer.ToArray();
            }
            await stream.WriteAsync(header, 0, header.Length, cancel).ConfigureAwait(false);
            await stream.WriteAsync(packet.Payload, 0, packet.Payload.Length, cancel).ConfigureAwait(false);
        }

        public static async Task<DuelPacket> ReadAsync(Stream stream, CancellationToken cancel)
        {
            var header = new byte[12];
            await ReadExactly(stream, header, cancel).ConfigureAwait(false);
            DuelMessage kind; int length;
            using (var buffer = new MemoryStream(header, false))
            using (var reader = new BinaryReader(buffer))
            {
                if (reader.ReadUInt32() != Magic || reader.ReadUInt16() != Version)
                    throw new InvalidDataException("Incompatible OpenZ duel protocol.");
                kind = (DuelMessage)reader.ReadUInt16(); length = reader.ReadInt32();
            }
            if (!Enum.IsDefined(typeof(DuelMessage), kind) || length < 0 || length > MaxPayload)
                throw new InvalidDataException("Invalid duel frame header.");
            var payload = new byte[length];
            await ReadExactly(stream, payload, cancel).ConfigureAwait(false);
            return new DuelPacket(kind, payload);
        }

        static async Task ReadExactly(Stream stream, byte[] buffer, CancellationToken cancel)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int count = await stream.ReadAsync(buffer, offset, buffer.Length - offset, cancel).ConfigureAwait(false);
                if (count == 0) throw new EndOfStreamException("Duel connection closed.");
                offset += count;
            }
        }
    }

    public sealed class DuelStreamConnection : IDisposable
    {
        readonly Stream stream;
        readonly SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);
        bool disposed;
        public DuelStreamConnection(Stream stream) { this.stream = stream ?? throw new ArgumentNullException(nameof(stream)); }
        public Task<DuelPacket> ReceiveAsync(CancellationToken cancel) => DuelWire.ReadAsync(stream, cancel);
        public async Task SendAsync(DuelPacket packet, CancellationToken cancel)
        {
            await writeLock.WaitAsync(cancel).ConfigureAwait(false);
            try
            {
                if (disposed) throw new ObjectDisposedException(nameof(DuelStreamConnection));
                // A partially written frame cannot be retried on this connection.
                try { await DuelWire.WriteAsync(stream, packet, cancel).ConfigureAwait(false); }
                catch { Dispose(); throw; }
            }
            finally { writeLock.Release(); }
        }
        public void Dispose() { disposed = true; stream.Dispose(); }
    }
}
