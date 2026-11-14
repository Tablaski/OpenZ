using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 9 (rule DV135, a player's report: "I command them to flag 1 and then, with Shift, to flag 2: they only go to flag 2"): a
    /// Shift queue is ADDED to the order the units already have. A unit's SEND_WAYPOINTS replaces its whole list (Zod, the rules, as for
    /// every unit), so a Shift queue sent after a plain order threw that order away. Now, when the queue is sent (PlayerInput.Flush, first,
    /// through OzShuttleQueueInput.Prefix), every unit in it that still has an order of the player gets that order put in front of its new
    /// points: order flag 1, then Shift-click flag 2, and the list sent is [flag 1, flag 2]. The command carries the whole list, so every
    /// peer and a replay see the same order (the rules never read anything here).
    /// <list type="bullet">
    /// <item>Kept: the unit's waypoints the player gave (playerGiven), exactly as they are now (a group order's own spot, the attack-move
    /// mark), so the unit walks on without a new start.</item>
    /// <item>Not kept: the moves the movement layer adds itself (step aside, back off, regroup, a bridge's waiting bay: refId -2 to -5),
    /// the waypoints a unit gave itself (an attack-move's target, a dodge, a flag or crate it went for by itself), and the first one when
    /// it cannot be overwritten (SEND_WAYPOINTS keeps that one itself: it would be doubled).</item>
    /// <item>Left alone: a plain click (it replaces the order, as before), a squad member (its leader's list is cloned to it), a shuttle
    /// (OzShuttleQueueInput puts the route it still has to fly in front), a patrol (it never ends: the queue replaces it, as before).</item>
    /// </list>
    /// Switch <see cref="ZWorld.ShiftAppendDeviation"/> (on; off = Zod: the queue replaces the order; off in 'Zod exact', View/ZGame.cs).
    /// The Shift preview (View/SelectionOverlay.cs) draws the queue's line from where the list that will be sent goes on (<see cref="PreviewFrom"/>).
    /// </summary>
    public static class OzShiftAppend
    {
        /// <summary>Checks: queues sent with a unit's order put in front, and the waypoints put in front in all.</summary>
        public static int Appended, AppendedPoints;
        static readonly List<ZWaypoint> tmp = new List<ZWaypoint>();

        /// <summary>The rule applies to this unit: the rule on, a unit that is not destroyed, not a squad member, not a shuttle, not on patrol.</summary>
        public static bool Applies(ZObject u) => ZWorld.ShiftAppendDeviation && u != null && !u.Destroyed && !u.IsMinion && !u.IsShuttle && !u.Patrolling;

        /// <summary>A waypoint of the order the player gave: given by the player, and not one of the movement layer's own moves (their refIds
        /// lie below -1: RefStepAside -2, RefBackOff -3, RefRegroup -4 in Sim/ZPathing.cs, RefBay -5 in Sim/ZBridges.cs).</summary>
        public static bool Keep(ZWaypoint w) => w.playerGiven && (w.refId > ZPathing.RefStepAside || w.refId == ZWorld.RefLavaIn);   // DV152 level 7: a walk into lava is the player's order too
                                                                                                                                    // (re-sent it is a MOVE onto lava again: LavaOrder makes it a walk in from where the unit is)

        /// <summary>The order a Shift queue is added to: the unit's kept waypoints in their order, from the first one that SEND_WAYPOINTS
        /// does not keep by itself.</summary>
        public static void CurrentOrder(ZObject u, List<ZWaypoint> into)
        {
            if (!Applies(u) || into == null) return;
            for (int i = u.CanOverwriteWP() || ZWorld.IsLavaWalk(u.Waypoints[0]) && u.Waypoints[0].refId == ZWorld.RefLavaIn ? 0 : 1; i < u.Waypoints.Count; i++)   // DV152: a running walk into lava is kept too (LavaOrder ends the kept one: it goes on from the copy)
                if (Keep(u.Waypoints[i])) into.Add(u.Waypoints[i]);
        }

        /// <summary>PlayerInput.Flush, first (through OzShuttleQueueInput.Prefix): after a Shift queue every unit's current order goes in front
        /// of its new points. shift = the queue was a Shift queue (after a plain click nothing changes). Returns the units whose list changed.</summary>
        public static int Prefix(Dictionary<ZObject, List<ZWaypoint>> pending, bool shift)
        {
            if (!shift || pending == null || !ZWorld.ShiftAppendDeviation) return 0;
            int n = 0;
            foreach (var kv in pending)
            {
                if (kv.Value == null || kv.Value.Count == 0 || !Applies(kv.Key)) continue;
                tmp.Clear(); CurrentOrder(kv.Key, tmp);
                if (tmp.Count == 0) continue;
                kv.Value.InsertRange(0, tmp);
                n++; Appended++; AppendedPoints += tmp.Count;
            }
            tmp.Clear();
            return n;
        }

        /// <summary>The Shift preview (View/SelectionOverlay.cs): the map point the queue's line goes on from = where the list that will be sent
        /// goes on: its last waypoint that stays (the first one SEND_WAYPOINTS keeps by itself, then the order put in front). false = none of
        /// the unit's waypoints stays: the line starts at the unit (also when the queue replaces the order: the rule off, a patrol).</summary>
        public static bool PreviewFrom(ZObject u, out int x, out int y)
        {
            x = y = 0;
            if (u == null || u.Waypoints.Count == 0) return false;
            bool any = false; int i0 = 0;
            if (!u.CanOverwriteWP()) { x = u.Waypoints[0].x; y = u.Waypoints[0].y; any = true; i0 = 1; }
            if (!Applies(u)) return any;
            for (int i = i0; i < u.Waypoints.Count; i++)
            {
                var w = u.Waypoints[i];
                if (Keep(w)) { x = w.x; y = w.y; any = true; }
            }
            return any;
        }
    }
}
