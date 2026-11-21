using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenZ.Multiplayer
{
    /// <summary>Encrypted WebRTC data channels: reliable ordered game frames and separate lossy voice.</summary>
    public sealed class DuelRtcPeer : IDuelPeer
    {
        const int Budget = 8 * 1024 * 1024, ChunkSize = 12000;
        static readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> Gatherings = new ConcurrentDictionary<int, TaskCompletionSource<bool>>();
        static readonly DuelRtcNative.GatheringCallback Gathering = OnGathering;
        readonly ConcurrentQueue<DuelPacket> incoming = new ConcurrentQueue<DuelPacket>(), outgoing = new ConcurrentQueue<DuelPacket>();
        readonly ConcurrentQueue<byte[]> voiceIn = new ConcurrentQueue<byte[]>(), voiceOut = new ConcurrentQueue<byte[]>();
        readonly CancellationTokenSource stop = new CancellationTokenSource();
        readonly TaskCompletionSource<bool> gathered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource<bool> authenticated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly string secret, fingerprint, name;
        readonly bool relayOnly;
        int pc = -1, game = -1, voice = -1, disposed, cleanupQueued, queuedBytes, receivedBytes, voiceInCount, voiceOutCount;
        Task pump;
        int nativeBuffered;
        long statePayloadBytesSent;
        public int PendingSendBytes => Volatile.Read(ref queuedBytes) + Volatile.Read(ref nativeBuffered);
        public int PendingReceiveBytes => Volatile.Read(ref receivedBytes);
        public long StatePayloadBytesSent => Interlocked.Read(ref statePayloadBytesSent);
        public string RemoteName { get; private set; }
        public string Failure { get; private set; }
        public bool UsesRelay { get; private set; }
        public bool Closed => Volatile.Read(ref disposed) != 0;
        public Task Ready => authenticated.Task;

        public DuelRtcPeer(string[] servers, bool relayOnly, string secret, string fingerprint, string name)
        {
            this.secret = secret; this.fingerprint = fingerprint; this.name = name; this.relayOnly = relayOnly;
            try
            {
                pc = DuelRtcNative.CreateConnection(servers, relayOnly);
                Gatherings[pc] = gathered;
                DuelRtcNative.Check(DuelRtcNative.rtcSetGatheringStateChangeCallback(pc, Gathering));
                var init = new DuelRtcNative.ChannelInit { Negotiated = true, ManualStream = true, Stream = 0 };
                game = DuelRtcNative.Check(DuelRtcNative.rtcCreateDataChannelEx(pc, "openz-game-v1", ref init));
                init.Stream = 1; init.Reliability = new DuelRtcNative.Reliability { Unordered = true, Unreliable = true, MaxRetransmits = 0 };
                voice = DuelRtcNative.Check(DuelRtcNative.rtcCreateDataChannelEx(pc, "openz-voice-v1", ref init));
            }
            catch { Dispose(); throw; }
        }
#if UNITY_5_3_OR_NEWER
        [AOT.MonoPInvokeCallback(typeof(DuelRtcNative.GatheringCallback))]
#endif
        static void OnGathering(int id, int state, IntPtr user)
        { if (state == 2 && Gatherings.TryGetValue(id, out var ready)) ready.TrySetResult(true); }

        public async Task<string> DescriptionAsync(string type, CancellationToken cancel)
        {
            DuelRtcNative.Check(DuelRtcNative.rtcSetLocalDescription(pc, type));
            using (cancel.Register(() => gathered.TrySetCanceled())) await gathered.Task.ConfigureAwait(false);
            var sdp = new StringBuilder(65536);
            DuelRtcNative.Check(DuelRtcNative.rtcGetLocalDescription(pc, sdp, sdp.Capacity));
            return sdp.ToString();
        }
        public void SetRemoteDescription(string sdp, string type)
        {
            if (sdp == null || sdp.Length > 60000 || (type != "offer" && type != "answer")) throw new InvalidDataException("Invalid connection offer.");
            DuelRtcNative.Check(DuelRtcNative.rtcSetRemoteDescription(pc, sdp, type));
        }
        public void Start() { if (pump != null) throw new InvalidOperationException(); pump = Task.Run(Pump); }
        public bool Send(DuelPacket packet)
        {
            if (Closed || packet?.Payload == null || packet.Payload.Length > DuelWire.MaxPayload) return false;
            if (Interlocked.Add(ref queuedBytes, packet.Payload.Length + 12) > Budget) { Fail("Your opponent's connection is too slow."); return false; }
            outgoing.Enqueue(packet.Kind == DuelMessage.RoomBattlefield && packet.Compress
                ? packet : new DuelPacket(packet.Kind, (byte[])packet.Payload.Clone(), packet.Compress));
            return true;
        }
        public bool TryReceive(out DuelPacket packet)
        {
            if (!incoming.TryDequeue(out packet)) return false;
            Interlocked.Add(ref receivedBytes, -packet.Payload.Length - 12); return true;
        }
        public bool SendVoice(byte[] data)
        {
            if (Closed || data == null || data.Length != DuelVoiceCodec.FrameSamples) return false;
            if (Interlocked.Increment(ref voiceOutCount) > 16) { Interlocked.Decrement(ref voiceOutCount); return false; }
            voiceOut.Enqueue((byte[])data.Clone()); return true;
        }
        public bool TryReceiveVoice(out byte[] data)
        { if (!voiceIn.TryDequeue(out data)) return false; Interlocked.Decrement(ref voiceInCount); return true; }
        async Task Pump()
        {
            try
            {
                while (!DuelRtcNative.rtcIsOpen(game) || !DuelRtcNative.rtcIsOpen(voice))
                { if (DuelRtcNative.rtcIsClosed(game)) throw new IOException("Unable to connect. Try again or use Advanced TCP/IP."); await Task.Delay(10, stop.Token).ConfigureAwait(false); }
                var localCandidate = new StringBuilder(2048); var remoteCandidate = new StringBuilder(2048);
                DuelRtcNative.Check(DuelRtcNative.rtcGetSelectedCandidatePair(pc, localCandidate, localCandidate.Capacity, remoteCandidate, remoteCandidate.Capacity));
                UsesRelay = localCandidate.ToString().Contains("typ relay") || remoteCandidate.ToString().Contains("typ relay");
                if (relayOnly && !UsesRelay) throw new IOException("The relay check did not select a relay.");
                Send(new DuelPacket(DuelMessage.Hello, DuelTcpPeer.EncodeHello(secret, fingerprint, name)));
                byte[] sending = null; int offset = 0, stateBytesInFlight = 0;
                int voiceSequence = 0, receivedVoiceSequence = 0;
                var receive = new byte[65536]; var decoder = new DuelRtcFrameDecoder();
                while (!Closed)
                {
                    stop.Token.ThrowIfCancellationRequested();
                    if (DuelRtcNative.rtcIsClosed(game)) throw new IOException("Your opponent disconnected.");
                    if (sending == null && outgoing.TryDequeue(out var packet))
                    {
                        var wirePacket = DuelWire.PackDelta(packet);
                        stateBytesInFlight = DuelWire.IsDelta(packet.Kind) ? wirePacket.Payload.Length : 0;
                        if (packet.Kind == DuelMessage.RoomBattlefield && packet.Compress)
                            sending = DuelWire.FrameForDirectRoom(packet);
                        else
                        {
                            using var buffer = new MemoryStream();
                            await DuelWire.WriteAsync(buffer, wirePacket, stop.Token).ConfigureAwait(false);
                            sending = buffer.ToArray();
                        }
                        offset = 0;
                        Interlocked.Add(ref queuedBytes, -packet.Payload.Length - 12);
                    }
                    Volatile.Write(ref nativeBuffered, DuelRtcNative.rtcGetBufferedAmount(game) + (sending == null ? 0 : sending.Length - offset));
                    if (sending != null && DuelRtcNative.rtcGetBufferedAmount(game) < 256 * 1024)
                    {
                        int size = Math.Min(ChunkSize, sending.Length - offset); var part = new byte[size]; Array.Copy(sending, offset, part, 0, size);
                        DuelRtcNative.Check(DuelRtcNative.rtcSendMessage(game, part, size)); offset += size;
                        if (offset == sending.Length)
                        {
                            sending = null;
                            if (stateBytesInFlight > 0) Interlocked.Add(ref statePayloadBytesSent, stateBytesInFlight);
                        }
                    }
                    for (int i = 0; i < 64; i++)
                    {
                        int size = receive.Length, result = DuelRtcNative.rtcReceiveMessage(game, receive, ref size);
                        if (result == -3) break;
                        DuelRtcNative.Check(result);
                        if (size <= 0) throw new InvalidDataException("Unexpected text data.");
                        foreach (var received in decoder.Add(receive, size))
                        {
                            if (!authenticated.Task.IsCompleted)
                            {
                                if (received.Kind != DuelMessage.Hello) throw new InvalidDataException("Expected a game handshake.");
                                var fields = DuelTcpPeer.DecodeHello(received.Payload);
                                if (!DuelTcpPeer.SameSecret(secret, fields[0]) || fields[1] != fingerprint) throw new InvalidDataException("Invitation or game version does not match.");
                                RemoteName = fields[2]; authenticated.TrySetResult(true);
                            }
                            else
                            {
                                long arrival = System.Diagnostics.Stopwatch.GetTimestamp();
                                var decoded = DuelWire.UnpackDelta(received);
                                if (Interlocked.Add(ref receivedBytes, decoded.Payload.Length + 12) > Budget) throw new IOException("Incoming game queue exceeded.");
                                if (DuelWire.IsDelta(decoded.Kind)) decoded.ReceivedAtTicks = arrival;
                                incoming.Enqueue(decoded);
                            }
                        }
                    }
                    if (authenticated.Task.IsCompletedSuccessfully)
                    {
                        if (voiceOut.TryDequeue(out var samples))
                        {
                            Interlocked.Decrement(ref voiceOutCount);
                            if (DuelRtcNative.rtcGetBufferedAmount(voice) < 16000)
                            {
                                var frame = new byte[samples.Length + 4]; BitConverter.GetBytes(++voiceSequence).CopyTo(frame, 0); Array.Copy(samples, 0, frame, 4, samples.Length);
                                DuelRtcNative.Check(DuelRtcNative.rtcSendMessage(voice, frame, frame.Length));
                            }
                        }
                        for (int i = 0; i < 16; i++)
                        {
                            int size = receive.Length, result = DuelRtcNative.rtcReceiveMessage(voice, receive, ref size);
                            if (result == -3) break;
                            DuelRtcNative.Check(result);
                            if (size != DuelVoiceCodec.FrameSamples + 4) continue;
                            int sequence = BitConverter.ToInt32(receive, 0);
                            if (sequence <= receivedVoiceSequence) continue;
                            receivedVoiceSequence = sequence;
                            if (Interlocked.Increment(ref voiceInCount) > 16) { Interlocked.Decrement(ref voiceInCount); continue; }
                            var samplesIn = new byte[DuelVoiceCodec.FrameSamples]; Array.Copy(receive, 4, samplesIn, 0, samplesIn.Length); voiceIn.Enqueue(samplesIn);
                        }
                    }
                    await Task.Delay(2, stop.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { Fail(error.Message); }
        }
        void Fail(string reason) { Failure = reason; authenticated.TrySetException(new IOException(reason)); stop.Cancel(); Interlocked.Exchange(ref disposed, 1); }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref cleanupQueued, 1) != 0) return;
            Interlocked.Exchange(ref disposed, 1); stop.Cancel(); authenticated.TrySetCanceled();
            // Deletion waits for native callbacks. Run it after the polling loop exits.
            if (pump != null) { _ = pump.ContinueWith(_ => DeleteNative()); pump = null; }
            else DeleteNative();
        }
        void DeleteNative()
        {
            int id = Interlocked.Exchange(ref pc, -1); if (id < 0) return;
            Gatherings.TryRemove(id, out _);
            DuelRtcNative.rtcClosePeerConnection(id);
            if (game >= 0) DuelRtcNative.rtcDeleteDataChannel(game);
            if (voice >= 0) DuelRtcNative.rtcDeleteDataChannel(voice);
            DuelRtcNative.rtcDeletePeerConnection(id);
        }
    }

    /// <summary>Bounded reassembly of the existing game stream across SCTP messages.</summary>
    public sealed class DuelRtcFrameDecoder
    {
        readonly byte[] header = new byte[12];
        byte[] frame;
        int headerCount, frameCount;
        public System.Collections.Generic.IEnumerable<DuelPacket> Add(byte[] bytes, int length, int offset = 0)
        {
            if (bytes == null || offset < 0 || length < 1 || length > bytes.Length - offset) throw new InvalidDataException("Invalid data channel fragment.");
            length += offset;
            while (offset < length)
            {
                if (frame == null)
                {
                    int n = Math.Min(12 - headerCount, length - offset); Array.Copy(bytes, offset, header, headerCount, n); offset += n; headerCount += n;
                    if (headerCount < 12) continue;
                    int size = BitConverter.ToInt32(header, 8);
                    if (BitConverter.ToUInt32(header, 0) != 0x31445A4F || BitConverter.ToUInt16(header, 4) != DuelWire.Version ||
                        !Enum.IsDefined(typeof(DuelMessage), (DuelMessage)BitConverter.ToUInt16(header, 6)) || size < 0 || size > DuelWire.MaxPayload)
                        throw new InvalidDataException("Invalid data channel frame.");
                    frame = new byte[size]; frameCount = 0;
                }
                int count = Math.Min(frame.Length - frameCount, length - offset); Array.Copy(bytes, offset, frame, frameCount, count); offset += count; frameCount += count;
                if (frameCount == frame.Length)
                { var packet = new DuelPacket((DuelMessage)BitConverter.ToUInt16(header, 6), frame); frame = null; headerCount = 0; yield return packet; }
            }
        }
    }
}
