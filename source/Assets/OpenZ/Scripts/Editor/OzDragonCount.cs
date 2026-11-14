using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// DV163, the level 7 dragons' attack: the counted check of the rule (Sim/ZWorld.Dragons.cs). Headless: ZWorld.Create + Tick, no scene, no Play, nothing saved. The orders here are
    /// what the view would send; where the rule needs time the clock is put next to the moment in a quiet world, the Play checks run the true clock.
    ///   A  rule on, no orders = rule off: fingerprint of every tick, 3 minutes, a computer match on level 7
    ///   B  the same order list twice: the same fingerprint every tick
    ///   C  refusals: first minute, rule off, no lava, vehicle, landed shuttle, a pass running, map cooldown, own cooldown, game over; the chance
    ///   D  the pass in time: flame on, off, 9 bites, the end
    ///   E  fire from the top: the angle of every bite, a robot off to the side gets none; E2 the flame on the squad: every robot within 2.5 m of its end, both teams;
    ///      E3 out of the cone it falls on the dragon's own line and still burns a robot there, never a jeep
    ///   F  no full-health robot of any type dies of one pass; a hurt one can: a fire death with no killer
    ///   G  nobody is pulled off a job: a squad walking under a pass, same place, orders and target tick by tick as without it; a robot in a real fight fires no shot at it
    ///   H  Zod's rand: not one draw more or less
    ///   I  kills over hash seeds: 1 robot never, 3 grunts about a coin flip, 6 mixed nearly always
    ///   J  walking: a Tough and a Pyro do not stop or fire, a grunt fires; a knocked-down robot does not
    ///   K  shuttle: bites only in the air, 18 % at most, no shots from the ground; a pass near another ship breaks off at 14 m; K2 to K5 the hated shuttle: its own chance,
    ///      its own shorter map gap (robots keep theirs), never under a quarter of its health
    ///   L  levels 2, 16 and 17: rule on and off the same, every order refused
    ///   M  the burning ground under the flame bites a robot that stays, never a vehicle, no kill credit, a healthy robot still lives; the Electrobot shoots back and his real gun stays charged
    /// Menu: OpenZ/L07 Lava/Checks/Dragons/0 Rule count (no Play). The last line reads "PASS n/n" when every line passed. Short form (whole = false, the parity report's row): no A, I, L. Also built off-Unity by the kit's tools/rehearse.py.
    /// </summary>
    public static class OzDragonCount
    {
        const double Tick = ZConst.TICK;
        const string Level = "L07";

        static void Set(ZDragonOrder q, int i, double x, double y, double z) { q.Path[i * 3] = x; q.Path[i * 3 + 1] = y; q.Path[i * 3 + 2] = z; }

        /// <summary>The view's dip over world point (x, z), along heading (ax, az): from soaring height behind it to 9.5 m over it, and out.</summary>
        static ZDragonOrder Dip(int dragon, int target, double x, double z, double ax = 1, double az = 0)
        {
            var q = new ZDragonOrder { Dragon = dragon, Target = target, T1 = 3.2, T2 = 3.4, GroundY = 0 };
            x -= ax * ZWorld.DragonFireShortM; z -= az * ZWorld.DragonFireShortM;                       // the view's fire point: short of the target on the line
            Set(q, 0, x - ax * 20, 30, z - az * 20); Set(q, 1, ax * 7.8, 0, az * 7.8); Set(q, 2, x, 9.5, z); Set(q, 3, ax * 6, 0, az * 6);
            Set(q, 4, x + ax * 30, 19.5, z + az * 30); Set(q, 5, ax * 8, 3.5, az * 8);
            return q;
        }
        static ZDragonOrder Dip(int dragon, ZObject t, double ax = 1, double az = 0) => Dip(dragon, t.Id, t.CenterX / 8.0, -t.CenterY / 8.0, ax, az);

        /// <summary>The view's level pass over a ship at the dragon's own height.</summary>
        static ZDragonOrder Level_(int dragon, ZObject ship, double y)
        {
            double x = ship.CenterX / 8.0 - ZWorld.DragonFireShortM, z = -ship.CenterY / 8.0;
            var q = new ZDragonOrder { Dragon = dragon, Target = ship.Id, T1 = 3, T2 = 3.4, GroundY = 0 };
            Set(q, 0, x - 20, y, z); Set(q, 1, 7.8, 0, 0); Set(q, 2, x, y, z); Set(q, 3, 6, 0, 0); Set(q, 4, x + 24, y + 2, z); Set(q, 5, 8, 0, 0);
            return q;
        }

        static void RunUntil(ZWorld w, Func<bool> done, double seconds)
        {
            double end = w.Time + seconds;
            while (w.Time < end && !done()) { w.Tick(Tick); w.Events.Clear(); }
        }
        static void RunFor(ZWorld w, double seconds) => RunUntil(w, () => false, seconds);
        static void RunPass(ZWorld w) { w.Tick(Tick); RunUntil(w, () => w.DragonPass == null, 12); }

        /// <summary>The middle of an open patch of ground, tiles walkable for robots all round, no lava: (px, py).</summary>
        public static (int x, int y) Open(ZWorld w, int r = 9)
        {
            int cx = w.Map.width / 2, cy = w.Map.height / 2;
            for (int ring = 0; ring < 60; ring++)
                for (int ty = cy - ring; ty <= cy + ring; ty++)
                    for (int tx = cx - ring; tx <= cx + ring; tx++)
                    {
                        if (Math.Max(Math.Abs(tx - cx), Math.Abs(ty - cy)) != ring) continue;
                        bool ok = tx > r && ty > r && tx < w.Map.width - r && ty < w.Map.height - r;
                        for (int j = -r; j <= r && ok; j++) for (int i = -r; i <= r && ok; i++) ok = w.Path.TilePassable(tx + i, ty + j, true) && !w.Map.LavaAt(tx + i, ty + j);
                        if (ok) return (tx * 16 + 8, ty * 16 + 8);
                    }
            return (w.Map.width * 8, w.Map.height * 8);
        }

        /// <summary>A quiet level 7 with no units but the blue dummy, the rule on, past the first minute.</summary>
        static ZWorld Quiet(string settings, string json, int seed, bool past = true)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.NoUnits(json), seed);
            var (x, y) = Open(w);
            OzV11Kit.Dummy(w, 32, 32);
            ZWorld.DragonDeviation = true;
            if (past) w.Time = ZWorld.DragonFirstSeconds + 1;
            return w;
        }

        /// <summary>One robot that stands where it is put (an idle robot of the player walks off to a flag or a crate by itself).</summary>
        static ZObject Robot(ZWorld w, string type, int x, int y, Team t = Team.Red, bool stand = true) { var o = w.CreateRobotGroup(type, x - 8, y - 8, t, 1); if (stand) w.SendHold(t, o); return o; }

        public static string Run(string settings, Func<string, string> mapText, bool whole = true)
        {
            var sb = new StringBuilder(); int pass = 0, total = 0;
            void V(bool ok, string what) { total++; if (ok) pass++; sb.AppendLine(OzV11Kit.Verdict(ok, what)); }
            bool ruleWas = ZWorld.DragonDeviation;
            string json = mapText(Level);
            var types = new[] { "grunt", "psycho", "sniper", "tough", "pyro", "laser", "electrobot" };
            try
            {
                // ---- A
                if (whole)
                {
                    ulong[] a = new ulong[18000], b = new ulong[18000];
                    for (int round = 0; round < 2; round++)
                    {
                        ZWorld.DragonDeviation = round == 1;
                        var w = ZWorld.Create(settings, json, 5);
                        w.Bots.Add(new ZBot(w, Team.Blue, 102)); w.Bots.Add(new ZBot(w, Team.Red, 1));
                        var o = round == 0 ? a : b; ulong h = 1469598103934665603UL;
                        for (int i = 0; i < o.Length; i++) { w.Tick(Tick); w.Events.Clear(); h = w.RulesFingerprint(h); o[i] = h; }
                    }
                    int bad = 0; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) bad++;
                    V(bad == 0, $"A rule on with no orders = rule off, computer match on level 7: {a.Length} ticks, {bad} differ, end {b[b.Length - 1]:X}");
                }
                // ---- B
                {
                    var run = new List<ulong>[2]; var ends = new string[2];
                    for (int round = 0; round < 2; round++)
                    {
                        var w = Quiet(settings, json, 7);
                        var (x, y) = Open(w);
                        var rs = new List<ZObject>(); string[] mix = { "grunt", "psycho", "laser", "pyro", "tough", "sniper" };
                        for (int i = 0; i < 6; i++) rs.Add(Robot(w, mix[i], x + 16 * (i - 3), y + 24));
                        w.DragonProofForce = true;
                        var list = new List<ulong>(); ulong h = 7; int asked = 0;
                        for (int i = 0; i < 4000; i++)
                        {
                            if (i == 100 || i == 1500 || i == 3000) { var t = rs[asked++ % rs.Count]; if (!t.Destroyed) w.DragonOrder(Dip(asked, t)); }
                            w.Tick(Tick); w.Events.Clear(); h = w.RulesFingerprint(h); list.Add(h);
                        }
                        run[round] = list; ends[round] = $"{w.DragonOrders} orders, {w.DragonBitesHit} bites, {w.DragonShotsFired} shots, {w.DragonShotsHit} hits, {w.DragonKills} kills";
                    }
                    int bad = 0; for (int i = 0; i < run[0].Count; i++) if (run[0][i] != run[1][i]) bad++;
                    V(bad == 0 && ends[0] == ends[1], $"B the same orders twice: {run[0].Count} ticks, {bad} differ; {ends[0]}");
                }
                // ---- C
                {
                    var w = Quiet(settings, json, 9, false);
                    var (x, y) = Open(w);
                    var r = Robot(w, "grunt", x, y); var v = OzV11Kit.Place(w, "jeep", x + 96, y, Team.Red);
                    w.Time = 59.5; int early = w.DragonOrder(Dip(0, r));
                    RunUntil(w, () => w.Time >= 59.985, 2);
                    int stillEarly = w.DragonOrder(Dip(0, r));
                    V(early == ZWorld.WhyEarly && stillEarly == ZWorld.WhyEarly, $"C order at {59.5} s and at {59.99:F2} s: {ZWorld.DragonWhyName[early]}, {ZWorld.DragonWhyName[stillEarly]}");
                    w.DragonProofForce = true;
                    w.Time = ZWorld.DragonFirstSeconds; int at60 = w.DragonOrder(Dip(0, r));
                    V(at60 == 0, $"C order at 60.00 s: {ZWorld.DragonWhyName[at60]}");
                    int busy = w.DragonOrder(Dip(1, r));
                    V(busy == ZWorld.WhyBusy, $"C a second pass while one runs: {ZWorld.DragonWhyName[busy]}");
                    RunPass(w); w.DragonProofForce = false;
                    int veh = w.DragonOrder(Dip(0, v));
                    V(veh == ZWorld.WhyTarget, $"C a vehicle as target: {ZWorld.DragonWhyName[veh]}");
                    using (ZWorld.DragonOff()) V(w.DragonOrder(Dip(0, r)) == ZWorld.WhyOff, "C rule off ('Zod exact', headless): refused");
                    w.DragonProofForce = true; w.DragonOrder(Dip(0, r)); RunPass(w); w.DragonProofForce = false;
                    // the cooldowns: the first order the chance lets through sets both
                    var w2 = Quiet(settings, json, 10); var (x2, y2) = Open(w2); var r2 = Robot(w2, "grunt", x2, y2);
                    int guard = 0; while (!ZWorld.DragonRoll(w2.TickIndex, 0, r2.Id) && guard++ < 5000) { w2.Tick(Tick); w2.Events.Clear(); }
                    int first = w2.DragonOrder(Dip(0, r2)); RunPass(w2);
                    guard = 0; while (!ZWorld.DragonRoll(w2.TickIndex, 1, r2.Id) && guard++ < 5000) { w2.Tick(Tick); w2.Events.Clear(); }
                    int mapCool = w2.DragonOrder(Dip(1, r2));
                    w2.Time += ZWorld.DragonCoolMap + 1;
                    int ownCool = w2.DragonOrder(Dip(0, r2));
                    w2.Time += ZWorld.DragonCoolOwn;
                    guard = 0; while (ZWorld.DragonRoll(w2.TickIndex, 0, r2.Id) && guard++ < 5000) { w2.Tick(Tick); w2.Events.Clear(); }
                    int roll = w2.DragonOrder(Dip(0, r2)), afterRoll = w2.DragonOrder(Dip(0, r2));
                    V(first == 0 && mapCool == ZWorld.WhyMapCool, $"C map cooldown {ZWorld.DragonCoolMap} s: {ZWorld.DragonWhyName[first]}, then {ZWorld.DragonWhyName[mapCool]}");
                    V(ownCool != ZWorld.WhyMapCool && ownCool != ZWorld.WhyOwnCool, $"C the map gap {ZWorld.DragonCoolMap} s is longer than a dragon's own {ZWorld.DragonCoolOwn} s: after it both are over: {ZWorld.DragonWhyName[ownCool]}");   // the own gap alone shows on a lost roll, the next row
                    V(roll == ZWorld.WhyRoll && afterRoll == ZWorld.WhyOwnCool, $"C a lost roll still costs the dragon its cooldown: {ZWorld.DragonWhyName[roll]}, then {ZWorld.DragonWhyName[afterRoll]}");
                    int yes = 0; for (int i = 0; i < 20000; i++) if (ZWorld.DragonRoll(i * 37 + 11, i % 3, 100 + i % 200)) yes++;
                    V(yes > 9400 && yes < 10600, $"C the chance over 20000 rolls: {yes / 200.0:F1} %, wanted about {ZWorld.DragonChancePerMille / 10.0} %");
                    var w3 = OzV11Kit.World(settings, OzV11Kit.NoUnits(json), 12); ZWorld.DragonDeviation = true; w3.Time = 100;
                    var (x3, y3) = Open(w3); var r3 = Robot(w3, "grunt", x3, y3);
                    RunUntil(w3, () => w3.GameOver, 6);
                    V(!w3.GameOver || w3.DragonOrder(Dip(0, r3)) == ZWorld.WhyGameOver, $"C game over: {(w3.GameOver ? "refused" : "the world did not end, not measured")}");
                }
                // ---- D, E
                {
                    var w = Quiet(settings, json, 13); var (x, y) = Open(w);
                    var r = Robot(w, "grunt", x, y);
                    w.DragonProofForce = true;
                    double t0 = w.Time; w.DragonOrder(Dip(0, r)); w.DragonProofForce = false;
                    var seen = new List<(ZDragonKind k, double t)>(); float maxDeg = 0; int hitN = 0;
                    w.Tick(Tick);
                    for (int i = 0; i < 1200 && (w.DragonPass != null || seen.Count == 0 || seen[seen.Count - 1].k != ZDragonKind.End); i++)
                    {
                        foreach (var e in w.Events)
                            if (e.type == ZEventType.DragonEvent)
                            {
                                var k = ZWorld.DragonKindOf(e); seen.Add((k, w.Time - t0));
                                if (k == ZDragonKind.Bite && e.flag) { hitN++; maxDeg = Math.Max(maxDeg, e.value); }
                            }
                        w.Events.Clear(); w.Tick(Tick);
                    }
                    double on = seen.Where(s => s.k == ZDragonKind.FlameOn).Select(s => s.t).DefaultIfEmpty(-1).First(), off = seen.Where(s => s.k == ZDragonKind.FlameOff).Select(s => s.t).DefaultIfEmpty(-1).First(),
                           end = seen.Where(s => s.k == ZDragonKind.End).Select(s => s.t).DefaultIfEmpty(-1).First();
                    int bites = seen.Count(s => s.k == ZDragonKind.Bite);
                    V(Math.Abs(on - 2.5) < 0.05 && Math.Abs(off - 4.3) < 0.05 && bites == ZWorld.DragonBites && Math.Abs(end - 6.6) < 0.05, $"D flame on {on:F2} s, off {off:F2} s, {bites} bites, end {end:F2} s (t1 3.2, t2 3.4)");
                    V(hitN >= 3 && maxDeg <= ZWorld.DragonConeDeg + 0.01, $"E top to bottom: {hitN} bites landed, the widest {maxDeg:F1} degrees from straight down (cone {ZWorld.DragonConeDeg})");
                    var side = Robot(w, "grunt", x, y + 96);
                    w.DragonProofForce = true; double hp0 = side.Health;
                    w.DragonOrder(Dip(0, side.Id, x / 8.0, -y / 8.0)); RunPass(w);
                    V(side.Health == hp0, $"E a robot 12 m to the side of the line: {hp0 - side.Health} HP taken");
                    var near = Robot(w, "grunt", x + 64, y + 40); hp0 = near.Health; int d0 = w.DragonBitesHit;
                    w.DragonOrder(Dip(0, near.Id, (x + 64) / 8.0, -y / 8.0)); RunPass(w);
                    V(near.Health < hp0 && w.DragonBitesHit > d0, $"E a robot 5 m to the side: {w.DragonBitesHit - d0} bites landed");
                }
                {
                    bool was = ZWorld.GroundFireDeviation; ZWorld.GroundFireDeviation = false;                 // the flame alone: the burning ground is row M
                    try
                    {
                        var got = new Dictionary<int, int>();
                        void Tally(ZEvent e) { if (e.type == ZEventType.DragonEvent && ZWorld.DragonKindOf(e) == ZDragonKind.Splash) { got.TryGetValue(e.otherId, out int c); got[e.otherId] = c + 1; } }
                        int G(ZObject o) => got.TryGetValue(o.Id, out int c) ? c : 0;
                        double P(ZObject o) => 100.0 * (1.0 - o.Health / (double)o.MaxHealth);
                        var w = Quiet(settings, json, 14); var (x, y) = Open(w);
                        var t = Robot(w, "grunt", x, y); var mate = Robot(w, "grunt", x + 16, y); var foe = Robot(w, "grunt", x, y + 16, Team.Blue); var mate2 = Robot(w, "grunt", x - 16, y);
                        var all = new[] { t, mate, foe, mate2 };
                        foreach (var o in all) o.Damage = 0;
                        w.DragonProofForce = true; int kills0 = all.Sum(o => o.Kills), dealt0 = w.DragonDealt;
                        w.DragonOrder(Dip(0, t)); RunUntilEvents(w, Tally, () => w.DragonPass == null, 12);
                        V(G(mate) >= 1 && G(foe) >= 1 && G(mate2) >= 1 && G(t) == 0 && all.All(o => !o.Destroyed && o.Health < o.MaxHealth) && all.Max(P) <= 60.0 && all.Sum(o => o.Kills) == kills0,
                          $"E2 a squad of 4 under one pass, 2 m apart: target {P(t):F1} %, mate {P(mate):F1} % ({G(mate)} splashes), enemy {P(foe):F1} % ({G(foe)}), mate {P(mate2):F1} % ({G(mate2)}), DragonDealt {w.DragonDealt - dealt0} HP, kill credit {all.Sum(o => o.Kills) - kills0}");
                        var w3 = Quiet(settings, json, 16); var (x3, y3) = Open(w3);
                        var side = Robot(w3, "grunt", x3, y3 + 96); var onLine = Robot(w3, "grunt", x3, y3); var jeep = OzV11Kit.Place(w3, "jeep", x3 + 40, y3, Team.Red); w3.SendHold(Team.Red, jeep);
                        got.Clear(); w3.DragonProofForce = true;
                        w3.DragonOrder(Dip(0, side.Id, x3 / 8.0, -y3 / 8.0)); RunUntilEvents(w3, Tally, () => w3.DragonPass == null, 12);
                        V(G(onLine) >= 1 && onLine.Health < onLine.MaxHealth && side.Health == side.MaxHealth && jeep.Health == jeep.MaxHealth,
                          $"E3 out of the cone the flame falls on the dragon's own line and still burns who stands there: the robot on the line {P(onLine):F1} % ({G(onLine)} splashes), the target 12 m off the line {P(side):F1} %, a jeep on the line {jeep.MaxHealth - jeep.Health} HP");
                    }
                    finally { ZWorld.GroundFireDeviation = was; }
                }
                // ---- F
                {
                    var w = Quiet(settings, json, 15); var (x, y) = Open(w);
                    w.DragonProofForce = true;
                    double most = 0; int dead = 0, passes = 0, full = 0; var per = new StringBuilder();
                    foreach (var ty in types)
                    {
                        double mostT = 0;
                        var r = Robot(w, ty, x, y);
                        for (int k = 0; k < 8; k++)
                        {
                            r.SetHealth(r.MaxHealth);
                            w.DragonOrder(Dip(k % 3, r, Math.Cos(k * 0.7), Math.Sin(k * 0.7))); RunPass(w); RunFor(w, 0.07 * k);
                            passes++;
                            if (r.Destroyed) { dead++; break; }
                            double took = 1.0 - r.Health / (double)r.MaxHealth;
                            if (took > 0.30) full++;
                            mostT = Math.Max(mostT, took); most = Math.Max(most, took);
                        }
                        per.Append($" {ty} {mostT * 100:F1}");
                        r.SetHealth(0); w.UpdateObjectHealth(r); RunFor(w, 0.05);
                    }
                    V(dead == 0 && most <= 0.60 && full > 0, $"F {passes} passes on full-health robots of {types.Length} types: {dead} died, most taken {most * 100:F1} % (limit 58.5 % + rounding):{per}");
                    var hurt = Robot(w, "grunt", x, y); hurt.SetHealth(hurt.MaxHealth / 10); int kills0 = hurt.Kills;
                    w.DragonOrder(Dip(0, hurt)); w.Tick(Tick);
                    bool fire = false; int credit = 0;
                    RunUntilEvents(w, e => { if (e.type == ZEventType.Death && e.objId == hurt.Id) { fire = e.flag; credit = e.otherId; } }, () => w.DragonPass == null, 12);
                    V(hurt.Destroyed && fire && credit == -1, $"F a robot at 10 % dies of it: dead {hurt.Destroyed}, a fire death {fire}, killer {credit} (no killer)");
                }
                // ---- G, H
                {
                    string[] log = new string[2]; var rand = new int[2][]; int fightShots = 0; int shotsB = 0;
                    var track = new List<string>[2];
                    for (int round = 0; round < 2; round++)
                    {
                        var w = Quiet(settings, json, 21); var (x, y) = Open(w);
                        var lead = w.CreateRobotGroup("grunt", x - 88, y - 8, Team.Red, 4);
                        var squad = OzV11Kit.Squad(lead).ToList();
                        var fighter = Robot(w, "grunt", x + 40, y + 40, Team.Red, false); var enemy = Robot(w, "grunt", x + 70, y + 40, Team.Blue, false);
                        fighter.MaxHealth = fighter.Health = enemy.MaxHealth = enemy.Health = 1000000;             // the fight lasts the whole pass
                        var target = Robot(w, "psycho", x + 24, y - 48);
                        OzV11Kit.PlayerMove(w, lead, x + 300, y, false);
                        OzV11Kit.PlayerAttack(w, fighter, enemy);
                        w.DragonProofForce = true;
                        var t = new List<string>();
                        for (int i = 0; i < 700; i++)
                        {
                            if (i == 10 && round == 1) w.DragonOrder(Dip(0, target));
                            w.Tick(Tick);
                            foreach (var e in w.Events) if (e.type == ZEventType.DragonEvent && ZWorld.DragonKindOf(e) == ZDragonKind.Shot) { if (e.objId == fighter.Id) fightShots++; if (squad.Any(s => s.Id == e.objId)) shotsB++; }
                            w.Events.Clear();
                            var b = new StringBuilder();
                            foreach (var s in squad) { b.Append(s.LocX).Append(',').Append(s.LocY).Append(',').Append(s.Waypoints.Count).Append(',').Append(s.AttackObject?.Id ?? -1).Append(';'); }
                            b.Append(fighter.LocX).Append(',').Append(fighter.AttackObject?.Id ?? -1).Append(',').Append(enemy.Health);
                            t.Add(b.ToString());
                        }
                        track[round] = t;
                        rand[round] = new[] { w.Rng.Next(), w.Rng.Next(), w.Rng.Next(), w.Rng.Next(), w.Rng.Next(), w.Rng.Next() };
                    }
                    int bad = 0; for (int i = 0; i < track[0].Count; i++) if (track[0][i] != track[1][i]) bad++;
                    V(bad == 0 && shotsB > 0, $"G a walking squad under a pass: {track[0].Count} ticks, {bad} differ from the run without it (place, orders, target); it fired {shotsB} shots on the way");
                    V(fightShots == 0, $"G a robot in a real fight fires no shot at the dragon: {fightShots}; its enemy's health stays the same tick by tick");
                    V(rand[0].SequenceEqual(rand[1]), "H Zod's rand: the same stream after a pass as without it");
                }
                // ---- I
                if (whole)
                {
                    int Kills(string[] mix, int runs, out string spread)
                    {
                        int kills = 0; var gun = new List<double>();
                        for (int s = 0; s < runs; s++)
                        {
                            var w = Quiet(settings, json, 100 + s); var (x, y) = Open(w);
                            for (int i = 0; i < mix.Length; i++) Robot(w, mix[i], x + 20 * (i - mix.Length / 2), y + (i % 2 == 0 ? 24 : -24));
                            RunFor(w, 0.01 * s);
                            w.DragonProofForce = true;
                            int t = 0; foreach (var o in w.Objects) if (o.Kind == ZKind.Robot && o.Owner == Team.Red) { t = o.Id; break; }
                            w.DragonOrder(Dip(s % 3, t, x / 8.0, -y / 8.0, Math.Cos(s * 0.9), Math.Sin(s * 0.9))); RunPass(w);
                            kills += w.DragonKills; gun.Add(w.DragonGunTaken);
                        }
                        gun.Sort();
                        spread = $"gun-seconds a pass: least {gun[0]:F2}, middle {gun[gun.Count / 2]:F2}, most {gun[gun.Count - 1]:F2}";
                        return kills;
                    }
                    const int Runs = 40;
                    int one = Kills(new[] { "grunt" }, Runs, out string h1), three = Kills(new[] { "grunt", "grunt", "grunt" }, Runs, out string h3), six = Kills(new[] { "grunt", "psycho", "laser", "pyro", "tough", "sniper" }, Runs, out string h6), seven = Kills(new[] { "grunt", "psycho", "laser", "pyro", "tough", "sniper", "electrobot" }, Runs, out string h7);
                    V(one == 0, $"I 1 grunt: {one} kills of {Runs} ({h1})");
                    foreach (var ty in new[] { "psycho", "sniper", "laser", "pyro", "tough", "electrobot" }) { int solo = Kills(new[] { ty }, 20, out string hs); V(solo == 0, $"I 1 {ty}: {solo} kills of 20 ({hs})"); }
                    V(three * 100 >= 35 * Runs && three * 100 <= 65 * Runs, $"I 3 grunts: {three * 100 / Runs} % kills ({h3}), wanted 35 to 65");
                    V(six * 100 >= 90 * Runs, $"I 6 mixed: {six * 100 / Runs} % kills ({h6}), wanted 90 or more");
                    V(seven >= six, $"I 7 mixed, the Electrobot too: {seven * 100 / Runs} % kills ({h7}), at least as many as without him");
                }
                // ---- J
                {
                    var w = Quiet(settings, json, 31); var (x, y) = Open(w);
                    var tough = Robot(w, "tough", x - 60, y + 30, Team.Red, false); var pyro = Robot(w, "pyro", x - 60, y - 30, Team.Red, false); var grunt = Robot(w, "grunt", x - 90, y, Team.Red, false);
                    var down = Robot(w, "grunt", x + 40, y + 30);
                    var target = Robot(w, "laser", x, y + 80);
                    foreach (var r in new[] { tough, pyro, grunt }) OzV11Kit.PlayerMove(w, r, r.CenterX + 240, r.CenterY, false);
                    w.Tick(Tick);
                    bool knockOn = ZWorld.KnockHoldDeviation; down.KnockUntil = w.Time + 30;
                    w.DragonProofForce = true; var shots = new Dictionary<int, int>();
                    var wps = (tough.Waypoints.Count, pyro.Waypoints.Count);
                    w.DragonOrder(Dip(0, target.Id, x / 8.0, -y / 8.0));
                    RunUntilEvents(w, e => { if (e.type == ZEventType.DragonEvent && ZWorld.DragonKindOf(e) == ZDragonKind.Shot) { shots.TryGetValue(e.objId, out int n); shots[e.objId] = n + 1; } }, () => w.DragonPass == null, 12);
                    int S(ZObject o) => shots.TryGetValue(o.Id, out int n) ? n : 0;
                    V(S(tough) == 0 && S(pyro) == 0 && wps.Item1 > 0, $"J a walking Tough fired {S(tough)}, a walking Pyro {S(pyro)}: they keep walking");
                    V(S(grunt) > 0, $"J a walking grunt fired {S(grunt)}");
                    V(!knockOn || S(down) == 0, $"J a knocked-down robot fired {S(down)} (hold rule {(knockOn ? "on" : "off")})");
                }
                // ---- K
                {
                    ZWorld.ShuttlePlayableDeviation = true;
                    var w = Quiet(settings, json, 41); var (x, y) = Open(w);
                    var s = w.SpawnShuttle(x, y, Team.Red, 0f, out string why);
                    var far = Robot(w, "grunt", x + 40, y + 40);
                    bool have = s != null;
                    V(have, $"K a shuttle on level 7 by the testing panel's rule: {(have ? "made" : why)}");
                    if (have)
                    {
                        int landed = w.DragonOrder(Level_(0, s, 30));
                        V(landed == ZWorld.WhyTarget, $"K a landed shuttle: {ZWorld.DragonWhyName[landed]}");
                        w.SendWaypoints(Team.Red, s, new[] { ZWaypoint.Move(s.CenterX + 160, s.CenterY) });
                        RunFor(w, 14);                                                                 // up, across, hovering at its height
                        double hp0 = s.Health; int shots0 = w.DragonShotsFired;
                        w.DragonProofForce = true;
                        int order = w.DragonOrder(Level_(0, s, 28));
                        RunPass(w);
                        double took = (hp0 - s.Health) / s.MaxHealth;
                        V(order == 0 && w.DragonShipBites > 0 && took <= 0.181, $"K the flying ship (height {s.ShuttleHeightM:F1} m): {ZWorld.DragonWhyName[order]}, {w.DragonShipBites} bites, {took * 100:F1} % of its hit points (18 at most)");
                        V(w.DragonShotsFired == shots0, $"K robots do not reach a dragon at 28 m: {w.DragonShotsFired - shots0} shots");
                        var r = Robot(w, "grunt", s.CenterX - 200, s.CenterY);
                        int brk0 = w.DragonBreakOffs;
                        w.DragonOrder(Dip(0, r)); RunPass(w);
                        V(w.DragonBreakOffs == brk0 + 1, $"K a dip past another flying ship breaks off at {ZWorld.DragonShipBreakM} m: {w.DragonBreakOffs - brk0} break-off");
                    }
                    ZWorld.ShuttlePlayableDeviation = false;
                }
                {
                    int yesShip = 0; for (int i = 0; i < 20000; i++) if (ZWorld.DragonRoll(i * 37 + 11, i % 3, 100 + i % 200, true)) yesShip++;
                    V(Math.Abs(yesShip - ZWorld.DragonShipChancePerMille * 20) < 600, $"K2 a flying shuttle's chance over 20000 rolls: {yesShip / 200.0:F1} %, wanted about {ZWorld.DragonShipChancePerMille / 10.0} % (a robot's {ZWorld.DragonChancePerMille / 10.0} %)");
                    ZWorld.ShuttlePlayableDeviation = true;
                    try
                    {
                        var w = Quiet(settings, json, 43); var (x, y) = Open(w);
                        var s = w.SpawnShuttle(x, y, Team.Red, 0f, out string why);
                        var r = Robot(w, "grunt", x + 40, y + 40);
                        if (s == null) V(false, $"K3 a shuttle on level 7: {why}");
                        else
                        {
                            void Roll(int dragon, int target, bool ship) { int guard = 0; while (!ZWorld.DragonRoll(w.TickIndex, dragon, target, ship) && guard++ < 5000) { w.Tick(Tick); w.Events.Clear(); } }
                            Roll(0, r.Id, false);
                            int robot1 = w.DragonOrder(Dip(0, r)); double t1 = w.Time; RunPass(w);                       // the shuttle still on the ground: nothing breaks this pass off
                            w.SendWaypoints(Team.Red, s, new[] { ZWaypoint.Move(s.CenterX + 160, s.CenterY) });
                            RunFor(w, 14);                                                                             // up, across, hovering at its height
                            w.Time = t1 + ZWorld.DragonShipCoolMap + 1;
                            int robot2 = w.DragonOrder(Dip(1, r));
                            Roll(2, s.Id, true);
                            int ship1 = w.DragonOrder(Level_(2, s, 28)); double t2 = w.Time; RunPass(w);
                            V(robot1 == 0 && robot2 == ZWorld.WhyMapCool && ship1 == 0, $"K3 {ZWorld.DragonShipCoolMap + 1} s after a pass on a robot: a robot {ZWorld.DragonWhyName[robot2]}, a flying shuttle {ZWorld.DragonWhyName[ship1]} (its gap {ZWorld.DragonShipCoolMap} s, robots {ZWorld.DragonCoolMap} s)");
                            w.Time = t2 + ZWorld.DragonShipCoolMap + 1; int robot3 = w.DragonOrder(Dip(3, r));
                            w.Time = t2 + ZWorld.DragonCoolMap + 1; int robot4 = w.DragonOrder(Dip(4, r));
                            if (w.DragonPass != null) RunPass(w);
                            V(robot3 == ZWorld.WhyMapCool && robot4 != ZWorld.WhyMapCool && robot4 != ZWorld.WhyOwnCool, $"K4 after a pass on the shuttle robots still wait {ZWorld.DragonCoolMap} s: at {ZWorld.DragonShipCoolMap + 1} s {ZWorld.DragonWhyName[robot3]}, at {ZWorld.DragonCoolMap + 1} s {ZWorld.DragonWhyName[robot4]}");
                            w.DragonProofForce = true;
                            int floor = ZWorld.DragonShipFloorHp(s);
                            s.SetHealth(s.MaxHealth * 3 / 10); int h30 = s.Health;
                            int low = w.DragonOrder(Level_(0, s, 28)); RunPass(w);
                            V(low == 0 && s.Health < h30 && s.Health >= floor && !s.Destroyed, $"K5 a flying shuttle at 30 %: {ZWorld.DragonWhyName[low]}, left at {100.0 * s.Health / s.MaxHealth:F1} %, never under {ZWorld.DragonShipFloorPerMille / 10.0} %");
                            s.SetHealth(floor); int atFloor = w.DragonOrder(Level_(1, s, 28));
                            V(atFloor == ZWorld.WhyTarget, $"K5 a flying shuttle at its floor is no target: {ZWorld.DragonWhyName[atFloor]}");
                            s.SetHealth(s.MaxHealth); int passes = 0, lowest = s.Health;
                            for (int k = 0; k < 14; k++) { if (w.DragonOrder(Level_(k % 3, s, 28)) == 0) { passes++; RunPass(w); } lowest = Math.Min(lowest, s.Health); }
                            V(!s.Destroyed && lowest >= floor && passes >= 6, $"K5 14 passes asked from full health, the chance and gaps set aside: {passes} flown, the shuttle never under {100.0 * lowest / s.MaxHealth:F1} % (floor {ZWorld.DragonShipFloorPerMille / 10.0} %), destroyed {s.Destroyed}");
                            w.DragonProofForce = false;
                        }
                    }
                    finally { ZWorld.ShuttlePlayableDeviation = false; }
                }
                // ---- M
                {
                    bool groundWas = ZWorld.GroundFireDeviation; ZWorld.GroundFireDeviation = true;
                    try
                    {
                        var w = Quiet(settings, json, 51); var (x, y) = Open(w);
                        var r = Robot(w, "grunt", x, y); var jeep = OzV11Kit.Place(w, "jeep", x - 8, y + 4, Team.Red); w.SendHold(Team.Red, jeep);
                        w.DragonProofForce = true;
                        w.DragonOrder(Dip(0, r)); RunPass(w);
                        bool over = w.GroundFires.Any(f => { long dx = f.x - jeep.CenterX, dy = f.y - jeep.CenterY; return dx * dx + dy * dy <= ZWorld.GroundFireRadiusPx * ZWorld.GroundFireRadiusPx; });
                        int lit = w.GroundFiresLit, patch0 = w.GroundFires.Count;
                        RunFor(w, ZWorld.GroundFireLife + 1);
                        double took = 1.0 - r.Health / (double)r.MaxHealth;
                        V(lit >= 1 && r.GroundFireTaken > 0 && w.GroundFires.Count == 0, $"M the ground under the flame: {lit} patches lit, {r.GroundFireTaken} HP taken from the grunt standing in it, out {ZWorld.GroundFireLife} s after the last bite");
                        V(over && jeep.GroundFireTaken == 0 && jeep.Health == jeep.MaxHealth, $"M a jeep in the same fire: {jeep.GroundFireTaken} HP (a patch under it: {over}, at {jeep.CenterX - x}, {jeep.CenterY - y} px from the robot)");
                        V(!r.Destroyed && took <= 0.76, $"M the flame and the ground together leave a healthy grunt alive: {took * 100:F1} % taken (76 at most)");
                        var hurt = Robot(w, "grunt", x, y + 40);
                        w.DragonOrder(Dip(0, hurt.Id, x / 8.0, -(y + 40) / 8.0)); RunPass(w);
                        hurt.SetHealth(10);                                                                     // the pass is over: only the burning ground can finish it now
                        bool burnt = false; int credit = -9;
                        RunUntilEvents(w, e => { if (e.type == ZEventType.Death && e.objId == hurt.Id) { burnt = e.flag; credit = e.otherId; } }, () => hurt.Destroyed, 4);
                        V(hurt.Destroyed && burnt && credit == -1, $"M a robot that dies on that ground: dead {hurt.Destroyed}, a fire death {burnt}, killer {credit} (no killer)");
                    }
                    finally { ZWorld.GroundFireDeviation = groundWas; }
                    {
                        var w = Quiet(settings, json, 53); var (x, y) = Open(w);
                        var el = Robot(w, ZElectro.Type, x + 40, y + 24); var t = Robot(w, "grunt", x, y);
                        w.DragonProofForce = true; int shots = 0; double flight = 0; bool allHit = true; int bolts0 = el.ElBolts;
                        w.DragonOrder(Dip(0, t));
                        RunUntilEvents(w, e => { if (e.type == ZEventType.DragonEvent && ZWorld.DragonKindOf(e) == ZDragonKind.Shot && e.objId == el.Id) { shots++; flight = e.value; allHit &= e.flag; } }, () => w.DragonPass == null, 12);
                        V(shots >= 1 && allHit && Math.Abs(flight - 0.35) < 0.001, $"M the Electrobot shoots back: {shots} bolt(s), lightning never misses, after his {flight:F2} s windup");
                        V(el.ElPhase == ElectroPhase.Charged && el.ElBolts == bolts0, $"M his real gun is not touched: {el.ElPhase}, {el.ElBolts - bolts0} real bolts");
                    }
                }
                // ---- L
                foreach (var lv in whole ? new[] { "L02", "L16", "L17" } : new string[0])
                {
                    string other; try { other = mapText(lv); } catch { other = null; }
                    if (other == null) { V(false, $"L {lv}: no map"); continue; }
                    ulong[] end = new ulong[2]; int refused = 0;
                    for (int round = 0; round < 2; round++)
                    {
                        ZWorld.DragonDeviation = round == 1;
                        var w = ZWorld.Create(settings, other, 3);
                        w.Bots.Add(new ZBot(w, Team.Blue, 102)); w.Bots.Add(new ZBot(w, Team.Red, 1));
                        ulong h = 3; for (int i = 0; i < 6000; i++) { w.Tick(Tick); w.Events.Clear(); h = w.RulesFingerprint(h); }
                        end[round] = h;
                        if (round == 1) { var r = w.Objects.FirstOrDefault(o => o.Kind == ZKind.Robot && !o.Destroyed); if (r != null && w.DragonOrder(Dip(0, r)) == ZWorld.WhyNoLava) refused++; }
                    }
                    V(end[0] == end[1] && refused == 1, $"L {lv}: 60 s of computers the same with the rule on and off, an order refused (no lava)");
                }
            }
            finally { ZWorld.DragonDeviation = ruleWas; ZWorld.ShuttlePlayableDeviation = false; }
            sb.AppendLine((pass == total ? "PASS " : "FAIL ") + pass + "/" + total);
            return sb.ToString();
        }

        static void RunUntilEvents(ZWorld w, Action<ZEvent> on, Func<bool> done, double seconds)
        {
            double end = w.Time + seconds;
            do
            {
                foreach (var e in w.Events) on(e);
                w.Events.Clear(); w.Tick(Tick);
            } while (w.Time < end && !done());
            foreach (var e in w.Events) on(e);
            w.Events.Clear();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("OpenZ/L07 Lava/Checks/Dragons/0 Rule count (no Play)")]
        static void Menu() => OzV11Kit.RunMenu("Level 7 dragons: the rule", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText));
#endif
    }
}
