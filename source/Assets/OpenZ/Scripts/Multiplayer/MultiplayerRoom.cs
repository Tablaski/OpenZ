using System;
using System.Collections.Generic;
using System.Linq;
using OpenZ.Sim;

namespace OpenZ.Multiplayer
{
    [Serializable]
    internal sealed class SkirmishRules
    {
        public int TerritoryCap = 50, FortSpeedup, VehicleTech, RobotTech, PlanningSeconds;
        public bool ProgressiveTerritory, ProgressiveFort, ClassicUnits, NerfedHowitzers, FriendlyFire, ClassicDynamics;
        public bool FreeForAll, AllForOne;
        public RadarLossRule RadarLoss;
        public bool CustomRoster;
        public string[] CustomUnits = Array.Empty<string>();
        public bool ExcludeMammoth, ExcludeBizon;
        public string TankDynamics = "", RobotDynamics = "";
        public bool OverrideFortTurrets;
        public string[] FortTurrets = new string[4];
        public string[] Common = new string[4];
        public int[] BuildPercent = Enumerable.Repeat(100, 8).ToArray();
        public string[] Extras = new string[32];
        public int[] AiIdleSeconds = new int[8], AiFactoryCapture = new int[8];

        internal bool Valid => TerritoryCap >= 10 && TerritoryCap <= 50 && FortSpeedup >= 0 && FortSpeedup <= 500 &&
            VehicleTech >= 0 && VehicleTech <= 6 && RobotTech >= 0 && RobotTech <= 6 &&
            PlanningSeconds >= 0 && PlanningSeconds <= DuelRules.MaxPlanningSeconds &&
            RadarLoss >= RadarLossRule.Off && RadarLoss <= RadarLossRule.FogAndZoom &&
            ValidDynamics(TankDynamics, ZBehaviorFamily.Tanks) && ValidDynamics(RobotDynamics, ZBehaviorFamily.Robots) &&
            (!OverrideFortTurrets || FortTurrets?.Length == 4 && FortTurrets.All(type => string.IsNullOrEmpty(type) ||
                DuelRules.AllowedFortTurrets.Contains(type) && Allows(type))) &&
            (!CustomRoster || !ClassicUnits && CustomUnits != null && CustomUnits.Length <= DuelUnitRoster.All.Length &&
                CustomUnits.All(DuelUnitRoster.Known) && CustomUnits.Distinct().Count() == CustomUnits.Length) &&
            BuildPercent?.Length == 8 && BuildPercent.All(value => value >= 50 && value <= 100) &&
            AiIdleSeconds?.Length == 8 && AiIdleSeconds.All(value => value == 0 || value == 15 || value == 30) &&
            AiFactoryCapture?.Length == 8 && AiFactoryCapture.All(value => value >= 0 && value <= 2) &&
            Common?.Length == 4 && Extras?.Length == 32 && Common.Concat(Extras).All(unit => string.IsNullOrEmpty(unit) ||
                DuelRules.AllowedExtras.Contains(unit) && Allows(unit));

        internal static bool ValidDynamics(string encoded, ZBehaviorFamily family)
        {
            if (string.IsNullOrEmpty(encoded)) return true;
            if (encoded.Length > 64000) return false;
            try { return ZWorld.DuelProfile(encoded, family).family == family; }
            catch (Exception error) when (error is FormatException || error is ArgumentException || error is OverflowException) { return false; }
        }

        internal ZBehaviorProfile Behavior(ZBehaviorFamily family)
        {
            string encoded = family == ZBehaviorFamily.Tanks ? TankDynamics : RobotDynamics;
            return ClassicDynamics ? ZBehaviorProfile.Classic(family) : ZWorld.DuelProfile(encoded, family);
        }

        internal bool Allows(string unit) => ClassicUnits ? DuelUnitRoster.Z1996.Contains(unit) :
            !CustomRoster || (unit == ZBigTanks.Mammoth ? !ExcludeMammoth :
                unit == ZBigTanks.Bizon ? !ExcludeBizon : CustomUnits.Contains(unit));

