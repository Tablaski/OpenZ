using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit tower_reach_fair_r6: the counted check of DV208's round 6 half, ONE distance both ways between a unit and a fort tower gun
    /// No Unity API (the kit's Mono harness
    /// runs it too). It reads only public members (RangeTo, WithinAttackRadius, CanAttackObject, TowerDrawnAt, the objects' fields), so the same
    /// check also runs on a tree with the 14 Nov rule alone (exe 14b) and gives the numbers before. Three parts:
    /// <para>1. Both ways, range only: level 7's blue fort (fort_front) and red fort (fort_back), turret spots (112,48) (south row) and (112,0)
    /// (north row; the west spots are their mirror), each tower gun type of the table, each unit type with a weapon, on the ground: every point
    /// of a grid round the gun, the unit's range at the gun (u.RangeTo) and the gun's at the unit (g.RangeTo), centre to centre as
    /// WithinAttackRadius tests them. Counted: 'unanswered' points (the unit reaches the gun, the gun does not reach the unit); straight south
    /// of the drawn tower in 1 px steps, how far the unit reaches the tower and the tower the unit; both ranges against their formulas at every
    /// point (attacker: flat + DV204 body + the 14 Nov pad, less the cut when its table range is shorter; tower: its flat range + the same pad).</para>
    /// <para>2. Scan: a red grunt that cannot move or shoot stands straight south of the drawn tower of each tower gun type (level 7 blue
    /// (112,48)), 4 px inside the tower's range from the drawn tower, i.e. outside the tower gun's old neighbour square (its range round its
    /// rule centre): the tower gun engages it within 2 s. And the answer (DV204's look square, ZWorld.TowerDrawnLookPad): an idle red light
    /// tank at the same point, shot at by the tower, answers it within 4 s (fires at it, or gets DV204's attack waypoint on it).</para>
    /// <para>
    /// terrain, no start units, the blue fort's crewed missile cannon on (112,48) (its own #980), its roof sniper as in the game, a blue dummy
    /// far away (no game over); a red medium tank, light tank or grunt squad (+ a laser squad, the one shorter gun DV204's cut touches, and a heavy
    /// tank, the same table range as the tower) from (768,448), PLAIN move to (736,248) under the drawn tower; DV204 on; seeds 1-3. Counted while
    /// the gun lives: ticks in which a red unit can fire at the gun (CanAttackObject + WithinAttackRadius, the line of sight too) and the gun
    /// cannot fire at it ('unanswered ticks'); when each side first has the other in reach; the first shot each way (time, distance to the drawn
    /// tower); shots each way; gun destroyed; red alive.</para>
    /// </summary>
    public static class OzV15TowerFairCount
    {
        public const int Runs = 3;
        const int StartX = 768, StartY = 448, GoalX = 736, GoalY = 248, Half = 330, South = 80;
        const double MaxSec = 45, Beat = 0.5, ScanSec = 2.0;
        public static readonly string[] TowerTypes = { "missile_cannon", "gun", "gatling", "howitzer" };
        static readonly (int lx, int ly, string row)[] Spots = { (112, 48, "south row"), (112, 0, "north row") };
        public static readonly (string unit, string name)[] TakeUnits = { ("mtank", "medium tank"), ("light", "light tank"), ("grunt", "grunt squad"), ("laser", "laser squad"), ("heavy", "heavy tank") };
        static readonly HashSet<string> OwnerThree = new HashSet<string> { "mtank", "light", "grunt" };

        static string NoUnits(string json)
        {
            var m = ZMapData.FromJson(json);
            m.objects.RemoveAll(o => { string c = o.kind.Split(':')[0]; return c == "robot" || c == "vehicle" || c == "cannon"; });
            return UnityEngine.JsonUtility.ToJson(m);
        }

        static double Dd(int ax, int ay, int bx, int by) => Math.Sqrt((double)(ax - bx) * (ax - bx) + (double)(ay - by) * (ay - by));
        static double Dd(ZObject a, int x, int y) => Dd(a.CenterX, a.CenterY, x, y);
        static long Sq(long dx, long dy) => dx * dx + dy * dy;
        static string Sec(double t) => t < 0 ? "never" : t.ToString("0.00");

        /// <summary>The unit's centre to (x, y), nothing else moved (part 1 never ticks its world).</summary>
        static void Put(ZObject u, int x, int y) { u.LocX = x - (u.WidthPix >> 1); u.LocY = y - (u.HeightPix >> 1); u.CenterX = x; u.CenterY = y; }

        /// <summary>The two ranges from their formulas for a unit u on the ground and fort tower gun g with drawn tower (dx, dy): DV162 (the
        /// tower above the unit), DV204 (the 24 px body for the attacker), DV208 (the pad: centre distance rounded up - distance to the drawn
        /// tower rounded down, not below 0) and round 6 (the same pad for the tower; the attacker's body cut when its table range is shorter).</summary>
        static void Expected(ZObject u, ZObject g, int dx, int dy, out int attacker, out int tower)
        {
            int ra = u.AttackRadius, rt = g.AttackRadius;
            int body = ZWorld.FortGunAnswerDeviation ? ZWorld.FortTowerBodyPx : 0;
            int back = ZWorld.HighGroundDeviation ? ZWorld.HighGroundRange(rt) : rt;
            int pad = 0, cut = 0;
            if (ZWorld.TowerDrawnReachDeviation)
            {
                long p = ZWorld.TowerSqrtUp(Sq(g.CenterX - u.CenterX, g.CenterY - u.CenterY)) - ZWorld.TowerSqrtDown(Sq(dx - u.CenterX, dy - u.CenterY));
                pad = p > 0 ? (int)p : 0;
                if (ra <= rt) cut = Math.Max(0, ra + body - back);
            }
            attacker = ra + body + pad - cut;
            tower = back + pad;
        }

        // ------------------------------------------------------------------ 1. both ways, range only
        internal sealed class Pair
        {
            public string fort, spot, tower, unit; public int ru, rt, unanswered, points, codeDiff, southUnit = -1, southTower = -1;
            public string firstDiff = "";
        }

        /// <summary>Part 1 for the given towers / units / spots (null = all), grid step `grid` px. The rule switches as the caller set them.</summary>
        internal static List<Pair> BothWays(string settings, Func<string, string> map, Func<string, bool> towerOk, Func<string, bool> unitOk, bool allForts, int grid, StringBuilder notes)
        {
            var res = new List<Pair>();
            var w = OzV11Kit.World(settings, NoUnits(map("L07")), 1);
            var types = w.S.Units.Where(kv => kv.Value.attack_radius > 0 && (unitOk == null || unitOk(kv.Key))).Select(kv => kv.Key)
                         .OrderBy(k => w.S.Units[k].attack_radius).ThenBy(k => k, StringComparer.Ordinal).ToList();
            var units = new List<ZObject>(); int park = 0;
            foreach (var ty in types)
            {
                ZObject u = null;
                try { u = OzV11Kit.Place(w, ty, 100 + park, 1400, Team.Red); } catch (Exception ex) { notes?.AppendLine($"(unit type {ty}: not made in a headless world ({ex.GetType().Name}), left out)"); }
                park += 48;
                if (u == null || u.AttackRadius <= 0) continue;
                units.Add(u);
            }
            var forts = w.Objects.Where(o => o.IsFort && (allForts || o.Owner == Team.Blue)).OrderBy(o => o.Owner == Team.Blue ? 0 : 1).ToList();
            foreach (var f in forts)
                foreach (var (lx, ly, row) in Spots)
                {
                    if (!allForts && ly != 48) continue;
                    foreach (var tt in TowerTypes)
                    {
                        if (towerOk != null && !towerOk(tt)) continue;
                        var gun = w.CreateObject("cannon", tt, f.LocX + lx, f.LocY + ly, f.Owner); gun.Ejectable = false;
                        if (!gun.TowerDrawnAt(out int tx, out int ty)) { notes?.AppendLine($"({f.Type} spot ({lx},{ly}) {tt}: no drawn tower, left out)"); continue; }
                        foreach (var u in units)
                        {
                            var p = new Pair { fort = $"{f.Type} {f.Owner}", spot = $"({lx},{ly}) {row}", tower = tt, unit = u.Type, ru = u.AttackRadius, rt = gun.AttackRadius };
                            int x0 = u.CenterX, y0 = u.CenterY;
                            for (int y = gun.CenterY - Half; y <= gun.CenterY + Half + South; y += grid)
                                for (int x = gun.CenterX - Half; x <= gun.CenterX + Half; x += grid)
                                {
                                    Put(u, x, y);
                                    double dc = Dd(u, gun.CenterX, gun.CenterY);
                                    int ra = u.RangeTo(gun), rg = gun.RangeTo(u);
                                    bool uIn = dc <= ra, gIn = dc <= rg;
                                    if (uIn || gIn) p.points++;
                                    if (uIn && !gIn) p.unanswered++;
                                    Expected(u, gun, tx, ty, out int ea, out int eg);
                                    if (ea != ra || eg != rg) { if (p.codeDiff == 0) p.firstDiff = $"at ({x},{y}): code {ra}/{rg}, formula {ea}/{eg}"; p.codeDiff++; }
                                }
                            for (int y = ty; y <= ty + 420; y++)                                     // straight south of the drawn tower, 1 px
                            {
                                Put(u, tx, y);
                                double dc = Dd(u, gun.CenterX, gun.CenterY);
                                if (dc <= u.RangeTo(gun)) p.southUnit = y - ty;
                                if (dc <= gun.RangeTo(u)) p.southTower = y - ty;
                            }
                            Put(u, x0, y0);
                            res.Add(p);
                        }
                    }
                }
            return res;
        }

        // ------------------------------------------------------------------ 2. the tower gun's scan
        internal static (bool engaged, double at, int dist, int fromRule) ScanOne(string settings, string l07, string towerType, int seed)
        {
            var w = OzV11Kit.World(settings, NoUnits(l07), seed);
            var fort = w.Objects.First(o => o.IsFort && o.Owner == Team.Blue);
            var gun = w.CreateObject("cannon", towerType, fort.LocX + 112, fort.LocY + 48, Team.Blue); gun.Ejectable = false;
            OzV11Kit.Dummy(w, 66 * 16, 80 * 16);
            gun.TowerDrawnAt(out int tx, out int ty);
            int reach = ZWorld.HighGroundDeviation ? ZWorld.HighGroundRange(gun.AttackRadius) : gun.AttackRadius;
            var g = w.CreateRobotGroup("grunt", tx - 8, ty + reach - 4 - 8, Team.Red, 1);
            g.MoveSpeed = 0; g.Damage = 0; g.MaxHealth = g.Health = 1000000000;
            int fromRule = (int)Math.Round(Dd(g, gun.CenterX, gun.CenterY));
            while (w.Time < ScanSec)
            {
                w.Tick(ZConst.TICK); w.Events.Clear();
                if (gun.AttackObject == g) return (true, w.Time, reach - 4, fromRule);
            }
            return (false, -1, reach - 4, fromRule);
        }

        /// <summary>Part 2, the answer: an idle red light tank 4 px inside the tower gun's range from the drawn tower, straight south. Shot at:
        /// the tower gun's first shot at it (s, -1 never); answered: the tank fires at the gun or has an attack waypoint on it (DV204), s.</summary>
        internal static (double shotAt, double answeredAt) AnswerOne(string settings, string l07, string towerType, int seed)
        {
            var w = OzV11Kit.World(settings, NoUnits(l07), seed);
            var fort = w.Objects.First(o => o.IsFort && o.Owner == Team.Blue);
            var gun = w.CreateObject("cannon", towerType, fort.LocX + 112, fort.LocY + 48, Team.Blue); gun.Ejectable = false;
            OzV11Kit.Dummy(w, 66 * 16, 80 * 16);
            gun.TowerDrawnAt(out int tx, out int ty);
            int reach = ZWorld.HighGroundDeviation ? ZWorld.HighGroundRange(gun.AttackRadius) : gun.AttackRadius;
            var u = OzV11Kit.Place(w, "light", tx, ty + reach - 4, Team.Red);
            double shot = -1;
            while (w.Time < 4.0 && OzV11Kit.Alive(u))
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events) if ((e.type == ZEventType.MissileFired || e.type == ZEventType.Hitscan) && e.objId == gun.Id && e.otherId == u.Id && shot < 0) shot = w.Time;
                w.Events.Clear();
                if (u.AttackObject == gun || u.Waypoints.Any(q => q.mode == WpMode.Attack && q.refId == gun.Id)) return (shot, w.Time);
            }
            return (shot, -1);
        }

        // ------------------------------------------------------------------ 3. the owner's round 5 take
        internal sealed class Take
        {
            public int sent, alive, redShots, gunShots, unansweredTicks; public bool gunDead;
            public double redFirst = -1, gunFirst = -1, gunDeadAt = -1, reachAt = -1, backAt = -1, end;
            public double redFirstDd = -1, gunFirstDd = -1;
            public string sig = "";
        }

        internal static Take RunTake(string settings, string l07, string unit, int seed, bool on, TextWriter trace = null)
        {
            ZWorld.TowerDrawnReachDeviation = on;
            var w = OzV11Kit.World(settings, NoUnits(l07), seed);
            var fort = w.Objects.First(o => o.IsFort && o.Owner == Team.Blue);
            var gun = w.CreateObject("cannon", "missile_cannon", fort.LocX + 112, fort.LocY + 48, Team.Blue); gun.Ejectable = false;   // as the fort's own #980
            OzV11Kit.Dummy(w, 66 * 16, 80 * 16);
            var u0 = OzV11Kit.Place(w, unit, StartX, StartY, Team.Red);
            var units = OzV11Kit.Squad(u0).ToList();
            OzV11Kit.PlayerMove(w, u0, GoalX, GoalY, false);
            var ids = new HashSet<int>(units.Select(x => x.Id));
            gun.TowerDrawnAt(out int tx, out int ty);
            var r = new Take { sent = units.Count };
            double next = 0;
            while (w.Time < MaxSec)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                {
                    if (e.type != ZEventType.MissileFired && e.type != ZEventType.Hitscan) continue;
                    if (e.objId == gun.Id && ids.Contains(e.otherId))
                    {
                        r.gunShots++;
                        if (r.gunFirst < 0) { r.gunFirst = w.Time; if (w.Get(e.otherId) is ZObject t) r.gunFirstDd = Dd(t, tx, ty); }
                    }
                    else if (ids.Contains(e.objId) && e.otherId == gun.Id)
                    {
                        r.redShots++;
                        if (r.redFirst < 0) { r.redFirst = w.Time; if (w.Get(e.objId) is ZObject s) r.redFirstDd = Dd(s, tx, ty); }
                    }
                }
                w.Events.Clear();
                if (!r.gunDead && !OzV11Kit.Alive(gun)) { r.gunDead = true; r.gunDeadAt = w.Time; }
                var live = units.Where(x => w.Objects.Contains(x) && OzV11Kit.Alive(x)).ToList();
                if (!r.gunDead)
                {
                    bool any = false;
                    foreach (var x in live)
                    {
                        bool hit = x.CanAttackObject(gun) && x.WithinAttackRadius(gun), back = gun.CanAttackObject(x) && gun.WithinAttackRadius(x);
                        if (hit && r.reachAt < 0) r.reachAt = w.Time;
                        if (back && r.backAt < 0) r.backAt = w.Time;
                        if (hit && !back) any = true;
                    }
                    if (any) r.unansweredTicks++;
                }
                if (trace != null && w.Time >= next)
                {
                    next += 0.5;
                    foreach (var u in live)
                        trace.WriteLine($"{w.Time,5:0.0} {u.Type}#{u.Id} ({u.CenterX},{u.CenterY}) to drawn tower {Dd(u, tx, ty),4:0} to rule centre {Dd(u, gun.CenterX, gun.CenterY),4:0} | its range at the gun {u.RangeTo(gun)} fires {(u.CanAttackObject(gun) && u.WithinAttackRadius(gun) ? 1 : 0)} | the gun's range at it {gun.RangeTo(u)} fires {(gun.CanAttackObject(u) && gun.WithinAttackRadius(u) ? 1 : 0)} | hp {u.Health} att {(u.AttackObject == null ? "-" : u.AttackObject.Type + "#" + u.AttackObject.Id)} | gun hp {gun.Health} on {(gun.AttackObject == null ? "-" : gun.AttackObject.Type + "#" + gun.AttackObject.Id)}");
                }
                if (live.Count == 0) break;
                if (r.gunDead && w.Time > r.gunDeadAt + 3) break;
            }
            r.end = w.Time;
            r.alive = units.Count(x => w.Objects.Contains(x) && OzV11Kit.Alive(x));
            r.sig = $"{r.alive}/{r.redShots}/{r.gunShots}/{r.unansweredTicks}/{r.redFirst:0.00}/{r.gunFirst:0.00}/{r.gunDeadAt:0.00}/{r.reachAt:0.00}/{r.backAt:0.00}/{r.end:0.00}/{w.Rng.Next(1 << 30)}";
            return r;
        }

        // ------------------------------------------------------------------ the whole check
        static List<(System.Reflection.FieldInfo f, bool v)> GameOn()
        {
            var saved = OzV15FortGunTargetCount.GameRules.Concat(new[] { "FortGunAnswerDeviation" }).Select(n => typeof(ZWorld).GetField(n))
                .Where(f => f != null && f.FieldType == typeof(bool)).Select(f => (f, (bool)f.GetValue(null))).ToList();
            foreach (var (f, _) in saved) f.SetValue(null, true);
            return saved;
        }

        /// <summary>Parts 1-3, the switches of a normal game (DV204 on), DV208 off (= before 14 Nov) and on (this tree's DV208). PASS / FAIL lines last.</summary>
        public static string Run(string settings, Func<string, string> map, int runs = Runs, TextWriter progress = null)
        {
            var sb = new StringBuilder(); var verdicts = new List<string>();
            bool had = ZWorld.TowerDrawnReachDeviation, calm = ZWorld.CalmStartDeviation;
            var saved = GameOn();
            try
            {
                ZWorld.CalmStartDeviation = false;
                // ---- 1
                var notes = new StringBuilder();
                ZWorld.TowerDrawnReachDeviation = false; var off = BothWays(settings, map, null, null, true, 4, null);
                ZWorld.TowerDrawnReachDeviation = true; var on = BothWays(settings, map, null, null, true, 4, notes);
                progress?.Write("1");
                sb.AppendLine("1. Both ways, range only (level 7; blue fort_front and red fort_back, spots (112,48) south row and (112,0) north row; every tower gun type x every unit type with a weapon, on the ground; a 4 px grid round the gun; DV204 on; DV208 off = before 14 Nov, on = this tree)");
                sb.Append(notes);
                sb.AppendLine("Level 7 blue fort_front, SE tower (112,48) (the clip), straight south of the drawn tower (px from the drawn tower; 'band' = how far the unit reaches past the tower's reach):");
                sb.AppendLine("| Tower gun (table) | Unit (table) | DV208 off: unit / tower / band | DV208 on: unit / tower / band | Unanswered points DV208 on (4 px grid, all 4 spots of both forts) | Code = formula (points that differ) |");
                sb.AppendLine("|---|---|---|---|---|---|");
                foreach (var tt in TowerTypes)
                    foreach (var p in on.Where(q => q.tower == tt && q.fort.StartsWith("fort_front") && q.spot.StartsWith("(112,48)")))
                    {
                        var o = off.First(q => q.tower == tt && q.unit == p.unit && q.fort == p.fort && q.spot == p.spot);
                        var all = on.Where(q => q.tower == tt && q.unit == p.unit).ToList();
                        string Band(Pair q) => $"{q.southUnit} / {q.southTower} / {(q.southUnit > q.southTower ? "+" + (q.southUnit - q.southTower) : "0")}";
                        sb.AppendLine($"| {tt} ({p.rt}) | {p.unit} ({p.ru}) | {Band(o)} | {Band(p)} | {all.Sum(q => q.unanswered)} | {all.Sum(q => q.codeDiff)}{(all.Any(q => q.codeDiff > 0) ? " first " + all.First(q => q.codeDiff > 0).firstDiff : "")} |");
                    }
                var shorter = on.Where(q => q.ru < q.rt).ToList();
                int shorterBad = shorter.Count(q => q.unanswered > 0), diff = on.Sum(q => q.codeDiff), offDiff = 0;
                foreach (var q in off) offDiff += q.codeDiff;
                var mine = on.Where(q => OwnerThree.Contains(q.unit) && q.tower == "missile_cannon" && q.fort.StartsWith("fort_front") && q.spot.StartsWith("(112,48)")).ToList();
                var longerEq = on.Where(q => q.ru >= q.rt && q.unanswered > 0).Select(q => $"{q.unit} ({q.ru}) at {q.tower} ({q.rt})").Distinct().ToList();
                sb.AppendLine();
                sb.AppendLine($"Pairs with the unit's table range shorter than the tower gun's: {shorter.Count} (x spots); with unanswered points: {shorterBad}. Units with an equal or longer table range that still outreach a tower (DV204's body, not this rule): {(longerEq.Count == 0 ? "none" : string.Join(", ", longerEq))}.");
                verdicts.Add(OzV11Kit.Verdict(shorter.Count > 0 && shorterBad == 0, $"DV208 on: no unit whose table range is shorter than the tower gun's reaches it where the tower cannot reach the unit back (unanswered points in {shorterBad} of {shorter.Count} unit / tower / spot sets)"));
                verdicts.Add(OzV11Kit.Verdict(diff == 0 && offDiff == 0, $"both ranges = their formulas at every grid point (the attacker's: the 14 Nov rule, less DV204's body where it would outreach a longer-ranged tower; the tower's: its range + the SAME pad): {diff} points differ with DV208 on, {offDiff} off"));
                verdicts.Add(OzV11Kit.Verdict(mine.Count == 3 && mine.All(q => q.southTower > q.southUnit), $"level 7 SE missile tower, straight south: the tower reaches farther than the medium tank, light tank and grunts ({string.Join(", ", mine.Select(q => $"{q.unit} {q.southUnit} px, tower {q.southTower} px"))})"));
                // ---- 2
                string l07 = map("L07");
                sb.AppendLine();
                sb.AppendLine($"2. Scan: a red grunt that cannot move or shoot, straight south of the drawn tower (level 7 blue (112,48)), 4 px inside the tower gun's range from the drawn tower; engaged within {ScanSec:0.0} s?");
                sb.AppendLine("| Tower gun | Distance to the drawn tower / to the rule centre (px) | Grunt engaged: DV208 off | DV208 on | Idle light tank there: shot at / answers, DV208 off | DV208 on |");
                sb.AppendLine("|---|---|---|---|---|---|");
                int scanOk = 0, answerOk = 0;
                foreach (var tt in TowerTypes)
                {
                    ZWorld.TowerDrawnReachDeviation = false; var a = ScanOne(settings, l07, tt, 1); var aa = AnswerOne(settings, l07, tt, 1);
                    ZWorld.TowerDrawnReachDeviation = true; var b = ScanOne(settings, l07, tt, 1); var ba = AnswerOne(settings, l07, tt, 1);
                    if (b.engaged) scanOk++;
                    if (ba.shotAt >= 0 && ba.answeredAt >= 0) answerOk++;
                    string Ans((double shotAt, double answeredAt) x) => $"{(x.shotAt < 0 ? "not shot at" : Sec(x.shotAt))} / {(x.answeredAt < 0 ? "no answer" : Sec(x.answeredAt))}";
                    sb.AppendLine($"| {tt} | {b.dist} / {b.fromRule} | {(a.engaged ? "engaged " + Sec(a.at) + " s" : "not engaged")} | {(b.engaged ? "engaged " + Sec(b.at) + " s" : "not engaged")} | {Ans(aa)} | {Ans(ba)} |");
                    progress?.Write(".");
                }
                verdicts.Add(OzV11Kit.Verdict(scanOk == TowerTypes.Length, $"DV208 on: every tower gun type engages a unit inside its range from the drawn tower, outside its old square round the rule centre ({scanOk} of {TowerTypes.Length})"));
                verdicts.Add(OzV11Kit.Verdict(answerOk == TowerTypes.Length, $"DV208 on: an idle light tank shot at by each tower gun type from 4 px inside its range from the drawn tower answers it (fires at it or DV204's attack waypoint) ({answerOk} of {TowerTypes.Length})"));
                // ---- 3
                sb.AppendLine();
                sb.AppendLine($"3. The owner's round 5 take (level 7, blue fort, crewed missile cannon on (112,48) as #980, roof sniper as in the game, blue dummy far away; red unit from ({StartX},{StartY}), PLAIN move to ({GoalX},{GoalY}); DV204 on; {runs} runs, seeds 1-{runs}; times in s, distances in px to the drawn tower)");
                sb.AppendLine("| Unit | DV208 | Unanswered ticks | Unit has the gun in reach / gun has the unit | First shot: unit at the gun (distance) | First shot: gun at the unit (distance) | Shots unit / gun | Gun destroyed | Red alive / sent |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
                int ownerRuns = 0, ownerAnswered = 0, ownerFired = 0, ownerFirst = 0, twice = 0, pairs = 0, shorterRuns = 0, shorterBadRuns = 0, gunFirstRuns = 0;
                foreach (var (unit, name) in TakeUnits)
                    foreach (bool rule in new[] { false, true })
                    {
                        var list = new List<Take>();
                        for (int s = 1; s <= runs; s++) { list.Add(RunTake(settings, l07, unit, s, rule)); progress?.Write("."); }
                        var again = RunTake(settings, l07, unit, 1, rule); pairs++;
                        if (again.sig == list[0].sig) twice++; else sb.AppendLine($"(differs twice: {name} DV208 {(rule ? "on" : "off")}: {list[0].sig} / {again.sig})");
                        string C(Func<Take, string> c) => string.Join(" / ", list.Select(c));
                        sb.AppendLine($"| {name} | {(rule ? "on" : "off")} | {C(x => x.unansweredTicks.ToString())} | {C(x => Sec(x.reachAt) + " - " + Sec(x.backAt))} | {C(x => x.redFirst < 0 ? "never" : $"{Sec(x.redFirst)} ({x.redFirstDd:0})")} | {C(x => x.gunFirst < 0 ? "never" : $"{Sec(x.gunFirst)} ({x.gunFirstDd:0})")} | {C(x => x.redShots + "/" + x.gunShots)} | {C(x => x.gunDead ? "yes " + Sec(x.gunDeadAt) : "no")} | {C(x => $"{x.alive}/{x.sent}")} |");
                        if (!rule) continue;
                        bool shorterUnit = unit != "heavy";
                        foreach (var x in list)
                        {
                            if (shorterUnit) { shorterRuns++; if (x.unansweredTicks > 0) shorterBadRuns++; }
                            if (!OwnerThree.Contains(unit)) continue;
                            ownerRuns++;
                            if (x.gunShots > 0) ownerFired++;
                            if (x.reachAt >= 0 && x.backAt >= 0 && x.backAt <= x.reachAt) ownerAnswered++;
                            if (x.gunFirst >= 0 && (x.redFirst < 0 || x.gunFirst <= x.redFirst + Beat)) ownerFirst++;
                            if (x.gunFirst >= 0 && (x.redFirst < 0 || x.gunFirst <= x.redFirst)) gunFirstRuns++;
                        }
                    }
                sb.AppendLine();
                sb.AppendLine($"DV208 on, medium tank / light tank / grunts: the tower fired first in {gunFirstRuns} of {ownerRuns} runs, within one {Beat:0.0} s target beat of the unit's first shot in {ownerFirst} of {ownerRuns}.");
                verdicts.Add(OzV11Kit.Verdict(shorterRuns > 0 && shorterBadRuns == 0, $"DV208 on: the medium tank, light tank, grunts and lasers (table range shorter than the tower's) never stand where they can fire at the tower and it cannot fire back ({shorterBadRuns} of {shorterRuns} runs with an unanswered tick)"));
                verdicts.Add(OzV11Kit.Verdict(ownerRuns > 0 && ownerAnswered == ownerRuns, $"DV208 on: the tower has the medium tank / light tank / grunts in reach no later than they have it ({ownerAnswered} of {ownerRuns} runs)"));
                verdicts.Add(OzV11Kit.Verdict(ownerRuns > 0 && ownerFired == ownerRuns && ownerFirst == ownerRuns, $"DV208 on: the tower fires at them in every run, its first shot no later than one {Beat:0.0} s target beat after theirs ({ownerFired} and {ownerFirst} of {ownerRuns} runs)"));
                verdicts.Add(OzV11Kit.Verdict(twice == pairs, $"the same run twice gives the same result ({twice} of {pairs} unit / rule pairs)"));
            }
            finally
            {
                foreach (var (f, v) in saved) f.SetValue(null, v);
                ZWorld.CalmStartDeviation = calm; ZWorld.TowerDrawnReachDeviation = had;
            }
            sb.AppendLine();
            foreach (var v in verdicts) sb.AppendLine(v);
            return sb.ToString();
        }

        /// <summary>Kit harness: one take, one seed, DV208 on / off, every half second each red unit.</summary>
        public static int Trace(string settings, Func<string, string> map, string unit, int seed, bool on, TextWriter o)
        {
            bool had = ZWorld.TowerDrawnReachDeviation, calm = ZWorld.CalmStartDeviation;
            var saved = GameOn();
            try
            {
                ZWorld.CalmStartDeviation = false;
                o.WriteLine($"{unit} plain move from ({StartX},{StartY}) to ({GoalX},{GoalY}), DV208 {(on ? "on" : "off")}, seed {seed}");
                var r = RunTake(settings, map("L07"), unit, seed, on, o);
                o.WriteLine($"END {r.end:0.0} s: unanswered ticks {r.unansweredTicks}; in reach: unit {Sec(r.reachAt)}, gun {Sec(r.backAt)}; first shot unit {Sec(r.redFirst)} ({r.redFirstDd:0} px from the drawn tower), gun {Sec(r.gunFirst)} ({r.gunFirstDd:0} px); shots unit {r.redShots}, gun {r.gunShots}; gun destroyed {r.gunDead} {Sec(r.gunDeadAt)}; alive {r.alive}/{r.sent}");
            }
            finally
            {
                foreach (var (f, v) in saved) f.SetValue(null, v);
                ZWorld.CalmStartDeviation = calm; ZWorld.TowerDrawnReachDeviation = had;
            }
            return 0;
        }

        /// <summary>
        /// level 7's SE missile tower.</summary>
        public static string Quick(string settings, Func<string, string> map)
        {
            bool had = ZWorld.TowerDrawnReachDeviation, calm = ZWorld.CalmStartDeviation;
            var saved = GameOn();
            var sb = new StringBuilder(); bool ok;
            try
            {
                ZWorld.CalmStartDeviation = false;
                var t = RunTake(settings, map("L07"), "mtank", 1, true);
                ZWorld.TowerDrawnReachDeviation = true;
                var p = BothWays(settings, map, x => x == "missile_cannon", x => x == "mtank", false, 4, null).First();
                sb.Append($"level 7 SE missile tower, medium tank, both ways: straight south the tank reaches {p.southUnit} px from the drawn tower, the tower {p.southTower} px; unanswered points {p.unanswered}, code = formula ({p.codeDiff} differ); the take: unanswered ticks {t.unansweredTicks}, the tower's first shot {Sec(t.gunFirst)} s, the tank's {Sec(t.redFirst)} s, tower shots {t.gunShots}; ");
                ok = p.southTower > p.southUnit && p.unanswered == 0 && p.codeDiff == 0 && t.unansweredTicks == 0 && t.gunShots > 0;
            }
            finally
            {
                foreach (var (f, v) in saved) f.SetValue(null, v);
                ZWorld.CalmStartDeviation = calm; ZWorld.TowerDrawnReachDeviation = had;
            }
            sb.AppendLine(); sb.AppendLine(OzV11Kit.Verdict(ok, "DV208 round 6: one distance both ways; the level 7 missile tower outreaches the medium tank and fires back at it"));
            return sb.ToString();
        }
    }
}
