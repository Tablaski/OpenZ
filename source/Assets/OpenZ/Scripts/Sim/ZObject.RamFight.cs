using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ 1.5 deviation DV196 "the Mammoth fights on while it rams" (kit mammoth_ram_fight_n26c, on top of DV182 the ram, DV188 the ram polish
    /// Z has no ram; every
    /// number is OpenZ's.
    /// <list type="bullet">
    /// <item>WHEN: a crewed Mammoth from the start of its ram, through the lift / push and a chained ram (DV195), until every vehicle it doomed is
    /// destroyed (its flip and landing included).</item>
    /// <item>WHAT: every <see cref="RamLookTicks"/> ticks (0.05 s) its gun's target is looked at: a live enemy in its gun range that DV188 lets it
    /// fire at is kept; else (none, dead, out of range, the rammed vehicle) the NEXT enemy in its gun range is engaged at once with Zod's own
    /// passive pick (an exposed driver first, else the first in range in object order; DV188 leaves every vehicle it rams or doomed out). It
    /// fires at it at its normal rate (its own reload; nothing is added or skipped). Engage only: no waypoint, no chase, no flag, no random draw
    /// of its own. None in range: it has no target and holds its fire (never at the rammed vehicle: DV188); the view holds its turret where it
    /// is (View/UnitAim.RamClear.cs: no swing aside, the barrels lifted only as much as the rammed hull needs).</item>
    /// <item>Before (DV188 alone): the pick ran at the ram's start only and then in Zod's 0.5 s passive look; a target that died or left the
    /// range during the ram left the gun idle for up to 0.5 s, and the view's ram clear swung the idle turret 90 deg aside with the barrels on
    /// their top stop.</item>
    /// <item>AFTER the ram (its victims destroyed): the normal targeting (Zod's passive look) as before.</item>
    /// </list>
    /// Deterministic: whole-number tick counts, Zod's own range and pick tests, object order, no random draw of its own. Switch
    /// <see cref="ZWorld.RamFightDeviation"/>: View/ZGame.cs on at every start (off in 'Zod exact'); headless default off; the parity report's
    /// Zod rows and the older counted checks hold it off. Off: the hook returns at its first test (no field of any unit changes).
    /// </summary>
    public partial class ZObject
    {
        /// <summary>DV196: the next tick this Mammoth looks at its gun's target during a ram.</summary>
        long ramFightNext;

        /// <summary>DV196: this Mammoth is in a ram (its ram running, or a vehicle it doomed not yet destroyed).</summary>
        public bool RamFightOn => ZWorld.RamFightDeviation && Type == ZBigTanks.Mammoth && Kind == ZKind.Vehicle && World != null && !Destroyed && (ram != null || RamFightVictimLive());

        bool RamFightVictimLive()
        {
            var all = World.Objects;
            for (int i = 0; i < all.Count; i++) { var o = all[i]; if (o != null && o.RamDoomBy == Id && !o.Destroyed) return true; }
            return false;
        }

        /// <summary>RamFireDrop (after DV188's drop of a barred target): the target look of a Mammoth in a ram (see the class notes).</summary>
        void RamFightStep()
        {
            if (!ZWorld.RamFightDeviation || Type != ZBigTanks.Mammoth || Kind != ZKind.Vehicle || World == null) return;
            long now = World.TickIndex;
            if (now < ramFightNext) return;
            ramFightNext = now + RamLookTicks;
            if (!RamFightOn || !CanAttack() || Owner == Team.Neutral || Drivers.Count == 0 || RamDoomed || KillAt >= 0) return;
            World.RamFightLooks++;
            var a = AttackObject;
            if (a != null && !a.Destroyed && a.KillAt < 0 && CanAttackObject(a) && WithinAttackRadius(a)) return;   // on a live target it may fire at: kept
            var pick = RamFightPick();
            if (pick == null) { if (a != null) { Disengage(); World.RamFightDrops++; } return; }              // none in range: no target, the fire held
            if (pick != a) { Engage(pick); World.RamFightPicks++; }
        }

        /// <summary>CheckPassiveEngage's own pick (engage only): an enemy unit in gun range it may fire at (DV188 leaves the rammed / doomed out);
        /// an exposed driver first, else the first one (object order).</summary>
        ZObject RamFightPick()
        {
            ZObject inRange = null;
            foreach (var o in World.NearUnits(CenterX, CenterY, NbPassiveReach(false), World.Objects))
            {
                if (!o.IsUnit || o.Destroyed || o.KillAt >= 0 || o.Owner == Team.Neutral || o.Owner == Owner || !CanAttackObject(o) || !WithinAttackRadius(o)) continue;
                if (!PrefersExposed || o.CanBeSniped()) return o;
                inRange ??= o;
            }
            return inRange;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV196 (Sim/ZObject.RamFight.cs). ZGame turns it on at every start (off in 'Zod exact'); headless default off.</summary>
        public static bool RamFightDeviation = false;

        /// <summary>The parity report and the older counted checks: off while this lives.</summary>
        public static IDisposable RamFightOff() => new RamFightOffHold();
        sealed class RamFightOffHold : IDisposable
        {
            readonly bool was = RamFightDeviation;
            public RamFightOffHold() { RamFightDeviation = false; }
            public void Dispose() { RamFightDeviation = was; }
        }

        /// <summary>Statistics (the counted check, the Play check): looks during rams, next targets engaged, targets dropped with none in range.</summary>
        public int RamFightLooks, RamFightPicks, RamFightDrops;
    }
}
