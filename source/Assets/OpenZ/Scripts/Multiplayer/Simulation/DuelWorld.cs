using System;
using System.Collections.Generic;
using System.Linq;
using OpenZ.Multiplayer;

namespace OpenZ.Sim
{
    public enum DuelAiStatus { None, IdleWarning, Ordered, CoolingDown }

    public partial class ZObject
    {
        public DuelAiStatus AiHelpStatus;
        public Team AiHelpFactoryFor { get; internal set; } = Team.Neutral;
        internal double AiHelpNextOrder;
    }

    public enum DuelOrderKind { Waypoints, Stop, Hold, Patrol, Build, Queue, CancelQueue, Repeat, Rally, Eject, PlaceCannon, StopBuild, RepeatQueue, ShuttleKick, RestartBuild, BuildShortcut }

    [Serializable]
    public sealed class DuelOrder
    {
        // No team field: the server derives the team from the authenticated connection.
        public long Sequence;
        public DuelOrderKind Kind;
        public int ObjectId, X, Y, Index;
        public string Unit;
        public bool Enabled;
        public ZWaypoint[] Points = Array.Empty<ZWaypoint>();
    }

    /// <summary>Host-only command acceptance. A receiver is bound permanently to one player's seat.</summary>
    public sealed class DuelOrderReceiver
    {
        const int OrdersPerSecond = 200;
        readonly ZWorld world;
        readonly Team team;
        readonly List<DuelOrder> openingOrders = new List<DuelOrder>();
        long lastSequence;
        double windowStart;
        int windowCount;
        public DuelOrderReceiver(ZWorld world, int slot)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            if (slot != 0 && slot != 1) throw new ArgumentOutOfRangeException(nameof(slot));
            team = slot == 0 ? Team.Red : Team.Blue;
        }

        public bool QueueOpeningOrder(DuelOrder order)
        {
            if (openingOrders.Count >= OrdersPerSecond || !ValidPayload(order)) return false;
            openingOrders.Add(order);
            return true;
        }

        public void ReleaseOpeningOrders()
        {
            foreach (var order in openingOrders) Execute(order);
            openingOrders.Clear();
            windowCount = 0;
        }

        static bool ValidPayload(DuelOrder order) => order != null &&
            Enum.IsDefined(typeof(DuelOrderKind), order.Kind) &&
            order.Points != null && order.Points.Length <= 64 && (order.Unit == null || order.Unit.Length <= 64);

