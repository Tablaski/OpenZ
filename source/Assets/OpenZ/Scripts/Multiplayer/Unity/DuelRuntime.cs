using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ.Multiplayer
{
    [Serializable] public sealed class DuelArmyData
    {
        public double BuildDuration;
        public string[] Units, Turrets;
        public static DuelArmyData From(DuelArmySettings a) => new DuelArmyData { BuildDuration = a.BuildDuration, Units = a.StartingUnits.ToArray(), Turrets = (string[])a.FortTurrets.Clone() };
        public DuelArmySettings Read()
        {
            var a = new DuelArmySettings { BuildDuration = BuildDuration, FortTurrets = Turrets };
            if (Units == null || Units.Length > 4) throw new InvalidDataException("Invalid starting army.");
            a.StartingUnits.AddRange(Units); return a;
        }
    }
    [Serializable] public sealed class DuelLobbyData
    {
        public string Map, HostName, GuestName;
        public string HostRelease, GuestRelease;
        public int Revision, Phase, HandicapSlot;
        public bool HostReady, GuestReady, HandicapEnabled;
        public double TerritoryBonus, MinimumBuildSeconds, FortLoserMaximum, TerritoryReductionCap;
        public DuelArmyData Common, Handicap;
        public string TankDynamics, RobotDynamics;
        public int UnitPreset;
        public string[] AllowedUnits;
        public string[] RedFortTurrets, BlueFortTurrets;
        public string ProposalJson;
        public DuelRules ProposedRules => string.IsNullOrEmpty(ProposalJson) ? null : JsonUtility.FromJson<DuelLobbyData>(ProposalJson).ReadRules();
        public int ProposalId;
        public static DuelLobbyData From(DuelLobby l)
        {
            var r = l.Rules;
            return new DuelLobbyData { Map = r.Map, HostName = l.Name(0), GuestName = l.Name(1), Revision = l.Revision,
                Phase = (int)l.Phase, HostReady = l.Ready(0), GuestReady = l.Ready(1), HandicapEnabled = r.HandicapEnabled,
                HandicapSlot = r.HandicapSlot, TerritoryBonus = r.TerritoryBonus, MinimumBuildSeconds = r.MinimumBuildSeconds, FortLoserMaximum = r.FortLoserMaximum, TerritoryReductionCap = r.TerritoryReductionCap,
                Common = DuelArmyData.From(r.Common), Handicap = DuelArmyData.From(r.Handicap),
                RedFortTurrets = r.FortTurretsFor(0), BlueFortTurrets = r.FortTurretsFor(1),
                TankDynamics = r.TankDynamics, RobotDynamics = r.RobotDynamics, UnitPreset = (int)r.UnitPreset, AllowedUnits = r.AllowedUnits.ToArray() };
        }
        public DuelRules ReadRules()
        {
            var rules = new DuelRules { Map = Map, TerritoryBonus = TerritoryBonus, MinimumBuildSeconds = MinimumBuildSeconds, FortLoserMaximum = FortLoserMaximum, TerritoryReductionCap = TerritoryReductionCap,
                Common = Common.Read(), Handicap = Handicap.Read(), HandicapEnabled = HandicapEnabled, HandicapSlot = HandicapSlot,
                RedFortTurrets = RedFortTurrets == null ? null : (string[])RedFortTurrets.Clone(), BlueFortTurrets = BlueFortTurrets == null ? null : (string[])BlueFortTurrets.Clone(),
                TankDynamics = TankDynamics ?? "", RobotDynamics = RobotDynamics ?? "", UnitPreset = (DuelUnitPreset)UnitPreset,
                AllowedUnits = AllowedUnits == null ? new List<string>(DuelUnitRoster.All) : new List<string>(AllowedUnits) };
            rules.Validate(); return rules;
        }
    }
    [Serializable] sealed class DuelReadyData { public int Revision; public bool Ready; public string Release; public bool ReleaseOnly; }
    [Serializable] sealed class DuelProposalData { public int Revision; public bool Cancel; public DuelLobbyData Rules; }
    [Serializable] public sealed class DuelEventData { public ZEvent[] Events; }

    [DefaultExecutionOrder(-1000)]
    public sealed class DuelRuntime : MonoBehaviour
    {
        public static DuelRuntime I { get; private set; }
        public static bool Active => I && I.session;
        public static bool CapturesInput => I && I.Menus && I.Menus.CapturesInput;
        public DuelMenus Menus { get; private set; }
        public DuelVoice Voice { get; private set; }
        public string VoiceSecret => secret;
        public IPAddress PeerAddress => (peer as DuelTcpPeer)?.RemoteAddress;
        public DuelRtcPeer OnlinePeer => peer as DuelRtcPeer;
        public bool OnlineMode { get; private set; }
        public string OnlineRoom => online?.RoomCode;
        public string ConnectionStatus => online != null && Connecting ? online.Status : Status;
        public int TcpPort { get; private set; }
        public bool IsHost { get; private set; }
        public bool Playing { get; private set; }
        public bool Connecting => connectTask != null;
        public bool Connected => peer != null && !peer.Closed;
        public bool InMatch => session;
        public double Countdown => startAt < 0 ? -1 : Math.Max(0, startAt - Now);
        public string Status { get; private set; } = "";
        public float PingMs { get; private set; }
        public DuelLobbyData View { get; private set; }
        public DuelRules Rules => View?.ReadRules() ?? Defaults("L02");
        public readonly List<string> Chat = new List<string>();
        public ZGame Game { get; private set; }
        public string Invitation(string address, int port) => OnlineMode ? DuelOnlineSettings.ValidateService(DuelOnlineSettings.CurrentService) + "/" + OnlineRoom : "openz://join?address=" + Uri.EscapeDataString(address) + "&port=" + port + "&invite=" + secret;
        IDuelPeer peer;
        DuelOnline online;
        TcpListener listener;
        CancellationTokenSource cancellation;
        Task<IDuelPeer> connectTask;
        DuelLobby lobby;
        string secret, localName;
        string guestRelease;
        static string LocalRelease => OzForkVersion.Label.Replace('\n', '|');
        public string ReleaseWarning => View == null ? "" : string.IsNullOrEmpty(View.HostRelease) || string.IsNullOrEmpty(View.GuestRelease)
            ? "WARNING: peer release unverified. You may still play at your own risk."
            : (View.HostRelease != View.GuestRelease || View.HostRelease.Contains("UNSTAMPED")) ? "WARNING: different releases. You may still play at your own risk." : "";
        bool session, loadStarted, sentGameOver;
        bool restartPending, restartPromptOpen;
        bool finalStatsSent, localEndReady, remoteEndReady;
        public bool FinalStatsReady { get; private set; }
        double restartDeadline;
        bool savedSoloRules, soloCrews, soloFortGun;
        double startAt = -1, lastReceive, lastPing, pendingPing = -1, lastSnapshot;
        long commandSequence;
        DuelReplica.CaptureBuffer capture = new DuelReplica.CaptureBuffer();
        double perfAt;
        long perfSendTicks, perfReceiveTicks, perfSendBytes, perfReceiveBytes;
        int perfSent, perfReceived, perfFrames;
        int perfGcStart;
        float perfWorstFrame;
        int QueuedSendBytes => peer is DuelRtcPeer rtc ? rtc.PendingSendBytes : peer is DuelTcpPeer tcp ? tcp.PendingSendBytes : 0;
        int QueuedReceiveBytes => peer is DuelRtcPeer rtc ? rtc.PendingReceiveBytes : peer is DuelTcpPeer tcp ? tcp.PendingReceiveBytes : 0;
        readonly DuelDeltaSender sender = new DuelDeltaSender();
        DuelDeltaReceiver receiver = new DuelDeltaReceiver();
        DuelOrderReceiver guestOrders;
        readonly List<ZEvent> pendingEvents = new List<ZEvent>();
        static double Now => Time.realtimeSinceStartupAsDouble;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (I) return;
            var go = new GameObject("OpenZ_1v1"); DontDestroyOnLoad(go); I = go.AddComponent<DuelRuntime>();
        }
        void Awake() { I = this; Menus = gameObject.AddComponent<DuelMenus>(); Voice = gameObject.AddComponent<DuelVoice>(); SceneManager.sceneLoaded += SceneLoaded; }
        void Start()
        {
            if (!Environment.GetCommandLineArgs().Any(a => a.StartsWith("-oz-duel-", StringComparison.Ordinal))) DuelInvitationLinks.Register();
            string invitation = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("openz://join?", StringComparison.OrdinalIgnoreCase));
            if (invitation != null)
            {
                if (invitation.IndexOf("room=", StringComparison.OrdinalIgnoreCase) >= 0) ConnectOnline(PlayerPrefs.GetString("oz_duel_name", "PLAYER"), false, invitation);
                else Menus.SetInvitation(invitation);
            }
        }
        void OnDestroy() { Close(); SceneManager.sceneLoaded -= SceneLoaded; if (I == this) I = null; }
        public static DuelRules Defaults(string map)
        {
            var text = Resources.Load<TextAsset>("OpenZ/Maps/" + map + "_grid");
            if (!text) throw new InvalidOperationException("Mission missing: " + map);
            return new DuelRules { Map = map, Common = DuelMissionDefaults.Read(ZMapData.FromJson(text.text)) };
        }
        string Fingerprint()
        {
            var text = new StringBuilder("OpenZ-VK-0.2-duel-6-proposals-fort-mounts\n" + DuelReplica.SchemaHash);
            text.Append(Resources.Load<TextAsset>("OpenZ/default_settings").text);
            foreach (string map in new[] { "L02", "L07", "L16", "L17" }.Concat(OzShittyMapCatalog.All.Where(m=>m.Duel).Select(m=>m.Id))) text.Append(Resources.Load<TextAsset>("OpenZ/Maps/" + map + "_grid").text);
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
        }
        public void Host(string name, int port)
        {
            if (port < 1 || port > 65534) throw new ArgumentOutOfRangeException(nameof(port));
            Close(); IsHost = true; TcpPort = port; localName = name; secret = DuelTcpPeer.NewInvitationSecret();
            lobby = new DuelLobby(); lobby.Join(0, name); lobby.ChangeRules(0, HostDefaults()); View = DuelLobbyData.From(lobby);
            cancellation = new CancellationTokenSource();
            try { listener = new TcpListener(IPAddress.Any, port); listener.Start(1); connectTask = AcceptTcp(); Status = "Waiting for the other player…"; }
            catch (Exception error) { Fail(error.Message); }
            Menus.ShowLobby();
        }
        public void Join(string name, string invitation)
        {
            Close(); IsHost = false; localName = name; cancellation = new CancellationTokenSource();
            try
            {
                var uri = new Uri(invitation.Trim());
                if (uri.Scheme != "openz" || uri.Host != "join") throw new ArgumentException("Expected an OpenZ invitation.");
                var args = uri.Query.TrimStart('?').Split('&').Select(p => p.Split(new[] { '=' }, 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
                secret = args["invite"]; int port = int.Parse(args["port"]);
                if (port < 1 || port > 65534) throw new ArgumentOutOfRangeException(nameof(port));
                TcpPort = port;
                connectTask = ConnectTcp(args["address"], port, name);
                Status = "Connecting…"; Menus.ShowLobby();
            }
            catch (Exception error) { Fail(error.Message); }
        }
        async Task<IDuelPeer> AcceptTcp() => await DuelTcpPeer.AcceptAsync(listener, secret, Fingerprint(), cancellation.Token);
        async Task<IDuelPeer> ConnectTcp(string address, int port, string name) => await DuelTcpPeer.ConnectAsync(address, port, secret, Fingerprint(), name, cancellation.Token);
        public void ConnectOnline(string name, bool host, string invitation)
        {
            Close(); IsHost = host; OnlineMode = true; localName = name;
            try
            {
                if (host) { lobby = new DuelLobby(); lobby.Join(0, name); lobby.ChangeRules(0, HostDefaults()); View = DuelLobbyData.From(lobby); }
                cancellation = new CancellationTokenSource(); online = new DuelOnline(cancellation.Token);
                string service = DuelOnlineSettings.CurrentService;
                connectTask = online.ConnectAsync(service, host, invitation, Fingerprint(), name,
                    Environment.GetCommandLineArgs().Contains("-oz-duel-relay-only"));
                Status = "Connecting…"; Menus.ShowLobby();
            }
            catch (Exception error) { Fail(error.Message); }
        }
        public void ChangeRules(DuelRules rules)
        {
            if (!IsHost || lobby == null || lobby.Phase != DuelPhase.Lobby) return;
            try { rules.Validate(); ZWorld.DuelProfile(rules.TankDynamics, ZBehaviorFamily.Tanks); ZWorld.DuelProfile(rules.RobotDynamics, ZBehaviorFamily.Robots); lobby.ChangeRules(0, rules); PublishLobby(); }
            catch (Exception error) { Status = error.Message; Menus.Refresh(); }
        }
        static DuelRules HostDefaults()
        {
            var rules = Defaults("L02");
            rules.TankDynamics = OzBehaviorLibrary.Selected(ZBehaviorFamily.Tanks).Encode();
            rules.RobotDynamics = OzBehaviorLibrary.Selected(ZBehaviorFamily.Robots).Encode();
            return rules;
        }
        static void ValidateArmies(DuelRules rules)
        {
            rules.Validate();
            var settings = ZSettings.Parse(Resources.Load<TextAsset>("OpenZ/default_settings").text);
            for (int slot = 0; slot < 2; slot++)
            {
                var army = rules.EffectiveFor(slot);
                int count = army.FortTurrets.Count(t => !string.IsNullOrEmpty(t)) + army.StartingUnits.Sum(t =>
                    ZBuildLists.IsRobot(t) ? Math.Max(1, settings.Unit(t)?.group_amount ?? 1) : 1);
                if (count == 0 || count > ZWorld.MAX_UNITS_PER_TEAM) throw new ArgumentException("Each team must start with 1 to 70 units, including squad members and turrets.");
            }
        }
        public void Ready()
        {
            if (!Connected || View == null || View.Phase != (int)DuelPhase.Lobby) return;
            try { ValidateArmies(Rules); }
            catch (Exception error) { Status = error.Message; Menus.Refresh(); return; }
            if (IsHost) { lobby.SetReady(0, View.Revision, !View.HostReady); PublishLobby(); }
            else SendJson(DuelMessage.Ready, new DuelReadyData { Revision = View.Revision, Ready = !View.GuestReady, Release = LocalRelease });
        }
        public void SendChat(string message)
        {
            message = new string((message ?? "").Where(c => !char.IsControl(c)).Take(240).ToArray()).Trim();
            if (message.Length == 0 || !Connected) return;
            AddChat(localName + " : " + message); peer.Send(new DuelPacket(DuelMessage.Chat, Encoding.UTF8.GetBytes(message)));
        }
        void AddChat(string message) { Chat.Add(message.Replace("<", "‹").Replace(">", "›")); if (Chat.Count > 30) Chat.RemoveAt(0); Menus.Refresh(); }
        void PublishLobby()
        {
            View = DuelLobbyData.From(lobby); View.HostRelease = LocalRelease; View.GuestRelease = guestRelease;
            View.ProposalId = lobby.ProposalId;
            if (lobby.Proposal != null) View.ProposalJson = JsonUtility.ToJson(RulesData(lobby.Proposal));
            if (Connected) SendJson(DuelMessage.Lobby, View); Menus.Refresh();
        }
        static DuelLobbyData RulesData(DuelRules rules)
        {
            var holder = new DuelLobby(); holder.ChangeRules(0, rules); return DuelLobbyData.From(holder);
        }
        double lastProposalAt = -10;
        public double ProposalWait => Math.Max(0, 3 - (Now - lastProposalAt));
        public void ProposeRules(DuelRules rules, int revision)
        {
            if (IsHost || !Connected || View == null || View.Phase != (int)DuelPhase.Lobby || revision != View.Revision || ProposalWait > 0) return;
            rules.Validate();
            SendJson(DuelMessage.ProposeRules, new DuelProposalData { Revision = revision, Rules = RulesData(rules) });
            lastProposalAt = Now;
        }
        public void WithdrawProposal() { if (!IsHost && Connected) SendJson(DuelMessage.ProposeRules, new DuelProposalData { Cancel = true }); }
        public void ResolveProposal(int id, bool accept)
        {
            if (!IsHost || lobby == null || lobby.Phase != DuelPhase.Lobby) return;
            if (lobby.ResolveProposal(0, id, accept)) PublishLobby();
        }
        void ReceiveProposal(DuelPacket packet)
        {
            if (!IsHost || lobby == null || lobby.Phase != DuelPhase.Lobby) return;
            var request = ReadJson<DuelProposalData>(packet);
            if (request == null) throw new InvalidDataException("Invalid proposal.");
            if (request.Cancel) { lobby.CancelProposal(1); PublishLobby(); return; }
            if (Now - lastProposalAt < 1) return;
            lastProposalAt = Now;
            if (request.Revision == lobby.Revision)
            {
                try
                {
                    var proposed = request.Rules.ReadRules();
                    ZWorld.DuelProfile(proposed.TankDynamics, ZBehaviorFamily.Tanks); ZWorld.DuelProfile(proposed.RobotDynamics, ZBehaviorFamily.Robots);
                    lobby.Propose(1, request.Revision, proposed);
                }
                catch (ArgumentException error) { Status = error.Message; }
            }
            PublishLobby();
        }
        void SendJson<T>(DuelMessage kind, T value) => peer?.Send(new DuelPacket(kind, Encoding.UTF8.GetBytes(JsonUtility.ToJson(value))));
        static T ReadJson<T>(DuelPacket packet)
        {
            if (packet.Payload.Length > 2 * 1024 * 1024) throw new InvalidDataException("Control message too large.");
            return JsonUtility.FromJson<T>(Encoding.UTF8.GetString(packet.Payload));
        }
        void Update()
        {
            try
            {
                if (connectTask != null && connectTask.IsCompleted)
                {
                    var task = connectTask; connectTask = null; peer = task.GetAwaiter().GetResult(); listener?.Stop(); listener = null;
                    lastReceive = Now; Status = OnlineMode ? (OnlinePeer.UsesRelay ? "Connected via relay" : "Connected directly") : "Connected";
                    if (IsHost) { lobby.Join(1, peer.RemoteName); PublishLobby(); } else Menus.Refresh();
                }
                if (peer == null) return;
                if (restartPromptOpen && (!Playing || !Game || Game.World.GameOver)) OzConfirm.Dismiss();
                if (IsHost && restartPending && Now > restartDeadline) { if (restartPromptOpen) OzConfirm.Dismiss(); else FinishRestartRequest(false); }
                if (peer.Closed) { Fail(peer.Failure ?? "Connection closed"); return; }
                int count = 0;
                long receiveStart = System.Diagnostics.Stopwatch.GetTimestamp();
                while (peer != null && count++ < 256 && peer.TryReceive(out var packet))
                {
                    lastReceive = Now; Handle(packet);
                    // Preserve every ordered delta; resume queued work next frame instead of monopolizing rendering.
                    if ((System.Diagnostics.Stopwatch.GetTimestamp() - receiveStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency >= 3) break;
                }
                if (peer == null) return;
                double responseTimeout = session && !Playing ? 180 : 15;
                if (Now - lastReceive > responseTimeout) { Fail("Connection lost. Your opponent is no longer responding."); return; }
                if (Now - lastPing > 1)
                {
                    lastPing = pendingPing = Now;
                    peer.Send(new DuelPacket(DuelMessage.Ping, BitConverter.GetBytes(pendingPing)));
                }
                if (IsHost && lobby.Phase == DuelPhase.Loading && !loadStarted)
                { loadStarted = true; session = true; LoadScene(); }
                if (session && !Playing && startAt >= 0 && Now >= startAt)
                {
                    Playing = true; if (IsHost) lobby.Advance(Now); Menus.CloseOverlay();
                }
                if (Game && session && !IsHost) SyncClientViews();
                if (Playing) LogPerformance();
            }
            catch (Exception error) { Debug.LogException(error); Fail(error.Message); }
        }
        void Handle(DuelPacket packet)
        {
            switch (packet.Kind)
            {
                case DuelMessage.Ping:
                    if (packet.Payload.Length != 8) throw new InvalidDataException("Invalid ping.");
                    peer.Send(new DuelPacket(DuelMessage.Pong, packet.Payload)); break;
                case DuelMessage.Pong:
                    if (packet.Payload.Length == 8 && BitConverter.ToDouble(packet.Payload, 0) == pendingPing)
                    { PingMs = (float)((Now - pendingPing) * 1000); pendingPing = -1; } break;
                case DuelMessage.Chat:
                    if (packet.Payload.Length > 1024) throw new InvalidDataException("Chat too large.");
                    string message = Encoding.UTF8.GetString(packet.Payload);
                    if (message.Length > 240 || message.Any(char.IsControl)) throw new InvalidDataException("Invalid chat.");
                    string receivedChat = (IsHost ? View.GuestName : View.HostName) + " : " + message;
                    AddChat(receivedChat); Menus.NotifyChat(receivedChat.Replace("<", "‹").Replace(">", "›")); break;
                case DuelMessage.Lobby:
                    if (IsHost) throw new InvalidDataException("Guest cannot publish a lobby.");
                    View = ReadJson<DuelLobbyData>(packet); View.ReadRules();
                    if (View.GuestRelease != LocalRelease)
                        SendJson(DuelMessage.Ready, new DuelReadyData { Revision = View.Revision, Ready = View.GuestReady, Release = LocalRelease, ReleaseOnly = true });
                    Menus.Refresh(); break;
                case DuelMessage.ProposeRules:
                    ReceiveProposal(packet); break;
                case DuelMessage.Ready:
                    if (!IsHost || lobby.Phase != DuelPhase.Lobby) break;
                    var ready = ReadJson<DuelReadyData>(packet);
                    guestRelease = string.IsNullOrEmpty(ready.Release) ? null : ready.Release;
                    if (guestRelease != null && guestRelease.Length > 512) throw new InvalidDataException("Invalid release identity.");
                    // A click sent against an obsolete revision is ignored and the current lobby is resent.
                    if (!ready.ReleaseOnly && ready.Revision == lobby.Revision)
                    {
                        try { if (ready.Ready) ValidateArmies(lobby.Rules); lobby.SetReady(1, ready.Revision, ready.Ready); }
                        catch (ArgumentException error) { Status = error.Message; }
                    }
                    PublishLobby(); break;
                case DuelMessage.Load:
                    if (IsHost || (session && (Playing || Game)) || View == null) throw new InvalidDataException("Unexpected load.");
                    ReceiveState(packet); session = true; loadStarted = true; LoadScene(); break;
                case DuelMessage.Loaded:
                    if (!IsHost || !session || lobby.Phase != DuelPhase.Loading) break;
                    lobby.MarkLoaded(1, View.Revision, Now); ScheduleStart(); break;
                case DuelMessage.Start:
                    if (IsHost || !session || !Game || packet.Payload.Length != 8) throw new InvalidDataException("Unexpected start.");
                    startAt = Now + Math.Max(0, BitConverter.ToDouble(packet.Payload, 0) - PingMs / 2000d); break;
                case DuelMessage.Command:
                    if (!IsHost || !Playing || !Game) break;
                    guestOrders.Execute(ReadJson<DuelOrder>(packet)); break;
                case DuelMessage.Delta:
                    if (IsHost || !session || !Game) throw new InvalidDataException("Unexpected replica state.");
                    ReceiveState(packet); break;
                case DuelMessage.MatchStats:
                    if (IsHost || !Game || !Game.World.GameOver) throw new InvalidDataException("Unexpected final statistics.");
                    ReadJson<DuelMatchStats>(packet).Apply(FindFirstObjectByType<OpenZHud>().Stats); FinalStatsReady = true; break;
                case DuelMessage.EndReady:
                    if (!IsHost || !Playing || !Game || !Game.World.GameOver) break;
                    remoteEndReady = true; TryReturnToLobby(); break;
                case DuelMessage.ReturnLobby:
                    if (IsHost || !Game || !Game.World.GameOver || !localEndReady) throw new InvalidDataException("Unexpected lobby return.");
                    ReturnToLobbyScene(); break;
                case DuelMessage.RestartRequest:
                    if (!Playing || !Game || Game.World.GameOver) break;
                    if (IsHost) BeginRestartRequest(false);
                    else AskRestart(() => peer.Send(new DuelPacket(DuelMessage.RestartReply, new byte[] { 1 })), () => peer.Send(new DuelPacket(DuelMessage.RestartReply, new byte[] { 0 })));
                    break;
                case DuelMessage.RestartReply:
                    if (!IsHost || packet.Payload.Length != 1) throw new InvalidDataException("Invalid restart reply.");
                    if (restartPending) FinishRestartRequest(packet.Payload[0] == 1);
                    break;
                case DuelMessage.RestartBegin:
                    if (IsHost || !Playing) throw new InvalidDataException("Unexpected restart.");
                    ResetMatchForRestart(); Menus.ShowLoading(); break;
                case DuelMessage.RestartNotice:
                    if (IsHost || packet.Payload.Length > 240) throw new InvalidDataException("Invalid restart notice.");
                    if (restartPromptOpen) OzConfirm.Dismiss();
                    Menus.NotifyChat(Encoding.UTF8.GetString(packet.Payload)); break;
                case DuelMessage.Surrender:
                    if (IsHost && Playing && Game) Game.World.DuelSurrender(1); break;
                case DuelMessage.Disconnect: Fail("Your opponent left the match."); break;
                default: throw new InvalidDataException("Unexpected duel message.");
            }
        }
        void LoadScene()
        {
            Status = "Loading both teams…"; Menus.ShowLoading();
            string scene = View.Map == "L02" ? "L02_Desert" : View.Map == "L07" ? "L07_Volcanic" : View.Map == "L16" ? "L16_Jungle" : View.Map == "L19" ? "L19_Mayhem_Converted" : "L17_CarPark";
            OzPlayBlue.Disarm("duel"); SceneManager.LoadSceneAsync(OzShittyMapCatalog.Find(View.Map)?.Scene ?? scene);
        }
        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu" && session) Close();
            if (session && Game) StartCoroutine(FinishLoading()); else Menus.Refresh();
        }
        IEnumerator FinishLoading()
        {
            yield return null;
            if (!session || !Connected || !Game) yield break;
            lastReceive = Now;
            Game.DuelBindStartingViews();
            if (!IsHost)
            {
                var camera = FindFirstObjectByType<RtsCamera>();
                var fort = Game.World.Objects.FirstOrDefault(o => o.IsFort && o.Owner == Team.Blue);
                if (camera && fort != null) { camera.focus = ZGame.PxToWorld(fort.CenterX, fort.CenterY); camera.Place(); }
                peer.Send(new DuelPacket(DuelMessage.Loaded, Array.Empty<byte>()));
            }
            else { lobby.MarkLoaded(0, View.Revision, Now); ScheduleStart(); }
        }
        void ScheduleStart()
        {
            if (startAt >= 0 || lobby.StartAt < 0) return;
            startAt = lobby.StartAt;
            peer.Send(new DuelPacket(DuelMessage.Start, BitConverter.GetBytes(Math.Max(0, startAt - Now))));
        }
        public static void PrepareGame(ZGame game)
        {
            if (!Active) return;
            if (!I.savedSoloRules)
            {
                I.soloCrews = ZWorld.L17CrewsDeviation; I.soloFortGun = ZWorld.FortDoorGunDeviation; I.savedSoloRules = true;
            }
            game.player = I.IsHost ? Team.Red : Team.Blue; game.blueBot = game.redBot = false;
            game.gameDifficulty = GameDifficulty.Hard; game.mayhem = false; game.gameSpeed = 1;
            ZWorld.L17CrewsDeviation = false; ZWorld.FortDoorGunDeviation = false;
        }
        public static void WorldCreated(ZGame game)
        {
            if (!Active) return;
            var runtime = I; runtime.Game = game; game.World.ConfigureDuel(runtime.Rules, !runtime.IsHost);
            if (runtime.IsHost)
            {
                game.World.DuelApplyStartingArmies(); runtime.guestOrders = new DuelOrderReceiver(game.World, 1);
                runtime.SendState(DuelMessage.Load, true);
            }
            else
            {
                game.World.DuelResetReplica(); DuelReplica.Apply(game.World, runtime.receiver.Snapshot());
                game.World.DuelForward = order => { if (!runtime.Playing) return false; order.Sequence = ++runtime.commandSequence; runtime.SendJson(DuelMessage.Command, order); return runtime.Connected; };
            }
        }
        public static bool Drive(ZGame game)
        {
            ZGame.RuleTicksLastFrame = 0; ZGame.RuleMsLastFrame = 0;
            if (!Active) return false;
            game.gameSpeed = 1; game.paused = false; Time.timeScale = 1;
            if (!I.Playing || !I.Connected) return true;
            if (I.IsHost)
            {
                game.DuelAdvance(Time.unscaledDeltaTime);
                if (I.pendingEvents.Count > 4096) { I.Fail("Too many pending network events."); return true; }
                if ((Now - I.lastSnapshot >= 1d / 15 && I.QueuedSendBytes < 256 * 1024) || (game.World.GameOver && (!I.sentGameOver || I.pendingEvents.Count > 0)))
                { I.lastSnapshot = Now; I.SendState(DuelMessage.Delta, false); if (game.World.GameOver) I.sentGameOver = true; }
            }
            return true;
        }
        public static void CaptureEvents(ZGame game)
        {
            if (Active && I.IsHost && game && game.World != null) I.pendingEvents.AddRange(game.World.Events);
        }
        void SendState(DuelMessage kind, bool full)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            var delta = DuelDeltaCodec.Encode(sender.CaptureImmutable(capture.Capture(Game.World), full));
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(delta.Length); writer.Write(delta);
                writer.Write(JsonUtility.ToJson(new DuelEventData { Events = pendingEvents.ToArray() }));
                perfSendBytes += stream.Length;
                if (!peer.Send(new DuelPacket(kind, stream.ToArray()))) throw new IOException("The connection cannot keep up with updates.");
            }
            pendingEvents.Clear(); perfSent++; perfSendTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started;
        }
        void ReceiveState(DuelPacket packet)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            using (var stream = new MemoryStream(packet.Payload, false)) using (var reader = new BinaryReader(stream))
            {
                int length = reader.ReadInt32();
                if (length < 0 || length > stream.Length - stream.Position) throw new InvalidDataException("Invalid state frame.");
                var delta = DuelDeltaCodec.Decode(reader.ReadBytes(length));
                if (!receiver.ApplyOwned(delta)) throw new InvalidDataException("Missing state baseline.");
                var events = JsonUtility.FromJson<DuelEventData>(reader.ReadString());
                if (stream.Position != stream.Length || events?.Events == null || events.Events.Length > 4096) throw new InvalidDataException("Invalid event frame.");
                if (Game)
                {
                    if (packet.Kind == DuelMessage.Delta) DuelReplica.ApplyDelta(Game.World, delta);
                    Game.World.Events.AddRange(events.Events);
                }
            }
            perfReceived++; perfReceiveBytes += packet.Payload.Length;
            perfReceiveTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started;
        }
        void LogPerformance()
        {
            perfFrames++; perfWorstFrame = Math.Max(perfWorstFrame, Time.unscaledDeltaTime * 1000);
            if (perfAt == 0) { perfAt = Now; perfGcStart = GC.CollectionCount(0); return; }
            double elapsed = Now - perfAt; if (elapsed < 10) return;
            double ms = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[OpenZ Multiplayer Perf] version={0} role={1} transport={2} fps={3:F1} worstFrameMs={4:F1} sendMsPerUpdate={5:F2} receiveMsPerUpdate={6:F2} sentHz={7:F1} receivedHz={8:F1} sendKBps={9:F1} receiveKBps={10:F1} queuedSendBytes={11} queuedReceiveBytes={12} pingMs={13:F0} gcCollections={14} objects={15} build={16}",
                OzForkVersion.Number, IsHost ? "host" : "guest", OnlineMode ? (OnlinePeer.UsesRelay ? "TURN" : "direct") : "TCP",
                perfFrames / elapsed, perfWorstFrame, perfSendTicks * ms / Math.Max(1, perfSent), perfReceiveTicks * ms / Math.Max(1, perfReceived),
                perfSent / elapsed, perfReceived / elapsed, perfSendBytes / 1024.0 / elapsed, perfReceiveBytes / 1024.0 / elapsed,
                QueuedSendBytes, QueuedReceiveBytes, PingMs, GC.CollectionCount(0) - perfGcStart, Game ? Game.World.Objects.Count : 0, OzForkVersion.Label.Replace('\n', '|')));
            perfAt = Now; perfFrames = perfSent = perfReceived = 0; perfWorstFrame = 0;
            perfGcStart = GC.CollectionCount(0);
            perfSendTicks = perfReceiveTicks = perfSendBytes = perfReceiveBytes = 0;
        }
        void SyncClientViews()
        {
            foreach (var o in Game.World.Objects)
                if (o.IsUnit && !Game.Views.ContainsKey(o.Id)) Game.SpawnUnitView(o);
        }
        public void PublishFinalStats(OpenZMatchStats stats)
        {
            if (!IsHost || finalStatsSent || !Game || !Game.World.GameOver) return;
            SendJson(DuelMessage.MatchStats, DuelMatchStats.From(stats)); finalStatsSent = FinalStatsReady = true;
        }
        public void ReadyForLobby()
        {
            if (!Connected || !Playing || !Game || !Game.World.GameOver || localEndReady) return;
            localEndReady = true; Menus.NotifyChat("Ready for lobby. Waiting for your opponent to finish viewing statistics.");
            if (IsHost) TryReturnToLobby(); else peer.Send(new DuelPacket(DuelMessage.EndReady, Array.Empty<byte>()));
        }
        void TryReturnToLobby()
        {
            if (!localEndReady || !remoteEndReady) return;
            lobby.ReturnToLobby();
            peer.Send(new DuelPacket(DuelMessage.ReturnLobby, Array.Empty<byte>()));
            PublishLobby(); ReturnToLobbyScene();
        }
        void ReturnToLobbyScene()
        {
            session = Playing = loadStarted = sentGameOver = restartPending = false; startAt = -1; Game = null;
            capture = new DuelReplica.CaptureBuffer();
            finalStatsSent = FinalStatsReady = localEndReady = remoteEndReady = false;
            commandSequence = 0; receiver = new DuelDeltaReceiver(); pendingEvents.Clear();
            Menus.ShowLobby(); SceneManager.LoadSceneAsync("MainMenu");
        }

        public void RequestRestart()
        {
            if (!Connected || !Playing || !Game || Game.World.GameOver) return;
            if (IsHost) BeginRestartRequest(true);
            else { peer.Send(new DuelPacket(DuelMessage.RestartRequest, Array.Empty<byte>())); Menus.NotifyChat("Restart requested. Waiting for your opponent."); }
        }
        void AskRestart(Action yes, Action no)
        {
            restartPromptOpen = true;
            OzConfirm.Ask("RESTART THIS DUEL?", "YOUR OPPONENT WANTS TO RESTART THE SAME LEVEL.", () => { restartPromptOpen = false; if (Connected && Playing && Game && !Game.World.GameOver) yes(); }, id => OzSfx.Ui(id), 1100, () => { restartPromptOpen = false; if (Connected) no(); });
        }
        void BeginRestartRequest(bool local)
        {
            if (restartPending) { RestartNotice("A restart request is already pending."); return; }
            restartPending = true; restartDeadline = Now + 30;
            if (local) { peer.Send(new DuelPacket(DuelMessage.RestartRequest, Array.Empty<byte>())); Menus.NotifyChat("Restart requested. Waiting for your opponent."); }
            else AskRestart(() => FinishRestartRequest(true), () => FinishRestartRequest(false));
        }
        void RestartNotice(string text)
        {
            Menus.NotifyChat(text); peer.Send(new DuelPacket(DuelMessage.RestartNotice, Encoding.UTF8.GetBytes(text)));
        }
        void FinishRestartRequest(bool accepted)
        {
            if (!restartPending) return;
            restartPending = false;
            if (!accepted || !Playing || !Game || Game.World.GameOver) { RestartNotice("Restart declined or request expired. The match continues."); return; }
            lobby.Restart();
            peer.Send(new DuelPacket(DuelMessage.RestartBegin, Array.Empty<byte>()));
            ResetMatchForRestart(); LoadScene();
        }
        void ResetMatchForRestart()
        {
            finalStatsSent = FinalStatsReady = localEndReady = remoteEndReady = false;
            restartPending = false; Playing = false; loadStarted = true; sentGameOver = false; startAt = -1;
            capture = new DuelReplica.CaptureBuffer();
            perfAt = 0; perfFrames = perfSent = perfReceived = 0; perfWorstFrame = 0;
            perfSendTicks = perfReceiveTicks = perfSendBytes = perfReceiveBytes = 0;
            Game = null; commandSequence = 0; receiver = new DuelDeltaReceiver(); pendingEvents.Clear(); lastSnapshot = 0;
            Menus.CloseOverlay();
        }

        public void Surrender()
        {
            if (!Playing || !Game) return;
            if (IsHost) Game.World.DuelSurrender(0); else peer.Send(new DuelPacket(DuelMessage.Surrender, Array.Empty<byte>()));
        }
        void Fail(string reason) { if (restartPromptOpen) OzConfirm.Dismiss(); Disconnect(); Status = reason; Playing = false; Menus.ShowError(); }
        public void ReportError(string reason) => Fail(reason);
        public void Close()
        {
            if (savedSoloRules)
            { ZWorld.L17CrewsDeviation = soloCrews; ZWorld.FortDoorGunDeviation = soloFortGun; savedSoloRules = false; }
            Disconnect(); OnlineMode = false; guestRelease = null;
            lastProposalAt = -10;
            finalStatsSent = FinalStatsReady = localEndReady = remoteEndReady = false;
            restartPending = false;
            session = Playing = loadStarted = sentGameOver = false; startAt = -1; Game = null; lobby = null; View = null;
            commandSequence = 0; receiver = new DuelDeltaReceiver(); pendingEvents.Clear(); Chat.Clear();
            capture = new DuelReplica.CaptureBuffer();
            perfAt = 0; perfFrames = perfSent = perfReceived = 0; perfWorstFrame = 0;
            perfSendTicks = perfReceiveTicks = perfSendBytes = perfReceiveBytes = 0;
        }
        void Disconnect()
        {
            cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
            listener?.Stop(); listener = null; peer?.Dispose(); peer = null;
            online?.Dispose(); online = null;
            // Observe a connection which completes after cancellation, and dispose it immediately.
            if (connectTask != null) _ = connectTask.ContinueWith(t => { if (t.Status == TaskStatus.RanToCompletion) t.Result.Dispose(); else _ = t.Exception; });
            connectTask = null;
        }
    }

}
namespace OpenZ
{
    public partial class ZGame
    {
        public void DuelAdvance(double dt)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            RuleTicksLastFrame = Advance(World, ref acc, dt, 1);
            RuleMsLastFrame = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        }
        public void DuelBindStartingViews()
        {
            var root = GameObject.Find("ZodObjects");
            if (root)
                foreach (var source in World.Map.objects.Where(o => (o.owner == "red" || o.owner == "blue") && (o.Category == "robot" || o.Category == "vehicle" || o.Category == "cannon")))
                    foreach (Transform child in root.transform)
                    {
                        string prefix = source.Type + "_" + source.x + "_" + source.y;
                        if (child.name == prefix || child.name.StartsWith(prefix + "_", StringComparison.Ordinal)) child.gameObject.SetActive(false);
                    }
            foreach (var pair in Views.ToArray()) if (World.Get(pair.Key) == null) { if (pair.Value) pair.Value.gameObject.SetActive(false); Views.Remove(pair.Key); }
            foreach (var o in World.Objects) if (o.IsUnit && !Views.ContainsKey(o.Id)) SpawnUnitView(o);
        }
    }
}
