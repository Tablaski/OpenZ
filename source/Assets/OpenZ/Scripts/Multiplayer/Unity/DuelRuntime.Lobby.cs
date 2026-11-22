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
            public string Change;
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

        internal void ApplyRoomTeams(int revision, int[] teams)
        {
            if (room == null || room.Launched || RoomHostReady) return;
            room.SetAlliances(RoomHostId, revision, teams);
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

        void ReceiveRoomLobbyStatus(DuelPacket packet)
        {
            if (packet.Payload.Length > 4096) return;
            var status = ReadJson<RoomLobbyStatus>(packet);
            if (status == null) return;
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
            previousLobbyRules = null;
            previousRoomMap = previousRoomSeats = null;
            RoomPings = Array.Empty<LobbyPing>();
            lastRoomLatencyAt = double.NegativeInfinity;
            pendingRoomChanges.Clear();
            publishRoomChangesAt = publishRoomLatencyAt = 0;
        }
    }
}
