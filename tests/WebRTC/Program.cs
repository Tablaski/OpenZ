using System.Runtime.InteropServices;
using OpenZ.Multiplayer;
using System.Text.Json;

string native = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../source/Packages/org.openz.datachannel/Plugins/x86_64/datachannel.dll"));
NativeLibrary.SetDllImportResolver(typeof(DuelRtcPeer).Assembly, (name, assembly, path) => name == "datachannel" ? NativeLibrary.Load(native) : IntPtr.Zero);
bool relayOnly = args.Contains("--relay");
string[] hostServers = Array.Empty<string>(), guestServers = Array.Empty<string>();
if (relayOnly)
{
    using var http = new HttpClient();
    string service = Environment.GetEnvironmentVariable("OPENZ_DUEL_SERVICE_URL") ?? "https://openz-duel.openz-tablaski.workers.dev";
    async Task<JsonElement> Post(string path, object data, string seat = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, service + path);
        request.Content = new StringContent(JsonSerializer.Serialize(data), System.Text.Encoding.UTF8, "application/json");
        if (seat != null) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", seat);
        using var response = await http.SendAsync(request); response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
    var a = await Post("/rooms", new { name = "Native relay host", fingerprint = new string('A', 64) });
    string room = a.GetProperty("room").GetString();
    var b = await Post("/rooms/" + room + "/join", new { name = "Native relay guest", fingerprint = new string('A', 64) });
    async Task<string[]> Servers(string seat)
    {
        var ice = await Post("/rooms/" + room + "/ice", new { }, seat);
        return ice.GetProperty("iceServers").EnumerateArray().SelectMany(s => s.GetProperty("urls").EnumerateArray().Select(u =>
            u.GetString().StartsWith("turn:") ? "turn:" + Uri.EscapeDataString(s.GetProperty("username").GetString()) + ":" + Uri.EscapeDataString(s.GetProperty("credential").GetString()) + "@" + u.GetString().Substring(5) : u.GetString())).ToArray();
    }
    hostServers = await Servers(a.GetProperty("seat").GetString()); guestServers = await Servers(b.GetProperty("seat").GetString());
}
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
string secret = DuelTcpPeer.NewInvitationSecret();
using var host = new DuelRtcPeer(hostServers, relayOnly, secret, "TEST-BUILD", "HOST");
using var guest = new DuelRtcPeer(guestServers, relayOnly, secret, "TEST-BUILD", "GUEST");
string offer = await host.DescriptionAsync("offer", timeout.Token);
Console.WriteLine("Host ICE gathering completed; relay candidate present: " + offer.Contains("typ relay"));
guest.SetRemoteDescription(offer, "offer");
string answer = await guest.DescriptionAsync("answer", timeout.Token);
Console.WriteLine("Guest ICE gathering completed; relay candidate present: " + answer.Contains("typ relay"));
host.SetRemoteDescription(answer, "answer");
host.Start(); guest.Start();
await Task.WhenAll(host.Ready, guest.Ready).WaitAsync(timeout.Token);
if (relayOnly && (!host.UsesRelay || !guest.UsesRelay)) throw new Exception("Forced relay did not use a TURN candidate pair.");
Console.WriteLine("Selected TURN path on both peers: " + (host.UsesRelay && guest.UsesRelay));
if (host.RemoteName != "GUEST" || guest.RemoteName != "HOST") throw new Exception("Handshake lost player names.");
var payload = new byte[1024 * 1024 + 137]; Random.Shared.NextBytes(payload);
if (!host.Send(new DuelPacket(DuelMessage.Load, payload))) throw new Exception("Failed to queue snapshot.");
if (!guest.Send(new DuelPacket(DuelMessage.Command, new byte[] { 1, 2, 3 }))) throw new Exception("Failed to queue order.");
byte[] receivedVoice = null;
bool receivedLoad = false, receivedOrder = false;
while (!receivedLoad || !receivedOrder || receivedVoice == null)
{
    timeout.Token.ThrowIfCancellationRequested();
    guest.SendVoice(Enumerable.Repeat((byte)42, DuelVoiceCodec.FrameSamples).ToArray());
    if (guest.TryReceive(out var packet)) { if (packet.Kind != DuelMessage.Load || !payload.SequenceEqual(packet.Payload)) throw new Exception("Fragmented snapshot corrupted."); receivedLoad = true; }
    if (host.TryReceive(out var order)) { if (order.Kind != DuelMessage.Command || !order.Payload.SequenceEqual(new byte[] { 1, 2, 3 })) throw new Exception("Order corrupted."); receivedOrder = true; }
    host.TryReceiveVoice(out var voice); if (voice != null) receivedVoice = voice;
    if (host.Closed || guest.Closed) throw new Exception(host.Failure ?? guest.Failure);
    await Task.Delay(5, timeout.Token);
}
if (receivedVoice.Any(b => b != 42)) throw new Exception("Voice corrupted.");
Console.WriteLine("PASS: native encrypted peer handshake, 1 MiB fragmented snapshot, reverse command and independent voice channel.");

byte[] report = System.Text.Encoding.UTF8.GetBytes("OpenZ-VK duel diagnostics v1\n" +
    string.Concat(Enumerable.Repeat("matchS=90.1 receiveMsPerUpdate=0.94 arrivalGapMaxMs=103.0\n", 900)));
if (!guest.Send(new DuelPacket(DuelMessage.DiagnosticsReport, report))) throw new Exception("Failed to queue diagnostic report.");
while (true)
{
    timeout.Token.ThrowIfCancellationRequested();
    if (host.TryReceive(out var packet))
    {
        if (packet.Kind != DuelMessage.DiagnosticsReport || !report.SequenceEqual(packet.Payload))
            throw new Exception("Diagnostic report corrupted.");
        break;
    }
    await Task.Delay(5, timeout.Token);
}
Console.WriteLine("PASS: guest diagnostic report delivered to host.");

using var wrong = new DuelRtcPeer(Array.Empty<string>(), false, secret, "WRONG-BUILD", "WRONG");
using var correct = new DuelRtcPeer(Array.Empty<string>(), false, secret, "TEST-BUILD", "CORRECT");
wrong.SetRemoteDescription(await correct.DescriptionAsync("offer", timeout.Token), "offer");
correct.SetRemoteDescription(await wrong.DescriptionAsync("answer", timeout.Token), "answer");
correct.Start(); wrong.Start();
try { await correct.Ready.WaitAsync(timeout.Token); throw new Exception("Mismatched build accepted."); }
catch (IOException) { Console.WriteLine("PASS: incompatible build rejected before game traffic."); }

var decoder = new DuelRtcFrameDecoder();
var invalid = new byte[12]; BitConverter.GetBytes(0x31445A4Fu).CopyTo(invalid, 0); BitConverter.GetBytes((ushort)1).CopyTo(invalid, 4);
BitConverter.GetBytes((ushort)DuelMessage.Load).CopyTo(invalid, 6); BitConverter.GetBytes(DuelWire.MaxPayload + 1).CopyTo(invalid, 8);
try { decoder.Add(invalid, invalid.Length).ToArray(); throw new Exception("Oversized frame accepted."); }
catch (InvalidDataException) { Console.WriteLine("PASS: oversized snapshot rejected before allocation."); }