        internal SkirmishRules Copy()
        {
            var copy = (SkirmishRules)MemberwiseClone();
            copy.Common = (string[])Common.Clone();
            copy.BuildPercent = (int[])BuildPercent.Clone();
            copy.Extras = (string[])Extras.Clone();
            copy.AiIdleSeconds = (int[])AiIdleSeconds.Clone();
            copy.AiFactoryCapture = (int[])AiFactoryCapture.Clone();
            copy.CustomUnits = (string[])CustomUnits?.Clone();
            copy.FortTurrets = (string[])FortTurrets?.Clone() ?? new string[4];
            return copy;
        }

        internal IEnumerable<string> StartingUnits(int seat) => Common.Concat(Extras.Skip(seat * 4).Take(4)).Where(unit => !string.IsNullOrEmpty(unit));

        internal DuelRules Production => new DuelRules { TerritoryReductionCap = TerritoryCap / 100d, FortSpeedup = FortSpeedup / 100d,
            ProgressiveTerritoryCap = ProgressiveTerritory, ProgressiveFortSpeedup = ProgressiveFort };
    }

    internal readonly struct RoomMember
    {
        public readonly string Id, Name;
        public readonly MatchSeatAssignment Seat;
        public readonly int Alliance;
        public readonly bool Ready;

        public RoomMember(string id, string name, MatchSeatAssignment seat, int alliance, bool ready = false)
        {
            Id = id; Name = name; Seat = seat; Alliance = alliance; Ready = ready;
        }

        public RoomMember WithReady(bool ready) => new RoomMember(Id, Name, Seat, Alliance, ready);
    }

    internal readonly struct RoomBot
    {
        public readonly int Position, Alliance;
        public readonly ZBotDifficulty Difficulty;

        public RoomBot(int position, int alliance, ZBotDifficulty difficulty)
        { Position = position; Alliance = alliance; Difficulty = difficulty; }
    }

    /// <summary>Authoritative room setup. Callers supply the authenticated sender's connection identity.</summary>
    internal sealed class MultiplayerRoom
    {
        readonly Dictionary<string, string> rosterIds = new Dictionary<string, string>(StringComparer.Ordinal);
        int nextRosterId;
        internal string RosterId(string id) => id != null && rosterIds.TryGetValue(id, out var token) ? token : null;

        readonly Dictionary<string, RoomMember> members = new Dictionary<string, RoomMember>(StringComparer.Ordinal);
        readonly Dictionary<int, RoomBot> bots = new Dictionary<int, RoomBot>();
        readonly HashSet<string> loaded = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<int> closed = new HashSet<int>();
        readonly bool allowSoloConversions;
        SkirmishRules rules = new SkirmishRules { FreeForAll = true };

        public string HostId { get; }
        public int HostPosition => Member(HostId).Seat.Position;
        public string Map { get; private set; }
        public int Starts { get; private set; }
        public int Capacity { get; private set; }
        public int Revision { get; private set; }
        public bool Launched { get; private set; }
        public RoomMember[] Members => members.Values.ToArray();
        public RoomBot[] Bots => bots.Values.OrderBy(bot => bot.Position).ToArray();
        public int[] ClosedPositions => closed.OrderBy(position => position).ToArray();
        public SkirmishRules Rules => rules.Copy();
        public bool SupportsTeams => CanFormTeams(Starts - closed.Count);
        internal static bool CanFormTeams(int positions) => positions >= 3;

        public MatchStartBlocker StartBlocker
        {
            get
            {
                var seats = MultiplayerMatchSeats.Check(Starts, members.Values.Select(member => member.Seat)
                    .Concat(bots.Keys.Concat(closed).Select(position => new MatchSeatAssignment(MatchSeatRole.Bot, position))));
                return seats == MatchStartBlocker.None && !ActiveAlliances().Distinct().Skip(1).Any()
                    ? MatchStartBlocker.NoOpposingTeam : seats;
            }
        }
        public bool CanLaunch => CanLaunchWithHostReady && Member(HostId).Ready;
        public bool CanLaunchWithHostReady => !Launched && StartBlocker == MatchStartBlocker.None &&
            members.Values.All(member => member.Id == HostId || !member.Seat.MayUsePlayerCommands || member.Ready);
        public bool MatchLoaded => Launched && members.Values.All(member => !member.Seat.MayUsePlayerCommands || loaded.Contains(member.Id));

