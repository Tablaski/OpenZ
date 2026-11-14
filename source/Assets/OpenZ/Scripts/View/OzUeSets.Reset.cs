// 1.2 item 6, View/OzUeSets.cs: what the player's Custom stats do, in plain terms, and the words that say it.
//  * The field has ONE set in use for every unit, both teams. A Custom value belongs to a unit type (the jeep, the grunt ...): a change
//    is used at once by every unit of that type on the field (red, blue and empty ones) and by every one placed later; the squad size
//    only by squads placed after it (the robots of a squad on the field stay). A Custom change made while another set is in use switches
//    the field to Custom (OzUeSets.cs CustomChanged), so a change is always seen at once; putting values back to the base set (a
//    right-click on one value, Reset unit, "Custom from" the base set) never switches the field's set.
//  * SAVE keeps the Custom values as the Custom profile (View/OzUeProfile.cs), the one the levels play when the player picks CUSTOM on
//    the level screen (OPENZ, the game's own numbers, is the default there). Changes since the last SAVE stay in the Unit editor (its
//    working copy, autosaved) until they are saved, or dropped by GO WITHOUT SAVING.
//  * RESET STATS: every Custom value back to OpenZ's own numbers, the field on OpenZ, the saved profile removed (the file is not touched).
//  * The words: a picked unit's name label "LASER  1868 / 2027 HP  ·  CUSTOM", the hover card's foot, the toast after an edit, the
//    inspector's line by the Custom column. A unit the set in use does not list (the Electrobot, the heroes, the shuttle in ZodEngine
//    and Z-Wiki) runs on OpenZ's values, and its words say so.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public static partial class OzUeSets
    {
        /// <summary>Checks: RESET STATS done, profiles saved, unsaved changes dropped, Custom changes that switched the field to Custom.</summary>
        public static int ResetAlls, ProfileSaves, Drops, SwitchesToCustom;
        /// <summary>The last Custom change switched the field from another set to Custom (its toast says so).</summary>
        public static bool EditSwitched;

        /// <summary>Custom values (every unit, every editable stat) that differ from OpenZ's own numbers now.</summary>
        public static int CustomDiffFromOpenZ
        {
            get
            {
                var d = D; int n = 0;
                foreach (var id in OzUeLiveStats.Units)
                    for (int i = 0; i < (int)UeStat.Dps; i++)
                    {
                        var st = (UeStat)i;
                        if (!Editable(id, st)) continue;
                        var o = d.openz[id][i];
                        if (o.HasNumber && !OzUeLiveStats.Same(CustomValue(id, st), o.value)) n++;
                    }
                return n;
            }
        }

        /// <summary>RESET STATS would change something: another set is in use, a Custom value differs from OpenZ's, the Custom values are
        /// based on another set (a loaded file), or a Custom profile is saved for the levels.</summary>
        public static bool AnythingToReset => InUse != UeSet.OpenZ || CustomBase != UeSet.OpenZ || CustomDiffFromOpenZ > 0 || OzUeProfile.Saved;

        /// <summary>RESET STATS: every editable Custom value = OpenZ's own number (the game's default stats), based on OpenZ, the default
        /// name; the field uses the OpenZ set again (remembered); the saved Custom profile is removed (the levels play OpenZ). Autosaved
        /// at once. The file is not touched. Returns how many values changed.</summary>
        public static int ResetAll()
        {
            var d = D;
            int n = CustomDiffFromOpenZ;
            foreach (var id in OzUeLiveStats.Units)
            {
                var a = Slot(id);
                for (int i = 0; i < (int)UeStat.Dps; i++)
                {
                    var o = d.openz[id][i];
                    a[i] = Editable(id, (UeStat)i) && o.HasNumber ? o.value : double.NaN;                       // as a first session without saved values
                }
            }
            d.customBase = UeSet.OpenZ; d.customName = DefaultName;
            saveDirty = true; saveAt = Time.unscaledTime;
            d.customPlan = MakePlan(d, UeSet.Custom);
            OzUeProfile.Clear();
            ResetAlls++;
            Use(UeSet.OpenZ, true, true);
            Debug.Log(Log + "RESET STATS: " + n + " Custom value(s) back to OpenZ's own numbers, base OpenZ, the field uses OpenZ, the saved Custom profile removed");
            return n;
        }

        // ------------------------------------------------------------------ the saved profile (View/OzUeProfile.cs)
        /// <summary>SAVE (View/OzUeFiles.cs, once the file is written): the Custom values now become the saved Custom profile.</summary>
        public static void SaveProfile()
        {
            var d = D;
            OzUeProfile.Write(CustomText(d), d.customName, d.customBase, CustomDiffFromOpenZ);
            ProfileSaves++;
        }

        static string cachedText; static Dictionary<string, double[]> cachedValues;

        static Dictionary<string, double[]> SavedValues()
        {
            string t = OzUeProfile.Text;
            if (cachedValues == null || t != cachedText) { cachedText = t; cachedValues = OzUeProfile.Parse(t); }
            return cachedValues;
        }

        /// <summary>The saved profile's value of an editable cell (OpenZ's where the profile names none).</summary>
        static double SavedValue(Dictionary<string, double[]> saved, string id, UeStat st)
        {
            if (saved != null && saved.TryGetValue(id, out var a) && !double.IsNaN(a[(int)st])) return a[(int)st];
            var o = D.openz[id][(int)st];
            return o.HasNumber ? o.value : double.NaN;
        }

        /// <summary>Custom values not saved yet: those that differ from the saved profile (from OpenZ's own numbers when none is saved).</summary>
        public static int UnsavedCount
        {
            get
            {
                if (!OzUeProfile.Saved) return CustomDiffFromOpenZ;
                var saved = SavedValues(); int n = 0;
                foreach (var id in OzUeLiveStats.Units)
                    for (int i = 0; i < (int)UeStat.Dps; i++)
                    {
                        var st = (UeStat)i;
                        if (!Editable(id, st)) continue;
                        double s = SavedValue(saved, id, st), v = CustomValue(id, st);
                        if (!double.IsNaN(s) && !OzUeLiveStats.Same(v, s)) n++;
                    }
                return n;
            }
        }

        /// <summary>GO WITHOUT SAVING (after its confirm): the Custom values go back to the saved profile (to OpenZ's own numbers when none
        /// is saved), its name and base too; the field takes them at once when it uses Custom. Written to the working copy now. Returns
        /// how many values were dropped.</summary>
        public static int DropUnsaved()
        {
            var d = D;
            int n = UnsavedCount;
            bool saved = OzUeProfile.Saved;
            var values = saved ? SavedValues() : null;
            foreach (var id in OzUeLiveStats.Units)
            {
                var a = Slot(id);
                for (int i = 0; i < (int)UeStat.Dps; i++)
                {
                    var st = (UeStat)i;
                    var o = d.openz[id][i];
                    if (!Editable(id, st) || !o.HasNumber) { a[i] = double.NaN; continue; }
                    a[i] = saved ? SavedValue(values, id, st) : o.value;
                }
            }
            d.customBase = saved ? OzUeProfile.Base : UeSet.OpenZ;
            d.customName = saved ? OzUeProfile.Name : DefaultName;
            d.customPlan = MakePlan(d, UeSet.Custom);
            if (inUse == UeSet.Custom) { d.inUsePlan = d.customPlan; ApplyInUse(); }
            saveDirty = true; saveAt = Time.unscaledTime;
            SaveCustomNow();
            Drops++;
            RaiseChanged();
            Debug.Log(Log + "GO WITHOUT SAVING: " + n + " unsaved Custom value(s) dropped, the Custom stats are " + (saved ? "the saved profile \"" + d.customName + "\"" : "OpenZ's own numbers (no profile saved)") + " again");
            return n;
        }

        // ------------------------------------------------------------------ the set each unit runs
        /// <summary>The set a unit of this type runs on the field now: the set in use, or OpenZ when that set does not list the unit
        /// (fallback = true; its values come from OpenZ).</summary>
        public static UeSet RunningFor(string id, out bool fallback)
        {
            var s = InUse; fallback = false;
            if ((s == UeSet.ZodEngine || s == UeSet.ZWiki) && id != null && OzUeLiveStats.Known(id) && Cell(id, UeStat.Health, s).kind == UeCellKind.NotInSet) { fallback = true; return UeSet.OpenZ; }
            return s;
        }

        static readonly string[] labelTags = new string[8];
        static readonly Dictionary<string, string> cardFeet = new Dictionary<string, string>();

        static int TagKey(string id, out UeSet run, out bool fallback) { run = RunningFor(id, out fallback); return (int)InUse * 2 + (fallback ? 1 : 0); }

        /// <summary>A set's name in the world labels' capitals: OPENZ, ZODENGINE, Z-WIKI, CUSTOM.</summary>
        public static string Caps(UeSet set) => Name(set).ToUpperInvariant();

        /// <summary>The picked unit's name label's tail: "  ·  CUSTOM" in the set's colour ("  ·  OPENZ (NOT IN Z-WIKI)" for a unit that set
        /// does not list); cached, no new string per frame.</summary>
        public static string LabelTag(string id)
        {
            int k = TagKey(id, out var run, out bool fb);
            if (k < 0 || k >= labelTags.Length) return "";
            return labelTags[k] ??= "  ·  " + OzUeGfx.Col(Caps(run) + (fb ? " (NOT IN " + Caps(InUse) + ")" : ""), Colour(run));
        }

        /// <summary>The hover card's foot: "Custom stats: every LASER here runs them." (the set's words in its colour); cached.</summary>
        public static string CardFoot(string id)
        {
            int k = TagKey(id, out var run, out bool fb);
            string key = k + "|" + id;
            if (cardFeet.TryGetValue(key, out var s)) return s;
            s = OzUeGfx.Col(Name(run) + " stats", Colour(run)) + (fb ? " (not in " + Name(InUse) + ")" : "") + ": every " + UnitName(id) + " here runs them.";
            cardFeet[key] = s;
            return s;
        }

        // ------------------------------------------------------------------ the words of a Custom change
        /// <summary>The toast after a Custom value was typed (back = put back to the base set's value by a right-click): every unit of the
        /// type takes it at once, both teams; the squad size counts for squads placed from now on; the build time counts in the levels
        /// (nothing is built here); a change that switched the field to Custom says so, a value put back never switches it (the words:
        /// UI/OzUePlan.cs EditToast).</summary>
        public static string EditToast(string id, UeStat st, bool back)
        {
            string v = OzUeLiveStats.Format(st, CustomValue(id, st)), unit = StatUnit(st);
            return OzUePlan.EditToast(UnitName(id), StatName(st), v + (unit.Length > 0 ? " " + unit : ""), st == UeStat.Squad, st == UeStat.Build, back, Name(CustomBase), EditSwitched);
        }

        /// <summary>The toast of a whole unit's Custom values changed in the inspector ("Custom from ZodEngine", Reset unit).</summary>
        public static string UnitToast(string id, string what) => OzUePlan.UnitToast(UnitName(id), what, EditSwitched);
    }
}