        public bool Execute(DuelOrder order)
        {
            if (world.DuelIsReplica || world.GameOver || !ValidPayload(order) || order.Sequence <= lastSequence) return false;
            // Server simulation clock; clients cannot reset their own rate limit.
            if (world.Time - windowStart >= 1) { windowStart = world.Time; windowCount = 0; }
            if (++windowCount > OrdersPerSecond) return false;
            lastSequence = order.Sequence;
            var o = world.Get(order.ObjectId);
            if (o == null || o.Owner != team || o.Destroyed || o.KillAt >= 0) return false;
            foreach (var p in order.Points)
                if (!Enum.IsDefined(typeof(WpMode), p.mode) || !OnMap(p.x, p.y)) return false;
            switch (order.Kind)
            {
                case DuelOrderKind.Waypoints: return world.SendWaypoints(team, o, order.Points);
                case DuelOrderKind.Stop: return world.SendStop(team, o);
                case DuelOrderKind.Hold: return world.SendHold(team, o);
                case DuelOrderKind.Patrol: return OnMap(order.X, order.Y) && world.SendPatrol(team, o, order.X, order.Y);
                case DuelOrderKind.Build: return world.StartBuilding(team, o, order.Unit);
                case DuelOrderKind.RestartBuild: return world.RestartBuilding(team, o, order.Unit);
                case DuelOrderKind.BuildShortcut: return world.BuildShortcut(team, o, order.Unit);
                case DuelOrderKind.StopBuild: return world.StopBuilding(team, o);
                case DuelOrderKind.Queue: return world.AddBuildingQueue(team, o, order.Unit);
                case DuelOrderKind.CancelQueue: return world.CancelBuildingQueue(team, o, order.Index, order.Unit);
                case DuelOrderKind.Repeat: return world.SetBuildingRepeat(team, o, order.Enabled);
                case DuelOrderKind.RepeatQueue: return world.SetBuildingRepeatQueue(team, o, order.Enabled);
                case DuelOrderKind.ShuttleKick: return world.ShuttleKickOrder(team, o);
                case DuelOrderKind.Rally: return world.SetRallyPoints(team, o, order.Points);
                case DuelOrderKind.Eject: return world.Eject(team, o);
                case DuelOrderKind.PlaceCannon:
                    return OnMap(order.X * 16L, order.Y * 16L) && world.PlaceCannon(team, o, order.X, order.Y, order.Index) != null;
                default: return false;
            }

        }
        bool OnMap(long x, long y) => x >= 0 && y >= 0 && x < world.Map.WidthPix && y < world.Map.HeightPix;
    }

    public sealed partial class ZWorld
    {
        DuelRules duelRules;
        ZBot duelAiHelper;
        readonly List<ZObject> duelAiReady = new List<ZObject>();
        readonly List<ZObject> duelAiOffered = new List<ZObject>();
        double nextDuelAiCheck;
        HashSet<string> soloAllowedUnits;
        bool soloRosterConfigured;
        bool soloExcludeMammoth, soloExcludeBizon;
        readonly double[] duelTerritoryReduction = new double[2];
        readonly double[] duelFortReduction = new double[2];
        public bool DuelIsReplica { get; private set; }
        public Func<DuelOrder, bool> DuelForward;
        internal bool DuelOrdersPaused { get; set; }
        public bool DuelDefersOrders => DuelIsReplica || DuelForward != null || DuelOrdersPaused;
        internal bool DuelSend(Team team, ZObject unit, DuelOrder order)
        {
            if (DuelOrdersPaused || unit == null || unit.Owner != team || unit.Destroyed || GameOver || DuelForward == null) return false;
            order.ObjectId = unit.Id; return DuelForward(order);
        }
        internal bool HasDuelRules => duelRules != null;
        public DuelRules DuelConfiguration => duelRules?.Copy();
        internal bool DuelNerfedHowitzers => duelRules?.NerfedHowitzers == true;

        public RadarLossRule DuelRadarLoss => duelRules?.RadarLoss ?? RadarLossRule.Off;
        public bool DuelAiIdleHelp => duelRules?.HandicapEnabled == true && duelRules.AiIdleSeconds > 0;
        public bool DuelAllowsUnit(string type)
        {
            if (duelRules != null) return duelRules.Allows(type);
            if (type == ZBigTanks.Mammoth) return !soloExcludeMammoth;
            if (type == ZBigTanks.Bizon) return !soloExcludeBizon;
            return soloAllowedUnits == null || soloAllowedUnits.Contains(type);
        }
        /// <summary>Solo roster only: preserve campaign production, difficulty, bots and combat profiles.</summary>
        public void ConfigureSoloUnits(IEnumerable<string> types, bool excludeMammoth = false, bool excludeBizon = false)
        {
            if (Time != 0 || duelRules != null || soloRosterConfigured) throw new InvalidOperationException("Configure a fresh solo world only.");
            var selection = new HashSet<string>(StringComparer.Ordinal);
            foreach (string type in types ?? throw new ArgumentNullException(nameof(types)))
                if (!DuelUnitRoster.Known(type) || !selection.Add(type)) throw new ArgumentException("Unknown or duplicate solo unit.");
            soloRosterConfigured = true;
            soloExcludeMammoth = excludeMammoth;
            soloExcludeBizon = excludeBizon;
            if (!selection.SetEquals(DuelUnitRoster.All)) soloAllowedUnits = selection;
            if (soloAllowedUnits == null && !soloExcludeMammoth && !soloExcludeBizon) return;
            foreach (var unit in Objects.ToArray())
                if (unit.IsUnit && !DuelAllowsUnit(unit.IsHero ? "heroes" : unit.Type)) DeleteObject(unit);
            FortDoorGuns.RemoveAll(unit => !DuelAllowsUnit(unit.Type));
            if (soloAllowedUnits != null) FilterProductionRoster();
        }
        public static ZBehaviorProfile DuelProfile(string encoded, ZBehaviorFamily family)
        {
            var profile = string.IsNullOrEmpty(encoded) ? new ZBehaviorProfile { family = family, name = "OpenZ Native", id = "native" } : ZBehaviorProfile.Decode(encoded);
            if (profile.family != family) throw new ArgumentException("Incorrect dynamics family.");
            return profile;
        }
        public System.Collections.Generic.IReadOnlyList<string> ProductionUnits(ZObject building)
        {
            var list = ZBuildLists.For(building.Type, building.BuildLevel);
            if (duelRules == null && soloAllowedUnits == null) return list;
            var filtered = new List<string>(); foreach (string type in list) if (DuelAllowsUnit(type)) filtered.Add(type);
            return filtered;
        }

        /// <summary>Call once after map creation, before the first simulation tick.</summary>
        public void ConfigureDuel(DuelRules rules, bool replica)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            rules.Validate();
            if (Time != 0 || duelRules != null || soloRosterConfigured) throw new InvalidOperationException("Configure a fresh world only.");
            var tanks = DuelProfile(rules.TankDynamics, ZBehaviorFamily.Tanks);
            var robots = DuelProfile(rules.RobotDynamics, ZBehaviorFamily.Robots);
            SetBehavior(tanks); SetBehavior(robots);
            duelRules = rules.Copy(); DuelIsReplica = replica;
            Bots.Clear(); Array.Clear(TechBonus, 0, TechBonus.Length);
            if (!replica && rules.HandicapEnabled && (rules.AiIdleSeconds > 0 || rules.AiManageProduction))
                duelAiHelper = new ZBot(this, rules.HandicapSlot == 0 ? Team.Red : Team.Blue, 439);
            ApplyDuelFactoryTech();
            DuelUpdateFortBonus();
            FilterProductionRoster();
        }

        public int MapFactoryTech(string factoryType) =>
            Objects.Where(o => o.Type == factoryType).Select(o => o.Level + 1).DefaultIfEmpty(0).Max();

        void ApplyDuelFactoryTech()
        {
            int vehicleDefault = MapFactoryTech("vehicle_factory"), robotDefault = MapFactoryTech("robot_factory");
            bool vehicles = duelRules.VehicleTech > 0 && vehicleDefault > 0, robots = duelRules.RobotTech > 0 && robotDefault > 0;
            foreach (var building in Objects)
            {
                if (vehicles && building.IsFort) building.Level = duelRules.VehicleTech - 1;
                else if (vehicles && building.Type == "vehicle_factory")
                    building.Level = Math.Max(0, building.Level + duelRules.VehicleTech - vehicleDefault);
                else if (robots && building.Type == "robot_factory")
                    building.Level = Math.Max(0, building.Level + duelRules.RobotTech - robotDefault);
            }
        }

        internal void UpdateDuelAiHelp()
        {
            int idleSeconds = duelRules?.AiIdleSeconds ?? 0;
            if (duelAiHelper == null || idleSeconds == 0 || GameOver || Time < nextDuelAiCheck) return;
            nextDuelAiCheck = Time + .2;
            foreach (var unit in Objects)
            {
                bool eligible = unit.Owner == duelAiHelper.Team && unit.IsMobile && unit.CanMove && !unit.Destroyed &&
                    unit.KillAt < 0 && !unit.IsMinion && !unit.IsShuttle && !unit.IsFortDefender && !unit.HoldPosition && !unit.Patrolling;
                if (!eligible)
                {
                    unit.AiHelpStatus = DuelAiStatus.None;
                    unit.AiHelpNextOrder = 0;
                    continue;
                }
                bool busy = unit.Waypoints.Count > 0 || unit.IsMoving || unit.AttackObject != null;
                if (busy)
                {
                    if (unit.AiHelpStatus == DuelAiStatus.CoolingDown && Time < unit.AiHelpNextOrder) continue;
                    if (unit.AiHelpStatus != DuelAiStatus.Ordered) unit.AiHelpStatus = DuelAiStatus.None;
                    unit.AiHelpNextOrder = 0;
                    continue;
                }
                if (unit.AiHelpStatus == DuelAiStatus.Ordered)
                {
                    unit.AiHelpStatus = DuelAiStatus.CoolingDown;
                    unit.AiHelpNextOrder = Time + (idleSeconds == 15 ? 10 : 20);
                }
                if (unit.AiHelpNextOrder == 0) unit.AiHelpNextOrder = Time + idleSeconds;
                if (Time >= unit.AiHelpNextOrder)
                {
                    duelAiOffered.Add(unit);
                    duelAiReady.Add(unit);
                }
                else if (unit.AiHelpStatus != DuelAiStatus.CoolingDown && Time >= unit.AiHelpNextOrder - 5)
                    unit.AiHelpStatus = DuelAiStatus.IdleWarning;
            }
            if (duelAiReady.Count == 0) return;
            duelAiHelper.AssistIdleUnits(duelAiReady);
            foreach (var unit in duelAiOffered)
            {
                unit.AiHelpStatus = unit.Waypoints.Count > 0 ? DuelAiStatus.Ordered : DuelAiStatus.None;
                unit.AiHelpNextOrder = unit.Waypoints.Count > 0 ? 0 : Time + idleSeconds;
            }
            duelAiReady.Clear();
            duelAiOffered.Clear();
        }

        static bool WeakProduction(string type) => type == "grunt" || type == "jeep" || type == "gatling";

        internal bool ApplyDuelAiOnCapture(ZObject building, Team previousOwner, Team capturingTeam)
        {
            if (previousOwner == capturingTeam) return false;
            building.AiHelpFactoryFor = Team.Neutral;
            if (duelRules?.HandicapEnabled != true || previousOwner != (duelRules.HandicapSlot == 0 ? Team.Red : Team.Blue) ||
                capturingTeam == Team.Neutral || building.Destroyed || !building.IsProducer || building.IsFort) return false;
            if (duelRules.AiCancelProduction)
            {
                StopBuilding(capturingTeam, building);
                return true;
            }
            if (!duelRules.AiManageProduction || !WeakProduction(building.CurUnit)) return false;
            string pick = duelAiHelper.PickProduction(building);
            if (pick == null || WeakProduction(pick))
                pick = ProductionUnits(building).FirstOrDefault(type => !WeakProduction(type));
            if (pick == null || !RestartBuilding(capturingTeam, building, pick)) return false;
            building.AiHelpFactoryFor = previousOwner;
            return true;
        }
        void FilterProductionRoster()
        {
            foreach (var b in Objects) b.StoredCannons.RemoveAll(t => !DuelAllowsUnit(t));
            // The same neutral technology policy applies to both human seats.
            foreach (var b in Objects) if (b.IsProducer)
            {
                b.Queue.RemoveAll(t => !DuelAllowsUnit(t) || (duelRules != null && !ZBuildLists.InList(b.Type, b.BuildLevel, t)));
                if (b.CurUnit != null && (!DuelAllowsUnit(b.CurUnit) || (duelRules != null && !ZBuildLists.InList(b.Type, b.BuildLevel, b.CurUnit)))) { b.StopBuildingProduction(false); b.SetBuildingDefaultProduction(); }
                b.RecalcBuildTime();
            }
        }

        (double territory, double fort) DuelProductionReductions(Team team, double own, double other)
        {
            double lead = ZForceComparison.RelativePercent(own, other);
            double territory = duelRules.TerritoryTimeReduction(TeamFlagFraction(team), lead);
            if (duelRules.FortSpeedup <= 0)
                return (territory, duelRules.FortLoserReduction(own, other));
            if (lead >= 0) return (territory, 0);
            var opponent = team == Team.Red ? Team.Blue : Team.Red;
            double opponentReduction = duelRules.TerritoryTimeReduction(TeamFlagFraction(opponent), -lead);
            double speed = duelRules.FortCatchupSpeed(-lead, opponentReduction);
            double fortTime = Math.Min(1 - territory, 1 / speed);
            return (territory, 1 - fortTime / (1 - territory));
        }

        public double DuelFortLoserReduction(Team team)
        {
            if (duelRules?.UsesDynamicProduction != true || (team != Team.Red && team != Team.Blue)) return 0;
            if (!DuelIsReplica) return duelFortReduction[team == Team.Red ? 0 : 1];
            return DuelProductionReductions(team, MilitaryValue(team), MilitaryValue(team == Team.Red ? Team.Blue : Team.Red)).fort;
        }

        internal void DuelUpdateFortBonus()
        {
            if (duelRules?.UsesDynamicProduction != true || DuelIsReplica) return;
            double red = MilitaryValue(Team.Red), blue = MilitaryValue(Team.Blue);
            for (int slot = 0; slot < 2; slot++)
            {
                Team team = slot == 0 ? Team.Red : Team.Blue;
                var (territory, fort) = DuelProductionReductions(team, slot == 0 ? red : blue, slot == 0 ? blue : red);
                bool territoryChanged = Math.Abs(territory - duelTerritoryReduction[slot]) > .0000001;
                bool fortChanged = Math.Abs(fort - duelFortReduction[slot]) > .0000001;
                if (!territoryChanged && !fortChanged) continue;
                duelTerritoryReduction[slot] = territory;
                duelFortReduction[slot] = fort;
                foreach (var building in Objects)
                    if (building.IsProducer && building.Owner == team && !building.Destroyed && building.Building &&
                        building.CurUnit != null && (territoryChanged || building.IsFort))
                        building.DuelUpdateProductionTime(building.ProductionProgress);
            }
        }

        internal double DuelBuildSeconds(ZObject building, int seconds)
        {
            if (duelRules == null) throw new InvalidOperationException("Not a duel world.");
            int slot = building.Owner == Team.Blue ? 1 : 0;
            bool hasTeam = building.Owner == Team.Red || building.Owner == Team.Blue;
            double territory = hasTeam && duelRules.UsesDynamicProduction && !DuelIsReplica
                ? duelTerritoryReduction[slot]
                : duelRules.TerritoryTimeReduction(Math.Max(0, Math.Min(1, building.ZoneOwnage)), 0);
            double fort = building.IsFort && hasTeam ? duelFortReduction[slot] : 0;
            double health = building.MaxHealth > 0 ? Math.Max(0, Math.Min(1, (double)building.Health / building.MaxHealth)) : 1;
            double armyDuration = (duelRules.HandicapEnabled && duelRules.HandicapSlot == slot ? duelRules.Handicap : duelRules.Common).BuildDuration;
            return Math.Max(duelRules.MinimumBuildSeconds, seconds * (1 - territory) * (1 - fort) * (1 + 1.25 * (1 - health)) * armyDuration);
        }
    }

    public partial class ZObject
    {
        // Called only by the small production hook installed into the original game.
        internal bool DuelResetZoneBuildTime(double fraction)
        {
            if (World == null || !World.HasDuelRules) return false;
            if (ZoneOwnage == fraction) return true;
            double progress = ProductionProgress;
            ZoneOwnage = fraction;
            if (IsProducer && CurUnit != null && Building) DuelUpdateProductionTime(progress);
            return true;
        }

        internal void DuelUpdateProductionTime(double progress)
        {
            double duration = World.DuelBuildSeconds(this, World.S.Unit(CurUnit)?.build_time ?? 0);
            BInit = World.Time - progress * duration;
            BFinal = BInit + duration;
        }
    }
}
