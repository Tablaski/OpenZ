using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// Version 1.3 item 29 (units that answer an order but do not move; Sim/ZWorld.OrderFallback.cs has the causes): the unit side.
    /// <list type="bullet">
    /// <item>OrderClearPending, SendWaypoints' clean start of a new order: Zod's ClearPending of the pathing layer, and when an action of
    /// the old order was still waiting (the unit stands for it) the first waypoint restarts like a new Zod waypoint (SetTarget + path
    /// from where it stands), also when the new waypoint is the same as the old one.</item>
    /// <item>The refusal note: why SEND_WAYPOINTS dropped a waypoint (Zod's CheckWaypoint rules, unchanged), with the sim time.</item>
    /// <item>OrderStandWhy: why a unit has not got under way since an order (read only, for View/OzOrderWatch.cs and the counted
    /// check Editor/OzV13GroupOrderCount.cs). A reason the player can see (fighting in range, a gun, stunned, waiting in a queue, at
    /// its spot already, the order refused by Zod's rules) or "silent": it has its order and stands, or its order ended without a step
    /// and without a reason.</item>
    /// </list>
    /// No rule here: nothing reads the notes inside the simulation, and OrderClearPending only acts on a unit the layer had stopped.
    /// </summary>
    public partial class ZObject
    {
        /// <summary>Why SEND_WAYPOINTS last dropped a waypoint of this unit (null = never), and the sim time of that order.</summary>
        public string OrderRefusedWhy { get; private set; }
        public double OrderRefusedAt { get; private set; } = -1;

        internal void NoteOrderRefused(ZWaypoint wp, ZObject t, double now)
        {
            OrderRefusedAt = now;
            switch (wp.mode)
            {
                case WpMode.Move: case WpMode.ForceMove: case WpMode.Dodge:
                    OrderRefusedWhy = Kind == ZKind.Cannon ? "a gun cannot move" : "cannot move"; break;
                case WpMode.Attack: case WpMode.Agro:
                    if (t == null || t.Destroyed) OrderRefusedWhy = "its target is gone";
                    else if (t.Owner == Owner) OrderRefusedWhy = "its own side's " + t.Type;
                    else if (!CanAttack()) OrderRefusedWhy = "it has no weapon";
                    else if (!HasExplosives() && t.AttackedByExplosives) OrderRefusedWhy = "only explosives hurt a " + t.Type;
                    else OrderRefusedWhy = "cannot attack " + t.Type + " now";
                    break;
                case WpMode.Enter: OrderRefusedWhy = t == null ? "nothing to enter" : "cannot enter " + t.Type; break;
                case WpMode.PickupGrenades: OrderRefusedWhy = "cannot pick up grenades"; break;
                default: OrderRefusedWhy = wp.mode + " refused"; break;
            }
        }

        /// <summary>SendWaypoints (in place of PS.ClearPending): drop the layer's actions of the old order; a unit that stood for one of
        /// them restarts its first waypoint (Zod's new-waypoint path) instead of waiting for the stuck guard. Only a unit that STANDS (no
        /// speed: PathHoldForGiveUp, OnBlocked's StopMove, a crowd hold) and whose first waypoint may be overwritten: a walking unit keeps
        /// its path as before (a re-aim only re-steers a moving unit; DV135's Shift queue keeps the current waypoint as it is), and a
        /// </summary>
        internal void OrderClearPending()
        {
            var s = PS;
            bool parked = (s.giveUp || s.arrive || s.reissue || s.hasRetarget || s.hasBackOff) && !IsMoving && CanOverwriteWP();
            s.ClearPending();
            if (!parked || !ZWorld.OrderPendingRestart) return;
            lastWp = ZWaypoint.Cleared;
            ZWorld.OrderPendingRestarts++;
        }

        /// <summary>
        /// step and no reason. As ZWorld.AttackOrderFallback does for a refused attack, the unit walks to the clicked point instead (the click's attack-move flag; a squad's members
        /// follow). False = the old end. Ctrl (force attack), the CPU and every other target keep it.</summary>
        bool OrderPlanFallback(ZWaypoint wp, ZObject t)
        {
            if (!ZWorld.OrderMoveFallbackDeviation || !wp.playerGiven || wp.forceFire || !CanMove || t == null || t.Kind != ZKind.MapItem || !World.IsHumanTeam(Owner)) return false;
            wp.mode = WpMode.Move; wp.refId = -1;
            Waypoints[0] = wp; UpdatedWaypoints = true; lastWp = ZWaypoint.Cleared;
            if (!IsMinion) CloneMinionWayPoints();
            ZWorld.OrderMoveFallbacks++;
            return true;
        }

        /// <summary>Where and when an order found the unit (OrderStandWhy compares against it).</summary>
        public struct OrderSnap { public int x, y, giveUps, arrivals, target; public double at; }

        public OrderSnap OrderSnapshot()
        {
            var m = Waypoints.Count > 0 ? Waypoints[0].mode : WpMode.None;
            int target = m == WpMode.Attack || m == WpMode.Enter || m == WpMode.PickupGrenades ? Waypoints[0].refId : -1;
            return new OrderSnap { x = CenterX, y = CenterY, giveUps = PS.GiveUps, arrivals = PS.Arrivals, target = target, at = World != null ? World.Time : 0 };
        }

        /// <summary>The unit has got under way since the snapshot: 2 px or more from where the order found it.</summary>
        public bool OrderMovedFrom(OrderSnap s) => Math.Abs(CenterX - s.x) + Math.Abs(CenterY - s.y) >= 2;

        /// <summary>OrderStandWhy (3 Nov, DV166 kit): a unit, or the squad leader it follows, held, queued or pushed this recently (s) is
        /// waiting in traffic (the stuck guard's gate marks, ZStuckGuard.cs Monitor: the look came as it was let through). A squad member in
        /// front of its place, within ZPathing.AheadReach of its walking leader, lets him pass (the squad follow's own short wait).</summary>
        public const double OrderTrafficLook = 0.25;

        /// <summary>Why the unit still stands where the order found it. silent = no reason the player could see (the bug of item 29);
        /// working = it does what it was told from where it stands (fires at its target in range, is at its spot already).</summary>
        public string OrderStandWhy(OrderSnap s, out bool silent, out bool working)
        {
            silent = false; working = false;
            if (World == null || Destroyed || KillAt >= 0 || World.Get(Id) != this) return "gone (destroyed or boarded)";
            if (Waypoints.Count == 0 && OrderRefusedWhy != null && OrderRefusedAt >= s.at - 1e-9) return "order refused: " + OrderRefusedWhy;
            if (!CanMove) return Kind == ZKind.Cannon ? "a gun cannot move" : "cannot move";
            if (Stunned) return "stunned";
            if (ExitWalking(World.Time)) return "still getting out of its carrier";
            if (PathInBuildingStage) return "in a repair, crane or fort stage";
            if (PathWaitingForRepair) return "waiting for the repair bay";
            if (AttackObject != null && CanAttackObject(AttackObject) && WithinAttackRadius(AttackObject)) { working = true; return "fighting " + AttackObject.Type + "#" + AttackObject.Id + " in range"; }
            if (Waypoints.Count == 0)
            {
                var t = s.target >= 0 ? World.Get(s.target) : null;
                if (s.target >= 0 && (t == null || t.Destroyed || t.KillAt >= 0 || t.Owner == Owner)) { working = true; return "its target is gone or taken"; }
                if (PS.GiveUps > s.giveUps) return "order given up: " + (PS.LastOrderEnd ?? PS.lastReason ?? "no way there");
                if (PS.Arrivals > s.arrivals || (PS.moveDoneArrived && PS.moveDoneAt >= s.at - 1e-9)) { working = true; return "arrived: its spot was where it stood"; }
                silent = true;
                return "its order ended without a step";
            }
            var wp = Waypoints[0];
            if (wp.mode == WpMode.Move && Math.Abs(CenterX - wp.x) + Math.Abs(CenterY - wp.y) <= 16) { working = true; return "at its spot already"; }
            if (PS.wait != ZPathWait.None) return "waiting in traffic (" + PS.wait + (PS.heldBy != null ? ", for " + PS.heldBy.Type + "#" + PS.heldBy.Id : "") + ")";
            if (World.Time - PS.lastTraffic <= OrderTrafficLook) return "waiting in traffic until a moment ago";
            var lead = IsMinion && Leader != null && !Leader.Destroyed && Leader.Waypoints.Count > 0 && Leader.Waypoints[0].Equals(wp) ? Leader : null;
            if (lead != null && (lead.PS.wait != ZPathWait.None || World.Time - lead.PS.lastTraffic <= OrderTrafficLook))
                return "waits for its squad leader in traffic (" + lead.Type + "#" + lead.Id + ")";   // (the squad goes with its leader: Zod's minions)
            if (lead != null && InFormation && PC.followMode != 3 && PC.followAlong < 0 && ZPathing.Moving(lead)
                && (CenterX - lead.CenterX) * (double)(CenterX - lead.CenterX) + (CenterY - lead.CenterY) * (double)(CenterY - lead.CenterY) <= ZPathing.AheadReach * ZPathing.AheadReach)
                return "lets its leader pass, in front of its place in the squad (" + lead.Type + "#" + lead.Id + ")";   // (the follow's own short wait within DV166's reach; beyond it DV166 walks it on)
            if (IsMoving && Kind == ZKind.Vehicle && cur.pathFindingId == 0 && cur.gotPfResponse) { working = true; return "turning onto its way"; }
            silent = true;
            string path = cur.pathFindingId == QueuedPathId ? "its path request still queued" : cur.pathFindingId != 0 ? "waiting for its path" : cur.gotPfResponse ? "has its path" : "no path asked";
            return "has its " + wp.mode + " order and stands (" + path + (IsMoving ? ", speed set" : ", no speed") + ")";
        }
    }
}
