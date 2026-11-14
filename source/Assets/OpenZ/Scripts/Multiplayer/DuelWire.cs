using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpenZ.Multiplayer
{
    public enum DuelMessage : ushort
    {
        Hello = 1, Welcome, Lobby, ChangeRules, Ready, Chat, Load, Loaded, Start,
        Command, Delta, Ping, Pong, Disconnect, Surrender
    }

    public sealed class DuelPacket
    {
        public readonly DuelMessage Kind;
        public readonly byte[] Payload;
        public DuelPacket(DuelMessage kind, byte[] payload) { Kind = kind; Payload = payload; }
    }

    /// <summary>
    /// Versioned, bounded framing for reliable ordered byte streams (TCP initially).
    /// Wire order is little endian. Packet payload codecs and simulation remain separate.
    /// One read loop per stream; writes are serialized by DuelStreamConnection.
    /// </summary>
    public static class DuelWire
    {
        public const int Version = 1, MaxPayload = 4 * 1024 * 1024;
        const uint Magic = 0x31445A4F; // OZ D1

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
