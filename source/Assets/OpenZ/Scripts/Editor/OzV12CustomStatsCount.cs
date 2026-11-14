using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.2 item 60 (DV142, CUSTOM UNIT STATS in the levels): the counted check, headless (no scene, no Play, nothing saved; menu
    /// OpenZ/V1.2/Unit Stats: 60 Custom Stats Check (headless), Editor/OzCustomStatsMenu.cs; the same class runs outside the editor). It
    /// builds each world the way View/ZGame.cs Awake does (the Play session's rule switches of the default settings, the DV142 switch, the
    /// level start of View/OzLevelStatsPlan.cs, ZWorld.Create, the CPUs, the head start).
    ///  A  the profile: the Unit editor's own text of a Custom set is read value for value; the plan is the Unit editor's own plan of it
    ///     (OzUeStatsApply.Make as the editor calls it: the same rows, rule values and shuttle values); broken, old and partial texts never
    ///     throw: unknown units / values, fixed values, non-numbers are left out, numbers outside the Unit editor's limits are kept inside
    ///     them, a text with nothing usable or no change falls back to OPENZ.
    ///  B  per level (2, 16, 17): OPENZ = the game's own rows, nothing set; CUSTOM = every unit of every team on the map (the player's, the
    ///     CPU's, neutral vehicles and guns, the start squads, the head start, the drivers, ALAN &amp; BRAD, the shuttle) has the numbers
    ///     the Unit editor's table shows for the Custom set; the rule values (sniper, bolt cycle, rifle pair) are the set's while the
    ///     level runs and the game's again after it; PLAY AS BLUE too; the units the CPUs build later (a bot match) are made from the
    ///     Custom rows; a whole match twice with the same text = the same match tick for tick (and not the OPENZ one); OPENZ with a profile
    ///     saved, and a broken text, give the same match as a start without this kit's level start, tick for tick.
    ///  C  every unit kind (a plain map): one of each robot, vehicle and gun for red, blue and nobody, and a shuttle, each with the Custom
    ///     health the table shows.
    /// Test only.
    /// </summary>
    public static class OzV12CustomStatsCount
    {
        public static readonly string[] Levels = { "L02", "L16", "L17" };
        public const int Seed = 1, MediumTech = 2;
        /// <summary>The bot matches (simulation seconds): the full check, the quick form (the parity report's row).</summary>
        public const double MatchSeconds = 300.0, QuickSeconds = 90.0;
        const double Tick = ZConst.TICK;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ================================================================================================ test profiles
        /// <summary>The text the Unit editor writes for a Custom set (OzLevelStatsPlan.TextOf, as View/OzUeSets.cs CustomText).</summary>
        public static string ProfileText(Dictionary<string, UeCell[]> openz, UeRules rules, Dictionary<string, double> changes) => OzLevelStatsPlan.TextOf(openz, rules, changes);

        /// <summary>The mixed test set: a row value of a vehicle, a robot (its squad size too), a tank's build time and range, a rule value of
        /// each kind (the sniper's reload, the Electrobot's bolt cycle, the psycho's rifle pair) and the shuttle's own two values.</summary>
        public static Dictionary<string, double> Mixed(Dictionary<string, UeCell[]> oz) => new Dictionary<string, double>
        {
            ["jeep.hp"] = Math.Round(oz["jeep"][(int)UeStat.Health].value * 1.25), ["jeep.speed"] = Math.Round(oz["jeep"][(int)UeStat.Speed].value * 1.2, 1),
            ["grunt.squad"] = 4, ["grunt.hp"] = Math.Round(oz["grunt"][(int)UeStat.Health].value * 1.1),
            ["light.build"] = 40, ["heavy.range"] = oz["heavy"][(int)UeStat.Range].value + 2.0,
            ["sniper.reload"] = 0.6, ["electrobot.reload"] = 5.0, ["psycho.dmgGrunt"] = Math.Round(oz["psycho"][(int)UeStat.Damage].value * 2.0),
            ["shuttle.hp"] = 5000, ["shuttle.speed"] = 8,
        };

        /// <summary>Every unit's health x 1.07 (where a Custom set can change it), for part C.</summary>
        public static Dictionary<string, double> AllHealth(Dictionary<string, UeCell[]> oz, UeRules r)
        {
            var d = new Dictionary<string, double>();
            foreach (var id in OzUeLiveStats.Units)
                if (oz.TryGetValue(id, out var c) && c[(int)UeStat.Health].HasNumber && OzUeLiveStats.Editable(OzUeLiveStats.Route(id, UeStat.Health, r, out _)))
                    d[id + ".hp"] = Math.Round(c[(int)UeStat.Health].value * 1.07);
            return d;
        }

        // ================================================================================================ the check
        static int pass, fail;
        static StringBuilder sb;
        static void V(bool ok, string what) { if (ok) pass++; else fail++; sb.AppendLine((ok ? "PASS  " : "FAIL  ") + what); }
        static void Note(string what) => sb.AppendLine("      " + what);
        static string F(double v) => v.ToString("0.###", Inv);

        /// <summary>The whole check; <paramref name="map"/> gives a level's map file. <paramref name="quick"/>: level 2 only and shorter bot
        /// matches (the parity report's row). Every switch and rule value is put back afterwards.</summary>
        public static string Check(string settings, Func<string, string> map, bool quick = false, Action<string> progress = null)
        {
            sb = new StringBuilder(); pass = fail = 0;
            var t0 = DateTime.Now;
            sb.AppendLine("1.2 item 60 CUSTOM UNIT STATS in the levels (DV142): counted headless (OzV12CustomStatsCount" + (quick ? ", quick form" : "") + ")");
            Team keepShuttle = ZWorld.ShuttleTeam, keepCrews = ZWorld.L17CrewsTeam;
            try
            {
                using (OzV11Kit.GameSwitches())
                {
                    OzV12PlayBlueCount.PlaySession("L02", false, false, OzSides.For(false, false));
                    progress?.Invoke("A");
                    PartA(settings);
                    foreach (var lv in quick ? new[] { "L02" } : Levels) { progress?.Invoke("B " + lv); PartB(settings, map(lv), lv, quick); }
                    progress?.Invoke("C");
                    PartC(settings);
                }
            }
            catch (Exception ex) { V(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace); }
            finally
            {
                OzLevelStatsPlan.End(null);
                ZWorld.ShuttleTeam = keepShuttle; ZWorld.L17CrewsTeam = keepCrews;
            }
            V(!ZWorld.CustomStatsDeviation && ZWorld.CustomStats == null && !OzLevelStatsPlan.RulesHeld, "after the check: the DV142 switch off, no Custom rows, no rule value held");
            sb.AppendLine($"RESULT {pass} PASS, {fail} FAIL ({(DateTime.Now - t0).TotalSeconds:0} s)");
            return sb.ToString();
        }

        // ------------------------------------------------------------------------------------------------ A: the profile
        static void PartA(string settings)
        {
            var rules = UeRules.FromGame();
            var oz = OzLevelStatsPlan.OpenZTable(settings, rules);
            var mixed = Mixed(oz);
            string text = ProfileText(oz, rules, mixed);
            int parts = text.Split(';').Length;
            var p = OzLevelStatsPlan.Read(text, "Mixed test", 0, oz, rules);
            V(p.Exists && p.Read == parts && p.Ignored == 0 && p.Clamped == 0 && p.Changed == mixed.Count,
              $"A1 the Unit editor's text of a Custom set ({parts} values, {text.Length} characters) is read whole: {p.Read} read, {p.Changed} differ from OpenZ (want {mixed.Count}), {p.Ignored} left out, {p.Clamped} moved");

            // A2 the plan is the Unit editor's own: OzUeStatsApply.Make called as View/OzUeSets.cs calls it for the Custom set (its Custom values
            // start as OpenZ's for every value a set can change, then the saved text, kept inside the limits)
            var custom = new Dictionary<string, double[]>();
            foreach (var id in OzUeLiveStats.Units)
            {
                var a = new double[OzUeLiveStats.StatCount]; for (int i = 0; i < a.Length; i++) a[i] = double.NaN;
                for (int i = 0; i < (int)UeStat.Dps; i++)
                    if (OzUeLiveStats.Editable(OzUeLiveStats.Route(id, (UeStat)i, rules, out _)) && oz[id][i].HasNumber) a[i] = oz[id][i].value;
                custom[id] = a;
            }
            foreach (var part in text.Split(';'))
            {
                int dot = part.IndexOf('.'), eq = part.IndexOf('=');
                string id = part.Substring(0, dot);
                OzUeLiveStats.TryKey(part.Substring(dot + 1, eq - dot - 1), out var st);
                custom[id][(int)st] = OzUeLiveStats.Limit(id, st, double.Parse(part.Substring(eq + 1), NumberStyles.Float, Inv));
            }
            var editor = OzUeStatsApply.Make(settings, rules, oz, (id, st) => custom.TryGetValue(id, out var a) ? a[(int)st] : double.NaN);
            var mine = OzLevelStatsPlan.Plan(p, settings, rules, oz);
            int rowsSame = 0, rowsAll = 0, cellsSame = 0, cellsAll = 0;
            foreach (var kv in editor.rows.Units) { rowsAll++; if (ZCustomStats.Same(kv.Value, mine.rows.Unit(kv.Key))) rowsSame++; }
            foreach (var id in OzUeLiveStats.Units)
                for (int i = 0; i < OzUeLiveStats.StatCount; i++)
                {
                    cellsAll++;
                    var x = editor.table[id][i]; var y = mine.table[id][i];
                    if (x.kind == y.kind && (!x.HasNumber || x.value.Equals(y.value))) cellsSame++;
                }
            V(rowsSame == rowsAll && cellsSame == cellsAll && OzLevelStatsPlan.SameRuleValues(editor.rules, mine.rules) && editor.shuttleHp.Equals(mine.shuttleHp) && editor.shuttleSpeed.Equals(mine.shuttleSpeed),
              $"A2 the level's plan IS the Unit editor's plan of the same Custom set: rows {rowsSame}/{rowsAll}, table cells {cellsSame}/{cellsAll}, rule values the same, shuttle {F(mine.shuttleHp)} HP {F(mine.shuttleSpeed)} m/s");
            var sim = OzLevelStatsPlan.ToSim(mine, settings, p.Name);
            var rowNames = sim.Rows.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            V(string.Join(",", rowNames) == "grunt,heavy,jeep,light" && sim.ShuttleHp == 5000 && sim.ShuttleSpeedPx == OzUeStatsApply.ShuttleSpeedPx(8),
              $"A3 the rows for the Sim are the changed ones only ({string.Join(", ", rowNames)}), the shuttle 5000 HP {sim.ShuttleSpeedPx} px/s; the rule values (sniper reload, bolt cycle, rifle pair) stay out of the rows");

            // A4 broken, old and partial texts
            int oks = 0, total = 0; var bad = new List<string>();
            void T(string label, string t, bool exists, int changed, int ignoredMin, int clamped, Func<OzStatsProfile, bool> extra = null)
            {
                total++;
                OzStatsProfile q = null; bool threw = false;
                try { q = OzLevelStatsPlan.Read(t, "x", 0, oz, rules); } catch (Exception) { threw = true; }
                bool ok = !threw && q != null && q.Exists == exists && (changed < 0 || q.Changed == changed) && q.Ignored >= ignoredMin && (clamped < 0 || q.Clamped == clamped) && (extra == null || extra(q));
                if (ok) oks++; else bad.Add(label + (q == null ? " (threw)" : $" (exists {q.Exists}, changed {q.Changed}, ignored {q.Ignored}, clamped {q.Clamped}, error {q.Error ?? "none"})"));
            }
            string jeepHp = "jeep.hp=" + mixed["jeep.hp"].ToString("R", Inv);
            T("nothing saved (null)", null, false, -1, 0, -1, q => q.Error != null);
            T("an empty text", "", false, -1, 0, -1, q => q.Error != null);
            T("garbage", "garbage;;=;.x=1;jeep.=2;jeep.hp;=5", false, -1, 5, -1, q => q.Error != null);
            T("unknown units / values, fixed values, non-numbers", "tank.hp=5;jeep.armour=3;heroes.hp=9999;apc.dmg=5;jeep.hp=NaN;jeep.hp=Infinity;jeep.hp=1e999;jeep.reload=0,5", false, -1, 8, -1);
            T("a value under the limit (kept at the Unit editor's minimum)", "jeep.hp=-5", true, 1, 0, 1, q => q.Value("jeep", UeStat.Health) == OzUeLiveStats.Def(UeStat.Health).min);
            T("a value over the limit (kept at its maximum)", "jeep.hp=9e9", true, 1, 0, 1, q => q.Value("jeep", UeStat.Health) == OzUeLiveStats.Def(UeStat.Health).max);
            T("an old partial text (one value)", jeepHp, true, 1, 0, 0, q => OzLevelStatsPlan.ToSim(OzLevelStatsPlan.Plan(q, settings, rules, oz), settings, "x").Rows.Count == 1);
            T("a later value of the same key wins", "jeep.hp=1500;jeep.hp=1600", true, 1, 0, 0, q => q.Value("jeep", UeStat.Health) == 1600);
            T("a newer file's unknown values beside known ones", "jeep.hp=1500;jeep.armour=9;laser.shield=3", true, 1, 2, 0);
            T("every value OpenZ's own (the editor's RESET STATS): no profile", ProfileText(oz, rules, null), false, 0, 0, 0);
            T("a text too long to read", new string('x', OzLevelStatsPlan.MaxChars + 1), false, -1, 0, -1, q => q.Error != null && q.Error.Contains("too long"));
            T("spaces round the parts", " jeep.hp = 1500 ; ;grunt.squad=4 ", true, -1, 1, 0, q => q.Value("grunt", UeStat.Squad) == 4);
            V(oks == total, $"A4 broken / old / partial texts: {oks} of {total} handled as the rule says, none throws" + (bad.Count > 0 ? "; WRONG: " + string.Join("; ", bad) : ""));
        }

        // ------------------------------------------------------------------------------------------------ B: the levels
        sealed class Expect
        {
            public Dictionary<string, UeCell[]> table; public int gruntHp, heroHp; public UeRules rules; public int shuttleHp, shuttleSpeedPx;
        }

        static Expect ExpectOf(OzLevelStatsPlan.Start s) => new Expect
        {
            table = s.plan.table, rules = s.plan.rules, gruntHp = (int)s.plan.table["grunt"][(int)UeStat.Health].value,
            heroHp = (int)s.plan.table[OzUeLiveStats.Heroes][(int)UeStat.Health].value, shuttleHp = s.rows.ShuttleHp, shuttleSpeedPx = s.rows.ShuttleSpeedPx,
        };

        /// <summary>The level start with the DV142 switch as View/ZGame.cs sets it, then the world as ZGame builds it.</summary>
        static ZWorld Start(string settings, string map, string level, OzSideSetup sides, bool custom, string text, object owner, out OzLevelStatsPlan.Start s)
        {
            ZWorld.CustomStatsDeviation = custom;
            s = OzLevelStatsPlan.Begin(custom ? text : null, "Mixed test", 0, settings, owner);
            return OzV12PlayBlueCount.NewMatch(settings, map, level, sides, ZBotDifficulty.Normal, MediumTech, true, Seed);
        }

        /// <summary>Every unit of the world against the Custom table: health (heroes and the shuttle by their own rules), speed, squad size
        /// (robots), damage / reload / range (armed units by the plain row rule), the drivers' health. Returns the count checked per team.</summary>
        static (int red, int blue, int neutral, int wrong, string first) Units(ZWorld w, Expect e, Func<ZObject, bool> which)
        {
            int red = 0, blue = 0, neutral = 0, wrong = 0; string first = null;
            void Bad(ZObject o, string what) { wrong++; if (first == null) first = $"{o.Owner} {o.Type} #{o.Id}: {what}"; }
            foreach (var o in w.Objects)
            {
                if (!o.IsUnit || o.Destroyed || !which(o)) continue;
                if (o.IsFortDefender) continue;                                                                // the fort's roof defender: the game's own numbers for him (DV41)
                string id = o.IsShuttle ? OzUeLiveStats.Shuttle : o.Hero != null ? OzUeLiveStats.Heroes : o.Type;
                if (!e.table.TryGetValue(id, out var c)) continue;
                if (o.Owner == Team.Red) red++; else if (o.Owner == Team.Blue) blue++; else neutral++;
                int hp = (int)c[(int)UeStat.Health].value;
                if (o.MaxHealth != hp) Bad(o, $"health {o.MaxHealth}, the table {hp}");
                if (o.IsShuttle) { if (o.MoveSpeed != e.shuttleSpeedPx) Bad(o, $"speed {o.MoveSpeed} px/s, the set's {e.shuttleSpeedPx}"); continue; }
                if (o.Hero != null) continue;                                                                  // ALAN & BRAD: health by the hero rule (above); their range / trigger are the hero rules'
                if (c[(int)UeStat.Speed].HasNumber && Math.Abs(o.MoveSpeed * (double)ZConst.PX_TO_M - c[(int)UeStat.Speed].value) > 1e-9) Bad(o, $"speed {o.MoveSpeed} px/s, the table {F(c[(int)UeStat.Speed].value)} m/s");
                if (o.Kind == ZKind.Robot && !o.IsMinion && o.Minions.Count + 1 != (int)c[(int)UeStat.Squad].value && w.TickIndex == 0) Bad(o, $"squad {o.Minions.Count + 1}, the table {c[(int)UeStat.Squad].value}");
                bool armed = o.Type != "apc" && o.Type != "crane" && c[(int)UeStat.Damage].HasNumber;
                if (armed && o.Damage != (int)c[(int)UeStat.Damage].value) Bad(o, $"damage {o.Damage}, the table {c[(int)UeStat.Damage].value}");
                if (armed && c[(int)UeStat.Range].HasNumber && Math.Abs(o.AttackRadius * (double)ZConst.PX_TO_M - c[(int)UeStat.Range].value) > 1e-9) Bad(o, $"range {o.AttackRadius} px, the table {F(c[(int)UeStat.Range].value)} m");
                if (armed && o.Type != ZElectro.Type && c[(int)UeStat.Reload].HasNumber && Math.Abs(o.AttackSpeed - c[(int)UeStat.Reload].value) > 1e-9) Bad(o, $"reload {F(o.AttackSpeed)} s, the table {F(c[(int)UeStat.Reload].value)} s");
                if (o.Kind != ZKind.Robot && o.Owner != Team.Neutral && o.Drivers.Count > 0 && o.DriverType == "grunt" && w.TickIndex == 0 && o.Drivers[0] != e.gruntHp) Bad(o, $"driver {o.Drivers[0]} HP, the Custom grunt's {e.gruntHp}");
            }
            return (red, blue, neutral, wrong, first);
        }

        static bool RowsAreTheGame(ZWorld w, string settings, out string first)
        {
            first = null; var fresh = ZSettings.Parse(settings);
            foreach (var kv in fresh.Units) if (!ZCustomStats.Same(kv.Value, w.S.Unit(kv.Key))) { first = kv.Key; return false; }
            return true;
        }

        static void PartB(string settings, string map, string level, bool quick)
        {
            var normal = OzSides.For(false, false); var bots = OzSides.For(false, true); var blue = OzSides.For(true, false);
            OzV12PlayBlueCount.PlaySession(level, false, false, normal);
            var gameRules = UeRules.FromGame();
            var oz = OzLevelStatsPlan.OpenZTable(settings, gameRules);
            string text = ProfileText(oz, gameRules, Mixed(oz));
            var owner = new object();

            // B1 OPENZ: nothing set, the game's own rows
            var w0 = Start(settings, map, level, normal, false, text, owner, out var s0);
            bool rows0 = RowsAreTheGame(w0, settings, out var f0);
            V(!s0.custom && ZWorld.CustomStats == null && !ZWorld.CustomStatsDeviation && !OzLevelStatsPlan.RulesHeld && w0.CustomStatsInUse == null && rows0,
              $"B1 {level} OPENZ (a profile saved, OPENZ chosen): nothing set, every row of the world is the game's own parse{(f0 != null ? " (NOT: " + f0 + ")" : "")}");
            var wb = Start(settings, map, level, normal, true, "garbage;tank.hp=1", owner, out var sb0);
            V(!sb0.custom && !ZWorld.CustomStatsDeviation && wb.CustomStatsInUse == null && RowsAreTheGame(wb, settings, out _) && sb0.line.StartsWith("OPENZ"),
              $"B1 {level} a broken text with CUSTOM chosen falls back to OPENZ and says so: \"{Cut(sb0.line, 150)}\"");

            // B2 CUSTOM: every unit on the map
            var w1 = Start(settings, map, level, normal, true, text, owner, out var s1);
            var e = ExpectOf(s1);
            var u = Units(w1, e, o => true);
            bool rulesOn = ZWorld.SniperReload.Equals(e.rules.sniperReload) && ZElectro.CycleTicks == e.rules.electroCycle && ZWorld.RiflePair.TryGetValue(("psycho", "grunt"), out int pair) && pair == (int)Math.Round(e.table["psycho"][(int)UeStat.VsGrunt].value);
            int jeeps = w1.Objects.Count(o => o.Type == "jeep" && !o.Destroyed), heroes = w1.Objects.Count(o => o.Hero != null), ships = w1.Objects.Count(o => o.IsShuttle);
            V(s1.custom && w1.CustomStatsInUse == s1.rows && u.wrong == 0 && u.red > 0 && u.blue > 0 && rulesOn,
              $"B2 {level} CUSTOM: every unit at the start has the Custom table's numbers: red {u.red}, blue {u.blue}, nobody's {u.neutral} checked ({jeeps} jeeps, {heroes} heroes, {ships} shuttles), {u.wrong} wrong{(u.first != null ? " (first: " + u.first + ")" : "")}; "
              + $"rule values the set's (sniper reload {F(ZWorld.SniperReload)} s, bolt {ZElectro.CycleTicks} ticks, psycho on a grunt {(ZWorld.RiflePair.TryGetValue(("psycho", "grunt"), out int pp) ? pp : 0)} HP): {rulesOn}");
            if (level == "L16") V(heroes > 0 && ships > 0, $"B2 {level} the map's ALAN & BRAD ({heroes} robots, {e.heroHp} HP each) and its shuttle ({e.shuttleHp} HP) are among them");
            V(w1.S.Unit("light").build_time == 40, $"B2 {level} the factories read the Custom build time (light tank {w1.S.Unit("light").build_time} s, OpenZ {oz["light"][(int)UeStat.Build].value} s)");

            // B3 PLAY AS BLUE: the same rows for the human blue and the red CPU
            OzV12PlayBlueCount.PlaySession(level, false, false, blue);
            var w2 = Start(settings, map, level, blue, true, text, owner, out var s2);
            var u2 = Units(w2, ExpectOf(s2), o => true);
            V(s2.custom && u2.wrong == 0 && u2.red > 0 && u2.blue > 0, $"B3 {level} PLAY AS BLUE + CUSTOM: red {u2.red}, blue {u2.blue}, nobody's {u2.neutral} checked, {u2.wrong} wrong{(u2.first != null ? " (" + u2.first + ")" : "")}");
            OzV12PlayBlueCount.PlaySession(level, false, false, normal);

            // B4 the rule values come back: the level's own teardown, the leftover case, an old level torn down after the next one began
            OzLevelStatsPlan.End(owner);
            bool back = !OzLevelStatsPlan.RulesHeld && SameStatics(gameRules) && ZWorld.CustomStats == null && !ZWorld.CustomStatsDeviation;
            var a = new object(); var b = new object();
            Start(settings, map, level, normal, true, text, a, out _);
            Start(settings, map, level, normal, false, text, b, out _);                                       // the next level (OPENZ) begins before the old one is torn down
            bool leftover = SameStatics(gameRules) && !OzLevelStatsPlan.RulesHeld;
            Start(settings, map, level, normal, true, text, b, out _);                                        // the next level (CUSTOM) ...
            OzLevelStatsPlan.End(a);                                                                          // ... then the old one goes: it leaves b's values alone
            bool keepsNext = OzLevelStatsPlan.RulesHeld && ZWorld.CustomStats != null && ZElectro.CycleTicks == e.rules.electroCycle;
            OzLevelStatsPlan.End(b);
            V(back && leftover && keepsNext && SameStatics(gameRules), $"B4 {level} the game's rule values are back after the level ({back}); a new level start puts back what an old one left ({leftover}); an old level torn down after the next began leaves the next one's values ({keepsNext})");

            // B5 units built later: a bot match, every unit at first sight
            double secs = quick ? QuickSeconds : MatchSeconds;
            OzV12PlayBlueCount.PlaySession(level, false, false, bots);
            var w3 = Start(settings, map, level, bots, true, text, owner, out var s3);
            var e3 = ExpectOf(s3);
            int startMax = w3.Objects.Count > 0 ? w3.Objects.Max(o => o.Id) : 0; var seen = new HashSet<int>();
            int laterRed = 0, laterBlue = 0, laterWrong = 0, laterJeeps = 0, laterGrunts = 0; string laterFirst = null;
            while (!w3.GameOver && w3.Time < secs)
            {
                w3.Tick(Tick); w3.Events.Clear();
                if (w3.TickIndex % 10 != 0) continue;
                foreach (var o in w3.Objects)
                {
                    if (o.Id <= startMax || !o.IsUnit || o.Destroyed || !seen.Add(o.Id) || o.IsMinion) continue;
                    if (!e3.table.TryGetValue(o.IsShuttle ? OzUeLiveStats.Shuttle : o.Hero != null ? OzUeLiveStats.Heroes : o.Type, out var c)) continue;
                    if (o.Owner == Team.Red) laterRed++; else if (o.Owner == Team.Blue) laterBlue++;
                    if (o.Type == "jeep") laterJeeps++;
                    bool ok = o.MaxHealth == (int)c[(int)UeStat.Health].value;
                    if (o.Type == "grunt" && o.Hero == null && !o.IsFortDefender) { laterGrunts++; ok &= o.Minions.Count + 1 <= (int)c[(int)UeStat.Squad].value; }   // a factory's squad is the row's size; a driver who got out is a squad of one
                    if (!ok) { laterWrong++; if (laterFirst == null) laterFirst = $"{o.Owner} {o.Type} #{o.Id}: health {o.MaxHealth}, squad {o.Minions.Count + 1}"; }
                }
            }
            V(laterRed > 0 && laterBlue > 0 && laterWrong == 0, $"B5 {level} units made after the start (a bot match, {F(w3.Time)} s): red {laterRed}, blue {laterBlue} ({laterGrunts} grunt squads, {laterJeeps} jeeps), every one with the Custom health / squad: {laterWrong} wrong{(laterFirst != null ? " (" + laterFirst + ")" : "")}");
            OzLevelStatsPlan.End(owner);

            // B6 deterministic: the same text twice = the same match; OPENZ = a start without this kit's level start, tick for tick
            ulong Match(bool custom, string t, bool noBegin)
            {
                OzV12PlayBlueCount.PlaySession(level, false, false, bots);
                ZWorld w;
                if (noBegin) { ZWorld.CustomStatsDeviation = false; ZWorld.CustomStats = null; w = OzV12PlayBlueCount.NewMatch(settings, map, level, bots, ZBotDifficulty.Normal, MediumTech, true, Seed); }
                else w = Start(settings, map, level, bots, custom, t, owner, out _);
                ulong h = 1469598103934665603UL;
                while (!w.GameOver && w.Time < secs) { w.Tick(Tick); h = OzV12PlayBlueCount.TickHash(w, h); w.Events.Clear(); }
                OzLevelStatsPlan.End(owner);
                return h;
            }
            ulong c1 = Match(true, text, false), c2 = Match(true, text, false), o1 = Match(false, text, false), plain = Match(false, null, true), broken = Match(true, "jeep.hp=abc", false);
            V(c1 == c2 && c1 != o1, $"B6 {level} CUSTOM twice with the same text: the same match tick for tick ({c1:x16} = {c2:x16}), not the OPENZ one ({o1:x16})");
            V(o1 == plain && broken == plain, $"B6 {level} OPENZ with a profile saved ({o1:x16}) and a broken text with CUSTOM ({broken:x16}) = a start without the kit's level start ({plain:x16}), tick for tick for {F(secs)} s");
            Note($"{level}: {s1.line}");
        }

        static bool SameStatics(UeRules r) =>
            ZWorld.SniperHitChance.Equals(r.sniperHit) && ZWorld.SniperReload.Equals(r.sniperReload) && ZElectro.CycleTicks == r.electroCycle && UeRules.PairText(ZWorld.RiflePair) == UeRules.PairText(r.pairs);

        static string Cut(string s, int n) => s == null ? "" : s.Length <= n ? s : s.Substring(0, n) + "...";

        // ------------------------------------------------------------------------------------------------ C: every unit kind
        static void PartC(string settings)
        {
            OzV12PlayBlueCount.PlaySession("L02", false, false, OzSides.For(false, false));
            var rules = UeRules.FromGame();
            var oz = OzLevelStatsPlan.OpenZTable(settings, rules);
            var changes = AllHealth(oz, rules);
            string text = ProfileText(oz, rules, changes);
            var owner = new object();
            ZWorld.CustomStatsDeviation = true;
            var s = OzLevelStatsPlan.Begin(text, "Every health", 0, settings, owner);
            var plain = OzV11Kit.Plain(80, 60);
            var w = ZWorld.Create(settings, plain, Seed);
            var e = ExpectOf(s);
            int made = 0, x = 4;
            var kinds = new List<string>();
            foreach (var id in OzUeLiveStats.Units)
            {
                if (id == OzUeLiveStats.Heroes) continue;                                                     // part B checks ALAN & BRAD on level 16
                if (id == OzUeLiveStats.Shuttle) { w.CreatePlayableShuttle(x * 16 + 16, 50 * 16, Team.Red); made++; kinds.Add(id); x += 4; continue; }
                bool robot = ZBuildLists.IsRobot(id), gun = ZBuildLists.IsCannon(id);
                var teams = robot ? new[] { Team.Red, Team.Blue } : new[] { Team.Red, Team.Blue, Team.Neutral };
                int y = 4;
                foreach (var t in teams)
                {
                    if (robot) w.CreateRobotGroup(id, x * 16, y * 16, t);
                    else w.CreateObject(gun ? "cannon" : "vehicle", id, x * 16, y * 16, t);
                    made++; y += 4;
                }
                kinds.Add(id); x += 4;
            }
            var u = Units(w, e, o => true);
            int changedHp = changes.Count;
            V(s.custom && u.wrong == 0 && kinds.Count == OzUeLiveStats.Units.Length - 1 && u.red > 0 && u.blue > 0 && u.neutral > 0,
              $"C  every unit kind ({kinds.Count}: {string.Join(" ", kinds)}; {changedHp} health values changed) made for red, blue and nobody on a plain map: red {u.red}, blue {u.blue}, nobody's {u.neutral}, each with the Custom health: {u.wrong} wrong{(u.first != null ? " (" + u.first + ")" : "")}");
            OzLevelStatsPlan.End(owner);
        }
    }
}
