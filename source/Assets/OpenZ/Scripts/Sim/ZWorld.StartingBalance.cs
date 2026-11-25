using System;
using System.Collections.Generic;
using System.Linq;
using OpenZ.Multiplayer;

namespace OpenZ.Sim
{
    public sealed partial class ZWorld
    {
        internal const int NeutralStartCostTolerance = 300;
        internal readonly HashSet<ZMapData.Obj> RemovedStartingSources = new HashSet<ZMapData.Obj>();

        void RemoveStartingUnit(ZObject unit)
        {
            if (unit.Source != null) RemovedStartingSources.Add(unit.Source);
            if (unit == PlayableShuttle) PlayableShuttle = null;
            DeleteObject(unit);
        }

        internal void LimitModernBotStartingUnits()
        {
            var modern = Bots.Where(bot => ZAiDifficulty.IsModern(bot.Difficulty)).ToArray();
            if (modern.Length == 0) return;

            var botTeams = new HashSet<Team>(Bots.Select(bot => bot.Team));
            var humans = Objects.Where(o => o.IsFort && !o.Destroyed && o.Owner != Team.Neutral && !botTeams.Contains(o.Owner))
                .Select(o => o.Owner).Distinct().ToArray();
            if (humans.Length == 0) return;

            var counts = Objects.Where(o => o.IsUnit && !o.Destroyed && !o.IsFortDefender).ToLookup(o => o.Owner);
            int limit = humans.Min(team => counts[team].Count());
            foreach (var bot in modern)
            {
                var units = counts[bot.Team];
                // Remove members before their leader and preserve fixed tower defences until last.
                foreach (var unit in units.OrderBy(o => o.IsTowerGun).ThenByDescending(o => o.Id)
                    .Take(Math.Max(0, units.Count() - limit)))
                    RemoveStartingUnit(unit);
            }
            FortDoorGuns.RemoveAll(gun => Get(gun.Id) == null);
        }