        public MultiplayerRoom(string hostId, string hostName, string map, bool allowSoloConversions = false)
        {
            if (string.IsNullOrWhiteSpace(hostId)) throw new ArgumentException("Missing host identity.", nameof(hostId));
            HostId = hostId;
            this.allowSoloConversions = allowSoloConversions;
            rules.AllForOne = !allowSoloConversions;
            Starts = MapStarts(map);
            Capacity = Starts;
            Map = map;
            rosterIds.Add(hostId, "P" + ++nextRosterId);
            members.Add(hostId, new RoomMember(hostId, DuelLobby.NormalizeName(hostName), new MatchSeatAssignment(MatchSeatRole.Human, 0), 0));
        }

        public void Join(string id, string name, bool replaceBot = false)
        {
            RequireSetup();
            if (string.IsNullOrWhiteSpace(id) || members.ContainsKey(id)) throw new ArgumentException("Invalid or duplicate participant identity.", nameof(id));
            int position = -1;
            for (int candidate = 0; candidate < Starts; candidate++)
                if (!Occupied(candidate)) { position = candidate; break; }
            if (position < 0 && replaceBot && bots.Count > 0)
            {
                position = bots.Keys.Min();
                bots.Remove(position);
            }
            rosterIds.Add(id, "P" + ++nextRosterId);
            members.Add(id, new RoomMember(id, DuelLobby.NormalizeName(name), new MatchSeatAssignment(MatchSeatRole.Human, position), position));
            InvalidateReady();
        }

        public void AssignHuman(string sender, string id, int position, int alliance)
        {
            RequireHostSetup(sender);
            CheckPosition(position, alliance);
            var member = Member(id);
            if (member.Seat.Position == position && member.Alliance == alliance) return;
            if (Occupied(position, id)) throw new InvalidOperationException("The starting position is occupied.");
            members[id] = new RoomMember(id, member.Name, new MatchSeatAssignment(MatchSeatRole.Human, position), alliance);
            InvalidateReady();
        }

        public void SetRules(string sender, SkirmishRules next)
        {
            RequireHostSetup(sender);
            if (next?.Valid != true) throw new ArgumentException("Invalid Skirmish rules.");
            rules = next.Copy();
            NormalizeTeamMode();
            InvalidateReady();
        }

        void NormalizeTeamMode()
        {
            rules.FreeForAll |= !SupportsTeams;
            if (rules.FreeForAll)
            {
                foreach (var member in members.Values.Where(member => member.Seat.Position >= 0).ToArray())
                    members[member.Id] = new RoomMember(member.Id, member.Name, member.Seat, member.Seat.Position);
                foreach (var bot in bots.Values.ToArray())
                    bots[bot.Position] = new RoomBot(bot.Position, bot.Position, bot.Difficulty);
            }
        }

        public void SetClosed(string sender, int position, bool value)
        {
            RequireHostSetup(sender);
            CheckPosition(position, 0);
            if (members.Values.Any(member => member.Seat.Position == position)) throw new InvalidOperationException("This position has a player.");
            if (value ? closed.Add(position) : closed.Remove(position))
            {
                bots.Remove(position);
                if (!rules.FreeForAll && !SupportsTeams) NormalizeTeamMode();
                InvalidateReady();
            }
        }

        public void BringWaitingPlayer(string sender, string id, int position)
        {
            RequireHostSetup(sender);
            CheckPosition(position, 0);
            if (Member(id).Seat.Position >= 0 || members.Values.Any(member => member.Seat.Position == position)) return;
            closed.Remove(position);
            bots.Remove(position);
            AssignHuman(sender, id, position, position);
        }

        public void AssignObserver(string sender, string id)
        {
            RequireHostSetup(sender);
            var member = Member(id);
            if (member.Seat.Role == MatchSeatRole.Observer) return;
            members[id] = new RoomMember(id, member.Name, new MatchSeatAssignment(MatchSeatRole.Observer, -1), -1);
            InvalidateReady();
        }

