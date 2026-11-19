using System;
using System.Collections.Generic;
using System.Linq;
using OpenZ.Multiplayer;

namespace OpenZ.Sim
{
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
        HashSet<string> soloAllowedUnits;
        bool soloRosterConfigured;
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
        public bool DuelTankDamageEffects => duelRules?.TankDamageEffects == true;
        public bool DuelTankDamageShake => duelRules?.TankDamageShake == true;
        public RadarLossRule DuelRadarLoss => duelRules?.RadarLoss ?? RadarLossRule.Off;
        public bool DuelAllowsUnit(string type) => duelRules != null ? duelRules.Allows(type) : soloAllowedUnits == null || soloAllowedUnits.Contains(type);
        /// <summary>Solo roster only: preserve campaign production, difficulty, bots and combat profiles.</summary>
        public void ConfigureSoloUnits(IEnumerable<string> types)
        {
            if(Time != 0 || duelRules != null || soloRosterConfigured) throw new InvalidOperationException("Configure a fresh solo world only.");
            var selection=new HashSet<string>(StringComparer.Ordinal);
            foreach(string type in types ?? throw new ArgumentNullException(nameof(types)))
                if(!DuelUnitRoster.Known(type) || !selection.Add(type))throw new ArgumentException("Unknown or duplicate solo unit.");
            soloRosterConfigured=true;
            if(selection.SetEquals(DuelUnitRoster.All))return;
            soloAllowedUnits=selection;
            foreach(var unit in Objects.ToArray())if(unit.IsUnit&&!DuelAllowsUnit(unit.IsHero?"heroes":unit.Type))DeleteObject(unit);
            FortDoorGuns.RemoveAll(unit=>!DuelAllowsUnit(unit.Type));
            FilterProductionRoster();
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
            FilterProductionRoster();
        }
        void FilterProductionRoster()
        {
            foreach (var b in Objects) b.StoredCannons.RemoveAll(t => !DuelAllowsUnit(t));
            // The same neutral technology policy applies to both human seats.
            foreach (var b in Objects) if (b.IsProducer)
            {
                b.Queue.RemoveAll(t => !DuelAllowsUnit(t));
                if (b.CurUnit != null && !DuelAllowsUnit(b.CurUnit)) { b.StopBuildingProduction(false); b.SetBuildingDefaultProduction(); }
                b.RecalcBuildTime();
            }
        }

        public double DuelFortLoserReduction(Team team)
        {
            if (duelRules == null || (team != Team.Red && team != Team.Blue)) return 0;
            return duelRules.FortLoserReduction(MilitaryValue(team), MilitaryValue(team == Team.Red ? Team.Blue : Team.Red));
        }
        internal void DuelUpdateFortBonus()
        {
            if (duelRules == null || DuelIsReplica || duelRules.FortLoserMaximum <= 0) return;
            for (int slot = 0; slot < 2; slot++)
            {
                Team team = slot == 0 ? Team.Red : Team.Blue;
                double reduction = DuelFortLoserReduction(team);
                if (Math.Abs(reduction - duelFortReduction[slot]) < .0000001) continue;
                duelFortReduction[slot] = reduction;
                foreach (var fort in Objects)
                    if (fort.IsFort && fort.Owner == team && !fort.Destroyed && fort.Building && fort.CurUnit != null)
                    {
                        double progress = fort.ProductionProgress;
                        double duration = DuelBuildSeconds(fort, S.Unit(fort.CurUnit)?.build_time ?? 0);
                        fort.BInit = Time - progress * duration; fort.BFinal = fort.BInit + duration;
                    }
            }
        }

        internal double DuelBuildSeconds(ZObject building, int seconds)
        {
            if (duelRules == null) throw new InvalidOperationException("Not a duel world.");
            double duration = duelRules.BuildSeconds(building.Owner == Team.Blue ? 1 : 0, seconds,
                Math.Max(0, Math.Min(1, building.ZoneOwnage)),
                building.MaxHealth > 0 ? Math.Max(0, Math.Min(1, (double)building.Health / building.MaxHealth)) : 1);
            return building.IsFort ? Math.Max(duelRules.MinimumBuildSeconds, duration * (1 - DuelFortLoserReduction(building.Owner))) : duration;
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
            if (IsProducer && CurUnit != null && Building)
            {
                double duration = World.DuelBuildSeconds(this, World.S.Unit(CurUnit)?.build_time ?? 0);
                BInit = World.Time - progress * duration;
                BFinal = BInit + duration;
            }
            return true;
        }
    }
}
