using System;
using System.Collections.Generic;
using System.Linq;
using OpenZ.Sim;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelRuntime
    {
        internal const string RoomSettingsChatPrefix = "[Settings] ";
        [Serializable] internal sealed class LobbyPing
        {
            public int Seat, Ping, Variation;
            public bool Disconnected;
        }

        [Serializable] sealed class RoomLobbyStatus
        {
            public LobbyPing[] Links;
            public LobbyPresence[] Presence;
            public string Change;
            public RoomSetupView Setup;
        }

        [Serializable] internal sealed class LobbyPresence
        {
            public string Id, Version;
        }

        readonly Dictionary<string, LobbyPresence> lobbyPresence = new();
        double nextLobbyPresence;

        internal string RoomPresenceId(string member) => IsHost ? room?.RosterId(member) : member;

        string LocalPresenceId => IsHost ? room?.RosterId(RoomHostId) : JoinedRoom?.Humans.FirstOrDefault(member => member.Local)?.Id;

        internal LobbyPresence RoomPresence(string id)
        {
            if (id == null) return null;
            if (id == LocalPresenceId)
                return new LobbyPresence { Id = id, Version = OzForkVersion.Number };
            return lobbyPresence.TryGetValue(id, out var entry) ? entry : null;
        }

        void PublishLobbyPresence()
        {
            if (session || Now < nextLobbyPresence || LocalPresenceId == null) return;
            nextLobbyPresence = Now + .25;
            var presence = IsHost ? room.Members.Select(member => RoomPresence(room.RosterId(member.Id))).Where(item => item != null).ToArray() :
                new[] { RoomPresence(LocalPresenceId) };
            SendJson(DuelMessage.RoomLobbyStatus, new RoomLobbyStatus { Presence = presence });
        }

        void StorePresence(string id, LobbyPresence item)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 16 || item == null) return;
            string version = item.Version?.Length <= 20 && Version.TryParse(item.Version, out var parsed) ? parsed.ToString() : null;
            bool changed = !lobbyPresence.TryGetValue(id, out var old) || old.Version != version;
            lobbyPresence[id] = new LobbyPresence { Id = id, Version = version };
            if (changed) Menus.Refresh();
        }

        internal enum SetupPanel { Rules, Maps, Units, AllowedUnits, AiHelp, Presets, CombatProfiles, Teams, ActivePlayers, Position }

        [Serializable] internal sealed class RoomSetupView
        {
            internal const float ScrollScale = 10000f;
            public int Tab, Seat, Slot, Scroll, MapPage, MapSize, AiIdle, AiCapture, Click, TeamCount, ActivePlayers, BotGeneration, PositionSeat;
            public int ChoiceCount, ChoiceStart;
            public SetupPanel Panel;
            public bool Fort, Official, Pointer, AllForOne;
            public int[] Teams;
            public string[] Choices, Formations;
            public string World, Search, Map, Unit;
            public int X, Y;

            internal bool SameNavigation(RoomSetupView other) => other != null &&
                Tab == other.Tab && Seat == other.Seat && Panel == other.Panel && Slot == other.Slot &&
                Scroll == other.Scroll && Fort == other.Fort && Official == other.Official &&
                MapPage == other.MapPage && MapSize == other.MapSize && World == other.World &&
                Search == other.Search && Map == other.Map && Unit == other.Unit &&
                AiIdle == other.AiIdle && AiCapture == other.AiCapture &&
                TeamCount == other.TeamCount && AllForOne == other.AllForOne && ActivePlayers == other.ActivePlayers &&
                BotGeneration == other.BotGeneration && PositionSeat == other.PositionSeat &&
                ChoiceCount == other.ChoiceCount && ChoiceStart == other.ChoiceStart &&
                SameItems(Teams, other.Teams) && SameItems(Choices, other.Choices) && SameItems(Formations, other.Formations);

            static bool SameItems<T>(T[] first, T[] second) =>
                first == null ? second == null : second != null && first.SequenceEqual(second);

            internal bool IsValid() => Tab >= 0 && Tab < 5 && Seat >= 0 && Seat < 8 &&
                Panel >= SetupPanel.Rules && Panel <= SetupPanel.Position &&
                Slot >= 0 && Slot < 4 && Scroll >= 0 && Scroll <= ScrollScale &&
                MapPage >= 0 && MapPage < 128 && (MapSize == 0 || MapSize == 2 || MapSize == 3 || MapSize == 4 || MapSize == 6 || MapSize == 8) &&
                AiIdle >= 0 && AiIdle <= 30 && AiCapture >= 0 && AiCapture <= 2 && Click >= 0 &&
                BotGeneration >= 0 && BotGeneration <= 2 && PositionSeat >= 0 && PositionSeat < 8 &&
                X >= 0 && X <= 1920 && Y >= 0 && Y < 766 &&
                (Panel != SetupPanel.Teams || TeamCount >= 2 && TeamCount <= 8 &&
                    Teams != null && Teams.Length >= 2 && Teams.Length <= 8 && Teams.All(team => team >= 0 && team < 8)) &&
                ChoiceCount >= 0 && ChoiceCount <= 4096 && ChoiceStart >= 0 && ChoiceStart <= ChoiceCount &&
                (Choices == null || Choices.Length <= 12 && ChoiceStart + Choices.Length <= ChoiceCount &&
                    Choices.All(name => ValidLabel(name, 50))) &&
                (Formations == null || Choices != null && Formations.Length == Choices.Length &&
                    Formations.All(formation => ValidLabel(formation, 90))) &&
                ValidText(World, 16) && ValidText(Search, 70) && ValidText(Map, 32) && ValidText(Unit, 32);

            static bool ValidText(string value, int limit) =>
                value == null || value.Length <= limit && !value.Any(char.IsControl);
            static bool ValidLabel(string value, int limit) => value != null && ValidText(value, limit) &&
                !value.Contains('<') && !value.Contains('>');
        }

        RoomSetupView lastRoomSetupSent, receivedRoomSetup;
        double nextRoomSetupSend, roomSetupHeartbeat, lastRoomSetupReceived;
        internal RoomSetupView HostSetup => !session && JoinedRoom?.Launched == false &&
            Now - lastRoomSetupReceived < 2.5 ? receivedRoomSetup : null;

        internal void PublishRoomSetup(RoomSetupView view)
        {
            if (room == null || room.Launched || session || !Connected || view == null || Now < nextRoomSetupSend) return;
            bool changed = !view.SameNavigation(lastRoomSetupSent) || view.Pointer != lastRoomSetupSent.Pointer ||
                view.Pointer && (view.X != lastRoomSetupSent.X || view.Y != lastRoomSetupSent.Y || view.Click != lastRoomSetupSent.Click);
            if (!changed && Now < roomSetupHeartbeat) return;
            var packet = new DuelPacket(DuelMessage.RoomLobbyStatus,
                System.Text.Encoding.UTF8.GetBytes(UnityEngine.JsonUtility.ToJson(new RoomLobbyStatus { Setup = view })));
            nextRoomSetupSend = Now + .05;
            if (!RoomTransport.Send(packet)) return;
            lastRoomSetupSent = view;
            roomSetupHeartbeat = Now + .75;
        }

        void ResetRoomSetup()
        {
            lastRoomSetupSent = receivedRoomSetup = null;
            nextRoomSetupSend = roomSetupHeartbeat = lastRoomSetupReceived = 0;
        }

        internal LobbyPing[] RoomPings { get; private set; } = Array.Empty<LobbyPing>();
        double lastRoomLatencyAt = double.NegativeInfinity;
        internal bool HasFreshRoomLatency => Now - lastRoomLatencyAt < 5;
        internal int RoomHostPosition => IsHost ? (room?.HostPosition ?? -1) :
            (JoinedRoom?.Humans.FirstOrDefault(member => member.Host)?.Position ?? -1);
        internal bool RoomObserver => JoinedRoom?.Humans.Any(member => member.Local && member.Observer) == true;
        internal bool RoomHostReady => room?.Members.Any(member => member.Id == room.HostId && member.Ready) ??
            JoinedRoom?.Humans.Any(member => member.Host && member.Ready) == true;
        SkirmishRules previousLobbyRules;
        string previousRoomMap, previousRoomSeats;
        readonly Dictionary<string, string> pendingRoomChanges = new Dictionary<string, string>();
        double publishRoomChangesAt, publishRoomLatencyAt;

        internal void ApplyRoomMap(string map, int capacity, string[] active)
        {
            if (room == null || room.Launched || RoomHostReady || !IsRoomMap(OzAiMapCatalog.FindSkirmishMap(map))) return;
            if (active.Any(id => !room.Members.Any(member => member.Id == id))) return;
            room.ConfigureMap(RoomHostId, map, capacity, active);
            PublishRoom();
        }

        internal void ApplyRoomTeams(int revision, int[] teams, bool allForOne)
        {
            if (room == null || room.Launched || RoomHostReady) return;
            room.SetAlliances(RoomHostId, revision, teams, allForOne);
            PublishRoom();
        }

        internal void SetRoomObserver(string id)
        {
            if (room == null || room.Launched || RoomHostReady || id == RoomHostId ||
                !room.Members.Any(member => member.Id == id)) return;
            room.AssignObserver(RoomHostId, id);
            PublishRoom();
        }

        internal void PublishRoomLatency()
        {
            if (Now >= publishRoomChangesAt && pendingRoomChanges.Count > 0)
            {
                string change = string.Join(" | ", pendingRoomChanges.Values);
                pendingRoomChanges.Clear();
                AddChat(RoomSettingsChatPrefix + change);
                SendJson(DuelMessage.RoomLobbyStatus, new RoomLobbyStatus { Change = change });
            }
            if (Now < publishRoomLatencyAt) return;
            publishRoomLatencyAt = Now + 1.5;
            var remotes = RoomTransport.Remotes;
            RoomPings = room.Members.Where(member => member.Id != RoomHostId && member.Seat.MayUsePlayerCommands)
                .Select(member => (Member: member, Link: remotes.FirstOrDefault(link => member.Id == RoomGuest(link.Id))))
                .Where(entry => session || (entry.Link != null && entry.Link.Ready && entry.Link.Ping > 0))
                .Select(entry => new LobbyPing
                {
                    Seat = entry.Member.Seat.Position,
                    Ping = (int)Math.Min(5000, Math.Round(entry.Link?.Ping ?? 0)),
                    Variation = (int)Math.Min(5000, Math.Round(entry.Link?.Jitter ?? 0)),
                    Disconnected = session && (entry.Link == null || !entry.Link.Ready ||
                        Now - entry.Link.LastPacketAt > 5)
                }).ToArray();
            lastRoomLatencyAt = Now;
            SendJson(DuelMessage.RoomLobbyStatus, new RoomLobbyStatus { Links = RoomPings });
        }

        void ReceiveRoomLobbyStatus(DuelPacket packet, int sender)
        {
            if (packet.Payload.Length > 4096) return;
            var status = ReadJson<RoomLobbyStatus>(packet);
            if (status == null) return;
            if (!session && status.Presence?.Length <= 8)
            {
                if (IsHost && status.Presence.Length == 1 && roomLinks.ContainsKey(sender))
                    StorePresence(room.RosterId(RoomGuest(sender)), status.Presence[0]);
                else if (!IsHost && sender == 1)
                    foreach (var item in status.Presence) StorePresence(item?.Id, item);
            }
            if (IsHost) return;
            if (!session && JoinedRoom?.Launched == false && status.Setup?.IsValid() == true)
            {
                receivedRoomSetup = status.Setup;
                lastRoomSetupReceived = Now;
            }
            if (!session && status.Change?.Length > 0 && status.Change.Length <= 512 && !status.Change.Any(char.IsControl))
                AddChat(RoomSettingsChatPrefix + status.Change);
            if (status.Links != null && status.Links.Length <= 8 && status.Links.All(link => link != null &&
                link.Seat >= 0 && link.Seat < 8 && link.Ping >= 0 && link.Ping <= 5000 && link.Variation >= 0 && link.Variation <= 5000))
            {
                RoomPings = status.Links;
                lastRoomLatencyAt = Now;
            }
        }

        void PublishRoomChanges()
        {
            if (room.Launched) return;
            var rules = room.Rules;
            string seats = string.Join(",", room.Members.Select(member => $"{member.Id}:{member.Seat.Position}:{member.Alliance}:{member.Seat.Role}")) +
                string.Join(",", room.Bots.Select(bot => $"{bot.Position}:{bot.Alliance}:{bot.Difficulty}")) + string.Join(",", room.ClosedPositions);
            if (previousLobbyRules != null)
            {
                var old = previousLobbyRules;
                if (previousRoomMap != room.Map)
                    Add("Map", "Map → " + OzAiMapCatalog.FindSkirmishMap(room.Map).Name);
                else if (seats != previousRoomSeats) Add("Positions", "Players, teams or positions updated");
                if (rules.FreeForAll != old.FreeForAll) Add("Mode", rules.FreeForAll ? "Mode → Free for All" : "Mode → Teams");
                if (rules.AllForOne != old.AllForOne) Add("All for one", rules.AllForOne ? "All for one → On" : "All for one → Off");
                if (rules.PlanningSeconds != old.PlanningSeconds) Add("Planning", $"Opening orders → {rules.PlanningSeconds} s");
                if (rules.VehicleTech != old.VehicleTech || rules.RobotTech != old.RobotTech) Add("Tech", "Factory tech updated");
                if (rules.TerritoryCap != old.TerritoryCap || rules.ProgressiveTerritory != old.ProgressiveTerritory ||
                    rules.FortSpeedup != old.FortSpeedup || rules.ProgressiveFort != old.ProgressiveFort) Add("Balance", "Balancing options updated");
                if (!rules.BuildPercent.SequenceEqual(old.BuildPercent) || !rules.Extras.SequenceEqual(old.Extras) ||
                    !rules.AiIdleSeconds.SequenceEqual(old.AiIdleSeconds) || !rules.AiFactoryCapture.SequenceEqual(old.AiFactoryCapture))
                    Add("Advantage", "Player advantages updated");
                if (rules.OverrideFortTurrets != old.OverrideFortTurrets || !rules.FortTurrets.SequenceEqual(old.FortTurrets) ||
                    !rules.Common.SequenceEqual(old.Common)) Add("Armies", "Starting armies updated");
                if (rules.RadarLoss != old.RadarLoss || rules.FriendlyFire != old.FriendlyFire ||
                    rules.NerfedHowitzers != old.NerfedHowitzers || rules.ClassicUnits != old.ClassicUnits ||
                    rules.CustomRoster != old.CustomRoster || !rules.CustomUnits.SequenceEqual(old.CustomUnits) ||
                    rules.ExcludeMammoth != old.ExcludeMammoth || rules.ExcludeBizon != old.ExcludeBizon ||
                    rules.ClassicDynamics != old.ClassicDynamics || rules.TankDynamics != old.TankDynamics ||
                    rules.RobotDynamics != old.RobotDynamics) Add("Battle", "Battle rules updated");
            }
            previousRoomMap = room.Map;
            previousRoomSeats = seats;
            previousLobbyRules = rules;

            void Add(string key, string description)
            {
                pendingRoomChanges[key] = description;
                publishRoomChangesAt = Now + .65;
            }
        }

        void ClearRoomLobbyStatus()
        {
            ResetRoomSetup();
            lobbyPresence.Clear();
            nextLobbyPresence = 0;
            previousLobbyRules = null;
            previousRoomMap = previousRoomSeats = null;
            RoomPings = Array.Empty<LobbyPing>();
            lastRoomLatencyAt = double.NegativeInfinity;
            pendingRoomChanges.Clear();
            publishRoomChangesAt = publishRoomLatencyAt = 0;
        }
    }
}
