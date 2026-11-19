using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// its range and fired; the second parked its nose on the box and never fired). A group ordered onto one of the Unit editor's BOXES
    /// (Sim/ZWorld.UeBoxes.cs) takes firing places: every unit its own, inside its own weapon range.
    /// <para>THE CAUSE (measured, kit_tools/harness): a plain right-click on a box is a MOVE to one point for every selected unit. The group spread
    /// (ZSlots.AssignSlots: a group's MOVE to one point gives every unit its own spot round it, nearest the point first) hands out the spots right
    /// beside the box: a box at (1440, 832), the second tank's spot (1456, 800). Only a unit whose own way crosses the box attacks it from range (N29's
    /// rule, Sim/ZWorld.UeBoxRange.cs); every other unit drives to its spot beside the box, stands against it and has no order to fire. The ATTACK
    /// order (Ctrl + right-click) had the twin fault: every unit plans the same nearest spot in range (DV28), the first ones stand there and the rest
    /// queue behind them out of range (12 light tanks: 4 fired).</para>
    /// <para>THE RULE (boxes only, groups of two or more; one unit alone exactly as before):</para>
    /// <list type="bullet">
    /// <item>THE ORDER. Units that get, in the same tick, a single MOVE onto the same box place (the clicked point lies on a standing box's tile) or a
    /// single ATTACK at the same box are one box group (<see cref="BoxOrderNote"/>, hook in SendWaypoints; <see cref="BoxPlacesTick"/>, hook after the
    /// group spread of the tick).</item>
    /// <item>THE PLACES (ZPathing.BoxGroupPlaces). A place of a unit: a footprint of its own graph, standable without blasting anything, no
    /// farther than its range - <see cref="BoxPlaceInPx"/> px and no nearer than <see cref="BoxPlaceMinPx"/> px from the box's near face, with a clear
    /// line for direct fire, outside the lanes' keep-clear areas, its room (its spot radius + <see cref="BoxPlaceGapPx"/> px) clear of every place
    /// given before it, of every unit that stands near and is not of the group, and of the straight way of every unit placed before it; reached
    /// in a straight line past nobody's room and past no box. A SMALL group stands at the range's edge: nearest the box first (then the lower id),
    /// each unit the place nearest to where it stands within <see cref="BoxPlaceBandPx"/> px of its range. When not every unit gets one that way
    /// (a big group), the units behind choose first: each the place nearest its ideal point (where its straight way to the box comes into its
    /// range), beside the places taken or in front of them, whichever is nearer - the units in front go deeper, never nearer than
    /// <see cref="BoxPlaceMinPx"/> px. A unit that cannot blast a box (a jeep, a rifle squad) takes a waiting place by the same rule (its own range,
    /// <see cref="BoxWaitRangePx"/> px without a weapon): nobody is sent to a spot against the box. Ties: the lower row, then the lower column.</item>
    /// <item>THE WALK. Its MOVE goes to its place, as its own order (no group spot rules: every place is its own free point). An ATTACK order keeps
    /// its attack behind that move.</item>
    /// <item>THE FIRE. A unit of a MOVE group that can blast boxes and stands on no waypoint attacks the lowest standing box on the ordered place
    /// (<see cref="ZObject.BoxOrderStand"/>, Zod's own attack waypoint): at its place it is in range and fires; stopped short of it, the attack walks
    /// it into range (DV28) and no closer. A box that falls into the gap is the next one; when no box is left on the place the order is over and it
    /// stays at its firing place. Any new order (STOP and HOLD too) ends it.</item>
    /// <item>NO PLACE (the ground round the box is full or closed): a unit that can blast it gets the plain attack order on it (it stops at the first
    /// spot in range, never at the contact); a unit that cannot keeps its move.</item>
    /// </list>
    /// <para>Every hook returns at its first test in a world where no box was ever placed (every level and match): no field written, no random draw,
    /// the same tick. Whole numbers only (pixels, squared distances), object order and ids for ties: deterministic.</para>
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>A place lies this far inside the unit's range (px, as DV23's shot spot); a small group's within this band behind that (px); never
        /// nearer the box's face than <see cref="BoxPlaceMinPx"/> px; the extra room round a place (px); a straight way keeps <see cref="BoxWayOffPx"/> px +
        /// the unit's room off a box's middle; the waiting distance (px) of a unit without a weapon.</summary>
        public const int BoxPlaceInPx = 4, BoxPlaceBandPx = 44, BoxPlaceMinPx = 64, BoxPlaceGapPx = 8, BoxWayOffPx = 48, BoxWaitRangePx = 120;

        List<(ZObject o, bool attack, int key, int boxId)> boxPending;
        Dictionary<int, int> boxOrder;                                                       // leader id -> the tile (row * width + column) its MOVE was ordered onto
        /// <summary>Checks: box groups given places, places given, attack waypoints given to a standing unit of a box group.</summary>
        public int BoxGroupOrders, BoxGroupPlaces, BoxOrderAttacks;

        /// <summary>A unit of this world still holds a box group's order (false at once in every level).</summary>
        internal bool BoxOrdersOpen => boxOrder != null && boxOrder.Count > 0;

        /// <summary>SendWaypoints hook (the unit's new list is in): any new order ends its box order; a single MOVE onto a standing box's tile, or a
        /// single ATTACK at a standing box, is noted for this tick's box groups. Returns at once in a world where no box was ever placed.</summary>
        void BoxOrderNote(ZObject o)
        {
            if (boxLevel == null) return;                                                    // every level: no box was ever placed
            boxOrder?.Remove(o.Id);
            if (boxes.Count == 0 || o.IsMinion || !o.CanMove || o.Waypoints.Count != 1) return;
            var w0 = o.Waypoints[0]; ZObject b; bool attack = w0.mode == WpMode.Attack; int key;
            if (attack) { b = Get(w0.refId); if (!IsBox(b) || !BoxStanding(b)) return; key = b.Id; }
            else if (w0.mode == WpMode.Move && w0.refId == -1)
            {
                int tx = w0.x >> 4, ty = w0.y >> 4;
                if (tx < 0 || ty < 0 || tx >= Map.width || ty >= Map.height) return;
                b = BoxAt(tx, ty); if (b == null) return;
                key = ty * Map.width + tx;
            }
            else return;
            boxPending ??= new List<(ZObject, bool, int, int)>();
            for (int i = 0; i < boxPending.Count; i++) if (boxPending[i].o == o) { boxPending.RemoveAt(i); break; }
            boxPending.Add((o, attack, key, b.Id));
        }

        /// <summary>The unit still holds, alone on its list, the order it was noted with (the group spread may have moved a MOVE's point).</summary>
        bool BoxOrderHolds(ZObject o, bool attack, int boxId)
        {
            if (o == null || Get(o.Id) != o || o.Destroyed || o.KillAt >= 0 || o.Owner == Team.Neutral || o.IsMinion || !o.CanMove || o.Waypoints.Count != 1) return false;
            var w0 = o.Waypoints[0];
            return attack ? w0.mode == WpMode.Attack && w0.refId == boxId : w0.mode == WpMode.Move && w0.refId == -1;
        }

        /// <summary>Tick hook (after Pathing.BeginTick: this tick's group spread is done, no unit has moved yet): the box groups of the orders given
        /// since the last tick take their places. Returns at once while nothing is noted (every level).</summary>
        void BoxPlacesTick()
        {
            if (boxPending == null || boxPending.Count == 0) return;
            int n = boxPending.Count;
            if (Pathing != null && Pathing.Enabled && boxes != null && boxes.Count > 0)
            {
                var done = new bool[n]; var group = new List<ZObject>();
                for (int i = 0; i < n; i++)
                {
                    if (done[i]) continue;
                    var (_, attack, key, boxId) = boxPending[i];
                    group.Clear();
                    for (int j = i; j < n; j++)
                    {
                        var (oj, aj, kj, bj) = boxPending[j];
                        if (done[j] || aj != attack || kj != key) continue;
                        done[j] = true;
                        if (BoxOrderHolds(oj, attack, bj)) group.Add(oj);
                    }
                    var box = attack ? Get(boxId) : BoxAt(key % Map.width, key / Map.width);
                    if (group.Count < 2 || box == null || !BoxStanding(box)) continue;
                    BoxGroupOrders++;
                    BoxGroupPlaces += Pathing.BoxGroupPlaces(group, box, attack, key);
                }
            }
            boxPending.Clear();
        }

        /// <summary>ZPathing.BoxGroupPlaces: unit u of a MOVE group attacks what stands on tile index `tile` when it stands on no waypoint.</summary>
        internal void BoxOrderSet(ZObject u, int tile) => (boxOrder ??= new Dictionary<int, int>())[u.Id] = tile;

        /// <summary>The box unit u's box order attacks next: the lowest standing box on the ordered tile; null = none (the order is over, or u can
        /// no longer blast boxes: its note is dropped).</summary>
        internal ZObject BoxOrderNext(ZObject u)
        {
            if (boxOrder == null || !boxOrder.TryGetValue(u.Id, out int tile)) return null;
            var b = BoxAt(tile % Map.width, tile / Map.width);
            if (b == null || u.IsMinion || u.Owner == Team.Neutral || !u.CanAttackObject(b)) { boxOrder.Remove(u.Id); return null; }
            BoxOrderAttacks++;
            return b;
        }

        /// <summary>View/PlayerInput.BuildWaypoint (a plain right-click on a playground obstacle is a MOVE): on a standing box the move's point is
        /// the box's own middle, not the ground point under the pointer (on a box up a stack that point lies behind the stack, on no box's tile).</summary>
        public void BoxClickPoint(ZObject h, ref int x, ref int y)
        {
            if (!IsBox(h) || !BoxStanding(h)) return;
            x = h.CenterX; y = h.CenterY;
        }
    }

    public sealed partial class ZPathing
    {
        /// <summary>The firing places of a box group (see ZWorld.UeBoxGroup.cs): the places at the range's edge when every unit of the group gets
        /// one (<see cref="BoxPlacesAtRange"/>: a small group); else the places round every unit's ideal point, the units behind first
        /// (<see cref="BoxPlaceFor"/>). Returns the places given.</summary>
        internal int BoxGroupPlaces(List<ZObject> group, ZObject box, bool attack, int tile)
        {
            var w = W; int placed = 0, n = group.Count;
            group.Sort((a, b) =>
            {                                                                                 // the unit nearest the box first (then the lower id)
                long da = ZWorld.BoxFaceD2(box, a.CenterX, a.CenterY), db = ZWorld.BoxFaceD2(box, b.CenterX, b.CenterY);
                return da != db ? da.CompareTo(db) : a.Id.CompareTo(b.Id);
            });
            var stand = new List<(int x, int y, int r)>();                                    // the room of who stands near and is not of the group
            foreach (var o in w.Objects)
            {
                if (!Separable(o) || group.Contains(o) || (o.Leader != null && group.Contains(o.Leader))) continue;
                if (Moving(o) && !Static(o)) continue;                                        // walking on: it will not be there
                stand.Add((o.CenterX, o.CenterY, Radius(o) + 2));
            }
            var block = new List<(int x, int y, int r)>(stand);                               // what a unit's straight way may not pass: who stands near, the boxes
            foreach (var b in w.Boxes) block.Add((b.CenterX, b.CenterY, ZWorld.BoxWayOffPx));
            var px = new int[n]; var py = new int[n]; var got = new bool[n];
            int wanted = 0; foreach (var u in group) if (!attack || u.CanAttackObject(box)) wanted++;
            if (BoxPlacesAtRange(group, box, attack, stand, block, px, py, got) < wanted)
            {
                var taken = new List<(int x, int y, int r)>(stand);
                var ways = new List<(int x0, int y0, int x1, int y1, int r)>();               // the straight ways of the units placed before (the units behind)
                for (int i = n - 1; i >= 0; i--)
                {                                                                             // the unit farthest from the box first
                    var u = group[i]; bool blaster = u.CanAttackObject(box);
                    got[i] = !(attack && !blaster) && BoxPlaceFor(u, box, blaster, taken, block, ways, out px[i], out py[i]);
                    if (!got[i]) continue;
                    int r = SlotRadius(u) + ZWorld.BoxPlaceGapPx;
                    taken.Add((px[i], py[i], r)); ways.Add((u.CenterX, u.CenterY, px[i], py[i], r));
                }
            }
            for (int i = 0; i < n; i++)
            {
                var u = group[i]; bool blaster = u.CanAttackObject(box);
                if (attack && !blaster) continue;
                var w0 = u.Waypoints[0];
                if (!got[i])
                {
                    if (!attack && blaster)
                    {                                                                         // no place: the plain attack order on it (the first spot in range)
                        BoxOwnOrder(u);
                        u.Waypoints[0] = new ZWaypoint { mode = WpMode.Attack, refId = box.Id, x = box.CenterX, y = box.CenterY, playerGiven = w0.playerGiven };
                        w.BoxOrderSet(u, tile);
                        u.CloneMinionWayPoints();
                    }
                    continue;
                }
                BoxOwnOrder(u);
                if (attack) u.Waypoints.Insert(0, new ZWaypoint { mode = WpMode.Move, refId = -1, x = px[i], y = py[i], playerGiven = w0.playerGiven });
                else
                {
                    w0.x = px[i]; w0.y = py[i]; u.Waypoints[0] = w0;
                    if (blaster) w.BoxOrderSet(u, tile);
                }
                u.CloneMinionWayPoints();
                placed++;
                Log(u, "box group: its " + (blaster ? "firing" : "waiting") + " place (" + px[i] + "," + py[i] + ")");
            }
            return placed;
        }

        /// <summary>A footprint (tile tx, ty; its middle x, y) where unit u can stand and fire at the box: of its own graph, standable without
        /// blasting, outside the keep-clear areas, a clear line for direct fire.</summary>
        bool BoxSpotOk(ZObject u, ZObject box, int tx, int ty, int x, int y, bool robot, bool nr, int comp, bool line, int ex) =>
            Reach.Comp(tx, ty, robot, nr) == comp && Reach.Comp(tx, ty, robot, false) >= 0 && KeepClearAt(x, y) == null
            && (!line || W.Sight.Clear(x, y, 0, box.CenterX, box.CenterY, ex));

        /// <summary>The places at the range's edge (a small group), the unit nearest the box first: between its range - BoxPlaceBandPx and its
        /// range - BoxPlaceInPx px from the box's near face, its room clear of every room taken and of the straight way of every unit placed
        /// before it, reached in a straight line past no room taken and no box; of those the nearest to where it stands (ties: the lower row,
        /// then the lower column). Returns the places found (px, py, got per unit): the caller uses them only when nobody is left out.</summary>
        int BoxPlacesAtRange(List<ZObject> group, ZObject box, bool attack, List<(int x, int y, int r)> stand, List<(int x, int y, int r)> block0, int[] px, int[] py, bool[] got)
        {
            var w = W; int n = 0;
            var taken = new List<(int x, int y, int r)>(stand);
            var block = new List<(int x, int y, int r)>(block0);
            var ways = new List<(int x0, int y0, int x1, int y1, int r)>();
            for (int i = 0; i < group.Count; i++)
            {
                var u = group[i]; got[i] = false;
                bool blaster = u.CanAttackObject(box);
                if (attack && !blaster) continue;
                bool robot = u.IsRobot, nr = NoRocks(u); int comp = Reach.UnitComp(u, nr);
                int range = blaster ? u.RangeTo(box) : u.AttackRadius > 0 ? u.AttackRadius : ZWorld.BoxWaitRangePx;
                int hi = range - ZWorld.BoxPlaceInPx, lo = Math.Max(range - ZWorld.BoxPlaceBandPx, ZWorld.BoxPlaceMinPx);
                if (comp < 0 || hi < lo) return n;
                int r = SlotRadius(u) + ZWorld.BoxPlaceGapPx;
                bool line = blaster && u.LosActive; int ex = line ? w.Sight.ExemptOf(box) : 0;
                bool found = false; long best = long.MaxValue; int bx = 0, by = 0;
                int tx0 = Math.Max(0, ((box.LocX - hi) >> 4) - 2), ty0 = Math.Max(0, ((box.LocY - hi) >> 4) - 2);
                int tx1 = Math.Min(w.Map.width - 1, ((box.LocX + box.WidthPix + hi) >> 4) + 1), ty1 = Math.Min(w.Map.height - 1, ((box.LocY + box.HeightPix + hi) >> 4) + 1);
                for (int ty = ty0; ty <= ty1; ty++)
                    for (int tx = tx0; tx <= tx1; tx++)
                    {
                        var (x, y) = ZReach.FootCentre(tx, ty, robot);
                        long d2 = ZWorld.BoxFaceD2(box, x, y);
                        if (d2 > (long)hi * hi || d2 < (long)lo * lo) continue;
                        long da = (long)(x - u.CenterX) * (x - u.CenterX) + (long)(y - u.CenterY) * (y - u.CenterY);
                        if (found && da >= best) continue;                                    // (no nearer than the best one)
                        if (!BoxSpotOk(u, box, tx, ty, x, y, robot, nr, comp, line, ex) || !BoxRoomFree(x, y, r, taken, ways)) continue;
                        if (!BoxWayFree(u.CenterX, u.CenterY, x, y, r, block)) continue;
                        found = true; best = da; bx = x; by = y;
                    }
                if (!found) return n;                                                         // (one unit left out: not the range's edge for this group)
                got[i] = true; px[i] = bx; py[i] = by; n++;
                taken.Add((bx, by, r)); block.Add((bx, by, r)); ways.Add((u.CenterX, u.CenterY, bx, by, r));
            }
            return n;
        }

        /// <summary>The room (radius r) round (x, y) is clear of every room taken and of the straight way of every unit placed before.</summary>
        static bool BoxRoomFree(int x, int y, int r, List<(int x, int y, int r)> taken, List<(int x0, int y0, int x1, int y1, int r)> ways)
        {
            for (int k = 0; k < taken.Count; k++)
            {
                var t = taken[k];
                if ((long)(t.x - x) * (t.x - x) + (long)(t.y - y) * (t.y - y) < (long)(t.r + r) * (t.r + r)) return false;
            }
            for (int k = 0; k < ways.Count; k++)
            {
                var t = ways[k];
                if (BoxNearWay(t.x0, t.y0, t.x1, t.y1, x, y, (long)(t.r + r) * (t.r + r))) return false;
            }
            return true;
        }

        /// <summary>Unit u's place at the box in a group too big for the range's edge. Its ideal point: where its straight way to the box's middle
        /// comes within its range - BoxPlaceInPx px of the box's near face (where it stands, when it is that near already). A place: between
        /// BoxPlaceMinPx and its range - BoxPlaceInPx px from the face, its room clear of every room taken (who stands near, the places given) and
        /// of the straight way of every unit placed before it (the units behind it), reached in a straight line past nobody who stands near and
        /// past no box. Of those the nearest to its ideal point (ties: the lower row, then the lower column): beside the places taken, or in
        /// front of them, whichever is nearer. False = none.</summary>
        bool BoxPlaceFor(ZObject u, ZObject box, bool blaster, List<(int x, int y, int r)> taken, List<(int x, int y, int r)> block, List<(int x0, int y0, int x1, int y1, int r)> ways, out int bx, out int by)
        {
            var w = W; bx = by = 0;
            bool robot = u.IsRobot, nr = NoRocks(u); int comp = Reach.UnitComp(u, nr);
            int range = blaster ? u.RangeTo(box) : u.AttackRadius > 0 ? u.AttackRadius : ZWorld.BoxWaitRangePx;
            int hi = range - ZWorld.BoxPlaceInPx, lo = ZWorld.BoxPlaceMinPx;
            if (comp < 0 || hi < lo) return false;
            int r = SlotRadius(u) + ZWorld.BoxPlaceGapPx;
            bool line = blaster && u.LosActive; int ex = line ? w.Sight.ExemptOf(box) : 0;
            int ix = u.CenterX, iy = u.CenterY;                                               // the ideal point: along the straight way to the box's middle, the first point within hi px of its face
            {
                int dx = box.CenterX - ix, dy = box.CenterY - iy, steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
                long hi2 = (long)hi * hi;
                for (int s = 0; s <= steps; s++)
                {
                    ix = u.CenterX + (steps == 0 ? 0 : dx * s / steps); iy = u.CenterY + (steps == 0 ? 0 : dy * s / steps);
                    if (ZWorld.BoxFaceD2(box, ix, iy) <= hi2) break;
                }
            }
            bool found = false; long best = long.MaxValue;
            int tx0 = Math.Max(0, ((box.LocX - hi) >> 4) - 2), ty0 = Math.Max(0, ((box.LocY - hi) >> 4) - 2);
            int tx1 = Math.Min(w.Map.width - 1, ((box.LocX + box.WidthPix + hi) >> 4) + 1), ty1 = Math.Min(w.Map.height - 1, ((box.LocY + box.HeightPix + hi) >> 4) + 1);
            for (int ty = ty0; ty <= ty1; ty++)
                for (int tx = tx0; tx <= tx1; tx++)
                {
                    var (x, y) = ZReach.FootCentre(tx, ty, robot);
                    long d2 = ZWorld.BoxFaceD2(box, x, y);
                    if (d2 > (long)hi * hi || d2 < (long)lo * lo) continue;
                    long cost = (long)(x - ix) * (x - ix) + (long)(y - iy) * (y - iy);
                    if (found && cost >= best) continue;                                      // (no nearer its ideal point than the best one)
                    if (!BoxSpotOk(u, box, tx, ty, x, y, robot, nr, comp, line, ex) || !BoxRoomFree(x, y, r, taken, ways)) continue;
                    if (!BoxWayFree(u.CenterX, u.CenterY, x, y, r, block)) continue;
                    found = true; best = cost; bx = x; by = y;
                }
            return found;
        }

        /// <summary>The unit's order is its own from here on: the group spread's keys of this order end (as with any new order).</summary>
        void BoxOwnOrder(ZObject u)
        {
            LiveOrderReset(u);
            u.PS.groupX = int.MinValue; u.PC.groupR = 0; u.PC.dvSpotWp = ZWaypoint.Cleared;
            foreach (var m in u.Minions) if (m != null) { m.PS.groupX = int.MinValue; m.PC.groupR = 0; m.PC.dvSpotWp = ZWaypoint.Cleared; }
        }

        /// <summary>The straight way from (x0, y0) to (x1, y1) passes no room of the list (its radius + r) that the start is not already inside.</summary>
        static bool BoxWayFree(int x0, int y0, int x1, int y1, int r, List<(int x, int y, int r)> rooms)
        {
            for (int k = 0; k < rooms.Count; k++)
            {
                var t = rooms[k];
                long rr = (long)(t.r + r) * (t.r + r), sx = t.x - x0, sy = t.y - y0;
                if (sx * sx + sy * sy < rr) continue;                                         // (it stands inside that room now: the way leads out of it)
                if (BoxNearWay(x0, y0, x1, y1, t.x, t.y, rr)) return false;
            }
            return true;
        }

        /// <summary>The point (px, py) lies nearer the segment (x0, y0) - (x1, y1) than the square root of rr. Whole numbers: the squared distance
        /// of the point to the segment against rr.</summary>
        static bool BoxNearWay(int x0, int y0, int x1, int y1, int px, int py, long rr)
        {
            long dx = x1 - x0, dy = y1 - y0, l2 = dx * dx + dy * dy, ax = px - x0, ay = py - y0, pa = ax * ax + ay * ay;
            long tn = ax * dx + ay * dy;
            if (tn <= 0 || l2 == 0) return pa < rr;
            if (tn >= l2) { long qx = px - x1, qy = py - y1; return qx * qx + qy * qy < rr; }
            return pa * l2 - tn * tn < rr * l2;
        }
    }

    public partial class ZObject
    {
        /// <summary>ProcessServer hook (a unit on no waypoint, a box order open in this world): a unit of a box group attacks the lowest standing box
        /// on the place it was ordered onto (Zod's own attack waypoint, the player's order); see ZWorld.UeBoxGroup.cs.</summary>
        internal void BoxOrderStand()
        {
            var b = World.BoxOrderNext(this);
            if (b == null) return;
            Waypoints.Add(new ZWaypoint { mode = WpMode.Attack, refId = b.Id, x = b.CenterX, y = b.CenterY, playerGiven = true });
            CloneMinionWayPoints();
        }
    }
}
