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
        enum RequestAction { Open, Select, Suggest, Accept, Decline }
        enum RequestResult { Pending, Declined, Expired, ChangeMap, ReturnToLobby }

        [Serializable] sealed class MatchAction
        {
            public MatchRequestKind Kind;
            public RequestAction Action;
            public int Id;
            public string Map;
        }

        [Serializable] sealed class MatchState
        {
            public MatchRequestKind Kind;
            public RequestResult Result;
            public int Id, Seat;
            public bool Suggested;
            public string Map;
        }

        MatchState matchRequest;
        int matchRequestSequence;
        int matchPauseSeat = -1;
        double matchDeadline, connectionDeadline = -1;
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
        internal bool MatchMenusBlocked => matchRequest != null || ConnectionInterrupted;
        internal bool MatchSuspended => MatchMenusBlocked || MatchPaused;
        internal static bool MatchPlayPaused => Active && I.MatchPaused;
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

        internal void RequestMapChange() => RequestMatchAction(MatchRequestKind.Map, RequestAction.Open);
        internal void RequestLobbyReturn() => RequestMatchAction(MatchRequestKind.Lobby, RequestAction.Open);
        internal void SelectMatchMap(string map) => RequestMatchAction(MatchRequestKind.Map, RequestAction.Select, map);
        internal void SuggestMatchMap() => RequestMatchAction(MatchRequestKind.Map, RequestAction.Suggest);
        internal void AnswerMatchRequest(bool accept) => RequestMatchAction(CurrentMatchRequest, accept ? RequestAction.Accept : RequestAction.Decline);

        void RequestMatchAction(MatchRequestKind kind, RequestAction action, string map = null)
        {
            if (!CanRequest || kind == MatchRequestKind.None || action == RequestAction.Open && (matchRequest != null || restartPromptOpen || restartPending)) return;
            var edit = new MatchAction { Kind = kind, Action = action, Id = matchRequest?.Id ?? 0, Map = map };
            if (IsHost) ApplyMatchAction(edit, 0);
            else
            {
                if (action == RequestAction.Open)
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
                else if (restartPending || restartPromptOpen)
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
                if (edit.Action == RequestAction.Select && edit.Map != View.Map && ValidDuelMap(edit.Map)) state.Map = edit.Map;
                else if (edit.Action == RequestAction.Suggest && state.Map != null) state.Suggested = true;
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

        void DisplayMatchRequest(MatchState state)
        {
            bool opening = matchRequest == null || matchRequest.Id == 0 || matchRequest.Id != state.Id;
            matchRequest = state;
            if (opening)
            {
                matchDeadline = Now + 30;
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
            switch (state.Result)
            {
                case RequestResult.ChangeMap:
                    ResetMatchForRestart();
                    if (IsHost)
                    {
                        lobby.Restart(state.Map);
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
