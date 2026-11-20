using System.Text;
using OpenZ.Multiplayer;

static class SfuLoopback
{
    static readonly TaskCompletionSource<bool> Gathered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    static readonly DuelRtcNative.GatheringCallback Gathering = (_, state, _) =>
    { if (state == 2) Gathered.TrySetResult(true); };

    internal static async Task Run()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(16));
        using var client = new SkirmishSfu(2);
        var config = new DuelRtcNative.Configuration { DisableAutoNegotiation = true };
        int server = DuelRtcNative.Check(DuelRtcNative.rtcCreatePeerConnection(ref config));
        var channels = new List<int>();
        int Channel(string name, ushort stream, bool lossy = false)
        {
            var init = new DuelRtcNative.ChannelInit { Negotiated = true, ManualStream = true, Stream = stream };
            if (lossy) init.Reliability = new DuelRtcNative.Reliability { Unordered = true, Unreliable = true };
            int id = DuelRtcNative.Check(DuelRtcNative.rtcCreateDataChannelEx(server, name, ref init));
            channels.Add(id);
            return id;
        }

        try
        {
            DuelRtcNative.Check(DuelRtcNative.rtcSetGatheringStateChangeCallback(server, Gathering));
            Channel("server-events", 0);
            DuelRtcNative.Check(DuelRtcNative.rtcSetLocalDescription(server, "offer"));
            await Gathered.Task.WaitAsync(timeout.Token);
            var offer = new StringBuilder(65536);
            DuelRtcNative.Check(DuelRtcNative.rtcGetLocalDescription(server, offer, offer.Capacity));
            var answer = await client.Answer(offer.ToString(), 0, timeout.Token);
            DuelRtcNative.Check(DuelRtcNative.rtcSetRemoteDescription(server, answer, "answer"));
            await client.WaitConnected(timeout.Token);
            int upstream = Channel("openz-game", 2);
            Channel("openz-voice", 3, true);
            int downstream = Channel("openz-game", 4);
            Channel("openz-voice", 5, true);
            client.Publish(2, 3);
            var subscription = client.Subscribe(1, 4, 5);
            var pump = client.Pump(timeout.Token);
            try
            {
                while (!subscription.Ready) await Task.Delay(10, timeout.Token);
                client.Send(0, new DuelPacket(DuelMessage.Ping, new byte[] { 123 }));
                var receive = new byte[32];
                int size = receive.Length;
                while (DuelRtcNative.rtcReceiveMessage(upstream, receive, ref size) == -3)
                { await Task.Delay(10, timeout.Token); size = receive.Length; }
                if (size != 17 || receive[16] != 123) throw new IOException("Negotiated publication lost its game data.");

                async Task Send(int to, byte data)
                {
                    using var stream = new MemoryStream();
                    stream.Write(BitConverter.GetBytes(to));
                    await DuelWire.WriteAsync(stream, new DuelPacket(DuelMessage.Chat, new[] { data }), timeout.Token);
                    var packet = stream.ToArray();
                    DuelRtcNative.Check(DuelRtcNative.rtcSendMessage(downstream, packet, packet.Length));
                }
                await Send(9, 99);
                await Send(2, 42);
                DuelPacket received;
                while (!subscription.TryReceive(out received)) await Task.Delay(10, timeout.Token);
                if (!received.Payload.SequenceEqual(new byte[] { 42 })) throw new IOException("Negotiated subscription ignored the recipient.");
            }
            finally
            {
                timeout.Cancel();
                try { await pump; } catch (OperationCanceledException) { }
            }
            Console.WriteLine("PASS: SFU-style offer/answer, negotiated channel IDs and addressed game frames over native WebRTC (local loopback).");
        }
        finally
        {
            foreach (int channel in channels) DuelRtcNative.rtcDeleteDataChannel(channel);
            DuelRtcNative.rtcDeletePeerConnection(server);
        }
    }
}