        public void ConfigureMap(string sender, string map, int capacity, IEnumerable<string> activePlayers)
        {
            RequireHostSetup(sender);
            int starts = MapStarts(map);
            var requested = activePlayers.ToArray();
            var active = requested.ToHashSet();
            if (capacity < 2 || capacity > starts || active.Count > capacity || active.Count != requested.Length ||
                !active.Contains(HostId) || active.Any(id => !members.ContainsKey(id)))
                throw new ArgumentException("Choose a valid set of active players for this map.");
            var oldMembers = members.Values.ToArray();

            var oldRules = rules.Copy();
            var occupied = new HashSet<int>();
            if (Map != map) rules.VehicleTech = rules.RobotTech = 0;
            Starts = starts;
            Capacity = capacity;
            Map = map;
            closed.RemoveWhere(position => position < capacity || position >= starts);
            for (int position = capacity; position < starts; position++) closed.Add(position);

            foreach (var member in oldMembers.Where(member => active.Contains(member.Id))
                .OrderBy(member => member.Seat.Position < 0 || member.Seat.Position >= capacity))
            {
                int oldPosition = member.Seat.Position;
                int position = oldPosition >= 0 && oldPosition < capacity && occupied.Add(oldPosition)
                    ? oldPosition : Enumerable.Range(0, capacity).First(candidate => occupied.Add(candidate));
                int alliance = rules.FreeForAll || member.Alliance < 0 || member.Alliance >= starts ? position : member.Alliance;
                members[member.Id] = new RoomMember(member.Id, member.Name, new MatchSeatAssignment(MatchSeatRole.Human, position), alliance);
            }
            foreach (var member in oldMembers.Where(member => !active.Contains(member.Id)))
                members[member.Id] = new RoomMember(member.Id, member.Name, new MatchSeatAssignment(MatchSeatRole.Observer, -1), -1);
            foreach (int position in bots.Keys.Where(position => position >= capacity || occupied.Contains(position)).ToArray())
                bots.Remove(position);
            foreach (var bot in bots.Values.Where(bot => bot.Alliance >= starts || rules.FreeForAll).ToArray())
                bots[bot.Position] = new RoomBot(bot.Position, bot.Position, bot.Difficulty);

            foreach (var member in oldMembers.Where(member => member.Seat.Position >= 0))
            {
                int position = member.Seat.Position;
                rules.BuildPercent[position] = 100;
                rules.AiIdleSeconds[position] = rules.AiFactoryCapture[position] = 0;
                Array.Clear(rules.Extras, position * 4, 4);
            }
            foreach (var member in oldMembers.Where(member => active.Contains(member.Id) && member.Seat.Position >= 0))
            {
                int from = member.Seat.Position, to = members[member.Id].Seat.Position;
                rules.BuildPercent[to] = oldRules.BuildPercent[from];
                rules.AiIdleSeconds[to] = oldRules.AiIdleSeconds[from];
                rules.AiFactoryCapture[to] = oldRules.AiFactoryCapture[from];
                Array.Copy(oldRules.Extras, from * 4, rules.Extras, to * 4, 4);
            }
            NormalizeTeamMode();
            InvalidateReady();
        }

        public void SetAlliances(string sender, int revision, int[] alliances, bool? allForOne = null)
        {
            RequireHostSetup(sender);
            if (revision != Revision || rules.FreeForAll || alliances?.Length != Starts ||
                alliances.Any(value => value < 0 || value >= Starts)) return;
            bool changed = (allForOne.HasValue && rules.AllForOne != allForOne.Value) ||
                members.Values.Any(member => member.Seat.MayUsePlayerCommands &&
                member.Alliance != alliances[member.Seat.Position]) || bots.Values.Any(bot => bot.Alliance != alliances[bot.Position]);
            if (!changed) return;
            if (allForOne.HasValue) rules.AllForOne = allForOne.Value;
            foreach (var member in members.Values.Where(member => member.Seat.MayUsePlayerCommands).ToArray())
                members[member.Id] = new RoomMember(member.Id, member.Name, member.Seat, alliances[member.Seat.Position]);
            foreach (var bot in bots.Values.ToArray())
                bots[bot.Position] = new RoomBot(bot.Position, alliances[bot.Position], bot.Difficulty);
            InvalidateReady();
        }

        public void SetBot(string sender, int position, int alliance, ZBotDifficulty difficulty)
        {
            RequireHostSetup(sender);
            CheckPosition(position, alliance);
            if (!ZAiDifficulty.IsValid(difficulty)) throw new ArgumentOutOfRangeException(nameof(difficulty));
            if (members.Values.Any(member => member.Seat.Position == position)) throw new InvalidOperationException("The starting position is occupied.");
            closed.Remove(position);
            if (bots.TryGetValue(position, out var existing) && existing.Alliance == alliance && existing.Difficulty == difficulty) return;
            bots[position] = new RoomBot(position, alliance, difficulty);
            InvalidateReady();
        }

