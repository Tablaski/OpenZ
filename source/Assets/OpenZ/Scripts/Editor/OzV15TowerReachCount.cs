using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit tower_reach_dv208: the counted check of DV208 "the reach to a fort tower gun is measured to where its tower is drawn"
    /// (Sim/ZObject.TowerDrawnReach.cs), no Unity API (the kit's Mono harness runs it too). Three parts:
    /// <para>1. Every fort of levels 2, 7, 16, 17 and 18, its four turret spots (a gun put on each free one): the rules' drawn tower point
    /// against the view's pad (View/TowerMounts.PadLocal and PadForSpot, the fort model as OpenZSetup places it: the centre of the fort's
    /// rectangle, fort_front turned 180 deg, fort_back 0), within 1 px.</para>
    /// <para>
    /// or guns; the blue fort gets one crewed missile cannon on its tower spot (112, 48) (as its own #980) and its roof defender as in the game;
    /// a red medium tank, light tank, heavy tank or grunt squad starts at (768, 448) and gets a PLAIN move to (736, 248), right under the drawn
    /// tower. The game's rule set with DV204 on (P today); DV208 off = P before this kit, on = this kit. Counted per run: when the drawn tower's
    /// body (24 px, DV204) is first in a red unit's reach as the player sees it, when the rules say so (WithinAttackRadius), the first shot at the
    /// tower gun, the shots at the roof sniper before it, the tower gun destroyed, red alive / sent.</para>
    /// <para>3. (the harness: DV204's whole check OzV15FortGunTargetCount.Run once with DV208 off and once on.)</para>
    /// </summary>
    public static class OzV15TowerReachCount
    {
        public const int Runs = 3;
        const int StartX = 768, StartY = 448, GoalX = 736, GoalY = 248;
        const double MaxSec = 45, Beat = 0.5, FireTime = 1.5;
        public static readonly string[] Levels = { "L02", "L07", "L16", "L17", "L18" };

        // ------------------------------------------------------------------ 1. geometry
        /// <summary>View/TowerMounts.PadLocal (fort-local metres, x and z), copied: the check has no Unity API.</summary>
        static readonly double[,] PadLocal = { { -6.985, -6.563 }, { 6.987, -6.564 }, { -6.984, 5.330 }, { 6.985, 5.314 } };
        static readonly (int lx, int ly)[] Spots = { (16, 0), (112, 0), (16, 48), (112, 48) };

        /// <summary>The view's pad of spot (lx, ly) of fort f, px: OpenZSetup's fort placement (centre of the rectangle, fort_front yaw 180,
        /// fort_back 0) + TowerMounts.PadForSpot (the pad furthest along the spot's diagonal) + PadWorld, metres x 8, world z = -px y.</summary>
        internal static (double x, double y) ViewPad(ZObject f, int lx, int ly)
        {
            double cx = f.LocX + f.WidthPix / 2.0, cy = f.LocY + f.HeightPix / 2.0;
            double yaw = f.Type == "fort_front" ? Math.PI : 0, c = Math.Cos(yaw), s = Math.Sin(yaw);
            double sx = lx >= 64 ? 1 : -1, sz = ly < 24 ? 1 : -1, best = double.NegativeInfinity, bx = 0, bz = 0;
            for (int i = 0; i < 4; i++)
            {
                double x = PadLocal[i, 0], z = PadLocal[i, 1];
                double wx = x * c + z * s, wz = -x * s + z * c;                          // Unity yaw about +y
                double score = wx * sx + wz * sz;
                if (score > best) { best = score; bx = wx; bz = wz; }
            }
            return (cx + bx * 8, cy - bz * 8);
        }

        public static string Geometry(string settings, Func<string, string> map, out int spots, out int bad, out double worst)
        {
            var sb = new StringBuilder(); spots = 0; bad = 0; worst = 0;
            sb.AppendLine("| Level | Fort | Spot | Gun's rule centre (px) | Drawn tower, rules DV208 (px) | View pad (px) | Error (px) | Drawn tower from the rule centre (m) |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var lv in Levels)
            {
                string json; try { json = map(lv); } catch (Exception ex) { sb.AppendLine($"| {lv} | map not read: {ex.Message} |"); bad++; continue; }
                var w = OzV11Kit.World(settings, json, 1);
                foreach (var f in w.Objects.Where(o => o.IsFort).ToList())
                    foreach (var (lx, ly) in Spots)
                    {
                        var gun = w.Objects.FirstOrDefault(o => o.Kind == ZKind.Cannon && o.LocX == f.LocX + lx && o.LocY == f.LocY + ly);
                        if (gun == null) { gun = w.CreateObject("cannon", "gun", f.LocX + lx, f.LocY + ly, f.Owner == Team.Neutral ? Team.Blue : f.Owner); gun.Ejectable = false; }
                        bool has = gun.TowerDrawnAt(out int x, out int y);
                        var (vx, vy) = ViewPad(f, lx, ly);
                        double e = Math.Sqrt((x - vx) * (x - vx) + (y - vy) * (y - vy)), m = Math.Sqrt((double)(x - gun.CenterX) * (x - gun.CenterX) + (double)(y - gun.CenterY) * (y - gun.CenterY)) / 8;
                        spots++; if (!has || e > 1.0) bad++; worst = Math.Max(worst, e);
                        sb.AppendLine($"| {lv} | {f.Type} {f.Owner} ({f.LocX >> 4},{f.LocY >> 4}) | ({lx},{ly}) {gun.Type}{(gun.IsTowerGun ? "" : " NOT a tower gun")} | ({gun.CenterX},{gun.CenterY}) | {(has ? $"({x},{y})" : "none")} | ({vx:0.0},{vy:0.0}) | {e:0.00} | {m:0.0} |");
                    }
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ 2. the 13 Nov tower test
        internal sealed class One
        {
            public int sent, alive, shotsGun, defBefore, defSeen, otherBefore, gunShots; public bool gunDead;
            public double seenAt = -1, rulesAt = -1, firstAt = -1, gunFirst = -1, gunDeadAt = -1, end; public bool fair;
            public string sig = "";
        }

        static bool Shot(ZEvent e) => e.type == ZEventType.MissileFired || e.type == ZEventType.Hitscan;

        static string NoUnits(string l07)
        {
            var m = ZMapData.FromJson(l07);
            m.objects.RemoveAll(o => { string c = o.kind.Split(':')[0]; return c == "robot" || c == "vehicle" || c == "cannon"; });
            return UnityEngine.JsonUtility.ToJson(m);
        }

        public static readonly (string unit, string name)[] Units = { ("mtank", "medium tank"), ("light", "light tank"), ("heavy", "heavy tank"), ("grunt", "grunt squad") };

        internal static One RunOne(string settings, string l07, string unit, int seed, bool on, TextWriter trace = null)
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
            gun.TowerDrawnAt(out int tx, out int ty);                                     // the drawn tower (also with the rule off: what the player sees)
            var r = new One { sent = units.Count };
            double next = 0;
            while (w.Time < MaxSec)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                {
                    if (!Shot(e)) continue;
                    if (e.objId == gun.Id && ids.Contains(e.otherId)) { r.gunShots++; if (r.gunFirst < 0) r.gunFirst = w.Time; }
                    if (!ids.Contains(e.objId)) continue;
                    if (e.otherId == gun.Id) { r.shotsGun++; if (r.firstAt < 0) r.firstAt = w.Time; continue; }
                    if (r.firstAt >= 0) continue;
                    bool def = w.Get(e.otherId) is ZObject t && t.IsFortDefender;
                    if (def) r.defBefore++; else r.otherBefore++;
                    if (def && r.seenAt >= 0 && w.Time > r.seenAt + Beat) r.defSeen++;   // the roof sniper while the drawn tower was already in reach
                }
                w.Events.Clear();
                if (!r.gunDead && !OzV11Kit.Alive(gun)) { r.gunDead = true; r.gunDeadAt = w.Time; }
                var live = units.Where(x => w.Objects.Contains(x) && OzV11Kit.Alive(x)).ToList();
                if (!r.gunDead)
                {
                    if (r.seenAt < 0 && live.Any(x => x.CanAttackObject(gun) && Seen(x, tx, ty, gun))) r.seenAt = w.Time;
                    if (r.rulesAt < 0 && live.Any(x => x.CanAttackObject(gun) && x.WithinAttackRadius(gun))) r.rulesAt = w.Time;
                }
                if (r.seenAt >= 0 && !r.fair && w.Time >= r.seenAt + FireTime && w.Time < r.seenAt + FireTime + ZConst.TICK * 1.5)
                    r.fair = r.shotsGun > 0 || (!r.gunDead && live.Count > 0);           // it lived FireTime with the drawn tower in reach
                if (trace != null && w.Time >= next)
                {
                    next += 0.5;
                    foreach (var u in live)
                        trace.WriteLine($"{w.Time,5:0.0} {u.Type}#{u.Id} ({u.CenterX},{u.CenterY}) to rule centre {OzV11Kit.Dist(u, gun),4:0} to drawn tower {Math.Sqrt((double)(u.CenterX - tx) * (u.CenterX - tx) + (double)(u.CenterY - ty) * (u.CenterY - ty)),4:0} range {u.RangeTo(gun)} seen {(Seen(u, tx, ty, gun) ? 1 : 0)} rules {(u.WithinAttackRadius(gun) ? 1 : 0)} hp {u.Health} att {(u.AttackObject == null ? "-" : u.AttackObject.Type + "#" + u.AttackObject.Id)} | gun hp {gun.Health} on {(gun.AttackObject == null ? "-" : gun.AttackObject.Type)}");
                }
                if (live.Count == 0) break;
                if (r.gunDead && w.Time > r.gunDeadAt + 3) break;
            }
            r.end = w.Time;
            r.alive = units.Count(x => w.Objects.Contains(x) && OzV11Kit.Alive(x));
            r.sig = $"{r.alive}/{r.shotsGun}/{r.defBefore}/{r.defSeen}/{r.otherBefore}/{r.gunShots}/{r.firstAt:0.00}/{r.gunDeadAt:0.00}/{r.seenAt:0.00}/{r.rulesAt:0.00}/{r.end:0.00}/{w.Rng.Next(1 << 30)}";
            return r;
        }

        /// <summary>The drawn tower's body (DV204's 24 px) inside the unit's plain range, measured from the unit to the drawn tower: in reach
        /// as the player sees it (the same test with the rule on or off).</summary>
        static bool Seen(ZObject u, int tx, int ty, ZObject gun = null)
        {
            long dx = tx - u.CenterX, dy = ty - u.CenterY; long r = u.AttackRadius + ZWorld.FortTowerBodyPx;
            if (gun != null && ZWorld.TowerDrawnReachDeviation && u.AttackRadius <= gun.AttackRadius)
            {
                long back = ZWorld.HighGroundDeviation ? ZWorld.HighGroundRange(gun.AttackRadius) : gun.AttackRadius;
                if (r > back) r = back;
            }
            return u.AttackRadius > 0 && dx * dx + dy * dy <= r * r;
        }

        static string Sec(double t) => t < 0 ? "never" : t.ToString("0.0");

        static List<(System.Reflection.FieldInfo f, bool v)> GameOn()
        {
            var saved = OzV15FortGunTargetCount.GameRules.Concat(new[] { "FortGunAnswerDeviation" }).Select(n => typeof(ZWorld).GetField(n))
                .Where(f => f != null && f.FieldType == typeof(bool)).Select(f => (f, (bool)f.GetValue(null))).ToList();
            foreach (var (f, _) in saved) f.SetValue(null, true);
            return saved;
        }

        /// <summary>The whole check: part 1 and part 2 (every unit 'runs' times, seeds 1..runs, DV208 off and on). PASS / FAIL lines last.</summary>
        public static string Run(string settings, Func<string, string> map, int runs = Runs, TextWriter progress = null)
        {
            var sb = new StringBuilder(); var verdicts = new List<string>();
            bool had = ZWorld.TowerDrawnReachDeviation, calm = ZWorld.CalmStartDeviation;
            var saved = GameOn();
            try
            {
                ZWorld.CalmStartDeviation = false;
                sb.AppendLine("1. Every fort tower spot of levels 2, 7, 16, 17, 18: the rules' drawn tower point (DV208) against the view's pad (TowerMounts.PadLocal / PadForSpot, the fort model as placed by OpenZSetup)");
                sb.Append(Geometry(settings, map, out int spots, out int bad, out double worst));
                verdicts.Add(OzV11Kit.Verdict(spots == 40 && bad == 0, $"every fort tower spot of the five levels: the rules' drawn tower within 1 px of the view's pad ({spots - bad} of {spots}, worst {worst:0.00} px)"));

                string l07 = map("L07");
                sb.AppendLine();
                sb.AppendLine($"2. The 13 Nov tower test (level 7, blue fort, crewed missile cannon on its tower spot (112, 48), roof sniper as in the game; red unit from ({StartX}, {StartY}), plain move to ({GoalX}, {GoalY}) under the drawn tower; DV204 on; {runs} runs (seeds 1-{runs}); off = P, on = DV208; times in s; 'seen' = the drawn tower's body in the unit's range)");
                sb.AppendLine("| Unit | DV208 | Drawn tower in reach (seen) | Rules: in reach | First shot at the tower gun | Shots at the roof sniper before it (of them with the tower seen in reach) | Shots at the tower gun | Tower gun's shots at red | Tower gun destroyed | Red alive / sent |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
                int onFair = 0, onFairShot = 0, onDefSeen = 0, onLate = 0, onShotRuns = 0, offShotRuns = 0, onKills = 0, offKills = 0, twice = 0, pairs = 0, tankRuns = 0, tankReach = 0, offTankReach = 0;
                var res = new Dictionary<(string, bool), List<One>>();
                foreach (var (unit, name) in Units)
                    foreach (bool on in new[] { false, true })
                    {
                        var list = new List<One>();
                        for (int s = 1; s <= runs; s++) { list.Add(RunOne(settings, l07, unit, s, on)); progress?.Write("."); }
                        res[(unit, on)] = list;
                        var again = RunOne(settings, l07, unit, 1, on); pairs++;
                        if (again.sig == list[0].sig) twice++; else sb.AppendLine($"(differs twice: {name} DV208 {(on ? "on" : "off")}: {list[0].sig} / {again.sig})");
                        string Cell(Func<One, string> c) => string.Join(" / ", list.Select(c));
                        sb.AppendLine($"| {name} | {(on ? "on" : "off (P)")} | {Cell(x => Sec(x.seenAt))} | {Cell(x => Sec(x.rulesAt))} | {Cell(x => Sec(x.firstAt))} | {Cell(x => $"{x.defBefore} ({x.defSeen})")} | {Cell(x => x.shotsGun.ToString())} | {Cell(x => x.gunShots.ToString())} | {Cell(x => x.gunDead ? "yes " + Sec(x.gunDeadAt) : "no")} | {Cell(x => $"{x.alive}/{x.sent}")} |");
                        foreach (var x in list)
                        {
                            if (on) { if (x.fair) { onFair++; if (x.shotsGun > 0) onFairShot++; } onDefSeen += x.defSeen > 0 ? 1 : 0; if (x.seenAt >= 0 && (x.rulesAt < 0 || x.rulesAt > x.seenAt + ZConst.TICK * 1.5)) onLate++; if (x.shotsGun > 0) onShotRuns++; if (x.gunDead) onKills++; }
                            else { if (x.shotsGun > 0) offShotRuns++; if (x.gunDead) offKills++; }
                            if (unit == "mtank" || unit == "light") { if (on) { tankRuns++; if (x.rulesAt >= 0) tankReach++; } else if (x.rulesAt >= 0) offTankReach++; }
                        }
                    }
                int total = Units.Length * runs;
                sb.AppendLine();
                sb.AppendLine($"DV208 on: {onShotRuns} of {total} runs fired at the tower gun, {onKills} destroyed it; off (P): {offShotRuns} of {total} fired, {offKills} destroyed it. Medium / light tank with the tower in the rules' reach: on {tankReach} of {tankRuns}, off (P) {offTankReach} of {tankRuns}.");
                verdicts.Add(OzV11Kit.Verdict(onLate == 0, $"DV208 on: the rules have the tower in reach no later than the player sees it in reach (one tick) in every run ({total - onLate} of {total})"));
                verdicts.Add(OzV11Kit.Verdict(onFair > 0 && onFairShot == onFair, $"DV208 on: every run in which a red unit has the drawn tower in reach (and lives {FireTime:0.0} s) fires at the tower gun ({onFairShot} of {onFair})"));
                verdicts.Add(OzV11Kit.Verdict(onDefSeen == 0, $"DV208 on: no shot at the roof sniper while the drawn tower is in reach (after one {Beat:0.0} s target beat) before the first shot at the tower ({onDefSeen} runs with one)"));
                verdicts.Add(OzV11Kit.Verdict(tankRuns > 0 && tankReach == tankRuns, $"DV208 on: the medium and the light tank get the tower in reach in every run ({tankReach} of {tankRuns}; P {offTankReach})"));
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

        /// <summary>Kit harness: one unit, one seed, rule on / off, every half second each red unit.</summary>
        public static int Trace(string settings, Func<string, string> map, string unit, int seed, bool on, TextWriter o)
        {
            bool had = ZWorld.TowerDrawnReachDeviation, calm = ZWorld.CalmStartDeviation;
            var saved = GameOn();
            try
            {
                ZWorld.CalmStartDeviation = false;
                o.WriteLine($"{unit} plain move DV208 {(on ? "on" : "off")} seed {seed}");
                var r = RunOne(settings, map("L07"), unit, seed, on, o);
                o.WriteLine($"END {r.end:0.0} s: seen {Sec(r.seenAt)}, rules {Sec(r.rulesAt)}, first shot at the gun {Sec(r.firstAt)}, sniper before {r.defBefore} ({r.defSeen} seen), shots at gun {r.shotsGun}, gun shots {r.gunShots}, gun destroyed {r.gunDead} {Sec(r.gunDeadAt)}, alive {r.alive}/{r.sent}");
            }
            finally
            {
                foreach (var (f, v) in saved) f.SetValue(null, v);
                ZWorld.CalmStartDeviation = calm; ZWorld.TowerDrawnReachDeviation = had;
            }
            return 0;
        }

        /// <summary>The parity row's probe: the medium tank of the tower test, one run, DV208 off and on, + the geometry of level 7.</summary>
        public static string Quick(string settings, Func<string, string> map)
        {
            bool had = ZWorld.TowerDrawnReachDeviation, calm = ZWorld.CalmStartDeviation;
            var saved = GameOn();
            var sb = new StringBuilder(); bool ok;
            try
            {
                ZWorld.CalmStartDeviation = false;
                var a = RunOne(settings, map("L07"), "mtank", 1, false); var b = RunOne(settings, map("L07"), "mtank", 1, true);
                var w = OzV11Kit.World(settings, map("L07"), 1);
                var f = w.Objects.First(o => o.IsFort && o.Owner == Team.Blue);
                var g = w.Objects.FirstOrDefault(o => o.Kind == ZKind.Cannon && o.LocX == f.LocX + 112 && o.LocY == f.LocY + 48) ?? w.CreateObject("cannon", "missile_cannon", f.LocX + 112, f.LocY + 48, Team.Blue);
                g.Ejectable = false;
                bool has = g.TowerDrawnAt(out int x, out int y); var (vx, vy) = ViewPad(f, 112, 48);
                double e = Math.Sqrt((x - vx) * (x - vx) + (y - vy) * (y - vy));
                sb.Append($"level 7 blue tower (112,48): rule centre ({g.CenterX},{g.CenterY}), drawn ({x},{y}), view pad ({vx:0.0},{vy:0.0}); medium tank under the drawn tower: tower in the rules' reach {Sec(a.rulesAt)} -> {Sec(b.rulesAt)} s (seen {Sec(b.seenAt)} s), first shot at it {Sec(a.firstAt)} -> {Sec(b.firstAt)} s, shots at the roof sniper first {a.defBefore} -> {b.defBefore}; ");
                ok = has && e <= 1.0 && b.rulesAt >= 0 && b.firstAt >= 0 && b.defSeen == 0;
            }
            finally
            {
                foreach (var (f, v) in saved) f.SetValue(null, v);
                ZWorld.CalmStartDeviation = calm; ZWorld.TowerDrawnReachDeviation = had;
            }
            sb.AppendLine(); sb.AppendLine(OzV11Kit.Verdict(ok, "DV208: level 7's tower drawn where the view stands it; the medium tank has it in reach and fires at it, no roof sniper first"));
            return sb.ToString();
        }
    }
}
