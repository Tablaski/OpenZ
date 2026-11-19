using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// The Unit editor's BOX (Sim/ZWorld.UeBoxes.cs) is attacked from the attacker's weapon range, never from
    /// contact.
    /// <para>THE CAUSE (measured, kit_tools/harness): a box is a destroyable barrier, and Zod plans the path of a unit with explosives (every tank, a
    /// Tough, a squad with grenades) THROUGH destroyable barriers and blasts the one it bumps into (DoAttackImpassableAtCoords, zobject.cpp:3716-3747):
    /// the attack waypoint is inserted at the moment of contact, so the tank stands touching the box and fires point blank. The Unit editor's plain
    /// right-click on a box is a MOVE to the ground point under the pointer (OzUeHooks.PlainObstacleClick: the box's own footprint when the pointer is
    /// on its low side): the path runs into the box, and the Bizon, the Mammoth, the heavy, medium and light tank all drove up to 15 px (the sim box
    /// touching) and fired from there (the owner's frame: the Bizon's nose on the bottom box, the Mammoth held right behind it). The Ctrl + right-click
    /// ATTACK order was not the cause: it already stopped at the weapon range (DV28's nearest spot in range; no box blocks a shot at a box: a box is no
    /// sight blocker and EngageBarrierBetween is never asked for a destroyable target), but measured to the box's centre: 16 px (one tile) closer to
    /// its face than the range.</para>
    /// <para>THE RULE (boxes only; the holo obstacle, rocks, huts and wrecks exactly as before):</para>
    /// <list type="bullet">
    /// <item>REACH: the range to a box is measured to the nearest point of its footprint (its near face), as DV26 measures a building's walls: an
    /// attacker stops at its weapon range from the face (<see cref="BoxFaceReach"/>, hook in ZObject.InReach: WithinAttackRadius, DV28's spot, the
    /// shot spot all use it). The shell is still aimed at the box's middle (the damage, its scatter and the one-level blast are unchanged).</item>
    /// <item>IN THE WAY: a unit that would blast what stands in its way (explosives; a squad's leader, never a minion), on a stoppable walk (move,
    /// attack approach, agro, enter, pickup ...), looks along its own way (its current leg, then its path points, the hull's 2 x 2 / 1 x 1 tile box
    /// in steps of 8 px, up to its range + 16 px): the first standing box its hull would touch is attacked NOW (the same waypoint Zod inserts at the
    /// contact) as soon as that box is inside its weapon range (WithinAttackRadius: the face reach, a clear line); it stands there and fires, and its
    /// order goes on when the box is gone (a box that falls into the gap is the next one in the way). A box of the stack it is already attacking is
    /// not taken twice. A box outside the range is driven on to; Zod's contact rule stays the fallback (a box it cannot fire at from range).</item>
    /// <item>NEVER a ram / crush / shove / chain target: a box is neutral (RamTargetOk: Owner != Neutral, so DV182 / DV183 / DV189 / DV195 / DV196 never
    /// take it) and never a vehicle wreck (DV193's crushWrecks only holds VehicleWreck items); the counted check proves both.</item>
    /// </list>
    /// <para>Every hook returns at its first test in a world where no box was ever placed (every level and match): no field written, no random
    /// draw, the same tick. Whole numbers only (pixels, squared distances, integer steps), object order and levels for ties: deterministic.</para>
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>Checks: boxes attacked from range because they stood in a unit's way (N29), way scans run.</summary>
        public int BoxWayAttacks, BoxWayScans;

        /// <summary>Boxes stand in this world (false at once in every level).</summary>
        internal bool BoxesStand => boxes != null && boxes.Count > 0;

        /// <summary>The standing box that covers tile (i, j): the lowest level, then the lowest id; null = none.</summary>
        public ZObject BoxAt(int i, int j)
        {
            if (boxes == null) return null;
            ZObject best = null; int bl = int.MaxValue;
            for (int k = 0; k < boxes.Count; k++)
            {
                var b = boxes[k];
                if (!BoxStanding(b) || !BoxOverlaps(b, i, j, 1)) continue;
                int l = boxLevel[b.Id];
                if (best == null || l < bl || (l == bl && b.Id < best.Id)) { best = b; bl = l; }
            }
            return best;
        }

        /// <summary>ZObject.InReach hook: o is a box of this world (false at once in every world without one). inReach = the nearest point of its
        /// footprint (its near face) lies within range of (x, y); squared whole pixels, as DV26's walls.</summary>
        internal bool BoxFaceReach(ZObject o, int x, int y, double range, out bool inReach)
        {
            inReach = false;
            if (boxLevel == null || !boxLevel.ContainsKey(o.Id)) return false;
            inReach = BoxFaceD2(o, x, y) <= (long)(range * range);
            return true;
        }

        /// <summary>Squared distance (px) from (x, y) to the nearest pixel of o's footprint (0 inside it).</summary>
        public static long BoxFaceD2(ZObject o, int x, int y)
        {
            int px = Math.Max(o.LocX, Math.Min(x, o.LocX + o.WidthPix - 1)), py = Math.Max(o.LocY, Math.Min(y, o.LocY + o.HeightPix - 1));
            long dx = px - x, dy = py - y;
            return dx * dx + dy * dy;
        }

        /// <summary>The standing box under a hull of w x h px with its centre at (cx, cy), the tiles Zod's WithinImpassable reads (the box shrunk by
        /// 1 px each side); the lowest level, then the lowest id; null = none.</summary>
        internal ZObject BoxUnderHull(int cx, int cy, int w, int h)
        {
            int lx = cx - (w >> 1), ly = cy - (h >> 1);
            int i0 = (lx + 1) >> 4, j0 = (ly + 1) >> 4, i1 = (lx + w - 2) >> 4, j1 = (ly + h - 2) >> 4;
            ZObject best = null; int bl = int.MaxValue;
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    if (!Path.HasDestroyableBarrier(i, j)) continue;                         // a box's tiles are destroyable barriers (cheap test first)
                    var b = BoxAt(i, j);
                    if (b == null) continue;
                    int l = boxLevel[b.Id];
                    if (best == null || l < bl || (l == bl && b.Id < best.Id)) { best = b; bl = l; }
                }
            return best;
        }

        /// <summary>t is box b, or a box whose footprint overlaps b's (the same stack, a brick over it).</summary>
        public bool BoxSameColumn(ZObject t, ZObject b)
        {
            if (t == null || b == null) return false;
            if (t == b) return true;
            return IsBox(t) && BoxOverlaps(t, b.LocX >> 4, b.LocY >> 4, BoxTiles);
        }

        /// <summary>A standing box lies within reach px (Chebyshev, of the footprint) of (x, y): the way scan's quick gate.</summary>
        internal bool BoxNear(int x, int y, int reach)
        {
            for (int k = 0; k < boxes.Count; k++)
            {
                var b = boxes[k];
                if (!BoxStanding(b)) continue;
                int dx = Math.Max(0, Math.Max(b.LocX - x, x - (b.LocX + b.WidthPix - 1))), dy = Math.Max(0, Math.Max(b.LocY - y, y - (b.LocY + b.HeightPix - 1)));
                if (dx <= reach && dy <= reach) return true;
            }
            return false;
        }
    }

    public partial class ZObject
    {
        /// <summary>The way scan's step (px) and how far past the range it looks (px, the hull's half).</summary>
        public const int BoxWayStepPx = 8, BoxWayExtraPx = 16;

        /// <summary>ProcessMoveOrKillWP hook (a stoppable walk, before the step): a box in this unit's way within its weapon range is attacked now, from
        /// here (see ZWorld.UeBoxRange.cs). True = the attack waypoint was put first (the walk stops this tick, as after Zod's contact insert).</summary>
        internal bool BoxInTheWay()
        {
            if (World == null || !World.BoxesStand) return false;                              // every level: no box was ever placed
            if (IsMinion || Waypoints.Count == 0 || !HasExplosives()) return false;
            int reach = AttackRadius + BoxWayExtraPx;
            if (!World.BoxNear(CenterX, CenterY, reach + (WidthPix > HeightPix ? WidthPix : HeightPix))) return false;
            World.BoxWayScans++;
            var b = BoxOnWay(reach);
            if (b == null) return false;
            var w0 = Waypoints[0];
            if ((w0.mode == WpMode.Attack || w0.mode == WpMode.Agro) && World.BoxSameColumn(World.Get(w0.refId), b)) return false;   // already its target, or a box of the stack it attacks
            if (!CanAttackObject(b) || !WithinAttackRadius(b)) return false;                    // not yet in range (drive on), or no shot from here
            Waypoints.Insert(0, new ZWaypoint { mode = WpMode.Attack, refId = b.Id, x = b.CenterX, y = b.CenterY });   // Zod's own insert (DoAttackImpassableAtCoords)
            CloneMinionWayPoints();
            World.BoxWayAttacks++;
            return true;
        }

        /// <summary>The first standing box the hull would touch along its way (the current leg to cur, then the path points), sampled every
        /// <see cref="BoxWayStepPx"/> px up to `reach` px of travel (Chebyshev); null = none.</summary>
        ZObject BoxOnWay(int reach)
        {
            int x0 = CenterX, y0 = CenterY, used = 0;
            int legs = 1 + (cur.pfPoints != null ? cur.pfPoints.Count : 0);
            for (int k = 0; k < legs && used <= reach; k++)
            {
                int x1 = k == 0 ? cur.x : cur.pfPoints[k - 1].x, y1 = k == 0 ? cur.y : cur.pfPoints[k - 1].y;
                int dx = x1 - x0, dy = y1 - y0, len = Math.Max(Math.Abs(dx), Math.Abs(dy));
                int n = len / BoxWayStepPx + 1;
                for (int s = k == 0 ? 0 : 1; s <= n; s++)
                {
                    if (used + len * s / n > reach) break;
                    var b = World.BoxUnderHull(x0 + dx * s / n, y0 + dy * s / n, WidthPix, HeightPix);
                    if (b != null) return b;
                }
                used += len; x0 = x1; y0 = y1;
            }
            return null;
        }
    }
}
