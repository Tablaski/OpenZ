using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Counted check of DV147 (the mech's health x 0.95, Sim/ZMechHp.cs) and DV148 (the computer's mech / medium tank mix,
    /// Sim/ZBot.MechMix.cs), 1.2 owner items 179 / 180. Headless: the simulation and the Unit editor's pure stats code only (no scene, no
    /// Play mode, nothing saved; public members only, so the off-engine harness compiles it as it is). Every line is a counted PASS / FAIL:
    ///  1 the rules: with DV147 off the mech is Zod's 6756 HP; on, every parse of the game's settings gives it 6418 HP (6756 x 0.95); the
    ///    medium tank keeps 7432 HP both ways (its own number, also when a text has no medium tank row); every other row unchanged (the APC
    ///    and the missile launcher share Zod's 0.675676); the Zod hold and the ZodEngine parse read Zod's mech while the switch is on;
    ///  2 units: placed and built (level 2's producer) mechs carry 6418 HP with the switch on, 6756 off; the medium tank 7432;
    ///  3 the Unit editor: its OpenZ set's mech card 6418 HP (default stats), the ZodEngine set 6756, a Custom plan with no value of its
    ///    own (a value reset to default) 6418 in its rows, a Custom value of the player's (7000) stays 7000;
    ///  4 the computer: CPU vs CPU on each level (the game's switches, Normal), DV145's old rule and DV148: mechs and medium tanks per team,
    ///    the medium tank share (35-65 % with DV148), the longest run of one type in a team's build order (at most 6 with DV148), every
    ///    team that built 4 or more of them built both; the same seed twice gives the
    ///    same build order (deterministic); with the medium tank off ('Zod exact') no medium tank at all.
    /// Level 2 (Desert) is left out of part 4: the computer's producers there are tech level 0-1 and the mech needs level 2, so it builds
    /// neither (the player's producers start two levels higher on Medium; the computer's do not).
    /// </summary>
    public static class OzV12MechHpCount
    {
        public static readonly string[] CpuLevels = { "L16", "L17" };
        public static readonly int[] CpuSeeds = { 1, 2, 3, 4, 5, 6 };
        /// <summary>The report's line before part 4 (the parity rows split the report there: DV147 above, DV148 below).</summary>
        public const string CpuHeading = "## the computer (DV148)";

        /// <summary>The switches View/ZGame.cs turns on in a normal game (no 'Zod exact', the default options, CPU vs CPU); each is set only
        /// when the simulation has it. The ones a CPU-vs-CPU game leaves off (Easy crews, Hard door guns, Custom stats, the armour chip
        /// option) are not in the list.</summary>
        public static readonly string[] PlaySwitches =
        {
            "ApcCrewDeviation", "ApcSnipeDeviation", "ApproachDeviation", "BoardNearestDeviation", "BotCommitDeviation", "BotFinishDeviation", "BotHeavyDeviation",
            "BotLaserDeviation", "BuildingReachDeviation", "CalmFollowDeviation", "CalmMoveDeviation", "CaptureRunDeviation", "CarParkBotDeviation", "ClearanceDeviation",
            "CloseGrabDeviation", "CpuDefendDeviation", "CpuRepairDeviation", "CpuShuttleDeviation", "CraneAnyRuinDeviation", "CraneFixDeviation", "CraneGunDeviation",
            "CraneSnipeDeviation", "DetailPropsDeviation", "DriverFireDeviation", "DuelDeviation", "EjectLockDeviation", "ElectroSteadyDeviation", "ElectrobotBotDeviation",
            "ElectrobotDeviation", "ElectrobotEscortDeviation", "EnterFallbackDeviation", "ExitLanesDeviation", "ExposedDriverDeviation", "FireOnMoveDeviation",
            "FormationDeviation", "FortDefenderDeviation", "FortGunDeviation", "FortMortarDeviation", "FortRunDeviation", "FortRushDeviation", "GrabCertainDeviation",
            "GrabPolicyDeviation", "GrenadeBoostDeviation", "GrenadeRuleDeviation", "GroundFireDeviation", "HarbourWrecksDeviation", "HeavyFallDeviation", "HeavyPairDeviation",
            "HelpDeviation", "HeroBoostDeviation", "HeroSquadDeviation", "JeepChargeDeviation", "KnockHoldDeviation", "L16PyramidsDeviation", "L16ShuttleDeviation",
            "L17ShuttlesDeviation", "LosDeviation", "MTankDeviation", "MechHandGunDeviation", "MechHpDeviation", "MechMixDeviation", "NoAutoFlagUnderFireDeviation",
            "NoTankDuelVsJeep", "OvertakeDeviation", "ParkedPathDeviation", "PathV3Deviation", "ProneTargetDeviation", "PyroForceBurnDeviation", "RepairBayFireDeviation",
            "RepairInPlaceDeviation", "RiflePairDeviation", "RockRoundDeviation", "RocketSpeedDeviation", "RunOverDeviation", "ShiftAppendDeviation", "ShuttlePlayableDeviation",
            "SideShuttlesDeviation", "SnipeDeviation", "SnipeSafeSpotDeviation", "SniperBalanceDeviation", "SniperHeadshotDeviation", "SniperSteadyDeviation",
            "StrikeGroupDeviation", "ToughPrepareDeviation", "ToughSteadyDeviation", "UnmannedStopDeviation", "VeteranDeviation",
        };

        public static void SetPlaySwitches()
        {
            foreach (var n in PlaySwitches) if (!OzV11Kit.TrySet(typeof(ZWorld), n, true)) OzV11Kit.TrySet(typeof(ZPathing), n, true);
            OzV11Kit.TrySet(typeof(ZPathing), "VehicleFirst", true);
            ZWorld.SetCrocodileRules(true, true, true, true, true);
        }

        const int ZodMechHp = 6756, MechHp = 6418, MTankHp = 7432;
        /// <summary>DV148's longest run of one type in a team's build order: at most 6 (24 seeds per level off-engine gave at most 5 with DV148,
        /// DV145's old rule up to 11; builds lost when a factory falls make the built order uneven, so 4 was too tight for other seeds).</summary>
        const int MaxRun = 6;

        /// <summary>statSets = the text of Resources/OpenZ/UnitEditor/unit_stat_sets.json (null: part 3 reports it missing); oldRule = also play
        /// DV145's old rule on the same seeds for comparison (INFO lines, no verdict).</summary>
        public static string Run(string settings, Func<string, string> map, string statSets, int matchSeconds = 1500, int[] seeds = null, string[] levels = null, bool oldRule = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DV147 the mech's health x 0.95 + DV148 the computer's mech / medium tank mix, counted (Sim/ZMechHp.cs, Sim/ZBot.MechMix.cs)");
            bool hp = ZWorld.MechHpDeviation, mix = ZWorld.MechMixDeviation, mt = ZWorld.MTankDeviation;
            try
            {
                Rules(sb, settings);
                Units(sb, settings, map);
                UnitEditor(sb, settings, statSets);
                sb.AppendLine(CpuHeading);
                Cpu(sb, settings, map, matchSeconds, seeds ?? CpuSeeds, levels ?? CpuLevels, oldRule);
            }
            catch (Exception e) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + e.GetType().Name + ": " + e.Message)); }
            finally { ZWorld.MechHpDeviation = hp; ZWorld.MechMixDeviation = mix; ZWorld.MTankDeviation = mt; }
            return sb.ToString();
        }

        static string N(double v) => v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ 1. the rules
        static void Rules(StringBuilder sb, string settings)
        {
            ZWorld.MechHpDeviation = false;
            var off = ZSettings.Parse(settings);
            ZWorld.MechHpDeviation = true;
            int applied0 = ZMechHp.Applied;
            var on = ZSettings.Parse(settings);
            var m0 = off.Unit(ZMechHp.Type); var m1 = on.Unit(ZMechHp.Type);
            sb.AppendLine(OzV11Kit.Verdict(m0 != null && m0.MaxHealth == ZodMechHp && Math.Abs(m0.health - 0.675676) < 1e-9,
                $"DV147 off ('Zod exact', the parity's Zod rows): the mech {m0?.MaxHealth} HP (health {N(m0?.health ?? 0)}) = Zod's {ZodMechHp}"));
            sb.AppendLine(OzV11Kit.Verdict(m1 != null && m1.MaxHealth == MechHp && Math.Abs(m1.health - ZMechHp.Health(m0.health)) < 1e-12 && ZMechHp.Applied == applied0 + 1,
                $"DV147 on (the game): the mech {m1?.MaxHealth} HP (health {N(m1?.health ?? 0)} = {N(m0?.health ?? 0)} x {N(ZMechHp.Factor)} rounded to six places; {ZodMechHp} x 0.95 = {ZodMechHp * 0.95:0.0}), " +
                $"{100.0 * (1 - (double)(m1?.MaxHealth ?? 0) / ZodMechHp):0.00} % less"));
            var t0 = off.Unit(ZMTank.Type); var t1 = on.Unit(ZMTank.Type);
            sb.AppendLine(OzV11Kit.Verdict(t0 != null && t1 != null && t0.MaxHealth == MTankHp && t1.MaxHealth == MTankHp && t0.health == t1.health,
                $"the medium tank unchanged: {t0?.MaxHealth} HP off / {t1?.MaxHealth} HP on (its own row, {N(t1?.health ?? 0)}); now {100.0 * ((double)(t1?.MaxHealth ?? 0) / Math.Max(1, m1?.MaxHealth ?? 1) - 1):0.0} % more than the mech"));
            var bad = new List<string>(); int fields = 0;
            foreach (var kv in off.Units)
            {
                var a = kv.Value; var b = on.Unit(kv.Key);
                if (b == null) { bad.Add(kv.Key + " missing"); continue; }
                foreach (var f in typeof(ZUnitSettings).GetFields())
                {
                    if (f.FieldType != typeof(double) && f.FieldType != typeof(int)) continue;
                    if (kv.Key == ZMechHp.Type && f.Name == "health") continue;
                    fields++;
                    if (!Equals(f.GetValue(a), f.GetValue(b))) bad.Add(kv.Key + "." + f.Name);
                }
            }
            sb.AppendLine(OzV11Kit.Verdict(bad.Count == 0 && fields > 100, $"every other value of every row is the same on and off ({fields} values; the APC {on.Unit("apc")?.MaxHealth} HP and the missile launcher {on.Unit("missile_launcher")?.MaxHealth} HP keep Zod's 0.675676)" +
                (bad.Count > 0 ? ": differs " + string.Join(", ", bad.Take(8)) : "")));
            var bare = string.Join("\n", settings.Replace("\r\n", "\n").Split('\n').Where(l => !l.StartsWith("unit." + ZMTank.Type + ".")));
            ZWorld.MechHpDeviation = false; var b0 = ZSettings.Parse(bare).Unit(ZMTank.Type);
            ZWorld.MechHpDeviation = true; var b1 = ZSettings.Parse(bare).Unit(ZMTank.Type);
            sb.AppendLine(OzV11Kit.Verdict(b0?.MaxHealth == MTankHp && b1?.MaxHealth == MTankHp, $"a settings text without the medium tank's row: its derived row {b0?.MaxHealth} HP off / {b1?.MaxHealth} HP on (an absolute number, not the mech's x 1.10)"));
            int z1; using (ZWorld.MechHpOff()) z1 = ZSettings.Parse(settings).Unit(ZMechHp.Type).MaxHealth;
            bool back = ZWorld.MechHpDeviation;
            int z2 = ZMechHp.ParseZod(settings).Unit(ZMechHp.Type).MaxHealth;
            sb.AppendLine(OzV11Kit.Verdict(z1 == ZodMechHp && z2 == ZodMechHp && back && ZWorld.MechHpDeviation,
                $"with the switch on, the Zod hold (ZWorld.MechHpOff) and the ZodEngine parse (ZMechHp.ParseZod) read Zod's mech: {z1} / {z2} HP; the switch is back on after each"));
        }

        // ------------------------------------------------------------------ 2. units
        static (int max, int now) Placed(string settings, string type, bool rule)
        {
            ZWorld.MechHpDeviation = rule;
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(30, 30), 1);
            OzV11Kit.Dummy(w, 400, 400);
            var o = OzV11Kit.Place(w, type, 120, 120, Team.Red);
            return (o.MaxHealth, o.Health);
        }

        static int BuildOne(string settings, string map, bool rule, out string where)
        {
            ZWorld.MechHpDeviation = rule;
            var w = ZWorld.Create(settings, map, 3);
            var f = w.Objects.Where(o => o.Owner == Team.Red && o.IsProducer && !o.Destroyed).OrderBy(o => o.Type == "vehicle_factory" ? 0 : 1).ThenBy(o => o.Id).First();
            if (f.Level < 2) f.Level = 2;                                                                   // the mech's tech level
            where = $"the red {f.Type}, level {f.BuildLevel}";
            foreach (var b in w.Objects.Where(o => o.IsProducer)) b.StopBuildingProduction();
            w.Events.Clear();
            if (!w.StartBuilding(Team.Red, f, ZMechHp.Type)) { where += " REFUSED the start"; return -1; }
            double t0 = w.Time;
            while (w.Time < t0 + 600)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                    if (e.type == ZEventType.UnitBuilt && e.otherId == f.Id) { var u = w.GetAny(e.objId); if (u != null && u.Type == ZMechHp.Type) return u.MaxHealth * 100000 + u.Health; }
                w.Events.Clear();
            }
            return -1;
        }

        static void Units(StringBuilder sb, string settings, Func<string, string> map)
        {
            var on = Placed(settings, ZMechHp.Type, true); var off = Placed(settings, ZMechHp.Type, false);
            var tOn = Placed(settings, ZMTank.Type, true); var aOn = Placed(settings, "apc", true); var lOn = Placed(settings, "missile_launcher", true);
            sb.AppendLine(OzV11Kit.Verdict(on.max == MechHp && on.now == MechHp && off.max == ZodMechHp && off.now == ZodMechHp,
                $"a placed mech: {on.now} / {on.max} HP with DV147, {off.now} / {off.max} HP without"));
            sb.AppendLine(OzV11Kit.Verdict(tOn.max == MTankHp && aOn.max == ZodMechHp && lOn.max == ZodMechHp,
                $"with DV147 on: a placed medium tank {tOn.max} HP, APC {aOn.max} HP, missile launcher {lOn.max} HP (unchanged)"));
            int b1 = BuildOne(settings, map("L02"), true, out string at), b0 = BuildOne(settings, map("L02"), false, out _);
            sb.AppendLine(OzV11Kit.Verdict(b1 == MechHp * 100000 + MechHp && b0 == ZodMechHp * 100000 + ZodMechHp,
                $"level 2, {at}: a mech built with DV147 comes out with {b1 % 100000} / {b1 / 100000} HP, without {b0 % 100000} / {b0 / 100000} HP"));
        }

        // ------------------------------------------------------------------ 3. the Unit editor (its pure stats code)
        static void UnitEditor(StringBuilder sb, string settings, string statSets)
        {
            ZWorld.MechHpDeviation = true;
            var rules = UeRules.FromGame();
            var oz = OzUeLiveStats.Table(ZSettings.Parse(settings), rules);
            double mech = oz[ZMechHp.Type][(int)UeStat.Health].value, tank = oz[ZMTank.Type][(int)UeStat.Health].value;
            sb.AppendLine(OzV11Kit.Verdict(mech == MechHp && tank == MTankHp, $"the Unit editor's OpenZ set (its default stats, the mech's card): mech {mech} HP, medium tank {tank} HP"));
            if (statSets == null) sb.AppendLine(OzV11Kit.Verdict(false, "the stats sets file (unit_stat_sets.json) was not given"));
            else
            {
                var sets = OzUeSetData.Parse(statSets);
                double zod = sets.zod.TryGetValue(ZMechHp.Type, out var z) && z[(int)UeStat.Health].HasNumber ? z[(int)UeStat.Health].value : -1;
                sb.AppendLine(OzV11Kit.Verdict(sets.error == null && zod == ZodMechHp && ZWorld.MechHpDeviation, $"its ZodEngine set: mech {zod} HP (Zod's, with the switch on){(sets.error != null ? "; " + sets.error : "")}"));
            }
            var reset = OzUeStatsApply.Make(settings, rules, oz, (id, st) => double.NaN);
            int rr = reset.rows.Unit(ZMechHp.Type).MaxHealth; double rc = reset.table[ZMechHp.Type][(int)UeStat.Health].value;
            sb.AppendLine(OzV11Kit.Verdict(rr == MechHp && rc == MechHp, $"a Custom plan with no value of its own (every value reset to default = OpenZ's): mech row {rr} HP, card {rc} HP"));
            var own = OzUeStatsApply.Make(settings, rules, oz, (id, st) => id == ZMechHp.Type && st == UeStat.Health ? 7000 : double.NaN);
            int or = own.rows.Unit(ZMechHp.Type).MaxHealth;
            var w = ZWorld.Create(settings, OzV11Kit.Plain(30, 30), 1);
            w.S.Unit(ZMechHp.Type).health = own.rows.Unit(ZMechHp.Type).health;                            // the plan's row into the playground world's row, as OzUeStatsApply does
            OzV11Kit.Dummy(w, 400, 400);
            var m = OzV11Kit.Place(w, ZMechHp.Type, 120, 120, Team.Red);
            sb.AppendLine(OzV11Kit.Verdict(or == 7000 && m.MaxHealth == 7000, $"a Custom mech value of the player's (7000 HP) stays his: row {or} HP, a mech made from it {m.MaxHealth} HP (not x 0.95 again)"));
            ZWorld.MechHpDeviation = false;
            var ozOff = OzUeLiveStats.Table(ZSettings.Parse(settings), UeRules.FromGame());
            sb.AppendLine(OzV11Kit.Verdict(ozOff[ZMechHp.Type][(int)UeStat.Health].value == ZodMechHp, $"with DV147 off ('Zod exact') its OpenZ set shows the mech at {ozOff[ZMechHp.Type][(int)UeStat.Health].value} HP"));
        }

        // ------------------------------------------------------------------ 4. the computer
        sealed class Match { public readonly Dictionary<string, int> built = new Dictionary<string, int>(); public readonly Dictionary<Team, StringBuilder> order = new Dictionary<Team, StringBuilder> { [Team.Red] = new StringBuilder(), [Team.Blue] = new StringBuilder() }; public double end; public bool over; public int coins; public string draws = ""; }

        static Match Play(string settings, string mapText, int seed, int seconds, bool mixRule, bool mtank)
        {
            SetPlaySwitches();
            ZWorld.MechMixDeviation = mixRule; ZWorld.MTankDeviation = mtank;
            var w = ZWorld.Create(settings, mapText, seed);
            var red = new ZBot(w, Team.Red, ZBot.SeedFor(Team.Red, seed)); var blue = new ZBot(w, Team.Blue, ZBot.SeedFor(Team.Blue, seed));
            w.Bots.Add(blue); w.Bots.Add(red);
            var r = new Match();
            while (w.Time < seconds && !w.GameOver)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                {
                    if (e.type != ZEventType.UnitBuilt) continue;
                    var u = w.GetAny(e.objId); if (u == null) continue;
                    string k = u.Owner + " " + u.Type;
                    r.built[k] = r.built.TryGetValue(k, out int n) ? n + 1 : 1;
                    if ((u.Type == ZMTank.Mech || u.Type == ZMTank.Type) && r.order.ContainsKey(u.Owner)) r.order[u.Owner].Append(u.Type == ZMTank.Mech ? 'M' : 'T');
                }
                w.Events.Clear();
            }
            r.end = w.Time; r.over = w.GameOver; r.coins = red.MechMixCoins + blue.MechMixCoins; r.draws = "red " + red.MechMixOrder + " / blue " + blue.MechMixOrder;
            return r;
        }

        static int LongestRun(string s)
        {
            int best = 0, run = 0; char last = '\0';
            foreach (char c in s) { run = c == last ? run + 1 : 1; last = c; best = Math.Max(best, run); }
            return best;
        }

        static void Cpu(StringBuilder sb, string settings, Func<string, string> map, int seconds, int[] seeds, string[] levels, bool oldRule)
        {
            int allM = 0, allT = 0;
            foreach (var lv in levels)
            {
                foreach (bool rule in oldRule ? new[] { false, true } : new[] { true })
                {
                    int m = 0, t = 0, longest = 0, teamsBoth = 0, teamsMany = 0, coins = 0;
                    var runs = new List<string>();
                    foreach (int seed in seeds)
                    {
                        var r = Play(settings, map(lv), seed, seconds, rule, true);
                        int G(string k) => r.built.TryGetValue(k, out int n) ? n : 0;
                        m += G("Red " + ZMTank.Mech) + G("Blue " + ZMTank.Mech); t += G("Red " + ZMTank.Type) + G("Blue " + ZMTank.Type);
                        coins += r.coins;
                        foreach (var team in new[] { Team.Red, Team.Blue })
                        {
                            string o = r.order[team].ToString();
                            longest = Math.Max(longest, LongestRun(o));
                            if (o.Length >= 4) { teamsMany++; if (o.Contains('M') && o.Contains('T')) teamsBoth++; }
                        }
                        runs.Add($"s{seed} {r.end:0} s{(r.over ? " over" : "")}: red {r.order[Team.Red]} / blue {r.order[Team.Blue]}");
                    }
                    double share = 100.0 * t / Math.Max(1, m + t);
                    string what = $"{lv}, {seeds.Length} CPU-vs-CPU games x up to {seconds / 60} min, {(rule ? "DV148 (the one its team has built fewer of, a coin when even)" : "DV145's old rule (fewer on the field)")}: " +
                                  $"mechs {m}, medium tanks {t}, medium tank share {share:0} %, longest run of one type in a team's build order {longest}, teams with 4+ mech-tier builds that built both {teamsBoth} of {teamsMany}";
                    if (rule)
                    {
                        allM += m; allT += t;
                        sb.AppendLine(OzV11Kit.Verdict(m > 0 && t > 0 && share >= 35 && share <= 65 && teamsBoth == teamsMany && longest <= MaxRun, what + $" (a coin decided {coins} of the choices)"));
                    }
                    else sb.AppendLine("INFO  " + what);
                    sb.AppendLine("  build order (M mech, T medium tank): " + string.Join("; ", runs));
                }
            }
            sb.AppendLine(OzV11Kit.Verdict(allM > 0 && allT > 0, $"DV148 over all the levels: mechs {allM}, medium tanks {allT} ({100.0 * allT / Math.Max(1, allM + allT):0} % medium tanks)"));
            var lv0 = levels[levels.Length - 1];
            var a = Play(settings, map(lv0), seeds[0], seconds, true, true); var b = Play(settings, map(lv0), seeds[0], seconds, true, true);
            string ka = string.Join(",", a.built.OrderBy(x => x.Key).Select(x => x.Key + x.Value)), kb = string.Join(",", b.built.OrderBy(x => x.Key).Select(x => x.Key + x.Value));
            sb.AppendLine(OzV11Kit.Verdict(ka == kb && a.draws == b.draws && a.order[Team.Red].ToString() == b.order[Team.Red].ToString() && a.order[Team.Blue].ToString() == b.order[Team.Blue].ToString() && Math.Abs(a.end - b.end) < 1e-9,
                $"deterministic: {lv0} seed {seeds[0]} twice with DV148 gives the same game (end {a.end:0} / {b.end:0} s, the same units built, the same choices {a.draws})"));
            var z = Play(settings, map(lv0), seeds[0], seconds, true, false);
            int zt = (z.built.TryGetValue("Red " + ZMTank.Type, out int z1) ? z1 : 0) + (z.built.TryGetValue("Blue " + ZMTank.Type, out int z2) ? z2 : 0);
            int zm = (z.built.TryGetValue("Red " + ZMTank.Mech, out int z3) ? z3 : 0) + (z.built.TryGetValue("Blue " + ZMTank.Mech, out int z4) ? z4 : 0);
            sb.AppendLine(OzV11Kit.Verdict(zt == 0 && zm > 0, $"the medium tank off ('Zod exact' lists), DV148 on: {lv0} seed {seeds[0]}: no medium tank ({zm} mechs), DV148 draws nothing ({z.draws})"));
        }

