using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenZ.Multiplayer
{
    /// <summary>
    /// One authenticated remote seat. Socket tasks never touch Unity or the simulation.
    /// Bounded queues disconnect a stalled peer instead of retaining unlimited snapshots.
    /// The main thread owns lobby, authority and application of received packets.
    /// </summary>
    public sealed class DuelTcpPeer : IDuelPeer
    {
        const int QueueBudget = 8 * 1024 * 1024;
        readonly TcpClient socket;
        readonly DuelStreamConnection connection;
        readonly CancellationTokenSource stop = new CancellationTokenSource();
        readonly ConcurrentQueue<DuelPacket> incoming = new ConcurrentQueue<DuelPacket>(), outgoing = new ConcurrentQueue<DuelPacket>();
        readonly SemaphoreSlim wake = new SemaphoreSlim(0);
        int receivedBytes, queuedBytes, disposed;
        long sentBytes, readBytes;
        public string RemoteName { get; private set; }
        public string Failure { get; private set; }
        public bool Closed => Volatile.Read(ref disposed) != 0;
        public long BytesSent => Interlocked.Read(ref sentBytes);
        public long BytesReceived => Interlocked.Read(ref readBytes);
        public System.Net.IPAddress RemoteAddress => ((System.Net.IPEndPoint)socket.Client.RemoteEndPoint).Address;

        DuelTcpPeer(TcpClient socket)
        {
            this.socket = socket; socket.NoDelay = true;
            connection = new DuelStreamConnection(socket.GetStream());
        }

        public static string NewInvitationSecret()
        {
            var bytes = new byte[24];
            using (var random = System.Security.Cryptography.RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public static async Task<DuelTcpPeer> AcceptAsync(TcpListener listener, string secret, string fingerprint, CancellationToken cancel)
        {
            using (cancel.Register(() => listener.Stop()))
            {
                var socket = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                var peer = new DuelTcpPeer(socket);
                try
                {
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(8));
                        using (timeout.Token.Register(() => socket.Close()))
                        {
                            var hello = await peer.connection.ReceiveAsync(timeout.Token).ConfigureAwait(false);
                            if (hello.Kind != DuelMessage.Hello) throw new InvalidDataException("Expected duel handshake.");
                            string[] fields = DecodeHello(hello.Payload);
                            if (!SameSecret(secret, fields[0]) || fields[1] != fingerprint)
                                throw new InvalidDataException("Invitation or game version does not match.");
                            peer.RemoteName = fields[2];
                            await peer.connection.SendAsync(new DuelPacket(DuelMessage.Welcome, Array.Empty<byte>()), timeout.Token).ConfigureAwait(false);
                        }
                    }
                    peer.Start(); return peer;
                }
                catch { peer.Dispose(); throw; }
            }
        }

        public static async Task<DuelTcpPeer> ConnectAsync(string host, int port, string secret, string fingerprint, string name, CancellationToken cancel)
        {
            var socket = new TcpClient();
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    using (timeout.Token.Register(() => socket.Close()))
                    {
                        await socket.ConnectAsync(host, port).ConfigureAwait(false);
                        var peer = new DuelTcpPeer(socket);
                        await peer.connection.SendAsync(new DuelPacket(DuelMessage.Hello, EncodeHello(secret, fingerprint, name)), timeout.Token).ConfigureAwait(false);
                        var welcome = await peer.connection.ReceiveAsync(timeout.Token).ConfigureAwait(false);
                        if (welcome.Kind != DuelMessage.Welcome || welcome.Payload.Length != 0)
                            throw new InvalidDataException("Host rejected handshake.");
                        peer.Start(); return peer;
                    }
                }
            }
            catch { socket.Dispose(); throw; }
        }

        public bool Send(DuelPacket packet)
        {
            if (packet?.Payload == null || packet.Payload.Length > DuelWire.MaxPayload) return false;
            int size = packet.Payload.Length + 12;
            if (Closed) return false;
            if (Interlocked.Add(ref queuedBytes, size) > QueueBudget)
            { Fail("Outgoing queue exceeded; peer is too slow."); return false; }
            // Ownership of payload stays with the caller, so take a stable copy before queuing.
            outgoing.Enqueue(new DuelPacket(packet.Kind, (byte[])packet.Payload.Clone())); wake.Release(); return true;
        }

        public bool TryReceive(out DuelPacket packet)
        {
            if (!incoming.TryDequeue(out packet)) return false;
            Interlocked.Add(ref receivedBytes, -packet.Payload.Length - 12); return true;
        }
        void Start() { _ = ReadLoop(); _ = WriteLoop(); }
        async Task ReadLoop()
        {
            try
            {
                while (!Closed)
                {
                    var packet = await connection.ReceiveAsync(stop.Token).ConfigureAwait(false);
                    int size = packet.Payload.Length + 12;
                    if (Interlocked.Add(ref receivedBytes, size) > QueueBudget) throw new IOException("Incoming queue exceeded.");
                    Interlocked.Add(ref readBytes, size); incoming.Enqueue(packet);
                }
            }
            catch (Exception error) { if (!Closed) Fail(error.Message); }
        }
        async Task WriteLoop()
        {
            try
            {
                while (!Closed)
                {
                    await wake.WaitAsync(stop.Token).ConfigureAwait(false);
                    if (!outgoing.TryDequeue(out var packet)) continue;
                    await connection.SendAsync(packet, stop.Token).ConfigureAwait(false);
                    Interlocked.Add(ref queuedBytes, -packet.Payload.Length - 12);
                    Interlocked.Add(ref sentBytes, packet.Payload.Length + 12);
                }
            }
            catch (Exception error) { if (!Closed) Fail(error.Message); }
        }
        void Fail(string reason) { Failure = reason; Dispose(); }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            stop.Cancel(); socket.Dispose(); connection.Dispose();
            while (incoming.TryDequeue(out _)) { }
            while (outgoing.TryDequeue(out _)) { }
        }
        internal static byte[] EncodeHello(string secret, string fingerprint, string name)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(secret + "\n" + fingerprint + "\n" + name);
            DecodeHello(bytes); return bytes;
        }
        internal static string[] DecodeHello(byte[] bytes)
        {
            if (bytes.Length > 512) throw new InvalidDataException("Handshake too large.");
            var fields = new UTF8Encoding(false, true).GetString(bytes).Split('\n');
            if (fields.Length != 3 || fields[0].Length != 32 || fields[1].Length > 256 || string.IsNullOrWhiteSpace(fields[1]) ||
                string.IsNullOrWhiteSpace(fields[2]) || fields[2].Length > 32)
                throw new InvalidDataException("Invalid handshake.");
            foreach (string field in fields) foreach (char c in field)
                if (char.IsControl(c)) throw new InvalidDataException("Invalid handshake text.");
            return fields;
        }
        internal static bool SameSecret(string expected, string actual)
        {
            if (expected == null || actual.Length != expected.Length) return false;
            int difference = 0; for (int i = 0; i < expected.Length; i++) difference |= expected[i] ^ actual[i];
            return difference == 0;
        }
    }
}
