using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// psychos and the gatling against robots on foot). Headless: the simulation only (ZWorld.Create + Tick), no scene, no Play mode,
    /// nothing saved. The caller sets the game's switches (harness: game_switches.txt, as View/ZGame.cs at a level start) and puts them back.
    /// Every fight is one world per seed on open plain ground; side A = red (the player's), side B = blue (a computer team that gives no
    /// orders of its own). Modes:
    ///  stand:D  both sides frozen in place D px apart (centre to centre), no orders: they fire by themselves (Zod's passive engage);
    ///           squads stand in a line across the line of fire, 16 px apart
    ///  order    both sides start 260 px apart and get an attack-move order through the other's start (a gun stands, it cannot move)
    ///  one-sided (flag) only side A fires (side B: damage 0): the owner's research trials were "no return fire"
    /// Measured per fight: the first shot (either side), the first kill, the wipe (one side has no unit left that fights: a robot dead, a
    /// vehicle / gun destroyed or emptied by a driver snipe), who wins, shots / hits / HP dealt per side, and for a vehicle or gun its
    /// driver snipe and hull death. Also the hull / building controls (damage per minute, hull only, no driver exposure) and a bot battle.
    /// Overrides for what-if runs (harness env, never in the game): see Program.cs (OZ_RIFLE, OZ_DMG, OZ_UNIT, OZ_SEAT, OZ_FLOOR).
    /// </summary>
    public static class OzV15SmallArmsDuel
    {
        public const int Big = 1000000000;
        public const int Y = 760, AX = 500;
        public static double Limit = 240;
        /// <summary>harness: side A blue (the computer's team, no orders of its own) and B red instead (a check of the side bias).</summary>
        public static bool SwapTeams;
        /// <summary>Even seeds swap the teams (A blue, B red), so a side's team bias cancels over the fights (on in every matrix run).</summary>
        public static bool AlternateTeams = true;                                                         // s of sim per fight at most
        static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>harness what-if: object Damage per type set at placement (OZ_DMG). Empty in every real check.</summary>
        public static readonly Dictionary<string, int> DamageOverride = new Dictionary<string, int>();

        public sealed class Side
        {
            public string type; public int size; public bool frozen, noDamage, driverImmortal, noExposure;
            public readonly List<ZObject> units = new List<ZObject>();
            public int shots, hits, driverHits; public long dealt; public double firstShot = -1;
            public bool IsVehicle => units.Count > 0 && units[0].Kind != ZKind.Robot;
        }

        public sealed class Fight
        {
            public string name, mode; public int seed, dist; public bool oneSided;
            public Side a, b;
            public double firstShot = -1, firstKill = -1, wipe = -1, sniped = -1, hullDead = -1, end;
            public string firstKillSide = "", winner = "none", vehSide = "";
            public int aLeft, bLeft; public double aHpLeft, bHpLeft;
            public double shotDist = -1;
            public double Rel(double t) => t < 0 || firstShot < 0 ? -1 : t - firstShot;
        }

        static bool Fights(ZObject o)
        {
            if (o == null || o.Destroyed || o.KillAt >= 0) return false;
            if (o.Kind == ZKind.Robot) return true;
            return o.Owner != Team.Neutral && o.Drivers.Count > 0;                                 // a vehicle / gun: crewed
        }

        static ZObject PlaceOne(ZWorld w, string type, int cx, int cy, Team team, int amount)
        {
            if (type == ZHeroSquads.AlanBrad) return w.CreateHeroSquad(type, team, new[] { cx / 16, cy / 16 });   // DV108 the ALAN & BRAD hero squad (two grunts, x3 HP, the strong shot)
            var o = OzV11Kit.Place(w, type, cx, cy, team, ZBuildLists.IsRobot(type) ? amount : -1);
            return o;
        }

        /// <summary>Puts a side down: robots as one squad (amount), a vehicle / gun one object. Squad members in a line across the fire.</summary>
        static void Put(ZWorld w, Side s, int cx, int cy, Team team)
        {
            var o = PlaceOne(w, s.type, cx, cy, team, s.size);
            s.units.AddRange(OzV11Kit.Squad(o));
            if (s.units.Count > 1)
            {
                int n = s.units.Count;
                for (int i = 0; i < n; i++)
                {
                    int off = (i - (n - 1) / 2) * 16;                                                    // -16, 0, +16 (3); -16..+32 (4)
                    var m = s.units[i]; m.SetCords(cx - 8, cy - 8 + off);
                }
            }
            foreach (var u in s.units)
            {
                if (DamageOverride.TryGetValue(u.Type, out int d)) u.Damage = d;
                if (s.noDamage) u.Damage = 0;
                if (s.frozen) { u.MoveSpeed = 0; u.RealMoveSpeed = 0; }
                if (s.driverImmortal && u.Drivers.Count > 0) u.Drivers[0] = Big;
                if (s.noExposure) { u.CanBeSnipedFlag = false; }
            }
        }

        static ZWorld NewWorld(string settings, int seed)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(96, 96), seed);
            OzV11Kit.Dummy(w, 60, 1480);                                                                       // blue: never over
            var d = w.CreateRobotGroup("grunt", 1432, 1472, Team.Red, 1); d.MoveSpeed = 0; d.RealMoveSpeed = 0; d.Damage = 0; d.MaxHealth = d.Health = Big;   // red: the same
            w.Events.Clear();
            return w;
        }

        /// <summary>One fight. mode "stand" (dist px apart, frozen) or "order" (260 px apart, attack-move through each other).</summary>
        public static Fight Run(string settings, int seed, string aType, int aSize, string bType, int bSize, string mode, int dist, bool oneSided,
                                bool bDriverImmortal = false, bool bNoExposure = false, double limit = -1)
        {
            if (limit < 0) limit = Limit;
            bool stand = mode == "stand";
            var f = new Fight { name = $"{aType} x{aSize} vs {bType} x{bSize}", mode = stand ? "stand " + dist + " px" : "order", seed = seed, dist = dist, oneSided = oneSided };
            f.a = new Side { type = aType, size = aSize, frozen = stand };
            f.b = new Side { type = bType, size = bSize, frozen = stand, noDamage = oneSided, driverImmortal = bDriverImmortal, noExposure = bNoExposure };
            var w = NewWorld(settings, seed);
            int gap = stand ? dist : 260;
            bool swap = SwapTeams ^ (AlternateTeams && seed % 2 == 0);                                  // even seeds: A blue, B red (the order fights have a side bias: see the README)
            bool mirror = AlternateTeams && seed % 2 == 0;                                              // even seeds: A on the right too (the order fights have a side bias)
            int ax = mirror ? AX + gap : AX, bx = mirror ? AX : AX + gap;
            Put(w, f.a, ax, Y, swap ? Team.Blue : Team.Red);
            Put(w, f.b, bx, Y, swap ? Team.Red : Team.Blue);
            if (!stand)
            {
                foreach (var s in new[] { f.a, f.b })
                {
                    var lead = s.units[0];
                    if (lead.Kind == ZKind.Cannon) continue;                                                // a gun cannot move
                    int other = s == f.a ? bx : ax, own = s == f.a ? ax : bx;
                    int gx = other + (other > own ? 200 : -200);                                           // through the other's start
                    w.SendWaypoints(lead.Owner, lead, new[] { ZWaypoint.Move(gx, Y, true) });
                }
            }
            var all = f.a.units.Concat(f.b.units).ToList();
            var sideOf = new Dictionary<int, Side>();
            foreach (var u in f.a.units) sideOf[u.Id] = f.a;
            foreach (var u in f.b.units) sideOf[u.Id] = f.b;
            var hpBefore = all.ToDictionary(u => u.Id, u => u.Health);
            while (w.Time < limit)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                {
                    if ((e.type == ZEventType.Hitscan || e.type == ZEventType.MissileFired) && sideOf.TryGetValue(e.objId, out var s) && sideOf.TryGetValue(e.otherId, out var ts) && ts != s)
                    {
                        s.shots++; if (e.type == ZEventType.Hitscan && e.flag) s.hits++; if (e.type == ZEventType.Hitscan && e.flag2) s.driverHits++;
                        if (s.firstShot < 0) s.firstShot = w.Time;
                        if (f.firstShot < 0)
                        {
                            f.firstShot = w.Time;
                            var at = w.Get(e.objId); var to = w.Get(e.otherId);
                            if (at != null && to != null) f.shotDist = OzV11Kit.Dist(at, to);
                        }
                    }
                    else if (e.type == ZEventType.Sniped && sideOf.TryGetValue(e.objId, out var vs) && f.sniped < 0) { f.sniped = w.Time; f.vehSide = vs == f.a ? "A" : "B"; }
                    else if (e.type == ZEventType.Death && sideOf.TryGetValue(e.objId, out var ds))
                    {
                        if (f.firstKill < 0) { f.firstKill = w.Time; f.firstKillSide = ds == f.a ? "B" : "A"; }   // the side that killed
                        var o = w.Get(e.objId);
                        if (o != null && o.Kind != ZKind.Robot && f.hullDead < 0) { f.hullDead = w.Time; f.vehSide = ds == f.a ? "A" : "B"; }
                    }
                }
                w.Events.Clear();
                // a driver sniped is a kill of that vehicle / gun for the fight (it no longer fights)
                if (f.firstKill < 0 && f.sniped >= 0) { f.firstKill = f.sniped; f.firstKillSide = f.vehSide == "A" ? "B" : "A"; }
                bool aAlive = f.a.units.Any(Fights), bAlive = f.b.units.Any(Fights);
                if (!aAlive || !bAlive)
                {
                    f.wipe = w.Time; f.winner = aAlive ? "A" : bAlive ? "B" : "both dead";
                    // keep going a little only to see a hull die after its snipe? no: the fight is over
                    break;
                }
            }
            f.end = w.Time;
            foreach (var s in new[] { f.a, f.b })
                foreach (var u in s.units)
                {
                    int lost = hpBefore[u.Id] - Math.Max(0, u.Health);
                    if (s == f.a) f.b.dealt += lost; else f.a.dealt += lost;
                }
            f.aLeft = f.a.units.Count(Fights); f.bLeft = f.b.units.Count(Fights);
            f.aHpLeft = f.a.units.Where(Fights).Sum(u => (double)u.Health / Math.Max(1, u.MaxHealth));
            f.bHpLeft = f.b.units.Where(Fights).Sum(u => (double)u.Health / Math.Max(1, u.MaxHealth));
            return f;
        }

        // ------------------------------------------------------------------ statistics
        public static string Stat(IEnumerable<double> xs)
        {
            var l = xs.Where(x => x >= 0).OrderBy(x => x).ToList();
            if (l.Count == 0) return "- / - / - / - (n 0)";
            double med = l.Count % 2 == 1 ? l[l.Count / 2] : 0.5 * (l[l.Count / 2 - 1] + l[l.Count / 2]);
            return $"{F(l.Average())} / {F(med)} / {F(l[0])} / {F(l[l.Count - 1])} (n {l.Count})";
        }

        public static string Header =>
            "matchup | mode | fights | first shot at s (mean) | shot dist px (mean) | first kill s after the first shot: mean / median / min / max | by | wipe s: mean / median / min / max | A wins | B wins | none | A shots | A hits | A HP dealt | B shots | B hits | B HP dealt | the vehicle's / gun's driver sniped (fights): s mean / median / min / max | its hull destroyed (fights): s | A left (mean units) | B left";

        public static string Row(string label, List<Fight> fs)
        {
            int n = fs.Count;
            string by = string.Join(" ", fs.GroupBy(x => x.firstKillSide).OrderBy(g => g.Key).Select(g => (g.Key == "" ? "-" : g.Key) + ":" + g.Count()));
            double m(Func<Fight, double> g) => n == 0 ? 0 : fs.Average(g);
            return $"{label} | {fs[0].mode}{(fs[0].oneSided ? " one-sided (only A fires)" : "")} | {n} | {F(m(x => x.firstShot))} | {F1(fs.Where(x => x.shotDist >= 0).Select(x => x.shotDist).DefaultIfEmpty(-1).Average())} | " +
                   $"{Stat(fs.Select(x => x.Rel(x.firstKill)))} | {by} | {Stat(fs.Select(x => x.Rel(x.wipe)))} | {fs.Count(x => x.winner == "A")} | {fs.Count(x => x.winner == "B")} | {fs.Count(x => x.winner != "A" && x.winner != "B")} | " +
                   $"{F1(m(x => x.a.shots))} | {F1(m(x => x.a.hits))} | {F1(m(x => x.a.dealt))} | {F1(m(x => x.b.shots))} | {F1(m(x => x.b.hits))} | {F1(m(x => x.b.dealt))} | " +
                   $"{fs.Count(x => x.sniped >= 0)}: {Stat(fs.Select(x => x.Rel(x.sniped)))} | {fs.Count(x => x.hullDead >= 0)}: {Stat(fs.Select(x => x.Rel(x.hullDead)))} | {F(m(x => x.aLeft))} | {F(m(x => x.bLeft))}";
        }

        // ------------------------------------------------------------------ controls: damage per minute on a hull / building, no driver exposure
        public sealed class Dpm { public string shooter, target; public double hpPerMin, hullPct; public int shots, hits, maxHp; public bool engaged; }

        public static Dpm Control(string settings, int seed, string shooter, string target, double seconds = 60)
        {
            var w = NewWorld(settings, seed);
            var sh = PlaceOne(w, shooter, AX, Y, Team.Red, 1);
            sh.MoveSpeed = 0; sh.RealMoveSpeed = 0;
            if (DamageOverride.TryGetValue(sh.Type, out int dd)) sh.Damage = dd;
            ZObject t;
            if (target == "radar")
            {
                t = w.CreateObject("building", "radar", AX + 70, Y - 16, Team.Blue);
            }
            else
            {
                t = PlaceOne(w, target, AX + 90, Y, Team.Blue, 1);
                t.MoveSpeed = 0; t.RealMoveSpeed = 0; t.Damage = 0; t.CanBeSnipedFlag = false;   // hull only: its driver can never be hit
            }
            int max = t.MaxHealth;
            t.MaxHealth = t.Health = Big;
            if (target == "radar") OzV11Kit.PlayerAttack(w, sh, t); else OzV11Kit.PlayerAttack(w, sh, t);
            var r = new Dpm { shooter = shooter, target = target, maxHp = max };
            double t0 = -1;
            while (w.Time < seconds + 5 && (t0 < 0 || w.Time - t0 < seconds))
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                    if ((e.type == ZEventType.Hitscan || e.type == ZEventType.MissileFired) && e.objId == sh.Id && e.otherId == t.Id)
                    { r.shots++; if (e.flag && e.type == ZEventType.Hitscan) r.hits++; if (t0 < 0) t0 = w.Time; r.engaged = true; }
                w.Events.Clear();
            }
            r.hpPerMin = (Big - t.Health) * (60.0 / seconds);
            r.hullPct = max > 0 ? 100.0 * r.hpPerMin / max : 0;
            return r;
        }

        // ------------------------------------------------------------------ the matrix (M1-M5 + controls)
        static int Size(string t) => t == "jeep" || t == "gatling" ? 1 : (t == "grunt" ? 3 : t == "psycho" ? 3 : t == "sniper" ? 3 : t == "tough" ? 2 : t == "pyro" ? 4 : t == "laser" ? 4 : 1);

        /// <summary>Every matchup of the owner's four asks, runs fights (seeds 1..runs) each. only: "" = all, else a list like "M1,M3,C".</summary>
        public static void Baseline(string settings, int runs, string only, Action<string> say)
        {
            bool On(string g) => string.IsNullOrEmpty(only) || only.Split(',').Contains(g);
            void Group(string label, string a, int an, string b, int bn, bool oneSided = false, bool bImm = false, bool bNoExp = false, double limit = -1, params string[] modes)
            {
                foreach (var md in modes.Length > 0 ? modes : new[] { "stand:100", "stand:60", "order" })
                {
                    var p = md.Split(':'); string mode = p[0]; int dist = p.Length > 1 ? int.Parse(p[1]) : 0;
                    var fs = new List<Fight>();
                    for (int s = 1; s <= runs; s++) fs.Add(Run(settings, s, a, an, b, bn, mode, dist, oneSided, bImm, bNoExp, limit));
                    say(Row(label, fs));
                }
            }
            say(Header);
            if (On("M1"))
            {
                Group("M1 grunt 1v1", "grunt", 1, "grunt", 1);
                Group("M1 grunt 1v1 (owner's trial: no return fire)", "grunt", 1, "grunt", 1, true, modes: new[] { "stand:100", "stand:60" });
            }
            if (On("M2")) Group("M2 grunt squad vs grunt squad", "grunt", 3, "grunt", 3);
            if (On("M2b"))
                foreach (var t in new[] { "tough", "sniper", "pyro", "laser", "psycho" })
                {
                    Group($"M2b grunt 1v1 {t}", "grunt", 1, t, 1, modes: new[] { "stand:100" });
                    Group($"M2b grunt 1v1 {t} (the {t} does not fire)", "grunt", 1, t, 1, true, modes: new[] { "stand:100" });
                    Group($"M2b grunt squad vs {t} squad", "grunt", 3, t, Size(t), modes: new[] { "stand:100", "order" });
                }
            if (On("M3"))
            {
                Group("M3 jeep vs 1 grunt", "jeep", 1, "grunt", 1);
                Group("M3 jeep vs grunt squad", "jeep", 1, "grunt", 3);
                Group("M3 jeep vs psycho squad", "jeep", 1, "psycho", 3, modes: new[] { "stand:100", "order" });
                Group("M3 jeep gun alone vs 1 grunt (grunt does not fire)", "jeep", 1, "grunt", 1, true, modes: new[] { "stand:100", "stand:60" });
                Group("M3 jeep gun alone vs grunt squad (squad does not fire)", "jeep", 1, "grunt", 3, true, modes: new[] { "stand:100", "stand:60" });
                Group("M3 grunt squad vs jeep (jeep does not fire): snipe", "grunt", 3, "jeep", 1, true, modes: new[] { "stand:100", "stand:60" });
                Group("M3 1 grunt vs jeep (jeep does not fire): snipe", "grunt", 1, "jeep", 1, true, modes: new[] { "stand:100", "stand:60" });
                Group("M3 grunt squad vs jeep (jeep does not fire, its driver cannot die): hull", "grunt", 3, "jeep", 1, true, true, false, 600, "stand:100");
                Group("M3 1 grunt vs jeep (jeep does not fire, its driver cannot die): hull", "grunt", 1, "jeep", 1, true, true, false, 900, "stand:60");
            }
            if (On("M4"))
            {
                foreach (var t in new[] { "grunt", "tough", "sniper", "pyro", "laser", "psycho" })
                {
                    Group($"M4 psycho 1v1 {t}", "psycho", 1, t, 1);
                    Group($"M4 psycho squad vs {t} squad", "psycho", 3, t, Size(t));
                    Group($"M4 psycho 1v1 {t} (the {t} does not fire)", "psycho", 1, t, 1, true, modes: new[] { "stand:100" });
                }
            }
            if (On("M5"))
            {
                foreach (var t in new[] { "grunt", "psycho", "tough" })
                {
                    Group($"M5 gatling vs {t} squad", "gatling", 1, t, Size(t));
                    Group($"M5 gatling vs {t} squad (the squad does not fire)", "gatling", 1, t, Size(t), true, modes: new[] { "stand:100" });
                }
            }
            if (On("P2"))                                                                                 // DV212 part 2 (the owner's Z idea): driver first or hull first, small arms on a jeep / light tank that does not fire
                foreach (var (sh, n) in new[] { ("grunt", 1), ("grunt", 3), ("psycho", 3), ("jeep", 1), ("gatling", 1) })
                {
                    Group($"P2 {sh} x{n} vs jeep (jeep does not fire): its driver sniped or its hull first", sh, n, "jeep", 1, true, false, false, 1800, "stand:100");
                    Group($"P2 {sh} x{n} vs jeep hull (jeep does not fire, its driver cannot die)", sh, n, "jeep", 1, true, true, false, 1800, "stand:100");
                    Group($"P2 {sh} x{n} vs light tank hull (does not fire, its driver cannot die)", sh, n, "light", 1, true, true, false, 1800, "stand:100");
                }
            if (On("C"))
            {
                say("CONTROL | shooter | target | fights | engaged | shots / min (mean) | hits / min (mean) | HP / min: mean / median / min / max | target's real HP | % of its HP per minute (mean) | seconds to destroy it (mean, from the rate)");
                foreach (var sh in new[] { "grunt", "psycho", "gatling", "jeep" })
                    foreach (var tg in new[] { "light", "jeep", "radar" })
                    {
                        var l = new List<Dpm>();
                        for (int s = 1; s <= runs; s++) l.Add(Control(settings, s, sh, tg));
                        double hp = l.Average(x => x.hpPerMin);
                        say($"CONTROL | {sh} | {tg} (hull only, no driver exposure) | {l.Count} | {l.Count(x => x.engaged)} | {F1(l.Average(x => x.shots))} | {F1(l.Average(x => x.hits))} | {Stat(l.Select(x => x.hpPerMin))} | {l[0].maxHp} | {F(l.Average(x => x.hullPct))} % | {(hp > 0 ? F(60.0 * l[0].maxHp / hp) : "never")}");
                    }
            }
        }

        // ------------------------------------------------------------------ DV212: the HP of one hit (the parity row's probe)
        public sealed class HitProbe { public int hits, odd, shots; public long lost; public readonly SortedSet<int> values = new SortedSet<int>(); public string Values => values.Count == 0 ? "none" : string.Join("/", values); }

        /// <summary>One frozen shooter on one frozen, silent target that cannot die (its health 10^9, its driver never hit) for some seconds:
        /// the HP each hit removed, tick by tick (the target's health drop / the hits of that tick; a drop that does not divide = odd).</summary>
        public static HitProbe Probe(string settings, int seed, string shooter, string target, double seconds = 20)
        {
            var w = NewWorld(settings, seed);
            var sh = PlaceOne(w, shooter, AX, Y, Team.Red, 1);
            foreach (var u in OzV11Kit.Squad(sh)) { u.MoveSpeed = 0; u.RealMoveSpeed = 0; }
            var t = PlaceOne(w, target, AX + 90, Y, Team.Blue, 1);
            t.MoveSpeed = 0; t.RealMoveSpeed = 0; t.Damage = 0; t.CanBeSnipedFlag = false; t.MaxHealth = t.Health = Big;
            foreach (var m in t.Minions) { m.SetHealth(0); w.UpdateObjectHealth(m, -1); }
            OzV11Kit.PlayerAttack(w, sh, t);
            var r = new HitProbe();
            while (w.Time < seconds)
            {
                int before = t.Health;
                w.Tick(ZConst.TICK);
                int n = 0;
                foreach (var e in w.Events)
                    if ((e.type == ZEventType.Hitscan || e.type == ZEventType.MissileFired) && e.objId == sh.Id && e.otherId == t.Id) { r.shots++; if (e.type == ZEventType.Hitscan && e.flag) n++; }
                w.Events.Clear();
                int drop = before - t.Health;
                r.hits += n; r.lost += drop;
                if (n > 0 && drop > 0 && drop % n == 0) r.values.Add(drop / n);
                else if (n > 0 || drop != 0) r.odd++;
            }
            return r;
        }

        /// <summary>The switches View/ZGame.cs turns on at a level start with the default OPTIONS (no 'Zod exact'), as the 1.5 kits' harnesses
        /// list them (kit_tools/harness/game_switches.txt), DV211 included; CalmStartDeviation too. DV212 itself is held by each row (off, then on).
        /// Set inside the caller's OzV11Kit.GameSwitches(), which puts every switch back.</summary>
        public static readonly string[] GameSwitchNames =
        {
            "AheadWalkDeviation", "ApcCrewDeviation", "ApcSnipeDeviation", "ApproachDeviation", "BigTankRamFirstDeviation", "BigTankWreckDeviation",
            "BizonAmphibDeviation", "BizonCrushDeviation", "BizonDeviation", "BoardNearestDeviation", "BotCommitDeviation", "BotFinishDeviation",
            "BotHeavyDeviation", "BotLaserDeviation", "BuildingReachDeviation", "CalmFollowDeviation", "CalmMoveDeviation", "CaptureRunDeviation",
            "CarParkBotDeviation", "ClearanceDeviation", "CloseGrabDeviation", "CpuDefendDeviation", "CpuRepairDeviation", "CpuShuttleDeviation",
            "CraneAnyRuinDeviation", "CraneFixDeviation", "CraneGunDeviation", "CraneSnipeDeviation", "CrushRollOffDeviation",
            "DetailPropsDeviation", "DragonDeviation", "DriverFireDeviation", "DriverThrownDeviation", "DuelDeviation", "EjectLockDeviation",
            "ElectrobotDeviation", "EmptiedTargetsDeviation", "EnterFallbackDeviation", "EvilZodDeviation", "ExitLanesDeviation",
            "ExposedDriverDeviation", "FireOnMoveDeviation", "FlagStepOffDeviation", "FormationDeviation", "FortAutoRushDeviation",
            "FortDefenderDeviation", "FortGunDeviation", "FortMortarDeviation", "FortRunDeviation", "FortRushDeviation", "FortTurretRofDeviation",
            "GatlingBuffDeviation", "GrabCertainDeviation", "GrabPolicyDeviation", "GrenadeBoostDeviation", "GrenadeRuleDeviation",
            "GroundFireDeviation", "HarbourWrecksDeviation", "HeavyFallDeviation", "HeavyPairDeviation", "HeavyRockDeviation", "HelpDeviation",
            "HeroBoostDeviation", "HeroSquadDeviation", "KnockHoldDeviation", "L16PyramidsDeviation", "L16ShuttleDeviation", "L17ShuttlesDeviation",
            "LosDeviation", "MTankDeviation", "MammothDeviation", "MammothRamDeviation", "MechHandGunDeviation", "MechHpDeviation",
            "MechMixDeviation", "MissileTurretBuildTimeDeviation", "NoAutoFlagUnderFireDeviation", "NoTankDuelVsJeep", "OrderMoveFallbackDeviation",
            "ParkedPathDeviation", "PremiumDodgeDeviation", "ProneTargetDeviation", "PyroForceBurnDeviation", "RamChainDeviation",
            "RamDebrisDeviation", "RamDuelDeviation", "RamFightDeviation", "RamPolishDeviation", "RepairBayFireDeviation", "RepairInPlaceDeviation",
            "RepeatQueueDeviation", "RiflePairDeviation", "RockRoundDeviation", "RocketSpeedDeviation", "RunOverDeviation", "ShiftAppendDeviation",
            "ShuttleNearestLandingDeviation", "SnipeDeviation", "SnipeSafeSpotDeviation", "SniperBalanceDeviation", "SniperHeadshotDeviation",
            "SniperSteadyDeviation", "SprintOrderDeviation", "ToughPrepareDeviation", "ToughSteadyDeviation", "TowerAnswerDeviation",
            "VehicleExitDeviation", "VeteranDeviation", "PathV3Deviation", "ElectrobotBotDeviation", "ElectrobotEscortDeviation",
            "JeepChargeDeviation", "MTankAimDeviation", "MTankHurryDeviation", "UnmannedStopDeviation", "MechGunLossDeviation",
            "ElectroSteadyDeviation", "HighGroundDeviation", "LavaDeviation", "StrikeGroupDeviation", "CpuGunManDeviation",
            "ShuttlePlayableDeviation", "SideShuttlesDeviation", "ShuttleBigCrashDeviation", "L18ContainersDeviation", "MarchGrabDeviation",
            "FortGunAnswerDeviation", "TowerDrawnReachDeviation", "BridgeHealthDeviation", "TrafficLightsKerbDeviation", "LowPitsDeviation",
            "CraneLandEndDeviation", "BigTankDriveDeviation", "BridgeDeckAimDeviation", "L18StumpsDeviation", "L18BigTanksDeviation",
            "BotBigTanksDeviation", "L18ShuttleDeviation", "BigTankUpDeviation"
        };

        static void GameOn() { foreach (var s in GameSwitchNames) OzV11Kit.TrySet(typeof(ZWorld), s, true); OzV11Kit.TrySet(typeof(ZWorld), "CalmStartDeviation", true); }

        /// <summary>DV212 part 1 table: the HP a body hit removes from a robot on foot with the rule on (when above today's).</summary>
        public static int Expected(string shooter, string target) => ZWorld.SmallArmsRobotHit(shooter, target);

        static readonly (string a, string t)[] ProbePairs =
        {
            ("grunt", "grunt"), ("grunt", "psycho"), ("grunt", "tough"), ("grunt", "sniper"), ("grunt", "pyro"), ("grunt", "laser"),
            ("psycho", "grunt"), ("psycho", "psycho"), ("psycho", "tough"), ("jeep", "grunt"), ("jeep", "psycho"), ("gatling", "grunt"), ("gatling", "tough"),
        };

        static string Same(Fight x) => $"{x.firstShot:0.###}|{x.firstKill:0.###}|{x.wipe:0.###}|{x.sniped:0.###}|{x.hullDead:0.###}|{x.winner}|{x.a.shots}|{x.a.hits}|{x.a.driverHits}|{x.a.dealt}|{x.b.shots}|{x.b.hits}|{x.b.driverHits}|{x.b.dealt}";

        static List<Fight> Many(string settings, int runs, string a, int an, string b, int bn, string mode, int dist, bool oneSided, bool bImm = false, double limit = -1)
        {
            var l = new List<Fight>();
            for (int s = 1; s <= runs; s++) l.Add(Run(settings, s, a, an, b, bn, mode, dist, oneSided, bImm, false, limit));
            return l;
        }

        static double MeanWipe(List<Fight> fs) { var l = fs.Where(x => x.wipe >= 0 && x.firstShot >= 0).Select(x => x.wipe - x.firstShot).ToList(); return l.Count == 0 ? -1 : l.Average(); }

        /// <summary>The parity row's probe (DV212, a few seconds): the HP per hit off / on for every pair, hulls / buildings / drivers the
        /// same off and on, the fights shorter, part 2 off, deterministic. The caller sets the game's switches (OzV11Kit.GameSwitches);
        /// this holds DV212 itself and puts it back.</summary>
        public static string Quick(string settings) => Rows(settings, 6);

        /// <summary>The whole check: Quick with 20 fights a row + the owner's table (before -> after) + the hero squad.</summary>
        public static string Check(string settings)
        {
            var sb = new StringBuilder(Rows(settings, 20));
            sb.AppendLine().AppendLine("The owner's table (20 fights a row, DV212 off -> on; seconds from the first shot):");
            OwnerTable(settings, 20, s => sb.AppendLine(s));
            return sb.ToString();
        }

        static string Rows(string settings, int runs)
        {
            var sb = new StringBuilder();
            GameOn();
            sb.AppendLine($"DV212 small arms against robots on foot (kit small_arms_balance_r8), {runs} fights a row; switches: RiflePair (DV78) {ZWorld.RiflePairDeviation}, DC2 {ZWorld.SnipeDeviation}, DV170 {ZWorld.GatlingBuffDeviation}");
            // 1 the HP of one hit, off and on
            bool hitsOk = true; var hl = new List<string>();
            foreach (var (a, t) in ProbePairs)
            {
                HitProbe off, on;
                using (ZWorld.SmallArmsOff()) off = Probe(settings, 1, a, t);
                using (ZWorld.SmallArmsOn()) on = Probe(settings, 1, a, t);
                int today = off.values.Count == 1 ? off.values.Min : -1, want = today < 0 ? -1 : Math.Max(today, Expected(a, t));
                bool ok = off.hits > 0 && off.odd == 0 && on.odd == 0 && off.values.Count == 1 && on.values.Count == 1 && on.values.Min == want;
                hitsOk &= ok;
                hl.Add($"{a}>{t} {off.Values} -> {on.Values}{(ok ? "" : " (want " + want + ", odd " + off.odd + "/" + on.odd + ")")}");
            }
            sb.AppendLine(OzV11Kit.Verdict(hitsOk, "HP of one body hit on a robot, DV212 off -> on (want the table: grunt>grunt 155, grunt>other robots 100, psycho 40, jeep gun 45, gatling 75, never below today): " + string.Join(", ", hl)));
            // 2 hulls and buildings: the same off and on, bit for bit
            bool hullOk = true; var cl = new List<string>();
            foreach (var sh in new[] { "grunt", "psycho", "jeep", "gatling" })
                foreach (var tg in new[] { "jeep", "light", "radar" })
                {
                    Dpm off, on;
                    using (ZWorld.SmallArmsOff()) off = Control(settings, 1, sh, tg, 30);
                    using (ZWorld.SmallArmsOn()) on = Control(settings, 1, sh, tg, 30);
                    bool ok = off.hpPerMin.Equals(on.hpPerMin) && off.shots == on.shots && off.hits == on.hits && (tg != "radar" ? off.hpPerMin > 0 : off.hpPerMin.Equals(0.0));
                    hullOk &= ok;
                    cl.Add($"{sh}>{tg} {F1(off.hpPerMin)}{(ok ? "" : " -> " + F1(on.hpPerMin) + " DIFFERS")}");
                }
            sb.AppendLine(OzV11Kit.Verdict(hullOk, "hull / building damage the same with DV212 on (HP per minute, 30 s, hull only; bullets cannot target a radar: 0 both): " + string.Join(", ", cl)));
            // 3 drivers: a driver snipe the same off and on (crew hits unchanged for every shooter)
            bool drvOk = true; var dl = new List<string>();
            foreach (var (sh, n) in new[] { ("grunt", 3), ("grunt", 1), ("psycho", 3), ("jeep", 1), ("gatling", 1) })
            {
                List<Fight> off, on;
                using (ZWorld.SmallArmsOff()) off = Many(settings, Math.Min(runs, 6), sh, n, "jeep", 1, "stand", 100, true, false, 240);
                using (ZWorld.SmallArmsOn()) on = Many(settings, Math.Min(runs, 6), sh, n, "jeep", 1, "stand", 100, true, false, 240);
                bool ok = off.Select(Same).SequenceEqual(on.Select(Same)) && off.Any(x => x.sniped >= 0);
                drvOk &= ok;
                dl.Add($"{sh} x{n}: sniped in {off.Count(x => x.sniped >= 0)} of {off.Count}, mean {F1(off.Where(x => x.sniped >= 0).Select(x => x.Rel(x.sniped)).DefaultIfEmpty(-1).Average())} s{(ok ? "" : " DIFFERS")}");
            }
            sb.AppendLine(OzV11Kit.Verdict(drvOk, "driver sniping the same with DV212 on (a silent jeep, its driver can be hit; every fight identical: shots, hits, driver hits, snipe time): " + string.Join("; ", dl)));
            // 4 the fights shorter
            bool fastOk = true; var fl = new List<string>();
            foreach (var (label, a, an, b, bn, os) in new[] { ("1 grunt on a silent grunt", "grunt", 1, "grunt", 1, true), ("grunt 1v1", "grunt", 1, "grunt", 1, false), ("grunt 3v3", "grunt", 3, "grunt", 3, false),
                                                             ("1 grunt on a silent psycho", "grunt", 1, "psycho", 1, true), ("psycho on a silent grunt", "psycho", 1, "grunt", 1, true),
                                                             ("psycho 3v3", "psycho", 3, "psycho", 3, false), ("jeep gun on a silent grunt squad", "jeep", 1, "grunt", 3, true), ("gatling on a silent grunt squad", "gatling", 1, "grunt", 3, true) })
            {
                List<Fight> off, on;
                using (ZWorld.SmallArmsOff()) off = Many(settings, runs, a, an, b, bn, "stand", 100, os);
                using (ZWorld.SmallArmsOn()) on = Many(settings, runs, a, an, b, bn, "stand", 100, os);
                double mo = MeanWipe(off), mn = MeanWipe(on);
                bool ok = mo > 0 && mn > 0 && mn < mo;
                fastOk &= ok;
                fl.Add($"{label} {F1(mo)} -> {F1(mn)} s");
            }
            sb.AppendLine(OzV11Kit.Verdict(fastOk, "the fights shorter with DV212 on (mean s from the first shot to the wipe, standing 100 px): " + string.Join(", ", fl)));
            // 5 part 2 off, the hold puts the switches back
            bool w0 = ZWorld.SmallArmsDeviation, z0 = ZWorld.SmallArmsZ95Deviation; bool inside;
            using (ZWorld.SmallArmsOn()) inside = ZWorld.SmallArmsDeviation && !ZWorld.SmallArmsZ95Deviation;
            bool p2Ok = inside && !ZWorld.SmallArmsZ95Deviation && ZWorld.SmallArmsDeviation == w0 && ZWorld.SmallArmsZ95Deviation == z0;
            sb.AppendLine(OzV11Kit.Verdict(p2Ok, $"part 2 (SmallArmsZ95Deviation, the owner's Z idea) OFF here and in the game (View/ZGame.cs sets it off); the holds put both switches back (DV212 now {ZWorld.SmallArmsDeviation})"));
            // 6 deterministic
            Fight d1, d2;
            using (ZWorld.SmallArmsOn()) { d1 = Run(settings, 3, "psycho", 3, "grunt", 3, "order", 0, false); d2 = Run(settings, 3, "psycho", 3, "grunt", 3, "order", 0, false); }
            sb.AppendLine(OzV11Kit.Verdict(SameText(d1) == SameText(d2) && d1.wipe >= 0,
                $"deterministic: the same psycho squad vs grunt squad fight on orders twice with DV212 on gives the same shots, hits, HP and times ({SameText(d1)})"));
            return sb.ToString();
        }

        static string SameText(Fight x) => $"{x.firstShot:0.###}|{x.firstKill:0.###}|{x.wipe:0.###}|{x.winner}|{x.a.shots}|{x.a.hits}|{x.a.dealt}|{x.b.shots}|{x.b.hits}|{x.b.dealt}";

        /// <summary>The owner's table: the matchups of his four asks, DV212 off -> on (mean seconds from the first shot to the wipe, wins).</summary>
        public static void OwnerTable(string settings, int runs, Action<string> say)
        {
            GameOn();
            say("matchup | mode | DV212 off: wipe s (mean) | A wins | DV212 on: wipe s (mean) | A wins | first kill s off -> on");
            void T(string label, string a, int an, string b, int bn, string mode, int dist, bool os, bool bImm = false, double limit = -1)
            {
                List<Fight> off, on;
                using (ZWorld.SmallArmsOff()) off = Many(settings, runs, a, an, b, bn, mode, dist, os, bImm, limit);
                using (ZWorld.SmallArmsOn()) on = Many(settings, runs, a, an, b, bn, mode, dist, os, bImm, limit);
                double fk(List<Fight> fs) => fs.Where(x => x.firstKill >= 0).Select(x => x.Rel(x.firstKill)).DefaultIfEmpty(-1).Average();
                say($"{label} | {(mode == "stand" ? "stand " + dist + " px" : "order")}{(os ? " one-sided" : "")} | {F1(MeanWipe(off))} | {off.Count(x => x.winner == "A")} of {runs} | {F1(MeanWipe(on))} | {on.Count(x => x.winner == "A")} of {runs} | {F1(fk(off))} -> {F1(fk(on))}");
            }
            T("1 grunt kills a grunt that does not fire back", "grunt", 1, "grunt", 1, "stand", 100, true);
            T("grunt 1v1", "grunt", 1, "grunt", 1, "stand", 100, false);
            T("grunt 3v3", "grunt", 3, "grunt", 3, "stand", 100, false);
            T("grunt 3v3", "grunt", 3, "grunt", 3, "order", 0, false);
            T("1 grunt on a psycho that does not fire back", "grunt", 1, "psycho", 1, "stand", 100, true);
            T("jeep gun on a grunt squad that does not fire back", "jeep", 1, "grunt", 3, "stand", 100, true);
            T("jeep vs a grunt squad", "jeep", 1, "grunt", 3, "stand", 100, false);
            T("jeep vs a grunt squad", "jeep", 1, "grunt", 3, "order", 0, false);
            T("psycho on a grunt that does not fire back", "psycho", 1, "grunt", 1, "stand", 100, true);
            T("psycho squad vs psycho squad", "psycho", 3, "psycho", 3, "stand", 100, false);
            T("gatling vs a grunt squad", "gatling", 1, "grunt", 3, "stand", 100, false);
            T("gatling on a psycho squad that does not fire back", "gatling", 1, "psycho", 3, "stand", 100, true);
            T("grunt squad snipes a jeep's driver (jeep silent)", "grunt", 3, "jeep", 1, "stand", 100, true, false, 240);
            T("ALAN & BRAD hero squad vs a Tough squad", ZHeroSquads.AlanBrad, 1, "tough", 2, "stand", 100, false);
            T("ALAN & BRAD hero squad vs a Tough squad", ZHeroSquads.AlanBrad, 1, "tough", 2, "order", 0, false);
            T("ALAN & BRAD hero squad vs a grunt squad", ZHeroSquads.AlanBrad, 1, "grunt", 3, "order", 0, false);
        }

        // ------------------------------------------------------------------ the numbers the sim uses (game switches on)
        public static string Numbers(string settings)
        {
            var sb = new StringBuilder();
            var w = ZWorld.Create(settings, OzV11Kit.Plain(32, 32), 1);
            sb.AppendLine("type | kind | MaxHealth | Damage per hit (object) | attack_speed s | hit chance | range px | splash px | snipe chance | CanSnipe | CanBeSniped flag | HasLid | missile | group");
            foreach (var t in new[] { "grunt", "tough", "sniper", "pyro", "psycho", "laser", "electrobot", "jeep", "light", "medium", ZMTank.Type, "heavy", ZBigTanks.Mammoth, ZBigTanks.Bizon, "apc", "missile_launcher", "crane", "gatling", "gun", "howitzer", "missile_cannon" })
            {
                var u = w.S.Unit(t); if (u == null) { sb.AppendLine(t + " | (no row)"); continue; }
                bool robot = ZBuildLists.IsRobot(t);
                var o = robot ? w.CreateObject("robot", t, 64, 64, Team.Red) : w.CreateObject(ZBuildLists.IsCannon(t) ? "cannon" : "vehicle", t, 128, 128, Team.Red);
                sb.AppendLine($"{t} | {o.Kind} | {o.MaxHealth} | {o.Damage} | {o.AttackSpeed.ToString(CultureInfo.InvariantCulture)} | {o.DamageChance.ToString(CultureInfo.InvariantCulture)} | {o.AttackRadius} | {o.DamageRadius} | {o.SnipeChance.ToString(CultureInfo.InvariantCulture)} | {o.CanSnipe} | {o.CanBeSnipedFlag} | {o.HasLid} | {o.DamageIsMissile} | {u.group_amount}" +
                              (o.Drivers.Count > 0 ? $" | driver {o.DriverType} {o.Drivers[0]} HP" : ""));
            }
            sb.AppendLine("RiflePair (DV78, robot shooter on a robot): " + string.Join(", ", ZWorld.RiflePair.Select(kv => $"{kv.Key.attacker}>{kv.Key.target}={kv.Value}")));
            sb.AppendLine("DriverHitFloor (DC2): " + string.Join(", ", ZWorld.DriverHitFloor.Select(kv => $"{kv.Key}={kv.Value}")) + "; SniperDriverFactor " + ZWorld.SniperDriverFactor);
            sb.AppendLine("OpenSeatPair (DC2): " + string.Join(", ", ZWorld.OpenSeatPair.Select(kv => $"{kv.Key.attacker}>{kv.Key.target}=({kv.Value.hit}, {kv.Value.chance.ToString(CultureInfo.InvariantCulture)})")));
            sb.AppendLine($"switches: RiflePair {ZWorld.RiflePairDeviation}, Snipe(DC2) {ZWorld.SnipeDeviation}, GatlingBuff(DV170) {ZWorld.GatlingBuffDeviation}, ArmourChip(DV29) {ZWorld.ArmourChipDeviation}, ExposedDriver(DV27) {ZWorld.ExposedDriverDeviation}, Veteran(DV12) {ZWorld.VeteranDeviation}, GroundFire(DV117) {ZWorld.GroundFireDeviation}, SniperBalance {ZWorld.SniperBalanceDeviation}, SniperHeadshot(DV116) {ZWorld.SniperHeadshotDeviation}, MechHp(DV147) {ZWorld.MechHpDeviation}, BigTankUp(DV211) {ZWorld.BigTankUpDeviation}");
            return sb.ToString();
        }
    }
}
