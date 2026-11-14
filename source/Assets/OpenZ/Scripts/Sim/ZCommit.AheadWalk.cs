using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// Version 1.3 item 29, rule DV166 "a squad member ahead of its leader walks on" (a player: units answered the order and did not
    /// move; the counted check Editor/OzV13GroupOrderCount.cs still found squad members standing while their leader walked).
    /// <para>The cause. A new order often finds a squad member level with or in front of its leader on his new way (the previous order went
    /// the other way, or the member was left beside him). The squad follow (DV44, ZCommit.Follow.cs FollowTarget) lets such a member walk on
    /// only beside the leader's line with a free step ahead; otherwise it stands until the leader has walked up to it and past it, then it
    /// falls in behind. Measured on L02, L16 and L17 (200 seeded group orders each, Editor/OzV13GroupOrderCount.cs): 21 of the 28 members
    /// counted "silent" stood in that branch 72 to 147 px (4.5 to 9 tiles) from their leader: on his line (|lateral| under the 13 px spacing),
    /// with a wall or rock right in front of them, or while he waited in traffic far behind them. Staged (one member 100 px in front, every
    /// robot type): it first stepped after 2.7 to 3.2 s and was 16 px nearer the goal after 4.7 to 5.6 s; its leader after 0.15 s and 1.1 to
    /// 1.4 s. The other 7 waited in traffic or let the leader pass close by (named reasons now, ZObject.OrderWatch.cs).</para>
    /// <para>The rule. Such a member farther than AheadReach px from its leader, or beside his line with a wall or rock right in front of it
    /// (the follow's walk on beside him cannot start, StepBlocked), leaves the follow for its own path to the order's goal, the
    /// path every minion walks in Zod (CloneMinionWayPoints gives each minion the leader's waypoint, each path-finds to it). It is the
    /// follow's existing exit (FollowStep: own Zod path through the DV44 path budget, ZFormation.ReenterDelay before it may re-join), so it
    /// walks at its own Zod speed and turns like any robot on its path. It re-joins the follow by the usual rules once it is within
    /// AheadReach - AheadHysteresis px of its leader or behind him. Within AheadReach and with a free step nothing changes: the follow's
    /// side-step out of the leader's line, the walk on beside it and the short wait while he passes stay as they are.</para>
    /// Both teams' squads. Sim only: fixed tick, positions and object order, no random number, no clock. Switch off (headless default,
    /// 'Zod exact', every counted check that does not set it): FollowTarget returns exactly as before.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV166 switch (on in the game, View/ZGame.cs; off in 'Zod exact' and in every headless check that does not set it itself).</summary>
        public static bool AheadWalkDeviation = false;
        /// <summary>Statistics (checks, all worlds): squad members DV166 sent on their own path.</summary>
        public static int AheadWalks;
    }

    public sealed partial class ZPathing
    {
        /// <summary>DV166: a member level with / ahead of its leader leaves the follow beyond this distance (px; the reach of the follow's own
        /// side-step out of the leader's line, ClearLeaderLine), and keeps its own path until it is within AheadReach - AheadHysteresis.</summary>
        public const double AheadReach = 64, AheadHysteresis = 16;

        /// <summary>DV166, FollowTarget's "level with / ahead of the leader" branch where the follow would stand the member: true = leave the
        /// follow for its own path (FollowTarget returns false, FollowStep's exit). False = stand as before.</summary>
        bool AheadWalk(ZObject m, ZObject l, double mx, double my)
        {
            if (!ZWorld.AheadWalkDeviation || m.Waypoints.Count == 0) return false;
            var mc = m.PC;
            double reach = mc.ownPath && mc.ownWp.Equals(m.Waypoints[0]) ? AheadReach - AheadHysteresis : AheadReach;   // (hysteresis: no follow / own path flicker at the edge)
            double dx = mx - CX(l), dy = my - CY(l), d2 = dx * dx + dy * dy;
            bool far = d2 > reach * reach;
            if (!far && !StepBlocked(m, l, mx, my)) return false;
            if (m.InFormation || mc.noPath) ZWorld.AheadWalks++;                         // (counted when it really leaves the follow; on its own path already: no count)
            mc.why = far ? $"{Math.Sqrt(d2):0} px ahead of its leader: own path (DV166)" : "beside its leader, a wall or rock in front of it: own path (DV166)";
            return true;
        }

        /// <summary>DV166: the leader walks, the member stands beside his line (clear of it), but its step on beside him (FollowTarget's walk on,
        /// AimAhead px along his heading) runs into a wall or rock. The same test as that walk on; in his line it is the follow's own side-step.</summary>
        bool StepBlocked(ZObject m, ZObject l, double mx, double my)
        {
            double lvx = l.Dx, lvy = l.Dy, lvl = Math.Sqrt(lvx * lvx + lvy * lvy);
            if (!Walking(l) || lvl <= 1e-6 || l.PC.vF <= GoSpeed) return false;
            double ux = lvx / lvl, uy = lvy / lvl, lat = (mx - CX(l)) * -uy + (my - CY(l)) * ux;
            if (Math.Abs(lat) < MinSpacing(l, m) + 1) return false;
            return !LineFreeBetween(m, mx, my, mx + ux * AimAhead, my + uy * AimAhead, NoRocks(m));
        }
    }
}
