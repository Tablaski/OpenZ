namespace OpenZ.Sim
{
    /// <summary>
    /// Version 1.3 item 29 (a player: "They did answer by their voice lines but they simply did not move sometimes usually it happens
    /// when I have multiple units selected"). Two causes found in the order path, both fixed here:
    /// <list type="number">
    /// <item>Rule DV158 (number left open), ATTACK ORDERS A UNIT CANNOT CARRY OUT. The right-click order table (View/PlayerInput.cs
    /// BuildWaypoint, zplayer.cpp:2746-2881) gives EVERY selected unit an ATTACK on the object under the pointer when it is not the
    /// player's: an enemy unit or building, or a neutral map object (rock, hut, cactus / boulder, wreck, the DV67 palms, signs and
    /// traffic lights, a tree that can be felled). The server then drops the waypoint for each unit that cannot attack that object
    /// (zcore.cpp:234-243: CanAttackObject; ZWorld.CheckWaypoint) after it has cleared the unit's list (zcore.cpp:548), so the unit
    /// stops where it is. A robot without grenades can attack no building and no map object (AttackedByExplosives), a jeep neither,
    /// an APC without passengers or a crane nothing at all. The robots that kept another order (the tanks did) still answered with
    /// the order line. In OpenZ the pointer meets such objects far more often than on Zod's small sprites: their pick boxes are
    /// the drawn 3D models (View/PlayerInput.cs ScreenRect, at least 18 px). With the switch on, a HUMAN player's unit that can move
    /// and cannot attack the clicked object gets a plain MOVE to the clicked point instead (the same attack-move flag the click gave;
    /// the group spread of ZSlots.cs gives it its own spot there). Units that can attack it keep Zod's ATTACK; the CPU's orders,
    /// guns (they cannot move) and every other order kind are unchanged. Also unchanged: a Ctrl click (force attack: the player asked
    /// for the attack itself, so Zod's refusal stands) and a click on an empty vehicle or gun (DV81,
    /// ZWorld.EmptyTargets.cs, has its own table for those).</item>
    /// <item>A bug of the pathing layer, no rule: SendWaypoints cleared the layer's deferred actions of the old order
    /// (ZPathState.ClearPending: give-up, crowd arrival, re-path, retarget, back-off, re-aim). A unit parked for such an action
    /// (PathHoldForGiveUp: no path answer; OnBlocked: stopped for its re-path) whose NEW first waypoint is the same as the old one
    /// (Zod compares the waypoint field by field, ZObject.ProcessServer) never restarted it: it stood with its order until the stuck
    /// guard re-pathed it about 1 s later (ZStuckGuard.cs Escalate). Such a unit now restarts the waypoint like a new Zod waypoint
    /// (ZObject.OrderClearPending). Units without a deferred action, walking units and kept stage waypoints are untouched.</item>
    /// </list>
    /// The refusals left (Zod's rules: a gun ordered to move, an order with no target left) are noted on the unit
    /// (ZObject.OrderRefusedWhy) so the order watch (View/OzOrderWatch.cs) and the counted check (Editor/OzV13GroupOrderCount.cs)
    /// can name the reason. Sim only, fixed tick, no clock, no random number.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV158 switch: a human player's unit that cannot attack the clicked object walks to the clicked point (on in the game,
        /// View/ZGame.cs; off in 'Zod exact' and in every headless check that does not set it itself: Zod's refusal, the unit stops).</summary>
        public static bool OrderMoveFallbackDeviation = false;
        /// <summary>A/B switch of the counted check only (the game never sets it): off = the old ClearPending without the restart.</summary>
        public static bool OrderPendingRestart = true;
        /// <summary>Statistics (checks, all worlds): attack orders turned into moves (DV158); units restarted by OrderClearPending.</summary>
        public static int OrderMoveFallbacks, OrderPendingRestarts;

        /// <summary>SEND_WAYPOINTS' check of one waypoint (CheckWaypointCore, Zod's CheckWaypoint), plus the note of a refusal.</summary>
        bool CheckWaypoint(ZObject o, ref ZWaypoint wp)
        {
            var asked = wp;
            bool ok = CheckWaypointCore(o, ref wp);
            if (!ok) o.NoteOrderRefused(asked, Get(asked.refId), Time);
            return ok;
        }

        /// <summary>DV158, CheckWaypointCore's ATTACK row after Zod's own test failed: true = the waypoint is now a MOVE to the clicked
        /// point (it is kept); false = Zod's refusal.</summary>
        bool AttackOrderFallback(ZObject o, ref ZWaypoint wp)
        {
            if (!OrderMoveFallbackDeviation || !wp.playerGiven || o == null || !o.CanMove || !IsHumanTeam(o.Owner)) return false;
            if (wp.forceFire) return false;                                                              // Ctrl (force attack, the player asked for the attack itself): Zod's refusal
            var t = Get(wp.refId);
            if (t != null && (t.Owner == o.Owner || IsEmptyUnit(t))) return false;                       // (an own unit: never sent by the order table, a move there; an empty vehicle or gun: DV81's own rows)
            wp.mode = WpMode.Move; wp.refId = -1; wp.forceFire = false;                                   // the clicked point, the click's attack-move flag
            OrderMoveFallbacks++;
            return true;
        }
    }
}
