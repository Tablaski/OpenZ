using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// game by walking into the enemy fort). In Zod a fort falls only to an ENTER_FORT order (zobject.cpp:2774-2913): a robot left
    /// standing right in front of the enemy gate stands there for ever. With DV168 a HUMAN player's robot that stands near an enemy
    /// fort's gate without an order of the player gets the very order the player's right-click on the gate gives (ENTER_FORT,
    /// through ZWorld.SendWaypoints like the player's input) and runs in: DV34 / DV102 do the run, the fort falls as with any
    /// ordered entry.
    /// <list type="bullet">
    /// <item><b>Who</b>: a squad leader (its squad follows, Zod's squad copy) of a team with no CPU (ZWorld.IsHumanTeam) that can move,
    /// is not stunned, is not a fort's roof defender, not EvilZod on guard, not walking out of a carrier, not on a patrol or on HOLD
    /// POSITION. Robots only: vehicles keep Zod's orders. The computer's own fort storm (DV60, ZBot.FinishGame.cs) is untouched: a CPU
    /// team's robot is never sent by this rule, and a bot against bot match has no human team, so the rule returns at once.</item>
    /// <item><b>When</b>: idle, or its last order just ended: no waypoint in its list was given by the player (an order of the player always
    /// wins, it is never replaced), and none is an ENTER_FORT already. Its own automatic steps (a chase of a target near it, a
    /// sidestep, a grab) are not orders of the player: the run in replaces them, as the player's click would.</item>
    /// <item><b>Where</b>: its centre within <see cref="FortAutoRushPx"/> px (4 tiles) of the gate's exit point (GetBuildingCreationMovePoint,
    /// the point every ENTER_FORT walks to first: 4 tiles in front of a front fort's gate mouth, 1 tile in front of a back fort's), of a
    /// living fort it may enter (ZObject.CanEnterFort); the nearest such fort when two are close.</item>
    /// <item><b>The way in is free</b>: Zod's own straight walk test for a robot (ZPathFinder.DirectPathPossible, rocks closed) from its
    /// centre to the exit point and from the exit point through the gate to the creation point inside. A wall, a cliff, water or a
    /// closed gate tile in between: no order (counted as blocked).</item>
    /// <item><b>The gate is clear</b> (the rule never sends robots to their death): no living enemy robot, vehicle or ground gun within
    /// <see cref="FortAutoRushClearPx"/> px (6 tiles) of that gate's exit point. Not counted: the fort's roof defender, a gun on one of the
    /// fort's tower spots (the fort's own guns, like its big roof gun, which is no object), a unit still inside a building (inside a fort's
    /// walls, rows 0-8, or a factory's or repair station's footprint), a crane at work (a CRANE_REPAIR order). Counted on level 2: blue's
    /// grunts that stand by its gate are 41 px from the gate point and its gate gun 72 px; the next ones 122 px. A defended gate: no order
    /// (counted as defended); the robots go on fighting where they stand, and are sent at the next check once the gate is clear.</item>
    /// <item><b>Pace</b>: the check runs every <see cref="FortAutoRushEveryTicks"/> ticks (0.1 s); a robot it sent (or tried to send) is
    /// left alone for <see cref="FortAutoRushRetryTicks"/> ticks (3 s), so an order that ends without getting in is not sent again
    /// every 0.1 s.</item>
    /// </list>
    /// Deterministic: sim ticks, integer px, object order, no random draw, no float. Called at the start of every tick (ZWorld.Tick),
    /// where a player's order from the input lands too. Switch <see cref="FortAutoRushDeviation"/>: off in the rule file (headless
    /// worlds, the parity's Zod rows and every older counted check keep the rules as they were); View/ZGame.cs turns it on at every
    /// start unless 'Zod exact'.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV168 the run into the enemy fort from near its gate (off in the rule file; View/ZGame.cs turns it on at every start
        /// unless 'Zod exact').</summary>
        public static bool FortAutoRushDeviation = false;
        /// <summary>DV168: px from the gate's exit point within which an idle robot of a human team runs in (4 tiles).</summary>
        public const int FortAutoRushPx = 64;
        /// <summary>DV168: px from the gate's exit point within which a living enemy robot, vehicle or ground gun keeps the robots out (6 tiles).</summary>
        public const int FortAutoRushClearPx = 96;
        /// <summary>DV168: the check runs every this many ticks (0.1 s).</summary>
        public const int FortAutoRushEveryTicks = 10;
        /// <summary>DV168: ticks a robot it sent (or tried to send) is left alone (3 s).</summary>
        public const int FortAutoRushRetryTicks = 300;

        /// <summary>DV168 statistics (checks, Play log): ENTER_FORT orders the rule gave, per team (index = (int)Team).</summary>
        public readonly int[] FortAutoRushOrdersOf = new int[4];
        /// <summary>DV168 statistics: every order the rule gave.</summary>
        public int FortAutoRushOrders { get; private set; }
        /// <summary>DV168 statistics: robots in reach of a gate whose way in was closed, per check.</summary>
        public int FortAutoRushBlocked { get; private set; }
        /// <summary>DV168 statistics: robots in reach of a gate that was defended (an enemy within FortAutoRushClearPx of it), per check.</summary>
        public int FortAutoRushDefended { get; private set; }
        /// <summary>DV168 statistics: orders the world refused (none expected: every condition of SEND_WAYPOINTS is checked first).</summary>
        public int FortAutoRushRefused { get; private set; }
        /// <summary>DV168 statistics: the tick of the last order the rule gave (-1 = none).</summary>
        public long FortAutoRushLastTick { get; private set; } = -1;

        /// <summary>DV168, for the view's log only (no rule reads it): a robot was sent (world, the robot, the fort, its distance to the
        /// gate's exit point in px).</summary>
        public static event Action<ZWorld, ZObject, ZObject, int> FortAutoRushSent;

        readonly List<ZObject> fortAutoRushForts = new List<ZObject>();
        readonly List<(ZObject u, ZObject f, int d)> fortAutoRushSend = new List<(ZObject, ZObject, int)>();

        /// <summary>DV168, at the start of every tick (after DV165's last defense, before the objects run).</summary>
        void FortAutoRushTick()
        {
            if (!FortAutoRushDeviation || GameOver || TickIndex % FortAutoRushEveryTicks != 0) return;
            if (!IsHumanTeam(Team.Red) && !IsHumanTeam(Team.Blue)) return;                          // a computer match: nothing to do
            fortAutoRushForts.Clear();
            for (int i = 0; i < Objects.Count; i++) { var f = Objects[i]; if (f.IsFort && !f.Destroyed && f.Owner != Team.Neutral) fortAutoRushForts.Add(f); }
            if (fortAutoRushForts.Count == 0) return;
            fortAutoRushSend.Clear();
            for (int i = 0; i < Objects.Count; i++)
            {
                var u = Objects[i];
                if (!FortAutoRushIdle(u)) continue;
                var f = FortAutoRushNearest(u, out int d2);
                if (f == null) continue;
                if (!FortAutoRushWayIn(u, f)) { FortAutoRushBlocked++; continue; }
                if (FortAutoRushDefender(u.Owner, f) != null) { FortAutoRushDefended++; continue; }
                fortAutoRushSend.Add((u, f, ISqrt(d2)));
            }
            foreach (var (u, f, d) in fortAutoRushSend)
            {
                u.FortAutoRushRetry = TickIndex + FortAutoRushRetryTicks;
                f.GetCreationMovePoint(out int ex, out int ey);
                var wp = new ZWaypoint { mode = WpMode.EnterFort, refId = f.Id, x = ex, y = ey, attackTo = false };   // the player's gate click (View/PlayerInput.cs BuildWaypoint)
                bool ok = SendWaypoints(u.Owner, u, new[] { wp }) && u.Waypoints.Count > 0 && u.Waypoints[0].mode == WpMode.EnterFort && u.Waypoints[0].refId == f.Id;
                if (!ok) { FortAutoRushRefused++; continue; }
                FortAutoRushOrders++; FortAutoRushOrdersOf[(int)u.Owner & 3]++; FortAutoRushLastTick = TickIndex;
                FortAutoRushSent?.Invoke(this, u, f, d);
            }
        }

        /// <summary>DV168: a squad leader of a human team, standing without an order of the player.</summary>
        bool FortAutoRushIdle(ZObject u)
        {
            if (u.Kind != ZKind.Robot || u.IsMinion || u.Destroyed || u.KillAt >= 0 || u.Owner == Team.Neutral) return false;
            if (!u.CanMove || u.Stunned || u.IsFortDefender || u.EvilGuarding || u.Patrolling || u.HoldPosition || u.ExitWalking(Time)) return false;
            if (u.FortAutoRushRetry > TickIndex || !IsHumanTeam(u.Owner)) return false;
            foreach (var w in u.Waypoints) if (w.playerGiven || w.mode == WpMode.EnterFort) return false;   // the player's order always wins
            return true;
        }

        /// <summary>DV168: the nearest living fort u may enter whose gate exit point is within FortAutoRushPx of u's centre (object order on a tie).</summary>
        ZObject FortAutoRushNearest(ZObject u, out int d2)
        {
            ZObject best = null; long bd = (long)FortAutoRushPx * FortAutoRushPx + 1;
            foreach (var f in fortAutoRushForts)
            {
                if (!f.CanEnterFort(u.Owner) || !f.GetCreationMovePoint(out int ex, out int ey)) continue;
                long dx = u.CenterX - ex, dy = u.CenterY - ey, dd = dx * dx + dy * dy;
                if (dd < bd) { bd = dd; best = f; }
            }
            d2 = best == null ? 0 : (int)bd;
            return best;
        }

        /// <summary>DV168: Zod's straight robot walk (rocks closed) is free from u's centre to the gate's exit point and from there through the
        /// gate to the creation point inside.</summary>
        bool FortAutoRushWayIn(ZObject u, ZObject f)
        {
            if (!f.GetCreationMovePoint(out int ex, out int ey) || !f.GetCreationPoint(out int cx, out int cy)) return false;
            return Path.DirectPathPossible(u.CenterX, u.CenterY, ex, ey, true, false) && Path.DirectPathPossible(ex, ey, cx, cy, true, false);
        }

        /// <summary>DV168: the first living enemy (of `team`) robot, vehicle or ground gun within FortAutoRushClearPx of fort f's gate exit point, in
        /// object order (null = the gate is clear). Not counted: a roof defender, a gun on a fort's tower spot, a unit inside a building, a crane at work.</summary>
        public ZObject FortAutoRushDefender(Team team, ZObject f)
        {
            if (f == null || !f.GetCreationMovePoint(out int ex, out int ey)) return null;
            const long r2 = (long)FortAutoRushClearPx * FortAutoRushClearPx;
            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (!o.IsUnit || o.Owner == team || o.Owner == Team.Neutral || o.Destroyed || o.KillAt >= 0) continue;
                long dx = o.CenterX - ex, dy = o.CenterY - ey;
                if (dx * dx + dy * dy > r2) continue;
                if (o.IsFortDefender || o.IsTowerGun) continue;                                       // the fort's own defences on its roof and towers
                if (o.Type == "crane" && o.Waypoints.Count > 0 && o.Waypoints[0].mode == WpMode.CraneRepair) continue;   // a crane at work
                if (FortAutoRushInBuilding(o)) continue;
                return o;
            }
            return null;
        }

        /// <summary>DV168: o's centre is inside a standing building: a fort's walls (its rows 0-8; the apron in front of a front fort's gate is outside),
        /// a factory's or repair station's footprint. Bridges are no buildings here.</summary>
        bool FortAutoRushInBuilding(ZObject o)
        {
            for (int i = 0; i < Objects.Count; i++)
            {
                var b = Objects[i];
                if (b.Kind != ZKind.Building || b.Destroyed || b.IsBridge) continue;
                int h = b.IsFort ? 9 * 16 : b.HeightPix;
                if (o.CenterX >= b.LocX && o.CenterX < b.LocX + b.WidthPix && o.CenterY >= b.LocY && o.CenterY < b.LocY + h) return true;
            }
            return false;
        }

        /// <summary>DV168: the point (x, y) is within FortAutoRushPx of fort f's gate exit point (the checks, the view's gate zone).</summary>
        public static bool FortAutoRushInReach(ZObject f, int x, int y)
        {
            if (f == null || !f.GetCreationMovePoint(out int ex, out int ey)) return false;
            long dx = x - ex, dy = y - ey;
            return dx * dx + dy * dy <= (long)FortAutoRushPx * FortAutoRushPx;
        }

        static int ISqrt(int v) { int r = 0; while ((long)(r + 1) * (r + 1) <= v) r++; return r; }

        /// <summary>DV168 off for a while (the parity's other probes measure without it; a Play session leaves the switch on).</summary>
        public static IDisposable FortAutoRushOff() => new FortAutoRushHold();
        sealed class FortAutoRushHold : IDisposable
        {
            readonly bool was = FortAutoRushDeviation;
            public FortAutoRushHold() { FortAutoRushDeviation = false; }
            public void Dispose() { FortAutoRushDeviation = was; }
        }
    }

    public partial class ZObject
    {
        /// <summary>DV168: the tick before which the fort run from near the gate leaves this robot alone (Sim/ZWorld.FortAutoRush.cs).</summary>
        internal long FortAutoRushRetry;
    }
}
