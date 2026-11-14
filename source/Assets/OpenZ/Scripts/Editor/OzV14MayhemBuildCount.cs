using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The counted check, headless on the Sim alone, on the real level 2 map, nothing saved: menu
    /// OpenZ/V1.4/Mayhem Build: Count (headless, DV175), and off-project on the editor's Mono (the kit's tools/harness). One line per fact,
    /// [OpenZ V1.4 mayhem build] PASS n/n; a fact with no sample is a FAIL, never a PASS.
    /// The rule in numbers: an item of the computer's side takes ZWorld.CpuBuildFasterScale (0.9) x the time the same item takes the human's
    /// side (Zod's formula: the same base time, flags and damage), in seconds bit for bit; it is done on the first 100 Hz tick at or after
    /// that time, the game's own rounding for both sides, so in ticks it is 0.9 x the player's ticks to within one tick.
    ///  T  every producible type (every unit and gun of the four producers' level-5 lists as a game has them: the medium tank switch DV145
    ///     on for the sweep, put back after; 19 types, mtank by name): on MAYHEM, both ways round (the human red against
    ///     the computer blue, and PLAY AS BLUE), on every producer that lists it, flags 0 / 1/7 / 2/7 / 1/2 / 1, health 100 / 60 / 25 %: the
    ///     player's time = Zod's bit for bit, the computer's = Zod's x 0.9 bit for bit, its ticks never more than the player's (never slower
    ///     than HARD) and within one tick of 0.9 x the player's ticks
    ///  H  EASY / MEDIUM / HARD (View/ZGame.cs names no side), and a world nobody told (the headless checks, the parity report, the gate
    ///     battles): every type, both sides, every case: Zod's time bit for bit
    ///  M  the side named: exactly the computer's side of a human-against-computer match, only when asked (all 16 cases)
    ///  L  a live change: the computer's running item re-timed at once from its start (x 0.9), back to Zod's bit for bit when it is off again;
    ///     the player's running item untouched both times
    ///  K  a factory changing hands with an item running: taken by the human it builds at the human's time, taken back by the computer at
    ///     0.9 x again
    ///  R  real ticks on level 2 (both forts start a grunt, then a jeep, at the same tick): the ticks counted in the running world = the
    ///     game's rounding of the two times; the computer's grunt and jeep 0.9 x the player's to within one tick; both ways round; with no
    ///     side named the two forts the same; the same run twice the same
    ///  U  (in the editor only) ZGame.ApplyTechBonus on a probe: MAYHEM names the computer's side (red human: blue, blue human: red), EASY /
    ///     MEDIUM / HARD, a computer-only match and a live change away from MAYHEM name none; the hint and tooltip texts
    /// </summary>
    public static class OzV14MayhemBuildCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);

        public static readonly string[] Producers = { "fort_front", "fort_back", "robot_factory", "vehicle_factory" };
        static readonly double[] Zones = { 0, 1.0 / 7, 2.0 / 7, 0.5, 1 };
        static readonly double[] Hps = { 1, 0.6, 0.25 };
        const double S = ZWorld.CpuBuildFasterScale;

        static Team Other(Team t) => t == Team.Red ? Team.Blue : Team.Red;

        /// <summary>Zod's build time (zbuilding.cpp:633-667), written out here on its own: base x (1 - 0.5 z) x (1 + 1.25 (1 - hp/max)).</summary>
        public static double Zod(int baseSeconds, double zone, int health, int maxHealth)
        {
            double z = Math.Max(0, Math.Min(1, zone));
            double t = baseSeconds - baseSeconds * 0.5 * z;
            double hp = maxHealth > 0 ? (double)health / maxHealth : 1;
            t += t * 1.25 * (1.0 - hp);
            return t;
        }

        /// <summary>The game's rounding of a build to ticks: from `start` (a tick's time) the world's clock steps ZConst.TICK each tick
        /// (ZWorld.Tick: Time += dt) and ProcessBuilding finishes the item on the first tick whose time is at or after BFinal.</summary>
        public static long TicksTo(double start, double bfinal)
        {
            double tm = start; long n = 0;
            while (!(tm >= bfinal) && n < 100000000) { tm += ZConst.TICK; n++; }
            return n;
        }

        /// <summary>Level 2 with nobody on it: every unit off the map at the first tick, no game end, every producer stopped.</summary>
        static ZWorld New(string settings, string map, int seed = 1)
        {
            var w = ZWorld.Create(settings, map, seed);
            w.NoGameEnd = true;
            foreach (var o in w.Objects) if (o.IsUnit && o.Owner != Team.Neutral) o.KillAt = 0;
            foreach (var o in w.Objects) if (o.IsProducer) o.StopBuildingProduction();
            for (int i = 0; i < 37; i++) w.Tick(ZConst.TICK);                                       // a start time that is not 0 (the clock's real steps)
            w.Events.Clear();
            return w;
        }

        /// <summary>Every (producer, unit) pair of the four producers' level-5 lists, as the game reads them now.</summary>
        public static List<(string producer, string unit)> Pairs()
        {
            var l = new List<(string, string)>();
            foreach (var p in Producers) foreach (var u in ZBuildLists.For(p, 5)) l.Add((p, u));
            return l;
        }

        sealed class Tally
        {
            public int n, playerZod, cpuScaled, notSlower, withinOne, exactRound, hardSame; public long maxOff; public string example;
            public bool All => n > 0 && playerZod == n && cpuScaled == n && notSlower == n && withinOne == n;
        }

        /// <summary>One item on one building under both owners: (BFinal as the player's, as the computer's), the building put back after.</summary>
        static (double p, double c, double binit) Both(ZWorld w, ZObject b, string unit, double zone, int health, Team human)
        {
            Team owner = b.Owner; int hp0 = b.Health; double z0 = b.ZoneOwnage; string cu = b.CurUnit; bool bu = b.Building; double bi = b.BInit, bf = b.BFinal;
            try
            {
                b.ZoneOwnage = zone; b.Health = health; b.CurUnit = unit; b.Building = true; b.BInit = w.Time;
                b.Owner = human; b.RecalcBuildTime(); double p = b.BFinal;
                b.Owner = Other(human); b.RecalcBuildTime(); double c = b.BFinal;
                return (p, c, b.BInit);
            }
            finally { b.Owner = owner; b.Health = hp0; b.ZoneOwnage = z0; b.CurUnit = cu; b.Building = bu; b.BInit = bi; b.BFinal = bf; }
        }

        // ------------------------------------------------------------------------------------------------ T and H
        static void TH(string settings, string map, string[] only)
        {
            bool mt = ZWorld.MTankDeviation; ZWorld.MTankDeviation = true;                                  // DV145 as in a game (View/ZGame.cs: on unless 'Zod exact'): the lists offer the medium tank after the mech
            try { Sweep(settings, map, only); }
            finally { ZWorld.MTankDeviation = mt; }                                                        // put back as it was (headless default off)
        }

        static void Sweep(string settings, string map, string[] only)
        {
            var pairs = Pairs().Where(x => only == null || only.Contains(x.unit)).ToList();
            var types = pairs.Select(x => x.unit).Distinct().ToList();
            var all = new Dictionary<string, Tally>();
            var h = new Tally(); var never = new Tally(); int missing = 0;
            foreach (var human in new[] { Team.Red, Team.Blue })
            {
                var w = New(settings, map);
                var bs = Producers.ToDictionary(p => p, p => w.Objects.FirstOrDefault(o => o.Type == p));
                missing += bs.Values.Count(b => b == null);
                foreach (var (prod, unit) in pairs)
                {
                    var b = bs[prod]; if (b == null) continue;
                    int bt = w.S.Unit(unit)?.build_time ?? -1; if (bt < 0) continue;
                    if (!all.TryGetValue(unit, out var t)) all[unit] = t = new Tally();
                    foreach (double z in Zones)
                        foreach (double r in Hps)
                        {
                            int hp = Math.Max(1, (int)Math.Round(b.MaxHealth * r));
                            double zod = Zod(bt, z, hp, b.MaxHealth);
                            // never told (the headless default): Zod for both owners
                            var n0 = Both(w, b, unit, z, hp, human);
                            never.n++; if (n0.p == n0.binit + zod && n0.c == n0.binit + zod) never.playerZod++;
                            // EASY / MEDIUM / HARD: ZGame names no side (on false)
                            w.SetCpuBuildFaster(false, human, human == Team.Red, human == Team.Blue);
                            var e = Both(w, b, unit, z, hp, human);
                            h.n++; if (e.p == e.binit + zod && e.c == e.binit + zod) h.playerZod++;
                            long hardTicks = TicksTo(e.binit, e.c);
                            // MAYHEM: the computer's side named
                            w.SetCpuBuildFaster(true, human, human == Team.Red, human == Team.Blue);
                            var m = Both(w, b, unit, z, hp, human);
                            w.SetCpuBuildFaster(false, human, human == Team.Red, human == Team.Blue);
                            long pt = TicksTo(m.binit, m.p), ct = TicksTo(m.binit, m.c);
                            long round = (long)Math.Round(pt * S, MidpointRounding.AwayFromZero), off = Math.Abs(ct - round);
                            t.n++;
                            if (m.p == m.binit + zod) t.playerZod++;
                            if (m.c == m.binit + zod * S) t.cpuScaled++;
                            if (ct <= hardTicks && ct <= pt) t.notSlower++;
                            if (off <= 1) t.withinOne++;
                            if (off == 0) t.exactRound++;
                            t.maxOff = Math.Max(t.maxOff, off);
                            if (z == 0 && r == 1 && t.example == null)
                                t.example = $"{bt} s base: the player {m.p - m.binit:0.###} s = {pt} ticks, the computer {m.c - m.binit:0.###} s = {ct} ticks (0.9 x {pt} = {pt * S:0.#})";
                        }
                }
                Check(!w.CpuBuildFaster(Team.Red) && !w.CpuBuildFaster(Team.Blue), $"T0 {human} human: no side left faster after the sweep");
            }
            Check(missing == 0, $"T1 level 2 has every producer ({string.Join(", ", Producers)}), so every list is measured on its own building ({missing} missing)");
            foreach (var u in types)
            {
                all.TryGetValue(u, out var t); t ??= new Tally();
                string on = string.Join("/", pairs.Where(x => x.unit == u).Select(x => x.producer));
                Check(t.All, $"T {u} ({on}): {t.n} samples (2 sides x flags x health): the player's time Zod's {t.playerZod}/{t.n}, the computer's Zod's x 0.9 {t.cpuScaled}/{t.n}, "
                             + $"not slower than HARD {t.notSlower}/{t.n}, within one tick of 0.9 x the player's ticks {t.withinOne}/{t.n} (exactly {t.exactRound}, at most {t.maxOff} off); {t.example}");
            }
            int distinct = all.Count(kv => kv.Value.n > 0);
            bool mtank = all.TryGetValue(ZMTank.Type, out var mtk) && mtk.n > 0;
            Check(only != null ? distinct == only.Length : distinct == types.Count && distinct >= 19 && mtank,
                  $"T2 every producible type of the game's lists measured: {distinct} of {types.Count} ({string.Join(" ", types)}), the medium tank ({ZMTank.Type}) {mtank}; {all.Values.Sum(x => x.n)} samples");
            Check(h.n > 0 && h.playerZod == h.n, $"H1 EASY / MEDIUM / HARD (no side named): every type, both sides, every case Zod's time bit for bit: {h.playerZod}/{h.n}");
            Check(never.n > 0 && never.playerZod == never.n, $"H2 a world nobody told (the headless checks, the parity report, the gate battles): Zod's time bit for bit {never.playerZod}/{never.n}");
        }

        // ------------------------------------------------------------------------------------------------ M
        static void M(string settings, string map)
        {
            var w = New(settings, map); int n = 0, ok = 0; var bad = new List<string>();
            foreach (bool on in new[] { false, true })
                foreach (var human in new[] { Team.Red, Team.Blue })
                    foreach (bool blueCpu in new[] { false, true })
                        foreach (bool redCpu in new[] { false, true })
                        {
                            w.SetCpuBuildFaster(on, human, blueCpu, redCpu);
                            bool vs = on && blueCpu != redCpu;
                            bool wantR = vs && redCpu && human == Team.Blue, wantB = vs && blueCpu && human == Team.Red;
                            n++;
                            if (w.CpuBuildFaster(Team.Red) == wantR && w.CpuBuildFaster(Team.Blue) == wantB && !w.CpuBuildFaster(Team.Neutral)) ok++;
                            else bad.Add($"on {on} human {human} blueCpu {blueCpu} redCpu {redCpu}");
                        }
            w.SetCpuBuildFaster(true, Team.Red, true, false); bool redHuman = w.CpuBuildFaster(Team.Blue) && !w.CpuBuildFaster(Team.Red);
            w.SetCpuBuildFaster(true, Team.Blue, false, true); bool blueHuman = w.CpuBuildFaster(Team.Red) && !w.CpuBuildFaster(Team.Blue);
            w.SetCpuBuildFaster(true, Team.Red, true, true); bool bots = !w.CpuBuildFaster(Team.Red) && !w.CpuBuildFaster(Team.Blue);
            Check(n == 16 && ok == n && redHuman && blueHuman && bots,
                  $"M1 the side named: {ok}/{n} cases right (only asked, only a human against the computer, only the computer's side): red human -> blue {redHuman}, PLAY AS BLUE -> red {blueHuman}, a computer-only match none {bots}" + (bad.Count > 0 ? "; WRONG: " + string.Join("; ", bad) : ""));
        }

        // ------------------------------------------------------------------------------------------------ L
        static void L(string settings, string map)
        {
            var w = New(settings, map);
            var red = w.Objects.First(o => o.IsFort && o.Owner == Team.Red); var blue = w.Objects.First(o => o.IsFort && o.Owner == Team.Blue);
            w.StartBuilding(Team.Red, red, "grunt"); w.StartBuilding(Team.Blue, blue, "grunt");
            for (int i = 0; i < 500; i++) w.Tick(ZConst.TICK);
            double rb = red.BFinal, bb = blue.BFinal, bi = blue.BInit; int bt = w.S.Unit("grunt").build_time; int rc0 = w.CpuBuildFasterRecalcs;
            double zod = Zod(bt, blue.ZoneOwnage, blue.Health, blue.MaxHealth);
            w.SetCpuBuildFaster(true, Team.Red, true, false);
            bool on = blue.BFinal == bi + zod * S && blue.BInit == bi && red.BFinal == rb && w.CpuBuildFasterRecalcs == rc0 + 1;
            double onF = blue.BFinal;
            for (int i = 0; i < 300; i++) w.Tick(ZConst.TICK);
            bool kept = blue.BFinal == onF && red.BFinal == rb;
            w.SetCpuBuildFaster(false, Team.Red, true, false);
            bool off = blue.BFinal == bb && red.BFinal == rb && w.CpuBuildFasterRecalcs == rc0 + 2;
            Check(on && kept && off && bb == bi + zod,
                  $"L1 MAYHEM turned on 5 s into a computer grunt: re-timed from its start {bb - bi:0.###} s -> {onF - bi:0.###} s (x 0.9) {on}, kept while it runs {kept}; off again: back to {blue.BFinal - bi:0.###} s bit for bit {off}; the player's grunt {rb - red.BInit:0.###} s untouched");
        }

        // ------------------------------------------------------------------------------------------------ K
        static void K(string settings, string map)
        {
            var w = New(settings, map);
            w.SetCpuBuildFaster(true, Team.Red, true, false);                                           // MAYHEM, red the human
            var flag = w.Flags.FirstOrDefault(f => f.LinkedObjects.Any(o => o.Type == "vehicle_factory"));
            if (flag == null) { Check(false, "K1 no flag zone with a vehicle factory on level 2"); return; }
            w.AwardZone(flag, Team.Blue);
            var b = flag.LinkedObjects.First(o => o.Type == "vehicle_factory");
            w.StopBuilding(Team.Blue, b); w.StartBuilding(Team.Blue, b, "jeep");
            for (int i = 0; i < 1000; i++) w.Tick(ZConst.TICK);
            int bt = w.S.Unit("jeep").build_time; double bi = b.BInit; int rc0 = w.CpuBuildFasterRecalcs;
            bool cpu0 = b.Owner == Team.Blue && b.CurUnit == "jeep" && b.BFinal == bi + Zod(bt, b.ZoneOwnage, b.Health, b.MaxHealth) * S;
            w.AwardZone(flag, Team.Red);
            bool human = b.Owner == Team.Red && b.CurUnit == "jeep" && b.BInit == bi && b.BFinal == bi + Zod(bt, b.ZoneOwnage, b.Health, b.MaxHealth);
            double hz = b.ZoneOwnage; int rc1 = w.CpuBuildFasterRecalcs;
            for (int i = 0; i < 500; i++) w.Tick(ZConst.TICK);
            w.AwardZone(flag, Team.Blue);
            bool cpu1 = b.Owner == Team.Blue && b.CurUnit == "jeep" && b.BInit == bi && b.BFinal == bi + Zod(bt, b.ZoneOwnage, b.Health, b.MaxHealth) * S;
            int rc2 = w.CpuBuildFasterRecalcs;
            Check(cpu0 && human && cpu1 && rc1 == rc0 + 1 && rc2 == rc1 + 1,
                  $"K1 a computer vehicle factory building a jeep at 0.9 x {cpu0}; taken by the human 10 s in: the human's time from the jeep's start (flags {hz:0.###}) {human}; taken back: 0.9 x again {cpu1}; re-timed on each change of hands ({rc0} -> {rc1} -> {rc2})");
        }

        // ------------------------------------------------------------------------------------------------ R
        sealed class Real { public long pGrunt = -1, cGrunt = -1, pJeep = -1, cJeep = -1, wpGrunt, wcGrunt, wpJeep, wcJeep; public string why = ""; public ulong hash = 1469598103934665603UL; }

        static Real Race(string settings, string map, Team human, bool mayhem)
        {
            var w = New(settings, map); var r = new Real();
            w.SetCpuBuildFaster(mayhem, human, human == Team.Red, human == Team.Blue);
            var hf = w.Objects.First(o => o.IsFort && o.Owner == human); var cf = w.Objects.First(o => o.IsFort && o.Owner == Other(human));
            foreach (var unit in new[] { "grunt", "jeep" })
            {
                w.StopBuilding(human, hf); w.StopBuilding(Other(human), cf);
                hf.RepeatProduction = cf.RepeatProduction = false;
                w.StartBuilding(human, hf, unit); w.StartBuilding(Other(human), cf, unit);
                double start = w.Time;
                long wp = TicksTo(start, hf.BFinal), wc = TicksTo(start, cf.BFinal), p = -1, c = -1;
                for (long i = 1; i <= 60000 && (p < 0 || c < 0); i++)
                {
                    w.Tick(ZConst.TICK);
                    foreach (var e in w.Events)
                    {
                        r.hash = (r.hash ^ (ulong)(long)e.type) * 1099511628211UL; r.hash = (r.hash ^ (ulong)(long)e.objId) * 1099511628211UL;
                        if (e.type == ZEventType.UnitBuilt && e.otherId == hf.Id && p < 0) p = i;
                        if (e.type == ZEventType.UnitBuilt && e.otherId == cf.Id && c < 0) c = i;
                    }
                    w.Events.Clear();
                }
                foreach (var o in w.Objects) if (o.IsUnit && o.Owner != Team.Neutral) o.KillAt = 0;   // the new units off the map
                w.Tick(ZConst.TICK); w.Events.Clear();
                if (unit == "grunt") { r.pGrunt = p; r.cGrunt = c; r.wpGrunt = wp; r.wcGrunt = wc; } else { r.pJeep = p; r.cJeep = c; r.wpJeep = wp; r.wcJeep = wc; }
            }
            return r;
        }

        static bool Within(long c, long p) => p > 0 && c > 0 && Math.Abs(c - (long)Math.Round(p * S, MidpointRounding.AwayFromZero)) <= 1;

        static void R(string settings, string map)
        {
            foreach (var human in new[] { Team.Red, Team.Blue })
            {
                var m = Race(settings, map, human, true);
                bool same = m.pGrunt == m.wpGrunt && m.cGrunt == m.wcGrunt && m.pJeep == m.wpJeep && m.cJeep == m.wcJeep;
                Check(same && Within(m.cGrunt, m.pGrunt) && Within(m.cJeep, m.pJeep) && m.cGrunt < m.pGrunt && m.cJeep < m.pJeep,
                      $"R1 MAYHEM, the human {human}: in the running world the player's grunt {m.pGrunt} ticks, the computer's {m.cGrunt} (0.9 x = {m.pGrunt * S:0.#}); "
                      + $"jeep {m.pJeep} / {m.cJeep} (0.9 x = {m.pJeep * S:0.#}); = the game's rounding of the two times {same}");
                var e = Race(settings, map, human, false);
                bool esame = e.pGrunt == e.wpGrunt && e.cGrunt == e.wcGrunt && e.pJeep == e.wpJeep && e.cJeep == e.wcJeep;
                Check(esame && e.pGrunt > 0 && e.pJeep > 0 && e.pGrunt == e.cGrunt && e.pJeep == e.cJeep && e.pGrunt == m.pGrunt && e.pJeep == m.pJeep,
                      $"R2 EASY / MEDIUM / HARD, the human {human}: both forts the same, grunt {e.pGrunt} / {e.cGrunt} ticks, jeep {e.pJeep} / {e.cJeep}; the player's own ticks as on MAYHEM ({m.pGrunt}, {m.pJeep})");
                if (human == Team.Red)
                {
                    var again = Race(settings, map, human, true);
                    Check(again.hash == m.hash && again.cGrunt == m.cGrunt && again.cJeep == m.cJeep, $"R3 the same MAYHEM run twice: the same ticks and events ({m.hash:X16})");
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ U (the editor only)
#if UNITY_EDITOR && !OPENZ_HARNESS
        static void U(string settings, string map)
        {
            var go = new UnityEngine.GameObject("OZ_MayhemBuildCount_Probe") { hideFlags = UnityEngine.HideFlags.HideAndDontSave }; go.SetActive(false);
            var set = typeof(ZGame).GetProperty("World").GetSetMethod(true);
            try
            {
                var g = go.AddComponent<ZGame>();
                string Case(GameDifficulty d, bool mayhem, Team player, bool blueBot, bool redBot)
                {
                    var w = New(settings, map); set.Invoke(g, new object[] { w });
                    g.gameDifficulty = d; g.mayhem = mayhem; g.player = player; g.blueBot = blueBot; g.redBot = redBot;
                    g.ApplyTechBonus();
                    return (w.CpuBuildFaster(Team.Red) ? "R" : "") + (w.CpuBuildFaster(Team.Blue) ? "B" : "");
                }
                var rows = new List<string>(); bool ok = true;
                void Want(string what, string got, string want) { rows.Add($"{what}: {(got == "" ? "none" : got)}"); ok &= got == want; }
                foreach (var d in new[] { GameDifficulty.Easy, GameDifficulty.Medium, GameDifficulty.Hard })
                {
                    Want(OpenZDifficulty.Label(d) + " red human", Case(d, false, Team.Red, true, false), "");
                    Want(OpenZDifficulty.Label(d) + " blue human", Case(d, false, Team.Blue, false, true), "");
                }
                Want("MAYHEM red human", Case(GameDifficulty.Hard, true, Team.Red, true, false), "B");
                Want("MAYHEM PLAY AS BLUE", Case(GameDifficulty.Hard, true, Team.Blue, false, true), "R");
                Want("MAYHEM computer-only match", Case(GameDifficulty.Hard, true, Team.Red, true, true), "");
                Want("MAYHEM changed live to EASY", Case(GameDifficulty.Easy, true, Team.Red, true, false), "");
                Check(ok && rows.Count == 10, "U1 ZGame.ApplyTechBonus names the faster side: " + string.Join("; ", rows));
            }
            catch (Exception ex) { Check(false, "U1 the ZGame probe threw " + ex.GetType().Name + ": " + ex.Message); }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            string hx = OpenZDifficulty.Hint(GameDifficulty.Mayhem), tip = OzSettingTips.Difficulty;
            Check(hx == "HARD + FOG OF WAR · FORT MISSILE CANNONS · ENEMY BUILDS 10 % FASTER" && tip.Contains("ground you have not explored. On MAYHEM the enemy also builds 10 % faster.|")
                  && OpenZDifficulty.Hint(GameDifficulty.Hard) == "Z'S BUILD LISTS · FORT MISSILE CANNONS" && OpenZDifficulty.Hint(GameDifficulty.Easy) == "EVERY UNIT UNLOCKED · GENTLE CPU"
                  && OpenZDifficulty.Hint(GameDifficulty.Medium) == "FACTORIES 2 TECH LEVELS UP · ZOD CPU" && OzDeployBar.CpuMayhem == "HARD + FOG OF WAR",
                  $"U2 the words: the OPTIONS hint \"{hx}\"; the DIFFICULTY tooltip ends \"...{tip.Substring(Math.Max(0, tip.Length - 60))}\"; EASY / MEDIUM / HARD hints and the battlefield bar's \"{OzDeployBar.CpuMayhem}\" unchanged");
        }
#endif

        // ------------------------------------------------------------------------------------------------ runs
        public static string Run(string settings, Func<string, string> mapText)
        {
            pass = fail = 0; sb = new StringBuilder();
            sb.AppendLine("[OpenZ V1.4 mayhem build] DV175: on MAYHEM the computer builds every item in 90 % of the player's time (Sim/ZWorld.CpuBuild.cs)");
            try
            {
                string map = mapText("L02");
                TH(settings, map, null);
                M(settings, map);
                L(settings, map);
                K(settings, map);
                R(settings, map);
#if UNITY_EDITOR && !OPENZ_HARNESS
                U(settings, map);
#else
                Info("U (ZGame.ApplyTechBonus on a probe, the hint and tooltip texts) runs in the editor only (menu OpenZ/V1.4/Mayhem Build: Count (headless, DV175))");
#endif
            }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace); }
            if (pass + fail == 0) Check(false, "no fact was counted");
            sb.AppendLine($"[OpenZ V1.4 mayhem build] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

        /// <summary>The Deviations row's probe (Editor/OpenZParity.V14MayhemBuild.cs): T for a grunt and a heavy tank, H, M, L.</summary>
        public static string Quick(string settings, string map)
        {
            pass = fail = 0; sb = new StringBuilder();
            try
            {
                TH(settings, map, new[] { "grunt", "heavy" });
                M(settings, map);
                L(settings, map);
            }
            catch (Exception ex) { Check(false, "the probe threw " + ex.GetType().Name + ": " + ex.Message); }
            if (pass + fail == 0) Check(false, "no fact was counted");
            sb.AppendLine($"[OpenZ V1.4 mayhem build quick] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

#if UNITY_EDITOR && !OPENZ_HARNESS
        [UnityEditor.MenuItem("OpenZ/V1.4/Mayhem Build: Count (headless, DV175)")]
        static void Menu() => OzV11Kit.RunMenu("mayhem build: count (DV175)", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText));
#endif
    }
}
