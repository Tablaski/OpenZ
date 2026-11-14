using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.3 item 7 (rule DV154, Sim/ZWorld.ShuttleBigCrash.cs): the shuttle's BIG crash landing, counted headless on level 16 (ZWorld.Create
    /// + Tick, no scene, nothing saved; public members only). The ship is flown empty (its pilot only) from pad to pad near where it stands,
    /// one landing after the other, and every touchdown is read on its own tick: the health just before and just after it, rough or not,
    /// big or not. Health changes on any other tick (an enemy shot) are counted apart and are not the landing's. Lines:
    ///  B1 the share, ZWorld.ShuttleBigCrashOfAllLandings = false, the default): over <see cref="Landings"/>
    ///     landings each of 3 ships (3 different ids) the crash landings are exactly today's (ship 0's the same landings as with the switch
    ///     off, about one in 4), and exactly one crash landing in every block of 3 (by the ship's own crash count) is the big one, at most 2
    ///     big crashes in a row among the crashes, at most 4 of today's crashes in a row; every big crash is also a crash landing (the rules'
    ///     timing); about one landing in 12 is big;
    ///  B2 the loss: the first big crash of a full ship takes exactly 25 % of the full health (1014 of 4054: within half a hit point);
    ///  B3 the floor: the flights' health is exactly the rules' sum (each big crash 25 % down to the floor, nothing else); then a ship gets
    ///     6 big crashes in a row (the proof switch) from full health: never under 10 % of the full health (405), it ends at the floor, a big
    ///     crash at the floor loses nothing, destroyed by its own landing 0;
    ///  B4 the three cases: a big crash at 30 %, 15 % and 5 % health ends at the floor, at the floor and at 5 % (unchanged);
    ///  B5 the switch off (ZWorld.ShuttleBigCrashDeviation = false) = 1.2: about one landing in 4 is a crash, no big crash, no hit point lost
    ///     (ship 0's flights that B1 compares with);
    ///  B6 the other reading, the code switch (ZWorld.ShuttleBigCrashOfAllLandings = true): exactly one landing in 3 is a big crash (every
    ///     block of 3 landings has one), at most 2 in a row, at most 4 clean in a row, every crash landing the big one;
    ///  D  the same flights twice: the same landing sequence and the same hit points (deterministic).
    /// Run it from the menu OpenZ/V1.3/Shuttle: 7 Big Crash Check (out of Play), Editor/OzV13BigCrashMenu.cs.
    /// </summary>
    public static class OzV13BigCrashCount
    {
        const double Tick = ZConst.TICK;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        /// <summary>Landings flown per ship (a multiple of 3: whole blocks of the rules' bag); 36 = about 9 crash landings each under the
        /// owner's reading (about 3 blocks of 3 crashes).</summary>
        public const int Landings = 36;

        static string settings, m16;
        static StringBuilder sb; static int pass, fail;
        static void V(bool ok, string what) { sb.AppendLine(OzV11Kit.Verdict(ok, what)); if (ok) pass++; else fail++; }
        static void L(string s) => sb.AppendLine("      " + s);
        static string F(double v, string f = "0.000") => v.ToString(f, Inv);

        /// <summary>One touchdown as the rules made it.</summary>
        struct Td { public int n, rn, hpBefore, hpAfter, max; public bool rough, big; }   // n = the landing's number, rn = the ship's crash count after it

        /// <summary>A ship's flights: its touchdowns, health changes on other ticks, whether it died, its id.</summary>
        sealed class Flights
        {
            public readonly List<Td> td = new List<Td>(); public int otherHits, id, hovers, maxHp, lowest = int.MaxValue; public bool destroyed, stuck; public string why = "";
            public string Seq => new string(td.Select(t => t.big ? 'B' : t.rough ? 'c' : '.').ToArray());
            public string Digest => Seq + "|" + string.Join(",", td.Select(t => t.hpAfter));
        }

        static ZWorld W16(int seed)
        {
            ZWorld.ShuttlePlayableDeviation = true; ZWorld.ShuttleOn = true; ZWorld.ShuttleTeam = Team.Red;
            ZWorld.ShuttleRoughProofNext = false; ZWorld.ShuttleBigCrashProofNext = false;
            var w = OzV11Kit.World(settings, m16, seed);
            return w;
        }

        /// <summary>The ground round the ship cleared of other units and guns (nobody walks onto its pads, nobody shoots at it).</summary>
        static void Clear(ZWorld w, ZObject s)
        {
            foreach (var o in w.Objects.ToList())
                if (o != s && (o.IsRobot || (o.Kind == ZKind.Vehicle && !o.IsShuttle) || o.Kind == ZKind.Cannon) && OzV11Kit.Alive(o) && OzV11Kit.Dist(o, s) < 1400) o.KillAt = w.Time;
            for (int i = 0; i < 20; i++) { w.Tick(Tick); w.Events.Clear(); }
        }

        /// <summary>A pad (the ship can land there now) minR .. maxR px from its home (hx, hy) and at least 160 px from where it stands, a
        /// different direction each flight (turn): the ship stays round its home, in the cleared ground.</summary>
        static bool PadSpot(ZWorld w, ZObject s, int hx, int hy, int minR, int maxR, int turn, out int px, out int py)
        {
            for (int r = minR; r <= maxR; r += 16)
                for (int a0 = 0; a0 < 32; a0++)
                {
                    int a = (a0 + turn * 11) % 32;
                    double g = a * Math.PI / 16;
                    var (x, y) = w.ShuttleClamp(hx + (int)(Math.Cos(g) * r), hy + (int)(Math.Sin(g) * r));
                    double dx = x - s.CenterX, dy = y - s.CenterY;
                    if (dx * dx + dy * dy < 160.0 * 160.0) continue;
                    if (!w.ShuttleNeverLands(x, y, s, out _) && w.ShuttlePadFree(x, y, s, out _)) { px = x; py = y; return true; }
                }
            px = py = 0; return false;
        }

        /// <summary>One ship flown `count` landings (before each: `setHp` may set its health, `proofBig` the rules' proof switch).</summary>
        static Flights Fly(ZWorld w, ZObject s, int count, Func<int, int> setHp = null, bool proofBig = false)
        {
            var f = new Flights { id = s.Id, maxHp = s.MaxHealth };
            int hx = s.CenterX, hy = s.CenterY, retries = 0;
            for (int k = 0; k < count; k++)
            {
                // wait until it stands level on the ground with its ramp shut (a take-off would wait anyway)
                for (int i = 0; i < 2000 && !(s.ShuttleState == ZShuttleState.Landed && !s.ShuttleAirborne && s.ShuttleLevelled && s.ShuttleRampPhase == ZShuttleRamp.Shut); i++) { w.Tick(Tick); w.Events.Clear(); }
                if (setHp != null) { int h = setHp(k); if (h > 0) s.SetHealth(h); }
                if (!PadSpot(w, s, hx, hy, 240, 640, k + retries, out int gx, out int gy)) { f.stuck = true; f.why = "no pad near"; break; }
                ZWorld.ShuttleBigCrashProofNext = proofBig;
                OzV11Kit.PlayerMove(w, s, gx, gy, false);
                int land0 = s.ShuttleLandings, hov0 = s.ShuttleHovers; bool done = false;
                for (int i = 0; i < 9000 && !done; i++)                                             // at most 90 s of game time per flight
                {
                    int hp0 = s.Health;
                    w.Tick(Tick); w.Events.Clear();
                    if (s.Destroyed || s.KillAt >= 0) { f.destroyed = true; break; }
                    if (s.ShuttleLandings > land0)
                    {   // the touchdown tick: what this landing did (the rules' damage comes in this very tick)
                        f.td.Add(new Td { n = s.ShuttleLandings, rn = s.ShuttleRoughLandings, hpBefore = hp0, hpAfter = s.Health, max = s.MaxHealth, rough = s.ShuttleRoughLanding, big = s.ShuttleBigCrash });
                        done = true;
                    }
                    else if (s.Health != hp0) f.otherHits++;
                    f.lowest = Math.Min(f.lowest, s.Health);
                    if (!done && s.ShuttleHovers > hov0)
                    {   // it could not land there after all (it hovers): another pad, ordered from the air; the same landing goes on being watched
                        hov0 = s.ShuttleHovers; f.hovers++; retries++;
                        if (retries > count || !PadSpot(w, s, hx, hy, 240, 640, k + 1 + retries, out int ax, out int ay)) { f.stuck = true; f.why = $"{f.hovers} hovers"; break; }
                        OzV11Kit.PlayerMove(w, s, ax, ay, false);
                    }
                }
                if (f.destroyed || f.stuck) break;
                if (!done) { f.stuck = true; f.why = "no landing within 90 s"; break; }
            }
            ZWorld.ShuttleBigCrashProofNext = false;
            return f;
        }

        static int MaxRun(string seq, Func<char, bool> on) { int best = 0, run = 0; foreach (var c in seq) { run = on(c) ? run + 1 : 0; best = Math.Max(best, run); } return best; }
        /// <summary>The crash landings only ('B' big, 'c' today's crash), in order.</summary>
        static string CrashSeq(Flights f) => new string(f.Seq.Where(c => c != '.').ToArray());
        /// <summary>Every whole block of 3 by `key` (the landing's number, or the ship's crash count) among `of` has exactly one big crash.</summary>
        static bool Blocks(IEnumerable<Td> of, Func<Td, int> key) => of.GroupBy(t => (key(t) - 1) / ZWorld.ShuttleBigCrashEvery).All(g => g.Count() < ZWorld.ShuttleBigCrashEvery || g.Count(t => t.big) == 1);

        /// <summary>Ship number 0 = level 16's own; 1 / 2 = one / two more dropped by the testing panel (the last one flown: another id).</summary>
        static (ZWorld w, ZObject s) Ship(int which, int seed)
        {
            var w = W16(seed); var s = w.PlayableShuttle;
            if (s == null) return (w, null);
            Clear(w, s);
            for (int i = 0; i < which; i++)
            {
                if (!PadSpot(w, s, s.CenterX, s.CenterY, 200 + 96 * i, 700, i * 5, out int x, out int y)) return (w, null);
                var n = w.SpawnShuttle(x, y, Team.Red, s.ShuttleHeadingDeg, out _);
                if (n == null) return (w, null);
                s = n;
            }
            Clear(w, s);
            return (w, s);
        }

        /// <summary>The whole check: the PASS / FAIL lines and the numbers (switches as they were afterwards).</summary>
        public static string Check(string settingsText, string level16)
        {
            settings = settingsText; m16 = level16; sb = new StringBuilder(); pass = fail = 0;
            bool dev0 = ZWorld.ShuttleBigCrashDeviation, all0 = ZWorld.ShuttleBigCrashOfAllLandings;
            sb.AppendLine("1.3 item 7, the shuttle's BIG crash landing (rule DV154, Sim/ZWorld.ShuttleBigCrash.cs), level 16, the ship flown empty from pad to pad");
            try
            {
                ZWorld.ShuttleBigCrashDeviation = true; ZWorld.ShuttleBigCrashOfAllLandings = false;   // The default
                int max = 0, loss = 0, floor = 0;
                var ships = new List<Flights>();
                for (int which = 0; which < 3; which++)
                {
                    var (w, s) = Ship(which, 70 + which);
                    if (s == null) { V(false, $"B0 ship {which}: made and cleared"); continue; }
                    max = s.MaxHealth; loss = ZWorld.ShuttleBigCrashLoss(max); floor = ZWorld.ShuttleBigCrashFloor(max);
                    var f = Fly(w, s, Landings);
                    ships.Add(f);
                    L($"ship {which} (id {f.id}, {f.maxHp} HP): {f.td.Count} landings {f.Seq} (B = big crash, c = today's crash, not big, . = clean); health after each: {string.Join(" ", f.td.Select(t => t.hpAfter))}; " +
                      $"lowest {f.lowest}; other health changes {f.otherHits}; hovers {f.hovers}; destroyed {f.destroyed}{(f.stuck ? "; STUCK " + f.why : "")}");
                }
                sb.AppendLine($"   the numbers: full health {max}, a big crash takes {loss} ({F(100.0 * loss / Math.Max(1, max), "0.00")} %), the floor {floor} ({F(100.0 * floor / Math.Max(1, max), "0.00")} %)");
                // B1 the share and the spread (the owner's reading): today's crash landings, one in 3 of those the big one
                Flights off0 = null;
                {
                    ZWorld.ShuttleBigCrashDeviation = false;                                            // ship 0 with the switch off: today's crash landings (1.2)
                    var (w0, s0) = Ship(0, 70);
                    if (s0 != null) off0 = Fly(w0, s0, Landings);
                    ZWorld.ShuttleBigCrashDeviation = true;
                }
                {
                    int n = ships.Sum(f => f.td.Count), big = ships.Sum(f => f.td.Count(t => t.big)), rough = ships.Sum(f => f.td.Count(t => t.rough));
                    bool blocks = ships.All(f => Blocks(f.td.Where(t => t.rough), t => t.rn));        // the rules' blocks by the ship's own crash count (crashes 1-3, 4-6, ...)
                    bool share = ships.All(f => { int c = f.td.Count(t => t.rough), b = f.td.Count(t => t.big); return Math.Abs(3 * b - c) <= 4; });   // one in 3 (a part block at each end: at most 4 off)
                    int runBig = ships.Count == 0 ? 99 : ships.Max(f => MaxRun(CrashSeq(f), c => c == 'B')), runToday = ships.Count == 0 ? 99 : ships.Max(f => MaxRun(CrashSeq(f), c => c != 'B'));
                    bool bigIsRough = ships.All(f => f.td.All(t => !t.big || t.rough));
                    string todayOff = off0 == null ? "" : new string(off0.td.Select(t => t.rough ? 'c' : '.').ToArray());
                    string todayOn = ships.Count == 0 ? "" : new string(ships[0].td.Select(t => t.rough ? 'c' : '.').ToArray());
                    var onByN = ships.Count == 0 ? new Dictionary<int, bool>() : ships[0].td.ToDictionary(t => t.n, t => t.rough);
                    bool same = off0 != null && off0.td.Count == Landings && off0.td.All(t => onByN.TryGetValue(t.n, out bool r) && r == t.rough);   // landing by landing (the landing's number)
                    L($"ship 0, crash landings with DV154 on {todayOn}; with the switch off (1.2) {todayOff}");
                    V(ships.Count == 3 && ships.All(f => f.td.Count == Landings && !f.stuck) && big > 0 && blocks && share && runBig <= 2 && runToday <= 4 && bigIsRough && same && rough >= n / 8 && rough <= n / 2,
                      $"B1 the share (the owner's reading: 1 in 3 of the crash landings): {rough} crash landings in {n} landings of 3 ships ({F(n > 0 ? (double)rough / n : 0)}, about 0.25), the same landings as today {same}; " +
                      $"{big} big ({F(n > 0 ? (double)big / n : 0)} of the landings, about 0.083; {F(rough > 0 ? (double)big / rough : 0)} of the crashes, one in 3: every block of 3 crash landings has one {blocks}, each ship's share {share}); " +
                      $"at most {runBig} big crashes in a row among the crashes (2), at most {runToday} of today's crashes in a row (4); every big crash a crash landing {bigIsRough}");
                }
                // B2 the loss of a full ship's first big crash
                {
                    var first = ships.SelectMany(f => f.td).Where(t => t.big && t.hpBefore == t.max).ToList();
                    bool exact = first.Count > 0 && first.All(t => t.hpBefore - t.hpAfter == loss && Math.Abs((t.hpBefore - t.hpAfter) - 0.25 * t.max) <= 0.5);
                    int otherCost = ships.SelectMany(f => f.td).Count(t => !t.big && t.hpAfter != t.hpBefore);
                    V(exact && otherCost == 0, $"B2 the loss: a full ship's big crash takes {string.Join(", ", first.Select(t => t.hpBefore - t.hpAfter).Distinct())} of {max} HP (25 % = {F(0.25 * max, "0.0")}, within half a hit point; {first.Count} such crashes); clean landings and crashes take nothing: " +
                      $"{otherCost} with a change (0)");
                }
                // B3 the floor and never destroyed
                {
                    bool sums = ships.All(f => f.td.All(t => t.hpAfter == (t.big ? ZWorld.ShuttleBigCrashHealthAfter(t.hpBefore, t.max) : t.hpBefore)) && f.td.Zip(f.td.Skip(1), (a, b) => b.hpBefore == a.hpAfter).All(x => x));
                    bool never = ships.All(f => !f.destroyed), aboveFloor = ships.All(f => f.lowest >= floor);
                    var (wr, sr) = Ship(2, 72);
                    var run = sr != null ? Fly(wr, sr, 6, k => k == 0 ? sr.MaxHealth : 0, true) : new Flights();   // 6 big crashes in a row (the proof switch) from full health (its drop-in landing may have been a big one), no repairs
                    var want = new List<int>(); for (int h = run.maxHp, i = 0; i < 6; i++) want.Add(h = ZWorld.ShuttleBigCrashHealthAfter(h, run.maxHp));   // 4054: 3040 2026 1012 405 405 405
                    bool runAll = run.td.Count == 6 && run.td.All(t => t.big), runFloor = run.td.Count > 0 && run.td.Last().hpAfter == floor && run.lowest >= floor;
                    bool runExact = run.td.Count == 6 && run.td[0].hpBefore == run.maxHp && run.td.Select(t => t.hpAfter).SequenceEqual(want);
                    int atFloor = run.td.Count(t => t.big && t.hpBefore <= floor);
                    bool belowLoses = atFloor > 0 && run.td.Concat(ships.SelectMany(f => f.td)).All(t => !(t.big && t.hpBefore <= floor) || t.hpAfter == t.hpBefore);
                    L($"6 big crashes in a row (ship id {run.id}): {run.Seq}; health {(run.td.Count > 0 ? run.td[0].hpBefore : -1)}, after each: {string.Join(" ", run.td.Select(t => t.hpAfter))} (the rules: {run.maxHp} {string.Join(" ", want)})");
                    V(sums && never && aboveFloor && runAll && runFloor && runExact && !run.destroyed && belowLoses && ships.Sum(f => f.otherHits) + run.otherHits == 0,
                      $"B3 over {Landings} landings a ship without repairs: the health is the rules' sum {sums}, lowest {(ships.Count > 0 ? ships.Min(f => f.lowest) : -1)}; 6 big crashes in a row: {run.td.Count(t => t.big)} big, " +
                      $"lowest {(run.td.Count > 0 ? run.lowest : -1)} (the floor {floor} or more), ends at the floor {runFloor}, step by step the rules' {runExact}; a big crash at the floor loses nothing {belowLoses} ({atFloor} at the floor); destroyed by its own landing {ships.Count(f => f.destroyed) + (run.destroyed ? 1 : 0)} (0); health changed on other ticks {ships.Sum(f => f.otherHits) + run.otherHits} (0)");
                }
                // B4 the three cases
                {
                    var res = new List<string>(); bool ok = true;
                    foreach (int pct in new[] { 30, 15, 5 })
                    {
                        var (w, s) = Ship(0, 90 + pct);
                        if (s == null) { ok = false; res.Add(pct + " %: no ship"); continue; }
                        int start = s.MaxHealth * pct / 100;
                        var f = Fly(w, s, 1, k => start, true);
                        var t = f.td.Count > 0 ? f.td[0] : new Td { hpBefore = -1, hpAfter = -1 };
                        int fl = ZWorld.ShuttleBigCrashFloor(s.MaxHealth), want = pct == 5 ? start : fl;
                        bool good = f.td.Count == 1 && t.big && t.hpBefore == start && t.hpAfter == want && !f.destroyed;
                        ok &= good;
                        res.Add($"{pct} %: {t.hpBefore} -> {t.hpAfter} HP (want {want}{(pct == 5 ? ", unchanged" : ", the floor")}; big {t.big}, destroyed {f.destroyed})");
                    }
                    V(ok, "B4 a big crash at 30 %, 15 % and 5 % health: " + string.Join("; ", res));
                }
                // B5 the switch off = 1.2 (ship 0's flights that B1 compared with)
                {
                    var f = off0 ?? new Flights();
                    int crashes = f.td.Count(t => t.rough), bigs = f.td.Count(t => t.big), lost = f.td.Count(t => t.hpAfter != t.hpBefore);
                    L($"switch off: {f.Seq}; health after each: {string.Join(" ", f.td.Select(t => t.hpAfter))}");
                    V(f.td.Count == Landings && bigs == 0 && lost == 0 && crashes >= Landings / 8 && crashes <= Landings / 2 && !f.destroyed,
                      $"B5 the switch off is the 1.2 rough landing: {crashes} crash landings in {f.td.Count} (about one in 4: {F(f.td.Count > 0 ? (double)crashes / f.td.Count : 0)}), big {bigs} (0), a landing that cost hit points {lost} (0)");
                }
                // B6 the other reading (the code switch): one landing in 3 of ALL landings
                {
                    ZWorld.ShuttleBigCrashOfAllLandings = true;
                    int n = 0, big = 0, runBig = 0, runClean = 0; bool blocks = true, share = true, roughIsBig = true, bigIsRough = true;
                    for (int which = 0; which < 3; which++)
                    {
                        var (w, s) = Ship(which, 70 + which);
                        if (s == null) { blocks = false; continue; }
                        var f = Fly(w, s, Landings);
                        n += f.td.Count; big += f.td.Count(t => t.big);
                        blocks &= !f.stuck && Blocks(f.td, t => t.n);
                        share &= Math.Abs(3 * f.td.Count(t => t.big) - f.td.Count) <= 4;                     // one in 3 (a part block at each end: at most 4 off)
                        roughIsBig &= f.td.All(t => !t.rough || t.big); bigIsRough &= f.td.All(t => !t.big || t.rough);
                        runBig = Math.Max(runBig, MaxRun(f.Seq, c => c == 'B')); runClean = Math.Max(runClean, MaxRun(f.Seq, c => c != 'B'));
                        L($"other reading, ship {which} (id {f.id}): {f.Seq}");
                    }
                    V(n == 3 * Landings && share && blocks && runBig <= 2 && runClean <= 4 && roughIsBig && bigIsRough,
                      $"B6 the other reading (1 in 3 of all landings, the code switch): {big} big crashes in {n} landings = {F(n > 0 ? (double)big / n : 0)} (one in 3: every block of 3 landings has one {blocks}, each ship's share {share}); at most {runBig} in a row (2), at most {runClean} clean in a row (4); every crash landing the big one {roughIsBig}, every big crash a crash landing {bigIsRough}");
                    ZWorld.ShuttleBigCrashOfAllLandings = false;
                }
                // D determinism
                {
                    var (wa, sa) = Ship(1, 71); var (wb, sb2) = Ship(1, 71);
                    var a = sa != null ? Fly(wa, sa, 12) : new Flights(); var b = sb2 != null ? Fly(wb, sb2, 12) : new Flights();
                    V(a.td.Count == 12 && a.Digest == b.Digest, $"D the same 12 flights twice: {(a.Digest == b.Digest ? "identical" : "DIFFERENT")} ({a.Seq} / {b.Seq})");
                }
            }
            finally { ZWorld.ShuttleBigCrashDeviation = dev0; ZWorld.ShuttleBigCrashOfAllLandings = all0; ZWorld.ShuttleBigCrashProofNext = false; ZWorld.ShuttleRoughProofNext = false; }
            sb.AppendLine($"1.3 item 7 result: {pass} PASS, {fail} FAIL");
            return sb.ToString();
        }
    }
}
