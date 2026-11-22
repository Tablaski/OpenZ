using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelRuntime
    {
        internal enum MatchRequestKind { None, Map, Lobby }
        enum RequestAction { Open, Suggest, Accept, Decline, Cancel }
        enum RequestResult { Pending, Declined, Expired, ChangeMap, ReturnToLobby }

        [Serializable] sealed class MatchAction
        {
            public MatchRequestKind Kind;
            public RequestAction Action;
            public int Id;
            public string Map;
            public int VehicleTech, RobotTech;
        }

        [Serializable] sealed class MatchState
        {
            public MatchRequestKind Kind;
            public RequestResult Result;
            public int Id, Seat;
            public bool Suggested;
            public string Map;
            public int VehicleTech, RobotTech;
        }

        MatchState matchRequest;
        int matchRequestSequence;
        int matchPauseSeat = -1;
        double matchDeadline, connectionDeadline = -1;
        const double MapRequestCooldown = 180;
        readonly double[] mapRequestAvailableAt = new double[8];
        Task<IDuelPeer> recoveryTask;
        bool recoveryStarted, recoveryAwaitingSync;
        double retryRecoveryAt;
        string reconnectTcpAddress;
        int LocalSeat => IsHost ? 0 : 1;
        internal MatchRequestKind CurrentMatchRequest => matchRequest?.Kind ?? MatchRequestKind.None;
        internal bool MatchRequestOwner => matchRequest?.Seat == LocalSeat;
        internal bool MatchMapSuggested => matchRequest?.Suggested == true;
        internal string MatchMap => matchRequest?.Map;
        internal bool ConnectionInterrupted => connectionDeadline >= 0;
        internal bool MatchMenusBlocked => matchRequest != null || activeRoomVote != 0 || ConnectionInterrupted;
        internal bool MatchSuspended => MatchMenusBlocked || MatchPaused;
        internal static bool MatchPlayPaused => Active && (I.MatchPaused || I.MapChangeActive);
        internal bool MapChangeActive => CurrentMatchRequest == MatchRequestKind.Map || activeRoomVote != 0;
        internal bool MapChangeOwner => SkirmishRoom ? RoomPosition == roomVoteState?.Seat : MatchRequestOwner;
        internal string MapChangeRequester => SkirmishRoom ? roomVoteState?.Name : matchRequest?.Seat == 0 ? View?.HostName : View?.GuestName;
        internal string ProposedMap => SkirmishRoom ? roomVoteMap : MatchMap;
        internal double MapChangeSeconds => SkirmishRoom ? RoomVoteSeconds : MatchRequestSeconds;
        internal double MapChangeCooldownSeconds
        {
            get
            {
                double available = SkirmishRoom
                    ? IsHost ? RoomMapRequestAvailableAt(RoomHostId) : localRoomMapRequestAvailableAt
                    : mapRequestAvailableAt[LocalSeat];
                return Math.Max(0, available - Now);
            }
        }
        internal int ProposedVehicleTech => SkirmishRoom ? roomVoteState?.VehicleTech ?? 0 : matchRequest?.VehicleTech ?? 0;
        internal int ProposedRobotTech => SkirmishRoom ? roomVoteState?.RobotTech ?? 0 : matchRequest?.RobotTech ?? 0;

        void SetMapChangePause(bool paused)
        {
            if (Game && Game.World != null) Game.World.DuelOrdersPaused = paused || MatchPaused;
            if (Game) Game.PauseSkirmishPlanning(paused);
            if (paused) OpenZHud.I?.Production?.Close();
            Time.timeScale = paused || MatchPaused ? 0 : 1;
        }
        internal bool MatchPaused => matchPauseSeat >= 0;
        internal bool OwnsMatchPause => matchPauseSeat == LocalSeat;
        internal string MatchPausePlayer => matchPauseSeat == 0 ? View?.HostName : View?.GuestName;
        internal bool CanPauseMatch => CanRequest && !MatchSuspended && !restartPromptOpen && !restartPending;
        internal double MatchRequestSeconds => Math.Max(0, matchDeadline - Now);
        internal double ReconnectSeconds => Math.Max(0, connectionDeadline - Now);
        bool CanRequest => !SkirmishRoom && Connected && Playing && Game && !Game.World.GameOver && !ConnectionInterrupted;

        internal void RequestPauseMatch()
        {
            if (!CanPauseMatch) return;
            if (IsHost) PublishMatchPause(LocalSeat);
            else peer.Send(new DuelPacket(DuelMessage.MatchPause, new byte[] { 1 }));
        }

        internal void ResumePausedMatch()
        {
            if (!CanRequest || !OwnsMatchPause || MatchMenusBlocked) return;
            if (IsHost) PublishMatchPause(-1);
            else peer.Send(new DuelPacket(DuelMessage.MatchPause, new byte[] { 0 }));
        }

        void ReceiveMatchPause(DuelPacket packet)
        {
            if (!Playing || !Game || Game.World.GameOver || packet.Payload.Length != 1) return;
            byte value = packet.Payload[0];
            if (!IsHost)
            {
                if (value <= 2) SetMatchPause(value - 1);
            }
            else if (value == 1 && CanPauseMatch) PublishMatchPause(1);
            else if (value == 0 && matchPauseSeat == 1 && CanRequest && !MatchMenusBlocked) PublishMatchPause(-1);
        }

        void PublishMatchPause(int seat)
        {
            SetMatchPause(seat);
            // Deliver the final match time and battlefield state before announcing the pause.
            if (MatchPaused) SendState(DuelMessage.Delta, false);
            SendMatchPause();
        }

        void SendMatchPause() => peer.Send(new DuelPacket(DuelMessage.MatchPause, new[] { (byte)(matchPauseSeat + 1) }));

        void SetMatchPause(int seat)
        {
            if (matchPauseSeat == seat) return;
            matchPauseSeat = seat;
            if (Game && Game.World != null)
            {
                Game.World.DuelOrdersPaused = MatchPaused;
                if (MatchPaused) OpenZHud.I?.Production?.Close();
            }
            Time.timeScale = MatchPaused ? 0 : 1;
            Menus?.Refresh();
        }

        internal void RequestMapChange()
        {
            var wait = TimeSpan.FromSeconds(Math.Ceiling(MapChangeCooldownSeconds));
            if (wait.TotalSeconds > 0)
            {
                Menus.CloseOverlay();
                Menus.NotifyMatch($"You can propose another map in {wait.Minutes}:{wait.Seconds:00}.");
                return;
            }
            if (SkirmishRoom)
            {
                if (RoomCanVote && !MapChangeActive) SendRoomVoteRequest(new RoomVoteRequest { Revision = loadedRoomRevision });
            }
            else RequestMatchAction(MatchRequestKind.Map, RequestAction.Open);
        }
        internal void CancelMapChange()
        {
            if (SkirmishRoom) SendRoomVoteRequest(new RoomVoteRequest { Revision = loadedRoomRevision, Vote = activeRoomVote, Cancel = true });
            else RequestMatchAction(MatchRequestKind.Map, RequestAction.Cancel);
        }
        internal void RequestLobbyReturn() => RequestMatchAction(MatchRequestKind.Lobby, RequestAction.Open);
        internal void SubmitMapChange(string map, int vehicleTech, int robotTech)
        {
            if (SkirmishRoom)
                SendRoomVoteRequest(new RoomVoteRequest { Revision = loadedRoomRevision, Vote = activeRoomVote,
                    Map = map, VehicleTech = vehicleTech, RobotTech = robotTech });
            else RequestMatchAction(MatchRequestKind.Map, RequestAction.Suggest, map, vehicleTech, robotTech);
        }
        internal void AnswerMatchRequest(bool accept) => RequestMatchAction(CurrentMatchRequest, accept ? RequestAction.Accept : RequestAction.Decline);

        void RequestMatchAction(MatchRequestKind kind, RequestAction action, string map = null, int vehicleTech = 0, int robotTech = 0)
        {
            if (!CanRequest || kind == MatchRequestKind.None || action == RequestAction.Open && (matchRequest != null || restartPromptOpen || restartPending)) return;
            var edit = new MatchAction { Kind = kind, Action = action, Id = matchRequest?.Id ?? 0, Map = map,
                VehicleTech = vehicleTech, RobotTech = robotTech };
            if (IsHost) ApplyMatchAction(edit, 0);
            else
            {
                if (action == RequestAction.Open && kind != MatchRequestKind.Map)
                    DisplayMatchRequest(new MatchState { Kind = kind, Seat = 1 });
                SendJson(DuelMessage.MatchRequest, edit);
            }
        }

        void ReceiveMatchRequest(DuelPacket packet)
        {
            if (!Playing || !Game || Game.World.GameOver || packet.Payload.Length > 1024) return;
            if (IsHost && packet.Kind == DuelMessage.MatchRequest)
                ApplyMatchAction(ReadJson<MatchAction>(packet), 1);
            else if (!IsHost && packet.Kind == DuelMessage.MatchRequestState)
            {
                var state = ReadJson<MatchState>(packet);
                if (state == null || state.Id <= 0) return;
                if (state.Result == RequestResult.Pending) DisplayMatchRequest(state);
                else CompleteMatchRequest(state);
            }
        }

        void ApplyMatchAction(MatchAction edit, int seat)
        {
            if (edit == null || edit.Kind != MatchRequestKind.Map && edit.Kind != MatchRequestKind.Lobby) return;
            if (edit.Action == RequestAction.Open)
            {
                if (matchRequest != null) SendJson(DuelMessage.MatchRequestState, matchRequest);
                else if (restartPending || restartPromptOpen || edit.Kind == MatchRequestKind.Map && Now < mapRequestAvailableAt[seat])
                    SendJson(DuelMessage.MatchRequestState, new MatchState { Kind = edit.Kind, Seat = seat, Id = ++matchRequestSequence, Result = RequestResult.Expired });
                else
                {
                    DisplayMatchRequest(new MatchState { Kind = edit.Kind, Seat = seat, Id = ++matchRequestSequence });
                    SendJson(DuelMessage.MatchRequestState, matchRequest);
                }
                return;
            }
            var state = matchRequest;
            if (state == null || state.Id != edit.Id || state.Kind != edit.Kind || Now >= matchDeadline) return;
            if (seat == state.Seat && state.Kind == MatchRequestKind.Map && !state.Suggested)
            {
                if (edit.Action == RequestAction.Cancel) { ResolveMatchRequest(RequestResult.Expired); return; }
                else if (edit.Action == RequestAction.Suggest)
                {
                    string map = edit.Map;
                    if (map == View.Map || !ValidDuelMap(map) || !ValidMapTech(edit.VehicleTech, edit.RobotTech)) return;
                    state.Map = map;
                    state.VehicleTech = edit.VehicleTech;
                    state.RobotTech = edit.RobotTech;
                    state.Suggested = true;
                    matchDeadline = Now + MapChangeVote.DurationSeconds;
                }
                else return;
                SendJson(DuelMessage.MatchRequestState, state);
                Menus.Refresh();
            }
            if (seat != state.Seat && (state.Kind == MatchRequestKind.Lobby || state.Suggested))
            {
                if (edit.Action == RequestAction.Accept)
                    ResolveMatchRequest(state.Kind == MatchRequestKind.Map ? RequestResult.ChangeMap : RequestResult.ReturnToLobby);
                else if (edit.Action == RequestAction.Decline)
                    ResolveMatchRequest(RequestResult.Declined);
            }
        }

        static bool ValidDuelMap(string id) => DuelRules.IsMapAllowed(id);
        static bool ValidMapTech(int vehicleTech, int robotTech) =>
            vehicleTech >= 0 && vehicleTech <= 6 && robotTech >= 0 && robotTech <= 6;

        void DisplayMatchRequest(MatchState state)
        {
            bool opening = matchRequest == null || matchRequest.Id == 0 || matchRequest.Id != state.Id;
            bool voting = state.Kind == MatchRequestKind.Map && state.Suggested && matchRequest?.Suggested != true;
            matchRequest = state;
            if (voting) matchDeadline = Now + MapChangeVote.DurationSeconds;
            if (opening)
            {
                matchDeadline = Now + MapChangeVote.DurationSeconds;
                if (state.Kind == MatchRequestKind.Map)
                {
                    mapRequestAvailableAt[state.Seat] = Now + MapRequestCooldown;
                    SetMapChangePause(true);
                    if (IsHost) SendState(DuelMessage.Delta, false);
                }
                Menus.ShowMatchRequest();
            }
            else Menus.Refresh();
        }

        void ResolveMatchRequest(RequestResult result)
        {
            var state = matchRequest;
            state.Result = result;
            SendJson(DuelMessage.MatchRequestState, state);
            CompleteMatchRequest(state);
        }

        void CompleteMatchRequest(MatchState state)
        {
            matchRequest = null;
            if (state.Kind == MatchRequestKind.Map) SetMapChangePause(false);
            switch (state.Result)
            {
                case RequestResult.ChangeMap:
                    Array.Clear(mapRequestAvailableAt, 0, mapRequestAvailableAt.Length);
                    ResetMatchForRestart();
                    if (IsHost)
                    {
                        lobby.Restart(state.Map, state.VehicleTech, state.RobotTech);
                        PublishLobby();
                        LoadScene();
                    }
                    else Menus.ShowMultiplayerLoading(state.Map, waitingForHost: true);
                    break;
                case RequestResult.ReturnToLobby:
                    if (IsHost) { lobby.ReturnToLobby(); PublishLobby(); }
                    ReturnToLobbyScene();
                    break;
                default:
                    if (!ConnectionInterrupted) Menus.CloseOverlay();
                    if (state.Result == RequestResult.Declined && state.Seat == LocalSeat)
                        Menus.NotifyChat(state.Kind == MatchRequestKind.Map ? "Your map change was declined" : "Your return to lobby was declined");
                    break;
            }
        }

        void UpdateMatchRequests(bool interrupted)
        {
            if (interrupted || recoveryStarted || recoveryAwaitingSync)
            {
                if (!ConnectionInterrupted)
                {
                    connectionDeadline = Now + 30;
                    Menus.ShowConnectionInterrupted();
                }
                if (Now >= connectionDeadline) { LeaveInterruptedMatch(); return; }
                if (recoveryAwaitingSync && (peer.Closed || Now - lastReceive > 6))
                {
                    recoveryAwaitingSync = false;
                    recoveryStarted = true;
                }
                if (recoveryAwaitingSync) return;
                if (recoveryStarted || Now - lastReceive >= 8)
                    PollMatchRecovery();
                return;
            }
            if (ConnectionInterrupted)
            {
                connectionDeadline = -1;
                if (matchRequest == null) Menus.CloseOverlay();
                else Menus.ShowMatchRequest();
            }
            if (IsHost && matchRequest != null && Now >= matchDeadline) ResolveMatchRequest(RequestResult.Expired);
        }

        void PollMatchRecovery()
        {
            if (recoveryTask == null)
            {
                if (Now < retryRecoveryAt) return;
                recoveryStarted = true;
                if (CurrentMatchRequest == MatchRequestKind.Map) SetMapChangePause(false);
                matchRequest = null;
                peer.Dispose();
                try
                {
                    if (OnlineMode) recoveryTask = online.ReconnectAsync();
                    else if (IsHost)
                    {
                        listener = new TcpListener(IPAddress.Any, TcpPort);
                        listener.Start(1);
                        recoveryTask = AcceptTcp();
                    }
                    else recoveryTask = ConnectTcp(reconnectTcpAddress, TcpPort, localName);
                }
                catch (SocketException) { retryRecoveryAt = Now + .75; }
                return;
            }
            if (!recoveryTask.IsCompleted) return;
            var completed = recoveryTask;
            recoveryTask = null;
            listener?.Stop();
            listener = null;
            if (completed.Status != TaskStatus.RanToCompletion)
            {
                _ = completed.Exception;
                retryRecoveryAt = Now + .75;
                return;
            }
            peer = completed.Result; lastReceive = lastPing = Now;
            recoveryStarted = false;
            recoveryAwaitingSync = true;
            if (IsHost)
            {
                pendingEvents.Clear();
                SendState(DuelMessage.MatchReconnectState, true);
                SendMatchPause();
            }
        }

        void ReceiveMatchRecovery(DuelPacket packet)
        {
            if (!recoveryAwaitingSync || !Game || !Playing) return;
            if (!IsHost && packet.Kind == DuelMessage.MatchReconnectState)
            {
                receiver = new DuelDeltaReceiver();
                ReceiveState(packet);
                DuelReplica.Apply(Game.World, receiver.Snapshot());
                peer.Send(new DuelPacket(DuelMessage.MatchReconnectReady, Array.Empty<byte>()));
                return;
            }
            if (packet.Kind != DuelMessage.MatchReconnectReady || packet.Payload.Length != 0) return;
            if (IsHost) peer.Send(new DuelPacket(DuelMessage.MatchReconnectReady, Array.Empty<byte>()));
            recoveryAwaitingSync = false;
            connectionDeadline = -1;
            Menus.CloseOverlay();
        }

        internal void LeaveInterruptedMatch()
        {
            Close();
            Menus.CloseOverlay();
            SceneManager.LoadSceneAsync("MainMenu");
        }

        internal void ProofInterruptMatch() => peer.Dispose();

        void ClearMatchRequest()
        {
            SetMatchPause(-1);
            if (CurrentMatchRequest == MatchRequestKind.Map) SetMapChangePause(false);
            matchRequest = null;
            connectionDeadline = -1;
            recoveryStarted = recoveryAwaitingSync = false;
            retryRecoveryAt = 0;
            if (recoveryTask != null)
                _ = recoveryTask.ContinueWith(t => { if (t.Status == TaskStatus.RanToCompletion) t.Result.Dispose(); else _ = t.Exception; });
            recoveryTask = null;
        }
    }
}