        internal void BalanceNeutralStartingUnits(bool preserveMatchedMultiplayerPlacements = false)
        {
            var forts = Objects.Where(o => o.IsFort && !o.Destroyed && o.Owner != Team.Neutral)
                .GroupBy(o => o.Owner).OrderBy(group => group.Key)
                .Select(group => group.FirstOrDefault(o => o.Type == "fort_front") ?? group.First()).ToArray();
            if (forts.Length < 2) return;

            var pickups = Objects.Where(o => !o.IsTowerGun && (o.CanBeEntered() ||
                o.IsShuttle && o.Owner == Team.Neutral && !o.Destroyed)).ToArray();
            if (pickups.Length == 0) return;

            var terrain = new NeutralStartTerrain(this);
            var gates = forts.Select(fort => {
                fort.GetCreationMovePoint(out int x, out int y);
                return (x, y);
            }).ToArray();
            var occupied = Objects.Where(o => !o.Destroyed).ToList();
            var travelCosts = gates.Select(gate => Path.RobotTravelCosts(gate.x, gate.y)).ToArray();
            var slots = forts.Select((fort, seat) => NeutralStartSlots(seat, travelCosts)).ToArray();
            var availableCosts = slots.Select(seat => seat.Keys.OrderBy(cost => cost).ToArray()).ToArray();

            foreach (var kind in pickups.GroupBy(o => (o.Type, o.Health)).OrderByDescending(group => group.First().IsShuttle))
            {
                var assigned = new int[forts.Length];
                var placements = new List<(ZObject unit, int seat, int distance, NeutralStartRole role)>();
                foreach (var unit in kind)
                {
                    int seat = Enumerable.Range(0, forts.Length)
                        .OrderBy(i => TravelCost(travelCosts[i], unit)).ThenBy(i => assigned[i]).First();
                    int cost = TravelCost(travelCosts[seat], unit);
                    if (cost == int.MaxValue)
                    {
                        occupied.Remove(unit);
                        RemoveStartingUnit(unit);
                        continue;
                    }
                    assigned[seat]++;
                    placements.Add((unit, seat, cost,
                        terrain.Role(unit, unit.CenterX, unit.CenterY, forts[seat], gates[seat])));
                }

                var unmatched = forts.Select(fort => new List<(ZObject unit, int distance, NeutralStartRole role)>()).ToArray();
                foreach (var role in placements.GroupBy(p => p.role))
                {
                    var queues = forts.Select((fort, seat) => new Queue<(ZObject unit, int distance, NeutralStartRole role)>(
                        role.Where(p => p.seat == seat).OrderBy(p => p.distance)
                            .Select(p => (p.unit, p.distance, p.role)))).ToArray();
                    while (queues.All(queue => queue.Count > 0))
                    {
                        var row = queues.Select(queue => queue.Dequeue().unit).ToArray();
                        if (!PlaceNeutralStartRow(forts, gates, terrain, new[] { role.Key }, occupied, slots, travelCosts, availableCosts, row, preserveMatchedMultiplayerPlacements))
                            foreach (var unit in row) RemoveStartingUnit(unit);
                    }
                    for (int seat = 0; seat < forts.Length; seat++) unmatched[seat].AddRange(queues[seat]);
                }

                var remaining = unmatched.Select(units => new Queue<(ZObject unit, int distance, NeutralStartRole role)>(
                    units.OrderBy(p => p.distance))).ToArray();
                int completeRows = remaining.Min(queue => queue.Count);
                foreach (var queue in remaining)
                    foreach (var leftover in queue.Skip(completeRows))
                    {
                        occupied.Remove(leftover.unit);
                        RemoveStartingUnit(leftover.unit);
                    }
                for (int i = 0; i < completeRows; i++)
                {
                    var candidates = remaining.Select(queue => queue.Dequeue()).ToArray();
                    var row = candidates.Select(p => p.unit).ToArray();
                    int priority = candidates.Max(p => StrategicPriority(p.role));
                    var preferred = candidates.Where(p => StrategicPriority(p.role) == priority)
                        .GroupBy(p => p.role).OrderByDescending(group => group.Count()).Select(group => group.Key).ToArray();
                    if (!PlaceNeutralStartRow(forts, gates, terrain, preferred, occupied, slots, travelCosts, availableCosts, row, preserveMatchedMultiplayerPlacements))
                        foreach (var unit in row) RemoveStartingUnit(unit);
                }
            }
        }

        static int StrategicPriority(NeutralStartRole role)
        {
            if ((role & NeutralStartRole.Junction) != 0) return 5;
            if ((role & NeutralStartRole.Bridge) != 0) return 4;
            if ((role & NeutralStartRole.Flag) != 0) return 3;
            if ((role & NeutralStartRole.Factory) != 0) return 2;
            return (role & (NeutralStartRole.OnRoad | NeutralStartRole.Roadside)) != 0 ? 1 : 0;
        }

        static int TravelCost(int[,] costs, ZObject unit)
        {
            int x = unit.CenterX >> 4, y = unit.CenterY >> 4;
            return (uint)x < (uint)costs.GetLength(0) && (uint)y < (uint)costs.GetLength(1)
                ? costs[x, y] : int.MaxValue;
        }

        Dictionary<int, List<(int x, int y)>> NeutralStartSlots(int seat, int[][,] travelCosts)
        {
            var slots = new Dictionary<int, List<(int x, int y)>>();
            for (int ty = 1; ty < Path.H - 1; ty++)
                for (int tx = 1; tx < Path.W - 1; tx++)
                {
                    int cost = travelCosts[seat][tx, ty];
                    if (cost == int.MaxValue || travelCosts.Any(other => other != travelCosts[seat] && other[tx, ty] <= cost)) continue;
                    int x = tx * ZConst.TILE + ZConst.TILE / 2, y = ty * ZConst.TILE + ZConst.TILE / 2;
                    if (!NeutralStartGround(x, y)) continue;
                    if (!slots.TryGetValue(cost, out var positions))
                        slots.Add(cost, positions = new List<(int, int)>());
                    positions.Add((x, y));
                }
            return slots;
        }

