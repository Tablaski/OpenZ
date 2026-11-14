using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Counted check of version 1.2 items 14 and 14a (rule DV136, Sim/ZBot.CpuDefend.cs: from minute 8 one in four of the computer's
    /// attack / capture orders becomes a defend order: a frontier flag, a spot of its own on the enemy side, two minutes, then released).
    /// Headless: no scene, no Play mode, nothing saved. Whole matches on the real levels with the switches of a game in Play (the strike
    /// groups and the unit mix on, as View/ZGame.cs sets them), each run twice with the same seed: the rule off and on.
    /// <para>Sides: a computer match (both teams computer, Normal) and the computer (blue, Normal) against the strike check's stand-in
    /// player on red (OzV11StrikeCount.StandIn: he holds his four flags nearest his fort and his gate, never attacks, keeps his posts
    /// manned), so a match runs past minute 8 on every level (the computer matches of level 2 and level 16 are won before 8:00).</para>
    /// <para>Measured, every tick: the defend orders and when (none before 8:00); the counted orders (the share: about one in four); every
    /// guard when it first stands on its spot: its distance from its flag, the side (toward the enemy direction of that flag), the
    /// direction of the last 8 px it moved (it faces where it last moved), the nearest other guard, the nearest flag; while it holds:
    /// any order somebody else gives it (none expected); its release: after how long and why, and the order it got then. The world
    /// fingerprint (OzV11StrikeCount.Fold, every tick) of the rule-on match against the rule-off match: identical until the tick of the
    /// first defend order (the hooks change nothing before the rule acts). The outcome of the two matches side by side. One match is
    /// run a third time with the rule on: the same fingerprints and the same defend events (deterministic).</para>
    /// The other half of the off proof (the rule off against the build without the kit, tick for tick) needs the two builds side by side;
    /// the kit's headless harness does it (tools/headless.py, SPEC.md part 3).
    /// </summary>
    public static class OzV12CpuDefendCount
    {
        public static readonly string[] Levels = { "L02", "L16", "L17" };
        public enum Sides { ComputerMatch, PlayerHolds }

        /// <summary>A guard when it first stood on its spot.</summary>
        public sealed class Stand
        {
            public Team team; public int unit, flag; public string type; public double t, fromFlag, cosSide, cosFace = double.NaN, nearestGuard = double.MaxValue, nearestFlag, offSpot;
            /// <summary>Firing at an enemy when it stood (it faces its target then: a robot turns to it, a turret tracks it, a vehicle's duel reverse keeps its hull on it): cosFace is the target's direction.</summary>
            public bool firing;
        }

        /// <summary>One computer player's numbers in one match.</summary>
        public sealed class BotStats
        {
            public Team team; public int counted, defends, noPlace, released, lost, before8, maxGuards, foreignOrders, orderAfter, relHeld, relFlag, relWalk, relOther, successors;
            public int hostileFrom8; public double firstAt = -1; public readonly List<int> heldSecs = new List<int>();
            public readonly Dictionary<WpMode, int> byMode = new Dictionary<WpMode, int>();
        }

        public sealed class Match
        {
            public string level, error; public int seed; public Sides sides; public bool on; public ZBotDifficulty difficulty;
            public double end; public bool over; public Team winner = Team.Neutral; public int blueFlagsEnd, redFlagsEnd, blueFlags20 = -1, redFlags20 = -1, blueUnitsEnd, redUnitsEnd;
            public readonly List<BotStats> bots = new List<BotStats>(); public readonly List<Stand> stands = new List<Stand>();
            public readonly List<ulong> hashes = new List<ulong>(); public int firstDefendIndex = -1; public readonly List<string> events = new List<string>();
            public int heldWithSwitchOff;
        }

        static double D(int ax, int ay, int bx, int by) => Math.Sqrt((double)(ax - bx) * (ax - bx) + (double)(ay - by) * (ay - by));
        static string F(double v, string f = "0") => v.ToString(f, CultureInfo.InvariantCulture);
        static string Clock(double s) => $"{(int)(s / 60)}:{(int)(s % 60):00}";

        /// <summary>One match. <paramref name="keepHashes"/>: the fingerprint of every tick (for the comparison of two runs).</summary>
        public static Match Run(string settings, string map, string level, int seed, Sides sides, bool on, double maxSeconds, ZBotDifficulty difficulty = ZBotDifficulty.Normal, bool keepHashes = true)
        {
            var M = new Match { level = level, seed = seed, sides = sides, on = on, difficulty = difficulty };
            try
            {
                using (OzV11StrikeCount.Game(true))
                {
                    ZWorld.BotHeavyDeviation = true;                                                // DV115 as in the game (the counted checks have it off)
                    ZWorld.CpuDefendDeviation = on;
                    var w = ZWorld.Create(settings, map, seed, level == "L17");
                    var blue = new ZBot(w, Team.Blue, seed + 101) { Difficulty = difficulty };
                    w.Bots.Add(blue);
                    ZBot red = null;
                    if (sides == Sides.ComputerMatch) { red = new ZBot(w, Team.Red, seed) { Difficulty = difficulty }; w.Bots.Add(red); }
                    var standIn = sides == Sides.PlayerHolds ? OzV11StrikeCount.StandIn(w) : null;
                    w.Events.Clear();
                    var bots = red != null ? new[] { blue, red } : new[] { blue };
                    var stats = new Dictionary<ZBot, BotStats>(); var seen = new Dictionary<ZBot, int>(); var at8 = new Dictionary<ZBot, Dictionary<WpMode, int>>();
                    foreach (var b in bots) { var s = new BotStats { team = b.Team }; stats[b] = s; M.bots.Add(s); seen[b] = 0; }
                    var last = new Dictionary<int, (int x, int y, int dx, int dy)>();             // every guard: where it was 8 px ago, its last 8 px of movement
                    var foreign = new HashSet<long>();
                    ulong h = 1469598103934665603UL;
                    int tick = 0;
                    while (w.Time < maxSeconds && !w.GameOver)
                    {
                        w.Tick(ZConst.TICK);
                        if (standIn != null) OzV11StrikeCount.StandInKeep(w, standIn);
                        w.Events.Clear();
                        h = OzV11StrikeCount.Fold(h, w);
                        foreach (var b in bots) { h ^= (ulong)b.Orders * 0x9E3779B97F4A7C15UL; h *= 1099511628211UL; }
                        if (keepHashes) M.hashes.Add(h);
                        if (!on) { if (tick % 100 == 0) foreach (var o in w.Objects) if (o.CpuDefendHeld) M.heldWithSwitchOff++; }
                        foreach (var b in bots)
                        {
                            var S = stats[b];
                            if (!at8.ContainsKey(b) && w.Time + 1e-6 >= ZWorld.CpuDefendStartSec) at8[b] = new Dictionary<WpMode, int>(b.OrdersByMode);
                            if (M.firstDefendIndex < 0 && b.DefendOrders > 0) M.firstDefendIndex = tick;
                            if (b.DefendGuardCount == 0 && b.DefendEvents.Count == seen[b]) continue;
                            var views = b.DefendGuards();
                            S.maxGuards = Math.Max(S.maxGuards, views.Count);
                            foreach (var v in views)
                            {
                                var o = w.Get(v.unitId); if (o == null) continue;
                                if (!last.TryGetValue(v.unitId, out var a)) last[v.unitId] = (o.CenterX, o.CenterY, 0, 0);
                                else { int dx = o.CenterX - a.x, dy = o.CenterY - a.y; if (dx * dx + dy * dy >= 64) last[v.unitId] = (o.CenterX, o.CenterY, dx, dy); }
                                if (v.holding && o.Waypoints.Exists(p => p.playerGiven) && foreign.Add(((long)v.unitId << 20) ^ (long)(v.arrived * 100))) S.foreignOrders++;
                            }
                            for (; seen[b] < b.DefendEvents.Count; seen[b]++)
                            {
                                var e = b.DefendEvents[seen[b]];
                                M.events.Add($"{F(e.time, "0.00")} {b.Team} {e.kind} #{e.unitId} flag {e.flagId} ({e.x},{e.y}) n {e.n}{(e.note != null ? " " + e.note : "")}");
                                switch (e.kind)
                                {
                                    case ZBot.DefendEventKind.Ordered:
                                        if (e.time + 1e-6 < ZWorld.CpuDefendStartSec) S.before8++;
                                        if (S.firstAt < 0) S.firstAt = e.time;
                                        break;
                                    case ZBot.DefendEventKind.Holds:
                                        if (e.note != null) break;                                       // back on its spot after a step aside: measured once, the first time
                                        var o = w.Get(e.unitId); var fl = w.Get(e.flagId);
                                        int vi = views.FindIndex(x => x.unitId == e.unitId);
                                        if (o == null || fl == null || vi < 0) break;
                                        var v = views[vi];
                                        var st = new Stand { team = b.Team, unit = e.unitId, flag = e.flagId, type = o.Type, t = e.time, offSpot = e.n };
                                        st.fromFlag = D(o.CenterX, o.CenterY, fl.CenterX, fl.CenterY);
                                        st.cosSide = st.fromFlag < 1 ? -1 : ((o.CenterX - fl.CenterX) * v.dirX + (o.CenterY - fl.CenterY) * v.dirY) / st.fromFlag;
                                        var at = o.AttackObject;
                                        if (at != null && !at.Destroyed && (at.CenterX != o.CenterX || at.CenterY != o.CenterY))
                                        {   // firing: it faces its target
                                            st.firing = true;
                                            double tx = at.CenterX - o.CenterX, ty = at.CenterY - o.CenterY;
                                            st.cosFace = (tx * v.dirX + ty * v.dirY) / Math.Sqrt(tx * tx + ty * ty);
                                        }
                                        else if (last.TryGetValue(e.unitId, out var lm) && (lm.dx != 0 || lm.dy != 0)) st.cosFace = (lm.dx * v.dirX + lm.dy * v.dirY) / Math.Sqrt((double)lm.dx * lm.dx + (double)lm.dy * lm.dy);
                                        foreach (var b2 in bots)
                                            foreach (var v2 in b2.DefendGuards())
                                            {
                                                if (v2.unitId == e.unitId || !v2.holding) continue;
                                                var o2 = w.Get(v2.unitId); if (o2 != null) st.nearestGuard = Math.Min(st.nearestGuard, D(o.CenterX, o.CenterY, o2.CenterX, o2.CenterY));
                                            }
                                        st.nearestFlag = w.Flags.Min(f => D(o.CenterX, o.CenterY, f.CenterX, f.CenterY));
                                        M.stands.Add(st);
                                        break;
                                    case ZBot.DefendEventKind.Released:
                                        string why = e.note ?? "";
                                        if (why.StartsWith("held", StringComparison.Ordinal)) { S.relHeld++; S.heldSecs.Add(e.n); }
                                        else if (why.Contains("flag was lost")) S.relFlag++;
                                        else if (why.Contains("reach")) S.relWalk++;
                                        else S.relOther++;
                                        break;
                                    case ZBot.DefendEventKind.OrderAfter: S.orderAfter++; break;
                                    case ZBot.DefendEventKind.Successor: S.successors++; break;
                                }
                            }
                        }
                        if (M.blueFlags20 < 0 && w.Time + 1e-6 >= 1200) { M.blueFlags20 = w.Flags.Count(f => f.Owner == Team.Blue); M.redFlags20 = w.Flags.Count(f => f.Owner == Team.Red); }
                        tick++;
                    }
                    M.end = w.Time; M.over = w.GameOver; M.winner = w.GameOver ? w.Winner : Team.Neutral;
                    M.blueFlagsEnd = w.Flags.Count(f => f.Owner == Team.Blue); M.redFlagsEnd = w.Flags.Count(f => f.Owner == Team.Red);
                    M.blueUnitsEnd = w.Objects.Count(o => o.IsMobile && !o.Destroyed && o.Owner == Team.Blue); M.redUnitsEnd = w.Objects.Count(o => o.IsMobile && !o.Destroyed && o.Owner == Team.Red);
                    foreach (var b in bots)
                    {
                        var S = stats[b];
                        S.counted = b.DefendCounted; S.defends = b.DefendOrders; S.noPlace = b.DefendNoPlace; S.released = b.DefendReleased; S.lost = b.DefendLost;
                        foreach (var kv in b.DefendByMode) S.byMode[kv.Key] = kv.Value;
                        if (at8.TryGetValue(b, out var m8))
                            foreach (var k in new[] { WpMode.Attack, WpMode.Move, WpMode.EnterFort })
                                S.hostileFrom8 += (b.OrdersByMode.TryGetValue(k, out int x) ? x : 0) - (m8.TryGetValue(k, out int y) ? y : 0);
                    }
                }
            }
            catch (Exception ex) { M.error = ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim(); }
            return M;
        }

        /// <summary>The first tick where two fingerprint lists differ (-1: identical, the same length).</summary>
        public static int FirstDiff(List<ulong> a, List<ulong> b)
        {
            int n = Math.Min(a.Count, b.Count);
            for (int i = 0; i < n; i++) if (a[i] != b[i]) return i;
            return a.Count == b.Count ? -1 : n;
        }

        static string Side(Sides s) => s == Sides.ComputerMatch ? "computer match" : "against the stand-in";
        static string Outcome(Match m) => m.error != null ? "ERROR " + m.error : $"{(m.over ? m.winner + " won at " + Clock(m.end) : "still on at " + Clock(m.end))}, flags blue {m.blueFlagsEnd} red {m.redFlagsEnd}{(m.blueFlags20 >= 0 ? $" (at 20:00 {m.blueFlags20} / {m.redFlags20})" : "")}, units blue {m.blueUnitsEnd} red {m.redUnitsEnd}";

        /// <summary>
        /// The check: for every level and seed, the computer match and the match against the stand-in, the rule off and on (and the first one
        /// a third time, on). <paramref name="minutes"/>: the length of a match unless it is won before. Returns the report (PASS / FAIL lines last).
        /// </summary>
        public static string Check(string settings, Func<string, string> map, int seeds, double minutes, string[] levels = null, Sides[] sides = null, int firstSeed = 1, Action<string> progress = null, bool quick = false, bool easy = false)
        {
            int minCounted = quick ? 12 : 40, minStands = quick ? 4 : 20;                       // the quick form (one match) judges fewer orders
            levels ??= Levels; sides ??= new[] { Sides.ComputerMatch, Sides.PlayerHolds };
            var sb = new StringBuilder();
            sb.AppendLine($"1.2 items 14 + 14a, the computer's defend orders (DV136): {string.Join(" ", levels)}, seeds {firstSeed}-{firstSeed + seeds - 1}, {F(minutes)} min a match (or its end), " +
                          $"{string.Join(" and ", sides.Select(Side))}, the rule off and on; the game's switches (strike groups and unit mix on){OzV11StrikeCount.RulesNote()}");
            sb.AppendLine($"the rule: from {Clock(ZWorld.CpuDefendStartSec)} one in {ZWorld.CpuDefendEvery} counted orders; spots {ZWorld.CpuDefendNearPx}-{ZWorld.CpuDefendFarPx} px from the flag (best {ZWorld.CpuDefendBestPx}), " +
                          $"{ZWorld.CpuDefendPitchPx} px apart, at most {ZWorld.CpuDefendPerFlag} a flag; hold {F(ZWorld.CpuDefendHoldSec)} s; walk at most {F(ZWorld.CpuDefendWalkSec)} s");
            var all = new List<(Match off, Match on)>();
            int sameBefore = 0, pairs = 0; var diffNotes = new List<string>(); int offDefends = 0, offHeld = 0, errors = 0;
            bool twinOk = true; string twinNote = "not run";
            foreach (var lv in levels)
            {
                string mp = map(lv);
                foreach (var sd in sides)
                    for (int s = firstSeed; s < firstSeed + seeds; s++)
                    {
                        progress?.Invoke($"{lv} {Side(sd)} seed {s}");
                        var off = Run(settings, mp, lv, s, sd, false, minutes * 60);
                        var on = Run(settings, mp, lv, s, sd, true, minutes * 60);
                        all.Add((off, on));
                        if (off.error != null || on.error != null) { errors++; sb.AppendLine($"{lv} {Side(sd)} seed {s}: off {Outcome(off)} | on {Outcome(on)}"); continue; }
                        pairs++;
                        int diff = FirstDiff(off.hashes, on.hashes);
                        bool ok = on.firstDefendIndex < 0 ? diff < 0 : diff < 0 || diff >= on.firstDefendIndex;
                        if (ok) sameBefore++; else diffNotes.Add($"{lv} {Side(sd)} seed {s}: first different tick {diff}, first defend order at tick {on.firstDefendIndex}");
                        offDefends += off.bots.Sum(b => b.defends + b.counted); offHeld += off.heldWithSwitchOff;
                        sb.AppendLine($"{lv} {Side(sd)} seed {s}");
                        sb.AppendLine($"   off: {Outcome(off)}");
                        sb.AppendLine($"   on:  {Outcome(on)}; the world identical to the off match until tick {(diff < 0 ? "the end" : diff.ToString())} (first defend order at tick {(on.firstDefendIndex < 0 ? "none" : on.firstDefendIndex + " = " + Clock((on.firstDefendIndex + 1) * ZConst.TICK))})");
                        foreach (var b in on.bots)
                            sb.AppendLine($"   {b.team}: counted {b.counted}, defend {b.defends} ({(b.counted > 0 ? F(100.0 * b.defends / b.counted) : "-")} %; first at {(b.firstAt < 0 ? "-" : Clock(b.firstAt))}; " +
                                          $"instead of {string.Join(" ", b.byMode.OrderBy(k => (int)k.Key).Select(k => k.Key + " " + k.Value))}), no free spot {b.noPlace}, before 8:00 {b.before8}; " +
                                          $"released {b.released} (held {b.relHeld}, flag lost {b.relFlag}, no way {b.relWalk}, other {b.relOther}), order after {b.orderAfter}, lost {b.lost}, squads on under a new leader {b.successors}; " +
                                          $"most guards at once {b.maxGuards}; orders by others while holding {b.foreignOrders}; attack / flag / fort orders from 8:00 {b.hostileFrom8}");
                    }
            }
            // EASY: the first level, the computer against the stand-in on EASY (its slower clock, its limit on attackers), the rule off and on
            Match eOff = null, eOn = null; int eDiff = -2;
            if (easy)
            {
                var lv = levels[0];
                progress?.Invoke($"{lv} against the stand-in seed {firstSeed}, EASY");
                eOff = Run(settings, map(lv), lv, firstSeed, Sides.PlayerHolds, false, minutes * 60, ZBotDifficulty.Easy);
                eOn = Run(settings, map(lv), lv, firstSeed, Sides.PlayerHolds, true, minutes * 60, ZBotDifficulty.Easy);
                if (eOff.error == null && eOn.error == null) eDiff = FirstDiff(eOff.hashes, eOn.hashes);
                sb.AppendLine($"{lv} against the stand-in seed {firstSeed}, the computer on EASY");
                sb.AppendLine($"   off: {Outcome(eOff)}");
                sb.AppendLine($"   on:  {Outcome(eOn)}; the world identical to the off match until tick {(eDiff < 0 ? "the end" : eDiff.ToString())} (first defend order at tick {(eOn.firstDefendIndex < 0 ? "none" : eOn.firstDefendIndex.ToString())})");
                foreach (var b in eOn.bots)
                    sb.AppendLine($"   {b.team}: counted {b.counted}, defend {b.defends} (first at {(b.firstAt < 0 ? "-" : Clock(b.firstAt))}), before 8:00 {b.before8}, released {b.released} (held {b.relHeld}), stood {eOn.stands.Count}, orders by others while holding {b.foreignOrders}");
            }

            // a third run, on, of the first match whose rule-on run had defend events (else the first match): deterministic. Not simply the
            // first match: the computer matches of level 2 and level 16 are often won before 8:00, and a match without a defend order proves nothing here.
            var twin = all.FirstOrDefault(p => p.on.error == null && p.on.events.Count > 0);
            if (twin.on == null && all.Count > 0) twin = all[0];
            if (twin.on != null && twin.on.error == null)
            {
                var a = twin.on;
                var again = Run(settings, map(a.level), a.level, a.seed, a.sides, true, minutes * 60);
                int d = FirstDiff(a.hashes, again.hashes);
                twinOk = again.error == null && d < 0 && a.events.SequenceEqual(again.events);
                twinNote = $"{a.level} {Side(a.sides)} seed {a.seed} run again with the rule on: fingerprints {(d < 0 ? "identical" : "differ from tick " + d)}, defend events {(a.events.SequenceEqual(again.events) ? "identical (" + a.events.Count + ")" : "DIFFERENT")}";
            }

            var ons = all.Where(p => p.on.error == null).Select(p => p.on).ToList();
            var bs = ons.SelectMany(m => m.bots).ToList();
            var stands = ons.SelectMany(m => m.stands).ToList();
            int counted = bs.Sum(b => b.counted), defends = bs.Sum(b => b.defends), before8 = bs.Sum(b => b.before8);
            sb.AppendLine();
            sb.AppendLine($"guards that stood on their spot: {stands.Count}; by type: {string.Join(", ", stands.GroupBy(s => s.type).OrderBy(g => g.Key).Select(g => g.Key + " " + g.Count()))}");
            if (stands.Count > 0)
            {
                sb.AppendLine($"   from the flag: {F(stands.Min(s => s.fromFlag))}-{F(stands.Max(s => s.fromFlag))} px (mean {F(stands.Average(s => s.fromFlag))}); off its spot when it stood: mean {F(stands.Average(s => s.offSpot), "0.0")} px, most {F(stands.Max(s => s.offSpot))}");
                sb.AppendLine($"   side (cosine to the enemy direction): least {F(stands.Min(s => s.cosSide), "0.00")}, mean {F(stands.Average(s => s.cosSide), "0.00")}");
                var faced = stands.Where(s => !double.IsNaN(s.cosFace)).ToList();
                if (faced.Count > 0) sb.AppendLine($"   facing (cosine to the enemy direction; the last 8 px it moved, or its target's direction when it stood firing ({faced.Count(s => s.firing)})): {faced.Count} measured, mean {F(faced.Average(s => s.cosFace), "0.00")}, within 90 degrees {faced.Count(s => s.cosFace >= 0)}, within 45 degrees {faced.Count(s => s.cosFace >= 0.707)}");
                var withMate = stands.Where(s => s.nearestGuard < double.MaxValue).ToList();
                if (withMate.Count > 0) sb.AppendLine($"   nearest other guard when it stood: least {F(withMate.Min(s => s.nearestGuard))} px ({withMate.Count} with another guard holding)");
                sb.AppendLine($"   nearest flag: least {F(stands.Min(s => s.nearestFlag))} px");
                foreach (var s in stands.Where(s => s.fromFlag < ZWorld.CpuDefendNearPx - ZWorld.CpuDefendSettlePx - 4 || s.fromFlag > ZWorld.CpuDefendFarPx + ZWorld.CpuDefendSettlePx + 8 || s.cosSide < 0
                                                    || s.nearestFlag < 32 || (!double.IsNaN(s.cosFace) && s.cosFace < 0) || s.nearestGuard < 24).Take(16))
                    sb.AppendLine($"   look at: {Clock(s.t)} {s.team} {s.type}#{s.unit} flag #{s.flag}: {F(s.fromFlag)} px from it, side {F(s.cosSide, "0.00")}, {(s.firing ? "its target" : "last move")} {(double.IsNaN(s.cosFace) ? "-" : F(s.cosFace, "0.00"))}, " +
                                  $"nearest guard {(s.nearestGuard < double.MaxValue ? F(s.nearestGuard) : "-")}, nearest flag {F(s.nearestFlag)}, off its spot {F(s.offSpot)}");
            }
            var held = bs.SelectMany(b => b.heldSecs).ToList();
            int released = bs.Sum(b => b.released), relHeld = bs.Sum(b => b.relHeld), orderAfter = bs.Sum(b => b.orderAfter), foreignOrders = bs.Sum(b => b.foreignOrders);
            sb.AppendLine($"releases: {released} (held {relHeld}{(held.Count > 0 ? $": {held.Min()}-{held.Max()} s" : "")}, flag lost {bs.Sum(b => b.relFlag)}, no way {bs.Sum(b => b.relWalk)}, other {bs.Sum(b => b.relOther)}); an order of the computer's own kind right after: {orderAfter}; lost on guard {bs.Sum(b => b.lost)}");
            sb.AppendLine(twinNote);
            foreach (var n in diffNotes) sb.AppendLine("   " + n);
            sb.AppendLine();

            var V = new List<string>();
            void Verdict(bool ok, string text) => V.Add((ok ? "PASS  " : "FAIL  ") + text);
            Verdict(errors == 0, $"D0 every match ran ({pairs} pairs{(errors > 0 ? ", " + errors + " with an error" : "")})");
            Verdict(before8 == 0 && bs.All(b => b.firstAt < 0 || b.firstAt + 1e-6 >= ZWorld.CpuDefendStartSec), $"D1 no defend order before 8:00 ({before8}); the first ones at {string.Join(", ", bs.Where(b => b.firstAt >= 0).Select(b => Clock(b.firstAt)).Distinct().Take(8))}");
            double share = counted > 0 ? 1.0 * defends / counted : 0;
            Verdict(counted >= minCounted && share >= 0.20 && share <= 0.27, $"D2 about one in four from 8:00: {defends} defend orders of {counted} counted = {F(100 * share, "0.0")} % (each block of 4 has one; a turn that finds no free spot passes on)");
            int lo = ZWorld.CpuDefendNearPx - ZWorld.CpuDefendSettlePx - 4, hi = ZWorld.CpuDefendFarPx + ZWorld.CpuDefendSettlePx + 8;
            int nearOk = stands.Count(s => s.fromFlag >= lo && s.fromFlag <= hi);
            int sideOk = stands.Count(s => s.cosSide >= 0), flagOk = stands.Count(s => s.nearestFlag >= 32);
            Verdict(stands.Count >= minStands && nearOk >= stands.Count * 0.95 && sideOk >= stands.Count * 0.95 && flagOk == stands.Count,
                    $"D3 near the frontier flag, on its enemy side, never on a flag: {nearOk} of {stands.Count} within {lo}-{hi} px, {sideOk} on the enemy side, {flagOk} at least 32 px from every flag");
            var faced2 = stands.Where(s => !double.IsNaN(s.cosFace)).ToList();
            Verdict(faced2.Count >= stands.Count * 0.8 && faced2.Count(s => s.cosFace >= 0) >= faced2.Count * 0.8,
                    $"D4 facing the enemy: of {faced2.Count} guards whose facing is known ({stands.Count} stood; {faced2.Count(s => s.firing)} of them firing at a target), {faced2.Count(s => s.cosFace >= 0)} faced the enemy side (the last 8 px they moved, or their target, within 90 degrees of the enemy direction; {faced2.Count(s => s.cosFace >= 0.707)} within 45)");
            var mates = stands.Where(s => s.nearestGuard < double.MaxValue).ToList();
            Verdict(mates.Count(s => s.nearestGuard >= 24) >= mates.Count * 0.95, $"D5 no overlap: {mates.Count(s => s.nearestGuard >= 24)} of {mates.Count} guards stood at least 24 px from every other holding guard (spots {ZWorld.CpuDefendPitchPx} px apart)");
            Verdict(relHeld > 0 && held.All(x => x >= ZWorld.CpuDefendHoldSec - 1 && x <= ZWorld.CpuDefendHoldSec + 1) && foreignOrders == 0 && relHeld >= released * 0.5,
                    $"D6 held 2 minutes, then released: {relHeld} of {released} releases after the hold ({(held.Count > 0 ? held.Min() + "-" + held.Max() : "-")} s), no order of anybody else while holding ({foreignOrders}); {orderAfter} got an order of their own right after");
            Verdict(pairs > 0 && sameBefore == pairs, $"D7 the rule changes nothing before it acts: in {sameBefore} of {pairs} matches the world was identical tick for tick to the rule-off match until the first defend order");
            Verdict(offDefends == 0 && offHeld == 0, $"D8 the switch off: no defend order, no turn counted, nobody held ({offDefends}, {offHeld})");
            Verdict(twinOk, "D9 deterministic: " + twinNote);
            // outcomes side by side: the side ahead at the end (the winner; in a match still on, the side holding more flags) and the flags held
            Team Ahead(Match m) => m.over ? m.winner : m.blueFlagsEnd > m.redFlagsEnd ? Team.Blue : m.redFlagsEnd > m.blueFlagsEnd ? Team.Red : Team.Neutral;
            var ok2 = all.Where(p => p.off.error == null && p.on.error == null).ToList();
            int sameEnd = ok2.Count(p => Ahead(p.off) == Ahead(p.on)), sameResult = ok2.Count(p => p.off.over == p.on.over && p.off.winner == p.on.winner);
            double flagDiff = ok2.Count > 0 ? ok2.Average(p => Math.Abs(p.off.blueFlagsEnd - p.on.blueFlagsEnd) + Math.Abs(p.off.redFlagsEnd - p.on.redFlagsEnd)) / 2.0 : 0;
            string outcome = $"the same side ahead at the end (the winner, or in a match still on the side holding more flags) in {sameEnd} of {pairs} matches, the same result (the same winner, or both still on) in {sameResult}, " +
                             $"a team's flags at the end {F(flagDiff, "0.0")} apart on average; every pair is listed above";
            if (pairs < 3) Verdict(pairs > 0, $"D10 outcomes: not judged on {pairs} match(es) (the whole check judges them): {outcome}");
            else Verdict(sameEnd >= pairs * 5 / 6 && flagDiff <= 2.0, "D10 outcomes not wildly changed: " + outcome);
            if (easy)
            {
                var eb = eOn?.bots.FirstOrDefault();
                bool eOk = eOn != null && eOn.error == null && eOff.error == null && eb != null && eb.defends > 0 && eb.before8 == 0 && eb.foreignOrders == 0
                           && (eDiff < 0 || (eOn.firstDefendIndex >= 0 && eDiff >= eOn.firstDefendIndex)) && eb.counted > 0 && 1.0 * eb.defends / eb.counted >= 0.15 && 1.0 * eb.defends / eb.counted <= 0.30;
                Verdict(eOk, eb == null ? "D11 EASY: the match did not run" : $"D11 EASY too: {eb.defends} defend orders of {eb.counted} counted, the first at {(eb.firstAt < 0 ? "-" : Clock(eb.firstAt))}, none before 8:00 ({eb.before8}), " +
                                                                              $"{eOn.stands.Count} stood on their spot, no order of anybody else while holding ({eb.foreignOrders}); the world identical to the rule-off match until the first one");
            }
            foreach (var v in V) sb.AppendLine(v);
            sb.AppendLine($"MACHINE counted {counted} defends {defends} stands {stands.Count} nearOk {nearOk} sideOk {sideOk} flagOk {flagOk} faced {faced2.Count} facedOk {faced2.Count(s => s.cosFace >= 0)} mates {mates.Count} matesOk {mates.Count(s => s.nearestGuard >= 24)} released {released} relHeld {relHeld} pairs {pairs} sameBefore {sameBefore} sameEnd {sameEnd} sameResult {sameResult} flagDiff {F(flagDiff, "0.00")} before8 {before8} foreign {foreignOrders} heldMin {(held.Count > 0 ? held.Min() : -1)} heldMax {(held.Count > 0 ? held.Max() : -1)}");
            return sb.ToString();
        }

        /// <summary>The Deviations row's probe: level 17, the computer against the stand-in player, one seed, 14 minutes, off and on. Not the
        /// computer match: with the rule on, level 17's computer match can be won before any guard's 2 minutes end, and D6 then cannot pass.</summary>
        public static string Quick(string settings, Func<string, string> map) => Check(settings, map, 1, 14, new[] { "L17" }, new[] { Sides.PlayerHolds }, 1, null, true);
    }
}