#if UNITY_EDITOR
        static string StatSetsText { get { var ta = UnityEngine.Resources.Load<UnityEngine.TextAsset>(OzUeSetData.ResourcePath); return ta ? ta.text : null; } }
        static readonly string ProofDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_2_20261022/k107_mech_hp_cpu_mix/proof"));

        [UnityEditor.MenuItem("OpenZ/V1.2/Mech HP + CPU mix: counted rules check (headless, DV147 + DV148)")]
        static void Menu()
        {
            if (UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning("[OpenZ V1.2] DV147 + DV148: leave Play mode first (the check sets the simulation's static switches)."); return; }
            string report;
            using (OzV11Kit.GameSwitches()) report = Run(OzV11Kit.SettingsText, OzV11Kit.MapText, StatSetsText);
            int pass = report.Split('\n').Count(l => l.StartsWith("PASS")), fail = report.Split('\n').Count(l => l.StartsWith("FAIL"));
            string head = $"[OpenZ V1.2] DV147 mech health + DV148 CPU mix: {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}";
            try { System.IO.Directory.CreateDirectory(ProofDir); System.IO.File.WriteAllText(System.IO.Path.Combine(ProofDir, "k107_rules_check_editor.txt"), head + "\n" + report); } catch (Exception e) { head += " (report not written: " + e.Message + ")"; }
            if (fail == 0) UnityEngine.Debug.Log(head + "\n" + report); else UnityEngine.Debug.LogError(head + "\n" + report);
        }
#endif
    }
}
