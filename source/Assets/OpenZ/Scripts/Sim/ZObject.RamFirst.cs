using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ 1.5 deviation DV189 "RAM FIRST" (kit ram_first, on top of DV180 / DV181 the big tanks, DV182 the Mammoth's ram and DV183 the Bizon's
    /// Z has no ram; every number is
    /// OpenZ's.
    /// <para>WHO: a crewed Mammoth / Bizon whose ram / crush rules are on (DV180 + DV182 / DV181 + DV183), not on HOLD POSITION, not stunned, not
    /// already ramming, and whose own state would let a ram start (DV182's RamMayStart).</para>
    /// <para>RAM-FIRST target: the Mammoth: an enemy crewed vehicle the DV182 ram may hit, EXCEPT the jeep and the light tank (v1 / v2: one rules
    /// type); the Bizon: the same vehicles + every enemy ground gun DV183 may crush. Never one in a lane zone (a bridge deck, a one-lane choke: the
    /// ram refuses those) and never one whose own ram on this tank already won (DV185).</para>
    /// <para>WHAT: when its attack target (a player's ATTACK order on it, a target it picked itself: AGRO from the idle pick, or the ATTACK the
    /// attack-move pick put in front) is a RAM-FIRST target inside its gun range, it does not stand there: it drives straight at the target's
    /// centre (its normal move, its normal speed: no boost) with the target engaged, so its gun keeps its normal shooting on the way. When the
    /// DV182 / DV183 start conditions are met (close in front, the way clear) the existing ram / crush starts in RamStep, unchanged. Outside its
    /// gun range it uses its normal approach (the ATTACK path, the AGRO drive).</para>
    /// <para>PICK: when it picks a target itself (the idle pick every 0.5 s; the attack-move pick) and RAM-FIRST targets are in its range (gun range,
    /// or agro range with a known way), the NEAREST of them comes first (squared centre distance, whole numbers; ties by object order). The
    /// attack-move pick draws its random number as before (the draw sequence is unchanged) and then takes the nearest RAM-FIRST one, if any.
    /// Jeep, light tank, robots, buildings: unchanged (it picks and shoots as before).</para>
    /// <para>FALLBACK: a target it cannot reach falls back to the old behaviour (stand and fire, Zod's / DV9's) for <see cref="RamFirstCoolTicks"/>
    /// ticks: its step blocked by a wall / water / rock, standing still for <see cref="RamFirstHeldTicks"/> ticks while it should drive (held by
    /// other units, the ram never starting), the chase longer than <see cref="RamFirstMaxTicks"/> ticks, or the pathing marks the target unreachable.
    /// HOLD POSITION: no chase (unchanged). A plain MOVE order is obeyed (no detour: only ATTACK / AGRO waypoints and the idle pick chase).</para>
    /// <para>Deterministic: whole numbers only in every decision (squared distances, tick counts), object order, no random draw of its own, no
    /// clock. Switch <see cref="ZWorld.BigTankRamFirstDeviation"/>: View/ZGame.cs on at every start (off in 'Zod exact'); headless default off; the
    /// parity report and the counted checks hold it off; it also needs DV180 / DV182 (Mammoth) or DV181 / DV183 (Bizon). Off: every hook returns at
    /// its first line, not one field of any unit changes.</para>
    /// </summary>
    public partial class ZObject
    {
        /// <summary>The fallback's cool-down (3 s), the longest chase (20 s: from the edge of the Mammoth's range to the ram's start is about 9 s at
        /// its 1 m/s, more when the target steps aside from its shells), the longest stand while it should drive (held by other units, a ram that
        /// does not start: 1 s), and the pause after which a chase of the same target counts as a new one (1 s).</summary>
        public const int RamFirstCoolTicks = 300, RamFirstMaxTicks = 2000, RamFirstHeldTicks = 100, RamFirstGapTicks = 100;

        int rfId = -1, rfIdle, rfCoolId = -1;
        long rfStart, rfLast, rfCoolUntil;

        /// <summary>DV189: the target this big tank drives in on (-1 = none).</summary>
        public int RamFirstTargetId => rfId;
        /// <summary>DV189: every DV189 field of this unit is at its start value (the switch-off check).</summary>
        public bool RamFirstFieldsAtStart => rfId == -1 && rfIdle == 0 && rfCoolId == -1 && rfStart == 0 && rfLast == 0 && rfCoolUntil == 0;

        /// <summary>This unit drives in on RAM-FIRST targets now (see WHO).</summary>
        bool RamFirstUnit() =>
            (Type == ZBigTanks.Mammoth || Type == ZBigTanks.Bizon) && RamUnit() && ram == null && !HoldPosition && !Stunned && !IsMinion && RamMayStart();

        /// <summary>t is a RAM-FIRST target of this big tank (see RAM-FIRST target).</summary>
        public bool RamFirstTarget(ZObject t)
        {
            if (t == null || !RamTargetOk(t) || (t.Kind == ZKind.Vehicle && (t.Type == "jeep" || t.Type == ZBigTanks.Light))) return false;
            if (RamDuelBarred(t, World.TickIndex)) return false;
            var lanes = World.Pathing != null && World.Pathing.Enabled ? World.Pathing : null;
            return lanes == null || !ChargeLaneZoneAt(lanes, t.CenterX, t.CenterY);
        }

        bool RamFirstCooled(ZObject t) => t != null && t.Id == rfCoolId && World.TickIndex < rfCoolUntil;

        long RamFirstD2(ZObject t) { long dx = t.CenterX - CenterX, dy = t.CenterY - CenterY; return dx * dx + dy * dy; }

        /// <summary>CheckPassiveEngage, before Zod's pick (every 0.5 s): an idle big tank with RAM-FIRST targets in range takes the nearest one
        /// (engaged at once, an AGRO waypoint on it: ProcessAgroWP drives in). True = picked (Zod's pick does not run this time).</summary>
        bool RamFirstPassive(bool engageOnly)
        {
            if (!ZWorld.BigTankRamFirstDeviation) return false;
            if (engageOnly || Waypoints.Count != 0 || CrocWary || World.CalmStart(Owner) || !RamFirstUnit()) return false;
            ZObject best = null; long bd = long.MaxValue;
            var all = World.Objects;
            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                if (!RamFirstTarget(o) || RamFirstCooled(o) || !CanAttackObject(o)) continue;
                long d2 = RamFirstD2(o);
                if (d2 >= bd) continue;
                if (!WithinAttackRadius(o) && !(WithinAgroRadius(o) && !PathUnreachable(o))) continue;
                best = o; bd = d2;
            }
            if (best == null) return false;
            Engage(best);
            Waypoints.Add(new ZWaypoint { mode = WpMode.Agro, refId = best.Id, x = best.CenterX, y = best.CenterY });
            World.RamFirstPicks++;
            return true;
        }

        /// <summary>CheckAttackTo (the attack-move pick), after its random draw: the nearest RAM-FIRST choice instead, if there is one.</summary>
        ZObject RamFirstPrefer(List<ZObject> choices, ZObject drawn)
        {
            if (!ZWorld.BigTankRamFirstDeviation || !RamFirstUnit()) return drawn;
            ZObject best = null; long bd = long.MaxValue;
            for (int i = 0; i < choices.Count; i++)
            {
                var o = choices[i];
                if (!RamFirstTarget(o) || RamFirstCooled(o)) continue;
                long d2 = RamFirstD2(o);
                if (d2 < bd) { best = o; bd = d2; }
            }
            if (best == null) return drawn;
            if (best != drawn) World.RamFirstPrefers++;
            return best;
        }

        /// <summary>ProcessAttackWP / ProcessAgroWP: a RAM-FIRST target inside its gun range: drive straight in, the gun on it. True = this tick
        /// is done (it drove, or it stood held); false = the old way this tick (out of range, not a RAM-FIRST target, the cool-down, a fallback).</summary>
        bool RamFirstStep(ZObject t, double dt, bool attackWp)
        {
            if (!ZWorld.BigTankRamFirstDeviation) return false;
            if (!RamFirstUnit() || !RamFirstTarget(t) || RamFirstCooled(t) || !WithinAttackRadius(t)) return false;
            long now = World.TickIndex;
            if (rfId != t.Id || now - rfLast > RamFirstGapTicks) { rfId = t.Id; rfStart = now; rfIdle = 0; World.RamFirstChases++; }
            rfLast = now;
            if (now - rfStart > RamFirstMaxTicks) { RamFirstGiveUp(t, attackWp); World.RamFirstEndsTime++; return false; }
            if (PathUnreachable(t)) { RamFirstGiveUp(t, attackWp); World.RamFirstEndsWay++; return false; }
            Engage(t);                                                                   // the gun keeps its normal shooting on the way
            SetTarget(t.CenterX, t.CenterY);
            double tile = World.Map.TileWalkSpeed(CenterX, CenterY);
            if (World.Pathing != null) tile = World.Pathing.SafeTileSpeed(this, tile);
            RealMoveSpeed = MoveSpeed * tile * DamagedSpeed() * RunSpeed();
            SetVelocity();
            World.RamFirstDriveTicks++;
            int lx = LocX, ly = LocY; double xo = XOver, yo = YOver;
            if (!ProcessMove(dt, true, out _, out _)) { RamFirstGiveUp(t, attackWp); World.RamFirstEndsBlocked++; return true; }
            if (LocX != lx || LocY != ly || XOver != xo || YOver != yo) rfIdle = 0;                                // it drove (as RamStep's own test)
            else if (++rfIdle > RamFirstHeldTicks) { RamFirstGiveUp(t, attackWp); World.RamFirstEndsHeld++; }      // held by other units / the ram never starting
            return true;
        }

        /// <summary>The chase is given up: the old behaviour for the cool-down (it stops; an ATTACK order plans its approach afresh).</summary>
        void RamFirstGiveUp(ZObject t, bool attackWp)
        {
            rfCoolId = t.Id; rfCoolUntil = World.TickIndex + RamFirstCoolTicks; rfId = -1;
            StopMove();
            if (attackWp) ClearCur();
            World.RamFirstGiveUps++;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV189 RAM FIRST (Sim/ZObject.RamFirst.cs). ZGame turns it on at every start (off in 'Zod exact'); headless default off.</summary>
        public static bool BigTankRamFirstDeviation = false;

        /// <summary>The parity report and the older counted checks: off while this lives.</summary>
        public static IDisposable RamFirstOff() => new RamFirstOffHold();
        sealed class RamFirstOffHold : IDisposable
        {
            readonly bool was = BigTankRamFirstDeviation;
            public RamFirstOffHold() { BigTankRamFirstDeviation = false; }
            public void Dispose() { BigTankRamFirstDeviation = was; }
        }

        /// <summary>Statistics (the counted check): idle picks, attack-move picks changed, chases started, ticks driven in, give-ups and why.</summary>
        public int RamFirstPicks, RamFirstPrefers, RamFirstChases, RamFirstDriveTicks, RamFirstGiveUps, RamFirstEndsTime, RamFirstEndsWay, RamFirstEndsHeld, RamFirstEndsBlocked;
    }
}
