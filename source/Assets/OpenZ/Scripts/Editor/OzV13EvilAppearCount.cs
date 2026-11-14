using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// the Sim alone, on the four levels, nothing saved: menu OpenZ/V1.3/EvilZod: Appearances Count (headless, DV165), and off-project on plain .NET (the kit's
    /// tools/harness). One line per fact, [OpenZ V1.3 EvilZod appear] PASS n/n.
    ///  S  the guard spot of each blue fort: walkable, dry, out of every exit lane, away from flags, clear of every object, near the doors, reachable;
    ///  A  row 58, the retry help: one EvilZod at the blue fort after the head start, the player's own unit, once;
    ///     spot, the doors free, the fort's next units get out; a later condition: out at its first second; no fort sector condition: nothing;
    ///  G  the guard: fights in range, steps a little toward a target just out of range and comes back facing out, never chases, never takes an order, never boards,
    ///     never near a flag; D  dead = never again; P  same seed = same fight; the watch alone changes no world (both computers, every level).
    /// </summary>
    public static class OzV13EvilAppearCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);
        static readonly string[] Levels = { "L02", "L07", "L16", "L17" };
        const string E = ZEvilZod.Type;
        const long Out = ZEvilAppear.LastDefenseTicks;

        static string Compass(double dx, double dy) => Math.Abs(dx) >= Math.Abs(dy) ? (dx >= 0 ? "east" : "west") : (dy >= 0 ? "south" : "north");
        static double D(int ax, int ay, int bx, int by) => Math.Sqrt((double)(ax - bx) * (ax - bx) + (double)(ay - by) * (ay - by));

        /// <summary>A level world as the game makes it (level 17 parks its start vehicles), with the computers asked for.</summary>
        public static ZWorld Level(string set, Func<string, string> map, string lv, int seed, bool blueCpu, bool redCpu, bool blueIgnored = false)
        {
            var w = ZWorld.Create(set, map(lv), seed, lv == "L17");
            if (blueCpu) w.Bots.Add(new ZBot(w, Team.Blue, seed + 101) { Ignored = blueIgnored });
            if (redCpu) w.Bots.Add(new ZBot(w, Team.Red, seed));
            w.Events.Clear();
            return w;
        }

        /// <summary>The door axis of a fort: exit point and the unit vector out of the door.</summary>
        static void Door(ZWorld w, ZObject fort, out int ex, out int ey, out double ux, out double uy)
        {
            fort.GetCreationMovePoint(out ex, out ey);
            w.EvilDoorAxis(fort, out ux, out uy, out _);
        }

        /// <summary>The spot's rules, checked from outside (public members only). "" = every rule kept.</summary>
        public static string SpotFaults(ZWorld w, ZObject fort, int x, int y, ZObject self)
        {
            var f = new List<string>();
            int tx = x >> 4, ty = y >> 4;
            for (int j = ty - 1; j <= ty + 1; j++)
                for (int i = tx - 1; i <= tx + 1; i++)
                {
                    var c = w.Map.ClassAt(i, j);
                    if (!w.Path.TilePassable(i, j, true) || c == ZClass.Water || c == ZClass.DeepWater || w.Map.LavaAt(i, j)) { f.Add("tile " + i + "," + j); i = tx + 2; j = ty + 2; }
                }
            if (w.InExitLane(x, y, ZEvilZod.BodyRadius)) f.Add("in an exit lane");
            foreach (var g in w.Flags) if (D(x, y, g.CenterX, g.CenterY) < ZEvilAppear.FlagPx) f.Add("flag " + (int)D(x, y, g.CenterX, g.CenterY) + " px");
            int bx0 = x - 8, by0 = y - 8, bx1 = x + 8, by1 = y + 8, r = ZEvilAppear.RoomPx;
            foreach (var o in w.Objects)
            {
                if (o == self || o.KillAt >= 0 || (o.Destroyed && o.Kind != ZKind.Building) || o.Type == "flag") continue;
                if (o.LocX < bx1 && bx0 < o.LocX + o.WidthPix && o.LocY < by1 && by0 < o.LocY + o.HeightPix) f.Add("OVERLAPS " + o.Type);
                else if (o.LocX < bx1 + r && bx0 - r < o.LocX + o.WidthPix && o.LocY < by1 + r && by0 - r < o.LocY + o.HeightPix) f.Add("within " + r + " px of " + o.Type);
            }
            Door(w, fort, out int ex, out int ey, out double ux, out double uy);
            if ((x - ex) * ux + (y - ey) * uy < 0) f.Add("behind the gate");
            if (D(x, y, ex, ey) > ZEvilAppear.NearDoorPx) f.Add("far from the doors");
            if (!w.Path.InSameRegion(ex, ey, x, y, true)) f.Add("not reachable from the exit");
            return string.Join(", ", f);
        }

        /// <summary>The test stage of row 59: every sector flag the team owns goes to the other side (a red sweep), except keep (the flag kept, -1 = none).</summary>
        static void Sweep(ZWorld w, Team team, int keep = -1)
        {
            var foe = team == Team.Blue ? Team.Red : Team.Blue;
            foreach (var f in w.Flags.ToList())
            {
                if (f.Id == keep) { if (f.Owner != team) w.AwardZone(f, team); continue; }
                if (f.Owner == team || f.Owner == Team.Neutral) w.AwardZone(f, foe);
            }
        }

        static IEnumerable<ZObject> Evils(ZWorld w) => w.Objects.Where(o => o.Type == E && !o.Destroyed && o.KillAt < 0);

        /// <summary>The walk went through the fort's gate: a point of it inside the door passage (from 48 px before the exit point to the exit point, within 28 px of
        /// the door axis: the gate's half width, ZWorld.FortDoorHalf). Returns the nearest such point's distance to the axis (-1 = none).</summary>
        static int ThroughDoor(List<(int x, int y)> track, int ex, int ey, double ux, double uy)
        {
            int best = -1;
            foreach (var (x, y) in track)
            {
                double along = (x - ex) * ux + (y - ey) * uy, lat = Math.Abs((x - ex) * -uy + (y - ey) * ux);
                if (along >= -48 && along <= 0 && lat <= 28 && (best < 0 || lat < best)) best = (int)lat;
            }
            return best;
        }

        /// <summary>D3 (6 Nov 2026): row G5's walk home as measured in the last run (diagnostics only).</summary>
        public static string G5Diag = "";
        public sealed class StageRun
        {
            public ZWorld w; public ZObject fort, ez; public long outTick = -1, arrived = -1; public int created, built, outEvents, blueAtOut, spotX, spotY; public string faultsAtOut;
            public int lastMoveDx, lastMoveDy; public readonly List<(int x, int y)> track = new List<(int, int)>();
            public readonly List<ZEvent> evs = new List<ZEvent>();
            /// <summary>One tick: the events of this tick (kept), the guard's own record.</summary>
            public void Tick()
            {
                int px = ez?.CenterX ?? 0, py = ez?.CenterY ?? 0;
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                    if (e.type == ZEventType.EvilGuardOut) { outEvents++; if (ez == null) { ez = w.Get(e.objId); outTick = w.TickIndex; track.Add((ez.CenterX, ez.CenterY)); faultsAtOut = SpotFaults(w, fort, ez.EvilGuardX, ez.EvilGuardY, ez); blueAtOut = w.UnitCount(Team.Blue); spotX = ez.EvilGuardX; spotY = ez.EvilGuardY; } }
                foreach (var e in w.Events)
                {
                    if (ez != null && e.objId == ez.Id && e.type == ZEventType.Created) created++;
                    if (ez != null && e.objId == ez.Id && e.type == ZEventType.UnitBuilt && e.otherId == fort.Id) built++;
                    evs.Add(e);
                }
                w.Events.Clear();
                if (ez != null && !ez.Destroyed)
                {
                    int dx = ez.CenterX - px, dy = ez.CenterY - py;
                    if ((dx != 0 || dy != 0) && px != 0) { lastMoveDx = dx; lastMoveDy = dy; }
                    if (w.TickIndex % 10 == 0) track.Add((ez.CenterX, ez.CenterY));
                }
            }
        }

        /// <summary>Row 59 on one level with the test stage: the computer of blue ignored; from sweepFrom blue's buildings stop building (no crowd up to the unit
        /// cap round its fort: its factories are lost anyway) and its sectors are handed to red each second (keepFlag stays blue until keepUntil). Runs until untilTick.</summary>
        public static StageRun Stage(string set, Func<string, string> map, string lv, int seed, long sweepFrom, long untilTick, int keepFlag = -1, long keepUntil = -1, bool watch = true, bool stopBuild = true)
        {
            var w = Level(set, map, lv, seed, true, false, true);
            w.EvilLastDefenseTeam = watch ? Team.Blue : Team.Neutral;
            var run = new StageRun { w = w, fort = w.EvilFort(Team.Blue) };
            while (w.TickIndex < untilTick)
            {
                if (w.TickIndex == sweepFrom && stopBuild) foreach (var b in w.Objects.ToList()) if (b.Kind == ZKind.Building && b.IsProducer && b.Owner == Team.Blue) w.StopBuilding(Team.Blue, b);
                if (w.TickIndex >= sweepFrom && w.TickIndex % 100 == 99) Sweep(w, Team.Blue, w.TickIndex < keepUntil ? keepFlag : -1);
                run.Tick();
            }
            return run;
        }

        // ------------------------------------------------------------------------------------------------ S: the spots
        static void S(string set, Func<string, string> map)
        {
            foreach (var lv in Levels)
            {
                var w = Level(set, map, lv, 1, true, false);
                var fort = w.EvilFort(Team.Blue);
                bool ok = w.EvilGuardSpot(fort, null, out int x, out int y);
                Door(w, fort, out int ex, out int ey, out double ux, out double uy);
                w.EvilDoorAxis(fort, out _, out _, out int side);
                string faults = ok ? SpotFaults(w, fort, x, y, null) : "no spot";
                Check(ok && faults == "", $"S1 {lv}: blue {fort.Type} spot ({x}, {y}) px = tile ({x >> 4}, {y >> 4}), {(int)((x - ex) * ux + (y - ey) * uy)} px out of the exit point ({ex}, {ey}), {(int)Math.Abs((x - ex) * -uy + (y - ey) * ux)} px {Compass((x - ex) - ((x - ex) * ux + (y - ey) * uy) * ux, (y - ey) - ((x - ex) * ux + (y - ey) * uy) * uy)} of the door axis ({(side > 0 ? "right" : "left")} of the way out: the side toward the red fort, the right when it lies straight ahead): {(faults == "" ? "every rule kept" : faults)}");
                bool again = w.EvilGuardSpot(fort, null, out int x2, out int y2);
                Check(again && x2 == x && y2 == y, $"S2 {lv}: the same spot asked twice ({x2}, {y2})");
            }
        }

        // ------------------------------------------------------------------------------------------------ A: row 58, the retry help
        static readonly string[] HeadL02 = { "light", "light", "jeep", "jeep", "medium" }, HeadL16 = { "jeep", "jeep", "light" }, HeadL07 = { "jeep", "jeep", "light" }, HeadL17 = { "missile_launcher", "heavy" };

        static void A(string set, Func<string, string> map)
        {
            foreach (var lv in Levels)
            {
                var w = Level(set, map, lv, 2, false, true);
                var head = lv == "L02" ? HeadL02 : lv == "L16" ? HeadL16 : lv == "L17" ? HeadL17 : HeadL07;
                var extra = w.AddHeadStart(Team.Blue, lv == "L17", head);
                int before = w.Objects.Count;
                var e = w.AddEvilRetryHelp(Team.Blue);
                var fort = w.EvilFort(Team.Blue);
                string faults = e == null ? "none made" : SpotFaults(w, fort, e.CenterX, e.CenterY, e);
                Check(e != null && e.IsEvilZod && e.Owner == Team.Blue && e.Minions.Count == 0 && w.Objects.Count == before + 1 && w.EvilRetryId == e.Id && faults == "",
                      $"A1 {lv}: after the head start ({extra.Count} vehicles) one EvilZod, blue, at ({e?.CenterX}, {e?.CenterY}) in front of the blue fort: {(faults == "" ? "every rule kept, nothing overlapped" : faults)}");
                Check(e != null && !e.EvilGuarding && !e.HoldPosition && e.Waypoints.Count == 0 && !w.EvilGuardUsed && w.EvilLastDefenseTeam == Team.Neutral,
                      $"A2 {lv}: the player's own unit: not on guard, no hold, no order of his own, no last defense in this world");
                Check(w.AddEvilRetryHelp(Team.Blue) == null && Evils(w).Count() == 1, $"A3 {lv}: once: a second call makes nobody");
                Door(w, fort, out int ex, out int ey, out double ux, out double uy);
                int gx = e.CenterX, gy = e.CenterY;
                bool took = w.SendWaypoints(Team.Blue, e, new[] { ZWaypoint.Move((int)(gx + ux * 96), (int)(gy + uy * 96)) });
                for (int i = 0; i < 400; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); }
                Check(took && D(e.CenterX, e.CenterY, gx, gy) > 20, $"A4 {lv}: he takes the player's move order and walks ({D(e.CenterX, e.CenterY, gx, gy):0} px in 4 s)");
            }
            {
                bool was = ZWorld.EvilZodDeviation; ZWorld.EvilZodDeviation = false;
                try { var w = Level(set, map, "L02", 2, false, true); Check(w.AddEvilRetryHelp(Team.Blue) == null && !Evils(w).Any(), "A5 DV161 off ('Zod exact'): no retry help"); }
                finally { ZWorld.EvilZodDeviation = was; }
            }
            {
                var w1 = Level(set, map, "L07", 4, false, true); var a = w1.AddEvilRetryHelp(Team.Blue);
                var w2 = Level(set, map, "L07", 4, false, true); var b = w2.AddEvilRetryHelp(Team.Blue);
                ulong h1 = 1469598103934665603UL, h2 = h1;
                for (int i = 0; i < 3000; i++) { w1.Tick(ZConst.TICK); w1.Events.Clear(); w2.Tick(ZConst.TICK); w2.Events.Clear(); }
                h1 = w1.RulesFingerprint(h1); h2 = w2.RulesFingerprint(h2);
                Check(a.Id == b.Id && a.CenterX == b.CenterX && a.CenterY == b.CenterY && h1 == h2, $"A6 same seed, same start: the same id, spot and 30 s with the red computer ({h1:x16})");
                var w3 = Level(set, map, "L07", 4, false, true);
                Check(w3.AddEvilRetryHelp(Team.Red) != null, "A7 the rule itself takes any team (View/ZGame.cs asks it for a blue player only)");
            }
            foreach (var lv in new[] { "L16", "L17" })
            {   // EASY / MEDIUM with the SHUTTLE box: a blue player's ship stands by the blue fort (DV144); he keeps 3 m from its outline and off its ramp lane
                bool sp = ZWorld.ShuttlePlayableDeviation, on = ZWorld.ShuttleOn, ss = ZWorld.SideShuttlesDeviation, l17 = ZWorld.L17ShuttlesDeviation, l17s = ZWorld.L17StartShuttle; var st = ZWorld.ShuttleTeam;
                try
                {
                    ZWorld.ShuttlePlayableDeviation = true; ZWorld.ShuttleOn = true; ZWorld.SideShuttlesDeviation = true; ZWorld.L17ShuttlesDeviation = true; ZWorld.L17StartShuttle = true; ZWorld.ShuttleTeam = Team.Blue;
                    var w = Level(set, map, lv, 2, false, true);
                    w.AddHeadStart(Team.Blue, lv == "L17", lv == "L16" ? HeadL16 : HeadL17);
                    var e = w.AddEvilRetryHelp(Team.Blue); var fort = w.EvilFort(Team.Blue);
                    var ships = w.Objects.Where(o => o.IsShuttle && !o.Destroyed).ToList();
                    double gap = 99, lane = 99;
                    if (e != null) foreach (var s in ships) { ZWorld.ShuttleBoxGaps(s, e.CenterX - 8, e.CenterY - 8, e.CenterX + 8, e.CenterY + 8, out double g, out double lg); gap = Math.Min(gap, g); lane = Math.Min(lane, lg); }
                    string faults = e == null ? "none made" : SpotFaults(w, fort, e.CenterX, e.CenterY, e);
                    Check(e != null && faults == "" && gap >= ZEvilAppear.ShuttleClearM && lane >= ZEvilAppear.ShuttleLaneClearM,
                          $"A8 {lv} with the SHUTTLE box ({ships.Count} ships on the map): at ({e?.CenterX}, {e?.CenterY}), {gap:0.0} m from the nearest ship's outline, {lane:0.0} m from its ramp lane: {(faults == "" ? "every rule kept" : faults)}");
                }
                finally { ZWorld.ShuttlePlayableDeviation = sp; ZWorld.ShuttleOn = on; ZWorld.SideShuttlesDeviation = ss; ZWorld.L17ShuttlesDeviation = l17; ZWorld.L17StartShuttle = l17s; ZWorld.ShuttleTeam = st; }
            }
        }

        // ------------------------------------------------------------------------------------------------ L: row 59, the last defense
        static void L(string set, Func<string, string> map)
        {
            var run = Stage(set, map, "L02", 1, 30000, Out - 1);
            var w = run.w; var fort = run.fort;
            Sweep(w, Team.Blue);
            Check(run.ez == null && !Evils(w).Any() && !w.EvilGuardUsed && w.EvilLastDefenseDue(Team.Blue, out _),
                  $"L1 L02: blue holds only its fort's sector from 5:00; at 9:59.99 nobody came out (due {w.EvilLastDefenseDue(Team.Blue, out _)})");
            run.Tick();
            Door(w, fort, out int ex, out int ey, out double ux, out double uy);
            fort.GetCreationPoint(out int cx, out int cy);
            var ez = run.ez;
            Check(ez != null && run.outTick == Out && w.EvilGuardOutTick == Out && ez.IsEvilZod && ez.Owner == Team.Blue && ez.EvilGuarding && ez.HoldPosition,
                  $"L2 L02: at 10:00.00 (tick {run.outTick}) EvilZod comes out, blue, on guard");
            Check(ez != null && D(ez.CenterX, ez.CenterY, cx, cy) <= 3 && run.created == 1 && run.built == 1 && run.outEvents == 1,
                  $"L3 L02: made inside the fort at its creation point ({ez?.CenterX}, {ez?.CenterY} vs {cx}, {cy}), Created + UnitBuilt (the door, the roll-out) + EvilGuardOut (his line) once each");
            if (ez == null) return;
            int gx = ez.EvilGuardX, gy = ez.EvilGuardY;
            Check(run.faultsAtOut == "", $"L4 L02: his spot ({gx}, {gy}) keeps every rule in the tick he came out ({run.blueAtOut} blue units on the map): {run.faultsAtOut}");
            while (w.TickIndex < Out + 4000 && run.arrived < 0)
            {
                run.Tick();
                if (!ez.EvilGuardWalkingOut && D(ez.CenterX, ez.CenterY, gx, gy) <= 4) run.arrived = w.TickIndex;
            }
            double lastDot = (run.lastMoveDx * ux + run.lastMoveDy * uy) / Math.Max(1e-9, Math.Sqrt(run.lastMoveDx * run.lastMoveDx + run.lastMoveDy * run.lastMoveDy));
            int door = ThroughDoor(run.track, ex, ey, ux, uy);
            Check(run.arrived > 0 && door >= 0, $"L5 L02: he walks out through the gate ({door} px from the door axis in the passage) and stands on his spot {(run.arrived - Out) / 100.0:0.0} s after the door opened");
            Check(lastDot > 0.7, $"L6 L02: his last step onto the spot points out of the door ({lastDot:0.00}): he stands facing the approach");
            // standing 5 min among blue's crowd (its production ran until the stage began): never in a lane, on his spot, no flag, no order
            int laneTicks = 0, farTicks = 0, flagNear = 9999, foreign = 0;
            for (int i = 0; i < 30000; i++)
            {
                run.Tick();
                if (w.InExitLane(ez.CenterX, ez.CenterY, ZEvilZod.BodyRadius)) laneTicks++;
                if (D(ez.CenterX, ez.CenterY, gx, gy) > ZEvilAppear.HomePx) farTicks++;
                foreach (var f in w.Flags) flagNear = Math.Min(flagNear, (int)D(ez.CenterX, ez.CenterY, f.CenterX, f.CenterY));
                foreach (var p in ez.Waypoints) if (p.playerGiven || (p.mode != WpMode.Move && p.mode != WpMode.ForceMove)) foreign++;
            }
            Check(laneTicks == 0 && farTicks == 0, $"L7 L02: 5 min standing among {w.UnitCount(Team.Blue)} blue units: never in an exit lane ({laneTicks} ticks), never more than {ZEvilAppear.HomePx} px off his spot ({farTicks} ticks)");
            Check(flagNear >= 64 && foreign == 0, $"L9 L02: never near a flag (closest {flagNear} px), never an order on him (an order's tick: {foreign})");
            int wpBefore = ez.Waypoints.Count;
            bool took = w.SendWaypoints(Team.Blue, ez, new[] { ZWaypoint.Move(ez.CenterX + 400, ez.CenterY + 400) })
                        || w.SendWaypoints(Team.Blue, ez, new[] { new ZWaypoint { mode = WpMode.Attack, refId = fort.Id, x = fort.CenterX, y = fort.CenterY } })
                        || w.SendHold(Team.Blue, ez) || w.SendStop(Team.Blue, ez);
            Check(!took && ez.Waypoints.Count == wpBefore && ez.HoldPosition, "G1 L02: a move, an attack, hold and stop sent to him: all refused, his own state kept");

            // dead = never again
            ez.DamageHealth(ez.Health); w.UpdateObjectHealth(ez);
            int dueSec = 0;
            for (int i = 0; i < 30000; i++) { if (w.TickIndex % 100 == 99) Sweep(w, Team.Blue); run.Tick(); if (w.TickIndex % 100 == 0 && w.EvilLastDefenseDue(Team.Blue, out _)) dueSec++; }
            Check(ez.Destroyed && !Evils(w).Any() && run.outEvents == 1 && w.EvilGuardId == ez.Id && dueSec >= 250 && !w.GameOver,
                  $"D1 L02: killed; 5 more minutes with blue holding only its fort sector: no second EvilZod (dead {ez.Destroyed}, alive of his type {Evils(w).Count()}, EvilGuardOut events {run.outEvents}, game over {w.GameOver}, the condition held {dueSec} of those 300 seconds)");

            // a second L02 world: blue builds nothing from the start (no crowd), the game kept running (the blue computer wakes later): the doors, the fights
            var runB = Stage(set, map, "L02", 1, 0, Out + 3000);
            var wb = runB.w; ez = runB.ez; fort = runB.fort;
            if (ez == null) { Check(false, "L8 L02 (no crowd): nobody came out at 10:00"); return; }
            wb.NoGameEnd = true;                                                                   // the blue computer must not end the game against an idle red side
            gx = ez.EvilGuardX; gy = ez.EvilGuardY;
            Check(runB.outTick == Out && D(ez.CenterX, ez.CenterY, gx, gy) <= 4, $"L8a L02 (blue built nothing): out at {runB.outTick / 100.0:0.00} s, on his spot ({gx}, {gy}) 30 s later");
            // the doors: the fort builds a jeep, then grunts, for 5 min; the same in a twin world where he never came out: he takes nothing from the gate
            List<(string type, double s)> DoorRun(StageRun rr, ZObject guard)
            {
                var ww = rr.w; var ff = rr.fort; var leaving = new Dictionary<int, long>(); var res = new List<(string, double)>();
                ww.StartBuilding(Team.Blue, ff, "jeep");
                long tt = ww.TickIndex;
                while (ww.TickIndex < tt + 30000)
                {
                    if (ww.TickIndex == tt + 12000) ww.StartBuilding(Team.Blue, ff, "grunt");
                    int n0 = rr.evs.Count;
                    rr.Tick();
                    for (int i = n0; i < rr.evs.Count; i++) { var e = rr.evs[i]; if (e.type == ZEventType.Created && e.otherId == ff.Id && (guard == null || e.objId != guard.Id)) leaving[e.objId] = ww.TickIndex; }
                    foreach (var kv in leaving.ToList())
                    {
                        var u = ww.Get(kv.Key);
                        if (u == null || u.Destroyed) { leaving.Remove(kv.Key); continue; }
                        if ((u.CenterX - ex) * ux + (u.CenterY - ey) * uy >= -8) { res.Add((u.Type, (ww.TickIndex - kv.Value) / 100.0)); leaving.Remove(kv.Key); }   // at the exit point or past it
                        else if (ww.TickIndex - kv.Value > 3000) { res.Add((u.Type + " (stays in the gate behind its squad mate on the exit point)", 99)); leaving.Remove(kv.Key); }
                    }
                }
                return res;
            }
            var withHim = DoorRun(runB, ez);
            var twin = Stage(set, map, "L02", 1, 0, Out + 3000, -1, -1, false); twin.w.NoGameEnd = true;
            var withoutHim = DoorRun(twin, null);
            bool sameDoor = withHim.Count == withoutHim.Count && withHim.Count >= 2 && withHim.Any(x => x.type == "jeep") && withHim.Zip(withoutHim, (x, y) => x.s <= y.s + 1.0).All(b => b);
            Check(sameDoor, $"L8 L02: the fort's next {withHim.Count} units leave its gate past him as in a twin world without him: with him {string.Join(", ", withHim.Select(x => x.type + " " + x.s.ToString("0.0") + " s"))}; without him {string.Join(", ", withoutHim.Select(x => x.type + " " + x.s.ToString("0.0") + " s"))}");

            // fights: three red grunts attack him
            var sq = wb.CreateRobotGroup("grunt", (int)(gx + ux * 210) - 8, (int)(gy + uy * 210) - 8, Team.Red, 3);
            var reds = new[] { sq }.Concat(sq.Minions).ToList();
            wb.SendWaypoints(Team.Red, sq, new[] { new ZWaypoint { mode = WpMode.Attack, refId = ez.Id, x = ez.CenterX, y = ez.CenterY } });
            int shots = 0; double maxOff = 0; int n1 = runB.evs.Count;
            for (int i = 0; i < 6000 && reds.Any(r => !r.Destroyed && r.KillAt < 0); i++) { runB.Tick(); maxOff = Math.Max(maxOff, D(ez.CenterX, ez.CenterY, gx, gy)); }
            for (int i = n1; i < runB.evs.Count; i++) if (runB.evs[i].type == ZEventType.EvilShot && runB.evs[i].objId == ez.Id) shots++;
            int down = reds.Count(r => r.Destroyed || r.KillAt >= 0);
            Check(shots >= 1 && down == 3 && !ez.Destroyed && maxOff <= ZEvilAppear.StepPx + ZEvilAppear.HomePx, $"G3 L02: three red grunts attack him: he fires ({shots} shots), all three down (the fort's guns help), he stays within {maxOff:0} px of his spot");
            for (int i = 0; i < 1500; i++) runB.Tick();

            // a step: a red heavy tank just out of his range, harmless, unkillable; then it goes and he walks home facing out.
            // 1.4: his step lane is kept free of his own team first. The row hung on where blue's idle grunts of this 15-minute world happened to stand: one
            // within 17 px of his step point ended each walk 9 px short of it, out of range (19 px, 0 shots; the same with the 1.3 rules, on the editor's Mono).
            // Every blue unit whose centre lies within 40 px of the line from his spot to 40 px toward the tank is taken out (RemoveNow) before the tank comes.
            int laneOut = 0;
            {
                double vx = ux * 40, vy = uy * 40;
                foreach (var o in wb.Objects.ToList())
                {
                    if (o == ez || !o.IsUnit || o.Owner != Team.Blue || o.Destroyed || o.KillAt >= 0) continue;
                    double t = Math.Max(0, Math.Min(1, ((o.CenterX - gx) * vx + (o.CenterY - gy) * vy) / (vx * vx + vy * vy)));
                    double ddx = o.CenterX - (gx + vx * t), ddy = o.CenterY - (gy + vy * t);
                    if (ddx * ddx + ddy * ddy < 40.0 * 40.0) laneOut += wb.RemoveNow(o);
                }
            }
            var tank = wb.CreateObject("vehicle", "heavy", (int)(gx + ux * (ez.AttackRadius + 22)) - 16, (int)(gy + uy * (ez.AttackRadius + 22)) - 16, Team.Red);
            tank.Damage = 0; tank.MoveSpeed = 0; tank.MaxHealth = tank.Health = 100000000;
            int steps0 = ez.EvilGuardSteps, shotsAt = 0; maxOff = 0; n1 = runB.evs.Count; double startOff = D(ez.CenterX, ez.CenterY, gx, gy);
            for (int i = 0; i < 2500; i++) { runB.Tick(); maxOff = Math.Max(maxOff, D(ez.CenterX, ez.CenterY, gx, gy)); }
            for (int i = n1; i < runB.evs.Count; i++) if (runB.evs[i].type == ZEventType.EvilShot && runB.evs[i].objId == ez.Id && runB.evs[i].otherId == tank.Id) shotsAt++;
            Check(ez.EvilGuardSteps > steps0 && shotsAt >= 1 && maxOff <= ZEvilAppear.StepPx + 3, $"G4 L02: a tank {ez.AttackRadius + 22} px from his spot (out of his range; he stood {startOff:0} px off it; {laneOut} blue units taken out of his 40 px step lane first): he steps {maxOff:0} px toward it and fires ({shotsAt} shots)");
            tank.Health = 1; tank.DamageHealth(1); wb.UpdateObjectHealth(tank);
            long home = -1; runB.lastMoveDx = runB.lastMoveDy = 0;
            var g5Track = new List<string>(); int g5px = ez.CenterX, g5py = ez.CenterY, g5Walks0 = ez.EvilGuardHomeWalks, g5Settles0 = ez.EvilGuardSettles; double g5MinAlong = double.MaxValue;   // D3 (6 Nov 2026) diagnostics only: read, never written
            for (int i = 0; i < 2500 && home < 0; i++)
            {
                runB.Tick(); if (i > 0 && ez.Waypoints.Count == 0 && D(ez.CenterX, ez.CenterY, gx, gy) <= 4) home = i;
                double along = (ez.CenterX - gx) * ux + (ez.CenterY - gy) * uy, across = -(ez.CenterX - gx) * uy + (ez.CenterY - gy) * ux; g5MinAlong = Math.Min(g5MinAlong, along);
                if (ez.CenterX != g5px || ez.CenterY != g5py) { g5Track.Add($"{i / 100.0:0.00}s {along:+0;-0}/{across:+0;-0} wp{ez.Waypoints.Count}"); if (g5Track.Count > 14) g5Track.RemoveAt(0); g5px = ez.CenterX; g5py = ez.CenterY; }
            }
            lastDot = (runB.lastMoveDx * ux + runB.lastMoveDy * uy) / Math.Max(1e-9, Math.Sqrt(runB.lastMoveDx * runB.lastMoveDx + runB.lastMoveDy * runB.lastMoveDy));
            Check(home >= 0 && lastDot > 0.7, $"G5 L02: the tank gone, he is back on his spot after {home / 100.0:0.0} s, the last step out of the door ({lastDot:0.00})");
            G5Diag = ($"[D3 G5 diagnostics] home walks {ez.EvilGuardHomeWalks - g5Walks0}, settles {ez.EvilGuardSettles - g5Settles0}, spot now ({ez.EvilGuardX - gx:+0;-0}, {ez.EvilGuardY - gy:+0;-0}) from the row's, back point along {(ez.EvilGuardBackX - gx) * ux + (ez.EvilGuardBackY - gy) * uy:0} px, "
                              + $"deepest point along the door axis {g5MinAlong:0} px (minus = behind his spot); his last moves (time along/across px, waypoints): {string.Join(" | ", g5Track)}");   // read after a run (no log line: the off-Unity harness builds this file too)

            // no chase: a red grunt in his range runs off
            var runner = wb.CreateRobotGroup("grunt", (int)(gx + ux * 100) - 8, (int)(gy + uy * 100) - 8, Team.Red, 1);
            runner.Damage = 0; runner.MaxHealth = runner.Health = 100000000;
            for (int i = 0; i < 400; i++) runB.Tick();
            bool engaged = ez.AttackObject == runner;
            bool ran = wb.SendWaypoints(Team.Red, runner, new[] { ZWaypoint.Move((int)(gx + ux * 520), (int)(gy + uy * 520)) }); int rwp = runner.Waypoints.Count;
            maxOff = 0;
            for (int i = 0; i < 4000; i++) { runB.Tick(); maxOff = Math.Max(maxOff, D(ez.CenterX, ez.CenterY, gx, gy)); }
            Check(maxOff <= ZEvilAppear.StepPx + 3 && D(runner.CenterX, runner.CenterY, gx, gy) > ez.AttackRadius + ZEvilAppear.StepPx && D(ez.CenterX, ez.CenterY, gx, gy) <= ZEvilAppear.HomePx,
                  $"G6 L02: a grunt he was firing at ({(engaged ? "engaged" : "seen")}, its order {(ran ? "taken" : "refused")}, {rwp} waypoints) runs off: he never follows past {maxOff:0} px, back on his spot, the grunt {D(runner.CenterX, runner.CenterY, gx, gy):0} px away");
            runner.Health = 1; runner.DamageHealth(1); wb.UpdateObjectHealth(runner);

            // never boards: an empty jeep beside him for 30 s
            var jeep = wb.CreateObject("vehicle", "jeep", gx + (int)(-uy * 40) - 16, gy + (int)(ux * 40) - 16, Team.Neutral);
            bool boarded = false;
            for (int i = 0; i < 3000; i++) { runB.Tick(); if (ez.KillAt >= 0 && !ez.Destroyed) boarded = true; }
            Check(!boarded && !ez.Destroyed && ez.KillAt < 0, $"G7 L02: an empty jeep 40 px beside him for 30 s: he never gets in (the jeep: {(jeep.Owner == Team.Neutral ? "still empty" : "taken by another " + jeep.Owner + " unit")})");

            // straight out of the gate (past its lane, harmless, unkillable) when he comes out: he stops on his way, clear of the door, and fires; they go, he walks to his spot
            {
                var runC = Stage(set, map, "L02", 1, 0, Out - 1);
                var wc = runC.w; wc.NoGameEnd = true;
                Sweep(wc, Team.Blue); runC.Tick();
                var ec = runC.ez;
                if (ec == null) { Check(false, "G8 L02: nobody came out at 10:00"); }
                else
                {
                    Door(wc, runC.fort, out int cex, out int cey, out double cux, out double cuy);
                    int cgx = ec.EvilGuardX, cgy = ec.EvilGuardY; double outPx = Math.Max(ec.AttackRadius * 0.75, 90);
                    var sqC = wc.CreateRobotGroup("grunt", (int)(cex + cux * outPx) - 8, (int)(cey + cuy * outPx) - 8, Team.Red, 3);
                    var redsC = new[] { sqC }.Concat(sqC.Minions).ToList();
                    foreach (var rc in redsC) { rc.Damage = 0; rc.MoveSpeed = 0; rc.MaxHealth = rc.Health = 100000000; }
                    int nC = runC.evs.Count, shotsC = 0; long firstShot = -1; double offAtShot = -1; bool laneAtShot = false, walkingAtShot = false;
                    for (int i = 0; i < 3000; i++)
                    {
                        runC.Tick();
                        for (int k = nC; k < runC.evs.Count; k++)
                            if (runC.evs[k].type == ZEventType.EvilShot && runC.evs[k].objId == ec.Id)
                            {
                                shotsC++;
                                if (firstShot < 0) { firstShot = wc.TickIndex; offAtShot = D(ec.CenterX, ec.CenterY, cgx, cgy); laneAtShot = wc.InExitLane(ec.CenterX, ec.CenterY, ZEvilZod.BodyRadius); walkingAtShot = ec.EvilGuardWalkingOut; }
                            }
                        nC = runC.evs.Count;
                    }
                    Check(ec.EvilGuardFights == 1 && shotsC >= 2 && firstShot > 0 && offAtShot > ZEvilAppear.HomePx && !laneAtShot && !walkingAtShot && !ec.Destroyed,
                          $"G8 L02: three red grunts {outPx:0} px straight out of the gate when he comes out: his walk out cut short {ec.EvilGuardFights} time(s), he fires ({shotsC} shots in 30 s, the first {(firstShot - Out) / 100.0:0.0} s after the door opened, {offAtShot:0} px short of his spot, in a lane {laneAtShot}, still walking out {walkingAtShot})");
                    foreach (var rc in redsC) { rc.Health = 1; rc.DamageHealth(1); wc.UpdateObjectHealth(rc); }
                    long homeC = -1;
                    for (int i = 0; i < 4000 && homeC < 0; i++) { runC.Tick(); if (ec.Waypoints.Count == 0 && D(ec.CenterX, ec.CenterY, cgx, cgy) <= 4) homeC = i; }
                    Check(homeC >= 0 && !ec.Destroyed, $"G9 L02: the grunts gone, he walks on to his spot ({cgx}, {cgy}) and stands on it {homeC / 100.0:0.0} s later");
                }
            }

            // the computer of blue awake for 2 min: he takes none of its orders
            var bot = wb.Bots.First(bb => bb.Team == Team.Blue); bot.Ignored = false; int rej0 = bot.RejectedOrders;
            foreign = 0; farTicks = 0; boarded = false; int helper = 0;
            for (int i = 0; i < 12000; i++)
            {
                runB.Tick();
                foreach (var p in ez.Waypoints) { if (p.playerGiven || (p.mode != WpMode.Move && p.mode != WpMode.ForceMove)) foreign++; else if (p.refId != ZEvilAppear.RefGuard) helper++; }
                if (D(ez.CenterX, ez.CenterY, gx, gy) > ZEvilAppear.StepPx + ZEvilAppear.HomePx) farTicks++;
                if (ez.KillAt >= 0 && !ez.Destroyed) boarded = true;
            }
            bot.Ignored = true;
            Check(foreign == 0 && farTicks == 0 && !boarded, $"G2 L02: 2 min with blue's computer giving orders: no order of it on him ({foreign}), never away from his spot ({farTicks} ticks), never in anything");
            Info($"L02: blue's computer refused orders in those 2 min: {bot.RejectedOrders - rej0} (its planners leave him out); the rules' own helper steps on him (a scuffle nudge, a step aside): {helper} ticks");


            foreach (var lv in new[] { "L07", "L16", "L17" })
            {
                var r = Stage(set, map, lv, 3, 30000, Out + 4000);
                var e = r.ez; var f = r.fort;
                Door(r.w, f, out int ex2, out int ey2, out double ux2, out double uy2);
                bool at = e != null && D(e.CenterX, e.CenterY, e.EvilGuardX, e.EvilGuardY) <= 4 && !e.EvilGuardWalkingOut;
                string faults = e == null ? "nobody" : r.faultsAtOut;                              // the rule is kept when he is made; later his own team may stand close (no overlap: below)
                string hit = e == null ? "" : string.Join(", ", r.w.Objects.Where(o => o != e && o.Kind != ZKind.Robot && o.Type != "flag" && o.KillAt < 0 && !(o.Destroyed && o.Kind != ZKind.Building)
                    && o.LocX < e.CenterX + 8 && e.CenterX - 8 < o.LocX + o.WidthPix && o.LocY < e.CenterY + 8 && e.CenterY - 8 < o.LocY + o.HeightPix).Select(o => o.Type));
                int doorE = e == null ? -1 : ThroughDoor(r.track, ex2, ey2, ux2, uy2);
                Check(e != null && r.outTick == Out && r.outEvents == 1 && at && faults == "" && hit == "" && doorE >= 0 && !r.w.InExitLane(e.CenterX, e.CenterY, ZEvilZod.BodyRadius),
                      $"L10 {lv}: out at {(r.outTick < 0 ? "never" : (r.outTick / 100.0).ToString("0.00") + " s")}, through the gate ({doorE} px from the door axis), on his spot ({e?.EvilGuardX}, {e?.EvilGuardY}) within 40 s ({r.blueAtOut} blue units on the map): {(faults == "" ? "every rule kept when he came out" : faults)}{(hit == "" ? ", nothing but robots near him 40 s later, no overlap" : "; OVERLAPS " + hit)}");
            }

            // a crowd: blue's production never stopped, its idle robots fill the gate's front (they do not step aside for a robot): he ends standing still, outside every lane
            {
                var r = Stage(set, map, "L16", 3, 30000, Out + 2000, -1, -1, true, false);
                var e = r.ez;
                if (e == null) Check(false, "L14 L16 crowd: nobody came out");
                else
                {
                    int gx0 = e.EvilGuardX, gy0 = e.EvilGuardY, walks0 = e.EvilGuardHomeWalks; bool moved = false;
                    for (int i = 0; i < 2000; i++) { int px = e.CenterX, py = e.CenterY; r.Tick(); if (e.CenterX != px || e.CenterY != py) moved = true; }
                    bool still = !moved && e.EvilGuardHomeWalks == walks0 && e.Waypoints.Count == 0;
                    bool atSpot = D(e.CenterX, e.CenterY, e.EvilGuardX, e.EvilGuardY) <= ZEvilAppear.HomePx;
                    Check(still && atSpot && !r.w.InExitLane(e.CenterX, e.CenterY, ZEvilZod.BodyRadius) && !e.Destroyed,
                          $"L14 L16 with {r.blueAtOut} blue units, the gate's front full of idle robots: 20 s later he stands still ({(still ? "no walk in the last 20 s" : "still walking")}) outside every exit lane at ({e.CenterX}, {e.CenterY}), {(e.EvilGuardSettles > 0 ? $"walled in by his own team he took that place as his spot ({e.EvilGuardSettles}x, {D(e.CenterX, e.CenterY, r.spotX, r.spotY):0} px from the spot he was given)" : "on his spot")}");
                }
            }
            {
                var w0 = Level(set, map, "L02", 1, true, false, true);
                int keep = w0.Flags.Where(fl => fl.Owner == Team.Blue).Select(fl => fl.Id).DefaultIfEmpty(w0.Flags[0].Id).First();
                var r = Stage(set, map, "L02", 1, 30000, 70100, keep, 69999);
                Check(r.outTick == 70000 && r.outEvents == 1, $"L11 L02: blue keeps a sector until 11:40: nobody at 10:00, out at {(r.outTick < 0 ? "never" : (r.outTick / 100.0).ToString("0.00") + " s")} (the first second after it fell)");
            }
            // the watch off: nothing ever
            {
                var w0 = Level(set, map, "L02", 1, true, false, true);
                for (int i = 0; i < 61000; i++) { if (i >= 30000 && i % 100 == 99) Sweep(w0, Team.Blue); w0.Tick(ZConst.TICK); w0.Events.Clear(); }
                Check(!Evils(w0).Any() && !w0.EvilGuardUsed, "L12 L02: a world without a last-defense team (bot match, headless, the Unit editor): the same stage, nobody ever");
            }
            // DV161 off: nothing
            {
                bool was = ZWorld.EvilZodDeviation; ZWorld.EvilZodDeviation = false;
                try { var r = Stage(set, map, "L02", 1, 30000, Out + 200); Check(r.ez == null && !Evils(r.w).Any(), "L13 DV161 off ('Zod exact'): nobody"); }
                finally { ZWorld.EvilZodDeviation = was; }
            }
        }

        // ------------------------------------------------------------------------------------------------ P: determinism, the watch alone
        static string Prints(ZWorld w, long ticks, long every)
        {
            var s = new StringBuilder(); ulong h = 1469598103934665603UL;
            for (long t = 1; t <= ticks; t++) { w.Tick(ZConst.TICK); w.Events.Clear(); if (t % every == 0) { h = w.RulesFingerprint(h); s.Append(' ').Append(h.ToString("x16")); } }
            return s.ToString();
        }

        static void P(string set, Func<string, string> map, bool full)
        {
            {
                var a = Stage(set, map, "L02", 5, 30000, Out + 3000); var b = Stage(set, map, "L02", 5, 30000, Out + 3000);
                ulong ha = a.w.RulesFingerprint(1469598103934665603UL), hb = b.w.RulesFingerprint(1469598103934665603UL);
                Check(a.outTick == b.outTick && a.ez != null && b.ez != null && a.ez.EvilGuardX == b.ez.EvilGuardX && a.ez.EvilGuardY == b.ez.EvilGuardY && ha == hb && a.track.SequenceEqual(b.track),
                      $"P1 L02 seed 5 twice with him: the same tick out, spot, walk and world ({ha:x16})");
            }
            foreach (var lv in Levels)
            {
                var off = Level(set, map, lv, 7, true, true); var on = Level(set, map, lv, 7, true, true); on.EvilLastDefenseTeam = Team.Blue;
                string po = Prints(off, 12000, 1000), pn = Prints(on, 12000, 1000);
                Check(po == pn && po.Length > 0 && !on.EvilGuardUsed, $"P2 {lv}: both computers 120 s, the last-defense watch on and off: the same world every 10 s");
            }
            if (full)
            {
                var off = Level(set, map, "L02", 9, true, true); var on = Level(set, map, "L02", 9, true, true); on.EvilLastDefenseTeam = Team.Blue;
                string po = Prints(off, 66000, 6000), pn = Prints(on, 66000, 6000);
                if (on.EvilGuardUsed) Info($"P3 L02 11 min both computers: blue's own fight met the condition, he came out at {on.EvilGuardOutTick / 100.0:0.00} s (the prints differ from then on, as they should)");
                else Check(po == pn, "P3 L02: both computers 11 min, the watch on and off: blue never held only its fort sector, the same world throughout");
            }
            foreach (var lv in Levels) { var w = Level(set, map, lv, 1, true, true); Check(!w.Objects.Any(o => o.Type == E), $"P4 {lv}: nobody of his type in the level at the start"); }
        }

        /// <summary>The whole check. Runs under OzV11Kit.GameSwitches.</summary>
        public static string Run(string settings, Func<string, string> map) => RunParts(settings, map, true);

        static string RunParts(string settings, Func<string, string> map, bool full)
        {
            pass = fail = 0; sb = new StringBuilder();
            bool was = ZWorld.EvilZodDeviation;
            ZWorld.EvilZodDeviation = true;
            try { S(settings, map); A(settings, map); L(settings, map); P(settings, map, full); }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " at " + (ex.StackTrace ?? "").Split('\n').FirstOrDefault()); }
            finally { ZWorld.EvilZodDeviation = was; }
            sb.Append("[OpenZ V1.3 EvilZod appear] ").Append(fail == 0 ? "PASS" : "FAIL").Append(' ').Append(pass).Append('/').Append(pass + fail).AppendLine();
            return sb.ToString();
        }

        /// <summary></summary>
        public static string Quick(string settings)
        {
            pass = fail = 0; sb = new StringBuilder();
            bool was = ZWorld.EvilZodDeviation; ZWorld.EvilZodDeviation = true;
            try
            {
                string plain = OzV11Kit.Plain(80, 120, ("building:fort_front", 30, 8, "blue"), ("building:fort_back", 30, 100, "red"));
                var w = ZWorld.Create(settings, plain, 1); w.NoGameEnd = true; w.Events.Clear(); w.EvilLastDefenseTeam = Team.Blue;   // no units on the plain map: the game would end at once
                var fort = w.EvilFort(Team.Blue); int outs = 0; long at = -1;
                while (w.TickIndex < Out + 3000) { w.Tick(ZConst.TICK); foreach (var e in w.Events) if (e.type == ZEventType.EvilGuardOut) { outs++; if (at < 0) at = w.TickIndex; } w.Events.Clear(); }
                var ez = Evils(w).FirstOrDefault();
                Check(at == Out && outs == 1 && ez != null && ez.EvilGuarding && D(ez.CenterX, ez.CenterY, ez.EvilGuardX, ez.EvilGuardY) <= 4 && SpotFaults(w, fort, ez.EvilGuardX, ez.EvilGuardY, ez) == "",
                      $"Q1 plain map, blue holds only its fort sector: out at {(at < 0 ? "never" : (at / 100.0).ToString("0.00") + " s")}, once, on his spot 30 s later");
                var w2 = ZWorld.Create(settings, plain, 1); var h = w2.AddEvilRetryHelp(Team.Blue);
                Check(h != null && !h.EvilGuarding && SpotFaults(w2, w2.EvilFort(Team.Blue), h.CenterX, h.CenterY, h) == "", "Q2 the retry help: one EvilZod at the blue fort, the player's own unit");
            }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message); }
            finally { ZWorld.EvilZodDeviation = was; }
            return sb.ToString();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("OpenZ/V1.3/EvilZod: Appearances Count (headless, DV165)")]
        static void Menu() => OzV11Kit.RunMenu("EvilZod: appearances count (DV165)", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText));
#endif
    }
}