        public void RemoveBot(string sender, int position)
        {
            RequireHostSetup(sender);
            if (bots.Remove(position)) InvalidateReady();
        }

        public void RemoveHuman(string sender, string id)
        {
            RequireHostSetup(sender);
            if (id == HostId) throw new InvalidOperationException("The host cannot be removed from their own room.");
            if (members.Remove(id))
            {
                rosterIds.Remove(id);
                InvalidateReady();
            }
        }

        public void SelectMap(string sender, string map)
        {
            RequireHostSetup(sender);
            if (map == Map) return;
            Starts = MapStarts(map);
            Capacity = Starts;
            Map = map;
            rules.VehicleTech = rules.RobotTech = 0;
            foreach (int position in bots.Keys.Where(position => position >= Starts).ToArray()) bots.Remove(position);
            closed.RemoveWhere(position => position >= Starts);
            foreach (var member in members.Values.Where(member => member.Seat.Position >= Starts).ToArray())
                members[member.Id] = new RoomMember(member.Id, member.Name, new MatchSeatAssignment(MatchSeatRole.Human, -1), -1);
            foreach (var member in members.Values.Where(member => member.Seat.Role == MatchSeatRole.Human && member.Seat.Position < 0).ToArray())
                for (int position = 0; position < Starts; position++)
                {
                    if (members.Values.Any(other => other.Seat.Position == position)) continue;
                    if (closed.Contains(position)) continue;
                    bots.Remove(position);
                    members[member.Id] = new RoomMember(member.Id, member.Name, new MatchSeatAssignment(MatchSeatRole.Human, position), position);
                    break;
                }
            NormalizeTeamMode();
            InvalidateReady();
        }

        public void SwapStartingSides(string sender) => RemapStartingSides(sender,
            position => position >= 0 && (position ^ 1) < Starts ? position ^ 1 : position);

        public void SwapForts(string sender, int source, int destination)
        {
            RequireHostSetup(sender);
            if (source == destination || source < 0 || destination < 0 || source >= Starts || destination >= Starts ||
                closed.Contains(source) || closed.Contains(destination)) return;
            RemapStartingSides(sender, position => position == source ? destination : position == destination ? source : position);
        }

        void RemapStartingSides(string sender, Func<int, int> Swap)
        {
            RequireHostSetup(sender);
            foreach (var member in members.Values.Where(member => member.Seat.Position >= 0).ToArray())
            {
                int seat = Swap(member.Seat.Position);
                members[member.Id] = new RoomMember(member.Id, member.Name, new MatchSeatAssignment(member.Seat.Role, seat),
                    rules.FreeForAll ? seat : member.Alliance);
            }
            var previousBots = bots.Values.ToArray();
            bots.Clear();
            foreach (var bot in previousBots)
            {
                int seat = Swap(bot.Position);
                bots[seat] = new RoomBot(seat, rules.FreeForAll ? seat : bot.Alliance, bot.Difficulty);
            }
            var previousClosed = closed.ToArray();
            closed.Clear();
            foreach (int seat in previousClosed) closed.Add(Swap(seat));
            for (int seat = 0; seat < Starts; seat++)
            {
                int destination = Swap(seat);
                if (destination <= seat) continue;
                (rules.BuildPercent[seat], rules.BuildPercent[destination]) = (rules.BuildPercent[destination], rules.BuildPercent[seat]);
                (rules.AiIdleSeconds[seat], rules.AiIdleSeconds[destination]) = (rules.AiIdleSeconds[destination], rules.AiIdleSeconds[seat]);
                (rules.AiFactoryCapture[seat], rules.AiFactoryCapture[destination]) = (rules.AiFactoryCapture[destination], rules.AiFactoryCapture[seat]);
                for (int unit = 0; unit < 4; unit++)
                {
                    int x = seat * 4 + unit, y = destination * 4 + unit;
                    (rules.Extras[x], rules.Extras[y]) = (rules.Extras[y], rules.Extras[x]);
                }
            }
            InvalidateReady();
        }

        public void SetReady(string sender, int revision, bool ready)
        {
            RequireSetup();
            var member = Member(sender);
            if (revision != Revision || !member.Seat.MayUsePlayerCommands) throw new InvalidOperationException("Stale room revision or inactive participant.");
            members[sender] = member.WithReady(ready);
        }

