// OpenZ = read LIVE from the game when the editor
// opens (OzUeLiveStats), ZodEngine + Z-Wiki = the shipped data (OzUeSetData), Custom = the player's own copy, autosaved (PlayerPrefs
// oz_ue_custom*), saved to a file by the editor's SAVE (OzUeFiles). The set in use drives the fight at once (OzUeStatsApply).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>The four stats sets and the set the playground uses.</summary>
    public static partial class OzUeSets
    {
        public const int StatCount = OzUeLiveStats.StatCount;
        public const string KeySet = "oz_ue_set", KeyCustom = "oz_ue_custom", KeyBase = "oz_ue_custom_base", KeyName = "oz_ue_custom_name";
        public const string DefaultName = "My custom stats", FileFormat = "openz-unit-stats";
        public const int FileVersion = 1;
        const string Log = "[OpenZ UnitEditor] ";

        public static readonly UeSet[] All = { UeSet.OpenZ, UeSet.ZodEngine, UeSet.ZWiki, UeSet.Custom };
        public static readonly UeSet[] Sources = { UeSet.OpenZ, UeSet.ZodEngine, UeSet.ZWiki };

        // ------------------------------------------------------------------ state of one editor session
        internal sealed class Data
        {
            public string settingsText;
            public UeRules rules;                                          // the game's switches and rule values as the level start set them
            public Dictionary<string, UeCell[]> openz;                     // the OpenZ set, live
            public OzUeSetData.Sets shipped;                               // ZodEngine + Z-Wiki
            public readonly Dictionary<string, double[]> custom = new Dictionary<string, double[]>();
            public UeSet customBase = UeSet.OpenZ;
            public string customName = DefaultName;
            public OzUeStatsApply.Plan customPlan, inUsePlan;
            public string error;
        }

        static Data data;
        static UeSet inUse = UeSet.OpenZ;
        static ZWorld world;
        static bool held, saveDirty;
        static float saveAt;

        internal static Data D => data ?? (data = Load(false));
        internal static bool Ready => data != null && data.openz != null;
        /// <summary>The set the playground uses now.</summary>
        public static UeSet InUse => inUse;
        /// <summary>The set in use or any Custom value changed (raised on the main thread from the editor's own calls only).</summary>
        public static event Action Changed;
        /// <summary>Counts for the checks and the proof.</summary>
        public static int Applies, UnitsRecopied, CustomEdits;

        /// <summary>OzUeStatsRoot.Awake: a fresh session. The OpenZ set is computed NOW, before any set is applied (the rule functions read
        /// the current statics, which a set changes later), then the remembered set is applied to the playground's world.</summary>
        internal static void Open(ZWorld w)
        {
            data = Load(true);
            world = w;
            held = false;
            int s = PlayerPrefs.GetInt(KeySet, 0);
            inUse = s >= 0 && s <= 3 ? (UeSet)s : UeSet.OpenZ;
            Use(inUse, false, false);
            Debug.Log(Log + "stats sets ready: OpenZ live (" + data.openz.Count + " units), ZodEngine + Z-Wiki " +
                      (data.shipped.error == null ? data.shipped.values + " values" : "NOT LOADED: " + data.shipped.error) + ", set in use " + Name(inUse));
        }

        /// <summary>OzUeStatsRoot.OnDestroy: the Custom values are saved; the session's data and every listener go.</summary>
        internal static void Close()
        {
            if (data != null) SaveCustomNow();
            Changed = null;
            world = null;
            data = null;
            held = false;
        }

        static Data Load(bool log)
        {
            var d = new Data();
            var st = Resources.Load<TextAsset>("OpenZ/default_settings");
            d.settingsText = st != null ? st.text : "";
            if (st == null) { d.error = "OpenZ/default_settings is missing"; Debug.LogError(Log + d.error); }
            d.rules = UeRules.FromGame();
            d.openz = OzUeLiveStats.Table(ZSettings.Parse(d.settingsText), d.rules);
            var js = Resources.Load<TextAsset>(OzUeSetData.ResourcePath);
            d.shipped = OzUeSetData.Parse(js != null ? js.text : null);
            if (js == null) d.shipped.error = OzUeSetData.ResourcePath + " is missing";
            if (d.shipped.error != null) Debug.LogError(Log + "the ZodEngine / Z-Wiki sets: " + d.shipped.error);
            LoadCustom(d);
            d.customPlan = MakePlan(d, UeSet.Custom);
            return d;
        }

        // ------------------------------------------------------------------ names and colours (DESIGN_SPEC 3.2)
        public static string Name(UeSet set) => set == UeSet.OpenZ ? "OpenZ" : set == UeSet.ZodEngine ? "ZodEngine" : set == UeSet.ZWiki ? "Z-Wiki" : "Custom";
        public static string LongName(UeSet set) =>
            set == UeSet.OpenZ ? "OpenZ (this game, as played)" : set == UeSet.ZodEngine ? "ZodEngine (open-source Z remake)"
            : set == UeSet.ZWiki ? "Z-Wiki (the fan wiki of the original 1996 Z)" : "Custom (your own values)";
        /// <summary>The set's key in a saved file ("based_on").</summary>
        public static string FileKey(UeSet set) => set == UeSet.OpenZ ? "openz" : set == UeSet.ZodEngine ? "zod" : set == UeSet.ZWiki ? "wiki" : "custom";
        static readonly Color ColOpenZ = new Color32(0xF2, 0xB8, 0x4B, 255), ColZod = new Color32(0x8F, 0xC7, 0xA0, 255), ColWiki = new Color32(0xD6, 0xCF, 0xAE, 255),
                              ColCustom = new Color32(0x6F, 0xD3, 0xE8, 255);
        static readonly Color LightOpenZ = new Color32(0xFF, 0xD9, 0x83, 255), LightZod = new Color32(0xB6, 0xE3, 0xC3, 255), LightWiki = new Color32(0xEF, 0xE9, 0xCF, 255),
                              LightCustom = new Color32(0xA9, 0xEC, 0xF7, 255);
        static readonly Color DiffZod = new Color32(0xA8, 0xE2, 0xB8, 255), DiffWiki = new Color32(0xEF, 0xDC, 0x8C, 255), DiffCustom = new Color32(0x95, 0xE7, 0xF5, 255);
        public static Color Colour(UeSet set) => set == UeSet.OpenZ ? ColOpenZ : set == UeSet.ZodEngine ? ColZod : set == UeSet.ZWiki ? ColWiki : ColCustom;
        /// <summary>The light end of the set colour's gradient (the selected switch cell).</summary>
        public static Color Light(UeSet set) => set == UeSet.OpenZ ? LightOpenZ : set == UeSet.ZodEngine ? LightZod : set == UeSet.ZWiki ? LightWiki : LightCustom;
        /// <summary>A value that differs from OpenZ, in that set's column (OpenZ itself: INK_2).</summary>
        public static Color DiffColour(UeSet set) => set == UeSet.OpenZ ? (Color)OzUi.Ink2 : set == UeSet.ZodEngine ? DiffZod : set == UeSet.ZWiki ? DiffWiki : DiffCustom;

        public static string StatName(UeStat s) => OzUeLiveStats.Def(s).full;
        public static string StatShort(UeStat s) => OzUeLiveStats.Def(s).head;
        public static string StatUnit(UeStat s) => OzUeLiveStats.Def(s).unit;
        /// <summary>"Base build time (...) (s)": the long name with its unit (tooltips, the saved file's "fields").</summary>
        public static string StatLong(UeStat s) { var d = OzUeLiveStats.Def(s); return d.longText + " (" + d.unitLong + ")"; }
        /// <summary>"GRUNT", "LIGHT TANK", "ALAN & BRAD" ...</summary>
        public static string UnitName(string id) => OzUePlayground.FullName(id);

        // ------------------------------------------------------------------ cells
        public static UeCell Cell(string id, UeStat stat, UeSet set)
        {
            var d = D;
            if (id == null || !OzUeLiveStats.Known(id)) return UeCell.Of(UeCellKind.NotInSet);
            int i = (int)stat;
            switch (set)
            {
                case UeSet.OpenZ: return d.openz[id][i];
                case UeSet.ZodEngine: return d.shipped.zod.TryGetValue(id, out var z) ? z[i] : UeCell.Of(UeCellKind.NotInSet);
                case UeSet.ZWiki: return d.shipped.wiki.TryGetValue(id, out var w) ? w[i] : UeCell.Of(UeCellKind.NotInSet);
            }
            if (stat == UeStat.Dps) return OzUeLiveStats.DpsCell(Cell(id, UeStat.Damage, set), Cell(id, UeStat.Reload, set), Cell(id, UeStat.Hit, set));
            var route = Route(id, stat, out _);
            if (OzUeLiveStats.Editable(route)) return UeCell.Num(CustomValue(id, stat));
            if (route == UeRoute.Fixed && d.customPlan != null) return d.customPlan.table[id][i];
            return d.openz[id][i];
        }

        /// <summary>The value the playground uses now (the set in use through the game's rules); NaN = no value.</summary>
        public static double InPlay(string id, UeStat stat)
        {
            var d = D;
            if (id == null || !OzUeLiveStats.Known(id)) return double.NaN;
            var t = d.inUsePlan != null ? d.inUsePlan.table : d.openz;
            var c = t[id][(int)stat];
            return c.HasNumber ? c.value : double.NaN;
        }

        public static UeCell InPlayCell(string id, UeStat stat)
        {
            var d = D;
            if (id == null || !OzUeLiveStats.Known(id)) return UeCell.Of(UeCellKind.NotInSet);
            return (d.inUsePlan != null ? d.inUsePlan.table : d.openz)[id][(int)stat];
        }

        public static string Text(UeStat stat, UeCell cell) => OzUeLiveStats.Text(stat, cell);

        /// <summary>How a Custom value of this unit reaches the Sim; rule = the game's rule that fixes it.</summary>
        public static UeRoute Route(string id, UeStat stat, out string rule) => OzUeLiveStats.Route(id, stat, D.rules, out rule);
        public static bool Editable(string id, UeStat stat) => OzUeLiveStats.Editable(Route(id, stat, out _));

        /// <summary>The set's value differs from OpenZ's (shown in the set's DIFF colour).</summary>
        public static bool Differs(string id, UeStat stat, UeSet set)
        {
            if (set == UeSet.OpenZ) return false;
            var c = Cell(id, stat, set); var o = Cell(id, stat, UeSet.OpenZ);
            return c.HasNumber && o.HasNumber && !SameShown(stat, c.value, o.value);
        }

        /// <summary>Equal as shown (DPS and other worked-out values compare by their rounded text).</summary>
        public static bool SameShown(UeStat stat, double a, double b) => OzUeLiveStats.Same(a, b) || OzUeLiveStats.Format(stat, a) == OzUeLiveStats.Format(stat, b);

        public static UeNote Note(string id, UeStat stat) => id != null && D.shipped.notes.TryGetValue(id, out var n) && n.TryGetValue(stat, out var x) ? x : null;

        // ------------------------------------------------------------------ the set in use
        /// <summary>The playground's units use this set at once (health keeps its fraction); remembered (oz_ue_set).</summary>
        public static void Use(UeSet set) => Use(set, true, true);

        static void Use(UeSet set, bool remember, bool raise)
        {
            var d = D;
            inUse = set;
            if (remember) PlayerPrefs.SetInt(KeySet, (int)set);
            d.inUsePlan = set == UeSet.Custom ? d.customPlan : MakePlan(d, set);
            ApplyInUse();
            if (raise) RaiseChanged();
        }

        static OzUeStatsApply.Plan MakePlan(Data d, UeSet set)
        {
            Func<string, UeStat, double> value;
            if (set == UeSet.OpenZ) value = null;
            else if (set == UeSet.Custom) value = (id, st) => d.custom.TryGetValue(id, out var a) ? a[(int)st] : double.NaN;
            else
            {
                var src = set == UeSet.ZodEngine ? d.shipped.zod : d.shipped.wiki;
                value = (id, st) => src.TryGetValue(id, out var c) && c[(int)st].HasNumber ? c[(int)st].value : double.NaN;
            }
            return OzUeStatsApply.Make(d.settingsText, d.rules, d.openz, value);
        }

        static void ApplyInUse()
        {
            var d = data;
            if (d == null || world == null || d.inUsePlan == null) return;
            HoldRuleValues();
            int n = OzUeStatsApply.ApplyToWorld(world, d.inUsePlan);
            Applies++; UnitsRecopied += n;
            Debug.Log(Log + "stats: the playground uses " + Name(inUse) + " (" + d.inUsePlan.rowWrites + " row values, " + d.inUsePlan.ruleWrites + " rule values, " + n + " units on the field)");
        }

        /// <summary>The rule values a set changes are statics shared by every world: held once (OzUeStatics restores them when the editor
        /// closes, before any other level can start).</summary>
        static void HoldRuleValues()
        {
            if (held) return;
            held = true;
            OzUeStatics.Hold<double>("ZWorld.SniperHitChance", () => ZWorld.SniperHitChance, v => ZWorld.SniperHitChance = v, ZWorld.SniperHitChance);
            OzUeStatics.Hold<double>("ZWorld.SniperReload", () => ZWorld.SniperReload, v => ZWorld.SniperReload = v, ZWorld.SniperReload);
            OzUeStatics.Hold<int>("ZElectro.CycleTicks", () => ZElectro.CycleTicks, v => ZElectro.CycleTicks = v, ZElectro.CycleTicks);
            string pairs = UeRules.PairText(ZWorld.RiflePair);
            OzUeStatics.Hold<string>("ZWorld.RiflePair", () => UeRules.PairText(ZWorld.RiflePair), v => UeRules.SetPairs(ZWorld.RiflePair, v), pairs);
        }

        /// <summary>A unit the playground just made (OzUePlayground.Spawned): the values the game sets on the unit itself (the shuttle).</summary>
        internal static void OnSpawned(ZObject o)
        {
            var d = data;
            if (o == null || d == null || world == null || d.inUsePlan == null || !o.IsShuttle) return;
            if (OzUeStatsApply.Recopy(world, o, d.inUsePlan)) UnitsRecopied++;
        }

        static void RaiseChanged()
        {
            var h = Changed;
            if (h == null) return;
            try { h(); } catch (Exception e) { Debug.LogException(e); }
        }

        // ------------------------------------------------------------------ Custom
        public static string CustomName => D.customName;
        public static UeSet CustomBase => D.customBase;
        public static int CustomChangedCount => ChangedCount(null);

        internal static double CustomValue(string id, UeStat st)
        {
            var d = D;
            double v = d.custom.TryGetValue(id, out var a) ? a[(int)st] : double.NaN;
            if (double.IsNaN(v)) { var o = d.openz[id][(int)st]; v = o.HasNumber ? o.value : double.NaN; }
            return v;
        }

        /// <summary>The base set's value of an editable cell (the OpenZ value when the base set has none).</summary>
        internal static double BaseValue(string id, UeStat st)
        {
            var c = Cell(id, st, D.customBase);
            if (c.HasNumber) return c.value;
            var o = D.openz[id][(int)st];
            return o.HasNumber ? o.value : double.NaN;
        }

        internal static bool IsChanged(string id, UeStat st) => Editable(id, st) && !OzUeLiveStats.Same(CustomValue(id, st), BaseValue(id, st));

        /// <summary>Custom values that differ from the base set: of one unit (id) or of all (null).</summary>
        internal static int ChangedCount(string id)
        {
            int n = 0;
            foreach (var u in OzUeLiveStats.Units)
            {
                if (id != null && u != id) continue;
                for (int i = 0; i < (int)UeStat.Dps; i++) if (IsChanged(u, (UeStat)i)) n++;
            }
            return n;
        }

        /// <summary>A typed / stepped value (clamped and rounded to the stat's step). False when the cell is not editable.</summary>
        internal static bool SetCustom(string id, UeStat st, double v)
        {
            if (!Editable(id, st) || double.IsNaN(v) || double.IsInfinity(v)) return false;
            Slot(id)[(int)st] = OzUeLiveStats.Clamp(id, st, v);
            CustomEdits++;
            CustomChanged();
            return true;
        }

        internal static void ResetCustom(string id, UeStat st)
        {
            if (!Editable(id, st)) return;
            Slot(id)[(int)st] = BaseValue(id, st);
            CustomChanged(false);                                                                            // a value put back never switches the field's set (View/OzUeSets.Reset.cs)
        }

        /// <summary>"Custom from <set>": the Custom values of one unit = that set's (OpenZ's where it has none).</summary>
        internal static void CopyToCustom(string id, UeSet from)
        {
            var a = Slot(id);
            for (int i = 0; i < (int)UeStat.Dps; i++)
            {
                var st = (UeStat)i;
                if (!Editable(id, st)) continue;
                var c = Cell(id, st, from);
                a[i] = c.HasNumber ? c.value : D.openz[id][i].value;                            // a copy keeps the set's own value exactly
            }
            CustomChanged(from != D.customBase);                                                             // from the base set (Reset unit) = values put back: the field's set stays
        }

        /// <summary>Reset: the Custom values of one unit back to the base set.</summary>
        internal static void ResetUnit(string id) => CopyToCustom(id, D.customBase);

        static double[] Slot(string id)
        {
            var d = D;
            if (!d.custom.TryGetValue(id, out var a)) { a = NewSlot(); d.custom[id] = a; }
            return a;
        }

        static double[] NewSlot() { var a = new double[StatCount]; for (int i = 0; i < a.Length; i++) a[i] = double.NaN; return a; }

        /// <summary>A Custom value changed. edit = a new value (typed, stepped, copied from another set): the field takes the Custom set
        /// at once when another set was in use (its toast says so); a value put back to the base set (edit = false) never switches it.</summary>
        static void CustomChanged(bool edit = true)
        {
            var d = D;
            d.customPlan = MakePlan(d, UeSet.Custom);
            EditSwitched = edit && inUse != UeSet.Custom;                                                    // a Custom change is used at once: the field takes the Custom set (View/OzUeSets.Reset.cs)
            if (EditSwitched) { inUse = UeSet.Custom; PlayerPrefs.SetInt(KeySet, (int)UeSet.Custom); SwitchesToCustom++; }
            if (inUse == UeSet.Custom) { d.inUsePlan = d.customPlan; ApplyInUse(); }
            saveDirty = true; saveAt = Time.unscaledTime + 1f;
            RaiseChanged();
        }

        // ---- autosave (PlayerPrefs): "grunt.build=72;grunt.squad=3;..." of every editable value
        static void LoadCustom(Data d)
        {
            d.customName = PlayerPrefs.GetString(KeyName, DefaultName);
            if (string.IsNullOrEmpty(d.customName)) d.customName = DefaultName;
            int b = PlayerPrefs.GetInt(KeyBase, 0);
            d.customBase = b >= 0 && b <= 2 ? (UeSet)b : UeSet.OpenZ;
            string text = PlayerPrefs.GetString(KeyCustom, "");
            foreach (var id in OzUeLiveStats.Units)
            {
                var a = NewSlot();
                for (int i = 0; i < (int)UeStat.Dps; i++)
                    if (OzUeLiveStats.Editable(OzUeLiveStats.Route(id, (UeStat)i, d.rules, out _)) && d.openz[id][i].HasNumber) a[i] = d.openz[id][i].value;
                d.custom[id] = a;
            }
            int n = 0;
            foreach (var part in text.Split(';'))
            {
                int dot = part.IndexOf('.'), eq = part.IndexOf('=');
                if (dot <= 0 || eq <= dot) continue;
                string id = part.Substring(0, dot);
                if (!d.custom.TryGetValue(id, out var a) || !OzUeLiveStats.TryKey(part.Substring(dot + 1, eq - dot - 1), out var st)) continue;
                if (!OzUeLiveStats.Editable(OzUeLiveStats.Route(id, st, d.rules, out _))) continue;
                if (!double.TryParse(part.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || double.IsNaN(v) || double.IsInfinity(v)) continue;
                a[(int)st] = OzUeLiveStats.Limit(id, st, v); n++;
            }
            if (n > 0) Debug.Log(Log + "Custom stats \"" + d.customName + "\": " + n + " values from the last session, base " + Name(d.customBase));
        }

        static string CustomText(Data d)
        {
            var sb = new StringBuilder(4096);
            foreach (var id in OzUeLiveStats.Units)
            {
                if (!d.custom.TryGetValue(id, out var a)) continue;
                for (int i = 0; i < (int)UeStat.Dps; i++)
                {
                    if (double.IsNaN(a[i]) || !OzUeLiveStats.Editable(OzUeLiveStats.Route(id, (UeStat)i, d.rules, out _))) continue;
                    if (sb.Length > 0) sb.Append(';');
                    sb.Append(id).Append('.').Append(OzUeLiveStats.Defs[i].key).Append('=').Append(a[i].ToString("R", CultureInfo.InvariantCulture));
                }
            }
            return sb.ToString();
        }

        /// <summary>OzUeStatsRoot.Update: the autosave, at most once a second after a change.</summary>
        internal static void TickSave()
        {
            if (saveDirty && Time.unscaledTime >= saveAt) SaveCustomNow();
        }

        static void SaveCustomNow()
        {
            var d = data;
            if (d == null) return;
            if (saveDirty)
            {
                PlayerPrefs.SetString(KeyCustom, CustomText(d));
                PlayerPrefs.SetInt(KeyBase, (int)d.customBase);
                PlayerPrefs.SetString(KeyName, d.customName ?? DefaultName);
                PlayerPrefs.Save();
            }
            saveDirty = false;
        }

        // ---- the saved file (DESIGN_SPEC 10): name, based_on, changed, fields, units
        /// <summary>The spec-10 keys of a saved file (name, based_on, changed, fields, units) as a tree
        /// (Dictionary&lt;string, object&gt; / List&lt;object&gt; / double / string / bool).</summary>
        public static object CustomToTree()
        {
            var d = D;
            var changed = new List<object>();
            var units = new Dictionary<string, object>();
            foreach (var id in OzUeLiveStats.Units)
            {
                var vals = new Dictionary<string, object>();
                for (int i = 0; i < (int)UeStat.Dps; i++)
                {
                    var st = (UeStat)i;
                    if (!Editable(id, st)) continue;
                    double v = CustomValue(id, st);
                    if (double.IsNaN(v)) continue;
                    vals[OzUeLiveStats.Defs[i].key] = v;
                    if (IsChanged(id, st)) changed.Add(id + "." + OzUeLiveStats.Defs[i].key);
                }
                if (vals.Count > 0) units[id] = vals;
            }
            var fields = new Dictionary<string, object>();
            for (int i = 0; i < (int)UeStat.Dps; i++) fields[OzUeLiveStats.Defs[i].key] = StatLong((UeStat)i);
            return new Dictionary<string, object>
            {
                ["name"] = d.customName ?? DefaultName, ["based_on"] = FileKey(d.customBase), ["changed"] = changed, ["fields"] = fields, ["units"] = units,
            };
        }

        /// <summary>Loads Custom from such a tree (only known units / keys, finite numbers, clamped); sets the Custom name and base and
        /// switches the playground to Custom. message = the toast text. The tree is CustomToTree's own (no "format" key) or a whole saved
        /// file (OzUeFiles checks its format first); a "format" that names another kind of file is refused.</summary>
        public static bool CustomFromTree(object tree, out string message)
        {
            message = null;
            var o = tree as Dictionary<string, object>;
            if (o == null) { message = "THIS FILE IS NOT A STATS FILE (NOT JSON)."; return false; }
            bool formatOk = !o.TryGetValue("format", out var f) || (f is string fs && fs == FileFormat);
            if (!formatOk || !o.TryGetValue("units", out var uo) || !(uo is Dictionary<string, object> units))
            { message = "THIS FILE IS NOT AN OPENZ STATS FILE."; return false; }
            var d = D;
            int n = 0, m = 0;
            foreach (var kv in units)
            {
                if (!OzUeLiveStats.Known(kv.Key) || !(kv.Value is Dictionary<string, object> vals)) continue;
                bool any = false;
                foreach (var kv2 in vals)
                {
                    if (!OzUeLiveStats.TryKey(kv2.Key, out var st) || !Editable(kv.Key, st)) continue;
                    double v = Number(kv2.Value);
                    if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                    Slot(kv.Key)[(int)st] = OzUeLiveStats.Limit(kv.Key, st, v);
                    n++; any = true;
                }
                if (any) m++;
            }
            if (o.TryGetValue("name", out var nm) && nm is string ns && ns.Trim().Length > 0) d.customName = ns.Trim().Length > 60 ? ns.Trim().Substring(0, 60) : ns.Trim();
            if (o.TryGetValue("based_on", out var bo) && bo is string bs)
                foreach (var s in Sources) if (FileKey(s) == bs) d.customBase = s;
            bool newer = o.TryGetValue("version", out var ver) && Number(ver) > FileVersion;
            saveDirty = true; saveAt = Time.unscaledTime;
            d.customPlan = MakePlan(d, UeSet.Custom);
            Use(UeSet.Custom, true, true);
            message = ("LOADED \"" + d.customName + "\": " + n + " VALUES, " + m + " UNITS" + (newer ? " (NEWER FILE, KNOWN VALUES ONLY)" : "")).ToUpperInvariant();
            Debug.Log(Log + "Custom stats loaded: \"" + d.customName + "\", " + n + " values, " + m + " units, base " + Name(d.customBase));
            return true;
        }

        static double Number(object x)
        {
            switch (x)
            {
                case double dv: return dv;
                case float fv: return fv;
                case int iv: return iv;
                case long lv: return lv;
                case decimal mv: return (double)mv;
            }
            return double.NaN;
        }

        // ------------------------------------------------------------------ texts
        /// <summary>The tooltip of one value (DESIGN_SPEC 6.4): what it is, the set's value, the source's own words, the difference.</summary>
        internal static string ValueTip(string id, UeStat st, UeSet set)
        {
            var def = OzUeLiveStats.Def(st);
            var c = Cell(id, st, set);
            var sb = new StringBuilder(256);
            sb.Append(UnitName(id)).Append(" - ").Append(def.longText).Append(" (").Append(def.unitLong).Append(')');
            if (st == UeStat.Dps)
            {
                sb.Append('|').Append(Name(set)).Append(": ").Append(Text(st, c)).Append(c.HasNumber ? " HP/s" : "");
                return sb.ToString();
            }
            switch (c.kind)
            {
                case UeCellKind.NotInSet: sb.Append('|').Append(Name(set)).Append(": this unit is not in this set (OpenZ value is used in the playground)"); break;
                case UeCellKind.NotListed: sb.Append('|').Append(Name(set)).Append(": not listed in this source (OpenZ value is used in the playground)"); break;
                case UeCellKind.NotApplicable: sb.Append("|Does not apply to this unit"); break;
                case UeCellKind.NoRoll: sb.Append("|Shells and rockets have no hit roll: they fly to the aimed spot +-2 m"); break;
                default: sb.Append('|').Append(Name(set)).Append(": ").Append(Text(st, c)).Append(def.unit.Length > 0 ? " " + def.unit : ""); break;
            }
            if (set == UeSet.Custom && Route(id, st, out string rule) == UeRoute.Fixed) sb.Append("|Fixed by the game's rule: ").Append(rule);
            var n = Note(id, st);
            if (n != null)
            {
                if (set == UeSet.ZWiki && n.wiki != null) sb.Append("|Z-Wiki text: ").Append(n.wiki);
                if ((set == UeSet.OpenZ || set == UeSet.Custom) && n.play != null) sb.Append("|OpenZ in play: ").Append(n.play);
                if (n.diff != null) sb.Append("|Difference: ").Append(n.diff);
            }
            if (Differs(id, st, set))
            {
                var o = Cell(id, st, UeSet.OpenZ);
                int p = OzUeLiveStats.PctOf(c.value, o.value);
                sb.Append("|OpenZ: ").Append(Text(st, o));
                if (p != int.MinValue) sb.Append(" (").Append(p > 0 ? "+" : "").Append(p).Append(" % vs OpenZ)");
            }
            return sb.ToString();
        }

        /// <summary>The tooltip of a Custom input.</summary>
        internal static string InputTip(string id, UeStat st)
        {
            var def = OzUeLiveStats.Def(st);
            string who = st == UeStat.Squad ? "|For " + UnitName(id) + " squads placed from now on, both teams (squads on the field keep their size)"
                       : st == UeStat.Build ? "|Counts in the levels when you pick Custom on the level screen (nothing is built here)"
                       : "|Every " + UnitName(id) + " here takes it at once, both teams";                    // whom a Custom change reaches (View/OzUeSets.Reset.cs)
            return UnitName(id) + " - " + def.longText + " (" + def.unitLong + ")|Type a value: Enter applies, Esc goes back|Mouse wheel or arrow keys: 1 % steps, with Shift 10 %|Right-click: back to " + Name(D.customBase) + who;
        }

        /// <summary>The checks (OzUeStatsRoot.SelfCheck, the proof): the session's tables as text.</summary>
        internal static string DumpOpenZ() => OzUeLiveStats.Dump(D.openz);
        internal static OzUeSetData.Sets Shipped => D.shipped;
        internal static UeRules Rules => D.rules;
        internal static Dictionary<string, UeCell[]> OpenZTable => D.openz;
    }
}
