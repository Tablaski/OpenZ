using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>A rolling minute and a short burst budget for real actions, shared by all planners of one bot.</summary>
    internal sealed class ZAiOrders
    {
        readonly ZWorld world;
        readonly Team team;
        readonly ZAiTuning tuning;
        readonly Queue<double> actions = new Queue<double>();
        readonly ZWaypoint[] command = new ZWaypoint[1];
        double credit, lastRefresh;

        internal int Actions { get; private set; }
        internal double FirstActionAt { get; private set; } = -1;
        internal ZAiOrders(ZWorld world, Team team, ZAiTuning tuning)
        {
            this.world = world; this.team = team; this.tuning = tuning;
            credit = tuning.Burst;
        }

        internal bool Available
        {
            get
            {
                double now = world.Time;
                while (actions.Count > 0 && now - actions.Peek() >= 60) actions.Dequeue();
                credit = Math.Min(tuning.Burst, credit + (now - lastRefresh) * tuning.Apm / 60d);
                lastRefresh = now;
                return actions.Count < tuning.Apm && credit + 1e-6 >= 1;
            }
        }

        void Record()
        {
            if (Actions == 0) FirstActionAt = world.Time;
            Actions++;
            actions.Enqueue(world.Time);
            credit -= 1;
        }

        internal bool CanSpendTwice => Available && actions.Count + 1 < tuning.Apm && credit + 1e-6 >= 2;

        internal bool Stop(ZObject unit, ZObject previousTarget)
        {
            if (!Available || !world.SendStop(team, unit)) return false;
            if (unit.AttackObject == previousTarget) unit.Disengage();
            Record();
            return true;
        }

        internal ZObject Eject(ZObject cannon)
        {
            int firstEvent = world.Events.Count;
            if (!Available || !world.Eject(team, cannon)) return null;
            Record();
            for (int i = firstEvent; i < world.Events.Count; i++)
            {
                var item = world.Events[i];
                if (item.type == ZEventType.Ejected && item.objId == cannon.Id)
                    return world.Get(item.otherId);
            }
            return null;
        }

        internal bool Send(ZObject unit, ZWaypoint wp, HashSet<int> planned)
        {
            if (!Available || !Apply(unit, wp, planned)) return false;
            Record();
            return true;
        }

        internal bool Send(List<ZObject> units, ZWaypoint wp, HashSet<int> planned)
        {
            if (!Available) return false;
            bool ok = false;
            foreach (var unit in units) if (Apply(unit, wp, planned)) ok = true;
            if (ok) Record();
            return ok;
        }

        bool Apply(ZObject unit, ZWaypoint wp, HashSet<int> planned)
        {
            if (unit.Owner != team || !unit.CanOverwriteWP() || planned.Contains(unit.Id)) return false;
            if (unit.Waypoints.Count > 0)
            {
                var next = unit.Waypoints[0];
                if (next.mode == wp.mode && next.refId == wp.refId &&
                    (wp.refId >= 0 || Math.Abs(next.x - wp.x) <= 16 && Math.Abs(next.y - wp.y) <= 16)) return false;
            }
            var validated = wp;
            if (!world.ValidateAiWaypoint(unit, ref validated)) return false;
            command[0] = wp;
            if (!world.SendWaypoints(team, unit, command) || unit.Waypoints.Count == 0) return false;
            planned.Add(unit.Id);
            return true;
        }

        internal bool Production(ZObject factory, string type)
        {
            if (!Available || factory.CurUnit == type) return false;
            bool ok = type == null ? world.StopBuilding(team, factory)
                : factory.CurUnit == null ? world.StartBuilding(team, factory, type)
                : world.RestartBuilding(team, factory, type);
            if (ok) Record();
            return ok;
        }

        internal bool CancelLast(ZObject factory)
        {
            int index = factory.Queue.Count - 1;
            if (index < 0 || !Available || !world.CancelBuildingQueue(team, factory, index, factory.Queue[index])) return false;
            Record();
            return true;
        }
    }

    public sealed partial class ZWorld
    {
        internal bool ValidateAiWaypoint(ZObject unit, ref ZWaypoint waypoint) => CheckWaypointCore(unit, ref waypoint);
    }
}
