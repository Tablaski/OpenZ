using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit vehicle_off_flag, rule DV184 "a vehicle steps off the flag it captured" (Sim/ZObject.FlagStepOff.cs): the counted check, headless
    /// on the Sim alone, nothing saved: menu OpenZ/V1.5/Vehicle Off Flag: Count (headless, DV184). One line per fact, ending
    /// [OpenZ V1.5 vehicle off flag] PASS n/n. A plain 64 x 40 map with one neutral flag in its middle (OzV11Kit.Plain); red is the player,
    /// blue a CPU that gives no orders of its own; the check turns DV184 on (and the DV69 calm start off) and puts both back.
    ///  F1 space: each vehicle (jeep, light tank, medium tank, mech, heavy tank, APC, mobile missile) driven onto the flag by the player's flag
    ///     click: captures, comes to rest on it, rolls off FlagOffSettleTicks later (+-2) to a spot next to the flag (box clear of the flag,
    ///     at most 8 px from it, moved at most 48 px), done within 600 ticks, once (still there 3 s later, one step-off); approached from the
    ///     west, east, north and south (light tank) it rolls on ahead (facing kept); a vehicle standing on the flag with no order rolls off too;
    ///  F2 no space: the flag ringed by eight empty vehicles: stays (no step-off, not moved in 4 s);
    ///  F2b the spot straight ahead taken: it rolls to a side (never back);
    ///  F2c (review 9 Nov) the spot ahead box-free but inside a parked unit's pathing spacing (heavy / heavy, APC / heavy, mobile missile /
    ///     APC): the parked unit is never shoved, the roller goes to a side;
    ///  F3 other orders: queued waypoints past the flag (follows them, ends at the last), an ATTACK given after the capture (attacks, no
    ///     step-off), a patrol through the flag (patrols on), HOLD on the flag, the player's STOP on the flag (before the capture and after
    ///     it), a group move onto the flag, a new MOVE after the capture (follows it): no step-off in each;
    ///  F4 robots unchanged: a grunt squad captures the flag: no step-off; the same scene with DV184 on and off: the same fingerprint;
    ///  F5 CPU vehicles too: a blue light tank standing on the flag: captures it and rolls off;
    ///  F6 DV184 off ('Zod exact'): the F1 drive ends parked on the flag (Zod) for 5 s;
    ///  F7 determinism: the F1 light-tank scene twice: the same fingerprint and spot;
    ///  F8 the flag never blocks: a light tank and a grunt squad sent straight across the flag pass over it (their box on it) to their goal;
    ///     on every level map no flag is a destroyable obstacle or has blocking tiles, and the tile under each flag (counted) stays passable.
    /// </summary>
    public static class OzV15VehicleOffFlagCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);

        const Team Me = Team.Red, Cpu = Team.Blue;
        public static readonly string[] Types = { "jeep", "light", ZMTank.Type, ZMTank.Mech, "heavy", "apc", "missile_launcher" };
        static string Name(string t) => t == "light" ? "light tank" : t == ZMTank.Type ? "medium tank" : t == "heavy" ? "heavy tank" : t == ZMTank.Mech ? "mech"
                                       : t == "missile_launcher" ? "mobile missile" : t == "apc" ? "APC" : t;

        const int GroundW = 64, GroundH = 40, FlagTx = 32, FlagTy = 20;

        sealed class Scene
        {
            public ZWorld w; public ZObject flag; public ulong hash = 1469598103934665603UL;
            public int Fx => flag.CenterX; public int Fy => flag.CenterY;
            void Fold(ZEvent e) { hash = (hash ^ (ulong)(long)e.type) * 1099511628211UL; hash = (hash ^ (ulong)(long)e.objId) * 1099511628211UL; hash = (hash ^ (ulong)(long)e.otherId) * 1099511628211UL; }
            public void Ticks(long n) { for (long i = 0; i < n; i++) { w.Tick(ZConst.TICK); foreach (var e in w.Events) Fold(e); w.Events.Clear(); } }
            public ulong State() => w.RulesFingerprint(hash);
            public ZObject Vehicle(string type, int cx, int cy, Team t) => w.CreateObject("vehicle", type, cx - 16, cy - 16, t);
            public ZObject Robots(string type, int cx, int cy, Team t, int n) => w.CreateRobotGroup(type, cx - 8, cy - 8, t, n);
            /// <summary>The player's right-click on the flag (View/PlayerInput.cs BuildWaypoint: MOVE to the clicked point, refId = the flag).</summary>
            public ZWaypoint FlagClick() => ZWaypoint.Move(Fx, Fy, true, flag.Id);
            public bool Captured(Team t, long max = 3000) { for (long i = 0; i < max && flag.Owner != t; i++) Ticks(1); return flag.Owner == t; }
            public long Rest(ZObject v, long max = 3000) { long i = 0; for (; i < max && (v.Waypoints.Count > 0 || v.IsMoving); i++) Ticks(1); return i; }
            /// <summary>Ticks until the step-off MOVE appears (or max): its tick count, -1 none.</summary>
            public long UntilStep(ZObject v, long max)
            {
                for (long i = 1; i <= max; i++) { Ticks(1); if (v.Waypoints.Count > 0 && v.Waypoints[0].refId == ZPathing.RefStepAside) return i; }
                return -1;
            }
        }

        static string groundMap;
        static Scene New(string set, int seed = 1)
        {
            var w = ZWorld.Create(set, groundMap ??= OzV11Kit.Plain(GroundW, GroundH, ("map_item:flag", FlagTx, FlagTy, "null")), seed);
            w.NoGameEnd = true;
            w.Bots.Add(new ZBot(w, Cpu, seed + 101) { Ignored = true });
            w.Events.Clear();
            var s = new Scene { w = w, flag = w.Flags.FirstOrDefault() };
            s.Ticks(2);
            return s;
        }

        sealed class Hold : IDisposable
        {
            readonly bool on0 = ZWorld.FlagStepOffDeviation, calm = ZWorld.CalmStartDeviation;
            public Hold(bool on) { ZWorld.FlagStepOffDeviation = on; ZWorld.CalmStartDeviation = false; }
            public void Dispose() { ZWorld.FlagStepOffDeviation = on0; ZWorld.CalmStartDeviation = calm; }
        }

        static bool OnFlag(ZObject v, ZObject f) => v.IntersectsObject(f);
        /// <summary>Px between the vehicle box and the flag box (0 = touching or overlapping).</summary>
        static int Gap(ZObject v, ZObject f)
        {
            int gx = Math.Max(0, Math.Max(f.LocX - (v.LocX + v.WidthPix), v.LocX - (f.LocX + f.WidthPix)));
            int gy = Math.Max(0, Math.Max(f.LocY - (v.LocY + v.HeightPix), v.LocY - (f.LocY + f.HeightPix)));
            return Math.Max(gx, gy);
        }

        sealed class Drive { public bool captured, onFlagAtRest, off, near, once; public long step, done; public int ax, ay, mx, my, gap; public string why; }

        /// <summary>A red `type` from (dx, dy) * 200 px off the flag, the flag click, the capture, its rest, then up to 600 ticks for the step-off.</summary>
        static Drive DriveOn(Scene s, string type, int dx, int dy, Team t = Me)
        {
            var d = new Drive();
            var v = s.Vehicle(type, s.Fx + dx * 200, s.Fy + dy * 200, t);
            s.w.SendWaypoints(t, v, new[] { s.FlagClick() });
            d.captured = s.Captured(t);
            s.Rest(v);
            d.ax = v.CenterX; d.ay = v.CenterY; d.onFlagAtRest = OnFlag(v, s.flag);
            int n0 = ZWorld.FlagStepOffs;
            d.step = s.UntilStep(v, 600);
            d.done = d.step < 0 ? -1 : s.Rest(v, 600);
            d.mx = v.CenterX - d.ax; d.my = v.CenterY - d.ay; d.off = !OnFlag(v, s.flag); d.gap = Gap(v, s.flag); d.near = d.off && d.gap <= 8;
            int px = v.CenterX, py = v.CenterY;
            s.Ticks(300);
            d.once = v.CenterX == px && v.CenterY == py && v.Waypoints.Count == 0 && ZWorld.FlagStepOffs - n0 == 1;
            d.why = v.FlagStepOffWhy;
            return d;
        }

        static string Say(Drive d) => $"captured {d.captured}, at rest on the flag {d.onFlagAtRest} ({d.ax},{d.ay}), step-off MOVE after {d.step} ticks (rule {ZObject.FlagOffSettleTicks}), "
                                     + $"rolled ({d.mx},{d.my}) px in {d.done} more ticks, off the flag {d.off}, {d.gap} px from it, once {d.once} ({d.why})";
        static bool Good(Drive d) => d.captured && d.onFlagAtRest && d.step >= ZObject.FlagOffSettleTicks - 2 && d.step <= ZObject.FlagOffSettleTicks + 2 && d.done >= 0 && d.done <= 600
                                     && d.off && d.near && Math.Abs(d.mx) <= 48 && Math.Abs(d.my) <= 48 && d.once;

        static void F1(string set)
        {
            foreach (var type in Types)
            {
                var s = New(set);
                var d = DriveOn(s, type, -1, 0);
                Check(Good(d) && d.mx > 0, $"F1 {Name(type)} from the west onto the flag: {Say(d)}; rolled on ahead (east) {d.mx > 0}");
            }
            foreach (var (dx, dy, dir) in new[] { (1, 0, "east"), (0, -1, "north"), (0, 1, "south") })
            {
                var s = New(set);
                var d = DriveOn(s, "light", dx, dy);
                bool ahead = d.mx * -dx + d.my * -dy > 0;
                Check(Good(d) && ahead, $"F1 light tank from the {dir}: {Say(d)}; rolled on ahead {ahead}");
            }
            {
                var s = New(set);
                var v = s.Vehicle("light", s.Fx, s.Fy, Me);
                int n0 = ZWorld.FlagStepOffs;
                bool cap = s.Captured(Me);
                long st = s.UntilStep(v, 600); long done = st < 0 ? -1 : s.Rest(v, 600);
                Check(cap && st > 0 && done >= 0 && !OnFlag(v, s.flag) && Gap(v, s.flag) <= 8 && ZWorld.FlagStepOffs - n0 == 1,
                      $"F1 a light tank standing on the flag with no order: captured {cap}, rolled off after {st} + {done} ticks, off {!OnFlag(v, s.flag)}, {Gap(v, s.flag)} px from it ({v.FlagStepOffWhy})");
            }
        }

        static void F2(string set)
        {
            var s = New(set);
            var v = s.Vehicle("light", s.Fx, s.Fy, Me);
            foreach (var (dx, dy) in new[] { (36, 0), (36, 36), (0, 36), (-36, 36), (-36, 0), (-36, -36), (0, -36), (36, -36) })
                s.Vehicle("jeep", s.Fx + dx, s.Fy + dy, Team.Neutral);
            int n0 = ZWorld.FlagStepOffs, x0 = v.CenterX, y0 = v.CenterY;
            bool cap = s.Captured(Me);
            s.Ticks(400);
            Check(cap && ZWorld.FlagStepOffs == n0 && v.CenterX == x0 && v.CenterY == y0 && OnFlag(v, s.flag) && (v.FlagStepOffWhy ?? "").Contains("no free space"),
                  $"F2 the flag ringed by 8 empty jeeps: captured {cap}, stays on it (moved {v.CenterX - x0},{v.CenterY - y0}), no step-off {ZWorld.FlagStepOffs == n0} ({v.FlagStepOffWhy})");
        }

        static void F2b(string set)
        {
            var s = New(set);
            s.Vehicle("jeep", s.Fx + 44, s.Fy, Team.Neutral);                                             // the spot straight ahead taken
            var d = DriveOn(s, "light", -1, 0);
            bool side = d.mx >= 0 && Math.Abs(d.my) >= 20;
            Check(Good(d) && side, $"F2b the spot straight ahead taken by an empty jeep: {Say(d)}; rolled to the side, never back {side}");
        }

        /// <summary>Review 9 Nov: a parked unit just past the flag whose box is clear of the spot ahead but which stands inside the pathing
        /// layer's spacing (heavy / heavy 40 px, APC / heavy 41, mobile missile / APC 40; centres 34 px apart): the roll never shoves it
        /// (it stays exactly where it was) and ends off the flag to a side, nothing left in the list.</summary>
        static void F2c(string set)
        {
            var bad = new List<string>(); var said = new StringBuilder();
            foreach (var (a, b) in new[] { ("heavy", "heavy"), ("apc", "heavy"), ("missile_launcher", "apc") })
            {
                var s = New(set);
                var nb = s.Vehicle(b, s.Fx + 62, s.Fy, Me);
                int bx = nb.CenterX, by = nb.CenterY;
                var d = DriveOn(s, a, -1, 0);
                bool still = nb.CenterX == bx && nb.CenterY == by, side = d.mx >= 0 && Math.Abs(d.my) >= 20;
                if (!(Good(d) && still && side)) bad.Add(Name(a));
                said.Append($"{Name(a)} with a parked {Name(b)} 62 px past the flag: {Say(d)}; the {Name(b)} not moved {still} ({nb.CenterX - bx},{nb.CenterY - by}); to a side {side}. ");
            }
            Check(bad.Count == 0, "F2c the spot ahead box-free but inside a parked unit's spacing: " + said.ToString().TrimEnd() + (bad.Count > 0 ? " FAILED: " + string.Join(", ", bad) : ""));
        }

        static bool NoStep(Scene s, ZObject v, long ticks, int n0)
        {
            for (long i = 0; i < ticks; i++) { s.Ticks(1); if (v.Waypoints.Count > 0 && v.Waypoints[0].refId == ZPathing.RefStepAside) return false; }
            return ZWorld.FlagStepOffs == n0;
        }

        static void F3(string set)
        {
            {   // queued waypoints past the flag
                var s = New(set);
                var v = s.Vehicle("light", s.Fx - 200, s.Fy, Me);
                int n0 = ZWorld.FlagStepOffs;
                s.w.SendWaypoints(Me, v, new[] { s.FlagClick(), ZWaypoint.Move(s.Fx + 160, s.Fy + 64, true) });
                bool cap = s.Captured(Me); s.Rest(v);
                bool there = Math.Abs(v.CenterX - (s.Fx + 160)) <= 24 && Math.Abs(v.CenterY - (s.Fy + 64)) <= 24;
                bool none = NoStep(s, v, 400, n0);
                Check(cap && there && none, $"F3 queued waypoints past the flag: captured {cap}, followed them to the last ({v.CenterX},{v.CenterY}) {there}, no step-off {none} ({v.FlagStepOffWhy})");
            }
            {   // an ATTACK after the capture
                var s = New(set);
                var foe = OzV11Kit.Dummy(s.w, s.Fx + 180, s.Fy - 100);
                var v = s.Vehicle("light", s.Fx - 200, s.Fy, Me);
                s.w.SendWaypoints(Me, v, new[] { s.FlagClick() });
                bool cap = s.Captured(Me);
                int n0 = ZWorld.FlagStepOffs;
                OzV11Kit.PlayerAttack(s.w, v, foe);
                bool none = NoStep(s, v, 600, n0);
                bool attacks = v.AttackObject == foe || (v.Waypoints.Count > 0 && v.Waypoints[0].mode == WpMode.Attack && v.Waypoints[0].refId == foe.Id);
                Check(cap && none && attacks && v.FlagStepOffFlagId < 0, $"F3 an ATTACK given after the capture: captured {cap}, attacks its target {attacks}, no step-off {none}");
            }
            {   // a patrol through the flag
                var s = New(set);
                var v = s.Vehicle("light", s.Fx - 200, s.Fy, Me);
                int n0 = ZWorld.FlagStepOffs;
                bool sent = s.w.SendPatrol(Me, v, s.Fx, s.Fy);
                bool cap = s.Captured(Me);
                bool none = NoStep(s, v, 4000, n0);
                Check(sent && cap && none && v.Patrolling && v.PatrolLegs >= 2, $"F3 a patrol to the flag: captured {cap}, patrols on {v.Patrolling} ({v.PatrolLegs} legs), no step-off {none} ({v.FlagStepOffWhy})");
            }
            foreach (var hold in new[] { true, false })
            {   // HOLD / STOP on the flag before the capture
                var s = New(set);
                var v = s.Vehicle("light", s.Fx, s.Fy, Me);
                int n0 = ZWorld.FlagStepOffs, x0 = v.CenterX, y0 = v.CenterY;
                bool sent = hold ? s.w.SendHold(Me, v) : s.w.SendStop(Me, v);
                bool cap = s.Captured(Me);
                bool none = NoStep(s, v, 400, n0);
                Check(sent && cap && none && v.CenterX == x0 && v.CenterY == y0, $"F3 {(hold ? "HOLD" : "the player's STOP")} on the flag, then the capture: captured {cap}, stays (moved {v.CenterX - x0},{v.CenterY - y0}), no step-off {none} ({v.FlagStepOffWhy})");
            }
            {   // the player's STOP after the capture, while it rests
                var s = New(set);
                var v = s.Vehicle("light", s.Fx - 200, s.Fy, Me);
                s.w.SendWaypoints(Me, v, new[] { s.FlagClick() });
                bool cap = s.Captured(Me); s.Rest(v); s.Ticks(10);
                int n0 = ZWorld.FlagStepOffs, x0 = v.CenterX, y0 = v.CenterY;
                s.w.SendStop(Me, v);
                bool none = NoStep(s, v, 400, n0);
                Check(cap && none && v.CenterX == x0 && v.CenterY == y0 && OnFlag(v, s.flag), $"F3 the player's STOP after the capture (resting on the flag): stays {v.CenterX == x0 && v.CenterY == y0}, no step-off {none}");
            }
            {   // a group move onto the flag
                var s = New(set);
                var a = s.Vehicle("light", s.Fx - 200, s.Fy - 24, Me); var b = s.Vehicle("light", s.Fx - 200, s.Fy + 24, Me);
                int n0 = ZWorld.FlagStepOffs;
                s.w.SendWaypoints(Me, a, new[] { s.FlagClick() }); s.w.SendWaypoints(Me, b, new[] { s.FlagClick() });
                bool cap = s.Captured(Me); s.Rest(a); s.Rest(b);
                bool none = NoStep(s, a, 400, n0) && NoStep(s, b, 1, n0);
                string why = (a.FlagStepOffWhy ?? "") + " / " + (b.FlagStepOffWhy ?? "");
                Check(cap && none && why.Contains("group move"), $"F3 a group move (two light tanks, one click on the flag): captured {cap}, no step-off {none} ({why})");
            }
            {   // a new MOVE after the capture
                var s = New(set);
                var v = s.Vehicle("light", s.Fx - 200, s.Fy, Me);
                s.w.SendWaypoints(Me, v, new[] { s.FlagClick() });
                bool cap = s.Captured(Me);
                int n0 = ZWorld.FlagStepOffs;
                OzV11Kit.PlayerMove(s.w, v, s.Fx + 40, s.Fy + 140);
                s.Rest(v);
                bool there = Math.Abs(v.CenterX - (s.Fx + 40)) <= 24 && Math.Abs(v.CenterY - (s.Fy + 140)) <= 24;
                bool none = NoStep(s, v, 400, n0);
                Check(cap && there && none, $"F3 a new MOVE after the capture: followed it ({v.CenterX},{v.CenterY}) {there}, no step-off {none}");
            }
        }

        static (ulong fp, bool cap, bool none) RobotScene(string set, bool on)
        {
            using (new Hold(on))
            {
                var s = New(set);
                var g = s.Robots("grunt", s.Fx - 160, s.Fy, Me, 3);
                int n0 = ZWorld.FlagStepOffs;
                s.w.SendWaypoints(Me, g, new[] { s.FlagClick() });
                bool cap = s.Captured(Me);
                s.Ticks(800);
                return (s.State(), cap, ZWorld.FlagStepOffs == n0);
            }
        }

        static void F4(string set)
        {
            var on = RobotScene(set, true); var off = RobotScene(set, false);
            Check(on.cap && on.none && off.cap && on.fp == off.fp, $"F4 robots unchanged: a grunt squad captures the flag, no step-off {on.none}; DV184 on and off: the same fingerprint {on.fp == off.fp}");
        }

        static void F5(string set)
        {
            var s = New(set);
            var v = s.Vehicle("light", s.Fx, s.Fy, Cpu);
            int n0 = ZWorld.FlagStepOffs;
            bool cap = s.Captured(Cpu);
            long st = s.UntilStep(v, 600); long done = st < 0 ? -1 : s.Rest(v, 600);
            Check(cap && st > 0 && done >= 0 && !OnFlag(v, s.flag) && Gap(v, s.flag) <= 8 && ZWorld.FlagStepOffs - n0 == 1,
                  $"F5 a CPU (blue) light tank standing on the flag: captured {cap}, rolled off after {st} + {done} ticks, off {!OnFlag(v, s.flag)} ({v.FlagStepOffWhy})");
        }

        static void F6(string set)
        {
            using (new Hold(false))
            {
                var s = New(set);
                var v = s.Vehicle("light", s.Fx - 200, s.Fy, Me);
                int n0 = ZWorld.FlagStepOffs;
                s.w.SendWaypoints(Me, v, new[] { s.FlagClick() });
                bool cap = s.Captured(Me); s.Rest(v);
                bool none = NoStep(s, v, 500, n0);
                Check(cap && none && OnFlag(v, s.flag) && v.FlagStepOffWhy == null, $"F6 DV184 off ('Zod exact'): the light tank captures and stays parked on the flag {OnFlag(v, s.flag)}, no step-off {none}");
            }
        }

        static void F7(string set)
        {
            var r = new List<(ulong, int, int)>();
            for (int k = 0; k < 2; k++)
            {
                var s = New(set);
                var v = s.Vehicle("light", s.Fx - 200, s.Fy, Me);
                s.w.SendWaypoints(Me, v, new[] { s.FlagClick() });
                s.Ticks(1500);
                r.Add((s.State(), v.CenterX, v.CenterY));
            }
            Check(r[0].Equals(r[1]), $"F7 the same scene twice: the same fingerprint and spot ({r[0].Item2},{r[0].Item3}) / ({r[1].Item2},{r[1].Item3})");
        }

        static void F8(string set, Func<string, string> map)
        {
            {
                var s = New(set);
                var v = s.Vehicle("light", s.Fx - 160, s.Fy, Me);
                OzV11Kit.PlayerMove(s.w, v, s.Fx + 160, s.Fy, false);
                bool over = false; int dev = 0;
                for (int i = 0; i < 6000 && (v.Waypoints.Count > 0 || v.IsMoving || i < 5); i++) { s.Ticks(1); over |= OnFlag(v, s.flag); dev = Math.Max(dev, Math.Abs(v.CenterY - s.Fy)); }
                bool there = Math.Abs(v.CenterX - (s.Fx + 160)) <= 16 && Math.Abs(v.CenterY - s.Fy) <= 16;
                Check(over && there && dev <= 8, $"F8 a light tank sent straight across the flag: drove over it {over}, reached its goal {there}, never left the straight line by more than {dev} px");
                var g = s.Robots("grunt", s.Fx - 120, s.Fy + 80, Me, 3);
                OzV11Kit.PlayerMove(s.w, g, s.Fx + 120, s.Fy - 80, false);
                bool gOver = false;
                for (int i = 0; i < 6000 && (g.Waypoints.Count > 0 || g.IsMoving || i < 5); i++) { s.Ticks(1); gOver |= OnFlag(g, s.flag); }
                bool gThere = Math.Abs(g.CenterX - (s.Fx + 120)) <= 24 && Math.Abs(g.CenterY - (s.Fy - 80)) <= 24;
                Check(gOver && gThere, $"F8 a grunt squad's leader sent across the flag: walked over it {gOver}, reached its goal {gThere}");
            }
            if (map == null) { Info("F8 the level maps: no map loader given, skipped"); return; }
            int flags = 0, bad = 0, open = 0;
            foreach (var lv in new[] { "L02", "L07", "L16", "L17" })
            {
                string json; try { json = map(lv); } catch (Exception) { json = null; }
                if (string.IsNullOrEmpty(json)) { Info($"F8 {lv}: no map"); continue; }
                var w = ZWorld.Create(set, json, 1, lv == "L17");
                foreach (var f in w.Flags)
                {
                    flags++;
                    if (f.IsDestroyableImpass || (f.BlockTiles != null && f.BlockTiles.Length > 0)) bad++;
                    if (w.Path.TilePassable(f.LocX >> 4, f.LocY >> 4, true)) open++;
                }
            }
            Check(flags > 0 && bad == 0, $"F8 the level maps L02 L07 L16 L17: {flags} flags, none a destroyable obstacle or with blocking tiles ({bad}); the tile under {open} of them passable for robots (the map's own ground)");
        }

        public static string Run(string settings, Func<string, string> map, bool full = true)
        {
            pass = fail = 0; sb = new StringBuilder();
            var t0 = DateTime.UtcNow; int n0 = ZWorld.FlagStepOffs, s0 = ZWorld.FlagStepOffStays;
            try
            {
                using (new Hold(true))
                {
                    F1(settings); F2(settings); F2b(settings); F2c(settings); F3(settings); F4(settings); F5(settings); F6(settings); F7(settings); F8(settings, map);
                }
            }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace); }
            Info($"step-offs in this run {ZWorld.FlagStepOffs - n0}, captures that stayed {ZWorld.FlagStepOffStays - s0}; run time {(DateTime.UtcNow - t0).TotalSeconds:0.0} s (the check's own, not a rule input)");
            sb.AppendLine($"[OpenZ V1.5 vehicle off flag] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

        /// <summary>The Deviations row's probe: the light tank driven onto the flag rolls off; ringed by vehicles it stays; a queued move
        /// follows on; DV184 off: parked on the flag.</summary>
        public static string Quick(string settings)
        {
            pass = fail = 0; sb = new StringBuilder();
            try
            {
                using (new Hold(true))
                {
                    var s = New(settings);
                    var d = DriveOn(s, "light", -1, 0);
                    Check(Good(d), "light tank onto the flag: " + Say(d));
                    F2(settings);
                }
                F6(settings);
            }
            catch (Exception ex) { Check(false, "the probe threw " + ex.GetType().Name + ": " + ex.Message); }
            sb.AppendLine($"[OpenZ V1.5 vehicle off flag quick] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

#if UNITY_EDITOR && !OPENZ_HARNESS
        [UnityEditor.MenuItem("OpenZ/V1.5/Vehicle Off Flag: Count (headless, DV184)")]
        static void Menu() => OzV11Kit.RunMenu("vehicle off flag: count (DV184)",
            () => Run(OzV11Kit.SettingsText, lv => UnityEngine.Resources.Load<UnityEngine.TextAsset>("OpenZ/Maps/" + lv + "_grid")?.text));
#endif
    }
}
