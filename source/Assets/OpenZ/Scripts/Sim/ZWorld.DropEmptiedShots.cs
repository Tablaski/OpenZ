namespace OpenZ.Sim
{
    /// <summary>
    /// unit of ANY team keeps shooting an emptied vehicle). Sim/ZWorld.DropEmptied.cs drops every unit's target and attack / agro waypoint on
    /// the emptied vehicle; two kinds of shot were already "loaded" past those and still went off at the empty hull:
    /// <list type="bullet">
    /// <item>
    /// shot it never drops its target and fires at the last aimed spot even when the target is no longer valid): it fired once more at the
    /// emptied (neutral) hull, up to 5.5 s after the EXIT;</item>
    /// <item>a heavy tank's loaded second barrel (DV30, Sim/ZObject.VehiclesNb.cs HeavyPairStep: it fires "whatever the unit does meanwhile");
    /// the neutral hull still passes its "t.Owner != Owner" test: one more shell at it up to HeavyPairGap 0.36 s after the EXIT.</item>
    /// </list>
    /// Now, in the tick the vehicle is emptied (DropAttacksOnEmptied, every emptying path: EXIT DRIVER, its crew shot out, a hero pilot's
    /// shuttle EXIT, a gun's EJECT), a fort gun whose target is it drops it and its commitment (it scans again at once: the driver who got out,
    /// if in reach, gets the usual 1.4 s lay) and a heavy whose loaded second shell is meant for it does not fire that shell. Shells already in
    /// the air may still land. A shot loaded AFTER the emptying (an explicit Ctrl attack on the empty hull, DV81) is untouched. Integers, the
    /// sim's own times and the world's object order only; no float, no Dictionary / HashSet order, no UnityEngine.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>Checks only (never read by a rule): fort guns that dropped an emptied vehicle (committed ones of them), heavy tanks' second
        /// shells at an emptied vehicle not fired (all worlds since the program started).</summary>
        public static int EmptiedFortDropped, EmptiedFortCommitDropped, EmptiedSecondDropped;
    }

    public partial class ZObject
    {
        /// <summary>DV176 (u2_followups): vehicle v was emptied in this tick (ZWorld.DropAttacksOnEmptied, the rule on): this unit's fort gun or
        /// loaded heavy second shell drops it.</summary>
        internal void DropShotsOnEmptied(ZObject v, double time)
        {
            if (v == null) return;
            if (FortGunTarget == v)
            {
                if (FortGunCommitted) ZWorld.EmptiedFortCommitDropped++;
                FortGunTarget = null; FortGunCommitted = false; fortGunScan = time;                       // as the gun does itself for a target gone before it committed (ZObject.FortGun.cs)
                ZWorld.EmptiedFortDropped++;
            }
            if (hpSecondAt >= 0 && hpTargetId == v.Id) { hpSecondAt = -1; ZWorld.EmptiedSecondDropped++; }
        }
    }
}