        bool NeutralStartGround(int x, int y)
        {
            const int half = ZConst.TILE;
            if (x - half < 0 || y - half < 0 || x + half >= Path.WidthPix || y + half >= Path.HeightPix ||
                Path.WithinImpassable(x - half, y - half, half * 2, half * 2, out _, out _, false)) return false;
            for (int ty = (y - half) / half; ty <= (y + half - 1) / half; ty++)
                for (int tx = (x - half) / half; tx <= (x + half - 1) / half; tx++)
                    if (Map.ClassAt(tx, ty) != ZClass.Ground && Map.ClassAt(tx, ty) != ZClass.Road) return false;
            return true;
        }

        bool PlaceNeutralStartRow(ZObject[] forts, (int x, int y)[] gates, NeutralStartTerrain terrain, NeutralStartRole[] roles,
            List<ZObject> occupied, Dictionary<int, List<(int x, int y)>>[] slots, int[][,] travelCosts,
            int[][] availableCosts, ZObject[] row, bool preserveMatchedMultiplayerPlacements)
        {
            var costs = row.Select((unit, seat) => TravelCost(travelCosts[seat], unit)).ToArray();
            if (costs.Max() - costs.Min() <= NeutralStartCostTolerance) return true;

            foreach (var unit in row) occupied.Remove(unit);
            var planned = new (int x, int y, double heading)[row.Length];
            int targetCost = (int)costs.Average();
            var nearbyCosts = availableCosts.SelectMany(costsForSeat => costsForSeat).Distinct()
                .OrderBy(cost => Math.Abs((long)cost + NeutralStartCostTolerance / 2 - targetCost)).ThenBy(cost => cost).ToArray();
            foreach (var role in roles)
            foreach (int cost in nearbyCosts)
            {
                var candidates = new (int x, int y, double heading)[row.Length][];
                int seat = 0;
                for (; seat < row.Length; seat++)
                {
                    candidates[seat] = Positions(seat, cost, role).ToArray();
                    if (candidates[seat].Length == 0) break;
                }
                if (seat != row.Length || !MatchSeat(0, candidates)) continue;

                for (int i = 0; i < row.Length; i++)
                {
                    var unit = row[i];
                    var (x, y, heading) = planned[i];
                    if (unit.IsShuttle) { unit.ShuttleHeadingDeg = (float)heading; unit.ShuttlePlace(x, y); }
                    else unit.SetCords(x - ZConst.TILE, y - ZConst.TILE);
                    unit.ZoneIndex = GetZoneIndex(unit.LocX, unit.LocY);
                    unit.InitRealMoveSpeed(Map);
                    occupied.Add(unit);
                }
                return true;
            }
            if (preserveMatchedMultiplayerPlacements) occupied.AddRange(row);
            return preserveMatchedMultiplayerPlacements;

            IEnumerable<(int x, int y, double heading)> Positions(int seat, int cost, NeutralStartRole role)
            {
                var unit = row[seat];
                int first = Array.BinarySearch(availableCosts[seat], cost);
                if (first < 0) first = ~first;
                var nearby = new List<(int x, int y)>();
                for (int i = first; i < availableCosts[seat].Length && availableCosts[seat][i] <= cost + NeutralStartCostTolerance; i++)
                    nearby.AddRange(slots[seat][availableCosts[seat][i]]);
                foreach (var (x, y) in nearby.OrderBy(p => FortDistance(p.x, p.y, unit)))
                {
                    if (terrain.Role(unit, x, y, forts[seat], gates[seat]) != role ||
                        !terrain.KeepsGunCoverage(unit, x, y) ||
                        !NeutralStartClear(unit, x, y, occupied)) continue;
                    double heading = unit.ShuttleHeadingDeg;
                    if (unit.IsShuttle && !ShuttleLandSpot(x, y, unit, heading, out heading, out _)) continue;
                    yield return (x, y, heading);
                }
            }

            bool MatchSeat(int seat, (int x, int y, double heading)[][] candidates)
            {
                if (seat == row.Length) return true;
                int gap = row[seat].IsShuttle ? 4 * ZConst.TILE : 2 * ZConst.TILE;
                foreach (var position in candidates[seat])
                {
                    if (planned.Take(seat).Any(p => Math.Abs(p.x - position.x) < gap && Math.Abs(p.y - position.y) < gap)) continue;
                    planned[seat] = position;
                    if (MatchSeat(seat + 1, candidates)) return true;
                }
                return false;
            }
        }

