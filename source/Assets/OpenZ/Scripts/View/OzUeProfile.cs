using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the Unit editor, View/OzUeSets.cs): the player's SAVED Custom stats, "the Custom profile", the one the levels can play:
    /// the level screen offers UNIT STATS: OPENZ / CUSTOM only while a profile is saved, and OPENZ (the game's own numbers) stays the
    /// default there. SAVE writes it (every way of saving: SAVE, SHARE, SAVE AGAIN, SAVE STATS AND GO; View/OzUeFiles.cs) from the Custom
    /// values the editor holds at that moment; RESET STATS removes it (View/OzUeSets.Reset.cs). Kept in the game's settings store
    /// (PlayerPrefs), four keys:
    ///  oz_ue_profile          every editable Custom value: "grunt.build=72;grunt.hp=1081;..." (unit id, a dot, the stat's key, "=", the
    ///                         value in the Unit editor's own units: s, HP, m, m/s, %; invariant culture, round trip), the same format
    ///                         as the editor's own working copy oz_ue_custom; a unit or a stat it does not name keeps OpenZ's value;
    ///  oz_ue_profile_name     the Custom stats' name ("My custom stats", or the name of a loaded file);
    ///  oz_ue_profile_base     the set the values were based on (0 OpenZ, 1 ZodEngine, 2 Z-Wiki);
    ///  oz_ue_profile_changed  how many of its values differed from OpenZ's own numbers when it was saved (0 = it plays as OpenZ).
    /// A level start reads it with Values() (the editor's units by stat index, NaN where it names nothing) and builds its rule rows the
    /// way the playground does (View/OzUeStatsApply.cs Make, then ApplyToWorld). The working copy oz_ue_custom (written a second after
    /// every change) is NOT the profile: changes since the last SAVE stay in the Unit editor until they are saved or dropped.
    /// </summary>
    public static class OzUeProfile
    {
        public const string KeyValues = "oz_ue_profile", KeyName = "oz_ue_profile_name", KeyBase = "oz_ue_profile_base", KeyChanged = "oz_ue_profile_changed";
        const string Log = "[OpenZ UnitEditor] ";
        /// <summary>Checks: profiles written and removed since the game started.</summary>
        public static int Writes, Clears;

        /// <summary>A Custom profile is saved (the level screen offers CUSTOM).</summary>
        public static bool Saved => Text.Length > 0;

        /// <summary>The saved values as stored (empty = no profile).</summary>
        public static string Text { get { try { return PlayerPrefs.GetString(KeyValues, "") ?? ""; } catch (Exception) { return ""; } } }

        /// <summary>The saved Custom stats' name ("My custom stats" when none was given).</summary>
        public static string Name
        {
            get { string n = ""; try { n = PlayerPrefs.GetString(KeyName, ""); } catch (Exception) { } return string.IsNullOrEmpty(n) ? OzUeSets.DefaultName : n; }
        }

        /// <summary>The set the saved values were based on.</summary>
        public static UeSet Base { get { int b = 0; try { b = PlayerPrefs.GetInt(KeyBase, 0); } catch (Exception) { } return b >= 0 && b <= 2 ? (UeSet)b : UeSet.OpenZ; } }

        /// <summary>Values of the profile that differed from OpenZ's own numbers when it was saved.</summary>
        public static int ChangedFromOpenZ { get { int n = 0; try { n = PlayerPrefs.GetInt(KeyChanged, 0); } catch (Exception) { } return Math.Max(0, n); } }

        /// <summary>The saved values: unit id -> one value per stat (index = (int)UeStat), NaN where the profile names nothing. Empty when
        /// no profile is saved. Only known units and stat keys, finite numbers, kept inside each stat's limits.</summary>
        public static Dictionary<string, double[]> Values() => Parse(Text);

        /// <summary>The values of a profile text (see Values).</summary>
        public static Dictionary<string, double[]> Parse(string text)
        {
            var d = new Dictionary<string, double[]>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return d;
            foreach (var part in text.Split(';'))
            {
                int dot = part.IndexOf('.'), eq = part.IndexOf('=');
                if (dot <= 0 || eq <= dot) continue;
                string id = part.Substring(0, dot);
                if (!OzUeLiveStats.Known(id) || !OzUeLiveStats.TryKey(part.Substring(dot + 1, eq - dot - 1), out var st)) continue;
                if (!double.TryParse(part.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || double.IsNaN(v) || double.IsInfinity(v)) continue;
                if (!d.TryGetValue(id, out var a))
                {
                    a = new double[OzUeLiveStats.StatCount];
                    for (int i = 0; i < a.Length; i++) a[i] = double.NaN;
                    d[id] = a;
                }
                a[(int)st] = OzUeLiveStats.Limit(id, st, v);
            }
            return d;
        }

        /// <summary>The profile text of a set of values (every unit in the game's order, every stat but DPS that has a number).</summary>
        public static string Format(Dictionary<string, double[]> values)
        {
            var sb = new StringBuilder(4096);
            if (values == null) return "";
            foreach (var id in OzUeLiveStats.Units)
            {
                if (!values.TryGetValue(id, out var a) || a == null) continue;
                for (int i = 0; i < (int)UeStat.Dps && i < a.Length; i++)
                {
                    if (double.IsNaN(a[i]) || double.IsInfinity(a[i])) continue;
                    if (sb.Length > 0) sb.Append(';');
                    sb.Append(id).Append('.').Append(OzUeLiveStats.Defs[i].key).Append('=').Append(a[i].ToString("R", CultureInfo.InvariantCulture));
                }
            }
            return sb.ToString();
        }

        /// <summary>SAVE: the profile is these values (OzUeSets.SaveProfile).</summary>
        internal static void Write(string text, string name, UeSet basedOn, int changed)
        {
            PlayerPrefs.SetString(KeyValues, text ?? "");
            PlayerPrefs.SetString(KeyName, string.IsNullOrEmpty(name) ? OzUeSets.DefaultName : name);
            PlayerPrefs.SetInt(KeyBase, basedOn == UeSet.ZodEngine || basedOn == UeSet.ZWiki ? (int)basedOn : 0);
            PlayerPrefs.SetInt(KeyChanged, Math.Max(0, changed));
            PlayerPrefs.Save();
            Writes++;
            Debug.Log(Log + "Custom profile saved for the levels (" + KeyValues + "): \"" + Name + "\", " + changed + " value(s) differ from OpenZ");
        }

        /// <summary>RESET STATS: no profile any more (the levels play OpenZ, the level screen offers no CUSTOM).</summary>
        internal static void Clear()
        {
            bool had = Saved;
            PlayerPrefs.DeleteKey(KeyValues); PlayerPrefs.DeleteKey(KeyName); PlayerPrefs.DeleteKey(KeyBase); PlayerPrefs.DeleteKey(KeyChanged);
            PlayerPrefs.Save();
            Clears++;
            Debug.Log(Log + "Custom profile removed (" + KeyValues + (had ? "" : ": there was none") + "): the levels play OpenZ");
        }

        // ------------------------------------------------------------------ the Play proof: the profile before a run, put back after it
        internal sealed class Snap { public bool had; public string text, name; public int basedOn, changed; }

        internal static Snap Take()
        {
            var s = new Snap();
            try
            {
                s.had = PlayerPrefs.HasKey(KeyValues);
                s.text = PlayerPrefs.GetString(KeyValues, ""); s.name = PlayerPrefs.HasKey(KeyName) ? PlayerPrefs.GetString(KeyName, "") : null;
                s.basedOn = PlayerPrefs.GetInt(KeyBase, -1); s.changed = PlayerPrefs.GetInt(KeyChanged, -1);
            }
            catch (Exception) { }
            return s;
        }

        internal static void Put(Snap s)
        {
            if (s == null) return;
            if (s.had) PlayerPrefs.SetString(KeyValues, s.text ?? ""); else PlayerPrefs.DeleteKey(KeyValues);
            if (s.name != null) PlayerPrefs.SetString(KeyName, s.name); else PlayerPrefs.DeleteKey(KeyName);
            if (s.basedOn >= 0) PlayerPrefs.SetInt(KeyBase, s.basedOn); else PlayerPrefs.DeleteKey(KeyBase);
            if (s.changed >= 0) PlayerPrefs.SetInt(KeyChanged, s.changed); else PlayerPrefs.DeleteKey(KeyChanged);
            PlayerPrefs.Save();
        }
    }
}
