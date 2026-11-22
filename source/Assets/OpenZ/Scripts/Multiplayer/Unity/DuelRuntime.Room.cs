using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelRuntime
    {
        const string RoomHostId = "host";
        MultiplayerRoom room;
        MultiplayerRoomGuestLink roomGuest;
        readonly Dictionary<int, MultiplayerRoomHostLink> roomLinks = new Dictionary<int, MultiplayerRoomHostLink>();
        readonly HashSet<int> roomDeparted = new HashSet<int>(), roomEndReady = new HashSet<int>();
        DuelReplica.CaptureBuffer roomCapture;
        SkirmishNetworkTuning roomTuning;
        int roomEpoch, roomDrainIndex, roomEndReadyEpoch = -1;
        DuelPacket roomFinalStats;
        bool roomFinalStatsPending;
        bool roomFullState, roomReplicaChanged;
        double roomRetuneAt, roomRouteChangedAt;
        double roomStateArrival, roomStateInterval, roomStateJitter;
        float roomSimulationPeak;
        long roomWireBytes;
        internal SkirmishOnline RoomTransport => peer as SkirmishOnline;
        internal bool RoomCanLaunch => room?.CanLaunch == true && Connected && RoomTransport != null &&
            roomEpoch == RoomTransport.Epoch &&
            room.Members.All(member => member.Id == RoomHostId || !member.Seat.MayUsePlayerCommands ||
                roomLinks.ContainsKey(int.Parse(member.Id, System.Globalization.CultureInfo.InvariantCulture))) &&
            RoomTransport.Remotes.All(link => IsRoomGhost(link.Id) || link.Ready && roomLinks.ContainsKey(link.Id));
        int RoomPosition => IsHost ? room?.HostPosition ?? -1 : JoinedRoom?.Humans.FirstOrDefault(member => member.Local)?.Position ?? -1;
        MapChangeVote roomVote;
        RoomVoteState roomVoteState, lastRoomVoteState;
        readonly Dictionary<string, double> roomMapRequestAvailableAt = new Dictionary<string, double>(StringComparer.Ordinal);
        string roomMapRequestBattlefield;
        double localRoomMapRequestAvailableAt;
        int lastClosedRoomVote;
        bool localRoomBallot;
        string roomVoteMap;
        int loadedRoomRevision = -1, roomVoteSequence;
        int activeRoomVote;
        double roomVoteDeadline;
        internal string RoomVoteMap => roomVoteMap;
        internal double RoomVoteSeconds => Math.Max(0, roomVoteDeadline - Now);
        internal bool RoomCanVote => RoomPosition >= 0 && Playing && Game && !Game.World.GameOver && !Game.World.SkirmishEliminated(LocalTeam);
        internal bool CanCastRoomMapVote => RoomCanVote && !MapChangeOwner && !localRoomBallot && roomVoteMap != null;
        internal int RoomVotesNeeded => roomVoteState?.VotesNeeded ?? 0;
        internal bool SkirmishRoom => room != null || roomGuest != null;
        internal MultiplayerRoom HostedRoom => room;
        internal RoomSnapshot JoinedRoom => roomGuest?.View;
        internal bool RoomBotAt(int seat) => room?.Bots.Any(bot => bot.Position == seat) == true ||
            JoinedRoom?.Bots.Any(bot => bot.Position == seat) == true;
        internal SkirmishRules RoomRules => room?.Rules ?? JoinedRoom?.Rules;
        bool TeamChatMatch => Playing && Game && Game.World.SkirmishHasTeams;
        internal bool CanChooseChatAudience => TeamChatMatch && !LocalMatchGhost;
        internal bool ChatToAll { get; set; }
        internal bool RoomVoiceMuted { get; private set; }
        internal bool RoomParticipantMuted(string id) => int.TryParse(id, out int number) && RoomTransport?.IsVoiceMuted(number) == true;

        internal void SetRoomRules(SkirmishRules rules)
        {
            if (room == null || room.Launched) return;
            room.SetRules(RoomHostId, rules);
            PublishRoom();
        }

        internal void SetRoomClosed(int seat, bool value)
        {
            if (room == null || room.Launched) return;
            room.SetClosed(RoomHostId, seat, value);
            PublishRoom();
        }

        internal void SwapRoomStartingSides()
        {
            if (room == null || room.Launched) return;
            room.SwapStartingSides(RoomHostId);
            PublishRoom();
        }

        internal void BringRoomPlayer(string id, int seat)
        {
            if (room == null || room.Launched || !room.Members.Any(member => member.Id == id)) return;
            room.BringWaitingPlayer(RoomHostId, id, seat);
            PublishRoom();
        }

        internal void ModerateRoomPlayer(string id, bool? ban, string note)
        {
            if (room == null || !int.TryParse(id, out int number) || !room.Members.Any(member => member.Id == id)) return;
            if (ban.HasValue) { RoomTransport.RemoveParticipant(number, ban.Value, note); return; }
            bool muted = !RoomTransport.IsVoiceMuted(number);
            RoomTransport.SetVoiceMuted(number, muted);
            RoomTransport.Remotes.FirstOrDefault(link => link.Id == number)?.Send(new DuelPacket(DuelMessage.RoomVoiceMute, new[] { (byte)(muted ? 1 : 0) }));
            Menus.Refresh();
        }

        void CreateSkirmishRoom(string name)
        {
            room = new MultiplayerRoom(RoomHostId, name, OzAiMapCatalog.ThreePlayerSkirmish.Id);
            FillRoomBots();
        }

        static string RoomGuest(int id) => id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        void PublishRoom()
        {
            foreach (var link in roomLinks.Values) link.Publish();
            PublishRoomChanges();
            Menus.Refresh();
        }

        internal void SelectRoomMap(string map)
        {
            if (room == null || !IsRoomMap(OzAiMapCatalog.FindSkirmishMap(map))) return;
            room.SelectMap(RoomHostId, map);
            FillRoomBots();
            PublishRoom();
        }

        void FillRoomBots()
        {
            var occupied = room.Members.Select(member => member.Seat.Position).Concat(room.Bots.Select(bot => bot.Position)).Concat(room.ClosedPositions).ToArray();
            for (int seat = 0; seat < room.Starts; seat++)
                if (!occupied.Contains(seat)) room.SetBot(RoomHostId, seat, seat, ZBotDifficulty.Normal);
        }

        internal static IEnumerable<OzAiMap> RoomMaps => OzAiMapCatalog.OfficialSkirmishMaps
            .Concat(OzAiMapCatalog.Playable.Where(map => map.Duel)).Concat(OzAiMapCatalog.MultiplayerMaps).Where(IsRoomMap);

        internal static bool IsRoomMap(OzAiMap map) => map != null && (map.IsOfficial || map.Duel || map.Starts >= 3) &&
            SceneUtility.GetBuildIndexByScenePath(map.ScenePath) >= 0 && Resources.Load<TextAsset>("OpenZ/Maps/" + map.Id + "_grid");

        internal void SetRoomBot(int seat, ZBotDifficulty? difficulty)
        {
            if (room == null) return;
            if (difficulty.HasValue)
            {
                int alliance = room.Bots.Where(bot => bot.Position == seat).Select(bot => bot.Alliance).DefaultIfEmpty(seat).Single();
                room.SetBot(RoomHostId, seat, alliance, difficulty.Value);
            }
            else room.RemoveBot(RoomHostId, seat);
            PublishRoom();
        }

        internal void CycleRoomAlliance(int seat)
        {
            if (room == null) return;
            var member = room.Members.FirstOrDefault(candidate => candidate.Seat.Position == seat);
            if (member.Id != null)
                room.AssignHuman(RoomHostId, member.Id, seat, (member.Alliance + 1) % room.Starts);
            else
            {
                var bot = room.Bots.Single(candidate => candidate.Position == seat);
                room.SetBot(RoomHostId, seat, (bot.Alliance + 1) % room.Starts, bot.Difficulty);
            }
            PublishRoom();
        }

        internal void SetRoomReady(bool ready)
        {
            if (room == null) roomGuest?.SetReady(ready);
            else
            {
                room.SetReady(RoomHostId, room.Revision, ready);
                PublishRoom();
            }
        }

        internal bool RenameRoomPlayer(string name)
        {
            try
            {
                if (room == null) return roomGuest?.Rename(name) == true;
                room.Rename(RoomHostId, name);
                PublishRoom();
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        internal void LaunchRoom()
        {
            if (!Connected || !RoomCanLaunch) return;
            RoomTransport.Lock(true);
            room.Launch(RoomHostId);
            PublishRoom();
            LoadRoomMap(room.Map, room.Revision);
        }

        bool HandleRoomPacket(DuelPacket packet, SkirmishOnline.Link sender = null)
        {
            switch (packet.Kind)
            {
                case DuelMessage.RoomCommand:
                case DuelMessage.RoomOrder:
                    if (packet.Kind == DuelMessage.RoomOrder && MapChangeActive) return true;
                    if (sender != null && roomLinks.TryGetValue(sender.Id, out var link) && link.Receive(packet, false) && packet.Kind == DuelMessage.RoomCommand)
                    {
                        PublishRoom();
                        if (room.MatchLoaded && !Playing) StartRoomPlay();
                        else if (Playing && room.Members.Any(member => member.Id == RoomGuest(sender.Id) &&
                            member.Seat.Role == MatchSeatRole.Observer)) roomFullState = true;
                    }
                    return true;
                case DuelMessage.RoomSnapshot:
                    if (IsHost || !OnlineMode || View != null) return true;
                    var guest = roomGuest ?? new MultiplayerRoomGuestLink(peer);
                    if (!guest.Receive(packet)) return true;
                    roomGuest = guest;
                    Menus.Refresh();
                    if (JoinedRoom.Launched && (!session || loadedRoomRevision != JoinedRoom.Revision)) LoadRoomMap(JoinedRoom.Map, JoinedRoom.Revision);
                    else if (!JoinedRoom.Launched && session) ReturnToLobbyScene();
                    else if (JoinedRoom.MatchLoaded && Game && !Playing) StartRoomPlay();
                    else if (JoinedRoom.Launched && !JoinedRoom.MatchLoaded && roomLoadedScene) AcknowledgeRoomLoad();
                    return true;
                case DuelMessage.RoomBattlefield:
                    long receiving = Stopwatch.GetTimestamp();
                    if (roomGuest?.Receive(packet) == true)
                    {
                        matchStateReceived += packet.WirePayloadLength;
                        long elapsed = Stopwatch.GetTimestamp() - receiving;
                        perf.Received++;
                        perf.ReceiveBytes += packet.WirePayloadLength;
                        perf.ReceivePayloadPeak = Math.Max(perf.ReceivePayloadPeak, packet.WirePayloadLength);
                        perf.ReceiveTicks += elapsed;
                        perf.ReceiveMaxTicks = Math.Max(perf.ReceiveMaxTicks, elapsed);
                        RecordDiagnosticState(packet.ReceivedAtTicks, elapsed, packet.ReceivedAtTicks > 0 ? receiving - packet.ReceivedAtTicks : 0);
                        RecordRoomMotion(packet.ReceivedAtTicks);
                        roomReplicaChanged = true;
                    }
                    return true;
                case DuelMessage.RoomVoteRequest:
                    if (room != null && room.Launched && sender != null && roomLinks.ContainsKey(sender.Id) && packet.Payload.Length <= 512)
                        ReceiveRoomVoteRequest(ReadJson<RoomVoteRequest>(packet), room.HumanTeam(RoomGuest(sender.Id)));
                    return true;
                case DuelMessage.RoomVoteState:
                    if (roomGuest != null && packet.Payload.Length <= 512) ShowRoomVote(ReadJson<RoomVoteState>(packet));
                    return true;
                case DuelMessage.Ping:
                case DuelMessage.Pong:
                case DuelMessage.Chat:
                case DuelMessage.Disconnect:
                case DuelMessage.Surrender:
                case DuelMessage.MatchStats:
                case DuelMessage.EndReady:
                    return false;
                default:
                    return SkirmishRoom;
            }
        }

        void UpdateOnlineRoom()
        {
            var transport = RoomTransport;
            if (transport.Closed) { Fail(transport.Failure ?? "The room connection closed."); return; }
            var remotes = transport.Remotes;
            if (transport.Epoch != roomEpoch)
            {
                roomEpoch = transport.Epoch;
                roomRouteChangedAt = Now;
                roomLinks.Clear();
                roomFullState = true;
                roomFinalStatsPending = roomFinalStats != null;
                roomPublishedReturnedSeats = -1;
                roomWireBytes = 0;
                roomStateArrival = 0;
                motionHistory.Reset();
                roomRetuneAt = Now + 1;
                Status = transport.Route == "SFU" ? "Preparing the relay…" : "Preparing direct connections…";
                if (roomEpoch > 1) Menus.NotifyChat(transport.Route == "SFU" ?
                    "Switching to the Cloudflare relay…" : "Switching to peer-to-peer connections…");
                Menus.Refresh();
                return;
            }
            if (IsHost) UpdateRoomParticipants(remotes);
            if (peer == null) return;

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int count = 0; remotes.Length > 0 && count < 256; count++)
            {
                var sender = remotes[roomDrainIndex++ % remotes.Length];
                if (roomDrainIndex >= 256) roomDrainIndex %= remotes.Length;
                if (!sender.Ready || !sender.TryReceive(out var packet))
                {
                    if (count >= remotes.Length && (System.Diagnostics.Stopwatch.GetTimestamp() - start) * PerfTickMs >= .5) break;
                    if (count >= remotes.Length * 2) break;
                    continue;
                }
                sender.LastPacketAt = Now;
                ReadOnlineRoom(sender, packet);
                if (peer == null) return;
                if ((System.Diagnostics.Stopwatch.GetTimestamp() - start) * PerfTickMs >= ReceiveBudgetMs) break;
            }

            foreach (var link in remotes.Where(link => link.Ready))
            {
                if (link.LastPacketAt == 0) link.LastPacketAt = Now;
                if (!link.StallReported && Now - link.LastPacketAt > (session && !Playing ? 180 : 20))
                {
                    link.StallReported = true;
                    transport.ReportStalled(link);
                }
                if (Now - link.LastPing < 1) continue;
                link.LastPing = Now;
                link.Send(new DuelPacket(DuelMessage.Ping, BitConverter.GetBytes(Now)));
            }
            PingMs = (float)remotes.Select(link => link.Ping).DefaultIfEmpty().Max();
            if (!IsHost && roomGuest != null && remotes.Any(link => link.Ready)) Status = "Connected · " + transport.Route;
            if (IsHost && (!session || Playing)) PublishRoomLatency();
            if (IsHost && roomFinalStatsPending && !roomFullState && sentGameOver)
                roomFinalStatsPending = !transport.Send(roomFinalStats);
            if (IsHost && room.Launched && TimedEndLobby && Game && Game.World.GameOver)
                PublishRoomEndLobbyStatus(transport);
            UpdateRoomVote();
            if (Game && session && !IsHost)
            {
                if (roomReplicaChanged)
                {
                    long syncing = Stopwatch.GetTimestamp();
                    SyncClientViews();
                    perf.ViewSyncMaxTicks = Math.Max(perf.ViewSyncMaxTicks, Stopwatch.GetTimestamp() - syncing);
                    roomReplicaChanged = false;
                }
                if (localEndReady && roomEndReadyEpoch != roomEpoch && transport.Send(new DuelPacket(DuelMessage.EndReady, Array.Empty<byte>())))
                    roomEndReadyEpoch = roomEpoch;
            }
            if (Playing)
            {
                int queued = QueuedReceiveBytes;
                perf.ReceiveQueuePeak = Math.Max(perf.ReceiveQueuePeak, queued);
                RecordDiagnosticQueue(queued);
                RecordPerformanceFrame();
                if (!IsHost) ReceiveBudgetMs = Time.unscaledDeltaTime > .025f ? 2 : QueuedReceiveBytes > 32 * 1024 ? 5 : DefaultReceiveBudgetMs;
            }
        }

        void RecordRoomMotion(long arrival)
        {
            if (!Playing || arrival <= 0) return;
            double milliseconds = arrival * PerfTickMs;
            if (roomStateArrival > 0)
            {
                double interval = Math.Min(250, Math.Max(0, milliseconds - roomStateArrival));
                roomStateJitter += .15 * (Math.Abs(interval - roomStateInterval) - roomStateJitter);
                roomStateInterval += .15 * (interval - roomStateInterval);
            }
            roomStateArrival = milliseconds;
            InterpolationMs = Mathf.Clamp((int)(roomStateInterval + 2 * roomStateJitter), 70, 220);
            CaptureMotion(arrival);
        }

        void UpdateRoomParticipants(SkirmishOnline.Link[] remotes)
        {
            bool changed = false;
            List<SkirmishOnline.Link> newLinks = null;
            foreach (var link in remotes.Where(link => link.Ready))
            {
                if (roomLinks.ContainsKey(link.Id)) continue;
                string id = RoomGuest(link.Id);
                if (!room.Members.Any(member => member.Id == id))
                {
                    if (room.Launched)
                    {
                        link.Send(new DuelPacket(DuelMessage.Disconnect, Array.Empty<byte>()));
                        continue;
                    }
                    room.Join(id, link.RemoteName, replaceBot: true);
                }
                var hostLink = new MultiplayerRoomHostLink(room, link, id, roomEpoch * 1000000);
                if (Playing) hostLink.AttachBattlefield(Game.World);
                roomLinks.Add(link.Id, hostLink);
                if (Playing && lastRoomVoteState?.Revision == room.Revision)
                    (newLinks ??= new List<SkirmishOnline.Link>()).Add(link);
                if (RoomTransport.IsVoiceMuted(link.Id)) link.Send(new DuelPacket(DuelMessage.RoomVoiceMute, new byte[] { 1 }));
                changed = true;
            }
            if (Now - roomRouteChangedAt > 2)
            {
                foreach (var member in room.Members.Where(member => member.Id != RoomHostId))
                {
                    int id = int.Parse(member.Id, System.Globalization.CultureInfo.InvariantCulture);
                    if (remotes.Any(link => link.Id == id) || !roomDeparted.Add(id)) continue;
                    if (Playing && member.Seat.MayUsePlayerCommands)
                    {
                        if (activeRoomVote != 0) FinishRoomVote(MapChangeResult.Refuse);
                        Game.World.SurrenderSkirmish(room.HumanTeam(member.Id));
                    }
                    else if (!Playing && room.Launched && member.Seat.MayUsePlayerCommands) { ReturnRoomToLobby(); ReturnToLobbyScene(); }
                    if (!room.Launched) room.RemoveHuman(RoomHostId, member.Id);
                    roomLinks.Remove(id);
                    changed = true;
                }
            }
            if (changed)
            {
                PublishRoom();
                if (newLinks != null)
                {
                    var state = lastRoomVoteState;
                    state.Seconds = state.Result == MapChangeResult.Pending ? RoomVoteSeconds : 0;
                    UpdateRoomVoteCooldown(state);
                    var packet = new DuelPacket(DuelMessage.RoomVoteState, System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(state)));
                    foreach (var link in newLinks) link.Send(packet);
                }
                TryReturnToLobby();
            }
            Status = roomLinks.Count == 0 ? "Waiting for players…" : "Connected · " + RoomTransport.Route;
        }

        void ReadOnlineRoom(SkirmishOnline.Link sender, DuelPacket packet)
        {
            if (IsHost && !roomLinks.ContainsKey(sender.Id)) return;
            switch (packet.Kind)
            {
                case DuelMessage.Ping:
                    if (packet.Payload.Length == 8) sender.Send(new DuelPacket(DuelMessage.Pong, packet.Payload));
                    return;
                case DuelMessage.Pong:
                    if (packet.Payload.Length != 8 || BitConverter.ToDouble(packet.Payload, 0) != sender.LastPing) return;
                    double sample = (Now - sender.LastPing) * 1000;
                    if (sender.Ping > 0) sender.Jitter += .2 * (Math.Abs(sample - sender.Ping) - sender.Jitter);
                    sender.Ping = sender.Ping == 0 ? sample : sender.Ping + .25 * (sample - sender.Ping);
                    sender.BasePing = Math.Min(sender.BasePing, sender.Ping);
                    return;
                case DuelMessage.Chat:
                    if (!IsHost || packet.Payload.Length == 0 || packet.Payload.Length > 1024) return;
                    int id = sender.Id;
                    bool ghost = session && IsMatchGhost(id);
                    bool teamChat = TeamChatMatch && !ghost;
                    byte audience = packet.Payload[0];
                    bool addressed = audience <= 1;
                    if (teamChat && !addressed) return;
                    int offset = addressed ? 1 : 0;
                    string text = System.Text.Encoding.UTF8.GetString(packet.Payload, offset, packet.Payload.Length - offset);
                    if (text.Length > 240 || text.Any(char.IsControl)) return;
                    string senderName = room.Members.FirstOrDefault(member => member.Id == RoomGuest(id)).Name;
                    if (string.IsNullOrEmpty(senderName)) return;
                    var incoming = new RoomChat { Sender = id, Name = senderName, Text = text, Ghost = ghost, ToAll = teamChat && audience == 1 };
                    ForwardRoomChat(incoming);
                    if (ReceivesRoomChat(incoming, RoomTransport.Id)) NotifyRoomChat(incoming);
                    return;
                case DuelMessage.RoomChat:
                    if (IsHost || sender.Id != 1 || packet.Payload.Length > 2048) return;
                    var chat = ReadJson<RoomChat>(packet);
                    if (chat != null && (chat.ToAll || chat.Ghost == (session && LocalMatchGhost)) && chat.Sender != RoomTransport.Id &&
                        chat.Name?.Length <= 32 && chat.Text?.Length <= 240)
                        NotifyRoomChat(chat);
                    return;
                case DuelMessage.RoomLobbyStatus:
                    if (!IsHost && sender.Id == 1) ReceiveRoomLobbyStatus(packet);
                    return;
                case DuelMessage.RoomGhostFollow:
                case DuelMessage.RoomGhostView:
                    ReadGhostPacket(sender, packet);
                    return;
                case DuelMessage.RoomVoiceMute:
                    if (!IsHost && sender.Id == 1 && packet.Payload.Length == 1)
                    {
                        RoomVoiceMuted = packet.Payload[0] != 0;
                        Menus.Refresh();
                    }
                    return;
                case DuelMessage.Surrender:
                    if (IsHost && Playing && Game && !MapChangeActive && room.HumanTeam(RoomGuest(sender.Id)) != Team.Neutral)
                        Game.World.SurrenderSkirmish(room.HumanTeam(RoomGuest(sender.Id)));
                    return;
                case DuelMessage.EndReady:
                    if (IsHost && Playing && Game && Game.World.GameOver && packet.Payload.Length == 0 &&
                        room.HumanTeam(RoomGuest(sender.Id)) != Team.Neutral)
                    {
                        roomEndReady.Add(sender.Id);
                        TryReturnToLobby();
                    }
                    else if (!IsHost && sender.Id == 1 && session && packet.Payload.Length == 1)
                    {
                        roomReturnedSeats = packet.Payload[0];
                        Menus.Refresh();
                    }
                    return;
                case DuelMessage.MatchStats:
                    if (!IsHost && Game && Game.World.GameOver) Handle(packet);
                    return;
                case DuelMessage.Disconnect:
                    if (!IsHost) Fail("The host is no longer accepting participants for this match.");
                    return;
                case DuelMessage.RoomSnapshot:
                case DuelMessage.RoomBattlefield:
                case DuelMessage.RoomVoteState:
                    if (!IsHost && sender.Id == 1) HandleRoomPacket(packet, sender);
                    return;
                case DuelMessage.RoomCommand:
                case DuelMessage.RoomOrder:
                case DuelMessage.RoomVoteRequest:
                    if (IsHost) HandleRoomPacket(packet, sender);
                    return;
            }
        }

        [Serializable] sealed class RoomChat { public int Sender; public string Name, Text; public bool Ghost, ToAll; }

        bool ReceivesRoomChat(RoomChat chat, int recipient)
        {
            if (chat.ToAll) return true;
            bool ghost = recipient == RoomTransport.Id ? LocalMatchGhost : IsMatchGhost(recipient);
            if (ghost != chat.Ghost) return false;
            if (!TeamChatMatch || ghost) return true;
            Team SeatTeam(int id) => room.HumanTeam(id == RoomTransport.Id ? RoomHostId : RoomGuest(id));
            var from = SeatTeam(chat.Sender);
            var to = SeatTeam(recipient);
            return from != Team.Neutral && to != Team.Neutral && Game.World.AreAllied(from, to);
        }

        bool IsRoomGhost(int id) => room?.Members.Any(member => member.Id == RoomGuest(id) &&
            member.Seat.Role == MatchSeatRole.Observer) == true;

        void ForwardRoomChat(RoomChat chat)
        {
            if (!session) { SendJson(DuelMessage.RoomChat, chat); return; }
            var packet = new DuelPacket(DuelMessage.RoomChat, System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(chat)));
            foreach (var link in RoomTransport.Remotes.Where(link => link.Id != chat.Sender && ReceivesRoomChat(chat, link.Id)))
                link.Send(packet);
        }

        void SendRoomChat(string message)
        {
            string sender = room?.Members.First(member => member.Id == RoomHostId).Name ??
                JoinedRoom?.Humans.FirstOrDefault(member => member.Local)?.Name ?? localName;
            var chat = new RoomChat { Sender = RoomTransport.Id, Name = sender, Text = message,
                Ghost = session && LocalMatchGhost, ToAll = CanChooseChatAudience && ChatToAll };
            AddChat(FormatRoomChat(chat));
            if (IsHost) ForwardRoomChat(chat);
            else peer.Send(new DuelPacket(DuelMessage.Chat, System.Text.Encoding.UTF8.GetBytes(
                CanChooseChatAudience ? (chat.ToAll ? "\u0001" : "\u0000") + message : message)));
        }

        static string FormatRoomChat(RoomChat chat)
        {
            string line = (chat.Name + " : " + chat.Text).Replace("<", "‹").Replace(">", "›");
            return chat.ToAll ? "<color=#70D8FF>TO ALL : " + line + "</color>" : line;
        }

        void NotifyRoomChat(RoomChat chat)
        {
            string line = FormatRoomChat(chat);
            AddChat(line);
            Menus.NotifyChat(line);
        }

        void PublishRoomEndLobbyStatus(SkirmishOnline transport)
        {
            int seats = 0;
            foreach (var member in room.Members)
            {
                if (!member.Seat.MayUsePlayerCommands) continue;
                bool returned = member.Id == RoomHostId ? localEndReady :
                    int.TryParse(member.Id, out int id) && (roomEndReady.Contains(id) || roomDeparted.Contains(id));
                if (returned) seats |= 1 << member.Seat.Position;
            }
            if (seats != roomReturnedSeats)
            {
                roomReturnedSeats = seats;
                Menus.Refresh();
            }
            if (seats != roomPublishedReturnedSeats && transport.Send(new DuelPacket(DuelMessage.EndReady, new[] { (byte)seats })))
                roomPublishedReturnedSeats = seats;
        }

        void ReturnRoomToLobby()
        {
            room.ReturnToLobby(RoomHostId);
            foreach (int id in roomDeparted) room.RemoveHuman(RoomHostId, RoomGuest(id));
            roomDeparted.Clear();
            roomEndReady.Clear();
            RoomTransport.Lock(false);
        }

        void ClearOnlineRoom()
        {
            ClearRoomLobbyStatus();
            ResetGhostView();
            roomLinks.Clear();
            roomDeparted.Clear();
            roomEndReady.Clear();
            roomEpoch = roomDrainIndex = 0;
            ResetEndLobby();
            roomLoadedScene = false;
            RoomVoiceMuted = false;
            roomReplicaChanged = false;
            roomCapture = null;
            roomFinalStats = null;
            roomFinalStatsPending = false;
        }

        bool roomLoadedScene;
        int roomLoadAcknowledgedEpoch = -1;

        void AcknowledgeRoomLoad()
        {
            if (roomLoadAcknowledgedEpoch != roomEpoch && roomGuest.MarkLoaded())
                roomLoadAcknowledgedEpoch = roomEpoch;
        }

        void LoadRoomMap(string mapId, int revision)
        {
            ChatToAll = false;
            ResetGhostView();
            if (roomMapRequestBattlefield != mapId)
            {
                roomMapRequestAvailableAt.Clear();
                localRoomMapRequestAvailableAt = 0;
                roomMapRequestBattlefield = mapId;
            }
            var map = OzAiMapCatalog.FindSkirmishMap(mapId);
            if (!IsRoomMap(map)) throw new InvalidDataException("This battlefield is missing from the compiled game.");
            if (Playing && Game) Game.World.DuelForward = _ => false;
            Game = null;
            Playing = sentGameOver = finalStatsSent = FinalStatsReady = localEndReady = remoteEndReady = false;
            pendingEvents.Clear();
            roomFinalStats = null;
            roomFinalStatsPending = false;
            roomLoadedScene = false;
            roomLoadAcknowledgedEpoch = -1;
            roomEndReadyEpoch = -1;
            startAt = -1;
            loadedRoomRevision = revision;
            session = loadStarted = true;
            if (IsHost) RoomTransport.IsSpectatorVoice = id => session && IsRoomGhost(id);
            Status = "Loading the battlefield…";
            OzPlayBlue.Disarm("multiplayer skirmish");
            BeginBattlefieldLoad(map.Id, map.ScenePath);
        }

        void AttachRoomWorld(ZWorld world)
        {
            // World creation precedes the load barrier. Only the host configures the bots.
            if (IsHost) return;
            var snapshot = JoinedRoom;
            var alliances = Enumerable.Range(0, snapshot.Starts).ToArray();
            foreach (var member in snapshot.Humans.Where(member => member.Position >= 0))
                alliances[member.Position] = member.Alliance;
            foreach (var bot in snapshot.Bots)
                alliances[bot.Position] = bot.Alliance;
            world.SetSkirmishAlliances(alliances);
            world.ConfigureSkirmishRules(snapshot.Rules, snapshot.Closed, replica: true);
            roomGuest.AttachBattlefield(world);
            world.DuelForward = order => RoomPosition >= 0 && Playing && Connected && !MapChangeActive && !world.SkirmishEliminated(LocalTeam) && roomGuest.SendOrder(order);
        }

        void FinishRoomLoading()
        {
            roomLoadedScene = true;
            if (!IsHost)
            {
                AcknowledgeRoomLoad();
                return;
            }
            room.MarkLoaded(RoomHostId, room.Revision);
            PublishRoom();
            if (room.MatchLoaded) StartRoomPlay();
        }

        void StartRoomPlay()
        {
            if (IsHost)
            {
                room.ConfigureWorld(Game.World, 19);
                Game.DuelBindStartingViews();
                foreach (var link in roomLinks.Values) link.AttachBattlefield(Game.World);
                roomCapture = new DuelReplica.CaptureBuffer();
                roomTuning = new SkirmishNetworkTuning();
                roomFullState = true;
                roomSimulationPeak = 0;
                roomRetuneAt = Now + 1;
                roomWireBytes = RoomTransport.StatePayloadBytesSent;
            }
            startAt = lastSnapshot = Now;
            Game.StartSkirmishPlanning(RoomRules.PlanningSeconds);
            Playing = true;
            if (RoomObserver)
            {
                var target = GhostTargets.FirstOrDefault(member => member.Host) ?? GhostTargets.FirstOrDefault();
                if (target != null) FollowGhostTarget(target.Position);
            }
            Menus.CloseOverlay();
            BeginPerformance();
            roomStateArrival = roomStateJitter = 0;
            roomStateInterval = 1000d / DefaultSnapshotHz;
            matchStateReceived = 0;
            wireSentAtMatchStart = WireStateSent;
        }

        void DriveRoom(ZGame game, bool advance = true)
        {
            if (!IsHost || roomEpoch != RoomTransport.Epoch) return;
            var links = RoomTransport.Remotes;
            if (room.Members.Count(member => member.Id != RoomHostId && !roomDeparted.Contains(int.Parse(member.Id))) != roomLinks.Count ||
                links.Any(link => roomLinks.ContainsKey(link.Id) && !link.Ready)) return;
            if (advance && game.SkirmishPlanningLeft <= 0)
            {
                game.DuelAdvance(Time.unscaledDeltaTime);
                roomSimulationPeak = Math.Max(roomSimulationPeak, ZGame.RuleMsLastFrame);
                perf.SimulationMaxMs = Math.Max(perf.SimulationMaxMs, ZGame.RuleMsLastFrame);
            }
            double now = Now;
            int queued = QueuedSendBytes;
            perf.SendQueuePeak = Math.Max(perf.SendQueuePeak, queued);
            if (now >= roomRetuneAt)
            {
                long bytes = RoomTransport.StatePayloadBytesSent;
                double bytesPerSecond = Math.Max(0, bytes - roomWireBytes) / (1 + now - roomRetuneAt);
                roomTuning.Update(queued, links.Select(link => link.Jitter).DefaultIfEmpty().Max(),
                    links.Select(link => link.Ping - link.BasePing).Where(growth => growth > 0).DefaultIfEmpty().Max(),
                    roomSimulationPeak, bytesPerSecond, roomLinks.Count, RoomTransport.Route == "SFU");
                roomSimulationPeak = 0;
                roomWireBytes = bytes;
                roomRetuneAt = now + 1;
                SnapshotHz = roomTuning.Hz;
            }
            if (roomLinks.Count == 0) { pendingEvents.Clear(); return; }
            if ((!advance || roomFullState || game.World.GameOver && !sentGameOver || now - lastSnapshot >= 1d / SnapshotHz) && queued < 64 * 1024)
            {
                long started = Stopwatch.GetTimestamp();
                var delta = roomCapture.CaptureDelta(game.World, roomFullState);
                long captured = Stopwatch.GetTimestamp();
                int eventCount = Math.Min(pendingEvents.Count, DuelEventCodec.MaxEvents);
                var events = eventCount == pendingEvents.Count ? pendingEvents : pendingEvents.GetRange(0, eventCount);
                var packet = MultiplayerRoomPackets.Battlefield(room.Revision, delta, events);
                long enqueuing = Stopwatch.GetTimestamp();
                roomFullState = !RoomTransport.SendBattlefield(packet, stateCompressionOverride);
                if (roomFullState) return;
                pendingEvents.RemoveRange(0, eventCount);
                lastSnapshot = now;
                sentGameOver = game.World.GameOver;
                long finished = Stopwatch.GetTimestamp();
                perf.Sent++;
                perf.SendBytes += packet.Payload.Length;
                perf.SendPayloadPeak = Math.Max(perf.SendPayloadPeak, packet.Payload.Length);
                perf.SendTicks += finished - started;
                perf.SendMaxTicks = Math.Max(perf.SendMaxTicks, finished - started);
                perf.CaptureTicks += captured - started;
                perf.EncodeTicks += enqueuing - captured;
                perf.EnqueueTicks += finished - enqueuing;
                RecordDiagnosticState(enqueuing, finished - started, 0);
            }
        }

        [Serializable] sealed class RoomVoteRequest
        {
            public int Revision, Vote, Ballot;
            public int VehicleTech, RobotTech;
            public bool Cancel;
            public string Map;
        }

        [Serializable] sealed class RoomVoteState
        {
            public int Revision, Vote;
            public int Seat, VehicleTech, RobotTech, VotesNeeded;
            public double Seconds, CooldownSeconds;
            public string Map, Name, RequesterId;
            public MapChangeResult Result;
        }

        internal void CastRoomVote(MapChangeBallot ballot)
        {
            if (!CanCastRoomMapVote || RoomVoteSeconds <= 0) return;
            localRoomBallot = true;
            SendRoomVoteRequest(new RoomVoteRequest { Revision = loadedRoomRevision, Vote = activeRoomVote, Ballot = (int)ballot });
            Menus.Refresh();
        }

        void SendRoomVoteRequest(RoomVoteRequest request)
        {
            if (IsHost) ReceiveRoomVoteRequest(request, LocalTeam);
            else SendJson(DuelMessage.RoomVoteRequest, request);
        }

        void ReceiveRoomVoteRequest(RoomVoteRequest request, Team sender)
        {
            if (request == null || !Playing || !Game || Game.World.GameOver ||
                request.Revision != room.Revision || sender == Team.Neutral || Game.World.SkirmishEliminated(sender)) return;
            int seat = (int)sender - 1;
            if (request.Vote == 0)
            {
                if (activeRoomVote != 0 || !string.IsNullOrEmpty(request.Map)) return;
                var member = room.Members.First(candidate => candidate.Seat.MayUsePlayerCommands && candidate.Seat.Position == seat);
                if (Now < RoomMapRequestAvailableAt(member.Id)) return;
                roomMapRequestAvailableAt[member.Id] = Now + MapRequestCooldown;
                DriveRoom(Game, advance: false);
                PublishRoomVote(new RoomVoteState { Revision = room.Revision, Vote = ++roomVoteSequence,
                    Seat = seat, Name = member.Name, RequesterId = member.Id });
                return;
            }
            if (request.Vote != activeRoomVote || Now >= roomVoteDeadline) return;
            if (roomVoteMap == null && seat == roomVoteState.Seat)
            {
                if (request.Cancel) { FinishRoomVote(MapChangeResult.Refuse); return; }
                var candidate = OzAiMapCatalog.FindSkirmishMap(request.Map);
                if (request.Map == room.Map || string.IsNullOrEmpty(request.Map) ||
                    !IsRoomMap(candidate) || candidate.Starts < room.Members.Count(member => member.Seat.MayUsePlayerCommands) ||
                    !ValidMapTech(request.VehicleTech, request.RobotTech)) return;
                var eligible = room.Members.Where(member => member.Seat.MayUsePlayerCommands &&
                    !Game.World.SkirmishEliminated(room.HumanTeam(member.Id))).Select(member => member.Seat.Position).ToArray();
                roomVote = new MapChangeVote(eligible, Now);
                PublishRoomVote(new RoomVoteState { Revision = room.Revision, Vote = activeRoomVote, Seat = seat,
                    Name = roomVoteState.Name, RequesterId = roomVoteState.RequesterId,
                    Map = request.Map, VehicleTech = request.VehicleTech,
                    RobotTech = request.RobotTech, VotesNeeded = (eligible.Length + 1) / 2 });
            }
            else if (roomVote != null && seat != roomVoteState.Seat)
                FinishRoomVote(roomVote.Cast(seat, (MapChangeBallot)request.Ballot, Now));
        }

        void PublishRoomVote(RoomVoteState state)
        {
            lastRoomVoteState = state;
            state.Seconds = state.Result == MapChangeResult.Pending ? MapChangeVote.DurationSeconds : 0;
            UpdateRoomVoteCooldown(state);
            SendJson(DuelMessage.RoomVoteState, state);
            ShowRoomVote(state);
        }

        double RoomMapRequestAvailableAt(string id) => roomMapRequestAvailableAt.TryGetValue(id, out var time) ? time : 0;

        void UpdateRoomVoteCooldown(RoomVoteState state) => state.CooldownSeconds = state.Result == MapChangeResult.Apply ? 0 :
            Math.Max(0, RoomMapRequestAvailableAt(state.RequesterId) - Now);

        void FinishRoomVote(MapChangeResult result)
        {
            if (result == MapChangeResult.Pending) return;
            var state = roomVoteState;
            state.Result = result;
            roomVote = null;
            PublishRoomVote(state);
            if (result != MapChangeResult.Apply) return;
            ReturnRoomToLobby();
            room.SelectMap(RoomHostId, state.Map);
            var rules = room.Rules;
            rules.VehicleTech = state.VehicleTech;
            rules.RobotTech = state.RobotTech;
            room.SetRules(RoomHostId, rules);
            FillRoomBots();
            foreach (var member in room.Members.Where(member => member.Seat.MayUsePlayerCommands))
                room.SetReady(member.Id, room.Revision, true);
            if (room.CanLaunch) LaunchRoom();
            else
            {
                PublishRoom();
                ReturnToLobbyScene();
            }
        }

        void ShowRoomVote(RoomVoteState state)
        {
            if (state == null || state.Vote <= 0 || state.Revision != loadedRoomRevision ||
                state.Seat < 0 || state.Seat >= mapRequestAvailableAt.Length ||
                (!string.IsNullOrEmpty(state.Map) && !IsRoomMap(OzAiMapCatalog.FindSkirmishMap(state.Map)))) return;
            if (string.IsNullOrEmpty(state.Map)) state.Map = null;
            if (state.Result == MapChangeResult.Pending)
            {
                if (state.Vote <= lastClosedRoomVote || activeRoomVote > state.Vote) return;
                bool opening = activeRoomVote != state.Vote;
                if (!opening && roomVoteMap != null && state.Map == null) return;
                if (!IsHost && RoomPosition == state.Seat)
                    localRoomMapRequestAvailableAt = Now + state.CooldownSeconds;
                activeRoomVote = state.Vote;
                roomVoteState = state;
                roomVoteMap = state.Map;
                roomVoteDeadline = IsHost && roomVote != null ? roomVote.Deadline : Now + state.Seconds;
                if (opening)
                {
                    localRoomBallot = false;
                    SetMapChangePause(true);
                    Menus.ShowRoomVote();
                }
                else Menus.Refresh();
                return;
            }
            lastClosedRoomVote = Math.Max(lastClosedRoomVote, state.Vote);
            if (activeRoomVote != state.Vote) return;
            if (!IsHost && RoomPosition == state.Seat)
                localRoomMapRequestAvailableAt = Now + state.CooldownSeconds;
            activeRoomVote = 0;
            roomVoteState = null;
            roomVoteMap = null;
            SetMapChangePause(false);
            if (state.Result == MapChangeResult.Apply)
            {
                roomMapRequestAvailableAt.Clear();
                localRoomMapRequestAvailableAt = 0;
            }
            string mapName = OzAiMapCatalog.FindSkirmishMap(state.Map)?.Name;
            string notice = state.Result == MapChangeResult.Apply ? "Map change accepted: " + mapName :
                state.Map == null ? "Map change request ended." : "Map change refused: " + mapName;
            Menus.CloseRoomVote();
            Menus.NotifyMatch(notice);
        }

        void UpdateRoomVote()
        {
            if (!IsHost || activeRoomVote == 0 || !Playing || !Game) return;
            if (Game.World.GameOver || roomVote == null && Now >= roomVoteDeadline)
                FinishRoomVote(MapChangeResult.Refuse);
            else if (roomVote != null) FinishRoomVote(roomVote.Advance(Now));
        }

        void ClearRoomVote()
        {
            roomVote = null;
            roomVoteState = lastRoomVoteState = null;
            roomVoteMap = null;
            if (activeRoomVote != 0) SetMapChangePause(false);
            activeRoomVote = roomVoteSequence = 0;
            lastClosedRoomVote = 0;
            roomMapRequestAvailableAt.Clear();
            roomMapRequestBattlefield = null;
            localRoomMapRequestAvailableAt = 0;
            loadedRoomRevision = -1;
        }
    }
}
