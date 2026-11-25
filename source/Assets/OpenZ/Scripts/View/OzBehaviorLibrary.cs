using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>Named behavior presets and editor drafts are independent of the selected next-match profiles.</summary>
    public static class OzBehaviorLibrary
    {
        public const string TanksLabel = "TANK BEHAVIOR", RobotsLabel = "ROBOT BEHAVIOR";
        internal const string ClassicStatus = "NOT READY YET";
        const string LibraryKey = "oz_behavior_library_v1";
        [Serializable] sealed class Saved { public string[] profiles = Array.Empty<string>(); }
        static List<ZBehaviorProfile> saved;
        static readonly Dictionary<ZBehaviorFamily, string> sessionSelection = new Dictionary<ZBehaviorFamily, string>();
        public static string LastError;
        public static List<ZBehaviorProfile> SavedProfiles
        {
            get
            {
                if (saved != null) return saved;
                saved = new List<ZBehaviorProfile>();
                try
                {
                    var data = JsonUtility.FromJson<Saved>(PlayerPrefs.GetString(LibraryKey, "{}"));
                    foreach (var text in data?.profiles ?? Array.Empty<string>())
                    {
                        try { var p = ZBehaviorProfile.Decode(text); if (p.id.Length > 0) saved.Add(p); }
                        catch (Exception e) { Debug.LogWarning("[OpenZ Behavior] Ignored invalid preset: " + e.Message); }
                    }
                }
                catch (Exception e) { Debug.LogWarning("[OpenZ Behavior] Could not read presets: " + e.Message); }
                return saved;
            }
        }
        public static ZBehaviorProfile Native(ZBehaviorFamily f) => new ZBehaviorProfile { family = f, id = "native", name = "OpenZ" };
        public static ZBehaviorProfile Zod(ZBehaviorFamily f) => ZBehaviorProfile.Classic(f);
        public static List<ZBehaviorProfile> Choices(ZBehaviorFamily f)
        {
            var list = new List<ZBehaviorProfile> { Native(f), Zod(f) };
            // Retain a previously selected legacy reference locally, while fresh
            // installations provide only the two published reference profiles.
            string selected = SelectedId(f);
            if (selected == "zod-local")
            {
                var local = new ZBehaviorProfile { family = f, id = selected, name = "Zod Local (legacy)", master = 100 };
                foreach (var k in new[] { ZBehaviorKey.Move, ZBehaviorKey.AttackOrder, ZBehaviorKey.Retreat, ZBehaviorKey.ForceMove, ZBehaviorKey.Approach }) local.Set("", k, 0);
                list.Add(local);
            }
            if (f == ZBehaviorFamily.Robots && selected == "retreat-dodge")
            {
                var retreat = Native(f); retreat.id = "retreat-dodge"; retreat.name = "Retreat Dodge";
                retreat.Set("", ZBehaviorKey.Retreat, 1); retreat.Set("", ZBehaviorKey.StandFirm, 0);
                retreat.Set("", ZBehaviorKey.BreakForRetreat, 1); retreat.Set("", ZBehaviorKey.Cooldown, 1);
                retreat.Set("", ZBehaviorKey.ShootFirst, 0); list.Add(retreat);
            }
            foreach (var p in SavedProfiles) if (p.family == f) list.Add(p.Clone());
            return list;
        }
        internal static string SelectedKey(ZBehaviorFamily f) => "oz_behavior_selected_" + (int)f;
        public static string SelectedId(ZBehaviorFamily f) => sessionSelection.TryGetValue(f, out var id) ? id : "native";
        public static ZBehaviorProfile Selected(ZBehaviorFamily f) => Choices(f).Find(p => p.id == SelectedId(f)) ?? Native(f);
        public static string SelectedName(ZBehaviorFamily f)
        {
            string id = SelectedId(f);
            if (id == "native") return "OpenZ  v";
            if (id == "zod-classic") return "Z Classic  v";
            if (id == "zod-local") return "Zod Local (idle only)  v";
            if (id == "retreat-dodge" && f == ZBehaviorFamily.Robots) return "Retreat Dodge  v";
            return (SavedProfiles.Find(p => p.id == id && p.family == f)?.name ?? "OpenZ Native") + "  v";
        }
        public static void Reload() { saved = null; }
        public static void Select(ZBehaviorProfile p)
        { sessionSelection[p.family] = p.id; PlayerPrefs.SetString(SelectedKey(p.family), p.id); PlayerPrefs.Save(); }

        internal static void ExperimentalIcon(Transform parent, float x, float y, Color color)
        {
            void Stroke(float x1, float y1, float x2, float y2)
            {
                var line = Hud.Img(parent, "Experimental flask", color);
                line.raycastTarget = false;
                float dx = x2 - x1, dy = y2 - y1;
                line.rectTransform.TL(x + x1, y + y1, Mathf.Sqrt(dx * dx + dy * dy), 2);
                line.rectTransform.pivot = new Vector2(0, .5f);
                line.rectTransform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            }
            Stroke(6, 1, 14, 1); Stroke(8, 1, 8, 7); Stroke(12, 1, 12, 7);
            Stroke(8, 7, 2, 17); Stroke(12, 7, 18, 17); Stroke(2, 17, 18, 17);
            Stroke(5, 13, 15, 13);
        }

        internal sealed class CombatProfile
        {
            internal readonly ZBehaviorProfile Tanks, Robots;
            internal string Name => Tanks.id == "native" ? Robots.name : Tanks.name;
            internal CombatProfile(ZBehaviorProfile tanks, ZBehaviorProfile robots) { Tanks = tanks; Robots = robots; }
            internal bool Matches(string tanks, string robots) => Tanks.id == tanks && Robots.id == robots;
        }

        internal static CombatProfile Standard(bool classic) => Pair(
            classic ? Zod(ZBehaviorFamily.Tanks) : Native(ZBehaviorFamily.Tanks),
            classic ? Zod(ZBehaviorFamily.Robots) : Native(ZBehaviorFamily.Robots));

        internal static CombatProfile Pair(ZBehaviorProfile tanks, ZBehaviorProfile robots) => new CombatProfile(tanks, robots);

        internal static List<CombatProfile> CustomProfiles() => SavedProfiles.GroupBy(p => p.id).Select(group =>
            Pair(group.FirstOrDefault(p => p.family == ZBehaviorFamily.Tanks) ?? Native(ZBehaviorFamily.Tanks),
                group.FirstOrDefault(p => p.family == ZBehaviorFamily.Robots) ?? Native(ZBehaviorFamily.Robots))).ToList();

        internal static void SelectCombat(CombatProfile profile)
        {
            sessionSelection[ZBehaviorFamily.Tanks] = profile.Tanks.id;
            sessionSelection[ZBehaviorFamily.Robots] = profile.Robots.id;
            PlayerPrefs.SetString(SelectedKey(ZBehaviorFamily.Tanks), profile.Tanks.id);
            PlayerPrefs.SetString(SelectedKey(ZBehaviorFamily.Robots), profile.Robots.id);
            PlayerPrefs.Save();
        }

        internal static void SaveCombat(CombatProfile profile, string name, bool copy)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > 80) throw new ArgumentException("Use a preset name between 1 and 80 characters.");
            string current = profile.Tanks.id == "native" ? profile.Robots.id : profile.Tanks.id;
            bool update = !copy && CustomProfiles().Any(p => p.Matches(profile.Tanks.id, profile.Robots.id));
            string id = update ? current : Guid.NewGuid().ToString("N");
            var frozen = new List<ZBehaviorProfile>();
            foreach (var part in new[] { profile.Tanks, profile.Robots })
            {
                var value = part.Clone();
                value.id = id;
                value.name = name;
                value.FreezeReferences();
                frozen.Add(value);
            }
            if (update)
            {
                // Legacy saved profiles can have only one component. Preserve
                // the selected behavior until the edited profile is selected.
                foreach (var value in frozen)
                {
                    if (SavedProfiles.Any(p => p.id == id && p.family == value.family)) continue;
                    if (SelectedId(value.family) == id) Select(Native(value.family));
                }
                SavedProfiles.RemoveAll(p => p.id == id);
            }
            SavedProfiles.AddRange(frozen);
            profile.Tanks.id = profile.Robots.id = id;
            profile.Tanks.name = profile.Robots.name = name;
            Persist();
        }

        internal static void DeleteCombat(CombatProfile profile)
        {
            foreach (var part in new[] { profile.Tanks, profile.Robots })
                if (SelectedId(part.family) == part.id) Select(Native(part.family));
            SavedProfiles.RemoveAll(p => p.id == profile.Tanks.id || p.id == profile.Robots.id);
            Persist();
        }

        [Serializable] sealed class JsonCombatProfile
        {
            public string format = "openz-combat-dynamics";
            public int version = 2;
            public string name;
            public JsonProfile tanks, robots;
        }

        internal static string ExportCombat(CombatProfile profile)
        {
            var data = new JsonCombatProfile { name = profile.Name,
                tanks = JsonUtility.FromJson<JsonProfile>(EncodeJson(profile.Tanks)),
                robots = JsonUtility.FromJson<JsonProfile>(EncodeJson(profile.Robots)) };
            return WriteExport(profile.Tanks, ".json", JsonUtility.ToJson(data, true), profile.Name);
        }

        internal static CombatProfile ImportCombat(string path)
        {
            if (new FileInfo(path).Length > 2000000) throw new IOException("Preset file is too large.");
            string text = File.ReadAllText(path);
            if (text.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                var data = JsonUtility.FromJson<JsonCombatProfile>(text);
                if (data?.version == 2 && data.format == "openz-combat-dynamics")
                {
                    if (data.tanks?.family != "tanks" || data.robots?.family != "robots")
                        throw new FormatException("The combat profile must contain both unit groups.");
                    var tanks = DecodeJson(JsonUtility.ToJson(data.tanks));
                    var robots = DecodeJson(JsonUtility.ToJson(data.robots));
                    tanks.name = robots.name = string.IsNullOrWhiteSpace(data.name) ? tanks.name : data.name;
                    return Pair(tanks, robots);
                }
            }
            var legacy = Import(path);
            return legacy.family == ZBehaviorFamily.Tanks ? Pair(legacy, Native(ZBehaviorFamily.Robots)) :
                Pair(Native(ZBehaviorFamily.Tanks), legacy);
        }
        public static void Save(ZBehaviorProfile working, string name, bool copy)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > 80) throw new ArgumentException("Use a preset name between 1 and 80 characters.");
            bool exists = SavedProfiles.Exists(p => p.id == working.id && p.family == working.family);
            if (copy || !exists) working.id = Guid.NewGuid().ToString("N");
            working.name = name;
            // Freeze the endpoints while retaining the master/override relationship for subsequent editing.
            var frozen = working.Clone();
            frozen.FreezeReferences();
            SavedProfiles.RemoveAll(p => p.id == working.id); SavedProfiles.Add(frozen);
            Persist();
        }
        public static void Delete(ZBehaviorProfile p)
        {
            SavedProfiles.RemoveAll(x => x.id == p.id); Persist();
            if (SelectedId(p.family) == p.id) Select(Native(p.family));
        }
        static void Persist()
        {
            var data = new Saved { profiles = SavedProfiles.ConvertAll(p => p.Encode()).ToArray() };
            PlayerPrefs.SetString(LibraryKey, JsonUtility.ToJson(data)); PlayerPrefs.Save();
        }
        public static void SaveDraft(ZBehaviorProfile p) => PlayerPrefs.SetString("oz_behavior_draft_" + (int)p.family, p.Encode());
        public static ZBehaviorProfile Draft(ZBehaviorFamily f)
        {
            try { string s = PlayerPrefs.GetString("oz_behavior_draft_" + (int)f, ""); return s.Length == 0 ? Selected(f) : ZBehaviorProfile.Decode(s); }
            catch (Exception) { return Native(f); }
        }
        public static string Folder => Path.Combine(OzUeFiles.Folder, "BehaviorPresets");
        [Serializable] sealed class JsonSetting { public string type, key; public double value; }
        [Serializable] sealed class JsonProfile
        {
            public string format = "openz-combat-dynamics";
            public int version = 1;
            public string family, name;
            public double master;
            public JsonSetting[] overrides, openZReferences, zodReferences;
        }
        public static string EncodeJson(ZBehaviorProfile profile) => ZBehaviorJson.EncodeJson(profile);
        public static ZBehaviorProfile DecodeJson(string text) => ZBehaviorJson.DecodeJson(text);
        public static string ExportJson(ZBehaviorProfile p) => WriteExport(p, ".json", EncodeJson(p));
        public static string Export(ZBehaviorProfile p)
            => WriteExport(p, ".ozbehavior", p.Encode());
        static string WriteExport(ZBehaviorProfile p, string extension, string contents, string name = null)
        {
            Directory.CreateDirectory(Folder);
            name ??= p.name;
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            if (name.Length == 0 || name == "." || name == "..") name = "Preset";
            string path = Path.Combine(Folder, name + extension);
            string temp = path + ".tmp"; File.WriteAllText(temp, contents);
            if (File.Exists(path)) File.Delete(path); File.Move(temp, path); return path;
        }
        public static ZBehaviorProfile Import(string path)
        {
            if (new FileInfo(path).Length > 2000000) throw new IOException("Preset file is too large.");
            string text = File.ReadAllText(path);
            var p = text.TrimStart().StartsWith("{", StringComparison.Ordinal) ? DecodeJson(text) : ZBehaviorProfile.Decode(text);
            p.id = ""; return p;
        }
        public static void AtWorldStart(ZGame game)
        {
            if (game.World == null || OzUnitEditor.IsPlayground(game.gameObject.scene)) return;
            // Both multiplayer seats use the frozen lobby profiles installed by ConfigureDuel.
            if (OpenZ.Multiplayer.DuelRuntime.Active) return;
            game.World.SetBehavior(Selected(ZBehaviorFamily.Tanks)); game.World.SetBehavior(Selected(ZBehaviorFamily.Robots));
        }
    }
}
