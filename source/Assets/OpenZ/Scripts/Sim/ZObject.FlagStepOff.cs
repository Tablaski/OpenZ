using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// Zod: a vehicle that takes a flag (CheckFlagCaptures, the first enemy box that touches it) simply ends its order there and parks on top
    /// of the flag for good. OpenZ (switch on): a VEHICLE (never a robot, a gun or the shuttle) of any team, the CPU's too, that captured a flag
    /// and then stands still on it with no further order rolls ONCE a short way off it, to the nearest free spot right beside the flag:
    /// <list type="bullet">
    /// <item>when: FlagOffSettleTicks (0.5 s) after it came to rest, while it is still idle (no waypoint, no target), still overlaps the flag
    /// and the flag is still its team's; at most FlagOffWaitTicks (15 s) after the capture (a long fight on the flag: it stays);</item>
    /// <item>where: the vehicle box moved in one of 8 directions just far enough to clear the flag box by FlagOffGapPx (4 px), never more than
    /// FlagOffMaxPx (40 px) per axis (about 1.6 tiles at most, mostly 18-26 px); cost = the squared move x a weight by the angle to its last
    /// heading (ahead 2, within 45 degrees 3, sideways 5, back-sideways 8, straight back 12: it rolls on rather than turning round, no dance);
    /// ties go by the fixed direction order E, SE, S, SW, W, NW, N, NE;</item>
    /// <item>free: the box at the spot and half way there clear of the vehicle map grid (Zod's own WithinImpassable: water, rock, cliff, lava,
    /// buildings, map edge), in the same vehicle region, and 2 px clear of every other object except bridges and loose map items (units of
    /// both teams, guns, buildings, other flags, rocks, wrecks, props); every unit at least the pathing gate's spacing from the spot (and no
    /// nearer half way than now), no unit's MOVE goal within it, the spot outside the lanes' keep-clear areas (review 9 Nov: the roll never
    /// shoves a parked unit or parks in a bridge's way);</item>
    /// <item>how: one MOVE of the pathing layer's own step-aside kind (ZPathing.RefStepAside: not player-given, no attack-move), so the path,
    /// traffic and arrival rules are the same as every other make-way move; any new order replaces it at once.</item>
    /// </list>
    /// It stays (overlap is fine) when: no such free spot; or the capture came with any other order: queued waypoints after the flag, an
    /// attack or any non-move order, a move to a point past the flag (more than FlagOffNearPx from its centre), a patrol, a hold position, a
    /// group move (the same order given to another unit of its side in the same tick: one click on several units, a CPU squad order; or the
    /// pathing layer gave it its own spot of a group order), or the player's (or a CPU's) explicit STOP while it still stands
    /// where it was stopped; or any order (SEND_WAYPOINTS) after the capture.
    /// Deterministic: integers only (pixels and the world's tick index), no random draw, the world's object order (FlagStepOffTick runs once a
    /// tick right after CheckFlagCaptures); no float or double is read or computed here (the heading is the integer pixel vector of its own
    /// last moves, not the view's facing).
    /// Switch: ZWorld.FlagStepOffDeviation (View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; held off in the parity's
    /// Zod rows and every counted check that does not set it itself). Off = Zod: nothing here runs (the order hook only notes the stop spot).
    /// The flag never blocks movement in the rules (checked for this kit: no flag is SetImpassable, IsDestroyableImpass stays false; flags only
    /// count as occupied for a shuttle's landing spot, the Unit editor's placement and the platform / exit-lane spot tests).
    /// </summary>
    public partial class ZObject
    {
        /// <summary>DV184: rest before the roll (0.5 s), the longest wait after the capture (15 s), the gap kept to the flag box, the longest move
        /// per axis, how near the flag a MOVE's goal must be to count as the capture order, how far from its STOP spot a stop still counts.</summary>
        public const int FlagOffSettleTicks = 50, FlagOffWaitTicks = 1500, FlagOffGapPx = 4, FlagOffMaxPx = 40, FlagOffNearPx = 24, FlagOffStopPx = 8;

        int flagOffId = -1, flagOffRx, flagOffRy, flagOffHx, flagOffHy;
        long flagOffAt, flagOffIdle = -1;
        bool flagOffStop; int flagOffStopX, flagOffStopY;
        long flagOrderTick = -1; int flagOrderX, flagOrderY, flagOrderRef;                              // the last order: its tick and first goal (a group = the same order to several units in one tick)

        /// <summary>Checks: the flag this vehicle waits to step off (-1 none); why its last capture ended as it did; where its step-off goes
        /// (centre px; int.MinValue none); the tick it began; the integer heading it used.</summary>
        public int FlagStepOffFlagId => flagOffId;
        public string FlagStepOffWhy { get; private set; }
        public int FlagStepOffGoalX { get; private set; } = int.MinValue;
        public int FlagStepOffGoalY { get; private set; } = int.MinValue;
        public long FlagStepOffTick { get; private set; } = -1;
        public int FlagStepOffHeadX => flagOffHx;
        public int FlagStepOffHeadY => flagOffHy;

        /// <summary>ZWorld.SendWaypoints (every player and CPU order): any order ends a pending step-off; an empty list (STOP, HOLD) notes
        /// the spot (a capture there afterwards stays).</summary>
        internal void FlagStepOffOrdered(bool stop)
        {
            flagOffId = -1; flagOffIdle = -1;
            flagOffStop = stop;
            if (stop) { flagOffStopX = CenterX; flagOffStopY = CenterY; }
            flagOrderTick = World != null ? World.TickIndex : -1;
            if (Waypoints.Count > 0) { flagOrderX = Waypoints[0].x; flagOrderY = Waypoints[0].y; flagOrderRef = Waypoints[0].refId; } else { flagOrderX = flagOrderY = int.MinValue; flagOrderRef = -1; }
        }

        /// <summary>CheckFlagCaptures, right after AwardZone(f, team, this): a vehicle that took flag f now.</summary>
        internal void FlagStepOffNote(ZObject f, long tick)
        {
            if (Kind != ZKind.Vehicle || IsShuttle || !CanMove || Destroyed || f == null) return;
            flagOffId = -1; flagOffIdle = -1;
            string why = FlagStepOffOrderWhy(f);
            if (why != null) { FlagStepOffStay("stays: " + why); return; }
            flagOffId = f.Id; flagOffAt = tick; flagOffRx = CenterX; flagOffRy = CenterY; flagOffHx = flagOffHy = 0;
            if (Waypoints.Count > 0)
            {
                int hx = Waypoints[0].x - CenterX, hy = Waypoints[0].y - CenterY;
                if (Math.Abs(hx) + Math.Abs(hy) >= 4) { flagOffHx = hx; flagOffHy = hy; }
            }
            FlagStepOffWhy = "captured: waits to stand still";
        }

        /// <summary>Null when the capture came with no further order; else the order that keeps it on the flag.</summary>
        string FlagStepOffOrderWhy(ZObject f)
        {
            if (HoldPosition) return "hold position";
            if (Patrolling) return "patrol";
            if (flagOffStop && Math.Abs(CenterX - flagOffStopX) + Math.Abs(CenterY - flagOffStopY) <= FlagOffStopPx) return "stopped here (STOP order)";
            int gx = CenterX, gy = CenterY;
            for (int i = 0; i < Waypoints.Count; i++)                                                      // every waypoint left must be the drive onto this flag (a copy of it too: DV84's grab on the way)
            {
                var wp = Waypoints[i];
                bool move = wp.mode == WpMode.Move || wp.mode == WpMode.ForceMove;
                bool helper = move && wp.refId < -1 && !wp.playerGiven;                                     // the pathing layer's own make-way / regroup move (no order): judged when it stands
                bool onto = move && (helper || wp.refId == f.Id || (Math.Abs(wp.x - f.CenterX) <= FlagOffNearPx && Math.Abs(wp.y - f.CenterY) <= FlagOffNearPx));
                if (onto) { gx = wp.x; gy = wp.y; continue; }
                if (i > 0) return "queued waypoints (" + FlagOffWps() + ")";
                return move ? "a move past the flag" : "another order (" + wp.mode + ")";
            }
            if (PS.groupX != int.MinValue && Math.Abs(PS.groupSlotX - gx) <= 4 && Math.Abs(PS.groupSlotY - gy) <= 4) return "a group move";
            if (flagOrderTick >= 0 && flagOrderX != int.MinValue && World != null)                        // the same order given to other units of its side in the same tick (one click, a CPU squad order)
                foreach (var o in World.Objects)
                    if (o != this && o.Owner == Owner && o.flagOrderTick == flagOrderTick && o.flagOrderX == flagOrderX && o.flagOrderY == flagOrderY && o.flagOrderRef == flagOrderRef && !o.Destroyed)
                        return "a group move (with " + o.Type + "#" + o.Id + ")";
            return null;
        }

        string FlagOffWps()
        {
            var s = new System.Text.StringBuilder();
            foreach (var w in Waypoints) s.Append(s.Length > 0 ? " " : "").Append(w.mode).Append('@').Append(w.x).Append(',').Append(w.y).Append('#').Append(w.refId);
            return s.ToString();
        }

        void FlagStepOffStay(string why) { FlagStepOffWhy = why; ZWorld.FlagStepOffStays++; }

        /// <summary>ZWorld.FlagStepOffTick, once a tick for a vehicle with a pending step-off.</summary>
        internal void FlagStepOffStep(ZWorld w)
        {
            long now = w.TickIndex;
            var f = w.Get(flagOffId);
            if (f == null || f.Destroyed || Destroyed || KillAt >= 0 || Owner == Team.Neutral || f.Owner != Owner) { flagOffId = -1; FlagStepOffStay("stays: the flag or the vehicle changed"); return; }
            if (now - flagOffAt > FlagOffWaitTicks) { flagOffId = -1; FlagStepOffStay("stays: never stood still on it in time"); return; }
            if (HoldPosition || Patrolling) { flagOffId = -1; FlagStepOffStay("stays: hold / patrol"); return; }
            if (Math.Abs(CenterX - flagOffRx) + Math.Abs(CenterY - flagOffRy) >= 6)                     // its own last heading: the pixel vector of its last moves
            { flagOffHx = CenterX - flagOffRx; flagOffHy = CenterY - flagOffRy; flagOffRx = CenterX; flagOffRy = CenterY; }
            if (Waypoints.Count > 0 || AttackObject != null) { flagOffIdle = -1; return; }              // still finishing the capture drive, or fighting: wait (review 9 Nov: no IsMoving here, Zod's velocity hysteresis can leave it true on an idle unit for good: ZPathing.Moving; it is a double test too)
            if (flagOffIdle < 0) { flagOffIdle = now; return; }
            if (now - flagOffIdle < FlagOffSettleTicks) return;
            flagOffId = -1;                                                                                 // decided once
            if (!IntersectsObject(f)) { FlagStepOffStay("stays: not on the flag"); return; }
            if (!FlagStepOffSpot(w, f, out int nx, out int ny)) { FlagStepOffStay("stays: no free space beside the flag"); return; }
            int gx = nx + (WidthPix >> 1), gy = ny + (HeightPix >> 1);
            Waypoints.Add(ZWaypoint.Move(gx, gy, false, ZPathing.RefStepAside));
            UpdatedWaypoints = true;
            FlagStepOffGoalX = gx; FlagStepOffGoalY = gy; FlagStepOffTick = now;
            FlagStepOffWhy = "rolls off to (" + gx + "," + gy + ")";
            ZWorld.FlagStepOffs++;
        }

        static readonly int[] FlagOffDx = { 1, 1, 0, -1, -1, -1, 0, 1 }, FlagOffDy = { 0, 1, 1, 1, 0, -1, -1, -1 };

        /// <summary>The nearest free spot (top-left px) beside flag f, by the cost in the summary.</summary>
        bool FlagStepOffSpot(ZWorld w, ZObject f, out int bx, out int by)
        {
            bx = by = 0;
            int right = f.LocX + f.WidthPix + FlagOffGapPx - LocX, left = LocX + WidthPix + FlagOffGapPx - f.LocX;
            int down = f.LocY + f.HeightPix + FlagOffGapPx - LocY, up = LocY + HeightPix + FlagOffGapPx - f.LocY;
            long best = long.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                int ux = FlagOffDx[i], uy = FlagOffDy[i], sx, sy;
                if (uy == 0) { sx = ux > 0 ? right : -left; sy = 0; }
                else if (ux == 0) { sx = 0; sy = uy > 0 ? down : -up; }
                else { int k = Math.Min(ux > 0 ? right : left, uy > 0 ? down : up); sx = ux * k; sy = uy * k; }
                if ((sx == 0 && sy == 0) || Math.Abs(sx) > FlagOffMaxPx || Math.Abs(sy) > FlagOffMaxPx) continue;
                int nx = LocX + sx, ny = LocY + sy;
                if (FlagOffBoxHits(nx, ny, f.LocX, f.LocY, f.WidthPix, f.HeightPix, 1)) continue;             // (never: the 4 px gap clears it)
                long cost = ((long)sx * sx + (long)sy * sy) * FlagOffWeight(ux, uy);
                if (cost >= best) continue;
                if (!FlagStepOffFree(w, f, nx, ny, true) || !FlagStepOffFree(w, f, LocX + sx / 2, LocY + sy / 2, false)) continue;
                if (!w.Path.InSameRegion(CenterX, CenterY, nx + (WidthPix >> 1), ny + (HeightPix >> 1), false)) continue;
                best = cost; bx = nx; by = ny;
            }
            return best != long.MaxValue;
        }

        /// <summary>The angle weight of direction (ux, uy) against the integer heading (ahead 2 ... straight back 12; no heading: 2).</summary>
        long FlagOffWeight(int ux, int uy)
        {
            long hx = flagOffHx, hy = flagOffHy;
            if (hx == 0 && hy == 0) return 2;
            long dot = ux * hx + uy * hy, uu = ux * ux + uy * uy, hh = hx * hx + hy * hy;
            long d2 = dot * dot * 1000, n = uu * hh;
            if (dot > 0) return d2 >= 854 * n ? 2 : d2 >= 500 * n ? 3 : 5;                                    // within 22.5 / 45 / 90 degrees (cos^2 0.854, 0.5)
            if (dot == 0) return 5;
            return d2 < 500 * n ? 8 : 12;                                                                      // 90-135 / 135-180 degrees
        }

        bool FlagOffBoxHits(int x, int y, int ox, int oy, int ow, int oh, int margin) =>
            x - margin < ox + ow && ox < x + WidthPix + margin && y - margin < oy + oh && oy < y + HeightPix + margin;

        /// <summary>The vehicle box at (x, y): clear of the vehicle grid (as ProcessMove tests it), and 2 px clear of every other object but
        /// the flag it leaves, bridges and loose map items; and (review 9 Nov) the pathing layer's own room kept: every other unit at least
        /// the movement gate's spacing (ZPathing.MinSpacing + 1) from the spot (at the half way point: no nearer than it is now), no other
        /// unit's MOVE goal within that spacing of the spot, and the spot outside every lane's keep-clear area (bridges, narrow passes). So
        /// the roll never shoves a parked unit aside or ends held short on the flag (the probe without this: a parked heavy tank pushed 7 px,
        /// one beside an APC 50 px, a mobile missile held short for 25 s).</summary>
        bool FlagStepOffFree(ZWorld w, ZObject f, int x, int y, bool end)
        {
            if (w.Path.WithinImpassable(x + 1, y + 1, WidthPix - 2, HeightPix - 2, out _, out _, false)) return false;
            int cx = x + (WidthPix >> 1), cy = y + (HeightPix >> 1);
            if (end && w.Pathing != null && w.Pathing.FlagOffKeepClear(cx, cy)) return false;
            foreach (var o in w.Objects)
            {
                if (o == this || o == f || o.Destroyed || o.KillAt >= 0 || o.IsBridge) continue;
                if (o.Kind == ZKind.MapItem && o.Type != "flag" && !o.IsDestroyableImpass) continue;
                if (x - 2 < o.LocX + o.WidthPix && o.LocX < x + WidthPix + 2 && y - 2 < o.LocY + o.HeightPix && o.LocY < y + HeightPix + 2) return false;
                if (!ZPathing.Separable(o)) continue;
                long sp = ZPathing.MinSpacing(this, o) + 1, sp2 = sp * sp;
                long dx = o.CenterX - cx, dy = o.CenterY - cy, d2 = dx * dx + dy * dy;
                long nx = o.CenterX - CenterX, ny = o.CenterY - CenterY;
                if (d2 < sp2 && (end || d2 < nx * nx + ny * ny)) return false;
                if (end && o.Waypoints.Count > 0)
                {
                    var g = o.Waypoints[0];
                    if (g.mode == WpMode.Move || g.mode == WpMode.ForceMove)
                    {
                        long gx = g.x - cx, gy = g.y - cy;
                        if (gx * gx + gy * gy < sp2) return false;
                    }
                }
            }
            return true;
        }
    }

    public sealed partial class ZPathing
    {
        /// <summary>DV184: the point lies in a lane's keep-clear area (Sim/ZLanes.cs KeepClearAt: lane zone + 3 tiles along it + 1 tile each
        /// side, where group spots, step-aside and regroup spots never go); false while the pathing layer is off.</summary>
        internal bool FlagOffKeepClear(int x, int y) => Enabled && KeepClearAt(x, y) != null;
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV184 switch: a vehicle steps off the flag it captured (Sim/ZObject.FlagStepOff.cs). View/ZGame.cs: on at every start, off in
        /// 'Zod exact'. Headless default off (Zod: it parks on the flag).</summary>
        public static bool FlagStepOffDeviation = false;
        /// <summary>Checks: step-offs started, captures that stayed (both teams, every world of this process).</summary>
        public static int FlagStepOffs, FlagStepOffStays;

        /// <summary>CheckFlagCaptures hook: the conqueror of flag f (a vehicle may step off it later).</summary>
        void FlagStepOffNote(ZObject f, ZObject o)
        {
            if (!FlagStepOffDeviation || o == null) return;
            o.FlagStepOffNote(f, TickIndex);
        }

        /// <summary>Once a tick, after CheckFlagCaptures: every vehicle with a pending step-off, in the object order.</summary>
        void FlagStepOffTick()
        {
            if (!FlagStepOffDeviation) return;
            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.FlagStepOffFlagId >= 0) o.FlagStepOffStep(this);
            }
        }
    }
}
