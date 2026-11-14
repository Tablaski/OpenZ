// OpenZ (GPL-3.0) - version 1.2, owner item 183: the medium tank in every unit list, card, table and text, counted in the editor.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.2 owner item 183 : the MEDIUM TANK (type mtank, DV145, Sim/ZMTank.cs) wherever the game lists, names or describes
    /// is a counted PASS / FAIL; INFO lines are known gaps that are not failures:
    ///  1. every unit list of the game (the static id arrays of the Unit editor, the testing panel, the voices, the repair lines, the pictures,
    ///     the checks) that holds both the light and the heavy tank also holds the medium tank;
    ///  2. the build menus: every fort / vehicle factory list of levels 0-5 that has the mech has the medium tank right after it;
    ///  3. its names (MEDIUM TANK / MEDIUM / MT / CANNON), its build-menu card words and numbers (the card's HP = the rules' HP);
    ///  4. its rules row in the default settings (its own HP, the light tank's gun) and its Unit editor hover notes;
    ///  5. its pictures: build-menu icons and unit card portraits (red and blue), the production badge faces x1..x4;
    ///  6. its voice pools: select / move / attack / repair / Electrobot stun events, every clip loads (mono 44.1 kHz);
    ///  7. the words: CHANGES &amp; UPDATES (its own card, the CPU repair, driver and Mech cards) and the Credits' original content list.
    /// </summary>
    public static class OzV12MTankEverywhereCount
    {
        public const string Menu = "OpenZ/V1.2/Medium Tank Everywhere Check (183)";
        const string T = "[OzV12MTankEverywhereCount] ";
        static readonly string M = ZMTank.Type;

#pragma warning disable 0649   // filled by JsonUtility
        [Serializable] class Card { public string title, in_z, now; }
        [Serializable] class Section { public string heading; public Card[] entries; }
        [Serializable] class Changes { public Section[] sections; }
        [Serializable] class Note { public string stat, wiki, play, diff; }
        [Serializable] class SetUnit { public string id; public Note[] notes; }
        [Serializable] class Sets { public string[] stats; public SetUnit[] units; }
#pragma warning restore 0649

        [MenuItem(Menu)]
        public static void Run()
        {
            var sb = new StringBuilder(); int pass = 0, fail = 0, info = 0;
            void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.AppendLine((ok ? "PASS " : "FAIL ") + what); }
            void Info(string what) { info++; sb.AppendLine("INFO " + what); }
            bool was = ZWorld.MTankDeviation;
            try
            {
                Lists(Check, Info);
                BuildMenus(Check);
                NamesAndCard(Check, sb);
                RulesAndNotes(Check);
                Pictures(Check);
                Voices(Check, Info);
                Words(Check, Info);
            }
            catch (Exception e) { Check(false, "the check threw " + e.GetType().Name + ": " + e.Message); }
            finally { ZWorld.MTankDeviation = was; }
            string head = T + (fail == 0 ? "PASS " : "FAIL ") + pass + "/" + (pass + fail) + " checks" + (info > 0 ? ", " + info + " INFO" : "");
            sb.Insert(0, "RESULT: " + (fail == 0 ? "PASS" : "FAIL") + "\n" + head + "\n\n");
            string path = null;
            try
            {
                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../..", "work", "records"));
                Directory.CreateDirectory(dir); path = Path.Combine(dir, "medium_tank_everywhere_check.md");
                File.WriteAllText(path, sb.ToString());
            }
            catch (Exception e) { sb.AppendLine("could not write the report: " + e.Message); }
            if (fail == 0) Debug.Log(sb.ToString()); else Debug.LogWarning(sb.ToString());
            Debug.Log(head + (path != null ? "; report " + path : ""));
        }

        // ------------------------------------------------------------------ 1. every unit list
        static readonly string[] ListTypes =
        {
            "OpenZ.OzUeLiveStats", "OpenZ.OzUeTable", "OpenZ.OzUeStatsList", "OpenZ.OzUePlayground", "OpenZ.OzCheatSpawner", "OpenZ.UnitVoices",
            "OpenZ.OzRepairLines", "OpenZ.UnitArt", "OpenZ.GateBattery", "OpenZ.OzBluePicturesCheck",
        };

        static Type Find(string name)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType(name, false);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>Arrays that hold tanks but are not lists of the units: told as INFO, never counted.</summary>
        static readonly Dictionary<string, string> NotAUnitList = new Dictionary<string, string>
        {
            { "OzUePlayground.BenchMix", "a sample army for the Unit editor's cap bench (a repeating mix of 16, not the unit list)" },
        };

        static string Id(string s) { int at = s.IndexOf('@'); return at >= 0 ? s.Substring(0, at) : s; }   // the testing panel's "light@v1" tiles

        static void Lists(Action<bool, string> check, Action<string> info)
        {
            int lists = 0, held = 0;
            foreach (var tn in ListTypes)
            {
                var t = Find(tn);
                if (t == null) { check(false, "unit lists: the type " + tn + " is not in the project"); continue; }
                foreach (var f in t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var rows = new List<(string name, string[] ids)>();
                    if (f.FieldType == typeof(string[])) { if (f.GetValue(null) is string[] a) rows.Add((f.Name, a)); }
                    else if (f.FieldType == typeof(string[][]) && f.GetValue(null) is string[][] g) for (int i = 0; i < g.Length; i++) if (g[i] != null) rows.Add((f.Name + "[" + i + "]", g[i]));
                    foreach (var (name, ids) in rows)
                    {
                        var n = ids.Where(x => x != null).Select(Id).ToList();
                        if (!n.Contains("light") || !n.Contains("heavy")) continue;      // not a list of the tanks
                        if (NotAUnitList.TryGetValue(t.Name + "." + name, out var why)) { info("not a unit list: " + t.Name + "." + name + ", " + why); continue; }
                        lists++;
                        bool has = n.Contains(M); if (has) held++;
                        check(has, "unit list " + t.Name + "." + name + " (" + n.Count + " entries) holds the medium tank" + (has ? " at " + n.IndexOf(M) : ""));
                    }
                }
            }
            check(lists >= 11 && held == lists, "unit lists counted: " + held + " of " + lists + " lists of the tanks hold the medium tank (11 lists expected: the Unit editor's 5, the testing panel, the voices, the repair lines, the pictures, 2 checks)");
        }

        // ------------------------------------------------------------------ 2. the build menus
        static void BuildMenus(Action<bool, string> check)
        {
            ZWorld.MTankDeviation = true;
            int withMech = 0, right = 0, stray = 0;
            foreach (var b in new[] { "fort_front", "fort_back", "vehicle_factory" })
                for (int lv = 0; lv <= 5; lv++)
                {
                    var l = ZBuildLists.For(b, lv).ToList(); int i = l.IndexOf("medium"), j = l.IndexOf(M);
                    if (i >= 0) { withMech++; if (j == i + 1) right++; }
                    else if (j >= 0) stray++;
                }
            check(withMech > 0 && right == withMech && stray == 0, "build menus: " + right + " of " + withMech + " fort / vehicle factory lists with the mech show the medium tank right after it; " + stray + " lists without the mech show it");
            var robot = Enumerable.Range(0, 6).Count(lv => ZBuildLists.For("robot_factory", lv).Contains(M));
            check(robot == 0, "build menus: no robot factory list has it (" + robot + ")");
            ZWorld.MTankDeviation = false;
            int zod = 0; foreach (var b in new[] { "fort_front", "fort_back", "vehicle_factory" }) for (int lv = 0; lv <= 5; lv++) if (ZBuildLists.For(b, lv).Contains(M)) zod++;
            check(zod == 0, "build menus in 'Zod exact': no list has it (" + zod + ")");
        }

        // ------------------------------------------------------------------ 3. names, card words and numbers
        static void NamesAndCard(Action<bool, string> check, StringBuilder sb)
        {
            check(UnitInfo.Name(M) == "MEDIUM TANK" && UnitInfo.Short(M) == "MEDIUM" && UnitInfo.Code(M) == "MT" && UnitInfo.WeaponOfType(M) == "CANNON",
                  "names: " + UnitInfo.Name(M) + " / " + UnitInfo.Short(M) + " / " + UnitInfo.Code(M) + " / " + UnitInfo.WeaponOfType(M));
            var w = OzUnitFacts.WordsOf(M); var lw = OzUnitFacts.WordsOf("light"); var hw = OzUnitFacts.WordsOf("heavy");
            bool words = w != null && w.role.Length > 0 && w.plus.Length > 0 && w.minus.Length > 0;
            int longest = new[] { lw, hw }.Where(x => x != null).SelectMany(x => new[] { x.role.Length, x.plus.Length, x.minus.Length }).DefaultIfEmpty(40).Max();
            check(words && Math.Max(w.role.Length, Math.Max(w.plus.Length, w.minus.Length)) <= Math.Max(longest, 40),
                  "build-menu card words: \"" + w?.role + "\" / \"" + w?.plus + "\" / \"" + w?.minus + "\" (no longer than the light and heavy tanks' lines, " + longest + " letters)");
            var ta = Resources.Load<TextAsset>("OpenZ/default_settings");
            var s = ta ? ZSettings.Parse(ta.text) : null; var u = s?.Unit(M);
            var c = s != null ? OzUnitFacts.For(s, M) : null;
            check(c != null && u != null && c.hpValue == u.MaxHealth && c.role == w?.role,
                  "build-menu card numbers: HP " + c?.hp + " (rules " + u?.MaxHealth + "), DAMAGE " + c?.damage + ", RANGE " + c?.range + ", bars " + c?.hpBar.ToString("0.00") + " / " + c?.damageBar.ToString("0.00") + " / " + c?.rangeBar.ToString("0.00"));
        }

        // ------------------------------------------------------------------ 4. the rules row, the Unit editor's notes
        static void RulesAndNotes(Action<bool, string> check)
        {
            var ta = Resources.Load<TextAsset>("OpenZ/default_settings");
            var s = ta ? ZSettings.Parse(ta.text) : null;
            var u = s?.Unit(M); var light = s?.Unit("light"); var mech = s?.Unit("medium"); var heavy = s?.Unit("heavy");
            bool own = ta && ta.text.Contains("unit." + M + ".health=");
            check(u != null && own && u.MaxHealth > (light?.MaxHealth ?? 0) && u.MaxHealth < (heavy?.MaxHealth ?? 0),
                  "rules row: its own unit." + M + ".* rows; " + u?.MaxHealth + " HP (light " + light?.MaxHealth + ", mech " + mech?.MaxHealth + ", heavy " + heavy?.MaxHealth + ")");
            check(u != null && light != null && u.attack_damage == light.attack_damage && Math.Abs(u.attack_speed - ZMTank.Reload(light.attack_speed)) < 1e-9 && u.attack_radius == light.attack_radius,
                  "rules row: the light tank's cannon (damage " + u?.attack_damage + ", range " + u?.attack_radius + " px), its own reload " + u?.attack_speed + " s = the light tank's " + light?.attack_speed + " s x " + ZMTank.ReloadFactor + " (DV156); speed " + u?.move_speed + " (mech " + mech?.move_speed + ")");
            var st = Resources.Load<TextAsset>("OpenZ/UnitEditor/unit_stat_sets");
            var sets = st ? JsonUtility.FromJson<Sets>(st.text) : null;
            var mu = sets?.units?.FirstOrDefault(x => x != null && x.id == M);
            var have = mu?.notes?.Where(x => x != null).Select(x => x.stat).ToList() ?? new List<string>();
            var want = new[] { "build", "hp", "dmg", "reload", "range", "blast", "shell", "speed" };
            check(mu != null && want.All(have.Contains) && mu.notes.All(x => !string.IsNullOrEmpty(x.play)),
                  "Unit editor notes: " + string.Join(", ", have) + " (want " + string.Join(", ", want) + ", each with its OpenZ words)");
        }

        // ------------------------------------------------------------------ 5. pictures
        static void Pictures(Action<bool, string> check)
        {
            foreach (var team in new[] { Team.Red, Team.Blue })
            {
                string tn = TeamColors.Key(team);
                var icon = UnitArt.Icon(M, team); var por = UnitArt.VehiclePortrait(M, team);
                check(icon && icon.name.Contains(M) && icon.name.Contains(tn), "build-menu icon " + tn + ": " + (icon ? icon.name + " " + icon.width + "x" + icon.height : "none"));
                check(por && por.name.Contains(M), "unit card portrait " + tn + ": " + (por ? por.name + " " + por.width + "x" + por.height : "none"));
                for (int t = 1; t <= OzBadgeArt.MaxTier; t++)
                {
                    var f = Resources.Load<Texture2D>(OzBadgeArt.Root + t + "/face_" + M + "_" + tn);
                    check(f, "production badge face x" + t + " " + tn + ": " + (f ? f.name : "none"));
                }
            }
        }

        // ------------------------------------------------------------------ 6. voices
        static void Voices(Action<bool, string> check, Action<string> info)
        {
            check(UnitVoices.Types.Contains(M) && OzRepairLines.Types.Contains(M), "voice types: UnitVoices " + UnitVoices.Types.Contains(M) + ", repair lines " + OzRepairLines.Types.Contains(M));
            var ta = Resources.Load<TextAsset>(AudioDirector.TablePath);
            var table = ta ? JsonUtility.FromJson<SoundTable>(ta.text) : null;
            var ev = new Dictionary<string, SoundEventDef>();
            if (table?.events != null) foreach (var e in table.events) if (e != null && e.id != null) ev[e.id] = e;
            void Pool(string id, int min, string[] lines)
            {
                if (!ev.TryGetValue(id, out var e) || e.clips == null) { check(false, "voice pool " + id + ": not in the sound table"); return; }
                int load = 0, fmt = 0; var said = new HashSet<string>();
                foreach (var cr in e.clips)
                {
                    var clip = Resources.Load<AudioClip>(cr.res);
                    if (clip) { load++; if (clip.channels == 1 && clip.frequency == 44100) fmt++; }
                    if (!string.IsNullOrEmpty(cr.line)) said.Add(cr.line);
                }
                bool all = lines == null || lines.All(said.Contains);
                check(e.clips.Length >= min && load == e.clips.Length && fmt == load && all && e.channel == "alan_main",
                      "voice pool " + id + ": " + load + " of " + e.clips.Length + " clips load, " + fmt + " mono 44.1 kHz, " + said.Count + " lines" + (all ? "" : " (NOT every line)") + ", " + e.channel);
            }
            Pool("vo_select_x_" + M, 4, null);
            Pool("vo_order_x_" + M + "_move", 4, null);
            Pool("vo_order_x_" + M + "_attack", 4, null);
            Pool("vo_order_x_" + M + "_repair", OzRepairLines.Lines.Length, OzRepairLines.Lines);
            Pool("vo_electro_stunned_" + M, 3, new[] { "Engine's fried!", "Systems frozen!", "Controls are dead!" });
            if (!ev.ContainsKey("vo_idle_x_" + M)) info("no idle jokes for the medium tank (vo_idle_x_" + M + "): its jokes need lines first; a quick triple click says a select line");
        }

        // ------------------------------------------------------------------ 7. the words
        static void Words(Action<bool, string> check, Action<string> info)
        {
            var ta = Resources.Load<TextAsset>("OpenZ/Text/changes_updates");
            var ch = ta ? JsonUtility.FromJson<Changes>(ta.text) : null;
            Card C(string head, string title) => ch?.sections?.FirstOrDefault(x => x != null && x.heading == head)?.entries?.FirstOrDefault(x => x != null && x.title == title);
            var own = C("New in 1.2", "The Medium Tank");
            check(own != null && !string.IsNullOrEmpty(own.now), "CHANGES & UPDATES: its own card \"The Medium Tank\": " + (own?.now ?? "missing"));
            foreach (var (h, t) in new[] { ("New in 1.2", "The CPU flies, repairs and defends"), ("New in 1.2", "More drivers to shoot"), ("Units", "Shooting the driver") })
            {
                var c = C(h, t);
                check(c != null && c.now.IndexOf("medium tank", StringComparison.OrdinalIgnoreCase) >= 0, "CHANGES & UPDATES: \"" + t + "\" names the medium tank");
            }
            var mech = C("Vehicles", "The Mech");
            check(mech != null && mech.in_z.StartsWith("Z had no Mech") && own != null && own.in_z.IndexOf("Mech", StringComparison.Ordinal) < 0,   // owner item 186: Z never had mechs
                  "CHANGES & UPDATES: \"The Mech\" (in Z) is an original unit, \"The Medium Tank\" (in Z) no longer names the Mech: " + mech?.in_z + " | " + own?.in_z);
            check(mech != null && mech.now.IndexOf("medium tank", StringComparison.OrdinalIgnoreCase) < 0 && mech.now.Contains("rockets") && mech.now.Contains("machine gun"),
                  "CHANGES & UPDATES: \"The Mech\" (now): its two kinds of weapons, never called a medium tank: " + mech?.now);
            var menu = Find("OpenZ.OpenZMainMenu");
            string[] A = menu?.GetField("CreditsOrigA", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as string[];
            string[] B = menu?.GetField("CreditsOrigB", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as string[];
            var all = (A ?? new string[0]).Concat(B ?? new string[0]).ToList();
            check(A != null && B != null && all.Any(x => x.Contains("<b>The Medium Tank</b>")) && !all.Any(x => x.Contains("walking medium tank")) && A.Length == B.Length,
                  "Credits, ORIGINAL CONTENT: " + (A?.Length ?? 0) + " + " + (B?.Length ?? 0) + " items, The Medium Tank listed, the Mechanoid no longer called a medium tank");
        }
    }
}
