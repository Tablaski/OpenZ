using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OpenZ.Multiplayer;

static class TransportChecks
{
    public static async Task Run(Action<bool, string> check)
    {
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
}
