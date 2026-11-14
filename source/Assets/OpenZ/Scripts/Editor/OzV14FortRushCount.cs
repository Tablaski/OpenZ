using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.4, finishing the enemy fort, rule DV168 (Sim/ZWorld.FortAutoRush.cs): the counted check, headless on the Sim alone, on the real level
    /// maps, nothing saved: menu OpenZ/V1.4/Fort: Auto Rush Count (headless, DV168), and off-project on plain .NET (the kit's tools/harness).
    /// One line per fact, [OpenZ V1.4 fort rush] PASS n/n. The check sets the switches as View/ZGame.cs does at a level start (DV34, DV102 and
    /// DV168 on) on top of the counted checks' usual game switches; the computer side is a CPU that gives no orders (ZBot.Ignored), so the
    /// other side counts as the player's.
    ///  G  the reach's numbers (4 tiles round the gate's exit point, both fort types);
    ///  A  a quiet fort (the start units off the map, no fort gun, no roof defender): an idle squad of the player 3 tiles out runs in at once and
    ///     the fort falls; the edge of the reach to the side; one tile past the reach, nothing; a move order of the player (away, and one that ends
    ///     at the gate); an attack order of the player that ends at the gate; HOLD POSITION; a closed gate; a CPU squad at the player's gate;
    ///     PLAY AS BLUE at the red fort; the switch off ('Zod exact'); a computer match; a vehicle;
    ///  R  the real level 2 blue fort with its guns, its fort gun, its roof defender and its own units standing: a squad 3 tiles out gets in;
    ///  L  levels 2, 7, 16 and 17: an idle squad 3 tiles out of the blue fort (red player) and of the red fort (blue player) gets in;
    ///  D  the same scene twice = the same ticks and events.
    /// </summary>
    public static class OzV14FortRushCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);

        /// <summary>An idle squad 3 tiles out gets in within this many seconds (the walk to the gate point, the run through the gate).</summary>
        public const double EnterSeconds = 10;
        /// <summary>The scenes that must give no order watch this long.</summary>
        public const double QuietSeconds = 20;

        sealed class Scene
        {
            public ZWorld w; public ZObject fort, keeper; public Team human, cpu;
            public int ex, ey, cx, cy, outY;
            public long enteredTick = -1; public int enteredBy = -1; public ulong hash = 1469598103934665603UL;
            public readonly List<long> orderTicks = new List<long>();
            public double Seconds(long ticks) => ticks * ZConst.TICK;
            /// <summary>Runs ticks (stop: after each tick, true = stop now). Records the rule's orders, the fort entry and an event hash.</summary>
            public void Run(double seconds, Func<bool> stop = null)
            {
                int n = (int)Math.Round(seconds / ZConst.TICK), seen = w.FortAutoRushOrders;
                for (int i = 0; i < n; i++)
                {
                    w.Tick(ZConst.TICK);
                    if (w.FortAutoRushOrders != seen) { seen = w.FortAutoRushOrders; orderTicks.Add(w.TickIndex); }
                    foreach (var e in w.Events)
                    {
                        hash = (hash ^ (ulong)(long)e.type) * 1099511628211UL; hash = (hash ^ (ulong)(long)e.objId) * 1099511628211UL;
                        if (e.type == ZEventType.FortEntered && e.otherId == fort.Id && enteredTick < 0) { enteredTick = w.TickIndex; enteredBy = e.objId; }
                    }
                    w.Events.Clear();
                    if (stop != null && stop()) break;
                }
            }
            /// <summary>Runs until the fort is entered (at most `seconds`).</summary>
            public void RunToEntry(double seconds) => Run(seconds, () => enteredTick >= 0);
            /// <summary>A squad of the given team whose leader stands `outPx` px out from the gate's exit point and `sidePx` px to its side.</summary>
            public ZObject Squad(string type, Team team, int outPx, int sidePx = 0, int amount = -1) => OzV11Kit.Place(w, type, ex + sidePx, ey + outY * outPx, team, amount);
            public int DistToGate(ZObject u) => (int)Math.Round(Math.Sqrt((double)(u.CenterX - ex) * (u.CenterX - ex) + (double)(u.CenterY - ey) * (u.CenterY - ey)));
            public bool Fell => fort.Destroyed;
            public string Entry => enteredTick < 0 ? "not entered" : $"entered by #{enteredBy} {Seconds(enteredTick - (orderTicks.Count > 0 ? orderTicks[0] : 0)):0.00} s after the order";
        }

        /// <summary>A world on a level map: `cpu` is a CPU that gives no orders; quiet = every unit of both teams off the map, no fort gun, no roof
        /// defender; a CPU robot that cannot move, fire or die stands at the player's own fort (the game goes on); the fort is the CPU's.</summary>
        static Scene New(string set, string map, Team human, bool quiet, int seed = 1, bool bothCpu = false)
        {
            var w = ZWorld.Create(set, map, seed);
            var cpu = human == Team.Red ? Team.Blue : Team.Red;
            w.Bots.Add(new ZBot(w, cpu, seed + 101) { Ignored = true });
            if (bothCpu) w.Bots.Add(new ZBot(w, human, seed) { Ignored = true });
            if (quiet) foreach (var o in w.Objects) if (o.IsUnit && o.Owner != Team.Neutral) o.KillAt = 0;   // off the map at the first tick
            var s = new Scene { w = w, human = human, cpu = cpu };
            s.fort = w.Objects.First(o => o.IsFort && o.Owner == cpu);
            s.fort.GetCreationMovePoint(out s.ex, out s.ey); s.fort.GetCreationPoint(out s.cx, out s.cy);
            s.outY = s.ey > s.cy ? 1 : -1;
            var own = w.Objects.First(o => o.IsFort && o.Owner == human);
            own.GetCreationMovePoint(out int kx, out int ky); own.GetCreationPoint(out _, out int kcy);
            s.keeper = w.CreateRobotGroup("grunt", kx - 8, ky + (ky > kcy ? 96 : -96) - 8, cpu, 1);
            s.keeper.MoveSpeed = 0; s.keeper.RealMoveSpeed = 0; s.keeper.Damage = 0; s.keeper.MaxHealth = s.keeper.Health = 1000000000;
            w.Events.Clear();
            return s;
        }

        /// <summary>Switches as View/ZGame.cs sets them at a level start for this rule's world (on top of OzV11Kit.GameSwitches), quiet or not.</summary>
        static IDisposable Game(bool quiet) => new Hold(quiet);
        sealed class Hold : IDisposable
        {
            readonly bool rush = ZWorld.FortAutoRushDeviation, run = ZWorld.FortRunDeviation, rushNb = ZWorld.FortRushDeviation, gun = ZWorld.FortGunDeviation, def = ZWorld.FortDefenderDeviation;
            public Hold(bool quiet)
            {
                ZWorld.FortAutoRushDeviation = true; ZWorld.FortRunDeviation = true; ZWorld.FortRushDeviation = true;
                if (quiet) { ZWorld.FortGunDeviation = false; ZWorld.FortDefenderDeviation = false; }
            }
            public void Dispose()
            {
                ZWorld.FortAutoRushDeviation = rush; ZWorld.FortRunDeviation = run; ZWorld.FortRushDeviation = rushNb; ZWorld.FortGunDeviation = gun; ZWorld.FortDefenderDeviation = def;
            }
        }

        static string Who(ZWorld w, int id) { var o = w.Get(id); return o == null ? "#" + id : $"{o.Type} #{id} ({o.Owner})"; }

        // ------------------------------------------------------------------------------------------------ G: the reach's numbers
        static void G(string set, Func<string, string> map)
        {
            var w = ZWorld.Create(set, map("L02"), 1);
            var front = w.Objects.First(o => o.Type == "fort_front"); var back = w.Objects.First(o => o.Type == "fort_back");
            front.GetCreationMovePoint(out int fx, out int fy); back.GetCreationMovePoint(out int bx, out int by);
            bool ok = ZWorld.FortAutoRushPx == 64 && fx == front.LocX + 80 && fy == front.LocY + 208 && bx == back.LocX + 80 && by == back.LocY - 16;
            Check(ok, $"G1 the reach is 4 tiles ({ZWorld.FortAutoRushPx} px) round Zod's gate point: a front fort's 13 tiles below its top-left ({fx - front.LocX}, {fy - front.LocY}) px, 4 tiles out of its gate mouth; a back fort's 1 tile above ({bx - back.LocX}, {by - back.LocY}) px");
            bool edges = true;
            foreach (var (f, x, y, o) in new[] { (front, fx, fy, 1), (back, bx, by, -1) })
                edges &= ZWorld.FortAutoRushInReach(f, x, y + o * 64) && !ZWorld.FortAutoRushInReach(f, x, y + o * 65) && ZWorld.FortAutoRushInReach(f, x + 64, y) && !ZWorld.FortAutoRushInReach(f, x - 65, y)
                         && ZWorld.FortAutoRushInReach(f, x + 45, y + o * 45) && !ZWorld.FortAutoRushInReach(f, x + 46, y + o * 46);
            Check(edges, "G2 the edges in whole px, both forts: straight out 64 in / 65 out, to the side 64 in / 65 out, on the diagonal 45,45 in / 46,46 out");
            Check(ZWorld.FortAutoRushEveryTicks == 10 && ZWorld.FortAutoRushRetryTicks == 300, "G3 the check every 10 ticks (0.1 s), a robot it sent is left alone 300 ticks (3 s)");
        }

        // ------------------------------------------------------------------------------------------------ A: a quiet fort
        static void A(string set, Func<string, string> map)
        {
            string l02 = map("L02");
            using (Game(true))
            {
                {   // A1 an idle squad 3 tiles out
                    var s = New(set, l02, Team.Red, true); var q = s.Squad("grunt", Team.Red, 48);
                    s.Run(0.02); int d0 = s.DistToGate(q);
                    s.RunToEntry(EnterSeconds + 1); s.Run(2.5);
                    long first = s.orderTicks.Count > 0 ? s.orderTicks[0] : -1;
                    Check(first > 0 && first <= 10 && s.w.FortAutoRushOrdersOf[(int)Team.Red] >= 1 && s.w.FortAutoRushRefused == 0,
                          $"A1 a red grunt squad of {1 + q.Minions.Count} idle {d0} px ({d0 / 16.0:0.0} tiles) from the blue fort's gate point: the rule's order at tick {first} ({s.Seconds(first):0.00} s), the player's own gate order (ENTER_FORT)");
                    Check(s.enteredTick > 0 && s.Seconds(s.enteredTick - first) <= EnterSeconds && s.Fell && s.w.GameOver && s.w.Winner == Team.Red && s.w.Get(s.enteredBy)?.Owner == Team.Red,
                          $"A1 it ran in and the fort fell: {s.Entry} (bar {EnterSeconds:0} s), the game over, winner {s.w.Winner}");
                }
                {   // A2 the edge of the reach, to the side along the apron
                    var s = New(set, l02, Team.Red, true); var q = s.Squad("grunt", Team.Red, 0, 56);
                    s.Run(0.02); int d0 = s.DistToGate(q);
                    s.RunToEntry(EnterSeconds + 1);
                    Check(s.orderTicks.Count > 0 && s.enteredTick > 0 && s.Fell, $"A2 a squad {d0} px to the side of the gate point (the edge of the reach is 64): ordered at {s.Seconds(s.orderTicks.FirstOrDefault()):0.00} s, {s.Entry}");
                }
                {   // A3 one tile past the reach: straight out and to the side
                    var s = New(set, l02, Team.Red, true); var q = s.Squad("grunt", Team.Red, 80); var r = s.Squad("grunt", Team.Red, 0, -80);
                    s.Run(0.02); int d0 = s.DistToGate(q), d1 = s.DistToGate(r), near = Math.Min(d0, d1);
                    s.Run(QuietSeconds, () => { near = Math.Min(near, Math.Min(s.DistToGate(q), s.DistToGate(r))); return false; });
                    Check(s.orderTicks.Count == 0 && !s.Fell && near > ZWorld.FortAutoRushPx,
                          $"A3 one tile past the reach, {d0} px straight out and {d1} px to the side: no order in {QuietSeconds:0} s (nearest the leaders came {near} px), the fort stands");
                }
                {   // A4 a move order of the player away from the gate
                    var s = New(set, l02, Team.Red, true); var q = s.Squad("grunt", Team.Red, 32);
                    OzV11Kit.PlayerMove(s.w, q, s.ex, s.ey + s.outY * 12 * 16);
                    s.Run(QuietSeconds);
                    Check(s.orderTicks.Count == 0 && !s.Fell && s.DistToGate(q) > ZWorld.FortAutoRushPx, $"A4 a squad 2 tiles out given a move 12 tiles away: no order of the rule while it walks off or after, it stands {s.DistToGate(q) / 16.0:0.0} tiles out");
                }
                {   // A5 a move order of the player that ends at the gate
                    var s = New(set, l02, Team.Red, true); var q = s.Squad("grunt", Team.Red, 12 * 16);
                    OzV11Kit.PlayerMove(s.w, q, s.ex, s.ey + s.outY * 40);
                    long moveEnd = -1; bool orderWhileMoving = false;
                    s.Run(QuietSeconds, () =>
                    {
                        bool moving = q.Waypoints.Count > 0 && q.Waypoints[0].mode == WpMode.Move && q.Waypoints[0].playerGiven;
                        if (moving && s.orderTicks.Count > 0) orderWhileMoving = true;
                        if (!moving && moveEnd < 0) moveEnd = s.w.TickIndex;
                        return s.enteredTick >= 0;
                    });
                    long first = s.orderTicks.Count > 0 ? s.orderTicks[0] : -1;
                    Check(!orderWhileMoving && moveEnd > 0 && first >= moveEnd && first - moveEnd <= 20 && s.Fell,
                          $"A5 a squad 12 tiles out given a move to 2.5 tiles out: no order while the move is on ({s.Seconds(moveEnd):0.00} s), the rule's order {first - moveEnd} ticks after it ended, {s.Entry}");
                }
                {   // A6 an attack order of the player that ends at the gate
                    var s = New(set, l02, Team.Red, true); var q = s.Squad("grunt", Team.Red, 12 * 16);
                    var t = OzV11Kit.Place(s.w, "grunt", s.ex, s.ey + s.outY * 24, Team.Blue, 1); t.MoveSpeed = 0; t.RealMoveSpeed = 0; t.Damage = 0;
                    OzV11Kit.PlayerAttack(s.w, q, t);
                    long attackEnd = -1; bool orderWhileAttacking = false; int endDist = -1;
                    s.Run(QuietSeconds + 20, () =>
                    {
                        bool attacking = q.Waypoints.Count > 0 && q.Waypoints[0].mode == WpMode.Attack && q.Waypoints[0].playerGiven;
                        if (attacking && s.orderTicks.Count > 0) orderWhileAttacking = true;
                        if (!attacking && attackEnd < 0) { attackEnd = s.w.TickIndex; endDist = s.DistToGate(q); }
                        return s.enteredTick >= 0;
                    });
                    long first = s.orderTicks.Count > 0 ? s.orderTicks[0] : -1;
                    Check(!orderWhileAttacking && t.Destroyed && attackEnd > 0 && first >= attackEnd && s.Fell,
                          $"A6 a squad 12 tiles out ordered onto a blue grunt 1.5 tiles out of the gate point: no order while the attack is on (it ended at {s.Seconds(attackEnd):0.00} s, target {(t.Destroyed ? "dead" : "alive")}, the leader then {endDist} px from the gate point), the rule's order {s.Seconds(first - attackEnd):0.00} s later, {s.Entry}");
                    if (endDist > ZWorld.FortAutoRushPx) Info($"A6 the attack ended with the leader {endDist} px out (it shoots from its range, outside the reach): it was sent once its own next steps brought it within {ZWorld.FortAutoRushPx} px");
                }
                {   // A7 HOLD POSITION
                    var s = New(set, l02, Team.Red, true); var q = s.Squad("grunt", Team.Red, 48);
                    bool held = s.w.SendHold(Team.Red, q);
                    s.Run(QuietSeconds);
                    Check(held && s.orderTicks.Count == 0 && !s.Fell, $"A7 a squad 3 tiles out on HOLD POSITION: no order in {QuietSeconds:0} s, the fort stands");
                }
                {   // A8 a closed gate: the tunnel's tiles closed
                    var s = New(set, l02, Team.Red, true); var q = s.Squad("grunt", Team.Red, 48);
                    int tx = s.fort.LocX / 16, ty = s.fort.LocY / 16;
                    s.w.Path.SetImpassable(tx + 4, ty + 7); s.w.Path.SetImpassable(tx + 5, ty + 7);
                    s.Run(QuietSeconds);
                    Check(s.orderTicks.Count == 0 && !s.Fell && s.w.FortAutoRushBlocked > 0, $"A8 the gate closed (its tunnel's two tiles in row 7 closed), a squad 3 tiles out: no order in {QuietSeconds:0} s ({s.w.FortAutoRushBlocked} checks found the way in closed), the fort stands");
                }
                {   // A9 a CPU squad at the player's own fort
                    var s = New(set, l02, Team.Red, true);
                    var red = s.w.Objects.First(o => o.IsFort && o.Owner == Team.Red);
                    red.GetCreationMovePoint(out int rx, out int ry); red.GetCreationPoint(out _, out int rcy);
                    var q = OzV11Kit.Place(s.w, "grunt", rx, ry + (ry > rcy ? 48 : -48), Team.Blue);
                    s.Run(QuietSeconds);
                    Check(s.w.FortAutoRushOrdersOf[(int)Team.Blue] == 0 && s.w.FortAutoRushOrders == 0 && !red.Destroyed && q.Waypoints.Count == 0,
                          "A9 a CPU (blue) squad idle 3 tiles out of the player's (red) gate: the rule gives it nothing, the red fort stands (the computer's own storm is DV60's)");
                }
                {   // A10 PLAY AS BLUE: the blue player at the red (back) fort
                    var s = New(set, l02, Team.Blue, true); var q = s.Squad("grunt", Team.Blue, 48);
                    s.Run(0.02); int d0 = s.DistToGate(q);
                    s.RunToEntry(EnterSeconds + 1); s.Run(2.5);
                    Check(s.orderTicks.Count > 0 && s.w.FortAutoRushOrdersOf[(int)Team.Blue] >= 1 && s.Fell && s.w.Winner == Team.Blue && s.Seconds(s.enteredTick - s.orderTicks[0]) <= EnterSeconds,
                          $"A10 PLAY AS BLUE: a blue squad {d0} px from the red fort's gate point (a back fort, its gate to the north): ordered at {s.Seconds(s.orderTicks.FirstOrDefault()):0.00} s, {s.Entry}, winner {s.w.Winner}");
                }
                {   // A11 the switch off
                    ZWorld.FortAutoRushDeviation = false;
                    try
                    {
                        var s = New(set, l02, Team.Red, true); s.Squad("grunt", Team.Red, 48);
                        s.Run(QuietSeconds);
                        Check(s.orderTicks.Count == 0 && !s.Fell, $"A11 the switch off ('Zod exact'): A1's squad stands {QuietSeconds:0} s, the fort stands (Zod's rule)");
                    }
                    finally { ZWorld.FortAutoRushDeviation = true; }
                }
                {   // A12 a computer match
                    var s = New(set, l02, Team.Red, true, 1, true); var q = s.Squad("grunt", Team.Red, 48); bool sentIn = false;
                    s.Run(QuietSeconds, () => { sentIn |= q.Waypoints.Count > 0 && q.Waypoints[0].mode == WpMode.EnterFort; return false; });
                    Check(s.orderTicks.Count == 0 && s.w.FortAutoRushOrders == 0 && !sentIn && !s.Fell,
                          $"A12 a computer match (both sides CPU, neither ordering): A1's squad is never sent in, the fort stands (the squad's own steps: {(q.Waypoints.Count > 0 ? q.Waypoints[0].mode.ToString() : "none")} at the end)");
                }
                {   // A13 robots only
                    var s = New(set, l02, Team.Red, true); var v = s.Squad("light", Team.Red, 48);
                    s.Run(QuietSeconds);
                    Check(s.orderTicks.Count == 0 && !s.Fell && v.Kind == ZKind.Vehicle, $"A13 a red {v.Type} tank idle 3 tiles out: no order (robots only; a vehicle is sent in by the player's click)");
                }
                foreach (var (tiles, want) in new[] { (5, false), (7, true) })
                {   // A14 / A15 one enemy grunt (harmless, unkillable) to the side of the gate point: inside the clear distance holds the squad back, outside it does not
                    var s = New(set, l02, Team.Red, true); s.Squad("grunt", Team.Red, 48);
                    var g = OzV11Kit.Place(s.w, "grunt", s.ex + tiles * 16, s.ey, Team.Blue, 1);
                    g.Damage = 0; g.MoveSpeed = 0; g.RealMoveSpeed = 0; g.MaxHealth = g.Health = 1000000000;
                    s.Run(10);
                    Check((s.orderTicks.Count > 0) == want && (want || s.w.FortAutoRushDefended > 0),
                          $"A{(want ? 15 : 14)} one blue grunt {Dist(s, g)} px ({tiles} tiles) to the side of the gate point (the clear distance is {ZWorld.FortAutoRushClearPx} px): {(s.orderTicks.Count > 0 ? $"sent at {s.Seconds(s.orderTicks[0]):0.00} s" : "no order in 10 s")}");
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ R: the real level 2 blue fort
        static void R(string set, Func<string, string> map)
        {
            using (Game(false))
            {
                {   // R1 the gate cleared by the player first (blue's units near it gone), the fort's own guns, fort gun and roof defender on
                    var s = New(set, map("L02"), Team.Red, false);
                    int cleared = 0;
                    foreach (var o in s.w.Objects)
                        if (o.IsUnit && o.Owner == Team.Blue && !o.IsTowerGun && o != s.keeper && Math.Abs(o.CenterX - s.ex) <= 12 * 16 && Math.Abs(o.CenterY - s.ey) <= 12 * 16) { o.KillAt = 0; cleared++; }
                    int towers = s.w.Objects.Count(o => o.IsTowerGun && o.Owner == Team.Blue);
                    s.Squad("grunt", Team.Red, 48, -12); s.Squad("grunt", Team.Red, 48, 20);
                    s.RunToEntry(EnterSeconds + 1);
                    long first = s.orderTicks.Count > 0 ? s.orderTicks[0] : -1;
                    Check(first > 0 && first <= 10 && s.enteredTick > 0 && s.Seconds(s.enteredTick - first) <= EnterSeconds && s.Fell,
                          $"R1 level 2 as it is, its gate cleared ({cleared} blue units within 12 tiles of it taken off; {towers} tower guns, the fort gun and the roof defender on): two red grunt squads idle 3 tiles out are sent at {s.Seconds(first):0.00} s, {s.Entry}");
                }
                {   // R2 the gate's defenders still there: no order while one of them stands within 6 tiles of the gate point
                    var s = New(set, map("L02"), Team.Red, false);
                    int near = s.w.Objects.Count(o => o.IsUnit && o.Owner == Team.Blue && o != s.keeper && Math.Abs(o.CenterX - s.ex) <= 12 * 16 && Math.Abs(o.CenterY - s.ey) <= 12 * 16);
                    var inClear = s.w.Objects.Where(o => o.IsUnit && o.Owner == Team.Blue && !o.IsTowerGun && Dist(s, o) <= ZWorld.FortAutoRushClearPx).Select(o => Dist(s, o)).OrderBy(d => d).ToList();
                    var a = s.Squad("grunt", Team.Red, 48, -12); var b = s.Squad("grunt", Team.Red, 48, 20);
                    bool sentWhileDefended = false; int seen = 0;
                    s.Run(EnterSeconds, () => { if (s.w.FortAutoRushOrders != seen) { seen = s.w.FortAutoRushOrders; if (s.w.FortAutoRushDefender(Team.Red, s.fort) != null) sentWhileDefended = true; } return s.enteredTick >= 0; });
                    int alive = OzV11Kit.Squad(a).Concat(OzV11Kit.Squad(b)).Count(OzV11Kit.Alive);
                    Check(!sentWhileDefended && s.w.FortAutoRushDefended > 0 && !s.Fell && (s.orderTicks.Count == 0 || s.w.FortAutoRushDefender(Team.Red, s.fort) == null),
                          $"R2 the same with blue's {near} units by its gate still standing ({inClear.Count} within {ZWorld.FortAutoRushClearPx} px of the gate point, at {string.Join("/", inClear)} px): no order while one of them lives ({s.w.FortAutoRushDefended} checks found the gate defended, orders {s.orderTicks.Count}), the fort stands, {alive} of 6 red alive after {EnterSeconds:0} s (they fought where they stood)");
                }
                using (Game(true))
                {   // R3 the defenders harmless and unkillable for 10 s (no order), then gone: sent at the next check (no fort gun, no roof defender: the squads wait unhurt)
                    var s = New(set, map("L02"), Team.Red, false);
                    var defs = s.w.Objects.Where(o => o.IsUnit && o.Owner == Team.Blue && !o.IsTowerGun && o != s.keeper && Dist(s, o) <= 12 * 16).ToList();
                    foreach (var o in defs) { o.Damage = 0; o.MoveSpeed = 0; o.RealMoveSpeed = 0; o.MaxHealth = o.Health = 1000000000; }
                    s.Squad("grunt", Team.Red, 48, -12); s.Squad("grunt", Team.Red, 48, 20);
                    s.Run(10);
                    int ordersDefended = s.orderTicks.Count; long gone = s.w.TickIndex;
                    foreach (var o in defs) o.KillAt = 0;
                    s.RunToEntry(EnterSeconds + 1);
                    long first = s.orderTicks.Count > 0 ? s.orderTicks[0] : -1;
                    Check(ordersDefended == 0 && first > gone && s.Seconds(first - gone) <= 0.5 && s.enteredTick > 0 && s.Seconds(s.enteredTick - first) <= EnterSeconds && s.Fell,
                          $"R3 blue's {defs.Count} units by its gate made harmless and unkillable: no order in 10 s; taken off: the order {s.Seconds(first - gone):0.00} s later (bar 0.5 s), {s.Entry}");
                }
                {   // R4 level 16: a gate with guns on the fort's tower spots (the fort's own guns do not count), the other units by it cleared
                    var s = New(set, map("L16"), Team.Red, false);
                    foreach (var o in s.w.Objects) if (o.IsUnit && o.Owner == Team.Blue && !o.IsTowerGun && o != s.keeper && Dist(s, o) <= 12 * 16) o.KillAt = 0;
                    int towers = s.w.Objects.Count(o => o.IsTowerGun && o.Owner == Team.Blue && !o.Destroyed);
                    var a = s.Squad("grunt", Team.Red, 48, -12); var b = s.Squad("grunt", Team.Red, 48, 20);
                    s.RunToEntry(EnterSeconds + 1);
                    long first = s.orderTicks.Count > 0 ? s.orderTicks[0] : -1;
                    Check(towers > 0 && first > 0 && first <= 10, $"R4 level 16, its gate cleared but its {towers} tower guns standing: sent at {s.Seconds(first):0.00} s (tower guns are the fort's own: they never hold the run back), {s.Entry}");
                }
            }
        }

        static int Dist(Scene s, ZObject o) => (int)Math.Round(Math.Sqrt((double)(o.CenterX - s.ex) * (o.CenterX - s.ex) + (double)(o.CenterY - s.ey) * (o.CenterY - s.ey)));

        // ------------------------------------------------------------------------------------------------ L: every level, both forts
        static void L(string set, Func<string, string> map)
        {
            using (Game(true))
                foreach (var lv in new[] { "L02", "L07", "L16", "L17" })
                    foreach (var human in new[] { Team.Red, Team.Blue })
                    {
                        var s = New(set, map(lv), human, true); var q = s.Squad("grunt", human, 48);
                        s.Run(0.02); int d0 = s.DistToGate(q);
                        s.RunToEntry(EnterSeconds + 1);
                        long first = s.orderTicks.Count > 0 ? s.orderTicks[0] : -1;
                        Check(first > 0 && first <= 10 && s.enteredTick > 0 && s.Seconds(s.enteredTick - first) <= EnterSeconds && s.Fell,
                              $"L {lv} {human} player at the {s.cpu} {s.fort.Type}: a squad {d0} px from the gate point, ordered at {s.Seconds(first):0.00} s, {s.Entry}");
                    }
        }

        // ------------------------------------------------------------------------------------------------ D: the same scene twice
        static void D(string set, Func<string, string> map)
        {
            var runs = new List<string>();
            using (Game(false))
                for (int k = 0; k < 2; k++)
                {
                    var s = New(set, map("L02"), Team.Red, false, 7);
                    s.Squad("grunt", Team.Red, 40, -16); s.Squad("pyro", Team.Red, 56, 24);
                    s.Run(15);
                    runs.Add($"orders {string.Join(",", s.orderTicks)} entered {s.enteredTick} by {s.enteredBy} events {s.hash:X16} rand {s.w.Rng.Next()}");
                }
            Check(runs[0] == runs[1], "D1 the same scene twice (level 2 as it is, two squads at the gate, 15 s): the same orders, entry and events: " + runs[0]);
        }

        public static string Run(string settings, Func<string, string> map)
        {
            pass = fail = 0; sb = new StringBuilder();
            bool was = ZWorld.FortAutoRushDeviation;
            try { G(settings, map); A(settings, map); R(settings, map); L(settings, map); D(settings, map); }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace); }
            finally { ZWorld.FortAutoRushDeviation = was; }
            sb.AppendLine($"[OpenZ V1.4 fort rush] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

        /// <summary>The Deviations row's probe on a quiet level 2: A1 (in), A3 (one tile past the reach: nothing), A11 (the switch off: nothing).</summary>
        public static string Quick(string settings, string l02)
        {
            pass = fail = 0; sb = new StringBuilder();
            bool was = ZWorld.FortAutoRushDeviation;
            try
            {
                using (Game(true))
                {
                    var s = New(settings, l02, Team.Red, true); s.Squad("grunt", Team.Red, 48);
                    s.RunToEntry(EnterSeconds + 1);
                    Check(s.orderTicks.Count > 0 && s.Fell, $"an idle red squad 3 tiles out of the blue gate is sent at {s.Seconds(s.orderTicks.FirstOrDefault()):0.00} s, {s.Entry}");
                    var f = New(settings, l02, Team.Red, true); f.Squad("grunt", Team.Red, 80);
                    f.Run(10);
                    Check(f.orderTicks.Count == 0 && !f.Fell, "one tile past the reach (5 tiles out): nothing in 10 s");
                    ZWorld.FortAutoRushDeviation = false;
                    var z = New(settings, l02, Team.Red, true); z.Squad("grunt", Team.Red, 48);
                    z.Run(10);
                    Check(z.orderTicks.Count == 0 && !z.Fell, "the switch off: nothing in 10 s (Zod's rule)");
                }
            }
            catch (Exception ex) { Check(false, "the probe threw " + ex.GetType().Name + ": " + ex.Message); }
            finally { ZWorld.FortAutoRushDeviation = was; }
            sb.AppendLine($"[OpenZ V1.4 fort rush quick] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

#if UNITY_EDITOR && !OPENZ_HARNESS
        [UnityEditor.MenuItem("OpenZ/V1.4/Fort: Auto Rush Count (headless, DV168)")]
        static void Menu() => OzV11Kit.RunMenu("fort: auto rush count (DV168)", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText));

        [UnityEditor.MenuItem("OpenZ/V1.4/Fort: Gate Click Check (Play, a level playing)")]
        static void GateMenu()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning("[OpenZ V1.4 fort gate] start a level in Play first (the RTS camera, the enemy fort standing)"); return; }
            OzFortGateCheck.Folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_4/kits/fort_finish/proof"));
            OzFortGateCheck.Begin();
        }

        [UnityEditor.MenuItem("OpenZ/V1.4/Fort: Gate Click Check, last report")]
        static void GateLast() => UnityEngine.Debug.Log(OzFortGateCheck.Running ? "[OpenZ V1.4 fort gate] running" : OzFortGateCheck.Report);
#endif
    }
}
