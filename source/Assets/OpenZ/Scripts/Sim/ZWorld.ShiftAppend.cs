namespace OpenZ.Sim
{
    /// <summary>
    /// Version 1.2 item 9, rule DV135 (a player's report: ordered to flag 1, then Shift-clicked flag 2, the units went only to flag 2): a
    /// player's Shift queue is added to the order the units already have (View/OzShiftAppend.cs). Zod's SEND_WAYPOINTS replaces a unit's
    /// whole list, so the queue used to throw the first order away.
    /// Only the switch lives here, with the other rule switches: the simulation itself never reads it. The player's input puts the order a
    /// unit already has in front of the queue's points before the command is sent, so the command carries the whole list (every peer and a
    /// replay see the same order) and SEND_WAYPOINTS is unchanged.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV135: a Shift queue goes after the units' current order (on). Off = Zod: the queue replaces the order. View/ZGame.cs sets it
        /// at every start (off in 'Zod exact'); read only by View/OzShiftAppend.cs (the queue, the Shift preview).</summary>
        public static bool ShiftAppendDeviation = true;
    }
}
