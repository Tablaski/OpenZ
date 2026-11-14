using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 10: what the wrench key over the command bar (UI/OpenZHud.RepairKey.cs) asks before it shows, and its click.
    /// RepairCandidate is the filter of the first lines of SendToRepair (the R order): an own vehicle of the selection, alive, not a
    /// minion, damaged, not already on its way to a repair station or on the pad, and a shuttle not on its repair trip. One step
    /// stricter about "on its way": the repair order anywhere in the vehicle's list counts (SendToRepair looks at the first only), because
    /// the sim puts short orders in front of it during the trip (an attack on whoever shoots at it, a dodge, a step aside, a bridge bay)
    /// and the key must not pop up for a vehicle that is still on its trip. So whenever the key shows, SendToRepair has a vehicle to send.
    /// OwnsRepairStation is SendToRepair's station list (a building that can repair the player's units). Read only: no order, no sound,
    /// nothing allocated.
    /// </summary>
    public partial class PlayerInput
    {
        /// <summary>One unit the R order would send now and that is not on its repair trip.</summary>
        public static bool RepairCandidate(ZObject o, Team player)
            => o != null && o.Owner == player && o.Kind == ZKind.Vehicle && !o.Destroyed && !o.IsMinion && o.CanBeRepaired()
               && !HasRepairOrder(o) && !(o.IsShuttle && o.ShuttleRepairTarget >= 0);

        /// <summary>The unit has a repair order (UNIT_REPAIR) anywhere in its list: first, or behind orders the sim put in front of it.</summary>
        public static bool HasRepairOrder(ZObject o)
        {
            var w = o.Waypoints;
            for (int i = 0; i < w.Count; i++) if (w[i].mode == WpMode.UnitRepair) return true;
            return false;
        }

        /// <summary>How many selected units SendToRepair would send now (0: the R order has nothing to send; also after the match end).</summary>
        public int RepairCandidates()
        {
            if (game == null || game.World == null || game.World.GameOver) return 0;
            int n = 0;
            for (int i = 0; i < Selection.Count; i++) if (RepairCandidate(Selection[i], game.player)) n++;
            return n;
        }

        /// <summary>The player owns a repair station (SendToRepair would find one).</summary>
        public bool OwnsRepairStation()
        {
            if (game == null || game.World == null) return false;
            var objs = game.World.Objects;
            for (int i = 0; i < objs.Count; i++) { var b = objs[i]; if (b.Kind == ZKind.Building && b.CanRepairUnit(game.player)) return true; }
            return false;
        }

        /// <summary>The wrench key's click: the R order (SendToRepair, its toast, its order sound and voice). The pointer rests on the HUD
        /// key, not on the map, so the unit that happens to lie under it on screen (Hover, which the order sound reads to pick the attack
        /// cue) is dropped for this frame: the click always gets the repair cue (the R key's cue with the pointer off an enemy). The next
        /// frame works Hover out again.</summary>
        public bool SendToRepairFromHud()
        {
            Hover = null;
            return SendToRepair();
        }
    }
}
