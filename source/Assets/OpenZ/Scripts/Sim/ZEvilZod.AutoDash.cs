using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// attack order. Now it also starts by itself, the same dash (windup, burst, plant, the shot at the full number, the same cooldown), when all of this holds:
    /// <list type="bullet">
    /// <item>the dash is ready (its cooldown over) and the gun is ready by the end of the windup (EvilDashAllowed, as for an ordered dash);</item>
    /// <item>he is under attack: an enemy's hit on him (ZObject.LastEnemyHit, enemy teams only) at most <see cref="ZEvilAutoDash.UnderFireSec"/> s ago;</item>
    /// <item>no enemy unit he can attack stands in his shot range (WithinAttackRadius: his range, Zod's barriers, DV23's line): with one in range he stands and fires;</item>
    /// <item>the nearest enemy unit he can attack (CanAttackObject; on the ground; a clear line and no barrier, the dash's own sight rule) is inside the dash range
    /// (<see cref="ZEvilZod.DashRangePx"/>), and the ordered dash's plan finds its landing spot (EvilDashPlan: no cliff, no lava, no deep water, no hull on the line,
    /// the stop at most <see cref="ZEvilZod.DashMaxLandPx"/> from that enemy);</item>
    /// <item>he has no player order at all (no waypoint given by the player or his side's computer: a move, an attack, an entry, a patrol, a lava walk), does not
    /// hold position (DV68), does not guard the blue fort as its last defense (DV165, <see cref="ZObject.EvilGuarding"/>: he holds his spot), is not stunned and not
    /// getting out of a shuttle (that hold comes first in ProcessServer).</item>
    /// </list>
    /// The dash goes at that enemy: an ATTACK waypoint nobody ordered (the help call's kind) is put in front of his list, so the windup's own test, the plant and the shot
    /// after the stop run exactly as for an ordered attack; the waypoints he had (an agro chase) follow it. A player order in the windup cancels it and gives the
    /// cooldown back, as for an ordered dash. EvilDashStart carries flag = true for an auto dash (the view's log); every other number of the event is the same.
    /// Asked every <see cref="ZEvilZod.RetryTicks"/> while the cheap conditions hold. Determinism: sim ticks, integer px (squared distances in longs), object order
    /// (the first of two at the same distance), no random draw, no float. Switch <see cref="ZWorld.EvilAutoDashDeviation"/>: on by default (the game never sets it;
    /// it does something only where an EvilZod stands, and DV161 has him only outside 'Zod exact'); off = DV161 round 2's dash on orders only, bit for bit.
    /// </summary>
    public static class ZEvilAutoDash
    {
        /// <summary>He counts as under attack while the last enemy hit on him is at most this old (s, sim time).</summary>
        public const double UnderFireSec = 2.0;
    }

    public partial class ZObject
    {
        long evilAutoNextTry;
        /// <summary>Checks: the auto dashes this EvilZod started.</summary>
        public int EvilAutoDashes { get; private set; }

        /// <summary>EvilStep, no dash running: true = an auto dash started this tick (its windup holds him from this tick on).</summary>
        bool EvilAutoDash(double time, long now)
        {
            if (!ZWorld.EvilAutoDashDeviation || evilDashReady != 0 || now < evilAutoNextTry) return false;
            if (time - LastEnemyHit > ZEvilAutoDash.UnderFireSec || EvilGuarding || HoldPosition || KillAt >= 0 || Stunned || !CanOverwriteWP()) return false;
            for (int i = 0; i < Waypoints.Count; i++) if (Waypoints[i].playerGiven) return false;       // a player order runs: it wins (DV16)
            evilAutoNextTry = now + ZEvilZod.RetryTicks;
            var t = EvilAutoTarget();
            if (t == null || !EvilDashAllowed(t, now) || !EvilDashPlan(t, out int lx, out int ly)) return false;
            if (Waypoints.Count == 0 || Waypoints[0].mode != WpMode.Attack || Waypoints[0].refId != t.Id)
                Waypoints.Insert(0, new ZWaypoint { mode = WpMode.Attack, refId = t.Id, x = t.CenterX, y = t.CenterY });
            UpdatedWaypoints = true;
            EvilDashBegin(t, lx, ly, now, true);
            EvilHold(time);
            EvilAutoDashes++; World.EvilAutoDashes++;
            return true;
        }

        /// <summary>The nearest enemy unit on the ground he can attack, inside the dash range, with a clear line and no barrier; null when an enemy he can attack
        /// stands in his shot range (he fires at it) or nobody is there. Object order, whole px.</summary>
        ZObject EvilAutoTarget()
        {
            ZObject best = null; long bestD2 = long.MaxValue, reach2 = (long)ZEvilZod.DashRangePx * ZEvilZod.DashRangePx;
            foreach (var o in World.UnitObjects)
            {
                if (o == this || o.Owner == Team.Neutral || o.Owner == Owner || o.Destroyed || o.KillAt >= 0 || !CanAttackObject(o)) continue;
                long dx = o.CenterX - CenterX, dy = o.CenterY - CenterY, d2 = dx * dx + dy * dy;
                if (d2 > reach2) continue;
                if (WithinAttackRadius(o)) return null;                                        // an enemy in his range: he stands and fires (the passive engage)
                if (o.ShuttleAirborne || d2 >= bestD2) continue;
                if (!World.Sight.Clear(this, o) || World.EngageBarrierBetween(CenterX, CenterY, o.CenterX, o.CenterY)) continue;
                best = o; bestD2 = d2;
            }
            return best;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>1.4 DV161 round 3 switch: EvilZod's auto dash (Sim/ZEvilZod.AutoDash.cs). On by default, the game never sets it; nothing happens without an EvilZod.</summary>
        public static bool EvilAutoDashDeviation = true;
        /// <summary>1.4 statistics of this world (checks): auto dashes started (they count in EvilDashes too).</summary>
        public int EvilAutoDashes;
    }
}
