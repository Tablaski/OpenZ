using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit fort_gun_target_r3: the counted check of DV204 "fort guns are answered on the way" (Sim/ZObject.FortGunAnswer.cs), no Unity API
    /// (the kit's Mono harness runs it too). Level 7 with its terrain and buildings, no start units or guns; the blue fort (the computer's,
    /// it gives no orders) gets one crewed MISSILE CANNON on its tower spot (112, 48) (centre 720, 160; 144 px, 158 px from the tower with
    /// DV162). Red units (the player's) start at (768, 480), south of the fort's east side, and are sent past it to (768, 64) on one order: a
    /// PLAIN move or an ATTACK-move (the game's right click: attack-move when no enemy is near at the click, else plain). The game's rule set
    /// (<see cref="GameRules"/>, as View/ZGame.cs sets it with default OPTIONS) with the calm start off; the fort's roof defender (DV41) comes
    /// as in the game. The rule off = P before this kit; on = DV204.
    /// Counted per run: the shots of the red units at the tower gun, the time from the gun's first shot at them to their first shot at it,
    /// their shots at other targets before that, whether the gun is destroyed (and when), red units alive at the end / sent, and whether
    /// the survivors reach the order's end.
    /// </summary>
    public static class OzV15FortGunTargetCount
    {
        public const int Runs = 2;
        const int StartX = 768, StartY = 480, GoalX = 768, GoalY = 64, ArrivePx = 48;
        const double MaxSec = 150;
        /// <summary>Zod's target beat (CheckPassiveEngage every 0.5 s) and the time a unit gets to turn its gun and fire once the tower's body is in its reach.</summary>
        const double Beat = 0.5, FireTime = 1.5;

        /// <summary>The rule switches View/ZGame.cs turns on at a level start with default OPTIONS that touch a fight like this one (combat,
        /// orders, pathing, the fort's guns and defender); every name that the simulation does not have is skipped.</summary>
        public static readonly string[] GameRules =
        {
            "LosDeviation", "DuelDeviation", "ApproachDeviation", "BuildingReachDeviation", "ExposedDriverDeviation", "ClearanceDeviation", "PathV3Deviation",
            "CalmMoveDeviation", "CalmFollowDeviation", "FireOnMoveDeviation", "HelpDeviation", "TowerAnswerDeviation", "HighGroundDeviation", "FortTurretRofDeviation",
            "FortDefenderDeviation", "FortSniperDeviation", "FortGunDeviation", "FortMortarDeviation", "VeteranDeviation", "SnipeDeviation", "SnipeSafeSpotDeviation",
            "ToughPrepareDeviation", "ToughSteadyDeviation", "SniperSteadyDeviation", "KnockHoldDeviation", "RocketSpeedDeviation", "HeavyPairDeviation",
            "MTankDeviation", "MTankHurryDeviation", "MTankAimDeviation", "PremiumDodgeDeviation", "MechHpDeviation", "MechHandGunDeviation", "MechGunLossDeviation",
            "GatlingBuffDeviation", "EmptiedTargetsDeviation", "UnmannedStopDeviation", "RiflePairDeviation", "GrenadeRuleDeviation", "DriverFireDeviation",
            "FormationDeviation", "AheadWalkDeviation", "GrabPolicyDeviation", "CloseGrabDeviation", "GrabCertainDeviation", "OrderMoveFallbackDeviation", "LavaDeviation",
        };

        internal sealed class Row { public string unit, name; public int count = 1; public bool am; }

        internal static List<Row> Rows()
        {
            var l = new List<Row>();
            foreach (var (u, n) in new[] { ("mtank", "medium tank"), ("light", "light tank"), ("heavy", "heavy tank"), ("medium", "mech"), ("jeep", "jeep"), ("grunt", "grunt squad"), ("tough", "tough squad") })
                foreach (bool am in new[] { false, true }) l.Add(new Row { unit = u, name = n, am = am });
            l.Add(new Row { unit = "mtank", name = "2 medium tanks together", count = 2, am = false });
            l.Add(new Row { unit = "mtank", name = "2 medium tanks together", count = 2, am = true });
            l.Add(new Row { unit = "mtank", name = "3 medium tanks together (a group)", count = 3, am = false });
            l.Add(new Row { unit = "mtank", name = "3 medium tanks together (a group)", count = 3, am = true });
            return l;
        }

        static string Map(string l07)
        {
            var m = ZMapData.FromJson(l07);
            m.objects.RemoveAll(o => { string c = o.kind.Split(':')[0]; return c == "robot" || c == "vehicle" || c == "cannon"; });
            return UnityEngine.JsonUtility.ToJson(m);
        }

        internal sealed class One
        {
            public int sent, alive, shotsGun, shotsOther, gunShots, answers; public bool gunDead, arrived; public double gunFirst = -1, firstAt = -1, gunDeadAt = -1, end, reachAt = -1; public bool fair;
            public string sig = "";
        }

        static bool Shot(ZEvent e) => e.type == ZEventType.MissileFired || e.type == ZEventType.Hitscan;

        internal static One RunOne(string settings, string l07, Row row, int seed, bool on, TextWriter trace = null)
        {
            ZWorld.FortGunAnswerDeviation = on;
            var w = OzV11Kit.World(settings, Map(l07), seed);
            var fort = w.Objects.First(o => o.IsFort && o.Owner == Team.Blue);
            var gun = w.CreateObject("cannon", "missile_cannon", fort.LocX + 112, fort.LocY + 48, Team.Blue); gun.Ejectable = false;   // as ZWorld.PlaceCannon on a tower spot
            OzV11Kit.Dummy(w, 66 * 16, 80 * 16);
            var units = new List<ZObject>();
            for (int i = 0; i < row.count; i++)
            {
                var u = OzV11Kit.Place(w, row.unit, StartX - 40 * i, StartY + 40 * i, Team.Red);
                units.AddRange(OzV11Kit.Squad(u));
                OzV11Kit.PlayerMove(w, u, GoalX, GoalY, row.am);
            }
            var ids = new HashSet<int>(units.Select(x => x.Id));
            int a0 = ZWorld.FortGunAnswers + ZWorld.FortGunEngages;
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
                    if (e.otherId == gun.Id) { r.shotsGun++; if (r.firstAt < 0) r.firstAt = w.Time; }
                    else if (r.firstAt < 0 && r.reachAt >= 0 && w.Time > r.reachAt + Beat) r.shotsOther++;   // another target while the tower's body was already in its reach (after one half-second target beat)
                }
                w.Events.Clear();
                if (!r.gunDead && !OzV11Kit.Alive(gun)) { r.gunDead = true; r.gunDeadAt = w.Time; }
                var live = units.Where(x => w.Objects.Contains(x) && OzV11Kit.Alive(x)).ToList();
                if (r.reachAt < 0 && !r.gunDead && live.Any(x => x.CanAttackObject(gun) && x.WithinAttackRadius(gun))) r.reachAt = w.Time;   // a red unit has the tower's body in its reach (DV204 body reach when on)
                if (r.reachAt >= 0 && !r.fair && w.Time >= r.reachAt + FireTime && w.Time < r.reachAt + FireTime + ZConst.TICK * 1.5)
                    r.fair = r.shotsGun > 0 || (!r.gunDead && live.Any(x => x.WithinAttackRadius(gun)));   // it lived FireTime in reach (time to turn and fire)
                if (trace != null && w.Time >= next)
                {
                    next += 0.5;
                    foreach (var u in live)
                        trace.WriteLine($"{w.Time,5:0.0} {u.Type}#{u.Id} ({u.CenterX},{u.CenterY}) d {OzV11Kit.Dist(u, gun),4:0} reach {(u.WithinAttackRadius(gun) ? 1 : 0)} hp {u.Health} wp {string.Join(",", u.Waypoints.Select(q => q.mode + ":" + q.refId + (q.attackTo ? "a" : "") + (q.playerGiven ? "p" : "")))} att {(u.AttackObject == null ? "-" : u.AttackObject.Type + "#" + u.AttackObject.Id)} | gun hp {gun.Health} on {(gun.AttackObject == null ? "-" : gun.AttackObject.Id.ToString())}");
                }
                if (live.Count == 0) break;
                bool there = live.All(x => x.Waypoints.Count == 0 && Math.Abs(x.CenterX - GoalX) <= ArrivePx && Math.Abs(x.CenterY - GoalY) <= ArrivePx);
                if (there && (r.gunDead || gun.AttackObject == null)) { r.arrived = true; break; }
            }
            r.end = w.Time;
            r.alive = units.Count(x => w.Objects.Contains(x) && OzV11Kit.Alive(x));
            r.answers = ZWorld.FortGunAnswers + ZWorld.FortGunEngages - a0;
            r.sig = $"{r.alive}/{r.shotsGun}/{r.shotsOther}/{r.gunShots}/{r.firstAt:0.00}/{r.gunDeadAt:0.00}/{r.arrived}/{r.end:0.00}/{r.reachAt:0.00}/{r.fair}/{w.Rng.Next(1 << 30)}";
            return r;
        }

        static string Sec(double t) => t < 0 ? "-" : t.ToString("0.0") + " s";

        /// <summary>The whole check: every row 'runs' times (seeds 1..runs) with the rule off and on. Returns the report (PASS / FAIL lines last).</summary>
        public static string Run(string settings, Func<string, string> map, int runs = Runs, TextWriter progress = null)
        {
            var sb = new StringBuilder();
            string l07 = map("L07");
            bool had = ZWorld.FortGunAnswerDeviation, calm = ZWorld.CalmStartDeviation;
            var saved = GameRules.Select(n => typeof(ZWorld).GetField(n)).Where(f => f != null && f.FieldType == typeof(bool)).Select(f => (f, (bool)f.GetValue(null))).ToList();
            int onNoShot = 0, onDefFirst = 0, onReach = 0, onReachShot = 0, offReach = 0, offReachShot = 0, groupRuns = 0, groupKills = 0, offShotRows = 0, rowsTwice = 0, onRows = 0, onKilled = 0, offKilled = 0, onBetter = 0, onWorse = 0;
            try
            {
                foreach (var (f, _) in saved) f.SetValue(null, true);
                ZWorld.CalmStartDeviation = false;
                sb.AppendLine($"DV204 fort guns are answered: level 7, blue fort's tower spot (112, 48) holds a crewed missile cannon; red units from ({StartX}, {StartY}) to ({GoalX}, {GoalY}) past it; {runs} runs a row (seeds 1-{runs}), off = P, on = DV204");
                sb.AppendLine("| Units | Order | Rule | In reach of the tower (s) | Shots at the tower gun | Time to first shot (after the gun's first) | Shots at others with the tower in reach, before it | Tower gun destroyed | Red alive / sent | Reached the order's end |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
                foreach (var row in Rows())
                {
                    var res = new Dictionary<bool, List<One>>();
                    foreach (bool on in new[] { false, true })
                    {
                        var list = new List<One>();
                        for (int s = 1; s <= runs; s++) { list.Add(RunOne(settings, l07, row, s, on)); progress?.Write("."); }
                        res[on] = list;
                        var two = RunOne(settings, l07, row, 1, on);
                        if (two.sig == list[0].sig) rowsTwice++; else sb.AppendLine($"(differs twice: {row.name} {(row.am ? "attack-move" : "plain")} {(on ? "on" : "off")}: {list[0].sig} / {two.sig})");
                        string Cell(Func<One, string> c) { var v = list.Select(c).Distinct().ToList(); return string.Join(" / ", list.Select(c)); }
                        sb.AppendLine($"| {row.name} | {(row.am ? "attack-move" : "plain move")} | {(on ? "on" : "off")} | {Cell(x => x.reachAt < 0 ? "never" : x.reachAt.ToString("0.0"))} | {Cell(x => x.shotsGun.ToString())} | {Cell(x => x.firstAt < 0 ? "never" : Sec(x.firstAt - x.gunFirst))} | {Cell(x => x.shotsOther.ToString())} | {Cell(x => x.gunDead ? "yes " + Sec(x.gunDeadAt) : "no")} | {Cell(x => x.alive + "/" + x.sent)} | {Cell(x => x.arrived ? "yes" : "no")} |");
                    }
                    onRows++;
                    foreach (var x in res[true]) { if (x.shotsGun == 0) onNoShot++; if (x.shotsOther > 0) onDefFirst++; if (x.gunDead) onKilled++; if (x.fair) { onReach++; if (x.shotsGun > 0) onReachShot++; } if (row.count >= 3) { groupRuns++; if (x.gunDead) groupKills++; } }
                    foreach (var x in res[false]) { if (x.shotsGun > 0) offShotRows++; if (x.gunDead) offKilled++; if (x.fair) { offReach++; if (x.shotsGun > 0) offReachShot++; } }
                    for (int i = 0; i < runs; i++)
                    {
                        var a = res[false][i]; var b = res[true][i];
                        int sa = (a.gunDead ? 1000 : 0) + a.alive * 10 + a.shotsGun, sb2 = (b.gunDead ? 1000 : 0) + b.alive * 10 + b.shotsGun;
                        if (sb2 > sa) onBetter++; else if (sb2 < sa) onWorse++;
                    }
                }
                int total = onRows * runs;
                sb.AppendLine();
                sb.AppendLine($"rule on: {total - onNoShot} of {total} runs fired at the tower gun, {onKilled} destroyed it; rule off (P): {offShotRows} of {total} fired at it, {offKilled} destroyed it; on better than off in {onBetter} runs, worse in {onWorse}");
                sb.AppendLine($"tower body in a red unit's reach and it alive {FireTime:0.0} s later: rule on {onReach} of {total} runs ({onReachShot} of them fired at the gun); rule off (P, centre range) {offReach} of {total} ({offReachShot} fired)");
                sb.AppendLine(OzV11Kit.Verdict(onReach > 0 && onReachShot == onReach, $"rule on: every run in which a red unit has the tower's body in its reach (and lives {FireTime:0.0} s) fires at the tower gun ({onReachShot} of {onReach})"));
                sb.AppendLine(OzV11Kit.Verdict(onDefFirst == 0, $"rule on: no shot at another target (the roof defender) while the tower's body is in reach (after one {Beat:0.0} s target beat), before the first shot at the gun ({onDefFirst} runs with one)"));
                sb.AppendLine(OzV11Kit.Verdict(groupRuns > 0 && groupKills * 2 >= groupRuns, $"rule on: a group (3 medium tanks) destroys the tower gun ({groupKills} of {groupRuns} runs)"));
                sb.AppendLine(OzV11Kit.Verdict(onKilled > offKilled, $"rule on destroys the tower gun more often than P ({onKilled} against {offKilled} of {total} runs)"));
                sb.AppendLine(OzV11Kit.Verdict(rowsTwice == onRows * 2, $"the same run twice gives the same result ({rowsTwice} of {onRows * 2} row / rule pairs)"));
            }
            finally
            {
                foreach (var (f, v) in saved) f.SetValue(null, v);
                ZWorld.CalmStartDeviation = calm; ZWorld.FortGunAnswerDeviation = had;
            }
            return sb.ToString();
        }

        /// <summary>Kit harness: one row, one seed, rule on / off, every half second each red unit (position, distance to the gun, waypoints, target).</summary>
        public static int Trace(string settings, Func<string, string> map, string rowIndex, int seed, bool on, TextWriter o)
        {
            var row = Rows()[int.Parse(rowIndex)];
            var saved = GameRules.Select(n => typeof(ZWorld).GetField(n)).Where(f => f != null && f.FieldType == typeof(bool)).ToList();
            foreach (var f in saved) f.SetValue(null, true);
            ZWorld.CalmStartDeviation = false;
            o.WriteLine($"{row.name} {(row.am ? "attack-move" : "plain move")} rule {(on ? "on" : "off")} seed {seed}");
            var r = RunOne(settings, map("L07"), row, seed, on, o);
            o.WriteLine($"END {Sec(r.end)}: shots at gun {r.shotsGun}, first {Sec(r.firstAt)} (gun first {Sec(r.gunFirst)}), other before {r.shotsOther}, gun destroyed {r.gunDead} {Sec(r.gunDeadAt)}, alive {r.alive}/{r.sent}, arrived {r.arrived}, gun shots {r.gunShots}, answers {r.answers}");
            return 0;
        }

        /// <summary>The parity row's probe: two rows (medium tank, plain move and attack-move), one run each, rule on.</summary>
        public static string Quick(string settings, string l07)
        {
            bool had = ZWorld.FortGunAnswerDeviation, calm = ZWorld.CalmStartDeviation;
            var saved = GameRules.Select(n => typeof(ZWorld).GetField(n)).Where(f => f != null && f.FieldType == typeof(bool)).Select(f => (f, (bool)f.GetValue(null))).ToList();
            var sb = new StringBuilder(); bool ok = true;
            try
            {
                foreach (var (f, _) in saved) f.SetValue(null, true);
                ZWorld.CalmStartDeviation = false;
                foreach (var row in Rows().Where(x => x.unit == "mtank" && x.count == 1))
                {
                    var a = RunOne(settings, l07, row, 1, false); var b = RunOne(settings, l07, row, 1, true);
                    sb.Append($"medium tank {(row.am ? "attack-move" : "plain move")}: shots at the tower gun {a.shotsGun} -> {b.shotsGun}, destroyed {(a.gunDead ? "yes" : "no")} -> {(b.gunDead ? "yes" : "no")}; ");
                    ok &= b.shotsGun > 0 && b.shotsOther == 0;
                }
            }
            finally
            {
                foreach (var (f, v) in saved) f.SetValue(null, v);
                ZWorld.CalmStartDeviation = calm; ZWorld.FortGunAnswerDeviation = had;
            }
            sb.AppendLine(); sb.AppendLine(OzV11Kit.Verdict(ok, "DV204: the medium tank fires at the fort's missile tower first on both orders"));
            return sb.ToString();
        }
    }
}