        bool NeutralStartClear(ZObject unit, int x, int y, List<ZObject> occupied)
        {
            const int half = ZConst.TILE;
            if (InExitLane(x, y, ZPathing.VehicleRadiusOf(unit.Type)) ||
                NearGateFace(x - half, y - half, x + half, y + half, 0) ||
                occupied.Any(o => x - half < o.LocX + o.WidthPix && x + half > o.LocX &&
                    y - half < o.LocY + o.HeightPix && y + half > o.LocY)) return false;
            if (ParkingKeepOutMap != Map.file) return true;
            foreach (var (px, py, radius) in ParkingKeepOut)
            {
                int dx = Math.Max(0, Math.Abs(x - px) - half);
                int dy = Math.Max(0, Math.Abs(y - py) - half);
                if (dx * dx + dy * dy <= radius * radius) return false;
            }
            return true;
        }

        static int FortDistance(int x, int y, ZObject fort) => StartDistance(x - fort.CenterX, y - fort.CenterY);
        static int StartDistance(int dx, int dy) => dx * dx + dy * dy;

        [Flags]
        enum NeutralStartRole
        {
            OpenGround = 0, Roadside = 1, OnRoad = 2, Junction = 4, Flag = 8,
            Factory = 16, Bridge = 32, Front = 64, Rear = 128
        }

        sealed class NeutralStartTerrain
        {
            readonly ZWorld world;
            readonly (int x, int y)[] junctions;
            readonly ZObject[] flags, factories, bridges;
            const int RoadSampleRadius = 3, RoadSampleWidth = 2 * RoadSampleRadius + 1;
            readonly int[] sampledRoad = new int[RoadSampleWidth * RoadSampleWidth];
            readonly int[] connectedRoad = new int[RoadSampleWidth * RoadSampleWidth];
            int roadSample;

            internal NeutralStartTerrain(ZWorld world)
            {
                this.world = world;
                flags = world.Flags.ToArray();
                factories = world.Objects.Where(o => !o.Destroyed && (o.Type == "robot_factory" ||
                    o.Type == "vehicle_factory" || o.Type == "repair")).ToArray();
                bridges = world.Objects.Where(o => o.IsBridge).ToArray();
                var crossings = new List<(int, int)>();
                for (int y = 0; y < world.Map.height; y++)
                    for (int x = 0; x < world.Map.width; x++)
                        if (Road(x, y) && RoadBranches(x, y) >= 3) crossings.Add((x * 16 + 8, y * 16 + 8));
                junctions = crossings.ToArray();
            }

            bool Road(int x, int y) => x >= 0 && y >= 0 && x < world.Map.width && y < world.Map.height &&
                world.Map.ClassAt(x, y) == ZClass.Road;

