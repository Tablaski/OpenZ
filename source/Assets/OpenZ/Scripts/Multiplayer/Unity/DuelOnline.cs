using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace OpenZ.Multiplayer
{
    [Serializable] public sealed class DuelOnlineSettings
    {
        public string ServiceUrl = DuelServiceConfiguration.ServiceUrl;
        public static string CurrentService => Environment.GetEnvironmentVariable("OPENZ_DUEL_SERVICE_URL") ?? PlayerPrefs.GetString("oz_duel_service", Load().ServiceUrl);
        public static DuelOnlineSettings Load()
        {
            var asset = Resources.Load<TextAsset>("OpenZ/DuelOnline");
            var settings = asset ? JsonUtility.FromJson<DuelOnlineSettings>(asset.text) : new DuelOnlineSettings();
            string configured = Environment.GetEnvironmentVariable("OPENZ_DUEL_SERVICE_URL");
            if (!string.IsNullOrWhiteSpace(configured)) settings.ServiceUrl = configured;
            return settings;
        }
        public static string ValidateService(string value)
        {
            if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
                throw new ArgumentException("Online play is not configured yet. Set up the invitation service first, or use Advanced TCP/IP.");
            return uri.GetLeftPart(UriPartial.Authority);
        }
    }
    [Serializable] sealed class DuelOnlineRequest { public string name, fingerprint; }
    [Serializable] sealed class DuelRoomResponse { public string room, secret, seat, role, error; }
    [Serializable] sealed class DuelIceServer { public string[] urls; public string username, credential; }
    [Serializable] sealed class DuelIceResponse { public DuelIceServer[] iceServers; public bool relay; public string relayReason, relayRemainingBytes, error; }
    [Serializable] sealed class DuelSignal { public string type, sdp; }

    /// <summary>Small HTTPS/WebSocket signaling client. Game and voice never pass through the lobby.</summary>
    public sealed class DuelOnline : IDisposable
    {
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        readonly CancellationTokenSource stop;
        ClientWebSocket socket;
        DuelRtcPeer peer;
        bool transferred;
        string serviceUrl, seatToken, versionFingerprint, localName;
        bool hostSeat, forceRelay;
        DuelIceResponse cachedIce;
        public string RoomCode { get; private set; }
        public string Secret { get; private set; }
        public string Status { get; private set; } = "Creating invitation…";
        public bool RelayAvailable { get; private set; }
        public long? RelayRemainingBytes { get; private set; }
        public string RelayNotice { get; private set; } = "";
        public DuelOnline(CancellationToken cancel) { stop = CancellationTokenSource.CreateLinkedTokenSource(cancel); }
        public static string ReadCode(string invitation)
        {
            string code = (invitation ?? "").Trim().ToUpperInvariant();
            if (Uri.TryCreate(invitation?.Trim(), UriKind.Absolute, out var uri))
            {
                if (uri.Scheme == "openz" && uri.Host == "join")
                {
                    var query = uri.Query.TrimStart('?').Split('&').Select(p => p.Split(new[] { '=' }, 2));
                    code = query.Where(p => p.Length == 2 && p[0] == "room").Select(p => Uri.UnescapeDataString(p[1]).ToUpperInvariant()).FirstOrDefault();
                }
                else if (uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback)) code = uri.AbsolutePath.Trim('/').ToUpperInvariant();
                else code = null;
            }
            if (code == null || code.Length != 29 || !code.StartsWith("OPNZ-", StringComparison.Ordinal) || code.Skip(5).Any(c => !"0123456789ABCDEF".Contains(c)))
                throw new ArgumentException("Paste the invitation link or room code from your opponent.");
            return code;
        }
        async Task<T> Post<T>(string url, object data, string seat = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new StringContent(JsonUtility.ToJson(data), Encoding.UTF8, "application/json");
                if (seat != null) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", seat);
                using (var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stop.Token).ConfigureAwait(false))
                using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[4096]; int count;
                    while ((count = await input.ReadAsync(buffer, 0, buffer.Length, stop.Token).ConfigureAwait(false)) > 0)
                    { if (output.Length + count > 65536) throw new IOException("Invitation service response is too large."); output.Write(buffer, 0, count); }
                    string text = Encoding.UTF8.GetString(output.ToArray());
                    if (!response.IsSuccessStatusCode)
                    {
                        string message = "Invitation service is unavailable (" + (int)response.StatusCode + "). Please try again.";
                        try { message = JsonUtility.FromJson<DuelRoomResponse>(text)?.error ?? message; } catch (ArgumentException) { }
                        throw new IOException(message);
                    }
                    return JsonUtility.FromJson<T>(text);
                }
            }
        }
        public async Task<IDuelPeer> ConnectAsync(string service, bool host, string invitation, string fingerprint, string name, bool relayOnly)
        {
            serviceUrl = DuelOnlineSettings.ValidateService(service);
            hostSeat = host; versionFingerprint = fingerprint; localName = name; forceRelay = relayOnly;
            var room = await Post<DuelRoomResponse>(host ? serviceUrl + "/rooms" : serviceUrl + "/rooms/" + ReadCode(invitation) + "/join",
                new DuelOnlineRequest { name = name, fingerprint = fingerprint }).ConfigureAwait(false);
            RoomCode = room.room; Secret = room.secret; seatToken = room.seat;
            return await ConnectTransport(false).ConfigureAwait(false);
        }

        internal Task<IDuelPeer> ReconnectAsync() => ConnectTransport(true);

        async Task<IDuelPeer> ConnectTransport(bool reconnect)
        {
            transferred = false;
            peer = null;
            try
            {
                if (reconnect && string.IsNullOrEmpty(seatToken)) throw new IOException("The invitation has no reconnect seat.");
                Status = reconnect ? "Reconnecting…" : hostSeat ? "Waiting for your opponent… Copy the invitation above." : "Joining your opponent…";
                socket = new ClientWebSocket(); socket.Options.SetRequestHeader("Authorization", "Bearer " + seatToken);
                var socketUri = new Uri(serviceUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "/rooms/" + RoomCode + "/socket");
                await socket.ConnectAsync(socketUri, stop.Token).ConfigureAwait(false);
                if (!reconnect) stop.CancelAfter(hostSeat ? TimeSpan.FromMinutes(30) : TimeSpan.FromSeconds(90));
                while ((await Receive().ConfigureAwait(false)).type != "ready") { }
                if (!reconnect) stop.CancelAfter(TimeSpan.FromSeconds(90));
                Status = "Preparing your connection…";
                var ice = reconnect ? cachedIce : await Post<DuelIceResponse>(serviceUrl + "/rooms/" + RoomCode + "/ice",
                    new DuelOnlineRequest(), seatToken).ConfigureAwait(false);
                cachedIce = ice;
                RelayAvailable = ice.relay;
                if (long.TryParse(ice.relayRemainingBytes, out long remaining) && remaining >= 0) RelayRemainingBytes = remaining;
                if (!ice.relay) RelayNotice = ice.relayReason == "budget"
                    ? "Relay allowance reserved for this billing period. Trying a direct connection."
                    : "Relay temporarily unavailable. Trying a direct connection.";
                if (forceRelay && !ice.relay) throw new IOException(RelayNotice + " Relay-only testing cannot continue.");
                string[] servers = ice.iceServers.SelectMany(server => server.urls.Select(url => IceUrl(url, server.username, server.credential))).ToArray();
                peer = new DuelRtcPeer(servers, forceRelay, Secret, versionFingerprint, localName);
                Status = ice.relay ? "Finding a connection that works for you both…" : RelayNotice;
                if (hostSeat) await Send(new DuelSignal { type = "offer", sdp = await peer.DescriptionAsync("offer", stop.Token).ConfigureAwait(false) }).ConfigureAwait(false);
                var description = await Receive().ConfigureAwait(false);
                if (description.type != (hostSeat ? "answer" : "offer")) throw new IOException("Your opponent left. Ask for a new invitation.");
                peer.SetRemoteDescription(description.sdp, description.type);
                if (!hostSeat) await Send(new DuelSignal { type = "answer", sdp = await peer.DescriptionAsync("answer", stop.Token).ConfigureAwait(false) }).ConfigureAwait(false);
                peer.Start();
                Status = ice.relay ? "Finishing your connection…" : RelayNotice;
                using (stop.Token.Register(() => peer.Dispose())) await peer.Ready.ConfigureAwait(false);
                transferred = true; stop.CancelAfter(Timeout.InfiniteTimeSpan); Status = "Connected";
                // Signaling is no longer needed; closing it does not close the peer connection.
                return peer;
            }
            catch (DllNotFoundException) { throw new IOException("The WebRTC library is missing. Reinstall the complete player folder or use Advanced TCP/IP."); }
            catch (OperationCanceledException) { throw new IOException("Connection cancelled or timed out. " + RelayNotice + " Try a new invitation or Advanced TCP/IP."); }
            finally
            {
                var signaling = socket; socket = null;
                signaling?.Dispose();
                if (!transferred) peer?.Dispose();
            }
        }
        static string IceUrl(string url, string username, string credential)
        {
            if (url.StartsWith("stun:", StringComparison.Ordinal)) return url;
            if (!url.StartsWith("turn:", StringComparison.Ordinal) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(credential)) throw new IOException("Invalid relay configuration.");
            return "turn:" + Uri.EscapeDataString(username) + ":" + Uri.EscapeDataString(credential) + "@" + url.Substring(5);
        }
        async Task Send(DuelSignal message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, stop.Token).ConfigureAwait(false);
        }
        async Task<DuelSignal> Receive()
        {
            using (var output = new MemoryStream())
            {
                var buffer = new byte[4096]; WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), stop.Token).ConfigureAwait(false);
                    if (result.MessageType != WebSocketMessageType.Text || output.Length + result.Count > 62000) throw new IOException("Invitation closed. Ask for a new link.");
                    output.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
                var message = JsonUtility.FromJson<DuelSignal>(Encoding.UTF8.GetString(output.ToArray()));
                if (message == null || message.type == "left") throw new IOException("Your opponent left. Ask for a new invitation.");
                return message;
            }
        }
        public void Dispose()
        {
            stop.Cancel(); socket?.Abort(); socket?.Dispose(); socket = null;
            if (!transferred) peer?.Dispose();
        }
    }
}
