using System;
using System.Collections.Generic;
using System.Linq;
using OpenZ.Multiplayer;

namespace OpenZ.Sim
{
    public sealed partial class ZWorld
    {
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

        internal void BalanceNeutralStartingUnits()
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
            var slots = forts.Select((fort, seat) => NeutralStartSlots(forts, seat, gates[seat])).ToArray();
            var commonRadii = slots[0].Keys.Where(d => slots.All(seat => seat.ContainsKey(d))).ToArray();

            foreach (var kind in pickups.GroupBy(o => (o.Type, o.Health)).OrderByDescending(group => group.First().IsShuttle))
            {
                var assigned = new int[forts.Length];
                var placements = new List<(ZObject unit, int seat, int distance, NeutralStartRole role)>();
                foreach (var unit in kind)
                {
                    int seat = Enumerable.Range(0, forts.Length)
                        .OrderBy(i => FortDistance(unit.CenterX, unit.CenterY, forts[i])).ThenBy(i => assigned[i]).First();
                    assigned[seat]++;
                    placements.Add((unit, seat, FortDistance(unit.CenterX, unit.CenterY, forts[seat]),
                        terrain.Role(unit, unit.CenterX, unit.CenterY, forts[seat], gates[seat])));
                }

                foreach (var role in placements.GroupBy(p => p.role))
                {
                    var queues = forts.Select((fort, seat) => new Queue<ZObject>(role.Where(p => p.seat == seat)
                        .OrderBy(p => p.distance).Select(p => p.unit))).ToArray();
                    while (queues.All(queue => queue.Count > 0))
                    {
                        var row = queues.Select(queue => queue.Dequeue()).ToArray();
                        if (!PlaceNeutralStartRow(forts, gates, terrain, role.Key, occupied, slots, commonRadii, row))
                            foreach (var unit in row) RemoveStartingUnit(unit);
                    }
                    foreach (var unit in queues.SelectMany(queue => queue))
                    {
                        occupied.Remove(unit);
                        RemoveStartingUnit(unit);
                    }
                }
            }
        }

        // Work in pixels because the two fort orientations can have different centre offsets within a tile.
        Dictionary<int, List<(int x, int y)>> NeutralStartSlots(ZObject[] forts, int seat, (int x, int y) gate)
        {
            var fort = forts[seat];
            var slots = new Dictionary<int, List<(int x, int y)>>();
            for (int y = fort.CenterY % ZConst.TILE; y < Path.HeightPix; y += ZConst.TILE)
                for (int x = fort.CenterX % ZConst.TILE; x < Path.WidthPix; x += ZConst.TILE)
                {
                    int distance = FortDistance(x, y, fort);
                    if (forts.Any(other => other != fort && FortDistance(x, y, other) <= distance)) continue;
                    if (!Path.ShouldBeAbleToMoveTo(gate.x, gate.y, x, y, true) || !NeutralStartGround(x, y)) continue;
                    if (!slots.TryGetValue(distance, out var positions))
                        slots.Add(distance, positions = new List<(int, int)>());
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

        bool PlaceNeutralStartRow(ZObject[] forts, (int x, int y)[] gates, NeutralStartTerrain terrain, NeutralStartRole role,
            List<ZObject> occupied, Dictionary<int, List<(int x, int y)>>[] slots, int[] commonRadii, ZObject[] row)
        {
            var distances = row.Select((unit, seat) => FortDistance(unit.CenterX, unit.CenterY, forts[seat])).ToArray();
            if (distances.All(distance => distance == distances[0])) return true;

            foreach (var unit in row) occupied.Remove(unit);
            var planned = new (int x, int y, double heading)[row.Length];
            int radius = (int)distances.Average();
            foreach (int distance in commonRadii.OrderBy(d => Math.Abs((long)d - radius)).ThenBy(d => d))
            {
                int seat = 0;
                for (; seat < row.Length; seat++)
                {
                    var unit = row[seat];
                    var candidates = slots[seat][distance].OrderBy(p => FortDistance(p.x, p.y, unit));
                    bool found = false;
                    foreach (var (x, y) in candidates)
                    {
                        int gap = unit.IsShuttle ? 4 * ZConst.TILE : 2 * ZConst.TILE;
                        if (planned.Take(seat).Any(p => Math.Abs(p.x - x) < gap && Math.Abs(p.y - y) < gap) ||
                            !NeutralStartClear(unit, x, y, occupied) || terrain.Role(unit, x, y, forts[seat], gates[seat]) != role) continue;
                        double heading = unit.ShuttleHeadingDeg;
                        if (unit.IsShuttle && !ShuttleLandSpot(x, y, unit, heading, out heading, out _)) continue;
                        planned[seat] = (x, y, heading);
                        found = true;
                        break;
                    }
                    if (!found) break;
                }
                if (seat != row.Length) continue;

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
            return false;
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

            // Count separate road arms on the perimeter around a road tile, including diagonal approaches.
            int RoadBranches(int x, int y)
            {
                const int r = 3;
                int arms = 0;
                bool last = Road(x - r, y - r + 1);
                for (int step = -r; step < r; step++) Count(x + step, y - r);
                for (int step = -r; step < r; step++) Count(x + r, y + step);
                for (int step = r; step > -r; step--) Count(x + step, y + r);
                for (int step = r; step > -r; step--) Count(x - r, y + step);
                return arms;

                void Count(int tx, int ty)
                {
                    bool road = Road(tx, ty);
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
