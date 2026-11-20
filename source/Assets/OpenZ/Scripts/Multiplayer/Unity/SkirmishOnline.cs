using System;
using System.Collections.Generic;
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
    [Serializable] internal sealed class SkirmishSignal
    {
        [Serializable] internal sealed class Member
        {
            public int id;
            public string name;
            public bool published;
        }

        public string type, sdp;
        public int id, epoch, game, voice;
        public bool sfu;
        public Member[] players;
    }

    /// <summary>One invitation, one authoritative host, and a transport for each authenticated participant.</summary>
    internal sealed class SkirmishOnline : IDuelPeer
    {
        static readonly string[] Stun = { "stun:stun.cloudflare.com:3478" };
        readonly ClientWebSocket socket = new ClientWebSocket();
        readonly SemaphoreSlim writer = new SemaphoreSlim(1);
        readonly CancellationTokenSource stop;
        readonly Func<SkirmishSignal, Task<SkirmishSignal>> call;
        readonly Dictionary<int, Link> links = new Dictionary<int, Link>();
        readonly string secret, fingerprint, name;
        Link[] remotes = Array.Empty<Link>();
        SkirmishSfu relay;
        Task pump;
        int epoch;
        bool useSfu, fallbackRequested;
        volatile bool closed;
        string failure;

        public int Id { get; }
        public int Epoch => Volatile.Read(ref epoch);
        public bool IsHost => Id == 1;
        public string Route => Volatile.Read(ref useSfu) ? "SFU" : "DIRECT";
        public Link[] Remotes => Volatile.Read(ref remotes);
        public bool Closed => closed;
        public string Failure => failure;
        public string RemoteName => Remotes.FirstOrDefault()?.RemoteName ?? "";
        public int PendingSendBytes => relay?.PendingBytes ?? Remotes.Select(link => link.Rtc?.PendingSendBytes ?? 0).DefaultIfEmpty().Max();
        public int PendingReceiveBytes => Remotes.Sum(link => link.Input?.QueuedBytes ?? link.Rtc?.PendingReceiveBytes ?? 0);
        public long StatePayloadBytesSent => relay?.StateBytesSent ?? Remotes.Sum(link => link.Rtc?.StatePayloadBytesSent ?? 0);

        public SkirmishOnline(int id, string seat, string secret, string fingerprint, string name,
            Func<SkirmishSignal, Task<SkirmishSignal>> call, CancellationToken cancel)
        {
            if (id < 1) throw new ArgumentException("The invitation has no participant identity.");
            Id = id;
            this.call = call;
            this.secret = secret;
            this.fingerprint = fingerprint;
            this.name = name;
            stop = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            socket.Options.SetRequestHeader("Authorization", "Bearer " + seat);
        }

        public async Task Start(Uri address)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            limit.CancelAfter(TimeSpan.FromSeconds(20));
            await socket.ConnectAsync(address, limit.Token).ConfigureAwait(false);
            _ = Run();
        }

        async Task Run()
        {
            try
            {
                var read = Read();
                while (!stop.IsCancellationRequested)
                {
                    if (await Task.WhenAny(read, Task.Delay(100, stop.Token)).ConfigureAwait(false) == read)
                    {
                        var signal = await read.ConfigureAwait(false);
                        read = Read();
                        await Process(signal).ConfigureAwait(false);
                    }
                    await CheckLinks().ConfigureAwait(false);
                }
            }
            catch (Exception error)
            {
                if (!stop.IsCancellationRequested) failure = error.Message;
            }
            finally
            {
                Dispose();
                ResetLinks();
                socket.Dispose();
            }
        }

        async Task Process(SkirmishSignal signal)
        {
            if (signal.type == "closed") throw new IOException("The host left the lobby.");
            if (signal.type == "roster")
            {
                if (signal.epoch < epoch || signal.players == null || signal.players.Length > 8) return;
                if (signal.epoch != epoch)
                {
                    ResetLinks();
                    Volatile.Write(ref useSfu, signal.sfu);
                    Volatile.Write(ref epoch, signal.epoch);
                    fallbackRequested = false;
                }
                var others = signal.players.Where(member => IsHost ? member.id > 1 : member.id == 1).ToArray();
                if (!IsHost && others.Length == 0) throw new IOException("The host left the lobby.");
                foreach (int id in links.Keys.Where(id => !others.Any(member => member.id == id)).ToArray())
                {
                    links[id].Dispose();
                    links.Remove(id);
                    relay?.Unsubscribe(id);
                }
                if (fallbackRequested) return;
                foreach (var member in others)
                    if (!links.ContainsKey(member.id)) links.Add(member.id, new Link(this, member));
                Volatile.Write(ref remotes, links.Values.OrderBy(link => link.Id).ToArray());
                if (useSfu && others.Length > 0)
                {
                    try
                    {
                        if (relay == null) await OpenRelay().ConfigureAwait(false);
                        foreach (var member in others.Where(member => member.published))
                        {
                            var link = links[member.id];
                            if (link.Input != null) continue;
                            SkirmishSignal channels;
                            try { channels = await Call("subscribe", member.id).ConfigureAwait(false); }
                            catch (IOException error) when (error.Message == "The publisher has left.") { continue; }
                            link.Input = relay.Subscribe(member.id, channels.game, channels.voice);
                        }
                    }
                    catch (Exception error) when (error is IOException || error is HttpRequestException || error is InvalidOperationException || error is OperationCanceledException)
                    { if (!stop.IsCancellationRequested) await RequestDirect().ConfigureAwait(false); }
                }
                else if (IsHost)
                {
                    foreach (var link in links.Values.Where(link => link.Rtc == null))
                    {
                        link.Rtc = CreateDirect();
                        link.Setup = Offer(link);
                    }
                }
                return;
            }
            if (useSfu || signal.epoch != epoch || !links.TryGetValue(signal.id, out var remote)) return;
            if (IsHost && signal.type == "answer" && remote.Rtc != null)
            {
                remote.Rtc.SetRemoteDescription(signal.sdp, "answer");
                remote.Rtc.Start();
            }
            else if (!IsHost && signal.type == "offer" && remote.Rtc == null)
            {
                remote.Rtc = CreateDirect();
                remote.Setup = Answer(remote, signal.sdp);
            }
        }

        async Task OpenRelay()
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            limit.CancelAfter(TimeSpan.FromSeconds(15));
            var offer = await Call("setup").ConfigureAwait(false);
            relay = new SkirmishSfu(Id, Stun);
            string answer = await relay.Answer(offer.sdp, offer.game, limit.Token).ConfigureAwait(false);
            await Call("answer", sdp: answer).ConfigureAwait(false);
            await relay.WaitConnected(limit.Token).ConfigureAwait(false);
            var published = await Call("publish").ConfigureAwait(false);
            relay.Publish(published.game, published.voice);
            pump = relay.Pump(stop.Token);
        }

        Task<SkirmishSignal> Call(string type, int id = 0, string sdp = null) =>
            call(new SkirmishSignal { type = type, id = id, sdp = sdp, epoch = epoch });

        DuelRtcPeer CreateDirect() => new DuelRtcPeer(Stun, false, secret, fingerprint, name);

        async Task Offer(Link link)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            limit.CancelAfter(TimeSpan.FromSeconds(12));
            var sdp = await link.Rtc.DescriptionAsync("offer", limit.Token).ConfigureAwait(false);
            await Write(new SkirmishSignal { type = "offer", id = link.Id, sdp = sdp, epoch = epoch }).ConfigureAwait(false);
        }

        async Task Answer(Link link, string offer)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            limit.CancelAfter(TimeSpan.FromSeconds(12));
            link.Rtc.SetRemoteDescription(offer, "offer");
            var sdp = await link.Rtc.DescriptionAsync("answer", limit.Token).ConfigureAwait(false);
            await Write(new SkirmishSignal { type = "answer", id = 1, sdp = sdp, epoch = epoch }).ConfigureAwait(false);
            link.Rtc.Start();
        }

        async Task CheckLinks()
        {
            if (fallbackRequested) return;
            foreach (var link in links.Values)
            {
                if (useSfu && link.Input?.Ready == true && !link.Introduced)
                {
                    if (IsHost)
                    {
                        if (link.Input.TryReceive(out var hello) && hello.Kind == DuelMessage.Hello && hello.Payload.Length == 0)
                            link.Introduced = link.Send(new DuelPacket(DuelMessage.Hello, Array.Empty<byte>()));
                    }
                    else
                    {
                        if (link.Input.TryReceive(out var hello) && hello.Kind == DuelMessage.Hello && hello.Payload.Length == 0)
                            link.Introduced = true;
                        if (!link.Introduced && DateTime.UtcNow >= link.NextHello)
                        {
                            relay.Send(0, new DuelPacket(DuelMessage.Hello, Array.Empty<byte>()));
                            link.NextHello = DateTime.UtcNow.AddMilliseconds(350);
                        }
                    }
                }
                if (link.Ready) continue;
                if (useSfu && (relay?.Failed == true || link.Closed || DateTime.UtcNow > link.Deadline))
                { await RequestDirect().ConfigureAwait(false); return; }
                if (!useSfu && (link.Setup?.IsFaulted == true || link.Rtc?.Closed == true || DateTime.UtcNow > link.Deadline))
                {
                    if (!IsHost) throw new IOException("A direct connection to the host could not be established.");
                    if (!link.DropRequested)
                    {
                        link.DropRequested = true;
                        await Write(new SkirmishSignal { type = "drop", id = link.Id, epoch = epoch }).ConfigureAwait(false);
                    }
                }
            }
        }

        async Task RequestDirect()
        {
            if (fallbackRequested) return;
            fallbackRequested = true;
            await Write(new SkirmishSignal { type = "fallback", epoch = epoch }).ConfigureAwait(false);
        }

        void ResetLinks()
        {
            Volatile.Write(ref remotes, Array.Empty<Link>());
            foreach (var link in links.Values) link.Dispose();
            links.Clear();
            relay?.Dispose();
            relay = null;
            if (pump != null) _ = pump.ContinueWith(task => { _ = task.Exception; }, TaskScheduler.Default);
            pump = null;
        }

        async Task<SkirmishSignal> Read()
        {
            using var output = new MemoryStream();
            var buffer = new byte[4096];
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), stop.Token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close && !string.IsNullOrEmpty(result.CloseStatusDescription))
                    throw new IOException(result.CloseStatusDescription);
                if (result.MessageType != WebSocketMessageType.Text || output.Length + result.Count > 62000)
                    throw new IOException("The invitation connection closed.");
                output.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);
            return JsonUtility.FromJson<SkirmishSignal>(Encoding.UTF8.GetString(output.ToArray()));
        }

        async Task Write(SkirmishSignal signal)
        {
            await writer.WaitAsync(stop.Token).ConfigureAwait(false);
            try
            {
                var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(signal));
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, stop.Token).ConfigureAwait(false);
            }
            finally { writer.Release(); }
        }

        internal void Lock(bool locked) => _ = Write(new SkirmishSignal { type = locked ? "lock" : "unlock", epoch = Epoch })
            .ContinueWith(task => { _ = task.Exception; }, TaskScheduler.Default);

        internal void ReportStalled(Link link)
        {
            string action = Route == "SFU" ? "fallback" : "drop";
            if (!IsHost && action == "drop")
            {
                failure = "The direct connection to the host stopped responding.";
                Dispose();
                return;
            }
            _ = Write(new SkirmishSignal { type = action, id = link.Id, epoch = Epoch })
                .ContinueWith(task => { _ = task.Exception; }, TaskScheduler.Default);
        }

        public bool Send(DuelPacket packet)
        {
            var ready = Remotes.Where(link => link.Ready).ToArray();
            if (ready.Length == 0) return false;
            if (Volatile.Read(ref useSfu)) return relay?.Send(0, packet) == true;
            bool sent = true;
            foreach (var link in ready) sent &= link.Send(packet);
            return sent;
        }

        public bool TryReceive(out DuelPacket packet)
        {
            foreach (var link in Remotes)
                if (link.Ready && link.TryReceive(out packet)) return true;
            packet = null;
            return false;
        }

        public bool SendVoice(byte[] frame, int exclude = 0)
        {
            var ready = Remotes.Where(link => link.Ready && link.Id != exclude).ToArray();
            if (ready.Length == 0) return false;
            if (Volatile.Read(ref useSfu)) return relay?.SendVoice(frame, exclude) == true;
            bool sent = false;
            foreach (var link in ready) sent |= link.Rtc?.SendVoice(frame) == true;
            return sent;
        }

        public bool TryReceiveVoice(out byte[] frame)
        {
            foreach (var link in Remotes)
            {
                if (!link.Ready || !link.TryReceiveVoice(out frame)) continue;
                if (IsHost) SendVoice(frame, link.Id);
                return true;
            }
            frame = null;
            return false;
        }

        public void Dispose()
        {
            closed = true;
            stop.Cancel();
        }

        internal sealed class Link : IDuelPeer
        {
            readonly SkirmishOnline room;
            internal readonly int Id;
            internal readonly DateTime Deadline = DateTime.UtcNow.AddSeconds(30);
            internal volatile DuelRtcPeer Rtc;
            internal volatile SkirmishSfu.Subscription Input;
            internal volatile bool Introduced;
            internal Task Setup;
            internal DateTime NextHello;
            internal double LastPing, LastPacketAt, Ping, Jitter, BasePing = double.PositiveInfinity;
            internal bool DropRequested, StallReported;
            internal Link(SkirmishOnline room, SkirmishSignal.Member member)
            { this.room = room; Id = member.id; RemoteName = member.name; }
            public string RemoteName { get; }
            public string Failure => Rtc?.Failure;
            public bool Closed => Rtc?.Closed == true || Input?.Closed == true;
            public bool Ready => !Closed && (Rtc?.Ready.Status == TaskStatus.RanToCompletion || Introduced && Input?.Ready == true);
            public long StatePayloadBytesSent => Rtc?.StatePayloadBytesSent ?? 0;
            public bool Send(DuelPacket packet) => Rtc?.Send(packet) ?? room.relay?.Send(room.IsHost ? Id : 0, packet) == true;
            public bool TryReceive(out DuelPacket packet)
            {
                packet = null;
                return Rtc?.TryReceive(out packet) ?? Input?.TryReceive(out packet) == true;
            }
            internal bool TryReceiveVoice(out byte[] frame)
            {
                frame = null;
                return Rtc?.TryReceiveVoice(out frame) ?? Input?.VoiceFrames.TryDequeue(out frame) == true;
            }
            public void Dispose()
            {
                Rtc?.Dispose();
                if (Setup != null) _ = Setup.ContinueWith(task => { _ = task.Exception; }, TaskScheduler.Default);
            }
        }
    }
}
