using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// 1.2 item 67 (the R key's trip that stopped in front of the station): three fixes in DV62's repair trip (ZObject.RepairBay.cs, ZObject.Buildings.cs).
    ///  * The waiting spot. DV62 waits 40 px out from the entrance (else at the entrance when its tiles are taken). A cannon nearer to that
    ///    spot than the movement layer lets the vehicle stand (ZPathing.MinSpacing + 1, the gate) kept every vehicle off it for good: the
    ///    Jungle's map gun beside the bay of its north station stands 33 px from the spot, every vehicle needs 33 to 39 px. The vehicle
    ///    pushed against the gun, the movement layer re-planned the same spot again and again, and the trip never reached its waiting
    ///    stage, so it never drove in. The computer's own repair rule (DV137, ZBot.CpuRepair.cs) already left such a station out; the
    ///    R key had no such test. Now the spot moves, only when a cannon keeps the vehicle off it, to the nearest point beside or behind
    ///    it (8 px steps, up to 32 px aside and 32 px further out, level with the doorway first) whose tiles are free and that no cannon
    ///    keeps the vehicle off (1 px clear of the gate). No such point: the old spot, as before. The drive in from there is the scripted
    ///    stage it always was (it passes units, the gun too).
    ///  * The way back. DV62 notes where the vehicle stood when its repair waypoint begins; the movement layer's re-path of a stuck
    ///    player order and a dodge in front of the waypoint start that waypoint again, so the note moved to wherever the vehicle stood
    ///    then (in front of the station: the vehicle came back to the doorstep). Now the note is taken once per trip: the same waypoint
    ///    taken up again keeps it; a new order (SEND_WAYPOINTS) starts a new trip.
    ///  * The wait. Several vehicles sent together queue for the bay on the same waiting spot; one driving off the pad and home, or one
    ///    squeezing by, pushes the waiting one off it. Then either the wait never ended (it ends only on the spot's own pixel, and the
    ///    stuck guard leaves a waiting vehicle alone: it stood there for good with the bay free, the next one behind it), or, when the
    ///    movement layer had made the pushed place its target, it drove in from 1 px beside the doorway line: a sideways step that small
    ///    is lost in the movement's 0.1 velocity hysteresis (zobject.cpp:3646-3647), so the vehicle never "reached" the pad and drove on
    ///    through the station and off the map. Seen at every station with one R press for several vehicles. Now, once the bay is free, a
    ///    vehicle that does not stand exactly on its waiting spot (worked out again as the trip's start does) takes up its repair
    ///    waypoint again (the path back to the spot; the way back stays, see above) and drives in from the spot itself. The human's
    ///    vehicles only (any side): the computer's trips stay as they were, so its matches are unchanged.
    /// The first two for both teams alike, any side the human plays. Deterministic: the object list order and fixed steps, no random draw. A trip whose spot
    /// no cannon blocks and whose waiting vehicle stays on its spot runs tick for tick as before.
    /// </summary>
    public partial class ZObject
    {
        /// <summary>k67 proof counter: waiting spots moved off a cannon (all trips).</summary>
        public static int RepairSpotMoves;
        /// <summary>k67 proof counter: waiting vehicles found off their waiting spot with the bay free, sent back to it first.</summary>
        public static int RepairWaitBack;

        /// <summary>k67, ZObject.Buildings.cs ProcessUnitRepairWP, Stage.Wait with the bay free (station t): a human team's vehicle that does
        /// not stand exactly on its waiting spot (the trip's own: the entrance, with DV62 RepairQueueSpot) takes up its repair waypoint again
        /// next tick (PathReissue, as the movement layer's re-issue): the path back to the spot, then the wait and the drive in from there.
        /// true = nothing more this tick.</summary>
        bool RepairWaitOffSpot(ZObject t)
        {
            if (World == null || !World.IsHumanTeam(Owner)) return false;                              // the computer's trips as they were (red-side matches stay the same)
            if (!t.GetRepairEntrance(out int sx, out int sy)) return false;
            if (ZWorld.RepairInPlaceDeviation) RepairQueueSpot(t, this, out sx, out sy);
            if (CenterX == sx && CenterY == sy) return false;
            RepairWaitBack++;
            PathReissue();
            return true;
        }

        /// <summary>k67: the repair waypoint whose trip noted the way back (ZObject.RepairBay.cs RepairTripStart), until a new order.</summary>
        bool repairTripLive; ZWaypoint repairTripWp;
        /// <summary>k67: the steps tried from a blocked waiting spot, nearest first (then level with the doorway, then the left one first).</summary>
        static readonly (int dx, int dy)[] RepairSpotSteps = BuildRepairSpotSteps();

        static (int dx, int dy)[] BuildRepairSpotSteps()
        {
            var l = new System.Collections.Generic.List<(int dx, int dy)>();
            for (int dy = 0; dy <= 32; dy += 8)
                for (int dx = -32; dx <= 32; dx += 8)
                    if (dx != 0 || dy != 0) l.Add((dx, dy));
            l.Sort((a, b) =>
            {
                int c = (a.dx * a.dx + a.dy * a.dy).CompareTo(b.dx * b.dx + b.dy * b.dy);
                if (c != 0) return c;
                c = a.dy.CompareTo(b.dy);
                return c != 0 ? c : a.dx.CompareTo(b.dx);
            });
            return l.ToArray();
        }

        /// <summary>k67: a cannon (standing, not leaving the game) nearer to (x, y) than u may stand to it: the movement gate's spacing
        /// (ZPathing.MinSpacing + 1) plus <paramref name="margin"/> px.</summary>
        public static bool RepairSpotBlocked(ZObject u, int x, int y, int margin = 0)
        {
            if (u == null || u.World == null) return false;
            foreach (var c in u.World.Objects)
            {
                if (c == u || c.Kind != ZKind.Cannon || c.Destroyed || c.KillAt >= 0) continue;
                long dx = c.CenterX - x, dy = c.CenterY - y, m = ZPathing.MinSpacing(u, c) + 1 + margin;
                if (dx * dx + dy * dy < m * m) return true;
            }
            return false;
        }

        /// <summary>k67: the waiting spot (x, y) of DV62, moved to the nearest free point when a cannon keeps u off it.</summary>
        static void RepairSpotClear(ZObject u, ref int x, ref int y)
        {
            if (!RepairSpotBlocked(u, x, y)) return;
            foreach (var (dx, dy) in RepairSpotSteps)
            {
                int cx = x + dx, cy = y + dy;
                if (u.World.Path.WithinImpassable(cx - (u.WidthPix >> 1) + 1, cy - (u.HeightPix >> 1) + 1, u.WidthPix - 2, u.HeightPix - 2, out _, out _, false)) continue;
                if (RepairSpotBlocked(u, cx, cy, 1)) continue;
                x = cx; y = cy; RepairSpotMoves++;
                return;
            }
        }
    }
}
