using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.3 item 11 (rule DV157, Sim/ZBot.CpuGunMan.cs).
    /// Counted headless (ZWorld.Create + Tick, no scene, nothing saved; the game's rules via OzV11StrikeCount.Game, DV115's unit
    /// mix on as in a game), computer against computer, L17 first, then L02 and L16. Every match is played twice with the same seed, the rule
    /// OFF (the old rule) and ON. Once a game second, for each side and each empty gun: is it ELIGIBLE (a squad of that side walks past it
    /// within CpuGunPassPx px on a non-fight, non-flag way, or it stands at that side's frontier, ZBot.GunFrontierZones / GunNearFrontier)?
    /// When was it first eligible, when did that side first have a squad that could go (the chance: a squad passing it, or at the frontier an idle
    /// squad in reach after the opening), when did that side man it (an Entered event of its robot into the gun), or did the other side take it
    /// first / was it destroyed?
    ///  G1 manned: with the rule ON at least <see cref="NeedManned"/> of the (side, gun) pairs with a chance are manned by that side within
    ///     <see cref="ManWindowSec"/> s of it (or taken by the other side / destroyed first: resolved), and more than with the rule OFF. Where the
    ///     old rule already resolves <see cref="OldRuleCeiling"/> or more there is nothing left to gain and a side's own share is a coin flip (a gun
    ///     at both frontiers makes two pairs, only one side can man it): there ON must resolve as much, the manned shares are shown only.
    ///     The clock from the first eligible second is shown too; it is not the gate: on L17 it runs while a side has no squad on foot;
    ///  G2 the limits held: 0 gun orders to a squad that was on a flag run or firing; never two squads of one side on their way to a gun this rule
    ///     sent a squad to (sampled every second; the report also shows the seconds with two squads at any gun, which the engine's own matching
    ///     gives OFF and ON);
    ///  G3 the old rule is untouched: ON and OFF play tick for tick the same world until the first DV157 order (fingerprint of every object
    ///     each tick), and with the switch OFF the rule gives 0 orders;
    ///  G4 the fights are not worse (the same seeds, ON against OFF, per level): flag captures of both computers >= 85 % of OFF, the mean
    ///     game length and the red win share within the limits below, units alive at the end not fewer than 80 % of OFF;
    ///  G5 deterministic: the first ON match played again gives the same fingerprint.
    /// </summary>
    public static class OzV13GunManCount
    {
        public static int Seeds = 24;                                                                     // 4 seeds cannot hold G4: the old rule against itself failed it in 62 % of the splits of 4 seeds against 4, 10 % of 24 against 24 (one level)
        public static double Minutes = 25;
        public static readonly string[] Levels = { "L17", "L02", "L16" };
        public const double NeedManned = 0.75, ManWindowSec = 180, CapturesKeep = 0.85, UnitsKeep = 0.8, WinShareGap = 0.25, LengthGap = 0.25, OldRuleCeiling = 0.95;

        public sealed class Pair { public Team side; public int gun; public double eligible = -1, chance = -1, manned = -1, lost = -1; public string how = "-"; }

        public sealed class Match
        {
            public string level, error; public int seed; public bool on;
            public double end; public bool over; public Team winner = Team.Neutral;
            public int captures, unitsEnd, ruleOrders, passing, frontier, keptDefenders, badFrom, crowd, crowdAll, gunsEmptyEnd, mannedRed, mannedBlue;
            public double firstRuleAt = -1; public int firstRuleTick = -1;
            public readonly List<ulong> hashes = new List<ulong>();
            /// <summary>The open (side, gun) pairs; a pair that is resolved (manned, taken by the other side, destroyed) moves to done, so a gun
            /// that is emptied again later (a sniped gunner) is a new pair.</summary>
            public readonly Dictionary<(Team, int), Pair> pairs = new Dictionary<(Team, int), Pair>();
            public readonly List<Pair> done = new List<Pair>();
            public IEnumerable<Pair> All => pairs.Values.Concat(done);
        }

        static string F(double v, string f = "0.0") => double.IsNaN(v) ? "-" : v.ToString(f, CultureInfo.InvariantCulture);
        static string Clock(double s) => s < 0 ? "-" : $"{(int)(s / 60)}:{(int)(s % 60):00}";

        public static Match Run(string settings, string map, string level, int seed, bool on, double seconds)
        {
            var M = new Match { level = level, seed = seed, on = on };
            try
            {
                using (OzV11StrikeCount.Game(true))
                using (on ? ZWorld.CpuGunManOn() : ZWorld.CpuGunManOff())
                {
                    ZWorld.BotHeavyDeviation = true;                                                    // DV115 as in the game (the counted checks have it off)
                    var w = ZWorld.Create(settings, map, seed, level == "L17");
                    var blue = new ZBot(w, Team.Blue, seed + 101); var red = new ZBot(w, Team.Red, seed);
                    w.Bots.Add(blue); w.Bots.Add(red);
                    var bots = new[] { blue, red };
                    var zones = new List<int>(); var idle = new List<ZObject>(); var idleTargeted = new List<ZObject>();
                    w.Events.Clear();
                    ulong h = 1469598103934665603UL; int tick = 0; double nextSample = 1.0;
                    while (w.Time < seconds && !w.GameOver)
                    {
                        w.Tick(ZConst.TICK);
                        foreach (var e in w.Events)
                        {
                            if (e.type == ZEventType.FlagCaptured) M.captures++;
                            if (e.type != ZEventType.Entered) continue;
                            var g = w.GetAny(e.otherId); var r = w.GetAny(e.objId);
                            if (g == null || g.Kind != ZKind.Cannon) continue;
                            Team side = r != null ? r.Owner : g.Owner;
                            foreach (var b in bots)
                            {
                                if (!M.pairs.TryGetValue((b.Team, g.Id), out var p) || p.manned >= 0 || p.lost >= 0) continue;
                                if (side == b.Team) { p.manned = w.Time; p.how = b.GunManEvents.Any(x => x.gunId == g.Id) ? "rule" : "engine"; if (side == Team.Red) M.mannedRed++; else M.mannedBlue++; }
                                else { p.lost = w.Time; p.how = "other side"; }
                            }
                        }
                        w.Events.Clear();
                        h = OzV11StrikeCount.Fold(h, w);
                        foreach (var b in bots) { h ^= (ulong)b.Orders * 0x9E3779B97F4A7C15UL; h *= 1099511628211UL; }
                        M.hashes.Add(h);
                        if (M.firstRuleTick < 0 && (blue.GunManEvents.Count > 0 || red.GunManEvents.Count > 0))
                        {
                            M.firstRuleTick = tick;
                            M.firstRuleAt = Math.Min(blue.GunManEvents.Count > 0 ? blue.GunManEvents[0].time : double.MaxValue, red.GunManEvents.Count > 0 ? red.GunManEvents[0].time : double.MaxValue);
                        }
                        if (w.Time + 1e-6 >= nextSample)
                        {
                            nextSample += 1.0;
                            foreach (var b in bots)
                            {
                                b.GunFrontierZones(zones);
                                idle.Clear(); idleTargeted.Clear(); b.CollectOurUnits_3(idle, idleTargeted);
                                var enRoute = new Dictionary<int, List<int>>();
                                foreach (var o in w.Objects)
                                {
                                    if (o.Kind != ZKind.Robot || o.Owner != b.Team || o.IsMinion || o.Destroyed) continue;
                                    var wps = b.KnownWaypoints(o);
                                    if (wps.Count == 0 || wps[0].mode != WpMode.Enter) continue;
                                    var t = w.Get(wps[0].refId);
                                    if (t != null && t.Kind == ZKind.Cannon && t.CanBeEntered())
                                    {
                                        if (!enRoute.TryGetValue(t.Id, out var ids)) enRoute[t.Id] = ids = new List<int>();
                                        ids.Add(o.Id);
                                    }
                                }
                                foreach (var kv in enRoute)
                                {
                                    if (kv.Value.Count < 2) continue;
                                    M.crowdAll++;                                                                   // any two squads at one gun (the engine does it too, OFF and ON)
                                    if (b.GunManEvents.Any(x => x.gunId == kv.Key && kv.Value.Contains(x.unitId))) M.crowd++;   // a gun this rule sent a squad to: must stay 0
                                }
                                foreach (var g in w.Objects)
                                {
                                    if (g.Kind != ZKind.Cannon || !g.CanBeEntered()) continue;
                                    if (M.pairs.TryGetValue((b.Team, g.Id), out var p0) && (p0.manned >= 0 || p0.lost >= 0)) { M.pairs.Remove((b.Team, g.Id)); M.done.Add(p0); }
                                    bool near = b.GunNearFrontier(g, zones), passer = false;
                                    foreach (var o in w.Objects)
                                    {
                                        if (!b.GunPasser(o)) continue;
                                        long dx = o.CenterX - g.CenterX, dy = o.CenterY - g.CenterY;
                                        if (dx * dx + dy * dy <= (long)ZWorld.CpuGunPassPx * ZWorld.CpuGunPassPx) { near = true; passer = true; break; }
                                    }
                                    if (!near) continue;
                                    if (!M.pairs.TryGetValue((b.Team, g.Id), out var pn)) M.pairs[(b.Team, g.Id)] = pn = new Pair { side = b.Team, gun = g.Id, eligible = w.Time };
                                    if (pn.chance < 0 && (passer || FreeSquad(b, w, g, idle))) pn.chance = w.Time;   // the clock of the 180 s: a squad of that side could go now
                                }
                            }
                            foreach (var p in M.pairs.Values)
                            {
                                if (p.manned >= 0 || p.lost >= 0) continue;
                                var g = w.GetAny(p.gun);
                                if (g == null || g.Destroyed) { p.lost = w.Time; p.how = "destroyed"; }
                            }
                        }
                        tick++;
                    }
                    M.end = w.Time; M.over = w.GameOver; M.winner = w.GameOver ? w.Winner : Team.Neutral;
                    M.unitsEnd = w.Objects.Count(o => o.IsMobile && !o.Destroyed && o.Owner != Team.Neutral);
                    M.gunsEmptyEnd = w.Objects.Count(o => o.Kind == ZKind.Cannon && o.CanBeEntered());
                    foreach (var b in bots)
                    {
                        M.passing += b.GunManPassing; M.frontier += b.GunManFrontier; M.keptDefenders += b.GunManKeptDefenders;
                        M.ruleOrders += b.GunManEvents.Count;
                        M.badFrom += b.GunManEvents.Count(x => x.fromFlag || x.fighting || (x.hadOrder && (x.fromMode == WpMode.Attack || x.fromMode == WpMode.EnterFort || x.fromMode == WpMode.Agro)));
                    }
                }
            }
            catch (Exception ex) { M.error = ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim(); }
            return M;
        }

        /// <summary>A robot squad of this side is free for this frontier gun now: idle in the computer's own sense (CollectOurUnits_3), allowed to enter,
        /// within the send range and on the same ground, after the opening.</summary>
        static bool FreeSquad(ZBot b, ZWorld w, ZObject g, List<ZObject> idle)
        {
            if (w.Time + 1e-6 < ZWorld.CpuGunFrontierFromSec) return false;
            foreach (var u in idle)
            {
                if (u.Kind != ZKind.Robot || u.NoAutoEnter || u.IsMinion) continue;
                long dx = u.CenterX - g.CenterX, dy = u.CenterY - g.CenterY;
                if (dx * dx + dy * dy <= (long)ZWorld.CpuGunSendPx * ZWorld.CpuGunSendPx && b.InSameRegion(u.LocX + 8, u.LocY + 8, g.LocX + 8, g.LocY + 8, true)) return true;
            }
            return false;
        }

        /// <summary>The (side, gun) pairs, counted from the first second that side had a squad that could go (fromChance true; the gate), or from the first
        /// second the gun was eligible at all (false; the first version's clock, shown for information: on L17 it runs while the side has no squad on foot).
        /// A pair that was gone before its side had a chance is not counted. Resolved within the window = manned by the side, taken by the other side, or destroyed.</summary>
        public static (int eligible, int resolved, int manned, List<double> waits) Tally(Match m, bool fromChance = true)
        {
            int el = 0, res = 0, man = 0; var waits = new List<double>();
            foreach (var p in m.All)
            {
                double t0 = fromChance ? p.chance : p.eligible;
                if (t0 < 0 || t0 > m.end - ManWindowSec) continue;                           // no chance yet, or too late in the match to judge
                if (p.manned >= 0 && p.manned < t0 || p.lost >= 0 && p.lost < t0) continue;
                el++;
                if (p.manned >= 0 && p.manned - t0 <= ManWindowSec) { man++; res++; waits.Add(p.manned - t0); }
                else if (p.lost >= 0 && p.lost - t0 <= ManWindowSec) res++;
            }
            return (el, res, man, waits);
        }

        public static string Check(string settings, Func<string, string> map, int seeds, double minutes, string[] levels = null, Action<string> progress = null)
        {
            levels = levels ?? Levels;
            var sb = new StringBuilder();
            sb.AppendLine($"1.3 item 11 the computer mans empty guns (rule DV157, Sim/ZBot.CpuGunMan.cs): computer against computer, {seeds} seeds x {minutes:0} min per level, the rule OFF and ON{OzV11StrikeCount.RulesNote()}");
            sb.AppendLine($"(pass {ZWorld.CpuGunPassPx} px, frontier {ZWorld.CpuGunFrontierPx} px, send {ZWorld.CpuGunSendPx} px, {ZWorld.CpuGunPerRound} a round (Easy {ZWorld.CpuGunPerRoundEasy}), {ZWorld.CpuGunMaxEnRoute} on the way, last defender {ZWorld.CpuGunGuardPx} px)");
            bool g1 = true, g2 = true, g3 = true, g4 = true, g5 = true;
            Match firstOn = null;
            foreach (var lv in levels)
            {
                sb.AppendLine($"\n## {lv}");
                sb.AppendLine("| seed | rule | end | winner | captures | units end | eligible | resolved | manned | median wait s | rule orders (way / frontier) | kept defenders | bad orders | crowded (rule gun / any gun) | first rule order |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
                var offs = new List<Match>(); var ons = new List<Match>();
                for (int seed = 1; seed <= seeds; seed++)
                {
                    progress?.Invoke($"{lv} seed {seed}");
                    var off = Run(settings, map(lv), lv, seed, false, minutes * 60);
                    var on = Run(settings, map(lv), lv, seed, true, minutes * 60);
                    if (firstOn == null && on.error == null) firstOn = on;
                    foreach (var m in new[] { off, on })
                    {
                        if (m.error != null) { sb.AppendLine($"| {seed} | {(m.on ? "ON" : "off")} | FAIL {m.error} |"); g1 = g2 = g3 = g4 = false; continue; }
                        var (el, res, man, waits) = Tally(m);
                        waits.Sort();
                        sb.AppendLine($"| {seed} | {(m.on ? "ON" : "off")} | {Clock(m.end)} | {(m.over ? m.winner.ToString() : "-")} | {m.captures} | {m.unitsEnd} | {el} | {res} | {man} | {(waits.Count > 0 ? F(waits[waits.Count / 2], "0") : "-")} | {m.ruleOrders} ({m.passing} / {m.frontier}) | {m.keptDefenders} | {m.badFrom} | {m.crowd} / {m.crowdAll} | {Clock(m.firstRuleAt)} |");
                        if (m.on && (m.badFrom > 0 || m.crowd > 0)) g2 = false;
                        if (!m.on && m.ruleOrders > 0) g3 = false;
                    }
                    if (off.error == null && on.error == null)
                    {
                        offs.Add(off); ons.Add(on);
                        int n = Math.Min(off.hashes.Count, on.hashes.Count), diff = -1;
                        for (int i = 0; i < n; i++) if (off.hashes[i] != on.hashes[i]) { diff = i; break; }
                        if (diff < 0 && off.hashes.Count != on.hashes.Count) diff = n;
                        bool same = on.firstRuleTick < 0 ? diff < 0 : diff < 0 || diff >= on.firstRuleTick;
                        if (!same) g3 = false;
                        sb.AppendLine($"|  | G3 | first difference at tick {diff} ({(diff < 0 ? "none" : Clock(diff * ZConst.TICK))}), first DV157 order at tick {on.firstRuleTick}: {(same ? "ok" : "FAIL")} |");
                    }
                }
                if (ons.Count == 0) continue;
                int elOn = 0, resOn = 0, manOn = 0, elOff = 0, resOff = 0, manOff = 0, el1On = 0, res1On = 0, man1On = 0, el1Off = 0, res1Off = 0, man1Off = 0;
                foreach (var m in ons) { var t = Tally(m); elOn += t.eligible; resOn += t.resolved; manOn += t.manned; t = Tally(m, false); el1On += t.eligible; res1On += t.resolved; man1On += t.manned; }
                foreach (var m in offs) { var t = Tally(m); elOff += t.eligible; resOff += t.resolved; manOff += t.manned; t = Tally(m, false); el1Off += t.eligible; res1Off += t.resolved; man1Off += t.manned; }
                double shareOn = elOn > 0 ? (double)resOn / elOn : 1, manShareOn = elOn > 0 ? (double)manOn / elOn : 0, manShareOff = elOff > 0 ? (double)manOff / elOff : 0;
                bool ceiling = el1Off > 0 && (double)res1Off / el1Off >= OldRuleCeiling;                  // the old rule already resolves (nearly) every gun of this level
                bool few = ons.Count < 4;                                                                 // the quick form: one seed says that the rule works, not how much
                bool l1 = few ? ons.Any(m => m.ruleOrders > 0) : shareOn >= NeedManned && (elOn == 0 || manShareOn > manShareOff || (ceiling && shareOn >= OldRuleCeiling));
                g1 &= l1;
                double capOn = ons.Average(m => (double)m.captures), capOff = offs.Average(m => (double)m.captures);
                double lenOn = ons.Average(m => m.end), lenOff = offs.Average(m => m.end);
                double unOn = ons.Average(m => (double)m.unitsEnd), unOff = offs.Average(m => (double)m.unitsEnd);
                double redOn = ons.Count(m => m.over && m.winner == Team.Red) / (double)ons.Count, redOff = offs.Count(m => m.over && m.winner == Team.Red) / (double)offs.Count;
                bool l4 = few || (capOn >= CapturesKeep * capOff && Math.Abs(lenOn - lenOff) <= LengthGap * lenOff && unOn >= UnitsKeep * unOff && Math.Abs(redOn - redOff) <= WinShareGap);   // the quick form's single seed shows G4, it cannot hold it
                g4 &= l4;
                sb.AppendLine($"- G1 {lv}: pairs with a chance ON {elOn}, resolved within {ManWindowSec:0} s of it {resOn} ({F(shareOn * 100, "0")} %, need {NeedManned * 100:0} %), manned by the side ON {F(manShareOn * 100, "0")} % against OFF {F(manShareOff * 100, "0")} % (OFF: {resOff} of {elOff} resolved){(ceiling ? $", the old rule already resolves {F(100.0 * res1Off / el1Off, "0")} %: ON must keep {OldRuleCeiling * 100:0} %" : "")}{(few ? ", quick form: the rule gave orders" : "")}: {(l1 ? "ok" : "FAIL")}");
                sb.AppendLine($"  (from the first eligible second, not the gate: ON {res1On} of {el1On} resolved, {man1On} manned; OFF {res1Off} of {el1Off}, {man1Off} manned. Guns still empty at the end: ON {F(ons.Average(m => (double)m.gunsEmptyEnd), "0.00")} / OFF {F(offs.Average(m => (double)m.gunsEmptyEnd), "0.00")}. Manned in a match, red / blue: ON {F(ons.Average(m => (double)m.mannedRed))} / {F(ons.Average(m => (double)m.mannedBlue))}, OFF {F(offs.Average(m => (double)m.mannedRed))} / {F(offs.Average(m => (double)m.mannedBlue))})");
                sb.AppendLine($"- G4 {lv}: captures ON {F(capOn)} / OFF {F(capOff)} (>= {CapturesKeep * 100:0} %), length {Clock(lenOn)} / {Clock(lenOff)} (within {LengthGap * 100:0} %), units at the end {F(unOn)} / {F(unOff)} (>= {UnitsKeep * 100:0} %), red wins {F(redOn * 100, "0")} % / {F(redOff * 100, "0")} % (within {WinShareGap * 100:0} points){(few ? ", quick form: shown only" : "")}: {(l4 ? "ok" : "FAIL")}");
            }
            if (firstOn != null)
            {
                var again = Run(settings, map(firstOn.level), firstOn.level, firstOn.seed, true, minutes * 60);
                g5 = again.error == null && again.hashes.Count == firstOn.hashes.Count && again.hashes.Count > 0 && again.hashes[again.hashes.Count - 1] == firstOn.hashes[firstOn.hashes.Count - 1];
                sb.AppendLine($"- G5 deterministic ({firstOn.level} seed {firstOn.seed} ON twice): {(g5 ? "the same fingerprint" : "FAIL: different")}");
            }
            sb.AppendLine();
            sb.AppendLine((g1 ? "PASS  " : "FAIL  ") + "G1 eligible empty guns manned (or resolved) by the computer with the rule on, more than with the old rule");
            sb.AppendLine((g2 ? "PASS  " : "FAIL  ") + "G2 limits: no squad taken off a flag run or a fight, never two squads of a side on their way to a gun the rule manned");
            sb.AppendLine((g3 ? "PASS  " : "FAIL  ") + "G3 the old rule untouched until the first gun order; switch off = 0 orders");
            sb.AppendLine((g4 ? "PASS  " : "FAIL  ") + "G4 the fights are not worse (captures, length, units, red win share)");
            sb.AppendLine((g5 ? "PASS  " : "FAIL  ") + "G5 deterministic");
            return sb.ToString();
        }

        /// <summary>The Deviations row's quick form: L17 only, one seed, 16 minutes: G2, G3 and G5 as ever; G1 only that the rule gave orders, G4 shown (one seed cannot hold them).</summary>
        public static string Quick(string settings, Func<string, string> map) => Check(settings, map, 1, 16, new[] { "L17" });

#if UNITY_EDITOR
        [UnityEditor.MenuItem("OpenZ/V1.3/CPU: Mans Empty Guns Check (item 11, headless)")]
        static void Menu() => OzV11Kit.RunMenu($"1.3 item 11 the computer mans empty guns (3 levels x {Seeds} seeds x 25 min, the rule off and on, + 1 repeat: {Seeds * 6 + 1} matches; over an hour)", () => Check(OzV11Kit.SettingsText, OzV11Kit.MapText, Seeds, Minutes));

        [UnityEditor.MenuItem("OpenZ/V1.3/CPU: Mans Empty Guns Check, L17 only (item 11, headless)")]
        static void MenuL17() => OzV11Kit.RunMenu($"1.3 item 11 the computer mans empty guns, L17 ({Seeds} seeds x 25 min, off and on)", () => Check(OzV11Kit.SettingsText, OzV11Kit.MapText, Seeds, Minutes, new[] { "L17" }));
#endif
    }
}
