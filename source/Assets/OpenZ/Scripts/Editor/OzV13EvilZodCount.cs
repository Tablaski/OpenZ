using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// OpenZ/V1.3/EvilZod: Rules Count (headless, DV161), and off-project on plain .NET (the kit's tools/harness). One line per fact, [OpenZ V1.3 EvilZod] PASS n/n.
    ///  R  his row, the damage by range, the shot cycle and the reload moment, one shot kills every robot close by;
    ///  D  the dash: when it fires, when it does not, where it stops, its ticks, the shot at the full number, the cooldown, a veteran's, its determinism;
    ///  H  the heal; C  never a driver, the shuttle in and out; N  he stands his ground; B  never built, in no list; P  levels the same with the switch on and off.
    /// </summary>
    public static class OzV13EvilZodCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);
        const string E = ZEvilZod.Type;

        sealed class Scene
        {
            public ZWorld w; public ZObject e;
            public readonly List<(long t, ZEvent ev)> log = new List<(long, ZEvent)>();
            public void Run(int ticks, Action each = null)
            {
                for (int i = 0; i < ticks; i++)
                {
                    w.Tick(ZConst.TICK);
                    foreach (var x in w.Events) if (x.type >= ZEventType.EvilDashStart) log.Add((w.TickIndex, x));
                    w.Events.Clear();
                    each?.Invoke();
                }
            }
            public int Count(ZEventType t) => log.Count(l => l.ev.type == t);
            public (long t, ZEvent ev) First(ZEventType t) => log.FirstOrDefault(l => l.ev.type == t);
            public List<(long t, ZEvent ev)> All(ZEventType t) => log.Where(l => l.ev.type == t).ToList();
        }

        static string Plain => OzV11Kit.Plain(200, 120);

        /// <summary>A plain map with painted tile classes (and lava) in the plain map's own form.</summary>
        static string Painted(Func<int, int, int> cls, Func<int, int, int> lava = null)
        {
            int w = 200, h = 120;
            var c = new int[w * h]; var l = new int[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { c[y * w + x] = cls(x, y); if (lava != null) l[y * w + x] = lava(x, y); }
            var s = new StringBuilder("{\"file\":\"v13_evil\",\"planet\":\"desert\",\"width\":" + w + ",\"height\":" + h + ",\"classes\":[");
            s.Append(string.Join(",", c)).Append("],\"tiles\":[").Append(string.Join(",", Enumerable.Repeat("0", w * h)));
            s.Append("],\"zones\":[{\"x\":0,\"y\":0,\"w\":" + w + ",\"h\":" + h + "}],\"objects\":[]");
            if (lava != null) s.Append(",\"lava\":[").Append(string.Join(",", l)).Append("]");
            return s.Append("}").ToString();
        }

        static Scene New(string set, string map, int cx = 400, int cy = 400, int seed = 1)
        {
            var w = ZWorld.Create(set, map, seed);
            w.NoGameEnd = true; w.Events.Clear();
            var e = OzV11Kit.Place(w, E, cx, cy, Team.Blue); e.EvilFaceTo(0);              // 1.4 DV174: facing east, where this check's targets stand (Sim/ZEvilZod.Face.cs)
            return new Scene { w = w, e = e };
        }

        /// <summary>A target that stands and does not fire back; bigHp: it survives every shot, so the HP removed can be read.</summary>
        static ZObject Target(Scene s, string type, int cx, int cy, bool bigHp = true, Team team = Team.Red)
        {
            var t = OzV11Kit.Place(s.w, type, cx, cy, team, ZBuildLists.IsRobot(type) ? 1 : -1);
            t.Damage = 0;
            if (bigHp) { t.MaxHealth = 10000000; t.Health = 10000000; }
            return t;
        }

        /// <summary>An attack nobody ordered (a help call's kind): the plain shot by range, no dash.</summary>
        static void Unordered(Scene s, ZObject t) => s.e.Waypoints.Add(new ZWaypoint { mode = WpMode.Attack, refId = t.Id, x = t.CenterX, y = t.CenterY });

        static double Dist(int ax, int ay, int bx, int by) => Math.Sqrt((double)(ax - bx) * (ax - bx) + (double)(ay - by) * (ay - by));

        // ------------------------------------------------------------------------------------------------ the row, the shot
        static void R(string set)
        {
            var s = New(set, Plain); var e = s.e; var S = s.w.S; var heavy = S.Unit("heavy"); var grunt = S.Unit("grunt");
            Check(e.IsEvilZod && e.Kind == ZKind.Robot && e.Minions.Count == 0 && e.WidthPix == 16 && e.HeightPix == 16 && ZPathing.Radius(e) == 9,
                  $"R1 one robot of his own type, a squad of one, flagged, footprint one tile, body radius {ZPathing.Radius(e)} px = 1.5 x a grunt's 6");
            Check(e.MaxHealth == heavy.MaxHealth && e.MaxHealth == 8378 && e.Health == e.MaxHealth, $"R1 hit points {e.MaxHealth} = the heavy tank's row");
            Check(e.AttackRadius == grunt.attack_radius && e.AttackRadius == 120, $"R1 shot range {e.AttackRadius} px = the grunt's row");
            Check(ZEvilZod.DashRangePx == S.Unit("howitzer").attack_radius && ZEvilZod.DashRangePx != e.AttackRadius, $"R1 dash range {ZEvilZod.DashRangePx} px = the howitzer's row, not his shot range");
            Check(e.MoveSpeed == heavy.move_speed && e.MoveSpeed == 9 && e.MoveSpeed < grunt.move_speed && e.MaxStamina == 0 && S.Unit(E).build_time == 0 && S.Unit(E).group_amount == 1,
                  $"R1 walk {e.MoveSpeed} px/s = the heavy tank's (a grunt {grunt.move_speed}), no running, no build time, squad 1");
            OzV11Kit.PlayerMove(s.w, e, 400 + 400, 400, false);
            s.Run(30); double x0 = e.CenterX; s.Run(1000); double v = (e.CenterX - x0) / 10.0; bool ran = e.IsRunning;
            Check(v > 9.8 && v <= 10.46 && !ran, $"R1 on a plain walk he covers {v:0.00} px/s (9 x {ZEvilZod.WalkFactor} = 10.35), never running");

            // the damage table, from a real shot at each distance
            foreach (var (d, rob, hull) in new[] { (64, 4500, 3000), (80, 3472, 2486), (96, 2443, 1972), (112, 1415, 1458), (120, 900, 1200) })
                foreach (var (type, want) in new[] { ("grunt", rob), ("heavy", hull) })
                {
                    var q = New(set, Plain);
                    var t = Target(q, type, 400 + d, 400);
                    Unordered(q, t);
                    q.Run(120);
                    var shot = q.First(ZEventType.EvilShot);
                    Check(q.Count(ZEventType.EvilShot) >= 1 && (int)shot.ev.value == want && shot.ev.radius == d && !shot.ev.flag && q.Count(ZEventType.EvilDashStart) == 0,
                          $"R3 {type} at {d} px: {(int)shot.ev.value} HP (table {want}), no dash");
                }
            Check(ZEvilZod.Hit(s.e, 10, 120, false) == 4500 && ZEvilZod.Hit(s.e, 100, 120, true) == 4500, "R3 a shot inside the full band, and a dash shot at any distance, takes the near number");

            // one shot kills any robot close by
            using (ZWorld.HeroBoostOn())
            {
                bool plus = ZWorld.HeroPlusDeviation; ZWorld.HeroPlusDeviation = true;
                try
                {
                    int top = 0; string who = "";
                    foreach (var r in ZSettings.Robots) if (S.Unit(r).MaxHealth > top) { top = S.Unit(r).MaxHealth; who = r; }
                    var q = New(set, Plain); var h = q.w.CreateHeroSquad(ZHeroSquads.AlanBrad, Team.Red, new[] { 60, 60 });
                    foreach (var m in OzV11Kit.Squad(h)) if (m.MaxHealth > top) { top = m.MaxHealth; who = m.HeroMemberName; }
                    Check(ZEvilZod.RobotNearHp >= top && top == 4476, $"R2 the near shot {ZEvilZod.RobotNearHp} HP is at or over the strongest robot's full health: {who} {top} HP");
                    int killed = 0;
                    foreach (var r in ZSettings.Robots)
                    {
                        var p = New(set, Plain); var t = Target(p, r, 400 + 60, 400, false);
                        OzV11Kit.PlayerAttack(p.w, p.e, t); p.Run(5);
                        if (t.Destroyed && p.All(ZEventType.EvilShot).Count(l => l.ev.flag2) == 1) killed++;
                    }
                    var a = New(set, Plain); var br = a.w.CreateHeroSquad(ZHeroSquads.AlanBrad, Team.Red, new[] { 28, 25 });
                    foreach (var m in OzV11Kit.Squad(br)) m.Damage = 0;
                    OzV11Kit.PlayerAttack(a.w, a.e, br); a.Run(5);
                    Check(killed == ZSettings.Robots.Length && br.Destroyed, $"R2 one shot at 60 px kills every robot type ({killed} of {ZSettings.Robots.Length}) and Brad with the heroes' boosts");
                }
                finally { ZWorld.HeroPlusDeviation = plus; }
            }
            {
                var q = New(set, Plain); var t = Target(q, E, 400 + 60, 400, false);
                t.Owner = Team.Red; OzV11Kit.PlayerAttack(q.w, q.e, t); q.Run(5);
                Check(!t.Destroyed && t.Health == 8378 - 4500, $"R2 another EvilZod lives through one shot ({t.Health} HP left)");
                var far = New(set, Plain); var g = Target(far, "grunt", 400 + 120, 400, false);
                Unordered(far, g); far.Run(5);
                Check(!g.Destroyed && g.Health == g.MaxHealth - 900, $"R3 less damage far away: a grunt at 15 m lives through one shot ({g.Health} of {g.MaxHealth} HP left)");
            }

            // the cycle, the reload, the kill credit
            {
                var q = New(set, Plain); var t = Target(q, "heavy", 400 + 80, 400);
                Unordered(q, t); q.Run(1000);
                var shots = q.All(ZEventType.EvilShot); var reloads = q.All(ZEventType.EvilReload);
                bool cycle = shots.Count >= 3 && shots[1].t - shots[0].t == ZEvilZod.CycleTicks && shots[2].t - shots[1].t == ZEvilZod.CycleTicks;
                bool reload = reloads.Count >= 3 && reloads.Count == shots.Count && reloads[0].t - shots[0].t == ZEvilZod.ReloadAfterTicks && reloads[1].t - shots[1].t == ZEvilZod.ReloadAfterTicks;
                Check(cycle, $"R4 shot to shot {(shots.Count > 1 ? shots[1].t - shots[0].t : 0)} ticks = {ZEvilZod.CycleTicks / 100}.{ZEvilZod.CycleTicks % 100:00} s");
                Check(cycle && shots[1].t - shots[0].t == 200 && reloads.Count > 0 && reloads[0].t - shots[0].t < shots[1].t - shots[0].t,
                      $"R6 row 82 (owner 3 Nov: he reloads too long): shot to shot {(shots.Count > 1 ? shots[1].t - shots[0].t : 0)} ticks = 2.00 s, was 300 = 3.00 s; the reload sound's event inside the cycle");
                Check(reload, $"R4 one reload event after every shot, {(reloads.Count > 0 ? reloads[0].t - shots[0].t : 0)} ticks later (the reload sound), {reloads.Count} of {shots.Count}");
                var k = New(set, Plain); int n = 0;
                for (int i = 0; i < 4; i++) { var g = Target(k, "grunt", 400 + 60, 400 + i * 10, false); OzV11Kit.PlayerAttack(k.w, k.e, g); k.Run(310); if (g.Destroyed) n++; }
                Check(n == 4 && k.e.Veteran && k.e.Kills == 4, $"R5 four kills make him a veteran as every crew ({k.e.Kills} kills, veteran {k.e.Veteran})");
            }
        }

        // ------------------------------------------------------------------------------------------------ the dash
        static void D1(string set)
        {
            var s = New(set, Plain); var t = Target(s, "grunt", 560, 400);
            OzV11Kit.PlayerAttack(s.w, s.e, t);
            int x0 = s.e.CenterX; bool still = true; double maxStep = 0, off = 0; int lastX = s.e.CenterX, lastY = s.e.CenterY;
            s.Run(30, () => { if (s.log.Count == 1 || s.e.EvilPhase == ZEvilPhase.Windup) still &= s.e.CenterX == x0; });
            var start = s.First(ZEventType.EvilDashStart); var go = s.First(ZEventType.EvilDashGo);
            lastX = s.e.CenterX; lastY = s.e.CenterY;
            s.Run(100, () =>
            {
                double step = Dist(s.e.CenterX, s.e.CenterY, lastX, lastY); lastX = s.e.CenterX; lastY = s.e.CenterY;
                maxStep = Math.Max(maxStep, step); off = Math.Max(off, Math.Abs(s.e.CenterY - 400));
            });
            var end = s.First(ZEventType.EvilDashEnd); var shot = s.First(ZEventType.EvilShot);
            int n = start.ev.radius;
            Check(s.Count(ZEventType.EvilDashStart) == 1 && start.t > 0 && start.t <= s.w.TickIndex && start.ev.otherId == t.Id, $"D1 an attack order at 160 px: the dash starts on the first tick ({start.t})");
            Check(go.t - start.t == ZEvilZod.WindupTicks && still, $"D1 windup {go.t - start.t} ticks, standing still");
            Check(end.t - go.t == n && n >= 22 && n <= 48 && end.ev.flag, $"D1 burst {end.t - go.t} ticks, straight: largest step {maxStep:0.0} px a tick, {off:0} px off the line");
            Check(maxStep <= 4.5 && off <= 1, "D1 no jump along the way");
            Check(shot.t - end.t == ZEvilZod.PlantTicks, $"D1 the plant {shot.t - end.t} ticks, then the shot");
            double stop = Dist(end.ev.x, end.ev.y, t.CenterX, t.CenterY);
            Check(Math.Abs(stop - 56) <= 1, $"D1 he stops {stop:0.0} px from the target (7 m): close, not too close");
            Check((int)shot.ev.value == 4500 && shot.ev.flag, $"D1 the dash shot removes {(int)shot.ev.value} HP, the maximum");
            Check(s.e.EvilDashReadyTick == start.t + 750 && s.e.EvilArrivedAt == end.t, $"D1 cooldown to tick {s.e.EvilDashReadyTick} (7.50 s after the start; 1.4: 15.00 s before)");
            double c = s.e.EvilDashCharge01;
            Check(c > 0 && c < 0.2, $"D1 the circle's fill right after the dash: {c:0.000}");
            int hullStop(string type) { var q = New(set, Plain); var h = Target(q, type, 570, 400); OzV11Kit.PlayerAttack(q.w, q.e, h); q.Run(100); var ev = q.First(ZEventType.EvilDashEnd); return (int)Math.Round(Dist(ev.ev.x, ev.ev.y, h.CenterX, h.CenterY)); }
            int hv = hullStop("heavy"), cr = hullStop("crane");
            Check(Math.Abs(hv - 60) <= 1 && Math.Abs(cr - 62) <= 1, $"D1 at a hull he stops by its size: heavy tank {hv} px, crane {cr} px");
        }

        static void D2(string set)
        {
            void Wall(string name, string map, Action<Scene> build)
            {
                var s = New(set, map); var t = Target(s, "grunt", 560, 400); build?.Invoke(s);
                OzV11Kit.PlayerAttack(s.w, s.e, t);
                int x0 = s.e.CenterX, y0 = s.e.CenterY;
                s.Run(120);
                bool kept = s.e.Waypoints.Count > 0 && s.e.Waypoints[0].mode == WpMode.Attack && s.e.Waypoints[0].refId == t.Id;
                double walked = Dist(s.e.CenterX, s.e.CenterY, x0, y0);
                Check(s.Count(ZEventType.EvilDashStart) == 0 && kept && walked > 3 && walked < 13.5, $"D2 {name} between: no dash, the order kept, he walks ({walked:0.0} px in 1.2 s)");
            }
            Wall("a cliff", Painted((x, y) => x == 30 && y >= 15 && y <= 35 ? (int)ZClass.Impassable : 0), null);
            Wall("a rock", Plain, s => s.w.CreateObject("map_item", "rock", 30 * 16, 23 * 16, Team.Neutral));
            Wall("a hut", Plain, s => s.w.CreateObject("map_item", "hut", 30 * 16, 24 * 16, Team.Neutral));
            Wall("a building", Plain, s => s.w.CreateObject("building", "radar", 29 * 16, 23 * 16, Team.Neutral));
        }

        static void D3(string set)
        {
            {
                var s = New(set, Plain); var t = Target(s, "grunt", 601, 400);
                OzV11Kit.PlayerAttack(s.w, s.e, t);
                s.Run(5); int early = s.Count(ZEventType.EvilDashStart);
                double d0 = -1;
                s.Run(400, () => { if (d0 < 0 && s.Count(ZEventType.EvilDashStart) > 0) d0 = Dist(s.e.CenterX, s.e.CenterY, t.CenterX, t.CenterY); });
                Check(early == 0 && s.Count(ZEventType.EvilDashStart) == 1 && d0 > 0 && d0 <= 200, $"D3 at 201 px no dash; he walks, and dashes once the target is in dash range ({d0:0.0} px)");
            }
            {
                var s = New(set, Plain); var t = Target(s, "grunt", 600, 400);
                OzV11Kit.PlayerAttack(s.w, s.e, t); s.Run(2);
                Check(s.Count(ZEventType.EvilDashStart) == 1, "D3 at 200 px, his dash range: the dash fires");
            }
            {
                var a = New(set, Plain); var u = Target(a, "grunt", 464, 400);
                OzV11Kit.PlayerAttack(a.w, a.e, u); a.Run(100);
                Check(a.Count(ZEventType.EvilDashStart) == 0 && a.Count(ZEventType.EvilShot) >= 1 && (int)a.First(ZEventType.EvilShot).ev.value == ZEvilZod.RobotNearHp, "D3 at 64 px, the full band, no dash: he stands and fires at the maximum");
            }
            foreach (var (d, type, full) in new[] { (65, "grunt", ZEvilZod.RobotNearHp), (100, "grunt", ZEvilZod.RobotNearHp), (119, "grunt", ZEvilZod.RobotNearHp), (120, "grunt", ZEvilZod.RobotNearHp), (121, "grunt", ZEvilZod.RobotNearHp), (100, "heavy", ZEvilZod.HullNearHp) })
            {   // inside his shot range and past the full band: the dash, and the first shot is the dash shot, none by the falloff before it
                var s = New(set, Plain); var t = Target(s, type, 400 + d, 400);
                OzV11Kit.PlayerAttack(s.w, s.e, t); s.Run(120);
                var end = s.First(ZEventType.EvilDashEnd); var shot = s.First(ZEventType.EvilShot);
                Check(s.Count(ZEventType.EvilDashStart) == 1 && s.Count(ZEventType.EvilShot) == 1 && shot.t == end.t + ZEvilZod.PlantTicks && shot.ev.flag && (int)shot.ev.value == full,
                      $"D3 {type} at {d} px: he dashes, stops at {Dist(end.ev.x, end.ev.y, t.CenterX, t.CenterY):0} px, the one shot takes {(int)shot.ev.value} HP");
            }
            {   // no room for even one sample: no dash, the cooldown stands, he fires by the range
                var s = New(set, Plain); var t = Target(s, "grunt", 470, 400); OzV11Kit.Place(s.w, "jeep", 418, 400, Team.Neutral);
                OzV11Kit.PlayerAttack(s.w, s.e, t); s.Run(30);
                var shot = s.First(ZEventType.EvilShot);
                Check(s.Count(ZEventType.EvilDashStart) == 0 && s.e.EvilDashReadyTick == 0 && s.Count(ZEventType.EvilShot) == 1 && !shot.ev.flag && (int)shot.ev.value == ZEvilZod.Hit(t, 70, 120, false),
                      $"D3 a hull right in front at 70 px: no dash on the spot, the cooldown not spent, the shot {(int)shot.ev.value} HP");
            }
            {   // the cooldown runs: he stands and fires by the range, and walks nowhere; the dash ready again goes before the next shot
                var s = New(set, Plain); var t1 = Target(s, "grunt", 560, 400, false);
                OzV11Kit.PlayerAttack(s.w, s.e, t1); s.Run(95);
                long ready = s.e.EvilDashReadyTick; int y0 = s.e.CenterY;
                var t2 = Target(s, "grunt", s.e.CenterX, y0 + 100);
                OzV11Kit.PlayerAttack(s.w, s.e, t2); s.Run(300);
                var fall = s.All(ZEventType.EvilShot).Last();
                Check(s.Count(ZEventType.EvilDashStart) == 1 && fall.ev.otherId == t2.Id && !fall.ev.flag && (int)fall.ev.value == ZEvilZod.Hit(t2, 100, 120, false) && s.e.CenterY == y0,
                      $"D3 cooldown running, a target at 100 px: he stands and fires {(int)fall.ev.value} HP");
                s.Run((int)(ready - s.w.TickIndex) + 400);
                var ds = s.All(ZEventType.EvilDashStart); var last = s.All(ZEventType.EvilShot).Last();
                Check(ds.Count == 2 && ds[1].t >= ready && last.ev.flag && (int)last.ev.value == ZEvilZod.RobotNearHp, $"D3 the cooldown over ({ready}): he dashes at tick {(ds.Count > 1 ? ds[1].t : -1)}, then {(int)last.ev.value} HP");
            }
        }

        static void D4(string set)
        {
            void No(string name, Action<Scene, ZObject> order)
            {
                var s = New(set, Plain); var t = Target(s, "grunt", 560, 400);
                order(s, t); s.Run(150);
                Check(s.Count(ZEventType.EvilDashStart) == 0, $"D4 {name}: no dash");
            }
            No("a plain move past an enemy", (s, t) => OzV11Kit.PlayerMove(s.w, s.e, 500, 500, false));
            No("an attack-move's own pick", (s, t) => OzV11Kit.PlayerMove(s.w, s.e, 560, 420, true));
            No("idle, an enemy in agro range", (s, t) => { });
            No("an attack nobody ordered (a help call's kind)", (s, t) => s.e.Waypoints.Add(new ZWaypoint { mode = WpMode.Attack, refId = t.Id, x = t.CenterX, y = t.CenterY }));
            {
                var s = New(set, Plain); var t = Target(s, "grunt", 560, 400); var j = Target(s, "jeep", 560, 460, true, Team.Neutral);
                OzV11Kit.PlayerAttack(s.w, s.e, j); s.Run(100);
                Check(s.Count(ZEventType.EvilDashStart) == 0, "D4 an attack order on an empty vehicle (Ctrl): no dash, it is no enemy");
            }
            {
                var s = New(set, Plain, 400, 560); s.e.Damage = 0;
                var ship = s.w.CreatePlayableShuttle(400, 400, Team.Red);
                s.w.SendWaypoints(Team.Red, ship, new[] { ZWaypoint.Move(1400, 1100, false) });
                bool ordered = false; int n = 0;
                s.Run(4000, () =>
                {
                    double d = Dist(s.e.CenterX, s.e.CenterY, ship.CenterX, ship.CenterY);
                    if (!ordered && ship.ShuttleAirborne && d > 121 && d < 199) { ordered = true; s.e.Damage = 4500; OzV11Kit.PlayerAttack(s.w, s.e, ship); }
                });
                n = s.Count(ZEventType.EvilDashStart);
                Check(ordered && n == 0, "D4 an attack order on a flying shuttle: no dash");
            }
            {
                var s = New(set, Plain); var t1 = Target(s, "grunt", 460, 400); var t2 = Target(s, "grunt", 400, 560);
                OzV11Kit.PlayerAttack(s.w, s.e, t1); s.Run(3);
                bool shot = s.Count(ZEventType.EvilShot) == 1; long at = s.First(ZEventType.EvilShot).t;
                OzV11Kit.PlayerAttack(s.w, s.e, t2); s.Run(ZEvilZod.CycleTicks - ZEvilZod.WindupTicks - 10);   // row 82: to just before the gun is ready for a dash (was 200 at a 300-tick cycle)
                int during = s.Count(ZEventType.EvilDashStart); s.Run(120);
                var st = s.First(ZEventType.EvilDashStart);
                Check(shot && during == 0 && s.Count(ZEventType.EvilDashStart) == 1 && st.t + ZEvilZod.WindupTicks >= at + ZEvilZod.CycleTicks, $"D5 the gun must be ready by the end of the windup: shot at {at}, dash at {st.t}");
            }
            {
                var s = New(set, Plain); var t = Target(s, "grunt", 560, 400);
                var el = OzV11Kit.Place(s.w, ZElectro.Type, 400, 540, Team.Red);
                s.Run(100);
                bool stunned = s.e.Stunned;
                OzV11Kit.PlayerAttack(s.w, s.e, t); s.Run(100);
                Check(stunned && s.Count(ZEventType.EvilDashStart) == 0 && s.e.CenterX == 400, "D4 stunned by an Electrobot: no dash");
            }
        }

        static void D5(string set)
        {
            foreach (bool vet in new[] { false, true })
            {
                var s = New(set, Plain); s.e.Veteran = vet;
                var t1 = Target(s, "grunt", 560, 400, false);
                OzV11Kit.PlayerAttack(s.w, s.e, t1);
                s.Run(2);
                var start = s.First(ZEventType.EvilDashStart);
                long ready = s.e.EvilDashReadyTick; int want = vet ? 650 : 750;                   // 1.4: the cooldown halved (1300 and 1500 before)
                s.Run(90);
                var land = (s.e.CenterX, s.e.CenterY);
                var t2 = Target(s, "grunt", land.Item1, land.Item2 + 170); var t3 = Target(s, "grunt", land.Item1, land.Item2 - 170);
                s.Run((int)(ready - 2 - s.w.TickIndex));
                OzV11Kit.PlayerAttack(s.w, s.e, t2); s.Run(1);
                bool one = s.Count(ZEventType.EvilDashStart) == 1 && s.w.TickIndex == ready - 1;
                OzV11Kit.PlayerAttack(s.w, s.e, t3); s.Run(1);
                var second = s.All(ZEventType.EvilDashStart);
                Check(ready - start.t == want && one && second.Count == 2 && second[1].t == ready, $"D5 {(vet ? "veteran" : "normal")}: cooldown {ready - start.t} ticks; an order one tick before it ends walks, the next tick it dashes");
                Check(s.Count(ZEventType.EvilDashReady) == 1 && s.First(ZEventType.EvilDashReady).t == ready, $"D5 {(vet ? "veteran" : "normal")}: the ready event on that tick");
            }
            {
                var s = New(set, Plain); var t1 = Target(s, "grunt", 560, 400, false);
                OzV11Kit.PlayerAttack(s.w, s.e, t1); s.Run(80 + ZEvilZod.PlantTicks);
                var t2 = Target(s, "grunt", s.e.CenterX, s.e.CenterY + 190);
                OzV11Kit.PlayerAttack(s.w, s.e, t2);
                long ready = s.e.EvilDashReadyTick;                                                // 1.4: the halved cooldown ends on this walk (at 15 s he reached shot range first)
                int y0 = s.e.CenterY; s.Run(30); double y1 = s.e.CenterY; s.Run(500);
                double v = (s.e.CenterY - y1) / 5.0;
                Check(s.Count(ZEventType.EvilDashStart) == 1 && v > 9.8 && v <= 10.46, $"D5 no dash while it cools down: he walks at {v:0.00} px/s");
                s.Run(400);
                var ds5 = s.All(ZEventType.EvilDashStart); var after5 = s.All(ZEventType.EvilShot).Where(x => x.t >= ready).ToList();
                Check(s.Count(ZEventType.EvilShot) >= 2 && ds5.Count == 2 && ds5[1].t >= ready && after5.Count >= 1 && after5[0].ev.flag && after5[0].ev.otherId == t2.Id,
                      $"D5 and when the cooldown ends on his walk (tick {ready}) the ready dash goes before the next shot: dash at {(ds5.Count > 1 ? ds5[1].t : -1)}, then the dash shot");
            }
        }

        static void D6(string set)
        {
            void No(string name, string map, Action<Scene> build)
            {
                var s = New(set, map); var t = Target(s, "grunt", 560, 400); build?.Invoke(s);
                OzV11Kit.PlayerAttack(s.w, s.e, t); s.Run(30);
                Check(s.Count(ZEventType.EvilDashStart) == 0, $"D6 {name}: no dash");
            }
            No("a parked vehicle on the line far from the target", Plain, s => OzV11Kit.Place(s.w, "jeep", 440, 400, Team.Neutral));
            No("a parked gun on the line", Plain, s => OzV11Kit.Place(s.w, "gun", 450, 400, Team.Neutral));
            No("deep water on the line", Painted((x, y) => x == 29 ? (int)ZClass.DeepWater : 0), null);
            No("a bridge rail on the line", Plain, s => s.w.CreateObject("building", "bridge_vert", 29 * 16, 22 * 16, Team.Neutral));
            No("lava on the line", Painted((x, y) => x == 29 ? (int)ZClass.Impassable : 0, (x, y) => x == 29 ? 2 : 0), null);
            {
                var s = New(set, Plain); var t = Target(s, "grunt", 560, 400);
                var mate = OzV11Kit.Place(s.w, "grunt", 450, 400, Team.Blue, 1); int hp = mate.Health;
                OzV11Kit.PlayerAttack(s.w, s.e, t); s.Run(100);
                Check(s.Count(ZEventType.EvilDashEnd) == 1 && s.First(ZEventType.EvilDashEnd).ev.flag && mate.Health == hp, "D6 a robot on the line does not stop him and is not hurt");
            }
            {
                var s = New(set, Plain); var t = Target(s, "grunt", 560, 400);
                var parked = OzV11Kit.Place(s.w, "jeep", 500, 400, Team.Neutral);
                OzV11Kit.PlayerAttack(s.w, s.e, t); s.Run(100);
                var end = s.First(ZEventType.EvilDashEnd);
                double gap = Dist(end.ev.x, end.ev.y, parked.CenterX, parked.CenterY), to = Dist(end.ev.x, end.ev.y, t.CenterX, t.CenterY);
                Check(s.Count(ZEventType.EvilDashStart) == 0 || (gap >= ZPathing.MinSpacing(s.e, parked) && to <= ZEvilZod.DashMaxLandPx), $"D6 a vehicle near the target: no dash, or a stop short of its hull inside the 80 px ({(s.Count(ZEventType.EvilDashStart) == 0 ? "no dash" : to.ToString("0") + " px")})");
            }
        }

        static void D7(string set)
        {
            {
                var s = New(set, Plain); var t = OzV11Kit.Place(s.w, "jeep", 575, 400, Team.Red); t.Damage = 0; t.MaxHealth = t.Health = 10000000;
                OzV11Kit.PlayerMove(s.w, t, 1500, 400, false);
                OzV11Kit.PlayerAttack(s.w, s.e, t); s.Run(200);
                var shot = s.First(ZEventType.EvilShot);
                Check(s.Count(ZEventType.EvilShot) == 1 && shot.ev.flag && (int)shot.ev.value == ZEvilZod.HullNearHp && shot.ev.radius > ZEvilZod.FullBandPx, $"D7 a jeep drives away in the dash: at {shot.ev.radius} px the shot is still the full {(int)shot.ev.value} HP");
            }
            {
                var s = New(set, Plain); var t = Target(s, "grunt", 560, 400, false);
                OzV11Kit.PlayerAttack(s.w, s.e, t); s.Run(5);
                t.SetHealth(0); s.w.UpdateObjectHealth(t); s.Run(3);
                Check(s.Count(ZEventType.EvilDashCancel) == 1 && s.Count(ZEventType.EvilDashGo) == 0 && s.e.EvilDashCharge01 == 1.0 && s.e.EvilDashReadyTick == 0 && s.e.CenterX == 400, "D7 target dead in the windup: cancelled, the cooldown given back");
            }
            {
                var s = New(set, Plain); var t = Target(s, "grunt", 560, 400);
                OzV11Kit.PlayerAttack(s.w, s.e, t); s.Run(5);
                OzV11Kit.PlayerMove(s.w, s.e, 300, 400, false); s.Run(3);
                Check(s.Count(ZEventType.EvilDashCancel) == 1 && s.Count(ZEventType.EvilDashGo) == 0 && s.e.EvilDashReadyTick == 0, "D7 a new order in the windup: cancelled, the cooldown given back");
            }
            {
                var s = New(set, Plain); var t = Target(s, "grunt", 560, 400);
                OzV11Kit.PlayerAttack(s.w, s.e, t); s.Run(30);
                bool burst = s.e.EvilPhase == ZEvilPhase.Burst; int hp0 = s.e.Health;
                s.e.DamageHealth(100); bool hurt = s.e.Health == hp0 - 100;
                s.e.SetHealth(0); s.w.UpdateObjectHealth(s.e);
                Check(burst && hurt && s.w.Events.Any(x => x.type == ZEventType.EvilDashCancel) && !s.e.EvilDashing, "D7 he can be hurt in the burst, and if he dies there the dash ends for the view");
            }
        }

        static string Print(ZWorld w) { var b = new StringBuilder(); foreach (var o in w.Objects) b.Append(o.Id).Append(':').Append(o.PosX).Append(',').Append(o.PosY).Append(',').Append(o.Health).Append(';'); return b.ToString(); }

        static void D8(string set)
        {
            string Run()
            {
                var s = New(set, Plain, seed: 7); var t = Target(s, "grunt", 560, 400); var g = Target(s, "heavy", 560, 500);
                OzV11Kit.PlayerAttack(s.w, s.e, t);
                var b = new StringBuilder(); s.Run(400, () => b.Append(s.e.CenterX).Append(',').Append(s.e.CenterY).Append(' '));
                return b.ToString() + "|" + Print(s.w) + "|" + s.w.Rng.Next(1000000);
            }
            Check(Run() == Run(), "D8 the same seed twice: the same ticks, the same positions, the same random stream");
            int Rng(bool dash)
            {
                var s = New(set, Plain, seed: 7); var t = Target(s, "grunt", 560, 400);
                if (dash) OzV11Kit.PlayerAttack(s.w, s.e, t);
                s.Run(400);
                if (dash && s.Count(ZEventType.EvilDashEnd) != 1) return -1;
                return s.w.Rng.Next(1000000);
            }
            int on = Rng(true), off = Rng(false);
            Check(on == off && on >= 0, "D8 a whole dash and shot draw no random number (the stream after it = the stream of a world where he stood idle)");
        }

        // ------------------------------------------------------------------------------------------------ the heal
        static void H(string set)
        {
            foreach (var (vet, want) in new[] { (false, 838), (true, 1005) })
            {
                var s = New(set, Plain); s.e.Veteran = vet; s.e.Health = s.e.MaxHealth / 2; int h0 = s.e.Health;
                s.Run(6000);
                int got = s.e.Health - h0, steps = s.Count(ZEventType.EvilHealStep);
                var st = s.All(ZEventType.EvilHealStep);
                int given = st.Sum(x => (int)x.ev.value);
                Check(Math.Abs(got - want) <= 1, $"H1 {(vet ? "veteran" : "normal")}: 60.00 s from half health: +{got} HP (10 % = 838, a veteran 12 % = 1005)");
                Check(steps >= 11 && steps <= 12 && st.Count > 1 && st[1].t - st[0].t == ZEvilZod.HealStepTicks && given >= got - (int)(got / 11.0) - 2, $"H1 {(vet ? "veteran" : "normal")}: a heal step every 5 s for the sound ({steps})");
            }
            {
                var s = New(set, Plain); s.e.Health = s.e.MaxHealth - 3; s.Run(200);
                int at = s.e.Health; s.log.Clear(); s.Run(1000);
                Check(at == s.e.MaxHealth && s.e.Health == s.e.MaxHealth && s.Count(ZEventType.EvilHealStep) == 0, "H1 never over full, and no heal step at full health");
            }
        }

        // ------------------------------------------------------------------------------------------------ vehicles, guns, the shuttle
        static void C(string set)
        {
            void Refused(string name, Func<Scene, ZObject> make)
            {
                var s = New(set, Plain); var t = make(s); s.e.Damage = 0;
                OzV11Kit.PlayerEnter(s.w, s.e, t);
                bool none = s.e.Waypoints.Count == 0; int x0 = s.e.CenterX;
                s.Run(200);
                Check(none && s.e.CenterX == x0 && !s.e.Destroyed && s.e.KillAt < 0 && t.Drivers.Count == (t.Owner == Team.Neutral ? 0 : 1), $"C1 ENTER {name}: refused, he stays");
            }
            Refused("an empty jeep", s => OzV11Kit.Place(s.w, "jeep", 440, 400, Team.Neutral));
            Refused("an empty heavy tank", s => OzV11Kit.Place(s.w, "heavy", 440, 400, Team.Neutral));
            Refused("an empty gun", s => OzV11Kit.Place(s.w, "gun", 440, 400, Team.Neutral));
            Refused("an empty howitzer", s => OzV11Kit.Place(s.w, "howitzer", 440, 400, Team.Neutral));
            Refused("an empty APC", s => OzV11Kit.Place(s.w, "apc", 440, 400, Team.Neutral));
            Refused("his own APC", s => OzV11Kit.Place(s.w, "apc", 440, 400, Team.Blue));
            Refused("his own tank", s => OzV11Kit.Place(s.w, "light", 440, 400, Team.Blue));
            Refused("an empty shuttle", s => s.w.CreatePlayableShuttle(600, 400, Team.Neutral));
            Refused("an enemy shuttle", s => s.w.CreatePlayableShuttle(600, 400, Team.Red));
            {
                var s = New(set, Plain); var r = OzV11Kit.Place(s.w, "grunt", 400, 450, Team.Blue, 1);
                var j = OzV11Kit.Place(s.w, "jeep", 440, 450, Team.Neutral);
                Check(j.CanTakePassengers(r) == false && r.EvilMayEnter(j) && !s.e.EvilMayEnter(j) && j.CanBeEntered(), "C1 another robot still may take that jeep: the rule is his alone");
            }
            {
                var s = New(set, Plain);
                var j = OzV11Kit.Place(s.w, "jeep", 430, 400, Team.Neutral); var g = OzV11Kit.Place(s.w, "gun", 380, 370, Team.Neutral);
                var ship = s.w.CreatePlayableShuttle(400, 300, Team.Blue);
                var (bx, by) = ship.ShuttleBackPoint(1);
                s.e.SetCords(bx - 8, by - 8);
                s.Run(6000);
                Check(j.Drivers.Count == 0 && g.Drivers.Count == 0 && ship.Passengers.Count == 0 && s.e.KillAt < 0 && s.e.NoAutoEnter && j.Owner == Team.Neutral, "C3 idle 60 s beside an empty vehicle, an empty gun and his own shuttle: he stays out");
            }
            Shuttle(set);
        }

        static ZObject Him(ZWorld w) => w.Objects.FirstOrDefault(o => o.IsEvilZod && o.KillAt < 0);

        static bool Board(Scene s, ZObject ship, double most = 60.0)
        {
            OzV11Kit.PlayerEnter(s.w, s.e, ship);
            int n = (int)(most / ZConst.TICK);
            for (int i = 0; i < n && ship.Passengers.Count == 0; i++) s.Run(1);
            return ship.Passengers.Count == 1;
        }

        static void Shuttle(string set)
        {
            {   // in and out on the ground
                var s = New(set, Plain);
                var ship = s.w.CreatePlayableShuttle(800, 500, Team.Blue);
                var (fx, fy) = ship.ShuttleBackPoint(1);
                s.e.SetCords(fx - 8, fy - 8 + 40);
                var d = Target(s, "grunt", fx + 400, fy + 40, false);
                s.e.Veteran = true; s.e.Kills = 6; s.e.Health = 5000; int pilotHp = ship.Drivers[0];
                bool walk = s.e.EvilMayEnter(ship) && ship.CanTakePassengers(s.e);
                long dashEnd = 0;
                {   // start a cooldown first: a dash at a target in range on the way
                    var t = Target(s, "grunt", s.e.CenterX + 160, s.e.CenterY);
                    OzV11Kit.PlayerAttack(s.w, s.e, t); s.Run(2); dashEnd = s.e.EvilDashReadyTick;
                    s.Run(90); t.SetHealth(0); s.w.UpdateObjectHealth(t); s.Run(2);
                }
                s.e.SetCords(fx - 8, fy - 8);                                                  // right at the ramp, the cooldown still running
                bool boarded = Board(s, ship);
                int hpIn = ship.Passengers.Count > 0 ? ship.Passengers[0] : -1;
                Check(walk && boarded && ship.PassengerType == E && ship.Drivers.Count == 1 && ship.DriverType == "grunt" && ship.Drivers[0] == pilotHp && s.e.KillAt >= 0, "C2 his own crewed shuttle: he boards as a passenger, the pilot stays the pilot");
                Check(ship.EvilAboard != null && ship.EvilAboard.veteran && ship.EvilAboard.kills == 6, "C2 his rank and cooldown ride with him");
                int ticks = 0; while (!ship.CanEjectCrew() && ticks++ < 6000) s.Run(1);
                s.Run(100);
                bool second = !ship.CanTakePassengers(ship.Owner);
                Check(second, "C2 a shuttle that carries him takes no other squad");
                bool out1 = s.w.Eject(Team.Blue, ship);
                ticks = 0; ZObject h = null; while (h == null && ticks++ < 2000) { s.Run(1); h = Him(s.w); }
                Check(out1 && h != null && h.Owner == Team.Blue && ship.Passengers.Count == 0 && ship.EvilAboard == null && ship.Drivers.Count == 1 && ship.Owner == Team.Blue, "C2 EXIT: he comes out, the ship and its pilot stay");
                s.Run(1);
                Check(h != null && h.Health >= hpIn && h.Health <= hpIn + 30 && h.Veteran && h.Kills == 6, $"C2 no heal aboard, the same hit points, rank and kills on the ground ({h?.Health} of {hpIn}, veteran {h?.Veteran}, {h?.Kills} kills)");
                Check(h != null && dashEnd > s.w.TickIndex && h.EvilDashReadyTick == dashEnd, $"C2 the cooldown rode with him and ticked on while he sat (ready at {h?.EvilDashReadyTick} of {dashEnd}, now {s.w.TickIndex})");
                Check(h != null && h.IsEvilZod && h.MaxHealth == 8378 && h.AttackRadius == 120 && h.MoveSpeed == 9, "C2 the same EvilZod comes out");
                bool enterAgain = h != null && h.EvilMayEnter(ship) && ship.CanTakePassengers(h);
                Check(enterAgain, "C2 and he can board again");
            }
            {   // EXIT in the air
                var s = New(set, Plain);
                var ship = s.w.CreatePlayableShuttle(800, 500, Team.Blue);
                var (fx, fy) = ship.ShuttleBackPoint(1);
                s.e.SetCords(fx - 8, fy - 8 + 40);
                bool boarded = Board(s, ship);
                int ticks = 0; while (!ship.ShuttleCrewSettled && ticks++ < 3000) s.Run(1);
                s.w.SendWaypoints(Team.Blue, ship, new[] { ZWaypoint.Move(1600, 900, false) });
                ticks = 0; while (!ship.ShuttleAirborne && ticks++ < 3000) s.Run(1);
                s.Run(300);
                bool air = ship.ShuttleAirborne;
                ticks = 0; while (!ship.CanEjectCrew() && ticks++ < 1000) s.Run(1);
                bool can = ship.CanEjectCrew(); bool go = s.w.Eject(Team.Blue, ship);
                ZObject h = null; ticks = 0; while (h == null && ticks++ < 6000) { s.Run(1); h = Him(s.w); }
                Check(boarded && air && can && go && h != null && !ship.ShuttleAirborne && ship.Drivers.Count == 1, $"C2 EXIT in the air: the ship lands, he comes out, the pilot stays (airborne {air}, may EXIT {can}, out {h != null})");
            }
            {   // the ship shot down while he walks in: saved, his rank with him
                var s = New(set, Plain);
                var ship = s.w.CreatePlayableShuttle(800, 500, Team.Blue);
                var (fx, fy) = ship.ShuttleBackPoint(1);
                s.e.SetCords(fx - 8, fy - 8 + 40);
                s.e.Veteran = true; s.e.Kills = 5;
                bool boarded = Board(s, ship);
                ship.SetHealth(0); s.w.UpdateObjectHealth(ship, -1);
                s.Run(1);
                var h = Him(s.w);
                Check(boarded && h != null && h.Veteran && h.Kills == 5, $"C2 the ship destroyed while he walks up the ramp: saved, as a passenger is, with his rank ({h != null})");
            }
            {   // a lone hero pilot's ship: EXIT lets everyone out, the ship goes empty
                var s = New(set, Plain);
                var ship = s.w.CreatePlayableShuttle(800, 500, Team.Neutral);
                var brad = s.w.CreateHeroSquad(ZHeroSquads.AlanBrad, Team.Blue, new[] { 40, 40 });
                foreach (var m in brad.Minions.ToList()) { m.SetHealth(0); s.w.UpdateObjectHealth(m); }
                s.Run(2);
                var (fx, fy) = ship.ShuttleBackPoint(1);
                brad.SetCords(fx - 8, fy - 8);
                OzV11Kit.PlayerEnter(s.w, brad, ship);
                int ticks = 0; while (ship.Drivers.Count == 0 && ticks++ < 3000) s.Run(1);
                ticks = 0; while (!ship.ShuttleCrewSettled && ticks++ < 3000) s.Run(1);
                s.e.SetCords(fx - 8, fy - 8 + 24); s.e.Veteran = true; s.e.Kills = 7;
                bool boarded = Board(s, ship);
                ticks = 0; while (!ship.CanEjectCrew() && ticks++ < 3000) s.Run(1);
                bool go = s.w.Eject(Team.Blue, ship);
                ZObject h = null; ticks = 0; while (h == null && ticks++ < 3000) { s.Run(1); h = Him(s.w); }
                Check(boarded && go && ship.ShuttleHeroPilot == false && h != null && h.Veteran && h.Kills == 7 && ship.EvilAboard == null, $"C2 a lone hero pilot's ship: EXIT lets him out with his rank (boarded {boarded}, out {h != null})");
            }
        }

        // ------------------------------------------------------------------------------------------------ he stands his ground
        static void N(string set)
        {
            {
                var s = New(set, Plain); var g = OzV11Kit.Place(s.w, "grunt", 600, 600, Team.Blue, 1); s.Run(1);
                int a = s.w.Rng.Next(1000000);
                var c = New(set, Plain); OzV11Kit.Place(c.w, "grunt", 600, 600, Team.Blue, 1); c.Run(1);
                c.w.Rng.Next(1000000);
                bool mine = s.e.DodgeMissile(410, 400, 2.0);
                bool other = g.DodgeMissile(610, 600, 2.0);
                Check(!mine && s.e.Waypoints.Count == 0 && other, "N1 a shell on its way: he never steps aside, a grunt does");
                var q = New(set, Plain); var r1 = new ZRng(9); int x = q.w.Rng.Next(1000000);
                var q2 = New(set, Plain); q2.e.DodgeMissile(410, 400, 2.0); int y = q2.w.Rng.Next(1000000);
                Check(x == y, "N1 his no draws nothing from the random stream");
            }
            {
                bool was = ZWorld.SniperHeadshotDeviation; int pct = ZWorld.HeadshotPct;
                ZWorld.SniperHeadshotDeviation = true; ZWorld.HeadshotPct = 100;
                try
                {
                    var s = New(set, Plain); s.e.Damage = 0;
                    var sn = OzV11Kit.Place(s.w, "sniper", 500, 400, Team.Red);
                    OzV11Kit.PlayerAttack(s.w, sn, s.e); s.Run(800);
                    var c = New(set, Plain); var g = Target(c, "tough", 460, 400, false, Team.Blue); c.e.Damage = 0; var sc = OzV11Kit.Place(c.w, "sniper", 500, 400, Team.Red);
                    OzV11Kit.PlayerAttack(c.w, sc, g); c.Run(800);
                    Check(!s.e.Destroyed && s.e.HeadshotBy < 0 && s.w.HeadshotRolls == 0 && s.e.Health < s.e.MaxHealth && c.w.HeadshotRolls > 0, $"N2 headshots at 100 %: none on him ({s.e.MaxHealth - s.e.Health} HP in plain hits); on a Tough the rule rolls ({c.w.HeadshotRolls})");
                }
                finally { ZWorld.SniperHeadshotDeviation = was; ZWorld.HeadshotPct = pct; }
            }
            {
                bool Run(string who, out int hits, out bool squashed, out bool pair)
                {
                    var s = New(set, Plain, 500, 400); s.e.Damage = 0;
                    var r = who == E ? s.e : Target(s, who, 500, 460, false, Team.Blue);
                    r.Damage = 0;
                    var j = OzV11Kit.Place(s.w, "jeep", 300, r.CenterY, Team.Red); j.Damage = 0; j.CanSnipe = false;
                    OzV11Kit.PlayerMove(s.w, j, 800, r.CenterY, false);
                    pair = false; for (int i = 0; i < 2000; i++) { s.Run(1); pair |= ZWorld.RunOverPair(j, r); }
                    hits = s.w.RunOverHits; squashed = r.Squashed;
                    return !r.Destroyed;
                }
                bool lives = Run(E, out int h1, out bool sq1, out bool p1); bool gl = Run("grunt", out int h2, out bool sq2, out bool p2);
                Check(lives && h1 == 0 && !sq1 && !p1 && (h2 > 0 || sq2), $"N2 a jeep drives at him: no run-over, no squash (on a grunt {h2} hits, squashed {sq2})");
            }
            {
                bool wasC = ZWorld.JeepChargeDeviation; ZWorld.JeepChargeDeviation = true;
                try
                {
                    bool Charged(string who)
                    {
                        var s = New(set, Plain, 500, 400); s.e.Damage = 0;
                        var r = who == E ? s.e : Target(s, who, 500, 460, false, Team.Blue);
                        var j = OzV11Kit.Place(s.w, "jeep", 500 + 32, r.CenterY, Team.Red);
                        OzV11Kit.PlayerAttack(s.w, j, r); bool on = false;
                        for (int i = 0; i < 300; i++) { s.Run(1); on |= j.ChargeOn; }
                        return on;
                    }
                    bool mine = Charged(E), other = Charged("tough");
                    Check(!mine && other, "N2 the jeep's charge never picks him; it does a Tough");
                }
                finally { ZWorld.JeepChargeDeviation = wasC; }
            }
            {
                double was = ZWorld.KnockChanceForTest; ZWorld.KnockChanceForTest = 1.0;
                try
                {
                    int Knocks(string who)
                    {
                        var s = New(set, Plain); s.e.Damage = 0;
                        var r = who == E ? s.e : Target(s, who, 460, 400, false, Team.Blue);
                        var tank = OzV11Kit.Place(s.w, "heavy", 800, 800, Team.Red);
                        s.w.AddMissileForTest(new ZMissile { x = r.CenterX, y = r.CenterY, damage = 100, radius = 40, team = Team.Red, attackerId = tank.Id, targetId = r.Id, explodeTime = s.w.Time });
                        s.Run(3);
                        return s.w.KnockCount;
                    }
                    int mine = Knocks(E), other = Knocks("tough");
                    Check(mine == 0 && other == 1, $"N2 a direct heavy blast never knocks him down (a Tough {other})");
                }
                finally { ZWorld.KnockChanceForTest = was; }
            }
            {
                var s = New(set, Plain);
                var box = s.w.CreateObject("map_item", "grenades", 26 * 16, 25 * 16, Team.Neutral);
                OzV11Kit.PlayerMove(s.w, s.e, box.CenterX, box.CenterY, false); s.Run(800);
                Check(s.e.Grenades == 0 && !s.e.CanPickupGrenades() && box.Grenades > 0, "N3 he walks over a grenade crate and takes nothing: a shotgun man");
                var q = New(set, Plain); var h = q.w.CreateObject("map_item", "rock", 26 * 16, 23 * 16, Team.Neutral);
                Check(!q.e.CanAttackObject(h), "N3 no explosives: no rock, hut or building is a target of his");
            }
        }

        // ------------------------------------------------------------------------------------------------ never built, levels unchanged
        static void B(string set)
        {
            int names = 0;
            foreach (bool on in new[] { false, true })
            {
                bool was = ZWorld.ElectrobotDeviation; ZWorld.ElectrobotDeviation = on;
                try { foreach (var b in new[] { "fort_front", "fort_back", "robot_factory", "vehicle_factory" }) for (int lv = 0; lv < 6; lv++) if (ZBuildLists.InList(b, lv, E)) names++; }
                finally { ZWorld.ElectrobotDeviation = was; }
            }
            Check(names == 0 && Array.IndexOf(ZSettings.Robots, E) < 0 && ZSettings.Robots.Length == 7, "B1 no build list of any building at any level names him; ZSettings.Robots stays the seven");
        }

        static void CpuSide(string set, Func<string, string> map)
        {
            var w = ZWorld.Create(set, map("L02"), 3);
            w.Bots.Add(new ZBot(w, Team.Red, 103)); w.Bots.Add(new ZBot(w, Team.Blue, 104));
            var fort = w.Objects.First(o => o.Owner == Team.Blue && (o.Type == "fort_front" || o.Type == "fort_back"));
            var e = w.CreateRobotGroup(E, fort.LocX + 80, fort.LocY + 220, Team.Blue);
            bool drove = false;
            for (int i = 0; i < 12000; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); if (e.KillAt >= 0 && !e.Destroyed) drove = true; }
            Check(!drove, $"B2 a tester drops him on a computer side on level 2: 120 s with both computers, no crash, never in a vehicle ({(e.Destroyed ? "he fell" : "he lives")})");
        }

        static ulong Fingerprint(ZWorld w)
        {
            ulong h = 1469598103934665603UL;
            void M(long v) { unchecked { h = (h ^ (ulong)v) * 1099511628211UL; } }
            foreach (var o in w.Objects) { M(o.Id); M(o.LocX); M(o.LocY); M(o.Health); M(o.KillAt < 0 ? 0 : 1); M(o.Waypoints.Count); M(o.AttackObject != null ? o.AttackObject.Id : 0); }
            M(w.Objects.Count); M(w.Rng.Next(1000000));
            return h;
        }

        /// <summary>One level world with both computers for sec seconds: its fingerprint every 10 s.</summary>
        public static string LevelPrint(string set, string map, int seed, int sec)
        {
            var w = ZWorld.Create(set, map, seed);
            w.Bots.Add(new ZBot(w, Team.Red, seed + 100)); w.Bots.Add(new ZBot(w, Team.Blue, seed + 101));
            var sb2 = new StringBuilder();
            for (int t = 1; t <= sec * 100; t++) { w.Tick(ZConst.TICK); w.Events.Clear(); if (t % 1000 == 0) sb2.Append(' ').Append(Fingerprint(w).ToString("x16")); }
            return sb2.ToString();
        }

        static void P(string set, Func<string, string> map)
        {
            foreach (var lv in new[] { "L02", "L16", "L17" })
            {
                bool was = ZWorld.EvilZodDeviation;
                try
                {
                    ZWorld.EvilZodDeviation = false; string off = LevelPrint(set, map(lv), 1, 60);
                    ZWorld.EvilZodDeviation = true; string on = LevelPrint(set, map(lv), 1, 60);
                    Check(off == on && off.Length > 0, $"P1 {lv}: 60 s with both computers, the object print the same with the switch off and on");
                    var w = ZWorld.Create(set, map(lv), 1);
                    Check(!w.Objects.Any(o => o.IsEvilZod || o.Type == E), $"P1 {lv}: nobody of his type in the level");
                }
                finally { ZWorld.EvilZodDeviation = was; }
            }
            string md5;
            using (var m = System.Security.Cryptography.MD5.Create()) md5 = string.Concat(m.ComputeHash(Encoding.UTF8.GetBytes(set)).Select(b => b.ToString("x2")));
            Check(md5 == "9e591c5135f3757afc6e9d925136a6d8", $"P1 default_settings.txt unchanged (MD5 {md5})");
            {
                bool was = ZWorld.EvilZodDeviation; ZWorld.EvilZodDeviation = false;
                try
                {
                    var w = ZWorld.Create(set, Plain, 1); var o = w.CreateRobotGroup(E, 400, 400, Team.Blue);
                    Check(!o.IsEvilZod && !o.NoAutoEnter && !o.EvilDashing, "P2 switch off: the flag stays false ('Zod exact': neither panel offers him)");
                }
                finally { ZWorld.EvilZodDeviation = was; }
            }
        }

        /// <summary>The whole check. Runs under OzV11Kit.GameSwitches.</summary>
        public static string Run(string settings, Func<string, string> map)
        {
            pass = fail = 0; sb = new StringBuilder();
            bool was = ZWorld.EvilZodDeviation, ship = ZWorld.ShuttlePlayableDeviation, on = ZWorld.ShuttleOn;
            ZWorld.EvilZodDeviation = true; ZWorld.ShuttlePlayableDeviation = true; ZWorld.ShuttleOn = true;
            try
            {
                R(settings); D1(settings); D2(settings); D3(settings); D4(settings); D5(settings); D6(settings); D7(settings); D8(settings);
                H(settings); C(settings); N(settings); B(settings); CpuSide(settings, map); P(settings, map);
            }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " at " + (ex.StackTrace ?? "").Split('\n').FirstOrDefault()); }
            finally { ZWorld.EvilZodDeviation = was; ZWorld.ShuttlePlayableDeviation = ship; ZWorld.ShuttleOn = on; }
            sb.Append("[OpenZ V1.3 EvilZod] ").Append(fail == 0 ? "PASS" : "FAIL").Append(' ').Append(pass).Append('/').Append(pass + fail).AppendLine();
            return sb.ToString();
        }

        /// <summary>The Deviations row's probe: his row and one dash on a plain map.</summary>
        public static string Quick(string settings)
        {
            pass = fail = 0; sb = new StringBuilder();
            bool was = ZWorld.EvilZodDeviation; ZWorld.EvilZodDeviation = true;
            try { D1(settings); }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message); }
            finally { ZWorld.EvilZodDeviation = was; }
            return sb.ToString();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("OpenZ/V1.3/EvilZod: Rules Count (headless, DV161)")]
        static void Menu() => OzV11Kit.RunMenu("EvilZod: rules count (DV161)", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText));
#endif
    }
}
