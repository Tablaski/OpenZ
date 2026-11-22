using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ 1.5 deviation DV202 "the computer drives its big tank" (kit l18_cpu_bigtank_r2, owner addition 5 again, 13 Nov: "Computer's Mammoth:
    /// it takes the empty Mammoth late, then barely fights with it"). Level 18 has an empty MAMMOTH and an empty BIZON in its start set (DV198);
    /// Z has no big tank.
    /// <para>MEASURED before (kit proof/bots_before_P.txt, P's code, computer against computer, 300 s): the computer's robots take the far Mammoth at
    /// 88 s, then Zod's planner (ZBot.MatchTargets_3 / GiveOutOrders_3) gives it one or two orders in 212 s: each round orders a random 15 to 35 %
    /// (+1) of the idle units and a unit only gets the target it is the NEAREST unit of; a slow tank standing far top-left is nobody's nearest, so
    /// it stood 100 to 190 s with no order, and an ENTER FORT order (which never fires on the way) drove it into the enemy's guns with 0 shots.</para>
    /// <para>DV202: the computer's own Mammoth / Bizon (DV199's <see cref="BotBigTank"/>, the bot not standing in for a human player) is driven by
    /// this round instead of Zod's planner (<see cref="BigTankDriveHeld"/> takes it out of CollectOurUnits_3, so Zod's rounds, DV56's idle waves
    /// and DV72 never see it). Every <see cref="BigTankDriveEverySec"/> s, for each such tank in object order:</para>
    /// <list type="bullet">
    /// <item>LEFT ALONE while its repair trip runs (DV137 / DV199: only when damaged, as before), while it is stunned, a strike group's or a
    /// frontier guard's, while it fights (its own pick, DV189's ram-first chase, DV182 / DV183 ram / crush) and while the pathing layer holds it
    /// in a queue (a bridge lane, a choke): then its still clock restarts.</item>
    /// <item>A NEW ORDER when it has none, when its target is gone (killed, a flag now its own), or when it has not got
    /// <see cref="BigTankDriveStallPx"/> px away from where it stood for <see cref="BigTankDriveStallSec"/> s on its order (rocking on one spot
    /// is no progress; that goal is then left out for <see cref="BigTankDriveSkipSec"/> s: the next one is taken). Every
    /// <see cref="BigTankDriveLookSec"/> s on a march it also looks round: an enemy within <see cref="BigTankDriveEngageTiles"/> tiles that it can
    /// reach turns the march into an attack, and a far attack target gives way to such a near enemy.</item>
    /// <item>THE PICK, first that exists: (1) the NEAREST enemy robot, vehicle or gun within <see cref="BigTankDriveEngageTiles"/> tiles that it
    /// can drive into its gun range (ATTACK: its own rules shoot, and ram / crush every vehicle DV189 picks); (1b) an enemy within
    /// <see cref="BigTankDriveDefendTiles"/> tiles of its OWN fort while the tank is within <see cref="BigTankDriveHomeTiles"/> tiles of it (it
    /// turns back to defend: measured, a Mammoth marching for flags let its fort fall without one shot); (2) a not-own flag within
    /// <see cref="BigTankDriveNearFlagTiles"/> tiles by ROAD, the one with the smallest "2 x road to the flag + flag to the enemy fort" (MOVE with
    /// attack-to: it fires on the way and takes the zone); (3) the enemy with the shortest road (ATTACK: the fight is where the enemy is);
    /// (4) the best other flag; (5) the enemy fort: within <see cref="BigTankDriveFortTiles"/> tiles of it ENTER FORT, else an attack-move to the
    /// nearest spot it can stand on by the fort; else no order (nothing reachable: it waits). Never its own fort, never a repair station
    /// (DV137 sends it there when it is damaged). ROAD = the tank's own driving way on its grid as it is now (ZWorld.StrikeWayField, the field the
    /// strike groups march on, whole tiles): measured, map distance alone sent a Mammoth round the empty north for 150 s between far flags.</item>
    /// </list>
    /// <para>"Can drive to" is DV199's exact reachability on the tank's own grid as it is now (<see cref="BigTankReaches"/>: bridges as they stand,
    /// the Bizon wades), so it is never sent over a bridge that is down. The order goes through the computer's own command path
    /// (ZWorld.SendWaypoints, attack-to, player-given), as every order of the computer. EASY: the same round from the Easy computer's start delay on
    /// (its tank is no attack wave of its cap: one tank).</para>
    /// <para>Switch <see cref="ZWorld.BigTankDriveDeviation"/>: View/ZGame.cs on at every start (off in 'Zod exact'); headless default off; the parity
    /// report and the counted checks hold it off (with DV199, <see cref="ZWorld.BotBigTanksOff"/>). Off: not one line runs, Zod's planner orders
    /// the big tanks as before. Only a computer's big tank is touched, so a level without one (levels 2, 7, 16, 17) plays tick for tick as before.
    /// Deterministic: whole numbers in every choice (squared or tile distances, ties by object order), the game clock, no random draw.</para>
    /// </summary>
    public sealed partial class ZBot
    {
        /// <summary>DV202: the round's period, the look-round period on a march, the stall limit (s / px), how long a stalled goal is left out.</summary>
        public const double BigTankDriveEverySec = 1.0, BigTankDriveLookSec = 3.0, BigTankDriveStallSec = 10.0, BigTankDriveSkipSec = 45.0;   // 10 s: a wading Bizon (DV192 pace 0.6) needs about 8 s for two tiles
        public const int BigTankDriveStallPx = 32;   // two tiles from where it stood: rocking on one spot (measured at a bridge head) is no progress
        /// <summary>DV202: an enemy this near (tiles, centre to centre) is attacked first; the enemy fort this near is entered.</summary>
        public const int BigTankDriveEngageTiles = 14, BigTankDriveFortTiles = 7;
        /// <summary>DV202: a not-own flag this near by road (tiles) is taken before the nearest enemy is sought.</summary>
        public const int BigTankDriveNearFlagTiles = 30;
        /// <summary>DV202: an enemy this near its own fort (tiles) is a threat it turns back for, while the tank is this near home (tiles).</summary>
        public const int BigTankDriveDefendTiles = 16, BigTankDriveHomeTiles = 45;

        /// <summary>DV202 statistics (checks): orders the round gave, stalled goals left out.</summary>
        public int BigTankDriveOrders { get; private set; }
        public int BigTankDriveStalls { get; private set; }

        sealed class BigTankDrive { public int x, y, goal = int.MinValue; public double still, look, owned = -1; }
        readonly Dictionary<int, BigTankDrive> btDrive = new Dictionary<int, BigTankDrive>();
        readonly Dictionary<long, double> btSkip = new Dictionary<long, double>();
        double btNext;

        /// <summary>DV202: the computer's own big tank, driven by <see cref="BigTankDriveTick"/> (Zod's planner leaves it out).</summary>
        public bool BigTankDriveHeld(ZObject u) => ZWorld.BigTankDriveDeviation && !Ignored && BotBigTank(u) && u.Owner == Team && !u.IsShuttle;

        /// <summary>DV202: the round, every server tick from ZBot.Tick (it runs once a second).</summary>
        void BigTankDriveTick()
        {
            if (!ZWorld.BigTankDriveDeviation || Ignored || W.GameOver) return;
            if (W.Time + TIME_EPS < btNext) return;
            btNext = W.Time + BigTankDriveEverySec;
            if (Difficulty == ZBotDifficulty.Easy && W.Time + TIME_EPS < EASY_START_DELAY) return;   // Easy: the CPU sits still at the start (as Stage1AI)
            foreach (var u in W.Objects)
            {
                if (!BigTankDriveHeld(u) || Gone(u) || u.Destroyed) continue;
                if (!btDrive.TryGetValue(u.Id, out var s)) { s = new BigTankDrive { x = u.CenterX, y = u.CenterY, still = W.Time, look = W.Time }; btDrive[u.Id] = s; }
                if (s.owned < 0) s.owned = W.Time;
                int mx = u.CenterX - s.x, my = u.CenterY - s.y;
                if (mx * mx + my * my > BigTankDriveStallPx * BigTankDriveStallPx) { s.x = u.CenterX; s.y = u.CenterY; s.still = W.Time; }
                bool queued = u.PathWait != ZPathWait.None && u.PathWait != ZPathWait.Arrived;
                bool fighting = (u.AttackObject != null && !Gone(u.AttackObject) && !u.AttackObject.Destroyed) || u.Ramming;
                if (u.CpuRepairHeld || u.StrikeHeld || u.CpuDefendHeld || u.Stunned || !u.CanMove || queued || fighting)
                { s.x = u.CenterX; s.y = u.CenterY; s.still = W.Time; continue; }
                var wps = KnownWaypoints(u);
                bool done = wps.Count == 0;
                if (!done && wps[0].refId >= 0)
                {
                    var t = BotObject(wps[0].refId);
                    if (t == null || Gone(t) || (t.Destroyed && t.Kind != ZKind.Building) || (t.Kind == ZKind.MapItem && t.Type == "flag" && W.AreAllied(Team, t.Owner))) done = true;
                    else if (t.Kind == ZKind.Building && t.Type == "repair") continue;                     // DV137's trip (held above while it runs)
                }
                if (!done && W.Time + TIME_EPS >= s.still + BigTankDriveStallSec)
                {   // on its order but not moving (no queue, no fight): leave that goal out for a while, take the next
                    if (s.goal != int.MinValue) btSkip[Key(u, s.goal)] = W.Time + BigTankDriveSkipSec;
                    BigTankDriveStalls++; done = true;
                    if (Verbose) Log.Add($"{W.Time:F0}s {Team} DV202 {u.Type}#{u.Id} stood {BigTankDriveStallSec:0} s at ({u.CenterX / 16},{u.CenterY / 16}) on its order: goal left out");
                }
                if (!done)
                {
                    if (W.Time + TIME_EPS < s.look + BigTankDriveLookSec) continue;
                    s.look = W.Time;
                    bool onUnit = wps[0].mode == WpMode.Attack;
                    if (onUnit)
                    {   // on its way to an enemy: a FAR target gives way to an enemy within the engage ring (measured: a Bizon sent at a fort gun
                        //  40 tiles off stood by the bridge head and let a near Electrobot kill it without one shot)
                        var tgt = BotObject(wps[0].refId);
                        if (tgt == null || Tiles(u.CenterX, u.CenterY, tgt.CenterX, tgt.CenterY) <= BigTankDriveEngageTiles) continue;
                        var close = NearestEnemy(u, BigTankDriveEngageTiles);
                        if (close != null && close != tgt) BigTankOrder(u, s, close, WpMode.Attack, close.LocX + 8, close.LocY + 8, close.Id);
                        continue;
                    }
                    var near = NearestEnemy(u, BigTankDriveEngageTiles) ?? FortThreat(u);
                    if (near != null) BigTankOrder(u, s, near, WpMode.Attack, near.LocX + 8, near.LocY + 8, near.Id);
                    continue;
                }
                s.look = W.Time;
                BigTankPick(u, s);
            }
        }

        long Key(ZObject u, int goal) => ((long)u.Id << 32) ^ (uint)goal;
        bool Skipped(ZObject u, int goal) => btSkip.TryGetValue(Key(u, goal), out double until) && W.Time + TIME_EPS < until;

        /// <summary>DV202: the pick (1) enemy near, (1b) a threat to its own fort, (2) a near flag, (3) the nearest enemy by road, (4) any other
        /// flag, (5) the enemy fort. Ways are the tank's own driving way on its grid as it is now (<see cref="ZWorld.StrikeWayField"/>, the field
        /// the strike groups march on), in whole tiles: measured, map distance alone sent a Mammoth round the empty north for 150 s between far
        /// flags (the river and the broken bridges make the road two or three times the straight line).</summary>
        void BigTankPick(ZObject u, BigTankDrive s)
        {
            var near = NearestEnemy(u, BigTankDriveEngageTiles) ?? FortThreat(u);
            if (near != null) { BigTankOrder(u, s, near, WpMode.Attack, near.LocX + 8, near.LocY + 8, near.Id); return; }
            double[] way;
            using (W.Path.Kind(W.GridKindOf(u))) way = W.StrikeWayField(u.CenterX, u.CenterY, false);
            ZObject fort = null;
            foreach (var o in W.Objects) if (o.IsFort && !W.AreAllied(Team, o.Owner) && o.Owner != Team.Neutral && !o.Destroyed) { fort = o; break; }
            // the flags: the smallest 2 x road-from-the-tank + flag-to-enemy-fort (tiles), ties by object order: the near zones first, of two alike
            //     the one toward the enemy
            var cands = new List<(int score, int road, int id, ZObject f)>();
            foreach (var f in W.Flags)
            {
                if (W.AreAllied(Team, f.Owner) || Gone(f) || Skipped(u, f.Id)) continue;
                int a = WayTiles(way, f.LocX + 8, f.LocY + 8, BigTankReachTiles);
                if (a == int.MaxValue) continue;
                int b = fort != null ? Tiles(f.LocX + 8, f.LocY + 8, fort.CenterX, fort.CenterY) : 0;
                cands.Add((2 * a + b, a, f.Id, f));
            }
            cands.Sort((p, q) => p.score != q.score ? p.score.CompareTo(q.score) : p.id.CompareTo(q.id));
            ZObject flag = null; int flagRoad = int.MaxValue;
            foreach (var c in cands)
                if (BigTankReaches(u, c.f.LocX + 8, c.f.LocY + 8, BigTankReachTiles, false)) { flag = c.f; flagRoad = c.road; break; }
            // (2) a near flag
            if (flag != null && flagRoad <= BigTankDriveNearFlagTiles) { BigTankOrder(u, s, flag, WpMode.Move, flag.CenterX, flag.CenterY, flag.Id); return; }
            // (3) the nearest enemy by road (the fight is where the enemy is)
            var any = NearestEnemyByRoad(u, way);
            if (any != null) { BigTankOrder(u, s, any, WpMode.Attack, any.LocX + 8, any.LocY + 8, any.Id); return; }
            // (4) any other flag
            if (flag != null) { BigTankOrder(u, s, flag, WpMode.Move, flag.CenterX, flag.CenterY, flag.Id); return; }
            // (5) the enemy fort
            if (fort != null && !Skipped(u, fort.Id))
            {
                int half = Math.Max(fort.WidthPix, fort.HeightPix) / 32;
                if (BigTankSpot(u, fort.CenterX, fort.CenterY, half + BigTankReachTiles, out int fx, out int fy))
                {
                    if (Tiles(u.CenterX, u.CenterY, fort.CenterX, fort.CenterY) <= half + BigTankDriveFortTiles && fort.CanEnterFort(Team))
                        BigTankOrder(u, s, fort, WpMode.EnterFort, fort.CenterX, fort.CenterY, fort.Id);
                    else BigTankOrder(u, s, null, WpMode.Move, fx, fy, fort.Id);
                }
            }
        }

        /// <summary>DV202: the road (whole tiles) from the tank to the point (px), the least over the tiles within `ring` of it; int.MaxValue = no road.</summary>
        int WayTiles(double[] way, int x, int y, int ring)
        {
            if (way == null) return int.MaxValue;
            int w = W.Path.W, h = W.Path.H;
            var (tx0, ty0) = ZReach.FootTileAt(x, y, false);
            double best = double.MaxValue;
            for (int ty = ty0 - ring; ty <= ty0 + ring; ty++)
                for (int tx = tx0 - ring; tx <= tx0 + ring; tx++)
                    if (tx >= 0 && ty >= 0 && tx < w && ty < h && way[ty * w + tx] < best) best = way[ty * w + tx];
            return best == double.MaxValue ? int.MaxValue : (int)(best / 16.0);
        }

        /// <summary>DV202: the enemy robot / vehicle / gun with the shortest road from the tank to within its gun range (ties by object order), that
        /// it can drive into range of now; null = none.</summary>
        ZObject NearestEnemyByRoad(ZObject u, double[] way)
        {
            int range = Math.Max(BigTankReachTiles, u.AttackRadius / 16), ring = Math.Min(range, 6);
            ZObject best = null; int bestRoad = int.MaxValue;
            foreach (var o in W.Objects)
            {
                if (o.Kind != ZKind.Robot && o.Kind != ZKind.Vehicle && o.Kind != ZKind.Cannon) continue;
                if (W.AreAllied(Team, o.Owner) || o.Owner == Team.Neutral || Gone(o) || o.Destroyed || !VisibleToBot(o) || Skipped(u, o.Id)) continue;
                if (o.ShuttleAirborne || !u.CanAttackObject(o)) continue;
                int r = WayTiles(way, o.LocX + 8, o.LocY + 8, ring);
                if (r < bestRoad || (r == bestRoad && best != null && o.Id < best.Id)) { best = o; bestRoad = r; }
            }
            return bestRoad == int.MaxValue ? null : best;
        }

        /// <summary>Whole tiles, Chebyshev, between two pixel points.</summary>
        static int Tiles(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by)) >> 4;

        /// <summary>DV202: the nearest enemy robot, vehicle or gun (squared centre distance, ties by object order) within `tiles` tiles (0 = any
        /// distance) that the tank can drive into its gun range now, and may attack.</summary>
        ZObject NearestEnemy(ZObject u, int tiles) => NearestEnemyTo(u, u.CenterX, u.CenterY, tiles);

        /// <summary>DV202: an enemy robot / vehicle / gun within <see cref="BigTankDriveDefendTiles"/> of the computer's own fort, the nearest to the
        /// fort that the tank can drive into range of, while the tank is within <see cref="BigTankDriveHomeTiles"/> of that fort; else null.</summary>
        ZObject FortThreat(ZObject u)
        {
            ZObject home = null;
            foreach (var o in W.Objects) if (o.IsFort && o.Owner == Team && !o.Destroyed) { home = o; break; }
            if (home == null || Tiles(u.CenterX, u.CenterY, home.CenterX, home.CenterY) > BigTankDriveHomeTiles) return null;
            return NearestEnemyTo(u, home.CenterX, home.CenterY, BigTankDriveDefendTiles);
        }

        /// <summary>DV202: the nearest enemy to the point (cx, cy) within `tiles` of it (0 = any distance), as <see cref="NearestEnemy"/>.</summary>
        ZObject NearestEnemyTo(ZObject u, int cx, int cy, int tiles)
        {
            var cands = new List<(long d, int id, ZObject o)>();
            long lim = tiles > 0 ? (long)tiles * 16 * tiles * 16 : long.MaxValue;
            foreach (var o in W.Objects)
            {
                if (o.Kind != ZKind.Robot && o.Kind != ZKind.Vehicle && o.Kind != ZKind.Cannon) continue;
                if (W.AreAllied(Team, o.Owner) || o.Owner == Team.Neutral || Gone(o) || o.Destroyed || !VisibleToBot(o) || Skipped(u, o.Id)) continue;
                if (o.ShuttleAirborne || !u.CanAttackObject(o)) continue;
                long dx = o.CenterX - cx, dy = o.CenterY - cy, d = dx * dx + dy * dy;
                if (d > lim) continue;
                cands.Add((d, o.Id, o));
            }
            if (cands.Count == 0) return null;
            cands.Sort((p, q) => p.d != q.d ? p.d.CompareTo(q.d) : p.id.CompareTo(q.id));
            int range = Math.Max(BigTankReachTiles, u.AttackRadius / 16);
            int tries = 0;
            foreach (var c in cands)
            {
                if (++tries > 12) break;                                                                      // the 12 nearest: enough, and bounded
                if (BigTankReaches(u, c.o.LocX + 8, c.o.LocY + 8, range, false)) return c.o;
            }
            return null;
        }

        /// <summary>DV202: the nearest spot (px) in the tank's own reachability component within `tiles` of (x, y).</summary>
        bool BigTankSpot(ZObject u, int x, int y, int tiles, out int rx, out int ry)
        {
            rx = x; ry = y;
            var reach = W.Pathing?.Reach;
            if (reach == null) return false;
            using (W.Path.Kind(W.GridKindOf(u)))
            {
                int c = reach.UnitComp(u, false);
                if (c < 0) return false;
                return reach.Nearest(x, y, false, false, c, out rx, out ry, tiles);
            }
        }

        /// <summary>DV202: one waypoint replacing the tank's orders, through the computer's own command path (Command's waypoint, attack-to).</summary>
        void BigTankOrder(ZObject u, BigTankDrive s, ZObject t, WpMode mode, int x, int y, int goal)
        {
            var wp = new ZWaypoint { mode = mode, refId = t != null ? t.Id : -1, x = x, y = y, attackTo = true, playerGiven = true };
            s.goal = goal; s.x = u.CenterX; s.y = u.CenterY; s.still = W.Time;
            if (!W.SendWaypoints(Team, u, new[] { wp })) { RejectedOrders++; btSkip[Key(u, goal)] = W.Time + BigTankDriveSkipSec; return; }
            if (ClientView) Relay(u);
            if (u.Waypoints.Count == 0) { RejectedOrders++; btSkip[Key(u, goal)] = W.Time + BigTankDriveSkipSec; return; }
            Orders++; BigTankDriveOrders++;
            OrdersByMode[mode] = OrdersByMode.TryGetValue(mode, out int n) ? n + 1 : 1;
            if (t != null) LastRoundOrders.Add(new BotOrder { unitId = u.Id, targetId = t.Id, mode = mode, time = W.Time });
            if (Verbose) Log.Add($"{W.Time:F0}s {Team} {u.Type}#{u.Id} -> {mode} {(t != null ? t.Type + "#" + t.Id : "point#" + goal)} DV202");
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV202 switch (off = Zod's planner orders a computer's big tank like any vehicle).</summary>
        public static bool BigTankDriveDeviation = false;
    }
}