        public void Rename(string sender, string name)
        {
            RequireSetup();
            var member = Member(sender);
            string normalized = DuelLobby.NormalizeName(name);
            members[sender] = new RoomMember(sender, normalized, member.Seat, member.Alliance, member.Ready);
        }

        public void Launch(string sender)
        {
            RequireHostSetup(sender);
            if (!CanLaunch) throw new InvalidOperationException("All starts must be filled, at least two alliances must compete, and active humans must be ready.");
            Launched = true;
        }

        public void ReturnToLobby(string sender)
        {
            RequireHost(sender);
            if (!Launched) return;
            Launched = false;
            loaded.Clear();
            InvalidateReady();
        }

        public void MarkLoaded(string sender, int revision)
        {
            var seat = Member(sender).Seat;
            if (!Launched || revision != Revision || !seat.MayUsePlayerCommands && seat.Role != MatchSeatRole.Observer)
                throw new InvalidOperationException("Stale load acknowledgment or inactive participant.");
            loaded.Add(sender);
        }

        public Team HumanTeam(string id)
        {
            var seat = Member(id).Seat;
            return Launched && seat.MayUsePlayerCommands ? (Team)(seat.Position + 1) : Team.Neutral;
        }

        public void ConfigureFreeForAllWorld(ZWorld world, int seed)
        {
            if (!MatchLoaded || world == null || OzAiMapCatalog.FindSkirmishMap(Map)?.File != world.Map.file)
                throw new InvalidOperationException("The loaded battlefield must match the launched room.");
            if (ActiveAlliances().Distinct().Count() != Starts)
                throw new InvalidOperationException("Free-for-all requires a distinct alliance per position.");
            ConfigureWorld(world, seed);
        }

        public void ConfigureWorld(ZWorld world, int seed)
        {
            if (!MatchLoaded || world == null || OzAiMapCatalog.FindSkirmishMap(Map)?.File != world.Map.file)
                throw new InvalidOperationException("The loaded battlefield must match the launched room.");

            int[] alliances = Enumerable.Range(0, Starts).ToArray();
            foreach (var member in members.Values.Where(member => member.Seat.MayUsePlayerCommands))
                alliances[member.Seat.Position] = member.Alliance;
            foreach (var bot in bots.Values)
                alliances[bot.Position] = bot.Alliance;

            world.ConfigureSkirmishBots(Bots.Select(bot => ((Team)(bot.Position + 1), bot.Difficulty)), seed);
            world.SetSkirmishAlliances(alliances);
            world.ConfigureSkirmishRules(rules, ClosedPositions);
        }

        IEnumerable<int> ActiveAlliances() => members.Values.Where(member => member.Seat.MayUsePlayerCommands)
            .Select(member => member.Alliance).Concat(bots.Values.Select(bot => bot.Alliance));

        void InvalidateReady()
        {
            Revision++;
            foreach (var member in members.Values.Where(member => member.Ready).ToArray()) members[member.Id] = member.WithReady(false);
        }

        int MapStarts(string map)
        {
            var entry = allowSoloConversions ? OzAiMapCatalog.FindSkirmishMap(map) : OzAiMapCatalog.Find(map);
            if (entry != null && (entry.Starts > 2 || allowSoloConversions)) return entry.Starts;
            if (DuelRules.IsMapAllowed(map)) return 2;
            throw new ArgumentException("Unknown multiplayer map.", nameof(map));
        }

        RoomMember Member(string id) => members.TryGetValue(id, out var member) ? member : throw new ArgumentException("Unknown participant.", nameof(id));
        bool Occupied(int position, string except = null) => closed.Contains(position) || bots.ContainsKey(position) || members.Values.Any(member => member.Id != except && member.Seat.Position == position);

        void CheckPosition(int position, int alliance)
        {
            if (position < 0 || position >= Starts) throw new ArgumentOutOfRangeException(nameof(position));
            if (alliance < 0 || alliance >= 8) throw new ArgumentOutOfRangeException(nameof(alliance));
        }

        void RequireSetup()
        {
            if (Launched) throw new InvalidOperationException("The match setup has already been launched.");
        }

        void RequireHost(string sender)
        {
            if (sender != HostId) throw new InvalidOperationException("Only the room creator can change this setting.");
        }

        void RequireHostSetup(string sender) { RequireHost(sender); RequireSetup(); }
    }
}
