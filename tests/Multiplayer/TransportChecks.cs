using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class TransportChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        CheckStatePacking(check);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                string secret = DuelTcpPeer.NewInvitationSecret();
                var accept = DuelTcpPeer.AcceptAsync(listener, secret, "test-build", cancel.Token);
                using (var guest = await DuelTcpPeer.ConnectAsync("127.0.0.1", port, secret, "test-build", "Guest", cancel.Token))
                using (var host = await accept)
                {
                    check(host.RemoteName == "Guest", "TCP authenticated identity lost");
                    for (int i = 0; i < 128; i++)
                        check(guest.Send(new DuelPacket(DuelMessage.Command, BitConverter.GetBytes(i))), "Socket queue refused ordinary traffic");
                    int received = 0;
                    while (received < 128 && !cancel.IsCancellationRequested)
                    {
                        while (host.TryReceive(out var packet))
                        {
                            check(packet.Kind == DuelMessage.Command && BitConverter.ToInt32(packet.Payload, 0) == received,
                                "TCP lost order or split message boundaries");
                            received++;
                        }
                        if (received < 128) await Task.Delay(5, cancel.Token);
                    }
                    check(received == 128 && host.BytesReceived == 128 * 16, "Socket byte accounting failed");
                    var state = new byte[4096];
                    for (int i = 0; i < 3; i++)
                        check(host.Send(new DuelPacket(i == 1 ? DuelMessage.DeltaBinaryEvents : DuelMessage.Delta, state, i == 1)),
                            "Socket queue refused mixed state updates");
                    long stateBytes = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        DuelPacket packet;
                        while (!guest.TryReceive(out packet)) await Task.Delay(5, cancel.Token);
                        check(packet.Kind == (i == 1 ? DuelMessage.DeltaBinaryEvents : DuelMessage.Delta) &&
                            packet.Payload.SequenceEqual(state) && packet.ReceivedAtTicks > 0, "Socket changed state update order or data");
                        check(i == 1 ? packet.WirePayloadLength < state.Length : packet.WirePayloadLength == state.Length,
                            "Socket did not switch compression on and off");
                        stateBytes += packet.WirePayloadLength;
                    }
                    while (host.StatePayloadBytesSent < stateBytes) await Task.Delay(5, cancel.Token);
                    check(host.StatePayloadBytesSent == stateBytes, "Socket state payload counters disagree");
                    guest.Dispose();
                    while (!host.Closed) await Task.Delay(5, cancel.Token);
                    check(host.Closed && host.Failure != null, "Disconnect was not detected");
                }
                var badAccept = DuelTcpPeer.AcceptAsync(listener, secret, "test-build", cancel.Token);
                bool rejected = false;
                try
                {
                    using (await DuelTcpPeer.ConnectAsync("127.0.0.1", port, DuelTcpPeer.NewInvitationSecret(), "test-build", "Intruder", cancel.Token)) { }
                }
                catch { rejected = true; }
                bool hostRejected = false;
                try { using (await badAccept) { } } catch { hostRejected = true; }
                check(rejected && hostRejected, "Wrong invitation reached authenticated session");
            }
            finally { listener.Stop(); }
        }
    }

    static void CheckStatePacking(Action<bool, string> check)
    {
        var raw = new DuelPacket(DuelMessage.DeltaBinaryEvents, new byte[2048], true);
        var packed = DuelWire.PackDelta(raw);
        var unpacked = DuelWire.UnpackDelta(packed);
        check(packed.Kind == DuelMessage.DeltaCompressed && unpacked.Kind == raw.Kind &&
            unpacked.Payload.SequenceEqual(raw.Payload) && unpacked.WirePayloadLength == packed.Payload.Length,
            "Compression did not preserve the state or count the encoded bytes");
        var tiny = new DuelPacket(DuelMessage.Delta, new byte[64], true);
        var noise = new byte[4096]; new Random(39).NextBytes(noise);
        var incompressible = new DuelPacket(DuelMessage.Delta, noise, true);
        var disabled = new DuelPacket(DuelMessage.Delta, new byte[4096]);
        check(DuelWire.PackDelta(tiny) == tiny && DuelWire.PackDelta(incompressible) == incompressible &&
            DuelWire.PackDelta(disabled) == disabled, "State compression should skip small, unprofitable and disabled packets");
        var badSize = (byte[])packed.Payload.Clone();
        BitConverter.GetBytes(int.MaxValue).CopyTo(badSize, 1);
        bool rejected = false;
        try { DuelWire.UnpackDelta(new DuelPacket(DuelMessage.DeltaCompressed, badSize)); }
        catch (InvalidDataException) { rejected = true; }
        check(rejected, "Compressed packet accepted an unbounded output size");

        var evt = new ZEvent { type = ZEventType.MissileExploded, objId = 500, otherId = -1,
            x = 290, y = 844, radius = 30, value = 127.5f, end = 123.456, flag = true, flag2 = true, driver = true };
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        using var reader = new BinaryReader(stream);
        DuelEventCodec.Write(writer, new[] { evt });
        stream.Position = 0;
        check(DuelEventCodec.Read(reader).Single().Equals(evt), "Binary events lost payload fields");
        stream.SetLength(stream.Length - 1); stream.Position = 0;
        rejected = false;
        try { DuelEventCodec.Read(reader); }
        catch (InvalidDataException) { rejected = true; }
        check(rejected, "Binary events accepted a truncated record");
    }
}