            internal bool KeepsGunCoverage(ZObject unit, int x, int y)
            {
                if (unit.Kind != ZKind.Cannon) return true;
                int rangeSquared = unit.AttackRadius * unit.AttackRadius;
                return junctions.All(p => StillCovers(p.x, p.y)) &&
                    flags.Concat(bridges).Concat(factories).All(target => StillCovers(target.CenterX, target.CenterY));

                bool StillCovers(int toX, int toY) =>
                    !Covers(unit.CenterX, unit.CenterY, toX, toY) || Covers(x, y, toX, toY);

                bool Covers(int fromX, int fromY, int toX, int toY) =>
                    StartDistance(toX - fromX, toY - fromY) <= rangeSquared &&
                    (!unit.LosActive || world.Sight.Clear(fromX, fromY, 0, toX, toY, 0));
            }

            // Count distinct arms of the same road on the perimeter, including diagonal approaches.
            int RoadBranches(int x, int y)
            {
                const int r = RoadSampleRadius, width = RoadSampleWidth;
                int head = 0, tail = 0;
                roadSample++;
                Visit(r, r);
                while (head < tail)
                {
                    int cell = connectedRoad[head++];
                    int ix = cell % width, iy = cell / width;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            Visit(ix + dx, iy + dy);
                }
                int arms = 0;
                bool last = sampledRoad[width] == roadSample;
                for (int step = 0; step < width - 1; step++) Count(step, 0);
                for (int step = 0; step < width - 1; step++) Count(width - 1, step);
                for (int step = width - 1; step > 0; step--) Count(step, width - 1);
                for (int step = width - 1; step > 0; step--) Count(0, step);
                return arms;

                void Visit(int tx, int ty)
                {
                    if ((uint)tx >= width || (uint)ty >= width) return;
                    int cell = ty * width + tx;
                    if (sampledRoad[cell] == roadSample || !Road(x + tx - r, y + ty - r)) return;
                    sampledRoad[cell] = roadSample;
                    connectedRoad[tail++] = cell;
                }

                void Count(int tx, int ty)
                {
                    bool road = sampledRoad[ty * width + tx] == roadSample;
                    if (road && !last) arms++;
                    last = road;
                }
            }

            internal NeutralStartRole Role(ZObject unit, int x, int y, ZObject fort, (int x, int y) gate)
            {
                const int tile = ZConst.TILE;
                int tx = x / tile, ty = y / tile;
                var role = Road(tx, ty) ? NeutralStartRole.OnRoad : NeutralStartRole.OpenGround;
                for (int oy = -2; oy <= 2 && role == NeutralStartRole.OpenGround; oy++)
                    for (int ox = -2; ox <= 2; ox++)
                        if (Road(tx + ox, ty + oy)) { role = NeutralStartRole.Roadside; break; }

                int junctionRange = unit.Kind == ZKind.Cannon ? Math.Max(4 * tile, unit.AttackRadius) : 4 * tile;
                if (junctions.Any(p => StartDistance(p.x - x, p.y - y) <= junctionRange * junctionRange &&
                    (!unit.LosActive || world.Sight.Clear(x, y, 0, p.x, p.y, 0)))) role |= NeutralStartRole.Junction;
                if (flags.Any(flag => FortDistance(x, y, flag) <= 80 * 80)) role |= NeutralStartRole.Flag;
                if (factories.Any(factory => NearFootprint(x, y, factory))) role |= NeutralStartRole.Factory;
                if (bridges.Any(bridge => NearFootprint(x, y, bridge))) role |= NeutralStartRole.Bridge;

                int direction = Math.Sign(gate.y - fort.CenterY) * (y - fort.CenterY);
                if (direction > Math.Abs(x - fort.CenterX)) role |= NeutralStartRole.Front;
                if (-direction > Math.Abs(x - fort.CenterX)) role |= NeutralStartRole.Rear;
                return role;
            }

            static bool NearFootprint(int x, int y, ZObject o)
            {
                int dx = Math.Max(0, Math.Max(o.LocX - x, x - o.LocX - o.WidthPix));
                int dy = Math.Max(0, Math.Max(o.LocY - y, y - o.LocY - o.HeightPix));
                return StartDistance(dx, dy) <= 64 * 64;
            }
        }
    }
}
