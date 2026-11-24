using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.4, EXIT DRIVER, rule DV171 (Sim/ZObject.VehicleExit.cs): the counted check, headless on the Sim alone, on a plain map and level 2, nothing
    /// saved: menu OpenZ/V1.4/Vehicle Exit: Count (headless, DV171), and off-project on the editor's Mono (the kit's tools/harness). One line per
    /// fact, [OpenZ V1.4 vehicle exit] PASS n/n. The check turns the rule's switch on as View/ZGame.cs does at a level start (a fresh editor has
    /// it off until a Play start) and the DV69 calm start off (the scenes act at once), on top of the counted checks' usual game switches, and
    /// puts both back. The scenes: a plain 64 x 40-tile map (OzV11Kit.Plain: no flag, no building, no map item), red is the player (no CPU),
    /// blue a CPU that gives no orders of its own, no game end; only the scene's own units are on it. The bot match (D): the real level 2.
    /// Fix round 3: L4 on level 17 (DV63's start crews), R10 on the real levels 2, 7, 16 and 17.
    ///  E  for the light tank, the medium tank, the heavy tank and the mech, with a grunt (3/4 health), a veteran grunt and Brad inside (each
    ///     got in by a player ENTER order): EXIT is taken, one Created and one Ejected event, the vehicle neutral, empty, stopped in the same
    ///     tick (also while it drove), its target and order gone; the robot is the same type, hit points, kills, rank and hero; his box is
    ///     beside the hull on free ground, on the first spot of the fixed order (S); the same run twice is the same;
    ///  L  idle red squads 2 to 4 tiles off, and the driver beside the hull: AutoEnterLocked at +1 and +2999 ticks, not at +3000; no entry
    ///     before 30.00 s; the first automatic entry comes after 30.00 s; the same with an idle blue CPU squad; fix round 3, L4: level 17 Easy's
    ///     start crews (DV63: an ENTER order of the fort's, playerGiven): the player takes the parked start tank whose crew comes last and lets
    ///     its driver out before that crew's turn: no start crew is sent to it and nobody climbs in during the 30 s; with the lock cleared by
    ///     hand a start crew is sent to it (the scene reaches that path); fix round 5, L5: a start crew already on its way (sent before the
    ///     player's grunt got in, EXIT 2 s after the capture: the review's case): its ENTER turns into DV59's walk and nobody climbs in during
    ///     the 30 s; the control with the fix switched off (ZWorld.DriverOutCrewWalkOn) walks in (the review's leak);
    ///     final fix, L6: a player's own order on a start crew is never touched: the crew (in the record) right-clicked by the player onto
    ///     another tank leaves the record, the EXIT on that tank keeps its ENTER and it climbs in; a start crew robot killed and those that got
    ///     into a vehicle are out of the record;
    ///  O  a red pyro squad ordered in (the player's right-click) at +2 s gets in at once and the lock ends; a blue CPU ENTER order gets in at once;
    ///  G  a red squad on the player's MOVE passing 3 tiles from the empty hull takes no grab on the way during the lock, a second one after it does;
    ///  H  fix round 2, rule DV132's hold for the driver: for each of the four, held as getting out for ZObject.DriverOutHoldTicks (held at +1
    ///     and +hold-1 ticks, free at +hold), a MOVE given at the EXIT waits (no step while held) and starts then; fix round 4 (the owner:
    ///     killable): an enemy grunt beside him picks him and hurts him while he is held, he fires at nobody while held, he fights back after
    ///     it; the hull destroyed while he is held: he is free in the next tick; DV132's switch off: no hold, he walks at once;
    ///  K  fix round 4 (the owner's O1-O5): K1 each of the four, a hurt driver killed by an enemy grunt WHILE HELD: one Death event, the kill
    ///     credited to that grunt, the vehicle neutral and empty, the 30 s lock unchanged, nobody in; K2 DV132's own robots (an APC's squad
    ///     let out) stay nobody's target; K3 each of the four, the hull destroyed in each phase of the climb (+0 the EXIT's tick, +1, out of the
    ///     hatch, on the deck, on the ladder, just landed, the last held tick) and one tick before the EXIT (refused): no exception, free in
    ///     the next tick, his hit points 6 s later the same as with DV132 off (what any robot standing there takes); K4 an ATTACK given at the
    ///     EXIT and an ENTER given 1 s after it are kept and done once he is out; K5 EXIT while the tank fires (it stops, no more shots), and a
    ///     second EXIT while the first driver is still held (a pyro ordered in meanwhile) is refused until he is free; fix round 5, K3b: K3's
    ///     phases with the hull's turret debris put 6 px from him: he is hurt exactly as the control (DV132 off) on the same spot;
    ///  K6 final fix (review r3: a vehicle ran over the held driver): K6a the light tank and the mech, a held tough, DV121 on, an enemy jeep 48 px
    ///     off: no charge, no knock, no squash while he is held, knocked once his hold ends, the control (DV132 off) knocked; K6b a held grunt,
    ///     an enemy jeep ordered onto his spot: on it while he is held, no knock, no squash, the control knocked;
    ///  U  the jeep, an empty APC and an APC with passengers, the missile launcher, the shuttle and every gun: EXIT with the rule's
    ///     switch on and off gives the same answer and the same state 5 s later (the guns keep DV58's 1500-tick lock and JustLeftCannon);
    ///  R  refused, nothing changed: a CPU team's tank, an empty tank, the switch off ('Zod exact': and the same state as with no EXIT at all),
    ///     a tank boxed in by hulls, by rocks, a tank on its factory exit move; fix round 1: a tank in a corridor two tiles wide between
    ///     one-tile walls, with and without tanks nose to tail (refused: a wall on every side of the hull; or the driver inside the corridor:
    ///     never behind a wall), a wall along the east flank (fix round 3: taken, the driver on the west side, the side with room) and one tile
    ///     off it (taken, the driver on the near side), a tank on a bridge deck (refused: on a bridge); fix round 3, R10: the real levels 2, 7,
    ///     16 and 17, a light tank on every free 2 x 2 tank position: at least 95 % can let the driver out, at least 95 % of those beside a flag
    ///     (every flag with a tank beside it that can), every accepted spot safe (robot-passable tiles, no building, no flag, off the hull, its
    ///     side of the hull free of terrain and buildings, a second-ring spot with nothing but units between it and the hull); a crewed red
    ///     light tank beside a flag and one beside a building really let out on each level (the robot on the spot found);
    ///  D  the same scene twice = the same ticks and events; a computer-against-computer match on level 2 with the rule's switch on and off:
    ///     the same fingerprint every 100 ticks, and nobody let a driver out.
    /// </summary>
    public static class OzV14VehicleExitCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);

        const Team Me = Team.Red, Cpu = Team.Blue;
        /// <summary>The four hatch vehicles (light tank, medium tank, heavy tank, mech).</summary>
        public static readonly string[] Types = { "light", ZMTank.Type, "heavy", ZMTank.Mech };
        static string Name(string t) => t == "light" ? "light tank" : t == ZMTank.Type ? "medium tank" : t == "heavy" ? "heavy tank" : t == ZMTank.Mech ? "mech" : t;
        const long Lock = ZObject.DriverOutLockTicks;

        // ------------------------------------------------------------------------------------------------ scenes
        sealed class Scene
        {
            public ZWorld w; public int ox, oy;                                                     // the open area's centre (px)
            public ulong hash = 1469598103934665603UL; public ZObject watch;
            public long firstIn = -1; public int firstBy = -1, entries;
            public void Fold(ZEvent e)
            {
                hash = (hash ^ (ulong)(long)e.type) * 1099511628211UL; hash = (hash ^ (ulong)(long)e.objId) * 1099511628211UL; hash = (hash ^ (ulong)(long)e.otherId) * 1099511628211UL;
                if (watch != null && e.type == ZEventType.Entered && e.otherId == watch.Id) { entries++; if (firstIn < 0) { firstIn = w.TickIndex; firstBy = e.objId; } }
            }
            /// <summary>The events now in the list (an order given between ticks), folded and cleared.</summary>
            public List<ZEvent> Take() { var l = new List<ZEvent>(w.Events); foreach (var e in l) Fold(e); w.Events.Clear(); return l; }
            public void Ticks(long n, Func<bool> stop = null)
            {
                for (long i = 0; i < n; i++)
                {
                    w.Tick(ZConst.TICK);
                    foreach (var e in w.Events) Fold(e);
                    w.Events.Clear();
                    if (stop != null && stop()) break;
                }
            }
            public void Run(double seconds, Func<bool> stop = null) => Ticks((long)Math.Round(seconds / ZConst.TICK), stop);
            public ulong State() => w.RulesFingerprint(hash);
            public ZObject Robot(string type, int cx, int cy, Team t, int amount = 1) => w.CreateRobotGroup(type, cx - 8, cy - 8, t, amount);
            public ZObject Vehicle(string type, int cx, int cy, Team t) => w.CreateObject(ZBuildLists.IsCannon(type) ? "cannon" : "vehicle", type, cx - 16, cy - 16, t);
            /// <summary>The robot gets in by an ENTER order of its team (the player's right-click, a CPU's capture order), at most `secs`.</summary>
            public bool Order(ZObject r, ZObject v, Team t, double secs = 20)
            {
                w.SendWaypoints(t, r, new[] { new ZWaypoint { mode = WpMode.Enter, refId = v.Id, x = v.CenterX, y = v.CenterY } });
                Run(secs, () => v.Owner == t && v.Drivers.Count > 0);
                return v.Owner == t && v.Drivers.Count > 0;
            }
            public void Remove(ZObject o) { if (o == null) return; o.KillAt = 0; foreach (var m in o.Minions) if (m != null) m.KillAt = 0; }
        }

        /// <summary>The scenes' ground: a plain 64 x 40-tile map (OzV11Kit.Plain: open ground, no flag, no building, no map item), so nothing but the
        /// scene's own units acts. The bot match (D) is on the real level 2.</summary>
        public const int GroundW = 64, GroundH = 40;
        static string groundMap;
        static Scene New(string set, int seed = 1)
        {
            var w = ZWorld.Create(set, groundMap ??= OzV11Kit.Plain(GroundW, GroundH), seed);
            w.NoGameEnd = true;
            w.Bots.Add(new ZBot(w, Cpu, seed + 101) { Ignored = true });
            w.Events.Clear();
            var s = new Scene { w = w, ox = GroundW * 8, oy = GroundH * 8 };
            s.Ticks(2);
            return s;
        }

        static IDisposable Rule(bool on = true) => new Hold(on);
        sealed class Hold : IDisposable
        {
            readonly bool ve = ZWorld.VehicleExitDeviation, calm = ZWorld.CalmStartDeviation, thrown = ZWorld.DriverThrownDeviation;   // 1.5 kit exit_leap_death: + DV179, held off (this check is DV171's 1.4 truth: K3 frees him at once)
            public Hold(bool on) { ZWorld.VehicleExitDeviation = on; ZWorld.CalmStartDeviation = false; ZWorld.DriverThrownDeviation = false; }       // as View/ZGame.cs at a start; the scenes act at once; DV179 off (Editor/OzV15ExitLeapDeathCount.cs counts it)
            public void Dispose() { ZWorld.VehicleExitDeviation = ve; ZWorld.CalmStartDeviation = calm; ZWorld.DriverThrownDeviation = thrown; }
        }

        static bool Has(List<ZEvent> l, ZEventType t, int a, int b) => l.Any(e => e.type == t && e.objId == a && e.otherId == b);
        static string Spot(ZObject v, ZObject r) => $"({r.CenterX - v.CenterX:+0;-0}, {r.CenterY - v.CenterY:+0;-0}) px from the hull's centre";

        /// <summary>A crewed red vehicle of `type` (its driver got in by the player's ENTER order) in the middle of the open area; crew: grunt
        /// (3/4 health), veteran (a grunt with 5 kills, promoted) or brad (Alan and Brad's squad: Brad drives); final fix: pyro or tough (one robot
        /// of that type, full health).</summary>
        static ZObject Crewed(Scene s, string type, string crew, out ZObject driver)
        {
            var v = s.Vehicle(type, s.ox, s.oy, Team.Neutral);
            if (crew == "brad") driver = s.w.CreateHeroSquad(ZHeroSquads.AlanBrad, Me, new[] { (s.ox - 64) / 16, (s.oy - 8) / 16, (s.ox - 64) / 16, (s.oy + 24) / 16 });
            else driver = s.Robot(crew == "pyro" || crew == "tough" ? crew : "grunt", s.ox - 64, s.oy, Me);
            if (crew == "grunt") driver.SetHealth(driver.MaxHealth * 3 / 4);
            if (crew == "veteran") { driver.Kills = 5; driver.Veteran = true; driver.PromotedAt = 0.5; }
            s.Order(driver, v, Me);
            return v;
        }

        // ------------------------------------------------------------------------------------------------ E
        static void E(string set)
        {
            foreach (var type in Types)
                foreach (var crew in new[] { "grunt", "veteran", "brad" })
                {
                    var s = New(set);
                    var v = Crewed(s, type, crew, out var d);
                    if (v.Owner != Me) { Check(false, $"E {Name(type)} / {crew}: the driver did NOT get in by the order"); continue; }
                    bool moving = crew == "grunt";
                    if (moving) { OzV11Kit.PlayerMove(s.w, v, v.CenterX + 320, v.CenterY, false); s.Run(1.2); }
                    bool wasMoving = v.IsMoving;
                    string dType = v.DriverType; int dHp = v.Drivers[0]; int kills = v.Kills; bool vet = v.Veteran; double at = v.PromotedAt;
                    int hx = v.CenterX, hy = v.CenterY; double px = v.PosX, py = v.PosY;
                    bool canNow = v.CanExitDriver(out var why, out int sx, out int sy);
                    int r1 = ZWorld.DriverExitRing1(type);
                    bool ok = s.w.Eject(Me, v);
                    var ev = s.Take();
                    var outs = ev.Where(e => e.type == ZEventType.Created && e.otherId == v.Id).Select(e => s.w.Get(e.objId)).Where(o => o != null).ToList();
                    var r = outs.FirstOrDefault();
                    bool events = outs.Count == 1 && r != null && Has(ev, ZEventType.Ejected, v.Id, r.Id) && ev.Count(e => e.type == ZEventType.Ejected) == 1;
                    bool empty = v.Owner == Team.Neutral && v.Drivers.Count == 0 && v.Waypoints.Count == 0 && v.AttackObject == null && !v.IsMoving;
                    s.Ticks(1);
                    double atEject = Math.Sqrt((v.PosX - px) * (v.PosX - px) + (v.PosY - py) * (v.PosY - py));
                    s.Ticks(99);
                    double moved = Math.Sqrt((v.PosX - px) * (v.PosX - px) + (v.PosY - py) * (v.PosY - py));
                    bool same = r != null && r.Type == dType && r.Health == dHp && r.Kills == kills && r.Veteran == vet && Math.Abs(r.PromotedAt - at) < 1e-9 && r.Owner == Me
                                && (crew != "brad" || (r.IsHero && r.HeroMemberName == "brad"));
                    bool beside = r != null && (Math.Abs(r.CenterX - hx) >= 24 || Math.Abs(r.CenterY - hy) >= 24);
                    bool first = r != null && r.CenterX == hx && r.CenterY == hy + r1 && sx == hx && sy == hy + r1;
                    bool free = r != null && !s.w.Path.WithinImpassable(r.CenterX - 8, r.CenterY - 8, 16, 16, out _, out _, true);
                    Check(canNow && ok && events && empty, $"E1 {Name(type)} with {(crew == "brad" ? "Brad" : crew == "veteran" ? "a veteran grunt" : "a grunt (3/4 health)")} inside{(moving ? ", driving" : "")}: EXIT taken {ok}, "
                          + $"Created + Ejected {(events ? "once each" : "WRONG")}, then neutral {v.Owner == Team.Neutral}, empty {v.Drivers.Count == 0}, no order {v.Waypoints.Count == 0}, no target {v.AttackObject == null}, moving {v.IsMoving}");
                    if (moving) Check(wasMoving && atEject < 0.5 && moved < 0.5, $"E2 {Name(type)} driving east at the EXIT (moving {wasMoving}): it stops in that tick (moved {atEject:0.00} px in the next tick, {moved:0.00} px in 1 s)");
                    Check(same, $"E3 {Name(type)} / {crew}: the driver comes out as he went in: {r?.Type ?? "nobody"} {r?.Health ?? 0} HP (in: {dType} {dHp} HP), kills {r?.Kills ?? -1} (in {kills}), veteran {r?.Veteran} (in {vet}), "
                          + $"promoted at {r?.PromotedAt ?? -9:0.00} (in {at:0.00}){(crew == "brad" ? $", hero {r?.HeroMemberName ?? "none"}" : "")}");
                    Check(beside && first && free, $"E4 {Name(type)} / {crew}: he stands {(r != null ? Spot(v, r) : "-")}, outside the 32 px hull box, on free ground {free}, on the first spot of the order (S, {r1} px: {first})");
                }
            // the same run twice
            ulong Twice(out string what)
            {
                var s = New(set);
                var v = Crewed(s, "light", "grunt", out _);
                s.Run(0.5); s.w.Eject(Me, v); s.Take(); s.Run(5);
                what = $"{s.w.TickIndex} ticks, {s.w.Objects.Count} objects";
                return s.State();
            }
            ulong a = Twice(out var wa), b = Twice(out var wb);
            Check(a == b, $"E5 the same EXIT scene twice: {a:X16} / {b:X16} ({wa})");
        }

        // ------------------------------------------------------------------------------------------------ L
        static void L(string set)
        {
            foreach (var type in Types)
            {
                var s = New(set);
                var v = Crewed(s, type, "veteran", out _);
                var sq = new[] { s.Robot("grunt", s.ox - 32, s.oy - 48, Me, 3), s.Robot("grunt", s.ox + 48, s.oy - 16, Me, 3), s.Robot("grunt", s.ox + 16, s.oy - 64, Me, 3) };
                s.Run(1);
                bool ok = s.w.Eject(Me, v); s.Take();
                long t0 = s.w.TickIndex; s.watch = v;
                s.Ticks(1); bool l1 = v.AutoEnterLocked && v.DriverOutLocked;
                s.Ticks(Lock - 2); bool l2999 = v.AutoEnterLocked; double left = v.DriverOutLockLeftSec; int inBefore = s.entries;
                s.Ticks(1); bool l3000 = v.AutoEnterLocked;
                s.Run(10, () => s.entries > 0);
                double tIn = s.firstIn < 0 ? -1 : (s.firstIn - t0) * ZConst.TICK;
                var by = s.firstBy >= 0 ? s.w.GetAny(s.firstBy) : null;
                Check(ok && l1 && l2999 && !l3000 && Math.Abs(left - 0.01) < 1e-9, $"L1 {Name(type)}: AutoEnterLocked at +1 tick {l1}, at +{Lock - 1} {l2999} (lock left {left:0.00} s), at +{Lock} {l3000} (the rule: {Lock} ticks = {Lock * ZConst.TICK:0.00} s)");
                Check(inBefore == 0 && tIn >= Lock * ZConst.TICK - 1e-9 && tIn <= 34.0, $"L2 {Name(type)}: the driver and 3 idle squads 2 to 4 tiles off: no entry before 30.00 s ({inBefore}); the first automatic entry {(tIn < 0 ? "none in 40 s" : $"{tIn:0.00} s after the EXIT")} by {(by == null ? "-" : by.Type + " #" + by.Id)}");
            }
            foreach (var type in new[] { "light", ZMTank.Mech })
            {
                var s = New(set);
                var v = Crewed(s, type, "grunt", out _);
                s.Run(0.5);
                s.w.Eject(Me, v); var ev = s.Take();
                foreach (var e in ev) if (e.type == ZEventType.Created) s.Remove(s.w.Get(e.objId));      // the driver walks off the map: only the CPU is near
                long t0 = s.w.TickIndex; s.watch = v;
                var blue = s.Robot("grunt", s.ox + 40, s.oy + 40, Cpu, 3);
                s.Ticks(Lock - 1); int inBefore = s.entries;
                s.Run(10, () => s.entries > 0);
                double tIn = s.firstIn < 0 ? -1 : (s.firstIn - t0) * ZConst.TICK;
                Check(inBefore == 0 && tIn >= Lock * ZConst.TICK - 1e-9 && tIn <= 34.0 && v.Owner == Cpu, $"L3 {Name(type)}: an idle blue CPU squad 2 tiles off: no entry before 30.00 s ({inBefore}); it climbs in by itself {(tIn < 0 ? "never in 40 s" : $"{tIn:0.00} s after the EXIT")} (now {v.Owner})");
            }
        }

        /// <summary>Fix round 3, L4: level 17 Easy's start crews (DV63, Sim/ZWorld.L17Crews.cs: one fort-made crew per parked start vehicle, nearest
        /// the red fort first, 2.5 s apart, each sent in with an ENTER order: playerGiven, which a lock never stops). The player's grunt takes the
        /// parked start tank whose crew comes last (an ENTER order), the player lets its driver out before that crew's turn and the driver is
        /// taken away. Locked: no start crew gets an ENTER order on it and nobody climbs in before the 30 s end. `clear`: the lock cleared by
        /// hand right after the EXIT (the control: a start crew is sent to it).</summary>
        static string L17Crews(string set, string l17, bool clear, out bool ordered, out bool inLock, out double turn, out double exitAt, out int boardOrders)
        {
            ordered = inLock = false; turn = exitAt = -1; boardOrders = 0;
            bool crews = ZWorld.L17CrewsDeviation, calm = ZWorld.CalmStartDeviation; var team = ZWorld.L17CrewsTeam;
            try
            {
                ZWorld.L17CrewsDeviation = true; ZWorld.L17CrewsTeam = Me; ZWorld.CalmStartDeviation = true;      // as View/ZGame.cs sets them on level 17 Easy (red the player)
                var w = ZWorld.Create(set, l17, 3, true);                                                          // DV32: the start vehicles parked, empty
                w.NoGameEnd = true; w.Bots.Add(new ZBot(w, Cpu, 104) { Ignored = true }); w.Events.Clear();
                var fort = w.Objects.FirstOrDefault(o => o.Kind == ZKind.Building && o.IsFort && o.Owner == Me && !o.Destroyed);
                if (fort == null) return "no red fort on level 17";
                long D(ZObject a) { long dx = a.CenterX - fort.CenterX, dy = a.CenterY - fort.CenterY; return dx * dx + dy * dy; }
                var parked = w.Objects.Where(o => o.Kind == ZKind.Vehicle && o.Owner == Team.Neutral && !o.Destroyed && o.Source != null && o.Source.owner == "red").ToList();
                parked.Sort((a, b) => { int c = D(a).CompareTo(D(b)); return c != 0 ? c : a.Id.CompareTo(b.Id); });   // the crews' order (L17Crews.cs InitL17Crews)
                int k = parked.FindLastIndex(o => o.IsHatchVehicle);
                if (k < 0) return "no parked red start tank on level 17";
                var x = parked[k]; turn = ZL17Balance.CrewFirstSeconds + k * ZL17Balance.CrewGapSeconds;
                ZObject g = null;
                for (int d = 0; d < 8 && g == null; d++)
                {
                    int cx = x.CenterX + ZWorld.DriverExitDx[d] * 40, cy = x.CenterY + ZWorld.DriverExitDy[d] * 40;
                    if (!w.Path.WithinImpassable(cx - 8, cy - 8, 16, 16, out _, out _, true)) g = w.CreateRobotGroup("grunt", cx - 8, cy - 8, Me, 1);
                }
                if (g == null) return "no free ground 40 px from the " + Name(x.Type);
                w.SendWaypoints(Me, g, new[] { new ZWaypoint { mode = WpMode.Enter, refId = x.Id, x = x.CenterX, y = x.CenterY } });   // the player's right-click
                var made = new HashSet<int>(); long exitTick = -1, firstIn = -1;
                for (int i = 0; i < 6000; i++)
                {
                    w.Tick(ZConst.TICK);
                    foreach (var e in w.Events)
                    {
                        if (e.type == ZEventType.Created && e.otherId == fort.Id) made.Add(e.objId);                         // a start crew out of the fort's door
                        if (exitTick >= 0 && e.type == ZEventType.Entered && e.otherId == x.Id && firstIn < 0) firstIn = w.TickIndex;
                    }
                    w.Events.Clear();
                    if (exitTick < 0 && x.Owner == Me && x.Drivers.Count > 0)
                    {
                        if (!w.Eject(Me, x)) return "EXIT DRIVER refused on the " + Name(x.Type);
                        exitTick = w.TickIndex; exitAt = w.Time;
                        foreach (var e in w.Events) if (e.type == ZEventType.Created) { var r = w.Get(e.objId); if (r != null) { r.KillAt = 0; foreach (var m in r.Minions) if (m != null) m.KillAt = 0; } }   // taken away: only a crew could climb in
                        w.Events.Clear();
                        if (clear) x.DriverOutLockUntilTick = -1;
                    }
                    if (exitTick < 0 && w.Time > 8.0) return "the grunt did not get in within 8 s";
                    if (exitTick >= 0 && !ordered)
                        foreach (var id in made) { var c = w.Get(id); if (c != null && c.Waypoints.Any(wp => wp.mode == WpMode.Enter && wp.refId == x.Id)) ordered = true; }
                    if (exitTick >= 0 && w.TickIndex >= exitTick + Lock) break;
                }
                inLock = firstIn >= 0 && firstIn - exitTick < Lock;
                boardOrders = w.L17CrewsBoardOrders[(int)Me];
                return $"{Name(x.Type)} #{x.Id}, number {k + 1} of {parked.Count} in the crews' order (its crew's turn at {turn:0.0} s); captured and let out at {exitAt:0.00} s";
            }
            finally { ZWorld.L17CrewsDeviation = crews; ZWorld.L17CrewsTeam = team; ZWorld.CalmStartDeviation = calm; }
        }

        static void L17(string set, string l17)
        {
            string a = L17Crews(set, l17, false, out bool ordA, out bool inA, out double turn, out double exitAt, out int boardA);
            L17Crews(set, l17, true, out bool ordB, out bool inB, out _, out _, out int boardB);
            Check(exitAt >= 0 && exitAt < turn && !ordA && !inA && ordB,
                  $"L4 level 17 Easy's start crews (DV63): the player takes the {a}; then a start crew sent to it: {ordA} (must be False), anybody in during the 30 s: {inA} (must be False); "
                  + $"crews' ENTER orders {boardA} (one per start vehicle still free at its turn); control, the lock cleared by hand right after the EXIT: a start crew sent to it: {ordB} (must be True), "
                  + $"in during the 30 s {inB}, orders {boardB}");
            // fix round 5, L5 (review: a start crew ALREADY on its way still walked in during the lock: its ENTER sat behind its walk out of the fort
            // door, and fix round 3's line only skips a vehicle for crews sent after the EXIT): the review's case, the first start tank in the crews'
            // order, its crew sent at its turn, the player's grunt in before that crew, EXIT 2 s after the capture; the control: the fix's switch off
            string c = L17EnRoute(set, l17, true, out bool sentC, out bool stillC, out bool inC, out double inAtC, out int walksC);
            L17EnRoute(set, l17, false, out bool sentD, out bool stillD, out bool inD, out double inAtD, out int walksD);
            Check(sentC && !stillC && !inC && walksC >= 1 && sentD && stillD && inD,
                  $"L5 level 17 Easy, a start crew already on its way: {c}; at the EXIT a crew's ENTER on it {sentC}; after the EXIT still an ENTER {stillC} (must be False; turned into DV59's walk: {walksC}), "
                  + $"anybody in during the 30 s {inC}{(inAtC >= 0 ? $" ({inAtC:0.00} s after the EXIT)" : "")} (must be False); control, the fix switched off (ZWorld.DriverOutCrewWalkOn = false, fix round 4): "
                  + $"the crew's ENTER kept {stillD}, in during the 30 s {inD}{(inAtD >= 0 ? $" ({inAtD:0.00} s after the EXIT)" : "")} (must be True: the review's leak)");
            // final fix, L6 (review r3: the record was never cleared, so an ENTER the PLAYER gave a former start crew was turned into a walk too)
            string p = L17PlayerOrder(set, l17, out bool notedBefore, out bool notedAfter, out bool exitOk, out bool kept, out int walksP, out double cInAt, out bool killedOut, out int gone, out int goneNoted);
            Check(notedBefore && !notedAfter && exitOk && kept && walksP == 0 && cInAt >= 0 && killedOut && gone > 0 && goneNoted == 0,
                  $"L6 level 17 Easy, a player's own order on a start crew is never touched: {p}; the crew in the start crews' record before the player's order {notedBefore} (so fix round 5's record would have turned "
                  + $"that ENTER into a walk at the EXIT), after it {notedAfter} (must be False); the EXIT on the tank it was sent to: taken {exitOk}, the crew's own ENTER kept {kept} (must be True), crews turned into a walk {walksP} "
                  + $"(must be 0); it climbs in {(cInAt >= 0 ? $"{cInAt:0.00} s after the EXIT" : "NOT within 30 s")} (the player's ENTER passes the lock); another start crew robot in the record killed: out of it {killedOut}; "
                  + $"start crew robots gone (got into a vehicle or died) {gone}, of them still in the record {goneNoted} (must be 0)");
        }

        /// <summary>Final fix, L6 (review r3): level 17 Easy seed 3, L5's set-up (the first start tank x in the crews' order, its crew c sent at its turn,
        /// the player's grunt in x before c gets there: c is in the start crews' record). 1 s after the capture the player right-clicks c onto an
        /// empty light tank y placed beside x on the side away from the fort (his own ENTER order), and his grunt b, next to y, too; b gets in
        /// first; the EXIT on y follows at once (b taken away). Out: c in the record before and after the player's order; the EXIT taken; c's own
        /// ENTER on y still in its orders right after it; crews turned into a walk by it; when c climbs into y (s after the EXIT, -1 not within
        /// 30 s); another start crew robot still in the record killed by the rules 6 s after the EXIT is out of it 2 ticks later; the scene runs to the lock's end; at the end the start crew robots
        /// gone from the world (got into a vehicle or died) and how many of those are still in the record.</summary>
        static string L17PlayerOrder(string set, string l17, out bool notedBefore, out bool notedAfter, out bool exitOk, out bool kept, out int walks, out double cInAt, out bool killedOut, out int gone, out int goneNoted)
        {
            notedBefore = notedAfter = exitOk = kept = killedOut = false; walks = 0; cInAt = -1; gone = goneNoted = 0;
            bool crews = ZWorld.L17CrewsDeviation, calm = ZWorld.CalmStartDeviation, wo = ZWorld.DriverOutCrewWalkOn; var team = ZWorld.L17CrewsTeam;
            try
            {
                ZWorld.L17CrewsDeviation = true; ZWorld.L17CrewsTeam = Me; ZWorld.CalmStartDeviation = true; ZWorld.DriverOutCrewWalkOn = true;
                var w = ZWorld.Create(set, l17, 3, true);
                w.NoGameEnd = true; w.Bots.Add(new ZBot(w, Cpu, 104) { Ignored = true }); w.Events.Clear();
                var fort = w.Objects.FirstOrDefault(o => o.Kind == ZKind.Building && o.IsFort && o.Owner == Me && !o.Destroyed);
                if (fort == null) return "no red fort on level 17";
                long D(ZObject a) { long dx = a.CenterX - fort.CenterX, dy = a.CenterY - fort.CenterY; return dx * dx + dy * dy; }
                var parked = w.Objects.Where(o => o.Kind == ZKind.Vehicle && o.Owner == Team.Neutral && !o.Destroyed && o.Source != null && o.Source.owner == "red").ToList();
                parked.Sort((a, b2) => { int k2 = D(a).CompareTo(D(b2)); return k2 != 0 ? k2 : a.Id.CompareTo(b2.Id); });
                int k = parked.FindIndex(o => o.IsHatchVehicle);
                if (k < 0) return "no parked red start tank on level 17";
                var x = parked[k]; double turn = ZL17Balance.CrewFirstSeconds + k * ZL17Balance.CrewGapSeconds;
                ZObject g = null, y = null;
                for (int d = 0; d < 8 && g == null; d++)
                {
                    int cx = x.CenterX + ZWorld.DriverExitDx[d] * 40, cy = x.CenterY + ZWorld.DriverExitDy[d] * 40;
                    if (!w.Path.WithinImpassable(cx - 8, cy - 8, 16, 16, out _, out _, true)) g = w.CreateRobotGroup("grunt", cx - 8, cy - 8, Me, 1);
                }
                if (g == null) return "no free ground 40 px from the " + Name(x.Type);
                long best = -1; int yx = -1, yy = -1;                                                                // y: free ground 80 or 112 px from x, as far from the fort as can be
                foreach (int dist in new[] { 80, 112 })
                    for (int d = 0; d < 8; d++)
                    {
                        int cx = x.CenterX + ZWorld.DriverExitDx[d] * dist, cy = x.CenterY + ZWorld.DriverExitDy[d] * dist;
                        if (cx < 48 || cy < 48 || cx > w.Path.WidthPix - 48 || cy > w.Path.HeightPix - 48 || w.Path.WithinImpassable(cx - 24, cy - 24, 48, 48, out _, out _, false)) continue;
                        if (w.Objects.Any(o => !o.Destroyed && (o.Kind == ZKind.Building || o.IsUnit) && Math.Abs(o.CenterX - cx) < 40 && Math.Abs(o.CenterY - cy) < 40)) continue;
                        long fd = (long)(cx - fort.CenterX) * (cx - fort.CenterX) + (long)(cy - fort.CenterY) * (cy - fort.CenterY);
                        if (fd > best) { best = fd; yx = cx; yy = cy; }
                    }
                if (best < 0) return "no free ground for a second tank beside the " + Name(x.Type);
                y = w.CreateObject("vehicle", "light", yx - 16, yy - 16, Team.Neutral);
                var made = new HashSet<int>(); ZObject c = null, b = null; long exitTick = -1; double capAt = -1, exitAt = -1; bool sent = false, ordered = false; int killedId = -1; long killedTick = -1;
                for (int i = 0; i < 9000; i++)
                {
                    if (!sent && w.Time + 1e-9 >= turn - 0.3) { w.SendWaypoints(Me, g, new[] { new ZWaypoint { mode = WpMode.Enter, refId = x.Id, x = x.CenterX, y = x.CenterY } }); sent = true; }   // L5's right-click: in just after the crew was sent
                    w.Tick(ZConst.TICK);
                    foreach (var e in w.Events)
                    {
                        if (e.type == ZEventType.Created && e.otherId == fort.Id) made.Add(e.objId);
                        if (exitTick >= 0 && c != null && e.type == ZEventType.Entered && e.objId == c.Id && e.otherId == y.Id && cInAt < 0) cInAt = (w.TickIndex - exitTick) * ZConst.TICK;
                    }
                    w.Events.Clear();
                    if (capAt < 0 && x.Owner == Me && x.Drivers.Count > 0) capAt = w.Time;
                    if (capAt < 0 && w.Time > turn + 8.0) return "the grunt did not get into the " + Name(x.Type) + " within 8 s of the crew's turn";
                    if (!ordered && capAt >= 0 && w.Time + 1e-9 >= capAt + 1.0)
                    {   // the player's own orders: the start crew c (on its way to x) right-clicked onto y, and his grunt b beside y onto it too
                        foreach (var id in made) { var o = w.Get(id); if (o != null && !o.IsMinion && o.Waypoints.Any(wp => wp.mode == WpMode.Enter && wp.refId == x.Id)) { c = o; break; } }
                        if (c == null) return "no start crew on its way to the " + Name(x.Type) + " 1 s after the capture";
                        notedBefore = w.L17CrewNoted(c.Id);
                        w.SendWaypoints(Me, c, new[] { new ZWaypoint { mode = WpMode.Enter, refId = y.Id, x = y.CenterX, y = y.CenterY } });
                        notedAfter = w.L17CrewNoted(c.Id) || c.Minions.Any(m => m != null && w.L17CrewNoted(m.Id));
                        b = w.CreateRobotGroup("grunt", yx + 24 - 8, yy - 8, Me, 1);
                        w.SendWaypoints(Me, b, new[] { new ZWaypoint { mode = WpMode.Enter, refId = y.Id, x = y.CenterX, y = y.CenterY } });
                        ordered = true;
                    }
                    if (ordered && exitTick < 0 && y.Owner == Me && y.Drivers.Count > 0)
                    {
                        if (OzV11Kit.Alive(c) == false) return "the start crew got into the light tank before the player's grunt";
                        int w0 = w.DriverOutCrewWalks;
                        exitOk = w.Eject(Me, y);
                        if (!exitOk) return "EXIT DRIVER refused on the light tank";
                        exitTick = w.TickIndex; exitAt = w.Time; walks = w.DriverOutCrewWalks - w0;
                        foreach (var e in w.Events) if (e.type == ZEventType.Created) { var r = w.Get(e.objId); if (r != null) { r.KillAt = 0; foreach (var m in r.Minions) if (m != null) m.KillAt = 0; } }   // taken away: only c could climb in
                        w.Events.Clear();
                        kept = c.Waypoints.Any(wp => wp.mode == WpMode.Enter && wp.refId == y.Id);
                    }
                    if (ordered && exitTick < 0 && w.Time > capAt + 12.0) return "the player's grunt did not get into the light tank within 11 s";
                    if (exitTick >= 0 && killedId < 0 && w.Time + 1e-9 >= exitAt + 6.0)
                    {   // another start crew robot still in the record: killed by the rules
                        foreach (var id in made.OrderBy(q => q)) { var o = w.Get(id); if (o != null && o != c && !c.Minions.Contains(o) && OzV11Kit.Alive(o) && w.L17CrewNoted(id)) { o.SetHealth(0); w.UpdateObjectHealth(o); killedId = id; killedTick = w.TickIndex; break; } }
                        if (killedId < 0) killedId = 0;                                                              // none left in the record (killedOut stays False)
                    }
                    if (killedId > 0 && killedTick >= 0 && w.TickIndex == killedTick + 2) killedOut = !w.L17CrewNoted(killedId);
                    if (exitTick >= 0 && w.TickIndex >= exitTick + Lock) break;
                }
                foreach (var id in made) if (w.Get(id) == null) { gone++; if (w.L17CrewNoted(id)) goneNoted++; }
                return $"{Name(x.Type)} #{x.Id} (its crew sent at {turn:0.00} s), the player's grunt in at {capAt:0.00} s; the start crew #{c?.Id} right-clicked onto the light tank #{y.Id} ({yx}, {yy}) px at {capAt + 1.0:0.00} s, "
                       + $"his grunt #{b?.Id} in first, EXIT at {exitAt:0.00} s; killed: #{killedId}";
            }
            finally { ZWorld.L17CrewsDeviation = crews; ZWorld.L17CrewsTeam = team; ZWorld.CalmStartDeviation = calm; ZWorld.DriverOutCrewWalkOn = wo; }
        }

        /// <summary>Fix round 5, L5: level 17 Easy's start crews; the first parked start tank (or mech) in the crews' order; the player's grunt beside it
        /// gets in by an ENTER order (the right-click) after that tank's start crew was sent (its ENTER waits behind its walk out of the door); EXIT
        /// 2 s after the capture, the driver taken away. `walkOn` = ZWorld.DriverOutCrewWalkOn. Out: at the EXIT a crew held an ENTER on it, after the
        /// EXIT one still does, somebody got in before the lock's end (and when, s after the EXIT), the crews turned into a walk.</summary>
        static string L17EnRoute(string set, string l17, bool walkOn, out bool sentBefore, out bool still, out bool inLock, out double inAt, out int walks)
        {
            sentBefore = still = inLock = false; inAt = -1; walks = 0;
            bool crews = ZWorld.L17CrewsDeviation, calm = ZWorld.CalmStartDeviation, wo = ZWorld.DriverOutCrewWalkOn; var team = ZWorld.L17CrewsTeam;
            try
            {
                ZWorld.L17CrewsDeviation = true; ZWorld.L17CrewsTeam = Me; ZWorld.CalmStartDeviation = true; ZWorld.DriverOutCrewWalkOn = walkOn;
                var w = ZWorld.Create(set, l17, 3, true);
                w.NoGameEnd = true; w.Bots.Add(new ZBot(w, Cpu, 104) { Ignored = true }); w.Events.Clear();
                var fort = w.Objects.FirstOrDefault(o => o.Kind == ZKind.Building && o.IsFort && o.Owner == Me && !o.Destroyed);
                if (fort == null) return "no red fort on level 17";
                long D(ZObject a) { long dx = a.CenterX - fort.CenterX, dy = a.CenterY - fort.CenterY; return dx * dx + dy * dy; }
                var parked = w.Objects.Where(o => o.Kind == ZKind.Vehicle && o.Owner == Team.Neutral && !o.Destroyed && o.Source != null && o.Source.owner == "red").ToList();
                parked.Sort((a, b) => { int k2 = D(a).CompareTo(D(b)); return k2 != 0 ? k2 : a.Id.CompareTo(b.Id); });
                int k = parked.FindIndex(o => o.IsHatchVehicle);
                if (k < 0) return "no parked red start tank on level 17";
                var x = parked[k]; double turn = ZL17Balance.CrewFirstSeconds + k * ZL17Balance.CrewGapSeconds;
                ZObject g = null;
                for (int d = 0; d < 8 && g == null; d++)
                {
                    int cx = x.CenterX + ZWorld.DriverExitDx[d] * 40, cy = x.CenterY + ZWorld.DriverExitDy[d] * 40;
                    if (!w.Path.WithinImpassable(cx - 8, cy - 8, 16, 16, out _, out _, true)) g = w.CreateRobotGroup("grunt", cx - 8, cy - 8, Me, 1);
                }
                if (g == null) return "no free ground 40 px from the " + Name(x.Type);
                var made = new HashSet<int>(); long exitTick = -1, firstIn = -1; double capAt = -1, exitAt = -1; bool sent = false;
                for (int i = 0; i < 8000; i++)
                {
                    if (!sent && w.Time + 1e-9 >= turn - 0.3) { w.SendWaypoints(Me, g, new[] { new ZWaypoint { mode = WpMode.Enter, refId = x.Id, x = x.CenterX, y = x.CenterY } }); sent = true; }   // the player's right-click, so that he gets in just after the crew was sent
                    w.Tick(ZConst.TICK);
                    foreach (var e in w.Events)
                    {
                        if (e.type == ZEventType.Created && e.otherId == fort.Id) made.Add(e.objId);
                        if (exitTick >= 0 && e.type == ZEventType.Entered && e.otherId == x.Id && firstIn < 0) firstIn = w.TickIndex;
                    }
                    w.Events.Clear();
                    if (capAt < 0 && x.Owner == Me && x.Drivers.Count > 0) capAt = w.Time;
                    if (exitTick < 0 && capAt >= 0 && w.Time + 1e-9 >= capAt + 2.0)
                    {
                        foreach (var id in made) { var c = w.Get(id); if (c != null && c.Waypoints.Any(wp => wp.mode == WpMode.Enter && wp.refId == x.Id)) sentBefore = true; }
                        int w0 = w.DriverOutCrewWalks;
                        if (!w.Eject(Me, x)) return "EXIT DRIVER refused on the " + Name(x.Type);
                        exitTick = w.TickIndex; exitAt = w.Time; walks = w.DriverOutCrewWalks - w0;
                        foreach (var e in w.Events) if (e.type == ZEventType.Created) { var r = w.Get(e.objId); if (r != null) { r.KillAt = 0; foreach (var m in r.Minions) if (m != null) m.KillAt = 0; } }   // taken away: only a crew could climb in
                        w.Events.Clear();
                        foreach (var id in made) { var c = w.Get(id); if (c != null && c.Waypoints.Any(wp => wp.mode == WpMode.Enter && wp.refId == x.Id)) still = true; }
                    }
                    if (capAt < 0 && w.Time > turn + 8.0) return "the grunt did not get in within 8 s of the crew's turn";
                    if (exitTick >= 0 && w.TickIndex >= exitTick + Lock) break;
                }
                inLock = firstIn >= 0 && firstIn - exitTick < Lock; inAt = firstIn >= 0 ? (firstIn - exitTick) * ZConst.TICK : -1;
                return $"{Name(x.Type)} #{x.Id}, number {k + 1} of {parked.Count} in the crews' order (its crew sent at {turn:0.00} s), the player's grunt in at {capAt:0.00} s, EXIT at {exitAt:0.00} s";
            }
            finally { ZWorld.L17CrewsDeviation = crews; ZWorld.L17CrewsTeam = team; ZWorld.CalmStartDeviation = calm; ZWorld.DriverOutCrewWalkOn = wo; }
        }

        // ------------------------------------------------------------------------------------------------ O
        static void O(string set)
        {
            foreach (var type in new[] { "light", ZMTank.Mech })
            {
                var s = New(set);
                var v = Crewed(s, type, "grunt", out _);
                s.Run(0.5); s.w.Eject(Me, v); s.Take(); long t0 = s.w.TickIndex; s.watch = v;
                s.Run(2);
                var pyro = s.Robot("pyro", s.ox - 48, s.oy + 48, Me, 1);
                bool inNow = s.Order(pyro, v, Me, 12);
                double tIn = s.firstIn < 0 ? -1 : (s.firstIn - t0) * ZConst.TICK;
                Check(inNow && v.DriverType == "pyro" && s.firstBy == pyro.Id && tIn > 0 && tIn < 10 && v.DriverOutLockUntilTick == -1 && !v.AutoEnterLocked,
                      $"O1 {Name(type)}: a red pyro ordered in (right-click) 2 s after the EXIT gets in {(tIn < 0 ? "NEVER" : $"{tIn:0.00} s after the EXIT")}; driver {v.DriverType}, the lock {v.DriverOutLockUntilTick} (ended)");
            }
            {
                var s = New(set);
                var v = Crewed(s, "heavy", "grunt", out _);
                s.Run(0.5); s.w.Eject(Me, v); var ev = s.Take();
                foreach (var e in ev) if (e.type == ZEventType.Created) s.Remove(s.w.Get(e.objId));
                long t0 = s.w.TickIndex; s.watch = v;
                var blue = s.Robot("grunt", s.ox + 64, s.oy + 48, Cpu, 3);
                s.Run(1);
                bool inNow = s.Order(blue, v, Cpu, 12);
                double tIn = s.firstIn < 0 ? -1 : (s.firstIn - t0) * ZConst.TICK;
                Check(inNow && tIn > 0 && tIn < 12, $"O2 heavy tank: a blue CPU ENTER order (the computer's capture order, SEND_WAYPOINTS) 1 s after the EXIT gets in {(tIn < 0 ? "NEVER" : $"{tIn:0.00} s after the EXIT")} (owner {v.Owner})");
            }
        }

        // ------------------------------------------------------------------------------------------------ G
        static void G(string set)
        {
            var s = New(set);
            var v = Crewed(s, "light", "grunt", out _);
            s.Run(0.5); s.w.Eject(Me, v); var ev = s.Take();
            foreach (var e in ev) if (e.type == ZEventType.Created) s.Remove(s.w.Get(e.objId));
            long t0 = s.w.TickIndex; s.watch = v;
            int g0 = ZWorld.GrabsOnTheWay;
            var a = s.Robot("grunt", s.ox - 128, s.oy + 48, Me, 3);
            OzV11Kit.PlayerMove(s.w, a, s.ox + 176, s.oy + 48);
            bool tookEnter = false;
            s.Run(16, () => { if (a.Waypoints.Count > 0 && a.Waypoints[0].mode == WpMode.Enter) tookEnter = true; return false; });
            int during = ZWorld.GrabsOnTheWay - g0;
            Check(!tookEnter && during == 0 && s.entries == 0 && v.Owner == Team.Neutral, $"G1 during the lock a red squad on the player's MOVE passes 3 tiles from the empty light tank: grabs on the way {during}, an ENTER taken {tookEnter}, entries {s.entries}");
            s.Remove(a);                                                                                    // before the lock ends: it would take the hull by itself from where it stops (DV84: 220 px)
            s.Ticks(Math.Max(0, t0 + Lock + 10 - s.w.TickIndex));
            int g1 = ZWorld.GrabsOnTheWay;
            var b = s.Robot("grunt", s.ox - 128, s.oy + 48, Me, 3);
            OzV11Kit.PlayerMove(s.w, b, s.ox + 176, s.oy + 48);
            s.Run(16, () => s.entries > 0);
            int after = ZWorld.GrabsOnTheWay - g1;
            Check(after >= 1 && s.entries == 1 && s.firstBy == b.Id, $"G2 after the lock a second squad on the same MOVE takes the grab on the way ({after}) and gets in (entries {s.entries}, by {(s.firstBy == b.Id ? "it" : "someone else")}) (DV84)");
        }

        // ------------------------------------------------------------------------------------------------ H (fix round 2)
        static ZObject OutOf(List<ZEvent> ev, Scene s, ZObject v) => ev.Where(e => e.type == ZEventType.Created && e.otherId == v.Id).Select(e => s.w.Get(e.objId)).FirstOrDefault(o => o != null);
        static double Moved(ZObject r, double x, double y) => Math.Sqrt((r.PosX - x) * (r.PosX - x) + (r.PosY - y) * (r.PosY - y));

        static void H(string set)
        {
            foreach (var type in Types)
            {
                var s = New(set);
                var v = Crewed(s, type, "grunt", out _);
                s.Run(0.5);
                double t0 = s.w.Time; int hold = ZObject.DriverOutHoldTicks(type);
                bool ok = s.w.Eject(Me, v); var r = OutOf(s.Take(), s, v);
                if (r == null) { Check(false, $"H1 {Name(type)}: nobody came out"); continue; }
                bool given = Math.Abs(r.ExitOutAt - (t0 + hold * ZConst.TICK)) < 1e-9 && r.DriverOutFromId == v.Id;
                OzV11Kit.PlayerMove(s.w, r, r.CenterX + 96, r.CenterY, false);                                // the player sends him off at once
                double x0 = r.PosX, y0 = r.PosY;
                s.Ticks(1); bool held1 = r.ExitWalking(s.w.Time);
                s.Ticks(hold - 2); bool heldLast = r.ExitWalking(s.w.Time); double whileHeld = Moved(r, x0, y0);
                s.Ticks(1); bool free = !r.ExitWalking(s.w.Time) && r.ExitOutAt < 0;
                s.Run(1.5); double after = Moved(r, x0, y0);
                Check(ok && given && held1 && heldLast && free && whileHeld < 1e-9 && after > 4,
                      $"H1 {Name(type)}: the driver is getting out (DV132's hold) for {hold} ticks ({hold * ZConst.TICK:0.00} s: his figure's way down at a human pace): out moment set {given}, "
                      + $"held at +1 tick {held1} and at +{hold - 1} {heldLast}, free at +{hold} {free}; a MOVE given at the EXIT: {whileHeld:0.00} px while held, {after:0.0} px in the 1.5 s after");
            }
            {   // fix round 4 (the owner: killable): a target while held, he fires at nobody while held; after it he fights back
                var s = New(set);
                var v = Crewed(s, "heavy", "grunt", out _);
                s.Run(0.5);
                s.w.Eject(Me, v); var r = OutOf(s.Take(), s, v);
                int hold = ZObject.DriverOutHoldTicks("heavy");
                var foe = s.Robot("grunt", r.CenterX + 40, r.CenterY + 8, Cpu, 1);                            // 2.5 tiles off: in range of each other
                int hp = r.Health; bool picked = false, fired = false; long pickedAt = -1, hurtAt = -1; long t0 = s.w.TickIndex;
                for (int i = 0; i < hold - 2; i++)
                {
                    s.Ticks(1);
                    if (foe.AttackObject == r && pickedAt < 0) { picked = true; pickedAt = s.w.TickIndex - t0; }
                    if (r.Health < hp && hurtAt < 0) hurtAt = s.w.TickIndex - t0;
                    if (r.AttackObject != null) fired = true;
                }
                bool heldAll = r.ExitWalking(s.w.Time) || r.Destroyed || r.KillAt >= 0;
                bool fight = false;
                s.Run(4, () => { fight = r.AttackObject == foe || foe.Health < foe.MaxHealth || r.Destroyed || r.KillAt >= 0; return fight; });
                Check(r != null && picked && hurtAt > 0 && !fired && heldAll && fight,
                      $"H2 heavy tank (fix round 4: killable): an enemy grunt 2.5 tiles from the driver while he is held: picks him {picked} (at +{pickedAt} ticks of {hold}), hurts him while held "
                      + $"{(hurtAt > 0 ? $"(first at +{hurtAt})" : "NOT")} ({hp} -> {r.Health} HP), he engages nobody while held {!fired}, still held {heldAll}; after the hold he fights back {fight}");
            }
            {   // the hull destroyed while he is held: free in the next tick
                var s = New(set);
                var v = Crewed(s, ZMTank.Mech, "grunt", out _);
                s.Run(0.5);
                s.w.Eject(Me, v); var r = OutOf(s.Take(), s, v);
                s.Run(0.75);                                                                                  // vehicle_exit_jump: halfway through the mech's 1.5 s hold (the jump)
                int cut0 = ZWorld.DriverHoldsCut; bool before = r.ExitWalking(s.w.Time);
                v.SetHealth(0); s.w.UpdateObjectHealth(v); s.Take();
                s.Ticks(1);
                bool free = !r.ExitWalking(s.w.Time) && r.ExitOutAt < 0;
                Check(before && free && ZWorld.DriverHoldsCut == cut0 + 1,
                      $"H3 the mech destroyed 0.75 s after the EXIT, its driver still held ({before}): free in the next tick {free} (holds cut {ZWorld.DriverHoldsCut - cut0}); the driver {(r.Destroyed || r.KillAt >= 0 ? "killed by the blast" : "alive")}");
            }
            {   // DV132's switch off: no hold
                bool was = ZWorld.ExitWalkHoldOn; ZWorld.ExitWalkHoldOn = false;
                try
                {
                    var s = New(set);
                    var v = Crewed(s, "light", "grunt", out _);
                    s.Run(0.5);
                    s.w.Eject(Me, v); var r = OutOf(s.Take(), s, v);
                    OzV11Kit.PlayerMove(s.w, r, r.CenterX + 96, r.CenterY, false);
                    double x0 = r.PosX, y0 = r.PosY;
                    s.Run(1);
                    Check(r != null && r.ExitOutAt < 0 && !r.ExitWalking(s.w.Time) && Moved(r, x0, y0) > 4, $"H4 DV132's switch off: the light tank's driver gets no hold (out moment {r?.ExitOutAt ?? -9}) and walks at once ({Moved(r, x0, y0):0.0} px in 1 s)");
                }
                finally { ZWorld.ExitWalkHoldOn = was; }
            }
        }

        // ------------------------------------------------------------------------------------------------ K (fix round 4, the owner's O1-O5)
        /// <summary>
        /// The rules' part of it.</summary>
        static void K(string set)
        {
            // K1: killed while held (each of the four): the kill, its credit, the vehicle and the lock afterwards
            foreach (var type in Types)
            {
                var s = New(set);
                var v = Crewed(s, type, "grunt", out _);
                s.Run(0.5);
                bool ok = s.w.Eject(Me, v); var r = OutOf(s.Take(), s, v);
                if (r == null) { Check(false, $"K1 {Name(type)}: nobody came out"); continue; }
                r.SetHealth(1);                                                                               // vehicle_exit_jump: a driver at 1 HP (the hold is the 1.3-1.5 s jump now): the first hit kills him, within the hold
                long lockEnd = v.DriverOutLockUntilTick, t0 = s.w.TickIndex; int hold = ZObject.DriverOutHoldTicks(type), cut0 = ZWorld.DriverHoldsCut; s.watch = v;
                var foe = s.Robot("grunt", r.CenterX + 40, r.CenterY + 8, Cpu, 1); int kills0 = foe.Kills;
                long diedAt = -1; bool heldBefore = false, held = true; var deaths = new List<ZEvent>(); string threw = null;
                try
                {
                    for (int i = 0; i < hold + 50 && diedAt < 0; i++)
                    {
                        held = r.ExitWalking(s.w.Time);
                        s.w.Tick(ZConst.TICK);
                        foreach (var e in s.w.Events) { s.Fold(e); if (e.type == ZEventType.Death && e.objId == r.Id) deaths.Add(e); }
                        s.w.Events.Clear();
                        if (r.Destroyed || r.KillAt >= 0) { diedAt = s.w.TickIndex - t0; heldBefore = held; }
                    }
                    s.Run(2);
                }
                catch (Exception ex) { threw = ex.GetType().Name + ": " + ex.Message; }
                bool credit = deaths.Count == 1 && deaths[0].otherId == foe.Id && foe.Kills == kills0 + 1;
                bool vehicle = v.Owner == Team.Neutral && v.Drivers.Count == 0 && v.DriverOutLockUntilTick == lockEnd && v.DriverOutLocked && s.entries == 0;
                Check(ok && threw == null && diedAt > 0 && diedAt < hold && heldBefore && credit && vehicle && ZWorld.DriverHoldsCut == cut0,
                      $"K1 {Name(type)}: a driver at 1 HP and an enemy grunt 2.5 tiles off: he dies WHILE HELD {heldBefore} at +{diedAt} ticks (hold {hold}); Death events {deaths.Count}, "
                      + $"killer {(deaths.Count > 0 ? deaths[0].otherId : -1)} (the grunt {foe.Id}), its kills {kills0} -> {foe.Kills}; then the vehicle {v.Owner} with {v.Drivers.Count} driver(s), "
                      + $"locked {v.DriverOutLocked} (until tick {v.DriverOutLockUntilTick}, set {lockEnd}), entries {s.entries}; no exception {threw == null}{(threw != null ? " (" + threw + ")" : "")}");
            }
            // K2: DV132's own robots stay nobody's target: an APC's passengers let out (DV15) beside an enemy grunt
            {
                var s = New(set);
                var apc = s.Vehicle("apc", s.ox, s.oy, Me);
                var p = s.Robot("grunt", s.ox - 48, s.oy, Me, 3); OzV11Kit.PlayerEnter(s.w, p, apc);
                s.Run(15, () => apc.Passengers.Count > 0);
                bool inA = apc.Passengers.Count > 0;
                bool ok = s.w.Eject(Me, apc); var ev = s.Take();
                var outs = ev.Where(e => e.type == ZEventType.Created && e.otherId == apc.Id).Select(e => s.w.Get(e.objId)).Where(o => o != null).ToList();
                var m = outs.FirstOrDefault();
                var foe = m != null ? s.Robot("grunt", m.CenterX + 40, m.CenterY + 8, Cpu, 1) : null;
                bool held = m != null && m.ExitWalking(s.w.Time), can = false;
                for (int i = 0; i < 40 && m != null; i++) { s.Ticks(1); if (m.ExitWalking(s.w.Time) && (foe.CanAttackObject(m) || foe.AttackObject == m)) can = true; }
                Check(inA && ok && m != null && held && !can && !m.DriverOutTarget,
                      $"K2 DV132's own hold unchanged: a squad let out of an APC (DV15) is held ({held}) and an enemy grunt beside it cannot pick one while it is held (picked {can}; DriverOutTarget {m?.DriverOutTarget})");
            }
            // K3: the hull killed while its driver climbs out, in each phase (and one tick before the EXIT): the hold ends in the next tick,
            // no exception, and he takes what any robot standing there takes (the control: the same scene with DV132 off, nobody held)
            foreach (var type in Types)
            {
                int hold = ZObject.DriverOutHoldTicks(type);
                var phases = new[] { (-1, "one tick before the EXIT"), (0, "in the EXIT's own tick (lid / commander)"), (1, "1 tick after"), (25, "the lid opening"), (35, "on the rim, crouch-springing"),
                                     (hold / 2, "in the air"), (hold - 20, "just before the touch"), (hold - 1, "the last held tick") };      // vehicle_exit_jump: the phases of the jump
                int good = 0; var bad = new List<string>(); var hps = new List<string>();
                foreach (var (off, what) in phases)
                {
                    string res = HullKilled(set, type, off, true, -1, out int hpA, out _, out bool cutOk, out bool refusedOk, out string infoA);
                    if (off < 0)
                    {
                        if (res == null && refusedOk) good++; else bad.Add($"{what}: {res ?? infoA}");
                        continue;
                    }
                    string resB = HullKilled(set, type, off, false, -1, out int hpB, out _, out _, out _, out string infoB);
                    bool same = res == null && resB == null && hpA == hpB;
                    if (same && cutOk) good++; else bad.Add($"{what} (+{off}): {res ?? "ok"}, hold cut next tick {cutOk}, HP {hpA} (control {hpB}) {infoA}");
                    hps.Add($"+{off} {hpA}/{hpB}");
                }
                Check(bad.Count == 0, $"K3 {Name(type)}: the hull destroyed {phases.Length - 1} times while its driver is held (+0 .. +{hold - 1} ticks) and once one tick before the EXIT: "
                      + $"{good} of {phases.Length} as the rules say (the EXIT refused before; else free in the next tick, no exception, his hit points 6 s later the control's: the same scene "
                      + $"with DV132 off, i.e. what any robot standing there takes: Zod's turret debris, one neutral 40 px / 40 HP blast 3-4 s later, vlight.cpp:261-278; HP held / control: {string.Join(", ", hps)}){(bad.Count > 0 ? "; WRONG: " + string.Join(" | ", bad) : "")}");
            }
            // K3b, fix round 5 (review: K3 proved no damage at all, the debris never came near him): the same phases with the hull's turret debris
            // (Zod's one neutral 40 px / 40 HP blast 3-4 s after the hull's death, ZWorld.DeathDebris) put 6 px from him: he is hurt, exactly as
            // the control (DV132 off: nobody held) standing on the same spot
            foreach (var type in Types)
            {
                int hold = ZObject.DriverOutHoldTicks(type);
                var phases = new[] { 0, 1, 25, 35, hold / 2, hold - 20, hold - 1 };                                // vehicle_exit_jump: the phases of the jump
                var bad = new List<string>(); var hps = new List<string>(); int good = 0;
                foreach (int off in phases)
                {
                    string res = HullKilled(set, type, off, true, 6, out int hpA, out int thenA, out bool cutOk, out _, out string infoA);
                    string resB = HullKilled(set, type, off, false, 6, out int hpB, out int thenB, out _, out _, out string infoB);
                    bool ok = res == null && resB == null && cutOk && hpA == hpB && thenA == thenB && hpA < thenA;
                    if (ok) good++; else bad.Add($"+{off}: {res ?? "ok"} / {resB ?? "ok"}, cut {cutOk}, HP {thenA} -> {hpA} (control {thenB} -> {hpB}) {infoA}");
                    hps.Add($"+{off} {thenA}->{hpA}/{thenB}->{hpB}");
                }
                Check(bad.Count == 0, $"K3b {Name(type)}: the hull destroyed {phases.Length} times while its driver is held, its turret debris (one neutral 40 px / 40 HP blast 3-4 s later) put 6 px from him: "
                      + $"{good} of {phases.Length} hurt exactly as the control (DV132 off, nobody held, the same spot): HP when the hull died -> 6 s later, held / control: {string.Join(", ", hps)}{(bad.Count > 0 ? "; WRONG: " + string.Join(" | ", bad) : "")}");
            }
            // K4: orders given while he is held are kept and done once he is out (ATTACK at the EXIT; ENTER into a jeep 1 s after it)
            foreach (var type in new[] { "light", ZMTank.Mech })
            {
                int hold = ZObject.DriverOutHoldTicks(type);
                var s = New(set);
                var v = Crewed(s, type, "grunt", out _); s.Run(0.5);
                s.w.Eject(Me, v); var r = OutOf(s.Take(), s, v);
                var foe = s.Robot("grunt", r.CenterX + 100, r.CenterY + 40, Cpu, 1); foe.Damage = 0;              // a target that fires at nobody: the order, not a fight, is checked
                OzV11Kit.PlayerAttack(s.w, r, foe);
                long t0 = s.w.TickIndex, engaged = -1; int fhp = foe.Health;
                s.Ticks(hold + 600, () => { if (engaged < 0 && (r.AttackObject == foe || foe.Health < fhp)) engaged = s.w.TickIndex - t0; return false; });
                var s2 = New(set);
                var v2 = Crewed(s2, type, "grunt", out _); var jeep = s2.Vehicle("jeep", s2.ox, s2.oy + 120, Team.Neutral); s2.Run(0.5);
                s2.w.Eject(Me, v2); var r2 = OutOf(s2.Take(), s2, v2);
                long t2 = s2.w.TickIndex; s2.Ticks(100);
                OzV11Kit.PlayerEnter(s2.w, r2, jeep);
                long inAt = -1;
                s2.Ticks(hold + 1500, () => { if (inAt < 0 && jeep.Owner == Me && jeep.Drivers.Count > 0) inAt = s2.w.TickIndex - t2; return inAt >= 0; });
                Check(engaged >= hold && engaged <= hold + 300 && inAt >= hold,
                      $"K4 {Name(type)}: orders given while he is held are kept: ATTACK given at the EXIT: engaged at +{engaged} ticks (hold {hold}: not before it, then at once); ENTER into a jeep given 1 s after the EXIT: in at +{inAt} (after the hold)");
            }
            // K5: EXIT while the tank fires at an enemy, and a second EXIT while the first driver is still held (a pyro ordered in meanwhile)
            {
                var s = New(set);
                var v = Crewed(s, "heavy", "grunt", out _);
                var foe = s.Robot("grunt", s.ox + 120, s.oy, Cpu, 1); foe.Damage = 0;
                OzV11Kit.PlayerAttack(s.w, v, foe);
                long fired = -1, tt = s.w.TickIndex; int shotsBefore = 0;
                for (int i = 0; i < 400 && fired < 0; i++) { s.w.Tick(ZConst.TICK); foreach (var e in s.w.Events) if ((e.type == ZEventType.MissileFired || e.type == ZEventType.Hitscan) && e.objId == v.Id) { fired = s.w.TickIndex - tt; shotsBefore++; } s.w.Events.Clear(); }
                bool ok = s.w.Eject(Me, v); var r = OutOf(s.Take(), s, v);
                int shotsAfter = 0;
                for (int i = 0; i < 300; i++) { s.w.Tick(ZConst.TICK); foreach (var e in s.w.Events) if ((e.type == ZEventType.MissileFired || e.type == ZEventType.Hitscan) && e.objId == v.Id) shotsAfter++; s.w.Events.Clear(); }
                Check(fired > 0 && ok && r != null && v.AttackObject == null && shotsAfter == 0 && v.Owner == Team.Neutral,
                      $"K5a EXIT while the heavy tank fights (its first shot at +{fired} ticks): taken {ok}, the hull's target {(v.AttackObject != null ? v.AttackObject.Id.ToString() : "none")}, its shots in the 3 s after {shotsAfter}, the driver out {r != null}");
            }
            foreach (var type in Types)
            {
                var s = New(set);
                var v = Crewed(s, type, "grunt", out _); var pyro = s.Robot("pyro", s.ox + 26, s.oy - 8, Me, 1); s.Run(0.5);   // vehicle_exit_jump: right beside the hull (the hold is the 1.3-1.5 s jump now)
                s.w.Eject(Me, v); var r1 = OutOf(s.Take(), s, v);
                s.Ticks(10);
                bool inOk = s.Order(pyro, v, Me, 6);
                bool r1Held = r1 != null && r1.ExitWalking(s.w.Time);
                int ref0 = s.w.DriverExitsRefused;
                bool can = v.CanExitDriver(out string why), again = s.w.Eject(Me, v); var ev = s.Take();
                bool none = ev.Count(e => e.type == ZEventType.Created) == 0 && v.Owner == Me && v.Drivers.Count == 1 && v.DriverType == "pyro";
                s.Ticks(Math.Max(1, (long)Math.Round((r1.ExitOutAt - s.w.Time) / ZConst.TICK)) + 1);
                bool free1 = !r1.ExitWalking(s.w.Time);
                bool later = s.w.Eject(Me, v); var r2 = OutOf(s.Take(), s, v);
                Check(inOk && r1Held && !can && !again && why == ZWorld.DriverWhyClimbing && none && s.w.DriverExitsRefused == ref0 + 1 && free1 && later && r2 != null && r2.Type == "pyro",
                      $"K5b {Name(type)}: a pyro beside the hull ordered in 0.1 s after the EXIT gets in {inOk} while the old driver is still held {r1Held}; EXIT again at once: refused {!again} ('{why}'), nobody out, the pyro inside {none}; "
                      + $"once the old driver is free ({free1}): EXIT taken {later}, the pyro out {r2 != null}");
            }
        }

        // ------------------------------------------------------------------------------------------------ K6 (final fix, review r3)
        /// <summary>Final fix (review r3, the owner lens O3; its probe with DV121 on: an enemy CPU jeep 48 px
        /// from a held tough knocked him at +157 ticks on the light tank and squashed him at +208 on the mech, an enemy jeep ordered onto his
        /// spot squashed a held grunt driver at +437 / +433): DV65's run-over and DV121's jeep charge skip a driver while his DV171 hold lasts
        /// (ZObject.DriverOutOnHull: his figure is on the hull). K6a, the light tank and the mech with a tough driver, DV121 on as in every Play
        /// session: an enemy jeep 48 px from his spot: while he is held no charge at him, no RunOverHit, no squash; once his hold ends he is
        /// charged and knocked like any robot; the control (DV132 off: nobody held, the same spot) is charged and knocked. K6b, the same two
        /// with a grunt driver: an enemy jeep ordered onto his spot while he is held drives onto it: no RunOverHit, no squash; the control is
        /// knocked. The driver and the jeep at a health no gun takes in the scene (the run-over is what is counted).</summary>
        static void RunOverHeld(string set)
        {
            bool jc = ZWorld.JeepChargeDeviation, ro = ZWorld.RunOverDeviation;
            try
            {
                ZWorld.JeepChargeDeviation = true; ZWorld.RunOverDeviation = true;                                // as View/ZGame.cs at a Play start
                foreach (var type in new[] { "light", ZMTank.Mech })
                {
                    string a = HeldRunOver(set, type, "tough", false, true, out bool chA, out bool hitA, out bool sqA, out long afterA, out long hold, out _);
                    string c = HeldRunOver(set, type, "tough", false, false, out _, out _, out _, out long ctlC, out _, out bool chC);
                    Check(a == null && c == null && !chA && !hitA && !sqA && afterA >= 0 && afterA <= 600 && chC && ctlC > 0,
                          $"K6a {Name(type)} (DV121 on): a held tough driver (hold {hold} ticks), an enemy jeep 48 px from his spot: charged at while held {chA}, knocked while held {hitA}, squashed while held {sqA} "
                          + $"(all must be False); after his hold: knocked {(afterA >= 0 ? $"+{afterA} ticks after it" : "NOT within 6 s")}; control, DV132 off (nobody held, the same spot): charged {chC}, "
                          + $"knocked {(ctlC > 0 ? $"at +{ctlC} ticks after the EXIT" : "NOT within 6 s")} (must be True: the review's leak){((a ?? c) != null ? "; " + (a ?? c) : "")}");
                }
                foreach (var type in new[] { "light", ZMTank.Mech })
                {
                    string a = HeldRunOver(set, type, "grunt", true, true, out _, out bool hitA, out bool sqA, out long onSpotA, out long hold, out _);
                    string c = HeldRunOver(set, type, "grunt", true, false, out _, out _, out _, out long ctlC, out _, out _);
                    Check(a == null && c == null && !hitA && !sqA && onSpotA > 0 && onSpotA < hold && ctlC > 0,
                          $"K6b {Name(type)}: a held grunt driver (hold {hold} ticks), an enemy jeep ordered onto his spot: on it {(onSpotA > 0 ? $"at +{onSpotA} ticks" : "NEVER")} while he is held, "
                          + $"knocked while held {hitA}, squashed while held {sqA} (both must be False); control, DV132 off: knocked {(ctlC > 0 ? $"at +{ctlC} ticks after the EXIT" : "NOT within 6 s")} (must be True)"
                          + $"{((a ?? c) != null ? "; " + (a ?? c) : "")}");
                }
            }
            finally { ZWorld.JeepChargeDeviation = jc; ZWorld.RunOverDeviation = ro; }
        }

        /// <summary>K6's scene: a crewed red `type` with a `crew` driver let out (holdOn = DV132's hold; off: the control, nobody held), the driver at a
        /// health no gun takes; an enemy CPU jeep (crewed, the same health) 48 px from his spot (ordered = false: it picks him itself, DV121 may
        /// charge) or 70 px off ordered onto his spot (ordered = true). Out (ticks after the EXIT unless said): charged at / knocked / squashed
        /// while held; `after` = the first RunOverHit on him after his hold, ticks after it (holdOn) or after the EXIT (the control); in the
        /// ordered case with the hold `after` = when the jeep stood on his spot (within 10 px) while he was held; the hold's ticks; charged at
        /// all. Null = the scene ran.</summary>
        static string HeldRunOver(string set, string type, string crew, bool ordered, bool holdOn, out bool chargedHeld, out bool hitHeld, out bool squashedHeld, out long after, out long hold, out bool chargedAny)
        {
            chargedHeld = hitHeld = squashedHeld = chargedAny = false; after = -1; hold = 0;
            bool was = ZWorld.ExitWalkHoldOn; ZWorld.ExitWalkHoldOn = holdOn;
            try
            {
                var s = New(set);
                var v = Crewed(s, type, crew, out _);
                if (v.Owner != Me) return "the driver did not get in";
                s.Run(0.5);
                if (!s.w.Eject(Me, v)) return "EXIT refused"; var r = OutOf(s.Take(), s, v);
                if (r == null) return "nobody came out";
                r.MaxHealth = r.Health = 100000;
                hold = holdOn ? ZObject.DriverOutHoldTicks(type) : 0;
                var jeep = ordered ? s.Vehicle("jeep", r.CenterX - 22, r.CenterY + 14, Cpu) : s.Vehicle("jeep", r.CenterX + 48, r.CenterY + 24, Cpu);   // vehicle_exit_jump: the ordered jeep 26 px off (the hold is the 1.3-1.5 s jump now)
                jeep.MaxHealth = jeep.Health = 100000;
                if (jeep.Drivers.Count == 0 || jeep.Owner != Cpu) return "the enemy jeep has no crew";
                if (ordered) s.w.SendWaypoints(Cpu, jeep, new[] { ZWaypoint.Move(r.CenterX, r.CenterY, false) });
                long t0 = s.w.TickIndex;
                for (long i = 0; i < hold + 600; i++)
                {
                    s.w.Tick(ZConst.TICK);
                    bool held = r.ExitWalking(s.w.Time);                                                   // as the run-over saw him in this tick (his hold is asked first, in ProcessServer: on its last tick he is out and acts)
                    long k = s.w.TickIndex - t0;
                    bool charging = jeep.ChargeOn && jeep.ChargeTargetId == r.Id;
                    if (charging) { chargedAny = true; if (held) chargedHeld = true; }
                    foreach (var e in s.w.Events)
                    {
                        s.Fold(e);
                        if (e.type == ZEventType.RunOverHit && e.objId == r.Id) { if (held) hitHeld = true; else if (after < 0 && !(ordered && holdOn)) after = holdOn ? k - hold : k; }
                        if (e.type == ZEventType.Death && e.objId == r.Id && r.Squashed && held) squashedHeld = true;
                    }
                    s.w.Events.Clear();
                    if (ordered && holdOn && held && after < 0 && Math.Abs(jeep.CenterX - r.CenterX) <= 10 && Math.Abs(jeep.CenterY - r.CenterY) <= 10) after = k;
                    if (r.Destroyed || r.KillAt >= 0) break;
                    if (!held && after > 0 && !(ordered && holdOn)) break;
                    if (ordered && holdOn && k >= hold) break;
                }
                return null;
            }
            finally { ZWorld.ExitWalkHoldOn = was; }
        }

        /// <summary>K3's scene: a crewed tank of `type`, its driver let out (held: DV132 on; the control: off), the hull destroyed `off` ticks after the
        /// EXIT (-1: one tick before it, the EXIT then); his hit points 6 s after the hull's death (-1: died, -2: none out). Null = no exception.
        /// Fix round 5 (K3b): debrisPx >= 0 puts the hull's turret debris that many px east of him as the hull dies (hpThen: his hit points then).</summary>
        static string HullKilled(string set, string type, int off, bool holdOn, int debrisPx, out int hp, out int hpThen, out bool cutOk, out bool refusedOk, out string info)
        {
            hp = -2; hpThen = -2; cutOk = refusedOk = false; info = "";
            bool was = ZWorld.ExitWalkHoldOn; ZWorld.ExitWalkHoldOn = holdOn;
            try
            {
                var s = New(set);
                var v = Crewed(s, type, "grunt", out _); s.Run(0.5);
                if (off < 0)
                {
                    v.SetHealth(0); s.w.UpdateObjectHealth(v); s.Take(); s.Ticks(1);
                    int ref0 = s.w.DriverExitsRefused;
                    bool ok = s.w.Eject(Me, v); var ev0 = s.Take();
                    refusedOk = !ok && s.w.DriverExitsRefused == ref0 + 1 && ev0.Count(e => e.type == ZEventType.Created) == 0;
                    info = $"EXIT {ok}, refused +{s.w.DriverExitsRefused - ref0}";
                    return null;
                }
                s.w.Eject(Me, v); var r = OutOf(s.Take(), s, v);
                if (r == null) return "nobody came out";
                if (off > 0) s.Ticks(off);
                bool heldThen = r.ExitWalking(s.w.Time); int cut0 = ZWorld.DriverHoldsCut; hpThen = r.Health;
                v.SetHealth(0); s.w.UpdateObjectHealth(v); s.Take();
                int moved = 0;
                if (debrisPx >= 0)
                    foreach (var m in s.w.QueuedMissiles.Concat(s.w.PendingMissiles))
                        if (m.debris && m.attackerId == v.Id) { m.x = r.CenterX + debrisPx; m.y = r.CenterY; moved++; }
                if (debrisPx >= 0 && moved != 1) return $"{moved} turret debris blasts found (1 wanted)";
                s.Ticks(1);
                cutOk = !holdOn || (heldThen && !r.ExitWalking(s.w.Time) && r.ExitOutAt < 0 && ZWorld.DriverHoldsCut == cut0 + 1);
                s.Run(6);
                hp = OzV11Kit.Alive(r) ? r.Health : -1;
                info = $"held then {heldThen}, the hull in the list {s.w.Get(v.Id) != null}";
                return s.w.Get(v.Id) == null ? null : "the hull is still in the object list";
            }
            catch (Exception ex) { return "threw " + ex.GetType().Name + ": " + ex.Message; }
            finally { ZWorld.ExitWalkHoldOn = was; }
        }

        // ------------------------------------------------------------------------------------------------ U
        static void U(string set)
        {
            var types = new List<string> { "jeep", "apc", "apc+", "missile_launcher", ZWorld.ShuttleType };
            types.AddRange(ZBuildLists.Cannons);
            int same = 0; var diff = new List<string>(); var lines = new List<string>();
            foreach (var t in types)
            {
                string Probe(bool on, out ulong h, out long lockLeft, out bool justLeft)
                {
                    using (Rule(on))
                    {
                        var s = New(set);
                        var v = s.Vehicle(t == "apc+" ? "apc" : t, s.ox, s.oy, Me);
                        if (t == "apc+") { var p = s.Robot("grunt", s.ox - 48, s.oy, Me, 3); OzV11Kit.PlayerEnter(s.w, p, v); s.Run(15, () => v.Passengers.Count > 0); }
                        s.Run(1);
                        bool ok = s.w.Eject(Me, v); var ev = s.Take();
                        lockLeft = v.NoAutoEnterUntilTick < 0 ? -1 : v.NoAutoEnterUntilTick - s.w.TickIndex;
                        justLeft = ev.Where(e => e.type == ZEventType.Created).Select(e => s.w.Get(e.objId)).Any(o => o != null && o.JustLeftCannon);
                        s.Run(5);
                        h = s.State();
                        return $"{ok}, {ev.Count} events, owner {v.Owner}, drivers {v.Drivers.Count}, passengers {v.Passengers.Count}, lock {v.DriverOutLockUntilTick}";
                    }
                }
                string on1 = Probe(true, out var hOn, out var lOn, out var jOn), off1 = Probe(false, out var hOff, out var lOff, out var jOff);
                bool eq = on1 == off1 && hOn == hOff && lOn == lOff && jOn == jOff;
                bool gun = ZBuildLists.IsCannon(t);
                if (gun) eq &= lOn == ZObject.EjectLockTicks && jOn;
                if (eq) same++; else diff.Add(t);
                lines.Add($"{t}: {on1}{(gun ? $", DV58 lock {lOn} ticks, JustLeftCannon {jOn}" : "")}");
            }
            Check(diff.Count == 0, $"U1 EXIT with the rule's switch on and off: the same answer and the same state 5 s later for {same} of {types.Count} kinds ({string.Join(", ", types)}){(diff.Count > 0 ? "; DIFFERENT: " + string.Join(", ", diff) : "")}; the guns keep DV58's {ZObject.EjectLockTicks}-tick lock and JustLeftCannon");
            foreach (var l in lines) Info("U  " + l);
        }

        // ------------------------------------------------------------------------------------------------ R
        static void R(string set)
        {
            {   // a CPU team's tank
                var s = New(set);
                var v = s.Vehicle("light", s.ox, s.oy, Cpu); s.Run(0.5);
                int r0 = s.w.DriverExitsRefused;
                bool a = s.w.Eject(Cpu, v), b = s.w.Eject(Me, v); var ev = s.Take();
                bool can = v.CanExitDriver(out var why);
                Check(!a && !b && ev.Count == 0 && v.Owner == Cpu && v.Drivers.Count == 1 && !can && s.w.DriverExitsRefused == r0 + 2, $"R1 a blue CPU tank: EXIT refused for blue ({a}) and for red ({b}), no event, still blue with its driver (CanExitDriver: {why})");
            }
            {   // an empty tank
                var s = New(set);
                var v = s.Vehicle("heavy", s.ox, s.oy, Team.Neutral); s.Run(0.5);
                bool a = s.w.Eject(Me, v), b = s.w.Eject(Team.Neutral, v); var ev = s.Take();
                Check(!a && !b && ev.Count == 0 && v.Owner == Team.Neutral && v.DriverOutLockUntilTick == -1, $"R2 an empty heavy tank: EXIT refused ({a}, {b}), no event, no lock");
            }
            {   // the switch off ('Zod exact'): refused, and the match is the one with no EXIT at all
                ulong Off(bool eject, out bool ok, out string why)
                {
                    using (Rule(false))
                    {
                        var s = New(set);
                        var v = Crewed(s, ZMTank.Mech, "grunt", out _); s.Run(0.5);
                        v.CanExitDriver(out why);
                        ok = eject && s.w.Eject(Me, v); s.Take();
                        s.Run(5);
                        return s.State();
                    }
                }
                ulong h1 = Off(true, out var ok1, out var why1), h0 = Off(false, out _, out _);
                Check(!ok1 && h1 == h0 && why1 == "off in 'Zod exact'", $"R3 the rule's switch off ('Zod exact'): the mech's EXIT refused ({why1}); 5 s later the state is the one with no EXIT given ({h1:X16} / {h0:X16})");
            }
            {   // boxed in by hulls
                var s = New(set);
                var v = Crewed(s, "light", "grunt", out _);
                int r1 = ZWorld.DriverExitRing1("light");
                for (int k = 0; k < 8; k++) s.Vehicle("heavy", v.CenterX + ZWorld.DriverExitDx[k] * r1, v.CenterY + ZWorld.DriverExitDy[k] * r1, Team.Neutral);
                s.Run(0.3);
                int r0 = s.w.DriverExitsRefused;
                bool can = v.CanExitDriver(out var why); bool ok = s.w.Eject(Me, v); var ev = s.Take();
                Check(!can && !ok && why == "no free ground beside the hatch" && ev.Count == 0 && v.Owner == Me && v.Drivers.Count == 1 && s.w.DriverExitsRefused == r0 + 1,
                      $"R4 a light tank boxed in by 8 empty heavy tanks on its first ring: EXIT refused ({why}), no event, still red with its driver");
            }
            {   // boxed in by rocks (each spot's centre tile blocked)
                var s = New(set);
                var v = Crewed(s, ZMTank.Mech, "grunt", out _);
                int r1 = ZWorld.DriverExitRing1(ZMTank.Mech); int rocks = 0;
                var tiles = new HashSet<(int, int)>();
                for (int ring = 0; ring < 2; ring++)
                    for (int k = 0; k < 8; k++)
                    {
                        int rr = r1 + ring * ZWorld.DriverExitRingStep, x = v.CenterX + ZWorld.DriverExitDx[k] * rr, y = v.CenterY + ZWorld.DriverExitDy[k] * rr;
                        if (tiles.Add((x >> 4, y >> 4))) { s.w.CreateObject("map_item", "rock", (x >> 4) * 16, ((y >> 4) - 2) * 16, Team.Neutral); rocks++; }
                    }
                bool can = v.CanExitDriver(out var why); bool ok = s.w.Eject(Me, v); s.Take();
                Check(!can && !ok && why == "no free ground beside the hatch" && v.Owner == Me, $"R5 a mech with {rocks} rocks on the tiles of its 16 spots: EXIT refused ({why}), still red");
            }
            {   // on its factory exit move
                var s = New(set);
                var v = s.Vehicle("light", s.ox, s.oy, Me);
                v.Waypoints.Add(new ZWaypoint { mode = WpMode.ForceMove, refId = -1, x = v.CenterX + 64, y = v.CenterY });   // the move a new vehicle gets out of its factory door (ZWorld.Rules.cs)
                bool can = v.CanExitDriver(out var why); bool ok = s.w.Eject(Me, v); s.Take();
                Check(!can && !ok && why == "leaving the factory or in the repair bay" && v.Owner == Me, $"R6 a light tank on its factory exit move (FORCE_MOVE): EXIT refused ({why})");
            }
            // fix round 1: a corridor two tiles wide between one-tile walls (the reviewer's case: before, the driver was put down behind a wall)
            foreach (bool convoy in new[] { true, false })
            {
                var s = New(set);
                int tx = s.ox >> 4, ty0 = s.oy >> 4;                                                          // the hull on tiles tx-1 .. tx; the walls on tx-2 and tx+1
                for (int ty = ty0 - 8; ty <= ty0 + 8; ty++) { s.w.Path.SetImpassable(tx - 2, ty); s.w.Path.SetImpassable(tx + 1, ty); }
                s.w.Path.RebuildRegions();                                                                    // as a map that has the walls: the region map knows them (they have ends)
                var v = s.Vehicle("light", s.ox, s.oy, Me);
                if (convoy) { s.Vehicle("light", s.ox, s.oy - 34, Team.Neutral); s.Vehicle("light", s.ox, s.oy + 34, Team.Neutral); }   // nose to tail in front and behind
                s.Run(0.3);
                bool can = v.CanExitDriver(out var why, out int sx, out int sy); bool ok = s.w.Eject(Me, v); var ev = s.Take();
                var r = ev.Where(e => e.type == ZEventType.Created).Select(e => s.w.Get(e.objId)).FirstOrDefault(o => o != null);
                bool inside = r != null && r.CenterX - 8 >= (tx - 1) * 16 && r.CenterX + 8 <= (tx + 1) * 16;
                Check((!can && !ok && why == ZWorld.DriverWhyRoom && ev.Count == 0 && v.Owner == Me && v.Drivers.Count == 1) || (ok && inside),
                      $"R7 a light tank in a corridor two tiles wide between one-tile walls{(convoy ? ", tanks nose to tail in front and behind" : "")}: "
                      + (ok ? $"EXIT taken, the driver {Spot(v, r)} inside the corridor {inside}" : $"EXIT refused ({why}: a wall on every side of the hull, fix round 3's room test), still red with its driver"));
            }
            // fix round 1: a wall along the flank and one tile off it (room: the driver stays on the near side); fix round 3: with the wall along the
            // east flank the driver climbs out on the west side (the room is tested on the spot's side of the hull only), never on the wall's side
            foreach (int gap in new[] { 0, 1 })
            {
                var s = New(set);
                var v = s.Vehicle("heavy", s.ox, s.oy, Me);
                int wx = ((s.ox + 16) >> 4) + gap;                                                            // the first tile east of the hull, or the one after it
                for (int ty = (s.oy >> 4) - 6; ty <= (s.oy >> 4) + 6; ty++) s.w.Path.SetImpassable(wx, ty);
                s.w.Path.RebuildRegions(); s.Run(0.3);
                bool can = v.CanExitDriver(out var why, out int sx, out int sy); bool ok = s.w.Eject(Me, v); var ev = s.Take();
                var r = ev.Where(e => e.type == ZEventType.Created).Select(e => s.w.Get(e.objId)).FirstOrDefault(o => o != null);
                if (gap == 0) Check(can && ok && r != null && r.CenterX < v.CenterX && sx == r.CenterX && sy == r.CenterY && !s.w.Path.WithinImpassable(v.CenterX - ZWorld.DriverExitRing1("heavy"), v.CenterY, ZWorld.DriverExitRing1("heavy"), ZWorld.DriverExitRing1("heavy"), out _, out _, true),
                                    $"R8 a heavy tank with a wall along its east flank: EXIT taken {ok}, the driver {(r != null ? Spot(v, r) : "missing")}, on the west side (the side with room) {r != null && r.CenterX < v.CenterX}");
                else Check(can && ok && r != null && r.CenterX + 8 <= wx * 16, $"R8 a heavy tank with a wall one tile east of its flank: EXIT taken {ok}, the driver {(r != null ? Spot(v, r) : "missing")}, on the near side of the wall {r != null && r.CenterX + 8 <= wx * 16}");
            }
            {   // fix round 1: on a bridge deck (two tiles between the bridge's walls): refused, the view could only climb down into the railing
                var s = New(set);
                var v = s.Vehicle(ZMTank.Type, s.ox, s.oy, Me);
                var b = s.w.CreateObject("building", "bridge_vert", ((s.ox >> 4) - 2) * 16, ((s.oy >> 4) - 2) * 16, Team.Neutral);   // its deck on the hull's two tile columns
                s.Run(0.3);
                bool can = v.CanExitDriver(out var why); bool ok = s.w.Eject(Me, v); var ev = s.Take();
                bool onDeck = v.LocX >= b.LocX + 16 && v.LocX + v.WidthPix <= b.LocX + b.WidthPix - 16;
                Check(onDeck && !can && !ok && why == ZWorld.DriverWhyBridge && ev.Count == 0 && v.Owner == Me && v.Drivers.Count == 1,
                      $"R9 a medium tank on a bridge deck (on the deck {onDeck}): EXIT refused ({why}), no event, still red with its driver");
            }
        }

        // ------------------------------------------------------------------------------------------------ R10 (fix round 3): the real levels
        static readonly string[] RealLevels = { "L02", "L07", "L16", "L17" };
        /// <summary>Fix round 3: at least this share of a level's free tank positions lets the driver out (fix round 2's whole-square test: 59-75 %).</summary>
        public const double RealShareMin = 0.95;
        static bool Over(ZObject b, int x, int y, int w, int h) => x < b.LocX + b.WidthPix && x + w > b.LocX && y < b.LocY + b.HeightPix && y + h > b.LocY;
        /// <summary>Box b within d px of the box (x, y, w, h) (0 = touching or overlapping).</summary>
        static bool Within(ZObject b, int x, int y, int w, int h, int d)
        {
            int gx = Math.Max(0, Math.Max(b.LocX - (x + w), x - (b.LocX + b.WidthPix))), gy = Math.Max(0, Math.Max(b.LocY - (y + h), y - (b.LocY + b.HeightPix)));
            return gx * gx + gy * gy <= d * d;
        }

        /// <summary>A free 2 x 2 tank position (top-left tile tx, ty): every tile vehicle-passable, off lava, no building on it but a bridge (bridge:
        /// a tank on the deck is refused by design), no other unit, no map item but a flag (a tank may stand on a flag).</summary>
        static bool FreeTankSpot(ZWorld w, ZObject probe, int tx, int ty, out bool bridge)
        {
            bridge = false;
            int x = tx * 16, y = ty * 16;
            if (w.Path.WithinImpassable(x, y, 32, 32, out _, out _, false)) return false;
            for (int j = 0; j < 2; j++) for (int i = 0; i < 2; i++) if (w.LavaAtPx(x + i * 16 + 8, y + j * 16 + 8)) return false;
            foreach (var o in w.Objects)
            {
                if (o == probe || o.Destroyed || o.KillAt >= 0 || !Over(o, x, y, 32, 32)) continue;
                if (o.Kind == ZKind.Building) { if (o.IsBridge) { bridge = true; continue; } return false; }
                if (o.IsUnit && !o.ShuttleAirborne) return false;
                if (o.Kind == ZKind.MapItem && o.Type != "flag") return false;
            }
            return true;
        }

        /// <summary>What is wrong with the accepted spot (centre sx, sy) of vehicle v (empty: nothing), tested apart from the rule's own code: the
        /// box 8 px inside the map, on robot-passable tiles, off lava, in no building (a bridge deck is ground), on no flag, off its own hull,
        /// outside every other unit's spacing radius (`self`: the driver once out, and his squad), in the hull's robot region; its side of the hull (the half of
        /// the first ring's square facing it) free of robot-impassable tiles and buildings; a second-ring spot: the first-ring box between it and
        /// the hull free of terrain and buildings.</summary>
        static string SpotWrong(ZWorld w, ZObject v, int sx, int sy, ZObject self = null)
        {
            var bad = new List<string>(); int x0 = sx - 8, y0 = sy - 8, r1 = ZWorld.DriverExitRing1(v.Type), hx = v.CenterX, hy = v.CenterY;
            int dx = Math.Sign(sx - hx), dy = Math.Sign(sy - hy);
            if (x0 < 8 || y0 < 8 || sx + 8 > w.Path.WidthPix - 8 || sy + 8 > w.Path.HeightPix - 8) bad.Add("off the map");
            for (int ty = y0 >> 4; ty <= (y0 + 15) >> 4; ty++) for (int tx = x0 >> 4; tx <= (x0 + 15) >> 4; tx++) if (!w.Path.TilePassable(tx, ty, true)) bad.Add("a blocked tile");
            if (w.LavaAtPx(x0, y0) || w.LavaAtPx(x0 + 15, y0) || w.LavaAtPx(x0, y0 + 15) || w.LavaAtPx(x0 + 15, y0 + 15)) bad.Add("lava");
            if (Over(v, x0, y0, 16, 16)) bad.Add("on its own hull");
            int hx0 = dx > 0 ? hx : hx - r1, hx1 = dx < 0 ? hx : hx + r1, hy0 = dy > 0 ? hy : hy - r1, hy1 = dy < 0 ? hy : hy + r1;
            if (w.Path.WithinImpassable(hx0, hy0, hx1 - hx0, hy1 - hy0, out _, out _, true)) bad.Add("terrain on its side of the hull");
            bool ring2 = Math.Max(Math.Abs(sx - hx), Math.Abs(sy - hy)) > r1;
            if (ring2 && w.Path.WithinImpassable(hx + dx * r1 - 8, hy + dy * r1 - 8, 16, 16, out _, out _, true)) bad.Add("ring 2 behind a blocked tile");
            foreach (var o in w.Objects)
            {
                if (o == v || o.Destroyed || o.KillAt >= 0 || (self != null && (o == self || self.Minions.Contains(o)))) continue;
                if (o.Kind == ZKind.Building && !o.IsBridge)
                {
                    if (Over(o, x0, y0, 16, 16)) bad.Add("in a building");
                    if (Over(o, hx0, hy0, hx1 - hx0, hy1 - hy0)) bad.Add("a building on its side of the hull");
                    if (ring2 && Over(o, hx + dx * r1 - 8, hy + dy * r1 - 8, 16, 16)) bad.Add("ring 2 behind a building");
                }
                if (o.Kind == ZKind.MapItem && o.Type == "flag" && Over(o, x0, y0, 16, 16)) bad.Add("on a flag");
                if (o.IsUnit && !o.ShuttleAirborne)
                {
                    long min = ZWorld.DriverRadiusOf(v.DriverType) + ZPathing.Radius(o), ddx = o.CenterX - sx, ddy = o.CenterY - sy;
                    if (ddx * ddx + ddy * ddy < min * min) bad.Add("inside a unit's spacing radius");
                }
            }
            if (w.Path.TilePassable(hx >> 4, hy >> 4, true) && !w.Path.InSameRegion(hx, hy, sx, sy, true)) bad.Add("another robot region");
            return string.Join(", ", bad.Distinct());
        }

        static void Real(string set, Func<string, string> map)
        {
            foreach (var lv in RealLevels)
            {
                var w = ZWorld.Create(set, map(lv), 3, lv == "L17");                                               // as a match starts (level 17: DV32's parked start vehicles)
                w.NoGameEnd = true; w.Events.Clear();
                var v = w.CreateObject("vehicle", "light", 0, 0, Team.Neutral);                                     // the probe tank: empty, moved over the map, never ticked
                int n = 0, bridge = 0, can = 0, f8 = 0, f8Can = 0, b8 = 0, b8Can = 0, bad = 0; string firstBad = null;
                var flagCan = new Dictionary<int, bool>(); (int x, int y) fPos = (-1, -1), bPos = (-1, -1);
                var flags = w.Objects.Where(o => o.Kind == ZKind.MapItem && o.Type == "flag" && !o.Destroyed).ToList();
                var blds = w.Objects.Where(o => o.Kind == ZKind.Building && !o.IsBridge && !o.Destroyed).ToList();
                for (int ty = 0; ty < w.Map.height - 1; ty++)
                    for (int tx = 0; tx < w.Map.width - 1; tx++)
                    {
                        if (!FreeTankSpot(w, v, tx, ty, out bool br)) continue;
                        if (br) { bridge++; continue; }
                        v.SetCords(tx * 16, ty * 16); n++;
                        bool ok = w.DriverExitSpot(v, out int sx, out int sy, out _);
                        if (ok) { can++; var wrong = SpotWrong(w, v, sx, sy); if (wrong.Length > 0) { bad++; firstBad ??= $"({tx}, {ty}): {wrong}"; } }
                        bool nf = false, nb = false;
                        foreach (var f in flags) if (Within(f, v.LocX, v.LocY, 32, 32, 8)) { nf = true; flagCan[f.Id] = (flagCan.TryGetValue(f.Id, out bool c) && c) || ok; }
                        foreach (var b in blds) if (Within(b, v.LocX, v.LocY, 32, 32, 8)) { nb = true; break; }
                        if (nf) { f8++; if (ok) { f8Can++; if (fPos.x < 0) fPos = (tx, ty); } }
                        if (nb) { b8++; if (ok) { b8Can++; if (bPos.x < 0) bPos = (tx, ty); } }
                    }
                v.KillAt = 0;                                                                                       // the probe out of the way of the two real exits below
                string Out((int x, int y) at, string what, out bool fine)
                {
                    fine = false;
                    if (at.x < 0) return what + ": no such position";
                    var t = w.CreateObject("vehicle", "light", at.x * 16, at.y * 16, Me);                            // crewed (a grunt: Zod's start driver)
                    w.Events.Clear();
                    bool can1 = t.CanExitDriver(out _, out int px, out int py), ok1 = w.Eject(Me, t);
                    var r = w.Events.Where(e => e.type == ZEventType.Created && e.otherId == t.Id).Select(e => w.Get(e.objId)).FirstOrDefault(o => o != null);
                    w.Events.Clear();
                    string wrong = r != null ? SpotWrong(w, t, r.CenterX, r.CenterY, r) : "nobody out";
                    fine = can1 && ok1 && r != null && r.CenterX == px && r.CenterY == py && t.Owner == Team.Neutral && wrong.Length == 0;
                    if (r != null) { r.KillAt = 0; foreach (var m in r.Minions) if (m != null) m.KillAt = 0; }
                    t.KillAt = 0;
                    return $"{what} at tile ({at.x}, {at.y}): EXIT taken {ok1}, the driver {(r != null ? Spot(t, r) : "missing")}{(wrong.Length > 0 ? " WRONG: " + wrong : "")}";
                }
                string fo = Out(fPos, "a crewed red light tank beside a flag", out bool fOk), bo = Out(bPos, "one beside a building", out bool bOk);
                int flagsBeside = flagCan.Count, flagsCan = flagCan.Count(kv => kv.Value);
                double share = n > 0 ? (double)can / n : 0, fShare = f8 > 0 ? (double)f8Can / f8 : 0;
                Check(n > 0 && share >= RealShareMin && f8 > 0 && fShare >= RealShareMin && flagsCan == flagsBeside && bad == 0 && fOk && bOk,
                      $"R10 level {int.Parse(lv.Substring(1))}: a light tank on each of its {n} free 2 x 2 tank positions ({bridge} more on a bridge deck, refused by design): EXIT possible on {can} = {share * 100:0.0} % "
                      + $"(at least {RealShareMin * 100:0} %); beside a flag (within 8 px) {f8Can} of {f8} = {fShare * 100:0.0} %, of its {flags.Count} flags {flagsCan} of the {flagsBeside} with a free position beside them "
                      + $"have a tank there that can; beside a building {b8Can} of {b8} = {(b8 > 0 ? 100.0 * b8Can / b8 : 0):0.0} %; accepted spots wrong {bad}{(firstBad != null ? " (first " + firstBad + ")" : "")}; {fo}; {bo}");
            }
        }

        // ------------------------------------------------------------------------------------------------ D
        static void D(string set, string l02, double botSeconds)
        {
            ulong Scene1()
            {
                var s = New(set);
                var v = Crewed(s, "heavy", "veteran", out _);
                s.Robot("grunt", s.ox - 32, s.oy - 48, Me, 3);
                s.Run(0.7); s.w.Eject(Me, v); s.Take(); s.Run(32);
                return s.State();
            }
            ulong a = Scene1(), b = Scene1();
            Check(a == b, $"D1 the same scene twice (heavy tank EXIT, 32 s with a squad beside it, the re-entry after the lock): {a:X16} / {b:X16}");
            if (botSeconds <= 0) return;
            List<ulong> Match(bool on, out int exits, out string end)
            {
                using (Rule(on))
                {
                    ZWorld.CalmStartDeviation = true;                                                       // a real match start
                    var w = ZWorld.Create(set, l02, 3);
                    w.Bots.Add(new ZBot(w, Cpu, 104)); w.Bots.Add(new ZBot(w, Me, 3));
                    var fp = new List<ulong>(); ulong h = 1469598103934665603UL;
                    int n = (int)Math.Round(botSeconds / ZConst.TICK);
                    for (int i = 0; i < n && !w.GameOver; i++)
                    {
                        w.Tick(ZConst.TICK);
                        foreach (var e in w.Events) { h = (h ^ (ulong)(long)e.type) * 1099511628211UL; h = (h ^ (ulong)(long)e.objId) * 1099511628211UL; h = (h ^ (ulong)(long)e.otherId) * 1099511628211UL; }
                        w.Events.Clear();
                        if (i % 100 == 99) { h = w.RulesFingerprint(h); fp.Add(h); }
                    }
                    exits = w.DriverExits + w.DriverExitsRefused;
                    end = $"{w.Time:0} s, {w.Objects.Count} objects, over {w.GameOver}";
                    return fp;
                }
            }
            var fOn = Match(true, out int eOn, out var endOn); var fOff = Match(false, out int eOff, out var endOff);
            int differ = 0; for (int i = 0; i < Math.Min(fOn.Count, fOff.Count); i++) if (fOn[i] != fOff[i]) differ++;
            Check(fOn.Count == fOff.Count && fOn.Count > 0 && differ == 0 && eOn == 0 && eOff == 0,
                  $"D2 computer against computer on level 2 (seed 3, {botSeconds:0} s): the rule's switch on and off give the same fingerprint at all {fOn.Count} points (every 100 ticks; {differ} differ); "
                  + $"EXIT orders {eOn} / {eOff} (the computer never gives one); end {endOn} / {endOff}");
        }

        public static string Run(string settings, Func<string, string> map, double botSeconds = 300)
        {
            pass = fail = 0; sb = new StringBuilder();
            string l02 = map("L02");
            var t0 = DateTime.UtcNow;
            try
            {
                using (Rule())
                {
                    Info($"the scenes: a plain {GroundW} x {GroundH}-tile map, the units round its centre ({GroundW * 8}, {GroundH * 8}) px; the rule's switch {ZWorld.VehicleExitDeviation}, lock {Lock} ticks, "
                         + $"first rings {string.Join(" / ", Types.Select(t => Name(t) + " " + ZWorld.DriverExitRing1(t)))} px; the getting-out holds (DV132) "
                         + $"{string.Join(" / ", Types.Select(t => Name(t) + " " + ZObject.DriverOutHoldTicks(t)))} ticks (switch {ZWorld.ExitWalkHoldOn}); the bot match on level 2");
                    E(settings); L(settings); L17(settings, map("L17")); O(settings); G(settings); H(settings); K(settings); RunOverHeld(settings); U(settings); R(settings); Real(settings, map); D(settings, l02, botSeconds);
                }
            }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace); }
            Info($"run time {(DateTime.UtcNow - t0).TotalSeconds:0.0} s (the check's own, not a rule input)");
            sb.AppendLine($"[OpenZ V1.4 vehicle exit] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

        /// <summary>The Deviations row's probe: a light tank and the mech let their drivers out (neutral, the robot beside the hull, the lock for
        /// exactly 3000 ticks with the driver and a squad beside it; fix round 2: the driver held as getting out for his vehicle's ticks), a pyro
        /// ordered in at once, a CPU team's EXIT refused, the switch off refused.</summary>
        public static string Quick(string settings)
        {
            pass = fail = 0; sb = new StringBuilder();
            try
            {
                using (Rule())
                {
                    foreach (var type in new[] { "light", ZMTank.Mech })
                    {
                        var s = New(settings);
                        var v = Crewed(s, type, "grunt", out _);
                        s.Robot("grunt", s.ox - 32, s.oy - 48, Me, 3);
                        s.Run(0.5);
                        bool ok = s.w.Eject(Me, v); var ev = s.Take();
                        var r = ev.Where(e => e.type == ZEventType.Created).Select(e => s.w.Get(e.objId)).FirstOrDefault(o => o != null);
                        long t0 = s.w.TickIndex; s.watch = v; int hold = ZObject.DriverOutHoldTicks(type);
                        s.Ticks(hold - 1); bool held = r != null && r.ExitWalking(s.w.Time);
                        s.Ticks(1); bool out1 = r != null && !r.ExitWalking(s.w.Time);
                        s.Ticks(Lock - hold - 1); bool locked = v.AutoEnterLocked; int before = s.entries;
                        s.Ticks(1); bool free = !v.AutoEnterLocked;
                        Check(ok && v.Owner == Team.Neutral && r != null && before == 0 && locked && free && held && out1,
                              $"{Name(type)}: EXIT taken {ok}, neutral {v.Owner == Team.Neutral}, the driver {(r != null ? Spot(v, r) : "missing")}, held as getting out for {hold} ticks ({held}, free then {out1}); "
                              + $"nobody climbs in by himself for {Lock} ticks ({before} entries; locked at +{Lock - 1}: {locked}, free at +{Lock}: {free})");
                    }
                    {
                        var s = New(settings);
                        var v = Crewed(s, "light", "grunt", out _);
                        s.Run(0.5); s.w.Eject(Me, v); s.Take(); s.watch = v; long t0 = s.w.TickIndex;
                        s.Run(2);
                        var pyro = s.Robot("pyro", s.ox - 48, s.oy + 48, Me, 1);
                        bool inNow = s.Order(pyro, v, Me, 12);
                        Check(inNow && v.DriverType == "pyro" && v.DriverOutLockUntilTick == -1, $"a pyro ordered in 2 s after the EXIT gets in at once ({(s.firstIn - t0) * ZConst.TICK:0.00} s after the EXIT), the lock ends");
                    }
                    {
                        var s = New(settings);
                        var v = s.Vehicle("light", s.ox, s.oy, Cpu); s.Run(0.3);
                        bool a = s.w.Eject(Cpu, v); s.Take();
                        var m = s.Vehicle(ZMTank.Mech, s.ox + 96, s.oy, Me); s.Run(0.3);
                        ZWorld.VehicleExitDeviation = false; bool off = s.w.Eject(Me, m); ZWorld.VehicleExitDeviation = true; s.Take();
                        Check(!a && v.Owner == Cpu && !off && m.Owner == Me, $"refused: a CPU team's tank ({a}), the switch off ({off})");
                    }
                }
            }
            catch (Exception ex) { Check(false, "the probe threw " + ex.GetType().Name + ": " + ex.Message); }
            sb.AppendLine($"[OpenZ V1.4 vehicle exit quick] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

#if UNITY_EDITOR && !OPENZ_HARNESS
        [UnityEditor.MenuItem("OpenZ/V1.4/Vehicle Exit: Count (headless, DV171)")]
        static void Menu() => OzV11Kit.RunMenu("vehicle exit: count (DV171)", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText));

        [UnityEditor.MenuItem("OpenZ/V1.4/Vehicle Exit: Count without the bot match (headless, DV171)")]
        static void MenuShort() => OzV11Kit.RunMenu("vehicle exit: count, no bot match (DV171)", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText, 0));

        [UnityEditor.MenuItem("OpenZ/V1.4/Vehicle Exit: Play Check (real mouse, a level playing)")]
        static void PlayMenu()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning("[OpenZ V1.4 vehicle exit play] start a level in Play first (level 2, the normal mode), click the Game view once"); return; }
            OzVehicleExitCheck.Folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_4/kits/vehicle_exit_jump/proof/play"));   // vehicle_exit_jump: the new Play check writes its pictures and report there
            OzVehicleExitCheck.Begin();
        }

        [UnityEditor.MenuItem("OpenZ/V1.4/Vehicle Exit: Play Check, last report")]
        static void PlayLast() => UnityEngine.Debug.Log(OzVehicleExitCheck.Running ? "[OpenZ V1.4 vehicle exit play] running" : OzVehicleExitCheck.Report);
#endif
    }
}
