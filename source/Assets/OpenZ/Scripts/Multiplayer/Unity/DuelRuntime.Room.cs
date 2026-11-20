using System;
using System.IO;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelRuntime
    {
        const string RoomHostId = "host", RoomGuestId = "guest";
        MultiplayerRoom room;
        MultiplayerRoomHostLink roomHost;
        MultiplayerRoomGuestLink roomGuest;
        MapChangeVote roomVote;
        string roomVoteMap;
        int loadedRoomRevision = -1, roomVoteSequence;
        int activeRoomVote;
        double roomVoteDeadline;
        internal string RoomVoteMap => roomVoteMap;
        internal double RoomVoteSeconds => Math.Max(0, roomVoteDeadline - Now);
        internal bool RoomCanVote => Playing && Game && !Game.World.GameOver && !Game.World.SkirmishEliminated(LocalTeam);
        internal bool SkirmishRoom => room != null || roomGuest != null;
        internal MultiplayerRoom HostedRoom => room;
        internal RoomSnapshot JoinedRoom => roomGuest?.View;

        void CreateSkirmishRoom(string name)
        {
            room = new MultiplayerRoom(RoomHostId, name, OzAiMapCatalog.ThreePlayerSkirmish.Id);
            FillRoomBots();
        }

        void ConnectRoomGuest()
        {
            room.Join(RoomGuestId, peer.RemoteName);
            roomHost = new MultiplayerRoomHostLink(room, peer, RoomGuestId);
            PublishRoom();
        }

        void PublishRoom()
        {
            if (roomHost != null && !roomHost.Publish()) throw new IOException("Could not send the room snapshot.");
            Menus.Refresh();
        }

        internal void SelectRoomMap(string map)
        {
            if (room == null || !IsRoomMap(OzAiMapCatalog.Find(map))) return;
            room.SelectMap(RoomHostId, map);
            FillRoomBots();
            PublishRoom();
        }

        void FillRoomBots()
        {
            var occupied = room.Members.Select(member => member.Seat.Position).Concat(room.Bots.Select(bot => bot.Position)).ToArray();
            for (int seat = 2; seat < room.Starts; seat++)
                if (!occupied.Contains(seat)) room.SetBot(RoomHostId, seat, seat, ZBotDifficulty.Normal);
        }

        internal static bool IsRoomMap(OzAiMap map) => map != null && map.Starts >= 3 &&
            SceneUtility.GetBuildIndexByScenePath(map.ScenePath) >= 0;

        internal void SetRoomBot(int seat, ZBotDifficulty? difficulty)
        {
            if (room == null) return;
            if (difficulty.HasValue) room.SetBot(RoomHostId, seat, seat, difficulty.Value);
            else room.RemoveBot(RoomHostId, seat);
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

        internal void LaunchRoom()
        {
            if (!Connected || room?.CanLaunch != true) return;
            room.Launch(RoomHostId);
            PublishRoom();
            LoadRoomMap(room.Map, room.Revision);
        }

        bool HandleRoomPacket(DuelPacket packet)
        {
            switch (packet.Kind)
            {
                case DuelMessage.RoomCommand:
                case DuelMessage.RoomOrder:
                    if (roomHost?.Receive(packet) == true && packet.Kind == DuelMessage.RoomCommand)
                    {
                        Menus.Refresh();
                        if (room.MatchLoaded && !Playing) StartRoomPlay();
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
                    return true;
                case DuelMessage.RoomBattlefield:
                    roomGuest?.Receive(packet);
                    return true;
                case DuelMessage.RoomVoteRequest:
                    if (room != null && packet.Payload.Length <= 512) ReceiveRoomVoteRequest(ReadJson<RoomVoteRequest>(packet), Team.Blue);
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

        void LoadRoomMap(string mapId, int revision)
        {
            var map = OzAiMapCatalog.Find(mapId);
            if (!IsRoomMap(map)) throw new InvalidDataException("This battlefield is missing from the compiled game.");
            if (Playing && Game) Game.World.DuelForward = _ => false;
            Game = null;
            Playing = sentGameOver = finalStatsSent = FinalStatsReady = localEndReady = remoteEndReady = false;
            pendingEvents.Clear();
            startAt = -1;
            loadedRoomRevision = revision;
            session = loadStarted = true;
            Status = "Loading the battlefield…";
            Menus.ShowLoading();
            OzPlayBlue.Disarm("multiplayer skirmish");
            SceneManager.LoadSceneAsync(map.ScenePath);
        }

        void AttachRoomWorld(ZWorld world)
        {
            // World creation precedes the load barrier. Only the host configures the bots.
            if (IsHost) return;
            roomGuest.AttachBattlefield(world);
            world.DuelForward = order => Playing && Connected && !world.SkirmishEliminated(LocalTeam) && roomGuest.SendOrder(order);
        }

        void FinishRoomLoading()
        {
            if (!IsHost)
            {
                if (!roomGuest.MarkLoaded()) throw new IOException("Could not acknowledge the battlefield load.");
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
                room.ConfigureFreeForAllWorld(Game.World, 19);
                roomHost.AttachBattlefield(Game.World);
                if (!roomHost.PublishBattlefield()) throw new IOException("Could not send the opening battlefield.");
            }
            startAt = lastSnapshot = Now;
            Playing = true;
            Menus.CloseOverlay();
            BeginPerformance();
        }

        void DriveRoom(ZGame game)
        {
            if (!IsHost) return;
            game.DuelAdvance(Time.unscaledDeltaTime);
            if (pendingEvents.Count > DuelEventCodec.MaxEvents)
            {
                Fail("Too many pending network events.");
                return;
            }
            if (game.World.GameOver && !sentGameOver || Now - lastSnapshot >= 1d / SnapshotHz && QueuedSendBytes < 256 * 1024)
            {
                if (!roomHost.PublishBattlefield(events: pendingEvents))
                {
                    Fail("The connection cannot keep up with battlefield updates.");
                    return;
                }
                pendingEvents.Clear();
                lastSnapshot = Now;
                sentGameOver = game.World.GameOver;
            }
        }

        [Serializable] sealed class RoomVoteRequest
        {
            public int Revision, Vote, Ballot;
            public string Map;
        }

        [Serializable] sealed class RoomVoteState
        {
            public int Revision, Vote;
            public string Map;
            public MapChangeResult Result;
        }

        internal void ProposeRoomMap(string map)
        {
            if (!RoomCanVote || roomVoteMap != null) return;
            SendRoomVoteRequest(new RoomVoteRequest { Revision = loadedRoomRevision, Map = map });
        }

        internal void CastRoomVote(MapChangeBallot ballot)
        {
            if (!RoomCanVote || roomVoteMap == null) return;
            SendRoomVoteRequest(new RoomVoteRequest { Revision = loadedRoomRevision, Vote = activeRoomVote, Ballot = (int)ballot });
        }

        void SendRoomVoteRequest(RoomVoteRequest request)
        {
            if (IsHost) ReceiveRoomVoteRequest(request, LocalTeam);
            else SendJson(DuelMessage.RoomVoteRequest, request);
        }

        void ReceiveRoomVoteRequest(RoomVoteRequest request, Team sender)
        {
            if (request == null || !Playing || !Game || Game.World.GameOver ||
                request.Revision != room.Revision || Game.World.SkirmishEliminated(sender)) return;
            if (request.Vote == 0)
            {
                if (roomVote != null || request.Map == room.Map || string.IsNullOrEmpty(request.Map) ||
                    !IsRoomMap(OzAiMapCatalog.Find(request.Map))) return;
                var eligible = room.Members.Where(member => member.Seat.MayUsePlayerCommands &&
                    !Game.World.SkirmishEliminated(room.HumanTeam(member.Id))).Select(member => member.Seat.Position);
                roomVote = new MapChangeVote(eligible, Now);
                var state = new RoomVoteState { Revision = room.Revision, Vote = ++roomVoteSequence, Map = request.Map };
                SendJson(DuelMessage.RoomVoteState, state);
                ShowRoomVote(state);
                return;
            }
            if (roomVote == null || request.Vote != activeRoomVote) return;
            FinishRoomVote(roomVote.Cast((int)sender - 1, (MapChangeBallot)request.Ballot, Now));
        }

        void FinishRoomVote(MapChangeResult result)
        {
            if (result == MapChangeResult.Pending) return;
            var state = new RoomVoteState { Revision = room.Revision, Vote = activeRoomVote, Map = roomVoteMap, Result = result };
            roomVote = null;
            SendJson(DuelMessage.RoomVoteState, state);
            ShowRoomVote(state);
            if (result != MapChangeResult.Apply) return;
            room.ReturnToLobby(RoomHostId);
            room.SelectMap(RoomHostId, state.Map);
            FillRoomBots();
            foreach (var member in room.Members.Where(member => member.Seat.MayUsePlayerCommands))
                room.SetReady(member.Id, room.Revision, true);
            LaunchRoom();
        }

        void ShowRoomVote(RoomVoteState state)
        {
            if (state == null || state.Vote <= 0 || state.Revision != loadedRoomRevision ||
                string.IsNullOrEmpty(state.Map) || !IsRoomMap(OzAiMapCatalog.Find(state.Map))) return;
            if (state.Result == MapChangeResult.Pending)
            {
                if (state.Vote <= activeRoomVote) return;
                activeRoomVote = state.Vote;
                roomVoteMap = state.Map;
                roomVoteDeadline = Now + MapChangeVote.DurationSeconds;
                Menus.ShowRoomVote();
                return;
            }
            if (activeRoomVote != state.Vote || roomVoteMap == null) return;
            roomVoteMap = null;
            string mapName = OzAiMapCatalog.Find(state.Map).Name;
            string notice = state.Result == MapChangeResult.Apply ? "Map change accepted: " + mapName : "Map change refused: " + mapName;
            AddChat(notice);
            Menus.CloseRoomVote();
            Menus.NotifyChat(notice);
        }

        void UpdateRoomVote()
        {
            if (roomVote == null || !Playing || !Game) return;
            FinishRoomVote(Game.World.GameOver ? MapChangeResult.Refuse : roomVote.Advance(Now));
        }

        void ClearRoomVote()
        {
            roomVote = null;
            roomVoteMap = null;
            activeRoomVote = roomVoteSequence = 0;
            loadedRoomRevision = -1;
        }
    }
}
