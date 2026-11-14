// 1.2 item 6, View/OzUnitEditor.cs: the shipped ZodEngine and Z-Wiki stats sets (Resources/OpenZ/UnitEditor/unit_stat_sets.json, made by
// the kit tool tools/p3_build_sets.py). ZodEngine ships as the Zod engine's own rule-table rows (its default settings, the 17 units of Z),
// turned into player units here by the SAME code as the OpenZ set (OzUeLiveStats.Cells) with every OpenZ rule off, so the two sets differ
// only where the rules differ. Z-Wiki ships as the fan wiki's numbers of the original Z in engine units. Plus the short notes per value
// (the wiki's own words, how OpenZ plays it, the kind of difference). Read with the engine's JsonUtility, as the Sim reads its maps; no
// other UnityEngine use (the kit's headless check reads it too).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenZ.Sim;

namespace OpenZ
{
    [Serializable]
    public sealed class UeSetFile
    {
        public string format;
        public int version;
        public string[] stats;          // the keys of the Z-Wiki value arrays, in their order (build, squad, hp, ...)
        public string[] zod_fields;     // the rule-table fields of the ZodEngine rows, in their order (group_amount, move_speed, ...)
        public UeSetUnit[] units;
    }

    [Serializable]
    public sealed class UeSetUnit
    {
        public string id;
        public double[] zod_row;        // the Zod engine's rule-table row of this unit; empty = not in ZodEngine
        public string[] wiki;           // one text per stat key: a number, "na", "noroll" or "" (not listed); empty = not in Z-Wiki
        public UeSetNote[] notes;
    }

    [Serializable]
    public sealed class UeSetNote
    {
        public string stat, wiki, play, diff;
    }

    /// <summary>One value's notes: the Z-Wiki's own words, how OpenZ plays it, the kind of difference.</summary>
    public sealed class UeNote { public string wiki, play, diff; }

    public static class OzUeSetData
    {
        public const string Format = "openz-unit-stat-sets";
        public const string ResourcePath = "OpenZ/UnitEditor/unit_stat_sets";

        public sealed class Sets
        {
            public readonly Dictionary<string, UeCell[]> zod = new Dictionary<string, UeCell[]>(), wiki = new Dictionary<string, UeCell[]>();
            public readonly Dictionary<string, Dictionary<UeStat, UeNote>> notes = new Dictionary<string, Dictionary<UeStat, UeNote>>();
            /// <summary>The Zod engine's rows as a settings text (the checks compare them with the game's own rows).</summary>
            public string zodSettings = "";
            public string error;
            public int values;
        }

        /// <summary>Reads the shipped sets. A unit missing from a set gets "not in this set" cells; DPS is worked out from the set's
        /// own damage, reload and hit chance.</summary>
        public static Sets Parse(string json)
        {
            var s = new Sets();
            UeSetFile f = null;
            try { f = string.IsNullOrEmpty(json) ? null : UnityEngine.JsonUtility.FromJson<UeSetFile>(json); }
            catch (Exception e) { s.error = "not readable: " + e.Message; }
            if (f == null || f.format != Format || f.stats == null || f.zod_fields == null || f.units == null)
            {
                if (s.error == null) s.error = "not a stats sets file";
                f = null;
            }
            // ---- ZodEngine: the rows as a settings text, parsed like the game's own (ZSettings.Parse: C atoi for the int fields, the censor)
            var sb = new StringBuilder(4096);
            var inZod = new HashSet<string>();
            if (f != null)
                foreach (var u in f.units)
                {
                    if (u == null || u.zod_row == null || u.zod_row.Length == 0 || u.zod_row.Length != f.zod_fields.Length || !OzUeLiveStats.Known(u.id)) continue;
                    inZod.Add(u.id);
                    for (int i = 0; i < f.zod_fields.Length; i++)
                        sb.Append("unit.").Append(u.id).Append('.').Append(f.zod_fields[i]).Append('=').Append(u.zod_row[i].ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                }
            s.zodSettings = sb.ToString();
            var zs = ZGatlingBuff.ParseZod(s.zodSettings);                           // DV147 / DV170: the ZodEngine set reads Zod's mech and Zod's gatling whatever the switches (Sim/ZMechHp.cs, Sim/ZGatlingBuff.cs)
            var zr = UeRules.ZodRules();
            // ---- Z-Wiki: value texts by the file's stat keys
            var keys = new UeStat[f != null ? f.stats.Length : 0];
            var known = new bool[keys.Length];
            for (int i = 0; i < keys.Length; i++) known[i] = OzUeLiveStats.TryKey(f.stats[i], out keys[i]);
            foreach (var id in OzUeLiveStats.Units)
            {
                UeSetUnit u = null;
                if (f != null) foreach (var x in f.units) if (x != null && x.id == id) { u = x; break; }
                if (inZod.Contains(id))
                {
                    var c = OzUeLiveStats.Cells(zs, zr, id);
                    foreach (var x in c) if (x.HasNumber) s.values++;
                    s.zod[id] = c;
                }
                else s.zod[id] = NotInSet();
                s.wiki[id] = WikiCells(u != null ? u.wiki : null, keys, known, s);
                var n = new Dictionary<UeStat, UeNote>();
                if (u != null && u.notes != null)
                    foreach (var x in u.notes)
                        if (x != null && OzUeLiveStats.TryKey(x.stat, out var st))
                            n[st] = new UeNote { wiki = Empty(x.wiki), play = Empty(x.play), diff = Empty(x.diff) };
                s.notes[id] = n;
            }
            return s;
        }

        static string Empty(string t) => string.IsNullOrEmpty(t) ? null : t;

        static UeCell[] NotInSet()
        {
            var c = new UeCell[OzUeLiveStats.StatCount];
            for (int i = 0; i < c.Length; i++) c[i] = UeCell.Of(UeCellKind.NotInSet);
            c[(int)UeStat.Dps] = UeCell.Of(UeCellKind.NotApplicable);
            return c;
        }

        static UeCell[] WikiCells(string[] vals, UeStat[] keys, bool[] known, Sets s)
        {
            if (vals == null || vals.Length == 0) return NotInSet();
            var c = new UeCell[OzUeLiveStats.StatCount];
            for (int i = 0; i < c.Length; i++) c[i] = UeCell.Of(UeCellKind.NotListed);
            for (int i = 0; i < keys.Length && i < vals.Length; i++)
            {
                if (!known[i]) continue;
                var v = vals[i] ?? "";
                if (v == "na") c[(int)keys[i]] = UeCell.Of(UeCellKind.NotApplicable);
                else if (v == "noroll") c[(int)keys[i]] = UeCell.Of(UeCellKind.NoRoll);
                else if (v.Length > 0 && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && !double.IsNaN(d) && !double.IsInfinity(d))
                { c[(int)keys[i]] = UeCell.Num(d); s.values++; }
            }
            c[(int)UeStat.Dps] = OzUeLiveStats.DpsCell(c[(int)UeStat.Damage], c[(int)UeStat.Reload], c[(int)UeStat.Hit]);
            return c;
        }
    }
}
