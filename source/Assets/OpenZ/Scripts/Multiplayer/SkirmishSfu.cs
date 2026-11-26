using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OpenZ.Multiplayer
{
    // The host has one SFU publication, irrespective of the number of subscribers.
    internal sealed class SkirmishSfu : IDisposable
    {
        const int FrameChunk = 12000, QueueLimit = 2 * DuelWire.MaxPayload + 64, NativeQueueLimit = 64 * 1024;
        const int StateRate = SkirmishNetworkTuning.SfuStateTarget * 2, StateBurst = StateRate * 2;
        static readonly ConcurrentDictionary<int, SkirmishSfu> Connections = new ConcurrentDictionary<int, SkirmishSfu>();
        static readonly DuelRtcNative.GatheringCallback Gathering = OnGathering, StateChange = OnStateChange;
        readonly object gate = new object();
        readonly Dictionary<int, Subscription> subscriptions = new Dictionary<int, Subscription>();
        readonly ConcurrentQueue<(int target, DuelPacket packet)> outgoing = new ConcurrentQueue<(int, DuelPacket)>();
        readonly byte[] receive = new byte[65536];
        readonly byte[] chunk = new byte[FrameChunk + sizeof(int)];
        readonly int localId;
        int pc = -1, setup = -1, game = -1, backlog, buffered;
        volatile int connectionState;
        volatile bool gathered, failed;
        byte[] writing;
        int target, offset, stateBytes;
        long bytesSent;
        double stateTokens = StateBurst;
        long refillAt = Stopwatch.GetTimestamp();

        public SkirmishSfu(int localId, string[] servers = null)
        {
            this.localId = localId;
            try
            {
                pc = DuelRtcNative.CreateConnection(servers ?? Array.Empty<string>());
                Connections[pc] = this;
                DuelRtcNative.Check(DuelRtcNative.rtcSetGatheringStateChangeCallback(pc, Gathering));
                DuelRtcNative.Check(DuelRtcNative.rtcSetStateChangeCallback(pc, StateChange));
            }
            catch { Dispose(); throw; }
        }

        public bool Failed => failed;
        public int PendingBytes => Volatile.Read(ref backlog) + Volatile.Read(ref buffered);
        public long StateBytesSent => Interlocked.Read(ref bytesSent);

#if UNITY_5_3_OR_NEWER
        [AOT.MonoPInvokeCallback(typeof(DuelRtcNative.GatheringCallback))]
#endif
        static void OnGathering(int id, int state, IntPtr _) { if (state == 2 && Connections.TryGetValue(id, out var link)) link.gathered = true; }

#if UNITY_5_3_OR_NEWER
        [AOT.MonoPInvokeCallback(typeof(DuelRtcNative.GatheringCallback))]
#endif
        static void OnStateChange(int id, int state, IntPtr _) { if (Connections.TryGetValue(id, out var link)) link.connectionState = state; }

        public async Task<string> Answer(string offer, int setupId, CancellationToken cancel)
        {
            if (string.IsNullOrEmpty(offer) || offer.Length > 60000) throw new IOException("Invalid SFU connection offer.");
            DuelRtcNative.Check(DuelRtcNative.rtcSetRemoteDescription(pc, offer, "offer"));
            setup = Create("server-events", setupId);
            DuelRtcNative.Check(DuelRtcNative.rtcSetLocalDescription(pc, "answer"));
            var sdp = new StringBuilder(65536);
            while (!gathered) await Task.Delay(20, cancel).ConfigureAwait(false);
            DuelRtcNative.Check(DuelRtcNative.rtcGetLocalDescription(pc, sdp, sdp.Capacity));
            return sdp.ToString();
        }

        public async Task WaitConnected(CancellationToken cancel)
        {
            while (true)
            {
                int state = connectionState;
                if (state == 2) return;
                if (state >= 3) throw new IOException("The SFU connection could not be established.");
                await Task.Delay(20, cancel).ConfigureAwait(false);
            }
        }

        public void Publish(int gameId)
        {
            lock (gate)
            {
                game = Create("openz-game", gameId);
            }
        }

        public Subscription Subscribe(int id, int gameId)
        {
            lock (gate)
            {
                int channel = Create("openz-game", gameId);
                try
                {
                    var result = new Subscription(channel);
                    subscriptions.Add(id, result);
                    return result;
                }
                catch { Delete(channel); throw; }
            }
        }

        public void Unsubscribe(int id)
        {
            lock (gate)
            {
                if (!subscriptions.TryGetValue(id, out var subscription)) return;
                subscriptions.Remove(id);
                Delete(subscription.Game);
                subscription.Closed = true;
            }
        }

        int Create(string label, int stream)
        {
            if (stream < 0 || stream >= ushort.MaxValue) throw new IOException("Invalid SFU channel allocation.");
            var init = new DuelRtcNative.ChannelInit { Negotiated = true, ManualStream = true, Stream = (ushort)stream };
            return DuelRtcNative.Check(DuelRtcNative.rtcCreateDataChannelEx(pc, label, ref init));
        }

        public bool Send(int to, DuelPacket packet)
        {
            if (Failed || packet?.Payload == null || packet.Payload.Length > DuelWire.MaxPayload) return false;
            int length = packet.Payload.Length + 12;
            if (Interlocked.Add(ref backlog, length) <= QueueLimit)
            {
                outgoing.Enqueue((to, packet));
                return true;
            }
            Interlocked.Add(ref backlog, -length);
            return false;
        }

        public async Task Pump(CancellationToken cancel)
        {
            while (!cancel.IsCancellationRequested)
            {
                lock (gate)
                {
                    if (pc < 0) return;
                    try { Transfer(cancel); }
                    catch (Exception error) when (error is IOException || error is InvalidOperationException)
                    { failed = true; return; }
                }
                await Task.Delay(5, cancel).ConfigureAwait(false);
            }
        }

        void Transfer(CancellationToken cancel)
        {
            if (connectionState >= 3 || (game >= 0 && DuelRtcNative.rtcIsClosed(game)))
                throw new IOException("The SFU connection closed.");
            bool published = game >= 0 && DuelRtcNative.rtcIsOpen(game);
            if (published)
            {
                long now = Stopwatch.GetTimestamp();
                stateTokens = Math.Min(StateBurst, stateTokens + (now - refillAt) * (StateRate / (double)Stopwatch.Frequency));
                refillAt = now;
                int nativeBytes = DuelRtcNative.Check(DuelRtcNative.rtcGetBufferedAmount(game));
                for (int writes = 0; nativeBytes < NativeQueueLimit && writes < 16; writes++)
                {
                    if (writing == null)
                    {
                        if (!outgoing.TryDequeue(out var item)) break;
                        var packet = DuelWire.PackDelta(item.packet);
                        using var buffer = new MemoryStream();
                        // MemoryStream writes complete synchronously.
                        DuelWire.WriteAsync(buffer, packet, cancel).GetAwaiter().GetResult();
                        writing = buffer.ToArray();
                        target = item.target;
                        offset = 0;
                        stateBytes = DuelWire.IsDelta(item.packet.Kind) ? writing.Length : 0;
                        Interlocked.Add(ref backlog, -item.packet.Payload.Length - 12);
                    }
                    int length = Math.Min(FrameChunk, writing.Length - offset);
                    int egress = stateBytes == 0 ? 0 : (length + sizeof(int)) * Math.Max(1, subscriptions.Count);
                    if (stateTokens < egress) break;
                    stateTokens -= egress;
                    chunk[0] = (byte)target;
                    chunk[1] = (byte)(target >> 8);
                    chunk[2] = (byte)(target >> 16);
                    chunk[3] = (byte)(target >> 24);
                    Buffer.BlockCopy(writing, offset, chunk, sizeof(int), length);
                    DuelRtcNative.Check(DuelRtcNative.rtcSendMessage(game, chunk, length + sizeof(int)));
                    nativeBytes += length + sizeof(int);
                    offset += length;
                    if (offset == writing.Length)
                    {
                        writing = null;
                        Interlocked.Add(ref bytesSent, stateBytes);
                    }
                }
                Volatile.Write(ref buffered, nativeBytes + (writing == null ? 0 : writing.Length - offset));
            }
            foreach (var subscription in subscriptions.Values)
            {
                if (subscription.Closed) continue;
                if (DuelRtcNative.rtcIsClosed(subscription.Game)) { subscription.Closed = true; continue; }
                subscription.Ready = published && DuelRtcNative.rtcIsOpen(subscription.Game);
                if (!subscription.Ready) continue;
                ReadGame(subscription);
            }
        }

        void ReadGame(Subscription subscription)
        {
            for (int i = 0; i < 32; i++)
            {
                int count = receive.Length;
                int result = DuelRtcNative.rtcReceiveMessage(subscription.Game, receive, ref count);
                if (result == -3) return;
                DuelRtcNative.Check(result);
                if (count <= sizeof(int)) throw new InvalidDataException("Invalid SFU game frame.");
                int to = BitConverter.ToInt32(receive, 0);
                if (to != 0 && to != localId) continue;
                foreach (var packet in subscription.Decoder.Add(receive, count - sizeof(int), sizeof(int)))
                {
                    if (localId == 1 && (packet.Kind == DuelMessage.RoomBattlefieldCompressed || DuelWire.IsDelta(packet.Kind))) continue;
                    var unpacked = DuelWire.UnpackDelta(packet);
                    unpacked.ReceivedAtTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                    if (Interlocked.Add(ref subscription.QueuedBytes, unpacked.Payload.Length + 12) > QueueLimit)
                        throw new IOException("The SFU receive queue is full.");
                    subscription.Packets.Enqueue(unpacked);
                }
            }
        }

        static void Delete(int channel) { if (channel >= 0) DuelRtcNative.rtcDeleteDataChannel(channel); }

        public void Dispose()
        {
            lock (gate)
            {
                if (pc < 0) return;
                failed = true;
                Connections.TryRemove(pc, out _);
                foreach (var subscription in subscriptions.Values)
                {
                    subscription.Closed = true;
                    Delete(subscription.Game);
                }
                Delete(game);
                Delete(setup);
                DuelRtcNative.rtcClosePeerConnection(pc);
                DuelRtcNative.rtcDeletePeerConnection(pc);
                pc = -1;
            }
        }

        internal sealed class Subscription
        {
            internal readonly int Game;
            internal readonly ConcurrentQueue<DuelPacket> Packets = new ConcurrentQueue<DuelPacket>();
            internal readonly DuelRtcFrameDecoder Decoder = new DuelRtcFrameDecoder();
            internal volatile bool Ready, Closed;
            internal int QueuedBytes;

            internal Subscription(int game) { Game = game; }

            internal bool TryReceive(out DuelPacket packet)
            {
                if (!Packets.TryDequeue(out packet)) return false;
                Interlocked.Add(ref QueuedBytes, -packet.Payload.Length - 12);
                return true;
            }
        }
    }
}
