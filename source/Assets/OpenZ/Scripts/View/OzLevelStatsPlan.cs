// 1.2 item 60 (DV142, Sim/ZWorld.CustomStats.cs): the pure part of CUSTOM UNIT STATS in the levels: the saved Custom profile read and
// checked value by value, the Unit editor's own plan of it (View/OzUeStatsApply.cs Make: the same rows, rule values and shuttle values the
// playground uses), the rows the Sim builds a level's world with, and the rule values a level start sets and puts back. No UnityEngine:
// the kit's headless check runs it on a headless world (Editor/OzV12CustomStatsCount.cs). The engine side: View/OzLevelStats.cs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>The Custom profile as read for a level: every value the text gives, checked as the Unit editor checks it when it loads its
    /// Custom stats (a known unit, a known value, one a Custom set can change under the game's rules, a finite number, kept inside the
    /// Unit editor's own limits); a value that fails the check is left out and the level uses OpenZ's own number for it.</summary>
    public sealed class OzStatsProfile
    {
        public string Name = "", Text = "";
        public int Base;
        /// <summary>unit id -> its values by UeStat (NaN = not given: OpenZ's own).</summary>
        public readonly Dictionary<string, double[]> Values = new Dictionary<string, double[]>();
        /// <summary>Parts of the text, values taken, values moved inside the limits, parts left out, values that differ from OpenZ's.</summary>
        public int Parts, Read, Clamped, Ignored, Changed;
        public readonly List<string> ClampedNotes = new List<string>(), IgnoredNotes = new List<string>(), ChangedNotes = new List<string>();
        /// <summary>Why the profile cannot be used at all (null = it was read).</summary>
        public string Error;
        /// <summary>A profile the selector offers: read, with at least one value that differs from OpenZ's.</summary>
        public bool Exists => Error == null && Changed > 0;
        public double Value(string id, UeStat st) => id != null && Values.TryGetValue(id, out var a) ? a[(int)st] : double.NaN;
    }

    public static class OzLevelStatsPlan
    {
        /// <summary>A longer text is not read (the Unit editor writes about 4 000 characters for every value of every unit).</summary>
        public const int MaxChars = 262144;
        /// <summary>Notes kept per kind for the log (the counts are always whole).</summary>
        public const int MaxNotes = 8;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The OpenZ set under these rules: a fresh parse of the game's settings through the game's rule functions (as the Unit
        /// editor computes it when it opens).</summary>
        public static Dictionary<string, UeCell[]> OpenZTable(string settingsText, UeRules rules) => OzUeLiveStats.Table(ZSettings.Parse(settingsText ?? ""), rules);

        // ------------------------------------------------------------------ the profile
        /// <summary>Reads the Unit editor's saved Custom text ("grunt.build=72;grunt.squad=3;...", its saved profile) value by value. Never
        /// throws: a text that cannot be read gives a profile with <see cref="OzStatsProfile.Error"/> set.</summary>
        public static OzStatsProfile Read(string text, string name, int baseIndex, Dictionary<string, UeCell[]> openz, UeRules rules)
        {
            var p = new OzStatsProfile { Name = string.IsNullOrEmpty(name) ? "" : name.Trim(), Text = text ?? "", Base = baseIndex >= 0 && baseIndex <= 2 ? baseIndex : 0 };
            if (p.Name.Length > 60) p.Name = p.Name.Substring(0, 60);
            try
            {
                if (openz == null || rules == null) { p.Error = "the game's own stats could not be read"; return p; }
                if (string.IsNullOrEmpty(text)) { p.Error = "no Custom stats saved"; return p; }
                if (text.Length > MaxChars) { p.Error = "the saved Custom stats are too long (" + text.Length + " characters)"; return p; }
                foreach (var raw in text.Split(';'))
                {
                    var part = raw.Trim();
                    if (part.Length == 0) continue;
                    p.Parts++;
                    int dot = part.IndexOf('.'), eq = part.IndexOf('=');
                    if (dot <= 0 || eq <= dot + 1) { Ignore(p, "\"" + Short(part) + "\" (not unit.value=number)"); continue; }
                    string id = part.Substring(0, dot), key = part.Substring(dot + 1, eq - dot - 1), num = part.Substring(eq + 1).Trim();
                    if (!OzUeLiveStats.Known(id) || !openz.TryGetValue(id, out var oz)) { Ignore(p, "\"" + Short(id) + "\" (no such unit)"); continue; }
                    if (!OzUeLiveStats.TryKey(key, out var st)) { Ignore(p, id + "." + Short(key) + " (no such value)"); continue; }
                    if (!OzUeLiveStats.Editable(OzUeLiveStats.Route(id, st, rules, out _)) || !oz[(int)st].HasNumber) { Ignore(p, id + "." + key + " (fixed by the game's rules)"); continue; }
                    if (!double.TryParse(num, NumberStyles.Float, Inv, out double v) || double.IsNaN(v) || double.IsInfinity(v)) { Ignore(p, id + "." + key + "=" + Short(num) + " (not a number)"); continue; }
                    double lim = OzUeLiveStats.Limit(id, st, v);
                    if (!lim.Equals(v)) { p.Clamped++; Note(p.ClampedNotes, id + "." + key + " " + v.ToString("R", Inv) + " -> " + lim.ToString("R", Inv)); }
                    if (!p.Values.TryGetValue(id, out var a)) { a = new double[OzUeLiveStats.StatCount]; for (int i = 0; i < a.Length; i++) a[i] = double.NaN; p.Values[id] = a; }
                    a[(int)st] = lim;                                                                            // the last one wins (as the Unit editor reads it)
                    p.Read++;
                }
                foreach (var id in OzUeLiveStats.Units)
                {
                    if (!p.Values.TryGetValue(id, out var a) || !openz.TryGetValue(id, out var oz)) continue;
                    for (int i = 0; i < (int)UeStat.Dps; i++)
                    {
                        if (double.IsNaN(a[i]) || !oz[i].HasNumber || OzUeLiveStats.Same(a[i], oz[i].value)) continue;
                        p.Changed++;
                        Note(p.ChangedNotes, id + " " + OzUeLiveStats.Defs[i].key + " " + OzUeLiveStats.Format((UeStat)i, a[i]) + " (OpenZ " + OzUeLiveStats.Format((UeStat)i, oz[i].value) + ")");
                    }
                }
                if (p.Read == 0) p.Error = "the saved Custom stats hold no value the game can use";
            }
            catch (Exception e) { p.Error = "the saved Custom stats could not be read (" + e.GetType().Name + ")"; }
            return p;
        }

        /// <summary>The text the Unit editor keeps and saves for a Custom set (View/OzUeSets.cs CustomText, the saved profile's format:
        /// "unit.value=number;..." of every value a Custom set can change, in the editor's order): OpenZ's own number unless
        /// <paramref name="changes"/> ("jeep.hp" -> 2195) gives one. The checks and the Play proof write their test profiles with it.</summary>
        public static string TextOf(Dictionary<string, UeCell[]> openz, UeRules rules, Dictionary<string, double> changes)
        {
            var sb = new StringBuilder(4096);
            foreach (var id in OzUeLiveStats.Units)
            {
                if (openz == null || !openz.TryGetValue(id, out var oz)) continue;
                for (int i = 0; i < (int)UeStat.Dps; i++)
                {
                    if (!OzUeLiveStats.Editable(OzUeLiveStats.Route(id, (UeStat)i, rules, out _)) || !oz[i].HasNumber) continue;
                    string key = id + "." + OzUeLiveStats.Defs[i].key;
                    double v = changes != null && changes.TryGetValue(key, out double c) ? c : oz[i].value;
                    if (sb.Length > 0) sb.Append(';');
                    sb.Append(key).Append('=').Append(v.ToString("R", Inv));
                }
            }
            return sb.ToString();
        }

        static void Ignore(OzStatsProfile p, string what) { p.Ignored++; Note(p.IgnoredNotes, what); }
        static void Note(List<string> l, string s) { if (l.Count < MaxNotes) l.Add(s); }
        static string Short(string s) => s == null ? "" : s.Length > 24 ? s.Substring(0, 24) + "..." : s;

        // ------------------------------------------------------------------ the plan and the Sim's rows
        /// <summary>The Unit editor's plan of this profile (OzUeStatsApply.Make with the profile's values; a value it does not give is
        /// OpenZ's): the rows, the rule values and the shuttle's values the playground would use with it.</summary>
        public static OzUeStatsApply.Plan Plan(OzStatsProfile p, string settingsText, UeRules rules, Dictionary<string, UeCell[]> openz) =>
            OzUeStatsApply.Make(settingsText ?? "", rules, openz, (id, st) => p != null ? p.Value(id, st) : double.NaN);

        /// <summary>The rows a level's world is built with: every row of the plan that differs from the game's own parse, and the shuttle's
        /// values (whole hit points, rule pixels per second, as the Unit editor sets them on a ship).</summary>
        public static ZCustomStats ToSim(OzUeStatsApply.Plan plan, string settingsText, string name)
        {
            var c = new ZCustomStats { Name = name ?? "" };
            if (plan == null || plan.rows == null) return c;
            var fresh = ZSettings.Parse(settingsText ?? "");
            foreach (var kv in plan.rows.Units)
            {
                var own = fresh.Unit(kv.Key);
                if (own == null || kv.Value == null || ZCustomStats.Same(own, kv.Value)) continue;
                var row = new ZUnitSettings { name = kv.Key };
                ZCustomStats.Copy(kv.Value, row);
                c.Rows[kv.Key] = row;
            }
            c.ShuttleHp = plan.shuttleHp > 0 ? (int)Math.Round(plan.shuttleHp, MidpointRounding.AwayFromZero) : 0;
            c.ShuttleSpeedPx = plan.shuttleSpeed > 0 ? OzUeStatsApply.ShuttleSpeedPx(plan.shuttleSpeed) : 0;
            return c;
        }

        /// <summary>The rule values two rule sets give (the sniper balance, the bolt cycle, the rifle pairs), exact.</summary>
        public static bool SameRuleValues(UeRules a, UeRules b) =>
            a != null && b != null && a.sniperHit.Equals(b.sniperHit) && a.sniperReload.Equals(b.sniperReload) && a.electroCycle == b.electroCycle
            && UeRules.PairText(a.pairs) == UeRules.PairText(b.pairs);

        // ------------------------------------------------------------------ the rule values (statics shared by every world)
        static bool held; static double heldHit, heldReload; static int heldCycle; static string heldPairs; static object heldBy;
        /// <summary>A level start has set its Custom rule values (they are put back by <see cref="RestoreRules"/>).</summary>
        public static bool RulesHeld => held;
        /// <summary>Checks: rule sets set and put back since the game started.</summary>
        public static int RuleSets, RuleRestores;

        /// <summary>The game's rule values are saved once, then the plan's are set (OzUeStatsApply.SetRuleValues, the Unit editor's own).</summary>
        public static void SetRules(UeRules r, object owner)
        {
            if (r == null) return;
            if (!held)
            {
                heldHit = ZWorld.SniperHitChance; heldReload = ZWorld.SniperReload; heldCycle = ZElectro.CycleTicks; heldPairs = UeRules.PairText(ZWorld.RiflePair);
                held = true;
            }
            heldBy = owner;
            OzUeStatsApply.SetRuleValues(r);
            RuleSets++;
        }

        /// <summary></summary>
        public static bool RestoreRules(object owner)
        {
            if (!held || (owner != null && !ReferenceEquals(owner, heldBy))) return false;
            ZWorld.SniperHitChance = heldHit; ZWorld.SniperReload = heldReload; ZElectro.CycleTicks = heldCycle;
            UeRules.SetPairs(ZWorld.RiflePair, heldPairs);
            held = false; heldBy = null; RuleRestores++;
            return true;
        }

        // ------------------------------------------------------------------ one level start
        /// <summary>The result of one level start (the log line, the checks).</summary>
        public sealed class Start
        {
            public bool custom;
            public UeRules rules;
            public OzStatsProfile profile;
            public OzUeStatsApply.Plan plan;
            public ZCustomStats rows;
            public bool rulesChanged;
            public string line = "";
        }

        /// <summary>A level start before the world is built (View/OzLevelStats.cs, the headless check), after the level's rule switches
        /// are set. Every start first puts back what an earlier Custom start set (whatever the order the old level and the new one are torn
        /// down and built in), then reads the game's rules (UeRules.FromGame, as the Unit editor does when it opens). Then: no profile, or
        /// the switch off: the game's own numbers, nothing set. A profile: read and checked value by value; a profile with no usable change
        /// falls back to the game's own numbers (the switch is turned off); else the rows go to the Sim (ZWorld.CustomStats) and the rule
        /// values are set where they differ from the game's. <paramref name="owner"/> is the level that may put them back.</summary>
        public static Start Begin(string text, string name, int baseIndex, string settingsText, object owner)
        {
            var s = new Start();
            RestoreRules(null);
            ZWorld.CustomStats = null; ownerOfRows = null;
            s.rules = UeRules.FromGame();
            if (!ZWorld.CustomStatsDeviation || text == null)
            {
                ZWorld.CustomStatsDeviation = false;
                s.line = "OPENZ (the game's own numbers)";
                return s;
            }
            var openz = OpenZTable(settingsText, s.rules);
            var p = Read(text, name, baseIndex, openz, s.rules);
            s.profile = p;
            if (!p.Exists)
            {
                ZWorld.CustomStatsDeviation = false;
                s.line = "OPENZ: the Custom stats cannot be used (" + (p.Error ?? "no value differs from OpenZ's") + "), the level plays with the game's own numbers";
                return s;
            }
            s.plan = Plan(p, settingsText, s.rules, openz);
            s.rows = ToSim(s.plan, settingsText, p.Name);
            ZWorld.CustomStats = s.rows; ownerOfRows = owner;
            s.rulesChanged = !SameRuleValues(s.plan.rules, s.rules);
            if (s.rulesChanged) SetRules(s.plan.rules, owner);
            s.custom = true;
            s.line = Describe(s);
            return s;
        }

        /// <summary>A Custom level ends (its own teardown, the main menu, Play stopped; owner null = whichever): its rule values back, the
        /// switch off, no rows. A level torn down after the next one began leaves the next one's values alone.</summary>
        public static void End(object owner)
        {
            RestoreRules(owner);
            if (owner == null || ReferenceEquals(ownerOfRows, owner)) { ZWorld.CustomStats = null; ZWorld.CustomStatsDeviation = false; ownerOfRows = null; }
        }

        static object ownerOfRows;
        /// <summary>The level that holds ZWorld.CustomStats now (null: none).</summary>
        public static object RowsOwner => ownerOfRows;

        /// <summary>The log line of a Custom start: the name, what differs from OpenZ, the rows, the rule values, the shuttle, and what the
        /// check did with the text.</summary>
        public static string Describe(Start s)
        {
            var p = s.profile; var sb = new StringBuilder();
            sb.Append("CUSTOM \"").Append(p.Name).Append("\" (DV142): ").Append(p.Changed).Append(p.Changed == 1 ? " value differs" : " values differ").Append(" from OpenZ");
            if (p.ChangedNotes.Count > 0) sb.Append(" (").Append(string.Join(", ", p.ChangedNotes)).Append(p.Changed > p.ChangedNotes.Count ? ", ..." : "").Append(')');
            var keys = new List<string>(s.rows.Rows.Keys); keys.Sort(StringComparer.Ordinal);
            sb.Append("; rows changed: ").Append(keys.Count > 0 ? string.Join(", ", keys) : "none");
            sb.Append("; rule values: ").Append(s.rulesChanged ? "the set's (sniper hit " + Pct(s.plan.rules.sniperHit) + " %, reload " + s.plan.rules.sniperReload.ToString("0.###", Inv)
                                                                 + " s, bolt cycle " + s.plan.rules.electroCycle + " ticks, " + s.plan.rules.pairs.Count + " rifle pairs)" : "the game's own");
            sb.Append("; shuttle ").Append(s.rows.ShuttleHp).Append(" HP, ").Append((s.rows.ShuttleSpeedPx * (double)ZConst.PX_TO_M).ToString("0.###", Inv)).Append(" m/s");
            sb.Append("; the saved text: ").Append(p.Read).Append(" values read");
            if (p.Clamped > 0) sb.Append(", ").Append(p.Clamped).Append(" kept inside the Unit editor's limits (").Append(string.Join(", ", p.ClampedNotes)).Append(p.Clamped > p.ClampedNotes.Count ? ", ..." : "").Append(')');
            if (p.Ignored > 0) sb.Append(", ").Append(p.Ignored).Append(" left out, OpenZ's own number used (").Append(string.Join(", ", p.IgnoredNotes)).Append(p.Ignored > p.IgnoredNotes.Count ? ", ..." : "").Append(')');
            sb.Append("; every other value OpenZ's own");
            return sb.ToString();
        }

        static string Pct(double f) => Math.Round(f * 100.0, 3).ToString("0.###", Inv);
    }
}
